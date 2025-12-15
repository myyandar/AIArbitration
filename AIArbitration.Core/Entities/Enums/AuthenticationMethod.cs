using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities.Enums
{
    public enum AuthenticationMethod
    {
        Password = 1,
        ApiKey = 2,
        OAuth = 3,
        SAML = 4,
        LDAP = 5,
        Certificate = 6,
        Biometric = 7,
        WebAuthn = 8,
        MagicLink = 9,
        SSO = 10
    }
}
