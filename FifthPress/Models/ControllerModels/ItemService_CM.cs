using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ControllerModels
{
    public class ItemService_CM
    {
        public List<ItemServiceDetails_CM> ItemServiceDetails { get; set; }
        public int RowId { get; set; }
        public string RecordType { get; set; }
    }
}