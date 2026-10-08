using System;
using System.Text.Json.Serialization;

namespace Fawry.Models
{
    /// <summary>
    /// Response to a charge request — and the shape of Fawry's payment callback.
    /// Always verify <see cref="Signature"/> before trusting a callback.
    /// </summary>
    public class ChargeResponse
    {
        /// <summary>Response type, e.g. "ChargeResponse".</summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }

        /// <summary>Fawry's transaction reference number.</summary>
        [JsonPropertyName("referenceNumber")]
        public string ReferenceNumber { get; set; }

        /// <summary>Your merchant reference number (echoed back).</summary>
        [JsonPropertyName("merchantRefNumber")]
        public string MerchantRefNumber { get; set; }

        /// <summary>Order amount.</summary>
        [JsonPropertyName("orderAmount")]
        public decimal OrderAmount { get; set; }

        /// <summary>Actually paid amount.</summary>
        [JsonPropertyName("paymentAmount")]
        public decimal PaymentAmount { get; set; }

        /// <summary>Fawry's processing fees, when present.</summary>
        [JsonPropertyName("fawryFees")]
        public decimal? FawryFees { get; set; }

        /// <summary>Shipping fees, when present.</summary>
        [JsonPropertyName("shippingFees")]
        public decimal? ShippingFees { get; set; }

        /// <summary>Payment method used, e.g. "PayAtFawry", "CARD", "MWALLET".</summary>
        [JsonPropertyName("paymentMethod")]
        public string PaymentMethod { get; set; }

        /// <summary>Order status, e.g. "PAID", "UNPAID", "EXPIRED".</summary>
        [JsonPropertyName("orderStatus")]
        public string OrderStatus { get; set; }

        /// <summary>Epoch millis of when the payment was processed.</summary>
        [JsonPropertyName("paymentTime")]
        public long? PaymentTime { get; set; }

        /// <summary>Customer mobile.</summary>
        [JsonPropertyName("customerMobile")]
        public string CustomerMobile { get; set; }

        /// <summary>Customer e-mail.</summary>
        [JsonPropertyName("customerMail")]
        public string CustomerMail { get; set; }

        /// <summary>Payment authentication number, when present.</summary>
        [JsonPropertyName("authNumber")]
        public string AuthNumber { get; set; }

        /// <summary>Customer profile id, when present.</summary>
        [JsonPropertyName("customerProfileId")]
        public string CustomerProfileId { get; set; }

        /// <summary>Response signature — verify with <see cref="FawrySignature.VerifyCallbackSignature"/>.</summary>
        [JsonPropertyName("signature")]
        public string Signature { get; set; }

        /// <summary>Fawry status code: "200" means success.</summary>
        [JsonPropertyName("statusCode")]
        public string StatusCode { get; set; }

        /// <summary>Human-readable status description.</summary>
        [JsonPropertyName("statusDescription")]
        public string StatusDescription { get; set; }

        /// <summary>True when the order status is PAID.</summary>
        [JsonIgnore]
        public bool IsPaid => string.Equals(OrderStatus, "PAID", StringComparison.OrdinalIgnoreCase);

        /// <summary>True when Fawry reported status code 200.</summary>
        [JsonIgnore]
        public bool IsSuccess => StatusCode == "200";
    }
}
