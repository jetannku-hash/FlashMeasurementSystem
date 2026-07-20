using System;
using System.Collections.Generic;

namespace FlashMeasurementSystem.Domain.HoleArrayAnalysis
{
    /// <summary>
    /// 純孔陣列分析（無 HALCON）。孔質心 → 主軸擬合（2×2 共變異的主特徵向量，支援傾斜網格）
    /// → 沿主軸 u / 次軸 v 投影分群成 rows×cols 網格 → 孔距 + 理想節點位置偏差 + 孔徑 + 六條件判定。
    /// 判定全在此層（pixelSizeUm 由呼叫端傳入，px 轉 mm），合成質心可全驗。
    /// </summary>
    public static class HoleArrayAnalyzer
    {
        public static HoleArrayAnalysisResult Analyze(
            IList<HoleArrayPoint> holes, double pixelSizeUm, HoleArrayAnalysisParameters parameters)
        {
            var p = parameters ?? HoleArrayAnalysisParameters.Default();
            if (holes == null || holes.Count < 2)
                return HoleArrayAnalysisResult.Failed("孔數不足（需 ≥ 2 個孔）");
            if (pixelSizeUm <= 0)
                return HoleArrayAnalysisResult.Failed("像素尺寸無效");
            if (p.Rows < 1 || p.Cols < 1)
                return HoleArrayAnalysisResult.Failed("網格列/行數無效（需 ≥ 1）");

            int n = holes.Count;
            double mmPerPx = pixelSizeUm / 1000.0;

            // 質心（＝理想網格原點）
            double meanRow = 0, meanCol = 0;
            foreach (HoleArrayPoint pt in holes) { meanRow += pt.Row; meanCol += pt.Col; }
            meanRow /= n; meanCol /= n;

            // 2×2 共變異矩陣 [[a,b],[b,c]]（a=Row 變異、c=Col 變異、b=交叉）
            double a = 0, b = 0, c = 0;
            foreach (HoleArrayPoint pt in holes)
            {
                double dr = pt.Row - meanRow, dc = pt.Col - meanCol;
                a += dr * dr; b += dr * dc; c += dc * dc;
            }
            a /= n; b /= n; c /= n;

            // 主特徵向量（較大特徵值）：λ1 = (a+c)/2 + sqrt(((a-c)/2)²+b²)
            double half = (a - c) / 2.0;
            double lambda1 = (a + c) / 2.0 + Math.Sqrt(half * half + b * b);
            double ur, uc; // 主軸單位方向（Row, Col）
            if (Math.Abs(b) > 1e-12)
            {
                ur = b; uc = lambda1 - a;
            }
            else
            {
                // 對角化：軸對齊，取變異較大的軸
                if (a >= c) { ur = 1; uc = 0; } else { ur = 0; uc = 1; }
            }
            double norm = Math.Sqrt(ur * ur + uc * uc);
            if (norm < 1e-12)
                return HoleArrayAnalysisResult.Failed("孔質心退化（無法擬合主軸）");
            ur /= norm; uc /= norm;
            double vr = -uc, vc = ur; // 次軸＝主軸法向

            // PCA 主軸只代表「展幅最大」的方向，未必是行(X)方向：高瘦網格（Y 展幅 > X 展幅）
            // 時主軸會落在列方向。故兩種軸指派都試，取網格擬合殘差(RMS)較小者。
            GridFit fitA = FitGrid(holes, meanRow, meanCol, ur, uc, vr, vc, p.Cols, p.Rows);
            GridFit fitB = FitGrid(holes, meanRow, meanCol, vr, vc, ur, uc, p.Cols, p.Rows);
            GridFit fit = ChooseFit(fitA, fitB, mmPerPx, p);

            int[] colIdx = fit.ColIdx;
            int[] rowIdx = fit.RowIdx;
            double pitchUPx = fit.PitchUPx;
            double pitchVPx = fit.PitchVPx;
            double maxPosDevPx = fit.MaxDevPx;

            // 孔徑（px → mm）
            double meanDiaMm = 0;
            foreach (HoleArrayPoint pt in holes) meanDiaMm += pt.DiameterPx * mmPerPx;
            meanDiaMm /= n;
            // 對「標稱」孔徑取最大偏差（不是對均值）：規格是「每一顆孔都要在公差內」，
            // 對均值取偏差會讓單顆嚴重超規的孔被平均掉而假 PASS。
            double diaMaxDevMm = 0;
            foreach (HoleArrayPoint pt in holes)
            {
                double d = Math.Abs(pt.DiameterPx * mmPerPx - p.NominalDiameterMm);
                if (d > diaMaxDevMm) diaMaxDevMm = d;
            }

            var result = new HoleArrayAnalysisResult
            {
                Success = true,
                HoleCount = n,
                MeanDiameterMm = meanDiaMm,
                DiameterMaxDevMm = diaMaxDevMm,
                PitchXMm = pitchUPx * mmPerPx,
                PitchYMm = pitchVPx * mmPerPx,
                MaxPositionDevMm = maxPosDevPx * mmPerPx
            };

            // 依 (列索引, 行索引) 排序輸出，讓下游 overlay 順序穩定
            var order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            Array.Sort(order, (x, y) =>
            {
                int cmp = rowIdx[x].CompareTo(rowIdx[y]);
                if (cmp != 0) return cmp;
                cmp = colIdx[x].CompareTo(colIdx[y]);
                return cmp != 0 ? cmp : x.CompareTo(y);
            });
            foreach (int i in order) result.Holes.Add(holes[i]);

            // 缺孔位置提示：列舉理想網格全部節點，沒有孔對應到的即為缺孔（比照 gear 缺齒 / PCD 缺孔）。
            // 用勝出指派的兩軸重建節點座標；缺孔會讓質心原點些微偏移，故為近似提示而非量測值。
            var occupied = new bool[p.Rows, p.Cols];
            for (int i = 0; i < n; i++)
            {
                if (rowIdx[i] >= 0 && rowIdx[i] < p.Rows && colIdx[i] >= 0 && colIdx[i] < p.Cols)
                    occupied[rowIdx[i], colIdx[i]] = true;
            }
            for (int j = 0; j < p.Rows; j++)
            {
                for (int i = 0; i < p.Cols; i++)
                {
                    if (occupied[j, i]) continue;
                    double du = (i - (p.Cols - 1) / 2.0) * pitchUPx;
                    double dv = (j - (p.Rows - 1) / 2.0) * pitchVPx;
                    result.MissingNodes.Add(new HoleArrayPoint
                    {
                        Row = meanRow + du * fit.ColAxisR + dv * fit.RowAxisR,
                        Col = meanCol + du * fit.ColAxisC + dv * fit.RowAxisC
                    });
                }
            }

            // 判定（單行/單列時該方向孔距不判定）
            result.CountOk = n == p.Rows * p.Cols;
            result.DiameterOk = Math.Abs(result.MeanDiameterMm - p.NominalDiameterMm) <= p.DiameterToleranceMm;
            result.DiameterMaxDevOk = result.DiameterMaxDevMm <= p.DiameterToleranceMm;
            result.PitchXOk = p.Cols <= 1 || Math.Abs(result.PitchXMm - p.NominalPitchXMm) <= p.PitchToleranceMm;
            result.PitchYOk = p.Rows <= 1 || Math.Abs(result.PitchYMm - p.NominalPitchYMm) <= p.PitchToleranceMm;
            result.PositionOk = result.MaxPositionDevMm <= p.PositionToleranceMm;
            result.IsPass = result.CountOk && result.DiameterOk && result.DiameterMaxDevOk
                            && result.PitchXOk && result.PitchYOk && result.PositionOk;
            return result;
        }

