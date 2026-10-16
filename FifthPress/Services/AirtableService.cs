using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using FifthPress.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FifthPress.Services
{
    /// <summary>
    /// Service for interacting with Airtable API to fetch onsite printing requests
    /// </summary>
    public class AirtableService
    {
        private readonly string _apiToken;
        private readonly string _baseId;
        private readonly decimal _pricePerSqFt;
        private static readonly HttpClient _httpClient = new HttpClient();

        // Constructor that accepts baseId as parameter (per-client)
        public AirtableService(string baseId, decimal? sqftPriceFromDb = null)
        {
            _apiToken = ConfigurationManager.AppSettings["AirtableApiToken"];
            _baseId = baseId; // Use the passed baseId

            // Priority: 1) Database value, 2) Config value, 3) Default 10.00
            if (sqftPriceFromDb.HasValue && sqftPriceFromDb.Value > 0)
            {
                _pricePerSqFt = sqftPriceFromDb.Value;
            }
            else
            {
                var priceStr = ConfigurationManager.AppSettings["OnsitePrintingPricePerSqFt"];
                _pricePerSqFt = decimal.TryParse(priceStr, out decimal price) ? price : 10.00m;
            }

            // Configure HttpClient (only once)
            if (_httpClient.DefaultRequestHeaders.Authorization == null)
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _apiToken);
                _httpClient.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));
            }
        }

        /// <summary>
        /// Get schema for the Airtable base
        /// </summary>
        public async Task<AirtableSchemaResponse> GetSchemaAsync()
        {
            try
            {
                var url = $"https://api.airtable.com/v0/meta/bases/{_baseId}/tables";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Airtable Schema API error: {response.StatusCode} - {errorContent}");
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var schema = JsonConvert.DeserializeObject<AirtableSchemaResponse>(jsonContent);

                return schema;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get Airtable schema: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Find the onsite printing table from schema
        /// Looks for table name containing "Onsite" and "Print"
        /// </summary>
        public AirtableTableSchema FindOnsitePrintingTable(AirtableSchemaResponse schema)
        {
            if (schema?.Tables == null || !schema.Tables.Any())
                return null;

            // Look for table with "Onsite" and "Print" in the name (case-insensitive)
            var table = schema.Tables.FirstOrDefault(t =>
                t.Name != null &&
                t.Name.IndexOf("Onsite", StringComparison.OrdinalIgnoreCase) >= 0 &&
                t.Name.IndexOf("Print", StringComparison.OrdinalIgnoreCase) >= 0);

            return table;
        }

        /// <summary>
        /// Get records from a specific table
        /// </summary>
        public async Task<List<AirtableRecord>> GetTableRecordsAsync(string tableIdOrName)
        {
            try
            {
                var allRecords = new List<AirtableRecord>();
                string offset = null;

                do
                {
                    var url = $"https://api.airtable.com/v0/{_baseId}/{Uri.EscapeDataString(tableIdOrName)}";
                    if (!string.IsNullOrEmpty(offset))
                    {
                        url += $"?offset={offset}";
                    }

                    var response = await _httpClient.GetAsync(url);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        throw new Exception($"Airtable Records API error: {response.StatusCode} - {errorContent}");
                    }

                    var jsonContent = await response.Content.ReadAsStringAsync();
                    var recordsResponse = JsonConvert.DeserializeObject<AirtableRecordsResponse>(jsonContent);

                    if (recordsResponse?.Records != null)
                    {
                        allRecords.AddRange(recordsResponse.Records);
                    }

                    offset = recordsResponse?.Offset;

                } while (!string.IsNullOrEmpty(offset));

                return allRecords;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get Airtable records: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get records from a table by its name (looks up table ID from schema first)
        /// </summary>
        public async Task<List<AirtableRecord>> GetTableRecordsByNameAsync(string tableName)
        {
            try
            {
                var schema = await GetSchemaAsync();
                var table = schema?.Tables?.FirstOrDefault(t =>
                    t.Name != null && t.Name.Equals(tableName, StringComparison.OrdinalIgnoreCase));

                if (table == null)
                {
                    return new List<AirtableRecord>();
                }

                return await GetTableRecordsAsync(table.Id);
            }
            catch (Exception)
            {
                return new List<AirtableRecord>();
            }
        }

        /// <summary>
        /// Parse Airtable records into OnsitePrintRequest objects
        /// </summary>
        public List<OnsitePrintRequest> ParseOnsitePrintRequests(List<AirtableRecord> records)
        {
            var printRequests = new List<OnsitePrintRequest>();

            foreach (var record in records)
            {
                try
                {
                    var request = new OnsitePrintRequest
                    {
                        RecordId = record.Id
                    };

                    // Parse fields - handle various field name variations
                    if (record.Fields.TryGetValue("Date Requested", out object dateRequested))
                    {
                        request.DateRequested = ParseDateTime(dateRequested);
                    }

                    if (record.Fields.TryGetValue("Requestor", out object requestor))
                    {
                        request.Requestor = requestor?.ToString();
                    }

                    if (record.Fields.TryGetValue("Status", out object status))
                    {
                        request.Status = status?.ToString();
                    }

                    if (record.Fields.TryGetValue("Description", out object description))
                    {
                        request.Description = description?.ToString();
                    }

                    if (record.Fields.TryGetValue("Quantity", out object quantity))
                    {
                        request.Quantity = ParseDecimal(quantity);
                    }

                    if (record.Fields.TryGetValue("FC Install?", out object fcInstalled))
                    {
                        request.FcInstalled = fcInstalled?.ToString();
                    }

                    if (record.Fields.TryGetValue("Date Completed", out object dateCompleted))
                    {
                        request.DateCompleted = ParseDateTime(dateCompleted);
                    }

                    if (record.Fields.TryGetValue("Length (Inches)", out object length))
                    {
                        request.Length = ParseDecimal(length);
                    }

                    if (record.Fields.TryGetValue("Width (Inches)", out object width))
                    {
                        request.Width = ParseDecimal(width);
                    }

                    if (record.Fields.TryGetValue("PerSign-Square Feet", out object perSignSqFt))
                    {
                        request.PerSignSquareFeet = ParseDecimal(perSignSqFt);
                    }

                    if (record.Fields.TryGetValue("Total Square Feet", out object totalSqFt))
                    {
                        request.TotalSquareFeet = ParseDecimal(totalSqFt);
                    }

                    // Calculate cost based on total square feet
                    if (request.TotalSquareFeet.HasValue && request.TotalSquareFeet.Value > 0)
                    {
                        request.CalculatedCost = request.TotalSquareFeet.Value * _pricePerSqFt;
                    }

                    printRequests.Add(request);
                }
                catch (Exception ex)
                {
                    // Log but don't fail entire operation for one bad record
                    Console.WriteLine($"Error parsing Airtable record {record.Id}: {ex.Message}");
                }
            }

            return printRequests;
        }

        /// <summary>
        /// Get all onsite print requests with calculated costs
        /// </summary>
        public async Task<List<OnsitePrintRequest>> GetOnsitePrintRequestsAsync()
        {
            try
            {
                // Step 1: Get schema
                var schema = await GetSchemaAsync();

                // Step 2: Find onsite printing table
                var onsiteTable = FindOnsitePrintingTable(schema);
                if (onsiteTable == null)
                {
                    throw new Exception("Onsite Printing table not found in Airtable schema");
                }

                // Step 3: Get records
                var records = await GetTableRecordsAsync(onsiteTable.Id);

                // Step 4: Parse into OnsitePrintRequest objects
                var printRequests = ParseOnsitePrintRequests(records);

                return printRequests;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get onsite print requests: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Calculate total onsite printing cost for display in events list
        /// </summary>
        public async Task<decimal> CalculateTotalOnsitePrintingCostAsync()
        {
            try
            {
                var requests = await GetOnsitePrintRequestsAsync();
                return requests.Sum(r => r.CalculatedCost);
            }
            catch (Exception ex)
            {
                // Return 0 on error rather than failing the whole request
                Console.WriteLine($"Error calculating onsite printing cost: {ex.Message}");
                return 0m;
            }
        }

        #region Helper Methods

        private DateTime? ParseDateTime(object value)
        {
            if (value == null) return null;

            if (value is DateTime dt)
                return dt;

            if (DateTime.TryParse(value.ToString(), out DateTime parsed))
                return parsed;

            return null;
        }

        private decimal ParseDecimal(object value)
        {
            if (value == null) return 0m;

            if (value is decimal dec)
                return dec;

            if (value is double dbl)
                return (decimal)dbl;

            if (value is int i)
                return i;

            if (value is long l)
                return l;

            if (decimal.TryParse(value.ToString(), out decimal parsed))
                return parsed;

            return 0m;
        }

        #endregion
    }
}
