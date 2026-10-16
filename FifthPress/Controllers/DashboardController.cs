using FifthPress.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Net;
using FifthPress.ActionFilters;
using Microsoft.Ajax.Utilities;

namespace FifthPress.Controllers
{
    [CookieFilter(Order = 1)]
    [UserPermissions(Order = 2)]
    public class DashboardController : DataController
    {
        // GET: Dashboard
        public async Task<ActionResult> Index()
        {
            HttpCookie activeUserCookie = HttpContext.Request.Cookies.Get("ActiveUser");
            int userId = int.Parse(activeUserCookie.Value);

            User u = await db.Users.FindAsync(userId);

            await db.Entry(u).Collection(c => c.DashboardTabPermissions).LoadAsync();

            List<int> allowedTabs = u.DashboardTabPermissions.Select(x => x.Tab).ToList();

            if (u == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "User not found");
            }

            //get the users that this data set has access to
            //List<int> AllowedUsers = await db.CardPermissions.Where(x => x.RequestingUser == u.Id).Select(x => x.RequestedUser).Distinct().ToListAsync();

            VM_Dashboard vm = new VM_Dashboard
            {
                Tabs = await db.DashboardTabs.Where(x => allowedTabs.Contains(x.Id)).ToListAsync()
            };
            //dashboard tab slugs
            foreach (DashboardTab dt in vm.Tabs)
                dt.TabSlug = dt.Title.ToLower().Replace(" ", "-");

            return View(vm);
        }

        public async Task<ActionResult> GetTabCards(int? tabId)
        {
            //make sure we have a tab id
            if (!tabId.HasValue)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            int? userId;

            //get user
            userId = UserSessionHelper.GetActiveUserId(HttpContext);

            //check that user exists in db
            User u = await db.Users.FindAsync(userId);

            if (u == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            await db.Entry(u).Collection(c => c.CardPermissions1).LoadAsync();
            List<int> assignedCards = u.CardPermissions1.Select(x => x.Card).Distinct().ToList();

            //get cards
            List<DashboardCard> cards = await db.DashboardCards.Where(x => x.DashboardTab == tabId && (x.Active ?? false) && assignedCards.Contains(x.Id)).ToListAsync();
            List<int> cardIds = cards.Select(x => x.Id).ToList();

            foreach (DashboardCard dc in cards)
                dc.slug = dc.CardTitle.Replace(" ", "-").ToLower();

            List<int> AllowedUsers = await db.CardPermissions.Where(x => cardIds.Contains(x.Card) && x.RequestingUser == u.Id).Select(x => x.RequestedUser).Distinct().ToListAsync();

            VM_CardDisplay vm = new VM_CardDisplay
            {
                Cards = cards,
                DepartmentSelect = new VM_MultiselectModel2
                {
                    SelectList = await GenerateSelectListAsync(Models.SelectListType.EmployeeDepartments),
                    SelectName = "Departments"
                },
                UserSelect =
                    new VM_MultiselectModel2
                    {
                        SelectList = await db.Users.Where(x => (x.AgencyUtilizationApplicable ?? false) && AllowedUsers.Contains(x.Id)).Select(x => new SelectListItem
                        {
                            Text = x.FirstName + " " + x.LastName,
                            Value = x.Id.ToString(),
                            Selected = true
                        }).OrderBy(x => x.Text).ToListAsync(),
                        SelectName = "Users"
                    }
            };

            return PartialView("~/Views/Dashboard/_CardDisplay.cshtml", vm);
        }
    }
}