using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;
using System;

namespace BlackBytesBox.Routed.GitBackend.Utility
{
    public static partial class HttpContextUtility
    {
        /// <summary>
        /// Retrieves the full request URI using the display URL.
        /// </summary>
        /// <param name="context">The HTTP context containing the request.</param>
        /// <param name="logger">An optional logger for debug logging.</param>
        /// <returns>
        /// The full URI of the request if successfully constructed; otherwise, <c>null</c>.
        /// </returns>
        public static Uri? GetUriFromRequestDisplayUrl(HttpContext context, ILogger? logger = null)
        {
            if (context is null)
            {
                logger?.LogError("HttpContext is null.");
                return null;
            }

            string encodedUrl = context.Request.GetEncodedUrl();

            if (string.IsNullOrWhiteSpace(encodedUrl))
            {
                logger?.LogError("Encoded URL is empty.");
                return null;
            }

            try
            {
                return new Uri(encodedUrl);
            }
            catch (Exception)
            {
                logger?.LogError("Failed to parse encoded URL: {EncodedUrl}", encodedUrl);
                return null;
            }
        }

    }
}
