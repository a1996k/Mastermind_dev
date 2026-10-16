using FifthPress.Models.ViewModels.JobItemLabel;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using QRCoder;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;

namespace FifthPress.Controllers
{
    public class LabelController : DataController
    {
        public async Task<ActionResult> GetItemLabel(int? jobItemId)
        {
            List<VM_JobItemLabel> labels = new List<VM_JobItemLabel>();

            JobItem ji = await db.JobItems.FindAsync(jobItemId);

            if (ji != null)
            {
                await db.Entry(ji).Reference(r => r.Job1).LoadAsync();
                await db.Entry(ji.Job1).Reference(r => r.Client1).LoadAsync();

                // Manually construct the URL
                string jobFilesUrl = $"{Request.Url.Scheme}://{Request.Url.Authority}/{(Request.Url.Authority.ToUpper() != "MM.FIFTHCOLOR.COM" ? "FifthPress/" : "")}view-item-files?jobItemId={ji.Id}";

                labels.Add(new VM_JobItemLabel
                {
                    JobItemId = ji.Id,
                    JobNumber = ji.Job1.JobNumber,
                    ClientName = ji.Job1.Client1.Title,
                    ItemDescription = ji.Description,
                    QRCodeBase64 = GenerateQRCodeBase64(jobFilesUrl)
                });

                return View("PrintLabels", labels);
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Item not found");
        }

        public async Task<ActionResult> GetItemLabels(int? jobId)
        {
            List<VM_JobItemLabel> labels = new List<VM_JobItemLabel>();

            Job j = await db.Jobs.FindAsync(jobId);

            if (j != null)
            {
                await db.Entry(j).Reference(r => r.Client1).LoadAsync();
                await db.Entry(j).Collection(c => c.JobItems).LoadAsync();

                foreach (JobItem ji in j.JobItems.OrderBy(x => x.Ordinal).ToList())
                {
                    // Manually construct the URL
                    string jobFilesUrl = $"{Request.Url.Scheme}://{Request.Url.Authority}/{(Request.Url.Authority.ToUpper() != "MM.FIFTHCOLOR.COM" ? "FifthPress/" : "")}view-item-files?jobItemId={ji.Id}";

                    labels.Add(new VM_JobItemLabel
                    {
                        JobItemId = ji.Id,
                        JobNumber = j.JobNumber,
                        ClientName = j.Client1.Title,
                        ItemDescription = ji.Description,
                        QRCodeBase64 = GenerateQRCodeBase64(jobFilesUrl)
                    });
                }

                return View("PrintLabels", labels);
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Job not found");
        }

        // Helper method to generate a QR Code as a Base64 string
        private string GenerateQRCodeBase64(string text)
        {
            using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
            using (QRCodeData qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q))
            using (QRCode qrCode = new QRCode(qrCodeData))
            using (Bitmap qrCodeImage = qrCode.GetGraphic(20)) // Adjust size factor as needed
            using (MemoryStream ms = new MemoryStream())
            {
                qrCodeImage.Save(ms, ImageFormat.Png);
                return $"data:image/png;base64,{Convert.ToBase64String(ms.ToArray())}"; // Return Base64 string
            }
        }
    }
}