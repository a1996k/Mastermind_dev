using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using FifthPress.Models.ViewModels2.PO25;

namespace FifthPress
{
    public class InventoryService25
    {
        private readonly FifthPressEntities _db;

        public InventoryService25(FifthPressEntities db)
        {
            _db = db;
        }

        public bool MarkItemAsReceived(int lineItemId, int quantityReceived, int userId, string storageLocation = "", string notes = "")
        {
            using (var transaction = _db.Database.BeginTransaction())
            {
                try
                {
                    var lineItem = _db.PO_LineItems25
                        .Include(li => li.PO_Orders25)
                        .FirstOrDefault(li => li.PO_LineItemId == lineItemId);

                    if (lineItem == null || !lineItem.InventoryTracked)
                    {
                        transaction.Rollback();
                        return false;
                    }

                    if (lineItem.PO_Orders25.Status != "Approved")
                    {
                        transaction.Rollback();
                        return false;
                    }

                    // Always create a new receipt record for each partial delivery
                    var receipt = new PO_InventoryReceipts25
                    {
                        PO_LineItemId = lineItemId,
                        QuantityReceived = quantityReceived,
                        DateReceived = DateTime.Now,
                        ReceivedByUserId = userId,
                        StorageLocation = storageLocation,
                        Notes = notes,
                        CreatedDate = DateTime.Now
                    };
                    _db.PO_InventoryReceipts25.Add(receipt);

                    // Calculate total received quantity
                    var totalReceived = _db.PO_InventoryReceipts25
                        .Where(r => r.PO_LineItemId == lineItemId)
                        .Sum(r => r.QuantityReceived) + quantityReceived; // Include current receipt

                    // Update line item DateReceived to most recent receipt date
                    lineItem.DateReceived = DateTime.Now;

                    // Optionally update status if fully received
                    if (totalReceived >= lineItem.Quantity)
                    {
                        lineItem.Status = "Fully Received";
                    }

                    // Adjust stock if line item is linked to a Material
                    if (lineItem.MaterialId.HasValue)
                    {
                        var material = _db.Materials.FirstOrDefault(m => m.Id == lineItem.MaterialId);
                        if (material != null && material.Inventory.HasValue)
                        {
                            _db.Entry(material).Reference(m => m.MaterialInventory).Load();

                            // CREATE INVENTORY ADJUSTMENT HISTORY RECORD (matching DataController.SlotMaterial pattern)
                            var inventoryAdjustment = new InventoryAdjustmentHistory
                            {
                                Material = material.Id,
                                DateUTC = DateTime.UtcNow,
                                AdjustedBy = userId,
                                OldStockAmount = material.MaterialInventory.Stock,
                                OldAllocatedAmount = material.MaterialInventory.Allocated ?? 0,
                                WasMaterialPull = false, // This is receiving, not a pull
                                Job = lineItem.JobId,
                                JobItem = null,
                                QuantityUnit = lineItem.UnitType // Track the unit type
                            };

                            // Update stock - quantityReceived already represents the actual inventory amount
                            material.MaterialInventory.Stock += quantityReceived;

                            // Record new amounts after adjustment
                            inventoryAdjustment.NewStockAmount = material.MaterialInventory.Stock;
                            inventoryAdjustment.NewAllocatedAmount = material.MaterialInventory.Allocated ?? 0;

                            // Add history record to database
                            _db.InventoryAdjustmentHistories.Add(inventoryAdjustment);
                            _db.Entry(material.MaterialInventory).State = EntityState.Modified;
                        }
                    }

                    _db.SaveChanges();
                    transaction.Commit();
                    return true;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    // Log the exception for debugging
                    System.Diagnostics.Debug.WriteLine($"Error in MarkItemAsReceived: {ex.Message}");
                    return false;
                }
            }
        }

        // Helper method to get receiving history
        public List<VM_ReceiptHistory25> GetReceiptHistory(int lineItemId)
        {
            return _db.PO_InventoryReceipts25
                .Where(r => r.PO_LineItemId == lineItemId)
                .Include(r => r.User) // Assuming you have a User navigation property
                .OrderByDescending(r => r.DateReceived)
                .Select(r => new VM_ReceiptHistory25
                {
                    ReceiptId = r.ReceiptId,
                    QuantityReceived = r.QuantityReceived,
                    DateReceived = r.DateReceived,
                    ReceivedBy = r.User.FirstName + " " + r.User.LastName,
                    StorageLocation = r.StorageLocation,
                    Notes = r.Notes
                })
                .ToList();
        }

        // Helper method to get receiving summary
        public VM_ReceivingSummary25 GetReceivingSummary(int lineItemId)
        {
            var lineItem = _db.PO_LineItems25.Find(lineItemId);
            if (lineItem == null) return null;

            var receipts = _db.PO_InventoryReceipts25
                .Where(r => r.PO_LineItemId == lineItemId)
                .ToList();

            var totalReceived = receipts.Sum(r => r.QuantityReceived);
            var remaining = lineItem.Quantity - totalReceived;

            return new VM_ReceivingSummary25
            {
                OrderedQuantity = lineItem.Quantity,
                TotalReceived = totalReceived,
                RemainingQuantity = remaining > 0 ? remaining : 0,
                IsFullyReceived = remaining <= 0,
                LastReceiptDate = receipts.OrderByDescending(r => r.DateReceived)
                                        .FirstOrDefault()?.DateReceived,
                ReceiptCount = receipts.Count
            };
        }

