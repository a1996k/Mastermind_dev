using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http.Dispatcher;
using System.Web.Mvc;
using FifthPress.ActionFilters;
using FifthPress.Models.ViewModels;
using Newtonsoft.Json;
using OfficeOpenXml.Style;
using FifthPress.Models.ViewModels2;
using System.Net;

namespace FifthPress.Controllers
{
    [CookieFilter(Order = 1)]
    [UserPermissions(Order = 2)]
    public class AdministrationController : DataController
    {
        private List<int> recordInspectorIds = new List<int>
        {
            11,28,14
        }; //Dylan and Marcus and Callah

        public ActionResult Index()
        {
            return View();
        }

        public async Task<ActionResult> VacationSchedule(DateTime? StartDate, DateTime? EndDate)
        {
            int userId = UserSessionHelper.GetActiveUserId(HttpContext);

            if (!recordInspectorIds.Contains(userId))
                return new HttpStatusCodeResult(
                    HttpStatusCode.InternalServerError,
                    "You're not marcus or dylan or callah!!!"
                );

            // default to calendar‐year
            if (StartDate == null || EndDate == null)
            {
                var now = DateTime.Now;
                StartDate = new DateTime(now.Year, 1, 1);
                EndDate = new DateTime(now.Year, 12, 31);
            }

            var events = db.Vacations
                .Include(i => i.User)
                .Where(x => DbFunctions.TruncateTime(x.VacStart) >= StartDate.Value
                         && DbFunctions.TruncateTime(x.VacEnd) <= EndDate.Value)
                .AsEnumerable()   // switch to in-memory so we can add days in LINQ-to-Objects
                .Select(v => new VM_VacationEvent
                {
                    id = v.Id,
                    title = v.User.FirstName + " " + v.User.LastName,
                    start = v.VacStart.ToString("yyyy-MM-dd"),
                    end = v.VacEnd.Value.AddDays(1).ToString("yyyy-MM-dd"),
                })
                .ToList();

            return View(events);
        }

        public ActionResult UpdateRecordInspector(
    decimal hourlyRate,
    decimal markup,
    DateTime start,
    DateTime end,
    string grouping
)
        {
            grouping = grouping.Trim().ToUpper();

            // Convert incoming markup (e.g. 40) into decimal multiplier (1.4)
            markup = 1m + (markup / 100m);

            // 1) Totals: sum across all jobs in range
            var jobStats = db.RecordInspectorJobStats(start, end)
                             .AsEnumerable();

            var resTotals = new[]
            {
        new
        {
            Hours = Math.Round(jobStats.Sum(x => x.HoursWorked), 2),
            Cost  = (jobStats.Sum(x => x.Cost)).ToString("C"),
            Price = (jobStats.Sum(x => x.Price)).ToString("C")
        }
    }.ToList();

            switch (grouping)
            {
                case "BY SERVICE":
                    {
                        var serRes = db.RecordInspectorServiceStats(start, end)
                                       .OrderBy(x => x.ServiceName)
                                       .AsEnumerable()
                                       .Select(x => new
                                       {
                                           Title = x.ServiceName,
                                           Hours = Math.Round(x.TotalHours, 2),
                                           Cost = (x.TotalCost).ToString("C"),
                                           Price = (x.TotalPrice).ToString("C")
                                       })
                                       .ToList();

                        var serResModel = new
                        {
                            records = serRes,
                            totals = resTotals
                        };

                        return new JsonResult
                        {
                            Data = JsonConvert.SerializeObject(
                                serResModel,
                                new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }
                            ),
                            JsonRequestBehavior = JsonRequestBehavior.AllowGet
                        };
                    }

                case "BY SERVICE CATEGORY":
                    {
                        var catRes = db.RecordInspectorServiceCategoryStats(start, end)
                                       .OrderBy(x => x.ServiceCategoryName)
                                       .AsEnumerable()
                                       .Select(x => new
                                       {
                                           Title = x.ServiceCategoryName,
                                           Hours = Math.Round(x.TotalHours, 2),
                                           Cost = (x.TotalCost).ToString("C"),
                                           Price = (x.TotalPrice).ToString("C")
                                       })
                                       .ToList();

                        var catResModel = new
                        {
                            records = catRes,
                            totals = resTotals
                        };

                        return new JsonResult
                        {
                            Data = JsonConvert.SerializeObject(
                                catResModel,
                                new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }
                            ),
                            JsonRequestBehavior = JsonRequestBehavior.AllowGet
                        };
                    }

