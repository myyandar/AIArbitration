namespace AIArbitration.Core.Models
{
    /// <summary>
    /// Circuit breaker open exception
    /// </summary>
    public class CircuitBreakerOpenException : ProviderException
    {
        public TimeSpan TimeUntilReset { get; }

        public CircuitBreakerOpenException(string message, string providerId, TimeSpan timeUntilReset)
            : base(message, providerId)
        {
            TimeUntilReset = timeUntilReset;
        }
    }
}
