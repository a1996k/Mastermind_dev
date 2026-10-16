using System;
using System.Security.Cryptography;
using System.Text;

namespace FifthPress.Crypt
{
    /// <summary>
    /// Helper class for generating HMAC signatures for API requests.
    /// Use this in testing or provide equivalent code to external server.
    /// </summary>
    public static class HmacSignatureHelper
    {
        /// <summary>
        /// Generate HMAC-SHA256 signature for an API request
        /// </summary>
        /// <param name="secretKey">Shared secret key</param>
        /// <param name="httpMethod">HTTP method (GET, POST, etc.)</param>
        /// <param name="rawUrl">Raw URL including query string (e.g., /ClientPortal/GetEvents?clientId=123)</param>
        /// <param name="timestamp">Unix timestamp in seconds</param>
        /// <param name="nonce">Unique nonce/request ID (optional but recommended)</param>
        /// <param name="requestBody">Request body content (empty string for GET requests)</param>
        /// <returns>Base64-encoded HMAC signature</returns>
        public static string GenerateSignature(
            string secretKey,
            string httpMethod,
            string rawUrl,
            long timestamp,
            string nonce,
            string requestBody)
        {
            // Create the message to sign: METHOD + URL + TIMESTAMP + NONCE + BODY
            var message = $"{httpMethod}\n{rawUrl}\n{timestamp}\n{nonce}\n{requestBody}";

            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey)))
            {
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
                return Convert.ToBase64String(hash);
            }
        }

        /// <summary>
        /// Generate current Unix timestamp in seconds
        /// </summary>
        public static long GetCurrentTimestamp()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        /// <summary>
        /// Generate a unique nonce
        /// </summary>
        public static string GenerateNonce()
        {
            return Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// Complete example of creating headers for a signed request
        /// </summary>
        public static (string signature, long timestamp, string nonce) CreateSignedRequest(
            string secretKey,
            string httpMethod,
            string rawUrl,
            string requestBody = "")
        {
            var timestamp = GetCurrentTimestamp();
            var nonce = GenerateNonce();
            var signature = GenerateSignature(secretKey, httpMethod, rawUrl, timestamp, nonce, requestBody);

            return (signature, timestamp, nonce);
        }
    }
}
