using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ControllerModels
{
    public class Search3_CM
    {
        /// <summary>
        /// 0 = Quotes, 1 = Jobs
        /// </summary>
        public bool SearchJobs { get; set; }

        /// <summary>
        /// 0 = Do Not Apply, 1 = Apply
        /// </summary>
        public bool EnteredToday { get; set; }

        /// <summary>
        /// 0 = Do Not Apply, 1 = Apply
        /// </summary>
        public bool WaitingOnArt { get; set; }

        /// <summary>
        /// 0 = Do Not Apply, 1 = Apply
        /// </summary>
        public bool InProgress { get; set; }

        /// <summary>
        /// 0 = Do Not Apply, 1 = Apply
        /// </summary>
        public bool ReadyToShip { get; set; }

        /// <summary>
        /// 0 = Do Not Apply, 1 = Apply
        /// </summary>
        public bool AtPrebill { get; set; }

        /// <summary>
        /// 0 = Do Not Apply, 1 = Apply
        /// </summary>
        public bool IsHistory { get; set; }

        /// <summary>
        /// 0 = Do Not Apply, 1 = Apply
        /// </summary>
        public bool ReadyToBill { get; set; }

        /// <summary>
        /// 0 = Completed, 1 = CreatedAt
        /// </summary>
        public bool CreatedAtDateType { get; set; }

        /// <summary>
        /// Optional Job/Quote number
        /// </summary>
        public string JobNumber { get; set; }

        /// <summary>
        /// Partial client name OR list of client IDs (comma-separated)
        /// </summary>
        public string Client { get; set; }

        /// <summary>
        /// Comma-separated list of user IDs (parent-level)
        /// </summary>
        public string AssignedTo { get; set; }

        /// <summary>
        /// Start date (optional)
        /// </summary>
        public DateTime? DateFrom { get; set; }

        /// <summary>
        /// End date (optional)
        /// </summary>
        public DateTime? DateTo { get; set; }

        /// <summary>
        /// Optional length filter
        /// </summary>
        public double? Length { get; set; }

        /// <summary>
        /// Optional width filter
        /// </summary>
        public double? Width { get; set; }

        /// <summary>
        /// Comma-separated search terms
        /// </summary>
        public string Keywords { get; set; }
    }
}