using FifthPress.Models.ViewModels.TournamentBudget;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace FifthPress.Services
{
    /// <summary>
    /// Service for managing tournament budget data
    /// Handles CRUD operations for budget line items and settings
    /// </summary>
    public class TournamentBudgetService
    {
        private readonly FifthPressEntities _db;

        public TournamentBudgetService(FifthPressEntities db)
        {
            _db = db;
        }

        #region Budget Loading

        /// <summary>
        /// Gets complete budget data for a client/tournament
        /// </summary>
        public VM_ClientBudget GetClientBudget(int clientId)
        {
            var client = _db.Clients.Find(clientId);
            if (client == null)
                return null;

            var settings = GetOrCreateSettings(clientId);

            var budget = new VM_ClientBudget
            {
                ClientId = clientId,
                ClientName = client.Title,
                TournamentLocation = settings.TournamentLocation,
                TaxRate = settings.TaxRate,
                Notes = settings.Notes
            };

            // Load all line items for this client
            var lineItems = _db.TournamentBudgetLineItems
                .Include(li => li.TournamentBudgetCategory)
                .Where(li => li.ClientId == clientId && !li.IsDeleted)
                .ToList();

            // Group by category and populate collections
            budget.HardwareRental = MapLineItems(lineItems, "Hardware Rental");
            budget.OffsiteLabor = MapLineItems(lineItems, "Offsite Labor");
            budget.TravelExpenses = MapLineItems(lineItems, "Travel Expenses");
            budget.Accommodation = MapLineItems(lineItems, "Hotel/Accommodation");
            budget.VehicleRental = MapLineItems(lineItems, "Vehicle Rental");
            budget.Freight = MapLineItems(lineItems, "Freight");
            budget.ReimbursablesHardware = MapLineItems(lineItems, "Reimbursables - Hardware");
            budget.ManagementFee = MapLineItems(lineItems, "Management Fee");
            budget.StorageFee = MapLineItems(lineItems, "Storage Fee");
            budget.DesignFee = MapLineItems(lineItems, "Design Fee");
            budget.Miscellaneous = MapLineItems(lineItems, "Miscellaneous");

            return budget;
        }

        /// <summary>
        /// Gets all budget categories
        /// </summary>
        public List<TournamentBudgetCategory> GetCategories()
        {
            return _db.TournamentBudgetCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .ToList();
        }

        /// <summary>
        /// Gets category by name
        /// </summary>
        public TournamentBudgetCategory GetCategoryByName(string categoryName)
        {
            return _db.TournamentBudgetCategories
                .FirstOrDefault(c => c.CategoryName == categoryName && c.IsActive);
        }

        #endregion

        #region Budget Settings

        /// <summary>
        /// Gets or creates budget settings for a client
        /// </summary>
        public TournamentBudgetSetting GetOrCreateSettings(int clientId)
        {
            var settings = _db.TournamentBudgetSettings.FirstOrDefault(s => s.ClientId == clientId);

            if (settings == null)
            {
                settings = new TournamentBudgetSetting
                {
                    ClientId = clientId,
                    TaxRate = 7.00M, // Default 7%
                    OnsitePrintingRateSqFt = 10.00M, // Default $10/sqft (reserved for future)
                    CreatedDate = DateTime.Now
                };

                _db.TournamentBudgetSettings.Add(settings);
                _db.SaveChanges();
            }

            return settings;
        }

        /// <summary>
        /// Updates budget settings for a client
        /// </summary>
        public bool SaveBudgetSettings(int clientId, string tournamentLocation, decimal taxRate, string notes)
        {
            try
            {
                var settings = GetOrCreateSettings(clientId);

                settings.TournamentLocation = tournamentLocation;
                settings.TaxRate = taxRate;
                settings.Notes = notes;
                settings.ModifiedDate = DateTime.Now;

                _db.Entry(settings).State = EntityState.Modified;
                _db.SaveChanges();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving budget settings: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Line Item CRUD

        /// <summary>
        /// Saves a budget line item (create or update)
        /// </summary>
        public bool SaveLineItem(VM_BudgetLineItem item, int userId)
        {
            try
            {
                TournamentBudgetLineItem entity;

                if (item.LineItemId > 0)
                {
                    // Update existing
                    entity = _db.TournamentBudgetLineItems.Find(item.LineItemId);
                    if (entity == null)
                        return false;

                    entity.ModifiedDate = DateTime.Now;
                    entity.ModifiedByUserId = userId;
                }
                else
                {
                    // Create new
                    entity = new TournamentBudgetLineItem
                    {
                        ClientId = item.ClientId,
                        CreatedDate = DateTime.Now,
                        CreatedByUserId = userId
                    };
                    _db.TournamentBudgetLineItems.Add(entity);
                }

                // Map properties
                entity.CategoryId = item.CategoryId;
                entity.ItemDescription = item.ItemDescription;
                entity.Quantity = item.Quantity;
                entity.QuantityDays = item.QuantityDays;
                entity.UnitPrice = item.UnitPrice;
                entity.Width = item.Width;
                entity.Height = item.Height;
                entity.SqFt = item.SqFt;
                entity.StartDate = item.StartDate;
                entity.EndDate = item.EndDate;
                entity.EstimatedAmount = item.EstimatedAmount;
                entity.ActualAmount = item.ActualAmount;
                entity.IsTaxable = item.IsTaxable;
                entity.Notes = item.Notes;

                _db.SaveChanges();

                // Update the view model with the new ID
                item.LineItemId = entity.LineItemId;

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving line item: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Deletes a budget line item (soft delete)
        /// </summary>
        public bool DeleteLineItem(int lineItemId)
        {
            try
            {
                var entity = _db.TournamentBudgetLineItems.Find(lineItemId);
                if (entity == null)
                    return false;

                entity.IsDeleted = true;
                entity.ModifiedDate = DateTime.Now;

                _db.Entry(entity).State = EntityState.Modified;
                _db.SaveChanges();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting line item: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Permanently deletes a line item (use with caution)
        /// </summary>
        public bool HardDeleteLineItem(int lineItemId)
        {
            try
            {
                var entity = _db.TournamentBudgetLineItems.Find(lineItemId);
                if (entity == null)
                    return false;

                _db.TournamentBudgetLineItems.Remove(entity);
                _db.SaveChanges();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error hard deleting line item: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Bulk save multiple line items
        /// </summary>
        public bool SaveLineItems(List<VM_BudgetLineItem> items, int userId)
        {
            try
            {
                foreach (var item in items)
                {
                    if (!SaveLineItem(item, userId))
                        return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving line items: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Budget Analysis

        /// <summary>
        /// Gets budget summary for a client
        /// </summary>
        public BudgetSummary GetBudgetSummary(int clientId)
        {
            var budget = GetClientBudget(clientId);
            if (budget == null)
                return null;

            return new BudgetSummary
            {
                ClientId = clientId,
                ClientName = budget.ClientName,
                TotalEstimated = budget.TotalEstimated,
                TotalActual = budget.TotalActual,
                TotalVariance = budget.TotalVariance,
                EstimatedTax = budget.EstimatedTax,
                ActualTax = budget.ActualTax,
                GrandTotalEstimated = budget.GrandTotalEstimated,
                GrandTotalActual = budget.GrandTotalActual,
                TaxRate = budget.TaxRate
            };
        }

        /// <summary>
        /// Gets variance by category for a client
        /// </summary>
        public List<CategoryVariance> GetCategoryVariances(int clientId)
        {
            var lineItems = _db.TournamentBudgetLineItems
                .Include(li => li.TournamentBudgetCategory)
                .Where(li => li.ClientId == clientId && !li.IsDeleted)
                .ToList();

            var variances = lineItems
                .GroupBy(li => li.TournamentBudgetCategory.CategoryName)
                .Select(g => new CategoryVariance
                {
                    CategoryName = g.Key,
                    TotalEstimated = g.Sum(li => li.EstimatedAmount),
                    TotalActual = g.Sum(li => li.ActualAmount ?? 0),
                    Variance = g.Sum(li => (li.ActualAmount ?? 0) - li.EstimatedAmount),
                    ItemCount = g.Count()
                })
                .OrderByDescending(cv => Math.Abs(cv.Variance))
                .ToList();

            return variances;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Maps database line items to view models for a specific category
        /// </summary>
        private List<VM_BudgetLineItem> MapLineItems(List<TournamentBudgetLineItem> allItems, string categoryName)
        {
            return allItems
                .Where(li => li.TournamentBudgetCategory.CategoryName == categoryName)
                .Select(li => new VM_BudgetLineItem
                {
                    LineItemId = li.LineItemId,
                    ClientId = li.ClientId,
                    CategoryId = li.CategoryId,
                    CategoryName = li.TournamentBudgetCategory.CategoryName,
                    ItemDescription = li.ItemDescription,
                    Quantity = li.Quantity,
                    QuantityDays = li.QuantityDays,
                    UnitPrice = li.UnitPrice,
                    Width = li.Width,
                    Height = li.Height,
                    SqFt = li.SqFt,
                    StartDate = li.StartDate,
                    EndDate = li.EndDate,
                    EstimatedAmount = li.EstimatedAmount,
                    ActualAmount = li.ActualAmount,
                    IsTaxable = li.IsTaxable,
                    Notes = li.Notes
                })
                .OrderBy(vm => vm.ItemDescription)
                .ToList();
        }

        #endregion
    }

    #region Supporting Classes

    /// <summary>
    /// Budget summary for reporting
    /// </summary>
    public class BudgetSummary
    {
        public int ClientId { get; set; }
        public string ClientName { get; set; }
        public decimal TotalEstimated { get; set; }
        public decimal TotalActual { get; set; }
        public decimal TotalVariance { get; set; }
        public decimal EstimatedTax { get; set; }
        public decimal ActualTax { get; set; }
        public decimal GrandTotalEstimated { get; set; }
        public decimal GrandTotalActual { get; set; }
        public decimal TaxRate { get; set; }
    }

    /// <summary>
    /// Category-level variance analysis
    /// </summary>
    public class CategoryVariance
    {
        public string CategoryName { get; set; }
        public decimal TotalEstimated { get; set; }
        public decimal TotalActual { get; set; }
        public decimal Variance { get; set; }
        public int ItemCount { get; set; }

        public string VarianceDisplay
        {
            get
            {
                var symbol = Variance >= 0 ? "+" : "";
                return $"{symbol}{Variance:C}";
            }
        }

        public string VarianceClass => Variance >= 0 ? "text-success" : "text-danger";
    }

    #endregion
}
