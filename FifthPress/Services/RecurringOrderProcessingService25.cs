using FifthPress.Interfaces;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace FifthPress.Services
{
    /// <summary>
    /// Service for automatically processing recurring purchase orders
    /// Runs on a timer and generates POs based on NextProcessDate
    /// </summary>
    public class RecurringOrderProcessingService25 : IRecurringOrderProcessor25, IDisposable
    {
        private Timer _processingTimer;
        private bool _isProcessing = false;
        private readonly object _lock = new object();

        // Configuration
        private const int TIMER_INTERVAL_MINUTES = 10; // Run every 10 minutes

        private const int START_HOUR = 6;  // 6 AM
        private const int END_HOUR = 22;   // 10 PM

        public RecurringOrderProcessingService25()
        {
            PurchaseOrderLogger.Log("[RecurringService] Service initialized");
        }

        /// <summary>
        /// Start the background timer for automatic processing
        /// </summary>
        public void StartProcessing()
        {
            if (_processingTimer != null)
            {
                PurchaseOrderLogger.LogWarning("[RecurringService] Timer already running");
                return;
            }

            _processingTimer = new Timer(TIMER_INTERVAL_MINUTES * 60 * 1000); // Convert to milliseconds
            _processingTimer.Elapsed += async (sender, e) => await OnTimerElapsed();
            _processingTimer.AutoReset = true;
            _processingTimer.Start();

            PurchaseOrderLogger.Log($"[RecurringService] Started - Will run every {TIMER_INTERVAL_MINUTES} minutes between {START_HOUR}:00 and {END_HOUR}:00");
        }

        /// <summary>
        /// Stop the background timer
        /// </summary>
        public void StopProcessing()
        {
            if (_processingTimer != null)
            {
                _processingTimer.Stop();
                _processingTimer.Dispose();
                _processingTimer = null;
                PurchaseOrderLogger.Log("[RecurringService] Stopped");
            }
        }

        /// <summary>
        /// Timer elapsed event handler
        /// </summary>
        private async Task OnTimerElapsed()
        {
            // Check if we're within operating hours
            var currentHour = DateTime.Now.Hour;
            if (currentHour < START_HOUR || currentHour >= END_HOUR)
            {
                PurchaseOrderLogger.Log($"[RecurringService] Outside operating hours ({currentHour}:00), skipping");
                return;
            }

            // Prevent concurrent execution
            lock (_lock)
            {
                if (_isProcessing)
                {
                    PurchaseOrderLogger.LogWarning("[RecurringService] Previous processing still running, skipping this cycle");
                    return;
                }
                _isProcessing = true;
            }

            try
            {
                await ProcessDueRecurringOrdersAsync();
            }
            finally
            {
                lock (_lock)
                {
                    _isProcessing = false;
                }
            }
        }

        /// <summary>
        /// Process all recurring schedules that are due (NextProcessDate <= tomorrow)
        /// </summary>
        public async Task ProcessDueRecurringOrdersAsync()
        {
            PurchaseOrderLogger.Log("[RecurringService] ========== Starting Processing Cycle ==========");

            var tomorrow = DateTime.Now.Date.AddDays(1);
            int processedCount = 0;
            int errorCount = 0;

            using (var db = new FifthPressEntities())
            {
                try
                {
                    // Find all schedules that are due for processing
                    var dueSchedules = await db.PO_RecurringSchedules25
                        .Include(r => r.PO_LineItems25)
                        .Include(r => r.PO_LineItems25.PO_Orders25)
                        .Include(r => r.PO_LineItems25.PO_Orders25.PO_Approvals25)
                        .Include(r => r.Department)
                        .Where(r => r.IsActive &&
                               r.NextProcessDate.HasValue &&
                               r.NextProcessDate.Value <= tomorrow)
                        .ToListAsync();

                    PurchaseOrderLogger.Log($"[RecurringService] Found {dueSchedules.Count} schedules due for processing");

                    foreach (var schedule in dueSchedules)
                    {
                        try
                        {
                            // Validate parent order is approved
                            var parentOrder = schedule.PO_LineItems25?.PO_Orders25;
                            if (parentOrder == null)
                            {
                                PurchaseOrderLogger.LogError($"[RecurringService] Schedule {schedule.RecurringId}: No parent order found");
                                errorCount++;
                                continue;
                            }

                            if (parentOrder.Status != "Approved")
                            {
                                PurchaseOrderLogger.LogWarning($"[RecurringService] Schedule {schedule.RecurringId}: Parent order {parentOrder.PO_OrderId} not approved (Status: {parentOrder.Status}), skipping");
                                continue;
                            }

                            // Check if already past end date
                            if (schedule.EndDate.HasValue && DateTime.Now.Date > schedule.EndDate.Value)
                            {
                                PurchaseOrderLogger.Log($"[RecurringService] Schedule {schedule.RecurringId}: Past end date, marking inactive");
                                schedule.IsActive = false;
                                await db.SaveChangesAsync();
                                continue;
                            }

                            await ProcessSingleScheduleAsync(db, schedule);
                            processedCount++;
                        }
                        catch (Exception ex)
                        {
                            PurchaseOrderLogger.LogError($"[RecurringService] Error processing schedule {schedule.RecurringId}: {ex.Message}");
                            PurchaseOrderLogger.LogException(ex);
                            errorCount++;
                        }
                    }

                    PurchaseOrderLogger.Log($"[RecurringService] ========== Processing Complete: {processedCount} successful, {errorCount} errors ==========");
                }
                catch (Exception ex)
                {
                    PurchaseOrderLogger.LogError($"[RecurringService] Fatal error in processing cycle: {ex.Message}");
                    PurchaseOrderLogger.LogException(ex);
                }
            }
        }

        /// <summary>
        /// Process a specific recurring schedule by ID
        /// </summary>
        public async Task ProcessRecurringScheduleAsync(int recurringId)
        {
            PurchaseOrderLogger.Log($"[RecurringService] Manual processing requested for schedule {recurringId}");

            using (var db = new FifthPressEntities())
            {
                var schedule = await db.PO_RecurringSchedules25
                    .Include(r => r.PO_LineItems25)
                    .Include(r => r.PO_LineItems25.PO_Orders25)
                    .Include(r => r.PO_LineItems25.PO_Orders25.PO_Approvals25)
                    .Include(r => r.Department)
                    .FirstOrDefaultAsync(r => r.RecurringId == recurringId);

                if (schedule == null)
                {
                    PurchaseOrderLogger.LogError($"[RecurringService] Schedule {recurringId} not found");
                    throw new Exception($"Recurring schedule {recurringId} not found");
                }

                if (!schedule.IsActive)
                {
                    PurchaseOrderLogger.LogWarning($"[RecurringService] Schedule {recurringId} is not active");
                    throw new Exception($"Recurring schedule {recurringId} is not active");
                }

                await ProcessSingleScheduleAsync(db, schedule);
                PurchaseOrderLogger.Log($"[RecurringService] Manual processing complete for schedule {recurringId}");
            }
        }

        /// <summary>
        /// Process a single recurring schedule and generate a new PO
        /// </summary>
        private async Task ProcessSingleScheduleAsync(FifthPressEntities db, PO_RecurringSchedules25 schedule)
        {
            PurchaseOrderLogger.Log($"[RecurringService] Processing schedule {schedule.RecurringId}: {schedule.Description}");

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    // Null check for parent line item
                    if (schedule.PO_LineItems25 == null)
                    {
                        PurchaseOrderLogger.LogError($"[RecurringService] Schedule {schedule.RecurringId}: PO_LineItems25 is null. Check ParentLineItemId foreign key.");
                        throw new Exception($"Schedule {schedule.RecurringId} has no parent line item. Cannot process.");
                    }

                    var parentLineItem = schedule.PO_LineItems25;

                    // Null check for parent order
                    if (parentLineItem.PO_Orders25 == null)
                    {
                        PurchaseOrderLogger.LogError($"[RecurringService] Schedule {schedule.RecurringId}: Parent line item {parentLineItem.PO_LineItemId} has no associated order.");
                        throw new Exception($"Schedule {schedule.RecurringId} parent line item has no order. Cannot process.");
                    }

                    var parentOrder = parentLineItem.PO_Orders25;

                    // Validate required fields before creating order
                    if (parentOrder.VendorId == null)
                    {
                        PurchaseOrderLogger.LogError($"[RecurringService] Schedule {schedule.RecurringId}: Parent order has null VendorId");
                        throw new Exception($"Schedule {schedule.RecurringId} parent order has no VendorId. Cannot process.");
                    }

                    if (parentOrder.SubmittedByUserId == null)
                    {
                        PurchaseOrderLogger.LogWarning($"[RecurringService] Schedule {schedule.RecurringId}: Parent order has null SubmittedByUserId, using default system user (1)");
                    }

                    // Create new PO Order (auto-approved because parent was approved)
                    var newOrder = new PO_Orders25
                    {
                        VendorId = parentOrder.VendorId,
                        SubmittedByUserId = parentOrder.SubmittedByUserId, // Default to system user if null
                        DepartmentId = schedule.DepartmentId,
                        Status = "Approved", // Auto-approved!
                        IsDraft = false,
                        OrderType = "Recurring-Generated",
                        DateCreated = DateTime.Now,
                        CreatedDate = DateTime.Now,
                        DateSubmitted = DateTime.Now,
                        DateOrdered = DateTime.Now,
                        Priority = "Normal",
                        Notes = $"Auto-generated from recurring schedule {schedule.RecurringId} on {DateTime.Now:yyyy-MM-dd}",
                        // Copy freight and tax settings from parent order
                        FreightAmount = parentOrder.FreightAmount,
                        TaxAmount = parentOrder.TaxAmount,
                        TaxRate = parentOrder.TaxRate,
                        IsTaxPercentage = parentOrder.IsTaxPercentage
                    };

                    PurchaseOrderLogger.Log($"[RecurringService] Creating order: VendorId={newOrder.VendorId}, DepartmentId={newOrder.DepartmentId}, SubmittedBy={newOrder.SubmittedByUserId}");

                    db.PO_Orders25.Add(newOrder);
                    await db.SaveChangesAsync();

                    PurchaseOrderLogger.Log($"[RecurringService] Created PO {newOrder.PO_OrderId} for schedule {schedule.RecurringId}");

                    // Create line item
                    var newLineItem = new PO_LineItems25
                    {
                        PO_OrderId = newOrder.PO_OrderId,
                        ItemDescription = parentLineItem.ItemDescription,
                        VendorLink = parentLineItem.VendorLink,
                        Cost = schedule.MonthlyAmount,
                        Quantity = 1,
                        Taxable = parentLineItem.Taxable,
                        Billable = parentLineItem.Billable,
                        InventoryTracked = parentLineItem.InventoryTracked,
                        IsAllocation = parentLineItem.IsAllocation,
                        RequiresReconciliation = parentLineItem.RequiresReconciliation,
                        LineItemType = parentLineItem.LineItemType,
                        UnitType = parentLineItem.UnitType ?? "Units",
                        MaterialId = parentLineItem.MaterialId,
                        CreatedDate = DateTime.Now,
                        Status = "Approved"
                    };

                    db.PO_LineItems25.Add(newLineItem);
                    await db.SaveChangesAsync();

                    // Create budget usage entry
                    var budgetUsage = new PO_DepartmentBudgetUsage25
                    {
                        PO_LineItemId = newLineItem.PO_LineItemId,
                        Amount = schedule.MonthlyAmount,
                        IsBillable = parentLineItem.Billable,
                        UsageDate = DateTime.Now
                    };
                    db.PO_DepartmentBudgetUsage25.Add(budgetUsage);
                    await db.SaveChangesAsync();

                    // Get the approver from parent order's approvals
                    var parentApproval = await db.PO_Approvals25
                        .Where(a => a.PO_OrderId == parentOrder.PO_OrderId && a.ActionTaken == "Approved")
                        .OrderByDescending(a => a.ActionDate)
                        .FirstOrDefaultAsync();

                    // Use parent order's approver, or fallback to the submitter
                    int? approverUserId = parentApproval?.ApproverUserId ?? parentOrder.SubmittedByUserId;

                    if (!approverUserId.HasValue)
                    {
                        PurchaseOrderLogger.LogError($"[RecurringService] Schedule {schedule.RecurringId}: Cannot determine approver user ID. Parent order has no approvals and no SubmittedByUserId");
                        throw new Exception($"Schedule {schedule.RecurringId} cannot determine approver user ID. Check parent order data.");
                    }

                    PurchaseOrderLogger.Log($"[RecurringService] Using approver user ID {approverUserId.Value} from parent order approvals");

                    // Create approval record
                    var approval = new PO_Approvals25
                    {
                        PO_OrderId = newOrder.PO_OrderId,
                        ApproverUserId = approverUserId.Value,
                        ActionTaken = "Approved",
                        Comments = "Auto-approved - generated from recurring schedule",
                        ActionDate = DateTime.Now,
                        SlaBreached = false
                    };
                    db.PO_Approvals25.Add(approval);
                    await db.SaveChangesAsync();

                    // Create history record
                    var history = new PO_RecurringHistory25
                    {
                        RecurringId = schedule.RecurringId,
                        GeneratedPO_OrderId = newOrder.PO_OrderId,
                        GeneratedPO_LineItemId = newLineItem.PO_LineItemId,
                        ProcessedDate = DateTime.Now,
                        Amount = schedule.MonthlyAmount,
                        FiscalYear = DateTime.Now.Year,
                        Month = DateTime.Now.Month,
                        Status = "Success",
                        Notes = $"Successfully generated PO {newOrder.PO_OrderId}"
                    };
                    db.PO_RecurringHistory25.Add(history);
                    await db.SaveChangesAsync();

                    // Update schedule for next processing
                    schedule.LastProcessedDate = DateTime.Now;
                    schedule.NextProcessDate = CalculateNextProcessDate(schedule.NextProcessDate.Value, schedule.RecurrenceType);
                    await db.SaveChangesAsync();

                    transaction.Commit();

                    PurchaseOrderLogger.Log($"[RecurringService] Successfully processed schedule {schedule.RecurringId}. Next process date: {schedule.NextProcessDate:yyyy-MM-dd}");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    PurchaseOrderLogger.LogError($"[RecurringService] Transaction failed for schedule {schedule.RecurringId}: {ex.Message}");
                    PurchaseOrderLogger.LogException(ex);

                    // Log failure in history
                    try
                    {
                        var failureHistory = new PO_RecurringHistory25
                        {
                            RecurringId = schedule.RecurringId,
                            ProcessedDate = DateTime.Now,
                            Amount = schedule.MonthlyAmount,
                            FiscalYear = DateTime.Now.Year,
                            Month = DateTime.Now.Month,
                            Status = "Failed",
                            Notes = $"Error: {ex.Message}"
                        };
                        db.PO_RecurringHistory25.Add(failureHistory);
                        await db.SaveChangesAsync();
                    }
                    catch { /* Ignore logging errors */ }

                    throw;
                }
            }
        }

        /// <summary>
        /// Calculate the next process date based on recurrence type
        /// </summary>
        private DateTime CalculateNextProcessDate(DateTime currentProcessDate, string recurrenceType)
        {
            switch (recurrenceType?.ToLower())
            {
                case "monthly":
                    return currentProcessDate.AddMonths(1);

                case "quarterly":
                    return currentProcessDate.AddMonths(3);

                case "annually":
                case "yearly":
                    return currentProcessDate.AddYears(1);

                default:
                    PurchaseOrderLogger.LogWarning($"[RecurringService] Unknown recurrence type: {recurrenceType}, defaulting to monthly");
                    return currentProcessDate.AddMonths(1);
            }
        }

        public void Dispose()
        {
            StopProcessing();
        }
    }
}