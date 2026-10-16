using FifthPress;
using System;
using System.Web;
using System.Web.Mvc;

namespace FifthPress
{
    /// <summary>
    /// Static helper class for easy role checking in controllers and views
    /// </summary>
    public static class PORoleHelper
    {
        /// <summary>
        /// Get PORoleService instance for current context
        /// </summary>
        private static PORoleService25 GetRoleService()
        {
            // You can implement caching here if needed
            var db = new FifthPressEntities();
            return new PORoleService25(db);
        }

        /// <summary>
        /// Get current user ID from session - fixed HttpContext reference
        /// </summary>
        private static int GetCurrentUserId()
        {
            var httpContext = HttpContext.Current;
            if (httpContext == null)
                throw new InvalidOperationException("HttpContext is not available");

            return UserSessionHelper.GetActiveUserId(new System.Web.HttpContextWrapper(httpContext));
        }

        /// <summary>
        /// Overloaded version that accepts HttpContext for controller use
        /// </summary>
        private static int GetCurrentUserId(HttpContextBase httpContext)
        {
            return UserSessionHelper.GetActiveUserId(httpContext);
        }

        #region Public Role Checking Methods - With HttpContext Parameter

        public static bool IsOwner(HttpContextBase httpContext)
        {
            var userId = GetCurrentUserId(httpContext);
            var roleService = GetRoleService();
            return roleService.IsOwner(userId);
        }

        public static bool IsDepartmentManager(HttpContextBase httpContext, int departmentId)
        {
            var userId = GetCurrentUserId(httpContext);
            var roleService = GetRoleService();
            return roleService.IsDepartmentManager(userId, departmentId);
        }

        public static bool IsDepartmentApprover(HttpContextBase httpContext, int departmentId)
        {
            var userId = GetCurrentUserId(httpContext);
            var roleService = GetRoleService();
            return roleService.IsDepartmentApprover(userId, departmentId);
        }

        public static bool IsPOAdmin(HttpContextBase httpContext)
        {
            var userId = GetCurrentUserId(httpContext);
            var roleService = GetRoleService();
            return roleService.IsPOAdmin(userId);
        }

        public static bool CanSubmitPOs(HttpContextBase httpContext)
        {
            var userId = GetCurrentUserId(httpContext);
            var roleService = GetRoleService();
            return roleService.CanSubmitPOs(userId);
        }

        public static bool CanManageInventory(HttpContextBase httpContext)
        {
            var userId = GetCurrentUserId(httpContext);
            var roleService = GetRoleService();
            return roleService.CanManageInventory(userId);
        }

        public static bool CanManageBudgets(HttpContextBase httpContext)
        {
            var userId = GetCurrentUserId(httpContext);
            var roleService = GetRoleService();
            return roleService.CanManageBudgets(userId);
        }

        public static bool CanApprove(HttpContextBase httpContext, int poId)
        {
            var userId = GetCurrentUserId(httpContext);
            var roleService = GetRoleService();
            return roleService.CanApprove(userId, poId);
        }

        public static bool HasRole(HttpContextBase httpContext, string roleName, int? departmentId = null)
        {
            var userId = GetCurrentUserId(httpContext);
            var roleService = GetRoleService();
            return roleService.HasRole(userId, roleName, departmentId);
        }

        #endregion Public Role Checking Methods - With HttpContext Parameter

        #region Public Role Checking Methods - Static (for Views with HttpContext.Current)

        public static bool IsOwner()
        {
            var userId = GetCurrentUserId();
            var roleService = GetRoleService();
            return roleService.IsOwner(userId);
        }

        public static bool IsOwner(int userId)
        {
            var roleService = GetRoleService();
            return roleService.IsOwner(userId);
        }

        public static bool IsDepartmentManager(int departmentId)
        {
            var userId = GetCurrentUserId();
            var roleService = GetRoleService();
            return roleService.IsDepartmentManager(userId, departmentId);
        }

