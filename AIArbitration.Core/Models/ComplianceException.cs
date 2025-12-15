namespace AIArbitration.Core.Models
{
    public class ComplianceException : Exception
    {
        public ComplianceException() { }
        public ComplianceException(string message) : base(message) { }
        public ComplianceException(string message, Exception inner) : base(message, inner) { }
    }
}