                default:
                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError);
            }
        }

        public ActionResult RecordInspector()
        {
            int userId = 0;

            try
            {
                userId = UserSessionHelper.GetActiveUserId(HttpContext);

                if (!recordInspectorIds.Contains(userId))
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "You're not marcus or dylan or callah!!!");
            }
            catch (Exception)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Cannot retrieve user");
            }

            return View();
        }

        public async Task<ActionResult> MarcusAdminScreen()
        {
            int userId = 0;

            try
            {
                userId = UserSessionHelper.GetActiveUserId(HttpContext);

                if (!recordInspectorIds.Contains(userId))
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "You're not marcus or dylan or callah!!!");
            }
            catch (Exception)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Cannot retrieve user");
            }

            VM_Marcus vm = new VM_Marcus
            {
                TodaysQuotes = await db.v_TodaysQuotes.ToListAsync(),
                RecentQuotes = await db.v_QuotesCreatedInLast7Days.ToListAsync()
            };

            foreach (v_TodaysQuotes v in vm.TodaysQuotes)
                v.CreatedAt = v.CreatedAt.Value.ToLocalTime();
            foreach (v_QuotesCreatedInLast7Days v in vm.RecentQuotes)
                v.CreatedAt = v.CreatedAt.Value.ToLocalTime();

            return View(vm);
        }

        public async Task<ActionResult> AdminNav(string toLoad)
        {
            switch (toLoad)
            {
                case "Services":
                    var sRecords = await db.Services.Where(x => x.Deleted == null).Select(y => new VM_AdminTableItem_Service
                    {
                        Id = y.Id,
                        Title = y.Title,
                        Category = y.ServiceCategory.Title
                    }).ToListAsync();

                    var sCats = await db.ServiceCategories.Where(x => x.Deleted == null && x.Services.Count > 0).OrderBy(x => x.Title).Select(x => new VM_SidebarCat
                    {
                        Title = x.Title,
                        Id = x.Id,
                        Type = "service"
                    }).ToListAsync();

                    return new JsonResult
                    {
                        Data = JsonConvert.SerializeObject(
                        new
                        {
                            theView = RenderViewToString("~/Views/Partials/_AdminEditItem_Service.cshtml", sRecords),
                            header = "Service",
                            tableHeader = RenderViewToString("~/Views/Partials/_AdminTableHeader.cshtml", new List<string> { "Title", "Category" }),
                            createButtonHtml = "<button type=\"button\" class=\"btn btn-success new-service-btn\">New Service</button>",
                            sideBar = RenderViewToString("~/Views/Partials/_AdminSidebar.cshtml", sCats)
                        }, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };

                case "Materials":
                    try
                    {
                        var mRecords = await db.Materials
                            .Where(x => x.Deleted == null)
                            .Include(i => i.MaterialInventory)
                            .Include(i => i.MaterialCostType)
                            .ToListAsync();

                        // Now, process the fetched data
                        var result = mRecords.Select(y =>
                        {
                            y.ParseDimensions(); // Call the method to populate ParsedLength and ParsedWidth

                            return new VM_AdminTableItem_Material
                            {
                                Id = y.Id,
                                Title = y.Title,
                                Description = y.Notes,
                                Size = $"{y.ParsedLength}x{y.ParsedWidth}", // Combine RunLength and RunWidth into a string
                                Quantity = y.MaterialInventory.Stock,
                                Allocated = y.MaterialInventory.Allocated ?? 0,
                                Unallocated = (y.MaterialInventory.Stock - (y.MaterialInventory.Allocated ?? 0)),
                                Caliper = (y.Caliper ?? 0f).ToString(),
                                CostPerUnit = y.Cost ?? 0m,
                                CostPerUnitFormatted = (y.Cost ?? 0m).ToString(),
                                CostPerUnitUnit = y.MaterialCostType.Title ?? "",
                                EntityName = "Material"
                            };
                        }).ToList();

                        foreach (VM_AdminTableItem_Material m in result)
                            m.CostPerUnitFormatted = m.CostPerUnit.ToString("C");

                        var mCats = await db.MaterialCategories.Where(x => x.Deleted == null && x.Materials.Count > 0).OrderBy(x => x.Title).Select(x => new VM_SidebarCat
                        {
                            Title = x.Title,
                            Id = x.Id,
                            Type = "material"
                        }).ToListAsync();

                        return new JsonResult
                        {
                            Data = JsonConvert.SerializeObject(
                            new
                            {
                                theView = RenderViewToString("~/Views/Partials/_AdminEditItem_Material.cshtml", result),
                                header = "Material",
                                tableHeader = RenderViewToString("~/Views/Partials/_AdminTableHeader.cshtml", new List<string> { "Title", "Description", "Size", "Quantity", "Caliper" }),
                                createButtonHtml = "<button type=\"button\" class=\"btn btn-success new-material-btn\">New Material</button>",
                                sideBar = RenderViewToString("~/Views/Partials/_AdminSidebar.cshtml", mCats)
                            }, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                            JsonRequestBehavior = JsonRequestBehavior.AllowGet
                        };
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                case "Presses":
                    return new JsonResult
                    {
                        Data =
                            JsonConvert.SerializeObject(
                                new
                                {
                                    theView = RenderViewToString("~/Views/Partials/_AdminEditItem_Press.cshtml",
                                        await db.Presses
                                            .Where(x => x.Deleted == null && x.StockType == (int)Material.AvailableStockTypes.Sheet)
                                            .Select(x => new VM_AdminTableItem_Press
                                            {
                                                Id = x.Id,
                                                Title = x.Title,
                                                Category = x.PressCategory.Title ?? ""
                                            }).ToListAsync()),
                                    header = "Press",
                                    tableHeader = RenderViewToString("~/Views/Partials/_AdminTableHeader.cshtml", new List<string> { "Title", "Category" }),
                                    createButtonHtml = "<button type=\"button\" class=\"btn btn-success new-press-btn\">New Press</button>"
                                }),
                        JsonRequestBehavior =
                            JsonRequestBehavior.AllowGet
                    };

                case "ServiceCategories":
                    return new JsonResult
                    {
                        Data =
                            JsonConvert.SerializeObject(
                                new
                                {
                                    theView = RenderViewToString("~/Views/Partials/_AdminEditItem_ServiceCategory.cshtml",
                                        await db.ServiceCategories.Include(x => x.Services)
                                            .Select(x => new VM_AdminTableItem_ServiceCategory
                                            {
                                                Id = x.Id,
                                                Title = x.Title,
                                                ServiceCount = x.Services.Count()
                                            }).ToListAsync()),
                                    header = "Service Category",
                                    tableHeader = RenderViewToString("~/Views/Partials/_AdminTableHeader.cshtml", new List<string> { "Title", "No. Of Services" }),
                                    createButtonHtml = "<button type=\"button\" class=\"btn btn-success new-service-cat-btn\">New Service Category</button>"
                                }),
                        JsonRequestBehavior =
                            JsonRequestBehavior.AllowGet
                    };

                case "Users":
                    return new JsonResult
                    {
                        Data =
                            JsonConvert.SerializeObject(
                                new
                                {
                                    theView = RenderViewToString("~/Views/Partials/_AdminEditItem_User.cshtml",
                                        await db.Users.Include(x => x.Role1)
                                            .Select(x => new VM_AdminTableItem_User
                                            {
                                                Id = x.Id,
                                                LastName = x.LastName,
                                                FirstName = x.FirstName,
                                                Title = "",
                                                Access = x.Role1.Title,
                                                UserId = x.Login
                                            }).ToListAsync()),
                                    header = "User",
                                    tableHeader = RenderViewToString("~/Views/Partials/_AdminTableHeader.cshtml", new List<string> { "Last Name", "First Name", "Title", "Access", "User Id" }),
                                    createButtonHtml = "<button type=\"button\" class=\"btn btn-success new-user-btn\">New User</button>"
                                }),
                        JsonRequestBehavior =
                            JsonRequestBehavior.AllowGet
                    };

                case "Constraints":
                    return new JsonResult
                    {
                        Data =
                            JsonConvert.SerializeObject(
                                new
                                {
                                    theView = RenderViewToString("~/Views/Partials/_AdminEditItem.cshtml",
                                        await db.Constraints
                                            .Select(x => new VM_AdminTableItem
                                            {
                                                Id = x.Id,
                                                Title = x.ConstraintName,
                                                Type = "Constraint",
                                                EntityName = "Constraint"
                                            }).ToListAsync()),
                                    header = "Constraint",
                                    tableHeader = RenderViewToString("~/Views/Partials/_AdminTableHeader.cshtml", new List<string> { "Title", "Category", "Type" }),
                                    createButtonHtml = "<button type=\"button\" class=\"btn btn-success new-constraint-btn\">New Constraint</button>"
                                }),
                        JsonRequestBehavior =
                            JsonRequestBehavior.AllowGet
                    };

                case "Plates":
                    return new JsonResult
                    {
                        Data = JsonConvert.SerializeObject(
                                new
                                {
                                    theView = RenderViewToString("~/Views/Partials/_AdminEditItem.cshtml",
                                    await db.Plates
                                        .Select(x => new VM_AdminTableItem
                                        {
                                            Id = x.Id,
                                            Title = x.Title,
                                            Type = "Plate",
                                            EntityName = "Plate"
                                        }).ToListAsync()),
                                    header = "Plate",
                                    tableHeader = RenderViewToString("~/Views/Partials/_AdminTableHeader.cshtml", new List<string> { "Title", "Category", "Type" }),
                                    createButtonHtml = "<button type=\"button\" class=\"btn btn-success new-plate-btn\">New Plate</button>"
                                }),
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };

                case "Customers":
                    return new JsonResult
                    {
                        Data = JsonConvert.SerializeObject(
                                new
                                {
                                    theView = RenderViewToString("~/Views/Partials/_AdminEditItem_Customer.cshtml",
                                    await db.Clients
                                        .Where(x => x.Deleted == null)
                                        .Select(x => new VM_AdminTableItem_Customer
                                        {
                                            Id = x.Id,
                                            Title = x.Title
                                        })
                                        .ToListAsync()),
                                    header = "Customer",
                                    tableHeader = RenderViewToString("~/Views/Partials/_AdminTableHeader.cshtml", new List<string> { "Title" }),
                                    createButtonHtml = "<button type=\"button\" class=\"btn btn-success new-customer-btn\">New Customer</button>"
                                }),
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };

                default:
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }
        }

        #region Tournament Budget Endpoints

        /// <summary>
        /// GET: /Administration/GetClientBudget?clientId=123
        /// Gets complete budget data for a client/tournament
        /// </summary>
        public ActionResult GetClientBudget(int clientId)
        {
            try
            {
                var service = new Services.TournamentBudgetService(db);
                var budget = service.GetClientBudget(clientId);

                if (budget == null)
                {
                    return Json(new { success = false, message = "Client not found" }, JsonRequestBehavior.AllowGet);
                }

                return Json(new { success = true, data = budget }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// GET: /Administration/GetBudgetCategories
        /// Gets all active budget categories
        /// </summary>
        public ActionResult GetBudgetCategories()
        {
            try
            {
                var service = new Services.TournamentBudgetService(db);
                var categories = service.GetCategories();

                return Json(new { success = true, data = categories }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// POST: /Administration/SaveBudgetLineItem
        /// Saves a budget line item (create or update)
        /// </summary>
        [HttpPost]
        public ActionResult SaveBudgetLineItem(Models.ViewModels.TournamentBudget.VM_BudgetLineItem item)
        {
            try
            {
                int userId = UserSessionHelper.GetActiveUserId(HttpContext);
                var service = new Services.TournamentBudgetService(db);

                bool success = service.SaveLineItem(item, userId);

                if (success)
                {
                    return Json(new
                    {
                        success = true,
                        message = "Line item saved successfully",
                        lineItemId = item.LineItemId
                    });
                }
                else
                {
                    return Json(new { success = false, message = "Failed to save line item" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// POST: /Administration/DeleteBudgetLineItem
        /// Deletes a budget line item (soft delete)
        /// </summary>
        [HttpPost]
        public ActionResult DeleteBudgetLineItem(int lineItemId)
        {
            try
            {
                var service = new Services.TournamentBudgetService(db);
                bool success = service.DeleteLineItem(lineItemId);

                if (success)
                {
                    return Json(new { success = true, message = "Line item deleted successfully" });
                }
                else
                {
                    return Json(new { success = false, message = "Line item not found" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// POST: /Administration/SaveBudgetSettings
        /// Saves budget settings for a client
        /// </summary>
        [HttpPost]
        public ActionResult SaveBudgetSettings(int clientId, string tournamentLocation, decimal taxRate, string notes)
        {
            try
            {
                var service = new Services.TournamentBudgetService(db);
                bool success = service.SaveBudgetSettings(clientId, tournamentLocation, taxRate, notes);

                if (success)
                {
                    return Json(new { success = true, message = "Settings saved successfully" });
                }
                else
                {
                    return Json(new { success = false, message = "Failed to save settings" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// POST: /Administration/SaveOnsitePrintInfo
        /// Saves onsite print info (BaseId and SqftPrice) to Clients table
        /// </summary>
        [HttpPost]
        public ActionResult SaveOnsitePrintInfo(int clientId, string baseId, decimal? sqftPrice)
        {
            try
            {
                var client = db.Clients.FirstOrDefault(c => c.Id == clientId);
                if (client == null)
                {
                    return Json(new { success = false, message = "Client not found" });
                }

                client.BaseId = baseId;
                client.SqftPrice = sqftPrice;

                db.Entry(client).State = EntityState.Modified;
                db.SaveChanges();

                return Json(new { success = true, message = "Onsite print info saved successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// GET: /Administration/GetOnsitePrintInfo?clientId=123
        /// Gets onsite print info (BaseId and SqftPrice) from Clients table
        /// </summary>
        [HttpGet]
        public ActionResult GetOnsitePrintInfo(int clientId)
        {
            try
            {
                var client = db.Clients.FirstOrDefault(c => c.Id == clientId);
                if (client == null)
                {
                    return Json(new { success = false, message = "Client not found" }, JsonRequestBehavior.AllowGet);
                }

                return Json(new
                {
                    success = true,
                    baseId = client.BaseId,
                    sqftPrice = client.SqftPrice
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// GET: /Administration/GetBudgetSummary?clientId=123
        /// Gets budget summary for reporting
        /// </summary>
        public ActionResult GetBudgetSummary(int clientId)
        {
            try
            {
                var service = new Services.TournamentBudgetService(db);
                var summary = service.GetBudgetSummary(clientId);

                if (summary == null)
                {
                    return Json(new { success = false, message = "Client not found" }, JsonRequestBehavior.AllowGet);
                }

                return Json(new { success = true, data = summary }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// GET: /Administration/GetCategoryVariances?clientId=123
        /// Gets variance analysis by category
        /// </summary>
        public ActionResult GetCategoryVariances(int clientId)
        {
            try
            {
                var service = new Services.TournamentBudgetService(db);
                var variances = service.GetCategoryVariances(clientId);

                return Json(new { success = true, data = variances }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        #endregion

        #region Reprint Reasons Management

        // GET: /Administration/ReprintReasons
        // Standalone management screen for reprint categories + reasons.
        public async Task<ActionResult> ReprintReasons()
        {
            var reasons = await db.Reprint_Reasons
                .OrderBy(r => r.Category)
                .ThenBy(r => r.SortOrder)
                .ThenBy(r => r.ReasonText)
                .ToListAsync();

            return View(reasons);
        }

        // GET: /Administration/CreateReprintReason
        public async Task<ActionResult> CreateReprintReason()
        {
            await PopulateReprintReasonLookups();

            var model = new Reprint_Reasons
            {
                IsActive = true,
                SortOrder = 0
            };

            return View(model);
        }

        // POST: /Administration/CreateReprintReason
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateReprintReason(Reprint_Reasons model)
        {
            if (string.IsNullOrWhiteSpace(model.Category))
                ModelState.AddModelError("Category", "Category is required.");
            if (string.IsNullOrWhiteSpace(model.ReasonText))
                ModelState.AddModelError("ReasonText", "Reason text is required.");
            if (string.IsNullOrWhiteSpace(model.DefaultCostAllocation))
                ModelState.AddModelError("DefaultCostAllocation", "Default cost allocation is required.");

            if (ModelState.IsValid)
            {
                model.Category = model.Category.Trim();
                model.ReasonText = model.ReasonText.Trim();
                model.DefaultCostAllocation = model.DefaultCostAllocation.Trim();
                model.CreatedDate = DateTime.UtcNow;

                db.Reprint_Reasons.Add(model);
                await db.SaveChangesAsync();

                TempData["Success"] = $"Reprint reason '{model.ReasonText}' added.";
                return RedirectToAction("ReprintReasons");
            }

            await PopulateReprintReasonLookups();
            return View(model);
        }

        // GET: /Administration/EditReprintReason/5
        public async Task<ActionResult> EditReprintReason(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            var reason = await db.Reprint_Reasons.FindAsync(id);
            if (reason == null)
                return HttpNotFound();

            await PopulateReprintReasonLookups();
            return View(reason);
        }

        // POST: /Administration/EditReprintReason/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> EditReprintReason(Reprint_Reasons model)
        {
            if (string.IsNullOrWhiteSpace(model.Category))
                ModelState.AddModelError("Category", "Category is required.");
            if (string.IsNullOrWhiteSpace(model.ReasonText))
                ModelState.AddModelError("ReasonText", "Reason text is required.");
            if (string.IsNullOrWhiteSpace(model.DefaultCostAllocation))
                ModelState.AddModelError("DefaultCostAllocation", "Default cost allocation is required.");

            if (ModelState.IsValid)
            {
                var reason = await db.Reprint_Reasons.FindAsync(model.ReasonId);
                if (reason == null)
                    return HttpNotFound();

                reason.Category = model.Category.Trim();
                reason.ReasonText = model.ReasonText.Trim();
                reason.DefaultCostAllocation = model.DefaultCostAllocation.Trim();
                reason.IsActive = model.IsActive;
                reason.SortOrder = model.SortOrder;

                await db.SaveChangesAsync();

                TempData["Success"] = $"Reprint reason '{reason.ReasonText}' updated.";
                return RedirectToAction("ReprintReasons");
            }

            await PopulateReprintReasonLookups();
            return View(model);
        }

        // POST: /Administration/ToggleReprintReason/5
        // Soft enable/disable. Reasons are FK-referenced by Reprint_Jobs, so we never
        // hard-delete; deactivating just hides them from the create-reprint picker.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ToggleReprintReason(int id)
        {
            var reason = await db.Reprint_Reasons.FindAsync(id);
            if (reason == null)
                return HttpNotFound();

            reason.IsActive = !reason.IsActive;
            await db.SaveChangesAsync();

            TempData["Success"] = $"Reprint reason '{reason.ReasonText}' {(reason.IsActive ? "activated" : "deactivated")}.";
            return RedirectToAction("ReprintReasons");
        }

        // Supplies existing distinct categories and cost-allocation values for the
        // datalists on the add/edit form (pick an existing one or type a new one).
        private async Task PopulateReprintReasonLookups()
        {
            ViewBag.Categories = await db.Reprint_Reasons
                .Select(r => r.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            ViewBag.CostAllocations = await db.Reprint_Reasons
                .Select(r => r.DefaultCostAllocation)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
        }

        #endregion
    }
}