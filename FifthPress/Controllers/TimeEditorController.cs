using FifthPress.ActionFilters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.Controllers
{
    [CookieFilter(Order = 1)]
    [UserPermissions(Order = 2)]
    public class TimeEditorController : DataController
    {
        // GET: TimeEditor
        public ActionResult Index()
        {
            //show job list
            //provide button to change job times
            return View();
        }
    }
}