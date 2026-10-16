using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_TimeTrackerJobList
    {
        public IEnumerable<IGrouping<string, GetJobsWithWeekdayFilter_Result>> GroupedJobs { get; set; }
        public List<JobItemService> PendingOverdueServices { get; set; }
        public List<IGrouping<string, JobItemService>> GeneralServicesByCategory { get; set; }
    }
}