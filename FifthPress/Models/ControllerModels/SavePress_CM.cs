using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Web;

namespace FifthPress.Models.ControllerModels
{
    public class SavePress_CM
    {
        public Press theItem { get; set; }
        public List<DefaultPressService> DefaultPressServices { get; set; }

        public List<PressHelperService> HelperServices { get; set; }

        public int StockType { get; set; }
    }
}