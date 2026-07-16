using System;
using FlashMeasurementSystem.Application.EdgeDetection;
using FlashMeasurementSystem.Domain.EdgeDetection;

namespace FlashMeasurementSystem.Tests
{
    public static class EdgeDetectionDomainTests
    {
        public static int Main()
        {
            EdgeDetectionParameters defaults = EdgeDetectionParameters.Default();
            AssertEqual(1.2, defaults.Sigma, "Default sigma");
            AssertEqual(25.0, defaults.Threshold, "Default threshold");
            AssertEqual("all", defaults.Polarity, "Default polarity");
            AssertEqual("all", defaults.EdgeSelector, "Default edge selector");
            AssertEqual(40.0, defaults.HighThreshold, "Default high threshold");

            var undefinedRoi = new EdgeDetectionRoi();
            AssertEqual(false, undefinedRoi.IsDefined, "Default ROI should be undefined");

            var roi = new EdgeDetectionRoi
            {
                CenterRow = 100.0,
                CenterCol = 200.0,
                Length1 = 250.0,
                Length2 = 50.0,
                AngleRad = 0.25
            };
            AssertEqual(true, roi.IsDefined, "ROI with positive lengths should be defined");

            EdgeDetectionRoi tinyRoi = EdgeDetectionRoi.FromBounds(100.0, 200.0, 102.0, 202.0);
            // HALCON requires Length1 >= 1 && Length2 >= 1
            AssertEqual(true, tinyRoi.IsDefined, "2px ROI should be defined");

            EdgeDetectionRoi subPixelRoi = EdgeDetectionRoi.FromBounds(100.0, 200.0, 100.5, 200.5);
            AssertEqual(false, subPixelRoi.IsDefined, "Sub-pixel ROI should not be HALCON-valid");
            // 橫長 ROI（colSpan=600 > rowSpan=400）→ 量水平邊緣 → Phi=π/2、major axis 沿 row
            EdgeDetectionRoi roiFromBounds = EdgeDetectionRoi.FromBounds(100.0, 200.0, 500.0, 800.0);
            AssertEqual(300.0, roiFromBounds.CenterRow, "ROI center row from bounds");
            AssertEqual(500.0, roiFromBounds.CenterCol, "ROI center col from bounds");
            AssertEqual(200.0, roiFromBounds.Length1, "Horizontal ROI Length1 should be row half span (along major axis)");
            AssertEqual(300.0, roiFromBounds.Length2, "Horizontal ROI Length2 should be col half span (perpendicular)");
            AssertEqual(Math.PI / 2.0, roiFromBounds.AngleRad, "Horizontal ROI angle should be π/2 to detect horizontal edges");
            AssertEqual(true, roiFromBounds.IsDefined, "ROI from bounds should be defined");

            // 縱長 ROI（rowSpan=600 > colSpan=400）→ 量垂直邊緣 → Phi=0、major axis 沿 col
            EdgeDetectionRoi tallRoi = EdgeDetectionRoi.FromBounds(100.0, 200.0, 700.0, 600.0);
            AssertEqual(400.0, tallRoi.CenterRow, "Tall ROI center row");
            AssertEqual(400.0, tallRoi.CenterCol, "Tall ROI center col");
            AssertEqual(200.0, tallRoi.Length1, "Tall ROI Length1 should be col half span (along major axis)");
            AssertEqual(300.0, tallRoi.Length2, "Tall ROI Length2 should be row half span (perpendicular)");
            AssertEqual(0.0, tallRoi.AngleRad, "Tall ROI angle should be 0 to detect vertical edges");

            var result = new EdgeResult();
            AssertEqual(false, result.Success, "New result should not default to success");
            AssertEqual(0, result.EdgePoints.Count, "New result should have empty edge list");
            AssertEqual(string.Empty, result.ErrorMessage, "New result should have empty error message");

            IEdgeDetector<object> detector = new FakeEdgeDetector();
            EdgeResult fakeResult = detector.DetectEdges(new object(), roi, defaults);
            AssertEqual(true, fakeResult.Success, "Fake detector should satisfy interface contract");

            // ─── Rectangular Measure Object enhancement tests ─────────────────────
            EdgeDetectionParameters p2 = EdgeDetectionParameters.Default();
            AssertEqual("nearest_neighbor", p2.Interpolation, "Default Interpolation");
            AssertEqual("single_edge", p2.MeasureMode, "Default MeasureMode");

            if (!EdgeDetectionParameters.IsSupportedInterpolation("nearest_neighbor"))
                throw new InvalidOperationException("nearest_neighbor should be supported");
            if (!EdgeDetectionParameters.IsSupportedInterpolation("bilinear"))
                throw new InvalidOperationException("bilinear should be supported");
            if (!EdgeDetectionParameters.IsSupportedInterpolation("bicubic"))
                throw new InvalidOperationException("bicubic should be supported");
            if (EdgeDetectionParameters.IsSupportedInterpolation("cubic"))
                throw new InvalidOperationException("cubic should not be supported");

            if (!EdgeDetectionParameters.IsSupportedMeasureMode("single_edge"))
                throw new InvalidOperationException("single_edge should be supported");
            if (!EdgeDetectionParameters.IsSupportedMeasureMode("edge_pair"))
                throw new InvalidOperationException("edge_pair should be supported");
            if (EdgeDetectionParameters.IsSupportedMeasureMode("multi_pair"))
                throw new InvalidOperationException("multi_pair should not be supported");

            // FromCenter factory
            EdgeDetectionRoi centerRoi = EdgeDetectionRoi.FromCenter(150.0, 250.0, 80.0, 30.0, 0.5);
            AssertEqual(150.0, centerRoi.CenterRow, "FromCenter CenterRow");
            AssertEqual(250.0, centerRoi.CenterCol, "FromCenter CenterCol");
            AssertEqual(80.0, centerRoi.Length1, "FromCenter Length1");
            AssertEqual(30.0, centerRoi.Length2, "FromCenter Length2");
            AssertEqual(0.5, centerRoi.AngleRad, "FromCenter AngleRad");
            AssertEqual(true, centerRoi.IsDefined, "FromCenter IsDefined");

            // FromCenter preserves negative values (the caller may pass NaN or negative;
            // IsDefined is the caller's responsibility)
            EdgeDetectionRoi invalidRoi = EdgeDetectionRoi.FromCenter(0, 0, 0, 0, 0);
            AssertEqual(false, invalidRoi.IsDefined, "FromCenter zero lengths not defined");

            // EdgePair DTO defaults
            EdgePair pair = new EdgePair();
            AssertEqual(0.0, pair.FirstRow, "EdgePair default FirstRow");
            AssertEqual(0.0, pair.FirstColumn, "EdgePair default FirstColumn");
            AssertEqual(0.0, pair.FirstAmplitude, "EdgePair default FirstAmplitude");
            AssertEqual(0.0, pair.SecondRow, "EdgePair default SecondRow");
            AssertEqual(0.0, pair.SecondColumn, "EdgePair default SecondColumn");
            AssertEqual(0.0, pair.SecondAmplitude, "EdgePair default SecondAmplitude");
            AssertEqual(0.0, pair.IntraDistance, "EdgePair default IntraDistance");
            AssertEqual(0.0, pair.InterDistance, "EdgePair default InterDistance");

            // EdgeResult EdgePairs defaults
            EdgeResult result2 = new EdgeResult();
            AssertEqual(0, result2.EdgePairs.Count, "New result should have empty EdgePairs list");
            LineFittingDomainTests.Run();
            Console.WriteLine("LineFittingDomainTests passed");
            CircleFittingDomainTests.Run();
            Console.WriteLine("CircleFittingDomainTests passed");
            EllipseFittingDomainTests.Run();
            Console.WriteLine("EllipseFittingDomainTests passed");
            RectangleFittingDomainTests.Run();
            Console.WriteLine("RectangleFittingDomainTests passed");
            DistanceMeasurementDomainTests.Run();
            Console.WriteLine("DistanceMeasurementDomainTests passed");
            AngleMeasurementDomainTests.Run();
            Console.WriteLine("AngleMeasurementDomainTests passed");
            AngleNormalizerTests.Run();
            Console.WriteLine("AngleNormalizerTests passed");
            ToleranceDomainTests.Run();
            Console.WriteLine("ToleranceDomainTests passed");
            CoordinateSystemDomainTests.Run();
            Console.WriteLine("CoordinateSystemDomainTests passed");
            CalibrationDomainTests.Run();
            Console.WriteLine("CalibrationDomainTests passed");
            RoiDomainTests.Run();
            Console.WriteLine("RoiDomainTests passed");
            WorkflowDomainTests.Run();
            Console.WriteLine("WorkflowDomainTests passed");
            Rect2EditMathTests.Run();
            Console.WriteLine("Rect2EditMathTests passed");
            ArcEditMathTests.Run();
            Console.WriteLine("ArcEditMathTests passed");
            GeometryConstructionDomainTests.Run();
            Console.WriteLine("GeometryConstructionDomainTests passed");
            GdtCalculatorDomainTests.Run();
            Console.WriteLine("GdtCalculatorDomainTests passed");
            GdtEvaluationDomainTests.Run();
            Console.WriteLine("GdtEvaluationDomainTests passed");
            RecipeValidatorTests.Run();
            Console.WriteLine("RecipeValidatorTests passed");
            CsvReportWriterTests.Run();
            Console.WriteLine("CsvReportWriterTests passed");
            MetrologyModelDomainTests.Run();
            Console.WriteLine("MetrologyModelDomainTests passed");
            DxfComparisonDomainTests.Run();
            Console.WriteLine("DxfComparisonDomainTests passed");
            ArcRecipeToolDomainTests.Run();
            Console.WriteLine("ArcRecipeToolDomainTests passed");
            GearAnalysisDomainTests.Run();
            Console.WriteLine("GearAnalysisDomainTests passed");
            GearRecipeToolDomainTests.Run();
            Console.WriteLine("GearRecipeToolDomainTests passed");
            Console.WriteLine("EdgeDetectionDomainTests passed");
            return 0;
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!object.Equals(expected, actual))
            {
                throw new InvalidOperationException(string.Format(
                    "{0}: expected {1}, actual {2}", message, expected, actual));
            }
        }

        private sealed class FakeEdgeDetector : IEdgeDetector<object>
        {
            public EdgeResult DetectEdges(object image, EdgeDetectionRoi roi, EdgeDetectionParameters parameters)
            {
                return new EdgeResult { Success = true };
            }

            public EdgeResult DetectEdgesSubPix(object image, EdgeDetectionRoi roi, EdgeDetectionParameters parameters)
            {
                return new EdgeResult { Success = true };
            }

            public EdgeResult DetectEdgesOnArc(object image, ArcMeasureRoi arcRoi, EdgeDetectionParameters parameters)
            {
                var r = new EdgeResult();
                r.EdgePoints.Add(new EdgePoint { Row = 1.0, Column = 2.0, Amplitude = 30.0, Distance = 0.0 });
                return r;
            }
        }
    }
}