        public static bool IsDepartmentManager(int userId, int departmentId)
        {
            var roleService = GetRoleService();
            return roleService.IsDepartmentManager(userId, departmentId);
        }

        public static bool IsDepartmentApprover(int departmentId)
        {
            var userId = GetCurrentUserId();
            var roleService = GetRoleService();
            return roleService.IsDepartmentApprover(userId, departmentId);
        }

        public static bool IsDepartmentApprover(int userId, int departmentId)
        {
            var roleService = GetRoleService();
            return roleService.IsDepartmentApprover(userId, departmentId);
        }

        public static bool IsPOAdmin()
        {
            var userId = GetCurrentUserId();
            var roleService = GetRoleService();
            return roleService.IsPOAdmin(userId);
        }

        public static bool IsPOAdmin(int userId)
        {
            var roleService = GetRoleService();
            return roleService.IsPOAdmin(userId);
        }

        public static bool CanSubmitPOs()
        {
            var userId = GetCurrentUserId();
            var roleService = GetRoleService();
            return roleService.CanSubmitPOs(userId);
        }

        public static bool CanSubmitPOs(int userId)
        {
            var roleService = GetRoleService();
            return roleService.CanSubmitPOs(userId);
        }

        public static bool CanManageInventory()
        {
            var userId = GetCurrentUserId();
            var roleService = GetRoleService();
            return roleService.CanManageInventory(userId);
        }

        public static bool CanManageBudgets()
        {
            var userId = GetCurrentUserId();
            var roleService = GetRoleService();
            return roleService.CanManageBudgets(userId);
        }

        public static bool CanApprove(int poId)
        {
            var userId = GetCurrentUserId();
            var roleService = GetRoleService();
            return roleService.CanApprove(userId, poId);
        }

        public static bool CanApprove(int userId, int poId)
        {
            var roleService = GetRoleService();
            return roleService.CanApprove(userId, poId);
        }

        public static bool HasRole(string roleName, int? departmentId = null)
        {
            var userId = GetCurrentUserId();
            var roleService = GetRoleService();
            return roleService.HasRole(userId, roleName, departmentId);
        }

        public static bool HasRole(int userId, string roleName, int? departmentId = null)
        {
            var roleService = GetRoleService();
            return roleService.HasRole(userId, roleName, departmentId);
        }

        #endregion Public Role Checking Methods - Static (for Views with HttpContext.Current)

        #region Authorization Helpers

        public static bool CanEditPO(int poId)
        {
            using (var db = new FifthPressEntities())
            {
                var po = db.PO_Orders25.Find(poId);
                if (po == null) return false;

                var userId = GetCurrentUserId();
                return po.SubmittedByUserId == userId || IsOwner();
            }
        }

        public static bool CanEditPO(HttpContextBase httpContext, int poId)
        {
            using (var db = new FifthPressEntities())
            {
                var po = db.PO_Orders25.Find(poId);
                if (po == null) return false;

                var userId = GetCurrentUserId(httpContext);
                return po.SubmittedByUserId == userId || IsOwner(httpContext);
            }
        }

        public static bool CanViewPO(int poId)
        {
            using (var db = new FifthPressEntities())
            {
                var po = db.PO_Orders25.Find(poId);
                if (po == null) return false;

                var userId = GetCurrentUserId();

                // Submitter can always view
                if (po.SubmittedByUserId == userId) return true;

                // Owner and admin can view all
                if (IsOwner() || IsPOAdmin()) return true;

                // Department approvers can view their department POs
                if (po.DepartmentId.HasValue && IsDepartmentApprover(po.DepartmentId.Value)) return true;

                return false;
            }
        }

