using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.Models.ViewModels
{
    public class VM_GlobalTimeTracking
    {
        public List<VM_ActiveTimeEntry> ActiveTimeEntries { get; set; }
        public bool HasPendingQACheck { get; set; }
        public List<SelectListItem> QAUsers { get; set; }
    }
}