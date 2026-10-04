using System;

namespace HaCreator.MapSimulator.Managers
{
    internal static class LoginWebsiteHandoffResolver
    {
        internal const string DefaultHomepageUrl = "https://www.nexon.com/";
        internal const string DefaultAccountSelfServiceUrl = "https://playersupport.nexon.com/hc/en-us/articles/4402870496148-Using-Account-Self-Service";
        internal const string DefaultFindEmailHelpUrl = DefaultAccountSelfServiceUrl;
        internal const string DefaultPasswordResetHelpUrl = DefaultAccountSelfServiceUrl;

        public static string ResolveOrDefault(string candidateUrl)
        {
            return TryResolve(candidateUrl, out string resolvedUrl)
                ? resolvedUrl
                : DefaultHomepageUrl;
        }

        public static bool TryResolve(string candidateUrl, out string resolvedUrl)
        {
            resolvedUrl = null;
            if (string.IsNullOrWhiteSpace(candidateUrl))
            {
                return false;
            }

            if (!Uri.TryCreate(candidateUrl.Trim(), UriKind.Absolute, out Uri uri))
            {
                return false;
            }

            if (!uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            resolvedUrl = uri.AbsoluteUri;
            return true;
        }
    }
}
