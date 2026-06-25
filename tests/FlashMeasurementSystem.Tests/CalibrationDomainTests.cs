using System;
using System.IO;
using FlashMeasurementSystem.Application.Calibration;
using FlashMeasurementSystem.Domain.Calibration;
using FlashMeasurementSystem.Infrastructure.Calibration;

namespace FlashMeasurementSystem.Tests
{
    public static class CalibrationDomainTests
    {
        public static void Run()
        {
            // ─── DTO 預設值 ──────────────────────────────────────────
            CalibrationProfile def = CalibrationProfile.Default();
            AssertEqual(1, def.SchemaVersion, "Default SchemaVersion");
            AssertEqual(10.0, def.PixelSizeUmX, "Default PixelSizeUmX");
            AssertEqual(10.0, def.PixelSizeUmY, "Default PixelSizeUmY");
            AssertEqual(false, def.DistortionCorrected, "Default DistortionCorrected");

            // ─── 真實校正數學 ───────────────────────────────────────
            ICalibrator calibrator = new PixelSizeCalibrator();

            // 水平 1000px 對應 10.000mm → 10000µm / 1000px = 10 µm/px
            CalibrationProfile p = calibrator.CalibrateTwoPoint(
                "CAL-1", 10.0, 500, 1000, 500, 2000, 4096, 3000);
            AssertClose(10.0, p.PixelSizeUmX, 1e-9, "10mm/1000px → 10 µm/px (X)");
            AssertClose(10.0, p.PixelSizeUmY, 1e-9, "等向 Y == X");
            AssertClose(1000.0, p.MeasuredPixels, 1e-9, "Measured pixels");
            AssertEqual("CAL-1", p.ProfileId, "ProfileId preserved");
            // FOV：4096px * 10µm /1000 = 40.96mm
            AssertClose(40.96, p.FieldOfViewMmX, 1e-6, "FOV X");
            AssertClose(30.00, p.FieldOfViewMmY, 1e-6, "FOV Y");

            // 對角線 3-4-5：距離 (300,400) → 500px，5.000mm → 10 µm/px
            CalibrationProfile p2 = calibrator.CalibrateTwoPoint(
                "CAL-2", 5.0, 0, 0, 300, 400, 1000, 1000);
            AssertClose(500.0, p2.MeasuredPixels, 1e-9, "3-4-5 diagonal 500px");
            AssertClose(10.0, p2.PixelSizeUmX, 1e-9, "5mm/500px → 10 µm/px");

            // 兩點重合 → 例外
            bool threwCoincident = false;
            try { calibrator.CalibrateTwoPoint("X", 10.0, 100, 100, 100, 100, 100, 100); }
            catch (ArgumentException) { threwCoincident = true; }
            AssertEqual(true, threwCoincident, "Coincident points throw");

            // 已知距離 <= 0 → 例外
            bool threwBadDist = false;
            try { calibrator.CalibrateTwoPoint("X", 0.0, 0, 0, 0, 100, 100, 100); }
            catch (ArgumentException) { threwBadDist = true; }
            AssertEqual(true, threwBadDist, "Non-positive known distance throws");

            // ─── 介面契約（Fake）────────────────────────────────────
            ICalibrationStore fake = new FakeCalibrationStore();
            CalibrationProfile loaded = fake.Load("dummy");
            AssertEqual("FAKE", loaded.ProfileId, "Fake store satisfies interface contract");

            // ─── 真實 Store round-trip（JSON 存→載→比對）─────────────
            ICalibrationStore store = new CalibrationStore();
            string path = Path.Combine(Path.GetTempPath(),
                "fms_cal_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".json");
            try
            {
                store.Save(p, path);
                if (!File.Exists(path))
                    throw new InvalidOperationException("Calibration file not written");
                CalibrationProfile rt = store.Load(path);
                AssertEqual(p.SchemaVersion, rt.SchemaVersion, "Round-trip SchemaVersion");
                AssertEqual("CAL-1", rt.ProfileId, "Round-trip ProfileId");
                AssertClose(p.PixelSizeUmX, rt.PixelSizeUmX, 1e-9, "Round-trip PixelSizeUmX");
                AssertClose(p.FieldOfViewMmX, rt.FieldOfViewMmX, 1e-9, "Round-trip FOV X");
                AssertEqual(p.CreatedAt, rt.CreatedAt, "Round-trip CreatedAt (DateTime)");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }

            // ─── Load 錯誤處理：缺檔擲明確例外（非 raw、非 null）──
            string missing = Path.Combine(Path.GetTempPath(),
                "fms_cal_missing_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".json");
            AssertThrows(() => new CalibrationStore().Load(missing), "Load missing calibration throws");

            // ─── Load 錯誤處理：損毀 JSON 擲明確例外 ──
            string corrupt = Path.Combine(Path.GetTempPath(),
                "fms_cal_corrupt_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".json");
            File.WriteAllText(corrupt, "{ not valid calibration json ");
            try { AssertThrows(() => new CalibrationStore().Load(corrupt), "Load corrupt calibration JSON throws"); }
            finally { if (File.Exists(corrupt)) File.Delete(corrupt); }

            // ─── 原子覆寫：覆寫既有檔後仍正確載回新內容，且不殘留 .tmp ──
            string ovr = Path.Combine(Path.GetTempPath(),
                "fms_cal_ovr_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".json");
            try
            {
                ICalibrationStore s = new CalibrationStore();
                s.Save(new CalibrationProfile { ProfileId = "C-V1", PixelSizeUmX = 11.0 }, ovr);
                s.Save(new CalibrationProfile { ProfileId = "C-V2", PixelSizeUmX = 22.0 }, ovr); // 覆寫既有檔
                CalibrationProfile reloaded = s.Load(ovr);
                AssertEqual("C-V2", reloaded.ProfileId, "Atomic overwrite keeps latest ProfileId");
                AssertClose(22.0, reloaded.PixelSizeUmX, 1e-9, "Atomic overwrite keeps latest value");
                if (File.Exists(ovr + ".tmp"))
                    throw new InvalidOperationException("Save should not leave a .tmp file behind");
            }
            finally { if (File.Exists(ovr)) File.Delete(ovr); }
        }

        private static void AssertThrows(Action action, string name)
        {
            try { action(); }
            catch { return; }
            throw new InvalidOperationException(name + " — expected an exception but none was thrown");
        }

        private static void AssertEqual<T>(T expected, T actual, string name)
        {
            if (!object.Equals(expected, actual))
                throw new InvalidOperationException(name + " expected " + expected + " but got " + actual);
        }

        private static void AssertClose(double expected, double actual, double tol, string name)
        {
            if (Math.Abs(expected - actual) > tol)
                throw new InvalidOperationException(name + " expected " + expected + " but got " + actual + " (tol " + tol + ")");
        }

        private sealed class FakeCalibrationStore : ICalibrationStore
        {
            public void Save(CalibrationProfile profile, string filePath) { }
            public CalibrationProfile Load(string filePath)
            {
                return new CalibrationProfile { ProfileId = "FAKE" };
            }
        }
    }
}
