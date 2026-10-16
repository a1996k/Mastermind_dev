using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using FifthPress.Models.ViewModels.CardViewModels;

namespace FifthPress.Models.ViewModels.CardViewModels
{
    public class VM_AgencyBurndownReport
    {
        public List<BurndownClientTotals> ClientTotals { get; set; }
    }
}