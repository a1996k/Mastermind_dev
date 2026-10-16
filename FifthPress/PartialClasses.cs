using FifthPress.Controllers;
using FifthPress.Models;
using FifthPress.Models.ViewModels;
using FifthPress.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Activities.Statements;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Core.Objects;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Threading.Tasks;
using System.Web;
using FifthPress.Initializers;

namespace FifthPress
{
    [DbConfigurationType(typeof(FifthPressDbConfiguration))]
    public partial class FifthPressEntities
    {
        private bool DifferenceNotPrecision(string ov, string cv)
        {
            try
            {
                decimal dorig = Decimal.Parse(ov);
                decimal dcur = Decimal.Parse(cv);

                // Both values are decimals - compare them numerically to avoid precision issues
                return dorig != dcur;
            }
            catch (Exception)
            {
                // Values are not decimals (strings, dates, booleans, etc.)
                // This represents a real difference, not a precision issue
                return true;
            }
        }

        public override async Task<int> SaveChangesAsync()
        {
            var modifiedEntities = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Modified || e.State == EntityState.Added || e.State == EntityState.Deleted)
                .ToList();

            //int code = HttpContext.Current.Response.StatusCode;

            // Check if we have an HTTP context (won't exist for background services)
            if (HttpContext.Current == null)
            {
                // No HTTP context - running in background service, skip audit logging
                return await base.SaveChangesAsync();
            }

            HttpCookie userCookie = HttpContext.Current.Request.Cookies.Get("ActiveUser");
            if (userCookie == null)
            {
                HttpContext.Current.Response.StatusCode = 500;
                return await base.SaveChangesAsync();
            }

            //get the username of the logged in user

