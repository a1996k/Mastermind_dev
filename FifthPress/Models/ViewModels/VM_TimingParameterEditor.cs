using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_TimingParameterEditor
    {
        public string ParameterNameCamelCase { get; set; }
        public string ParameterNameDisplay { get; set; }
        public string ParameterValue { get; set; }
        public decimal ParameterValueUnits { get; set; }
        public string ParameterValueUnitText { get; set; }
    }
}