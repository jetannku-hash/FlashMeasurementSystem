using System;
using FlashMeasurementSystem.Application.CoordinateSystem;
using FlashMeasurementSystem.Domain.CoordinateSystem;
using FlashMeasurementSystem.Domain.EdgeDetection;

namespace FlashMeasurementSystem.Tests
{
    /// <summary>
    /// ArcRoiTransform 姿態變換測試。此類刻意放在 Application（純介面+Domain 型別）就是為了能被
    /// 測試守護，但一直沒有測試——本檔補上。剛體變換下：中心需變換、起始角需旋轉，
    /// 半徑/角度範圍/環寬不變；transform 為 null/無效時回傳原弧的複本（不變換）。
    /// 用假 ICoordinateMapper 注入已知變換，驗證只有 中心+起始角 被套用、其餘不動。
    /// </summary>
    public static class ArcRoiTransformDomainTests
    {
        public static void Run()
        {
            NullArcReturnsNull();
            NullTransformReturnsUnchangedCopy();
            InvalidTransformReturnsCopy();
            ValidTransformAppliesCenterAndStartAngleOnly();

            Console.WriteLine("ArcRoiTransformDomainTests passed");
        }

        private static void NullArcReturnsNull()
        {
            var result = ArcRoiTransform.TransformArc(new OffsetMapper(), null, ValidTransform());
            AssertTrue(result == null, "null arc → null");
        }

        // transform 為 null → 回傳「複本」（不同實例）、欄位不變、且不呼叫 mapper。
        private static void NullTransformReturnsUnchangedCopy()
        {
            ArcMeasureRoi arc = SampleArc();
            var result = ArcRoiTransform.TransformArc(new OffsetMapper(), arc, null);

            AssertTrue(result != null, "null transform → not null");
            AssertTrue(!ReferenceEquals(result, arc), "null transform → distinct copy instance");
            AssertClose(arc.CenterRow, result.CenterRow, 1e-9, "null transform: CenterRow unchanged");
            AssertClose(arc.CenterCol, result.CenterCol, 1e-9, "null transform: CenterCol unchanged");
            AssertClose(arc.AngleStart, result.AngleStart, 1e-9, "null transform: AngleStart unchanged");
            AssertClose(arc.Radius, result.Radius, 1e-9, "null transform: Radius unchanged");
            AssertClose(arc.AngleExtent, result.AngleExtent, 1e-9, "null transform: AngleExtent unchanged");
            AssertClose(arc.AnnulusRadius, result.AnnulusRadius, 1e-9, "null transform: AnnulusRadius unchanged");
        }

        // 無效 transform（HomMat2D 非 6 元素）→ 複本、不變、不呼叫 mapper（OffsetMapper 會改值，
        // 若值沒變即證明未呼叫）。
        private static void InvalidTransformReturnsCopy()
        {
            ArcMeasureRoi arc = SampleArc();
            var invalid = new RigidTransform { HomMat2D = null };   // IsValid=false
            var result = ArcRoiTransform.TransformArc(new OffsetMapper(), arc, invalid);

            AssertClose(arc.CenterRow, result.CenterRow, 1e-9, "invalid transform: CenterRow unchanged (mapper not called)");
            AssertClose(arc.AngleStart, result.AngleStart, 1e-9, "invalid transform: AngleStart unchanged");
        }

        // 有效 transform → 中心 + 起始角取自 mapper.TransformRoi；半徑/角度範圍/環寬不變。
        private static void ValidTransformAppliesCenterAndStartAngleOnly()
        {
            ArcMeasureRoi arc = SampleArc();   // Center(300,400) Start=0.2 Radius=50 Extent=1.0 Annulus=10
            var result = ArcRoiTransform.TransformArc(new OffsetMapper(), arc, ValidTransform());

            // OffsetMapper：Row+100, Col+200, Angle+0.5
            AssertClose(400.0, result.CenterRow, 1e-9, "valid transform: CenterRow = 300+100");
            AssertClose(600.0, result.CenterCol, 1e-9, "valid transform: CenterCol = 400+200");
            AssertClose(0.7, result.AngleStart, 1e-9, "valid transform: AngleStart = 0.2+0.5");
            // 剛體變換不縮放：以下三者必須原封不動
            AssertClose(50.0, result.Radius, 1e-9, "valid transform: Radius unchanged");
            AssertClose(1.0, result.AngleExtent, 1e-9, "valid transform: AngleExtent unchanged");
            AssertClose(10.0, result.AnnulusRadius, 1e-9, "valid transform: AnnulusRadius unchanged");
        }

        private static ArcMeasureRoi SampleArc()
        {
            return new ArcMeasureRoi
            {
                CenterRow = 300, CenterCol = 400, Radius = 50,
                AngleStart = 0.2, AngleExtent = 1.0, AnnulusRadius = 10
            };
        }

        private static RigidTransform ValidTransform()
        {
            // IsValid 需要 HomMat2D 長度 6；內容不影響 ArcRoiTransform（它只透過 mapper.TransformRoi）。
            return new RigidTransform { HomMat2D = new double[6] };
        }

        // 假 mapper：TransformRoi 對 中心+角度 各加固定偏移，讓「有無被套用」可觀察。
        private sealed class OffsetMapper : ICoordinateMapper
        {
            public RigidTransform CreateFromMatch(double rr, double rc, double ra, double cr, double cc, double ca)
                => throw new InvalidOperationException("CreateFromMatch not used by ArcRoiTransform");

            public TransformedRoi TransformRoi(double refRow, double refCol, double refAngleRad, RigidTransform transform)
                => new TransformedRoi { Row = refRow + 100, Col = refCol + 200, AngleRad = refAngleRad + 0.5 };
        }

        private static void AssertTrue(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("FAIL " + name);
        }

        private static void AssertClose(double expected, double actual, double tol, string name)
        {
            if (Math.Abs(expected - actual) > tol)
                throw new InvalidOperationException(
                    "FAIL " + name + " | expected=" + expected + " actual=" + actual);
        }
    }
}
