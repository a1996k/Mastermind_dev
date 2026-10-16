using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Configuration;
using System.Text;
using FifthPress.Models.ViewModels2.PO25;

namespace FifthPress
{
    public class NotificationService25
    {
        private readonly FifthPressEntities _db;
        private readonly PORoleService25 _roleService;

        public NotificationService25()
        {
            _db = new FifthPressEntities();
            _roleService = new PORoleService25(_db);
        }

        public NotificationService25(FifthPressEntities db)
        {
            _db = db;
        }

        public void SendApprovalNotification(int poId, int approverUserId)
        {
            var po = _db.PO_Orders25.Find(poId);
            if (po == null) return;

            var approver = _db.Users.Find(approverUserId);
            var submitter = _db.Users.Find(po.SubmittedByUserId);

            if (approver == null || submitter == null) return;

            // Send email to submitter
            var subject = $"PO #{poId} Approved by {approver.FirstName} {approver.LastName}";
            var body = BuildApprovalEmailBody(po, approver, submitter);

            SendEmail(submitter.Email, subject, body);

            // Send Teams notification if configured
            SendTeamsNotification($"✅ PO #{poId} approved by {approver.FirstName} {approver.LastName}", po);

            // Check if fully approved and notify next approver if needed
            var approvalService = new ApprovalService25(_db);
            if (!approvalService.IsFullyApproved(poId))
            {
                NotifyNextApprover(poId);
            }
        }

        public void SendDenialNotification(int poId, int approverUserId)
        {
            var po = _db.PO_Orders25.Find(poId);
            if (po == null) return;

            var approver = _db.Users.Find(approverUserId);
            var submitter = _db.Users.Find(po.SubmittedByUserId);

            if (approver == null || submitter == null) return;

            var subject = $"PO #{poId} Denied by {approver.FirstName} {approver.LastName}";
            var body = BuildDenialEmailBody(po, approver, submitter);

            SendEmail(submitter.Email, subject, body);
            SendTeamsNotification($"❌ PO #{poId} denied by {approver.FirstName} {approver.LastName}", po);
        }

        public void SendReturnNotification(int poId, int approverUserId)
        {
            var po = _db.PO_Orders25.Find(poId);
            if (po == null) return;

            var approver = _db.Users.Find(approverUserId);
            var submitter = _db.Users.Find(po.SubmittedByUserId);

            if (approver == null || submitter == null) return;

            var subject = $"PO #{poId} Returned for Revision by {approver.FirstName} {approver.LastName}";
            var body = BuildReturnEmailBody(po, approver, submitter);

            SendEmail(submitter.Email, subject, body);
            SendTeamsNotification($"🔄 PO #{poId} returned for revision by {approver.FirstName} {approver.LastName}", po);
        }

        public void SendSubmissionNotification(int poId)
        {
            var po = _db.PO_Orders25.Find(poId);
            if (po == null) return;

            var submitter = _db.Users.Find(po.SubmittedByUserId);
            if (submitter == null) return;

            // Notify department approvers (only if department is set)
            var departmentApprovers = po.DepartmentId.HasValue
                ? GetDepartmentApprovers(po.DepartmentId.Value)
                : new List<User>();
            var totalAmount = po.PO_LineItems25.Sum(li => li.Cost * li.Quantity);

            foreach (var approver in departmentApprovers)
            {
                var subject = $"New PO #{poId} Awaiting Approval - ${totalAmount:F2}";
                var body = BuildSubmissionEmailBody(po, submitter, approver);
                SendEmail(approver.Email, subject, body);
            }

            SendTeamsNotification($"📋 New PO #{poId} submitted by {submitter.FirstName} {submitter.LastName} - ${totalAmount:F2}", po);
        }

