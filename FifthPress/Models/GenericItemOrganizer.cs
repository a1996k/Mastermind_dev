using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models
{
    public class GenericItemOrganizer
    {
        public int Id { get; set; }
        public string RowType { get; set; }
        public int Ordinal { get; set; }
    }
}