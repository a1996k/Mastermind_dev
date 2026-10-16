using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_JobCostSheet
    {
        public Job Job { get; set; }
        public List<VM_JobCostSheetGroup> ItemGroups { get; set; }
        public VM_AggregateTotals JobTotals { get; set; }
        public List<IGrouping<string, GetJobCostSheetTVF_Result>> RollUps { get; set; }

        // Client Cost Allocation
        public List<VM_ClientCostSplit> ClientCostSplits { get; set; }
        public bool HasMultipleClients => ClientCostSplits != null && ClientCostSplits.Count > 1;
    }
}