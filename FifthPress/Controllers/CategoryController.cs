using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Threading.Tasks;
using System.Web.Mvc;
using FifthPress.Models.ViewModels;
using Newtonsoft.Json;
using FifthPress.ActionFilters;
using OfficeOpenXml.Style;
using OfficeOpenXml.Drawing;
using System.Data.Entity;
using FifthPress.Models;

namespace FifthPress.Controllers
{
    [CookieFilter(Order = 1)]
    public class CategoryController : DataController
    {
        public async Task<ActionResult> GetServiceCategoryForEdit(int? id)
        {
            if (!id.HasValue)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, "id not provided");

            ServiceCategory sc = db.ServiceCategories.Find(id);

            if (sc == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.NotFound, ("Cannot find a service category with id = " + id.Value));

            await db.Entry(sc).Reference(c => c.ServiceType1).LoadAsync();

            ViewData["ServiceCategory.ServiceType"] = await GenerateSelectListAsync(SelectListType.ServiceTypes, sc, "ServiceType");

            return new JsonResult { Data = JsonConvert.SerializeObject(RenderViewToString("~/Views/Category/GetServiceCategoryForEdit.cshtml", new VM_ServiceCategory { ServiceCategory = sc })), JsonRequestBehavior = JsonRequestBehavior.AllowGet };
        }

        public async Task<ActionResult> SaveServiceCategory(ServiceCategory theItem)
        {
            if (theItem == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            //using (ConnectToDatabase())
            {
                var sc = await db.ServiceCategories.FindAsync(theItem.Id);

                if (sc == null)
                {
                    db.ServiceCategories.Add(theItem);
                }
                else
                {
                    await db.Entry(sc).Reference(r => r.ServiceType1).LoadAsync();
                    sc = UpdateServiceCategoryRecord(sc, theItem);
                    db.Entry(sc).State = System.Data.Entity.EntityState.Modified;
                }

                await db.SaveChangesAsync();
            }

            return new JsonResult { Data = "done", JsonRequestBehavior = JsonRequestBehavior.AllowGet };
        }

        public async Task<ActionResult> NewServiceCategory()
        {
            //using (ConnectToDatabase())
            {
                ViewData["ServiceCategory.ServiceType"] = await GenerateSelectListAsync(SelectListType.ServiceTypes);
                return new JsonResult { Data = JsonConvert.SerializeObject(RenderViewToString("~/Views/Category/GetServiceCategoryForEdit.cshtml", new VM_ServiceCategory { ServiceCategory = new ServiceCategory() })), JsonRequestBehavior = JsonRequestBehavior.AllowGet };
            }
        }

        public async Task<ActionResult> LoadCategory(int? id, string type)
        {
            if (!id.HasValue || String.IsNullOrEmpty(type))
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, "missing required parameters");

            switch (type.ToUpper())
            {
                case "SERVICE":

                    List<VM_AdminTableItem_Service> sRecords = db.Services.Where(x => x.Deleted == null && x.CatNo == id.Value).Select(y => new VM_AdminTableItem_Service
                    {
                        Id = y.Id,
                        Title = y.Title,
                        Category = y.ServiceCategory.Title
                    }).ToList();

                    var serJson = new
                    {
                        theView = RenderViewToString("~/Views/Partials/_AdminEditItem_Service.cshtml", sRecords),
                        headerView = RenderViewToString("~/Views/Partials/_AdminEditItem_Service_TableHeader.cshtml", null),
                        header = sRecords.First().Category
                    };

                    return new JsonResult
                    {
                        Data = JsonConvert.SerializeObject(serJson, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };

                case "MATERIAL":

                    List<VM_AdminTableItem_Material> mRecords = new List<VM_AdminTableItem_Material>();
                    if (id.Value == -1)
                    {
                        List<Material> mDbRecords = await db.Materials.Where(x => x.Deleted == null && (x.QuickCategory ?? false)).ToListAsync();

                        foreach (Material m in mDbRecords)
                        {
                            await db.Entry(m).Reference(r => r.MaterialCostType).LoadAsync();
                            await db.Entry(m).Reference(r => r.MaterialInventory).LoadAsync();
                            await db.Entry(m).Reference(r => r.MaterialCategory).LoadAsync();
                        }

                        //quick items
                        mRecords = mDbRecords.Select(y => new VM_AdminTableItem_Material
                        {
                            Id = y.Id,
                            Title = y.Title,
                            Size = y.Size,
                            Quantity = y.MaterialInventory.Stock,
                            Allocated = y.MaterialInventory.Allocated ?? 0,
                            Unallocated = (y.MaterialInventory.Stock - (y.MaterialInventory.Allocated ?? 0)),
                            Caliper = (y.Caliper ?? 0).ToString(),
                            Category = y.MaterialCategory.Title,
                            CostPerUnit = y.Cost ?? 0m,
                            CostPerUnitFormatted = (y.Cost ?? 0m).ToString("C"),
                            CostPerUnitUnit = y.MaterialCostType.Title ?? "",
                            EntityName = "Material"
                        }).ToList();
                    }
                    else
                    {
                        List<Material> mDbRecords = await db.Materials.Where(x => x.Deleted == null && x.MaterialCategory_Id == id.Value).ToListAsync();

                        foreach (Material m in mDbRecords)
                        {
                            await db.Entry(m).Reference(r => r.MaterialCostType).LoadAsync();
                            await db.Entry(m).Reference(r => r.MaterialInventory).LoadAsync();
                            await db.Entry(m).Reference(r => r.MaterialCategory).LoadAsync();
                        }

                        mRecords = mDbRecords.Select(y => new VM_AdminTableItem_Material
                        {
                            Id = y.Id,
                            Title = y.Title,
                            Size = y.Size,
                            Quantity = y.MaterialInventory.Stock,
                            Allocated = y.MaterialInventory.Allocated ?? 0,
                            Unallocated = (y.MaterialInventory.Stock - (y.MaterialInventory.Allocated ?? 0)),
                            Caliper = (y.Caliper ?? 0).ToString(),
                            Category = y.MaterialCategory.Title,
                            CostPerUnit = y.Cost ?? 0m,
                            CostPerUnitFormatted = (y.Cost ?? 0m).ToString("C"),
                            CostPerUnitUnit = y.MaterialCostType.Title ?? "",
                            EntityName = "Material"
                        }).ToList();
                    }

                    var matJson = new
                    {
                        theView = RenderViewToString("~/Views/Partials/_AdminEditItem_Material.cshtml", mRecords),
                        headerView = RenderViewToString("~/Views/Partials/_AdminEditItem_Material_TableHeader.cshtml", null),
                        header = mRecords.First().Category
                    };

                    return new JsonResult
                    {
                        Data = JsonConvert.SerializeObject(matJson, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };

                default:
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "In switch default");
            }
        }
    }
}