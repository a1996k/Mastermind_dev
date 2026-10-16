using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_ConstraintTableRecord
    {
        public string ConstraintRecordId { get; set; }
        public string ConstraintName { get; set; }
        public string ConstraintType { get; set; }
        public string LowerBounds { get; set; }
        public string UpperBounds { get; set; }
    }
}