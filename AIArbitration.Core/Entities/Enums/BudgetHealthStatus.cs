using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities.Enums
{
    /// <summary>
    /// Health status of a budget
    /// </summary>
    public enum BudgetHealthStatus
    {
        Unknown = 0,
        Healthy,        // Below warning threshold
        Warning,        // At or above warning threshold
        Critical,       // At or above critical threshold
        OverBudget      // Over budget
    }
}
