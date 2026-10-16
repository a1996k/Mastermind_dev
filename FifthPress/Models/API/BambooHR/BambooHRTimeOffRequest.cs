using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json;

namespace FifthPress.Models.API.BambooHR
{
    public class BambooHRTimeOffRequest
    {
        public string id { get; set; }
        public string employeeId { get; set; }
        public BambooHRTimeOffStatus status { get; set; }
        public DateTime? start { get; set; }
        public DateTime? end { get; set; }
        public BambooHRTimeOffType type { get; set; }
        public BambooHRTimeOffAmount amount { get; set; }
        public BambooHRNotes notes { get; set; }
        public string name { get; set; }
        public Dictionary<string, string> dates { get; set; }

        [JsonExtensionData]
        public Dictionary<string, object> AdditionalData { get; set; }
    }

    public class BambooHRTimeOffStatus
    {
        public string status { get; set; }
        public string lastChanged { get; set; }
        public string lastChangedByUserId { get; set; }
    }

    public class BambooHRTimeOffAmount
    {
        public string unit { get; set; }
        public string amount { get; set; }
    }

    public class BambooHRNotes
    {
        public string employee { get; set; }
        public string manager { get; set; }
    }
}
