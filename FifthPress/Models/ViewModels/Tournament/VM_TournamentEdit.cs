using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.Models.ViewModels.Tournament
{
    public class VM_TournamentEdit
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Tournament name is required")]
        [StringLength(255, ErrorMessage = "Name cannot exceed 255 characters")]
        [Display(Name = "Tournament Name")]
        public string Name { get; set; }

        [Display(Name = "Description")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Start date is required")]
        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "End date is required")]
        [Display(Name = "End Date")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }

        [StringLength(255, ErrorMessage = "Location cannot exceed 255 characters")]
        [Display(Name = "Location")]
        public string Location { get; set; }

        [Required(ErrorMessage = "Status is required")]
        [Display(Name = "Status")]
        public TournamentStatus Status { get; set; }

        [Display(Name = "Client")]
        public int? ClientId { get; set; }

        [Display(Name = "Advance Week Start Date")]
        [DataType(DataType.Date)]
        public DateTime? AdvanceWeekStartDate { get; set; }

        [Display(Name = "Advance Week End Date")]
        [DataType(DataType.Date)]
        public DateTime? AdvanceWeekEndDate { get; set; }

        [Display(Name = "Setup Start Date")]
        [DataType(DataType.Date)]
        public DateTime? SetupStartDate { get; set; }

        [Display(Name = "Setup End Date")]
        [DataType(DataType.Date)]
        public DateTime? SetupEndDate { get; set; }

        [Display(Name = "Teardown Start Date")]
        [DataType(DataType.Date)]
        public DateTime? TeardownStartDate { get; set; }

        [Display(Name = "Teardown End Date")]
        [DataType(DataType.Date)]
        public DateTime? TeardownEndDate { get; set; }

        [Display(Name = "Minimum Wage")]
        [DataType(DataType.Currency)]
        public decimal MinimumWage { get; set; }

        [Display(Name = "Per Diem")]
        [DataType(DataType.Currency)]
        public decimal PerDiem { get; set; }

        [Display(Name = "Travel Per Diem")]
        [DataType(DataType.Currency)]
        public decimal TravelPerDiem { get; set; }

        [Display(Name = "Bill Rate")]
        [DataType(DataType.Currency)]
        public decimal? BillRate { get; set; }

        [Display(Name = "Manager Bill Rate")]
        [DataType(DataType.Currency)]
        public decimal? ManagerBillRate { get; set; }

        [Display(Name = "Print Bill Rate")]
        [DataType(DataType.Currency)]
        public decimal? PrintBillRate { get; set; }

        public bool OTByDay { get; set; }

        // Dropdown lists
        public List<SelectListItem> AvailableClients { get; set; }

        public VM_TournamentEdit()
        {
            AvailableClients = new List<SelectListItem>();
        }

        public List<SelectListItem> AvailableStatuses
        {
            get
            {
                return new List<SelectListItem>
                {
                    new SelectListItem { Value = "1", Text = "Planned" },
                    new SelectListItem { Value = "2", Text = "Active" },
                    new SelectListItem { Value = "3", Text = "Completed" },
                    new SelectListItem { Value = "4", Text = "Cancelled" }
                };
            }
        }

        public bool IsEditing => Id > 0;
    }

    // Custom validation attribute to ensure EndDate is after StartDate
    public class EndDateAfterStartDateAttribute : ValidationAttribute
    {
        public override bool IsValid(object value)
        {
            var model = value as VM_TournamentEdit;
            if (model == null) return true;

            return model.EndDate >= model.StartDate;
        }

        public override string FormatErrorMessage(string name)
        {
            return "End date must be on or after the start date";
        }
    }

    // Custom validation attribute to ensure advance week dates are before tournament dates
    public class AdvanceWeekBeforeTournamentAttribute : ValidationAttribute
    {
        public override bool IsValid(object value)
        {
            var model = value as VM_TournamentEdit;
            if (model == null) return true;

            // If advance week dates are not set, validation passes
            if (!model.AdvanceWeekStartDate.HasValue || !model.AdvanceWeekEndDate.HasValue)
                return true;

            // Advance week should end before or on tournament start date
            return model.AdvanceWeekEndDate.Value <= model.StartDate;
        }

        public override string FormatErrorMessage(string name)
        {
            return "Advance week must end before or on the tournament start date";
        }
    }
}