using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ControllerModels
{
    public class PurchaseOrderSearch_CM
    {
        public string PONumber { get; set; }
        public string Vendor { get; set; }
        public string Status { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public DateTime? DueFrom { get; set; }  // new
        public DateTime? DueTo { get; set; }    // new
        public string Keywords { get; set; }
    }
}