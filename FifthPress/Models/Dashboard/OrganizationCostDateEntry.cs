using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.Dashboard
{
    public class OrganizationCostDateEntry
    {
        public DateTime Date { get; set; }
        public fn_OrganizationCost_Result Data { get; set; }
    }
}