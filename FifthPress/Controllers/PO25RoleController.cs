using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using FifthPress.Models.ViewModels2.PO25;
using FifthPress.ActionFilters;

namespace FifthPress.Controllers
{
    [CookieFilter(Order = 1)]
    [UserPermissions(Order = 2)]
    public class PO25RoleController : DataController
    {
        private readonly PORoleService25 _roleService;

        public PO25RoleController()
        {
            _roleService = new PORoleService25(db);
        }

        public ActionResult Index()
        {
            var userId = UserSessionHelper.GetActiveUserId(HttpContext);

            if (!_roleService.IsOwner(userId) && !_roleService.IsPOAdmin(userId))
                return new HttpStatusCodeResult(403, "You do not have permission to manage roles.");

            var model = new VM_RoleManagement25
            {
                Users = _roleService.GetUsersWithRoles(),
                AvailableRoles = _roleService.GetAvailableRoles(),
                Departments = GetDepartments()
            };

            return View(model);
        }

        public ActionResult UserRoles(int id)
        {
            var userId = UserSessionHelper.GetActiveUserId(HttpContext);

            if (!_roleService.IsOwner(userId) && !_roleService.IsPOAdmin(userId))
                return new HttpStatusCodeResult(403, "You do not have permission to manage roles.");

            var user = db.Users.Find(id);
            if (user == null) return HttpNotFound();

            var model = new VM_UserRoleManagement25
            {
                UserId = id,
                UserName = user.Username,
                Email = user.Email,
                UserDepartmentId = user.AgencyDepartment,
                CurrentRoles = _roleService.GetUserRoles(id),
                AvailableRoles = _roleService.GetAvailableRoles(),
                Departments = GetDepartments()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AssignRole(int userId, int roleId, int? departmentId = null)
        {
            var currentUserId = UserSessionHelper.GetActiveUserId(HttpContext);

            if (!_roleService.IsOwner(currentUserId) && !_roleService.IsPOAdmin(currentUserId))
                return Json(new { success = false, message = "Permission denied" });

            try
            {
                var success = _roleService.AssignRole(userId, roleId, departmentId, currentUserId);

                if (success)
                {
                    var roleName = db.PO_Roles25.Where(r => r.RoleId == roleId).Select(r => r.RoleName).FirstOrDefault();
                    var deptName = departmentId.HasValue ?
                        db.Departments.Where(d => d.Id == departmentId).Select(d => d.Title).FirstOrDefault() :
                        "System-wide";

                    return Json(new
                    {
                        success = true,
                        message = $"Role '{roleName}' assigned for {deptName}"
                    });
                }
                else
                {
                    return Json(new { success = false, message = "Role assignment failed or already exists" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RemoveRole(int userId, int roleId, int? departmentId = null)
        {
            var currentUserId = UserSessionHelper.GetActiveUserId(HttpContext);

            if (!_roleService.IsOwner(currentUserId) && !_roleService.IsPOAdmin(currentUserId))
                return Json(new { success = false, message = "Permission denied" });

            try
            {
                var success = _roleService.RemoveRole(userId, roleId, departmentId);

                if (success)
                {
                    var roleName = db.PO_Roles25.Where(r => r.RoleId == roleId).Select(r => r.RoleName).FirstOrDefault();
                    return Json(new
                    {
                        success = true,
                        message = $"Role '{roleName}' removed successfully"
                    });
                }
                else
                {
                    return Json(new { success = false, message = "Role removal failed or role not found" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        public ActionResult RoleUsers(int id)
        {
            var userId = UserSessionHelper.GetActiveUserId(HttpContext);

            if (!_roleService.IsOwner(userId) && !_roleService.IsPOAdmin(userId))
                return new HttpStatusCodeResult(403, "You do not have permission to view role assignments.");

            var role = db.PO_Roles25.Find(id);
            if (role == null) return HttpNotFound();

            var usersWithRole = db.PO_UserRoles25
                .Where(ur => ur.RoleId == id && ur.IsActive)
                .Include(ur => ur.User1)
                .Include(ur => ur.Department)
                .Select(ur => new VM_RoleUser25
                {
                    UserId = ur.UserId,
                    UserName = ur.User1.Username,
                    Email = ur.User1.Email,
                    DepartmentId = ur.DepartmentId,
                    DepartmentName = ur.Department != null ? ur.Department.Title : "System-wide",
                    AssignedDate = ur.AssignedDate
                })
                .OrderBy(u => u.UserName)
                .ToList();

            var model = new VM_RoleUserList25
            {
                RoleId = id,
                RoleName = role.RoleName,
                RoleDescription = role.Description,
                Users = usersWithRole
            };

            return View(model);
        }

        public ActionResult CreateRole()
        {
            var userId = UserSessionHelper.GetActiveUserId(HttpContext);

            if (!_roleService.IsOwner(userId))
                return new HttpStatusCodeResult(403, "Only owners can create new roles.");

            return View(new VM_CreateRole25());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateRole(VM_CreateRole25 model)
        {
            var userId = UserSessionHelper.GetActiveUserId(HttpContext);

            if (!_roleService.IsOwner(userId))
                return new HttpStatusCodeResult(403, "Only owners can create new roles.");

            if (!ModelState.IsValid)
                return View(model);

            try
            {
                if (db.PO_Roles25.Any(r => r.RoleName == model.RoleName))
                {
                    ModelState.AddModelError("RoleName", "Role name already exists");
                    return View(model);
                }

                var role = new PO_Roles25
                {
                    RoleName = model.RoleName,
                    Description = model.Description,
                    IsActive = true,
                    CreatedDate = DateTime.Now
                };

                db.PO_Roles25.Add(role);
                db.SaveChanges();

                TempData["SuccessMessage"] = $"Role '{model.RoleName}' created successfully.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error creating role: " + ex.Message);
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeactivateRole(int roleId)
        {
            var userId = UserSessionHelper.GetActiveUserId(HttpContext);

            if (!_roleService.IsOwner(userId))
                return Json(new { success = false, message = "Only owners can deactivate roles" });

            try
            {
                var role = db.PO_Roles25.Find(roleId);
                if (role == null)
                    return Json(new { success = false, message = "Role not found" });

                if (role.RoleName == "Owner")
                    return Json(new { success = false, message = "Cannot deactivate Owner role" });

                role.IsActive = false;
                db.SaveChanges();

                return Json(new { success = true, message = $"Role '{role.RoleName}' deactivated" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        public JsonResult GetDepartmentUsers(int departmentId)
        {
            try
            {
                var users = db.Users
                    .Where(u => u.AgencyDepartment == departmentId || departmentId == 0)
                    .Select(u => new
                    {
                        UserId = u.Id,
                        UserName = u.Username,
                        Email = u.Email
                    })
                    .OrderBy(u => u.UserName)
                    .ToList();

                return Json(new { success = true, users = users });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult CheckRoleRequirements(int roleId, int? departmentId)
        {
            try
            {
                var role = db.PO_Roles25.Find(roleId);
                if (role == null)
                    return Json(new { success = false, message = "Role not found" });

                var requiresDepartment = role.RoleName == "Department Manager" ||
                                       role.RoleName == "Department Approver";

                return Json(new
                {
                    success = true,
                    requiresDepartment = requiresDepartment,
                    roleName = role.RoleName,
                    description = role.Description
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        public ActionResult BulkAssign()
        {
            var userId = UserSessionHelper.GetActiveUserId(HttpContext);

            if (!_roleService.IsOwner(userId))
                return new HttpStatusCodeResult(403, "Only owners can perform bulk role assignments.");

            var model = new VM_BulkRoleAssignment25
            {
                AvailableRoles = _roleService.GetAvailableRoles(),
                Departments = GetDepartments(),
                Users = db.Users.Select(u => new SelectListItem
                {
                    Value = u.Id.ToString(),
                    Text = u.FirstName + " " + u.LastName + " (" + u.Email + ")"
                }).OrderBy(u => u.Text).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult BulkAssign(VM_BulkRoleAssignment25 model)
        {
            var userId = UserSessionHelper.GetActiveUserId(HttpContext);

            if (!_roleService.IsOwner(userId))
                return new HttpStatusCodeResult(403, "Only owners can perform bulk role assignments.");

            if (!ModelState.IsValid)
            {
                model.AvailableRoles = _roleService.GetAvailableRoles();
                model.Departments = GetDepartments();
                model.Users = db.Users.Select(u => new SelectListItem
                {
                    Value = u.Id.ToString(),
                    Text = u.FirstName + " " + u.LastName + " (" + u.Email + ")"
                }).OrderBy(u => u.Text).ToList();
                return View(model);
            }

            int successCount = 0;
            int failCount = 0;

            try
            {
                foreach (var targetUserId in model.SelectedUserIds)
                {
                    var success = _roleService.AssignRole(targetUserId, model.RoleId, model.DepartmentId, userId);
                    if (success) successCount++;
                    else failCount++;
                }

                TempData["SuccessMessage"] = $"Bulk assignment completed: {successCount} successful, {failCount} failed.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error during bulk assignment: " + ex.Message);
                model.AvailableRoles = _roleService.GetAvailableRoles();
                model.Departments = GetDepartments();
                model.Users = db.Users.Select(u => new SelectListItem
                {
                    Value = u.Id.ToString(),
                    Text = u.Username + " (" + u.Email + ")"
                }).OrderBy(u => u.Text).ToList();
                return View(model);
            }
        }

        #region Private Helper Methods

        private SelectList GetDepartments()
        {
            var departments = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "System-wide" }
            };

            departments.AddRange(
                db.Departments.OrderBy(d => d.Title)
                    .Select(d => new SelectListItem
                    {
                        Value = d.Id.ToString(),
                        Text = d.Title
                    }));

            return new SelectList(departments, "Value", "Text");
        }

        #endregion Private Helper Methods
    }
}