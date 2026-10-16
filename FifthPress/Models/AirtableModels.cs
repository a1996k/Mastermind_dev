using System;
using System.Collections.Generic;

namespace FifthPress.Models
{
    #region Airtable Schema Models

    /// <summary>
    /// Response from Airtable Get Schema API
    /// </summary>
    public class AirtableSchemaResponse
    {
        public List<AirtableTableSchema> Tables { get; set; }
    }

    /// <summary>
    /// Individual table schema from Airtable
    /// </summary>
    public class AirtableTableSchema
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string PrimaryFieldId { get; set; }
        public List<AirtableFieldSchema> Fields { get; set; }
    }

    /// <summary>
    /// Field schema from Airtable table
    /// </summary>
    public class AirtableFieldSchema
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
    }

    #endregion

    #region Airtable Records Models

    /// <summary>
    /// Response from Airtable List Records API
    /// </summary>
    public class AirtableRecordsResponse
    {
        public List<AirtableRecord> Records { get; set; }
        public string Offset { get; set; }
    }

    /// <summary>
    /// Individual record from Airtable
    /// </summary>
    public class AirtableRecord
    {
        public string Id { get; set; }
        public string CreatedTime { get; set; }
        public Dictionary<string, object> Fields { get; set; }
    }

    #endregion

    #region Onsite Printing Models

    /// <summary>
    /// Parsed onsite print request from Airtable
    /// </summary>
    public class OnsitePrintRequest
    {
        /// <summary>
        /// Airtable record ID
        /// </summary>
        public string RecordId { get; set; }

        /// <summary>
        /// Date requested
        /// </summary>
        public DateTime? DateRequested { get; set; }

        /// <summary>
        /// Person who requested the sign
        /// </summary>
        public string Requestor { get; set; }

        /// <summary>
        /// Status of the request (e.g., "Discussion", "Approved", "Done-Ready For Install")
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Description of the item
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Quantity of signs
        /// </summary>
        public decimal Quantity { get; set; }

        /// <summary>
        /// Whether FC installed the sign (YES/NO)
        /// </summary>
        public string FcInstalled { get; set; }

        /// <summary>
        /// Date completed
        /// </summary>
        public DateTime? DateCompleted { get; set; }

        /// <summary>
        /// Length in inches
        /// </summary>
        public decimal? Length { get; set; }

        /// <summary>
        /// Width in inches
        /// </summary>
        public decimal? Width { get; set; }

        /// <summary>
        /// Square feet per sign
        /// </summary>
        public decimal? PerSignSquareFeet { get; set; }

        /// <summary>
        /// Total square feet (Quantity × PerSignSquareFeet)
        /// </summary>
        public decimal? TotalSquareFeet { get; set; }

        /// <summary>
        /// Calculated cost based on square footage and pricing
        /// </summary>
        public decimal CalculatedCost { get; set; }
    }

    #endregion
}
