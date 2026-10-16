using FifthPress.ActionFilters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace FifthPress.Controllers
{
    // Read-only JSON endpoints for the Mastermind MCP server (dev only).
    // Deliberately not a DataController: no per-request side-menu queries and no inherited actions.
    // Nothing here calls SaveChanges; every query is AsNoTracking and bounded.
    // Disabled unless the McpApiEnvironment app setting is present and is not a production name.
    [CookieFilter(Order = 1)]
    public class McpApiController : Controller
    {
        private const string RecordLookupPermissionUrl = "RecordLookup"; // PermissionsManifest row "Record Lookup" (2d)
        private const string ReportsPermissionUrl = "reports"; // PermissionsManifest row "Reports" (1b)
        private const int MaxReportDays = 92;
        private const int MaxReportRows = 500;
        private static readonly string[] Statuses = { "In progress", "Ready to ship", "Ready to post", "Completed" };
        private const int MaxCandidateLimit = 200;
        private const int CandidateWindowDays = 365;
        private static readonly string[] Modes = { "production", "material", "services", "product", "customer" };

        [HttpGet]
        public async Task<ActionResult> GetJob(string jobNumber, CancellationToken cancellationToken)
        {
            using (var db = CreateContext())
            {
                var denied = await Authorize(db, RecordLookupPermissionUrl, cancellationToken);
                if (denied != null) return denied;
                if (!ValidJobNumber(jobNumber)) return Error(HttpStatusCode.BadRequest, "jobNumber must be 1-50 characters.");

                var matches = await FindJobIds(db, jobNumber.Trim(), cancellationToken);
                if (matches.Count > 1) return Error(HttpStatusCode.Conflict, "More than one non-deleted job has this number.");
                if (matches.Count == 0) return JsonReply(HttpStatusCode.NotFound, new { provenance = Provenance(db) });

                var jobs = await LoadJobs(db, matches, cancellationToken);
                return JsonReply(HttpStatusCode.OK, new { provenance = Provenance(db), job = jobs.Single() });
            }
        }

        [HttpGet]
        public async Task<ActionResult> GetSimilarCandidates(string jobNumber, string mode, bool includeCancelled = false, int maxCandidates = MaxCandidateLimit, CancellationToken cancellationToken = default(CancellationToken))
        {
            using (var db = CreateContext())
            {
                var denied = await Authorize(db, RecordLookupPermissionUrl, cancellationToken);
                if (denied != null) return denied;
                if (!ValidJobNumber(jobNumber)) return Error(HttpStatusCode.BadRequest, "jobNumber must be 1-50 characters.");
                if (!Modes.Contains(mode)) return Error(HttpStatusCode.BadRequest, "mode must be one of: " + string.Join(", ", Modes) + ".");
                maxCandidates = Math.Max(1, Math.Min(maxCandidates, MaxCandidateLimit));

                var matches = await FindJobIds(db, jobNumber.Trim(), cancellationToken);
                if (matches.Count > 1) return Error(HttpStatusCode.Conflict, "More than one non-deleted job has this number.");
                if (matches.Count == 0) return JsonReply(HttpStatusCode.NotFound, new { provenance = Provenance(db) });
                int sourceId = matches[0];
                var sourceJob = (await LoadJobs(db, new List<int> { sourceId }, cancellationToken)).Single();

                // Candidate scope: non-deleted jobs with a number, created in the last year, newest first, capped.
                // SQL only narrows by the mode's cheap keys; the MCP server applies the actual scoring rules.
                DateTime since = DateTime.Now.AddDays(-CandidateWindowDays);
                var query = db.Jobs.AsNoTracking().Where(j => j.Id != sourceId && j.Deleted == null
                    && j.JobNumber != null && j.JobNumber != "" && j.CreatedAt >= since);
                if (!includeCancelled) query = query.Where(j => j.Cancelled == null);

                var items = db.JobItems.AsNoTracking().Where(i => i.Job == sourceId && i.Deleted == null);
                var materialIds = await items.Where(i => i.Material > 0).Select(i => i.Material.Value).Distinct().ToListAsync(cancellationToken);
                var pressIds = await items.Where(i => i.Press > 0).Select(i => i.Press.Value).Distinct().ToListAsync(cancellationToken);
                var serviceIds = await db.JobItemServices.AsNoTracking()
                    .Where(s => s.JobItem.Job == sourceId && s.JobItem.Deleted == null && s.Deleted == null && s.ServiceId > 0)
                    .Select(s => s.ServiceId).Distinct().ToListAsync(cancellationToken);
                int? customerId = await db.Jobs.Where(j => j.Id == sourceId).Select(j => j.Client).SingleAsync(cancellationToken);

                string filter;
                switch (mode)
                {
                    case "customer":
                        query = customerId > 0 ? query.Where(j => j.Client == customerId) : query.Where(j => false);
                        filter = "same known customer ID";
                        break;
                    case "material":
                        query = query.Where(j => j.JobItems.Any(i => i.Deleted == null && materialIds.Contains(i.Material.Value)));
                        filter = "a non-deleted standard item with one of the source's material IDs";
                        break;
                    case "services":
                        query = query.Where(j => j.JobItems.Any(i => i.Deleted == null && i.JobItemServices.Any(s => s.Deleted == null && serviceIds.Contains(s.ServiceId))));
                        filter = "a non-deleted standard item sharing at least one of the source's service IDs";
                        break;
                    case "production":
                        query = query.Where(j => j.JobItems.Any(i => i.Deleted == null && (materialIds.Contains(i.Material.Value) || pressIds.Contains(i.Press.Value)
                            || i.JobItemServices.Any(s => s.Deleted == null && serviceIds.Contains(s.ServiceId)))));
                        filter = "a non-deleted standard item sharing a material, press or service ID with the source (dimension-only or quantity-only similarity is not searched)";
                        break;
                    default:
                        filter = "no pre-filter (product wording is compared by the MCP server)";
                        break;
                }

                int inScope = await query.CountAsync(cancellationToken);
                var ids = await query.OrderByDescending(j => j.CreatedAt).ThenByDescending(j => j.Id)
                    .Select(j => j.Id).Take(maxCandidates).ToListAsync(cancellationToken);
                var candidates = await LoadJobs(db, ids, cancellationToken);

                return JsonReply(HttpStatusCode.OK, new
                {
                    provenance = Provenance(db),
                    source = sourceJob,
                    scope = new
                    {
                        description = string.Format("Non-deleted jobs with a job number, created on or after {0:yyyy-MM-dd} (server local time; jobs without a creation date are excluded), {1}, matching {2}. Newest first, at most {3}.",
                            since, includeCancelled ? "cancelled jobs included" : "cancelled jobs excluded", filter, maxCandidates),
                        createdOnOrAfter = since,
                        maxCandidates,
                        candidatesInScope = inScope,
                        candidatesReturned = candidates.Count,
                        truncated = inScope > candidates.Count,
                        authorization = "Requires the Record Lookup permission. Legacy Mastermind has no per-client or per-job restriction for staff, so this permission covers every job returned."
                    },
                    candidates
                });
            }
        }

        // Report: jobs created in a date range. Agreed definition: Jobs.CreatedAt (server local wall-clock time),
        // inclusive dates, at most 92 days and 500 rows, Reports permission. Quantities are listed per item, never summed.
        [HttpGet]
        public async Task<ActionResult> GetJobsCreatedReport(string createdFrom, string createdTo, int? customerId = null, string status = null,
            bool includeCancelled = false, int maxRows = MaxReportRows, CancellationToken cancellationToken = default(CancellationToken))
        {
            using (var db = CreateContext())
            {
                var denied = await Authorize(db, ReportsPermissionUrl, cancellationToken);
                if (denied != null) return denied;
                DateTime fromDate, toDate;
                if (!DateTime.TryParseExact(createdFrom, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out fromDate)
                    || !DateTime.TryParseExact(createdTo, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out toDate))
                    return Error(HttpStatusCode.BadRequest, "createdFrom and createdTo must be yyyy-MM-dd dates.");
                if (toDate < fromDate || (toDate - fromDate).TotalDays + 1 > MaxReportDays)
                    return Error(HttpStatusCode.BadRequest, "The inclusive date range must be 1 to " + MaxReportDays + " days.");
                if (status != null && !Statuses.Contains(status))
                    return Error(HttpStatusCode.BadRequest, "status must be one of: " + string.Join(", ", Statuses) + ".");
                if (customerId != null && customerId <= 0) return Error(HttpStatusCode.BadRequest, "customerId must be positive.");
                maxRows = Math.Max(1, Math.Min(maxRows, MaxReportRows));

                DateTime toExclusive = toDate.AddDays(1);
                var query = db.Jobs.AsNoTracking().Where(j => j.Deleted == null && j.JobNumber != null && j.JobNumber != ""
                    && j.CreatedAt >= fromDate && j.CreatedAt < toExclusive);
                if (!includeCancelled) query = query.Where(j => j.Cancelled == null);
                if (customerId != null) query = query.Where(j => j.Client == customerId);
                // Same precedence as Job.SetCurrentJobStatus: Billed, then Shipped, then Complete.
                switch (status)
                {
                    case "Completed": query = query.Where(j => j.Billed != null); break;
                    case "Ready to post": query = query.Where(j => j.Billed == null && j.Shipped != null); break;
                    case "Ready to ship": query = query.Where(j => j.Billed == null && j.Shipped == null && j.Complete != null); break;
                    case "In progress": query = query.Where(j => j.Billed == null && j.Shipped == null && j.Complete == null); break;
                }

                int total = await query.CountAsync(cancellationToken);
                var rows = await query.OrderBy(j => j.CreatedAt).ThenBy(j => j.Id).Take(maxRows).Select(j => new
                {
                    j.Id, j.JobNumber, j.Title, j.Description, j.Client, j.CreatedAt, j.DueDate, j.InHandDate,
                    j.Complete, j.Shipped, j.ReleasedToBillingDate, j.Billed, j.Cancelled,
                    Items = j.JobItems.Where(i => i.Deleted == null).OrderBy(i => i.Ordinal).ThenBy(i => i.Id)
                        .Select(i => new { i.Id, i.Quantity, i.QuantityBy, i.TotalQuantity }),
                    OutsourcedItemCount = j.JobOutsourcedItems.Count()
                }).ToListAsync(cancellationToken);

                return JsonReply(HttpStatusCode.OK, new
                {
                    provenance = Provenance(db),
                    definition = new
                    {
                        report = "jobs-created",
                        dateField = "Jobs.CreatedAt, stored as server local wall-clock time without a timezone",
                        createdFrom = createdFrom, createdTo = createdTo, inclusive = true,
                        recordScope = "Non-deleted jobs with a job number" + (includeCancelled ? "; cancelled jobs included and flagged" : "; cancelled jobs excluded"),
                        customerId, status,
                        statusDefinition = "Legacy Job.SetCurrentJobStatus timestamp precedence (Billed > Shipped > Complete); cancellation is separate.",
                        quantitySemantics = "Quantity, QuantityBy and TotalQuantity per non-deleted standard item, unchanged and not summed.",
                        excluded = "Prices, costs, notes, contacts and addresses."
                    },
                    totalJobs = total,
                    rowsReturned = rows.Count,
                    truncated = total > rows.Count,
                    rows = rows.Select(j =>
                    {
                        var stage = new Job { Complete = j.Complete, Shipped = j.Shipped, Billed = j.Billed };
                        stage.SetCurrentJobStatus();
                        return new
                        {
                            legacyId = j.Id, jobNumber = j.JobNumber, title = j.Title, description = j.Description, customerId = j.Client,
                            status = StatusText(stage.OverallJobStage), isCancelled = j.Cancelled != null,
                            createdAt = j.CreatedAt, dueDate = j.DueDate, inHandDate = j.InHandDate, productionCompletedAt = j.Complete,
                            shippedAt = j.Shipped, releasedToBillingAt = j.ReleasedToBillingDate, billedAt = j.Billed, cancelledAt = j.Cancelled,
                            items = j.Items.Select(i => new { legacyId = i.Id, quantity = i.Quantity, quantityBy = i.QuantityBy, totalQuantity = i.TotalQuantity }),
                            outsourcedItemCount = j.OutsourcedItemCount
                        };
                    })
                });
            }
        }

        private static FifthPressEntities CreateContext()
        {
            var db = new FifthPressEntities();
            db.Configuration.LazyLoadingEnabled = false;
            db.Configuration.ProxyCreationEnabled = false;
            db.Database.CommandTimeout = 15;
            return db;
        }

        private static string ConfiguredEnvironment()
        {
            string value = (ConfigurationManager.AppSettings["McpApiEnvironment"] ?? "").Trim();
            return value == "" || new[] { "prod", "production", "live" }.Contains(value.ToLowerInvariant()) ? null : value;
        }

        // Same identity and permission rules the app's pages use, but answered as JSON instead of a redirect or HTML page.
        private async Task<ActionResult> Authorize(FifthPressEntities db, string permissionUrl, CancellationToken cancellationToken)
        {
            if (ConfiguredEnvironment() == null) return Error(HttpStatusCode.ServiceUnavailable, "The MCP API is not enabled in this environment.");
            var cookie = Request.Cookies["ActiveUser"];
            int userId;
            if (cookie == null || !int.TryParse(cookie.Value, out userId)) return Error(HttpStatusCode.Unauthorized, "Not signed in.");

            var user = await db.Users.AsNoTracking().Where(u => u.Id == userId && u.Disabled == false)
                .Select(u => new { u.Role1.SuperAdmin, u.Role1.PermissionString }).SingleOrDefaultAsync(cancellationToken);
            if (user == null) return Error(HttpStatusCode.Unauthorized, "Not signed in.");
            if (user.SuperAdmin) return null;

            // Fail closed: unlike the page filter, a missing manifest row denies access.
            var permission = await db.PermissionsManifests.AsNoTracking().Where(p => p.Url == permissionUrl)
                .Select(p => new { p.Title, p.PermissionId }).FirstOrDefaultAsync(cancellationToken);
            if (permission == null || string.IsNullOrEmpty(permission.PermissionId) || !(user.PermissionString ?? "").Contains(permission.PermissionId))
                return Error(HttpStatusCode.Forbidden, "This user's role does not have the " + (permission != null ? permission.Title : permissionUrl) + " permission.");
            return null;
        }

        private static bool ValidJobNumber(string jobNumber)
        {
            return !string.IsNullOrWhiteSpace(jobNumber) && jobNumber.Trim().Length <= 50;
        }

        // Returns up to two IDs so an ambiguous job number is reported instead of silently picking one.
        private static Task<List<int>> FindJobIds(FifthPressEntities db, string jobNumber, CancellationToken cancellationToken)
        {
            return db.Jobs.AsNoTracking().Where(j => j.JobNumber == jobNumber && j.Deleted == null)
                .Select(j => j.Id).Take(2).ToListAsync(cancellationToken);
        }

        // Explicit DTOs in the snapshot export's shape. No prices, costs, notes, contacts or addresses.
        // Stored values (quantities, dimension text, nulls) are passed through unchanged; empty collections are null, as in the export.
        private static async Task<List<object>> LoadJobs(FifthPressEntities db, List<int> ids, CancellationToken cancellationToken)
        {
            var rows = await db.Jobs.AsNoTracking().Where(j => ids.Contains(j.Id)).Select(j => new
            {
                j.Id, j.JobNumber, j.Title, j.Description, j.Client, j.JobStatus_Id, j.CreatedAt, j.DueDate, j.InHandDate,
                j.Complete, j.Shipped, j.ReleasedToBillingDate, j.Billed, j.Cancelled,
                Items = j.JobItems.Where(i => i.Deleted == null).Select(i => new
                {
                    i.Id, i.Ordinal, i.SubitemOf, i.Title, i.Description, i.Quantity, i.QuantityBy, i.TotalQuantity, i.Width, i.Length,
                    i.Material, MaterialTitle = i.Material1.Title, MaterialFriendlyName = i.Material1.FriendlyName,
                    i.Press, PressTitle = i.Press1.Title, i.PressColorProfileFront, i.PressColorCountFront,
                    i.PressColorProfileBack, i.PressColorCountBack, i.Collate, i.Staple, i.Numbering,
                    Services = i.JobItemServices.Where(s => s.Deleted == null)
                        .Select(s => new { s.Id, s.ServiceId, s.Ordinal, s.Title, s.FriendlyName, s.IsFinishingService, s.IsCuttingService })
                }),
                Outsourced = j.JobOutsourcedItems.Select(o => new { o.Id, o.Ordinal, o.SubitemOf, o.Description, o.Quantity, o.Vendor })
            }).ToListAsync(cancellationToken);

            return rows.OrderBy(r => ids.IndexOf(r.Id)).Select(j =>
            {
                // Reuse the legacy timestamp precedence; SetCurrentJobStatus only sets an unmapped property.
                var stage = new Job { Complete = j.Complete, Shipped = j.Shipped, Billed = j.Billed };
                stage.SetCurrentJobStatus();
                var items = j.Items.OrderBy(i => i.Ordinal).ThenBy(i => i.Id).Select(i => (object)new
                {
                    itemType = "standard", legacyId = i.Id, ordinal = i.Ordinal, parentStandardItemId = i.SubitemOf,
                    title = i.Title, description = i.Description, quantity = i.Quantity, quantityBy = i.QuantityBy, totalQuantity = i.TotalQuantity,
                    widthInchesText = i.Width, lengthInchesText = i.Length,
                    materialId = i.Material, materialTitle = i.MaterialTitle, materialFriendlyName = i.MaterialFriendlyName,
                    pressId = i.Press, pressTitle = i.PressTitle,
                    colorProfileFront = i.PressColorProfileFront, colorCountFront = i.PressColorCountFront,
                    colorProfileBack = i.PressColorProfileBack, colorCountBack = i.PressColorCountBack,
                    collate = i.Collate, staple = i.Staple, numbering = i.Numbering,
                    services = NullIfEmpty(i.Services.OrderBy(s => s.Ordinal).ThenBy(s => s.Id).Select(s => (object)new
                    {
                        legacyId = s.Id, serviceId = s.ServiceId, ordinal = s.Ordinal, title = s.Title, friendlyName = s.FriendlyName,
                        isFinishingService = s.IsFinishingService, isCuttingService = s.IsCuttingService
                    }))
                });
                var outsourced = j.Outsourced.OrderBy(o => o.Ordinal).ThenBy(o => o.Id).Select(o => (object)new
                {
                    itemType = "outsourced", legacyId = o.Id, ordinal = o.Ordinal, parentStandardItemId = o.SubitemOf,
                    description = o.Description, quantity = o.Quantity, vendorId = o.Vendor
                });
                return (object)new
                {
                    legacyId = j.Id, jobNumber = j.JobNumber, title = j.Title, description = j.Description, customerId = j.Client,
                    status = StatusText(stage.OverallJobStage), legacyStoredStatusId = j.JobStatus_Id, isCancelled = j.Cancelled != null,
                    createdAt = j.CreatedAt, dueDate = j.DueDate, inHandDate = j.InHandDate, productionCompletedAt = j.Complete,
                    shippedAt = j.Shipped, releasedToBillingAt = j.ReleasedToBillingDate, billedAt = j.Billed, cancelledAt = j.Cancelled,
                    items = NullIfEmpty(items), outsourcedItems = NullIfEmpty(outsourced)
                };
            }).ToList();
        }

        private static List<object> NullIfEmpty(IEnumerable<object> values)
        {
            var list = values.ToList();
            return list.Count == 0 ? null : list;
        }

        private static string StatusText(JobStage stage)
        {
            switch (stage)
            {
                case JobStage.ReadyToShip: return "Ready to ship";
                case JobStage.ReadyToPost: return "Ready to post";
                case JobStage.Completed: return "Completed";
                default: return "In progress";
            }
        }

        private static object Provenance(FifthPressEntities db)
        {
            string database = db.Database.Connection.Database;
            return new
            {
                dataSource = "mastermind-application",
                environment = ConfiguredEnvironment(),
                databaseName = database,
                retrievedAtUtc = DateTime.UtcNow.ToString("o"),
                freshness = "Read at request time from the " + database + " database configured for this environment. That database may be a mirror; its currency relative to production is not established by this response."
            };
        }

        private ActionResult Error(HttpStatusCode status, string message)
        {
            return JsonReply(status, new { message });
        }

        private ActionResult JsonReply(HttpStatusCode status, object body)
        {
            Response.StatusCode = (int)status;
            Response.TrySkipIisCustomErrors = true;
            return Content(JsonConvert.SerializeObject(body), "application/json");
        }
    }
}
