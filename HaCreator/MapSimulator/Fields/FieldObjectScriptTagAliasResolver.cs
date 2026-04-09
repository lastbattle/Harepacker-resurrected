using System;
using System.Collections.Generic;
using System.Text;

namespace HaCreator.MapSimulator.Fields
{
    internal static class FieldObjectScriptTagAliasResolver
    {
        internal readonly record struct PublishedTagMutation(
            IReadOnlyList<string> TagsToEnable,
            IReadOnlyList<string> TagsToDisable);

        public static IReadOnlyList<string> ResolvePublishedTags(string scriptName, IEnumerable<string> availableTags)
        {
            return ResolvePublishedTagMutation(scriptName, availableTags).TagsToEnable;
        }

        public static PublishedTagMutation ResolvePublishedTagMutation(string scriptName, IEnumerable<string> availableTags)
        {
            if (string.IsNullOrWhiteSpace(scriptName) || availableTags == null)
            {
                return new PublishedTagMutation(Array.Empty<string>(), Array.Empty<string>());
            }

            var availableTagSet = new HashSet<string>(availableTags, StringComparer.OrdinalIgnoreCase);
            if (availableTagSet.Count == 0)
            {
                return new PublishedTagMutation(Array.Empty<string>(), Array.Empty<string>());
            }

            var resolvedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var retiredTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string candidateScriptName in EnumerateScriptAliasCandidates(scriptName))
            {
                AddIfAvailable(candidateScriptName, availableTagSet, resolvedTags);
                AddQuestScriptAliasCandidates(candidateScriptName, availableTagSet, resolvedTags);

                if (TryParseTrailingStage(candidateScriptName, out string scriptBaseName, out int stage))
                {
                    AddStageFamilyTags(scriptBaseName, stage, availableTagSet, resolvedTags, retiredTags);

                    string camelCaseBaseName = ToCamelCase(scriptBaseName);
                    if (!string.IsNullOrWhiteSpace(camelCaseBaseName)
                        && !string.Equals(camelCaseBaseName, scriptBaseName, StringComparison.OrdinalIgnoreCase))
                    {
                        AddStageFamilyTags(camelCaseBaseName, stage, availableTagSet, resolvedTags, retiredTags);
                    }

                    continue;
                }

                AddIfAvailable(ToCamelCase(candidateScriptName), availableTagSet, resolvedTags);
            }

            return new PublishedTagMutation(
                resolvedTags.Count == 0 ? Array.Empty<string>() : new List<string>(resolvedTags),
                retiredTags.Count == 0 ? Array.Empty<string>() : new List<string>(retiredTags));
        }

        public static bool TryResolvePublishedTagMutation(
            string scriptName,
            IEnumerable<string> availableTags,
            out PublishedTagMutation mutation)
        {
            mutation = ResolvePublishedTagMutation(scriptName, availableTags);
            return mutation.TagsToEnable.Count > 0 || mutation.TagsToDisable.Count > 0;
        }

        private static void AddIfAvailable(string candidateTag, ISet<string> availableTags, ISet<string> resolvedTags)
        {
            if (!string.IsNullOrWhiteSpace(candidateTag) && availableTags.Contains(candidateTag))
            {
                resolvedTags.Add(candidateTag);
            }
        }

