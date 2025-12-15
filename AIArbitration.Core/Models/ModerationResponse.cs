using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Models
{
    public class ModerationResponse
    {
        public string Id { get; set; }
        public string ModelUsed { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public bool IsFlagged { get; set; }
        public Dictionary<string, decimal> CategoryScores { get; set; } = new();

        /// <summary>
        /// Time taken to process the request
        /// </summary>
        public TimeSpan ProcessingTime { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Individual category scores and flags
        /// </summary>
        public Dictionary<string, ModerationCategory> Categories { get; set; } = new ();

        /// <summary>
        /// Overall moderation score
        /// </summary>
        public double Score { get; set; }

        /// <summary>
        /// Provider's original response for debugging
        /// </summary>
        public string ProviderRawResponse { get; set; }

        /// <summary>
        /// Provider-specific metadata
        /// </summary>
        public Dictionary<string, object> ProviderMetadata { get; set; } = new Dictionary<string, object>();


        /// <summary>
        /// Request identifier (echoed from request)
        /// </summary>
        public string RequestId { get; set; }

        /// <summary>
        /// Provider identifier
        /// </summary>
        public string ProviderId { get; set; }
    }

    public class ModerationCategory
    {
        public bool Flagged { get; set; }
        public double Score { get; set; }
        public string Description { get; set; }
    }
}
