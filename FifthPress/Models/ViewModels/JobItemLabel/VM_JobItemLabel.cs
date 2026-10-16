using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using QRCoder;

namespace FifthPress.Models.ViewModels.JobItemLabel
{
    public class VM_JobItemLabel
    {
        public int JobItemId { get; set; }
        public string JobNumber { get; set; }
        public string ClientName { get; set; }
        public string ItemDescription { get; set; }

        public string QRCodeBase64 { get; set; }
    }
}