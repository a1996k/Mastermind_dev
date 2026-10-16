using FifthPress.ActionFilters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.Controllers
{
    //main JobController was getting pretty crowded, so I'm starting a new controller
    [CookieFilter(Order = 1)]
    [UserPermissions(Order = 2)]
    public class Job2Controller : DataController
    {
        public async Task<ActionResult> CancelJob(int? id)
        {
            Job j = await db.Jobs.FindAsync(id);

            if (j != null)
            {
                int userId = UserSessionHelper.GetActiveUserId(HttpContext);
                j.Cancelled = DateTime.UtcNow;
                j.CancelledBy = userId;

                db.Entry(j).State = System.Data.Entity.EntityState.Modified;

                try
                {
                    await db.SaveChangesAsync();
                }
                catch (Exception e)
                {
                    MastermindLogger.Log(LogLevel.Error, "Error cancelling job #" + j.JobNumber + " by user id = " + userId.ToString() + " :: Job2Controller.CancelJob(id)");
                    MastermindLogger.LogException(e);
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError);
                }

                return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK);
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Unable to find that job");
        }
    }
}