using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using FifthPress.Models.ViewModels2.PO25;

namespace FifthPress
{
    public class PORoleService25
    {
        private readonly FifthPressEntities _db;

        public PORoleService25(FifthPressEntities db)
        {
            _db = db;
        }

        #region Role Checking Methods

        public bool HasRole(int userId, string roleName, int? departmentId = null)
        {
            return _db.PO_UserRoles25
                .Any(ur => ur.UserId == userId &&
                            ur.PO_Roles25.RoleName == roleName &&
                            ur.IsActive &&
                            ur.PO_Roles25.IsActive &&
                            (departmentId == null || ur.DepartmentId == departmentId || ur.DepartmentId == null));
        }

        public bool IsOwner(int userId)
        {
            return HasRole(userId, "Owner");
        }

        public bool IsDepartmentManager(int userId, int departmentId)
        {
            return HasRole(userId, "Department Manager", departmentId) || IsOwner(userId);
        }

        public bool IsDepartmentApprover(int userId, int departmentId)
        {
            return HasRole(userId, "Department Approver", departmentId) ||
                    IsDepartmentManager(userId, departmentId) ||
                    IsOwner(userId);
        }

        public bool IsPOAdmin(int userId)
        {
            // Check PO25-specific roles only (not system Roles table)
            return HasRole(userId, "Admin") || IsOwner(userId);
        }

        public bool IsExecutive(int userId)
        {
            return HasRole(userId, "Executive") || IsOwner(userId);
        }

        public bool IsAccounting(int userId)
        {
            return HasRole(userId, "Accounting");
        }

        public bool IsPurchasing(int userId)
        {
            return HasRole(userId, "Purchasing");
        }

        public bool IsInventoryManager(int userId)
        {
            return HasRole(userId, "Inventory Manager");
        }

        /// <summary>
        /// Check if user can see all departments in dropdown (based on PO_Roles25 only, not system Roles)
        /// </summary>
        public bool CanSeeAllDepartments(int userId)
        {
            return IsOwner(userId) ||
                   HasRole(userId, "Admin") ||
                   IsExecutive(userId) ||
                   IsAccounting(userId) ||
                   IsPurchasing(userId) ||
                   IsInventoryManager(userId);
        }

        /// <summary>
        /// Check if user can create company-wide POs (not tied to a specific department)
        /// </summary>
        public bool CanCreateCompanyWidePO(int userId)
        {
            return IsOwner(userId) || IsExecutive(userId) || IsAccounting(userId) || IsPurchasing(userId);
        }

        /// <summary>
        /// Check if user can manage company-wide budgets
        /// </summary>
        public bool CanManageCompanyBudgets(int userId)
        {
            return IsOwner(userId) || IsExecutive(userId) || IsAccounting(userId);
        }

        /// <summary>
        /// Check if user can skip normal approval flow (auto-approve) based on role and amount
        /// </summary>
        public bool CanSkipApproval(int userId, decimal amount)
        {
            // Owner can always skip approval
            if (IsOwner(userId)) return true;

            var config = new ConfigurationService25(_db);

            // Executive can auto-approve up to their threshold
            if (IsExecutive(userId))
                return amount <= config.GetConfigValue("AutoApproveExecutiveUnder", 5000m);

            // Accounting can auto-approve up to their threshold
            if (IsAccounting(userId))
                return amount <= config.GetConfigValue("AutoApproveAccountingUnder", 2500m);

            // Purchasing can auto-approve up to their threshold
            if (IsPurchasing(userId))
                return amount <= config.GetConfigValue("AutoApprovePurchasingUnder", 2500m);

            return false;
        }

        /// <summary>
        /// Check if user can amend approved POs (tax/freight changes)
        /// </summary>
        public bool CanAmendApprovedPO(int userId)
        {
            return IsOwner(userId) || IsPOAdmin(userId) || IsAccounting(userId) || IsPurchasing(userId) || IsInventoryManager(userId);
        }

