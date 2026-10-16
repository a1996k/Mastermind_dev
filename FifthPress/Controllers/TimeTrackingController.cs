using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Threading.Tasks;
using FifthPress.ActionFilters;
using FifthPress.Models.ViewModels;
using System.Data.Entity;
using Newtonsoft.Json;
using FifthPress.Models;
using System.Configuration;
using System.Text;
using System.Net;
using System.Security.Cryptography;
using System.Data.Entity.Infrastructure;
using System.Web.Http.Results;

namespace FifthPress.Controllers
{
    [CookieFilter(Order = 1)]
    [UserPermissions(Order = 2)]
    public class TimeTrackingController : DataController
    {
        public async Task<ActionResult> EditTimeEntry(int? id)
        {
            int userId = UserSessionHelper.GetActiveUserId(HttpContext);

            try
            {
                VM_EditTimeEntry vm = new VM_EditTimeEntry();

                ////determine if the user has permission to edit
                PermissionsManifest pme = await db.PermissionsManifests.SingleAsync(x => x.Title == "Edit Time");
                User u = await db.Users.FindAsync(userId);
                await db.Entry(u).Reference(r => r.Role1).LoadAsync();
                vm.HasPermission = u.Role1.PermissionString.Contains(pme.PermissionId);

                JobTimingSR jt = await db.JobTimingSRs.FindAsync(id);

                if (jt != null)
                {
                    await db.Entry(jt).Reference(r => r.Job1).LoadAsync();
                    await db.Entry(jt).Reference(r => r.JobItem1).LoadAsync();
                    await db.Entry(jt).Reference(r => r.JobItemService1).LoadAsync();
                    vm.Id = jt.Id;
                    vm.JobDescription = jt.Job1.Description;
                    vm.ItemDescription = jt.JobItem1.Description;
                    vm.ServiceTitle = jt.JobItemService1.Title;
                    vm.StartTime = jt.StartTimeUTC.Value.ToLocalTime();
                    vm.HasEndTime = jt.EndTimeUTC.HasValue;
                    vm.EndTime = jt.EndTimeUTC.HasValue ? jt.EndTimeUTC.Value.ToLocalTime() : DateTime.MinValue;

                    return new JsonResult
                    {
                        Data = new
                        {
                            Html = RenderViewToString("~/Views/Reports/Modals/_EditTimeModal.cshtml", vm),
                        },
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };
                }
                else
                {
                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Cannot find JobTimingSR");
                }
            }
            catch (Exception)
            {
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Critical Error: Cannot find JobTimingSR");
            }
        }

        public async Task<ActionResult> SaveTimeEdit(VM_EditTimeEntry entry)
        {
            try
            {
                int userId = UserSessionHelper.GetActiveUserId(HttpContext);

                //get job timing entry, serialize existing properties. change and serialize again
                JobTimingSR existing = await db.JobTimingSRs.FindAsync(entry.Id);
                string curSerialzied = JsonConvert.SerializeObject(existing, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
                //update the actual timing record
                existing.StartTimeUTC = entry.StartTime.ToUniversalTime();
                existing.EndTimeUTC = entry.EndTime.HasValue ? entry.EndTime.Value.ToUniversalTime() : (DateTime?)null;
                db.Entry(existing).State = EntityState.Modified;
                string newSerialized = JsonConvert.SerializeObject(existing, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });

                //new edit record
                db.TimeRecordEdits.Add(new TimeRecordEdit
                {
                    JobTiming = entry.Id,
                    PreviousRecordDetailsJSON = curSerialzied,
                    NewRecordDetailsJSON = newSerialized,
                    AdjustedBy = userId
                });

                await db.SaveChangesAsync();
                //save and return
                return new System.Web.Mvc.RedirectResult(Request.UrlReferrer.ToString());
            }
            catch (Exception) { }

            return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Critical Error: Cannot find JobTimingSR");
        }

