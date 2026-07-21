using System;
using FlashMeasurementSystem.Application.TemplateMatching;
using FlashMeasurementSystem.Domain.TemplateMatching;
using HalconDotNet;

namespace FlashMeasurementSystem.Halcon.TemplateMatching
{
    public class HalconTemplateMatcher : ITemplateMatcher<HImage, HRegion>, IDisposable
    {
        private HTuple _modelID;
        private double _modelAngleStartRad;
        private double _modelAngleExtentRad;
        private string _loadedModelPath;

        public void LoadModel(string modelFilePath)
        {
            // 同一路徑已載入則略過重載：連續量測每個 part 都呼叫 LoadModel，否則每次都
            // ReadShapeModel（磁碟讀取 + 反序列化），是逐 part 的主要成本。注意：模板檔每次
            // 由 CreateTemplate 寫到新的時間戳路徑，故同路徑不會被改寫，快取不會取到舊模型。
            if (_modelID != null &&
                string.Equals(_loadedModelPath, modelFilePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            DisposeModel();
            HOperatorSet.ReadShapeModel(modelFilePath, out _modelID);

            HOperatorSet.GetShapeModelParams(_modelID,
                out HTuple _,
                out HTuple angleStart,
                out HTuple angleExtent,
                out HTuple _,
                out HTuple _,
                out HTuple _,
                out HTuple _,
                out HTuple _,
                out HTuple _
            );

            _modelAngleStartRad = angleStart.D;
            _modelAngleExtentRad = angleExtent.D;

            // 快取路徑「最後」才記錄。原本在 ReadShapeModel 之後、GetShapeModelParams 之前就指派，
            // 若讀取參數擲例外，角度範圍會停在 0，而路徑已被記下——之後重試同一個模板會被開頭的
            // 快取判斷直接 return，靜默沿用「不容許旋轉」的搜尋範圍，工件一旋轉就匹配不到，
            // 且沒有任何跡象指向真正的原因。放到最後，例外時快取不成立，重試會真的重載。
            _loadedModelPath = modelFilePath;
        }

        public TemplateMatchResult FindMatches(HImage image, HRegion searchRegion, TemplateMatchingParameters parameters)
        {
            var effectiveParams = parameters ?? TemplateMatchingParameters.Default();

            if (_modelID == null)
            {
                return new TemplateMatchResult { Found = false, Message = "請先載入模板" };
            }

            HImage searchImage = null;
            bool ownsSearchImage = false;
            HImage grayImage = null;
            try
            {
                // find_shape_model 要求單通道影像；彩色（多通道）圖直接傳入會拋 HalconException、
                // 每個 part 都回「模板匹配錯誤」。與其他 adapter 相同慣例：先轉單通道再 reduce/find。
                grayImage = EnsureSingleChannel(image);
                HImage baseImage = grayImage ?? image;

                if (searchRegion != null)
                {
                    searchImage = baseImage.ReduceDomain(searchRegion);
                    ownsSearchImage = true;
                }
                else
                {
                    searchImage = baseImage;
                }

                HOperatorSet.FindShapeModel(
                    searchImage,
                    _modelID,
                    new HTuple(_modelAngleStartRad),
                    new HTuple(_modelAngleExtentRad),
                    new HTuple(effectiveParams.MinScore),
                    new HTuple(effectiveParams.NumMatches),
                    new HTuple(effectiveParams.MaxOverlap),
                    new HTuple("none"),
                    new HTuple(0),
                    new HTuple(0.5),
                    out HTuple matchRow,
                    out HTuple matchCol,
                    out HTuple matchAngle,
                    out HTuple matchScore
                );

                if (matchScore.Length > 0)
                {
                    return new TemplateMatchResult
                    {
                        Found = true,
                        Row = matchRow[0].D,
                        Column = matchCol[0].D,
                        AngleDeg = matchAngle[0].D * 180.0 / Math.PI,
                        Score = matchScore[0].D,
                        Message = $"匹配成功 (score={matchScore[0].D:F4})"
                    };
                }

                return new TemplateMatchResult { Found = false, Message = "未找到匹配" };
            }
            catch (HalconException ex)
            {
                return new TemplateMatchResult { Found = false, Message = $"模板匹配錯誤：{ex.Message}" };
            }
            finally
            {
                if (ownsSearchImage) searchImage?.Dispose();
                grayImage?.Dispose();  // 僅在多通道轉換時新建，需釋放（單通道路徑為 null）
            }
        }
        /// <summary>
        /// 取得模板輪廓在匹配位置的變換後形狀，用於顯示疊加。
        /// 呼叫者擁有回傳的 HObject，須自行 Dispose。
        /// </summary>
        public HObject GetMatchContour(double row, double col, double angleDeg)
        {
            if (_modelID == null)
                throw new InvalidOperationException("請先載入模板");

            HObject modelContours = null;
            HTuple homMat2D = null;

            try
            {
                double angleRad = angleDeg * Math.PI / 180.0;
                HOperatorSet.GetShapeModelContours(out modelContours, _modelID, 1);
                HOperatorSet.VectorAngleToRigid(0, 0, 0, row, col, angleRad, out homMat2D);
                HOperatorSet.AffineTransContourXld(modelContours, out HObject transformed, homMat2D);
                return transformed;
            }
            finally
            {
                modelContours?.Dispose();
                homMat2D = null;
            }
        }

        public void Dispose()
        {
            DisposeModel();
        }

        private void DisposeModel()
        {
            if (_modelID != null)
            {
                HOperatorSet.ClearShapeModel(_modelID);
                _modelID = null;
            }
            _modelAngleStartRad = 0.0;
            _modelAngleExtentRad = 0.0;
            _loadedModelPath = null;
        }

        // 回傳 null 表示原圖已是單通道（直接用原圖）；非 null 為新建的單通道影像，由呼叫端 dispose。
        // 3 通道用 rgb1_to_gray（加權灰階），其他取第 1 通道。與其他 HALCON adapter 相同慣例。
        private static HImage EnsureSingleChannel(HImage source)
        {
            HOperatorSet.CountChannels(source, out HTuple channels);
            int channelCount = (channels != null && channels.Length > 0) ? channels.I : 1;
            if (channelCount <= 1) return null;
            return channelCount == 3 ? source.Rgb1ToGray() : source.AccessChannel(1);
        }
    }
}
