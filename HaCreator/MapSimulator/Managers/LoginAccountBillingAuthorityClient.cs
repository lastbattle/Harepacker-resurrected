using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class LoginAccountBillingEntitlementRecord
    {
        public int AccountId { get; init; }
        public int BuyCharacterCount { get; init; }
        public byte ResultFlag { get; init; }
        public bool CanHaveExtraCharacter { get; init; }
        public string Source { get; init; } = string.Empty;
    }

    public interface ILoginAccountBillingAuthorityClient
    {
        bool TryResolveExtraCharacterEntitlement(
            string accountName,
            int accountId,
            out LoginAccountBillingEntitlementRecord entitlement);
    }

    public sealed class HttpLoginAccountBillingAuthorityClient : ILoginAccountBillingAuthorityClient
    {
        public const string EndpointEnvironmentVariableName = "MAPSIM_LOGIN_ACCOUNT_BILLING_AUTHORITY_URL";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly string _endpointUrl;
        private readonly TimeSpan _timeout;

        public HttpLoginAccountBillingAuthorityClient(string endpointUrl, TimeSpan? timeout = null)
        {
            _endpointUrl = string.IsNullOrWhiteSpace(endpointUrl) ? null : endpointUrl.Trim();
            _timeout = timeout ?? TimeSpan.FromMilliseconds(250);
        }

        public static ILoginAccountBillingAuthorityClient CreateFromEnvironment()
        {
            string endpointUrl = Environment.GetEnvironmentVariable(EndpointEnvironmentVariableName);
            return string.IsNullOrWhiteSpace(endpointUrl)
                ? null
                : new HttpLoginAccountBillingAuthorityClient(endpointUrl);
        }

        public bool TryResolveExtraCharacterEntitlement(
            string accountName,
            int accountId,
            out LoginAccountBillingEntitlementRecord entitlement)
        {
            entitlement = null;
            if (accountId <= 0 || string.IsNullOrWhiteSpace(_endpointUrl))
            {
                return false;
            }

            try
            {
                using HttpClient client = new()
                {
                    Timeout = _timeout
                };

                string requestUrl = BuildRequestUrl(accountName, accountId);
                string json = client.GetStringAsync(requestUrl).GetAwaiter().GetResult();
                RemoteEntitlementResponse response =
                    JsonSerializer.Deserialize<RemoteEntitlementResponse>(json, JsonOptions);
                if (response == null)
                {
                    return false;
                }

                int resolvedAccountId = response.AccountId > 0 ? response.AccountId : accountId;
                if (resolvedAccountId != accountId)
                {
                    return false;
                }

                bool canHaveExtraCharacter = response.CanHaveExtraCharacter &&
                                             response.ResultFlag == 0;
                entitlement = new LoginAccountBillingEntitlementRecord
                {
                    AccountId = accountId,
                    BuyCharacterCount = canHaveExtraCharacter
                        ? Math.Max(1, response.BuyCharacterCount)
                        : 0,
                    ResultFlag = canHaveExtraCharacter ? (byte)0 : (byte)1,
                    CanHaveExtraCharacter = canHaveExtraCharacter,
                    Source = string.IsNullOrWhiteSpace(response.Source)
                        ? "remote-account-billing-authority"
                        : response.Source.Trim()
                };
                return true;
            }
            catch
            {
                entitlement = null;
                return false;
            }
        }

        private string BuildRequestUrl(string accountName, int accountId)
        {
            string separator = _endpointUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
            string normalizedAccountName = string.IsNullOrWhiteSpace(accountName)
                ? "explorergm"
                : accountName.Trim();
            return _endpointUrl +
                   separator +
                   "accountId=" +
                   accountId.ToString(CultureInfo.InvariantCulture) +
                   "&accountName=" +
                   Uri.EscapeDataString(normalizedAccountName);
        }

        private sealed class RemoteEntitlementResponse
        {
            public int AccountId { get; set; }
            public int BuyCharacterCount { get; set; }
            public byte ResultFlag { get; set; }
            public bool CanHaveExtraCharacter { get; set; }
            public string Source { get; set; }
        }
    }
}
