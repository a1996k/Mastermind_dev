using FifthPress.ActionFilters;
using FifthPress.Models.ViewModels;
using FifthPress.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Activities.Statements;
using System.Data.Entity;
using FifthPress.Models.ControllerModels;
using Newtonsoft.Json.Serialization;
using FifthPress.Models.ViewModels2;

namespace FifthPress.Controllers
{
    [CookieFilter(Order = 1)]
    [UserPermissions(Order = 2)]
    public class ShippingController : DataController
    {
        [HttpGet]
        public async Task<ActionResult> GetJobItemsForShipment(int jobId, int? excludeShipmentId = null)
        {
            try
            {
                var query = db.JobItems
                    .Where(x => x.Job == jobId && x.LastShipped == null);

                if (excludeShipmentId.HasValue)
                {
                    var existingItemIds = db.JobShipmentPivots
                        .Where(p => p.Shipment == excludeShipmentId.Value)
                        .Select(p => p.JobItem);

                    query = query.Where(x => !existingItemIds.Contains(x.Id));
                }

                var items = await query
                    .Select(x => new
                    {
                        x.Id,
                        x.Ordinal,
                        x.Description,
                        x.Job,
                        JobNumber = x.Job1.JobNumber
                    })
                    .ToListAsync();

                return Json(new { items }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return new HttpStatusCodeResult(500, ex.Message);
            }
        }

        public async Task<ActionResult> MarkShipmentAsShipped(int? shipment)
        {
            JobShipment s = await db.JobShipments.FindAsync(shipment);
            if (s != null)
            {
                //load the pivot records
                await db.Entry(s).Collection(c => c.JobShipmentPivots).LoadAsync();

                //set the ship date and by whom
                s.ShippedAtUTC = DateTime.UtcNow;
                s.ShippedBy = UserSessionHelper.GetActiveUserId(HttpContext);

                foreach (JobShipmentPivot j in s.JobShipmentPivots.ToList())
                {
                    await db.Entry(j).Reference(r => r.JobItem1).LoadAsync();

                    j.JobItem1.LastShipped = s.ShippedAtUTC;
                    db.Entry(j.JobItem1).State = EntityState.Modified;
                }

                try
                {
                    await db.SaveChangesAsync();

                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK);
                }
                catch (Exception e)
                {
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, e.Message);
                }
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Shipment not found");
        }

