using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class CreateRecurringOrderViewModel
    {
        public string OrderType { get; set; } = "Recurring";

        [Required]
        [Display(Name = "Vendor")]
        public int VendorId { get; set; }

        [Required]
        [Display(Name = "Department")]
        public int DepartmentId { get; set; }

        [Display(Name = "Order Description")]
        public string OrderDescription { get; set; }

        public List<RecurringLineItemViewModel> LineItems { get; set; } = new List<RecurringLineItemViewModel>();

        // For dropdowns
        public IEnumerable<SelectListItem> Vendors { get; set; }
        public IEnumerable<SelectListItem> Departments { get; set; }
    }
}