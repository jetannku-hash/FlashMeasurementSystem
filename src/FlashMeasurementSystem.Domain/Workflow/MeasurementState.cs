namespace FlashMeasurementSystem.Domain.Workflow
{
    /// <summary>
    /// Software-only measurement workflow states (hardware stages omitted — see manual §4.14).
    /// </summary>
    public enum MeasurementState
    {
        Idle,
        CheckingImage,
        MatchingTemplate,
        TransformingRois,
        Measuring,
        Evaluating,
        Reporting,
        Completed,
        Failed
    }
}
