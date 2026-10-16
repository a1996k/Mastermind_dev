using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.ActionFilters
{


    public class RequirePOApprovalAccessAttribute : ActionFilterAttribute
    {
        private readonly FifthPressEntities _db = new FifthPressEntities();

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var userId = UserSessionHelper.GetActiveUserId(filterContext.HttpContext);

            if (!filterContext.ActionParameters.TryGetValue("id", out var idObj) || !int.TryParse(idObj?.ToString(), out int poId))
            {
                Deny(filterContext, "Invalid or missing PO ID.");
                return;
            }

            // Get services via DependencyResolver
            var db = DependencyResolver.Current.GetService<FifthPressEntities>();
            var approvalService = new ApprovalService25(db);

            // Check if PO exists
            if (!db.PO_Orders25.Any(p => p.PO_OrderId == poId))
            {
                Deny(filterContext, "Purchase order not found.");
                return;
            }

            // Determine if this is an approval action
            var isApprovalAction = filterContext.ActionDescriptor.ActionName.Equals("Approve", StringComparison.OrdinalIgnoreCase) ||
                                  filterContext.ActionDescriptor.ActionName.Equals("ProcessApproval", StringComparison.OrdinalIgnoreCase);

            if (isApprovalAction)
            {
                // For approval actions, use the full CanUserApprovePO check
                if (!approvalService.CanUserApprovePO(userId, poId))
                {
                    Deny(filterContext, "You don't have permission to approve this purchase order.");
                    return;
                }
            }
            else
            {
                // For non-approval actions (like Review), use a basic access check
                var po = db.PO_Orders25.Find(poId);
                var user = db.Users.Find(userId);

                // Allow access if:
                // 1. User is the submitter, or
                // 2. User is an approver for this PO's department, or
                // 3. User has admin/owner role
                var hasBasicAccess = po.SubmittedByUserId == userId ||
                                   db.PO_UserRoles25.Any(ur => ur.UserId == userId &&
                                                              ur.IsActive &&
                                                              (ur.DepartmentId == po.DepartmentId ||
                                                               ur.RoleId == 1 || // Owner
                                                               ur.RoleId == 4)); // Admin

                if (!hasBasicAccess)
                {
                    Deny(filterContext, "You don't have permission to view this purchase order.");
                    return;
                }
            }

            base.OnActionExecuting(filterContext);
        }

        private void Deny(ActionExecutingContext ctx, string reason)
        {
            PurchaseOrderLogger.Log($"[AUTH] Access denied: {reason}. User: {UserSessionHelper.GetActiveUserId(ctx.HttpContext)}, Action: {ctx.ActionDescriptor.ActionName}");

            ctx.Result = new ViewResult
            {
                ViewName = "Error403",
                ViewData = new ViewDataDictionary
                {
                    ["SpecificErrorMessage"] = reason
                }
            };
            ctx.HttpContext.Response.StatusCode = 403;
        }
    }
}