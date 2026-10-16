using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using FifthPress.Models.ViewModels;
using Newtonsoft.Json;
using System.Net;
using FifthPress.Models;
using FifthPress.Models.ControllerModels;
using FifthPress.Models.ServiceParameterModels;

namespace FifthPress.Controllers
{
    public class ServicesController : DataController
    {
        public async Task<ActionResult> SideMenuStop(int? recordId)
        {
            try
            {
                //ensure user is valid
                int userId = UserSessionHelper.GetActiveUserId(HttpContext);

                JobTimingSR jt = await db.JobTimingSRs.SingleAsync(x => x.Id == (recordId ?? 0) && x.UserId == userId);

                if (jt != null)
                {
                    //update the end time and save the record
                    jt.EndTimeUTC = DateTime.UtcNow;
                    db.Entry(jt).State = EntityState.Modified;
                    await db.SaveChangesAsync();

                    return new HttpStatusCodeResult(HttpStatusCode.OK);
                }
                else
                {
                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Timing record not found");
                }
            }
            catch (Exception)
            {
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized);
            }
        }

        public async Task<ActionResult> AddUserToJobService(int? op, int? recordId)
        {
            if (!op.HasValue || !recordId.HasValue)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Missing required parameters");

            //using (ConnectToDatabase())
            {
                JobItemService jis = await db.JobItemServices.FindAsync(recordId);
                jis.Operator = op;
                db.Entry(jis).State = EntityState.Modified;
                await db.SaveChangesAsync();
            }

            return new JsonResult { Data = "User added", JsonRequestBehavior = JsonRequestBehavior.AllowGet };
        }

        public async Task<ActionResult> ReloadServiceListHtml(string recordtype, int? id)
        {
            // [SMOKE-TEST] Instrumentation for ReloadServiceListHtml N+1 fix — remove once verified.
            var sw_rsl = System.Diagnostics.Stopwatch.StartNew();

            switch (recordtype.ToUpper())
            {
                case "QUOTE":
                    // Load the QuoteItem with its full service nav tree in one query.
                    // Before: FindAsync + Collection load + per-service Reference(Service) +
                    //         Reference(ServiceCategory) + Reference(ServiceType1) = 2 + 3*P round-trips.
                    // After:  one SQL with JOINs; the LINQ below sees nav properties already populated.
                    QuoteItem qi = await db.QuoteItems
                        .Include(q => q.QuoteItemServices.Select(qis => qis.Service.ServiceCategory.ServiceType1))
                        .FirstOrDefaultAsync(q => q.Id == id);
                    if (qi != null)
                    {

                        var jsonQ = new
                        {
                            prepressServices = RenderViewToString("~/Views/Partials/_ItemServiceRows.cshtml", new VM_ItemServices
                            {
                                ServiceList =
                                    qi.QuoteItemServices
                                    .Where(x => x.Service != null && x.Service.ServiceCategory.ServiceType1.Title == "Prepress")
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
                            }),
                            printingServices = RenderViewToString("~/Views/Partials/_ItemServiceRows.cshtml", new VM_ItemServices
                            {
                                ServiceList =
                                    qi.QuoteItemServices
                                    .Where(x => x.Service != null && x.Service.ServiceCategory.ServiceType1.Title == "Printing")
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
                            }),
                            cuttingServices = RenderViewToString("~/Views/Partials/_ItemServiceRows.cshtml", new VM_ItemServices
                            {
                                ServiceList =
                                    qi.QuoteItemServices
                                    .Where(x => x.Service != null && x.Service.ServiceCategory.ServiceType1.Title == "Finishing" && (x.IsCuttingService ?? false))
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
                            }),
                            finishingServices = RenderViewToString("~/Views/Partials/_ItemServiceRows.cshtml", new VM_ItemServices
                            {
                                ServiceList =
                                    qi.QuoteItemServices
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
                            })
                        };

                        // [SMOKE-TEST]
                        MastermindLogger.Log(LogLevel.Info, $"[ReloadServiceListHtml] QUOTE itemId={id} services={qi.QuoteItemServices.Count} elapsed={sw_rsl.ElapsedMilliseconds}ms");
                        return new JsonResult
                        {
                            Data = JsonConvert.SerializeObject(jsonQ,
                                    new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                            JsonRequestBehavior = JsonRequestBehavior.AllowGet
                        };
                    }
                    break;

                case "JOB":
                    // Load the JobItem with its full service nav tree in one query (same pattern as QUOTE branch).
                    JobItem ji = await db.JobItems
                        .Include(j => j.JobItemServices.Select(jis => jis.Service.ServiceCategory.ServiceType1))
                        .FirstOrDefaultAsync(j => j.Id == id);
                    if (ji != null)
                    {

                        var jsonJ = new
                        {
                            prepressServices = RenderViewToString("~/Views/Partials/_ItemServiceRows.cshtml", new VM_ItemServices
                            {
                                ServiceList =
                                    ji.JobItemServices
                                    .Where(x => x.Service != null && x.Service.ServiceCategory.ServiceType1.Title == "Prepress")
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
                            }),
                            printingServices = RenderViewToString("~/Views/Partials/_ItemServiceRows.cshtml", new VM_ItemServices
                            {
                                ServiceList =
                                    ji.JobItemServices
                                    .Where(x => x.Service != null && x.Service.ServiceCategory.ServiceType1.Title == "Printing")
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
                                        RequiresQualityCheck = true,
                                        IsPrintingService = true,
                                        IsFinishingService = x.IsFinishingService ?? false
                                    }).ToList()
                            }),
                            cuttingServices = RenderViewToString("~/Views/Partials/_ItemServiceRows.cshtml", new VM_ItemServices
                            {
                                ServiceList =
                                    ji.JobItemServices
                                    .Where(x => x.Service != null && x.Service.ServiceCategory.ServiceType1.Title == "Finishing" && (x.IsCuttingService ?? false))
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
                            }),
                            finishingServices = RenderViewToString("~/Views/Partials/_ItemServiceRows.cshtml", new VM_ItemServices
                            {
                                ServiceList =
                                    ji.JobItemServices
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
                            })
                        };

                        // [SMOKE-TEST]
                        MastermindLogger.Log(LogLevel.Info, $"[ReloadServiceListHtml] JOB itemId={id} services={ji.JobItemServices.Count} elapsed={sw_rsl.ElapsedMilliseconds}ms");
                        return new JsonResult
                        {
                            Data = JsonConvert.SerializeObject(jsonJ,
                                    new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                            JsonRequestBehavior = JsonRequestBehavior.AllowGet
                        };
                    }
                    break;

                default:
                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Invalid record type");
            }

            return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Unable to find record");
        }

