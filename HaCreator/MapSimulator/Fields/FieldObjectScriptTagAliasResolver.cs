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

            foreach (string quotedArgument in EnumerateQuotedAliasArguments(scriptName))
            {
                foreach (string candidate in EnumerateScriptAliasCandidatesCore(quotedArgument.Trim()))
                {
                    if (!string.IsNullOrWhiteSpace(candidate) && seen.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }

            foreach (string argument in EnumerateFunctionAliasArguments(scriptName))
            {
                foreach (string candidate in EnumerateScriptAliasCandidatesCore(argument.Trim()))
                {
                    if (!string.IsNullOrWhiteSpace(candidate) && seen.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }
        }

        private static IEnumerable<string> EnumerateScriptAliasCandidatesCore(string scriptName)
        {
            yield return scriptName;

            string transportStem = TrimTransportSuffix(scriptName);
            if (!string.Equals(transportStem, scriptName, StringComparison.OrdinalIgnoreCase))
            {
                yield return transportStem;
            }

            string fileName = TrimPathPrefix(transportStem);
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

        private static IEnumerable<string> EnumerateQuotedAliasArguments(string scriptName)
        {
            if (string.IsNullOrWhiteSpace(scriptName)
                || scriptName.IndexOf('(') < 0)
            {
                yield break;
            }

            char quote = '\0';
            int argumentStart = -1;
            for (int i = 0; i < scriptName.Length; i++)
            {
                char current = scriptName[i];
                if (quote == '\0')
                {
                    if (current == '"' || current == '\'')
                    {
                        quote = current;
                        argumentStart = i + 1;
                    }

                    continue;
                }

                if (current == '\\' && i + 1 < scriptName.Length)
                {
                    i++;
                    continue;
                }

                if (current != quote)
                {
                    continue;
                }

                if (argumentStart >= 0 && i > argumentStart)
                {
                    yield return scriptName[argumentStart..i];
                }

                quote = '\0';
                argumentStart = -1;
            }
        }

        private static IEnumerable<string> EnumerateFunctionAliasArguments(string scriptName)
        {
            if (string.IsNullOrWhiteSpace(scriptName)
                || scriptName.IndexOf('(') < 0)
            {
                yield break;
            }

            foreach (string argument in EnumerateFunctionArguments(scriptName))
            {
                string normalizedArgument = NormalizeFunctionAliasArgument(argument);
                if (!IsPotentialAliasArgument(normalizedArgument))
                {
                    continue;
                }

                yield return normalizedArgument;

                foreach (string nestedArgument in EnumerateFunctionAliasArguments(normalizedArgument))
                {
                    yield return nestedArgument;
                }
            }
        }

        private static IEnumerable<string> EnumerateFunctionArguments(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            int openIndex = value.IndexOf('(');
            while (openIndex >= 0)
            {
                int closeIndex = FindMatchingCloseParenthesis(value, openIndex);
                if (closeIndex <= openIndex + 1)
                {
                    openIndex = value.IndexOf('(', openIndex + 1);
                    continue;
                }

                foreach (string argument in SplitFunctionArguments(value[(openIndex + 1)..closeIndex]))
                {
                    yield return argument;
                }

                openIndex = value.IndexOf('(', openIndex + 1);
            }
        }

        private static int FindMatchingCloseParenthesis(string value, int openIndex)
        {
            int depth = 0;
            char quote = '\0';
            for (int i = openIndex; i < value.Length; i++)
            {
                char current = value[i];
                if (quote != '\0')
                {
                    if (current == '\\' && i + 1 < value.Length)
                    {
                        i++;
                        continue;
                    }

                    if (current == quote)
                    {
                        quote = '\0';
                    }

                    continue;
                }

                if (current == '"' || current == '\'')
                {
                    quote = current;
                    continue;
                }

                if (current == '(')
                {
                    depth++;
                    continue;
                }

                if (current != ')')
                {
                    continue;
                }

                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }

            return -1;
        }

        private static IEnumerable<string> SplitFunctionArguments(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            int tokenStart = 0;
            int groupingDepth = 0;
            char quote = '\0';
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (quote != '\0')
                {
                    if (current == '\\' && i + 1 < value.Length)
                    {
                        i++;
                        continue;
                    }

                    if (current == quote)
                    {
                        quote = '\0';
                    }

                    continue;
                }

                if (current == '"' || current == '\'')
                {
                    quote = current;
                    continue;
                }

                if (current == '(' || current == '[' || current == '{')
                {
                    groupingDepth++;
                    continue;
                }

                if (current == ')' || current == ']' || current == '}')
                {
                    if (groupingDepth > 0)
                    {
                        groupingDepth--;
                    }

                    continue;
                }

                if (groupingDepth > 0 || current is not (',' or ';'))
                {
                    continue;
                }

                yield return value[tokenStart..i];
                tokenStart = i + 1;
            }

            yield return value[tokenStart..];
        }

        private static string NormalizeFunctionAliasArgument(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Trim().Trim('"', '\'').Trim();
        }

        private static bool IsPotentialAliasArgument(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            bool hasAliasCharacter = false;
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (char.IsLetter(current) || current is '_' or '-' or '/' or '\\' or '.')
                {
                    hasAliasCharacter = true;
                    continue;
                }

                if (char.IsDigit(current) || current == '(' || current == ')' || current == '"' || current == '\'')
                {
                    continue;
                }

                return false;
            }

            return hasAliasCharacter;
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

        private static string TrimTransportSuffix(string scriptName)
        {
            if (string.IsNullOrWhiteSpace(scriptName))
            {
                return scriptName;
            }

            int suffixIndex = scriptName.IndexOfAny(new[] { '?', '#' });
            if (suffixIndex > 0)
            {
                scriptName = scriptName[..suffixIndex];
            }

            int argumentIndex = scriptName.IndexOf('(');
            if (argumentIndex > 0 && scriptName.EndsWith(")", StringComparison.Ordinal))
            {
                scriptName = scriptName[..argumentIndex];
            }

            return scriptName.Trim();
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
