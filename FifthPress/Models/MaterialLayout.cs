using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models
{
    /* this might be unnecessary */
    public class MaterialLayout
    {
        public bool Parsed { get; set; }
        
        public bool MixGrain { get; set; }
        
        public bool UserSwapped { get; set; }

        public int SheetCount { get; set; }

        public List<int> SheetItemCountBreakdown { get; set; }
    }
}