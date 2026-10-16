using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_ShippingAddressCollection
    {
        public List<ShippingAddress> ShippingAddresses { get; set; }
        public int? SelectedAddressId { get; set; }
    }
}