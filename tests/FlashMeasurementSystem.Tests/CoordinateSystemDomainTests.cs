using System;
using FlashMeasurementSystem.Application.CoordinateSystem;
using FlashMeasurementSystem.Domain.CoordinateSystem;

namespace FlashMeasurementSystem.Tests
{
    // 註：HalconCoordinateMapper 用到 HALCON 算子，依專案慣例 HALCON adapter 不做單元測試
    // （於 GUI 手動驗證）。此處只測 Domain DTO 預設值與 Application 介面契約（Fake）。
    public static class CoordinateSystemDomainTests
    {
        public static void Run()
        {
            // ─── RigidTransform 預設值與 IsValid ────────────────────
            RigidTransform empty = new RigidTransform();
            AssertEqual(false, empty.IsValid, "Default RigidTransform invalid (null matrix)");
            AssertEqual(0.0, empty.RotationRad, "Default RotationRad");
            AssertEqual(0.0, empty.TranslationRow, "Default TranslationRow");
            AssertEqual(0.0, empty.TranslationCol, "Default TranslationCol");

            RigidTransform wrongLen = new RigidTransform { HomMat2D = new double[] { 1, 0, 0 } };
            AssertEqual(false, wrongLen.IsValid, "3-element matrix invalid");

            RigidTransform valid = new RigidTransform
            {
                HomMat2D = new double[] { 1, 0, 0, 0, 1, 0 }
            };
            AssertEqual(true, valid.IsValid, "6-element matrix valid");

            // ─── TransformedRoi 預設值 ──────────────────────────────
            TransformedRoi roi = new TransformedRoi();
            AssertEqual(0.0, roi.Row, "Default TransformedRoi Row");
            AssertEqual(0.0, roi.Col, "Default TransformedRoi Col");
            AssertEqual(0.0, roi.AngleRad, "Default TransformedRoi AngleRad");

            // ─── 介面契約（Fake）────────────────────────────────────
            ICoordinateMapper mapper = new FakeCoordinateMapper();
            RigidTransform t = mapper.CreateFromMatch(0, 0, 0, 10, 20, 0.5);
            AssertEqual(true, t.IsValid, "Fake mapper returns valid transform");
            AssertEqual(10.0, t.TranslationRow, "Fake mapper translation row");
            AssertEqual(20.0, t.TranslationCol, "Fake mapper translation col");

            TransformedRoi out1 = mapper.TransformRoi(5, 5, 0.0, t);
            AssertEqual(15.0, out1.Row, "Fake mapper transformed row");
            AssertEqual(25.0, out1.Col, "Fake mapper transformed col");
            AssertEqual(0.5, out1.AngleRad, "Fake mapper transformed angle");
        }

        private static void AssertEqual<T>(T expected, T actual, string name)
        {
            if (!object.Equals(expected, actual))
            {
                throw new InvalidOperationException(name + " expected " + expected + " but got " + actual);
            }
        }

        // 純加法的假實作，僅用於驗證介面契約（不代表真實剛體變換語意）。
        private sealed class FakeCoordinateMapper : ICoordinateMapper
        {
            public RigidTransform CreateFromMatch(
                double refRow, double refCol, double refAngleRad,
                double curRow, double curCol, double curAngleRad)
            {
                return new RigidTransform
                {
                    HomMat2D = new double[] { 1, 0, 0, 0, 1, 0 },
                    RotationRad = curAngleRad - refAngleRad,
                    TranslationRow = curRow - refRow,
                    TranslationCol = curCol - refCol
                };
            }

            public TransformedRoi TransformRoi(
                double refRoiRow, double refRoiCol, double refRoiAngleRad,
                RigidTransform transform)
            {
                return new TransformedRoi
                {
                    Row = refRoiRow + transform.TranslationRow,
                    Col = refRoiCol + transform.TranslationCol,
                    AngleRad = refRoiAngleRad + transform.RotationRad
                };
            }
        }
    }
}
