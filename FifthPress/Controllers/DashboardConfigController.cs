using FifthPress.Models.ViewModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Hierarchy;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.Controllers
{
    //put a special cookie filter here with increased security for this controller
    public class DashboardConfigController : DataController
    {
        public async Task<ActionResult> AddCardPermission(int? userId, int? cardId)
        {
            //exist check
            User u = await db.Users.FindAsync(userId);
            DashboardCard card = await db.DashboardCards.FindAsync(cardId);

            if (u == null || card == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError);

            db.CardPermissions.Add(new CardPermission
            {
                RequestedUser = userId.Value,
                RequestingUser = userId.Value,
                Card = cardId.Value
            });

            await db.SaveChangesAsync();

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK);
        }

        public async Task<ActionResult> RemoveCardPermission(int? userId, int? cardId)
        {
            User u = await db.Users.FindAsync(userId);
            DashboardCard card = await db.DashboardCards.FindAsync(cardId);

            if (u == null || card == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError);

            await db.Entry(u).Collection(c => c.CardPermissions1).LoadAsync();

            foreach (CardPermission cp in u.CardPermissions1.Where(x => x.Card == cardId).ToList())
                db.CardPermissions.Remove(cp);

            await db.SaveChangesAsync();

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK);
        }

        public async Task<ActionResult> GetUserChartPermissions(int? userId, int? chartId)
        {
            //ensure we can find the specified user and the card (exist check)
            User u = await db.Users.FindAsync(userId);
            DashboardCard card = await db.DashboardCards.FindAsync(chartId);

            if (u == null || card == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError);

            await db.Entry(u).Collection(c => c.CardPermissions1).LoadAsync();

            var permissionsForCard = u.CardPermissions1.Where(x => x.Card == card.Id && x.RequestedUser != u.Id);

            foreach (var permission in permissionsForCard)
            {
                //user = RequestedUser, User1 = RequestingUser
                await db.Entry(permission).Reference(r => r.User).LoadAsync();
            }

            //get the full list of available users for this card, filtering out the already assigned users
            var usersOnCard = permissionsForCard.Select(x => x.RequestedUser).ToList();

            var unassignedUsers =
                await db.Users
                    .Where(x => !(x.Disabled ?? true) && x.Deleted == null && !usersOnCard.Contains(x.Id) && x.Id != u.Id)
                    .OrderBy(x => x.FirstName)
                    .Select(x => new VMMUserChartPermissions
                    {
                        UserId = x.Id,
                        FirstName = x.FirstName,
                        LastName = x.LastName
                    })
                    .ToListAsync();

            VM_UserChartPermissions vm = new VM_UserChartPermissions
            {
                AssignedUsers = permissionsForCard.Select(x => new VMMUserChartPermissions { UserId = x.RequestedUser, FirstName = x.User.FirstName, LastName = x.User.LastName }).ToList(),
                UnassignedUsers = unassignedUsers,
                CardTitle = card.CardTitle,
                RequestingUserFullName = u.FirstName + " " + u.LastName
            };

            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(vm, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> GetUserCardAssignments(int? userId)
        {
            //configure dashboard data permissions for a particular user
            User u = await db.Users.FindAsync(userId);

            List<DashboardCard> allCards = await db.DashboardCards.ToListAsync();

            if (u != null)
            {
                await db.Entry(u).Collection(c => c.CardPermissions1).LoadAsync();

                foreach (CardPermission cp in u.CardPermissions1)
                {
                    await db.Entry(cp).Reference(r => r.DashboardCard).LoadAsync();
                }

                //will throw an error if card permissions exist for a non-existent card
                List<int> activeCardIds = u.CardPermissions1.Select(x => x.DashboardCard.Id).Distinct().ToList();

                List<Tuple<int, string>> activeCards = allCards
                            .Where(x => activeCardIds.Contains(x.Id))
                            .Select(x => new Tuple<int, string>(x.Id, x.CardTitle)).ToList();

                List<Tuple<int, string>> inactiveCards =
                    allCards
                        .Where(x => !activeCardIds.Contains(x.Id))
                        .Select(x => new Tuple<int, string>(x.Id, x.CardTitle))
                        .ToList();

                VM_DashboardConfigurator vm = new VM_DashboardConfigurator
                {
                    User = u,
                    ActiveCards = activeCards,
                    InactiveCards = inactiveCards
                };

                return new JsonResult
                {
                    Data = JsonConvert.SerializeObject(vm, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "User not found");
        }

        public async Task<ActionResult> AddUserToCardPermission(int? requestingUserId, int? requestedUserId, int? cardId)
        {
            //requestingUserId = owner of the dashboard/card
            //requestedUserId = user whose data should appear on the card
            //cardId = the dashboard card

            if (!requestingUserId.HasValue || !requestedUserId.HasValue || !cardId.HasValue)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, "Missing required parameters");

            //exist check
            User requestingUser = await db.Users.FindAsync(requestingUserId.Value);
            User requestedUser = await db.Users.FindAsync(requestedUserId.Value);
            DashboardCard card = await db.DashboardCards.FindAsync(cardId.Value);

            if (requestingUser == null || requestedUser == null || card == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.NotFound, "User or card not found");

            //check if permission already exists
            bool exists = await db.CardPermissions.AnyAsync(x =>
                x.RequestingUser == requestingUserId.Value &&
                x.RequestedUser == requestedUserId.Value &&
                x.Card == cardId.Value);

            if (exists)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK, "Permission already exists");

            db.CardPermissions.Add(new CardPermission
            {
                RequestingUser = requestingUserId.Value,
                RequestedUser = requestedUserId.Value,
                Card = cardId.Value
            });

            await db.SaveChangesAsync();

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK);
        }

        public async Task<ActionResult> RemoveUserFromCardPermission(int? requestingUserId, int? requestedUserId, int? cardId)
        {
            //requestingUserId = owner of the dashboard/card
            //requestedUserId = user whose data should be removed from the card
            //cardId = the dashboard card

            if (!requestingUserId.HasValue || !requestedUserId.HasValue || !cardId.HasValue)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, "Missing required parameters");

            //find and remove the permission
            CardPermission permission = await db.CardPermissions.FirstOrDefaultAsync(x =>
                x.RequestingUser == requestingUserId.Value &&
                x.RequestedUser == requestedUserId.Value &&
                x.Card == cardId.Value);

            if (permission == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.NotFound, "Permission not found");

            db.CardPermissions.Remove(permission);
            await db.SaveChangesAsync();

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK);
        }

        public async Task<ActionResult> Index()
        {
            List<SelectListItem> usersList = new List<SelectListItem>
            {
                new SelectListItem
                {
                    Text = "",
                    Value = ""
                }
            };

            usersList.AddRange(await GenerateSelectListAsync(Models.SelectListType.Users));
            //get a drop down of all users
            ViewData["Users"] = usersList;
            //return view
            return View();
        }
    }
}