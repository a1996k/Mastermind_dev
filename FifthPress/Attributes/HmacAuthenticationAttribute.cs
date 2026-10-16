using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Mvc;

namespace FifthPress.Attributes
{
    /// <summary>
    /// HMAC-based authentication for server-to-server API calls.
    /// Validates that requests are signed with a shared secret key.
    /// </summary>
    public class HmacAuthenticationAttribute : AuthorizeAttribute
    {
        private const string SignatureHeaderName = "X-Signature";
        private const string TimestampHeaderName = "X-Timestamp";
        private const string NonceHeaderName = "X-Nonce";
        private const int MaxRequestAgeInSeconds = 300; // 5 minutes

        /// <summary>
        /// Override authorization check to validate HMAC signature
        /// </summary>
        protected override bool AuthorizeCore(System.Web.HttpContextBase httpContext)
        {
            try
            {
                var request = httpContext.Request;

                // Get signature from header
                var providedSignature = request.Headers[SignatureHeaderName];
                if (string.IsNullOrEmpty(providedSignature))
                {
                    LogAuthFailure("Missing signature header");
                    return false;
                }

                // Get timestamp from header
                var timestampString = request.Headers[TimestampHeaderName];
                if (string.IsNullOrEmpty(timestampString))
                {
                    LogAuthFailure("Missing timestamp header");
                    return false;
                }

                // Parse and validate timestamp (prevent replay attacks)
                if (!long.TryParse(timestampString, out long timestamp))
                {
                    LogAuthFailure("Invalid timestamp format");
                    return false;
                }

                var requestTime = DateTimeOffset.FromUnixTimeSeconds(timestamp);
                var now = DateTimeOffset.UtcNow;
                var age = Math.Abs((now - requestTime).TotalSeconds);

                if (age > MaxRequestAgeInSeconds)
                {
                    LogAuthFailure($"Request too old: {age} seconds");
                    return false;
                }

                // Get nonce (optional but recommended)
                var nonce = request.Headers[NonceHeaderName] ?? string.Empty;

                // Read request body
                string requestBody = ReadRequestBody(request);

                // Calculate expected signature
                var secretKey = GetSecretKey();
                var expectedSignature = CalculateHmacSignature(secretKey, requestBody, timestampString, nonce, request.HttpMethod, request.RawUrl);

                // Compare signatures (constant-time comparison to prevent timing attacks)
                bool isValid = SecureCompare(providedSignature, expectedSignature);

                if (!isValid)
                {
                    LogAuthFailure("Signature mismatch");
                }

                return isValid;
            }
            catch (Exception ex)
            {
                LogAuthFailure($"Exception during HMAC validation: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Handle unauthorized access
        /// </summary>
        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            filterContext.Result = new HttpUnauthorizedResult("HMAC authentication failed");
        }

        /// <summary>
        /// Calculate HMAC-SHA256 signature
        /// </summary>
        private string CalculateHmacSignature(string secretKey, string requestBody, string timestamp, string nonce, string httpMethod, string rawUrl)
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
        /// Read request body (supports multiple reads)
        /// </summary>
        private string ReadRequestBody(System.Web.HttpRequestBase request)
        {
            // Enable multiple reads of the input stream
            request.InputStream.Position = 0;

            using (var reader = new StreamReader(request.InputStream, Encoding.UTF8, true, 1024, true))
            {
                var body = reader.ReadToEnd();
                request.InputStream.Position = 0; // Reset for controller to read again
                return body;
            }
        }

        /// <summary>
        /// Get secret key from configuration
        /// </summary>
        private string GetSecretKey()
        {
            var key = ConfigurationManager.AppSettings["HmacSecretKey"];

            if (string.IsNullOrEmpty(key))
            {
                throw new ConfigurationErrorsException("HmacSecretKey not configured in Web.config");
            }

            return key;
        }

        /// <summary>
        /// Constant-time string comparison to prevent timing attacks
        /// </summary>
        private bool SecureCompare(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;

            int result = 0;
            for (int i = 0; i < a.Length; i++)
            {
                result |= a[i] ^ b[i];
            }

            return result == 0;
        }

        /// <summary>
        /// Log authentication failures (customize as needed)
        /// </summary>
        private void LogAuthFailure(string message)
        {
            // TODO: Replace with your logging framework (e.g., log4net, NLog, etc.)
            System.Diagnostics.Debug.WriteLine($"[HMAC Auth Failed] {DateTime.UtcNow:O} - {message}");
        }
    }
}
