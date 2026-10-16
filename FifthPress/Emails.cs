using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FifthPress;
using System.Linq;
using System.Data.Entity;
using System.Net.Mail;
using System.Net;
using System.Configuration;
using System.Net.Configuration;
using Newtonsoft.Json;

public static class Helpers
{
    public static string DecimalHoursToHumanReadable(decimal? hours)
    {
        // Check if hours is null
        if (hours == null)
        {
            return "No time provided";
        }

        // Separate the integer hours from the fractional part
        int wholeHours = (int)hours.Value;
        int minutes = (int)((hours.Value - wholeHours) * 60);

        // Format the result as human-readable time
        return $"{wholeHours} Hr{(wholeHours != 1 ? "s." : ".")} {minutes} Min{(minutes != 1 ? "s." : ".")}";
    }

    public static string DecimalHoursToFormatted(decimal? hours)
    {
        if (hours == null)
        {
            return "";
        }
        else
        {
            decimal pt = hours ?? 0.00m;

            int wholeHours = (int)pt;
            decimal fractionalHours = pt - wholeHours;

            int minutes = (int)(fractionalHours * 60);

            //one minute minimum
            if (hours.Value > 0m && wholeHours == 0 && minutes == 0)
            {
                return "0:01";
            }

            return $"{wholeHours}:{minutes:D2}";
        }
    }
}

public abstract class BaseEmail
{
    public string To { get; set; }
    public string From { get; set; }
    public string Subject { get; set; }
    public string Body { get; set; }

    protected readonly FifthPressEntities _db;

    protected BaseEmail(FifthPressEntities db)
    {
        _db = db;
    }

    public async Task SendAsync(string EmailTo)
    {
        // Retrieve SMTP settings from web.config
        var smtpSection = (SmtpSection)ConfigurationManager.GetSection("system.net/mailSettings/smtp");

        if (smtpSection == null)
        {
            throw new ConfigurationErrorsException("SMTP configuration section not found in web.config.");
        }

        using (var message = new MailMessage())
        using (var client = new SmtpClient(smtpSection.Network.Host, smtpSection.Network.Port))
        {
            message.From = new MailAddress(smtpSection.From);
            message.To.Add(EmailTo);
            message.Subject = Subject;
            message.Body = Body;
            message.IsBodyHtml = true;

            // Configure SMTP client
            client.EnableSsl = smtpSection.Network.EnableSsl;
            client.Credentials = new NetworkCredential(smtpSection.Network.UserName, smtpSection.Network.Password);

            try
            {
                await client.SendMailAsync(message);
                Console.WriteLine($"Email sent to {To} successfully!");
            }
            catch (SmtpException ex)
            {
                Console.WriteLine($"SMTP Error: {ex.StatusCode} - {ex.Message}");
                throw; // Rethrow to allow calling code to handle the exception
            }
            catch (Exception ex)
            {
                Console.WriteLine($"General Error: {ex.Message}");
                throw; // Rethrow to allow calling code to handle the exception
            }
        }
    }
}

public class UtilizationEmail : BaseEmail
{
    public UtilizationEmail()
        : base(null) { }

    public async Task SendPersonalPrintUtilizationEmail(string destination, string emailBody)
    {
        string EmailTitle = "Personal Utilization Report";

        //From = "Mastermind@fifthcolor.com";
        //To = destination;
        Subject = EmailTitle;
        Body = emailBody;

        try
        {
            await SendAsync(destination);
        }
        catch (Exception ex)
        {
            // Handle email sending error (optional logging or rethrowing)
            Console.WriteLine($"Error sending print utilization email: {ex.Message}");
        }
    }

    public async Task SendPersonalAgencyUtilizationEmail(string destination, string emailBody)
    {
        string EmailTitle = "Personal Utilization Report";

        //From = "Mastermind@fifthcolor.com";
        //To = destination;
        Subject = EmailTitle;
        Body = emailBody;

        try
        {
            await SendAsync(destination);
        }
        catch (Exception ex)
        {
            // Handle email sending error (optional logging or rethrowing)
            Console.WriteLine($"Error sending Agency Utilization email: {ex.Message}");
        }
    }

