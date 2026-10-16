using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models
{
    /// <summary>
    /// Represents a golf event/tournament
    /// Maps to Client (where IsGolfClient = 1) + Tournament data
    /// </summary>
    public class Event
    {
        /// <summary>
        /// Client ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Tournament name (or Client.Title if no tournament)
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Event status: "pending", "active", or "completed"
        /// Calculated from tournament dates
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Setup phase start date
        /// From Tournament.SetupStartDate
        /// </summary>
        public DateTime? SetupStartDate { get; set; }

        /// <summary>
        /// Setup phase end date
        /// From Tournament.SetupEndDate
        /// </summary>
        public DateTime? SetupEndDate { get; set; }

        /// <summary>
        /// Advanced week start date
        /// From Tournament.AdvanceWeekStartDate
        /// </summary>
        public DateTime? AdvancedWeekStart { get; set; }

        /// <summary>
        /// Advanced week end date
        /// From Tournament.AdvanceWeekEndDate
        /// </summary>
        public DateTime? AdvancedWeekEnd { get; set; }

        /// <summary>
        /// Tournament start date
        /// From Tournament.StartDate
        /// </summary>
        public DateTime? TournamentStart { get; set; }

        /// <summary>
        /// Tournament end date
        /// From Tournament.EndDate
        /// </summary>
        public DateTime? TournamentEnd { get; set; }

        /// <summary>
        /// Teardown date
        /// From Tournament.TeardownEndDate or Tournament.TeardownStartDate
        /// </summary>
        public DateTime? TeardownDate { get; set; }

        /// <summary>
        /// Tournament location
        /// From Tournament.Location
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// Tournament description
        /// From Tournament.Description
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Total budget amount
        /// Calculated by summing all budget categories
        /// </summary>
        public decimal TotalBudget { get; set; }

        /// <summary>
        /// Client logo path
        /// From Client.ClientLogo (or default logo)
        /// </summary>
        public string ClientLogo { get; set; }

        /// <summary>
        /// Client name
        /// From Client.Title
        /// </summary>
        public string ClientName { get; set; }

        /// <summary>
        /// Collection of tournaments for this client
        /// Used when returning multiple tournaments per client
        /// </summary>
        public List<EventTournament> Tournaments { get; set; }
    }

    /// <summary>
    /// Represents a tournament within an event
    /// </summary>
    public class EventTournament
    {
        /// <summary>
        /// Tournament ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Tournament name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Tournament status: "pending", "active", or "completed"
        /// Calculated from tournament dates
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Setup phase start date
        /// </summary>
        public DateTime? SetupStartDate { get; set; }

        /// <summary>
        /// Setup phase end date
        /// </summary>
        public DateTime? SetupEndDate { get; set; }

        /// <summary>
        /// Advanced week start date
        /// </summary>
        public DateTime? AdvancedWeekStart { get; set; }

        /// <summary>
        /// Advanced week end date
        /// </summary>
        public DateTime? AdvancedWeekEnd { get; set; }

        /// <summary>
        /// Tournament start date
        /// </summary>
        public DateTime? TournamentStart { get; set; }

        /// <summary>
        /// Tournament end date
        /// </summary>
        public DateTime? TournamentEnd { get; set; }

        /// <summary>
        /// Teardown date
        /// </summary>
        public DateTime? TeardownDate { get; set; }

        /// <summary>
        /// Tournament location
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// Tournament description
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Total budget amount for this tournament
        /// Sum of labor costs, per diem, and travel expenses
        /// </summary>
        public decimal TotalBudget { get; set; }

        /// <summary>
        /// Advance week labor cost
        /// </summary>
        public decimal AdvanceWeekLaborCost { get; set; }

        /// <summary>
        /// Tournament week labor cost
        /// </summary>
        public decimal TournamentWeekLaborCost { get; set; }

        /// <summary>
        /// Total labor cost (advance + tournament week)
        /// </summary>
        public decimal TotalLaborCost { get; set; }

        /// <summary>
        /// Total per diem cost
        /// </summary>
        public decimal TotalPerDiemCost { get; set; }

        /// <summary>
        /// Total travel expenses
        /// </summary>
        public decimal TotalTravelExpenses { get; set; }

        /// <summary>
        /// Staff assigned to this tournament
        /// </summary>
        public List<EventTournamentStaff> Staff { get; set; }
    }

    /// <summary>
    /// Represents a staff member assigned to a tournament
    /// </summary>
    public class EventTournamentStaff
    {
        /// <summary>
        /// User ID
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// Staff member's full name
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// Role assigned for this tournament
        /// </summary>
        public string RoleName { get; set; }

        /// <summary>
        /// Daily rate for this role
        /// </summary>
        public decimal DailyRate { get; set; }

        /// <summary>
        /// Total days scheduled
        /// </summary>
        public int TotalDays { get; set; }

        /// <summary>
        /// Days scheduled during advance week
        /// </summary>
        public int AdvanceWeekDays { get; set; }

        /// <summary>
        /// Days scheduled during tournament week
        /// </summary>
        public int TournamentWeekDays { get; set; }

        /// <summary>
        /// Labor cost for advance week
        /// </summary>
        public decimal AdvanceWeekLaborCost { get; set; }

        /// <summary>
        /// Labor cost for tournament week
        /// </summary>
        public decimal TournamentWeekLaborCost { get; set; }

        /// <summary>
        /// Total labor cost
        /// </summary>
        public decimal TotalLaborCost { get; set; }

        /// <summary>
        /// Per diem cost
        /// </summary>
        public decimal PerDiemCost { get; set; }

        /// <summary>
        /// Travel expenses
        /// </summary>
        public decimal TravelExpenses { get; set; }

        /// <summary>
        /// Total cost for this staff member
        /// </summary>
        public decimal TotalCost { get; set; }
    }
}