using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_InventoryItem25
    {
        public int PO_LineItemId { get; set; }
        public int PO_OrderId { get; set; }
        public string ItemDescription { get; set; }
        public decimal Quantity { get; set; }
        public decimal Cost { get; set; }
        public decimal TotalValue { get; set; }
        public DateTime? DateOrdered { get; set; }
        public DateTime? DateExpected { get; set; }
        public DateTime? DateReceived { get; set; }
        public bool IsReceived { get; set; }
        public string DepartmentName { get; set; }
        public string OrderedBy { get; set; }
        public string StorageLocation { get; set; }
        public string InternalComments { get; set; }
        public string VendorLink { get; set; }
    }
}