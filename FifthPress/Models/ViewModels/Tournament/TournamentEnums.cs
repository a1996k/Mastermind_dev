using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels.Tournament
{
    public enum TournamentStatus
    {
        [Display(Name = "Planned")]
        Planned = 1,
        
        [Display(Name = "Active")]
        Active = 2,
        
        [Display(Name = "Completed")]
        Completed = 3,
        
        [Display(Name = "Cancelled")]
        Cancelled = 4
    }

    public enum TravelType
    {
        [Display(Name = "Travel To")]
        TravelTo = 1,
        
        [Display(Name = "Travel From")]
        TravelFrom = 2,
        
        [Display(Name = "Both")]
        Both = 3
    }

    public enum ExpenseType
    {
        [Display(Name = "Hotel")]
        Hotel = 1,
        
        [Display(Name = "Rental Car")]
        RentalCar = 2,
        
        [Display(Name = "Airfare")]
        Airfare = 3,
        
        [Display(Name = "Mileage")]
        Mileage = 4,
        
        [Display(Name = "Other")]
        Other = 5
    }
}