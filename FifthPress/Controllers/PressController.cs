using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.Entity;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Net;
using FifthPress.Models.ViewModels;
using FifthPress;
using FifthPress.Models.ControllerModels;
using OfficeOpenXml.Drawing;
using FifthPress.Models;

namespace FifthPress.Controllers
{
    public enum TypeOfConstraint
    {
        Range = 1,
        Minimum = 2,
        Maximum = 3,
        Exact = 4,
        NotSet = 5
    }

    public enum ConstraintValueType
    {
        Text = 1,
        Number = 2,
        Timespan = 3,
        Boolean = 4
    }

    public class PressController : DataController
    {
        /// <summary>
        /// Main validation function for a constraint. Takes a value as a string and the id of the constraint to check against.
        /// The string value is parsed according to the value type in the specified constraint
        /// Note: function is deterministic (not piece-wise), so future changes will require a complete function review and most likely complete refactoring
        /// Another Note: w/ respect to parsing of min/max numbers, in the interest of simplicity, the type of parse (decimal or int) will depend on if the min value contains a period
        /// </summary>
        /// <param name="constraintId">The database id of the constraint to validate against</param>
        /// <param name="value">The value to be parsed and validated</param>
        /// <returns>True if the value falls within the constraint or if no constraint with that Id is found. False, otherwise</returns>
        private async Task<bool> ValidateConstraint(int constraintId, dynamic value)
        {
            //helper vars
            int minIntValue, maxIntValue;
            decimal minDecValue, maxDecValue;
            TimeSpan minTSValue, maxTSValue;
            bool boolValue; //range doesn't apply to bool but make sure it won't error
            string minStringValue, maxStringValue; //doesn't really apply as a range either but leave the option in case a use is found

            Constraint c = null;

            //using (ConnectToDatabase())
            {
                c = await db.Constraints.FindAsync(constraintId);
                await db.Entry(c).Reference(r => r.ConstraintType1).LoadAsync();
                await db.Entry(c).Reference(r => r.ConstraintValueType).LoadAsync();
            }

            if (c == null)
                return true;

            switch ((TypeOfConstraint)c.ConstraintType)
            {
                case TypeOfConstraint.Range:

                    switch ((ConstraintValueType)c.ValueType)
                    {
                        case ConstraintValueType.Text:
                            minStringValue = c.MinValue ?? "";
                            maxStringValue = c.MaxValue ?? "";

                            //return the comaprison of the lengths
                            return (minStringValue.Length <= value.Length && value.Length <= maxStringValue.Length);

                        //general if/else flow
                        //parse the min/max values; error if unable to
                        //parse the value to check; return false if can't parse
                        //compare and return the result
                        case ConstraintValueType.Number:

                            if (c.MinValue.Contains(".") || c.MaxValue.Contains("."))
                            {
                                bool pb1 = decimal.TryParse(c.MinValue, out minDecValue);
                                bool pb2 = decimal.TryParse(c.MaxValue, out maxDecValue);

                                if (!pb1 || !pb2)
                                    throw new Exception(String.Format("Unable to parse db values. results follow: pb1 = {0}, pb2 = {1}", pb1.ToString(), pb2.ToString()));

                                bool pb3 = decimal.TryParse(value[0], out decimal cv1);

                                if (!pb3)
                                    return false;

                                return (minDecValue <= cv1 && cv1 <= maxDecValue);
                            }
                            else
                            {
                                bool pb4 = int.TryParse(c.MinValue, out minIntValue);
                                bool pb5 = int.TryParse(c.MaxValue, out maxIntValue); //need to parse infinite

                                if (!pb4 || !pb5)
                                    throw new Exception(String.Format("Unable to parse db values. results follow: pb4 = {0}, pb5 = {1}", pb4.ToString(), pb5.ToString()));

                                bool pb6 = int.TryParse(value[0], out int cv2);

                                if (!pb6)
                                    return false;

                                return (minIntValue <= cv2 && cv2 <= maxIntValue);
                            }

                        case ConstraintValueType.Timespan:

                            bool pb7 = TimeSpan.TryParse(c.MinValue, out minTSValue);
                            bool pb8 = TimeSpan.TryParse(c.MaxValue, out maxTSValue);

                            if (!pb7 || !pb8)
                                throw new Exception(String.Format("Unable to parse db values. results follow: pb7 = {0}, pb8 = {1}", pb7.ToString(), pb8.ToString()));

                            TimeSpan cv3;

                            bool pb9 = TimeSpan.TryParse(value[0], out cv3);

                            if (!pb9)
                                return false;

                            return (minTSValue <= cv3 && cv3 <= maxTSValue);

                        case ConstraintValueType.Boolean:
                            return true;

                        default:
                            break;
                    }

                    break;

                case TypeOfConstraint.Minimum:

                    switch ((ConstraintValueType)c.ValueType)
                    {
                        case ConstraintValueType.Text:
                            minStringValue = c.MinValue ?? "";

                            //return the comaprison of the lengths
                            return (minStringValue.Length <= value.Length);

                        //general if/else flow
                        //parse the min/max values; error if unable to
                        //parse the value to check; return false if can't parse
                        //compare and return the result
                        case ConstraintValueType.Number:

                            if (c.MinValue.Contains("."))
                            {
                                bool pb1 = decimal.TryParse(c.MinValue, out minDecValue);

                                if (!pb1)
                                    throw new Exception(String.Format("Unable to parse db values. results follow: pb1 = {0}", pb1.ToString()));

                                bool pb3 = decimal.TryParse(value[0], out decimal cv1);

                                if (!pb3)
                                    return false;

                                return (minDecValue <= cv1);
                            }
                            else
                            {
                                bool pb4 = int.TryParse(c.MinValue, out minIntValue);

                                if (!pb4)
                                    throw new Exception(String.Format("Unable to parse db values. results follow: pb4 = {0}", pb4.ToString()));

                                bool pb6 = int.TryParse(value[0], out int cv2);

                                if (!pb6)
                                    return false;

                                return (minIntValue <= cv2);
                            }

                        case ConstraintValueType.Timespan:

                            bool pb7 = TimeSpan.TryParse(c.MinValue, out minTSValue);

                            if (!pb7)
                                throw new Exception(String.Format("Unable to parse db values. results follow: pb7 = {0}", pb7.ToString()));

                            TimeSpan cv3;

                            bool pb9 = TimeSpan.TryParse(value[0], out cv3);

                            if (!pb9)
                                return false;

                            return (minTSValue <= cv3);

                        case ConstraintValueType.Boolean:
                            return true;

                        default:
                            break;
                    }

                    break;

                case TypeOfConstraint.Maximum:

                    switch ((ConstraintValueType)c.ValueType)
                    {
                        case ConstraintValueType.Text:
                            maxStringValue = c.MinValue ?? "";

                            //return the comaprison of the lengths
                            return (maxStringValue.Length >= value.Length);

                        //general if/else flow
                        //parse the min/max values; error if unable to
                        //parse the value to check; return false if can't parse
                        //compare and return the result
                        case ConstraintValueType.Number:

                            if (c.MinValue.Contains("."))
                            {
                                bool pb1 = decimal.TryParse(c.MinValue, out maxDecValue);

                                if (!pb1)
                                    throw new Exception(String.Format("Unable to parse db values. results follow: pb1 = {0}", pb1.ToString()));

                                bool pb3 = decimal.TryParse(value[0], out decimal cv1);

                                if (!pb3)
                                    return false;

                                return (maxDecValue >= cv1);
                            }
                            else
                            {
                                bool pb4 = int.TryParse(c.MinValue, out maxIntValue);

                                if (!pb4)
                                    throw new Exception(String.Format("Unable to parse db values. results follow: pb4 = {0}", pb4.ToString()));

                                bool pb6 = int.TryParse(value[0], out int cv2);

                                if (!pb6)
                                    return false;

                                return (maxIntValue >= cv2);
                            }

                        case ConstraintValueType.Timespan:

                            bool pb7 = TimeSpan.TryParse(c.MinValue, out maxTSValue);

                            if (!pb7)
                                throw new Exception(String.Format("Unable to parse db values. results follow: pb7 = {0}", pb7.ToString()));

                            TimeSpan cv3;

                            bool pb9 = TimeSpan.TryParse(value[0], out cv3);

                            if (!pb9)
                                return false;

                            return (maxTSValue >= cv3);

                        case ConstraintValueType.Boolean:
                            return true;

                        default:
                            break;
                    }

                    break;

                case TypeOfConstraint.Exact:

                    switch ((ConstraintValueType)c.ValueType)
                    {
                        case ConstraintValueType.Text:
                            minStringValue = c.MinValue ?? "";

                            //return the comaprison of the lengths
                            return (minStringValue.Length <= value.Length);

                        //general if/else flow
                        //parse the min/max values; error if unable to
                        //parse the value to check; return false if can't parse
                        //compare and return the result
                        case ConstraintValueType.Number:

                            if (c.MinValue.Contains("."))
                            {
                                bool pb1 = decimal.TryParse(c.MinValue, out minDecValue);

                                if (!pb1)
                                    throw new Exception(String.Format("Unable to parse db values. results follow: pb1 = {0}", pb1.ToString()));

                                bool pb3 = decimal.TryParse(value[0], out decimal cv1);

                                if (!pb3)
                                    return false;

                                return (minDecValue == cv1);
                            }
                            else
                            {
                                bool pb4 = int.TryParse(c.MinValue, out minIntValue);

                                if (!pb4)
                                    throw new Exception(String.Format("Unable to parse db values. results follow: pb4 = {0}", pb4.ToString()));

                                bool pb6 = int.TryParse(value[0], out int cv2);

                                if (!pb6)
                                    return false;

                                return (minIntValue == cv2);
                            }

                        case ConstraintValueType.Timespan:

                            bool pb7 = TimeSpan.TryParse(c.MinValue, out minTSValue);

                            if (!pb7)
                                throw new Exception(String.Format("Unable to parse db values. results follow: pb7 = {0}", pb7.ToString()));

                            TimeSpan cv3;

                            bool pb9 = TimeSpan.TryParse(value[0], out cv3);

                            if (!pb9)
                                return false;

                            return (minTSValue == cv3);

                        case ConstraintValueType.Boolean:
                            bool pb10 = Boolean.TryParse(c.MinValue, out boolValue);

                            if (!pb10)
                                throw new Exception(String.Format("Unable to parse db values. results follow: pb4 = {0}", pb10.ToString()));

                            bool cv4;

                            bool pb11 = Boolean.TryParse(value[0], out cv4);

                            if (!pb11)
                                return false;

                            return boolValue == cv4;

                        default:
                            break;
                    }

                    break;

                default:
                    return true;
            }

            return false; //cause why not
        }

