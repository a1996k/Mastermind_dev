using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_DashboardConfigurator
    {
        public User User { get; set; }
        public List<Tuple<int, string>> ActiveCards { get; set; }
        public List<Tuple<int, string>> InactiveCards { get; set; }
    }
}