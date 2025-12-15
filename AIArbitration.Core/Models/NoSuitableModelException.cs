namespace AIArbitration.Core.Models
{
    // Custom Exceptions
    public class NoSuitableModelException : Exception
    {
        public NoSuitableModelException() { }
        public NoSuitableModelException(string message) : base(message) { }
        public NoSuitableModelException(string message, Exception inner) : base(message, inner) { }
    }
}
