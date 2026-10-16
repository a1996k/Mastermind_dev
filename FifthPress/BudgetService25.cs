using FifthPress.Models.ViewModels2.PO25;
using FifthPress;
using System.Collections.Generic;
using System;
using System.Data.Entity;
using System.Linq;

public class BudgetCheckResult
{
    public bool IsWithinBudget { get; set; }
    public decimal BudgetRemaining { get; set; }
    public decimal POAmount { get; set; }
    public string DepartmentName { get; set; }

    public string Message => IsWithinBudget
        ? "Within budget."
        : $"Over budget by {POAmount - BudgetRemaining:C} for {DepartmentName}.";
}

public class BudgetService25
{
    private readonly FifthPressEntities _db;

    public BudgetService25(FifthPressEntities db)
    {
        _db = db;
    }

    public BudgetCheckResult CheckBudget(int departmentId, decimal poTotal)
    {
        var currentYear = DateTime.Now.Year;

        var budget = _db.PO_DepartmentBudgets25
            .FirstOrDefault(b => b.DepartmentId == departmentId && b.FiscalYear == currentYear);

        if (budget == null)
        {
            return new BudgetCheckResult
            {
                IsWithinBudget = false,
                POAmount = poTotal,
                BudgetRemaining = 0,
                DepartmentName = "Unknown (No Budget Defined)"
            };
        }

        var used = (
            from order in _db.PO_Orders25
            join line in _db.PO_LineItems25 on order.PO_OrderId equals line.PO_OrderId
            where order.DepartmentId == departmentId
                && order.Status != "Rejected"
                && !line.Billable  // Exclude billable (pass-through) expenses from budget
            select (decimal?)(line.Cost * line.Quantity)
        ).Sum() ?? 0;

        var remaining = budget.Allocated - used;

        // Get actual department name
        var departmentName = _db.Departments
            .Where(d => d.Id == departmentId)
            .Select(d => d.Title)
            .FirstOrDefault() ?? $"Department ID {departmentId}";

        return new BudgetCheckResult
        {
            IsWithinBudget = poTotal <= remaining,
            BudgetRemaining = remaining,
            POAmount = poTotal,
            DepartmentName = departmentName
        };
    }

    public List<VM_DepartmentBudget25> GetAllDepartmentBudgets()
    {
        return _db.PO_DepartmentBudgets25
            .Select(b => new
            {
                b.DepartmentId,
                DepartmentName = b.Department.Title,
                AnnualBudget = b.Allocated,
                YtdSpent = _db.PO_DepartmentBudgetUsage25
                    .Where(u => u.PO_LineItems25.PO_Orders25.DepartmentId == b.DepartmentId && !u.IsBillable)
                    .Sum(u => (decimal?)u.Amount) ?? 0
            })
            .ToList()
            .Select(x => new VM_DepartmentBudget25
            {
                DepartmentId = x.DepartmentId,
                DepartmentName = x.DepartmentName,
                AnnualBudget = x.AnnualBudget,
                YtdSpent = x.YtdSpent,
                AvailableBalance = x.AnnualBudget - x.YtdSpent,
                PercentUsed = x.AnnualBudget > 0 ? (x.YtdSpent / x.AnnualBudget) * 100 : 0,
                IsOverBudget = x.YtdSpent > x.AnnualBudget
            })
            .ToList();
    }

    public VM_DepartmentBudget25 GetDepartmentBudget(int departmentId)
    {
        var budget = _db.PO_DepartmentBudgets25.FirstOrDefault(b => b.DepartmentId == departmentId);
        if (budget == null) return null;

        var ytdSpent = _db.PO_DepartmentBudgetUsage25
            .Where(u => u.PO_LineItems25.PO_Orders25.DepartmentId == departmentId && !u.IsBillable)
            .Sum(u => (decimal?)u.Amount) ?? 0;

        return new VM_DepartmentBudget25
        {
            DepartmentId = budget.DepartmentId,
            DepartmentName = budget.Department?.Title,
            AnnualBudget = budget.Allocated,
            YtdSpent = ytdSpent,
            AvailableBalance = budget.Allocated - ytdSpent
        };
    }

