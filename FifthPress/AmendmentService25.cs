using System;
using System.Collections.Generic;
using System.Linq;
using FifthPress.Models.ViewModels2.PO25;

namespace FifthPress
{
    public class AmendmentService25
    {
        private readonly FifthPressEntities _db;
        private readonly PORoleService25 _roleService;
        private readonly ConfigurationService25 _configService;

        public AmendmentService25(FifthPressEntities db)
        {
            _db = db;
            _roleService = new PORoleService25(db);
            _configService = new ConfigurationService25(db);
        }

        /// <summary>
        /// Check if amendments are allowed for this PO
        /// </summary>
        public bool CanAmendPO(int poId, int userId)
        {
            // Check if amendments are enabled globally
            if (!_configService.GetConfigValue("AllowPostApprovalTaxEdit", true))
                return false;

            // Check if user has permission to amend
            if (!_roleService.CanAmendApprovedPO(userId))
                return false;

            // Check if PO exists and is in amendable status
            var po = _db.PO_Orders25.Find(poId);
            if (po == null)
                return false;

            // Only approved POs can be amended for tax/freight
            return po.Status == "Approved";
        }

        /// <summary>
        /// Amend the freight amount on an approved PO
        /// </summary>
        public bool AmendFreight(int poId, decimal? newAmount, int userId, string reason)
        {
            if (!CanAmendPO(poId, userId))
                return false;

            var po = _db.PO_Orders25.Find(poId);
            if (po == null)
                return false;

            // Check if reason is required
            if (_configService.GetConfigValue("RequireAmendmentReason", true) && string.IsNullOrWhiteSpace(reason))
                return false;

            // Create amendment record
            var amendment = new PO_OrderAmendments25
            {
                PO_OrderId = poId,
                AmendmentType = "FreightChange",
                PreviousValue = po.FreightAmount,
                NewValue = newAmount,
                AmendedByUserId = userId,
                AmendedDate = DateTime.Now,
                Reason = reason
            };

            _db.PO_OrderAmendments25.Add(amendment);

            // Update the PO
            po.FreightAmount = newAmount;
            po.ModifiedDate = DateTime.Now;

            _db.SaveChanges();

            PurchaseOrderLogger.Log($"[AmendFreight] PO {poId} freight amended from {amendment.PreviousValue} to {newAmount} by user {userId}");
            return true;
        }

        /// <summary>
        /// Amend the tax on an approved PO (can be fixed amount or percentage)
        /// </summary>
        public bool AmendTax(int poId, decimal? newTaxAmount, decimal? newTaxRate, bool isTaxPercentage, int userId, string reason)
        {
            if (!CanAmendPO(poId, userId))
                return false;

            var po = _db.PO_Orders25.Find(poId);
            if (po == null)
                return false;

            // Check if reason is required
            if (_configService.GetConfigValue("RequireAmendmentReason", true) && string.IsNullOrWhiteSpace(reason))
                return false;

            // Determine what changed and create appropriate amendment records
            if (isTaxPercentage && newTaxRate != po.TaxRate)
            {
                var amendment = new PO_OrderAmendments25
                {
                    PO_OrderId = poId,
                    AmendmentType = "TaxRateChange",
                    PreviousValue = po.TaxRate,
                    NewValue = newTaxRate,
                    AmendedByUserId = userId,
                    AmendedDate = DateTime.Now,
                    Reason = reason
                };
                _db.PO_OrderAmendments25.Add(amendment);

                po.TaxRate = newTaxRate;
                po.IsTaxPercentage = true;
                po.TaxAmount = null; // Clear fixed amount when using percentage

                PurchaseOrderLogger.Log($"[AmendTax] PO {poId} tax rate amended from {amendment.PreviousValue}% to {newTaxRate}% by user {userId}");
            }
            else if (!isTaxPercentage && newTaxAmount != po.TaxAmount)
            {
                var amendment = new PO_OrderAmendments25
                {
                    PO_OrderId = poId,
                    AmendmentType = "TaxChange",
                    PreviousValue = po.TaxAmount,
                    NewValue = newTaxAmount,
                    AmendedByUserId = userId,
                    AmendedDate = DateTime.Now,
                    Reason = reason
                };
                _db.PO_OrderAmendments25.Add(amendment);

                po.TaxAmount = newTaxAmount;
                po.IsTaxPercentage = false;
                po.TaxRate = null; // Clear rate when using fixed amount

                PurchaseOrderLogger.Log($"[AmendTax] PO {poId} tax amount amended from {amendment.PreviousValue} to {newTaxAmount} by user {userId}");
            }

            po.ModifiedDate = DateTime.Now;
            _db.SaveChanges();

            return true;
        }

        /// <summary>
        /// Get amendment history for a PO
        /// </summary>
        public List<VM_Amendment25> GetAmendmentHistory(int poId)
        {
            return _db.PO_OrderAmendments25
                .Where(a => a.PO_OrderId == poId)
                .OrderByDescending(a => a.AmendedDate)
                .Select(a => new VM_Amendment25
                {
                    AmendmentId = a.AmendmentId,
                    PO_OrderId = a.PO_OrderId,
                    AmendmentType = a.AmendmentType,
                    PreviousValue = a.PreviousValue,
                    NewValue = a.NewValue,
                    AmendedBy = _db.Users
                        .Where(u => u.Id == a.AmendedByUserId)
                        .Select(u => u.FirstName + " " + u.LastName)
                        .FirstOrDefault() ?? "Unknown",
                    AmendedDate = a.AmendedDate,
                    Reason = a.Reason
                })
                .ToList();
        }

        /// <summary>
        /// Get a friendly description of the amendment type
        /// </summary>
        public static string GetAmendmentTypeDescription(string amendmentType)
        {
            switch (amendmentType)
            {
                case "FreightChange":
                    return "Freight/Shipping Changed";
                case "TaxChange":
                    return "Tax Amount Changed";
                case "TaxRateChange":
                    return "Tax Rate Changed";
                default:
                    return amendmentType;
            }
        }
    }
}
