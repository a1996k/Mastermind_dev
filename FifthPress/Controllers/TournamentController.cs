using FifthPress.ActionFilters;
using FifthPress.Models.ViewModels;
using FifthPress.Models.ViewModels.Tournament;
using FifthPress.Services;
using Newtonsoft.Json;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.Controllers
{
    [CookieFilter(Order = 1)]
    [UserPermissions(Order = 2)]
    public class TournamentController : DataController
    {
        // Data classes for API requests
        public class ScheduleDeletionRequest
        {
            public int UserId { get; set; }
            public string Date { get; set; }
        }

        [HttpPost]
        public ActionResult AddUser(string first, string last)
        {
            MastermindLogger.Log(LogLevel.Info, $"=== TournamentController.AddUser START ===");
            MastermindLogger.Log(LogLevel.Info, $"Input Parameters - First: '{first}', Last: '{last}'");

            // Validate input parameters
            if (String.IsNullOrEmpty(first) || String.IsNullOrEmpty(last))
            {
                MastermindLogger.Log(LogLevel.Warning, $"Validation failed - First or Last name is empty");
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "First and last name required");
            }

            try
            {
                MastermindLogger.Log(LogLevel.Info, $"Calling stored procedure: EventSchedule_AddUser('{first}', '{last}')");

                var result = db.EventSchedule_AddUser(first, last);
                MastermindLogger.Log(LogLevel.Info, $"Stored procedure executed successfully");

                var firstResult = result.FirstOrDefault();
                MastermindLogger.Log(LogLevel.Info, $"First result from stored procedure: {(firstResult.HasValue ? firstResult.Value.ToString() : "NULL")}");

                if (!firstResult.HasValue)
                {
                    MastermindLogger.Log(LogLevel.Error, $"Stored procedure returned NULL result");
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Stored procedure returned no result");
                }

                int newUserId = firstResult ?? -1;
                MastermindLogger.Log(LogLevel.Info, $"Successfully added user with ID: {newUserId}");
                MastermindLogger.Log(LogLevel.Info, $"=== TournamentController.AddUser END (Success) ===");

                return new JsonResult
                {
                    Data = newUserId,
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
            catch (Exception e)
            {
                MastermindLogger.Log(LogLevel.Error, $"Exception caught in AddUser method");
                MastermindLogger.Log(LogLevel.Error, $"Exception Type: {e.GetType().FullName}");
                MastermindLogger.Log(LogLevel.Error, $"Exception Message: {e.Message}");
                MastermindLogger.Log(LogLevel.Error, $"Stack Trace: {e.StackTrace}");

                if (e.InnerException != null)
                {
                    MastermindLogger.Log(LogLevel.Error, $"Inner Exception Type: {e.InnerException.GetType().FullName}");
                    MastermindLogger.Log(LogLevel.Error, $"Inner Exception Message: {e.InnerException.Message}");
                    MastermindLogger.Log(LogLevel.Error, $"Inner Exception Stack Trace: {e.InnerException.StackTrace}");
                }

                MastermindLogger.LogException(e);
                MastermindLogger.Log(LogLevel.Info, $"=== TournamentController.AddUser END (Error) ===");

                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, e.Message);
            }
        }

        public ActionResult CheckStaffAvailability(DateTime startDate, DateTime endDate)
        {
            try
            {
                // Convert input dates to UTC for database queries
                var startUtc = DateTime.SpecifyKind(startDate.Date, DateTimeKind.Local).ToUniversalTime();
                var endUtc = DateTime.SpecifyKind(endDate.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local).ToUniversalTime();

                // Get all tournaments (dates already stored in UTC)
                var tournaments = db.Tournaments
                    .Where(t => t.Deleted == null)
                    .ToList();

                // Filter tournaments with lifecycle overlap
                var overlappingTournaments = tournaments
                    .Where(t =>
                    {
                        // Convert DB dates to correct event dates (mimic JSON serialization behavior)
                        // Treat DB date as Local time, convert to UTC to get correct event date
                        var eventStartDate = DateTime.SpecifyKind(t.StartDate, DateTimeKind.Local).ToUniversalTime().Date;
                        var eventEndDate = DateTime.SpecifyKind(t.EndDate, DateTimeKind.Local).ToUniversalTime().Date;

                        // Tournament lifecycle calculation
                        var setupStart = eventStartDate.AddDays(-21); // 21 days before event start
                        var teardownEnd = eventEndDate.AddDays(6);     // 6 days after event end

                        // Compare dates
                        return setupStart <= endUtc && teardownEnd >= startUtc;
                    })
                    .ToList();

                if (!overlappingTournaments.Any())
                {
                    return Json(new
                    {
                        success = true,
                        tournamentDates = new List<object>(),
                        totalDatesWithTournaments = 0,
                        message = "No tournament activity found in this date range"
                    }, JsonRequestBehavior.AllowGet);
                }

                // Generate all tournament dates (in UTC)
                var tournamentDatesUtc = new HashSet<DateTime>();

                foreach (var tournament in overlappingTournaments)
                {
                    // Convert DB dates to correct event dates (mimic JSON serialization behavior)
                    var eventStartDate = DateTime.SpecifyKind(tournament.StartDate, DateTimeKind.Local).ToUniversalTime().Date;
                    var eventEndDate = DateTime.SpecifyKind(tournament.EndDate, DateTimeKind.Local).ToUniversalTime().Date;

                    var setupStart = eventStartDate.AddDays(-21);
                    var teardownEnd = eventEndDate.AddDays(6);

                    // Generate dates
                    for (var date = setupStart; date <= teardownEnd; date = date.AddDays(1))
                    {
                        // Only include dates within user's requested range
                        if (date >= startUtc.Date && date <= endUtc.Date)
                        {
                            tournamentDatesUtc.Add(date);
                        }
                    }
                }

                // Get schedule entries (in UTC) - use DbFunctions for SQL compatibility
                // Include ALL entry types: Tournament (1), Not Working (2), and Travel (3)
                var scheduleEntries = db.StaffScheduleEntries
                    .Where(s => s.Deleted == null &&
                               s.ScheduleDate >= startUtc &&
                               s.ScheduleDate <= endUtc)
                    .Select(s => new { s.UserId, ScheduleDate = DbFunctions.TruncateTime(s.ScheduleDate) })
                    .ToList();

                // Get all event staff
                var allEventStaff = db.Users
                    .Where(u => u.IsEventStaff == true &&
                               u.Deleted == null)
                    .Select(u => new
                    {
                        UserId = u.Id,
                        u.FirstName,
                        u.LastName,
                        FullName = u.FirstName + " " + u.LastName
                    })
                    .ToList();

                // Build results
                var results = new List<object>();

                foreach (var dateUtc in tournamentDatesUtc.OrderBy(d => d))
                {
                    // Find assigned users on this UTC date
                    var assignedUserIds = scheduleEntries
                        .Where(s => s.ScheduleDate == dateUtc)
                        .Select(s => s.UserId)
                        .Distinct()
                        .ToList();

                    // Find available staff (not assigned on this date)
                    var availableStaff = allEventStaff
                        .Where(u => !assignedUserIds.Contains(u.UserId))
                        .OrderBy(u => u.FirstName)
                        .ThenBy(u => u.LastName)
                        .ToList();

                    results.Add(new
                    {
                        Date = dateUtc.ToString("yyyy-MM-dd"), // Format UTC date directly (no time conversion)
                        AvailableStaff = availableStaff,
                        AvailableCount = availableStaff.Count
                    });
                }

                return Json(new
                {
                    success = true,
                    tournamentDates = results,
                    totalDatesWithTournaments = tournamentDatesUtc.Count,
                    activeTournaments = overlappingTournaments.Select(t => t.Name).ToList()
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception e)
            {
                return Json(new
                {
                    success = false,
                    message = $"Error checking staff availability: {e.Message}"
                }, JsonRequestBehavior.AllowGet);
            }
        }

        // Tournament Management Actions
        [HttpGet]
        public async Task<ActionResult> TournamentIndex()
        {
            var tournaments = await db.Tournaments
                .Where(x => x.Deleted == null)
                .Include(t => t.Client)
                .Include(t => t.User)
                .OrderByDescending(t => t.StartDate)
                .ToListAsync();

            var data = tournaments.Select(t => new
            {
                t.Id,
                t.Name,
                t.Description,
                t.StartDate,
                t.EndDate,
                t.Location,
                Status = (TournamentStatus)t.Status,
                ClientName = t.Client?.Title ?? "No Client",
                CreatedBy = t.User?.FirstName + " " + t.User?.LastName,
                t.AdvanceWeekStartDate,
                t.AdvanceWeekEndDate,
                t.SetupStartDate,
                t.SetupEndDate,
                t.TeardownStartDate,
                t.TeardownEndDate,
                t.MinimumWage,
                t.PerDiem,
                t.TravelPerDiem,
                t.OTByDay
            }).ToList();

            return Json(data, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public async Task<ActionResult> Details(int id)
        {
            var tournament = await db.Tournaments
                .Where(x => x.Id == id && x.Deleted == null)
                .Include(t => t.Client)
                .Include(t => t.User)
                .Include(t => t.User1)
                .FirstOrDefaultAsync();

            if (tournament == null)
            {
                return Json(new { success = false, message = "Tournament not found" }, JsonRequestBehavior.AllowGet);
            }

            var data = new
            {
                tournament.Id,
                tournament.Name,
                tournament.Description,
                tournament.StartDate,
                tournament.EndDate,
                tournament.Location,
                Status = (TournamentStatus)tournament.Status,
                tournament.ClientId,
                ClientName = tournament.Client?.Title,
                tournament.AdvanceWeekStartDate,
                tournament.AdvanceWeekEndDate,
                tournament.SetupStartDate,
                tournament.SetupEndDate,
                tournament.TeardownStartDate,
                tournament.TeardownEndDate,
                tournament.Created,
                CreatedBy = tournament.User?.FirstName + " " + tournament.User?.LastName,
                tournament.Modified,
                ModifiedBy = tournament.User1?.FirstName + " " + tournament.User1?.LastName,
                tournament.MinimumWage,
                tournament.PerDiem,
                tournament.TravelPerDiem,
                tournament.OTByDay
            };

            return Json(data, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public async Task<ActionResult> GetCreateData()
        {
            var vm = new VM_TournamentEdit
            {
                StartDate = DateTime.Today.AddDays(30),
                EndDate = DateTime.Today.AddDays(33),
                Status = TournamentStatus.Planned
            };

            await PopulateDropdowns(vm);

            var data = new
            {
                DefaultStartDate = vm.StartDate,
                DefaultEndDate = vm.EndDate,
                DefaultStatus = vm.Status,
                AvailableClients = vm.AvailableClients?.Select(c => new { Value = c.Value, Text = c.Text }),
                AvailableStatuses = vm.AvailableStatuses?.Select(s => new { Value = s.Value, Text = s.Text })
            };

            return Json(data, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public async Task<ActionResult> Create(VM_TournamentEdit vm)
        {
            if (ModelState.IsValid)
            {
                var tournament = new Tournament
                {
                    Name = vm.Name,
                    Description = vm.Description,
                    StartDate = vm.StartDate,
                    EndDate = vm.EndDate,
                    Location = vm.Location,
                    Status = (int)vm.Status,
                    ClientId = vm.ClientId,
                    AdvanceWeekStartDate = vm.AdvanceWeekStartDate,
                    AdvanceWeekEndDate = vm.AdvanceWeekEndDate,
                    SetupStartDate = vm.SetupStartDate,
                    SetupEndDate = vm.SetupEndDate,
                    TeardownStartDate = vm.TeardownStartDate,
                    TeardownEndDate = vm.TeardownEndDate,
                    Created = DateTime.UtcNow,
                    CreatedBy = UserSessionHelper.GetActiveUserId(HttpContext),
                    MinimumWage = vm.MinimumWage,
                    PerDiem = vm.PerDiem,
                    TravelPerDiem = vm.TravelPerDiem,
                    BillRate = vm.BillRate,
                    ManagerBillRate = vm.ManagerBillRate,
                    PrintBillRate = vm.PrintBillRate,
                    OTByDay = vm.OTByDay
                };

                db.Tournaments.Add(tournament);
                await db.SaveChangesAsync();

                return Json(new { success = true, message = "Tournament created successfully", tournamentId = tournament.Id });
            }

            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            return Json(new { success = false, message = "Validation failed", errors = errors });
        }

        [HttpPost]
        public async Task<ActionResult> SaveTournaments(List<TournamentSaveData> tournaments)
        {
            try
            {
                var results = new List<object>();

                foreach (var tournamentData in tournaments)
                {
                    Tournament tournament;
                    bool isNew = false;

                    // Check if tournament exists
                    tournament = await db.Tournaments
                        .FirstOrDefaultAsync(t => t.Id == tournamentData.Id && t.Deleted == null);

                    if (tournament == null)
                    {
                        // Create new tournament
                        tournament = new Tournament
                        {
                            Created = DateTime.UtcNow,
                            CreatedBy = UserSessionHelper.GetActiveUserId(HttpContext)
                        };
                        db.Tournaments.Add(tournament);
                        isNew = true;
                    }
                    else
                    {
                        // Update existing tournament
                        tournament.Modified = DateTime.UtcNow;
                        tournament.ModifiedBy = UserSessionHelper.GetActiveUserId(HttpContext);
                    }

                    // Update tournament properties
                    tournament.Name = tournamentData.Name;
                    tournament.Location = tournamentData.Location;
                    tournament.StartDate = tournamentData.StartDate;
                    tournament.EndDate = tournamentData.EndDate;
                    tournament.AdvanceWeekStartDate = tournamentData.AdvanceWeekStartDate;
                    tournament.AdvanceWeekEndDate = tournamentData.AdvanceWeekEndDate;
                    tournament.SetupStartDate = tournamentData.SetupStartDate;
                    tournament.SetupEndDate = tournamentData.SetupEndDate;
                    tournament.TeardownStartDate = tournamentData.TeardownStartDate;
                    tournament.TeardownEndDate = tournamentData.TeardownEndDate;
                    tournament.Status = tournamentData.Status;
                    tournament.ClientId = tournamentData.ClientId;
                    tournament.MinimumWage = tournamentData.MinimumWage;
                    tournament.PerDiem = tournamentData.PerDiem;
                    tournament.TravelPerDiem = tournamentData.TravelPerDiem;
                    tournament.OTByDay = tournamentData.OTByDay;

                    results.Add(new
                    {
                        id = tournamentData.Id,
                        action = isNew ? "created" : "updated",
                        name = tournament.Name
                    });
                }

                await db.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"Successfully processed {tournaments.Count} tournament(s)",
                    results = results
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Error saving tournaments: " + ex.Message
                });
            }
        }

        [HttpGet]
        public async Task<ActionResult> GetEditData(int id)
        {
            var tournament = await db.Tournaments
                .Where(x => x.Id == id && x.Deleted == null)
                .FirstOrDefaultAsync();

            if (tournament == null)
            {
                return Json(new { success = false, message = "Tournament not found" }, JsonRequestBehavior.AllowGet);
            }

            var vm = new VM_TournamentEdit
            {
                Id = tournament.Id,
                Name = tournament.Name,
                Description = tournament.Description,
                StartDate = tournament.StartDate,
                EndDate = tournament.EndDate,
                Location = tournament.Location,
                Status = (TournamentStatus)tournament.Status,
                ClientId = tournament.ClientId,
                AdvanceWeekStartDate = tournament.AdvanceWeekStartDate,
                AdvanceWeekEndDate = tournament.AdvanceWeekEndDate,
                SetupStartDate = tournament.SetupStartDate,
                SetupEndDate = tournament.SetupEndDate,
                TeardownStartDate = tournament.TeardownStartDate,
                TeardownEndDate = tournament.TeardownEndDate,
                MinimumWage = tournament.MinimumWage ?? 0m,
                PerDiem = tournament.PerDiem ?? 0m,
                TravelPerDiem = tournament.TravelPerDiem ?? 0m,
                OTByDay = tournament.OTByDay ?? false,
            };

            await PopulateDropdowns(vm);

            var data = new
            {
                Tournament = vm,
                AvailableClients = vm.AvailableClients?.Select(c => new { Value = c.Value, Text = c.Text }),
                AvailableStatuses = vm.AvailableStatuses?.Select(s => new { Value = s.Value, Text = s.Text })
            };

            return Json(data, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public async Task<ActionResult> Edit(VM_TournamentEdit vm)
        {
            if (ModelState.IsValid)
            {
                var tournament = await db.Tournaments
                    .Where(x => x.Id == vm.Id && x.Deleted == null)
                    .FirstOrDefaultAsync();

                if (tournament == null)
                {
                    return Json(new { success = false, message = "Tournament not found" });
                }

                tournament.Name = vm.Name;
                tournament.Description = vm.Description;
                tournament.StartDate = vm.StartDate;
                tournament.EndDate = vm.EndDate;
                tournament.Location = vm.Location;
                tournament.Status = (int)vm.Status;
                tournament.ClientId = vm.ClientId;
                tournament.AdvanceWeekStartDate = vm.AdvanceWeekStartDate;
                tournament.AdvanceWeekEndDate = vm.AdvanceWeekEndDate;
                tournament.SetupStartDate = vm.SetupStartDate;
                tournament.SetupEndDate = vm.SetupEndDate;
                tournament.TeardownStartDate = vm.TeardownStartDate;
                tournament.TeardownEndDate = vm.TeardownEndDate;
                tournament.Modified = DateTime.UtcNow;
                tournament.ModifiedBy = UserSessionHelper.GetActiveUserId(HttpContext);
                tournament.MinimumWage = vm.MinimumWage;
                tournament.PerDiem = vm.PerDiem;
                tournament.TravelPerDiem = vm.TravelPerDiem;
                tournament.BillRate = vm.BillRate;
                tournament.ManagerBillRate = vm.ManagerBillRate;
                tournament.PrintBillRate = vm.PrintBillRate;
                tournament.OTByDay = vm.OTByDay;

                await db.SaveChangesAsync();

                return Json(new { success = true, message = "Tournament updated successfully", tournamentId = tournament.Id });
            }

            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            return Json(new { success = false, message = "Validation failed", errors = errors });
        }

        [HttpPost]
        public async Task<ActionResult> Delete(int id)
        {
            var tournament = await db.Tournaments
                .Where(x => x.Id == id && x.Deleted == null)
                .FirstOrDefaultAsync();

            if (tournament == null)
            {
                return Json(new { success = false, message = "Tournament not found" });
            }

            // Soft delete
            tournament.Deleted = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Json(new { success = true, message = "Tournament deleted successfully" });
        }

        private async Task PopulateDropdowns(VM_TournamentEdit vm)
        {
            // Get golf clients for tournament assignment
            vm.AvailableClients = await db.Clients
                .Where(x => x.Deleted == null && (x.IsGolfClient ?? false))
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Title
                })
                .ToListAsync();

            vm.AvailableClients.Insert(0, new SelectListItem { Value = "", Text = "-- Select Client --" });
        }

        // Staff Assignment Management
        [HttpGet]
        public async Task<ActionResult> GetStaffAssignments(int id)
        {
            var tournament = await db.Tournaments
                .Where(x => x.Id == id && x.Deleted == null)
                .Include(t => t.Client)
                .FirstOrDefaultAsync();

            if (tournament == null)
            {
                return Json(new { success = false, message = "Tournament not found" }, JsonRequestBehavior.AllowGet);
            }

            var assignments = await (from entry in db.StaffScheduleEntries
                                     join user in db.Users on entry.UserId equals user.Id
                                     from role in db.TournamentRoles.Where(r => r.Id == entry.TournamentRoleId).DefaultIfEmpty()
                                     where entry.TournamentId == id && entry.Deleted == null
                                     group new { entry, user, role } by new { entry.UserId, entry.TournamentId, entry.TournamentRoleId } into g
                                     select new
                                     {
                                         g.Key.UserId,
                                         g.Key.TournamentId,
                                         g.Key.TournamentRoleId,
                                         User = g.FirstOrDefault().user,
                                         TournamentRole = g.FirstOrDefault().role,
                                         IsActive = true,
                                         Notes = ""
                                     }).ToListAsync();

            var vm = new VM_TournamentStaffAssignment
            {
                TournamentId = tournament.Id,
                TournamentName = tournament.Name,
                TournamentStartDate = tournament.StartDate,
                TournamentEndDate = tournament.EndDate,
                AdvanceWeekStartDate = tournament.AdvanceWeekStartDate,
                AdvanceWeekEndDate = tournament.AdvanceWeekEndDate,
                Assignments = assignments.Select(a => new VM_StaffAssignmentItem
                {
                    Id = 0, // No longer relevant
                    TournamentId = a.TournamentId ?? 0,
                    UserId = a.UserId,
                    UserName = a.User.FirstName + " " + a.User.LastName,
                    TournamentRoleId = a.TournamentRoleId ?? 0,
                    RoleName = a.TournamentRole?.Title ?? "Unknown",
                    DailyRate = a.TournamentRole?.DailyRate ?? 0,
                    Notes = a.Notes,
                    IsActive = a.IsActive
                }).ToList()
            };

            await PopulateStaffDropdowns(vm);

            var data = new
            {
                Tournament = new
                {
                    Id = tournament.Id,
                    Name = tournament.Name,
                    StartDate = tournament.StartDate,
                    EndDate = tournament.EndDate,
                    AdvanceWeekStartDate = tournament.AdvanceWeekStartDate,
                    AdvanceWeekEndDate = tournament.AdvanceWeekEndDate
                },
                Assignments = vm.Assignments,
                AvailableUsers = vm.AvailableUsers?.Select(u => new { Value = u.Value, Text = u.Text }),
                AvailableRoles = vm.AvailableRoles?.Select(r => new { Value = r.Value, Text = r.Text })
            };

            return Json(data, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public async Task<ActionResult> AddStaffAssignment(VM_AddStaffAssignment vm)
        {
            if (ModelState.IsValid)
            {
                // Check if user is already assigned to this tournament
                var existingAssignment = await db.StaffScheduleEntries
                    .Where(x => x.TournamentId == vm.TournamentId && x.UserId == vm.UserId && x.Deleted == null)
                    .FirstOrDefaultAsync();

                if (existingAssignment != null)
                {
                    return Json(new { success = false, message = "User is already assigned to this tournament" });
                }

                // For now, just return success since assignments are created through schedule entries
                // The actual assignment will be created when schedule entries are saved

                return Json(new { success = true, message = "Staff member assigned successfully" });
            }

            return Json(new { success = false, message = "Invalid assignment data" });
        }

        [HttpPost]
        public async Task<ActionResult> RemoveStaffAssignment(int userId, int tournamentId)
        {
            var entries = await db.StaffScheduleEntries
                .Where(x => x.UserId == userId && x.TournamentId == tournamentId && x.Deleted == null)
                .ToListAsync();

            if (!entries.Any())
            {
                return Json(new { success = false, message = "Assignment not found" });
            }

            // Soft delete all schedule entries for this user/tournament
            foreach (var entry in entries)
            {
                entry.Deleted = DateTime.UtcNow;
            }
            await db.SaveChangesAsync();

            return Json(new { success = true, message = "Staff assignment removed successfully" });
        }

        [HttpPost]
        public async Task<ActionResult> UpdateStaffAssignment(VM_StaffAssignmentItem vm)
        {
            var entries = await db.StaffScheduleEntries
                .Where(x => x.UserId == vm.UserId && x.TournamentId == vm.TournamentId && x.Deleted == null)
                .ToListAsync();

            if (!entries.Any())
            {
                return Json(new { success = false, message = "Assignment not found" });
            }

            // Update all schedule entries for this user/tournament
            foreach (var entry in entries)
            {
                entry.TournamentRoleId = vm.TournamentRoleId;
                entry.Modified = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();

            return Json(new { success = true, message = "Assignment updated successfully" });
        }

        // Schedule Management
        [HttpGet]
        public async Task<ActionResult> GetSchedule(int? id)
        {
            // If no ID provided, show all tournament schedules
            if (!id.HasValue)
            {
                return await GetAllSchedulesJson();
            }

            // Single tournament schedule (existing logic)
            var tournament = await db.Tournaments
                .Where(x => x.Id == id.Value && x.Deleted == null)
                .FirstOrDefaultAsync();

            if (tournament == null)
            {
                return Json(new { success = false, message = "Tournament not found" }, JsonRequestBehavior.AllowGet);
            }

            var scheduleEntries = await db.StaffScheduleEntries
                .Where(x => x.TournamentId == id.Value && x.Deleted == null)
                .Include(e => e.User)
                .ToListAsync();

            var assignments = scheduleEntries
                .GroupBy(e => new { e.UserId, e.TournamentId })
                .Select(g => new
                {
                    UserId = g.Key.UserId,
                    TournamentId = g.Key.TournamentId,
                    User = g.First().User,
                    TournamentRole = new { Title = g.First().Role, DailyRate = g.First().DailyRate },
                    ScheduleEntries = g.ToList()
                }).ToList();

            // Generate schedule dates (advance week + tournament dates)
            var scheduleDates = new List<DateTime>();
            if (tournament.AdvanceWeekStartDate.HasValue && tournament.AdvanceWeekEndDate.HasValue)
            {
                for (var date = tournament.AdvanceWeekStartDate.Value; date <= tournament.AdvanceWeekEndDate.Value; date = date.AddDays(1))
                {
                    scheduleDates.Add(date);
                }
            }
            for (var date = tournament.StartDate; date <= tournament.EndDate; date = date.AddDays(1))
            {
                scheduleDates.Add(date);
            }
            scheduleDates = scheduleDates.OrderBy(d => d).ToList();

            // Build staff schedule entries - only return actual schedule records
            var staff = assignments.Select(a => new
            {
                AssignmentId = 0,
                UserId = a.UserId,
                UserName = a.User.FirstName + " " + a.User.LastName,
                RoleName = a.TournamentRole.Title,
                DailyRate = 0m,
                ScheduleEntries = a.ScheduleEntries
                    .Where(s => s.EntryType == 1) // Only include tournament entries
                    .Select(s => new
                    {
                        Id = s.Id,
                        TournamentStaffAssignmentId = 0,
                        ScheduleDate = s.ScheduleDate,
                        IsScheduledToWork = true,
                        IsAdvanceWeek = s.IsAdvanceWeek,
                        StartTime = (TimeSpan?)null,
                        EndTime = (TimeSpan?)null,
                        Notes = ""
                    }).ToList()
            }).ToList();

            var data = new
            {
                TournamentId = tournament.Id,
                TournamentName = tournament.Name,
                TournamentStartDate = tournament.StartDate,
                TournamentEndDate = tournament.EndDate,
                AdvanceWeekStartDate = tournament.AdvanceWeekStartDate,
                AdvanceWeekEndDate = tournament.AdvanceWeekEndDate,
                ScheduleDates = scheduleDates,
                Staff = staff
            };

            return Json(data, JsonRequestBehavior.AllowGet);
        }

        // All Tournament Schedules
        private async Task<ActionResult> GetAllSchedulesJson()
        {
            var tournaments = await db.Tournaments
                .Where(x => x.Deleted == null)
                .Include(t => t.Client)
                .OrderByDescending(t => t.StartDate)
                .ToListAsync();

            var vm = new VM_AllTournamentSchedules();

            foreach (var tournament in tournaments)
            {
                var scheduleEntries = await db.StaffScheduleEntries
                    .Where(x => x.TournamentId == tournament.Id && x.Deleted == null)
                    .Include(e => e.User)
                    .ToListAsync();

                var assignments = scheduleEntries
                    .GroupBy(e => new { e.UserId, e.TournamentId })
                    .Select(g => new
                    {
                        UserId = g.Key.UserId,
                        User = g.First().User,
                        ScheduleEntries = g.ToList()
                    }).ToList();

                // Generate schedule dates (advance week + tournament dates)
                var scheduleDates = new List<DateTime>();
                if (tournament.AdvanceWeekStartDate.HasValue && tournament.AdvanceWeekEndDate.HasValue)
                {
                    for (var date = tournament.AdvanceWeekStartDate.Value; date <= tournament.AdvanceWeekEndDate.Value; date = date.AddDays(1))
                    {
                        scheduleDates.Add(date);
                    }
                }
                for (var date = tournament.StartDate; date <= tournament.EndDate; date = date.AddDays(1))
                {
                    scheduleDates.Add(date);
                }

                var tournamentSchedule = new VM_TournamentScheduleSummary
                {
                    TournamentId = tournament.Id,
                    TournamentName = tournament.Name,
                    TournamentStartDate = tournament.StartDate,
                    TournamentEndDate = tournament.EndDate,
                    AdvanceWeekStartDate = tournament.AdvanceWeekStartDate,
                    AdvanceWeekEndDate = tournament.AdvanceWeekEndDate,
                    Location = tournament.Location,
                    Status = (TournamentStatus)tournament.Status,
                    ClientName = tournament.Client?.Title ?? "No Client",
                    ScheduleDates = scheduleDates.OrderBy(d => d).ToList()
                };

                // Build staff schedule entries
                tournamentSchedule.Staff = assignments.Select(a => new VM_ScheduleStaffItem
                {
                    AssignmentId = 0,
                    UserId = a.UserId,
                    UserName = a.User.FirstName + " " + a.User.LastName,
                    RoleName = "General",
                    DailyRate = 350m,
                    ScheduleEntries = tournamentSchedule.ScheduleDates.Select(date =>
                    {
                        var existingSchedule = a.ScheduleEntries
                            .FirstOrDefault(s => s.ScheduleDate == date);

                        return new VM_ScheduleDateEntry
                        {
                            Id = existingSchedule?.Id ?? 0,
                            TournamentStaffAssignmentId = 0,
                            ScheduleDate = date,
                            IsScheduledToWork = existingSchedule != null,
                            IsAdvanceWeek = tournament.AdvanceWeekStartDate.HasValue &&
                                           tournament.AdvanceWeekEndDate.HasValue &&
                                           date >= tournament.AdvanceWeekStartDate.Value &&
                                           date <= tournament.AdvanceWeekEndDate.Value,
                            StartTime = null,
                            EndTime = null,
                            Notes = ""
                        };
                    }).ToList()
                }).ToList();

                vm.Tournaments.Add(tournamentSchedule);
            }

            return Json(vm, JsonRequestBehavior.AllowGet);
        }

        private async Task PopulateStaffDropdowns(VM_TournamentStaffAssignment vm)
        {
            // Get available users (event staff)
            vm.AvailableUsers = await db.Users
                .Where(x => x.Deleted == null && x.IsEventStaff == true)
                .Select(u => new SelectListItem
                {
                    Value = u.Id.ToString(),
                    Text = u.FirstName + " " + u.LastName
                })
                .ToListAsync();

            vm.AvailableUsers.Insert(0, new SelectListItem { Value = "", Text = "-- Select User --" });

            // Get available tournament roles
            vm.AvailableRoles = await db.TournamentRoles
                .Where(r => r.Deleted == null && r.IsActive)
                .Select(r => new SelectListItem
                {
                    Value = r.Id.ToString(),
                    Text = r.Title + " (" + r.DailyRate.ToString("C") + "/day)"
                })
                .ToListAsync();

            vm.AvailableRoles.Insert(0, new SelectListItem { Value = "", Text = "-- Select Role --" });
        }

        // Tournament Role Management API
        [HttpGet]
        public async Task<ActionResult> GetTournamentRoles()
        {
            var roles = await db.TournamentRoles
                .Where(r => r.Deleted == null && r.IsActive)
                .Select(r => new
                {
                    Id = r.Id,
                    Title = r.Title,
                    DailyRate = r.DailyRate,
                    Description = r.Description
                })
                .ToListAsync();

            return Json(roles, JsonRequestBehavior.AllowGet);
        }

        // Get all clients for dropdown (used by Select2 autocomplete)
        [HttpGet]
        public async Task<ActionResult> GetClients(string search = "")
        {
            try
            {
                var query = db.Clients.Where(c => c.Deleted == null);

                // Apply search filter if provided (minimum 3 characters)
                if (!string.IsNullOrWhiteSpace(search) && search.Length >= 3)
                {
                    search = search.ToLower();
                    query = query.Where(c => c.Title.ToLower().Contains(search));
                }

                var clients = await query
                    .OrderBy(c => c.Title)
                    .Select(c => new
                    {
                        Id = c.Id,
                        Title = c.Title
                    })
                    .ToListAsync();

                return Json(clients, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error loading clients: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public async Task<ActionResult> CreateTournamentRole(string title, decimal dailyRate, string description = null)
        {
            if (string.IsNullOrWhiteSpace(title) || dailyRate <= 0)
            {
                return Json(new { success = false, message = "Invalid role data" });
            }

            var role = new TournamentRole
            {
                Title = title,
                DailyRate = dailyRate,
                Description = description,
                IsActive = true,
                Created = DateTime.UtcNow
            };

            db.TournamentRoles.Add(role);
            await db.SaveChangesAsync();

            return Json(new { success = true, message = "Role created successfully", roleId = role.Id });
        }

        [HttpPost]
        public async Task<ActionResult> UpdateTournamentRole(int id, string title, decimal dailyRate, string description = null)
        {
            var role = await db.TournamentRoles
                .Where(r => r.Id == id && r.Deleted == null)
                .FirstOrDefaultAsync();

            if (role == null)
            {
                return Json(new { success = false, message = "Role not found" });
            }

            role.Title = title;
            role.DailyRate = dailyRate;
            role.Description = description;

            await db.SaveChangesAsync();

            return Json(new { success = true, message = "Role updated successfully" });
        }

        [HttpPost]
        public async Task<ActionResult> DeleteTournamentRole(int id)
        {
            var role = await db.TournamentRoles
                .Where(r => r.Id == id && r.Deleted == null)
                .FirstOrDefaultAsync();

            if (role == null)
            {
                return Json(new { success = false, message = "Role not found" });
            }

            // Check if role is in use
            var assignmentsCount = await db.StaffScheduleEntries
                .CountAsync(a => a.TournamentRoleId == id && a.Deleted == null);

            if (assignmentsCount > 0)
            {
                return Json(new { success = false, message = "Cannot delete role that is currently assigned to staff" });
            }

            role.Deleted = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Json(new { success = true, message = "Role deleted successfully" });
        }

        // Tournament Cost Calculation API - UPDATED to separate Advance Week costs
        [HttpGet]
        public async Task<ActionResult> GetTournamentCosts(int tournamentId)
        {
            var tournament = await db.Tournaments
                .Where(t => t.Id == tournamentId && t.Deleted == null)
                .FirstOrDefaultAsync();

            if (tournament == null)
            {
                return Json(new { success = false, message = "Tournament not found" }, JsonRequestBehavior.AllowGet);
            }

            var scheduleEntries = await db.StaffScheduleEntries
                .Where(a => a.TournamentId == tournamentId && a.Deleted == null)
                .Include(a => a.User)
                .ToListAsync();

            var assignments = scheduleEntries
                .GroupBy(e => new { e.UserId, e.TournamentId })
                .Select(g => new
                {
                    User = g.First().User,
                    TournamentRole = new { Title = g.First().Role, DailyRate = g.First().DailyRate },
                    ScheduleEntries = g.Where(x => x.EntryType == 1).ToList(),
                    TravelEntries = g.Where(x => x.EntryType == 2).ToList(),
                    TournamentTravelExpenses = new List<object>()
                }).ToList();

            var costBreakdown = assignments.Select(a =>
            {
                var scheduledDays = a.ScheduleEntries.ToList();
                var advanceWeekDays = scheduledDays.Count(s => s.IsAdvanceWeek);
                var tournamentWeekDays = scheduledDays.Count(s => !s.IsAdvanceWeek);
                var totalDays = scheduledDays.Count;

                // Calculate costs separately for advance week and tournament week
                var advanceWeekLaborCost = advanceWeekDays * a.TournamentRole.DailyRate;
                var tournamentWeekLaborCost = tournamentWeekDays * a.TournamentRole.DailyRate;
                var totalLaborCost = advanceWeekLaborCost + tournamentWeekLaborCost;

                var travelDays = a.TravelEntries.ToList();
                var perDiemCost = travelDays.Sum(t => t.DailyRate);

                var travelExpenses = 0m; // Travel expenses not implemented in new schema

                var totalCost = totalLaborCost + perDiemCost + travelExpenses;

                return new
                {
                    AssignmentId = 0, // No longer relevant in new schema
                    UserId = a.User.Id,
                    UserName = a.User.FirstName + " " + a.User.LastName,
                    RoleName = a.TournamentRole.Title,
                    DailyRate = a.TournamentRole.DailyRate,
                    TotalDays = totalDays,
                    AdvanceWeekDays = advanceWeekDays,
                    TournamentWeekDays = tournamentWeekDays,
                    // Separated costs
                    AdvanceWeekLaborCost = advanceWeekLaborCost,
                    TournamentWeekLaborCost = tournamentWeekLaborCost,
                    TotalLaborCost = totalLaborCost,
                    PerDiemCost = perDiemCost,
                    TravelExpenses = travelExpenses,
                    TotalCost = totalCost
                };
            }).ToList();

            var tournamentSummary = new
            {
                TournamentId = tournamentId,
                TournamentName = tournament.Name,
                Location = tournament.Location,
                StartDate = tournament.StartDate,
                EndDate = tournament.EndDate,
                AdvanceWeekStartDate = tournament.AdvanceWeekStartDate,
                AdvanceWeekEndDate = tournament.AdvanceWeekEndDate,
                TotalStaff = costBreakdown.Count,
                // Separated cost summaries
                AdvanceWeekLaborCost = costBreakdown.Sum(c => c.AdvanceWeekLaborCost),
                TournamentWeekLaborCost = costBreakdown.Sum(c => c.TournamentWeekLaborCost),
                TotalLaborCost = costBreakdown.Sum(c => c.TotalLaborCost),
                TotalPerDiemCost = costBreakdown.Sum(c => c.PerDiemCost),
                TotalTravelExpenses = costBreakdown.Sum(c => c.TravelExpenses),
                GrandTotal = costBreakdown.Sum(c => c.TotalCost),
                // Note about advance week hours per requirements
                Note = "Advance week hours are tracked separately and NOT included in tournament hour calculations",
                StaffBreakdown = costBreakdown
            };

            return Json(tournamentSummary, JsonRequestBehavior.AllowGet);
        }

        // Travel functionality removed - not yet in production

        // Method removed - not yet in production
        [HttpGet]
        public async Task<ActionResult> GetYearlyTournamentSchedule(int? year = null)
        {
            try
            {
                var targetYear = year ?? DateTime.Now.Year;

                // Include tournaments where any phase date (Setup, Advanced Week, Teardown) falls in the target year
                var tournamentsFromDb = await db.Tournaments
                    .Where(t => t.Deleted == null &&
                               (t.StartDate.Year == targetYear ||
                                t.EndDate.Year == targetYear ||
                                (t.SetupStartDate.HasValue && t.SetupStartDate.Value.Year == targetYear) ||
                                (t.SetupEndDate.HasValue && t.SetupEndDate.Value.Year == targetYear) ||
                                (t.AdvanceWeekStartDate.HasValue && t.AdvanceWeekStartDate.Value.Year == targetYear) ||
                                (t.AdvanceWeekEndDate.HasValue && t.AdvanceWeekEndDate.Value.Year == targetYear) ||
                                (t.TeardownStartDate.HasValue && t.TeardownStartDate.Value.Year == targetYear) ||
                                (t.TeardownEndDate.HasValue && t.TeardownEndDate.Value.Year == targetYear)))
                    .Include(t => t.Client)
                    .OrderBy(t => t.StartDate)
                    .ToListAsync();

                // Use phase dates from database, with fallback to calculated values if not set
                var tournaments = tournamentsFromDb.Select(t => new
                {
                    TournamentId = t.Id,
                    TournamentName = t.Name,
                    Location = t.Location,
                    StartDate = t.StartDate,
                    EndDate = t.EndDate,
                    SetupStartDate = t.SetupStartDate ?? t.StartDate.AddDays(-21), // Use DB value or fallback to 21 days before
                    SetupEndDate = t.SetupEndDate ?? (t.AdvanceWeekStartDate.HasValue ? t.AdvanceWeekStartDate.Value.AddDays(-1) : t.StartDate.AddDays(-8)),
                    AdvanceWeekStartDate = t.AdvanceWeekStartDate,
                    AdvanceWeekEndDate = t.AdvanceWeekEndDate,
                    TeardownStartDate = t.TeardownStartDate ?? t.EndDate.AddDays(1), // Use DB value or fallback to 1 day after
                    TeardownEndDate = t.TeardownEndDate ?? t.EndDate.AddDays(6), // Use DB value or fallback to 6 days after
                    Status = t.Status,
                    ClientId = t.ClientId,
                    ClientName = t.Client != null ? t.Client.Title : null,
                    MinimumWage = t.MinimumWage,
                    PerDiem = t.PerDiem,
                    TravelPerDiem = t.TravelPerDiem,
                    BillRate = t.BillRate,
                    ManagerBillRate = t.ManagerBillRate,
                    PrintBillRate = t.PrintBillRate,
                    OTByDay = t.OTByDay
                }).ToList();

                var result = new
                {
                    Year = targetYear,
                    Tournaments = tournaments
                };

                return Json(result, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error loading tournaments: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // Get available tournament roles for frontend
        [HttpGet]
        public async Task<ActionResult> GetAvailableTournamentRoles()
        {
            try
            {
                var roles = await db.TournamentRoles
                    .Where(r => r.Deleted == null && (r.IsActive || r.Title == "Unassigned"))
                    .OrderBy(r => r.Title)
                    .Select(r => new
                    {
                        Id = r.Id,
                        Title = r.Title,
                        DailyRate = r.DailyRate,
                        Description = r.Description
                    })
                    .ToListAsync();

                return Json(roles, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error loading tournament roles: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // Get daily staff distribution for frontend schedule loading
        [HttpGet]
        public async Task<ActionResult> GetDailyStaffDistributionForFrontend(int? year = null)
        {
            try
            {
                var targetYear = year ?? DateTime.Now.Year;
                var currentUserId = UserSessionHelper.GetActiveUserId(HttpContext);
                var isAdmin = IsTournamentAdmin(currentUserId);

                IQueryable<StaffScheduleEntry> entriesQuery = db.StaffScheduleEntries
                    .Where(entry => entry.ScheduleDate.Year == targetYear && entry.Deleted == null);

                // Filter based on user role - only if NOT admin
                if (!isAdmin)
                {
                    // Get tournaments user has access to
                    var accessibleTournamentIds = await GetAccessibleTournamentIds(currentUserId, targetYear);

                    // Filter entries based on tournament access
                    entriesQuery = entriesQuery.Where(entry =>
                        entry.TournamentId.HasValue &&
                        accessibleTournamentIds.Contains(entry.TournamentId.Value) &&
                        (entry.UserId == currentUserId || // User's own entries
                         db.StaffScheduleEntries.Any(managerEntry =>
                             managerEntry.UserId == currentUserId &&
                             managerEntry.TournamentId == entry.TournamentId &&
                             managerEntry.TournamentRoleId == 1 &&
                             managerEntry.Deleted == null)) // Or user is manager on this tournament
                    );
                }
                // If admin, no filtering - they see everything

                var scheduleAssignments = await (from entry in entriesQuery
                                                 join user in db.Users on entry.UserId equals user.Id
                                                 from tournament in db.Tournaments.Where(t => t.Id == entry.TournamentId).DefaultIfEmpty()
                                                 from role in db.TournamentRoles.Where(r => r.Id == entry.TournamentRoleId).DefaultIfEmpty()
                                                 where user.Deleted == null
                                                 select new
                                                 {
                                                     Date = entry.ScheduleDate,
                                                     UserId = user.Id,
                                                     StaffName = user.FirstName + " " + user.LastName,
                                                     TournamentId = entry.TournamentId,
                                                     TournamentName = tournament != null ? tournament.Name : null,
                                                     Role = entry.Role ?? role.Title ?? "General Laborer",
                                                     DailyRate = entry.DailyRate ?? role.DailyRate,
                                                     IsScheduled = entry.EntryType == 1, // Tournament assignment
                                                     IsTravelDay = entry.EntryType == 2, // Travel day
                                                     IsAdvanceWeek = entry.IsAdvanceWeek,
                                                     EntryType = entry.EntryType,
                                                     StartTime = "08:00", // Default start time
                                                     EndTime = "18:00"    // Default end time
                                                 })
                                                .OrderBy(a => a.Date)
                                                .ThenBy(a => a.StaffName)
                                                .ToListAsync();

                // Group assignments by date for daily schedule format
                var dailySchedule = scheduleAssignments
                    .GroupBy(a => a.Date.Date)
                    .Select(g => new
                    {
                        Date = g.Key,
                        Assignments = g.ToList()
                    })
                    .OrderBy(d => d.Date)
                    .ToList();

                // Also provide staff assignments list for compatibility
                var staffAssignments = scheduleAssignments
                    .GroupBy(a => a.StaffName)
                    .Select(g => new
                    {
                        StaffName = g.Key,
                        UserId = g.First().UserId,
                        Assignments = g.ToList()
                    })
                    .ToList();

                var result = new
                {
                    Year = targetYear,
                    DailySchedule = dailySchedule,
                    StaffAssignments = staffAssignments
                };

                return Json(result, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error loading staff distribution: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // JSON-compatible tournament creation for EventSchedule frontend
        [HttpPost]
        public async Task<ActionResult> CreateFromEventSchedule()
        {
            try
            {
                // Read JSON payload
                string jsonPayload = "";
                using (var reader = new System.IO.StreamReader(Request.InputStream))
                {
                    jsonPayload = reader.ReadToEnd();
                }

                var tournamentData = JsonConvert.DeserializeObject<TournamentSaveData>(jsonPayload);

                if (tournamentData == null)
                {
                    return Json(new { success = false, message = "Invalid tournament data" });
                }

                var tournament = new Tournament
                {
                    Name = tournamentData.Name,
                    Description = tournamentData.Description ?? tournamentData.Name + " Tournament",
                    StartDate = tournamentData.StartDate,
                    EndDate = tournamentData.EndDate,
                    Location = tournamentData.Location,
                    Status = 1, // Planned
                    ClientId = null, // Will need to be set later
                    AdvanceWeekStartDate = tournamentData.AdvanceWeekStartDate,
                    AdvanceWeekEndDate = tournamentData.AdvanceWeekEndDate,
                    SetupStartDate = tournamentData.SetupStartDate,
                    SetupEndDate = tournamentData.SetupEndDate,
                    TeardownStartDate = tournamentData.TeardownStartDate,
                    TeardownEndDate = tournamentData.TeardownEndDate,
                    Created = DateTime.UtcNow,
                    CreatedBy = UserSessionHelper.GetActiveUserId(HttpContext),
                    MinimumWage = tournamentData.MinimumWage,
                    PerDiem = tournamentData.PerDiem,
                    TravelPerDiem = tournamentData.TravelPerDiem,
                    BillRate = tournamentData.BillRate,
                    ManagerBillRate = tournamentData.ManagerBillRate,
                    PrintBillRate = tournamentData.PrintBillRate,
                    OTByDay = tournamentData.OTByDay
                };

                db.Tournaments.Add(tournament);
                await db.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Tournament created successfully",
                    data = new { id = tournament.Id, name = tournament.Name }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error creating tournament: " + ex.Message });
            }
        }

        // JSON-compatible tournament editing for EventSchedule frontend
        [HttpPost]
        public async Task<ActionResult> EditFromEventSchedule()
        {
            try
            {
                // Read JSON payload
                string jsonPayload = "";
                using (var reader = new System.IO.StreamReader(Request.InputStream))
                {
                    jsonPayload = reader.ReadToEnd();
                }

                var tournamentData = JsonConvert.DeserializeObject<TournamentSaveData>(jsonPayload);

                if (tournamentData == null)
                {
                    return Json(new { success = false, message = "Invalid tournament data" });
                }

                var tournament = await db.Tournaments
                    .Where(t => t.Id == tournamentData.Id && t.Deleted == null)
                    .FirstOrDefaultAsync();

                if (tournament == null)
                {
                    return Json(new { success = false, message = "Tournament not found" });
                }

                // Update tournament properties
                tournament.Name = tournamentData.Name;
                tournament.Description = tournamentData.Description ?? tournamentData.Name + " Tournament";
                tournament.StartDate = tournamentData.StartDate;
                tournament.EndDate = tournamentData.EndDate;
                tournament.Location = tournamentData.Location;
                tournament.AdvanceWeekStartDate = tournamentData.AdvanceWeekStartDate;
                tournament.AdvanceWeekEndDate = tournamentData.AdvanceWeekEndDate;
                tournament.SetupStartDate = tournamentData.SetupStartDate;
                tournament.SetupEndDate = tournamentData.SetupEndDate;
                tournament.TeardownStartDate = tournamentData.TeardownStartDate;
                tournament.TeardownEndDate = tournamentData.TeardownEndDate;
                tournament.Modified = DateTime.UtcNow;
                tournament.MinimumWage = tournamentData.MinimumWage;
                tournament.PerDiem = tournamentData.PerDiem;
                tournament.TravelPerDiem = tournamentData.TravelPerDiem;
                tournament.BillRate = tournamentData.BillRate;
                tournament.ManagerBillRate = tournamentData.ManagerBillRate;
                tournament.PrintBillRate = tournamentData.PrintBillRate;
                tournament.OTByDay = tournamentData.OTByDay;

                await db.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Tournament updated successfully",
                    data = new { id = tournament.Id, name = tournament.Name }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error updating tournament: " + ex.Message });
            }
        }

        // Event Schedule main page
        public async Task<ActionResult> EventSchedule()
        {
            var currentYear = DateTime.Now.Year;
            var currentUserId = UserSessionHelper.GetActiveUserId(HttpContext);
            var isAdmin = IsTournamentAdmin(currentUserId);

            // Get tournaments for the current year (including phase dates like Setup, Advanced Week, Teardown)
            var tournamentsQuery = db.Tournaments
                .Where(t => t.Deleted == null &&
                           (t.StartDate.Year == currentYear ||
                            t.EndDate.Year == currentYear ||
                            (t.SetupStartDate.HasValue && t.SetupStartDate.Value.Year == currentYear) ||
                            (t.SetupEndDate.HasValue && t.SetupEndDate.Value.Year == currentYear) ||
                            (t.AdvanceWeekStartDate.HasValue && t.AdvanceWeekStartDate.Value.Year == currentYear) ||
                            (t.AdvanceWeekEndDate.HasValue && t.AdvanceWeekEndDate.Value.Year == currentYear) ||
                            (t.TeardownStartDate.HasValue && t.TeardownStartDate.Value.Year == currentYear) ||
                            (t.TeardownEndDate.HasValue && t.TeardownEndDate.Value.Year == currentYear)));

            // If not admin, filter to only tournaments user has access to
            if (!isAdmin)
            {
                var accessibleTournamentIds = await GetAccessibleTournamentIds(currentUserId, currentYear);
                tournamentsQuery = tournamentsQuery.Where(t => accessibleTournamentIds.Contains(t.Id));
            }

            var tournaments = await tournamentsQuery
                .OrderBy(t => t.StartDate)
                .Select(t => new VM_TournamentSummary
                {
                    Id = t.Id,
                    Name = t.Name,
                    StartDate = t.StartDate,
                    EndDate = t.EndDate,
                    AdvanceWeekStartDate = t.AdvanceWeekStartDate,
                    AdvanceWeekEndDate = t.AdvanceWeekEndDate,
                    MinimumWage = t.MinimumWage ?? 0,
                    PerDiem = t.PerDiem ?? 0,
                    TravelPerDiem = t.TravelPerDiem ?? 0,
                    OTByDay = t.OTByDay ?? false
                })
                .ToListAsync();

            // Get event staff (users who can be assigned)
            // Include TerminationDate so frontend can filter by year
            var eventStaff = await db.Users
                .Where(u => u.Deleted == null && u.IsEventStaff == true)
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .Select(u => new VM_EventStaffMember
                {
                    UserId = u.Id,
                    FullName = u.FirstName + " " + u.LastName,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    TerminationDate = u.TerminationDate
                })
                .ToListAsync();

            var model = new VM_EventSchedule
            {
                CurrentYear = currentYear,
                Tournaments = tournaments,
                EventStaff = eventStaff,
                IsAdmin = isAdmin
            };

            return View(model);
        }

        [HttpPost]
        public async Task<ActionResult> SaveSchedule()
        {
            try
            {
                // Read JSON payload
                string jsonPayload = "";
                using (var reader = new System.IO.StreamReader(Request.InputStream))
                {
                    jsonPayload = reader.ReadToEnd();
                }

                var assignments = JsonConvert.DeserializeObject<List<TournamentScheduleAssignment>>(jsonPayload);

                if (assignments == null || assignments.Count == 0)
                {
                    return Json(new { success = true, message = "No assignments to save" });
                }

                var savedCount = 0;

                // Pre-load all data to avoid N+1 queries
                var tournamentIds = assignments.Where(a => a.TournamentId.HasValue).Select(a => a.TournamentId.Value).Distinct().ToList();
                var tournaments = await db.Tournaments.Where(t => tournamentIds.Contains(t.Id) && t.Deleted == null).ToListAsync();
                var tournamentLookup = tournaments.ToDictionary(t => t.Id);

                var roleNames = assignments.Where(a => !string.IsNullOrEmpty(a.Role)).Select(a => a.Role).Distinct().ToList();
                var roles = await db.TournamentRoles.Where(r => roleNames.Contains(r.Title) && r.Deleted == null).ToListAsync();
                var roleLookup = roles.ToDictionary(r => r.Title);

                // Get all existing entries in one query
                var userIds = assignments.Select(a => a.UserId).Distinct().ToList();
                var dates = assignments.Where(a => DateTime.TryParse(a.Date, out _)).Select(a => DateTime.Parse(a.Date).Date).Distinct().ToList();
                var existingEntries = await db.StaffScheduleEntries
                    .Where(e => userIds.Contains(e.UserId) &&
                               dates.Contains(DbFunctions.TruncateTime(e.ScheduleDate).Value) &&
                               e.Deleted == null)
                    .ToListAsync();

                foreach (var assignment in assignments)
                {
                    if (string.IsNullOrEmpty(assignment.Date) || !DateTime.TryParse(assignment.Date, out var assignmentDate))
                        continue;

                    // Use EntryType from frontend if provided (supports 3=Sick, 4=TimeOffOffsite, 5=TimeOffOnsite)
                    // Fall back to old calculation for backward compatibility
                    int entryType = assignment.EntryType > 0 ? assignment.EntryType :
                                    (assignment.IsTravel ? 2 : assignment.NotWorking ? 3 : 1);

                    int? tournamentRoleId = null;

                    // For tournament entries, validate and get role
                    if (entryType == 1 && assignment.TournamentId.HasValue)
                    {
                        if (!tournamentLookup.TryGetValue(assignment.TournamentId.Value, out var tournament))
                            continue;

                        if (!roleLookup.TryGetValue(assignment.Role, out var role))
                        {
                            role = new TournamentRole { Title = assignment.Role, DailyRate = assignment.DailyRate, Created = DateTime.Now };
                            db.TournamentRoles.Add(role);
                            await db.SaveChangesAsync();
                            roleLookup[assignment.Role] = role;
                        }
                        tournamentRoleId = role.Id;

                        assignment.IsAdvanceWeek = tournament.AdvanceWeekStartDate.HasValue &&
                                                 tournament.AdvanceWeekEndDate.HasValue &&
                                                 assignmentDate >= tournament.AdvanceWeekStartDate.Value &&
                                                 assignmentDate <= tournament.AdvanceWeekEndDate.Value;
                    }

                    // Find existing entry from pre-loaded data
                    var targetDate = assignmentDate.Date;
                    var existingEntry = existingEntries.FirstOrDefault(e =>
                        e.UserId == assignment.UserId &&
                        e.ScheduleDate.Date == targetDate);
                    // EntryType filter removed - ensures only 1 entry per user per day

                    if (existingEntry == null)
                    {
                        var entry = new StaffScheduleEntry
                        {
                            UserId = assignment.UserId,
                            ScheduleDate = assignmentDate,
                            EntryType = entryType,
                            TournamentId = assignment.TournamentId,
                            TournamentRoleId = tournamentRoleId,
                            Role = assignment.Role,
                            DailyRate = assignment.DailyRate,
                            DailyRate2 = assignment.DailyRate2,
                            // BillRate = assignment.BillRate,
                            IsAdvanceWeek = assignment.IsAdvanceWeek,
                            TravelTournamentId = assignment.TravelTournamentId,
                            Created = DateTime.Now,
                            ClockInUTC = null,
                            ClockOutUTC = null
                        };
                        db.StaffScheduleEntries.Add(entry);
                        savedCount++;
                    }
                    else
                    {
                        existingEntry.EntryType = entryType;  // Update entry type (Working/Travel/NotWorking)
                        existingEntry.TournamentId = assignment.TournamentId;
                        existingEntry.TournamentRoleId = tournamentRoleId;
                        existingEntry.Role = assignment.Role;
                        existingEntry.DailyRate = assignment.DailyRate;
                        existingEntry.DailyRate2 = assignment.DailyRate2;
                        // existingEntry.BillRate = assignment.BillRate;
                        existingEntry.IsAdvanceWeek = assignment.IsAdvanceWeek;
                        existingEntry.TravelTournamentId = assignment.TravelTournamentId;
                        existingEntry.Modified = DateTime.Now;
                        existingEntry.ClockInUTC = assignment.ClockInUTC;
                        existingEntry.ClockOutUTC = assignment.ClockOutUTC;
                        savedCount++;
                    }
                }

                await db.SaveChangesAsync();
                return Json(new { success = true, message = $"Successfully saved {savedCount} schedule assignments", count = savedCount });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error saving schedule: " + ex.Message });
            }
        }

        // Delete specific schedule assignments
        [HttpPost]
        public async Task<ActionResult> DeleteScheduleAssignments(List<ScheduleDeletionRequest> deletionRequests)
        {
            try
            {
                if (deletionRequests == null || deletionRequests.Count == 0)
                {
                    return Json(new { success = true, message = "No assignments to delete" });
                }

                var deletedCount = 0;

                foreach (var request in deletionRequests)
                {
                    if (!DateTime.TryParse(request.Date, out var targetDate))
                    {
                        continue; // Skip invalid dates
                    }

                    // Find and delete all schedule entries for this user on this date
                    var schedulesToDelete = await db.StaffScheduleEntries
                        .Where(s => s.UserId == request.UserId &&
                                   s.ScheduleDate == targetDate.Date &&
                                   s.Deleted == null)
                        .ToListAsync();

                    foreach (var schedule in schedulesToDelete)
                    {
                        //schedule.Deleted = DateTime.Now;
                        db.StaffScheduleEntries.Remove(schedule);
                        db.Entry(schedule).State = EntityState.Deleted;
                    }
                    deletedCount += schedulesToDelete.Count;
                }

                await db.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"Successfully deleted {deletedCount} schedule assignments",
                    count = deletedCount
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Error deleting schedule assignments: " + ex.Message
                });
            }
        }

        [HttpGet]
        public async Task<ActionResult> GetScheduleAssignments(int? year = null)
        {
            try
            {
                var currentYear = year ?? DateTime.Now.Year;
                var currentUserId = UserSessionHelper.GetActiveUserId(HttpContext);
                var isAdmin = IsTournamentAdmin(currentUserId);

                IQueryable<StaffScheduleEntry> entriesQuery = db.StaffScheduleEntries
                    .Where(entry => entry.ScheduleDate.Year == currentYear && entry.Deleted == null);

                // Filter based on user role - only if NOT admin
                if (!isAdmin)
                {
                    // Get tournaments user has access to
                    var accessibleTournamentIds = await GetAccessibleTournamentIds(currentUserId, currentYear);

                    // Filter entries based on tournament access
                    entriesQuery = entriesQuery.Where(entry =>
                        entry.TournamentId.HasValue &&
                        accessibleTournamentIds.Contains(entry.TournamentId.Value) &&
                        (entry.UserId == currentUserId || // User's own entries
                         db.StaffScheduleEntries.Any(managerEntry =>
                             managerEntry.UserId == currentUserId &&
                             managerEntry.TournamentId == entry.TournamentId &&
                             managerEntry.TournamentRoleId == 1 &&
                             managerEntry.Deleted == null)) // Or user is manager on this tournament
                    );
                }
                // If admin, no filtering - they see everything

                var assignmentsRaw = await (from entry in entriesQuery
                                            join user in db.Users on entry.UserId equals user.Id
                                            from tournament in db.Tournaments.Where(t => t.Id == entry.TournamentId).DefaultIfEmpty()
                                            from role in db.TournamentRoles.Where(r => r.Id == entry.TournamentRoleId).DefaultIfEmpty()
                                            from travelTournament in db.Tournaments.Where(t => t.Id == entry.TravelTournamentId).DefaultIfEmpty()
                                            select new
                                            {
                                                Date = entry.ScheduleDate,
                                                StaffName = user.FirstName + " " + user.LastName,
                                                TournamentId = entry.TournamentId,
                                                TournamentName = tournament.Name,
                                                Role = entry.Role,
                                                DailyRate = entry.DailyRate,
                                                DailyRate2 = entry.DailyRate2,
                                                // BillRate = entry.BillRate,
                                                IsTravel = entry.EntryType == 2,
                                                NotWorking = entry.EntryType >= 3, // 3=Sick, 4=TimeOffOffsite, 5=TimeOffOnsite
                                                EntryType = entry.EntryType, // Send actual EntryType value to frontend
                                                IsAdvanceWeek = entry.IsAdvanceWeek,
                                                ClockInUTC = entry.ClockInUTC,
                                                ClockOutUTC = entry.ClockOutUTC,
                                                TravelTournamentId = entry.TravelTournamentId,
                                                TravelTournamentName = travelTournament.Name
                                            })
                    .ToListAsync();

                // Format time strings after the query executes
                var assignments = assignmentsRaw.Select(a => new
                {
                    a.Date,
                    a.StaffName,
                    a.TournamentId,
                    a.TournamentName,
                    a.Role,
                    a.DailyRate,
                    a.DailyRate2,
                    // a.BillRate,
                    IsWorking = !a.IsTravel && !a.NotWorking, // EntryType 1 = working (tournament assignment)
                    a.IsTravel,
                    a.NotWorking,
                    a.EntryType, // Include EntryType so frontend knows which absence type (3=Sick, 4=TimeOff, 5=TimeOffOnsite)
                    a.IsAdvanceWeek,
                    ClockIn = a.ClockInUTC.HasValue ? a.ClockInUTC.Value.ToLocalTime().ToString("h:mm tt") : "",
                    ClockOut = a.ClockOutUTC.HasValue ? a.ClockOutUTC.Value.ToLocalTime().ToString("h:mm tt") : "",
                    a.TravelTournamentId,
                    a.TravelTournamentName
                }).ToList();

                var jsonResult = Json(new { success = true, assignments = assignments }, JsonRequestBehavior.AllowGet);
                jsonResult.MaxJsonLength = int.MaxValue;
                return jsonResult;
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error loading schedule assignments: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public async Task<ActionResult> ExportEventSchedule(int? year = null)
        {
            var exportYear = year ?? DateTime.Now.Year;
            var currentUserId = UserSessionHelper.GetActiveUserId(HttpContext);
            var isAdmin = IsTournamentAdmin(currentUserId);

            var eventStaff = await db.Users
                .Where(u => u.Deleted == null && u.IsEventStaff == true)
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .Select(u => new { u.Id, FullName = u.FirstName + " " + u.LastName })
                .ToListAsync();

            IQueryable<StaffScheduleEntry> entriesQuery = db.StaffScheduleEntries
                .Where(e => e.ScheduleDate.Year == exportYear && e.Deleted == null);

            if (!isAdmin)
            {
                var accessibleIds = await GetAccessibleTournamentIds(currentUserId, exportYear);
                entriesQuery = entriesQuery.Where(e =>
                    e.TournamentId.HasValue && accessibleIds.Contains(e.TournamentId.Value) &&
                    (e.UserId == currentUserId ||
                     db.StaffScheduleEntries.Any(m =>
                         m.UserId == currentUserId &&
                         m.TournamentId == e.TournamentId &&
                         m.TournamentRoleId == 1 &&
                         m.Deleted == null)));
            }

            var entries = await entriesQuery.ToListAsync();

            var tournamentIdsNeeded = entries
                .SelectMany(e => new[] { e.TournamentId, e.TravelTournamentId })
                .Where(id => id.HasValue)
                .Select(id => id.Value)
                .Distinct()
                .ToList();

            var tournaments = await db.Tournaments
                .Where(t => tournamentIdsNeeded.Contains(t.Id) && t.Deleted == null)
                .Select(t => new
                {
                    t.Id, t.Name,
                    t.StartDate, t.EndDate,
                    t.SetupStartDate, t.SetupEndDate,
                    t.AdvanceWeekStartDate, t.AdvanceWeekEndDate,
                    t.TeardownStartDate, t.TeardownEndDate
                })
                .ToListAsync();

            var tournamentById = tournaments.ToDictionary(t => t.Id);

            var entryLookup = new Dictionary<string, StaffScheduleEntry>();
            foreach (var e in entries)
            {
                var k = $"{e.UserId}_{e.ScheduleDate.Date:yyyyMMdd}";
                if (!entryLookup.ContainsKey(k))
                    entryLookup[k] = e;
            }

            // Same 20-color palette as EventSchedule.js getTournamentColorMap()
            var palette = new[]
            {
                Color.FromArgb(0x25, 0x63, 0xEB), Color.FromArgb(0xDC, 0x26, 0x26),
                Color.FromArgb(0x05, 0x96, 0x69), Color.FromArgb(0x7C, 0x3A, 0xED),
                Color.FromArgb(0xD9, 0x77, 0x06), Color.FromArgb(0x08, 0x91, 0xB2),
                Color.FromArgb(0xBE, 0x18, 0x5D), Color.FromArgb(0x43, 0x38, 0xCA),
                Color.FromArgb(0x65, 0xA3, 0x0D), Color.FromArgb(0x93, 0x33, 0xEA),
                Color.FromArgb(0xEA, 0x58, 0x0C), Color.FromArgb(0x0D, 0x94, 0x88),
                Color.FromArgb(0xE1, 0x1D, 0x48), Color.FromArgb(0x1D, 0x4E, 0xD8),
                Color.FromArgb(0x15, 0x80, 0x3D), Color.FromArgb(0xA2, 0x1C, 0xAF),
                Color.FromArgb(0xCA, 0x8A, 0x04), Color.FromArgb(0x63, 0x66, 0xF1),
                Color.FromArgb(0x16, 0xA3, 0x4A), Color.FromArgb(0xDB, 0x27, 0x77),
            };

            // Assign colors by sorted tournament ID (matches JS behavior)
            var sortedTournamentIds = tournaments.Select(t => t.Id).OrderBy(id => id).ToList();
            var tournamentColors = sortedTournamentIds
                .Select((id, idx) => new { id, color = palette[idx % palette.Length] })
                .ToDictionary(x => x.id, x => x.color);

            Color TournamentColor(int? id) =>
                id.HasValue && tournamentColors.TryGetValue(id.Value, out var c) ? c : Color.FromArgb(201, 218, 248);

            Color TextColor(Color bg)
            {
                var luminance = (0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B) / 255.0;
                return luminance > 0.6 ? Color.Black : Color.White;
            }

            ExcelPackage.License.SetNonCommercialOrganization("Fifth Color");
            using (var package = new ExcelPackage())
            {
                var ws = package.Workbook.Worksheets.Add($"Event Schedule {exportYear}");

                ws.Cells[1, 1].Value = "Date";
                for (int i = 0; i < eventStaff.Count; i++)
                    ws.Cells[1, i + 2].Value = eventStaff[i].FullName;

                using (var header = ws.Cells[1, 1, 1, eventStaff.Count + 1])
                {
                    header.Style.Font.Bold = true;
                    header.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    header.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(0x43, 0x4C, 0x5E));
                    header.Style.Font.Color.SetColor(Color.White);
                }

                var row = 2;
                for (var date = new DateTime(exportYear, 1, 1); date.Year == exportYear; date = date.AddDays(1))
                {
                    ws.Cells[row, 1].Value = date.ToString("ddd MM/dd/yyyy");

                    for (int col = 0; col < eventStaff.Count; col++)
                    {
                        var k = $"{eventStaff[col].Id}_{date:yyyyMMdd}";
                        if (!entryLookup.TryGetValue(k, out var entry)) continue;

                        string label;
                        Color bgColor;

                        if (entry.EntryType == 2)
                        {
                            var travelId = entry.TravelTournamentId ?? entry.TournamentId;
                            var tName = travelId.HasValue && tournamentById.TryGetValue(travelId.Value, out var tt) ? tt.Name : null;
                            label = tName != null ? $"{tName} - Travel" : "Travel";
                            bgColor = TournamentColor(travelId);
                        }
                        else if (entry.EntryType == 3)
                        {
                            label = "Sick";
                            bgColor = Color.FromArgb(244, 204, 204);
                        }
                        else if (entry.EntryType == 4)
                        {
                            label = "Time Off (Offsite)";
                            bgColor = Color.FromArgb(244, 204, 204);
                        }
                        else if (entry.EntryType == 5)
                        {
                            label = "Time Off (Onsite)";
                            bgColor = Color.FromArgb(244, 204, 204);
                        }
                        else if (entry.TournamentId.HasValue && tournamentById.TryGetValue(entry.TournamentId.Value, out var t))
                        {
                            var d = date.Date;

                            // Use ToUniversalTime().Date to match parseNetDate() in EventScheduleAPI.js,
                            // which extracts UTC year/month/day to avoid CDT offset shifting dates by one day.
                            DateTime UtcDate(DateTime dt) => dt.ToUniversalTime().Date;
                            DateTime? UtcDateN(DateTime? dt) => dt.HasValue ? dt.Value.ToUniversalTime().Date : (DateTime?)null;

                            var tStart     = UtcDate(t.StartDate);
                            var tEnd       = UtcDate(t.EndDate);
                            var setupStart = UtcDateN(t.SetupStartDate) ?? tStart.AddDays(-21);
                            var setupEnd   = UtcDateN(t.SetupEndDate)
                                               ?? (t.AdvanceWeekStartDate.HasValue
                                                   ? UtcDate(t.AdvanceWeekStartDate.Value).AddDays(-1)
                                                   : tStart.AddDays(-8));
                            var advStart   = UtcDateN(t.AdvanceWeekStartDate);
                            var advEnd     = UtcDateN(t.AdvanceWeekEndDate) ?? tStart.AddDays(-1);
                            var tdStart    = UtcDateN(t.TeardownStartDate) ?? tEnd.AddDays(1);
                            var tdEnd      = UtcDateN(t.TeardownEndDate) ?? tEnd.AddDays(6);

                            string phase;
                            if (d >= setupStart && d <= setupEnd)
                                phase = "Setup";
                            else if (advStart.HasValue && d >= advStart.Value && d <= advEnd)
                                phase = "Advanced";
                            else if (d >= tStart && d <= tEnd)
                                phase = "Event Date";
                            else if (d >= tdStart && d <= tdEnd)
                                phase = "Teardown";
                            else
                                phase = "Setup";

                            label = $"{t.Name} - {phase}";
                            bgColor = TournamentColor(entry.TournamentId);
                        }
                        else
                        {
                            label = "Assigned";
                            bgColor = Color.FromArgb(201, 218, 248);
                        }

                        // Append role and clock times as additional lines
                        if (!string.IsNullOrWhiteSpace(entry.Role))
                            label += "\n" + entry.Role;

                        if (entry.ClockInUTC.HasValue || entry.ClockOutUTC.HasValue)
                        {
                            var clockIn = entry.ClockInUTC.HasValue ? entry.ClockInUTC.Value.ToLocalTime().ToString("h:mm tt") : "?";
                            var clockOut = entry.ClockOutUTC.HasValue ? entry.ClockOutUTC.Value.ToLocalTime().ToString("h:mm tt") : "?";
                            label += $"\n{clockIn} - {clockOut}";
                        }

                        var cell = ws.Cells[row, col + 2];
                        cell.Value = label;
                        cell.Style.WrapText = true;
                        cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        cell.Style.Fill.BackgroundColor.SetColor(bgColor);
                        cell.Style.Font.Color.SetColor(TextColor(bgColor));
                    }
                    row++;
                }

                ws.View.FreezePanes(2, 2);
                ws.Cells[ws.Dimension.Address].AutoFitColumns();

                var bytes = package.GetAsByteArray();
                return File(bytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"EventSchedule_{exportYear}.xlsx");
            }
        }

        [HttpPost]
        public async Task<ActionResult> SaveClockTimes()
        {
            try
            {
                // Read JSON payload (following SaveSchedule pattern)
                string jsonPayload = "";
                using (var reader = new System.IO.StreamReader(Request.InputStream))
                {
                    jsonPayload = reader.ReadToEnd();
                }

                var clockTimeUpdates = JsonConvert.DeserializeObject<List<ClockTimeAssignment>>(jsonPayload);

                if (clockTimeUpdates == null || clockTimeUpdates.Count == 0)
                {
                    return Json(new { success = true, message = "No clock times to save" });
                }

                var updatedCount = 0;

                foreach (var update in clockTimeUpdates)
                {
                    // Find existing entry for this user, tournament, and date
                    var entry = await db.StaffScheduleEntries
                        .FirstOrDefaultAsync(e => e.UserId == update.UserId &&
                                                 e.TournamentId == update.TournamentId &&
                                                 e.ScheduleDate.Year == update.Date.Year &&
                                                 e.ScheduleDate.Month == update.Date.Month &&
                                                 e.ScheduleDate.Day == update.Date.Day &&
                                                 e.Deleted == null);

                    if (entry != null)
                    {
                        // Update clock times if provided
                        if (!string.IsNullOrEmpty(update.ClockInTime))
                        {
                            if (TimeSpan.TryParse(update.ClockInTime, out var clockInTime))
                            {
                                // Create DateTime in local timezone and convert to UTC
                                var localDateTime = DateTime.SpecifyKind(entry.ScheduleDate.Date + clockInTime, DateTimeKind.Local);
                                entry.ClockInUTC = localDateTime.ToUniversalTime();
                            }
                        }

                        if (!string.IsNullOrEmpty(update.ClockOutTime))
                        {
                            if (TimeSpan.TryParse(update.ClockOutTime, out var clockOutTime))
                            {
                                // Create DateTime in local timezone and convert to UTC
                                var localDateTime = DateTime.SpecifyKind(entry.ScheduleDate.Date + clockOutTime, DateTimeKind.Local);
                                entry.ClockOutUTC = localDateTime.ToUniversalTime();
                            }
                        }

                        entry.Modified = DateTime.UtcNow;
                        updatedCount++;
                    }
                }

                await db.SaveChangesAsync();

                return Json(new { success = true, message = $"Updated clock times for {updatedCount} entries" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error saving clock times: " + ex.Message });
            }
        }

        // Helper method to get default daily rate for a role
        private decimal GetDefaultRateForRole(string roleName)
        {
            if (string.IsNullOrEmpty(roleName)) return 350.00m;

            switch (roleName.ToLower().Trim())
            {
                case "manager":
                    return 500.00m;

                case "printer":
                    return 450.00m;

                case "general laborer":
                case "general":
                case "laborer":
                    return 350.00m;

                default:
                    return 350.00m; // Default to general laborer rate
            }
        }

        // Helper method to check if user is Tournament Admin (TournamentUserRole.Id = 1)
        private bool IsTournamentAdmin(int userId)
        {
            return db.TournamentUserRolePivots
                .Any(p => p.User == userId && p.Role == 1);
        }

        // Helper method to check if user is a Manager on a specific tournament (TournamentRole.Id = 1)
        private bool IsManagerOnTournament(int userId, int tournamentId)
        {
            return db.StaffScheduleEntries
                .Any(e => e.UserId == userId &&
                         e.TournamentId == tournamentId &&
                         e.TournamentRoleId == 1 &&
                         e.Deleted == null);
        }

        // Helper method to get tournaments user has access to
        private async Task<List<int>> GetAccessibleTournamentIds(int userId, int year)
        {
            // Get all tournaments user has schedule entries for in the given year
            return await db.StaffScheduleEntries
                .Where(e => e.UserId == userId &&
                           e.ScheduleDate.Year == year &&
                           e.TournamentId.HasValue &&
                           e.Deleted == null)
                .Select(e => e.TournamentId.Value)
                .Distinct()
                .ToListAsync();
        }

        // Template Schedule Management Methods
        [HttpGet]
        public async Task<ActionResult> GetTemplateAssignments(int year)
        {
            try
            {
                var assignments = await db.ScheduleTemplates
                    .Where(st => st.Date.Year == year)
                    .Include(st => st.Tournament)
                    .Select(st => new
                    {
                        TemplateName = st.TemplateName,
                        Date = st.Date,
                        TournamentId = st.TournamentId,
                        TournamentName = st.Tournament != null ? st.Tournament.Name : null,
                        Role = st.Role,
                        DailyRate = st.DailyRate,
                        DailyRate2 = st.DailyRate2,
                        IsTravel = st.IsTravel,
                        IsAdvanceWeek = st.IsAdvanceWeek,
                        IsWorking = st.IsWorking,
                        NotWorking = st.NotWorking
                    })
                    .OrderBy(st => st.TemplateName)
                    .ThenBy(st => st.Date)
                    .ToListAsync();

                return Json(new { success = true, assignments = assignments }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public async Task<ActionResult> SaveTemplateSchedule()
        {
            try
            {
                // Read JSON payload
                string jsonPayload = "";
                using (var reader = new System.IO.StreamReader(Request.InputStream))
                {
                    jsonPayload = reader.ReadToEnd();
                }

                var assignments = JsonConvert.DeserializeObject<List<ScheduleTemplateAssignment>>(jsonPayload);

                if (assignments == null || assignments.Count == 0)
                {
                    return Json(new { success = true, message = "No template assignments to save" });
                }

                var savedCount = 0;

                // Group by template name to handle each template separately
                var templateGroups = assignments.GroupBy(a => a.TemplateName);

                foreach (var templateGroup in templateGroups)
                {
                    var templateName = templateGroup.Key;

                    // Clear existing assignments for this template
                    var existingAssignments = await db.ScheduleTemplates
                        .Where(st => st.TemplateName == templateName)
                        .ToListAsync();

                    if (existingAssignments.Any())
                    {
                        db.ScheduleTemplates.RemoveRange(existingAssignments);
                    }

                    // Add new assignments
                    foreach (var assignment in templateGroup)
                    {
                        if (string.IsNullOrEmpty(assignment.Date) || !DateTime.TryParse(assignment.Date, out var assignmentDate))
                            continue;

                        var templateAssignment = new ScheduleTemplate
                        {
                            TemplateName = assignment.TemplateName,
                            Date = assignmentDate,
                            TournamentId = assignment.TournamentId,
                            Role = assignment.Role,
                            DailyRate = assignment.DailyRate,
                            DailyRate2 = assignment.DailyRate2,
                            // BillRate = assignment.BillRate,
                            IsTravel = assignment.IsTravel,
                            IsAdvanceWeek = assignment.IsAdvanceWeek,
                            IsWorking = assignment.IsWorking,
                            NotWorking = assignment.NotWorking,
                            CreatedDate = DateTime.UtcNow,
                            ModifiedDate = DateTime.UtcNow
                        };

                        db.ScheduleTemplates.Add(templateAssignment);
                        savedCount++;
                    }
                }

                await db.SaveChangesAsync();

                return Json(new { success = true, message = $"Successfully saved {savedCount} template assignments" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Delete specific template schedule assignments
        [HttpPost]
        public async Task<ActionResult> DeleteScheduleTemplateAssignments(List<ScheduleTemplateDeletionRequest> deletionRequests)
        {
            try
            {
                if (deletionRequests == null || deletionRequests.Count == 0)
                {
                    return Json(new { success = true, message = "No template assignments to delete" });
                }

                var deletedCount = 0;

                foreach (var request in deletionRequests)
                {
                    if (string.IsNullOrEmpty(request.TemplateName) || !DateTime.TryParse(request.Date, out var targetDate))
                    {
                        continue; // Skip invalid requests
                    }

                    // Find and delete all template entries for this template name on this date
                    var templatesToDelete = await db.ScheduleTemplates
                        .Where(st => st.TemplateName == request.TemplateName &&
                                    st.Date == targetDate.Date)
                        .ToListAsync();

                    foreach (var template in templatesToDelete)
                    {
                        db.ScheduleTemplates.Remove(template);
                        db.Entry(template).State = EntityState.Deleted;
                    }
                    deletedCount += templatesToDelete.Count;
                }

                await db.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"Successfully deleted {deletedCount} template assignments",
                    count = deletedCount
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Error deleting template assignments: " + ex.Message
                });
            }
        }

        // Executive Budgeting API
        [HttpGet]
        public async Task<ActionResult> GetExecutiveBudgetSummary(int? year = null, int? clientId = null)
        {
            try
            {
                var targetYear = year ?? DateTime.Now.Year;

                // Get tournaments for the year with optional client filter
                var tournamentsQuery = db.Tournaments
                    .Where(t => t.Deleted == null &&
                               (t.StartDate.Year == targetYear || t.EndDate.Year == targetYear));

                if (clientId.HasValue && clientId.Value > 0)
                {
                    tournamentsQuery = tournamentsQuery.Where(t => t.ClientId == clientId.Value);
                }

                var tournaments = await tournamentsQuery
                    .Include(t => t.Client)
                    .OrderBy(t => t.StartDate)
                    .ToListAsync();

                var budgetSummaries = new List<ExecutiveBudgetSummary>();

                foreach (var tournament in tournaments)
                {
                    // Get labor costs from existing GetTournamentCosts logic
                    var scheduleEntries = await db.StaffScheduleEntries
                        .Where(a => a.TournamentId == tournament.Id && a.Deleted == null)
                        .Include(a => a.User)
                        .ToListAsync();

                    var assignments = scheduleEntries
                        .GroupBy(e => new { e.UserId, e.TournamentId })
                        .Select(g => new
                        {
                            User = g.First().User,
                            TournamentRole = new { Title = g.First().Role, DailyRate = g.First().DailyRate },
                            ScheduleEntries = g.Where(x => x.EntryType == 1).ToList(),
                            TravelEntries = g.Where(x => x.EntryType == 2).ToList()
                        }).ToList();

                    // Calculate labor costs
                    var advanceWeekLaborCost = assignments.Sum(a =>
                        a.ScheduleEntries.Count(s => s.IsAdvanceWeek) * (a.TournamentRole.DailyRate ?? 0));
                    var tournamentWeekLaborCost = assignments.Sum(a =>
                        a.ScheduleEntries.Count(s => !s.IsAdvanceWeek) * (a.TournamentRole.DailyRate ?? 0));
                    var perDiemCost = assignments.Sum(a => a.TravelEntries.Sum(t => t.DailyRate ?? 0));

                    // Get material and freight costs for this tournament's client
                    decimal materialCosts = 0;
                    decimal freightCosts = 0;

                    if (tournament.ClientId.HasValue)
                    {
                        var clientIdStr = tournament.ClientId.Value.ToString();

                        // Get time and material costs for this client
                        var timeAndCosts = db.Billing_EventClientTimeAndMaterialCostsByItem(clientIdStr).ToList();

                        // Filter by jobs that might be related to this tournament (by date proximity)
                        // Since we don't have JobStartDate in the stored proc result, we'll get jobs from the Job table
                        var jobIds = timeAndCosts.Select(tc => tc.JobId).Distinct().ToList();

                        DateTime startThirty = tournament.StartDate.AddDays(-30);
                        DateTime endThirty = tournament.EndDate.AddDays(30);

                        var relevantJobs = await db.Jobs
                            .Where(j => jobIds.Contains(j.Id) &&
                                       j.Deleted == null &&
                                       ((j.MinStartDate.HasValue && j.MinStartDate.Value >= startThirty && j.MinStartDate.Value <= endThirty) ||
                                        (j.DueDate.HasValue && j.DueDate.Value >= startThirty && j.DueDate.Value <= endThirty)))
                            .Select(j => j.Id)
                            .ToListAsync();

                        //var relevantJobs = await db.Jobs
                        //    .Where(j => jobIds.Contains(j.Id) &&
                        //               j.Deleted == null &&
                        //               ((j.MinStartDate.HasValue && j.MinStartDate.Value >= tournament.StartDate.AddDays(-30) && j.MinStartDate.Value <= tournament.EndDate.AddDays(30)) ||
                        //                (j.DueDate.HasValue && j.DueDate.Value >= tournament.StartDate.AddDays(-30) && j.DueDate.Value <= tournament.EndDate.AddDays(30))))
                        //    .Select(j => j.Id)
                        //    .ToListAsync();

                        // Calculate material costs for relevant jobs
                        materialCosts = timeAndCosts
                            .Where(tc => relevantJobs.Contains(tc.JobId))
                            .Sum(tc => tc.ActualMaterialCost ?? 0);

                        // Get freight costs
                        var freightCostsList = await db.Billing_FreightCostByItem(clientIdStr).ToListAsync();
                        var relevantJobItems = timeAndCosts
                            .Where(tc => relevantJobs.Contains(tc.JobId))
                            .Select(tc => tc.JobItemId)
                            .ToList();

                        freightCosts = freightCostsList
                            .Where(fc => relevantJobItems.Contains(fc.JobItemId))
                            .Sum(fc => fc.TotalFreightCost);
                    }

                    var totalDirectCosts = advanceWeekLaborCost + tournamentWeekLaborCost + perDiemCost + materialCosts + freightCosts;

                    var budgetSummary = new ExecutiveBudgetSummary
                    {
                        TournamentId = tournament.Id,
                        TournamentName = tournament.Name,
                        StartDate = tournament.StartDate,
                        EndDate = tournament.EndDate,
                        Location = tournament.Location,
                        ClientName = tournament.Client?.Title ?? "No Client",
                        ClientId = tournament.ClientId,
                        Status = (TournamentStatus)tournament.Status,

                        // Labor Costs
                        AdvanceWeekLaborCost = advanceWeekLaborCost,
                        TournamentWeekLaborCost = tournamentWeekLaborCost,
                        TotalLaborCost = advanceWeekLaborCost + tournamentWeekLaborCost,
                        PerDiemCost = perDiemCost,

                        // Material/Job Costs
                        MaterialCosts = materialCosts,
                        FreightCosts = freightCosts,

                        // Combined Totals
                        TotalDirectCosts = totalDirectCosts,
                        ProjectMargin = totalDirectCosts > 0 ? (totalDirectCosts * 0.25m) : 0, // 25% margin estimate
                        ProfitabilityIndex = totalDirectCosts > 0 ? (totalDirectCosts * 1.25m) : 0,

                        // Staff metrics
                        TotalStaffCount = assignments.Count,
                        TotalLaborDays = assignments.Sum(a => a.ScheduleEntries.Count)
                    };

                    budgetSummaries.Add(budgetSummary);
                }

                var result = new
                {
                    Year = targetYear,
                    ClientId = clientId,
                    TotalTournaments = budgetSummaries.Count,
                    TotalBudget = budgetSummaries.Sum(b => b.ProfitabilityIndex),
                    TotalDirectCosts = budgetSummaries.Sum(b => b.TotalDirectCosts),
                    TotalLaborCosts = budgetSummaries.Sum(b => b.TotalLaborCost),
                    TotalMaterialCosts = budgetSummaries.Sum(b => b.MaterialCosts),
                    TotalFreightCosts = budgetSummaries.Sum(b => b.FreightCosts),
                    Tournaments = budgetSummaries
                };

                return Json(result, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error loading executive budget summary: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public ActionResult ExecutiveBudget()
        {
            ViewBag.PageTitle = "Executive Budget Dashboard";
            ViewBag.CurrentYear = DateTime.Now.Year;
            return View();
        }
    }

    // Data model for receiving schedule assignments from frontend
    public class TournamentScheduleAssignment
    {
        public int UserId { get; set; }
        public int? TournamentId { get; set; } // Nullable for travel/not working days
        public string Date { get; set; }
        public string Role { get; set; }
        public decimal DailyRate { get; set; }
        public decimal? DailyRate2 { get; set; } // New field for daily rate popup
        // public decimal? BillRate { get; set; }
        public bool IsTravel { get; set; }
        public bool IsAdvanceWeek { get; set; }
        public bool IsWorking { get; set; } = true; // Default to working
        public bool NotWorking { get; set; } = false; // Default to working

        public DateTime? ClockInUTC { get; set; }
        public DateTime? ClockOutUTC { get; set; }

        // EntryType: 1=Tournament, 2=Travel, 3=Sick, 4=TimeOffOffsite, 5=TimeOffOnsite
        public int EntryType { get; set; }

        // TravelTournamentId: For travel days between tournaments (NULL for Home travel)
        public int? TravelTournamentId { get; set; }
    }

    // Data model for receiving tournament data from frontend
    public class TournamentSaveData
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Location { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime? SetupStartDate { get; set; }
        public DateTime? SetupEndDate { get; set; }
        public DateTime? AdvanceWeekStartDate { get; set; }
        public DateTime? AdvanceWeekEndDate { get; set; }
        public DateTime? TeardownStartDate { get; set; }
        public DateTime? TeardownEndDate { get; set; }
        public int Status { get; set; }
        public int ClientId { get; set; }

        public string Description { get; set; }
        public decimal MinimumWage { get; set; }
        public decimal PerDiem { get; set; }
        public decimal TravelPerDiem { get; set; }
        public decimal? BillRate { get; set; }
        public decimal? ManagerBillRate { get; set; }
        public decimal? PrintBillRate { get; set; }
        public bool OTByDay { get; set; }
    }

    // Data model for schedule deletion requests
    public class ScheduleDeletionRequest
    {
        public int UserId { get; set; }
        public string Date { get; set; }
    }

    // Data model for receiving template schedule assignments from frontend
    public class ScheduleTemplateAssignment
    {
        public string TemplateName { get; set; }
        public string Date { get; set; }
        public int? TournamentId { get; set; } // Nullable for travel/not working days
        public string Role { get; set; }
        public decimal? DailyRate { get; set; }
        public decimal? DailyRate2 { get; set; }
        // public decimal? BillRate { get; set; }
        public bool IsTravel { get; set; } = false;
        public bool IsAdvanceWeek { get; set; } = false;
        public bool IsWorking { get; set; } = true;
        public bool NotWorking { get; set; } = false;
    }

    // Data model for template deletion requests
    public class ScheduleTemplateDeletionRequest
    {
        public string TemplateName { get; set; }
        public string Date { get; set; }
    }

    // Executive Budget Summary Data Model
    public class ExecutiveBudgetSummary
    {
        public int TournamentId { get; set; }
        public string TournamentName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Location { get; set; }
        public string ClientName { get; set; }
        public int? ClientId { get; set; }
        public TournamentStatus Status { get; set; }

        // Labor Costs from Tournament System
        public decimal AdvanceWeekLaborCost { get; set; }

        public decimal TournamentWeekLaborCost { get; set; }
        public decimal TotalLaborCost { get; set; }
        public decimal PerDiemCost { get; set; }

        // Material/Job Costs from Billing System
        public decimal MaterialCosts { get; set; }

        public decimal FreightCosts { get; set; }

        // Combined Totals
        public decimal TotalDirectCosts { get; set; }

        public decimal ProjectMargin { get; set; }
        public decimal ProfitabilityIndex { get; set; }

        // Staff Metrics
        public int TotalStaffCount { get; set; }

        public int TotalLaborDays { get; set; }
    }

    public class ClockTimeAssignment
    {
        public int UserId { get; set; }
        public int TournamentId { get; set; }
        public DateTime Date { get; set; }
        public string ClockInTime { get; set; }  // Format: "09:00"
        public string ClockOutTime { get; set; } // Format: "17:00"
    }
}