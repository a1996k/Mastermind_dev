using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json.Serialization;
using System.Web;

namespace FifthPress.Models.ServiceParameterModels
{
    public class LinearCuttingConfig
    {
        [JsonPropertyName("Seconds_Per_Cut")]
        [Display(Name = "Seconds Per Cut")]
        public string Seconds_Per_Cut { get; set; }

        [JsonPropertyName("Lift_Height_Inches")]
        [Display(Name = "Lift Height (Inches)")]
        public string Lift_Height_Inches { get; set; }
    }

    public class ZundCuttingConfig
    {
        [JsonPropertyName("Linear")]
        [Display(Name = "Linear")]
        public int Linear { get; set; }

        [JsonPropertyName("Knife_Speed")]
        [Display(Name = "Knife Speed")]
        public int Knife_Speed { get; set; }
    }

    public class ThreeHolePunchConfig
    {
        [JsonPropertyName("Sheets_Per_Hour")]
        [Display(Name = "Sheets Per Hour")]
        public int Sheets_Per_Hour { get; set; }
    }

    public class PressPlatesConfig
    {
        [JsonPropertyName("Make_Ready")]
        [Display(Name = "Make Ready")]
        public int Make_Ready { get; set; }

        [JsonPropertyName("Wash_Up")]
        [Display(Name = "Wash Up")]
        public int Wash_Up { get; set; }

        [JsonPropertyName("Make_Ready_Per_Plate")]
        [Display(Name = "Make Ready Per Plate")]
        public int Make_Ready_Per_Plate { get; set; }

        [JsonPropertyName("Wash_Up_Per_Plate")]
        [Display(Name = "Wash Up Per Plate")]
        public int Wash_Up_Per_Plate { get; set; }

        [JsonPropertyName("Material_Cost")]
        [Display(Name = "Material Cost")]
        public decimal Material_Cost { get; set; }
    }

    public class VariableHolePunchConfig
    {
        [JsonPropertyName("Sheets_Per_Hour")]
        [Display(Name = "Sheets Per Hour")]
        public int Sheets_Per_Hour { get; set; }

        [JsonPropertyName("Holes_Per_Sheet")]
        [Display(Name = "Holes Per Sheet")]
        public int Holes_Per_Sheet { get; set; }
    }

    public class FoldingTimeConfig
    {
        [JsonPropertyName("Folds_Per_Sheet")]
        [Display(Name = "Folds Per Sheet")]
        public int Folds_Per_Sheet { get; set; }

        [JsonPropertyName("Folds_Per_Hour")]
        [Display(Name = "Folds Per Hour")]
        public int Folds_Per_Hour { get; set; }
    }

    public class ScoringTimeConfig
    {
        [JsonPropertyName("Scores_Per_Piece")]
        [Display(Name = "Scores Per Piece")]
        public int Scores_Per_Piece { get; set; }

        [JsonPropertyName("Scores_Per_Hour")]
        [Display(Name = "Scores Per Hour")]
        public int Scores_Per_Hour { get; set; }
    }

    public class PolybaggingConfig
    {
        [JsonPropertyName("Items_Per_Bag")]
        [Display(Name = "Items Per Bag")]
        public int Items_Per_Bag { get; set; }

        [JsonPropertyName("Seconds_Per_Set")]
        [Display(Name = "Seconds Per Set")]
        public int Seconds_Per_Set { get; set; }
    }

    public class ShrinkWrappingConfig
    {
        [JsonPropertyName("Items_Per_Set")]
        [Display(Name = "Items Per Set")]
        public int Items_Per_Set { get; set; }

        [JsonPropertyName("Seconds_Per_Set")]
        [Display(Name = "Seconds Per Set")]
        public int Seconds_Per_Set { get; set; }
    }

    public class BulkPackConfig
    {
        [JsonPropertyName("SqFt_Per_Hour")]
        public int SqFt_Per_Hour { get; set; }
    }

    public class KitPackConfig
    {
        [JsonPropertyName("Number_Per_Kit")]
        [Display(Name = "Number Per Kit")]
        public int Number_Per_Kit { get; set; }

        [JsonPropertyName("Seconds_Per_Kit")]
        [Display(Name = "Seconds Per Kit")]
        public string Seconds_Per_Kit { get; set; }
    }

    public class PreflightConfig
    {
        [JsonPropertyName("Versions")]
        [Display(Name = "Versions")]
        public int Versions { get; set; }
    }

    public class PerforateConfig
    {
        [JsonPropertyName("Scrap_Sheets")]
        [Display(Name = "Scrap Sheets")]
        public int Scrap_Sheets { get; set; }

        [JsonPropertyName("Scrap_Percent")]
        [Display(Name = "Scrap Percent")]
        public int Scrap_Percent { get; set; }

        [JsonPropertyName("Sheets_Per_Hour")]
        [Display(Name = "Sheets Per Hour")]
        public int Sheets_Per_Hour { get; set; }
    }

    public class CustomCartonConfig
    {
        [JsonPropertyName("Cost_Per_Carton")]
        [Display(Name = "Cost Per Carton")]
        public decimal Cost_Per_Carton { get; set; }

        [JsonPropertyName("Minutes_Per_Carton")]
        [Display(Name = "Minutes Per Carton")]
        public string Minutes_Per_Carton { get; set; }
    }

    public class GrommetConfig
    {
        [JsonPropertyName("Grommets_Per_Item")]
        [Display(Name = "Grommets Per Item")]
        public decimal Grommets_Per_Item { get; set; }

        [JsonPropertyName("Cost_Per_Grommet")]
        [Display(Name = "Cost Per Grommet")]
        public string Cost_Per_Grommet { get; set; }
    }

    public class DieCutConfig
    {
        [JsonPropertyName("Sheets_Per_Hour")]
        [Display(Name = "Sheets Per Hour")]
        public int SheetsPerHour { get; set; }
    }

    public class ScrappingConfig
    {
        [JsonPropertyName("Pieces_Per_Hour")]
        [Display(Name = "Pieces Per Hour")]
        public int PiecesPerHour { get; set; }
    }

    public class LaminateConfig
    {
        [JsonPropertyName("Feet_Per_Hour")]
        [Display(Name = "Feet Per Hour")]
        public decimal Feet_Per_Hour { get; set; }
    }

    public class GeneralSecondsConfig
    {
        [JsonPropertyName("Seconds_Per_Item")]
        [Display(Name = "Seconds Per Item")]
        public int Seconds_Per_Item { get; set; }
    }

    public class GeneralMinutesConfig
    {
        [JsonPropertyName("Minutes_Per_Item")]
        [Display(Name = "Minutes Per Item")]
        public int Minutes_Per_Item { get; set; }
    }
}