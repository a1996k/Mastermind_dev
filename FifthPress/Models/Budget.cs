using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models
{
    /// <summary>
    /// Budget breakdown for an event
    /// </summary>
    public class Budget
    {
        public List<BudgetItem> PrintApproved { get; set; }
        public List<BudgetItem> PrintQuoted { get; set; }

        // These will be populated from external sources later
        public List<BudgetItem> HardwareRental { get; set; }

        public List<BudgetItem> OnsitePrinting { get; set; }
        public List<BudgetItem> OnsiteLabor { get; set; }
        public List<BudgetItem> OffsiteLabor { get; set; }
        public List<BudgetItem> TravelExpenses { get; set; }
        public List<BudgetItem> Accommodation { get; set; }
        public List<BudgetItem> VehicleRental { get; set; }
        public List<BudgetItem> Freight { get; set; }
        public List<BudgetItem> Reimbursables { get; set; }
        public List<BudgetItem> ManagementFees { get; set; }
        public List<BudgetItem> StorageFees { get; set; }
        public List<BudgetItem> DesignFees { get; set; }
        public List<BudgetItem> Miscellaneous { get; set; }
    }
}