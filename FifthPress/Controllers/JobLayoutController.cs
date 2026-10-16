using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using FifthPress.Extensions;
using System.Data.Entity;
using FifthPress.Models.ViewModels2.LayoutManager;

namespace FifthPress.Controllers
{
    public class JobLayoutController : DataController
    {
        // Generic interface for items that can be laid out
        public interface ILayoutItem
        {
            int Id { get; }
            string Length { get; }
            string Width { get; }
            string Description { get; }
            int? Material { get; }
            int? Press { get; }
            int? TotalQuantity { get; }
            int? Up { get; }
            bool? UpValueLocked { get; }

            // Layout settings - will be loaded from ItemLayoutSettings
            ItemLayoutSetting LayoutSettings { get; set; }
        }

        // Wrapper classes
        public class JobItemWrapper : ILayoutItem
        {
            private readonly JobItem _item;
            public ItemLayoutSetting LayoutSettings { get; set; }

            public JobItemWrapper(JobItem item, ItemLayoutSetting layoutSettings = null)
            {
                _item = item;
                LayoutSettings = layoutSettings ?? new ItemLayoutSetting();
            }

            public int Id => _item.Id;
            public string Length => _item.Length;
            public string Width => _item.Width;
            public string Description => _item.Description;
            public int? Material => _item.Material;
            public int? Press => _item.Press;
            public int? TotalQuantity => _item.TotalQuantity;
            public int? Up => _item.Up;
            public bool? UpValueLocked => _item.UpValueLocked;

            // Access to original item
            public JobItem JobItem => _item;
        }

        public class QuoteItemWrapper : ILayoutItem
        {
            private readonly QuoteItem _item;
            public ItemLayoutSetting LayoutSettings { get; set; }

            public QuoteItemWrapper(QuoteItem item, ItemLayoutSetting layoutSettings = null)
            {
                _item = item;
                LayoutSettings = layoutSettings ?? new ItemLayoutSetting();
            }

            public int Id => _item.Id;
            public string Length => _item.Length;
            public string Width => _item.Width;
            public string Description => _item.Description;
            public int? Material => _item.Material;
            public int? Press => _item.Press;
            public int? TotalQuantity => _item.TotalQuantity;
            public int? Up => _item.Up;
            public bool? UpValueLocked => _item.UpValueLocked;

            // Access to original item
            public QuoteItem QuoteItem => _item;
        }

        // Unified controller methods
        public async Task<ActionResult> ViewJobLayout(int? jobItemId)
        {
            var jobItem = await GetJobItemAsync(jobItemId);
            if (jobItem == null)
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "unable to find that job item");

            var layoutSettings = await GetOrCreateLayoutSettingsAsync(jobItemId: jobItemId, null);
            var layoutItem = new JobItemWrapper(jobItem, layoutSettings);
            var model = await CreateLayoutModelAsync(layoutItem, jobItem.Job1, "Job");

