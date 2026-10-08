using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Fawry.Models;

namespace Fawry
{
    /// <summary>
    /// .NET client for FawryPay charge APIs: Pay-at-Fawry reference numbers,
    /// card payments (immediate or authorize-then-capture), mobile wallets,
    /// plus capture / cancel-authorization and callback verification.
    /// Bring your own <see cref="HttpClient"/>; the client never disposes one you pass in.
    /// </summary>
    public class FawryClient : IDisposable
    {
        private readonly FawryClientOptions _options;
        private readonly HttpClient _http;
        private readonly bool _ownsHttpClient;
        private bool _disposed;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Creates a client. Pass your own <see cref="HttpClient"/> (recommended,
        /// e.g. via IHttpClientFactory) or let the client create one.
        /// </summary>
        public FawryClient(FawryClientOptions options, HttpClient httpClient = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(options.MerchantCode))
                throw new ArgumentException("MerchantCode is required.", nameof(options));
            if (string.IsNullOrWhiteSpace(options.SecureKey))
                throw new ArgumentException("SecureKey is required.", nameof(options));

            _ownsHttpClient = httpClient == null;
            _http = httpClient ?? new HttpClient();
        }

        // ---------- charge APIs ----------

        /// <summary>
        /// Creates a Pay-at-Fawry charge: the customer gets a reference number
        /// and pays in cash at any Fawry outlet before <c>PaymentExpiry</c>.
        /// The response's <c>ReferenceNumber</c> is what the customer pays with.
        /// </summary>
        public Task<ChargeResponse> ChargePayAtFawryAsync(
            ChargeRequest request, CancellationToken cancellationToken = default)
            => ChargeAsync(request, FawryPaymentMethods.PayAtFawry,
                "ECommerceWeb/Fawry/payments/charge", cancellationToken);

        /// <summary>
        /// Creates a mobile-wallet charge (e.g. Vodafone Cash): the customer
        /// approves the payment on their phone.
        /// </summary>
        public Task<ChargeResponse> ChargeMobileWalletAsync(
            ChargeRequest request, CancellationToken cancellationToken = default)
            => ChargeAsync(request, FawryPaymentMethods.MobileWallet,
                "ECommerceWeb/api/payments/charge", cancellationToken);

        /// <summary>
        /// Charges a bank card. Set <see cref="CardChargeRequest.AuthCaptureMode"/>
        /// to authorize now and <see cref="CaptureAsync"/> later.
        /// Collecting raw card data requires PCI compliance — otherwise use
        /// Fawry's card-tokenization plugin and pass the token as the card number.
        /// </summary>
        public async Task<ChargeResponse> ChargeCardAsync(
            CardChargeRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            RequireChargeFields(request);
            if (string.IsNullOrWhiteSpace(request.CardNumber))
                throw new ArgumentException("CardNumber is required.", nameof(request));

            var signature = FawrySignature.ComputeCardChargeSignature(
                _options.MerchantCode, request.MerchantRefNum, request.CustomerProfileId,
                FawryPaymentMethods.Card, request.Amount,
                request.CardNumber, request.CardExpiryYear, request.CardExpiryMonth,
                request.Cvv, _options.SecureKey);

            var body = BuildChargeBody(request, FawryPaymentMethods.Card, signature);
            body["cardNumber"] = request.CardNumber;
            body["cardExpiryYear"] = request.CardExpiryYear;
            body["cardExpiryMonth"] = request.CardExpiryMonth;
            body["cvv"] = request.Cvv;
            body["authCaptureModePayment"] = request.AuthCaptureMode;
            body["enable3DS"] = request.Enable3DS;

            return await PostChargeAsync("ECommerceWeb/Fawry/payments/charge", body, cancellationToken)
                .ConfigureAwait(false);
        }

        // ---------- authorize / capture / cancel ----------

        /// <summary>
        /// Captures a previously authorized card payment.
        /// Omit <paramref name="captureAmount"/> to capture the full amount.
        /// </summary>
        public async Task<OperationResponse> CaptureAsync(
            string merchantRefNum, decimal? captureAmount = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(merchantRefNum))
                throw new ArgumentException("merchantRefNum is required.", nameof(merchantRefNum));

            var body = new Dictionary<string, object>
            {
                ["merchantCode"] = _options.MerchantCode,
                ["merchantRefNum"] = merchantRefNum,
                ["requestSignature"] = FawrySignature.ComputeCaptureSignature(
                    _options.MerchantCode, merchantRefNum, captureAmount, _options.SecureKey)
            };
            if (captureAmount.HasValue)
                body["captureAmount"] = FawrySignature.FormatAmount(captureAmount.Value);

