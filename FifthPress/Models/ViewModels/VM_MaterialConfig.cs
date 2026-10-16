using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Windows.Media.Converters;

namespace FifthPress.Models.ViewModels
{
    public class VM_MaterialConfig
    {
        public string RunLength { get; set; }
        public string RunWidth { get; set; }
        public int Up { get; set; }
        public string ColorFrontProfile { get; set; }
        public int ColorFrontCount { get; set; }
        public string ColorFrontNotes { get; set; }
        public string ColorBackProfile { get; set; }
        public int ColorBackCount { get; set; }
        public string ColorBackNotes { get; set; }
        public int Sheets { get; set; }
        public int ScrapSheets { get; set; }
        public int RunSheets { get; set; }
        public int BuySheets { get; set; }
        public bool DoubleInk { get; set; }
        public bool Collate { get; set; }
        public bool Staple { get; set; }
        public bool Numbering { get; set; }
        public decimal MaterialCost { get; set; }
        public string MaterialCostType { get; set; }
        public bool UserSheetOverride { get; set; }
        public int Out { get; set; }
    }
}