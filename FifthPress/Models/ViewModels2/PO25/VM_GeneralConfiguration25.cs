using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_GeneralConfiguration25
    {
        [Display(Name = "Max Line Items per PO")]
        [Range(1, 100, ErrorMessage = "Max line items must be between 1 and 100")]
        public int MaxLineItemsPerPO { get; set; }

        [Display(Name = "Default Tax Rate")]
        [Range(0, 1, ErrorMessage = "Tax rate must be between 0 and 1 (e.g., 0.0875 for 8.75%)")]
        public decimal DefaultTaxRate { get; set; }

        [Display(Name = "Max File Upload Size (MB)")]
        [Range(1, 100, ErrorMessage = "File size must be between 1 and 100 MB")]
        public int MaxFileUploadSizeMB { get; set; }

        [Display(Name = "Allowed File Extensions")]
        [StringLength(500, ErrorMessage = "File extensions cannot exceed 500 characters")]
        public string AllowedFileExtensions { get; set; }

        [Display(Name = "System Maintenance Mode")]
        public bool SystemMaintenanceMode { get; set; }
    }
}