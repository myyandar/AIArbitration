namespace AIArbitration.Core.Entities.Enums
{
    public enum CircuitEventType
    {
        Closed,
        Opened,
        Reset,
        Isolated,
        ForcedOpen,
        ManualOverride,
        Error,
        Timeout,
        ConfigUpdated,
        Failure,
        HalfOpen
    }
}
