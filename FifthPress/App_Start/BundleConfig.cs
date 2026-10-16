using Microsoft.Ajax.Utilities;
using System.Web.Optimization;

namespace FifthPress
{
    public class BundleConfig
    {
        // For more information on bundling, visit https://go.microsoft.com/fwlink/?LinkId=301862
        public static void RegisterBundles(BundleCollection bundles)
        {
            bundles.Add(new Bundle("~/bundles/jquery").Include(
                        "~/Scripts/jquery-{version}.js"));

            bundles.Add(new Bundle("~/bundles/jqueryuijs").Include(
                    "~/Scripts/jquery-ui.js"
                ));

            bundles.Add(new Bundle("~/Content/jqueryuicss").Include(
                    "~/Content/jquery-ui.structure.css",
                    "~/Content/jquery-ui.css"
                ));

            // Use the development version of Modernizr to develop with and learn from. Then, when you're
            // ready for production, use the build tool at https://modernizr.com to pick only the tests you need.
            bundles.Add(new Bundle("~/bundles/modernizr").Include(
                        "~/Scripts/modernizr-*"));

            bundles.Add(new Bundle("~/bundles/bootstrap").Include(
                      "~/Scripts/bootstrap.bundle.js"));

            bundles.Add(new Bundle("~/bundles/chart").Include(
                      "~/Scripts/chart.js"));

            bundles.Add(new Bundle("~/bundles/daterangepicker").Include(
                      "~/Scripts/daterangepicker.js"));

            bundles.Add(new Bundle("~/bundles/qol").Include(
                      "~/Scripts/joblist.js"));

            bundles.Add(new Bundle("~/bundles/autorefresh").Include(
                      "~/Scripts/autorefresh.js"));

            bundles.Add(new Bundle("~/bundles/quotes").Include(
                      "~/Scripts/quotes.js"));

            bundles.Add(new Bundle("~/bundles/jobs").Include(
                      "~/Scripts/jobs.js"));

            bundles.Add(new Bundle("~/bundles/estimates").Include(
                      "~/Scripts/estimates.js"));

            bundles.Add(new Bundle("~/bundles/administration").Include(
                    "~/Scripts/administration.js",
                    "~/Scripts/tournament-budget.js"));

            bundles.Add(new Bundle("~/bundles/ccalc").Include(
                    "~/Scripts/TimingAndCosts.js"));

            bundles.Add(new Bundle("~/bundles/validator").Include(
                    "~/Scripts/Validator.js"));

            bundles.Add(new Bundle("~/bundles/ordersjs").Include(
                    "~/Scripts/orders.js"));

            bundles.Add(new Bundle("~/bundles/timetracking").Include(
                    "~/Scripts/TimeTracking.js"));

            bundles.Add(new Bundle("~/Content/css").Include(
                      "~/Content/bootstrap.bundle.css",
                      "~/Content/custom_base_style.css",
                      "~/Content/Site.css"));

            bundles.Add(new Bundle("~/Content/globalcss").Include(
                    "~/Content/Global.css",
                    "~/Content/PO25-Accessibility.css"));

            bundles.Add(new Bundle("~/Content/joblistcss").Include(
                    "~/Content/JobList.css"));

            bundles.Add(new Bundle("~/Content/estimatecss").Include(
                    "~/Content/Estimates.css"));

            bundles.Add(new Bundle("~/Content/admincss").Include(
                    "~/Content/Admin.css"));

            bundles.Add(new Bundle("~/bundles/globaljs").Include(
                    "~/Scripts/global.js",
                    "~/Scripts/custom-validation.js"));

            bundles.Add(new Bundle("~/bundles/layoutmgr").Include(
                    "~/Scripts/LayoutManager.js"));

            bundles.Add(new Bundle("~/bundles/layoutmgr2").Include(
                "~/Scripts/LayoutManager2.js"));

            bundles.Add(new Bundle("~/Content/layoutplanning").Include(
                    "~/Content/LayoutManager.css"));

            bundles.Add(new Bundle("~/bundles/timingparams").Include(
                    "~/Scripts/TimingParameters.js"));

            bundles.Add(new Bundle("~/bundles/owljs").Include(
                    "~/Scripts/owl.carousel.js"));

            bundles.Add(new Bundle("~/Content/arr_quote_css").Include(
                    "~/Content/ArrangeQuote.css"));

            bundles.Add(new Bundle("~/bundles/momentjs").Include(
                    "~/Scripts/moment.js"));

            bundles.Add(new Bundle("~/Content/arr_job_css").Include(
                    "~/Content/ArrangeJob.css"));

            bundles.Add(new Bundle("~/bundles/pojs").Include(
                    "~/Scripts/PurchaseOrders.js"));

            bundles.Add(new Bundle("~/Content/pocss").Include(
                    "~/Content/PurchaseOrders.css"));

            bundles.Add(new Bundle("~/Content/sidenavcss").Include(
                    "~/Content/SideNav.css"));

            bundles.Add(new Bundle("~/Content/daterangepicker").Include(
                    "~/Content/daterangepicker.css"));

            bundles.Add(new Bundle("~/Content/jobticket").Include(
                    "~/Content/JobTicket.css"));

            bundles.Add(new Bundle("~/Content/deliveryticket").Include(
                    "~/Content/DeliveryTicket.css"));

            bundles.Add(new Bundle("~/Content/quoteticket").Include(
                    "~/Content/QuoteTicket.css"));

            bundles.Add(new Bundle("~/Content/dashboard").Include(
                    "~/Content/Dashboard.css"));

            bundles.Add(new Bundle("~/bundles/dashboard").Include(
                "~/Scripts/dashboard-charts.js",
                "~/Scripts/Dashboard.js"));

            /*bundles.Add(new Bundle("~/bundles/quotelayout").Include(
                    "~/Scripts/QuoteLayout.js"));

            bundles.Add(new Bundle("~/bundles/joblayout").Include(
                    "~/Scripts/JobLayout.js"));
            */
            bundles.Add(new Bundle("~/bundles/billing").Include(
                    "~/Scripts/billing.js"));

            bundles.Add(new Bundle("~/bundles/billingreports").Include(
                    "~/Scripts/billingreports.js"));

            bundles.Add(new Bundle("~/bundles/bugs").Include(
                    "~/Scripts/bugs.js"));

            bundles.Add(new Bundle("~/Content/billing").Include(
                    "~/Content/Billing.css"));

            bundles.Add(new Bundle("~/Content/costsheet").Include(
                    "~/Content/CostSheet.css"));

            bundles.Add(new Bundle("~/Content/invoice").Include(
                    "~/Content/Invoice.css"));

            bundles.Add(new Bundle("~/Content/orders").Include(
                    "~/Content/Orders.css"));

            bundles.Add(new Bundle("~/Content/DashboardConfigurator").Include(
                    "~/Content/DashboardConfigurator.css"));

            bundles.Add(new Bundle("~/bundles/DashboardConfigurator").Include(
                    "~/Scripts/DashboardConfigurator.js"));

            bundles.Add(new Bundle("~/bundles/recordsearch").Include(
                    "~/Scripts/RecordSearch.js"));

            bundles.Add(new Bundle("~/bundles/purchaseordersearch").Include(
                    "~/Scripts/PurchaseOrderSearch.js"));

            bundles.Add(new Bundle("~/bundles/jobshipments").Include(
                    "~/Scripts/JobShipments.js"));

            bundles.Add(new Bundle("~/bundles/ordershipping").Include(
                    "~/Scripts/OrderShipping.js"));

            bundles.Add(new Bundle("~/bundles/shipmentshipping").Include(
                    "~/Scripts/ShipmentShipping.js"));

            bundles.Add(new Bundle("~/bundles/eventschedule").Include(
                    "~/Scripts/EventSchedule.js",
                    "~/Scripts/EventScheduleAPI.js"));

            bundles.Add(new Bundle("~/Content/eventschedule").Include(
                    "~/Content/EventSchedule.css"));

            bundles.Add(new Bundle("~/bundles/clientlogin").Include(
                    "~/Scripts/ClientLogin.js"));

            bundles.Add(new Bundle("~/Content/recordsearch").Include(
                    "~/Content/RecordSearch.css"));

            bundles.Add(new Bundle("~/Content/owlcss").Include(
                    "~/Content/OwlCarousel/owl.carousel.css",
                    "~/Content/OwlCarousel/owl.theme.css",
                    "~/Content/OwlCarousel/owl.transitions.css"));

            bundles.Add(new Bundle("~/Content/timetracking").Include(
                    "~/Content/TimeTracking.css"
                ));

            bundles.Add(new Bundle("~/Content/smtt").Include(
                    "~/Content/SideMenuTimeTicker.css"));

            bundles.Add(new Bundle("~/Content/poprint").Include(
                "~/Content/PurchaseOrderPrint.css"));

            bundles.Add(new Bundle("~/bundles/reprint").Include(
                "~/Scripts/reprint.js"));
        }
    }
}