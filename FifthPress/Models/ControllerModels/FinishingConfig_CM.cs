using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ControllerModels
{
    public class FinishingConfig_CM
    {
        public int id { get; set; }
        public string value { get; set; }
        public int applicationValue { get; set; }

        public decimal cost { get; set; }
    }
}