    public async Task SendDepartmentUtilizationEmail(string emailTitle, string destination, string emailBody)
    {
        //string EmailTitle = departmentName + " Department Utilization Report";

        //From = "Mastermind@fifthcolor.com";
        //To = destination;
        Subject = emailTitle;
        Body = emailBody;

        try
        {
            await SendAsync(destination);
        }
        catch (Exception ex)
        {
            // Handle email sending error (optional logging or rethrowing)
            Console.WriteLine($"Error sending {emailTitle}: {ex.Message}");
        }
    }
}

// Define specific email types
public class PerformanceEmail : BaseEmail
{
    public PerformanceEmail(FifthPressEntities dbContext)
        : base(dbContext) { }

    public async Task SendJobRecapEmail(string testEmail = null)
    {
        string EmailTitle = "Daily Job Recap";

        // Fetch email configuration from the database
        Email e = await _db.Emails.SingleAsync(x => x.Title == EmailTitle);

        DateTime fromDate = DateTime.Now.AddDays(-1).Date;
        string body = $"<h2>{EmailTitle} - {fromDate.ToString("MMMM dd, yyyy")}</h2><br><br>";

        // Fetch jobs and quotes
        var jobs = await _db.Jobs
            .Include(i => i.Client1)
            .Include(i => i.User)
            .Include(i => i.JobItems.Select(ji => ji.Material1.MaterialInventory))
            .Include(i => i.JobItems.Select(ji => ji.JobItemFinishingMaterials.Select(jfm => jfm.Material1.MaterialInventory)))
            .Where(x => DbFunctions.TruncateTime(x.CreatedAt.Value) == fromDate.Date)
            .ToListAsync();

        var quotes = await _db.Quotes
            .Include(i => i.Client1)
            .Include(i => i.User)
            .Where(x => DbFunctions.TruncateTime(x.CreatedAt.Value) == fromDate.Date)
            .ToListAsync();

        // Build email body
        if (quotes.Count > 0)
        {
            body += "<h3>Today's Quotes:</h3><br>";
            foreach (var quote in quotes)
            {
                body += $"Quote# {quote.JobNumber ?? "No Quote Number"}, Client: {quote.Client1?.Title ?? "No Client Specified"}, Created By: {(quote.User?.FirstName + " " + quote.User?.LastName) ?? "Unknown User"}, Created At: {quote.CreatedAt?.ToLocalTime().ToString("MM/dd/yyyy HH:mm") ?? "Unknown Date"}<br>";
            }
        }
        else
        {
            body += "<p>No quotes were created today.</p><br>";
        }

        body += "<br>";

        // Pre-load material classification data
        var allMaterialIds = jobs
            .SelectMany(j => j.JobItems.Where(ji => ji.Deleted == null))
            .SelectMany(ji =>
            {
                var ids = new List<int>();
                if (ji.Material.HasValue) ids.Add(ji.Material.Value);
                ids.AddRange(ji.JobItemFinishingMaterials.Select(fm => fm.Material));
                return ids;
            }).Distinct().ToList();

        var poMaterialIds = new HashSet<int>();
        if (allMaterialIds.Any())
        {
            var activePOItems = await _db.PO_Items
                .Include(p => p.PO_Orders)
                .Where(p => p.TrackInventory == true
                    && allMaterialIds.Contains(p.Item)
                    && p.PO_Orders.Status == "approved"
                    && (p.PO_Orders.Received == null || p.PO_Orders.Received == false))
                .ToListAsync();
            poMaterialIds = new HashSet<int>(activePOItems.Select(p => p.Item));
        }

        var reorderMaterialIds = new HashSet<int>(
            _db.LowStockInventory3().Select(ls => ls.MaterialId));

        if (jobs.Count > 0)
        {
            body += "<h3>Today's Jobs:</h3><br>";
            foreach (var job in jobs)
            {
                body += $"Job# {job.JobNumber ?? "No Job Number"}, Client: {job.Client1?.Title ?? "No Client Specified"}, Due Date: {job.DueDate?.ToString("MM/dd/yyyy") ?? "No Due Date"}, From Quote: {job.QuoteNumber ?? "No Quote Number"}, Created By: {(job.User?.FirstName + " " + job.User?.LastName) ?? "Unknown User"}, Created At: {job.CreatedAt?.ToLocalTime().ToString("MM/dd/yyyy HH:mm") ?? "Unknown Date"}<br>";

                // Primary materials
                foreach (var ji in job.JobItems.Where(ji => ji.Deleted == null && ji.Material.HasValue))
                {
                    var mat = ji.Material1;
                    if (mat == null) continue;
                    string code = mat.Title ?? "Unknown Material";
                    string qty = $"{ji.RunSheets ?? 0} sheets";
                    string classification = GetMaterialClassification(mat.Id, mat.MaterialInventory, poMaterialIds, reorderMaterialIds);
                    body += $"&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;{code} - {qty} - {classification}<br>";
                }

                // Finishing materials
                foreach (var ji in job.JobItems.Where(ji => ji.Deleted == null))
                {
                    foreach (var fm in ji.JobItemFinishingMaterials)
                    {
                        var mat = fm.Material1;
                        if (mat == null) continue;
                        string code = mat.Title ?? "Unknown Material";
                        string unitText = ParseFinishingUnit(fm.FinishingFormulaConfiguration);
                        string qty = $"{fm.FinalCount ?? 0} {unitText}";
                        string classification = GetMaterialClassification(mat.Id, mat.MaterialInventory, poMaterialIds, reorderMaterialIds);
                        body += $"&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;{code} - {qty} - {classification}<br>";
                    }
                }
            }
        }
        else
        {
            body += "<p>No jobs were created today.</p><br>";
        }

        // Send email to recipients
        List<EmailUserPivot> recipients = await _db.EmailUserPivots
            .Include(i => i.User1)
            .Where(x => x.Email == e.Id)
            .ToListAsync();

        Subject = EmailTitle;
        Body = body;

        if (!string.IsNullOrEmpty(testEmail))
        {
            await SendAsync(testEmail);
        }
        else if (recipients.Count > 0)
        {
            foreach (EmailUserPivot eup in recipients)
            {
                try
                {
                    await SendAsync(eup.User1.Email);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending job recap email: {ex.Message}");
                    throw;
                }
            }
        }
    }

