using System.Collections.Generic;
using FlashMeasurementSystem.Domain.Tolerance;
using FlashMeasurementSystem.Domain.Workflow;

namespace FlashMeasurementSystem.Application.Reporting
{
    /// <summary>
    /// Appends one measurement run as rows to a CSV report file.
    /// </summary>
    public interface IMeasurementReportWriter
    {
        void Append(WorkflowResult overall, IList<ItemJudgment> items, string filePath);
    }
}
