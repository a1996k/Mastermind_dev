using FifthPress.Models.ViewModels2.PO25;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace FifthPress
{
    public class CompanyBudgetService25
    {
        private readonly FifthPressEntities _db;

        public CompanyBudgetService25(FifthPressEntities db)
        {
            _db = db;
        }

        #region Budget Management

        /// <summary>
        /// Get all company budgets for a fiscal year
        /// </summary>
        public List<VM_CompanyBudget25> GetCompanyBudgets(int? fiscalYear = null)
        {
            int year = fiscalYear ?? DateTime.Now.Year;

            return _db.PO_CompanyBudgets25
                .Where(b => b.FiscalYear == year)
                .OrderBy(b => b.BudgetName)
                .Select(b => new VM_CompanyBudget25
                {
                    CompanyBudgetId = b.CompanyBudgetId,
                    FiscalYear = b.FiscalYear,
                    BudgetName = b.BudgetName,
                    Description = b.Description,
                    Allocated = b.Allocated,
                    AllowOverages = b.AllowOverages,
                    IsActive = b.IsActive,
                    CreatedDate = b.CreatedDate,
                    CreatedByUserId = b.CreatedByUserId,
                    CreatedByName = b.User.FirstName + " " + b.User.LastName
                })
                .ToList();
        }

        /// <summary>
        /// Get a single company budget with usage details
        /// </summary>
        public VM_CompanyBudget25 GetCompanyBudget(int budgetId)
        {
            var budget = _db.PO_CompanyBudgets25
                .Include(b => b.User)
                .FirstOrDefault(b => b.CompanyBudgetId == budgetId);

            if (budget == null) return null;

            var ytdSpent = _db.PO_CompanyBudgetUsage25
                .Where(u => u.CompanyBudgetId == budgetId)
                .Sum(u => (decimal?)u.Amount) ?? 0;

            return new VM_CompanyBudget25
            {
                CompanyBudgetId = budget.CompanyBudgetId,
                FiscalYear = budget.FiscalYear,
                BudgetName = budget.BudgetName,
                Description = budget.Description,
                Allocated = budget.Allocated,
                YtdSpent = ytdSpent,
                AvailableBalance = budget.Allocated - ytdSpent,
                PercentUsed = budget.Allocated > 0 ? (ytdSpent / budget.Allocated) * 100 : 0,
                AllowOverages = budget.AllowOverages,
                IsActive = budget.IsActive,
                CreatedDate = budget.CreatedDate,
                CreatedByUserId = budget.CreatedByUserId,
                CreatedByName = budget.User?.FirstName + " " + budget.User?.LastName
            };
        }

        /// <summary>
        /// Get all active company budgets for dropdown selection
        /// </summary>
        public List<VM_CompanyBudget25> GetActiveCompanyBudgets()
        {
            int currentYear = DateTime.Now.Year;

            return _db.PO_CompanyBudgets25
                .Where(b => b.IsActive && b.FiscalYear == currentYear)
                .OrderBy(b => b.BudgetName)
                .Select(b => new VM_CompanyBudget25
                {
                    CompanyBudgetId = b.CompanyBudgetId,
                    BudgetName = b.BudgetName,
                    Allocated = b.Allocated
                })
                .ToList();
        }

        /// <summary>
        /// Create a new company budget
        /// </summary>
        public int CreateCompanyBudget(VM_CompanyBudget25 model, int createdByUserId)
        {
            var budget = new PO_CompanyBudgets25
            {
                FiscalYear = model.FiscalYear,
                BudgetName = model.BudgetName,
                Description = model.Description,
                Allocated = model.Allocated,
                AllowOverages = model.AllowOverages,
                IsActive = true,
                CreatedDate = DateTime.Now,
                CreatedByUserId = createdByUserId
            };

            _db.PO_CompanyBudgets25.Add(budget);
            _db.SaveChanges();

            return budget.CompanyBudgetId;
        }

        /// <summary>
        /// Update an existing company budget
        /// </summary>
        public bool UpdateCompanyBudget(VM_CompanyBudget25 model)
        {
            var budget = _db.PO_CompanyBudgets25.Find(model.CompanyBudgetId);
            if (budget == null) return false;

            budget.BudgetName = model.BudgetName;
            budget.Description = model.Description;
            budget.Allocated = model.Allocated;
            budget.AllowOverages = model.AllowOverages;
            budget.IsActive = model.IsActive;

            _db.SaveChanges();
            return true;
        }

        /// <summary>
        /// Delete a company budget (soft delete by setting IsActive = false)
        /// </summary>
        public bool DeactivateCompanyBudget(int budgetId)
        {
            var budget = _db.PO_CompanyBudgets25.Find(budgetId);
            if (budget == null) return false;

            budget.IsActive = false;
            _db.SaveChanges();
            return true;
        }

        #endregion

        #region Budget Validation

        /// <summary>
        /// Check if a company budget has sufficient funds
        /// </summary>
        public CompanyBudgetCheckResult CheckCompanyBudget(int budgetId, decimal amount)
        {
            var budget = _db.PO_CompanyBudgets25.Find(budgetId);
            if (budget == null)
            {
                return new CompanyBudgetCheckResult
                {
                    IsValid = false,
                    Message = "Company budget not found."
                };
            }

            var ytdSpent = _db.PO_CompanyBudgetUsage25
                .Where(u => u.CompanyBudgetId == budgetId)
                .Sum(u => (decimal?)u.Amount) ?? 0;

            var available = budget.Allocated - ytdSpent;
            var willExceed = amount > available;

            return new CompanyBudgetCheckResult
            {
                IsValid = !willExceed || budget.AllowOverages,
                IsWithinBudget = !willExceed,
                BudgetName = budget.BudgetName,
                BudgetRemaining = available,
                POAmount = amount,
                PercentUsed = budget.Allocated > 0 ? ((ytdSpent + amount) / budget.Allocated) * 100 : 0,
                Message = willExceed && !budget.AllowOverages
                    ? $"This purchase would exceed the company budget '{budget.BudgetName}'. Available: {available:C}"
                    : null,
                WarningLevel = GetWarningLevel(ytdSpent + amount, budget.Allocated)
            };
        }

        private string GetWarningLevel(decimal spent, decimal allocated)
        {
            if (allocated <= 0) return "None";

            var percentUsed = (spent / allocated) * 100;

            if (percentUsed >= 100) return "Critical";
            if (percentUsed >= 90) return "Warning";
            if (percentUsed >= 75) return "Caution";
            return "Normal";
        }

        #endregion

        #region Budget Usage Tracking

        /// <summary>
        /// Record budget usage when a PO is approved
        /// </summary>
        public void RecordCompanyBudgetUsage(int budgetId, int lineItemId, decimal amount)
        {
            var usage = new PO_CompanyBudgetUsage25
            {
                CompanyBudgetId = budgetId,
                PO_LineItemId = lineItemId,
                Amount = amount,
                UsageDate = DateTime.Now
            };

            _db.PO_CompanyBudgetUsage25.Add(usage);
            _db.SaveChanges();
        }

        /// <summary>
        /// Remove budget usage when a PO is cancelled/rejected
        /// </summary>
        public void RemoveCompanyBudgetUsage(int lineItemId)
        {
            var usageRecords = _db.PO_CompanyBudgetUsage25
                .Where(u => u.PO_LineItemId == lineItemId)
                .ToList();

            _db.PO_CompanyBudgetUsage25.RemoveRange(usageRecords);
            _db.SaveChanges();
        }

        /// <summary>
        /// Get usage details for a company budget
        /// </summary>
        public List<VM_CompanyBudgetUsageDetail25> GetBudgetUsageDetails(int budgetId)
        {
            return _db.PO_CompanyBudgetUsage25
                .Where(u => u.CompanyBudgetId == budgetId)
                .OrderByDescending(u => u.UsageDate)
                .Select(u => new VM_CompanyBudgetUsageDetail25
                {
                    UsageId = u.UsageId,
                    PONumber = u.PO_LineItems25.PO_Orders25.PO_OrderId,
                    LineItemDescription = u.PO_LineItems25.ItemDescription,
                    Amount = u.Amount,
                    UsageDate = u.UsageDate,
                    VendorName = u.PO_LineItems25.PO_Orders25.Vendor.Title
                })
                .ToList();
        }

        #endregion
    }

    public class CompanyBudgetCheckResult
    {
        public bool IsValid { get; set; }
        public bool IsWithinBudget { get; set; }
        public string BudgetName { get; set; }
        public decimal BudgetRemaining { get; set; }
        public decimal POAmount { get; set; }
        public decimal PercentUsed { get; set; }
        public string Message { get; set; }
        public string WarningLevel { get; set; }
    }
}
