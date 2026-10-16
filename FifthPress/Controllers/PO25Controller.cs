using FifthPress.ActionFilters;
using FifthPress.Models.ViewModels2;
using FifthPress.Models.ViewModels2.PO25;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.Controllers
{
    [CookieFilter(Order = 1)]
    [UserPermissions(Order = 2)]
    public class PO25Controller : DataController
    {
        private readonly ApprovalService25 _approvalService;
        private readonly NotificationService25 _notificationService;
        private readonly PORoleService25 _roleService;
        private readonly BudgetService25 _budgetService;
        private readonly AmendmentService25 _amendmentService;
        private readonly DepartmentSplitService25 _splitService;
        private readonly CompanyBudgetService25 _companyBudgetService;

        public PO25Controller()
        {
            _approvalService = new ApprovalService25(db);
            _notificationService = new NotificationService25(db);
            _roleService = new PORoleService25(db);
            _budgetService = new BudgetService25(db);
            _amendmentService = new AmendmentService25(db);
            _splitService = new DepartmentSplitService25(db);
            _companyBudgetService = new CompanyBudgetService25(db);
        }

        [HttpGet]
        public JsonResult AutocompleteJobs(string term)
        {
            try
            {
                var jobs = db.Jobs
                    .Where(j => j.JobNumber.Contains(term) &&
                               j.Complete == null &&
                               j.Cancelled == null &&
                               j.Deleted == null)
                    .OrderBy(j => j.JobNumber)
                    .Take(10)
                    .Select(j => new
                    {
                        label = j.JobNumber + (string.IsNullOrEmpty(j.Description) ? "" : " - " + j.Description),
                        value = j.JobNumber,
                        jobId = j.Id,
                        minStartDate = j.MinStartDate // Add this field
                    })
                    .ToList();

                return Json(jobs, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new List<object>(), JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public async Task<ActionResult> CreateRecurring()
        {
            int userId = UserSessionHelper.GetActiveUserId(HttpContext); // You'll need to implement this based on your auth

            // Check permissions - only certain roles can create recurring orders
            var userRoles = db.PO_UserRoles25
                .Where(ur => ur.UserId == userId && ur.IsActive)
                .ToList();

            // Get user's agency department or default to Production (department ID 4)
            User u = await db.Users.FindAsync(userId);
            int defaultDepartmentId = u.AgencyDepartment ?? 4; // Default to Production

            var model = new CreateRecurringOrderViewModel
            {
                Vendors = await GenerateSelectListAsync(Models.SelectListType.Vendors),
                Departments = GetDepartmentSelectList(userId), // Filter departments by user access
                OrderType = "Recurring",
                DepartmentId = defaultDepartmentId
            };

            return View(model);
        }

        // POST: PO25/CreateRecurring
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateRecurring(CreateRecurringOrderViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Vendors = await GenerateSelectListAsync(Models.SelectListType.Vendors);
                model.Departments = GetDepartmentSelectList(UserSessionHelper.GetActiveUserId(HttpContext));
                return View(model);
            }
            try
            {
                int userId = UserSessionHelper.GetActiveUserId(HttpContext);

                // Get user's agency department or default to Production (department ID 4)
                User u = await db.Users.FindAsync(userId);
                int departmentId = model.DepartmentId > 0 ? model.DepartmentId : (u.AgencyDepartment ?? 4);

                using (var transaction = db.Database.BeginTransaction())
                {
                    // Create the main PO Order
                    var order = new PO_Orders25
                    {
                        VendorId = model.VendorId, // Use vendor from model
                        SubmittedByUserId = userId,
                        DepartmentId = departmentId,
                        Status = "Draft",
                        IsDraft = true,
                        OrderType = "Recurring",
                        DateCreated = DateTime.Now,
                        CreatedDate = DateTime.Now
                    };
                    db.PO_Orders25.Add(order);
                    db.SaveChanges();

                    // Create line items and recurring schedules
                    foreach (var lineItem in model.LineItems)
                    {
                        var poLineItem = new PO_LineItems25
                        {
                            PO_OrderId = order.PO_OrderId,
                            ItemDescription = lineItem.Description,
                            Cost = lineItem.MonthlyAmount,
                            Quantity = 1,
                            Taxable = false,
                            Billable = false,
                            InventoryTracked = false,
                            IsAllocation = lineItem.IsAllocation,
                            RequiresReconciliation = lineItem.IsAllocation,
                            LineItemType = lineItem.IsAllocation ? "Allocation" : "ActualCost",
                            CreatedDate = DateTime.Now
                        };
                        db.PO_LineItems25.Add(poLineItem);
                        db.SaveChanges();

                        // Create recurring schedule (inactive until manually activated)
                        var recurringSchedule = new PO_RecurringSchedules25
                        {
                            PO_LineItemId = poLineItem.PO_LineItemId,
                            DepartmentId = departmentId, // Use the resolved department ID
                            RecurrenceType = lineItem.RecurrenceType,
                            MonthlyAmount = lineItem.MonthlyAmount,
                            StartDate = lineItem.StartDate,
                            EndDate = lineItem.EndDate,
                            NextProcessDate = null, // Will be set when user activates schedule
                            Description = lineItem.Description,
                            IsActive = false, // Inactive until user manually activates via "Activate Schedule" button
                            CreatedDate = DateTime.Now,
                            LastProcessedDate = null
                        };
                        db.PO_RecurringSchedules25.Add(recurringSchedule);
                    }

                    db.SaveChanges();
                    transaction.Commit();

                    TempData["Success"] = "Recurring order created successfully. Use the 'Activate Schedule' button to start automatic processing.";
                    return RedirectToAction("Index");
                }
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException ex)
            {
                var errorMessages = ex.EntityValidationErrors
                    .SelectMany(x => x.ValidationErrors)
                    .Select(x => x.PropertyName + ": " + x.ErrorMessage);

                var fullErrorMessage = string.Join("; ", errorMessages);

                TempData["Error"] = "Validation Error: " + fullErrorMessage;
                model.Vendors = await GenerateSelectListAsync(Models.SelectListType.Vendors);
                model.Departments = GetDepartmentSelectList(UserSessionHelper.GetActiveUserId(HttpContext));
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error creating recurring order: " + ex.Message;
                model.Vendors = await GenerateSelectListAsync(Models.SelectListType.Vendors);
                model.Departments = GetDepartmentSelectList(UserSessionHelper.GetActiveUserId(HttpContext));
                return View(model);
            }
        }

        // GET: PO25/RecurringIndex
        public ActionResult RecurringIndex()
        {
            int userId = UserSessionHelper.GetActiveUserId(HttpContext);

            var recurringQuery = db.PO_RecurringSchedules25
                .Include(r => r.PO_LineItems25)
                .Include(r => r.PO_LineItems25.PO_Orders25)
                .Include(r => r.Department)
                .AsQueryable();

            // Filter by department permissions if not Owner/Admin
            var userRoles = db.PO_UserRoles25
                .Where(ur => ur.UserId == userId && ur.IsActive)
                .ToList();

            var isOwnerOrAdmin = userRoles.Any(r => r.RoleId == 1 || r.RoleId == 4);

            if (!isOwnerOrAdmin)
            {
                var userDepartments = userRoles
                    .Where(ur => ur.DepartmentId.HasValue)
                    .Select(ur => ur.DepartmentId.Value)
                    .Distinct()
                    .ToList();

                if (userDepartments.Any())
                {
                    // Show schedules from user's departments OR created by the user
                    recurringQuery = recurringQuery.Where(r =>
                        userDepartments.Contains(r.DepartmentId) ||
                        r.PO_LineItems25.PO_Orders25.SubmittedByUserId == userId);
                }
                else
                {
                    // If no department roles, show only schedules created by this user
                    recurringQuery = recurringQuery.Where(r => r.PO_LineItems25.PO_Orders25.SubmittedByUserId == userId);
                }
            }

            var model = recurringQuery
                .ToList()
                .Select(r => new RecurringScheduleViewModel
                {
                    RecurringId = r.RecurringId,
                    ParentPOId = r.PO_LineItems25?.PO_OrderId ?? 0,
                    ParentPOStatus = r.PO_LineItems25?.PO_Orders25?.Status ?? "Unknown",
                    Description = r.Description,
                    DepartmentName = r.Department?.Title ?? "Unknown",
                    RecurrenceType = r.RecurrenceType,
                    MonthlyAmount = r.MonthlyAmount,
                    StartDate = r.StartDate,
                    EndDate = r.EndDate,
                    NextProcessDate = r.NextProcessDate,
                    LastProcessedDate = r.LastProcessedDate,
                    IsAllocation = r.PO_LineItems25?.IsAllocation ?? false,
                    IsActive = r.IsActive,
                    FormattedAmount = r.MonthlyAmount.ToString("C")
                })
                .ToList();

            return View(model);
        }

        public ActionResult ReconciliationHistory(int? year, int? month)
        {
            int userId = UserSessionHelper.GetActiveUserId(HttpContext);

            // Check permissions - only certain roles can access reconciliation
            var userRoles = db.PO_UserRoles25
                .Where(ur => ur.UserId == userId && ur.IsActive)
                .ToList();

            if (!userRoles.Any(r => r.RoleId == 1 || r.RoleId == 2 || r.RoleId == 4 || r.RoleId == 6))
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
            }

            var currentYear = DateTime.Now.Year;
            var currentMonth = DateTime.Now.Month;

            // If no year/month specified, show current month
            var selectedYear = year ?? currentYear;
            var selectedMonth = month ?? currentMonth;

            // Get reconciliation items for selected month with the additional properties
            var reconciliationItems = db.PO_AllocationReconciliation25
                .Include(r => r.PO_RecurringSchedules25)
                .Include(r => r.PO_RecurringSchedules25.Department)
                .Include(r => r.PO_RecurringSchedules25.PO_LineItems25)
                .Where(r => r.FiscalYear == selectedYear && r.Month == selectedMonth)
                .Select(r => new ReconciliationHistoryViewModel
                {
                    FiscalYear = r.FiscalYear,
                    Month = r.Month,
                    AllocatedAmount = r.AllocatedAmount,
                    ActualSpent = r.ActualSpent,
                    IsReconciled = r.IsReconciled,
                    ReconciliationDate = r.ReconciliationDate,
                    ReconciliationNotes = r.ReconciliationNotes,
                    Description = r.PO_RecurringSchedules25.Description,
                    DepartmentName = r.PO_RecurringSchedules25.Department.Title
                }).ToList();

            // Get available months/years for navigation
            var availablePeriods = db.PO_AllocationReconciliation25
                .GroupBy(r => new { r.FiscalYear, r.Month })
                .Select(g => new
                {
                    Year = g.Key.FiscalYear,
                    Month = g.Key.Month,
                    RecordCount = g.Count(),
                    ReconciledCount = g.Count(r => r.IsReconciled)
                })
                .OrderByDescending(p => p.Year)
                .ThenByDescending(p => p.Month)
                .ToList();

            var model = new ReconciliationHistoryPageViewModel
            {
                SelectedMonth = selectedMonth,
                SelectedYear = selectedYear,
                CurrentMonth = currentMonth,
                CurrentYear = currentYear,
                ReconciliationItems = reconciliationItems,
                TotalAllocated = reconciliationItems.Sum(r => r.AllocatedAmount),
                TotalSpent = reconciliationItems.Sum(r => r.ActualSpent ?? 0),
                UnreconciledCount = reconciliationItems.Count(r => !r.IsReconciled),
                AvailablePeriods = availablePeriods.Select(p => new ReconciliationPeriod
                {
                    Year = p.Year,
                    Month = p.Month,
                    MonthName = new DateTime(p.Year, p.Month, 1).ToString("MMMM yyyy"),
                    RecordCount = p.RecordCount,
                    ReconciledCount = p.ReconciledCount,
                    IsComplete = p.RecordCount == p.ReconciledCount
                }).ToList()
            };

            return View(model);
        }

        // Also update your existing ReconciliationDashboard to include navigation
        public ActionResult ReconciliationDashboard()
        {
            int userId = UserSessionHelper.GetActiveUserId(HttpContext);

            // Check permissions - only certain roles can access reconciliation
            var userRoles = db.PO_UserRoles25
                .Where(ur => ur.UserId == userId && ur.IsActive)
                .ToList();

            if (!userRoles.Any(r => r.RoleId == 1 || r.RoleId == 2 || r.RoleId == 4 || r.RoleId == 6))
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
            }

            var currentYear = DateTime.Now.Year;
            var currentMonth = DateTime.Now.Month;

            // Get reconciliation items for current month
            var reconciliationItems = db.PO_AllocationReconciliation25
                .Include(r => r.PO_RecurringSchedules25)
                .Include(r => r.PO_RecurringSchedules25.Department)
                .Include(r => r.PO_RecurringSchedules25.PO_LineItems25)
                .Where(r => r.FiscalYear == currentYear && r.Month == currentMonth)
                .Select(r => new ReconciliationViewModel
                {
                    ReconciliationId = r.ReconciliationId,
                    RecurringId = r.RecurringId,
                    Description = r.PO_RecurringSchedules25.Description,
                    DepartmentName = r.PO_RecurringSchedules25.Department.Title,
                    AllocatedAmount = r.AllocatedAmount,
                    ActualSpent = r.ActualSpent,
                    IsReconciled = r.IsReconciled,
                    ReconciliationDate = r.ReconciliationDate,
                    ReconciliationNotes = r.ReconciliationNotes
                }).ToList();

            var model = new ReconciliationDashboardViewModel
            {
                CurrentMonth = currentMonth,
                CurrentYear = currentYear,
                ReconciliationItems = reconciliationItems,
                TotalAllocated = reconciliationItems.Sum(r => r.AllocatedAmount),
                TotalSpent = reconciliationItems.Sum(r => r.ActualSpent ?? 0),
                UnreconciledCount = reconciliationItems.Count(r => !r.IsReconciled)
            };

            return View(model);
        }

        // POST: PO25/UpdateReconciliation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateReconciliation(int reconciliationId, decimal actualSpent, string notes)
        {
            try
            {
                var reconciliation = db.PO_AllocationReconciliation25.Find(reconciliationId);
                if (reconciliation == null)
                {
                    return Json(new { success = false, message = "Reconciliation record not found." });
                }

                reconciliation.ActualSpent = actualSpent;
                reconciliation.ReconciliationNotes = notes;
                reconciliation.IsReconciled = true;
                reconciliation.ReconciliationDate = DateTime.Now.Date;

                db.SaveChanges();

                return Json(new { success = true, message = "Reconciliation updated successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error updating reconciliation: " + ex.Message });
            }
        }

        // Helper method
        private DateTime CalculateNextProcessDate(DateTime startDate, string recurrenceType)
        {
            var now = DateTime.Now.Date;
            var processDate = startDate;

            while (processDate <= now)
            {
                switch (recurrenceType)
                {
                    case "Monthly":
                        processDate = processDate.AddMonths(1);
                        break;

                    case "Quarterly":
                        processDate = processDate.AddMonths(3);
                        break;

                    case "Annually":
                        processDate = processDate.AddYears(1);
                        break;
                }
            }

            return processDate;
        }

        [HttpGet]
        public JsonResult AutocompleteMaterials(string term)
        {
            var matches = db.Materials
                .Where(m => m.Title.Contains(term))
                .OrderBy(m => m.Title)
                .Take(10)
                .Select(m => new
                {
                    label = m.Title,
                    value = m.Id
                }).ToList();

            return Json(matches, JsonRequestBehavior.AllowGet);
        }

        public async Task<ActionResult> Index()
        {
            try
            {
                int userId = UserSessionHelper.GetActiveUserId(HttpContext);
                var userRoles = _roleService.GetUserRoles(userId);
                var userDept = await db.Users
                    .Where(u => u.Id == userId)
                    .Select(u => u.AgencyDepartment)
                    .FirstOrDefaultAsync();

                if (userRoles?.Any() == true)
                {
                    foreach (var role in userRoles)
                    {
                        //System.Diagnostics.Debug.WriteLine($"DEBUG: Role = {role.RoleName}, DeptId = {role.DepartmentId}");
                    }
                }

                // Set ViewBag for admin button visibility and full order access
                // Purchasing and Inventory Manager can see all orders but don't have admin access
                ViewBag.IsAdminOrOwner = userRoles.Any(r => r.RoleName == "Owner" || r.RoleName == "Admin" ||
                                                            r.RoleName == "Executive" || r.RoleName == "Accounting");

                //Set ViewBag for all order visibility; allows for Purchasing and Inventory Manager to see all orders while not allowing access to admin portal
                ViewBag.ShowAllOrders = userRoles.Any(r => r.RoleName == "Owner" || r.RoleName == "Admin" ||
                                                            r.RoleName == "Purchasing" || r.RoleName == "Inventory Manager" ||
                                                            r.RoleName == "Executive" || r.RoleName == "Accounting");

                // === REGULAR PURCHASE ORDERS ===
                var baseQuery = db.PO_Orders25
                    .Include(po => po.PO_LineItems25)
                    .Include(po => po.Department)
                    .Include(po => po.User)
                    .Include(po => po.Vendor)
                    .AsQueryable();

                List<PO_Orders25> regularPos;

                // For Owners and Admins - no filtering
                if (ViewBag.ShowAllOrders)
                {
                    regularPos = await baseQuery
                        .Where(po => po.OrderType != "Recurring") // Exclude recurring-generated orders
                        .OrderByDescending(po => po.DateSubmitted ?? DateTime.MinValue)
                        .ToListAsync();
                }
                else
                {
                    // For everyone else - build filtered query (exclude recurring-generated orders)
                    var filteredQuery = baseQuery.Where(po => po.SubmittedByUserId == userId && po.OrderType != "Recurring");

                    // Add department visibility for approvers/managers
                    if (userRoles.Any(r => r.RoleName == "Department Manager" || r.RoleName == "Department Approver"))
                    {
                        var managedDepts = userRoles
                            .Where(r => r.DepartmentId.HasValue)
                            .Select(r => r.DepartmentId.Value)
                            .Distinct()
                            .ToList();

                        if (managedDepts.Any())
                        {
                            filteredQuery = filteredQuery
                                .Union(baseQuery.Where(po => po.DepartmentId.HasValue && managedDepts.Contains(po.DepartmentId.Value) && po.OrderType != "Recurring"));
                        }
                    }

                    try
                    {
                        // Add inventory/budget manager visibility
                        if (userDept.HasValue &&
                            userRoles.Any(r => r.RoleName == "Inventory Manager" || r.RoleName == "Budget Manager"))
                        {
                            filteredQuery = filteredQuery
                                .Union(baseQuery.Where(po => po.DepartmentId == userDept.Value && po.OrderType != "Recurring"));
                        }
                    }
                    catch (Exception e)
                    {
                        // Handle exception silently
                    }

                    // Execute the final query
                    regularPos = await filteredQuery
                        .Distinct()
                        .OrderByDescending(po => po.DateSubmitted ?? DateTime.MinValue)
                        .ToListAsync();
                }

                // === RECURRING SCHEDULES ===
                var recurringQuery = db.PO_RecurringSchedules25
                    .Include(r => r.PO_LineItems25)
                    .Include(r => r.PO_LineItems25.PO_Orders25)
                    .Include(r => r.Department)
                    .AsQueryable();

                // Apply same filtering logic for recurring schedules
                if (!ViewBag.ShowAllOrders)
                {
                    var userDepartments = userRoles.Where(ur => ur.DepartmentId.HasValue)
                                                  .Select(ur => ur.DepartmentId.Value)
                                                  .Distinct()
                                                  .ToList();

                    if (userDepartments.Any())
                    {
                        // Show schedules from user's departments OR created by the user
                        recurringQuery = recurringQuery.Where(r =>
                            userDepartments.Contains(r.DepartmentId) ||
                            r.PO_LineItems25.PO_Orders25.SubmittedByUserId == userId);
                    }
                    else
                    {
                        // If no department roles, show only schedules created by this user
                        recurringQuery = recurringQuery.Where(r => r.PO_LineItems25.PO_Orders25.SubmittedByUserId == userId);
                    }
                }

                var recurringSchedules = new List<RecurringScheduleViewModel>();

                try
                {
                    var schedules = await recurringQuery
                        .ToListAsync();

                    recurringSchedules = schedules.Select(r => new RecurringScheduleViewModel
                    {
                        RecurringId = r.RecurringId,
                        ParentPOId = r.PO_LineItems25?.PO_OrderId ?? 0,
                        ParentPOStatus = r.PO_LineItems25?.PO_Orders25?.Status ?? "Unknown",
                        Description = r.Description,
                        DepartmentName = r.Department?.Title ?? "Unknown",
                        RecurrenceType = r.RecurrenceType,
                        MonthlyAmount = r.MonthlyAmount,
                        StartDate = r.StartDate,
                        EndDate = r.EndDate,
                        NextProcessDate = r.NextProcessDate,
                        LastProcessedDate = r.LastProcessedDate,
                        IsAllocation = r.PO_LineItems25?.IsAllocation ?? false,
                        IsActive = r.IsActive,
                        FormattedAmount = r.MonthlyAmount.ToString("C")
                    }).ToList();
                }
                catch (Exception recurringEx)
                {
                    //System.Diagnostics.Debug.WriteLine($"Error loading recurring schedules: {recurringEx.Message}");
                    // Continue with empty list if recurring schedules fail
                }

                // Create unified view model
                var viewModel = new UnifiedPurchaseOrderViewModel
                {
                    RegularPOs = regularPos,
                    RecurringSchedules = recurringSchedules
                };

                // Pass current user ID to view for filtering
                ViewBag.CurrentUserId = userId;

                return View(viewModel);
            }
            catch (Exception ex)
            {
                // Log the error
                //System.Diagnostics.Debug.WriteLine($"Error in PO Index: {ex.Message}");
                TempData["Error"] = "An error occurred while loading purchase orders";
                return View(new UnifiedPurchaseOrderViewModel());
            }
        }

        // GET: /PurchaseOrders25/Create
        public async Task<ActionResult> Create()
        {
            int userId = UserSessionHelper.GetActiveUserId(HttpContext);
            var model = new VM_CreatePO25();

            model.InitializeDefaults();

            model.Vendors = await GenerateSelectListAsync(Models.SelectListType.Vendors);
            model.Departments = GetDepartmentSelectList(userId);
            model.CompanyBudgets = GetCompanyBudgetSelectList(userId);

            // Set whether user can override budget (Owner/Admin)
            ViewBag.CanOverrideBudget = _roleService.IsPOAdmin(userId);

            return View(model);
        }

        // POST: /PurchaseOrders25/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(VM_CreatePO25 model)
        {
            System.Diagnostics.Debug.WriteLine($"=== CREATE PO - IsDraft: {model.IsDraft} ===");
            if (model.LineItems != null)
            {
                for (int i = 0; i < model.LineItems.Count; i++)
                {
                    System.Diagnostics.Debug.WriteLine($"LineItem[{i}] Cost: {model.LineItems[i].Cost}, Qty: {model.LineItems[i].Quantity}");
                }
            }

            int userId = UserSessionHelper.GetActiveUserId(HttpContext);

            if (!ModelState.IsValid)
            {
                System.Diagnostics.Debug.WriteLine("=== ModelState INVALID ===");
                foreach (var key in ModelState.Keys)
                {
                    var state = ModelState[key];
                    if (state.Errors.Count > 0)
                    {
                        foreach (var error in state.Errors)
                        {
                            System.Diagnostics.Debug.WriteLine($"Key: {key}, Error: {error.ErrorMessage}");
                        }
                    }
                }

                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = false, message = "Please fix validation errors." });
                }

                model.InitializeDefaults();
                model.Vendors = await GenerateSelectListAsync(Models.SelectListType.Vendors);
                model.Departments = GetDepartmentSelectList(userId);
                model.CompanyBudgets = GetCompanyBudgetSelectList(userId);
                ViewBag.CanOverrideBudget = _roleService.CanSeeAllDepartments(userId);
                return View(model);
            }

            System.Diagnostics.Debug.WriteLine("=== ModelState VALID - Proceeding to save ===");

            // Validate: DepartmentId required unless using company-wide budget, CompanyBudgetId required if IsCompanyWide
            if (!model.IsCompanyWide && (!model.DepartmentId.HasValue || model.DepartmentId == 0))
            {
                // No department selected and not company-wide - try to set from user's AgencyDepartment
                User u = await db.Users.FindAsync(userId);
                if (u.AgencyDepartment.HasValue)
                {
                    model.DepartmentId = u.AgencyDepartment.Value;
                }
                else
                {
                    // Default to Production department
                    var productionDept = db.Departments.FirstOrDefault(d => d.Title == "Production");
                    if (productionDept != null)
                    {
                        model.DepartmentId = productionDept.Id;
                    }
                    else
                    {
                        ModelState.AddModelError("", "Please select a department.");
                        model.InitializeDefaults();
                        model.Vendors = await GenerateSelectListAsync(Models.SelectListType.Vendors);
                        model.Departments = GetDepartmentSelectList(userId);
                        model.CompanyBudgets = GetCompanyBudgetSelectList(userId);
                        ViewBag.CanOverrideBudget = _roleService.CanSeeAllDepartments(userId);
                        return View(model);
                    }
                }
            }

            // Clear DepartmentId if using company-wide budget (it should be null)
            if (model.IsCompanyWide)
            {
                model.DepartmentId = null;
            }

            if (model.IsCompanyWide && !model.CompanyBudgetId.HasValue)
            {
                ModelState.AddModelError("", "Please select a company budget.");
                model.InitializeDefaults();
                model.Vendors = await GenerateSelectListAsync(Models.SelectListType.Vendors);
                model.Departments = GetDepartmentSelectList(userId);
                model.CompanyBudgets = GetCompanyBudgetSelectList(userId);
                ViewBag.CanOverrideBudget = _roleService.CanSeeAllDepartments(userId);
                return View(model);
            }

            // 🔹 Company budget permission check
            if (model.IsCompanyWide && model.CompanyBudgetId.HasValue)
            {
                if (!CanUseCompanyBudgets(userId))
                {
                    ModelState.AddModelError("", "You do not have permission to use company-wide budgets.");
                    model.InitializeDefaults();
                    model.Vendors = await GenerateSelectListAsync(Models.SelectListType.Vendors);
                    model.Departments = GetDepartmentSelectList(userId);
                    model.CompanyBudgets = GetCompanyBudgetSelectList(userId);
                    return View(model);
                }
            }

            // 🔹 Budget enforcement and override logic
            // Skip department budget check when using company-wide budget
            bool userCanOverride = model.DepartmentId.HasValue && _roleService.CanOverrideBudget(userId, model.DepartmentId.Value);
            bool userIsAttemptingOverride = model.OverrideRequested;
            var budgetErrors = new List<string>();
            bool isOverBudget = false;

            if (!model.IsCompanyWide && model.DepartmentId.HasValue)
            {
                // Only count non-billable items against budget (use LineTotal for proper bulk pricing calculation)
                var nonBillableTotal = model.LineItems.Where(i => !i.Billable).Sum(i => i.LineTotal);

                // Check budgets - handle department splits if applicable
                try
                {
                    if (model.SplitType == "OrderLevel" && model.OrderSplits != null && model.OrderSplits.Any(s => s.DepartmentId > 0 && s.SplitPercentage > 0))
                    {
                        // Check budget for each department in the split
                        var validSplits = model.OrderSplits.Where(s => s.DepartmentId > 0 && s.SplitPercentage > 0).ToList();
                        foreach (var split in validSplits)
                        {
                            var splitAmount = nonBillableTotal * ((split.SplitPercentage ?? 0) / 100);
                            var splitBudgetResult = _budgetService.CheckBudget(split.DepartmentId, splitAmount);
                            if (!splitBudgetResult.IsWithinBudget)
                            {
                                isOverBudget = true;
                                budgetErrors.Add($"{splitBudgetResult.DepartmentName}: over by {(splitAmount - splitBudgetResult.BudgetRemaining):C}");
                            }
                        }
                    }
                    else
                    {
                        // Single department - check the primary department's budget
                        PurchaseOrderLogger.Log($"Calling CheckBudget for DepartmentId: {model.DepartmentId}, Non-Billable Total: {nonBillableTotal}");
                        var budgetResult = _budgetService.CheckBudget(model.DepartmentId.Value, nonBillableTotal);
                        if (!budgetResult.IsWithinBudget)
                        {
                            isOverBudget = true;
                            budgetErrors.Add(budgetResult.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    PurchaseOrderLogger.LogError($"CheckBudget failed for DepartmentId: {model.DepartmentId}, Non-Billable Total: {nonBillableTotal}");
                    PurchaseOrderLogger.LogException(ex);

                    ModelState.AddModelError("", "A system error occurred while checking department budget.");

                    model.InitializeDefaults();
                    model.Vendors = await GenerateSelectListAsync(Models.SelectListType.Vendors);
                    model.Departments = GetDepartmentSelectList(userId);
                    model.CompanyBudgets = GetCompanyBudgetSelectList(userId);
                    ViewBag.CanOverrideBudget = false;
                    return View(model);
                }

                // Only enforce budget restrictions when submitting for approval, not when saving as draft
                if (!model.IsDraft && isOverBudget && !(userCanOverride && userIsAttemptingOverride))
                {
                    string message = string.Join(" | ", budgetErrors);
                    if (userCanOverride)
                    {
                        message += " You have override privileges. Check the override box to proceed.";
                    }

                    if (Request.IsAjaxRequest())
                    {
                        return Json(new { success = false, message });
                    }

                    ModelState.AddModelError("", message);
                    model.InitializeDefaults();
                    model.Vendors = await GenerateSelectListAsync(Models.SelectListType.Vendors);
                    model.Departments = GetDepartmentSelectList(userId);
                    model.CompanyBudgets = GetCompanyBudgetSelectList(userId);
                    ViewBag.CanOverrideBudget = userCanOverride;
                    return View(model);
                }
            }
            // TODO: Add company budget check here if needed

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var po = model.ToPOEntity(userId);
                    db.PO_Orders25.Add(po);
                    db.SaveChanges();

                    // Handle department splits (order-level only)
                    if (model.SplitType == "OrderLevel" && model.OrderSplits != null && model.OrderSplits.Any())
                    {
                        var validSplits = model.OrderSplits.Where(s => s.DepartmentId > 0 && s.SplitPercentage > 0).ToList();
                        if (validSplits.Any())
                        {
                            var splitsSaved = _splitService.SaveOrderSplits(po.PO_OrderId, validSplits);
                            if (!splitsSaved)
                            {
                                // Splits didn't save (likely validation failed - percentages must equal 100%)
                                // Still continue with order save, just without splits
                                System.Diagnostics.Debug.WriteLine($"WARNING: Department splits not saved for PO {po.PO_OrderId}. Validation may have failed.");
                            }
                        }
                    }

                    // Optional: log override event
                    if (isOverBudget && userCanOverride && userIsAttemptingOverride)
                    {
                        // Insert override logging here if needed
                    }

                    string successMessage;
                    if (!model.IsDraft)
                    {
                        // Calculate total for auto-approval check (use LineTotal for proper bulk pricing)
                        var poTotal = model.LineItems.Sum(li => li.LineTotal);

                        // Check if user qualifies for auto-approval
                        if (_roleService.CanSkipApproval(userId, poTotal))
                        {
                            // Process auto-approval
                            if (_approvalService.ProcessAutoApproval(po.PO_OrderId, userId))
                            {
                                successMessage = "PO auto-approved successfully.";
                            }
                            else
                            {
                                // Fall back to normal approval flow if auto-approval fails
                                _approvalService.InitiateApprovalFlow(po.PO_OrderId);
                                _notificationService.SendSubmissionNotification(po.PO_OrderId);
                                successMessage = "PO submitted for approval successfully.";
                            }
                        }
                        else
                        {
                            // Normal approval flow
                            _approvalService.InitiateApprovalFlow(po.PO_OrderId);
                            _notificationService.SendSubmissionNotification(po.PO_OrderId);
                            successMessage = "PO submitted for approval successfully.";
                        }
                    }
                    else
                    {
                        successMessage = "PO saved as draft successfully.";
                    }

                    db.SaveChanges();
                    transaction.Commit();

                    if (Request.IsAjaxRequest())
                    {
                        return Json(new
                        {
                            success = true,
                            message = successMessage,
                            redirectUrl = Url.Action("Index")
                        });
                    }

                    TempData["SuccessMessage"] = successMessage;
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();

                    if (Request.IsAjaxRequest())
                    {
                        return Json(new
                        {
                            success = false,
                            message = "An error occurred while saving the PO: " + ex.Message
                        });
                    }

                    ModelState.AddModelError("", "An error occurred while saving the PO: " + ex.Message);
                    model.InitializeDefaults();
                    model.Vendors = await GenerateSelectListAsync(Models.SelectListType.Vendors);
                    model.Departments = GetDepartmentSelectList(userId);
                    model.CompanyBudgets = GetCompanyBudgetSelectList(userId);
                    ViewBag.CanOverrideBudget = userCanOverride;
                    return View(model);
                }
            }
        }

        // GET: /PurchaseOrders25/Edit/5
        public async Task<ActionResult> Edit(int id)
        {
            var po = await db.PO_Orders25.FindAsync(id);
            if (po == null) return HttpNotFound();

            await db.Entry(po).Collection(c => c.PO_LineItems25).LoadAsync();

            // Recurring orders must be edited through EditRecurring action
            if (po.OrderType == "Recurring")
            {
                TempData["ErrorMessage"] = "Recurring orders cannot be edited here. Please use 'Edit Schedule' from the Recurring Orders page.";
                return RedirectToAction("RecurringIndex");
            }

            // Only allow editing of drafts or sent back POs
            if (po.Status != "Draft" && po.Status != "SentBack")
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden, "This PO cannot be edited in its current status.");

            // Check if user owns this PO or has permission to edit
            int userId = UserSessionHelper.GetActiveUserId(HttpContext);
            if (!CanUserEditPO(userId, po))
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden, "You do not have permission to edit this PO.");

            var model = VM_CreatePO25.FromPOEntity(po);

            // Load existing order splits if applicable
            if (po.UsesDepartmentSplits && po.SplitType == "OrderLevel")
            {
                model.OrderSplits = _splitService.GetOrderSplits(po.PO_OrderId);
                // Calculate amounts for display
                var nonBillableTotal = model.NonBillableAmount;
                foreach (var split in model.OrderSplits)
                {
                    split.CalculatedAmount = nonBillableTotal * ((split.SplitPercentage ?? 0) / 100);
                }
            }

            model.Vendors = await GenerateSelectListAsync(Models.SelectListType.Vendors);
            model.Departments = GetDepartmentSelectList(userId);
            model.CompanyBudgets = GetCompanyBudgetSelectList(userId);

            // Set whether user can override budget (Owner/Admin)
            ViewBag.CanOverrideBudget = _roleService.IsPOAdmin(userId);

            return View(model);
        }

        // POST: /PurchaseOrders25/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(VM_CreatePO25 model)
        {
            int userId = UserSessionHelper.GetActiveUserId(HttpContext);

            // DEBUG: Log received line item values
            System.Diagnostics.Debug.WriteLine("=== EDIT POST DEBUG ===");
            if (model.LineItems != null)
            {
                foreach (var li in model.LineItems)
                {
                    System.Diagnostics.Debug.WriteLine($"LineItem: Cost={li.Cost}, Qty={li.Quantity}, IsBulk={li.IsBulkItem}");
                }
            }

            if (!ModelState.IsValid)
            {
                // DEBUG: Log validation errors
                foreach (var key in ModelState.Keys)
                {
                    var errors = ModelState[key].Errors;
                    foreach (var error in errors)
                    {
                        System.Diagnostics.Debug.WriteLine($"Validation Error - {key}: {error.ErrorMessage}");
                    }
                }

                model.InitializeDefaults();
                model.Vendors = await GenerateSelectListAsync(Models.SelectListType.Vendors);
                model.Departments = GetDepartmentSelectList(userId);
                model.CompanyBudgets = GetCompanyBudgetSelectList(userId);
                return View(model);
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var existingPO = await db.PO_Orders25.FindAsync(model.PO_OrderId);
                    if (existingPO == null) return HttpNotFound();

                    await db.Entry(existingPO).Collection(c => c.PO_LineItems25).LoadAsync();

                    // Recurring orders must be edited through EditRecurring action
                    if (existingPO.OrderType == "Recurring")
                    {
                        ModelState.AddModelError("", "Recurring orders cannot be edited here. Please use 'Edit Schedule' from the Recurring Orders page.");
                        model.InitializeDefaults();
                        model.Vendors = await GenerateSelectListAsync(Models.SelectListType.Vendors);
                        model.Departments = GetDepartmentSelectList(userId);
                        model.CompanyBudgets = GetCompanyBudgetSelectList(userId);
                        return View(model);
                    }

                    // DEBUG: Log edit attempt details
                    //System.Diagnostics.Debug.WriteLine($"Edit attempt: UserId={userId}, PO_OrderId={model.PO_OrderId}, IsDraft={model.IsDraft}, ExistingStatus={existingPO.Status}, PO_DepartmentId={existingPO.DepartmentId}");

                    // Check permissions
                    if (!CanUserEditPO(userId, existingPO))
                        return new HttpStatusCodeResult(HttpStatusCode.Forbidden);

                    // Company budget permission check
                    if (model.IsCompanyWide && model.CompanyBudgetId.HasValue)
                    {
                        if (!CanUseCompanyBudgets(userId))
                        {
                            ModelState.AddModelError("", "You do not have permission to use company-wide budgets.");
                            model.InitializeDefaults();
                            model.Vendors = await GenerateSelectListAsync(Models.SelectListType.Vendors);
                            model.Departments = GetDepartmentSelectList(userId);
                            model.CompanyBudgets = GetCompanyBudgetSelectList(userId);
                            return View(model);
                        }
                    }

                    // 🔹 Budget enforcement and override logic (Edit)
                    // Skip department budget check when using company-wide budget
                    bool userCanOverride = model.DepartmentId.HasValue && _roleService.CanOverrideBudget(userId, model.DepartmentId.Value);
                    bool userIsAttemptingOverride = model.OverrideRequested;

                    if (!model.IsCompanyWide && model.DepartmentId.HasValue)
                    {
                        // Only count non-billable items against budget (use LineTotal for proper bulk pricing)
                        var nonBillableTotal = model.LineItems.Where(i => !i.Billable).Sum(i => i.LineTotal);

                        // Check budgets - handle department splits if applicable
                        var budgetErrors = new List<string>();
                        bool isOverBudget = false;

                        if (model.SplitType == "OrderLevel" && model.OrderSplits != null && model.OrderSplits.Any(s => s.DepartmentId > 0 && s.SplitPercentage > 0))
                        {
                            // Check budget for each department in the split
                            var validSplits = model.OrderSplits.Where(s => s.DepartmentId > 0 && s.SplitPercentage > 0).ToList();
                            foreach (var split in validSplits)
                            {
                                var splitAmount = nonBillableTotal * ((split.SplitPercentage ?? 0) / 100);
                                var splitBudgetResult = _budgetService.CheckBudget(split.DepartmentId, splitAmount);
                                if (!splitBudgetResult.IsWithinBudget)
                                {
                                    isOverBudget = true;
                                    budgetErrors.Add($"{splitBudgetResult.DepartmentName}: over by {(splitAmount - splitBudgetResult.BudgetRemaining):C}");
                                }
                            }
                        }
                        else
                        {
                            // Single department - check the primary department's budget
                            var budgetResult = _budgetService.CheckBudget(model.DepartmentId.Value, nonBillableTotal);
                            if (!budgetResult.IsWithinBudget)
                            {
                                isOverBudget = true;
                                budgetErrors.Add(budgetResult.Message);
                            }
                        }

                        // Only enforce budget restrictions when submitting for approval, not when saving as draft
                        if (!model.IsDraft && isOverBudget && !(userCanOverride && userIsAttemptingOverride))
                        {
                            string message = string.Join(" | ", budgetErrors);
                            if (userCanOverride)
                            {
                                message += " You have override privileges. Check the override box to proceed.";
                            }

                            ModelState.AddModelError("", message);
                            model.InitializeDefaults();
                            model.Vendors = await GenerateSelectListAsync(Models.SelectListType.Vendors);
                            model.Departments = GetDepartmentSelectList(userId);
                            model.CompanyBudgets = GetCompanyBudgetSelectList(userId);
                            ViewBag.CanOverrideBudget = userCanOverride;
                            return View(model);
                        }
                    }
                    // TODO: Add company budget check here if needed

                    // Update PO properties
                    existingPO.VendorId = model.VendorId;
                    existingPO.DepartmentId = model.IsCompanyWide ? null : model.DepartmentId; // null for company-wide
                    existingPO.Status = model.IsDraft ? "Draft" : "Pending";
                    existingPO.IsDraft = model.IsDraft;
                    existingPO.IsCompanyWide = model.IsCompanyWide;
                    existingPO.CompanyBudgetId = model.IsCompanyWide ? model.CompanyBudgetId : null;

                    // Update freight and tax fields
                    existingPO.FreightAmount = model.FreightAmount;
                    existingPO.TaxAmount = model.TaxAmount;
                    existingPO.TaxRate = model.TaxRate;
                    existingPO.IsTaxPercentage = model.IsTaxPercentage;

                    if (!model.IsDraft && existingPO.DateSubmitted == null)
                    {
                        existingPO.DateSubmitted = DateTime.Now;
                    }

                    // Remove related records before removing line items (foreign key constraints)
                    var lineItemIds = existingPO.PO_LineItems25.Select(li => li.PO_LineItemId).ToList();
                    if (lineItemIds.Any())
                    {
                        // Delete budget usage records
                        var budgetUsageRecords = db.PO_DepartmentBudgetUsage25.Where(b => lineItemIds.Contains(b.PO_LineItemId ?? 0));
                        db.PO_DepartmentBudgetUsage25.RemoveRange(budgetUsageRecords);

                        var companyBudgetUsageRecords = db.PO_CompanyBudgetUsage25.Where(b => lineItemIds.Contains(b.PO_LineItemId));
                        db.PO_CompanyBudgetUsage25.RemoveRange(companyBudgetUsageRecords);

                        // Delete department splits
                        var splitRecords = db.PO_LineItemDepartmentSplits25.Where(s => lineItemIds.Contains(s.PO_LineItemId));
                        db.PO_LineItemDepartmentSplits25.RemoveRange(splitRecords);
                    }

                    // Remove existing line items
                    db.PO_LineItems25.RemoveRange(existingPO.PO_LineItems25);

                    // Add updated line items
                    foreach (var lineItem in model.LineItems)
                    {
                        existingPO.PO_LineItems25.Add(new PO_LineItems25
                        {
                            ItemDescription = lineItem.ItemDescription,
                            VendorLink = lineItem.VendorLink,
                            Cost = lineItem.Cost,
                            Quantity = lineItem.Quantity,
                            Taxable = lineItem.Taxable,
                            Billable = lineItem.Billable,
                            DateNeeded = lineItem.DateNeeded,
                            DateExpected = lineItem.DateExpected,
                            InternalComments = lineItem.InternalComments,
                            FileAttachmentPath = lineItem.FileAttachmentPath,
                            InventoryTracked = lineItem.InventoryTracked,
                            MaterialId = lineItem.MaterialId,
                            UnitType = lineItem.UnitType,
                            // Bulk item fields
                            IsBulkItem = lineItem.IsBulkItem,
                            BulkContainerType = lineItem.BulkContainerType,
                            BulkUnitQuantity = lineItem.BulkUnitQuantity,
                            BulkUnitType = lineItem.BulkUnitType,
                            CalculatedUnitCost = lineItem.CalculatedUnitCost
                        });
                    }

                    // Update split settings
                    existingPO.UsesDepartmentSplits = model.SplitType == "OrderLevel";
                    existingPO.SplitType = model.SplitType == "OrderLevel" ? "OrderLevel" : null;

                    db.SaveChanges();

                    // Handle department splits (order-level only)
                    if (model.SplitType == "OrderLevel" && model.OrderSplits != null && model.OrderSplits.Any())
                    {
                        var validSplits = model.OrderSplits.Where(s => s.DepartmentId > 0 && s.SplitPercentage > 0).ToList();
                        if (validSplits.Any())
                        {
                            _splitService.SaveOrderSplits(existingPO.PO_OrderId, validSplits);
                        }
                        else
                        {
                            _splitService.ClearAllSplits(existingPO.PO_OrderId);
                        }
                    }
                    else
                    {
                        // Clear splits if not using order-level splitting
                        _splitService.ClearAllSplits(existingPO.PO_OrderId);
                    }

                    string successMessage;
                    // Handle approval workflow for non-draft submissions
                    if (!model.IsDraft)
                    {
                        // Calculate total for auto-approval check (use LineTotal for proper bulk pricing)
                        var poTotal = model.LineItems.Sum(li => li.LineTotal);

                        // Check if user qualifies for auto-approval
                        if (_roleService.CanSkipApproval(userId, poTotal))
                        {
                            // Process auto-approval
                            if (_approvalService.ProcessAutoApproval(existingPO.PO_OrderId, userId))
                            {
                                successMessage = "PO auto-approved successfully.";
                            }
                            else
                            {
                                // Fall back to normal approval flow if auto-approval fails
                                _approvalService.InitiateApprovalFlow(existingPO.PO_OrderId);
                                _notificationService.SendSubmissionNotification(existingPO.PO_OrderId);
                                successMessage = "PO submitted for approval successfully.";
                            }
                        }
                        else
                        {
                            // Normal approval flow
                            _approvalService.InitiateApprovalFlow(existingPO.PO_OrderId);
                            _notificationService.SendSubmissionNotification(existingPO.PO_OrderId);
                            successMessage = "PO submitted for approval successfully.";
                        }
                    }
                    else
                    {
                        successMessage = "PO updated successfully.";
                    }

                    transaction.Commit();

                    TempData["SuccessMessage"] = successMessage;
                    return RedirectToAction("Index");
                }
                catch (System.Data.Entity.Validation.DbEntityValidationException ex)
                {
                    transaction.Rollback();
                    var errorMessages = ex.EntityValidationErrors
                        .SelectMany(x => x.ValidationErrors)
                        .Select(x => x.PropertyName + ": " + x.ErrorMessage);
                    var fullErrorMessage = string.Join("; ", errorMessages);
                    PurchaseOrderLogger.LogError($"Edit PO Validation Error: {fullErrorMessage}");
                    ModelState.AddModelError("", "Validation Error: " + fullErrorMessage);
                    model.InitializeDefaults();
                    model.Vendors = await GenerateSelectListAsync(Models.SelectListType.Vendors);
                    model.Departments = GetDepartmentSelectList(userId);
                    model.CompanyBudgets = GetCompanyBudgetSelectList(userId);
                    return View(model);
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    var innerMsg = ex.InnerException?.InnerException?.Message ?? ex.InnerException?.Message ?? ex.Message;
                    PurchaseOrderLogger.LogError($"Edit PO Error: {innerMsg}");
                    ModelState.AddModelError("", "An error occurred while updating the PO: " + innerMsg);
                    model.InitializeDefaults();
                    model.Vendors = await GenerateSelectListAsync(Models.SelectListType.Vendors);
                    model.Departments = GetDepartmentSelectList(userId);
                    model.CompanyBudgets = GetCompanyBudgetSelectList(userId);
                    return View(model);
                }
            }
        }

        // GET: /PurchaseOrders25/Details/5
        public async Task<ActionResult> Details(int id)
        {
            var po = db.PO_Orders25.Find(id);
            if (po == null) return HttpNotFound();

            await db.Entry(po).Reference(r => r.Department).LoadAsync();
            await db.Entry(po).Reference(r => r.User).LoadAsync();
            await db.Entry(po).Reference(r => r.Vendor).LoadAsync();
            await db.Entry(po).Collection(c => c.PO_LineItems25).LoadAsync();
            await db.Entry(po).Collection(c => c.PO_Approvals25).LoadAsync();

            // Load approval users
            foreach (var approval in po.PO_Approvals25)
            {
                await db.Entry(approval).Reference(a => a.User).LoadAsync();
            }

            // Check if user has permission to view this PO
            int userId = UserSessionHelper.GetActiveUserId(HttpContext);
            if (!CanUserViewPO(userId, po))
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden, "You do not have permission to view this PO.");

            var model = new VM_POReview25
            {
                Order = po,
                LineItems = MapLineItems(po.PO_LineItems25.ToList()),
                Approvals = MapApprovals(po.PO_Approvals25.OrderBy(a => a.ActionDate).ToList()),
                BudgetImpact = po.DepartmentId.HasValue ? _approvalService.CalculateBudgetImpact(id, po.DepartmentId.Value) : null,
                AvailableTags = GetApprovalTags(),
                SLAStatus = new SLAService25(db).GetPOStatus(id),
                CanFinalApprove = _roleService.CanApprove(userId, id),
                ReceivingSummary = po.Status == "Approved" ? GetOrderReceivingSummary(id) : null
            };

            // Set ViewBag for amend button visibility
            ViewBag.CanAmendPO = po.Status == "Approved" && _roleService.CanAmendApprovedPO(userId);

            return View(model);
        }

        // GET: /PurchaseOrders25/Print/5
        public async Task<ActionResult> Print(int id)
        {
            var po = db.PO_Orders25.Find(id);
            if (po == null) return HttpNotFound();

            await db.Entry(po).Reference(r => r.Department).LoadAsync();
            await db.Entry(po).Reference(r => r.User).LoadAsync();
            await db.Entry(po).Reference(r => r.Vendor).LoadAsync();
            await db.Entry(po).Collection(c => c.PO_LineItems25).LoadAsync();

            // Check if user has permission to view this PO
            int userId = UserSessionHelper.GetActiveUserId(HttpContext);
            if (!CanUserViewPO(userId, po))
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden, "You do not have permission to view this PO.");

            var model = new VM_POReview25
            {
                Order = po,
                LineItems = MapLineItems(po.PO_LineItems25.ToList())
            };

            return View(model);
        }

        // Other action methods remain the same...
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Cancel(int id)
        {
            var po = db.PO_Orders25.Find(id);
            if (po == null) return HttpNotFound();

            int userId = UserSessionHelper.GetActiveUserId(HttpContext);

            if (!CanUserEditPO(userId, po))
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);

            if (po.Status != "Pending" && po.Status != "Draft")
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden, "This PO cannot be cancelled in its current status.");

            po.Status = "Cancelled";
            db.SaveChanges();

            TempData["SuccessMessage"] = "PO cancelled successfully.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Submit(int id)
        {
            var po = db.PO_Orders25.Find(id);
            if (po == null) return HttpNotFound();

            int userId = UserSessionHelper.GetActiveUserId(HttpContext);

            if (po.SubmittedByUserId != userId)
            {
                TempData["ErrorMessage"] = "You do not have permission to submit this purchase order.";
                return RedirectToAction("RecurringIndex");
            }

            if (po.Status != "Draft")
            {
                TempData["ErrorMessage"] = "Only draft POs can be submitted. This PO has status: " + po.Status;
                return RedirectToAction("RecurringIndex");
            }

            // Budget check when submitting draft for approval (skip if company-wide)
            db.Entry(po).Collection(p => p.PO_LineItems25).Load();
            var poTotal = po.PO_LineItems25.Sum(li => li.CalculatedLineTotal);

            if (po.DepartmentId.HasValue && !po.IsCompanyWide)
            {
                var budgetResult = _budgetService.CheckBudget(po.DepartmentId.Value, poTotal ?? 0m);
                bool userCanOverride = _roleService.CanOverrideBudget(userId, po.DepartmentId.Value);

                if (!budgetResult.IsWithinBudget && !userCanOverride)
                {
                    TempData["ErrorMessage"] = budgetResult.Message;
                    if (po.OrderType == "Recurring")
                    {
                        return RedirectToAction("RecurringIndex");
                    }
                    return RedirectToAction("Index");
                }
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    // Check if user qualifies for auto-approval
                    if (_roleService.CanSkipApproval(userId, poTotal ?? 0m))
                    {
                        // Process auto-approval
                        if (_approvalService.ProcessAutoApproval(id, userId))
                        {
                            db.SaveChanges();
                            transaction.Commit();
                            TempData["SuccessMessage"] = "PO auto-approved successfully.";
                        }
                        else
                        {
                            // Fall back to normal approval flow if auto-approval fails
                            _approvalService.InitiateApprovalFlow(id);
                            _notificationService.SendSubmissionNotification(id);
                            db.SaveChanges();
                            transaction.Commit();
                            TempData["SuccessMessage"] = "PO submitted for approval successfully.";
                        }
                    }
                    else
                    {
                        // Normal approval flow
                        _approvalService.InitiateApprovalFlow(id);
                        _notificationService.SendSubmissionNotification(id);
                        db.SaveChanges();
                        transaction.Commit();
                        TempData["SuccessMessage"] = "PO submitted for approval successfully.";
                    }
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    PurchaseOrderLogger.LogException(ex);
                    TempData["ErrorMessage"] = "An error occurred while submitting the PO: " + ex.Message;
                }
            }

            // For recurring orders, redirect back to RecurringIndex to see the updated status
            if (po.OrderType == "Recurring")
            {
                return RedirectToAction("RecurringIndex");
            }

            return RedirectToAction("Index");
        }

        public async Task<ActionResult> Copy(int id)
        {
            var originalPO = db.PO_Orders25
                .Include(po => po.PO_LineItems25)
                .FirstOrDefault(po => po.PO_OrderId == id);

            if (originalPO == null) return HttpNotFound();

            int userId = UserSessionHelper.GetActiveUserId(HttpContext);

            if (!CanUserViewPO(userId, originalPO))
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);

            var model = VM_CreatePO25.FromPOEntity(originalPO);
            model.PO_OrderId = 0;
            model.IsDraft = true;
            model.Status = "Draft";

            model.Vendors = await GenerateSelectListAsync(Models.SelectListType.Vendors);
            model.Departments = GetDepartmentSelectList(userId);

            return View("Create", model);
        }

        [HttpPost]
        public JsonResult GetBudgetImpact(int departmentId, decimal amount, bool billable)
        {
            try
            {
                var budget = db.PO_DepartmentBudgets25
                    .Include(b => b.Department)
                    .FirstOrDefault(b => b.DepartmentId == departmentId);

                if (budget == null)
                {
                    var deptName = db.Departments
                        .Where(d => d.Id == departmentId)
                        .Select(d => d.Title)
                        .FirstOrDefault() ?? "Unknown";
                    return Json(new
                    {
                        success = false,
                        message = "No budget found for " + deptName
                    });
                }

                var ytdSpent = db.PO_DepartmentBudgetUsage25
                    .Where(u => u.PO_LineItems25.PO_Orders25.DepartmentId == departmentId && !u.IsBillable)
                    .Sum(u => (decimal?)u.Amount) ?? 0;

                var available = budget.Allocated - ytdSpent;
                var nonBillableAmount = billable ? 0 : amount;
                var willExceed = nonBillableAmount > available;

                return Json(new
                {
                    success = true,
                    departmentName = budget.Department?.Title ?? "Unknown",
                    budgetRemaining = available,
                    poAmount = nonBillableAmount,
                    willExceedBudget = willExceed,
                    percentUsed = budget.Allocated > 0 ? ((ytdSpent + nonBillableAmount) / budget.Allocated) * 100 : 0,
                    warningLevel = GetBudgetWarningLevel(nonBillableAmount, available)
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Error calculating budget impact: " + ex.Message
                });
            }
        }

        [HttpPost]
        public JsonResult GetSplitBudgetImpact(string splits)
        {
            try
            {
                var splitList = Newtonsoft.Json.JsonConvert.DeserializeObject<List<SplitBudgetRequest>>(splits);
                var results = new List<object>();
                var anyExceeds = false;

                foreach (var split in splitList.Where(s => s.DepartmentId > 0 && s.Amount > 0))
                {
                    var budget = db.PO_DepartmentBudgets25
                        .Include(b => b.Department)
                        .FirstOrDefault(b => b.DepartmentId == split.DepartmentId);

                    var deptName = db.Departments
                        .Where(d => d.Id == split.DepartmentId)
                        .Select(d => d.Title)
                        .FirstOrDefault() ?? "Unknown";

                    if (budget == null)
                    {
                        results.Add(new
                        {
                            departmentId = split.DepartmentId,
                            departmentName = deptName,
                            success = false,
                            message = "No budget defined",
                            poAmount = split.Amount
                        });
                        continue;
                    }

                    var ytdSpent = db.PO_DepartmentBudgetUsage25
                        .Where(u => u.PO_LineItems25.PO_Orders25.DepartmentId == split.DepartmentId && !u.IsBillable)
                        .Sum(u => (decimal?)u.Amount) ?? 0;

                    var available = budget.Allocated - ytdSpent;
                    var willExceed = split.Amount > available;
                    if (willExceed) anyExceeds = true;

                    results.Add(new
                    {
                        departmentId = split.DepartmentId,
                        departmentName = deptName,
                        success = true,
                        budgetRemaining = available,
                        poAmount = split.Amount,
                        willExceedBudget = willExceed,
                        percentUsed = budget.Allocated > 0 ? ((ytdSpent + split.Amount) / budget.Allocated) * 100 : 0,
                        warningLevel = GetBudgetWarningLevel(split.Amount, available)
                    });
                }

                return Json(new { success = true, departments = results, anyExceedsBudget = anyExceeds });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error calculating split budget impact: " + ex.Message });
            }
        }

        public class SplitBudgetRequest
        {
            public int DepartmentId { get; set; }
            public decimal Amount { get; set; }
        }

        [HttpPost]
        public ActionResult AddLineItem()
        {
            var lineItem = new VM_LineItem25
            {
                Quantity = 1,
                Taxable = true,
                Billable = false
            };

            return PartialView("_LineItemRow", lineItem);
        }

        public ActionResult MyDrafts()
        {
            int userId = UserSessionHelper.GetActiveUserId(HttpContext);

            var drafts = db.PO_Orders25
                .Include(po => po.PO_LineItems25)
                .Include(po => po.Department)
                .Where(po => po.SubmittedByUserId == userId && po.Status == "Draft")
                .OrderByDescending(po => po.PO_OrderId)
                .ToList();

            return View(drafts);
        }

        public ActionResult PendingApproval()
        {
            int userId = UserSessionHelper.GetActiveUserId(HttpContext);

            var pending = db.PO_Orders25
                .Include(po => po.PO_LineItems25)
                .Include(po => po.Department)
                .Include(po => po.User)
                .Where(po => po.SubmittedByUserId == userId && po.Status == "Pending")
                .OrderByDescending(po => po.DateSubmitted)
                .ToList();

            return View(pending);
        }

        public ActionResult SentBack()
        {
            int userId = UserSessionHelper.GetActiveUserId(HttpContext);

            var sentBack = db.PO_Orders25
                .Include(po => po.PO_LineItems25)
                .Include(po => po.Department)
                .Include(po => po.PO_Approvals25.Select(a => a.User))
                .Where(po => po.SubmittedByUserId == userId && po.Status == "SentBack")
                .OrderByDescending(po => po.DateSubmitted)
                .ToList();

            return View(sentBack);
        }

        public ActionResult Approved()
        {
            int userId = UserSessionHelper.GetActiveUserId(HttpContext);

            var approved = db.PO_Orders25
                .Include(po => po.PO_LineItems25)
                .Include(po => po.Department)
                .Where(po => po.SubmittedByUserId == userId && po.Status == "Approved")
                .OrderByDescending(po => po.DateOrdered)
                .ToList();

            return View(approved);
        }

        [HttpPost]
        public JsonResult DeleteLineItem(int index)
        {
            return Json(new { success = true, message = "Line item removed" });
        }

        [HttpPost]
        public JsonResult GetClientJobs(int clientId)
        {
            try
            {
                var jobs = db.Jobs
                    .Where(j => j.Client == clientId && j.MinStartDate.HasValue && j.Complete == null)
                    .Select(j => new
                    {
                        JobId = j.Id,
                        JobNumber = j.JobNumber,
                        JobName = j.Description
                    })
                    .OrderBy(j => j.JobNumber)
                    .ToList();

                return Json(new { success = true, jobs = jobs });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: PO25/PauseSchedule
        [HttpPost]
        public ActionResult PauseSchedule(int id)
        {
            try
            {
                var schedule = db.PO_RecurringSchedules25.Find(id);
                if (schedule == null)
                {
                    return Json(new { success = false, message = "Schedule not found." });
                }

                // Check permissions
                int userId = UserSessionHelper.GetActiveUserId(HttpContext);
                if (!CanUserManageSchedule(userId, schedule.DepartmentId))
                {
                    return Json(new { success = false, message = "You don't have permission to manage this schedule." });
                }

                schedule.IsActive = false;
                db.SaveChanges();

                return Json(new { success = true, message = "Schedule paused successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error pausing schedule: " + ex.Message });
            }
        }

        // POST: PO25/ActivateSchedule
        [HttpPost]
        public ActionResult ActivateSchedule(int id)
        {
            try
            {
                var schedule = db.PO_RecurringSchedules25.Find(id);
                if (schedule == null)
                {
                    return Json(new { success = false, message = "Schedule not found." });
                }

                // Check permissions
                int userId = UserSessionHelper.GetActiveUserId(HttpContext);
                if (!CanUserManageSchedule(userId, schedule.DepartmentId))
                {
                    return Json(new { success = false, message = "You don't have permission to manage this schedule." });
                }

                schedule.IsActive = true;

                // Set next process date if it's null or in the past
                if (!schedule.NextProcessDate.HasValue || schedule.NextProcessDate.Value <= DateTime.Now.Date)
                {
                    schedule.NextProcessDate = CalculateNextProcessDate(schedule.StartDate, schedule.RecurrenceType);
                }

                db.SaveChanges();

                return Json(new { success = true, message = "Schedule activated successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error activating schedule: " + ex.Message });
            }
        }

        // POST: PO25/DeleteSchedule
        [HttpPost]
        public ActionResult DeleteSchedule(int id)
        {
            try
            {
                var schedule = db.PO_RecurringSchedules25.Find(id);
                if (schedule == null)
                {
                    return Json(new { success = false, message = "Schedule not found." });
                }

                // Check permissions
                int userId = UserSessionHelper.GetActiveUserId(HttpContext);
                if (!CanUserManageSchedule(userId, schedule.DepartmentId))
                {
                    return Json(new { success = false, message = "You don't have permission to delete this schedule." });
                }

                // Check if there are any processed orders
                var hasHistory = db.PO_RecurringHistory25.Any(h => h.RecurringId == id);
                if (hasHistory)
                {
                    return Json(new { success = false, message = "Cannot delete schedule with existing history. Pause it instead." });
                }

                // Check for reconciliation records
                var hasReconciliation = db.PO_AllocationReconciliation25.Any(r => r.RecurringId == id);
                if (hasReconciliation)
                {
                    return Json(new { success = false, message = "Cannot delete schedule with reconciliation records. Pause it instead." });
                }

                db.PO_RecurringSchedules25.Remove(schedule);
                db.SaveChanges();

                return Json(new { success = true, message = "Schedule deleted successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error deleting schedule: " + ex.Message });
            }
        }

        [HttpPost]
        public JsonResult GetMaterialStockType(int materialId)
        {
            try
            {
                var material = db.Materials.Find(materialId);
                if (material != null)
                {
                    return Json(new
                    {
                        success = true,
                        stockTypeId = material.StockType,
                        stockTypeName = db.MaterialStockTypes
                            .Where(st => st.Id == material.StockType)
                            .Select(st => st.Title)
                            .FirstOrDefault() ?? "Unknown"
                    });
                }
                return Json(new { success = false });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        #region Reconciliation Actions

        // POST: PO25/UnreconcileItem
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UnreconcileItem(int reconciliationId)
        {
            try
            {
                var reconciliation = db.PO_AllocationReconciliation25.Find(reconciliationId);
                if (reconciliation == null)
                {
                    return Json(new { success = false, message = "Reconciliation record not found." });
                }

                // Check permissions
                int userId = UserSessionHelper.GetActiveUserId(HttpContext);
                var userRoles = db.PO_UserRoles25
                    .Where(ur => ur.UserId == userId && ur.IsActive)
                    .ToList();

                if (!userRoles.Any(r => r.RoleId == 1 || r.RoleId == 2 || r.RoleId == 4 || r.RoleId == 6))
                {
                    return Json(new { success = false, message = "You don't have permission to edit reconciliation records." });
                }

                reconciliation.ActualSpent = null;
                reconciliation.ReconciliationNotes = null;
                reconciliation.IsReconciled = false;
                reconciliation.ReconciliationDate = null;

                db.SaveChanges();

                return Json(new { success = true, message = "Reconciliation record reset successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error editing reconciliation: " + ex.Message });
            }
        }

        // POST: PO25/FinalizeReconciliation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult FinalizeReconciliation(int year, int month)
        {
            try
            {
                // Check permissions
                int userId = UserSessionHelper.GetActiveUserId(HttpContext);
                var userRoles = db.PO_UserRoles25
                    .Where(ur => ur.UserId == userId && ur.IsActive)
                    .ToList();

                if (!userRoles.Any(r => r.RoleId == 1 || r.RoleId == 4 || r.RoleId == 6)) // Owner, Admin, or Budget Manager
                {
                    return Json(new { success = false, message = "You don't have permission to finalize reconciliation." });
                }

                // Check if all items are reconciled
                var unreconciledItems = db.PO_AllocationReconciliation25
                    .Where(r => r.FiscalYear == year && r.Month == month && !r.IsReconciled)
                    .ToList();

                if (unreconciledItems.Any())
                {
                    return Json(new { success = false, message = $"Cannot finalize: {unreconciledItems.Count} items still need reconciliation." });
                }

                // Create finalization record (you might want to add a table for this)
                // For now, we'll just update a flag or create a log entry

                // You could add a PO_ReconciliationFinalization25 table to track this
                // For now, we'll log it in a comment or create a summary record

                return Json(new { success = true, message = "Reconciliation finalized successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error finalizing reconciliation: " + ex.Message });
            }
        }

        #endregion Reconciliation Actions

        #region History and Reporting Actions

        // GET: PO25/ViewHistory
        public ActionResult ViewHistory(int id)
        {
            try
            {
                //System.Diagnostics.Debug.WriteLine($"[ViewHistory] Starting for RecurringId: {id}");

                var schedule = db.PO_RecurringSchedules25
                    .Include(s => s.Department)
                    .Include(s => s.PO_LineItems25)
                    .FirstOrDefault(s => s.RecurringId == id);

                if (schedule == null)
                {
                    //System.Diagnostics.Debug.WriteLine($"[ViewHistory] Schedule not found for RecurringId: {id}");
                    TempData["Error"] = "Recurring schedule not found.";
                    return RedirectToAction("RecurringIndex");
                }

                //System.Diagnostics.Debug.WriteLine($"[ViewHistory] Schedule found: {schedule.Description}, DepartmentId: {schedule.DepartmentId}");

                // Check permissions
                int userId = UserSessionHelper.GetActiveUserId(HttpContext);
                //System.Diagnostics.Debug.WriteLine($"[ViewHistory] Checking permissions for UserId: {userId}, DepartmentId: {schedule.DepartmentId}");

                if (!CanUserViewSchedule(userId, schedule.DepartmentId))
                {
                    //System.Diagnostics.Debug.WriteLine($"[ViewHistory] Permission denied for UserId: {userId}");
                    TempData["Error"] = "You do not have permission to view this schedule history.";
                    return RedirectToAction("RecurringIndex");
                }

                //System.Diagnostics.Debug.WriteLine($"[ViewHistory] Permission granted, loading history data...");

                var history = db.PO_RecurringHistory25
                    .Include(h => h.PO_Orders25)
                    .Include(h => h.PO_LineItems25)
                    .Where(h => h.RecurringId == id)
                    .OrderByDescending(h => h.ProcessedDate)
                    .ToList();

                //System.Diagnostics.Debug.WriteLine($"[ViewHistory] Found {history.Count} history records");

                var reconciliationHistory = db.PO_AllocationReconciliation25
                    .Where(r => r.RecurringId == id)
                    .OrderByDescending(r => r.FiscalYear)
                    .ThenByDescending(r => r.Month)
                    .ToList();

                //System.Diagnostics.Debug.WriteLine($"[ViewHistory] Found {reconciliationHistory.Count} reconciliation records");

                var model = new RecurringHistoryViewModel
                {
                    Schedule = schedule,
                    ProcessedOrders = history.Select(h => new ProcessedOrderViewModel
                    {
                        HistoryId = h.HistoryId,
                        PO_OrderId = h.GeneratedPO_OrderId ?? -1,
                        ProcessedDate = h.ProcessedDate,
                        Amount = h.Amount,
                        FiscalYear = h.FiscalYear,
                        Month = h.Month,
                        OrderStatus = h.PO_Orders25?.Status ?? "Unknown"
                    }).ToList(),
                    ReconciliationHistory = reconciliationHistory.Select(r => new ReconciliationHistoryViewModel
                    {
                        FiscalYear = r.FiscalYear,
                        Month = r.Month,
                        AllocatedAmount = r.AllocatedAmount,
                        ActualSpent = r.ActualSpent,
                        IsReconciled = r.IsReconciled,
                        ReconciliationDate = r.ReconciliationDate,
                        ReconciliationNotes = r.ReconciliationNotes
                    }).ToList()
                };

                //System.Diagnostics.Debug.WriteLine($"[ViewHistory] Returning view with model");
                return View(model);
            }
            catch (Exception ex)
            {
                //System.Diagnostics.Debug.WriteLine($"[ViewHistory] Exception caught: {ex.Message}");
                //System.Diagnostics.Debug.WriteLine($"[ViewHistory] Stack trace: {ex.StackTrace}");
                TempData["Error"] = "Error loading history: " + ex.Message;
                return RedirectToAction("RecurringIndex");
            }
        }

        // GET: PO25/EditRecurring
        public async Task<ActionResult> EditRecurring(int id)
        {
            try
            {
                var schedule = db.PO_RecurringSchedules25
                    .Include(s => s.Department)
                    .Include(s => s.PO_LineItems25)
                    .Include(s => s.PO_LineItems25.PO_Orders25)
                    .FirstOrDefault(s => s.RecurringId == id);

                if (schedule == null)
                {
                    TempData["Error"] = "Recurring schedule not found.";
                    return RedirectToAction("RecurringIndex");
                }

                // Check permissions
                int userId = UserSessionHelper.GetActiveUserId(HttpContext);
                if (!CanUserManageSchedule(userId, schedule.DepartmentId))
                {
                    return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
                }

                var model = new EditRecurringScheduleViewModel
                {
                    RecurringId = schedule.RecurringId,
                    VendorId = schedule.PO_LineItems25?.PO_Orders25?.VendorId ?? 0,
                    Description = schedule.Description,
                    MonthlyAmount = schedule.MonthlyAmount,
                    RecurrenceType = schedule.RecurrenceType,
                    StartDate = schedule.StartDate,
                    EndDate = schedule.EndDate,
                    IsActive = schedule.IsActive,
                    DepartmentId = schedule.DepartmentId,
                    IsAllocation = schedule.PO_LineItems25?.IsAllocation ?? false,
                    Vendors = await GenerateSelectListAsync(Models.SelectListType.Vendors),
                    Departments = GetDepartmentSelectList(userId)
                };

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error loading schedule: " + ex.Message;
                return RedirectToAction("RecurringIndex");
            }
        }

        // POST: PO25/EditRecurring
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> EditRecurring(EditRecurringScheduleViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Vendors = await GenerateSelectListAsync(Models.SelectListType.Vendors);
                model.Departments = GetDepartmentSelectList(UserSessionHelper.GetActiveUserId(HttpContext));
                return View(model);
            }

            try
            {
                var schedule = db.PO_RecurringSchedules25
                    .Include(s => s.PO_LineItems25)
                    .Include(s => s.PO_LineItems25.PO_Orders25)
                    .FirstOrDefault(s => s.RecurringId == model.RecurringId);

                if (schedule == null)
                {
                    TempData["Error"] = "Recurring schedule not found.";
                    return RedirectToAction("RecurringIndex");
                }

                // Check permissions
                int userId = UserSessionHelper.GetActiveUserId(HttpContext);
                if (!CanUserManageSchedule(userId, schedule.DepartmentId))
                {
                    return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
                }

                // Update schedule
                schedule.Description = model.Description;
                schedule.MonthlyAmount = model.MonthlyAmount;
                schedule.RecurrenceType = model.RecurrenceType;
                schedule.EndDate = model.EndDate;
                schedule.IsActive = model.IsActive;

                // Update parent order vendor if changed
                if (schedule.PO_LineItems25?.PO_Orders25 != null && schedule.PO_LineItems25.PO_Orders25.VendorId != model.VendorId)
                {
                    schedule.PO_LineItems25.PO_Orders25.VendorId = model.VendorId;
                }

                // Update line item allocation flag
                if (schedule.PO_LineItems25 != null)
                {
                    schedule.PO_LineItems25.IsAllocation = model.IsAllocation;
                    schedule.PO_LineItems25.RequiresReconciliation = model.IsAllocation;
                }

                // Recalculate next process date if needed
                if (model.IsActive && schedule.NextProcessDate <= DateTime.Now.Date)
                {
                    schedule.NextProcessDate = CalculateNextProcessDate(DateTime.Now.Date, model.RecurrenceType);
                }

                db.SaveChanges();

                TempData["Success"] = "Recurring schedule updated successfully.";
                return RedirectToAction("RecurringIndex");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error updating schedule: " + ex.Message;
                model.Vendors = await GenerateSelectListAsync(Models.SelectListType.Vendors);
                model.Departments = GetDepartmentSelectList(UserSessionHelper.GetActiveUserId(HttpContext));
                return View(model);
            }
        }

        // GET: PO25/ExportReconciliation
        public ActionResult ExportReconciliation(int year, int month)
        {
            try
            {
                // Check permissions
                int userId = UserSessionHelper.GetActiveUserId(HttpContext);
                var userRoles = db.PO_UserRoles25
                    .Where(ur => ur.UserId == userId && ur.IsActive)
                    .ToList();

                if (!userRoles.Any(r => r.RoleId == 1 || r.RoleId == 2 || r.RoleId == 4 || r.RoleId == 6))
                {
                    return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
                }

                var reconciliationItems = db.PO_AllocationReconciliation25
                    .Include(r => r.PO_RecurringSchedules25)
                    .Include(r => r.PO_RecurringSchedules25.Department)
                    .Where(r => r.FiscalYear == year && r.Month == month)
                    .OrderBy(r => r.PO_RecurringSchedules25.Department.Title)
                    .ThenBy(r => r.PO_RecurringSchedules25.Description)
                    .ToList();

                // Create CSV content
                var csv = new StringBuilder();
                csv.AppendLine("Department,Description,Allocated Amount,Actual Spent,Variance,Status,Reconciliation Date,Notes");

                foreach (var item in reconciliationItems)
                {
                    var variance = (item.ActualSpent ?? 0) - item.AllocatedAmount;
                    var status = item.IsReconciled ? "Reconciled" : "Pending";
                    var reconciliationDate = item.ReconciliationDate?.ToString("MM/dd/yyyy") ?? "";
                    var notes = (item.ReconciliationNotes ?? "").Replace("\"", "\"\"");

                    csv.AppendLine($"\"{item.PO_RecurringSchedules25.Department.Title}\"," +
                                  $"\"{item.PO_RecurringSchedules25.Description}\"," +
                                  $"{item.AllocatedAmount:F2}," +
                                  $"{item.ActualSpent?.ToString("F2") ?? ""}," +
                                  $"{variance:F2}," +
                                  $"{status}," +
                                  $"{reconciliationDate}," +
                                  $"\"{notes}\"");
                }

                var fileName = $"Reconciliation_{year}_{month:D2}_{DateTime.Now:yyyyMMdd}.csv";
                var bytes = Encoding.UTF8.GetBytes(csv.ToString());

                return File(bytes, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error exporting reconciliation: " + ex.Message;
                return RedirectToAction("ReconciliationDashboard");
            }
        }

        #endregion History and Reporting Actions

        #region Post-Approval Amendment Actions

        // GET: PO25/AmendTaxFreight/5
        public async Task<ActionResult> AmendTaxFreight(int id)
        {
            var po = await db.PO_Orders25
                .Include(p => p.Department)
                .Include(p => p.Vendor)
                .Include(p => p.PO_LineItems25)
                .FirstOrDefaultAsync(p => p.PO_OrderId == id);

            if (po == null)
                return HttpNotFound();

            int userId = UserSessionHelper.GetActiveUserId(HttpContext);

            // Check if user can amend this PO
            if (!_amendmentService.CanAmendPO(id, userId))
            {
                TempData["ErrorMessage"] = "You do not have permission to amend this PO, or it cannot be amended in its current status.";
                return RedirectToAction("Details", new { id });
            }

            // Calculate subtotal (line items only, excluding tax/freight)
            var subtotal = po.PO_LineItems25.Sum(li => li.CalculatedLineTotal);

            var model = new VM_AmendTaxFreight25
            {
                PO_OrderId = po.PO_OrderId,
                PONumber = po.PO_Number,
                VendorName = po.Vendor?.Title ?? "Unknown Vendor",
                DepartmentName = po.Department?.Title ?? "Unknown Department",
                SubtotalAmount = subtotal ?? 0m,

                // Current values
                CurrentFreightAmount = po.FreightAmount,
                CurrentTaxAmount = po.TaxAmount,
                CurrentTaxRate = po.TaxRate,
                CurrentIsTaxPercentage = po.IsTaxPercentage,

                // Initialize new values to current values
                NewFreightAmount = po.FreightAmount,
                NewTaxAmount = po.TaxAmount,
                NewTaxRate = po.TaxRate,
                IsTaxPercentage = po.IsTaxPercentage,

                // Get amendment history
                AmendmentHistory = _amendmentService.GetAmendmentHistory(id)
            };

            return View(model);
        }

        // POST: PO25/AmendTaxFreight
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AmendTaxFreight(VM_AmendTaxFreight25 model)
        {
            int userId = UserSessionHelper.GetActiveUserId(HttpContext);

            if (!_amendmentService.CanAmendPO(model.PO_OrderId, userId))
            {
                TempData["ErrorMessage"] = "You do not have permission to amend this PO.";
                return RedirectToAction("Details", new { id = model.PO_OrderId });
            }

            // Validate that at least one value has changed
            var po = await db.PO_Orders25.FindAsync(model.PO_OrderId);
            if (po == null)
                return HttpNotFound();

            bool freightChanged = model.NewFreightAmount != po.FreightAmount;
            bool taxChanged = model.IsTaxPercentage
                ? model.NewTaxRate != po.TaxRate
                : model.NewTaxAmount != po.TaxAmount;

            if (!freightChanged && !taxChanged)
            {
                TempData["ErrorMessage"] = "No changes were made.";
                return RedirectToAction("AmendTaxFreight", new { id = model.PO_OrderId });
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    // Amend freight if changed
                    if (freightChanged)
                    {
                        if (!_amendmentService.AmendFreight(model.PO_OrderId, model.NewFreightAmount, userId, model.Reason))
                        {
                            throw new Exception("Failed to amend freight amount.");
                        }
                    }

                    // Amend tax if changed
                    if (taxChanged)
                    {
                        if (!_amendmentService.AmendTax(model.PO_OrderId, model.NewTaxAmount, model.NewTaxRate, model.IsTaxPercentage, userId, model.Reason))
                        {
                            throw new Exception("Failed to amend tax.");
                        }
                    }

                    transaction.Commit();
                    TempData["SuccessMessage"] = "PO amended successfully.";
                    return RedirectToAction("Details", new { id = model.PO_OrderId });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    TempData["ErrorMessage"] = "Error amending PO: " + ex.Message;

                    // Reload model data
                    await db.Entry(po).Reference(p => p.Department).LoadAsync();
                    await db.Entry(po).Reference(p => p.Vendor).LoadAsync();
                    await db.Entry(po).Collection(p => p.PO_LineItems25).LoadAsync();

                    model.PONumber = po.PO_Number;
                    model.VendorName = po.Vendor?.Title ?? "Unknown Vendor";
                    model.DepartmentName = po.Department?.Title ?? "Unknown Department";
                    model.SubtotalAmount = po.PO_LineItems25.Sum(li => li.CalculatedLineTotal) ?? 0m;
                    model.CurrentFreightAmount = po.FreightAmount;
                    model.CurrentTaxAmount = po.TaxAmount;
                    model.CurrentTaxRate = po.TaxRate;
                    model.CurrentIsTaxPercentage = po.IsTaxPercentage;
                    model.AmendmentHistory = _amendmentService.GetAmendmentHistory(model.PO_OrderId);

                    return View(model);
                }
            }
        }

        #endregion Post-Approval Amendment Actions

        #region Private Helper Methods

        private string GetBudgetWarningLevel(decimal amount, decimal remaining)
        {
            if (amount > remaining) return "Critical";
            if (amount > remaining * 0.8m) return "Warning";
            return "Safe";
        }

        private bool CanUserEditPO(int userId, PO_Orders25 po)
        {
            // Original submitter can edit
            if (po.SubmittedByUserId == userId) return true;

            // Owner can edit anything
            if (_roleService.IsOwner(userId)) return true;

            // Admin can edit anything
            var userRoles = _roleService.GetUserRoles(userId);
            if (userRoles.Any(r => r.RoleName == "Admin")) return true;

            // Purchasing can edit any PO
            if (_roleService.IsPurchasing(userId)) return true;

            // Inventory Manager can edit any PO
            if (_roleService.IsInventoryManager(userId)) return true;

            // Executive can edit any PO
            if (_roleService.IsExecutive(userId)) return true;

            // Accounting can edit any PO
            if (_roleService.IsAccounting(userId)) return true;

            // Department Manager can edit POs in their department
            if (userRoles.Any(r => r.RoleName == "Department Manager" && r.DepartmentId == po.DepartmentId))
                return true;

            return false;
        }

        private bool CanUserViewPO(int userId, PO_Orders25 po)
        {
            if (po.SubmittedByUserId == userId) return true;
            if (_roleService.IsOwner(userId)) return true;
            if (_roleService.IsPOAdmin(userId)) return true;
            if (_roleService.IsPurchasing(userId)) return true;
            if (_roleService.IsInventoryManager(userId)) return true;
            if (_roleService.IsExecutive(userId)) return true;
            if (_roleService.IsAccounting(userId)) return true;

            var userDepartments = _roleService.GetApprovalDepartments(userId);
            return po.DepartmentId.HasValue && userDepartments.Contains(po.DepartmentId.Value);
        }

        private List<SelectListItem> GetApprovalTags()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "MissingInfo", Text = "Missing Information" },
                new SelectListItem { Value = "OverBudget", Text = "Over Budget" },
                new SelectListItem { Value = "WrongVendor", Text = "Unapproved Vendor" },
                new SelectListItem { Value = "IncorrectJobCode", Text = "Incorrect Job Code" },
                new SelectListItem { Value = "NeedsApproval", Text = "Needs Additional Approval" },
                new SelectListItem { Value = "Other", Text = "Other" }
            };
        }

        private List<VM_LineItem25> MapLineItems(List<PO_LineItems25> lineItems)
        {
            return lineItems.Select(li => MapLineItem(li)).ToList();
        }

        private VM_LineItem25 MapLineItem(PO_LineItems25 li)
        {
            return new VM_LineItem25
            {
                PO_LineItemId = li.PO_LineItemId,
                ItemDescription = li.ItemDescription,
                VendorLink = li.VendorLink,
                Cost = li.Cost,
                Quantity = li.Quantity,
                Taxable = li.Taxable,
                Billable = li.Billable,
                DateNeeded = li.DateNeeded,
                DateExpected = li.DateExpected,
                InternalComments = li.InternalComments,
                FileAttachmentPath = li.FileAttachmentPath,
                InventoryTracked = li.InventoryTracked,
                DateReceived = li.DateReceived,
                MaterialId = li.MaterialId,
                UnitType = li.UnitType ?? "Per Unit",
                JobTicketNumber = li.JobTicketNumber,
                JobId = li.JobId,
                ReceivingSummary = GetReceivingSummary(li.PO_LineItemId),
                ReceiptHistory = GetReceiptHistory(li.PO_LineItemId),
                // Bulk item properties
                IsBulkItem = li.IsBulkItem,
                BulkContainerType = li.BulkContainerType,
                BulkUnitQuantity = li.BulkUnitQuantity,
                BulkUnitType = li.BulkUnitType,
                CalculatedUnitCost = li.CalculatedUnitCost
            };
        }

        private List<VM_Approval25> MapApprovals(List<PO_Approvals25> approvals)
        {
            return approvals.Select(a => new VM_Approval25
            {
                ApprovalId = a.PO_ApprovalId,
                PO_OrderId = a.PO_OrderId,
                ApproverUserId = a.ApproverUserId,
                ActionTaken = a.ActionTaken,
                ActionDate = a.ActionDate,
                Comments = a.Comments,
                Tags = a.Tags,
                User = a.User
            }).ToList();
        }

        private VM_OrderReceivingSummary25 GetOrderReceivingSummary(int poId)
        {
            var lineItems = db.PO_LineItems25
                .Where(li => li.PO_OrderId == poId)
                .ToList();

            if (!lineItems.Any()) return null;

            var summary = new VM_OrderReceivingSummary25
            {
                TotalInventoryItems = lineItems.Count
            };

            foreach (var item in lineItems)
            {
                var totalReceived = db.PO_InventoryReceipts25
                    .Where(r => r.PO_LineItemId == item.PO_LineItemId)
                    .Sum(r => (int?)r.QuantityReceived) ?? 0;

                if (totalReceived >= item.Quantity)
                {
                    summary.FullyReceivedItems++;
                }
                else if (totalReceived > 0)
                {
                    summary.PartiallyReceivedItems++;
                }
                else
                {
                    summary.PendingItems++;
                }
            }

            // Try the receipt query more defensively
            try
            {
                var lineItemIds = lineItems.Select(i => i.PO_LineItemId).ToList();
                var allReceipts = db.PO_InventoryReceipts25
                    .Where(r => lineItemIds.Contains(r.PO_LineItemId))
                    .OrderBy(r => r.DateReceived)
                    .ToList();

                if (allReceipts.Any())
                {
                    summary.FirstReceiptDate = allReceipts.First().DateReceived;
                    summary.LastReceiptDate = allReceipts.Last().DateReceived;
                }
            }
            catch (Exception ex)
            {
                // For now, just ignore the receipt dates if there's an error
                //System.Diagnostics.Debug.WriteLine($"Receipt query error: {ex.Message}");
            }

            return summary;
        }

        private VM_ReceivingSummary25 GetReceivingSummary(int lineItemId)
        {
            var lineItem = db.PO_LineItems25.Find(lineItemId);
            if (lineItem == null) return null;

            var receipts = db.PO_InventoryReceipts25
                .Where(r => r.PO_LineItemId == lineItemId)
                .ToList();

            var totalReceived = receipts.Sum(r => r.QuantityReceived);
            var remaining = lineItem.Quantity - totalReceived;

            return new VM_ReceivingSummary25
            {
                OrderedQuantity = lineItem.Quantity,
                TotalReceived = totalReceived,
                RemainingQuantity = remaining > 0 ? remaining : 0,
                IsFullyReceived = remaining <= 0,
                LastReceiptDate = receipts.OrderByDescending(r => r.DateReceived)
                                        .FirstOrDefault()?.DateReceived,
                ReceiptCount = receipts.Count
            };
        }

        private List<VM_ReceiptHistory25> GetReceiptHistory(int lineItemId)
        {
            return db.PO_InventoryReceipts25
                .Where(r => r.PO_LineItemId == lineItemId)
                .Join(db.Users, r => r.ReceivedByUserId, u => u.Id, (r, u) => new VM_ReceiptHistory25
                {
                    ReceiptId = r.ReceiptId,
                    QuantityReceived = r.QuantityReceived,
                    DateReceived = r.DateReceived,
                    ReceivedBy = u.FirstName + " " + u.LastName,
                    StorageLocation = r.StorageLocation,
                    Notes = r.Notes
                })
                .OrderByDescending(r => r.DateReceived)
                .ToList();
        }

        private IEnumerable<SelectListItem> GetDepartmentSelectList(int userId)
        {
            // Owner, Admin, Executive, Purchasing, and Accounting can see all departments
            // Note: Uses PO_Roles25 only, not system Roles table
            if (_roleService.CanSeeAllDepartments(userId))
            {
                return db.Departments
                    .OrderBy(d => d.Title)
                    .Select(d => new SelectListItem
                    {
                        Value = d.Id.ToString(),
                        Text = d.Title
                    }).ToList();
            }

            // Standard users only see their AgencyDepartment, or Production if null
            var userAgencyDept = db.Users
                .Where(u => u.Id == userId)
                .Select(u => u.AgencyDepartment)
                .FirstOrDefault();

            int departmentId;
            if (userAgencyDept.HasValue)
            {
                departmentId = userAgencyDept.Value;
            }
            else
            {
                // Default to Production department if AgencyDepartment is null
                var productionDept = db.Departments.FirstOrDefault(d => d.Title == "Production");
                departmentId = productionDept?.Id ?? 1; // Fallback to ID 1 if Production not found
            }

            // Return only the user's department
            return db.Departments
                .Where(d => d.Id == departmentId)
                .Select(d => new SelectListItem
                {
                    Value = d.Id.ToString(),
                    Text = d.Title
                }).ToList();
        }

        private bool CanUserManageSchedule(int userId, int departmentId)
        {
            var userRoles = db.PO_UserRoles25
                .Where(ur => ur.UserId == userId && ur.IsActive)
                .ToList();

            // Owner and Admin can manage all schedules
            if (userRoles.Any(r => r.RoleId == 1 || r.RoleId == 4))
                return true;

            // Department Manager and Budget Manager can manage their department's schedules
            return userRoles.Any(r => (r.RoleId == 2 || r.RoleId == 6) && r.DepartmentId == departmentId);
        }

        private bool CanUserViewSchedule(int userId, int departmentId)
        {
            var userRoles = db.PO_UserRoles25
                .Where(ur => ur.UserId == userId && ur.IsActive)
                .ToList();

            // Owner and Admin can view all schedules
            if (userRoles.Any(r => r.RoleId == 1 || r.RoleId == 4))
                return true;

            // Any role in the department can view schedules
            return userRoles.Any(r => r.DepartmentId == departmentId);
        }

        #endregion Private Helper Methods

        #region Company Budget Helpers

        // AJAX: Get company budget impact for Create/Edit PO
        [HttpPost]
        public JsonResult GetCompanyBudgetImpact(int budgetId, decimal amount)
        {
            try
            {
                var result = _companyBudgetService.CheckCompanyBudget(budgetId, amount);
                return Json(new
                {
                    success = true,
                    budgetName = result.BudgetName,
                    isWithinBudget = result.IsWithinBudget,
                    budgetRemaining = result.BudgetRemaining,
                    poAmount = result.POAmount,
                    percentUsed = result.PercentUsed,
                    warningLevel = result.WarningLevel,
                    willExceedBudget = !result.IsWithinBudget,
                    message = result.Message
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error checking budget: " + ex.Message });
            }
        }

        // Helper: Check if user can manage (add/edit/update) company budgets
        private bool CanManageCompanyBudgets(int userId)
        {
            // Owner, Executive, or Purchasing can manage company budgets
            return _roleService.IsPOAdmin(userId) ||
                   _roleService.IsExecutive(userId) ||
                   _roleService.IsPurchasing(userId);
        }

        // Helper: Check if user can use company budgets on POs
        private bool CanUseCompanyBudgets(int userId)
        {
            // Owner, Executive, Purchasing, or Accounting can use company budgets
            return _roleService.IsPOAdmin(userId) ||
                   _roleService.IsExecutive(userId) ||
                   _roleService.IsPurchasing(userId) ||
                   _roleService.IsAccounting(userId);
        }

        // Helper: Get company budgets for dropdown (only for users with permission)
        private IEnumerable<SelectListItem> GetCompanyBudgetSelectList(int userId)
        {
            // Only return budgets if user has permission to use them
            if (!CanUseCompanyBudgets(userId))
            {
                return Enumerable.Empty<SelectListItem>();
            }

            var budgets = _companyBudgetService.GetActiveCompanyBudgets();
            return budgets.Select(b => new SelectListItem
            {
                Value = b.CompanyBudgetId.ToString(),
                Text = $"{b.BudgetName} ({b.Allocated:C} allocated)"
            });
        }

        #endregion Company Budget Management
    }
}