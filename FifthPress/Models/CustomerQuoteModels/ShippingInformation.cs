using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.CustomerQuoteModels
{
    public class ShippingInformation
    {
        public string CompanyName { get; set; }
        public string Attention { get; set; }
        public string Address { get; set; }
        public string Address2 { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string ZipCode { get; set; }
    }
}