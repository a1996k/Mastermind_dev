using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ControllerModels
{
    public class Search_CM
    {
        public string SearchTerm { get; set; }
        public string Client { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool ByCreatedAt { get; set; }
    }
}