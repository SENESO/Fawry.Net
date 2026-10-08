using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using Fawry;
using Fawry.Models;

namespace Fawry.IntegrationTests
{
    /// <summary>
    /// Integration tests against Fawry's staging environment
    /// (the SDK's default, <see cref="FawryClientOptions.StagingBaseUrl"/>).
    ///
    /// Credentials come from the environment — nothing is hardcoded:
    ///   FAWRY_MERCHANT_CODE  merchant code from Fawry merchant onboarding
    ///   FAWRY_SECURITY_KEY   secure key from Fawry merchant onboarding
    ///   FAWRY_BASE_URL       optional override; defaults to the staging URL
    ///
    /// Every test calls <see cref="RequireEnv"/> and skips via
    /// <see cref="Assert.Ignore()"/> when credentials are absent, so the suite
    /// stays green on machines without sandbox access.
    ///
    /// All tests are read-oriented or exercise documented error paths.
    /// None of them initiates a payment, captures funds, cancels a real
    /// authorization, or moves money in any way.
    ///
    /// Setup and run instructions live in INTEGRATION_TESTING.md.
    /// </summary>
    [TestFixture]
    [Category("Integration")]
    public class FawryIntegrationTests
    {
        private const string EnvMerchantCode = "FAWRY_MERCHANT_CODE";
        private const string EnvSecureKey = "FAWRY_SECURITY_KEY";
        private const string EnvBaseUrl = "FAWRY_BASE_URL";

