using FifthPress.Controllers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace FifthPress.Models.Calculations
{
    public class RowColumnInformation
    {
        public int RowCount { get; set; }
        public int ColumnCount { get; set; }
        public int TotalSheets { get; set; }
        public int TotalColumns { get; set; }
        public int TotalRows { get; set; }
        public int UpValue { get; set; }
        public int RunSheets { get; set; }
        public int Out { get; set; }
    }
}