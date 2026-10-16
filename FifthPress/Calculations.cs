using System;
using System.Data.Entity.Core.Objects;
using System.Drawing.Text;
using System.Linq;
using FifthPress.Models.Calculations;
using System.Threading.Tasks;
using WebGrease;
using Microsoft.Ajax.Utilities;
using System.Web.Http.Routing.Constraints;
using FifthPress.Models;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Data.Entity.ModelConfiguration.Conventions;
using System.Data.Entity;
using Newtonsoft.Json;

namespace FifthPress
{
    public static class CalculationUtilities
    {
        /// <summary>
        /// Calculates the total number of columns and rows that are necessary to print all items
        /// </summary>
        /// <param name="itemLength">The length of the item to be printed, including all vertical space</param>
        /// <param name="itemWidth">The width of the item to be printed, including all hotizontal space</param>
        /// <param name="quantity">The number of items to be printed</param>
        /// <param name="runSizeWidth">The width of the printable material, minus any horizontal margins</param>
        /// <param name="runSizeLength">The length of the printable material, minus any vertical margins</param>
        /// <returns>PrintJobInformation object with number of rows, columns and sheet count</returns>
        public static RowColumnInformation CalculateLayout(
            decimal itemLength,
            decimal itemWidth,
            int quantity,
            decimal runSizeWidth,
            decimal runSizeLength,
            bool upLocked,
            bool sheetsLocked,
            int upValue,
            int currentSheetCount,
            Material.AvailableStockTypes stockType,
            int materialPartCount, // materialPartCount is for stock like NCR where a quantity of 1 can equal 3 sheets
            decimal materialLength,
            decimal materialWidth,
            int scrapSheets)
        {
            // Set default values for item dimensions if they are zero to avoid division by zero
            itemWidth = itemWidth == 0m ? 1m : itemWidth;
            itemLength = itemLength == 0m ? 1m : itemLength;

            // Calculate the number of columns and rows on a single sheet
            int columns1 = (int)(runSizeWidth / itemWidth);
            int rows1 = (int)(runSizeLength / itemLength);
            int columns2 = (int)(runSizeLength / itemWidth);
            int rows2 = (int)(runSizeWidth / itemLength);

            bool useResultOne = true;

            // Set items per sheet to 1
            int itemsPerSheet = 1;

            // If we locked the up value, use the specified up value
            if (upLocked)
            {
                itemsPerSheet = upValue;
            }
            // Otherwise, check the stock type. If a roll stock, up value is the number of columns. Else (sheet stock) is rows x columns
            else
            {
                if (stockType == Material.AvailableStockTypes.RollStock || stockType == Material.AvailableStockTypes.CutVinyl)
                {
                    itemsPerSheet = columns1;
                }
                else
                {
                    // MAS-39: Use the orientation that yields the most up for sheet stock
                    int ips1 = (columns1 * rows1);
                    int ips2 = (columns2 * rows2);
                    itemsPerSheet = Math.Max(ips1, ips2);

                    if (ips2 > ips1)
                        useResultOne = false;
                }
            }

            if (itemsPerSheet == 0)
                itemsPerSheet = 1;

            // Calculate the total number of sheets needed
            int totalSheets = sheetsLocked ? currentSheetCount : (int)Math.Ceiling((decimal)quantity / itemsPerSheet);

            int totalRunSheetsFromMaterial = 1;

            // Additional logic for calculating run sheets from the main material
            if (runSizeLength < materialLength || runSizeWidth < materialWidth)
            {
                // Calculate how many full run sheets fit within the main material
                int runSheetsLengthwise = (int)(materialLength / runSizeLength);
                int runSheetsWidthwise = (int)(materialWidth / runSizeWidth);

                // Total run sheets that can be cut from the main material
                totalRunSheetsFromMaterial = runSheetsLengthwise * runSheetsWidthwise;
            }

            return new RowColumnInformation
            {
                ColumnCount = useResultOne ? columns1 : columns2,
                RowCount = useResultOne ? rows1 : rows2,
                TotalColumns = (useResultOne ? columns1 : columns2) * totalSheets,
                TotalRows = (useResultOne ? rows1 : rows2) * totalSheets,
                UpValue = itemsPerSheet,
                Out = totalRunSheetsFromMaterial,
                TotalSheets = totalSheets * materialPartCount,
                RunSheets = 0
                //RunSheets = (totalSheets + scrapSheets) * materialPartCount //setting to 0 for now because unsure if necessary
            };
        }
    }

    public class Calculations
    {
        public List<ServiceCalculation> serviceCalculations;
        public List<PrintingCalculation> printingCalculations;
        public List<FinishingMaterialCalculation> finishingMaterialCalculations;
        public List<MaterialCalculation> materialCalculations;
        public List<OutsourcedCalculation> outsourcedCalculations;
        public List<SqftPricingCalculation> sqftPricingCalculations;

        public Calculations()
        {
            serviceCalculations = new List<ServiceCalculation>();
            printingCalculations = new List<PrintingCalculation>();
            finishingMaterialCalculations = new List<FinishingMaterialCalculation>();
            materialCalculations = new List<MaterialCalculation>();
            outsourcedCalculations = new List<OutsourcedCalculation>();
            sqftPricingCalculations = new List<SqftPricingCalculation>();
        }

        public enum Formulas
        {
            LinearCutting = 1,
            ZundCutting = 2,
            ThreeHolePunch = 3,
            PressPlates = 4,
            VariableHolePunch = 5,
            Folding = 6,
            Scoring = 7,
            Polybagging = 8,
            ShrinkWrapping = 9,
            BulkPack = 10,
            KitPack = 11,
            Preflight = 13,
            Perforate = 14,
            CustomCartons = 15,
            Grommets = 16,
            DieCutting = 17,
            Scrapping = 19,
            Laminate = 20,
            SecondsPerPiece = 21,
            MinutesPerPiece = 22
        }

        public class OutsourcedCalculation
        {
            private int itemId;
            private string itemType;
            private double totalCost;
            private double markup;
            private double totalPrice;
            private string title;
            private double quantity;

            public OutsourcedCalculation(string itemType, int itemId)
            {
                this.itemType = (itemType ?? "").ToUpper();
                this.itemId = itemId;
                this.title = "Outsourced Item"; //vendor name?
            }

