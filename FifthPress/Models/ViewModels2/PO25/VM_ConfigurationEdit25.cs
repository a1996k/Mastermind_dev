using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_ConfigurationEdit25
    {
        public int ConfigId { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Configuration Key")]
        public string ConfigKey { get; set; }

        [Required]
        [StringLength(1000)]
        [Display(Name = "Configuration Value")]
        public string ConfigValue { get; set; }

        [StringLength(50)]
        [Display(Name = "Data Type")]
        public string DataType { get; set; }

        [StringLength(500)]
        [Display(Name = "Description")]
        public string Description { get; set; }

        [StringLength(100)]
        [Display(Name = "Category")]
        public string Category { get; set; }

        [Display(Name = "Is System Configuration")]
        public bool IsSystemConfig { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; }
    }
}