        public async Task<ActionResult> StartServiceCheck()
        {
            int user = -1;
            user = UserSessionHelper.GetActiveUserId(HttpContext);

            User u = await db.Users.FindAsync(user);
            if (u != null)
            {
                var activeServices =
                    await db.JobTimingSRs
                        .Where(x => x.EndTimeUTC == null && x.UserId == u.Id)
                        .Include(i => i.Job1)
                        .Include(i => i.JobItem1)
                        .Include(i => i.JobItemService1)
                        .ToListAsync();

                if (activeServices.Count > 0)
                {
                    return new JsonResult
                    {
                        Data = new
                        {
                            showPrompt = true,
                            services = activeServices.Select(x => new
                            {
                                x.Id,
                                x.Job1.JobNumber,
                                x.JobItem1.Ordinal,
                                x.JobItemService1.Title,
                                JobItemServiceId = x.JobItemService,
                                StartTime = x.StartTimeUTC.Value.ToLocalTime().ToString("M/d/yyyy h:mm tt")
                            })
                        },
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };
                }
                else
                {
                    return new JsonResult
                    {
                        Data = new
                        {
                            showPrompt = false
                        },
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };
                }
            }

            return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Problem retrieving user");
        }

        private async Task<ActionResult> SetItemAsCompleteIfAllServicesComplete(JobItem jobItem)
        {
            if (jobItem == null)
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Cannot find JobItem");

            await db.Entry(jobItem).Collection(c => c.JobItemServices).LoadAsync();

            if (jobItem.JobItemServices.All(x => x.Complete.HasValue))
            {
                jobItem.Complete = DateTime.UtcNow;
                db.Entry(jobItem).State = EntityState.Modified;
                await db.SaveChangesAsync();
            }

            return new JsonResult { Data = "done", JsonRequestBehavior = JsonRequestBehavior.AllowGet };
        }

        private async Task<ActionResult> SetJobAsReadyToShipIfNoMoreServicesToComplete(Job job)
        {
            if (job == null)
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Cannot find Job");

            await db.Entry(job).Collection(c => c.JobItems).LoadAsync();

            if (job.JobItems.All(x => x.Complete.HasValue))
            {
                job.Complete = DateTime.UtcNow;
                db.Entry(job).State = EntityState.Modified;
                await db.SaveChangesAsync();
            }

            return new JsonResult { Data = "done", JsonRequestBehavior = JsonRequestBehavior.AllowGet };
        }

        public async Task<ActionResult> TaskStateToggle(int? id, bool? complete)
        {
            if (!id.HasValue || !complete.HasValue)
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Invalid request");

            JobItemService jis = await db.JobItemServices.FindAsync(id);
            await db.Entry(jis).Reference(r => r.JobItem).LoadAsync();
            await db.Entry(jis.JobItem).Reference(r => r.Job1).LoadAsync();

            if (complete.Value)
                jis.Complete = DateTime.UtcNow;
            else
                jis.Complete = null;

            db.Entry(jis).State = EntityState.Modified;

            await db.SaveChangesAsync();

            await SetItemAsCompleteIfAllServicesComplete(jis.JobItem);
            await SetJobAsReadyToShipIfNoMoreServicesToComplete(jis.JobItem.Job1);

            return new HttpStatusCodeResult(HttpStatusCode.OK);
        }

        //NEW start task
        public async Task<ActionResult> StartTask(int? serviceId)
        {
            int userId = 0;
            userId = UserSessionHelper.GetActiveUserId(HttpContext);

            User u = await db.Users.FindAsync(userId);

            if (u != null)
            {
                JobItemService jis = await db.JobItemServices.FindAsync(serviceId);
                await db.Entry(jis).Reference(r => r.JobItem).LoadAsync();

                try
                {
                    JobTimingSR potentialExisting = await db.JobTimingSRs.SingleAsync(x =>
                                x.Job == jis.JobItem.Job &&
                                x.JobItemService == jis.Id &&
                                x.UserId == userId &&
                                x.EndTimeUTC == null);
                    //record already exists, so do nothing

                    return new JsonResult
                    {
                        Data = "Clock already started",
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };
                }
                catch (Exception)
                {
                    //negative(positive) result; create the new record because one doesn't exist

                    try
                    {
                        JobItem jobItem = jis.JobItem;
                        JobItemService jobItemService = jis;

                        JobTimingSR newJt = new JobTimingSR
                        {
                            Job = jobItem.Job,
                            JobItem = jobItemService.JobItemId,
                            JobItemService = jobItemService.Id,
                            StartTimeUTC = DateTime.UtcNow,
                            UserId = userId
                        };

                        db.JobTimingSRs.Add(newJt);

                        await db.SaveChangesAsync();

                        //return card for sidenav as well?

                        return new JsonResult
                        {
                            Data =
                                JsonConvert.SerializeObject(new
                                {
                                    NeedsQACheck = HasPendingQACheck(),
                                    IsPrintQA = IsPendingQAForPrintService()
                                }, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }
                            ),
                            JsonRequestBehavior = JsonRequestBehavior.AllowGet
                        };
                    }
                    catch (Exception)
                    {
                        //cant find service or item; either way, bad news
                        return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Cannot find JobItem or JobItemService");
                    }
                }
            }

