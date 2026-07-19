using System;
using System.Globalization;
using System.IO;
using FlashMeasurementSystem.Application.MetrologyModel;
using FlashMeasurementSystem.Domain.MetrologyModel;
using HalconDotNet;

namespace FlashMeasurementSystem.Halcon.MetrologyModel
{
    /// <summary>
    /// HALCON 2D 量測模型適配器（唯一放 metrology HOperatorSet 呼叫之處）。
    /// 每次 Apply 都重新 create → … → clear（try/finally，不留 handle）。
    /// 生命週期順序：create → set_image_size → (reference_system) → add objects → (align) → apply → query → clear。
    /// 操作簽章對照 halcon_pdf/reference（見 02-RESEARCH.md「Verbatim Operator Signatures」）。
    /// </summary>
    public class HalconMetrologyModelRunner : IMetrologyModelRunner<HImage>
    {
        public MetrologyModelResult Apply(
            MetrologyModelDef model,
            double refRow, double refCol, double refAngleRad, bool hasReferencePose,
            HImage image,
            double matchRow, double matchCol, double matchAngleRad, bool hasMatch)
        {
            var result = new MetrologyModelResult();
            if (model == null || model.Objects == null || model.Objects.Count == 0)
                return result;

            HTuple handle = null;
            bool handleCreated = false;
            HObject grayCopy = null;
            try
            {
                // 1) 單通道保證（多通道 apply 會靜默回零結果，與 measure_pos 同坑）。
                HObject applyImage = image;
                HOperatorSet.CountChannels(image, out HTuple channels);
                if (channels.I > 1)
                {
                    HOperatorSet.Rgb1ToGray(image, out grayCopy);
                    applyImage = grayCopy;
                }

                // 2) 建模
                HOperatorSet.CreateMetrologyModel(out handle);
                handleCreated = true;

                // 3) 影像尺寸（必須在 add 之前）：用 def 提示，否則即時查詢。
                int w = model.ImageWidth;
                int h = model.ImageHeight;
                if (w <= 0 || h <= 0)
                {
                    HOperatorSet.GetImageSize(applyImage, out HTuple iw, out HTuple ih);
                    w = iw.I; h = ih.I;
                }
                HOperatorSet.SetMetrologyModelImageSize(handle, w, h);

                Log("APPLY START imgW=" + w + " imgH=" + h + " objects=" + model.Objects.Count
                    + " hasRef=" + hasReferencePose + " hasMatch=" + hasMatch);

                // 4) 參考座標系（reference_hdevelop.txt ~L7000：[row, column, angle]）。
                //    HasReferencePose=false 時略過，標稱幾何即絕對影像座標。
                if (hasReferencePose)
                    HOperatorSet.SetMetrologyModelParam(handle, "reference_system",
                        new HTuple(new double[] { refRow, refCol, refAngleRad }));

                // 5) 加入物件（逐物件隔離：驗證或 add 失敗只記為該物件失敗，不中斷整批）。
                int n = model.Objects.Count;
                var indices = new int[n];        // -1 = 未加入（驗證/add 失敗）
                var preFailures = new MetrologyObjectResult[n];
                for (int i = 0; i < n; i++)
                {
                    MetrologyObjectDef def = model.Objects[i];
                    indices[i] = -1;

                    string validationError = ValidateMeasureLength1(def);
                    if (validationError != null)
                    {
                        Log("ADD skip(validate) " + DescribeDef(def) + " err=" + validationError);
                        preFailures[i] = FailResult(def, validationError);
                        continue;
                    }

                    try
                    {
                        HTuple idx = AddObject(handle, def);
                        indices[i] = idx.I;
                        Log("ADD ok idx=" + indices[i] + " " + DescribeDef(def));
                        // 單一實例 → 結果 tuple 以每形狀固定長度解析（Pitfall 6）。
                        HOperatorSet.SetMetrologyObjectParam(handle, new HTuple(indices[i]), "num_instances", 1);
                        if (def.MeasureDistance > 0)
                            HOperatorSet.SetMetrologyObjectParam(handle, new HTuple(indices[i]), "measure_distance", def.MeasureDistance);
                        else if (def.NumMeasures > 0)
                            HOperatorSet.SetMetrologyObjectParam(handle, new HTuple(indices[i]), "num_measures", def.NumMeasures);
                        // 注意：刻意不呼叫 set_metrology_object_fuzzy_param（fuzzy 屬 Phase 3，Pitfall 9）。
                    }
                    catch (HalconException ex)
                    {
                        indices[i] = -1;
                        Log("ADD fail " + DescribeDef(def) + " ex=" + ex.Message);
                        preFailures[i] = FailResult(def, "add 失敗: " + ex.Message);
                    }
                }

                // 6) 對齊（僅在有參考姿態且有匹配時；傳絕對匹配座標，非差量 — Pitfall 7）。
                if (hasReferencePose && hasMatch)
                    HOperatorSet.AlignMetrologyModel(handle, matchRow, matchCol, matchAngleRad);

                // 7) 套用（單次處理所有物件 — MET2D-04）。
                HOperatorSet.ApplyMetrologyModel(applyImage, handle);

                // 8) 查詢結果。
                for (int i = 0; i < n; i++)
                {
                    if (preFailures[i] != null)
                    {
                        result.Objects.Add(preFailures[i]);
                        continue;
                    }
                    MetrologyObjectResult r = QueryResult(handle, indices[i], model.Objects[i]);
                    Log("RESULT " + r.Shape + " success=" + r.Success + " score=" + r.Score.ToString("F2")
                        + " value=" + r.ValueText + " msg=" + r.ErrorMessage);
                    result.Objects.Add(r);
                }
            }
            catch (HalconException ex)
            {
                Log("APPLY EXCEPTION " + ex.Message);
                result.ErrorMessage = ex.Message;
            }
            finally
            {
                if (handleCreated && handle != null)
                {
                    try { HOperatorSet.ClearMetrologyModel(handle); } catch (HalconException) { /* 釋放best-effort */ }
                }
                if (grayCopy != null) grayCopy.Dispose();
            }
            return result;
        }