    public bool UpdateDepartmentBudget(int departmentId, decimal newBudget)
    {
        try
        {
            var budget = _db.PO_DepartmentBudgets25.FirstOrDefault(b => b.DepartmentId == departmentId);

            if (budget == null)
            {
                // Create new budget record
                budget = new PO_DepartmentBudgets25
                {
                    DepartmentId = departmentId,
                    Allocated = newBudget, // Using 'Allocated' field
                    FiscalYear = DateTime.Now.Year,
                    AllowOverages = false
                };
                _db.PO_DepartmentBudgets25.Add(budget);
            }
            else
            {
                budget.Allocated = newBudget;
            }

            _db.SaveChanges();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public bool SetOveragePermission(int departmentId, bool allowOverages)
    {
        try
        {
            var budget = _db.PO_DepartmentBudgets25.FirstOrDefault(b => b.DepartmentId == departmentId);
            if (budget == null) return false;

            budget.AllowOverages = allowOverages;
            _db.SaveChanges();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public List<VM_BudgetAlert25> GetBudgetAlerts()
    {
        var alerts = new List<VM_BudgetAlert25>();
        var budgets = GetAllDepartmentBudgets();

        foreach (var budget in budgets)
        {
            if (budget.IsOverBudget)
            {
                alerts.Add(new VM_BudgetAlert25
                {
                    DepartmentId = budget.DepartmentId,
                    DepartmentName = budget.DepartmentName,
                    AlertType = "OverBudget",
                    Message = $"{budget.DepartmentName} is over budget by ${Math.Abs(budget.AvailableBalance):F2}",
                    AlertLevel = "Critical"
                });
            }
            else if (budget.PercentUsed >= 90)
            {
                alerts.Add(new VM_BudgetAlert25
                {
                    DepartmentId = budget.DepartmentId,
                    DepartmentName = budget.DepartmentName,
                    AlertType = "NearLimit",
                    Message = $"{budget.DepartmentName} has used {budget.PercentUsed:F1}% of budget",
                    AlertLevel = "Warning"
                });
            }
            else if (budget.PercentUsed >= 75)
            {
                alerts.Add(new VM_BudgetAlert25
                {
                    DepartmentId = budget.DepartmentId,
                    DepartmentName = budget.DepartmentName,
                    AlertType = "Approaching",
                    Message = $"{budget.DepartmentName} has used {budget.PercentUsed:F1}% of budget",
                    AlertLevel = "Info"
                });
            }
        }

        return alerts.OrderBy(a => a.AlertLevel == "Critical" ? 1 :
                                  a.AlertLevel == "Warning" ? 2 : 3)
                     .ToList();
    }

    public VM_BudgetSummary25 GetBudgetSummary()
    {
        var budgets = GetAllDepartmentBudgets();

        return new VM_BudgetSummary25
        {
            TotalBudget = budgets.Sum(b => b.AnnualBudget),
            TotalSpent = budgets.Sum(b => b.YtdSpent),
            TotalRemaining = budgets.Sum(b => b.AvailableBalance),
            DepartmentsOverBudget = budgets.Count(b => b.IsOverBudget),
            DepartmentsNearLimit = budgets.Count(b => !b.IsOverBudget && b.PercentUsed >= 90),
            AveragePercentUsed = budgets.Any() ? budgets.Average(b => b.PercentUsed) : 0
        };
    }

    public List<VM_BudgetUsageDetail25> GetBudgetUsageHistory(int departmentId, int months = 12)
    {
        var startDate = DateTime.Now.AddMonths(-months);

        return _db.PO_DepartmentBudgetUsage25
            .Where(u => u.PO_LineItems25.PO_Orders25.DepartmentId == departmentId &&
                       u.UsageDate >= startDate)
            .Select(u => new VM_BudgetUsageDetail25
            {
                UsageDate = u.UsageDate.Value,
                Amount = u.Amount,
                PO_OrderId = u.PO_LineItems25.PO_OrderId,
                Description = u.PO_LineItems25.ItemDescription,
                IsBillable = u.IsBillable
            })
            .OrderByDescending(u => u.UsageDate)
            .ToList();
    }

    public bool ValidatePOAgainstBudget(int poId, int departmentId)
    {
        var po = _db.PO_Orders25.Find(poId);
        if (po == null) return false;

        var nonBillableAmount = po.PO_LineItems25
            .Where(li => !li.Billable)  // Only count non-billable items against budget
            .Sum(li => li.Cost * li.Quantity);

        var budget = GetDepartmentBudget(departmentId);
        if (budget == null) return true; // No budget constraint

        var allowOverages = _db.PO_DepartmentBudgets25
            .Where(b => b.DepartmentId == departmentId)
            .Select(b => b.AllowOverages)
            .FirstOrDefault();

        return allowOverages || nonBillableAmount <= budget.AvailableBalance;
    }
}