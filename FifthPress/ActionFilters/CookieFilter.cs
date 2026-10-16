using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.ModelBinding;
using System.Web.Mvc;
using System.Web.Routing;

namespace FifthPress.ActionFilters
{
    public class CookieFilter : ActionFilterAttribute
    {
        private readonly List<string> CookieNames = new List<string> { "ActiveUser", "UserRole", "Permissions" };
        private readonly string FIFTHCOLOR_PUBLIC_IP = "47.225.203.154";

        private string GetClientIpAddress(HttpRequestBase request)
        {
            // First check if the client IP is forwarded by a proxy or load balancer
            string ipAddress = request.ServerVariables["HTTP_X_FORWARDED_FOR"];

            if (!string.IsNullOrEmpty(ipAddress))
            {
                // The X-Forwarded-For header can contain a list of IPs (comma-separated).
                // The first IP in the list is the client’s original IP.
                string[] ipRange = ipAddress.Split(',');
                ipAddress = ipRange[0];
            }
            else
            {
                // If X-Forwarded-For is not set, fall back to the remote IP address
                ipAddress = request.ServerVariables["REMOTE_ADDR"];
            }

            return ipAddress;
        }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            // Retrieve required cookies
            var cookies = RetrieveCookies(filterContext);
            var userCookie = cookies.SingleOrDefault(c => c.Name == "ActiveUser");

            string clientIp = GetClientIpAddress(filterContext.HttpContext.Request);
            var ipMatch = clientIp == FIFTHCOLOR_PUBLIC_IP;
            //MastermindLogger.Log(LogLevel.Info, String.Format("IP: {0}, Match = {1}", clientIp, ipMatch));

            if (userCookie == null)
            {

                // No ActiveUser cookie found, exit early
                base.OnActionExecuting(filterContext);
                return;
            }

            int? userId = ParseUserId(userCookie.Value);
            if (userId == null)
            {
                // Invalid User ID in cookie, exit early
                base.OnActionExecuting(filterContext);
                return;
            }

            using (var db = new FifthPressEntities())
            {
                var user = db.Users.SingleOrDefault(u => u.Id == userId.Value);
                if (user == null)
                {
                    // User not found in the database, exit early
                    base.OnActionExecuting(filterContext);
                    return;
                }

                // Load related role and permissions
                db.Entry(user).Reference(u => u.Role1).Load();
                user.RetrievePermissions();

                // Check for tampering and update cookies if needed
                var userRoleCookie = cookies.SingleOrDefault(c => c.Name == "UserRole");
                var permissionsCookie = cookies.SingleOrDefault(c => c.Name == "Permissions");

                if (userRoleCookie == null || permissionsCookie == null)
                {
                    // One or more cookies are missing, exit early
                    base.OnActionExecuting(filterContext);
                    return;
                }

                if (DetectCookieTampering(userRoleCookie.Value, permissionsCookie.Value, user))
                {
                    throw new Exception("Stop Tampering with the cookies!");
                }

                // Update and refresh cookies
                UpdateCookies(filterContext, user, userRoleCookie, permissionsCookie, userCookie, db);
            }

            base.OnActionExecuting(filterContext);
        }

        private List<HttpCookie> RetrieveCookies(ActionExecutingContext filterContext)
        {
            return CookieNames
                .Select(cookieName => filterContext.HttpContext.Request.Cookies[cookieName])
                .Where(cookie => cookie != null)
                .ToList();
        }

        private bool IsPassthruPermission(string permissionId)
        {
            return permissionId.ToLower().EndsWith("s");
        }

        private int? ParseUserId(string userIdValue)
        {
            if (int.TryParse(userIdValue, out int userId))
            {
                return userId;
            }
            return null;
        }

        private bool DetectCookieTampering(string userRoleCookieValue, string permissionsCookieValue, User user)
        {
            string dbUserRole = user.Role1.Title;

            // SuperAdmin bypasses tampering detection - their permissions are rebuilt dynamically
            if (user.Role1.SuperAdmin)
            {
                return userRoleCookieValue != dbUserRole;
            }

            string dbPermissions = string.Join("", user.PermissionsModel.SelectMany(x => x.PermissionIdentifier));

            return userRoleCookieValue != dbUserRole || permissionsCookieValue != dbPermissions;
        }

        private string GetAllPermissionsFromManifest(FifthPressEntities db)
        {
            var allPermissions = db.PermissionsManifests
                .Select(p => p.PermissionId)
                .Distinct()
                .ToList();
            return string.Join("", allPermissions);
        }

        private void UpdateCookies(ActionExecutingContext filterContext, User user, HttpCookie userRoleCookie, HttpCookie permissionsCookie, HttpCookie userCookie, FifthPressEntities db)
        {
            // Update the values in the cookies
            userRoleCookie.Value = user.Role1.Title;

            // SuperAdmin gets all permissions dynamically from the manifest
            if (user.Role1.SuperAdmin)
            {
                permissionsCookie.Value = GetAllPermissionsFromManifest(db);
            }
            else
            {
                permissionsCookie.Value = string.Join("", user.PermissionsModel.SelectMany(x => x.PermissionIdentifier));
            }

            userCookie.Value = user.Id.ToString();

            // Set cookie expiration and secure properties
            SetCookieExpirationAndSecurity(userRoleCookie);
            SetCookieExpirationAndSecurity(permissionsCookie);
            SetCookieExpirationAndSecurity(userCookie);

            // Set updated cookies in the response
            filterContext.HttpContext.Response.SetCookie(userRoleCookie);
            filterContext.HttpContext.Response.SetCookie(permissionsCookie);
            filterContext.HttpContext.Response.SetCookie(userCookie);
        }

        private void SetCookieExpirationAndSecurity(HttpCookie cookie)
        {
            cookie.Expires = DateTime.Now.ToLocalTime().AddMinutes(240);
            cookie.Secure = false;  // Set to true if using HTTPS
        }
    }
}