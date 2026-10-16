using FifthPress.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.Controllers
{
    //things were getting crowded in JobController and Job2Controller is currently on another branch, so Job3Controller it is

    public class Job3Controller : DataController
    {
        public async Task<ActionResult> DeleteJobDocuments(List<int> ids)
        {
            ids = ids ?? new List<int>();

            foreach (int i in ids)
            {
                var jobDocument = await db.JobDocuments.FindAsync(i);

                if (jobDocument != null)
                {
                    //delete the physical file; jobDocument.DocumentPath is full path from root
                    if (System.IO.File.Exists(jobDocument.DocumentPath))
                    {
                        System.IO.File.Delete(jobDocument.DocumentPath);
                        //delete the record from the database
                        db.JobDocuments.Remove(jobDocument);
                    }
                }
                else
                {
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "(job): unable to find document with id = " + i.ToString());
                }
            }

            try
            {
                await db.SaveChangesAsync();
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK);
            }
            catch (Exception e)
            {
                MastermindLogger.Log(LogLevel.Error, "Error while trying to save changes to deleted job document records; ids = " + string.Join(",", ids));
                MastermindLogger.LogException(e);

                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ActionResult> DeleteQuoteDocuments(List<int> ids)
        {
            ids = ids ?? new List<int>();

            foreach (int i in ids)
            {
                var quoteDocument = await db.QuoteDocuments.FindAsync(i);

                if (quoteDocument != null)
                {
                    //delete the physical file; quoteDocument.DocumentPath is full path from root
                    if (System.IO.File.Exists(quoteDocument.DocumentPath))
                    {
                        System.IO.File.Delete(quoteDocument.DocumentPath);
                        //delete the record from the database
                        db.QuoteDocuments.Remove(quoteDocument);
                    }
                }
                else
                {
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "(quote): unable to find document with id = " + i.ToString());
                }
            }

            try
            {
                await db.SaveChangesAsync();
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK);
            }
            catch (Exception e)
            {
                MastermindLogger.Log(LogLevel.Error, "Error while trying to save changes to deleted quote document records; ids = " + string.Join(",", ids));
                MastermindLogger.LogException(e);

                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError);
            }
        }
    }
}