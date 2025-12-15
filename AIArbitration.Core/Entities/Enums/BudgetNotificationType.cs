using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities.Enums
{
    /// <summary>
    /// Budget notification types
    /// </summary>
    public enum BudgetNotificationType
    {
        Warning = 0,
        Critical,
        OverBudget,
        Reset,
        Custom
    }
}