        public List<VM_InventoryItem25> GetInventoryItems(string searchTerm = "", bool? received = null)
        {
            var query = _db.PO_LineItems25
                .Where(li => li.InventoryTracked)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(li => li.ItemDescription.Contains(searchTerm) ||
                                         li.PO_Orders25.PO_OrderId.ToString().Contains(searchTerm));
            }

            if (received.HasValue)
            {
                if (received.Value)
                    query = query.Where(li => _db.PO_InventoryReceipts25.Any(r => r.PO_LineItemId == li.PO_LineItemId));
                else
                    query = query.Where(li => !_db.PO_InventoryReceipts25.Any(r => r.PO_LineItemId == li.PO_LineItemId));
            }

            return query
                .Include(li => li.PO_Orders25)
                .Include(li => li.PO_Orders25.Department)
                .Include(li => li.PO_Orders25.User) // Using .User as clarified
                .OrderByDescending(li => li.PO_Orders25.DateOrdered)
                .ToList()
                .Select(li => new VM_InventoryItem25
                {
                    PO_LineItemId = li.PO_LineItemId,
                    PO_OrderId = li.PO_OrderId,
                    ItemDescription = li.ItemDescription,
                    Quantity = li.Quantity,
                    Cost = li.Cost,
                    TotalValue = li.Cost * li.Quantity,
                    DateOrdered = li.PO_Orders25.DateOrdered,
                    DateExpected = li.DateExpected,
                    DateReceived = GetReceiptDate(li.PO_LineItemId),
                    IsReceived = IsItemReceived(li.PO_LineItemId),
                    DepartmentName = li.PO_Orders25.Department?.Title,
                    OrderedBy = li.PO_Orders25.User?.FirstName + " " + li.PO_Orders25.User?.LastName, // Using .User as clarified
                    StorageLocation = GetStorageLocation(li.PO_LineItemId)
                })
                .ToList();
        }

        public VM_InventoryItem25 GetInventoryItem(int lineItemId)
        {
            var item = _db.PO_LineItems25
                .Include(li => li.PO_Orders25)
                .Include(li => li.PO_Orders25.Department)
                .Include(li => li.PO_Orders25.User) // Using .User as clarified
                .FirstOrDefault(li => li.PO_LineItemId == lineItemId && li.InventoryTracked);

            if (item == null) return null;

            return new VM_InventoryItem25
            {
                PO_LineItemId = item.PO_LineItemId,
                PO_OrderId = item.PO_OrderId,
                ItemDescription = item.ItemDescription,
                Quantity = item.Quantity,
                Cost = item.Cost,
                TotalValue = item.Cost * item.Quantity,
                DateOrdered = item.PO_Orders25.DateOrdered,
                DateExpected = item.DateExpected,
                DateReceived = GetReceiptDate(item.PO_LineItemId),
                IsReceived = IsItemReceived(item.PO_LineItemId),
                DepartmentName = item.PO_Orders25.Department?.Title,
                OrderedBy = item.PO_Orders25.User?.FirstName + " " + item.PO_Orders25.User?.LastName, // Using .User as clarified
                StorageLocation = GetStorageLocation(item.PO_LineItemId),
                InternalComments = item.InternalComments,
                VendorLink = item.VendorLink
            };
        }

        public VM_InventoryReceipt25 GetLineItemReceipt(int lineItemId)
        {
            var receipt = _db.PO_InventoryReceipts25
                .Include(r => r.PO_LineItems25)
                .Include(r => r.User) // Using .User as clarified
                .FirstOrDefault(r => r.PO_LineItemId == lineItemId);

            if (receipt == null) return null;

            return new VM_InventoryReceipt25
            {
                PO_LineItemId = lineItemId,
                QuantityOrdered = receipt.PO_LineItems25.Quantity,
                QuantityReceived = receipt.QuantityReceived,
                DateReceived = receipt.DateReceived,
                ReceivedBy = receipt.User?.FirstName + " " + receipt.User?.LastName, // Using .User as clarified
                StorageLocation = receipt.StorageLocation,
                Notes = receipt.Notes,
                IsFullyReceived = receipt.QuantityReceived >= receipt.PO_LineItems25.Quantity
            };
        }

        public List<VM_InventorySummary25> GetInventorySummaryByDepartment()
        {
            return _db.PO_LineItems25
                .Where(li => li.InventoryTracked)
                .GroupBy(li => li.PO_Orders25.Department.Title)
                .Select(g => new VM_InventorySummary25
                {
                    DepartmentName = g.Key,
                    TotalItems = g.Sum(li => li.Quantity),
                    TotalValue = g.Sum(li => li.Cost * li.Quantity),
                    ReceivedItems = g.Where(li => _db.PO_InventoryReceipts25.Any(r => r.PO_LineItemId == li.PO_LineItemId))
                                     .Sum(li => li.Quantity),
                    PendingItems = g.Where(li => !_db.PO_InventoryReceipts25.Any(r => r.PO_LineItemId == li.PO_LineItemId))
                                    .Sum(li => li.Quantity)
                })
                .ToList();
        }

