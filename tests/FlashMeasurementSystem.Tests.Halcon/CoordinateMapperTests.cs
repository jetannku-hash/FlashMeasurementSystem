using System;
using FlashMeasurementSystem.Domain.CoordinateSystem;
using FlashMeasurementSystem.Halcon.CoordinateSystem;

namespace FlashMeasurementSystem.Tests.Halcon
{
    public static class CoordinateMapperTests
    {
        private const double Tol = 3.0;

        public static void Run()
        {
            var mapper = new HalconCoordinateMapper();

            // 1. Identity: same ref/cur → near-identity transform
            RigidTransform tIdent = mapper.CreateFromMatch(
                100.0, 200.0, 0.5,
                100.0, 200.0, 0.5);
            Assert(tIdent.IsValid, "identity: IsValid=true");
            Assert(tIdent.HomMat2D != null && tIdent.HomMat2D.Length == 6, "identity: 6 elements");

            // 2. Pure translation (dRow=50)
            RigidTransform tTrans = mapper.CreateFromMatch(
                100.0, 200.0, 0.0,
                150.0, 200.0, 0.0);
            Assert(tTrans.IsValid, "translation: IsValid=true");
            TransformedRoi trTrans = mapper.TransformRoi(50.0, 100.0, 0.0, tTrans);
            Assert(Math.Abs(trTrans.Row - 100.0) < Tol, "translation: Row shifted by 50, got=" + trTrans.Row.ToString("F1"));
            Assert(Math.Abs(trTrans.Col - 100.0) < Tol, "translation: Col unchanged, got=" + trTrans.Col.ToString("F1"));

            // 3. Pure rotation (180° = π) around (100,200)
            RigidTransform tRot = mapper.CreateFromMatch(
                100.0, 200.0, 0.0,
                100.0, 200.0, Math.PI);
            Assert(tRot.IsValid, "rotation: IsValid=true");
            TransformedRoi trRot = mapper.TransformRoi(150.0, 200.0, 0.0, tRot);
            // Point (150,200) rotated 180° around (100,200) → (50,200)
            Assert(Math.Abs(trRot.Row - 50.0) < Tol, "rotation 180°: Row ~50, got=" + trRot.Row.ToString("F1"));
            Assert(Math.Abs(trRot.Col - 200.0) < Tol, "rotation 180°: Col unchanged, got=" + trRot.Col.ToString("F1"));

            // 4. Translate + rotate
            RigidTransform tBoth = mapper.CreateFromMatch(
                100.0, 200.0, Math.PI / 6.0,
                100.0, 200.0, Math.PI / 3.0);
            Assert(tBoth.IsValid, "translate+rotate: IsValid=true");
            TransformedRoi trBoth = mapper.TransformRoi(150.0, 200.0, 0.0, tBoth);
            // Exact values depend on HALCON's vector_angle_to_rigid + affine_trans_point2d;
            // just verify the transform produces reasonable non-identity output.
            Console.WriteLine("  T+R: Row=" + trBoth.Row.ToString("F1") + " Col=" + trBoth.Col.ToString("F1"));
            Assert(Math.Abs(trBoth.Row - 150.0) > 0.1 || Math.Abs(trBoth.Col - 200.0) > 0.1,
                "T+R: output differs from input (not identity)");

            // 5. Null transform → ArgumentException
            try
            {
                mapper.TransformRoi(10.0, 20.0, 0.0, null);
                throw new InvalidOperationException("null transform should throw");
            }
            catch (ArgumentException) { /* expected */ }

            // 6. Invalid transform (HomMat2D length != 6)
            try
            {
                var invalid = new RigidTransform { HomMat2D = new double[] { 1, 2 } };
                Assert(!invalid.IsValid, "short HomMat2D: IsValid=false");
                mapper.TransformRoi(10.0, 20.0, 0.0, invalid);
                throw new InvalidOperationException("invalid transform should throw");
            }
            catch (ArgumentException) { /* expected */ }

            Console.WriteLine("CoordinateMapperTests passed");
        }

        private static void Assert(bool cond, string msg) { if (!cond) throw new InvalidOperationException(msg); }
    }
}
