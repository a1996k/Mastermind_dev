using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_FreightShippingAddressCollection
    {
        public List<FreightShippingAddress> FreightShippingAddresses { get; set; }
        public int? SelectedAddressId { get; set; }
    }
}