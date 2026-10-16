namespace FifthPress
{
    using System.ComponentModel.DataAnnotations.Schema;

    /// <summary>
    /// Partial class to extend auto-generated ContractForecast with additional properties.
    /// </summary>
    public partial class ContractForecast
    {
        /// <summary>
        /// Links to an ExternalProject (Paymo project) when this contract was converted from an imported project.
        /// Used to prevent duplicate conversions and track provenance.
        /// </summary>
        [Column("LinkedExternalProjectId")]
        public int? LinkedExternalProjectId { get; set; }
    }
}