        /// <summary>
        /// 在兩種軸指派間挑一個。殘差(RMS)是主要判準：Rows != Cols 時錯誤的指派會被迫用錯的
        /// 群數切分，殘差明顯變大，足以分辨。
        /// 但 Rows == Cols 時兩種指派都能完美重建網格（殘差相同、完美網格下同為 0），殘差失去
        /// 鑑別力，之前一律退回 fitA＝PCA 主軸＝「展幅較大」的方向，孔距不同的正方網格就會
        /// X/Y 對調（真值 X 1.0 / Y 2.0 會報成 X 2.0 / Y 1.0）。
        /// 故殘差打平時改用 nominal 孔距當第二判準：取 |pitchU−NominalX| + |pitchV−NominalY|
        /// 較小的一組。兩個 nominal 都是 0（無標稱可依據）時維持原本的 PCA 主軸順序。
        /// </summary>
        private static GridFit ChooseFit(GridFit fitA, GridFit fitB, double mmPerPx,
            HoleArrayAnalysisParameters p)
        {
            // 相對容忍：真實影像有雜訊，正方網格的兩組殘差只會「幾乎」相等而非完全相等。
            double tie = 1e-9 + 0.01 * Math.Max(fitA.RmsDevPx, fitB.RmsDevPx);
            if (Math.Abs(fitA.RmsDevPx - fitB.RmsDevPx) > tie)
                return fitA.RmsDevPx <= fitB.RmsDevPx ? fitA : fitB;

            if (p.NominalPitchXMm <= 0 && p.NominalPitchYMm <= 0)
                return fitA; // 無標稱孔距可導引 → 沿用 PCA 主軸順序

            double errA = Math.Abs(fitA.PitchUPx * mmPerPx - p.NominalPitchXMm)
                        + Math.Abs(fitA.PitchVPx * mmPerPx - p.NominalPitchYMm);
            double errB = Math.Abs(fitB.PitchUPx * mmPerPx - p.NominalPitchXMm)
                        + Math.Abs(fitB.PitchVPx * mmPerPx - p.NominalPitchYMm);
            return errB < errA ? fitB : fitA;
        }

