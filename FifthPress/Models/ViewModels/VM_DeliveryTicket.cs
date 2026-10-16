using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_DeliveryTicket
    {
        public Job Job { get; set; }
        public List<JobItemGroup> ItemGroups { get; set; }
    }
}