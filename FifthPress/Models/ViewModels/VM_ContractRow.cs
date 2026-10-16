using System;
using System.Linq;
using FifthPress.Controllers;

namespace FifthPress.Models.ViewModels
{
    public class VM_ContractRow
    {
        public int Id { get; set; }
        public string ContractName { get; set; }
        public string ClientName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsRetainer { get; set; }
        public int BillingClassCount { get; set; }
        public decimal TotalBudgetedHours { get; set; }
        public bool IsExternal { get; set; }
        public bool IncludeInCharts { get; set; }
        public string BillingClassSummary { get; set; }

        public static VM_ContractRow FromContractForecast(FifthPress.ContractForecast cf)
        {
            return new VM_ContractRow
            {
                Id = cf.ContractForecastID,
                ContractName = cf.ContractName,
                ClientName = cf.ClientName,
                StartDate = cf.StartDate,
                EndDate = cf.EndDate,
                IsRetainer = cf.IsRetainer,
                BillingClassCount = cf.ContractForecastDetails != null ? cf.ContractForecastDetails.Count : 0,
                TotalBudgetedHours = cf.ContractForecastDetails != null
                    ? cf.ContractForecastDetails.Sum(d => d.BudgetedHours)
                    : 0,
                IsExternal = false,
                IncludeInCharts = cf.IncludeInCharts,
                BillingClassSummary = cf.ContractForecastDetails != null && cf.ContractForecastDetails.Any()
                    ? string.Join("\n", cf.ContractForecastDetails.Select(d =>
                        (d.BillingClass != null ? d.BillingClass.Title : "Unknown")
                        + (!string.IsNullOrEmpty(d.BillingSubclass) ? " (" + d.BillingSubclass + ")" : "")
                        + ": " + d.BudgetedHours.ToString("N1") + " hrs"))
                    : null
            };
        }

        public static VM_ContractRow FromExternalProject(ExternalProjectContractInfo ep)
        {
            return new VM_ContractRow
            {
                Id = ep.Id,
                ContractName = ep.ProjectName,
                ClientName = ep.ClientName,
                StartDate = ep.ProjectStartDate,
                EndDate = ep.ProjectEndDate,
                IsRetainer = false,
                BillingClassCount = ep.BillingClassCount,
                TotalBudgetedHours = ep.TotalBudgetedHours,
                IsExternal = true,
                IncludeInCharts = true,
                BillingClassSummary = !string.IsNullOrEmpty(ep.BillingClassSummary) ? ep.BillingClassSummary : null
            };
        }
    }
}
