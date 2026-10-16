using FifthPress.ActionFilters;
using FifthPress.Models;
using FifthPress.Models.ControllerModels;
using FifthPress.Models.ViewModels;
using FifthPress.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.UI.WebControls;
using FifthPress.Models.Calculations;
using System.Data.Entity.Core.Objects;
using FifthPress.Models.Deserialization;
using System.Text.RegularExpressions;

namespace FifthPress.Controllers
{
    [CookieFilter(Order = 1)]
    [UserPermissions(Order = 2)]
    public class JobController : DataController
    {
        private async Task<List<IGrouping<string, GetJobCostSheetTVF_Result>>> CreateCostSheetRollUpRecords(List<VM_JobCostSheetGroup> ItemGroups)
        {
            List<IGrouping<string, GetJobCostSheetTVF_Result>> GroupedByService =
                ItemGroups.SelectMany(x => x.ItemPrices).GroupBy(y => y.EntryTitle).ToList();

            return GroupedByService;
        }

        private bool PassedQualityCheck(QAApproval qa)
        {
            if (qa.FirstApproverPass && qa.SecondApproverPass)
                return true;
            else
                return false;
        }

        public async Task<ActionResult> ItemsReadyToShip()
        {
            List<ItemsReadyToShip_Result> items = await db.ItemsReadyToShip().ToListAsync();

            return View(items);
        }

        public async Task<ActionResult> ShowBilledJobs()
        {
            var billedJobs =
                await db.Jobs
                    .Where(x => x.Deleted == null && x.Billed != null)
                    .Include(i => i.Client1)
                    .Include(i => i.User)
                    .ToListAsync();

            return View(billedJobs);
        }

        public async Task<ActionResult> LoadItemReorderModal(int? id, string itemType)
        {
            itemType = itemType ?? "";

            switch (itemType)
            {
                case "quote":
                    Quote q = await db.Quotes.FindAsync(id);

                    if (q != null)
                    {
                        await db.Entry(q).Collection(c => c.QuoteItems).LoadAsync();

                        var quoteItemDetails = q.QuoteItems.OrderBy(x => x.Ordinal).Select(x => new
                        {
                            x.Id,
                            x.Description,
                            x.Ordinal,
                            x.Quantity,
                            x.Length,
                            x.Width
                        }).ToList();

                        return new JsonResult
                        {
                            Data = quoteItemDetails,
                            JsonRequestBehavior = JsonRequestBehavior.AllowGet
                        };
                    }

                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Job not found");

                case "job":
                    Job j = await db.Jobs.FindAsync(id);

                    if (j != null)
                    {
                        await db.Entry(j).Collection(r => r.JobItems).LoadAsync();

                        var jobItemDetails = j.JobItems.OrderBy(x => x.Ordinal).Select(x => new
                        {
                            x.Id,
                            x.Description,
                            x.Ordinal,
                            x.Quantity,
                            x.Length,
                            x.Width
                        }).ToList();

                        return new JsonResult
                        {
                            Data = jobItemDetails,
                            JsonRequestBehavior = JsonRequestBehavior.AllowGet
                        };
                    }

                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Job not found");

                default:
                    return new HttpStatusCodeResult(HttpStatusCode.OK);
            }
        }

        public async Task<ActionResult> UpdateItemPositions(List<int> itemIds, List<int> ordinals, string itemType)
        {
            switch (itemType)
            {
                case "quote":
                    int quoteId = 0;

                    for (int i = 0; i < itemIds.Count; i++)
                    {
                        QuoteItem qi = await db.QuoteItems.FindAsync(itemIds[i]);
                        quoteId = qi.Quote ?? 0;
                        qi.Ordinal = ordinals[i];
                        db.Entry(qi).State = EntityState.Modified;
                    }

                    await db.SaveChangesAsync();

                    Quote q = await db.Quotes.FindAsync(quoteId);
                    await db.Entry(q).Collection(c => c.QuoteItems).LoadAsync();

                    //load foreign key entities
                    foreach (QuoteItem qi in q.QuoteItems.ToList())
                    {
                        string unqKey = "hasDutchCut_" + qi.Id.ToString();
                        ViewData[unqKey] = false;

                        try
                        {
                            ItemLayoutSetting ils = await db.ItemLayoutSettings.SingleAsync(x => x.QuoteItemId == qi.Id);
                            ViewData[unqKey] = ils.DutchCut;
                        }
                        catch (Exception e)
                        {
                            //no record found; move on
                        }

                        if (!qi.MaterialCost.HasValue)
                            qi.MaterialCost = 0m;
                        if (!qi.Sheets.HasValue)
                            qi.Sheets = 1;

                        await db.Entry(qi).Collection(c => c.QuoteItemServices).LoadAsync();
                        await db.Entry(qi).Reference(r => r.Press1).LoadAsync();
                        await db.Entry(qi).Reference(r => r.Material1).LoadAsync();
                        await db.Entry(qi).Collection(c => c.QuoteItemFinishingMaterials).LoadAsync();

                        foreach (QuoteItemFinishingMaterial qifm in qi.QuoteItemFinishingMaterials)
                        {
                            await db.Entry(qifm).Reference(r => r.Material1).LoadAsync();
                            await db.Entry(qifm).Reference(r => r.FinishingMaterialApplication).LoadAsync();

                            Dictionary<string, string> d = JsonConvert.DeserializeObject<Dictionary<string, string>>((qifm.FinishingFormulaConfiguration ?? "{\"\":\"\"}"));
                            qifm.ConfigLabel = d.First().Key.Split('_')[0];
                            qifm.ConfigValue = d.First().Value;
                        }

                        if (qi.Material.HasValue)
                        {
                            await db.Entry(qi.Material1).Reference(r => r.MaterialStockType).LoadAsync();
                            await db.Entry(qi.Material1).Reference(r => r.MaterialCostType).LoadAsync();

                            qi.MaterialCostPerUnitFormatted = qi.MaterialCostPerUnit.HasValue ? qi.MaterialCostPerUnit.Value.ToString("C") : "0.00"; //.Value because should always have a value
                            qi.MaterialCostFormatted = (qi.MaterialCost ?? 0m).ToString("C");
                        }
                        else
                        {
                            qi.MaterialCostPerUnit = 0m;
                            qi.MaterialCostPerUnitFormatted = qi.MaterialCostPerUnit.Value.ToString("C");
                            qi.MaterialCostFormatted = (qi.MaterialCost ?? 0m).ToString("C");
                        }
                        await db.Entry(qi).Reference(r => r.SpeedDenominator).LoadAsync();
                        /*
                        if (qi.Press.HasValue)
                        {
                            await db.Entry(qi.Press1).Reference(r => r.SpeedDenominator1).LoadAsync();
                        }*/

                        try
                        {
                            foreach (QuoteItemService qis in qi.QuoteItemServices)
                            {
                                await db.Entry(qis).Reference(r => r.Service).LoadAsync();
                                if (qis.Service == null) continue;
                                await db.Entry(qis.Service).Reference(r => r.ServiceCategory).LoadAsync();
                                await db.Entry(qis.Service.ServiceCategory).Reference(r => r.ServiceType1).LoadAsync();
                                qis.FormattedServiceTime = DecimalHoursToFormatted(qis.ServiceTime);
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }

                        if (qi.Material.HasValue)
                        {
                            qi.Material1.ParseDimensions();
                        }
                        qi.ParseRunSize();

                        qi.FormattedPrintTime = DecimalHoursToFormatted(qi.PrintTime);

                        qi.DocumentCount = await db.QuoteDocuments.Where(x => x.ItemId == qi.Id && x.RowType == "Standard").CountAsync();

                        //null coalesce any null'd checkboxes
                        qi.DoubleHit = qi.DoubleHit ?? false;
                        qi.Staple = qi.Staple ?? false;
                        qi.Collate = qi.Collate ?? false;
                    }

                    return new JsonResult
                    {
                        Data = RenderViewToString("~/Views/Partials/_QuoteItemRows.cshtml", q.QuoteItems.ToList()),
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };

                case "job":
                    int jobId = 0;

                    for (int i = 0; i < itemIds.Count; i++)
                    {
                        JobItem ji = await db.JobItems.FindAsync(itemIds[i]);
                        jobId = ji.Job ?? 0;
                        ji.Ordinal = ordinals[i];
                        db.Entry(ji).State = EntityState.Modified;
                    }

                    await db.SaveChangesAsync();

                    Job j = await db.Jobs.FindAsync(jobId);
                    await db.Entry(j).Collection(c => c.JobItems).LoadAsync();

                    //load foreign key entities
                    foreach (JobItem ji in j.JobItems.ToList())
                    {
                        string unqKey = "hasDutchCut_" + ji.Id.ToString();
                        ViewData[unqKey] = false;

                        try
                        {
                            ItemLayoutSetting ils = await db.ItemLayoutSettings.SingleAsync(x => x.JobItemId == ji.Id);
                            ViewData[unqKey] = ils.DutchCut;
                        }
                        catch (Exception e)
                        {
                            //no record found; move on
                        }

                        if (!ji.MaterialCost.HasValue)
                            ji.MaterialCost = 0m;
                        if (!ji.Sheets.HasValue)
                            ji.Sheets = 1;

                        await db.Entry(ji).Collection(c => c.JobItemServices).LoadAsync();
                        await db.Entry(ji).Reference(r => r.Press1).LoadAsync();
                        await db.Entry(ji).Reference(r => r.Material1).LoadAsync();
                        await db.Entry(ji).Collection(c => c.JobItemFinishingMaterials).LoadAsync();

                        foreach (JobItemFinishingMaterial jifm in ji.JobItemFinishingMaterials)
                        {
                            await db.Entry(jifm).Reference(r => r.Material1).LoadAsync();
                            await db.Entry(jifm).Reference(r => r.FinishingMaterialApplication).LoadAsync();

                            Dictionary<string, string> d = JsonConvert.DeserializeObject<Dictionary<string, string>>((jifm.FinishingFormulaConfiguration ?? "{\"\":\"\"}"));
                            jifm.ConfigLabel = d.First().Key.Split('_')[0];
                            jifm.ConfigValue = d.First().Value;
                        }

                        if (ji.Material.HasValue)
                        {
                            await db.Entry(ji.Material1).Reference(r => r.MaterialStockType).LoadAsync();
                            await db.Entry(ji.Material1).Reference(r => r.MaterialCostType).LoadAsync();

                            ji.MaterialCostPerUnitFormatted = ji.MaterialCostPerUnit.HasValue ? ji.MaterialCostPerUnit.Value.ToString("C") : "0.00";
                            ji.MaterialCostFormatted = (ji.MaterialCost ?? 0m).ToString("C");
                        }
                        else
                        {
                            ji.MaterialCostPerUnit = 0m;
                            ji.MaterialCostPerUnitFormatted = ji.MaterialCostPerUnit.Value.ToString("C");
                            ji.MaterialCostFormatted = (ji.MaterialCost ?? 0m).ToString("C");
                        }

                        await db.Entry(ji).Reference(r => r.SpeedDenominator).LoadAsync();
                        //if (ji.Press.HasValue)
                        //    await db.Entry(ji.Press1).Reference(r => r.SpeedDenominator1).LoadAsync();

                        foreach (JobItemService jis in ji.JobItemServices)
                        {
                            await db.Entry(jis).Reference(r => r.Service).LoadAsync();
                            await db.Entry(jis.Service).Reference(r => r.ServiceCategory).LoadAsync();
                            await db.Entry(jis.Service.ServiceCategory).Reference(r => r.ServiceType1).LoadAsync();
                            jis.FormattedServiceTime = DecimalHoursToFormatted(jis.ServiceTime);
                        }

                        if (ji.Material.HasValue)
                        {
                            ji.Material1.ParseDimensions();
                        }

                        ji.ParseRunSize();

                        ji.FormattedPrintTime = DecimalHoursToFormatted(ji.PrintTime);

                        ji.DocumentCount = await db.JobDocuments.Where(x => x.ItemId == ji.Id && x.RowType == "Standard").CountAsync();

                        //null coalesce any null'd checkboxes to false
                        ji.DoubleHit = ji.DoubleHit ?? false;
                        ji.Staple = ji.Staple ?? false;
                        ji.Collate = ji.Collate ?? false;
                    }

                    return new JsonResult
                    {
                        Data = RenderViewToString("~/Views/Partials/_JobItemRows.cshtml", j.JobItems.ToList()),
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };

                default:
                    return new HttpStatusCodeResult(HttpStatusCode.OK);
            }
        }

        public async Task<ActionResult> GetPrebillJobs()
        {
            //get jobs that are not deleted, have a shipped value and do not have a released to billing date
            var jobs = await db.Jobs
                .Where(x => !x.Deleted.HasValue && x.Shipped.HasValue && !x.ReleasedToBillingDate.HasValue)
                .Include(i => i.Client1)
                .Include(i => i.User)
                .Include(i => i.OrdersShippeds)
                .Include(i => i.JobItems.Select(ji => ji.JobShipmentPivots.Select(jsp => jsp.JobShipment)))
                .ToListAsync();

            return View(jobs);
        }

        public async Task<ActionResult> MarkAsPrebilled(int? jobId)
        {
            Job j = await db.Jobs.FindAsync(jobId);

            if (j != null)
            {
                j.ReleasedToBillingDate = DateTime.UtcNow;
                db.Entry(j).State = EntityState.Modified;
                await db.SaveChangesAsync();

                return new HttpStatusCodeResult(HttpStatusCode.OK);
            }

            return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "job not found");
        }

        public async Task<ActionResult> MarkJobAsShipped(int? jobId)
        {
            Job j = await db.Jobs.FindAsync(jobId);

            if (j != null)
            {
                j.Shipped = DateTime.UtcNow;
                db.Entry(j).State = EntityState.Modified;
                await db.SaveChangesAsync();
            }

            return new HttpStatusCodeResult(HttpStatusCode.OK);
        }

        public async Task<ActionResult> GetReadyToShipJobs()
        {
            var readyToShip =
                await db.Jobs
                    .Where(x => x.Deleted == null && x.Client != null && x.DueDate != null && x.Complete != null && x.Shipped == null)
                    .Include(i => i.Client1)
                    .ToListAsync();

            return View(readyToShip);
        }

        public async Task<ActionResult> NavbarDuoLookup(string q)
        {
            //this function expects a full number, so do a single lookup
            bool isJob = Regex.IsMatch(q, @"^\d{2}-");

            try
            {
                if (isJob)
                {
                    Job j = await db.Jobs.SingleAsync(x => x.JobNumber == q);

                    return RedirectToRoute("EditJobSingle", new { jobId = j.Id });
                }
                else
                {
                    Quote qu = await db.Quotes.SingleAsync(x => x.JobNumber == q);

                    return RedirectToRoute("EditQuoteSingle", new { quoteId = qu.Id });
                }
            }
            catch (Exception e)
            {
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ActionResult> QAComplete(QAApproval qa)
        {
            int userId = 0;
            try
            {
                userId = UserSessionHelper.GetActiveUserId(HttpContext);
            }
            catch (Exception)
            {
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Critical error saving QA Record");
            }

            bool passedQA = PassedQualityCheck(qa);

            DateTime timeNow = DateTime.UtcNow;

            QACheck q = new QACheck
            {
                ApprovalJson = JsonConvert.SerializeObject(qa.Responses, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                FirstApprovalTime = qa.FirstApprovalTime.ToUniversalTime(),
                SecondApprovalTime = timeNow,
                FirstApprover = userId,
                SecondApprover = qa.SecondApprover == -1 ? null : qa.SecondApprover,
                JobItemServiceId = qa.JobItemService,
                Passed = passedQA
            };

            db.QAChecks.Add(q);

            if (!passedQA)
            {
                try
                {
                    JobTimingSR jtClose = await db.JobTimingSRs.SingleAsync(x => x.JobItemService == qa.JobItemService && x.UserId == userId && x.EndTimeUTC == null);
                    jtClose.EndTimeUTC = timeNow;
                    db.Entry(jtClose).State = EntityState.Modified;
                }
                catch (Exception)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "SingleAsync() error when closing JTSR");
                }
            }

            try
            {
                await db.SaveChangesAsync();
            }
            catch (Exception e)
            {
            }
            return new JsonResult
            {
                Data = "Done",
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> QAJobDetails()
        {
            int userId = 0;

            try
            {
                userId = UserSessionHelper.GetActiveUserId(HttpContext);
                JobTimingSR jt = await db.JobTimingSRs.SingleAsync(x => x.UserId == userId && x.EndTimeUTC == null &&
                    (x.JobItemService1.Service.Title == "Print" || (x.JobItemService1.RequiresQualityCheck ?? false)));
                Job j = await db.Jobs.FindAsync(jt.Job);
                JobItem ji = await db.JobItems.FindAsync(jt.JobItem);
                JobItemService jis = await db.JobItemServices.FindAsync(jt.JobItemService);
                await db.Entry(ji).Reference(r => r.Material1).LoadAsync();
                await db.Entry(jis).Reference(r => r.Service).LoadAsync();

                bool isPrintQA = jis.Service.Title == "Print";

                // Get current time
                // If M-F 8am->5pm, do NOT allow bypass
                DateTime now = DateTime.Now;
                bool isWorkday = now.DayOfWeek >= DayOfWeek.Monday && now.DayOfWeek <= DayOfWeek.Friday;
                bool isWorkHours = now.Hour >= 8 && now.Hour < 17; // exclusive upper bound to avoid 5:01pm being included

                bool allowQABypass = !(isWorkday && isWorkHours);

                //otherwise, allow bypass

                var jobDetails = new
                {
                    j.JobNumber,
                    ji.Ordinal,
                    MaterialName = ji.Material1 != null ? ji.Material1.Title : "",
                    ji.MaterialRunSize,
                    ji.PressColorProfileFront,
                    ji.PressColorProfileBack,
                    jt.JobItemService,
                    allowQABypass,
                    userId,
                    PrintNotes = jis.Notes ?? "",
                    IsPrintQA = isPrintQA,
                    ItemDescription = ji.Description ?? "",
                    ServiceTitle = jis.Title ?? ""
                };

                return new JsonResult
                {
                    Data = JsonConvert.SerializeObject(
                            jobDetails,
                            new JsonSerializerSettings
                            {
                                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                            }
                        ),
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
            catch (Exception)
            {
                //general catch; return empty model for this?
                return new JsonResult
                {
                    Data = JsonConvert.SerializeObject(
                            new
                            {
                                JobNumber = "",
                                Ordinal = 0,
                                MaterialName = "",
                                MaterialRunSize = "",
                                PressColorProfileFront = "",
                                PressColorProfileBack = "",
                                JobItemService = 0,
                                IsPrintQA = false,
                                ItemDescription = "",
                                ServiceTitle = ""
                            },
                            new JsonSerializerSettings
                            {
                                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                            }
                        ),
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
        }

        public async Task<ActionResult> GetCostSheet(int? jobId)
        {
            Job j = await db.Jobs.FindAsync(jobId);

            if (j != null)
            {
                await db.Entry(j).Collection(c => c.JobItems).LoadAsync();
                await db.Entry(j).Reference(r => r.Client1).LoadAsync();
                await db.Entry(j).Reference(r => r.ShippingAddress1).LoadAsync();
                await db.Entry(j).Collection(c => c.JobOutsourcedItems).LoadAsync();
                await db.Entry(j).Reference(r => r.Contact1).LoadAsync();
                await db.Entry(j).Reference(r => r.ShippingOption).LoadAsync();
                await db.Entry(j).Reference(r => r.User).LoadAsync();

                if (j.ShippingAddress.HasValue)
                {
                    await db.Entry(j.ShippingAddress1).Reference(r => r.USState).LoadAsync();
                }

                if (j.Client.HasValue)
                {
                    await db.Entry(j.Client1).Reference(r => r.USState).LoadAsync();
                }

                try
                {
                    //run f(x)
                    List<GetJobCostSheetTVF_Result> jobCosts = await db.GetJobCostSheetTVF(jobId).ToListAsync();

                    foreach (GetJobCostSheetTVF_Result res in jobCosts)
                    {
                        //ensure we have formatted times for each record
                        res.ActualTimeFormatted = DecimalHoursToFormatted(res.DurationHours);
                        res.ChargeTimeFormatted = DecimalHoursToFormatted(res.ChargedTime);
                    }

                    //get job item ids
                    List<int?> distinctJobItemIds = jobCosts.Select(x => x.ItemId).Distinct().ToList();

                    //new vm
                    VM_JobCostSheet costsheetVM = new VM_JobCostSheet
                    {
                        Job = j,
                        ItemGroups = new List<VM_JobCostSheetGroup>(),
                        JobTotals = new VM_AggregateTotals()
                    };

                    //all real time prices for this job
                    List<GetRealTimeJobCostRecords_Result> realtime = await db.GetRealTimeJobCostRecords(j.Id).ToListAsync();

                    //create new JobCostSheetGroups per item
                    foreach (int i in distinctJobItemIds)
                    {
                        VM_JobCostSheetGroup jcsg = new VM_JobCostSheetGroup
                        {
                            ItemDescription = jobCosts.Where(x => x.ItemId == i).First().ItemDescription,
                            ItemPrices = jobCosts.Where(x => x.ItemId == i).ToList(),
                            ItemTotals = new VM_ItemLevelTotal()
                        };

                        //all real time prices for this item
                        var realTimePrices = realtime.Where(x => x.Item_Id == i).ToList();
                        int debugctr = 0;

                        foreach (GetJobCostSheetTVF_Result res in jcsg.ItemPrices)
                        {
                            res.ChargedPriceString = (res.ChargedPrice ?? 0m).ToString("N2");

                            if (res.EntryTitle.Contains("Ink Cost") || res.EntryTitle == "Outsource")
                            {
                                res.HourlyRate = null;
                            }
                            if (res.ItemServiceId.HasValue)
                            {
                                GetRealTimeJobCostRecords_Result rtInfo = null;

                                try
                                {
                                    if (res.EntryTitle.Contains("Ink Cost"))
                                        rtInfo = realTimePrices.Single(x => x.JIS_Id == res.ItemServiceId && x.Service_Title.Contains("Ink Cost"));
                                    else if (res.EntryTitle.Contains("Labor Cost"))
                                        rtInfo = realTimePrices.Single(x => x.JIS_Id == res.ItemServiceId && x.Service_Title.Contains("Labor Cost"));
                                    else
                                        rtInfo = realTimePrices.Single(x => x.JIS_Id == res.ItemServiceId);
                                }
                                catch (Exception e)
                                {
                                    rtInfo = new GetRealTimeJobCostRecords_Result
                                    {
                                        Internal_Cost = 0m,
                                        Time_Tracked = 0m
                                    };
                                }
                                res.InternalCost = rtInfo.Internal_Cost ?? 0m;
                                res.InternalCostString = res.InternalCost.ToString("N2");
                                //res.SpreadAmount = (res.EstimatedPrice ?? 0m) - (res.EstimatedCost ?? 0m);
                                res.SpreadAmount = (res.ChargedPrice ?? 0m) - (res.InternalCost);
                                res.PositiveSpread = res.SpreadAmount >= 0m;
                                res.SpreadAmountString = res.SpreadAmount.ToString("N2");
                                res.ActualTime = rtInfo.Time_Tracked;
                                res.ActualTimeFormatted = DecimalHoursToFormatted(rtInfo.Time_Tracked);

                                if (res.IsMaterial == 1)
                                {
                                    //unit cost should be InternalCost / DurationHours (which is quantity in this context)
                                    res.UnitCost = res.InternalCost / Convert.ToInt32((res.DurationHours == 0m ? 1m : res.DurationHours));
                                    res.UnitCostString = (res.UnitCost ?? 0m).ToString("N4");
                                }
                            }
                            else
                            {
                                //I think this is only for finishing materials?

                                //set internal cost to estimated cost because these are untimed
                                res.InternalCost = res.ChargedCost ?? 0m;
                                res.InternalCostString = (res.ChargedCost ?? 0m).ToString("N2");
                                res.SpreadAmount = (res.ChargedPrice ?? 0m) - (res.ChargedCost ?? 0m);
                                res.PositiveSpread = res.SpreadAmount >= 0m;
                                res.SpreadAmountString = res.SpreadAmount.ToString("N2");

                                //finishing material unit cost
                                res.UnitCost = res.InternalCost / Convert.ToInt32(res.DurationHours == 0m ? 1m : res.DurationHours);
                                res.UnitCostString = (res.UnitCost ?? 0m).ToString("N4");
                            }

                            if (res.EntryTitle.Contains("Ink Cost"))
                            {
                                res.ActualTime = null;
                                res.ActualTimeFormatted = "";
                                res.ChargedTime = null;
                                res.ChargeTimeFormatted = "";
                            }

                            //then, generate the unit cost string
                            res.PercentDifference = CalculatePercentDifference((res.ChargedPrice ?? 0m), res.InternalCost);

                            res.PercentDifferenceString = res.PercentDifference.ToString("N2");
                        }

                        jcsg.ItemTotals.ChargedPrice = jcsg.ItemPrices.Sum(x => (x.ChargedPrice ?? 0m));
                        jcsg.ItemTotals.ChargedPriceString = jcsg.ItemTotals.ChargedPrice.ToString("N2");
                        jcsg.ItemTotals.InternalCost = jcsg.ItemPrices.Sum(x => x.InternalCost);
                        jcsg.ItemTotals.InternalCostString = jcsg.ItemTotals.InternalCost.ToString("N2");
                        jcsg.ItemTotals.PercentDifference = CalculatePercentDifference(jcsg.ItemTotals.ChargedPrice, jcsg.ItemTotals.InternalCost);
                        jcsg.ItemTotals.PercentDifferenceString = jcsg.ItemTotals.PercentDifference.ToString("N2");
                        jcsg.ItemTotals.TotalSpread = (jcsg.ItemTotals.ChargedPrice - jcsg.ItemTotals.InternalCost);
                        jcsg.ItemTotals.TotalSpreadString = jcsg.ItemTotals.TotalSpread.ToString("N2");
                        jcsg.ItemTotals.PositiveSpread = jcsg.ItemTotals.TotalSpread > 0m;
                        jcsg.ItemTotals.ItemTimeActual = jcsg.ItemPrices.Sum(x => (x.ActualTime ?? 0m));
                        jcsg.ItemTotals.ItemTimeCharged = jcsg.ItemPrices.Sum(x => (x.ChargedTime ?? 0m));
                        jcsg.ItemTotals.ItemTimeActualFormatted = DecimalHoursToFormatted(jcsg.ItemTotals.ItemTimeActual);
                        jcsg.ItemTotals.ItemTimeChargedFormatted = DecimalHoursToFormatted(jcsg.ItemTotals.ItemTimeCharged);

                        costsheetVM.ItemGroups.Add(jcsg);

                        debugctr++;
                    }

                    //overall totals (running)
                    decimal costTotal = 0m;
                    decimal priceTotal = 0m;
                    foreach (VM_JobCostSheetGroup vm in costsheetVM.ItemGroups)
                    {
                        costTotal += vm.ItemPrices.Sum(x => x.InternalCost);
                        priceTotal += vm.ItemPrices.Sum(x => (x.ChargedPrice ?? 0m));
                    }

                    costsheetVM.JobTotals.InternalCost = costTotal;
                    costsheetVM.JobTotals.InternalCostString = costsheetVM.JobTotals.InternalCost.ToString("N2");
                    costsheetVM.JobTotals.ChargedPrice = priceTotal;
                    costsheetVM.JobTotals.ChargedPriceString = costsheetVM.JobTotals.ChargedPrice.ToString("N2");
                    costsheetVM.JobTotals.PercentDifference = CalculatePercentDifference(priceTotal, costTotal);
                    costsheetVM.JobTotals.PercentDifferenceString = costsheetVM.JobTotals.PercentDifference.ToString("N2");
                    costsheetVM.JobTotals.TotalSpread = (costsheetVM.JobTotals.ChargedPrice - costsheetVM.JobTotals.InternalCost);
                    costsheetVM.JobTotals.TotalSpreadString = costsheetVM.JobTotals.TotalSpread.ToString("N2");
                    costsheetVM.JobTotals.PositiveSpread = costsheetVM.JobTotals.TotalSpread > 0m;
                    costsheetVM.JobTotals.TargetMarkup = j.MarkupOverride ?? 0m;
                    costsheetVM.JobTotals.TargetMarkupString = (costsheetVM.JobTotals.TargetMarkup * 100).ToString("N2");
                    costsheetVM.JobTotals.JobTimeActual = costsheetVM.ItemGroups.Sum(x => x.ItemTotals.ItemTimeActual);
                    costsheetVM.JobTotals.JobTimeCharged = costsheetVM.ItemGroups.Sum(x => x.ItemTotals.ItemTimeCharged);
                    costsheetVM.JobTotals.JobTimeActualFormatted = DecimalHoursToHumanReadable(costsheetVM.JobTotals.JobTimeActual);
                    costsheetVM.JobTotals.JobTimeChargedFormatted = DecimalHoursToHumanReadable(costsheetVM.JobTotals.JobTimeCharged);

                    costsheetVM.RollUps = await CreateCostSheetRollUpRecords(costsheetVM.ItemGroups);

                    return View("~/Views/Job/CostSheet.cshtml", costsheetVM);
                }
                catch (Exception e)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Error Generating cost sheet");
                }
            }

            return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Job not found");
        }

        public async Task<ActionResult> EditFromQuoteNumber(string quoteNumber)
        {
            Quote q = await db.Quotes.SingleAsync(x => x.JobNumber == quoteNumber);
            return RedirectToAction("EditQuote", new { quoteId = q.Id });
        }

        public async Task<ActionResult> GetInvoice2(int? jobId)
        {
            Job j = await db.Jobs.FindAsync(jobId);

            List<VM_InvoiceRow2> finalRows = new List<VM_InvoiceRow2>();

            await db.Entry(j).Collection(c => c.JobItems).LoadAsync();
            await db.Entry(j).Reference(r => r.Client1).LoadAsync();
            await db.Entry(j).Reference(r => r.ShippingAddress1).LoadAsync();
            await db.Entry(j).Collection(c => c.JobOutsourcedItems).LoadAsync();
            await db.Entry(j).Reference(r => r.Contact1).LoadAsync();
            await db.Entry(j).Reference(r => r.ShippingOption).LoadAsync();
            await db.Entry(j).Reference(r => r.User).LoadAsync();

            if (j.ShippingAddress.HasValue)
            {
                await db.Entry(j.ShippingAddress1).Reference(r => r.USState).LoadAsync();
            }

            if (j.Client.HasValue)
            {
                await db.Entry(j.Client1).Reference(r => r.USState).LoadAsync();
            }

            if (j != null)
            {
                await db.Entry(j).Collection(c => c.JobItems).LoadAsync();
                List<JobParentItemTotals_Result> itemTotals = await db.JobParentItemTotals(j.Id).ToListAsync();

                foreach (JobParentItemTotals_Result r in itemTotals)
                {
                    try
                    {
                        JobItem ji = j.JobItems.Single(x => x.Id == r.JobItem);

                        VM_InvoiceRow2 newRow = new VM_InvoiceRow2
                        {
                            Quantity = (ji.Quantity ?? 0).ToString("N0"),
                            Description = ji.Description ?? "",
                            Notes = ji.Notes ?? "",
                            Price = r.Total_Price ?? 0m,
                            PriceString = (r.Total_Price ?? 0m).ToString("N2"),
                            UnitPrice = Decimal.Round((r.Total_Price ?? 0m) / (ji.Quantity ?? 1), 4),
                            Ordinal = ji.Ordinal ?? 0
                        };
                        newRow.UnitPriceString = newRow.UnitPrice.ToString("N2");

                        finalRows.Add(newRow);
                    }
                    catch (Exception)
                    {
                        //try outsourced
                        JobOutsourcedItem joi = j.JobOutsourcedItems.Single(x => x.Id == r.JobItem);

                        VM_InvoiceRow2 joiNewRow = new VM_InvoiceRow2
                        {
                            Quantity = (joi.Quantity ?? 0).ToString("N0"),
                            Description = joi.Description ?? "",
                            Notes = joi.Notes ?? "",
                            Price = joi.JobCostAdjusted ?? 0m,
                            PriceString = (joi.JobCostAdjusted ?? 0m).ToString("N2"),
                            UnitPrice = Decimal.Round((joi.JobCostAdjusted ?? 0m) / (joi.Quantity ?? 1), 4),
                            Ordinal = joi.Ordinal ?? 0
                        };

                        joiNewRow.UnitPriceString = joiNewRow.UnitPrice.ToString("N2");

                        finalRows.Add(joiNewRow);
                    }
                }

                var legacyShipments = await db.OrdersShippeds.Where(x => x.Job == j.Id).ToListAsync();

                var groupedShipmentIds = await db.JobShipmentPivots
                    .Where(p => p.Job == j.Id)
                    .Select(p => p.Shipment)
                    .Distinct()
                    .ToListAsync();
                var groupedShipments = await db.ShipmentsShippeds
                    .Where(ss => groupedShipmentIds.Contains(ss.JobShipment))
                    .ToListAsync();

                //count distinct jobs per group shipment so freight is split evenly across them
                var pivotsForShipments = await db.JobShipmentPivots
                    .Where(p => p.Shipment.HasValue && groupedShipmentIds.Contains(p.Shipment))
                    .Select(p => new { p.Shipment, p.Job })
                    .ToListAsync();
                var jobsPerShipment = pivotsForShipments
                    .Where(x => x.Shipment.HasValue)
                    .GroupBy(x => x.Shipment.Value)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.Job).Distinct().Count());

                VM_Invoice2 inv2 = new VM_Invoice2
                {
                    SourceInformation = new Models.CustomerQuoteModels.SourceInformation(),
                    Job = j,
                    Rows = finalRows,
                    TotalPrice = Decimal.Round(itemTotals.Sum(x => (x.Total_Price ?? 0m)), 2),
                    Shipments = legacyShipments,
                    GroupedShipments = groupedShipments
                };

                //calculate total freight (group shipments split evenly among their jobs)
                decimal groupedFreightShare = 0m;
                foreach (var ss in inv2.GroupedShipments)
                {
                    int jobCount;
                    if (!jobsPerShipment.TryGetValue(ss.JobShipment, out jobCount) || jobCount < 1)
                        jobCount = 1;
                    decimal share = Decimal.Round((ss.FreightPrice ?? 0m) / jobCount, 2);
                    inv2.GroupedShipmentShares[ss.Id] = share;
                    groupedFreightShare += share;
                }
                inv2.TotalFreightPrice = inv2.Shipments.Sum(x => (x.FreightPrice ?? 0m))
                                       + groupedFreightShare;

                //check client's tax exempt status directly
                bool isTaxExempt = (j.Client1?.TaxExempt ?? false);

                if (!isTaxExempt)
                {
                    //calculate tax
                    inv2.TaxAmount = Math.Round(inv2.TotalPrice * inv2.TaxPercent, 2);
                    //calculate total with tax
                    inv2.TotalWithTax = inv2.TotalPrice + inv2.TaxAmount;
                }
                else
                {
                    inv2.TaxAmount = 0m;
                    inv2.TotalWithTax = inv2.TotalPrice;
                }

                //calculate total + freight
                inv2.TotalWithFreight = inv2.TotalWithTax + inv2.TotalFreightPrice;

                inv2.FinalTotal = inv2.TotalWithFreight;

                //create display string
                inv2.TotalPriceString = inv2.TotalPrice.ToString("N2");
                inv2.TaxAmountString = inv2.TaxAmount.ToString("N2");
                inv2.TotalWithTaxString = inv2.TotalWithTax.ToString("N2");
                inv2.TotalFreightPriceString = inv2.TotalFreightPrice.ToString("N2");
                inv2.TotalWithFreightString = inv2.TotalWithFreight.ToString("N2");
                inv2.FinalTotalString = inv2.FinalTotal.ToString("N2");

                try
                {
                    Models.CustomerQuoteModels.ClientInformation clientInformation = new Models.CustomerQuoteModels.ClientInformation();

                    if (j != null)
                    {
                        clientInformation.ContactName = j.Contact.HasValue && j.Contact1 != null ? String.Format("{0} {1}", j.Contact1.First, j.Contact1.Last) : "";
                        clientInformation.Attention = ""; // Assuming static or no input needed.
                        clientInformation.Address = j.Client1?.Address ?? "";
                        clientInformation.ClientName = j.Client1?.Title; // Ensure Title is handled if null.
                        clientInformation.City = j.Client1?.City ?? "";
                        clientInformation.State = j.Client1?.ShippingUSState.HasValue == true && j.Client1.USState1 != null ? j.Client1.USState1.Abbreviation : "";
                        clientInformation.ZipCode = j.Client1?.Zip ?? "";
                        clientInformation.Email = j.Contact.HasValue && j.Contact1 != null ? j.Contact1.Email ?? "" : "";
                        clientInformation.Phone = j.Contact.HasValue && j.Contact1 != null ? j.Contact1.Phone ?? "" : "";
                    }
                    else
                    {
                        // Handle the case where `j` is null.
                        throw new InvalidOperationException("The job object 'j' cannot be null.");
                    }

                    inv2.ClientInformation = clientInformation;
                }
                catch (Exception e)
                {
                }
                if ((j.IsPickup ?? false) || !j.ShippingAddress.HasValue)
                {
                    inv2.ShippingInformation = new Models.CustomerQuoteModels.ShippingInformation
                    {
                        Address = "",
                        Address2 = "",
                        Attention = "",
                        City = "",
                        State = "",
                        ZipCode = "",
                        CompanyName = ""
                    };
                }
                else
                {
                    await db.Entry(j.ShippingAddress1).Reference(r => r.USState).LoadAsync();

                    inv2.ShippingInformation = new Models.CustomerQuoteModels.ShippingInformation
                    {
                        Address = j?.ShippingAddress1.Address ?? "",
                        Address2 = j?.ShippingAddress1.Address2 ?? "",
                        Attention = j?.ShippingAddress1.Attention ?? "",
                        City = j?.ShippingAddress1.City ?? "",
                        State = j?.ShippingAddress1.USState.Abbreviation,
                        ZipCode = j?.ShippingAddress1.Zip ?? "",
                        CompanyName = j?.ShippingAddress1.CustomerName ?? ""
                    };
                }

                return View("~/Views/Job/JobInvoice2.cshtml", inv2);
            }

            return new HttpStatusCodeResult(HttpStatusCode.InternalServerError);
        }

        public async Task<ActionResult> GetInvoice(int? jobId)
        {
            Job j = await db.Jobs.FindAsync(jobId);

            VM_Invoice invoice = new VM_Invoice();

            List<VM_InvoiceRow> finalRows = new List<VM_InvoiceRow>();

            await db.Entry(j).Collection(c => c.JobItems).LoadAsync();
            await db.Entry(j).Reference(r => r.Client1).LoadAsync();
            await db.Entry(j).Reference(r => r.ShippingAddress1).LoadAsync();
            await db.Entry(j).Collection(c => c.JobOutsourcedItems).LoadAsync();
            await db.Entry(j).Reference(r => r.Contact1).LoadAsync();
            await db.Entry(j).Reference(r => r.ShippingOption).LoadAsync();
            await db.Entry(j).Reference(r => r.User).LoadAsync();

            if (j.ShippingAddress.HasValue)
            {
                await db.Entry(j.ShippingAddress1).Reference(r => r.USState).LoadAsync();
            }

            if (j.Client.HasValue)
            {
                await db.Entry(j.Client1).Reference(r => r.USState).LoadAsync();
            }

            if (j != null)
            {
                await db.Entry(j).Collection(c => c.JobItems).LoadAsync();
                //List<ActualJobItemParentTotals_Result> itemTotals = await db.ActualJobItemParentTotals(j.Id).ToListAsync();
                List<JobParentItemTotals_Result> itemTotals = await db.JobParentItemTotals(j.Id).ToListAsync();
                //foreach (ActualJobItemParentTotals_Result r in itemTotals)
                foreach (JobParentItemTotals_Result r in itemTotals)
                {
                    try
                    {
                        JobItem ji = j.JobItems.Single(x => x.Id == r.JobItem);

                        finalRows.Add(new VM_InvoiceRow
                        {
                            Quantity = ji.TotalQuantity ?? 0,
                            Description = ji.Description ?? "",
                            TotalCost = r.Total_Price ?? 0m,
                            UnitCost = Decimal.Round((r.Total_Price ?? 0m) / (ji.TotalQuantity ?? 1), 4)
                        });
                    }
                    catch (Exception)
                    {
                        //try outsourced
                        JobOutsourcedItem joi = j.JobOutsourcedItems.Single(x => x.Id == r.JobItem);

                        finalRows.Add(new VM_InvoiceRow
                        {
                            Quantity = joi.Quantity ?? 0,
                            Description = joi.Description ?? "",
                            TotalCost = joi.JobCostAdjusted ?? 0m,
                            UnitCost = Decimal.Round((joi.JobCostAdjusted ?? 0m) / (joi.Quantity ?? 1), 4)
                        });
                    }
                }

                return View("~/Views/Job/JobInvoice.cshtml", new VM_Invoice
                {
                    Job = j,
                    Rows = finalRows,
                    TotalPrice = Decimal.Round(itemTotals.Sum(x => (x.Total_Price ?? 0m)), 2)
                });
            }

            return new HttpStatusCodeResult(HttpStatusCode.InternalServerError);
        }

        public async Task<ActionResult> ReadyToBill()
        {
            var readyToBillJobs = await db.JobsReadyToBill().ToListAsync();
            return View(readyToBillJobs);
        }

        public async Task<ActionResult> ReadyToShip()
        {
            var readyToShipJobs = await db.JobsReadyToShip().ToListAsync();
            return View(readyToShipJobs);
        }

        public async Task<ActionResult> InProgressJobs()
        {
            var inProgressJobs = await db.InProgressJobs().ToListAsync();
            return View(inProgressJobs);
        }

        public async Task<ActionResult> ClearSheetOverride(string itemtype, int? itemId)
        {
            if (String.IsNullOrEmpty(itemtype))
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            switch (itemtype.ToUpper())
            {
                case "QUOTE":
                    QuoteItem qi = await db.QuoteItems.FindAsync(itemId);
                    qi.UserSheetOverride = false;
                    db.Entry(qi).State = EntityState.Modified;
                    await db.SaveChangesAsync();
                    return new HttpStatusCodeResult(HttpStatusCode.OK);

                case "JOB":
                    JobItem ji = await db.JobItems.FindAsync(itemId);
                    ji.UserSheetOverride = false;
                    db.Entry(ji).State = EntityState.Modified;
                    await db.SaveChangesAsync();
                    return new HttpStatusCodeResult(HttpStatusCode.OK);

                default:
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
        }

        public async Task<ActionResult> GetMaterialConfig(int? id, string itemType)
        {
            switch (itemType.ToUpper() ?? "")
            {
                case "JOB":
                    JobItem ji = await db.JobItems.FindAsync(id);
                    await db.Entry(ji).Reference(r => r.Material1).LoadAsync();
                    if (ji.Material.HasValue)
                    {
                        await db.Entry(ji.Material1).Reference(r => r.MaterialCostType).LoadAsync();
                    }

                    if (ji != null)
                    {
                        ji.ParseRunSize();

                        VM_MaterialConfig c = new VM_MaterialConfig
                        {
                            ColorBackCount = ji.PressColorCountBack ?? 0,
                            ColorFrontCount = ji.PressColorCountFront ?? 0,
                            ColorBackNotes = ji.PressColorNotesBack ?? "",
                            ColorFrontNotes = ji.PressColorNotesFront ?? "",
                            ColorBackProfile = ji.PressColorProfileBack ?? "",
                            ColorFrontProfile = ji.PressColorProfileFront ?? "",
                            RunLength = ji.RunLength.ToString(),
                            RunWidth = ji.RunWidth.ToString(),
                            //RunSheets = ji.TotalRunSheets ?? 0,
                            RunSheets = ji.RunSheets ?? 0,
                            ScrapSheets = ji.TotalScrapSheets ?? 0,
                            Sheets = ji.Sheets ?? 0,
                            BuySheets = ji.BuySheets ?? 0,
                            Up = ji.Up ?? 0,
                            DoubleInk = ji.DoubleHit ?? false,
                            Collate = ji.Collate ?? false,
                            Staple = ji.Staple ?? false,
                            MaterialCost = ji.MaterialCost ?? 0m,
                            MaterialCostType = ji.Material.HasValue ? ji.Material1.MaterialCostType.Title : "",
                            UserSheetOverride = ji.UserSheetOverride ?? false,
                            Out = ji.Out ?? 1
                        };

                        return new JsonResult
                        {
                            Data = JsonConvert.SerializeObject(c, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                            JsonRequestBehavior = JsonRequestBehavior.AllowGet
                        };
                    }
                    break;

                case "QUOTE":
                    QuoteItem qi = await db.QuoteItems.FindAsync(id);
                    await db.Entry(qi).Reference(r => r.Material1).LoadAsync();
                    if (qi.Material.HasValue)
                    {
                        await db.Entry(qi.Material1).Reference(r => r.MaterialCostType).LoadAsync();
                    }

                    if (qi != null)
                    {
                        qi.ParseRunSize();

                        VM_MaterialConfig c = new VM_MaterialConfig
                        {
                            ColorBackCount = qi.PressColorCountBack ?? 0,
                            ColorFrontCount = qi.PressColorCountFront ?? 0,
                            ColorBackNotes = qi.PressColorNotesBack ?? "",
                            ColorFrontNotes = qi.PressColorNotesFront ?? "",
                            ColorBackProfile = qi.PressColorProfileBack ?? "",
                            ColorFrontProfile = qi.PressColorProfileFront ?? "",
                            RunLength = qi.RunLength.ToString(),
                            RunWidth = qi.RunWidth.ToString(),
                            //RunSheets = qi.TotalRunSheets ?? 0,
                            RunSheets = qi.RunSheets ?? 0,
                            ScrapSheets = qi.TotalScrapSheets ?? 0,
                            Sheets = qi.Sheets ?? 0,
                            BuySheets = qi.BuySheets ?? 0,
                            Up = qi.Up ?? 0,
                            DoubleInk = qi.DoubleHit ?? false,
                            Collate = qi.Collate ?? false,
                            Staple = qi.Staple ?? false,
                            MaterialCost = qi.MaterialCost ?? 0m,
                            MaterialCostType = qi.Material.HasValue ? qi.Material1.MaterialCostType.Title : "",
                            UserSheetOverride = qi.UserSheetOverride ?? false,
                            Out = qi.Out ?? 1
                        };

                        return new JsonResult
                        {
                            Data = JsonConvert.SerializeObject(c, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                            JsonRequestBehavior = JsonRequestBehavior.AllowGet
                        };
                    }
                    break;

                default:
                    break;
            }

            return new HttpStatusCodeResult(HttpStatusCode.InternalServerError);
        }

        public async Task<ActionResult> ClearShippingAddress(int? recordId, string itemtype)
        {
            switch (itemtype.ToUpper())
            {
                case "QUOTE":
                    Quote q = await db.Quotes.FindAsync(recordId);
                    if (q != null)
                    {
                        q.ShippingAddress = null;
                        db.Entry(q).State = EntityState.Modified;
                        await db.SaveChangesAsync();

                        return new HttpStatusCodeResult(HttpStatusCode.OK);
                    }
                    break;

                case "JOB":
                    Job j = await db.Jobs.FindAsync(recordId);
                    if (j != null)
                    {
                        j.ShippingAddress = null;
                        db.Entry(j).State = EntityState.Modified;
                        await db.SaveChangesAsync();

                        return new HttpStatusCodeResult(HttpStatusCode.OK);
                    }
                    break;

                default:
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Invalid item type");
            }

            return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Cannot find that record");
        }

        public async Task<ActionResult> ClearFreightAddress(int? recordId, string itemtype)
        {
            switch (itemtype.ToUpper())
            {
                case "QUOTE":
                    Quote q = await db.Quotes.FindAsync(recordId);
                    if (q != null)
                    {
                        q.FreightAddress = null;
                        db.Entry(q).State = EntityState.Modified;
                        await db.SaveChangesAsync();

                        return new HttpStatusCodeResult(HttpStatusCode.OK);
                    }
                    break;

                case "JOB":
                    Job j = await db.Jobs.FindAsync(recordId);
                    if (j != null)
                    {
                        j.FreightAddress = null;
                        db.Entry(j).State = EntityState.Modified;
                        await db.SaveChangesAsync();

                        return new HttpStatusCodeResult(HttpStatusCode.OK);
                    }
                    break;

                default:
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Invalid item type");
            }

            return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Cannot find that record");
        }

        public async Task<ActionResult> ClearBlindShippingAddress(int? recordId, string itemtype)
        {
            switch (itemtype.ToUpper())
            {
                case "JOB":
                    Job j = await db.Jobs.FindAsync(recordId);
                    if (j != null)
                    {
                        j.BlindShippingAddress = null;
                        db.Entry(j).State = EntityState.Modified;
                        await db.SaveChangesAsync();

                        return new HttpStatusCodeResult(HttpStatusCode.OK);
                    }
                    break;

                default:
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Invalid item type");
            }

            return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Cannot find that record");
        }

        //only handles the add/remove of finishing materials
        public async Task<ActionResult> UpdateFinishingMaterials(int? itemId, List<int> fmIds, string itemtype)
        {
            if (!itemId.HasValue || String.IsNullOrEmpty(itemtype))
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            fmIds = fmIds ?? new List<int>();

            Service generalFinishingMaterialService = await db.Services.FindAsync(1341);

            var finishingService = await db.Services.SingleAsync(
                x => x.SerNo == (int)Service.SerNoSpecialServices.FinishingService);

            switch (itemtype.ToUpper())
            {
                case "QUOTE":
                    var existingQMaterials = await db.QuoteItemFinishingMaterials.Where(x => x.QuoteItemId == itemId).ToListAsync();
                    var existingQMaterialIds = existingQMaterials.Select(x => x.Material).ToList();

                    var notExistingQMaterials = fmIds.Where(x => !existingQMaterialIds.Contains(x)).ToList();
                    var toRemoveQMaterials = existingQMaterialIds.Where(x => !fmIds.Contains(x.Value)).ToList();

                    // Get next ordinal for new finishing material service rows
                    var existingQisForItem = await db.QuoteItemServices.Where(x => x.QuoteItemId == itemId.Value).ToListAsync();
                    int nextQOrdinal = existingQisForItem.Any() ? existingQisForItem.Max(x => x.Ordinal) + 1 : 1;

                    foreach (var q in notExistingQMaterials)
                    {
                        FinishingMaterialFormulaPivot pivot = await db.FinishingMaterialFormulaPivots.AsNoTracking().Where(x => x.Material == q).FirstOrDefaultAsync();
                        FinishingMaterialFormula formula = await db.FinishingMaterialFormulas.AsNoTracking().Where(x => x.Id == pivot.FinishingFormula).FirstOrDefaultAsync();
                        Material m = await db.Materials.AsNoTracking().Where(x => x.Id == q).FirstOrDefaultAsync();

                        QuoteItemFinishingMaterial newQifm = new QuoteItemFinishingMaterial
                        {
                            QuoteItemId = itemId,
                            Material = q,
                            FinishingFormulaConfiguration = formula.DefaultConfig,
                            FinishingFormulaApplication = pivot.FormulaApplication,
                            ItemCost = m.Cost ?? 0m
                        };

                        db.QuoteItemFinishingMaterials.Add(newQifm);

                        await db.SaveChangesAsync();

                        // Create a corresponding QuoteItemService linked to this finishing material
                        var newQis = new QuoteItemService
                        {
                            QuoteItemId = itemId.Value,
                            OriginalService = finishingService.Id,
                            Title = m.Title,
                            FriendlyName = m.FriendlyName,
                            IsFinishingService = true,
                            QuoteItemFinishingMaterial = newQifm.Id,
                            Ordinal = nextQOrdinal++
                        };
                        db.QuoteItemServices.Add(newQis);
                        await db.SaveChangesAsync();
                    }

                    foreach (var q in toRemoveQMaterials)
                    {
                        QuoteItemService qisToRemove = null;
                        try
                        {
                            QuoteItemFinishingMaterial oldQifm = await db.QuoteItemFinishingMaterials.SingleAsync(x => x.Material == q && x.QuoteItemId == itemId);
                            db.QuoteItemFinishingMaterials.Remove(oldQifm);

                            //find the corresponding service record (if one exists)
                            try
                            {
                                qisToRemove = await db.QuoteItemServices.SingleAsync(x => x.QuoteItemId == itemId && x.QuoteItemFinishingMaterial == oldQifm.Id);
                                db.QuoteItemServices.Remove(qisToRemove);
                            }
                            catch (Exception)
                            {
                                //does not exist, which is ok in this scenario
                            }

                            try
                            {
                                QuotePrice qPriceOne = await db.QuotePrices.SingleAsync(x => x.QuoteItem == itemId && x.QuoteItemService == qisToRemove.Id);
                                db.QuotePrices.Remove(qPriceOne);
                            }
                            catch (Exception)
                            {
                                //no price exists, which is ok
                            }

                            try
                            {
                                QuotePrice qPriceTwo = await db.QuotePrices.SingleAsync(x => x.QuoteItem == itemId && x.QuoteItemService == oldQifm.Id);
                                db.QuotePrices.Remove(qPriceTwo);
                            }
                            catch (Exception)
                            {
                                //ok; move on
                            }
                        }
                        catch (Exception)
                        {
                            //no item finishing material found, which is weird but ok
                        }
                    }
                    try
                    {
                        await db.SaveChangesAsync();
                    }
                    catch (Exception)
                    {
                    }
                    break;

                case "JOB":
                    var existingJMaterials = await db.JobItemFinishingMaterials.Where(x => x.JobItemId == itemId).ToListAsync();
                    var existingJMaterialIds = existingJMaterials.Select(x => x.Material).ToList();

                    var notExistingJMaterials = fmIds.Where(x => !existingJMaterialIds.Contains(x)).ToList();
                    var toRemoveJMaterials = existingJMaterialIds.Where(x => !fmIds.Contains(x)).ToList();

                    // Get next ordinal for new finishing material service rows
                    var existingJisForItem = await db.JobItemServices.Where(x => x.JobItemId == itemId.Value).ToListAsync();
                    int nextJOrdinal = existingJisForItem.Any() ? existingJisForItem.Max(x => x.Ordinal ?? 0) + 1 : 1;

                    //NOTE: JIFM Id is not marked as Identity so next Id needs to be retrieved
                    int nextJifmId = 1;

                    try
                    {
                        nextJifmId = await db.JobItemFinishingMaterials.MaxAsync(x => x.Id) + 1;
                    }
                    catch (Exception) { /* ok; move on */ }

                    foreach (var q in notExistingJMaterials)
                    {
                        FinishingMaterialFormulaPivot pivot = await db.FinishingMaterialFormulaPivots.AsNoTracking().Where(x => x.Material == q).FirstOrDefaultAsync();
                        FinishingMaterialFormula formula = await db.FinishingMaterialFormulas.AsNoTracking().Where(x => x.Id == pivot.FinishingFormula).FirstOrDefaultAsync();
                        Material m = await db.Materials.AsNoTracking().Where(x => x.Id == q).FirstOrDefaultAsync();

                        JobItemFinishingMaterial newJifm = new JobItemFinishingMaterial
                        {
                            Id = nextJifmId++,
                            JobItemId = itemId.Value,
                            Material = q,
                            FinishingFormulaConfiguration = formula.DefaultConfig,
                            FinishingFormulaApplication = pivot.FormulaApplication,
                            ItemCost = m.Cost ?? 0m
                        };

                        db.JobItemFinishingMaterials.Add(newJifm);

                        await db.SaveChangesAsync();

                        // Create a corresponding JobItemService linked to this finishing material
                        var newJis = new JobItemService
                        {
                            JobItemId = itemId.Value,
                            ServiceId = finishingService.Id,
                            Title = m.Title,
                            FriendlyName = m.FriendlyName,
                            IsFinishingService = true,
                            JobItemFinishingMaterial = newJifm.Id,
                            Ordinal = nextJOrdinal++
                        };
                        db.JobItemServices.Add(newJis);
                        await db.SaveChangesAsync();
                    }

                    foreach (var j in toRemoveJMaterials)
                    {
                        JobItemService jisToRemove = null;

                        try
                        {
                            JobItemFinishingMaterial oldJifm = await db.JobItemFinishingMaterials.SingleAsync(x => x.Material == j && x.JobItemId == itemId);
                            db.JobItemFinishingMaterials.Remove(oldJifm);

                            //find the corresponding service record (if one exists)
                            try
                            {
                                jisToRemove = await db.JobItemServices.SingleAsync(x => x.JobItemId == itemId && x.JobItemFinishingMaterial == oldJifm.Id);
                                db.JobItemServices.Remove(jisToRemove);
                            }
                            catch (Exception)
                            {
                                //does not exist, which is ok in this scenario
                            }

                            try
                            {
                                JobPrice jPriceOne = await db.JobPrices.SingleAsync(x => x.JobItem == itemId && x.JobItemService == jisToRemove.Id);
                                db.JobPrices.Remove(jPriceOne);
                            }
                            catch (Exception)
                            {
                                //no price exists, which is ok
                            }

                            try
                            {
                                JobPrice jPriceTwo = await db.JobPrices.SingleAsync(x => x.JobItem == itemId && x.JobItemService == oldJifm.Id);
                                db.JobPrices.Remove(jPriceTwo);
                            }
                            catch (Exception)
                            {
                                //ok; move on
                            }
                        }
                        catch (Exception)
                        {
                            //no item finishing material found, which is weird but ok
                        }
                    }
                    await db.SaveChangesAsync();
                    break;

                default:
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            // Re-query ALL finishing services (not just finishing materials) for this item
            string finishingServicesHtml = "";

            switch (itemtype.ToUpper())
            {
                case "QUOTE":
                    QuoteItem qiRef = await db.QuoteItems.FindAsync(itemId.Value);
                    if (qiRef != null)
                    {
                        await db.Entry(qiRef).Collection(c => c.QuoteItemServices).LoadAsync();
                        foreach (QuoteItemService qis in qiRef.QuoteItemServices)
                        {
                            await db.Entry(qis).Reference(r => r.Service).LoadAsync();
                            if (qis.Service == null) continue;
                            await db.Entry(qis.Service).Reference(r => r.ServiceCategory).LoadAsync();
                            await db.Entry(qis.Service.ServiceCategory).Reference(r => r.ServiceType1).LoadAsync();
                        }
                        finishingServicesHtml = RenderViewToString("~/Views/Partials/_ItemServiceRows.cshtml", new VM_ItemServices
                        {
                            ServiceList = qiRef.QuoteItemServices
                                .Where(x => x.Service != null && x.Service.ServiceCategory.ServiceType1.Title == "Finishing" && !(x.IsCuttingService ?? false))
                                .OrderBy(x => x.ApexOrdinal)
                                .Select(x => new VM_ItemServiceRow
                                {
                                    RecordId = x.Id,
                                    ServiceTitle = x.Title,
                                    Ordinal = x.Ordinal,
                                    ServiceId = x.OriginalService,
                                    Notes = x.Notes,
                                    Required = x.Service.Required ?? false,
                                    IsCuttingService = x.IsCuttingService ?? false,
                                    FormattedServiceTime = DecimalHoursToFormatted(x.ServiceTime ?? 0m),
                                    IsTimed = x.Service.IsTimed ?? false,
                                    IsFinishingService = x.IsFinishingService ?? false
                                }).ToList()
                        });
                    }
                    break;

                case "JOB":
                    JobItem jiRef = await db.JobItems.FindAsync(itemId.Value);
                    if (jiRef != null)
                    {
                        await db.Entry(jiRef).Collection(c => c.JobItemServices).LoadAsync();
                        foreach (JobItemService jis in jiRef.JobItemServices)
                        {
                            await db.Entry(jis).Reference(r => r.Service).LoadAsync();
                            await db.Entry(jis.Service).Reference(r => r.ServiceCategory).LoadAsync();
                            await db.Entry(jis.Service.ServiceCategory).Reference(r => r.ServiceType1).LoadAsync();
                        }
                        finishingServicesHtml = RenderViewToString("~/Views/Partials/_ItemServiceRows.cshtml", new VM_ItemServices
                        {
                            ServiceList = jiRef.JobItemServices
                                .Where(x => x.Service != null && x.Service.ServiceCategory.ServiceType1.Title == "Finishing" && !(x.IsCuttingService ?? false))
                                .OrderBy(x => x.ApexOrdinal)
                                .Select(x => new VM_ItemServiceRow
                                {
                                    RecordId = x.Id,
                                    ServiceTitle = x.Title,
                                    Ordinal = x.Ordinal ?? 0,
                                    ServiceId = x.ServiceId,
                                    Notes = x.Notes,
                                    Required = x.Service.Required ?? false,
                                    IsCuttingService = x.IsCuttingService ?? false,
                                    FormattedServiceTime = DecimalHoursToFormatted(x.ServiceTime ?? 0m),
                                    IsTimed = x.Service.IsTimed ?? false,
                                    RequiresQualityCheck = x.RequiresQualityCheck ?? false,
                                    IsPrintingService = false,
                                    ShowQAToggle = true,
                                    IsFinishingService = x.IsFinishingService ?? false
                                }).ToList()
                        });
                    }
                    break;
            }

            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(new { finishingServices = finishingServicesHtml }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> UpdateFinishingConfig(string itemtype, List<FinishingConfig_CM> configs)
        {
            if (String.IsNullOrEmpty(itemtype))
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            foreach (FinishingConfig_CM fc in configs)
            {
                switch (itemtype.ToUpper())
                {
                    case "QUOTE":
                        QuoteItemFinishingMaterial qifm = await db.QuoteItemFinishingMaterials.FindAsync(fc.id);
                        if (qifm != null)
                        {
                            qifm.FinishingFormulaApplication = fc.applicationValue;
                            qifm.ItemCost = fc.cost;

                            var config = qifm.FinishingFormulaConfiguration;
                            int replaceIndex = config.IndexOf(":");
                            string endOfString = config.Substring(replaceIndex);
                            string newEndOfString = ":\"" + fc.value + "\"}";
                            qifm.FinishingFormulaConfiguration = config.Replace(endOfString, newEndOfString);

                            db.Entry(qifm).State = EntityState.Modified;
                        }
                        break;

                    case "JOB":
                        JobItemFinishingMaterial jifm = await db.JobItemFinishingMaterials.FindAsync(fc.id);
                        if (jifm != null)
                        {
                            jifm.FinishingFormulaApplication = fc.applicationValue;
                            jifm.ItemCost = fc.cost;

                            var config = jifm.FinishingFormulaConfiguration;
                            int replaceIndex = config.IndexOf(":");
                            string endOfString = config.Substring(replaceIndex);
                            string newEndOfString = ":\"" + fc.value + "\"}";
                            jifm.FinishingFormulaConfiguration = config.Replace(endOfString, newEndOfString);

                            db.Entry(jifm).State = EntityState.Modified;
                        }
                        break;

                    default:
                        return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }

                await db.SaveChangesAsync();
            }

            return new HttpStatusCodeResult(HttpStatusCode.OK);
        }

        //refactored AddPressToItem: broken into smaller chunks
        public async Task<ActionResult> AddPressToItem(string itemType, int? itemId, int? pressId, int? selectedSpeed)
        {
            if (string.IsNullOrEmpty(itemType))
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Missing required parameters");

            Press masterPress = await db.Presses.FindAsync(pressId);

            if (masterPress == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Invalid press ID");

            //if selectedSpeed is null, then get the default speed for this press
            if (!selectedSpeed.HasValue)
            {
                List<PressSpeed> speeds = await db.PressSpeeds.Where(x => x.Press == masterPress.Id).ToListAsync();
                PressSpeed defaultSpeed = speeds.FirstOrDefault(x => (x.IsDefault ?? false));
                selectedSpeed = defaultSpeed.Id;
            }

            PressSpeed ps = await db.PressSpeeds.Include(i => i.SpeedDenominator1).SingleAsync(x => x.Id == selectedSpeed);

            switch (itemType.ToUpper())
            {
                case "QUOTE":
                    int qPressId = await HandleQuoteItemPressAssignment(itemId, masterPress, selectedSpeed);

                    return new JsonResult
                    {
                        Data = JsonConvert.SerializeObject(new { pressId = qPressId, speedId = ps.Id.ToString(), pressSpeed = ps.SpeedNumerator.Value.ToString(), pressSpeedUnit = ps.SpeedDenominator1.Text }, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };

                case "JOB":
                    int jPressId = await HandleJobItemPressAssignment(itemId, masterPress, selectedSpeed);

                    return new JsonResult
                    {
                        Data = JsonConvert.SerializeObject(new { pressId = jPressId, speedId = ps.Id.ToString(), pressSpeed = ps.SpeedNumerator.Value.ToString(), pressSpeedUnit = ps.SpeedDenominator1.Text }, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };

                default:
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Invalid item type");
            }
        }

        private async Task<int> HandleQuoteItemPressAssignment(int? itemId, Press masterPress, int? selectedSpeed)
        {
            QuoteItem qi = await db.QuoteItems.FindAsync(itemId);
            if (qi == null)
                throw new Exception("Item not found");

            Press assignedPress = await GetAssignedPress(qi.Material, masterPress);
            AssignPressToQuoteItem(qi, assignedPress);

            qi.SelectedSpeed = selectedSpeed;

            await ClearExistingQuotePrintPrices(qi);

            db.Entry(qi).State = EntityState.Modified;

            await db.SaveChangesAsync();
            return qi.Press ?? 0;
        }

        private async Task<int> HandleJobItemPressAssignment(int? itemId, Press masterPress, int? selectedSpeed)
        {
            JobItem ji = await db.JobItems.FindAsync(itemId);
            if (ji == null)
                throw new Exception("item not found");

            Press assignedPress = await GetAssignedPress(ji.Material, masterPress);
            AssignPressToJobItem(ji, assignedPress);

            ji.SelectedSpeed = selectedSpeed;

            await ClearExistingJobPrintPrices(ji);

            db.Entry(ji).State = EntityState.Modified;

            await db.SaveChangesAsync();
            return ji.Press ?? 0;
        }

        private async Task<Press> GetAssignedPress(int? materialId, Press masterPress)
        {
            if (materialId.HasValue)
            {
                Material material = await db.Materials.FindAsync(materialId);
                if (material == null)
                    throw new Exception("Material not found");

                int materialStockType = material.StockType ?? throw new Exception("Material does not have a stock type");

                if (masterPress.StockType == materialStockType)
                {
                    return masterPress;
                }

                await db.Entry(masterPress).Collection(c => c.Presses1).LoadAsync();
                Press matchedPress = masterPress.Presses1.FirstOrDefault(x => x.StockType == materialStockType);

                if (matchedPress == null)
                    throw new Exception("No matching press found for the material's stock type");

                return matchedPress;
            }

            return masterPress;
        }

        private async Task ClearExistingQuotePrintPrices(QuoteItem qi)
        {
            await db.Entry(qi).Collection(c => c.QuotePrices).LoadAsync();

            try
            {
                QuotePrice inkPrice = qi.QuotePrices.Single(x => x.Title.Contains("Ink Cost"));
                db.QuotePrices.Remove(inkPrice);
            }
            catch (Exception)
            {
                //no existing ink price, which is ok
            }

            try
            {
                QuotePrice laborPrice = qi.QuotePrices.Single(x => x.Title.Contains("Labor Cost"));
                db.QuotePrices.Remove(laborPrice);
            }
            catch (Exception)
            {
                //no existing labor price, which is ok
            }
        }

        private async Task ClearExistingJobPrintPrices(JobItem ji)
        {
            await db.Entry(ji).Collection(c => c.JobPrices).LoadAsync();

            try
            {
                JobPrice inkPrice = ji.JobPrices.Single(x => x.Title.Contains("Ink Cost"));
                db.JobPrices.Remove(inkPrice);
            }
            catch (Exception)
            {
                //ok; move on
            }

            try
            {
                JobPrice laborPrice = ji.JobPrices.Single(x => x.Title.Contains("Labor Cost"));
                db.JobPrices.Remove(laborPrice);
            }
            catch (Exception)
            {
                //ok; move o
            }
        }

        private void AssignPressToQuoteItem(QuoteItem qi, Press press)
        {
            qi.Press = press.Id;
            qi.PressSpeedDenominator = press.SpeedDenominator;
            qi.PressSpeedNumerator = press.SpeedNumerator;
            qi.SideGuides = press.SideGuides;
            qi.Gripper = press.Gripper;
            qi.Tail = press.Tail;
            qi.MarginXL = press.MarginLeft;
            qi.MarginXR = press.MarginRight;
            qi.MarginYB = press.MarginBottom;
            qi.MarginYT = press.MarginTop;
        }

        private void AssignPressToJobItem(JobItem ji, Press press)
        {
            ji.Press = press.Id;
            ji.PressSpeedDenominator = press.SpeedDenominator;
            ji.PressSpeedNumerator = press.SpeedNumerator;
            ji.SideGuides = press.SideGuides;
            ji.Gripper = press.Gripper;
            ji.Tail = press.Tail;
            ji.MarginXL = press.MarginLeft;
            ji.MarginXR = press.MarginRight;
            ji.MarginYB = press.MarginBottom;
            ji.MarginYT = press.MarginTop;
        }

        public async Task<ActionResult> SpecificMarkupOverride(
            int? recordId,
            decimal? val,
            string recType2,
            bool isItemTotal)
        {
            if (!recordId.HasValue)
            {
                return Json(new { error = "Record ID is required." }, JsonRequestBehavior.AllowGet);
            }
            if (string.IsNullOrWhiteSpace(recType2))
            {
                return Json(new { error = "Record type is required." }, JsonRequestBehavior.AllowGet);
            }

            switch (recType2.ToUpperInvariant())
            {
                case "QUOTE":
                    await ProcessQuoteMarkup(recordId.Value, val, isItemTotal);
                    break;

                case "JOB":
                    await ProcessJobMarkup(recordId.Value, val, isItemTotal);
                    break;

                default:
                    return Json(new { error = "Invalid record type." }, JsonRequestBehavior.AllowGet);
            }

            await db.SaveChangesAsync();
            return Json("Done", JsonRequestBehavior.AllowGet);
        }

        private async Task ProcessQuoteMarkup(int recordId, decimal? val, bool isItemTotal)
        {
            if (isItemTotal)
            {
                // In this scenario, recordId is the QuoteItem identifier.
                // Retrieve all pricing records that belong to that item.
                var quotePrices = await db.QuotePrices
                    .Where(qp => qp.QuoteItem == recordId)
                    .Include(qp => qp.QuoteItem1)
                    .Include(qp => qp.QuoteItemService1)
                    .ToListAsync();

                foreach (var qp in quotePrices)
                {
                    await UpdateQuotePriceAsync(qp, val);
                }
            }
            else
            {
                // recordId is the primary key of a single QuotePrice record.
                var qp = await db.QuotePrices.FindAsync(recordId);
                if (qp == null) return;

                // Load the proper navigation property based on the pricing type.
                if (qp.IsMaterial.GetValueOrDefault() ||
                    qp.Title.Contains("Ink Cost") || qp.Title.Contains("Labor Cost"))
                {
                    await db.Entry(qp).Reference(q => q.QuoteItem1).LoadAsync();
                }
                else
                {
                    await db.Entry(qp).Reference(q => q.QuoteItemService1).LoadAsync();
                }
                await UpdateQuotePriceAsync(qp, val);
            }
        }

        private async Task ProcessJobMarkup(int recordId, decimal? val, bool isItemTotal)
        {
            if (isItemTotal)
            {
                // Here, recordId is the JobItem identifier.
                var jobPrices = await db.JobPrices
                    .Where(jp => jp.JobItem == recordId)
                    .Include(jp => jp.JobItem1)
                    .Include(jp => jp.JobItemService1)
                    .ToListAsync();

                foreach (var jp in jobPrices)
                {
                    await UpdateJobPriceAsync(jp, val);
                }
            }
            else
            {
                // recordId is the primary key of a single JobPrice record.
                var jp = await db.JobPrices.FindAsync(recordId);
                if (jp == null) return;

                if (jp.IsMaterial.GetValueOrDefault() ||
                    jp.Title.Contains("Ink Cost") || jp.Title.Contains("Labor Cost"))
                {
                    await db.Entry(jp).Reference(j => j.JobItem1).LoadAsync();
                }
                else
                {
                    await db.Entry(jp).Reference(j => j.JobItemService1).LoadAsync();
                }
                await UpdateJobPriceAsync(jp, val);
            }
        }

        private async Task UpdateQuotePriceAsync(QuotePrice qp, decimal? val)
        {
            qp.Markup = val;
            qp.OverriddenMarkup = true;

            if (qp.IsMaterial.GetValueOrDefault())
            {
                // Update custom material markup on the item.
                qp.QuoteItem1.CustomMaterialMarkup = val;
                db.Entry(qp.QuoteItem1).State = EntityState.Modified;
            }
            else if (qp.Title.Contains("Ink Cost") || qp.Title.Contains("Labor Cost"))
            {
                // Update press markup info on the item.
                if (qp.Title.Contains("Ink Cost"))
                {
                    qp.QuoteItem1.CustomInkMarkup = val;
                }
                else
                {
                    qp.QuoteItem1.CustomPrintLaborMarkup = val;
                }
                db.Entry(qp.QuoteItem1).State = EntityState.Modified;
            }
            else
            {
                // Update the related service's custom markup.
                if (qp.QuoteItemService1 != null)
                {
                    qp.QuoteItemService1.CustomMarkup = val;
                    db.Entry(qp.QuoteItemService1).State = EntityState.Modified;
                }
                else if (qp.QuoteItemService.HasValue)
                {
                    // This is a finishing material - QuoteItemService contains the QuoteItemFinishingMaterial.Id
                    var finishingMaterial = await db.QuoteItemFinishingMaterials.FindAsync(qp.QuoteItemService.Value);
                    if (finishingMaterial != null)
                    {
                        finishingMaterial.CustomMarkup = val;
                        db.Entry(finishingMaterial).State = EntityState.Modified;
                    }
                }
                qp.Markup = val;
                db.Entry(qp).State = EntityState.Modified;
            }
            // Mark the pricing record as modified.
            db.Entry(qp).State = EntityState.Modified;
        }

        private async Task UpdateJobPriceAsync(JobPrice jp, decimal? val)
        {
            jp.Markup = val;
            jp.OverriddenMarkup = true;

            if (jp.IsMaterial.GetValueOrDefault())
            {
                jp.JobItem1.CustomMaterialMarkup = val;
                db.Entry(jp.JobItem1).State = EntityState.Modified;
            }
            else if (jp.Title.Contains("Ink Cost") || jp.Title.Contains("Labor Cost"))
            {
                if (jp.Title.Contains("Ink Cost"))
                {
                    jp.JobItem1.CustomInkMarkup = val;
                }
                else
                {
                    jp.JobItem1.CustomPrintLaborMarkup = val;
                }
                db.Entry(jp.JobItem1).State = EntityState.Modified;
            }
            else
            {
                if (jp.JobItemService1 != null)
                {
                    jp.JobItemService1.CustomMarkup = val;
                    db.Entry(jp.JobItemService1).State = EntityState.Modified;
                }
                else if (jp.JobItemService.HasValue)
                {
                    // This is a finishing material - JobItemService contains the JobItemFinishingMaterial.Id
                    var finishingMaterial = await db.JobItemFinishingMaterials.FindAsync(jp.JobItemService.Value);
                    if (finishingMaterial != null)
                    {
                        finishingMaterial.CustomMarkup = val;
                        db.Entry(finishingMaterial).State = EntityState.Modified;
                    }
                }
                jp.Markup = val;
                db.Entry(jp).State = EntityState.Modified;
            }
            db.Entry(jp).State = EntityState.Modified;
        }

        public async Task<ActionResult> OverrideItemPrice(string recordType, int? itemId, decimal? newPrice)
        {
            if (!itemId.HasValue || !newPrice.HasValue || string.IsNullOrWhiteSpace(recordType))
            {
                return Json(new { error = "Missing required parameters." }, JsonRequestBehavior.AllowGet);
            }

            switch (recordType.ToUpperInvariant())
            {
                case "QUOTE":
                {
                    var quotePrices = await db.QuotePrices
                        .Where(qp => qp.QuoteItem == itemId.Value)
                        .Include(qp => qp.QuoteItem1)
                        .Include(qp => qp.QuoteItemService1)
                        .ToListAsync();

                    if (!quotePrices.Any())
                        return Json(new { error = "No price records found for this item." }, JsonRequestBehavior.AllowGet);

                    decimal totalCost = quotePrices.Sum(qp => qp.Cost ?? 0m);
                    decimal markup = totalCost != 0 ? (newPrice.Value - totalCost) / totalCost : 0m;

                    foreach (var qp in quotePrices)
                    {
                        await UpdateQuotePriceAsync(qp, markup);
                        qp.Price = qp.Cost + (qp.Cost * markup);
                    }

                    var quoteItem = await db.QuoteItems.FindAsync(itemId.Value);
                    if (quoteItem != null)
                    {
                        quoteItem.OverriddenPrice = newPrice.Value;
                        db.Entry(quoteItem).State = EntityState.Modified;
                    }

                    break;
                }
                case "JOB":
                {
                    var jobPrices = await db.JobPrices
                        .Where(jp => jp.JobItem == itemId.Value)
                        .Include(jp => jp.JobItem1)
                        .Include(jp => jp.JobItemService1)
                        .ToListAsync();

                    if (!jobPrices.Any())
                        return Json(new { error = "No price records found for this item." }, JsonRequestBehavior.AllowGet);

                    decimal totalCost = jobPrices.Sum(jp => jp.Cost ?? 0m);
                    decimal markup = totalCost != 0 ? (newPrice.Value - totalCost) / totalCost : 0m;

                    foreach (var jp in jobPrices)
                    {
                        await UpdateJobPriceAsync(jp, markup);
                        jp.Price = jp.Cost + (jp.Cost * markup);
                    }

                    var jobItem = await db.JobItems.FindAsync(itemId.Value);
                    if (jobItem != null)
                    {
                        jobItem.OverriddenPrice = newPrice.Value;
                        db.Entry(jobItem).State = EntityState.Modified;
                    }

                    break;
                }
                default:
                    return Json(new { error = "Invalid record type." }, JsonRequestBehavior.AllowGet);
            }

            await db.SaveChangesAsync();
            return Json(new { success = true }, JsonRequestBehavior.AllowGet);
        }

        public async Task<ActionResult> RevertItemPrice(string recordType, int? itemId)
        {
            if (!itemId.HasValue || string.IsNullOrWhiteSpace(recordType))
            {
                return Json(new { error = "Missing required parameters." }, JsonRequestBehavior.AllowGet);
            }

            switch (recordType.ToUpperInvariant())
            {
                case "QUOTE":
                {
                    var quoteItem = await db.QuoteItems.FindAsync(itemId.Value);
                    if (quoteItem == null)
                        return Json(new { error = "Item not found." }, JsonRequestBehavior.AllowGet);

                    quoteItem.CustomMarkup = null;
                    quoteItem.CustomInkMarkup = null;
                    quoteItem.CustomMaterialMarkup = null;
                    quoteItem.CustomPrintLaborMarkup = null;
                    quoteItem.OverriddenPrice = null;
                    db.Entry(quoteItem).State = EntityState.Modified;

                    var quoteServices = await db.QuoteItemServices.Where(s => s.QuoteItemId == itemId.Value).ToListAsync();
                    foreach (var svc in quoteServices)
                    {
                        svc.CustomMarkup = null;
                        db.Entry(svc).State = EntityState.Modified;
                    }

                    var quoteFinishing = await db.QuoteItemFinishingMaterials.Where(f => f.QuoteItemId == itemId.Value).ToListAsync();
                    foreach (var fm in quoteFinishing)
                    {
                        fm.CustomMarkup = null;
                        db.Entry(fm).State = EntityState.Modified;
                    }

                    var quotePrices = await db.QuotePrices.Where(qp => qp.QuoteItem == itemId.Value).ToListAsync();
                    foreach (var qp in quotePrices)
                    {
                        qp.OverriddenMarkup = false;
                        qp.Markup = null;
                        db.Entry(qp).State = EntityState.Modified;
                    }

                    break;
                }
                case "JOB":
                {
                    var jobItem = await db.JobItems.FindAsync(itemId.Value);
                    if (jobItem == null)
                        return Json(new { error = "Item not found." }, JsonRequestBehavior.AllowGet);

                    jobItem.CustomMarkup = null;
                    jobItem.CustomInkMarkup = null;
                    jobItem.CustomMaterialMarkup = null;
                    jobItem.CustomPrintLaborMarkup = null;
                    jobItem.OverriddenPrice = null;
                    db.Entry(jobItem).State = EntityState.Modified;

                    var jobServices = await db.JobItemServices.Where(s => s.JobItemId == itemId.Value).ToListAsync();
                    foreach (var svc in jobServices)
                    {
                        svc.CustomMarkup = null;
                        db.Entry(svc).State = EntityState.Modified;
                    }

                    var jobFinishing = await db.JobItemFinishingMaterials.Where(f => f.JobItemId == itemId.Value).ToListAsync();
                    foreach (var fm in jobFinishing)
                    {
                        fm.CustomMarkup = null;
                        db.Entry(fm).State = EntityState.Modified;
                    }

                    var jobPrices = await db.JobPrices.Where(jp => jp.JobItem == itemId.Value).ToListAsync();
                    foreach (var jp in jobPrices)
                    {
                        jp.OverriddenMarkup = false;
                        jp.Markup = null;
                        db.Entry(jp).State = EntityState.Modified;
                    }

                    break;
                }
                default:
                    return Json(new { error = "Invalid record type." }, JsonRequestBehavior.AllowGet);
            }

            await db.SaveChangesAsync();
            return Json(new { success = true }, JsonRequestBehavior.AllowGet);
        }

        public async Task<ActionResult> ResetServiceMarkup(int? recordId, string recType)
        {
            if (!recordId.HasValue || string.IsNullOrWhiteSpace(recType))
            {
                return Json(new { error = "Missing required parameters." }, JsonRequestBehavior.AllowGet);
            }

            switch (recType.ToUpperInvariant())
            {
                case "QUOTE":
                {
                    var qp = await db.QuotePrices.FindAsync(recordId.Value);
                    if (qp == null)
                        return Json(new { error = "Price record not found." }, JsonRequestBehavior.AllowGet);

                    qp.OverriddenMarkup = false;
                    qp.Markup = null;
                    db.Entry(qp).State = EntityState.Modified;

                    // Clear the custom markup on the associated service/material/item field
                    if (qp.IsMaterial.GetValueOrDefault())
                    {
                        var quoteItem = await db.QuoteItems.FindAsync(qp.QuoteItem);
                        if (quoteItem != null)
                        {
                            quoteItem.CustomMaterialMarkup = null;
                            db.Entry(quoteItem).State = EntityState.Modified;
                        }
                    }
                    else if (qp.Title != null && (qp.Title.Contains("Ink Cost") || qp.Title.Contains("Labor Cost")))
                    {
                        var quoteItem = await db.QuoteItems.FindAsync(qp.QuoteItem);
                        if (quoteItem != null)
                        {
                            if (qp.Title.Contains("Ink Cost"))
                                quoteItem.CustomInkMarkup = null;
                            else
                                quoteItem.CustomPrintLaborMarkup = null;
                            db.Entry(quoteItem).State = EntityState.Modified;
                        }
                    }
                    else if (qp.QuoteItemService.HasValue)
                    {
                        // Could be a service or finishing material
                        var svc = await db.QuoteItemServices.FindAsync(qp.QuoteItemService.Value);
                        if (svc != null)
                        {
                            svc.CustomMarkup = null;
                            db.Entry(svc).State = EntityState.Modified;
                        }
                        else
                        {
                            var fm = await db.QuoteItemFinishingMaterials.FindAsync(qp.QuoteItemService.Value);
                            if (fm != null)
                            {
                                fm.CustomMarkup = null;
                                db.Entry(fm).State = EntityState.Modified;
                            }
                        }
                    }
                    break;
                }
                case "JOB":
                {
                    var jp = await db.JobPrices.FindAsync(recordId.Value);
                    if (jp == null)
                        return Json(new { error = "Price record not found." }, JsonRequestBehavior.AllowGet);

                    jp.OverriddenMarkup = false;
                    jp.Markup = null;
                    db.Entry(jp).State = EntityState.Modified;

                    // Clear the custom markup on the associated service/material/item field
                    if (jp.IsMaterial.GetValueOrDefault())
                    {
                        var jobItem = await db.JobItems.FindAsync(jp.JobItem);
                        if (jobItem != null)
                        {
                            jobItem.CustomMaterialMarkup = null;
                            db.Entry(jobItem).State = EntityState.Modified;
                        }
                    }
                    else if (jp.Title != null && (jp.Title.Contains("Ink Cost") || jp.Title.Contains("Labor Cost")))
                    {
                        var jobItem = await db.JobItems.FindAsync(jp.JobItem);
                        if (jobItem != null)
                        {
                            if (jp.Title.Contains("Ink Cost"))
                                jobItem.CustomInkMarkup = null;
                            else
                                jobItem.CustomPrintLaborMarkup = null;
                            db.Entry(jobItem).State = EntityState.Modified;
                        }
                    }
                    else if (jp.JobItemService.HasValue)
                    {
                        var svc = await db.JobItemServices.FindAsync(jp.JobItemService.Value);
                        if (svc != null)
                        {
                            svc.CustomMarkup = null;
                            db.Entry(svc).State = EntityState.Modified;
                        }
                        else
                        {
                            var fm = await db.JobItemFinishingMaterials.FindAsync(jp.JobItemService.Value);
                            if (fm != null)
                            {
                                fm.CustomMarkup = null;
                                db.Entry(fm).State = EntityState.Modified;
                            }
                        }
                    }
                    break;
                }
                default:
                    return Json(new { error = "Invalid record type." }, JsonRequestBehavior.AllowGet);
            }

            await db.SaveChangesAsync();
            return Json(new { success = true }, JsonRequestBehavior.AllowGet);
        }

        public async Task<ActionResult> RemoveServiceItemEntry(int? id, string itemtype)
        {
            if (!id.HasValue || String.IsNullOrEmpty(itemtype))
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            switch (itemtype.ToUpper())
            {
                case "QUOTE":
                    QuoteItemService qisToRemove = await db.QuoteItemServices.FindAsync(id);
                    if (qisToRemove != null)
                    {
                        try
                        {
                            QuotePrice qp = await db.QuotePrices.SingleAsync(x => x.QuoteItemService == qisToRemove.Id);
                            db.QuotePrices.Remove(qp);
                        }
                        catch (Exception) { }
                        db.QuoteItemServices.Remove(qisToRemove);
                    }
                    break;

                case "JOB":
                    JobItemService jisToRemove = await db.JobItemServices.FindAsync(id);
                    if (jisToRemove != null)
                    {
                        try
                        {
                            JobPrice jp = await db.JobPrices.SingleAsync(x => x.JobItemService == jisToRemove.Id);
                            db.JobPrices.Remove(jp);
                        }
                        catch (Exception) { }
                        db.JobItemServices.Remove(jisToRemove);
                    }
                    break;
            }

            await db.SaveChangesAsync();

            return new JsonResult { Data = "done", JsonRequestBehavior = JsonRequestBehavior.AllowGet };
        }

        public async Task<ActionResult> RemoveFinishingMaterialService(int? id, string itemtype)
        {
            if (!id.HasValue || String.IsNullOrEmpty(itemtype))
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            switch (itemtype.ToUpper())
            {
                case "QUOTE":
                    QuoteItemService qisToRemove = await db.QuoteItemServices.FindAsync(id);
                    if (qisToRemove != null)
                    {
                        // Delete price record keyed to the service ID
                        try
                        {
                            QuotePrice qp = await db.QuotePrices.SingleAsync(x => x.QuoteItemService == qisToRemove.Id);
                            db.QuotePrices.Remove(qp);
                        }
                        catch (Exception) { }

                        // Delete the finishing material record and its keyed price record
                        if (qisToRemove.QuoteItemFinishingMaterial.HasValue)
                        {
                            // Delete price record keyed to the QIFM ID
                            try
                            {
                                QuotePrice qp2 = await db.QuotePrices.SingleAsync(x => x.QuoteItem == qisToRemove.QuoteItemId && x.QuoteItemService == qisToRemove.QuoteItemFinishingMaterial.Value);
                                db.QuotePrices.Remove(qp2);
                            }
                            catch (Exception) { }

                            try
                            {
                                QuoteItemFinishingMaterial qifm = await db.QuoteItemFinishingMaterials.FindAsync(qisToRemove.QuoteItemFinishingMaterial.Value);
                                if (qifm != null)
                                    db.QuoteItemFinishingMaterials.Remove(qifm);
                            }
                            catch (Exception) { }
                        }

                        db.QuoteItemServices.Remove(qisToRemove);
                    }
                    break;

                case "JOB":
                    JobItemService jisToRemove = await db.JobItemServices.FindAsync(id);
                    if (jisToRemove != null)
                    {
                        // Delete price record keyed to the service ID
                        try
                        {
                            JobPrice jp = await db.JobPrices.SingleAsync(x => x.JobItemService == jisToRemove.Id);
                            db.JobPrices.Remove(jp);
                        }
                        catch (Exception) { }

                        // Delete the finishing material record and its keyed price record
                        if (jisToRemove.JobItemFinishingMaterial.HasValue)
                        {
                            // Delete price record keyed to the JIFM ID
                            try
                            {
                                JobPrice jp2 = await db.JobPrices.SingleAsync(x => x.JobItem == jisToRemove.JobItemId && x.JobItemService == jisToRemove.JobItemFinishingMaterial.Value);
                                db.JobPrices.Remove(jp2);
                            }
                            catch (Exception) { }

                            try
                            {
                                JobItemFinishingMaterial jifm = await db.JobItemFinishingMaterials.FindAsync(jisToRemove.JobItemFinishingMaterial.Value);
                                if (jifm != null)
                                    db.JobItemFinishingMaterials.Remove(jifm);
                            }
                            catch (Exception) { }
                        }

                        db.JobItemServices.Remove(jisToRemove);
                    }
                    break;
            }

            await db.SaveChangesAsync();

            return new JsonResult { Data = "done", JsonRequestBehavior = JsonRequestBehavior.AllowGet };
        }

        public async Task<ActionResult> DeleteItemServices(string recordtype, int? id)
        {
            if (String.IsNullOrEmpty(recordtype) || !id.HasValue)
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Missing required params");

            List<QuoteItemService> qServices = new List<QuoteItemService>();
            List<QuotePrice> qPrices = new List<QuotePrice>();
            List<JobItemService> jServices = new List<JobItemService>();
            List<JobPrice> jPrices = new List<JobPrice>();

            switch (recordtype.ToUpper())
            {
                case "QUOTE":
                    qServices = await db.QuoteItemServices.Where(x => x.QuoteItemId == id.Value).ToListAsync();
                    List<int> sIds = qServices.Select(x => x.Id).ToList();
                    qPrices = await db.QuotePrices.Where(x => sIds.Contains(x.QuoteItemService.Value) && x.QuoteItem == id).ToListAsync();
                    break;

                case "JOB":
                    jServices = await db.JobItemServices.Where(x => x.JobItemId == id.Value).ToListAsync();
                    List<int> sIds2 = jServices.Select(x => x.Id).ToList();
                    jPrices = await db.JobPrices.Where(x => sIds2.Contains(x.JobItemService.Value) && x.JobItem == id).ToListAsync();
                    break;

                default:
                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Default in switch");
            }

            foreach (JobItemService jis in jServices)
                db.JobItemServices.Remove(jis);

            foreach (JobPrice jp in jPrices)
                db.JobPrices.Remove(jp);

            foreach (QuoteItemService qis in qServices)
                db.QuoteItemServices.Remove(qis);

            foreach (QuotePrice qp in qPrices)
                db.QuotePrices.Remove(qp);

            await db.SaveChangesAsync();

            return new JsonResult { Data = "done", JsonRequestBehavior = JsonRequestBehavior.AllowGet };
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

        public async Task<ActionResult> FreightAddressChange(string itemtype, int? addressId, int? recordId)
        {
            if (String.IsNullOrEmpty(itemtype) || !addressId.HasValue || !recordId.HasValue)
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Bad request");

            switch (itemtype.ToUpper())
            {
                case "QUOTE":
                    Quote q = await db.Quotes.FindAsync(recordId);
                    if (q != null)
                    {
                        q.FreightAddress = addressId;
                        db.Entry(q).State = EntityState.Modified;

                        await db.SaveChangesAsync();

                        q.FormattedFreightShippingAddress = await q.GetFormattedFreightAddress(db);

                        return new JsonResult
                        {
                            Data = new
                            {
                                CustomerName = q.FreightShippingAddress.CustomerName,
                                CustomerAddress = q.FreightShippingAddress.Address,
                                CustomerCSZ = q.FormattedShippingAddress
                            },
                            JsonRequestBehavior = JsonRequestBehavior.AllowGet
                        };
                    }
                    break;

                case "JOB":
                    Job j = await db.Jobs.FindAsync(recordId);
                    if (j != null)
                    {
                        j.ShippingAddress = addressId;
                        db.Entry(j).State = EntityState.Modified;

                        await db.SaveChangesAsync();

                        j.FormattedFreightShippingAddress = await j.GetFormattedFreightAddress(db);

                        return new JsonResult
                        {
                            Data = new
                            {
                                CustomerName = j.FreightShippingAddress.CustomerName,
                                CustomerAddress = j.FreightShippingAddress.Address,
                                CustomerCSZ = j.FormattedShippingAddress
                            },
                            JsonRequestBehavior = JsonRequestBehavior.AllowGet
                        };
                    }
                    break;

                default:
                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "in switch default");
            }

            return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Invalid Item Type");
        }

        public async Task<ActionResult> ShippingAddressChange(string itemtype, int? addressId, int? recordId)
        {
            if (String.IsNullOrEmpty(itemtype) || !addressId.HasValue || !recordId.HasValue)
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Bad request");

            switch (itemtype.ToUpper())
            {
                case "QUOTE":
                    Quote q = await db.Quotes.FindAsync(recordId);
                    if (q != null)
                    {
                        q.ShippingAddress = addressId;
                        db.Entry(q).State = EntityState.Modified;

                        await db.SaveChangesAsync();

                        q.FormattedShippingAddress = await q.GetFormattedShippingAddress(db);

                        return new JsonResult
                        {
                            Data = new
                            {
                                CustomerName = q.ShippingAddress1.CustomerName,
                                CustomerAddress = q.ShippingAddress1.Address,
                                CustomerCSZ = q.FormattedShippingAddress
                            },
                            JsonRequestBehavior = JsonRequestBehavior.AllowGet
                        };
                    }
                    break;

                case "JOB":
                    Job j = await db.Jobs.FindAsync(recordId);
                    if (j != null)
                    {
                        j.ShippingAddress = addressId;
                        db.Entry(j).State = EntityState.Modified;

                        await db.SaveChangesAsync();

                        j.FormattedShippingAddress = await j.GetFormattedShippingAddress(db);

                        return new JsonResult
                        {
                            Data = new
                            {
                                CustomerName = j.ShippingAddress1.CustomerName,
                                CustomerAddress = j.ShippingAddress1.Address,
                                CustomerCSZ = j.FormattedShippingAddress
                            },
                            JsonRequestBehavior = JsonRequestBehavior.AllowGet
                        };
                    }
                    break;

                default:
                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "in switch default");
            }

            return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Invalid Item Type");
        }

        public async Task<ActionResult> BlindShippingAddressChange(string itemtype, int? addressId, int? recordId)
        {
            if (String.IsNullOrEmpty(itemtype) || !addressId.HasValue || !recordId.HasValue)
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Bad request");

            switch (itemtype.ToUpper())
            {
                case "JOB":
                    Job j = await db.Jobs.FindAsync(recordId);
                    if (j != null)
                    {
                        j.BlindShippingAddress = addressId;
                        db.Entry(j).State = EntityState.Modified;

                        await db.SaveChangesAsync();

                        // Load the blind shipping address details
                        await db.Entry(j).Reference(r => r.BlindShippingPivotTable).LoadAsync();

                        if (j.BlindShippingPivotTable != null)
                        {
                            return new JsonResult
                            {
                                Data = new
                                {
                                    FromCompany = j.BlindShippingPivotTable.FromCompany,
                                    FromAddress = j.BlindShippingPivotTable.FromAddress,
                                    FromCSZ = $"{j.BlindShippingPivotTable.FromCity}, {j.BlindShippingPivotTable.FromState} {j.BlindShippingPivotTable.FromZip}",
                                    ShipCompany = j.BlindShippingPivotTable.ShipCompany,
                                    ShipAddress = j.BlindShippingPivotTable.ShipAddress,
                                    ShipCSZ = $"{j.BlindShippingPivotTable.ShipCity}, {j.BlindShippingPivotTable.ShipState} {j.BlindShippingPivotTable.ShipZip}"
                                },
                                JsonRequestBehavior = JsonRequestBehavior.AllowGet
                            };
                        }
                    }
                    break;

                default:
                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "in switch default");
            }

            return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Invalid Item Type");
        }

        public async Task<ActionResult> GetSelectedFinishingMaterials(string itemtype, int? itemId)
        {
            if (!itemId.HasValue || String.IsNullOrEmpty(itemtype))
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            switch (itemtype.ToUpper())
            {
                case "QUOTE":
                    List<QuoteItemFinishingMaterial> qMats =
                        await db.QuoteItemFinishingMaterials
                        .Include(i => i.Material1)
                        .Include(i => i.Material1.MaterialCategory)
                        .Where(x => x.QuoteItemId == itemId)
                        .ToListAsync();

                    return new JsonResult
                    {
                        Data = JsonConvert.SerializeObject(qMats.Select(x => new { x.Material, x.Material1.Title, Category = x.Material1.MaterialCategory.Title }).ToList(), new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };

                case "JOB":
                    List<JobItemFinishingMaterial> jMats =
                        await db.JobItemFinishingMaterials
                        .Include(i => i.Material1)
                        .Include(i => i.Material1.MaterialCategory)
                        .Where(x => x.JobItemId == itemId).ToListAsync();
                    return new JsonResult
                    {
                        Data = JsonConvert.SerializeObject(jMats.Select(x => new { x.Material, x.Material1.Title, Category = x.Material1.MaterialCategory.Title }).ToList(), new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };

                default:
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
        }

        public async Task<ActionResult> GetFinishingMaterialConfigs(string itemType, int? id)
        {
            ViewData["FinishingMaterialApplications"] = await GenerateSelectListAsync(SelectListType.FinishingFormulaApplications);

            if (String.IsNullOrEmpty(itemType) || !id.HasValue)
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "itemtype and id are required");

            switch (itemType.ToUpper())
            {
                case "QUOTE":
                    var qfm = await db.QuoteItemFinishingMaterials
                                        .Where(x => x.QuoteItemId == id)
                                        .Select(x => new
                                        {
                                            x.Id,
                                            x.FinishingFormulaConfiguration,
                                            x.FinishingFormulaApplication,
                                            x.Material1.Title,
                                            Cost = x.ItemCost,
                                            CostType = x.Material1.MaterialCostType.Title,
                                            CostTypeId = x.Material1.CostType,
                                            Stock = (int?)x.Material1.MaterialInventory.Stock,
                                            Allocated = x.Material1.MaterialInventory.Allocated
                                        })
                                        .ToListAsync();

                    return new JsonResult
                    {
                        Data = JsonConvert.SerializeObject(qfm, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };

                case "JOB":
                    var jfm = await db.JobItemFinishingMaterials
                                        .Where(x => x.JobItemId == id)
                                        .Select(x => new
                                        {
                                            x.Id,
                                            x.FinishingFormulaConfiguration,
                                            x.FinishingFormulaApplication,
                                            x.Material1.Title,
                                            Cost = x.ItemCost,
                                            CostType = x.Material1.MaterialCostType.Title,
                                            CostTypeId = x.Material1.CostType,
                                            Stock = (int?)x.Material1.MaterialInventory.Stock,
                                            Allocated = x.Material1.MaterialInventory.Allocated
                                        })
                                        .ToListAsync();

                    return new JsonResult
                    {
                        Data = JsonConvert.SerializeObject(jfm, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };

                default:
                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "in switch default");
            }
        }

        public async Task<ActionResult> GetCuttingServiceServiceIds(List<int> cuttingConfigs)
        {
            cuttingConfigs = cuttingConfigs ?? new List<int>();

            List<int> returnList = new List<int>();

            foreach (int i in cuttingConfigs)
            {
                CuttingConfiguration cc = await db.CuttingConfigurations.FindAsync(i);
                CuttingConfigurationPivot ccp = await db.CuttingConfigurationPivots.Where(x => x.GroupId == cc.GroupId).FirstAsync();
                returnList.Add(ccp.Service);
            }

            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(
                        returnList,
                        new JsonSerializerSettings
                        {
                            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                        }
                    ),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> SetCuttingConfigurations(List<int> items, List<int> configs, string itemtype)
        {
            itemtype = itemtype ?? "";
            items = items ?? new List<int>();
            configs = configs ?? new List<int>();

            for (int i = 0; i < items.Count; i++)
            {
                CuttingConfiguration cc = await db.CuttingConfigurations.FindAsync(configs[i]);

                switch (itemtype.ToUpper())
                {
                    case "QUOTE":
                        QuoteItemService qis = await db.QuoteItemServices.FindAsync(items[i]);

                        qis.ServiceConfiguration = cc.Configuration;
                        db.Entry(qis).State = EntityState.Modified;
                        break;

                    case "JOB":
                        JobItemService jis = await db.JobItemServices.FindAsync(items[i]);

                        jis.ServiceConfiguration = cc.Configuration;
                        db.Entry(jis).State = EntityState.Modified;
                        break;

                    default:
                        return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }
            }

            await db.SaveChangesAsync();

            return new HttpStatusCodeResult(HttpStatusCode.OK);
        }

        public async Task<ActionResult> CopyItem(int? id, string itemType)
        {
            if (!id.HasValue || String.IsNullOrEmpty(itemType))
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Missing required parameters");

            //copy the item
            ObjectResult<int?> res = db.CopyItem(id, itemType);

            //store the new id
            int? newId = res.FirstOrDefault();

            switch (itemType.ToUpper())
            {
                case "QUOTE":
                    QuoteItem qi = await db.QuoteItems.FindAsync(newId);
                    qi.ParseRunSize();
                    await db.Entry(qi).Reference(r => r.Press1).LoadAsync();
                    await db.Entry(qi).Reference(r => r.SpeedDenominator).LoadAsync();
                    await db.Entry(qi).Reference(r => r.Material1).LoadAsync();
                    if (qi != null)
                    {
                        string unqKey = "hasDutchCut_" + qi.Id.ToString();
                        ViewData[unqKey] = false;

                        try
                        {
                            ItemLayoutSetting ils = await db.ItemLayoutSettings.SingleAsync(x => x.QuoteItemId == qi.Id);
                            ViewData[unqKey] = ils.DutchCut;
                        }
                        catch (Exception e)
                        {
                            //no record found; move on
                        }

                        return new JsonResult
                        {
                            Data = JsonConvert.SerializeObject(RenderViewToString("~/Views/Partials/_QuoteItemRow.cshtml", new VM_QuoteItem { QuoteItem = qi })),
                            JsonRequestBehavior = JsonRequestBehavior.AllowGet
                        };
                    }

                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Unable to retrieve the new row");

                case "JOB":
                    JobItem ji = await db.JobItems.FindAsync(newId);
                    ji.ParseRunSize();
                    await db.Entry(ji).Reference(r => r.Press1).LoadAsync();
                    await db.Entry(ji).Reference(r => r.SpeedDenominator).LoadAsync();
                    await db.Entry(ji).Reference(r => r.Material1).LoadAsync();
                    if (ji != null)
                    {
                        string unqKey = "hasDutchCut_" + ji.Id.ToString();
                        ViewData[unqKey] = false;

                        try
                        {
                            ItemLayoutSetting ils = await db.ItemLayoutSettings.SingleAsync(x => x.JobItemId == ji.Id);
                            ViewData[unqKey] = ils.DutchCut;
                        }
                        catch (Exception e)
                        {
                            //no record found; move on
                        }

                        var latestProof = await db.JobProofs
                            .Where(p => p.JobItemId == ji.Id && p.Deleted == null)
                            .OrderByDescending(p => p.Version)
                            .FirstOrDefaultAsync();
                        return new JsonResult
                        {
                            Data = JsonConvert.SerializeObject(RenderViewToString("~/Views/Partials/_JobItemRow.cshtml", new VM_JobItem { JobItem = ji, LatestProofStatus = latestProof?.ApprovalStatus })),
                            JsonRequestBehavior = JsonRequestBehavior.AllowGet
                        };
                    }

                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Unable to retrieve the new row");

                default:
                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "problem retrieving new item");
            }
        }

        public List<JobItemGroup> GroupJobItems(Job job)
        {
            List<JobItemGroup> groups = new List<JobItemGroup>();

            foreach (JobItem ji in job.JobItems.Where(x => x.SubitemOf == null).ToList())
            {
                JobItemGroup newJig = new JobItemGroup { ParentItem = new GenericItemOrganizer { Id = ji.Id, RowType = "Standard", Ordinal = ji.Ordinal.Value }, SubItems = new List<GenericItemOrganizer>() };

                newJig.SubItems = newJig.SubItems.Concat(job.JobItems.Where(x => x.SubitemOf == ji.Id).Select(x => new GenericItemOrganizer { Id = x.Id, RowType = "Standard", Ordinal = x.Ordinal.Value })).ToList();
                newJig.SubItems = newJig.SubItems.Concat(job.JobOutsourcedItems.Where(x => x.SubitemOf == ji.Id).Select(x => new GenericItemOrganizer { Id = x.Id, RowType = "Outsourced", Ordinal = x.Ordinal.Value })).ToList();

                groups.Add(newJig);
            }

            foreach (JobOutsourcedItem joi in job.JobOutsourcedItems.Where(x => x.SubitemOf == null).ToList())
            {
                JobItemGroup newJig = new JobItemGroup { ParentItem = new GenericItemOrganizer { Id = joi.Id, Ordinal = joi.Ordinal.Value, RowType = "OUTSOURCED" }, SubItems = new List<GenericItemOrganizer>() };

                groups.Add(newJig);
            }

            return groups;
        }

        public List<QuoteItemGroup> GroupQuoteItems(Quote quote)
        {
            List<QuoteItemGroup> groups = new List<QuoteItemGroup>();

            foreach (QuoteItem qi in quote.QuoteItems.Where(x => x.SubitemOf == null).ToList())
            {
                QuoteItemGroup newQig = new QuoteItemGroup { ParentItem = new GenericItemOrganizer { Id = qi.Id, Ordinal = qi.Ordinal.Value, RowType = "Standard" }, SubItems = new List<GenericItemOrganizer>() };

                newQig.SubItems = newQig.SubItems.Concat(quote.QuoteItems.Where(x => x.SubitemOf == qi.Id).Select(x => new GenericItemOrganizer { Id = x.Id, RowType = "Standard", Ordinal = x.Ordinal.Value })).ToList();
                newQig.SubItems = newQig.SubItems.Concat(quote.QuoteOutsourcedItems.Where(x => x.SubitemOf == qi.Id).Select(x => new GenericItemOrganizer { Id = x.Id, RowType = "Outsourced", Ordinal = x.Ordinal.Value })).ToList();

                groups.Add(newQig);
            }

            foreach (QuoteOutsourcedItem qoi in quote.QuoteOutsourcedItems.Where(x => x.SubitemOf == null).ToList())
            {
                QuoteItemGroup newQig = new QuoteItemGroup { ParentItem = new GenericItemOrganizer { Id = qoi.Id, Ordinal = qoi.Ordinal.Value, RowType = "OUTSOURCED" }, SubItems = new List<GenericItemOrganizer>() };

                groups.Add(newQig);
            }

            return groups;
        }

        public async Task<ActionResult> GetJobTicket(int? job)
        {
            VM_JobTicket vm = new VM_JobTicket();

            Job j = await db.Jobs.FindAsync(job);
            if (j == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Job not found");

            await db.Entry(j).Collection(c => c.JobItems).LoadAsync();
            await db.Entry(j).Collection(c => c.JobOutsourcedItems).LoadAsync();
            await db.Entry(j).Reference(r => r.Client1).LoadAsync();
            await db.Entry(j).Reference(r => r.ShippingAddress1).LoadAsync();
            await db.Entry(j).Reference(r => r.Contact1).LoadAsync();
            await db.Entry(j).Reference(r => r.ShippingOption).LoadAsync();
            await db.Entry(j).Reference(r => r.User).LoadAsync();

            if (j.ShippingAddress.HasValue)
            {
                await db.Entry(j.ShippingAddress1).Reference(r => r.USState).LoadAsync();
            }

            if (j.Client.HasValue)
            {
                await db.Entry(j.Client1).Reference(r => r.USState).LoadAsync();
            }
            //await db.Entry(j).Reference(r => r.JobStatus).LoadAsync();

            foreach (JobOutsourcedItem joi in j.JobOutsourcedItems.ToList())
            {
                await db.Entry(joi).Reference(r => r.Vendor1).LoadAsync();
            }

            foreach (JobItem ji in j.JobItems.ToList())
            {
                await db.Entry(ji).Collection(c => c.JobItemServices).LoadAsync();
                await db.Entry(ji).Reference(r => r.JobStatus1).LoadAsync();
                await db.Entry(ji).Reference(r => r.Material1).LoadAsync();
                await db.Entry(ji).Reference(r => r.Press1).LoadAsync();
                await db.Entry(ji).Reference(r => r.PressSpeed).LoadAsync();
                await db.Entry(ji).Reference(r => r.SpeedDenominator).LoadAsync();
                await db.Entry(ji).Collection(c => c.JobItemFinishingMaterials).LoadAsync();

                foreach (JobItemFinishingMaterial jifm in ji.JobItemFinishingMaterials)
                {
                    await db.Entry(jifm).Reference(r => r.Material1).LoadAsync();
                    jifm.ParseConfiguration(jifm.FinishingFormulaConfiguration);
                }

                foreach (JobItemService jis in ji.JobItemServices.ToList())
                {
                    await db.Entry(jis).Reference(r => r.JobStatus1).LoadAsync();
                    await db.Entry(jis).Reference(r => r.Service).LoadAsync();
                    await db.Entry(jis.Service).Reference(r => r.ServiceCategory).LoadAsync();
                    await db.Entry(jis).Reference(r => r.User).LoadAsync();

                    jis.ParseServiceConfiguration();

                    jis.FormattedServiceTime = (!jis.ServiceTime.HasValue || jis.ServiceTime.Value == 0m) ? "" : DecimalHoursToFormatted(jis.ServiceTime);
                }

                if (ji.Material.HasValue)
                {
                    ji.Material1.ParseDimensions();
                }

                ji.ParseRunSize();
            }

            //code copilot
            // Fetch service details using the imported function
            var serviceDetails = await db.ServiceTimeEntriesByJob(j.Id).ToListAsync();
            foreach (var jobItem in j.JobItems)
            {
                //hotfix to set Out to 1 if null
                if (!jobItem.Out.HasValue)
                    jobItem.Out = 1;
            }

            vm.ItemGroups = GroupJobItems(j);

            vm.Job = j;

            vm.ServiceTypes = await db.ServiceTypes.OrderBy(x => x.JobTicketOrdinal).ToListAsync();

            try
            {
                return View("~/Views/Job/JobTicket.cshtml", vm);
            }
            catch (Exception e)
            {
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, e.Message);
            }
        }

        public async Task<ActionResult> CustomerQuote3(int? quoteId, int? showTotal)
        {
            bool includeTotal = (showTotal ?? 0) == 1;

            Quote q = await db.Quotes.FindAsync(quoteId);

            if (q != null)
            {
                //get items (generic item organizer)
                //get customer
                //get contact
                //get shipping
                //get source info
                //^^^ ref VM_CustomerQuote3

                List<QuoteParentItemTotals_Result> parentTotals = await db.QuoteParentItemTotals(quoteId).ToListAsync();

                await db.Entry(q).Reference(r => r.User).LoadAsync();

                VM_CustomerQuote3 vm = new VM_CustomerQuote3
                {
                    SourceInformation = new Models.CustomerQuoteModels.SourceInformation(),
                    CurrentDateStringFull = DateTime.Now.ToString("dddd, MMMM dd, yyyy"),
                    ProjectManagerName = String.Format("{0} {1}", (q.User.FirstName ?? ""), (q.User.LastName ?? "")),
                    QuoteNumber = q.JobNumber ?? "<No Quote Number!>",
                    IsShipped = !(q.IsPickup ?? false),
                    QuoteDescription = q.Description ?? ""
                };
                vm.QuoteItems = new List<QuoteTicket_QuoteItem>();
                vm.QuantitySums = new List<QuantitySums>();

                await db.Entry(q).Collection(c => c.QuoteItems).LoadAsync();
                await db.Entry(q).Collection(c => c.QuoteOutsourcedItems).LoadAsync();
                await db.Entry(q).Reference(r => r.Client1).LoadAsync();
                await db.Entry(q.Client1).Reference(r => r.USState1).LoadAsync();
                await db.Entry(q.Client1).Reference(r => r.USState).LoadAsync();
                await db.Entry(q.Client1).Reference(r => r.USState2).LoadAsync();
                await db.Entry(q).Reference(r => r.Contact1).LoadAsync();
                await db.Entry(q).Reference(r => r.ShippingAddress1).LoadAsync();

                vm.ClientInformation = new Models.CustomerQuoteModels.ClientInformation();

                vm.ClientInformation.ContactName = q.Contact.HasValue ? String.Format("{0} {1}", q?.Contact1.First, q?.Contact1.Last) : "";
                vm.ClientInformation.Attention = "";
                vm.ClientInformation.Address = q?.Client1.Address ?? "";
                vm.ClientInformation.ClientName = q?.Client1.Title ?? "";
                vm.ClientInformation.City = q?.Client1.City ?? "";
                if (q.Client1.USStateId.HasValue)
                {
                    vm.ClientInformation.State = q.Client1.USState2.Abbreviation ?? "";
                }
                else
                {
                    vm.ClientInformation.State = "";
                }
                //vm.ClientInformation.State = q?.Client1.USState1.Abbreviation ?? "";
                vm.ClientInformation.ZipCode = q?.Client1.Zip ?? "";
                vm.ClientInformation.Email = q.Contact.HasValue ? (q?.Contact1.Email ?? "No Email Address") : "";
                vm.ClientInformation.Phone = q.Contact.HasValue ? (q?.Contact1.Phone ?? "No Phone Number") : "";

                /*vm.ClientInformation = new Models.CustomerQuoteModels.ClientInformation
                {
                    ContactName = q.Contact.HasValue ? String.Format("{0} {1}", q?.Contact1.First, q?.Contact1.Last) : "",
                    Attention = "",
                    Address = q?.Client1.Address ?? "",
                    ClientName = q?.Client1.Title ?? "",
                    City = q?.Client1.City ?? "",
                    State = q?.Client1.USState1.Abbreviation ?? "",
                    ZipCode = q?.Client1.Zip ?? "",
                    Email = q.Contact.HasValue ? (q?.Contact1.Email ?? "No Email Address") : "",
                    Phone = q.Contact.HasValue ? (q?.Contact1.Phone ?? "No Phone Number") : ""
                };*/

                if ((q.IsPickup ?? false) || !q.ShippingAddress.HasValue)
                {
                    vm.ShippingInformation = new Models.CustomerQuoteModels.ShippingInformation
                    {
                        Address = "",
                        Address2 = "",
                        Attention = "",
                        City = "",
                        State = "",
                        ZipCode = "",
                        CompanyName = ""
                    };
                }
                else
                {
                    await db.Entry(q.ShippingAddress1).Reference(r => r.USState).LoadAsync();

                    vm.ShippingInformation = new Models.CustomerQuoteModels.ShippingInformation
                    {
                        Address = q?.ShippingAddress1.Address,
                        Address2 = q?.ShippingAddress1.Address2,
                        Attention = q?.ShippingAddress1.Attention,
                        City = q?.ShippingAddress1.City,
                        State = q?.ShippingAddress1.USState.Abbreviation,
                        ZipCode = q?.ShippingAddress1.Zip,
                        CompanyName = q?.ShippingAddress1.CustomerName ?? ""
                    };
                }

                List<GenericItemOrganizer> items = new List<GenericItemOrganizer>();

                items = items.Concat(q.QuoteItems.Select(x => new GenericItemOrganizer
                {
                    RowType = "Standard",
                    Id = x.Id,
                    Ordinal = x.Ordinal ?? -1
                })).ToList();
                items = items.Concat(q.QuoteOutsourcedItems.Select(x => new GenericItemOrganizer
                {
                    RowType = "Outsourced",
                    Id = x.Id,
                    Ordinal = x.Ordinal ?? -1
                })).ToList();

                items = items.OrderBy(x => x.Ordinal).ToList();

                foreach (GenericItemOrganizer gio in items)
                {
                    switch (gio.RowType)
                    {
                        case "Standard":
                            QuoteItem qi = q.QuoteItems.FirstOrDefault(x => x.Id == gio.Id);
                            await db.Entry(qi).Reference(r => r.Material1).LoadAsync();
                            await db.Entry(qi).Collection(c => c.QuoteItemServices).LoadAsync();
                            foreach (QuoteItemService qis in qi.QuoteItemServices.ToList())
                            {
                                await db.Entry(qis).Reference(r => r.Service).LoadAsync();
                                if (qis.Service == null) continue;
                                await db.Entry(qis.Service).Reference(r => r.ServiceCategory).LoadAsync();
                                await db.Entry(qis.Service.ServiceCategory).Reference(r => r.ServiceType1).LoadAsync();
                            }

                            qi.ParseRunSize();

                            decimal costForItem = parentTotals.Single(x => x.QuoteItem == qi.Id).Total_Price ?? 0m;
                            decimal quantityForCalc = Convert.ToDecimal(qi.Quantity ?? 1);

                            vm.QuoteItems.Add(new QuoteTicket_QuoteItem
                            {
                                Description = qi.Description,
                                StockName = qi.Material.HasValue ? (qi.Material1.FriendlyName ?? qi.Material1.Title) : "",
                                Size = String.Format("{0}\" x {1}\"", qi.Length, qi.Width),
                                SidesPrinting = qi.PressColorCountBack > 0 ? "2-Sided" : "1-Sided",
                                Colors = String.Format("{0} {1} {2}", qi.PressColorProfileFront, (qi.PressColorCountBack > 0 ? "/" : ""), (qi.PressColorCountBack > 0 ? qi.PressColorProfileBack : "")),
                                Quantity = qi.Quantity ?? 0,
                                Price = costForItem,
                                PriceEach = Math.Round(costForItem / quantityForCalc, 4),
                                Ordinal = qi.Ordinal ?? 0,
                                FinishingServiceString = string.Join(", ",
                                                            qi.QuoteItemServices
                                                                .Where(x => x.Service != null && x.Service.ServiceCategory.ServiceType1.Title == "Finishing" && (x.Service.ShowOnQuote ?? false))
                                                                .OrderBy(x => x.Ordinal)
                                                                .Select(x => x.Title)),
                                ArtworkProvided = qi.ArtworkProvided ?? true,
                                DesignHours = qi.DesignHours ?? 0m,
                                Notes = qi.Notes ?? "",
                                HasDefinedDimensions = (qi.Length != "0" && qi.Width != "0"),
                                IsOutsourced = false,
                                DoubleHit = qi.DoubleHit ?? false
                            });
                            break;

                        case "Outsourced":
                            QuoteOutsourcedItem qoi = q.QuoteOutsourcedItems.FirstOrDefault(x => x.Id == gio.Id);

                            decimal costForOItem = parentTotals.Single(x => x.QuoteItem == qoi.Id && x.IsOutsourced == 1).Total_Price ?? 0m;
                            decimal quantityForOCalc = Convert.ToDecimal(qoi.Quantity ?? 0);

                            vm.QuoteItems.Add(new QuoteTicket_QuoteItem
                            {
                                Description = qoi.Description,
                                StockName = "N/A",
                                Size = "N/A",
                                SidesPrinting = "N/A",
                                Colors = "N/A",
                                Quantity = qoi.Quantity ?? 0,
                                Price = costForOItem,
                                PriceEach = Math.Round(costForOItem / quantityForOCalc, 4),
                                Ordinal = qoi.Ordinal ?? 0,
                                HasDefinedDimensions = false,
                                Notes = qoi.Notes ?? "",
                                IsOutsourced = true
                            });
                            break;

                        default:
                            break;
                    }
                }

                var groupedByDescription =
                    vm.QuoteItems.GroupBy(x => x.Description).ToList();

                return View("~/Views/Job/CustomerQuote3.cshtml", vm);
            }

            return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Quote not found");
        }

        public async Task<ActionResult> CustomerQuote4(int? quoteId, bool? showTotal)
        {
            showTotal = showTotal ?? false;

            Quote q = await db.Quotes.FindAsync(quoteId);

            if (q != null)
            {
                List<QuoteParentItemTotals_Result> parentTotals = await db.QuoteParentItemTotals(quoteId).ToListAsync();

                await db.Entry(q).Reference(r => r.User).LoadAsync();

                VM_CustomerQuote4 vm = new VM_CustomerQuote4
                {
                    SourceInformation = new Models.CustomerQuoteModels.SourceInformation(),
                    CurrentDateStringFull = DateTime.Now.ToString("dddd, MMMM dd, yyyy"),
                    ProjectManagerName = String.Format("{0} {1}", (q.User.FirstName ?? ""), (q.User.LastName ?? "")),
                    QuoteNumber = q.JobNumber ?? "<No Quote Number!>",
                    IsShipped = !(q.IsPickup ?? false),
                    QuoteDescription = q.Description ?? "",
                    ShowTotalPrice = showTotal.Value,
                    TotalPriceString = ("$" + parentTotals.Sum(x => (x.Total_Price ?? 0m)).ToString("N2")),
                    ClientPONumber = q.ClientPONumber ?? ""
                };
                vm.QuoteItems = new List<QuoteTicket_QuoteItem>();
                vm.QuantitySums = new List<QuantitySums>();

                await db.Entry(q).Collection(c => c.QuoteItems).LoadAsync();
                await db.Entry(q).Collection(c => c.QuoteOutsourcedItems).LoadAsync();
                await db.Entry(q).Reference(r => r.Client1).LoadAsync();
                await db.Entry(q.Client1).Reference(r => r.USState1).LoadAsync();
                await db.Entry(q.Client1).Reference(r => r.USState).LoadAsync();
                await db.Entry(q.Client1).Reference(r => r.USState2).LoadAsync();
                await db.Entry(q).Reference(r => r.Contact1).LoadAsync();
                await db.Entry(q).Reference(r => r.ShippingAddress1).LoadAsync();

                vm.ClientInformation = new Models.CustomerQuoteModels.ClientInformation();

                vm.ClientInformation.ContactName = q.Contact.HasValue ? String.Format("{0} {1}", q?.Contact1.First, q?.Contact1.Last) : "";
                vm.ClientInformation.Attention = "";
                vm.ClientInformation.Address = q?.Client1.Address ?? "";
                vm.ClientInformation.ClientName = q?.Client1.Title ?? "";
                vm.ClientInformation.City = q?.Client1.City ?? "";
                if (q.Client1.USStateId.HasValue)
                {
                    vm.ClientInformation.State = q.Client1.USState2.Abbreviation ?? "";
                }
                else
                {
                    vm.ClientInformation.State = "";
                }
                vm.ClientInformation.ZipCode = q?.Client1.Zip ?? "";
                vm.ClientInformation.Email = q.Contact.HasValue ? (q?.Contact1.Email ?? "No Email Address") : "";
                vm.ClientInformation.Phone = q.Contact.HasValue ? (q?.Contact1.Phone ?? "No Phone Number") : "";
                vm.ClientInformation.AREmail = q?.Client1.AREmail ?? "";

                if ((q.IsPickup ?? false) || !q.ShippingAddress.HasValue)
                {
                    vm.ShippingInformation = new Models.CustomerQuoteModels.ShippingInformation
                    {
                        Address = "",
                        Address2 = "",
                        Attention = "",
                        City = "",
                        State = "",
                        ZipCode = "",
                        CompanyName = ""
                    };
                }
                else
                {
                    await db.Entry(q.ShippingAddress1).Reference(r => r.USState).LoadAsync();

                    vm.ShippingInformation = new Models.CustomerQuoteModels.ShippingInformation
                    {
                        Address = q?.ShippingAddress1.Address,
                        Address2 = q?.ShippingAddress1.Address2,
                        Attention = q?.ShippingAddress1.Attention,
                        City = q?.ShippingAddress1.City,
                        State = q?.ShippingAddress1.USState.Abbreviation,
                        ZipCode = q?.ShippingAddress1.Zip,
                        CompanyName = q?.ShippingAddress1.CustomerName
                    };
                }

                List<GenericItemOrganizer> items = new List<GenericItemOrganizer>();

                items = items.Concat(q.QuoteItems.Select(x => new GenericItemOrganizer
                {
                    RowType = "Standard",
                    Id = x.Id,
                    Ordinal = x.Ordinal ?? -1
                })).ToList();
                items = items.Concat(q.QuoteOutsourcedItems.Select(x => new GenericItemOrganizer
                {
                    RowType = "Outsourced",
                    Id = x.Id,
                    Ordinal = x.Ordinal ?? -1
                })).ToList();

                items = items.OrderBy(x => x.Ordinal).ToList();

                foreach (GenericItemOrganizer gio in items)
                {
                    switch (gio.RowType)
                    {
                        case "Standard":
                            QuoteItem qi = q.QuoteItems.FirstOrDefault(x => x.Id == gio.Id);
                            await db.Entry(qi).Reference(r => r.Material1).LoadAsync();
                            await db.Entry(qi).Collection(c => c.QuoteItemServices).LoadAsync();
                            foreach (QuoteItemService qis in qi.QuoteItemServices.ToList())
                            {
                                await db.Entry(qis).Reference(r => r.Service).LoadAsync();
                                if (qis.Service == null) continue;
                                await db.Entry(qis.Service).Reference(r => r.ServiceCategory).LoadAsync();
                                await db.Entry(qis.Service.ServiceCategory).Reference(r => r.ServiceType1).LoadAsync();
                            }

                            qi.ParseRunSize();

                            decimal costForItem = parentTotals.Single(x => x.QuoteItem == qi.Id).Total_Price ?? 0m;
                            decimal quantityForCalc = Convert.ToDecimal(qi.Quantity ?? 1);

                            int sidesPrinted = 0;
                            if (qi.PressColorCountFront > 0)
                            {
                                sidesPrinted++;
                            }
                            if (qi.PressColorCountBack > 0)
                            {
                                sidesPrinted++;
                            }

                            vm.QuoteItems.Add(new QuoteTicket_QuoteItem
                            {
                                Description = qi.Description,
                                StockName = qi.Material.HasValue ? (qi.Material1.FriendlyName ?? qi.Material1.Title) : "",
                                Size = String.Format("{0}\" x {1}\"", qi.Length, qi.Width),
                                SidesPrinting = sidesPrinted == 0 ? "" : String.Format("{0}-Sided", sidesPrinted),
                                Colors = String.Format("{0} {1} {2}", qi.PressColorProfileFront, (qi.PressColorCountBack > 0 ? "/" : ""), (qi.PressColorCountBack > 0 ? qi.PressColorProfileBack : "")),
                                Quantity = qi.Quantity ?? 0,
                                Price = costForItem,
                                PriceEach = Math.Round(costForItem / quantityForCalc, 4),
                                Ordinal = qi.Ordinal ?? 0,
                                FinishingServiceString = string.Join(", ",
                                                            qi.QuoteItemServices
                                                                .Where(x => x.Service != null && x.Service.ServiceCategory.ServiceType1.Title == "Finishing" && (x.Service.ShowOnQuote ?? false))
                                                                .OrderBy(x => x.Ordinal)
                                                                .Select(x => x.Title)),
                                ArtworkProvided = qi.ArtworkProvided ?? true,
                                DesignHours = qi.DesignHours ?? 0m,
                                Notes = qi.Notes ?? "",
                                HasDefinedDimensions = (qi.Length != "0" && qi.Width != "0"),
                                IsOutsourced = false,
                                DoubleHit = qi.DoubleHit ?? false
                            });
                            break;

                        case "Outsourced":
                            QuoteOutsourcedItem qoi = q.QuoteOutsourcedItems.FirstOrDefault(x => x.Id == gio.Id);

                            decimal costForOItem = parentTotals.Single(x => x.QuoteItem == qoi.Id && x.IsOutsourced == 1).Total_Price ?? 0m;
                            decimal quantityForOCalc = Convert.ToDecimal(qoi.Quantity ?? 0);

                            vm.QuoteItems.Add(new QuoteTicket_QuoteItem
                            {
                                Description = qoi.Description,
                                StockName = "N/A",
                                Size = "N/A",
                                SidesPrinting = "N/A",
                                Colors = "N/A",
                                Quantity = qoi.Quantity ?? 0,
                                Price = costForOItem,
                                PriceEach = Math.Round(costForOItem / quantityForOCalc, 4),
                                Ordinal = qoi.Ordinal ?? 0,
                                HasDefinedDimensions = false,
                                Notes = qoi.Notes ?? "",
                                IsOutsourced = true
                            });
                            break;

                        default:
                            break;
                    }
                }

                var groupedByDescription =
                    vm.QuoteItems.GroupBy(x => x.Description).ToList();

                return View("~/Views/Job/CustomerQuote4.cshtml", vm);
            }

            return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Quote not found");
        }

        public async Task<ActionResult> DeleteIncomplete(string itemType, int? id)
        {
            if (!String.IsNullOrEmpty(itemType) && id.HasValue)
            {
                //using (ConnectToDatabase())
                {
                    switch (itemType.ToUpper())
                    {
                        case "QUOTE":
                            Quote q = await db.Quotes.FindAsync(id);
                            if (q != null)
                                db.Quotes.Remove(q);
                            else
                                return new HttpStatusCodeResult(HttpStatusCode.OK, "No quote actually deleted");
                            break;

                        case "JOB":
                            Job j = await db.Jobs.FindAsync(id);
                            if (j != null)
                                db.Jobs.Remove(j);
                            else
                                return new HttpStatusCodeResult(HttpStatusCode.OK, "No job actually deleted");
                            break;

                        default:
                            return new HttpStatusCodeResult(HttpStatusCode.OK, "Nothing deleted");
                    }

                    await db.SaveChangesAsync();

                    return new HttpStatusCodeResult(HttpStatusCode.OK, "Nothing deleted");
                }
            }

            return new HttpStatusCodeResult(HttpStatusCode.OK, "Nothing deleted");
        }

        public async Task<ActionResult> GetItemNotes(int? id, string itemType, bool isTemp)
        {
            if (!id.HasValue || String.IsNullOrEmpty(itemType))
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Missing required parameters");

            //using (ConnectToDatabase())
            {
                if (isTemp)
                {
                    Service service = await db.Services.FindAsync(id);
                    if (service == null)
                        return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Service not found");

                    return new JsonResult { Data = JsonConvert.SerializeObject(new VM_ItemNotes { Notes = service.Notes, Id = service.Id }), JsonRequestBehavior = JsonRequestBehavior.AllowGet };
                }
                else
                {
                    switch (itemType.ToUpper())
                    {
                        case "QUOTE":
                            QuoteItemService qis = await db.QuoteItemServices.FindAsync(id);
                            if (qis == null)
                                return new HttpStatusCodeResult(HttpStatusCode.NotFound, "QuoteItemService not found");

                            return new JsonResult { Data = JsonConvert.SerializeObject(new VM_ItemNotes { Notes = qis.Notes, Id = qis.Id }, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }), JsonRequestBehavior = JsonRequestBehavior.AllowGet };

                        case "JOB":
                            JobItemService jis = await db.JobItemServices.FindAsync(id);
                            if (jis == null)
                                return new HttpStatusCodeResult(HttpStatusCode.NotFound, "JobItemService not found");

                            return new JsonResult { Data = JsonConvert.SerializeObject(new VM_ItemNotes { Notes = jis.Notes, Id = jis.Id }, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }), JsonRequestBehavior = JsonRequestBehavior.AllowGet };

                        default:
                            return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Invalid itemType");
                    }
                }
            }
        }

        public async Task<ActionResult> SaveItemNotes(int? id, string notes, string itemType)
        {
            if (!id.HasValue || String.IsNullOrEmpty(itemType))
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Missing required parameters");

            //using (ConnectToDatabase())
            {
                switch (itemType.ToUpper())
                {
                    case "JOB":
                        JobItemService jis = await db.JobItemServices.FindAsync(id);

                        if (jis == null)
                            return new HttpStatusCodeResult(HttpStatusCode.NotFound, "JobItemService not found");

                        jis.Notes = notes;
                        db.Entry(jis).State = EntityState.Modified;
                        break;

                    case "QUOTE":
                        QuoteItemService qis = await db.QuoteItemServices.FindAsync(id);
                        if (qis == null)
                            return new HttpStatusCodeResult(HttpStatusCode.NotFound, "QuoteItemService not found");

                        qis.Notes = notes;
                        db.Entry(qis).State = EntityState.Modified;
                        break;
                }

                await db.SaveChangesAsync();

                return new HttpStatusCodeResult(HttpStatusCode.OK);
            }
        }

        public async Task<ActionResult> QuickItemUpdate(string itemtype, int? itemRecordId, int? cols, int? rows, int? sheets)
        {
            if (!itemRecordId.HasValue)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "No record id provided");

            switch (itemtype.ToUpper())
            {
                case "QUOTE":
                    QuoteItem qi = await db.QuoteItems.FindAsync(itemRecordId);
                    if (qi != null)
                    {
                        qi.Columns = cols;
                        qi.Rows = rows;
                        qi.Sheets = sheets;

                        await db.SaveChangesAsync();
                    }
                    break;

                case "JOB":
                    JobItem ji = await db.JobItems.FindAsync(itemRecordId);
                    if (ji != null)
                    {
                        ji.Columns = cols;
                        ji.Rows = rows;
                        ji.Sheets = sheets;

                        await db.SaveChangesAsync();
                    }
                    break;

                default:
                    break;
            }

            return new JsonResult { Data = "Done", JsonRequestBehavior = JsonRequestBehavior.AllowGet };
        }

        public async Task<ActionResult> Details(int? jobId)
        {
            if (jobId.HasValue)
            {
                //using (ConnectToDatabase())
                {
                    Job j = await db.Jobs.FindAsync(jobId);

                    if (j == null)
                        return new HttpStatusCodeResult(System.Net.HttpStatusCode.NotFound);
                    else
                        return PartialView("~/Views/Job/DetailsSimple.cshtml", j);

                    //return job details (rendered partial view?)
                }
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError);
        }

        public ActionResult Index()
        {
            return View();
        }

        public async Task<ActionResult> GetAllQuotes()
        {
            //using (ConnectToDatabase())
            {
                List<Quote> quotes = await db.Quotes.Where(x => x.Deleted == null).ToListAsync();

                List<List<ValueTuple<string, bool>>> test = new List<List<ValueTuple<string, bool>>>();
                foreach (Quote q in quotes)
                {
                    List<ValueTuple<string, bool>> itemList = new List<ValueTuple<string, bool>>
                    {
                        (q.Id.ToString(), false),
                        (q.Title ?? "", false),
                        (q.PriceLevel == null ? "No Price Level Set" : q.PriceLevel.Title, false),
                        (String.Format("<input type=\"button\" class=\"btn btn-success\" value=\"Choose\" data-record-id=\"{0}\" />", q.Id.ToString()), true)
                    };

                    test.Add(itemList);
                }

                FifthPress.Models.DataTable dt = new FifthPress.Models.DataTable
                {
                    /* Set column headers HERE */
                    TableHead = RenderViewToString("~/Views/Partials/Table/_TableHead.cshtml", new List<string> { "This", "Is", "A", "Header" }),
                    TableBody = RenderViewToString("~/Views/Partials/Table/_TableBody.cshtml", test)
                };

                return new JsonResult { Data = JsonConvert.SerializeObject(dt), JsonRequestBehavior = JsonRequestBehavior.AllowGet };
            }
        }

        public async Task<ActionResult> GetAllJobs()
        {
            //using (ConnectToDatabase())
            {
                var jobs = await db.Jobs.Where(x => x.Deleted == null).ToListAsync();

                //job num, title, customer name

                return new JsonResult { Data = JsonConvert.SerializeObject(jobs), JsonRequestBehavior = JsonRequestBehavior.AllowGet };
            }
        }

        public async Task<string> GenerateJobNumber(RecordType typeOfRecord)
        {
            int recordCount;

            switch (typeOfRecord)
            {
                case RecordType.Job:
                    string lastTwoOfYear = DateTime.UtcNow.Year.ToString().Substring(2, 2);
                    int maxJobNumber = db.v_JobNumbersAsInts.OrderByDescending(x => x.ParsedNumber).First().ParsedNumber ?? 0;
                    maxJobNumber++;
                    return String.Format("{0}-{1}", lastTwoOfYear, maxJobNumber.ToString().PadLeft(4, '0'));

                /*
            case RecordType.Quote:
                recordCount = await db.Quotes.Where(x => x.Deleted == null).CountAsync();
                recordCount++;
                return String.Format("{0}", recordCount.ToString().PadLeft(4, '0'));
                */

                case RecordType.Quote:
                    // Retrieve all non-deleted quote numbers, and bring them into memory
                    var quoteNumbers = await db.Quotes
                        .Where(x => x.Deleted == null)
                        .Select(x => x.JobNumber)
                        .ToListAsync();

                    // Parse to integers and find the maximum, defaulting to 0 if none exist
                    var maxQuoteNumber = quoteNumbers
                        .Select(q => int.TryParse(q, out var num) ? num : 0)
                        .DefaultIfEmpty(0)
                        .Max();

                    // Increment the maximum quote number to get the next in sequence
                    var newQuoteNumber = maxQuoteNumber + 1;

                    // Format the new quote number as a 4-character string, padding with leading zeros
                    return newQuoteNumber.ToString().PadLeft(4, '0');

                default:
                    return "";
            }
        }

        [HttpPost]
        public async Task<ActionResult> CreateQuoteFromJob(int? jobId)
        {
            Job j = await db.Jobs.FindAsync(jobId);

            if (j != null)
            {
                int userId = -1;
                userId = UserSessionHelper.GetActiveUserId(HttpContext);

                if (userId != -1)
                {
                    ObjectResult<int?> res = db.ConvertJobToQuote(jobId, userId);
                    int newQuoteId = res.FirstOrDefault() ?? 0;

                    //copy documents
                    try
                    {
                        await j.CopyDocumentsToNewQuote(newQuoteId, db);
                    }
                    catch (Exception)
                    {
                        return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "There was an error copying documents. The quote was still created.");
                    }

                    return new JsonResult
                    {
                        Data = JsonConvert.SerializeObject(newQuoteId),
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };
                }

                return new HttpStatusCodeResult(HttpStatusCode.NotFound, "User not found");
            }

            return new HttpStatusCodeResult(HttpStatusCode.NotFound, "job not found");
        }

        public async Task<ActionResult> CreateJobFromQuote(int? quoteId, string PurchaseOrderNumber, DateTime? DueDate)
        {
            if (quoteId.HasValue)
            {
                HttpCookie activeUser = HttpContext.Response.Cookies["ActiveUser"];
                int userId = int.Parse(activeUser.Value);
                ObjectResult<int?> res = db.ConvertQuoteToJob(quoteId, userId);

                int newJobId = res.FirstOrDefault() ?? 0;

                //allocate the materials
                Job j = await db.Jobs.FindAsync(newJobId);
                j.ScheduleSquash = true;
                await db.Entry(j).Collection(c => c.JobItems).LoadAsync();
                await db.Entry(j).Reference(r => r.Client1).LoadAsync();

                //set tax exempt status based on client
                j.TaxExempt = (j.Client1.TaxExempt ?? false);

                await db.SaveChangesAsync();

                //allocate materials
                //foreach (JobItem ji in j.JobItems)
                //{
                //if (ji.Material.HasValue)
                //{
                //await db.Entry(ji).Reference(r => r.Material1).LoadAsync();
                //if roll stock
                //if ((Material.AvailableStockTypes)ji.Material1.StockType == Material.AvailableStockTypes.RollStock)
                //{
                //ji.ParseRunSize();
                //await AllocateMaterial(ji.Material, (int)(Math.Ceiling(ji.RunLength / 12m)), j.Id, ji.Id);
                //}
                //otherwise sheets
                //else
                //{
                //    await AllocateMaterial(ji.Material, ji.BuySheets, j.Id, ji.Id);
                //}
                //}

                //await db.Entry(ji).Collection(c => c.JobItemFinishingMaterials).LoadAsync();

                //                    foreach (JobItemFinishingMaterial jifm in ji.JobItemFinishingMaterials.ToList())
                //                    {
                //                        await AllocateMaterial(jifm.Material, (int)Math.Ceiling(jifm.FinalCount ?? 0m), j.Id, ji.Id);
                //                    }
                //}

                //end allocate the materials

                //copy any documents
                try
                {
                    //out of order but i need to get this published
                    Quote q = await db.Quotes.FindAsync(quoteId);
                    await q.CopyDocumentsToNewJob(newJobId, db);
                }
                catch (Exception)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "error copying documents but job should be created");
                }

                return new JsonResult
                {
                    Data = JsonConvert.SerializeObject(newJobId),
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }

            return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Quote not found");
        }

        [HttpGet]
        public async Task<ActionResult> CreateJob()
        {
            ViewData["SpeedDenominator"] = await GenerateSelectListAsync(SelectListType.SpeedDenominators);
            ViewData["VM_Job.Job.ShippingMethod"] = await GenerateSelectListAsync(SelectListType.ShippingOptions);
            ViewData["State"] = await GenerateSelectListAsync(SelectListType.USStates);
            ViewData["Vendors"] = await GenerateSelectListAsync(SelectListType.Vendors);
            ViewData["MaterialCategory_Id"] = await GenerateSelectListAsync(SelectListType.MaterialCategories);
            ViewData["CostType"] = await GenerateSelectListAsync(SelectListType.CostType);
            ViewData["StockType"] = await GenerateSelectListAsync(SelectListType.StockType);
            ViewData["JobOutsourcedItem.JobPriceUnit"] = await GenerateSelectListAsync(SelectListType.CostType);
            ViewData["JobOutsourcedItem.Vendor"] = await GenerateSelectListAsync(SelectListType.Vendors);
            //for material adder - finishing formula
            ViewData["Category"] = await GenerateSelectListAsync(SelectListType.MaterialCategories);
            ViewData["FinishingMaterialFormula"] = await GenerateSelectListAsync(SelectListType.FinishingMaterialFormulas);
            var finishingCategoryIdsCreateJob = await db.MaterialCategories
                .Where(x => x.Deleted == null && x.MaterialType == (int)MaterialCategory.MaterialTypes.Finishing)
                .Select(x => x.Id)
                .ToListAsync();
            ViewData["FinishingCategoryIds"] = string.Join(",", finishingCategoryIdsCreateJob);

            HttpCookie userCookie = HttpContext.Response.Cookies.Get("ActiveUser");

            VM_JobWrapper JobVm = new VM_JobWrapper
            {
                VM_Job = new VM_Job
                {
                    Job = new Job
                    {
                        JobNumber = await GenerateJobNumber(RecordType.Job),
                        JobStatus_Id = (int)JobState.NotInProgress,
                        CreatedBy = int.Parse(userCookie.Value),
                        CreatedAt = DateTime.UtcNow,
                        BlindShipping = false,
                        QuoteNumber = "N/A",
                        IsWarehouseJob = false,
                        IsEvent = false,
                        ScheduleSquash = false,
                        Donated = false,
                        UseSqftPricing = false
                    }
                },
                ItemTotals = new JobPriceInformation
                {
                    ItemTotals = new List<JobParentItemTotals_Result>(),
                    Total = 0m,
                    TotalString = "$0.00"
                }
            };

            try
            {
                db.Jobs.Add(JobVm.VM_Job.Job);
                await db.SaveChangesAsync();
                await db.Entry(JobVm.VM_Job.Job).ReloadAsync();
                return RedirectToAction("EditJob", new { jobId = JobVm.VM_Job.Job.Id });
                //return View(JobVm);
            }
            catch (Exception e)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, e.Message);
            }
        }

        [HttpGet]
        public async Task<ActionResult> CreateQuote()
        {
            ViewData["SpeedDenominator"] = await GenerateSelectListAsync(SelectListType.SpeedDenominators);
            ViewData["VM_Quote.Quote.ShippingMethod"] = await GenerateSelectListAsync(SelectListType.ShippingOptions);
            ViewData["State"] = await GenerateSelectListAsync(SelectListType.USStates);
            ViewData["Vendors"] = await GenerateSelectListAsync(SelectListType.Vendors);
            ViewData["MaterialCategory_Id"] = await GenerateSelectListAsync(SelectListType.MaterialCategories);
            ViewData["CostType"] = await GenerateSelectListAsync(SelectListType.CostType);
            ViewData["StockType"] = await GenerateSelectListAsync(SelectListType.StockType);
            ViewData["QuoteOutsourcedItem.JobPriceUnit"] = await GenerateSelectListAsync(SelectListType.CostType);
            ViewData["QuoteOutsourcedItem.Vendor"] = await GenerateSelectListAsync(SelectListType.Vendors);
            //for material adder - finishing formula
            ViewData["Category"] = await GenerateSelectListAsync(SelectListType.MaterialCategories);
            ViewData["FinishingMaterialFormula"] = await GenerateSelectListAsync(SelectListType.FinishingMaterialFormulas);
            var finishingCategoryIdsCreateQuote = await db.MaterialCategories
                .Where(x => x.Deleted == null && x.MaterialType == (int)MaterialCategory.MaterialTypes.Finishing)
                .Select(x => x.Id)
                .ToListAsync();
            ViewData["FinishingCategoryIds"] = string.Join(",", finishingCategoryIdsCreateQuote);

            HttpCookie userCookie = HttpContext.Response.Cookies.Get("ActiveUser");

            VM_QuoteWrapper quoteVm = new VM_QuoteWrapper
            {
                VM_Quote = new VM_Quote
                {
                    Quote = new Quote
                    {
                        JobNumber = await GenerateJobNumber(RecordType.Quote),
                        CreatedBy = int.Parse(userCookie.Value),
                        CreatedAt = DateTime.UtcNow,
                        IsWarehouseJob = false,
                        IsEvent = false,
                        UseSqftPricing = false,
                        ReleaseToClientView = true
                    }
                },
                ItemTotals = new QuotePriceInformation
                {
                    ItemTotals = new List<QuoteParentItemTotals_Result>(),
                    Total = 0m,
                    TotalString = "$0.00"
                }
            };

            try
            {
                db.Quotes.Add(quoteVm.VM_Quote.Quote);
                await db.SaveChangesAsync();
                await db.Entry(quoteVm.VM_Quote.Quote).ReloadAsync();
                return RedirectToAction("EditQuote", new { quoteId = quoteVm.VM_Quote.Quote.Id });
            }
            catch (Exception e)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, e.Message);
            }
        }

        public async Task<ActionResult> EditQuote(int? quoteId)
        {
            // [SMOKE-TEST] Instrumentation for Step 1 N+1 fixes — remove once verified.
            var sw_total = System.Diagnostics.Stopwatch.StartNew();
            var sw_stage = System.Diagnostics.Stopwatch.StartNew();
            MastermindLogger.Log(LogLevel.Info, $"[EditQuote] === START quoteId={quoteId} ===");

            ViewData["FinishingFormulaApplications"] = await GenerateSelectListAsync(SelectListType.FinishingFormulaApplications);

            ViewData["SpeedDenominator"] = await GenerateSelectListAsync(SelectListType.SpeedDenominators);
            ViewData["State"] = await GenerateSelectListAsync(SelectListType.USStates);
            //for client adder
            ViewData["Client.USStateId"] = await GenerateSelectListAsync(SelectListType.USStates);
            ViewData["Vendors"] = await GenerateSelectListAsync(SelectListType.Vendors);
            //for material adder vvv
            ViewData["Category"] = await GenerateSelectListAsync(SelectListType.MaterialCategories);
            ViewData["CostType"] = await GenerateSelectListAsync(SelectListType.CostType);
            ViewData["StockType"] = await GenerateSelectListAsync(SelectListType.StockType);
            ViewData["FinishingMaterialFormula"] = await GenerateSelectListAsync(SelectListType.FinishingMaterialFormulas);
            var finishingCategoryIds = await db.MaterialCategories
                .Where(x => x.Deleted == null && x.MaterialType == (int)MaterialCategory.MaterialTypes.Finishing)
                .Select(x => x.Id)
                .ToListAsync();
            ViewData["FinishingCategoryIds"] = string.Join(",", finishingCategoryIds);

            List<int> itemIds = await db.QuoteItems.Where(x => x.Quote == quoteId.Value).Select(x => x.Id).ToListAsync();

            VM_QuoteWrapper VM_QuoteWrapper = new VM_QuoteWrapper
            {
                VM_Quote = new VM_Quote
                {
                    Quote = await db.Quotes.FindAsync(quoteId)
                },
                ItemTotals = new QuotePriceInformation
                {
                    ItemTotals = await db.QuoteParentItemTotals(quoteId).ToListAsync()
                },
                VM_Presses = new VM_Presses
                {
                    Presses = await GetPressesAsync(),
                    PressCategories = await GetPressCategoriesAsync(),
                    PressKeys = await GetPressCategoriesAsync(true)
                }
            };

            //totals
            VM_QuoteWrapper.ItemTotals.Total = VM_QuoteWrapper.ItemTotals.ItemTotals.Sum(x => (x.Total_Price ?? 0m));
            VM_QuoteWrapper.ItemTotals.TotalString = VM_QuoteWrapper.ItemTotals.Total.ToString("C");

            // SQFT pricing fields
            VM_QuoteWrapper.ItemTotals.QuoteId = quoteId.Value;
            VM_QuoteWrapper.ItemTotals.UseSqftPricing = VM_QuoteWrapper.VM_Quote.Quote.UseSqftPricing ?? false;
            VM_QuoteWrapper.ItemTotals.SqftPriceOverride = VM_QuoteWrapper.VM_Quote.Quote.SqftPriceOverride;

            // Service markup override item IDs for sidebar indicator
            var qSvcOvr = db.QuoteItemServices
                .Where(s => s.QuoteItem.Quote == quoteId.Value && s.CustomMarkup != null)
                .Select(s => s.QuoteItemId).Distinct().ToList();
            var qFmOvr = db.QuoteItemFinishingMaterials
                .Where(f => f.QuoteItemId.HasValue && f.QuoteItem.Quote == quoteId.Value && f.CustomMarkup != null)
                .Select(f => f.QuoteItemId.Value).Distinct().ToList();
            var qItmOvr = db.QuoteItems
                .Where(qi => qi.Quote == quoteId.Value && (qi.CustomMaterialMarkup != null || qi.CustomInkMarkup != null || qi.CustomPrintLaborMarkup != null))
                .Select(qi => qi.Id).ToList();
            VM_QuoteWrapper.ItemTotals.ServiceOverrideItemIds = new HashSet<int>(qSvcOvr.Concat(qFmOvr).Concat(qItmOvr));

            VM_QuoteWrapper.RelatedJobs =
                    await db.Jobs
                        .Where(x => x.QuoteNumber == VM_QuoteWrapper.VM_Quote.Quote.JobNumber)
                        .Select(x => new RelatedJob
                        {
                            JobNumber = x.JobNumber,
                            JobId = x.Id,
                            QuoteId = VM_QuoteWrapper.VM_Quote.Quote.Id,
                            QuoteNumber = VM_QuoteWrapper.VM_Quote.Quote.JobNumber
                        })
                        .ToListAsync();

            // Load QuotePrices with item + service references in one query.
            // Before: 1 Collection load + 2 Reference loads per price row = 1 + 2×P round-trips.
            // After:  single query with JOINs regardless of how many price rows exist.
            await db.Entry(VM_QuoteWrapper.VM_Quote.Quote)
                .Collection(c => c.QuotePrices)
                .Query()
                .Include(qp => qp.QuoteItem1)
                .Include(qp => qp.QuoteItemService1)
                .LoadAsync();

            if (VM_QuoteWrapper.VM_Quote.Quote == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, ("cannot find quote with id = " + quoteId.Value));

            VM_QuoteWrapper.Contacts = await db.Contacts.Where(x => x.CusNo == VM_QuoteWrapper.VM_Quote.Quote.Client).ToListAsync();

            VM_QuoteWrapper.VM_Quote.Quote.MarkupOverrideAsPercent = (VM_QuoteWrapper.VM_Quote.Quote.MarkupOverride ?? 0m) * 100m;
            //VM_QuoteWrapper.VM_Quote.Quote.MarkupOverrideAsInteger = (int)((VM_QuoteWrapper.VM_Quote.Quote.MarkupOverride ?? 0m) * 100m);
            VM_QuoteWrapper.VM_Quote.Quote.MarkupOverrideAsFormattedPercent = Convert.ToInt32(VM_QuoteWrapper.VM_Quote.Quote.MarkupOverrideAsPercent).ToString() + "%";
            VM_QuoteWrapper.VM_Quote.Quote.OverallMarkupPercentAsFormattedPercent = VM_QuoteWrapper.VM_Quote.Quote.OverallMarkupPercentAsPercent.ToString() + "%";

            await db.Entry(VM_QuoteWrapper.VM_Quote.Quote).Reference(r => r.Client1).LoadAsync();
            await db.Entry(VM_QuoteWrapper.VM_Quote.Quote).Reference(r => r.Contact1).LoadAsync();
            // Eagerly load all QuoteItem navigation properties in one query.
            // Before: plain collection load, then per-item calls for Press, Material, Services,
            //         FinishingMaterials and their nested references (N×many round-trips).
            // After:  one SQL statement with JOINs — loop below has zero DB calls for these.
            await db.Entry(VM_QuoteWrapper.VM_Quote.Quote)
                .Collection(c => c.QuoteItems)
                .Query()
                .Include(qi => qi.Press1.SpeedDenominator1)
                .Include(qi => qi.Material1.MaterialStockType)
                .Include(qi => qi.Material1.MaterialCostType)
                .Include(qi => qi.QuoteItemServices.Select(qis => qis.Service.ServiceCategory.ServiceType1))
                .Include(qi => qi.QuoteItemFinishingMaterials.Select(qifm => qifm.Material1))
                .Include(qi => qi.QuoteItemFinishingMaterials.Select(qifm => qifm.FinishingMaterialApplication))
                .LoadAsync();
            await db.Entry(VM_QuoteWrapper.VM_Quote.Quote).Collection(c => c.QuoteOutsourcedItems).LoadAsync();
            await db.Entry(VM_QuoteWrapper.VM_Quote.Quote).Reference(r => r.User).LoadAsync();

            List<int> iids = VM_QuoteWrapper.VM_Quote.Quote.QuoteItems.Select(x => x.Id).ToList();

            // Pre-fetch all ItemLayoutSettings for this quote in one query.
            // Before: SingleAsync() inside the loop = N separate round-trips.
            // After:  one query into a dictionary, looked up by QuoteItemId in the loop.
            var layoutSettingsDict = iids.Count > 0
                ? await db.ItemLayoutSettings
                    .Where(ils => ils.QuoteItemId.HasValue && iids.Contains(ils.QuoteItemId.Value))
                    .ToDictionaryAsync(ils => ils.QuoteItemId.Value)
                : new Dictionary<int, ItemLayoutSetting>();

            // Pre-fetch all standard document counts grouped by item in one query.
            // Before: CountAsync() inside the loop = N separate round-trips (paperclip badge).
            // After:  one grouped query into a dictionary, looked up by ItemId in the loop.
            var docCountsDict = iids.Count > 0
                ? await db.QuoteDocuments
                    .Where(x => x.ItemId.HasValue && iids.Contains(x.ItemId.Value) && x.RowType == "Standard")
                    .GroupBy(x => x.ItemId.Value)
                    .Select(g => new { ItemId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.ItemId, x => x.Count)
                : new Dictionary<int, int>();

            // Same idea for outsourced items — pre-fetch their doc counts in one grouped query.
            List<int> oids = VM_QuoteWrapper.VM_Quote.Quote.QuoteOutsourcedItems.Select(x => x.Id).ToList();
            var outsourcedDocCountsDict = oids.Count > 0
                ? await db.QuoteDocuments
                    .Where(x => x.ItemId.HasValue && oids.Contains(x.ItemId.Value) && x.RowType == "Outsourced")
                    .GroupBy(x => x.ItemId.Value)
                    .Select(g => new { ItemId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.ItemId, x => x.Count)
                : new Dictionary<int, int>();

            // [SMOKE-TEST] Verify pre-fetch produced what we expect before running the foreach.
            int svcCount = VM_QuoteWrapper.VM_Quote.Quote.QuoteItems.Sum(x => x.QuoteItemServices.Count);
            int finCount = VM_QuoteWrapper.VM_Quote.Quote.QuoteItems.Sum(x => x.QuoteItemFinishingMaterials.Count);
            MastermindLogger.Log(LogLevel.Info,
                $"[EditQuote] LoadPhase {sw_stage.ElapsedMilliseconds}ms | quoteId={quoteId} | items={iids.Count} outsourced={oids.Count} services={svcCount} finishing={finCount} | dicts: layoutSettings={layoutSettingsDict.Count} stdDocCounts={docCountsDict.Count} outsourcedDocCounts={outsourcedDocCountsDict.Count}");
            sw_stage.Restart();

            // [SMOKE-TEST] Counters for null-nav diagnostics inside the loop.
            int nullPressNav = 0, nullMaterialNav = 0, nullServiceNav = 0, nullFinishMatNav = 0;

            //load foreign key entities
            foreach (QuoteItem qi in VM_QuoteWrapper.VM_Quote.Quote.QuoteItems)
            {
                string unqKey = "hasDutchCut_" + qi.Id.ToString();
                ViewData[unqKey] = false;

                // Dictionary lookup — no DB call (pre-fetched above).
                // Before: SingleAsync per item (threw + was silently caught if duplicate existed).
                if (layoutSettingsDict.TryGetValue(qi.Id, out var ils))
                    ViewData[unqKey] = ils.DutchCut;

                // [SMOKE-TEST] Detect orphaned FKs that the Include chain couldn't resolve.
                if (qi.Press.HasValue && qi.Press1 == null)
                {
                    nullPressNav++;
                    MastermindLogger.Log(LogLevel.Warning, $"[EditQuote] QuoteItem {qi.Id} has Press={qi.Press} but Press1 nav is null (orphan FK)");
                }
                if (qi.Material.HasValue && qi.Material1 == null)
                {
                    nullMaterialNav++;
                    MastermindLogger.Log(LogLevel.Warning, $"[EditQuote] QuoteItem {qi.Id} has Material={qi.Material} but Material1 nav is null (orphan FK)");
                }
                foreach (var qisChk in qi.QuoteItemServices)
                {
                    if (qisChk.Service == null)
                    {
                        nullServiceNav++;
                        MastermindLogger.Log(LogLevel.Warning, $"[EditQuote] QuoteItemService {qisChk.Id} (item {qi.Id}) has null Service nav (OriginalService={qisChk.OriginalService}) — will crash UpdateQuote line ~8028");
                    }
                }
                foreach (var qifmChk in qi.QuoteItemFinishingMaterials)
                {
                    if (qifmChk.Material.HasValue && qifmChk.Material1 == null)
                    {
                        nullFinishMatNav++;
                        MastermindLogger.Log(LogLevel.Warning, $"[EditQuote] QuoteItemFinishingMaterial {qifmChk.Id} (item {qi.Id}) has Material={qifmChk.Material} but Material1 nav is null");
                    }
                }

                if (!qi.MaterialCost.HasValue)
                    qi.MaterialCost = 0m;
                if (!qi.Sheets.HasValue)
                    qi.Sheets = 1;

                // All navigation properties (Services, Press, Material, FinishingMaterials and
                // their nested references) are already in memory from the Include chain above.
                // No DB calls needed here — just in-memory formatting and config parsing.

                foreach (QuoteItemFinishingMaterial qifm in qi.QuoteItemFinishingMaterials)
                {
                    Dictionary<string, string> d = JsonConvert.DeserializeObject<Dictionary<string, string>>((qifm.FinishingFormulaConfiguration ?? "{\"\":\"\"}"));
                    qifm.ConfigLabel = d.First().Key.Split('_')[0];
                    qifm.ConfigValue = d.First().Value;
                }

                if (qi.Material.HasValue)
                {
                    qi.MaterialCostPerUnitFormatted = qi.MaterialCostPerUnit.HasValue ? qi.MaterialCostPerUnit.Value.ToString("C") : "0.00"; //.Value because should always have a value
                    qi.MaterialCostFormatted = (qi.MaterialCost ?? 0m).ToString("C");
                }
                else
                {
                    qi.MaterialCostPerUnit = 0m;
                    qi.MaterialCostPerUnitFormatted = qi.MaterialCostPerUnit.Value.ToString("C");
                    qi.MaterialCostFormatted = (qi.MaterialCost ?? 0m).ToString("C");
                }

                foreach (QuoteItemService qis in qi.QuoteItemServices)
                {
                    qis.FormattedServiceTime = DecimalHoursToFormatted(qis.ServiceTime);
                }

                if (qi.Material.HasValue)
                {
                    qi.Material1.ParseDimensions();
                }
                qi.ParseRunSize();

                qi.FormattedPrintTime = DecimalHoursToFormatted(qi.PrintTime);

                // Dictionary lookup — no DB call (pre-fetched above).
                qi.DocumentCount = docCountsDict.TryGetValue(qi.Id, out var docCount) ? docCount : 0;

                //null coalesce any null'd checkboxes
                qi.DoubleHit = qi.DoubleHit ?? false;
                qi.Staple = qi.Staple ?? false;
                qi.Collate = qi.Collate ?? false;
            }

            // [SMOKE-TEST] Foreach over QuoteItems is in-memory only after Step 1 changes.
            MastermindLogger.Log(LogLevel.Info, $"[EditQuote] T1 foreach(QuoteItems) {sw_stage.ElapsedMilliseconds}ms"); sw_stage.Restart();

            foreach (QuoteOutsourcedItem qoi in VM_QuoteWrapper.VM_Quote.Quote.QuoteOutsourcedItems.ToList())
            {
                // Dictionary lookup — no DB call (pre-fetched above).
                qoi.DocumentCount = outsourcedDocCountsDict.TryGetValue(qoi.Id, out var oCount) ? oCount : 0;
            }

            // Reuse the result already fetched into VM_QuoteWrapper.ItemTotals.ItemTotals (line ~3992).
            VM_QuoteWrapper.VM_Quote.Quote.ItemTotals = VM_QuoteWrapper.ItemTotals.ItemTotals;

            VM_QuoteWrapper.GenericItemOrganizer = new List<GenericItemOrganizer>();
            VM_QuoteWrapper.GenericItemOrganizer = VM_QuoteWrapper.VM_Quote.Quote.QuoteItems.Select(x => new GenericItemOrganizer
            {
                Id = x.Id,
                Ordinal = x.Ordinal ?? -1,
                RowType = "Standard"
            }).ToList();
            VM_QuoteWrapper.GenericItemOrganizer.AddRange(VM_QuoteWrapper.VM_Quote.Quote.QuoteOutsourcedItems.Select(x => new GenericItemOrganizer
            {
                Id = x.Id,
                Ordinal = x.Ordinal ?? -1,
                RowType = "Outsourced"
            }).ToList());

            VM_QuoteWrapper.GenericItemOrganizer.OrderBy(x => x.Ordinal);

            // [SMOKE-TEST]
            MastermindLogger.Log(LogLevel.Info, $"[EditQuote] T2 outsourcedForeach+Organizer {sw_stage.ElapsedMilliseconds}ms"); sw_stage.Restart();

            ViewData["VM_Quote.Quote.ShippingMethod"] = await GenerateSelectListAsync(SelectListType.ShippingOptions, VM_QuoteWrapper.VM_Quote.Quote, "ShippingMethod");
            ViewData["QuoteOutsourcedItem.Vendor"] = await GenerateSelectListAsync(SelectListType.Vendors);
            ViewData["QuoteOutsourcedItem.JobPriceUnit"] = await GenerateSelectListAsync(SelectListType.CostType);

            // [SMOKE-TEST]
            MastermindLogger.Log(LogLevel.Info, $"[EditQuote] T3 ViewData(Shipping/Vendor/CostType) {sw_stage.ElapsedMilliseconds}ms"); sw_stage.Restart();

            //VM_QuoteWrapper.AvailableServices = await GetServicesForView();
            VM_QuoteWrapper.AvailablePrepressServices = await GetPrepressServicesForView();
            MastermindLogger.Log(LogLevel.Info, $"[EditQuote] T4 GetPrepressServicesForView {sw_stage.ElapsedMilliseconds}ms"); sw_stage.Restart();

            VM_QuoteWrapper.AvailableCuttingServices = await GetCuttingServicesForView();
            MastermindLogger.Log(LogLevel.Info, $"[EditQuote] T5 GetCuttingServicesForView {sw_stage.ElapsedMilliseconds}ms"); sw_stage.Restart();

            VM_QuoteWrapper.AvailableFinishingServices = await GetFinishingServicesForView();
            MastermindLogger.Log(LogLevel.Info, $"[EditQuote] T6 GetFinishingServicesForView {sw_stage.ElapsedMilliseconds}ms"); sw_stage.Restart();

            VM_QuoteWrapper.AvailableFinishingMaterials = await GetFinishingMaterialsForView();
            MastermindLogger.Log(LogLevel.Info, $"[EditQuote] T7 GetFinishingMaterialsForView {sw_stage.ElapsedMilliseconds}ms"); sw_stage.Restart();

            await db.Entry(VM_QuoteWrapper.VM_Quote.Quote).Reference(r => r.Job).LoadAsync();
            MastermindLogger.Log(LogLevel.Info, $"[EditQuote] T8 Reference(Job).Load {sw_stage.ElapsedMilliseconds}ms"); sw_stage.Restart();

            VM_QuoteWrapper.VM_Quote.Quote.FormattedShippingAddress = await VM_QuoteWrapper.VM_Quote.Quote.GetFormattedShippingAddress(db);
            MastermindLogger.Log(LogLevel.Info, $"[EditQuote] T9 GetFormattedShippingAddress {sw_stage.ElapsedMilliseconds}ms"); sw_stage.Restart();

            VM_QuoteWrapper.VM_Quote.Quote.FormattedFreightShippingAddress = await VM_QuoteWrapper.VM_Quote.Quote.GetFormattedFreightAddress(db);
            MastermindLogger.Log(LogLevel.Info, $"[EditQuote] T10 GetFormattedFreightAddress {sw_stage.ElapsedMilliseconds}ms"); sw_stage.Restart();

            VM_QuoteWrapper.PriceListViewModel = GetPriceListViewModelWrapper(VM_QuoteWrapper.VM_Quote.Quote);
            MastermindLogger.Log(LogLevel.Info, $"[EditQuote] T11 GetPriceListViewModelWrapper {sw_stage.ElapsedMilliseconds}ms"); sw_stage.Restart();

            // [SMOKE-TEST] Final summary — total time, in-loop time, orphan counts.
            MastermindLogger.Log(LogLevel.Info,
                $"[EditQuote] === END quoteId={quoteId} totalElapsed={sw_total.ElapsedMilliseconds}ms | orphans: nullPress={nullPressNav} nullMaterial={nullMaterialNav} nullService={nullServiceNav} nullFinishMat={nullFinishMatNav} ===");

            return View("~/Views/Job/EditQuote.cshtml", VM_QuoteWrapper);
        }

        public async Task<ActionResult> EditJob(int? jobId)
        {
            ViewData["FinishingFormulaApplications"] = await GenerateSelectListAsync(SelectListType.FinishingFormulaApplications);

            ViewData["SpeedDenominator"] = await GenerateSelectListAsync(SelectListType.SpeedDenominators);
            ViewData["State"] = await GenerateSelectListAsync(SelectListType.USStates);
            //for client adder
            ViewData["Client.USStateId"] = await GenerateSelectListAsync(SelectListType.USStates);
            ViewData["Vendors"] = await GenerateSelectListAsync(SelectListType.Vendors);
            //for material adder vvv
            ViewData["Category"] = await GenerateSelectListAsync(SelectListType.MaterialCategories);
            ViewData["CostType"] = await GenerateSelectListAsync(SelectListType.CostType);
            ViewData["StockType"] = await GenerateSelectListAsync(SelectListType.StockType);
            ViewData["FinishingMaterialFormula"] = await GenerateSelectListAsync(SelectListType.FinishingMaterialFormulas);
            var finishingCategoryIdsJob = await db.MaterialCategories
                .Where(x => x.Deleted == null && x.MaterialType == (int)MaterialCategory.MaterialTypes.Finishing)
                .Select(x => x.Id)
                .ToListAsync();
            ViewData["FinishingCategoryIds"] = string.Join(",", finishingCategoryIdsJob);

            List<int> itemIds = await db.JobItems.Where(X => X.Job == jobId.Value).Select(x => x.Id).ToListAsync();

            VM_JobWrapper VM_JobWrapper = new VM_JobWrapper
            {
                VM_Job = new VM_Job
                {
                    Job = await db.Jobs.FindAsync(jobId)
                },
                ItemTotals = new JobPriceInformation
                {
                    ItemTotals = await db.JobParentItemTotals(jobId).ToListAsync()
                },
                VM_Presses = new VM_Presses
                {
                    Presses = await GetPressesAsync(),
                    PressCategories = await GetPressCategoriesAsync(),
                    PressKeys = await GetPressCategoriesAsync(true)
                }
            };

            //total information
            VM_JobWrapper.ItemTotals.Total = VM_JobWrapper.ItemTotals.ItemTotals.Sum(x => (x.Total_Price ?? 0m));
            VM_JobWrapper.ItemTotals.TotalString = VM_JobWrapper.ItemTotals.Total.ToString("C");

            // SQFT pricing fields
            VM_JobWrapper.ItemTotals.JobId = jobId.Value;
            VM_JobWrapper.ItemTotals.UseSqftPricing = VM_JobWrapper.VM_Job.Job.UseSqftPricing ?? false;
            VM_JobWrapper.ItemTotals.SqftPriceOverride = VM_JobWrapper.VM_Job.Job.SqftPriceOverride;

            // Service markup override item IDs for sidebar indicator
            var jSvcOvr = db.JobItemServices
                .Where(s => s.JobItem.Job == jobId.Value && s.CustomMarkup != null)
                .Select(s => s.JobItemId).Distinct().ToList();
            var jFmOvr = db.JobItemFinishingMaterials
                .Where(f => f.JobItem.Job == jobId.Value && f.CustomMarkup != null)
                .Select(f => f.JobItemId).Distinct().ToList();
            var jItmOvr = db.JobItems
                .Where(ji => ji.Job == jobId.Value && (ji.CustomMaterialMarkup != null || ji.CustomInkMarkup != null || ji.CustomPrintLaborMarkup != null))
                .Select(ji => ji.Id).ToList();
            VM_JobWrapper.ItemTotals.ServiceOverrideItemIds = new HashSet<int>(jSvcOvr.Concat(jFmOvr).Concat(jItmOvr));

            //load new job prices
            await db.Entry(VM_JobWrapper.VM_Job.Job).Collection(c => c.JobPrices).LoadAsync();

            foreach (JobPrice jp in VM_JobWrapper.VM_Job.Job.JobPrices)
            {
                await db.Entry(jp).Reference(r => r.JobItem1).LoadAsync();
                await db.Entry(jp).Reference(r => r.JobItemService1).LoadAsync();
            }

            if (VM_JobWrapper.VM_Job.Job == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, ("cannot find job with id = " + jobId.Value));

            VM_JobWrapper.Contacts = await db.Contacts.Where(x => x.CusNo == VM_JobWrapper.VM_Job.Job.Client).ToListAsync();

            VM_JobWrapper.VM_Job.Job.MarkupOverrideAsPercent = (VM_JobWrapper.VM_Job.Job.MarkupOverride ?? 0m) * 100m;
            //VM_JobWrapper.VM_Job.Job.MarkupOverrideAsInteger = (int)((VM_JobWrapper.VM_Job.Job.MarkupOverride ?? 0m) * 100m);
            VM_JobWrapper.VM_Job.Job.MarkupOverrideAsFormattedPercent = Convert.ToInt32(VM_JobWrapper.VM_Job.Job.MarkupOverrideAsPercent).ToString() + "%";
            VM_JobWrapper.VM_Job.Job.OverallMarkupPercentAsFormattedPercent = VM_JobWrapper.VM_Job.Job.OverallMarkupPercentAsPercent.ToString() + "%";
            try
            {
                await db.Entry(VM_JobWrapper.VM_Job.Job).Reference(r => r.Client1).LoadAsync();
                await db.Entry(VM_JobWrapper.VM_Job.Job).Reference(r => r.Contact1).LoadAsync();
                await db.Entry(VM_JobWrapper.VM_Job.Job).Collection(c => c.JobItems).LoadAsync();
                await db.Entry(VM_JobWrapper.VM_Job.Job).Collection(c => c.JobOutsourcedItems).LoadAsync();
                await db.Entry(VM_JobWrapper.VM_Job.Job).Reference(r => r.User).LoadAsync();
            }
            catch (Exception e)
            {
            }
            VM_JobWrapper.VM_Job.Job.MinStartDate = VM_JobWrapper.VM_Job.Job.JobItems.Min(x => x.MinStartDate);

            List<int> iids = VM_JobWrapper.VM_Job.Job.JobItems.Select(x => x.Id).ToList();

            //load foreign key entities
            foreach (JobItem ji in VM_JobWrapper.VM_Job.Job.JobItems)
            {
                string unqKey = "hasDutchCut_" + ji.Id.ToString();
                ViewData[unqKey] = false;

                try
                {
                    ItemLayoutSetting ils = await db.ItemLayoutSettings.SingleAsync(x => x.JobItemId == ji.Id);
                    ViewData[unqKey] = ils.DutchCut;
                }
                catch (Exception e)
                {
                    //no record found; move on
                }

                if (!ji.MaterialCost.HasValue)
                    ji.MaterialCost = 0m;
                if (!ji.Sheets.HasValue)
                    ji.Sheets = 1;

                await db.Entry(ji).Collection(c => c.JobItemServices).LoadAsync();
                await db.Entry(ji).Reference(r => r.Press1).LoadAsync();
                await db.Entry(ji).Reference(r => r.Material1).LoadAsync();
                await db.Entry(ji).Collection(c => c.JobItemFinishingMaterials).LoadAsync();

                foreach (JobItemFinishingMaterial jifm in ji.JobItemFinishingMaterials)
                {
                    await db.Entry(jifm).Reference(r => r.Material1).LoadAsync();
                    await db.Entry(jifm).Reference(r => r.FinishingMaterialApplication).LoadAsync();

                    Dictionary<string, string> d = JsonConvert.DeserializeObject<Dictionary<string, string>>((jifm.FinishingFormulaConfiguration ?? "{\"\":\"\"}"));
                    jifm.ConfigLabel = d.First().Key.Split('_')[0];
                    jifm.ConfigValue = d.First().Value;
                }

                if (ji.Material.HasValue)
                {
                    await db.Entry(ji.Material1).Reference(r => r.MaterialStockType).LoadAsync();
                    await db.Entry(ji.Material1).Reference(r => r.MaterialCostType).LoadAsync();

                    ji.MaterialCostPerUnitFormatted = ji.MaterialCostPerUnit.HasValue ? ji.MaterialCostPerUnit.Value.ToString("C") : "0.00";
                    ji.MaterialCostFormatted = (ji.MaterialCost ?? 0m).ToString("C");
                }
                else
                {
                    ji.MaterialCostPerUnit = 0m;
                    ji.MaterialCostPerUnitFormatted = ji.MaterialCostPerUnit.Value.ToString("C");
                    ji.MaterialCostFormatted = (ji.MaterialCost ?? 0m).ToString("C");
                }

                if (ji.Press.HasValue)
                    await db.Entry(ji.Press1).Reference(r => r.SpeedDenominator1).LoadAsync();

                foreach (JobItemService jis in ji.JobItemServices)
                {
                    await db.Entry(jis).Reference(r => r.Service).LoadAsync();
                    await db.Entry(jis.Service).Reference(r => r.ServiceCategory).LoadAsync();
                    await db.Entry(jis.Service.ServiceCategory).Reference(r => r.ServiceType1).LoadAsync();
                    jis.FormattedServiceTime = DecimalHoursToFormatted(jis.ServiceTime);
                }

                if (ji.Material.HasValue)
                {
                    ji.Material1.ParseDimensions();
                }

                ji.ParseRunSize();

                ji.FormattedPrintTime = DecimalHoursToFormatted(ji.PrintTime);

                ji.DocumentCount = await db.JobDocuments.Where(x => x.ItemId == ji.Id && x.RowType == "Standard").CountAsync();
                var latestProof = await db.JobProofs
                    .Where(x => x.JobItemId == ji.Id && x.Deleted == null)
                    .OrderByDescending(x => x.Version)
                    .FirstOrDefaultAsync();
                ji.LatestProofStatus = latestProof?.ApprovalStatus;

                //null coalesce any null'd checkboxes to false
                ji.DoubleHit = ji.DoubleHit ?? false;
                ji.Staple = ji.Staple ?? false;
                ji.Collate = ji.Collate ?? false;
            }

            foreach (JobOutsourcedItem joi in VM_JobWrapper.VM_Job.Job.JobOutsourcedItems.ToList())
            {
                joi.DocumentCount = await db.JobDocuments.Where(x => x.ItemId == joi.Id && x.RowType == "Outsourced").CountAsync();
            }

            VM_JobWrapper.VM_Job.Job.ItemTotals = await db.JobParentItemTotals(jobId).ToListAsync();

            ViewData["VM_Job.Job.ShippingMethod"] = await GenerateSelectListAsync(SelectListType.ShippingOptions, VM_JobWrapper.VM_Job.Job, "ShippingMethod");
            ViewData["JobOutsourcedItem.Vendor"] = await GenerateSelectListAsync(SelectListType.Vendors);
            ViewData["JobOutsourcedItem.JobPriceUnit"] = await GenerateSelectListAsync(SelectListType.CostType);

            //VM_JobWrapper.AvailableServices = await GetServicesForView();

            VM_JobWrapper.AvailablePrepressServices = await GetPrepressServicesForView();
            VM_JobWrapper.AvailableCuttingServices = await GetCuttingServicesForView();
            VM_JobWrapper.AvailableFinishingServices = await GetFinishingServicesForView();
            VM_JobWrapper.AvailableFinishingMaterials = await GetFinishingMaterialsForView();

            VM_JobWrapper.VM_Job.Job.FormattedShippingAddress = await VM_JobWrapper.VM_Job.Job.GetFormattedShippingAddress(db);
            VM_JobWrapper.VM_Job.Job.FormattedFreightShippingAddress = await VM_JobWrapper.VM_Job.Job.GetFormattedFreightAddress(db);

            // Load blind shipping address if it exists
            if (VM_JobWrapper.VM_Job.Job.BlindShippingAddress.HasValue)
            {
                await db.Entry(VM_JobWrapper.VM_Job.Job).Reference(r => r.BlindShippingPivotTable).LoadAsync();
            }

            VM_JobWrapper.PriceListViewModel = GetPriceListViewModelWrapper(VM_JobWrapper.VM_Job.Job);

            // Load ReleaseToClientView from related Quote (QuoteNumber on Job = JobNumber on Quote)
            if (!string.IsNullOrEmpty(VM_JobWrapper.VM_Job.Job.QuoteNumber))
            {
                var relatedQuote = await db.Quotes.FirstOrDefaultAsync(q => q.JobNumber == VM_JobWrapper.VM_Job.Job.QuoteNumber);
                if (relatedQuote != null)
                {
                    VM_JobWrapper.ReleaseToClientView = relatedQuote.ReleaseToClientView ?? false;
                    VM_JobWrapper.RelatedQuoteId = relatedQuote.Id;
                }
            }

            return View("~/Views/Job/EditJob.cshtml", VM_JobWrapper);
        }

        [HttpPost]
        public ActionResult DeleteQuote(int? quoteId)
        {
            if (!quoteId.HasValue)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.NotFound);

            db.DeleteQuote(quoteId);

            return new HttpStatusCodeResult(HttpStatusCode.OK);
        }

        [HttpPost]
        public async Task<ActionResult> DeleteJob(int? quoteId)
        {
            if (!quoteId.HasValue)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.NotFound);

            //using (ConnectToDatabase())
            {
                Quote existing = db.Quotes.Find(quoteId);

                if (existing == null)
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.NotFound);

                db.Quotes.Remove(existing);

                await db.SaveChangesAsync();

                return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK);
            }
        }

        public async Task<ActionResult> RemoveItemRow(string itemType, int? itemId, bool? isOutsource)
        {
            bool finalIsOutsource = isOutsource ?? false;

            if (String.IsNullOrEmpty(itemType) || !itemId.HasValue)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, "Invalid parameter");

            switch (itemType.ToUpper())
            {
                case "QUOTE":
                    if (finalIsOutsource)
                    {
                        //removing outsourced quote item
                        QuoteOutsourcedItem qoi = await db.QuoteOutsourcedItems.FindAsync(itemId);
                        //no quote pricing for these items
                        if (qoi == null)
                            return new HttpStatusCodeResult(HttpStatusCode.NotFound, "Cannot find that quote item");

                        db.QuoteOutsourcedItems.Remove(qoi);

                        try
                        {
                            QuotePrice outsourcePrice = await db.QuotePrices.SingleAsync(x => x.QuoteItem == itemId && (x.OutsourcedItem ?? false));
                            db.QuotePrices.Remove(outsourcePrice);
                        }
                        catch (Exception)
                        {
                            //no price found, which is okay
                        }
                    }
                    else
                    {
                        QuoteItem ji = await db.QuoteItems.FindAsync(itemId);

                        await db.Entry(ji).Collection(c => c.QuoteItemFinishingMaterials).LoadAsync();

                        foreach (QuoteItemFinishingMaterial qifm in ji.QuoteItemFinishingMaterials.ToList())
                            db.QuoteItemFinishingMaterials.Remove(qifm);

                        if (ji == null)
                            return new HttpStatusCodeResult(HttpStatusCode.NotFound);

                        await db.Entry(ji).Collection(c => c.QuoteItemServices).LoadAsync();

                        foreach (QuoteItemService jis in ji.QuoteItemServices.ToList())
                            db.QuoteItemServices.Remove(jis);

                        await db.Entry(ji).Collection(c => c.QuotePrices).LoadAsync();

                        foreach (QuotePrice qp in ji.QuotePrices.ToList())
                            db.QuotePrices.Remove(qp);

                        var ilsq = await db.ItemLayoutSettings.FirstOrDefaultAsync(x => x.QuoteItemId == ji.Id);
                        if (ilsq != null)
                            db.ItemLayoutSettings.Remove(ilsq);

                        db.QuoteItems.Remove(ji);
                    }
                    break;

                case "JOB":
                    if (finalIsOutsource)
                    {
                        JobOutsourcedItem joi = await db.JobOutsourcedItems.FindAsync(itemId);
                        //no job pricing for these items
                        if (joi == null)
                            return new HttpStatusCodeResult(HttpStatusCode.NotFound, "Cannot find that job item");

                        db.JobOutsourcedItems.Remove(joi);

                        try
                        {
                            JobPrice outsourcePrice = await db.JobPrices.SingleAsync(x => x.JobItem == itemId && (x.OutsourcedItem ?? false));
                            db.JobPrices.Remove(outsourcePrice);
                        }
                        catch (Exception)
                        {
                            //no price found, which is okay
                        }
                    }
                    else
                    {
                        JobItem jobItem = await db.JobItems.FindAsync(itemId);

                        await db.Entry(jobItem).Collection(c => c.JobItemFinishingMaterials).LoadAsync();

                        //delete finishing materials
                        foreach (JobItemFinishingMaterial jifm in jobItem.JobItemFinishingMaterials.ToList())
                            db.JobItemFinishingMaterials.Remove(jifm);

                        if (jobItem == null)
                            return new HttpStatusCodeResult(HttpStatusCode.NotFound);

                        await db.Entry(jobItem).Collection(c => c.JobItemServices).LoadAsync();

                        foreach (JobItemService jis in jobItem.JobItemServices.ToList())
                            db.JobItemServices.Remove(jis);

                        await db.Entry(jobItem).Collection(c => c.JobPrices).LoadAsync();

                        foreach (JobPrice jp in jobItem.JobPrices.ToList())
                            db.JobPrices.Remove(jp);

                        var ilsj = await db.ItemLayoutSettings.FirstOrDefaultAsync(x => x.JobItemId == jobItem.Id);

                        if (ilsj != null)
                            db.ItemLayoutSettings.Remove(ilsj);

                        db.JobItems.Remove(jobItem);
                    }
                    break;

                default:
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, "Invalid item type");
            }

            await db.SaveChangesAsync();

            return new HttpStatusCodeResult(HttpStatusCode.OK);
        }

        public async Task<ActionResult> NewOutsourcedItemRow(string itemType, int? masterRecordId)
        {
            if (String.IsNullOrEmpty(itemType) || !masterRecordId.HasValue)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "item type and master record id are required");

            int currentRowId = 1;
            int newOrdinal = 1;

            //using (ConnectToDatabase())
            {
                switch (itemType.ToUpper())
                {
                    case "QUOTE":
                        try { currentRowId = await db.QuoteItems.MaxAsync(x => x.Id) + 1; } catch (Exception) { }
                        Quote q = await db.Quotes.FindAsync(masterRecordId);
                        await db.Entry(q).Collection(c => c.QuoteItems).LoadAsync();
                        await db.Entry(q).Collection(c => c.QuoteOutsourcedItems).LoadAsync();

                        newOrdinal = q.QuoteItems.Count + q.QuoteOutsourcedItems.Count + 1;

                        ViewData["QuoteOutsourcedItem.Vendor"] = await GenerateSelectListAsync(SelectListType.Vendors);
                        ViewData["QuoteOutsourcedItem.JobPriceUnit"] = await GenerateSelectListAsync(SelectListType.CostType);

                        QuoteOutsourcedItem newOutsourcedQuote = new QuoteOutsourcedItem
                        {
                            Id = currentRowId,
                            Ordinal = newOrdinal,
                            Quote = masterRecordId.Value
                        };

                        db.QuoteOutsourcedItems.Add(newOutsourcedQuote);

                        await db.SaveChangesAsync();

                        await db.Entry(newOutsourcedQuote).ReloadAsync();

                        return new JsonResult { Data = JsonConvert.SerializeObject(RenderViewToString("~/Views/Partials/_QuoteOutsourcedItemRow.cshtml", new VM_QuoteOutsourcedItem { QuoteOutsourcedItem = newOutsourcedQuote })), JsonRequestBehavior = JsonRequestBehavior.AllowGet };

                    case "JOB":
                        try { currentRowId = await db.JobItems.MaxAsync(x => x.Id) + 1; } catch (Exception) { }
                        Job j = await db.Jobs.FindAsync(masterRecordId);
                        await db.Entry(j).Collection(c => c.JobItems).LoadAsync();
                        await db.Entry(j).Collection(c => c.JobOutsourcedItems).LoadAsync();

                        newOrdinal = j.JobItems.Count + j.JobOutsourcedItems.Count + 1;

                        ViewData["JobOutsourcedItem.Vendor"] = await GenerateSelectListAsync(SelectListType.Vendors);
                        ViewData["JobOutsourcedItem.JobPriceUnit"] = await GenerateSelectListAsync(SelectListType.CostType);

                        JobOutsourcedItem newOutsourcedJob = new JobOutsourcedItem
                        {
                            Id = currentRowId,
                            Ordinal = newOrdinal,
                            Job = masterRecordId.Value
                        };

                        db.JobOutsourcedItems.Add(newOutsourcedJob);

                        await db.SaveChangesAsync();

                        await db.Entry(newOutsourcedJob).ReloadAsync();

                        return new JsonResult { Data = JsonConvert.SerializeObject(RenderViewToString("~/Views/Partials/_JobOutsourcedItemRow.cshtml", new VM_JobOutsourcedItem { JobOutsourcedItem = newOutsourcedJob })), JsonRequestBehavior = JsonRequestBehavior.AllowGet };

                    default:
                        return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Invalid item type");
                }
            }
        }

        public async Task<ActionResult> NewItemRow(string itemType, int? masterRecordId)
        {
            if (String.IsNullOrEmpty(itemType))
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, "Invalid Parameter");

            if (!masterRecordId.HasValue)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, "Invalid Master Id");

            int newRecordOrdinal = -1;

            switch (itemType.ToUpper())
            {
                case "QUOTE":
                    Quote q = await db.Quotes.FindAsync(masterRecordId);
                    await db.Entry(q).Collection(c => c.QuoteItems).LoadAsync();
                    await db.Entry(q).Collection(c => c.QuoteOutsourcedItems).LoadAsync();
                    newRecordOrdinal = q.QuoteItems.Count + q.QuoteOutsourcedItems.Count + 1;

                    if (q == null)
                        return new HttpStatusCodeResult(System.Net.HttpStatusCode.NotFound, $"No record found with {masterRecordId.Value}");
                    break;

                case "JOB":
                    Job j = await db.Jobs.FindAsync(masterRecordId);
                    await db.Entry(j).Collection(c => c.JobItems).LoadAsync();
                    await db.Entry(j).Collection(c => c.JobOutsourcedItems).LoadAsync();
                    newRecordOrdinal = j.JobItems.Count + j.JobOutsourcedItems.Count + 1;

                    if (j == null)
                        return new HttpStatusCodeResult(System.Net.HttpStatusCode.NotFound, $"No record found with {masterRecordId.Value}");
                    break;

                default:
                    return new HttpStatusCodeResult(HttpStatusCode.NotFound, "No item with that type exists");
            }

            switch (itemType.ToUpper())
            {
                case "QUOTE":
                    ViewData["QuoteItem.PressSpeedDenominator"] = await GenerateSelectListAsync(SelectListType.SpeedDenominators);
                    //create quote item; save for new id; create service record; save to save reocrd; load references and related collections before sending to view
                    QuoteItem qi =
                        db.QuoteItems
                        .Add(new QuoteItem
                        {
                            Quote = masterRecordId.Value,
                            Ordinal = newRecordOrdinal,
                            QuantityBy = 1,
                            DoubleHit = false,
                            Collate = false,
                            Staple = false,
                            Numbering = false,
                            UpValueLocked = false,
                            ArtworkProvided = true
                        });

                    await db.SaveChangesAsync();
                    await db.Entry(qi).ReloadAsync(); //reload the saved record?

                    //add required services
                    foreach (Service s in db.Services.Where(x => x.Deleted == null && (x.Required ?? false) == true).ToList())
                        db.QuoteItemServices.Add(new QuoteItemService { QuoteItemId = qi.Id, OriginalService = s.Id, Ordinal = 1, Title = s.Title });

                    QuoteItemService qPrintService =
                        db.QuoteItemServices
                        .Add(new QuoteItemService
                        {
                            QuoteItemId = qi.Id,
                            OriginalService = db.Services.Single(x => x.SerNo == (int)Service.SerNoSpecialServices.Print).Id,
                            Ordinal = 2,
                            Title = "Print"
                        });

                    if (qi.Press.HasValue)
                    {
                        await db.Entry(qi).Reference(r => r.Press1).LoadAsync();
                        qPrintService.HourlyRate = qi.Press1.LaborCostPerHour ?? 0m;
                        qPrintService.MakeReady = qi.Press1.MakeReady ?? 0m;
                        qPrintService.WashUp = qi.Press1.WashUp ?? 0m;
                    }

                    await db.SaveChangesAsync();
                    await db.Entry(qi).Collection(r => r.QuoteItemServices).LoadAsync();

                    foreach (QuoteItemService qis in qi.QuoteItemServices)
                    {
                        await db.Entry(qis).Reference(r => r.Service).LoadAsync();
                        if (qis.Service == null) continue;
                        await db.Entry(qis.Service).Reference(r => r.ServiceCategory).LoadAsync();
                        await db.Entry(qis.Service.ServiceCategory).Reference(r => r.ServiceType1).LoadAsync();
                    }

                    ViewData[("hasDutchCut_" + qi.Id.ToString())] = false;

                    return new JsonResult { Data = JsonConvert.SerializeObject(RenderViewToString("~/Views/Partials/_QuoteItemRow.cshtml", new VM_QuoteItem { QuoteItem = qi })), JsonRequestBehavior = JsonRequestBehavior.AllowGet };

                case "JOB":
                    ViewData["JobItem.PressSpeedDenominator"] = await GenerateSelectListAsync(SelectListType.SpeedDenominators);
                    JobItem jobItem =
                        db.JobItems
                        .Add(new JobItem
                        {
                            Job = masterRecordId.Value,
                            Ordinal = newRecordOrdinal,
                            QuantityBy = 1,
                            DoubleHit = false,
                            Collate = false,
                            Staple = false,
                            Numbering = false,
                            UpValueLocked = false,
                            ArtworkProvided = true
                        });
                    await db.SaveChangesAsync();
                    await db.Entry(jobItem).ReloadAsync();

                    //add required services
                    foreach (Service s in db.Services.Where(x => x.Deleted == null && (x.Required ?? false) == true).ToList())
                        db.JobItemServices.Add(new JobItemService { JobItemId = jobItem.Id, ServiceId = s.Id, Ordinal = 1, Title = s.Title });

                    JobItemService jPrintService =
                        db.JobItemServices
                        .Add(new JobItemService
                        {
                            JobItemId = jobItem.Id,
                            ServiceId = db.Services.Single(x => x.SerNo == (int)Service.SerNoSpecialServices.Print).Id,
                            Ordinal = 2,
                            Title = "Print"
                        });

                    if (jobItem.Press.HasValue)
                    {
                        await db.Entry(jobItem).Reference(r => r.Press1).LoadAsync();
                        jPrintService.HourlyRate = jobItem.Press1.LaborCostPerHour ?? 0m;
                        jPrintService.MakeReady = jobItem.Press1.MakeReady ?? 0m;
                        jPrintService.WashUp = jobItem.Press1.WashUp ?? 0m;
                    }

                    await db.SaveChangesAsync();
                    await db.Entry(jobItem).Collection(r => r.JobItemServices).LoadAsync();

                    foreach (JobItemService jis in jobItem.JobItemServices)
                    {
                        await db.Entry(jis).Reference(r => r.Service).LoadAsync();
                        await db.Entry(jis.Service).Reference(r => r.ServiceCategory).LoadAsync();
                        await db.Entry(jis.Service.ServiceCategory).Reference(r => r.ServiceType1).LoadAsync();
                    }

                    ViewData[("hasDutchCut_" + jobItem.Id.ToString())] = false;

                    var latestProof = await db.JobProofs
                        .Where(p => p.JobItemId == jobItem.Id && p.Deleted == null)
                        .OrderByDescending(p => p.Version)
                        .FirstOrDefaultAsync();
                    return new JsonResult { Data = JsonConvert.SerializeObject(RenderViewToString("~/Views/Partials/_JobItemRow.cshtml", new VM_JobItem { JobItem = jobItem, LatestProofStatus = latestProof?.ApprovalStatus })), JsonRequestBehavior = JsonRequestBehavior.AllowGet };

                default:
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, "Invalid item type");
            }
        }

        public async Task<ActionResult> ViewJobLayout(int? jobItemId)
        {
            // Find the specific QuoteItem by its ID
            JobItem jobItem = await db.JobItems
                .Include(qi => qi.Material1)
                .Include(qi => qi.Press1)
                .Include(qi => qi.Job1)
                .Include(qi => qi.SpeedDenominator)
                .Include(qi => qi.JobItemServices.Select(s => s.Service.ServiceCategory))
                .FirstOrDefaultAsync(qi => qi.Id == jobItemId && qi.Material != null);

            // If the JobItem is not found, return an error
            if (jobItem == null)
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "unable to find that job item");

            // Load related entities that were lazy-loaded
            if (jobItem.Material.HasValue)
            {
                await db.Entry(jobItem.Material1).Reference(r => r.MaterialStockType).LoadAsync();
            }

            // Additional processing specific to Material1.StockType
            if (jobItem.Material1.StockType != (int)Material.AvailableStockTypes.RollStock)
                jobItem.MaterialLength = Decimal.Parse(jobItem.Length) * 2.5m;

            foreach (JobItemService qis in jobItem.JobItemServices.ToList())
            {
                await db.Entry(qis).Reference(r => r.Service).LoadAsync();
                if (qis.Service == null) continue;
                await db.Entry(qis.Service).Reference(r => r.ServiceCategory).LoadAsync();
            }

            // Create the ViewModel for a single JobItem
            jobItem.ParseRunSize();

            string runSizeLength = "0";
            string runSizeWidth = "0";

            runSizeLength = jobItem.RunLength.ToString();
            runSizeWidth = jobItem.RunWidth.ToString();

            if (jobItem.Material1.StockType == (int)Material.AvailableStockTypes.RollStock)
            {
                decimal lResultOne = Decimal.Parse(jobItem.Length ?? "0") + Decimal.Parse(jobItem.Gripper ?? "0") + Decimal.Parse(jobItem.Tail ?? "0") + Decimal.Parse(jobItem.MarginYT ?? "0") + Decimal.Parse(jobItem.MarginYB ?? "0");
                decimal lResultTwo = Decimal.Parse(jobItem.Width ?? "0") + Decimal.Parse(jobItem.Gripper ?? "0") + Decimal.Parse(jobItem.Tail ?? "0") + Decimal.Parse(jobItem.MarginYT ?? "0") + Decimal.Parse(jobItem.MarginYB ?? "0");

                if (lResultOne < lResultTwo)
                {
                    runSizeLength = lResultTwo.ToString();
                    jobItem.Swap = false;
                }
                else
                {
                    runSizeLength = lResultOne.ToString();
                    jobItem.Swap = true;
                }
            }

            VM_JobLayoutPlanning model = new VM_JobLayoutPlanning
            {
                Job = jobItem.Job1,
                VM_ItemMaterialLayouts = new List<VM_JobItemMaterialLayout>
                {
                    new VM_JobItemMaterialLayout
                    {
                        MaterialLength = runSizeLength,
                        MaterialWidth = runSizeWidth,
                        ItemLength = jobItem.Length,
                        ItemWidth = jobItem.Width,
                        Quantity = jobItem.TotalQuantity.ToString() ?? "",
                        MarginXL = String.IsNullOrEmpty(jobItem.MarginXL) ? "0" : jobItem.MarginXL,
                        MarginXR = String.IsNullOrEmpty(jobItem.MarginXR) ? "0" : jobItem.MarginXR,
                        MarginYT = String.IsNullOrEmpty(jobItem.MarginYT) ? "0" : jobItem.MarginYT,
                        MarginYB = String.IsNullOrEmpty(jobItem.MarginYB) ? "0" : jobItem.MarginYB,
                        MixGrain = jobItem.MixGrain ?? false,
                        SideGuides = jobItem.SideGuides ?? "0",
                        Gripper = jobItem.Gripper ?? "0",
                        Tail = jobItem.Tail ?? "0",
                        Up = jobItem.Up.HasValue ? jobItem.Up.Value.ToString() : "",
                        StockType = jobItem.Material1.MaterialStockType.Title,
                        SwapOverride = jobItem.Swap ?? false,
                        CssSelector = "#material_0",
                        MaterialId = "material_0",
                        ItemId = jobItem.Id,
                        ItemDescription = jobItem.Description ?? "No description set",
                        PressId = jobItem.Press,
                        UpValueLocked = jobItem.UpValueLocked ?? false,
                        VM_SelectedJobItemServices = jobItem.JobItemServices.Select(z => new VM_SelectedJobItemService
                        {
                            Category = z.Service.ServiceCategory.Title,
                            ServiceIndex = z.Ordinal ?? 0,
                            Title = z.Service.Title,
                            JobItemService = z
                        }).ToList()
                    }
                }
            };

            var res = new
            {
                html = RenderViewToString("~/Views/Job/ViewJobLayout.cshtml", model)
            };

            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(res, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        //ViewQuoteLayout refactored by Copilot
        public async Task<ActionResult> ViewQuoteLayout(int? quoteItemId)
        {
            // Find the specific QuoteItem by its ID
            QuoteItem quoteItem = await db.QuoteItems
                .Include(qi => qi.Material1)
                .Include(qi => qi.Press1)
                .Include(qi => qi.Quote1)
                .Include(qi => qi.SpeedDenominator)
                .Include(qi => qi.QuoteItemServices.Select(s => s.Service.ServiceCategory))
                .FirstOrDefaultAsync(qi => qi.Id == quoteItemId && qi.Material != null);

            // If the QuoteItem is not found, return an error
            if (quoteItem == null)
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "unable to find that quote item");

            // Load related entities that were lazy-loaded
            if (quoteItem.Material.HasValue)
            {
                await db.Entry(quoteItem.Material1).Reference(r => r.MaterialStockType).LoadAsync();
            }

            // Additional processing specific to Material1.StockType
            if (quoteItem.Material1.StockType != (int)Material.AvailableStockTypes.RollStock)
                quoteItem.MaterialLength = Decimal.Parse(quoteItem.Length) * 2.5m;

            foreach (QuoteItemService qis in quoteItem.QuoteItemServices.ToList())
            {
                await db.Entry(qis).Reference(r => r.Service).LoadAsync();
                if (qis.Service == null) continue;
                await db.Entry(qis.Service).Reference(r => r.ServiceCategory).LoadAsync();
            }

            // Create the ViewModel for a single QuoteItem
            quoteItem.ParseRunSize();

            string runSizeLength = "0";
            string runSizeWidth = "0";

            runSizeLength = quoteItem.RunLength.ToString();
            runSizeWidth = quoteItem.RunWidth.ToString();

            if (quoteItem.Material1.StockType == (int)Material.AvailableStockTypes.RollStock)
            {
                decimal lResultOne = Decimal.Parse(quoteItem.Length ?? "0") + Decimal.Parse(quoteItem.Gripper ?? "0") + Decimal.Parse(quoteItem.Tail ?? "0") + Decimal.Parse(quoteItem.MarginYT ?? "0") + Decimal.Parse(quoteItem.MarginYB ?? "0");
                decimal lResultTwo = Decimal.Parse(quoteItem.Width ?? "0") + Decimal.Parse(quoteItem.Gripper ?? "0") + Decimal.Parse(quoteItem.Tail ?? "0") + Decimal.Parse(quoteItem.MarginYT ?? "0") + Decimal.Parse(quoteItem.MarginYB ?? "0");

                if (lResultOne < lResultTwo)
                {
                    runSizeLength = lResultTwo.ToString();
                    quoteItem.Swap = false;
                }
                else
                {
                    runSizeLength = lResultOne.ToString();
                    quoteItem.Swap = true;
                }
            }

            VM_QuoteLayoutPlanning model = new VM_QuoteLayoutPlanning
            {
                Quote = quoteItem.Quote1,
                VM_ItemMaterialLayouts = new List<VM_QuoteItemMaterialLayout>
                {
                    new VM_QuoteItemMaterialLayout
                    {
                        MaterialLength = runSizeLength,
                        MaterialWidth = runSizeWidth,
                        ItemLength = quoteItem.Length,
                        ItemWidth = quoteItem.Width,
                        Quantity = quoteItem.TotalQuantity.ToString() ?? "",
                        MarginXL = String.IsNullOrEmpty(quoteItem.MarginXL) ? "0" : quoteItem.MarginXL,
                        MarginXR = String.IsNullOrEmpty(quoteItem.MarginXR) ? "0" : quoteItem.MarginXR,
                        MarginYT = String.IsNullOrEmpty(quoteItem.MarginYT) ? "0" : quoteItem.MarginYT,
                        MarginYB = String.IsNullOrEmpty(quoteItem.MarginYB) ? "0" : quoteItem.MarginYB,
                        MixGrain = quoteItem.MixGrain ?? false,
                        SideGuides = quoteItem.SideGuides ?? "0",
                        Gripper = quoteItem.Gripper ?? "0",
                        Tail = quoteItem.Tail ?? "0",
                        Up = quoteItem.Up.HasValue ? quoteItem.Up.Value.ToString() : "",
                        StockType = quoteItem.Material1.MaterialStockType.Title,
                        SwapOverride = quoteItem.Swap ?? false,
                        CssSelector = "#material_0",
                        MaterialId = "material_0",
                        ItemId = quoteItem.Id,
                        ItemDescription = quoteItem.Description ?? "No description set",
                        PressId = quoteItem.Press,
                        UpValueLocked = quoteItem.UpValueLocked ?? false,
                        VM_SelectedQuoteItemServices = quoteItem.QuoteItemServices.Select(z => new VM_SelectedQuoteItemService
                        {
                            Category = z.Service.ServiceCategory.Title,
                            ServiceIndex = z.Ordinal,
                            Title = z.Service.Title,
                            QuoteItemService = z
                        }).ToList()
                    }
                }
            };

            var res = new
            {
                html = RenderViewToString("~/Views/Job/ViewQuoteLayout.cshtml", model)
            };

            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(res, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> OverrideJobLayout(JobLayoutOverride_CM overrides)
        {
            JobItem ji = await db.JobItems.FindAsync(overrides.JobItemId);

            if (ji == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "no Job item found with id " + overrides.JobItemId);

            ji.Quantity = overrides.Quantity;
            ji.Up = overrides.Up;
            ji.MarginXL = overrides.MarginXL;
            ji.MarginXR = overrides.MarginXR;
            ji.MarginYT = overrides.MarginYT;
            ji.MarginYB = overrides.MarginYB;
            ji.SideGuides = overrides.SideGuides;
            ji.Gripper = overrides.Gripper;
            ji.Tail = overrides.Tail;
            ji.UpValueLocked = overrides.UpValueLocked;

            db.Entry(ji).State = EntityState.Modified;

            await db.SaveChangesAsync();

            return new HttpStatusCodeResult(HttpStatusCode.OK);
        }

        public async Task<ActionResult> OverrideQuoteLayout(QuoteLayoutOverride_CM overrides)
        {
            QuoteItem qi = await db.QuoteItems.FindAsync(overrides.QuoteItemId);

            if (qi == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "no quote item found with id " + overrides.QuoteItemId);

            qi.Up = overrides.Up;
            qi.MarginXL = overrides.MarginXL;
            qi.MarginXR = overrides.MarginXR;
            qi.MarginYT = overrides.MarginYT;
            qi.MarginYB = overrides.MarginYB;
            qi.SideGuides = overrides.SideGuides;
            qi.Gripper = overrides.Gripper;
            qi.Tail = overrides.Tail;
            qi.UpValueLocked = overrides.UpValueLocked;

            db.Entry(qi).State = EntityState.Modified;

            await db.SaveChangesAsync();

            return new HttpStatusCodeResult(HttpStatusCode.OK);
        }

        public async Task<ActionResult> SaveServiceArrangement(ItemConfiguration[] configParam, string itemType)
        {
            List<ItemConfiguration> config = configParam.ToList();

            switch (itemType.ToUpper())
            {
                case "QUOTE":
                    foreach (ItemConfiguration ic in config)
                    {
                        List<ItemService> itemServices = ic.Services.ToList();
                        List<QuoteItemService> qis = await db.QuoteItemServices.Where(x => x.QuoteItemId == ic.ItemId).ToListAsync();

                        foreach (QuoteItemService q in qis)
                        {
                            try
                            {
                                ItemService service = itemServices.Single(x => x.ServiceItemRecordId == q.Id);
                                q.ApexOrdinal = service.ApexOrdinal;

                                db.Entry(q).State = EntityState.Modified;
                            }
                            catch (Exception)
                            {
                                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Critical error");
                            }
                        }
                    }
                    break;

                case "JOB":
                    foreach (ItemConfiguration ic in config)
                    {
                        List<ItemService> itemServices = ic.Services.ToList();
                        List<JobItemService> jis = await db.JobItemServices.Where(x => x.JobItemId == ic.ItemId).ToListAsync();

                        foreach (JobItemService j in jis)
                        {
                            try
                            {
                                ItemService service = itemServices.Single(x => x.ServiceItemRecordId == j.Id);
                                j.ApexOrdinal = service.ApexOrdinal;

                                db.Entry(j).State = EntityState.Modified;
                            }
                            catch (Exception)
                            {
                                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Critical error");
                            }
                        }
                    }
                    break;

                default:
                    break;
            }

            try
            {
                await db.SaveChangesAsync();
            }
            catch (Exception e)
            {
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, e.Message);
            }
            return new JsonResult { Data = "Saved", JsonRequestBehavior = JsonRequestBehavior.AllowGet };
        }

        public async Task<ActionResult> DuplicateQuote(int? id)
        {
            //get the current user cookie
            HttpCookie activeUser = HttpContext.Response.Cookies["ActiveUser"];
            int userId = int.Parse(activeUser.Value);

            ObjectResult<int?> dqRes = db.CopyQuote(id, userId);

            int newQuoteId = dqRes.First().Value;

            //copy documents
            Quote q = await db.Quotes.FindAsync(id);
            await q.CopyDocumentsToNewQuote(newQuoteId, db);

            return new JsonResult
            {
                Data = newQuoteId.ToString(),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> DuplicateJob(int? id)
        {
            HttpCookie activeUser = HttpContext.Response.Cookies["ActiveUser"];
            int userId = int.Parse(activeUser.Value);

            ObjectResult<int?> djRes = db.CopyJob(id, userId);

            int newJobId = djRes.First().Value;

            //copy documents
            Job j = await db.Jobs.FindAsync(id);
            await j.CopyDocumentsToNewJob(newJobId, db);

            return RedirectToAction("EditJob", new { jobId = newJobId });
        }

        public async Task<ActionResult> AjaxUpdateSpeed(int? id, string type, int? numerator, int? denominator, int? speedId)
        {
            if (!id.HasValue)
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "No id provided");

            if (String.IsNullOrEmpty(type))
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "No id provided");

            if (!numerator.HasValue)
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "No speed numerator provided");

            if (!denominator.HasValue)
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "No speed denominator provided");

            switch (type.ToUpper())
            {
                case "QUOTE":
                    QuoteItem qi = await db.QuoteItems.FindAsync(id);
                    if (qi == null)
                        return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Cannot find that quote item");

                    qi.PressSpeedNumerator = numerator.Value;
                    qi.PressSpeedDenominator = denominator.Value;
                    qi.SelectedSpeed = speedId == 0 ? null : speedId;

                    db.Entry(qi).State = EntityState.Modified;
                    break;

                case "JOB":
                    JobItem ji = await db.JobItems.FindAsync(id);
                    if (ji == null)
                        return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Cannot find that job item");

                    ji.PressSpeedNumerator = numerator.Value;
                    ji.PressSpeedDenominator = denominator.Value;
                    ji.SelectedSpeed = speedId == 0 ? null : speedId;

                    db.Entry(ji).State = EntityState.Modified;
                    break;
            }

            await db.SaveChangesAsync();

            return new JsonResult { Data = "done", JsonRequestBehavior = JsonRequestBehavior.AllowGet };
        }

        /// <summary>
        /// Condensed read-only job view with reprint functionality
        /// </summary>
        public async Task<ActionResult> ViewJob(int? id)
        {
            if (!id.HasValue)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Job ID is required");

            // Load job with related data
            var job = await db.Jobs
                .Include(j => j.Client1)
                .Include(j => j.Contact1)
                .Include(j => j.JobStatus)
                .Include(j => j.JobItems.Select(ji => ji.JobPrices))
                .Include(j => j.JobItems.Select(ji => ji.Material1))
                .FirstOrDefaultAsync(j => j.Id == id.Value);

            if (job == null)
                return new HttpStatusCodeResult(HttpStatusCode.NotFound, "Job not found");

            // Calculate totals
            decimal totalCost = job.JobItems
                .Where(ji => ji.Deleted == null)
                .Sum(ji => ji.JobPrices?.Sum(jp => jp.Price ?? 0) ?? 0);

            ViewBag.TotalCost = totalCost;
            ViewBag.FormattedTotalCost = totalCost.ToString("C");

            // Format MarkupOverride as percentage (stored as decimal, e.g., 0.89 = 89%)
            ViewBag.MarkupOverridePercent = job.MarkupOverride.HasValue
                ? (job.MarkupOverride.Value * 100).ToString("0.##") + "%"
                : null;

            // Load reprint history using ReprintService
            var reprintService = new ReprintService(db);
            var reprintHistory = await reprintService.GetReprintHistoryAsync(id.Value);
            ViewBag.ReprintHistory = reprintHistory;

            return View("~/Views/Job/ViewJob.cshtml", job);
        }

        public async Task<ActionResult> ViewQuote(int? id)
        {
            if (!id.HasValue)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Quote ID is required");

            // Load quote with related data
            var quote = await db.Quotes
                .Include(q => q.Client1)
                .Include(q => q.Contact1)
                .Include(q => q.QuoteItems.Select(qi => qi.QuotePrices))
                .Include(q => q.QuoteItems.Select(qi => qi.Material1))
                .FirstOrDefaultAsync(q => q.Id == id.Value);

            if (quote == null)
                return new HttpStatusCodeResult(HttpStatusCode.NotFound, "Quote not found");

            // Calculate totals
            decimal totalCost = quote.QuoteItems
                .Where(qi => qi.Deleted == null)
                .Sum(qi => qi.QuotePrices?.Sum(qp => qp.Price ?? 0) ?? 0);

            ViewBag.TotalCost = totalCost;
            ViewBag.FormattedTotalCost = totalCost.ToString("C");

            // Format MarkupOverride as percentage (stored as decimal, e.g., 0.89 = 89%)
            ViewBag.MarkupOverridePercent = quote.MarkupOverride.HasValue
                ? (quote.MarkupOverride.Value * 100).ToString("0.##") + "%"
                : null;

            return View("~/Views/Job/ViewQuote.cshtml", quote);
        }

        public async Task<ActionResult> UpdateMaterialReset(int? id, string itemType, int? materialId)
        {
            if (String.IsNullOrEmpty(itemType))
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "no item type provided");

            decimal stockTypeAmount = 0m;
            decimal materialCostScaled = 0m;

            switch (itemType.ToUpper())
            {
                case "QUOTE":
                    QuoteItem qi = await db.QuoteItems.FindAsync(id);
                    if (qi == null)
                        return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Record not found");

                    await db.Entry(qi).Reference(r => r.Press1).LoadAsync();

                    Material mqi = await db.Materials.FindAsync(materialId);
                    await db.Entry(mqi).Reference(r => r.MaterialCostType).LoadAsync();

                    if (mqi == null)
                        return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Material Not Found");

                    //implement stock types here
                    switch ((Material.AvailableStockTypes)mqi.StockType)
                    {
                        case Material.AvailableStockTypes.Sheet:
                            stockTypeAmount = (qi.Sheets ?? 0 + qi.ScrapSheets ?? 0);
                            break;

                        case Material.AvailableStockTypes.RollStock:
                        case Material.AvailableStockTypes.CutVinyl:
                            qi.ParseRunSize();
                            stockTypeAmount = qi.RunLength / 12m;
                            break;

                        default:
                            return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "in switch default");
                    }
                    qi.Up = null;

                    materialCostScaled = mqi.Cost ?? 0m;

                    qi.MaterialCost = mqi.Cost ?? 0m;
                    qi.MaterialCostPerUnit = materialCostScaled;
                    qi.MaterialCostPerUnitFormatted = qi.MaterialCostPerUnit.Value.ToString("C");
                    qi.Material = materialId;
                    qi.MaterialRunSize = mqi.Size;

                    db.Entry(qi).State = EntityState.Modified;
                    await db.SaveChangesAsync();

                    try
                    {
                        await db.Entry(qi).Reference(r => r.Press1).LoadAsync();
                        Material.AvailableStockTypes materialStockType = (Material.AvailableStockTypes)mqi.StockType;
                        //if the stock type does not match between press and the material
                        if (qi.Press1.StockType != mqi.StockType)
                        {
                            //if qi.Press1.FromPress != qi.Press1.Id, Load the press record indicated by qi.Press1.FromPress
                            if (qi.Press1.FromPress != qi.Press1.Id)
                                await db.Entry(qi.Press1).Reference(r => r.Press1).LoadAsync();
                            {
                                Press masterPress = await db.Presses.FindAsync(qi.Press1.FromPress);
                                //if that stock type matches the material stock type, set the press to that press and update the press configuration
                                if (masterPress.StockType == mqi.StockType)
                                {
                                    //switch to this press
                                    qi.Press = masterPress.Id;
                                    qi.PressSpeedDenominator = masterPress.SpeedDenominator;
                                    qi.PressSpeedNumerator = masterPress.SpeedNumerator;
                                    qi.SideGuides = masterPress.SideGuides;
                                    qi.Gripper = masterPress.Gripper;
                                    qi.Tail = masterPress.Tail;
                                    qi.MarginXL = masterPress.MarginLeft;
                                    qi.MarginXR = masterPress.MarginRight;
                                    qi.MarginYB = masterPress.MarginBottom;
                                    qi.MarginYT = masterPress.MarginTop;
                                }
                                else
                                {
                                    //otherwise, select the single record with a matching stock type
                                    await db.Entry(masterPress).Collection(c => c.Presses1).LoadAsync();
                                    Press newPress = masterPress.Presses1.Single(y => y.StockType == mqi.StockType);
                                    qi.Press = newPress.Id;
                                    qi.PressSpeedDenominator = newPress.SpeedDenominator;
                                    qi.PressSpeedNumerator = newPress.SpeedNumerator;
                                    qi.SideGuides = newPress.SideGuides;
                                    qi.Gripper = newPress.Gripper;
                                    qi.Tail = newPress.Tail;
                                    qi.MarginXL = masterPress.MarginLeft;
                                    qi.MarginXR = masterPress.MarginRight;
                                    qi.MarginYB = masterPress.MarginBottom;
                                    qi.MarginYT = masterPress.MarginTop;
                                }
                            }

                            db.Entry(qi).State = EntityState.Modified;
                            await db.SaveChangesAsync();
                        }
                    }
                    catch (Exception)
                    {
                        //implies that no press with a matching stock type was found
                    }

                    var jsonq = new
                    {
                        MaterialCostFormatted = qi.MaterialCostPerUnitFormatted,
                        MaterialCost = Math.Round(qi.MaterialCostPerUnit.Value, 2), //.Value because null coalesce above
                        MatCostUnit = mqi.MaterialCostType.Title ?? "",
                        PressId = qi.Press,
                        ColorFrontProfile = qi.PressColorProfileFront,
                        ColorBackProfile = qi.PressColorProfileBack,
                        ColorCountFront = qi.PressColorCountFront,
                        ColorCountBack = qi.PressColorCountBack
                    };

                    return new JsonResult { Data = JsonConvert.SerializeObject(jsonq, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }), JsonRequestBehavior = JsonRequestBehavior.AllowGet };

                case "JOB":
                    JobItem ji = await db.JobItems.FindAsync(id);
                    if (ji == null)
                        return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Record not found");

                    await db.Entry(ji).Reference(r => r.Press1).LoadAsync();

                    Material mji = await db.Materials.FindAsync(materialId);
                    await db.Entry(mji).Reference(r => r.MaterialCostType).LoadAsync();

                    if (mji == null)
                        return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Material Not Found");

                    //implement stock types here
                    switch ((Material.AvailableStockTypes)mji.StockType)
                    {
                        case Material.AvailableStockTypes.Sheet:
                            stockTypeAmount = (ji.Sheets ?? 0 + ji.ScrapSheets ?? 0);
                            break;

                        case Material.AvailableStockTypes.RollStock:
                            ji.ParseRunSize();
                            stockTypeAmount = ji.RunLength / 12m;
                            break;

                        case Material.AvailableStockTypes.CutVinyl:
                            ji.ParseRunSize();
                            stockTypeAmount = ji.RunLength / 12m;
                            break;

                        default:
                            return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "in switch default");
                    }

                    ji.Up = null;

                    materialCostScaled = mji.Cost ?? 0m;

                    ji.MaterialCost = mji.Cost ?? 0m;
                    ji.MaterialCostPerUnit = materialCostScaled;
                    ji.MaterialCostPerUnitFormatted = ji.MaterialCostPerUnit.Value.ToString("C");
                    ji.Material = materialId;
                    ji.MaterialRunSize = mji.Size;

                    db.Entry(ji).State = EntityState.Modified;
                    await db.SaveChangesAsync();

                    try
                    {
                        Material.AvailableStockTypes materialStockType = (Material.AvailableStockTypes)mji.StockType;
                        //check if stock types match between the material and press
                        if (ji.Press1.StockType != mji.StockType)
                        {
                            //if qi.Press1.FromPress != qi.Press1.Id, Load the press record indicated by qi.Press1.FromPress
                            if (ji.Press1.FromPress != ji.Press1.Id)
                                await db.Entry(ji.Press1).Reference(r => r.Press1).LoadAsync();
                            {
                                Press masterPress = await db.Presses.FindAsync(ji.Press1.FromPress);
                                //if that stock type matches the material stock type, set the press to that press and update the press configuration
                                if (masterPress.StockType == mji.StockType)
                                {
                                    //switch to this press
                                    ji.Press = masterPress.Id;
                                    ji.PressSpeedDenominator = masterPress.SpeedDenominator;
                                    ji.PressSpeedNumerator = masterPress.SpeedNumerator;
                                    ji.SideGuides = masterPress.SideGuides;
                                    ji.Gripper = masterPress.Gripper;
                                    ji.Tail = masterPress.Tail;
                                    ji.MarginXL = masterPress.MarginLeft;
                                    ji.MarginXR = masterPress.MarginRight;
                                    ji.MarginYB = masterPress.MarginBottom;
                                    ji.MarginYT = masterPress.MarginTop;
                                }
                                else
                                {
                                    //otherwise, select the single record with a matching stock type
                                    await db.Entry(masterPress).Collection(c => c.Presses1).LoadAsync();
                                    Press newPress = masterPress.Presses1.Single(y => y.StockType == mji.StockType);
                                    ji.Press = newPress.Id;
                                    ji.PressSpeedDenominator = newPress.SpeedDenominator;
                                    ji.PressSpeedNumerator = newPress.SpeedNumerator;
                                    ji.SideGuides = newPress.SideGuides;
                                    ji.Gripper = newPress.Gripper;
                                    ji.Tail = newPress.Tail;
                                    ji.MarginXL = masterPress.MarginLeft;
                                    ji.MarginXR = masterPress.MarginRight;
                                    ji.MarginYB = masterPress.MarginBottom;
                                    ji.MarginYT = masterPress.MarginTop;
                                }
                            }

                            db.Entry(ji).State = EntityState.Modified;
                            await db.SaveChangesAsync();
                        }
                    }
                    catch (Exception)
                    {
                        //implies that no press with a matching stock type was found
                    }

                    var jsonj = new
                    {
                        MaterialCostFormatted = ji.MaterialCostPerUnitFormatted,
                        MaterialCost = Math.Round(ji.MaterialCostPerUnit.Value, 2), //.Value because null coalesce above
                        MatCostUnit = mji.MaterialCostType.Title ?? "",
                        PressId = ji.Press,
                        ColorFrontProfile = ji.PressColorProfileFront,
                        ColorBackProfile = ji.PressColorProfileBack,
                        ColorCountFront = ji.PressColorCountFront,
                        ColorCountBack = ji.PressColorCountBack
                    };

                    return new JsonResult { Data = JsonConvert.SerializeObject(jsonj, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }), JsonRequestBehavior = JsonRequestBehavior.AllowGet };

                default:
                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "In default in switch");
            }
        }

        public async Task<ActionResult> GetDocuments(string itemType, int? id, bool? isOutsourced)
        {
            if (String.IsNullOrEmpty(itemType) || !id.HasValue || !isOutsourced.HasValue)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Invalid parameters");

            switch (itemType.ToUpper())
            {
                case "QUOTE":
                    List<VM_Document> qds =
                        isOutsourced.Value ?
                        await db.QuoteDocuments.Where(x => x.ItemId == id.Value && x.RowType == "Outsourced").Include(i => i.User).Select(y => new VM_Document
                        {
                            QuoteDocument = y,
                            DocumentName = ""
                        }).ToListAsync()
                        :
                        await db.QuoteDocuments.Where(x => x.ItemId == id.Value && x.RowType == "Standard").Include(i => i.User).Select(x => new VM_Document { QuoteDocument = x, DocumentName = "" }).ToListAsync();
                    foreach (VM_Document d in qds)
                        d.DocumentName = Path.GetFileName(d.QuoteDocument.DocumentPath);

                    return new JsonResult { Data = JsonConvert.SerializeObject(qds, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }), JsonRequestBehavior = JsonRequestBehavior.AllowGet };

                case "JOB":
                    List<VM_Document> jds =
                        isOutsourced.Value ?
                        await db.JobDocuments.Where(x => x.ItemId == id.Value && x.RowType == "Outsourced").Include(i => i.User).Select(y => new VM_Document { JobDocument = y, DocumentName = "" }).ToListAsync()
                        :
                        await db.JobDocuments.Where(x => x.ItemId == id.Value).Include(i => i.User).Select(x => new VM_Document { JobDocument = x, DocumentName = "" }).ToListAsync();
                    foreach (VM_Document d2 in jds)
                        d2.DocumentName = Path.GetFileName(d2.JobDocument.DocumentPath);

                    return new JsonResult { Data = JsonConvert.SerializeObject(jds, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }), JsonRequestBehavior = JsonRequestBehavior.AllowGet };

                default:
                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "In switch default");
            }
        }

        public async Task<ActionResult> GetDocument(int? id, string itemType)
        {
            if (!id.HasValue || String.IsNullOrEmpty(itemType))
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "parameter issue");

            string filePath = "";
            string fileName = "";

            switch (itemType.ToUpper())
            {
                case "QUOTE":
                    QuoteDocument qd = await db.QuoteDocuments.FindAsync(id);
                    if (qd == null)
                        return new HttpStatusCodeResult(HttpStatusCode.NotFound, "Document not found");

                    filePath = qd.DocumentPath;
                    fileName = Path.GetFileName(qd.DocumentPath);

                    break;

                case "JOB":
                    JobDocument jd = await db.JobDocuments.FindAsync(id);
                    if (jd == null)
                        return new HttpStatusCodeResult(HttpStatusCode.NotFound, "Document not found");

                    filePath = jd.DocumentPath;
                    fileName = Path.GetFileName(jd.DocumentPath);

                    break;

                default:
                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "in default on switch");
            }

            byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);

            return File(fileBytes, "application/zip", fileName);
        }

        public async Task<ActionResult> AddDocument(JobDocument_CM jDoc)
        {
            string storagePath = @"C:\Storage\mastermind_job_docs";

            //hidden field being rendered as a binary so we're just checking for true
            bool isOutsourcedItem = jDoc.RowType == "true";

            //get the user id value from the cookie
            HttpCookie activeUserCookie = HttpContext.Request.Cookies.Get("ActiveUser");

            int userId = -1;

            try
            {
                userId = int.Parse(activeUserCookie.Value);
            }
            catch (Exception)
            {
                //cannot parse user Id, so error
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Cannot parse id value");
            }

            if (jDoc.TheDocs != null && jDoc.TheDocs.Any())
            {
                foreach (var file in jDoc.TheDocs)
                {
                    if (file != null && file.ContentLength > 0)
                    {
                        // Read file contents
                        using (var reader = new BinaryReader(file.InputStream))
                        {
                            byte[] fileContent = reader.ReadBytes(file.ContentLength);
                            byte[] hashBytes;
                            using (SHA256 sha256 = SHA256.Create())
                            {
                                hashBytes = sha256.ComputeHash(fileContent);
                            }

                            string hashedContent = BitConverter.ToString(hashBytes).Replace("-", "_").ToLower();

                            string finalPath = "";
                            string directoryPath = "";

                            try
                            {
                                switch (jDoc.ItemType.ToLower())
                                {
                                    case "job":

                                        string jobNumberWithoutDashes = "";

                                        JobItem thisItem = await db.JobItems.FindAsync(jDoc.ItemId);
                                        Job job = await db.Jobs.FindAsync(thisItem.Job);
                                        string jobNumber = job.JobNumber;
                                        jobNumberWithoutDashes = jobNumber.Replace("-", "_");

                                        // Set the finalPath
                                        finalPath = Path.Combine(storagePath, jobNumberWithoutDashes, file.FileName);

                                        // Ensure directory exists
                                        directoryPath = Path.GetDirectoryName(finalPath);
                                        if (!Directory.Exists(directoryPath))
                                        {
                                            Directory.CreateDirectory(directoryPath);
                                        }

                                        if (System.IO.File.Exists(finalPath))
                                            return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "file " + file.FileName + " already exists");

                                        // Write the file
                                        System.IO.File.WriteAllBytes(finalPath, fileContent);

                                        db.JobDocuments.Add(new JobDocument
                                        {
                                            ItemId = jDoc.ItemId,
                                            DocumentPath = finalPath,
                                            DocumentHash = hashedContent,
                                            UploadedBy = userId,
                                            UploadDate = DateTime.UtcNow,
                                            RowType = isOutsourcedItem ? "Outsourced" : "Standard"
                                        });

                                        break;

                                    case "quote":

                                        QuoteItem qItem = await db.QuoteItems.FindAsync(jDoc.ItemId);
                                        Quote quote = await db.Quotes.FindAsync(qItem.Quote);
                                        string quoteNumber = quote.JobNumber;

                                        // Set the finalPath
                                        finalPath = Path.Combine(storagePath, quoteNumber, file.FileName);

                                        // Ensure directory exists
                                        directoryPath = Path.GetDirectoryName(finalPath);
                                        if (!Directory.Exists(directoryPath))
                                        {
                                            Directory.CreateDirectory(directoryPath);
                                        }

                                        if (System.IO.File.Exists(finalPath))
                                            return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "file " + file.FileName + " already exists");

                                        // Write the file
                                        System.IO.File.WriteAllBytes(finalPath, fileContent);

                                        db.QuoteDocuments.Add(new QuoteDocument
                                        {
                                            ItemId = jDoc.ItemId,
                                            DocumentPath = finalPath,
                                            DocumentHash = hashedContent,
                                            UploadedBy = userId,
                                            UploadDate = DateTime.UtcNow,
                                            RowType = isOutsourcedItem ? "Outsourced" : "Standard"
                                        });

                                        break;

                                    default:
                                        return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Invalid item type");
                                }

                                await db.SaveChangesAsync();
                            }
                            catch (Exception)
                            {
                                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Unknown error while saving file");
                            }
                        }
                    }
                }
            }
            else
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "No files uploaded.");
            }

            return new JsonResult { Data = "done", JsonRequestBehavior = JsonRequestBehavior.AllowGet };
        }

        #region Proof Methods

        [HttpPost]
        public async Task<ActionResult> AddProof(int itemId, HttpPostedFileBase proofFile)
        {
            // Validate PDF file type
            if (proofFile == null || proofFile.ContentLength == 0)
                return Json(new { success = false, message = "No file uploaded" });

            if (!proofFile.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                return Json(new { success = false, message = "Only PDF files are allowed" });

            // Get user id from cookie
            HttpCookie activeUserCookie = HttpContext.Request.Cookies.Get("ActiveUser");
            int userId = -1;
            try
            {
                userId = int.Parse(activeUserCookie.Value);
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "Cannot determine user" });
            }

            // Get job item and job number
            var jobItem = await db.JobItems.Include(ji => ji.Job1).FirstOrDefaultAsync(ji => ji.Id == itemId);
            if (jobItem == null)
                return Json(new { success = false, message = "Job item not found" });

            var jobNumber = jobItem.Job1.JobNumber.Replace("-", "_");

            // Create storage folder: {jobNumber}/items/{itemId}/proofs/
            string storagePath = @"C:\Storage\mastermind_job_docs";
            string proofFolder = Path.Combine(storagePath, jobNumber, "items", itemId.ToString(), "proofs");
            Directory.CreateDirectory(proofFolder);

            // Determine version number
            int existingVersions = await db.JobProofs.Where(p => p.JobItemId == itemId && p.Deleted == null).CountAsync();
            int version = existingVersions + 1;

            // Read file content
            byte[] fileContent;
            byte[] hashBytes;
            using (var reader = new BinaryReader(proofFile.InputStream))
            {
                fileContent = reader.ReadBytes(proofFile.ContentLength);
                using (SHA256 sha256 = SHA256.Create())
                {
                    hashBytes = sha256.ComputeHash(fileContent);
                }
            }
            string hashedContent = BitConverter.ToString(hashBytes).Replace("-", "_").ToLower();

            // Save PDF file with versioned name
            string pdfFileName = $"proof_v{version}_{proofFile.FileName}";
            string pdfPath = Path.Combine(proofFolder, pdfFileName);
            System.IO.File.WriteAllBytes(pdfPath, fileContent);

            // Generate thumbnail
            string thumbnailFileName = $"proof_v{version}_thumb.png";
            string thumbnailPath = Path.Combine(proofFolder, thumbnailFileName);
            string thumbnailError = null;
            try
            {
                GeneratePdfThumbnail(pdfPath, thumbnailPath);
            }
            catch (Exception ex)
            {
                // Thumbnail generation failed, continue without it
                thumbnailError = ex.Message;
                thumbnailPath = null;
            }

            // Create JobDocument record
            var jobDoc = new JobDocument
            {
                ItemId = itemId,
                DocumentPath = pdfPath,
                DocumentHash = hashedContent,
                UploadDate = DateTime.UtcNow,
                UploadedBy = userId,
                RowType = "Proof"
            };
            db.JobDocuments.Add(jobDoc);
            await db.SaveChangesAsync();

            // Create JobProof record
            var jobProof = new JobProof
            {
                JobDocumentId = jobDoc.Id,
                JobItemId = itemId,
                Version = version,
                ApprovalStatus = 0, // Pending
                RequestedBy = userId,
                CreatedAt = DateTime.UtcNow,
                ThumbnailPath = thumbnailPath
            };
            db.JobProofs.Add(jobProof);
            await db.SaveChangesAsync();

            return Json(new
            {
                success = true,
                proofId = jobProof.Id,
                version = version,
                thumbnailUrl = thumbnailPath != null ? $"/Job/GetProofThumbnail/{jobProof.Id}" : null,
                thumbnailError = thumbnailError
            });
        }

        public async Task<ActionResult> GetProofs(int itemId)
        {
            try
            {
                var proofs = await db.JobProofs
                    .Where(p => p.JobItemId == itemId && p.Deleted == null)
                    .OrderByDescending(p => p.Version)
                    .Select(p => new
                    {
                        proofId = p.Id,
                        version = p.Version,
                        status = p.ApprovalStatus,
                        jobDocumentId = p.JobDocumentId,
                        requestedBy = p.RequestedBy,
                        uploadDate = p.CreatedAt,
                        approvalDate = p.ApprovalDate,
                        approvalNotes = p.ApprovalNotes,
                        thumbnailPath = p.ThumbnailPath
                    })
                    .ToListAsync();

                var result = new List<object>();
                foreach (var p in proofs)
                {
                    string fileName = "Unknown";
                    if (p.jobDocumentId.HasValue)
                    {
                        var doc = await db.JobDocuments.FindAsync(p.jobDocumentId.Value);
                        if (doc != null && !string.IsNullOrEmpty(doc.DocumentPath))
                        {
                            fileName = Path.GetFileName(doc.DocumentPath);
                        }
                    }

                    string uploadedBy = "Unknown";
                    if (p.requestedBy.HasValue)
                    {
                        var user = await db.Users.FindAsync(p.requestedBy.Value);
                        if (user != null)
                        {
                            uploadedBy = (user.FirstName + " " + user.LastName).Trim();
                        }
                    }

                    // Get thumbnail as base64 to avoid concurrent request issues
                    string thumbnailBase64 = null;
                    if (!string.IsNullOrEmpty(p.thumbnailPath) && System.IO.File.Exists(p.thumbnailPath))
                    {
                        try
                        {
                            byte[] bytes = System.IO.File.ReadAllBytes(p.thumbnailPath);
                            thumbnailBase64 = "data:image/png;base64," + Convert.ToBase64String(bytes);
                        }
                        catch { }
                    }

                    result.Add(new
                    {
                        proofId = p.proofId,
                        version = p.version,
                        status = p.status,
                        statusText = p.status == 0 ? "Pending" : (p.status == 1 ? "Approved" : "Rejected"),
                        fileName = fileName,
                        uploadedBy = uploadedBy,
                        uploadDate = p.uploadDate,
                        approvalDate = p.approvalDate,
                        approvalNotes = p.approvalNotes,
                        thumbnailBase64 = thumbnailBase64
                    });
                }

                return Json(result, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public async Task<ActionResult> DeleteProof(int proofId)
        {
            var proof = await db.JobProofs.FindAsync(proofId);
            if (proof == null)
                return Json(new { success = false, message = "Proof not found" });

            // Soft delete
            proof.Deleted = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Json(new { success = true });
        }

        public async Task<ActionResult> DownloadProof(int proofId)
        {
            try
            {
                var proof = await db.JobProofs.FindAsync(proofId);
                if (proof == null || !proof.JobDocumentId.HasValue)
                    return HttpNotFound("Proof not found");

                var jobDoc = await db.JobDocuments.FindAsync(proof.JobDocumentId.Value);
                if (jobDoc == null || string.IsNullOrEmpty(jobDoc.DocumentPath))
                    return HttpNotFound("Document not found");

                string filePath = jobDoc.DocumentPath;
                if (!System.IO.File.Exists(filePath))
                    return HttpNotFound("File not found");

                string fileName = Path.GetFileName(filePath);
                return File(filePath, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                return HttpNotFound("Error: " + ex.Message);
            }
        }

        public ActionResult GetProofThumbnail(int proofId)
        {
            try
            {
                // Use AsNoTracking to avoid EF context conflicts with concurrent requests
                var thumbnailPath = db.JobProofs
                    .AsNoTracking()
                    .Where(p => p.Id == proofId)
                    .Select(p => p.ThumbnailPath)
                    .FirstOrDefault();

                if (string.IsNullOrEmpty(thumbnailPath))
                    return HttpNotFound("Proof or thumbnail not found");

                if (!System.IO.File.Exists(thumbnailPath))
                    return HttpNotFound("Thumbnail file not found: " + thumbnailPath);

                // Read file into memory to avoid file locking issues
                byte[] fileBytes = System.IO.File.ReadAllBytes(thumbnailPath);
                return File(fileBytes, "image/png");
            }
            catch (Exception ex)
            {
                return Content("Error: " + ex.Message, "text/plain");
            }
        }

        private void GeneratePdfThumbnail(string pdfPath, string thumbnailPath)
        {
            // Using PdfiumViewer for PDF rendering
            using (var document = PdfiumViewer.PdfDocument.Load(pdfPath))
            {
                // Render first page at 150 DPI for thumbnail
                using (var image = document.Render(0, 150, 150, true))
                {
                    image.Save(thumbnailPath, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
        }

        /// <summary>
        /// Generate a signed magic link for client access to proofs
        /// GET: /Job/GetProofAccessLink/{jobId}
        /// </summary>
        public async Task<ActionResult> GetProofAccessLink(int jobId)
        {
            try
            {
                var job = await db.Jobs.FindAsync(jobId);
                if (job == null || job.Deleted != null)
                    return Json(new { success = false, message = "Job not found" }, JsonRequestBehavior.AllowGet);

                if (!job.Client.HasValue)
                    return Json(new { success = false, message = "Job has no client assigned" }, JsonRequestBehavior.AllowGet);

                int clientId = job.Client.Value;

                // Generate HMAC-SHA256 signature of the clientId
                string signingKey = System.Configuration.ConfigurationManager.AppSettings["ProofAccessSigningKey"];
                string signature = GenerateHmacSignature(clientId.ToString(), signingKey);

                // Build the proof access URL
                string baseUrl = System.Configuration.ConfigurationManager.AppSettings["ProofsViewBaseUrl"];
                string proofAccessUrl = $"{baseUrl}/?clientId={clientId}&sig={signature}";

                return Json(new
                {
                    success = true,
                    proofAccessUrl = proofAccessUrl,
                    clientId = clientId
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Generate HMAC-SHA256 signature for a message
        /// </summary>
        private string GenerateHmacSignature(string message, string key)
        {
            using (var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(key)))
            {
                byte[] hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(message));
                return Convert.ToBase64String(hash).Replace("+", "-").Replace("/", "_").TrimEnd('='); // URL-safe base64
            }
        }

        #endregion

        public async Task<ActionResult> ServiceRowHandler(ItemService_CM items, int? pressId, string serviceType)
        {
            // Retrieve linked services based on pressId
            List<DefaultPressService> pressDefaultServices = new List<DefaultPressService>();

            if (pressId.HasValue)
            {
                pressDefaultServices = await db.DefaultPressServices.Where(x => x.Press == pressId).ToListAsync();
            }

            try
            {
                switch (items.RecordType.ToUpper())
                {
                    case "JOB":
                        var jobItem = await db.JobItems.FindAsync(items.RowId) ?? throw new Exception();
                        await db.Entry(jobItem).Collection(c => c.JobItemServices).LoadAsync();

                        int newJobItemServiceId = 0;
                        int ordinalCounterJob = 1;

                        try
                        {
                            newJobItemServiceId = await db.JobItemServices.MaxAsync(x => x.Id);
                        }
                        catch (Exception)
                        {
                            // No existing records, start from 1
                        }

                        newJobItemServiceId++;

                        foreach (var itemServiceDetail in items.ItemServiceDetails)
                        {
                            var service = await db.Services.FindAsync(itemServiceDetail.ServiceId);

                            if (itemServiceDetail.RecordId == 0)
                            {
                                // Adding a new item
                                var newJobItemService = new JobItemService
                                {
                                    Id = newJobItemServiceId,
                                    ServiceId = itemServiceDetail.ServiceId,
                                    JobItemId = items.RowId,
                                    IsCuttingService = itemServiceDetail.IsCuttingService,
                                    Title = service.Title,
                                    FriendlyName = service.FriendlyName
                                };

                                try
                                {
                                    newJobItemService.Ordinal = pressDefaultServices.Single(x => x.Service == service.Id).Ordinal;
                                }
                                catch (Exception)
                                {
                                    newJobItemService.Ordinal = ordinalCounterJob;
                                }

                                // Set defaults
                                newJobItemService = await SetJobItemServiceDefaults(newJobItemService);

                                db.JobItemServices.Add(newJobItemService);
                                newJobItemServiceId++;
                                ordinalCounterJob++;
                            }
                            else
                            {
                                // Removing an item
                                var removeJobItemService = await db.JobItemServices.FindAsync(itemServiceDetail.ServiceItemId);

                                if (removeJobItemService != null)
                                {
                                    // Remove the pricing record if it exists
                                    try
                                    {
                                        var jobPrice = await db.JobPrices.SingleAsync(x => x.JobItemService == removeJobItemService.Id);
                                        db.JobPrices.Remove(jobPrice);
                                    }
                                    catch (Exception) { }

                                    db.JobItemServices.Remove(removeJobItemService);
                                }
                            }
                        }

                        await db.SaveChangesAsync();
                        await db.Entry(jobItem).Collection(c => c.JobItemServices).LoadAsync();

                        foreach (var jobItemService in jobItem.JobItemServices)
                        {
                            await db.Entry(jobItemService).Reference(r => r.Service).LoadAsync();
                            await db.Entry(jobItemService.Service).Reference(r => r.ServiceCategory).LoadAsync();
                            await db.Entry(jobItemService.Service.ServiceCategory).Reference(r => r.ServiceType1).LoadAsync();
                        }

                        var jobItemServicesJson = new
                        {
                            services = RenderViewToString("~/Views/Partials/_ItemServiceRows.cshtml", new VM_ItemServices
                            {
                                ServiceList = await GetJobItemServiceList(jobItem.Id, serviceType)
                            })
                        };

                        /*var jobItemServicesJson = new
                        {
                            services = RenderViewToString("~/Views/Partials/_ItemServiceRows.cshtml", new VM_ItemServices
                            {
                                ServiceList = jobItem.JobItemServices
                                    .OrderBy(x => x.ApexOrdinal)
                                    .Select(x => new VM_ItemServiceRow
                                    {
                                        RecordId = x.Id,
                                        ServiceTitle = x.Service.Title,
                                        Ordinal = x.Ordinal ?? 0,
                                        ServiceId = x.ServiceId,
                                        Notes = x.Notes,
                                        Required = x.Service.Required ?? false,
                                        IsCuttingService = x.IsCuttingService ?? false,
                                        FormattedServiceTime = DecimalHoursToFormatted(x.ServiceTime ?? 0m),
                                        IsTimed = x.Service.IsTimed ?? false
                                    }).ToList()
                            })
                        };*/

                        return new JsonResult
                        {
                            Data = JsonConvert.SerializeObject(jobItemServicesJson, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                            JsonRequestBehavior = JsonRequestBehavior.AllowGet
                        };

                    case "QUOTE":
                        var quoteItem = await db.QuoteItems.FindAsync(items.RowId) ?? throw new Exception();
                        await db.Entry(quoteItem).Collection(c => c.QuoteItemServices).LoadAsync();

                        int newQuoteItemServiceId = 0;
                        int ordinalCounterQuote = 1;

                        try
                        {
                            newQuoteItemServiceId = await db.QuoteItemServices.MaxAsync(x => x.Id);
                        }
                        catch (Exception)
                        {
                            // No existing records, start from 1
                        }

                        newQuoteItemServiceId++;

                        foreach (var itemServiceDetail in items.ItemServiceDetails)
                        {
                            var service = await db.Services.FindAsync(itemServiceDetail.ServiceId);

                            if (itemServiceDetail.RecordId == 0)
                            {
                                // Adding a new item
                                var newQuoteItemService = new QuoteItemService
                                {
                                    Id = newQuoteItemServiceId,
                                    OriginalService = itemServiceDetail.ServiceId,
                                    QuoteItemId = items.RowId,
                                    IsCuttingService = itemServiceDetail.IsCuttingService,
                                    Title = service.Title,
                                    FriendlyName = service.FriendlyName,
                                    ShowOnQuote = service.ShowOnQuote ?? false
                                };

                                try
                                {
                                    newQuoteItemService.Ordinal = pressDefaultServices.Single(x => x.Service == service.Id).Ordinal.Value;
                                }
                                catch (Exception)
                                {
                                    newQuoteItemService.Ordinal = ordinalCounterQuote;
                                }

                                // Set defaults
                                newQuoteItemService = await SetQuoteItemServiceDefaults(newQuoteItemService);

                                db.QuoteItemServices.Add(newQuoteItemService);
                                newQuoteItemServiceId++;
                                ordinalCounterQuote++;
                            }
                            else
                            {
                                // Removing an item
                                var removeQuoteItemService = await db.QuoteItemServices.FindAsync(itemServiceDetail.ServiceItemId);
                                if (removeQuoteItemService != null)
                                {
                                    // Remove the pricing record if it exists
                                    try
                                    {
                                        var quotePrice = await db.QuotePrices.SingleAsync(x => x.QuoteItemService == removeQuoteItemService.Id);
                                        db.QuotePrices.Remove(quotePrice);
                                    }
                                    catch (Exception) { }

                                    db.QuoteItemServices.Remove(removeQuoteItemService);
                                }
                            }
                        }

                        await db.SaveChangesAsync();
                        await db.Entry(quoteItem).Collection(c => c.QuoteItemServices).LoadAsync();

                        foreach (var quoteItemService in quoteItem.QuoteItemServices)
                        {
                            await db.Entry(quoteItemService).Reference(r => r.Service).LoadAsync();
                            await db.Entry(quoteItemService.Service).Reference(r => r.ServiceCategory).LoadAsync();
                            await db.Entry(quoteItemService.Service.ServiceCategory).Reference(r => r.ServiceType1).LoadAsync();
                        }

                        var quoteItemServicesJson = new
                        {
                            services = RenderViewToString("~/Views/Partials/_ItemServiceRows.cshtml", new VM_ItemServices
                            {
                                ServiceList = await GetQuoteItemServiceList(quoteItem.Id, serviceType)
                            })
                        };

                        return new JsonResult
                        {
                            Data = JsonConvert.SerializeObject(quoteItemServicesJson, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                            JsonRequestBehavior = JsonRequestBehavior.AllowGet
                        };

                    default:
                        return new HttpStatusCodeResult(HttpStatusCode.OK);
                }
            }
            catch (Exception)
            {
                return new HttpStatusCodeResult(HttpStatusCode.OK);
            }
        }

        public async Task<List<VM_ItemServiceRow>> GetQuoteItemServiceList(int? id, string serviceType)
        {
            QuoteItem qi = await db.QuoteItems.FindAsync(id);
            if (qi != null)
            {
                List<VM_ItemServiceRow> services = new List<VM_ItemServiceRow>();

                await db.Entry(qi).Collection(c => c.QuoteItemServices).LoadAsync();

                foreach (QuoteItemService qis in qi.QuoteItemServices.ToList())
                {
                    await db.Entry(qis).Reference(r => r.Service).LoadAsync();
                    if (qis.Service == null) continue;
                    await db.Entry(qis.Service).Reference(r => r.ServiceCategory).LoadAsync();
                    await db.Entry(qis.Service.ServiceCategory).Reference(r => r.ServiceType1).LoadAsync();

                    //if serviceType == "Cutting", then we filter based on Finishing services with IsCuttingService = true
                    //else, we filter based on the serviceType
                    if (serviceType == "Cutting")
                    {
                        if (qis.Service.ServiceCategory.ServiceType1.Title == "Finishing" && (qis.Service.IsCuttingService ?? false))
                        {
                            services.Add(new VM_ItemServiceRow
                            {
                                RecordId = qis.Id,
                                ServiceTitle = qis.Title,
                                Ordinal = qis.Ordinal,
                                ServiceId = qis.OriginalService,
                                Notes = qis.Notes,
                                Required = qis.Service.Required ?? false,
                                IsCuttingService = qis.IsCuttingService ?? false,
                                IsFinishingService = qis.IsFinishingService ?? false,
                                ShowQAToggle = true,
                                RequiresQualityCheck = false,
                                FormattedServiceTime = DecimalHoursToFormatted(qis.ServiceTime ?? 0m),
                                IsTimed = qis.Service.IsTimed ?? false
                            });
                        }
                    }
                    else
                    {
                        if (qis.Service.ServiceCategory.ServiceType1.Title == serviceType)
                        {
                            services.Add(new VM_ItemServiceRow
                            {
                                RecordId = qis.Id,
                                ServiceTitle = qis.Title,
                                Ordinal = qis.Ordinal,
                                ServiceId = qis.OriginalService,
                                Notes = qis.Notes,
                                Required = qis.Service.Required ?? false,
                                IsCuttingService = qis.IsCuttingService ?? false,
                                IsFinishingService = qis.IsFinishingService ?? false,
                                ShowQAToggle = true,
                                RequiresQualityCheck = false,
                                FormattedServiceTime = DecimalHoursToFormatted(qis.ServiceTime ?? 0m),
                                IsTimed = qis.Service.IsTimed ?? false
                            });
                        }
                    }
                }

                return services;
            }

            return new List<VM_ItemServiceRow>();
        }

        public async Task<List<VM_ItemServiceRow>> GetJobItemServiceList(int? id, string serviceType)
        {
            JobItem ji = await db.JobItems.FindAsync(id);
            if (ji != null)
            {
                List<VM_ItemServiceRow> services = new List<VM_ItemServiceRow>();

                await db.Entry(ji).Collection(c => c.JobItemServices).LoadAsync();

                foreach (JobItemService jis in ji.JobItemServices.ToList())
                {
                    await db.Entry(jis).Reference(r => r.Service).LoadAsync();
                    await db.Entry(jis.Service).Reference(r => r.ServiceCategory).LoadAsync();
                    await db.Entry(jis.Service.ServiceCategory).Reference(r => r.ServiceType1).LoadAsync();

                    //if serviceType == "Cutting", then we filter based on Finishing services with IsCuttingService = true
                    //else, we filter based on the serviceType
                    if (serviceType == "Cutting")
                    {
                        if (jis.Service.ServiceCategory.ServiceType1.Title == "Finishing" && (jis.Service.IsCuttingService ?? false))
                        {
                            services.Add(new VM_ItemServiceRow
                            {
                                RecordId = jis.Id,
                                ServiceTitle = jis.Title,
                                Ordinal = jis.Ordinal ?? 0,
                                ServiceId = jis.ServiceId,
                                Notes = jis.Notes,
                                Required = jis.Service.Required ?? false,
                                IsCuttingService = jis.IsCuttingService ?? false,
                                IsFinishingService = jis.IsFinishingService ?? false,
                                ShowQAToggle = true,
                                RequiresQualityCheck = jis.RequiresQualityCheck ?? false,
                                FormattedServiceTime = DecimalHoursToFormatted(jis.ServiceTime ?? 0m),
                                IsTimed = jis.Service.IsTimed ?? false
                            });
                        }
                    }
                    else
                    {
                        if (jis.Service.ServiceCategory.ServiceType1.Title == serviceType)
                        {
                            services.Add(new VM_ItemServiceRow
                            {
                                RecordId = jis.Id,
                                ServiceTitle = jis.Title,
                                Ordinal = jis.Ordinal ?? 0,
                                ServiceId = jis.ServiceId,
                                Notes = jis.Notes,
                                Required = jis.Service.Required ?? false,
                                IsCuttingService = jis.IsCuttingService ?? false,
                                IsFinishingService = jis.IsFinishingService ?? false,
                                ShowQAToggle = true,
                                RequiresQualityCheck = jis.RequiresQualityCheck ?? false,
                                FormattedServiceTime = DecimalHoursToFormatted(jis.ServiceTime ?? 0m),
                                IsTimed = jis.Service.IsTimed ?? false
                            });
                        }
                    }
                }

                return services;
            }

            return new List<VM_ItemServiceRow>();
        }

        public async Task<ActionResult> SubitemLink(int? srcItem, int? destItem, string itemtype)
        {
            if (!srcItem.HasValue || !destItem.HasValue || String.IsNullOrEmpty(itemtype))
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "bad request");

            switch (itemtype.ToUpper())
            {
                case "QUOTE":
                    QuoteItem qToLink = await db.QuoteItems.FindAsync(srcItem);

                    if (qToLink != null)
                    {
                        qToLink.SubitemOf = destItem;
                        db.Entry(qToLink).State = EntityState.Modified;
                    }
                    break;

                case "JOB":
                    JobItem jToLink = await db.JobItems.FindAsync(srcItem);

                    if (jToLink != null)
                    {
                        jToLink.SubitemOf = destItem;
                        db.Entry(jToLink).State = EntityState.Modified;
                    }
                    break;

                case "O-JOB":
                    JobOutsourcedItem joiToLink = await db.JobOutsourcedItems.FindAsync(srcItem);

                    if (joiToLink != null)
                    {
                        joiToLink.SubitemOf = destItem;
                        db.Entry(joiToLink).State = EntityState.Modified;
                    }
                    break;

                case "O-QUOTE":
                    QuoteOutsourcedItem qoiToLink = await db.QuoteOutsourcedItems.FindAsync(srcItem);

                    if (qoiToLink != null)
                    {
                        qoiToLink.SubitemOf = destItem;
                        db.Entry(qoiToLink).State = EntityState.Modified;
                    }
                    break;

                default:
                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "invalid record type");
            }

            await db.SaveChangesAsync();

            return new JsonResult { Data = "done", JsonRequestBehavior = JsonRequestBehavior.AllowGet };
        }

        public async Task<ActionResult> SubitemUnlink(int? idToUnlink, string itemtype)
        {
            if (!idToUnlink.HasValue || String.IsNullOrEmpty(itemtype))
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "bad request");

            switch (itemtype.ToUpper())
            {
                case "QUOTE":
                    QuoteItem qToUnlink = await db.QuoteItems.FindAsync(idToUnlink);

                    if (qToUnlink != null)
                    {
                        qToUnlink.SubitemOf = null;
                        db.Entry(qToUnlink).State = EntityState.Modified;
                    }
                    break;

                case "JOB":
                    JobItem jToUnlink = await db.JobItems.FindAsync(idToUnlink);

                    if (jToUnlink != null)
                    {
                        jToUnlink.SubitemOf = null;
                        db.Entry(jToUnlink).State = EntityState.Modified;
                    }
                    break;

                case "O-JOB":
                    JobOutsourcedItem joiToUnlink = await db.JobOutsourcedItems.FindAsync(idToUnlink);

                    if (joiToUnlink != null)
                    {
                        joiToUnlink.SubitemOf = null;
                        db.Entry(joiToUnlink).State = EntityState.Modified;
                    }
                    break;

                case "O-QUOTE":
                    QuoteOutsourcedItem qoiToUnlink = await db.QuoteOutsourcedItems.FindAsync(idToUnlink);

                    if (qoiToUnlink != null)
                    {
                        qoiToUnlink.SubitemOf = null;
                        db.Entry(qoiToUnlink).State = EntityState.Modified;
                    }
                    break;

                default:
                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "invalid record type");
            }

            await db.SaveChangesAsync();

            return new JsonResult { Data = "done", JsonRequestBehavior = JsonRequestBehavior.AllowGet };
        }

        public async Task<ActionResult> UpdateItemOrdinals(List<int> itemIds, List<int> ordinals, List<string> itemtypes)
        {
            int recordCount = itemIds.Count;

            for (int i = 0; i < recordCount; i++)
            {
                switch (itemtypes[i].ToUpper())
                {
                    case "O-QUOTE":
                        QuoteOutsourcedItem qoi = await db.QuoteOutsourcedItems.FindAsync(itemIds[i]);
                        if (qoi != null)
                        {
                            qoi.Ordinal = ordinals[i];
                            db.Entry(qoi).State = EntityState.Modified;
                        }
                        break;

                    case "QUOTE":
                        QuoteItem qi = await db.QuoteItems.FindAsync(itemIds[i]);
                        if (qi != null)
                        {
                            qi.Ordinal = ordinals[i];
                            db.Entry(qi).State = EntityState.Modified;
                        }
                        break;

                    case "O-JOB":
                        JobOutsourcedItem joi = await db.JobOutsourcedItems.FindAsync(itemIds[i]);
                        if (joi != null)
                        {
                            joi.Ordinal = ordinals[i];
                            db.Entry(joi).State = EntityState.Modified;
                        }
                        break;

                    case "JOB":
                        JobItem ji = await db.JobItems.FindAsync(itemIds[i]);
                        if (ji != null)
                        {
                            ji.Ordinal = ordinals[i];
                            db.Entry(ji).State = EntityState.Modified;
                        }
                        break;
                }
            }

            await db.SaveChangesAsync();

            return new JsonResult
            {
                Data = "done",
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> GenerateCustomerQuote(Client.CustomerQuoteOptions quoteOption, int? recordId, bool finalTotal)
        {
            Quote quote = await db.Quotes.FindAsync(recordId);

            if (quote == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Missing record id");

            await db.Entry(quote).Reference(r => r.Client1).LoadAsync();

            if (quote.Client.HasValue)
                await db.Entry(quote.Client1).Reference(r => r.USState1).LoadAsync();

            await db.Entry(quote).Reference(r => r.Contact1).LoadAsync();
            await db.Entry(quote).Reference(r => r.ShippingOption).LoadAsync();
            await db.Entry(quote).Reference(r => r.ShippingAddress1).LoadAsync();

            if (quote.ShippingAddress.HasValue)
            {
                await db.Entry(quote.ShippingAddress1).Reference(r => r.USState).LoadAsync();
            }

            await db.Entry(quote).Collection(c => c.QuoteItems).LoadAsync();
            //new
            await db.Entry(quote).Collection(c => c.QuoteOutsourcedItems).LoadAsync();

            List<GenericItemOrganizer> org1 = quote.QuoteItems.Select(x => new GenericItemOrganizer
            {
                Ordinal = x.Ordinal ?? -1,
                RowType = "Standard",
                Id = x.Id
            }).ToList();

            List<GenericItemOrganizer> org2 = quote.QuoteOutsourcedItems.Select(x => new GenericItemOrganizer
            {
                Id = x.Id,
                RowType = "Outsourced",
                Ordinal = x.Ordinal ?? -1
            }).ToList();

            foreach (QuoteItem qi in quote.QuoteItems.ToList())
            {
                await db.Entry(qi).Reference(r => r.Material1).LoadAsync();
                await db.Entry(qi).Reference(r => r.Press1).LoadAsync();
                await db.Entry(qi).Reference(r => r.SpeedDenominator).LoadAsync();
                await db.Entry(qi).Collection(c => c.QuoteItemServices).LoadAsync();
                await db.Entry(qi).Collection(c => c.QuoteItemFinishingMaterials).LoadAsync();
            }

            VM_CustomerQuote vm = new VM_CustomerQuote { Quote = quote };
            vm.QuoteItemPricing = new VM_QuoteItemPricing();

            vm.AllItems = org1.Concat(org2).OrderBy(x => x.Ordinal).ToList();

            ViewBag.MaxOrdinal = vm.AllItems.Max(x => x.Ordinal);

            switch (quoteOption)
            {
                case Client.CustomerQuoteOptions.ParentItemTotals:
                    vm.QuoteItemPricing.ShowItemziedPrices = false;
                    vm.QuoteItemPricing.ShowItemPrices = true;

                    var parentTotals = db.QuoteParentItemTotals(quote.Id).OrderBy(x => x.Ordinal).ToList();

                    if (finalTotal)
                    {
                        vm.QuoteItemPricing.QuotePrice = parentTotals.Sum(x => (x.Total_Price ?? 0m));
                    }
                    else
                    {
                        vm.QuoteItemPricing.QuotePrice = null;
                    }

                    //getting the item prices into the final model
                    vm.QuoteItemPricing.QuoteItemPrices = new List<decimal>();

                    foreach (var itemTotal in parentTotals)
                    {
                        vm.QuoteItemPricing.QuoteItemPrices.Add((itemTotal.Total_Price ?? 0m));
                    }
                    break;

                case Client.CustomerQuoteOptions.OnlyFinalPrice:
                    vm.QuoteItemPricing.ShowItemziedPrices = false;
                    vm.QuoteItemPricing.ShowItemPrices = false;

                    var quoteTotal = db.QuoteTotalPrice(quote.Id);

                    if (finalTotal)
                    {
                        vm.QuoteItemPricing.QuotePrice = (quoteTotal.First().Total_Price ?? 0m);
                    }
                    else
                    {
                        vm.QuoteItemPricing.QuotePrice = null;
                    }
                    break;

                case Client.CustomerQuoteOptions.FullPricingBreakdown:
                    vm.QuoteItemPricing.ShowItemziedPrices = true;
                    vm.QuoteItemPricing.ShowItemPrices = true;

                    var breakdown = db.QuoteItemizedBreakdown(quote.Id);

                    int maxOrdinal = (breakdown.Max(x => x.Item_Ordinal)) ?? 0;

                    vm.QuoteItemPricing.QuoteItemItemizedPrices = new List<List<decimal>>();
                    vm.QuoteItemPricing.QuoteItemPrices = new List<decimal>();
                    vm.QuoteItemPricing.QuoteItemItemizedDescriptions = new List<List<string>>();

                    //getting the item prices into the final model

                    //getting the itemized prices into the final model
                    for (int i = 1; i <= maxOrdinal; i++)
                    {
                        //item total
                        vm.QuoteItemPricing.QuoteItemPrices.Add(breakdown.Where(x => x.Item_Ordinal == i).Sum(y => (y.Price ?? 0m)));

                        //new list of prices for the item
                        List<decimal> newPriceList = new List<decimal>();
                        List<string> newDescList = new List<string>();
                        //add all itemized prices for this item to the list
                        foreach (var det in breakdown.Where(x => x.Item_Ordinal == i).ToList())
                        {
                            newPriceList.Add((det.Price ?? 0));
                            newDescList.Add((det.Pricing_Descriptor ?? ""));
                        }
                        //add the lists to their final lists
                        vm.QuoteItemPricing.QuoteItemItemizedPrices.Add(newPriceList);
                        vm.QuoteItemPricing.QuoteItemItemizedDescriptions.Add(newDescList);
                    }

                    if (finalTotal)
                    {
                        vm.QuoteItemPricing.QuotePrice = vm.QuoteItemPricing.QuoteItemPrices.Sum(x => x);
                    }
                    else
                    {
                        vm.QuoteItemPricing.QuotePrice = null;
                    }

                    break;

                default:
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Invalid quote pricing option selected");
            }

            return View("CustomerQuote", vm);
        }

        public async Task<ActionResult> GetDeliveryTicket2(int? job, string po)
        {
            VM_DeliveryTicket2 vm = new VM_DeliveryTicket2
            {
                SourceInformation = new Models.CustomerQuoteModels.SourceInformation()
            };

            // Store PO number for blind shipping
            ViewBag.BlindShippingPO = po;

            Job j = await db.Jobs.FindAsync(job);

            if (j != null)
            {
                await db.Entry(j).Collection(c => c.JobItems).LoadAsync();
                await db.Entry(j).Reference(r => r.Client1).LoadAsync();

                if (j.Client1 != null && j.Client1.CreditHold == true)
                    throw new InvalidOperationException("Delivery ticket cannot be generated for a client on credit hold.");

                await db.Entry(j).Reference(r => r.ShippingAddress1).LoadAsync();
                await db.Entry(j).Collection(c => c.JobOutsourcedItems).LoadAsync();
                await db.Entry(j).Reference(r => r.Contact1).LoadAsync();
                await db.Entry(j).Reference(r => r.ShippingOption).LoadAsync();
                await db.Entry(j).Reference(r => r.User).LoadAsync();

                if (j.ShippingAddress.HasValue)
                {
                    await db.Entry(j.ShippingAddress1).Reference(r => r.USState).LoadAsync();
                }

                // Load blind shipping address if it exists
                if (j.BlindShippingAddress.HasValue)
                {
                    await db.Entry(j).Reference(r => r.BlindShippingPivotTable).LoadAsync();
                }

                if (j.Client.HasValue)
                {
                    await db.Entry(j.Client1).Reference(r => r.USState1).LoadAsync();
                }

                vm.Job = j;

                //get item totals
                List<JobParentItemTotals_Result> itemTotals = await db.JobParentItemTotals(j.Id).ToListAsync();

                List<VM_InvoiceRow2> finalRows = new List<VM_InvoiceRow2>();

                foreach (JobParentItemTotals_Result r in itemTotals)
                {
                    try
                    {
                        JobItem ji = j.JobItems.Single(x => x.Id == r.JobItem);

                        VM_InvoiceRow2 newRow = new VM_InvoiceRow2
                        {
                            Quantity = (ji.Quantity ?? 0).ToString("N0"),
                            Description = ji.Description ?? "",
                            Price = r.Total_Price ?? 0m,
                            PriceString = (r.Total_Price ?? 0m).ToString("N2"),
                            UnitPrice = Decimal.Round((r.Total_Price ?? 0m) / (ji.Quantity ?? 1), 4),
                            Notes = ji.Notes ?? "",
                            Ordinal = ji.Ordinal ?? 0
                        };
                        newRow.UnitPriceString = newRow.UnitPrice.ToString("N2");

                        finalRows.Add(newRow);
                    }
                    catch (Exception)
                    {
                        //try outsourced
                        JobOutsourcedItem joi = j.JobOutsourcedItems.Single(x => x.Id == r.JobItem);

                        VM_InvoiceRow2 joiNewRow = new VM_InvoiceRow2
                        {
                            Quantity = (joi.Quantity ?? 0).ToString("N0"),
                            Description = joi.Description ?? "",
                            Price = joi.JobCostAdjusted ?? 0m,
                            PriceString = (joi.JobCostAdjusted ?? 0m).ToString("N2"),
                            UnitPrice = Decimal.Round((joi.JobCostAdjusted ?? 0m) / (joi.Quantity ?? 1), 4),
                            Notes = joi.Notes ?? "",
                            Ordinal = joi.Ordinal ?? 0
                        };

                        joiNewRow.UnitPriceString = joiNewRow.UnitPrice.ToString("N2");

                        finalRows.Add(joiNewRow);
                    }
                }

                vm.Items = finalRows;

                try
                {
                    vm.ClientInformation = new Models.CustomerQuoteModels.ClientInformation();
                    vm.ClientInformation.ContactName = j.Contact.HasValue ? String.Format("{0} {1}", j.Contact1.First, j.Contact1.Last) : "";
                    vm.ClientInformation.Attention = "";
                    vm.ClientInformation.Address = j.Client.HasValue ? (j.Client1.Address ?? "") : "";
                    vm.ClientInformation.ClientName = j.Client.HasValue ? (j.Client1.Title ?? "") : "";
                    vm.ClientInformation.City = j.Client.HasValue ? (j.Client1.City ?? "") : "";
                    vm.ClientInformation.State = j.Client.HasValue ? (j.Client1.ShippingUSState.HasValue ? j.Client1.USState1.Abbreviation : "") : "";
                    vm.ClientInformation.ZipCode = j.Client.HasValue ? (j.Client1.Zip ?? "") : "";
                    vm.ClientInformation.Email = j.Contact.HasValue ? (j.Contact1.Email ?? "") : "";
                    vm.ClientInformation.Phone = j.Contact.HasValue ? (j.Contact1.Phone ?? "") : "";

                    /*vm.ClientInformation = new Models.CustomerQuoteModels.ClientInformation
                    {
                        ContactName = j.Contact.HasValue ? String.Format("{0} {1}", j.Contact1.First, j.Contact1.Last) : "",
                        Attention = "",
                        Address = j.Client.HasValue ? (j.Client1.Address ?? "") : "",
                        ClientName = j.Client.HasValue ? (j.Client1.Title ?? "") : "",
                        City = j.Client.HasValue ? (j.Client1.City ?? "") : "",
                        State = j.Client.HasValue ? (j.Client1.ShippingUSState.HasValue ? j.Client1.USState.Abbreviation : "") : "",
                        ZipCode = j.Client.HasValue ? (j.Client1.Zip ?? "") : "",
                        Email = j.Contact.HasValue ? (j.Contact1.Email ?? "") : "",
                        Phone = j.Contact.HasValue ? (j.Contact1.Phone ?? "") : ""
                    };*/
                }
                catch (Exception e)
                {
                }
                if ((j.IsPickup ?? false) || !j.ShippingAddress.HasValue)
                {
                    vm.ShippingInformation = new Models.CustomerQuoteModels.ShippingInformation
                    {
                        Address = "",
                        Address2 = "",
                        Attention = "",
                        City = "",
                        State = "",
                        ZipCode = "",
                        CompanyName = ""
                    };
                }
                else
                {
                    await db.Entry(j.ShippingAddress1).Reference(r => r.USState).LoadAsync();

                    vm.ShippingInformation = new Models.CustomerQuoteModels.ShippingInformation
                    {
                        Address = j?.ShippingAddress1.Address ?? "",
                        Address2 = j?.ShippingAddress1.Address2 ?? "",
                        Attention = j?.ShippingAddress1.Attention ?? "",
                        City = j?.ShippingAddress1.City ?? "",
                        State = j?.ShippingAddress1.USState.Abbreviation,
                        ZipCode = j?.ShippingAddress1.Zip ?? "",
                        CompanyName = j?.ShippingAddress1.CustomerName ?? ""
                    };
                }

                return View("DeliveryTicket2", vm);
            }

            return null;
        }

        public async Task<ActionResult> GetDeliveryTicket(int? job, string po)
        {
            VM_DeliveryTicket vm = new VM_DeliveryTicket();

            // Store PO number for blind shipping
            ViewBag.BlindShippingPO = po;

            Job j = await db.Jobs.FindAsync(job);

            if (j != null)
            {
                await db.Entry(j).Collection(c => c.JobItems).LoadAsync();
                await db.Entry(j).Reference(r => r.Client1).LoadAsync();

                if (j.Client1 != null && j.Client1.CreditHold == true)
                    throw new InvalidOperationException("Delivery ticket cannot be generated for a client on credit hold.");

                await db.Entry(j).Reference(r => r.Contact1).LoadAsync();
                await db.Entry(j).Reference(r => r.ShippingAddress1).LoadAsync();
                await db.Entry(j).Reference(r => r.User).LoadAsync();

                if (j.ShippingAddress.HasValue)
                {
                    await db.Entry(j.ShippingAddress1).Reference(r => r.USState).LoadAsync();
                }

                // Load blind shipping address if it exists
                if (j.BlindShippingAddress.HasValue)
                {
                    await db.Entry(j).Reference(r => r.BlindShippingPivotTable).LoadAsync();
                }

                if (j.Client.HasValue)
                {
                    await db.Entry(j.Client1).Reference(r => r.USState).LoadAsync();
                }

                vm.Job = j;

                vm.ItemGroups = GroupJobItems(vm.Job);
                vm.ItemGroups = vm.ItemGroups.OrderBy(x => x.ParentItem.Ordinal).ToList();

                return View("DeliveryTicket", vm);
            }

            return null;
        }

        public async Task<ActionResult> SaveItemNotesFromTable(int? recordId, string content)
        {
            if (!recordId.HasValue)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Missing record id");

            content = content ?? "";

            JobItem ji = await db.JobItems.FindAsync(recordId);

            if (ji != null)
            {
                ji.InternalNotes = content;
                db.Entry(ji).State = EntityState.Modified;
                await db.SaveChangesAsync();
            }

            return new JsonResult
            {
                Data = "Done",
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        //Runs when the "Ready to Ship" button is clicked on the job view
        public async Task<ActionResult> SetAsComplete(int? id)
        {
            Job j = await db.Jobs.FindAsync(id);

            if (j != null)
            {
                //return if we found a job but it's already complete
                if (j.Complete.HasValue)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.OK, "done");
                }

                j.Complete = DateTime.UtcNow;

                db.Entry(j).State = EntityState.Modified;

                await db.SaveChangesAsync();

                return new HttpStatusCodeResult(HttpStatusCode.OK, "done");
            }

            return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Job not found");
        }

        //Runs when the "Ready to Bill" button is clicked on the job view
        public async Task<ActionResult> SetAsShipped(int? id)
        {
            Job j = await db.Jobs.FindAsync(id);

            if (j != null)
            {
                //return if we found a job but it's already complete
                if (!j.Complete.HasValue)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "The job must be complete before being billed");
                }
                if (j.Shipped.HasValue)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.OK, "Already shipped - time not updated");
                }

                j.Shipped = DateTime.UtcNow;

                db.Entry(j).State = EntityState.Modified;

                await db.SaveChangesAsync();

                return new HttpStatusCodeResult(HttpStatusCode.OK, "done");
            }

            return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Job not found");
        }

        public async Task<ActionResult> UpdateJobAssignments(ServiceAssignment_CM config)
        {
            //get all assignment records for this service
            var ua = await db.UserAssignments.Where(x => x.JobItemService == config.recordId).ToListAsync();
            //delete records with User Id values that dont exist in config
            foreach (var u in ua)
            {
                if (!config.selectedUsers.Contains(u.Employee))
                {
                    db.UserAssignments.Remove(u);
                }
            }

            await db.SaveChangesAsync(); //save deletions

            ua = await db.UserAssignments.Where(x => x.JobItemService == config.recordId).ToListAsync();
            //add records with User Id values that dont exist in the db
            foreach (int u in config.selectedUsers)
            {
                if (!ua.Any(x => x.Employee == u))
                {
                    db.UserAssignments.Add(new UserAssignment
                    {
                        Employee = u,
                        JobItemService = config.recordId
                    });
                }
            }

            await db.SaveChangesAsync(); //save additions

            return new JsonResult
            {
                Data = "Done",
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        [HttpPost]
        public async Task<ActionResult> UpdateJob(Job j, JobItem[] items, JobOutsourcedItem[] outsourcedItems, TempNote[] TempNotes)
        {
            List<Task> inventoryTasks = new List<Task>();

            decimal rollStockLength = 0m;

            Job existing = await db.Jobs.FindAsync(j.Id);

            if (existing == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.NotFound, "Cannot find Job with id " + j.Id);

            await db.Entry(existing).Collection(c => c.JobItems).LoadAsync();
            await db.Entry(existing).Collection(c => c.JobOutsourcedItems).LoadAsync();

            foreach (JobItem ji2 in existing.JobItems)
                await db.Entry(ji2).Collection(c => c.JobItemServices).LoadAsync();

            existing.Title = j.Title;
            existing.Description = j.Description;

            // Handle client assignment/change - copy SQFT price from client
            if (existing.Client != j.Client)
            {
                if (j.Client.HasValue)
                {
                    // Client assigned or changed - copy SQFT price from new client
                    Client newClient = await db.Clients.FindAsync(j.Client.Value);
                    if (newClient != null)
                    {
                        // Copy client's SQFT price (or null if client doesn't have one)
                        existing.SqftPriceOverride = newClient.SqftPrice;
                    }
                }
                else
                {
                    // Client removed - clear SQFT price override
                    existing.SqftPriceOverride = null;
                }
            }

            existing.Client = j.Client;
            existing.Contact = j.Contact;
            existing.Notes = j.Notes;
            existing.JobNumber = j.JobNumber;
            existing.DueDate = j.DueDate; //job specific
            existing.InHandDate = j.InHandDate; //job specific
            existing.MinStartDate = j.MinStartDate; //job specific
            existing.InternalNotes = j.InternalNotes;
            existing.BillingNotes = j.BillingNotes;
            existing.BlindShipping = j.BlindShipping ?? false; //job specific
            existing.IsWarehouseJob = j.IsWarehouseJob ?? false;
            existing.IsEvent = j.IsEvent ?? false;
            existing.MarkupOverride = j.MarkupOverride;
            existing.IsPickup = j.IsPickup;
            existing.ShippingMethod = j.ShippingMethod;
            existing.PurchaseOrderNumber = j.PurchaseOrderNumber; //job specific
            existing.ScheduleSquash = j.ScheduleSquash;
            existing.Donated = j.Donated;

            db.Entry(existing).State = EntityState.Modified;

            await db.SaveChangesAsync();

            if (items != null)
            {
                foreach (JobItem ji in items.Where(x => x.SubitemOf == null).ToList())
                {
                    JobItem existingItem = existing.JobItems.Single(x => x.Id == ji.Id);

                    List<JobItem> subItems = existing.JobItems.Where(x => x.SubitemOf == ji.Id).ToList();

                    Press assignedPress = await db.Presses.FindAsync(ji.Press);
                    Material assignedMaterial = await db.Materials.FindAsync(ji.Material);

                    //assign default of 1 for material dimensions in case a material was not chosen
                    if (assignedMaterial == null)
                        assignedMaterial = new Material { Size = "1x1", ParsedDimensions = true, ParsedLength = "1", ParsedWidth = "1", StockType = (int)Material.AvailableStockTypes.Sheet, CostType = (int)Material.MaterialCostTypes.Piece };
                    else
                    {
                        await db.Entry(assignedMaterial).Reference(r => r.MaterialStockType).LoadAsync();
                        assignedMaterial.ParseDimensions();
                    }

                    //assign main record markup to item if item markup null
                    if (!ji.CustomMarkup.HasValue)
                    {
                        ji.CustomMarkup = j.MarkupOverride;
                    }

                    foreach (JobItemService jiService in ji.JobItemServices)
                    {
                        Service s = await db.Services.FindAsync(jiService.ServiceId);

                        // Ensure the service is already tracked or fetch it from the database if not
                        var existingService = await db.JobItemServices
                                                      .SingleOrDefaultAsync(si => si.Id == jiService.Id);

                        if (existingService != null)
                        {
                            // Update the properties of the existing entity
                            existingService.ApexOrdinal = jiService.Ordinal;
                            existingService.RequiresQualityCheck = jiService.RequiresQualityCheck;

                            if (s != null)
                            {
                                if (s.SerNo == (int)Service.SerNoSpecialServices.PullMaterial)
                                {
                                    existingService.Notes += $" {assignedMaterial.Title} ({ji.MaterialRunSize})";
                                }
                            }

                            db.Entry(existingService).State = EntityState.Modified;
                        }
                        else
                        {
                            jiService.ApexOrdinal = ji.JobItemServices.Max(x => x.ApexOrdinal) + 1;
                            // If the service is not tracked, add it as a new entity
                            db.JobItemServices.Add(jiService);
                        }

                        if (s != null)
                        {
                            List<JobItemService> PullMaterialServices = ji.JobItemServices.
                                Where(x => x.ServiceId == s.Id && s.SerNo == (int)Service.SerNoSpecialServices.PullMaterial).ToList();

                            foreach (JobItemService jis in PullMaterialServices)
                            {
                                jis.Notes += String.Format(" {0} ( {1} )", assignedMaterial.Title, ji.MaterialRunSize);
                            }
                        }
                    }

                    //existing item properties
                    existingItem.Quantity = ji.Quantity;
                    existingItem.Length = ji.Length;
                    existingItem.Width = ji.Width;
                    existingItem.Description = ji.Description;
                    existingItem.Notes = ji.Notes;
                    existingItem.CustomMarkup = ji.CustomMarkup;
                    existingItem.SelectedSpeed = ji.SelectedSpeed;
                    existingItem.MaterialNotes = ji.MaterialNotes;
                    existingItem.UpValueLocked = ji.UpValueLocked ?? false;
                    existingItem.MaterialRunSize = ji.MaterialRunSize ?? "0x0";
                    existingItem.ArtworkProvided = ji.ArtworkProvided ?? false;
                    existingItem.DesignHours = ji.DesignHours ?? 0m;

                    db.Entry(existingItem).State = EntityState.Modified;

                    try
                    {
                        await db.SaveChangesAsync();
                    }
                    catch (Exception e)
                    {
                    }
                    //press change
                    if (existingItem.Press != ji.Press)
                    {
                        existingItem.Press = ji.Press;
                    }
                    //press defaults
                    if (ji.Press.HasValue)
                    {
                        ji.ParseRunSize();

                        //run twice because inter-dependent calculations (sheets required for scrap sheets)
                        for (int i = 0; i <= 1; i++)
                        {
                            ScrapSheetCalculator ssc = new ScrapSheetCalculator();
                            //calculate scrap sheet amounts
                            ScrapSheetCalculator.ScrapSheets scrapSheets = await ssc.CalculateScrapSheets(db, ji.Press.Value, ji.Id, "Job");

                            int totalScrap = (scrapSheets.VariableScrapAmount + scrapSheets.DefaultScrap + scrapSheets.ColorScrap);

                            if (!(existingItem.UserSheetOverride ?? false))
                            {
                                //refactor?
                                existingItem.VariableScrapSheets = scrapSheets.VariableScrapAmount;
                                existingItem.ScrapSheets = scrapSheets.ColorScrap + scrapSheets.DefaultScrap;
                                existingItem.TotalScrapSheets = existingItem.VariableScrapSheets + existingItem.ScrapSheets;
                                existingItem.MaterialRunSize = ji.MaterialRunSize;
                            }
                            else
                            {
                                totalScrap = existingItem.TotalScrapSheets ?? 0;
                            }

                            // SwapResult swapResult = await EvaluateAndApplySwap(ji, existingItem, assignedMaterial, totalScrap);

                            RowColumnInformation rowColumnCalc = CalculationUtilities.CalculateLayout(
                                await GenerateTrueItemLength(ji.Id, "Job"),
                                await GenerateTrueItemWidth(ji.Id, "Job"),
                                ji.TotalQuantity ?? 1,
                                ji.RunWidth,
                                ji.RunLength,
                                ji.UpValueLocked ?? false,
                                existingItem.UserSheetOverride ?? false,
                                ji.Up ?? 0,
                                existingItem.Sheets ?? 0,
                                assignedMaterial == null ? Material.AvailableStockTypes.Sheet : (Material.AvailableStockTypes)assignedMaterial.StockType,
                                assignedMaterial == null ? 1 : (assignedMaterial.NumberOfParts ?? 1),
                                assignedMaterial == null ? 0m : decimal.Parse(assignedMaterial.GetLength() ?? "0"),
                                assignedMaterial == null ? 0m : decimal.Parse(assignedMaterial.GetWidth() ?? "0"),
                                totalScrap
                            );

                            existingItem.Rows = rowColumnCalc.RowCount;
                            existingItem.Columns = rowColumnCalc.ColumnCount;
                            existingItem.MaxRows = rowColumnCalc.TotalRows;
                            existingItem.MaxColumns = rowColumnCalc.TotalColumns;
                            existingItem.Sheets = rowColumnCalc.TotalSheets;
                            existingItem.Up = rowColumnCalc.UpValue;
                            existingItem.Out = rowColumnCalc.Out;

                            // Zero scrap if less than half a sheet is used on a single sheet
                            // The remaining material can serve as a reprint buffer
                            if (!(existingItem.UserSheetOverride ?? false))
                            {
                                int quantity = ji.TotalQuantity ?? 1;
                                int upValue = rowColumnCalc.UpValue;
                                int sheets = rowColumnCalc.TotalSheets;

                                if (sheets == 1 && upValue > 0 && quantity <= (upValue / 2))
                                {
                                    totalScrap = 0;
                                    existingItem.VariableScrapSheets = 0;
                                    existingItem.ScrapSheets = 0;
                                    existingItem.TotalScrapSheets = 0;
                                }
                            }

                            //existingItem.RunSheets = rowColumnCalc.RunSheets;
                            existingItem.RunSheets = rowColumnCalc.TotalSheets + totalScrap;

                            if (!(existingItem.UserSheetOverride ?? false))
                            {
                                existingItem.BuySheets = (int)Math.Ceiling((decimal)existingItem.RunSheets / (decimal)rowColumnCalc.Out);
                            }
                            db.Entry(existingItem).State = EntityState.Modified;
                            await db.SaveChangesAsync();
                        }

                        if (ji.Material.HasValue)
                        {
                            // Get layout settings for both roll and sheet stock
                            var layoutSettings = await db.ItemLayoutSettings
                                .FirstOrDefaultAsync(ils => ils.JobItemId == existingItem.Id);

                            if (assignedMaterial.StockType == (int)Material.AvailableStockTypes.RollStock)
                            {
                                decimal theLength, theGripper, theTail, xGutter, yGutter;
                                int theQuantity;

                                theLength = Decimal.Parse(existingItem.Length);
                                theQuantity = ji.TotalQuantity ?? 1;

                                if (layoutSettings != null)
                                {
                                    // Use ItemLayoutSettings (authoritative source)
                                    theGripper = layoutSettings.Gripper;
                                    theTail = layoutSettings.Tail;
                                    xGutter = layoutSettings.XGutter;
                                    yGutter = layoutSettings.YGutter;
                                }
                                else
                                {
                                    // Fall back to legacy JobItem fields
                                    theGripper = Decimal.Parse((existingItem.Gripper ?? "0"));
                                    theTail = Decimal.Parse((existingItem.Tail ?? "0"));
                                    xGutter = Decimal.Parse((existingItem.MarginXR ?? "0")); // Legacy X gutter field
                                    yGutter = Decimal.Parse((existingItem.MarginYT ?? "0")); // Legacy Y gutter field
                                }

                                //HOTFIX: calculate number of rows which is quantity / up
                                int rsRowCount = theQuantity / (existingItem.Up ?? 1);
                                //see if there is anything leftover
                                int countOnFinalRow = theQuantity % (existingItem.Up ?? 1);

                                //if leftovers, add one row
                                if (countOnFinalRow > 0)
                                    rsRowCount++;

                                // Use the SAME formula as JavaScript: rowsNeeded * (itemLength + yGutter) - yGutter + gripper + tail
                                rollStockLength = (rsRowCount * (theLength + yGutter)) - yGutter + theGripper + theTail;

                                MastermindLogger.Log(LogLevel.Info, $"Roll stock calculation (matching JavaScript): ({rsRowCount} * ({theLength} + {yGutter})) - {yGutter} + {theGripper} + {theTail} = {rollStockLength}");

                                existingItem.SetRollStockLength(rollStockLength);
                            }
                            else
                            {
                                // Sheet stock - similar calculation but per sheet instead of per roll
                                decimal theLength, theGripper, theTail, xGutter, yGutter;
                                int theQuantity;

                                theLength = Decimal.Parse(existingItem.Length);
                                theQuantity = ji.TotalQuantity ?? 1;

                                if (layoutSettings != null)
                                {
                                    // Use ItemLayoutSettings (authoritative source)
                                    theGripper = layoutSettings.Gripper;
                                    theTail = layoutSettings.Tail;
                                    xGutter = layoutSettings.XGutter;
                                    yGutter = layoutSettings.YGutter;

                                    // Sync back to legacy fields for compatibility
                                    existingItem.Gripper = layoutSettings.Gripper.ToString();
                                    existingItem.Tail = layoutSettings.Tail.ToString();
                                    existingItem.MarginXL = layoutSettings.LeftSideGuide.ToString();
                                    existingItem.MarginXR = layoutSettings.RightSideGuide.ToString(); // Fixed: RightSideGuide -> MarginXR
                                    existingItem.MarginYT = "0"; // TODO: Determine correct top margin logic
                                    existingItem.MarginYB = layoutSettings.YGutter.ToString(); // Y gutter -> MarginYB
                                    existingItem.SideGuides = Math.Max(layoutSettings.LeftSideGuide, layoutSettings.RightSideGuide).ToString();
                                }
                                else
                                {
                                    // Fall back to legacy JobItem fields
                                    theGripper = Decimal.Parse((existingItem.Gripper ?? "0"));
                                    theTail = Decimal.Parse((existingItem.Tail ?? "0"));
                                    xGutter = Decimal.Parse((existingItem.MarginXR ?? "0")); // Legacy X gutter field
                                    yGutter = Decimal.Parse((existingItem.MarginYT ?? "0")); // Legacy Y gutter field
                                }

                                // For sheet stock, calculate items per sheet
                                int itemsPerSheet = existingItem.Up ?? 1;

                                // Calculate sheet count
                                int sheetCount = (int)Math.Ceiling((decimal)theQuantity / itemsPerSheet);

                                // Sheet stock doesn't need MaterialRunSize updates since sheet dimensions are fixed
                                // But we can log the calculation for consistency
                                MastermindLogger.Log(LogLevel.Info, $"Sheet stock calculation - Items per sheet: {itemsPerSheet}, Sheets needed: {sheetCount}, XGutter: {xGutter}, YGutter: {yGutter}, Gripper: {theGripper}, Tail: {theTail}");
                            }
                        }

                        try
                        {
                            foreach (JobItemService jisPrintLoad in existingItem.JobItemServices)
                                await db.Entry(jisPrintLoad).Reference(r => r.Service).LoadAsync();

                            //update print service defaults
                            JobItemService pService = existingItem.JobItemServices.Single(x => x.Service.SerNo == (int)Service.SerNoSpecialServices.Print);
                            pService.HourlyRate = assignedPress.LaborCostPerHour ?? 0m;
                            pService.MakeReady = assignedPress.MakeReady ?? 0m;
                            pService.WashUp = assignedPress.WashUp ?? 0m;

                            db.Entry(pService).State = EntityState.Modified;
                        }
                        catch (Exception)
                        {
                            //create the print service record because one doesnt exist and we need it
                            db.JobItemServices.Add(new JobItemService
                            {
                                ServiceId = 1321,
                                Ordinal = 2,
                                JobItemId = ji.Id,
                                HourlyRate = assignedPress.LaborCostPerHour ?? 0m,
                                MakeReady = assignedPress.MakeReady ?? 0m,
                                WashUp = assignedPress.WashUp ?? 0m,
                                Title = "Print"
                            });

                            await db.SaveChangesAsync();
                        }
                    }
                    else
                    {
                        //there is no press assigned, so delete the QuoteItemService's print service
                        try
                        {
                            JobItemService printJobItemServiceToDelete = await db.JobItemServices.SingleAsync(x => x.JobItemId == ji.Id && x.Service.SerNo == (int)Service.SerNoSpecialServices.Print);
                            db.JobItemServices.Remove(printJobItemServiceToDelete);
                        }
                        catch (Exception)
                        {
                            //there is no print service, which is good because we werre going to remove it
                        }
                    }

                    //more item properties
                    existingItem.PressSpeedNumerator = ji.PressSpeedNumerator;
                    existingItem.PressSpeedDenominator = ji.PressSpeedDenominator;
                    existingItem.QuantityBy = ji.QuantityBy;
                    existingItem.TotalQuantity = ji.TotalQuantity;
                    existingItem.SelectedSpeed = ji.SelectedSpeed;
                    existingItem.InternalNotes = ji.InternalNotes;

                    existingItem.Material = ji.Material;
                    existingItem.MixGrain = ji.MixGrain ?? false;
                    existingItem.Material = assignedMaterial.Id == 0 ? (int?)null : assignedMaterial.Id;

                    db.Entry(existingItem).State = EntityState.Modified;

                    try
                    {
                        //print service hotfix: get ordinal of pull material service. set ordinal of print service to pull material + 1. Get all services with ordinal > pull material and increment by 1
                        JobItemService printJobItemService = existingItem.JobItemServices.First(x => x.Title == "Print");
                        JobItemService pullMaterialItemService = existingItem.JobItemServices.First(x => x.Title == "Pull Material");
                        printJobItemService.ApexOrdinal = pullMaterialItemService.ApexOrdinal + 1;
                        db.Entry(printJobItemService).State = EntityState.Modified;

                        foreach (JobItemService jis in existingItem.JobItemServices.Where(x => x.Title != "Print" && x.ApexOrdinal > pullMaterialItemService.ApexOrdinal))
                        {
                            jis.ApexOrdinal++;
                            db.Entry(jis).State = EntityState.Modified;
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }

                await db.SaveChangesAsync();

                List<int> jobItemIds = items.Select(x => x.Id).ToList();
                List<JobItemService> services = await db.JobItemServices.Where(x => jobItemIds.Contains(x.JobItemId)).Include(i => i.JobItem).ToListAsync();

                foreach (JobItemService jis in services)
                {
                    await db.Entry(jis).Reference(r => r.Service).LoadAsync();
                    await db.Entry(jis.Service).Reference(r => r.ServiceCategory).LoadAsync();
                    await db.Entry(jis.Service.ServiceCategory).Reference(r => r.ServiceType1).LoadAsync();
                }

                await db.SaveChangesAsync();

                try
                {
                    List<JobItem> jItems = await db.JobItems.Where(x => x.Job == j.Id).ToListAsync();

                    foreach (JobItem ji in jItems)
                        await db.Entry(ji).Collection(c => c.JobItemServices).LoadAsync();

                    List<JobItemService> allServices = new List<JobItemService>();

                    foreach (JobItem ji in jItems)
                    {
                        List<JobItemService> theseServices = ji.JobItemServices.ToList();
                        allServices = allServices.Concat(theseServices).ToList();
                    }

                    if (TempNotes != null)
                    {
                        //update temp notes
                        foreach (TempNote tn in TempNotes)
                        {
                            try
                            {
                                JobItemService jisTemp = allServices.Single(x => x.ServiceId == tn.ServiceId && x.JobItem.Ordinal == tn.RowOrdinal);
                                jisTemp.Notes = tn.Notes;
                                db.Entry(jisTemp).State = EntityState.Modified;
                            }
                            catch (Exception)
                            {
                                //not found, which is ok
                            }
                        }
                    }

                    await db.SaveChangesAsync();
                }
                catch (Exception)
                {
                }

                List<JobItem> jobItems = await db.JobItems.Where(x => x.Job == j.Id).OrderBy(x => x.Ordinal).ToListAsync();
                List<JobOutsourcedItem> jobOutsourcedItems = await db.JobOutsourcedItems.Where(x => x.Job == j.Id).OrderBy(x => x.Ordinal).ToListAsync();

                List<GenericItemOrganizer> itemsOrganized = jobItems.Select(x => new GenericItemOrganizer
                {
                    Id = x.Id,
                    Ordinal = x.Ordinal ?? 0,
                    RowType = "Standard"
                }).ToList();
                itemsOrganized = itemsOrganized.Concat(
                    jobOutsourcedItems.Select(x => new GenericItemOrganizer
                    {
                        Id = x.Id,
                        Ordinal = x.Ordinal ?? 0,
                        RowType = "Outsourced"
                    }).ToList()).ToList();

                itemsOrganized = itemsOrganized.OrderBy(x => x.Ordinal).ToList();

                int ordinalTracker = 1;

                foreach (GenericItemOrganizer gio in itemsOrganized)
                {
                    switch (gio.RowType)
                    {
                        case "Standard":
                            JobItem tItem = jobItems.Single(x => x.Id == gio.Id);
                            tItem.Ordinal = ordinalTracker++;
                            db.Entry(tItem).State = EntityState.Modified;
                            break;

                        case "Outsourced":
                            JobOutsourcedItem toItem = jobOutsourcedItems.Single(x => x.Id == gio.Id);
                            toItem.Ordinal = ordinalTracker++;
                            db.Entry(toItem).State = EntityState.Modified;
                            break;

                        default:
                            break;
                    }
                }

                await db.SaveChangesAsync();
            }

            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(
                    await db.JobItems.Where(x => x.Job == j.Id).Select(x => new { x.Id, x.Up }).ToListAsync(),
                    new JsonSerializerSettings
                    {
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                    }
                ),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        [HttpPost]
        public async Task<ActionResult> UpdateQuote(Quote q, QuoteItem[] items, QuoteOutsourcedItem[] outsourcedItems, TempNote[] tempNotes)
        {
            // [SMOKE-TEST] Instrumentation for Step 2 N+1 + save-loop fixes — remove once verified.
            var sw_total = System.Diagnostics.Stopwatch.StartNew();
            var sw_stage = System.Diagnostics.Stopwatch.StartNew();
            MastermindLogger.Log(LogLevel.Info, $"[UpdateQuote] === START quoteId={q.Id} items={items?.Length ?? 0} outsourced={outsourcedItems?.Length ?? 0} tempNotes={tempNotes?.Length ?? 0} ===");

            List<Task> inventoryTasks = new List<Task>();

            decimal rollStockLength = 0m;

            Quote existing = await db.Quotes.FindAsync(q.Id);

            if (existing == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.NotFound, "Cannot find Quote with id " + q.Id);

            // Load existing QuoteItems with their full service nav tree in one query.
            // Before: collection load + per-item Collection(QuoteItemServices) load = 1 + N round-trips,
            //         and a separate post-loop block (~line 8090) re-loaded Service/ServiceCategory/ServiceType1 per service.
            // After:  one SQL with JOINs covers it all; the per-service Reference loads later are deletable.
            await db.Entry(existing)
                .Collection(c => c.QuoteItems)
                .Query()
                .Include(qi => qi.QuoteItemServices.Select(qis => qis.Service.ServiceCategory.ServiceType1))
                .LoadAsync();
            await db.Entry(existing).Collection(c => c.QuoteOutsourcedItems).LoadAsync();

            existing.Title = q.Title;
            existing.Description = q.Description;

            // Handle client assignment/change - copy SQFT price from client
            if (existing.Client != q.Client)
            {
                if (q.Client.HasValue)
                {
                    // Client assigned or changed - copy SQFT price from new client
                    Client newClient = await db.Clients.FindAsync(q.Client.Value);
                    if (newClient != null)
                    {
                        // Copy client's SQFT price (or null if client doesn't have one)
                        existing.SqftPriceOverride = newClient.SqftPrice;
                    }
                }
                else
                {
                    // Client removed - clear SQFT price override
                    existing.SqftPriceOverride = null;
                }
            }

            existing.Client = q.Client;
            existing.Contact = q.Contact;
            existing.Notes = q.Notes;
            existing.JobNumber = q.JobNumber;
            existing.InternalNotes = q.InternalNotes;
            existing.IsWarehouseJob = q.IsWarehouseJob ?? false;
            existing.IsEvent = q.IsEvent ?? false;
            existing.MarkupOverride = q.MarkupOverride;
            existing.IsPickup = (q.IsPickup ?? false);
            existing.ShippingMethod = q.ShippingMethod;
            existing.BillingNotes = q.BillingNotes;
            existing.ReleaseToClientView = q.ReleaseToClientView ?? false;
            existing.ClientPONumber = q.ClientPONumber;

            db.Entry(existing).State = EntityState.Modified;

            await db.SaveChangesAsync();

            // [SMOKE-TEST]
            MastermindLogger.Log(LogLevel.Info, $"[UpdateQuote] T1 LoadPhase+QuoteSave {sw_stage.ElapsedMilliseconds}ms | existingItems={existing.QuoteItems.Count} existingServices={existing.QuoteItems.Sum(x => x.QuoteItemServices.Count)}");
            sw_stage.Restart();

            // Pre-fetch every entity referenced inside the per-item loop in three bulk queries.
            // Before: per-item Press.Find + Material.Find + Material.Reference(Stock) + per-service Service.FindAsync
            //         + per-service QuoteItemServices.SingleOrDefaultAsync + per-item ItemLayoutSettings.FirstOrDefaultAsync
            //         = ~5 queries per item + ~2 queries per service.
            // After:  5 dictionaries built before the loop; loop body is dictionary lookups only.
            List<int> pressIds = items?.Where(x => x.Press.HasValue).Select(x => x.Press.Value).Distinct().ToList() ?? new List<int>();
            List<int> materialIds = items?.Where(x => x.Material.HasValue).Select(x => x.Material.Value).Distinct().ToList() ?? new List<int>();
            List<int> serviceIds = items?.SelectMany(x => x.QuoteItemServices ?? new List<QuoteItemService>())
                                         .Select(s => s.OriginalService)
                                         .Distinct().ToList() ?? new List<int>();
            List<int> existingItemIds = existing.QuoteItems.Select(x => x.Id).ToList();

            var pressDict = pressIds.Count > 0
                ? await db.Presses.Where(p => pressIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id)
                : new Dictionary<int, Press>();
            var materialDict = materialIds.Count > 0
                ? await db.Materials.Where(m => materialIds.Contains(m.Id)).Include(m => m.MaterialStockType).ToDictionaryAsync(m => m.Id)
                : new Dictionary<int, Material>();
            var layoutSettingsDict = existingItemIds.Count > 0
                ? await db.ItemLayoutSettings.Where(ils => ils.QuoteItemId.HasValue && existingItemIds.Contains(ils.QuoteItemId.Value)).ToDictionaryAsync(ils => ils.QuoteItemId.Value)
                : new Dictionary<int, ItemLayoutSetting>();
            var serviceDict = serviceIds.Count > 0
                ? await db.Services.Where(s => serviceIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id)
                : new Dictionary<int, Service>();
            // existing QuoteItemServices were loaded with the Include chain at the top — flatten by Id.
            var existingServicesDict = existing.QuoteItems
                .SelectMany(qi => qi.QuoteItemServices)
                .ToDictionary(qis => qis.Id);

            // [SMOKE-TEST]
            MastermindLogger.Log(LogLevel.Info, $"[UpdateQuote] T2 PreFetchDicts {sw_stage.ElapsedMilliseconds}ms | press={pressDict.Count} material={materialDict.Count} layout={layoutSettingsDict.Count} service={serviceDict.Count} existingServices={existingServicesDict.Count}");
            sw_stage.Restart();

            if (items != null)
            {
                foreach (QuoteItem qi in items.Where(x => x.SubitemOf == null).ToList())
                {
                    QuoteItem existingItem = existing.QuoteItems.Single(x => x.Id == qi.Id);

                    List<QuoteItem> subItems = existing.QuoteItems.Where(x => x.SubitemOf == qi.Id).ToList();

                    // Dictionary lookups — no DB calls (pre-fetched above).
                    Press assignedPress = qi.Press.HasValue && pressDict.TryGetValue(qi.Press.Value, out var _p) ? _p : null;
                    Material assignedMaterial = qi.Material.HasValue && materialDict.TryGetValue(qi.Material.Value, out var _m) ? _m : null;

                    //assign default of 1 for material dimensions in case a material was not chosen
                    if (assignedMaterial == null)
                        assignedMaterial = new Material { Size = "1x1", ParsedDimensions = true, ParsedLength = "1", ParsedWidth = "1", CostType = (int)Material.MaterialCostTypes.Piece, StockType = (int)Material.AvailableStockTypes.Sheet };
                    else
                    {
                        // MaterialStockType already loaded via Include in materialDict.
                        assignedMaterial.ParseDimensions();
                    }

                    //assign main record markup to item if markup is null
                    if (!qi.CustomMarkup.HasValue)
                    {
                        qi.CustomMarkup = q.MarkupOverride;
                    }

                    foreach (QuoteItemService qiService in qi.QuoteItemServices)
                    {
                        // Dictionary lookups — no DB calls (pre-fetched above).
                        Service s = serviceDict.TryGetValue(qiService.OriginalService, out var _s) ? _s : null;
                        existingServicesDict.TryGetValue(qiService.Id, out var existingService);

                        if (existingService != null)
                        {
                            // Update the properties of the existing entity
                            existingService.ApexOrdinal = qiService.Ordinal;

                            if (s != null)
                            {
                                if (s.SerNo == (int)Service.SerNoSpecialServices.PullMaterial)
                                {
                                    existingService.Notes += $" {assignedMaterial.Title} ({qi.MaterialRunSize})";
                                }
                            }

                            db.Entry(existingService).State = EntityState.Modified;
                        }
                        // Note: if existingService is null, the QIS was deleted by another session.
                        // Do not re-insert it — the payload OriginalService value is a QIS ID, not a Service ID.

                        if (s != null)
                        {
                            List<QuoteItemService> PullMaterialServices = qi.QuoteItemServices
                                .Where(x => x.OriginalService == s.Id && s.SerNo == (int)Service.SerNoSpecialServices.PullMaterial).ToList();

                            foreach (QuoteItemService qis in PullMaterialServices)
                            {
                                qis.Notes += String.Format(" {0} ( {1} )", assignedMaterial.Title, qi.MaterialRunSize);
                            }
                        }
                    }

                    existingItem.Quantity = qi.Quantity;
                    existingItem.Length = qi.Length;
                    existingItem.Width = qi.Width;
                    existingItem.Description = qi.Description;
                    existingItem.Notes = qi.Notes;
                    existingItem.CustomMarkup = qi.CustomMarkup;
                    existingItem.SelectedSpeed = qi.SelectedSpeed;
                    existingItem.MaterialNotes = qi.MaterialNotes;
                    existingItem.UpValueLocked = qi.UpValueLocked ?? false;
                    //added to account for material run size being null
                    existingItem.MaterialRunSize = qi.MaterialRunSize ?? "0x0";
                    existingItem.ArtworkProvided = qi.ArtworkProvided ?? false;
                    existingItem.DesignHours = qi.DesignHours ?? 0m;

                    db.Entry(existingItem).State = EntityState.Modified;

                    //save here (so we can set as modified in loop)
                    // KEPT INTENTIONALLY: provides per-item transactional isolation (a failure on item N doesn't
                    // roll back items 1..N-1) and forces EF relationship fix-up before the print-service hotfix
                    // block below uses navigation properties of newly-added QuoteItemServices.
                    await db.SaveChangesAsync();

                    if (qi.Press != existingItem.Press)
                    {
                        existingItem.Press = qi.Press;
                    }
                    if (qi.Press.HasValue)
                    {
                        qi.ParseRunSize();

                        //do it twice because we have inter-dependent calculations
                        for (int i = 0; i <= 1; i++)
                        {
                            ScrapSheetCalculator ssc = new ScrapSheetCalculator();
                            //calculate scrap sheet amounts
                            ScrapSheetCalculator.ScrapSheets scrapSheets = await ssc.CalculateScrapSheets(db, qi.Press.Value, qi.Id, "QUOTE");

                            int totalScrap = (scrapSheets.VariableScrapAmount + scrapSheets.DefaultScrap + scrapSheets.ColorScrap);

                            if (!(existingItem.UserSheetOverride ?? false))
                            {
                                //refactor?
                                existingItem.VariableScrapSheets = scrapSheets.VariableScrapAmount;
                                existingItem.ScrapSheets = scrapSheets.ColorScrap + scrapSheets.DefaultScrap;
                                existingItem.TotalScrapSheets = existingItem.VariableScrapSheets + existingItem.ScrapSheets;
                                existingItem.MaterialRunSize = qi.MaterialRunSize;
                            }
                            else
                            {
                                totalScrap = existingItem.TotalScrapSheets ?? 0;
                            }

                            //SwapResult swapResult = await EvaluateAndApplySwap(qi, existingItem, assignedMaterial, totalScrap);

                            RowColumnInformation rowColumnCalc = CalculationUtilities.CalculateLayout(
                                await GenerateTrueItemLength(qi.Id, "Quote"), //ok
                                await GenerateTrueItemWidth(qi.Id, "Quote"), //ok
                                qi.TotalQuantity ?? 1, //ok
                                qi.RunWidth, //non-nullable:ok
                                qi.RunLength, //non-nullable:ok
                                qi.UpValueLocked ?? false, //ok
                                existingItem.UserSheetOverride ?? false, //ok
                                qi.Up ?? 0, //ok
                                existingItem.Sheets ?? 0, //ok //use existingItem because sheet count is updated dynamically if overridden
                                assignedMaterial == null ? Material.AvailableStockTypes.Sheet : (Material.AvailableStockTypes)assignedMaterial.StockType,
                                assignedMaterial == null ? 1 : (assignedMaterial.NumberOfParts ?? 1),
                                assignedMaterial == null ? 0m : decimal.Parse(assignedMaterial.GetLength() ?? "0"),
                                assignedMaterial == null ? 0m : decimal.Parse(assignedMaterial.GetWidth() ?? "0"),
                                totalScrap
                                );

                            existingItem.Rows = rowColumnCalc.RowCount;
                            existingItem.Columns = rowColumnCalc.ColumnCount;
                            existingItem.MaxRows = rowColumnCalc.TotalRows;
                            existingItem.MaxColumns = rowColumnCalc.TotalColumns;
                            existingItem.Sheets = rowColumnCalc.TotalSheets;
                            existingItem.Up = rowColumnCalc.UpValue;
                            existingItem.Out = rowColumnCalc.Out;

                            // Zero scrap if less than half a sheet is used on a single sheet
                            // The remaining material can serve as a reprint buffer
                            if (!(existingItem.UserSheetOverride ?? false))
                            {
                                int quantity = qi.TotalQuantity ?? 1;
                                int upValue = rowColumnCalc.UpValue;
                                int sheets = rowColumnCalc.TotalSheets;

                                if (sheets == 1 && upValue > 0 && quantity <= (upValue / 2))
                                {
                                    totalScrap = 0;
                                    existingItem.VariableScrapSheets = 0;
                                    existingItem.ScrapSheets = 0;
                                    existingItem.TotalScrapSheets = 0;
                                }
                            }

                            //existingItem.RunSheets = rowColumnCalc.RunSheets;
                            existingItem.RunSheets = (rowColumnCalc.TotalSheets + totalScrap);

                            if (!(existingItem.UserSheetOverride ?? false))
                            {
                                existingItem.BuySheets = (int)Math.Ceiling((decimal)existingItem.RunSheets / (decimal)rowColumnCalc.Out);
                            }
                            db.Entry(existingItem).State = EntityState.Modified;
                            // Inner SaveChangesAsync removed — accumulated state changes flush at end of foreach.
                            // (was: await db.SaveChangesAsync(); inside double-pass = up to 2N saves)
                        }

                        if (qi.Material.HasValue)
                        {
                            // Dictionary lookup — no DB call (pre-fetched above).
                            layoutSettingsDict.TryGetValue(existingItem.Id, out var layoutSettings);

                            if (assignedMaterial.StockType == (int)Material.AvailableStockTypes.RollStock)
                            {
                                decimal theLength, theGripper, theTail, xGutter, yGutter;
                                int theQuantity;

                                theLength = Decimal.Parse(existingItem.Length);
                                theQuantity = qi.TotalQuantity ?? 1;

                                if (layoutSettings != null)
                                {
                                    // Use ItemLayoutSettings (authoritative source)
                                    theGripper = layoutSettings.Gripper;
                                    theTail = layoutSettings.Tail;
                                    xGutter = layoutSettings.XGutter;
                                    yGutter = layoutSettings.YGutter;
                                }
                                else
                                {
                                    // Fall back to legacy QuoteItem fields
                                    theGripper = Decimal.Parse((existingItem.Gripper ?? "0"));
                                    theTail = Decimal.Parse((existingItem.Tail ?? "0"));
                                    xGutter = Decimal.Parse((existingItem.MarginXR ?? "0")); // Legacy X gutter field
                                    yGutter = Decimal.Parse((existingItem.MarginYT ?? "0")); // Legacy Y gutter field
                                }

                                //HOTFIX: calculate number of rows which is quantity / up
                                int rsRowCount = theQuantity / (existingItem.Up ?? 1);
                                //see if there is anything leftover
                                int countOnFinalRow = theQuantity % (existingItem.Up ?? 1);

                                //if leftovers, add one row
                                if (countOnFinalRow > 0)
                                    rsRowCount++;

                                // Use the SAME formula as JavaScript: rowsNeeded * (itemLength + yGutter) - yGutter + gripper + tail
                                rollStockLength = (rsRowCount * (theLength + yGutter)) - yGutter + theGripper + theTail;

                                MastermindLogger.Log(LogLevel.Info, $"Quote roll stock calculation (matching JavaScript): ({rsRowCount} * ({theLength} + {yGutter})) - {yGutter} + {theGripper} + {theTail} = {rollStockLength}");

                                existingItem.SetRollStockLength(rollStockLength);
                            }
                            else
                            {
                                // Sheet stock - similar calculation but per sheet instead of per roll
                                decimal theLength, theGripper, theTail, xGutter, yGutter;
                                int theQuantity;

                                theLength = Decimal.Parse(existingItem.Length);
                                theQuantity = qi.TotalQuantity ?? 1;

                                if (layoutSettings != null)
                                {
                                    // Use ItemLayoutSettings (authoritative source)
                                    theGripper = layoutSettings.Gripper;
                                    theTail = layoutSettings.Tail;
                                    xGutter = layoutSettings.XGutter;
                                    yGutter = layoutSettings.YGutter;

                                    // Sync back to legacy fields for compatibility
                                    existingItem.Gripper = layoutSettings.Gripper.ToString();
                                    existingItem.Tail = layoutSettings.Tail.ToString();
                                    existingItem.MarginXL = layoutSettings.LeftSideGuide.ToString();
                                    existingItem.MarginXR = layoutSettings.RightSideGuide.ToString(); // Fixed: RightSideGuide -> MarginXR
                                    existingItem.MarginYT = "0"; // TODO: Determine correct top margin logic
                                    existingItem.MarginYB = layoutSettings.YGutter.ToString(); // Y gutter -> MarginYB
                                    existingItem.SideGuides = Math.Max(layoutSettings.LeftSideGuide, layoutSettings.RightSideGuide).ToString();
                                }
                                else
                                {
                                    // Fall back to legacy QuoteItem fields
                                    theGripper = Decimal.Parse((existingItem.Gripper ?? "0"));
                                    theTail = Decimal.Parse((existingItem.Tail ?? "0"));
                                    xGutter = Decimal.Parse((existingItem.MarginXR ?? "0")); // Legacy X gutter field
                                    yGutter = Decimal.Parse((existingItem.MarginYT ?? "0")); // Legacy Y gutter field
                                }

                                // For sheet stock, calculate items per sheet
                                int itemsPerSheet = existingItem.Up ?? 1;

                                // Calculate sheet count
                                int sheetCount = (int)Math.Ceiling((decimal)theQuantity / itemsPerSheet);

                                // Sheet stock doesn't need MaterialRunSize updates since sheet dimensions are fixed
                                // But we can log the calculation for consistency
                                MastermindLogger.Log(LogLevel.Info, $"Quote sheet stock calculation - Items per sheet: {itemsPerSheet}, Sheets needed: {sheetCount}, XGutter: {xGutter}, YGutter: {yGutter}, Gripper: {theGripper}, Tail: {theTail}");
                            }
                        }

                        try
                        {
                            // Service nav already loaded for all existing services via the Include chain at the top of UpdateQuote.
                            // (was: foreach Reference(Service).LoadAsync() = N×P round-trips per Update)

                            //update print service defaults
                            //when the press is changed, the print service is updated here
                            QuoteItemService pService = existingItem.QuoteItemServices.Single(x => x.Service.SerNo == (int)Service.SerNoSpecialServices.Print);
                            pService.HourlyRate = assignedPress.LaborCostPerHour ?? 0m;
                            pService.MakeReady = assignedPress.MakeReady ?? 0m;
                            pService.WashUp = assignedPress.WashUp ?? 0m;

                            db.Entry(pService).State = EntityState.Modified;
                        }
                        catch (Exception)
                        {
                            //create the print service record because one doesnt exist and we need it
                            db.QuoteItemServices.Add(new QuoteItemService
                            {
                                OriginalService = 1321,
                                Ordinal = 2,
                                QuoteItemId = qi.Id,
                                HourlyRate = assignedPress.LaborCostPerHour ?? 0m,
                                MakeReady = assignedPress.MakeReady ?? 0m,
                                WashUp = assignedPress.WashUp ?? 0m,
                                Title = "Print"
                            });

                            await db.SaveChangesAsync();
                        }
                    }
                    else
                    {
                        //there is no press assigned, so delete the QuoteItemService's print service
                        try
                        {
                            QuoteItemService printQuoteItemServiceToDelete = await db.QuoteItemServices.SingleAsync(x => x.QuoteItemId == qi.Id && x.Service.SerNo == (int)Service.SerNoSpecialServices.Print);
                            db.QuoteItemServices.Remove(printQuoteItemServiceToDelete);
                        }
                        catch (Exception)
                        {
                            //there is no print service, which is good because we werre going to remove it
                        }
                    }

                    //TESTING THE REMOVAL OF THESE PROPERTIES FROM THE MODEL: MIGHT NOT BE NECESSARY AND MIGHT FIX SOME ISSUES
                    existingItem.PressSpeedNumerator = qi.PressSpeedNumerator;
                    existingItem.PressSpeedDenominator = qi.PressSpeedDenominator;
                    existingItem.QuantityBy = qi.QuantityBy;
                    existingItem.TotalQuantity = qi.TotalQuantity;
                    existingItem.SelectedSpeed = qi.SelectedSpeed;
                    existingItem.InternalNotes = qi.InternalNotes;

                    existingItem.Material = qi.Material;
                    existingItem.MixGrain = qi.MixGrain ?? false;
                    existingItem.Material = assignedMaterial.Id == 0 ? (int?)null : assignedMaterial.Id;

                    db.Entry(existingItem).State = EntityState.Modified;

                    try
                    {
                        //print service hotfix: get ordinal of pull material service. set ordinal of print service to pull material + 1. Get all services with ordinal > pull material and increment by 1
                        QuoteItemService printQuoteItemService = existingItem.QuoteItemServices.First(x => x.Title == "Print");
                        QuoteItemService pullMaterialItemService = existingItem.QuoteItemServices.First(x => x.Title == "Pull Material");
                        printQuoteItemService.ApexOrdinal = pullMaterialItemService.ApexOrdinal + 1;
                        db.Entry(printQuoteItemService).State = EntityState.Modified;

                        foreach (QuoteItemService qis in existingItem.QuoteItemServices.Where(x => x.Title != "Print" && x.ApexOrdinal > pullMaterialItemService.ApexOrdinal))
                        {
                            qis.ApexOrdinal++;
                            db.Entry(qis).State = EntityState.Modified;
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }

                await db.SaveChangesAsync();

                // [SMOKE-TEST]
                MastermindLogger.Log(LogLevel.Info, $"[UpdateQuote] T3 ItemForeach+Save {sw_stage.ElapsedMilliseconds}ms");
                sw_stage.Restart();

                // Removed unused load block: previously loaded `List<QuoteItemService> services` with 3
                // per-service Reference(Service/ServiceCategory/ServiceType1) loads, but `services` was
                // never read. Net: 1 + 3*P queries deleted with no behavior change. Same for the trailing
                // SaveChangesAsync that was marked //not needed.

                try
                {
                    // Re-fetch QuoteItems for tempNotes lookup. Use one Include instead of N round-trips.
                    // (was: ToListAsync + foreach Collection(QuoteItemServices).LoadAsync = 1 + N queries)
                    List<QuoteItem> qItems = await db.QuoteItems
                        .Where(x => x.Quote == q.Id)
                        .Include(qi => qi.QuoteItemServices)
                        .ToListAsync();

                    List<QuoteItemService> allServices = new List<QuoteItemService>();

                    foreach (QuoteItem qi in qItems)
                    {
                        List<QuoteItemService> theseServices = qi.QuoteItemServices.ToList();
                        allServices = allServices.Concat(theseServices).ToList();
                    }

                    if (tempNotes != null)
                    {
                        //update temp notes
                        foreach (TempNote tn in tempNotes)
                        {
                            try
                            {
                                QuoteItemService qisTemp = allServices.Single(x => x.OriginalService == tn.ServiceId && x.QuoteItem.Ordinal == tn.RowOrdinal);
                                qisTemp.Notes = tn.Notes;
                                db.Entry(qisTemp).State = EntityState.Modified;
                            }
                            catch (Exception)
                            {
                                //not found, which is ok
                            }
                        }
                    }

                    await db.SaveChangesAsync();
                }
                catch (Exception)
                {
                }

                // [SMOKE-TEST]
                MastermindLogger.Log(LogLevel.Info, $"[UpdateQuote] T4 TempNotesBlock {sw_stage.ElapsedMilliseconds}ms");
                sw_stage.Restart();

                List<QuoteItem> quoteItems = await db.QuoteItems.Where(x => x.Quote == q.Id).OrderBy(x => x.Ordinal).ToListAsync();
                List<QuoteOutsourcedItem> quoteOutsourcedItems = await db.QuoteOutsourcedItems.Where(x => x.Quote == q.Id).OrderBy(x => x.Ordinal).ToListAsync();

                List<GenericItemOrganizer> itemsOrganized = quoteItems.Select(x => new GenericItemOrganizer
                {
                    Id = x.Id,
                    Ordinal = x.Ordinal ?? 0,
                    RowType = "Standard"
                }).ToList();
                itemsOrganized = itemsOrganized.Concat(
                    quoteOutsourcedItems.Select(x => new GenericItemOrganizer
                    {
                        Id = x.Id,
                        Ordinal = x.Ordinal ?? 0,
                        RowType = "Outsourced"
                    }).ToList()).ToList();

                itemsOrganized = itemsOrganized.OrderBy(x => x.Ordinal).ToList();

                int ordinalTracker = 1;

                foreach (GenericItemOrganizer gio in itemsOrganized)
                {
                    switch (gio.RowType)
                    {
                        case "Standard":
                            QuoteItem tItem = quoteItems.Single(x => x.Id == gio.Id);
                            tItem.Ordinal = ordinalTracker++;
                            db.Entry(tItem).State = EntityState.Modified;
                            break;

                        case "Outsourced":
                            QuoteOutsourcedItem toItem = quoteOutsourcedItems.Single(x => x.Id == gio.Id);
                            toItem.Ordinal = ordinalTracker++;
                            db.Entry(toItem).State = EntityState.Modified;
                            break;

                        default:
                            break;
                    }
                }

                await db.SaveChangesAsync();
            }

            // [SMOKE-TEST]
            MastermindLogger.Log(LogLevel.Info, $"[UpdateQuote] T5 OrdinalSync+FinalSave {sw_stage.ElapsedMilliseconds}ms");
            MastermindLogger.Log(LogLevel.Info, $"[UpdateQuote] === END quoteId={q.Id} totalElapsed={sw_total.ElapsedMilliseconds}ms ===");

            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(
                            await db.QuoteItems.Where(x => x.Quote == q.Id).Select(x => new { x.Id, x.Up }).ToListAsync(),
                            new JsonSerializerSettings
                            {
                                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                            }
                        ),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        [HttpGet]
        public async Task<JsonResult> GetJobQuoteItems(string jobNumber)
        {
            try
            {
                if (string.IsNullOrEmpty(jobNumber))
                    return Json(new { success = false, message = "Job number is required" }, JsonRequestBehavior.AllowGet);

                // Try to find as a job first
                var job = await db.Jobs.FirstOrDefaultAsync(x => x.JobNumber == jobNumber);
                if (job != null)
                {
                    var jobItems = await db.JobItems
                        .Where(x => x.Job == job.Id && x.Deleted == null)
                        .Select(x => new
                        {
                            Id = x.Id,
                            Type = "Job",
                            Title = x.Title,
                            Description = x.Description,
                            Quantity = x.Quantity,
                            MaterialCost = x.MaterialCost,
                            MaterialName = x.Material1.Title,
                            Width = x.Width,
                            Length = x.Length,
                            Sheets = x.Sheets
                        })
                        .ToListAsync();

                    var allItems = jobItems.ToList();

                    return Json(new
                    {
                        success = true,
                        recordType = "Job",
                        recordNumber = jobNumber,
                        items = allItems
                    }, JsonRequestBehavior.AllowGet);
                }

                // Try to find as a quote
                var quote = await db.Quotes.FirstOrDefaultAsync(x => x.JobNumber == jobNumber);
                if (quote != null)
                {
                    var quoteItems = await db.QuoteItems
                        .Where(x => x.Quote == quote.Id && x.Deleted == null)
                        .Select(x => new
                        {
                            Id = x.Id,
                            Type = "Quote",
                            Title = x.Title,
                            Description = x.Description,
                            Quantity = x.Quantity,
                            MaterialCost = x.MaterialCost,
                            MaterialName = x.Material1.Title,
                            Width = x.Width,
                            Length = x.Length,
                            Sheets = x.Sheets
                        })
                        .ToListAsync();

                    var allItems = quoteItems.ToList();

                    return Json(new
                    {
                        success = true,
                        recordType = "Quote",
                        recordNumber = jobNumber,
                        items = allItems
                    }, JsonRequestBehavior.AllowGet);
                }

                return Json(new { success = false, message = "Job or Quote not found with number: " + jobNumber }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error retrieving items: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public async Task<JsonResult> ImportSelectedItems(int targetId, string targetType, int[] selectedItemIds, string sourceType, bool copyTimingRecords = false)
        {
            try
            {
                if (selectedItemIds == null || selectedItemIds.Length == 0)
                    return Json(new { success = false, message = "No items selected for import" });

                var importedCount = 0;
                var newItemIds = new List<int>();
                string storagePath = @"C:\Storage\mastermind_job_docs";

                foreach (var itemId in selectedItemIds)
                {
                    int? newItemId = null;

                    if (sourceType == "Job" && targetType == "Job")
                    {
                        // Job to Job - copy all item data and related entities
                        var sourceItem = await db.JobItems
                            .Include(x => x.JobItemServices)
                            .Include(x => x.JobItemFinishingMaterials)
                            .Include(x => x.JobDocuments)
                            .FirstOrDefaultAsync(x => x.Id == itemId);

                        if (sourceItem != null)
                        {
                            var maxOrdinal = await db.JobItems.Where(x => x.Job == targetId).MaxAsync(x => (int?)x.Ordinal) ?? 0;

                            var newItem = new JobItem
                            {
                                Job = targetId,
                                Title = sourceItem.Title,
                                Description = sourceItem.Description,
                                Quantity = sourceItem.Quantity,
                                Notes = sourceItem.Notes,
                                Deleted = null,
                                Sheets = sourceItem.Sheets,
                                SheetsUnit = sourceItem.SheetsUnit,
                                Width = sourceItem.Width,
                                Length = sourceItem.Length,
                                Material = sourceItem.Material,
                                LayoutString = sourceItem.LayoutString,
                                MarginXL = sourceItem.MarginXL,
                                MarginYT = sourceItem.MarginYT,
                                MaterialRunSize = sourceItem.MaterialRunSize,
                                SideGuides = sourceItem.SideGuides,
                                Gripper = sourceItem.Gripper,
                                Tail = sourceItem.Tail,
                                Up = sourceItem.Up,
                                PressSpeedNumerator = sourceItem.PressSpeedNumerator,
                                PressSpeedDenominator = sourceItem.PressSpeedDenominator,
                                Press = sourceItem.Press,
                                PressColorProfileFront = sourceItem.PressColorProfileFront,
                                PressColorCountFront = sourceItem.PressColorCountFront,
                                PressColorNotesFront = sourceItem.PressColorNotesFront,
                                PressColorProfileBack = sourceItem.PressColorProfileBack,
                                PressColorCountBack = sourceItem.PressColorCountBack,
                                PressColorNotesBack = sourceItem.PressColorNotesBack,
                                Ordinal = maxOrdinal + 1,
                                Columns = sourceItem.Columns,
                                Rows = sourceItem.Rows,
                                MaxRows = sourceItem.MaxRows,
                                MaxColumns = sourceItem.MaxColumns,
                                Swap = sourceItem.Swap,
                                MixGrain = sourceItem.MixGrain,
                                SelectedSpeed = sourceItem.SelectedSpeed,
                                QuantityBy = sourceItem.QuantityBy,
                                TotalQuantity = sourceItem.TotalQuantity,
                                MaterialCost = sourceItem.MaterialCost,
                                CuttingConfiguration = sourceItem.CuttingConfiguration,
                                MarginXR = sourceItem.MarginXR,
                                MarginYB = sourceItem.MarginYB,
                                ScrapSheets = sourceItem.ScrapSheets,
                                MaterialCostPerUnit = sourceItem.MaterialCostPerUnit,
                                DoubleHit = sourceItem.DoubleHit,
                                UserSheetOverride = false,
                                RunSheets = sourceItem.RunSheets,
                                RunScrapSheets = sourceItem.RunScrapSheets,
                                Out = sourceItem.Out,
                                SubitemOf = null,
                                Collate = sourceItem.Collate,
                                Staple = sourceItem.Staple,
                                CustomMarkup = sourceItem.CustomMarkup,
                                CustomInkMarkup = sourceItem.CustomInkMarkup,
                                CustomMaterialMarkup = sourceItem.CustomMaterialMarkup,
                                CustomPrintLaborMarkup = sourceItem.CustomPrintLaborMarkup,
                                InternalNotes = sourceItem.InternalNotes,
                                InkCost = sourceItem.InkCost,
                                PrintTime = sourceItem.PrintTime,
                                UpValueLocked = sourceItem.UpValueLocked,
                                TotalSheets = sourceItem.TotalSheets,
                                TotalRunSheets = sourceItem.TotalRunSheets,
                                VariableScrapSheets = sourceItem.VariableScrapSheets,
                                TotalScrapSheets = sourceItem.TotalScrapSheets,
                                MaterialNotes = sourceItem.MaterialNotes,
                                ArtworkProvided = sourceItem.ArtworkProvided,
                                DesignHours = sourceItem.DesignHours,
                                BuySheets = sourceItem.BuySheets,
                                LastShipped = null,
                                Numbering = sourceItem.Numbering
                            };

                            db.JobItems.Add(newItem);
                            await db.SaveChangesAsync();
                            newItemId = newItem.Id;

                            // Copy JobItemServices and create mapping for timing records
                            var serviceIdMapping = new Dictionary<int, int>(); // old service ID -> new service ID
                            var pendingFinishingLinks = new Dictionary<int, JobItemService>(); // old JIFM ID -> new JIS awaiting FK link
                            foreach (var service in sourceItem.JobItemServices)
                            {
                                var newService = new JobItemService
                                {
                                    JobItemId = newItemId.Value,
                                    ServiceId = service.ServiceId,
                                    Ordinal = service.Ordinal,
                                    Operator = service.Operator,
                                    Deleted = service.Deleted,
                                    Notes = service.Notes,
                                    Caliper = service.Caliper,
                                    HourlyRate = service.HourlyRate,
                                    ServiceConfiguration = service.ServiceConfiguration,
                                    ServiceTime = service.ServiceTime,
                                    MakeReady = service.MakeReady,
                                    WashUp = service.WashUp,
                                    Complete = null,
                                    ApexOrdinal = service.ApexOrdinal,
                                    FormattedServiceTime = service.FormattedServiceTime,
                                    MaterialCost = service.MaterialCost,
                                    IsCuttingService = service.IsCuttingService,
                                    UserTimeOverride = service.UserTimeOverride,
                                    IsFinishingService = service.IsFinishingService,
                                    CustomMarkup = service.CustomMarkup,
                                    FormulaConfiguration = service.FormulaConfiguration,
                                    Title = service.Title,
                                    FromQuoteServiceId = service.FromQuoteServiceId,
                                    ScrapAmount = service.ScrapAmount,
                                    ScrapPercent = service.ScrapPercent,
                                    FriendlyName = service.FriendlyName
                                };
                                db.JobItemServices.Add(newService);
                                await db.SaveChangesAsync(); // Save to get the new ID

                                // Map old service ID to new service ID
                                serviceIdMapping[service.Id] = newService.Id;

                                if ((service.IsFinishingService ?? false) && service.JobItemFinishingMaterial.HasValue)
                                {
                                    pendingFinishingLinks[service.JobItemFinishingMaterial.Value] = newService;
                                }
                            }

                            // Copy JobItemFinishingMaterials
                            if (sourceItem.JobItemFinishingMaterials.Any())
                            {
                                var maxJifmId = await db.JobItemFinishingMaterials.MaxAsync(x => (int?)x.Id) ?? 0;
                                var jifmIdCounter = 1;

                                foreach (var material in sourceItem.JobItemFinishingMaterials)
                                {
                                    var newJifmId = maxJifmId + jifmIdCounter;
                                    var newMaterial = new JobItemFinishingMaterial
                                    {
                                        Id = newJifmId,
                                        JobItemId = newItemId.Value,
                                        Material = material.Material,
                                        FinishingFormulaConfiguration = material.FinishingFormulaConfiguration,
                                        FinishingFormulaApplication = material.FinishingFormulaApplication,
                                        FinalCount = material.FinalCount,
                                        ShippingCost = material.ShippingCost,
                                        CustomMarkup = material.CustomMarkup,
                                        ItemCost = material.ItemCost
                                    };
                                    db.JobItemFinishingMaterials.Add(newMaterial);
                                    jifmIdCounter++;

                                    if (pendingFinishingLinks.TryGetValue(material.Id, out var linkedJis))
                                    {
                                        linkedJis.JobItemFinishingMaterial = newJifmId;
                                    }
                                }

                                await db.SaveChangesAsync();
                            }

                            // Copy JobDocuments with proper path handling
                            foreach (var document in sourceItem.JobDocuments)
                            {
                                string newDocumentPath = document.DocumentPath;

                                // Update document path for new job
                                if (!string.IsNullOrEmpty(document.DocumentPath) && System.IO.File.Exists(document.DocumentPath))
                                {
                                    try
                                    {
                                        var targetJob = await db.Jobs.FindAsync(targetId);
                                        string targetJobNumber = targetJob.JobNumber.Replace("-", "_");
                                        string fileName = Path.GetFileName(document.DocumentPath);
                                        string newDirectoryPath = Path.Combine(storagePath, targetJobNumber);
                                        newDocumentPath = Path.Combine(newDirectoryPath, fileName);

                                        // Create directory if it doesn't exist
                                        if (!Directory.Exists(newDirectoryPath))
                                        {
                                            Directory.CreateDirectory(newDirectoryPath);
                                        }

                                        // Copy the physical file if it doesn't already exist
                                        if (!System.IO.File.Exists(newDocumentPath))
                                        {
                                            System.IO.File.Copy(document.DocumentPath, newDocumentPath, false);
                                        }
                                    }
                                    catch
                                    {
                                        // If file copying fails, use original path
                                        newDocumentPath = document.DocumentPath;
                                    }
                                }

                                var newDocument = new JobDocument
                                {
                                    ItemId = newItemId.Value,
                                    DocumentPath = newDocumentPath,
                                    DocumentHash = document.DocumentHash,
                                    UploadDate = document.UploadDate,
                                    UploadedBy = document.UploadedBy,
                                    RowType = document.RowType
                                };
                                db.JobDocuments.Add(newDocument);
                            }

                            // Copy ItemLayoutSettings
                            var sourceLayout = await db.ItemLayoutSettings.FirstOrDefaultAsync(x => x.JobItemId == itemId);
                            if (sourceLayout != null)
                            {
                                var newLayout = new ItemLayoutSetting
                                {
                                    JobItemId = newItemId.Value,
                                    QuoteItemId = null,
                                    LeftSideGuide = sourceLayout.LeftSideGuide,
                                    RightSideGuide = sourceLayout.RightSideGuide,
                                    XGutter = sourceLayout.XGutter,
                                    YGutter = sourceLayout.YGutter,
                                    Gripper = sourceLayout.Gripper,
                                    Tail = sourceLayout.Tail,
                                    SwapDimensions = sourceLayout.SwapDimensions,
                                    DutchCut = sourceLayout.DutchCut,
                                    LinkSideGuides = sourceLayout.LinkSideGuides,
                                    LinkGutters = sourceLayout.LinkGutters,
                                    CreatedAt = DateTime.UtcNow,
                                    UpdatedAt = DateTime.UtcNow
                                };
                                db.ItemLayoutSettings.Add(newLayout);
                            }

                            // Copy JobTimingSR records if requested
                            if (copyTimingRecords)
                            {
                                await db.Entry(sourceItem).Collection(c => c.JobTimingSRs).LoadAsync();
                                foreach (var timing in sourceItem.JobTimingSRs)
                                {
                                    var newTiming = new JobTimingSR
                                    {
                                        Job = targetId,
                                        JobItem = newItemId.Value,
                                        JobItemService = null,
                                        StartTimeUTC = timing.StartTimeUTC,
                                        EndTimeUTC = timing.EndTimeUTC,
                                        UserId = timing.UserId
                                    };

                                    // Map old service ID to new service ID using our mapping
                                    if (timing.JobItemService.HasValue && serviceIdMapping.ContainsKey(timing.JobItemService.Value))
                                    {
                                        newTiming.JobItemService = serviceIdMapping[timing.JobItemService.Value];
                                    }

                                    db.JobTimingSRs.Add(newTiming);
                                }
                            }
                        }
                    }
                    else if (sourceType == "Quote" && targetType == "Quote")
                    {
                        // Quote to Quote - copy all item data and related entities
                        var sourceItem = await db.QuoteItems
                            .Include(x => x.QuoteItemServices)
                            .Include(x => x.QuoteItemFinishingMaterials)
                            .Include(x => x.QuoteDocuments)
                            .FirstOrDefaultAsync(x => x.Id == itemId);

                        if (sourceItem != null)
                        {
                            var maxOrdinal = await db.QuoteItems.Where(x => x.Quote == targetId).MaxAsync(x => (int?)x.Ordinal) ?? 0;

                            var newItem = new QuoteItem
                            {
                                Quote = targetId,
                                Title = sourceItem.Title,
                                Description = sourceItem.Description,
                                Quantity = sourceItem.Quantity,
                                Notes = sourceItem.Notes,
                                Deleted = null,
                                Sheets = sourceItem.Sheets,
                                SheetsUnit = sourceItem.SheetsUnit,
                                Width = sourceItem.Width,
                                Length = sourceItem.Length,
                                Material = sourceItem.Material,
                                LayoutString = sourceItem.LayoutString,
                                MarginXL = sourceItem.MarginXL,
                                MarginYT = sourceItem.MarginYT,
                                MaterialRunSize = sourceItem.MaterialRunSize,
                                SideGuides = sourceItem.SideGuides,
                                Gripper = sourceItem.Gripper,
                                Tail = sourceItem.Tail,
                                Up = sourceItem.Up,
                                PressSpeedNumerator = sourceItem.PressSpeedNumerator,
                                PressSpeedDenominator = sourceItem.PressSpeedDenominator,
                                Press = sourceItem.Press,
                                PressColorProfileFront = sourceItem.PressColorProfileFront,
                                PressColorCountFront = sourceItem.PressColorCountFront,
                                PressColorNotesFront = sourceItem.PressColorNotesFront,
                                PressColorProfileBack = sourceItem.PressColorProfileBack,
                                PressColorCountBack = sourceItem.PressColorCountBack,
                                PressColorNotesBack = sourceItem.PressColorNotesBack,
                                Ordinal = maxOrdinal + 1,
                                Columns = sourceItem.Columns,
                                Rows = sourceItem.Rows,
                                MaxRows = sourceItem.MaxRows,
                                MaxColumns = sourceItem.MaxColumns,
                                Swap = sourceItem.Swap,
                                MixGrain = sourceItem.MixGrain,
                                SelectedSpeed = sourceItem.SelectedSpeed,
                                QuantityBy = sourceItem.QuantityBy,
                                TotalQuantity = sourceItem.TotalQuantity,
                                MaterialCost = sourceItem.MaterialCost,
                                CuttingConfiguration = sourceItem.CuttingConfiguration,
                                MarginXR = sourceItem.MarginXR,
                                MarginYB = sourceItem.MarginYB,
                                ScrapSheets = sourceItem.ScrapSheets,
                                MaterialCostPerUnit = sourceItem.MaterialCostPerUnit,
                                DoubleHit = sourceItem.DoubleHit,
                                UserSheetOverride = false,
                                RunSheets = sourceItem.RunSheets,
                                RunScrapSheets = sourceItem.RunScrapSheets,
                                Out = sourceItem.Out,
                                SubitemOf = null,
                                Collate = sourceItem.Collate,
                                Staple = sourceItem.Staple,
                                CustomMarkup = sourceItem.CustomMarkup,
                                CustomInkMarkup = sourceItem.CustomInkMarkup,
                                CustomMaterialMarkup = sourceItem.CustomMaterialMarkup,
                                CustomPrintLaborMarkup = sourceItem.CustomPrintLaborMarkup,
                                InternalNotes = sourceItem.InternalNotes,
                                InkCost = sourceItem.InkCost,
                                PrintTime = sourceItem.PrintTime,
                                UpValueLocked = sourceItem.UpValueLocked,
                                TotalSheets = sourceItem.TotalSheets,
                                TotalRunSheets = sourceItem.TotalRunSheets,
                                VariableScrapSheets = sourceItem.VariableScrapSheets,
                                TotalScrapSheets = sourceItem.TotalScrapSheets,
                                MaterialNotes = sourceItem.MaterialNotes,
                                ArtworkProvided = sourceItem.ArtworkProvided,
                                DesignHours = sourceItem.DesignHours,
                                BuySheets = sourceItem.BuySheets,
                                FromJobItemId = null,
                                Numbering = sourceItem.Numbering
                            };

                            db.QuoteItems.Add(newItem);
                            await db.SaveChangesAsync();
                            newItemId = newItem.Id;

                            // Copy QuoteItemServices
                            foreach (var service in sourceItem.QuoteItemServices)
                            {
                                var newService = new QuoteItemService
                                {
                                    QuoteItemId = newItemId.Value,
                                    OriginalService = service.OriginalService,
                                    Ordinal = service.Ordinal,
                                    Operator = service.Operator,
                                    Deleted = service.Deleted,
                                    Notes = service.Notes,
                                    Caliper = service.Caliper,
                                    HourlyRate = service.HourlyRate,
                                    ServiceConfiguration = service.ServiceConfiguration,
                                    ServiceTime = service.ServiceTime,
                                    MakeReady = service.MakeReady,
                                    WashUp = service.WashUp,
                                    ApexOrdinal = service.ApexOrdinal,
                                    FormattedServiceTime = service.FormattedServiceTime,
                                    MaterialCost = service.MaterialCost,
                                    IsCuttingService = service.IsCuttingService,
                                    UserTimeOverride = service.UserTimeOverride,
                                    IsFinishingService = service.IsFinishingService,
                                    CustomMarkup = service.CustomMarkup,
                                    FormulaConfiguration = service.FormulaConfiguration,
                                    Title = service.Title,
                                    ScrapAmount = service.ScrapAmount,
                                    ScrapPercent = service.ScrapPercent,
                                    ShowOnQuote = service.ShowOnQuote,
                                    FriendlyName = service.FriendlyName,
                                    FromJobItemServiceId = null
                                };
                                db.QuoteItemServices.Add(newService);
                            }

                            // Copy QuoteItemFinishingMaterials
                            foreach (var material in sourceItem.QuoteItemFinishingMaterials)
                            {
                                var newMaterial = new QuoteItemFinishingMaterial
                                {
                                    QuoteItemId = newItemId.Value,
                                    Material = material.Material,
                                    FinishingFormulaConfiguration = material.FinishingFormulaConfiguration,
                                    FinishingFormulaApplication = material.FinishingFormulaApplication,
                                    FinalCount = material.FinalCount,
                                    ShippingCost = material.ShippingCost,
                                    CustomMarkup = material.CustomMarkup,
                                    ItemCost = material.ItemCost
                                };
                                db.QuoteItemFinishingMaterials.Add(newMaterial);
                            }

                            // Copy QuoteDocuments with proper path handling
                            foreach (var document in sourceItem.QuoteDocuments)
                            {
                                string newDocumentPath = document.DocumentPath;

                                // Update document path for new quote
                                if (!string.IsNullOrEmpty(document.DocumentPath) && System.IO.File.Exists(document.DocumentPath))
                                {
                                    try
                                    {
                                        var targetQuote = await db.Quotes.FindAsync(targetId);
                                        string targetQuoteNumber = targetQuote.JobNumber;
                                        string fileName = Path.GetFileName(document.DocumentPath);
                                        string newDirectoryPath = Path.Combine(storagePath, targetQuoteNumber);
                                        newDocumentPath = Path.Combine(newDirectoryPath, fileName);

                                        // Create directory if it doesn't exist
                                        if (!Directory.Exists(newDirectoryPath))
                                        {
                                            Directory.CreateDirectory(newDirectoryPath);
                                        }

                                        // Copy the physical file if it doesn't already exist
                                        if (!System.IO.File.Exists(newDocumentPath))
                                        {
                                            System.IO.File.Copy(document.DocumentPath, newDocumentPath, false);
                                        }
                                    }
                                    catch
                                    {
                                        // If file copying fails, use original path
                                        newDocumentPath = document.DocumentPath;
                                    }
                                }

                                var newDocument = new QuoteDocument
                                {
                                    ItemId = newItemId.Value,
                                    DocumentPath = newDocumentPath,
                                    DocumentHash = document.DocumentHash,
                                    UploadDate = document.UploadDate,
                                    UploadedBy = document.UploadedBy,
                                    RowType = document.RowType
                                };
                                db.QuoteDocuments.Add(newDocument);
                            }

                            // Copy ItemLayoutSettings
                            var sourceLayout = await db.ItemLayoutSettings.FirstOrDefaultAsync(x => x.QuoteItemId == itemId);
                            if (sourceLayout != null)
                            {
                                var newLayout = new ItemLayoutSetting
                                {
                                    QuoteItemId = newItemId.Value,
                                    JobItemId = null,
                                    LeftSideGuide = sourceLayout.LeftSideGuide,
                                    RightSideGuide = sourceLayout.RightSideGuide,
                                    XGutter = sourceLayout.XGutter,
                                    YGutter = sourceLayout.YGutter,
                                    Gripper = sourceLayout.Gripper,
                                    Tail = sourceLayout.Tail,
                                    SwapDimensions = sourceLayout.SwapDimensions,
                                    DutchCut = sourceLayout.DutchCut,
                                    LinkSideGuides = sourceLayout.LinkSideGuides,
                                    LinkGutters = sourceLayout.LinkGutters,
                                    CreatedAt = DateTime.UtcNow,
                                    UpdatedAt = DateTime.UtcNow
                                };
                                db.ItemLayoutSettings.Add(newLayout);
                            }
                        }
                    }
                    else if (sourceType == "Job" && targetType == "Quote")
                    {
                        // Job to Quote - copy all item data and related entities
                        var sourceItem = await db.JobItems
                            .Include(x => x.JobItemServices)
                            .Include(x => x.JobItemFinishingMaterials)
                            .Include(x => x.JobDocuments)
                            .FirstOrDefaultAsync(x => x.Id == itemId);

                        if (sourceItem != null)
                        {
                            var maxOrdinal = await db.QuoteItems.Where(x => x.Quote == targetId).MaxAsync(x => (int?)x.Ordinal) ?? 0;

                            var newItem = new QuoteItem
                            {
                                Quote = targetId,
                                Title = sourceItem.Title,
                                Description = sourceItem.Description,
                                Quantity = sourceItem.Quantity,
                                Notes = sourceItem.Notes,
                                Deleted = null,
                                Sheets = sourceItem.Sheets,
                                SheetsUnit = sourceItem.SheetsUnit,
                                Width = sourceItem.Width,
                                Length = sourceItem.Length,
                                Material = sourceItem.Material,
                                LayoutString = sourceItem.LayoutString,
                                MarginXL = sourceItem.MarginXL,
                                MarginYT = sourceItem.MarginYT,
                                MaterialRunSize = sourceItem.MaterialRunSize,
                                SideGuides = sourceItem.SideGuides,
                                Gripper = sourceItem.Gripper,
                                Tail = sourceItem.Tail,
                                Up = sourceItem.Up,
                                PressSpeedNumerator = sourceItem.PressSpeedNumerator,
                                PressSpeedDenominator = sourceItem.PressSpeedDenominator,
                                Press = sourceItem.Press,
                                PressColorProfileFront = sourceItem.PressColorProfileFront,
                                PressColorCountFront = sourceItem.PressColorCountFront,
                                PressColorNotesFront = sourceItem.PressColorNotesFront,
                                PressColorProfileBack = sourceItem.PressColorProfileBack,
                                PressColorCountBack = sourceItem.PressColorCountBack,
                                PressColorNotesBack = sourceItem.PressColorNotesBack,
                                Ordinal = maxOrdinal + 1,
                                Columns = sourceItem.Columns,
                                Rows = sourceItem.Rows,
                                MaxRows = sourceItem.MaxRows,
                                MaxColumns = sourceItem.MaxColumns,
                                Swap = sourceItem.Swap,
                                MixGrain = sourceItem.MixGrain,
                                SelectedSpeed = sourceItem.SelectedSpeed,
                                QuantityBy = sourceItem.QuantityBy,
                                TotalQuantity = sourceItem.TotalQuantity,
                                MaterialCost = sourceItem.MaterialCost,
                                CuttingConfiguration = sourceItem.CuttingConfiguration,
                                MarginXR = sourceItem.MarginXR,
                                MarginYB = sourceItem.MarginYB,
                                ScrapSheets = sourceItem.ScrapSheets,
                                MaterialCostPerUnit = sourceItem.MaterialCostPerUnit,
                                DoubleHit = sourceItem.DoubleHit,
                                UserSheetOverride = false,
                                RunSheets = sourceItem.RunSheets,
                                RunScrapSheets = sourceItem.RunScrapSheets,
                                Out = sourceItem.Out,
                                SubitemOf = null,
                                Collate = sourceItem.Collate,
                                Staple = sourceItem.Staple,
                                CustomMarkup = sourceItem.CustomMarkup,
                                CustomInkMarkup = sourceItem.CustomInkMarkup,
                                CustomMaterialMarkup = sourceItem.CustomMaterialMarkup,
                                CustomPrintLaborMarkup = sourceItem.CustomPrintLaborMarkup,
                                InternalNotes = sourceItem.InternalNotes,
                                InkCost = sourceItem.InkCost,
                                PrintTime = sourceItem.PrintTime,
                                UpValueLocked = sourceItem.UpValueLocked,
                                TotalSheets = sourceItem.TotalSheets,
                                TotalRunSheets = sourceItem.TotalRunSheets,
                                VariableScrapSheets = sourceItem.VariableScrapSheets,
                                TotalScrapSheets = sourceItem.TotalScrapSheets,
                                MaterialNotes = sourceItem.MaterialNotes,
                                ArtworkProvided = sourceItem.ArtworkProvided,
                                DesignHours = sourceItem.DesignHours,
                                BuySheets = sourceItem.BuySheets,
                                FromJobItemId = sourceItem.Id,
                                Numbering = sourceItem.Numbering
                            };

                            db.QuoteItems.Add(newItem);
                            await db.SaveChangesAsync();
                            newItemId = newItem.Id;

                            // Copy JobItemServices -> QuoteItemServices
                            foreach (var service in sourceItem.JobItemServices)
                            {
                                var newService = new QuoteItemService
                                {
                                    QuoteItemId = newItemId.Value,
                                    OriginalService = service.ServiceId,
                                    Ordinal = service.Ordinal ?? 0,
                                    Operator = service.Operator,
                                    Deleted = service.Deleted,
                                    Notes = service.Notes,
                                    Caliper = service.Caliper,
                                    HourlyRate = service.HourlyRate,
                                    ServiceConfiguration = service.ServiceConfiguration,
                                    ServiceTime = service.ServiceTime,
                                    MakeReady = service.MakeReady,
                                    WashUp = service.WashUp,
                                    ApexOrdinal = service.ApexOrdinal,
                                    FormattedServiceTime = service.FormattedServiceTime,
                                    MaterialCost = service.MaterialCost,
                                    IsCuttingService = service.IsCuttingService,
                                    UserTimeOverride = service.UserTimeOverride,
                                    IsFinishingService = service.IsFinishingService,
                                    CustomMarkup = service.CustomMarkup,
                                    FormulaConfiguration = service.FormulaConfiguration,
                                    Title = service.Title,
                                    ScrapAmount = service.ScrapAmount,
                                    ScrapPercent = service.ScrapPercent,
                                    ShowOnQuote = true,
                                    FriendlyName = service.FriendlyName,
                                    FromJobItemServiceId = service.Id
                                };
                                db.QuoteItemServices.Add(newService);
                            }

                            // Copy JobItemFinishingMaterials -> QuoteItemFinishingMaterials
                            foreach (var material in sourceItem.JobItemFinishingMaterials)
                            {
                                var newMaterial = new QuoteItemFinishingMaterial
                                {
                                    QuoteItemId = newItemId.Value,
                                    Material = material.Material,
                                    FinishingFormulaConfiguration = material.FinishingFormulaConfiguration,
                                    FinishingFormulaApplication = material.FinishingFormulaApplication,
                                    FinalCount = material.FinalCount,
                                    ShippingCost = material.ShippingCost,
                                    CustomMarkup = material.CustomMarkup,
                                    ItemCost = material.ItemCost
                                };
                                db.QuoteItemFinishingMaterials.Add(newMaterial);
                            }

                            // Copy JobDocuments -> QuoteDocuments with proper path handling
                            foreach (var document in sourceItem.JobDocuments)
                            {
                                string newDocumentPath = document.DocumentPath;

                                // Update document path for new quote
                                if (!string.IsNullOrEmpty(document.DocumentPath) && System.IO.File.Exists(document.DocumentPath))
                                {
                                    try
                                    {
                                        var targetQuote = await db.Quotes.FindAsync(targetId);
                                        string targetQuoteNumber = targetQuote.JobNumber;
                                        string fileName = Path.GetFileName(document.DocumentPath);
                                        string newDirectoryPath = Path.Combine(storagePath, targetQuoteNumber);
                                        newDocumentPath = Path.Combine(newDirectoryPath, fileName);

                                        // Create directory if it doesn't exist
                                        if (!Directory.Exists(newDirectoryPath))
                                        {
                                            Directory.CreateDirectory(newDirectoryPath);
                                        }

                                        // Copy the physical file if it doesn't already exist
                                        if (!System.IO.File.Exists(newDocumentPath))
                                        {
                                            System.IO.File.Copy(document.DocumentPath, newDocumentPath, false);
                                        }
                                    }
                                    catch
                                    {
                                        // If file copying fails, use original path
                                        newDocumentPath = document.DocumentPath;
                                    }
                                }

                                var newDocument = new QuoteDocument
                                {
                                    ItemId = newItemId.Value,
                                    DocumentPath = newDocumentPath,
                                    DocumentHash = document.DocumentHash,
                                    UploadDate = document.UploadDate,
                                    UploadedBy = document.UploadedBy,
                                    RowType = document.RowType
                                };
                                db.QuoteDocuments.Add(newDocument);
                            }

                            // Copy ItemLayoutSettings
                            var sourceLayout = await db.ItemLayoutSettings.FirstOrDefaultAsync(x => x.JobItemId == itemId);
                            if (sourceLayout != null)
                            {
                                var newLayout = new ItemLayoutSetting
                                {
                                    QuoteItemId = newItemId.Value,
                                    JobItemId = null,
                                    LeftSideGuide = sourceLayout.LeftSideGuide,
                                    RightSideGuide = sourceLayout.RightSideGuide,
                                    XGutter = sourceLayout.XGutter,
                                    YGutter = sourceLayout.YGutter,
                                    Gripper = sourceLayout.Gripper,
                                    Tail = sourceLayout.Tail,
                                    SwapDimensions = sourceLayout.SwapDimensions,
                                    DutchCut = sourceLayout.DutchCut,
                                    LinkSideGuides = sourceLayout.LinkSideGuides,
                                    LinkGutters = sourceLayout.LinkGutters,
                                    CreatedAt = DateTime.UtcNow,
                                    UpdatedAt = DateTime.UtcNow
                                };
                                db.ItemLayoutSettings.Add(newLayout);
                            }
                        }
                    }
                    else if (sourceType == "Quote" && targetType == "Job")
                    {
                        // Quote to Job - copy all item data and related entities
                        var sourceItem = await db.QuoteItems
                            .Include(x => x.QuoteItemServices)
                            .Include(x => x.QuoteItemFinishingMaterials)
                            .Include(x => x.QuoteDocuments)
                            .FirstOrDefaultAsync(x => x.Id == itemId);

                        if (sourceItem != null)
                        {
                            var maxOrdinal = await db.JobItems.Where(x => x.Job == targetId).MaxAsync(x => (int?)x.Ordinal) ?? 0;

                            var newItem = new JobItem
                            {
                                Job = targetId,
                                Title = sourceItem.Title,
                                Description = sourceItem.Description,
                                Quantity = sourceItem.Quantity,
                                Notes = sourceItem.Notes,
                                Deleted = null,
                                Sheets = sourceItem.Sheets,
                                SheetsUnit = sourceItem.SheetsUnit,
                                Width = sourceItem.Width,
                                Length = sourceItem.Length,
                                Material = sourceItem.Material,
                                LayoutString = sourceItem.LayoutString,
                                MarginXL = sourceItem.MarginXL,
                                MarginYT = sourceItem.MarginYT,
                                MaterialRunSize = sourceItem.MaterialRunSize,
                                SideGuides = sourceItem.SideGuides,
                                Gripper = sourceItem.Gripper,
                                Tail = sourceItem.Tail,
                                Up = sourceItem.Up,
                                PressSpeedNumerator = sourceItem.PressSpeedNumerator,
                                PressSpeedDenominator = sourceItem.PressSpeedDenominator,
                                Press = sourceItem.Press,
                                PressColorProfileFront = sourceItem.PressColorProfileFront,
                                PressColorCountFront = sourceItem.PressColorCountFront,
                                PressColorNotesFront = sourceItem.PressColorNotesFront,
                                PressColorProfileBack = sourceItem.PressColorProfileBack,
                                PressColorCountBack = sourceItem.PressColorCountBack,
                                PressColorNotesBack = sourceItem.PressColorNotesBack,
                                Ordinal = maxOrdinal + 1,
                                Columns = sourceItem.Columns,
                                Rows = sourceItem.Rows,
                                MaxRows = sourceItem.MaxRows,
                                MaxColumns = sourceItem.MaxColumns,
                                Swap = sourceItem.Swap,
                                MixGrain = sourceItem.MixGrain,
                                SelectedSpeed = sourceItem.SelectedSpeed,
                                QuantityBy = sourceItem.QuantityBy,
                                TotalQuantity = sourceItem.TotalQuantity,
                                MaterialCost = sourceItem.MaterialCost,
                                CuttingConfiguration = sourceItem.CuttingConfiguration,
                                MarginXR = sourceItem.MarginXR,
                                MarginYB = sourceItem.MarginYB,
                                ScrapSheets = sourceItem.ScrapSheets,
                                MaterialCostPerUnit = sourceItem.MaterialCostPerUnit,
                                DoubleHit = sourceItem.DoubleHit,
                                UserSheetOverride = false,
                                RunSheets = sourceItem.RunSheets,
                                RunScrapSheets = sourceItem.RunScrapSheets,
                                Out = sourceItem.Out,
                                SubitemOf = null,
                                Collate = sourceItem.Collate,
                                Staple = sourceItem.Staple,
                                CustomMarkup = sourceItem.CustomMarkup,
                                CustomInkMarkup = sourceItem.CustomInkMarkup,
                                CustomMaterialMarkup = sourceItem.CustomMaterialMarkup,
                                CustomPrintLaborMarkup = sourceItem.CustomPrintLaborMarkup,
                                InternalNotes = sourceItem.InternalNotes,
                                InkCost = sourceItem.InkCost,
                                PrintTime = sourceItem.PrintTime,
                                UpValueLocked = sourceItem.UpValueLocked,
                                TotalSheets = sourceItem.TotalSheets,
                                TotalRunSheets = sourceItem.TotalRunSheets,
                                VariableScrapSheets = sourceItem.VariableScrapSheets,
                                TotalScrapSheets = sourceItem.TotalScrapSheets,
                                MaterialNotes = sourceItem.MaterialNotes,
                                ArtworkProvided = sourceItem.ArtworkProvided,
                                DesignHours = sourceItem.DesignHours,
                                BuySheets = sourceItem.BuySheets,
                                LastShipped = null,
                                Numbering = sourceItem.Numbering,
                                FromQuoteServiceItemId = sourceItem.FromJobItemId,
                                Complete = null,
                                JobStatus = null,
                                MinStartDate = null
                            };

                            db.JobItems.Add(newItem);
                            await db.SaveChangesAsync();
                            newItemId = newItem.Id;

                            // Copy QuoteItemServices -> JobItemServices
                            var pendingFinishingLinks = new Dictionary<int, JobItemService>(); // old QIFM ID -> new JIS awaiting FK link
                            foreach (var service in sourceItem.QuoteItemServices)
                            {
                                var newService = new JobItemService
                                {
                                    JobItemId = newItemId.Value,
                                    ServiceId = service.OriginalService,
                                    Ordinal = service.Ordinal,
                                    Operator = service.Operator,
                                    Deleted = service.Deleted,
                                    Notes = service.Notes,
                                    Caliper = service.Caliper,
                                    HourlyRate = service.HourlyRate,
                                    ServiceConfiguration = service.ServiceConfiguration,
                                    ServiceTime = service.ServiceTime,
                                    MakeReady = service.MakeReady,
                                    WashUp = service.WashUp,
                                    Complete = null,
                                    ApexOrdinal = service.ApexOrdinal,
                                    FormattedServiceTime = service.FormattedServiceTime,
                                    MaterialCost = service.MaterialCost,
                                    IsCuttingService = service.IsCuttingService,
                                    UserTimeOverride = service.UserTimeOverride,
                                    IsFinishingService = service.IsFinishingService,
                                    CustomMarkup = service.CustomMarkup,
                                    FormulaConfiguration = service.FormulaConfiguration,
                                    Title = service.Title,
                                    FromQuoteServiceId = service.Id,
                                    ScrapAmount = service.ScrapAmount,
                                    ScrapPercent = service.ScrapPercent,
                                    FriendlyName = service.FriendlyName
                                };
                                db.JobItemServices.Add(newService);

                                if ((service.IsFinishingService ?? false) && service.QuoteItemFinishingMaterial.HasValue)
                                {
                                    pendingFinishingLinks[service.QuoteItemFinishingMaterial.Value] = newService;
                                }
                            }

                            // Copy QuoteItemFinishingMaterials -> JobItemFinishingMaterials
                            if (sourceItem.QuoteItemFinishingMaterials.Any())
                            {
                                var maxJifmId = await db.JobItemFinishingMaterials.MaxAsync(x => (int?)x.Id) ?? 0;
                                var jifmIdCounter = 1;

                                foreach (var material in sourceItem.QuoteItemFinishingMaterials)
                                {
                                    var newJifmId = maxJifmId + jifmIdCounter;
                                    var newMaterial = new JobItemFinishingMaterial
                                    {
                                        Id = newJifmId,
                                        JobItemId = newItemId.Value,
                                        Material = material.Material.Value,
                                        FinishingFormulaConfiguration = material.FinishingFormulaConfiguration,
                                        FinishingFormulaApplication = material.FinishingFormulaApplication,
                                        FinalCount = material.FinalCount,
                                        ShippingCost = material.ShippingCost,
                                        CustomMarkup = material.CustomMarkup,
                                        ItemCost = material.ItemCost
                                    };
                                    db.JobItemFinishingMaterials.Add(newMaterial);
                                    jifmIdCounter++;

                                    if (pendingFinishingLinks.TryGetValue(material.Id, out var linkedJis))
                                    {
                                        linkedJis.JobItemFinishingMaterial = newJifmId;
                                    }
                                }

                                await db.SaveChangesAsync();
                            }

                            // Copy QuoteDocuments -> JobDocuments with proper path handling
                            foreach (var document in sourceItem.QuoteDocuments)
                            {
                                string newDocumentPath = document.DocumentPath;

                                // Update document path for new job
                                if (!string.IsNullOrEmpty(document.DocumentPath) && System.IO.File.Exists(document.DocumentPath))
                                {
                                    try
                                    {
                                        var targetJob = await db.Jobs.FindAsync(targetId);
                                        string targetJobNumber = targetJob.JobNumber.Replace("-", "_");
                                        string fileName = Path.GetFileName(document.DocumentPath);
                                        string newDirectoryPath = Path.Combine(storagePath, targetJobNumber);
                                        newDocumentPath = Path.Combine(newDirectoryPath, fileName);

                                        // Create directory if it doesn't exist
                                        if (!Directory.Exists(newDirectoryPath))
                                        {
                                            Directory.CreateDirectory(newDirectoryPath);
                                        }

                                        // Copy the physical file if it doesn't already exist
                                        if (!System.IO.File.Exists(newDocumentPath))
                                        {
                                            System.IO.File.Copy(document.DocumentPath, newDocumentPath, false);
                                        }
                                    }
                                    catch
                                    {
                                        // If file copying fails, use original path
                                        newDocumentPath = document.DocumentPath;
                                    }
                                }

                                var newDocument = new JobDocument
                                {
                                    ItemId = newItemId.Value,
                                    DocumentPath = newDocumentPath,
                                    DocumentHash = document.DocumentHash,
                                    UploadDate = document.UploadDate,
                                    UploadedBy = document.UploadedBy,
                                    RowType = document.RowType
                                };
                                db.JobDocuments.Add(newDocument);
                            }

                            // Copy ItemLayoutSettings
                            var sourceLayout = await db.ItemLayoutSettings.FirstOrDefaultAsync(x => x.QuoteItemId == itemId);
                            if (sourceLayout != null)
                            {
                                var newLayout = new ItemLayoutSetting
                                {
                                    JobItemId = newItemId.Value,
                                    QuoteItemId = null,
                                    LeftSideGuide = sourceLayout.LeftSideGuide,
                                    RightSideGuide = sourceLayout.RightSideGuide,
                                    XGutter = sourceLayout.XGutter,
                                    YGutter = sourceLayout.YGutter,
                                    Gripper = sourceLayout.Gripper,
                                    Tail = sourceLayout.Tail,
                                    SwapDimensions = sourceLayout.SwapDimensions,
                                    DutchCut = sourceLayout.DutchCut,
                                    LinkSideGuides = sourceLayout.LinkSideGuides,
                                    LinkGutters = sourceLayout.LinkGutters,
                                    CreatedAt = DateTime.UtcNow,
                                    UpdatedAt = DateTime.UtcNow
                                };
                                db.ItemLayoutSettings.Add(newLayout);
                            }

                            // Note: JobTimingSR copying is only available for Job→Job imports
                            // Quote items don't have timing records to copy from
                        }
                    }

                    if (newItemId.HasValue)
                    {
                        importedCount++;
                        newItemIds.Add(newItemId.Value);
                    }
                }

                await db.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"Successfully imported {importedCount} item(s)",
                    newItemIds = newItemIds
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error importing items: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<ActionResult> RefreshQuoteItems(int quoteId)
        {
            try
            {
                // Use the exact same approach as the existing UpdateItemPositions method
                Quote q = await db.Quotes.FindAsync(quoteId);
                if (q == null)
                    return new HttpStatusCodeResult(HttpStatusCode.NotFound);

                await db.Entry(q).Collection(c => c.QuoteItems).LoadAsync();

                //load foreign key entities (exact copy from UpdateItemPositions)
                foreach (QuoteItem qi in q.QuoteItems.Where(x => x.Deleted == null))
                {
                    string unqKey = "hasDutchCut_" + qi.Id.ToString();
                    ViewData[unqKey] = false;

                    try
                    {
                        ItemLayoutSetting ils = await db.ItemLayoutSettings.SingleAsync(x => x.QuoteItemId == qi.Id);
                        ViewData[unqKey] = ils.DutchCut;
                    }
                    catch (Exception e)
                    {
                        //no record found; move on
                    }

                    if (!qi.MaterialCost.HasValue)
                        qi.MaterialCost = 0m;
                    if (!qi.Sheets.HasValue)
                        qi.Sheets = 1;

                    await db.Entry(qi).Collection(c => c.QuoteItemServices).LoadAsync();
                    await db.Entry(qi).Reference(r => r.Press1).LoadAsync();
                    await db.Entry(qi).Reference(r => r.Material1).LoadAsync();
                    await db.Entry(qi).Collection(c => c.QuoteItemFinishingMaterials).LoadAsync();

                    foreach (QuoteItemFinishingMaterial qifm in qi.QuoteItemFinishingMaterials)
                    {
                        await db.Entry(qifm).Reference(r => r.Material1).LoadAsync();
                        await db.Entry(qifm).Reference(r => r.FinishingMaterialApplication).LoadAsync();

                        Dictionary<string, string> d = JsonConvert.DeserializeObject<Dictionary<string, string>>((qifm.FinishingFormulaConfiguration ?? "{\"\":\"\"}"));
                        qifm.ConfigLabel = d.First().Key.Split('_')[0];
                        qifm.ConfigValue = d.First().Value;
                    }

                    if (qi.Material.HasValue)
                    {
                        await db.Entry(qi.Material1).Reference(r => r.MaterialStockType).LoadAsync();
                        await db.Entry(qi.Material1).Reference(r => r.MaterialCostType).LoadAsync();

                        qi.MaterialCostPerUnitFormatted = qi.MaterialCostPerUnit.HasValue ? qi.MaterialCostPerUnit.Value.ToString("C") : "0.00";
                        qi.MaterialCostFormatted = (qi.MaterialCost ?? 0m).ToString("C");
                    }
                    else
                    {
                        qi.MaterialCostPerUnit = 0m;
                        qi.MaterialCostPerUnitFormatted = qi.MaterialCostPerUnit.Value.ToString("C");
                        qi.MaterialCostFormatted = (qi.MaterialCost ?? 0m).ToString("C");
                    }

                    if (qi.Press.HasValue)
                    {
                        await db.Entry(qi.Press1).Reference(r => r.SpeedDenominator1).LoadAsync();
                    }

                    try
                    {
                        foreach (QuoteItemService qis in qi.QuoteItemServices)
                        {
                            await db.Entry(qis).Reference(r => r.Service).LoadAsync();
                            if (qis.Service == null) continue;
                            await db.Entry(qis.Service).Reference(r => r.ServiceCategory).LoadAsync();
                            await db.Entry(qis.Service.ServiceCategory).Reference(r => r.ServiceType1).LoadAsync();
                            qis.FormattedServiceTime = DecimalHoursToFormatted(qis.ServiceTime);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }

                    if (qi.Material.HasValue)
                    {
                        qi.Material1.ParseDimensions();
                    }
                    qi.ParseRunSize();

                    qi.FormattedPrintTime = DecimalHoursToFormatted(qi.PrintTime);

                    qi.DocumentCount = await db.QuoteDocuments.Where(x => x.ItemId == qi.Id && x.RowType == "Standard").CountAsync();

                    //null coalesce any null'd checkboxes
                    qi.DoubleHit = qi.DoubleHit ?? false;
                    qi.Staple = qi.Staple ?? false;
                    qi.Collate = qi.Collate ?? false;
                }

                // Return the same way as UpdateItemPositions
                return new JsonResult
                {
                    Data = RenderViewToString("~/Views/Partials/_QuoteItemRows.cshtml", q.QuoteItems.Where(x => x.Deleted == null).OrderBy(x => x.Ordinal ?? -1).ToList()),
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
            catch (Exception ex)
            {
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpGet]
        public async Task<ActionResult> RefreshJobItems(int jobId)
        {
            try
            {
                // Use the exact same approach as the existing UpdateItemPositions method
                Job j = await db.Jobs.FindAsync(jobId);
                if (j == null)
                    return new HttpStatusCodeResult(HttpStatusCode.NotFound);

                await db.Entry(j).Collection(c => c.JobItems).LoadAsync();

                //load foreign key entities (exact copy from UpdateItemPositions)
                foreach (JobItem ji in j.JobItems.Where(x => x.Deleted == null))
                {
                    string unqKey = "hasDutchCut_" + ji.Id.ToString();
                    ViewData[unqKey] = false;

                    try
                    {
                        ItemLayoutSetting ils = await db.ItemLayoutSettings.SingleAsync(x => x.JobItemId == ji.Id);
                        ViewData[unqKey] = ils.DutchCut;
                    }
                    catch (Exception e)
                    {
                        //no record found; move on
                    }

                    if (!ji.MaterialCost.HasValue)
                        ji.MaterialCost = 0m;
                    if (!ji.Sheets.HasValue)
                        ji.Sheets = 1;

                    await db.Entry(ji).Collection(c => c.JobItemServices).LoadAsync();
                    await db.Entry(ji).Reference(r => r.Press1).LoadAsync();
                    await db.Entry(ji).Reference(r => r.Material1).LoadAsync();
                    await db.Entry(ji).Collection(c => c.JobItemFinishingMaterials).LoadAsync();

                    foreach (JobItemFinishingMaterial jifm in ji.JobItemFinishingMaterials)
                    {
                        await db.Entry(jifm).Reference(r => r.Material1).LoadAsync();
                        await db.Entry(jifm).Reference(r => r.FinishingMaterialApplication).LoadAsync();

                        Dictionary<string, string> d = JsonConvert.DeserializeObject<Dictionary<string, string>>((jifm.FinishingFormulaConfiguration ?? "{\"\":\"\"}"));
                        jifm.ConfigLabel = d.First().Key.Split('_')[0];
                        jifm.ConfigValue = d.First().Value;
                    }

                    if (ji.Material.HasValue)
                    {
                        await db.Entry(ji.Material1).Reference(r => r.MaterialStockType).LoadAsync();
                        await db.Entry(ji.Material1).Reference(r => r.MaterialCostType).LoadAsync();

                        ji.MaterialCostPerUnitFormatted = ji.MaterialCostPerUnit.HasValue ? ji.MaterialCostPerUnit.Value.ToString("C") : "0.00";
                        ji.MaterialCostFormatted = (ji.MaterialCost ?? 0m).ToString("C");
                    }
                    else
                    {
                        ji.MaterialCostPerUnit = 0m;
                        ji.MaterialCostPerUnitFormatted = ji.MaterialCostPerUnit.Value.ToString("C");
                        ji.MaterialCostFormatted = (ji.MaterialCost ?? 0m).ToString("C");
                    }

                    if (ji.Press.HasValue)
                        await db.Entry(ji.Press1).Reference(r => r.SpeedDenominator1).LoadAsync();

                    foreach (JobItemService jis in ji.JobItemServices)
                    {
                        await db.Entry(jis).Reference(r => r.Service).LoadAsync();
                        await db.Entry(jis.Service).Reference(r => r.ServiceCategory).LoadAsync();
                        await db.Entry(jis.Service.ServiceCategory).Reference(r => r.ServiceType1).LoadAsync();
                        jis.FormattedServiceTime = DecimalHoursToFormatted(jis.ServiceTime);
                    }

                    if (ji.Material.HasValue)
                        ji.Material1.ParseDimensions();

                    ji.ParseRunSize();
                    ji.FormattedPrintTime = DecimalHoursToFormatted(ji.PrintTime);
                    ji.DocumentCount = await db.JobDocuments.Where(x => x.ItemId == ji.Id && x.RowType == "Standard").CountAsync();

                    //null coalesce any null'd checkboxes to false
                    ji.DoubleHit = ji.DoubleHit ?? false;
                    ji.Staple = ji.Staple ?? false;
                    ji.Collate = ji.Collate ?? false;
                }

                // Return the same way as UpdateItemPositions
                return new JsonResult
                {
                    Data = RenderViewToString("~/Views/Partials/_JobItemRows.cshtml", j.JobItems.Where(x => x.Deleted == null).OrderBy(x => x.Ordinal ?? -1).ToList()),
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
            catch (Exception ex)
            {
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpPost]
        public async Task<ActionResult> ToggleJobSqftPricing(int jobId, bool useSqftPricing)
        {
            try
            {
                Job j = await db.Jobs.FindAsync(jobId);

                if (j != null)
                {
                    j.UseSqftPricing = useSqftPricing;

                    db.Entry(j).State = EntityState.Modified;

                    await db.SaveChangesAsync();

                    return new JsonResult
                    {
                        Data = new { success = true, useSqftPricing = useSqftPricing },
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };
                }

                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Internal error trying to switch pricing structures");
            }
            catch (Exception ex)
            {
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpPost]
        public async Task<ActionResult> ToggleQuoteSqftPricing(int quoteId, bool useSqftPricing)
        {
            try
            {
                Quote q = await db.Quotes.FindAsync(quoteId);

                if (q != null)
                {
                    q.UseSqftPricing = useSqftPricing;

                    db.Entry(q).State = EntityState.Modified;

                    await db.SaveChangesAsync();

                    return new JsonResult
                    {
                        Data = new { success = true, useSqftPricing = useSqftPricing },
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };
                }

                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Internal error trying to switch pricing structures");
            }
            catch (Exception ex)
            {
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpPost]
        public async Task<ActionResult> UpdateJobSqftPrice(int jobId, decimal sqftPrice)
        {
            try
            {
                Job j = await db.Jobs.FindAsync(jobId);

                if (j != null)
                {
                    j.SqftPriceOverride = sqftPrice;

                    db.Entry(j).State = EntityState.Modified;

                    await db.SaveChangesAsync();

                    return new JsonResult
                    {
                        Data = new { success = true, sqftPrice = sqftPrice },
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };
                }

                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Error switching pricing structures");
            }
            catch (Exception ex)
            {
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpGet]
        public async Task<ActionResult> GetClientSqftPrice(int clientId)
        {
            try
            {
                Client client = await db.Clients.FindAsync(clientId);
                if (client == null)
                    return new JsonResult
                    {
                        Data = new { sqftPrice = (decimal?)null },
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };

                return new JsonResult
                {
                    Data = new { sqftPrice = client.SqftPrice },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
            catch (Exception ex)
            {
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpPost]
        public async Task<ActionResult> UpdateQuoteSqftPrice(int quoteId, decimal sqftPrice)
        {
            try
            {
                Quote q = await db.Quotes.FindAsync(quoteId);

                if (q != null)
                {
                    q.SqftPriceOverride = sqftPrice;

                    db.Entry(q).State = EntityState.Modified;

                    await db.SaveChangesAsync();

                    return new JsonResult
                    {
                        Data = new { success = true, sqftPrice = sqftPrice },
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };
                }

                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Error switching pricing structures");
            }
            catch (Exception ex)
            {
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpPost]
        public async Task<ActionResult> UpdateQuoteReleaseToClientView(int quoteId, bool releaseToClientView)
        {
            try
            {
                Quote q = await db.Quotes.FindAsync(quoteId);

                if (q != null)
                {
                    q.ReleaseToClientView = releaseToClientView;

                    db.Entry(q).State = EntityState.Modified;

                    await db.SaveChangesAsync();

                    return new JsonResult
                    {
                        Data = new { success = true, releaseToClientView = releaseToClientView },
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };
                }

                return new JsonResult
                {
                    Data = new { success = false, message = "Quote not found" },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
            catch (Exception ex)
            {
                return new JsonResult
                {
                    Data = new { success = false, message = ex.Message },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
        }

        #region Client Cost Splits

        /// <summary>
        /// Get client cost splits for a job
        /// </summary>
        [HttpGet]
        public JsonResult GetJobClientSplits(int jobId)
        {
            try
            {
                var service = new ClientCostSplitService(db);
                var splits = service.GetJobSplits(jobId);

                return new JsonResult
                {
                    Data = new { success = true, splits = splits },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
            catch (Exception ex)
            {
                return new JsonResult
                {
                    Data = new { success = false, message = ex.Message },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
        }

        /// <summary>
        /// Get client cost splits for a quote
        /// </summary>
        [HttpGet]
        public JsonResult GetQuoteClientSplits(int quoteId)
        {
            try
            {
                var service = new ClientCostSplitService(db);
                var splits = service.GetQuoteSplits(quoteId);

                return new JsonResult
                {
                    Data = new { success = true, splits = splits },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
            catch (Exception ex)
            {
                return new JsonResult
                {
                    Data = new { success = false, message = ex.Message },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
        }

        /// <summary>
        /// Save client cost splits for a job
        /// </summary>
        [HttpPost]
        public JsonResult SaveJobClientSplits(int jobId, List<VM_ClientCostSplit> splits)
        {
            try
            {
                var job = db.Jobs.Find(jobId);
                if (job == null)
                {
                    return new JsonResult
                    {
                        Data = new { success = false, message = "Job not found" }
                    };
                }

                if (!job.Client.HasValue)
                {
                    return new JsonResult
                    {
                        Data = new { success = false, message = "Job must have a base client assigned" }
                    };
                }

                var service = new ClientCostSplitService(db);
                var (isValid, errorMessage) = service.ValidateSplitsWithMessage(splits);

                if (!isValid)
                {
                    return new JsonResult
                    {
                        Data = new { success = false, message = errorMessage }
                    };
                }

                var userName = User?.Identity?.Name;
                var result = service.SaveJobSplits(jobId, job.Client.Value, splits, userName);

                return new JsonResult
                {
                    Data = new { success = result, message = result ? "Splits saved successfully" : "Failed to save splits" }
                };
            }
            catch (Exception ex)
            {
                return new JsonResult
                {
                    Data = new { success = false, message = ex.Message }
                };
            }
        }

        /// <summary>
        /// Save client cost splits for a quote
        /// </summary>
        [HttpPost]
        public JsonResult SaveQuoteClientSplits(int quoteId, List<VM_ClientCostSplit> splits)
        {
            try
            {
                var quote = db.Quotes.Find(quoteId);
                if (quote == null)
                {
                    return new JsonResult
                    {
                        Data = new { success = false, message = "Quote not found" }
                    };
                }

                if (!quote.Client.HasValue)
                {
                    return new JsonResult
                    {
                        Data = new { success = false, message = "Quote must have a base client assigned" }
                    };
                }

                var service = new ClientCostSplitService(db);
                var (isValid, errorMessage) = service.ValidateSplitsWithMessage(splits);

                if (!isValid)
                {
                    return new JsonResult
                    {
                        Data = new { success = false, message = errorMessage }
                    };
                }

                var userName = User?.Identity?.Name;
                var result = service.SaveQuoteSplits(quoteId, quote.Client.Value, splits, userName);

                return new JsonResult
                {
                    Data = new { success = result, message = result ? "Splits saved successfully" : "Failed to save splits" }
                };
            }
            catch (Exception ex)
            {
                return new JsonResult
                {
                    Data = new { success = false, message = ex.Message }
                };
            }
        }

        /// <summary>
        /// Get available clients for adding to splits (excludes already added clients)
        /// </summary>
        [HttpGet]
        public JsonResult GetAvailableClientsForSplit(int entityId, string entityType, string searchTerm = "")
        {
            try
            {
                var service = new ClientCostSplitService(db);
                List<int> existingClientIds;

                if (entityType?.ToLower() == "quote")
                {
                    existingClientIds = service.GetQuoteSplits(entityId).Select(s => s.ClientId).ToList();
                }
                else
                {
                    existingClientIds = service.GetJobSplits(entityId).Select(s => s.ClientId).ToList();
                }

                var query = db.Clients
                    .Where(c => c.Deleted == null && !existingClientIds.Contains(c.Id));

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    query = query.Where(c => c.Title.Contains(searchTerm));
                }

                var clients = query
                    .OrderBy(c => c.Title)
                    .Take(50)
                    .Select(c => new { c.Id, c.Title })
                    .ToList();

                return new JsonResult
                {
                    Data = new { success = true, clients = clients },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
            catch (Exception ex)
            {
                return new JsonResult
                {
                    Data = new { success = false, message = ex.Message },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
        }

        #endregion Client Cost Splits
    }
}