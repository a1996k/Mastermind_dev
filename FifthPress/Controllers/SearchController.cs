using FifthPress.ActionFilters;
using FifthPress.Models.ControllerModels;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using FifthPress.Models;
using Newtonsoft.Json;
using System.Data.Entity.Core.Common.CommandTrees;
using System.Data.Entity;
using System.Threading.Tasks;
using FifthPress.Models.ViewModels;
using System;
using FifthPress.Models.ViewModels2;

namespace FifthPress.Controllers
{
    [CookieFilter(Order = 1)]
    [UserPermissions(Order = 2)]
    public class SearchController : DataController
    {
        public async Task<ActionResult> PurchaseOrderSearchIndex()
        {
            VM_PurchaseOrderSearch vm = new VM_PurchaseOrderSearch
            {
                VendorSelect = new VM_MultiselectModel2
                {
                    SelectList = await GenerateSelectListAsync(SelectListType.Vendors),
                    SelectName = "Vendors"
                },
                UserSelect = new VM_MultiselectModel2
                {
                    SelectList = await FilterByUserFlagAsync(await GenerateSelectListAsync(SelectListType.Users), UserFlags.IsProjectManager),
                    SelectName = "Users"
                }
            };
            foreach (SelectListItem sli in vm.VendorSelect.SelectList.ToList())
                sli.Selected = true;
            foreach (SelectListItem sli in vm.UserSelect.SelectList.ToList())
                sli.Selected = true;

            //add Wendy Tenpas (hotfix; need to update users or expand generate select list to reference po pivot)
            //id:24
            vm.UserSelect.SelectList = vm.UserSelect.SelectList.Append(new SelectListItem
            {
                Value = "24",
                Text = "Wendy TenPas",
                Selected = true
            });

            return View(vm);
        }

