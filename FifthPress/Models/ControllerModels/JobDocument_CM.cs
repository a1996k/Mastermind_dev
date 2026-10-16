using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ControllerModels
{
    public class JobDocument_CM
    {
        public string ItemType { get; set; }
        public IEnumerable<HttpPostedFileBase> TheDocs { get; set; }
        public string ApplyToAll { get; set; }
        public int ItemId { get; set; }
        public string RowType { get; set; } //outsource or standard
    }
}