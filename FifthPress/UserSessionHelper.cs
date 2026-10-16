using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress
{
    public class UserSessionHelper
    {
        public static int GetActiveUserId(HttpContextBase context)
        {
            var cookie = context.Request.Cookies["ActiveUser"];

            if (cookie != null && int.TryParse(cookie.Value, out int id))
                return id;
            else
                throw new Exception("Critical error in GetActiveUserId() (static)");
        }
    }
}