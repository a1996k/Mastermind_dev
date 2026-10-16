using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VMMUserChartPermissions
    {
        public int UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public bool Included { get; set; }
    }
    public class VM_UserChartPermissions
    {
        public List<VMMUserChartPermissions> UnassignedUsers { get; set; }
        public List<VMMUserChartPermissions> AssignedUsers { get; set; }
        public string CardTitle { get; set; }
        public string RequestingUserFullName { get; set; }
    }
}