        // 各形狀的 MeasureLength1 限制（reference：circle < Radius；ellipse < Radius1 且 < Radius2；
        // rectangle < Length1 且 < Length2；line 無限制）。違反回傳錯誤訊息，否則 null。
        private static string ValidateMeasureLength1(MetrologyObjectDef def)
        {
            switch (def.Shape)
            {
                case MetrologyObjectType.Circle:
                    if (def.MeasureLength1 >= def.Radius)
                        return string.Format("MeasureLength1 ({0}) 必須 < Radius ({1})", def.MeasureLength1, def.Radius);
                    break;
                case MetrologyObjectType.Ellipse:
                    if (def.MeasureLength1 >= def.Radius1 || def.MeasureLength1 >= def.Radius2)
                        return string.Format("MeasureLength1 ({0}) 必須 < Radius1 ({1}) 且 < Radius2 ({2})",
                            def.MeasureLength1, def.Radius1, def.Radius2);
                    break;
                case MetrologyObjectType.Rectangle:
                    if (def.MeasureLength1 >= def.Length1 || def.MeasureLength1 >= def.Length2)
                        return string.Format("MeasureLength1 ({0}) 必須 < Length1 ({1}) 且 < Length2 ({2})",
                            def.MeasureLength1, def.Length1, def.Length2);
                    break;
            }
            return null;
        }

        private static HTuple AddObject(HTuple handle, MetrologyObjectDef d)
        {
            HTuple empty = new HTuple();
            HTuple index;
            switch (d.Shape)
            {
                case MetrologyObjectType.Line:
                    HOperatorSet.AddMetrologyObjectLineMeasure(handle,
                        d.RowBegin, d.ColumnBegin, d.RowEnd, d.ColumnEnd,
                        d.MeasureLength1, d.MeasureLength2, d.MeasureSigma, d.MeasureThreshold,
                        empty, empty, out index);
                    break;
                case MetrologyObjectType.Circle:
                    HOperatorSet.AddMetrologyObjectCircleMeasure(handle,
                        d.Row, d.Column, d.Radius,
                        d.MeasureLength1, d.MeasureLength2, d.MeasureSigma, d.MeasureThreshold,
                        empty, empty, out index);
                    break;
                case MetrologyObjectType.Ellipse:
                    HOperatorSet.AddMetrologyObjectEllipseMeasure(handle,
                        d.Row, d.Column, d.Phi, d.Radius1, d.Radius2,
                        d.MeasureLength1, d.MeasureLength2, d.MeasureSigma, d.MeasureThreshold,
                        empty, empty, out index);
                    break;
                case MetrologyObjectType.Rectangle:
                    HOperatorSet.AddMetrologyObjectRectangle2Measure(handle,
                        d.Row, d.Column, d.Phi, d.Length1, d.Length2,
                        d.MeasureLength1, d.MeasureLength2, d.MeasureSigma, d.MeasureThreshold,
                        empty, empty, out index);
                    break;
                default:
                    throw new HalconException("未支援的量測物件型別: " + d.Shape);
            }
            return index;
        }

