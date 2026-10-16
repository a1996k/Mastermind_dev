using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_QuoteItemService
    {
        public QuoteItemService QuoteItemService { get; set; }
        public VM_TimeInput ServiceTimeEditor { get; set; }
        public string TimingParametersJson { get; set; }
        public string TimingParameterModelType { get; set; }
    }
}