using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Fawry.Models;

namespace Fawry
{
    /// <summary>
    /// FawryPay request signing and callback verification.
    /// All recipes follow Fawry's official documentation
    /// (https://developer.fawrystaging.com): lowercase hex SHA-256 over
    /// concatenated fields, amounts always in two-decimal format ("580.55").
    /// </summary>
    public static class FawrySignature
    {
        /// <summary>Formats an amount the way Fawry expects it: two decimals, invariant culture.</summary>
        public static string FormatAmount(decimal amount)
            => amount.ToString("0.00", CultureInfo.InvariantCulture);

        /// <summary>Lowercase hex SHA-256 of the UTF-8 bytes of <paramref name="input"/>.</summary>
        public static string Sha256Hex(string input)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input ?? string.Empty));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>
        /// Charge request signature for PAYATFAWRY / MWALLET / VALU:
        /// merchantCode + merchantRefNum + customerProfileId (or "") + paymentMethod + amount (2dp) + secureKey
        /// </summary>
        public static string ComputeChargeSignature(
            string merchantCode,
            string merchantRefNum,
            string customerProfileId,
            string paymentMethod,
            decimal amount,
            string secureKey)
            => Sha256Hex(merchantCode + merchantRefNum + (customerProfileId ?? string.Empty)
                + paymentMethod + FormatAmount(amount) + secureKey);

        /// <summary>
        /// Card charge signature (Fawry's "Authorize and Capture Payments" docs):
        /// merchantCode + merchantRefNum + customerProfileId + paymentMethod + amount (2dp)
        /// + cardNumber + cardExpiryYear + cardExpiryMonth + cvv + secureKey
        /// </summary>
        public static string ComputeCardChargeSignature(
            string merchantCode,
            string merchantRefNum,
            string customerProfileId,
            string paymentMethod,
            decimal amount,
            string cardNumber,
            string cardExpiryYear,
            string cardExpiryMonth,
            string cvv,
            string secureKey)
            => Sha256Hex(merchantCode + merchantRefNum + (customerProfileId ?? string.Empty)
                + paymentMethod + FormatAmount(amount)
                + cardNumber + cardExpiryYear + cardExpiryMonth + cvv + secureKey);

        /// <summary>
        /// Capture signature: merchantRefNum + captureAmount (2dp, or "" when capturing the full amount) + merchantCode + secureKey
        /// </summary>
        public static string ComputeCaptureSignature(
            string merchantCode,
            string merchantRefNum,
            decimal? captureAmount,
            string secureKey)
            => Sha256Hex(merchantRefNum
                + (captureAmount.HasValue ? FormatAmount(captureAmount.Value) : string.Empty)
                + merchantCode + secureKey);

        /// <summary>
        /// Cancel-authorization signature: merchantRefNum + merchantCode + secureKey
        /// </summary>
        public static string ComputeCancelSignature(
            string merchantCode,
            string merchantRefNum,
            string secureKey)
            => Sha256Hex(merchantRefNum + merchantCode + secureKey);

        /// <summary>
        /// Refund signature (Fawry's "Refund API" docs):
        /// merchantCode + referenceNumber + refundAmount (2dp) + reason (or "") + secureKey
        /// </summary>
        public static string ComputeRefundSignature(
            string merchantCode,
            string referenceNumber,
            decimal refundAmount,
            string reason,
            string secureKey)
            => Sha256Hex((merchantCode ?? string.Empty) + (referenceNumber ?? string.Empty)
                + FormatAmount(refundAmount) + (reason ?? string.Empty) + secureKey);

        /// <summary>
        /// V2 server-to-server notification signature
        /// (Fawry's "Server To Server Notification V2" docs):
        /// fawryRefNumber + merchantRefNumber + paymentAmount (2dp) + orderAmount (2dp)
        /// + orderStatus + paymentMethod + paymentRefrenceNumber (or "") + secureKey
        /// </summary>
        public static string ComputeCallbackV2Signature(
            string fawryRefNumber,
            string merchantRefNumber,
            decimal paymentAmount,
            decimal orderAmount,
            string orderStatus,
            string paymentMethod,
            string paymentReferenceNumber,
            string secureKey)
            => Sha256Hex((fawryRefNumber ?? string.Empty) + (merchantRefNumber ?? string.Empty)
                + FormatAmount(paymentAmount) + FormatAmount(orderAmount)
                + (orderStatus ?? string.Empty) + (paymentMethod ?? string.Empty)
                + (paymentReferenceNumber ?? string.Empty) + secureKey);

        /// <summary>
        /// Verifies a V2 server-to-server notification using its <c>messageSignature</c>,
        /// with constant-time comparison to avoid timing attacks.
        /// </summary>
        public static bool VerifyCallbackV2Signature(ChargeResponse response, string secureKey)
        {
            if (response == null) throw new ArgumentNullException(nameof(response));
            if (string.IsNullOrEmpty(response.MessageSignature)) return false;

            var expected = ComputeCallbackV2Signature(
                response.FawryRefNumber ?? response.ReferenceNumber,
                response.MerchantRefNumber,
                response.PaymentAmount,
                response.OrderAmount,
                response.OrderStatus,
                response.PaymentMethod,
                response.PaymentReferenceNumber,
                secureKey);

            return FixedTimeEquals(expected, response.MessageSignature);
        }

        /// <summary>
        /// Verifies a charge response / payment callback using Fawry's recipe:
        /// referenceNumber (if present) + merchantRefNum + paymentAmount (2dp) + orderAmount (2dp)
        /// + orderStatus + paymentMethod + fawryFees (if present, 2dp) + shippingFees (if present, 2dp)
        /// + authNumber (if present) + customerMail (if present) + customerMobile (if present) + secureKey.
        /// Uses constant-time comparison to avoid timing attacks.
        /// </summary>
        public static bool VerifyCallbackSignature(ChargeResponse response, string secureKey)
        {
            if (response == null) throw new ArgumentNullException(nameof(response));

            // Server-to-server notifications V2 carry messageSignature, computed
            // with Fawry's V2 recipe — verify with that instead of the legacy one.
            if (!string.IsNullOrEmpty(response.MessageSignature))
                return VerifyCallbackV2Signature(response, secureKey);

            if (string.IsNullOrEmpty(response.Signature)) return false;

            var sb = new StringBuilder();
            sb.Append(response.ReferenceNumber ?? string.Empty);
            sb.Append(response.MerchantRefNumber ?? string.Empty);
            sb.Append(FormatAmount(response.PaymentAmount));
            sb.Append(FormatAmount(response.OrderAmount));
            sb.Append(response.OrderStatus ?? string.Empty);
            sb.Append(response.PaymentMethod ?? string.Empty);
            sb.Append(response.FawryFees.HasValue ? FormatAmount(response.FawryFees.Value) : string.Empty);
            sb.Append(response.ShippingFees.HasValue ? FormatAmount(response.ShippingFees.Value) : string.Empty);
            sb.Append(response.AuthNumber ?? string.Empty);
            sb.Append(response.CustomerMail ?? string.Empty);
            sb.Append(response.CustomerMobile ?? string.Empty);
            sb.Append(secureKey);

            return FixedTimeEquals(Sha256Hex(sb.ToString()), response.Signature);
        }

        /// <summary>
        /// Constant-time string comparison. (netstandard2.0 has no CryptographicOperations.FixedTimeEquals.)
        /// </summary>
        public static bool FixedTimeEquals(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            var diff = 0;
            for (var i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