    private string GetMaterialClassification(int materialId, MaterialInventory inventory, HashSet<int> poMaterialIds, HashSet<int> reorderMaterialIds)
    {
        int available = (inventory?.Stock ?? 0) - (inventory?.Allocated ?? 0);
        var parts = new List<string> { $"{available} available" };
        if (poMaterialIds.Contains(materialId)) parts.Add("PO issued");
        if (reorderMaterialIds.Contains(materialId)) parts.Add("on reorder report");
        return string.Join(" | ", parts);
    }

    private string ParseFinishingUnit(string config)
    {
        if (string.IsNullOrEmpty(config)) return "units";
        try
        {
            var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(config);
            if (dict != null && dict.Count > 0)
                return dict.Keys.First();
        }
        catch { }
        return "units";
    }
}

public abstract class PurchaseOrderEmail : BaseEmail
{
    protected PurchaseOrderEmail(FifthPressEntities dbContext)
        : base(dbContext) { }
}

public class VendorAlertEmail : PurchaseOrderEmail
{
    public VendorAlertEmail(FifthPressEntities dbContext)
        : base(dbContext) { }
}

public class ClientLoginMagicLinkEmail : BaseEmail
{
    public ClientLoginMagicLinkEmail()
        : base(null) { }

    public async Task SendClientViewAuthEmail(string destination, string emailBody)
    {
        string EmailTitle = "Your Login Link - FifthColor Client Portal";

        Subject = EmailTitle;
        Body = emailBody;

        try
        {
            await SendAsync(destination);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending client view auth email: {ex.Message}");
            throw;
        }
    }
}

