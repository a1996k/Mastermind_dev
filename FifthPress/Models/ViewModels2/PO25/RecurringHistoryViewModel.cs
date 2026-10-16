using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class RecurringHistoryViewModel
    {
        public PO_RecurringSchedules25 Schedule { get; set; }
        public List<ProcessedOrderViewModel> ProcessedOrders { get; set; } = new List<ProcessedOrderViewModel>();
        public List<ReconciliationHistoryViewModel> ReconciliationHistory { get; set; } = new List<ReconciliationHistoryViewModel>();

        public decimal TotalProcessed => ProcessedOrders?.Sum(p => p.Amount) ?? 0;
        public int TotalOrdersGenerated => ProcessedOrders?.Count ?? 0;
        public decimal AverageMonthlySpend
        {
            get
            {
                var withActual = ReconciliationHistory?.Where(r => r.ActualSpent.HasValue).ToList();
                return (withActual != null && withActual.Any()) ? withActual.Average(r => r.ActualSpent.Value) : 0;
            }
        }
    }
}