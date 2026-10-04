namespace FSOC.AI
{
    public enum AIStatus
    {
        Unknown,
        ModelUnavailable,
        ModelLoaded,
        InferenceAvailable,
        InferenceActive,
        ClassicalFallbackActive,
        Error
    }
}
