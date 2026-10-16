using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models
{
    public class UserTimeTrackingAggregates
    {
        public int MondaySeconds { get; set; }
        public string MondayDisplay { get; set; }
        public int TuesdaySeconds { get; set; }
        public string TuesdayDisplay { get; set; }
        public int WednesdaySeconds { get; set; }
        public string WednesdayDisplay { get; set; }

        public int ThursdaySeconds { get; set; }
        public string ThursdayDisplay { get; set; }
        public int FridaySeconds { get; set; }
        public string FridayDisplay { get; set; }
        public int WeekendSeconds { get; set; }
        public string WeekendDisplay { get; set; }
        public int TotalWeekSeconds { get; set; }
        public string TotalWeekDisplay { get; set; }
    }
}