        public void SendSLAWarningNotification(int poId)
        {
            var po = _db.PO_Orders25.Find(poId);
            if (po == null) return;

            var slaService = new SLAService25(_db);
            var slaStatus = slaService.GetPOStatus(poId);

            if (slaStatus.IsOverdue)
            {
                // Notify escalation contacts
                var escalationEmails = GetEscalationContacts();
                var subject = $"OVERDUE: PO #{poId} SLA Breach";
                var body = BuildSLABreachEmailBody(po, slaStatus);

                foreach (var email in escalationEmails)
                {
                    SendEmail(email, subject, body);
                }

                SendTeamsNotification($"⚠️ OVERDUE: PO #{poId} is {Math.Abs(slaStatus.HoursRemaining)} hours overdue", po);
            }
        }

        #region Private Email Building Methods

        private string BuildApprovalEmailBody(PO_Orders25 po, User approver, User submitter)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Hello {submitter.FirstName},");
            sb.AppendLine();
            sb.AppendLine($"Your Purchase Order #{po.PO_OrderId} has been approved by {approver.FirstName} {approver.LastName}.");
            sb.AppendLine();
            sb.AppendLine("PO Details:");
            sb.AppendLine($"Total Amount: ${po.PO_LineItems25.Sum(li => li.Cost * li.Quantity):F2}");
            sb.AppendLine($"Department: {po.Department?.Title}");
            // NEW CODE (add this instead):
            var jobTickets = po.PO_LineItems25
                .Where(li => !string.IsNullOrEmpty(li.JobTicketNumber))
                .Select(li => li.JobTicketNumber)
                .Distinct()
                .ToList();

            if (jobTickets.Any())
            {
                sb.AppendLine($"Job Tickets: {string.Join(", ", jobTickets)}");
            }
            sb.AppendLine();

            var approvalService = new ApprovalService25(_db);
            if (approvalService.IsFullyApproved(po.PO_OrderId))
            {
                sb.AppendLine("✅ Your PO is fully approved and ready for ordering!");
            }
            else
            {
                sb.AppendLine("⏳ Your PO is awaiting additional approval before ordering.");
            }

            sb.AppendLine();
            sb.AppendLine($"View PO: {GetPOUrl(po.PO_OrderId)}");
            sb.AppendLine();
            sb.AppendLine("Thank you!");

            return sb.ToString();
        }

        private string BuildDenialEmailBody(PO_Orders25 po, User approver, User submitter)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Hello {submitter.FirstName},");
            sb.AppendLine();
            sb.AppendLine($"Your Purchase Order #{po.PO_OrderId} has been denied by {approver.FirstName} {approver.LastName}.");
            sb.AppendLine();

            // Get the denial approval record for comments
            var denial = _db.PO_Approvals25
                .Where(a => a.PO_OrderId == po.PO_OrderId && a.ApproverUserId == approver.Id && a.ActionTaken == "Denied")
                .OrderByDescending(a => a.ActionDate)
                .FirstOrDefault();

            if (denial != null && !string.IsNullOrEmpty(denial.Comments))
            {
                sb.AppendLine("Reason for denial:");
                sb.AppendLine(denial.Comments);
                sb.AppendLine();
            }

            sb.AppendLine("Please review the feedback and create a new PO if needed.");
            sb.AppendLine();
            sb.AppendLine($"View PO: {GetPOUrl(po.PO_OrderId)}");