        public static bool CanViewPO(HttpContextBase httpContext, int poId)
        {
            using (var db = new FifthPressEntities())
            {
                var po = db.PO_Orders25.Find(poId);
                if (po == null) return false;

                var userId = GetCurrentUserId(httpContext);

                // Submitter can always view
                if (po.SubmittedByUserId == userId) return true;

                // Owner and admin can view all
                if (IsOwner(httpContext) || IsPOAdmin(httpContext)) return true;

                // Department approvers can view their department POs
                if (po.DepartmentId.HasValue && IsDepartmentApprover(httpContext, po.DepartmentId.Value)) return true;

                return false;
            }
        }

        #endregion Authorization Helpers
    }
}

// Extension methods for easier use in views and controllers
namespace System.Web.Mvc
{
    public static class PORoleExtensions
    {
        // For Views (using HttpContext.Current)
        public static bool IsOwner(this HtmlHelper html)
        {
            return PORoleHelper.IsOwner();
        }

        public static bool IsPOAdmin(this HtmlHelper html)
        {
            return PORoleHelper.IsPOAdmin();
        }

        public static bool CanSubmitPOs(this HtmlHelper html)
        {
            return PORoleHelper.CanSubmitPOs();
        }

        public static bool CanManageInventory(this HtmlHelper html)
        {
            return PORoleHelper.CanManageInventory();
        }

        public static bool CanManageBudgets(this HtmlHelper html)
        {
            return PORoleHelper.CanManageBudgets();
        }

        public static bool HasPORole(this HtmlHelper html, string roleName, int? departmentId = null)
        {
            return PORoleHelper.HasRole(roleName, departmentId);
        }

        public static bool CanEditPO(this HtmlHelper html, int poId)
        {
            return PORoleHelper.CanEditPO(poId);
        }

        public static bool CanViewPO(this HtmlHelper html, int poId)
        {
            return PORoleHelper.CanViewPO(poId);
        }

        public static bool CanApprove(this HtmlHelper html, int poId)
        {
            return PORoleHelper.CanApprove(poId);
        }

        public static bool IsDepartmentManager(this HtmlHelper html, int departmentId)
        {
            return PORoleHelper.IsDepartmentManager(departmentId);
        }

        public static bool IsDepartmentApprover(this HtmlHelper html, int departmentId)
        {
            return PORoleHelper.IsDepartmentApprover(departmentId);
        }

        // For Controllers (using Controller.HttpContext)
        public static bool IsOwner(this Controller controller)
        {
            return PORoleHelper.IsOwner(controller.HttpContext);
        }

        public static bool IsPOAdmin(this Controller controller)
        {
            return PORoleHelper.IsPOAdmin(controller.HttpContext);
        }

        public static bool CanSubmitPOs(this Controller controller)
        {
            return PORoleHelper.CanSubmitPOs(controller.HttpContext);
        }

        public static bool CanManageInventory(this Controller controller)
        {
            return PORoleHelper.CanManageInventory(controller.HttpContext);
        }

        public static bool CanManageBudgets(this Controller controller)
        {
            return PORoleHelper.CanManageBudgets(controller.HttpContext);
        }

        public static bool HasPORole(this Controller controller, string roleName, int? departmentId = null)
        {
            return PORoleHelper.HasRole(controller.HttpContext, roleName, departmentId);
        }

        public static bool CanEditPO(this Controller controller, int poId)
        {
            return PORoleHelper.CanEditPO(controller.HttpContext, poId);
        }

        public static bool CanViewPO(this Controller controller, int poId)
        {
            return PORoleHelper.CanViewPO(controller.HttpContext, poId);
        }

        public static bool CanApprove(this Controller controller, int poId)
        {
            return PORoleHelper.CanApprove(controller.HttpContext, poId);
        }

        public static bool IsDepartmentManager(this Controller controller, int departmentId)
        {
            return PORoleHelper.IsDepartmentManager(controller.HttpContext, departmentId);
        }

        public static bool IsDepartmentApprover(this Controller controller, int departmentId)
        {
            return PORoleHelper.IsDepartmentApprover(controller.HttpContext, departmentId);
        }
    }
}