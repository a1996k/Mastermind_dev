using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using FifthPress.Models.ViewModels;
using System.Data.Entity;
using FifthPress.Models;
using FifthPress.Services;

namespace FifthPress.Controllers
{
    public class CustomersController : DataController
    {
        private readonly decimal DEFAULT_MARKUP = .4m;

        public async Task<ActionResult> GetClient2(int id)
        {
            try
            {
                var client = await db.Clients.FindAsync(id);
                return Json(client, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public async Task<ActionResult> SaveClient2(Client client)
        {
            try
            {
                bool wasPreviouslyOnHold = false;
                if (client.Id > 0)
                {
                    // Update existing client
                    var existingClient = await db.Clients.FindAsync(client.Id);
                    if (existingClient != null)
                    {
                        existingClient.Title = client.Title;
                        existingClient.Address = client.Address;
                        existingClient.AddressAlt = client.AddressAlt;
                        existingClient.City = client.City;
                        existingClient.Zip = client.Zip;
                        existingClient.Notes = client.Notes;
                        existingClient.TaxExempt = client.TaxExempt;
                        existingClient.BlindShipping = client.BlindShipping;
                        existingClient.IsGolfClient = client.IsGolfClient;
                        wasPreviouslyOnHold = existingClient.CreditHold == true;
                        existingClient.CreditHold = client.CreditHold;
                        existingClient.PriceLevelId = client.PriceLevelId;
                        existingClient.USStateId = client.USStateId;
                        existingClient.Markup = client.Markup;
                        existingClient.ShippingAddress = client.ShippingAddress;
                        existingClient.ShippingCity = client.ShippingCity;
                        existingClient.ShippingZip = client.ShippingZip;
                        existingClient.ShippingUSState = client.ShippingUSState;
                        existingClient.BillingAddress = client.BillingAddress;
                        existingClient.BillingCity = client.BillingCity;
                        existingClient.BillingZip = client.BillingZip;
                        existingClient.BillingUSState = client.BillingUSState;
                        existingClient.AREmail = client.AREmail;
                        existingClient.TaxAddress    = client.TaxAddress;
                        existingClient.TaxAddressAlt = client.TaxAddressAlt;
                        existingClient.TaxCity       = client.TaxCity;
                        existingClient.TaxZip        = client.TaxZip;
                        existingClient.TaxUSStateId  = client.TaxUSStateId;

                        existingClient.ThirdPartyShippingAccount = client.ThirdPartyShippingAccount;
                        existingClient.CreditAppFilePath = client.CreditAppFilePath;
                        existingClient.CreditRefCheckRequested = client.CreditRefCheckRequested;
                        existingClient.CreditReferences = client.CreditReferences;
                        existingClient.W9FilePath = client.W9FilePath;
                        existingClient.TaxCertFilePath = client.TaxCertFilePath;

                        db.Entry(existingClient).State = EntityState.Modified;
                    }
                }
                else
                {
                    // Add new client
                    db.Clients.Add(client);
                }

                await db.SaveChangesAsync();

                if (client.Id > 0 && client.CreditHold == true && !wasPreviouslyOnHold)
                {
                    try
                    {
                        var creditHoldEmail = new CreditHoldEmail(db);
                        await creditHoldEmail.SendCreditHoldNotification(client.Title);
                    }
                    catch (Exception) { /* log but don't fail save */ }
                }

                if (client.Id > 0 && client.CreditHold != true && wasPreviouslyOnHold)
                {
                    try
                    {
                        var creditHoldEmail = new CreditHoldEmail(db);
                        await creditHoldEmail.SendCreditHoldRemovedNotification(client.Title);
                    }
                    catch (Exception) { /* don't fail save */ }
                }

                return Json(new { success = true, client = client });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<ActionResult> GenerateOnboardingLink(string companyNameHint = null)
        {
            try
            {
                int userId = UserSessionHelper.GetActiveUserId(HttpContext);
                var user = await db.Users.FindAsync(userId);
                if (user == null)
                    return Json(new { success = false, message = "User not found." });

                string token = Guid.NewGuid().ToString("N");

                var onboardingToken = new ClientOnboardingToken
                {
                    Token = token,
                    CreatedByUserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddDays(7),
                    Status = "Pending",
                    CompanyNameHint = companyNameHint,
                    NotificationEmail = user.Email
                };

                db.ClientOnboardingTokens.Add(onboardingToken);
                await db.SaveChangesAsync();

                string baseUrl = System.Configuration.ConfigurationManager.AppSettings["ProofsViewBaseUrl"];
                string link = baseUrl + "/onboard?token=" + token;

                return Json(new { success = true, link, token, expiresAt = onboardingToken.ExpiresAt });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<ActionResult> ValidateTaxAddress(
            int clientId,
            string taxAddress, string taxAddressAlt,
            string taxCity, int? taxUSStateId, string taxZip)
        {
            var stateAbbr = "";
            if (taxUSStateId.HasValue)
            {
                var state = await db.USStates.FindAsync(taxUSStateId.Value);
                stateAbbr = state?.Abbreviation ?? "";
            }

            var svc    = new FifthPress.Services.AvalaraService();
            var result = await svc.ValidateAddressAsync(
                taxAddress    ?? "",
                taxAddressAlt ?? "",
                taxCity       ?? "",
                stateAbbr,
                taxZip        ?? "");

            return Json(new
            {
                success           = true,
                isValid           = result.IsValid,
                resolutionQuality = result.ResolutionQuality,
                messages          = result.Messages,
                original          = result.OriginalAddress,
                suggested         = result.SuggestedAddress
            });
        }

        public async Task<ActionResult> SaveBillingInfo(Client client)
        {
            Client existing = await db.Clients.FindAsync(client.Id);

            if (existing != null)
            {
                existing.BillingAddress = client.BillingAddress;
                existing.BillingCity = client.BillingCity;
                existing.BillingUSState = client.BillingUSState;
                existing.BillingZip = client.BillingZip;
                existing.AREmail = client.AREmail;

                db.Entry(existing).State = EntityState.Modified;

                await db.SaveChangesAsync();

                return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK);
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "client not found");
        }

        public async Task<ActionResult> GetClientBillingDetails(int? id)
        {
            Client c = await db.Clients.FindAsync(id);

            if (c != null)
            {
                var vm = new
                {
                    c.BillingAddress,
                    c.BillingCity,
                    c.BillingZip,
                    c.BillingUSState,
                    c.AREmail,
                    states = await GenerateSelectListAsync(SelectListType.USStates)
                };

                return new JsonResult
                {
                    Data = vm,
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
            else
            {
                return null;
            }
        }

        public async Task<ActionResult> DynamicContactCreation(Contact c)
        {
            db.Contacts.Add(c);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (Exception e)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, e.Message);
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK);
        }

        public async Task<ActionResult> DynamicClientCreation(Client c)
        {
            db.Clients.Add(c);
            try
            {
                await db.SaveChangesAsync();
            }
            catch (Exception e)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, e.Message);
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK);
        }

        public async Task<ActionResult> DynamicShippingAddressAddition(ShippingAddress address)
        {
            db.ShippingAddresses.Add(address);
            await db.SaveChangesAsync();
            await db.Entry(address).ReloadAsync();
            return new JsonResult
            {
                Data = "done",
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        [HttpGet]
        public async Task<ActionResult> GetShippingAddress(int id)
        {
            ShippingAddress address = await db.ShippingAddresses.FindAsync(id);
            if (address == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.NotFound, "Address not found");
            }

            return new JsonResult
            {
                Data = new
                {
                    address.Id,
                    address.Client,
                    address.CustomerName,
                    address.Address,
                    address.Address2,
                    address.City,
                    address.State,
                    address.Zip,
                    address.Phone,
                    address.Email,
                    address.Attention
                },
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        [HttpPost]
        public async Task<ActionResult> UpdateShippingAddress(ShippingAddress address)
        {
            ShippingAddress existing = await db.ShippingAddresses.FindAsync(address.Id);
            if (existing == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.NotFound, "Address not found");
            }

            existing.CustomerName = address.CustomerName;
            existing.Address = address.Address;
            existing.Address2 = address.Address2;
            existing.City = address.City;
            existing.State = address.State;
            existing.Zip = address.Zip;
            existing.Phone = address.Phone;
            existing.Email = address.Email;
            existing.Attention = address.Attention;

            await db.SaveChangesAsync();

            // Load state for formatted address
            await db.Entry(existing).Reference(r => r.USState).LoadAsync();

            return new JsonResult
            {
                Data = new
                {
                    success = true,
                    address = new
                    {
                        existing.CustomerName,
                        existing.Address,
                        City = existing.City,
                        State = existing.USState?.Abbreviation ?? "",
                        existing.Zip
                    }
                },
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> DynamicFreightAddressAddition(FreightShippingAddress address)
        {
            db.FreightShippingAddresses.Add(address);
            await db.SaveChangesAsync();
            await db.Entry(address).ReloadAsync();
            return new JsonResult
            {
                Data = "done",
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> DynamicBlindShippingAddressAddition(BlindShippingPivotTable address)
        {
            db.BlindShippingPivotTables.Add(address);
            await db.SaveChangesAsync();
            await db.Entry(address).ReloadAsync();
            return new JsonResult
            {
                Data = "done",
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> NewShippingAddress(int? clientId)
        {
            Client c = await db.Clients.FindAsync(clientId);

            if (c != null)
            {
                int newsaId = 0;
                try
                {
                    newsaId = db.ShippingAddresses.Max(x => x.Id);
                }
                catch (Exception) { }

                newsaId++;
                ShippingAddress sa = new ShippingAddress
                {
                    Id = newsaId,
                    Client = clientId.Value,
                    CustomerName = c.Title,
                    IsDefaultForClient = false
                };
                db.ShippingAddresses.Add(sa);

                await db.SaveChangesAsync();

                await db.Entry(sa).ReloadAsync();

                ViewData["State"] = await GenerateSelectListAsync(SelectListType.USStates);

                return new JsonResult
                {
                    Data = JsonConvert.SerializeObject(
                            RenderViewToString("~/Views/Partials/_ShippingAddressRow.cshtml", sa),
                            new JsonSerializerSettings
                            {
                                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                            }
                        ),
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }

            return new JsonResult
            {
                Data = JsonConvert.SerializeObject("Error creating Shipping Address"),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> NewFreightShippingAddress(int? clientId)
        {
            Client c = await db.Clients.FindAsync(clientId);

            if (c != null)
            {
                int newsaId = 0;
                try
                {
                    newsaId = db.FreightShippingAddresses.Max(x => x.Id);
                }
                catch (Exception) { }

                newsaId++;
                FreightShippingAddress sa = new FreightShippingAddress
                {
                    Id = newsaId,
                    Client = clientId.Value,
                    CustomerName = c.Title,
                    IsDefaultForClient = false
                };
                db.FreightShippingAddresses.Add(sa);

                await db.SaveChangesAsync();

                await db.Entry(sa).ReloadAsync();

                ViewData["State"] = await GenerateSelectListAsync(SelectListType.USStates);

                return new JsonResult
                {
                    Data = JsonConvert.SerializeObject(
                            RenderViewToString("~/Views/Partials/_FreightAddressRow.cshtml", sa),
                            new JsonSerializerSettings
                            {
                                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                            }
                        ),
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }

            return new JsonResult
            {
                Data = JsonConvert.SerializeObject("Error creating Shipping Address"),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> RemoveContact(int? id)
        {
            Contact c = await db.Contacts.FindAsync(id);

            if (c != null)
            {
                c.Deleted = DateTime.UtcNow;
                db.Entry(c).State = EntityState.Modified;

                await db.SaveChangesAsync();
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK);
        }

        public async Task<ActionResult> NewContact(int? clientId)
        {
            Contact c = new Contact
            {
                CusNo = clientId,
            };

            db.Contacts.Add(c);

            await db.SaveChangesAsync();

            await db.Entry(c).ReloadAsync();

            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(
                        RenderViewToString("~/Views/Partials/_ClientContactRow.cshtml", c),
                        new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> NewCustomer()
        {
            ViewData["Client.USStateId"] = await GenerateSelectListAsync(SelectListType.USStates);
            ViewData["Client.TaxUSStateId"] = await GenerateSelectListAsync(SelectListType.USStates);
            ViewData["State"] = await GenerateSelectListAsync(SelectListType.USStates);

            Client c = new Client
            {
                BlindShipping = false,
                Markup = DEFAULT_MARKUP, //default markup
                TaxExempt = false,
                IsGolfClient = false,
                CreditHold = false,
                CreditRefCheckRequested = false
            };
            db.Clients.Add(c);
            await db.SaveChangesAsync();
            await db.Entry(c).ReloadAsync();

            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(RenderViewToString("~/Views/Customers/GetCustomerForEdit.cshtml",
                    new VM_Customer
                    {
                        Client = c
                    })),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> EditCustomer(int? id)
        {
            if (id.HasValue)
            {
                Client c = await db.Clients.FindAsync(id);

                if (c == null)
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.NotFound, "Client not found");

                await db.Entry(c).Collection(co => co.Contacts).LoadAsync();
                await db.Entry(c).Collection(co => co.ShippingAddresses).LoadAsync();
                await db.Entry(c).Collection(co => co.FreightShippingAddresses).LoadAsync();

                ViewData["Client.USStateId"] = await GenerateSelectListAsync(SelectListType.USStates, c, "USStateId");
                ViewData["Client.TaxUSStateId"] = await GenerateSelectListAsync(SelectListType.USStates, c, "TaxUSStateId");
                ViewData["State"] = await GenerateSelectListAsync(SelectListType.USStates);

                return new JsonResult { Data = JsonConvert.SerializeObject(RenderViewToString("~/Views/Customers/GetCustomerForEdit.cshtml", new VM_Customer { Client = c })), JsonRequestBehavior = JsonRequestBehavior.AllowGet };
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Client ID not provided");
        }

        public async Task<ActionResult> SaveCustomer(Client theItem)
        {
            if (theItem == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            var c = await db.Clients.FindAsync(theItem.Id);

            if (c == null)
            {
                int nextClientNo = 0;
                int nextContactNo = 0;
                int nextShippingAddressNo = 0;

                try { nextClientNo = db.Clients.Max(x => x.Id); } catch (Exception) { }
                try { nextContactNo = db.Contacts.Max(x => x.Id); } catch (Exception) { }
                try { nextShippingAddressNo = db.ShippingAddresses.Max(x => x.Id); } catch (Exception) { }

                nextClientNo++;
                nextContactNo++;
                nextShippingAddressNo++;

                db.Clients.Add(theItem);

                foreach (Contact contact in theItem.Contacts.ToList())
                {
                    contact.Id = nextContactNo++;
                    db.Contacts.Add(contact);
                }

                foreach (ShippingAddress shippingAddress in theItem.ShippingAddresses.ToList())
                {
                    shippingAddress.Id = nextShippingAddressNo++;
                    db.ShippingAddresses.Add(shippingAddress);
                }
            }
            else
            {
                bool wasPreviouslyOnHold = c.CreditHold == true;
                c = UpdateCustomerRecord(c, theItem);

                foreach (Contact contact in c.Contacts.ToList())
                {
                    db.Entry(contact).State = System.Data.Entity.EntityState.Modified;
                }
                foreach (ShippingAddress sa in c.ShippingAddresses.ToList())
                {
                    db.Entry(sa).State = System.Data.Entity.EntityState.Modified;
                }

                db.Entry(c).State = System.Data.Entity.EntityState.Modified;

                try
                {
                    await db.SaveChangesAsync();
                }
                catch (Exception e)
                {
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, e.Message);
                }

                if (c.CreditHold == true && !wasPreviouslyOnHold)
                {
                    try
                    {
                        var creditHoldEmail = new CreditHoldEmail(db);
                        await creditHoldEmail.SendCreditHoldNotification(c.Title);
                    }
                    catch (Exception) { /* don't fail save */ }
                }

                if (c.CreditHold != true && wasPreviouslyOnHold)
                {
                    try
                    {
                        var creditHoldEmail = new CreditHoldEmail(db);
                        await creditHoldEmail.SendCreditHoldRemovedNotification(c.Title);
                    }
                    catch (Exception) { /* don't fail save */ }
                }

                return new JsonResult { Data = "done", JsonRequestBehavior = JsonRequestBehavior.AllowGet };
            }

            try
            {
                await db.SaveChangesAsync();
            }
            catch (Exception e)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, e.Message);
            }

            return new JsonResult { Data = "done", JsonRequestBehavior = JsonRequestBehavior.AllowGet };
        }

        public async Task<ActionResult> GetFreightAddresses(int? clientId, int? recordId, string recordtype)
        {
            if (!clientId.HasValue || !recordId.HasValue || String.IsNullOrEmpty(recordtype))
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Bad request");

            List<FreightShippingAddress> addresses = await db.FreightShippingAddresses.Where(x => x.Deleted == null && x.Client == clientId.Value).Include(i => i.USState).ToListAsync();
            int? selectedId = null;

            //null protection for bools
            foreach (FreightShippingAddress sa in addresses)
                sa.IsDefaultForClient = (sa.IsDefaultForClient ?? false);

            switch (recordtype.ToUpper())
            {
                case "JOB":
                    Job j = await db.Jobs.FindAsync(recordId);
                    selectedId = j.FreightAddress;
                    break;

                case "QUOTE":
                    Quote q = await db.Quotes.FindAsync(recordId);
                    selectedId = q.FreightAddress;
                    break;

                default:
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "In switch default");
            }

            VM_FreightShippingAddressCollection vm = new VM_FreightShippingAddressCollection
            {
                FreightShippingAddresses = addresses,
                SelectedAddressId = selectedId
            };

            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(
                        RenderViewToString("~/Views/Partials/_FreightAddressRowCollection.cshtml", vm),
                        new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> GetShippingAddresses(int? clientId, int? recordId, string recordtype)
        {
            if (!clientId.HasValue || !recordId.HasValue || String.IsNullOrEmpty(recordtype))
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Bad request");

            List<ShippingAddress> addresses = await db.ShippingAddresses.Where(x => x.Deleted == null && x.Client == clientId.Value).Include(i => i.USState).OrderBy(x => x.CustomerName).ToListAsync();
            int? selectedId = null;

            //null protection for bools
            foreach (ShippingAddress sa in addresses)
                sa.IsDefaultForClient = (sa.IsDefaultForClient ?? false);

            switch (recordtype.ToUpper())
            {
                case "JOB":
                    Job j = await db.Jobs.FindAsync(recordId);
                    selectedId = j.ShippingAddress;
                    break;

                case "QUOTE":
                    Quote q = await db.Quotes.FindAsync(recordId);
                    selectedId = q.ShippingAddress;
                    break;

                default:
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "In switch default");
            }

            VM_ShippingAddressCollection vm = new VM_ShippingAddressCollection
            {
                ShippingAddresses = addresses,
                SelectedAddressId = selectedId
            };

            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(
                        RenderViewToString("~/Views/Partials/_ShippingAddressRowCollection.cshtml", vm),
                        new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> GetBlindShippingAddresses(int? clientId, int? recordId, string recordtype)
        {
            if (!clientId.HasValue || !recordId.HasValue || String.IsNullOrEmpty(recordtype))
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Bad request");

            List<BlindShippingPivotTable> addresses = await db.BlindShippingPivotTables.Where(x => x.ClientId == clientId.Value).ToListAsync();
            int? selectedId = null;

            switch (recordtype.ToUpper())
            {
                case "JOB":
                    Job j = await db.Jobs.FindAsync(recordId);
                    selectedId = j.BlindShippingAddress;
                    break;

                default:
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "In switch default");
            }

            VM_BlindShippingAddressCollection vm = new VM_BlindShippingAddressCollection
            {
                BlindShippingAddresses = addresses,
                SelectedAddressId = selectedId
            };

            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(
                        RenderViewToString("~/Views/Partials/_BlindShippingAddressRowCollection.cshtml", vm),
                        new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> DeleteShippingAddress(int? recordId)
        {
            ShippingAddress sa = await db.ShippingAddresses.FindAsync(recordId);
            if (sa != null)
            {
                sa.Deleted = DateTime.UtcNow;
                db.Entry(sa).State = EntityState.Modified;
                //db.ShippingAddresses.Remove(sa);
                await db.SaveChangesAsync();

                return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK, "Deleted");
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK, "Nothing Deleted");
        }

        public async Task<ActionResult> DeleteFreightAddress(int? recordId)
        {
            FreightShippingAddress sa = await db.FreightShippingAddresses.FindAsync(recordId);
            if (sa != null)
            {
                sa.Deleted = DateTime.UtcNow;
                db.Entry(sa).State = EntityState.Modified;

                await db.SaveChangesAsync();

                return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK, "Deleted");
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK, "Nothing Deleted");
        }

        /// <summary>
        /// Validate a shipping address using Avalara address validation API
        /// </summary>
        [HttpPost]
        public async Task<ActionResult> ValidateShippingAddress(
            string address,
            string address2,
            string city,
            int? stateId,
            string zip)
        {
            try
            {
                // Get state abbreviation from stateId
                string stateCode = null;
                if (stateId.HasValue)
                {
                    var state = await db.USStates.FindAsync(stateId.Value);
                    stateCode = state?.Abbreviation;
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(address) ||
                    string.IsNullOrWhiteSpace(city) ||
                    string.IsNullOrWhiteSpace(stateCode))
                {
                    return Json(new
                    {
                        success = false,
                        isValid = false,
                        message = "Address, City, and State are required for validation"
                    });
                }

                // Call Avalara service
                var avalaraService = new AvalaraService();
                var result = await avalaraService.ValidateAddressAsync(
                    address,
                    address2,
                    city,
                    stateCode,
                    zip
                );

                return Json(new
                {
                    success = true,
                    isValid = result.IsValid,
                    resolutionQuality = result.ResolutionQuality,
                    messages = result.Messages,
                    original = result.OriginalAddress,
                    suggested = result.SuggestedAddress
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    isValid = false,
                    message = $"Address validation failed: {ex.Message}"
                });
            }
        }

        public async Task<ActionResult> GetContact(int id)
        {
            try
            {
                // Replace with your actual data access logic
                var contact = await db.Contacts.FindAsync(id);
                return Json(contact, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public async Task<ActionResult> SaveContact(Contact contact)
        {
            try
            {
                if (contact.Id > 0)
                {
                    // Update existing contact
                    var existingContact = await db.Contacts.FindAsync(contact.Id);
                    if (existingContact != null)
                    {
                        existingContact.First = contact.First;
                        existingContact.Last = contact.Last;
                        existingContact.Phone = contact.Phone;
                        existingContact.Phone2 = contact.Phone2;
                        existingContact.Cell = contact.Cell;
                        existingContact.Fax = contact.Fax;
                        existingContact.Email = contact.Email;
                        existingContact.Position = contact.Position;
                        existingContact.ContactNote = contact.ContactNote;
                        existingContact.ConTag = contact.ConTag;
                        existingContact.SendEmail = contact.SendEmail;
                        existingContact.SendSurvey = contact.SendSurvey;
                        existingContact.KeyContact = contact.KeyContact;
                        existingContact.ShipTo = contact.ShipTo;

                        db.Entry(existingContact).State = EntityState.Modified;
                    }
                }
                else
                {
                    // Add new contact
                    db.Contacts.Add(contact);
                }

                await db.SaveChangesAsync();

                return Json(new { success = true, contact = contact });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        public ActionResult DownloadOnboardingFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
                return HttpNotFound("File not found.");

            // Ensure the file is within the allowed storage directory
            string allowedRoot = @"C:\Storage\client_onboarding";
            string fullPath = System.IO.Path.GetFullPath(path);
            if (!fullPath.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase))
                return HttpNotFound("Access denied.");

            string fileName = System.IO.Path.GetFileName(fullPath);
            string contentType = MimeMapping.GetMimeMapping(fileName);
            return File(fullPath, contentType, fileName);
        }
    }
}