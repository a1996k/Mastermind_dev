using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_UserRoleManagement25
    {
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public int? UserDepartmentId { get; set; }
        public List<VM_UserRole25> CurrentRoles { get; set; }
        public List<VM_AvailableRole25> AvailableRoles { get; set; }
        public SelectList Departments { get; set; }
    }
}