            public int GetItemId()
            {
                return this.itemId;
            }

            public async Task<double> GetMarkup(FifthPressEntities db)
            {
                this.markup = 0;
                double markup = 0;

                switch (itemType)
                {
                    case "QUOTE":
                        QuoteOutsourcedItem qoi = await db.QuoteOutsourcedItems.FindAsync(itemId);
                        if (qoi.CustomMarkup.HasValue)
                        {
                            markup = (double)qoi.CustomMarkup.Value;
                            quantity = (double)qoi.Quantity;
                        }
                        else
                        {
                            await db.Entry(qoi).Reference(r => r.Quote1).LoadAsync();
                            markup = (double)(qoi.Quote1.MarkupOverride ?? 0m);
                            quantity = (double)qoi.Quantity;
                        }
                        break;

                    case "JOB":
                        JobOutsourcedItem joi = await db.JobOutsourcedItems.FindAsync(itemId);
                        if (joi.CustomMarkup.HasValue)
                        {
                            markup = (double)joi.CustomMarkup.Value;
                            quantity = (double)joi.Quantity;
                        }
                        else
                        {
                            await db.Entry(joi).Reference(r => r.Job1).LoadAsync();
                            markup = (double)(joi.Job1.MarkupOverride ?? 0m);
                            quantity = (double)joi.Quantity;
                        }
                        break;

                    default:
                        break;
                }

                this.markup = markup;

                return this.markup;
            }

            public double CalculateCost(FifthPressEntities db)
            {
                this.totalCost = 0;
                double cost = 0;

                switch (itemType)
                {
                    case "QUOTE":
                        ObjectResult<decimal?> qRes = db.Quote_OutsourcedItemCost(this.itemId);
                        cost = (double)(qRes.FirstOrDefault() ?? 0m);
                        break;

                    case "JOB":
                        ObjectResult<decimal?> jRes = db.Job_OutsourcedItemCost(this.itemId);
                        cost = (double)(jRes.FirstOrDefault() ?? 0m);
                        break;

                    default:
                        break;
                }

                this.totalCost = cost;

                return this.totalCost;
            }

            public double CalculatePrice()
            {
                this.totalPrice = 0;
                double price = this.totalCost + (this.totalCost * this.markup);

                this.totalPrice = price;
                return this.totalPrice;
            }

            public decimal GetPrice()
            {
                return (decimal)this.totalPrice;
            }

            public decimal GetCost()
            {
                return (decimal)this.totalCost;
            }

            public decimal GetMarkupValue()
            {
                return (decimal)this.markup;
            }

            public string GetTitle()
            {
                return this.title;
            }
        }

        public class SqftPricingCalculation
        {
            private int itemId;
            private string itemType;
            private double totalSqft;
            private double sqftRate;
            private double sqftCost;
            private double markup;
            private double sqftPrice;
            private string title;

            public SqftPricingCalculation(string itemType, int itemId)
            {
                this.itemType = (itemType ?? "").ToUpper();
                this.itemId = itemId;
                this.title = "SQFT Pricing";
            }

            public int GetItemId()
            {
                return this.itemId;
            }

            public async Task<double> CalculateSqft(FifthPressEntities db)
            {
                this.totalSqft = 0;
                double sqft = 0;

                switch (itemType)
                {
                    case "QUOTE":
                        QuoteItem qi = await db.QuoteItems.FindAsync(itemId);
                        if (qi != null)
                        {
                            //Adding Math.Ceiling as per the ticket MAS-44
                            decimal length = Math.Ceiling(Decimal.Parse(String.IsNullOrEmpty(qi.Length) ? "0m" : qi.Length));
                            decimal width = Math.Ceiling(Decimal.Parse(String.IsNullOrEmpty(qi.Width) ? "0m" : qi.Width));
                            int quantity = qi.Quantity ?? 0;
                            int by = qi.QuantityBy ?? 1;

                            // Convert from inches to square feet: (L × W / 144) × (Qty × By)
                            sqft = (double)((length * width / 144m) * (quantity * by));
                        }
                        break;

                    case "JOB":
                        JobItem ji = await db.JobItems.FindAsync(itemId);
                        if (ji != null)
                        {
                            //Adding Math.Ceiling as per the ticket MAS-44
                            decimal length = Math.Ceiling(Decimal.Parse(String.IsNullOrEmpty(ji.Length) ? "0m" : ji.Length));
                            decimal width = Math.Ceiling(Decimal.Parse(String.IsNullOrEmpty(ji.Width) ? "0m" : ji.Width));
                            int quantity = ji.Quantity ?? 0;
                            int by = ji.QuantityBy ?? 1;

                            // Convert from inches to square feet: (L × W / 144) × (Qty × By)
                            sqft = (double)((length * width / 144m) * (quantity * by));
                        }
                        break;

                    default:
                        break;
                }

                this.totalSqft = sqft;
                return this.totalSqft;
            }

            public async Task<double> GetSqftRate(FifthPressEntities db)
            {
                this.sqftRate = 0;
                double rate = 0;

                switch (itemType)
                {
                    case "QUOTE":
                        QuoteItem qi = await db.QuoteItems.FindAsync(itemId);
                        await db.Entry(qi).Reference(r => r.Quote1).LoadAsync();

                        // Check for Quote-level override first
                        if (qi.Quote1.SqftPriceOverride.HasValue)
                        {
                            rate = (double)qi.Quote1.SqftPriceOverride.Value;
                        }
                        // Otherwise use Client's SQFT price
                        else if (qi.Quote1.Client.HasValue)
                        {
                            await db.Entry(qi.Quote1).Reference(r => r.Client1).LoadAsync();
                            rate = (double)(qi.Quote1.Client1.SqftPrice ?? 0m);
                        }
                        break;

                    case "JOB":
                        JobItem ji = await db.JobItems.FindAsync(itemId);
                        await db.Entry(ji).Reference(r => r.Job1).LoadAsync();

                        // Check for Job-level override first
                        if (ji.Job1.SqftPriceOverride.HasValue)
                        {
                            rate = (double)ji.Job1.SqftPriceOverride.Value;
                        }
                        // Otherwise use Client's SQFT price
                        else if (ji.Job1.Client.HasValue)
                        {
                            await db.Entry(ji.Job1).Reference(r => r.Client1).LoadAsync();
                            rate = (double)(ji.Job1.Client1.SqftPrice ?? 0m);
                        }
                        break;

                    default:
                        break;
                }

                this.sqftRate = rate;
                return this.sqftRate;
            }

