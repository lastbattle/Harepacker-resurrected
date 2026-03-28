using System;
using System.Linq;

namespace HaCreator.MapSimulator.Managers
{
    internal static class LoginPacketPayloadArgumentNormalizer
    {
        public static string[] Normalize(LoginPacketType packetType, string[] arguments)
        {
            if (arguments == null || arguments.Length == 0)
            {
                return Array.Empty<string>();
            }

            if (!SupportsCollapsedHexPayload(packetType) || !ShouldCollapseHexPayload(arguments))
            {
                return arguments;
            }

            string compactHex = string.Concat(arguments.Select(NormalizeHexFragment));
            return new[] { $"payloadhex={compactHex}" };
        }

        private static bool SupportsCollapsedHexPayload(LoginPacketType packetType)
        {
            return packetType is LoginPacketType.WorldInformation
                or LoginPacketType.SelectWorldResult
                or LoginPacketType.RecommendWorldMessage;
        }

        private static bool ShouldCollapseHexPayload(string[] arguments)
        {
            if (arguments.Length == 1)
            {
                string token = NormalizeHexFragment(arguments[0]);
                return token.Length >= 4 && IsEvenLengthHex(token);
            }

            if (arguments.Length < 4)
            {
                return false;
            }

            return arguments.All(argument =>
            {
                string token = NormalizeHexFragment(argument);
                return token.Length > 0 && IsEvenLengthHex(token);
            });
        }

        private static string NormalizeHexFragment(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string normalized = text.Trim();
            if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[2..];
            }

            return new string(normalized.Where(ch => !char.IsWhiteSpace(ch) && ch != '-').ToArray());
        }

        private static bool IsEvenLengthHex(string text)
        {
            return (text.Length & 1) == 0 && text.All(Uri.IsHexDigit);
        }
    }
}
