using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using FifthPress.Models.ViewModels;
using FifthPress.Models;
using System.Data.Entity;
using FifthPress.ActionFilters;
using System.IO;
using System.Security.Cryptography;

namespace FifthPress.Controllers
{
    [CookieFilter(Order = 1)]
    [UserPermissions(Order = 2)]
    public class OrderController : DataController
    {
        public async Task<ActionResult> SubmitOrder(QuoteRequest request, List<QuoteRequestItem> items, List<HttpPostedFileBase> files)
        {
            files = files ?? new List<HttpPostedFileBase>();

            int userId = UserSessionHelper.GetActiveUserId(HttpContext);

            int itemDebugCt = 1;
            //validate length and width entries (try decimal parse)
            foreach (QuoteRequestItem qri in items.ToList())
            {
                bool lengthPassed = Decimal.TryParse(qri.LengthInches, out _);

                bool widthPassed = Decimal.TryParse(qri.WidthInches, out _);

                if (!widthPassed || !lengthPassed)
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Invalid Dimension Entry On Item #" + itemDebugCt.ToString());

                itemDebugCt++;
            }

            request.SubmissionDate = DateTime.UtcNow;
            request.SubmittedBy = userId;

            //submit the request to get an id
            db.QuoteRequests.Add(request);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (Exception)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Unkown error saving request");
            }

            await db.Entry(request).ReloadAsync();

            //build file entities
            List<QuoteRequestFile> submittedFiles = new List<QuoteRequestFile>();

            string saveBase = @"C:\Storage\OrderFiles";

            //create a directory for files if some were submitted
            if ((files ?? new List<HttpPostedFileBase>()).Count > 0)
            {
                saveBase = Path.Combine(saveBase, request.Id.ToString());
                Directory.CreateDirectory(saveBase);
            }

            foreach (HttpPostedFileBase f in files)
            {
                QuoteRequestFile qrf = new QuoteRequestFile
                {
                    QuoteRequest = request.Id
                };

                // Read file contents
                using (var reader = new BinaryReader(f.InputStream))
                {
                    byte[] fileContent = reader.ReadBytes(f.ContentLength);
                    byte[] hashBytes;
                    using (SHA256 sha256 = SHA256.Create())
                    {
                        hashBytes = sha256.ComputeHash(fileContent);
                    }

                    qrf.FileHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower(); // Remove dashes
                    qrf.FileSizeKb = (int)Math.Round((double)fileContent.Length / 1024, 0);
                    qrf.FileLocation = Path.Combine(saveBase, f.FileName);

                    // Ensure directory exists
                    string directory = Path.GetDirectoryName(qrf.FileLocation);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    try
                    {
                        // Write the file to disk
                        System.IO.File.WriteAllBytes(qrf.FileLocation, fileContent);
                        db.QuoteRequestFiles.Add(qrf);
                    }
                    catch (Exception)
                    {
                        MastermindLogger.Log(LogLevel.Error, "Critical error while saving files on order request with id = " + request.Id.ToString());
                        throw;
                    }
                }

                try
                {
                    await db.SaveChangesAsync();
                }
                catch (Exception)
                {
                    MastermindLogger.Log(LogLevel.Error, "Critical error while saving file records to database for request with id = " + request.Id.ToString());
                }
            }

            //save the items
            foreach (QuoteRequestItem qri in items)
            {
                qri.QuoteRequest = request.Id;
                db.QuoteRequestItems.Add(qri);
            }

            try
            {
                await db.SaveChangesAsync();
            }
            catch (Exception)
            {
                MastermindLogger.Log(LogLevel.Error, "Critical error while attempting to save items for request with id = " + request.Id.ToString());
                throw;
            }

            //done (this is where email would send)
            return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK);
        }

        public async Task<ActionResult> RequestForm()
        {
            VM_OrderRequest g = new VM_OrderRequest
            {
                //Clients = await GenerateSelectListAsync(SelectListType.Clients),
                Clients = await db.Clients.Where(x => x.Deleted == null)
                                          .Select(x => new SelectListItem
                                          {
                                              Value = x.Id.ToString(),
                                              Text = x.Title
                                          })
                                          .OrderBy(x => x.Text)
                                          .ToListAsync(),
                Materials = await db.Materials
                                    .Where(x => x.FriendlyName != null && x.FriendlyName != "" && (x.IncludeOnGolfOrder ?? false))
                                    .Select(x => new SelectListItem
                                    {
                                        Value = x.Id.ToString(),
                                        Text = x.FriendlyName
                                    })
                                    .ToListAsync()
            };

            // Add a "None" option to Materials
            ((List<SelectListItem>)g.Materials).Add(new SelectListItem
            {
                Value = "-1",
                Text = "None"
            });
            // Add the "Other" option to the Materials list
            ((List<SelectListItem>)g.Materials).Add(new SelectListItem
            {
                Value = "99999",
                Text = "Other"
            });

            ((List<SelectListItem>)g.Clients).Add(new SelectListItem
            {
                Value = "99999",
                Text = "Other"
            });

            g.Materials = g.Materials.AsEnumerable().OrderBy(x => int.Parse(x.Value)).ToList();

            ViewData["Client.USStateId"] = await GenerateSelectListAsync(SelectListType.Clients);

            return View(g);
        }

        public async Task<ActionResult> GetBillingInfo(int? clientId)
        {
            Client c = await db.Clients.FindAsync(clientId);

            if (c != null)
            {
                await db.Entry(c).Reference(r => r.USState2).LoadAsync();

                return new JsonResult
                {
                    Data = new
                    {
                        c.BillingAddress,
                        c.BillingCity,
                        BillingState = c.BillingUSState.HasValue ? c.USState2.Abbreviation : "",
                        c.BillingZip,
                        c.AREmail
                    },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Client not found");
        }

        public async Task<ActionResult> GetShippingAddresses(int? clientId)
        {
            Client c = await db.Clients.FindAsync(clientId);

            if (c != null)
            {
                await db.Entry(c).Collection(d => d.ShippingAddresses).LoadAsync();

                foreach (ShippingAddress sa in c.ShippingAddresses.ToList())
                    await db.Entry(sa).Reference(r => r.USState).LoadAsync();

                return new JsonResult
                {
                    Data = c.ShippingAddresses.Where(x => x.Deleted == null).Select(x => new
                    {
                        x.Id,
                        x.Address,
                        x.City,
                        State = x.USState.Abbreviation,
                        x.Zip,
                        x.CustomerName
                    }),
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Client not found");
        }
    }
}