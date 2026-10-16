using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Threading.Tasks;
using FifthPress.Models.ViewModels;
using FifthPress.Models;
using System.Configuration;
using System.Activities;
using FifthPress.Models.ControllerModels;

namespace FifthPress.Controllers
{
    public class MaterialController : DataController
    {
        public async Task<ActionResult> GetInks()
        {
            //all ink categories regardless of press - they follow the "<Press> Ink" naming convention (e.g. "Vanguard Ink", "Tauro Ink")
            List<int> inkCatIds =
                await db.MaterialCategories
                    .Where(x => !x.Deleted.HasValue && x.Title.EndsWith(" Ink"))
                    .Select(x => x.Id)
                    .ToListAsync();

            var inks =
                await db.Materials
                    .Where(x => !x.Deleted.HasValue && x.MaterialCategory_Id.HasValue && inkCatIds.Contains(x.MaterialCategory_Id.Value))
                    .OrderBy(x => x.MaterialCategory_Id)
                    .ThenBy(x => x.Id)
                    .Select(x => new
                    {
                        x.Id,
                        x.Title,
                        x.FriendlyName,
                        CategoryTitle = x.MaterialCategory.Title,
                        MaterialInventory = new { Stock = (int?)x.MaterialInventory.Stock }
                    })
                    .ToListAsync();

            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(inks, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> GetBasePrice(int? materialId)
        {
            Material m = await db.Materials.FindAsync(materialId);

            if (m != null)
            {
                string price = (m.Cost ?? 0m).ToString("N2");

                return new JsonResult
                {
                    Data = new
                    {
                        price
                    },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Material not found");
        }

        public async Task<ActionResult> UpdateMaterialConfig(VM_MaterialConfig config, int? itemId, string itemType)
        {
            if (config != null && itemId.HasValue && !String.IsNullOrEmpty(itemType))
            {
                switch (itemType.ToUpper())
                {
                    case "QUOTE":
                        QuoteItem qi = await db.QuoteItems.FindAsync(itemId);

                        bool qsheetsOverridden =
                            (qi.UserSheetOverride ?? false) ||
                            qi.Sheets != config.Sheets ||
                            qi.TotalScrapSheets != config.ScrapSheets ||
                            qi.BuySheets != config.BuySheets;

                        if (qi != null)
                        {
                            qi.UserSheetOverride = qsheetsOverridden;
                            qi.MaterialRunSize = String.Format("{0}x{1}", config.RunLength, config.RunWidth);
                            qi.Up = config.Up;
                            qi.Out = config.Out;
                            qi.PressColorProfileFront = config.ColorFrontProfile;
                            qi.PressColorCountFront = config.ColorFrontCount;
                            qi.PressColorNotesFront = config.ColorFrontNotes;
                            qi.PressColorProfileBack = config.ColorBackProfile;
                            qi.PressColorCountBack = config.ColorBackCount;
                            qi.PressColorNotesBack = config.ColorBackNotes;
                            qi.Sheets = config.Sheets;
                            qi.TotalScrapSheets = config.ScrapSheets;
                            qi.ScrapSheets = 0;
                            qi.VariableScrapSheets = 0;
                            qi.RunSheets = config.RunSheets;
                            qi.BuySheets = config.BuySheets;

                            //if the user overrides sheets, then set variable to false
                            if ((qi.UserSheetOverride ?? false))
                            {
                                qi.VariableScrapSheets = 0;
                                qi.ScrapSheets = 0;
                            }
                            qi.DoubleHit = config.DoubleInk;
                            qi.Collate = config.Collate;
                            qi.Staple = config.Staple;
                            qi.Numbering = config.Numbering;
                            qi.MaterialCost = config.MaterialCost;

                            db.Entry(qi).State = EntityState.Modified;
                            await db.SaveChangesAsync();
                        }
                        break;

                    case "JOB":
                        JobItem ji = await db.JobItems.FindAsync(itemId);

                        bool jsheetsOverridden =
                            (ji.UserSheetOverride ?? false) ||
                            ji.Sheets != config.Sheets ||
                            ji.TotalScrapSheets != config.ScrapSheets ||
                            ji.BuySheets != config.BuySheets;

                        if (ji != null)
                        {
                            ji.UserSheetOverride = jsheetsOverridden;

                            ji.MaterialRunSize = String.Format("{0}x{1}", config.RunLength, config.RunWidth);
                            ji.Up = config.Up;
                            ji.Out = config.Out;
                            ji.PressColorProfileFront = config.ColorFrontProfile;
                            ji.PressColorCountFront = config.ColorFrontCount;
                            ji.PressColorNotesFront = config.ColorFrontNotes;
                            ji.PressColorProfileBack = config.ColorBackProfile;
                            ji.PressColorCountBack = config.ColorBackCount;
                            ji.PressColorNotesBack = config.ColorBackNotes;
                            ji.Sheets = config.Sheets;
                            ji.TotalScrapSheets = config.ScrapSheets;
                            ji.RunSheets = config.RunSheets;
                            ji.BuySheets = config.BuySheets;

                            if ((ji.UserSheetOverride ?? false))
                            {
                                ji.VariableScrapSheets = 0;
                                ji.ScrapSheets = 0;
                            }
                            ji.DoubleHit = config.DoubleInk;
                            ji.Staple = config.Staple;
                            ji.Collate = config.Collate;
                            ji.Numbering = config.Numbering;
                            ji.MaterialCost = config.MaterialCost;

                            db.Entry(ji).State = EntityState.Modified;
                            await db.SaveChangesAsync();
                        }
                        break;

                    default:
                        return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, "Invalid item type");
                }
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK, "Function finished");
        }

        public async Task<ActionResult> PriceOverride(int? id, string newPrice, string recordType)
        {
            if (!id.HasValue || String.IsNullOrEmpty(newPrice) || String.IsNullOrEmpty(recordType))
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, "Item row id, new price and record type required");

            try
            {
                decimal newPriceParsed = decimal.Parse(newPrice);
                decimal stockTypeAmount = 0m;
                decimal materialCostScaled = 0m;

                switch (recordType.ToUpper())
                {
                    case "QUOTE":
                        QuoteItem qi = await db.QuoteItems.SingleAsync(x => x.Id == id.Value);
                        await db.Entry(qi).Reference(r => r.Material1).LoadAsync();
                        //because we're overriding the price, we can assume a material has been selected
                        await db.Entry(qi.Material1).Reference(r => r.MaterialStockType).LoadAsync();

                        switch ((Material.AvailableStockTypes)qi.Material1.MaterialStockType.Id)
                        {
                            case Material.AvailableStockTypes.Sheet:
                                stockTypeAmount = (qi.Sheets ?? 0) + (qi.ScrapSheets ?? 0);
                                break;

                            case Material.AvailableStockTypes.RollStock:
                            case Material.AvailableStockTypes.CutVinyl:
                                qi.ParseRunSize();
                                stockTypeAmount = qi.MaterialLength / 12m;
                                break;

                            default:
                                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "invalid stock type");
                        }

                        try
                        {
                            qi.MaterialCost = Decimal.Parse(newPrice);
                        }
                        catch (Exception)
                        {
                            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Invalid price format");
                        }

                        //used for the scaled material cost
                        switch ((Material.MaterialCostTypes)qi.Material1.CostType)
                        {
                            case Material.MaterialCostTypes.Piece:
                                materialCostScaled = qi.MaterialCost.Value; //.Value because try/catch with http return above
                                break;

                            case Material.MaterialCostTypes.Hundred:
                                materialCostScaled = qi.MaterialCost.Value / 100m;
                                break;

                            case Material.MaterialCostTypes.Thousand:
                                materialCostScaled = qi.MaterialCost.Value / 1000m;
                                break;

                            case Material.MaterialCostTypes.Foot:
                                break;
                        }
                        qi.MaterialCostPerUnit = materialCostScaled;
                        qi.MaterialCostPerUnitFormatted = qi.MaterialCostPerUnit.Value.ToString("C");

                        db.Entry(qi).State = EntityState.Modified;
                        break;

                    case "JOB":
                        JobItem ji = await db.JobItems.SingleAsync(x => x.Id == id.Value);
                        await db.Entry(ji).Reference(r => r.Material1).LoadAsync();
                        await db.Entry(ji.Material1).Reference(r => r.MaterialStockType).LoadAsync();

                        switch ((Material.AvailableStockTypes)ji.Material1.MaterialStockType.Id)
                        {
                            case Material.AvailableStockTypes.Sheet:
                                stockTypeAmount = (ji.Sheets ?? 0) + (ji.ScrapSheets ?? 0);
                                break;

                            case Material.AvailableStockTypes.RollStock:
                            case Material.AvailableStockTypes.CutVinyl:
                                ji.ParseRunSize();
                                stockTypeAmount = ji.MaterialLength / 12m;
                                break;

                            default:
                                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "invalid stock type");
                        }

                        try
                        {
                            ji.MaterialCost = Decimal.Parse(newPrice);
                        }
                        catch (Exception)
                        {
                            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Invalid price format");
                        }

                        //used for the scaled material cost
                        switch ((Material.MaterialCostTypes)ji.Material1.CostType)
                        {
                            case Material.MaterialCostTypes.Piece:
                                materialCostScaled = ji.MaterialCost.Value; //.Value because try/catch with http return above
                                break;

                            case Material.MaterialCostTypes.Hundred:
                                materialCostScaled = ji.MaterialCost.Value / 100m;
                                break;

                            case Material.MaterialCostTypes.Thousand:
                                materialCostScaled = ji.MaterialCost.Value / 1000m;
                                break;

                            case Material.MaterialCostTypes.Foot:
                                break;
                        }
                        ji.MaterialCostPerUnit = materialCostScaled;
                        ji.MaterialCostPerUnitFormatted = ji.MaterialCostPerUnit.Value.ToString("C");

                        db.Entry(ji).State = EntityState.Modified;
                        break;

                    default:
                        return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, "Must specify quote or job");
                }

                await db.SaveChangesAsync();

                return new JsonResult { Data = "Done", JsonRequestBehavior = JsonRequestBehavior.AllowGet };
            }
            catch (Exception e)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, e.Message);
            }
        }

        public async Task<ActionResult> NewMaterial()
        {
            ViewData["Material.MaterialCategory_Id"] = await GenerateSelectListAsync(SelectListType.MaterialCategories);
            ViewData["Material.StockType"] = await GenerateSelectListAsync(SelectListType.StockType);
            ViewData["Material.CostType"] = await GenerateSelectListAsync(SelectListType.CostType);
            ViewData["Material.FinishingMaterialFormula"] = await GenerateSelectListAsync(SelectListType.FinishingMaterialFormulas);

            // Get finishing category IDs for JavaScript
            var finishingCategoryIds = await db.MaterialCategories
                .Where(x => x.Deleted == null && x.MaterialType == (int)MaterialCategory.MaterialTypes.Finishing)
                .Select(x => x.Id)
                .ToListAsync();
            ViewData["FinishingCategoryIds"] = string.Join(",", finishingCategoryIds);
            ViewData["ExistingFinishingFormula"] = "";

            return new JsonResult { Data = JsonConvert.SerializeObject(RenderViewToString("~/Views/Material/GetMaterialForEdit.cshtml", new VM_Material { Material = new Material { NumberOfParts = 1, IncludeOnGolfOrder = false } })), JsonRequestBehavior = JsonRequestBehavior.AllowGet };
        }

        //sql procedure
        public async Task<ActionResult> DynamicMaterialAddition(NewMaterial_CM material)
        {
            try
            {
                db.Live_InsertNewMaterial(material.Title, material.Category, material.Caliper, material.Cost, material.CostType, material.Size, material.Quantity, material.FriendlyName);
                await db.SaveChangesAsync();

                // If a finishing formula was selected, create the pivot record
                if (material.FinishingMaterialFormula.HasValue && material.FinishingMaterialFormula.Value > 0)
                {
                    // Get the newly created material by title (since stored proc doesn't return ID)
                    var newMaterial = await db.Materials
                        .Where(x => x.Title == material.Title && x.MaterialCategory_Id == material.Category)
                        .OrderByDescending(x => x.Id)
                        .FirstOrDefaultAsync();

                    if (newMaterial != null)
                    {
                        // Check if category is a finishing type
                        await db.Entry(newMaterial).Reference(r => r.MaterialCategory).LoadAsync();
                        if (newMaterial.MaterialCategory?.MaterialType == (int)MaterialCategory.MaterialTypes.Finishing)
                        {
                            db.FinishingMaterialFormulaPivots.Add(new FinishingMaterialFormulaPivot
                            {
                                Material = newMaterial.Id,
                                FinishingFormula = material.FinishingMaterialFormula.Value
                            });
                            await db.SaveChangesAsync();
                        }
                    }
                }

                return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK, "Material Added");
            }
            catch (Exception)
            {
                //unknown error
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Unknown error while adding material");
            }
        }

        public async Task<ActionResult> SaveMaterial(Material theItem)
        {
            if (theItem == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            // Get FinishingMaterialFormula from form (it's submitted as theItem[FinishingMaterialFormula])
            int? FinishingMaterialFormula = null;
            var formulaValue = Request.Form["theItem[FinishingMaterialFormula]"];
            if (!string.IsNullOrEmpty(formulaValue) && int.TryParse(formulaValue, out int parsedFormula))
            {
                FinishingMaterialFormula = parsedFormula;
            }

            var m = db.Materials.Find(theItem.Id);
            bool isNewMaterial = m == null;
            int materialId;

            if (isNewMaterial)
            {
                //add new material
                db.Materials.Add(theItem);
                await db.SaveChangesAsync();
                await db.Entry(theItem).ReloadAsync();

                materialId = theItem.Id;

                //get the next material inventory record
                int nextMaterialInventoryId = 0;
                try
                {
                    nextMaterialInventoryId = await db.MaterialInventories.MaxAsync(x => x.Id);
                }
                catch (Exception)
                {
                    //ok; move on
                }

                nextMaterialInventoryId++;

                //new material inventory record
                db.MaterialInventories.Add(new MaterialInventory
                {
                    Id = nextMaterialInventoryId,
                    Stock = 0,
                    Allocated = 0
                });

                theItem.Inventory = nextMaterialInventoryId;

                db.Entry(theItem).State = EntityState.Modified;
            }
            else
            {
                //save existing material
                m = UpdateMaterialRecord(m, theItem);
                db.Entry(m).State = EntityState.Modified;
                materialId = m.Id;
            }

            await db.SaveChangesAsync();

            // Handle FinishingMaterialFormulaPivot for finishing materials
            System.Diagnostics.Debug.WriteLine($"SaveMaterial: FinishingMaterialFormula = {FinishingMaterialFormula}, materialId = {materialId}");

            if (FinishingMaterialFormula.HasValue && FinishingMaterialFormula.Value > 0)
            {
                System.Diagnostics.Debug.WriteLine($"SaveMaterial: Formula has value, checking category...");

                // Check if this is a finishing material category
                var material = await db.Materials.FindAsync(materialId);
                await db.Entry(material).Reference(r => r.MaterialCategory).LoadAsync();

                System.Diagnostics.Debug.WriteLine($"SaveMaterial: MaterialType = {material.MaterialCategory?.MaterialType}");

                if (material.MaterialCategory?.MaterialType == (int)MaterialCategory.MaterialTypes.Finishing)
                {
                    System.Diagnostics.Debug.WriteLine($"SaveMaterial: Is finishing material, creating pivot...");

                    // Check for existing pivot record
                    var existingPivot = await db.FinishingMaterialFormulaPivots
                        .FirstOrDefaultAsync(x => x.Material == materialId);

                    if (existingPivot != null)
                    {
                        // Update existing pivot
                        existingPivot.FinishingFormula = FinishingMaterialFormula.Value;
                        db.Entry(existingPivot).State = EntityState.Modified;
                        System.Diagnostics.Debug.WriteLine($"SaveMaterial: Updated existing pivot");
                    }
                    else
                    {
                        // Create new pivot record
                        db.FinishingMaterialFormulaPivots.Add(new FinishingMaterialFormulaPivot
                        {
                            Material = materialId,
                            FinishingFormula = FinishingMaterialFormula.Value
                        });
                        System.Diagnostics.Debug.WriteLine($"SaveMaterial: Added new pivot");
                    }

                    await db.SaveChangesAsync();
                    System.Diagnostics.Debug.WriteLine($"SaveMaterial: Pivot saved");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"SaveMaterial: No formula selected or value is 0");
            }

            return new JsonResult { Data = "done", JsonRequestBehavior = JsonRequestBehavior.AllowGet };
        }

        public async Task<ActionResult> GetMaterialForEdit(int? id)
        {
            if (id.HasValue)
            {
                Material m = await db.Materials.FindAsync(id);

                if (m != null)
                {
                    ViewData["Material.MaterialCategory_Id"] = await GenerateSelectListAsync(SelectListType.MaterialCategories, m, "MaterialCategory_Id");
                    ViewData["Material.StockType"] = await GenerateSelectListAsync(SelectListType.StockType, m, "StockType");
                    ViewData["Material.CostType"] = await GenerateSelectListAsync(SelectListType.CostType, m, "CostType");
                    ViewData["Material.FinishingMaterialFormula"] = await GenerateSelectListAsync(SelectListType.FinishingMaterialFormulas);

                    // Get finishing category IDs for JavaScript
                    var finishingCategoryIds = await db.MaterialCategories
                        .Where(x => x.Deleted == null && x.MaterialType == (int)MaterialCategory.MaterialTypes.Finishing)
                        .Select(x => x.Id)
                        .ToListAsync();
                    ViewData["FinishingCategoryIds"] = string.Join(",", finishingCategoryIds);

                    // Get existing finishing formula if any
                    var existingPivot = await db.FinishingMaterialFormulaPivots
                        .FirstOrDefaultAsync(x => x.Material == m.Id);
                    ViewData["ExistingFinishingFormula"] = existingPivot?.FinishingFormula.ToString() ?? "";

                    await db.Entry(m).Reference(r => r.MaterialCategory).LoadAsync();
                    await db.Entry(m).Reference(r => r.MaterialInventory).LoadAsync();

                    m.QuickCategory = m.QuickCategory ?? false;

                    return new JsonResult { Data = JsonConvert.SerializeObject(RenderViewToString("~/Views/Material/GetMaterialForEdit.cshtml", new VM_Material { Material = m })), JsonRequestBehavior = JsonRequestBehavior.AllowGet };
                }
            }

            return null;
        }

        //amount will be negative if removing stock
        public async Task<ActionResult> AdjustMaterialInventory(int? material, int? stockAdjustment, int? allocatedAdjustment)
        {
            if (!material.HasValue || !stockAdjustment.HasValue || !allocatedAdjustment.HasValue)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            Material m = await db.Materials.FindAsync(material);

            if (m != null)
            {
                if (m.Inventory.HasValue)
                {
                    await db.Entry(m).Reference(r => r.MaterialInventory).LoadAsync();

                    m.MaterialInventory.Stock += stockAdjustment.Value;
                    m.MaterialInventory.Allocated += allocatedAdjustment.Value;

                    db.Entry(m.MaterialInventory).State = EntityState.Modified;

                    await db.SaveChangesAsync();
                }
            }

            return new JsonResult { Data = "done", JsonRequestBehavior = JsonRequestBehavior.AllowGet };
        }

        public async Task<ActionResult> LoadMaterialsByCategory(int? cat)
        {
            cat = cat ?? -1;

            if (cat == -1)
            {
                //quick category materials
                var mQc = await db.Materials
                    .Include(i => i.MaterialInventory)
                    .Include(i => i.MaterialCategory)
                    .Include(i => i.MaterialCostType)
                    .Where(x => (x.QuickCategory ?? false))
                    .ToListAsync();

                foreach (var m in mQc)
                    m.ParseDimensions();

                var mQcRes = mQc.Select(x => new
                {
                    x.Size,
                    x.Title,
                    x.Id,
                    x.Cost,
                    CostFormatted = (x.Cost ?? 0m).ToString("C"),
                    CostType = x.MaterialCostType.Title,
                    x.Notes,
                    x.MaterialInventory.Stock,
                    x.Caliper
                }).ToList();

                return new JsonResult
                {
                    Data = JsonConvert.SerializeObject(mQcRes, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
            else
            {
                //materials by Category
                var mC = await db.Materials.Include(i => i.MaterialInventory).Include(i => i.MaterialCategory).Include(i => i.MaterialCostType).Where(x => x.MaterialCategory_Id == cat).ToListAsync();

                foreach (var m in mC)
                    m.ParseDimensions();

                var mCRes = mC.Select(x => new
                {
                    x.Size,
                    x.Title,
                    x.Id,
                    x.Cost,
                    CostFormatted = (x.Cost ?? 0m).ToString("C"),
                    CostType = x.MaterialCostType.Title,
                    x.Notes,
                    x.MaterialInventory.Stock,
                    x.Caliper
                }).ToList();

                return new JsonResult
                {
                    Data = JsonConvert.SerializeObject(mCRes, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
        }
    }
}