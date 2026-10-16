using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models
{
    public class PullMaterial
    {
        public string MaterialCode { get; set; }
        public string MaterialNotes { get; set; }
        public string FriendlyName { get; set; }
        public string UnitText { get; set; }
        public int AmountToPull { get; set; }
        public int AmountPulled { get; set; }
        public int MaterialId { get; set; }
    }
}