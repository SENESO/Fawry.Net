using System;
using System.Text.Json.Serialization;

namespace Fawry.Models
{
    /// <summary>
    /// Response to capture and cancel-authorization operations
    /// (Fawry's "PaymentStatusResponse").
    /// </summary>
    public class OperationResponse
    {
        /// <summary>Response type, e.g. "PaymentStatusResponse".</summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }

        /// <summary>Fawry's reference number for the order.</summary>
        [JsonPropertyName("fawryRefNumber")]
        public string FawryRefNumber { get; set; }

        /// <summary>Your merchant code (echoed back).</summary>
        [JsonPropertyName("merchantCode")]
        public string MerchantCode { get; set; }

        /// <summary>Your merchant reference number (echoed back).</summary>
        [JsonPropertyName("merchantRefNumber")]
        public string MerchantRefNumber { get; set; }

        /// <summary>Order status, e.g. "PAID", "CANCELED".</summary>
        [JsonPropertyName("orderStatus")]
        public string OrderStatus { get; set; }

        /// <summary>Fawry status code: "200" means success.</summary>
        [JsonPropertyName("statusCode")]
        public string StatusCode { get; set; }

        /// <summary>Human-readable status description.</summary>
        [JsonPropertyName("statusDescription")]
        public string StatusDescription { get; set; }

        /// <summary>True when Fawry reported status code 200.</summary>
        [JsonIgnore]
        public bool IsSuccess => StatusCode == "200";
    }
}