        public async Task<ActionResult> PurchaseOrderSearch(PurchaseOrderSearch_CM searchModel)
        {
            try
            {
                List<sch_PurchaseOrders_Result> searchResults = await db.sch_PurchaseOrders(
                    searchModel.PONumber,
                    searchModel.Vendor,
                    searchModel.Status,
                    searchModel.CreatedBy,
                    searchModel.DateFrom,
                    searchModel.DateTo,
                    searchModel.Keywords,
                    searchModel.DueFrom, // new
                    searchModel.DueTo   // new
                ).ToListAsync();

                foreach (var r in searchResults)
                {
                    r.ApproverName = r.ApproverName ?? "";
                    r.RequestorName = r.RequestorName ?? "";
                    r.VendorName = r.VendorName ?? "";
                    r.StatusLabel = r.StatusLabel ?? "";
                    r.ItemTitle = r.ItemTitle ?? "";
                    r.TotalPrice = r.TotalPrice ?? 0;
                    r.JobNumber = r.JobNumber ?? "";
                    r.FormattedPONumber = r.FormattedPONumber ?? "";

                    r.ApprovalDateString = (r.ApprovalDate.HasValue && r.ApprovalDate.Value != DateTime.MinValue)
                        ? r.ApprovalDate.Value.ToLongDateString()
                        : "";
                }

                return new JsonResult
                {
                    Data = JsonConvert.SerializeObject(searchResults, new JsonSerializerSettings
                    {
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                    }),
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public async Task<ActionResult> RecordSearch4(Search3_CM searchModel)
        {
            try
            {
                List<RecordSearch4_Result> searchResults = await db.RecordSearch4(
                    searchModel.SearchJobs,
                    searchModel.EnteredToday,
                    searchModel.WaitingOnArt,
                    searchModel.InProgress,
                    searchModel.ReadyToShip,
                    searchModel.AtPrebill,
                    searchModel.ReadyToBill,
                    searchModel.IsHistory,
                    searchModel.CreatedAtDateType,
                    searchModel.JobNumber,
                    searchModel.Client,
                    searchModel.AssignedTo,
                    searchModel.DateFrom,
                    searchModel.DateTo,
                    searchModel.Length,
                    searchModel.Width,
                    searchModel.Keywords)
                    .ToListAsync();

                foreach (RecordSearch4_Result r in searchResults)
                {
                    r.ClientTitle = r.ClientTitle ?? "";
                    r.CreatedString = (r.CreatedAt.HasValue && r.CreatedAt.Value != DateTime.MinValue) ? r.CreatedAt.Value.ToLongDateString() : "";
                    //important note: Complete is being returned from sql as a string, which coalesces into min value. check against min value
                    if (r.Complete.HasValue)
                    {
                        if (r.Complete.Value == RecordSearch4_Result.NoCreatedDate)
                        {
                            r.CompletedString = "";
                        }
                        else
                        {
                            r.CompletedString = r.Complete.Value.ToLongDateString();
                        }
                    }
                    else
                    {
                        r.CompletedString = "";
                    }
                    r.Dimensions = String.Format("{0} L x {1} W", r.Length, r.Width);
                    r.TotalQuantity = r.TotalQuantity ?? 0;
                    r.TotalPriceString = (r.TotalPrice ?? 0m).ToString("C");
                }

                // Flag cancelled jobs so the results list can indicate them.
                // Only jobs can be cancelled (Jobs.Cancelled); quotes are skipped.
                if (searchModel.SearchJobs)
                {
                    List<int> jobIds = searchResults
                        .Where(x => x.ParentId.HasValue)
                        .Select(x => x.ParentId.Value)
                        .Distinct()
                        .ToList();

                    HashSet<int> cancelledJobIds = new HashSet<int>(
                        await db.Jobs
                            .Where(j => jobIds.Contains(j.Id) && j.Cancelled != null)
                            .Select(j => j.Id)
                            .ToListAsync());

                    foreach (RecordSearch4_Result r in searchResults)
                    {
                        r.IsCancelled = r.ParentId.HasValue && cancelledJobIds.Contains(r.ParentId.Value);
                    }
                }

                return new JsonResult
                {
                    Data = JsonConvert.SerializeObject(searchResults, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                    MaxJsonLength = int.MaxValue
                };
            }
            catch (Exception e)
            {
                return null;
            }
        }

        public ActionResult RecordSearch(Search_CM searchModel)
        {
            List<RecordSearch2_Result> results =
                db.RecordSearch2
                (
                    searchModel.SearchTerm ?? "",
                    searchModel.Client ?? "",
                    searchModel.StartDate,
                    searchModel.EndDate,
                    searchModel.ByCreatedAt
                ).ToList();

            foreach (RecordSearch2_Result rs2 in results)
            {
                rs2.CreatedAtString =
                    rs2.CreatedAt.HasValue ? rs2.CreatedAt.Value.ToShortDateString() :
                    "";
                rs2.CompletedString =
                    rs2.Complete.HasValue ? rs2.Complete.Value.ToShortDateString() :
                    "";

                //trim long strings
                rs2.InternalNotes = (rs2.InternalNotes ?? "").TrimLongString(200);
                rs2.Notes = (rs2.Notes ?? "").TrimLongString(200);
                rs2.Description = (rs2.Description ?? "").TrimLongString(200);

                //legacy record null client handler
                rs2.ClientTitle = (rs2.ClientTitle ?? "");
            }

            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(results, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        // GET: Index
        public async Task<ActionResult> Index()
        {
            VM_RecordSearch3 vm = new VM_RecordSearch3
            {
                ClientSelect = new VM_MultiselectModel2
                {
                    SelectList = await GenerateSelectListAsync(SelectListType.Clients),
                    SelectName = "Clients"
                },
                UserSelect = new VM_MultiselectModel2
                {
                    SelectList = await FilterByUserFlagAsync(await GenerateSelectListAsync(SelectListType.Users), UserFlags.IsProjectManager),
                    SelectName = "Users"
                }
            };

            foreach (SelectListItem sli in vm.ClientSelect.SelectList.ToList())
                sli.Selected = true;
            foreach (SelectListItem sli in vm.UserSelect.SelectList.ToList())
                sli.Selected = true;

            return View(vm);
        }
    }
}