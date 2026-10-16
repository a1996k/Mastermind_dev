using System;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_Amendment25
    {
        public int AmendmentId { get; set; }
        public int PO_OrderId { get; set; }
        public string AmendmentType { get; set; }
        public decimal? PreviousValue { get; set; }
        public decimal? NewValue { get; set; }
        public string AmendedBy { get; set; }
        public DateTime AmendedDate { get; set; }
        public string Reason { get; set; }

        /// <summary>
        /// Get friendly description of amendment type
        /// </summary>
        public string AmendmentTypeDescription
        {
            get
            {
                switch (AmendmentType)
                {
                    case "FreightChange":
                        return "Freight/Shipping";
                    case "TaxChange":
                        return "Tax Amount";
                    case "TaxRateChange":
                        return "Tax Rate";
                    default:
                        return AmendmentType;
                }
            }
        }

        /// <summary>
        /// Format the change for display
        /// </summary>
        public string ChangeDescription
        {
            get
            {
                if (AmendmentType == "TaxRateChange")
                {
                    return $"{PreviousValue?.ToString("F2") ?? "N/A"}% → {NewValue?.ToString("F2") ?? "N/A"}%";
                }
                return $"{PreviousValue?.ToString("C") ?? "N/A"} → {NewValue?.ToString("C") ?? "N/A"}";
            }
        }
    }

    public class VM_AmendTaxFreight25
    {
        public int PO_OrderId { get; set; }

        // Current values (read-only display)
        public decimal? CurrentFreightAmount { get; set; }
        public decimal? CurrentTaxAmount { get; set; }
        public decimal? CurrentTaxRate { get; set; }
        public bool CurrentIsTaxPercentage { get; set; }

        // New values (editable)
        public decimal? NewFreightAmount { get; set; }
        public decimal? NewTaxAmount { get; set; }
        public decimal? NewTaxRate { get; set; }
        public bool IsTaxPercentage { get; set; }

        // Required reason for amendment
        public string Reason { get; set; }

        // Amendment history for display
        public System.Collections.Generic.List<VM_Amendment25> AmendmentHistory { get; set; }

        // PO Info for display
        public string PONumber { get; set; }
        public decimal SubtotalAmount { get; set; }
        public string VendorName { get; set; }
        public string DepartmentName { get; set; }

        /// <summary>
        /// Calculate the current total with tax and freight
        /// </summary>
        public decimal CurrentTotal
        {
            get
            {
                var tax = CurrentIsTaxPercentage && CurrentTaxRate.HasValue
                    ? SubtotalAmount * (CurrentTaxRate.Value / 100)
                    : (CurrentTaxAmount ?? 0);
                return SubtotalAmount + (CurrentFreightAmount ?? 0) + tax;
            }
        }

        /// <summary>
        /// Calculate the new total with amended values
        /// </summary>
        public decimal NewTotal
        {
            get
            {
                var tax = IsTaxPercentage && NewTaxRate.HasValue
                    ? SubtotalAmount * (NewTaxRate.Value / 100)
                    : (NewTaxAmount ?? 0);
                return SubtotalAmount + (NewFreightAmount ?? 0) + tax;
            }
        }
    }
}
