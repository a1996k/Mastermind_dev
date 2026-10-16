using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_ForecastedJob
    {
        public string JobNumber { get; set; }
        public string Customer { get; set; }
        public string Title { get; set; }
        public string FullDescription { get; set; }
        public string TruncatedDescription { get; set; }
        public DateTime DueDate { get; set; }
        public string DueDateFormatted { get; set; }
        public int Week { get; set; }
        public decimal Balance { get; set; }
        public string BalanceFormatted { get; set; }
        public string CSR { get; set; }
        public Nullable<decimal> Van { get; set; }
        public Nullable<decimal> Ric { get; set; }
        public Nullable<decimal> VLT { get; set; }
        public Nullable<decimal> Hei { get; set; }
        public Nullable<decimal> Rol { get; set; }
        public Nullable<decimal> QME { get; set; }
        public Nullable<decimal> Lam { get; set; }
        public Nullable<decimal> Zun { get; set; }
        public Nullable<decimal> Gul { get; set; }
        public Nullable<decimal> Die { get; set; }
        public Nullable<decimal> Fin { get; set; }
        public Nullable<decimal> Out { get; set; }
        public Nullable<decimal> SPs { get; set; }
        public Nullable<decimal> Tau { get; set; }
        public string Notes { get; set; }
        public Nullable<DateTime> MinStartDate { get; set; }
        public string MinStartDateFormatted { get; set; }
        public decimal DaysRemaining { get; set; }
        public int JobId { get; set; }
        public int JobItemId { get; set; }
        public int JobStatus { get; set; }
        public bool IsOverdue { get; set; }
        public bool IsDueToday { get; set; }
        public bool ScheduleSquash { get; set; }
        public string MainJobDescription { get; set; }
        public string JobStage { get; set; }
        public bool IsCreditHold { get; set; }
    }
}