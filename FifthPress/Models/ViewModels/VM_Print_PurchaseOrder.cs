using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using FifthPress.Models.CustomerQuoteModels;

namespace FifthPress.Models.ViewModels
{
    public class VM_Print_PurchaseOrder
    {
        public PO_Orders Order { get; set; }
        public SourceInformation SourceInformation { get; set; }
        public ShippingInformation ShippingInformation { get; set; }
    }
}