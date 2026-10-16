using FifthPress.ActionFilters;
using FifthPress.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.Controllers
{
    [CookieFilter(Order = 1)]
    [UserPermissions(Order = 2)]
    public class ClientLoginController : DataController
    {
        // Configuration: Client login token timeout duration (4 hours = 240 minutes)
        private const int TOKEN_TIMEOUT_MINUTES = 240;

        // GET: ClientLogin/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: ClientLogin/Create
        [HttpPost]
        public async Task<ActionResult> Create(string name, List<string> emails, List<bool> isPrimary, List<int> clientIds)
        {
            try
            {
                // Validation
                if (emails == null || !emails.Any() || emails.All(string.IsNullOrWhiteSpace))
                {
                    return Json(new { success = false, message = "At least one email is required" });
                }

                if (clientIds == null || !clientIds.Any())
                {
                    return Json(new { success = false, message = "At least one client must be selected" });
                }

                // Filter out empty emails
                var validEmails = new List<(string Email, bool IsPrimary)>();
                for (int i = 0; i < emails.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(emails[i]))
                    {
                        validEmails.Add((emails[i].Trim(), isPrimary != null && i < isPrimary.Count && isPrimary[i]));
                    }
                }

                if (!validEmails.Any())
                {
                    return Json(new { success = false, message = "At least one valid email is required" });
                }

                // Check if any email already exists
                var emailAddresses = validEmails.Select(e => e.Email.ToLower()).ToList();

                /*var existingEmails = await db.ClientLoginEmails
                    .Where(e => emailAddresses.Contains(e.Email.ToLower()) && e.Deleted == null)
                    .Select(e => e.Email)
                    .ToListAsync();

                if (existingEmails.Any())
                {
                    return Json(new { success = false, message = $"The following email(s) already exist: {string.Join(", ", existingEmails)}" });
                }*/

                // Step 1: Create new ClientLogin record
                var clientLogin = new ClientLogin
                {
                    Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow,
                    FailedLoginAttempts = 0
                };

                db.ClientLogins.Add(clientLogin);

                // Step 2: Save changes and reload record to get the generated Id
                await db.SaveChangesAsync();

                // Reload to ensure we have the Id
                var createdLoginId = clientLogin.Id;

                // Step 3: Create ClientLoginEmail records for each email
                foreach (var emailEntry in validEmails)
                {
                    var clientLoginEmail = new ClientLoginEmail
                    {
                        ClientLoginId = createdLoginId,
                        Email = emailEntry.Email,
                        IsPrimary = emailEntry.IsPrimary,
                        CreatedDate = DateTime.UtcNow
                    };

                    db.ClientLoginEmails.Add(clientLoginEmail);
                }

                // Step 4: Create ClientLoginPivot records for each selected client
                foreach (var clientId in clientIds)
                {
                    var clientLoginPivot = new ClientLoginPivot
                    {
                        ClientLoginId = createdLoginId,
                        ClientId = clientId,
                        CreatedDate = DateTime.UtcNow
                    };

                    db.ClientLoginPivots.Add(clientLoginPivot);
                }

                // Step 5: Save all changes
                await db.SaveChangesAsync();

                return Json(new { success = true, message = "Client login created successfully!", clientLoginId = createdLoginId });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }

        // GET: ClientLogin/Edit/{id}
        public async Task<ActionResult> Edit(int id)
        {
            try
            {
                var clientLogin = await db.ClientLogins
                    .Include(cl => cl.ClientLoginEmails)
                    .Include(cl => cl.ClientLoginPivots.Select(p => p.Client))
                    .FirstOrDefaultAsync(cl => cl.Id == id && cl.Deleted == null);

                if (clientLogin == null)
                {
                    return HttpNotFound("Client login not found");
                }

                return View(clientLogin);
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "ClientLogin", "Edit"));
            }
        }

        // POST: ClientLogin/Edit
        [HttpPost]
        public async Task<ActionResult> Edit(int id, string name, List<string> emails, List<bool> isPrimary, List<int> clientIds)
        {
            try
            {
                var clientLogin = await db.ClientLogins
                    .Include(cl => cl.ClientLoginEmails)
                    .Include(cl => cl.ClientLoginPivots)
                    .FirstOrDefaultAsync(cl => cl.Id == id && cl.Deleted == null);

                if (clientLogin == null)
                {
                    return Json(new { success = false, message = "Client login not found" });
                }

                // Validation
                if (emails == null || !emails.Any() || emails.All(string.IsNullOrWhiteSpace))
                {
                    return Json(new { success = false, message = "At least one email is required" });
                }

                if (clientIds == null || !clientIds.Any())
                {
                    return Json(new { success = false, message = "At least one client must be selected" });
                }

                // Filter out empty emails
                var validEmails = new List<(string Email, bool IsPrimary)>();
                for (int i = 0; i < emails.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(emails[i]))
                    {
                        validEmails.Add((emails[i].Trim(), isPrimary != null && i < isPrimary.Count && isPrimary[i]));
                    }
                }

                if (!validEmails.Any())
                {
                    return Json(new { success = false, message = "At least one valid email is required" });
                }

                // Check if any email already exists (excluding current login's emails)
                var emailAddresses = validEmails.Select(e => e.Email.ToLower()).ToList();
                /*var existingEmails = await db.ClientLoginEmails
                    .Where(e => emailAddresses.Contains(e.Email.ToLower()) && e.Deleted == null && e.ClientLoginId != id)
                    .Select(e => e.Email)
                    .ToListAsync();

                if (existingEmails.Any())
                {
                    return Json(new { success = false, message = $"The following email(s) already exist: {string.Join(", ", existingEmails)}" });
                }*/

                // Update name
                clientLogin.Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();

                // Soft delete all existing emails for this login
                foreach (var existingEmail in clientLogin.ClientLoginEmails.Where(e => e.Deleted == null))
                {
                    existingEmail.Deleted = DateTime.UtcNow;
                }

                // Add new email records
                foreach (var emailEntry in validEmails)
                {
                    var clientLoginEmail = new ClientLoginEmail
                    {
                        ClientLoginId = id,
                        Email = emailEntry.Email,
                        IsPrimary = emailEntry.IsPrimary,
                        CreatedDate = DateTime.UtcNow
                    };

                    db.ClientLoginEmails.Add(clientLoginEmail);
                }

                // Soft delete all existing client pivots
                foreach (var existingPivot in clientLogin.ClientLoginPivots.Where(p => p.Deleted == null))
                {
                    existingPivot.Deleted = DateTime.UtcNow;
                }

                // Add new pivot records
                foreach (var clientId in clientIds)
                {
                    var clientLoginPivot = new ClientLoginPivot
                    {
                        ClientLoginId = id,
                        ClientId = clientId,
                        CreatedDate = DateTime.UtcNow
                    };

                    db.ClientLoginPivots.Add(clientLoginPivot);
                }

                await db.SaveChangesAsync();

                return Json(new { success = true, message = "Client login updated successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }

        // POST: ClientLogin/Delete
        [HttpPost]
        public async Task<ActionResult> Delete(int id)
        {
            try
            {
                var clientLogin = await db.ClientLogins
                    .Include(cl => cl.ClientLoginEmails)
                    .Include(cl => cl.ClientLoginPivots)
                    .FirstOrDefaultAsync(cl => cl.Id == id && cl.Deleted == null);

                if (clientLogin == null)
                {
                    return Json(new { success = false, message = "Client login not found" });
                }

                // Soft delete the client login
                clientLogin.Deleted = DateTime.UtcNow;

                // Soft delete associated emails
                foreach (var email in clientLogin.ClientLoginEmails.Where(e => e.Deleted == null))
                {
                    email.Deleted = DateTime.UtcNow;
                }

                // Soft delete associated pivots
                foreach (var pivot in clientLogin.ClientLoginPivots.Where(p => p.Deleted == null))
                {
                    pivot.Deleted = DateTime.UtcNow;
                }

                await db.SaveChangesAsync();

                return Json(new { success = true, message = "Client login deleted successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }

        // POST: ClientLogin/SendEmail
        [HttpPost]
        public async Task<ActionResult> SendEmail(int id, string email)
        {
            try
            {
                var clientLogin = await db.ClientLogins
                    .Include(cl => cl.ClientLoginEmails)
                    .Include(cl => cl.ClientLoginPivots.Select(p => p.Client))
                    .FirstOrDefaultAsync(cl => cl.Id == id && cl.Deleted == null);

                if (clientLogin == null)
                {
                    return Json(new { success = false, message = "Client login not found" });
                }

                // Validate that the email belongs to this ClientLogin
                var emailRecord = clientLogin.ClientLoginEmails
                    .FirstOrDefault(e => e.Email.Equals(email, StringComparison.OrdinalIgnoreCase) && e.Deleted == null);

                if (emailRecord == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Email address not associated with this login"
                    });
                }

                // Check if token exists and is still valid
                if (!string.IsNullOrEmpty(clientLogin.LoginToken) &&
                    clientLogin.TokenExpiration.HasValue &&
                    clientLogin.TokenExpiration.Value > DateTime.UtcNow)
                {
                    // Reuse existing token, extend expiration
                    clientLogin.TokenExpiration = DateTime.UtcNow.AddMinutes(TOKEN_TIMEOUT_MINUTES);
                }
                else
                {
                    // Generate new token (expired or doesn't exist)
                    clientLogin.LoginToken = Guid.NewGuid().ToString();
                    clientLogin.TokenExpiration = DateTime.UtcNow.AddMinutes(TOKEN_TIMEOUT_MINUTES);
                }

                await db.SaveChangesAsync();

                // Build client view link - points to frontend, not backend
                var clientViewBaseUrl = ConfigurationManager.AppSettings["ClientViewBaseUrl"];
                var clientViewLink = $"{clientViewBaseUrl}/verify?token={clientLogin.LoginToken}";

                // Get associated client names for email context
                var clientNames = clientLogin.ClientLoginPivots
                    .Where(p => p.Deleted == null)
                    .Select(p => p.Client.Title)
                    .ToList();

                // Create view model for email template
                var emailViewModel = new ClientViewAuthEmailViewModel
                {
                    ClientViewLink = clientViewLink,
                    ClientList = clientNames.Any() ? string.Join(", ", clientNames) : "your events",
                    ExpirationTime = clientLogin.TokenExpiration.Value.ToLocalTime().ToString("h:mm tt")
                };

                // Choose template based on whether this is first time or re-authentication
                // If LastLoginDate is null, they've never logged in -> Welcome email
                // If LastLoginDate has value, they've logged in before -> Re-auth email
                string templatePath = clientLogin.LastLoginDate == null
                    ? "~/Views/EmailTemplates/ClientViewAuthEmail.cshtml"      // Welcome email
                    : "~/Views/EmailTemplates/ClientViewReauthEmail.cshtml";   // Re-auth email

                // Render the email template to HTML string
                string emailBody = RenderViewToString(templatePath, emailViewModel);

                // Send email to the specific email address
                var emailSender = new ClientLoginMagicLinkEmail();
                await emailSender.SendClientViewAuthEmail(email, emailBody);

                return Json(new
                {
                    success = true,
                    message = $"Magic link sent to {email}",
                    expiresAt = clientLogin.TokenExpiration.Value.ToLocalTime().ToString("M/d/yyyy h:mm tt")
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }


        

        // GET: ClientLogin/RequestAccess
        public ActionResult RequestAccess()
        {
            return View();
        }

        // POST: ClientLogin/RequestAccess
        [HttpPost]
        public async Task<ActionResult> RequestAccess(string email)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                {
                    return Json(new { success = false, message = "Please enter an email address" });
                }

                // Look up email in ClientLoginEmail table
                var clientLoginEmail = await db.ClientLoginEmails
                    .Include(e => e.ClientLogin)
                    .Include(e => e.ClientLogin.ClientLoginPivots.Select(p => p.Client))
                    .FirstOrDefaultAsync(e => e.Email.Equals(email, StringComparison.OrdinalIgnoreCase) && e.Deleted == null);

                if (clientLoginEmail == null)
                {
                    return Json(new { success = false, message = "Invalid email address entered" });
                }

                var clientLogin = clientLoginEmail.ClientLogin;

                if (clientLogin.Deleted != null)
                {
                    return Json(new { success = false, message = "Invalid email address entered" });
                }

                // Generate new token
                clientLogin.LoginToken = Guid.NewGuid().ToString();
                clientLogin.TokenExpiration = DateTime.UtcNow.AddMinutes(TOKEN_TIMEOUT_MINUTES);
                await db.SaveChangesAsync();

                // Build client view link - points to frontend, not backend
                var clientViewBaseUrl = ConfigurationManager.AppSettings["ClientViewBaseUrl"];
                var clientViewLink = $"{clientViewBaseUrl}/verify?token={clientLogin.LoginToken}";

                // Get associated client names
                var clientNames = clientLogin.ClientLoginPivots
                    .Where(p => p.Deleted == null)
                    .Select(p => p.Client.Title)
                    .ToList();

                // Create view model for email template
                var emailViewModel = new ClientViewAuthEmailViewModel
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

                return Json(new
                {
                    success = true,
                    message = $"A new magic link has been sent to {email}"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }

        // GET: ClientLogin/Index
        public async Task<ActionResult> Index()
        {
            try
            {
                var clientLogins = await db.ClientLogins
                    .Where(cl => cl.Deleted == null)
                    .Include(cl => cl.ClientLoginEmails)
                    .Include(cl => cl.ClientLoginPivots.Select(p => p.Client))
                    .OrderByDescending(cl => cl.CreatedDate)
                    .ToListAsync();

                return View(clientLogins);
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "ClientLogin", "Index"));
            }
        }
    }
}