public class ApprovalAlertEmail : PurchaseOrderEmail
{
    public ApprovalAlertEmail(FifthPressEntities dbContext)
        : base(dbContext) { }

    public async Task SendPendingSpecialApprovals()
    {
        // Get all PO_Orders with RequiredSpecialApproval = true and SpecialApprovalSent = false
        var orders = await _db.PO_Orders
            .Where(x => (x.RequiredSpecialApproval ?? false) && !(x.SpecialApprovalSent ?? false))
            .ToListAsync();

        // Only proceed if there are orders requiring an email to be sent
        if (orders.Count > 0)
        {
            // Build the email body
            Body = "The following orders require special approval: " + Environment.NewLine;

            foreach (PO_Orders o in orders)
            {
                await _db.Entry(o).Reference(r => r.Vendor1).LoadAsync();

                Body += $"PO# {o.PONumber ?? "No PO#"} for {o.Vendor1?.Title ?? "No Vendor Specified!"} ( ${o.POTotal ?? 0m} ): {@"{redacted}" + o.Id}{Environment.NewLine}";
            }

            // Get the approvers
            List<User> specialApprovers = await _db.Users
                .Where(x => (x.IsSpecialApprover ?? false))
                .ToListAsync();

            // Build the recipient list
            var recipientEmails = specialApprovers
                .Where(u => !string.IsNullOrEmpty(u.Email))
                .Select(u => u.Email)
                .ToList();

            if (recipientEmails.Count > 0)
            {
                // To = string.Join(",", recipientEmails);
                //From = "your_email@yourdomain.com"; // Centralize this or make it configurable
                Subject = "Special PO Approvals Required";

                try
                {
                    // Send the email
                    await SendAsync(string.Join(",", recipientEmails));

                    // Update the orders to mark them as sent
                    foreach (PO_Orders o in orders)
                    {
                        o.SpecialApprovalSent = true;
                        o.SpecialApprovalSentDate = DateTime.UtcNow;
                        _db.Entry(o).State = System.Data.Entity.EntityState.Modified;
                    }

                    // Save changes to the database
                    await _db.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    // Log the error and rethrow the exception
                    Console.WriteLine($"Error sending approval alert email: {ex.Message}");
                    throw;
                }
            }
            else
            {
                Console.WriteLine("No valid email addresses found for special approvers.");
            }
        }
        else
        {
            Console.WriteLine("No orders require special approval at this time.");
        }
    }
}

public abstract class CustomerEmail : BaseEmail
{
    protected CustomerEmail(FifthPressEntities dbContext)
        : base(dbContext) { }
}

public class JobAlertEmail : CustomerEmail
{
    public JobAlertEmail(FifthPressEntities dbContext)
        : base(dbContext) { }
}

public class QuoteEmail : CustomerEmail
{
    public QuoteEmail(FifthPressEntities dbContext)
        : base(dbContext) { }
}

public class InvoiceEmail : CustomerEmail
{
    public InvoiceEmail(FifthPressEntities dbContext)
        : base(dbContext) { }
}

public class LowMarkupAlertEmail : BaseEmail
{
    public LowMarkupAlertEmail(FifthPressEntities dbContext)
        : base(dbContext) { }

