using FifthPress.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace FifthPress
{
    /// <summary>
    /// NOTE: this class requires an active database connection
    /// </summary>
    public class ServiceTimeCalculations : Controllers.DataController
    {
        private int CalculateDieCuttingTime(string recordtype, int? itemRecordId, int? itemServiceId)
        {
            return 0;
        }

        public decimal DieCuttingFormula(string recordtype, int? itemRecordId, int? itemServiceId)
        {
            decimal serviceTime = CalculateDieCuttingTime(recordtype, itemRecordId, itemServiceId); //in minutes
            decimal serviceTimeHours = (decimal)serviceTime / 60m;

            return serviceTimeHours;
        }
    }
}