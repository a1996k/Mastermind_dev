using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ControllerModels
{
    public class Invoice_CM
    {
        public HttpPostedFileBase TheDoc { get; set; }
        public int ItemId { get; set; }
    }
}