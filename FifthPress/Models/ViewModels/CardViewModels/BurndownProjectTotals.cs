using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Web;

namespace FifthPress.Models.ViewModels.CardViewModels
{
    public class BurndownProjectTotals
    {
        public int ClientId { get; set; }
        public string ProjectTitle { get; set; }
        public decimal OverallBudgetTime { get; set; }
        public string OverallBudgetTimeFormatted { get; set; }
        public decimal OverallActualTime { get; set; }
        public string OverallActualTimeFormatted { get; set; }
        public decimal OverallRemainingTime { get; set; }
        public string OverallRemainingTimeFormatted { get; set; }
        public decimal CreativeBudgetTime { get; set; }
        public string CreativeBudgetTimeFormatted { get; set; }
        public decimal CreativeActualTime { get; set; }
        public string CreativeActualTimeFormatted { get; set; }
        public decimal CreativeRemainingTime { get; set; }
        public string CreativeRemainingTimeFormatted { get; set; }
        public decimal StrategyBudgetTime { get; set; }
        public string StrategyBudgetTimeFormatted { get; set; }
        public decimal StrategyActualTime { get; set; }
        public string StrategyActualTimeFormatted { get; set; }
        public decimal StrategyRemainingTime { get; set; }
        public string StrategyRemainingTimeFormatted { get; set; }
        public decimal DevelopmentBudgetTime { get; set; }
        public string DevelopmentBudgetTimeFormatted { get; set; }
        public decimal DevelopmentActualTime { get; set; }
        public string DevelopmentActualTimeFormatted { get; set; }
        public decimal DevelopmentRemainingTime { get; set; }
        public string DevelopmentRemainingTimeFormatted { get; set; }
    }
}