using System;

namespace FlashMeasurementSystem.Domain.Workflow
{
    /// <summary>
    /// Overall result of a one-click measurement run (software-only, no hardware stages).
    /// </summary>
    public class WorkflowResult
    {
        public bool Success { get; set; }
        public bool AllOk { get; set; }
        public int OkCount { get; set; }
        public int NgCount { get; set; }
        public string RecipeName { get; set; } = "";
        public string ReportPath { get; set; } = "";
        public MeasurementState FinalState { get; set; } = MeasurementState.Idle;
        public string Message { get; set; } = "";
        public DateTime Timestamp { get; set; }

        // Template match pose for this run (for UI overlay sync). HasMatch=false when the
        // recipe has no reference pose (no matching performed).
        public bool HasMatch { get; set; }
        public double MatchRow { get; set; }
        public double MatchCol { get; set; }
        public double MatchAngleDeg { get; set; }

        public static WorkflowResult Default()
        {
            return new WorkflowResult();
        }
    }
}
