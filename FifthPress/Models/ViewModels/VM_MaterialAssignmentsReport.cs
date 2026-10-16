using System.Collections.Generic;

namespace FifthPress.Models.ViewModels
{
    public class VM_MaterialAssignmentsReport
    {
        public List<MaterialAssignmentRow> Rows { get; set; }
    }

    public class MaterialAssignmentRow
    {
        public string MaterialCode { get; set; }
        public string Type { get; set; }
        public string JobNumber { get; set; }
        public int Allocated { get; set; }
        public string Pulled { get; set; }
        // Quantity already pulled from stock for this (material, job). In Stock is
        // already net of this, so it must be subtracted from Allocated when comparing
        // demand against stock — otherwise pulls get counted twice.
        public int PulledAmount { get; set; }
        public decimal InStock { get; set; }
        public int OnOrder { get; set; }
        public decimal? MinLevel { get; set; }

        // Remaining demand on stock for this row = gross allocation minus what's pulled.
        public int Remaining => Allocated - PulledAmount;

        // Net allocated (remaining) across all jobs for this material — set in controller after grouping
        public int TotalAllocatedForMaterial { get; set; }
        // True when (InStock + OnOrder) cannot cover the material's net (un-pulled) allocation
        public bool IsShort { get; set; }
    }
}
