using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_TimeSheetReport
    {
        public List<EmployeeTimeSheet> EmployeeTimeSheets { get; set; }
        public string ReportTotalHoursWorked { get; set; }
        public string ReportTotalNetWorkTime { get; set; }
        public string ReportTotalBreakTime { get; set; }
    }
}