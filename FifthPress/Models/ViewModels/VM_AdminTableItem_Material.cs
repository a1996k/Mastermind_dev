using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_AdminTableItem_Material
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Size { get; set; }
        public int Quantity { get; set; }
        public int Allocated { get; set; }
        public int Unallocated { get; set; }
        public string Caliper { get; set; }
        public string EntityName { get; set; }
        public string Category { get; set; }
        public decimal CostPerUnit { get; set; }
        public string CostPerUnitFormatted { get; set; }
        public string CostPerUnitUnit { get; set; }
    }
}