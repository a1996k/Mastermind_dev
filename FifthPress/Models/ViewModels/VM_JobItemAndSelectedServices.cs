using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_JobItemAndSelectedServices
    {
        public JobItem JobItem { get; set; }
        public List<VM_SelectedJobItemService> JobItemServices { get; set; }
    }
}