        private static MetrologyObjectResult QueryResult(HTuple handle, int index, MetrologyObjectDef def)
        {
            var r = new MetrologyObjectResult { Id = def.Id, Name = def.Name, Shape = def.Shape };
            try
            {
                HOperatorSet.GetMetrologyObjectResult(handle, new HTuple(index), "all",
                    "result_type", "all_param", out HTuple p);
                HOperatorSet.GetMetrologyObjectResult(handle, new HTuple(index), "all",
                    "result_type", "score", out HTuple score);
                r.Score = score != null && score.Length > 0 ? score.D : 0.0;

                // 量測區內找到的所有邊點（順序未定義，僅供顯示/佐證）。
                HObject measureContours = null;
                try
                {
                    HOperatorSet.GetMetrologyObjectMeasures(out measureContours, handle,
                        new HTuple(index), "all", out HTuple mRow, out HTuple mCol);
                    for (int k = 0; k < mRow.Length; k++) r.MeasurePointRows.Add(mRow[k].D);
                    for (int k = 0; k < mCol.Length; k++) r.MeasurePointCols.Add(mCol[k].D);
                }
                finally { if (measureContours != null) measureContours.Dispose(); }

                int need = ExpectedParamCount(def.Shape);
                if (p == null || p.Length < need)
                {
                    Log("FIT insufficient shape=" + def.Shape + " gotParams=" + (p == null ? 0 : p.Length)
                        + " need=" + need + " score=" + r.Score.ToString("F2"));
                    r.Success = false;
                    r.ErrorMessage = "未取得有效擬合（量測區無足夠邊點）";
                    return r;
                }

                switch (def.Shape)
                {
                    case MetrologyObjectType.Circle:
                        r.FitRow = p[0].D; r.FitColumn = p[1].D; r.FitRadius = p[2].D;
                        r.ValueText = string.Format("R={0:F2}px Score={1:F2}", r.FitRadius, r.Score);
                        break;
                    case MetrologyObjectType.Ellipse:
                        r.FitRow = p[0].D; r.FitColumn = p[1].D; r.FitPhi = p[2].D;
                        r.FitRadius1 = p[3].D; r.FitRadius2 = p[4].D;
                        r.ValueText = string.Format("R1={0:F2} R2={1:F2}px Score={2:F2}", r.FitRadius1, r.FitRadius2, r.Score);
                        break;
                    case MetrologyObjectType.Line:
                        r.FitRowBegin = p[0].D; r.FitColumnBegin = p[1].D;
                        r.FitRowEnd = p[2].D; r.FitColumnEnd = p[3].D;
                        r.ValueText = string.Format("Score={0:F2}", r.Score);
                        break;
                    case MetrologyObjectType.Rectangle:
                        r.FitRow = p[0].D; r.FitColumn = p[1].D; r.FitPhi = p[2].D;
                        r.FitLength1 = p[3].D; r.FitLength2 = p[4].D;
                        r.ValueText = string.Format("L1={0:F2} L2={1:F2}px Score={2:F2}", r.FitLength1, r.FitLength2, r.Score);
                        break;
                }
                r.Success = r.Score > 0.0;
            }
            catch (HalconException ex)
            {
                r.Success = false;
                r.ErrorMessage = "查詢結果失敗: " + ex.Message;
            }
            return r;
        }

        private static int ExpectedParamCount(MetrologyObjectType shape)
        {
            switch (shape)
            {
                case MetrologyObjectType.Circle: return 3;     // row, col, radius
                case MetrologyObjectType.Ellipse: return 5;    // row, col, phi, r1, r2
                case MetrologyObjectType.Line: return 4;       // rowBegin, colBegin, rowEnd, colEnd
                case MetrologyObjectType.Rectangle: return 5;  // row, col, phi, l1, l2
                default: return 0;
            }
        }

