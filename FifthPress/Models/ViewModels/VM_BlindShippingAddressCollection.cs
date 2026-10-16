using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_BlindShippingAddressCollection
    {
        public List<BlindShippingPivotTable> BlindShippingAddresses { get; set; }
        public int? SelectedAddressId { get; set; }
    }
}
