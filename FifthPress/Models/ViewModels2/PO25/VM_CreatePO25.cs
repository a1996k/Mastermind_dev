using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_CreatePO25
    {
        public int PO_OrderId { get; set; }

        [Required]
        [Display(Name = "Vendor")]
        public int VendorId { get; set; }

        [Display(Name = "Department")]
        public int? DepartmentId { get; set; }

        [Required]
        public string Status { get; set; } = "Draft";

        [Display(Name = "Save as Draft")]
        public bool IsDraft { get; set; } = true;

        public List<VM_LineItem25> LineItems { get; set; } = new List<VM_LineItem25>();

        // Dropdown lists for form
        public IEnumerable<SelectListItem> Vendors { get; set; }

        public IEnumerable<SelectListItem> Departments { get; set; }

        // Freight and Tax fields
        [Display(Name = "Freight Amount")]
        public decimal? FreightAmount { get; set; }

        [Display(Name = "Tax Amount")]
        public decimal? TaxAmount { get; set; }

        [Display(Name = "Tax Rate (%)")]
        public decimal? TaxRate { get; set; }

        [Display(Name = "Tax is Percentage")]
        public bool IsTaxPercentage { get; set; } = true;

        // Summary properties
        public decimal SubtotalAmount => LineItems?.Sum(li => li.LineTotal) ?? 0;

        public decimal TaxAmountCalculated => IsTaxPercentage && TaxRate.HasValue
            ? SubtotalAmount * (TaxRate.Value / 100)
            : (TaxAmount ?? 0);

        public decimal TotalAmount => SubtotalAmount + (FreightAmount ?? 0) + TaxAmountCalculated;

        public decimal BillableAmount => LineItems?.Where(li => li.Billable).Sum(li => li.LineTotal) ?? 0;
        public decimal NonBillableAmount => LineItems?.Where(li => !li.Billable).Sum(li => li.LineTotal) ?? 0;

        public bool OverrideRequested { get; set; }

        // Company-Wide Budget Properties
        [Display(Name = "Company-Wide Purchase")]
        public bool IsCompanyWide { get; set; }

        [Display(Name = "Company Budget")]
        public int? CompanyBudgetId { get; set; }

        public IEnumerable<SelectListItem> CompanyBudgets { get; set; }

        // Department Split Properties
        public bool UsesDepartmentSplits { get; set; }
        public string SplitType { get; set; } // "None", "OrderLevel", or "LineItemLevel"
        public List<VM_DepartmentSplit25> OrderSplits { get; set; } = new List<VM_DepartmentSplit25>();

        public void InitializeDefaults()
        {
            // Add a default line item if none exist
            if (LineItems == null || !LineItems.Any())
            {
                LineItems = new List<VM_LineItem25>
                {
                    new VM_LineItem25
                    {
                        Quantity = 1,
                        Taxable = true,
                        Billable = true,
                        UnitType = "Units" // Add this default
                    }
                };
            }
        }

        public PO_Orders25 ToPOEntity(int userId)
        {
            DateTime createdTime = DateTime.UtcNow;

            var po = new PO_Orders25
            {
                PO_OrderId = this.PO_OrderId,
                VendorId = this.VendorId,
                SubmittedByUserId = userId,
                DepartmentId = this.IsCompanyWide ? null : this.DepartmentId, // null for company-wide
                Status = this.IsDraft ? "Draft" : "Pending",
                DateSubmitted = this.IsDraft ? (DateTime?)null : DateTime.Now,
                IsDraft = this.IsDraft,
                PO_LineItems25 = new List<PO_LineItems25>(),
                CreatedDate = createdTime,
                DateCreated = createdTime,
                FreightAmount = this.FreightAmount,
                TaxAmount = this.TaxAmount,
                TaxRate = this.TaxRate,
                IsTaxPercentage = this.IsTaxPercentage,
                UsesDepartmentSplits = this.UsesDepartmentSplits,
                SplitType = this.SplitType,
                IsCompanyWide = this.IsCompanyWide,
                CompanyBudgetId = this.IsCompanyWide ? this.CompanyBudgetId : null
            };

            // Add line items
            foreach (var lineItem in this.LineItems)
            {
                po.PO_LineItems25.Add(new PO_LineItems25
                {
                    ItemDescription = lineItem.ItemDescription,
                    VendorLink = lineItem.VendorLink,
                    Cost = lineItem.Cost,
                    Quantity = lineItem.Quantity,
                    Taxable = lineItem.Taxable,
                    Billable = lineItem.Billable,
                    DateNeeded = lineItem.DateNeeded,
                    DateExpected = lineItem.DateExpected,
                    InternalComments = lineItem.InternalComments,
                    FileAttachmentPath = lineItem.FileAttachmentPath,
                    InventoryTracked = lineItem.InventoryTracked,
                    MaterialId = lineItem.MaterialId,
                    UnitType = lineItem.UnitType ?? "Units",
                    JobTicketNumber = lineItem.JobTicketNumber,
                    JobId = lineItem.JobId,
                    CreatedDate = createdTime,
                    // Bulk item fields
                    IsBulkItem = lineItem.IsBulkItem,
                    BulkContainerType = lineItem.BulkContainerType,
                    BulkUnitQuantity = lineItem.BulkUnitQuantity,
                    BulkUnitType = lineItem.BulkUnitType,
                    CalculatedUnitCost = lineItem.CalculatedUnitCost,
                    // Department for line-item level splits
                    DepartmentId = lineItem.DepartmentId
                });
            }

            return po;
        }

        public static VM_CreatePO25 FromPOEntity(PO_Orders25 po)
        {
            var model = new VM_CreatePO25
            {
                PO_OrderId = po.PO_OrderId,
                VendorId = po.VendorId ?? -1,
                DepartmentId = po.DepartmentId,
                Status = po.Status,
                IsDraft = po.IsDraft,
                FreightAmount = po.FreightAmount,
                TaxAmount = po.TaxAmount,
                TaxRate = po.TaxRate,
                IsTaxPercentage = po.IsTaxPercentage,
                UsesDepartmentSplits = po.UsesDepartmentSplits,
                SplitType = po.SplitType,
                IsCompanyWide = po.IsCompanyWide,
                CompanyBudgetId = po.CompanyBudgetId
            };

            // Convert line items
            if (po.PO_LineItems25 != null)
            {
                foreach (var lineItem in po.PO_LineItems25)
                {
                    model.LineItems.Add(new VM_LineItem25
                    {
                        PO_LineItemId = lineItem.PO_LineItemId,
                        ItemDescription = lineItem.ItemDescription,
                        VendorLink = lineItem.VendorLink,
                        Cost = lineItem.Cost,
                        Quantity = lineItem.Quantity,
                        Taxable = lineItem.Taxable,
                        Billable = lineItem.Billable,
                        DateNeeded = lineItem.DateNeeded,
                        DateExpected = lineItem.DateExpected,
                        InternalComments = lineItem.InternalComments,
                        FileAttachmentPath = lineItem.FileAttachmentPath,
                        InventoryTracked = lineItem.InventoryTracked,
                        MaterialId = lineItem.MaterialId,
                        UnitType = lineItem.UnitType ?? "Units",
                        JobTicketNumber = lineItem.JobTicketNumber,
                        JobId = lineItem.JobId,
                        // Bulk item fields
                        IsBulkItem = lineItem.IsBulkItem,
                        BulkContainerType = lineItem.BulkContainerType,
                        BulkUnitQuantity = lineItem.BulkUnitQuantity,
                        BulkUnitType = lineItem.BulkUnitType,
                        CalculatedUnitCost = lineItem.CalculatedUnitCost,
                        // Department for line-item level splits
                        DepartmentId = lineItem.DepartmentId,
                        DepartmentName = lineItem.Department?.Title
                    });
                }
            }

            model.InitializeDefaults();
            return model;
        }
    }
}