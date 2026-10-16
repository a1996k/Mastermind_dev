using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.Models.ViewModels
{
    public class VM_QuoteItem
    {
        public QuoteItem QuoteItem { get; set; }
        public List<SelectListItem> SpeedDenominators { get; set; }
    }
}