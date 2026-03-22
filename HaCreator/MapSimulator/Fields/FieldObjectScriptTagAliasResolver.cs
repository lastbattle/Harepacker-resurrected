using System;
using System.Collections.Generic;
using System.Text;

namespace HaCreator.MapSimulator.Fields
{
    internal static class FieldObjectScriptTagAliasResolver
    {
        public static IReadOnlyList<string> ResolvePublishedTags(string scriptName, IEnumerable<string> availableTags)
        {
            if (string.IsNullOrWhiteSpace(scriptName) || availableTags == null)
            {
                return Array.Empty<string>();
            }

            var availableTagSet = new HashSet<string>(availableTags, StringComparer.OrdinalIgnoreCase);
            if (availableTagSet.Count == 0)
            {
                return Array.Empty<string>();
            }

            var resolvedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddIfAvailable(scriptName.Trim(), availableTagSet, resolvedTags);

            string trimmedScriptName = scriptName.Trim();
            if (TryParseTrailingStage(trimmedScriptName, out string scriptBaseName, out int stage))
            {
                string camelCaseBaseName = ToCamelCase(scriptBaseName);
                if (!string.IsNullOrWhiteSpace(camelCaseBaseName))
                {
                    if (stage == 1)
                    {
                        AddIfAvailable(camelCaseBaseName, availableTagSet, resolvedTags);
                    }
                    else
                    {
                        AddIfAvailable(camelCaseBaseName + stage, availableTagSet, resolvedTags);
                    }
                }
            }
            else
            {
                AddIfAvailable(ToCamelCase(trimmedScriptName), availableTagSet, resolvedTags);
            }

            return resolvedTags.Count == 0 ? Array.Empty<string>() : new List<string>(resolvedTags);
        }

        private static void AddIfAvailable(string candidateTag, ISet<string> availableTags, ISet<string> resolvedTags)
        {
            if (!string.IsNullOrWhiteSpace(candidateTag) && availableTags.Contains(candidateTag))
            {
                resolvedTags.Add(candidateTag);
            }
        }

        private static bool TryParseTrailingStage(string scriptName, out string scriptBaseName, out int stage)
        {
            scriptBaseName = scriptName;
            stage = 0;
            if (string.IsNullOrWhiteSpace(scriptName))
            {
                return false;
            }

            int separatorIndex = scriptName.LastIndexOfAny(new[] { '_', '-' });
            if (separatorIndex < 0 || separatorIndex >= scriptName.Length - 1)
            {
                return false;
            }

            ReadOnlySpan<char> suffix = scriptName.AsSpan(separatorIndex + 1);
            if (!int.TryParse(suffix, out stage) || stage <= 0)
            {
                return false;
            }

            scriptBaseName = scriptName[..separatorIndex];
            return !string.IsNullOrWhiteSpace(scriptBaseName);
        }

        private static string ToCamelCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string[] parts = value.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0)
            {
                return null;
            }

            var builder = new StringBuilder();
            builder.Append(parts[0].ToLowerInvariant());
            for (int i = 1; i < parts.Length; i++)
            {
                string part = parts[i];
                if (part.Length == 0)
                {
                    continue;
                }

                builder.Append(char.ToUpperInvariant(part[0]));
                if (part.Length > 1)
                {
                    builder.Append(part[1..]);
                }
            }

            return builder.ToString();
        }
    }
}
