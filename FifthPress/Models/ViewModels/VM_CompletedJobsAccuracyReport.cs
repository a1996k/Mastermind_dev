using System;
using System.Collections.Generic;
using System.Web.Mvc;

namespace FifthPress.Models.ViewModels
{
    // Filter model for the Completed Jobs Estimated-vs-Actual report page.
    public class VM_CompletedJobsAccuracyFilter
    {
        public List<SelectListItem> Clients { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    // One row of the report, matching the columns returned by
    // sp_GetCompletedJobsEstVsActual. Property names become the JSON keys the
    // DataTable binds to.
    public class CompletedJobAccuracyRow
    {
        public int JobId { get; set; }
        public string JobNumber { get; set; }
        public string CustomerName { get; set; }
        public string JobDescription { get; set; }
        public int ItemCount { get; set; }
        public decimal? TotalEstimatedHours { get; set; }
        public decimal? TotalActualHours { get; set; }
        public decimal? OverUnderHours { get; set; }
    }
}