        public List<VM_PendingDelivery25> GetPendingDeliveries()
        {
            var thirtyDaysAgo = DateTime.Now.AddDays(-30);

            return _db.PO_LineItems25
                .Where(li => li.InventoryTracked &&
                           li.PO_Orders25.Status == "Approved" &&
                           !_db.PO_InventoryReceipts25.Any(r => r.PO_LineItemId == li.PO_LineItemId) &&
                           li.PO_Orders25.DateOrdered >= thirtyDaysAgo)
                .Include(li => li.PO_Orders25)
                .Include(li => li.PO_Orders25.Department)
                .OrderBy(li => li.DateExpected ?? DateTime.MaxValue)
                .ToList()
                .Select(li => new VM_PendingDelivery25
                {
                    PO_OrderId = li.PO_OrderId,
                    PO_LineItemId = li.PO_LineItemId,
                    ItemDescription = li.ItemDescription,
                    Quantity = li.Quantity,
                    DateOrdered = li.PO_Orders25.DateOrdered,
                    DateExpected = li.DateExpected,
                    DaysOverdue = li.DateExpected.HasValue && li.DateExpected < DateTime.Now
                        ? (int)(DateTime.Now - li.DateExpected.Value).TotalDays
                        : 0,
                    DepartmentName = li.PO_Orders25.Department?.Title,
                    IsOverdue = li.DateExpected.HasValue && li.DateExpected < DateTime.Now
                })
                .ToList();
        }

        public VM_InventoryDashboard25 GetInventoryDashboard()
        {
            var allInventoryItems = _db.PO_LineItems25.Where(li => li.InventoryTracked).ToList();
            var receivedItems = allInventoryItems.Where(li =>
                _db.PO_InventoryReceipts25.Any(r => r.PO_LineItemId == li.PO_LineItemId)).ToList();
            var pendingItems = allInventoryItems.Where(li =>
                !_db.PO_InventoryReceipts25.Any(r => r.PO_LineItemId == li.PO_LineItemId)).ToList();

            return new VM_InventoryDashboard25
            {
                TotalItemsTracked = allInventoryItems.Sum(li => li.Quantity),
                TotalValueTracked = allInventoryItems.Sum(li => li.Cost * li.Quantity),
                ItemsReceived = receivedItems.Sum(li => li.Quantity),
                ValueReceived = receivedItems.Sum(li => li.Cost * li.Quantity),
                ItemsPending = pendingItems.Sum(li => li.Quantity),
                ValuePending = pendingItems.Sum(li => li.Cost * li.Quantity),
                OverdueDeliveries = GetPendingDeliveries().Count(d => d.IsOverdue),
                RecentReceipts = GetRecentReceipts().Take(5).ToList()
            };
        }

        #region Private Helper Methods

        private DateTime? GetReceiptDate(int lineItemId)
        {
            return _db.PO_InventoryReceipts25
                .Where(r => r.PO_LineItemId == lineItemId)
                .Select(r => (DateTime?)r.DateReceived)
                .FirstOrDefault();
        }

        private bool IsItemReceived(int lineItemId)
        {
            return _db.PO_InventoryReceipts25.Any(r => r.PO_LineItemId == lineItemId);
        }

        private string GetStorageLocation(int lineItemId)
        {
            return _db.PO_InventoryReceipts25
                .Where(r => r.PO_LineItemId == lineItemId)
                .Select(r => r.StorageLocation)
                .FirstOrDefault() ?? "Not assigned";
        }

        private void LogInventoryReceipt(int poId, int userId)
        {
            // Log the receipt for audit trail
            System.Diagnostics.Debug.WriteLine($"PO {poId} inventory items marked as received by user {userId} at {DateTime.Now}");
        }


        private List<VM_RecentReceipt25> GetRecentReceipts()
        {
            return _db.PO_InventoryReceipts25
                .Include(r => r.PO_LineItems25)
                .Include(r => r.PO_LineItems25.PO_Orders25)
                .Include(r => r.User) // Using .User as clarified
                .OrderByDescending(r => r.DateReceived)
                .Take(10)
                .ToList()
                .GroupBy(r => r.PO_LineItems25.PO_OrderId)
                .Select(g => new VM_RecentReceipt25
                {
                    PO_OrderId = g.Key,
                    DateReceived = g.Max(r => r.DateReceived),
                    ReceivedBy = g.First().User?.FirstName + " " + g.First().User?.LastName ?? "Unknown", // Using .User as clarified
                    ItemCount = g.Count(),
                    TotalValue = g.Sum(r => r.PO_LineItems25.Cost * r.QuantityReceived)
                })
                .ToList();
        }

        #endregion Private Helper Methods
    }
}