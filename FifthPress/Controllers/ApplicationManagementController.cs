using FifthPress.ActionFilters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.Controllers
{
    [CookieFilter(Order = 1)]
    [UserPermissions(Order = 2)]
    public class ApplicationManagementController : DataController
    {
        public ActionResult NewBugReport()
        {
            //render html and return in json.html
            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(
                        new
                        {
                            html = RenderViewToString("~/Views/ApplicationManagement/_Bugs.cshtml", null)
                        }
                    ),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> SubmitBug(Bug bug)
        {
            int userId = UserSessionHelper.GetActiveUserId(HttpContext);

            DateTime dateStamp = DateTime.UtcNow;

            bug.Reporter = userId;
            bug.ReportDate = dateStamp;
            bug.LastUpdatedDate = dateStamp;

            db.Bugs.Add(bug);

            await db.SaveChangesAsync();
            await db.Entry(bug).ReloadAsync();
            return new JsonResult
            {
                Data = bug.Id,
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> GetBugs()
        {
            var bugs = await db.Bugs.Include(i => i.PermissionsManifest).Include(i => i.HostingEnvironment).ToListAsync();
            return View("~/Views/ApplicationManagement/Bugs.cshtml", bugs);
        }

        public ActionResult NewFeatureRequest()
        {
            //render html and return in json.html
            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(
                        new
                        {
                            html = RenderViewToString("~/Views/ApplicationManagement/_FeatureRequest.cshtml", null)
                        }
                    ),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> SubmitFeatureRequest(FeatureRequest featureRequest)
        {
            int userId = UserSessionHelper.GetActiveUserId(HttpContext);
            DateTime dateStamp = DateTime.UtcNow;

            featureRequest.RequestDate = dateStamp;
            featureRequest.Requester = userId;
            featureRequest.LastUpdatedDate = dateStamp;

            db.FeatureRequests.Add(featureRequest);

            await db.SaveChangesAsync();
            await db.Entry(featureRequest).ReloadAsync();

            return new JsonResult
            {
                Data = featureRequest.Id,
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> GetFeatureRequests()
        {
            var requests = await db.FeatureRequests.Include(i => i.PermissionsManifest).ToListAsync();
            return View("~/Views/ApplicationManagement/FeatureRequests.cshtml", requests);
        }

        public async Task<ActionResult> ListBugs()
        {
            var bugs = await db.Bugs.ToListAsync();
            foreach (var b in bugs)
            {
                if (b.FixDate.HasValue)
                    b.FixDate = b.FixDate.Value.ToLocalTime();
                if (b.LastUpdatedDate.HasValue)
                    b.LastUpdatedDate = b.LastUpdatedDate.Value.ToLocalTime();
                if (b.ReportDate.HasValue)
                    b.ReportDate = b.ReportDate.Value.ToLocalTime();

                await db.Entry(b).Reference(r => r.User).LoadAsync();
            }

            return View("~/Views/ApplicationManagement/ListBugs.cshtml", bugs);
        }
    }
}