using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Windows.Media.Converters;
using FifthPress.Models.QA;

namespace FifthPress.Models.QA
{
    public class QARecord
    {
        public string JobNumber { get; set; }
        public int ItemNumber { get; set; }
        public string ServiceTitle { get; set; }
        public DateTime FirstApprovalTime { get; set; }
        public string FirstApprovalTimeString { get; set; }
        public DateTime SecondApprovalTime { get; set; }
        public string SecondApprovalTimeString { get; set; }
        public string FirstApprovalUser { get; set; }
        public string SecondApprovalUser { get; set; }
        public bool Passed { get; set; }
        public List<QA> Responses { get; set; }
        public int JobItemService { get; set; }
        public int Id { get; set; }
    }
}