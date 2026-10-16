using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_RoleManagement25
    {
        public List<VM_UserWithRoles25> Users { get; set; }
        public List<VM_AvailableRole25> AvailableRoles { get; set; }
        public SelectList Departments { get; set; }
    }
}