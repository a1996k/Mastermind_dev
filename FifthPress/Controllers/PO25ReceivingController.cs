using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using FifthPress.Models.ViewModels2.PO25;

namespace FifthPress.Controllers
{
    public class PO25ReceivingController : DataController
    {
        [HttpPost]
        public async Task<ActionResult> MarkItemAsReceived(int lineItemId, int quantityReceived, string storageLocation, string notes)
        {
            if (quantityReceived <= 0)
                return Json(new { success = false, message = "Quantity received must be greater than zero." });

            var lineItem = await db.PO_LineItems25.FindAsync(lineItemId);
            if (lineItem == null)
                return Json(new { success = false, message = "Line item not found." });

            int userId = UserSessionHelper.GetActiveUserId(HttpContext);

            // Create the receipt record
            var receipt = new PO_InventoryReceipts25
            {
                PO_LineItemId = lineItemId,
                QuantityReceived = quantityReceived,
                StorageLocation = storageLocation,
                Notes = notes,
                DateReceived = DateTime.UtcNow,
                ReceivedByUserId = userId,
                CreatedDate = DateTime.UtcNow
            };

            db.PO_InventoryReceipts25.Add(receipt);
            try
            {
                await db.SaveChangesAsync();
            }
            catch (Exception e)
            {
            }

            // Update MaterialInventory if applicable
            if (lineItem.InventoryTracked && lineItem.MaterialId.HasValue)
            {
                var material = await db.Materials.FindAsync(lineItem.MaterialId.Value);
                if (material != null)
                {
                    var inventory = await db.MaterialInventories.FindAsync(material.Inventory);
                    if (inventory == null)
                    {
                        inventory = new MaterialInventory
                        {
                            Stock = 0,
                            Allocated = 0,
                            CostType = material.CostType
                        };

                        db.MaterialInventories.Add(inventory);
                        await db.SaveChangesAsync();

                        // Wire the new inventory record to the material
                        material.Inventory = inventory.Id;
                        db.Entry(material).State = EntityState.Modified;
                        await db.SaveChangesAsync();
                    }

                    // BULK/SPECIAL PRICING HANDLING:
                    // For old container-based bulk items with BulkUnitQuantity set: multiply to get inventory units
                    // For new simplified bulk pricing (Per Hundred/Thousand): quantityReceived IS the inventory quantity
                    decimal inventoryQuantityToAdd;
                    if (lineItem.IsBulkItem && lineItem.BulkUnitQuantity.HasValue && lineItem.BulkUnitQuantity.Value > 0)
                    {
                        // Legacy container-based bulk items: quantityReceived is containers
                        inventoryQuantityToAdd = quantityReceived * lineItem.BulkUnitQuantity.Value;
                    }
                    else
                    {
                        // Regular items and new bulk pricing items: quantityReceived is the inventory quantity
                        inventoryQuantityToAdd = quantityReceived;
                    }

                    inventory.Stock += (int)Math.Ceiling(inventoryQuantityToAdd);
                    db.Entry(inventory).State = EntityState.Modified;
                    await db.SaveChangesAsync();
                }
            }

            return Json(new { success = true });
        }

