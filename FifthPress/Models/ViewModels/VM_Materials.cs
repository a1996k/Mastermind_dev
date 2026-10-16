using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_Materials
    {
        public List<Material> Materials { get; set; }
        public List<MaterialCategory> MaterialCategoryObjs { get; set; }
        public List<string> MaterialCategories { get; set; }
        public List<string> MaterialKeys { get; set; }
    }
}