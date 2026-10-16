using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_Marcus
    {
        public List<v_QuotesCreatedInLast7Days> RecentQuotes { get; set; }
        public List<v_TodaysQuotes> TodaysQuotes { get; set; }
    }
}