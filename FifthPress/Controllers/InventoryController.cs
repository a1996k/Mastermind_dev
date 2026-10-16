using FifthPress.ActionFilters;
using Newtonsoft.Json;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using FifthPress.Models;
using FifthPress.Models.ViewModels;

namespace FifthPress.Controllers
{
    [CookieFilter(Order = 1)]
    [UserPermissions(Order = 2)]
    public class InventoryController : DataController
    {
        public async Task<ActionResult> LoadPullMaterialInfo(int? jobItem)
        {
            JobItem ji = await db.JobItems.FindAsync(jobItem);
            if (ji == null) return HttpNotFound();

            var vmPM = new VM_PullMaterial
            {
                Pulls = new List<PullMaterial>(),
                History = new List<IGrouping<string, InventoryAdjustmentHistory>>()
            };

            await db.Entry(ji).Reference(r => r.Material1).LoadAsync();

            // Process primary material only (finishing materials now have their own endpoint)
            if (ji.Material1 != null)
            {
                if (ji.Material1.StockType == (int)Material.AvailableStockTypes.Sheet)
                {
                    vmPM.Pulls.Add(new PullMaterial
                    {
                        MaterialId = ji.Material1.Id,
                        MaterialCode = ji.Material1.Title,
                        MaterialNotes = ji.Material1.Notes,
                        AmountToPull = ji.BuySheets ?? 0,
                        AmountPulled = 0,
                        FriendlyName = ji.Material1.FriendlyName ?? "",
                        UnitText = "Sheets"
                    });
                }
                else
                {
                    ji.ParseRunSize();
                    int totalInchesNeeded = (int)Math.Ceiling(ji.RunLength);

                    vmPM.Pulls.Add(new PullMaterial
                    {
                        MaterialId = ji.Material1.Id,
                        MaterialCode = ji.Material1.Title,
                        MaterialNotes = ji.Material1.Notes,
                        AmountToPull = totalInchesNeeded,
                        AmountPulled = 0,
                        FriendlyName = ji.Material1.FriendlyName ?? "",
                        UnitText = "Inches"
                    });
                }
            }

            // Get primary material ID for history filtering
            var primaryMaterialIds = new List<int>();
            if (ji.Material1 != null)
            {
                primaryMaterialIds.Add(ji.Material1.Id);
            }

            vmPM.History = db.InventoryAdjustmentHistories
                .Where(x => (x.WasMaterialPull ?? false)
                       && x.JobItem == jobItem
                       && primaryMaterialIds.Contains(x.Material))
                .Include(i => i.Material1)
                .Include(i => i.User)
                .OrderByDescending(x => x.DateUTC)
                .AsEnumerable()
                .GroupBy(x => x.Material1.Title)
                .OrderBy(x => x.Key)
                .ToList();

            foreach (var pull in vmPM.Pulls)
            {
                var materialHistory = vmPM.History.FirstOrDefault(g => g.Key == pull.MaterialCode);
                if (materialHistory != null)
                {
                    bool isLengthBasedMaterial = pull.UnitText == "Inches";

                    decimal pulledAmountInches = materialHistory.Sum(record =>
                    {
                        var feetPulled = (record.OldStockAmount ?? 0) - (record.NewStockAmount ?? 0);
                        return isLengthBasedMaterial ? feetPulled * 12 : feetPulled;
                    });

                    pull.AmountPulled = (int)pulledAmountInches;
                    pull.AmountToPull = Math.Max(0, pull.AmountToPull - (int)pulledAmountInches);

                    if (pull.AmountToPull < 0)
                    {
                        pull.AmountPulled = pull.AmountToPull;
                    }
                }
            }

            var model = vmPM.Pulls.Select(pull => new
            {
                pull.MaterialId,
                MaterialTitle = pull.MaterialCode,
                pull.AmountToPull,
                pull.AmountPulled,
                pull.FriendlyName,
                pull.UnitText,
                History = vmPM.History
                    .Where(g => g.Key == pull.MaterialCode)
                    .SelectMany(g => g.Select(record =>
                    {
                        var difference = (record.OldStockAmount - record.NewStockAmount) ?? 0;
                        decimal pulledQuantity;
                        string quantityUnit;

                        if (record.Material1.StockType == (int)Material.AvailableStockTypes.Sheet)
                        {
                            pulledQuantity = difference;
                            quantityUnit = "Sheets";
                        }
                        else
                        {
                            // Primary material: stored in feet, display in inches
                            pulledQuantity = difference * 12;
                            quantityUnit = "Inches";
                        }

                        return new
                        {
                            PulledBy = record.User != null
                                ? $"{record.User.FirstName} {record.User.LastName}"
                                : "Unknown",
                            PulledDate = record.DateUTC.ToLocalTime().ToString("M/d/yyyy h:mm tt"),
                            PulledQuantity = pulledQuantity,
                            QuantityUnit = quantityUnit
                        };
                    }))
                    .ToList()
            }).ToList();

            return Json(model, JsonRequestBehavior.AllowGet);
        }

