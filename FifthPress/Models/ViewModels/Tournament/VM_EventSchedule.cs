using System;
using System.Collections.Generic;

namespace FifthPress.Models.ViewModels.Tournament
{
    public class VM_EventSchedule
    {
        public VM_EventSchedule()
        {
            EventStaff = new List<VM_EventStaffMember>();
            Tournaments = new List<VM_TournamentSummary>();
            CurrentYear = DateTime.Now.Year;
        }

        public List<VM_EventStaffMember> EventStaff { get; set; }
        public List<VM_TournamentSummary> Tournaments { get; set; }
        public int CurrentYear { get; set; }
        public bool IsAdmin { get; set; }
    }

    public class VM_EventStaffMember
    {
        public int UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string FullName { get; set; }
        public string Username { get; set; }
        public string Login { get; set; }
        public DateTime? TerminationDate { get; set; }
    }

    public class VM_TournamentSummary
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Location { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime? AdvanceWeekStartDate { get; set; }
        public DateTime? AdvanceWeekEndDate { get; set; }
        public TournamentStatus Status { get; set; }
        public string StatusDisplay { get; set; }
        public string ClientName { get; set; }
        public decimal MinimumWage { get; set; }
        public decimal PerDiem { get; set; }
        public decimal TravelPerDiem { get; set; }
        public bool OTByDay { get; set; }
    }
}