    public async Task SendLowMarkupNotification(string jobNumber, string quoteNumber, decimal markupPercentage, string createdByName)
    {
        string EmailTitle = "Low Markup Alert";

        Email e = await _db.Emails.SingleOrDefaultAsync(x => x.Title == EmailTitle);
        if (e == null) return;

        List<EmailUserPivot> recipients = await _db.EmailUserPivots
            .Include(i => i.User1)
            .Where(x => x.Email == e.Id)
            .ToListAsync();

        if (recipients.Count == 0) return;

        bool isDev = false;
        var context = System.Web.HttpContext.Current;
        if (context != null)
        {
            string host = context.Request.Url.Host.ToLower();
            isDev = host.Contains("internals.fifthcolor.com") || host.Contains("localhost");
        }

        string devBanner = isDev
            ? "<div style=\"background-color: #ff0000; color: #fff; padding: 10px; margin-bottom: 15px; font-weight: bold; text-align: center;\">⚠ THIS EMAIL WAS SENT FROM THE DEV ENVIRONMENT ⚠</div>"
            : "";

        string recordLabel = !string.IsNullOrEmpty(jobNumber)
            ? $"Job #{jobNumber}"
            : $"Quote #{quoteNumber}";

        string subjectLine = (isDev ? "[DEV] " : "")
            + $"{recordLabel} Markup {markupPercentage:N2}% is less than 35% ({createdByName})";

        string body = devBanner
            + $"<h2>Low Markup Alert</h2>"
            + $"<p><strong>{recordLabel}</strong> has a markup of <strong>{markupPercentage:N2}%</strong>, which is below the 35% threshold.</p>"
            + $"<p><strong>Changed By:</strong> {createdByName}</p>"
            + $"<p><strong>Date:</strong> {DateTime.Now.ToString("MMMM dd, yyyy h:mm tt")}</p>";

        Subject = subjectLine;
        Body = body;

        foreach (EmailUserPivot eup in recipients)
        {
            if (!string.IsNullOrEmpty(eup.User1?.Email))
            {
                try
                {
                    await SendAsync(eup.User1.Email);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending low markup alert to {eup.User1.Email}: {ex.Message}");
                }
            }
        }
    }
}

public class CreditHoldEmail : BaseEmail
{
    public CreditHoldEmail(FifthPressEntities dbContext)
        : base(dbContext) { }

    public async Task SendCreditHoldNotification(string clientName)
    {
        string EmailTitle = "Credit Hold Notification";

        Email e = await _db.Emails.SingleOrDefaultAsync(x => x.Title == EmailTitle);
        if (e == null) return;

        List<EmailUserPivot> recipients = await _db.EmailUserPivots
            .Include(i => i.User1)
            .Where(x => x.Email == e.Id)
            .ToListAsync();

        if (recipients.Count == 0) return;

        bool isDev = false;
        var context = System.Web.HttpContext.Current;
        if (context != null)
        {
            string host = context.Request.Url.Host.ToLower();
            isDev = host.Contains("internals.fifthcolor.com") || host.Contains("localhost");
        }

        string devBanner = isDev
            ? "<div style=\"background-color: #ff0000; color: #fff; padding: 10px; margin-bottom: 15px; font-weight: bold; text-align: center;\">⚠ THIS EMAIL WAS SENT FROM THE DEV ENVIRONMENT ⚠</div>"
            : "";

        string body = devBanner
            + $"<h2>Credit Hold Notification</h2>"
            + $"<p>The following client has been placed on <strong>credit hold</strong>:</p>"
            + $"<p><strong>Client:</strong> {clientName}</p>"
            + $"<p><strong>Date:</strong> {DateTime.Now.ToString("MMMM dd, yyyy h:mm tt")}</p>"
            + $"<p>No delivery tickets can be generated for this client until the hold is removed.</p>";

        Subject = (isDev ? "[DEV] " : "") + clientName + " is on HOLD";
        Body = body;

        foreach (EmailUserPivot eup in recipients)
        {
            if (!string.IsNullOrEmpty(eup.User1?.Email))
            {
                try
                {
                    await SendAsync(eup.User1.Email);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending credit hold notification to {eup.User1.Email}: {ex.Message}");
                }
            }
        }
    }

