using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.Models.ViewModels
{
    public class VM_OrderRequest
    { 
        public IEnumerable<SelectListItem> Materials { get; set; }
        public IEnumerable<SelectListItem> Clients { get; set; }
    }
}