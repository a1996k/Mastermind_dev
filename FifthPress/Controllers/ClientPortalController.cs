using FifthPress.ActionFilters;
using FifthPress.Attributes;
using FifthPress.Models;
using FifthPress.Models.ViewModels;
using FifthPress.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.Controllers
{
    /// <summary>
    /// API endpoints for external server access to client portal data.
    /// All endpoints are protected with ClientCookieFilter (except Verify which creates the session).
    /// Extends DataController for db access but overrides OnActionExecuting to skip Mastermind session requirements.
    /// </summary>
    public class ClientPortalController : DataController
    {
        // Configuration: Client cookie timeout duration (4 hours = 240 minutes)
        private readonly int ClientCookieTimeoutMins = 240;

        /// <summary>
        /// Override to skip Mastermind session requirements - ClientPortal uses ClientActiveUser cookie instead.
        /// Do NOT call base.OnActionExecuting() as it requires ActiveUser cookie.
        /// Also handles CORS for cross-origin requests from external apps (Proofs, ClientView).
        /// </summary>
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            // Intentionally do not call base which requires Mastermind auth
            // v2 - deployed check

            // Handle CORS for external app requests
            var origin = Request.Headers["Origin"];
            if (!string.IsNullOrEmpty(origin))
            {
                Response.Headers["Access-Control-Allow-Origin"] = origin;
                Response.Headers["Access-Control-Allow-Credentials"] = "true";
                Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
                Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, X-Requested-With, X-Signature, X-Timestamp, X-Nonce";
            }

            // Handle preflight OPTIONS requests
            if (Request.HttpMethod == "OPTIONS")
            {
                Response.StatusCode = 200;
                Response.End();
            }
        }

        // Helper to return JSON with ISO dates
        private ActionResult JsonWithISODates(object data)
        {
            var settings = new JsonSerializerSettings
            {
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                DateTimeZoneHandling = DateTimeZoneHandling.Utc
            };
            var json = JsonConvert.SerializeObject(data, settings);
            return Content(json, "application/json");
        }

        /// <summary>
        /// Helper to send JSON response with explicit status code.
        /// Required for endpoints that don't use Mastermind auth - without explicit 200, responses may return 500.
        /// </summary>
        private ActionResult SendJsonResponse(object data, int statusCode = 200)
        {
            Response.Clear();
            Response.StatusCode = statusCode;
            Response.ContentType = "application/json";
            Response.Write(JsonConvert.SerializeObject(data));
            HttpContext.ApplicationInstance.CompleteRequest();
            return new EmptyResult();
        }

        // GET: ClientLogin/Verify
        public async Task<ActionResult> Verify(string token)
        {
            try
            {
                if (string.IsNullOrEmpty(token))
                {
                    return SendJsonResponse(new
                    {
                        success = false,
                        message = "Invalid link. No token provided."
                    });
                }

                var clientLogin = await db.ClientLogins
                    .Include(cl => cl.ClientLoginPivots.Select(p => p.Client))
                    .FirstOrDefaultAsync(cl => cl.LoginToken == token && cl.Deleted == null);

                if (clientLogin == null)
                {
                    return SendJsonResponse(new
                    {
                        success = false,
                        message = "This link is invalid or has already been used."
                    });
                }

                // Check if token has expired
                if (!clientLogin.TokenExpiration.HasValue || clientLogin.TokenExpiration.Value < DateTime.UtcNow)
                {
                    return SendJsonResponse(new
                    {
                        success = false,
                        message = "This link has expired. Please request a new one."
                    });
                }

                // Check if account is active
                if (!clientLogin.IsActive)
                {
                    return SendJsonResponse(new
                    {
                        success = false,
                        message = "This account is currently inactive. Please contact support."
                    });
                }

                // Extend token expiration (sliding window)
                clientLogin.TokenExpiration = DateTime.UtcNow.AddMinutes(ClientCookieTimeoutMins);
                clientLogin.LastLoginDate = DateTime.UtcNow;
                await db.SaveChangesAsync();

                // Create session cookie (separate from Mastermind)
                var isHttps = Request.IsSecureConnection ||
                    Request.Headers["X-Forwarded-Proto"]?.Equals("https", StringComparison.OrdinalIgnoreCase) == true;

                // PRODUCTION VERSION (commented for local testing)
                var clientCookie = new HttpCookie("ClientActiveUser")
                {
                    Value = clientLogin.Id.ToString(),
                    Expires = DateTime.Now.AddMinutes(ClientCookieTimeoutMins),
                    Secure = isHttps,
                    HttpOnly = true,
                    Domain = ".fifthcolor.com",
                    Path = "/",
                    SameSite = isHttps ? SameSiteMode.None : SameSiteMode.Lax
                };

                // LOCAL TESTING VERSION (remove domain restriction, allow HTTP)
                //var clientCookie = new HttpCookie("ClientActiveUser")
                //{
                //    Value = clientLogin.Id.ToString(),
                //    Expires = DateTime.Now.AddMinutes(ClientCookieTimeoutMins),
                //    Secure = false, // Allow HTTP for local testing
                //    HttpOnly = true,
                //    Domain = null, // Works with localhost
                //    Path = "/",
                //    SameSite = SameSiteMode.Lax // Lax for HTTP
                //};

                // Only set domain for production - localhost needs no domain restriction for cookies to work in dev
                if (!Request.Url.Host.Contains("localhost"))
                {
                    clientCookie.Domain = ".fifthcolor.com";
                }

                Response.SetCookie(clientCookie);

                return SendJsonResponse(new
                {
                    success = true,
                    clientLoginId = clientLogin.Id,
                    name = clientLogin.Name,
                    message = "Authentication successful"
                });
            }
            catch (Exception ex)
            {
                return SendJsonResponse(new
                {
                    success = false,
                    message = $"An error occurred: {ex.Message}"
                }, 500);
            }
        }

        // POST: ClientPortal/RequestAccess
        // API endpoint for client portal frontend to request magic link (no Mastermind auth required)
        // Use ?app=proofs to send magic link to Proofs app instead of ClientView
        [HttpPost]
        public async Task<ActionResult> RequestAccess(string email, string app = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                {
                    return SendJsonResponse(new { success = false, message = "Please enter an email address" });
                }

                // Look up email in ClientLoginEmail table
                var clientLoginEmail = await db.ClientLoginEmails
                    .Include(e => e.ClientLogin)
                    .Include(e => e.ClientLogin.ClientLoginPivots.Select(p => p.Client))
                    .FirstOrDefaultAsync(e => e.Email.Equals(email, StringComparison.OrdinalIgnoreCase) && e.Deleted == null);

                if (clientLoginEmail == null)
                {
                    return SendJsonResponse(new { success = false, message = "Invalid email address entered" });
                }

                var clientLogin = clientLoginEmail.ClientLogin;

                if (clientLogin.Deleted != null)
                {
                    return SendJsonResponse(new { success = false, message = "Invalid email address entered" });
                }

                // Generate new token
                clientLogin.LoginToken = Guid.NewGuid().ToString();
                clientLogin.TokenExpiration = DateTime.UtcNow.AddMinutes(ClientCookieTimeoutMins);
                await db.SaveChangesAsync();

                // Determine which app is requesting - use ProofsViewBaseUrl for proofs app, otherwise ClientViewBaseUrl
                string baseUrl;
                if (string.Equals(app, "proofs", StringComparison.OrdinalIgnoreCase))
                {
                    baseUrl = System.Configuration.ConfigurationManager.AppSettings["ProofsViewBaseUrl"];
                }
                else
                {
                    baseUrl = System.Configuration.ConfigurationManager.AppSettings["ClientViewBaseUrl"];
                }
                var clientViewLink = $"{baseUrl}/verify?token={clientLogin.LoginToken}";

                // Get associated client names
                var clientNames = clientLogin.ClientLoginPivots
                    .Where(p => p.Deleted == null)
                    .Select(p => p.Client.Title)
                    .ToList();

                // Create view model for email template
                var emailViewModel = new Models.ViewModels.ClientViewAuthEmailViewModel
                {
                    ClientViewLink = clientViewLink,
                    ClientList = clientNames.Any() ? string.Join(", ", clientNames) : "your events",
                    ExpirationTime = clientLogin.TokenExpiration.Value.ToLocalTime().ToString("h:mm tt")
                };

                // Render the re-auth email template (RequestAccess is always a re-request)
                string emailBody = RenderViewToString("~/Views/EmailTemplates/ClientViewReauthEmail.cshtml", emailViewModel);

                // Send email
                var emailSender = new ClientLoginMagicLinkEmail();
                await emailSender.SendClientViewAuthEmail(email, emailBody);

                return SendJsonResponse(new
                {
                    success = true,
                    message = $"A new magic link has been sent to {email}"
                });
            }
            catch (Exception ex)
            {
                return SendJsonResponse(new { success = false, message = $"An error occurred: {ex.Message}" }, 500);
            }
        }

        // GET: /ClientPortal/GetEvents?clientId={clientId}
        [ClientCookieFilter]
        public async Task<ActionResult> GetEvents(int? clientId = null)
        {
            var query = db.Clients.Where(c => c.IsGolfClient == true && c.Deleted == null);

            if (clientId.HasValue)
            {
                query = query.Where(c => c.Id == clientId.Value);
            }

            var clientsWithTournaments = await query
                .Select(c => new
                {
                    Client = c,
                    Tournament = c.Tournaments.FirstOrDefault(t => t.Deleted == null)
                })
                .ToListAsync();

            var events = new List<Event>();
            foreach (var item in clientsWithTournaments)
            {
                events.Add(await MapToEvent(item.Client, item.Tournament));
            }

            return JsonWithISODates(events);
        }

        // GET: /ClientPortal/GetEventYears?clientLoginId={clientLoginId}
        // Returns distinct years that have tournaments for this login (lightweight query)
        [ClientCookieFilter]
        public async Task<ActionResult> GetEventYears(int clientLoginId)
        {
            var login = await db.ClientLogins
                .FirstOrDefaultAsync(cl => cl.Id == clientLoginId && cl.IsActive && cl.Deleted == null);

            if (login == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound, "Login not found or inactive");
            }

            var clientIds = await db.ClientLoginPivots
                .Where(clp => clp.ClientLoginId == clientLoginId && clp.Deleted == null)
                .Select(clp => clp.ClientId)
                .ToListAsync();

            if (!clientIds.Any())
            {
                return Json(new List<int>(), JsonRequestBehavior.AllowGet);
            }

            // Resolve which of the linked clients are actually golf clients
            var golfClientIds = await db.Clients
                .Where(c => clientIds.Contains(c.Id) && c.IsGolfClient == true && c.Deleted == null)
                .Select(c => c.Id)
                .ToListAsync();


            var years = await db.Tournaments
                .Where(t => t.ClientId.HasValue && golfClientIds.Contains(t.ClientId.Value) && t.Deleted == null)
                .Select(t => t.StartDate.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();

            return Json(years, JsonRequestBehavior.AllowGet);
        }

        // GET: /ClientPortal/GetEventsForLogin?clientLoginId={clientLoginId}
        [ClientCookieFilter]
        public async Task<ActionResult> GetEventsForLogin(int clientLoginId, int? year = null)
        {
            // Verify the login exists and is active
            var login = await db.ClientLogins
                .FirstOrDefaultAsync(cl => cl.Id == clientLoginId && cl.IsActive && cl.Deleted == null);

            if (login == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound, "Login not found or inactive");
            }

            // Get all client IDs associated with this login
            var clientIds = await db.ClientLoginPivots
                .Where(clp => clp.ClientLoginId == clientLoginId && clp.Deleted == null)
                .Select(clp => clp.ClientId)
                .ToListAsync();

            if (!clientIds.Any())
            {
                // Return empty array if no clients are associated
                return JsonWithISODates(new List<object>());
            }

            // Query clients that are golf clients and match the authorized client IDs
            // Eagerly load ShippingAddresses and related USState for tax calculation
            var clients = await db.Clients
                .Where(c => clientIds.Contains(c.Id) && c.IsGolfClient == true && c.Deleted == null)
                .Include("ShippingAddresses.USState")
                .ToListAsync();

            var events = new List<object>();

            foreach (var client in clients)
            {
                // Get the tournament for this client (1:1 relationship)
                var tournament = await db.Tournaments
                    .FirstOrDefaultAsync(t => t.ClientId == client.Id && t.Deleted == null);

                // Filter by year if specified
                if (year.HasValue && tournament != null)
                {
                    if (tournament.StartDate.Year != year.Value)
                    {
                        continue;
                    }
                }
                else if (year.HasValue && tournament == null)
                {
                    continue;
                }

                // Fetch all budget items and calculate total budget
                // Store items so we can also collect taxable items for tax calculation
                var allBudgetItems = new List<BudgetItem>();

                var printApproved = await GetPrintApproved(client.Id);
                allBudgetItems.AddRange(printApproved);

                var isPastTeardown = tournament?.TeardownEndDate.HasValue == true &&
                                     DateTime.Now.Date > tournament.TeardownEndDate.Value.Date;

                if (!isPastTeardown)
                {
                    var printQuoted = await GetPrintQuoted(client.Id);
                    allBudgetItems.AddRange(printQuoted);
                }

                var onsiteLabor = await GetOnsiteLabor(client.Id);
                allBudgetItems.AddRange(onsiteLabor);

                var hardwareRental = await GetBudgetLineItems(client.Id, "Hardware Rental",         "HardwareRental");
                allBudgetItems.AddRange(hardwareRental);

                var offsiteLabor = await GetBudgetLineItems(client.Id, "Offsite Labor",            "OffsiteLabor");
                allBudgetItems.AddRange(offsiteLabor);

                var travelExpenses = await GetBudgetLineItems(client.Id, "Travel Expenses",          "TravelExpenses");
                allBudgetItems.AddRange(travelExpenses);

                var hotel = await GetBudgetLineItems(client.Id, "Hotel/Accommodation",      "Accommodation");
                allBudgetItems.AddRange(hotel);

                var vehicleRental = await GetBudgetLineItems(client.Id, "Vehicle Rental",           "VehicleRental");
                allBudgetItems.AddRange(vehicleRental);

                var freight = await GetBudgetLineItems(client.Id, "Freight",                  "Freight");
                allBudgetItems.AddRange(freight);

                var reimbursables = await GetBudgetLineItems(client.Id, "Reimbursables - Hardware", "Reimbursables");
                allBudgetItems.AddRange(reimbursables);

                var managementFee = await GetBudgetLineItems(client.Id, "Management Fee",           "ManagementFees");
                allBudgetItems.AddRange(managementFee);

                var storageFee = await GetBudgetLineItems(client.Id, "Storage Fee",              "StorageFees");
                allBudgetItems.AddRange(storageFee);

                var designFee = await GetBudgetLineItems(client.Id, "Design Fee",               "DesignFees");
                allBudgetItems.AddRange(designFee);

                var miscellaneous = await GetBudgetLineItems(client.Id, "Miscellaneous",            "Miscellaneous");
                allBudgetItems.AddRange(miscellaneous);

                // Get onsite printing items from Airtable for THIS client
                decimal onsitePrintingCost = 0m;
                try
                {
                    string clientBaseId = client.BaseId ?? "appr4CfzgemFQSNGR"; // Hardcoded for testing

                    if (!string.IsNullOrEmpty(clientBaseId))
                    {
                        var airtableService = new AirtableService(clientBaseId, client.SqftPrice); // Pass client's SqftPrice
                        var onsitePrintingItems = await GetOnsitePrintingFromAirtableAsync(clientBaseId, client.SqftPrice);
                        allBudgetItems.AddRange(onsitePrintingItems);
                        onsitePrintingCost = onsitePrintingItems.Sum(item => item.Price);
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue - onsite printing cost will be 0
                    Console.WriteLine($"Error fetching Airtable data for client {client.Id}: {ex.Message}");
                }

                // Calculate total budget from all items
                decimal totalBudget = allBudgetItems.Sum(item => item.Price);

                // Calculate tax using Avalara
                decimal totalTax = 0m;
                try
                {
                    // Resolve tax jurisdiction: taxable address → billing address fallback
                    string taxCity, taxZip, taxStateAbbr;
                    if (!string.IsNullOrWhiteSpace(client.TaxCity) && client.TaxUSStateId.HasValue)
                    {
                        var taxState = await db.USStates.FindAsync(client.TaxUSStateId.Value);
                        taxCity      = client.TaxCity;
                        taxZip       = client.TaxZip ?? "";
                        taxStateAbbr = taxState?.Abbreviation ?? "";
                    }
                    else
                    {
                        var billingState = client.BillingUSState.HasValue
                            ? await db.USStates.FindAsync(client.BillingUSState.Value)
                            : null;
                        taxCity      = client.BillingCity ?? "";
                        taxZip       = client.BillingZip ?? "";
                        taxStateAbbr = billingState?.Abbreviation ?? "";
                    }

                    if (!string.IsNullOrWhiteSpace(taxStateAbbr))
                    {
                        System.Diagnostics.Debug.WriteLine("[TaxCode] ----", allBudgetItems);
                        // Collect taxable items and convert to TaxableLineItem format
                        var taxableItems = allBudgetItems
                            .Where(item => item.Taxable && item.Price > 0)
                            .Select(item => new TaxableLineItem
                            {
                                Description = item.Name ?? item.Category ?? "Miscellaneous",
                                Amount = item.Price,
                                TaxCode = DetermineTaxCode(item.Category),
                                Quantity = 1
                            })
                            .ToList();

                        // Only call Avalara if there are taxable items
                        if (taxableItems.Any())
                        {
                            var avalaraService = new AvalaraService();
                            var avalaraResult = await avalaraService.CalculateTaxForEventAsync(
                                taxCity,
                                taxStateAbbr,
                                taxZip,
                                taxableItems,
                                client.Title
                            );
                            totalTax = avalaraResult.TotalTax;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue - tax will be 0
                }

                var eventSummary = new
                {
                    Id = client.Id,  // Client ID (Event ID)
                    BaseId = client.BaseId,  // Base ID from Clients entity
                    TournamentId = tournament?.Id,  // Tournament ID for reference
                    Name = tournament?.Name ?? client.Title,  // Tournament name or client name as fallback
                    Status = tournament != null ? DetermineTournamentStatus(tournament) : "pending",
                    SetupStartDate = tournament?.SetupStartDate,
                    SetupEndDate = tournament?.SetupEndDate,
                    AdvancedWeekStart = tournament?.AdvanceWeekStartDate,
                    AdvancedWeekEnd = tournament?.AdvanceWeekEndDate,
                    TournamentStart = tournament?.StartDate,
                    TournamentEnd = tournament?.EndDate,
                    TeardownDate = tournament?.TeardownEndDate ?? tournament?.TeardownStartDate,
                    Location = tournament?.Location,
                    Description = tournament?.Description,
                    TotalBudget = totalBudget,
                    TotalTax = totalTax,
                    GrandTotalWithTax = totalBudget + totalTax,
                    OnsitePrintingCost = onsitePrintingCost,
                    ClientLogo = "/assets/logos/default-logo.svg",
                    ClientName = client.Title
                };

                events.Add(eventSummary);
            }

            return JsonWithISODates(events);
        }

        // GET: /ClientPortal/GetEvent/{eventId}
        // eventId is the Client ID (because Client = Event in this context)
        [ClientCookieFilter]
        public async Task<ActionResult> GetEvent(int eventId)
        {
            var client = await db.Clients
                .FirstOrDefaultAsync(c => c.Id == eventId && c.IsGolfClient == true && c.Deleted == null);

            if (client == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound, "Event not found");
            }

            // Get the tournament for this client (1:1 relationship)
            var tournament = await db.Tournaments
                .FirstOrDefaultAsync(t => t.ClientId == client.Id && t.Deleted == null);

            if (tournament == null)
            {
                // No tournament exists for this client/event
                return JsonWithISODates(new
                {
                    Id = client.Id,
                    Name = client.Title,
                    Status = "pending",
                    TotalBudget = 0m,
                    ClientLogo = "/assets/logos/default-logo.svg",
                    ClientName = client.Title,
                    Staff = new List<object>()
                });
            }

            // Get tournament costs and staff breakdown
            var tournamentCosts = await CalculateTournamentCostsForClient(tournament.Id);

            var eventDetail = new
            {
                Id = client.Id,  // Client ID (Event ID)
                TournamentId = tournament.Id,
                Name = tournament.Name,
                Status = DetermineTournamentStatus(tournament),
                SetupStartDate = tournament.SetupStartDate,
                SetupEndDate = tournament.SetupEndDate,
                AdvancedWeekStart = tournament.AdvanceWeekStartDate,
                AdvancedWeekEnd = tournament.AdvanceWeekEndDate,
                TournamentStart = tournament.StartDate,
                TournamentEnd = tournament.EndDate,
                TeardownDate = tournament.TeardownEndDate ?? tournament.TeardownStartDate,
                Location = tournament.Location,
                Description = tournament.Description,
                TotalBudget = tournamentCosts.GrandTotal,
                AdvanceWeekLaborCost = tournamentCosts.AdvanceWeekLaborCost,
                TournamentWeekLaborCost = tournamentCosts.TournamentWeekLaborCost,
                TotalLaborCost = tournamentCosts.TotalLaborCost,
                TotalPerDiemCost = tournamentCosts.TotalPerDiemCost,
                TotalTravelExpenses = tournamentCosts.TotalTravelExpenses,
                ClientLogo = "/assets/logos/default-logo.svg",
                ClientName = client.Title,
                Staff = tournamentCosts.Staff
            };

            return JsonWithISODates(eventDetail);
        }

        // GET: /ClientPortal/GetEventBudget/{eventId}
        [ClientCookieFilter]
        public async Task<ActionResult> GetEventBudget(int eventId)
        {
            // Verify the event (client) exists and load shipping addresses for tax calculation
            var client = await db.Clients
                .Include("ShippingAddresses.USState")
                .FirstOrDefaultAsync(c => c.Id == eventId && c.IsGolfClient == true && c.Deleted == null);

            if (client == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound, "Event not found");
            }

            // Get the tournament for this client (1:1 relationship)
            var tournament = await db.Tournaments
                .FirstOrDefaultAsync(t => t.ClientId == client.Id && t.Deleted == null);

            // Calculate tournament costs for the event info
            decimal advanceWeekLaborCost = 0m;
            decimal tournamentWeekLaborCost = 0m;
            decimal totalLaborCost = 0m;
            decimal totalPerDiemCost = 0m;
            decimal totalTravelExpenses = 0m;

            if (tournament != null)
            {
                var tournamentCosts = await CalculateTournamentCostsForClient(tournament.Id);
                advanceWeekLaborCost = tournamentCosts.AdvanceWeekLaborCost;
                tournamentWeekLaborCost = tournamentCosts.TournamentWeekLaborCost;
                totalLaborCost = tournamentCosts.TotalLaborCost;
                totalPerDiemCost = tournamentCosts.TotalPerDiemCost;
                totalTravelExpenses = tournamentCosts.TotalTravelExpenses;
            }

            // Get onsite printing data from Airtable for this client
            string clientBaseId = client.BaseId ?? "appr4CfzgemFQSNGR"; // Hardcoded for testing
            var onsitePrintingItems = await GetOnsitePrintingFromAirtableAsync(clientBaseId, client.SqftPrice);

            // Check if we're past the teardown date
            var isPastTeardown = tournament?.TeardownEndDate.HasValue == true &&
                                 DateTime.Now.Date > tournament.TeardownEndDate.Value.Date;

            // Get all budget items
            var budget = new Budget
            {
                PrintApproved = await GetPrintApproved(eventId),
                PrintQuoted = isPastTeardown ? new List<BudgetItem>() : await GetPrintQuoted(eventId),
                OnsiteLabor = await GetOnsiteLabor(eventId),

                // Load data from new TournamentBudget tables
                HardwareRental = await GetBudgetLineItems(eventId, "Hardware Rental"),
                OnsitePrinting = onsitePrintingItems, // From Airtable
                OffsiteLabor = await GetBudgetLineItems(eventId, "Offsite Labor"),
                TravelExpenses = await GetBudgetLineItems(eventId, "Travel Expenses"),
                Accommodation = await GetBudgetLineItems(eventId, "Hotel/Accommodation"),
                VehicleRental = await GetBudgetLineItems(eventId, "Vehicle Rental"),
                Freight = await GetBudgetLineItems(eventId, "Freight"),
                Reimbursables = await GetBudgetLineItems(eventId, "Reimbursables - Hardware"),
                ManagementFees = await GetBudgetLineItems(eventId, "Management Fee"),
                StorageFees = await GetBudgetLineItems(eventId, "Storage Fee"),
                DesignFees = await GetBudgetLineItems(eventId, "Design Fee"),
                Miscellaneous = await GetBudgetLineItems(eventId, "Miscellaneous")
            };

            // Calculate total budget by summing all budget items
            decimal totalBudget = 0m;
            totalBudget += budget.PrintApproved.Sum(item => item.Price);
            totalBudget += budget.PrintQuoted.Sum(item => item.Price);
            totalBudget += budget.OnsiteLabor.Sum(item => item.Price);
            totalBudget += budget.HardwareRental.Sum(item => item.Price);
            totalBudget += budget.OnsitePrinting.Sum(item => item.Price);
            totalBudget += budget.OffsiteLabor.Sum(item => item.Price);
            totalBudget += budget.TravelExpenses.Sum(item => item.Price);
            totalBudget += budget.Accommodation.Sum(item => item.Price);
            totalBudget += budget.VehicleRental.Sum(item => item.Price);
            totalBudget += budget.Freight.Sum(item => item.Price);
            totalBudget += budget.Reimbursables.Sum(item => item.Price);
            totalBudget += budget.ManagementFees.Sum(item => item.Price);
            totalBudget += budget.StorageFees.Sum(item => item.Price);
            totalBudget += budget.DesignFees.Sum(item => item.Price);
            totalBudget += budget.Miscellaneous.Sum(item => item.Price);

            // Calculate per-category tax breakdown for ALL categories
            var taxBreakdown = new TaxBreakdown();

            try
            {
                // Resolve tax jurisdiction: taxable address → billing address fallback
                string taxCity, taxZip, taxStateAbbr;
                if (!string.IsNullOrWhiteSpace(client.TaxCity) && client.TaxUSStateId.HasValue)
                {
                    var taxState = await db.USStates.FindAsync(client.TaxUSStateId.Value);
                    taxCity      = client.TaxCity;
                    taxZip       = client.TaxZip ?? "";
                    taxStateAbbr = taxState?.Abbreviation ?? "";
                }
                else
                {
                    var billingState = client.BillingUSState.HasValue
                        ? await db.USStates.FindAsync(client.BillingUSState.Value)
                        : null;
                    taxCity      = client.BillingCity ?? "";
                    taxZip       = client.BillingZip ?? "";
                    taxStateAbbr = billingState?.Abbreviation ?? "";
                }

                if (!string.IsNullOrWhiteSpace(taxStateAbbr))
                {
                    var clientTitle = client.Title;

                    // Calculate tax for each category (only if category has items)
                    if (budget.PrintQuoted.Any())
                        taxBreakdown.PrintQuoted = await CalculateCategoryTaxAsync(
                            budget.PrintQuoted, "PrintQuoted", taxCity, taxStateAbbr, taxZip, clientTitle);

                    if (budget.PrintApproved.Any())
                        taxBreakdown.PrintApproved = await CalculateCategoryTaxAsync(
                            budget.PrintApproved, "PrintApproved", taxCity, taxStateAbbr, taxZip, clientTitle);

                    if (budget.HardwareRental.Any())
                        taxBreakdown.HardwareRental = await CalculateCategoryTaxAsync(
                            budget.HardwareRental, "HardwareRental", taxCity, taxStateAbbr, taxZip, clientTitle);

                    if (budget.OnsitePrinting.Any())
                        taxBreakdown.OnsitePrinting = await CalculateCategoryTaxAsync(
                            budget.OnsitePrinting, "OnsitePrinting", taxCity, taxStateAbbr, taxZip, clientTitle);

                    if (budget.OnsiteLabor.Any())
                        taxBreakdown.OnsiteLabor = await CalculateCategoryTaxAsync(
                            budget.OnsiteLabor, "OnsiteLabor", taxCity, taxStateAbbr, taxZip, clientTitle);

                    if (budget.OffsiteLabor.Any())
                        taxBreakdown.OffsiteLabor = await CalculateCategoryTaxAsync(
                            budget.OffsiteLabor, "OffsiteLabor", taxCity, taxStateAbbr, taxZip, clientTitle);

                    if (budget.TravelExpenses.Any())
                        taxBreakdown.TravelExpenses = await CalculateCategoryTaxAsync(
                            budget.TravelExpenses, "TravelExpenses", taxCity, taxStateAbbr, taxZip, clientTitle);

                    if (budget.Accommodation.Any())
                        taxBreakdown.Accommodation = await CalculateCategoryTaxAsync(
                            budget.Accommodation, "Accommodation", taxCity, taxStateAbbr, taxZip, clientTitle);

                    if (budget.VehicleRental.Any())
                        taxBreakdown.VehicleRental = await CalculateCategoryTaxAsync(
                            budget.VehicleRental, "VehicleRental", taxCity, taxStateAbbr, taxZip, clientTitle);

                    if (budget.Freight.Any())
                        taxBreakdown.Freight = await CalculateCategoryTaxAsync(
                            budget.Freight, "Freight", taxCity, taxStateAbbr, taxZip, clientTitle);

                    if (budget.Reimbursables.Any())
                        taxBreakdown.Reimbursables = await CalculateCategoryTaxAsync(
                            budget.Reimbursables, "Reimbursables", taxCity, taxStateAbbr, taxZip, clientTitle);

                    if (budget.ManagementFees.Any())
                        taxBreakdown.ManagementFees = await CalculateCategoryTaxAsync(
                            budget.ManagementFees, "ManagementFees", taxCity, taxStateAbbr, taxZip, clientTitle);

                    if (budget.StorageFees.Any())
                        taxBreakdown.StorageFees = await CalculateCategoryTaxAsync(
                            budget.StorageFees, "StorageFees", taxCity, taxStateAbbr, taxZip, clientTitle);

                    if (budget.DesignFees.Any())
                        taxBreakdown.DesignFees = await CalculateCategoryTaxAsync(
                            budget.DesignFees, "DesignFees", taxCity, taxStateAbbr, taxZip, clientTitle);

                    if (budget.Miscellaneous.Any())
                        taxBreakdown.Miscellaneous = await CalculateCategoryTaxAsync(
                            budget.Miscellaneous, "Miscellaneous", taxCity, taxStateAbbr, taxZip, clientTitle);

                    // Calculate totals across all categories
                    var allCategories = new[] {
                        taxBreakdown.PrintQuoted, taxBreakdown.PrintApproved, taxBreakdown.HardwareRental,
                        taxBreakdown.OnsitePrinting, taxBreakdown.OnsiteLabor, taxBreakdown.OffsiteLabor,
                        taxBreakdown.TravelExpenses, taxBreakdown.Accommodation, taxBreakdown.VehicleRental,
                        taxBreakdown.Freight, taxBreakdown.Reimbursables, taxBreakdown.ManagementFees,
                        taxBreakdown.StorageFees, taxBreakdown.DesignFees, taxBreakdown.Miscellaneous
                    }.Where(c => c != null);

                    taxBreakdown.TotalTaxableAmount = Math.Round(allCategories.Sum(c => c.TaxableAmount), 2);
                    taxBreakdown.TotalTax           = Math.Round(allCategories.Sum(c => c.Tax), 2);
                    taxBreakdown.GrandTotal         = Math.Round(totalBudget + taxBreakdown.TotalTax, 2);
                }
                else
                {
                    // No valid tax jurisdiction resolved - set defaults
                    taxBreakdown.TotalTaxableAmount = 0;
                    taxBreakdown.TotalTax = 0;
                    taxBreakdown.GrandTotal = totalBudget;
                }
            }
            catch (Exception ex)
            {
                // Return defaults on error
                taxBreakdown.TotalTaxableAmount = 0;
                taxBreakdown.TotalTax = 0;
                taxBreakdown.GrandTotal = totalBudget;
            }

            var response = new
            {
                // Top-level event/tournament information
                Event = new
                {
                    Id = client.Id,  // Client ID (Event ID)
                    BaseId = client.BaseId,  // Base ID from Clients entity
                    TournamentId = tournament?.Id,
                    Name = tournament?.Name ?? client.Title,
                    Status = tournament != null ? DetermineTournamentStatus(tournament) : "pending",
                    SetupStartDate = tournament?.SetupStartDate,
                    SetupEndDate = tournament?.SetupEndDate,
                    AdvancedWeekStart = tournament?.AdvanceWeekStartDate,
                    AdvancedWeekEnd = tournament?.AdvanceWeekEndDate,
                    TournamentStart = tournament?.StartDate,
                    TournamentEnd = tournament?.EndDate,
                    TeardownDate = tournament?.TeardownEndDate ?? tournament?.TeardownStartDate,
                    Location = tournament?.Location,
                    Description = tournament?.Description,
                    TotalBudget = totalBudget,
                    TotalTax = taxBreakdown.TotalTax,
                    GrandTotalWithTax = taxBreakdown.GrandTotal,
                    AdvanceWeekLaborCost = advanceWeekLaborCost,
                    TournamentWeekLaborCost = tournamentWeekLaborCost,
                    TotalLaborCost = totalLaborCost,
                    TotalPerDiemCost = totalPerDiemCost,
                    TotalTravelExpenses = totalTravelExpenses,
                    ClientLogo = "/assets/logos/default-logo.svg",
                    ClientName = client.Title
                },
                // Budget details
                Budget = budget,
                // Per-category tax breakdown
                TaxBreakdown = taxBreakdown
            };

            return JsonWithISODates(response);
        }

        #region Helper Methods

        // OLD (synchronous): private List<BudgetItem> GetOnsitePrintingFromAirtable()
        private async Task<List<BudgetItem>> GetOnsitePrintingFromAirtableAsync(string baseId, decimal? sqftPrice = null)
        {
            try
            {
                var airtableService = new AirtableService(baseId, sqftPrice); // Pass client's baseId and SqftPrice
                // OLD (causes deadlock): var printRequests = airtableService.GetOnsitePrintRequestsAsync().Result;
                var printRequests = await airtableService.GetOnsitePrintRequestsAsync();

                var budgetItems = new List<BudgetItem>();

                foreach (var request in printRequests)
                {
                    budgetItems.Add(new BudgetItem
                    {
                        Id = $"at-{request.RecordId}",
                        Name = request.Description ?? "Onsite Print Request",
                        Category = "OnsitePrinting",
                        Qty = (int)request.Quantity,
                        Width = request.Width,
                        Height = request.Length, // Note: Length maps to Height in BudgetItem
                        SqFt = request.TotalSquareFeet,
                        Price = request.CalculatedCost,
                        Actual = request.CalculatedCost, // For now, estimate = actual
                        Taxable = true, // Onsite printing is taxable
                        WorkflowState = DetermineOnsitePrintWorkflowState(request.Status),
                        StartDate = request.DateRequested,
                        EndDate = request.DateCompleted,
                        EmployeeName = request.Requestor,
                        Notes = $"FC Installed: {request.FcInstalled ?? "N/A"}"
                    });
                }

                return budgetItems;
            }
            catch (Exception ex)
            {
                // Log error but return empty list rather than failing
                Console.WriteLine($"Error fetching Airtable onsite printing: {ex.Message}");
                return new List<BudgetItem>();
            }
        }

        private string DetermineOnsitePrintWorkflowState(string status)
        {
            if (string.IsNullOrEmpty(status))
                return "quoted";

            var statusLower = status.ToLower();

            if (statusLower.Contains("done") || statusLower.Contains("complete"))
                return "completed";

            if (statusLower.Contains("approved"))
                return "approved";

            if (statusLower.Contains("discussion") || statusLower.Contains("pending"))
                return "quoted";

            // Default to quoted
            return "quoted";
        }
        private async Task<List<BudgetItem>> GetBudgetLineItems(int clientId, string categoryName, string categoryKey = null)
        {
            // Load line items from TournamentBudgetLineItems table
            var lineItems = await db.TournamentBudgetLineItems
                .Include(li => li.TournamentBudgetCategory)
                .Where(li => li.ClientId == clientId &&
                            li.TournamentBudgetCategory.CategoryName == categoryName &&
                            !li.IsDeleted)
                .ToListAsync();

            var budgetItems = new List<BudgetItem>();

            foreach (var lineItem in lineItems)
            {
                budgetItems.Add(new BudgetItem
                {
                    Id = $"tb-{lineItem.LineItemId}",
                    Name = lineItem.ItemDescription,
                    Category = categoryKey ?? categoryName,
                    Qty = lineItem.Quantity.HasValue ? (int?)Convert.ToInt32(lineItem.Quantity.Value) : null,
                    QtyDays = lineItem.QuantityDays,
                    UnitPrice = lineItem.UnitPrice,
                    Width = lineItem.Width,
                    Height = lineItem.Height,
                    SqFt = lineItem.SqFt,
                    StartDate = lineItem.StartDate,
                    EndDate = lineItem.EndDate,
                    Price = lineItem.EstimatedAmount,
                    Actual = lineItem.ActualAmount,
                    Taxable = lineItem.IsTaxable,
                    Notes = lineItem.Notes,
                    WorkflowState = "approved" // Budget items are pre-approved
                });
            }

            return budgetItems;
        }

        private async Task<Event> MapToEvent(Client client, Tournament tournament)
        {
            // Determine status based on tournament dates
            string status = "pending";
            if (tournament != null)
            {
                var now = DateTime.Now;
                if (tournament.EndDate < now)
                {
                    status = "completed";
                }
                else if (tournament.StartDate <= now && tournament.EndDate >= now)
                {
                    status = "active";
                }
            }

            // Calculate total budget
            var totalBudget = await CalculateTotalBudget(client.Id);

            return new Event
            {
                Id = client.Id,
                Name = tournament?.Name ?? client.Title,
                Status = status,
                SetupStartDate = tournament?.SetupStartDate,
                SetupEndDate = tournament?.SetupEndDate,
                AdvancedWeekStart = tournament?.AdvanceWeekStartDate,
                AdvancedWeekEnd = tournament?.AdvanceWeekEndDate,
                TournamentStart = tournament?.StartDate,
                TournamentEnd = tournament?.EndDate,
                TeardownDate = tournament?.TeardownEndDate ?? tournament?.TeardownStartDate,
                Location = tournament?.Location,
                Description = tournament?.Description,
                TotalBudget = totalBudget,
                ClientLogo = "/assets/logos/default-logo.svg", // Client doesn't have ClientLogo property
                ClientName = client.Title
            };
        }

        private async Task<decimal> CalculateTotalBudget(int clientId)
        {
            // Sum up print approved, quoted, and onsite labor
            var printApprovedTotal = (await GetPrintApproved(clientId)).Sum(i => i.Price);
            var printQuotedTotal = (await GetPrintQuoted(clientId)).Sum(i => i.Price);
            var onsiteLaborTotal = (await GetOnsiteLabor(clientId)).Sum(i => i.Price);

            return printApprovedTotal + printQuotedTotal + onsiteLaborTotal;
        }

        private async Task<List<Job>> FilterJobsByReleaseToClientView(List<Job> jobs)
        {
            if (!jobs.Any())
                return new List<Job>();

            // Get all unique quote numbers from these jobs
            var quoteNumbers = jobs
                .Where(j => !string.IsNullOrEmpty(j.QuoteNumber))
                .Select(j => j.QuoteNumber)
                .Distinct()
                .ToList();

            if (!quoteNumbers.Any())
                return new List<Job>(); // No quotes to check, hide all jobs per user requirement

            // Fetch all related quotes in one query (performance optimization)
            var relatedQuotes = await db.Quotes
                .Where(q => quoteNumbers.Contains(q.JobNumber) && q.Deleted == null)
                .Select(q => new { q.JobNumber, q.ReleaseToClientView })
                .ToListAsync();

            // Create lookup dictionary for fast access
            var quoteReleaseLookup = relatedQuotes.ToDictionary(q => q.JobNumber, q => q.ReleaseToClientView);

            // Filter jobs based on ReleaseToClientView
            return jobs.Where(job =>
            {
                // If job has no QuoteNumber, hide it
                if (string.IsNullOrEmpty(job.QuoteNumber))
                    return false;

                // If quote exists in lookup, check its ReleaseToClientView flag
                if (quoteReleaseLookup.ContainsKey(job.QuoteNumber))
                {
                    return quoteReleaseLookup[job.QuoteNumber] == true;
                }

                // If no matching quote found, hide the job
                return false;
            }).ToList();
        }

        private async Task<List<BudgetItem>> GetPrintApproved(int clientId)
        {
            // Get jobs where client is base client OR has a cost allocation split
            var jobIdsWithSplits = await db.Job_ClientCostSplits
                .Where(s => s.ClientId == clientId)
                .Select(s => s.JobId)
                .Distinct()
                .ToListAsync();

            // Jobs with DueDate but no Cancelled, or Deleted date
            // Include jobs where client is primary OR has a cost allocation split
            var jobs = await db.Jobs
                .Where(j => (j.Client == clientId || jobIdsWithSplits.Contains(j.Id)) &&
                           j.DueDate.HasValue &&
                           j.Cancelled == null &&
                           j.Deleted == null)
                .ToListAsync();

            // Filter jobs by related quote's ReleaseToClientView flag
            jobs = await FilterJobsByReleaseToClientView(jobs);

            var budgetItems = new List<BudgetItem>();

            foreach (var job in jobs)
            {
                // Get client's allocation percentage for this job
                var clientSplit = await db.Job_ClientCostSplits
                    .FirstOrDefaultAsync(s => s.JobId == job.Id && s.ClientId == clientId);
                decimal allocationPercent = clientSplit?.SplitPercentage ?? 100m;

                var jobItems = await db.JobItems
                    .Where(ji => ji.Job == job.Id && ji.Deleted == null)
                    .ToListAsync();

                if (!jobItems.Any())
                    continue;

                // Sum all item prices into a single job-level entry
                decimal totalPrice = 0m;
                foreach (var jobItem in jobItems)
                {
                    var itemPrice = await CalculateJobItemPrice(job, jobItem);
                    totalPrice += itemPrice;
                }

                // Apply client's allocation percentage to the total
                var allocatedTotal = totalPrice * (allocationPercent / 100m);

                budgetItems.Add(new BudgetItem
                {
                    Id = $"pa-{job.Id}",
                    Name = job.Description ?? job.JobNumber,
                    Item = job.JobNumber,
                    Category = DetermineJobCategory(jobItems.First()),
                    Qty = jobItems.Count,
                    Price = allocatedTotal,
                    Actual = allocatedTotal,
                    Taxable = job.TaxExempt.HasValue ? !job.TaxExempt.Value : true,
                    WorkflowState = DetermineWorkflowState(job)
                });
            }

            return budgetItems;
        }

        private async Task<List<BudgetItem>> GetPrintQuoted(int clientId)
        {
            // Check if event teardown has passed - if so, hide all quotes
            var tournament = await db.Tournaments
                .FirstOrDefaultAsync(t => t.ClientId == clientId && t.Deleted == null);

            if (tournament != null)
            {
                var teardownDate = tournament.TeardownEndDate ?? tournament.TeardownStartDate;
                if (teardownDate.HasValue && DateTime.Now > teardownDate.Value)
                {
                    // Event is over, don't show quotes (only show completed jobs)
                    return new List<BudgetItem>();
                }
            }

            // Get quote IDs where client has a cost allocation split
            var quoteIdsWithSplits = await db.Quote_ClientCostSplits
                .Where(s => s.ClientId == clientId)
                .Select(s => s.QuoteId)
                .Distinct()
                .ToListAsync();

            // Get all quotes that don't have a corresponding job
            // Include quotes where client is primary OR has a cost allocation split
            var quotesWithoutJobs = (await db.Quotes
                .Where(q => (q.Client == clientId || quoteIdsWithSplits.Contains(q.Id)) &&
                            q.Deleted == null &&
                            q.ReleaseToClientView == true)
                .ToListAsync())
                .Where(q => !db.Jobs.Any(j => j.QuoteNumber == q.JobNumber && j.Deleted == null))
                .ToList();

            var budgetItems = new List<BudgetItem>();

            foreach (var quote in quotesWithoutJobs)
            {
                // Get client's allocation percentage for this quote
                var clientSplit = await db.Quote_ClientCostSplits
                    .FirstOrDefaultAsync(s => s.QuoteId == quote.Id && s.ClientId == clientId);
                decimal allocationPercent = clientSplit?.SplitPercentage ?? 100m;

                var quoteItems = await db.QuoteItems
                    .Where(qi => qi.Quote == quote.Id && qi.Deleted == null)
                    .ToListAsync();

                if (!quoteItems.Any())
                    continue;

                // Sum all item prices into a single quote-level entry
                decimal totalPrice = 0m;
                foreach (var quoteItem in quoteItems)
                {
                    var itemPrice = await CalculateQuoteItemPrice(quote, quoteItem);
                    totalPrice += itemPrice;
                }

                // Apply client's allocation percentage to the total
                var allocatedTotal = totalPrice * (allocationPercent / 100m);

                budgetItems.Add(new BudgetItem
                {
                    Id = $"pq-{quote.Id}",
                    Name = quote.Description ?? quote.JobNumber,
                    Item = quote.JobNumber,
                    Category = DetermineQuoteCategory(quoteItems.First()),
                    Qty = quoteItems.Count,
                    Price = allocatedTotal,
                    Taxable = true,
                    WorkflowState = await DetermineQuoteWorkflowState(quote),
                    QuoteDate = quote.CreatedAt
                });
            }

            return budgetItems;
        }

        private string DetermineJobCategory(JobItem jobItem)
        {
            return "PrintApproved";
        }

        private string DetermineQuoteCategory(QuoteItem quoteItem)
        {
            return "PrintQuoted";
        }

        private async Task<decimal> CalculateJobItemPrice(Job job, JobItem jobItem)
        {
            // Use the same database function that calculates Job.Price
            // This ensures consistency with the rest of the system
            var itemTotals = await db.JobParentItemTotals(job.Id)
                .FirstOrDefaultAsync(x => x.JobItem == jobItem.Id);

            return itemTotals?.Total_Price ?? 0m;
        }

        private async Task<decimal> CalculateQuoteItemPrice(Quote quote, QuoteItem quoteItem)
        {
            // Use the same database function that calculates Quote.Price
            // This ensures consistency with the rest of the system
            var itemTotals = await db.QuoteParentItemTotals(quote.Id)
                .FirstOrDefaultAsync(x => x.QuoteItem == quoteItem.Id);

            return itemTotals?.Total_Price ?? 0m;
        }

        private string DetermineWorkflowState(Job job)
        {
            // Map job status to workflow state
            if (job.Complete.HasValue) return "completed";
            if (job.Cancelled.HasValue) return "cancelled";
            if (job.Shipped.HasValue) return "shipped";
            // Job doesn't have Started property, check if any work has been done
            if (job.JobItems.Any(ji => ji.Complete.HasValue)) return "in-progress";
            return "approved";
        }

        private async Task<string> DetermineQuoteWorkflowState(Quote quote)
        {
            // Check if quote has been converted to job
            var hasJob = await db.Jobs.AnyAsync(j => j.QuoteNumber == quote.JobNumber && j.Deleted == null);
            if (hasJob) return "approved";

            // Otherwise it's just quoted
            return "quoted";
        }

        private async Task<List<BudgetItem>> GetOnsiteLabor(int clientId)
        {
            // Get all tournaments for this client
            var tournaments = await db.Tournaments
                .Where(t => t.ClientId == clientId && t.Deleted == null)
                .ToListAsync();

            if (!tournaments.Any())
            {
                return new List<BudgetItem>();
            }

            // Get tournament IDs
            var tournamentIds = tournaments.Select(t => t.Id).ToList();

            // Create tournament lookup with bill rates
            var tournamentLookup = tournaments.ToDictionary(t => t.Id);

            // Get all staff schedule entries for all tournaments (include User for names)
            // Include entries where tournament is either TournamentId OR TravelTournamentId
            var scheduleEntries = await db.StaffScheduleEntries
                .Include(sse => sse.User)
                .Where(sse => (tournamentIds.Contains(sse.TournamentId.Value)
                            || (sse.TravelTournamentId.HasValue && tournamentIds.Contains(sse.TravelTournamentId.Value)))
                            && sse.Deleted == null)
                .ToListAsync();

            // Group by user to create labor budget items
            var laborByUser = scheduleEntries
                .GroupBy(sse => new { sse.UserId, sse.User.FirstName, sse.User.LastName })
                .Select(g => new
                {
                    UserId = g.Key.UserId,
                    EmployeeName = $"{g.Key.FirstName} {g.Key.LastName}".Trim(),
                    Entries = g.ToList()
                })
                .ToList();

            var budgetItems = new List<BudgetItem>();

            // Track total labor and travel days across all employees for per diem calculation
            int totalLaborDays = 0;
            int totalTravelDays = 0;

            // Track travel entries with their calculated rates for grouping
            var travelEntriesWithRates = new List<(decimal rate, StaffScheduleEntry entry)>();

            foreach (var userGroup in laborByUser)
            {
                var entries = userGroup.Entries;

                // Calculate date range
                var startDate = entries.Min(e => e.ScheduleDate);
                var endDate = entries.Max(e => e.ScheduleDate);

                // Count billable labor days (EntryType: 1=Tournament only, exclude 2=Travel, 3=Sick, 4=TimeOffOffsite, 5=TimeOffOnsite)
                var laborBillableDays = entries.Count(e => e.EntryType == 1);

                // Count per diem eligible days by type
                var laborPerDiemDays = entries.Count(e => e.EntryType == 1 || e.EntryType == 3); // Tournament + Sick days get regular per diem
                //var travelPerDiemDays = entries.Count(e => e.EntryType == 2); // Travel days get travel per diem

                //totalTravelDays += travelPerDiemDays;

                foreach (var travelEntry in entries.Where(e => e.EntryType == 2))
                {
                    // Determine which tournament from THIS client is involved in the travel
                    int currentTournamentId = 0;

                    if (travelEntry.TournamentId.HasValue && tournamentIds.Contains(travelEntry.TournamentId.Value))
                    {
                        currentTournamentId = travelEntry.TournamentId.Value;
                    }
                    else if (travelEntry.TravelTournamentId.HasValue && tournamentIds.Contains(travelEntry.TravelTournamentId.Value))
                    {
                        currentTournamentId = travelEntry.TravelTournamentId.Value;
                    }

                    // Calculate averaged travel per diem using helper function
                    if (currentTournamentId > 0)
                    {
                        decimal travelRate = await GetAveragedTravelPerDiem(currentTournamentId, travelEntry);
                        travelEntriesWithRates.Add((travelRate, travelEntry));
                    }
                }

                totalLaborDays += laborPerDiemDays;

                // Calculate total cost using tournament bill rates
                decimal totalCost = 0;
                foreach (var entry in entries)
                {
                    // Only bill for EntryType 1 (Tournament) - skip 2=Travel, 3=Sick, 4=TimeOffOffsite, 5=TimeOffOnsite
                    if (entry.EntryType != 1)
                        continue;

                    decimal rate = 0;

                    // Use tournament bill rate if available (based on role)
                    if (entry.TournamentId.HasValue && tournamentLookup.ContainsKey(entry.TournamentId.Value))
                    {
                        var entryTournament = tournamentLookup[entry.TournamentId.Value];
                        var fallbackRate = entry.DailyRate2 ?? entry.DailyRate ?? 0;
                        rate = GetBillRateForRole(entry.Role, entryTournament, fallbackRate);
                    }
                    else
                    {
                        // No tournament - use entry's own rates
                        rate = entry.DailyRate2 ?? entry.DailyRate ?? 0;
                    }

                    totalCost += rate;
                }

                // Determine category based on role or entry type
                var category = DetermineLaborCategory(entries.FirstOrDefault());

                // Use the first tournament for workflow state (or could aggregate status)
                var tournament = tournaments.First();

                budgetItems.Add(new BudgetItem
                {
                    Id = $"ol-{userGroup.UserId}",
                    Name = userGroup.EmployeeName,
                    EmployeeName = userGroup.EmployeeName,
                    Category = category,
                    SubCategory = "Labor",  // Subcategory for grouping
                    Qty = laborBillableDays,  // Only count EntryType 1 (Tournament) for labor billing
                    QtyDays = laborBillableDays,
                    StartDate = startDate,
                    EndDate = endDate,
                    Price = totalCost,
                    Actual = totalCost, // For now, estimate = actual
                    Taxable = false, // Labor typically not taxable
                    WorkflowState = DetermineScheduleWorkflowState(entries, tournament)
                });
            }

            // Add aggregate per diem items
            var firstTournament = tournaments.First();
            var perDiemRate = firstTournament.PerDiem ?? 0m;
            var travelPerDiemRate = firstTournament.TravelPerDiem ?? 0m;

            // Add Labor Per Diem item for Tournament and Sick days (if there are labor days)
            if (totalLaborDays > 0 && perDiemRate > 0)
            {
                budgetItems.Add(new BudgetItem
                {
                    Id = "pd-labor",
                    Name = $"Labor Per Diem ({totalLaborDays} days × {perDiemRate:C})",
                    Category = "Per Diem",
                    SubCategory = "Per Diem",
                    Qty = totalLaborDays,
                    UnitPrice = perDiemRate,
                    Price = totalLaborDays * perDiemRate,
                    Actual = totalLaborDays * perDiemRate,
                    Taxable = false,
                    WorkflowState = "approved"
                });
            }

            // Add Travel Per Diem items grouped by rate (EntryType 2)
            // Group travel entries by their calculated rate to show quantity × unit price
            var travelGroups = travelEntriesWithRates
                .GroupBy(t => t.rate)
                .Select(g => new
                {
                    Rate = g.Key,
                    Count = g.Count(),
                    TotalCost = g.Key * g.Count()
                })
                .ToList();

            int travelItemIndex = 0;
            foreach (var group in travelGroups)
            {
                budgetItems.Add(new BudgetItem
                {
                    Id = $"pd-travel-{travelItemIndex++}",
                    Name = $"Travel Per Diem ({group.Count} days × {group.Rate:C})",
                    Category = "Per Diem",
                    SubCategory = "Per Diem",
                    Qty = group.Count,
                    UnitPrice = group.Rate,
                    Price = group.TotalCost,
                    Actual = group.TotalCost,
                    Taxable = false,
                    WorkflowState = "approved"
                });
            }

            return budgetItems;
        }

        private string DetermineLaborCategory(StaffScheduleEntry entry)
        {
            if (entry == null)
                return "Staff";

            // Check if it's advance week
            if (entry.IsAdvanceWeek)
                return "Setup";

            // Check entry type: 1=Tournament, 2=Travel, 3=Sick, 4=TimeOffOffsite, 5=TimeOffOnsite
            if (entry.EntryType == 2)
                return "Travel";

            // Try to use role
            if (!string.IsNullOrWhiteSpace(entry.Role))
            {
                var role = entry.Role.ToLower();
                if (role.Contains("manager") || role.Contains("supervisor"))
                    return "Management";
                if (role.Contains("setup") || role.Contains("install"))
                    return "Setup";
                return "Support";
            }

            // Default
            return "Staff";
        }

        /// <summary>
        /// Determines which tournament bill rate to use based on staff role
        /// </summary>
        /// <param name="roleName">Role name from StaffScheduleEntry</param>
        /// <param name="tournament">Tournament containing bill rates</param>
        /// <param name="fallbackRate">Fallback rate if tournament bill rate not set</param>
        /// <returns>Appropriate bill rate based on role</returns>
        private decimal GetBillRateForRole(string roleName, Tournament tournament, decimal fallbackRate)
        {
            if (tournament == null)
                return fallbackRate;

            // Normalize role name for comparison
            var role = (roleName ?? "").ToLower().Trim();

            // Check for manager/supervisor roles
            if (role.Contains("manager"))
            {
                return tournament.ManagerBillRate ?? tournament.BillRate ?? fallbackRate;
            }

            // Check for print roles
            if (role.Contains("print"))
            {
                return tournament.PrintBillRate ?? tournament.BillRate ?? fallbackRate;
            }

            // Default to general bill rate
            return tournament.BillRate ?? fallbackRate;
        }

        /// <summary>
        /// Calculate averaged travel per diem rate for a travel entry
        /// Handles Home↔Tournament and Tournament↔Tournament scenarios
        /// </summary>
        private async Task<decimal> GetAveragedTravelPerDiem(int currentTournamentId, StaffScheduleEntry travelEntry)
        {
            var currentTournament = await db.Tournaments.FindAsync(currentTournamentId);
            var thisRate = currentTournament?.TravelPerDiem ?? 0m;

            // Case 1: Home ↔ Tournament (TravelTournamentId is NULL)
            if (travelEntry.TravelTournamentId == null)
            {
                return thisRate;  // Use this tournament's rate
            }

            // Case 2: Tournament ↔ Tournament (both fields have values)
            // Get the OTHER tournament's rate
            var otherTournamentId = (travelEntry.TournamentId == currentTournamentId)
                ? travelEntry.TravelTournamentId.Value
                : travelEntry.TournamentId.Value;

            var otherTournament = await db.Tournaments.FindAsync(otherTournamentId);
            var otherRate = otherTournament?.TravelPerDiem ?? 0m;

            // Return average of both rates
            return (thisRate + otherRate) / 2m;
        }

        /// <summary>
        /// Determine Avalara tax code based on budget category
        /// </summary>
        private string DetermineTaxCode(string category)
        {
            if (string.IsNullOrEmpty(category))
            {
                System.Diagnostics.Debug.WriteLine("[TaxCode] category is null/empty → P0000000");
                return "P0000000";
            }

            var taxCodeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "PrintQuoted",    "SP106914" },
                { "PrintApproved",  "SP106914" },
                { "HardwareRental", "PR091000" },
                { "OnsitePrinting", "SP106914" },
                { "OnsiteLabor",    "SI020100" },
                { "OffsiteLabor",   "SI020100" },
                { "TravelExpenses", "OT010300" },
                { "Accommodation",  "OT010300" },
                { "VehicleRental",  "OT010300" },
                { "ManagementFees", "SI020100" },
                { "StorageFees",    "SW117210" },
                { "DesignFees",     "SD086570" },
                { "Freight",        "FR023000" },
                { "Reimbursables",  "SI020100" },
                { "Miscellaneous",  "P0000000" },
            };

            var code = taxCodeMap.TryGetValue(category, out var mapped) ? mapped : "P0000000";
            var label = taxCodeMap.ContainsKey(category) ? "MATCHED" : "NO MATCH → default";
            System.Diagnostics.Debug.WriteLine($"[TaxCode] category='{category}' | {label} | code={code}");
            return code;
        }

        /// <summary>
        /// Calculate tax breakdown for a single category
        /// </summary>
        private async Task<CategoryTaxBreakdown> CalculateCategoryTaxAsync(
            List<BudgetItem> categoryItems,
            string categoryName,
            string city,
            string stateAbbreviation,
            string postalCode,
            string clientTitle)
        {
            var allItems = categoryItems.Where(i => i.Price > 0).ToList();
            var taxableItems = allItems.Where(i => i.Taxable).ToList();

            var subtotal = allItems.Sum(i => i.Price);
            var taxableAmount = taxableItems.Sum(i => i.Price);
            var nonTaxableAmount = subtotal - taxableAmount;

            decimal tax = 0m;
            decimal taxRate = 0m;

            // Only call Avalara if there are taxable items
            if (taxableItems.Any())
            {
                var taxableLineItems = taxableItems.Select(item => new TaxableLineItem
                {
                    Description = item.Name ?? item.Category ?? "Item",
                    Amount = item.Price,
                    TaxCode = DetermineTaxCode(categoryName),
                    Quantity = 1
                }).ToList();

                var avalaraService = new AvalaraService();
                var avalaraResult = await avalaraService.CalculateTaxForEventAsync(
                    city,
                    stateAbbreviation,
                    postalCode,
                    taxableLineItems,
                    clientTitle
                );

                tax = avalaraResult.TotalTax;
                taxRate = avalaraResult.EffectiveTaxRate;

                // Map per-line tax back to each BudgetItem (taxableItems order = Avalara line order)
                for (int i = 0; i < taxableItems.Count; i++)
                {
                    taxableItems[i].Tax = avalaraResult.TaxByLineIndex.ContainsKey(i)
                        ? Math.Round(avalaraResult.TaxByLineIndex[i], 2)
                        : 0m;
                }
            }

            return new CategoryTaxBreakdown
            {
                Subtotal         = Math.Round(subtotal, 2),
                TaxableAmount    = Math.Round(taxableAmount, 2),
                NonTaxableAmount = Math.Round(nonTaxableAmount, 2),
                Tax              = Math.Round(tax, 2),
                TaxRate          = Math.Round(taxRate, 4),
                Total            = Math.Round(subtotal + tax, 2),
                TotalItemCount   = allItems.Count,
                TaxableItemCount = taxableItems.Count
            };
        }

        private string DetermineScheduleWorkflowState(List<StaffScheduleEntry> entries, Tournament tournament)
        {
            var now = DateTime.Now;

            // If tournament is completed
            if (tournament.EndDate < now)
                return "completed";

            // If currently active
            if (tournament.StartDate <= now && tournament.EndDate >= now)
                return "active";

            // Check if any entries have clock times (means they've started working)
            if (entries.Any(e => e.ClockInUTC.HasValue))
                return "in-progress";

            // Otherwise scheduled/approved
            return "approved";
        }

        private string DetermineTournamentStatus(Tournament tournament)
        {
            if (tournament == null)
                return "pending";

            var now = DateTime.Now;
            if (tournament.EndDate < now)
            {
                return "completed";
            }
            else if (tournament.StartDate <= now && tournament.EndDate >= now)
            {
                return "active";
            }
            return "pending";
        }

        private class TournamentCostSummary
        {
            public decimal AdvanceWeekLaborCost { get; set; }
            public decimal TournamentWeekLaborCost { get; set; }
            public decimal TotalLaborCost { get; set; }
            public decimal TotalPerDiemCost { get; set; }
            public decimal TotalTravelExpenses { get; set; }
            public decimal GrandTotal { get; set; }
            public List<EventTournamentStaff> Staff { get; set; }
        }

        /// <summary>
        /// Tax breakdown for a single budget category
        /// Supports mixed taxable/non-taxable items
        /// </summary>
        private class CategoryTaxBreakdown
        {
            public decimal Subtotal { get; set; }           // Total of all items
            public decimal TaxableAmount { get; set; }      // Sum of taxable items only
            public decimal NonTaxableAmount { get; set; }   // Sum of non-taxable items only
            public decimal Tax { get; set; }                // Tax from Avalara
            public decimal TaxRate { get; set; }            // Effective tax rate (percentage)
            public decimal Total { get; set; }              // Subtotal + Tax
            public int TotalItemCount { get; set; }         // All items in category
            public int TaxableItemCount { get; set; }       // Count of taxable items
        }

        /// <summary>
        /// Complete tax breakdown for all 15 budget categories
        /// </summary>
        private class TaxBreakdown
        {
            public CategoryTaxBreakdown PrintQuoted { get; set; }
            public CategoryTaxBreakdown PrintApproved { get; set; }
            public CategoryTaxBreakdown HardwareRental { get; set; }
            public CategoryTaxBreakdown OnsitePrinting { get; set; }
            public CategoryTaxBreakdown OnsiteLabor { get; set; }
            public CategoryTaxBreakdown OffsiteLabor { get; set; }
            public CategoryTaxBreakdown TravelExpenses { get; set; }
            public CategoryTaxBreakdown Accommodation { get; set; }
            public CategoryTaxBreakdown VehicleRental { get; set; }
            public CategoryTaxBreakdown Freight { get; set; }
            public CategoryTaxBreakdown Reimbursables { get; set; }
            public CategoryTaxBreakdown ManagementFees { get; set; }
            public CategoryTaxBreakdown StorageFees { get; set; }
            public CategoryTaxBreakdown DesignFees { get; set; }
            public CategoryTaxBreakdown Miscellaneous { get; set; }

            public decimal TotalTaxableAmount { get; set; }
            public decimal TotalTax { get; set; }
            public decimal GrandTotal { get; set; }
        }

        private async Task<TournamentCostSummary> CalculateTournamentCostsForClient(int tournamentId)
        {
            // Get tournament to access per diem rates
            var tournament = await db.Tournaments.FindAsync(tournamentId);
            var perDiemRate = tournament?.PerDiem ?? 0m;
            var travelPerDiemRate = tournament?.TravelPerDiem ?? 0m;

            // Based on TournamentController.GetTournamentCosts logic
            var scheduleEntries = await db.StaffScheduleEntries
                .Where(a => a.TournamentId == tournamentId && a.Deleted == null)
                .Include(a => a.User)
                .ToListAsync();

            // Get travel entries where this tournament is involved (either TournamentId or TravelTournamentId)
            var travelEntries = await db.StaffScheduleEntries
                .Where(a => (a.TournamentId == tournamentId || a.TravelTournamentId == tournamentId)
                            && a.EntryType == 2
                            && a.Deleted == null)
                .Include(a => a.User)
                .ToListAsync();

            var assignments = scheduleEntries
                .GroupBy(e => new { e.UserId, e.TournamentId })
                .Select(g => new
                {
                    UserId = g.Key.UserId,
                    User = g.First().User,
                    TournamentRole = new { Title = g.First().Role, DailyRate = g.First().DailyRate },
                    ScheduleEntries = g.Where(x => x.EntryType == 1).ToList(), // EntryType 1 = Tournament (labor billing + per diem)
                    SickDayEntries = g.Where(x => x.EntryType == 3).ToList(),  // EntryType 3 = Sick (per diem only, no labor billing)
                    TravelEntries = travelEntries.Where(t => t.UserId == g.Key.UserId).ToList()  // Travel entries for this user
                }).ToList();

            // Add users who ONLY have travel entries for this tournament (no regular schedule entries)
            // This ensures Tournament B gets charged when someone travels from Tournament A to Tournament B
            var usersWithScheduleEntries = assignments.Select(a => a.UserId).ToHashSet();
            var travelOnlyUsers = travelEntries
                .Where(t => !usersWithScheduleEntries.Contains(t.UserId))
                .GroupBy(t => t.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    User = g.First().User,
                    TournamentRole = new { Title = g.First().Role ?? "Travel", DailyRate = g.First().DailyRate },
                    ScheduleEntries = new List<StaffScheduleEntry>(),
                    SickDayEntries = new List<StaffScheduleEntry>(),
                    TravelEntries = g.ToList()
                });

            assignments = assignments.Concat(travelOnlyUsers).ToList();

            var staffList = new List<EventTournamentStaff>();
            decimal totalAdvanceWeekLaborCost = 0m;
            decimal totalTournamentWeekLaborCost = 0m;
            decimal totalPerDiemCost = 0m;
            decimal totalTravelExpenses = 0m;

            foreach (var a in assignments)
            {
                var scheduledDays = a.ScheduleEntries.ToList();
                var sickDays = a.SickDayEntries.ToList();
                var advanceWeekDays = scheduledDays.Count(s => s.IsAdvanceWeek);
                var tournamentWeekDays = scheduledDays.Count(s => !s.IsAdvanceWeek);
                var totalDays = scheduledDays.Count;
                var sickDaysCount = sickDays.Count;

                // Determine bill rate based on role
                var fallbackRate = a.TournamentRole.DailyRate ?? 0;
                var rateToUse = GetBillRateForRole(a.TournamentRole.Title, tournament, fallbackRate);

                // Calculate costs separately for advance week and tournament week using role-based bill rate
                // Only bill for EntryType 1 (Tournament) - sick days (EntryType 3) are NOT billed for labor
                var advanceWeekLaborCost = advanceWeekDays * rateToUse;
                var tournamentWeekLaborCost = tournamentWeekDays * rateToUse;
                var totalLaborCost = advanceWeekLaborCost + tournamentWeekLaborCost;

                // Calculate per diem costs based on actual entry types
                // Tournament days (EntryType 1) and Sick days (EntryType 3) get regular PerDiem
                var laborPerDiemDays = totalDays + sickDaysCount; // Tournament days + Sick days
                var laborDayPerDiemCost = laborPerDiemDays * perDiemRate;

                // Travel days (EntryType 2) get TravelPerDiem with tournament-to-tournament logic
                decimal travelDayPerDiemCost = 0m;

                foreach (var travelEntry in a.TravelEntries)
                {

                    // Use helper function to calculate averaged travel per diem
                    decimal travelRate = await GetAveragedTravelPerDiem(tournamentId, travelEntry);

                    travelDayPerDiemCost += travelRate;
                }

                var perDiemCost = laborDayPerDiemCost + travelDayPerDiemCost;
                var travelExpenses = 0m; // Travel expenses not implemented in new schema

                var totalCost = totalLaborCost + perDiemCost + travelExpenses;

                staffList.Add(new EventTournamentStaff
                {
                    UserId = a.User.Id,
                    UserName = a.User.FirstName + " " + a.User.LastName,
                    RoleName = a.TournamentRole.Title,
                    DailyRate = rateToUse,
                    TotalDays = totalDays,
                    AdvanceWeekDays = advanceWeekDays,
                    TournamentWeekDays = tournamentWeekDays,
                    AdvanceWeekLaborCost = advanceWeekLaborCost,
                    TournamentWeekLaborCost = tournamentWeekLaborCost,
                    TotalLaborCost = totalLaborCost,
                    PerDiemCost = perDiemCost,
                    TravelExpenses = travelExpenses,
                    TotalCost = totalCost
                });

                totalAdvanceWeekLaborCost += advanceWeekLaborCost;
                totalTournamentWeekLaborCost += tournamentWeekLaborCost;
                totalPerDiemCost += perDiemCost;
                totalTravelExpenses += travelExpenses;
            }

            return new TournamentCostSummary
            {
                AdvanceWeekLaborCost = totalAdvanceWeekLaborCost,
                TournamentWeekLaborCost = totalTournamentWeekLaborCost,
                TotalLaborCost = totalAdvanceWeekLaborCost + totalTournamentWeekLaborCost,
                TotalPerDiemCost = totalPerDiemCost,
                TotalTravelExpenses = totalTravelExpenses,
                GrandTotal = totalAdvanceWeekLaborCost + totalTournamentWeekLaborCost + totalPerDiemCost + totalTravelExpenses,
                Staff = staffList
            };
        }

        #endregion Helper Methods

        #region Proofing Endpoints

        /// <summary>
        /// Get all jobs for a client with proof status counts
        /// GET: /ClientPortal/GetProofingJobs?clientId={clientId}
        /// </summary>
        [ClientCookieFilter]
        public async Task<ActionResult> GetProofingJobs(int clientId)
        {
            try
            {
                // Verify client is linked to logged-in user
                var clientLoginId = int.Parse(Request.Cookies["ClientActiveUser"].Value);
                var hasAccess = await db.ClientLoginPivots
                    .AnyAsync(clp => clp.ClientLoginId == clientLoginId
                                  && clp.ClientId == clientId
                                  && clp.Deleted == null);

                if (!hasAccess)
                {
                    return SendJsonResponse(new { success = false, message = "Access denied" }, 403);
                }

                // Get all jobs for this client
                var jobs = await db.Jobs
                    .Include(j => j.JobItems)
                    .Where(j => j.Client == clientId
                             && j.Deleted == null
                             && j.Cancelled == null)
                    .OrderByDescending(j => j.CreatedAt)
                    .ToListAsync();

                var jobList = new List<object>();

                foreach (var job in jobs)
                {
                    // Get all job item IDs for this job
                    var jobItemIds = job.JobItems
                        .Where(ji => ji.Deleted == null)
                        .Select(ji => ji.Id)
                        .ToList();

                    // Count proofs by status for these items
                    var proofCounts = await db.JobProofs
                        .Where(p => jobItemIds.Contains(p.JobItemId.Value) && p.Deleted == null)
                        .GroupBy(p => p.ApprovalStatus)
                        .Select(g => new { Status = g.Key, Count = g.Count() })
                        .ToListAsync();

                    var pendingProofs = proofCounts.FirstOrDefault(p => p.Status == 0)?.Count ?? 0;
                    var approvedProofs = proofCounts.FirstOrDefault(p => p.Status == 1)?.Count ?? 0;
                    var rejectedProofs = proofCounts.FirstOrDefault(p => p.Status == 2)?.Count ?? 0;

                    jobList.Add(new
                    {
                        jobId = job.Id,
                        jobNumber = job.JobNumber,
                        title = job.Title,
                        description = job.Description,
                        createdAt = job.CreatedAt,
                        dueDate = job.DueDate,
                        status = DetermineWorkflowState(job),
                        totalItems = jobItemIds.Count,
                        pendingProofs = pendingProofs,
                        approvedProofs = approvedProofs,
                        rejectedProofs = rejectedProofs
                    });
                }

                return SendJsonResponse(new { success = true, jobs = jobList });
            }
            catch (Exception ex)
            {
                return SendJsonResponse(new { success = false, message = $"An error occurred: {ex.Message}" }, 500);
            }
        }

        /// <summary>
        /// Get job details with all items and their proof status
        /// GET: /ClientPortal/GetProofingJobDetails?jobId={jobId}
        /// </summary>
        [ClientCookieFilter]
        public async Task<ActionResult> GetProofingJobDetails(int jobId)
        {
            try
            {
                // Get the job with items
                var job = await db.Jobs
                    .Include(j => j.JobItems.Select(ji => ji.Material1))
                    .Include(j => j.ShippingAddress1)
                    .Include(j => j.ShippingAddress1.USState)
                    .Include(j => j.ShippingOption)
                    .FirstOrDefaultAsync(j => j.Id == jobId && j.Deleted == null);

                if (job == null)
                {
                    return SendJsonResponse(new { success = false, message = "Job not found" }, 404);
                }

                // Verify client is linked to logged-in user
                var clientLoginId = int.Parse(Request.Cookies["ClientActiveUser"].Value);
                var hasAccess = await db.ClientLoginPivots
                    .AnyAsync(clp => clp.ClientLoginId == clientLoginId
                                  && clp.ClientId == job.Client
                                  && clp.Deleted == null);

                if (!hasAccess)
                {
                    return SendJsonResponse(new { success = false, message = "Access denied" }, 403);
                }

                // Build shipping info
                object shippingInfo = null;
                if (job.ShippingAddress1 != null)
                {
                    shippingInfo = new
                    {
                        method = job.ShippingOption?.Title,
                        address = job.ShippingAddress1.Address,
                        address2 = job.ShippingAddress1.Address2,
                        city = job.ShippingAddress1.City,
                        state = job.ShippingAddress1.USState?.Abbreviation,
                        zip = job.ShippingAddress1.Zip,
                        attention = job.ShippingAddress1.Attention
                    };
                }

                // Build items with proofs
                var items = new List<object>();
                foreach (var jobItem in job.JobItems.Where(ji => ji.Deleted == null))
                {
                    // Parse dimensions
                    decimal? width = null;
                    decimal? height = null;
                    if (!string.IsNullOrEmpty(jobItem.Width) && decimal.TryParse(jobItem.Width, out decimal w))
                        width = w;
                    if (!string.IsNullOrEmpty(jobItem.Length) && decimal.TryParse(jobItem.Length, out decimal h))
                        height = h;

                    // Get proofs for this item
                    var proofs = await db.JobProofs
                        .Include(p => p.JobDocument)
                        .Where(p => p.JobItemId == jobItem.Id && p.Deleted == null)
                        .OrderByDescending(p => p.Version)
                        .ToListAsync();

                    var proofList = proofs.Select(p => new
                    {
                        proofId = p.Id,
                        version = p.Version,
                        status = p.ApprovalStatus,
                        statusText = p.ApprovalStatus == 0 ? "Pending" : (p.ApprovalStatus == 1 ? "Approved" : "Rejected"),
                        approvalDate = p.ApprovalDate,
                        approvalNotes = p.ApprovalNotes,
                        downloadUrl = $"/ClientPortal/DownloadProof/{p.Id}",
                        thumbnailBase64 = GetProofThumbnailBase64(p.ThumbnailPath),
                        createdAt = p.CreatedAt
                    }).ToList();

                    items.Add(new
                    {
                        itemId = jobItem.Id,
                        title = jobItem.Title,
                        description = jobItem.Description,
                        quantity = jobItem.Quantity,
                        specs = new
                        {
                            width = width,
                            height = height,
                            material = jobItem.Material1?.FriendlyName ?? jobItem.Material1?.Title,
                            colorsFront = jobItem.PressColorProfileFront,
                            colorsBack = jobItem.PressColorProfileBack
                        },
                        notes = jobItem.Notes,
                        proofs = proofList
                    });
                }

                var response = new
                {
                    success = true,
                    job = new
                    {
                        jobId = job.Id,
                        jobNumber = job.JobNumber,
                        title = job.Title,
                        description = job.Description,
                        createdAt = job.CreatedAt,
                        dueDate = job.DueDate,
                        status = DetermineWorkflowState(job),
                        shipping = shippingInfo
                    },
                    items = items
                };

                return JsonWithISODates(response);
            }
            catch (Exception ex)
            {
                return SendJsonResponse(new { success = false, message = $"An error occurred: {ex.Message}" }, 500);
            }
        }

        /// <summary>
        /// Convert proof thumbnail to base64 data URI
        /// </summary>
        private string GetProofThumbnailBase64(string thumbnailPath)
        {
            if (string.IsNullOrEmpty(thumbnailPath) || !System.IO.File.Exists(thumbnailPath))
                return null;

            try
            {
                byte[] bytes = System.IO.File.ReadAllBytes(thumbnailPath);
                return "data:image/png;base64," + Convert.ToBase64String(bytes);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Update proof approval status (approve or reject)
        /// POST: /ClientPortal/UpdateProofStatus
        /// </summary>
        [ClientCookieFilter]
        [HttpPost]
        public async Task<ActionResult> UpdateProofStatus(int proofId, int status, string notes = null)
        {
            try
            {
                // Validate status (1 = approved, 2 = rejected)
                if (status != 1 && status != 2)
                {
                    return SendJsonResponse(new { success = false, message = "Invalid status. Use 1 for approved, 2 for rejected." }, 400);
                }

                // Get the proof
                var proof = await db.JobProofs
                    .Include(p => p.JobItem)
                    .FirstOrDefaultAsync(p => p.Id == proofId && p.Deleted == null);

                if (proof == null)
                {
                    return SendJsonResponse(new { success = false, message = "Proof not found" }, 404);
                }

                // Get the job to verify client access
                var job = await db.Jobs.FirstOrDefaultAsync(j => j.Id == proof.JobItem.Job);
                if (job == null)
                {
                    return SendJsonResponse(new { success = false, message = "Job not found" }, 404);
                }

                // Verify client is linked to logged-in user
                var clientLoginId = int.Parse(Request.Cookies["ClientActiveUser"].Value);
                var hasAccess = await db.ClientLoginPivots
                    .AnyAsync(clp => clp.ClientLoginId == clientLoginId
                                  && clp.ClientId == job.Client
                                  && clp.Deleted == null);

                if (!hasAccess)
                {
                    return SendJsonResponse(new { success = false, message = "Access denied" }, 403);
                }

                // Update the proof status
                proof.ApprovalStatus = status;
                proof.ApprovalDate = DateTime.UtcNow;
                proof.ApprovalNotes = notes;

                await db.SaveChangesAsync();

                var statusText = status == 1 ? "approved" : "rejected";
                return SendJsonResponse(new { success = true, message = $"Proof {statusText} successfully" });
            }
            catch (Exception ex)
            {
                return SendJsonResponse(new { success = false, message = $"An error occurred: {ex.Message}" }, 500);
            }
        }

        /// <summary>
        /// Download proof PDF file
        /// GET: /ClientPortal/DownloadProof/{proofId}
        /// </summary>
        [ClientCookieFilter]
        [HttpGet]
        public async Task<ActionResult> DownloadProof(int proofId)
        {
            try
            {
                var clientLoginId = int.Parse(Request.Cookies["ClientActiveUser"].Value);

                var proof = await db.JobProofs.FindAsync(proofId);
                if (proof == null || proof.Deleted != null)
                    return SendJsonResponse(new { success = false, message = "Proof not found" }, 404);

                var jobItem = await db.JobItems.Include(ji => ji.Job1)
                    .FirstOrDefaultAsync(ji => ji.Id == proof.JobItemId);
                if (jobItem == null || jobItem.Job1 == null)
                    return SendJsonResponse(new { success = false, message = "Job not found" }, 404);

                var hasAccess = await db.ClientLoginPivots
                    .AnyAsync(clp => clp.ClientLoginId == clientLoginId
                                  && clp.ClientId == jobItem.Job1.Client
                                  && clp.Deleted == null);
                if (!hasAccess)
                    return SendJsonResponse(new { success = false, message = "Access denied" }, 403);

                if (!proof.JobDocumentId.HasValue)
                    return SendJsonResponse(new { success = false, message = "No document attached" }, 404);

                var jobDoc = await db.JobDocuments.FindAsync(proof.JobDocumentId.Value);
                if (jobDoc == null || string.IsNullOrEmpty(jobDoc.DocumentPath))
                    return SendJsonResponse(new { success = false, message = "Document not found" }, 404);

                if (!System.IO.File.Exists(jobDoc.DocumentPath))
                    return SendJsonResponse(new { success = false, message = "File not found" }, 404);

                string fileName = System.IO.Path.GetFileName(jobDoc.DocumentPath);
                return File(jobDoc.DocumentPath, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                return SendJsonResponse(new { success = false, message = ex.Message }, 500);
            }
        }

        /// <summary>
        /// Verify a signed client access link (magic link authentication)
        /// GET: /ClientPortal/VerifyClientAccess?clientId={clientId}&sig={signature}
        /// No auth required - the valid signature IS the authentication
        /// </summary>
        public ActionResult VerifyClientAccess(int clientId, string sig)
        {
            try
            {
                if (string.IsNullOrEmpty(sig))
                {
                    return SendJsonResponse(new { success = false, message = "Missing signature" }, 400);
                }

                // Get the signing key
                string signingKey = System.Configuration.ConfigurationManager.AppSettings["ProofAccessSigningKey"];
                if (string.IsNullOrEmpty(signingKey))
                {
                    return SendJsonResponse(new { success = false, message = "Server configuration error" }, 500);
                }

                // Compute expected signature
                string expectedSignature = GenerateHmacSignature(clientId.ToString(), signingKey);

                // Constant-time comparison to prevent timing attacks
                if (!ConstantTimeEquals(sig, expectedSignature))
                {
                    return SendJsonResponse(new { success = false, message = "Invalid access link" }, 403);
                }

                // Verify the client exists
                var client = db.Clients.FirstOrDefault(c => c.Id == clientId && c.Deleted == null);
                if (client == null)
                {
                    return SendJsonResponse(new { success = false, message = "Client not found" }, 404);
                }

                return SendJsonResponse(new
                {
                    success = true,
                    clientId = clientId,
                    clientName = client.Title,
                    message = "Access verified"
                });
            }
            catch (Exception ex)
            {
                return SendJsonResponse(new { success = false, message = $"An error occurred: {ex.Message}" }, 500);
            }
        }

        /// <summary>
        /// Get proofing jobs for a client using signed access (no cookie auth required)
        /// GET: /ClientPortal/GetProofingJobsSigned?clientId={clientId}&sig={signature}
        /// </summary>
        public async Task<ActionResult> GetProofingJobsSigned(int clientId, string sig)
        {
            try
            {
                // Verify signature first
                string signingKey = System.Configuration.ConfigurationManager.AppSettings["ProofAccessSigningKey"];
                string expectedSignature = GenerateHmacSignature(clientId.ToString(), signingKey);

                if (!ConstantTimeEquals(sig ?? "", expectedSignature))
                {
                    return SendJsonResponse(new { success = false, message = "Invalid access link" }, 403);
                }

                // Fetch client to get BaseId for Airtable integration
                var client = await db.Clients.FindAsync(clientId);

                // Get all jobs for this client (same logic as GetProofingJobs but without cookie auth)
                var jobs = await db.Jobs
                    .Include(j => j.JobItems)
                    .Where(j => j.Client == clientId
                             && j.Deleted == null
                             && j.Cancelled == null)
                    .OrderByDescending(j => j.CreatedAt)
                    .ToListAsync();

                var jobList = new List<object>();

                foreach (var job in jobs)
                {
                    var jobItemIds = job.JobItems
                        .Where(ji => ji.Deleted == null)
                        .Select(ji => ji.Id)
                        .ToList();

                    // Get only the latest proof (highest Version) per item, then count by status
                    var latestProofsByItem = await db.JobProofs
                        .Where(p => jobItemIds.Contains(p.JobItemId.Value) && p.Deleted == null)
                        .GroupBy(p => p.JobItemId)
                        .Select(g => g.OrderByDescending(p => p.Version).FirstOrDefault())
                        .ToListAsync();

                    var proofCounts = latestProofsByItem
                        .Where(p => p != null)
                        .GroupBy(p => p.ApprovalStatus)
                        .Select(g => new { Status = g.Key, Count = g.Count() })
                        .ToList();

                    var pendingProofs = proofCounts.FirstOrDefault(p => p.Status == 0)?.Count ?? 0;
                    var approvedProofs = proofCounts.FirstOrDefault(p => p.Status == 1)?.Count ?? 0;
                    var rejectedProofs = proofCounts.FirstOrDefault(p => p.Status == 2)?.Count ?? 0;

                    // Skip jobs with no proofs
                    var totalProofs = pendingProofs + approvedProofs + rejectedProofs;
                    if (totalProofs == 0)
                        continue;

                    jobList.Add(new
                    {
                        jobId = job.Id,
                        jobNumber = job.JobNumber,
                        title = job.Title,
                        description = job.Description,
                        createdAt = job.CreatedAt,
                        dueDate = job.DueDate,
                        status = DetermineWorkflowState(job),
                        totalItems = jobItemIds.Count,
                        pendingProofs = pendingProofs,
                        approvedProofs = approvedProofs,
                        rejectedProofs = rejectedProofs
                    });
                }

                return SendJsonResponse(new { success = true, jobs = jobList, baseId = client?.BaseId });
            }
            catch (Exception ex)
            {
                return SendJsonResponse(new { success = false, message = $"An error occurred: {ex.Message}" }, 500);
            }
        }

        /// <summary>
        /// Get Airtable data for a client using signed access (no cookie auth required)
        /// GET: /ClientPortal/GetAirtableDataSigned?clientId={clientId}&sig={signature}
        /// </summary>
        public async Task<ActionResult> GetAirtableDataSigned(int clientId, string sig)
        {
            try
            {
                // Verify signature first
                string signingKey = System.Configuration.ConfigurationManager.AppSettings["ProofAccessSigningKey"];
                string expectedSignature = GenerateHmacSignature(clientId.ToString(), signingKey);

                if (!ConstantTimeEquals(sig ?? "", expectedSignature))
                {
                    return SendJsonResponse(new { success = false, message = "Invalid access link" }, 403);
                }

                // Get client to check for BaseId
                var client = await db.Clients.FindAsync(clientId);
                if (client == null)
                {
                    return SendJsonResponse(new { success = false, message = "Client not found" }, 404);
                }

                if (string.IsNullOrEmpty(client.BaseId))
                {
                    return SendJsonResponse(new { success = false, message = "Client does not have Airtable integration configured" }, 400);
                }

                // Initialize Airtable service with client's BaseId
                var airtableService = new Services.AirtableService(client.BaseId, client.SqftPrice);

                // Fetch all 4 tables in parallel
                var mainViewTask = airtableService.GetTableRecordsByNameAsync("2025-MainView");
                var onsitePrintTask = airtableService.GetTableRecordsByNameAsync("Onsite Print Requests");
                var laborScheduleTask = airtableService.GetTableRecordsByNameAsync("Labor Schedule");
                var eventDatesTask = airtableService.GetTableRecordsByNameAsync("EventDates");

                await Task.WhenAll(mainViewTask, onsitePrintTask, laborScheduleTask, eventDatesTask);

                return SendJsonResponse(new
                {
                    success = true,
                    tables = new
                    {
                        mainView = mainViewTask.Result,
                        onsitePrintRequests = onsitePrintTask.Result,
                        laborSchedule = laborScheduleTask.Result,
                        eventDates = eventDatesTask.Result
                    }
                });
            }
            catch (Exception ex)
            {
                return SendJsonResponse(new { success = false, message = $"An error occurred: {ex.Message}" }, 500);
            }
        }

        /// <summary>
        /// Get job details with proofs using signed access (no cookie auth required)
        /// GET: /ClientPortal/GetProofingJobDetailsSigned?jobId={jobId}&clientId={clientId}&sig={signature}
        /// </summary>
        public async Task<ActionResult> GetProofingJobDetailsSigned(int jobId, int clientId, string sig)
        {
            try
            {
                // Verify signature first
                string signingKey = System.Configuration.ConfigurationManager.AppSettings["ProofAccessSigningKey"];
                string expectedSignature = GenerateHmacSignature(clientId.ToString(), signingKey);

                if (!ConstantTimeEquals(sig ?? "", expectedSignature))
                {
                    return SendJsonResponse(new { success = false, message = "Invalid access link" }, 403);
                }

                // Get the job
                var job = await db.Jobs
                    .Include(j => j.JobItems.Select(ji => ji.Material1))
                    .Include(j => j.ShippingAddress1)
                    .Include(j => j.ShippingAddress1.USState)
                    .Include(j => j.ShippingOption)
                    .FirstOrDefaultAsync(j => j.Id == jobId && j.Deleted == null);

                if (job == null)
                {
                    return SendJsonResponse(new { success = false, message = "Job not found" }, 404);
                }

                // Verify job belongs to this client
                if (job.Client != clientId)
                {
                    return SendJsonResponse(new { success = false, message = "Access denied" }, 403);
                }

                // Build shipping info
                object shippingInfo = null;
                if (job.ShippingAddress1 != null)
                {
                    shippingInfo = new
                    {
                        method = job.ShippingOption?.Title,
                        address = job.ShippingAddress1.Address,
                        address2 = job.ShippingAddress1.Address2,
                        city = job.ShippingAddress1.City,
                        state = job.ShippingAddress1.USState?.Abbreviation,
                        zip = job.ShippingAddress1.Zip,
                        attention = job.ShippingAddress1.Attention
                    };
                }

                // Build items with proofs (same as GetProofingJobDetails)
                var items = new List<object>();
                foreach (var jobItem in job.JobItems.Where(ji => ji.Deleted == null))
                {
                    decimal? width = null;
                    decimal? height = null;
                    if (!string.IsNullOrEmpty(jobItem.Width) && decimal.TryParse(jobItem.Width, out decimal w))
                        width = w;
                    if (!string.IsNullOrEmpty(jobItem.Length) && decimal.TryParse(jobItem.Length, out decimal h))
                        height = h;

                    var proofs = await db.JobProofs
                        .Include(p => p.JobDocument)
                        .Where(p => p.JobItemId == jobItem.Id && p.Deleted == null)
                        .OrderByDescending(p => p.Version)
                        .ToListAsync();

                    var proofList = proofs.Select(p => new
                    {
                        proofId = p.Id,
                        version = p.Version,
                        status = p.ApprovalStatus,
                        statusText = p.ApprovalStatus == 0 ? "Pending" : (p.ApprovalStatus == 1 ? "Approved" : "Rejected"),
                        approvalDate = p.ApprovalDate,
                        approvalNotes = p.ApprovalNotes,
                        downloadUrl = $"/ClientPortal/DownloadProofSigned/{p.Id}?clientId={clientId}&sig={sig}",
                        thumbnailBase64 = GetProofThumbnailBase64(p.ThumbnailPath),
                        createdAt = p.CreatedAt
                    }).ToList();

                    // Skip items with no proofs
                    if (!proofList.Any())
                        continue;

                    items.Add(new
                    {
                        itemId = jobItem.Id,
                        title = jobItem.Title,
                        description = jobItem.Description,
                        quantity = jobItem.Quantity,
                        specs = new
                        {
                            width = width,
                            height = height,
                            material = jobItem.Material1?.FriendlyName ?? jobItem.Material1?.Title,
                            colorsFront = jobItem.PressColorProfileFront,
                            colorsBack = jobItem.PressColorProfileBack
                        },
                        notes = jobItem.Notes,
                        proofs = proofList
                    });
                }

                var response = new
                {
                    success = true,
                    job = new
                    {
                        jobId = job.Id,
                        jobNumber = job.JobNumber,
                        title = job.Title,
                        description = job.Description,
                        createdAt = job.CreatedAt,
                        dueDate = job.DueDate,
                        status = DetermineWorkflowState(job),
                        shipping = shippingInfo
                    },
                    items = items
                };

                return JsonWithISODates(response);
            }
            catch (Exception ex)
            {
                return SendJsonResponse(new { success = false, message = $"An error occurred: {ex.Message}" }, 500);
            }
        }

        /// <summary>
        /// Download proof PDF using signed access (no cookie auth required)
        /// GET: /ClientPortal/DownloadProofSigned/{proofId}?clientId={clientId}&sig={signature}
        /// </summary>
        [HttpGet]
        public async Task<ActionResult> DownloadProofSigned(int proofId, int clientId, string sig)
        {
            try
            {
                // Verify signature first
                string signingKey = System.Configuration.ConfigurationManager.AppSettings["ProofAccessSigningKey"];
                string expectedSignature = GenerateHmacSignature(clientId.ToString(), signingKey);

                if (!ConstantTimeEquals(sig ?? "", expectedSignature))
                {
                    return SendJsonResponse(new { success = false, message = "Invalid access link" }, 403);
                }

                var proof = await db.JobProofs.FindAsync(proofId);
                if (proof == null || proof.Deleted != null)
                    return SendJsonResponse(new { success = false, message = "Proof not found" }, 404);

                var jobItem = await db.JobItems.Include(ji => ji.Job1)
                    .FirstOrDefaultAsync(ji => ji.Id == proof.JobItemId);
                if (jobItem == null || jobItem.Job1 == null)
                    return SendJsonResponse(new { success = false, message = "Job not found" }, 404);

                // Verify proof belongs to this client
                if (jobItem.Job1.Client != clientId)
                    return SendJsonResponse(new { success = false, message = "Access denied" }, 403);

                if (!proof.JobDocumentId.HasValue)
                    return SendJsonResponse(new { success = false, message = "No document attached" }, 404);

                var jobDoc = await db.JobDocuments.FindAsync(proof.JobDocumentId.Value);
                if (jobDoc == null || string.IsNullOrEmpty(jobDoc.DocumentPath))
                    return SendJsonResponse(new { success = false, message = "Document not found" }, 404);

                if (!System.IO.File.Exists(jobDoc.DocumentPath))
                    return SendJsonResponse(new { success = false, message = "File not found" }, 404);

                string fileName = System.IO.Path.GetFileName(jobDoc.DocumentPath);
                return File(jobDoc.DocumentPath, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                return SendJsonResponse(new { success = false, message = ex.Message }, 500);
            }
        }

        /// <summary>
        /// Update proof status using signed access (no cookie auth required)
        /// POST: /ClientPortal/UpdateProofStatusSigned
        /// </summary>
        [HttpPost]
        public async Task<ActionResult> UpdateProofStatusSigned(int proofId, int status, int clientId, string sig, string notes = null)
        {
            try
            {
                // Verify signature first
                string signingKey = System.Configuration.ConfigurationManager.AppSettings["ProofAccessSigningKey"];
                string expectedSignature = GenerateHmacSignature(clientId.ToString(), signingKey);

                if (!ConstantTimeEquals(sig ?? "", expectedSignature))
                {
                    return SendJsonResponse(new { success = false, message = "Invalid access link" }, 403);
                }

                // Validate status (1 = approved, 2 = rejected)
                if (status != 1 && status != 2)
                {
                    return SendJsonResponse(new { success = false, message = "Invalid status. Use 1 for approved, 2 for rejected." }, 400);
                }

                var proof = await db.JobProofs
                    .Include(p => p.JobItem)
                    .FirstOrDefaultAsync(p => p.Id == proofId && p.Deleted == null);

                if (proof == null)
                {
                    return SendJsonResponse(new { success = false, message = "Proof not found" }, 404);
                }

                var job = await db.Jobs.FirstOrDefaultAsync(j => j.Id == proof.JobItem.Job);
                if (job == null)
                {
                    return SendJsonResponse(new { success = false, message = "Job not found" }, 404);
                }

                // Verify proof belongs to this client
                if (job.Client != clientId)
                {
                    return SendJsonResponse(new { success = false, message = "Access denied" }, 403);
                }

                // Update the proof status
                proof.ApprovalStatus = status;
                proof.ApprovalDate = DateTime.UtcNow;
                proof.ApprovalNotes = notes;

                await db.SaveChangesAsync();

                var statusText = status == 1 ? "approved" : "rejected";
                return SendJsonResponse(new { success = true, message = $"Proof {statusText} successfully" });
            }
            catch (Exception ex)
            {
                return SendJsonResponse(new { success = false, message = $"An error occurred: {ex.Message}" }, 500);
            }
        }

        /// <summary>
        /// Generate HMAC-SHA256 signature for a message
        /// </summary>
        private string GenerateHmacSignature(string message, string key)
        {
            using (var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(key)))
            {
                byte[] hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(message));
                return Convert.ToBase64String(hash).Replace("+", "-").Replace("/", "_").TrimEnd('='); // URL-safe base64
            }
        }

        /// <summary>
        /// Constant-time string comparison to prevent timing attacks
        /// </summary>
        private bool ConstantTimeEquals(string a, string b)
        {
            if (a == null || b == null)
                return false;
            if (a.Length != b.Length)
                return false;

            int result = 0;
            for (int i = 0; i < a.Length; i++)
            {
                result |= a[i] ^ b[i];
            }
            return result == 0;
        }

        #endregion Proofing Endpoints

        #region Client Onboarding Endpoints

        public async Task<ActionResult> ValidateOnboardingToken(string token)
        {
            try
            {
                if (string.IsNullOrEmpty(token))
                    return SendJsonResponse(new { success = false, message = "Token is required." }, 400);

                var onboardingToken = await db.ClientOnboardingTokens
                    .FirstOrDefaultAsync(t => t.Token == token);

                if (onboardingToken == null)
                    return SendJsonResponse(new { success = false, message = "Invalid token." }, 404);

                if (onboardingToken.Status != "Pending")
                    return SendJsonResponse(new { success = false, message = "This onboarding link has already been used." }, 400);

                if (onboardingToken.ExpiresAt < DateTime.UtcNow)
                {
                    onboardingToken.Status = "Expired";
                    await db.SaveChangesAsync();
                    return SendJsonResponse(new { success = false, message = "This onboarding link has expired." }, 400);
                }

                return SendJsonResponse(new
                {
                    success = true,
                    companyNameHint = onboardingToken.CompanyNameHint,
                    expiresAt = onboardingToken.ExpiresAt
                });
            }
            catch (Exception ex)
            {
                return SendJsonResponse(new { success = false, message = ex.Message }, 500);
            }
        }

        [HttpPost]
        public async Task<ActionResult> UploadOnboardingFile()
        {
            try
            {
                string token = Request.Form["token"];
                string fileType = Request.Form["fileType"];

                if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(fileType))
                    return SendJsonResponse(new { success = false, message = "Token and fileType are required." }, 400);

                var allowedTypes = new[] { "w9", "taxcert", "creditapp" };
                if (!allowedTypes.Contains(fileType.ToLower()))
                    return SendJsonResponse(new { success = false, message = "Invalid fileType. Must be w9, taxcert, or creditapp." }, 400);

                var onboardingToken = await db.ClientOnboardingTokens
                    .FirstOrDefaultAsync(t => t.Token == token);

                if (onboardingToken == null)
                    return SendJsonResponse(new { success = false, message = "Invalid token." }, 404);

                if (onboardingToken.Status != "Pending" || onboardingToken.ExpiresAt < DateTime.UtcNow)
                    return SendJsonResponse(new { success = false, message = "Token is expired or already used." }, 400);

                if (Request.Files.Count == 0 || Request.Files[0] == null || Request.Files[0].ContentLength == 0)
                    return SendJsonResponse(new { success = false, message = "No file provided." }, 400);

                var file = Request.Files[0];
                string safeFileName = System.IO.Path.GetFileName(file.FileName);
                string storageDir = System.IO.Path.Combine(@"C:\Storage\client_onboarding", token);

                if (!System.IO.Directory.Exists(storageDir))
                    System.IO.Directory.CreateDirectory(storageDir);

                string savedFileName = fileType.ToLower() + "_" + safeFileName;
                string fullPath = System.IO.Path.Combine(storageDir, savedFileName);
                file.SaveAs(fullPath);

                switch (fileType.ToLower())
                {
                    case "w9":
                        onboardingToken.CompanyNameHint = onboardingToken.CompanyNameHint; // preserve
                        // Store path on token temporarily; will transfer to Client on submit
                        break;
                    case "taxcert":
                        break;
                    case "creditapp":
                        break;
                }

                await db.SaveChangesAsync();

                return SendJsonResponse(new { success = true, fileName = savedFileName });
            }
            catch (Exception ex)
            {
                return SendJsonResponse(new { success = false, message = ex.Message }, 500);
            }
        }

        [HttpPost]
        public async Task<ActionResult> SubmitOnboarding()
        {
            try
            {
                string requestBody;
                using (var reader = new System.IO.StreamReader(Request.InputStream))
                {
                    Request.InputStream.Position = 0;
                    requestBody = await reader.ReadToEndAsync();
                }

                var submission = JsonConvert.DeserializeObject<VM_OnboardingSubmission>(requestBody);
                if (submission == null || string.IsNullOrEmpty(submission.Token))
                    return SendJsonResponse(new { success = false, message = "Invalid submission data." }, 400);

                var onboardingToken = await db.ClientOnboardingTokens
                    .FirstOrDefaultAsync(t => t.Token == submission.Token);

                if (onboardingToken == null)
                    return SendJsonResponse(new { success = false, message = "Invalid token." }, 404);

                if (onboardingToken.Status != "Pending")
                    return SendJsonResponse(new { success = false, message = "This onboarding link has already been used." }, 400);

                if (onboardingToken.ExpiresAt < DateTime.UtcNow)
                {
                    onboardingToken.Status = "Expired";
                    await db.SaveChangesAsync();
                    return SendJsonResponse(new { success = false, message = "This onboarding link has expired." }, 400);
                }

                // Resolve file paths from upload directory
                string storageDir = System.IO.Path.Combine(@"C:\Storage\client_onboarding", submission.Token);
                string w9Path = null, taxCertPath = null, creditAppPath = null;

                if (System.IO.Directory.Exists(storageDir))
                {
                    var files = System.IO.Directory.GetFiles(storageDir);
                    w9Path = files.FirstOrDefault(f => System.IO.Path.GetFileName(f).StartsWith("w9_"));
                    taxCertPath = files.FirstOrDefault(f => System.IO.Path.GetFileName(f).StartsWith("taxcert_"));
                    creditAppPath = files.FirstOrDefault(f => System.IO.Path.GetFileName(f).StartsWith("creditapp_"));
                }

                // Create client
                var billing = submission.BillingAddress;
                var client = new Client
                {
                    Title = submission.LegalCompanyName,
                    Address = billing?.Address,
                    City = billing?.City,
                    Zip = billing?.Zip,
                    USStateId = billing?.StateId,
                    BillingAddress = billing?.Address,
                    BillingCity = billing?.City,
                    BillingZip = billing?.Zip,
                    BillingUSState = billing?.StateId,
                    Markup = 0.4m,
                    BlindShipping = false,
                    IsGolfClient = false,
                    CreditHold = false,
                    TaxExempt = submission.TaxExempt,
                    AREmail = submission.ApContact?.Email,
                    ThirdPartyShippingAccount = submission.ThirdPartyShippingAccount,
                    CreditRefCheckRequested = submission.RequestCreditCheck,
                    CreditReferences = submission.CreditReferences != null && submission.CreditReferences.Count > 0
                        ? JsonConvert.SerializeObject(submission.CreditReferences)
                        : null,
                    W9FilePath = w9Path,
                    TaxCertFilePath = taxCertPath,
                    CreditAppFilePath = creditAppPath
                };

                db.Clients.Add(client);
                await db.SaveChangesAsync();

                // Create AP contact
                if (submission.ApContact != null)
                {
                    var apContact = new Contact
                    {
                        CusNo = client.Id,
                        First = submission.ApContact.FirstName,
                        Last = submission.ApContact.LastName,
                        Email = submission.ApContact.Email,
                        Phone = submission.ApContact.Phone,
                        Position = "Accounts Payable",
                        KeyContact = "Y"
                    };
                    db.Contacts.Add(apContact);
                }

                // Create Purchasing contact
                if (submission.PurchasingContact != null)
                {
                    var purchContact = new Contact
                    {
                        CusNo = client.Id,
                        First = submission.PurchasingContact.FirstName,
                        Last = submission.PurchasingContact.LastName,
                        Email = submission.PurchasingContact.Email,
                        Phone = submission.PurchasingContact.Phone,
                        Position = "Purchasing"
                    };
                    db.Contacts.Add(purchContact);
                }

                // Create shipping addresses
                if (submission.ShippingAddresses != null)
                {
                    foreach (var sa in submission.ShippingAddresses)
                    {
                        var shippingAddress = new ShippingAddress
                        {
                            Client = client.Id,
                            Address = sa.Address,
                            Address2 = sa.Address2,
                            City = sa.City,
                            State = sa.StateId,
                            Zip = sa.Zip,
                            Phone = sa.Phone,
                            Attention = sa.Attention,
                            CustomerName = sa.CustomerName,
                            IsDefaultForClient = sa.IsDefault
                        };
                        db.ShippingAddresses.Add(shippingAddress);
                    }
                }

                await db.SaveChangesAsync();

                // Update token
                onboardingToken.Status = "Completed";
                onboardingToken.CompletedAt = DateTime.UtcNow;
                onboardingToken.CreatedClientId = client.Id;
                await db.SaveChangesAsync();

                // Send notification email
                if (!string.IsNullOrEmpty(onboardingToken.NotificationEmail))
                {
                    try
                    {
                        var email = new OnboardingCompleteEmail(db);
                        await email.SendOnboardingCompleteNotification(
                            onboardingToken.NotificationEmail,
                            submission.LegalCompanyName,
                            client.Id);
                    }
                    catch (Exception) { /* don't fail submission for email */ }
                }

                return SendJsonResponse(new { success = true, clientId = client.Id });
            }
            catch (Exception ex)
            {
                return SendJsonResponse(new { success = false, message = ex.Message }, 500);
            }
        }

        #endregion Client Onboarding Endpoints
    }
}