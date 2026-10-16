using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class ProcessedOrderViewModel
    {
        public int HistoryId { get; set; }
        public int PO_OrderId { get; set; }
        public DateTime ProcessedDate { get; set; }
        public decimal Amount { get; set; }
        public int FiscalYear { get; set; }
        public int Month { get; set; }
        public string OrderStatus { get; set; }

        public string FormattedAmount => Amount.ToString("C");
        public string MonthName => new DateTime(FiscalYear, Month, 1).ToString("MMMM yyyy");
        public string StatusBadgeClass => GetStatusBadgeClass(OrderStatus);

        private string GetStatusBadgeClass(string status)
        {
            string returnValue = "label-default";

            switch (status)
            {
                case "Draft":
                    returnValue = "label-secondary";
                    break;

                case "Pending":
                    returnValue = "label-warning";
                    break;

                case "Approved":
                    returnValue = "label-success";
                    break;

                case "Denied":
                    returnValue = "label-danger";
                    break;

                case "SentBack":
                    returnValue = "label-info";
                    break;

                case "Cancelled":
                    returnValue = "label-default";
                    break;

                default: break;
            }

            return returnValue;
        }
    }
}