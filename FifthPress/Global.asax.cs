using Autofac;
using Autofac.Integration.Mvc;
using FifthPress.Initializers;
using FifthPress.Services;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Configuration;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace FifthPress
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        private FifthPressEntities db;
        private static RecurringOrderProcessingService25 _recurringService;

        protected void Application_Error(object sender, EventArgs e)
        {
            // 1) Grab & clear the exception
            Exception ex = Server.GetLastError();
            Server.ClearError();

            // Generate a unique error reference ID
            string errorRefId = GenerateErrorReferenceId();

            // Capture request info before any redirects
            string requestUrl = "";
            string httpMethod = "";
            string userAgent = "";
            string referrer = "";

            if (HttpContext.Current?.Request != null)
            {
                var request = HttpContext.Current.Request;
                requestUrl = request.Url?.ToString() ?? "";
                httpMethod = request.HttpMethod ?? "";
                userAgent = request.UserAgent ?? "";
                referrer = request.UrlReferrer?.ToString() ?? "";
            }

            // Log the exception details using MastermindLogger
            try
            {
                MastermindLogger.Log(LogLevel.Error, "========== APPLICATION ERROR ==========");
                MastermindLogger.Log(LogLevel.Error, $"Error Reference ID: {errorRefId}");
                MastermindLogger.Log(LogLevel.Error, $"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

                // Log request information
                MastermindLogger.Log(LogLevel.Error, $"URL: {requestUrl}");
                MastermindLogger.Log(LogLevel.Error, $"HTTP Method: {httpMethod}");
                MastermindLogger.Log(LogLevel.Error, $"User Agent: {userAgent}");
                MastermindLogger.Log(LogLevel.Error, $"Referrer: {referrer}");

                // Log exception details
                if (ex != null)
                {
                    MastermindLogger.Log(LogLevel.Error, $"Exception Type: {ex.GetType().FullName}");
                    MastermindLogger.Log(LogLevel.Error, $"Exception Message: {ex.Message}");
                    MastermindLogger.Log(LogLevel.Error, $"Stack Trace: {ex.StackTrace}");

                    // Log inner exceptions
                    var innerEx = ex.InnerException;
                    int innerCount = 1;
                    while (innerEx != null)
                    {
                        MastermindLogger.Log(LogLevel.Error, $"Inner Exception {innerCount} Type: {innerEx.GetType().FullName}");
                        MastermindLogger.Log(LogLevel.Error, $"Inner Exception {innerCount} Message: {innerEx.Message}");
                        MastermindLogger.Log(LogLevel.Error, $"Inner Exception {innerCount} Stack Trace: {innerEx.StackTrace}");
                        innerEx = innerEx.InnerException;
                        innerCount++;
                    }
                }
                else
                {
                    MastermindLogger.Log(LogLevel.Error, "Exception is NULL");
                }

                MastermindLogger.Log(LogLevel.Error, "======================================");
            }
            catch (Exception logEx)
            {
                // If logging fails, at least trace it
                System.Diagnostics.Trace.WriteLine($"[ErrorHandler] Logging failed: {logEx.Message}");
            }

            // 2) Inspect incoming cookies
            var requestCookies = HttpContext.Current.Request.Cookies;
            var allKeys = requestCookies.AllKeys;
            System.Diagnostics.Trace.WriteLine($"[ErrorHandler] Cookies present: {string.Join(",", allKeys)}");

            // 3) Pull out your flag
            var tvCookie = requestCookies.Get("IsTvClient");
            bool isTv = false;
            if (tvCookie != null)
            {
                var val = tvCookie.Value?.Trim();
                System.Diagnostics.Trace.WriteLine($"[ErrorHandler] IsTvClient cookie value: '{val}'");
                isTv = string.Equals(val, "true", StringComparison.OrdinalIgnoreCase);
            }

            // 4) Redirect to error page with reference ID
            string errorRedirectUrl = $"~/Error/Details?refId={errorRefId}";

            if (isTv)
                Response.Redirect("~/tv");
            else
                Response.Redirect(errorRedirectUrl);
        }

        private string GenerateErrorReferenceId()
        {
            // Format: ERR-YYYYMMDD-HHMMSS-XXXX (where XXXX is random alphanumeric)
            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string randomPart = GenerateRandomAlphanumeric(4);
            return $"ERR-{timestamp}-{randomPart}";
        }

        private string GenerateRandomAlphanumeric(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        // CORS configuration for client portal frontend
        protected void Application_BeginRequest()
        {
            var origin = HttpContext.Current.Request.Headers["Origin"];
            var clientViewBaseUrl = System.Configuration.ConfigurationManager.AppSettings["ClientViewBaseUrl"];
            var proofsViewBaseUrl = System.Configuration.ConfigurationManager.AppSettings["ProofsViewBaseUrl"];

            // Allow requests from ClientViewBaseUrl, ProofsViewBaseUrl, or localhost for development
            bool isAllowedOrigin = origin == clientViewBaseUrl ||
                origin == proofsViewBaseUrl ||
                (!string.IsNullOrEmpty(origin) && origin.StartsWith("https://events.fifthcolor.com")) ||
                (!string.IsNullOrEmpty(origin) && origin.StartsWith("https://proofs.fifthcolor.com")) ||
                (!string.IsNullOrEmpty(origin) && origin.StartsWith("http://localhost:"));

            if (isAllowedOrigin)
            {
                HttpContext.Current.Response.AddHeader("Access-Control-Allow-Origin", origin);
                HttpContext.Current.Response.AddHeader("Access-Control-Allow-Credentials", "true");
                HttpContext.Current.Response.AddHeader("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
                HttpContext.Current.Response.AddHeader("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Signature, X-Timestamp, X-Nonce");
            }

            // Handle preflight OPTIONS requests
            if (HttpContext.Current.Request.HttpMethod == "OPTIONS" && isAllowedOrigin)
            {
                HttpContext.Current.Response.StatusCode = 200;
                HttpContext.Current.Response.End();
            }
        }

        protected void Application_Start()
        {
            try
            {
                using (db = new FifthPressEntities())
                {
                    foreach (ActiveUser au in db.ActiveUsers.ToList())
                        db.ActiveUsers.Remove(au);

                    db.SaveChanges();
                }

                MastermindLogger.SetLogFilePath(@"C:\tmp\trouble.txt");

                AreaRegistration.RegisterAllAreas();
                GlobalConfiguration.Configure(WebApiConfig.Register);
                FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
                RouteConfig.RegisterRoutes(RouteTable.Routes);
                BundleConfig.RegisterBundles(BundleTable.Bundles);

                // Start recurring order processing service
                try
                {
                    _recurringService = new RecurringOrderProcessingService25();
                    _recurringService.StartProcessing();
                    PurchaseOrderLogger.Log("[Application_Start] Recurring order processing service started successfully");
                }
                catch (Exception serviceEx)
                {
                    PurchaseOrderLogger.LogError($"[Application_Start] Failed to start recurring service: {serviceEx.Message}");
                    PurchaseOrderLogger.LogException(serviceEx);
                }
            }
            catch (Exception e)
            {
                var x = e.InnerException;
                var y = e.Message;
            }
        }

        protected void Application_End()
        {
            try
            {
                // Stop recurring order processing service
                if (_recurringService != null)
                {
                    _recurringService.StopProcessing();
                    _recurringService.Dispose();
                    PurchaseOrderLogger.Log("[Application_End] Recurring order processing service stopped");
                }
            }
            catch (Exception ex)
            {
                PurchaseOrderLogger.LogError($"[Application_End] Error stopping recurring service: {ex.Message}");
            }
        }
    }
}