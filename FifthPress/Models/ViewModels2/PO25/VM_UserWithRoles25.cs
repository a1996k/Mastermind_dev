using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_UserWithRoles25
    {
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public int? DepartmentId { get; set; }
        public List<VM_UserRole25> Roles { get; set; } = new List<VM_UserRole25>();

        public bool HasRole(string roleName) => Roles.Any(r => r.RoleName == roleName);

        public bool IsOwner => HasRole("Owner");
        public bool CanApprovePOs => HasRole("Owner") || HasRole("DepartmentManager") || HasRole("DepartmentApprover");
    }
}