        public bool CanSubmitPOs(int userId)
        {
            return HasRole(userId, "Submitter") ||
                    HasAnyApprovalRole(userId) ||
                    IsOwner(userId);
        }

        public bool CanManageInventory(int userId)
        {
            return HasRole(userId, "Inventory Manager") || IsOwner(userId);
        }

        public bool CanManageBudgets(int userId)
        {
            return HasRole(userId, "Budget Manager") || IsOwner(userId);
        }

        public bool CanOverrideBudget(int userId, int departmentId)
        {
            var overrideRoles = new[] { "Owner" };

            var roles = _db.PO_UserRoles25
                .Where(ur =>
                    ur.UserId == userId &&
                    ur.IsActive &&
                    (ur.DepartmentId == departmentId || ur.DepartmentId == null))
                .Select(ur => ur.PO_Roles25.RoleName)
                .ToList();

            return roles.Any(r => overrideRoles.Contains(r));
        }

        public bool HasAnyApprovalRole(int userId)
        {
            return _db.PO_UserRoles25
                .Any(ur => ur.UserId == userId &&
                            ur.IsActive &&
                            ur.PO_Roles25.IsActive &&
                            (ur.PO_Roles25.RoleName == "Owner" ||
                            ur.PO_Roles25.RoleName == "Admin" ||
                            ur.PO_Roles25.RoleName == "Executive" ||
                            ur.PO_Roles25.RoleName == "Accounting" ||
                            ur.PO_Roles25.RoleName == "Purchasing" ||
                            ur.PO_Roles25.RoleName == "Inventory Manager" ||
                            ur.PO_Roles25.RoleName == "Department Manager" ||
                            ur.PO_Roles25.RoleName == "Department Approver"));
        }

        #endregion Role Checking Methods

        #region Approval Logic

        public bool CanApprove(int userId, int poId)
        {
            // Owner can approve anything, regardless of department or submitter
            if (IsOwner(userId)) return true;

            // Admin can approve anything, regardless of department or submitter
            if (HasRole(userId, "Admin")) return true;

            // Executive can approve anything (system-wide role)
            if (IsExecutive(userId)) return true;

            // Accounting can approve anything (system-wide role)
            if (IsAccounting(userId)) return true;

            // Purchasing can approve anything (system-wide role)
            if (IsPurchasing(userId)) return true;

            // Inventory Manager can approve anything (system-wide role)
            if (IsInventoryManager(userId)) return true;

            // For other roles, check department membership and approval limits
            var po = _db.PO_Orders25.Find(poId);
            if (po == null) return false;

            var total = po.PO_LineItems25.Sum(li => li.CalculatedLineTotal);
            var userRoles = GetUserRoles(userId);
            bool isSubmitter = po.SubmittedByUserId == userId;

            // Department Manager - up to $5000, own department only, can approve own orders
            if (userRoles.Any(r => r.RoleName == "Department Manager" && r.DepartmentId == po.DepartmentId))
            {
                return total <= 5000;
            }

            // Department Approver - own department only, CANNOT approve own orders
            if (userRoles.Any(r => r.RoleName == "Department Approver" && r.DepartmentId == po.DepartmentId))
            {
                // Department Approvers cannot approve their own orders
                if (isSubmitter) return false;

                // For $0-$1000: Department Approver can approve
                if (total <= 1000) return true;

                // For $1001-$5000: Department Approver can approve (if not submitter)
                if (total <= 5000) return true;

                // For $5001+: Only Owner can approve
                return false;
            }

            return false;
        }

        public bool RequiresOwnerApproval(int poId)
        {
            var po = _db.PO_Orders25.Find(poId);
            if (po == null) return false;

            var totalAmount = po.PO_LineItems25.Sum(li => li.Cost * li.Quantity);
            return totalAmount > 5000;
        }