            return await PostAsync<OperationResponse>(
                "ECommerceWeb/api/payment/capture", body, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Cancels a payment authorization (releases the hold on the customer's card).
        /// </summary>
        public async Task<OperationResponse> CancelAuthorizationAsync(
            string merchantRefNum, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(merchantRefNum))
                throw new ArgumentException("merchantRefNum is required.", nameof(merchantRefNum));

            var body = new Dictionary<string, object>
            {
                ["merchantCode"] = _options.MerchantCode,
                ["merchantRefNum"] = merchantRefNum,
                ["requestSignature"] = FawrySignature.ComputeCancelSignature(
                    _options.MerchantCode, merchantRefNum, _options.SecureKey)
            };

            return await PostAsync<OperationResponse>(
                "ECommerceWeb/api/payment/cancel", body, cancellationToken).ConfigureAwait(false);
        }

        // ---------- callbacks ----------

        /// <summary>
        /// Verifies a payment callback's signature with constant-time comparison.
        /// Always call this before trusting a callback — never rely on query params alone.
        /// </summary>
        public bool VerifyCallbackSignature(ChargeResponse callback)
            => FawrySignature.VerifyCallbackSignature(callback, _options.SecureKey);

        // ---------- internals ----------

        private async Task<ChargeResponse> ChargeAsync(
            ChargeRequest request, string paymentMethod, string path,
            CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            RequireChargeFields(request);

            var signature = FawrySignature.ComputeChargeSignature(
                _options.MerchantCode, request.MerchantRefNum, request.CustomerProfileId,
                paymentMethod, request.Amount, _options.SecureKey);

            return await PostChargeAsync(path, BuildChargeBody(request, paymentMethod, signature), cancellationToken)
                .ConfigureAwait(false);
        }

        private static void RequireChargeFields(ChargeRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.MerchantRefNum))
                throw new ArgumentException("MerchantRefNum is required and must be unique per charge.", nameof(request));
            if (request.Amount <= 0)
                throw new ArgumentException("Amount must be greater than zero.", nameof(request));
            if (request.ChargeItems == null || !request.ChargeItems.Any())
                throw new ArgumentException("At least one charge item is required.", nameof(request));
        }

        private Dictionary<string, object> BuildChargeBody(
            ChargeRequest request, string paymentMethod, string signature)
        {
            var body = new Dictionary<string, object>
            {
                ["merchantCode"] = _options.MerchantCode,
                ["merchantRefNum"] = request.MerchantRefNum,
                ["customerName"] = request.CustomerName,
                ["customerMobile"] = request.CustomerMobile,
                ["customerEmail"] = request.CustomerEmail,
                ["amount"] = FawrySignature.FormatAmount(request.Amount),
                ["paymentExpiry"] = request.PaymentExpiry.ToUnixTimeMilliseconds(),
                ["currencyCode"] = _options.CurrencyCode,
                ["language"] = _options.Language,
                ["chargeItems"] = request.ChargeItems.Select(i => new Dictionary<string, object>
                {
                    ["itemId"] = i.ItemId,
                    ["description"] = i.Description,
                    // Fawry's own docs send these as strings; keep the exact 2dp format.
                    ["price"] = FawrySignature.FormatAmount(i.Price),
                    ["quantity"] = i.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture)
                }).ToList(),
                ["signature"] = signature,
                ["paymentMethod"] = paymentMethod
            };
            if (!string.IsNullOrWhiteSpace(request.CustomerProfileId))
                body["customerProfileId"] = request.CustomerProfileId;
            if (!string.IsNullOrWhiteSpace(request.Description))
                body["description"] = request.Description;
            return body;
        }

        private Task<ChargeResponse> PostChargeAsync(
            string path, Dictionary<string, object> body, CancellationToken cancellationToken)
            => PostAsync<ChargeResponse>(path, body, cancellationToken);

        private async Task<T> PostAsync<T>(
            string path, Dictionary<string, object> body, CancellationToken cancellationToken)
        {
            var url = _options.BaseUrl.TrimEnd('/') + "/" + path.TrimStart('/');
            var json = JsonSerializer.Serialize(body, JsonOptions);

            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            using (var response = await _http.PostAsync(url, content, cancellationToken).ConfigureAwait(false))
            {
                var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (typeof(T) == typeof(ChargeResponse))
                {
                    var charge = JsonSerializer.Deserialize<ChargeResponse>(responseBody, JsonOptions);
                    if (charge != null && !charge.IsSuccess)
                        throw new FawryApiException(charge.StatusCode, charge.StatusDescription, responseBody);
                    return (T)(object)charge;
                }

                var op = JsonSerializer.Deserialize<OperationResponse>(responseBody, JsonOptions);
                if (op != null && !op.IsSuccess)
                    throw new FawryApiException(op.StatusCode, op.StatusDescription, responseBody);
                return (T)(object)op;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_ownsHttpClient) _http.Dispose();
        }
    }
}
