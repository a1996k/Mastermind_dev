using Newtonsoft.Json;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Web.Mvc;

namespace FifthPress.Models
{
    public class PressMetadata
    {
        [Display(Name = "Id")]
        public int Id { get; set; }

        [Display(Name = "Title")]
        public string Title { get; set; }

        [Display(Name = "Ink Cost")]
        public Nullable<decimal> InkCostNumerator { get; set; }

        [Display(Name = "Unit")]
        public Nullable<decimal> InkCostDenominator { get; set; }

        [Display(Name = "Scrap %")]
        public Nullable<decimal> ScrapLevel { get; set; }

        [Display(Name = "Make Ready")]
        public Nullable<decimal> MakeReady { get; set; }

        [Display(Name = "Color Profile")]
        public Nullable<int> ColorProfile { get; set; }

        [Display(Name = "Speed")]
        public Nullable<int> SpeedNumerator { get; set; }

        [Display(Name = "Unit")]
        public Nullable<int> SpeedDenominator { get; set; }

        [Display(Name = "Deleted")]
        public Nullable<System.DateTime> Deleted { get; set; }

        [Display(Name = "Category")]
        public Nullable<int> Category { get; set; }

        [Display(Name = "Ink Cost Unit")]
        public Nullable<int> InkCostDenominatorUnit { get; set; }

        [Display(Name = "Gutter Top")]
        public string MarginTop { get; set; }

        [Display(Name = "Gutter Left")]
        public string MarginLeft { get; set; }

        [Display(Name = "Gutter Bottom")]
        public string MarginBottom { get; set; }

        [Display(Name = "Gutter Right")]
        public string MarginRight { get; set; }

        [Display(Name = "Tail")]
        public string Tail { get; set; }

        [Display(Name = "Side Guides")]
        public string SideGuides { get; set; }

        [Display(Name = "Gripper")]
        public string Gripper { get; set; }

        [Display(Name = "Scrap Per Color")]
        public Nullable<int> ScrapSheetsPerColor { get; set; }

        [Display(Name = "Scrap Per Job")]
        public Nullable<int> ScrapSheetsPerJob { get; set; }

        [Display(Name = "Hourly Labor Cost")]
        public Nullable<decimal> LaborCostPerHour { get; set; }

        [Display(Name = "Wash Up")]
        public Nullable<decimal> WashUp { get; set; }
    }

    public class MaterialMetadata
    {
        [Display(Name = "Notes")]
        [DataType(DataType.MultilineText)]
        public string Notes;
    }

    public class JobMetadata
    {
        [Display(Name = "Due Date")]
        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy}", ApplyFormatInEditMode = true)]
        public DateTime DueDate;

