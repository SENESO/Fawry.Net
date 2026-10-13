using System;

namespace Fawry
{
    /// <summary>
    /// Thrown when Fawry's API returns a non-success status code.
    /// </summary>
    public class FawryApiException : Exception
    {
        /// <summary>Fawry's status code (e.g. "9946" = blank or invalid signature).</summary>
        public string StatusCode { get; }

        /// <summary>Fawry's human-readable status description.</summary>
        public string StatusDescription { get; }

        /// <summary>Raw response body returned by the API.</summary>
        public string ResponseBody { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FawryApiException"/> class.
        /// </summary>
        /// <param name="statusCode">Fawry's status code (e.g. "9946" = blank or invalid signature).</param>
        /// <param name="statusDescription">Fawry's human-readable status description.</param>
        /// <param name="responseBody">Raw response body returned by the API.</param>
        public FawryApiException(string statusCode, string statusDescription, string responseBody)
            : base($"Fawry API error {statusCode}: {statusDescription}")
        {
            StatusCode = statusCode;
            StatusDescription = statusDescription;
            ResponseBody = responseBody;
        }
    }
}
