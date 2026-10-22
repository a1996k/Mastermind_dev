using FifthPress.ActionFilters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
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
        private const int CandidateWindowDays = 730; // 2 years, so last year's seasonal and repeat orders stay in range
        private const int MaxIgnoredServices = 20;
        private const int MaxSearchRows = 50;
        private const int MaxSearchDays = 730;
        private const int MaxLineageJobs = 10;
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
        public async Task<ActionResult> GetSimilarCandidates(string jobNumber, string mode, bool includeCancelled = false, string ignoreServiceIds = null,
            string ignoreServiceCategoryIds = null, string keepServiceIds = null,
            int maxCandidates = MaxCandidateLimit, CancellationToken cancellationToken = default(CancellationToken))
        {
            using (var db = CreateContext())
            {
                var denied = await Authorize(db, RecordLookupPermissionUrl, cancellationToken);
                if (denied != null) return denied;
                if (!ValidJobNumber(jobNumber)) return Error(HttpStatusCode.BadRequest, "jobNumber must be 1-50 characters.");
                if (!Modes.Contains(mode)) return Error(HttpStatusCode.BadRequest, "mode must be one of: " + string.Join(", ", Modes) + ".");
                maxCandidates = Math.Max(1, Math.Min(maxCandidates, MaxCandidateLimit));
                // Generic services named by the MCP matching rules (e.g. "Outsource", or whole workflow categories such as
                // Pre-Press, except kept services) must not pull jobs into the candidate set.
                var ignored = ParseIds(ignoreServiceIds);
                var ignoredCategories = ParseIds(ignoreServiceCategoryIds);
                var kept = ParseIds(keepServiceIds);
                if (ignored == null || ignoredCategories == null || kept == null)
                    return Error(HttpStatusCode.BadRequest, "ignoreServiceIds, ignoreServiceCategoryIds and keepServiceIds must each be at most " + MaxIgnoredServices + " comma-separated positive integers.");

                var matches = await FindJobIds(db, jobNumber.Trim(), cancellationToken);
                if (matches.Count > 1) return Error(HttpStatusCode.Conflict, "More than one non-deleted job has this number.");
                if (matches.Count == 0) return JsonReply(HttpStatusCode.NotFound, new { provenance = Provenance(db) });
                int sourceId = matches[0];
                var sourceJob = (await LoadJobs(db, new List<int> { sourceId }, cancellationToken)).Single();

                // Candidate scope: non-deleted jobs with a number, created in the last two years, most relevant first, capped.
                // SQL only narrows and orders by cheap estimates; the MCP server applies the actual scoring rules.
                DateTime since = DateTime.Now.AddDays(-CandidateWindowDays);
                var query = db.Jobs.AsNoTracking().Where(j => j.Id != sourceId && j.Deleted == null
                    && j.JobNumber != null && j.JobNumber != "" && j.CreatedAt >= since);
                if (!includeCancelled) query = query.Where(j => j.Cancelled == null);

                var items = db.JobItems.AsNoTracking().Where(i => i.Job == sourceId && i.Deleted == null);
                var materialIds = await items.Where(i => i.Material > 0).Select(i => i.Material.Value).Distinct().ToListAsync(cancellationToken);
                var pressIds = await items.Where(i => i.Press > 0).Select(i => i.Press.Value).Distinct().ToListAsync(cancellationToken);
                var serviceIds = await db.JobItemServices.AsNoTracking()
                    .Where(s => s.JobItem.Job == sourceId && s.JobItem.Deleted == null && s.Deleted == null && s.ServiceId > 0 && !ignored.Contains(s.ServiceId)
                        && (kept.Contains(s.ServiceId) || s.Service.CatNo == null || !ignoredCategories.Contains(s.Service.CatNo.Value)))
                    .Select(s => s.ServiceId).Distinct().ToListAsync(cancellationToken);
                int? customerId = await db.Jobs.Where(j => j.Id == sourceId).Select(j => j.Client).SingleAsync(cancellationToken);
                // Exact size text of the source's items in either orientation, and its most distinctive description words,
                // for the relevance estimates below.
                var sizes = await items.Where(i => i.Width != null && i.Length != null).Select(i => new { i.Width, i.Length }).ToListAsync(cancellationToken);
                var sizeKeys = sizes.Select(s => new { W = s.Width.Trim(), L = s.Length.Trim() })
                    .Where(s => s.W != "" && s.L != "" && s.W != "0" && s.L != "0")
                    .SelectMany(s => new[] { s.W + "x" + s.L, s.L + "x" + s.W }).Distinct().ToList();
                var sourceText = await db.Jobs.Where(j => j.Id == sourceId).Select(j => new { j.Title, j.Description }).SingleAsync(cancellationToken);
                var words = RelevanceWords(sourceText.Title + " " + sourceText.Description);

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
                if ((mode == "services" || mode == "production") && ignored.Count + ignoredCategories.Count > 0)
                    filter += "; generic services are ignored (service IDs " + string.Join(", ", ignored) + "; service categories " + string.Join(", ", ignoredCategories)
                        + (kept.Count > 0 ? " except service IDs " + string.Join(", ", kept) : "") + ")";

                // When more jobs are in scope than the cap, keep the ones most likely to score well rather than the newest:
                // a cheap SQL estimate of the MCP's rules, newest first among equals. The MCP server still scores every candidate exactly.
                int inScope = await query.CountAsync(cancellationToken);
                IQueryable<int> ordered;
                string orderedBy;
                if (mode == "customer" || mode == "product")
                {
                    string w0 = WordOrNone(words, 0), w1 = WordOrNone(words, 1), w2 = WordOrNone(words, 2), w3 = WordOrNone(words, 3),
                        w4 = WordOrNone(words, 4), w5 = WordOrNone(words, 5), w6 = WordOrNone(words, 6), w7 = WordOrNone(words, 7);
                    ordered = query.Select(j => new
                    {
                        j.Id, j.CreatedAt,
                        Relevance = (j.Description.Contains(w0) || j.Title.Contains(w0) ? 1 : 0) + (j.Description.Contains(w1) || j.Title.Contains(w1) ? 1 : 0)
                            + (j.Description.Contains(w2) || j.Title.Contains(w2) ? 1 : 0) + (j.Description.Contains(w3) || j.Title.Contains(w3) ? 1 : 0)
                            + (j.Description.Contains(w4) || j.Title.Contains(w4) ? 1 : 0) + (j.Description.Contains(w5) || j.Title.Contains(w5) ? 1 : 0)
                            + (j.Description.Contains(w6) || j.Title.Contains(w6) ? 1 : 0) + (j.Description.Contains(w7) || j.Title.Contains(w7) ? 1 : 0)
                    }).OrderByDescending(x => x.Relevance).ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Select(x => x.Id);
                    orderedBy = words.Count == 0 ? "newest first (the source has no usable description words)"
                        : "most of the source's description words (" + string.Join(", ", words) + ") found in the job title or description, then newest";
                }
                else
                {
                    int serviceCount = Math.Max(1, serviceIds.Count);
                    ordered = query.Select(j => new
                    {
                        j.Id, j.CreatedAt,
                        Relevance = (j.JobItems.Any(i => i.Deleted == null && materialIds.Contains(i.Material.Value)) ? 30 : 0)
                            + (j.JobItems.Any(i => i.Deleted == null && pressIds.Contains(i.Press.Value)) ? 15 : 0)
                            + (j.JobItems.Any(i => i.Deleted == null && sizeKeys.Contains(i.Width.Trim() + "x" + i.Length.Trim())) ? 25 : 0)
                            + j.JobItems.Where(i => i.Deleted == null).SelectMany(i => i.JobItemServices)
                                .Where(s => s.Deleted == null && serviceIds.Contains(s.ServiceId)).Select(s => s.ServiceId).Distinct().Count() * 15 / serviceCount
                    }).OrderByDescending(x => x.Relevance).ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Select(x => x.Id);
                    orderedBy = "estimated production relevance (shared material 30, press 15, exact size text 25, shared non-generic services up to 15), then newest";
                }
                var ids = await ordered.Take(maxCandidates).ToListAsync(cancellationToken);
                var candidates = await LoadJobs(db, ids, cancellationToken);

                return JsonReply(HttpStatusCode.OK, new
                {
                    provenance = Provenance(db),
                    source = sourceJob,
                    scope = new
                    {
                        description = string.Format("Non-deleted jobs with a job number, created on or after {0:yyyy-MM-dd} (server local time; jobs without a creation date are excluded), {1}, matching {2}. Ordered by {4}; at most {3}.",
                            since, includeCancelled ? "cancelled jobs included" : "cancelled jobs excluded", filter, maxCandidates, orderedBy),
                        orderedBy,
                        createdOnOrAfter = since,
                        windowDays = CandidateWindowDays,
                        ignoredServiceIds = ignored,
                        ignoredServiceCategoryIds = ignoredCategories,
                        keptServiceIds = kept,
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
                query = WhereStatus(query, status);

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

        // Search: jobs matching filters, newest first by Jobs.CreatedAt. Bounded to a 2-year range and 50 rows.
        [HttpGet]
        public async Task<ActionResult> SearchJobs(int? materialId = null, string sameMaterialAsJob = null, int? customerId = null,
            string createdFrom = null, string createdTo = null, string status = null, bool includeCancelled = false,
            int maxRows = 20, CancellationToken cancellationToken = default(CancellationToken))
        {
            using (var db = CreateContext())
            {
                var denied = await Authorize(db, RecordLookupPermissionUrl, cancellationToken);
                if (denied != null) return denied;
                if (materialId != null && !string.IsNullOrWhiteSpace(sameMaterialAsJob))
                    return Error(HttpStatusCode.BadRequest, "Use materialId or sameMaterialAsJob, not both.");
                if (materialId != null && materialId <= 0) return Error(HttpStatusCode.BadRequest, "materialId must be positive.");
                if (customerId != null && customerId <= 0) return Error(HttpStatusCode.BadRequest, "customerId must be positive.");
                if (status != null && !Statuses.Contains(status))
                    return Error(HttpStatusCode.BadRequest, "status must be one of: " + string.Join(", ", Statuses) + ".");
                DateTime toDate = DateTime.Today, fromDate;
                if (createdTo != null && !DateTime.TryParseExact(createdTo, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out toDate))
                    return Error(HttpStatusCode.BadRequest, "createdTo must be a yyyy-MM-dd date.");
                fromDate = toDate.AddDays(-MaxSearchDays);
                if (createdFrom != null && !DateTime.TryParseExact(createdFrom, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out fromDate))
                    return Error(HttpStatusCode.BadRequest, "createdFrom must be a yyyy-MM-dd date.");
                if (toDate < fromDate || (toDate - fromDate).TotalDays > MaxSearchDays)
                    return Error(HttpStatusCode.BadRequest, "The created date range must run forwards and span at most " + MaxSearchDays + " days.");
                maxRows = Math.Max(1, Math.Min(maxRows, MaxSearchRows));

                // "Same material as job X" means any material on that job's non-deleted items; the job itself is left out.
                var materialIds = new List<int>();
                int? excludedJobId = null;
                if (materialId != null) materialIds.Add(materialId.Value);
                if (!string.IsNullOrWhiteSpace(sameMaterialAsJob))
                {
                    if (!ValidJobNumber(sameMaterialAsJob)) return Error(HttpStatusCode.BadRequest, "sameMaterialAsJob must be 1-50 characters.");
                    var matches = await FindJobIds(db, sameMaterialAsJob.Trim(), cancellationToken);
                    if (matches.Count > 1) return Error(HttpStatusCode.Conflict, "More than one non-deleted job has this number.");
                    if (matches.Count == 0) return JsonReply(HttpStatusCode.NotFound, new { provenance = Provenance(db) });
                    int sourceId = matches[0];
                    excludedJobId = sourceId;
                    materialIds = await db.JobItems.AsNoTracking().Where(i => i.Job == sourceId && i.Deleted == null && i.Material > 0)
                        .Select(i => i.Material.Value).Distinct().ToListAsync(cancellationToken);
                    if (materialIds.Count == 0)
                        return JsonReply(HttpStatusCode.OK, new
                        {
                            provenance = Provenance(db), filters = new { sameMaterialAsJob = sameMaterialAsJob.Trim() },
                            totalJobs = 0, rowsReturned = 0, truncated = false, rows = new object[0],
                            message = "Job " + sameMaterialAsJob.Trim() + " has no known material on its items, so there is nothing to match."
                        });
                }

                DateTime toExclusive = toDate.AddDays(1);
                var query = db.Jobs.AsNoTracking().Where(j => j.Deleted == null && j.JobNumber != null && j.JobNumber != ""
                    && j.CreatedAt >= fromDate && j.CreatedAt < toExclusive);
                if (excludedJobId != null) { int excluded = excludedJobId.Value; query = query.Where(j => j.Id != excluded); }
                if (!includeCancelled) query = query.Where(j => j.Cancelled == null);
                if (customerId != null) query = query.Where(j => j.Client == customerId);
                if (materialIds.Count > 0) query = query.Where(j => j.JobItems.Any(i => i.Deleted == null && materialIds.Contains(i.Material.Value)));
                query = WhereStatus(query, status);

                bool byMaterial = materialIds.Count > 0;
                int total = await query.CountAsync(cancellationToken);
                var rows = await query.OrderByDescending(j => j.CreatedAt).ThenByDescending(j => j.Id).Take(maxRows).Select(j => new
                {
                    j.Id, j.JobNumber, j.Description, j.Client, j.CreatedAt, j.DueDate, j.Complete, j.Shipped, j.Billed, j.Cancelled,
                    ItemCount = j.JobItems.Count(i => i.Deleted == null),
                    // With a material filter, only the matching items; otherwise the first items.
                    Items = j.JobItems.Where(i => i.Deleted == null && (!byMaterial || materialIds.Contains(i.Material.Value)))
                        .OrderBy(i => i.Ordinal).ThenBy(i => i.Id).Take(5)
                        .Select(i => new { i.Id, i.Material, MaterialTitle = i.Material1.Title, i.Width, i.Length, i.Quantity, i.QuantityBy, i.TotalQuantity, PressTitle = i.Press1.Title })
                }).ToListAsync(cancellationToken);
                var materials = await db.Materials.AsNoTracking().Where(m => materialIds.Contains(m.Id))
                    .Select(m => new { materialId = m.Id, title = m.Title, friendlyName = m.FriendlyName }).ToListAsync(cancellationToken);

                return JsonReply(HttpStatusCode.OK, new
                {
                    provenance = Provenance(db),
                    filters = new
                    {
                        materials, sameMaterialAsJob = string.IsNullOrWhiteSpace(sameMaterialAsJob) ? null : sameMaterialAsJob.Trim(),
                        customerId, createdFrom = fromDate.ToString("yyyy-MM-dd"), createdTo = toDate.ToString("yyyy-MM-dd"), status, includeCancelled,
                        recordScope = "Non-deleted jobs with a job number" + (excludedJobId != null ? ", excluding the job whose materials were used" : "")
                            + (includeCancelled ? "; cancelled jobs included and flagged" : "; cancelled jobs excluded"),
                        order = "Newest first by Jobs.CreatedAt (server local time)"
                    },
                    totalJobs = total,
                    rowsReturned = rows.Count,
                    truncated = total > rows.Count,
                    rows = rows.Select(j => new
                    {
                        legacyId = j.Id, jobNumber = j.JobNumber, description = j.Description, customerId = j.Client,
                        status = StatusOf(j.Complete, j.Shipped, j.Billed), isCancelled = j.Cancelled != null,
                        createdAt = j.CreatedAt, dueDate = j.DueDate, itemCount = j.ItemCount,
                        items = j.Items.Select(i => new
                        {
                            legacyId = i.Id, materialId = i.Material, materialTitle = i.MaterialTitle, widthInchesText = i.Width, lengthInchesText = i.Length,
                            quantity = i.Quantity, quantityBy = i.QuantityBy, totalQuantity = i.TotalQuantity, pressTitle = i.PressTitle
                        })
                    })
                });
            }
        }

        // Material lookup by name, stock code or category (every word must match; spaces ignored, so "6mm" finds "6 mm"),
        // with recent usage so look-alike materials can be told apart. No costs or inventory.
        [HttpGet]
        public async Task<ActionResult> FindMaterials(string query, int maxRows = 20, CancellationToken cancellationToken = default(CancellationToken))
        {
            using (var db = CreateContext())
            {
                var denied = await Authorize(db, RecordLookupPermissionUrl, cancellationToken);
                if (denied != null) return denied;
                var words = (query ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Take(5).ToList();
                if (words.Count == 0 || query.Trim().Length > 50) return Error(HttpStatusCode.BadRequest, "query must be 1-50 characters.");
                maxRows = Math.Max(1, Math.Min(maxRows, MaxSearchRows));

                var materials = db.Materials.AsNoTracking().AsQueryable();
                foreach (var word in words)
                {
                    string w = word;
                    materials = materials.Where(m => ((m.Title ?? "") + "|" + (m.FriendlyName ?? "") + "|" + (m.MaterialCategory.Title ?? "")).Replace(" ", "").Contains(w));
                }
                DateTime since = DateTime.Now.AddDays(-CandidateWindowDays);
                int total = await materials.CountAsync(cancellationToken);
                var rows = await materials.Select(m => new
                {
                    m.Id, m.Title, m.FriendlyName, m.MaterialCategory_Id, CategoryTitle = m.MaterialCategory.Title, Retired = m.Deleted != null,
                    JobsLast2Years = m.JobItems.Where(i => i.Deleted == null && i.Job1.Deleted == null && i.Job1.CreatedAt >= since).Select(i => i.Job).Distinct().Count()
                }).OrderBy(m => m.Retired).ThenByDescending(m => m.JobsLast2Years).ThenBy(m => m.Title).Take(maxRows).ToListAsync(cancellationToken);

                return JsonReply(HttpStatusCode.OK, new
                {
                    provenance = Provenance(db), query = query.Trim(), totalMaterials = total, rowsReturned = rows.Count, truncated = total > rows.Count,
                    order = "Active before retired, then most used in the last 2 years",
                    rows = rows.Select(m => new
                    {
                        materialId = m.Id, title = m.Title, friendlyName = m.FriendlyName, categoryId = m.MaterialCategory_Id,
                        categoryTitle = m.CategoryTitle, retired = m.Retired, jobsLast2Years = m.JobsLast2Years
                    })
                });
            }
        }

        // Customer lookup by company name words, or the names for given customer IDs. Only the ID and company name are returned:
        // no contacts, addresses, pricing or credit fields. The Record Lookup page already lists every customer's name.
        [HttpGet]
        public async Task<ActionResult> FindCustomers(string query = null, string customerIds = null, int maxRows = 20,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            using (var db = CreateContext())
            {
                var denied = await Authorize(db, RecordLookupPermissionUrl, cancellationToken);
                if (denied != null) return denied;
                bool byName = !string.IsNullOrWhiteSpace(query);
                if (byName == (customerIds != null)) return Error(HttpStatusCode.BadRequest, "Give either query or customerIds, not both.");
                maxRows = Math.Max(1, Math.Min(maxRows, MaxSearchRows));

                if (!byName)
                {
                    var ids = ParseIds(customerIds);
                    if (ids == null || ids.Count == 0)
                        return Error(HttpStatusCode.BadRequest, "customerIds must be 1-" + MaxIgnoredServices + " comma-separated positive IDs.");
                    // Deleted customers are included here, because older jobs can still point at them.
                    var found = await db.Clients.AsNoTracking().Where(c => ids.Contains(c.Id))
                        .Select(c => new { customerId = c.Id, companyName = c.Title }).ToListAsync(cancellationToken);
                    return JsonReply(HttpStatusCode.OK, new
                    {
                        provenance = Provenance(db), customerIds = ids, totalCustomers = found.Count, rowsReturned = found.Count, truncated = false,
                        rows = found.OrderBy(c => ids.IndexOf(c.customerId)),
                        notFoundIds = ids.Where(id => !found.Any(c => c.customerId == id))
                    });
                }

                // Every word must appear in the name, spaces ignored, as in FindMaterials. Deleted customers are left out, as in the app's autocomplete.
                if (query.Trim().Length > 50) return Error(HttpStatusCode.BadRequest, "query must be 1-50 characters.");
                var words = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Take(5).ToList();
                var customers = db.Clients.AsNoTracking().Where(c => c.Deleted == null);
                foreach (var word in words)
                {
                    string w = word;
                    customers = customers.Where(c => (c.Title ?? "").Replace(" ", "").Contains(w));
                }
                string first = words[0];
                int total = await customers.CountAsync(cancellationToken);
                var rows = await customers.OrderBy(c => (c.Title ?? "").StartsWith(first) ? 0 : 1).ThenBy(c => c.Title).ThenBy(c => c.Id).Take(maxRows)
                    .Select(c => new { customerId = c.Id, companyName = c.Title }).ToListAsync(cancellationToken);

                return JsonReply(HttpStatusCode.OK, new
                {
                    provenance = Provenance(db), query = query.Trim(), totalCustomers = total, rowsReturned = rows.Count, truncated = total > rows.Count,
                    order = "Names starting with the first word first, then alphabetical; deleted customers are left out",
                    rows
                });
            }
        }

        // Lineage: links Mastermind itself recorded (Convert To Quote, shared quote numbers, reprints). Facts, not similarity.
        [HttpGet]
        public async Task<ActionResult> GetJobLineage(string jobNumber, CancellationToken cancellationToken)
        {
            using (var db = CreateContext())
            {
                var denied = await Authorize(db, RecordLookupPermissionUrl, cancellationToken);
                if (denied != null) return denied;
                if (!ValidJobNumber(jobNumber)) return Error(HttpStatusCode.BadRequest, "jobNumber must be 1-50 characters.");
                var matches = await FindJobIds(db, jobNumber.Trim(), cancellationToken);
                if (matches.Count > 1) return Error(HttpStatusCode.Conflict, "More than one non-deleted job has this number.");
                if (matches.Count == 0) return JsonReply(HttpStatusCode.NotFound, new { provenance = Provenance(db) });
                int id = matches[0];
                var job = await db.Jobs.AsNoTracking().Where(j => j.Id == id).Select(j => new { j.QuoteNumber, j.IsReprint, j.OriginalJobId }).SingleAsync(cancellationToken);
                string quoteNumber = (job.QuoteNumber ?? "").Trim();
                bool hasQuote = quoteNumber != "" && !string.Equals(quoteNumber, "N/A", StringComparison.OrdinalIgnoreCase);

                // 1. The quote this job was created from, and the earlier job that quote was converted from (Convert To Quote).
                object createdFromQuote = null;
                if (hasQuote)
                {
                    var quotes = await db.Quotes.AsNoTracking().Where(q => q.JobNumber == quoteNumber && q.Deleted == null)
                        .Select(q => new { q.CreatedAt, q.FromJob }).Take(2).ToListAsync(cancellationToken);
                    if (quotes.Count == 1)
                    {
                        var origin = quotes[0].FromJob != null ? await JobRefs(db, new List<int> { quotes[0].FromJob.Value }, cancellationToken) : new List<object>();
                        createdFromQuote = new { quoteNumber, quoteCreatedAt = quotes[0].CreatedAt, convertedFromJob = origin.FirstOrDefault() };
                    }
                    else createdFromQuote = new { quoteNumber, note = quotes.Count == 0 ? "No live quote has this number." : "More than one quote has this number, so the link is ambiguous." };
                }

                // 2. Other jobs with the same quote number: "Duplicate" copies it, and one quote can become several jobs.
                var sameQuoteNumber = await LinkedJobs(db, db.Jobs.AsNoTracking()
                    .Where(j => hasQuote && j.Id != id && j.Deleted == null && j.QuoteNumber == quoteNumber), cancellationToken);

                // 3. Quotes converted from this job, and the later jobs those quotes became.
                var quotesFromThis = db.Quotes.AsNoTracking().Where(q => q.FromJob == id && q.Deleted == null);
                var quoteNumbersFromThis = await quotesFromThis.Where(q => q.JobNumber != null && q.JobNumber != "")
                    .Select(q => q.JobNumber).Distinct().Take(50).ToListAsync(cancellationToken);
                var laterJobs = await LinkedJobs(db, db.Jobs.AsNoTracking()
                    .Where(j => j.Id != id && j.Deleted == null && quoteNumbersFromThis.Contains(j.QuoteNumber)), cancellationToken);
                int quoteCount = await quotesFromThis.CountAsync(cancellationToken);
                var quoteRefs = await quotesFromThis.OrderByDescending(q => q.CreatedAt).Take(MaxLineageJobs)
                    .Select(q => new { quoteNumber = q.JobNumber, createdAt = q.CreatedAt }).ToListAsync(cancellationToken);

                // 4. Reprint records in both directions (type and category only; reasons and costs are left out).
                var reprintRecords = await db.Reprint_Jobs.AsNoTracking().Where(r => r.ReprintJobId == id)
                    .Select(r => new { r.OriginalJobId, r.ReprintType, r.ReprintCategory, r.DateCreated }).Take(5).ToListAsync(cancellationToken);
                var reprintOf = new List<object>();
                foreach (var r in reprintRecords)
                    reprintOf.Add(new { job = (await JobRefs(db, new List<int> { r.OriginalJobId }, cancellationToken)).FirstOrDefault(),
                        reprintType = r.ReprintType, reprintCategory = r.ReprintCategory, recordedAt = (DateTime?)r.DateCreated });
                if (reprintOf.Count == 0 && job.OriginalJobId != null)
                    reprintOf.Add(new { job = (await JobRefs(db, new List<int> { job.OriginalJobId.Value }, cancellationToken)).FirstOrDefault(),
                        reprintType = (string)null, reprintCategory = (string)null, recordedAt = (DateTime?)null });
                var reprints = await LinkedJobs(db, db.Jobs.AsNoTracking()
                    .Where(j => j.Deleted == null && db.Reprint_Jobs.Any(r => r.OriginalJobId == id && r.ReprintJobId == j.Id)), cancellationToken);

                return JsonReply(HttpStatusCode.OK, new
                {
                    provenance = Provenance(db),
                    jobNumber = jobNumber.Trim(),
                    meaning = "Links Mastermind recorded when jobs were made: Convert To Quote (an earlier job became a quote, then a job), shared quote numbers "
                        + "(Duplicate copies the number; one quote can become several jobs) and reprint records. They are facts, not similarity scores. "
                        + "A job typed from scratch has no links, which does not mean it was never made before.",
                    createdFromQuote,
                    sameQuoteNumber,
                    laterJobsFromThisJob = laterJobs,
                    quotesFromThisJob = new { count = quoteCount, quotes = quoteRefs },
                    isReprint = job.IsReprint,
                    reprintOf,
                    reprints
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

        private static readonly HashSet<string> StopWords = new HashSet<string>("a an and the for of to in on with by x job print printing order qty quantity".Split(' '));
        // Fills unused word slots in the relevance estimate; no title or description contains it.
        private const string NoWord = "\u0001";

        // Up to 8 of the source's most distinctive words (3+ characters with a letter, longest first) for the relevance estimate.
        // SQL matches them as substrings ("tree" also matches "street"); the MCP server then compares whole words exactly.
        private static List<string> RelevanceWords(string text)
        {
            return Regex.Matches((text ?? "").ToLowerInvariant(), @"[\p{L}\p{N}]+").Cast<Match>().Select(m => m.Value)
                .Where(w => w.Length >= 3 && w.Any(char.IsLetter) && !StopWords.Contains(w))
                .Distinct().OrderByDescending(w => w.Length).ThenBy(w => w, StringComparer.Ordinal).Take(8).ToList();
        }

        private static string WordOrNone(List<string> words, int index)
        {
            return index < words.Count ? words[index] : NoWord;
        }

        // Up to MaxIgnoredServices comma-separated positive integers; null when the text is not valid.
        private static List<int> ParseIds(string text)
        {
            var ids = new List<int>();
            foreach (var part in (text ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int id;
                if (!int.TryParse(part.Trim(), out id) || id <= 0 || ids.Count >= MaxIgnoredServices) return null;
                ids.Add(id);
            }
            return ids;
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
                    MaterialCategoryId = i.Material1.MaterialCategory_Id, MaterialCategoryTitle = i.Material1.MaterialCategory.Title,
                    i.Press, PressTitle = i.Press1.Title, PressCategoryId = i.Press1.Category, PressCategoryTitle = i.Press1.PressCategory.Title,
                    i.PressColorProfileFront, i.PressColorCountFront,
                    i.PressColorProfileBack, i.PressColorCountBack, i.Collate, i.Staple, i.Numbering,
                    Services = i.JobItemServices.Where(s => s.Deleted == null)
                        .Select(s => new { s.Id, s.ServiceId, s.Ordinal, s.Title, s.FriendlyName, s.IsFinishingService, s.IsCuttingService,
                            CategoryId = s.Service.CatNo, CategoryTitle = s.Service.ServiceCategory.Title }),
                    // Finishing materials (e.g. lamination) as material IDs only; their costs and prices are left out.
                    Finishing = i.JobItemFinishingMaterials.Select(f => new { f.Id, f.Material, Title = f.Material1.Title,
                        CategoryId = f.Material1.MaterialCategory_Id, Application = f.FinishingMaterialApplication.Title })
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
                    materialCategoryId = i.MaterialCategoryId, materialCategoryTitle = i.MaterialCategoryTitle,
                    pressId = i.Press, pressTitle = i.PressTitle, pressCategoryId = i.PressCategoryId, pressCategoryTitle = i.PressCategoryTitle,
                    colorProfileFront = i.PressColorProfileFront, colorCountFront = i.PressColorCountFront,
                    colorProfileBack = i.PressColorProfileBack, colorCountBack = i.PressColorCountBack,
                    collate = i.Collate, staple = i.Staple, numbering = i.Numbering,
                    services = NullIfEmpty(i.Services.OrderBy(s => s.Ordinal).ThenBy(s => s.Id).Select(s => (object)new
                    {
                        legacyId = s.Id, serviceId = s.ServiceId, ordinal = s.Ordinal, title = s.Title, friendlyName = s.FriendlyName,
                        isFinishingService = s.IsFinishingService, isCuttingService = s.IsCuttingService,
                        serviceCategoryId = s.CategoryId, serviceCategoryTitle = s.CategoryTitle
                    })),
                    finishingMaterials = NullIfEmpty(i.Finishing.OrderBy(f => f.Id).Select(f => (object)new
                    {
                        legacyId = f.Id, materialId = f.Material, title = f.Title, materialCategoryId = f.CategoryId, application = f.Application
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

        // Same precedence as Job.SetCurrentJobStatus: Billed, then Shipped, then Complete.
        private static IQueryable<Job> WhereStatus(IQueryable<Job> query, string status)
        {
            switch (status)
            {
                case "Completed": return query.Where(j => j.Billed != null);
                case "Ready to post": return query.Where(j => j.Billed == null && j.Shipped != null);
                case "Ready to ship": return query.Where(j => j.Billed == null && j.Shipped == null && j.Complete != null);
                case "In progress": return query.Where(j => j.Billed == null && j.Shipped == null && j.Complete == null);
                default: return query;
            }
        }

        private static string StatusOf(DateTime? complete, DateTime? shipped, DateTime? billed)
        {
            var stage = new Job { Complete = complete, Shipped = shipped, Billed = billed };
            stage.SetCurrentJobStatus();
            return StatusText(stage.OverallJobStage);
        }

        // Compact job references for lists: number, dates, customer, description and status only.
        private static async Task<List<object>> JobRefs(FifthPressEntities db, List<int> ids, CancellationToken cancellationToken)
        {
            var rows = await db.Jobs.AsNoTracking().Where(j => ids.Contains(j.Id) && j.Deleted == null)
                .Select(j => new { j.Id, j.JobNumber, j.CreatedAt, j.Client, j.Description, j.Complete, j.Shipped, j.Billed, j.Cancelled })
                .ToListAsync(cancellationToken);
            return rows.OrderBy(r => ids.IndexOf(r.Id)).Select(r => (object)new
            {
                jobNumber = r.JobNumber, createdAt = r.CreatedAt, customerId = r.Client, description = r.Description,
                status = StatusOf(r.Complete, r.Shipped, r.Billed), isCancelled = r.Cancelled != null
            }).ToList();
        }

        // A count plus the most recent linked jobs: one quote can link 100+ jobs.
        private static async Task<object> LinkedJobs(FifthPressEntities db, IQueryable<Job> jobs, CancellationToken cancellationToken)
        {
            int count = await jobs.CountAsync(cancellationToken);
            var ids = await jobs.OrderByDescending(j => j.CreatedAt).ThenByDescending(j => j.Id).Select(j => j.Id).Take(MaxLineageJobs).ToListAsync(cancellationToken);
            return new { count, shown = ids.Count, jobs = await JobRefs(db, ids, cancellationToken) };
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
