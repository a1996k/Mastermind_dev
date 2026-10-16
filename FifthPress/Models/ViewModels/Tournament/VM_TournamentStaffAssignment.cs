using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.Models.ViewModels.Tournament
{
    public class VM_TournamentStaffAssignment
    {
        public int TournamentId { get; set; }
        public string TournamentName { get; set; }
        public DateTime TournamentStartDate { get; set; }
        public DateTime TournamentEndDate { get; set; }
        public DateTime? AdvanceWeekStartDate { get; set; }
        public DateTime? AdvanceWeekEndDate { get; set; }

        public List<VM_StaffAssignmentItem> Assignments { get; set; }
        public List<SelectListItem> AvailableUsers { get; set; }
        public List<SelectListItem> AvailableRoles { get; set; }

        public VM_TournamentStaffAssignment()
        {
            Assignments = new List<VM_StaffAssignmentItem>();
            AvailableUsers = new List<SelectListItem>();
            AvailableRoles = new List<SelectListItem>();
        }
    }

    public class VM_StaffAssignmentItem
    {
        public int Id { get; set; }
        public int TournamentId { get; set; }
        
        [Required(ErrorMessage = "Please select a user")]
        [Display(Name = "User")]
        public int UserId { get; set; }
        public string UserName { get; set; }
        
        [Required(ErrorMessage = "Please select a role")]
        [Display(Name = "Role")]
        public int TournamentRoleId { get; set; }
        public string RoleName { get; set; }
        public decimal DailyRate { get; set; }
        
        [Display(Name = "Notes")]
        public string Notes { get; set; }
        
        public bool IsActive { get; set; }
        
        // Schedule summary
        public int TotalScheduledDays { get; set; }
        public int AdvanceWeekDays { get; set; }
        public int TournamentWeekDays { get; set; }
        public decimal EstimatedCost { get; set; }
        
        public bool IsNew => Id == 0;
    }

    public class VM_AddStaffAssignment
    {
        public int TournamentId { get; set; }
        
        [Required(ErrorMessage = "Please select a user")]
        [Display(Name = "User")]
        public int UserId { get; set; }
        
        [Required(ErrorMessage = "Please select a role")]
        [Display(Name = "Role")]
        public int TournamentRoleId { get; set; }
        
        [Display(Name = "Notes")]
        public string Notes { get; set; }
        
        public List<SelectListItem> AvailableUsers { get; set; }
        public List<SelectListItem> AvailableRoles { get; set; }

        public VM_AddStaffAssignment()
        {
            AvailableUsers = new List<SelectListItem>();
            AvailableRoles = new List<SelectListItem>();
        }
    }

    public class VM_TournamentStaffSchedule
    {
        public int TournamentId { get; set; }
        public string TournamentName { get; set; }
        public DateTime TournamentStartDate { get; set; }
        public DateTime TournamentEndDate { get; set; }
        public DateTime? AdvanceWeekStartDate { get; set; }
        public DateTime? AdvanceWeekEndDate { get; set; }

        public List<VM_ScheduleStaffItem> Staff { get; set; }
        public List<DateTime> ScheduleDates { get; set; }

        public VM_TournamentStaffSchedule()
        {
            Staff = new List<VM_ScheduleStaffItem>();
            ScheduleDates = new List<DateTime>();
        }
    }

    public class VM_ScheduleStaffItem
    {
        public int AssignmentId { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string RoleName { get; set; }
        public decimal DailyRate { get; set; }
        
        public List<VM_ScheduleDateEntry> ScheduleEntries { get; set; }

        public VM_ScheduleStaffItem()
        {
            ScheduleEntries = new List<VM_ScheduleDateEntry>();
        }
    }

    public class VM_ScheduleDateEntry
    {
        public int Id { get; set; }
        public int TournamentStaffAssignmentId { get; set; }
        public DateTime ScheduleDate { get; set; }
        public bool IsScheduledToWork { get; set; }
        public bool IsAdvanceWeek { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public string Notes { get; set; }
        
        public bool IsWeekend => ScheduleDate.DayOfWeek == DayOfWeek.Saturday || ScheduleDate.DayOfWeek == DayOfWeek.Sunday;
        public string DateDisplayName => ScheduleDate.ToString("ddd MM/dd");
    }

    public class VM_AllTournamentSchedules
    {
        public List<VM_TournamentScheduleSummary> Tournaments { get; set; }

        public VM_AllTournamentSchedules()
        {
            Tournaments = new List<VM_TournamentScheduleSummary>();
        }
    }

    public class VM_TournamentScheduleSummary
    {
        public int TournamentId { get; set; }
        public string TournamentName { get; set; }
        public DateTime TournamentStartDate { get; set; }
        public DateTime TournamentEndDate { get; set; }
        public DateTime? AdvanceWeekStartDate { get; set; }
        public DateTime? AdvanceWeekEndDate { get; set; }
        public string Location { get; set; }
        public TournamentStatus Status { get; set; }
        public string ClientName { get; set; }
        
        public List<VM_ScheduleStaffItem> Staff { get; set; }
        public List<DateTime> ScheduleDates { get; set; }
        
        public int TotalStaffCount => Staff?.Count ?? 0;
        public int TotalScheduledDays => Staff?.Sum(s => s.ScheduleEntries?.Count(e => e.IsScheduledToWork) ?? 0) ?? 0;
        public decimal TotalEstimatedCost => Staff?.Sum(s => (s.ScheduleEntries?.Count(e => e.IsScheduledToWork) ?? 0) * s.DailyRate) ?? 0;

        public string DateRange
        {
            get
            {
                if (TournamentStartDate.Date == TournamentEndDate.Date)
                {
                    return TournamentStartDate.ToString("MMM dd, yyyy");
                }
                else if (TournamentStartDate.Month == TournamentEndDate.Month && TournamentStartDate.Year == TournamentEndDate.Year)
                {
                    return $"{TournamentStartDate.ToString("MMM dd")}-{TournamentEndDate.ToString("dd, yyyy")}";
                }
                else
                {
                    return $"{TournamentStartDate.ToString("MMM dd, yyyy")} - {TournamentEndDate.ToString("MMM dd, yyyy")}";
                }
            }
        }

        public string AdvanceWeekDateRange
        {
            get
            {
                if (AdvanceWeekStartDate.HasValue && AdvanceWeekEndDate.HasValue)
                {
                    var start = AdvanceWeekStartDate.Value;
                    var end = AdvanceWeekEndDate.Value;
                    
                    if (start.Date == end.Date)
                    {
                        return start.ToString("MMM dd, yyyy");
                    }
                    else if (start.Month == end.Month && start.Year == end.Year)
                    {
                        return $"{start.ToString("MMM dd")}-{end.ToString("dd, yyyy")}";
                    }
                    else
                    {
                        return $"{start.ToString("MMM dd, yyyy")} - {end.ToString("MMM dd, yyyy")}";
                    }
                }
                return "Not Set";
            }
        }

        public VM_TournamentScheduleSummary()
        {
            Staff = new List<VM_ScheduleStaffItem>();
            ScheduleDates = new List<DateTime>();
        }
    }
}