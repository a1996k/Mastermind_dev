using System;

namespace FifthPress.Models.API.BambooHR
{
    public class BambooHRWhosOutItem
    {
        public string Type { get; set; }
        public string Id { get; set; }
        public string EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public string HolidayName { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }
}
