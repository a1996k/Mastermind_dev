using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace FifthPress.Services
{
    /// <summary>
    /// Service for interacting with Avalara AvaTax API
    /// Handles address validation and tax calculation
    /// </summary>
    public class AvalaraService
    {
        private readonly string _accountId;
        private readonly string _licenseKey;
        private readonly string _companyCode;
        private readonly string _baseUrl;
        private static readonly HttpClient _httpClient = new HttpClient();

        public AvalaraService()
        {
            _accountId = ConfigurationManager.AppSettings["AvalaraAccountId"];
            _licenseKey = ConfigurationManager.AppSettings["AvalaraLicenseKey"];
            _companyCode = ConfigurationManager.AppSettings["AvalaraCompanyCode"];

            var environment = ConfigurationManager.AppSettings["AvalaraEnvironment"];
            _baseUrl = environment == "Production"
                ? "https://rest.avatax.com"
                : "https://sandbox-rest.avatax.com";

            // Configure HttpClient (only once)
            if (_httpClient.DefaultRequestHeaders.Authorization == null)
            {
                var credentials = Convert.ToBase64String(
                    Encoding.ASCII.GetBytes($"{_accountId}:{_licenseKey}"));
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Basic", credentials);
                _httpClient.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));
            }
        }

        /// <summary>
        /// Calculate tax for an event using Avalara's CreateTransaction API
        /// </summary>
        public async Task<AvalaraTaxResult> CalculateTaxForEventAsync(
            string city,
            string stateCode,
            string postalCode,
            List<TaxableLineItem> lineItems,
            string customerCode = "GUEST")
        {
            try
            {
                var transactionRequest = new
                {
                    type = "SalesOrder",
                    companyCode = _companyCode,
                    date = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                    customerCode = customerCode,
                    addresses = new
                    {
                        shipTo = new
                        {
                            city = city,
                            region = stateCode,
                            postalCode = postalCode,
                            country = "US"
                        }
                    },
                    lines = lineItems.Select((item, index) => new
                    {
                        number = (index + 1).ToString(),
                        quantity = item.Quantity,
                        amount = item.Amount,
                        taxCode = item.TaxCode,
                        description = item.Description
                    }).ToList(),
                    commit = false // Don't commit the transaction, just calculate
                };

                var url = $"{_baseUrl}/api/v2/transactions/create";

                var content = new StringContent(
                    JsonConvert.SerializeObject(transactionRequest),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync(url, content);
                var jsonResponse = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    // Log error but return empty result rather than failing
                    return new AvalaraTaxResult();
                }

                var avalaraResponse = JsonConvert.DeserializeObject<AvalaTaxCalculationResponse>(jsonResponse);

                var result = new AvalaraTaxResult { TotalTax = avalaraResponse?.totalTax ?? 0m };

                if (avalaraResponse?.lines != null)
                {
                    foreach (var line in avalaraResponse.lines)
                    {
                        if (int.TryParse(line.lineNumber, out int lineNum))
                            result.TaxByLineIndex[lineNum - 1] = line.tax;  // 1-based → 0-based
                    }

                    // All lines in a category share the same jurisdiction → use first line's details
                    // to get the exact combined rate (sum of state + county + special rates)
                    var firstLine = avalaraResponse.lines.FirstOrDefault();
                    if (firstLine?.details != null && firstLine.details.Any())
                    {
                        result.EffectiveTaxRate = firstLine.details.Sum(d => d.rate) * 100; // → e.g. 6.0
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                // Log error but return empty result rather than failing the entire request
                return new AvalaraTaxResult();
            }
        }

        /// <summary>
        /// Validate a shipping address using Avalara's address validation API
        /// </summary>
        public async Task<AddressValidationResult> ValidateAddressAsync(
            string line1,
            string line2,
            string city,
            string region,
            string postalCode,
            string country = "US")
        {
            try
            {
                var addressRequest = new
                {
                    line1 = line1,
                    line2 = line2,
                    city = city,
                    region = region,
                    postalCode = postalCode,
                    country = country
                };

                var url = $"{_baseUrl}/api/v2/addresses/resolve";
                var content = new StringContent(
                    JsonConvert.SerializeObject(addressRequest),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync(url, content);
                var jsonResponse = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Avalara API error: {response.StatusCode} - {jsonResponse}");
                }

                var avalaraResponse = JsonConvert.DeserializeObject<AvalaraAddressResponse>(jsonResponse);

                // Determine if address is valid based on resolution quality
                var isValid = avalaraResponse.resolutionQuality == "External" ||
                              avalaraResponse.resolutionQuality == "Intersection" ||
                              avalaraResponse.resolutionQuality == "Interpolated";

                var result = new AddressValidationResult
                {
                    IsValid = isValid,
                    ResolutionQuality = avalaraResponse.resolutionQuality,
                    Messages = avalaraResponse.messages ?? new System.Collections.Generic.List<AvalaraMessage>(),
                    OriginalAddress = new AddressInfo
                    {
                        Line1 = line1,
                        Line2 = line2,
                        City = city,
                        Region = region,
                        PostalCode = postalCode,
                        Country = country
                    }
                };

                // If Avalara provided a validated address, include it
                if (avalaraResponse.validatedAddresses != null &&
                    avalaraResponse.validatedAddresses.Count > 0)
                {
                    var validated = avalaraResponse.validatedAddresses[0];
                    result.SuggestedAddress = new AddressInfo
                    {
                        Line1 = validated.line1,
                        Line2 = validated.line2,
                        City = validated.city,
                        Region = validated.region,
                        PostalCode = validated.postalCode,
                        Country = validated.country
                    };
                }

                return result;
            }
            catch (Exception ex)
            {
                // Return error result rather than throwing
                return new AddressValidationResult
                {
                    IsValid = false,
                    ResolutionQuality = "Error",
                    Messages = new System.Collections.Generic.List<AvalaraMessage>
                    {
                        new AvalaraMessage
                        {
                            summary = "Address validation failed",
                            details = ex.Message,
                            severity = "Error"
                        }
                    },
                    OriginalAddress = new AddressInfo
                    {
                        Line1 = line1,
                        Line2 = line2,
                        City = city,
                        Region = region,
                        PostalCode = postalCode,
                        Country = country
                    }
                };
            }
        }
    }

    #region Response Models

    /// <summary>
    /// Result of address validation
    /// </summary>
    public class AddressValidationResult
    {
        public bool IsValid { get; set; }
        public string ResolutionQuality { get; set; }
        public System.Collections.Generic.List<AvalaraMessage> Messages { get; set; }
        public AddressInfo OriginalAddress { get; set; }
        public AddressInfo SuggestedAddress { get; set; }
    }

    /// <summary>
    /// Address information
    /// </summary>
    public class AddressInfo
    {
        public string Line1 { get; set; }
        public string Line2 { get; set; }
        public string City { get; set; }
        public string Region { get; set; }
        public string PostalCode { get; set; }
        public string Country { get; set; }
    }

    /// <summary>
    /// Avalara API address response
    /// </summary>
    internal class AvalaraAddressResponse
    {
        public string resolutionQuality { get; set; }
        public System.Collections.Generic.List<AvalaraValidatedAddress> validatedAddresses { get; set; }
        public System.Collections.Generic.List<AvalaraMessage> messages { get; set; }
    }

    /// <summary>
    /// Validated address from Avalara
    /// </summary>
    internal class AvalaraValidatedAddress
    {
        public string line1 { get; set; }
        public string line2 { get; set; }
        public string city { get; set; }
        public string region { get; set; }
        public string postalCode { get; set; }
        public string country { get; set; }
    }

    /// <summary>
    /// Message from Avalara API
    /// </summary>
    public class AvalaraMessage
    {
        public string summary { get; set; }
        public string details { get; set; }
        public string severity { get; set; }
    }

    /// <summary>
    /// Taxable line item for tax calculation
    /// </summary>
    public class TaxableLineItem
    {
        public string Description { get; set; }
        public decimal Amount { get; set; }
        public string TaxCode { get; set; }
        public int Quantity { get; set; }
    }

    /// <summary>
    /// Avalara tax calculation response
    /// </summary>
    internal class AvalaTaxCalculationResponse
    {
        public decimal totalTax { get; set; }
        public decimal totalAmount { get; set; }
        public List<AvalaTaxLine> lines { get; set; }
    }

    internal class AvalaTaxLine
    {
        public string lineNumber { get; set; }
        public decimal tax { get; set; }
        public List<AvalaTaxLineDetail> details { get; set; }
    }

    internal class AvalaTaxLineDetail
    {
        public decimal rate { get; set; }   // e.g. 0.043000 for VA state
    }

    public class AvalaraTaxResult
    {
        public decimal TotalTax { get; set; }
        /// <summary>Key = 0-based index matching the input taxableLineItems list order</summary>
        public Dictionary<int, decimal> TaxByLineIndex { get; set; } = new Dictionary<int, decimal>();
        /// <summary>
        /// Effective tax rate as a percentage (e.g. 6.0 for 6%), sourced by summing
        /// jurisdiction rates from Avalara's first line details. Exact — not back-calculated.
        /// </summary>
        public decimal EffectiveTaxRate { get; set; }
    }

    #endregion
}