        [HttpGet]
        public async Task<ActionResult> GetShippingAddressesForShipment(int shipmentId)
        {
            var clientIds = await db.JobShipmentPivots
                .Where(p => p.Shipment == shipmentId && p.Job != null)
                .Select(p => p.Job1.Client)
                .Distinct()
                .ToListAsync();

            var addresses = await db.ShippingAddresses
                .Include(i => i.USState)
                .Where(sa => clientIds.Contains(sa.Client))
                .Select(sa => new
                {
                    sa.Id,
                    Display = sa.Address + ", " + sa.City + ", " + sa.USState.Abbreviation + " " + sa.Zip
                })
                .ToListAsync();

            return Json(addresses, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult EditJobShipmentBasic(int id)
        {
            var shipment = db.JobShipments
                .Include(s => s.JobShipmentPivots.Select(p => p.JobItem1.Job1))
                .FirstOrDefault(s => s.Id == id);

            if (shipment == null)
                return HttpNotFound();

            return View("EditJobShipmentBasic", shipment);
        }

        [HttpPost]
        public ActionResult EditJobShipmentBasic(FormCollection form)
        {
            if (!int.TryParse(form["Id"], out int id))
                return HttpNotFound();

            string title = form["Title"];
            string notes = form["Notes"];
            var jobIdsRaw = form.GetValues("JobIds");
            var jobIds = jobIdsRaw?.Select(int.Parse).ToList() ?? new List<int>();

            var shipment = db.JobShipments
                .Include("JobShipmentPivots")
                .FirstOrDefault(s => s.Id == id);

            if (shipment == null)
                return HttpNotFound();

            shipment.Title = title;
            shipment.Notes = notes;

            db.JobShipmentPivots.RemoveRange(shipment.JobShipmentPivots);

            foreach (var jobId in jobIds)
            {
                var itemIds = db.JobItems
                    .Where(ji => ji.Job == jobId && ji.LastShipped == null)
                    .Select(ji => ji.Id)
                    .ToList();

                foreach (var itemId in itemIds)
                {
                    db.JobShipmentPivots.Add(new JobShipmentPivot
                    {
                        Shipment = shipment.Id,
                        JobItem = itemId,
                        Job = jobId
                    });
                }
            }

            db.SaveChanges();

            return RedirectToAction("ReviewShipments");
        }

        public async Task<ActionResult> SaveJobShipment(CM_AddNewShipment shipment, string action)
        {
            if (shipment == null)
                return new HttpStatusCodeResult(400, "Shipment object not bound at all");

            if (string.IsNullOrWhiteSpace(shipment.Title))
                return new HttpStatusCodeResult(400, "Missing Title");

            if (shipment.JobIds == null || !shipment.JobIds.Any())
                return new HttpStatusCodeResult(400, "No Jobs provided");

            try
            {
                JobShipment js = new JobShipment
                {
                    Title = shipment.Title,
                    Notes = shipment.Notes,
                    CreatedAtUTC = DateTime.UtcNow,
                    CreatedBy = UserSessionHelper.GetActiveUserId(HttpContext)
                };

                db.JobShipments.Add(js);

                await db.SaveChangesAsync();
                await db.Entry(js).ReloadAsync();

                foreach (int jobId in shipment.JobIds)
                {
                    var itemIds = await db.JobItems
                        .Where(ji => ji.Job == jobId && ji.LastShipped == null)
                        .Select(ji => ji.Id)
                        .ToListAsync();

                    foreach (int itemId in itemIds)
                    {
                        db.JobShipmentPivots.Add(new JobShipmentPivot
                        {
                            JobItem = itemId,
                            Shipment = js.Id,
                            Job = jobId
                        });
                    }
                }

                await db.SaveChangesAsync();

                await db.Entry(js).ReloadAsync();

                if (action == "saveAndFinalize")
                {
                    return Json(new { success = true, js.Id });
                }

                return Json(new { success = true });
            }
            catch (Exception)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Shipment save error");
            }
        }

        public async Task<ActionResult> ReviewShipments()
        {
            var shipments = await db.JobShipments
                .Where(s => s.ShippedAtUTC == null)
                .Include(s => s.JobShipmentPivots.Select(p => p.Job1))
                .Include(s => s.JobShipmentPivots.Select(p => p.JobItem1))
                .OrderByDescending(s => s.CreatedAtUTC)
                .ToListAsync();

            return View("ReviewShipments", shipments);
        }

        [HttpPost]
        public async Task<ActionResult> FinalizeShipment(CM_ShipmentFinalize model)
        {
            var shipment = await db.JobShipments
                .FirstOrDefaultAsync(s => s.Id == model.JobShipmentId);

            if (shipment == null)
                return HttpNotFound();

            var now = model.ShipmentDate ?? DateTime.UtcNow;
            int userId = UserSessionHelper.GetActiveUserId(HttpContext);

            //guard against double-submit creating duplicate ShipmentsShipped rows
            //(same JobShipment + tracking# + freight + date already saved)
            var jobShipmentId = shipment.Id;
            var tn = model.TrackingNumber;
            var fp = model.FreightPrice;
            var alreadySaved = await db.ShipmentsShippeds.AnyAsync(s =>
                s.JobShipment == jobShipmentId
                && s.TrackingNumber == tn
                && s.FreightPrice == fp
                && s.ShipmentDate == now);

            if (alreadySaved)
                return new HttpStatusCodeResult(200);

            shipment.ShippedAtUTC = now;
            shipment.ShippedBy = userId;

            db.ShipmentsShippeds.Add(new ShipmentsShipped
            {
                JobShipment = shipment.Id,
                FreightCost = model.FreightCost,
                Markup = model.Markup,
                FreightPrice = model.FreightPrice,
                TrackingNumber = model.TrackingNumber,
                ShippingAddress = model.ShippingAddress,
                Notes = model.Notes,
                InternalCharge = model.InternalCharge,
                ShipmentDate = now,
                UserId = userId
            });

            var jobIdsAffected = await db.JobShipmentPivots
                .Where(p => p.Shipment == shipment.Id && p.Job != null)
                .Select(p => p.Job.Value)
                .Distinct()
                .ToListAsync();

            var jobsToUpdate = await db.Jobs
                .Where(j => jobIdsAffected.Contains(j.Id) && j.Shipped == null)
                .ToListAsync();

            foreach (var job in jobsToUpdate)
            {
                job.Shipped = now;
            }

            await db.SaveChangesAsync();

            return new HttpStatusCodeResult(200);
        }

        public async Task<ActionResult> ViewShipped()
        {
            var shipments = await db.JobShipments
                .Where(s => s.ShippedAtUTC != null)
                .Include(s => s.JobShipmentPivots.Select(p => p.JobItem1.Job1)) // include Job1 via JobItem1
                .Include(s => s.ShipmentsShippeds)
                .OrderByDescending(s => s.ShippedAtUTC)
                .ToListAsync();

            return View(shipments);
        }

        public ActionResult NewJobShipment()
        {
            var res = new
            {
                html = RenderViewToString("~/Views/Shipping/Partials/_NewJobShipment.cshtml", null)
            };

            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(res, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> BundleJobShipments_Index()
        {
            return View();
        }

        /// <summary>
        /// NEW FUNCTION: this is meant to save edits to partial and full job shipments. added when JobShipments was added
        /// </summary>

        [HttpPost]
        public async Task<ActionResult> SaveEditedShippedOrders(
            List<CM_LegacyShippedOrderEdit> legacyOrders,
            List<CM_JobShipmentEdit> jobShipments)
        {
            legacyOrders = legacyOrders ?? new List<CM_LegacyShippedOrderEdit>();
            jobShipments = jobShipments ?? new List<CM_JobShipmentEdit>();

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    // Update OrdersShippeds
                    foreach (var legacy in legacyOrders)
                    {
                        var record = await db.OrdersShippeds.FindAsync(legacy.Id);
                        if (record != null)
                        {
                            record.FreightCost = legacy.FreightCost;
                            record.Markup = legacy.Markup;
                            record.FreightPrice = legacy.FreightPrice;
                            record.InternalCharge = legacy.InternalCharge;
                            record.Notes = legacy.Notes;
                            record.TrackingNumber = legacy.TrackingNumber;
                        }
                    }

                    // Update ShipmentsShipped
                    foreach (var shipment in jobShipments)
                    {
                        var detail = await db.ShipmentsShippeds
                            .FirstOrDefaultAsync(x => x.JobShipment == shipment.Id);

                        if (detail != null)
                        {
                            detail.FreightCost = shipment.FreightCost;
                            detail.Markup = shipment.Markup;
                            detail.FreightPrice = shipment.FreightPrice;
                            detail.Notes = shipment.Notes;
                            detail.TrackingNumber = shipment.TrackingNumber;
                            detail.InternalCharge = shipment.InternalCharge;
                        }
                    }

                    await db.SaveChangesAsync();
                    transaction.Commit();
                    return Json(new { success = true });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return Json(new { success = false, error = ex.Message });
                }
            }
        }

        public async Task<ActionResult> GetShippedOrdersForEdit(int jobId)
        {
            var legacyShipped = await db.OrdersShippeds
                .Where(x => x.Job == jobId)
                .Include(x => x.User)
                .ToListAsync();

            var jobShipments = await db.JobShipments
                .Where(s => s.ShippedAtUTC != null &&
                            s.JobShipmentPivots.Any(p => p.Job == jobId))
                .Include(s => s.ShipmentsShippeds)
                .Include(s => s.JobShipmentPivots.Select(p => p.JobItem1.Job1))
                .Include(s => s.JobShipmentPivots.Select(p => p.JobItem1)) // needed for Quantity, Description, etc.
                .ToListAsync();

            var vm = new VM_ShippedOrdersModal
            {
                LegacyShippedOrders = legacyShipped,
                JobShipments = jobShipments
            };

            return PartialView("~/Views/Partials/_EditShippedOrdersModal_Body.cshtml", vm);
        }

        public async Task<ActionResult> EditShippedOrders(int? jobId)
        {
            var orders = await db.OrdersShippeds.Where(x => x.Job == jobId).ToListAsync();

            return View(orders);
        }

        public async Task<ActionResult> GetShippedOrders(int? jobId)
        {
            Job j = await db.Jobs.FindAsync(jobId);

            if (j != null)
            {
                await db.Entry(j).Collection(c => c.OrdersShippeds).LoadAsync();

                var res = new
                {
                    ModalHeader = RenderViewToString("~/Views/Shipping/Partials/_ShippedOrdersModalHeader.cshtml", j.JobNumber),
                    ModalBody = RenderViewToString("~/Views/Shipping/Partials/_ShippedOrdersModalBody.cshtml", j.OrdersShippeds.ToList()),
                    ModalFooter = RenderViewToString("~/Views/Shipping/Partials/_ShippedOrdersModalFooter.cshtml", j.OrdersShippeds.Count())
                };

                return new JsonResult
                {
                    Data = JsonConvert.SerializeObject(res),
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Job not found");
        }

        public async Task<ActionResult> GetShippedShipments(int? shipmentId)
        {
            JobShipment j = await db.JobShipments.FindAsync(shipmentId);

            if (j != null)
            {
                await db.Entry(j).Collection(c => c.ShipmentsShippeds).LoadAsync();

                var res = new
                {
                    ModalHeader = RenderViewToString("~/Views/Shipping/Partials/_ShippedShipmentsModalHeader.cshtml", new Tuple<string, string>(j.Title, j.Notes)),
                    ModalBody = RenderViewToString("~/Views/Shipping/Partials/_ShippedShipmentsModalBody.cshtml", j.ShipmentsShippeds.ToList()),
                    ModalFooter = RenderViewToString("~/Views/Shipping/Partials/_ShippedShipmentsModalFooter.cshtml", j.ShipmentsShippeds.Count())
                };

                return new JsonResult
                {
                    Data = JsonConvert.SerializeObject(res),
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Job not found");
        }

        public async Task<ActionResult> SaveOrderShipment(OrdersShipped model)
        {
            //get the shipping address from the job and save it to the model
            int userId = UserSessionHelper.GetActiveUserId(HttpContext);

            Job j = await db.Jobs.FindAsync(model.Job);
            if (j != null)
            {
                model.ShippingAddress = j.ShippingAddress;
                model.ShipmentDate = DateTime.UtcNow;
                model.UserId = userId;

                //add the new record to the database
                db.OrdersShippeds.Add(model);
                //save changes
                await db.SaveChangesAsync();

                await db.Entry(model).ReloadAsync();

                return new JsonResult
                {
                    Data = new
                    {
                        Id = model.Id.ToString(),
                        DateString = model.ShipmentDate.Value.ToLongDateString(),
                        model.TrackingNumber
                    },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Job not found");
        }

        public async Task<ActionResult> SetAddressOnJob(int? jobId, int? addressId)
        {
            try
            {
                Job j = await db.Jobs.FindAsync(jobId);
                j.ShippingAddress = addressId;
                db.Entry(j).State = System.Data.Entity.EntityState.Modified;
                await db.SaveChangesAsync();

                return new JsonResult
                {
                    Data = "Set",
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
            catch (Exception)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Error setting address; contact developer");
            }
        }

        //vvv refactor necessary because of prev parameters (see 1st iteration)
        public async Task<ActionResult> GetShippingAddressesForJob(int? job)
        {
            //load the job
            Job j = await db.Jobs.FindAsync(job);

            if (j != null)
            {
                Client c = await db.Clients.FindAsync(j.Client);

                if (c != null)
                {
                    await db.Entry(c).Collection(cl => cl.ShippingAddresses).LoadAsync();

                    var addresses = j.Client1.ShippingAddresses.Where(x => x.Deleted == null).ToList();

                    foreach (ShippingAddress sa in addresses)
                        await db.Entry(sa).Reference(r => r.USState).LoadAsync();

                    //we only arrived at this function if we didn't have a selected shipping address, so do not check for a selected

                    VM_ShippingAddressCollection vm = new VM_ShippingAddressCollection
                    {
                        ShippingAddresses = addresses,
                        SelectedAddressId = null
                    };

                    return new JsonResult
                    {
                        Data = JsonConvert.SerializeObject(RenderViewToString("~/Views/Partials/_ShippingAddressRowCollection.cshtml", vm), new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };
                }

                //client doesn't exist

                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Client not found");
            }

            //job doesn't exist
            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Job not found");
        }

        //json result: if job has a shipping address, return true. else false.
        public async Task<ActionResult> JobHasShippingAddress(int? job)
        {
            Job j = await db.Jobs.FindAsync(job);

            if (j != null)
            {
                bool hasAddress = j.ShippingAddress.HasValue;

                return new JsonResult
                {
                    Data = hasAddress,
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError);
        }

        public ActionResult Index()
        {
            return View();
        }
    }
}