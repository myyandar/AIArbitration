using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities.Enums
{
    public enum AuthenticationType
    {
        None = 0,

        // API Key Methods
        ApiKey,                     // x-api-key header
        BearerToken,                // Bearer token
        BasicAuth,                  // Basic authentication

        // OAuth Methods
        OAuth2,                     // OAuth 2.0
        OAuth2ClientCredentials,    // Client credentials flow
        OAuth2AuthorizationCode,    // Authorization code flow

        // Cloud Provider Methods
        AWSSignatureV4,             // AWS Signature Version 4
        AzureAD,                    // Azure Active Directory
        GoogleCloud,                // Google Cloud authentication

        // Custom Methods
        CustomHeader,               // Custom header authentication
        QueryParameter,             // API key in query parameter
        HMAC,                      // HMAC signature
        JWT,                       // JSON Web Token
        ApiKeyAndSecret,           // API Key + Secret combination

        // Enterprise Methods
        LDAP,                      // LDAP authentication
        SAML,                      // SAML authentication
        Kerberos,                  // Kerberos authentication

        // Multi-factor
        ApiKeyWithIPWhitelist,     // API key + IP whitelist
        CertificateBased,          // SSL/TLS client certificate
        MutualTLS                  // Mutual TLS authentication
    }
}
