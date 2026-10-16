using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.Dashboard
{
    public class AgencyUtilizationDatasetAverages
    {
        public decimal InternalBillableAverageUtilization { get; set; }
        public decimal ExternalBillableAverageUtilization { get; set; }
        public decimal NonBillableAverageUtilization { get; set; }
    }
}