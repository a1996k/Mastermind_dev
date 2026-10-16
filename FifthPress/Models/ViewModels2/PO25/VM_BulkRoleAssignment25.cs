using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_BulkRoleAssignment25
    {
        [Required]
        [Display(Name = "Role")]
        public int RoleId { get; set; }

        [Display(Name = "Department (optional)")]
        public int? DepartmentId { get; set; }

        [Required]
        [Display(Name = "Users")]
        public List<int> SelectedUserIds { get; set; } = new List<int>();

        public List<VM_AvailableRole25> AvailableRoles { get; set; }
        public SelectList Departments { get; set; }
        public List<SelectListItem> Users { get; set; }
    }
}