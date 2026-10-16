using FifthPress.Models.CustomerQuoteModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_Invoice2
    {
        public Job Job { get; set; }
        public List<VM_InvoiceRow2> Rows { get; set; }
        public decimal TotalPrice { get; set; }
        public string TotalPriceString { get; set; }
        public SourceInformation SourceInformation { get; set; }
        public ClientInformation ClientInformation { get; set; }
        public ShippingInformation ShippingInformation { get; set; }
        public decimal TotalWithFreight { get; set; }
        public string TotalWithFreightString { get; set; }
        public decimal TaxPercent { get; set; } = .055m;
        public decimal TaxAmount { get; set; }
        public string TaxAmountString { get; set; }
        public decimal TotalWithTax { get; set; }
        public string TotalWithTaxString { get; set; }
        public List<OrdersShipped> Shipments { get; set; }
        public List<ShipmentsShipped> GroupedShipments { get; set; }
        public Dictionary<int, decimal> GroupedShipmentShares { get; set; } = new Dictionary<int, decimal>();

        public decimal TotalFreightPrice { get; set; }
        public string TotalFreightPriceString { get; set; }

        public decimal FinalTotal { get; set; }
        public string FinalTotalString { get; set; }
    }
}