using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.Models.ViewModels
{
    public class VM_ConstraintEditor
    {
        public Constraint Constraint { get; set; }
        public bool IsRange { get; set; }
    }
}