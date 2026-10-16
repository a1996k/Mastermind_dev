using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models
{
    public class QuoteItemGroup
    {
        public GenericItemOrganizer ParentItem { get; set; }
        public List<GenericItemOrganizer> SubItems { get; set; }
    }
}