using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels.EmailTemplateModels
{
    public class VM_PersonalAgencyUtilization
    {
        public CalculateUtilizationForAgency_Result Yesterday { get; set; }
        public CalculateUtilizationForAgency_Result YTD { get; set; }
        public string TargetUtilizationString { get; set; }
        public bool MeetsDailyBillableTarget { get; set; }
        public bool MeetsYearlyBillableTarget { get; set; }

        public bool MeetsDailyOverallTarget { get; set; }
        public bool MeetsYearlyOverallTarget { get; set; }
    }
}