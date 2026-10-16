using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models
{
    public class ReadyToBillEntry
    {
        public int Id { get; set; }
        public string JobNumber { get; set; }
        public string Description { get; set; }
        public string Client { get; set; }
        public DateTime Created { get; set; }
        public string CreatedText { get; set; }
        public DateTime Due { get; set; }
        public string DueText { get; set; }
        public DateTime Completed { get; set; }
        public string CompletedText { get; set; }
        public DateTime Shipped { get; set; }
        public string ShippedText { get; set; }
        public string ProjectManager { get; set; }
        public int User { get; set; }
        public string BillingNotes { get; set; }
    }
}