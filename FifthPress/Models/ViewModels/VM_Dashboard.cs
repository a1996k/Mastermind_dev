using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_Dashboard
    {
        //public List<DashboardCard> Cards { get; set; }
        //public string UserRole { get; set; }
        //public int UserId { get; set; }
        /*public VM_MultiselectModel2 DepartmentSelect { get; set; }
        public VM_MultiselectModel2 UserSelect { get; set; }
        public List<VMMUserChartPermissions> UserPermissions { get; set; }*/
        public List<DashboardTab> Tabs { get; set; }
    }
}