        private static IEnumerable<string> EnumerateScriptAliasCandidates(string scriptName)
        {
            if (string.IsNullOrWhiteSpace(scriptName))
            {
                yield break;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string candidate in EnumerateScriptAliasCandidatesCore(scriptName.Trim()))
            {
                if (!string.IsNullOrWhiteSpace(candidate) && seen.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }

        private static IEnumerable<string> EnumerateScriptAliasCandidatesCore(string scriptName)
        {
            yield return scriptName;

            string fileName = TrimPathPrefix(scriptName);
            if (!string.Equals(fileName, scriptName, StringComparison.OrdinalIgnoreCase))
            {
                yield return fileName;
            }

            string fileStem = TrimFileExtension(fileName);
            if (!string.Equals(fileStem, fileName, StringComparison.OrdinalIgnoreCase))
            {
                yield return fileStem;
            }
        }

        private static string TrimPathPrefix(string scriptName)
        {
            if (string.IsNullOrWhiteSpace(scriptName))
            {
                return scriptName;
            }

            int separatorIndex = scriptName.LastIndexOfAny(new[] { '/', '\\' });
            return separatorIndex >= 0 && separatorIndex < scriptName.Length - 1
                ? scriptName[(separatorIndex + 1)..]
                : scriptName;
        }

        private static string TrimFileExtension(string scriptName)
        {
            if (string.IsNullOrWhiteSpace(scriptName))
            {
                return scriptName;
            }

            int extensionIndex = scriptName.LastIndexOf('.');
            return extensionIndex > 0
                ? scriptName[..extensionIndex]
                : scriptName;
        }

        private static void AddQuestScriptAliasCandidates(
            string scriptName,
            ISet<string> availableTags,
            ISet<string> resolvedTags)
        {
            if (!TryParseQuestScriptAlias(scriptName, out string questScriptBaseName, out string questIdTag))
            {
                return;
            }

            AddIfAvailable(questScriptBaseName, availableTags, resolvedTags);
            AddIfAvailable(questIdTag, availableTags, resolvedTags);
        }

        private static void AddStageFamilyTags(
            string familyBaseName,
            int activeStage,
            ISet<string> availableTags,
            ISet<string> resolvedTags,
            ISet<string> retiredTags)
        {
            if (string.IsNullOrWhiteSpace(familyBaseName))
            {
                return;
            }

            if (activeStage <= 1)
            {
                AddIfAvailable(familyBaseName, availableTags, resolvedTags);
            }
            else if (!TryAddAvailableStageSpecificTag(familyBaseName, activeStage, availableTags, resolvedTags))
            {
                AddIfAvailable(familyBaseName, availableTags, resolvedTags);
            }

            AddSiblingStageTags(familyBaseName, activeStage, availableTags, resolvedTags, retiredTags);
        }

        private static bool TryAddAvailableStageSpecificTag(
            string familyBaseName,
            int activeStage,
            ISet<string> availableTags,
            ISet<string> resolvedTags)
        {
            if (string.IsNullOrWhiteSpace(familyBaseName) || activeStage < 0)
            {
                return false;
            }

            int originalCount = resolvedTags.Count;
            AddIfAvailable(familyBaseName + activeStage, availableTags, resolvedTags);
            AddIfAvailable(familyBaseName + "_" + activeStage, availableTags, resolvedTags);
            AddIfAvailable(familyBaseName + "-" + activeStage, availableTags, resolvedTags);
            return resolvedTags.Count != originalCount;
        }

        private static void AddSiblingStageTags(
            string familyBaseName,
            int activeStage,
            ISet<string> availableTags,
            ISet<string> resolvedTags,
            ISet<string> retiredTags)
        {
            if (string.IsNullOrWhiteSpace(familyBaseName) || availableTags == null)
            {
                return;
            }

            foreach (string availableTag in availableTags)
            {
                if (resolvedTags.Contains(availableTag))
                {
                    continue;
                }

                if (!TryParseStageTagCandidate(
                    availableTag,
                    familyBaseName,
                    treatBaseTagAsStageZero: activeStage == 0,
                    out int stage))
                {
                    continue;
                }

                if (stage == activeStage)
                {
                    resolvedTags.Add(availableTag);
                }
                else
                {
                    retiredTags.Add(availableTag);
                }
            }
        }

        private static bool TryParseStageTagCandidate(
            string availableTag,
            string familyBaseName,
            bool treatBaseTagAsStageZero,
            out int stage)
        {
            stage = 0;
            if (string.IsNullOrWhiteSpace(availableTag)
                || string.IsNullOrWhiteSpace(familyBaseName)
                || !availableTag.StartsWith(familyBaseName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (availableTag.Length == familyBaseName.Length)
            {
                stage = treatBaseTagAsStageZero ? 0 : 1;
                return true;
            }

            ReadOnlySpan<char> suffix = availableTag.AsSpan(familyBaseName.Length);
            if (suffix.Length > 1 && (suffix[0] == '_' || suffix[0] == '-'))
            {
                suffix = suffix[1..];
            }

            return int.TryParse(suffix, out stage) && stage >= 0;
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
            if (separatorIndex >= 0 && separatorIndex < scriptName.Length - 1)
            {
                ReadOnlySpan<char> separatedSuffix = scriptName.AsSpan(separatorIndex + 1);
                if (int.TryParse(separatedSuffix, out stage) && stage >= 0)
                {
                    scriptBaseName = scriptName[..separatorIndex];
                    return !string.IsNullOrWhiteSpace(scriptBaseName);
                }
            }

            int suffixStart = scriptName.Length;
            while (suffixStart > 0 && char.IsDigit(scriptName[suffixStart - 1]))
            {
                suffixStart--;
            }

            if (suffixStart <= 0 || suffixStart >= scriptName.Length)
            {
                return false;
            }

            ReadOnlySpan<char> suffix = scriptName.AsSpan(suffixStart);
            if (!int.TryParse(suffix, out stage) || stage < 0)
            {
                return false;
            }

            scriptBaseName = scriptName[..suffixStart];
            return !string.IsNullOrWhiteSpace(scriptBaseName);
        }

        private static bool TryParseQuestScriptAlias(
            string scriptName,
            out string questScriptBaseName,
            out string questIdTag)
        {
            questScriptBaseName = null;
            questIdTag = null;
            if (string.IsNullOrWhiteSpace(scriptName)
                || scriptName.Length < 4
                || scriptName[0] is not ('q' or 'Q'))
            {
                return false;
            }

            int suffixIndex = scriptName.Length - 1;
            char suffix = char.ToLowerInvariant(scriptName[suffixIndex]);
            if (suffix != 's' && suffix != 'e')
            {
                return false;
            }

            string questId = scriptName[1..suffixIndex];
            if (string.IsNullOrWhiteSpace(questId))
            {
                return false;
            }

            for (int i = 0; i < questId.Length; i++)
            {
                if (!char.IsDigit(questId[i]))
                {
                    return false;
                }
            }

            questScriptBaseName = scriptName[..suffixIndex];
            questIdTag = questId;
            return true;
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
