using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Models
{
    /// <summary>
    /// Provider exception
    /// </summary>
    public class ProviderException : Exception
    {
        public string ProviderId { get; }
        public DateTime Timestamp { get; }
        public string? RequestId { get; set; }
        public string? ErrorCode { get; set; }

        public ProviderException(string message, string providerId, Exception innerException = null)
            : base(message, innerException)
        {
            ProviderId = providerId;
            Timestamp = DateTime.UtcNow;
        }

        public ProviderException(string message, string providerId, string errorCode, Exception innerException = null)
            : this(message, providerId, innerException)
        {
            ErrorCode = errorCode;
        }

        public ProviderException(string message, string providerId, string requestId, string errorCode, Exception innerException = null)
            : this(message, providerId, innerException)
        {
            RequestId = requestId;
            ErrorCode = errorCode;
        }
    }
}
