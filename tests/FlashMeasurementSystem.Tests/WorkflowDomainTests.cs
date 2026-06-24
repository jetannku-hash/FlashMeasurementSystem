using System;
using FlashMeasurementSystem.Domain.Workflow;

namespace FlashMeasurementSystem.Tests
{
    public static class WorkflowDomainTests
    {
        public static void Run()
        {
            WorkflowResult defaults = WorkflowResult.Default();
            AssertEqual(false, defaults.Success, "Default Success");
            AssertEqual(false, defaults.AllOk, "Default AllOk");
            AssertEqual(0, defaults.OkCount, "Default OkCount");
            AssertEqual(0, defaults.NgCount, "Default NgCount");
            AssertEqual("", defaults.RecipeName, "Default RecipeName");
            AssertEqual("", defaults.ReportPath, "Default ReportPath");
            AssertEqual(MeasurementState.Idle, defaults.FinalState, "Default FinalState");
            AssertEqual("", defaults.Message, "Default Message");
            AssertEqual(default(DateTime), defaults.Timestamp, "Default Timestamp");

            // Enum values existence check
            var states = new[]
            {
                MeasurementState.Idle,
                MeasurementState.CheckingImage,
                MeasurementState.MatchingTemplate,
                MeasurementState.TransformingRois,
                MeasurementState.Measuring,
                MeasurementState.Evaluating,
                MeasurementState.Reporting,
                MeasurementState.Completed,
                MeasurementState.Failed
            };
            AssertEqual(9, states.Length, "Enum member count");

            Console.WriteLine("WorkflowDomainTests passed");
        }

        private static void AssertEqual<T>(T expected, T actual, string name)
        {
            if (!object.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    string.Format("{0}: expected {1}, actual {2}", name, expected, actual));
            }
        }
    }
}
