using RestSharp;
using RestSharp.Authenticators;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using FifthPress.Models.API.BambooHR;
using Newtonsoft.Json;

namespace FifthPress
{
    public class BambooHRConnector
    {
        private readonly string ApiKey;
        private readonly string Subdomain;
        private readonly string BaseUrl;

        // BambooHR API endpoints
        private readonly string EmployeeDirectoryEndpoint = "/employees/directory";
        private readonly string TimesheetEntriesEndpoint = "/time_tracking/timesheet_entries";
        private readonly string TimeOffRequestsEndpoint = "/time_off/requests/";
        private readonly string WhosOutEndpoint = "/time_off/whos_out/";

        public BambooHRConnector()
        {
            ApiKey = ConfigurationManager.AppSettings["BambooHRApiKey"];
            Subdomain = ConfigurationManager.AppSettings["BambooHRSubdomain"];
            BaseUrl = $"https://api.bamboohr.com/api/gateway.php/{Subdomain}/v1";
        }

        /// <summary>
        /// Build RestClient with Basic Auth (API key as username, "x" as password)
        /// </summary>
        private RestClient BuildClient()
        {
            var options = new RestClientOptions(BaseUrl)
            {
                Authenticator = new HttpBasicAuthenticator(ApiKey, "x")
            };
            return new RestClient(options);
        }

        /// <summary>
        /// Build request with standard BambooHR headers
        /// </summary>
        private RestRequest BuildRequest(string endpoint, Method method)
        {
            var request = new RestRequest(endpoint, method);
            request.AddHeader("Accept", "application/json");
            return request;
        }

        /// <summary>
        /// Get all employees from directory
        /// </summary>
        public async Task<BambooHREmployeeDirectory> GetEmployees()
        {
            var client = BuildClient();
            var request = BuildRequest(EmployeeDirectoryEndpoint, Method.Get);

            var response = await client.ExecuteAsync(request);

            if (response.IsSuccessful)
            {
                return JsonConvert.DeserializeObject<BambooHREmployeeDirectory>(response.Content);
            }
            else
            {
                throw new Exception($"BambooHR GetEmployees failed: {response.StatusCode} - {response.ErrorMessage}");
            }
        }

        /// <summary>
        /// Get timesheet entries for a date range
        /// </summary>
        /// <param name="startDate">Start date for query</param>
        /// <param name="endDate">End date for query</param>
        /// <param name="employeeId">Optional: specific employee ID</param>
        public async Task<List<BambooHRClockEntry>> GetClockEntries(DateTime startDate, DateTime endDate, string employeeId = null)
        {
            var client = BuildClient();
            var request = BuildRequest(TimesheetEntriesEndpoint, Method.Get);

            request.AddQueryParameter("start", startDate.ToString("yyyy-MM-dd"));
            request.AddQueryParameter("end", endDate.ToString("yyyy-MM-dd"));

            if (!string.IsNullOrEmpty(employeeId))
            {
                request.AddQueryParameter("employeeIds", employeeId);
            }

            var response = await client.ExecuteAsync(request);

            if (response.IsSuccessful)
            {
                // Return empty list if no content
                if (string.IsNullOrEmpty(response.Content) || response.Content == "[]")
                {
                    return new List<BambooHRClockEntry>();
                }
                return JsonConvert.DeserializeObject<List<BambooHRClockEntry>>(response.Content) ?? new List<BambooHRClockEntry>();
            }
            else
            {
                // Include response content for debugging
                string errorDetails = !string.IsNullOrEmpty(response.Content) ? response.Content : response.ErrorMessage;
                throw new Exception($"BambooHR GetClockEntries failed: {response.StatusCode} - {errorDetails}");
            }
        }

