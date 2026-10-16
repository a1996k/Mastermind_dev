using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_ApprovalConfiguration25
    {
        [Display(Name = "Department Approver Limit")]
        [Range(0, 999999999, ErrorMessage = "Please enter a valid amount")]
        public decimal DepartmentApproverLimit { get; set; }

        [Display(Name = "Department Manager Limit")]
        [Range(0, 999999999, ErrorMessage = "Please enter a valid amount")]
        public decimal DepartmentManagerLimit { get; set; }

        [Display(Name = "Owner Approval Required Amount")]
        [Range(0, 999999999, ErrorMessage = "Please enter a valid amount")]
        public decimal RequireOwnerApprovalAmount { get; set; }

        [Display(Name = "Executive Auto-Approve Limit")]
        [Range(0, 999999999, ErrorMessage = "Please enter a valid amount")]
        public decimal AutoApproveExecutiveUnder { get; set; }

        [Display(Name = "Accounting Auto-Approve Limit")]
        [Range(0, 999999999, ErrorMessage = "Please enter a valid amount")]
        public decimal AutoApproveAccountingUnder { get; set; }

        [Display(Name = "Purchasing Auto-Approve Limit")]
        [Range(0, 999999999, ErrorMessage = "Please enter a valid amount")]
        public decimal AutoApprovePurchasingUnder { get; set; }
    }
}