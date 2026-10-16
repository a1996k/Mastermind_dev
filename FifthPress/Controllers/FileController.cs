using FifthPress.ActionFilters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;
using System.IO;
using FifthPress.Models.ViewModels.JobItemLabel;

namespace FifthPress.Controllers
{
    [CookieFilter(Order = 1)]
    [UserPermissions(Order = 2)]
    public class FileController : DataController
    {
        public async Task<ActionResult> ViewItemFiles(int? jobItemId)
        {
            JobItem ji = await db.JobItems.FindAsync(jobItemId);

            if (ji != null)
            {
                //get all job documents pertaining to proofs
                List<JobDocument> proofs = await db.JobDocuments.Where(x => x.ItemId == jobItemId && x.DocumentPath.ToUpper().EndsWith("_PROOF.PDF")).ToListAsync();

                //set the file name from the path
                foreach (JobDocument jd in proofs.ToList())
                    jd.FileName = Path.GetFileName(jd.DocumentPath);

                //get a link to the job ticket -- should this be downloadable or just link? just link for now
                //GetJobTicket/{JobId}
                string jobTicketUrl = $"{Request.Url.Scheme}://{Request.Url.Authority}/GetJobTicket/{ji.Job.ToString()}";

                VM_JobItemFiles vm = new VM_JobItemFiles
                {
                    JobDocuments = proofs,
                    JobTicketUrl = jobTicketUrl
                };

                return View(vm);
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Job Item not found");
        }

        //function to download proof file
        //fileId: the id of the record corresponding to the file to be rerieved. UNSAFE unless the provided files to download are constrained to proofs, which is the intention
        public async Task<ActionResult> DownloadProof(int? proofId)
        {
            if (proofId == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, "Invalid Proof ID");
            }

            // Get the file indicated by proofId
            JobDocument jd = await db.JobDocuments.FindAsync(proofId);

            if (jd != null)
            {
                // Parse the file name
                jd.FileName = Path.GetFileName(jd.DocumentPath);

                // If the upper file name doesn't end in _PROOF.PDF, log the attempt and return 500
                if (!jd.FileName.ToUpper().EndsWith("_PROOF.PDF"))
                {
                    int userId = UserSessionHelper.GetActiveUserId(HttpContext);
                    MastermindLogger.Log(LogLevel.Warning, $"Attempt by {userId} to access JobDocument indicated by Id = {jd.Id}");
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Internal Error");
                }

                string filePath = jd.DocumentPath;

                // Check if the file exists before attempting to return it
                if (System.IO.File.Exists(filePath))
                {
                    byte[] fileBytes = System.IO.File.ReadAllBytes(filePath); // <-- Use synchronous method here
                    string contentType = "application/pdf"; // Assuming all proof files are PDFs

                    return File(fileBytes, contentType, jd.FileName);
                }
                else
                {
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.NotFound, "File not found");
                }
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Unable to find Job Document");
        }
    }
}