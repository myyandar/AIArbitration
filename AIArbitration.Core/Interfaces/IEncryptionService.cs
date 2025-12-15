using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AIArbitration.Core.Interfaces
{
    public interface IEncryptionService
    {
        string? Encrypt(string? plainText);
        string? Decrypt(string? cipherText);
    }

    public class EncryptionService : IEncryptionService
    {
        private readonly IDataProtector _protector;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EncryptionService> _logger;

        public EncryptionService(
            IDataProtectionProvider dataProtectionProvider,
            IConfiguration configuration,
            ILogger<EncryptionService> logger)
        {
            // Create a protector with a specific purpose string
            _protector = dataProtectionProvider.CreateProtector("Arbitration.API.Secrets");
            _configuration = configuration;
            _logger = logger;
        }

        public string? Encrypt(string? plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return plainText;

            try
            {
                // For development/testing, you might want to use a simple base64 encoding
                if (_configuration["Encryption:Mode"] == "Development")
                {
                    return $"encrypted_dev:{Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText))}";
                }

                // Production: Use proper encryption
                return _protector.Protect(plainText);
            }
            catch (CryptographicException ex)
            {
                _logger.LogError(ex, "Failed to encrypt value");
                throw;
            }
        }

        public string? Decrypt(string? cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return cipherText;

            // Check if it's development-encrypted
            if (cipherText.StartsWith("encrypted_dev:"))
            {
                var base64Data = cipherText.Substring("encrypted_dev:".Length);
                return Encoding.UTF8.GetString(Convert.FromBase64String(base64Data));
            }

            // Check if it's production-encrypted
            if (cipherText.StartsWith("encrypted:"))
            {
                var encryptedData = cipherText.Substring("encrypted:".Length);
                try
                {
                    return _protector.Unprotect(encryptedData);
                }
                catch (CryptographicException)
                {
                    _logger.LogWarning("Failed to decrypt using protector, trying direct");
                    return _protector.Unprotect(cipherText);
                }
            }

            // Try direct decryption (for backward compatibility)
            try
            {
                return _protector.Unprotect(cipherText);
            }
            catch (CryptographicException)
            {
                // If it's not encrypted, return as-is
                return cipherText;
            }
        }
    }

    // Alternative: Simple AES encryption (not recommended for production without proper key management)
    public class AesEncryptionService : IEncryptionService
    {
        private readonly byte[] _key;
        private readonly byte[] _iv;

        public AesEncryptionService(IConfiguration configuration)
        {
            // WARNING: This is simplified. In production, use proper key management!
            var encryptionKey = configuration["Encryption:Key"];
            var encryptionIV = configuration["Encryption:IV"];

            _key = Convert.FromBase64String(encryptionKey ??
                throw new InvalidOperationException("Encryption key not configured"));
            _iv = Convert.FromBase64String(encryptionIV ??
                throw new InvalidOperationException("Encryption IV not configured"));
        }

        public string? Encrypt(string? plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return plainText;

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs))
            {
                sw.Write(plainText);
            }

            return Convert.ToBase64String(ms.ToArray());
        }

        public string? Decrypt(string? cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return cipherText;

            try
            {
                var buffer = Convert.FromBase64String(cipherText);

                using var aes = Aes.Create();
                aes.Key = _key;
                aes.IV = _iv;

                using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                using var ms = new MemoryStream(buffer);
                using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                using var sr = new StreamReader(cs);

                return sr.ReadToEnd();
            }
            catch (FormatException)
            {
                // Not base64, return as-is
                return cipherText;
            }
        }
    }
}
