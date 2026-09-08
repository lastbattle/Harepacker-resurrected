using HaCreator.MapSimulator.Interaction;
using System;
using System.Collections.Concurrent;
using System.Text;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class EngagementProposalInboxMessage
    {
        public EngagementProposalInboxMessage(
            string proposerName,
            string partnerName,
            int sealItemId,
            byte[] requestPayload,
            string customMessage,
            string source,
            string rawText)
        {
            ProposerName = proposerName?.Trim() ?? string.Empty;
            PartnerName = partnerName?.Trim() ?? string.Empty;
            SealItemId = sealItemId > 0 ? sealItemId : EngagementProposalRuntime.DefaultSealItemId;
            RequestPayload = requestPayload != null ? (byte[])requestPayload.Clone() : Array.Empty<byte>();
            CustomMessage = customMessage?.Trim() ?? string.Empty;
            Source = string.IsNullOrWhiteSpace(source) ? "engagement-inbox" : source;
            RawText = rawText ?? string.Empty;
        }

        public EngagementProposalInboxMessage(
            byte[] decisionPayload,
            string source,
            string rawText)
        {
            Kind = EngagementProposalInboxMessageKind.Decision;
            ProposerName = string.Empty;
            PartnerName = string.Empty;
            SealItemId = EngagementProposalRuntime.DefaultSealItemId;
            RequestPayload = decisionPayload != null ? (byte[])decisionPayload.Clone() : Array.Empty<byte>();
            CustomMessage = string.Empty;
            Source = string.IsNullOrWhiteSpace(source) ? "engagement-inbox" : source;
            RawText = rawText ?? string.Empty;
        }

        public EngagementProposalInboxMessageKind Kind { get; }

        public string ProposerName { get; }
        public string PartnerName { get; }
        public int SealItemId { get; }
        public byte[] RequestPayload { get; }
        public string CustomMessage { get; }
        public string Source { get; }
        public string RawText { get; }
    }

    public enum EngagementProposalInboxMessageKind
    {
        Request,
        Decision
    }

    public sealed class EngagementProposalInboxManager : IDisposable
    {
        public const string DefaultHost = "127.0.0.1";
        private const string RequestCommand = "request";
        private const string DecisionCommand = "decision";

        private readonly ConcurrentQueue<EngagementProposalInboxMessage> _pendingMessages = new();
        public string LastStatus { get; private set; } = "Engagement proposal inbox ready for role-session/local ingress.";

        public bool TryDequeue(out EngagementProposalInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void EnqueueProxy(EngagementProposalInboxMessage message)
        {
            EnqueueMessage(message);
        }

        public void EnqueueLocal(EngagementProposalInboxMessage message)
        {
            EnqueueMessage(message);
        }

        public void RecordDispatchResult(EngagementProposalInboxMessage message, bool success, string detail)
        {
            string source = message?.Source ?? "engagement-inbox";
            string summary = string.IsNullOrWhiteSpace(detail)
                ? DescribeMessage(message)
                : detail;
            LastStatus = success
                ? $"Applied engagement request from {source}: {summary}"
                : $"Ignored engagement request from {source}: {summary}";
        }

        public void Dispose()
        {
        }

        internal static string BuildRequestLine(EngagementProposalInboxDispatch dispatch)
        {
            ArgumentNullException.ThrowIfNull(dispatch.RequestPayload);

            StringBuilder builder = new();
            builder.Append(RequestCommand);
            builder.Append(' ');
            builder.Append(string.IsNullOrWhiteSpace(dispatch.ProposerName) ? "Player" : dispatch.ProposerName.Trim());
            builder.Append(' ');
            builder.Append(string.IsNullOrWhiteSpace(dispatch.PartnerName) ? "Partner" : dispatch.PartnerName.Trim());
            builder.Append(' ');
            builder.Append("payloadhex=");
            builder.Append(Convert.ToHexString(dispatch.RequestPayload));

            if (dispatch.SealItemId > 0)
            {
                builder.Append(' ');
                builder.Append(dispatch.SealItemId);
            }

            if (!string.IsNullOrWhiteSpace(dispatch.CustomMessage))
            {
                builder.Append(' ');
                builder.Append(dispatch.CustomMessage.Trim());
            }

            return builder.ToString();
        }
        public static bool TryParseLine(string text, out EngagementProposalInboxMessage message, out string error)
        {
            message = null;
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Engagement inbox line is empty.";
                return false;
            }

            string trimmed = text.Trim();
            if (trimmed.StartsWith("/engageinbox", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed["/engageinbox".Length..].TrimStart();
            }

            string[] tokens = trimmed.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                error = "Engagement inbox line is empty.";
                return false;
            }

            if (string.Equals(tokens[0], DecisionCommand, StringComparison.OrdinalIgnoreCase))
            {
                if (tokens.Length != 2)
                {
                    error = "Engagement inbox decision line must be: decision <payloadhex=..|payloadb64=..>";
                    return false;
                }

                if (!TryParsePayloadToken(tokens[1], out byte[] decisionPayload, out error))
                {
                    return false;
                }

                message = new EngagementProposalInboxMessage(
                    decisionPayload,
                    "engagement-inbox",
                    text);
                return true;
            }

            if (tokens.Length < 4 || !string.Equals(tokens[0], RequestCommand, StringComparison.OrdinalIgnoreCase))
            {
                error = "Engagement inbox line must be: request <proposerName> <partnerName> <payloadhex=..|payloadb64=..> [sealItemId] [message...] or decision <payloadhex=..|payloadb64=..>";
                return false;
            }

            string proposerName = tokens[1];
            string partnerName = tokens[2];
            if (string.IsNullOrWhiteSpace(proposerName) || string.IsNullOrWhiteSpace(partnerName))
            {
                error = "Engagement inbox request requires non-empty proposer and partner names.";
                return false;
            }

            if (!TryParsePayloadToken(tokens[3], out byte[] requestPayload, out error))
            {
                return false;
            }

            int sealItemId = EngagementProposalRuntime.DefaultSealItemId;
            int messageIndex = 4;
            if (tokens.Length > 4 && int.TryParse(tokens[4], out int parsedSealItemId) && parsedSealItemId > 0)
            {
                sealItemId = parsedSealItemId;
                messageIndex = 5;
            }

            string customMessage = tokens.Length > messageIndex
                ? string.Join(" ", tokens, messageIndex, tokens.Length - messageIndex)
                : string.Empty;

            message = new EngagementProposalInboxMessage(
                proposerName,
                partnerName,
                sealItemId,
                requestPayload,
                customMessage,
                "engagement-inbox",
                text);
            return true;
        }

        internal static bool TryParsePayloadToken(string token, out byte[] payload, out string error)
        {
            payload = Array.Empty<byte>();
            error = null;
            if (string.IsNullOrWhiteSpace(token))
            {
                error = "Engagement inbox request payload is missing.";
                return false;
            }

            const string payloadHexPrefix = "payloadhex=";
            const string payloadBase64Prefix = "payloadb64=";
            if (token.StartsWith(payloadHexPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string hex = token[payloadHexPrefix.Length..].Trim();
                if (hex.Length == 0 || (hex.Length % 2) != 0)
                {
                    error = "payloadhex= must contain an even-length hexadecimal byte string.";
                    return false;
                }

                try
                {
                    payload = Convert.FromHexString(hex);
                    return true;
                }
                catch (FormatException)
                {
                    error = "payloadhex= must contain only hexadecimal characters.";
                    return false;
                }
            }

            if (token.StartsWith(payloadBase64Prefix, StringComparison.OrdinalIgnoreCase))
            {
                string base64 = token[payloadBase64Prefix.Length..].Trim();
                if (base64.Length == 0)
                {
                    error = "payloadb64= must not be empty.";
                    return false;
                }

                try
                {
                    payload = Convert.FromBase64String(base64);
                    return true;
                }
                catch (FormatException)
                {
                    error = "payloadb64= must contain a valid base64 payload.";
                    return false;
                }
            }

            error = "Engagement inbox payload must use payloadhex=.. or payloadb64=..";
            return false;
        }

        internal static string DescribeMessage(EngagementProposalInboxMessage message)
        {
            if (message == null)
            {
                return "engagement payload";
            }

            return message.Kind == EngagementProposalInboxMessageKind.Decision
                ? "engagement decision payload"
                : $"engagement request {message.ProposerName} -> {message.PartnerName}";
        }

        private void EnqueueMessage(EngagementProposalInboxMessage message)
        {
            if (message == null)
            {
                return;
            }

            _pendingMessages.Enqueue(message);
            LastStatus = $"Queued {DescribeMessage(message)} from {message.Source}.";
        }
    }
}

