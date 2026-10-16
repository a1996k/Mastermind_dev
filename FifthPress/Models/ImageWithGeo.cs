using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models
{
    public class ImageWithGeo
    {
        public HttpPostedFileBase image { get; set; }
        public string latitude { get; set; }
        public string longitude { get; set; }
    }
}