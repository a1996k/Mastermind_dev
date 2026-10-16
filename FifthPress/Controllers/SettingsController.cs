using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.Controllers
{
    public class SettingsController : DataController
    {
        //private readonly CostCalculator _ccalculator;

        /*public SettingsController(CostCalculator calculator)
        : base(calculator) // Call the base class constructor
        {
            _ccalculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
        }*/
        // GET: Settings
        public ActionResult Index()
        {
            return View();
        }
    }
}