namespace Fawry
{
    /// <summary>
    /// Configuration for <see cref="FawryClient"/>.
    /// Get your merchant code and secure key from the FawryPay team during account setup.
    /// </summary>
    public class FawryClientOptions
    {
        /// <summary>
        /// Staging (test) environment base URL.
        /// </summary>
        public const string StagingBaseUrl = "https://atfawry.fawrystaging.com";

        /// <summary>
        /// Production environment base URL.
        /// </summary>
        public const string ProductionBaseUrl = "https://www.atfawry.com";

        /// <summary>
        /// The merchant code provided by the FawryPay team during account setup.
        /// </summary>
        public string MerchantCode { get; set; }

        /// <summary>
        /// The secure key provided by the FawryPay team. Used to sign requests
        /// and verify callbacks. Never expose this in client-side code.
        /// </summary>
        public string SecureKey { get; set; }

        /// <summary>
        /// API base URL. Defaults to the staging environment.
        /// Use <see cref="ProductionBaseUrl"/> when you go live.
        /// </summary>
        public string BaseUrl { get; set; } = StagingBaseUrl;

        /// <summary>
        /// Notification language sent to the customer: "en-gb" or "ar-eg". Default "en-gb".
        /// </summary>
        public string Language { get; set; } = "en-gb";

        /// <summary>
        /// Currency code. Default "EGP".
        /// </summary>
        public string CurrencyCode { get; set; } = "EGP";
    }
}
