using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_CardDisplay
    {
        public List<DashboardCard> Cards { get; set; }
        public VM_MultiselectModel2 UserSelect { get; set; }
        public VM_MultiselectModel2 DepartmentSelect { get; set; }
    }
}