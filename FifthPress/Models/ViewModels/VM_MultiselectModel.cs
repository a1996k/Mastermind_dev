using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class Option
    {
        public string Value { get; set; }
        public string Text { get; set; }
        public bool Selected { get; set; }
    }
    public class VM_MultiselectModel
    {
        public List<Option> Options { get; set; }
        public List<string> SelectedOptions { get; set; }

        public string SelectTitle { get; set; }
    }
}