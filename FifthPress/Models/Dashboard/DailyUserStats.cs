using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.Dashboard
{
    public class DailyUserStats
    {
        public DateTime Date { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; }
        public decimal LateClockIns { get; set; }
        public decimal EarlyClockOuts { get; set; }
        public decimal WorkedHours { get; set; }
        public decimal ExternalBillableTime { get; set; }
        public decimal InternalBillableTime { get; set; }
        public decimal NonBillableTime { get; set; }
        public decimal ExternalUtilization { get; set; }
        public decimal InternalUtilization { get; set; }
        public decimal NonBillableUtilization { get; set; }
    }
}