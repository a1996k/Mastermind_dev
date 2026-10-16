using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using FifthPress.Models.ViewModels2.PO25;

namespace FifthPress
{
    public class DepartmentSplitService25
    {
        private readonly FifthPressEntities _db;

        public DepartmentSplitService25(FifthPressEntities db)
        {
            _db = db;
        }

        #region Order-Level Splits

        /// <summary>
        /// Validate that order-level splits sum to 100%
        /// </summary>
        public bool ValidateOrderLevelSplits(List<VM_DepartmentSplit25> splits)
        {
            if (splits == null || !splits.Any())
                return false;

            var total = splits.Sum(s => s.SplitPercentage ?? 0);
            return Math.Abs(total - 100) < 0.01m; // Allow small rounding error
        }

        /// <summary>
        /// Save order-level percentage splits
        /// </summary>
        public bool SaveOrderSplits(int poId, List<VM_DepartmentSplit25> splits, bool useTransaction = true)
        {
            if (!ValidateOrderLevelSplits(splits))
                return false;

            try
            {
                // Remove existing splits for this order
                var existingSplits = _db.PO_OrderDepartmentSplits25
                    .Where(s => s.PO_OrderId == poId)
                    .ToList();
                _db.PO_OrderDepartmentSplits25.RemoveRange(existingSplits);

                // Add new splits
                foreach (var split in splits.Where(s => s.SplitPercentage > 0))
                {
                    var newSplit = new PO_OrderDepartmentSplits25
                    {
                        PO_OrderId = poId,
                        DepartmentId = split.DepartmentId,
                        SplitPercentage = split.SplitPercentage.Value,
                        CreatedDate = DateTime.Now
                    };
                    _db.PO_OrderDepartmentSplits25.Add(newSplit);
                }

                // Update order to indicate it uses splits
                var po = _db.PO_Orders25.Find(poId);
                if (po != null)
                {
                    po.UsesDepartmentSplits = true;
                    po.SplitType = "OrderLevel";
                }

                _db.SaveChanges();
                return true;
            }
            catch (Exception ex)
            {
                PurchaseOrderLogger.LogException(ex);
                return false;
            }
        }

        /// <summary>
        /// Get order-level splits for a PO
        /// </summary>
        public List<VM_DepartmentSplit25> GetOrderSplits(int poId)
        {
            return _db.PO_OrderDepartmentSplits25
                .Where(s => s.PO_OrderId == poId)
                .Select(s => new VM_DepartmentSplit25
                {
                    SplitId = s.OrderSplitId,
                    DepartmentId = s.DepartmentId,
                    DepartmentName = s.Department.Title,
                    SplitPercentage = s.SplitPercentage,
                    IsPercentageBased = true
                })
                .ToList();
        }

        #endregion Order-Level Splits

        #region Line Item-Level Splits

        /// <summary>
        /// Validate line item splits (must sum to 100% if percentage-based)
        /// </summary>
        public bool ValidateLineItemSplits(List<VM_DepartmentSplit25> splits)
        {
            if (splits == null || !splits.Any())
                return false;

            // If percentage-based, must sum to 100%
            if (splits.All(s => s.IsPercentageBased))
            {
                var total = splits.Sum(s => s.SplitPercentage ?? 0);
                return Math.Abs(total - 100) < 0.01m;
            }

            // Amount-based splits don't need to sum to anything specific
            return true;
        }

        /// <summary>
        /// Save line item-level splits
        /// </summary>
        public bool SaveLineItemSplits(int lineItemId, List<VM_DepartmentSplit25> splits)
        {
            using (var transaction = _db.Database.BeginTransaction())
            {
                try
                {
                    // Remove existing splits for this line item
                    var existingSplits = _db.PO_LineItemDepartmentSplits25
                        .Where(s => s.PO_LineItemId == lineItemId)
                        .ToList();
                    _db.PO_LineItemDepartmentSplits25.RemoveRange(existingSplits);

                    // Add new splits
                    foreach (var split in splits)
                    {
                        var newSplit = new PO_LineItemDepartmentSplits25
                        {
                            PO_LineItemId = lineItemId,
                            DepartmentId = split.DepartmentId,
                            SplitPercentage = split.IsPercentageBased ? split.SplitPercentage : null,
                            SplitAmount = split.IsPercentageBased ? null : split.SplitAmount,
                            IsPercentageBased = split.IsPercentageBased,
                            CreatedDate = DateTime.Now
                        };
                        _db.PO_LineItemDepartmentSplits25.Add(newSplit);
                    }

                    _db.SaveChanges();
                    transaction.Commit();
                    return true;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    PurchaseOrderLogger.LogException(ex);
                    return false;
                }
            }
        }