            public async Task<double> GetMarkup(FifthPressEntities db)
            {
                this.markup = 0;
                double markup = 0;

                switch (itemType)
                {
                    case "QUOTE":
                        QuoteItem qi = await db.QuoteItems.FindAsync(itemId);
                        await db.Entry(qi).Reference(r => r.Quote1).LoadAsync();

                        // Use ink markup from item or quote-level markup override
                        if (qi.CustomInkMarkup.HasValue)
                        {
                            markup = (double)qi.CustomInkMarkup.Value;
                        }
                        else
                        {
                            markup = (double)(qi.Quote1.MarkupOverride ?? 0m);
                        }
                        break;

                    case "JOB":
                        JobItem ji = await db.JobItems.FindAsync(itemId);
                        await db.Entry(ji).Reference(r => r.Job1).LoadAsync();

                        // Use ink markup from item or job-level markup override
                        if (ji.CustomInkMarkup.HasValue)
                        {
                            markup = (double)ji.CustomInkMarkup.Value;
                        }
                        else
                        {
                            markup = (double)(ji.Job1.MarkupOverride ?? 0m);
                        }
                        break;

                    default:
                        break;
                }

                this.markup = markup;
                return this.markup;
            }

            public double CalculateCost()
            {
                this.sqftCost = 0;
                this.sqftCost = this.totalSqft * this.sqftRate;
                return this.sqftCost;
            }

            public double CalculatePrice()
            {
                this.sqftPrice = 0;
                this.sqftPrice = this.sqftCost + (this.sqftCost * this.markup);
                return this.sqftPrice;
            }

            public decimal GetPrice()
            {
                return (decimal)this.sqftPrice;
            }

            public decimal GetCost()
            {
                return (decimal)this.sqftCost;
            }

            public decimal GetMarkupValue()
            {
                return (decimal)this.markup;
            }

            public string GetTitle()
            {
                return this.title;
            }

            public decimal GetTotalSqft()
            {
                return (decimal)this.totalSqft;
            }

            public decimal GetSqftRateValue()
            {
                return (decimal)this.sqftRate;
            }
        }

        public class PrintingCalculation
        {
            private int itemServiceId;
            private string itemType;
            private double printTime;
            private double printCost;
            private double hourlyRate;
            private double inkCost;
            private double inkMarkup;
            private double laborMarkup;
            private double inkPrice;
            private double printPrice;
            private int parentItemId;
            private string laborTitle;
            private string inkTitle;

            public PrintingCalculation(string itemType, int? itemServiceId)
            {
                this.itemType = (itemType ?? "").ToUpper();
                this.itemServiceId = itemServiceId ?? -1;
            }

            public int GetItemId()
            {
                return this.parentItemId;
            }

            public int GetItemServiceId()
            {
                return this.itemServiceId;
            }

            public async Task<double> CalculateTime(FifthPressEntities db)
            {
                double printTime = 0;
                this.printTime = 0;

                switch (itemType)
                {
                    case "QUOTE":
                        QuoteItemService qis = await db.QuoteItemServices.FindAsync(itemServiceId);
                        await db.Entry(qis).Reference(r => r.QuoteItem).LoadAsync();

                        //set the parent item id
                        parentItemId = qis.QuoteItemId;

                        //set the labor and ink titles
                        await db.Entry(qis.QuoteItem).Reference(r => r.Press1).LoadAsync();
                        this.laborTitle = qis.QuoteItem.Press1.Title + " Labor Cost";
                        this.inkTitle = qis.QuoteItem.Press1.Title + " Ink Cost";
                        //get the hourly rate
                        this.hourlyRate = (double)(qis.QuoteItem.Press1.LaborCostPerHour ?? 0m);

                        // If user has locked the time, use the existing value
                        if (qis.UserTimeOverride == true)
                        {
                            printTime = (double)(qis.ServiceTime ?? 0m);
                        }
                        else if (qis.QuoteItem.Press.HasValue)
                        {
                            ObjectResult<decimal?> qTime = db.Quote_PrintingTime(this.itemServiceId);
                            printTime = (double)(qTime.FirstOrDefault() ?? 0m);
                        }
                        break;

                    case "JOB":
                        JobItemService jis = await db.JobItemServices.FindAsync(itemServiceId);
                        await db.Entry(jis).Reference(r => r.JobItem).LoadAsync();

                        //set the parent item id
                        parentItemId = jis.JobItemId;

                        //set the labor and ink titles
                        await db.Entry(jis.JobItem).Reference(r => r.Press1).LoadAsync();
                        this.laborTitle = jis.JobItem.Press1.Title + " Labor Cost";
                        this.inkTitle = jis.JobItem.Press1.Title + " Ink Cost";
                        //get the hourly rate
                        this.hourlyRate = (double)(jis.JobItem.Press1.LaborCostPerHour ?? 0m);

                        // If user has locked the time, use the existing value
                        if (jis.UserTimeOverride == true)
                        {
                            printTime = (double)(jis.ServiceTime ?? 0m);
                        }
                        else if (jis.JobItem.Press.HasValue)
                        {
                            ObjectResult<decimal?> jTime = db.Job_PrintingTime(this.itemServiceId);
                            printTime = (double)(jTime.FirstOrDefault() ?? 0m);
                        }
                        break;

                    default:
                        break;
                }

                this.printTime = printTime;

                return printTime;
            }

