using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.Models.ViewModels
{
    public class VM_MultiselectModel2
    {
        public IEnumerable<SelectListItem> SelectList { get; set; }
        public string SelectName { get; set; }
    }
}