        public async Task<ActionResult> NewConstraint()
        {
            //using (ConnectToDatabase())
            {
                ViewData["Constraint.ConstraintType"] = await GenerateSelectListAsync(SelectListType.ConstraintTypes);
                ViewData["Constraint.ValueType"] = await GenerateSelectListAsync(SelectListType.ConstraintValueTypes);
            }

            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(RenderViewToString("~/Views/Partials/_ConstraintEditor.cshtml", new VM_ConstraintEditor { Constraint = new Constraint(), IsRange = false }), new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> SaveConstraint(Constraint constraint, int? press)
        {
            if (!press.HasValue)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Must provide a press id");

            //using (ConnectToDatabase())
            {
                //ensure the provided press id is valid
                Press p = await db.Presses.FindAsync(press);

                if (p == null)
                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Id provided but no press found");

                if (constraint.Id == 0)
                {
                    constraint.Press = press.Value;
                    int newId = db.Constraints.Max(x => x.Id) + 1;
                    constraint.Id = newId;
                    if (String.IsNullOrEmpty(constraint.ErrorMessage))
                        constraint.ErrorMessage = "";

                    db.Constraints.Add(constraint);
                }
                else
                {
                    Constraint dbConstraint = db.Constraints.Find(constraint.Id);
                    dbConstraint.Press = press.Value;
                    dbConstraint.ConstraintName = constraint.ConstraintName;
                    dbConstraint.MinValue = constraint.MinValue;
                    dbConstraint.MaxValue = constraint.MaxValue;
                    dbConstraint.Unit = constraint.Unit;
                    dbConstraint.ValueType = constraint.ValueType;
                    dbConstraint.ConstraintType = constraint.ConstraintType;
                    dbConstraint.ErrorMessage = constraint.ErrorMessage ?? "";

                    db.Entry(dbConstraint).State = System.Data.Entity.EntityState.Modified;
                }

                await db.SaveChangesAsync();
            }
            return Json(new { success = true });
        }

        public async Task<ActionResult> GetConstraintForEdit(int? id, bool ReturnHtmlString = true)
        {
            //using (ConnectToDatabase())
            {
                Constraint c = await db.Constraints.FindAsync(id);

                if (c == null)
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.NotFound, "Unable to find a record with that id");

                await db.Entry(c).Reference(r => r.ConstraintType1).LoadAsync();
                await db.Entry(c).Reference(r => r.ConstraintValueType).LoadAsync();

                ViewData["Constraint.ValueType"] = await GenerateSelectListAsync(SelectListType.ConstraintValueTypes, c, "ValueType");
                ViewData["Constraint.ConstraintType"] = await GenerateSelectListAsync(SelectListType.ConstraintTypes, c, "ConstraintType");

                VM_ConstraintEditor vm = new VM_ConstraintEditor
                {
                    Constraint = c,
                    IsRange = c.ConstraintType1.Title == "Range"
                };

                if (ReturnHtmlString)
                {
                    return new JsonResult { Data = JsonConvert.SerializeObject(RenderViewToString("~/Views/Partials/_ConstraintEditor.cshtml", vm)), JsonRequestBehavior = JsonRequestBehavior.AllowGet };
                }
                else
                {
                    return View("~/Views/Partials/_ConstraintEditor.cshtml", vm);
                }
            }
        }

        /// <summary>
        /// Meant for constraint editing. Takes a ConstraintType id value and returns the fields which should be shown (min/max or single value)
        /// </summary>
        /// <param name="constraintTypeId">the Id of the ConstraintType record</param>
        /// <returns>A Json result indicating the following: "range" if this constraint type requires a range input, otherwise "value" if this constraint requires a single value input</returns>
        public async Task<ActionResult> ConstraintTypeCheck(int? constraintTypeId)
        {
            if (constraintTypeId.HasValue)
            {
                //using (ConnectToDatabase())
                {
                    ConstraintType ct = await db.ConstraintTypes.FindAsync(constraintTypeId);

                    if (ct == null)
                        return new HttpStatusCodeResult(HttpStatusCode.NotFound, "No Constraint Type exists with that id");

                    if (ct.Id == 1)
                        return new JsonResult { Data = "range", JsonRequestBehavior = JsonRequestBehavior.AllowGet };
                    else
                        return new JsonResult { Data = "value", JsonRequestBehavior = JsonRequestBehavior.AllowGet };
                }
            }

            return new HttpStatusCodeResult(HttpStatusCode.OK);
        }

        //returns the default and helper services for the press specified by id
        public async Task<ActionResult> GetDefaultServices(int? pressId, int? itemId, int? speedId, string itemtype)
        {
            if (!pressId.HasValue)
                return new HttpStatusCodeResult(HttpStatusCode.NotFound);

            //get helper services and case them into a default service record
            //get default service records
            //concat together
            try
            {
                //default color profile load: NOTE: does not currently look at SetColorProfile bool (just do for all because it's on press change)
                PressSpeed selectedSpeed = await db.PressSpeeds.FindAsync(speedId);

                switch (itemtype.ToUpper())
                {
                    case "QUOTE":
                        QuoteItem qi = await db.QuoteItems.FindAsync(itemId);
                        if (qi != null)
                        {
                            if (selectedSpeed != null)
                            {
                                qi.PressColorProfileFront = selectedSpeed.ColorProfileFront;
                                qi.PressColorCountFront = selectedSpeed.ColorCountFront;
                                qi.PressColorProfileBack = selectedSpeed.ColorProfileBack;
                                qi.PressColorCountBack = selectedSpeed.ColorCountBack;
                            }
                            else
                            {
                                qi.PressColorProfileFront = "4CP";
                                qi.PressColorCountFront = 4;
                                qi.PressColorProfileBack = "None";
                                qi.PressColorCountBack = 0;
                            }

                            db.Entry(qi).State = EntityState.Modified;
                            await db.SaveChangesAsync();
                        }
                        break;

                    case "JOB":
                        JobItem ji = await db.JobItems.FindAsync(itemId);
                        if (ji != null)
                        {
                            if (selectedSpeed != null)
                            {
                                ji.PressColorProfileFront = selectedSpeed.ColorProfileFront;
                                ji.PressColorCountFront = selectedSpeed.ColorCountFront;
                                ji.PressColorProfileBack = selectedSpeed.ColorProfileBack;
                                ji.PressColorCountBack = selectedSpeed.ColorCountBack;
                            }
                            else
                            {
                                ji.PressColorProfileFront = "4CP";
                                ji.PressColorCountFront = 4;
                                ji.PressColorProfileBack = "None";
                                ji.PressColorCountBack = 0;
                            }

                            db.Entry(ji).State = EntityState.Modified;
                            await db.SaveChangesAsync();
                        }
                        break;
                }

                var hSers = await db.PressHelperServices
                    .Include(i => i.Service1)
                    .Include(i => i.Service1.ServiceCategory)
                    .Include(i => i.Service1.ServiceCategory.ServiceType1)
                    .Where(x => x.Press == pressId.Value).ToListAsync();
                var dSers = await db.DefaultPressServices
                    .Include(i => i.Service1)
                    .Include(i => i.Service1.ServiceCategory)
                    .Include(i => i.Service1.ServiceCategory.ServiceType1)
                    .Where(x => x.Press == pressId.Value).ToListAsync();

                //add helper services
                var model = new
                {
                    prepress = hSers
                        .Where(x => x.Service1.ServiceCategory.ServiceType1.Title == "Prepress")
                        .Select(x => new { x.Service })
                        .ToList(),
                    cutting = hSers
                        .Where(x => x.Service1.ServiceCategory.ServiceType1.Title == "Cutting")
                        .Select(x => new { x.Service })
                        .ToList(),
                    finishing = hSers
                        .Where(x => x.Service1.ServiceCategory.ServiceType1.Title == "Finishing")
                        .Select(x => new { x.Service })
                        .ToList(),
                };
                //add default services
                model.prepress.AddRange(
                    dSers.Where(x => x.Service1.ServiceCategory.ServiceType1.Title == "Prepress").Select(x => new { x.Service }).ToList()
                );
                model.cutting.AddRange(
                    dSers.Where(x => x.Service1.ServiceCategory.ServiceType1.Title == "Cutting").Select(x => new { x.Service }).ToList()
                );
                model.finishing.AddRange(
                    dSers.Where(x => x.Service1.ServiceCategory.ServiceType1.Title == "Finishing").Select(x => new { x.Service }).ToList()
                );

                return new JsonResult { Data = JsonConvert.SerializeObject(model, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }), JsonRequestBehavior = JsonRequestBehavior.AllowGet };
            }
            catch (Exception e) { return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, e.Message); }
        }

