using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using FifthPress.ActionFilters;
using FifthPress.Models;
using FifthPress.Models.ViewModels;
using OfficeOpenXml;

namespace FifthPress.Controllers
{
    [CookieFilter(Order = 1)]
    [UserPermissions(Order = 2)]
    public class DevController : DataController
    {
        public async Task<ActionResult> LogGeo(ImageWithGeo output)
        {
            int user = UserSessionHelper.GetActiveUserId(HttpContext);
            User u = await db.Users.FindAsync(user);

            if (u != null)
            {
                MastermindLogger.Log(LogLevel.Info,
                    String.Format("{0}: Lat: {1} | Lon: {2} (Image name = {3})", (u.FirstName + " " + u.LastName), output.latitude, output.longitude, output.image.FileName));
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "User Not found");
        }

        public ActionResult GeoTest()
        {
            return View();
        }
    }
}