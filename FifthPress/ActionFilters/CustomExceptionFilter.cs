using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.ActionFilters
{
    public class CustomExceptionFilter : FilterAttribute, IExceptionFilter
    {
        public void OnException(ExceptionContext filterContext)
        {
            if (!filterContext.ExceptionHandled)
            {
                // Log the exception
                MastermindLogger.Log(LogLevel.Error, filterContext.Exception.Message);

                // Redirect to ErrorController's Index action
                filterContext.Result = new RedirectToRouteResult(
                    new System.Web.Routing.RouteValueDictionary
                    {
                    { "controller", "Error" },
                    { "action", "Index" }
                    });
                filterContext.ExceptionHandled = true;
            }
        }
    }
}