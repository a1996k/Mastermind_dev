using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels.Tournament
{
    public class VM_TournamentIndex
    {
        public List<VM_TournamentListItem> Tournaments { get; set; }

        public VM_TournamentIndex()
        {
            Tournaments = new List<VM_TournamentListItem>();
        }
    }

    public class VM_TournamentListItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Location { get; set; }
        public TournamentStatus Status { get; set; }
        public string ClientName { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? AdvanceWeekStartDate { get; set; }
        public DateTime? AdvanceWeekEndDate { get; set; }
        
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

        public string DateRange
        {
            get
            {
                if (StartDate.Date == EndDate.Date)
                {
                    return StartDate.ToString("MMM dd, yyyy");
                }
                else if (StartDate.Month == EndDate.Month && StartDate.Year == EndDate.Year)
                {
                    return $"{StartDate.ToString("MMM dd")}-{EndDate.ToString("dd, yyyy")}";
                }
                else
                {
                    return $"{StartDate.ToString("MMM dd, yyyy")} - {EndDate.ToString("MMM dd, yyyy")}";
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
    }
}