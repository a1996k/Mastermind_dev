using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels.EmailTemplateModels
{
    public class ProductionOverviewDto
    {
        public int? UserId { get; set; }
        public string UserName { get; set; }
        public decimal TotalWorkedHours { get; set; }
        public decimal TotalJobTime { get; set; }
        public decimal AvgUtilization { get; set; }

        // New field that isn't in fn_AgencyOverview_Result
        public decimal? TargetUtilization { get; set; }

        // New properties
        public bool MeetsOverallWeekUtzTarget { get; set; }
        public bool MeetsOverallYTDUtzTarget { get; set; }
    }
}