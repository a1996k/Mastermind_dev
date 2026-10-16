using Amazon.Runtime.Internal.Util;
using FifthPress.Models;
using FifthPress.Models.API.BambooHR;
using FifthPress.Models.Calculations;
using FifthPress.Models.Dashboard;
using FifthPress.Models.ViewModels;
using FifthPress.Models.ViewModels.CardViewModels;
using FifthPress.Models.ViewModels.NewPriceDisplay;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Data.Entity.Core.Objects;
using System.Diagnostics;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Windows;

namespace FifthPress.Controllers
{
    public class DataController : Controller
    {
        protected readonly FifthPressEntities db;
        protected readonly printersplanEntities pdb;

        private readonly string ellipsis = "...";

        protected bool HasPendingQACheck()
        {
            try
            {
                int userId = UserSessionHelper.GetActiveUserId(HttpContext);

                try
                {
                    JobTimingSR jt = db.JobTimingSRs.Single(x => x.UserId == userId && x.EndTimeUTC == null &&
                        (x.JobItemService1.Service.Title == "Print" || (x.JobItemService1.RequiresQualityCheck ?? false)));

                    try
                    {
                        // Get today's date at midnight to compare only the date part
                        DateTime today = DateTime.UtcNow.Date;

                        // Attempt to find a passed QA check for today, by this user, for this service
                        QACheck check = db.QAChecks.OrderByDescending(x => x.FirstApprovalTime).FirstOrDefault(x => x.JobItemServiceId == jt.JobItemService &&
                                                                         (x.Passed ?? false) &&
                                                                         x.FirstApprover == userId &&
                                                                         DbFunctions.TruncateTime(x.FirstApprovalTime) == today);

                        // If no matching check is found today, return true (QA check is required)
                        if (check == null)
                        {
                            return true;
                        }

                        // If a matching check is found for today, return false (no QA check needed)
                        return false;
                    }
                    catch (Exception)
                    {
                        // No check found or database error, prompt for QA check
                        return true;
                    }
                }
                catch (Exception)
                {
                    // No active job found that matches the conditions
                    return false;
                }
            }
            catch (Exception)
            {
                // Failure to retrieve user ID
                return false;
            }
        }

        protected bool IsPendingQAForPrintService()
        {
            try
            {
                int userId = UserSessionHelper.GetActiveUserId(HttpContext);
                JobTimingSR jt = db.JobTimingSRs.SingleOrDefault(x =>
                    x.UserId == userId && x.EndTimeUTC == null &&
                    x.JobItemService1.Service.Title == "Print");
                return jt != null;
            }
            catch { return false; }
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);

            // Populate the ViewData with the time tracking data
            var timeTrackingModel = new VM_GlobalTimeTracking
            {
                ActiveTimeEntries = GetActiveTimeEntries(),  // Retrieve or mock active entries here
                HasPendingQACheck = HasPendingQACheck(),
                QAUsers = FilterByUserFlag(GenerateSelectList(SelectListType.Users), UserFlags.IsQAChecker).ToList()
            };
            ViewData["TimeTrackingModel"] = timeTrackingModel;

            int userId = -1;

            try
            {
                //display name for sidemenu
                userId = UserSessionHelper.GetActiveUserId(HttpContext);
            }
            catch (Exception e) { }

            User u = db.Users.Find(userId);

            ViewData["LoggedInUserDisplayName"] = (u == null ? "" : (u.FirstName + " " + u.LastName));
        }

        private List<VM_ActiveTimeEntry> GetActiveTimeEntries()
        {
            try
            {
                int userId = UserSessionHelper.GetActiveUserId(HttpContext);

                List<VM_ActiveTimeEntry> timeEntries =
                    db.JobTimingSRs
                        .Where(x => x.UserId == userId && x.EndTimeUTC == null)
                        .Select(x => new VM_ActiveTimeEntry
                        {
                            RecordId = x.Id,
                            JobId = x.Job ?? 0,
                            JobItemId = x.JobItem ?? 0,
                            JobItemServiceId = x.JobItemService ?? 0,
                            StartTime = x.StartTimeUTC.Value,
                            ServiceTitle = x.JobItemService1.Title,
                            JobNumber = x.Job1.JobNumber,
                            ItemNumber = x.JobItem1.Ordinal ?? 0,
                            ItemDescription = x.JobItem1.Description
                        })
                        .ToList();

                foreach (VM_ActiveTimeEntry v in timeEntries)
                    v.ItemDescription = TruncateString(v.ItemDescription);

                return timeEntries;
            }
            catch (Exception)
            {
                return new List<VM_ActiveTimeEntry>();
            }
        }