        private QuoteItemService UpdateQuoteItemServiceRecord(QuoteItemService current, QuoteItemService updated)
        {
            //commented out properties that do not need to be handled in this function - this function is called after updating via the service editor on a job/quote

            //current.QuoteItemId = updated.QuoteItemId;
            //current.OriginalService = updated.OriginalService;
            //current.Ordinal = updated.Ordinal;
            //current.Operator = updated.Operator;
            current.Notes = updated.Notes;
            current.Caliper = updated.Caliper;
            current.HourlyRate = updated.HourlyRate;
            current.Deleted = null;
            current.MakeReady = updated.MakeReady;
            current.WashUp = updated.WashUp;
            //current.ServiceConfiguration = updated.ServiceConfiguration;

            current.UserTimeOverride = updated.UserTimeOverride;
            current.ServiceTime = updated.ServiceTime;
            current.FormattedServiceTime = updated.FormattedServiceTime;
            current.MaterialCost = updated.MaterialCost;
            //current.ApexOrdinal = updated.ApexOrdinal;
            //current.IsCuttingService = updated.IsCuttingService;
            current.ScrapAmount = updated.ScrapAmount;
            current.ScrapPercent = updated.ScrapPercent;

            return current;
        }

        private JobItemService UpdateJobItemServiceRecord(JobItemService current, JobItemService updated)
        {
            //commented out properties that do not need to be handled in this function - this function is called after updating via the service editor on a job/quote

            //current.JobItemId = updated.JobItemId;
            //current.ServiceId = updated.ServiceId;
            //current.Ordinal = updated.Ordinal;
            //current.Operator = updated.Operator;
            current.Notes = updated.Notes;
            current.Caliper = updated.Caliper;
            current.HourlyRate = updated.HourlyRate;
            current.Deleted = null;
            current.MakeReady = updated.MakeReady;
            current.WashUp = updated.WashUp;
            //current.ServiceConfiguration = updated.ServiceConfiguration;

            current.UserTimeOverride = updated.UserTimeOverride;
            current.ServiceTime = updated.ServiceTime;
            current.FormattedServiceTime = updated.FormattedServiceTime;
            //current.JobStatus = updated.JobStatus;
            current.MaterialCost = updated.MaterialCost;
            //current.ApexOrdinal = updated.ApexOrdinal;
            //current.IsCuttingService = updated.IsCuttingService;
            current.ScrapAmount = updated.ScrapAmount;
            current.ScrapPercent = updated.ScrapPercent;

            return current;
        }

