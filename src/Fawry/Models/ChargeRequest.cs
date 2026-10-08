using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Fawry.Models
{
    /// <summary>Payment methods supported by FawryPay charge APIs.</summary>
    public static class FawryPaymentMethods
    {
        /// <summary>Pay with a reference number at any Fawry outlet (cash).</summary>
        public const string PayAtFawry = "PAYATFAWRY";
        /// <summary>Pay with a bank card.</summary>
        public const string Card = "CARD";
        /// <summary>Pay with a mobile wallet (e.g. Vodafone Cash).</summary>
        public const string MobileWallet = "MWALLET";
        /// <summary>ValU installments.</summary>
        public const string Valu = "VALU";
    }

    /// <summary>One line item in a charge request.</summary>
    public class ChargeItem
    {
        /// <summary>Item id in your system.</summary>
        [JsonPropertyName("itemId")]
        public string ItemId { get; set; }

        /// <summary>Item description.</summary>
        [JsonPropertyName("description")]
        public string Description { get; set; }

        /// <summary>Price per unit.</summary>
        [JsonIgnore]
        public decimal Price { get; set; }

        /// <summary>Quantity.</summary>
        [JsonIgnore]
        public decimal Quantity { get; set; }
    }

    /// <summary>
    /// A charge request for Pay-at-Fawry and mobile-wallet payments.
    /// The client computes the request signature for you.
    /// </summary>
    public class ChargeRequest
    {
        /// <summary>Your unique order reference. Must be unique per charge.</summary>
        public string MerchantRefNum { get; set; }

        /// <summary>Customer full name.</summary>
        public string CustomerName { get; set; }

        /// <summary>Customer mobile, e.g. "01xxxxxxxxx".</summary>
        public string CustomerMobile { get; set; }

        /// <summary>Customer e-mail.</summary>
        public string CustomerEmail { get; set; }

        /// <summary>
        /// Optional customer profile id in your system (e.g. user id).
        /// Included in the signature as "" when not set.
        /// </summary>
        public string CustomerProfileId { get; set; }

        /// <summary>Charge amount.</summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// How long the charge stays payable. Defaults to 24 hours from now.
        /// Fawry expects epoch milliseconds.
        /// </summary>
        public DateTimeOffset PaymentExpiry { get; set; } = DateTimeOffset.UtcNow.AddHours(24);

        /// <summary>Order line items.</summary>
        public List<ChargeItem> ChargeItems { get; set; } = new List<ChargeItem>();

        /// <summary>Optional order description.</summary>
        public string Description { get; set; }
    }

    /// <summary>
    /// A card charge request. Supports both immediate capture and
    /// authorize-now-capture-later (set <see cref="AuthCaptureMode"/> to true,
    /// then call <see cref="FawryClient.CaptureAsync"/>).
    /// </summary>
    public class CardChargeRequest : ChargeRequest
    {
        /// <summary>Card number (digits only).</summary>
        public string CardNumber { get; set; }

        /// <summary>Card expiry year, two digits (e.g. "27").</summary>
        public string CardExpiryYear { get; set; }

        /// <summary>Card expiry month, two digits (e.g. "05").</summary>
        public string CardExpiryMonth { get; set; }

        /// <summary>Card CVV.</summary>
        public string Cvv { get; set; }

        /// <summary>
        /// Set to true to only authorize the amount now and capture it later
        /// with <see cref="FawryClient.CaptureAsync"/>. Default false (immediate charge).
        /// </summary>
        public bool AuthCaptureMode { get; set; }

        /// <summary>Request 3-D Secure. Default true.</summary>
        public bool Enable3DS { get; set; } = true;
    }
}
