using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.QA
{
    public class QA
    {
        public string Question { get; set; }
        [JsonProperty("Response")]
        public bool Response { get; set; }
        public string Notes { get; set; }
    }
}