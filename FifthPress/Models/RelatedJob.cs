using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models
{
    public class RelatedJob
    {
        public int QuoteId { get; set; }
        public string QuoteNumber { get; set; }
        public int JobId { get; set; }
        public string JobNumber { get; set; }
    }
}