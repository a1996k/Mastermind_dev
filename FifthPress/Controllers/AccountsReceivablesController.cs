using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.Controllers
{
    public class AccountsReceivablesController : DataController
    {
        //private readonly CostCalculator _ccalculator;

        /*public AccountsReceivablesController(CostCalculator calculator)
        : base(calculator) // Call the base class constructor
        {
            _ccalculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
        }*/
        // GET: AccountsReceivables
        public ActionResult Index()
        {
            return View();
        }
    }
}