using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models
{
    public class TimeEntry
    {
        public string TaskStartDate { get; set; }
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public DateTimeOffset SessionStartTime { get; set; }
        public DateTimeOffset SessionEndTime { get; set; }
        public int SessionDurationInSeconds { get; set; }
        public string SessionStartTimeString { get; set; }
        public string SessionEndTimeString { get; set; }
        public string DurationString { get; set; }
        public int ServiceId { get; set; }
        public string ServiceTitle { get; set; }
    }
}