            foreach (var entityEntry in modifiedEntities)
            {
                var entityType = entityEntry.Entity.GetType();
                var properties = entityType.GetProperties();

                foreach (var property in properties)
                {
                    string typeNameToWrite = "";
                    try
                    {
                        if (entityType.Name.IndexOf('_') > -1)
                            typeNameToWrite = entityType.Name.Split('_')[0];
                        else
                            typeNameToWrite = entityType.Name;

                        var propertyName = property.Name;
                        var originalValue = entityEntry.OriginalValues.GetValue<object>(propertyName)?.ToString();
                        var currentValue = entityEntry.CurrentValues.GetValue<object>(propertyName)?.ToString();

                        if (originalValue != currentValue && DifferenceNotPrecision(originalValue, currentValue))
                        {
                            var auditLog = new Audit
                            {
                                EntityType = typeNameToWrite,
                                PrimaryKeyValue = entityEntry.OriginalValues.GetValue<object>("Id").ToString(),
                                PropertyName = propertyName,
                                OldValue = originalValue,
                                NewValue = currentValue,
                                ChangeDateTime = DateTime.Now,
                                UserId = int.Parse(userCookie.Value)
                            };

                            this.Audits.Add(auditLog);
                        }
                    }
                    catch (Exception)
                    { }
                }
            }
            return await base.SaveChangesAsync();
        }
    }

    public partial class PO_Orders25
    {
        public decimal TotalAmount
        {
            get
            {
                if (PO_LineItems25 == null || !PO_LineItems25.Any())
                    return 0;

                return PO_LineItems25
                    .Sum(li => li.Quantity * li.Cost);
            }
        }

        public DateTime? DateApproved
        {
            get
            {
                if (PO_Approvals25 == null)
                    return null;

                return PO_Approvals25
                    .Where(a => a.ActionTaken == "Approved")
                    .OrderBy(a => a.ActionDate)
                    .Select(a => (DateTime?)a.ActionDate)
                    .FirstOrDefault();
            }
        }

        public DateTime? SLADueDate
        {
            get
            {
                if (!DateSubmitted.HasValue)
                    return null;

                var config = new ConfigurationService25(new FifthPressEntities());

                int slaHours = config.GetConfigValue("StandardApprovalHours", 72);
                var ownerLimit = config.GetConfigValue("RequireOwnerApprovalAmount", 5000m);
                var ownerHours = config.GetConfigValue("OwnerApprovalHours", 96);

                if (TotalAmount >= ownerLimit)
                    slaHours = ownerHours;

                return DateSubmitted.Value.AddHours(slaHours);
            }
        }

        public string SLAStatus
        {
            get
            {
                var due = SLADueDate;
                if (!due.HasValue)
                    return "Unknown";

                if (!DateApproved.HasValue)
                    return DateTime.Now > due.Value ? "Pending (Overdue)" : "Pending";

                return DateApproved <= due ? "OnTime" : "Late";
            }
        }
    }

    public partial class InventoryAdjustmentHistory
    {
        public string DateUTCString { get; set; }
        public string QuantityUnit { get; set; }
    }

    public enum JobStage
    {
        InProgress = 1,
        ReadyToShip = 2,
        ReadyToPost = 3,
        Completed = 4
    }

    public enum JobState
    {
        NotInProgress = 1,
        InProgress = 3
    }

    public partial class CalculateUtilizationForAgency_Result
    {
        public DailyTotals dailyTotals { get; set; }

        public bool MeetsTargetWeeklyUtilization { get; set; } // Flag: meets target utilization
        public bool MeetsExpectedWeeklyTime { get; set; } // Flag: meets expected total time
        public bool MeetsTargetYTDUtilization { get; set; }
        public bool MeetsExpectedYTDTime { get; set; }

        public class BillableTotals
        {
            public decimal BillableUtilization { get; set; }
            public string BillableUtilizationString { get; set; }
            public decimal BillableTime { get; set; }
            public string BillableTimeString { get; set; }
            public bool MeetsUtzTarget { get; set; }
        }

        public class DailyTotals
        {
            public decimal TotalTime { get; set; }
            public string TotalTimeString { get; set; }
            public decimal TotalUtilization { get; set; }
            public string TotalUtilizationString { get; set; }
            public bool MeetsTimeTarget { get; set; }

            public BillableTotals BillableTotals { get; set; }
        }

        public YearlyTotals yearlyTotals { get; set; }

        public class YearlyTotals
        {
            public decimal TotalTime { get; set; }
            public string TotalTimeString { get; set; }
            public decimal TotalUtilization { get; set; }
            public string TotalUtilizationString { get; set; }
            public bool MeetsTimeTarget { get; set; }

            public BillableTotals BillableTotals { get; set; }
        }
    }

    public partial class Report_TimeEntries_Result
    {
        public bool IsActive { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
    }

    public partial class sch_PurchaseOrders_Result
    {
        public string ApprovalDateString { get; set; }
        public string ReferenceString { get; set; }
        public string ItemOrdinalString { get; set; }
    }

    public partial class RecordSearch3_Result
    {
        public string CompletedString { get; set; }
        public string CreatedString { get; set; }
        public string Dimensions { get; set; }
        public string TotalPriceString { get; set; }

        public static readonly DateTime NoCreatedDate = DateTime.Parse("1900-01-01");
    }

    public partial class RecordSearch4_Result
    {
        public string CompletedString { get; set; }
        public string CreatedString { get; set; }
        public string Dimensions { get; set; }
        public string TotalPriceString { get; set; }
        public bool IsCancelled { get; set; }

        public static readonly DateTime NoCreatedDate = DateTime.Parse("1900-01-01");
    }

    public partial class GetJobCostSheetTVF_Result
    {
        public Nullable<decimal> ActualTime { get; set; }
        public string ActualTimeFormatted { get; set; }
        public string ChargeTimeFormatted { get; set; }
        public bool PositiveSpread { get; set; }
        public decimal SpreadAmount { get; set; }
        public string SpreadAmountString { get; set; }
        public string UnitCostString { get; set; }
        public decimal InternalCost { get; set; }
        public string InternalCostString { get; set; }
        public string ChargedPriceString { get; set; }
        public decimal PercentDifference { get; set; }
        public string PercentDifferenceString { get; set; }
    }

    public partial class fn_AgencyOverview2_Result
    {
        public DailyTotals dailyTotals { get; set; }

        public bool MeetsTargetWeeklyUtilization { get; set; } // Flag: meets target utilization
        public bool MeetsExpectedWeeklyTime { get; set; } // Flag: meets expected total time
        public bool MeetsTargetYTDUtilization { get; set; }
        public bool MeetsExpectedYTDTime { get; set; }

        public class BillableTotals
        {
            public decimal BillableUtilization { get; set; }
            public string BillableUtilizationString { get; set; }
            public decimal BillableTime { get; set; }
            public string BillableTimeString { get; set; }
            public bool MeetsUtzTarget { get; set; }
        }

        public class DailyTotals
        {
            public decimal TotalTime { get; set; }
            public string TotalTimeString { get; set; }
            public decimal TotalUtilization { get; set; }
            public string TotalUtilizationString { get; set; }
            public bool MeetsTimeTarget { get; set; }

            public BillableTotals BillableTotals { get; set; }
        }

        public YearlyTotals yearlyTotals { get; set; }

        public class YearlyTotals
        {
            public decimal TotalTime { get; set; }
            public string TotalTimeString { get; set; }
            public decimal TotalUtilization { get; set; }
            public string TotalUtilizationString { get; set; }
            public bool MeetsTimeTarget { get; set; }

            public BillableTotals BillableTotals { get; set; }
        }
    }

    public partial class ClockActivitySinceDate_Result
    {
        public string TimeInString { get; set; }
        public string TimeOutString { get; set; }
        public string BreakStartString { get; set; }
        public string BreakEndString { get; set; }
        public string EventDateString { get; set; }
    }

    public partial class fn_CompletedJobSummary2_Result
    {
        public string FormattedTime { get; set; }
        public int TimeInSeconds { get; set; }
    }

    public partial class Report_Time_WeeklyTimeReport_Result
    {
        public string DaySpanFormatted { get; set; }
        public string BreakTimeFormatted { get; set; }
        public string WorkTimeFormatted { get; set; }
    }

    [MetadataType(typeof(PO_OrdersMetadata))]
    public partial class PO_Orders
    {
        public enum POState
        {
            New,
            Pending,
            Denied,
            Approved,
            Complete,
            Cancelled
        }

        public POState OrderState { get; set; }

        public void SetPurchaseOrderState()
        {
            // Initialize OrderState to New
            OrderState = POState.New;

            // Use the RequestDate or a default minimum value
            DateTime lastRequestDate = this.RequestDate ?? DateTime.MinValue;

            // Count approvals and denials after the last RequestDate
            int approvalsAfterRequest = this.PO_Approvals.Count(a => a.ApprovalDate.Value > lastRequestDate && a.Invalidated == null);
            int denialsAfterRequest = this.PO_Denials.Count(d => d.DenialDate.Value > lastRequestDate);

            if (this.CancelDate.HasValue)
            {
                OrderState = POState.Cancelled;
            }
            // If there are any denials after the RequestDate, set to Denied
            else if (denialsAfterRequest > 0)
            {
                OrderState = POState.Denied;
            }
            // Else if there are any approvals after the RequestDate, set to Approved
            else if (approvalsAfterRequest > 0)
            {
                OrderState = POState.Approved;
            }
            // Else if the RequestDate has a value, set to Pending
            else if (this.RequestDate.HasValue)
            {
                OrderState = POState.Pending;
            }
            // Else, the OrderState remains New
        }
    }

    public partial class JobDocument
    {
        public string FileName { get; set; }
    }

    public partial class JobPrice
    {
        public string StringTime { get; set; }
        public int PriceListOrdinal { get; set; }
    }

    public partial class PO_Items
    {
        public string ItemTitle { get; set; }

        public void LoadItemTitle(FifthPressEntities _db)
        {
            this.ItemTitle = "";

            //sometimes 0 is set as the item id, so ensure it's not 0 here
            if (this.Item != 0)
            {
                //set the item title to the material or item, bassed on "track inventory" property
                if (this.TrackInventory ?? false)
                    this.ItemTitle = _db.Materials.Find(this.Item).Title;
                else
                    this.ItemTitle = _db.Items.Find(this.Item).Title;
            }
        }
    }

    public partial class QuotePrice
    {
        public string StringTime { get; set; }
        public int PriceListOrdinal { get; set; }
    }

    public partial class DashboardTab
    {
        public string TabSlug { get; set; }
    }

    [MetadataType(typeof(JobMetadata))]
    public partial class Job
    {
        public string TruncatedTitle { get; set; }
        public string TruncatedDescription { get; set; }
        public int DaysRemaining { get; set; }
        public JobState CurrentState { get; set; }
        public JobStage OverallJobStage { get; set; }
        public decimal OverallMarkupPercentAsPercent { get; set; }
        public decimal MarkupOverrideAsPercent { get; set; }

        //public int MarkupOverrideAsInteger { get; set; }
        public string OverallMarkupPercentAsFormattedPercent { get; set; }

        public string MarkupOverrideAsFormattedPercent { get; set; }
        public decimal TotalJobTime { get; set; }
        public string TotalJobTimeFormatted { get; set; }
        public int TotalSheetCount { get; set; }
        public bool JobSchedule_JobIsValid { get; set; }
        public string FormattedShippingAddress { get; set; }
        public string FormattedFreightShippingAddress { get; set; }
        public List<JobParentItemTotals_Result> ItemTotals { get; set; }

        public async Task CopyDocumentsToNewJob(int newJobId, FifthPressEntities _db)
        {
            // Base path to the storage directory
            string basePath = @"C:\Storage\mastermind_job_docs"; // Update with your actual base path

            // 1. Ensure 'this' Job is loaded with its JobItems
            await _db.Entry(this).Collection(c => c.JobItems).LoadAsync();

            // 2. Find the new Job by newJobId
            Job newJob = await _db.Jobs.FindAsync(newJobId);

            // Safety check
            if (newJob == null)
                throw new Exception("New Job Not Found");

            // 3. Load the JobItems related to the new Job
            await _db.Entry(newJob).Collection(c => c.JobItems).LoadAsync();

            // Iterate over each JobItem in the "old" (current) Job
            foreach (JobItem oldJobItem in this.JobItems.ToList())
            {
                // 4. Find the matching JobItem in the new Job
                //    This uses Ordinal as an example — adapt as needed
                var newJobItem = newJob.JobItems.FirstOrDefault(x => x.Ordinal == oldJobItem.Ordinal);
                if (newJobItem == null)
                    continue; // Skip if no matching item is found in the new Job

                // 5. Get all JobDocuments associated with the old JobItem
                var documents = await _db.JobDocuments
                    .Where(x => x.ItemId == oldJobItem.Id && x.RowType == "Standard")
                    .ToListAsync();

                // For each document, copy the file and create a new JobDocument
                foreach (JobDocument jd in documents)
                {
                    string oldDocumentPath = jd.DocumentPath;

                    // Original job number (replacing dashes with underscores for path consistency)
                    string oldJobNumber = this.JobNumber.Replace("-", "_");

                    // New job number (replace with underscores if needed)
                    string newJobNumber = newJob.JobNumber.Replace("-", "_");

                    // 6. Adjust the DocumentPath by replacing the old job number with the new one
                    //    The assumption is the path structure contains \\oldJobNumber\\ somewhere
                    string newDocumentPath = oldDocumentPath.Replace(
                        $@"\{oldJobNumber}\",
                        $@"\{newJobNumber}\"
                    );

                    // 7. Copy the physical file from old path to new path
                    try
                    {
                        // Make sure the destination directory exists
                        Directory.CreateDirectory(Path.GetDirectoryName(newDocumentPath));

                        // Copy the file
                        File.Copy(oldDocumentPath, newDocumentPath, overwrite: true);
                    }
                    catch (Exception ex)
                    {
                        // Handle exceptions as needed
                        throw new Exception($"Error copying file {oldDocumentPath}: {ex.Message}");
                    }

                    // 8. Create a new JobDocument (now associated with the new JobItem)
                    _db.JobDocuments.Add(new JobDocument
                    {
                        ItemId = newJobItem.Id,
                        DocumentPath = newDocumentPath,
                        DocumentHash = jd.DocumentHash,
                        UploadDate = jd.UploadDate,
                        UploadedBy = jd.UploadedBy,
                        RowType = jd.RowType
                    });
                }
            }

            // 9. Save changes to the database
            await _db.SaveChangesAsync();
        }

        public async Task CopyDocumentsToNewQuote(int? quoteId, FifthPressEntities _db)
        {
            // Base path to the storage directory
            string basePath = @"C:\Storage\mastermind_job_docs"; // Update with your actual base path

            // Ensure 'this' Job is loaded with its JobItems
            await _db.Entry(this).Collection(c => c.JobItems).LoadAsync();

            // Find the Quote by quoteId
            Quote q = await _db.Quotes.FindAsync(quoteId);

            // Safety check
            if (q == null)
                throw new Exception("Quote Not Found");

            // Load the QuoteItems related to the found Quote
            await _db.Entry(q).Collection(c => c.QuoteItems).LoadAsync();

            foreach (JobItem ji in this.JobItems.ToList())
            {
                // Find the corresponding QuoteItem for the JobItem
                var qi = q.QuoteItems.FirstOrDefault(x => x.Ordinal == ji.Ordinal);

                if (qi == null)
                    continue; // Skip if no matching QuoteItem is found

                // Get all JobDocuments associated with the JobItem
                var documents = await _db.JobDocuments
                    .Where(x => x.ItemId == ji.Id && x.RowType == "Standard")
                    .ToListAsync();

                foreach (JobDocument jd in documents)
                {
                    // Adjust the DocumentPath by replacing the JobNumber with the QuoteNumber
                    string oldDocumentPath = jd.DocumentPath;
                    string jobNumber = this.JobNumber.Replace("-", "_");
                    string quoteNumber = q.JobNumber;

                    string newDocumentPath = oldDocumentPath.Replace(String.Format("\\{0}\\", jobNumber), String.Format("\\{0}\\", quoteNumber));
                    //string newDocumentPath = ReplaceFirstOccurrence(oldDocumentPath, jobNumber, quoteNumber);

                    // **Copy the physical file from old path to new path**
                    try
                    {
                        //string sourceFilePath = Path.Combine(basePath, oldDocumentPath);
                        //string destinationFilePath = Path.Combine(basePath, newDocumentPath);

                        // Ensure the destination directory exists
                        Directory.CreateDirectory(Path.GetDirectoryName(newDocumentPath));

                        // **Copy the file**
                        File.Copy(oldDocumentPath, newDocumentPath, overwrite: true);
                    }
                    catch (Exception ex)
                    {
                        // Handle exceptions as needed
                        throw new Exception($"Error copying file {jd.DocumentPath}: {ex.Message}");
                    }

                    // Create a new QuoteDocument
                    _db.QuoteDocuments.Add(new QuoteDocument
                    {
                        ItemId = qi.Id,
                        DocumentPath = newDocumentPath,
                        DocumentHash = jd.DocumentHash,
                        UploadDate = jd.UploadDate,
                        UploadedBy = jd.UploadedBy,
                        RowType = jd.RowType
                    });
                }
            }

            // Save changes to the database
            await _db.SaveChangesAsync();
        }

        // Helper function to replace the first occurrence of a string
        public static string ReplaceFirstOccurrence(string source, string find, string replace)
        {
            int place = source.IndexOf(find);
            if (place == -1)
            {
                return source;
            }
            return source.Remove(place, find.Length).Insert(place, replace);
        }

        public async Task<string> GetFormattedShippingAddress(FifthPressEntities _db)
        {
            if (this.ShippingAddress.HasValue)
            {
                await _db.Entry(this).Reference(r => r.ShippingAddress1).LoadAsync();
                await _db.Entry(this.ShippingAddress1).Reference(r => r.USState).LoadAsync();

                //extra handling for null city/state/zip
                if (ShippingAddress1.City == null || ShippingAddress1.State == null || ShippingAddress1.Zip == null)
                    return "";
                else
                    return String.Format("{0}, {1} {2}", ShippingAddress1.City, ShippingAddress1.USState.Abbreviation, ShippingAddress1.Zip);
            }
            else
            {
                return "";
            }
        }

        public async Task<string> GetFormattedFreightAddress(FifthPressEntities _db)
        {
            if (this.FreightAddress.HasValue)
            {
                await _db.Entry(this).Reference(r => r.FreightShippingAddress).LoadAsync();
                await _db.Entry(this.FreightShippingAddress).Reference(r => r.USState).LoadAsync();

                //extra handling for null city/state/zip
                if (FreightShippingAddress.City == null || FreightShippingAddress.State == null || FreightShippingAddress.Zip == null)
                    return "";
                else
                    return String.Format("{0}, {1} {2}", FreightShippingAddress.City, FreightShippingAddress.USState.Abbreviation, FreightShippingAddress.Zip);
            }
            else
            {
                return "";
            }
        }

        public void SetCurrentJobStatus()
        {
            if (this.Complete == null)
                this.OverallJobStage = JobStage.InProgress;
            if (this.Complete.HasValue)
                this.OverallJobStage = JobStage.ReadyToShip;
            if (this.Shipped.HasValue)
                this.OverallJobStage = JobStage.ReadyToPost;
            if (this.Billed.HasValue)
                this.OverallJobStage = JobStage.Completed;
        }
    }

    public partial class JobItem
    {
        public string FormattedPrintTime { get; set; }
        public decimal MaterialLength { get; set; }
        public decimal MaterialWidth { get; set; }
        public decimal RunLength { get; set; }
        public decimal RunWidth { get; set; }
        public decimal FinishLength { get; set; }
        public decimal FinishWidth { get; set; }
        public string MaterialCostPerUnitFormatted { get; set; }
        public string MaterialCostFormatted { get; set; }
        public int DocumentCount { get; set; }
        public int? LatestProofStatus { get; set; }  // 0=Pending, 1=Approved, 2=Rejected/Denied

        public string ItemStatus { get; set; }

        public void SetRollStockLength(decimal length)
        {
            if (MaterialRunSize == null)
                MaterialRunSize = "0x0";

            string[] mrs = MaterialRunSize.Split('x');

            // Ensure we have at least 2 parts
            if (mrs.Length < 2)
            {
                MaterialRunSize = "0x0";
                mrs = MaterialRunSize.Split('x');
            }

            // Format the length to avoid locale issues and excessive decimal places
            mrs[0] = length.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
            MaterialRunSize = string.Join("x", mrs);
        }

        public void ParseRunSize()
        {
            string runSize = this.MaterialRunSize ?? "";

            if (runSize.Contains("x"))
            {
                string[] runSizeArray = runSize.Split('x');

                if (runSizeArray.Length == 2)
                {
                    bool parsedLength = decimal.TryParse(runSizeArray[0], out decimal length);
                    bool parsedWidth = decimal.TryParse(runSizeArray[1], out decimal width);

                    if (parsedLength && parsedWidth)
                    {
                        this.RunLength = length;
                        this.RunWidth = width;
                    }
                    else
                    {
                        this.RunLength = 0m;
                        this.RunWidth = 0m;
                    }
                }
            }
        }

        //expects all foreign keys to be loaded upon being called
        public decimal CalculateMaterialCost(decimal? costOverride)
        {
            decimal finalCost = 0m;
            decimal matCost = 0m;
            if (costOverride != null)
            {
                matCost = costOverride.Value;
            }

            if (Material.HasValue)
            {
                if (costOverride == null)
                {
                    matCost = Material1.Cost ?? 0m;
                }
                switch (Material1.StockType)
                {
                    case (int)FifthPress.Material.AvailableStockTypes.CutVinyl:
                    case (int)FifthPress.Material.AvailableStockTypes.RollStock:
                        decimal lengthInFeet = (RunLength + Decimal.Parse(Gripper ?? "0") + Decimal.Parse(Tail ?? "0")) / 12m;
                        finalCost = lengthInFeet * matCost;
                        break;

                    case (int)FifthPress.Material.AvailableStockTypes.Sheet:

                        int totalSheets = (Sheets ?? 0) + (ScrapSheets ?? 0);
                        switch (Material1.CostType)
                        {
                            case (int)FifthPress.Material.MaterialCostTypes.Piece:
                                finalCost = totalSheets * matCost;
                                break;

                            case (int)FifthPress.Material.MaterialCostTypes.Hundred:
                                finalCost = (totalSheets / 100m) * matCost;
                                break;

                            case (int)FifthPress.Material.MaterialCostTypes.Thousand:
                                finalCost = (totalSheets / 1000m) * matCost;
                                break;
                        }

                        break;
                }

                return finalCost;
            }

            return 0m;
        }

        public string GetItemStatus(FifthPressEntities _db)
        {
            var itemStatus = new ObjectParameter("ItemStatus", typeof(string));

            if (String.IsNullOrEmpty(this.ItemStatus))
            {
                try
                {
                    _db.GetItemStatus(this.Id, itemStatus);
                    this.ItemStatus = itemStatus.Value as string;
                }
                catch (Exception)
                {
                    MastermindLogger.Log(LogLevel.Error, "Exception while retrieving status for Job Id = " + this.Id.ToString());
                    throw;
                }
            }

            return this.ItemStatus;
        }
    }

    [MetadataType(typeof(JobItemServiceMetadata))]
    public partial class JobItemService
    {
        public decimal? TimeSpent { get; set; }
        public string TimeSpentFormatted { get; set; }
        public List<Tuple<int, string>> AssignedUsers { get; set; }

        //meant for time tracking page; indicates if this service is running for this user
        public bool UserContext_IsRunning { get; set; }

        //meant for time tracking page; indicates the start time of the task for the user
        public DateTime? UserContext_StartTimeUTC { get; set; }

        public decimal UserContext_TotalOverallTime { get; set; } = 0m;

        public string UserContext_TotalOverallTimeFormatted = "--:--";
        public decimal UserContext_TotalRemainingTime { get; set; } = 0m;
        public string UserContext_TotalRemainingTimeFormatted { get; set; } = "--:--";
        public decimal UserContext_UserServiceTime { get; set; } = 0m;
        public string UserContext_UserServiceTimeFormatted { get; set; } = "--:--";
        public long UserContext_UserElapsedMilliseconds { get; set; } = 0;
        public decimal UserContext_SingleServiceTime { get; set; } = 0m;
        public string UserContext_SingleServiceTimeFormatted { get; set; } = "--:--";
        public long UserContext_SingleServiceTimeMilliseconds { get; set; } = 0;

        public bool ServiceComplete { get; set; }

        public void SetDefaults(Service s)
        {
            this.Caliper = s.Caliper;
            this.Operator = s.Operator;
            this.Deleted = null;
            this.Notes = s.Notes;
            this.HourlyRate = s.BasePrice;
            this.ServiceConfiguration = s.ServiceConfiguration;
            this.MakeReady = s.MakeReady;
            this.WashUp = s.WashUp;
            this.ServiceTime = 0m;
        }

        public class ServiceConfig
        {
            public Dictionary<string, string> ConfigKeyValuePairs { get; set; }

            public List<string> Keys { get; set; }

            public string ConfigName { get; set; }
            public string ConfigNameFriendly { get; set; }

            public string GetKeyValue(string key)
            {
                bool parsed = ConfigKeyValuePairs.TryGetValue(key, out string value);

                if (parsed)
                    return value;
                else
                    throw new Exception("Unable to parse that value or an item with that key does not exist");
            }

            public void SetKeyValue(string key, string value)
            {
                ConfigKeyValuePairs[key] = value;
            }

            public void AddKey(string key, string value)
            {
                if (ConfigKeyValuePairs.ContainsKey(key))
                    throw new Exception("An item with that key already exists");
                else
                    ConfigKeyValuePairs.Add(key, value);
            }

            public void RemoveKey(string key)
            {
                ConfigKeyValuePairs.Remove(key);
            }

            public bool HasKey(string key)
            {
                return ConfigKeyValuePairs.ContainsKey(key);
            }

            public ServiceConfig(string theConfig)
            {
                if (String.IsNullOrEmpty(theConfig))
                {
                    ConfigKeyValuePairs = new Dictionary<string, string>();
                }
                else
                {
                    JObject parsedObject = JObject.Parse(theConfig);
                    if (parsedObject.Count > 0)
                    {
                        // Get the first property in the JSON object
                        JProperty firstProperty = (JProperty)parsedObject.First;

                        // Set ConfigName and ConfigNameFriendly
                        ConfigName = firstProperty.Name;
                        ConfigNameFriendly = ConfigName.Replace("_", " ");

                        // Deserialize the inner JSON object to dictionary
                        ConfigKeyValuePairs = firstProperty.Value.ToObject<Dictionary<string, string>>();
                    }
                    else
                    {
                        ConfigKeyValuePairs = new Dictionary<string, string>();
                    }
                }

                Keys = new List<string>();

                foreach (KeyValuePair<string, string> kvp in ConfigKeyValuePairs)
                {
                    Keys.Add(kvp.Key);
                }
            }
        }

        public ServiceConfig ServiceConfigg { get; set; }
        public decimal WorkedTimeDecimalHours { get; set; }
        public string WorkedTimeFormatted { get; set; }

        public void ParseServiceConfiguration()
        {
            string fc = FormulaConfiguration ?? "";

            this.ServiceConfigg = new ServiceConfig(fc);
        }
    }

    [MetadataType(typeof(QuoteMetadata))]
    public partial class Quote
    {
        public decimal OverallMarkupPercentAsPercent { get; set; }
        public decimal MarkupOverrideAsPercent { get; set; }

        //        public int MarkupOverrideAsInteger { get; set; }
        public string FormattedShippingAddress { get; set; }

        public string FormattedFreightShippingAddress { get; set; }

        public string OverallMarkupPercentAsFormattedPercent { get; set; }
        public string MarkupOverrideAsFormattedPercent { get; set; }
        public decimal TotalJobTime { get; set; }
        public string TotalJobTimeFormatted { get; set; }
        public string TruncatedDescription { get; set; }
        public List<QuoteParentItemTotals_Result> ItemTotals { get; set; }

        public async Task<string> GetFormattedFreightAddress(FifthPressEntities _db)
        {
            if (this.FreightAddress.HasValue)
            {
                await _db.Entry(this).Reference(r => r.FreightShippingAddress).LoadAsync();
                await _db.Entry(this.FreightShippingAddress).Reference(r => r.USState).LoadAsync();

                //extra handling for null city/state/zip
                if (FreightShippingAddress.City == null || FreightShippingAddress.State == null || FreightShippingAddress.Zip == null)
                    return "";
                else
                    return String.Format("{0}, {1} {2}", FreightShippingAddress.City, FreightShippingAddress.USState.Abbreviation, FreightShippingAddress.Zip);
            }
            else
            {
                return "";
            }
        }

        public async Task<string> GetFormattedShippingAddress(FifthPressEntities _db)
        {
            if (this.ShippingAddress.HasValue)
            {
                await _db.Entry(this).Reference(r => r.ShippingAddress1).LoadAsync();
                await _db.Entry(this.ShippingAddress1).Reference(r => r.USState).LoadAsync();

                //extra handling for null city/state/zip
                if (ShippingAddress1.City == null || ShippingAddress1.State == null || ShippingAddress1.Zip == null)
                    return "";
                else
                    return String.Format("{0}, {1} {2}", ShippingAddress1.City, ShippingAddress1.USState.Abbreviation, ShippingAddress1.Zip);
            }
            else
            {
                return "";
            }
        }

        public async Task CopyDocumentsToNewJob(int? jobId, FifthPressEntities _db)
        {
            // Base path to the storage directory
            string basePath = @"C:\Storage\mastermind_job_docs"; // Update with your actual base path

            // Ensure 'this' Quote is loaded with its QuoteItems
            await _db.Entry(this).Collection(c => c.QuoteItems).LoadAsync();

            // Find the Job by jobId
            Job j = await _db.Jobs.FindAsync(jobId);

            // Safety check
            if (j == null)
                throw new Exception("Job Not Found");

            // Load the JobItems related to the found Job
            await _db.Entry(j).Collection(c => c.JobItems).LoadAsync();

            foreach (QuoteItem qi in this.QuoteItems.ToList())
            {
                // Find the corresponding JobItem for the QuoteItem
                var ji = j.JobItems.FirstOrDefault(x => x.Ordinal == qi.Ordinal);

                if (ji == null)
                    continue; // Skip if no matching JobItem is found

                // Get all QuoteDocuments associated with the QuoteItem
                var documents = await _db.QuoteDocuments
                    .Where(x => x.ItemId == qi.Id && x.RowType == "Standard")
                    .ToListAsync();

                foreach (QuoteDocument qd in documents)
                {
                    string oldDocumentPath = qd.DocumentPath;
                    string fileName = Path.GetFileName(oldDocumentPath);

                    // Build new path using job number with underscores (matches AddDocument convention for jobs)
                    string jobFolder = j.JobNumber.Replace("-", "_");
                    string newDocumentPath = Path.Combine(basePath, jobFolder, fileName);

                    try
                    {
                        string sourceFilePath = oldDocumentPath;  // DocumentPath is already absolute
                        string destinationFilePath = newDocumentPath;

                        Directory.CreateDirectory(Path.GetDirectoryName(destinationFilePath));
                        File.Copy(sourceFilePath, destinationFilePath, overwrite: true);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Error copying file {qd.DocumentPath}: {ex.Message}");
                    }

                    // Create a new JobDocument based on the QuoteDocument
                    _db.JobDocuments.Add(new JobDocument
                    {
                        ItemId = ji.Id,
                        DocumentPath = newDocumentPath,
                        DocumentHash = qd.DocumentHash,
                        UploadDate = qd.UploadDate,
                        UploadedBy = qd.UploadedBy,
                        RowType = qd.RowType
                    });
                }
            }

            // Save changes to the database
            await _db.SaveChangesAsync();
        }

        public async Task CopyDocumentsToNewQuote(int newQuoteId, FifthPressEntities _db)
        {
            // Base path to the storage directory
            string basePath = @"C:\Storage\mastermind_job_docs"; // Update with your actual base path

            // 1. Ensure 'this' Quote is loaded with its QuoteItems
            await _db.Entry(this).Collection(c => c.QuoteItems).LoadAsync();

            // 2. Find the new Quote
            Quote newQuote = await _db.Quotes.FindAsync(newQuoteId);

            // Safety check
            if (newQuote == null)
                throw new Exception("New Quote Not Found");

            // 3. Load the QuoteItems for the new Quote
            await _db.Entry(newQuote).Collection(c => c.QuoteItems).LoadAsync();

            // Iterate over each QuoteItem in the "old" (current) Quote
            foreach (QuoteItem oldQuoteItem in this.QuoteItems.ToList())
            {
                // 4. Find the matching QuoteItem in the new Quote by Ordinal (change logic if needed)
                var newQuoteItem = newQuote.QuoteItems.FirstOrDefault(x => x.Ordinal == oldQuoteItem.Ordinal);
                if (newQuoteItem == null)
                    continue; // Skip if no matching item is found

                // 5. Get all QuoteDocuments associated with the old QuoteItem
                var documents = await _db.QuoteDocuments
                    .Where(x => x.ItemId == oldQuoteItem.Id && x.RowType == "Standard")
                    .ToListAsync();

                // For each document, copy the file and create a new QuoteDocument
                foreach (QuoteDocument oldDoc in documents)
                {
                    string oldDocumentPath = oldDoc.DocumentPath;
                    string fileName = Path.GetFileName(oldDocumentPath);

                    // Build new path using new quote's JobNumber (matches AddDocument convention for quotes)
                    string newDocumentPath = Path.Combine(basePath, newQuote.JobNumber, fileName);

                    try
                    {
                        string sourceFilePath = oldDocumentPath;  // DocumentPath is already absolute
                        string destinationFilePath = newDocumentPath;

                        Directory.CreateDirectory(Path.GetDirectoryName(destinationFilePath));

                        // Use FileStream with FileShare.ReadWrite for resilience against external locks
                        using (var sourceStream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var destStream = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            await sourceStream.CopyToAsync(destStream);
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Error copying file {oldDoc.DocumentPath}: {ex.Message}");
                    }

                    // 8. Create a new QuoteDocument (associated with the new quote's item)
                    _db.QuoteDocuments.Add(new QuoteDocument
                    {
                        ItemId = newQuoteItem.Id,
                        DocumentPath = newDocumentPath,
                        DocumentHash = oldDoc.DocumentHash,
                        UploadDate = oldDoc.UploadDate,
                        UploadedBy = oldDoc.UploadedBy,
                        RowType = oldDoc.RowType
                    });
                }
            }

            // 9. Save changes to the database
            await _db.SaveChangesAsync();
        }

        // Helper function to replace the first occurrence of a string
        public static string ReplaceFirstOccurrence(string source, string find, string replace)
        {
            int place = source.IndexOf(find);
            if (place == -1)
            {
                return source;
            }
            return source.Remove(place, find.Length).Insert(place, replace);
        }
    }

    [MetadataType(typeof(MaterialMetadata))]
    public partial class Material
    {
        public class MaterialConfig
        {
            public Dictionary<string, string> ConfigKeyValuePairs;

            public string GetConfigurationJson()
            {
                return JsonConvert.SerializeObject(ConfigKeyValuePairs, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
            }

            public string GetKeyValue(string key)
            {
                bool parsed = ConfigKeyValuePairs.TryGetValue(key, out string value);

                if (parsed)
                    return value;
                else
                    throw new Exception("Unable to parse that value or an item with that key does not exist");
            }

            public void SetKeyValue(string key, string value)
            {
                ConfigKeyValuePairs[key] = value;
            }

            public void AddKey(string key, string value)
            {
                //if ConfigKeyValuePairs already contains an entry with this key, throw an exception
                if (ConfigKeyValuePairs.ContainsKey(key))
                    throw new Exception("An item with that key already exists");
                else
                    ConfigKeyValuePairs.Add(key, value);
            }

            public void RemoveKey(string key)
            {
                ConfigKeyValuePairs.Remove(key);
            }

            public bool HasKey(string key)
            {
                return ConfigKeyValuePairs.ContainsKey(key);
            }

            public MaterialConfig(string theConfig)
            {
                if (theConfig == null)
                    ConfigKeyValuePairs = new Dictionary<string, string>();
                else
                    ConfigKeyValuePairs = JsonConvert.DeserializeObject<Dictionary<string, string>>(theConfig);
            }
        }

        public MaterialConfig MaterialConfigg { get; set; }
        public string ParsedLength { get; set; }
        public string ParsedWidth { get; set; }
        public string ConfigLabel { get; set; }
        public string ConfigValue { get; set; }

        public enum AllocationTypes
        {
            Sheet,
            NotSheet
        }

        public enum AvailableStockTypes
        {
            Sheet = 3,
            RollStock = 2,
            CutVinyl = 4
        }

        public enum MaterialCostTypes
        {
            Piece = 1,
            Hundred = 2,
            Thousand = 3,
            Foot = 4
        }

        public enum FormulaApplications
        {
            Multiply = 1,
            Divide = 2,
            Equal = 3,
            Lot = 4,
            MaterialSheets = 5
        }

        public Material(string parsedWidth)
        {
            this.ParsedWidth = parsedWidth;
        }

        public bool ParsedDimensions { get; set; }

        public void ParseDimensions()
        {
            if (this != null)
            {
                var size = this.Size;
                this.ParsedDimensions = false;

                if (size != null)
                {
                    try
                    {
                        string[] splitSize = size.Split('x');
                        ParsedLength = splitSize[0];
                        ParsedWidth = splitSize[1];
                        ParsedDimensions = true;
                    }
                    catch (Exception)
                    { }
                }
            }
        }

        public string GetLength()
        {
            if (!this.ParsedDimensions)
                this.ParseDimensions();

            return this.ParsedLength;
        }

        public string GetWidth()
        {
            if (!this.ParsedDimensions)
                this.ParseDimensions();

            return this.ParsedWidth;
        }
    }

    public partial class MaterialCategory
    {
        public enum MaterialTypes
        {
            Printing = 1,
            Finishing = 2,
            Supplies = 3
        }
    }

    [MetadataType(typeof(PressMetadata))]
    public partial class Press
    {
        public enum PressSpeedDenominator
        {
            SheetsPerHour = 1,
            RollsPerHour = 2,
            SqFtPerHour = 3,
            LinearFtPerHour = 4
        }

        public string InkCostFull { get; set; }
    }

    [MetadataType(typeof(QuoteItemServiceMetadata))]
    public partial class QuoteItemService
    {
        public void SetDefaults(Service s)
        {
            this.Caliper = s.Caliper;
            this.Operator = s.Operator;
            this.Deleted = null;
            this.Notes = s.Notes;
            this.HourlyRate = s.BasePrice;
            this.ServiceConfiguration = s.ServiceConfiguration;
            this.MakeReady = s.MakeReady;
            this.WashUp = s.WashUp;
            this.ServiceTime = 0m;
            this.MaterialCost = s.MaterialCost;
        }

        public class ServiceConfig
        {
            public Dictionary<string, string> ConfigKeyValuePairs { get; set; }

            public List<string> Keys { get; set; }

            public string ConfigName { get; set; }
            public string ConfigNameFriendly { get; set; }

            public string GetKeyValue(string key)
            {
                bool parsed = ConfigKeyValuePairs.TryGetValue(key, out string value);

                if (parsed)
                    return value;
                else
                    throw new Exception("Unable to parse that value or an item with that key does not exist");
            }

            public void SetKeyValue(string key, string value)
            {
                ConfigKeyValuePairs[key] = value;
            }

            public void AddKey(string key, string value)
            {
                if (ConfigKeyValuePairs.ContainsKey(key))
                    throw new Exception("An item with that key already exists");
                else
                    ConfigKeyValuePairs.Add(key, value);
            }

            public void RemoveKey(string key)
            {
                ConfigKeyValuePairs.Remove(key);
            }

            public bool HasKey(string key)
            {
                return ConfigKeyValuePairs.ContainsKey(key);
            }

            public ServiceConfig(string theConfig)
            {
                if (String.IsNullOrEmpty(theConfig))
                {
                    ConfigKeyValuePairs = new Dictionary<string, string>();
                }
                else
                {
                    JObject parsedObject = JObject.Parse(theConfig);
                    if (parsedObject.Count > 0)
                    {
                        // Get the first property in the JSON object
                        JProperty firstProperty = (JProperty)parsedObject.First;

                        // Set ConfigName and ConfigNameFriendly
                        ConfigName = firstProperty.Name;
                        ConfigNameFriendly = ConfigName.Replace("_", " ");

                        // Deserialize the inner JSON object to dictionary
                        ConfigKeyValuePairs = firstProperty.Value.ToObject<Dictionary<string, string>>();
                    }
                    else
                    {
                        ConfigKeyValuePairs = new Dictionary<string, string>();
                    }
                }

                Keys = new List<string>();

                foreach (KeyValuePair<string, string> kvp in ConfigKeyValuePairs)
                {
                    Keys.Add(kvp.Key);
                }
            }
        }

        public ServiceConfig ServiceConfigg { get; set; }
        public decimal WorkedTimeDecimalHours { get; set; }
        public string WorkedTimeFormatted { get; set; }

        public void ParseServiceConfiguration()
        {
            string fc = FormulaConfiguration ?? "";

            this.ServiceConfigg = new ServiceConfig(fc);
        }
    }

    public partial class QuoteItem
    {
        public string FormattedPrintTime { get; set; }

        public decimal MaterialLength { get; set; }
        public decimal MaterialWidth { get; set; }
        public decimal RunLength { get; set; }
        public decimal RunWidth { get; set; }
        public decimal FinishLength { get; set; }
        public decimal FinishWidth { get; set; }
        public string MaterialCostPerUnitFormatted { get; set; }
        public string MaterialCostFormatted { get; set; }
        public int DocumentCount { get; set; }

        public void SetRollStockLength(decimal length)
        {
            if (MaterialRunSize == null)
                MaterialRunSize = "0x0";

            string[] mrs = MaterialRunSize.Split('x');

            // Ensure we have at least 2 parts
            if (mrs.Length < 2)
            {
                MaterialRunSize = "0x0";
                mrs = MaterialRunSize.Split('x');
            }

            // Format the length to avoid locale issues and excessive decimal places
            mrs[0] = length.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
            MaterialRunSize = string.Join("x", mrs);
        }

        public void ParseRunSize()
        {
            string runSize = this.MaterialRunSize ?? "";

            if (runSize.Contains("x"))
            {
                string[] runSizeArray = runSize.Split('x');

                if (runSizeArray.Length == 2)
                {
                    var parsedLength = decimal.TryParse(runSizeArray[0], out decimal length);
                    bool parsedWidth = decimal.TryParse(runSizeArray[1], out decimal width);

                    if (parsedLength && parsedWidth)
                    {
                        this.RunLength = length;
                        this.RunWidth = width;
                    }
                    else
                    {
                        this.RunLength = 0m;
                        this.RunWidth = 0m;
                    }
                }
            }
        }

        //expects all foreign keys to be loaded upon being called
        public decimal CalculateMaterialCost(decimal? costOverride)
        {
            decimal matCost = 0m;

            if (costOverride.HasValue)
            {
                matCost = costOverride.Value;
            }

            decimal finalCost = 0m;

            if (Material.HasValue)
            {
                if (!costOverride.HasValue)
                {
                    matCost = Material1.Cost ?? 0m;
                }
                switch (Material1.StockType)
                {
                    case (int)FifthPress.Material.AvailableStockTypes.CutVinyl:
                    case (int)FifthPress.Material.AvailableStockTypes.RollStock:
                        decimal lengthInFeet = (RunLength + Decimal.Parse(Gripper ?? "0") + Decimal.Parse(Tail ?? "0")) / 12m;
                        finalCost = lengthInFeet * matCost;
                        break;

                    case (int)FifthPress.Material.AvailableStockTypes.Sheet:

                        int totalSheets = (Sheets ?? 0) + (ScrapSheets ?? 0);
                        switch (Material1.CostType)
                        {
                            case (int)FifthPress.Material.MaterialCostTypes.Piece:
                                finalCost = totalSheets * matCost;
                                break;

                            case (int)FifthPress.Material.MaterialCostTypes.Hundred:
                                finalCost = (totalSheets / 100m) * matCost;
                                break;

                            case (int)FifthPress.Material.MaterialCostTypes.Thousand:
                                finalCost = (totalSheets / 1000m) * matCost;
                                break;
                        }

                        break;
                }

                return finalCost;
            }

            return 0m;
        }
    }

    [MetadataType(typeof(ServiceMetadata))]
    public partial class Service
    {
        public class ServiceConfig
        {
            public Dictionary<string, Tuple<int, decimal>> ConfigKeyValuePairs;

            public string GetConfigurationJson()
            {
                return JsonConvert.SerializeObject(ConfigKeyValuePairs, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
            }

            public List<VM_TimingParameterEditor> GetTimeParametersForList()
            {
                List<VM_TimingParameterEditor> vmList = new List<VM_TimingParameterEditor>();

                foreach (var item in ConfigKeyValuePairs)
                    vmList.Add(new VM_TimingParameterEditor { ParameterNameCamelCase = item.Key, ParameterNameDisplay = StringExtensions.SplitCamelCase(item.Key), ParameterValue = item.Value.Item1.ToString(), ParameterValueUnits = item.Value.Item2 });

                return vmList;
            }

            public Tuple<int, decimal> GetKeyValue(string key)
            {
                bool parsed = ConfigKeyValuePairs.TryGetValue(key, out Tuple<int, decimal> value);

                if (parsed)
                    return value;
                else
                    throw new Exception("Unable to parse that value or an item with that key does not exist");
            }

            public void SetKeyValue(string key, Tuple<int, decimal> value)
            {
                ConfigKeyValuePairs[key] = value;
            }

            public void AddKey(string key, Tuple<int, decimal> value)
            {
                //if ConfigKeyValuePairs already contains an entry with this key, throw an exception
                if (ConfigKeyValuePairs.ContainsKey(key))
                    throw new Exception("An item with that key already exists");
                else
                    ConfigKeyValuePairs.Add(key, value);
            }

            public void RemoveKey(string key)
            {
                ConfigKeyValuePairs.Remove(key);
            }

            public bool HasKey(string key)
            {
                return ConfigKeyValuePairs.ContainsKey(key);
            }

            public ServiceConfig(string theConfig)
            {
                if (theConfig == null)
                    ConfigKeyValuePairs = new Dictionary<string, Tuple<int, decimal>>();
                else
                    ConfigKeyValuePairs = JsonConvert.DeserializeObject<Dictionary<string, Tuple<int, decimal>>>(theConfig);
            }
        }

        public ServiceConfig ServiceConfigg { get; set; }

        public void ParseServiceConfiguration()
        {
            _ = ServiceConfiguration ?? "";

            this.ServiceConfigg = new ServiceConfig(this.ServiceConfiguration);
        }

        public enum TypeOfService
        {
            Outsource = 4,
            Prepress = 1,
            Printing = 2,
            Finishing = 3
        }

        //uses serno as the value
        public enum SerNoSpecialServices
        {
            Print = 1,
            QualityAssurance = 2,
            ProofApproval = 3,
            FinishingService = 4,
            Zund = 1858, // not sure if Zund needs to be here any longer?
            PullMaterial = 9999
        }

        //uses serno as the value
        public enum ServicesWithMaterialCost
        {
            PressProofLessThan11x17 = 1899,
            PressProof11x17To20x28 = 1900,
            PressProofGreaterThan20x28 = 1901
        }
    }

    public partial class JobOutsourcedItem
    {
        public int DocumentCount { get; set; }
    }

    public partial class QuoteOutsourcedItem
    {
        public int DocumentCount { get; set; }
    }

    public partial class QuoteItemFinishingMaterial
    {
        public string ConfigLabel { get; set; }
        public string ConfigValue { get; set; }

        public Dictionary<string, string> ParseConfiguration(string theConfig)
        {
            Dictionary<string, string> ConfigTuple = JsonConvert.DeserializeObject<Dictionary<string, string>>(theConfig);
            ConfigLabel = ConfigTuple.Keys.First();
            ConfigValue = ConfigTuple.Values.First();

            return ConfigTuple;
        }
    }

    public partial class JobItemFinishingMaterial
    {
        public string ConfigLabel { get; set; }
        public string ConfigValue { get; set; }

        public Dictionary<string, string> ParseConfiguration(string theConfig)
        {
            Dictionary<string, string> ConfigTuple = JsonConvert.DeserializeObject<Dictionary<string, string>>(theConfig);
            ConfigLabel = ConfigTuple.Keys.First();
            ConfigValue = ConfigTuple.Values.First();

            return ConfigTuple;
        }
    }

    public partial class RecordSearch2_Result
    {
        public string CreatedAtString { get; set; }
        public string CompletedString { get; set; }
    }

    public partial class User
    {
        public string WelcomeMessage { get; set; }

        public List<Permission> PermissionsModel { get; set; }

        public string POPermission { get; set; }

        public void RetrievePermissions()
        {
            //expects the user and their role to be loaded
            string pString = this.Role1.PermissionString ?? "";

            PermissionsModel = new List<Permission>();

            //split pString at every 2 characters and add to PermissionsModel
            for (int i = 0; i < pString.Length / 2; i++)
            {
                PermissionsModel.Add(new Permission
                {
                    PermissionIdentifier = pString.Substring((i * 2), 2)
                });
            }
        }

        public string GetFullName()
        {
            return (this.FirstName ?? "") + " " + (this.LastName ?? "");
        }

        public void GetPOPermission(FifthPressEntities _db)
        {
            _db.Entry(this).Collection(r => r.POPermissionsPivots).Load();

            try
            {
                POPermission = POPermissionsPivots.Single(x => x.User == Id).Permission;
            }
            catch (Exception)
            {
                POPermission = "None";
            }
        }

        public bool HasApprovalPermissions()
        {
            switch (POPermission)
            {
                case "Approver":
                case "Special Approver":
                case "Admin":
                    return true;

                default:
                    return false;
            }
        }

        public bool HasDenialPermissions()
        {
            switch (POPermission)
            {
                case "Approver":
                case "Special Approver":
                case "Admin":
                    return true;

                default:
                    return false;
            }
        }

        public bool HasCancelPermissions()
        {
            switch (POPermission)
            {
                case "Approver":
                case "Special Approver":
                case "Admin":
                    return true;

                default:
                    return false;
            }
        }
    }

    public partial class JobStatus : IComparable<JobStatus>
    {
        public int CompareTo(JobStatus other)
        {
            return Id.CompareTo(other.Id);
        }
    }

    public partial class Client
    {
        public enum CustomerQuoteOptions
        {
            ParentItemTotals = 1,
            OnlyFinalPrice = 2,
            FullPricingBreakdown = 3
        }
    }

    public partial class DashboardCard
    {
        public string slug { get; set; }
    }

    [MetadataType(typeof(VendorMetadata))]
    public partial class Vendor
    { }
}