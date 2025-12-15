namespace AIArbitration.Core.Models
{
    /// <summary>
    /// Rate limit exceeded exception
    /// </summary>
    public class RateLimitExceededException : ProviderException
    {
        // public RateLimitExceededException() { }
        // public RateLimitExceededException(string message) : base(message, , , ,) { }
        // Exception inner = null;
        // string providerId = null;
        // public RateLimitExceededException(string message, Exception inner) : base(message, providerId, inner) { }
        public DateTime? RetryAfter { get; }
        public int? RemainingRequests { get; }
        public int? Limit { get; }
        public TimeSpan? ResetAfter { get; }

        public RateLimitExceededException(string message, string providerId, DateTime? retryAfter = null,
            int? remainingRequests = null, int? limit = null, TimeSpan? resetAfter = null)
            : base(message, providerId)
        {
            RetryAfter = retryAfter;
            RemainingRequests = remainingRequests;
            Limit = limit;
            ResetAfter = resetAfter;
        }
    }
}
