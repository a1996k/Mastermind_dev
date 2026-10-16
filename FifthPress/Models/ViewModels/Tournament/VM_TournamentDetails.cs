using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels.Tournament
{
    public class VM_TournamentDetails
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Location { get; set; }
        public TournamentStatus Status { get; set; }
        public int? ClientId { get; set; }
        public string ClientName { get; set; }
        public DateTime? AdvanceWeekStartDate { get; set; }
        public DateTime? AdvanceWeekEndDate { get; set; }
        public DateTime Created { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? Modified { get; set; }
        public string ModifiedBy { get; set; }

        // Staff assignment summary (will be populated later)
        public List<VM_TournamentStaffSummary> StaffAssignments { get; set; }

        public VM_TournamentDetails()
        {
            StaffAssignments = new List<VM_TournamentStaffSummary>();
        }

        public string StatusDisplayName
        {
            get
            {
                switch (Status)
                {
                    case TournamentStatus.Planned:
                        return "Planned";
                    case TournamentStatus.Active:
                        return "Active";
                    case TournamentStatus.Completed:
                        return "Completed";
                    case TournamentStatus.Cancelled:
                        return "Cancelled";
                    default:
                        return "Unknown";
                }
            }
        }

        public string StatusCssClass
        {
            get
            {
                switch (Status)
                {
                    case TournamentStatus.Planned:
                        return "badge-primary";
                    case TournamentStatus.Active:
                        return "badge-success";
                    case TournamentStatus.Completed:
                        return "badge-secondary";
                    case TournamentStatus.Cancelled:
                        return "badge-danger";
                    default:
                        return "badge-light";
                }
            }
        }

        public string DateRange
        {
            get
            {
                if (StartDate.Date == EndDate.Date)
                {
                    return StartDate.ToString("MMMM dd, yyyy");
                }
                else if (StartDate.Month == EndDate.Month && StartDate.Year == EndDate.Year)
                {
                    return $"{StartDate.ToString("MMMM dd")}-{EndDate.ToString("dd, yyyy")}";
                }
                else
                {
                    return $"{StartDate.ToString("MMMM dd, yyyy")} - {EndDate.ToString("MMMM dd, yyyy")}";
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
                        return start.ToString("MMMM dd, yyyy");
                    }
                    else if (start.Month == end.Month && start.Year == end.Year)
                    {
                        return $"{start.ToString("MMMM dd")}-{end.ToString("dd, yyyy")}";
                    }
                    else
                    {
                        return $"{start.ToString("MMMM dd, yyyy")} - {end.ToString("MMMM dd, yyyy")}";
                    }
                }
                return "Not Set";
            }
        }

        public int DurationDays
        {
            get
            {
                return (int)(EndDate - StartDate).TotalDays + 1;
            }
        }

        public int? AdvanceWeekDurationDays
        {
            get
            {
                if (AdvanceWeekStartDate.HasValue && AdvanceWeekEndDate.HasValue)
                {
                    return (int)(AdvanceWeekEndDate.Value - AdvanceWeekStartDate.Value).TotalDays + 1;
                }
                return null;
            }
        }
    }

    public class VM_TournamentStaffSummary
    {
        public int AssignmentId { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string RoleName { get; set; }
        public decimal DailyRate { get; set; }
        public int ScheduledDays { get; set; }
        public int AdvanceWeekDays { get; set; }
        public decimal EstimatedCost { get; set; }
    }
}