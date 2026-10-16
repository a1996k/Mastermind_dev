using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Objects;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using static FifthPress.Controllers.DataController;

namespace FifthPress.Controllers
{
    public class FormulaController : DataController
    {
        public ActionResult LinearCuttingFormula(string recordtype, int? itemRecordId, int? itemServiceId)
        {
            if (String.IsNullOrEmpty(recordtype) || !itemRecordId.HasValue || !itemServiceId.HasValue)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Invalid parameter configuration. Please inspect");

            try
            {
                ObjectResult<decimal?> procedureReturn;
                decimal t = 0m;

                switch (recordtype.ToUpper())
                {
                    case "QUOTE":
                        procedureReturn = db.Quote_CalculateLinearCuttingTime(itemServiceId);
                        break;

                    case "JOB":
                        procedureReturn = db.Job_CalculateLinearCuttingTime(itemServiceId);
                        break;

                    default:
                        return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, "No record type specified");
                }

                t = procedureReturn.First() ?? 0m;

                return new JsonResult { Data = JsonConvert.SerializeObject(t, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }), JsonRequestBehavior = JsonRequestBehavior.AllowGet };
            }
            catch (Exception e)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, e.Message);
            }
        }

        public ActionResult ZundCuttingFormula(string recordtype, int? itemRecordId, int? itemServiceId)
        {
            if (String.IsNullOrEmpty(recordtype) || !itemRecordId.HasValue || !itemServiceId.HasValue)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Invalid parameter configuration. Please inspect");

            try
            {
                ObjectResult<decimal?> procedureReturn;
                decimal t = 0m;

                switch (recordtype.ToUpper())
                {
                    case "QUOTE":
                        procedureReturn = db.Quote_CalculateZundCuttingTime(itemServiceId);
                        break;

                    case "JOB":
                        procedureReturn = db.Job_CalculateZundCuttingTime(itemServiceId);
                        break;

                    default:
                        return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, "No record type specified");
                }

                t = procedureReturn.First() ?? 0m;

                return new JsonResult { Data = JsonConvert.SerializeObject(t, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }), JsonRequestBehavior = JsonRequestBehavior.AllowGet };
            }
            catch (Exception e)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, e.Message);
            }
        }

        //three hole
        public ActionResult ThreeHolePunchFormula(string recordtype, int? itemRecordId, int? itemServiceId)
        {
            if (String.IsNullOrEmpty(recordtype) || !itemRecordId.HasValue || !itemServiceId.HasValue)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Invalid parameter configuration. Please inspect");

            try
            {
                ObjectResult<decimal?> procedureReturn;
                decimal t = 0m;

                switch (recordtype.ToUpper())
                {
                    case "QUOTE":
                        procedureReturn = db.Quote_ThreeHolePunchTime(itemServiceId);
                        break;

                    case "JOB":
                        procedureReturn = db.Job_ThreeHolePunchTime(itemServiceId);
                        break;

                    default:
                        return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, "No record type specified");
                }

                t = procedureReturn.First() ?? 0m;
                return new JsonResult { Data = JsonConvert.SerializeObject(t, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }), JsonRequestBehavior = JsonRequestBehavior.AllowGet };
            }
            catch (Exception e)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, e.Message);
            }
        }

        //variable hole punch
        public ActionResult VariableHolePunchFormula(string recordtype, int? itemRecordId, int? itemServiceId)
        {
            if (String.IsNullOrEmpty(recordtype) || !itemRecordId.HasValue || !itemServiceId.HasValue)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Invalid parameter configuration. Please inspect");

            try
            {
                ObjectResult<decimal?> procedureReturn;
                decimal t = 0m;

                switch (recordtype.ToUpper())
                {
                    case "QUOTE":
                        procedureReturn = db.Quote_VariableHolePunchTime(itemServiceId);
                        break;

                    case "JOB":
                        procedureReturn = db.Job_VariableHolePunchTime(itemServiceId);
                        break;

                    default:
                        return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, "No record type specified");
                }

                t = procedureReturn.First() ?? 0m; return new JsonResult { Data = JsonConvert.SerializeObject(t, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }), JsonRequestBehavior = JsonRequestBehavior.AllowGet };
            }
            catch (Exception e)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, e.Message);
            }
        }

        //folding time
        public ActionResult FoldingTimeFormula(string recordtype, int? itemRecordId, int? itemServiceId)
        {
            if (String.IsNullOrEmpty(recordtype) || !itemRecordId.HasValue || !itemServiceId.HasValue)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Invalid parameter configuration. Please inspect");

            try
            {
                ObjectResult<decimal?> procedureReturn;
                decimal t = 0m;

                switch (recordtype.ToUpper())
                {
                    case "QUOTE":
                        procedureReturn = db.Quote_FoldingTime(itemServiceId);
                        break;

                    case "JOB":
                        procedureReturn = db.Job_FoldingTime(itemServiceId);
                        break;

                    default:
                        return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, "No record type specified");
                }

                t = procedureReturn.First() ?? 0m; return new JsonResult { Data = JsonConvert.SerializeObject(t, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }), JsonRequestBehavior = JsonRequestBehavior.AllowGet };
            }
            catch (Exception e)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, e.Message);
            }
        }

        //scoring
        public ActionResult ScoringFormula(string recordtype, int? itemRecordId, int? itemServiceId)
        {
            if (String.IsNullOrEmpty(recordtype) || !itemRecordId.HasValue || !itemServiceId.HasValue)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Invalid parameter configuration. Please inspect");

            try
            {
                ObjectResult<decimal?> procedureReturn;
                decimal t = 0m;

                switch (recordtype.ToUpper())
                {
                    case "QUOTE":
                        procedureReturn = db.Quote_ScoringTime(itemServiceId);
                        break;

                    case "JOB":
                        procedureReturn = db.Job_ScoringTime(itemServiceId);
                        break;

                    default:
                        return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, "No record type specified");
                }

                t = procedureReturn.First() ?? 0m;
                return new JsonResult { Data = JsonConvert.SerializeObject(t, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }), JsonRequestBehavior = JsonRequestBehavior.AllowGet };
            }
            catch (Exception e)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, e.Message);
            }
        }

        //polybagging
        public ActionResult PolybaggingFormula(string recordtype, int? itemRecordId, int? itemServiceId)
        {
            if (String.IsNullOrEmpty(recordtype) || !itemRecordId.HasValue || !itemServiceId.HasValue)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Invalid parameter configuration. Please inspect");

            try
            {
                ObjectResult<decimal?> procedureReturn;
                decimal t = 0m;

                switch (recordtype.ToUpper())
                {
                    case "QUOTE":
                        procedureReturn = db.Quote_PolybaggingTime(itemServiceId);
                        break;

                    case "JOB":
                        procedureReturn = db.Job_PolybaggingTime(itemServiceId);
                        break;

                    default:
                        return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, "No record type specified");
                }

                t = procedureReturn.First() ?? 0m; return new JsonResult { Data = JsonConvert.SerializeObject(t, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }), JsonRequestBehavior = JsonRequestBehavior.AllowGet };
            }
            catch (Exception e)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, e.Message);
            }
        }

        //shrinkwrapping
        public ActionResult ShrinkWrappingFormula(string recordtype, int? itemRecordId, int? itemServiceId)
        {
            if (String.IsNullOrEmpty(recordtype) || !itemRecordId.HasValue || !itemServiceId.HasValue)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Invalid parameter configuration. Please inspect");

            try
            {
                ObjectResult<decimal?> procedureReturn;
                decimal t = 0m;

                switch (recordtype.ToUpper())
                {
                    case "QUOTE":
                        procedureReturn = db.Quote_ShrinkwrappingTime(itemServiceId);
                        break;

                    case "JOB":
                        procedureReturn = db.Job_ShrinkwrappingTime(itemServiceId);
                        break;

                    default:
                        return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, "No record type specified");
                }

                t = procedureReturn.First() ?? 0m; return new JsonResult { Data = JsonConvert.SerializeObject(t, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }), JsonRequestBehavior = JsonRequestBehavior.AllowGet };
            }
            catch (Exception e)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, e.Message);
            }
        }

        //bulk pack
        public ActionResult BulkPackingFormula(string recordtype, int? itemRecordId, int? itemServiceId)
        {
            if (String.IsNullOrEmpty(recordtype) || !itemRecordId.HasValue || !itemServiceId.HasValue)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Invalid parameter configuration. Please inspect");

            try
            {
                ObjectResult<decimal?> procedureReturn;
                decimal t = 0m;

                switch (recordtype.ToUpper())
                {
                    case "QUOTE":
                        procedureReturn = db.Quote_BulkPackingTime(itemServiceId);
                        break;

                    case "JOB":
                        procedureReturn = db.Job_BulkPackingTime(itemServiceId);
                        break;

                    default:
                        return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, "No record type specified");
                }

                t = procedureReturn.First() ?? 0m; return new JsonResult { Data = JsonConvert.SerializeObject(t, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }), JsonRequestBehavior = JsonRequestBehavior.AllowGet };
            }
            catch (Exception e)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, e.Message);
            }
        }

        //kit pack
        public ActionResult KitPackingFormula(string recordtype, int? itemRecordId, int? itemServiceId)
        {
            if (String.IsNullOrEmpty(recordtype) || !itemRecordId.HasValue || !itemServiceId.HasValue)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Invalid parameter configuration. Please inspect");

            try
            {
                ObjectResult<decimal?> procedureReturn;
                decimal t = 0m;

                switch (recordtype.ToUpper())
                {
                    case "QUOTE":
                        procedureReturn = db.Quote_KitPackingTime(itemServiceId);
                        break;

                    case "JOB":
                        procedureReturn = db.Job_KitPackingTime(itemServiceId);
                        break;

                    default:
                        return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, "No record type specified");
                }

                t = procedureReturn.First() ?? 0m;
                return new JsonResult { Data = JsonConvert.SerializeObject(t, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }), JsonRequestBehavior = JsonRequestBehavior.AllowGet };
            }
            catch (Exception e)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, e.Message);
            }
        }
    }
}