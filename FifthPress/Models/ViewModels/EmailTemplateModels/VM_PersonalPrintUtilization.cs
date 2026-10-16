using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels.EmailTemplateModels
{
    public class VM_PersonalPrintUtilization
    {
        public fn_AgencyOverview_Result Yesterday { get; set; }
        public fn_AgencyOverview_Result YTD { get; set; }
        public string TargetUtilizationString { get; set; }
        public decimal? TargetUtilization { get; set; }
    }
}