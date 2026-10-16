using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models
{
    public class EmployeeTimeSheet
    {
        public string Employee { get; set; }
        public List<Report_Time_WeeklyTimeReport_Result> Records { get; set; }
        public string TotalHoursWorkedFormatted { get; set; }
        public decimal TotalHoursWorked { get; set; }
        public string TotalNetWorkTimeFormatted { get; set; }
        public decimal TotalNetWorkTime { get; set; }
        public string TotalBreakTimeFormatted { get; set; }
        public decimal TotalBreakTime { get; set; }
    }

}