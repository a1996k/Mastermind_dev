using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using FifthPress.Models.Deserialization;

namespace FifthPress.Models.Deserialization
{
    //redefine this to be the full approval model or change this to be a specific response model
    public class QAApproval
    {
        public List<QAResponse> Responses { get; set; }
        public int JobItemService { get; set; }
        public DateTime FirstApprovalTime { get; set; }
        public int? SecondApprover { get; set; }
        public bool FirstApproverPass { get; set; }
        public bool SecondApproverPass { get; set; }
        public DateTime PromptTime { get; set; }
    }
}