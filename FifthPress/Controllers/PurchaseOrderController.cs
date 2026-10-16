using FifthPress.ActionFilters;
using FifthPress.Models.ViewModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using FifthPress.Models.ControllerModels;
using FifthPress.Models;
using System.Data;

using Amazon.SimpleEmailV2.Model;
using System.Diagnostics;
using Amazon.Runtime.Internal.Auth;
using Microsoft.Ajax.Utilities;
using System.Management.Instrumentation;

namespace FifthPress.Controllers
{
    [CookieFilter(Order = 1)]
    [UserPermissions(Order = 2)]
    public class PurchaseOrderController : DataController
    {
        private readonly string DefaultShipTo = "FifthColor";
        private readonly string DefaultShipToAddress = "1500 Stedman Way";
        private readonly string DefaultShipToLocation = "Sheboygan Falls, WI 53085";

        private async Task<PO_Orders> LoadPurchaseOrderReferences(PO_Orders o)
        {
            await db.Entry(o).Reference(r => r.Vendor1).LoadAsync();
            await db.Entry(o).Reference(r => r.User).LoadAsync(); //Requestor?
            await db.Entry(o).Reference(r => r.Job1).LoadAsync();
            await db.Entry(o).Reference(r => r.User1).LoadAsync(); //cancelled by?
            await db.Entry(o).Reference(r => r.User2).LoadAsync(); //special approval approver
            await db.Entry(o).Reference(r => r.User3).LoadAsync(); //created by

            //po_items
            await db.Entry(o).Collection(c => c.PO_Items).LoadAsync();

            foreach (PO_Items i in o.PO_Items.ToList())
            {
                i.LoadItemTitle(db);
                await db.Entry(i).Reference(r => r.MaterialCostType).LoadAsync();
                if (i.Job.HasValue)
                    await db.Entry(i).Reference(r => r.Job1).LoadAsync();
            }

            if (o.Vendor.HasValue)
                await db.Entry(o.Vendor1).Reference(r => r.USState).LoadAsync();

            return o;
        }

        public async Task<ActionResult> Print(int? id)
        {
            PO_Orders o = await db.PO_Orders.FindAsync(id);

            if (o != null)
            {
                o = await LoadPurchaseOrderReferences(o);

                VM_Print_PurchaseOrder vm = new VM_Print_PurchaseOrder
                {
                    SourceInformation = new Models.CustomerQuoteModels.SourceInformation(),
                    Order = o
                };

                return View(vm);
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError);
        }

        public string GetPOStatus(PO_Orders o)
        {
            string status = "new";

            //if placedat is not null and cancelled is not null, then "cancelled"
            //if placedat is not null and denials has a length of 0, then "approved"
            //if requestdate has value and placedat is null, then "waiting"

            if (o.PlacedAt.HasValue && o.CancelDate.HasValue)
                status = "cancelled";
            if (o.PlacedAt.HasValue && !o.CancelDate.HasValue && o.PO_Denials.Count == 0)
                status = "approved";
            if (o.RequestDate.HasValue && !o.PlacedAt.HasValue)
                status = "waiting";

            return status;
        }

