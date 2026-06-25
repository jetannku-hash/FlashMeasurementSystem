using System;

namespace FlashMeasurementSystem.Tests.Halcon
{
    public static class Program
    {
        public static int Main()
        {
            var suites = new Action[]
            {
                CoordinateMapperTests.Run,
                DistanceMeasurerTests.Run,
                EdgeDetectorTests.Run,
                LineFitterTests.Run,
                CircleFitterTests.Run,
                EllipseFitterTests.Run,
                AngleMeasurerTests.Run,
                ImageQualityCheckerTests.Run,
                TemplateMatcherTests.Run,
            };

            int passed = 0, failed = 0;
            foreach (var suite in suites)
            {
                try
                {
                    suite();
                    passed++;
                }
                catch (Exception ex)
                {
                    failed++;
                    Console.Error.WriteLine("FAIL: " + suite.Method.Name + " — " + ex.Message);
                }
            }
            Console.WriteLine();
            Console.WriteLine("HALCON adapter tests: " + passed + "/" + (passed + failed) + " suites passed");
            return failed > 0 ? 1 : 0;
        }
    }
}