            return new HttpStatusCodeResult(HttpStatusCode.InternalServerError);
        }

        //new stop task
        public async Task<ActionResult> StopTask(int? serviceId)
        {
            int userId = 0;
            userId = UserSessionHelper.GetActiveUserId(HttpContext);

            User u = await db.Users.FindAsync(userId);

            if (u != null)
            {
                try
                {
                    JobTimingSR existing = await db.JobTimingSRs.SingleAsync(x => x.UserId == userId && x.JobItemService == serviceId && x.EndTimeUTC == null);
                    existing.EndTimeUTC = DateTime.UtcNow;
                    db.Entry(existing).State = EntityState.Modified;
                    await db.SaveChangesAsync();

                    Job j = await db.Jobs.FindAsync(existing.Job);

                    db.UpdateServiceTotalWorkedTime(serviceId);

                    return new HttpStatusCodeResult(HttpStatusCode.OK);
                }
                catch (Exception)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError);
                }
            }

            return new HttpStatusCodeResult(HttpStatusCode.InternalServerError);
        }

        //use the following function to perform tasks at the moment of completion (pre-complete)
        public async Task<ActionResult> CompletePromptCheck(int? id, bool complete)
        {
            if (complete)
            {
                JobItemService jis = await db.JobItemServices.FindAsync(id);
                if (jis != null)
                {
                    return new JsonResult
                    {
                        Data = "",
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };
                }

                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Critical Error: Service Not Found");
            }
            else
            {
                return new JsonResult
                {
                    Data = "",
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
        }

        public async Task<ActionResult> MaterialPullPromptSubmit(List<PullMaterial> pulls, int jobId, int jobItemId)
        {
            foreach (PullMaterial pm in pulls.ToList())
            {
                await RunMaterialPull(pm.MaterialId, pm.AmountPulled, jobId, jobItemId);
            }

            return new JsonResult
            {
                Data = "done",
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        //jobId = required, itemId = optional. load first item if itemId not provided.
        public async Task<ActionResult> Index(int? jobId, int? itemId)
        {
            //get user Id
            int userId = 0;
            userId = UserSessionHelper.GetActiveUserId(HttpContext);

            Job j = await db.Jobs.FindAsync(jobId);

            if (j != null)
            {
                VM_TimeTracking vm = new VM_TimeTracking();

                await db.Entry(j).Collection(c => c.JobItems).LoadAsync();

                //if no item id provided, set it to the id of the first item on this job
                if (!itemId.HasValue)
                {
                    itemId = j.JobItems.OrderBy(x => x.Ordinal).First().Id;
                }

                vm.Job = j;
                vm.JobItem = j.JobItems.Single(x => x.Id == itemId);
                vm.JobItem.ParseRunSize();

                //load service info for job item
                await db.Entry(vm.JobItem).Collection(c => c.JobItemServices).LoadAsync();

                //load base services
                foreach (JobItemService jis in vm.JobItem.JobItemServices.ToList())
                {
                    await db.Entry(jis).Reference(r => r.Service).LoadAsync();
                    await db.Entry(jis.Service).Reference(r => r.ServiceCategory).LoadAsync();
                    await db.Entry(jis.Service.ServiceCategory).Reference(r => r.ServiceType1).LoadAsync();
                    jis.FormattedServiceTime = DecimalHoursToFormatted(jis.ServiceTime ?? 0m);

                    //load timing records for this service
                    await db.Entry(jis).Collection(c => c.JobTimingSRs).LoadAsync();

                    //set is running based on any existing timing records for this user on this service
                    jis.UserContext_IsRunning = jis.JobTimingSRs.Any(x => x.UserId == userId && x.EndTimeUTC == null);
                    jis.ServiceComplete = jis.Complete.HasValue;

                    //if the service is running for this user,get the start time
                    if (jis.UserContext_IsRunning)
                        jis.UserContext_StartTimeUTC = jis.JobTimingSRs.Single(x => x.UserId == userId && x.EndTimeUTC == null).StartTimeUTC.Value;

                    if (jis.JobTimingSRs.Count > 0)
                    {
                        //total closed time on closed time records for all users (including this user)
                        jis.UserContext_TotalOverallTime = jis.JobTimingSRs
                            .Where(x => x.EndTimeUTC != null)
                            .Select(x => (decimal)(x.EndTimeUTC.Value - x.StartTimeUTC.Value).TotalHours)  // Calculate duration in hours
                            .Sum();  // Sum up all the durations

                        //total worked time on all records for this user
                        jis.UserContext_UserServiceTime = jis.JobTimingSRs
                            .Where(x => x.UserId == userId)
                            .Select(x => (decimal)((x.EndTimeUTC == null ? DateTime.UtcNow : x.EndTimeUTC.Value) - x.StartTimeUTC.Value).TotalHours)
                            .Sum();
                    }

                    jis.UserContext_TotalOverallTimeFormatted = DecimalHoursToFormatted(jis.UserContext_TotalOverallTime);
                    jis.UserContext_UserServiceTimeFormatted = DecimalHoursToFormatted(jis.UserContext_UserServiceTime);
                    jis.UserContext_UserElapsedMilliseconds = DecimalHoursToMilliseconds(jis.UserContext_UserServiceTime);

                    jis.UserContext_TotalRemainingTime = Math.Max((jis.ServiceTime ?? 0m) - jis.UserContext_TotalOverallTime, 0m);
                    jis.UserContext_TotalRemainingTimeFormatted = DecimalHoursToFormatted(jis.UserContext_TotalRemainingTime);
                }

                //get the other items
                vm.OtherItems = j.JobItems.Where(x => x.Id != itemId).Select(x => new Models.TimeTracking.OtherItem
                {
                    JobId = x.Job.Value,
                    ItemId = x.Id,
                    JobNumberDisplay = String.Format("{0}.{1}", x.Job1.JobNumber, x.Ordinal),
                    Description = x.Description,
                    Ordinal = x.Ordinal ?? -1
                }).ToList();

                //get list of service ids on this item
                List<int> JobItemServiceIds = vm.JobItem.JobItemServices.Select(x => x.Id).ToList();

                //get qa checks that are not currently open (Determined by second approval time)
                List<QACheck> checks =
                    await db.QAChecks
                        .Where(x => JobItemServiceIds.Contains((x.JobItemServiceId ?? 0)) && x.SecondApprovalTime != null)
                        .Include(i => i.JobItemService)
                        .Include(i => i.JobItemService.JobItem)
                        .Include(i => i.JobItemService.JobItem.Job1)
                        .Include(i => i.User)
                        .Include(i => i.User1)
                        .ToListAsync();

                vm.QARecords = checks.Select(x => new Models.QA.QARecord
                {
                    ServiceTitle = x.JobItemService.Title,
                    JobNumber = x.JobItemService.JobItem.Job1.JobNumber,
                    ItemNumber = x.JobItemService.JobItem.Ordinal ?? 0,
                    FirstApprovalTime = x.FirstApprovalTime.Value.ToLocalTime(),
                    FirstApprovalTimeString = x.FirstApprovalTime.Value.ToLocalTime().ToString("M/d/yyyy h:mm tt"),
                    SecondApprovalTime = x.SecondApprovalTime.Value.ToLocalTime(),
                    SecondApprovalTimeString = x.SecondApprovalTime.Value.ToLocalTime().ToString("M/d/yyyy h:mm tt"),
                    FirstApprovalUser = x.User.FirstName + " " + x.User.LastName,
                    SecondApprovalUser = x.SecondApprover == null ? x.User.FirstName + " " + x.User.LastName : x.User1.FirstName + " " + x.User1.LastName,
                    Passed = x.Passed ?? false,
                    Responses = JsonConvert.DeserializeObject<List<Models.QA.QA>>(x.ApprovalJson),
                    JobItemService = x.JobItemServiceId ?? 0,
                    Id = x.Id
                }).ToList();

                await db.Entry(vm.JobItem).Reference(r => r.Material1).LoadAsync();

                // Pre-compute finishing material pull status for each finishing material service
                // 0 = none pulled, 1 = partially pulled, 2 = fully pulled
                vm.FinishingMaterialPullStatus = new Dictionary<int, int>();
                await db.Entry(vm.JobItem).Collection(c => c.JobItemFinishingMaterials).LoadAsync();

                foreach (var jifm in vm.JobItem.JobItemFinishingMaterials)
                {
                    await db.Entry(jifm).Reference(r => r.Material1).LoadAsync();

                    int amountToPull = (int)(jifm.FinalCount ?? 0);

                    // Get pull history for this specific finishing material
                    var pullHistory = db.InventoryAdjustmentHistories
                        .Where(x => (x.WasMaterialPull ?? false)
                               && x.JobItem == vm.JobItem.Id
                               && x.Material == jifm.Material)
                        .ToList();

                    decimal pulledAmount = pullHistory.Sum(r => (r.OldStockAmount ?? 0) - (r.NewStockAmount ?? 0));
                    int remaining = Math.Max(0, amountToPull - (int)pulledAmount);

                    if (remaining == 0 && amountToPull > 0)
                        vm.FinishingMaterialPullStatus[jifm.Id] = 2; // fully pulled
                    else if (pulledAmount > 0)
                        vm.FinishingMaterialPullStatus[jifm.Id] = 1; // partially pulled
                    else
                        vm.FinishingMaterialPullStatus[jifm.Id] = 0; // none pulled
                }

                // Pre-compute primary material pull status
                if (vm.JobItem.Material1 != null)
                {
                    bool isSheet = vm.JobItem.Material1.StockType == (int)Material.AvailableStockTypes.Sheet;
                    int amountToPull;
                    if (isSheet)
                    {
                        amountToPull = vm.JobItem.BuySheets ?? 0;
                    }
                    else
                    {
                        vm.JobItem.ParseRunSize();
                        amountToPull = (int)Math.Ceiling(vm.JobItem.RunLength);
                    }

                    var materialHistory = db.InventoryAdjustmentHistories
                        .Where(x => (x.WasMaterialPull ?? false)
                               && x.JobItem == vm.JobItem.Id
                               && x.Material == vm.JobItem.Material)
                        .ToList();

                    decimal pulledAmount = materialHistory.Sum(r =>
                    {
                        var diff = (r.OldStockAmount ?? 0) - (r.NewStockAmount ?? 0);
                        return isSheet ? diff : diff * 12; // roll stock stored in feet, amount in inches
                    });
                    int materialRemaining = Math.Max(0, amountToPull - (int)pulledAmount);
                    if (materialRemaining == 0 && amountToPull > 0)
                        vm.MaterialPullStatus = 2; // fully pulled
                    else if (pulledAmount > 0)
                        vm.MaterialPullStatus = 1; // partially pulled
                    else
                        vm.MaterialPullStatus = 0; // none pulled
                }

                return View(vm);
            }

            return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Job not found");
        }

        public async Task<ActionResult> ListJobs()
        {
            VM_TimeTrackerJobList vm = new VM_TimeTrackerJobList();

            DateTime today = DateTime.UtcNow.Date;

            int userId = UserSessionHelper.GetActiveUserId(HttpContext);

            // Populate GroupedJobs
            vm.GroupedJobs = await db.GetJobsWithWeekdayFilter(today)
                .OrderBy(x => x.GroupSort)
                .GroupBy(x => x.JobStatus)
                .ToListAsync();

            // Initialize the PendingOverdueServices list
            vm.PendingOverdueServices = new List<JobItemService>();

            // GroupSort == 1 corresponds to Overdue jobs
            var overdueJobs = vm.GroupedJobs
                .Where(group => group.First().GroupSort == 1)
                .SelectMany(group => group); // Flatten the groups to get individual jobs

            foreach (var job in overdueJobs)
            {
                GetPendingServiceForItem_Result pendingService = null;
                try
                {
                    pendingService = await db.GetPendingServiceForItem(job.Id).SingleAsync(x => x.JobItemId == job.Id);
                }
                catch (Exception e)
                {
                    //ok; move on
                }
                //GetPendingServiceForItem_Result pendingService = await db.GetPendingServiceForItem(job.Id).FirstAsync(); // Assuming this returns a list

                if (pendingService != null)
                {
                    vm.PendingOverdueServices.Add(new JobItemService
                    {
                        Id = pendingService.Id,
                        Title = pendingService.Title,
                        JobItemId = pendingService.JobItemId
                    });
                }
                else
                {
                    vm.PendingOverdueServices.Add(new JobItemService
                    {
                        Id = -1,
                        Title = "None",
                        JobItemId = job.Id
                    });
                }
            }

            //get general services and group by their category id
            //vm.GeneralServicesByCategory = await db.Services.Where(x => x.ServiceCategory.ServiceType1.Title == "Other").GroupBy(x => x.ServiceCategory.Title).ToListAsync();
            vm.GeneralServicesByCategory = await db.JobItemServices.Where(x => x.JobItem.Job1.JobNumber == "999999").GroupBy(x => x.Service.ServiceCategory.Title).ToListAsync();

            foreach (IGrouping<string, JobItemService> group in vm.GeneralServicesByCategory)
            {
                //load base services
                foreach (JobItemService jis in group.ToList())
                {
                    await db.Entry(jis).Reference(r => r.Service).LoadAsync();
                    jis.FormattedServiceTime = DecimalHoursToFormatted(jis.ServiceTime ?? 0m);

                    //load timing records for this service
                    await db.Entry(jis).Collection(c => c.JobTimingSRs).LoadAsync();

                    //set is running based on any existing timing records for this user on this service
                    jis.UserContext_IsRunning = jis.JobTimingSRs.Any(x => x.UserId == userId && x.EndTimeUTC == null);
                    jis.ServiceComplete = jis.Complete.HasValue;

                    //if the service is running for this user,get the start time
                    if (jis.UserContext_IsRunning)
                        jis.UserContext_StartTimeUTC = jis.JobTimingSRs.Single(x => x.UserId == userId && x.EndTimeUTC == null).StartTimeUTC.Value;

                    if (jis.JobTimingSRs.Count > 0)
                    {
                        //total closed time on closed time records for all users (including this user)
                        jis.UserContext_TotalOverallTime = jis.JobTimingSRs
                            .Where(x => x.EndTimeUTC != null)
                            .Select(x => (decimal)(x.EndTimeUTC.Value - x.StartTimeUTC.Value).TotalHours)  // Calculate duration in hours
                            .Sum();  // Sum up all the durations

                        //total worked time on all records for this user
                        jis.UserContext_UserServiceTime = jis.JobTimingSRs
                            .Where(x => x.UserId == userId)
                            .Select(x => (decimal)((x.EndTimeUTC == null ? DateTime.UtcNow : x.EndTimeUTC.Value) - x.StartTimeUTC.Value).TotalHours)
                            .Sum();

                        jis.UserContext_SingleServiceTime = jis.JobTimingSRs
                            .Where(x => x.UserId == userId && x.EndTimeUTC == null)
                            .Select(x => (decimal)(DateTime.UtcNow - x.StartTimeUTC.Value).TotalHours)
                            .Sum();
                    }

                    jis.UserContext_SingleServiceTimeFormatted = DecimalHoursToFormatted(jis.UserContext_SingleServiceTime);
                    jis.UserContext_TotalOverallTimeFormatted = DecimalHoursToFormatted(jis.UserContext_TotalOverallTime);
                    jis.UserContext_UserServiceTimeFormatted = DecimalHoursToFormatted(jis.UserContext_UserServiceTime);
                    jis.UserContext_UserElapsedMilliseconds = DecimalHoursToMilliseconds(jis.UserContext_UserServiceTime);
                    jis.UserContext_SingleServiceTimeMilliseconds = DecimalHoursToMilliseconds(jis.UserContext_SingleServiceTime);

                    jis.UserContext_TotalRemainingTime = Math.Max((jis.ServiceTime ?? 0m) - jis.UserContext_TotalOverallTime, 0m);
                    jis.UserContext_TotalRemainingTimeFormatted = DecimalHoursToFormatted(jis.UserContext_TotalRemainingTime);
                }
            }

            return View(vm);
        }
    }
}