        /// <summary>
        /// Get time-off requests for a date range
        /// </summary>
        /// <param name="startDate">Start date for query</param>
        /// <param name="endDate">End date for query</param>
        public async Task<List<BambooHRTimeOffRequest>> GetTimeOffRequests(DateTime startDate, DateTime endDate)
        {
            var client = BuildClient();
            var request = BuildRequest(TimeOffRequestsEndpoint, Method.Get);

            request.AddQueryParameter("start", startDate.ToString("yyyy-MM-dd"));
            request.AddQueryParameter("end", endDate.ToString("yyyy-MM-dd"));
            request.AddQueryParameter("status", "approved");

            var response = await client.ExecuteAsync(request);

            if (response.IsSuccessful)
            {
                // Return empty list if no content
                if (string.IsNullOrEmpty(response.Content) || response.Content == "[]")
                {
                    return new List<BambooHRTimeOffRequest>();
                }
                return JsonConvert.DeserializeObject<List<BambooHRTimeOffRequest>>(response.Content) ?? new List<BambooHRTimeOffRequest>();
            }
            else
            {
                // Include response content for debugging
                string errorDetails = !string.IsNullOrEmpty(response.Content) ? response.Content : response.ErrorMessage;
                throw new Exception($"BambooHR GetTimeOffRequests failed: {response.StatusCode} - {errorDetails}");
            }
        }

        /// <summary>
        /// Get who's out data for a date range. Returns XML with holidays and time-off items separated by type.
        /// </summary>
        public async Task<List<BambooHRWhosOutItem>> GetWhosOut(DateTime startDate, DateTime endDate)
        {
            var client = BuildClient();
            // Don't use BuildRequest — WhosOut returns XML, not JSON
            var request = new RestRequest(WhosOutEndpoint, Method.Get);
            request.AddQueryParameter("start", startDate.ToString("yyyy-MM-dd"));
            request.AddQueryParameter("end", endDate.ToString("yyyy-MM-dd"));

            var response = await client.ExecuteAsync(request);

            if (!response.IsSuccessful)
            {
                string errorDetails = !string.IsNullOrEmpty(response.Content) ? response.Content : response.ErrorMessage;
                throw new Exception($"BambooHR GetWhosOut failed: {response.StatusCode} - {errorDetails}");
            }

            if (string.IsNullOrEmpty(response.Content))
            {
                return new List<BambooHRWhosOutItem>();
            }

            var items = new List<BambooHRWhosOutItem>();
            var doc = XDocument.Parse(response.Content);

            foreach (var itemEl in doc.Descendants("item"))
            {
                string type = itemEl.Attribute("type")?.Value;
                if (string.IsNullOrEmpty(type)) continue;

                var startEl = itemEl.Element("start");
                var endEl = itemEl.Element("end");

                if (startEl == null) continue;

                DateTime parsedStart;
                if (!DateTime.TryParse(startEl.Value, out parsedStart)) continue;

                DateTime parsedEnd = parsedStart;
                if (endEl != null)
                    DateTime.TryParse(endEl.Value, out parsedEnd);

                var whosOutItem = new BambooHRWhosOutItem
                {
                    Type = type,
                    Start = parsedStart,
                    End = parsedEnd
                };

                if (type == "holiday")
                {
                    var holidayEl = itemEl.Element("holiday");
                    if (holidayEl != null)
                    {
                        whosOutItem.Id = holidayEl.Attribute("id")?.Value;
                        whosOutItem.HolidayName = holidayEl.Value;
                    }
                }
                else if (type == "timeOff")
                {
                    var requestEl = itemEl.Element("request");
                    if (requestEl != null)
                        whosOutItem.Id = requestEl.Attribute("id")?.Value;

                    var employeeEl = itemEl.Element("employee");
                    if (employeeEl != null)
                    {
                        whosOutItem.EmployeeId = employeeEl.Attribute("id")?.Value;
                        whosOutItem.EmployeeName = employeeEl.Value;
                    }
                }

                items.Add(whosOutItem);
            }

            return items;
        }
    }
}
