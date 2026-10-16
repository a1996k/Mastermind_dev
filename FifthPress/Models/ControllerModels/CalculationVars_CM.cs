using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ControllerModels
{
    public class CalculationVars_CM
    {
        public string ItemLength { get; set; }
        public string ItemWidth { get; set; }
        public int Quantity { get; set; }
        public int PressId { get; set; }
        public int MaterialId { get; set; }
        public string RunSizeLength { get; set; }
        public string RunSizeWidth { get; set; }
        public int Ordinal { get; set; }
        public int PricingRecordId { get; set; }
        public string PrintSpeed { get; set; }
        public int PrintSpeedUnit { get; set; }
        /*public int[] ServiceIdValues { get; set; }*/
        public string RecordType { get; set; }
        public int ItemId { get; set; }
        public Nullable<int> UpValue { get; set; }
    }
}