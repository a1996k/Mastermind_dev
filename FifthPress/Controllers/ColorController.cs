using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;
using static FifthPress.Controllers.DataController;

namespace FifthPress.Controllers
{
    public class ColorController : DataController
    {
        //private readonly CostCalculator _ccalculator;

        /*public ColorController(CostCalculator calculator)
        : base(calculator) // Call the base class constructor
        {
            _ccalculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
        }*/
        /*public async Task<ActionResult> NewColorProfile()
        {
            return null;
        }

        public async Task<ActionResult> SaveColorProfile()
        {
            return null;
        }*/

        /*public async Task<ActionResult> GetColorProfileForEdit(int? id)
        {
            if (id.HasValue)
            {
                //using (ConnectToDatabase())
                {
                    ColorProfile cp = await db.ColorProfiles.SingleOrDefaultAsync(x => x.Id == id.Value);

                    if (cp != null)
                        return new JsonResult { Data = JsonConvert.SerializeObject(RenderViewToString("~/Views/Color/GetColorProfileForEdit.cshtml", cp)), JsonRequestBehavior = JsonRequestBehavior.AllowGet };
                }
            }

            return null;
        }*/
    }
}