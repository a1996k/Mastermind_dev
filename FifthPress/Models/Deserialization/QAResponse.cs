using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.Deserialization
{
    public class QAResponse
    {
        public string Question { get; set; }
        public bool Response { get; set; }
        public string Notes { get; set; }
    }
}