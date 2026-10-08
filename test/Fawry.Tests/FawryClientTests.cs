using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Fawry;
using Fawry.Models;

namespace Fawry.Tests
{
    [TestFixture]
    public class FawryClientTests
    {
        private const string MerchantCode = "testMerchantCode";
        private const string SecureKey = "testSecureKey123";

        private static FawryClientOptions Options() => new FawryClientOptions
        {
            MerchantCode = MerchantCode,
            SecureKey = SecureKey
        };

        private static ChargeRequest SampleRequest() => new ChargeRequest
        {
            MerchantRefNum = "100162801",
            CustomerName = "Ahmed Ali",
            CustomerMobile = "01234567891",
            CustomerEmail = "ahmed@example.com",
            Amount = 580.55m,
            ChargeItems = new List<ChargeItem>
            {
                new ChargeItem { ItemId = "item-1", Description = "Test item", Price = 580.55m, Quantity = 1 }
            }
        };

        private class CapturingHandler : HttpMessageHandler
        {
            private readonly string _responseBody;
            public List<(string Url, string Body)> Requests { get; } = new List<(string, string)>();

            public CapturingHandler(string responseBody) => _responseBody = responseBody;

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var body = request.Content == null ? null : await request.Content.ReadAsStringAsync();
                Requests.Add((request.RequestUri.ToString(), body));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
                };
            }
        }

        private const string ChargeOkBody =
            @"{ ""type"": ""ChargeResponse"", ""referenceNumber"": ""963455678"",
               ""merchantRefNumber"": ""100162801"", ""orderAmount"": 580.55, ""paymentAmount"": 580.55,
               ""paymentMethod"": ""PayAtFawry"", ""orderStatus"": ""UNPAID"",
               ""statusCode"": ""200"", ""statusDescription"": ""Operation done successfully"" }";

        private const string OperationOkBody =
            @"{ ""type"": ""PaymentStatusResponse"", ""fawryRefNumber"": ""963455678"",
               ""merchantCode"": ""testMerchantCode"", ""merchantRefNumber"": ""100162801"",
               ""orderStatus"": ""PAID"", ""statusCode"": ""200"",
               ""statusDescription"": ""Operation done successfully"" }";

        [Test]
        public async Task ChargePayAtFawry_PostsToChargeEndpoint_WithValidSignature()
        {
            var handler = new CapturingHandler(ChargeOkBody);
            var client = new FawryClient(Options(), new HttpClient(handler));

            var response = await client.ChargePayAtFawryAsync(SampleRequest());

            Assert.AreEqual(1, handler.Requests.Count);
            Assert.IsTrue(handler.Requests[0].Url.EndsWith("/ECommerceWeb/Fawry/payments/charge"));

            using (var doc = JsonDocument.Parse(handler.Requests[0].Body))
            {
                var root = doc.RootElement;
                Assert.AreEqual(MerchantCode, root.GetProperty("merchantCode").GetString());
                Assert.AreEqual("100162801", root.GetProperty("merchantRefNum").GetString());
                Assert.AreEqual("580.55", root.GetProperty("amount").GetString());
                Assert.AreEqual("PAYATFAWRY", root.GetProperty("paymentMethod").GetString());

                // The signature must match the documented recipe for the sent fields.
                var expected = FawrySignature.ComputeChargeSignature(
                    MerchantCode, "100162801", null, "PAYATFAWRY", 580.55m, SecureKey);
                Assert.AreEqual(expected, root.GetProperty("signature").GetString());
            }

            Assert.AreEqual("963455678", response.ReferenceNumber);
            Assert.IsFalse(response.IsPaid); // UNPAID until the customer pays at an outlet
        }

        [Test]
        public async Task ChargeMobileWallet_UsesApiPath()
        {
            var handler = new CapturingHandler(ChargeOkBody);
            var client = new FawryClient(Options(), new HttpClient(handler));

            await client.ChargeMobileWalletAsync(SampleRequest());

            Assert.IsTrue(handler.Requests[0].Url.EndsWith("/ECommerceWeb/api/payments/charge"));
            using (var doc = JsonDocument.Parse(handler.Requests[0].Body))
                Assert.AreEqual("MWALLET", doc.RootElement.GetProperty("paymentMethod").GetString());
        }

        [Test]
        public async Task ChargeCard_IncludesCardFields_AndCardSignature()
        {
            var handler = new CapturingHandler(ChargeOkBody);
            var client = new FawryClient(Options(), new HttpClient(handler));

            var request = new CardChargeRequest
            {
                MerchantRefNum = "100162801",
                CustomerName = "Ahmed Ali",
                CustomerMobile = "01234567891",
                CustomerEmail = "ahmed@example.com",
                Amount = 580.55m,
                CardNumber = "4242424242424242",
                CardExpiryYear = "25",
                CardExpiryMonth = "05",
                Cvv = "123",
                ChargeItems = new List<ChargeItem>
                {
                    new ChargeItem { ItemId = "item-1", Description = "Test item", Price = 580.55m, Quantity = 1 }
                }
            };

            await client.ChargeCardAsync(request);

            using (var doc = JsonDocument.Parse(handler.Requests[0].Body))
            {
                var root = doc.RootElement;
                Assert.AreEqual("CARD", root.GetProperty("paymentMethod").GetString());
                Assert.AreEqual("4242424242424242", root.GetProperty("cardNumber").GetString());

                var expected = FawrySignature.ComputeCardChargeSignature(
                    MerchantCode, "100162801", null, "CARD", 580.55m,
                    "4242424242424242", "25", "05", "123", SecureKey);
                Assert.AreEqual(expected, root.GetProperty("signature").GetString());
            }
        }

        [Test]
        public void Charge_ThrowsFawryApiException_OnErrorStatus()
        {
            var handler = new CapturingHandler(
                @"{ ""type"": ""ChargeResponse"", ""statusCode"": ""9946"",
                   ""statusDescription"": ""Blank or invalid signature."" }");
            var client = new FawryClient(Options(), new HttpClient(handler));

            var ex = Assert.ThrowsAsync<FawryApiException>(
                () => client.ChargePayAtFawryAsync(SampleRequest()));
            Assert.AreEqual("9946", ex.StatusCode);
        }

        [Test]
        public async Task Capture_PostsToCaptureEndpoint_WithSignature()
        {
            var handler = new CapturingHandler(OperationOkBody);
            var client = new FawryClient(Options(), new HttpClient(handler));

            var result = await client.CaptureAsync("100162801", 500.00m);

            Assert.IsTrue(handler.Requests[0].Url.EndsWith("/ECommerceWeb/api/payment/capture"));
            using (var doc = JsonDocument.Parse(handler.Requests[0].Body))
            {
                var root = doc.RootElement;
                Assert.AreEqual("500.00", root.GetProperty("captureAmount").GetString());
                var expected = FawrySignature.ComputeCaptureSignature(
                    MerchantCode, "100162801", 500.00m, SecureKey);
                Assert.AreEqual(expected, root.GetProperty("requestSignature").GetString());
            }
            Assert.IsTrue(result.IsSuccess);
        }

        [Test]
        public async Task Capture_WithoutAmount_OmitsCaptureAmount()
        {
            var handler = new CapturingHandler(OperationOkBody);
            var client = new FawryClient(Options(), new HttpClient(handler));

            await client.CaptureAsync("100162801");

            using (var doc = JsonDocument.Parse(handler.Requests[0].Body))
                Assert.Throws<KeyNotFoundException>(
                    () => doc.RootElement.GetProperty("captureAmount"));
        }

        [Test]
        public async Task CancelAuthorization_PostsToCancelEndpoint()
        {
            var handler = new CapturingHandler(OperationOkBody);
            var client = new FawryClient(Options(), new HttpClient(handler));

            await client.CancelAuthorizationAsync("100162801");

            Assert.IsTrue(handler.Requests[0].Url.EndsWith("/ECommerceWeb/api/payment/cancel"));
            using (var doc = JsonDocument.Parse(handler.Requests[0].Body))
            {
                var expected = FawrySignature.ComputeCancelSignature(
                    MerchantCode, "100162801", SecureKey);
                Assert.AreEqual(expected, doc.RootElement.GetProperty("requestSignature").GetString());
            }
        }

        [Test]
        public void VerifyCallbackSignature_DelegatesToOptionsKey()
        {
            var client = new FawryClient(Options());
            var callback = new ChargeResponse
            {
                ReferenceNumber = "963455678",
                MerchantRefNumber = "9990d0642040",
                PaymentAmount = 20.00m,
                OrderAmount = 20.00m,
                OrderStatus = "PAID",
                PaymentMethod = "PayAtFawry",
                FawryFees = 1.00m,
                AuthNumber = "12336534",
                CustomerMail = "example@email.com",
                CustomerMobile = "01234567891",
                Signature = "f7a657d1340de031029789adc56f0c4fb232a7158dfdeed474d1caf6b34e6cc4"
            };

            Assert.IsTrue(client.VerifyCallbackSignature(callback));
        }

        [Test]
        public void MissingMerchantCode_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => new FawryClient(new FawryClientOptions { SecureKey = "x" }));
        }

        [Test]
        public void Charge_WithoutItems_Throws()
        {
            var client = new FawryClient(Options());
            var request = SampleRequest();
            request.ChargeItems.Clear();

            Assert.ThrowsAsync<ArgumentException>(
                () => client.ChargePayAtFawryAsync(request));
        }
    }
}
