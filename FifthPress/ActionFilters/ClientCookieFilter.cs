using System;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.ActionFilters
{
    /// <summary>
    /// Action filter to validate client portal session cookies on each request.
    /// Mirrors CookieFilter.cs pattern but simplified for client authentication.
    /// Uses cookie-only sliding window (4 hours = 240 minutes) with no database writes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class ClientCookieFilter : ActionFilterAttribute
    {
        private const string COOKIE_NAME = "ClientActiveUser";
        private const int TIMEOUT_MINUTES = 240;

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            // 1. Retrieve ClientActiveUser cookie
            var clientCookie = filterContext.HttpContext.Request.Cookies[COOKIE_NAME];

            // 2. If cookie is missing, return 401 (session expired or never logged in)
            if (clientCookie == null || string.IsNullOrEmpty(clientCookie.Value))
            {
                filterContext.Result = new HttpStatusCodeResult(HttpStatusCode.Unauthorized, "Session expired");
                return;
            }

            // 3. Parse clientLoginId from cookie value
            if (!int.TryParse(clientCookie.Value, out int clientLoginId))
            {
                filterContext.Result = new HttpStatusCodeResult(HttpStatusCode.Unauthorized, "Invalid session");
                return;
            }

            // 4. Validate clientLoginId in database (READ ONLY - no writes)
            using (var _db = new FifthPressEntities())
            {
                var clientLogin = _db.ClientLogins.FirstOrDefault(cl =>
                    cl.Id == clientLoginId &&
                    cl.IsActive &&
                    cl.Deleted == null);

                if (clientLogin == null)
                {
                    filterContext.Result = new HttpStatusCodeResult(HttpStatusCode.Unauthorized, "Invalid session");
                    return;
                }

                // 5. Extend cookie expiration (+4 hours) - NO DATABASE UPDATE
                // This creates a sliding window session timeout
                var isHttps = filterContext.HttpContext.Request.IsSecureConnection ||
                    filterContext.HttpContext.Request.Headers["X-Forwarded-Proto"]?.Equals("https", StringComparison.OrdinalIgnoreCase) == true;

                // PRODUCTION VERSION (commented for local testing)
                var updatedCookie = new HttpCookie(COOKIE_NAME, clientLoginId.ToString())
                {
                    Expires = DateTime.Now.AddMinutes(TIMEOUT_MINUTES),
                    Secure = isHttps,
                    HttpOnly = true,
                    Domain = ".fifthcolor.com",
                    Path = "/",
                    SameSite = isHttps ? SameSiteMode.None : SameSiteMode.Lax
                };

                // LOCAL TESTING VERSION (remove domain restriction, allow HTTP)
                //var updatedCookie = new HttpCookie(COOKIE_NAME, clientLoginId.ToString())
                //{
                //    Expires = DateTime.Now.AddMinutes(TIMEOUT_MINUTES),
                //    Secure = false, // Allow HTTP for local testing
                //    HttpOnly = true,
                //    Domain = null, // Works with localhost
                //    Path = "/",
                //    SameSite = SameSiteMode.Lax // Lax for HTTP
                //};

                filterContext.HttpContext.Response.SetCookie(updatedCookie);
            }

            base.OnActionExecuting(filterContext);
        }
    }
}
