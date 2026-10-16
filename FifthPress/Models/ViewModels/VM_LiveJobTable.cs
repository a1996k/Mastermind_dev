using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_LiveJobTable
    {
        public List<VM_ForecastedJob> Jobs { get; set; }
        public List<VM_DepartmentTotal> DepartmentTotals { get; set; }
    }
}