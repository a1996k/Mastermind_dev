using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.Controllers
{
    public class ErrorController : Controller
    {
        // GET: Error
        public ActionResult Index()
        {
            return View("~/Views/Shared/Error500.cshtml");
        }

        // GET: Error/Details?refId=ERR-XXXXXXXX-XXXXXX-XXXX
        public ActionResult Details(string refId)
        {
            ViewBag.ErrorReferenceId = refId ?? "Unknown";
            ViewBag.Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            return View("~/Views/Shared/Error500.cshtml");
        }

        // GET: Error/NotFound
        public ActionResult NotFound()
        {
            Response.StatusCode = 404;
            ViewBag.ErrorReferenceId = null;
            ViewBag.ErrorType = "Page Not Found";
            ViewBag.ErrorMessage = "The page you requested could not be found.";
            return View("~/Views/Shared/Error.cshtml");
        }

        // GET: Error/Forbidden
        public ActionResult Forbidden(string message = null)
        {
            Response.StatusCode = 403;
            ViewData["SpecificErrorMessage"] = message;
            return View("~/Views/Shared/Error403.cshtml");
        }
    }
}