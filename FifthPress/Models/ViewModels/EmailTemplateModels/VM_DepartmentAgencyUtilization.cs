using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels.EmailTemplateModels
{
    public class VM_DepartmentAgencyUtilization
    {
        public string DepartmentName { get; set; }

        // Raw data for each individual
        public List<CalculateUtilizationForAgency_Result> WeeklyResults { get; set; }

        public List<CalculateUtilizationForAgency_Result> YTDResults { get; set; }

        // Aggregated + summarized department metrics
        public DepartmentMetrics AggregatedMetrics { get; set; }

        public List<User> UsersForReference { get; set; }

        public class DepartmentMetrics
        {
            // -- EXISTING PROPERTIES --
            public decimal TotalTimeWeekly { get; set; }

            public decimal TotalUtilizationWeekly { get; set; }
            public decimal BillableUtilizationWeekly { get; set; }

            public decimal TotalTimeYTD { get; set; }
            public decimal TotalUtilizationYTD { get; set; }
            public decimal BillableUtilizationYTD { get; set; }

            public string TotalTimeWeeklyString { get; set; }
            public string TotalUtilizationWeeklyString { get; set; }
            public string BillableUtilizationWeeklyString { get; set; }

            public string TotalTimeYTDString { get; set; }
            public string TotalUtilizationYTDString { get; set; }
            public string BillableUtilizationYTDString { get; set; }

            // -- NEW: Internal Billable (Weekly + YTD) --
            public decimal TotalHours_InternalBillableWeekly { get; set; }

            public decimal Utilization_InternalBillableWeekly { get; set; }
            public string TotalHours_InternalBillableWeeklyString { get; set; }
            public string Utilization_InternalBillableWeeklyString { get; set; }

            public decimal TotalHours_InternalBillableYTD { get; set; }
            public decimal Utilization_InternalBillableYTD { get; set; }
            public string TotalHours_InternalBillableYTDString { get; set; }
            public string Utilization_InternalBillableYTDString { get; set; }

            // -- NEW: External Billable (Weekly + YTD) --
            public decimal TotalHours_ExternalBillableWeekly { get; set; }

            public decimal Utilization_ExternalBillableWeekly { get; set; }
            public string TotalHours_ExternalBillableWeeklyString { get; set; }
            public string Utilization_ExternalBillableWeeklyString { get; set; }

            public decimal TotalHours_ExternalBillableYTD { get; set; }
            public decimal Utilization_ExternalBillableYTD { get; set; }
            public string TotalHours_ExternalBillableYTDString { get; set; }
            public string Utilization_ExternalBillableYTDString { get; set; }

            // -- NEW: Non-Billable (Weekly + YTD) --
            public decimal TotalHours_NonBillableWeekly { get; set; }

            public decimal Utilization_NonBillableWeekly { get; set; }
            public string TotalHours_NonBillableWeeklyString { get; set; }
            public string Utilization_NonBillableWeeklyString { get; set; }

            public decimal TotalHours_NonBillableYTD { get; set; }
            public decimal Utilization_NonBillableYTD { get; set; }
            public string TotalHours_NonBillableYTDString { get; set; }
            public string Utilization_NonBillableYTDString { get; set; }

            public string TotalBillableTimeWeeklyString { get; set; }
            public string TotalBillableUtilizationWeeklyString { get; set; }

            public string TotalBillableTimeYTDString { get; set; }
            public string TotalBillableUtilizationYTDString { get; set; }

            // Target Utilization
            public decimal AverageTargetUtilization { get; set; }
            public string AverageTargetUtilizationString { get; set; }

            // New properties for Total Billable Time
            public decimal TotalBillableHoursWeekly { get; set; }
            public decimal TotalBillableHoursYTD { get; set; }
            public string TotalBillableHoursWeeklyString { get; set; }
            public string TotalBillableHoursYTDString { get; set; }

            // New properties
            public bool MeetsWeeklyTarget { get; set; }
            public bool MeetsYTDTarget { get; set; }

            public bool MeetsWeeklyExpectedTime { get; set; } // Flag: department meets weekly expected time
            public bool MeetsYTDExpectedTime { get; set; } // Flag: department meets YTD expected time
        }
    }
}