        public async Task<ActionResult> HttpAdjustStock(int? materialId, decimal? enteredValue, int? minLevel)
        {
            Material m = await db.Materials.FindAsync(materialId);

            if (m != null)
            {
                if (m.Inventory.HasValue)
                {
                    await db.Entry(m).Reference(r => r.MaterialInventory).LoadAsync();

                    if (enteredValue.HasValue)
                    {
                        m.MaterialInventory.Stock = (int)enteredValue.Value;
                    }

                    if (minLevel.HasValue)
                    {
                        m.MaterialInventory.MinLevel = (int)minLevel.Value;
                    }

                    db.Entry(m).State = System.Data.Entity.EntityState.Modified;

                    await db.SaveChangesAsync();

                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK);
                }
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Cannot find record");
        }

        //adjustment functions
        public async Task<ActionResult> HttpAllocateStock(int? materialId, decimal? enteredValue)
        {
            Material m = await db.Materials.FindAsync(materialId);

            if (m != null)
            {
                if (m.Inventory.HasValue)
                {
                    await db.Entry(m).Reference(r => r.MaterialInventory).LoadAsync();

                    //int curStock = m.MaterialInventory.Stock;
                    int curAllocated = m.MaterialInventory.Allocated ?? 0;

                    //int newStock = curStock - (int)enteredValue;
                    int newAllocated = curAllocated + (int)enteredValue;

                    //m.MaterialInventory.Stock = newStock;
                    m.MaterialInventory.Allocated = newAllocated;

                    db.Entry(m).State = System.Data.Entity.EntityState.Modified;

                    await db.SaveChangesAsync();

                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK);
                }
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Cannot find record");
        }

        public async Task<ActionResult> HttpReceiveStock(int? deliveryId)
        {
            //recordId is the id of the AddedMaterialHistory record
            AddedMaterialInventoryPricingHistory rec = await db.AddedMaterialInventoryPricingHistories.FindAsync(deliveryId);

            if (rec != null)
            {
                HttpCookie userCookie = HttpContext.Response.Cookies.Get("ActiveUser");
                rec.UtcReceivedAt = DateTime.UtcNow;
                rec.ReceivedBy = int.Parse(userCookie.Value); //cant get here without a valid cookie
                db.Entry(rec).State = EntityState.Modified;

                await db.SaveChangesAsync();

                //adjust the inventory
                Material m = await db.Materials.FindAsync(rec.Material);

                if (m != null)
                {
                    await db.Entry(m).Reference(r => r.MaterialInventory).LoadAsync();
                    int currentStock = m.MaterialInventory.Stock;
                    int newStock = currentStock + (int)Math.Ceiling(rec.AmountOrdered ?? 0m);
                    m.MaterialInventory.Stock = newStock;
                    db.Entry(m.MaterialInventory).State = EntityState.Modified;

                    await db.SaveChangesAsync();
                }
            }

            return new JsonResult { Data = "done", JsonRequestBehavior = JsonRequestBehavior.AllowGet };
        }

        public async Task<ActionResult> HttpRemoveStock(int? materialId, decimal? enteredValue)
        {
            Material m = await db.Materials.FindAsync(materialId);

            if (m != null)
            {
                if (m.Inventory.HasValue)
                {
                    await db.Entry(m).Reference(r => r.MaterialInventory).LoadAsync();

                    int currentStock = m.MaterialInventory.Stock;
                    int newStock = currentStock - (int)enteredValue;

                    m.MaterialInventory.Stock = newStock;

                    db.Entry(m).State = System.Data.Entity.EntityState.Modified;

                    await db.SaveChangesAsync();

                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK);
                }
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Error finding record");
        }

