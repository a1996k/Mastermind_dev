using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models
{
    /// <summary>
    /// expects rendered html in the form of a string
    /// </summary>
    public class DataTable
    {
        public string TableHead { get; set; }
        public string TableBody { get; set; }
    }
}