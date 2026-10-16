using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ControllerModels
{
    public class ItemServiceDetails_CM
    {
        public int ServiceId { get; set; }
        public string ServiceTitle { get; set; }
        public int ServiceItemId { get; set; }
        public int RecordId { get; set; }
        public bool IsCuttingService { get; set; }
    }
}