        public async Task<ActionResult> NewService()
        {
            ViewData["Service.CatNo"] = await GenerateSelectListAsync(SelectListType.ServiceCategories);
            ViewData["Service.TimeFormula"] = await GenerateSelectListAsync(SelectListType.Formulas);
            ViewData["Service.ProductionDepartment"] = await GenerateSelectListAsync(SelectListType.ProductionDepartments);

            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(RenderViewToString("~/Views/Services/GetServiceForEdit.cshtml", new VM_Service { Service = new Service { Required = false, IsTimed = false }, Services = new List<Service>() })),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> SaveService(Service theItem, List<ServiceLinkedService> LinkedServices)
        {
            if (theItem == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            if (LinkedServices == null)
                LinkedServices = new List<ServiceLinkedService>();

            var s = db.Services.Find(theItem.Id);

            if (s == null)
            {
                int nextSerNo = (await db.Services.MaxAsync(x => x.SerNo) ?? 0) + 1;
                theItem.SerNo = nextSerNo;
                //save new service
                db.Services.Add(theItem);
                if (theItem.TimeFormula.HasValue)
                {
                    var tf = await db.TimeFormulas.FindAsync(theItem.TimeFormula.Value);
                    if (tf != null)
                        theItem.ServiceConfiguration = tf.DefaultConfig;
                    else
                        theItem.TimeFormula = null;
                }

                if ((theItem.IsCuttingService ?? false))
                {
                    try
                    {
                        CuttingConfigurationPivot ccp = await db.CuttingConfigurationPivots.SingleAsync(x => x.Service == theItem.Id);
                        if (!(ccp.Enabled ?? false))
                            ccp.Enabled = true;

                        db.Entry(ccp).State = EntityState.Modified;
                    }
                    catch (Exception)
                    {
                        //no pivot; create a new one
                        CuttingConfigurationPivot ccp = new CuttingConfigurationPivot
                        {
                            Service = theItem.Id,
                            Enabled = true
                        };

                        int newGroupId = 0;
                        try
                        {
                            newGroupId = await db.CuttingConfigurationPivots.MaxAsync(x => x.GroupId);
                        }
                        catch (Exception) { }
                        finally
                        {
                            newGroupId++; //always increment newGroupId
                        }
                        ccp.GroupId = newGroupId;
                        db.CuttingConfigurationPivots.Add(ccp);
                    }
                }
            }
            else
            {
                //save existing service
                bool changingTimeFormula = s.TimeFormula != theItem.TimeFormula;
                //if we changed from a cutting service to a non-cutting service...
                if (!(theItem.IsCuttingService ?? false) && (s.IsCuttingService ?? false))
                {
                    try
                    {
                        //find the pivot record
                        CuttingConfigurationPivot ccp = await db.CuttingConfigurationPivots.SingleAsync(x => x.Service == theItem.Id);
                        List<CuttingConfiguration> configs = await db.CuttingConfigurations.Where(x => x.GroupId == ccp.GroupId).ToListAsync();
                        foreach (CuttingConfiguration c in configs)
                            db.CuttingConfigurations.Remove(c);

                        db.CuttingConfigurationPivots.Remove(ccp);
                    }
                    catch (Exception)
                    {
                        //for now, don't worry. don't think we should actually his this catch but here as a jic
                        throw;
                    }
                }
                s = UpdateServiceRecord(s, theItem);
                if (s.TimeFormula.HasValue || changingTimeFormula) //might need to change this to AND
                {
                    var tf = await db.TimeFormulas.FindAsync(theItem.TimeFormula.Value);
                    if (tf != null)
                        s.ServiceConfiguration = tf.DefaultConfig;
                    else
                    {
                        s.ServiceConfiguration = null;
                        s.TimeFormula = null;
                    }
                }
                db.Entry(s).State = EntityState.Modified;

                if (s.IsCuttingService ?? false)
                {
                    try
                    {
                        CuttingConfigurationPivot ccp = await db.CuttingConfigurationPivots.SingleAsync(x => x.Service == theItem.Id);
                        if (!(ccp.Enabled ?? false))
                            ccp.Enabled = true;

                        db.Entry(ccp).State = EntityState.Modified;
                    }
                    catch (Exception)
                    {
                        //no pivot; create a new one
                        CuttingConfigurationPivot ccp = new CuttingConfigurationPivot
                        {
                            Service = theItem.Id,
                            Enabled = true
                        };

                        int newGroupId = 0;
                        try
                        {
                            newGroupId = await db.CuttingConfigurationPivots.MaxAsync(x => x.GroupId);
                        }
                        catch (Exception) { }
                        finally
                        {
                            newGroupId++; //always increment newGroupId
                        }
                        ccp.GroupId = newGroupId;
                        db.CuttingConfigurationPivots.Add(ccp);
                    }
                }
            }

            await db.SaveChangesAsync();

            return new JsonResult { Data = "done", JsonRequestBehavior = JsonRequestBehavior.AllowGet };
        }

        public async Task<ActionResult> GetServiceForEdit(int? id)
        {
            if (id.HasValue)
            {
                Service s = await db.Services.Include(i => i.ServiceCategory).SingleOrDefaultAsync(x => x.Id == id.Value);

                if (s == null)
                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Cannot find that service");

                //s.ParseServiceConfiguration();

                await db.Entry(s).Collection(c => c.ServiceLinkedServices1).LoadAsync();

                foreach (ServiceLinkedService sls in s.ServiceLinkedServices1.ToList())
                    await db.Entry(sls).Reference(r => r.Service1).LoadAsync();

                ViewData["Service.CatNo"] = await GenerateSelectListAsync(SelectListType.ServiceCategories, s, "CatNo");
                ViewData["Service.TimeFormula"] = await GenerateSelectListAsync(SelectListType.Formulas, s, "TimeFormula");
                ViewData["Service.ProductionDepartment"] = await GenerateSelectListAsync(SelectListType.ProductionDepartments, s, "ProductionDepartment");

                VM_Service serviceVM = new VM_Service
                {
                    Service = s,
                    Services = await db.Services.Where(x => x.Deleted == null).ToListAsync()
                };

                return new JsonResult { Data = JsonConvert.SerializeObject(RenderViewToString("~/Views/Services/GetServiceForEdit.cshtml", serviceVM)), JsonRequestBehavior = JsonRequestBehavior.AllowGet };
            }

            return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "No id provided");
        }

        public async Task<ActionResult> GetJobItemServiceForEdit(int? id)
        {
            if (id.HasValue)
            {
                try
                {
                    JobItemService s = await db.JobItemServices.Include(i => i.Service).SingleAsync(x => x.Id == id.Value);

                    if (s == null)
                        return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, ("Cannot find record with id = " + id.Value));

                    //s.ParseServiceConfiguration();
                    s.FormattedServiceTime = DecimalHoursToFormatted(s.ServiceTime);

                    VM_JobItemService vm = new VM_JobItemService
                    {
                        JobItemService = s,
                        ServiceTimeEditor = new VM_TimeInput { FormattedServiceTime = s.FormattedServiceTime, ServiceTime = s.ServiceTime ?? 0m, LockTime = s.UserTimeOverride ?? false },
                        TimingParametersJson = s.FormulaConfiguration
                    };

                    if (s != null)
                        return new JsonResult { Data = JsonConvert.SerializeObject(RenderViewToString("~/Views/Services/GetJobItemServiceForEdit.cshtml", vm)), JsonRequestBehavior = JsonRequestBehavior.AllowGet };
                }
                catch (Exception)
                {
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Exception thrown");
                }
            }

            return null;
        }

