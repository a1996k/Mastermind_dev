using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_Invoice
    {
        public Job Job { get; set; }
        public List<VM_InvoiceRow> Rows { get; set; }
        public decimal TotalPrice { get; set; }

        // Client Cost Allocation
        public List<VM_ClientCostSplit> ClientCostSplits { get; set; }
        public bool HasMultipleClients => ClientCostSplits != null && ClientCostSplits.Count > 1;
    }
}