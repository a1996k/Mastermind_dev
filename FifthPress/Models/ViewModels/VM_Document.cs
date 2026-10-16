using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_Document
    {
        public JobDocument JobDocument { get; set; }
        public QuoteDocument QuoteDocument { get; set; }
        public string DocumentName { get; set; }
    }
}