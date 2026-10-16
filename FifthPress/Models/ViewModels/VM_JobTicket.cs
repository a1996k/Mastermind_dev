using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_JobTicket
    {
        public Job Job { get; set; }
        public List<ServiceType> ServiceTypes { get; set; }
        public List<JobItemGroup> ItemGroups { get; set; }
    }
}