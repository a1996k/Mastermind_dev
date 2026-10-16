using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.Controllers
{
    public class PricingController : DataController
    {
        public async Task<ActionResult> RemovePrice(int priceId, string recordType)
        {
            if (recordType == "job")
            {
                var jobPrice = await db.JobPrices.FindAsync(priceId);
                if (jobPrice != null)
                {
                    db.JobPrices.Remove(jobPrice);
                    await db.SaveChangesAsync();
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK);
                }
            }
            else if (recordType == "quote")
            {
                var quotePrice = await db.QuotePrices.FindAsync(priceId);
                if (quotePrice != null)
                {
                    db.QuotePrices.Remove(quotePrice);
                    await db.SaveChangesAsync();
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK);
                }
            }
            return new HttpStatusCodeResult(System.Net.HttpStatusCode.NotFound, "Record not found");
        }
    }
}