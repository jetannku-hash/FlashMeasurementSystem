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

        public void LoadModel(string modelFilePath)
        {
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
            try
            {
                if (searchRegion != null)
                {
                    searchImage = image.ReduceDomain(searchRegion);
                    ownsSearchImage = true;
                }
                else
                {
                    searchImage = image;
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
        }
    }
}
