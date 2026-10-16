using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels.EmailTemplateModels
{
    public class VM_WeeklyAgencyDepartmentUtilization
    {
        public List<VM_PersonalAgencyUtilization> EmployeeRecords { get; set; }

        public Department Department { get; set; }
    }
}