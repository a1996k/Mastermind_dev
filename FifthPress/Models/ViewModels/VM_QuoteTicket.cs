using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_QuoteTicket
    {
        public Quote Quote { get; set; }
        public List<ServiceType> ServiceTypes { get; set; }
        public List<QuoteItemGroup> ItemGroups { get; set; }
    }
}