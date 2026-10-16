using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace FifthPress.Models.ViewModels2
{
    public class VM_TournamentAccuracyFilter
    {
        public List<SelectListItem> Clients { get; set; }
        public int? SelectedClientId { get; set; } // optional
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

}
