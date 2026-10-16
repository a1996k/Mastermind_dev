using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_ViewJob
    {
        public Job Job { get; set; }
        public List<Tuple<int, bool>> UserServiceActivity { get; set; }
        public VM_MultiselectModel MultiselectModel { get; set; }
        public bool ShowStatusButtons { get; set; }
    }
}