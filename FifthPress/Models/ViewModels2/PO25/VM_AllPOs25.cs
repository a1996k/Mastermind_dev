using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_AllPOs25
    {
        public List<VM_SimplePO25> POs { get; set; }
        public string StatusFilter { get; set; }
        public int DepartmentFilter { get; set; }
        public DateTime? StartDateFilter { get; set; }
        public DateTime? EndDateFilter { get; set; }
        public SelectList Departments { get; set; }
        public SelectList Statuses { get; set; }
    }
}