            var res = new { html = RenderViewToString("~/Views/JobLayout/ViewLayoutNEW.cshtml", model) };
            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(res, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> ViewQuoteLayout(int? quoteItemId)
        {
            var quoteItem = await GetQuoteItemAsync(quoteItemId);
            if (quoteItem == null)
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "unable to find that quote item");

            var layoutSettings = await GetOrCreateLayoutSettingsAsync(null, quoteItemId: quoteItemId);
            var layoutItem = new QuoteItemWrapper(quoteItem, layoutSettings);
            var model = await CreateLayoutModelAsync(layoutItem, quoteItem.Quote1, "Quote");

            var res = new { html = RenderViewToString("~/Views/JobLayout/ViewLayoutNEW.cshtml", model) };
            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(res, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        [HttpPost]
        public async Task<ActionResult> SaveMainItemData(int quantity, int up, string parentType, int parentId)
        {
            bool upValueLock = false;

            try
            {
                if (parentType == "Job")
                {
                    var jobItem = await db.JobItems.FindAsync(parentId);
                    if (jobItem != null)
                    {
                        jobItem.Quantity = quantity;

                        if (jobItem.Up != up)
                        {
                            jobItem.UpValueLocked = true;
                            upValueLock = true;
                        }
                        jobItem.Up = up;

                        await db.SaveChangesAsync();
                    }
                }
                else if (parentType == "Quote")
                {
                    var quoteItem = await db.QuoteItems.FindAsync(parentId);
                    if (quoteItem != null)
                    {
                        quoteItem.Quantity = quantity;

                        if (quoteItem.Up != up)
                        {
                            upValueLock = true;
                            quoteItem.UpValueLocked = true;
                        }
                        quoteItem.Up = up;

                        await db.SaveChangesAsync();
                    }
                }

                return Json(new { upValue = up, upLocked = upValueLock, qty = quantity, success = true, message = "Main item data saved successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error saving main item data: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<ActionResult> SaveLayoutSettings(int? jobItemId, int? quoteItemId,
        decimal leftSideGuide, decimal rightSideGuide, decimal xGutter, decimal yGutter,
        decimal gripper, decimal tail, bool swapDimensions, bool dutchCut,
        bool linkSideGuides, bool linkGutters, decimal materialLength)
        {
            try
            {
                var settings = await GetOrCreateLayoutSettingsAsync(jobItemId, quoteItemId);

                // Update layout settings values (matching existing field names)
                settings.LeftSideGuide = leftSideGuide;
                settings.RightSideGuide = rightSideGuide;
                settings.XGutter = xGutter;
                settings.YGutter = yGutter;
                settings.Gripper = gripper;
                settings.Tail = tail;
                settings.DutchCut = dutchCut;

                // Add new fields for linking
                settings.LinkSideGuides = linkSideGuides;
                settings.LinkGutters = linkGutters;

                settings.UpdatedAt = DateTime.UtcNow;

                // Add to context if new
                if (settings.Id == 0)
                {
                    db.ItemLayoutSettings.Add(settings);
                }

                // Update MaterialRunSize on the Job/QuoteItem
                if (jobItemId.HasValue)
                {
                    var jobItem = await db.JobItems.FindAsync(jobItemId.Value);
                    if (jobItem != null)
                    {
                        jobItem.SetRollStockLength(materialLength);
                        jobItem.ParseRunSize(); // Update the parsed length/width properties
                    }
                }
                else if (quoteItemId.HasValue)
                {
                    var quoteItem = await db.QuoteItems.FindAsync(quoteItemId.Value);
                    if (quoteItem != null)
                    {
                        quoteItem.SetRollStockLength(materialLength);
                        quoteItem.ParseRunSize(); // Update the parsed length/width properties
                    }
                }

                try
                {
                    await db.SaveChangesAsync();
                }
                catch (System.Data.Entity.Infrastructure.DbUpdateException dbEx) when (IsUniqueConstraintViolation(dbEx))
                {
                    // A concurrent SaveLayoutSettings request inserted a row first.
                    // Retry as an update against the row that now exists.
                    MastermindLogger.Log(LogLevel.Warning,
                        $"SaveLayoutSettings unique-constraint race detected (jobItemId={jobItemId}, quoteItemId={quoteItemId}). Retrying as update.");

                    db.Entry(settings).State = EntityState.Detached;

                    var existing = await db.ItemLayoutSettings
                        .FirstOrDefaultAsync(ils => ils.JobItemId == jobItemId && ils.QuoteItemId == quoteItemId);

                    if (existing == null)
                    {
                        return Json(new { success = false, message = "Error saving to database: unique-violation retry could not locate the existing row." });
                    }

                    existing.LeftSideGuide = leftSideGuide;
                    existing.RightSideGuide = rightSideGuide;
                    existing.XGutter = xGutter;
                    existing.YGutter = yGutter;
                    existing.Gripper = gripper;
                    existing.Tail = tail;
                    existing.DutchCut = dutchCut;
                    existing.LinkSideGuides = linkSideGuides;
                    existing.LinkGutters = linkGutters;
                    existing.UpdatedAt = DateTime.UtcNow;

                    try
                    {
                        await db.SaveChangesAsync();
                    }
                    catch (Exception retryEx)
                    {
                        return Json(new { success = false, message = "Error saving to database (retry failed): " + retryEx.Message });
                    }
                }
                catch (Exception e)
                {
                    // Consider logging the exception details
                    return Json(new { success = false, message = "Error saving to database: " + e.Message });
                }

                return Json(new { dutchCutEnabled = dutchCut, success = true, message = "Layout settings saved successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error saving layout settings: " + ex.Message });
            }
        }

        private async Task<ItemLayoutSetting> GetOrCreateLayoutSettingsAsync(int? jobItemId, int? quoteItemId)
        {
            // Try to find existing settings
            var settings = await db.ItemLayoutSettings
                .FirstOrDefaultAsync(ils => ils.JobItemId == jobItemId && ils.QuoteItemId == quoteItemId);

            if (settings == null)
            {
                // Get Press defaults
                Press press = null;

                if (jobItemId.HasValue)
                {
                    var jobItem = await db.JobItems
                        .Include(ji => ji.Press1)
                        .FirstOrDefaultAsync(ji => ji.Id == jobItemId.Value);
                    press = jobItem?.Press1;
                }
                else if (quoteItemId.HasValue)
                {
                    var quoteItem = await db.QuoteItems
                        .Include(qi => qi.Press1)
                        .FirstOrDefaultAsync(qi => qi.Id == quoteItemId.Value);
                    press = quoteItem?.Press1;
                }

                // Helper method to safely parse string to decimal
                decimal ParseDecimal(string value) => decimal.TryParse(value, out var result) ? result : 0;

                // Create new settings with Press defaults or fallback defaults
                settings = new ItemLayoutSetting
                {
                    JobItemId = jobItemId,
                    QuoteItemId = quoteItemId,
                    LeftSideGuide = ParseDecimal(press.SideGuides ?? "0"),
                    RightSideGuide = ParseDecimal(press.SideGuides ?? "0"),
                    XGutter = ParseDecimal(press.MarginRight ?? "0"),
                    YGutter = ParseDecimal(press.MarginTop ?? "0"),
                    Gripper = ParseDecimal(press.Gripper ?? "0"),
                    Tail = ParseDecimal(press.Tail ?? "0"),
                    SwapDimensions = false,
                    DutchCut = false,
                    LinkSideGuides = false,
                    LinkGutters = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
            }

            return settings;
        }

        private static bool IsUniqueConstraintViolation(System.Data.Entity.Infrastructure.DbUpdateException ex)
        {
            Exception current = ex;
            while (current != null)
            {
                if (current is System.Data.SqlClient.SqlException sqlEx)
                {
                    return sqlEx.Number == 2601 || sqlEx.Number == 2627;
                }
                current = current.InnerException;
            }
            return false;
        }

        private async Task<JobItem> GetJobItemAsync(int? jobItemId)
        {
            return await db.JobItems
                .Include(ji => ji.Material1)
                .Include(ji => ji.Press1)
                .Include(ji => ji.Job1)
                .Include(ji => ji.SpeedDenominator)
                .Include("JobItemServices.Service.ServiceCategory")
                .SingleAsync(ji => ji.Id == jobItemId && ji.Material != null);
        }

        private async Task<QuoteItem> GetQuoteItemAsync(int? quoteItemId)
        {
            return await db.QuoteItems
                .Include(qi => qi.Material1)
                .Include(qi => qi.Press1)
                .Include(qi => qi.Quote1)
                .Include(qi => qi.SpeedDenominator)
                .Include("QuoteItemServices.Service.ServiceCategory")
                .SingleAsync(qi => qi.Id == quoteItemId && qi.Material != null);
        }

        private async Task<VM_UnifiedLayoutPlanning> CreateLayoutModelAsync(ILayoutItem layoutItem, object parent, string parentType)
        {
            // Load related entities based on type
            await LoadRelatedEntitiesAsync(layoutItem, parentType);

            // Process material and run size calculations
            var (runSizeLength, runSizeWidth) = CalculateRunSize(layoutItem, parentType);

            var model = new VM_UnifiedLayoutPlanning
            {
                ParentObject = parent,
                ParentType = parentType,
                VM_ItemMaterialLayouts = new List<VM_UnifiedItemMaterialLayout>
                {
                    CreateMaterialLayout(layoutItem, runSizeLength, runSizeWidth, parentType)
                }
            };

            return model;
        }

        private async Task LoadRelatedEntitiesAsync(ILayoutItem layoutItem, string parentType)
        {
            if (!layoutItem.Material.HasValue) return;

            if (parentType == "Job")
            {
                var jobWrapper = layoutItem as JobItemWrapper;
                var jobItem = jobWrapper.JobItem;

                await db.Entry(jobItem.Material1).Reference(r => r.MaterialStockType).LoadAsync();

                foreach (var service in jobItem.JobItemServices.ToList())
                {
                    await db.Entry(service).Reference(r => r.Service).LoadAsync();
                    await db.Entry(service.Service).Reference(r => r.ServiceCategory).LoadAsync();
                }
            }
            else
            {
                var quoteWrapper = layoutItem as QuoteItemWrapper;
                var quoteItem = quoteWrapper.QuoteItem;

                await db.Entry(quoteItem.Material1).Reference(r => r.MaterialStockType).LoadAsync();

                foreach (var service in quoteItem.QuoteItemServices.ToList())
                {
                    await db.Entry(service).Reference(r => r.Service).LoadAsync();
                    await db.Entry(service.Service).Reference(r => r.ServiceCategory).LoadAsync();
                }
            }
        }

        private (string length, string width) CalculateRunSize(ILayoutItem layoutItem, string parentType)
        {
            // Get material info based on type
            var material = GetMaterialFromItem(layoutItem, parentType);

            // Parse run size
            ParseRunSize(layoutItem, parentType);

            var runLength = GetRunLength(layoutItem, parentType);
            var runWidth = GetRunWidth(layoutItem, parentType);

            string runSizeLength = runLength.ToString();
            string runSizeWidth = runWidth.ToString();

            // Handle roll stock calculations
            if (material.StockType == (int)Material.AvailableStockTypes.RollStock)
            {
                // For roll stock, calculate which orientation requires less material
                // Access layout settings through the LayoutSettings property
            }
            else
            {
                // For sheet stock, apply the 2.5x multiplier
                SetMaterialLength(layoutItem, ParseDecimal(layoutItem.Length) * 2.5m, parentType);
            }

            return (runSizeLength, runSizeWidth);
        }

        // Helper methods for type-specific operations
        private dynamic GetMaterialFromItem(ILayoutItem layoutItem, string parentType)
        {
            if (parentType == "Job")
                return ((JobItemWrapper)layoutItem).JobItem.Material1;
            else
                return ((QuoteItemWrapper)layoutItem).QuoteItem.Material1;
        }

        private void ParseRunSize(ILayoutItem layoutItem, string parentType)
        {
            if (parentType == "Job")
                ((JobItemWrapper)layoutItem).JobItem.ParseRunSize();
            else
                ((QuoteItemWrapper)layoutItem).QuoteItem.ParseRunSize();
        }

        private decimal GetRunLength(ILayoutItem layoutItem, string parentType)
        {
            if (parentType == "Job")
                return ((JobItemWrapper)layoutItem).JobItem.RunLength;
            else
                return ((QuoteItemWrapper)layoutItem).QuoteItem.RunLength;
        }

        private decimal GetRunWidth(ILayoutItem layoutItem, string parentType)
        {
            if (parentType == "Job")
                return ((JobItemWrapper)layoutItem).JobItem.RunWidth;
            else
                return ((QuoteItemWrapper)layoutItem).QuoteItem.RunWidth;
        }

        private void SetMaterialLength(ILayoutItem layoutItem, decimal length, string parentType)
        {
            if (parentType == "Job")
                ((JobItemWrapper)layoutItem).JobItem.MaterialLength = length;
            else
                ((QuoteItemWrapper)layoutItem).QuoteItem.MaterialLength = length;
        }

        private decimal ParseDecimal(string value) =>
            decimal.TryParse(value ?? "0", out var result) ? result : 0m;

        private VM_UnifiedItemMaterialLayout CreateMaterialLayout(ILayoutItem layoutItem, string runSizeLength, string runSizeWidth, string parentType)
        {
            var material = GetMaterialFromItem(layoutItem, parentType);

            // Check if ItemLayoutSettings exist, otherwise use legacy values
            string leftSideGuide, rightSideGuide, xGutter, yGutter, gripper, tail;
            bool swapOverride, dutchCut;

            // Check if ItemLayoutSettings has been persisted to database (Id > 0)
            // If Id is 0, it means GetOrCreateLayoutSettingsAsync created it in-memory with Press defaults
            // but it hasn't been saved yet, so we should use the legacy item fields instead
            bool hasPersistedLayoutSettings = layoutItem.LayoutSettings != null && layoutItem.LayoutSettings.Id > 0;

            if (hasPersistedLayoutSettings)
            {
                // Use ItemLayoutSettings (authoritative source)
                leftSideGuide = layoutItem.LayoutSettings.LeftSideGuide.ToString();
                rightSideGuide = layoutItem.LayoutSettings.RightSideGuide.ToString();
                xGutter = layoutItem.LayoutSettings.XGutter.ToString();
                yGutter = layoutItem.LayoutSettings.YGutter.ToString();
                gripper = layoutItem.LayoutSettings.Gripper.ToString();
                tail = layoutItem.LayoutSettings.Tail.ToString();
                dutchCut = layoutItem.LayoutSettings.DutchCut;

                MastermindLogger.Log(LogLevel.Info, $"Using persisted ItemLayoutSettings - Id: {layoutItem.LayoutSettings.Id}");
            }
            else
            {
                MastermindLogger.Log(LogLevel.Info, $"No persisted ItemLayoutSettings found (Id: {layoutItem.LayoutSettings?.Id}), using legacy item fields");

                // Fall back to legacy fields - need to cast to access type-specific properties
                if (parentType == "Job" && layoutItem is JobItemWrapper jobItemWrapper)
                {
                    var jobItem = jobItemWrapper.JobItem;

                    leftSideGuide = jobItem.SideGuides ?? "0";        // Use SideGuides field
                    rightSideGuide = jobItem.SideGuides ?? "0";       // Use SideGuides field
                    xGutter = jobItem.MarginXR ?? "0";                // Legacy X gutter field
                    yGutter = jobItem.MarginYT ?? "0";                // Legacy Y gutter field
                    gripper = jobItem.Gripper ?? "0";                 // Use 0 if NULL
                    tail = jobItem.Tail ?? "0";                       // Use 0 if NULL
                }
                else if (parentType == "Quote" && layoutItem is QuoteItemWrapper quoteItemWrapper)
                {
                    var quoteItem = quoteItemWrapper.QuoteItem;

                    // Log the raw values first
                    MastermindLogger.Log(LogLevel.Info, $"Raw QuoteItem values - MarginXR: '{quoteItem?.MarginXR}', MarginYB: '{quoteItem?.MarginYB}', SideGuides: '{quoteItem?.SideGuides}', Gripper: '{quoteItem?.Gripper}', Tail: '{quoteItem?.Tail}'");

                    leftSideGuide = quoteItem.SideGuides ?? "0";      // Use SideGuides field
                    rightSideGuide = quoteItem.SideGuides ?? "0";     // Use SideGuides field
                    xGutter = quoteItem.MarginXR ?? "0";              // Press MarginRight (X gutter)
                    yGutter = quoteItem.MarginYT ?? "0";              // Press MarginBottom (Y gutter)
                    gripper = quoteItem.Gripper ?? "0";               // Use press value or 0 if not specified
                    tail = quoteItem.Tail ?? "0";                     // Use press value or 0 if not specified

                    MastermindLogger.Log(LogLevel.Info, $"Final ViewModel values - XGutter: '{xGutter}', YGutter: '{yGutter}', LeftSideGuide: '{leftSideGuide}', RightSideGuide: '{rightSideGuide}', Gripper: '{gripper}', Tail: '{tail}'");
                }
                else
                {
                    // Fallback defaults if casting fails
                    leftSideGuide = "0";
                    rightSideGuide = "0";
                    xGutter = "0";
                    yGutter = "0";
                    gripper = "0";
                    tail = "0";
                    swapOverride = false;
                }

                dutchCut = false; // Default for legacy items
            }

            return new VM_UnifiedItemMaterialLayout
            {
                MaterialLength = runSizeLength,
                MaterialWidth = runSizeWidth,
                ItemLength = layoutItem.Length,
                ItemWidth = layoutItem.Width,
                Quantity = layoutItem.TotalQuantity?.ToString() ?? "",

                // Use calculated values (from ItemLayoutSettings or legacy fallback)
                LeftSideGuide = leftSideGuide,
                RightSideGuide = rightSideGuide,
                XGutter = xGutter,
                YGutter = yGutter,
                Gripper = gripper,
                Tail = tail,
                DutchCut = dutchCut,

                Up = layoutItem.Up?.ToString() ?? "",
                StockType = material.MaterialStockType.Title,
                CssSelector = "#material_0",
                MaterialId = "material_0",
                ItemId = layoutItem.Id,
                ItemDescription = layoutItem.Description ?? "No description set",
                PressId = layoutItem.Press,
                UpValueLocked = layoutItem.UpValueLocked ?? false,
                ParentType = parentType,
                VM_SelectedItemServices = CreateServicesList(layoutItem, parentType)
            };
        }

        private List<VM_UnifiedSelectedItemService> CreateServicesList(ILayoutItem layoutItem, string parentType)
        {
            if (parentType == "Job")
            {
                var jobItem = ((JobItemWrapper)layoutItem).JobItem;
                return jobItem.JobItemServices.Select(z => new VM_UnifiedSelectedItemService
                {
                    Category = z.Service.ServiceCategory.Title,
                    ServiceIndex = z.Ordinal ?? 0,
                    Title = z.Service.Title,
                    ItemService = z
                }).ToList();
            }
            else
            {
                var quoteItem = ((QuoteItemWrapper)layoutItem).QuoteItem;
                return quoteItem.QuoteItemServices.Select(z => new VM_UnifiedSelectedItemService
                {
                    Category = z.Service.ServiceCategory.Title,
                    ServiceIndex = z.Ordinal,
                    Title = z.Service.Title,
                    ItemService = z
                }).ToList();
            }
        }
    }
}