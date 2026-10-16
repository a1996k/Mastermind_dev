using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models
{
    
    public class ItemConfiguration
    {
        public List<ItemService> Services { get; set; }
        public int ItemId { get; set; }
    }

    public class ItemService
    {
        public int ServiceItemRecordId { get; set; }
        public int ApexOrdinal { get; set; }
    }
}