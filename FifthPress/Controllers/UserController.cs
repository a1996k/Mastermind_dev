using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Threading.Tasks;
using System.Net;
using Newtonsoft.Json;
using System.Data.Entity;
using FifthPress.Models;
using System.Security;
using FifthPress.ActionFilters;
using OfficeOpenXml.Style;
using System.Activities.Statements;

namespace FifthPress.Controllers
{
    [CookieFilter(Order = 1)]
    public class UserController : UserAccountController
    {
        private readonly int ActiveUserCookieTimeoutMins = 240;

        private string GetUserName(string userCode, bool lastFirst = false)
        {
            try
            {
                User u = db.Users.Single(x => x.Login == userCode);
                //return the name
                if (lastFirst)
                    return String.Format("{0}, {1}", u.LastName, u.FirstName);
                else
                    return String.Format("{0} {1}", u.FirstName, u.LastName);
            }
            catch (Exception)
            {
                //return an empty string because we couldn't find an user
                return "";
            }
        }

        private string GenerateWelcomeMessage(User user)
        {
            int curHour = DateTime.Now.ToLocalTime().Hour;
            string timeOfDay = "Morning";
            if (curHour >= 11 && curHour < 17)
                timeOfDay = "Afternoon";
            if (curHour >= 17)
                timeOfDay = "Evening";

            return String.Format("Good {0}, {1}", timeOfDay, user.FirstName);
        }

