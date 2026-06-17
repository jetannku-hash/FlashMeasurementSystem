using System.Collections.Generic;
using FlashMeasurementSystem.Domain.Tolerance;

namespace FlashMeasurementSystem.Application.Tolerance
{
    public interface IToleranceJudger
    {
        OverallJudgment Judge(IList<ToleranceItemInput> items);
    }
}
