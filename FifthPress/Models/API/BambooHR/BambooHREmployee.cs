using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.API.BambooHR
{
    public class BambooHREmployee
    {
        public string id { get; set; }
        public string displayName { get; set; }
        public string firstName { get; set; }
        public string lastName { get; set; }
        public string preferredName { get; set; }
        public string jobTitle { get; set; }
        public string workEmail { get; set; }
        public string department { get; set; }
        public string location { get; set; }
        public string photoUrl { get; set; }
    }
}