        //meant for ajax requests to get the real-time status of an order
        public async Task<ActionResult> GetPOStatus(int? id)
        {
            PO_Orders o = await db.PO_Orders.FindAsync(id);

            if (o != null)
            {
                await db.Entry(o).Collection(c => c.PO_Approvals).LoadAsync();
                await db.Entry(o).Collection(c => c.PO_Denials).LoadAsync();

                string status = GetPOStatus(o);

                return new JsonResult
                {
                    Data = status,
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }

            return new JsonResult
            {
                Data = "Not Found",
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        private async Task<int> GetRemainingQuantity(int itemId)
        {
            PO_Items i = await db.PO_Items.FindAsync(itemId);

            await db.Entry(i).Collection(c => c.PO_Receiving).LoadAsync();

            int totalQuantity = i.Quantity ?? 0;
            int deliveredQty = i.PO_Receiving.Sum(x => x.AmountReceived) ?? 0;
            int remainingQty = totalQuantity - deliveredQty;

            return remainingQty;
        }

        private async Task ReceiveInventoryDelivery(int materialId, int amount)
        {
            Material m = await db.Materials.FindAsync(materialId);

            if (m != null)
            {
                MaterialInventory mi = await db.MaterialInventories.FindAsync(m.Inventory);

                if (mi == null)
                {
                    //create a new inventory record if one doesn't exist
                    mi.Allocated = 0;
                    mi.Stock = 0;
                    mi.CostType = m.CostType;
                    db.MaterialInventories.Add(mi);

                    await db.SaveChangesAsync();

                    await db.Entry(mi).ReloadAsync();
                }

                mi.Stock += amount;

                db.Entry(mi).State = EntityState.Modified;

                await db.SaveChangesAsync();
            }
            else
            {
                throw new Exception("Material not found");
            }
        }

        public PO_Orders UpdatePurchaseOrder(PO_Orders e, PO_Orders n)
        {
            PropertyInfo[] props = typeof(PO_Orders).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo p in props)
            {
                if (!p.CanWrite) continue;

                // Skip navigation properties — copying null navigation objects triggers EF
                // relationship fix-up which nullifies FK values, and copying collection
                // interfaces (ICollection<T>) attaches form-bound items as Added, causing
                // spurious INSERTs on the next SaveChanges.
                var propType = p.PropertyType;
                if ((propType.IsClass || propType.IsInterface) && propType != typeof(string)) continue;

                object value = p.GetValue(n);
                p.SetValue(e, value);
            }

            return e;
        }

        public async Task<PO_Items> UpdatePurchaseOrderItem(PO_Items e, PO_Items n)
        {
            e.Quantity = n.Quantity;
            e.Price = n.Price;
            e.TotalPrice = n.TotalPrice;
            e.DueDate = n.DueDate;
            e.GLCode = n.GLCode;
            e.CostType = n.CostType;
            e.Delivered = n.Delivered;
            e.Job = n.Job;
            e.TrackInventory = n.TrackInventory;
            e.Notes = n.Notes;

            if (n.TrackInventory.Value == true)
            {
                e.Item = n.Item;
            }
            else
            {
                try
                {
                    //try to find an "Item" with the same title
                    Item existing = await db.Items.SingleAsync(x => x.Title.ToUpper() == n.ItemTitle.ToUpper());

                    e.Item = existing.Id;
                }
                catch (Exception ex)
                {
                    //no item exists, so make one
                    Item newItem = new Item
                    {
                        Title = n.ItemTitle,
                        Cost = n.Price,
                        CostType = n.CostType
                    };

                    db.Items.Add(newItem);
                    await db.SaveChangesAsync();

                    await db.Entry(newItem).ReloadAsync();

                    e.Item = newItem.Id;
                }
            }

            db.Entry(e).State = EntityState.Modified;

            return e;
        }

        public async Task<ActionResult> Copy(int? id)
        {
            PO_Orders o = await db.PO_Orders.FindAsync(id);

            if (o != null)
            {
                int res = db.DuplicatePurchaseOrder(id);
                int newId = res;
                return RedirectToAction("Edit", "PurchaseOrder", new { id = newId });
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Cannot find that purchase order");
        }

        public async Task<ActionResult> Edit(int? id)
        {
            //HttpCookie userRole = HttpContext.Response.Cookies["UserRole"];

            //ViewData["UserRole"] = "Approver";

            int userId = 0;
            userId = UserSessionHelper.GetActiveUserId(HttpContext);

            User u = await db.Users.FindAsync(userId);
            u.GetPOPermission(db);

            ViewData["UserRole"] = u.POPermission;
            ViewData["ThisUser"] = u.Id;

            PO_Orders order = await db.PO_Orders.FindAsync(id);

            if (order == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "PO Not found");

            await db.Entry(order).Reference(r => r.User).LoadAsync();
            await db.Entry(order).Reference(r => r.User1).LoadAsync();
            await db.Entry(order).Reference(r => r.User2).LoadAsync();
            await db.Entry(order).Reference(r => r.User3).LoadAsync(); //created by
            await db.Entry(order).Collection(c => c.PO_Items).LoadAsync();
            await db.Entry(order).Collection(c => c.PO_Denials).LoadAsync();
            await db.Entry(order).Collection(c => c.PO_Approvals).LoadAsync();

            foreach (PO_Items i in order.PO_Items)
            {
                await db.Entry(i).Collection(c => c.PO_Invoices).LoadAsync();
                await db.Entry(i).Reference(r => r.Job1).LoadAsync();
            }

            await db.Entry(order).Reference(r => r.Vendor1).LoadAsync();

            if (order.Vendor.HasValue)
            {
                await db.Entry(order.Vendor1).Reference(r => r.USState).LoadAsync();
            }

            VM_POEdit vm = new VM_POEdit
            {
                Order = order,
                Items = new List<VM_PO_Items>()
            };

            try
            {
                List<PO_Items> items = order.PO_Items.ToList();

                foreach (PO_Items i in items)
                {
                    await db.Entry(i).Reference(r => r.MaterialCostType).LoadAsync();

                    VM_PO_Items newVM = new VM_PO_Items
                    {
                        Item = i
                    };

                    newVM.Item.LoadItemTitle(db);

                    vm.Items.Add(newVM);
                }

                if (order.RequestDate.HasValue && order.PO_Approvals.Count == 0)
                    vm.AwaitingApproval = true;
                else
                    vm.AwaitingApproval = false;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            ViewData["Order.Vendor"] = await GenerateSelectListAsync(SelectListType.Vendors, order, "Vendor");
            ViewData["State"] = await GenerateSelectListAsync(SelectListType.USStates);
            ViewData["Item.CostType"] = await GenerateSelectListAsync(SelectListType.CostType);
            ViewData["CostType"] = await GenerateSelectListAsync(SelectListType.CostType);
            ViewData["MaterialCategory_Id"] = await GenerateSelectListAsync(SelectListType.MaterialCategories);
            ViewData["StockType"] = await GenerateSelectListAsync(SelectListType.StockType);
            //ViewData["UserRole"] = userRole.Value;
            ViewData["Category"] = await GenerateSelectListAsync(SelectListType.MaterialCategories);
            ViewData["FinishingMaterialFormula"] = await GenerateSelectListAsync(SelectListType.FinishingMaterialFormulas);
            var finishingCategoryIdsPO = await db.MaterialCategories
                .Where(x => x.Deleted == null && x.MaterialType == (int)MaterialCategory.MaterialTypes.Finishing)
                .Select(x => x.Id)
                .ToListAsync();
            ViewData["FinishingCategoryIds"] = string.Join(",", finishingCategoryIdsPO);

            vm.Order.SetPurchaseOrderState();

            if (u.POPermission == "None")
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "permission string is None");
            }

            try
            {
                return View(vm);
            }
            catch (Exception e) { return null; }
        }

        public async Task<ActionResult> Create()
        {
            HttpCookie activeUser = HttpContext.Response.Cookies["ActiveUser"];

            int auId = int.Parse(activeUser.Value);

            User u = await db.Users.FindAsync(auId);
            u.GetPOPermission(db);

            PO_Orders newOrder = new PO_Orders
            {
                DateEntered = DateTime.Now,
                Requestor = auId,
                ShipTo = DefaultShipTo,
                ShipToAddress = String.Format("{0}{1}{2}", DefaultShipToAddress, Environment.NewLine, DefaultShipToLocation),
                RequiredSpecialApproval = false,
                PONumber = db.NextPONumber().First(),
                TaxPercent = 0,
                Freight = 0,
                Tax = 0,
                SubTotal = 0,
                Total = 0,
                POTotal = 0,
                Paid = 0,
                Status = "new",
                CreatedBy = auId
            };

            // default PO Number if NULL
            if (newOrder.PONumber == null)
            {
                newOrder.PONumber = "00001";
            }

            db.PO_Orders.Add(newOrder);

            await db.SaveChangesAsync();

            await db.Entry(newOrder).ReloadAsync();

            ViewData["UserRole"] = u.POPermission;

            if (u.POPermission == "None")
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Permission is None");

            return RedirectToAction("Edit", new { id = newOrder.Id });
        }

        // GET: PurchaseOrder
        public async Task<ActionResult> Index()
        {
            try
            {
                HttpCookie activeUser = HttpContext.Response.Cookies["ActiveUser"];
                HttpCookie userRole = HttpContext.Response.Cookies["UserRole"];

                //purchase order index hotfix for nav
                var req = Request;
                ViewData["IsInternals"] = req.UrlReferrer?.DnsSafeHost == "internals.fifthcolor.com";

                int auId = int.Parse(activeUser.Value);
                User u = await db.Users.FindAsync(auId);
                u.GetPOPermission(db);

                List<PO_Orders> ordersForUser =
                    await db.PO_Orders
                        .Where(x => (x.Requestor == auId || x.CreatedBy == auId) && x.CancelDate == null)
                        .ToListAsync();

                List<PO_Orders> awaitingApproval =
                    await db.PO_Orders
                        //.Where(x => x.CancelDate == null && x.PO_Approvals.Count == 0)
                        .Where(x => x.Status == "pending")
                        .ToListAsync();

                List<PO_Orders> allOrders =
                    await db.PO_Orders
                        .ToListAsync();

                foreach (PO_Orders o in ordersForUser)
                {
                    await db.Entry(o).Reference(r => r.User).LoadAsync();
                    await db.Entry(o).Reference(r => r.Vendor1).LoadAsync();
                    await db.Entry(o).Collection(c => c.PO_Approvals).LoadAsync();
                    await db.Entry(o).Collection(c => c.PO_Items).LoadAsync();

                    foreach (PO_Items i in o.PO_Items)
                    {
                        await db.Entry(i).Reference(r => r.Job1).LoadAsync();
                    }
                }

                foreach (PO_Orders o in awaitingApproval)
                {
                    await db.Entry(o).Reference(r => r.User).LoadAsync();
                    await db.Entry(o).Reference(r => r.Vendor1).LoadAsync();
                    await db.Entry(o).Collection(c => c.PO_Approvals).LoadAsync();
                    await db.Entry(o).Collection(c => c.PO_Items).LoadAsync();

                    foreach (PO_Items i in o.PO_Items)
                    {
                        await db.Entry(i).Reference(r => r.Job1).LoadAsync();
                    }
                }

                foreach (PO_Orders o in allOrders)
                {
                    await db.Entry(o).Reference(r => r.User).LoadAsync();
                    await db.Entry(o).Reference(r => r.Vendor1).LoadAsync();
                    await db.Entry(o).Collection(c => c.PO_Approvals).LoadAsync();
                    await db.Entry(o).Collection(c => c.PO_Items).LoadAsync();

                    foreach (PO_Items i in o.PO_Items)
                    {
                        await db.Entry(i).Reference(r => r.Job1).LoadAsync();
                    }
                }

                VM_POIndex vm = new VM_POIndex
                {
                    AwaitingApproval = awaitingApproval.Select(x => new VM_PO_Orders
                    {
                        Order = x,
                        Items = x.PO_Items != null && x.PO_Items.Count > 0 ?
                            x.PO_Items.Select(y => new VM_PO_Items
                            {
                                Item = y
                            }).ToList()
                            : new List<VM_PO_Items>(),
                        UniqueJobCount = x.PO_Items != null && x.PO_Items.Count > 0 ? x.PO_Items.Select(y => y.Job).Distinct().Count() : 0
                    }).ToList(),
                    MyOrders = ordersForUser.Select(x => new VM_PO_Orders
                    {
                        Order = x,
                        Items = x.PO_Items != null && x.PO_Items.Count > 0 ?
                            x.PO_Items.Select(y => new VM_PO_Items
                            {
                                Item = y
                            }).ToList()
                            : new List<VM_PO_Items>(),
                        UniqueJobCount = x.PO_Items != null && x.PO_Items.Count > 0 ? x.PO_Items.Select(y => y.Job).Distinct().Count() : 0
                    }).ToList(),
                    AllOrders = allOrders.Select(x => new VM_PO_Orders
                    {
                        Order = x,
                        Items = x.PO_Items != null && x.PO_Items.Count > 0 ?
                            x.PO_Items.Select(y => new VM_PO_Items
                            {
                                Item = y
                            }).ToList()
                            : new List<VM_PO_Items>(),
                        UniqueJobCount = x.PO_Items != null && x.PO_Items.Count > 0 ? x.PO_Items.Select(y => y.Job).Distinct().Count() : 0
                    }).ToList()
                };

                //load item titles
                foreach (VM_PO_Items i in vm.AwaitingApproval.SelectMany(x => x.Items).Concat(vm.MyOrders.SelectMany(x => x.Items).Concat(vm.AllOrders.SelectMany(x => x.Items))).ToList())
                    i.Item.LoadItemTitle(db);

                ViewData["UserRole"] = u.POPermission;

                if (u.POPermission == "None")
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Permission is None");

                return View(vm);
            }
            catch (Exception e)
            {
                return null;
            }
        }

        //need permissions
        public async Task<ActionResult> Delete(PO_Orders order)
        {
            PO_Orders existing = await db.PO_Orders.FindAsync(order.Id);

            if (existing != null)
            {
                db.PO_Orders.Remove(existing);
                await db.SaveChangesAsync();

                return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK, "Deleted");
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Error deleting Purchase Order");
        }

        //need permissions
        public async Task<ActionResult> Save(PO_Orders order)
        {
            PO_Orders existing = await db.PO_Orders.FindAsync(order.Id);

            if (existing != null)
            {
                // Preserve fields managed by dedicated workflows — Save must not overwrite these
                var cancelDate  = existing.CancelDate;
                var cancelledBy = existing.CancelledBy;
                var status      = existing.Status;
                var requestDate = existing.RequestDate;
                var requestor   = existing.Requestor;
                var placedAt    = existing.PlacedAt;
                var createdBy   = existing.CreatedBy;

                order.PONumber = existing.PONumber;
                existing = UpdatePurchaseOrder(existing, order);

                existing.CancelDate  = cancelDate;
                existing.CancelledBy = cancelledBy;
                existing.Status      = status;
                existing.RequestDate = requestDate;
                existing.Requestor   = requestor;
                existing.PlacedAt    = placedAt;
                existing.CreatedBy   = createdBy;

                foreach (PO_Items i in order.PO_Items.ToList())
                {
                    PO_Items iExisting = await db.PO_Items.FindAsync(i.Id);

                    if (iExisting != null)
                    {
                        //update the item
                        iExisting = await UpdatePurchaseOrderItem(iExisting, i);
                    }
                    else
                    {
                        iExisting = await UpdatePurchaseOrderItem(new PO_Items(), i);
                        //create the item
                        db.PO_Items.Add(iExisting);
                    }
                }
                db.Entry(existing).State = EntityState.Modified;
                await db.SaveChangesAsync();

                return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK);
            }
            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Not implemented");
        }

        public async Task<ActionResult> NewRow(int? poId)
        {
            if (!poId.HasValue)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "PO Not found");

            PO_Items i = new PO_Items
            {
                PurchaseOrder = poId.Value,
                TrackInventory = true
            };

            db.PO_Items.Add(i);

            await db.SaveChangesAsync();

            await db.Entry(i).ReloadAsync();

            ViewData["Item.CostType"] = await GenerateSelectListAsync(SelectListType.CostType);

            VM_PO_Items vm = new VM_PO_Items
            {
                Item = i
            };

            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(
                        RenderViewToString("~/Views/Partials/_PurchaseOrderRow.cshtml", vm), new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> RemoveRow(int? id)
        {
            PO_Items i = await db.PO_Items.FindAsync(id);

            if (i != null)
            {
                db.PO_Items.Remove(i);
                await db.SaveChangesAsync();

                return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK, "Deleted");
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Item not found for deletion");
        }

        public async Task<ActionResult> GetDeliveryStatus(int? rowId)
        {
            PO_Items i = await db.PO_Items.FindAsync(rowId);

            if (i != null)
            {
                await db.Entry(i).Collection(c => c.PO_Receiving).LoadAsync();
                //await db.Entry(i).Reference(r => r.Material).LoadAsync();

                foreach (PO_Receiving re in i.PO_Receiving.ToList())
                    await db.Entry(re).Reference(r => r.User).LoadAsync();

                int totalQty = i.Quantity ?? 0;
                int deliveredQty = i.PO_Receiving.Sum(x => x.AmountReceived) ?? 0;
                int remainingQty = totalQty - deliveredQty;

                var dStatus = new
                {
                    remainingQty,
                    orders = i.PO_Receiving
                        .OrderBy(x => x.ReceivedAt)
                        .Select(x => new
                        {
                            ReceivedAt = x.ReceivedAt.Value.ToLocalTime(),
                            x.AmountReceived,
                            receivedBy = x.User.FirstName + " " + x.User.LastName
                        })
                };

                return new JsonResult
                {
                    Data = JsonConvert.SerializeObject(dStatus, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Not Found");
        }

        public async Task<ActionResult> AcceptDelivery(int? itemId, int? quantityReceived)
        {
            PO_Items i = await db.PO_Items.FindAsync(itemId);

            HttpCookie activeUser = HttpContext.Response.Cookies["ActiveUser"];

            if (i != null)
            {
                //make a new receiving record
                db.PO_Receiving.Add(new PO_Receiving
                {
                    Item = itemId,
                    ReceivedAt = DateTime.UtcNow,
                    ReceivedBy = int.Parse(activeUser.Value),
                    AmountReceived = quantityReceived ?? 0
                });

                await db.SaveChangesAsync();

                //receive the delivery (adjust inventory)
                await ReceiveInventoryDelivery(i.Item, quantityReceived.Value);

                //if remaining quantity is 0, set delivered to the current date time
                int remainingQuantity = await GetRemainingQuantity(itemId.Value);

                if (remainingQuantity <= 0)
                {
                    i.Delivered = DateTime.UtcNow;
                    db.Entry(i).State = EntityState.Modified;
                    await db.SaveChangesAsync();
                }

                return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK, "record created");
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Cannot find that item");
        }

        public async Task<ActionResult> ApprovePurchaseOrder(int? poId)
        {
            PO_Orders o = await db.PO_Orders.FindAsync(poId);

            HttpCookie activeUser = HttpContext.Response.Cookies["ActiveUser"];
            int userId = int.Parse(activeUser.Value);

            User u = await db.Users.FindAsync(userId);
            u.GetPOPermission(db);

            ViewData["UserRole"] = u.POPermission;

            if (o != null)
            {
                //determine if requires special approval or not
                if ((o.RequiredSpecialApproval ?? false) && o.SpecialApprovalDate == null)
                {
                    //exit here if insufficient permissions
                    if (u.POPermission != "Special Approver" && u.POPermission != "Admin")
                        return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Requires a special approver, which you are not");

                    o.SpecialApprovalDate = DateTime.UtcNow;
                    o.SpecialApprovalApprover = userId;

                    o.Status = "approved";
                    o.PlacedAt = DateTime.UtcNow;

                    db.Entry(o).State = EntityState.Modified;

                    db.PO_Approvals.Add(new PO_Approvals
                    {
                        ApprovalDate = DateTime.UtcNow,
                        ApprovedBy = userId,
                        PurchaseOrder = poId,
                        WasSpecialApproval = true
                    });
                }
                else
                {
                    //only allow approval if the user is an approver and they are not the requestor
                    if (u.HasApprovalPermissions() && (o.Requestor ?? u.Id) != u.Id)
                    {
                        //vvv does not require special approval
                        db.PO_Approvals.Add(new PO_Approvals
                        {
                            ApprovalDate = DateTime.UtcNow,
                            ApprovedBy = userId,
                            PurchaseOrder = poId,
                            WasSpecialApproval = false
                        });

                        // set the status on the order to approved
                        o.Status = "approved";
                        o.PlacedAt = DateTime.UtcNow;

                        db.Entry(o).State = EntityState.Modified;
                    }
                    else
                    {
                        //potential exit here
                        return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "You must be an approver. Additionally, you cannot approve your own orders.");
                    }
                }

                await db.SaveChangesAsync();

                return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK, "Created");
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Cannot approve a non-existent purchase order");
        }

        public async Task<ActionResult> DenyPurchaseOrder(int? poId, string reason)
        {
            PO_Orders o = await db.PO_Orders.FindAsync(poId);

            HttpCookie activeUser = HttpContext.Response.Cookies["ActiveUser"];
            int userId = int.Parse(activeUser.Value);

            User u = await db.Users.FindAsync(userId);
            u.GetPOPermission(db);

            ViewData["UserRole"] = u.POPermission;

            DateTime actionTime = DateTime.UtcNow;

            if (o != null)
            {
                if (u.HasDenialPermissions())
                {
                    db.PO_Denials.Add(new PO_Denials
                    {
                        DenialDate = actionTime,
                        DeniedBy = userId,
                        PurchaseOrder = poId,
                        Reason = reason ?? ""
                    });

                    // set the status on the order to approved
                    o.Status = "denied";
                    o.RequestDate = null;

                    //invalidate any approvals (if they exist for some reason)
                    await db.Entry(o).Collection(c => c.PO_Approvals).LoadAsync();
                    foreach (PO_Approvals a in o.PO_Approvals.Where(x => x.Invalidated == null))
                    {
                        a.Invalidated = actionTime;
                        db.Entry(a).State = EntityState.Modified;
                    }

                    await db.SaveChangesAsync();

                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK, "Created");
                }
                else
                {
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Insufficient permissions to deny this purchase order");
                }
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Cannot deny a non-existent purchase order");
        }

        //Add an "invalidated" column to PO_Approvals; "invalidate" approvals upon cancelling.
        //will require view alterations to handle the new invalidated state
        public async Task<ActionResult> CancelPurchaseOrder(int? id)
        {
            PO_Orders p = await db.PO_Orders.FindAsync(id);
            DateTime actionTime = DateTime.UtcNow;

            User u = await db.Users.FindAsync(UserSessionHelper.GetActiveUserId(HttpContext));
            u.GetPOPermission(db);

            ViewData["UserRole"] = u.POPermission;

            if (p != null)
            {
                if (u.HasCancelPermissions() || (p.Requestor ?? (u.Id + 1)) == u.Id)
                {
                    HttpCookie activeUser = HttpContext.Response.Cookies["ActiveUser"];

                    int userId = int.Parse(activeUser.Value);

                    p.CancelDate = actionTime;
                    p.CancelledBy = userId;
                    p.Status = "cancelled";

                    //remove all approvals?
                    await db.Entry(p).Collection(c => c.PO_Approvals).LoadAsync();
                    foreach (PO_Approvals a in p.PO_Approvals.Where(x => x.Invalidated == null))
                    {
                        a.Invalidated = actionTime;
                        db.Entry(a).State = EntityState.Modified;
                    }

                    //execute inventory adjustments
                    //need to prompt user: should received inventory be undone? <- in the event that some of the order has been received before being cancelled
                    db.Entry(p).State = EntityState.Modified;
                    await db.SaveChangesAsync();

                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK);
                }
                else
                {
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Insufficient permissions to cancel order");
                }
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Purchase order not found");
        }

        //IMPORTANT NOTE: Special Approval check is done on database side via trigger on PO_Orders
        public async Task<ActionResult> SubmitForApproval(int? id)
        {
            PO_Orders order = await db.PO_Orders.FindAsync(id);

            User u = await db.Users.FindAsync(UserSessionHelper.GetActiveUserId(HttpContext));
            u.GetPOPermission(db);

            ViewData["UserRole"] = u.POPermission;

            if (order != null && u.POPermission != "None")
            {
                order.Status = "pending";
                order.RequestDate = DateTime.UtcNow;
                order.Requestor = u.Id;

                //remove any cancellation status from the order
                order.CancelDate = null;
                order.CancelledBy = null;

                db.Entry(order).State = EntityState.Modified;
                await db.SaveChangesAsync();

                return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK);
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError);
        }
    }
}