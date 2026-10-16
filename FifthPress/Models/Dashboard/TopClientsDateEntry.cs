using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.Dashboard
{
    public class TopClientsDateEntry
    {
        public DateTime Date { get; set; }
        public List<fn_ClientSpend_Result> Data { get; set; }
        public fn_ClientSpend_Result Aggregates { get; set; }
    }
}