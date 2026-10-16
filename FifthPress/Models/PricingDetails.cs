using FifthPress.Controllers;
using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Metadata.Edm;
using System.Linq;
using System.Web;

namespace FifthPress.Models
{
    public class PricingDetails
    {
        public int Up { get; set; }
        public decimal InkCost { get; set; }
        public decimal InkPrice { get; set; }
        public decimal InkMarkup { get; set; }
        public string InkMarkupFormatted { get; set; }
        public string InkPriceFormatted { get; set; }
        public string InkCostFormatted { get; set; }
        public string InkCostDescriptor { get; set; }
        public string InkCostId { get; set; }
        public decimal MaterialCost { get; set; }
        public decimal MaterialPrice { get; set; }
        public decimal MaterialMarkup { get; set; }
        public string MaterialMarkupFormatted { get; set; }
        public string MaterialPriceFormatted { get; set; }
        public string MaterialCostFormatted { get; set; }
        public string MaterialCostDescriptor { get; set; }
        public string MaterialCostId { get; set; }
        public int MaterialSheetCount { get; set; }
        public string MaterialSheetCountFormatted { get; set; }
        public List<decimal> ServiceCosts { get; set; }
        public List<decimal> ServicePrices { get; set; }
        public List<string> ServicePricesFormatted { get; set; }
        public List<string> ServiceCostsFormatted { get; set; }
        public List<string> ServiceCostDescriptors { get; set; }
        public List<int> ServiceOrdinals { get; set; }
        public List<int> ApexServiceOrdinals { get; set; }
        public List<string> ServiceCostIds { get; set; }
        public List<string> ServiceTimesFormatted { get; set; }
        public List<string> FinishingMaterialDescriptors { get; set; }
        public List<string> FinishingMaterialCostIds { get; set; }
        public List<decimal> FinishingMaterialCosts { get; set; }
        public List<string> FinishingMaterialCostsFormatted { get; set; }
        public List<string> FinishingMaterialPricesFormatted { get; set; }
        public List<decimal> FinishingMaterialPrices { get; set; }
        public List<decimal> FinishingMaterialMarkups { get; set; }
        public List<string> FinishingMaterialMarkupsFormatted { get; set; }
        public List<string> FinishingMaterialCounts { get; set; } //like 4, 10, 5, etc.
        public List<string> FinishingMaterialCountsFormatted { get; set; } //like 4 sheets, 10 feet, 5 boxes, etc.
        public List<int> FinishingMaterialRecordIds { get; set; }
        public List<decimal> ServiceMarkups { get; set; }
        public List<string> ServiceMarkupsFormatted { get; set; }
        public List<int> ServiceRecordIds { get; set; }
        public decimal TotalServiceCost { get; set; }
        public decimal TotalServicePrice { get; set; }
        public string TotalServicePriceFormatted { get; set; }
        public string TotalServiceCostFormatted { get; set; }
        public int Ordinal { get; set; }
        public decimal TotalCost { get; set; }
        public decimal TotalPrice { get; set; }
        public string TotalPriceFormatted { get; set; }
        public string TotalCostFormatted { get; set; }
        public decimal TotalPrintingTime { get; set; }
        public string TotalPrintingTimeFormatted { get; set; }
        public decimal TotalTime { get; set; }
        public string TotalTimeFormatted { get; set; }
        public int PricingRecordId { get; set; }
        public decimal PrintLaborCost { get; set; }
        public decimal PrintLaborPrice { get; set; }
        public decimal PrintLaborMarkup { get; set; }
        public string PrintLaborMarkupFormatted { get; set; }
        public string PrintLaborPriceFormatted { get; set; }
        public string PrintLaborCostFormatted { get; set; }
        public string PrintLaborCostDescriptor { get; set; }
        public int TotalSheets { get; set; }
        public decimal TotalFeet { get; set; }
        public decimal TotalRunSizeLength { get; set; }
        public decimal TotalRunSizeWidth { get; set; }
        public int RowId { get; set; }
        public decimal TotalItemMarkup { get; set; }
        public string TotalItemMarkupFormatted { get; set; }
    }
}