using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_ServiceAndCuttingConfigurations
    {
        public Service Service { get; set; }
        public List<CuttingConfiguration> Configurations { get; set; }
    }
}