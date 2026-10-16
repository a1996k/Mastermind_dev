using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class RecurringScheduleViewModel
    {
        public int RecurringId { get; set; }
        public int ParentPOId { get; set; }
        public string ParentPOStatus { get; set; }
        public string Description { get; set; }
        public string DepartmentName { get; set; }
        public string RecurrenceType { get; set; }
        public decimal MonthlyAmount { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? NextProcessDate { get; set; }
        public DateTime? LastProcessedDate { get; set; }
        public bool IsAllocation { get; set; }
        public bool IsActive { get; set; }
        public string FormattedAmount { get; set; }

        // Helper property to get frequency display
        public string FrequencyDisplay
        {
            get
            {
                if (string.IsNullOrEmpty(RecurrenceType))
                    return "Unknown";

                switch (RecurrenceType.ToLower())
                {
                    case "monthly":
                        return "Monthly";

                    case "quarterly":
                        return "Quarterly";

                    case "annual":
                    case "yearly":
                        return "Annual";

                    default:
                        return RecurrenceType;
                }
            }
        }

        // Helper property to calculate actual recurring amount based on frequency
        public decimal ActualRecurringAmount
        {
            get
            {
                if (string.IsNullOrEmpty(RecurrenceType))
                    return MonthlyAmount;

                /*switch (RecurrenceType.ToLower())
                {
                    case "monthly":
                        return MonthlyAmount;

                    case "quarterly":
                        return MonthlyAmount * 3;

                    case "annual":
                    case "yearly":
                        return MonthlyAmount * 12;

                    default:
                        return MonthlyAmount;
                }*/

                return MonthlyAmount;
            }
        }

        public string FormattedRecurringAmount => ActualRecurringAmount.ToString("C");
    }
}