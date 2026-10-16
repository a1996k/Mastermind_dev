using System;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;

namespace FifthPress.ActionFilters
{
    /// <summary>
    /// Authorization filter that checks if user has Owner or Admin roles in the PO system
    /// </summary>
    public class POAdminAuthorizeAttribute : ActionFilterAttribute
    {
        public new int Order { get; set; }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            try
            {
                // Get current user ID
                int userId = UserSessionHelper.GetActiveUserId(filterContext.HttpContext);
                if (userId <= 0)
                {
                    // User not logged in or invalid session
                    filterContext.Result = new HttpStatusCodeResult(HttpStatusCode.Unauthorized,
                        "User not authenticated");
                    return;
                }

                // Create our own database context for this filter
                using (var db = new FifthPressEntities())
                {
                    // Check PO system roles
                    var userRoles = db.PO_UserRoles25
                        .Include(ur => ur.PO_Roles25)
                        .Where(ur => ur.UserId == userId && ur.IsActive && ur.PO_Roles25.IsActive)
                        .Select(ur => ur.PO_Roles25.RoleName)
                        .ToList();

                    // Check if user has Owner or Admin role
                    bool hasAdminAccess = userRoles.Contains("Owner") || userRoles.Contains("Admin");

                    if (!hasAdminAccess)
                    {
                        // User doesn't have required permissions
                        filterContext.Result = new HttpStatusCodeResult(HttpStatusCode.Forbidden,
                            "Access denied. Purchase Order administrative privileges required.");
                        return;
                    }

                    // Log admin access for audit purposes (optional)
                    System.Diagnostics.Debug.WriteLine($"PO Admin access granted to User ID: {userId}, Roles: {string.Join(", ", userRoles)}");
                }
            }
            catch (Exception ex)
            {
                // Log the exception and deny access
                System.Diagnostics.Debug.WriteLine($"Error in POAdminAuthorize filter: {ex.Message}");
                filterContext.Result = new HttpStatusCodeResult(HttpStatusCode.InternalServerError,
                    "An error occurred while checking permissions");
                return;
            }

            base.OnActionExecuting(filterContext);
        }
    }

    /// <summary>
    /// Extended version that allows specific roles to be specified
    /// </summary>
    public class POAuthorizeAttribute : ActionFilterAttribute
    {
        private readonly string[] _allowedRoles;

        public new int Order { get; set; }

        /// <summary>
        /// Constructor that accepts specific PO roles
        /// </summary>
        /// <param name="allowedRoles">Roles that are allowed access (e.g., "Owner", "Admin", "Department Manager")</param>
        public POAuthorizeAttribute(params string[] allowedRoles)
        {
            _allowedRoles = allowedRoles ?? new string[0];
        }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            try
            {
                int userId = UserSessionHelper.GetActiveUserId(filterContext.HttpContext);
                if (userId <= 0)
                {
                    filterContext.Result = new HttpStatusCodeResult(HttpStatusCode.Unauthorized,
                        "User not authenticated");
                    return;
                }

                // Create our own database context for this filter
                using (var db = new FifthPressEntities())
                {
                    var userRoles = db.PO_UserRoles25
                        .Include(ur => ur.PO_Roles25)
                        .Where(ur => ur.UserId == userId && ur.IsActive && ur.PO_Roles25.IsActive)
                        .Select(ur => ur.PO_Roles25.RoleName)
                        .ToList();

                    // Check if user has any of the allowed roles
                    bool hasAccess = _allowedRoles.Any(role => userRoles.Contains(role));

                    if (!hasAccess)
                    {
                        filterContext.Result = new HttpStatusCodeResult(HttpStatusCode.Forbidden,
                            $"Access denied. Required roles: {string.Join(", ", _allowedRoles)}");
                        return;
                    }

                    System.Diagnostics.Debug.WriteLine($"PO Access granted to User ID: {userId}, Required: [{string.Join(", ", _allowedRoles)}], User has: [{string.Join(", ", userRoles)}]");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in POAuthorize filter: {ex.Message}");
                filterContext.Result = new HttpStatusCodeResult(HttpStatusCode.InternalServerError,
                    "An error occurred while checking permissions");
                return;
            }

            base.OnActionExecuting(filterContext);
        }
    }

    /// <summary>
    /// Helper class to check PO roles in controllers or views
    /// </summary>
    public static class POPermissionHelper
    {
        /// <summary>
        /// Check if current user has specific PO roles
        /// </summary>
        public static bool HasPORoles(HttpContextBase httpContext, params string[] roles)
        {
            try
            {
                int userId = UserSessionHelper.GetActiveUserId(httpContext);
                if (userId <= 0) return false;

                using (var db = new FifthPressEntities())
                {
                    var userRoles = db.PO_UserRoles25
                        .Include(ur => ur.PO_Roles25)
                        .Where(ur => ur.UserId == userId && ur.IsActive && ur.PO_Roles25.IsActive)
                        .Select(ur => ur.PO_Roles25.RoleName)
                        .ToList();

                    return roles.Any(role => userRoles.Contains(role));
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if current user is PO admin (Owner or Admin)
        /// </summary>
        public static bool IsPOAdmin(HttpContextBase httpContext)
        {
            return HasPORoles(httpContext, "Owner", "Admin");
        }

        /// <summary>
        /// Get all PO roles for current user
        /// </summary>
        public static string[] GetUserPORoles(HttpContextBase httpContext)
        {
            try
            {
                int userId = UserSessionHelper.GetActiveUserId(httpContext);
                if (userId <= 0) return new string[0];

                using (var db = new FifthPressEntities())
                {
                    return db.PO_UserRoles25
                        .Include(ur => ur.PO_Roles25)
                        .Where(ur => ur.UserId == userId && ur.IsActive && ur.PO_Roles25.IsActive)
                        .Select(ur => ur.PO_Roles25.RoleName)
                        .ToArray();
                }
            }
            catch
            {
                return new string[0];
            }
        }

        /// <summary>
        /// Check if user has specific role for a specific department
        /// </summary>
        public static bool HasRoleForDepartment(HttpContextBase httpContext, string roleName, int departmentId)
        {
            try
            {
                int userId = UserSessionHelper.GetActiveUserId(httpContext);
                if (userId <= 0) return false;

                using (var db = new FifthPressEntities())
                {
                    return db.PO_UserRoles25
                        .Include(ur => ur.PO_Roles25)
                        .Any(ur => ur.UserId == userId &&
                                  ur.IsActive &&
                                  ur.PO_Roles25.IsActive &&
                                  ur.PO_Roles25.RoleName == roleName &&
                                  (ur.DepartmentId == departmentId || ur.DepartmentId == null)); // null = system-wide
                }
            }
            catch
            {
                return false;
            }
        }
    }
}