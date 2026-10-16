using System;
using System.Collections.Generic;
using System.Web.Mvc;

namespace FifthPress.Models.ViewModels
{
    // Filter model for the Press/Service Utilization report.
    // Items is a combined list of presses and services; each option value is a
    // token "S:<id>" (service) or "P:<id>" (press).
    public class VM_PressServiceUtilizationFilter
    {
        public List<SelectListItem> Items { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    // One row of the report, matching the columns returned by
    // sp_GetPressServiceUtilization.
    public class PressServiceUtilizationRow
    {
        public string Name { get; set; }
        public string Kind { get; set; }            // "Press" or "Service"
        public decimal TotalActualHours { get; set; }
        public decimal EstimatedHours { get; set; }
        public int DaysUsed { get; set; }
        public decimal MaxPossibleHours { get; set; }
        public decimal UtilizationPercent { get; set; }
    }
}
