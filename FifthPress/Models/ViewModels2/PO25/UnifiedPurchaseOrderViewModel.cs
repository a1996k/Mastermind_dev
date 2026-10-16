using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class UnifiedPurchaseOrderViewModel
    {
        public List<PO_Orders25> RegularPOs { get; set; } = new List<PO_Orders25>();
        public List<RecurringScheduleViewModel> RecurringSchedules { get; set; } = new List<RecurringScheduleViewModel>();
    }
}