            return sb.ToString();
        }

        private string BuildReturnEmailBody(PO_Orders25 po, User approver, User submitter)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Hello {submitter.FirstName},");
            sb.AppendLine();
            sb.AppendLine($"Your Purchase Order #{po.PO_OrderId} has been returned for revision by {approver.FirstName} {approver.LastName}.");
            sb.AppendLine();

            // Get the return approval record for comments
            var returnRecord = _db.PO_Approvals25
                .Where(a => a.PO_OrderId == po.PO_OrderId && a.ApproverUserId == approver.Id && a.ActionTaken == "SentBack")
                .OrderByDescending(a => a.ActionDate)
                .FirstOrDefault();

            if (returnRecord != null && !string.IsNullOrEmpty(returnRecord.Comments))
            {
                sb.AppendLine("Comments:");
                sb.AppendLine(returnRecord.Comments);
                sb.AppendLine();
            }

            sb.AppendLine("Please make the requested changes and resubmit your PO.");
            sb.AppendLine();
            sb.AppendLine($"Edit PO: {GetPOEditUrl(po.PO_OrderId)}");

            return sb.ToString();
        }

        private string BuildSubmissionEmailBody(PO_Orders25 po, User submitter, User approver)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Hello {approver.FirstName},");
            sb.AppendLine();
            sb.AppendLine($"A new Purchase Order #{po.PO_OrderId} has been submitted and requires your approval.");
            sb.AppendLine();
            sb.AppendLine("PO Details:");
            sb.AppendLine($"Submitted by: {submitter.FirstName} ${submitter.LastName}");
            sb.AppendLine($"Total Amount: ${po.PO_LineItems25.Sum(li => li.Cost * li.Quantity):F2}");
            sb.AppendLine($"Department: {po.Department?.Title}");
            // NEW CODE (add this instead):
            var jobTickets = po.PO_LineItems25
                .Where(li => !string.IsNullOrEmpty(li.JobTicketNumber))
                .Select(li => li.JobTicketNumber)
                .Distinct()
                .ToList();

            if (jobTickets.Any())
            {
                sb.AppendLine($"Job Tickets: {string.Join(", ", jobTickets)}");
            }
            sb.AppendLine();

            var slaService = new SLAService25(_db);
            var slaStatus = slaService.GetPOStatus(po.PO_OrderId);
            sb.AppendLine($"Due Date: {slaStatus.DueDate:MM/dd/yyyy hh:mm tt}");
            sb.AppendLine();
            sb.AppendLine($"Review and Approve: {GetApprovalUrl(po.PO_OrderId)}");

            return sb.ToString();
        }

        private string BuildSLABreachEmailBody(PO_Orders25 po, VM_SLAStatus25 slaStatus)
        {
            var sb = new StringBuilder();
            sb.AppendLine("SLA BREACH ALERT");
            sb.AppendLine();
            sb.AppendLine($"Purchase Order #{po.PO_OrderId} has breached its SLA.");
            sb.AppendLine();
            sb.AppendLine("Details:");
            sb.AppendLine($"Submitted: {slaStatus.SubmissionDate:MM/dd/yyyy hh:mm tt}");
            sb.AppendLine($"Due Date: {slaStatus.DueDate:MM/dd/yyyy hh:mm tt}");
            sb.AppendLine($"Hours Overdue: {Math.Abs(slaStatus.HoursRemaining)}");
            sb.AppendLine($"Total Amount: ${po.PO_LineItems25.Sum(li => li.Cost * li.Quantity):F2}");
            sb.AppendLine();
            sb.AppendLine($"Immediate action required: {GetApprovalUrl(po.PO_OrderId)}");

            return sb.ToString();
        }

        #endregion Private Email Building Methods

        #region Private Helper Methods

        private void SendEmail(string to, string subject, string body)
        {
            
            /*try
            {
                using (var client = new SmtpClient())
                {
                    var message = new MailMessage
                    {
                        From = new MailAddress(ConfigurationManager.AppSettings["POSystemFromEmail"] ?? "po-system@fifthpress.com"),
                        Subject = subject,
                        Body = body,
                        IsBodyHtml = false
                    };

                    message.To.Add(to);
                    client.Send(message);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't throw - notifications shouldn't break the main process
                System.Diagnostics.Debug.WriteLine($"Email send failed: {ex.Message}");
            }*/
        }

        private void SendTeamsNotification(string message, PO_Orders25 po)
        {
            //try
            //{
            //    // Basic Teams webhook implementation
            //    // You would implement the actual Teams webhook call here
            //    var teamsWebhookUrl = ConfigurationManager.AppSettings["TeamsWebhookUrl"];
            //    if (string.IsNullOrEmpty(teamsWebhookUrl)) return;

            //    // Implementation would go here for Teams webhook
            //    // For now, just log the message
            //    System.Diagnostics.Debug.WriteLine($"Teams notification: {message}");
            //}
            //catch (Exception ex)
            //{
            //    System.Diagnostics.Debug.WriteLine($"Teams notification failed: {ex.Message}");
            //}
        }

        private List<User> GetDepartmentApprovers(int departmentId)
        {
            // Get users who can approve for this department
            return _db.PO_UserRoles25
                .Where(ur => ur.IsActive &&
                            ur.PO_Roles25.IsActive &&
                            (ur.DepartmentId == departmentId || ur.DepartmentId == null) &&
                            (ur.PO_Roles25.RoleName == "Owner" ||
                             ur.PO_Roles25.RoleName == "DepartmentManager" ||
                             ur.PO_Roles25.RoleName == "DepartmentApprover"))
                .Select(ur => ur.User)
                .Distinct()
                .ToList();
        }

        private List<string> GetEscalationContacts()
        {
            return _db.PO_UserRoles25
                .Where(ur => ur.IsActive &&
                            ur.PO_Roles25.IsActive &&
                            (ur.PO_Roles25.RoleName == "Owner" || ur.PO_Roles25.RoleName == "POAdmin"))
                .Select(ur => ur.User.Email)
                .Distinct()
                .ToList();
        }

        private void NotifyNextApprover(int poId)
        {
            var po = _db.PO_Orders25.Find(poId);
            if (po == null) return;

            var totalAmount = po.PO_LineItems25.Sum(li => li.Cost * li.Quantity);
            var requiresOwnerApproval = totalAmount > 5000;

            var approvals = _db.PO_Approvals25
                .Where(a => a.PO_OrderId == poId && a.ActionTaken == "Approved")
                .ToList();

            var hasDepartmentApproval = !po.DepartmentId.HasValue || approvals.Any(a =>
                _roleService.IsDepartmentApprover(a.ApproverUserId, po.DepartmentId.Value));

            // If we have department approval but need owner approval
            if (hasDepartmentApproval && requiresOwnerApproval)
            {
                var owners = _db.PO_UserRoles25
                    .Where(ur => ur.IsActive && ur.PO_Roles25.RoleName == "Owner")
                    .Select(ur => ur.User)
                    .ToList();

                var submitter = _db.Users.Find(po.SubmittedByUserId);

                foreach (var owner in owners)
                {
                    var subject = $"Owner Approval Required: PO #{poId} - ${totalAmount:F2}";
                    var body = BuildOwnerApprovalEmailBody(po, submitter, owner);
                    SendEmail(owner.Email, subject, body);
                }
            }
        }

        private string BuildOwnerApprovalEmailBody(PO_Orders25 po, User submitter, User owner)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Hello {owner.FirstName},");
            sb.AppendLine();
            sb.AppendLine($"Purchase Order #{po.PO_OrderId} requires owner approval due to amount over $5,000.");
            sb.AppendLine();
            sb.AppendLine("PO Details:");
            sb.AppendLine($"Submitted by: {submitter.FirstName} {submitter.LastName}");
            sb.AppendLine($"Total Amount: ${po.PO_LineItems25.Sum(li => li.Cost * li.Quantity):F2}");
            sb.AppendLine($"Department: {po.Department?.Title}");
            sb.AppendLine();
            sb.AppendLine("Department approval has been completed.");
            sb.AppendLine();
            sb.AppendLine($"Review and Approve: {GetApprovalUrl(po.PO_OrderId)}");

            return sb.ToString();
        }

        private bool IsDepartmentApprover(int userId, int departmentId)
        {
            return _roleService.IsDepartmentApprover(userId, departmentId);
        }

        private string GetPOUrl(int poId)
        {
            var baseUrl = ConfigurationManager.AppSettings["BaseUrl"] ?? "https://yoursite.com";
            return $"{baseUrl}/PO25/Details/{poId}";
        }

        private string GetPOEditUrl(int poId)
        {
            var baseUrl = ConfigurationManager.AppSettings["BaseUrl"] ?? "https://yoursite.com";
            return $"{baseUrl}/PO25/Edit/{poId}";
        }

        private string GetApprovalUrl(int poId)
        {
            var baseUrl = ConfigurationManager.AppSettings["BaseUrl"] ?? "https://yoursite.com";
            return $"{baseUrl}/PO25Approval/Review/{poId}";
        }

        #endregion Private Helper Methods
    }
}