using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_EmailConfiguration25
    {
        [Display(Name = "Enable Email Notifications")]
        public bool NotificationsEnabled { get; set; }

        [Display(Name = "Admin Email Address")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [StringLength(255, ErrorMessage = "Email address cannot exceed 255 characters")]
        public string AdminEmailAddress { get; set; }

        [Display(Name = "From Email Address")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [StringLength(255, ErrorMessage = "Email address cannot exceed 255 characters")]
        public string EmailFromAddress { get; set; }

        [Display(Name = "Email Signature")]
        [StringLength(500, ErrorMessage = "Email signature cannot exceed 500 characters")]
        public string EmailSignature { get; set; }
    }
}