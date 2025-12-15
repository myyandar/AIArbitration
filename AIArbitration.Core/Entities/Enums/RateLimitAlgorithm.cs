namespace AIArbitration.Core.Entities.Enums
{
    public enum RateLimitAlgorithm
    {
        FixedWindow,
        SlidingWindow,
        TokenBucket,
        LeakyBucket,
        Adaptive
    }
}
