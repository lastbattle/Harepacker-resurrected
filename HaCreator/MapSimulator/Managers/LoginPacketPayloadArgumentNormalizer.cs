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

            arguments = NormalizePayloadAliases(arguments);
            if (!SupportsCollapsedHexPayload(packetType) || !ShouldCollapseHexPayload(arguments))
            {
                return arguments;
            }

            string compactHex = string.Concat(arguments.Select(NormalizeHexFragment));
            return new[] { $"payloadhex={compactHex}" };
        }

        private static string[] NormalizePayloadAliases(string[] arguments)
        {
            string[] normalizedArguments = new string[arguments.Length];
            for (int index = 0; index < arguments.Length; index++)
            {
                normalizedArguments[index] = NormalizePayloadAlias(arguments[index]);
            }

            return normalizedArguments;
        }

        private static string NormalizePayloadAlias(string argument)
        {
            if (string.IsNullOrWhiteSpace(argument))
            {
                return argument;
            }

            string trimmed = argument.Trim();
            if (TryNormalizePayloadAlias(trimmed, "payload", out string normalized) ||
                TryNormalizePayloadAlias(trimmed, "hex", out normalized) ||
                TryNormalizePayloadAlias(trimmed, "data", out normalized) ||
                TryNormalizePayloadAlias(trimmed, "bytes", out normalized) ||
                TryNormalizePayloadAlias(trimmed, "raw", out normalized))
            {
                return normalized;
            }

            if (TryNormalizeBase64Alias(trimmed, "b64", out normalized) ||
                TryNormalizeBase64Alias(trimmed, "base64", out normalized))
            {
                return normalized;
            }

            return trimmed;
        }

        private static bool TryNormalizePayloadAlias(string argument, string alias, out string normalized)
        {
            normalized = null;
            if (!TrySplitAlias(argument, alias, out string value))
            {
                return false;
            }

            string compactHex = NormalizeHexFragment(value);
            if (compactHex.Length > 0 && IsEvenLengthHex(compactHex))
            {
                normalized = $"payloadhex={compactHex}";
                return true;
            }

            normalized = $"payloadb64={value.Trim()}";
            return true;
        }

        private static bool TryNormalizeBase64Alias(string argument, string alias, out string normalized)
        {
            normalized = null;
            if (!TrySplitAlias(argument, alias, out string value))
            {
                return false;
            }

            normalized = $"payloadb64={value.Trim()}";
            return true;
        }

        private static bool TrySplitAlias(string argument, string alias, out string value)
        {
            value = null;
            if (argument.Length <= alias.Length + 1)
            {
                return false;
            }

            if (!argument.StartsWith(alias, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            char separator = argument[alias.Length];
            if (separator != '=' && separator != ':')
            {
                return false;
            }

            value = argument[(alias.Length + 1)..];
            return true;
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
