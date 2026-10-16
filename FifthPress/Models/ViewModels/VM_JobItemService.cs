using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_JobItemService
    {
        public JobItemService JobItemService { get; set; }
        public VM_TimeInput ServiceTimeEditor { get; set; }
        public string TimingParametersJson { get; set; }
        public string TimingParameterModelType { get; set; }
    }
}