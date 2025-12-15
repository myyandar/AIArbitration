namespace AIArbitration.Core.Models
{
    public class InsufficientBudgetException : Exception
    {
        public InsufficientBudgetException() { }
        public InsufficientBudgetException(string message) : base(message) { }
        public InsufficientBudgetException(string message, Exception inner) : base(message, inner) { }
    }
}
