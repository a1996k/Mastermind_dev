using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels.EmailTemplateModels
{
    public class VM_DepartmentPrintUtilization
    {
        // Weekly data
        public List<ProductionOverviewDto> WeeklyResults { get; set; }

        public ProductionOverviewDto WeeklyTotals { get; set; }

        // YTD data
        public List<ProductionOverviewDto> YTDResults { get; set; }

        public ProductionOverviewDto YTDTotals { get; set; }
    }
}