        public async Task<ActionResult> HttpAddOrder(int? materialId, decimal? enteredValue, decimal? price, string po, int? ct, int? st)
        {
            Material m = await db.Materials.FindAsync(materialId);

            po = po ?? "";

            if (m != null)
            {
                if (m.Inventory.HasValue)
                {
                    if (price.HasValue)
                    {
                        int nextHistoryId = 0;
                        try { nextHistoryId = await db.AddedMaterialInventoryPricingHistories.MaxAsync(x => x.Id); } catch (Exception) { }
                        nextHistoryId++;

                        db.AddedMaterialInventoryPricingHistories.Add(new AddedMaterialInventoryPricingHistory
                        {
                            Id = nextHistoryId,
                            Material = materialId,
                            Price = price,
                            UtcTimestamp = DateTime.UtcNow,
                            PurchaseOrderNumber = po,
                            CostType = ct,
                            StockType = st,
                            AmountOrdered = enteredValue ?? 0m
                        });

                        await db.SaveChangesAsync();
                    }

                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK);
                }
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Error finding record");
        }

        public async Task<ActionResult> HttpDeallocateStock(int? materialId, decimal? enteredValue)
        {
            Material m = await db.Materials.FindAsync(materialId);

            if (m != null)
            {
                if (m.Inventory.HasValue)
                {
                    await db.Entry(m).Reference(r => r.MaterialInventory).LoadAsync();

                    //int curStock = m.MaterialInventory.Stock;
                    int curAllocated = m.MaterialInventory.Allocated ?? 0;

                    //int newStock = curStock + (int)enteredValue;
                    int newAllocated = curAllocated - (int)enteredValue;

                    //m.MaterialInventory.Stock = newStock;
                    m.MaterialInventory.Allocated = newAllocated;

                    db.Entry(m).State = System.Data.Entity.EntityState.Modified;

                    await db.SaveChangesAsync();

                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK);
                }
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Error finding record");
        }

