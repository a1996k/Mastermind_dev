using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_PrepressServices
    {
        public List<Service> Services { get; set; }
        public List<string> ServiceCategories { get; set; }

        public List<string> ServiceKeys { get; set; }
    }
}