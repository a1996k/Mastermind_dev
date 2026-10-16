using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json;

namespace FifthPress.Models.API.BambooHR
{
    public class BambooHRClockEntry
    {
        public string id { get; set; }
        public string employeeId { get; set; }

        // Different possible field names from BambooHR
        public DateTime? clockIn { get; set; }
        public DateTime? clockOut { get; set; }
        public string date { get; set; }
        public string start { get; set; }
        public string end { get; set; }
        public decimal? hours { get; set; }

        public string timezone { get; set; }
        public string note { get; set; }
        public decimal? totalHours { get; set; }

        [JsonExtensionData]
        public Dictionary<string, object> AdditionalData { get; set; }
    }
}
