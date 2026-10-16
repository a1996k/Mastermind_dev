using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Threading.Tasks;
using System.Data.Entity;
using Newtonsoft.Json;
using FifthPress.Models;

namespace FifthPress.Controllers
{
    public class VendorsController : DataController
    {
        public async Task<ActionResult> GetAddressInfo(int? id)
        {
            Vendor v = await db.Vendors.FindAsync(id);
            await db.Entry(v).Reference(r => r.USState).LoadAsync();

            if (v != null)
            {
                var addr = new
                {
                    Address = v.Address ?? "",
                    Address2 = v.Address2 ?? "",
                    City = v.City ?? "",
                    USState = v.USState.Abbreviation ?? "",
                    Zip = v.Zip ?? "",
                    Phone = v.Phone ?? "",
                    TaxNo = "?",
                    Email = v.Email ?? "",
                    Fax = v.Fax ?? ""
                };

                return new JsonResult
                {
                    Data = JsonConvert.SerializeObject(addr, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Vendor not found");
        }

        public async Task<ActionResult> DynamicVendorCreation(Vendor vendor)
        {
            int nextVendorId = 0;
            try { nextVendorId = db.Vendors.Max(x => x.Id); } catch (Exception) { }
            nextVendorId++;

            vendor.Id = nextVendorId;
            db.Vendors.Add(vendor);
            await db.SaveChangesAsync();

            var vendors = await GenerateSelectListAsync(SelectListType.Vendors);

            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(vendors, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        // GET: Vendors/GetStates - Get states list for dropdown
        [HttpGet]
        public async Task<ActionResult> GetStates()
        {
            try
            {
                var states = await GenerateSelectListAsync(SelectListType.USStates);
                return Json(states, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error loading states: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // POST: Vendors/AddVendor - Modern API for PO25 system
        [HttpPost]
        public async Task<ActionResult> AddVendor(Vendor vendor)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(vendor.Title))
                {
                    return Json(new { success = false, message = "Vendor name is required." });
                }

                // Don't set Id - it's an identity column and will be auto-generated
                vendor.Active = 1; // Set as active by default

                db.Vendors.Add(vendor);
                await db.SaveChangesAsync();

                // After SaveChanges, the Id will be populated by EF
                return Json(new
                {
                    success = true,
                    vendor = new
                    {
                        id = vendor.Id,
                        title = vendor.Title,
                        contactName = vendor.ContactName,
                        phone = vendor.Phone,
                        address = vendor.Address,
                        city = vendor.City,
                        state = vendor.State,
                        zip = vendor.Zip
                    },
                    message = "Vendor added successfully."
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error adding vendor: " + ex.Message });
            }
        }

        // GET: Vendors
        public ActionResult Index()
        {
            return View();
        }
    }
}