        public List<int> GetApprovalDepartments(int userId)
        {
            return _db.PO_UserRoles25
                .Where(ur => ur.UserId == userId &&
                            ur.IsActive &&
                            ur.PO_Roles25.IsActive &&
                            (ur.PO_Roles25.RoleName == "Department Manager" ||
                                ur.PO_Roles25.RoleName == "Department Approver") &&
                            ur.DepartmentId.HasValue)
                .Select(ur => ur.DepartmentId.Value)
                .Distinct()
                .ToList();
        }

        #endregion Approval Logic

        #region Role Management

        public List<VM_UserRole25> GetUserRoles(int userId)
        {
            return _db.PO_UserRoles25
                .Where(ur => ur.UserId == userId && ur.IsActive)
                .Include(ur => ur.PO_Roles25)
                .Include(ur => ur.Department)
                .Select(ur => new VM_UserRole25
                {
                    UserRoleId = ur.UserRoleId,
                    UserId = ur.UserId,
                    RoleId = ur.RoleId,
                    RoleName = ur.PO_Roles25.RoleName,
                    RoleDescription = ur.PO_Roles25.Description,
                    DepartmentId = ur.DepartmentId,
                    DepartmentName = ur.Department != null ? ur.Department.Title : "System-wide",
                    AssignedDate = ur.AssignedDate
                })
                .ToList();
        }

        public bool AssignRole(int userId, int roleId, int? departmentId = null, int assignedByUserId = 0)
        {
            try
            {
                // Check if assignment already exists
                var existing = _db.PO_UserRoles25
                    .FirstOrDefault(ur => ur.UserId == userId &&
                                            ur.RoleId == roleId &&
                                            ur.DepartmentId == departmentId);

                if (existing != null)
                {
                    if (!existing.IsActive)
                    {
                        existing.IsActive = true;
                        existing.AssignedDate = DateTime.Now;
                        existing.AssignedByUserId = assignedByUserId;
                    }
                }
                else
                {
                    var userRole = new PO_UserRoles25
                    {
                        UserId = userId,
                        RoleId = roleId,
                        DepartmentId = departmentId,
                        AssignedByUserId = assignedByUserId,
                        AssignedDate = DateTime.Now,
                        IsActive = true
                    };
                    _db.PO_UserRoles25.Add(userRole);
                }

                _db.SaveChanges();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool RemoveRole(int userId, int roleId, int? departmentId = null)
        {
            try
            {
                var userRole = _db.PO_UserRoles25
                    .FirstOrDefault(ur => ur.UserId == userId &&
                                            ur.RoleId == roleId &&
                                            ur.DepartmentId == departmentId);

                if (userRole != null)
                {
                    userRole.IsActive = false;
                    _db.SaveChanges();
                    return true;
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public List<VM_AvailableRole25> GetAvailableRoles()
        {
            return _db.PO_Roles25
                .Where(r => r.IsActive)
                .Select(r => new VM_AvailableRole25
                {
                    RoleId = r.RoleId,
                    RoleName = r.RoleName,
                    Description = r.Description
                })
                .OrderBy(r => r.RoleName)
                .ToList();
        }

        public List<VM_UserWithRoles25> GetUsersWithRoles(int? departmentId = null)
        {
            var query = _db.Users.Where(x => !(x.Disabled ?? true)).AsQueryable();

            if (departmentId.HasValue)
                query = query.Where(u => u.AgencyDepartment == departmentId);

            return query
                .Select(u => new VM_UserWithRoles25
                {
                    UserId = u.Id,
                    UserName = u.Username,
                    Email = u.Email,
                    DepartmentId = u.AgencyDepartment,
                    Roles = _db.PO_UserRoles25
                        .Where(ur => ur.UserId == u.Id && ur.IsActive)
                        .Select(ur => new VM_UserRole25
                        {
                            UserRoleId = ur.UserRoleId,
                            RoleId = ur.RoleId,
                            RoleName = ur.PO_Roles25.RoleName,
                            DepartmentId = ur.DepartmentId,
                            DepartmentName = ur.Department != null ? ur.Department.Title : "System-wide"
                        })
                        .ToList()
                })
                .OrderBy(u => u.UserName)
                .ToList();
        }

        #endregion Role Management
    }
}