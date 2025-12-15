namespace AIArbitration.Core.Models
{
    public class RuleCondition
    {
        public string Field { get; set; } = string.Empty;
        public string Operator { get; set; } = string.Empty; // "==", ">", "<", "contains", etc.
        public string Value { get; set; } = string.Empty;
    }
}