        public async Task<ActionResult> SaveJobItemServiceOverride(JobItemService jobItemService)
        {
            //update the database record with quoteItemService
            JobItemService s = await db.JobItemServices.FindAsync(jobItemService.Id);

            if (s == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, ("Cannot find record with id = " + jobItemService.Id));

            s = UpdateJobItemServiceRecord(s, jobItemService);
            db.Entry(s).State = EntityState.Modified;

            await db.SaveChangesAsync();

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK);
        }

        public async Task<ActionResult> GetQuoteItemServiceForEdit(int? id)
        {
            if (id.HasValue)
            {
                try
                {
                    QuoteItemService s = await db.QuoteItemServices.Include(i => i.Service).SingleAsync(x => x.Id == id.Value);

                    if (s == null)
                        return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, ("Cannot find record with id = " + id.Value));

                    //s.ParseServiceConfiguration();
                    s.FormattedServiceTime = DecimalHoursToFormatted(s.ServiceTime);

                    VM_QuoteItemService vm = new VM_QuoteItemService
                    {
                        QuoteItemService = s,
                        ServiceTimeEditor = new VM_TimeInput { FormattedServiceTime = s.FormattedServiceTime, ServiceTime = s.ServiceTime ?? 0m, LockTime = s.UserTimeOverride ?? false },
                        TimingParametersJson = s.FormulaConfiguration
                    };

                    return new JsonResult { Data = JsonConvert.SerializeObject(RenderViewToString("~/Views/Services/GetQuoteItemServiceForEdit.cshtml", vm)), JsonRequestBehavior = JsonRequestBehavior.AllowGet };
                }
                catch (Exception)
                {
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Exception thrown");
                }
            }

            return null;
        }

        public async Task<ActionResult> SaveQuoteItemServiceOverride(QuoteItemService quoteItemService)
        {
            //update the database record with quoteItemService
            QuoteItemService s = await db.QuoteItemServices.FindAsync(quoteItemService.Id);

            if (s == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, ("Cannot find record with id = " + quoteItemService.Id));

            s = UpdateQuoteItemServiceRecord(s, quoteItemService);
            db.Entry(s).State = EntityState.Modified;

            await db.SaveChangesAsync();

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK);
        }

        public ActionResult Index()
        {
            return View();
        }

        public async Task<ActionResult> GetServices()
        {
            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(await GetServicesAsync(), new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> AddCuttingServices(List<CuttingService_CM> services, int rowId, string recordType)
        {
            switch (recordType)
            {
                case "QUOTE":
                    List<QuoteItemService> newQ = new List<QuoteItemService>();

                    foreach (CuttingService_CM qc in services)
                    {
                        CuttingConfiguration qcc = await db.CuttingConfigurations.FindAsync(qc.configId);
                        Service qs = await db.Services.FindAsync(qc.serviceId);

                        // MAS-20: Default General Cost to $63.50 for Aluminum and Metal Document cutting configs
                        decimal? defaultMaterialCost = null;
                        if (qcc?.DisplayText != null &&
                            (qcc.DisplayText.StartsWith("Aluminum", StringComparison.OrdinalIgnoreCase) ||
                             qcc.DisplayText.StartsWith("Metal Document", StringComparison.OrdinalIgnoreCase)))
                        {
                            defaultMaterialCost = 63.50m;
                        }

                        newQ.Add(new QuoteItemService
                        {
                            OriginalService = qc.serviceId,
                            Ordinal = 1,
                            Notes = qs.Notes,
                            Caliper = qs.Caliper,
                            HourlyRate = qs.BasePrice,
                            MaterialCost = defaultMaterialCost,
                            MakeReady = qs.MakeReady,
                            WashUp = qs.WashUp,
                            ApexOrdinal = 1,
                            IsCuttingService = true,
                            FormulaConfiguration = qcc.Configuration,
                            QuoteItemId = rowId,
                            Title = qs.Title
                        });

                        db.QuoteItemServices.Add(newQ.Last());
                    }

                    await db.SaveChangesAsync();

                    foreach (QuoteItemService qis in newQ)
                    {
                        await db.Entry(qis).ReloadAsync();
                    }

                    var jsonQ = new
                    {
                        services = RenderViewToString("~/Views/Partials/_ItemServiceRows.cshtml", new VM_ItemServices
                        {
                            ServiceList =
                                    newQ
                                    .OrderBy(x => x.Ordinal)
                                    .Select(x => new VM_ItemServiceRow
                                    {
                                        RecordId = x.Id,
                                        ServiceTitle = x.Service.Title,
                                        Ordinal = x.Ordinal,
                                        ServiceId = x.OriginalService,
                                        Notes = x.Notes,
                                        Required = x.Service.Required ?? false,
                                        IsCuttingService = x.IsCuttingService ?? false
                                    }).ToList()
                        })
                    };

                    return new JsonResult
                    {
                        Data = JsonConvert.SerializeObject(jsonQ,
                                    new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };

                case "JOB":
                    List<JobItemService> newJ = new List<JobItemService>();

                    foreach (CuttingService_CM jc in services)
                    {
                        CuttingConfiguration jcc = await db.CuttingConfigurations.FindAsync(jc.configId);
                        Service js = await db.Services.FindAsync(jc.serviceId);

                        // MAS-20: Default General Cost to $63.50 for Aluminum and Metal Document cutting configs
                        decimal? defaultMaterialCost = null;
                        if (jcc?.DisplayText != null &&
                            (jcc.DisplayText.StartsWith("Aluminum", StringComparison.OrdinalIgnoreCase) ||
                             jcc.DisplayText.StartsWith("Metal Document", StringComparison.OrdinalIgnoreCase)))
                        {
                            defaultMaterialCost = 63.50m;
                        }

                        newJ.Add(new JobItemService
                        {
                            ServiceId = jc.serviceId,
                            Ordinal = 1,
                            Notes = js.Notes,
                            Caliper = js.Caliper,
                            HourlyRate = js.BasePrice,
                            MaterialCost = defaultMaterialCost,
                            MakeReady = js.MakeReady,
                            WashUp = js.WashUp,
                            ApexOrdinal = 1,
                            IsCuttingService = true,
                            FormulaConfiguration = jcc.Configuration,
                            JobItemId = rowId,
                            Title = js.Title
                        });

                        db.JobItemServices.Add(newJ.Last());
                    }

                    await db.SaveChangesAsync();

                    foreach (JobItemService jis in newJ)
                    {
                        await db.Entry(jis).ReloadAsync();
                    }

                    var jsonJ = new
                    {
                        services = RenderViewToString("~/Views/Partials/_ItemServiceRows.cshtml", new VM_ItemServices
                        {
                            ServiceList =
                                    newJ
                                    .OrderBy(x => x.Ordinal)
                                    .Select(x => new VM_ItemServiceRow
                                    {
                                        RecordId = x.Id,
                                        ServiceTitle = x.Service.Title,
                                        Ordinal = x.Ordinal ?? 0,
                                        ServiceId = x.ServiceId,
                                        Notes = x.Notes,
                                        Required = x.Service.Required ?? false,
                                        IsCuttingService = x.IsCuttingService ?? false
                                    }).ToList()
                        })
                    };

                    return new JsonResult
                    {
                        Data = JsonConvert.SerializeObject(jsonJ,
                                    new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };

                default:
                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Invalid record type");
            }
        }

        public async Task<ActionResult> GetCuttingServices()
        {
            List<VM_ServiceAndCuttingConfigurations> listOfServicesAndConfigs = new List<VM_ServiceAndCuttingConfigurations>();

            List<Service> CuttingServices = await db.Services.Where(x => (x.IsCuttingService ?? false)).ToListAsync();
            foreach (Service s in CuttingServices)
            {
                await db.Entry(s).Collection(c => c.CuttingConfigurationPivots).LoadAsync();
                int groupId = s.CuttingConfigurationPivots.First().GroupId;

                List<CuttingConfiguration> cc = await db.CuttingConfigurations.Where(x => x.GroupId == groupId).ToListAsync();

                listOfServicesAndConfigs.Add(new VM_ServiceAndCuttingConfigurations
                {
                    Service = s,
                    Configurations = cc
                });
            }
            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(RenderViewToString("~/Views/Services/_CuttingServiceList.cshtml", listOfServicesAndConfigs), new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> GetCuttingServiceConfiguration(List<int> serIds)
        {
            serIds = serIds ?? new List<int>();

            List<List<VM_CuttingConfiguration>> configurations = new List<List<VM_CuttingConfiguration>>();

            foreach (int serId in serIds)
            {
                try
                {
                    CuttingConfigurationPivot ccp = await db.CuttingConfigurationPivots.SingleAsync(x => x.Service == serId);
                    await db.Entry(ccp).Reference(r => r.Service1).LoadAsync();
                    List<CuttingConfiguration> ccList = await db.CuttingConfigurations.Where(x => x.GroupId == ccp.GroupId).ToListAsync();
                    List<VM_CuttingConfiguration> vm_ccList = new List<VM_CuttingConfiguration>();

                    foreach (CuttingConfiguration cc in ccList)
                    {
                        // Deserialize JSON into a nested Dictionary<string, object>
                        var configData = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, object>>>(cc.Configuration);

                        //ZUND
                        if (configData.TryGetValue("Zund_Config", out var zundConfig))
                        {
                            if (zundConfig.TryGetValue("Knife_Speed", out var knifeSpeedObj) && zundConfig.TryGetValue("Linear", out var linearObj))
                            {
                                var knifeSpeed = Convert.ToInt32(knifeSpeedObj);
                                var isLinear = linearObj.ToString() == "1"; // True if "1", False otherwise

                                // Determine the appropriate unit based on whether it's linear or not
                                string unit = isLinear ? "ft/hr" : "sqft/hr"; // Replace with your actual logic

                                vm_ccList.Add(new VM_CuttingConfiguration
                                {
                                    RecordId = cc.Id,
                                    SpeedDisplay = $"{cc.DisplayText}",
                                    SpeedValue = knifeSpeed,
                                    CuttingServiceName = ccp.Service1.Title
                                });
                            }
                        }
                        //GUILLOTINE
                        else if (configData.TryGetValue("Linear_Config", out var linearConfig))
                        {
                            if (linearConfig.TryGetValue("Seconds_Per_Cut", out var secondsPerCutObj))
                            {
                                var secondsPerCut = Convert.ToInt32(secondsPerCutObj);

                                vm_ccList.Add(new VM_CuttingConfiguration
                                {
                                    RecordId = cc.Id,
                                    SpeedDisplay = $"{secondsPerCut} Seconds Per Cut",
                                    SpeedValue = secondsPerCut,
                                    CuttingServiceName = ccp.Service1.Title
                                });
                            }
                        }
                        //NOTHING
                        else
                        {
                            Console.WriteLine("Invalid JSON structure");
                        }
                    }

                    configurations.Add(vm_ccList);
                }
                catch (Exception ex)
                {
                    // Log error and continue processing other IDs
                    Console.WriteLine($"Error processing service ID {serId}: {ex.Message}");
                    continue;
                }
            }

            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(RenderViewToString("~/Views/Partials/_CuttingConfigSelect.cshtml", configurations), new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> UpdateItemCuttingServiceConfigurations(int[] itemServiceIds, string recordType, int[] configIds)
        {
            var indexCount = itemServiceIds.Length;

            switch (recordType.ToUpper())
            {
                case "QUOTE":
                    for (int q = 0; q < indexCount; q++)
                    {
                        QuoteItemService qis = await db.QuoteItemServices.FindAsync(itemServiceIds[q]);

                        if (qis != null)
                            db.SetItemServiceConfiguration(qis.Id, "Quote", configIds[q]);
                    }
                    break;

                case "JOB":
                    for (int j = 0; j < indexCount; j++)
                    {
                        JobItemService jis = await db.JobItemServices.FindAsync(itemServiceIds[j]);

                        if (jis != null)
                            db.SetItemServiceConfiguration(jis.Id, "Job", configIds[j]);
                    }
                    break;

                default:
                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Invalid record type");
            }

            return new JsonResult
            {
                Data = "done",
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> FormulaParameterEditor(int? id)
        {
            FormulaConfiguration fc = await db.FormulaConfigurations.FindAsync(id);
            if (fc != null)
            {
                ViewBag.Json = fc.DefaultConfig;
                ViewBag.Prefix = fc.Prefix;
                ViewBag.RecordId = fc.Id;
                return View();
            }

            return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
        }

        public async Task<ActionResult> GetFormulaParameters(int? id, string recordType)
        {
            switch (recordType.ToUpper())
            {
                case "QUOTE":
                    QuoteItemService qis = await db.QuoteItemServices.FindAsync(id);

                    if (qis != null)
                    {
                        await db.Entry(qis).Reference(r => r.Service).LoadAsync();

                        switch ((Calculations.Formulas)qis.Service.TimeFormula)
                        {
                            case Calculations.Formulas.LinearCutting:
                                var lcc = JsonConvert.DeserializeObject<Dictionary<string, LinearCuttingConfig>>(qis.FormulaConfiguration);

                                return new JsonResult
                                {
                                    Data = JsonConvert.SerializeObject(lcc, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                                };

                            case Calculations.Formulas.ZundCutting:
                                var zc = JsonConvert.DeserializeObject<Dictionary<string, ZundCuttingConfig>>(qis.FormulaConfiguration);

                                return new JsonResult
                                {
                                    Data = JsonConvert.SerializeObject(zc, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                                };

                            case Calculations.Formulas.ThreeHolePunch:
                                var thp = JsonConvert.DeserializeObject<Dictionary<string, ThreeHolePunchConfig>>(qis.FormulaConfiguration);

                                return new JsonResult
                                {
                                    Data = JsonConvert.SerializeObject(thp, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                                };

                            case Calculations.Formulas.PressPlates:
                                var pp = JsonConvert.DeserializeObject<Dictionary<string, PressPlatesConfig>>(qis.FormulaConfiguration);

                                return new JsonResult
                                {
                                    Data = JsonConvert.SerializeObject(pp, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                                };

                            case Calculations.Formulas.VariableHolePunch:
                                var vhp = JsonConvert.DeserializeObject<Dictionary<string, VariableHolePunchConfig>>(qis.FormulaConfiguration);

                                return new JsonResult
                                {
                                    Data = JsonConvert.SerializeObject(vhp, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                                };

                            case Calculations.Formulas.Folding:
                                var f = JsonConvert.DeserializeObject<Dictionary<string, FoldingTimeConfig>>(qis.FormulaConfiguration);

                                return new JsonResult
                                {
                                    Data = JsonConvert.SerializeObject(f, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                                };

                            case Calculations.Formulas.Scoring:
                                var s = JsonConvert.DeserializeObject<Dictionary<string, ScoringTimeConfig>>(qis.FormulaConfiguration);

                                return new JsonResult
                                {
                                    Data = JsonConvert.SerializeObject(s, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                                };

                            case Calculations.Formulas.Polybagging:
                                var pb = JsonConvert.DeserializeObject<Dictionary<string, PolybaggingConfig>>(qis.FormulaConfiguration);

                                return new JsonResult
                                {
                                    Data = JsonConvert.SerializeObject(pb, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                                };

                            case Calculations.Formulas.ShrinkWrapping:
                                var sw = JsonConvert.DeserializeObject<Dictionary<string, ShrinkWrappingConfig>>(qis.FormulaConfiguration);

                                return new JsonResult
                                {
                                    Data = JsonConvert.SerializeObject(sw, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                                };

                            case Calculations.Formulas.BulkPack:
                                var bp = JsonConvert.DeserializeObject<Dictionary<string, BulkPackConfig>>(qis.FormulaConfiguration);

                                return new JsonResult
                                {
                                    Data = JsonConvert.SerializeObject(bp, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                                };

                            case Calculations.Formulas.KitPack:
                                var kp = JsonConvert.DeserializeObject<Dictionary<string, KitPackConfig>>(qis.FormulaConfiguration);

                                return new JsonResult
                                {
                                    Data = JsonConvert.SerializeObject(kp, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                                };

                            case Calculations.Formulas.Preflight:
                                var pf = JsonConvert.DeserializeObject<Dictionary<string, PreflightConfig>>(qis.FormulaConfiguration);

                                return new JsonResult
                                {
                                    Data = JsonConvert.SerializeObject(pf, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                                };

                            case Calculations.Formulas.Perforate:
                                var pfate = JsonConvert.DeserializeObject<Dictionary<string, PerforateConfig>>(qis.FormulaConfiguration);

                                return new JsonResult
                                {
                                    Data = JsonConvert.SerializeObject(pfate, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                                };

                            case Calculations.Formulas.SecondsPerPiece:
                                var sppiece = JsonConvert.DeserializeObject<Dictionary<string, GeneralSecondsConfig>>(qis.FormulaConfiguration);
                                return new JsonResult
                                {
                                    Data = JsonConvert.SerializeObject(sppiece, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                                };

                            case Calculations.Formulas.MinutesPerPiece:
                                var mppiece = JsonConvert.DeserializeObject<Dictionary<string, GeneralMinutesConfig>>(qis.FormulaConfiguration);
                                return new JsonResult
                                {
                                    Data = JsonConvert.SerializeObject(mppiece, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                                };

                            case Calculations.Formulas.Laminate:
                                var qlam = JsonConvert.DeserializeObject<Dictionary<string, LaminateConfig>>(qis.FormulaConfiguration);
                                return new JsonResult
                                {
                                    Data = JsonConvert.SerializeObject(qlam, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                                };

                            default:
                                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Bad Formula type");
                        }
                    }

                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Cannot find that item");

                case "JOB":
                    JobItemService jis = await db.JobItemServices.FindAsync(id);

                    if (jis != null)
                    {
                        await db.Entry(jis).Reference(r => r.Service).LoadAsync();

                        switch ((Calculations.Formulas)jis.Service.TimeFormula)
                        {
                            case Calculations.Formulas.LinearCutting:
                                var lcc = JsonConvert.DeserializeObject<Dictionary<string, LinearCuttingConfig>>(jis.FormulaConfiguration);

                                return new JsonResult
                                {
                                    Data = JsonConvert.SerializeObject(lcc, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                                };

                            case Calculations.Formulas.ZundCutting:
                                var zc = JsonConvert.DeserializeObject<Dictionary<string, ZundCuttingConfig>>(jis.FormulaConfiguration);

                                return new JsonResult
                                {
                                    Data = JsonConvert.SerializeObject(zc, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                                };

                            case Calculations.Formulas.ThreeHolePunch:
                                var thp = JsonConvert.DeserializeObject<Dictionary<string, ThreeHolePunchConfig>>(jis.FormulaConfiguration);

                                return new JsonResult
                                {
                                    Data = JsonConvert.SerializeObject(thp, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                                };

                            case Calculations.Formulas.PressPlates:
                                var pp = JsonConvert.DeserializeObject<Dictionary<string, PressPlatesConfig>>(jis.FormulaConfiguration);

                                return new JsonResult
                                {
                                    Data = JsonConvert.SerializeObject(pp, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                                };

                            case Calculations.Formulas.VariableHolePunch:
                                var vhp = JsonConvert.DeserializeObject<Dictionary<string, VariableHolePunchConfig>>(jis.FormulaConfiguration);

                                return new JsonResult
                                {
                                    Data = JsonConvert.SerializeObject(vhp, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                                };

                            case Calculations.Formulas.Folding:
                                var f = JsonConvert.DeserializeObject<Dictionary<string, FoldingTimeConfig>>(jis.FormulaConfiguration);

                                return new JsonResult
                                {
                                    Data = JsonConvert.SerializeObject(f, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                                };

                            case Calculations.Formulas.Scoring:
                                var s = JsonConvert.DeserializeObject<Dictionary<string, ScoringTimeConfig>>(jis.FormulaConfiguration);

                                return new JsonResult
                                {
                                    Data = JsonConvert.SerializeObject(s, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                                };

                            case Calculations.Formulas.Polybagging:
                                var pb = JsonConvert.DeserializeObject<Dictionary<string, PolybaggingConfig>>(jis.FormulaConfiguration);

                                return new JsonResult
                                {
                                    Data = JsonConvert.SerializeObject(pb, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                                };

                            case Calculations.Formulas.ShrinkWrapping:
                                var sw = JsonConvert.DeserializeObject<Dictionary<string, ShrinkWrappingConfig>>(jis.FormulaConfiguration);

                                return new JsonResult
                                {
                                    Data = JsonConvert.SerializeObject(sw, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                                };

                            case Calculations.Formulas.BulkPack:
                                var bp = JsonConvert.DeserializeObject<Dictionary<string, BulkPackConfig>>(jis.FormulaConfiguration);

                                return new JsonResult
                                {
                                    Data = JsonConvert.SerializeObject(bp, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                                };

                            case Calculations.Formulas.KitPack:
                                var kp = JsonConvert.DeserializeObject<Dictionary<string, KitPackConfig>>(jis.FormulaConfiguration);

                                return new JsonResult
                                {
                                    Data = JsonConvert.SerializeObject(kp, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                                };

                            case Calculations.Formulas.Preflight:
                                var pf = JsonConvert.DeserializeObject<Dictionary<string, PreflightConfig>>(jis.FormulaConfiguration);

                                return new JsonResult
                                {
                                    Data = JsonConvert.SerializeObject(pf, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                                };

                            case Calculations.Formulas.Perforate:
                                var pfate = JsonConvert.DeserializeObject<Dictionary<string, PerforateConfig>>(jis.FormulaConfiguration);

                                return new JsonResult
                                {
                                    Data = JsonConvert.SerializeObject(pfate, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                                };

                            case Calculations.Formulas.SecondsPerPiece:
                                var sppiece = JsonConvert.DeserializeObject<Dictionary<string, GeneralSecondsConfig>>(jis.FormulaConfiguration);
                                return new JsonResult
                                {
                                    Data = JsonConvert.SerializeObject(sppiece, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                                };

                            case Calculations.Formulas.MinutesPerPiece:
                                var mppiece = JsonConvert.DeserializeObject<Dictionary<string, GeneralMinutesConfig>>(jis.FormulaConfiguration);
                                return new JsonResult
                                {
                                    Data = JsonConvert.SerializeObject(mppiece, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                                };

                            case Calculations.Formulas.Laminate:
                                var jlam = JsonConvert.DeserializeObject<Dictionary<string, LaminateConfig>>(jis.FormulaConfiguration);
                                return new JsonResult
                                {
                                    Data = JsonConvert.SerializeObject(jlam, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                                };

                            default:
                                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Bad Formula type");
                        }
                    }

                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Cannot find that item");

                default:
                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Bad Item type");
            }
        }

        public async Task<ActionResult> UpdateFormulaConfig(int? id, string config, string modelType, string itemType)
        {
            // Correctly format the config string as JSON
            string configX = config.Replace("=", "\":\"").Replace("&", "\",\"");
            string fullConfig = "{\"" + modelType + "\":" + configX + "}";

            string configString = "";
            try
            {
                switch (modelType.ToUpper())
                {
                    case "LINEAR_CONFIG":
                        var lcDict = JsonConvert.DeserializeObject<Dictionary<string, LinearCuttingConfig>>(fullConfig);
                        configString = JsonConvert.SerializeObject(lcDict);
                        break;

                    case "ZUND_CONFIG":
                        var zcDict = JsonConvert.DeserializeObject<Dictionary<string, ZundCuttingConfig>>(fullConfig);
                        configString = JsonConvert.SerializeObject(zcDict);
                        break;

                    case "PLATE_CONFIG":
                        var pcDict = JsonConvert.DeserializeObject<Dictionary<string, PressPlatesConfig>>(fullConfig);
                        configString = JsonConvert.SerializeObject(pcDict);
                        break;

                    case "PUNCH_CONFIG":
                        try
                        {
                            var vhpcDict = JsonConvert.DeserializeObject<Dictionary<string, VariableHolePunchConfig>>(fullConfig);
                            configString = JsonConvert.SerializeObject(vhpcDict);
                        }
                        catch (Exception)
                        {
                            var thpcDict = JsonConvert.DeserializeObject<Dictionary<string, ThreeHolePunchConfig>>(fullConfig);
                            configString = JsonConvert.SerializeObject(thpcDict);
                        }
                        break;

                    case "FOLD_CONFIG":
                        var fcDict = JsonConvert.DeserializeObject<Dictionary<string, FoldingTimeConfig>>(fullConfig);
                        configString = JsonConvert.SerializeObject(fcDict);
                        break;

                    case "SCORE_CONFIG":
                        var scDict = JsonConvert.DeserializeObject<Dictionary<string, ScoringTimeConfig>>(fullConfig);
                        configString = JsonConvert.SerializeObject(scDict);
                        break;

                    case "BAGGING_CONFIG":
                        var pbcDict = JsonConvert.DeserializeObject<Dictionary<string, PolybaggingConfig>>(fullConfig);
                        configString = JsonConvert.SerializeObject(pbcDict);
                        break;

                    case "SHRINK_CONFIG":
                        var swcDict = JsonConvert.DeserializeObject<Dictionary<string, ShrinkWrappingConfig>>(fullConfig);
                        configString = JsonConvert.SerializeObject(swcDict);
                        break;

                    case "PACKING_CONFIG":
                        var bpcDict = JsonConvert.DeserializeObject<Dictionary<string, BulkPackConfig>>(fullConfig);
                        configString = JsonConvert.SerializeObject(bpcDict);
                        break;

                    case "KIT_CONFIG":
                        var kpcDict = JsonConvert.DeserializeObject<Dictionary<string, KitPackConfig>>(fullConfig);
                        configString = JsonConvert.SerializeObject(kpcDict);
                        break;

                    case "PREFLIGHT_CONFIG":
                        var pfcDict = JsonConvert.DeserializeObject<Dictionary<string, PreflightConfig>>(fullConfig);
                        configString = JsonConvert.SerializeObject(pfcDict);
                        break;

                    case "PERFORATE_CONFIG":
                        var pftDict = JsonConvert.DeserializeObject<Dictionary<string, PerforateConfig>>(fullConfig);
                        configString = JsonConvert.SerializeObject(pftDict);
                        break;

                    case "DIE_CONFIG":
                        var dcDict = JsonConvert.DeserializeObject<Dictionary<string, DieCutConfig>>(fullConfig);
                        configString = JsonConvert.SerializeObject(dcDict);
                        break;

                    case "SCRAPPING_CONFIG":
                        var scrcDict = JsonConvert.DeserializeObject<Dictionary<string, ScrappingConfig>>(fullConfig);
                        configString = JsonConvert.SerializeObject(scrcDict);
                        break;

                    case "LAMINATE_CONFIG":
                        var lamDict = JsonConvert.DeserializeObject<Dictionary<string, LaminateConfig>>(fullConfig);
                        configString = JsonConvert.SerializeObject(lamDict);
                        break;

                    case "GENERAL_SECONDS_CONFIG":
                        var gscDict = JsonConvert.DeserializeObject<Dictionary<string, GeneralSecondsConfig>>(fullConfig);
                        configString = JsonConvert.SerializeObject(gscDict);
                        break;

                    case "GENERAL_MINUTES_CONFIG":
                        var gmcDict = JsonConvert.DeserializeObject<Dictionary<string, GeneralMinutesConfig>>(fullConfig);
                        configString = JsonConvert.SerializeObject(gmcDict);
                        break;
                }

                switch (itemType.ToUpper())
                {
                    case "QUOTE":
                        var qis = await db.Set<QuoteItemService>().FindAsync(id);
                        qis.FormulaConfiguration = configString;
                        db.Entry(qis).State = EntityState.Modified;
                        break;

                    case "JOB":
                        var jis = await db.Set<JobItemService>().FindAsync(id);
                        jis.FormulaConfiguration = configString;
                        db.Entry(jis).State = EntityState.Modified;
                        break;

                    default:
                        break;
                }

                await db.SaveChangesAsync();
            }
            catch (Exception)
            {
                // Handle exceptions if necessary
            }

            return new JsonResult
            {
                Data = "done",
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }
    }
}