using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_JobCostSheetGroup
    {
        public string ItemDescription { get; set; }
        public List<GetJobCostSheetTVF_Result> ItemPrices { get; set; }
        public VM_ItemLevelTotal ItemTotals { get; set; }
    }
}