        public async Task<ActionResult> NewPress()
        {
            ViewData["Press.Category"] = await GenerateSelectListAsync(SelectListType.PressCategories);
            ViewData["Press.SpeedDenominator"] = await GenerateSelectListAsync(SelectListType.SpeedDenominators);
            //ViewData["Press.ColorProfile"] = await GenerateSelectListAsync(SelectListType.ColorProfiles);
            ViewData["Press.InkCostDenominatorUnit"] = await GenerateSelectListAsync(SelectListType.InkCostDenominatorUnit);

            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(RenderViewToString("~/Views/Press/GetPressForEdit.cshtml",
                new VM_Press { StockTypes = await db.MaterialStockTypes.ToListAsync(), SheetConfig = new VM_PressSheetConfig { Press = new Press() } })),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        //need to add constraints to this
        [HttpPost]
        public async Task<ActionResult> SavePress(SavePress_CM savePress)
        {
            if (savePress == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            if (savePress.theItem == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            if (savePress.DefaultPressServices == null)
                savePress.DefaultPressServices = new List<DefaultPressService>();

            if (savePress.HelperServices == null)
                savePress.HelperServices = new List<PressHelperService>();

            var p = await db.Presses.FindAsync(savePress.theItem.Id);

            if (p == null)
            {
                //set default speed if possible
                try
                {
                    PressSpeed ps = db.PressSpeeds.Single(x => x.Press == savePress.theItem.Id && x.IsDefault == true);
                    savePress.theItem.SpeedNumerator = ps.SpeedNumerator;
                    savePress.theItem.SpeedDenominator = ps.SpeedDenominator;
                }
                catch (Exception)
                {
                    //no default speed found
                    savePress.theItem.SpeedNumerator = 0;
                    savePress.theItem.SpeedDenominator = (int)SpeedDenominators.None;
                }

                //add new press
                db.Presses.Add(savePress.theItem);
            }
            else
            {
                //save existing press
                p = UpdatePressRecord(p, savePress.theItem);
                db.Entry(p).State = EntityState.Modified;
            }
            //save press info
            await db.SaveChangesAsync();

            List<PressHelperService> existingHelperServices = await db.PressHelperServices.Where(x => x.Press == savePress.theItem.Id).ToListAsync();

            //add missing helper services
            foreach (PressHelperService phs in savePress.HelperServices)
            {
                if (!existingHelperServices.Any(x => x.Id == phs.Id))
                    db.PressHelperServices.Add(phs);
            }
            //remove helper services that are not present
            foreach (PressHelperService phs in existingHelperServices)
            {
                if (!savePress.HelperServices.Any(x => x.Id == phs.Id))
                    db.PressHelperServices.Remove(phs);
            }

            await db.SaveChangesAsync();

            //save default services
            List<DefaultPressService> existingServices = await db.DefaultPressServices.Where(x => x.Press == savePress.theItem.Id).ToListAsync();

            //add any DefaultPressService records from savePress.DefaultPressServices that do not exist in existingServices
            foreach (DefaultPressService dps in savePress.DefaultPressServices)
            {
                if (!existingServices.Any(x => x.Id == dps.Id))
                    db.DefaultPressServices.Add(dps);
            }
            //remove any DefaultPressService records from existingServices that do not exist in savePress.DefaultPressServices
            foreach (DefaultPressService dps in existingServices)
            {
                if (!savePress.DefaultPressServices.Any(x => x.Id == dps.Id))
                    db.DefaultPressServices.Remove(dps);
            }
            foreach (DefaultPressService dps in savePress.DefaultPressServices)
            {
                try
                {
                    DefaultPressService editService = existingServices.Single(x => x.Id == dps.Id);
                    editService.Ordinal = dps.Ordinal;
                    db.Entry(editService).State = EntityState.Modified;
                }
                catch (Exception)
                {
                    //really shouldn't get here
                }
            }

            await db.SaveChangesAsync();

            return new JsonResult { Data = "done", JsonRequestBehavior = JsonRequestBehavior.AllowGet };
        }

        public async Task<ActionResult> CreatePressStockConfig(int? sourcePress, int? stockId)
        {
            if (!stockId.HasValue || !sourcePress.HasValue)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Missing required parameters; stock type id and press id required");

            //using (ConnectToDatabase())
            {
                //try to find a press record with a matching stock type and frompress = sourcePress
                try
                {
                    Press p = await db.Presses.SingleAsync(x => x.StockType == stockId && x.FromPress == sourcePress);

                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Record already exists");
                }
                catch (Exception)
                {
                    //press does not exist, which is good.
                    //get the original press
                    Press originalPress = await db.Presses.FindAsync(sourcePress);

                    if (originalPress == null)
                        return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "cannot find original press record");

                    Press newPress = CopyPress(originalPress);

                    int newPressId = 0;

                    try
                    {
                        newPressId = await db.Presses.MaxAsync(x => x.Id);
                    }
                    catch (Exception)
                    {
                        //ok; move on
                    }

                    newPressId++;

                    newPress.Id = newPressId;
                    newPress.FromPress = sourcePress;
                    newPress.StockType = stockId;

                    db.Presses.Add(newPress);

                    await db.SaveChangesAsync();
                }

                return new HttpStatusCodeResult(HttpStatusCode.OK);
            }
        }

        public async Task<ActionResult> LoadPressStockConfig(int? stockTypeId, int? pressId)
        {
            if (!stockTypeId.HasValue || !pressId.HasValue)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Missing required parameters; stock type id and press id required");

            //using (ConnectToDatabase())
            {
                // Load the initial press record
                Press thePress = await db.Presses.FindAsync(pressId);

                if (thePress == null)
                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Cannot find that press");

                try
                {
                    Press correspondingPress = null;

                    try
                    {
                        // Try to find a corresponding press record with matching FromPress and StockType
                        correspondingPress = await db.Presses
                            .SingleAsync(x => x.FromPress == pressId.Value && x.StockType == (int)stockTypeId.Value);
                    }
                    catch (Exception)
                    {
                        //IMPORTANT: If the following single async call fails, move to the catch of the parent try/catch (means there is no existing stock type for that press/stock config)
                        correspondingPress = await db.Presses
                            .SingleAsync(x => x.Id == pressId.Value && x.StockType == (int)stockTypeId.Value);
                    }
                    await db.Entry(correspondingPress).Collection(c => c.PressLinkedServices).LoadAsync();
                    await db.Entry(correspondingPress).Collection(c => c.DefaultPressServices).LoadAsync();

                    foreach (PressLinkedService pls in correspondingPress.PressLinkedServices)
                    {
                        await db.Entry(pls).Reference(x => x.Service1).LoadAsync();
                    }

                    foreach(DefaultPressService dps in correspondingPress.DefaultPressServices)
                    {
                        await db.Entry(dps).Reference(r => r.Service1).LoadAsync();
                    }

                    ViewData["Press.Category"] = await GenerateSelectListAsync(SelectListType.PressCategories, correspondingPress, "Category");
                    ViewData["Press.SpeedDenominator"] = await GenerateSelectListAsync(SelectListType.SpeedDenominators, correspondingPress, "SpeedDenominator");
                    ViewData["Press.InkCostDenominatorUnit"] = await GenerateSelectListAsync(SelectListType.InkCostDenominatorUnit, correspondingPress, "InkCostDenominatorUnit");

                    // Record found, proceed to render the partial view
                    switch ((Material.AvailableStockTypes)stockTypeId.Value)
                    {
                        case Material.AvailableStockTypes.Sheet:
                            VM_PressSheetConfig vSheet = new VM_PressSheetConfig
                            {
                                Press = correspondingPress,
                                HasRecord = true
                            };
                            return new JsonResult
                            {
                                Data = JsonConvert.SerializeObject(new { Html = RenderViewToString("~/Views/Press/_PressSheetConfig.cshtml", vSheet), vSheet.HasRecord },
                                    new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                JsonRequestBehavior = JsonRequestBehavior.AllowGet
                            };

                        case Material.AvailableStockTypes.RollStock:
                            VM_PressRollStockConfig vRollStock = new VM_PressRollStockConfig
                            {
                                Press = correspondingPress,
                                HasRecord = true
                            };
                            return new JsonResult
                            {
                                Data = JsonConvert.SerializeObject(new { Html = RenderViewToString("~/Views/Press/_PressRollStockConfig.cshtml", vRollStock), vRollStock.HasRecord },
                                    new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                JsonRequestBehavior = JsonRequestBehavior.AllowGet
                            };

                        case Material.AvailableStockTypes.CutVinyl:
                            VM_PressCutVinylConfig vVinyl = new VM_PressCutVinylConfig
                            {
                                Press = correspondingPress,
                                HasRecord = true
                            };
                            return new JsonResult
                            {
                                Data = JsonConvert.SerializeObject(new { Html = RenderViewToString("~/Views/Press/_PressCutVinylConfig.cshtml", vVinyl), vVinyl.HasRecord },
                                    new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                JsonRequestBehavior = JsonRequestBehavior.AllowGet
                            };

                        default:
                            return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Default in switch; provided stock type not found");
                    }
                }
                catch (Exception)
                {
                    // Record not found, instantiate the Press to a default record
                    Press defaultPress = new Press();

                    switch ((Material.AvailableStockTypes)stockTypeId.Value)
                    {
                        case Material.AvailableStockTypes.Sheet:
                            VM_PressSheetConfig vSheet = new VM_PressSheetConfig
                            {
                                Press = defaultPress,
                                HasRecord = false
                            };

                            return new JsonResult
                            {
                                Data = JsonConvert.SerializeObject(new { Html = "", vSheet.HasRecord },
                                    new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                JsonRequestBehavior = JsonRequestBehavior.AllowGet
                            };

                        case Material.AvailableStockTypes.RollStock:
                            VM_PressRollStockConfig vRollStock = new VM_PressRollStockConfig
                            {
                                Press = defaultPress,
                                HasRecord = false
                            };

                            return new JsonResult
                            {
                                Data = JsonConvert.SerializeObject(new { Html = "", vRollStock.HasRecord },
                                    new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                JsonRequestBehavior = JsonRequestBehavior.AllowGet
                            };

                        case Material.AvailableStockTypes.CutVinyl:
                            VM_PressCutVinylConfig vVinyl = new VM_PressCutVinylConfig
                            {
                                Press = defaultPress,
                                HasRecord = false
                            };

                            return new JsonResult
                            {
                                Data = JsonConvert.SerializeObject(new { Html = "", vVinyl.HasRecord },
                                    new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                                JsonRequestBehavior = JsonRequestBehavior.AllowGet
                            };

                        default:
                            return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Default in switch; provided stock type not found");
                    }
                }
            }
        }

        public async Task<ActionResult> GetPressForEdit(int? id)
        {
            if (id.HasValue)
            {
                Press p = await db.Presses.Include(i => i.PressCategory).SingleOrDefaultAsync(x => x.Id == id.Value);

                await db.Entry(p).Reference(r => r.PressCategory).LoadAsync();
                await db.Entry(p).Reference(r => r.SpeedDenominator1).LoadAsync();
                await db.Entry(p).Reference(r => r.InkCostDenominatorUnit1).LoadAsync();
                await db.Entry(p).Collection(c => c.DefaultPressServices).LoadAsync();
                await db.Entry(p).Collection(c => c.PressHelperServices).LoadAsync();

                foreach (PressHelperService phs in p.PressHelperServices)
                    await db.Entry(phs).Reference(r => r.Service1).LoadAsync();

                foreach (DefaultPressService dps in p.DefaultPressServices)
                    await db.Entry(dps).Reference(r => r.Service1).LoadAsync();

                foreach (PressLinkedService pls in p.PressLinkedServices)
                    await db.Entry(pls).Reference(r => r.Service1).LoadAsync();

                ViewData["Press.Category"] = await GenerateSelectListAsync(SelectListType.PressCategories, p, "Category");
                ViewData["Press.SpeedDenominator"] = await GenerateSelectListAsync(SelectListType.SpeedDenominators, p, "SpeedDenominator");
                ViewData["Press.InkCostDenominatorUnit"] = await GenerateSelectListAsync(SelectListType.InkCostDenominatorUnit, p, "InkCostDenominatorUnit");

                VM_Press pressVM = new VM_Press
                {
                    StockTypes = await db.MaterialStockTypes.ToListAsync(),
                    SheetConfig = new VM_PressSheetConfig
                    {
                        Press = p
                    },
                    Services = await db.Services.Where(x => x.Deleted == null).OrderBy(x => x.Title).ToListAsync()
                };

                return new JsonResult { Data = JsonConvert.SerializeObject(RenderViewToString("~/Views/Press/GetPressForEdit.cshtml", pressVM)), JsonRequestBehavior = JsonRequestBehavior.AllowGet };
            }

            return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "No id provided");
        }

        public async Task<ActionResult> LoadConstraints(int? press)
        {
            if (!press.HasValue)
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Must provide a press id");

            //using (ConnectToDatabase())
            {
                Press p = await db.Presses.FindAsync(press);

                if (p == null)
                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, String.Format("No press found with id {0}", press.Value));

                List<Constraint> constraints = await db.Constraints.Where(x => x.Press == press.Value).ToListAsync();

                foreach (Constraint c in constraints)
                {
                    await db.Entry(c).Reference(r => r.ConstraintType1).LoadAsync();
                    await db.Entry(c).Reference(r => r.ConstraintValueType).LoadAsync();
                }

                return new JsonResult
                {
                    Data = JsonConvert.SerializeObject(RenderViewToString("~/Views/Partials/_ConstraintList.cshtml", await db.Constraints.Where(x => x.Press == press.Value).ToListAsync())),
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
        }

        public async Task<ActionResult> ConstraintIsValid(int? constraintId, dynamic value)
        {
            if (!constraintId.HasValue)
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "No constraint id provided");

            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(await ValidateConstraint(constraintId.Value, value), new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> GetPressSpeeds(int? press)
        {
            if (!press.HasValue)
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "No press id provided");

            Press thePress = await db.Presses.FindAsync(press);
            if (thePress != null)
            {
                List<PressSpeed> speeds = await db.PressSpeeds.Where(x => x.Press == thePress.Id || x.Press == thePress.FromPress).OrderBy(y => y.Ordinal).ToListAsync();

                foreach (PressSpeed speed in speeds)
                    await db.Entry(speed).Reference(r => r.SpeedDenominator1).LoadAsync();

                return new JsonResult { Data = RenderViewToString("~/Views/Partials/_SpeedList.cshtml", speeds), JsonRequestBehavior = JsonRequestBehavior.AllowGet };
            }

            return new HttpStatusCodeResult(HttpStatusCode.NotFound, "Unable to find that press record");
        }

        public async Task<ActionResult> GetPressSpeedForEdit(int? pressSpeedId)
        {
            PressSpeed ps = new PressSpeed { IsDefault = false };
            //using (ConnectToDatabase())
            {
                if (pressSpeedId.HasValue)
                {
                    ps = await db.PressSpeeds.FindAsync(pressSpeedId);
                    ViewData["SpeedDenominator"] = await GenerateSelectListAsync(SelectListType.SpeedDenominators, ps, "SpeedDenominator");
                }
                else
                {
                    ViewData["SpeedDenominator"] = await GenerateSelectListAsync(SelectListType.SpeedDenominators);
                }
            }

            return new JsonResult { Data = RenderViewToString("~/Views/Partials/_SpeedEditor.cshtml", ps), JsonRequestBehavior = JsonRequestBehavior.AllowGet };
        }

        public async Task<ActionResult> SavePressSpeed(PressSpeed pressSpeed)
        {
            //using (ConnectToDatabase())
            {
                if (pressSpeed.Id == 0)
                {
                    //new speed
                    int newId = 1;

                    try
                    {
                        newId = await db.PressSpeeds.MaxAsync(x => x.Id) + 1;
                    }
                    catch (Exception)
                    {
                        //ok; move on
                    }

                    pressSpeed.Id = newId;

                    if (pressSpeed.IsDefault.Value == true)
                    {
                        //if default, make sure to unset an existing defaults
                        try
                        {
                            PressSpeed ps = await db.PressSpeeds.SingleAsync(x => x.Press == pressSpeed.Press && (x.IsDefault ?? false) == true);
                            ps.IsDefault = false;
                            db.Entry(ps).State = EntityState.Modified;
                        }
                        catch (Exception)
                        {
                            //no default press speed found
                        }
                    }

                    db.PressSpeeds.Add(pressSpeed);
                }
                else
                {
                    //editing speed
                    PressSpeed existing = await db.PressSpeeds.FindAsync(pressSpeed.Id);
                    existing.SpeedNumerator = pressSpeed.SpeedNumerator;
                    existing.SpeedDenominator = pressSpeed.SpeedDenominator;
                    existing.Notes = pressSpeed.Notes;

                    //if pressSpeed isDefault is true, update any other existing press speed records (for this press) that are default
                    if ((pressSpeed.IsDefault ?? false))
                    {
                        List<PressSpeed> existingDefaults = await db.PressSpeeds.Where(x => x.Press == pressSpeed.Press).ToListAsync();

                        foreach (PressSpeed ps in existingDefaults)
                        {
                            if (ps.Id != pressSpeed.Id)
                            {
                                ps.IsDefault = false;
                                db.Entry(ps).State = EntityState.Modified;
                            }
                        }
                        existing.IsDefault = pressSpeed.IsDefault;
                    }

                    db.Entry(existing).State = EntityState.Modified;
                }

                await db.SaveChangesAsync();
            }

            return new JsonResult { Data = "done", JsonRequestBehavior = JsonRequestBehavior.AllowGet };
        }

        public async Task<ActionResult> SaveSpeedArrangement(PressSpeedArrangement_CM arrangement)
        {
            List<int> i = arrangement.SpeedIds.ToList();
            List<int> o = arrangement.SpeedOrdinals.ToList();

            int itemCount = i.Count;

            //using (ConnectToDatabase())
            {
                for (int a = 0; a < itemCount; a++)
                {
                    PressSpeed ps = await db.PressSpeeds.FindAsync(i[a]);

                    if (ps != null)
                    {
                        ps.Ordinal = o[a];
                        ps.IsDefault = ps.IsDefault ?? false;
                        db.Entry(ps).State = EntityState.Modified;
                    }
                }

                await db.SaveChangesAsync();
            }

            return new JsonResult { Data = "done", JsonRequestBehavior = JsonRequestBehavior.AllowGet };
        }

        public async Task<ActionResult> ItemLoadSpeeds(int? id, string itemType)
        {
            if (id.HasValue)
            {
                List<PressSpeed> speeds = await db.PressSpeeds.Where(x => x.Press == id.Value).OrderBy(y => y.Ordinal).ToListAsync();

                foreach (PressSpeed ps in speeds)
                    await db.Entry(ps).Reference(r => r.SpeedDenominator1).LoadAsync();

                ViewData["SpeedDenominator"] = await GenerateSelectListAsync(SelectListType.SpeedDenominators);

                return new JsonResult
                {
                    Data = RenderViewToString("~/Views/Partials/_PressSpeedRow.cshtml", speeds),
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }

            return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "no id provided");
        }

        public async Task<ActionResult> ActivateStockTypeForPress(int? pressId, int? stockId)
        {
            if (!pressId.HasValue || !stockId.HasValue)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Press id and stock type required");

            //using (ConnectToDatabase())
            {
                //get the press as indicated by pressId
                Press origPress = await db.Presses.FindAsync(pressId);

                if (origPress == null)
                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Press not found");

                try
                {
                    //try to find a press record
                    Press newStockType = await db.Presses.SingleAsync(x => x.FromPress == origPress.Id && x.StockType == stockId);

                    if ((newStockType.ActiveStockType ?? false))
                    {
                        //press record found; enable it if not enabled.
                        newStockType.ActiveStockType = true;

                        db.Entry(newStockType).State = EntityState.Modified;
                    }
                }
                catch (Exception)
                {
                    int newId = 0;

                    try
                    {
                        newId = await db.Presses.MaxAsync(x => x.Id);
                    }
                    catch (Exception)
                    {
                        //ok; move on
                    }

                    newId++;
                    //that record was not found, so create one
                    Press newPress = CopyPress(origPress);
                    newPress.Id = newId;
                    newPress.ActiveStockType = true;
                    newPress.StockType = stockId;
                    db.Presses.Add(newPress);
                }

                await db.SaveChangesAsync(); //save
            }

            return new HttpStatusCodeResult(HttpStatusCode.OK, "Done");
        }
    }
}