        protected async Task RunMaterialPull(int materialId, int quantity, int? jobId, int? jobItem)
        {
            await PullMaterial(materialId, quantity, jobId, jobItem);
        }
        public async Task<ActionResult> Refresh(DateTime? LastUpdate)
        {
            List<Job> jobs = new List<Job>();
            DateTime? timeToSendBack = null;
            bool shouldRefresh = false;

            if (!LastUpdate.HasValue)
            {
                //initial request (records are loaded on page load so this first one is simply setting the client time)
                aux_LastJobUpdate last = await db.aux_LastJobUpdate.FirstAsync();
                DateTime lastUpdateFromDb = last.LastUpdateDateTime.Value;

                timeToSendBack = lastUpdateFromDb;
            }
            else
            {
                aux_LastJobUpdate last = await db.aux_LastJobUpdate.FirstAsync();
                //last datetime is in utc; ours will be as well
                DateTime lastUpdateFromClient = LastUpdate.Value;
                DateTime lastUpdateFromDb = last.LastUpdateDateTime.Value; //could be null; make sure it always has a value on db side

                if (lastUpdateFromClient < lastUpdateFromDb)
                {
                    //update time to send back
                    timeToSendBack = lastUpdateFromDb;

                    shouldRefresh = true;
                }
                else
                {
                    //send back the provided time
                    timeToSendBack = LastUpdate.Value;
                }
            }

            //set job stages
            jobs.ForEach(x => x.SetCurrentJobStatus());

            var jReturn = new
            {
                lastUpdate = timeToSendBack,
                shouldRefresh = shouldRefresh
            };

            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(jReturn, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public DataController()
        {
            db = new FifthPressEntities();
            db.Configuration.LazyLoadingEnabled = false;
            pdb = new printersplanEntities();
            pdb.Configuration.LazyLoadingEnabled = false;
        }

        //charged price, internal cost (cp - ic)/(ic) x 100
        public decimal CalculatePercentDifference(decimal price, decimal cost, bool asMarkup = false)
        {
            if (asMarkup)
                return (price - cost) / (price == 0m ? 1m : price) * 100;
            else
                return
                    (price - cost) / (cost == 0m ? 1m : cost) * 100;
        }

        public IEnumerable<SelectListItem> FilterByUserFlag(IEnumerable<SelectListItem> toFilter, UserFlags flag)
        {
            List<int> userIds = new List<int>();

            // Get the user ids based on the flag
            switch (flag)
            {
                case UserFlags.IsProductionEmployee:
                    userIds = db.Users
                                      .Where(x => (x.IsProductionEmployee ?? false))
                                      .Select(x => x.Id)
                                      .ToList();
                    break;

                case UserFlags.IsProjectManager:
                    userIds = db.Users
                                      .Where(x => (x.IsProjectManager ?? false))
                                      .Select(x => x.Id)
                                      .ToList();
                    break;

                case UserFlags.IsSpecialApprover:
                    userIds = db.Users
                                      .Where(x => (x.IsSpecialApprover ?? false))
                                      .Select(x => x.Id)
                                      .ToList();
                    break;

                case UserFlags.IsQAChecker:
                    userIds = db.Users
                                      .Where(x => (x.IsQAChecker ?? false))
                                      .Select(x => x.Id)
                                      .ToList();
                    break;

                default:
                    return Enumerable.Empty<SelectListItem>(); // Return an empty list if no flag matches
            }

            // Filter the toFilter collection based on the retrieved user IDs
            var filteredList = toFilter.Where(item => userIds.Contains(int.Parse(item.Value)));

            return filteredList;
        }

        //currently only filters by a single flag
        public async Task<IEnumerable<SelectListItem>> FilterByUserFlagAsync(IEnumerable<SelectListItem> toFilter, UserFlags flag)
        {
            List<int> userIds = new List<int>();

            // Get the user ids based on the flag
            switch (flag)
            {
                case UserFlags.IsProductionEmployee:
                    userIds = await db.Users
                                      .Where(x => (x.IsProductionEmployee ?? false))
                                      .Select(x => x.Id)
                                      .ToListAsync();
                    break;

                case UserFlags.IsProjectManager:
                    userIds = await db.Users
                                      .Where(x => (x.IsProjectManager ?? false))
                                      .Select(x => x.Id)
                                      .ToListAsync();
                    break;

                case UserFlags.IsSpecialApprover:
                    userIds = await db.Users
                                      .Where(x => (x.IsSpecialApprover ?? false))
                                      .Select(x => x.Id)
                                      .ToListAsync();
                    break;

                case UserFlags.IsQAChecker:
                    userIds = await db.Users
                                      .Where(x => (x.IsQAChecker ?? false))
                                      .Select(x => x.Id)
                                      .ToListAsync();

                    break;

                default:
                    return Enumerable.Empty<SelectListItem>(); // Return an empty list if no flag matches
            }

            // Filter the toFilter collection based on the retrieved user IDs
            var filteredList = toFilter.Where(item => userIds.Contains(int.Parse(item.Value)));

            return filteredList;
        }

        private DateTime GetCurrentPeriodStart()
        {
            try
            {
                string dateString = System.IO.File.ReadAllLines(Server.MapPath("~/PayPeriod.txt"))[0];
                DateTime payPeriodDate = DateTime.Parse(dateString);

                return payPeriodDate;
            }
            catch (Exception)
            {
                throw;
            }
        }

        private async Task UpdateQuotePrice(int id)
        {
            decimal runningTotal = 0m;

            Quote q = await db.Quotes.FindAsync(id);

            foreach (QuoteParentItemTotals_Result r in db.QuoteParentItemTotals(id))
                runningTotal += (r.Total_Price ?? 0m);

            q.Price = runningTotal;

            db.Entry(q).State = EntityState.Modified;

            await db.SaveChangesAsync();
        }

        protected async Task UpdateJobPrice(int id)
        {
            decimal runningTotal = 0m;

            Job j = await db.Jobs.FindAsync(id);

            foreach (JobParentItemTotals_Result r in db.JobParentItemTotals(id))
                runningTotal += (r.Total_Price ?? 0m);

            j.Price = runningTotal;

            db.Entry(j).State = EntityState.Modified;

            await db.SaveChangesAsync();
        }

        public string ConvertSecondsToHhMmSs(long totalSeconds)
        {
            long hours = totalSeconds / 3600;
            long minutes = (totalSeconds % 3600) / 60;
            long seconds = totalSeconds % 60;

            return string.Format("{0:D2}:{1:D2}:{2:D2}", hours, minutes, seconds);
        }

        public string ConvertSecondsToHhMmSs(int totalSeconds)
        {
            int hours = totalSeconds / 3600;
            int minutes = (totalSeconds % 3600) / 60;
            int seconds = totalSeconds % 60;

            return string.Format("{0:D2}:{1:D2}:{2:D2}", hours, minutes, seconds);
        }

        public string DecimalHoursToHumanReadable(decimal? hours)
        {
            // Check if hours is null
            if (hours == null)
            {
                return "No time provided";
            }

            // Separate the integer hours from the fractional part
            int wholeHours = (int)hours.Value;
            int minutes = (int)((hours.Value - wholeHours) * 60);

            // Format the result as human-readable time
            return $"{wholeHours} Hour{(wholeHours != 1 ? "s" : "")} {minutes} Minute{(minutes != 1 ? "s" : "")}";
        }

        public long DecimalHoursToMilliseconds(decimal? hours)
        {
            return (long)((hours ?? 0m) * 3600m * 1000m);
        }

        public string DecimalHoursToFormatted(decimal? hours)
        {
            if (hours == null)
            {
                return "";
            }

            // Capture the raw value
            decimal raw = hours.Value;

            // Determine if negative
            bool isNegative = (raw < 0);

            // Work with absolute value to get the correct minutes
            decimal absVal = Math.Abs(raw);

            int wholeHours = (int)absVal;                  // e.g., 1 for -1.25
            decimal fractionalHours = absVal - wholeHours; // e.g., 0.25 for -1.25
            int minutes = (int)(fractionalHours * 60);     // e.g., 15 for 0.25

            // Retain your existing "one minute minimum" for positive decimals.
            // If hours > 0, but it’s less than 1 minute, force "0:01".
            if (raw > 0m && wholeHours == 0 && minutes == 0)
            {
                return "0:01";
            }

            // Include '-' if negative, otherwise empty
            string sign = isNegative ? "-" : "";

            // Combine into H:MM format
            return $"{sign}{wholeHours}:{minutes:D2}";
        }

        public decimal FormattedTimeToDecimalHours(string formattedTime)
        {
            if (formattedTime == null)
            {
                return 0.00m;
            }
            else
            {
                string[] timeParts = formattedTime.Split(':');
                int hours = int.Parse(timeParts[0]);
                int minutes = int.Parse(timeParts[1]);

                decimal fractionalHours = (decimal)minutes / 60;

                return hours + fractionalHours;
            }
        }

        public async Task<string> GetSpeedValueText(int speedId)
        {
            try
            {
                SpeedDenominator sd = await db.SpeedDenominators.SingleAsync(x => x.Id == speedId);
                return sd.Text;
            }
            catch (Exception)
            {
                return "None";
            }
        }

        public ActionResult AjaxDecimalHoursToFormatted(decimal decimalTime)
        {
            return new JsonResult { Data = DecimalHoursToFormatted(decimalTime), JsonRequestBehavior = JsonRequestBehavior.AllowGet };
        }

        public ActionResult AjaxFormattedTimeToDecimal(string formattedTime)
        {
            return new JsonResult { Data = FormattedTimeToDecimalHours(formattedTime), JsonRequestBehavior = JsonRequestBehavior.AllowGet };
        }

        protected bool ShouldProcessToRunSize(decimal matLength, decimal matWidth, decimal runSizeLength, decimal runSizeWidth)
        {
            if ((runSizeLength != matLength) || (runSizeWidth != matWidth))
                return true;

            return false;
        }

        protected int ProcessToRunSize(decimal matLength, decimal matWidth, decimal runSizeLength, decimal runSizeWidth)
        {
            int res1 =
                Convert.ToInt32(
                    Math.Floor((matLength / (runSizeLength == 0 ? 1 : runSizeLength)) * (matWidth / (runSizeWidth == 0 ? 1 : runSizeWidth)))
                );

            int res2 =
                Convert.ToInt32(
                        Math.Floor((matLength / (runSizeWidth == 0 ? 1 : runSizeWidth)) * (matWidth / (runSizeLength == 0 ? 1 : runSizeLength)))
                    );

            if (res1 > res2)
                return res1;
            else
                return res2;
        }

        public async Task AllocateMaterial(int? id, int? quantity, int? job, int? jobItem)
        {
            Material m = await db.Materials.FindAsync(id);

            if (m != null)
            {
                quantity = quantity ?? 0;

                InventoryAdjustmentHistory i = new InventoryAdjustmentHistory
                {
                    Material = m.Id,
                    DateUTC = DateTime.UtcNow,
                    AdjustedBy = UserSessionHelper.GetActiveUserId(HttpContext)
                };

                await db.Entry(m).Reference(r => r.MaterialInventory).LoadAsync();

                i.OldStockAmount = m.MaterialInventory.Stock;
                i.OldAllocatedAmount = m.MaterialInventory.Allocated;

                m.MaterialInventory.Allocated = (m.MaterialInventory.Allocated ?? 0) + quantity.Value;

                i.NewStockAmount = m.MaterialInventory.Stock;
                i.NewAllocatedAmount = m.MaterialInventory.Allocated;

                i.WasMaterialPull = false;

                i.Job = job;
                i.JobItem = jobItem;

                db.InventoryAdjustmentHistories.Add(i);
                db.Entry(m).State = EntityState.Modified;

                await db.SaveChangesAsync();
            }
        }
        public async Task SlotMaterial(int? id, int? quantity)
        {
            Material m = await db.Materials.FindAsync(id);

            if (m != null)
            {
                quantity = quantity ?? 0;

                InventoryAdjustmentHistory i = new InventoryAdjustmentHistory
                {
                    Material = m.Id,
                    DateUTC = DateTime.UtcNow,
                    AdjustedBy = UserSessionHelper.GetActiveUserId(HttpContext)
                };

                await db.Entry(m).Reference(r => r.MaterialInventory).LoadAsync();

                i.OldStockAmount = m.MaterialInventory.Stock;
                i.OldAllocatedAmount = m.MaterialInventory.Allocated;

                m.MaterialInventory.Stock += quantity.Value;

                i.NewStockAmount = m.MaterialInventory.Stock;
                i.NewAllocatedAmount = m.MaterialInventory.Allocated;

                i.WasMaterialPull = false;

                i.Job = null;
                i.JobItem = null;

                db.InventoryAdjustmentHistories.Add(i);
                db.Entry(m).State = EntityState.Modified;

                await db.SaveChangesAsync();
            }
        }

        public async Task PullMaterial(int? id, int? quantity, int? job, int? jobItem)
        {
            Material m = await db.Materials.FindAsync(id);

            await db.Entry(m).Reference(r => r.MaterialStockType).LoadAsync();

            if (m != null)
            {
                quantity = quantity ?? 0;

                InventoryAdjustmentHistory i = new InventoryAdjustmentHistory
                {
                    Material = m.Id,
                    DateUTC = DateTime.UtcNow,
                    AdjustedBy = UserSessionHelper.GetActiveUserId(HttpContext)
                };

                // Check if this is a finishing material with length units (Feet)
                bool isLengthBasedFinishingMaterial = false;
                if (jobItem.HasValue)
                {
                    var finishingMaterial = await db.JobItemFinishingMaterials
                        .FirstOrDefaultAsync(x => x.JobItem.Id == jobItem.Value && x.Material == m.Id);

                    if (finishingMaterial != null && !string.IsNullOrEmpty(finishingMaterial.FinishingFormulaConfiguration))
                    {
                        try
                        {
                            var config = JsonConvert.DeserializeObject<Dictionary<string, string>>(finishingMaterial.FinishingFormulaConfiguration);
                            if (config != null && config.Keys.Any(k => k == "Feet"))
                            {
                                isLengthBasedFinishingMaterial = true;
                            }
                        }
                        catch (Exception)
                        {
                            // If JSON parsing fails, treat as non-finishing material
                        }
                    }
                }

                // Only convert inches to feet for PRIMARY materials (roll stock/vinyl that are NOT finishing materials)
                // - Primary materials: user enters inches, need to convert to feet
                // - Finishing materials with "Feet": user enters feet, already in correct unit (no conversion)
                // - Finishing materials with "Pieces": no conversion
                // - Sheets: no conversion
                if (m.MaterialStockType.Title != "Sheet" && !isLengthBasedFinishingMaterial)
                {
                    quantity = (int)Math.Ceiling(quantity.Value / 12m);
                }

                await db.Entry(m).Reference(r => r.MaterialInventory).LoadAsync();

                i.OldStockAmount = m.MaterialInventory.Stock;
                i.OldAllocatedAmount = m.MaterialInventory.Allocated;

                m.MaterialInventory.Stock -= quantity.Value;

                i.NewStockAmount = m.MaterialInventory.Stock;
                i.NewAllocatedAmount = m.MaterialInventory.Allocated;

                i.WasMaterialPull = true;

                i.Job = job;
                i.JobItem = jobItem;

                db.InventoryAdjustmentHistories.Add(i);
                db.Entry(m).State = EntityState.Modified;

                await db.SaveChangesAsync();
            }
        }

        public async Task<List<Service>> GetPrepressServicesAsync()
        {
            return await db.Services.Where(x => x.Deleted == null && x.ServiceCategory.ServiceType1.Title == "Prepress").Include(i => i.ServiceCategory).ToListAsync();
        }

        public async Task<List<string>> GetPrepressServiceCategoriesAsync()
        {
            List<string> categories =
                await db.ServiceCategories
                    .Where(x => x.Deleted == null && x.ServiceType1.Title == "Prepress")
                    .Select(x => x.Title)
                    .Distinct()
                    .ToListAsync();

            return categories;
        }

        public async Task<List<string>> GetPrepressServiceCategoriesAsync(bool keys)
        {
            List<string> categories =
                await db.ServiceCategories
                    .Where(x => x.Deleted == null && x.ServiceType1.Title == "Prepress")
                    .Select(x => x.Title)
                    .Distinct()
                    .ToListAsync();

            categories.ForEach(x => x.SanitizeForKey());
            return categories;
        }

        public async Task<List<Service>> GetCuttingServicesAsync()
        {
            return
                await db.Services
                    .Where(x => x.Deleted == null && x.ServiceCategory.ServiceType1.Title == "Finishing" && (x.IsCuttingService ?? false))
                    .Include(i => i.ServiceCategory)
                    .ToListAsync();
        }

        public async Task<List<string>> GetCuttingServiceCategoriesAsync()
        {
            List<string> categories =
                await db.ServiceCategories
                    .Where(x => x.Deleted == null && x.ServiceType1.Title == "Finishing" && x.Services.Any(y => (y.IsCuttingService ?? false)))
                    .Select(x => x.Title)
                    .Distinct()
                    .ToListAsync();

            return categories;
        }

        public async Task<List<string>> GetCuttingServiceCategoriesAsync(bool keys)
        {
            List<string> categories =
                await db.ServiceCategories
                    .Where(x => x.Deleted == null && x.ServiceType1.Title == "Finishing" && x.Services.Any(y => (y.IsCuttingService ?? false)))
                    .Select(x => x.Title)
                    .Distinct()
                    .ToListAsync();

            categories.ForEach(x => x.SanitizeForKey());
            return categories;
        }

        public async Task<List<Service>> GetFinishingServicesAsync()
        {
            return await db.Services.Where(x => x.Deleted == null && x.ServiceCategory.ServiceType1.Title == "Finishing" && !(x.IsCuttingService ?? false)).Include(i => i.ServiceCategory).ToListAsync();
        }

        public async Task<List<string>> GetFinishingServiceCategoriesAsync()
        {
            List<string> categories =
                await db.ServiceCategories
                    .Where(x => x.Deleted == null && x.ServiceType1.Title == "Finishing" && !x.Services.Any(y => (y.IsCuttingService ?? false)))
                    .Select(x => x.Title)
                    .Distinct()
                    .ToListAsync();

            return categories;
        }

        public async Task<List<string>> GetFinishingServiceCategoriesAsync(bool keys)
        {
            List<string> categories =
                await db.ServiceCategories
                    .Where(x => x.Deleted == null && x.ServiceType1.Title == "Finishing" && !x.Services.Any(y => (y.IsCuttingService ?? false)))
                    .Select(x => x.Title)
                    .Distinct()
                    .ToListAsync();

            categories.ForEach(x => x.SanitizeForKey());
            return categories;
        }

        public async Task<List<Service>> GetServicesAsync()
        {
            return await db.Services.Where(x => x.Deleted == null).Include(i => i.ServiceCategory).ToListAsync();
        }

        public async Task<List<Material>> GetAllMaterialsAsync()
        {
            return await db.Materials.Where(x => x.Deleted == null).Include(i => i.MaterialInventory).ToListAsync();
        }

        public async Task<List<Material>> GetPrintingMaterialsAsync()
        {
            return await db.Materials.Where(x => x.Deleted == null && x.MaterialCategory.MaterialType == (int)MaterialCategory.MaterialTypes.Printing).ToListAsync();
        }

        public async Task<List<Material>> GetFinishingMaterialsAsync()
        {
            // Eager-load nav properties in one query.
            // Before: per-material Reference(MaterialCategory) + Collection(FinishingMaterialFormulaPivots)
            //         + Reference(FinishingMaterialFormula) = 3×N round-trips (~25s for ~500 materials).
            // After:  one SQL with JOINs; loop is in-memory only.
            List<Material> fMaterials = await db.Materials
                .Where(x => x.Deleted == null && x.MaterialCategory.MaterialType == (int)MaterialCategory.MaterialTypes.Finishing)
                .Include(m => m.MaterialCategory)
                .Include(m => m.FinishingMaterialFormulaPivots.Select(fmp => fmp.FinishingMaterialFormula))
                .ToListAsync();

            foreach (Material m in fMaterials)
            {
                try
                {
                    FinishingMaterialFormulaPivot mp = m.FinishingMaterialFormulaPivots.Single(x => x.Material == m.Id);
                    m.ConfigLabel = JsonConvert.DeserializeObject<Dictionary<string, string>>(mp.FinishingMaterialFormula.DefaultConfig).Keys.First().Replace("_", " ");
                    m.ConfigValue = JsonConvert.DeserializeObject<Dictionary<string, string>>(mp.FinishingMaterialFormula.DefaultConfig).Values.First();
                }
                catch (Exception)
                {
                    m.ConfigLabel = "";
                    m.ConfigValue = "";
                }
            }

            return fMaterials;
        }

        public async Task<List<MaterialCategory>> GetPrintingMaterialCategoryObjsAsync()
        {
            List<MaterialCategory> cats = await db.MaterialCategories.Where(x => x.Deleted == null && x.Materials.Count > 0 && x.MaterialType == (int)MaterialCategory.MaterialTypes.Printing).OrderBy(x => x.Title).ToListAsync();
            cats.Add(new MaterialCategory { Id = -1, Title = "Quick Items", MaterialType = (int)MaterialCategory.MaterialTypes.Printing, ListOrder = -1 });
            return cats;
        }

        public async Task<List<MaterialCategory>> GetFinishingMaterialCategoryObjsAsync()
        {
            List<MaterialCategory> cats = await db.MaterialCategories.Where(x => x.Deleted == null && x.Materials.Count > 0 && x.MaterialType == (int)MaterialCategory.MaterialTypes.Finishing).OrderBy(x => x.Title).ToListAsync();
            cats.Add(new MaterialCategory { Id = -1, Title = "Quick Items", MaterialType = (int)MaterialCategory.MaterialTypes.Finishing, ListOrder = -1 });
            return cats;
        }

        public async Task<List<string>> GetPrintingMaterialCategoriesAsync()
        {
            List<string> categories = await db.MaterialCategories.Where(x => x.Deleted == null && x.Materials.Count > 0 && x.MaterialType == (int)MaterialCategory.MaterialTypes.Printing).Select(x => x.Title).ToListAsync();
            return categories;
        }

        public async Task<List<string>> GetFinishingMaterialCategoriesAsync()
        {
            List<string> categories = await db.MaterialCategories.Where(x => x.Deleted == null && x.Materials.Count > 0 && x.MaterialType == (int)MaterialCategory.MaterialTypes.Finishing).Select(x => x.Title).ToListAsync();
            return categories;
        }

        public async Task<List<string>> GetPrintingMaterialCategoriesAsync(bool keys)
        {
            List<string> categories = await db.MaterialCategories.Where(x => x.Deleted == null && x.MaterialType == (int)MaterialCategory.MaterialTypes.Printing).Select(x => x.Title).Distinct().ToListAsync();
            //add quick category
            categories.Add("Quick Items");
            categories.ForEach(x => x.SanitizeForKey());
            return categories;
        }

        public async Task<List<string>> GetFinishingMaterialCategoriesAsync(bool keys)
        {
            List<string> categories = await db.MaterialCategories.Where(x => x.Deleted == null && x.MaterialType == (int)MaterialCategory.MaterialTypes.Finishing).Select(x => x.Title).Distinct().ToListAsync();
            categories.ForEach(x => x.SanitizeForKey());
            return categories;
        }

        public async Task<List<Press>> GetPressesAsync()
        {
            var presses = await db.Presses.Where(x => x.Deleted == null && x.StockType == (int)Material.AvailableStockTypes.Sheet).ToListAsync();

            foreach (Press p in presses)
            {
                await db.Entry(p).Reference(r => r.PressCategory).LoadAsync();
                await db.Entry(p).Reference(r => r.SpeedDenominator1).LoadAsync();
            }

            return presses;
        }

        public async Task<List<string>> GetPressCategoriesAsync()
        {
            return await db.PressCategories.Where(x => x.Deleted == null).Select(x => x.Title).Distinct().ToListAsync();
        }

        public async Task<List<string>> GetPressCategoriesAsync(bool keys)
        {
            List<string> categories = await db.PressCategories.Where(x => x.Deleted == null).Select(x => x.Title).Distinct().ToListAsync();
            categories.ForEach(x => x.SanitizeForKey());
            return categories;
        }

        public async Task<List<string>> GetServiceCategoriesAsync()
        {
            return await db.ServiceCategories.Where(x => x.Deleted == null).Select(x => x.Title).Distinct().ToListAsync();
        }

        public async Task<List<string>> GetServiceCategoriesAsync(bool keys)
        {
            List<string> categories = await db.ServiceCategories.Where(x => x.Deleted == null).Select(x => x.Title).Distinct().ToListAsync();
            categories.ForEach(x => x.SanitizeForKey());
            return categories;
        }

        private async Task UpdateQuotePricing(Calculations calcs, int? recordId)
        {
            // Pre-fetch all existing QuotePrices for this quote in one query.
            // UpdateOrCreateQuotePrice will look up in this list instead of hitting the DB per call.
            List<QuotePrice> existingPrices = await db.QuotePrices.Where(qp => qp.Quote == recordId).ToListAsync();

            // Check if we're using SQFT pricing mode
            if (calcs.sqftPricingCalculations.Any())
            {
                // SQFT Pricing Mode: Store SQFT price in Ink Cost record, set others to 0
                foreach (Calculations.SqftPricingCalculation sqftCalc in calcs.sqftPricingCalculations)
                {
                    try
                    {
                        int itemId = sqftCalc.GetItemId();
                        MastermindLogger.Log(LogLevel.Info, $"=== Processing SQFT pricing for Quote Item {itemId} ===");

                        // Get the QuoteItem to find the printing service
                        QuoteItem qi = await db.QuoteItems.FindAsync(itemId);

                        if (qi != null)
                        {
                            await db.Entry(qi).Collection(c => c.QuoteItemServices).LoadAsync();
                            await db.Entry(qi).Collection(c => c.QuoteItemFinishingMaterials).LoadAsync();
                            await db.Entry(qi).Reference(r => r.Press1).LoadAsync();

                            MastermindLogger.Log(LogLevel.Info, $"Loaded {qi.QuoteItemServices.Count} services and {qi.QuoteItemFinishingMaterials.Count} finishing materials");

                            // Get the press title from the item
                            string pressTitle = qi.Press1?.Title ?? "Unknown Press";
                            MastermindLogger.Log(LogLevel.Info, $"Press: {pressTitle}");

                            // Find the printing service (OriginalService == 1321) to get the service ID
                            QuoteItemService printService = qi.QuoteItemServices.FirstOrDefault(x => x.OriginalService == 1321);

                            if (printService != null)
                            {
                                MastermindLogger.Log(LogLevel.Info, $"Found print service for {pressTitle}");

                                // Store SQFT price in Ink Cost record only (using press title from item)
                                await UpdateOrCreateQuotePrice(existingPrices, itemId, printService.Id, recordId, sqftCalc.GetTotalSqft(),
                                    sqftCalc.GetCost(), sqftCalc.GetMarkupValue(), sqftCalc.GetPrice(), false, false,
                                    pressTitle + " Ink Cost", true, 0);

                                MastermindLogger.Log(LogLevel.Info, $"Updated Ink Cost record for {pressTitle}");

                                // Set Labor Cost to 0
                                await UpdateOrCreateQuotePrice(existingPrices, itemId, printService.Id, recordId, 0, 0, 0, 0,
                                    false, false, pressTitle + " Labor Cost", true, 0);

                                MastermindLogger.Log(LogLevel.Info, $"Set Labor Cost to 0 for {pressTitle}");
                            }
                            else
                            {
                                MastermindLogger.Log(LogLevel.Warning, $"No print service found for Quote Item {itemId}");
                            }

                            // Set Material to 0
                            await UpdateOrCreateQuotePrice(existingPrices, itemId, -1, recordId, 0, 0, 0, 0, false, true, "", false);
                            MastermindLogger.Log(LogLevel.Info, "Set Material to 0");

                            // Set all other services to 0 (finishing services)
                            foreach (QuoteItemService qis in qi.QuoteItemServices.Where(x => x.OriginalService != 1321))
                            {
                                await db.Entry(qis).Reference(r => r.Service).LoadAsync();
                                await UpdateOrCreateQuotePrice(existingPrices, qi.Id, qis.Id, recordId, 0, 0, 0, 0, false, false, qis.Service.Title, false, 0);
                            }
                            MastermindLogger.Log(LogLevel.Info, $"Set {qi.QuoteItemServices.Count(x => x.OriginalService != 1321)} other services to 0");

                            // Set finishing materials to 0
                            foreach (QuoteItemFinishingMaterial qifm in qi.QuoteItemFinishingMaterials)
                            {
                                await db.Entry(qifm).Reference(r => r.Material1).LoadAsync();
                                await UpdateOrCreateQuotePrice(existingPrices, qi.Id, qifm.Id, recordId, 0, 0, 0, 0, false, false, qifm.Material1.Title, false);
                            }
                            MastermindLogger.Log(LogLevel.Info, $"Set {qi.QuoteItemFinishingMaterials.Count} finishing materials to 0");
                        }
                        else
                        {
                            MastermindLogger.Log(LogLevel.Error, $"QuoteItem {itemId} not found!");
                        }
                    }
                    catch (Exception ex)
                    {
                        MastermindLogger.Log(LogLevel.Error, $"Error processing SQFT pricing for Quote Item");
                        MastermindLogger.LogException(ex);
                        throw;
                    }
                }
            }
            else
            {
                // Standard Pricing Mode: Use all standard calculations
                // Process Material Calculations
                foreach (Calculations.MaterialCalculation mc in calcs.materialCalculations)
                {
                    await UpdateOrCreateQuotePrice(existingPrices, mc.GetItemId(), mc.GetMaterialId(), recordId, mc.GetTotalMaterialAmount(), mc.GetCost(), mc.GetMarkupValue(), mc.GetPrice(), false, true, mc.GetTitle(), false);
                }

                // Handle other types of calculations if they have similar or different identification schemes
                foreach (Calculations.FinishingMaterialCalculation fc in calcs.finishingMaterialCalculations)
                {
                    await UpdateOrCreateQuotePrice(existingPrices, fc.GetItemId(), fc.GetFinishingMaterialId(), recordId, fc.GetMaterialAmount(), fc.GetMaterialCost(), fc.GetMarkupValue(), fc.GetMaterialPrice(), false, false, fc.GetTitle(), false);
                }

                // Services
                foreach (Calculations.ServiceCalculation sc in calcs.serviceCalculations)
                {
                    await UpdateOrCreateQuotePrice(existingPrices, sc.GetItemId(), sc.GetItemServiceId(), recordId, 0, sc.GetCost(), sc.GetMarkupValue(), sc.GetPrice(), false, false, sc.GetTitle(), false, sc.GetTime());
                }

                // Printing Calculations have multiple entries per item
                foreach (Calculations.PrintingCalculation pc in calcs.printingCalculations)
                {
                    // Ink
                    await UpdateOrCreateQuotePrice(existingPrices, pc.GetItemId(), pc.GetItemServiceId(), recordId, 0, pc.GetInkCost(), pc.GetInkMarkupValue(), pc.GetInkPrice(), false, false, pc.GetInkTitle(), true, pc.GetTime());

                    // Labor
                    await UpdateOrCreateQuotePrice(existingPrices, pc.GetItemId(), pc.GetItemServiceId(), recordId, 0, pc.GetPrintCost(), pc.GetLaborMarkupValue(), pc.GetPrintPrice(), false, false, pc.GetLaborTitle(), true, pc.GetTime());
                }

                // Outsourced Calculations
                foreach (Calculations.OutsourcedCalculation oc in calcs.outsourcedCalculations)
                {
                    await UpdateOrCreateQuotePrice(existingPrices, oc.GetItemId(), null, recordId, 0, oc.GetCost(), oc.GetMarkupValue(), oc.GetPrice(), true, false, oc.GetTitle(), false);
                }
            }

            try
            {
                await db.SaveChangesAsync();
            }
            catch (Exception e)
            {
                MastermindLogger.Log(LogLevel.Error, "Error saving quote pricing");
                MastermindLogger.LogException(e);
            }
        }

        private Task UpdateOrCreateQuotePrice(
            List<QuotePrice> existingPrices,
            int? itemId,
            int? itemServiceId,
            int? recordId,
            decimal quantity,
            decimal cost,
            decimal markup,
            decimal price,
            bool outsourcedItem,
            bool isMaterial,
            string title,
            bool isPrint,
            decimal? duration = null)
        {
            QuotePrice existingQuotePrice;
            bool skipMaterial = false;

            // In-memory lookup against pre-fetched list — no DB round trip per call.
            if (isMaterial)
            {
                existingQuotePrice = existingPrices.FirstOrDefault(qp => qp.QuoteItem == itemId && qp.QuoteItemService == itemServiceId && qp.IsMaterial == true);
                if (itemServiceId == -1)
                {
                    skipMaterial = true;
                }
            }
            else if (isPrint)
            {
                existingQuotePrice = existingPrices.FirstOrDefault(qp => qp.QuoteItem == itemId && qp.Title == title);
            }
            else
            {
                existingQuotePrice = existingPrices.FirstOrDefault(qp => qp.QuoteItem == itemId && qp.QuoteItemService == itemServiceId && qp.IsMaterial == false);
            }

            if (existingQuotePrice != null)
            {
                // Update existing record
                existingQuotePrice.Quantity = quantity;
                existingQuotePrice.Cost = cost;
                existingQuotePrice.Markup = markup;
                existingQuotePrice.Price = price;
                existingQuotePrice.OutsourcedItem = outsourcedItem;
                existingQuotePrice.IsMaterial = isMaterial;
                existingQuotePrice.Title = title;

                //poor solution but need to hack something together - if the title contains Ink Cost, then forego the duration (also in else)
                if (duration.HasValue && (title ?? "").ToUpper().IndexOf("INK COST") == -1)
                    existingQuotePrice.Duration = duration.Value;
                else
                    existingQuotePrice.Duration = 0m;

                // Only transition tracked rows from Unchanged → Modified.
                // If the entity was Added earlier in this same Calculate call (PK still 0),
                // do NOT flip it to Modified — that triggers EF FixupKey and throws
                // "more than one entity of type 'QuotePrice' have the same primary key value".
                // Mutating the fields above is sufficient — EF will INSERT with the new values.
                var entry = db.Entry(existingQuotePrice);
                if (entry.State != EntityState.Added)
                {
                    entry.State = EntityState.Modified;
                }
            }
            else
            {
                if (!skipMaterial)
                {
                    // Add new record
                    var newPrice = new QuotePrice
                    {
                        Quote = recordId,
                        QuoteItem = itemId,
                        QuoteItemService = itemServiceId,
                        Quantity = quantity,
                        Cost = cost,
                        Markup = markup,
                        Price = price,
                        OutsourcedItem = outsourcedItem,
                        IsMaterial = isMaterial,
                        Title = title,

                        Duration = (title ?? "").ToUpper().IndexOf("INK COST") == -1 ? duration : 0m
                    };
                    db.QuotePrices.Add(newPrice);
                    // Keep in-memory list in sync so subsequent same-key lookups within this call find the new row.
                    existingPrices.Add(newPrice);
                }
            }

            return Task.CompletedTask;
        }

        private async Task UpdateJobPricing(Calculations calcs, int? recordId)
        {
            // Pre-fetch all existing JobPrices for this job in one query.
            // UpdateOrCreateJobPrice will look up in this list instead of hitting the DB per call.
            List<JobPrice> existingPrices = await db.JobPrices.Where(jp => jp.Job == recordId).ToListAsync();

            // Check if we're using SQFT pricing mode
            if (calcs.sqftPricingCalculations.Any())
            {
                // SQFT Pricing Mode: Store SQFT price in Ink Cost record, set others to 0
                foreach (Calculations.SqftPricingCalculation sqftCalc in calcs.sqftPricingCalculations)
                {
                    try
                    {
                        int itemId = sqftCalc.GetItemId();
                        MastermindLogger.Log(LogLevel.Info, $"=== Processing SQFT pricing for Job Item {itemId} ===");

                        // Get the JobItem to find the printing service
                        JobItem ji = await db.JobItems.FindAsync(itemId);

                        if (ji != null)
                        {
                            await db.Entry(ji).Collection(c => c.JobItemServices).LoadAsync();
                            await db.Entry(ji).Collection(c => c.JobItemFinishingMaterials).LoadAsync();
                            await db.Entry(ji).Reference(r => r.Press1).LoadAsync();

                            MastermindLogger.Log(LogLevel.Info, $"Loaded {ji.JobItemServices.Count} services and {ji.JobItemFinishingMaterials.Count} finishing materials");

                            // Get the press title from the item
                            string pressTitle = ji.Press1?.Title ?? "Unknown Press";
                            MastermindLogger.Log(LogLevel.Info, $"Press: {pressTitle}");

                            // Find the printing service (ServiceId == 1321) to get the service ID
                            JobItemService printService = ji.JobItemServices.FirstOrDefault(x => x.ServiceId == 1321);

                            if (printService != null)
                            {
                                MastermindLogger.Log(LogLevel.Info, $"Found print service for {pressTitle}");

                                // Store SQFT price in Ink Cost record only (using press title from item)
                                await UpdateOrCreateJobPrice(existingPrices, itemId, printService.Id, recordId, sqftCalc.GetTotalSqft(),
                                    sqftCalc.GetCost(), sqftCalc.GetMarkupValue(), sqftCalc.GetPrice(), false, false,
                                    pressTitle + " Ink Cost", true, 0);

                                MastermindLogger.Log(LogLevel.Info, $"Updated Ink Cost record for {pressTitle}");

                                // Set Labor Cost to 0
                                await UpdateOrCreateJobPrice(existingPrices, itemId, printService.Id, recordId, 0, 0, 0, 0,
                                    false, false, pressTitle + " Labor Cost", true, 0);

                                MastermindLogger.Log(LogLevel.Info, $"Set Labor Cost to 0 for {pressTitle}");
                            }
                            else
                            {
                                MastermindLogger.Log(LogLevel.Warning, $"No print service found for Job Item {itemId}");
                            }

                            // Set Material to 0
                            await UpdateOrCreateJobPrice(existingPrices, itemId, -1, recordId, 0, 0, 0, 0, false, true, "", false);
                            MastermindLogger.Log(LogLevel.Info, "Set Material to 0");

                            // Set all other services to 0 (finishing services)
                            foreach (JobItemService jis in ji.JobItemServices.Where(x => x.ServiceId != 1321))
                            {
                                await db.Entry(jis).Reference(r => r.Service).LoadAsync();
                                await UpdateOrCreateJobPrice(existingPrices, ji.Id, jis.Id, recordId, 0, 0, 0, 0, false, false, jis.Service.Title, false, 0);
                            }
                            MastermindLogger.Log(LogLevel.Info, $"Set {ji.JobItemServices.Count(x => x.ServiceId != 1321)} other services to 0");

                            // Set finishing materials to 0
                            foreach (JobItemFinishingMaterial jifm in ji.JobItemFinishingMaterials)
                            {
                                await db.Entry(jifm).Reference(r => r.Material1).LoadAsync();
                                await UpdateOrCreateJobPrice(existingPrices, ji.Id, jifm.Id, recordId, 0, 0, 0, 0, false, false, jifm.Material1.Title, false);
                            }
                            MastermindLogger.Log(LogLevel.Info, $"Set {ji.JobItemFinishingMaterials.Count} finishing materials to 0");
                        }
                        else
                        {
                            MastermindLogger.Log(LogLevel.Error, $"JobItem {itemId} not found!");
                        }
                    }
                    catch (Exception ex)
                    {
                        MastermindLogger.Log(LogLevel.Error, $"Error processing SQFT pricing for Job Item");
                        MastermindLogger.LogException(ex);
                        throw;
                    }
                }
            }
            else
            {
                // Standard Pricing Mode: Use all standard calculations
                // Process Material Calculations
                foreach (Calculations.MaterialCalculation mc in calcs.materialCalculations)
                {
                    await UpdateOrCreateJobPrice(existingPrices, mc.GetItemId(), mc.GetMaterialId(), recordId, mc.GetTotalMaterialAmount(), mc.GetCost(), mc.GetMarkupValue(), mc.GetPrice(), false, true, mc.GetTitle(), false);
                }

                // Handle other types of calculations if they have similar or different identification schemes
                foreach (Calculations.FinishingMaterialCalculation fc in calcs.finishingMaterialCalculations)
                {
                    await UpdateOrCreateJobPrice(existingPrices, fc.GetItemId(), fc.GetFinishingMaterialId(), recordId, fc.GetMaterialAmount(), fc.GetMaterialCost(), fc.GetMarkupValue(), fc.GetMaterialPrice(), false, false, fc.GetTitle(), false);
                }

                // Services
                foreach (Calculations.ServiceCalculation sc in calcs.serviceCalculations)
                {
                    await UpdateOrCreateJobPrice(existingPrices, sc.GetItemId(), sc.GetItemServiceId(), recordId, 0, sc.GetCost(), sc.GetMarkupValue(), sc.GetPrice(), false, false, sc.GetTitle(), false, sc.GetTime());
                }

                // Printing Calculations have multiple entries per item
                foreach (Calculations.PrintingCalculation pc in calcs.printingCalculations)
                {
                    // Ink
                    await UpdateOrCreateJobPrice(existingPrices, pc.GetItemId(), pc.GetItemServiceId(), recordId, 0, pc.GetInkCost(), pc.GetInkMarkupValue(), pc.GetInkPrice(), false, false, pc.GetInkTitle(), true, pc.GetTime());

                    // Labor
                    await UpdateOrCreateJobPrice(existingPrices, pc.GetItemId(), pc.GetItemServiceId(), recordId, 0, pc.GetPrintCost(), pc.GetLaborMarkupValue(), pc.GetPrintPrice(), false, false, pc.GetLaborTitle(), true, pc.GetTime());
                }

                // Outsourced Calculations
                foreach (Calculations.OutsourcedCalculation oc in calcs.outsourcedCalculations)
                {
                    await UpdateOrCreateJobPrice(existingPrices, oc.GetItemId(), null, recordId, 0, oc.GetCost(), oc.GetMarkupValue(), oc.GetPrice(), true, false, oc.GetTitle(), false);
                }
            }

            try
            {
                await db.SaveChangesAsync();
            }
            catch (Exception e)
            {
                MastermindLogger.Log(LogLevel.Error, "Error saving job pricing");
                MastermindLogger.LogException(e);
            }
        }

        private Task UpdateOrCreateJobPrice(
            List<JobPrice> existingPrices,
            int? itemId,
            int? itemServiceId,
            int? recordId,
            decimal quantity,
            decimal cost,
            decimal markup,
            decimal price,
            bool outsourcedItem,
            bool isMaterial,
            string title,
            bool isPrint,
            decimal? duration = null)
        {
            JobPrice existingJobPrice;
            bool skipMaterial = false;

            // In-memory lookup against pre-fetched list — no DB round trip per call.
            if (isMaterial)
            {
                existingJobPrice = existingPrices.FirstOrDefault(jp => jp.JobItem == itemId && jp.JobItemService == itemServiceId && jp.IsMaterial == true);
                if (itemServiceId == -1)
                {
                    skipMaterial = true;
                }
            }
            else if (isPrint)
            {
                existingJobPrice = existingPrices.FirstOrDefault(jp => jp.JobItem == itemId && jp.Title == title);
            }
            else
            {
                existingJobPrice = existingPrices.FirstOrDefault(jp => jp.JobItem == itemId && jp.JobItemService == itemServiceId && jp.IsMaterial == false);
            }

            if (existingJobPrice != null)
            {
                // Update existing record
                existingJobPrice.Quantity = quantity;
                existingJobPrice.Cost = cost;
                existingJobPrice.Markup = markup;
                existingJobPrice.Price = price;
                existingJobPrice.OutsourcedItem = outsourcedItem;
                existingJobPrice.IsMaterial = isMaterial;
                existingJobPrice.Title = title;
                if (duration.HasValue && (title ?? "").ToUpper().IndexOf("INK COST") == -1)
                    existingJobPrice.Duration = duration.Value;
                else
                    existingJobPrice.Duration = 0m;

                // Only transition tracked rows from Unchanged → Modified.
                // If the entity was Added earlier in this same Calculate call (PK still 0),
                // do NOT flip it to Modified — that triggers EF FixupKey and throws
                // "more than one entity of type 'JobPrice' have the same primary key value".
                // Mutating the fields above is sufficient — EF will INSERT with the new values.
                var entry = db.Entry(existingJobPrice);
                if (entry.State != EntityState.Added)
                {
                    entry.State = EntityState.Modified;
                }
            }
            else
            {
                if (!skipMaterial)
                {
                    // Add new record
                    var newPrice = new JobPrice
                    {
                        Job = recordId,
                        JobItem = itemId,
                        JobItemService = itemServiceId,
                        Quantity = quantity,
                        Cost = cost,
                        Markup = markup,
                        Price = price,
                        OutsourcedItem = outsourcedItem,
                        IsMaterial = isMaterial,
                        Title = title,

                        Duration = (title ?? "").ToUpper().IndexOf("INK COST") == -1 ? duration : 0m
                    };
                    db.JobPrices.Add(newPrice);
                    // Keep in-memory list in sync so subsequent same-key lookups within this call find the new row.
                    existingPrices.Add(newPrice);

                }
            }

            return Task.CompletedTask;
        }

        public async Task<ActionResult> TestCalcs(string recordType, int? recordId)
        {
            Calculations calcs = new Calculations();
            List<string> calcErrors = new List<string>();

            // Add initial logging
            MastermindLogger.Log(LogLevel.Info, $"=== Starting TestCalcs for {recordType} {recordId} ===");

            // [SMOKE-TEST] stage timing for Step 3
            var sw_calc = System.Diagnostics.Stopwatch.StartNew();
            var sw_stage = System.Diagnostics.Stopwatch.StartNew();

            // Hoisted: reused by the second switch (response section) so we don't FindAsync again.
            Quote q = null;
            Job j = null;

            switch (recordType.ToUpper())
            {
                case "QUOTE":
                    q = await db.Quotes.FindAsync(recordId);

                    if (q != null)
                    {
                        // ONE query loads QuoteItems + their services (with Service nav) + their finishing materials.
                        // Replaces the per-item Collection.LoadAsync() pair and the per-service Reference(Service).LoadAsync().
                        await db.Entry(q)
                            .Collection(c => c.QuoteItems)
                            .Query()
                            .Include(qi => qi.QuoteItemServices.Select(qis => qis.Service))
                            .Include(qi => qi.QuoteItemFinishingMaterials)
                            .LoadAsync();
                        await db.Entry(q).Collection(c => c.QuoteOutsourcedItems).LoadAsync();

                        // Check if using SQFT pricing mode
                        bool useSqftPricing = q.UseSqftPricing ?? false;

                        //foreach quote item
                        foreach (QuoteItem qi in q.QuoteItems.OrderBy(x => x.Ordinal).ToList())
                        {
                            // QuoteItemServices, QuoteItemFinishingMaterials, and Service nav already loaded via Include chain above.

                            if (useSqftPricing)
                            {
                                // SQFT Pricing Mode: Only add SQFT calculation
                                calcs.sqftPricingCalculations.Add(new Calculations.SqftPricingCalculation("QUOTE", qi.Id));
                            }
                            else
                            {
                                // Standard Pricing Mode: Use all standard calculations
                                //calculate Material
                                calcs.materialCalculations.Add(new Calculations.MaterialCalculation("QUOTE", qi.Id));

                                //calculate each finishing material
                                foreach (QuoteItemFinishingMaterial qifm in qi.QuoteItemFinishingMaterials.ToList())
                                {
                                    calcs.finishingMaterialCalculations.Add(new Calculations.FinishingMaterialCalculation("QUOTE", qifm.Id));
                                }

                                //calculate each service (skip finishing material services — they're already calculated via FinishingMaterialCalculation)
                                foreach (QuoteItemService qis in qi.QuoteItemServices.Where(x => x.IsFinishingService != true).ToList())
                                {
                                    // Service nav already loaded via Include chain — no per-row DB call.
                                    //print service
                                    if (qis.OriginalService == 1321)
                                    {
                                        calcs.printingCalculations.Add(new Calculations.PrintingCalculation("QUOTE", qis.Id));
                                    }
                                    else
                                    {
                                        calcs.serviceCalculations.Add(new Calculations.ServiceCalculation("QUOTE", qis.Id));
                                    }
                                }
                            }
                        }

                        // Outsourced items: Set to 0 in SQFT mode, calculate normally in standard mode
                        if (!useSqftPricing)
                        {
                            foreach (QuoteOutsourcedItem qoi in q.QuoteOutsourcedItems.ToList())
                            {
                                calcs.outsourcedCalculations.Add(new Calculations.OutsourcedCalculation("QUOTE", qoi.Id));
                            }
                        }
                    }
                    break;

                case "JOB":
                    j = await db.Jobs.FindAsync(recordId);

                    //make sure calcs aren't locked
                    if (j.CalculationLock ?? false)
                        return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Calculations are locked for this job.");

                    if (j != null)
                    {
                        // ONE query loads JobItems + their services (with Service nav) + their finishing materials.
                        // Replaces the per-item Collection.LoadAsync() pair and the per-service Reference(Service).LoadAsync().
                        await db.Entry(j)
                            .Collection(c => c.JobItems)
                            .Query()
                            .Include(ji => ji.JobItemServices.Select(jis => jis.Service))
                            .Include(ji => ji.JobItemFinishingMaterials)
                            .LoadAsync();
                        await db.Entry(j).Collection(c => c.JobOutsourcedItems).LoadAsync();

                        // Check if using SQFT pricing mode
                        bool useSqftPricing = j.UseSqftPricing ?? false;

                        foreach (JobItem ji in j.JobItems.OrderBy(x => x.Ordinal).ToList())
                        {
                            // JobItemServices, JobItemFinishingMaterials, and Service nav already loaded via Include chain above.

                            if (useSqftPricing)
                            {
                                // SQFT Pricing Mode: Only add SQFT calculation
                                calcs.sqftPricingCalculations.Add(new Calculations.SqftPricingCalculation("JOB", ji.Id));
                            }
                            else
                            {
                                // Standard Pricing Mode: Use all standard calculations
                                calcs.materialCalculations.Add(new Calculations.MaterialCalculation("JOB", ji.Id));

                                foreach (JobItemFinishingMaterial jifm in ji.JobItemFinishingMaterials.ToList())
                                {
                                    calcs.finishingMaterialCalculations.Add(new Calculations.FinishingMaterialCalculation("JOB", jifm.Id));
                                }

                                foreach (JobItemService jis in ji.JobItemServices.Where(x => x.IsFinishingService != true).ToList())
                                {
                                    // Service nav already loaded via Include chain — no per-row DB call.
                                    //print service
                                    if (jis.ServiceId == 1321)
                                    {
                                        calcs.printingCalculations.Add(new Calculations.PrintingCalculation("JOB", jis.Id));
                                    }
                                    else
                                    {
                                        calcs.serviceCalculations.Add(new Calculations.ServiceCalculation("JOB", jis.Id));
                                    }
                                }
                            }
                        }

                        // Outsourced items: Set to 0 in SQFT mode, calculate normally in standard mode
                        if (!useSqftPricing)
                        {
                            foreach (JobOutsourcedItem joi in j.JobOutsourcedItems.ToList())
                            {
                                calcs.outsourcedCalculations.Add(new Calculations.OutsourcedCalculation("JOB", joi.Id));
                            }
                        }
                    }
                    break;

                default:
                    break;
            }

            // [SMOKE-TEST]
            MastermindLogger.Log(LogLevel.Info, $"[TestCalcs] T1 LoadPhase {sw_stage.ElapsedMilliseconds}ms");
            sw_stage.Restart();

            //material
            MastermindLogger.Log(LogLevel.Info, $"=== Starting Material Calculations - {calcs.materialCalculations.Count} items ===");
            foreach (Calculations.MaterialCalculation mc in calcs.materialCalculations)
            {
                try
                {
                    mc.CalculateCost(db);
                    await mc.GetMarkup(db);
                    mc.CalculatePrice();
                }
                catch (Exception e)
                {
                    calcErrors.Add($"Material calculation failed for \"{mc.GetTitle()}\": {e.Message}");
                    MastermindLogger.Log(LogLevel.Error, $"Error in Material Calculation for \"{mc.GetTitle()}\"");
                    MastermindLogger.LogException(e);
                }
            }

            //finishing materials
            foreach (Calculations.FinishingMaterialCalculation fc in calcs.finishingMaterialCalculations)
            {
                try
                {
                    await fc.CalculateMaterialQuantity(db);
                    fc.CalculateMaterialCost();
                    await fc.GetMarkup(db);
                    fc.CalculateMaterialPrice();
                }
                catch (Exception e)
                {
                    calcErrors.Add($"Finishing material calculation failed for \"{fc.GetTitle()}\": {e.Message}");
                    MastermindLogger.Log(LogLevel.Error, $"Error in Finishing Material Calculation for \"{fc.GetTitle()}\"");
                    MastermindLogger.LogException(e);
                }
            }

            //services
            foreach (Calculations.ServiceCalculation sc in calcs.serviceCalculations)
            {
                try
                {
                    await sc.CalculateTime(db);
                    sc.CalculateCost();
                    await sc.GetMarkup(db);
                    sc.CalculatePrice();
                }
                catch (Exception e)
                {
                    calcErrors.Add($"Service calculation failed for \"{sc.GetTitle()}\": {e.Message}");
                    MastermindLogger.Log(LogLevel.Error, $"Error in Service Calculation for \"{sc.GetTitle()}\"");
                    MastermindLogger.LogException(e);
                }
            }

            //printing
            foreach (Calculations.PrintingCalculation pc in calcs.printingCalculations)
            {
                try
                {
                    await pc.CalculateTime(db);

                    await pc.CalculateInkCost(db);
                    pc.CalculatePrintCost();

                    await pc.GetInkMarkup(db);
                    await pc.GetLaborMarkup(db);

                    pc.CalculateInkPrice();
                    pc.CalculatePrintPrice();
                }
                catch (Exception e)
                {
                    calcErrors.Add($"Printing calculation failed for \"{pc.GetLaborTitle()}\": {e.Message}");
                    MastermindLogger.Log(LogLevel.Error, $"Error in Printing Calculation for \"{pc.GetLaborTitle()}\"");
                    MastermindLogger.LogException(e);
                }
            }

            //outsourced items
            foreach (Calculations.OutsourcedCalculation oc in calcs.outsourcedCalculations)
            {
                try
                {
                    oc.CalculateCost(db);
                    await oc.GetMarkup(db);
                    oc.CalculatePrice();
                }
                catch (Exception e)
                {
                    calcErrors.Add($"Outsourced item calculation failed for \"{oc.GetTitle()}\": {e.Message}");
                    MastermindLogger.Log(LogLevel.Error, $"Error in Outsourced Calculation for \"{oc.GetTitle()}\"");
                    MastermindLogger.LogException(e);
                }
            }

            //sqft pricing calculations
            foreach (Calculations.SqftPricingCalculation sqftCalc in calcs.sqftPricingCalculations)
            {
                try
                {
                    await sqftCalc.CalculateSqft(db);
                    await sqftCalc.GetSqftRate(db);
                    sqftCalc.CalculateCost();
                    await sqftCalc.GetMarkup(db);
                    sqftCalc.CalculatePrice();
                }
                catch (Exception e)
                {
                    calcErrors.Add($"Sqft pricing calculation failed for \"{sqftCalc.GetTitle()}\": {e.Message}");
                    MastermindLogger.Log(LogLevel.Error, $"Error in SQFT Pricing Calculation for \"{sqftCalc.GetTitle()}\"");
                    MastermindLogger.LogException(e);
                }
            }

            await db.SaveChangesAsync();

            // [SMOKE-TEST]
            MastermindLogger.Log(LogLevel.Info, $"[TestCalcs] T2 CalcPhase+Save {sw_stage.ElapsedMilliseconds}ms");
            sw_stage.Restart();

            //if update prices then update prices
            switch (recordType.ToUpper())
            {
                case "QUOTE":
                    await UpdateQuotePricing(calcs, recordId);
                    break;

                case "JOB":
                    await UpdateJobPricing(calcs, recordId);
                    break;

                default:
                    break;
            }

            await db.SaveChangesAsync();

            // [SMOKE-TEST]
            MastermindLogger.Log(LogLevel.Info, $"[TestCalcs] T3 UpdatePricing+Save {sw_stage.ElapsedMilliseconds}ms");
            sw_stage.Restart();

            MastermindLogger.Log(LogLevel.Info, $"=== TestCalcs completed for {recordType} {recordId} ===");

            switch (recordType.ToUpper())
            {
                case "QUOTE":
                    // q is already loaded from the first switch.
                    // ONE query loads QuotePrices + QuoteItem1 + QuoteItemService1 navs.
                    // Replaces the per-QuotePrice Reference.LoadAsync() pair.
                    await db.Entry(q)
                        .Collection(c => c.QuotePrices)
                        .Query()
                        .Include(qp => qp.QuoteItem1)
                        .Include(qp => qp.QuoteItemService1)
                        .LoadAsync();

                    var qTotalPrices = await db.QuoteParentItemTotals(recordId).OrderBy(x => x.Ordinal).ToListAsync();

                    // DEBUG: Log Item #23 final pricing
                    var item23Quote = qTotalPrices.FirstOrDefault(x => x.Ordinal == 23);
                    if (item23Quote != null)
                    {
                        MastermindLogger.Log(LogLevel.Info, $"=== FINAL QUOTE ITEM #23 PRICE: {item23Quote.Total_Price} ===");
                    }

                    // Low Markup Alert check
                    var qOverallTotal = qTotalPrices.Sum(x => (x.Total_Price ?? 0m));
                    var qOverallCost = qTotalPrices.Sum(x => (x.Total_Cost ?? 0m));
                    if (qOverallCost > 0)
                    {
                        decimal qEffectiveMarkup = (qOverallTotal - qOverallCost) / qOverallCost;
                        decimal qMarkupPct = Math.Round(qEffectiveMarkup * 100, 4);

                        if (qEffectiveMarkup < 0.35m)
                        {
                            decimal? lastNotified = q.LastNotifiedMarkup;
                            if (lastNotified == null || Math.Round(lastNotified.Value * 100, 4) != qMarkupPct)
                            {
                                q.LastNotifiedMarkup = qEffectiveMarkup;
                                await db.SaveChangesAsync();

                                int activeUserId = UserSessionHelper.GetActiveUserId(HttpContext);
                                User activeUser = await db.Users.FindAsync(activeUserId);
                                string changedByName = activeUser != null
                                    ? $"{activeUser.FirstName} {activeUser.LastName}".Trim()
                                    : "Unknown";

                                var lowMarkupEmail = new LowMarkupAlertEmail(db);
                                await lowMarkupEmail.SendLowMarkupNotification(null, q.JobNumber, qMarkupPct, changedByName);
                            }
                        }
                        else if (q.LastNotifiedMarkup != null)
                        {
                            q.LastNotifiedMarkup = null;
                            await db.SaveChangesAsync();
                        }
                    }

                    var qSvcOverrides = db.QuoteItemServices
                        .Where(s => s.QuoteItem.Quote == recordId && s.CustomMarkup != null)
                        .Select(s => s.QuoteItemId)
                        .Distinct().ToList();
                    var qFmOverrides = db.QuoteItemFinishingMaterials
                        .Where(f => f.QuoteItemId.HasValue && f.QuoteItem.Quote == recordId && f.CustomMarkup != null)
                        .Select(f => f.QuoteItemId.Value)
                        .Distinct().ToList();
                    var qItemOverrides = db.QuoteItems
                        .Where(qi => qi.Quote == recordId && (qi.CustomMaterialMarkup != null || qi.CustomInkMarkup != null || qi.CustomPrintLaborMarkup != null))
                        .Select(qi => qi.Id)
                        .ToList();
                    var qItemsWithServiceOverrides = new HashSet<int>(
                        qSvcOverrides.Concat(qFmOverrides).Concat(qItemOverrides)
                    );

                    // [SMOKE-TEST]
                    MastermindLogger.Log(LogLevel.Info, $"[TestCalcs] T4 ResponseBuild {sw_stage.ElapsedMilliseconds}ms | totalElapsed={sw_calc.ElapsedMilliseconds}ms");

                    return new JsonResult
                    {
                        Data = JsonConvert.SerializeObject(new
                        {
                            newPricingList = RenderViewToString("~/Views/NewPriceDisplay/_PriceListModal.cshtml", GetPriceListViewModelWrapper(q)),
                            totalPrices = qTotalPrices,
                            overallTotal = qOverallTotal,
                            overallCost = qOverallCost,
                            useSqftPricing = q.UseSqftPricing ?? false,
                            sqftPriceOverride = q.SqftPriceOverride,
                            // Reuse q.QuoteItems already loaded by Include chain in first switch.
                            additionalData =
                                q.QuoteItems
                                    .Select(x =>
                                    {
                                        x.ParseRunSize();
                                        return new
                                        {
                                            x.Id,
                                            x.Up,
                                            x.RunLength,
                                            x.RunWidth,
                                            x.Sheets,
                                            x.RunSheets,
                                            x.TotalScrapSheets,
                                            x.TotalSheets,
                                            x.TotalRunSheets,
                                            x.BuySheets,
                                            x.Out,
                                            x.PressColorProfileFront,
                                            x.PressColorProfileBack,
                                            x.OverriddenPrice,
                                            HasServiceMarkupOverride = qItemsWithServiceOverrides.Contains(x.Id)
                                        };
                                    }).ToList(),
                            calcErrors = calcErrors
                        }, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };
                case "JOB":
                    // j is already loaded from the first switch.
                    // ONE query loads JobPrices + JobItem1 + JobItemService1 navs.
                    // Replaces the per-JobPrice Reference.LoadAsync() pair.
                    await db.Entry(j)
                        .Collection(c => c.JobPrices)
                        .Query()
                        .Include(jp => jp.JobItem1)
                        .Include(jp => jp.JobItemService1)
                        .LoadAsync();

                    var jTotalPrices = await db.JobParentItemTotals(recordId).OrderBy(x => x.Ordinal).ToListAsync();

                    // DEBUG: Log Item #23 final pricing
                    var item23Job = jTotalPrices.FirstOrDefault(x => x.Ordinal == 23);
                    if (item23Job != null)
                    {
                        MastermindLogger.Log(LogLevel.Info, $"=== FINAL JOB ITEM #23 PRICE: {item23Job.Total_Price} ===");
                    }

                    // Low Markup Alert check
                    var jOverallTotal = jTotalPrices.Sum(x => (x.Total_Price ?? 0m));
                    var jOverallCost = jTotalPrices.Sum(x => (x.Total_Cost ?? 0m));
                    if (jOverallCost > 0)
                    {
                        decimal jEffectiveMarkup = (jOverallTotal - jOverallCost) / jOverallCost;
                        decimal jMarkupPct = Math.Round(jEffectiveMarkup * 100, 4);

                        if (jEffectiveMarkup < 0.35m)
                        {
                            decimal? lastNotified = j.LastNotifiedMarkup;
                            if (lastNotified == null || Math.Round(lastNotified.Value * 100, 4) != jMarkupPct)
                            {
                                j.LastNotifiedMarkup = jEffectiveMarkup;
                                await db.SaveChangesAsync();

                                int activeUserId = UserSessionHelper.GetActiveUserId(HttpContext);
                                User activeUser = await db.Users.FindAsync(activeUserId);
                                string changedByName = activeUser != null
                                    ? $"{activeUser.FirstName} {activeUser.LastName}".Trim()
                                    : "Unknown";

                                var lowMarkupEmail = new LowMarkupAlertEmail(db);
                                await lowMarkupEmail.SendLowMarkupNotification(j.JobNumber, j.QuoteNumber, jMarkupPct, changedByName);
                            }
                        }
                        else if (j.LastNotifiedMarkup != null)
                        {
                            j.LastNotifiedMarkup = null;
                            await db.SaveChangesAsync();
                        }
                    }

                    var jSvcOverrides = db.JobItemServices
                        .Where(s => s.JobItem.Job == recordId && s.CustomMarkup != null)
                        .Select(s => s.JobItemId)
                        .Distinct().ToList();
                    var jFmOverrides = db.JobItemFinishingMaterials
                        .Where(f => f.JobItem.Job == recordId && f.CustomMarkup != null)
                        .Select(f => f.JobItemId)
                        .Distinct().ToList();
                    var jItemOverrides = db.JobItems
                        .Where(ji => ji.Job == recordId && (ji.CustomMaterialMarkup != null || ji.CustomInkMarkup != null || ji.CustomPrintLaborMarkup != null))
                        .Select(ji => ji.Id)
                        .ToList();
                    var jItemsWithServiceOverrides = new HashSet<int>(
                        jSvcOverrides.Concat(jFmOverrides).Concat(jItemOverrides)
                    );

                    // [SMOKE-TEST]
                    MastermindLogger.Log(LogLevel.Info, $"[TestCalcs] T4 ResponseBuild {sw_stage.ElapsedMilliseconds}ms | totalElapsed={sw_calc.ElapsedMilliseconds}ms");

                    return new JsonResult
                    {
                        Data = JsonConvert.SerializeObject(new
                        {
                            newPricingList = RenderViewToString("~/Views/NewPriceDisplay/_PriceListModal.cshtml", GetPriceListViewModelWrapper(j)),
                            totalPrices = jTotalPrices,
                            overallTotal = jOverallTotal,
                            overallCost = jOverallCost,
                            useSqftPricing = j.UseSqftPricing ?? false,
                            sqftPriceOverride = j.SqftPriceOverride,
                            // Reuse j.JobItems already loaded by Include chain in first switch.
                            additionalData =
                                j.JobItems
                                    .Select(x =>
                                    {
                                        x.ParseRunSize();

                                        return new
                                        {
                                            x.Id,
                                            x.Up,
                                            x.RunLength,
                                            x.RunWidth,
                                            x.Sheets,
                                            x.RunSheets,
                                            x.TotalScrapSheets,
                                            x.TotalSheets,
                                            x.TotalRunSheets,
                                            x.BuySheets,
                                            x.Out,
                                            x.PressColorProfileFront,
                                            x.PressColorProfileBack,
                                            x.OverriddenPrice,
                                            HasServiceMarkupOverride = jItemsWithServiceOverrides.Contains(x.Id)
                                        };
                                    }).ToList(),
                            calcErrors = calcErrors
                        }, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };
                default:
                    return null;
            }
        }

        public async Task<ActionResult> JobNumberExists(string j, int? id, RecordType jType)
        {
            if (String.IsNullOrEmpty(j) || !id.HasValue)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            bool exists = true;

            switch (jType)
            {
                case RecordType.Job:
                    try
                    {
                        await db.Jobs.SingleAsync(x => x.JobNumber.ToUpper() == j.ToUpper() && x.Id != id.Value);
                    }
                    catch (Exception)
                    {
                        exists = false;
                    }
                    break;

                case RecordType.Quote:
                    try
                    {
                        await db.Quotes.SingleAsync(x => x.JobNumber.ToUpper() == j.ToUpper() && x.Id != id.Value);
                    }
                    catch (Exception)
                    {
                        try
                        {
                            await db.Quotes.SingleAsync(x => x.JobNumber.ToUpper() == j.ToUpper() && x.Id != id.Value);
                        }
                        catch (Exception)
                        {
                            exists = false;
                        }
                    }
                    break;

                default:
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Error validating Job number");
            }

            return new JsonResult { Data = exists, JsonRequestBehavior = JsonRequestBehavior.AllowGet };
        }

        public async Task<ActionResult> AutocompleteVendor(string q)
        {
            int MaxResults = 15;

            var vendors = await db.Vendors.Where(x => x.Title.ToUpper().StartsWith(q.ToUpper())).Take(15).ToListAsync();
            List<int> selectedMaterials = vendors.Select(x => x.Id).ToList();

            if (vendors.Count < MaxResults)
            {
                //if we have less than 10 results, do a contains check for user convenience
                int diff = MaxResults - vendors.Count;

                var vendors2 = await db.Vendors.Where(x => x.Title.ToUpper().Contains(q.ToUpper()) && !selectedMaterials.Contains(x.Id)).Take(diff).ToListAsync();

                vendors = (List<Vendor>)vendors.Concat(vendors2).ToList();
            }
            return new JsonResult
            {
                Data =
                    JsonConvert.SerializeObject(vendors, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> AutocompleteItem(string q)
        {
            int MaxResults = 15;

            var items = await db.Items.Where(x => x.Deleted == null && x.Title.ToUpper().StartsWith(q.ToUpper())).Take(15).ToListAsync();
            List<int> selectedItems = items.Select(x => x.Id).ToList();

            if (items.Count < MaxResults)
            {
                //if we have less than 10 results, do a contains check for user convenience
                int diff = MaxResults - items.Count;

                var items2 = await db.Items.Where(x => x.Deleted == null && x.Title.ToUpper().Contains(q.ToUpper()) && !selectedItems.Contains(x.Id)).Take(diff).ToListAsync();

                items = (List<Item>)items.Concat(items2).ToList();
            }
            return new JsonResult
            {
                Data =
                    JsonConvert.SerializeObject(items, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> AutocompleteMaterial(string q)
        {
            int MaxResults = 15;

            var materials = await db.Materials.Where(x => x.Deleted == null && x.Title.ToUpper().StartsWith(q.ToUpper())).Take(15).ToListAsync();
            List<int> selectedMaterials = materials.Select(x => x.Id).ToList();

            if (materials.Count < MaxResults)
            {
                //if we have less than 10 results, do a contains check for user convenience
                int diff = MaxResults - materials.Count;

                var materials2 = await db.Materials.Where(x => x.Deleted == null && x.Title.ToUpper().Contains(q.ToUpper()) && !selectedMaterials.Contains(x.Id)).Take(diff).ToListAsync();

                materials = (List<Material>)materials.Concat(materials2).ToList();
            }
            return new JsonResult
            {
                Data =
                    JsonConvert.SerializeObject(materials, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> AutocompleteClient(string q)
        {
            int MaxResults = 15;

            var clients = await db.Clients.Include(i => i.Contacts).Where(x => x.Deleted == null && x.Title.ToUpper().StartsWith(q.ToUpper())).Take(15).ToListAsync();
            List<int> selectedClients = clients.Select(x => x.Id).ToList();

            if (clients.Count < MaxResults)
            {
                //if we have less than 10 results, do a contains check for user convenience
                int diff = MaxResults - clients.Count;

                var clients2 = await db.Clients.Where(x => x.Deleted == null && x.Title.ToUpper().Contains(q.ToUpper()) && !selectedClients.Contains(x.Id)).Take(diff).ToListAsync();

                clients = (List<Client>)clients.Concat(clients2).ToList();
            }

            //load address collection for each client
            foreach (Client c in clients)
            {
                await db.Entry(c).Collection(col => col.ShippingAddresses).LoadAsync();
            }

            var clientResult = clients.Select(x => new
            {
                x.Title,
                x.Id,
                x.Contacts,
                BlindShipping = x.BlindShipping ?? false,
                DefaultAddress = x.ShippingAddresses.FirstOrDefault(y => (y.IsDefaultForClient ?? false)).Id,
                markup = x.Markup ?? 0m,
                sqftPrice = x.SqftPrice ?? 0m
            }); //Id should be 0 in firstordefault if no result?

            return new JsonResult
            {
                Data =
                    JsonConvert.SerializeObject(clients, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> AutocompleteContact(string q, int? clientId)
        {
            if (!clientId.HasValue)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            int MaxResults = 15;

            q = q.ToLower();

            // Fetch contacts matching the customer number
            var contacts = await db.Contacts.Where(x => x.CusNo == clientId).ToListAsync();

            // Log or inspect contacts to ensure they are what you expect
            Console.WriteLine($"Fetched {contacts.Count} contacts.");

            // Perform in-memory filtering and convert fields to lowercase for comparison
            var results = contacts.Where(x =>
                (!string.IsNullOrWhiteSpace(x.First) && (x.First.ToLower().Contains(q))) ||
                (!string.IsNullOrWhiteSpace(x.Last) && (x.Last.ToLower().Contains(q)))
            ).Select(x => new
            {
                FirstName = x.First ?? "",
                LastName = x.Last ?? "",
                x.Id
            }).ToList();

            // Return JSON result
            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(results, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        //changing from t_fp to fp entity (removing t_fp)
        public async Task<ActionResult> AutocompleteJobNumber(string q)
        {
            var jobNumbers = await db.Jobs.Where(x => x.JobNumber.ToUpper().StartsWith(q.ToUpper())).Take(15).ToListAsync();
            var selectedJobNumbers = jobNumbers.Select(x => new
            {
                x.JobNumber,
                x.Id
            }).ToList();

            return new JsonResult
            {
                Data =
                    JsonConvert.SerializeObject(selectedJobNumbers, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        //changing from t_fp to fp entity (removing t_fp)
        public async Task<ActionResult> AutocompleteJobsReadyToShip(string q)
        {
            List<JobsReadyToShip_Result> JobsReadyToShip = await db.JobsReadyToShip().ToListAsync();
            List<int> idsReadyToShip = JobsReadyToShip.Select(x => x.Id).ToList();
            var jobNumbers = await db.Jobs.Where(x => x.JobNumber.ToUpper().StartsWith(q.ToUpper()) && idsReadyToShip.Contains(x.Id)).Take(15).ToListAsync();
            var selectedJobNumbers = jobNumbers.Select(x => new
            {
                x.JobNumber,
                x.Id
            }).ToList();

            return new JsonResult
            {
                Data =
                    JsonConvert.SerializeObject(selectedJobNumbers, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> AutocompleteActiveJobNumber(string q)
        {
            var jobNumbers = await db.Jobs
                .Where(x => x.JobNumber.ToUpper().StartsWith(q.ToUpper()) &&
                        x.MinStartDate.HasValue &&
                        x.Complete == null).Take(15).ToListAsync();
            var selectedJobNumbers = jobNumbers.Select(x => new
            {
                x.JobNumber,
                x.Id
            }).ToList();

            return new JsonResult
            {
                Data =
                    JsonConvert.SerializeObject(selectedJobNumbers, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> AutocompleteReadyToBillJobNumber(string q)
        {
            //var jobNumbers = await t_db.t_Jobs.Where(x => x.JobNumber.ToUpper().StartsWith(q.ToUpper())).Take(15).ToListAsync();

            /*var jobNumbers = await db.Jobs
                .Where(x => x.JobNumber.ToUpper().StartsWith(q.ToUpper()) &&
                        x.Shipped != null).Take(15).ToListAsync();*/

            //changed to search all job numbers 9-11-2024
            var jobNumbers = await db.Jobs
                .Where(x => x.JobNumber.ToUpper().StartsWith(q.ToUpper())).Take(15).ToListAsync();
            var selectedJobNumbers = jobNumbers.Select(x => new
            {
                x.JobNumber,
                x.Id
            }).ToList();

            return new JsonResult
            {
                Data =
                    JsonConvert.SerializeObject(selectedJobNumbers, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> AutocompleteQuoteNumber(string q)
        {
            var quoteNumbers = await db.Quotes.Where(x => x.JobNumber.ToUpper().StartsWith(q.ToUpper())).Take(15).ToListAsync();
            var selectedQuoteNumbers = quoteNumbers.Select(x => new
            {
                x.JobNumber,
                x.Id
            }).ToList();

            return new JsonResult
            {
                Data =
                    JsonConvert.SerializeObject(selectedQuoteNumbers, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> AutocompleteJobQuote(string q)
        {
            var jobs = await db.Jobs.Where(x => x.JobNumber.Contains(q)).Select(x => new
            {
                x.JobNumber,
                x.Id,
                recordType = "Job"
            }).ToListAsync();

            var quotes = await db.Quotes.Where(x => x.JobNumber.Contains(q)).Select(x => new
            {
                x.JobNumber,
                x.Id,
                recordType = "Quote"
            }).ToListAsync();

            jobs.AddRange(quotes);

            return new JsonResult
            {
                Data =
                    JsonConvert.SerializeObject(jobs, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public string RenderViewToString(string viewName, object model)
        {
            ViewData.Model = model;
            using (var sw = new StringWriter())
            {
                var viewResult = ViewEngines.Engines.FindPartialView(ControllerContext, viewName);

                // Check if view was found
                if (viewResult.View == null)
                {
                    throw new InvalidOperationException($"View '{viewName}' not found. Searched locations: {string.Join(", ", viewResult.SearchedLocations)}");
                }

                var viewContext = new ViewContext(ControllerContext, viewResult.View, ViewData, TempData, sw);
                viewResult.View.Render(viewContext, sw);
                viewResult.ViewEngine.ReleaseView(ControllerContext, viewResult.View);
                return sw.GetStringBuilder().ToString();
            }
        }

        public string TruncateString(string s, int length = 47, bool includeEllipsis = true)
        {
            if (!String.IsNullOrEmpty(s))
            {
                int actualLength = includeEllipsis ? (length + ellipsis.Length) + 1 : length;

                return
                    s.Length > actualLength ?
                    (includeEllipsis ? String.Format("{0}{1}", s.Substring(0, length), ellipsis) : String.Format("{0}", s.Substring(length))) :
                    s;
            }

            return "";
        }

        /// <summary>
        /// This function assumes that a connection is open to the database
        /// </summary>
        /// <param name="list">The type of select list to generate</param>
        /// <returns></returns>
        public async Task<IEnumerable<SelectListItem>> GenerateSelectListAsync(SelectListType list)
        {
            switch (list)
            {
                case SelectListType.PriceLevel:
                    return await db.PriceLevels
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToListAsync();

                case SelectListType.Clients:
                    return await db.Clients
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToListAsync();

                case SelectListType.ServiceCategories:
                    return await db.ServiceCategories
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToListAsync();

                case SelectListType.MaterialCategories:
                    return await db.MaterialCategories
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToListAsync();

                case SelectListType.PressCategories:
                    return await db.PressCategories
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToListAsync();

                /*case SelectListType.ColorProfiles:
                    return await db.ColorProfiles
                        .OrderBy(x => x.Id)
                        .Select(x => new SelectListItem { Text = x.ColorString, Value = x.Id.ToString() })
                        .ToListAsync();
                */
                case SelectListType.SpeedDenominators:
                    return await db.SpeedDenominators
                        .OrderBy(x => x.Id)
                        .Select(x => new SelectListItem { Text = x.Text, Value = x.Id.ToString() })
                        .ToListAsync();

                case SelectListType.InkCostDenominatorUnit:
                    return await db.InkCostDenominatorUnits
                        .OrderBy(x => x.Id)
                        .Select(x => new SelectListItem { Text = x.Text, Value = x.Id.ToString() })
                        .ToListAsync();

                case SelectListType.Roles:
                    return await db.Roles
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToListAsync();

                case SelectListType.ServiceTypes:
                    return await db.ServiceTypes
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToListAsync();

                case SelectListType.ConstraintTypes:
                    return await db.ConstraintTypes
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToListAsync();

                case SelectListType.ConstraintValueTypes:
                    return await db.ConstraintValueTypes
                        .OrderBy(x => x.ValueType)
                        .Select(x => new SelectListItem { Text = x.ValueType, Value = x.Id.ToString() })
                        .ToListAsync();

                case SelectListType.Users:
                    return await db.Users
                        .Where(x => !(x.Disabled ?? true) && x.Deleted == null)
                        .OrderBy(x => x.LastName)
                        .Select(x => new SelectListItem { Text = x.FirstName + " " + x.LastName, Value = x.Id.ToString() })
                        .ToListAsync();

                case SelectListType.TimeTrackingViewBy:
                    SelectListItem viewByUser = new SelectListItem { Text = "User", Value = "User" };
                    SelectListItem viewByJob = new SelectListItem { Text = "Job", Value = "Job" };
                    List<SelectListItem> ViewByList = new List<SelectListItem>
                    {
                        viewByJob,
                        viewByUser
                    };
                    return ViewByList;

                case SelectListType.Formulas:
                    List<SelectListItem> tFormulas = new List<SelectListItem>
                    {
                        new SelectListItem { Text = "None", Value = "0", Selected = true }
                    };
                    tFormulas.AddRange(
                            await db.TimeFormulas
                                .OrderBy(x => x.Title)
                                .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                                .ToListAsync()
                            );
                    return tFormulas;

                case SelectListType.USStates:
                    return await db.USStates
                        .OrderBy(x => x.Id)
                        .Select(x => new SelectListItem { Text = x.Name, Value = x.Id.ToString() })
                        .ToListAsync();

                case SelectListType.StockType:
                    return await db.MaterialStockTypes
                        .OrderByDescending(x => x.UI_Default)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToListAsync();

                case SelectListType.CostType:
                    return await db.MaterialCostTypes
                        .OrderBy(x => x.Id)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToListAsync();

                case SelectListType.ShippingOptions:
                    return await db.ShippingOptions
                        .OrderBy(x => x.SortOrder)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToListAsync();

                case SelectListType.Vendors:
                    return await db.Vendors
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToListAsync();

                case SelectListType.ProductionDepartments:
                    return await db.ProductionDepartments
                        .OrderBy(x => x.SortPosition)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToListAsync();

                case SelectListType.CustomerServiceReps:
                    return await db.Users
                        .Where(x => x.Deleted == null && x.Role == (int)GlobalUserRoles.CSR)
                        .OrderBy(x => x.LastName)
                        .Select(x => new SelectListItem { Text = (x.FirstName + " " + x.LastName), Value = x.Id.ToString() })
                        .ToListAsync();

                case SelectListType.FinishingFormulaApplications:
                    return await db.FinishingMaterialApplications
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToListAsync();

                case SelectListType.FinishingMaterialFormulas:
                    return await db.FinishingMaterialFormulas
                        .Where(x => x.Id != 1) // Exclude unused formula
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToListAsync();

                case SelectListType.Environments:
                    return await db.HostingEnvironments
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToListAsync();

                case SelectListType.Modules:
                    return await db.PermissionsManifests
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToListAsync();

                case SelectListType.FinishingServices:
                    return await db.Services.Where(x => x.Deleted == null && x.ServiceCategory.ServiceType == (int)Service.TypeOfService.Finishing)
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToListAsync();

                case SelectListType.Tournaments:
                    return await db.Clients.Where(x => (x.IsGolfClient ?? false) && x.Deleted == null)
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToListAsync();

                case SelectListType.EmployeeDepartments:
                    return await db.Departments
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToListAsync();

                case SelectListType.BillingClasses:
                    return await db.BillingClasses
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToListAsync();

                default:
                    return null;
            }
        }

        public IEnumerable<SelectListItem> GenerateSelectList(SelectListType list)
        {
            switch (list)
            {
                case SelectListType.PriceLevel:
                    return db.PriceLevels
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToList();

                case SelectListType.Clients:
                    return db.Clients
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToList();

                case SelectListType.ServiceCategories:
                    return db.ServiceCategories
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToList();

                case SelectListType.MaterialCategories:
                    return db.MaterialCategories
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToList();

                case SelectListType.PressCategories:
                    return db.PressCategories
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToList();

                case SelectListType.SpeedDenominators:
                    return db.SpeedDenominators
                        .OrderBy(x => x.Id)
                        .Select(x => new SelectListItem { Text = x.Text, Value = x.Id.ToString() })
                        .ToList();

                case SelectListType.InkCostDenominatorUnit:
                    return db.InkCostDenominatorUnits
                        .OrderBy(x => x.Id)
                        .Select(x => new SelectListItem { Text = x.Text, Value = x.Id.ToString() })
                        .ToList();

                case SelectListType.Roles:
                    return db.Roles
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToList();

                case SelectListType.ServiceTypes:
                    return db.ServiceTypes
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToList();

                case SelectListType.ConstraintTypes:
                    return db.ConstraintTypes
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToList();

                case SelectListType.ConstraintValueTypes:
                    return db.ConstraintValueTypes
                        .OrderBy(x => x.ValueType)
                        .Select(x => new SelectListItem { Text = x.ValueType, Value = x.Id.ToString() })
                        .ToList();

                case SelectListType.Users:
                    return db.Users
                        .Where(x => !(x.Disabled ?? true) && x.Deleted == null)
                        .OrderBy(x => x.LastName)
                        .Select(x => new SelectListItem { Text = x.FirstName + " " + x.LastName, Value = x.Id.ToString() })
                        .ToList();

                case SelectListType.TimeTrackingViewBy:
                    SelectListItem viewByUser = new SelectListItem { Text = "User", Value = "User" };
                    SelectListItem viewByJob = new SelectListItem { Text = "Job", Value = "Job" };
                    List<SelectListItem> ViewByList = new List<SelectListItem>
                    {
                        viewByJob,
                        viewByUser
                    };
                    return ViewByList;

                case SelectListType.Formulas:
                    List<SelectListItem> tFormulas = new List<SelectListItem>
                    {
                        new SelectListItem { Text = "None", Value = "0", Selected = true }
                    };
                    tFormulas.AddRange(
                            db.TimeFormulas
                                .OrderBy(x => x.Title)
                                .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                                .ToList()
                            );
                    return tFormulas;

                case SelectListType.USStates:
                    return db.USStates
                        .OrderBy(x => x.Id)
                        .Select(x => new SelectListItem { Text = x.Name, Value = x.Id.ToString() })
                        .ToList();

                case SelectListType.StockType:
                    return db.MaterialStockTypes
                        .OrderByDescending(x => x.UI_Default)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToList();

                case SelectListType.CostType:
                    return db.MaterialCostTypes
                        .OrderBy(x => x.Id)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToList();

                case SelectListType.ShippingOptions:
                    return db.ShippingOptions
                        .OrderBy(x => x.SortOrder)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToList();

                case SelectListType.Vendors:
                    return db.Vendors
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToList();

                case SelectListType.ProductionDepartments:
                    return db.ProductionDepartments
                        .OrderBy(x => x.SortPosition)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToList();

                case SelectListType.CustomerServiceReps:
                    return db.Users
                        .Where(x => x.Deleted == null && x.Role == (int)GlobalUserRoles.CSR)
                        .OrderBy(x => x.LastName)
                        .Select(x => new SelectListItem { Text = (x.FirstName + " " + x.LastName), Value = x.Id.ToString() })
                        .ToList();

                case SelectListType.FinishingFormulaApplications:
                    return db.FinishingMaterialApplications
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToList();

                case SelectListType.Environments:
                    return db.HostingEnvironments
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToList();

                case SelectListType.Modules:
                    return db.PermissionsManifests
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToList();

                case SelectListType.FinishingServices:
                    return db.Services.Where(x => x.Deleted == null && x.ServiceCategory.ServiceType == (int)Service.TypeOfService.Finishing)
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToList();

                case SelectListType.Tournaments:
                    return db.Clients.Where(x => (x.IsGolfClient ?? false) && x.Deleted == null)
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToList();

                case SelectListType.EmployeeDepartments:
                    return db.Departments
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToList();

                default:
                    return null;
            }
        }

        /// <summary>
        /// Renders a select list based on the provided SelectListType. This function also sets the selected value. See parameters for more info.
        /// </summary>
        /// <param name="list">Which select list to render</param>
        /// <param name="item">an item containing the property used to set the selected property on the appropriate entry</param>
        /// <param name="property">the name of the property to reference for setting the selected attribute</param>
        /// <returns>The select list with the appropriate item selected</returns>
        public async Task<IEnumerable<SelectListItem>> GenerateSelectListAsync(SelectListType list, object item, string property)
        {
            bool hasValue = !String.IsNullOrEmpty(property);
            string theVal = "";

            //test to be sure we have a value set on the item (can't be null)
            try
            {
                item.GetType().GetProperties().Single(y => y.Name.ToUpper() == property.ToUpper()).GetValue(item).ToString();
                theVal = item.GetType().GetProperties().Single(y => y.Name.ToUpper() == property.ToUpper()).GetValue(item).ToString();
            }
            catch (Exception)
            {
                hasValue = false;
            }

            switch (list)
            {
                case SelectListType.PriceLevel:

                    var pl = await db.PriceLevels
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToListAsync();

                    if (hasValue)
                        foreach (SelectListItem pl2 in pl)
                            pl2.Selected = item.GetType().GetProperties().Single(y => y.Name.ToUpper() == property.ToUpper()).GetValue(item).ToString() == pl2.Value;
                    else
                        foreach (SelectListItem pl2 in pl)
                            pl2.Selected = false;

                    return pl;

                case SelectListType.Clients:

                    var cl = await db.Clients
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToListAsync();

                    if (hasValue)
                        foreach (SelectListItem cl2 in cl)
                            cl2.Selected = item.GetType().GetProperties().Single(y => y.Name.ToUpper() == property.ToUpper()).GetValue(item).ToString() == cl2.Value;
                    else
                        foreach (SelectListItem cl2 in cl)
                            cl2.Selected = false;

                    return cl;

                case SelectListType.ServiceCategories:

                    var sc = await db.ServiceCategories
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToListAsync();

                    if (hasValue)
                        foreach (SelectListItem sc2 in sc)
                            sc2.Selected = item.GetType().GetProperties().Single(y => y.Name.ToUpper() == property.ToUpper()).GetValue(item).ToString() == sc2.Value;
                    else
                        foreach (SelectListItem sc2 in sc)
                            sc2.Selected = false;

                    return sc;

                case SelectListType.MaterialCategories:

                    var mc = await db.MaterialCategories
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToListAsync();

                    if (hasValue)
                        foreach (SelectListItem mc2 in mc)
                            mc2.Selected = item.GetType().GetProperties().Single(y => y.Name.ToUpper() == property.ToUpper()).GetValue(item).ToString() == mc2.Value;
                    else
                        foreach (SelectListItem mc2 in mc)
                            mc2.Selected = false;

                    return mc;

                case SelectListType.PressCategories:

                    var pc = await db.PressCategories
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToListAsync();

                    if (hasValue)
                        foreach (SelectListItem pc2 in pc)
                            pc2.Selected = item.GetType().GetProperties().Single(y => y.Name.ToUpper() == property.ToUpper()).GetValue(item).ToString() == pc2.Value;
                    else
                        foreach (SelectListItem pc2 in pc)
                            pc2.Selected = false;

                    return pc;

                case SelectListType.SpeedDenominators:

                    var sd = await db.SpeedDenominators
                        .OrderBy(x => x.Text)
                        .Select(x => new SelectListItem { Text = x.Text, Value = x.Id.ToString() })
                        .ToListAsync();

                    if (hasValue)
                        foreach (SelectListItem sd2 in sd)
                            sd2.Selected = item.GetType().GetProperties().Single(y => y.Name.ToUpper() == property.ToUpper()).GetValue(item).ToString() == sd2.Value;
                    else
                        foreach (SelectListItem sd2 in sd)
                            sd2.Selected = false;

                    return sd;

                /*case SelectListType.ColorProfiles:

                    var cp = await db.ColorProfiles
                        .OrderBy(x => x.Id)
                        .Select(x => new SelectListItem { Text = x.ColorString, Value = x.Id.ToString() })
                        .ToListAsync();

                    if (hasValue)
                        foreach (SelectListItem cp2 in cp)
                            cp2.Selected = item.GetType().GetProperties().Single(y => y.Name.ToUpper() == property.ToUpper()).GetValue(item).ToString() == cp2.Value;
                    else
                        foreach (SelectListItem cp2 in cp)
                            cp2.Selected = false;

                    return cp;*/

                case SelectListType.InkCostDenominatorUnit:

                    var icdu = await db.InkCostDenominatorUnits
                        .OrderBy(x => x.Id)
                        .Select(x => new SelectListItem { Text = x.Text, Value = x.Id.ToString() })
                        .ToListAsync();

                    if (hasValue)
                        foreach (SelectListItem icdu2 in icdu)
                            icdu2.Selected = item.GetType().GetProperties().Single(y => y.Name.ToUpper() == property.ToUpper()).GetValue(item).ToString() == icdu2.Value;
                    else
                        foreach (SelectListItem icdu2 in icdu)
                            icdu2.Selected = false;

                    return icdu;

                case SelectListType.Roles:
                    var roles = await db.Roles
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToListAsync();

                    if (hasValue)
                        foreach (SelectListItem roles2 in roles)
                            roles2.Selected = item.GetType().GetProperties().Single(y => y.Name.ToUpper() == property.ToUpper()).GetValue(item).ToString() == roles2.Value;
                    else
                        foreach (SelectListItem roles2 in roles)
                            roles2.Selected = false;

                    return roles;

                case SelectListType.ServiceTypes:
                    var serTypes = await db.ServiceTypes
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToListAsync();

                    if (hasValue)
                        foreach (SelectListItem serTypes2 in serTypes)
                            serTypes2.Selected = item.GetType().GetProperties().Single(y => y.Name.ToUpper() == property.ToUpper()).GetValue(item).ToString() == serTypes2.Value;
                    else
                        foreach (SelectListItem serTypes2 in serTypes)
                            serTypes2.Selected = false;

                    return serTypes;

                case SelectListType.StockType:
                    var stockTypes = await db.MaterialStockTypes
                        .OrderByDescending(x => x.UI_Default)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToListAsync();

                    if (hasValue)
                        foreach (SelectListItem stTypes in stockTypes)
                            stTypes.Selected = item.GetType().GetProperties().Single(y => y.Name.ToUpper() == property.ToUpper()).GetValue(item).ToString() == stTypes.Value;
                    else
                        foreach (SelectListItem stTypes in stockTypes)
                            stTypes.Selected = false;

                    return stockTypes;

                case SelectListType.CostType:
                    var costTypes = await db.MaterialCostTypes
                        .OrderBy(x => x.Id)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToListAsync();

                    if (hasValue)
                        foreach (SelectListItem cstTypes in costTypes)
                            cstTypes.Selected = item.GetType().GetProperties().Single(y => y.Name.ToUpper() == property.ToUpper()).GetValue(item).ToString() == cstTypes.Value;
                    else
                        foreach (SelectListItem cstTypes in costTypes)
                            cstTypes.Selected = false;

                    return costTypes;

                case SelectListType.ConstraintTypes:
                    var conTypes = await db.ConstraintTypes
                        .OrderBy(x => x.Title)
                        .Select(x => new SelectListItem { Text = x.Title, Value = x.Id.ToString() })
                        .ToListAsync();

                    if (hasValue)
                        foreach (SelectListItem conTypes2 in conTypes)
                            conTypes2.Selected = item.GetType().GetProperties().Single(y => y.Name.ToUpper() == property.ToUpper()).GetValue(item).ToString() == conTypes2.Value;
                    else
                        foreach (SelectListItem conTypes2 in conTypes)
                            conTypes2.Selected = false;

                    return conTypes;

                case SelectListType.ConstraintValueTypes:
                    var conValTypes = await db.ConstraintValueTypes
                        .OrderBy(x => x.ValueType)
                        .Select(x => new SelectListItem { Text = x.ValueType, Value = x.Id.ToString() })
                        .ToListAsync();

                    if (hasValue)
                        foreach (SelectListItem conValTypes2 in conValTypes)
                            conValTypes2.Selected = item.GetType().GetProperties().Single(y => y.Name.ToUpper() == property.ToUpper()).GetValue(item).ToString() == conValTypes2.Value;
                    else
                        foreach (SelectListItem conValTypes2 in conValTypes)
                            conValTypes2.Selected = false;

                    return conValTypes;

                case SelectListType.Formulas:
                    var formulas = await GenerateSelectListAsync(SelectListType.Formulas);

                    if (hasValue)
                    {
                        formulas.Single(x => x.Value == "0").Selected = false;
                        foreach (SelectListItem formula in formulas)
                        {
                            formula.Selected = item.GetType().GetProperties().Single(y => y.Name.ToUpper() == property.ToUpper()).GetValue(item).ToString() == formula.Value;
                        }
                    }
                    else
                    {
                        foreach (SelectListItem formula in formulas)
                        {
                            formula.Selected = false;
                        }

                        formulas.Single(x => x.Value == "0").Selected = true;
                    }

                    return formulas;

                case SelectListType.USStates:
                    var usstates = await GenerateSelectListAsync(SelectListType.USStates);

                    if (hasValue)
                    {
                        usstates.Single(X => X.Value == "51").Selected = false;
                        foreach (SelectListItem usstate in usstates)
                        {
                            usstate.Selected = item.GetType().GetProperties().Single(y => y.Name.ToUpper() == property.ToUpper()).GetValue(item).ToString() == usstate.Value;
                        }
                    }
                    else
                    {
                        foreach (SelectListItem st in usstates)
                        {
                            st.Selected = false;
                        }

                        usstates.Single(X => X.Value == "51").Selected = true;
                    }

                    return usstates;

                case SelectListType.ShippingOptions:
                    var shippingoptions = await GenerateSelectListAsync(SelectListType.ShippingOptions);

                    if (hasValue)
                    {
                        foreach (SelectListItem shippingoption in shippingoptions)
                        {
                            shippingoption.Selected = item.GetType().GetProperties().Single(y => y.Name.ToUpper() == property.ToUpper()).GetValue(item).ToString() == shippingoption.Value;
                        }
                    }
                    else
                    {
                        foreach (SelectListItem shippingoption in shippingoptions)
                            shippingoption.Selected = false;
                    }

                    return shippingoptions;

                case SelectListType.Vendors:
                    var vendors = await GenerateSelectListAsync(SelectListType.Vendors);

                    if (hasValue)
                    {
                        foreach (SelectListItem vendor in vendors)
                        {
                            vendor.Selected = item.GetType().GetProperties().Single(y => y.Name.ToUpper() == property.ToUpper()).GetValue(item).ToString() == vendor.Value;
                        }
                    }
                    else
                    {
                        foreach (SelectListItem vendor in vendors)
                            vendor.Selected = false;
                    }

                    return vendors;

                case SelectListType.ProductionDepartments:
                    var productionDepartments = await GenerateSelectListAsync(SelectListType.ProductionDepartments);

                    if (hasValue)
                    {
                        foreach (SelectListItem pd in productionDepartments)
                        {
                            pd.Selected = item.GetType().GetProperties().Single(y => y.Name.ToUpper() == property.ToUpper()).GetValue(item).ToString() == pd.Value;
                        }
                    }
                    else
                    {
                        foreach (SelectListItem pd in productionDepartments)
                            pd.Selected = false;
                    }

                    return productionDepartments;

                case SelectListType.Modules:
                    var permissionsManifests = await GenerateSelectListAsync(SelectListType.Modules);

                    if (hasValue)
                    {
                        foreach (SelectListItem pm in permissionsManifests)
                        {
                            pm.Selected = item.GetType().GetProperties().Single(y => y.Name.ToUpper() == property.ToUpper()).GetValue(item).ToString() == pm.Value;
                        }
                    }
                    else
                    {
                        foreach (SelectListItem pm in permissionsManifests)
                            pm.Selected = false;
                    }

                    return permissionsManifests;

                case SelectListType.Environments:
                    var environments = await GenerateSelectListAsync(SelectListType.Environments);

                    if (hasValue)
                    {
                        foreach (SelectListItem e in environments)
                        {
                            e.Selected = item.GetType().GetProperties().Single(y => y.Name.ToUpper() == property.ToUpper()).GetValue(item).ToString() == e.Value;
                        }
                    }
                    else
                    {
                        foreach (SelectListItem e in environments)
                            e.Selected = false;
                    }

                    return environments;

                case SelectListType.FinishingServices:
                    var finServices = await GenerateSelectListAsync(SelectListType.FinishingServices);

                    if (hasValue)
                    {
                        foreach (SelectListItem fs in finServices)
                        {
                            fs.Selected = item.GetType().GetProperties().Single(y => y.Name.ToUpper() == property.ToUpper()).GetValue(item).ToString() == fs.Value;
                        }
                    }
                    else
                    {
                        foreach (SelectListItem fs in finServices)
                            fs.Selected = false;
                    }

                    return finServices;

                case SelectListType.EmployeeDepartments:
                    var eDeps = await GenerateSelectListAsync(SelectListType.EmployeeDepartments);

                    if (hasValue)
                    {
                        foreach (SelectListItem ed in eDeps)
                        {
                            ed.Selected = item.GetType().GetProperties().Single(y => y.Name.ToUpper() == property.ToUpper()).GetValue(item).ToString() == ed.Value;
                        }
                    }
                    else
                    {
                        foreach (SelectListItem ed in eDeps)
                            ed.Selected = false;
                    }

                    return eDeps;

                default:
                    return null;
            }
        }

        protected Job UpdateJobRecord(Job e, Job n)
        {
            PropertyInfo[] props = typeof(Job).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo p in props)
            {
                if (p.CanWrite)
                {
                    object value = p.GetValue(n);
                    p.SetValue(e, value);
                }
            }

            return e;
        }

        //it is assumed that the parameters are matched one-to-one
        protected List<JobItem> UpdateJobItemRecords(List<JobItem> e, List<JobItem> n)
        {
            foreach (JobItem ji in n)
            {
                JobItem ji2 = e.Where(x => x.Id == ji.Id).First();

                PropertyInfo[] props = typeof(QuoteItem).GetProperties(BindingFlags.Public | BindingFlags.Instance);

                foreach (PropertyInfo p in props)
                {
                    if (p.CanWrite)
                    {
                        object value = p.GetValue(ji);
                        p.SetValue(ji2, value);
                    }
                }
            }

            return e;
        }

        protected Client UpdateCustomerRecord(Client e, Client n)
        {
            PropertyInfo[] props = typeof(Client).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo p in props)
            {
                if (p.CanWrite)
                {
                    object value = p.GetValue(n);
                    p.SetValue(e, value);
                }
            }

            return e;
        }

        protected Service UpdateServiceRecord(Service e, Service n)
        {
            PropertyInfo[] props = typeof(Service).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo p in props)
            {
                if (p.CanWrite)
                {
                    object value = p.GetValue(n);
                    p.SetValue(e, value);
                }
            }

            return e;
        }

        protected Material UpdateMaterialRecord(Material e, Material n)
        {
            PropertyInfo[] props = typeof(Material).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo p in props)
            {
                if (p.CanWrite)
                {
                    object value = p.GetValue(n);
                    p.SetValue(e, value);
                }
            }

            return e;
        }

        protected Press UpdatePressRecord(Press e, Press n)
        {
            PropertyInfo[] props = typeof(Press).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo p in props)
            {
                if (p.CanWrite)
                {
                    object value = p.GetValue(n);
                    p.SetValue(e, value);
                }
            }

            //set default speed if possible
            try
            {
                PressSpeed ps = db.PressSpeeds.Single(x => x.Press == e.Id && x.IsDefault == true);
                e.SpeedNumerator = ps.SpeedNumerator;
                e.SpeedDenominator = ps.SpeedDenominator;
            }
            catch (Exception)
            {
                //no default speed found
                e.SpeedNumerator = 0;
                e.SpeedDenominator = (int)SpeedDenominators.None;
            }

            return e;
        }

        protected Press CopyPress(Press toCopy)
        {
            Press newPress = new Press();

            PropertyInfo[] props = typeof(Press).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo p in props)
            {
                if (p.CanWrite)
                {
                    object value = p.GetValue(toCopy);
                    p.SetValue(newPress, value);
                }
            }

            return newPress;
        }

        protected User UpdateUserRecord(User e, User n)
        {
            PropertyInfo[] props = typeof(User).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo p in props)
            {
                if (p.CanWrite)
                {
                    object value = p.GetValue(n);
                    p.SetValue(e, value);
                }
            }

            return e;
        }

        protected ServiceCategory UpdateServiceCategoryRecord(ServiceCategory e, ServiceCategory n)
        {
            PropertyInfo[] props = typeof(ServiceCategory).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo p in props)
            {
                if (p.CanWrite)
                {
                    object value = p.GetValue(n);
                    p.SetValue(e, value);
                }
            }

            return e;
        }

        protected bool IsPropertyConstrained(string entityName, string propertyName)
        {
            System.Web.Mvc.ModelMetadata modelMetadata;

            switch (entityName.ToUpper())
            {
                case "JOBITEM":
                    modelMetadata = System.Web.Mvc.ModelMetadataProviders.Current.GetMetadataForType(null, typeof(JobItem));
                    break;

                case "QUOTEITEM":
                    modelMetadata = System.Web.Mvc.ModelMetadataProviders.Current.GetMetadataForType(null, typeof(QuoteItem));
                    break;

                default:
                    return false;
            }

            var propertyMetadata = modelMetadata.Properties.FirstOrDefault(x => x.PropertyName == propertyName);

            if (propertyMetadata != null && propertyMetadata.AdditionalValues.ContainsKey("Constrainable"))
                return true;
            else
                return false;
        }

        public async Task<VM_Services> GetServicesForView()
        {
            return new VM_Services
            {
                Services = await GetServicesAsync(),
                ServiceCategories = await GetServiceCategoriesAsync(),
                ServiceKeys = await GetServiceCategoriesAsync(true)
            };
        }

        public async Task<VM_PrepressServices> GetPrepressServicesForView()
        {
            return new VM_PrepressServices
            {
                Services = await GetPrepressServicesAsync(),
                ServiceCategories = await GetPrepressServiceCategoriesAsync(),
                ServiceKeys = await GetPrepressServiceCategoriesAsync(true)
            };
        }

        public async Task<VM_CuttingServices> GetCuttingServicesForView()
        {
            return new VM_CuttingServices
            {
                Services = await GetCuttingServicesAsync(),
                ServiceCategories = await GetCuttingServiceCategoriesAsync(),
                ServiceKeys = await GetCuttingServiceCategoriesAsync(true)
            };
        }

        public async Task<VM_FinishingServices> GetFinishingServicesForView()
        {
            return new VM_FinishingServices
            {
                Services = await GetFinishingServicesAsync(),
                ServiceCategories = await GetFinishingServiceCategoriesAsync(),
                ServiceKeys = await GetFinishingServiceCategoriesAsync(true)
            };
        }

        public async Task<VM_FinishingMaterials> GetFinishingMaterialsForView()
        {
            return new VM_FinishingMaterials
            {
                Materials = await GetFinishingMaterialsAsync(),
                MaterialCategories = await GetFinishingMaterialCategoriesAsync()
            };
        }

        public async Task<ActionResult> GetMaterialModel()
        {
            VM_Materials m = new VM_Materials
            {
                Materials = await GetAllMaterialsAsync(),
                MaterialCategories = await GetPrintingMaterialCategoriesAsync(),
                MaterialCategoryObjs = await GetPrintingMaterialCategoryObjsAsync(),
                MaterialKeys = await GetPrintingMaterialCategoriesAsync(true)
            };

            var json = RenderViewToString("~/Views/Partials/_MaterialSelectModal.cshtml", m);

            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(json, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> GetPressModel()
        {
            VM_Presses press = new VM_Presses
            {
                Presses = await GetPressesAsync(),
                PressCategories = await GetPressCategoriesAsync(),
                PressKeys = await GetPressCategoriesAsync(true)
            };

            var json = RenderViewToString("~/Views/Partials/_PressSelectModal.cshtml", press);

            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(json, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> GetFinishingMaterialsModel()
        {
            VM_FinishingMaterials fm = new VM_FinishingMaterials
            {
                Materials = await GetFinishingMaterialsAsync(),
                MaterialCategories = await GetFinishingMaterialCategoriesAsync()
            };

            var json = RenderViewToString("~/Views/Partials/_FinishingMaterialSelectModal.cshtml", fm);

            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(json, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<QuoteItemService> SetQuoteItemServiceDefaults(QuoteItemService itemService)
        {
            Service s = await db.Services.FindAsync(itemService.OriginalService);
            FormulaConfiguration fc = await db.FormulaConfigurations.FindAsync(s.TimeFormula); //ids match between tables

            if (s == null)
                return itemService;

            itemService.Notes = s.Notes;
            itemService.Caliper = s.Caliper;
            itemService.HourlyRate = s.BasePrice;
            itemService.ServiceConfiguration = s.ServiceConfiguration;
            itemService.MakeReady = s.MakeReady;
            itemService.WashUp = s.WashUp;
            itemService.MaterialCost = s.MaterialCost;
            itemService.UserTimeOverride = false;
            itemService.IsCuttingService = false;
            itemService.IsFinishingService = false;
            itemService.FriendlyName = "";
            itemService.ShowOnQuote = true;

            if (fc != null)
                itemService.FormulaConfiguration = fc.DefaultConfig;

            return itemService;
        }

        public async Task<JobItemService> SetJobItemServiceDefaults(JobItemService itemService)
        {
            Service s = await db.Services.FindAsync(itemService.ServiceId);
            FormulaConfiguration fc = await db.FormulaConfigurations.FindAsync(s.TimeFormula);

            if (s == null)
                return itemService;

            itemService.Notes = s.Notes;
            itemService.Caliper = s.Caliper;
            itemService.HourlyRate = s.BasePrice;
            itemService.ServiceConfiguration = s.ServiceConfiguration;
            itemService.MakeReady = s.MakeReady;
            itemService.WashUp = s.WashUp;
            itemService.MaterialCost = s.MaterialCost;
            itemService.UserTimeOverride = false;
            itemService.IsFinishingService = false;
            itemService.IsCuttingService = false;
            itemService.FriendlyName = "";

            if (fc != null)
                itemService.FormulaConfiguration = fc.DefaultConfig;

            return itemService;
        }

        public ActionResult ExternalBHRPull()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            MastermindLogger.Log(LogLevel.Info, "Beginning external data sync...");

            db.PullExternalProjects();
            db.DeleteExternalTimeEntriesByDateRange(DateTime.Now.AddDays(-4).Date, DateTime.Now.Date);
            db.PullExternalTimeEntries(DateTime.Now.AddDays(-4).Date, DateTime.Now.Date);

            sw.Stop();
            MastermindLogger.Log(LogLevel.Info, $"Process completed in {sw.ElapsedMilliseconds / 1000} seconds");

            return new JsonResult
            {
                Data = "done",
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        #region BambooHR Integration

        /// <summary>
        /// Main sync action for BambooHR time entries
        /// Mirrors GetEmployeeTimeCards() pattern for ADP
        /// </summary>
        public async Task<ActionResult> GetBambooHRTimeEntries()
        {
            int sleepDuration = 1000;
            Stopwatch sw = new Stopwatch();
            sw.Start();

            BambooHRConnector bConnector = new BambooHRConnector();
            DateTime periodStart = GetCurrentPeriodStart();
            DateTime periodEnd = DateTime.Now.Date;

            MastermindLogger.Log(LogLevel.Info, "Beginning BambooHR Time Entry Retrieval...");

            int employeesProcessed = 0;
            int clockEntriesProcessed = 0;
            int holidaysProcessed = 0;
            int timeOffProcessed = 0;
            string clockEntriesError = null;

            try
            {
                // Step 1: Sync employee directory
                MastermindLogger.Log(LogLevel.Info, "Fetching employee directory...");
                var employeeDirectory = await bConnector.GetEmployees();
                if (employeeDirectory?.employees != null)
                {
                    employeesProcessed = employeeDirectory.employees.Count;
                    await UpdateBambooHREmployeePivotTable(employeeDirectory.employees);
                }

                Thread.Sleep(sleepDuration);

                // Step 2: Try to get clock entries (may fail if Time Tracking module not enabled)
                try
                {
                    MastermindLogger.Log(LogLevel.Info, $"Fetching clock entries from {periodStart:yyyy-MM-dd} to {periodEnd:yyyy-MM-dd}...");
                    var clockEntries = await bConnector.GetClockEntries(periodStart, periodEnd);

                    if (clockEntries != null && clockEntries.Count > 0)
                    {
                        MastermindLogger.Log(LogLevel.Info, $"Processing {clockEntries.Count} clock entries...");
                        await UpdateBambooHRClockEntries(clockEntries);
                        clockEntriesProcessed = clockEntries.Count;
                    }
                }
                catch (Exception clockEx)
                {
                    // Time Tracking module may not be enabled - log and continue
                    clockEntriesError = clockEx.Message;
                    MastermindLogger.Log(LogLevel.Warning, $"Clock entries unavailable (Time Tracking may not be enabled): {clockEx.Message}");
                }

                Thread.Sleep(sleepDuration);

                // Step 3: Get who's out (holidays + time-off separated)
                MastermindLogger.Log(LogLevel.Info, "Fetching WhosOut data...");
                var whosOutItems = await bConnector.GetWhosOut(periodStart, periodEnd);

                if (whosOutItems != null && whosOutItems.Count > 0)
                {
                    MastermindLogger.Log(LogLevel.Info, $"Processing {whosOutItems.Count} WhosOut items...");
                    var result = await UpdateBambooHRWhosOut(whosOutItems);
                    holidaysProcessed = result.holidays;
                }

                Thread.Sleep(sleepDuration);

                // Step 4: Get PTO-only time-off requests (replaces WhosOut for vacation tracking)
                MastermindLogger.Log(LogLevel.Info, "Fetching PTO time-off requests...");
                var timeOffRequests = await bConnector.GetTimeOffRequests(periodStart, periodEnd);

                if (timeOffRequests != null && timeOffRequests.Count > 0)
                {
                    MastermindLogger.Log(LogLevel.Info, $"Processing {timeOffRequests.Count} time-off requests...");
                    timeOffProcessed = await UpdateBambooHRTimeOffRequests(timeOffRequests);
                }

                sw.Stop();
                MastermindLogger.Log(LogLevel.Info, $"BambooHR sync completed in {sw.ElapsedMilliseconds / 1000} seconds");

                return new JsonResult
                {
                    Data = new {
                        status = "done",
                        employees = employeesProcessed,
                        clockEntries = clockEntriesProcessed,
                        clockEntriesError = clockEntriesError,
                        holidays = holidaysProcessed,
                        timeOff = timeOffProcessed
                    },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
            catch (Exception e)
            {
                MastermindLogger.Log(LogLevel.Error, $"BambooHR sync failed: {e.Message}");
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, e.Message);
            }
        }

        /// <summary>
        /// Maps BambooHR employees to Users table via ApiEmployeePivot
        /// Uses FirstName + LastName matching (same pattern as ADP)
        /// </summary>
        private async Task UpdateBambooHREmployeePivotTable(List<BambooHREmployee> employees)
        {
            if (employees == null || !employees.Any())
            {
                MastermindLogger.Log(LogLevel.Warning, "No BambooHR employees to process");
                return;
            }

            List<User> activeUsers = await db.Users.Where(x => x.Deleted == null).ToListAsync();
            List<ApiEmployeePivot> existingPivots = await db.ApiEmployeePivots
                .Where(x => x.ApiName == "BambooHR")
                .ToListAsync();

            var existingApiIds = existingPivots.Select(p => p.ApiId).ToList();

            foreach (var employee in employees)
            {
                try
                {
                    // Match by FirstName and LastName (same pattern as ADP)
                    // Consider preferredName as an alternative to firstName
                    string firstName = (!string.IsNullOrEmpty(employee.preferredName)
                        ? employee.preferredName
                        : employee.firstName)?.Trim();
                    string lastName = employee.lastName?.Trim();

                    // Try to find matching user - trim and case-insensitive
                    User matchingUser = activeUsers.SingleOrDefault(x =>
                        x.FirstName != null && x.LastName != null &&
                        x.FirstName.Trim().Equals(firstName, StringComparison.OrdinalIgnoreCase) &&
                        x.LastName.Trim().Equals(lastName, StringComparison.OrdinalIgnoreCase));

                    // If no match found with preferredName, try with firstName
                    if (matchingUser == null && !string.IsNullOrEmpty(employee.preferredName))
                    {
                        string actualFirstName = employee.firstName?.Trim();
                        matchingUser = activeUsers.SingleOrDefault(x =>
                            x.FirstName != null && x.LastName != null &&
                            x.FirstName.Trim().Equals(actualFirstName, StringComparison.OrdinalIgnoreCase) &&
                            x.LastName.Trim().Equals(lastName, StringComparison.OrdinalIgnoreCase));
                    }

                    if (matchingUser == null)
                    {
                        MastermindLogger.Log(LogLevel.Warning,
                            $"No matching user found for BambooHR employee: {employee.firstName} {employee.lastName} (preferredName: {employee.preferredName})");
                        continue;
                    }

                    if (!existingApiIds.Contains(employee.id))
                    {
                        db.ApiEmployeePivots.Add(new ApiEmployeePivot
                        {
                            ApiId = employee.id,
                            ApiName = "BambooHR",
                            UserId = matchingUser.Id
                        });
                        MastermindLogger.Log(LogLevel.Info,
                            $"Added BambooHR pivot for user: {matchingUser.FirstName} {matchingUser.LastName}");
                    }
                }
                catch (Exception ex)
                {
                    MastermindLogger.Log(LogLevel.Error,
                        $"Error processing BambooHR employee {employee.firstName} {employee.lastName}: {ex.Message}");
                }
            }

            await db.SaveChangesAsync();
        }

        /// <summary>
        /// Upserts BambooHR clock entries to ClockActivity table
        /// Uses try-catch upsert pattern (same as ADP)
        /// </summary>
        private async Task UpdateBambooHRClockEntries(List<BambooHRClockEntry> clockEntries)
        {
            if (clockEntries == null || !clockEntries.Any())
            {
                MastermindLogger.Log(LogLevel.Info, "No BambooHR clock entries to process");
                return;
            }

            foreach (var entry in clockEntries)
            {
                // Log entry details for debugging
                MastermindLogger.Log(LogLevel.Info, $"Processing entry: id={entry.id}, employeeId={entry.employeeId}, date={entry.date}, start={entry.start}, end={entry.end}, hours={entry.hours}, clockIn={entry.clockIn}, clockOut={entry.clockOut}");
                if (entry.AdditionalData != null && entry.AdditionalData.Any())
                {
                    MastermindLogger.Log(LogLevel.Info, $"Additional fields: {string.Join(", ", entry.AdditionalData.Select(kv => $"{kv.Key}={kv.Value}"))}");
                }

                // Try to get time in from various possible field names
                DateTime? timeIn = entry.clockIn;
                DateTime? timeOut = entry.clockOut;

                // If clockIn is null, try to parse from start field (may be full ISO datetime)
                if (timeIn == null && !string.IsNullOrEmpty(entry.start))
                {
                    if (DateTime.TryParse(entry.start, out DateTime parsedStart))
                    {
                        timeIn = parsedStart;
                    }
                }

                // If still null, try date field
                if (timeIn == null && !string.IsNullOrEmpty(entry.date))
                {
                    if (DateTime.TryParse(entry.date, out DateTime parsedDate))
                    {
                        timeIn = parsedDate;
                    }
                }

                // Try to parse end time
                if (timeOut == null && !string.IsNullOrEmpty(entry.end))
                {
                    if (DateTime.TryParse(entry.end, out DateTime parsedEnd))
                    {
                        timeOut = parsedEnd;
                    }
                }

                if (timeIn == null)
                {
                    MastermindLogger.Log(LogLevel.Warning, $"Skipping entry {entry.id} - could not determine time in");
                    continue;
                }

                try
                {
                    // Find the user via ApiEmployeePivot
                    var pivot = await db.ApiEmployeePivots
                        .SingleOrDefaultAsync(x => x.ApiId == entry.employeeId && x.ApiName == "BambooHR");

                    if (pivot == null)
                    {
                        MastermindLogger.Log(LogLevel.Warning,
                            $"No pivot found for BambooHR employee {entry.employeeId}");
                        continue;
                    }

                    // Prefix entry ID with "BHR_" to avoid collision with ADP entries
                    string apiEntryId = $"BHR_{entry.id}";

                    try
                    {
                        // Try to update existing
                        ClockActivity existingActivity = await db.ClockActivities
                            .SingleAsync(x => x.ApiEntryId == apiEntryId);

                        existingActivity.TimeIn = timeIn.Value.ToUniversalTime();
                        existingActivity.TimeOut = timeOut?.ToUniversalTime();
                        existingActivity.LastUpdated = DateTime.UtcNow;

                        db.Entry(existingActivity).State = EntityState.Modified;
                        MastermindLogger.Log(LogLevel.Info, $"Updated clock activity {apiEntryId}");
                    }
                    catch (Exception)
                    {
                        // Create new entry
                        db.ClockActivities.Add(new ClockActivity
                        {
                            ApiEntryId = apiEntryId,
                            Employee = pivot.UserId,
                            TimeIn = timeIn.Value.ToUniversalTime(),
                            TimeOut = timeOut?.ToUniversalTime(),
                            LastUpdated = DateTime.UtcNow
                        });
                        MastermindLogger.Log(LogLevel.Info, $"Created clock activity {apiEntryId} for user {pivot.UserId}");
                    }

                    await db.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    MastermindLogger.Log(LogLevel.Error, $"Error processing clock entry {entry.id}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Upserts BambooHR time-off requests to HolidayTimeOff table
        /// DEPRECATED: Replaced by UpdateBambooHRWhosOut which separates holidays from PTO
        /// </summary>
        private async Task UpdateBambooHRTimeOff(List<BambooHRTimeOffRequest> timeOffRequests)
        {
            if (timeOffRequests == null || !timeOffRequests.Any())
            {
                MastermindLogger.Log(LogLevel.Info, "No BambooHR time-off requests to process");
                return;
            }

            foreach (var request in timeOffRequests)
            {
                if (request.status?.status?.ToLower() != "approved")
                {
                    continue;
                }

                if (request.start == null)
                {
                    MastermindLogger.Log(LogLevel.Warning, $"Skipping request {request.id} - no start date");
                    continue;
                }

                try
                {
                    // Find the user via ApiEmployeePivot
                    var pivot = await db.ApiEmployeePivots
                        .SingleOrDefaultAsync(x => x.ApiId == request.employeeId && x.ApiName == "BambooHR");

                    if (pivot == null)
                    {
                        MastermindLogger.Log(LogLevel.Warning,
                            $"No pivot found for BambooHR employee {request.employeeId}");
                        continue;
                    }

                    // Prefix entry ID with "BHR_" to avoid collision with ADP entries
                    string apiEntryId = $"BHR_{request.id}";

                    try
                    {
                        // Try to update existing
                        HolidayTimeOff existingHoliday = await db.HolidayTimeOffs
                            .SingleAsync(x => x.ApiEntryId == apiEntryId);

                        existingHoliday.HolidayStart = request.start.Value.ToUniversalTime();
                        existingHoliday.HolidayEnd = request.end?.ToUniversalTime();
                        existingHoliday.LastUpdated = DateTime.UtcNow;

                        db.Entry(existingHoliday).State = EntityState.Modified;
                    }
                    catch (Exception)
                    {
                        // Create new entry
                        db.HolidayTimeOffs.Add(new HolidayTimeOff
                        {
                            ApiEntryId = apiEntryId,
                            Employee = pivot.UserId,
                            HolidayStart = request.start.Value.ToUniversalTime(),
                            HolidayEnd = request.end?.ToUniversalTime(),
                            LastUpdated = DateTime.UtcNow
                        });
                    }

                    await db.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    MastermindLogger.Log(LogLevel.Error, $"Error processing time-off request {request.id}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Processes WhosOut items: holidays go to HolidayTimeOffs (Employee=0, company-wide),
        /// timeOff goes to Vacations table (per-employee PTO).
        /// Returns tuple of (holidaysProcessed, timeOffProcessed).
        /// </summary>
        private async Task<(int holidays, int timeOff)> UpdateBambooHRWhosOut(List<BambooHRWhosOutItem> items)
        {
            int holidaysProcessed = 0;
            int timeOffProcessed = 0;

            if (items == null || !items.Any())
            {
                MastermindLogger.Log(LogLevel.Info, "No BambooHR WhosOut items to process");
                return (0, 0);
            }

            foreach (var item in items)
            {
                try
                {
                    if (item.Type == "holiday")
                    {
                        string apiEntryId = $"BHR_HOL_{item.Id}";

                        var existing = await db.HolidayTimeOffs
                            .SingleOrDefaultAsync(x => x.ApiEntryId == apiEntryId);

                        if (existing != null)
                        {
                            existing.HolidayStart = item.Start;
                            existing.HolidayEnd = item.Start.AddHours(8);
                            existing.LastUpdated = DateTime.UtcNow;
                            db.Entry(existing).State = EntityState.Modified;
                        }
                        else
                        {
                            db.HolidayTimeOffs.Add(new HolidayTimeOff
                            {
                                ApiEntryId = apiEntryId,
                                Employee = 0,
                                HolidayStart = item.Start,
                                HolidayEnd = item.Start.AddHours(8),
                                LastUpdated = DateTime.UtcNow
                            });
                        }

                        await db.SaveChangesAsync();
                        holidaysProcessed++;
                    }
                }
                catch (Exception ex)
                {
                    MastermindLogger.Log(LogLevel.Error, $"Error processing WhosOut item {item.Type}/{item.Id}: {ex.Message}");
                }
            }

            return (holidaysProcessed, timeOffProcessed);
        }

        private async Task<int> UpdateBambooHRTimeOffRequests(List<BambooHRTimeOffRequest> requests)
        {
            int processed = 0;

            if (requests == null || !requests.Any())
            {
                MastermindLogger.Log(LogLevel.Info, "No BambooHR time-off requests to process");
                return 0;
            }

            foreach (var req in requests)
            {
                try
                {
                    if (req.start == null) continue;

                    var pivot = await db.ApiEmployeePivots
                        .SingleOrDefaultAsync(x => x.ApiId == req.employeeId && x.ApiName == "BambooHR");

                    if (pivot == null)
                    {
                        MastermindLogger.Log(LogLevel.Warning,
                            $"No pivot found for BambooHR employee {req.employeeId} (request {req.id})");
                        continue;
                    }

                    // Build the set of days to insert from the dates dictionary
                    // dates keys are "yyyy-MM-dd", values are hours as string
                    var dayEntries = new List<(string apiEntryId, DateTime vacStart, DateTime vacEnd)>();

                    if (req.dates != null && req.dates.Any())
                    {
                        foreach (var kvp in req.dates)
                        {
                            if (!decimal.TryParse(kvp.Value, out decimal dayHours) || dayHours <= 0)
                                continue; // skip weekends and zero-amount days

                            if (!DateTime.TryParseExact(kvp.Key, "yyyy-MM-dd",
                                System.Globalization.CultureInfo.InvariantCulture,
                                System.Globalization.DateTimeStyles.None, out DateTime date))
                                continue;

                            DateTime vacStart = date.Date.AddHours(9);  // 09:00:00
                            DateTime vacEnd   = vacStart.AddHours((double)dayHours);

                            dayEntries.Add(($"BHR_{req.id}", vacStart, vacEnd));
                        }
                    }
                    else
                    {
                        // Fallback: no dates array — use total amount and single row
                        decimal totalHours = 8m;
                        if (req.amount != null && decimal.TryParse(req.amount.amount, out decimal parsed))
                            totalHours = parsed;

                        DateTime vacStart = req.start.Value.Date.AddHours(9);
                        DateTime vacEnd   = vacStart.AddHours((double)totalHours);
                        dayEntries.Add(($"BHR_{req.id}", vacStart, vacEnd));
                    }

                    if (!dayEntries.Any()) continue;

                    // Upsert each day row
                    foreach (var (apiEntryId, vacStart, vacEnd) in dayEntries)
                    {
                        var existing = await db.Vacations
                            .FirstOrDefaultAsync(x => x.ApiEntryId == apiEntryId && x.VacStart == vacStart);

                        if (existing != null)
                        {
                            existing.VacStart    = vacStart;
                            existing.VacEnd      = vacEnd;
                            existing.LastUpdated = DateTime.UtcNow;
                            db.Entry(existing).State = EntityState.Modified;
                        }
                        else
                        {
                            db.Vacations.Add(new Vacation
                            {
                                ApiEntryId  = apiEntryId,
                                Employee    = pivot.UserId,
                                VacStart    = vacStart,
                                VacEnd      = vacEnd,
                                LastUpdated = DateTime.UtcNow
                            });
                        }
                    }

                    await db.SaveChangesAsync();
                    processed++;
                }
                catch (Exception ex)
                {
                    MastermindLogger.Log(LogLevel.Error,
                        $"Error processing time-off request {req.id}: {ex.Message}");
                }
            }

            return processed;
        }

        #endregion

        public async Task<decimal> GenerateTrueItemWidth(int itemId, string itemType)
        {
            switch (itemType.ToUpper())
            {
                case "QUOTE":
                    QuoteItem qi = await db.QuoteItems.FindAsync(itemId);

                    if (qi != null)
                    {
                        // Check if MarginXR is null or empty and load the press record if needed
                        if (String.IsNullOrEmpty(qi.MarginXR))
                        {
                            await db.Entry(qi).Reference(r => r.Press1).LoadAsync();
                            qi.MarginXR = qi.Press1?.MarginRight ?? "0"; // Use the marginXR from the press record
                            qi.MarginXL = qi.Press1?.MarginLeft ?? "0";
                        }

                        // Get the item width and side margins
                        decimal w = Decimal.Parse(qi.Width ?? "0");
                        decimal mxl = Decimal.Parse(qi.MarginXL ?? "0");
                        decimal mxr = Decimal.Parse(qi.MarginXR ?? "0");

                        // Adjust the item width by adding on the side margins
                        w = w + (mxl + mxr);

                        return w;
                    }

                    // Return 0 if item not found
                    return 0m;

                case "JOB":
                    JobItem ji = await db.JobItems.FindAsync(itemId);

                    if (ji != null)
                    {
                        if (String.IsNullOrEmpty(ji.MarginXR))
                        {
                            await db.Entry(ji).Reference(r => r.Press1).LoadAsync();
                            ji.MarginXR = ji.Press1?.MarginRight ?? "0"; // Use the marginXR from the press record
                            ji.MarginXL = ji.Press1?.MarginLeft ?? "0";
                        }
                        //get the item width and side margins
                        //adjust the item width by adding on the side margins
                        decimal w = Decimal.Parse(ji.Width ?? "0");
                        decimal mxl = Decimal.Parse(ji.MarginXL ?? "0");
                        decimal mxr = Decimal.Parse(ji.MarginXR ?? "0");

                        w = w + (mxl + mxr);

                        return w;
                    }

                    //return 0 if item not found
                    return 0m;

                default:
                    return 0m;
            }
        }

        public async Task<decimal> GenerateTrueItemLength(int itemId, string itemType)
        {
            switch (itemType.ToUpper())
            {
                case "QUOTE":
                    QuoteItem qi = await db.QuoteItems.FindAsync(itemId);

                    if (qi != null)
                    {
                        if (String.IsNullOrEmpty(qi.MarginYB))
                        {
                            await db.Entry(qi).Reference(r => r.Press1).LoadAsync();
                            qi.MarginYB = qi.Press1?.MarginBottom ?? "0"; // Use the marginYB from the press record
                            qi.MarginYT = qi.Press1?.MarginTop ?? "0"; //use the margin top from the press record
                        }
                        //get the item height and top/bottom margins
                        //figure out the new height
                        decimal l = Decimal.Parse(qi.Length ?? "0");
                        decimal myt = Decimal.Parse(qi.MarginYT ?? "0");
                        decimal myb = Decimal.Parse(qi.MarginYB ?? "0");

                        l = l + (myt + myb);

                        return l;
                    }

                    //return 0 if item not found
                    return 0m;

                case "JOB":
                    JobItem ji = await db.JobItems.FindAsync(itemId);

                    if (ji != null)
                    {
                        if (String.IsNullOrEmpty(ji.MarginYB))
                        {
                            await db.Entry(ji).Reference(r => r.Press1).LoadAsync();
                            ji.MarginYB = ji.Press1?.MarginBottom ?? "0"; // Use the marginYB from the press record
                            ji.MarginYT = ji.Press1?.MarginTop ?? "0"; //use the margin top from the press record
                        }

                        //get the item length and y margins
                        //Get the new height
                        decimal w = Decimal.Parse(ji.Length ?? "0");
                        decimal myt = Decimal.Parse(ji.MarginYT ?? "0");
                        decimal myb = Decimal.Parse(ji.MarginYB ?? "0");

                        w = w + (myt + myb);

                        return w;
                    }

                    //return 0 if item not found
                    return 0m;

                default:
                    return 0m;
            }
        }

        protected DateTime GetStartOfLastMonth()
        {
            DateTime lastMonth = DateTime.Now.AddMonths(-1);

            DateTime startOfLastMonth = new DateTime(lastMonth.Year, lastMonth.Month, 1);

            return startOfLastMonth;
        }

        protected DateTime GetEndOfLastMonth()
        {
            DateTime lastMonth = DateTime.Now.AddMonths(-1);

            DateTime endOfLastMonth = new DateTime(lastMonth.Year, lastMonth.Month, DateTime.DaysInMonth(lastMonth.Year, lastMonth.Month));

            return endOfLastMonth;
        }

        protected DateTime GetStartOfLastWeek()
        {
            DateTime now = DateTime.Now;

            while (now.DayOfWeek != DayOfWeek.Sunday)
                now = now.AddDays(-1);

            now = now.AddDays(-7);

            return now.Date;
        }

        protected DateTime GetEndOfLastWeek()
        {
            DateTime now = DateTime.Now;

            while (now.DayOfWeek != DayOfWeek.Monday)
                now = now.AddDays(-1);

            now = now.AddDays(-2);

            return now.Date;
        }

        private PriceListViewModel GetPriceListViewModel(List<PriceItemViewModel> items)
        {
            return new PriceListViewModel
            {
                Items = items
            };
        }

        protected PriceListViewModel GetPriceListViewModelWrapper(Job j)
        {
            var viewModel = GetPriceListViewModel(
                    j.JobPrices
                    .Where(x => x.JobItemService != -1) //-1 implies a material price on a record that does not use a printable material, so it can be ignored
                    .OrderBy(x => x.JobItem1 == null ? 0 : x.JobItem1.Ordinal)
                    .ThenBy(x => x.JobItemService1 == null ? 0 : x.JobItemService1.ApexOrdinal)
                    .GroupBy(x => new
                    {
                        JobItemId = (x.JobItem ?? 0),
                        ItemName = x.JobItem1 == null ? "" : x.JobItem1.Description
                    })
                    .Select(g => new PriceItemViewModel
                    {
                        JobItemId = g.Key.JobItemId,
                        ItemName = g.Key.ItemName,
                        Ordinal = g.First().JobItem1 == null ? 0 : (g.First().JobItem1.Ordinal ?? 0),
                        PriceRecords = g.Select(x => new PriceRecordViewModel
                        {
                            Id = x.Id,
                            Reason = x.Title,
                            DurationOrQuantity = (x.Quantity ?? 0m) > 0m
                                      ? x.Quantity.Value.ToString()
                                      : string.Format("{0} ({1})", DecimalHoursToFormatted(x.Duration ?? 0m), (x.Duration ?? 0m).ToString()),
                            Cost = x.Cost ?? 0m,
                            Markup = x.Markup ?? 0m,
                            Price = x.Price ?? 0m,
                            OverriddenMarkup =
                                (x.JobItemService1 != null && x.JobItemService1.CustomMarkup != null)
                                || (x.IsMaterial == true && x.JobItem1 != null && x.JobItem1.CustomMaterialMarkup != null)
                                || (x.Title != null && x.Title.Contains("Ink Cost") && x.JobItem1 != null && x.JobItem1.CustomInkMarkup != null)
                                || (x.Title != null && x.Title.Contains("Labor Cost") && x.JobItem1 != null && x.JobItem1.CustomPrintLaborMarkup != null)
                                || (x.OverriddenMarkup == true),
                            Ordinal = x.JobItemService1 == null ? 0 : (x.JobItemService1.ApexOrdinal ?? 0)
                        }).ToList(),

                        // Optionally set a default GroupMarkup if needed; otherwise, leave as null.
                        GroupMarkup = null
                    }).ToList()
                );

            // Populate SQFT pricing properties
            viewModel.RecordId = j.Id;
            viewModel.RecordType = "JOB";
            viewModel.UseSqftPricing = j.UseSqftPricing ?? false;
            viewModel.SqftPriceOverride = j.SqftPriceOverride;

            return viewModel;
        }

        protected PriceListViewModel GetPriceListViewModelWrapper(Quote q)
        {
            var viewModel = GetPriceListViewModel(
                    q.QuotePrices
                    .Where(x => x.QuoteItemService != -1) // -1 here implies a material price on a record that does not use a printable material, so it can be ignored
                    .OrderBy(x => x.QuoteItem1 == null ? 0 : x.QuoteItem1.Ordinal)
                    .ThenBy(x => x.QuoteItemService1 == null ? 0 : x.QuoteItemService1.ApexOrdinal)
                    .GroupBy(x => new
                    {
                        JobItemId = (x.QuoteItem ?? 0),
                        ItemName = x.QuoteItem1 == null ? "" : x.QuoteItem1.Description
                    })
                    .Select(g => new PriceItemViewModel
                    {
                        JobItemId = g.Key.JobItemId,
                        ItemName = g.Key.ItemName,
                        Ordinal = g.First().QuoteItem1 == null ? 0 : (g.First().QuoteItem1.Ordinal ?? 0),
                        PriceRecords = g.Select(x => new PriceRecordViewModel
                        {
                            Id = x.Id,
                            Reason = x.Title,
                            DurationOrQuantity = (x.Quantity ?? 0m) > 0m
                                      ? x.Quantity.Value.ToString()
                                      : string.Format("{0} ({1})", DecimalHoursToFormatted(x.Duration ?? 0m), (x.Duration ?? 0m).ToString()),
                            Cost = x.Cost ?? 0m,
                            Markup = x.Markup ?? 0m,
                            Price = x.Price ?? 0m,
                            OverriddenMarkup =
                                (x.QuoteItemService1 != null && x.QuoteItemService1.CustomMarkup != null)
                                || (x.IsMaterial == true && x.QuoteItem1 != null && x.QuoteItem1.CustomMaterialMarkup != null)
                                || (x.Title != null && x.Title.Contains("Ink Cost") && x.QuoteItem1 != null && x.QuoteItem1.CustomInkMarkup != null)
                                || (x.Title != null && x.Title.Contains("Labor Cost") && x.QuoteItem1 != null && x.QuoteItem1.CustomPrintLaborMarkup != null)
                                || (x.OverriddenMarkup == true),
                            Ordinal = x.QuoteItemService1 == null ? 0 : (x.QuoteItemService1.ApexOrdinal ?? 0)
                        }).ToList(),

                        // Optionally set a default GroupMarkup if needed; otherwise, leave as null.
                        GroupMarkup = null
                    }).ToList()
                );

            // Populate SQFT pricing properties
            viewModel.RecordId = q.Id;
            viewModel.RecordType = "QUOTE";
            viewModel.UseSqftPricing = q.UseSqftPricing ?? false;
            viewModel.SqftPriceOverride = q.SqftPriceOverride;

            return viewModel;
        }
    }

    public static class StringExtensions
    {
        public static string TrimLongString(this string str, int length)
        {
            string ellipses = "...";

            //if the string is less than the cutoff length then return the string
            if (str.Length < length)
                return str;
            else
            {
                //otherwise, substring and append ellipses
                string altString = str.Substring(0, length);
                altString += ellipses;

                return altString;
            }
        }

        public static string SanitizeForKey(this string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                throw new ArgumentNullException(nameof(input));
            }

            //replace the string to remove any characters that are not alphnumeric
            return Regex.Replace(input, @"[^0-9a-zA-Z]+", "");
        }

        public static string SplitCamelCase(this string str)
        {
            string theResult = Regex.Replace(
                Regex.Replace(
                    str,
                    @"(\P{Ll})(\P{Ll}\p{Ll})",
                    "$1 $2"
                ),
                @"(\p{Ll})(\P{Ll})",
                "$1 $2"
            );

            //capitalize the first character of theResult
            return theResult.Substring(0, 1).ToUpper() + theResult.Substring(1);
        }

        public static string ToCamelCase(this string str)
        {
            //split str on space
            string[] strSplit = str.Split(' ');
            List<string> camelCaseString = new List<string>();
            if (strSplit.Length > 0)
            {
                for (int i = 0; i < strSplit.Length; i++)
                {
                    string theString = strSplit[i];

                    if (i == 0)
                        theString = theString.Substring(0, 1).ToLower() + theString.Substring(1);
                    else
                        theString = theString.Substring(0, 1).ToUpper() + theString.Substring(1).ToLower();

                    camelCaseString.Add(theString);
                }
            }

            return string.Join("", camelCaseString);
        }
    }
}