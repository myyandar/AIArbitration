namespace AIArbitration.Core.Entities.Enums
{
    public enum ViolationType
    {
        RateLimitExceeded,
        BudgetExceeded,
        SuspiciousActivity,
        DataExfiltrationAttempt,
        ModelAbuse,
        ComplianceViolation,
        UnauthorizedAccess,
        TokenTheftAttempt
    }
}