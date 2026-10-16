using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace FifthPress.Models.ViewModels.TournamentBudget
{
    /// <summary>
    /// View model for managing all budget data for a tournament/client
    /// Combines manual line items with automated data from Jobs, Quotes, and Tournament schedules
    /// </summary>
    public class VM_ClientBudget
    {
        public int ClientId { get; set; }

        [Display(Name = "Client/Event Name")]
        public string ClientName { get; set; }

        [Display(Name = "Tournament Location")]
        [StringLength(200)]
        public string TournamentLocation { get; set; }

        [Required]
        [Display(Name = "Tax Rate (%)")]
        [Range(0, 100)]
        public decimal TaxRate { get; set; } = 7.00M;

        [Display(Name = "Notes")]
        [DataType(DataType.MultilineText)]
        public string Notes { get; set; }

        // Manual budget categories (user-entered)
        public List<VM_BudgetLineItem> HardwareRental { get; set; } = new List<VM_BudgetLineItem>();
        public List<VM_BudgetLineItem> OffsiteLabor { get; set; } = new List<VM_BudgetLineItem>();
        public List<VM_BudgetLineItem> TravelExpenses { get; set; } = new List<VM_BudgetLineItem>();
        public List<VM_BudgetLineItem> Accommodation { get; set; } = new List<VM_BudgetLineItem>();
        public List<VM_BudgetLineItem> VehicleRental { get; set; } = new List<VM_BudgetLineItem>();
        public List<VM_BudgetLineItem> Freight { get; set; } = new List<VM_BudgetLineItem>();
        public List<VM_BudgetLineItem> ReimbursablesHardware { get; set; } = new List<VM_BudgetLineItem>();
        public List<VM_BudgetLineItem> ManagementFee { get; set; } = new List<VM_BudgetLineItem>();
        public List<VM_BudgetLineItem> StorageFee { get; set; } = new List<VM_BudgetLineItem>();
        public List<VM_BudgetLineItem> DesignFee { get; set; } = new List<VM_BudgetLineItem>();
        public List<VM_BudgetLineItem> Miscellaneous { get; set; } = new List<VM_BudgetLineItem>();

        // Summary calculations
        public decimal TotalEstimated
        {
            get
            {
                return HardwareRental.Sum(x => x.EstimatedAmount)
                    + OffsiteLabor.Sum(x => x.EstimatedAmount)
                    + TravelExpenses.Sum(x => x.EstimatedAmount)
                    + Accommodation.Sum(x => x.EstimatedAmount)
                    + VehicleRental.Sum(x => x.EstimatedAmount)
                    + Freight.Sum(x => x.EstimatedAmount)
                    + ReimbursablesHardware.Sum(x => x.EstimatedAmount)
                    + ManagementFee.Sum(x => x.EstimatedAmount)
                    + StorageFee.Sum(x => x.EstimatedAmount)
                    + DesignFee.Sum(x => x.EstimatedAmount)
                    + Miscellaneous.Sum(x => x.EstimatedAmount);
            }
        }

        public decimal TotalActual
        {
            get
            {
                return HardwareRental.Sum(x => x.ActualAmount ?? 0)
                    + OffsiteLabor.Sum(x => x.ActualAmount ?? 0)
                    + TravelExpenses.Sum(x => x.ActualAmount ?? 0)
                    + Accommodation.Sum(x => x.ActualAmount ?? 0)
                    + VehicleRental.Sum(x => x.ActualAmount ?? 0)
                    + Freight.Sum(x => x.ActualAmount ?? 0)
                    + ReimbursablesHardware.Sum(x => x.ActualAmount ?? 0)
                    + ManagementFee.Sum(x => x.ActualAmount ?? 0)
                    + StorageFee.Sum(x => x.ActualAmount ?? 0)
                    + DesignFee.Sum(x => x.ActualAmount ?? 0)
                    + Miscellaneous.Sum(x => x.ActualAmount ?? 0);
            }
        }

        public decimal TotalTaxableEstimated
        {
            get
            {
                var allItems = GetAllLineItems();
                return allItems.Where(x => x.IsTaxable).Sum(x => x.EstimatedAmount);
            }
        }

        public decimal TotalTaxableActual
        {
            get
            {
                var allItems = GetAllLineItems();
                return allItems.Where(x => x.IsTaxable).Sum(x => x.ActualAmount ?? 0);
            }
        }

        public decimal EstimatedTax => TotalTaxableEstimated * (TaxRate / 100);
        public decimal ActualTax => TotalTaxableActual * (TaxRate / 100);

        public decimal GrandTotalEstimated => TotalEstimated + EstimatedTax;
        public decimal GrandTotalActual => TotalActual + ActualTax;

        public decimal TotalVariance => TotalActual - TotalEstimated;

        private List<VM_BudgetLineItem> GetAllLineItems()
        {
            var allItems = new List<VM_BudgetLineItem>();
            allItems.AddRange(HardwareRental);
            allItems.AddRange(OffsiteLabor);
            allItems.AddRange(TravelExpenses);
            allItems.AddRange(Accommodation);
            allItems.AddRange(VehicleRental);
            allItems.AddRange(Freight);
            allItems.AddRange(ReimbursablesHardware);
            allItems.AddRange(ManagementFee);
            allItems.AddRange(StorageFee);
            allItems.AddRange(DesignFee);
            allItems.AddRange(Miscellaneous);
            return allItems;
        }
    }
}