        public async Task<ActionResult> QAPasswordCheck(string pw, int? selectedUser)
        {
            if (selectedUser == -1)
            {
                return new JsonResult
                {
                    Data = JsonConvert.SerializeObject(new { accepted = true }, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
            else
            {
                User u = await db.Users.FindAsync(selectedUser);

                if (u != null)
                {
                    bool pwValid = VerifyPassword(pw, u.Hash, u.Salt);

                    return new JsonResult
                    {
                        Data = JsonConvert.SerializeObject(new { accepted = pwValid }, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };
                }

                return new JsonResult
                {
                    Data = JsonConvert.SerializeObject(new { accepted = false }, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
        }

        public async Task<ActionResult> SetPassword(string newPassword)
        {
            //password requirements could go here (length, regex check, etc.)
            HttpCookie activeUserCookie = HttpContext.Request.Cookies.Get("ActiveUser");
            try
            {
                int userId = int.Parse(activeUserCookie.Value);
                User u = await db.Users.FindAsync(userId);

                if (u != null)
                {
                    string newSalt = "";
                    try
                    {
                        string pwHash = HashPassword(newPassword, out newSalt);

                        u.Hash = pwHash;
                        u.Salt = newSalt;

                        db.Entry(u).State = EntityState.Modified;

                        //reauthenticate?

                        await db.SaveChangesAsync();

                        return new HttpStatusCodeResult(HttpStatusCode.OK);
                    }
                    catch (Exception)
                    {
                        return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Critical hashing error");
                    }
                }

                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "User not found");
            }
            catch (Exception)
            {
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Error parsing user id from cookie");
            }
        }

        public async Task<ActionResult> Login(string userName, string passcode)
        {
            //no op code provided
            if (String.IsNullOrEmpty(userName))
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "No user name was provided");

            try
            {
                User op = await db.Users.SingleAsync(x => x.Username.ToUpper() == userName.ToUpper() && !(x.Disabled ?? true));
                await db.Entry(op).Reference(r => r.Role1).LoadAsync();
                op.RetrievePermissions();
                bool validCredentials = VerifyPassword(passcode, op.Hash, op.Salt);

                if (!validCredentials)
                    return new HttpStatusCodeResult(HttpStatusCode.Unauthorized);

                //db.OperatorLogon(op.Id);
                HttpCookie activeUserCookie = new HttpCookie("ActiveUser", op.Id.ToString());
                HttpCookie userRoleCookie = new HttpCookie("UserRole", op.Role1.Title);
                HttpCookie permissionsCookie = new HttpCookie("Permissions", string.Join("", op.PermissionsModel.SelectMany(x => x.PermissionIdentifier)));
                activeUserCookie.Domain = HttpContext.Request.Url.Host.Contains("localhost") ? HttpContext.Request.Url.Host : "";
                userRoleCookie.Domain = HttpContext.Request.Url.Host.Contains("localhost") ? HttpContext.Request.Url.Host : "";
                permissionsCookie.Domain = HttpContext.Request.Url.Host.Contains("localhost") ? HttpContext.Request.Url.Host : "";
                activeUserCookie.Expires = DateTime.Now.ToLocalTime().AddMinutes(ActiveUserCookieTimeoutMins);
                userRoleCookie.Expires = DateTime.Now.ToLocalTime().AddMinutes(ActiveUserCookieTimeoutMins);
                permissionsCookie.Expires = DateTime.Now.ToLocalTime().AddMinutes(ActiveUserCookieTimeoutMins);
                activeUserCookie.Secure = false;
                userRoleCookie.Secure = false;
                permissionsCookie.Secure = false;
                HttpContext.Response.SetCookie(activeUserCookie);
                HttpContext.Response.SetCookie(userRoleCookie);
                HttpContext.Response.SetCookie(permissionsCookie);

                //get the welcome message (future implementation). If there is none, default to "Good {TimeOfDay}, {Name}!"
                op.WelcomeMessage = GenerateWelcomeMessage(op);

                return new JsonResult { Data = JsonConvert.SerializeObject(op, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }), JsonRequestBehavior = JsonRequestBehavior.AllowGet };
            }
            catch (Exception)
            {
            }

            return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Unknown Error");
        }

        //check if a user is logged in. also set their active login time to the current time
        public async Task<bool> IsUserValid(int? userId)
        {
            if (!userId.HasValue)
                return false;
            else
            {
                try
                {
                    ActiveUser au = await db.ActiveUsers.SingleAsync(x => x.UserId == userId.Value);
                    DateTime loginTime = au.UtcTimestamp.Value.ToLocalTime();
                    DateTime currentTime = DateTime.Now.ToLocalTime();

                    if (loginTime.AddMinutes(ActiveUserCookieTimeoutMins) > currentTime)
                    {
                        //valid
                        au.UtcTimestamp = DateTime.UtcNow;
                        db.Entry(au).State = EntityState.Modified;
                        await db.SaveChangesAsync();

                        return true;
                    }
                    else
                    {
                        db.ActiveUsers.Remove(au);
                        await db.SaveChangesAsync();
                        return false;
                    }
                }
                catch (Exception)
                {
                    //can't find an active user in db, so false
                    return false;
                }
            }
        }

        public async Task<ActionResult> Logout(int? userId)
        {
            //no op code provided
            if (!userId.HasValue)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "No user code was provided");

            User op = await db.Users.FindAsync(userId);

            //no op found
            if (op == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, String.Format("No user with id {0} found", userId.Value));

            //if (await IsUserLogoffAllowed(op.Id))
            //{
            db.OperatorLogoff(op.Id);
            HttpCookie activeUserCookie = HttpContext.Request.Cookies.Get("ActiveUser");
            HttpCookie userRoleCookie = HttpContext.Request.Cookies.Get("UserRole");
            HttpCookie permissionsCookie = HttpContext.Request.Cookies.Get("Permissions");

            if (activeUserCookie != null)
            {
                activeUserCookie.Expires = DateTime.Now.ToLocalTime().AddDays(-1);
                HttpContext.Response.SetCookie(activeUserCookie);
            }
            if (userRoleCookie != null)
            {
                userRoleCookie.Expires = DateTime.Now.ToLocalTime().AddDays(-1);
                HttpContext.Response.SetCookie(userRoleCookie);
            }
            if (permissionsCookie != null)
            {
                permissionsCookie.Expires = DateTime.Now.ToLocalTime().AddDays(-1);
                HttpContext.Response.SetCookie(permissionsCookie);
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK);
            //}
            //else
            //    return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK, String.Format("User {0} already logged off", userId.Value));
        }

        public async Task<ActionResult> NewUser()
        {
            ViewBag.Role = await GenerateSelectListAsync(SelectListType.Roles);

            //init flags to false to avoid null reference exception in view
            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(RenderViewToString("~/Views/User/GetUserForEdit.cshtml",
                    new User
                    {
                        IsProjectManager = false,
                        IsProductionEmployee = false,
                        IsExecutive = false,
                        AgencyUtilizationApplicable = false,
                        IsSpecialApprover = false,
                        IsEventStaff = false
                    })),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> Save(User theItem)
        {
            if (theItem == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            var u = await db.Users.FindAsync(theItem.Id);
            if (u == null)
            {
                //save new user
                db.Users.Add(theItem);
            }
            else
            {
                //save existing user
                u = UpdateUserRecord(u, theItem);
                db.Entry(u).State = System.Data.Entity.EntityState.Modified;
            }

            await db.SaveChangesAsync();

            return new JsonResult { Data = "done", JsonRequestBehavior = JsonRequestBehavior.AllowGet };
        }

        public async Task<ActionResult> EditUser(int? id)
        {
            if (id.HasValue)
            {
                User user = await db.Users.FindAsync(id);

                if (user != null)
                {
                    await db.Entry(user).Reference(x => x.Role1).LoadAsync();

                    ViewBag.Role = await GenerateSelectListAsync(SelectListType.Roles, user, "Role");

                    return new JsonResult { Data = JsonConvert.SerializeObject(RenderViewToString("~/Views/User/GetUserForEdit.cshtml", user)), JsonRequestBehavior = JsonRequestBehavior.AllowGet };
                }
            }

            return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "No id provided");
        }

        public async Task<ActionResult> GetUserStatus()
        {
            // try get the user id from the ActiveUser cookie
            HttpCookie activeUserCookie = HttpContext.Request.Cookies.Get("ActiveUser");
            int userId = Convert.ToInt32(activeUserCookie.Value);
            User u = await db.Users.FindAsync(userId);
            bool isMarcus = false;

            //hack for marcus
            if (u.Username == "marcusd" || u.Username == "dylanb")
            {
                isMarcus = true;
            }

            try
            {
                ClockActivity ca = await db.ClockActivities.SingleAsync(x => x.Employee == userId && x.TimeOut == null);
                bool breakStarted;
                string breakStartedAt;
                TimeSpan? breakElapsed = null;

                try
                {
                    EmployeeBreak currentBreak = await db.EmployeeBreaks.SingleAsync(b => b.Employee == userId && b.BreakEnd == null);
                    breakStarted = true;
                    breakStartedAt = currentBreak.BreakStart.ToLocalTime().ToString("G");
                    breakElapsed = DateTime.UtcNow.Subtract(currentBreak.BreakStart);
                }
                catch (InvalidOperationException)
                {
                    // No ongoing break or more than one found, which should not happen in a well-designed system
                    breakStarted = false;
                    breakStartedAt = null;
                }

                var res = new
                {
                    dayStarted = true,
                    startedAt = ca.TimeIn.ToLocalTime().ToString("G"),
                    userName = u.Username,
                    breakStarted = breakStarted,
                    breakStartedAt = breakStartedAt,
                    breakElapsed = breakElapsed.HasValue ? breakElapsed.Value.ToString(@"hh\:mm\:ss") : null,
                    isMarcus
                };

                return new JsonResult
                {
                    Data = JsonConvert.SerializeObject(res, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
            catch (Exception)
            {
                // no active day or other errors
                var res = new { dayStarted = false, userName = u.Username, breakStarted = false, isMarcus };
                return new JsonResult
                {
                    Data = JsonConvert.SerializeObject(res, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
        }

        public async Task<ActionResult> GetClockActivity(int? id)
        {
            if (!id.HasValue)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Missing user id");

            DateTime cutoff = DateTime.UtcNow.AddDays(-30);

            List<ClockActivitySinceDate_Result> results = await db.ClockActivitySinceDate(cutoff, id.Value).ToListAsync();

            //adjust times to local time
            foreach (ClockActivitySinceDate_Result c in results)
            {
                c.TimeIn = c.TimeIn.ToLocalTime();
                c.TimeInString = c.TimeIn.ToString("HH:mm");

                if (c.TimeOut.HasValue)
                {
                    c.TimeOut = c.TimeOut.Value.ToLocalTime();
                    c.TimeOutString = c.TimeOut.Value.ToString("HH:mm");
                }
                else
                {
                    c.TimeOutString = "";
                }
                if (c.BreakStart.HasValue)
                {
                    c.BreakStart = c.BreakStart.Value.ToLocalTime();
                    c.BreakStartString = c.BreakStart.Value.ToString("HH:mm");
                }
                else
                {
                    c.BreakStartString = "";
                }
                if (c.BreakEnd.HasValue)
                {
                    c.BreakEnd = c.BreakEnd.Value.ToLocalTime();
                    c.BreakEndString = c.BreakEnd.Value.ToString("HH:mm");
                }
                else
                {
                    c.BreakEndString = "";
                }

                c.EventDate = c.TimeIn.Date;
                c.EventDateString = c.EventDate.Value.ToString("M/d/yyyy");
            }

            results = results.OrderBy(x => x.TimeIn).ToList();

            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(results, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        /*
        public async Task<ActionResult> UpdateUserClockTime(ClockUpdate clockUpdate)
        {
            //incoming times will be local time
            ClockActivity ca = await db.ClockActivities.FindAsync(clockUpdate.ClockActivityId);
            EmployeeBreak eb = null;

            if (clockUpdate.BreakRecordId.HasValue)
                eb = await db.EmployeeBreaks.FindAsync(clockUpdate.BreakRecordId);

            ca.TimeIn = clockUpdate.ClockInTime.ToUniversalTime();

            if (clockUpdate.ClockOutTime.HasValue)
                ca.TimeOut = clockUpdate.ClockOutTime.Value.ToUniversalTime();

            db.Entry(ca).State = EntityState.Modified;

            if (eb != null)
            {
                if (clockUpdate.BreakStartTime.HasValue)
                    eb.BreakStart = clockUpdate.BreakStartTime.Value.ToUniversalTime();

                if (clockUpdate.BreakEndTime.HasValue)
                    eb.BreakEnd = clockUpdate.BreakEndTime.Value.ToUniversalTime();

                db.Entry(eb).State = EntityState.Modified;
            }

            await db.SaveChangesAsync();

            return new JsonResult
            {
                Data = "done",
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }*/
    }
}