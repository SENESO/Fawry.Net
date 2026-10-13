using System.Text.Json.Serialization;

namespace Fawry.Models
{
    /// <summary>
    /// Response to a refund request (Fawry's refund API).
    /// </summary>
    public class RefundResponse
    {
        /// <summary>Response type, e.g. "ChargeResponse".</summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }

        /// <summary>
        /// Fawry status code: "200" means success.
        /// Fawry sends this as a JSON number on some endpoints and a string on
        /// others; both are read into this string.
        /// </summary>
        [JsonPropertyName("statusCode")]
        [JsonConverter(typeof(NumericStringConverter))]
        public string StatusCode { get; set; }

        /// <summary>Human-readable status description.</summary>
        [JsonPropertyName("statusDescription")]
        public string StatusDescription { get; set; }

        /// <summary>Fawry's order reference number (echoed back).</summary>
        [JsonPropertyName("referenceNumber")]
        public string ReferenceNumber { get; set; }

        /// <summary>Your merchant reference number (echoed back).</summary>
        [JsonPropertyName("merchantRefNumber")]
        public string MerchantRefNumber { get; set; }

        /// <summary>True when Fawry reported status code 200.</summary>
        [JsonIgnore]
        public bool IsSuccess => StatusCode == "200";
    }
}
