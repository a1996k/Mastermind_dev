using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_DepartmentTotal
    {
        public string Department { get; set; }
        public Nullable<decimal> DecimalHours { get; set; }
    }
}