    public async Task SendCreditHoldRemovedNotification(string clientName)
    {
        string EmailTitle = "Credit Hold Notification";

        Email e = await _db.Emails.SingleOrDefaultAsync(x => x.Title == EmailTitle);
        if (e == null) return;

        List<EmailUserPivot> recipients = await _db.EmailUserPivots
            .Include(i => i.User1)
            .Where(x => x.Email == e.Id)
            .ToListAsync();

        if (recipients.Count == 0) return;

        bool isDev = false;
        var context = System.Web.HttpContext.Current; // kept for CreditHoldEmail
        if (context != null)
        {
            string host = context.Request.Url.Host.ToLower();
            isDev = host.Contains("internals.fifthcolor.com") || host.Contains("localhost");
        }

        string devBanner = isDev
            ? "<div style=\"background-color: #ff0000; color: #fff; padding: 10px; margin-bottom: 15px; font-weight: bold; text-align: center;\">⚠ THIS EMAIL WAS SENT FROM THE DEV ENVIRONMENT ⚠</div>"
            : "";

        string body = devBanner
            + $"<h2>Credit Hold Removed</h2>"
            + $"<p>The following client has been <strong>removed from credit hold</strong>:</p>"
            + $"<p><strong>Client:</strong> {clientName}</p>"
            + $"<p><strong>Date:</strong> {DateTime.Now.ToString("MMMM dd, yyyy h:mm tt")}</p>"
            + $"<p>Delivery tickets can now be generated for this client.</p>";

        Subject = (isDev ? "[DEV] " : "") + clientName + " is OFF HOLD";
        Body = body;

        foreach (EmailUserPivot eup in recipients)
        {
            if (!string.IsNullOrEmpty(eup.User1?.Email))
            {
                try
                {
                    await SendAsync(eup.User1.Email);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending credit hold removed notification to {eup.User1.Email}: {ex.Message}");
                }
            }
        }
    }
}

public class OnboardingCompleteEmail : BaseEmail
{
    public OnboardingCompleteEmail(FifthPress.FifthPressEntities dbContext)
        : base(dbContext) { }

    public async Task SendOnboardingCompleteNotification(string recipientEmail, string companyName, int clientId)
    {
        if (string.IsNullOrEmpty(recipientEmail)) return;

        bool isDev = false;
        var context = System.Web.HttpContext.Current;
        if (context != null)
        {
            string host = context.Request.Url.Host.ToLower();
            isDev = host.Contains("internals.fifthcolor.com") || host.Contains("localhost");
        }

        string devBanner = isDev
            ? "<div style=\"background-color: #ff0000; color: #fff; padding: 10px; margin-bottom: 15px; font-weight: bold; text-align: center;\">THIS EMAIL WAS SENT FROM THE DEV ENVIRONMENT</div>"
            : "";

        string mastermindUrl = isDev
            ? "https://internals.fifthcolor.com"
            : "https://mm.fifthcolor.com";

        string body = devBanner
            + $"<h2>Client Onboarding Complete</h2>"
            + $"<p>A new client has completed the onboarding form:</p>"
            + $"<p><strong>Company:</strong> {System.Net.WebUtility.HtmlEncode(companyName)}</p>"
            + $"<p><strong>Client ID:</strong> {clientId}</p>"
            + $"<p><a href=\"{mastermindUrl}/#clients/{clientId}\">View Client in Mastermind</a></p>"
            + $"<p><strong>Date:</strong> {DateTime.Now.ToString("MMMM dd, yyyy h:mm tt")}</p>";

        Subject = (isDev ? "[DEV] " : "") + "New Client Onboarding: " + companyName;
        Body = body;

        try
        {
            await SendAsync(recipientEmail);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending onboarding complete notification to {recipientEmail}: {ex.Message}");
        }
    }
}