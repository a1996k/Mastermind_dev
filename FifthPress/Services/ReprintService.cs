using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using FifthPress.Models.ViewModels2.Reprint;

namespace FifthPress.Services
{
    /// <summary>
    /// Service for handling reprint job creation and management
    /// Core business logic for the reprint system
    /// </summary>
    public class ReprintService
    {
        private readonly FifthPressEntities db;

        public ReprintService(FifthPressEntities context)
        {
            db = context;
        }

        /// <summary>
        /// Creates a new reprint job based on the request
        /// Clones the original job and creates tracking records
        /// </summary>
        public async Task<ReprintResult> CreateReprintJobAsync(VM_CreateReprintRequest request)
        {
            try
            {
                // 1. Validate request
                var validationResult = ValidateReprintRequest(request);
                if (!validationResult.IsSuccess)
                {
                    return validationResult;
                }

                // 2. Get original job with all related data
                var originalJob = await db.Jobs
                    .Include(j => j.JobItems.Select(ji => ji.JobPrices))
                    .Include(j => j.JobItems.Select(ji => ji.JobItemServices))
                    .Include(j => j.Client1)
                    .FirstOrDefaultAsync(j => j.Id == request.OriginalJobId);

                if (originalJob == null)
                {
                    return ReprintResult.Failure("Original job not found");
                }

                // 3. Generate reprint job number - use next available job number instead of appending -R suffix
                var reprintJobNumber = await GenerateNextJobNumber();

                // 4. Clone the job
                var reprintJob = CloneJob(originalJob, reprintJobNumber, request);

                // 5. Save the new job
                db.Jobs.Add(reprintJob);
                await db.SaveChangesAsync();

                // 6. Create reprint tracking record
                var reprintRecord = new Reprint_Jobs
                {
                    OriginalJobId = request.OriginalJobId,
                    ReprintJobId = reprintJob.Id,
                    ReprintType = request.ReprintType,
                    ReprintReasonId = request.ReprintReasonId,
                    ReprintCategory = request.ReprintCategory,
                    ReprintReasonText = request.ReprintReasonText,
                    CostAllocation = request.CostAllocation,
                    EstimatedCost = request.EstimatedCost,
                    Description = request.Description,
                    InternalNotes = request.InternalNotes,
                    NotifyCustomer = request.NotifyCustomer,
                    RequestedByUserId = request.RequestedByUserId,
                    DateCreated = DateTime.UtcNow
                };

                db.Reprint_Jobs.Add(reprintRecord);
                await db.SaveChangesAsync();

                // 7. If partial reprint, create item tracking records
                if (request.ReprintType == "Partial" && request.SelectedItemIds != null && request.SelectedItemIds.Any())
                {
                    foreach (var itemId in request.SelectedItemIds)
                    {
                        var originalItem = originalJob.JobItems.FirstOrDefault(ji => ji.Id == itemId);
                        if (originalItem != null)
                        {
                            // Calculate cost from JobPrices
                            var itemCost = originalItem.JobPrices?.Sum(jp => jp.Price ?? 0) ?? 0;

                            var reprintItem = new Reprint_Items
                            {
                                ReprintId = reprintRecord.ReprintId,
                                OriginalJobItemId = itemId,
                                Quantity = originalItem.Quantity ?? 0,
                                Cost = itemCost
                            };

                            db.Reprint_Items.Add(reprintItem);
                        }
                    }

                    await db.SaveChangesAsync();
                }

                // 8. Send notification if requested
                if (request.NotifyCustomer)
                {
                    // TODO: Implement email notification
                    // await SendCustomerNotification(reprintJob, originalJob, request);
                }

                return ReprintResult.Success(reprintJob.Id, reprintJobNumber);
            }
            catch (Exception ex)
            {
                return ReprintResult.Failure($"Error creating reprint job: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the next sequential reprint number for a job
        /// </summary>
        private async Task<int> GetNextReprintNumber(int originalJobId)
        {
            var existingReprints = await db.Reprint_Jobs
                .Where(rj => rj.OriginalJobId == originalJobId)
                .CountAsync();

            return existingReprints + 1;
        }

        /// <summary>
        /// Generates the next available job number using the same logic as CreateJob
        /// </summary>
        private async Task<string> GenerateNextJobNumber()
        {
            string lastTwoOfYear = DateTime.UtcNow.Year.ToString().Substring(2, 2);
            int maxJobNumber = db.v_JobNumbersAsInts.OrderByDescending(x => x.ParsedNumber).First().ParsedNumber ?? 0;
            maxJobNumber++;
            return String.Format("{0}-{1}", lastTwoOfYear, maxJobNumber.ToString().PadLeft(4, '0'));
        }

        /// <summary>
        /// Clones a job for reprinting
        /// </summary>
        private Job CloneJob(Job original, string newJobNumber, VM_CreateReprintRequest request)
        {
            var clonedJob = new Job
            {
                // Copy core job fields
                Title = original.Title,
                JobNumber = newJobNumber,
                Description = $"[REPRINT] {original.Description ?? ""}",
                Client = original.Client,
                Contact = original.Contact,
                JobStatus_Id = original.JobStatus_Id,
                PriceLevelId = original.PriceLevelId,
                MarkupOverride = original.MarkupOverride,
                ShippingMethod = original.ShippingMethod,
                ShippingAddress = original.ShippingAddress,

                // Copy nullable boolean properties with defaults (ensures non-null values)
                TaxExempt = original.TaxExempt ?? false,
                IsPickup = original.IsPickup ?? false,
                BlindShipping = original.BlindShipping ?? false,
                IsWarehouseJob = original.IsWarehouseJob ?? false,
                IsEvent = original.IsEvent ?? false,
                ScheduleSquash = original.ScheduleSquash ?? false,
                RoundPrice = original.RoundPrice ?? false,
                CalculationLock = original.CalculationLock ?? false,
                Donated = original.Donated ?? false,
                UseSqftPricing = original.UseSqftPricing ?? false,

                CreatedBy = request.RequestedByUserId,
                CreatedAt = DateTime.UtcNow,

                // Reprint-specific fields
                IsReprint = true,
                OriginalJobId = original.Id,

                // Don't set dates - force user to update
                MinStartDate = null,
                DueDate = null,

                // Notes
                InternalNotes = $"REPRINT OF JOB #{original.JobNumber}\n" +
                               $"Reason: {request.ReprintReasonText}\n" +
                               $"Cost Allocation: {request.CostAllocation}\n\n" +
                               $"{request.Description}\n\n" +
                               $"Internal Notes: {request.InternalNotes ?? "None"}",

                Notes = original.Notes
            };

            // Clone job items based on reprint type
            if (request.ReprintType == "Partial" && request.SelectedItemIds != null && request.SelectedItemIds.Any())
            {
                // Only clone selected items
                foreach (var itemId in request.SelectedItemIds)
                {
                    var originalItem = original.JobItems.FirstOrDefault(ji => ji.Id == itemId);
                    if (originalItem != null)
                    {
                        clonedJob.JobItems.Add(CloneJobItem(originalItem));
                    }
                }
            }
            else
            {
                // Clone all items (Full, Additional, Sample)
                foreach (var item in original.JobItems.Where(ji => ji.Deleted == null))
                {
                    clonedJob.JobItems.Add(CloneJobItem(item));
                }
            }

            return clonedJob;
        }

        /// <summary>
        /// Clones a single job item - copies ALL properties and services from original
        /// User can update quantities as needed on the new job
        /// </summary>
        private JobItem CloneJobItem(JobItem original)
        {
            var clonedItem = new JobItem
            {
                // Basic info
                Title = original.Title,
                Description = original.Description,
                Notes = original.Notes,
                InternalNotes = original.InternalNotes,
                MaterialNotes = original.MaterialNotes,
                Quantity = original.Quantity,
                Material = original.Material,

                // Dimensions
                Width = original.Width,
                Length = original.Length,
                Sheets = original.Sheets,
                Up = original.Up,
                Columns = original.Columns,
                Rows = original.Rows,
                MaxRows = original.MaxRows,
                MaxColumns = original.MaxColumns,
                MaterialRunSize = original.MaterialRunSize,
                RunSheets = original.RunSheets,
                RunScrapSheets = original.RunScrapSheets,
                TotalRunSheets = original.TotalRunSheets,

                // Press settings
                Press = original.Press,
                PressSpeedNumerator = original.PressSpeedNumerator,
                PressSpeedDenominator = original.PressSpeedDenominator,
                PressColorCountFront = original.PressColorCountFront,
                PressColorCountBack = original.PressColorCountBack,
                PressColorProfileFront = original.PressColorProfileFront,
                PressColorProfileBack = original.PressColorProfileBack,
                PressColorNotesFront = original.PressColorNotesFront,
                PressColorNotesBack = original.PressColorNotesBack,
                SelectedSpeed = original.SelectedSpeed,

                // Costs and pricing
                MaterialCost = original.MaterialCost,
                MaterialCostPerUnit = original.MaterialCostPerUnit,
                InkCost = original.InkCost,
                PrintTime = original.PrintTime,
                CustomMarkup = original.CustomMarkup,
                CustomInkMarkup = original.CustomInkMarkup,
                CustomMaterialMarkup = original.CustomMaterialMarkup,
                CustomPrintLaborMarkup = original.CustomPrintLaborMarkup,

                // Booleans (with defaults to ensure non-null)
                Swap = original.Swap ?? false,
                MixGrain = original.MixGrain ?? false,
                DoubleHit = original.DoubleHit ?? false,
                UserSheetOverride = original.UserSheetOverride ?? false,
                Collate = original.Collate ?? false,
                Staple = original.Staple ?? false,
                UpValueLocked = original.UpValueLocked ?? false,
                ArtworkProvided = original.ArtworkProvided ?? false,
                Numbering = original.Numbering ?? false,

                // Quantity info
                QuantityBy = original.QuantityBy,
                TotalQuantity = original.TotalQuantity,

                // Design
                DesignHours = original.DesignHours,

                // Ordinal (preserve order)
                Ordinal = original.Ordinal,

                // Reference to original quote item if it exists
                FromQuoteServiceItemId = original.FromQuoteServiceItemId,

                // Don't copy status/completion fields - start fresh
                JobStatus = null,
                Deleted = null,
                Complete = null
            };

            // Clone all JobItemServices
            if (original.JobItemServices != null && original.JobItemServices.Any())
            {
                foreach (var service in original.JobItemServices.Where(jis => jis.Deleted == null))
                {
                    clonedItem.JobItemServices.Add(new JobItemService
                    {
                        // Service reference
                        ServiceId = service.ServiceId,

                        // Core properties
                        Title = service.Title,
                        FriendlyName = service.FriendlyName,
                        Notes = service.Notes,
                        Ordinal = service.Ordinal,
                        ApexOrdinal = service.ApexOrdinal,

                        // Timing
                        ServiceTime = service.ServiceTime,
                        MakeReady = service.MakeReady,
                        WashUp = service.WashUp,
                        HourlyRate = service.HourlyRate,
                        UserTimeOverride = service.UserTimeOverride ?? false,

                        // Configuration
                        ServiceConfiguration = service.ServiceConfiguration,
                        FormulaConfiguration = service.FormulaConfiguration,

                        // Material/finishing
                        MaterialCost = service.MaterialCost,
                        JobItemFinishingMaterial = service.JobItemFinishingMaterial,
                        Caliper = service.Caliper,

                        // Scrap
                        ScrapAmount = service.ScrapAmount,
                        ScrapPercent = service.ScrapPercent,

                        // Markup
                        CustomMarkup = service.CustomMarkup,

                        // Service types (with defaults)
                        IsCuttingService = service.IsCuttingService ?? false,
                        IsFinishingService = service.IsFinishingService ?? false,

                        // Quote reference
                        FromQuoteServiceId = service.FromQuoteServiceId,

                        // Don't copy status/completion/user assignments
                        Operator = null,
                        JobStatus = null,
                        Deleted = null,
                        Complete = null
                    });
                }
            }

            return clonedItem;
        }

        /// <summary>
        /// Validates a reprint request
        /// </summary>
        public ReprintResult ValidateReprintRequest(VM_CreateReprintRequest request)
        {
            if (request == null)
                return ReprintResult.Failure("Request cannot be null");

            if (request.OriginalJobId <= 0)
                return ReprintResult.Failure("Invalid original job ID");

            if (string.IsNullOrWhiteSpace(request.ReprintType))
                return ReprintResult.Failure("Reprint type is required");

            if (!request.ReprintReasonId.HasValue)
                return ReprintResult.Failure("Reprint reason is required");

            if (string.IsNullOrWhiteSpace(request.CostAllocation))
                return ReprintResult.Failure("Cost allocation is required");

            if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Length < 10)
                return ReprintResult.Failure("Description must be at least 10 characters");

            if (request.ReprintType == "Partial" && (request.SelectedItemIds == null || !request.SelectedItemIds.Any()))
                return ReprintResult.Failure("Partial reprint requires at least one item to be selected");

            return ReprintResult.Success(0, "");
        }

        /// <summary>
        /// Gets reprint history for a job (both as original and as reprint)
        /// </summary>
        public async Task<VM_ReprintHistory> GetReprintHistoryAsync(int jobId)
        {
            var job = await db.Jobs.FindAsync(jobId);
            if (job == null)
                return null;

            var history = new VM_ReprintHistory
            {
                JobId = jobId,
                JobNumber = job.JobNumber,
                IsReprint = job.IsReprint
            };

            if (job.IsReprint && job.OriginalJobId.HasValue)
            {
                // This IS a reprint - get original job info
                var originalReprintRecord = await db.Reprint_Jobs
                    .Include(rj => rj.Job) // Original job
                    .Include(rj => rj.User) // Requested by
                    .FirstOrDefaultAsync(rj => rj.ReprintJobId == jobId);

                if (originalReprintRecord != null)
                {
                    history.OriginalJobInfo = MapToHistoryItem(originalReprintRecord);
                }
            }
            else
            {
                // This is an ORIGINAL job - get all reprints
                var reprints = await db.Reprint_Jobs
                    .Include(rj => rj.Job1) // Reprint job
                    .Include(rj => rj.User) // Requested by
                    .Where(rj => rj.OriginalJobId == jobId)
                    .OrderByDescending(rj => rj.DateCreated)
                    .ToListAsync();

                history.Reprints = reprints.Select(MapToHistoryItem).ToList();
            }

            return history;
        }

        /// <summary>
        /// Maps Reprint_Jobs entity to VM_ReprintHistoryItem
        /// </summary>
        private VM_ReprintHistoryItem MapToHistoryItem(Reprint_Jobs record)
        {
            return new VM_ReprintHistoryItem
            {
                ReprintId = record.ReprintId,
                OriginalJobId = record.OriginalJobId,
                OriginalJobNumber = record.Job?.JobNumber,
                ReprintJobId = record.ReprintJobId,
                ReprintJobNumber = record.Job1?.JobNumber,
                ReprintType = record.ReprintType,
                ReprintCategory = record.ReprintCategory,
                ReprintReasonText = record.ReprintReasonText,
                CostAllocation = record.CostAllocation,
                EstimatedCost = record.EstimatedCost,
                Description = record.Description,
                DateCreated = record.DateCreated,
                RequestedByName = $"{record.User?.FirstName} {record.User?.LastName}"
            };
        }

        /// <summary>
        /// Updates the reprint reason on an existing reprint record.
        /// Re-writes the denormalized reason fields (ReprintReasonId / ReprintCategory /
        /// ReprintReasonText) the same way CreateReprintJobAsync sets them, so reporting
        /// (which reads the denormalized text/category) stays consistent.
        /// Cost allocation is intentionally left unchanged - it is a separate decision.
        /// </summary>
        public async Task<ReprintResult> UpdateReprintReasonAsync(int reprintId, int reasonId)
        {
            var reprint = await db.Reprint_Jobs.FirstOrDefaultAsync(rj => rj.ReprintId == reprintId);
            if (reprint == null)
                return ReprintResult.Failure("Reprint record not found");

            var reason = await db.Reprint_Reasons.FirstOrDefaultAsync(rr => rr.ReasonId == reasonId);
            if (reason == null)
                return ReprintResult.Failure("Reprint reason not found");

            reprint.ReprintReasonId = reason.ReasonId;
            reprint.ReprintCategory = reason.Category;
            reprint.ReprintReasonText = reason.ReasonText;

            await db.SaveChangesAsync();

            return ReprintResult.Success(reprint.ReprintJobId, "");
        }

        /// <summary>
        /// Gets all categorized reprint reasons
        /// </summary>
        public async Task<List<VM_ReprintReason>> GetReprintReasonsAsync()
        {
            var reasons = await db.Reprint_Reasons
                .Where(rr => rr.IsActive)
                .OrderBy(rr => rr.SortOrder)
                .ToListAsync();

            return reasons.Select(r => new VM_ReprintReason
            {
                ReasonId = r.ReasonId,
                Category = r.Category,
                ReasonText = r.ReasonText,
                DefaultCostAllocation = r.DefaultCostAllocation,
                SortOrder = r.SortOrder
            }).ToList();
        }

        /// <summary>
        /// Calculates estimated cost for reprint based on job and selected items
        /// </summary>
        public async Task<decimal> CalculateReprintCostAsync(int jobId, List<int> selectedItemIds = null)
        {
            var job = await db.Jobs
                .Include(j => j.JobItems.Select(ji => ji.JobPrices))
                .FirstOrDefaultAsync(j => j.Id == jobId);

            if (job == null)
                return 0;

            if (selectedItemIds != null && selectedItemIds.Any())
            {
                // Partial reprint - sum only selected items
                return job.JobItems
                    .Where(ji => selectedItemIds.Contains(ji.Id) && ji.Deleted == null)
                    .Sum(ji => ji.JobPrices?.Sum(jp => jp.Price ?? 0) ?? 0);
            }

            // Full reprint - sum all items
            return job.JobItems
                .Where(ji => ji.Deleted == null)
                .Sum(ji => ji.JobPrices?.Sum(jp => jp.Price ?? 0) ?? 0);
        }

        /// <summary>
        /// Gets job items for selection in partial reprint
        /// </summary>
        public async Task<List<VM_JobItemSelection>> GetJobItemsForSelectionAsync(int jobId)
        {
            var job = await db.Jobs
                .Include(j => j.JobItems.Select(ji => ji.JobPrices))
                .Include(j => j.JobItems.Select(ji => ji.Material1))
                .FirstOrDefaultAsync(j => j.Id == jobId);

            if (job == null)
                return new List<VM_JobItemSelection>();

            return job.JobItems
                .Where(ji => ji.Deleted == null)
                .Select(ji => new VM_JobItemSelection
                {
                    JobItemId = ji.Id,
                    Description = ji.Description ?? ji.Title,
                    Quantity = ji.Quantity ?? 0,
                    Material = ji.Material1?.Title ?? "N/A",
                    Cost = ji.JobPrices?.Sum(jp => jp.Price ?? 0) ?? 0,
                    IsSelected = false
                })
                .ToList();
        }
    }

    /// <summary>
    /// Result object for reprint operations
    /// </summary>
    public class ReprintResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public int NewJobId { get; set; }
        public string NewJobNumber { get; set; }

        public static ReprintResult Success(int jobId, string jobNumber)
        {
            return new ReprintResult
            {
                IsSuccess = true,
                NewJobId = jobId,
                NewJobNumber = jobNumber,
                Message = "Reprint job created successfully"
            };
        }

        public static ReprintResult Failure(string message)
        {
            return new ReprintResult
            {
                IsSuccess = false,
                Message = message
            };
        }
    }
}