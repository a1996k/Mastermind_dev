using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels.JobItemLabel
{
    public class VM_JobItemFiles
    {
        public List<JobDocument> JobDocuments { get; set; }
        public string JobTicketUrl { get; set; }
    }
}