        /// <summary>
        /// Get line item-level splits
        /// </summary>
        public List<VM_DepartmentSplit25> GetLineItemSplits(int lineItemId)
        {
            return _db.PO_LineItemDepartmentSplits25
                .Where(s => s.PO_LineItemId == lineItemId)
                .Select(s => new VM_DepartmentSplit25
                {
                    SplitId = s.SplitId,
                    DepartmentId = s.DepartmentId,
                    DepartmentName = s.Department.Title,
                    SplitPercentage = s.SplitPercentage,
                    SplitAmount = s.SplitAmount,
                    IsPercentageBased = s.IsPercentageBased
                })
                .ToList();
        }

        #endregion Line Item-Level Splits

        #region Budget Calculations

        /// <summary>
        /// Calculate actual amounts for each department from percentage splits
        /// </summary>
        public Dictionary<int, decimal> CalculateSplitAmounts(int poId)
        {
            var result = new Dictionary<int, decimal>();
            var po = _db.PO_Orders25
                .Include(p => p.PO_LineItems25)
                .FirstOrDefault(p => p.PO_OrderId == poId);

            if (po == null)
                return result;

            var totalAmount = po.PO_LineItems25
                .Where(li => !li.Billable)
                .Sum(li => li.Cost * li.Quantity);

            if (po.SplitType == "OrderLevel")
            {
                // Order-level percentage splits
                var orderSplits = _db.PO_OrderDepartmentSplits25
                    .Where(s => s.PO_OrderId == poId)
                    .ToList();

                foreach (var split in orderSplits)
                {
                    var amount = totalAmount * (split.SplitPercentage / 100);
                    if (!result.ContainsKey(split.DepartmentId))
                        result[split.DepartmentId] = 0;
                    result[split.DepartmentId] += amount;
                }
            }
            else if (po.SplitType == "LineItemLevel")
            {
                // Line item-level splits
                foreach (var lineItem in po.PO_LineItems25.Where(li => !li.Billable))
                {
                    var lineTotal = lineItem.Cost * lineItem.Quantity;
                    var splits = _db.PO_LineItemDepartmentSplits25
                        .Where(s => s.PO_LineItemId == lineItem.PO_LineItemId)
                        .ToList();

                    if (splits.Any())
                    {
                        foreach (var split in splits)
                        {
                            decimal amount;
                            if (split.IsPercentageBased)
                            {
                                amount = lineTotal * ((split.SplitPercentage ?? 0) / 100);
                            }
                            else
                            {
                                amount = split.SplitAmount ?? 0;
                            }

                            if (!result.ContainsKey(split.DepartmentId))
                                result[split.DepartmentId] = 0;
                            result[split.DepartmentId] += amount;
                        }
                    }
                    else if (lineItem.DepartmentId.HasValue)
                    {
                        // Line item assigned to specific department (no splits)
                        if (!result.ContainsKey(lineItem.DepartmentId.Value))
                            result[lineItem.DepartmentId.Value] = 0;
                        result[lineItem.DepartmentId.Value] += lineTotal;
                    }
                    else if (po.DepartmentId.HasValue)
                    {
                        // Fall back to order's department
                        if (!result.ContainsKey(po.DepartmentId.Value))
                            result[po.DepartmentId.Value] = 0;
                        result[po.DepartmentId.Value] += lineTotal;
                    }
                }
            }
            else if (po.DepartmentId.HasValue)
            {
                // No splits - all goes to order's department
                result[po.DepartmentId.Value] = totalAmount;
            }

            return result;
        }