        private static MetrologyObjectResult FailResult(MetrologyObjectDef def, string message)
        {
            return new MetrologyObjectResult
            {
                Id = def.Id,
                Name = def.Name,
                Shape = def.Shape,
                Success = false,
                ErrorMessage = message,
                ValueText = message   // 讓結果表/疊加顯示失敗原因，而非空白
            };
        }

        // 診斷用：格式化物件的標稱幾何 + measure 參數（形狀相關欄位 + 共通量測參數）。
        private static string DescribeDef(MetrologyObjectDef d)
        {
            if (d == null) return "def=null";
            string geom;
            switch (d.Shape)
            {
                case MetrologyObjectType.Line:
                    geom = string.Format(CultureInfo.InvariantCulture,
                        "Line(RB={0:F1},CB={1:F1},RE={2:F1},CE={3:F1})",
                        d.RowBegin, d.ColumnBegin, d.RowEnd, d.ColumnEnd);
                    break;
                case MetrologyObjectType.Circle:
                    geom = string.Format(CultureInfo.InvariantCulture,
                        "Circle(R={0:F1},C={1:F1},Rad={2:F1})", d.Row, d.Column, d.Radius);
                    break;
                case MetrologyObjectType.Ellipse:
                    geom = string.Format(CultureInfo.InvariantCulture,
                        "Ellipse(R={0:F1},C={1:F1},Phi={2:F4},Rad1={3:F1},Rad2={4:F1})",
                        d.Row, d.Column, d.Phi, d.Radius1, d.Radius2);
                    break;
                case MetrologyObjectType.Rectangle:
                    geom = string.Format(CultureInfo.InvariantCulture,
                        "Rectangle(R={0:F1},C={1:F1},Phi={2:F4},L1={3:F1},L2={4:F1})",
                        d.Row, d.Column, d.Phi, d.Length1, d.Length2);
                    break;
                default:
                    geom = d.Shape.ToString();
                    break;
            }
            return string.Format(CultureInfo.InvariantCulture,
                "{0} measure(ML1={1},ML2={2},Sig={3},Thr={4},MeasDist={5},NumMeas={6})",
                geom, d.MeasureLength1, d.MeasureLength2, d.MeasureSigma, d.MeasureThreshold,
                d.MeasureDistance, d.NumMeasures);
        }

        // ─── 診斷 log（檔案 + Debug.WriteLine） ───────────────────────────
        private static readonly object _logLock = new object();
        private static string _cachedLogPath;
        private const long MaxLogBytes = 5L * 1024 * 1024; // 超過 5MB 自動截斷

        private static string GetLogPath()
        {
            if (_cachedLogPath != null) return _cachedLogPath;
            try
            {
                var current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                while (current != null)
                {
                    if (File.Exists(Path.Combine(current.FullName, "FlashMeasurementSystem.sln")))
                    {
                        var logsDir = Path.Combine(current.FullName, "data", "logs");
                        Directory.CreateDirectory(logsDir);
                        _cachedLogPath = Path.Combine(logsDir, "metrology.log");
                        return _cachedLogPath;
                    }
                    current = current.Parent;
                }
            }
            catch
            {
                // 任何路徑問題都退回到 TEMP
            }
            _cachedLogPath = Path.Combine(Path.GetTempPath(), "FlashMeasurementSystem_metrology.log");
            return _cachedLogPath;
        }

        private static void Log(string line)
        {
            string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
            string msg = "[" + stamp + "] " + line;
            System.Diagnostics.Debug.WriteLine(msg);
            try
            {
                string path = GetLogPath();
                lock (_logLock)
                {
                    if (File.Exists(path) && new FileInfo(path).Length > MaxLogBytes)
                    {
                        // 截斷時保留上一輪歷史到 .bak（除錯需要回溯），而非直接刪除。
                        // 先刪舊 .bak 再 Move，避免 Move 因目標已存在而拋例外。
                        string bak = path + ".bak";
                        if (File.Exists(bak)) File.Delete(bak);
                        File.Move(path, bak);
                    }
                    File.AppendAllText(path, msg + Environment.NewLine);
                }
            }
            catch
            {
                // log 永遠不可拋例外
            }
        }
    }
}
