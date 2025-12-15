using AIArbitration.Core.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Models
{
    public class BudgetCheckResult
    {
        public bool IsAllowed { get; set; }
        public BudgetStatus? BudgetStatus { get; set; }
        public decimal EstimatedCost { get; set; }
        public decimal AvailableBalance { get; set; }
        public bool WillExceedWarning { get; set; }
        public bool WillExceedCritical { get; set; }
        public bool WillExceedBudget { get; set; }
        public string? Message { get; set; }
        public Dictionary<string, object> Details { get; set; } = new();
        public bool HasSufficientBudget { get; set; }
    }
}