        [Display(Name = "Min. Start Date")]
        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy}", ApplyFormatInEditMode = true)]
        public DateTime MinStartDate;

        [Display(Name = "Notes")]
        [DataType(DataType.MultilineText)]
        public string Notes;

        [Display(Name = "Description")]
        [DataType(DataType.MultilineText)]
        public string Description;

        [Display(Name = "Internal Notes")]
        [DataType(DataType.MultilineText)]
        public string InternalNotes;

        [Display(Name = "Billing Notes")]
        [DataType(DataType.MultilineText)]
        public string BillingNotes;

        [Display(Name = "Job #")]
        [DataType(DataType.Text)]
        public string JobNumber;

        [Display(Name = "Squash into one row on job schedule")]
        public Nullable<bool> ScheduleSquash;
    }

    public class QuoteMetadata
    {
        [Display(Name = "Notes")]
        [DataType(DataType.MultilineText)]
        public string Notes;

        [Display(Name = "Description")]
        [DataType(DataType.MultilineText)]
        public string Description;

        [Display(Name = "Internal Notes")]
        [DataType(DataType.MultilineText)]
        public string InternalNotes;

        [Display(Name = "Billing Notes")]
        [DataType(DataType.MultilineText)]
        public string BillingNotes;

        [Display(Name = "Quote #")]
        [DataType(DataType.Text)]
        public string JobNumber;
    }

    public class ServiceMetadata
    {
        public int Id { get; set; }

        [Display(Name = "#")]
        public Nullable<int> SerNo { get; set; }

        [Display(Name = "Title")]
        public string Title { get; set; }

        [Display(Name = "Caliper")]
        public Nullable<float> Caliper { get; set; }

        [Display(Name = "Operator")]
        public Nullable<int> Operator { get; set; }

        [Display(Name = "Deleted")]
        public Nullable<System.DateTime> Deleted { get; set; }

        [Display(Name = "Hourly Rate")]
        [DataType(DataType.Currency)]
        [DisplayFormat(DataFormatString = "{0:C}")]
        public Nullable<decimal> BasePrice { get; set; }
    }

    public class QuoteItemMetadata
    {
        [AdditionalMetadata("Constrainable", "QuoteItem.ParsedLength")]
        public string ParsedLength { get; set; }

        [AdditionalMetadata("Constrainable", "QuoteItem.ParsedWidth")]
        public string ParsedWidth { get; set; }

        [AdditionalMetadata("Constrainable", "QuoteItem.PressColorCountFront")]
        public Nullable<int> PressColorCountFront { get; set; }

        [AdditionalMetadata("Constrainable", "QuoteItem.PressColorCountBack")]
        public Nullable<int> PressColorCountBack { get; set; }

        [AdditionalMetadata("Constrainable", "QuoteItem.Gripper")]
        public string Gripper { get; set; }

        [AdditionalMetadata("Constrainable", "QuoteItem.SideGuides")]
        public string SideGuides { get; set; }

        [AdditionalMetadata("Constrainable", "QuoteItem.Tail")]
        public string Tail { get; set; }

        [Display(Name = "Gutter Left")]
        public string MarginXL { get; set; }

        [Display(Name = "Gutter Right")]
        public string MarginXR { get; set; }

        [Display(Name = "Gutter Top")]
        public string MarginYT { get; set; }

        [Display(Name = "Gutter Bottom")]
        public string MarginYB { get; set; }

        [Display(Name = "Internal Notes")]
        [DataType(DataType.MultilineText)]
        public string InternalNotes;
    }

    public class JobItemMetadata
    {
        [AdditionalMetadata("Constrainable", "QuoteItem.ParsedLength")]
        public string ParsedLength { get; set; }

        [AdditionalMetadata("Constrainable", "QuoteItem.ParsedWidth")]
        public string ParsedWidth { get; set; }

        [AdditionalMetadata("Constrainable", "QuoteItem.PressColorCountFront")]
        public Nullable<int> PressColorCountFront { get; set; }

        [AdditionalMetadata("Constrainable", "QuoteItem.PressColorCountBack")]
        public Nullable<int> PressColorCountBack { get; set; }

        [AdditionalMetadata("Constrainable", "QuoteItem.Gripper")]
        public string Gripper { get; set; }

        [AdditionalMetadata("Constrainable", "QuoteItem.SideGuides")]
        public string SideGuides { get; set; }

        [AdditionalMetadata("Constrainable", "QuoteItem.Tail")]
        public string Tail { get; set; }

        [Display(Name = "Gutter Left")]
        public string MarginXL { get; set; }

        [Display(Name = "Gutter Right")]
        public string MarginXR { get; set; }

        [Display(Name = "Gutter Top")]
        public string MarginYT { get; set; }

        [Display(Name = "Gutter Bottom")]
        public string MarginYB { get; set; }

        [Display(Name = "Internal Notes")]
        [DataType(DataType.MultilineText)]
        public string InternalNotes;
    }

    public class JobItemServiceMetadata
    {
        public string ServiceConfiguration { get; set; }
        public decimal? ServiceTime { get; set; }

        [Display(Name = "Hourly Rate")]
        public Nullable<decimal> HourlyRate { get; set; }

        [Display(Name = "General Cost")]
        public Nullable<decimal> MaterialCost { get; set; }
    }

    public class QuoteItemServiceMetadata
    {
        public string ServiceConfiguration { get; set; }
        public decimal? ServiceTime { get; set; }

        [Display(Name = "Hourly Rate")]
        public Nullable<decimal> HourlyRate { get; set; }

        [Display(Name = "General Cost")]
        public Nullable<decimal> MaterialCost { get; set; }
    }

    public class PO_OrdersMetadata
    {
        [DataType(DataType.MultilineText)]
        public string ShipToAddress { get; set; }

        [DataType(DataType.MultilineText)]
        public string Notes { get; set; }
    }

    public class VendorMetadata
    {
        [DataType(DataType.MultilineText)]
        public string Notes { get; set; }
    }
}