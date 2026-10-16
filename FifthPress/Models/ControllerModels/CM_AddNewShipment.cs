using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ControllerModels
{
    public class CM_AddNewShipment
    {
        public string Title { get; set; }
        public string Notes { get; set; }
        public List<int> JobIds { get; set; }
    }
}