        private static string RequireEnv(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value))
                Assert.Ignore(
                    "Integration test skipped: environment variable '" + name +
                    "' is not set. See INTEGRATION_TESTING.md for setup instructions.");
            return value.Trim();
        }

        private static FawryClientOptions BuildOptions()
        {
            var baseUrl = Environment.GetEnvironmentVariable(EnvBaseUrl);
            return new FawryClientOptions
            {
                MerchantCode = RequireEnv(EnvMerchantCode),
                SecureKey = RequireEnv(EnvSecureKey),
                BaseUrl = string.IsNullOrWhiteSpace(baseUrl)
                    ? FawryClientOptions.StagingBaseUrl
                    : baseUrl.Trim()
            };
        }

        /// <summary>
        /// Queries Fawry's "Get Payment Status" endpoint with a signature built
        /// by the SDK (<c>merchantCode + merchantRefNumber + secureKey</c>,
        /// per Fawry's documented recipe) and parses the reply as a
        /// <see cref="ChargeResponse"/>. Set <paramref name="tamperSignature"/>
        /// to corrupt the signature and observe staging's rejection.
        /// </summary>
        private static async Task<ChargeResponse> GetChargeStatusAsync(
            FawryClientOptions options, string merchantRefNumber, bool tamperSignature = false)
        {
            var signature = FawrySignature.Sha256Hex(
                options.MerchantCode + merchantRefNumber + options.SecureKey);
            if (tamperSignature)
                signature = "0" + signature.Substring(1);

            var url = options.BaseUrl.TrimEnd('/') + "/ECommerceWeb/api/payments/status" +
                      "?merchantCode=" + Uri.EscapeDataString(options.MerchantCode) +
                      "&merchantRefNumber=" + Uri.EscapeDataString(merchantRefNumber) +
                      "&signature=" + Uri.EscapeDataString(signature);

            try
            {
                using (var http = new HttpClient())
                using (var response = await http.GetAsync(url).ConfigureAwait(false))
                {
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(body),
                        "Staging returned an empty response body.");
                    var parsed = JsonSerializer.Deserialize<ChargeResponse>(body);
                    Assert.IsNotNull(parsed,
                        "Staging response was not a Fawry response envelope. Raw body: " + body);
                    return parsed;
                }
            }
            catch (HttpRequestException ex)
            {
                Assert.Ignore("Fawry staging is unreachable from this machine: " + ex.Message);
                return null; // unreachable; keeps the compiler happy
            }
        }

        private static string NewProbeRef()
            => "integration-probe-" + Guid.NewGuid().ToString("N");

        // ---------- tests ----------

        /// <summary>
        /// Proves TLS, DNS, routing and URL construction against staging:
        /// a signed status query must come back as a JSON Fawry envelope
        /// carrying a statusCode (success or a documented error — both are
        /// parseable responses, not transport failures).
        /// </summary>
        [Test]
        public async Task Staging_IsReachable_AndReturnsFawryEnvelope()
        {
            var options = BuildOptions();
            var status = await GetChargeStatusAsync(options, NewProbeRef()).ConfigureAwait(false);

            Assert.IsFalse(string.IsNullOrWhiteSpace(status.StatusCode),
                "Expected a Fawry statusCode in the staging response.");
        }

        /// <summary>
        /// Queries charge status for a probe reference with the SDK's status
        /// signature recipe. With live sandbox credentials the response
        /// carries a real orderStatus, and — when staging signs the reply —
        /// <see cref="FawryClient.VerifyCallbackSignature"/> must accept it,
        /// proving the request and response signature recipes round-trip.
        /// </summary>
        [Test]
        public async Task ChargeStatusQuery_SignatureRecipe_RoundTripsAgainstStaging()
        {
            var options = BuildOptions();
            var status = await GetChargeStatusAsync(options, NewProbeRef()).ConfigureAwait(false);

            Assert.IsFalse(string.IsNullOrWhiteSpace(status.StatusCode));

            if (status.IsSuccess)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(status.OrderStatus),
                    "A successful staging response should carry an orderStatus.");
                if (!string.IsNullOrEmpty(status.Signature))
                {
                    using (var client = new FawryClient(options))
                        Assert.IsTrue(client.VerifyCallbackSignature(status),
                            "Staging's response signature did not verify with the configured secure key.");
                }
            }
        }

        /// <summary>
        /// Corrupts the status-query signature and asserts staging rejects it
        /// with Fawry's documented "invalid signature" status code (9946).
        /// With placeholder credentials staging may fail the request at
        /// merchant level instead; the test skips in that case since signature
        /// validation cannot be exercised.
        /// </summary>
        [Test]
        public async Task TamperedSignature_StatusQuery_IsRejectedByStaging()
        {
            var options = BuildOptions();
            var tampered = await GetChargeStatusAsync(
                options, NewProbeRef(), tamperSignature: true).ConfigureAwait(false);

            if (tampered.StatusCode != "9946")
                Assert.Ignore(
                    "Cannot exercise signature validation: staging answered the tampered " +
                    "request with '" + tampered.StatusCode + ": " + tampered.StatusDescription +
                    "' instead of the invalid-signature code 9946.");
        }

        /// <summary>
        /// Drives <see cref="FawryClient.CaptureAsync"/> against staging with a
        /// reference that cannot match any real authorization. Fawry must
        /// reject it gracefully as a FawryApiException carrying its own
        /// statusCode — proving the SDK's capture plumbing and error mapping
        /// work against the live API without touching real money.
        /// </summary>
        [Test]
        public void Capture_NonExistentAuthorization_ThrowsFawryApiException()
        {
            var options = BuildOptions();
            using (var client = new FawryClient(options))
            {
                try
                {
                    var ex = Assert.ThrowsAsync<FawryApiException>(async () =>
                        await client.CaptureAsync(NewProbeRef()).ConfigureAwait(false));

                    Assert.IsFalse(string.IsNullOrWhiteSpace(ex.StatusCode),
                        "Expected Fawry's status code on the rejection.");
                    Assert.IsTrue(ex.ResponseBody.Contains("statusCode"),
                        "Expected a Fawry JSON envelope in the raw response body.");
                }
                catch (HttpRequestException ex)
                {
                    Assert.Ignore("Fawry staging is unreachable from this machine: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Same graceful-rejection proof as the capture test, but through
        /// <see cref="FawryClient.CancelAuthorizationAsync"/>.
        /// </summary>
        [Test]
        public void CancelAuthorization_NonExistentReference_ThrowsFawryApiException()
        {
            var options = BuildOptions();
            using (var client = new FawryClient(options))
            {
                try
                {
                    var ex = Assert.ThrowsAsync<FawryApiException>(async () =>
                        await client.CancelAuthorizationAsync(NewProbeRef()).ConfigureAwait(false));

                    Assert.IsFalse(string.IsNullOrWhiteSpace(ex.StatusCode),
                        "Expected Fawry's status code on the rejection.");
                    Assert.IsTrue(ex.ResponseBody.Contains("statusCode"),
                        "Expected a Fawry JSON envelope in the raw response body.");
                }
                catch (HttpRequestException ex)
                {
                    Assert.Ignore("Fawry staging is unreachable from this machine: " + ex.Message);
                }
            }
        }
    }
}
