using FifthPress.ActionFilters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.Controllers
{
    [CookieFilter(Order = 1)]
    public class SplashController : DataController
    {
        // GET: Splash
        public ActionResult Index()
        {
            //get the ActiveUser cookie
            //remove the tv cookie if it exists
            //this is important so that normal users are redirected to root on error and tv's remaining redirected to /tv on error
            // if the TV‐client cookie exists, expire it immediately
            if (Request.Cookies["IsTvClient"] != null)
            {
                var tvCookie = new HttpCookie("IsTvClient")
                {
                    Expires = DateTime.UtcNow.AddDays(-1),
                    Path = "/"
                };
                Response.Cookies.Add(tvCookie);
            }

            return View();
        }
    }
}