        public async Task<ActionResult> GetPendingOrders(int? matId)
        {
            var orders =
                await db.PO_Items
                .Where(x => x.PO_Orders.CancelDate == null && x.Delivered == null)
                .ToListAsync();

            foreach (PO_Items i in orders)
            {
                //TODO: add code to load material/item information
                await db.Entry(i).Reference(r => r.MaterialCostType).LoadAsync();
            }

            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(orders, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> GetMaterialInventory(int? id)
        {
            Material material = await db.Materials.FindAsync(id);
            string stockType = string.Empty;

            if (material != null)
            {
                await db.Entry(material).Reference(r => r.MaterialInventory).LoadAsync();

                if (material.StockType.HasValue)
                {
                    await db.Entry(material).Reference(r => r.MaterialStockType).LoadAsync();
                    stockType = material.MaterialStockType.DisplayValue;
                }
            }

            var pendingDeliveries = await db.PO_Items
                .Where(x => x.PO_Orders.CancelDate == null &&
                            x.PO_Orders.PlacedAt != null &&
                            x.Item == material.Id)
                .ToListAsync();

            List<int> pendingDeliveryIds = pendingDeliveries.Select(x => x.Id).ToList();
            int totalOnOrder = pendingDeliveries.Sum(x => x.Quantity) ?? 0;

            int alreadyDelivered = await db.PO_Receiving
                .Where(x => pendingDeliveryIds.Contains(x.Item.Value))
                .SumAsync(y => y.AmountReceived) ?? 0;

            int remainingOnOrder = totalOnOrder - alreadyDelivered;

            // Allocated is computed live from active-job demand (net of pulls) rather than
            // the stored MaterialInventory.Allocated counter, which is only touched by the
            // manual "Allocate Stock" flow and so reads 0 for most materials. This mirrors
            // sp_GetAllMaterialAssignments (which backs the Material Assignments report) so
            // the two screens agree:
            //   primary substrate (BuySheets for sheet stock, else run LENGTH in inches / 12
            //     for roll stock - GetRunSizeLength returns inches, everything else is feet)
            //   + finishing materials (FinalCount)
            //   - amounts already pulled from stock (WasMaterialPull), since Stock is already
            //     post-pull and would otherwise be double-counted.
            // Active jobs only: DueDate set, not complete, not cancelled. Clamped at >= 0.
            const string allocSql = @"
                SELECT CAST(CASE WHEN Net < 0 THEN 0 ELSE Net END AS DECIMAL(18,3)) FROM (
                    SELECT
                        ISNULL((
                            SELECT SUM(CASE
                                        WHEN m.StockType = 3 THEN COALESCE(ji.BuySheets, 0)
                                        ELSE CEILING(CAST(dbo.GetRunSizeLength(ji.MaterialRunSize) AS DECIMAL(10,3)) / 12.0)
                                       END)
                            FROM  JobItems ji
                            JOIN  Jobs j ON ji.Job = j.Id
                            JOIN  Materials m ON ji.Material = m.Id
                            WHERE ji.Material = @mid
                              AND j.DueDate IS NOT NULL AND j.Complete IS NULL AND j.Cancelled IS NULL
                        ), 0)
                        + ISNULL((
                            SELECT SUM(jifm.FinalCount)
                            FROM  JobItemFinishingMaterials jifm
                            JOIN  JobItems ji ON jifm.JobItemId = ji.Id
                            JOIN  Jobs j ON ji.Job = j.Id
                            WHERE jifm.Material = @mid
                              AND j.DueDate IS NOT NULL AND j.Complete IS NULL AND j.Cancelled IS NULL
                        ), 0)
                        - ISNULL((
                            SELECT SUM(iah.OldStockAmount - iah.NewStockAmount)
                            FROM  InventoryAdjustmentHistory iah
                            JOIN  JobItems ji ON iah.JobItem = ji.Id
                            JOIN  Jobs j ON ji.Job = j.Id
                            WHERE iah.Material = @mid AND iah.WasMaterialPull = 1
                              AND j.DueDate IS NOT NULL AND j.Complete IS NULL AND j.Cancelled IS NULL
                        ), 0) AS Net
                ) x";

            decimal computedAllocated = (await db.Database
                .SqlQuery<decimal?>(allocSql, new SqlParameter("@mid", material.Id))
                .FirstOrDefaultAsync()) ?? 0m;

            int allocated = (int)computedAllocated;

            var result = new
            {
                material.Id,
                material.MaterialInventory.Stock,
                Allocated = allocated,
                Available = material.MaterialInventory.Stock - allocated,
                stockType,
                remainingOnOrder,
                MinLevel = material.MaterialInventory.MinLevel ?? 0
            };

            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(result, new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> LoadFinishingMaterialPullInfo(int? jifmId)
        {
            if (!jifmId.HasValue)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, "Missing finishing material ID");

            var jifm = await db.JobItemFinishingMaterials.FindAsync(jifmId.Value);
            if (jifm == null) return HttpNotFound();

            await db.Entry(jifm).Reference(r => r.Material1).LoadAsync();
            if (jifm.Material1 == null) return HttpNotFound();

            // Extract unit text from FinishingFormulaConfiguration JSON
            string unitText = "Units";
            if (!string.IsNullOrEmpty(jifm.FinishingFormulaConfiguration))
            {
                try
                {
                    var config = JsonConvert.DeserializeObject<Dictionary<string, string>>(jifm.FinishingFormulaConfiguration);
                    if (config != null && config.Count > 0)
                    {
                        unitText = config.Keys.First();
                    }
                }
                catch (Exception) { }
            }

            int amountToPull = (int)(jifm.FinalCount ?? 0);

            // Load pull history for this specific material + job item
            var history = db.InventoryAdjustmentHistories
                .Where(x => (x.WasMaterialPull ?? false)
                       && x.JobItem == jifm.JobItemId
                       && x.Material == jifm.Material)
                .Include(i => i.Material1)
                .Include(i => i.User)
                .OrderByDescending(x => x.DateUTC)
                .ToList();

            // Calculate already-pulled amount
            bool isLengthBased = unitText == "Feet";
            decimal pulledAmount = history.Sum(record =>
            {
                var diff = (record.OldStockAmount ?? 0) - (record.NewStockAmount ?? 0);
                return isLengthBased ? diff : diff;
            });

            int amountPulled = (int)pulledAmount;
            amountToPull = Math.Max(0, amountToPull - amountPulled);

            var model = new[]
            {
                new
                {
                    MaterialId = jifm.Material1.Id,
                    MaterialTitle = jifm.Material1.Title,
                    AmountToPull = amountToPull,
                    AmountPulled = amountPulled,
                    FriendlyName = jifm.Material1.FriendlyName ?? "",
                    UnitText = unitText,
                    History = history.Select(record =>
                    {
                        var difference = (record.OldStockAmount - record.NewStockAmount) ?? 0;
                        return new
                        {
                            PulledBy = record.User != null
                                ? $"{record.User.FirstName} {record.User.LastName}"
                                : "Unknown",
                            PulledDate = record.DateUTC.ToLocalTime().ToString("M/d/yyyy h:mm tt"),
                            PulledQuantity = difference,
                            QuantityUnit = unitText
                        };
                    }).ToList()
                }
            };

            return Json(model, JsonRequestBehavior.AllowGet);
        }

        // GET: Inventory
        [CookieFilter(Order = 1)]
        [UserPermissions(Order = 2)]
        public async Task<ActionResult> Index()
        {
            ViewData["CostType"] = await GenerateSelectListAsync(SelectListType.CostType);
            ViewData["StockType"] = await GenerateSelectListAsync(SelectListType.StockType);
            return View();
        }
    }
}