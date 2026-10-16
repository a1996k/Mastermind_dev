using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using System.Web.Mvc;
using System.Text.RegularExpressions;

namespace FifthPress.ActionFilters
{
    using System;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Web;
    using System.Web.Mvc;

    public class UserPermissionsAttribute : ActionFilterAttribute
    {
        private readonly string FIFTHCOLOR_PUBLIC_IP = "47.225.203.154";
        private FifthPressEntities db;

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            // Check for the existence of the user cookie
            HttpCookie userCookie = filterContext.HttpContext.Request.Cookies.Get("ActiveUser");

            if (userCookie == null)
            {
                // Not logged in - redirect to homepage instead of showing 403
                filterContext.Result = new RedirectResult("~/");
                return;
            }

            // Attempt to parse the user ID from the cookie
            if (!TryGetUserId(userCookie, out int userId))
            {
                DenyAccess(filterContext, "Invalid user ID in cookie.");
                return;
            }

            // Check permissions for the user
            if (!CheckUserPermissions(filterContext, userId))
            {
                DenyAccess(filterContext, "Unauthorized");
                return;
            }

            base.OnActionExecuting(filterContext);
        }

        private bool TryGetUserId(HttpCookie userCookie, out int userId)
        {
            return int.TryParse(userCookie.Value, out userId);
        }

        private void DenyAccess(ActionExecutingContext filterContext, string errorMessage)
        {
            filterContext.Result = new ViewResult
            {
                ViewName = "Error403",
                ViewData = new ViewDataDictionary
                {
                    ["SpecificErrorMessage"] = errorMessage
                }
            };
            filterContext.HttpContext.Response.StatusCode = 403;
        }

        private bool CheckUserPermissions(ActionExecutingContext filterContext, int userId)
        {
            string url = filterContext.HttpContext.Request.Url.AbsolutePath.ToLower();

            // Extract the resource from the URL using a regex pattern
            string resource = ExtractResourceFromUrl(url);
            if (string.IsNullOrEmpty(resource)) return true;  // If no match, assume public access

            // Load the necessary user and permission data from the database
            PermissionsManifest permissionsManifest = GetPermissionsManifest(resource);
            if (permissionsManifest == null) return true;  // No secured resource, proceed without permission check

            // Check the user's permissions for this resource
            return UserHasPermission(userId, permissionsManifest.PermissionId);
        }

        private string ExtractResourceFromUrl(string url)
        {
            string pattern = @"/([^/]+)(?:/\d+\??[^/]*)?$";
            Match match = Regex.Match(url, pattern);
            return match.Success ? match.Groups[1].Value : null;
        }

        private PermissionsManifest GetPermissionsManifest(string resource)
        {
            using (db = new FifthPressEntities())
            {
                try
                {
                    return db.PermissionsManifests.SingleOrDefault(x => x.Url == resource);
                }
                catch (Exception)
                {
                    // Return null if no permissions manifest is found (indicating the URL is not secured)
                    return null;
                }
            }
        }

        private bool IsPassthruPermission(string permissionId)
        {
            return permissionId.ToLower().EndsWith("s");
        }

        private bool UserHasPermission(int userId, string requiredPermissionId)
        {
            using (db = new FifthPressEntities())
            {
                try
                {
                    // Load the user and their role's permissions
                    User user = db.Users.Single(x => x.Id == userId);
                    db.Entry(user).Reference(r => r.Role1).Load();

                    // SuperAdmin has access to everything
                    if (user.Role1.SuperAdmin)
                    {
                        return true;
                    }

                    bool isPassthruPermission = IsPassthruPermission(requiredPermissionId);

                    //if isPassthruPermission, return true: we wouldn't get this far without valid cookies (and thereby valid auth)
                    //the purpose of this check is to ensure passthru permissions don't result in denial for properly authenticated users
                    //^ also should remove the need for passthru pages to be included in role permission strings (redundant)
                    if (isPassthruPermission)
                    {
                        return true;
                    }
                    //otherwise return the result of the permission check
                    else
                    {
                        // Check if the user's role contains the required permission
                        return user.Role1.PermissionString.Contains(requiredPermissionId);
                    }
                }
                catch (Exception)
                {
                    return false;  // Return false if any error occurs while loading user permissions
                }
            }
        }
    }
}