using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
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
        public const string AuthorizationEnvironmentVariableName = "MAPSIM_LOGIN_ACCOUNT_BILLING_AUTHORITY_AUTHORIZATION";
        public const string BearerTokenEnvironmentVariableName = "MAPSIM_LOGIN_ACCOUNT_BILLING_AUTHORITY_BEARER_TOKEN";
        public const string ClientIdEnvironmentVariableName = "MAPSIM_LOGIN_ACCOUNT_BILLING_AUTHORITY_CLIENT_ID";
        public const string ClientSecretEnvironmentVariableName = "MAPSIM_LOGIN_ACCOUNT_BILLING_AUTHORITY_CLIENT_SECRET";
        public const string RequestModeEnvironmentVariableName = "MAPSIM_LOGIN_ACCOUNT_BILLING_AUTHORITY_REQUEST_MODE";
        public const string RequestSigningSecretEnvironmentVariableName = "MAPSIM_LOGIN_ACCOUNT_BILLING_AUTHORITY_REQUEST_SIGNING_SECRET";

        private const string PostJsonRequestMode = "post-json";
        private const string ExtraCharacterEntitlementProtocol = "extra-character-entitlement.v95";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        private readonly string _endpointUrl;
        private readonly TimeSpan _timeout;
        private readonly string _authorizationHeader;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _requestMode;
        private readonly string _requestSigningSecret;
        private readonly HttpMessageHandler _messageHandler;

        public HttpLoginAccountBillingAuthorityClient(
            string endpointUrl,
            TimeSpan? timeout = null,
            string authorizationHeader = null,
            string clientId = null,
            string clientSecret = null,
            string requestMode = null,
            string requestSigningSecret = null,
            HttpMessageHandler messageHandler = null)
        {
            _endpointUrl = string.IsNullOrWhiteSpace(endpointUrl) ? null : endpointUrl.Trim();
            _timeout = timeout ?? TimeSpan.FromMilliseconds(250);
            _authorizationHeader = string.IsNullOrWhiteSpace(authorizationHeader) ? null : authorizationHeader.Trim();
            _clientId = string.IsNullOrWhiteSpace(clientId) ? null : clientId.Trim();
            _clientSecret = string.IsNullOrWhiteSpace(clientSecret) ? null : clientSecret.Trim();
            _requestMode = string.IsNullOrWhiteSpace(requestMode) ? null : requestMode.Trim();
            _requestSigningSecret = string.IsNullOrWhiteSpace(requestSigningSecret)
                ? _clientSecret
                : requestSigningSecret.Trim();
            _messageHandler = messageHandler;
        }

        public static ILoginAccountBillingAuthorityClient CreateFromEnvironment()
        {
            string endpointUrl = Environment.GetEnvironmentVariable(EndpointEnvironmentVariableName);
            return string.IsNullOrWhiteSpace(endpointUrl)
                ? null
                : new HttpLoginAccountBillingAuthorityClient(
                    endpointUrl,
                    authorizationHeader: ResolveAuthorizationHeaderFromEnvironment(),
                    clientId: Environment.GetEnvironmentVariable(ClientIdEnvironmentVariableName),
                    clientSecret: Environment.GetEnvironmentVariable(ClientSecretEnvironmentVariableName),
                    requestMode: Environment.GetEnvironmentVariable(RequestModeEnvironmentVariableName),
                    requestSigningSecret: Environment.GetEnvironmentVariable(RequestSigningSecretEnvironmentVariableName));
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
                using HttpClient client = _messageHandler == null
                    ? new HttpClient()
                    : new HttpClient(_messageHandler, disposeHandler: false);
                client.Timeout = _timeout;

                using HttpRequestMessage request = CreateAuthorityRequest(accountName, accountId);
                ApplyConfiguredAuthorityHeaders(request);

                using HttpResponseMessage responseMessage = client.SendAsync(request).GetAwaiter().GetResult();
                if (!responseMessage.IsSuccessStatusCode)
                {
                    return false;
                }

                byte[] responseBytes = responseMessage.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                if (TryResolvePacketAuthoredEntitlement(
                        responseBytes,
                        accountId,
                        source: "remote-account-billing-authority-packet",
                        out entitlement))
                {
                    return true;
                }

                string json = Encoding.UTF8.GetString(responseBytes);
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

                if (TryResolvePacketAuthoredEntitlement(
                        response,
                        accountId,
                        out entitlement))
                {
                    return true;
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

        private static string ResolveAuthorizationHeaderFromEnvironment()
        {
            string authorizationHeader = Environment.GetEnvironmentVariable(AuthorizationEnvironmentVariableName);
            if (!string.IsNullOrWhiteSpace(authorizationHeader))
            {
                return authorizationHeader.Trim();
            }

            string bearerToken = Environment.GetEnvironmentVariable(BearerTokenEnvironmentVariableName);
            return string.IsNullOrWhiteSpace(bearerToken)
                ? null
                : "Bearer " + bearerToken.Trim();
        }

        private void ApplyConfiguredAuthorityHeaders(HttpRequestMessage request)
        {
            if (request == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(_authorizationHeader) &&
                AuthenticationHeaderValue.TryParse(_authorizationHeader, out AuthenticationHeaderValue authorization))
            {
                request.Headers.Authorization = authorization;
            }

            if (!string.IsNullOrWhiteSpace(_clientId))
            {
                request.Headers.TryAddWithoutValidation("X-MapSim-Account-Billing-Client-Id", _clientId);
            }

            if (!string.IsNullOrWhiteSpace(_clientSecret))
            {
                request.Headers.TryAddWithoutValidation("X-MapSim-Account-Billing-Client-Secret", _clientSecret);
            }
        }

        private HttpRequestMessage CreateAuthorityRequest(string accountName, int accountId)
        {
            string normalizedAccountName = NormalizeAccountName(accountName);
            if (!IsPostJsonRequestMode())
            {
                HttpRequestMessage request = new(HttpMethod.Get, BuildRequestUrl(normalizedAccountName, accountId));
                ApplyRequestIdentityHeaders(request, normalizedAccountName, accountId);
                return request;
            }

            string requestedAtUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            string nonce = Guid.NewGuid().ToString("N");
            RemoteEntitlementRequest requestBody = new()
            {
                Protocol = ExtraCharacterEntitlementProtocol,
                AccountId = accountId,
                AccountName = normalizedAccountName,
                RequestedPacketType = (int)LoginPacketType.ExtraCharInfoResult,
                RequestedAtUtc = requestedAtUtc,
                Nonce = nonce
            };

            string json = JsonSerializer.Serialize(requestBody, JsonOptions);
            HttpRequestMessage postRequest = new(HttpMethod.Post, _endpointUrl)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            ApplyRequestIdentityHeaders(postRequest, normalizedAccountName, accountId);
            postRequest.Headers.TryAddWithoutValidation("X-MapSim-Account-Billing-Protocol", ExtraCharacterEntitlementProtocol);
            postRequest.Headers.TryAddWithoutValidation("X-MapSim-Account-Billing-Requested-Packet-Type", ((int)LoginPacketType.ExtraCharInfoResult).ToString(CultureInfo.InvariantCulture));
            postRequest.Headers.TryAddWithoutValidation("X-MapSim-Account-Billing-Requested-At", requestedAtUtc);
            postRequest.Headers.TryAddWithoutValidation("X-MapSim-Account-Billing-Nonce", nonce);
            ApplyRequestSignatureHeader(postRequest, normalizedAccountName, accountId, requestedAtUtc, nonce);
            return postRequest;
        }

        private bool IsPostJsonRequestMode()
        {
            return string.Equals(_requestMode, PostJsonRequestMode, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(_requestMode, "post", StringComparison.OrdinalIgnoreCase);
        }

        private void ApplyRequestIdentityHeaders(
            HttpRequestMessage request,
            string accountName,
            int accountId)
        {
            if (request == null)
            {
                return;
            }

            request.Headers.TryAddWithoutValidation("X-MapSim-Account-Billing-Account-Id", accountId.ToString(CultureInfo.InvariantCulture));
            request.Headers.TryAddWithoutValidation("X-MapSim-Account-Billing-Account-Name", accountName);
        }

        private void ApplyRequestSignatureHeader(
            HttpRequestMessage request,
            string accountName,
            int accountId,
            string requestedAtUtc,
            string nonce)
        {
            if (request == null || string.IsNullOrWhiteSpace(_requestSigningSecret))
            {
                return;
            }

            string signingText = string.Join(
                "\n",
                ExtraCharacterEntitlementProtocol,
                accountId.ToString(CultureInfo.InvariantCulture),
                accountName ?? string.Empty,
                ((int)LoginPacketType.ExtraCharInfoResult).ToString(CultureInfo.InvariantCulture),
                requestedAtUtc ?? string.Empty,
                nonce ?? string.Empty);
            byte[] keyBytes = Encoding.UTF8.GetBytes(_requestSigningSecret);
            byte[] payloadBytes = Encoding.UTF8.GetBytes(signingText);
            using HMACSHA256 hmac = new(keyBytes);
            string signature = Convert.ToHexString(hmac.ComputeHash(payloadBytes)).ToLowerInvariant();
            request.Headers.TryAddWithoutValidation("X-MapSim-Account-Billing-Signature", "hmac-sha256=" + signature);
        }

        private static bool TryResolvePacketAuthoredEntitlement(
            RemoteEntitlementResponse response,
            int accountId,
            out LoginAccountBillingEntitlementRecord entitlement)
        {
            entitlement = null;
            if (response == null)
            {
                return false;
            }

            foreach (string packetText in EnumeratePacketTextFields(response))
            {
                if (TryDecodePacketText(packetText, out byte[] packetBytes) &&
                    TryResolvePacketAuthoredEntitlement(
                        packetBytes,
                        accountId,
                        string.IsNullOrWhiteSpace(response.Source)
                            ? "remote-account-billing-authority-packet"
                            : response.Source.Trim(),
                        out entitlement))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<string> EnumeratePacketTextFields(RemoteEntitlementResponse response)
        {
            yield return response.ExtraCharInfoResultPacketHex;
            yield return response.ExtraCharInfoResultPacketBase64;
            yield return response.PacketHex;
            yield return response.PacketBase64;
            yield return response.PayloadHex;
            yield return response.PayloadBase64;
        }

        private static bool TryDecodePacketText(string value, out byte[] packetBytes)
        {
            packetBytes = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalizedValue = value.Trim();
            if (TryDecodeHex(normalizedValue, out packetBytes))
            {
                return true;
            }

            try
            {
                packetBytes = Convert.FromBase64String(normalizedValue);
                return packetBytes.Length > 0;
            }
            catch (FormatException)
            {
                packetBytes = null;
                return false;
            }
        }

        private static bool TryDecodeHex(string value, out byte[] bytes)
        {
            bytes = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalizedValue = value.Trim();
            if (normalizedValue.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                normalizedValue = normalizedValue[2..];
            }

            normalizedValue = normalizedValue
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace(":", string.Empty, StringComparison.Ordinal);
            if (normalizedValue.Length == 0 ||
                normalizedValue.Length % 2 != 0)
            {
                return false;
            }

            bytes = new byte[normalizedValue.Length / 2];
            for (int index = 0; index < bytes.Length; index++)
            {
                if (!byte.TryParse(
                        normalizedValue.Substring(index * 2, 2),
                        NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture,
                        out bytes[index]))
                {
                    bytes = null;
                    return false;
                }
            }

            return true;
        }

        private static bool TryResolvePacketAuthoredEntitlement(
            byte[] packetBytes,
            int accountId,
            string source,
            out LoginAccountBillingEntitlementRecord entitlement)
        {
            entitlement = null;
            if (packetBytes == null ||
                packetBytes.Length < 5 ||
                accountId <= 0)
            {
                return false;
            }

            if (!TryDecodeExtraCharInfoPacket(packetBytes, out LoginExtraCharInfoResultProfile profile) ||
                profile.AccountId != accountId)
            {
                return false;
            }

            bool canHaveExtraCharacter = profile.ResultFlag == 0 && profile.CanHaveExtraCharacter;
            entitlement = new LoginAccountBillingEntitlementRecord
            {
                AccountId = accountId,
                BuyCharacterCount = canHaveExtraCharacter ? 1 : 0,
                ResultFlag = canHaveExtraCharacter ? (byte)0 : (byte)1,
                CanHaveExtraCharacter = canHaveExtraCharacter,
                Source = string.IsNullOrWhiteSpace(source)
                    ? "remote-account-billing-authority-packet"
                    : source.Trim()
            };
            return true;
        }

        private static bool TryDecodeExtraCharInfoPacket(
            byte[] packetBytes,
            out LoginExtraCharInfoResultProfile profile)
        {
            profile = null;
            if (LoginExtraCharInfoResultCodec.TryDecode(packetBytes, out profile, out _))
            {
                return true;
            }

            if (packetBytes == null ||
                packetBytes.Length < 7 ||
                BitConverter.ToUInt16(packetBytes, 0) != (ushort)LoginPacketType.ExtraCharInfoResult)
            {
                return false;
            }

            byte[] payloadBytes = new byte[packetBytes.Length - 2];
            Buffer.BlockCopy(packetBytes, 2, payloadBytes, 0, payloadBytes.Length);
            return LoginExtraCharInfoResultCodec.TryDecode(payloadBytes, out profile, out _);
        }

        private string BuildRequestUrl(string accountName, int accountId)
        {
            string separator = _endpointUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
            string normalizedAccountName = NormalizeAccountName(accountName);
            return _endpointUrl +
                   separator +
                   "accountId=" +
                   accountId.ToString(CultureInfo.InvariantCulture) +
                   "&accountName=" +
                   Uri.EscapeDataString(normalizedAccountName);
        }

        private static string NormalizeAccountName(string accountName)
        {
            return string.IsNullOrWhiteSpace(accountName)
                ? "explorergm"
                : accountName.Trim();
        }

        private sealed class RemoteEntitlementRequest
        {
            public string Protocol { get; set; }
            public int AccountId { get; set; }
            public string AccountName { get; set; }
            public int RequestedPacketType { get; set; }
            public string RequestedAtUtc { get; set; }
            public string Nonce { get; set; }
        }

        private sealed class RemoteEntitlementResponse
        {
            public int AccountId { get; set; }
            public int BuyCharacterCount { get; set; }
            public byte ResultFlag { get; set; }
            public bool CanHaveExtraCharacter { get; set; }
            public string Source { get; set; }
            public string ExtraCharInfoResultPacketHex { get; set; }
            public string ExtraCharInfoResultPacketBase64 { get; set; }
            public string PacketHex { get; set; }
            public string PacketBase64 { get; set; }
            public string PayloadHex { get; set; }
            public string PayloadBase64 { get; set; }
        }
    }
}
