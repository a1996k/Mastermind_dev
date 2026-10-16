using System;
using System.Collections.Generic;

namespace FifthPress.Models.ViewModels
{
    public class VM_BudgetClient
    {
        public int ClientId { get; set; }
        public string ClientName { get; set; }
        public int ActiveContractCount { get; set; }
        public decimal TotalApprovedHours { get; set; }
        public decimal TotalApprovedBudget { get; set; }
        public decimal TotalHoursUsed { get; set; }
    }

    public class VM_BudgetProject
    {
        public int ContractForecastId { get; set; }
        public string ContractName { get; set; }
        public string ClientName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsRetainer { get; set; }
        public decimal ApprovedHours { get; set; }
        public decimal HoursUsed { get; set; }
        public decimal HoursRemaining { get; set; }
        public decimal ApprovedBudget { get; set; }
        public decimal SpentToDate { get; set; }
        public decimal BudgetRemaining { get; set; }
        public List<VM_BudgetBillingClass> BillingClasses { get; set; }
        public bool HasLinkedProject { get; set; }
    }

    public class VM_BudgetBillingClass
    {
        public string BillingClassName { get; set; }
        public decimal BudgetedHours { get; set; }
        public decimal HourlyRate { get; set; }
        public decimal ApprovedBudget { get; set; }
    }

    public class VM_BudgetMonthDepartment
    {
        public string DepartmentName { get; set; }
        public decimal HoursUsed { get; set; }
        public decimal EstimatedCost { get; set; }
    }

    public class VM_BudgetMonth
    {
        public int Year { get; set; }
        public int MonthNumber { get; set; }
        public string MonthName { get; set; }
        public decimal HoursUsed { get; set; }
        public decimal EstimatedCost { get; set; }
        public decimal CumulativeCost { get; set; }
        public decimal MonthlyBudgetAllocation { get; set; }  // retainer: budget/months; 0 for non-retainer
        public List<VM_BudgetMonthDepartment> DepartmentBreakdown { get; set; }
    }

    public class VM_BudgetDepartmentGroup
    {
        public string DepartmentName { get; set; }
        public decimal ApprovedHours { get; set; }
        public decimal ApprovedBudget { get; set; }
        public decimal BlendedRate { get; set; }
        public decimal HoursUsed { get; set; }      // actual hours from time entries
        public bool IsInContract { get; set; }      // false = dept has hours but no billing class in contract
        public List<VM_BudgetBillingClass> BillingClasses { get; set; }
    }

    public class VM_BudgetProjectDetail : VM_BudgetProject
    {
        public int ContractMonthCount { get; set; }
        public List<VM_BudgetMonth> MonthlyBreakdown { get; set; }
        public List<VM_BudgetDepartmentGroup> DepartmentGroups { get; set; }
    }
}
