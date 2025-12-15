using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities.Enums
{
    public enum SessionStatus
    {
        Active = 1,
        Idle = 2,
        Expired = 3,
        Revoked = 4,
        Terminated = 5,
        Suspended = 6
    }
}
