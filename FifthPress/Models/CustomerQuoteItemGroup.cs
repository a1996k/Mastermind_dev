using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models
{
    public class CustomerQuoteItemGroup
    {
        public GenericItemOrganizer ParentItem { get; set; }
        public List<GenericItemOrganizer> SubItems { get; set; }
        public decimal TotalPrice { get; set; }
    }
}