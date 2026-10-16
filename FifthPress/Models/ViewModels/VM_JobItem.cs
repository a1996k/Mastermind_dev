using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.Models.ViewModels
{
    public class VM_JobItem
    {
        public JobItem JobItem { get; set; }
        public List<SelectListItem> SpeedDenominators { get; set; }
        public int? LatestProofStatus { get; set; }
    }
}