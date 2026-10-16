using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Threading.Tasks;

namespace FifthPress.Models.Calculations
{
    public class ScrapSheetCalculator
    {
        private enum SpecialPresses
        {
            HEIDELBERG = 26,
            ROLAND = 31
        }

        public class ScrapSheets
        {
            public int ColorScrap { get; set; }
            public int DefaultScrap { get; set; }
            public int VariableScrapAmount { get; set; }
            public int RunSheetScrap { get; set; }
        }

        public async Task<ScrapSheets> CalculateScrapSheets(FifthPressEntities db, int pressId, int itemId, string itemType)
        {
            Press p = await db.Presses.FindAsync(pressId);
            int scrapPerColor = p.ScrapSheetsPerColor ?? 0;
            int scrapPerJob = p.ScrapSheetsPerJob ?? 0;
            decimal scrapAmount = (p.ScrapLevel ?? 0m) / 100m;

            int frontColorCount = 0;
            int backColorCount = 0;
            int sidesPrintedOn = 0;

            bool userOverride = false;

            int colorCount = 0;
            int colorScrapSheets = 0;
            int printableSheetCount = 0;
            int variableScrapSheets = 0;
            int materialPartsCount = 1;

            switch (itemType.ToUpper())
            {
                case "QUOTE":
                    QuoteItem qi = await db.QuoteItems.FindAsync(itemId);
                    await db.Entry(qi).Reference(r => r.Material1).LoadAsync();

                    //material will have value at this point (checked before function call)
                    materialPartsCount = qi.Material1.NumberOfParts ?? 1;
                    userOverride = (qi.UserSheetOverride ?? false);

                    frontColorCount = qi.PressColorCountFront ?? 0;
                    backColorCount = qi.PressColorCountBack ?? 0;

                    if (frontColorCount > 0)
                        sidesPrintedOn++;
                    if (backColorCount > 0)
                        sidesPrintedOn++;

                    colorCount = frontColorCount + backColorCount;

                    if (!userOverride)
                    {
                        //if the press is heidl or roland and sidesPrintedOn > 1, double the scrap percentage
                        if (sidesPrintedOn > 1)
                        {
                            if (
                                p.Id == (int)SpecialPresses.HEIDELBERG ||
                                p.Id == (int)SpecialPresses.ROLAND ||
                                (p.FromPress ?? 0) == (int)SpecialPresses.HEIDELBERG ||
                                (p.FromPress ?? 0) == (int)SpecialPresses.ROLAND)
                            {
                                scrapAmount = (scrapAmount * 2m);

                                //use the larger of the two values for this situation
                                colorCount = backColorCount > frontColorCount ? backColorCount : frontColorCount;
                            }
                        }
                        printableSheetCount = qi.Sheets ?? 0;
                        variableScrapSheets = (int)Math.Ceiling((decimal)printableSheetCount * scrapAmount);
                        colorScrapSheets = colorCount * scrapPerColor;
                    }

                    break;

                case "JOB":
                    JobItem ji = await db.JobItems.FindAsync(itemId);
                    await db.Entry(ji).Reference(r => r.Material1).LoadAsync();

                    //material will have value at this point (checked before function call)
                    materialPartsCount = ji.Material1.NumberOfParts ?? 1;
                    userOverride = (ji.UserSheetOverride ?? false);

                    frontColorCount = ji.PressColorCountFront ?? 0;
                    backColorCount = ji.PressColorCountBack ?? 0;

                    if (frontColorCount > 0)
                        sidesPrintedOn++;
                    if (backColorCount > 0)
                        sidesPrintedOn++;

                    //color count being set here in case we have to override it later
                    colorCount = frontColorCount + backColorCount;

                    if (!userOverride)
                    {
                        //if the press is heidl or roland and sidesPrintedOn > 1, double the scrap percentage
                        if (sidesPrintedOn > 1)
                        {
                            if (
                                p.Id == (int)SpecialPresses.HEIDELBERG ||
                                p.Id == (int)SpecialPresses.ROLAND ||
                                (p.FromPress ?? 0) == (int)SpecialPresses.HEIDELBERG ||
                                (p.FromPress ?? 0) == (int)SpecialPresses.ROLAND)
                            {
                                scrapAmount = (scrapAmount * 2m);

                                //use the larger of the two values for this situation
                                colorCount = backColorCount > frontColorCount ? backColorCount : frontColorCount;
                            }
                        }

                        printableSheetCount = ji.Sheets ?? 0;
                        variableScrapSheets = (int)Math.Ceiling((decimal)printableSheetCount * scrapAmount);
                        colorScrapSheets = colorCount * scrapPerColor;
                    }

                    break;

                default:
                    break;
            }

            //material parts count adjustment: all scrap sheet values need to be divisible by the number of parts

            if (colorScrapSheets > 0)
                while (colorScrapSheets % materialPartsCount != 0)
                    colorScrapSheets++;

            if (scrapPerJob > 0)
                while (scrapPerJob % materialPartsCount != 0)
                    scrapPerJob++;

            if (variableScrapSheets > 0)
                while (variableScrapSheets % materialPartsCount != 0)
                    variableScrapSheets++;

            ScrapSheets ss = new ScrapSheets
            {
                ColorScrap = colorScrapSheets,
                DefaultScrap = scrapPerJob,
                VariableScrapAmount = variableScrapSheets
            };

            return ss;
        }
    }
}