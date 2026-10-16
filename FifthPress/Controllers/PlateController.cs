using Newtonsoft.Json;
using System;
using System.Activities.Statements;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using FifthPress.Models.ViewModels;

namespace FifthPress.Controllers
{
    public class PlateController : DataController
    {
        private Plate UpdatePlateRecord(Plate existing, Plate n)
        {
            existing.MakeReady = n.MakeReady;
            existing.WashUp = n.MakeReady;
            existing.MinRunTime = n.MinRunTime;
            existing.Title = n.Title;

            return existing;
        }
        public ActionResult NewPlate()
        {
            return new JsonResult
            {
                Data = JsonConvert.SerializeObject(
                    RenderViewToString("~/Views/Partials/GetPlateForEdit.cshtml", 
                    new VM_Plate { Plate = new Plate(), AssignedPresses = new List<PressPlate>() })),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public async Task<ActionResult> SavePlate(Plate plate, List<int> AssignedPressIds)
        {
            //using (ConnectToDatabase())
            {
                Plate existingPlate = await db.Plates.FindAsync(plate.Id);
                
                if (existingPlate == null)
                {
                    //new plate

                    int newPlateId = 0;
                    
                    try
                    {
                        newPlateId = await db.Plates.MaxAsync(x => x.Id);
                    }
                    catch (Exception)
                    {
                        //ok; move on
                    }

                    newPlateId++;

                    plate.Id = newPlateId;

                    //foreach assigned press ids
                    int newPlatePressId = 0;
                    try
                    {
                        newPlatePressId = await db.PressPlates.MaxAsync(x => x.Id);
                    }
                    catch (Exception)
                    {
                        //ok; move on
                    }

                    newPlatePressId++;
                    //create a new record

                    foreach(int i in AssignedPressIds)
                    {
                        db.PressPlates.Add(new PressPlate
                        {
                            Id = newPlatePressId,
                            Press = i,
                            Plate = newPlateId
                        });

                        newPlatePressId++;
                    }
                }
                else
                {
                    //update plate
                    existingPlate = UpdatePlateRecord(existingPlate, plate);

                    //get all PressPlate records which have a matching plate.Id value but do not match any of the ids in AssignedPressIds
                    List<PressPlate> pressPlatesToDelete = await db.PressPlates.Where(x => x.Plate == plate.Id && !AssignedPressIds.Contains(x.Press)).ToListAsync();
                    
                    //delete those presses
                    foreach (PressPlate pp in pressPlatesToDelete)
                        db.PressPlates.Remove(pp);

                    //save
                    
                    //make new records if necessary
                    
                    List<PressPlate> pressPlates = await db.PressPlates.Where(x => x.Press == plate.Id).ToListAsync();

                    int newPressPlateId = 0;
                    try
                    {
                        newPressPlateId = await db.PressPlates.MaxAsync(x => x.Id);
                    }catch (Exception)
                    {
                        //ok; move on
                    }

                    newPressPlateId++;
                    foreach(int i in AssignedPressIds)
                    {
                        db.PressPlates.Add(new PressPlate
                        {
                            Id = newPressPlateId,
                            Press = i,
                            Plate = plate.Id
                        });

                        newPressPlateId++;
                    }
                }

                await db.SaveChangesAsync();
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK);
        }

        public async Task<ActionResult> GetPlateForEdit(int? id)
        {
            if (id.HasValue)
            {
                //using (ConnectToDatabase())
                {
                    Plate p = await db.Plates.FindAsync(id);
                    if(p == null)
                    {
                        return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "record not found");
                    }
                    List<PressPlate> pressPlates = await db.PressPlates.Where(x => x.Plate == id.Value).ToListAsync();

                    foreach(PressPlate pp in pressPlates)
                    {
                        await db.Entry(pp).Reference(r => r.Press1).LoadAsync();
                        await db.Entry(pp).Reference(r => r.Plate1).LoadAsync();
                    }

                    VM_Plate platevm = new VM_Plate
                    {
                        Plate = p,
                        AssignedPresses = pressPlates
                    };

                    return new JsonResult
                    {
                        Data = JsonConvert.SerializeObject(RenderViewToString("~/Views/Plate/GetPlateForEdit.cshtml", platevm)),
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };
                }
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError);
        }

        public async Task<ActionResult> AddPlateToPress(int? plate, int? press)
        {
            if (!plate.HasValue || !press.HasValue)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Missing required parameters");

            int newPressPlateId = 0;

            //using (ConnectToDatabase())
            {
                try
                {
                    newPressPlateId = await db.PressPlates.MaxAsync(x => x.Id);
                }
                catch (Exception)
                {
                    //ok; move on
                }

                newPressPlateId++;

                db.PressPlates.Add(new PressPlate
                {
                    Id = newPressPlateId,
                    Press = press.Value,
                    Plate = plate.Value
                });

                await db.SaveChangesAsync();

                return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK);
            }
        }

        public async Task<ActionResult> RemovePlateFromPress(int? plate, int? press)
        {
            if(plate.HasValue && press.HasValue)
            {
                //using (ConnectToDatabase())
                {
                    try
                    {
                        PressPlate pp = await db.PressPlates.SingleAsync(x => x.Plate == plate.Value && x.Press == press.Value);
                        db.PressPlates.Remove(pp);
                        await db.SaveChangesAsync();

                        return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK, "Record deleted");
                    }
                    catch (Exception)
                    {
                        return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK, "Nothing actually deleted");
                    }
                }
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Parameter error");
        }
    }
}