        [HttpPost]
        public ActionResult MarkAsReceived(int poId)
        {
            int userId = UserSessionHelper.GetActiveUserId(HttpContext);

            var po = db.PO_Orders25.Include("PO_LineItems25").FirstOrDefault(p => p.PO_OrderId == poId);
            if (po == null)
            {
                return Json(new { success = false, message = "PO not found." });
            }

            var lineItems = po.PO_LineItems25.ToList();

            foreach (var item in lineItems)
            {
                // Get remaining quantity
                var receipts = db.PO_InventoryReceipts25.Where(r => r.PO_LineItemId == item.PO_LineItemId).ToList();
                var totalReceived = receipts.Sum(r => r.QuantityReceived);
                var remaining = item.Quantity - totalReceived;

                if (remaining <= 0)
                    continue;

                // Create receipt record
                var receipt = new PO_InventoryReceipts25
                {
                    PO_LineItemId = item.PO_LineItemId,
                    QuantityReceived = remaining,
                    DateReceived = DateTime.UtcNow,
                    ReceivedByUserId = userId,
                    Notes = "Bulk receive",
                    StorageLocation = null,
                    CreatedDate = DateTime.UtcNow
                };
                db.PO_InventoryReceipts25.Add(receipt);

                // Update MaterialInventory if linked
                if (item.MaterialId.HasValue)
                {
                    var material = db.Materials.Find(item.MaterialId.Value);
                    if (material != null)
                    {
                        var inv = db.MaterialInventories.Find(material.Inventory);
                        if (inv != null)
                        {
                            // BULK/SPECIAL PRICING HANDLING:
                            // For old container-based bulk items with BulkUnitQuantity set: multiply to get inventory units
                            // For new simplified bulk pricing (Per Hundred/Thousand): remaining IS the inventory quantity
                            decimal inventoryQuantityToAdd;
                            if (item.IsBulkItem && item.BulkUnitQuantity.HasValue && item.BulkUnitQuantity.Value > 0)
                            {
                                // Legacy container-based bulk items: remaining is containers
                                inventoryQuantityToAdd = remaining * item.BulkUnitQuantity.Value;
                            }
                            else
                            {
                                // Regular items and new bulk pricing items: remaining is the inventory quantity
                                inventoryQuantityToAdd = remaining;
                            }

                            inv.Stock += (int)Math.Ceiling(inventoryQuantityToAdd);
                            db.Entry(inv).State = EntityState.Modified;
                        }
                    }
                }

                // Mark delivered if fully received
                if (remaining >= item.Quantity)
                {
                    item.DateReceived = DateTime.UtcNow;
                    db.Entry(item).State = EntityState.Modified;
                }
            }

            db.SaveChanges();

            return Json(new { success = true });
        }

        public ActionResult ReceivingHistory(int id)
        {
            var po = db.PO_Orders25
                .Include(p => p.PO_LineItems25)
                .FirstOrDefault(p => p.PO_OrderId == id);

            if (po == null)
                return HttpNotFound();

            var lineItems = po.PO_LineItems25
                .Select(li => new
                {
                    LineItem = li,
                    Receipts = db.PO_InventoryReceipts25
                        .Include(r => r.User)
                        .Where(r => r.PO_LineItemId == li.PO_LineItemId)
                        .OrderByDescending(r => r.DateReceived)
                        .ToList()
                })
                .Where(item => item.Receipts.Any())
                .ToList();

            var model = new VM_POReceivingHistory25
            {
                POId = id,
                PONumber = id,
                LineItemHistories = lineItems.Select(item => new VM_LineItemReceivingHistory25
                {
                    LineItemId = item.LineItem.PO_LineItemId,
                    ItemDescription = item.LineItem.ItemDescription,
                    QuantityOrdered = item.LineItem.Quantity,
                    TotalReceived = item.Receipts.Sum(r => r.QuantityReceived),
                    Receipts = item.Receipts.Select(r => new VM_ReceiptRecord25
                    {
                        QuantityReceived = r.QuantityReceived,
                        DateReceived = r.DateReceived,
                        ReceivedByName = r.User != null ? r.User.FirstName + " " + r.User.LastName : "Unknown",
                        Notes = r.Notes,
                        StorageLocation = r.StorageLocation
                    }).ToList()
                }).ToList()
            };

            return View("ReceivingHistory", model);
        }

        public ActionResult ItemReceivingHistory(int lineItemId)
        {
            var lineItem = db.PO_LineItems25
                .Include(li => li.PO_Orders25)
                .Include(li => li.PO_InventoryReceipts25.Select(r => r.User))
                .FirstOrDefault(li => li.PO_LineItemId == lineItemId);

            if (lineItem == null)
                return HttpNotFound();

            ViewBag.POId = lineItem.PO_OrderId;

            var model = new VM_ReceivingHistory25
            {
                LineItemId = lineItemId,
                ItemDescription = lineItem.ItemDescription,
                QuantityOrdered = lineItem.Quantity,
                Receipts = lineItem.PO_InventoryReceipts25
                    .OrderByDescending(r => r.DateReceived)
                    .Select(r => new VM_ReceiptRecord25
                    {
                        QuantityReceived = r.QuantityReceived,
                        DateReceived = r.DateReceived,
                        ReceivedByName = r.User != null ? r.User.FirstName + " " + r.User.LastName : "Unknown",
                        Notes = r.Notes,
                        StorageLocation = r.StorageLocation
                    }).ToList()
            };

            return View("ItemReceivingHistory", model);
        }
    }
}