using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using FifthPress.Models;
using System.Threading.Tasks;
using System.Data.Entity;
using FifthPress.Models.ViewModels;
using Newtonsoft.Json;
using FifthPress.ActionFilters;
using Newtonsoft.Json.Linq;

namespace FifthPress.Controllers
{
    public class PublicController : DataController
    {
        public async Task<ActionResult> ProductionSchedule(bool tv = false, string filter = null)
        {
            //add cookie to indicate this is a tv
            //this is important so that the error handler knows where to redirect the user to (tvs -> /tv otherwise root)
            // mark this browser as a TV client
            var tvCookie = new HttpCookie("IsTvClient", "true")
            {
                HttpOnly = true,
                Path = "/"           // so it applies to every URL on your site
            };
            Response.Cookies.Add(tvCookie);

            ViewData["TvMode"] = tv;
            // Capitalize first letter of filter to match view logic (Vanguard/Zund)
            if (!string.IsNullOrEmpty(filter))
            {
                filter = char.ToUpper(filter[0]) + filter.Substring(1).ToLower();
            }
            ViewData["Filter"] = tv ? filter : null;
            VM_LiveJobTable vm = new VM_LiveJobTable();

            vm.Jobs = await db.LiveJobStats.Select(x => new VM_ForecastedJob
            {
                JobNumber = x.JobNumber,
                Customer = x.Client ?? "",
                Title = x.Title ?? "",
                FullDescription = x.Description,
                MainJobDescription = x.MainJobDescription ?? "",
                DueDate = x.DueDate ?? DateTime.MinValue,
                Week = x.Week ?? -1,
                Balance = x.Balance ?? 0m,
                CSR = x.CSR ?? "",
                Van = x.VanguardTime,
                Ric = x.RicohTime,
                VLT = x.VinylLetteringTime,
                Hei = x.HeidelbergTime,
                Rol = x.RolandTime,
                QME = x.QMTime,
                Lam = x.LaminatorTime,
                Zun = x.ZundTime,
                Gul = x.GuillotineTime,
                Die = x.DieCutterTime,
                Fin = x.FinishingTime,
                Out = x.Out,
                Tau = x.TauroTime,
                Notes = x.Notes ?? "",
                MinStartDate = x.MinStartDate,
                JobId = x.JobId ?? 0,
                JobItemId = x.JobItemId ?? 0,
                ScheduleSquash = x.Squash ?? false,
                JobStage = x.JobStage ?? ""
            }).ToListAsync();

            List<int> jobIds = vm.Jobs.Select(x => x.JobId).ToList();
            List<Job> applicableJobs = await db.Jobs.AsNoTracking()
                .Include(j => j.Client1)
                .Where(x => jobIds.Contains(x.Id)).ToListAsync();

            // Group jobs by JobId for potential squashing
            var groupedJobs = vm.Jobs.GroupBy(j => j.JobId);

            List<VM_ForecastedJob> finalJobs = new List<VM_ForecastedJob>();

            foreach (var jobGroup in groupedJobs)
            {
                bool shouldSquash = jobGroup.Any(j => j.ScheduleSquash);

                if (!shouldSquash)
                {
                    // Just add all items as is
                    finalJobs.AddRange(jobGroup);
                }
                else
                {
                    // Squash into a single aggregated row
                    var firstItem = jobGroup.First();

                    decimal totalVan = jobGroup.Sum(x => x.Van ?? 0m);
                    decimal totalRic = jobGroup.Sum(x => x.Ric ?? 0m);
                    decimal totalVLT = jobGroup.Sum(x => x.VLT ?? 0m);
                    decimal totalHei = jobGroup.Sum(x => x.Hei ?? 0m);
                    decimal totalRol = jobGroup.Sum(x => x.Rol ?? 0m);
                    decimal totalQME = jobGroup.Sum(x => x.QME ?? 0m);
                    decimal totalLam = jobGroup.Sum(x => x.Lam ?? 0m);
                    decimal totalZun = jobGroup.Sum(x => x.Zun ?? 0m);
                    decimal totalGul = jobGroup.Sum(x => x.Gul ?? 0m);
                    decimal totalDie = jobGroup.Sum(x => x.Die ?? 0m);
                    decimal totalFin = jobGroup.Sum(x => x.Fin ?? 0m);
                    decimal totalOut = jobGroup.Sum(x => x.Out ?? 0m);
                    decimal totalTau = jobGroup.Sum(x => x.Tau ?? 0m);

                    DateTime? earliestMinStartDate = jobGroup
                        .Where(x => x.MinStartDate.HasValue)
                        .Select(x => x.MinStartDate.Value)
                        .DefaultIfEmpty()
                        .Min();

                    string baseJobNumber = firstItem.JobNumber;
                    int dotIndex = baseJobNumber.LastIndexOf('.');
                    if (dotIndex > -1)
                    {
                        baseJobNumber = baseJobNumber.Substring(0, dotIndex);
                    }

                    VM_ForecastedJob aggregated = new VM_ForecastedJob
                    {
                        JobId = firstItem.JobId,
                        JobItemId = firstItem.JobItemId,
                        JobNumber = baseJobNumber,
                        Customer = firstItem.Customer,
                        Title = firstItem.Title,
                        FullDescription = firstItem.MainJobDescription,
                        MainJobDescription = firstItem.MainJobDescription,
                        DueDate = firstItem.DueDate,
                        Week = firstItem.Week,
                        Balance = jobGroup.Sum(x => x.Balance),
                        CSR = firstItem.CSR,

                        // If total == 0, assign null
                        Van = totalVan == 0 ? (decimal?)null : totalVan,
                        Ric = totalRic == 0 ? (decimal?)null : totalRic,
                        VLT = totalVLT == 0 ? (decimal?)null : totalVLT,
                        Hei = totalHei == 0 ? (decimal?)null : totalHei,
                        Rol = totalRol == 0 ? (decimal?)null : totalRol,
                        QME = totalQME == 0 ? (decimal?)null : totalQME,
                        Lam = totalLam == 0 ? (decimal?)null : totalLam,
                        Zun = totalZun == 0 ? (decimal?)null : totalZun,
                        Gul = totalGul == 0 ? (decimal?)null : totalGul,
                        Die = totalDie == 0 ? (decimal?)null : totalDie,
                        Fin = totalFin == 0 ? (decimal?)null : totalFin,
                        Out = totalOut == 0 ? (decimal?)null : totalOut,
                        Tau = totalTau == 0 ? (decimal?)null : totalTau,

                        Notes = firstItem.Notes,
                        MinStartDate = earliestMinStartDate,
                        ScheduleSquash = true,
                        JobStage = jobGroup.Any(x => x.JobStage == "Production") ? "Production" : jobGroup.First().JobStage
                    };

                    finalJobs.Add(aggregated);
                }
            }

            // Replace vm.Jobs with the new list
            vm.Jobs = finalJobs;

            // Continue with formatting and status assignment
            foreach (VM_ForecastedJob v in vm.Jobs)
            {
                v.DueDateFormatted = v.DueDate.ToString("M/d/yyyy");
                v.BalanceFormatted = v.Balance.ToString("C");
                v.MinStartDateFormatted = v.MinStartDate.HasValue ? v.MinStartDate.Value.ToString("M/d/yyyy") : "";
                v.TruncatedDescription = TruncateString(v.FullDescription);

                Job j = applicableJobs.SingleOrDefault(x => x.Id == v.JobId);
                if (j != null)
                {
                    v.JobStatus = j.JobStatus_Id;
                    v.IsCreditHold = j.Client1 != null && (j.Client1.CreditHold == true);
                }

                if (v.MinStartDate.HasValue)
                {
                    v.DaysRemaining = (decimal)(v.DueDate - DateTime.Now).TotalDays + 1m;
                    v.IsOverdue = v.DaysRemaining < 0m;
                    v.IsDueToday = v.DaysRemaining >= 0m && v.DaysRemaining <= 1m;
                }
            }

            v_ProductionDepartmentAggregates agg = await db.v_ProductionDepartmentAggregates.FirstOrDefaultAsync();

            vm.DepartmentTotals = new List<VM_DepartmentTotal>();
            if (agg != null)
            {
                vm.DepartmentTotals = new List<VM_DepartmentTotal>
                {
                    new VM_DepartmentTotal { Department = "Van", DecimalHours = agg.Van },
                    new VM_DepartmentTotal { Department = "Ric", DecimalHours = agg.Ric },
                    new VM_DepartmentTotal { Department = "VLT", DecimalHours = agg.VLT },
                    new VM_DepartmentTotal { Department = "Lam", DecimalHours = agg.Lam },
                    new VM_DepartmentTotal { Department = "Zun", DecimalHours = agg.Zun },
                    new VM_DepartmentTotal { Department = "Gul", DecimalHours = agg.Gul },
                    new VM_DepartmentTotal { Department = "Die", DecimalHours = agg.Die },
                    new VM_DepartmentTotal { Department = "Fin", DecimalHours = agg.Fin },
                    new VM_DepartmentTotal { Department = "Out", DecimalHours = agg.Out },
                    new VM_DepartmentTotal { Department = "Tau", DecimalHours = agg.Tau }
                };
            }

            return View("~/Views/Home/Index_New.cshtml", vm);
        }
    }
}