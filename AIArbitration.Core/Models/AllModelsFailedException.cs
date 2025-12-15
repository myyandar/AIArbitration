namespace AIArbitration.Core.Models
{
    public class AllModelsFailedException : Exception
    {
        public AllModelsFailedException() { }
        public AllModelsFailedException(string message) : base(message) { }
        public AllModelsFailedException(string message, Exception inner) : base(message, inner) { }
    }
}