        /// <summary>
        /// Create budget usage records for split departments
        /// </summary>
        public void CreateSplitBudgetUsage(int poId)
        {
            var po = _db.PO_Orders25
                .Include(p => p.PO_LineItems25)
                .FirstOrDefault(p => p.PO_OrderId == poId);

            if (po == null || !po.UsesDepartmentSplits)
                return;

            var splitAmounts = CalculateSplitAmounts(poId);

            foreach (var lineItem in po.PO_LineItems25.Where(li => !li.Billable))
            {
                var lineTotal = lineItem.Cost * lineItem.Quantity;

                if (po.SplitType == "OrderLevel")
                {
                    // Create proportional usage for each department based on order splits
                    var orderSplits = _db.PO_OrderDepartmentSplits25
                        .Where(s => s.PO_OrderId == poId)
                        .ToList();

                    foreach (var split in orderSplits)
                    {
                        var amount = lineTotal * (split.SplitPercentage / 100);
                        var usage = new PO_DepartmentBudgetUsage25
                        {
                            PO_LineItemId = lineItem.PO_LineItemId,
                            Amount = amount,
                            IsBillable = false,
                            DepartmentId = split.DepartmentId,
                            UsageDate = DateTime.Now
                        };
                        _db.PO_DepartmentBudgetUsage25.Add(usage);
                    }
                }
                else if (po.SplitType == "LineItemLevel")
                {
                    // Create usage based on line-item specific splits
                    var lineItemSplits = _db.PO_LineItemDepartmentSplits25
                        .Where(s => s.PO_LineItemId == lineItem.PO_LineItemId)
                        .ToList();

                    if (lineItemSplits.Any())
                    {
                        foreach (var split in lineItemSplits)
                        {
                            decimal amount;
                            if (split.IsPercentageBased)
                            {
                                amount = lineTotal * ((split.SplitPercentage ?? 0) / 100);
                            }
                            else
                            {
                                amount = split.SplitAmount ?? 0;
                            }

                            var usage = new PO_DepartmentBudgetUsage25
                            {
                                PO_LineItemId = lineItem.PO_LineItemId,
                                Amount = amount,
                                IsBillable = false,
                                DepartmentId = split.DepartmentId,
                                SplitId = split.SplitId,
                                UsageDate = DateTime.Now
                            };
                            _db.PO_DepartmentBudgetUsage25.Add(usage);
                        }
                    }
                    else
                    {
                        // No splits - use line item's department or order's department
                        var deptId = lineItem.DepartmentId ?? po.DepartmentId;
                        var usage = new PO_DepartmentBudgetUsage25
                        {
                            PO_LineItemId = lineItem.PO_LineItemId,
                            Amount = lineTotal,
                            IsBillable = false,
                            DepartmentId = deptId,
                            UsageDate = DateTime.Now
                        };
                        _db.PO_DepartmentBudgetUsage25.Add(usage);
                    }
                }
            }

            _db.SaveChanges();
        }

        /// <summary>
        /// Check budgets for all departments in a split scenario
        /// </summary>
        public List<BudgetCheckResult> CheckBudgetWithSplits(int poId)
        {
            var results = new List<BudgetCheckResult>();
            var splitAmounts = CalculateSplitAmounts(poId);
            var budgetService = new BudgetService25(_db);

            foreach (var kvp in splitAmounts)
            {
                var result = budgetService.CheckBudget(kvp.Key, kvp.Value);
                results.Add(result);
            }

            return results;
        }

        #endregion Budget Calculations

        #region Utility Methods

        /// <summary>
        /// Clear all splits from a PO (resets to single department)
        /// </summary>
        public bool ClearAllSplits(int poId)
        {
            try
            {
                // Remove order-level splits
                var orderSplits = _db.PO_OrderDepartmentSplits25
                    .Where(s => s.PO_OrderId == poId)
                    .ToList();
                _db.PO_OrderDepartmentSplits25.RemoveRange(orderSplits);

                // Remove line-item level splits
                var lineItemIds = _db.PO_LineItems25
                    .Where(li => li.PO_OrderId == poId)
                    .Select(li => li.PO_LineItemId)
                    .ToList();

                var lineItemSplits = _db.PO_LineItemDepartmentSplits25
                    .Where(s => lineItemIds.Contains(s.PO_LineItemId))
                    .ToList();
                _db.PO_LineItemDepartmentSplits25.RemoveRange(lineItemSplits);

                // Update order
                var po = _db.PO_Orders25.Find(poId);
                if (po != null)
                {
                    po.UsesDepartmentSplits = false;
                    po.SplitType = null;
                }

                _db.SaveChanges();
                return true;
            }
            catch (Exception ex)
            {
                PurchaseOrderLogger.LogException(ex);
                return false;
            }
        }

        /// <summary>
        /// Copy splits from one PO to another (for Copy PO feature)
        /// </summary>
        public bool CopySplits(int sourcePoId, int targetPoId)
        {
            var sourcePo = _db.PO_Orders25.Find(sourcePoId);
            if (sourcePo == null || !sourcePo.UsesDepartmentSplits)
                return false;

            if (sourcePo.SplitType == "OrderLevel")
            {
                var splits = GetOrderSplits(sourcePoId);
                return SaveOrderSplits(targetPoId, splits);
            }

            // Line-item level splits would need to be copied per line item
            // This would typically be handled in the line item copy logic
            return true;
        }

        #endregion Utility Methods
    }
}
