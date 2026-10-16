// ============================================================================
// Utilization Forecasting Module - MVC Controller
// Phase 1: Core CRUD Operations
// Created: October 30, 2025
// ============================================================================

using FifthPress.ActionFilters;
using FifthPress.Models;
using FifthPress.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.Controllers
{
    public class ForecastingController : DataController
    {
        // ============================================================================
        // Forecast Period Actions
        // ============================================================================

        // GET: Forecasting/Index
        public async Task<ActionResult> Index()
        {
            var periods = await db.ForecastPeriods
                .Where(p => p.IsActive)
                .OrderByDescending(p => p.StartDate)
                .ToListAsync();

            return View(periods);
        }

        // GET: Forecasting/CreatePeriod
        public async Task<ActionResult> CreatePeriod()
        {
            var model = new ForecastPeriod
            {
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddMonths(3)
            };

            return View(model);
        }

        // POST: Forecasting/CreatePeriod
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreatePeriod(ForecastPeriod model)
        {
            if (ModelState.IsValid)
            {
                HttpCookie userCookie = HttpContext.Request.Cookies.Get("ActiveUser");
                if (userCookie != null)
                {
                    model.CreatedBy = userCookie.Value;
                }
                model.CreatedDate = DateTime.UtcNow;

                db.ForecastPeriods.Add(model);
                await db.SaveChangesAsync();

                TempData["Success"] = $"Forecast period '{model.PeriodName}' created successfully.";
                return RedirectToAction("Index");
            }

            return View(model);
        }

        // GET: Forecasting/EditPeriod/5
        public async Task<ActionResult> EditPeriod(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var period = await db.ForecastPeriods.FindAsync(id);
            if (period == null)
            {
                return HttpNotFound();
            }

            return View(period);
        }

        // POST: Forecasting/EditPeriod/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> EditPeriod(ForecastPeriod model)
        {
            if (ModelState.IsValid)
            {
                var period = await db.ForecastPeriods.FindAsync(model.ForecastPeriodID);
                if (period == null)
                {
                    return HttpNotFound();
                }

                period.PeriodName = model.PeriodName;
                period.StartDate = model.StartDate;
                period.EndDate = model.EndDate;
                period.IsActive = model.IsActive;
                period.Notes = model.Notes;

                HttpCookie userCookie = HttpContext.Request.Cookies.Get("ActiveUser");
                if (userCookie != null)
                {
                    period.ModifiedBy = userCookie.Value;
                }
                period.ModifiedDate = DateTime.UtcNow;

                db.Entry(period).State = EntityState.Modified;
                await db.SaveChangesAsync();

                TempData["Success"] = $"Forecast period '{period.PeriodName}' updated successfully.";
                return RedirectToAction("Index");
            }

            return View(model);
        }

        // ============================================================================
        // Contract Forecast Actions
        // ============================================================================

        // GET: Forecasting/Contracts/5
        public async Task<ActionResult> Contracts(int? periodId, int? departmentId = null)
        {
            if (periodId == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var period = await db.ForecastPeriods.FindAsync(periodId);
            if (period == null)
            {
                return HttpNotFound();
            }

            ViewBag.Period = period;

            // Load internal ContractForecast rows
            var contracts = await db.ContractForecasts
                .Include(c => c.ContractForecastDetails.Select(d => d.BillingClass))
                .Where(c => c.ForecastPeriodID == periodId && c.IsActive)
                .OrderBy(c => c.ClientName)
                .ThenBy(c => c.ContractName)
                .ToListAsync();

            // Map to unified VM_ContractRow list
            var rows = contracts.Select(VM_ContractRow.FromContractForecast)
                .OrderBy(r => r.ClientName)
                .ThenBy(r => r.ContractName)
                .ToList();

            // Load billing classes for bulk-apply dropdown (Feature D)
            ViewData["BillingClasses"] = await GenerateSelectListAsync(SelectListType.BillingClasses);

            // Fetch utilization data for dashboard charts
            try
            {
                // Load departments for the chart
                var departments = await db.Departments
                    .Where(d => d.IsAgency == true)
                    .OrderBy(d => d.Title)
                    .ToListAsync();
                ViewBag.Departments = departments;

                // Use passed departmentId or default to first department
                var selectedDeptId = departmentId ?? departments.FirstOrDefault()?.Id;
                ViewBag.SelectedDepartmentId = selectedDeptId;

                // Fetch monthly utilization data for selected department (default to Both for dashboard, showing all contracts)
                var utilizationData = db.sp_GetMonthlyUtilizationByDepartment(periodId, selectedDeptId, 0, 0).ToList();  // 0 = Both contract types
                ViewBag.UtilizationData = utilizationData;

                // Fetch Actual data for ALL departments (for per-department Actual vs Forecast breakdown)
                var actualByDeptData = db.sp_GetMonthlyUtilizationByDepartment(periodId, null, 0, 1).ToList();  // contractTypeFilter=1 (Actual Only)
                ViewBag.ActualByDeptData = actualByDeptData;

                // Fetch Forecast data for ALL departments
                var forecastByDeptData = db.sp_GetMonthlyUtilizationByDepartment(periodId, null, 0, 2).ToList();  // contractTypeFilter=2 (Forecast Only)
                ViewBag.ForecastByDeptData = forecastByDeptData;

                // Billing class breakdown - commented out, replaced by per-department Actual vs Forecast above
                // var billingClassData = await db.Database.SqlQuery<sp_GetMonthlyUtilizationByBillingClass_Result>(
                //     "EXEC sp_GetMonthlyUtilizationByBillingClass @ForecastPeriodID, @DepartmentID",
                //     new SqlParameter("@ForecastPeriodID", periodId),
                //     new SqlParameter("@DepartmentID", (object)selectedDeptId ?? DBNull.Value)
                // ).ToListAsync();
                // ViewBag.BillingClassData = billingClassData;
            }
            catch (Exception ex)
            {
                // Log but don't fail - charts are optional
                MastermindLogger.Log(LogLevel.Warning, $"Failed to load dashboard chart data: {ex.Message}");
                ViewBag.UtilizationData = null;
                // ViewBag.BillingClassData = null;
                ViewBag.ActualByDeptData = null;
                ViewBag.ForecastByDeptData = null;
            }

            return View(rows);
        }

        // GET: Forecasting/CreateContract/5
        public async Task<ActionResult> CreateContract(int? periodId)
        {
            if (periodId == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var period = await db.ForecastPeriods.FindAsync(periodId);
            if (period == null)
            {
                return HttpNotFound();
            }

            ViewBag.Period = period;
            ViewData["BillingClasses"] = await GenerateSelectListAsync(SelectListType.BillingClasses);

            var clientList = await db.Database.SqlQuery<ExternalClientDropdown>(
                "SELECT ClientId, ClientName FROM ExternalClients WHERE Active = 1 ORDER BY ClientName").ToListAsync();
            ViewBag.Clients = new SelectList(clientList, "ClientId", "ClientName");

            var model = new ContractForecast
            {
                ForecastPeriodID = periodId.Value,
                StartDate = period.StartDate,
                EndDate = period.EndDate,
                Billable = true,  // Default to billable
                IsInternal = false  // Default to external
            };

            return View(model);
        }

        // POST: Forecasting/CreateContract
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateContract(ContractForecast model, bool IsActual = false)
        {
            try
            {
                MastermindLogger.Log(LogLevel.Info, $"CreateContract POST called - PeriodID: {model.ForecastPeriodID}, ContractName: {model.ContractName}, IsActual: {IsActual}");
                model.IsActual = IsActual;

                if (!ModelState.IsValid)
                {
                    MastermindLogger.Log(LogLevel.Error, "CreateContract ModelState is INVALID");
                    foreach (var key in ModelState.Keys)
                    {
                        var errors = ModelState[key].Errors;
                        if (errors.Count > 0)
                        {
                            foreach (var error in errors)
                            {
                                MastermindLogger.Log(LogLevel.Error, $"ModelState Error [{key}]: {error.ErrorMessage} | Exception: {error.Exception?.Message}");
                            }
                        }
                    }
                }

                if (ModelState.IsValid)
                {
                    MastermindLogger.Log(LogLevel.Info, "ModelState is valid, creating contract...");

                    // Set ClientName from ExternalClientId for backwards compatibility
                    if (model.ExternalClientId.HasValue)
                    {
                        var selectedClient = await db.ExternalClients.FirstOrDefaultAsync(ec => ec.ClientId == model.ExternalClientId.Value);
                        if (selectedClient != null)
                            model.ClientName = selectedClient.ClientName;
                    }

                    HttpCookie userCookie = HttpContext.Request.Cookies.Get("ActiveUser");
                    if (userCookie != null)
                    {
                        model.CreatedBy = userCookie.Value;
                    }
                    model.CreatedDate = DateTime.UtcNow;
                    model.IsActive = true;

                    db.ContractForecasts.Add(model);
                    await db.SaveChangesAsync();

                    MastermindLogger.Log(LogLevel.Info, $"Contract created successfully with ID: {model.ContractForecastID}");

                    TempData["Success"] = $"Contract '{model.ContractName}' created successfully.";
                    return RedirectToAction("EditContract", new { id = model.ContractForecastID });
                }

                MastermindLogger.Log(LogLevel.Info, "Returning to CreateContract view with validation errors");
                var period = await db.ForecastPeriods.FindAsync(model.ForecastPeriodID);
                ViewBag.Period = period;
                ViewData["BillingClasses"] = await GenerateSelectListAsync(SelectListType.BillingClasses);
                var clientListErr = await db.Database.SqlQuery<ExternalClientDropdown>(
                    "SELECT ClientId, ClientName FROM ExternalClients WHERE Active = 1 ORDER BY ClientName").ToListAsync();
                ViewBag.Clients = new SelectList(clientListErr, "ClientId", "ClientName", model.ExternalClientId);

                return View(model);
            }
            catch (Exception ex)
            {
                MastermindLogger.Log(LogLevel.Error, $"CreateContract exception: {ex.Message}");
                MastermindLogger.Log(LogLevel.Error, $"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    MastermindLogger.Log(LogLevel.Error, $"Inner exception: {ex.InnerException.Message}");
                }
                throw;
            }
        }

        // GET: Forecasting/EditContract/5
        public async Task<ActionResult> EditContract(int? id)
        {
            try
            {
                MastermindLogger.Log(LogLevel.Info, $"EditContract GET called with id: {id}");

                if (id == null)
                {
                    MastermindLogger.Log(LogLevel.Error, "EditContract: id is null");
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }

                MastermindLogger.Log(LogLevel.Info, $"Loading contract {id} from database...");

                var contract = await db.ContractForecasts
                    .Include(c => c.ForecastPeriod)
                    .Include(c => c.ContractForecastDetails.Select(d => d.BillingClass))
                    .Include(c => c.ContractDateCheckpoints)
                    .FirstOrDefaultAsync(c => c.ContractForecastID == id);

                if (contract == null)
                {
                    MastermindLogger.Log(LogLevel.Error, $"EditContract: contract {id} not found in database");
                    return HttpNotFound();
                }

                MastermindLogger.Log(LogLevel.Info, $"Contract found: {contract.ContractName}, loading billing classes...");

                // Load billing classes using centralized method
                ViewData["BillingClasses"] = await GenerateSelectListAsync(SelectListType.BillingClasses);

                MastermindLogger.Log(LogLevel.Info, $"Billing classes loaded");

                // Load ExternalClients dropdown
                var editClientList = await db.Database.SqlQuery<ExternalClientDropdown>(
                    "SELECT ClientId, ClientName FROM ExternalClients WHERE Active = 1 ORDER BY ClientName").ToListAsync();
                ViewBag.Clients = new SelectList(editClientList, "ClientId", "ClientName", contract.ExternalClientId);

                // Pass billing class default rates for auto-fill in the Add Detail modal
                ViewBag.BillingClassDefaults = await db.BillingClasses
                    .Where(bc => bc.DefaultHourlyRate != null)
                    .Select(bc => new { bc.Id, bc.DefaultHourlyRate })
                    .ToDictionaryAsync(bc => bc.Id, bc => bc.DefaultHourlyRate);

                // Load forecast contracts for linking dropdown (only forecasted contracts in same period, excluding current)
                var forecastContracts = await db.ContractForecasts
                    .Where(c => c.ForecastPeriodID == contract.ForecastPeriodID
                            && c.IsActive
                            && !c.IsActual
                            && c.ContractForecastID != contract.ContractForecastID)
                    .OrderBy(c => c.ClientName)
                    .ThenBy(c => c.ContractName)
                    .Select(c => new SelectListItem
                    {
                        Value = c.ContractForecastID.ToString(),
                        Text = c.ClientName + " - " + c.ContractName,
                        Selected = c.ContractForecastID == contract.LinkedContractForecastID
                    })
                    .ToListAsync();
                ViewBag.ForecastContracts = forecastContracts;

                MastermindLogger.Log(LogLevel.Info, $"Returning view");

                return View(contract);
            }
            catch (Exception ex)
            {
                MastermindLogger.Log(LogLevel.Error, $"EditContract exception: {ex.Message}");
                MastermindLogger.Log(LogLevel.Error, $"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    MastermindLogger.Log(LogLevel.Error, $"Inner exception: {ex.InnerException.Message}");
                }
                throw;
            }
        }

        // POST: Forecasting/EditContract/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> EditContract(ContractForecast model, bool IsActual = false, int? LinkedContractForecastID = null)
        {
            if (ModelState.IsValid)
            {
                var contract = await db.ContractForecasts.FindAsync(model.ContractForecastID);
                if (contract == null)
                {
                    return HttpNotFound();
                }

                contract.ContractName = model.ContractName;
                contract.ExternalClientId = model.ExternalClientId;
                // Update ClientName from selected ExternalClient for backwards compatibility
                if (model.ExternalClientId.HasValue)
                {
                    var selectedClient = await db.ExternalClients.FirstOrDefaultAsync(ec => ec.ClientId == model.ExternalClientId.Value);
                    if (selectedClient != null)
                        contract.ClientName = selectedClient.ClientName;
                }
                else if (!string.IsNullOrEmpty(model.ClientName))
                {
                    contract.ClientName = model.ClientName;
                }
                // If neither, leave contract.ClientName unchanged
                contract.StartDate = model.StartDate;
                contract.EndDate = model.EndDate;
                contract.IsRetainer = model.IsRetainer;
                contract.Billable = model.Billable;
                contract.IsInternal = model.IsInternal;
                contract.TotalBudgetedHours = model.TotalBudgetedHours;
                contract.Notes = model.Notes;
                contract.IsActual = IsActual;
                contract.LinkedContractForecastID = IsActual ? LinkedContractForecastID : null;
                contract.IncludeInCharts = model.IncludeInCharts;

                HttpCookie userCookie = HttpContext.Request.Cookies.Get("ActiveUser");
                if (userCookie != null)
                {
                    contract.ModifiedBy = userCookie.Value;
                }
                contract.ModifiedDate = DateTime.UtcNow;

                db.Entry(contract).State = EntityState.Modified;
                await db.SaveChangesAsync();

                TempData["Success"] = $"Contract '{contract.ContractName}' updated successfully.";
                return RedirectToAction("Contracts", new { periodId = contract.ForecastPeriodID });
            }

            var contractData = await db.ContractForecasts
                .Include(c => c.ForecastPeriod)
                .Include(c => c.ContractForecastDetails.Select(d => d.BillingClass))
                .FirstOrDefaultAsync(c => c.ContractForecastID == model.ContractForecastID);

            ViewData["BillingClasses"] = await GenerateSelectListAsync(SelectListType.BillingClasses);
            var editErrClientList = await db.Database.SqlQuery<ExternalClientDropdown>(
                "SELECT ClientId, ClientName FROM ExternalClients WHERE Active = 1 ORDER BY ClientName").ToListAsync();
            ViewBag.Clients = new SelectList(editErrClientList, "ClientId", "ClientName", model.ExternalClientId);

            return View(contractData);
        }

        // ============================================================================
        // Contract Forecast Detail Actions (AJAX)
        // ============================================================================

        // POST: Forecasting/AddDetail
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AddDetail(int contractId, int billingClassId, string billingSubclass, decimal budgetedHours, decimal? hourlyRate = null)
        {
            try
            {
                HttpCookie userCookie = HttpContext.Request.Cookies.Get("ActiveUser");

                // If no rate supplied, fall back to the billing class default
                if ((hourlyRate == null || hourlyRate == 0))
                {
                    var bc = await db.BillingClasses.FindAsync(billingClassId);
                    if (bc?.DefaultHourlyRate != null)
                        hourlyRate = bc.DefaultHourlyRate;
                }

                var detail = new ContractForecastDetail
                {
                    ContractForecastID = contractId,
                    BillingClassID = billingClassId,
                    BillingSubclass = billingSubclass,
                    BudgetedHours = budgetedHours,
                    HourlyRate = hourlyRate,
                    CreatedBy = userCookie?.Value,
                    CreatedDate = DateTime.UtcNow
                };

                db.ContractForecastDetails.Add(detail);
                await db.SaveChangesAsync();

                // Reload with billing class info
                var detailWithClass = await db.ContractForecastDetails
                    .Include(d => d.BillingClass)
                    .FirstOrDefaultAsync(d => d.ContractForecastDetailID == detail.ContractForecastDetailID);

                return Json(new
                {
                    success = true,
                    detail = new
                    {
                        id = detailWithClass.ContractForecastDetailID,
                        billingClassName = detailWithClass.BillingClass.Title,
                        billingSubclass = detailWithClass.BillingSubclass,
                        budgetedHours = detailWithClass.BudgetedHours
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: Forecasting/UpdateDetailRate
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> UpdateDetailRate(int id, decimal hourlyRate)
        {
            try
            {
                var detail = await db.ContractForecastDetails.FindAsync(id);
                if (detail == null)
                    return Json(new { success = false, message = "Detail not found." });

                detail.HourlyRate = hourlyRate;
                db.Entry(detail).State = EntityState.Modified;
                await db.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: Forecasting/DeleteDetail/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteDetail(int id)
        {
            try
            {
                var detail = await db.ContractForecastDetails.FindAsync(id);
                if (detail == null)
                {
                    return Json(new { success = false, message = "Detail not found." });
                }

                db.ContractForecastDetails.Remove(detail);
                await db.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: Forecasting/DeleteContract
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteContract(int contractId)
        {
            try
            {
                var contract = await db.ContractForecasts.FindAsync(contractId);
                if (contract == null)
                {
                    return Json(new { success = false, message = "Contract not found." });
                }

                contract.IsActive = false;

                HttpCookie userCookie = HttpContext.Request.Cookies.Get("ActiveUser");
                if (userCookie != null)
                {
                    contract.ModifiedBy = userCookie.Value;
                }
                contract.ModifiedDate = DateTime.UtcNow;

                db.Entry(contract).State = EntityState.Modified;
                await db.SaveChangesAsync();

                return Json(new { success = true, message = $"Contract '{contract.ContractName}' deleted." });
            }
            catch (Exception ex)
            {
                MastermindLogger.Log(LogLevel.Error, $"DeleteContract exception: {ex.Message}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ============================================================================
        // Utilization Reporting Actions
        // ============================================================================

        // GET: Forecasting/ViewUtilization/5
        public async Task<ActionResult> ViewUtilization(int? periodId, int? billingClassId)
        {
            try
            {
                MastermindLogger.Log(LogLevel.Info, $"ViewUtilization called - periodId: {periodId}, billingClassId: {billingClassId}");

                if (periodId == null)
                {
                    MastermindLogger.Log(LogLevel.Warning, "ViewUtilization: periodId is null");
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }

                MastermindLogger.Log(LogLevel.Info, $"Loading forecast period {periodId}...");
                var period = await db.ForecastPeriods.FindAsync(periodId);

                if (period == null)
                {
                    MastermindLogger.Log(LogLevel.Error, $"ViewUtilization: Period {periodId} not found");
                    return HttpNotFound();
                }

                MastermindLogger.Log(LogLevel.Info, $"Period found: {period.PeriodName} ({period.StartDate:yyyy-MM-dd} to {period.EndDate:yyyy-MM-dd})");

                ViewBag.Period = period;

                MastermindLogger.Log(LogLevel.Info, "Loading billing classes...");
                ViewData["BillingClasses"] = await GenerateSelectListAsync(SelectListType.BillingClasses);
                MastermindLogger.Log(LogLevel.Info, "Billing classes loaded successfully");

                // Call stored procedure to get utilization forecast
                MastermindLogger.Log(LogLevel.Info, $"Executing sp_GetUtilizationForecast with params: StartDate={period.StartDate:yyyy-MM-dd}, EndDate={period.EndDate:yyyy-MM-dd}, PeriodID={periodId}, BillingClassID={billingClassId?.ToString() ?? "NULL"}");

                var results = await db.Database.SqlQuery<sp_GetUtilizationForecast_Result>(
                    "EXEC sp_GetUtilizationForecast @StartDate, @EndDate, @ForecastPeriodID, @BillingClassID",
                    new SqlParameter("@StartDate", period.StartDate),
                    new SqlParameter("@EndDate", period.EndDate),
                    new SqlParameter("@ForecastPeriodID", periodId),
                    new SqlParameter("@BillingClassID", (object)billingClassId ?? DBNull.Value)
                ).ToListAsync();

                MastermindLogger.Log(LogLevel.Info, $"Stored procedure returned {results.Count} records");

                ViewBag.SelectedBillingClassId = billingClassId;

                MastermindLogger.Log(LogLevel.Info, "Returning ViewUtilization view");
                return View(results);
            }
            catch (Exception ex)
            {
                MastermindLogger.Log(LogLevel.Error, $"ViewUtilization exception: {ex.Message}");
                MastermindLogger.Log(LogLevel.Error, $"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    MastermindLogger.Log(LogLevel.Error, $"Inner exception: {ex.InnerException.Message}");
                }
                throw;
            }
        }

        // GET: Forecasting/SummaryByBillingClass/5
        public async Task<ActionResult> SummaryByBillingClass(int? periodId)
        {
            try
            {
                MastermindLogger.Log(LogLevel.Info, $"SummaryByBillingClass called - periodId: {periodId}");

                if (periodId == null)
                {
                    MastermindLogger.Log(LogLevel.Warning, "SummaryByBillingClass: periodId is null");
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }

                MastermindLogger.Log(LogLevel.Info, $"Loading forecast period {periodId}...");
                var period = await db.ForecastPeriods.FindAsync(periodId);

                if (period == null)
                {
                    MastermindLogger.Log(LogLevel.Error, $"SummaryByBillingClass: Period {periodId} not found");
                    return HttpNotFound();
                }

                MastermindLogger.Log(LogLevel.Info, $"Period found: {period.PeriodName} ({period.StartDate:yyyy-MM-dd} to {period.EndDate:yyyy-MM-dd})");

                ViewBag.Period = period;

                // Call stored procedure to get summary by billing class
                MastermindLogger.Log(LogLevel.Info, $"Executing sp_GetUtilizationSummaryByBillingClass with params: StartDate={period.StartDate:yyyy-MM-dd}, EndDate={period.EndDate:yyyy-MM-dd}, PeriodID={periodId}");

                var results = await db.Database.SqlQuery<sp_GetUtilizationSummaryByBillingClass_Result>(
                    "EXEC sp_GetUtilizationSummaryByBillingClass @StartDate, @EndDate, @ForecastPeriodID",
                    new SqlParameter("@StartDate", period.StartDate),
                    new SqlParameter("@EndDate", period.EndDate),
                    new SqlParameter("@ForecastPeriodID", periodId)
                ).ToListAsync();

                MastermindLogger.Log(LogLevel.Info, $"Stored procedure returned {results.Count} records");
                MastermindLogger.Log(LogLevel.Info, "Returning SummaryByBillingClass view");

                return View(results);
            }
            catch (Exception ex)
            {
                MastermindLogger.Log(LogLevel.Error, $"SummaryByBillingClass exception: {ex.Message}");
                MastermindLogger.Log(LogLevel.Error, $"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    MastermindLogger.Log(LogLevel.Error, $"Inner exception: {ex.InnerException.Message}");
                }
                throw;
            }
        }

        // GET: Forecasting/UtilizationByCategory/5
        public async Task<ActionResult> UtilizationByCategory(int? periodId)
        {
            try
            {
                MastermindLogger.Log(LogLevel.Info, $"UtilizationByCategory called - periodId: {periodId}");

                if (periodId == null)
                {
                    MastermindLogger.Log(LogLevel.Warning, "UtilizationByCategory: periodId is null");
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }

                MastermindLogger.Log(LogLevel.Info, $"Loading forecast period {periodId}...");
                var period = await db.ForecastPeriods.FindAsync(periodId);

                if (period == null)
                {
                    MastermindLogger.Log(LogLevel.Error, $"UtilizationByCategory: Period {periodId} not found");
                    return HttpNotFound();
                }

                MastermindLogger.Log(LogLevel.Info, $"Period found: {period.PeriodName} ({period.StartDate:yyyy-MM-dd} to {period.EndDate:yyyy-MM-dd})");

                ViewBag.Period = period;

                // Call stored procedure to get utilization summary by category
                MastermindLogger.Log(LogLevel.Info, $"Executing sp_GetUtilizationSummaryByCategory with params: StartDate={period.StartDate:yyyy-MM-dd}, EndDate={period.EndDate:yyyy-MM-dd}, PeriodID={periodId}");

                var results = await db.Database.SqlQuery<sp_GetUtilizationSummaryByCategory_Result>(
                    "EXEC sp_GetUtilizationSummaryByCategory @StartDate, @EndDate, @ForecastPeriodID",
                    new SqlParameter("@StartDate", period.StartDate),
                    new SqlParameter("@EndDate", period.EndDate),
                    new SqlParameter("@ForecastPeriodID", periodId)
                ).ToListAsync();

                MastermindLogger.Log(LogLevel.Info, $"Stored procedure returned {results.Count} category records");
                MastermindLogger.Log(LogLevel.Info, "Returning UtilizationByCategory view");

                return View(results);
            }
            catch (Exception ex)
            {
                MastermindLogger.Log(LogLevel.Error, $"UtilizationByCategory exception: {ex.Message}");
                MastermindLogger.Log(LogLevel.Error, $"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    MastermindLogger.Log(LogLevel.Error, $"Inner exception: {ex.InnerException.Message}");
                }
                throw;
            }
        }

        // GET: Forecasting/ActualVsForecast/5
        public async Task<ActionResult> ActualVsForecast(int? periodId)
        {
            try
            {
                MastermindLogger.Log(LogLevel.Info, $"ActualVsForecast called - periodId: {periodId}");

                if (periodId == null)
                {
                    MastermindLogger.Log(LogLevel.Warning, "ActualVsForecast: periodId is null");
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }

                MastermindLogger.Log(LogLevel.Info, $"Loading forecast period {periodId}...");
                var period = await db.ForecastPeriods.FindAsync(periodId);

                if (period == null)
                {
                    MastermindLogger.Log(LogLevel.Error, $"ActualVsForecast: Period {periodId} not found");
                    return HttpNotFound();
                }

                MastermindLogger.Log(LogLevel.Info, $"Period found: {period.PeriodName} ({period.StartDate:yyyy-MM-dd} to {period.EndDate:yyyy-MM-dd})");

                ViewBag.Period = period;

                // Call stored procedure to get actual vs forecast comparison
                MastermindLogger.Log(LogLevel.Info, $"Executing sp_GetActualVsForecastByCategory with params: StartDate={period.StartDate:yyyy-MM-dd}, EndDate={period.EndDate:yyyy-MM-dd}, PeriodID={periodId}");

                var results = await db.Database.SqlQuery<sp_GetActualVsForecastByCategory_Result>(
                    "EXEC sp_GetActualVsForecastByCategory @StartDate, @EndDate, @ForecastPeriodID",
                    new SqlParameter("@StartDate", period.StartDate),
                    new SqlParameter("@EndDate", period.EndDate),
                    new SqlParameter("@ForecastPeriodID", periodId)
                ).ToListAsync();

                MastermindLogger.Log(LogLevel.Info, $"Stored procedure returned {results.Count} comparison records");

                // Get monthly breakdown data for the stacked bar chart
                var monthlyData = await GetActualVsForecastMonthlyData(periodId.Value, period.StartDate, period.EndDate);
                ViewBag.MonthlyData = monthlyData;

                MastermindLogger.Log(LogLevel.Info, "Returning ActualVsForecast view");

                return View(results);
            }
            catch (Exception ex)
            {
                MastermindLogger.Log(LogLevel.Error, $"ActualVsForecast exception: {ex.Message}");
                MastermindLogger.Log(LogLevel.Error, $"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    MastermindLogger.Log(LogLevel.Error, $"Inner exception: {ex.InnerException.Message}");
                }
                throw;
            }
        }

        // ============================================================================
        // Retainer Client Actions
        // ============================================================================

        // GET: Forecasting/Retainers
        public async Task<ActionResult> Retainers()
        {
            var retainers = await db.RetainerClients
                .Include(r => r.RetainerBillingClassAllocations.Select(a => a.BillingClass))
                .Where(r => r.IsActive)
                .OrderBy(r => r.ClientName)
                .ToListAsync();

            return View(retainers);
        }

        // GET: Forecasting/CreateRetainer
        public async Task<ActionResult> CreateRetainer()
        {
            ViewData["BillingClasses"] = await GenerateSelectListAsync(SelectListType.BillingClasses);

            var model = new RetainerClient
            {
                StartDate = DateTime.Today
            };

            return View(model);
        }

        // POST: Forecasting/CreateRetainer
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateRetainer(RetainerClient model)
        {
            if (ModelState.IsValid)
            {
                HttpCookie userCookie = HttpContext.Request.Cookies.Get("ActiveUser");
                if (userCookie != null)
                {
                    model.CreatedBy = userCookie.Value;
                }
                model.CreatedDate = DateTime.UtcNow;

                db.RetainerClients.Add(model);
                await db.SaveChangesAsync();

                TempData["Success"] = $"Retainer client '{model.ClientName}' created successfully.";
                return RedirectToAction("EditRetainer", new { id = model.RetainerClientID });
            }

            ViewData["BillingClasses"] = await GenerateSelectListAsync(SelectListType.BillingClasses);

            return View(model);
        }

        // GET: Forecasting/EditRetainer/5
        public async Task<ActionResult> EditRetainer(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var retainer = await db.RetainerClients
                .Include(r => r.RetainerBillingClassAllocations.Select(a => a.BillingClass))
                .Include(r => r.RetainerMonthlyOverrides)
                .FirstOrDefaultAsync(r => r.RetainerClientID == id);

            if (retainer == null)
            {
                return HttpNotFound();
            }

            ViewData["BillingClasses"] = await GenerateSelectListAsync(SelectListType.BillingClasses);

            return View(retainer);
        }

        // POST: Forecasting/EditRetainer/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> EditRetainer(RetainerClient model)
        {
            if (ModelState.IsValid)
            {
                var retainer = await db.RetainerClients.FindAsync(model.RetainerClientID);
                if (retainer == null)
                {
                    return HttpNotFound();
                }

                retainer.ClientName = model.ClientName;
                retainer.DefaultMonthlyHours = model.DefaultMonthlyHours;
                retainer.StartDate = model.StartDate;
                retainer.EndDate = model.EndDate;
                retainer.IsActive = model.IsActive;
                retainer.Notes = model.Notes;

                HttpCookie userCookie = HttpContext.Request.Cookies.Get("ActiveUser");
                if (userCookie != null)
                {
                    retainer.ModifiedBy = userCookie.Value;
                }
                retainer.ModifiedDate = DateTime.UtcNow;

                db.Entry(retainer).State = EntityState.Modified;
                await db.SaveChangesAsync();

                TempData["Success"] = $"Retainer client '{retainer.ClientName}' updated successfully.";
                return RedirectToAction("Retainers");
            }

            var retainerData = await db.RetainerClients
                .Include(r => r.RetainerBillingClassAllocations.Select(a => a.BillingClass))
                .FirstOrDefaultAsync(r => r.RetainerClientID == model.RetainerClientID);

            ViewData["BillingClasses"] = await GenerateSelectListAsync(SelectListType.BillingClasses);

            return View(retainerData);
        }

        // ============================================================================
        // Monthly Utilization Chart Actions
        // ============================================================================

        // GET: Forecasting/MonthlyUtilization/5
        public async Task<ActionResult> MonthlyUtilization(int? periodId, int? departmentId, int? retainerFilter, int? contractTypeFilter)
        {
            try
            {
                MastermindLogger.Log(LogLevel.Info, $"MonthlyUtilization called - periodId: {periodId}, departmentId: {departmentId}, retainerFilter: {retainerFilter}, contractTypeFilter: {contractTypeFilter}");

                if (periodId == null)
                {
                    MastermindLogger.Log(LogLevel.Warning, "MonthlyUtilization: periodId is null");
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }

                MastermindLogger.Log(LogLevel.Info, $"Loading forecast period {periodId}...");
                var period = await db.ForecastPeriods.FindAsync(periodId);

                if (period == null)
                {
                    MastermindLogger.Log(LogLevel.Error, $"MonthlyUtilization: Period {periodId} not found");
                    return HttpNotFound();
                }

                MastermindLogger.Log(LogLevel.Info, $"Period found: {period.PeriodName}");

                ViewBag.Period = period;

                // Load departments for dropdown
                var departments = await db.Departments
                    .Where(d => d.IsAgency == true)
                    .OrderBy(d => d.Title)
                    .ToListAsync();

                ViewBag.Departments = departments;

                // Default to first department if none selected
                if (departmentId == null && departments.Any())
                {
                    departmentId = departments.First().Id;
                }

                ViewBag.SelectedDepartmentId = departmentId;
                ViewBag.RetainerFilter = retainerFilter ?? 0;  // 0=Both, 1=Retainer Only, 2=Non-Retainer Only
                ViewBag.ContractTypeFilter = contractTypeFilter ?? 0;  // 0=Both, 1=Actual Only, 2=Forecast Only

                // Call stored procedure
                // ContractTypeFilter: 0=Both (default for forecasting view), 1=Actual Only, 2=Forecast Only
                var selectedContractType = contractTypeFilter ?? 0;
                MastermindLogger.Log(LogLevel.Info, $"Executing sp_GetMonthlyUtilizationByDepartment with periodId={periodId}, departmentId={departmentId}, retainerFilter={retainerFilter ?? 0}, contractTypeFilter={selectedContractType}");

                var results = db.sp_GetMonthlyUtilizationByDepartment(periodId, departmentId, retainerFilter ?? 0, selectedContractType).ToList();

                MastermindLogger.Log(LogLevel.Info, $"Stored procedure returned {results.Count} records");

                return View(results);
            }
            catch (Exception ex)
            {
                MastermindLogger.Log(LogLevel.Error, $"MonthlyUtilization exception: {ex.Message}");
                MastermindLogger.Log(LogLevel.Error, $"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    MastermindLogger.Log(LogLevel.Error, $"Inner exception: {ex.InnerException.Message}");
                }
                throw;
            }
        }

        // GET: Forecasting/MonthlyUtilizationDetailed/5
        public async Task<ActionResult> MonthlyUtilizationDetailed(int? periodId, int? departmentId)
        {
            try
            {
                MastermindLogger.Log(LogLevel.Info, $"MonthlyUtilizationDetailed called - periodId: {periodId}, departmentId: {departmentId}");

                if (periodId == null)
                {
                    MastermindLogger.Log(LogLevel.Warning, "MonthlyUtilizationDetailed: periodId is null");
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }

                MastermindLogger.Log(LogLevel.Info, $"Loading forecast period {periodId}...");
                var period = await db.ForecastPeriods.FindAsync(periodId);

                if (period == null)
                {
                    MastermindLogger.Log(LogLevel.Error, $"MonthlyUtilizationDetailed: Period {periodId} not found");
                    return HttpNotFound();
                }

                MastermindLogger.Log(LogLevel.Info, $"Period found: {period.PeriodName}");

                ViewBag.Period = period;

                // Load departments for dropdown
                var departments = await db.Departments
                    .Where(d => d.IsAgency == true)
                    .OrderBy(d => d.Title)
                    .ToListAsync();

                ViewBag.Departments = departments;

                // Default to first department if none selected
                if (departmentId == null && departments.Any())
                {
                    departmentId = departments.First().Id;
                }

                ViewBag.SelectedDepartmentId = departmentId;

                // Call stored procedure
                MastermindLogger.Log(LogLevel.Info, $"Executing sp_GetMonthlyUtilizationByBillingClass with periodId={periodId}, departmentId={departmentId}");

                var results = await db.Database.SqlQuery<sp_GetMonthlyUtilizationByBillingClass_Result>(
                    "EXEC sp_GetMonthlyUtilizationByBillingClass @ForecastPeriodID, @DepartmentID",
                    new SqlParameter("@ForecastPeriodID", periodId),
                    new SqlParameter("@DepartmentID", (object)departmentId ?? DBNull.Value)
                 ).ToListAsync();

                MastermindLogger.Log(LogLevel.Info, $"Stored procedure returned {results.Count} records");

                return View(results);
            }
            catch (Exception ex)
            {
                MastermindLogger.Log(LogLevel.Error, $"MonthlyUtilizationDetailed exception: {ex.Message}");
                MastermindLogger.Log(LogLevel.Error, $"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    MastermindLogger.Log(LogLevel.Error, $"Inner exception: {ex.InnerException.Message}");
                }
                throw;
            }
        }

        // ============================================================================
        // Utilization Dashboard (Combined Monthly + Actual vs Forecast)
        // ============================================================================

        // GET: Forecasting/UtilizationDashboard/5
        public async Task<ActionResult> UtilizationDashboard(int? periodId, int? departmentId, int? retainerFilter, int? contractTypeFilter)
        {
            try
            {
                MastermindLogger.Log(LogLevel.Info, $"UtilizationDashboard called - periodId: {periodId}, departmentId: {departmentId}, retainerFilter: {retainerFilter}, contractTypeFilter: {contractTypeFilter}");

                if (periodId == null)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }

                var period = await db.ForecastPeriods.FindAsync(periodId);
                if (period == null)
                {
                    return HttpNotFound();
                }

                ViewBag.Period = period;

                // Load departments for dropdown
                var departments = await db.Departments
                    .Where(d => d.IsAgency == true)
                    .OrderBy(d => d.Title)
                    .ToListAsync();
                ViewBag.Departments = departments;

                // Store filter values for view
                ViewBag.SelectedDepartmentId = departmentId;
                ViewBag.RetainerFilter = retainerFilter ?? 0;  // 0=Both, 1=Retainer Only, 2=Non-Retainer Only
                ViewBag.ContractTypeFilter = contractTypeFilter ?? 0;  // 0=Both, 1=Actual Only, 2=Forecast Only

                // Get Monthly Utilization data with filters
                var selectedRetainer = retainerFilter ?? 0;
                var selectedContractType = contractTypeFilter ?? 0;
                var monthlyData = db.sp_GetMonthlyUtilizationByDepartment(periodId, departmentId, selectedRetainer, selectedContractType).ToList();
                ViewBag.MonthlyData = monthlyData;

                // Get Actual vs Forecast data by month
                var actualVsForecastData = await GetActualVsForecastMonthlyData(periodId.Value, period.StartDate, period.EndDate);
                ViewBag.ActualVsForecastData = actualVsForecastData;

                return View();
            }
            catch (Exception ex)
            {
                MastermindLogger.Log(LogLevel.Error, $"UtilizationDashboard exception: {ex.Message}");
                throw;
            }
        }

        // Helper method to get actual vs forecast data by month
        private async Task<List<dynamic>> GetActualVsForecastMonthlyData(int periodId, DateTime startDate, DateTime endDate)
        {
            var result = new List<dynamic>();

            // Get all contracts for this period
            var contracts = await db.ContractForecasts
                .Include(c => c.ContractForecastDetails)
                .Where(c => c.ForecastPeriodID == periodId && c.IsActive && c.IncludeInCharts)
                .ToListAsync();

            // Iterate through months in the period
            var currentMonth = new DateTime(startDate.Year, startDate.Month, 1);
            var endMonth = new DateTime(endDate.Year, endDate.Month, 1);

            while (currentMonth <= endMonth)
            {
                var monthStart = currentMonth;
                var monthEnd = currentMonth.AddMonths(1).AddDays(-1);

                // Calculate forecasted hours for this month (from forecasted contracts)
                decimal forecastedHours = 0;
                decimal actualHours = 0;

                foreach (var contract in contracts)
                {
                    // Check if contract overlaps with this month
                    if (contract.EndDate >= monthStart && contract.StartDate <= monthEnd)
                    {
                        // Calculate proportion of contract hours for this month
                        var contractDays = (contract.EndDate - contract.StartDate).TotalDays + 1;
                        var overlapStart = contract.StartDate > monthStart ? contract.StartDate : monthStart;
                        var overlapEnd = contract.EndDate < monthEnd ? contract.EndDate : monthEnd;
                        var overlapDays = (overlapEnd - overlapStart).TotalDays + 1;
                        var proportion = contractDays > 0 ? (decimal)(overlapDays / contractDays) : 0;

                        var contractHours = contract.ContractForecastDetails?.Sum(d => d.BudgetedHours) ?? 0;
                        var monthlyHours = contractHours * proportion;

                        if (contract.IsActual)
                        {
                            actualHours += monthlyHours;
                        }
                        else
                        {
                            forecastedHours += monthlyHours;
                        }
                    }
                }

                result.Add(new
                {
                    MonthName = currentMonth.ToString("MMM yyyy"),
                    MonthStart = monthStart,
                    MonthEnd = monthEnd,
                    ActualHours = Math.Round(actualHours, 1),
                    ForecastedHours = Math.Round(forecastedHours, 1)
                });

                currentMonth = currentMonth.AddMonths(1);
            }

            return result;
        }

        // ============================================================================
        // Project Date Management Actions
        // ============================================================================

        // GET: Forecasting/ProjectDates
        public async Task<ActionResult> ProjectDates(int? departmentId, string searchTerm, bool? hasNoDates, string tagFilter)
        {
            try
            {
                MastermindLogger.Log(LogLevel.Info, $"ProjectDates called - departmentId: {departmentId}, searchTerm: {searchTerm}, hasNoDates: {hasNoDates}, tagFilter: {tagFilter}");

                // Load departments for dropdown
                var departments = await db.Departments
                    .Where(d => d.IsAgency == true)
                    .OrderBy(d => d.Title)
                    .ToListAsync();

                ViewBag.Departments = departments;
                ViewBag.SelectedDepartmentId = departmentId;
                ViewBag.SearchTerm = searchTerm;
                ViewBag.HasNoDates = hasNoDates ?? false;
                ViewBag.TagFilter = tagFilter;

                // Load forecast periods for convert-to-contract dropdown
                var periods = await db.ForecastPeriods
                    .Where(p => p.IsActive)
                    .OrderByDescending(p => p.StartDate)
                    .ToListAsync();
                ViewBag.ForecastPeriods = periods;

                // Start with all active external projects
                // Note: Tags column is optional - returns NULL if column doesn't exist yet
                // Run ALTER_ExternalProjects_AddTags.sql to add the Tags column, then update SSIS to populate it
                var query = db.Database.SqlQuery<ExternalProjectDateInfo>(
                    @"SELECT
                        ep.ProjectId,
                        ep.Id AS ExternalProjectId,
                        ep.ProjectName,
                        ep.ProjectDescription,
                        ep.Active,
                        ep.TotalBudgetHours,
                        ep.ParsedCreativeBudgetHours,
                        ep.ParsedStrategyBudgetHours,
                        ep.ParsedDevelopmentBudgetHours,
                        ep.ProjectStartDate,
                        ep.ProjectEndDate,
                        ep.EstimatedCompletionDate,
                        ec.ClientName,
                        (SELECT MIN(CAST(te.StartTime AS DATE)) FROM ExternalTimeEntries te WHERE te.ProjectId = ep.ProjectId) AS FirstTimeEntryDate,
                        (SELECT MAX(CAST(te.StartTime AS DATE)) FROM ExternalTimeEntries te WHERE te.ProjectId = ep.ProjectId) AS LastTimeEntryDate,
                        (SELECT SUM(DATEDIFF(MINUTE, te.StartTime, te.EndTime) / 60.0)
                         FROM ExternalTimeEntries te
                         WHERE te.ProjectId = ep.ProjectId) AS TotalHoursLogged,
                        NULL AS Tags,
                        ep.CreatedOn,
                        -- Billing class info
                        ISNULL((SELECT COUNT(*) FROM ExternalProjectBillingClasses epbc WHERE epbc.ExternalProjectId = ep.Id), 0) AS BillingClassCount,
                        (SELECT SUM(epbc.BudgetedHours) FROM ExternalProjectBillingClasses epbc WHERE epbc.ExternalProjectId = ep.Id) AS AssignedHours,
                        (SELECT STRING_AGG(bc.Title + ' (' + CAST(epbc.BudgetedHours AS VARCHAR) + 'h)', ', ')
                         FROM ExternalProjectBillingClasses epbc
                         INNER JOIN BillingClasses bc ON epbc.BillingClassId = bc.Id
                         WHERE epbc.ExternalProjectId = ep.Id) AS BillingClassSummary,
                        -- Linked contract info (for duplicate detection)
                        cf.ContractForecastID AS LinkedContractId,
                        cf.ContractName AS LinkedContractName
                    FROM ExternalProjects ep
                    LEFT JOIN ExternalClients ec ON ep.ClientId = ec.ClientId
                    LEFT JOIN ContractForecast cf ON cf.LinkedExternalProjectId = ep.Id AND cf.IsActive = 1
                    WHERE ep.Active = 1"
                );

                var projects = await query.ToListAsync();

                // Apply filters
                if (departmentId.HasValue)
                {
                    projects = projects.Where(p =>
                        (departmentId == 1 && p.ParsedCreativeBudgetHours > 0) ||
                        (departmentId == 2 && p.ParsedStrategyBudgetHours > 0) ||
                        (departmentId == 3 && p.ParsedDevelopmentBudgetHours > 0)
                    ).ToList();
                }

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    var search = searchTerm.ToLower();
                    projects = projects.Where(p =>
                        (p.ProjectName != null && p.ProjectName.ToLower().Contains(search)) ||
                        (p.ClientName != null && p.ClientName.ToLower().Contains(search))
                    ).ToList();
                }

                if (hasNoDates == true)
                {
                    projects = projects.Where(p =>
                        !p.ProjectStartDate.HasValue &&
                        !p.ProjectEndDate.HasValue &&
                        !p.EstimatedCompletionDate.HasValue
                    ).ToList();
                }

                // Tag filter - matches any project where Tags contains the filter string (case-insensitive)
                // Tags are stored as comma-separated values like "Website,Branding,Q1-2026"
                if (!string.IsNullOrWhiteSpace(tagFilter))
                {
                    var tag = tagFilter.ToLower().Trim();
                    projects = projects.Where(p =>
                        !string.IsNullOrWhiteSpace(p.Tags) &&
                        p.Tags.ToLower().Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Any(t => t.Trim().Contains(tag))
                    ).ToList();
                }

                // Sort by projects with no dates first, then by budget hours descending
                projects = projects
                    .OrderBy(p => p.ProjectStartDate.HasValue || p.ProjectEndDate.HasValue ? 1 : 0)
                    .ThenByDescending(p => p.TotalBudgetHours)
                    .ToList();

                MastermindLogger.Log(LogLevel.Info, $"Returning {projects.Count} projects");

                // Recently created projects (last 7 days)
                var recentProjects = projects.Where(p => p.CreatedOn.HasValue && p.CreatedOn.Value >= DateTime.UtcNow.AddDays(-7)).ToList();
                ViewBag.RecentProjects = recentProjects;

                return View(projects);
            }
            catch (Exception ex)
            {
                MastermindLogger.Log(LogLevel.Error, $"ProjectDates exception: {ex.Message}");
                MastermindLogger.Log(LogLevel.Error, $"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    MastermindLogger.Log(LogLevel.Error, $"Inner exception: {ex.InnerException.Message}");
                }
                throw;
            }
        }

        // POST: Forecasting/SaveProjectDate
        [HttpPost]
        public async Task<JsonResult> SaveProjectDate(int projectId, DateTime? startDate, DateTime? endDate, DateTime? estimatedCompletionDate)
        {
            try
            {
                MastermindLogger.Log(LogLevel.Info, $"SaveProjectDate called - projectId: {projectId}, startDate: {startDate}, endDate: {endDate}, estimatedCompletionDate: {estimatedCompletionDate}");

                // Find the project by Paymo ProjectId (not the DB primary key Id)
                var project = await db.ExternalProjects.FirstOrDefaultAsync(p => p.ProjectId == projectId);

                if (project == null)
                {
                    return Json(new { success = false, message = "Project not found" });
                }

                // Update properties
                project.ProjectStartDate = startDate;
                project.ProjectEndDate = endDate;
                project.EstimatedCompletionDate = estimatedCompletionDate;

                // Save changes using EF
                await db.SaveChangesAsync();

                MastermindLogger.Log(LogLevel.Info, $"Successfully updated dates for project {projectId}");

                return Json(new { success = true, message = "Project dates saved successfully" });
            }
            catch (Exception ex)
            {
                MastermindLogger.Log(LogLevel.Error, $"SaveProjectDate exception: {ex.Message}");
                MastermindLogger.Log(LogLevel.Error, $"Stack trace: {ex.StackTrace}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // POST: Forecasting/BulkSetDates
        [HttpPost]
        public async Task<JsonResult> BulkSetDates(List<int> projectIds, DateTime? startDate, DateTime? endDate, DateTime? estimatedCompletionDate)
        {
            try
            {
                MastermindLogger.Log(LogLevel.Info, $"BulkSetDates called for {projectIds?.Count ?? 0} projects, startDate: {startDate}, endDate: {endDate}, estimatedCompletionDate: {estimatedCompletionDate}");

                if (projectIds == null || !projectIds.Any())
                {
                    return Json(new { success = false, message = "No projects selected" });
                }

                // Load all projects that match the IDs
                var projects = await db.ExternalProjects
                    .Where(p => projectIds.Contains(p.ProjectId))
                    .ToListAsync();

                if (!projects.Any())
                {
                    return Json(new { success = false, message = "No matching projects found" });
                }

                // Update each project
                foreach (var project in projects)
                {
                    project.ProjectStartDate = startDate;
                    project.ProjectEndDate = endDate;
                    project.EstimatedCompletionDate = estimatedCompletionDate;
                }

                // Save all changes using EF
                await db.SaveChangesAsync();

                MastermindLogger.Log(LogLevel.Info, $"Successfully updated dates for {projects.Count} projects");

                return Json(new { success = true, message = $"Dates updated for {projects.Count} projects" });
            }
            catch (Exception ex)
            {
                MastermindLogger.Log(LogLevel.Error, $"BulkSetDates exception: {ex.Message}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // ============================================================================
        // External Project Management (Ashley's screen)
        // ============================================================================

        // POST: Forecasting/RemoveBillingClass
        [HttpPost]
        public async Task<JsonResult> RemoveBillingClass(int externalProjectId, int? billingClassId = null)
        {
            try
            {
                MastermindLogger.Log(LogLevel.Info, $"RemoveBillingClass called - externalProjectId: {externalProjectId}, billingClassId: {billingClassId}");

                int deleted;
                if (billingClassId.HasValue)
                {
                    // Remove specific billing class
                    deleted = await db.Database.ExecuteSqlCommandAsync(
                        "DELETE FROM ExternalProjectBillingClasses WHERE ExternalProjectId = @p0 AND BillingClassId = @p1",
                        externalProjectId, billingClassId.Value);
                }
                else
                {
                    // Remove all billing classes for this project
                    deleted = await db.Database.ExecuteSqlCommandAsync(
                        "DELETE FROM ExternalProjectBillingClasses WHERE ExternalProjectId = @p0",
                        externalProjectId);
                }

                MastermindLogger.Log(LogLevel.Info, $"Removed {deleted} billing class assignment(s) from project {externalProjectId}");

                return Json(new { success = true, message = $"Removed {deleted} billing class assignment(s)" });
            }
            catch (Exception ex)
            {
                MastermindLogger.Log(LogLevel.Error, $"RemoveBillingClass exception: {ex.Message}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // POST: Forecasting/ConvertToContract
        [HttpPost]
        public async Task<JsonResult> ConvertToContract(int externalProjectId, int periodId)
        {
            try
            {
                MastermindLogger.Log(LogLevel.Info, $"ConvertToContract called - externalProjectId: {externalProjectId}, periodId: {periodId}");

                // Get the external project
                var project = await db.ExternalProjects.FindAsync(externalProjectId);
                if (project == null)
                {
                    return Json(new { success = false, message = "Project not found" });
                }

                // Check if already converted
                var existingContract = await db.ContractForecasts
                    .FirstOrDefaultAsync(cf => cf.LinkedExternalProjectId == externalProjectId && cf.IsActive);
                if (existingContract != null)
                {
                    return Json(new { success = false, message = $"Project already converted to contract: {existingContract.ContractName}" });
                }

                // Get client name via raw SQL since EF entity doesn't have it
                var clientName = await db.Database.SqlQuery<string>(
                    "SELECT ClientName FROM ExternalClients WHERE ClientId = @p0",
                    project.ClientId).FirstOrDefaultAsync() ?? "Unknown Client";

                HttpCookie userCookie = HttpContext.Request.Cookies.Get("ActiveUser");
                var createdBy = userCookie?.Value;

                // Determine date range
                var startDate = project.ProjectStartDate ?? DateTime.Today;
                var endDate = project.ProjectEndDate ?? startDate.AddMonths(3);

                // Create the contract
                var contract = new ContractForecast
                {
                    ForecastPeriodID = periodId,
                    ContractName = project.ProjectName,
                    ClientName = clientName,
                    StartDate = startDate,
                    EndDate = endDate,
                    IsRetainer = false,
                    Billable = true,
                    IsInternal = false,
                    IsActive = true,
                    IncludeInCharts = true,
                    IsActual = false, // Forecasted
                    TotalBudgetedHours = project.TotalBudgetHours,
                    LinkedExternalProjectId = externalProjectId,
                    CreatedBy = createdBy,
                    CreatedDate = DateTime.UtcNow
                };

                db.ContractForecasts.Add(contract);
                await db.SaveChangesAsync();

                MastermindLogger.Log(LogLevel.Info, $"Created contract {contract.ContractForecastID} from external project {externalProjectId}");

                return Json(new {
                    success = true,
                    message = $"Created contract: {contract.ContractName}",
                    contractId = contract.ContractForecastID
                });
            }
            catch (Exception ex)
            {
                MastermindLogger.Log(LogLevel.Error, $"ConvertToContract exception: {ex.Message}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // POST: Forecasting/BulkConvertToContracts
        [HttpPost]
        public async Task<JsonResult> BulkConvertToContracts(List<int> externalProjectIds, int periodId)
        {
            try
            {
                MastermindLogger.Log(LogLevel.Info, $"BulkConvertToContracts called - {externalProjectIds?.Count ?? 0} projects, periodId: {periodId}");

                if (externalProjectIds == null || !externalProjectIds.Any())
                {
                    return Json(new { success = false, message = "No projects selected" });
                }

                var period = await db.ForecastPeriods.FindAsync(periodId);
                if (period == null)
                {
                    return Json(new { success = false, message = "Invalid forecast period" });
                }

                HttpCookie userCookie = HttpContext.Request.Cookies.Get("ActiveUser");
                var createdBy = userCookie?.Value;

                int convertedCount = 0;
                int skippedCount = 0;

                foreach (var externalProjectId in externalProjectIds)
                {
                    // Check if already converted
                    var existingContract = await db.ContractForecasts
                        .FirstOrDefaultAsync(cf => cf.LinkedExternalProjectId == externalProjectId && cf.IsActive);
                    if (existingContract != null)
                    {
                        skippedCount++;
                        continue;
                    }

                    // Get the external project
                    var project = await db.ExternalProjects.FindAsync(externalProjectId);
                    if (project == null)
                    {
                        skippedCount++;
                        continue;
                    }

                    // Get client name via raw SQL since EF entity doesn't have it
                    var clientName = await db.Database.SqlQuery<string>(
                        "SELECT ClientName FROM ExternalClients WHERE ClientId = @p0",
                        project.ClientId).FirstOrDefaultAsync() ?? "Unknown Client";

                    // Determine date range
                    var startDate = project.ProjectStartDate ?? DateTime.Today;
                    var endDate = project.ProjectEndDate ?? startDate.AddMonths(3);

                    var contract = new ContractForecast
                    {
                        ForecastPeriodID = periodId,
                        ContractName = project.ProjectName,
                        ClientName = clientName,
                        StartDate = startDate,
                        EndDate = endDate,
                        IsRetainer = false,
                        Billable = true,
                        IsInternal = false,
                        IsActive = true,
                        IncludeInCharts = true,
                        IsActual = false,
                        TotalBudgetedHours = project.TotalBudgetHours,
                        LinkedExternalProjectId = externalProjectId,
                        CreatedBy = createdBy,
                        CreatedDate = DateTime.UtcNow
                    };

                    db.ContractForecasts.Add(contract);
                    convertedCount++;
                }

                if (convertedCount > 0)
                {
                    await db.SaveChangesAsync();
                }

                var message = $"Converted {convertedCount} project(s) to contracts.";
                if (skippedCount > 0)
                {
                    message += $" Skipped {skippedCount} (already converted or not found).";
                }

                MastermindLogger.Log(LogLevel.Info, $"BulkConvertToContracts completed: {convertedCount} converted, {skippedCount} skipped");

                return Json(new { success = true, convertedCount, skippedCount, message });
            }
            catch (Exception ex)
            {
                MastermindLogger.Log(LogLevel.Error, $"BulkConvertToContracts exception: {ex.Message}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // POST: Forecasting/QuickAddContract
        [HttpPost]
        public async Task<JsonResult> QuickAddContract(QuickAddContractRequest request)
        {
            try
            {
                MastermindLogger.Log(LogLevel.Info, $"QuickAddContract called - periodId: {request.PeriodId}, contractName: {request.ContractName}");

                HttpCookie userCookie = HttpContext.Request.Cookies.Get("ActiveUser");
                var createdBy = userCookie?.Value;

                var contract = new ContractForecast
                {
                    ForecastPeriodID = request.PeriodId,
                    ContractName = request.ContractName,
                    ClientName = request.ClientName,
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    IsRetainer = request.IsRetainer,
                    Billable = true,
                    IsInternal = false,
                    IsActive = true,
                    IncludeInCharts = request.IncludeInCharts,
                    IsActual = request.IsActual,
                    CreatedBy = createdBy,
                    CreatedDate = DateTime.UtcNow
                };

                db.ContractForecasts.Add(contract);
                await db.SaveChangesAsync();

                // Add billing class details if provided
                decimal totalHours = 0;
                if (request.BillingClasses != null && request.BillingClasses.Any())
                {
                    foreach (var bc in request.BillingClasses)
                    {
                        var detail = new ContractForecastDetail
                        {
                            ContractForecastID = contract.ContractForecastID,
                            BillingClassID = bc.Id,
                            BudgetedHours = bc.Hours,
                            CreatedBy = createdBy,
                            CreatedDate = DateTime.UtcNow
                        };
                        db.ContractForecastDetails.Add(detail);
                        totalHours += bc.Hours;
                    }

                    contract.TotalBudgetedHours = totalHours;
                    await db.SaveChangesAsync();
                }

                MastermindLogger.Log(LogLevel.Info, $"QuickAddContract created contract {contract.ContractForecastID} with {request.BillingClasses?.Count ?? 0} billing classes");

                return Json(new { success = true, contractId = contract.ContractForecastID, contractName = contract.ContractName });
            }
            catch (Exception ex)
            {
                MastermindLogger.Log(LogLevel.Error, $"QuickAddContract exception: {ex.Message}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: Forecasting/GetBillingClasses (for external project billing class modal)
        [HttpGet]
        public async Task<JsonResult> GetProjectBillingClasses(int externalProjectId)
        {
            try
            {
                var billingClasses = await db.Database.SqlQuery<ProjectBillingClassInfo>(
                    @"SELECT epbc.Id, epbc.BillingClassId, bc.Title AS BillingClassName,
                             bc.DepartmentId, d.Title AS DepartmentName,
                             epbc.BudgetedHours
                      FROM ExternalProjectBillingClasses epbc
                      INNER JOIN BillingClasses bc ON epbc.BillingClassId = bc.Id
                      LEFT JOIN Departments d ON bc.DepartmentId = d.Id
                      WHERE epbc.ExternalProjectId = @p0
                      ORDER BY bc.Title",
                    externalProjectId).ToListAsync();

                return Json(new { success = true, billingClasses }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                MastermindLogger.Log(LogLevel.Error, $"GetProjectBillingClasses exception: {ex.Message}");
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // ============================================================================
        // Internal Bucket Generation (Feature C)
        // ============================================================================

        // POST: Forecasting/GenerateInternalBuckets
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> GenerateInternalBuckets(int periodId)
        {
            try
            {
                MastermindLogger.Log(LogLevel.Info, $"GenerateInternalBuckets called for periodId: {periodId}");

                var period = await db.ForecastPeriods.FindAsync(periodId);
                if (period == null)
                {
                    return HttpNotFound();
                }

                HttpCookie userCookie = HttpContext.Request.Cookies.Get("ActiveUser");
                var createdBy = userCookie?.Value;
                int created = 0;

                // Iterate each month from StartDate to EndDate
                var currentMonth = new DateTime(period.StartDate.Year, period.StartDate.Month, 1);
                var endMonth = new DateTime(period.EndDate.Year, period.EndDate.Month, 1);

                int skipped = 0;
                while (currentMonth <= endMonth)
                {
                    var monthStart = currentMonth;
                    var monthEnd = new DateTime(currentMonth.Year, currentMonth.Month, DateTime.DaysInMonth(currentMonth.Year, currentMonth.Month));

                    // Skip if an internal bucket already exists for this month
                    bool exists = await db.ContractForecasts.AnyAsync(cf =>
                        cf.ForecastPeriodID == periodId
                        && cf.IsInternal == true
                        && cf.Billable == false
                        && cf.StartDate.Year == monthStart.Year
                        && cf.StartDate.Month == monthStart.Month);

                    if (!exists)
                    {
                        var bucket = new ContractForecast
                        {
                            ForecastPeriodID = periodId,
                            ContractName = $"Internal FifthColor \u2014 {currentMonth:MMM yyyy}",
                            ClientName = "FifthColor",
                            StartDate = monthStart,
                            EndDate = monthEnd,
                            IsRetainer = false,
                            Billable = false,
                            IsInternal = true,
                            IsActive = true,
                            IncludeInCharts = true,
                            IsActual = true,
                            CreatedBy = createdBy,
                            CreatedDate = DateTime.UtcNow
                        };

                        db.ContractForecasts.Add(bucket);
                        created++;
                    }
                    else
                    {
                        skipped++;
                    }

                    currentMonth = currentMonth.AddMonths(1);
                }

                await db.SaveChangesAsync();
                TempData["Success"] = $"Generated {created} internal bucket(s)." + (skipped > 0 ? $" Skipped {skipped} existing month(s)." : "");

                MastermindLogger.Log(LogLevel.Info, $"GenerateInternalBuckets created {created} buckets for period {periodId}");

                return RedirectToAction("Contracts", new { periodId });
            }
            catch (Exception ex)
            {
                MastermindLogger.Log(LogLevel.Error, $"GenerateInternalBuckets exception: {ex.Message}");
                TempData["Error"] = $"Error generating internal buckets: {ex.Message}";
                return RedirectToAction("Contracts", new { periodId });
            }
        }

        // ============================================================================
        // Bulk Apply Billing Class Details (Feature D)
        // ============================================================================

        // POST: Forecasting/BulkAddDetails
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> BulkAddDetails(List<int> contractIds, List<int> externalProjectIds, int billingClassId, string billingSubclass, decimal budgetedHours)
        {
            try
            {
                MastermindLogger.Log(LogLevel.Info, $"BulkAddDetails called - contracts: {contractIds?.Count ?? 0}, externalProjects: {externalProjectIds?.Count ?? 0}, billingClassId: {billingClassId}, hours: {budgetedHours}");

                int totalContracts = (contractIds?.Count ?? 0) + (externalProjectIds?.Count ?? 0);
                if (totalContracts == 0)
                {
                    return Json(new { success = false, message = "No contracts selected" });
                }

                HttpCookie userCookie = HttpContext.Request.Cookies.Get("ActiveUser");
                var createdBy = userCookie?.Value;
                int successCount = 0;

                // Handle internal ContractForecast rows
                if (contractIds != null)
                {
                    foreach (var contractId in contractIds)
                    {
                        var detail = new ContractForecastDetail
                        {
                            ContractForecastID = contractId,
                            BillingClassID = billingClassId,
                            BillingSubclass = billingSubclass,
                            BudgetedHours = budgetedHours,
                            CreatedBy = createdBy,
                            CreatedDate = DateTime.UtcNow
                        };

                        db.ContractForecastDetails.Add(detail);
                        successCount++;
                    }

                    if (successCount > 0)
                    {
                        await db.SaveChangesAsync();
                    }
                }

                // Handle external project rows (write to ExternalProjectBillingClasses via raw SQL)
                if (externalProjectIds != null && externalProjectIds.Any())
                {
                    foreach (var projectId in externalProjectIds)
                    {
                        await db.Database.ExecuteSqlCommandAsync(
                            @"IF NOT EXISTS (
                                  SELECT 1 FROM ExternalProjectBillingClasses
                                  WHERE ExternalProjectId = @ProjectId AND BillingClassId = @BillingClassId
                              )
                              INSERT INTO ExternalProjectBillingClasses
                              (ExternalProjectId, BillingClassId, BudgetedHours, CreatedDate, CreatedBy)
                              VALUES (@ProjectId, @BillingClassId, @BudgetedHours, @CreatedDate, @CreatedBy)
                              ELSE
                              UPDATE ExternalProjectBillingClasses
                              SET BudgetedHours = BudgetedHours + @BudgetedHours, ModifiedDate = @CreatedDate, ModifiedBy = @CreatedBy
                              WHERE ExternalProjectId = @ProjectId AND BillingClassId = @BillingClassId",
                            new SqlParameter("@ProjectId", projectId),
                            new SqlParameter("@BillingClassId", billingClassId),
                            new SqlParameter("@BudgetedHours", budgetedHours),
                            new SqlParameter("@CreatedDate", DateTime.UtcNow),
                            new SqlParameter("@CreatedBy", (object)createdBy ?? DBNull.Value)
                        );
                        successCount++;
                    }
                }

                var message = $"Added billing class to {successCount} contract(s).";

                MastermindLogger.Log(LogLevel.Info, $"BulkAddDetails completed: {successCount} added");

                return Json(new { success = true, message, successCount });
            }
            catch (Exception ex)
            {
                MastermindLogger.Log(LogLevel.Error, $"BulkAddDetails exception: {ex.Message}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // ============================================================================
        // Budget Tracker Actions
        // ============================================================================

        // GET: /Forecasting/BudgetTracker
        [UserPermissions]
        public async Task<ActionResult> BudgetTracker()
        {
            var clients = await db.Database.SqlQuery<VM_BudgetClientRaw>(
                @"SELECT ec.ClientId, ec.ClientName, COUNT(cf.ContractForecastID) AS ContractCount
                  FROM ExternalClients ec
                  INNER JOIN ContractForecast cf ON cf.ExternalClientId = ec.ClientId
                  WHERE cf.IsActual = 1 AND cf.IsActive = 1
                  GROUP BY ec.ClientId, ec.ClientName
                  ORDER BY ec.ClientName").ToListAsync();

            var model = new List<FifthPress.Models.ViewModels.VM_BudgetClient>();
            foreach (var c in clients)
            {
                var details = await db.ContractForecastDetails
                    .Include(d => d.ContractForecast)
                    .Where(d => d.ContractForecast.ExternalClientId == c.ClientId
                             && d.ContractForecast.IsActual
                             && d.ContractForecast.IsActive)
                    .ToListAsync();

                decimal totalHours = details.Sum(d => d.BudgetedHours);
                decimal totalBudget = details.Sum(d => d.BudgetedHours * (d.HourlyRate ?? 0m));

                // Get total hours used via time entries for all contracts of this client
                var projectIds = await db.ContractForecasts
                    .Where(cf => cf.ExternalClientId == c.ClientId && cf.IsActual && cf.IsActive && cf.LinkedExternalProjectId != null)
                    .Select(cf => cf.LinkedExternalProjectId.Value)
                    .ToListAsync();

                decimal hoursUsed = 0;
                foreach (var epId in projectIds)
                {
                    var ep = await db.ExternalProjects.FirstOrDefaultAsync(e => e.Id == epId);
                    if (ep != null)
                    {
                        int paymoId = ep.ProjectId;
                        var entries = await db.ExternalTimeEntries
                            .Where(te => te.ProjectId == paymoId && te.EndTime != null)
                            .ToListAsync();
                        hoursUsed += (decimal)entries.Sum(te => (te.EndTime.Value - te.StartTime).TotalHours);
                    }
                }

                model.Add(new FifthPress.Models.ViewModels.VM_BudgetClient
                {
                    ClientId = c.ClientId,
                    ClientName = c.ClientName,
                    ActiveContractCount = c.ContractCount,
                    TotalApprovedHours = totalHours,
                    TotalApprovedBudget = totalBudget,
                    TotalHoursUsed = hoursUsed
                });
            }

            return View(model);
        }

        // GET: /Forecasting/BudgetTrackerClient/5
        [UserPermissions]
        public async Task<ActionResult> BudgetTrackerClient(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            var client = await db.ExternalClients.FirstOrDefaultAsync(ec => ec.ClientId == id);
            if (client == null) return HttpNotFound();

            ViewBag.ClientName = client.ClientName;
            ViewBag.ClientId = id;

            var contracts = await db.ContractForecasts
                .Include(c => c.ContractForecastDetails.Select(d => d.BillingClass))
                .Where(c => c.ExternalClientId == id && c.IsActual && c.IsActive)
                .ToListAsync();

            var model = new List<FifthPress.Models.ViewModels.VM_BudgetProject>();
            foreach (var cf in contracts)
            {
                decimal approvedHours = cf.ContractForecastDetails.Sum(d => d.BudgetedHours);
                decimal approvedBudget = cf.ContractForecastDetails.Sum(d => d.BudgetedHours * (d.HourlyRate ?? 0m));
                decimal blendedRate = approvedHours > 0 ? approvedBudget / approvedHours : 0;

                decimal hoursUsed = 0;
                bool hasLink = cf.LinkedExternalProjectId.HasValue;
                if (hasLink)
                {
                    var ep = await db.ExternalProjects.FirstOrDefaultAsync(e => e.Id == cf.LinkedExternalProjectId.Value);
                    if (ep != null)
                    {
                        int paymoId = ep.ProjectId;
                        var entries = await db.ExternalTimeEntries
                            .Where(te => te.ProjectId == paymoId && te.EndTime != null)
                            .ToListAsync();
                        hoursUsed = (decimal)entries.Sum(te => (te.EndTime.Value - te.StartTime).TotalHours);
                    }
                }

                var billingClasses = cf.ContractForecastDetails.Select(d => new FifthPress.Models.ViewModels.VM_BudgetBillingClass
                {
                    BillingClassName = d.BillingClass != null ? d.BillingClass.Title : "Unknown",
                    BudgetedHours = d.BudgetedHours,
                    HourlyRate = d.HourlyRate ?? 0m,
                    ApprovedBudget = d.BudgetedHours * (d.HourlyRate ?? 0m)
                }).ToList();

                model.Add(new FifthPress.Models.ViewModels.VM_BudgetProject
                {
                    ContractForecastId = cf.ContractForecastID,
                    ContractName = cf.ContractName,
                    ClientName = cf.ClientName,
                    StartDate = cf.StartDate,
                    EndDate = cf.EndDate,
                    ApprovedHours = approvedHours,
                    HoursUsed = hoursUsed,
                    HoursRemaining = approvedHours - hoursUsed,
                    ApprovedBudget = approvedBudget,
                    SpentToDate = hoursUsed * blendedRate,
                    BudgetRemaining = approvedBudget - (hoursUsed * blendedRate),
                    BillingClasses = billingClasses,
                    HasLinkedProject = hasLink
                });
            }

            return View(model);
        }

        // GET: /Forecasting/BudgetTrackerProject/5
        [UserPermissions]
        public async Task<ActionResult> BudgetTrackerProject(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            var cf = await db.ContractForecasts
                .Include(c => c.ContractForecastDetails.Select(d => d.BillingClass.Department))
                .FirstOrDefaultAsync(c => c.ContractForecastID == id);

            if (cf == null) return HttpNotFound();

            ViewBag.ClientId = cf.ExternalClientId;

            decimal approvedHours = cf.ContractForecastDetails.Sum(d => d.BudgetedHours);
            decimal approvedBudget = cf.ContractForecastDetails.Sum(d => d.BudgetedHours * (d.HourlyRate ?? 0m));
            decimal blendedRate = approvedHours > 0 ? approvedBudget / approvedHours : 0;

            // Contract month count: number of calendar months spanned by the contract
            int contractMonthCount = (cf.EndDate.Year - cf.StartDate.Year) * 12
                                   + cf.EndDate.Month - cf.StartDate.Month + 1;
            if (contractMonthCount < 1) contractMonthCount = 1;

            decimal monthlyBudgetAllocation = cf.IsRetainer
                ? approvedBudget / contractMonthCount
                : 0m;

            var monthlyBreakdown = new List<FifthPress.Models.ViewModels.VM_BudgetMonth>();
            decimal hoursUsed = 0;
            bool hasLink = cf.LinkedExternalProjectId.HasValue;

            // Build per-department budget summary from the contract's billing classes.
            // DepartmentId → (DepartmentName, blended rate from billing classes in that dept)
            var deptBudget = cf.ContractForecastDetails
                .Where(d => d.BillingClass != null && d.BillingClass.DepartmentId != null)
                .GroupBy(d => d.BillingClass.DepartmentId.Value)
                .Select(g => new
                {
                    DeptId        = g.Key,
                    DeptName      = g.First().BillingClass.Department != null
                                        ? g.First().BillingClass.Department.Title
                                        : g.First().BillingClass.Title,
                    ApprovedHrs   = g.Sum(d => d.BudgetedHours),
                    ApprovedBudgt = g.Sum(d => d.BudgetedHours * (d.HourlyRate ?? 0m))
                })
                .ToDictionary(x => x.DeptId, x => new
                {
                    x.DeptName,
                    x.ApprovedHrs,
                    BlendedRate = x.ApprovedHrs > 0 ? x.ApprovedBudgt / x.ApprovedHrs : blendedRate
                });

            // Load all departments by ID for naming uncontracted departments
            var allDepts = await db.Departments
                .Select(d => new { d.Id, d.Title })
                .ToDictionaryAsync(d => d.Id, d => d.Title);

            // Load system-wide default rates by department (for departments not in this contract)
            var systemDeptRates = (await db.BillingClasses
                .Where(bc => bc.DepartmentId != null && bc.DefaultHourlyRate != null)
                .Select(bc => new { bc.DepartmentId, bc.DefaultHourlyRate })
                .ToListAsync())
                .GroupBy(bc => bc.DepartmentId.Value)
                .ToDictionary(g => g.Key,
                    g => (decimal)g.Average(bc => (double)bc.DefaultHourlyRate.Value));

            if (hasLink)
            {
                var ep = await db.ExternalProjects.FirstOrDefaultAsync(e => e.Id == cf.LinkedExternalProjectId.Value);
                if (ep != null)
                {
                    int paymoId = ep.ProjectId;
                    var entries = await db.ExternalTimeEntries
                        .Where(te => te.ProjectId == paymoId && te.EndTime != null)
                        .ToListAsync();

                    hoursUsed = (decimal)entries.Sum(te => (te.EndTime.Value - te.StartTime).TotalHours);

                    // Map EmployeeRef → DepartmentId via ApiEmployeePivot → User
                    var employeeRefs = entries.Select(te => te.EmployeeRef)
                        .Where(r => r != null).Distinct().ToList();

                    var pivotEntries = await db.ApiEmployeePivots
                        .Where(p => employeeRefs.Contains(p.ApiId))
                        .Select(p => new { p.ApiId, p.UserId })
                        .ToListAsync();

                    var pivotUserIds = pivotEntries.Select(p => p.UserId).Distinct().ToList();

                    var userDepts = await db.Users
                        .Where(u => pivotUserIds.Contains(u.Id) && u.AgencyDepartment != null)
                        .Select(u => new { u.Id, u.AgencyDepartment })
                        .ToListAsync();

                    // EmployeeRef → DepartmentId
                    var empRefToDeptId = pivotEntries
                        .Join(userDepts, p => p.UserId, u => u.Id,
                              (p, u) => new { p.ApiId, u.AgencyDepartment })
                        .GroupBy(x => x.ApiId)
                        .ToDictionary(g => g.Key, g => g.First().AgencyDepartment);

                    // Resolve an EmployeeRef to its actual department name.
                    // Falls back to "Other" only when the employee can't be linked to any department at all.
                    Func<string, string> deptName = empRef =>
                    {
                        if (empRef != null && empRefToDeptId.TryGetValue(empRef, out int? dId) && dId.HasValue)
                        {
                            if (deptBudget.ContainsKey(dId.Value))
                                return deptBudget[dId.Value].DeptName;
                            if (allDepts.ContainsKey(dId.Value))
                                return allDepts[dId.Value];
                        }
                        return "Other";
                    };

                    // Rate: use contract dept rate if available, system default rate next, then blended.
                    Func<string, decimal> deptRate = empRef =>
                    {
                        if (empRef != null && empRefToDeptId.TryGetValue(empRef, out int? dId) && dId.HasValue)
                        {
                            if (deptBudget.ContainsKey(dId.Value))
                                return deptBudget[dId.Value].BlendedRate;
                            if (systemDeptRates.ContainsKey(dId.Value))
                                return systemDeptRates[dId.Value];
                        }
                        return blendedRate;
                    };

                    var grouped = entries
                        .GroupBy(te => new { te.StartTime.Year, te.StartTime.Month })
                        .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month);

                    decimal runningCost = 0;
                    foreach (var g in grouped)
                    {
                        var deptBreakdown = g
                            .GroupBy(te => deptName(te.EmployeeRef))
                            .Select(dg =>
                            {
                                decimal dHours = (decimal)dg.Sum(te => (te.EndTime.Value - te.StartTime).TotalHours);
                                decimal dCost  = dg.Aggregate(0m, (sum, te) =>
                                    sum + (decimal)(te.EndTime.Value - te.StartTime).TotalHours
                                          * deptRate(te.EmployeeRef));
                                return new FifthPress.Models.ViewModels.VM_BudgetMonthDepartment
                                {
                                    DepartmentName = dg.Key,
                                    HoursUsed      = dHours,
                                    EstimatedCost  = dCost
                                };
                            })
                            .OrderBy(d => d.DepartmentName == "Other")
                            .ThenBy(d => d.DepartmentName)
                            .ToList();

                        decimal monthHours = deptBreakdown.Sum(d => d.HoursUsed);
                        decimal monthCost  = deptBreakdown.Sum(d => d.EstimatedCost);
                        runningCost += monthCost;

                        monthlyBreakdown.Add(new FifthPress.Models.ViewModels.VM_BudgetMonth
                        {
                            Year                    = g.Key.Year,
                            MonthNumber             = g.Key.Month,
                            MonthName               = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                            HoursUsed               = monthHours,
                            EstimatedCost           = monthCost,
                            CumulativeCost          = runningCost,
                            MonthlyBudgetAllocation = monthlyBudgetAllocation,
                            DepartmentBreakdown     = deptBreakdown
                        });
                    }
                }
            }

            var billingClasses = cf.ContractForecastDetails.Select(d => new FifthPress.Models.ViewModels.VM_BudgetBillingClass
            {
                BillingClassName = d.BillingClass != null ? d.BillingClass.Title : "Unknown",
                BudgetedHours    = d.BudgetedHours,
                HourlyRate       = d.HourlyRate ?? 0m,
                ApprovedBudget   = d.BudgetedHours * (d.HourlyRate ?? 0m)
            }).ToList();

            // Build department groups: contracted departments (with billing class sub-rows) +
            // any uncontracted departments that have actual hours tracked against this project.
            Func<ContractForecastDetail, string> detailDeptName = d =>
            {
                if (d.BillingClass == null) return "Unknown";
                if (d.BillingClass.Department != null) return d.BillingClass.Department.Title;
                if (d.BillingClass.DepartmentId.HasValue && allDepts.ContainsKey(d.BillingClass.DepartmentId.Value))
                    return allDepts[d.BillingClass.DepartmentId.Value];
                return d.BillingClass.Title;
            };

            // Aggregate actual hours per department name from the monthly breakdown
            var deptHoursMap = monthlyBreakdown
                .SelectMany(m => m.DepartmentBreakdown ?? new List<FifthPress.Models.ViewModels.VM_BudgetMonthDepartment>())
                .GroupBy(d => d.DepartmentName)
                .ToDictionary(g => g.Key, g => g.Sum(d => d.HoursUsed));

            var contractedGroups = cf.ContractForecastDetails
                .Where(d => d.BillingClass != null)
                .GroupBy(d => detailDeptName(d))
                .Select(g =>
                {
                    decimal dApprovedHrs    = g.Sum(d => d.BudgetedHours);
                    decimal dApprovedBudget = g.Sum(d => d.BudgetedHours * (d.HourlyRate ?? 0m));
                    return new FifthPress.Models.ViewModels.VM_BudgetDepartmentGroup
                    {
                        DepartmentName = g.Key,
                        ApprovedHours  = dApprovedHrs,
                        ApprovedBudget = dApprovedBudget,
                        BlendedRate    = dApprovedHrs > 0 ? dApprovedBudget / dApprovedHrs : 0m,
                        HoursUsed      = deptHoursMap.ContainsKey(g.Key) ? deptHoursMap[g.Key] : 0m,
                        IsInContract   = true,
                        BillingClasses = g.Select(d => new FifthPress.Models.ViewModels.VM_BudgetBillingClass
                        {
                            BillingClassName = d.BillingClass.Title,
                            BudgetedHours    = d.BudgetedHours,
                            HourlyRate       = d.HourlyRate ?? 0m,
                            ApprovedBudget   = d.BudgetedHours * (d.HourlyRate ?? 0m)
                        }).ToList()
                    };
                }).ToList();

            var contractedDeptNames = new HashSet<string>(contractedGroups.Select(g => g.DepartmentName));
            var uncontractedGroups = deptHoursMap
                .Where(kvp => !contractedDeptNames.Contains(kvp.Key))
                .Select(kvp => new FifthPress.Models.ViewModels.VM_BudgetDepartmentGroup
                {
                    DepartmentName = kvp.Key,
                    ApprovedHours  = 0m,
                    ApprovedBudget = 0m,
                    BlendedRate    = 0m,
                    HoursUsed      = kvp.Value,
                    IsInContract   = false,
                    BillingClasses = new List<FifthPress.Models.ViewModels.VM_BudgetBillingClass>()
                }).ToList();

            var departmentGroups = contractedGroups
                .Concat(uncontractedGroups)
                .OrderBy(g => !g.IsInContract)
                .ThenBy(g => g.DepartmentName)
                .ToList();

            decimal spentToDate = monthlyBreakdown.Sum(m => m.EstimatedCost);

            var model = new FifthPress.Models.ViewModels.VM_BudgetProjectDetail
            {
                ContractForecastId = cf.ContractForecastID,
                ContractName = cf.ContractName,
                ClientName = cf.ClientName,
                StartDate = cf.StartDate,
                EndDate = cf.EndDate,
                IsRetainer = cf.IsRetainer,
                ApprovedHours = approvedHours,
                HoursUsed = hoursUsed,
                HoursRemaining = approvedHours - hoursUsed,
                ApprovedBudget = approvedBudget,
                SpentToDate = spentToDate,
                BudgetRemaining = approvedBudget - spentToDate,
                BillingClasses = billingClasses,
                HasLinkedProject = hasLink,
                ContractMonthCount = contractMonthCount,
                MonthlyBreakdown = monthlyBreakdown,
                DepartmentGroups = departmentGroups
            };

            return View(model);
        }
    }

    // ============================================================================
    // Helper Classes for Contract & Project Management
    // ============================================================================

    public class ExternalProjectContractInfo
    {
        public int Id { get; set; }
        public string ProjectName { get; set; }
        public string ClientName { get; set; }
        public DateTime ProjectStartDate { get; set; }
        public DateTime ProjectEndDate { get; set; }
        public int BillingClassCount { get; set; }
        public decimal TotalBudgetedHours { get; set; }
        public string BillingClassSummary { get; set; }
    }

    public class ExternalProjectDateInfo
    {
        public int ProjectId { get; set; }
        public int ExternalProjectId { get; set; }  // The Id column (PK), not ProjectId (Paymo ID)
        public string ProjectName { get; set; }
        public string ProjectDescription { get; set; }
        public bool Active { get; set; }
        public decimal? TotalBudgetHours { get; set; }
        public decimal? ParsedCreativeBudgetHours { get; set; }
        public decimal? ParsedStrategyBudgetHours { get; set; }
        public decimal? ParsedDevelopmentBudgetHours { get; set; }
        public DateTime? ProjectStartDate { get; set; }
        public DateTime? ProjectEndDate { get; set; }
        public DateTime? EstimatedCompletionDate { get; set; }
        public string ClientName { get; set; }
        public DateTime? FirstTimeEntryDate { get; set; }
        public DateTime? LastTimeEntryDate { get; set; }
        public decimal? TotalHoursLogged { get; set; }
        public string Tags { get; set; }  // Comma-separated tags from Paymo (scaffolded 2026-01-20)
        public DateTime? CreatedOn { get; set; }

        // Billing class info (for managing assigned hours)
        public int BillingClassCount { get; set; }
        public decimal? AssignedHours { get; set; }
        public string BillingClassSummary { get; set; }

        // Linked contract info (for duplicate detection)
        public int? LinkedContractId { get; set; }
        public string LinkedContractName { get; set; }
    }

    public class ProjectBillingClassInfo
    {
        public int Id { get; set; }
        public int BillingClassId { get; set; }
        public string BillingClassName { get; set; }
        public int? DepartmentId { get; set; }
        public string DepartmentName { get; set; }
        public decimal BudgetedHours { get; set; }
    }

    public class QuickAddContractRequest
    {
        public int PeriodId { get; set; }
        public string ContractName { get; set; }
        public string ClientName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsRetainer { get; set; }
        public bool IsActual { get; set; }
        public bool IncludeInCharts { get; set; } = true;
        public List<QuickAddBillingClass> BillingClasses { get; set; }
    }

    public class QuickAddBillingClass
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Hours { get; set; }
    }

    public class ExternalClientDropdown
    {
        public int ClientId { get; set; }
        public string ClientName { get; set; }
    }

    public class VM_BudgetClientRaw
    {
        public int ClientId { get; set; }
        public string ClientName { get; set; }
        public int ContractCount { get; set; }
    }

}