        /// <summary>單一軸指派下的網格擬合結果。</summary>
        private sealed class GridFit
        {
            public int[] ColIdx;
            public int[] RowIdx;
            public double PitchUPx; // 沿行方向軸（X）的相鄰群心平均間距
            public double PitchVPx; // 沿列方向軸（Y）的相鄰群心平均間距
            public double MaxDevPx;
            public double RmsDevPx;
            // 本指派實際採用的兩軸（勝出者才知道哪個是行/列方向），供缺孔節點定位重建理想網格。
            public double ColAxisR, ColAxisC, RowAxisR, RowAxisC;
        }

        /// <summary>
        /// 給定一組 (行方向軸 uR/uC, 列方向軸 vR/vC)：投影 → 分群 → 理想節點殘差。
        /// 節點(i,j) = 質心 + (i−(cols−1)/2)·pitchU·u + (j−(rows−1)/2)·pitchV·v。
        /// </summary>
        private static GridFit FitGrid(IList<HoleArrayPoint> holes, double meanRow, double meanCol,
            double uR, double uC, double vR, double vC, int cols, int rows)
        {
            int n = holes.Count;
            var av = new double[n];
            var bv = new double[n];
            for (int i = 0; i < n; i++)
            {
                double dr = holes[i].Row - meanRow, dc = holes[i].Col - meanCol;
                av[i] = dr * uR + dc * uC;
                bv[i] = dr * vR + dc * vC;
            }

            // 沿行方向軸取最大的 (cols−1) 個間隙切成 cols 群；沿列方向軸同理切 rows 群
            double pitchU, pitchV;
            int[] colIdx = Cluster(av, cols, out pitchU);
            int[] rowIdx = Cluster(bv, rows, out pitchV);

            double maxDev = 0, sumSq = 0;
            for (int i = 0; i < n; i++)
            {
                double du = (colIdx[i] - (cols - 1) / 2.0) * pitchU;
                double dv = (rowIdx[i] - (rows - 1) / 2.0) * pitchV;
                double idealRow = meanRow + du * uR + dv * vR;
                double idealCol = meanCol + du * uC + dv * vC;
                double dr = holes[i].Row - idealRow, dc = holes[i].Col - idealCol;
                double dev = Math.Sqrt(dr * dr + dc * dc);
                if (dev > maxDev) maxDev = dev;
                sumSq += dev * dev;
            }

            return new GridFit
            {
                ColAxisR = uR, ColAxisC = uC, RowAxisR = vR, RowAxisC = vC,
                ColIdx = colIdx,
                RowIdx = rowIdx,
                PitchUPx = pitchU,
                PitchVPx = pitchV,
                MaxDevPx = maxDev,
                RmsDevPx = Math.Sqrt(sumSq / n)
            };
        }

        /// <summary>
        /// 把一維投影值切成 groups 群：排序後取最大的 (groups−1) 個間隙當分界。
        /// 回傳各點的群索引（依投影值由小到大 0..groups−1），並輸出相鄰群心的平均間距。
        /// groups==1 或點數不足以切這麼多刀時自動退化（孔數不符由 CountOk 攔截）。
        /// </summary>
        private static int[] Cluster(double[] values, int groups, out double pitch)
        {
            int n = values.Length;
            var idx = new int[n];
            for (int i = 0; i < n; i++) idx[i] = i;
            Array.Sort(idx, (x, y) => values[x].CompareTo(values[y]));

            int splits = Math.Min(groups - 1, n - 1);
            var isSplit = new bool[Math.Max(n - 1, 0)]; // isSplit[k]：排序後第 k 與 k+1 點之間切一刀
            if (splits > 0)
            {
                var gapOrder = new int[n - 1];
                for (int k = 0; k < n - 1; k++) gapOrder[k] = k;
                Array.Sort(gapOrder, (x, y) =>
                {
                    double gx = values[idx[x + 1]] - values[idx[x]];
                    double gy = values[idx[y + 1]] - values[idx[y]];
                    int cmp = gy.CompareTo(gx); // 間隙由大到小
                    return cmp != 0 ? cmp : x.CompareTo(y);
                });
                for (int k = 0; k < splits; k++) isSplit[gapOrder[k]] = true;
            }

            var group = new int[n];
            var sum = new List<double>();
            var count = new List<int>();
            int g = 0;
            sum.Add(0); count.Add(0);
            for (int k = 0; k < n; k++)
            {
                int orig = idx[k];
                group[orig] = g;
                sum[g] += values[orig]; count[g]++;
                if (k < n - 1 && isSplit[k]) { g++; sum.Add(0); count.Add(0); }
            }

            // 相鄰群心平均間距（＝頭尾群心距 / 群數−1）
            pitch = 0;
            int gc = sum.Count;
            if (gc > 1)
            {
                double total = 0;
                for (int k = 0; k < gc - 1; k++)
                    total += (sum[k + 1] / count[k + 1]) - (sum[k] / count[k]);
                pitch = total / (gc - 1);
            }
            return group;
        }
    }
}
