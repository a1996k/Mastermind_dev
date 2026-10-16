using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using static FifthPress.Controllers.JobLayoutController;

namespace FifthPress.Extensions
{
    // Extensions to make existing models implement the interface
    public static class LayoutItemExtensions
    {
        public static ILayoutItem AsLayoutItem(this JobItem item) => new JobItemWrapper(item);

        public static ILayoutItem AsLayoutItem(this QuoteItem item) => new QuoteItemWrapper(item);
    }
}