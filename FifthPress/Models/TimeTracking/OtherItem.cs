using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.TimeTracking
{
    public class OtherItem
    {
        public int JobId { get; set; }
        public int ItemId { get; set; }
        public string JobNumberDisplay { get; set; }
        public string Description { get; set; }
        public int Ordinal { get; set; }
    }
}