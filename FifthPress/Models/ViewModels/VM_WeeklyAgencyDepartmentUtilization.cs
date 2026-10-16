using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_WeeklyAgencyDepartmentUtilization
    {
        public string DepartmentName { get; set; } // The department name for this report
        public List<CalculateUtilizationForAgency_Result> WeeklyResults { get; set; } = new List<CalculateUtilizationForAgency_Result>();
        public CalculateUtilizationForAgency_Result YTD { get; set; } // YTD result for the department
        public string TargetUtilizationString { get; set; } // Display target as a string (e.g., "80%")
        public decimal? TargetUtilization { get; set; } // Target utilization percentage
    }
}