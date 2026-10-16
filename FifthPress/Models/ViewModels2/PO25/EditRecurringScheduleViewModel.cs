using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web.Mvc;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class EditRecurringScheduleViewModel
    {
        public int RecurringId { get; set; }

        [Required]
        [Display(Name = "Vendor")]
        public int VendorId { get; set; }

        [Required]
        [Display(Name = "Description")]
        public string Description { get; set; }

        [Required]
        [Display(Name = "Monthly Amount")]
        [DataType(DataType.Currency)]
        public decimal MonthlyAmount { get; set; }

        [Required]
        [Display(Name = "Recurrence Type")]
        public string RecurrenceType { get; set; }

        [Required]
        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [Display(Name = "End Date")]
        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        [Display(Name = "Department")]
        public int DepartmentId { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; }

        [Display(Name = "This is an allocation (requires reconciliation)")]
        public bool IsAllocation { get; set; }

        // For dropdowns
        public IEnumerable<SelectListItem> Vendors { get; set; }
        public IEnumerable<SelectListItem> Departments { get; set; }

        public IEnumerable<SelectListItem> RecurrenceTypes => new[]
        {
            new SelectListItem { Value = "Monthly", Text = "Monthly" },
            new SelectListItem { Value = "Quarterly", Text = "Quarterly" },
            new SelectListItem { Value = "Annually", Text = "Annually" }
        };
    }
}