            public async Task<double> GetLaborMarkup(FifthPressEntities db)
            {
                this.laborMarkup = 0;
                double markup = 0;

                switch (itemType)
                {
                    case "QUOTE":
                        QuoteItemService qis = await db.QuoteItemServices.FindAsync(itemServiceId);
                        await db.Entry(qis).Reference(r => r.QuoteItem).LoadAsync();

                        if (qis.QuoteItem.CustomPrintLaborMarkup.HasValue)
                        {
                            markup = (double)qis.QuoteItem.CustomPrintLaborMarkup.Value;
                        }
                        else
                        {
                            await db.Entry(qis.QuoteItem).Reference(r => r.Quote1).LoadAsync();
                            markup = (double)(qis.QuoteItem.Quote1.MarkupOverride ?? 0m);
                        }
                        break;

                    case "JOB":
                        JobItemService jis = await db.JobItemServices.FindAsync(itemServiceId);
                        await db.Entry(jis).Reference(r => r.JobItem).LoadAsync();

                        if (jis.JobItem.CustomPrintLaborMarkup.HasValue)
                        {
                            markup = (double)jis.JobItem.CustomPrintLaborMarkup.Value;
                        }
                        else
                        {
                            await db.Entry(jis).Reference(r => r.JobItem).LoadAsync();
                            await db.Entry(jis.JobItem).Reference(r => r.Job1).LoadAsync();
                            markup = (double)(jis.JobItem.Job1.MarkupOverride ?? 0m);
                        }
                        break;

                    default:
                        break;
                }

                this.laborMarkup = markup;

                return markup;
            }

            public async Task<double> GetInkMarkup(FifthPressEntities db)
            {
                this.inkMarkup = 0;
                double markup = 0;

                switch (itemType)
                {
                    case "QUOTE":
                        QuoteItemService qis = await db.QuoteItemServices.FindAsync(itemServiceId);
                        await db.Entry(qis).Reference(r => r.QuoteItem).LoadAsync();

                        if (qis.QuoteItem.CustomInkMarkup.HasValue)
                        {
                            markup = (double)qis.QuoteItem.CustomInkMarkup.Value;
                        }
                        else
                        {
                            await db.Entry(qis.QuoteItem).Reference(r => r.Quote1).LoadAsync();
                            markup = (double)(qis.QuoteItem.Quote1.MarkupOverride ?? 0m);
                        }
                        break;

                    case "JOB":
                        JobItemService jis = await db.JobItemServices.FindAsync(itemServiceId);
                        await db.Entry(jis).Reference(r => r.JobItem).LoadAsync();

                        if (jis.JobItem.CustomInkMarkup.HasValue)
                        {
                            markup = (double)jis.JobItem.CustomInkMarkup.Value;
                        }
                        else
                        {
                            await db.Entry(jis).Reference(r => r.JobItem).LoadAsync();
                            await db.Entry(jis.JobItem).Reference(r => r.Job1).LoadAsync();
                            markup = (double)(jis.JobItem.Job1.MarkupOverride ?? 0m);
                        }
                        break;

                    default:
                        break;
                }

                this.inkMarkup = markup;

                return markup;
            }

            public async Task<double> CalculateInkCost(FifthPressEntities db)
            {
                this.inkCost = 0;
                double inkCost = 0;

                switch (itemType)
                {
                    case "QUOTE":
                        QuoteItemService qis = await db.QuoteItemServices.FindAsync(itemServiceId);
                        ObjectResult<decimal?> qRes = db.Quote_InkCost(itemServiceId);
                        inkCost = (double)(qRes.FirstOrDefault() ?? 0m);
                        break;

                    case "JOB":
                        JobItemService jis = await db.JobItemServices.FindAsync(itemServiceId);
                        ObjectResult<decimal?> jRes = db.Job_InkCost(itemServiceId);
                        inkCost = (double)(jRes.FirstOrDefault() ?? 0m);
                        break;

                    default:
                        break;
                }

                this.inkCost = inkCost;
                return inkCost;
            }

            public double CalculateInkPrice()
            {
                this.inkPrice = 0;
                this.inkPrice = (this.inkCost) + (this.inkCost * this.inkMarkup);
                return this.inkPrice;
            }

            public double CalculatePrintCost()
            {
                this.printCost = 0;
                this.printCost = this.printTime * this.hourlyRate;

                return this.printCost;
            }

            public double CalculatePrintPrice()
            {
                this.printPrice = 0;
                this.printPrice = this.printCost + (this.printCost * this.laborMarkup);
                return this.printPrice;
            }

            public decimal GetPrintCost()
            {
                return (decimal)this.printCost;
            }

            public decimal GetInkCost()
            {
                return (decimal)this.inkCost;
            }

            public decimal GetPrintPrice()
            {
                return (decimal)this.printPrice;
            }

            public decimal GetInkPrice()
            {
                return (decimal)this.inkPrice;
            }

            public double CalculateCost()
            {
                this.printCost = this.printTime * this.hourlyRate;
                //include ink cost
                return this.printCost;
            }

            public decimal GetTime()
            {
                return (decimal)this.printTime;
            }

            public decimal GetInkMarkupValue()
            {
                return (decimal)this.inkMarkup;
            }

            public decimal GetLaborMarkupValue()
            {
                return (decimal)this.laborMarkup;
            }

            public string GetInkTitle()
            {
                return this.inkTitle;
            }

            public string GetLaborTitle()
            {
                return this.laborTitle;
            }
        }

        public class ServiceCalculation
        {
            private int itemServiceId;
            private string itemType;
            private int formulaId;
            private double serviceTime;
            private double serviceCost;
            private double hourlyRate;
            private bool isTimed;
            private double markup;
            private double servicePrice;
            private int parentItemId;
            private string title;
            private double materialCost;

            public ServiceCalculation(string itemType, int? itemServiceId)
            {
                this.itemServiceId = itemServiceId ?? -1;
                this.itemType = (itemType ?? "").ToUpper();
                materialCost = 0;
            }

            public int GetItemId()
            {
                return this.parentItemId;
            }

            public int GetItemServiceId()
            {
                return this.itemServiceId;
            }

