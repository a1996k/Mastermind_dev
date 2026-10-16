using FifthPress.Models.CustomerQuoteModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_DeliveryTicket2
    {
        public Job Job { get; set; }
        public List<VM_InvoiceRow2> Items { get; set; }
        public SourceInformation SourceInformation { get; set; }
        public ClientInformation ClientInformation { get; set; }
        public ShippingInformation ShippingInformation { get; set; }
    }
}