using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models
{
    public class ClockUpdate
    {
        //user id
        public int UserId { get; set; }
        //clock activity id
        public int ClockActivityId { get; set; }
        //break record id
        public int? BreakRecordId { get; set; }
        //clock in time
        public DateTime ClockInTime { get; set; }
        //clock out time
        public DateTime? ClockOutTime { get; set; }
        //break start time
        public DateTime? BreakStartTime { get; set; }
        //break end time
        public DateTime? BreakEndTime { get; set; }
    }
}