            public async Task<double> CalculateTime(FifthPressEntities db)
            {
                switch (itemType)
                {
                    case "QUOTE":
                        QuoteItemService qis = await db.QuoteItemServices.FindAsync(itemServiceId);
                        await db.Entry(qis).Reference(r => r.Service).LoadAsync();

                        parentItemId = qis.QuoteItemId;

                        if (qis.Service == null) break;

                        this.title = qis.Service.Title;
                        hourlyRate = (double)(qis.HourlyRate ?? 0m);
                        formulaId = qis.Service.TimeFormula ?? 0;
                        isTimed = (qis.Service.IsTimed ?? false);
                        materialCost = (double)(qis.MaterialCost ?? 0m);

                        break;

                    case "JOB":
                        JobItemService jis = await db.JobItemServices.FindAsync(itemServiceId);
                        await db.Entry(jis).Reference(r => r.Service).LoadAsync();

                        parentItemId = jis.JobItemId;

                        if (jis.Service == null) break;

                        this.title = jis.Service.Title;
                        hourlyRate = (double)(jis.HourlyRate ?? 0m);
                        formulaId = jis.Service.TimeFormula ?? 0;
                        isTimed = (jis.Service.IsTimed ?? false);
                        materialCost = (double)(jis.MaterialCost ?? 0m);
                        break;

                    default:
                        break;
                }

                if (isTimed)
                {
                    double time = 0;
                    this.serviceTime = 0;

                    switch (itemType)
                    {
                        case "QUOTE":

                            switch ((Calculations.Formulas)formulaId)
                            {
                                case Calculations.Formulas.LinearCutting:
                                    ObjectResult<decimal?> lcResult = db.Quote_CalculateLinearCuttingTime(itemServiceId);
                                    time = (double)(lcResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.ZundCutting:
                                    ObjectResult<decimal?> zcResult = db.Quote_CalculateZundCuttingTime(itemServiceId);
                                    time = (double)(zcResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.ThreeHolePunch:
                                    ObjectResult<decimal?> thpResult = db.Quote_ThreeHolePunchTime(itemServiceId);
                                    time = (double)(thpResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.PressPlates:
                                    ObjectResult<decimal?> ppResult = db.Quote_PlateTime(itemServiceId);
                                    time = (double)(ppResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.VariableHolePunch:
                                    ObjectResult<decimal?> vhpResult = db.Quote_VariableHolePunchTime(itemServiceId);
                                    time = (double)(vhpResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.Folding:
                                    ObjectResult<decimal?> fResult = db.Quote_FoldingTime(itemServiceId);
                                    time = (double)(fResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.Scoring:
                                    ObjectResult<decimal?> sResult = db.Quote_ScoringTime(itemServiceId);
                                    time = (double)(sResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.Polybagging:
                                    ObjectResult<decimal?> pbResult = db.Quote_PolybaggingTime(itemServiceId);
                                    time = (double)(pbResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.ShrinkWrapping:
                                    ObjectResult<decimal?> swResult = db.Quote_ShrinkwrappingTime(itemServiceId);
                                    time = (double)(swResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.BulkPack:
                                    ObjectResult<decimal?> bpResult = db.Quote_BulkPackingTime(itemServiceId);
                                    time = (double)(bpResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.KitPack:
                                    ObjectResult<decimal?> kpResult = db.Quote_KitPackingTime(itemServiceId);
                                    time = (double)(kpResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.Preflight:
                                    ObjectResult<decimal?> preflightResult = db.Quote_Preflight(itemServiceId);
                                    time = (double)(preflightResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.Perforate:
                                    ObjectResult<decimal?> perforateResult = db.Quote_PeforateTime(itemServiceId);
                                    time = (double)(perforateResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.CustomCartons:
                                    ObjectResult<decimal?> customCartonsResult = db.Quote_CustomCartonTime(itemServiceId);
                                    time = (double)(customCartonsResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.Grommets:
                                    ObjectResult<decimal?> grommetsResult = db.Quote_GrommetTime(itemServiceId);
                                    time = (double)(grommetsResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.DieCutting:
                                    ObjectResult<decimal?> dieCuttingResult = db.Quote_DieCuttingTime(itemServiceId);
                                    time = (double)(dieCuttingResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.Scrapping:
                                    ObjectResult<decimal?> scrappingResult = db.Quote_ScrappingTime(itemServiceId);
                                    time = (double)(scrappingResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.Laminate:
                                    ObjectResult<decimal?> laminateResult = db.Quote_LaminateTime(itemServiceId);
                                    time = (double)(laminateResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.SecondsPerPiece:
                                    ObjectResult<decimal?> sppResult = db.Quote_SecondsPerItemTime(itemServiceId);
                                    time = (double)(sppResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.MinutesPerPiece:
                                    ObjectResult<decimal?> mppResult = db.Quote_MinutesPerItemTime(itemServiceId);
                                    time = (double)(mppResult.FirstOrDefault() ?? 0m);
                                    break;

                                default:
                                    ObjectResult<decimal?> defResult = db.Quote_NoFormula(itemServiceId);
                                    time = (double)(defResult.FirstOrDefault() ?? 0m);
                                    break;
                            }

                            break;

                        case "JOB":
                            switch ((Calculations.Formulas)formulaId)
                            {
                                case Calculations.Formulas.LinearCutting:
                                    ObjectResult<decimal?> lcResult = db.Job_CalculateLinearCuttingTime(itemServiceId);
                                    time = (double)(lcResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.ZundCutting:
                                    ObjectResult<decimal?> zcResult = db.Job_CalculateZundCuttingTime(itemServiceId);
                                    time = (double)(zcResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.ThreeHolePunch:
                                    ObjectResult<decimal?> thpResult = db.Job_ThreeHolePunchTime(itemServiceId);
                                    time = (double)(thpResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.PressPlates:
                                    ObjectResult<decimal?> ppResult = db.Job_PlateTime(itemServiceId);
                                    time = (double)(ppResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.VariableHolePunch:
                                    ObjectResult<decimal?> vhpResult = db.Job_VariableHolePunchTime(itemServiceId);
                                    time = (double)(vhpResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.Folding:
                                    ObjectResult<decimal?> fResult = db.Job_FoldingTime(itemServiceId);
                                    time = (double)(fResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.Scoring:
                                    ObjectResult<decimal?> sResult = db.Job_ScoringTime(itemServiceId);
                                    time = (double)(sResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.Polybagging:
                                    ObjectResult<decimal?> pbResult = db.Job_PolybaggingTime(itemServiceId);
                                    time = (double)(pbResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.ShrinkWrapping:
                                    ObjectResult<decimal?> swResult = db.Job_ShrinkwrappingTime(itemServiceId);
                                    time = (double)(swResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.BulkPack:
                                    ObjectResult<decimal?> bpResult = db.Job_BulkPackingTime(itemServiceId);
                                    time = (double)(bpResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.KitPack:
                                    ObjectResult<decimal?> kpResult = db.Job_KitPackingTime(itemServiceId);
                                    time = (double)(kpResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.Preflight:
                                    ObjectResult<decimal?> preflightResult = db.Job_Preflight(itemServiceId);
                                    time = (double)(preflightResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.Perforate:
                                    ObjectResult<decimal?> perforateResult = db.Job_PeforateTime(itemServiceId);
                                    time = (double)(perforateResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.CustomCartons:
                                    ObjectResult<decimal?> customCartonsResult = db.Job_CustomCartonTime(itemServiceId);
                                    time = (double)(customCartonsResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.Grommets:
                                    ObjectResult<decimal?> grommetsResult = db.Job_GrommetTime(itemServiceId);
                                    time = (double)(grommetsResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.DieCutting:
                                    ObjectResult<decimal?> dieCuttingResult = db.Job_DieCuttingTime(itemServiceId);
                                    time = (double)(dieCuttingResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.Scrapping:
                                    ObjectResult<decimal?> scrappingResult = db.Job_ScrappingTime(itemServiceId);
                                    time = (double)(scrappingResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.Laminate:
                                    ObjectResult<decimal?> laminateResult = db.Job_LaminateTime(itemServiceId);
                                    time = (double)(laminateResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.SecondsPerPiece:
                                    ObjectResult<decimal?> sppResult = db.Job_SecondsPerItemTime(itemServiceId);
                                    time = (double)(sppResult.FirstOrDefault() ?? 0m);
                                    break;

                                case Calculations.Formulas.MinutesPerPiece:
                                    ObjectResult<decimal?> mppResult = db.Job_MinutesPerItemTime(itemServiceId);
                                    time = (double)(mppResult.FirstOrDefault() ?? 0m);
                                    break;

                                default:
                                    ObjectResult<decimal?> defResult = db.Job_NoFormula(itemServiceId);
                                    time = (double)(defResult.FirstOrDefault() ?? 0m);
                                    break;
                            }
                            break;
                    }

                    this.serviceTime = time;
                    return time;
                }
                return 0;
            }

            public string GetTitle()
            {
                return this.title;
            }

            public async Task<double> GetMarkup(FifthPressEntities db)
            {
                this.markup = 0;
                double markup = 0;

                switch (itemType)
                {
                    case "QUOTE":
                        QuoteItemService qis = await db.QuoteItemServices.FindAsync(itemServiceId);
                        if (qis.CustomMarkup.HasValue)
                        {
                            markup = (double)qis.CustomMarkup.Value;
                        }
                        else
                        {
                            await db.Entry(qis).Reference(r => r.QuoteItem).LoadAsync();
                            await db.Entry(qis.QuoteItem).Reference(r => r.Quote1).LoadAsync();
                            markup = (double)(qis.QuoteItem.Quote1.MarkupOverride ?? 0m);
                        }
                        break;

                    case "JOB":
                        JobItemService jis = await db.JobItemServices.FindAsync(itemServiceId);
                        if (jis.CustomMarkup.HasValue)
                        {
                            markup = (double)jis.CustomMarkup.Value;
                        }
                        else
                        {
                            await db.Entry(jis).Reference(r => r.JobItem).LoadAsync();
                            await db.Entry(jis.JobItem).Reference(r => r.Job1).LoadAsync();
                            markup = (double)(jis.JobItem.Job1.MarkupOverride ?? 0m);
                        }
                        break;

                    default:
                        break;
                }

                this.markup = markup;

                return markup;
            }

            public double CalculateCost()
            {
                this.serviceCost = 0;
                this.serviceCost = (this.serviceTime * this.hourlyRate);
                this.serviceCost += this.materialCost;

                return this.serviceCost;
            }

            public decimal GetTime()
            {
                return (decimal)this.serviceTime;
            }

            public decimal GetCost()
            {
                return (decimal)this.serviceCost;
            }

            public double CalculatePrice()
            {
                this.servicePrice = 0;
                this.servicePrice = this.serviceCost + (this.serviceCost * this.markup);
                return this.servicePrice;
            }

            public decimal GetPrice()
            {
                return (decimal)this.servicePrice;
            }

            public decimal GetMarkupValue()
            {
                return (decimal)this.markup;
            }
        }

        public class MaterialCalculation
        {
            private string itemtype;
            private int itemId;
            private double totalCost;
            private double markup;
            private double totalPrice;
            private string title;
            private int materialId;
            private double totalMaterialAmount;

            public MaterialCalculation(string itemType, int itemId)
            {
                this.itemtype = itemType.ToUpper();
                this.itemId = itemId;
            }

            public int GetItemId()
            {
                return this.itemId;
            }

            public double CalculateCost(FifthPressEntities db)
            {
                switch (itemtype)
                {
                    case "QUOTE":
                        ObjectResult<Quote_PrintableMaterialCost_Result> qRes = db.Quote_PrintableMaterialCost(itemId);
                        Quote_PrintableMaterialCost_Result q_pmc_r = qRes.First();
                        totalCost = (double)(q_pmc_r.FinalCost ?? 0m);
                        totalMaterialAmount = (double)(q_pmc_r.FinalAmount ?? 0m);
                        break;

                    case "JOB":
                        ObjectResult<Job_PrintableMaterialCost_Result> jRes = db.Job_PrintableMaterialCost(itemId);
                        Job_PrintableMaterialCost_Result j_pmc_r = jRes.First();
                        totalCost = (double)(j_pmc_r.FinalCost ?? 0m);
                        totalMaterialAmount = (double)(j_pmc_r.FinalAmount ?? 0m);
                        break;

                    default:
                        totalCost = 0;
                        totalMaterialAmount = 0;
                        break;
                }

                return totalCost;
            }

            public decimal GetCost()
            {
                return (decimal)this.totalCost;
            }

            public async Task<double> GetMarkup(FifthPressEntities db)
            {
                this.markup = 0;
                double markup = 0;

                switch (itemtype)
                {
                    case "QUOTE":
                        QuoteItem qi = await db.QuoteItems.FindAsync(itemId);
                        await db.Entry(qi).Reference(r => r.Material1).LoadAsync();

                        if (qi.Material.HasValue)
                        {
                            //get material title
                            this.title = qi.Material1.Title;
                            this.materialId = qi.Material ?? -1;

                            if (qi.CustomMaterialMarkup.HasValue)
                            {
                                markup = (double)qi.CustomMaterialMarkup.Value;
                            }
                            else
                            {
                                await db.Entry(qi).Reference(r => r.Quote1).LoadAsync();
                                markup = (double)(qi.Quote1.MarkupOverride ?? 0m);
                            }
                        }
                        else
                        {
                            this.title = "";
                            this.materialId = -1;
                            markup = (double)0m;
                        }
                        break;

                    case "JOB":
                        JobItem ji = await db.JobItems.FindAsync(itemId);
                        await db.Entry(ji).Reference(r => r.Material1).LoadAsync();

                        if (ji.Material.HasValue)
                        {
                            this.title = ji.Material1.Title;
                            this.materialId = ji.Material ?? -1;

                            if (ji.CustomMaterialMarkup.HasValue)
                            {
                                markup = (double)ji.CustomMaterialMarkup.Value;
                            }
                            else
                            {
                                await db.Entry(ji).Reference(r => r.Job1).LoadAsync();
                                markup = (double)(ji.Job1.MarkupOverride ?? 0m);
                            }
                        }
                        else
                        {
                            this.title = "";
                            this.materialId = -1;
                            this.markup = (double)0m;
                        }
                        break;

                    default:
                        break;
                }

                this.markup = markup;

                return markup;
            }

            public double CalculatePrice()
            {
                this.totalPrice = 0;
                this.totalPrice = this.totalCost + (this.totalCost * this.markup);
                return this.totalPrice;
            }

            public decimal GetMarkupValue()
            {
                return (decimal)this.markup;
            }

            public decimal GetPrice()
            {
                return (decimal)this.totalPrice;
            }

            public string GetTitle()
            {
                return this.title;
            }

            public int GetMaterialId()
            {
                return this.materialId;
            }

            public decimal GetTotalMaterialAmount()
            {
                return (decimal)this.totalMaterialAmount;
            }
        }

        public class FinishingMaterialCalculation
        {
            private readonly string itemType;
            private int itemId;
            private readonly int fmItemId;
            private double materialAmount;
            private double materialCost;
            private double markup;
            private double materialPrice;
            private string title;
            private double materialUnitCost; //used for figuring the final cost
            private int materialCostType;

            public FinishingMaterialCalculation(string itemType, int? finishingMaterialId)
            {
                this.itemType = itemType.ToUpper();
                this.fmItemId = finishingMaterialId ?? 0;
            }

            public int GetItemId()
            {
                return this.itemId;
            }

            public int GetFinishingMaterialId()
            {
                return this.fmItemId;
            }

            public async Task<double> CalculateMaterialQuantity(FifthPressEntities db)
            {
                decimal materialAmount = 0m;
                //need to find the finishing formula for this finishing material
                switch (itemType)
                {
                    case "QUOTE":
                        QuoteItemFinishingMaterial qifm = await db.QuoteItemFinishingMaterials.FindAsync(fmItemId);
                        FinishingMaterialFormulaPivot qPivot = db.FinishingMaterialFormulaPivots.FirstOrDefault(x => x.Material == qifm.Material);

                        //get finishing material title
                        await db.Entry(qifm).Reference(r => r.Material1).LoadAsync();
                        this.title = qifm.Material1.Title;

                        //get the item id
                        this.itemId = qifm.QuoteItemId ?? -1;

                        //get material unit cost
                        //this.materialUnitCost = (double)(qifm.Material1.Cost ?? 0m);
                        this.materialUnitCost = (double)(qifm.ItemCost ?? 0m);
                        //get the cost type
                        this.materialCostType = qifm.Material1.CostType ?? -1;

                        // Heal a stale config key so it matches the material's current formula (e.g. a
                        // By Length material still carrying a legacy "Pieces" key from before it was
                        // switched, or copied via duplication). The formula procs read the value by a
                        // specific JSON key (Quote_FM_ByLength reads $.Feet), so a mismatched key yields
                        // NULL -> $0. Keep the entered number, relabel the key. Persist before the proc
                        // runs, because the proc reads the config from the DB.
                        FinishingMaterialFormula qFormula = await db.FinishingMaterialFormulas.FindAsync(qPivot.FinishingFormula);
                        if (qFormula != null)
                        {
                            string qReconciled = ReconcileFinishingConfigKey(qifm.FinishingFormulaConfiguration, qFormula.DefaultConfig);
                            if (qReconciled != null)
                            {
                                qifm.FinishingFormulaConfiguration = qReconciled;
                                db.Entry(qifm).State = EntityState.Modified;
                                await db.SaveChangesAsync();
                            }
                        }

                        switch (qPivot.FinishingFormula)
                        {
                            case 1:
                                //bundling
                                ObjectResult<int?> qBundling = db.Quote_FM_ByBundle(fmItemId);
                                materialAmount = qBundling.First() ?? 0m;
                                break;

                            case 2:
                                //byquantity
                                ObjectResult<int?> qQuantity = db.Quote_FM_ByQuantity(fmItemId);
                                materialAmount = qQuantity.First() ?? 0;
                                break;

                            case 3:
                                //bylength
                                ObjectResult<int?> qLength = db.Quote_FM_ByLength(fmItemId);
                                materialAmount = qLength.First() ?? 0m;
                                break;

                            default:
                                MastermindLogger.Log(LogLevel.Warning, $"[FinishingMaterialCalculation] Unmapped FinishingFormula={qPivot.FinishingFormula} for QUOTE fmItemId={fmItemId} ('{this.title}') — materialAmount defaulting to 0");
                                break;
                        }
                        break;

                    case "JOB":
                        JobItemFinishingMaterial jifm = await db.JobItemFinishingMaterials.FindAsync(fmItemId);
                        FinishingMaterialFormulaPivot jPivot = db.FinishingMaterialFormulaPivots.FirstOrDefault(x => x.Material == jifm.Material);

                        //get finishing material title
                        await db.Entry(jifm).Reference(r => r.Material1).LoadAsync();
                        this.title = jifm.Material1.Title;

                        //get the item id
                        this.itemId = jifm.JobItemId;

                        //this.materialCost = (double)(jifm.Material1.Cost ?? 0m);

                        this.materialUnitCost = (double)(jifm.ItemCost ?? 0m);
                        this.materialCostType = jifm.Material1.CostType ?? -1;

                        // Heal a stale config key so it matches the material's current formula (see the
                        // QUOTE branch above for the full rationale). Persist before the proc runs.
                        FinishingMaterialFormula jFormula = await db.FinishingMaterialFormulas.FindAsync(jPivot.FinishingFormula);
                        if (jFormula != null)
                        {
                            string jReconciled = ReconcileFinishingConfigKey(jifm.FinishingFormulaConfiguration, jFormula.DefaultConfig);
                            if (jReconciled != null)
                            {
                                jifm.FinishingFormulaConfiguration = jReconciled;
                                db.Entry(jifm).State = EntityState.Modified;
                                await db.SaveChangesAsync();
                            }
                        }

                        switch (jPivot.FinishingFormula)
                        {
                            case 1:
                                //bundling
                                ObjectResult<int?> jBundling = db.Job_FM_ByBundle(fmItemId);
                                materialAmount = jBundling.First() ?? 0m;
                                break;

                            case 2:
                                //quantity
                                ObjectResult<int?> jQuantity = db.Job_FM_ByQuantity(fmItemId);
                                materialAmount = jQuantity.First() ?? 0m;
                                break;

                            case 3:
                                //length
                                ObjectResult<int?> jLength = db.Job_FM_ByLength(fmItemId);
                                materialAmount = jLength.First() ?? 0m;
                                break;

                            default:
                                MastermindLogger.Log(LogLevel.Warning, $"[FinishingMaterialCalculation] Unmapped FinishingFormula={jPivot.FinishingFormula} for JOB fmItemId={fmItemId} ('{this.title}') — materialAmount defaulting to 0");
                                break;
                        }
                        break;

                    default:
                        return 0;
                }

                this.materialAmount = (double)materialAmount;
                return (double)materialAmount;
            }

            // Returns a corrected finishing config whose single key matches the formula's expected
            // key (from DefaultConfig), preserving the entered value — or null when no change is
            // needed or possible (empty/unparseable config, keys already match). This heals legacy
            // configs (e.g. a "Pieces" key left on a material later switched to By Length, which
            // reads $.Feet) that would otherwise silently price to $0.
            private static string ReconcileFinishingConfigKey(string currentConfig, string defaultConfig)
            {
                if (string.IsNullOrWhiteSpace(currentConfig) || string.IsNullOrWhiteSpace(defaultConfig))
                    return null;

                try
                {
                    Dictionary<string, string> current = JsonConvert.DeserializeObject<Dictionary<string, string>>(currentConfig);
                    Dictionary<string, string> expected = JsonConvert.DeserializeObject<Dictionary<string, string>>(defaultConfig);

                    if (current == null || current.Count == 0 || expected == null || expected.Count == 0)
                        return null;

                    string currentKey = current.Keys.First();
                    string expectedKey = expected.Keys.First();

                    if (currentKey == expectedKey)
                        return null;

                    return JsonConvert.SerializeObject(new Dictionary<string, string> { { expectedKey, current[currentKey] } });
                }
                catch
                {
                    return null;
                }
            }

            public string GetTitle()
            {
                return this.title;
            }

            public async Task<double> GetMarkup(FifthPressEntities db)
            {
                this.markup = 0;
                double markup = 0;

                switch (itemType)
                {
                    case "QUOTE":
                        QuoteItemFinishingMaterial qifm = await db.QuoteItemFinishingMaterials.FindAsync(fmItemId);
                        if (qifm.CustomMarkup.HasValue)
                        {
                            markup = (double)qifm.CustomMarkup.Value;
                        }
                        else
                        {
                            await db.Entry(qifm).Reference(r => r.QuoteItem).LoadAsync();
                            await db.Entry(qifm.QuoteItem).Reference(r => r.Quote1).LoadAsync();
                            markup = (double)(qifm.QuoteItem.Quote1.MarkupOverride ?? 0m);
                        }

                        break;

                    case "JOB":
                        JobItemFinishingMaterial jifm = await db.JobItemFinishingMaterials.FindAsync(fmItemId);
                        if (jifm.CustomMarkup.HasValue)
                        {
                            markup = (double)jifm.CustomMarkup.Value;
                        }
                        else
                        {
                            await db.Entry(jifm).Reference(r => r.JobItem).LoadAsync();
                            await db.Entry(jifm.JobItem).Reference(r => r.Job1).LoadAsync();
                            markup = (double)(jifm.JobItem.Job1.MarkupOverride ?? 0m);
                        }
                        break;

                    default:
                        break;
                }

                this.markup = markup;

                return markup;
            }

            public decimal GetMaterialAmount()
            {
                return (decimal)this.materialAmount;
            }

            public double CalculateMaterialCost()
            {
                this.materialCost = 0;

                double materialCost = 0;

                switch (this.materialCostType)
                {
                    case 1://each
                        materialCost = this.materialAmount * this.materialUnitCost;
                        break;

                    case 2://hundred
                        materialCost = this.materialAmount * (this.materialUnitCost / 100.00);
                        break;

                    case 3://thousand
                        materialCost = this.materialAmount * (this.materialUnitCost / 1000.00);
                        break;

                    case 4://feet
                        materialCost = this.materialAmount * this.materialUnitCost;
                        break;

                    default:
                        break;
                }

                this.materialCost = materialCost;

                return this.materialCost;
            }

            public decimal GetMaterialCost()
            {
                return (decimal)this.materialCost;
            }

            public double CalculateMaterialPrice()
            {
                this.materialPrice = 0;
                this.materialPrice = this.materialCost + (this.materialCost * this.markup);

                return this.materialPrice;
            }

            public decimal GetMaterialPrice()
            {
                return (decimal)this.materialPrice;
            }

            public decimal GetMarkupValue()
            {
                return (decimal)this.markup;
            }
        }
    }
}