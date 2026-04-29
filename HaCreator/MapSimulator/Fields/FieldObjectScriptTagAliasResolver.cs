using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using HaCreator.MapSimulator.Interaction;

namespace HaCreator.MapSimulator.Fields
{
    internal static class FieldObjectScriptTagAliasResolver
    {
        private static readonly Regex LocalAliasAssignmentPattern = new(
            @"(?:^|[;\{\(\s,])(?:(?:var|let|const)\s+)?(?<lhs>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<rhs>[^;,\r\n]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex FunctionBodyAliasInvocationPattern = new(
            @"(?:^|[^A-Za-z0-9_\.])(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex BracketMemberAliasPattern = new(
            @"\[\s*[""'](?<name>[A-Za-z_][A-Za-z0-9_]*)[""']\s*\]",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex BracketMemberAliasVariablePattern = new(
            @"\[\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\]",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex BracketMemberAliasInvocationPattern = new(
            @"(?:^|[^A-Za-z0-9_])(?:this|owner|[A-Za-z_][A-Za-z0-9_]*)\s*\[\s*[""'](?<name>[A-Za-z_][A-Za-z0-9_]*)[""']\s*\]\s*\(",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex BracketMemberAliasInvokerInvocationPattern = new(
            @"(?:^|[^A-Za-z0-9_])(?:this|owner|[A-Za-z_][A-Za-z0-9_]*)\s*\[\s*[""'](?<name>[A-Za-z_][A-Za-z0-9_]*)[""']\s*\]\s*\.\s*(?:call|apply|bind)\s*\(",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex MemberAliasInvocationPattern = new(
            @"(?:^|[^A-Za-z0-9_])(?:this|owner|[A-Za-z_][A-Za-z0-9_]*)\s*\.\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex MemberAliasReferencePattern = new(
            @"(?:^|[^A-Za-z0-9_])(?:this|owner|[A-Za-z_][A-Za-z0-9_]*)\s*\.\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex MemberAliasInvokerInvocationPattern = new(
            @"(?:^|[^A-Za-z0-9_])(?:this|owner|[A-Za-z_][A-Za-z0-9_]*)\s*\.\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\.\s*(?:call|apply|bind)\s*\(",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex FunctionBodyReturnMemberAliasPattern = new(
            @"\breturn\s+(?:this|owner|[A-Za-z_][A-Za-z0-9_]*)\s*\.\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex FunctionBodyReturnBracketMemberAliasPattern = new(
            @"\breturn\s+(?<owner>this|owner|[A-Za-z_][A-Za-z0-9_]*)\s*\[(?<expr>[^\]]+)\]",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex FunctionBodyReturnAliasVariablePattern = new(
            @"\breturn\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex FunctionBodyReturnExpressionPattern = new(
            @"\breturn\s+(?<expr>[^;\{\}]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex FunctionDeclarationPattern = new(
            @"(?:^|[;\{\(\s])function\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\([^)]*\)\s*\{(?<body>[^{}]*)\}",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ObjectLiteralAssignmentPattern = new(
            @"(?:^|[;\{\(\s,])(?:(?:var|let|const)\s+)?(?<lhs>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*\{(?<body>[^{}]*)\}",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ArrayLiteralAssignmentPattern = new(
            @"(?:^|[;\{\(\s,])(?:(?:var|let|const)\s+)?(?<lhs>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*\[(?<body>[^\[\]]*)\]",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ArrayDestructuringAliasAssignmentPattern = new(
            @"(?:^|[;\{\(\s,])(?:(?:var|let|const)\s+)?\[(?<lhs>[^\[\]]*)\]\s*=\s*(?<source>[A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ObjectDestructuringAliasAssignmentPattern = new(
            @"(?:^|[;\{\(\s,])(?:(?:var|let|const)\s+)?\{(?<lhs>[^{}]*)\}\s*=\s*(?<source>[A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ObjectMemberAliasAssignmentPattern = new(
            @"(?:^|[;\{\(\s,])(?<object>[A-Za-z_][A-Za-z0-9_]*)\s*(?:(?:\.\s*(?<member>[A-Za-z_][A-Za-z0-9_]*))|(?:\[\s*(?<expr>[^\]]+)\s*\]))\s*=\s*(?<rhs>[^;,\r\n]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ArrayMutatorAliasCallPattern = new(
            @"(?:^|[;\{\(\s,])(?<object>[A-Za-z_][A-Za-z0-9_]*)\s*\.\s*(?<method>push|unshift|splice)\s*\(",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ArrayConcatAliasAssignmentPattern = new(
            @"(?:^|[;\{\(\s,])(?:(?:var|let|const)\s+)?(?<lhs>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<source>[A-Za-z_][A-Za-z0-9_]*)\s*\.\s*concat\s*\(",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ArrayCopyAliasAssignmentPattern = new(
            @"(?:^|[;\{\(\s,])(?:(?:var|let|const)\s+)?(?<lhs>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<source>[A-Za-z_][A-Za-z0-9_]*)\s*\.\s*(?<method>slice)\s*\(",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ObjectAssignAliasAssignmentPattern = new(
            @"(?:^|[;\{\(\s,])(?:(?:var|let|const)\s+)?(?<lhs>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*Object\s*\.\s*assign\s*\(",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ArrayFactoryAliasAssignmentPattern = new(
            @"(?:^|[;\{\(\s,])(?:(?:var|let|const)\s+)?(?<lhs>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*Array\s*\.\s*(?<method>of|from)\s*\(",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        internal readonly record struct PublishedTagMutation(
            IReadOnlyList<string> TagsToEnable,
            IReadOnlyList<string> TagsToDisable);

        internal readonly record struct ScriptAliasPublication(
            string ScriptName,
            int DelayMs);

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

        internal static bool TryResolveTimerCallbackDelayMs(string scriptName, out int delayMs)
        {
            delayMs = 0;
            if (string.IsNullOrWhiteSpace(scriptName))
            {
                return false;
            }

            bool foundDelay = false;
            foreach ((string FunctionName, IReadOnlyList<string> Arguments) call in EnumerateFunctionCalls(scriptName))
            {
                if (!IsDelayedCallbackFunctionName(call.FunctionName))
                {
                    continue;
                }

                int firstDelayCandidateIndex = call.Arguments.Count > 1 ? 1 : call.Arguments.Count;
                for (int i = firstDelayCandidateIndex; i < call.Arguments.Count; i++)
                {
                    string argument = NormalizeFunctionAliasArgument(call.Arguments[i]);
                    if (!TryParsePositiveDelayMs(argument, out int parsedDelayMs))
                    {
                        continue;
                    }

                    delayMs = Math.Max(delayMs, parsedDelayMs);
                    foundDelay = true;
                }
            }

            return foundDelay;
        }

        internal static IReadOnlyList<ScriptAliasPublication> ResolveTimerCallbackPublications(string scriptName)
        {
            if (string.IsNullOrWhiteSpace(scriptName))
            {
                return Array.Empty<ScriptAliasPublication>();
            }

            var publications = new List<ScriptAliasPublication>();
            var seen = new HashSet<(string ScriptName, int DelayMs)>();
            IReadOnlyDictionary<string, string> localAliasMap = BuildLocalAliasMap(scriptName);
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap =
                BuildObjectMemberAliasMap(scriptName, localAliasMap);
            localAliasMap = BuildLocalAliasMap(scriptName, objectMemberAliasMap);
            IReadOnlyDictionary<string, IReadOnlyList<string>> localAliasCandidateMap =
                BuildLocalAliasCandidateMap(scriptName, localAliasMap, objectMemberAliasMap);
            CollectTimerCallbackPublications(
                scriptName,
                inheritedDelayMs: 0,
                publications,
                seen,
                localAliasMap,
                objectMemberAliasMap,
                localAliasCandidateMap);
            return publications.Count == 0
                ? Array.Empty<ScriptAliasPublication>()
                : publications;
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

            IReadOnlyDictionary<string, string> localAliasMap = BuildLocalAliasMap(scriptName);
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap =
                BuildObjectMemberAliasMap(scriptName, localAliasMap);
            foreach (string candidate in EnumerateScriptAliasCandidates(scriptName, localAliasMap, objectMemberAliasMap))
            {
                yield return candidate;
            }
        }

        private static IEnumerable<string> EnumerateScriptAliasCandidates(
            string scriptName,
            IReadOnlyDictionary<string, string> localAliasMap,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string candidate in EnumerateScriptAliasCandidatesCore(scriptName.Trim()))
            {
                foreach (string canonicalCandidate in EnumerateCanonicalAliasCandidates(candidate, localAliasMap, objectMemberAliasMap))
                {
                    if (!string.IsNullOrWhiteSpace(canonicalCandidate) && seen.Add(canonicalCandidate))
                    {
                        yield return canonicalCandidate;
                    }
                }
            }

            foreach (string quotedArgument in EnumerateQuotedAliasArguments(scriptName))
            {
                foreach (string candidate in EnumerateScriptAliasCandidatesCore(quotedArgument.Trim()))
                {
                    foreach (string canonicalCandidate in EnumerateCanonicalAliasCandidates(candidate, localAliasMap, objectMemberAliasMap))
                    {
                        if (!string.IsNullOrWhiteSpace(canonicalCandidate) && seen.Add(canonicalCandidate))
                        {
                            yield return canonicalCandidate;
                        }
                    }
                }
            }

            foreach (string argument in EnumerateFunctionAliasArguments(scriptName))
            {
                foreach (string candidate in EnumerateScriptAliasCandidatesCore(argument.Trim()))
                {
                    foreach (string canonicalCandidate in EnumerateCanonicalAliasCandidates(candidate, localAliasMap, objectMemberAliasMap))
                    {
                        if (!string.IsNullOrWhiteSpace(canonicalCandidate) && seen.Add(canonicalCandidate))
                        {
                            yield return canonicalCandidate;
                        }
                    }
                }
            }

            foreach (string functionAliasName in EnumerateFunctionAliasNames(scriptName))
            {
                foreach (string candidate in EnumerateScriptAliasCandidatesCore(functionAliasName.Trim()))
                {
                    foreach (string canonicalCandidate in EnumerateCanonicalAliasCandidates(candidate, localAliasMap, objectMemberAliasMap))
                    {
                        if (!string.IsNullOrWhiteSpace(canonicalCandidate) && seen.Add(canonicalCandidate))
                        {
                            yield return canonicalCandidate;
                        }
                    }
                }
            }
        }

        private static IReadOnlyDictionary<string, string> BuildLocalAliasMap(
            string scriptName,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap = null)
        {
            if (string.IsNullOrWhiteSpace(scriptName))
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var localAliasMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in LocalAliasAssignmentPattern.Matches(scriptName))
            {
                string leftName = NormalizeFunctionAliasArgument(match.Groups["lhs"]?.Value);
                string rightValue = NormalizeFunctionAliasArgument(match.Groups["rhs"]?.Value);
                if (!IsPotentialFunctionAliasName(leftName))
                {
                    continue;
                }

                string resolvedAlias = ResolveAssignmentAliasCandidate(
                    rightValue,
                    localAliasMap,
                    objectMemberAliasMap);
                if (string.IsNullOrWhiteSpace(resolvedAlias))
                {
                    continue;
                }

                localAliasMap[leftName] = resolvedAlias;
            }

            foreach (Match match in FunctionDeclarationPattern.Matches(scriptName))
            {
                string functionName = NormalizeFunctionAliasArgument(match.Groups["name"]?.Value).TrimEnd(';');
                string functionBody = match.Groups["body"]?.Value;
                if (!IsPotentialFunctionAliasName(functionName) || string.IsNullOrWhiteSpace(functionBody))
                {
                    continue;
                }

                string resolvedAlias = ResolveFunctionExpressionAliasCandidate(
                    "function(){" + functionBody + "}",
                    localAliasMap,
                    objectMemberAliasMap);
                if (string.IsNullOrWhiteSpace(resolvedAlias))
                {
                    continue;
                }

                localAliasMap[functionName] = resolvedAlias;
            }

            AddDestructuredLocalAliases(localAliasMap, scriptName, objectMemberAliasMap);
            return localAliasMap;
        }

        private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildLocalAliasCandidateMap(
            string scriptName,
            IReadOnlyDictionary<string, string> localAliasMap,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap)
        {
            if (string.IsNullOrWhiteSpace(scriptName))
            {
                return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            }

            var localAliasCandidateMap = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in LocalAliasAssignmentPattern.Matches(scriptName))
            {
                string leftName = NormalizeFunctionAliasArgument(match.Groups["lhs"]?.Value);
                string rightValue = NormalizeFunctionAliasArgument(match.Groups["rhs"]?.Value);
                if (!IsPotentialFunctionAliasName(leftName))
                {
                    continue;
                }

                IReadOnlyList<string> resolvedAliases = ResolveAssignmentAliasCandidates(
                    rightValue,
                    localAliasMap,
                    objectMemberAliasMap);
                if (resolvedAliases.Count > 0)
                {
                    localAliasCandidateMap[leftName] = resolvedAliases;
                }
            }

            foreach (Match match in FunctionDeclarationPattern.Matches(scriptName))
            {
                string functionName = NormalizeFunctionAliasArgument(match.Groups["name"]?.Value).TrimEnd(';');
                string functionBody = match.Groups["body"]?.Value;
                if (!IsPotentialFunctionAliasName(functionName) || string.IsNullOrWhiteSpace(functionBody))
                {
                    continue;
                }

                IReadOnlyList<string> resolvedAliases = ResolveAssignmentAliasCandidates(
                    "function(){" + functionBody + "}",
                    localAliasMap,
                    objectMemberAliasMap);
                if (resolvedAliases.Count > 0)
                {
                    localAliasCandidateMap[functionName] = resolvedAliases;
                }
            }

            AddDestructuredLocalAliasCandidates(
                localAliasCandidateMap,
                scriptName,
                objectMemberAliasMap);
            return localAliasCandidateMap;
        }

        private static void AddDestructuredLocalAliases(
            IDictionary<string, string> localAliasMap,
            string scriptName,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap)
        {
            if (localAliasMap == null || string.IsNullOrWhiteSpace(scriptName) || objectMemberAliasMap == null)
            {
                return;
            }

            foreach ((string LocalName, string AliasName) destructuredAlias in EnumerateDestructuredLocalAliases(
                         scriptName,
                         objectMemberAliasMap))
            {
                localAliasMap[destructuredAlias.LocalName] = destructuredAlias.AliasName;
            }
        }

        private static void AddDestructuredLocalAliasCandidates(
            IDictionary<string, IReadOnlyList<string>> localAliasCandidateMap,
            string scriptName,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap)
        {
            if (localAliasCandidateMap == null || string.IsNullOrWhiteSpace(scriptName) || objectMemberAliasMap == null)
            {
                return;
            }

            foreach ((string LocalName, string AliasName) destructuredAlias in EnumerateDestructuredLocalAliases(
                         scriptName,
                         objectMemberAliasMap))
            {
                localAliasCandidateMap[destructuredAlias.LocalName] = new[] { destructuredAlias.AliasName };
            }
        }

        private static IEnumerable<(string LocalName, string AliasName)> EnumerateDestructuredLocalAliases(
            string scriptName,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap)
        {
            if (string.IsNullOrWhiteSpace(scriptName) || objectMemberAliasMap == null || objectMemberAliasMap.Count == 0)
            {
                yield break;
            }

            foreach (Match match in ArrayDestructuringAliasAssignmentPattern.Matches(scriptName))
            {
                string sourceName = NormalizeFunctionAliasArgument(match.Groups["source"]?.Value).TrimEnd(';');
                if (string.IsNullOrWhiteSpace(sourceName)
                    || !objectMemberAliasMap.TryGetValue(sourceName, out IReadOnlyDictionary<string, string> memberAliasMap))
                {
                    continue;
                }

                IReadOnlyList<string> localNames = SplitTopLevelByComma(match.Groups["lhs"]?.Value);
                for (int i = 0; i < localNames.Count; i++)
                {
                    string localName = NormalizeFunctionAliasArgument(localNames[i]).TrimEnd(';');
                    if (!IsPotentialFunctionAliasName(localName)
                        || !memberAliasMap.TryGetValue(i.ToString(System.Globalization.CultureInfo.InvariantCulture), out string aliasName)
                        || !IsPotentialFunctionAliasName(aliasName))
                    {
                        continue;
                    }

                    yield return (localName, aliasName);
                }
            }

            foreach (Match match in ObjectDestructuringAliasAssignmentPattern.Matches(scriptName))
            {
                string sourceName = NormalizeFunctionAliasArgument(match.Groups["source"]?.Value).TrimEnd(';');
                if (string.IsNullOrWhiteSpace(sourceName)
                    || !objectMemberAliasMap.TryGetValue(sourceName, out IReadOnlyDictionary<string, string> memberAliasMap))
                {
                    continue;
                }

                foreach (string rawMember in SplitTopLevelByComma(match.Groups["lhs"]?.Value))
                {
                    if (!TryParseObjectDestructuringAliasMember(rawMember, out string memberName, out string localName)
                        || !memberAliasMap.TryGetValue(memberName, out string aliasName)
                        || !IsPotentialFunctionAliasName(aliasName))
                    {
                        continue;
                    }

                    yield return (localName, aliasName);
                }
            }
        }

        private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> BuildObjectMemberAliasMap(
            string scriptName,
            IReadOnlyDictionary<string, string> localAliasMap)
        {
            if (string.IsNullOrWhiteSpace(scriptName))
            {
                return new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            }

            var objectMemberAliasMap =
                new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in ObjectLiteralAssignmentPattern.Matches(scriptName))
            {
                string objectName = NormalizeFunctionAliasArgument(match.Groups["lhs"]?.Value);
                string objectBody = match.Groups["body"]?.Value;
                if (!IsPotentialFunctionAliasName(objectName) || string.IsNullOrWhiteSpace(objectBody))
                {
                    continue;
                }

                var memberAliasMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string objectMember in SplitTopLevelByComma(objectBody))
                {
                    if (TryParseSpreadAliasMember(objectMember, out string spreadSourceName)
                        && objectMemberAliasMap.TryGetValue(spreadSourceName, out IReadOnlyDictionary<string, string> spreadMemberAliasMap))
                    {
                        CopyObjectMemberAliases(memberAliasMap, spreadMemberAliasMap);
                        continue;
                    }

                    if (!TryParseObjectLiteralMember(
                            objectMember,
                            localAliasMap,
                            out string memberKey,
                            out string memberValue))
                    {
                        continue;
                    }

                    string resolvedAlias = ResolveAssignmentAliasCandidate(
                        memberValue,
                        localAliasMap,
                        objectMemberAliasMap);
                    if (IsPotentialFunctionAliasName(resolvedAlias))
                    {
                        memberAliasMap[memberKey] = resolvedAlias;
                    }
                }

                if (memberAliasMap.Count > 0)
                {
                    objectMemberAliasMap[objectName] = memberAliasMap;
                }
            }

            foreach (Match match in ArrayLiteralAssignmentPattern.Matches(scriptName))
            {
                string arrayName = NormalizeFunctionAliasArgument(match.Groups["lhs"]?.Value);
                string arrayBody = match.Groups["body"]?.Value;
                if (!IsPotentialFunctionAliasName(arrayName) || string.IsNullOrWhiteSpace(arrayBody))
                {
                    continue;
                }

                var memberAliasMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                IReadOnlyList<string> elements = SplitTopLevelByComma(arrayBody);
                for (int i = 0; i < elements.Count; i++)
                {
                    if (TryParseSpreadAliasMember(elements[i], out string spreadSourceName)
                        && objectMemberAliasMap.TryGetValue(spreadSourceName, out IReadOnlyDictionary<string, string> spreadMemberAliasMap))
                    {
                        CopyArrayMemberAliases(memberAliasMap, spreadMemberAliasMap);
                        continue;
                    }

                    string resolvedAlias = ResolveAssignmentAliasCandidate(
                        elements[i],
                        localAliasMap,
                        objectMemberAliasMap);
                    if (IsPotentialFunctionAliasName(resolvedAlias))
                    {
                        string memberKey = GetNextArrayMemberIndex(memberAliasMap).ToString(System.Globalization.CultureInfo.InvariantCulture);
                        memberAliasMap[memberKey] = resolvedAlias;
                    }
                }

                if (memberAliasMap.Count > 0)
                {
                    objectMemberAliasMap[arrayName] = memberAliasMap;
                }
            }

            foreach (Match match in ObjectMemberAliasAssignmentPattern.Matches(scriptName))
            {
                string objectName = NormalizeFunctionAliasArgument(match.Groups["object"]?.Value).TrimEnd(';');
                string rightValue = match.Groups["rhs"]?.Value;
                if (!IsPotentialFunctionAliasName(objectName) || string.IsNullOrWhiteSpace(rightValue))
                {
                    continue;
                }

                string memberKey = NormalizeFunctionAliasArgument(match.Groups["member"]?.Value).TrimEnd(';');
                if (string.IsNullOrWhiteSpace(memberKey))
                {
                    string indexExpression = match.Groups["expr"]?.Value;
                    if (!TryResolveBracketIndexKey(indexExpression, localAliasMap, out memberKey))
                    {
                        continue;
                    }

                    memberKey = NormalizeFunctionAliasArgument(memberKey).TrimEnd(';');
                }

                if (string.IsNullOrWhiteSpace(memberKey))
                {
                    continue;
                }

                string resolvedAlias = ResolveAssignmentAliasCandidate(
                    rightValue,
                    localAliasMap,
                    objectMemberAliasMap);
                if (!IsPotentialFunctionAliasName(resolvedAlias))
                {
                    continue;
                }

                if (!objectMemberAliasMap.TryGetValue(objectName, out IReadOnlyDictionary<string, string> existingMemberAliasMap)
                    || existingMemberAliasMap is not Dictionary<string, string> memberAliasMap)
                {
                    memberAliasMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    if (existingMemberAliasMap != null)
                    {
                        foreach (KeyValuePair<string, string> existingMemberAlias in existingMemberAliasMap)
                        {
                            memberAliasMap[existingMemberAlias.Key] = existingMemberAlias.Value;
                        }
                    }

                    objectMemberAliasMap[objectName] = memberAliasMap;
                }

                memberAliasMap[memberKey] = resolvedAlias;
            }

            foreach ((string ObjectName, string MethodName, IReadOnlyList<string> Arguments) mutation in EnumerateArrayMutatorAliasCalls(scriptName))
            {
                if (!IsPotentialFunctionAliasName(mutation.ObjectName) || mutation.Arguments.Count == 0)
                {
                    continue;
                }

                Dictionary<string, string> memberAliasMap = GetOrCreateMemberAliasMap(
                    objectMemberAliasMap,
                    mutation.ObjectName);

                if (mutation.MethodName.Equals("splice", StringComparison.OrdinalIgnoreCase))
                {
                    ApplyArraySpliceAliasMutation(
                        memberAliasMap,
                        mutation.Arguments,
                        localAliasMap,
                        objectMemberAliasMap);
                    continue;
                }

                IReadOnlyList<string> aliasArguments = FlattenArrayAliasArguments(
                    mutation.Arguments,
                    objectMemberAliasMap);
                int insertionIndex = mutation.MethodName.Equals("unshift", StringComparison.OrdinalIgnoreCase)
                    ? 0
                    : GetNextArrayMemberIndex(memberAliasMap);
                if (mutation.MethodName.Equals("unshift", StringComparison.OrdinalIgnoreCase))
                {
                    ShiftArrayMemberIndexes(memberAliasMap, aliasArguments.Count);
                }

                for (int argumentIndex = 0; argumentIndex < aliasArguments.Count; argumentIndex++)
                {
                    string resolvedAlias = ResolveAssignmentAliasCandidate(
                        aliasArguments[argumentIndex],
                        localAliasMap,
                        objectMemberAliasMap);
                    if (!IsPotentialFunctionAliasName(resolvedAlias))
                    {
                        continue;
                    }

                    string memberKey = mutation.MethodName.Equals("unshift", StringComparison.OrdinalIgnoreCase)
                        ? (insertionIndex + argumentIndex).ToString(System.Globalization.CultureInfo.InvariantCulture)
                        : GetNextArrayMemberIndex(memberAliasMap).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    memberAliasMap[memberKey] = resolvedAlias;
                }
            }

            foreach ((string TargetName, IReadOnlyList<string> Arguments) assignCall in EnumerateObjectAssignAliasCalls(scriptName))
            {
                if (!IsPotentialFunctionAliasName(assignCall.TargetName) || assignCall.Arguments.Count == 0)
                {
                    continue;
                }

                Dictionary<string, string> memberAliasMap = GetOrCreateMemberAliasMap(
                    objectMemberAliasMap,
                    assignCall.TargetName);
                for (int argumentIndex = 1; argumentIndex < assignCall.Arguments.Count; argumentIndex++)
                {
                    ApplyObjectLiteralAliasMembers(
                        memberAliasMap,
                        assignCall.Arguments[argumentIndex],
                        localAliasMap,
                        objectMemberAliasMap);
                }
            }

            foreach ((string TargetName, IReadOnlyList<string> Arguments) assignCall in EnumerateObjectAssignAliasAssignments(scriptName))
            {
                if (!IsPotentialFunctionAliasName(assignCall.TargetName) || assignCall.Arguments.Count == 0)
                {
                    continue;
                }

                Dictionary<string, string> memberAliasMap = GetOrCreateMemberAliasMap(
                    objectMemberAliasMap,
                    assignCall.TargetName);
                for (int argumentIndex = 0; argumentIndex < assignCall.Arguments.Count; argumentIndex++)
                {
                    string sourceName = NormalizeFunctionAliasArgument(assignCall.Arguments[argumentIndex]).TrimEnd(';');
                    if (objectMemberAliasMap.TryGetValue(sourceName, out IReadOnlyDictionary<string, string> sourceMemberAliasMap))
                    {
                        CopyObjectMemberAliases(memberAliasMap, sourceMemberAliasMap);
                        continue;
                    }

                    ApplyObjectLiteralAliasMembers(
                        memberAliasMap,
                        assignCall.Arguments[argumentIndex],
                        localAliasMap,
                        objectMemberAliasMap);
                }
            }

            foreach ((string TargetName, string SourceName, IReadOnlyList<string> Arguments) concatAssignment in EnumerateArrayConcatAliasAssignments(scriptName))
            {
                if (!IsPotentialFunctionAliasName(concatAssignment.TargetName)
                    || !IsPotentialFunctionAliasName(concatAssignment.SourceName))
                {
                    continue;
                }

                Dictionary<string, string> targetMemberAliasMap = GetOrCreateMemberAliasMap(
                    objectMemberAliasMap,
                    concatAssignment.TargetName);
                if (!concatAssignment.TargetName.Equals(concatAssignment.SourceName, StringComparison.OrdinalIgnoreCase)
                    && objectMemberAliasMap.TryGetValue(concatAssignment.SourceName, out IReadOnlyDictionary<string, string> sourceMemberAliasMap))
                {
                    CopyArrayMemberAliases(targetMemberAliasMap, sourceMemberAliasMap);
                }

                IReadOnlyList<string> aliasArguments = FlattenArrayAliasArguments(
                    concatAssignment.Arguments,
                    objectMemberAliasMap);
                for (int argumentIndex = 0; argumentIndex < aliasArguments.Count; argumentIndex++)
                {
                    string resolvedAlias = ResolveAssignmentAliasCandidate(
                        aliasArguments[argumentIndex],
                        localAliasMap,
                        objectMemberAliasMap);
                    if (!IsPotentialFunctionAliasName(resolvedAlias))
                    {
                        continue;
                    }

                    string memberKey = GetNextArrayMemberIndex(targetMemberAliasMap).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    targetMemberAliasMap[memberKey] = resolvedAlias;
                }
            }

            foreach ((string TargetName, string SourceName) copyAssignment in EnumerateArrayCopyAliasAssignments(scriptName))
            {
                if (!IsPotentialFunctionAliasName(copyAssignment.TargetName)
                    || !IsPotentialFunctionAliasName(copyAssignment.SourceName)
                    || !objectMemberAliasMap.TryGetValue(copyAssignment.SourceName, out IReadOnlyDictionary<string, string> sourceMemberAliasMap))
                {
                    continue;
                }

                Dictionary<string, string> targetMemberAliasMap = GetOrCreateMemberAliasMap(
                    objectMemberAliasMap,
                    copyAssignment.TargetName);
                CopyArrayMemberAliases(targetMemberAliasMap, sourceMemberAliasMap);
            }

            foreach ((string TargetName, string MethodName, IReadOnlyList<string> Arguments) arrayFactoryAssignment in EnumerateArrayFactoryAliasAssignments(scriptName))
            {
                if (!IsPotentialFunctionAliasName(arrayFactoryAssignment.TargetName)
                    || arrayFactoryAssignment.Arguments.Count == 0)
                {
                    continue;
                }

                Dictionary<string, string> targetMemberAliasMap = GetOrCreateMemberAliasMap(
                    objectMemberAliasMap,
                    arrayFactoryAssignment.TargetName);
                if (arrayFactoryAssignment.MethodName.Equals("from", StringComparison.OrdinalIgnoreCase))
                {
                    string sourceName = NormalizeFunctionAliasArgument(arrayFactoryAssignment.Arguments[0]).TrimEnd(';');
                    if (objectMemberAliasMap.TryGetValue(sourceName, out IReadOnlyDictionary<string, string> sourceMemberAliasMap))
                    {
                        CopyArrayMemberAliases(targetMemberAliasMap, sourceMemberAliasMap);
                        continue;
                    }
                }

                IReadOnlyList<string> aliasArguments = arrayFactoryAssignment.MethodName.Equals("from", StringComparison.OrdinalIgnoreCase)
                    ? FlattenArrayAliasArguments(new[] { arrayFactoryAssignment.Arguments[0] }, objectMemberAliasMap)
                    : arrayFactoryAssignment.Arguments;
                for (int argumentIndex = 0; argumentIndex < aliasArguments.Count; argumentIndex++)
                {
                    string resolvedAlias = ResolveAssignmentAliasCandidate(
                        aliasArguments[argumentIndex],
                        localAliasMap,
                        objectMemberAliasMap);
                    if (!IsPotentialFunctionAliasName(resolvedAlias))
                    {
                        continue;
                    }

                    string memberKey = GetNextArrayMemberIndex(targetMemberAliasMap).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    targetMemberAliasMap[memberKey] = resolvedAlias;
                }
            }

            return objectMemberAliasMap;
        }

        private static IReadOnlyList<string> ResolveAssignmentAliasCandidates(
            string value,
            IReadOnlyDictionary<string, string> localAliasMap,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap)
        {
            string normalizedValue = NormalizeOptionalChainingAliasAccess(
                NormalizeFunctionAliasArgument(value)).TrimEnd(';');
            if (string.IsNullOrWhiteSpace(normalizedValue))
            {
                return Array.Empty<string>();
            }

            var aliases = new List<string>();
            var seenAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void AddAlias(string alias)
            {
                string normalizedAlias = NormalizeFunctionAliasArgument(alias).TrimEnd(';');
                if (IsPotentialFunctionAliasName(normalizedAlias) && seenAliases.Add(normalizedAlias))
                {
                    aliases.Add(normalizedAlias);
                }
            }

            if (TryTrimCallbackInvokerTargetExpression(normalizedValue, out string callbackTargetExpression)
                && !string.Equals(callbackTargetExpression, normalizedValue, StringComparison.OrdinalIgnoreCase))
            {
                foreach (string callbackTargetAlias in ResolveAssignmentAliasCandidates(
                             callbackTargetExpression,
                             localAliasMap,
                             objectMemberAliasMap))
                {
                    AddAlias(callbackTargetAlias);
                }
            }

            foreach (string conditionalBranch in EnumerateConditionalExpressionBranches(normalizedValue))
            {
                foreach (string branchAlias in ResolveAssignmentAliasCandidates(
                             conditionalBranch,
                             localAliasMap,
                             objectMemberAliasMap))
                {
                    AddAlias(branchAlias);
                }
            }

            foreach (string logicalBranch in EnumerateLogicalExpressionBranches(normalizedValue))
            {
                foreach (string branchAlias in ResolveAssignmentAliasCandidates(
                             logicalBranch,
                             localAliasMap,
                             objectMemberAliasMap))
                {
                    AddAlias(branchAlias);
                }
            }

            foreach (string sequenceTail in EnumerateSequenceExpressionTailCandidates(normalizedValue))
            {
                foreach (string sequenceAlias in ResolveAssignmentAliasCandidates(
                             sequenceTail,
                             localAliasMap,
                             objectMemberAliasMap))
                {
                    AddAlias(sequenceAlias);
                }
            }

            if (IsFunctionExpressionText(normalizedValue))
            {
                foreach (string returnExpression in EnumerateFunctionReturnExpressions(normalizedValue))
                {
                    foreach (string returnAlias in ResolveAssignmentAliasCandidates(
                                 returnExpression,
                                 localAliasMap,
                                 objectMemberAliasMap))
                    {
                        AddAlias(returnAlias);
                    }
                }
            }

            string singleAlias = ResolveAssignmentAliasCandidate(
                normalizedValue,
                localAliasMap,
                objectMemberAliasMap);
            AddAlias(singleAlias);

            foreach (string memberAlias in EnumerateMemberAliasCandidates(normalizedValue))
            {
                AddAlias(memberAlias);
            }

            foreach (string functionAliasName in EnumerateFunctionAliasNames(normalizedValue))
            {
                AddAlias(functionAliasName);
            }

            IReadOnlyList<string> parsedScriptNames = QuestRuntimeManager.ParseScriptNames(normalizedValue);
            for (int i = 0; i < parsedScriptNames.Count; i++)
            {
                AddAlias(parsedScriptNames[i]);
            }

            return aliases.Count == 0 ? Array.Empty<string>() : aliases;
        }

        private static string ResolveAssignmentAliasCandidate(
            string value,
            IReadOnlyDictionary<string, string> localAliasMap,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap)
        {
            string normalizedValue = NormalizeOptionalChainingAliasAccess(
                NormalizeFunctionAliasArgument(value)).TrimEnd(';');
            if (string.IsNullOrWhiteSpace(normalizedValue))
            {
                return string.Empty;
            }

            if (TryTrimCallbackInvokerTargetExpression(normalizedValue, out string callbackTargetExpression)
                && !string.Equals(callbackTargetExpression, normalizedValue, StringComparison.OrdinalIgnoreCase))
            {
                string callbackTargetAlias = ResolveAssignmentAliasCandidate(
                    callbackTargetExpression,
                    localAliasMap,
                    objectMemberAliasMap);
                if (!string.IsNullOrWhiteSpace(callbackTargetAlias))
                {
                    return callbackTargetAlias;
                }
            }

            foreach (string conditionalBranch in EnumerateConditionalExpressionBranches(normalizedValue))
            {
                string branchAlias = ResolveAssignmentAliasCandidate(
                    conditionalBranch,
                    localAliasMap,
                    objectMemberAliasMap);
                if (!string.IsNullOrWhiteSpace(branchAlias))
                {
                    return branchAlias;
                }
            }

            foreach (string logicalBranch in EnumerateLogicalExpressionBranches(normalizedValue))
            {
                string branchAlias = ResolveAssignmentAliasCandidate(
                    logicalBranch,
                    localAliasMap,
                    objectMemberAliasMap);
                if (!string.IsNullOrWhiteSpace(branchAlias))
                {
                    return branchAlias;
                }
            }

            foreach (string sequenceTail in EnumerateSequenceExpressionTailCandidates(normalizedValue))
            {
                string sequenceAlias = ResolveAssignmentAliasCandidate(
                    sequenceTail,
                    localAliasMap,
                    objectMemberAliasMap);
                if (!string.IsNullOrWhiteSpace(sequenceAlias))
                {
                    return sequenceAlias;
                }
            }

            if (TryResolveNoArgumentFunctionCallAliasCandidate(
                    normalizedValue,
                    localAliasMap,
                    out string noArgumentCallAlias))
            {
                return noArgumentCallAlias;
            }

            if (TryResolveBracketVariableAliasCandidate(
                    normalizedValue,
                    localAliasMap,
                    objectMemberAliasMap,
                    out string variableAlias))
            {
                return variableAlias;
            }

            if (TryResolveMemberLeafAliasCandidate(
                    normalizedValue,
                    localAliasMap,
                    objectMemberAliasMap,
                    out string memberAlias))
            {
                return memberAlias;
            }

            if (IsFunctionExpressionText(normalizedValue))
            {
                string functionAlias = ResolveFunctionExpressionAliasCandidate(
                    normalizedValue,
                    localAliasMap,
                    objectMemberAliasMap);
                if (!string.IsNullOrWhiteSpace(functionAlias))
                {
                    return functionAlias;
                }
            }

            if (TryResolveBracketIndexKeyLiteral(normalizedValue, out string indexKey))
            {
                return indexKey;
            }

            if (!IsPotentialAliasArgument(normalizedValue, argumentIndex: 0))
            {
                return string.Empty;
            }

            IReadOnlyList<string> parsedScriptNames = QuestRuntimeManager.ParseScriptNames(normalizedValue);
            if (parsedScriptNames.Count == 0)
            {
                return IsPotentialFunctionAliasName(normalizedValue) ? normalizedValue : string.Empty;
            }

            for (int i = 0; i < parsedScriptNames.Count; i++)
            {
                string parsedName = NormalizeFunctionAliasArgument(parsedScriptNames[i]).TrimEnd(';');
                if (IsPotentialFunctionAliasName(parsedName))
                {
                    return parsedName;
                }
            }

            return string.Empty;
        }

        private static IEnumerable<(string ObjectName, string MethodName, IReadOnlyList<string> Arguments)> EnumerateArrayMutatorAliasCalls(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            foreach (Match match in ArrayMutatorAliasCallPattern.Matches(value))
            {
                string objectName = NormalizeFunctionAliasArgument(match.Groups["object"]?.Value);
                string methodName = NormalizeFunctionAliasArgument(match.Groups["method"]?.Value);
                int openIndex = value.IndexOf('(', match.Index);
                if (openIndex < 0)
                {
                    continue;
                }

                int closeIndex = FindMatchingCloseParenthesis(value, openIndex);
                if (closeIndex <= openIndex)
                {
                    continue;
                }

                string argumentText = value[(openIndex + 1)..closeIndex];
                yield return (objectName, methodName, new List<string>(SplitFunctionArguments(argumentText)));
            }
        }

        private static IEnumerable<(string TargetName, string SourceName)> EnumerateArrayCopyAliasAssignments(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            foreach (Match match in ArrayCopyAliasAssignmentPattern.Matches(value))
            {
                string targetName = NormalizeFunctionAliasArgument(match.Groups["lhs"]?.Value).TrimEnd(';');
                string sourceName = NormalizeFunctionAliasArgument(match.Groups["source"]?.Value).TrimEnd(';');
                if (!IsPotentialFunctionAliasName(targetName) || !IsPotentialFunctionAliasName(sourceName))
                {
                    continue;
                }

                int openIndex = value.IndexOf('(', match.Index);
                if (openIndex < 0)
                {
                    continue;
                }

                int closeIndex = FindMatchingCloseParenthesis(value, openIndex);
                if (closeIndex <= openIndex)
                {
                    continue;
                }

                var arguments = new List<string>(SplitFunctionArguments(value[(openIndex + 1)..closeIndex]));
                if (arguments.Count > 0
                    && (!TryResolveBracketIndexKeyLiteral(arguments[0], out string startKey)
                        || !string.Equals(startKey, "0", StringComparison.Ordinal)))
                {
                    continue;
                }

                if (arguments.Count > 1)
                {
                    continue;
                }

                yield return (targetName, sourceName);
            }
        }

        private static IEnumerable<(string TargetName, IReadOnlyList<string> Arguments)> EnumerateObjectAssignAliasCalls(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            foreach ((string FunctionName, IReadOnlyList<string> Arguments) call in EnumerateFunctionCalls(value, includeNested: false))
            {
                if (!string.Equals(call.FunctionName, "Object.assign", StringComparison.OrdinalIgnoreCase)
                    || call.Arguments.Count < 2)
                {
                    continue;
                }

                string targetName = NormalizeFunctionAliasArgument(call.Arguments[0]).TrimEnd(';');
                if (IsPotentialFunctionAliasName(targetName))
                {
                    yield return (targetName, call.Arguments);
                }
            }
        }

        private static IEnumerable<(string TargetName, string SourceName, IReadOnlyList<string> Arguments)> EnumerateArrayConcatAliasAssignments(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            foreach (Match match in ArrayConcatAliasAssignmentPattern.Matches(value))
            {
                string targetName = NormalizeFunctionAliasArgument(match.Groups["lhs"]?.Value).TrimEnd(';');
                string sourceName = NormalizeFunctionAliasArgument(match.Groups["source"]?.Value).TrimEnd(';');
                if (!IsPotentialFunctionAliasName(targetName) || !IsPotentialFunctionAliasName(sourceName))
                {
                    continue;
                }

                int openIndex = value.IndexOf('(', match.Index);
                if (openIndex < 0)
                {
                    continue;
                }

                int closeIndex = FindMatchingCloseParenthesis(value, openIndex);
                if (closeIndex <= openIndex)
                {
                    continue;
                }

                string argumentText = value[(openIndex + 1)..closeIndex];
                yield return (targetName, sourceName, new List<string>(SplitFunctionArguments(argumentText)));
            }
        }

        private static IEnumerable<(string TargetName, IReadOnlyList<string> Arguments)> EnumerateObjectAssignAliasAssignments(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            foreach (Match match in ObjectAssignAliasAssignmentPattern.Matches(value))
            {
                string targetName = NormalizeFunctionAliasArgument(match.Groups["lhs"]?.Value).TrimEnd(';');
                if (!IsPotentialFunctionAliasName(targetName))
                {
                    continue;
                }

                int openIndex = value.IndexOf('(', match.Index);
                if (openIndex < 0)
                {
                    continue;
                }

                int closeIndex = FindMatchingCloseParenthesis(value, openIndex);
                if (closeIndex <= openIndex)
                {
                    continue;
                }

                string argumentText = value[(openIndex + 1)..closeIndex];
                yield return (targetName, new List<string>(SplitFunctionArguments(argumentText)));
            }
        }

        private static IEnumerable<(string TargetName, string MethodName, IReadOnlyList<string> Arguments)> EnumerateArrayFactoryAliasAssignments(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            foreach (Match match in ArrayFactoryAliasAssignmentPattern.Matches(value))
            {
                string targetName = NormalizeFunctionAliasArgument(match.Groups["lhs"]?.Value).TrimEnd(';');
                string methodName = NormalizeFunctionAliasArgument(match.Groups["method"]?.Value);
                if (!IsPotentialFunctionAliasName(targetName) || string.IsNullOrWhiteSpace(methodName))
                {
                    continue;
                }

                int openIndex = value.IndexOf('(', match.Index);
                if (openIndex < 0)
                {
                    continue;
                }

                int closeIndex = FindMatchingCloseParenthesis(value, openIndex);
                if (closeIndex <= openIndex)
                {
                    continue;
                }

                string argumentText = value[(openIndex + 1)..closeIndex];
                yield return (targetName, methodName, new List<string>(SplitFunctionArguments(argumentText)));
            }
        }

        private static int GetNextArrayMemberIndex(IReadOnlyDictionary<string, string> memberAliasMap)
        {
            if (memberAliasMap == null || memberAliasMap.Count == 0)
            {
                return 0;
            }

            int nextIndex = 0;
            foreach (string key in memberAliasMap.Keys)
            {
                if (int.TryParse(key, out int parsedIndex) && parsedIndex >= nextIndex)
                {
                    nextIndex = parsedIndex + 1;
                }
            }

            return nextIndex;
        }

        private static Dictionary<string, string> GetOrCreateMemberAliasMap(
            IDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap,
            string objectName)
        {
            if (!objectMemberAliasMap.TryGetValue(objectName, out IReadOnlyDictionary<string, string> existingMemberAliasMap)
                || existingMemberAliasMap is not Dictionary<string, string> memberAliasMap)
            {
                memberAliasMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (existingMemberAliasMap != null)
                {
                    foreach (KeyValuePair<string, string> existingMemberAlias in existingMemberAliasMap)
                    {
                        memberAliasMap[existingMemberAlias.Key] = existingMemberAlias.Value;
                    }
                }

                objectMemberAliasMap[objectName] = memberAliasMap;
            }

            return memberAliasMap;
        }

        private static void ApplyObjectLiteralAliasMembers(
            IDictionary<string, string> memberAliasMap,
            string objectLiteral,
            IReadOnlyDictionary<string, string> localAliasMap,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap)
        {
            if (memberAliasMap == null || string.IsNullOrWhiteSpace(objectLiteral))
            {
                return;
            }

            string normalizedLiteral = StripOuterBalancedParentheses(objectLiteral.Trim());
            if (normalizedLiteral.Length < 2 || normalizedLiteral[0] != '{' || normalizedLiteral[^1] != '}')
            {
                return;
            }

            foreach (string objectMember in SplitTopLevelByComma(normalizedLiteral[1..^1]))
            {
                if (!TryParseObjectLiteralMember(
                        objectMember,
                        localAliasMap,
                        out string memberKey,
                        out string memberValue))
                {
                    continue;
                }

                string resolvedAlias = ResolveAssignmentAliasCandidate(
                    memberValue,
                    localAliasMap,
                    objectMemberAliasMap);
                if (IsPotentialFunctionAliasName(resolvedAlias))
                {
                    memberAliasMap[memberKey] = resolvedAlias;
                }
            }
        }

        private static IReadOnlyList<string> FlattenArrayAliasArguments(
            IReadOnlyList<string> arguments,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap)
        {
            if (arguments == null || arguments.Count == 0)
            {
                return Array.Empty<string>();
            }

            var flattenedArguments = new List<string>();
            for (int i = 0; i < arguments.Count; i++)
            {
                string argument = StripOuterBalancedParentheses(arguments[i]?.Trim());
                if (TryParseSpreadAliasMember(argument, out string spreadSourceName)
                    && objectMemberAliasMap != null
                    && objectMemberAliasMap.TryGetValue(spreadSourceName, out IReadOnlyDictionary<string, string> spreadMemberAliasMap))
                {
                    foreach ((_, string aliasName) in EnumerateArrayMemberAliasesInIndexOrder(spreadMemberAliasMap))
                    {
                        flattenedArguments.Add(aliasName);
                    }

                    continue;
                }

                if (argument.Length >= 2 && argument[0] == '[' && argument[^1] == ']')
                {
                    IReadOnlyList<string> arrayElements = SplitTopLevelByComma(argument[1..^1]);
                    for (int elementIndex = 0; elementIndex < arrayElements.Count; elementIndex++)
                    {
                        flattenedArguments.Add(arrayElements[elementIndex]);
                    }

                    continue;
                }

                flattenedArguments.Add(arguments[i]);
            }

            return flattenedArguments;
        }

        private static bool TryParseSpreadAliasMember(string value, out string sourceName)
        {
            sourceName = string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalizedValue = NormalizeFunctionAliasArgument(value).TrimEnd(';');
            if (!normalizedValue.StartsWith("...", StringComparison.Ordinal))
            {
                return false;
            }

            sourceName = NormalizeFunctionAliasArgument(normalizedValue[3..]).TrimEnd(';');
            return IsPotentialFunctionAliasName(sourceName);
        }

        private static void ApplyArraySpliceAliasMutation(
            IDictionary<string, string> memberAliasMap,
            IReadOnlyList<string> arguments,
            IReadOnlyDictionary<string, string> localAliasMap,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap)
        {
            if (memberAliasMap == null || arguments == null || arguments.Count < 3)
            {
                return;
            }

            if (!TryResolveBracketIndexKey(arguments[0], localAliasMap, out string indexKey)
                || !int.TryParse(indexKey, out int insertionIndex)
                || insertionIndex < 0)
            {
                return;
            }

            int deleteCount = 0;
            if (TryResolveBracketIndexKey(arguments[1], localAliasMap, out string deleteCountKey)
                && int.TryParse(deleteCountKey, out int parsedDeleteCount)
                && parsedDeleteCount > 0)
            {
                deleteCount = parsedDeleteCount;
            }

            var rawInsertionArguments = new List<string>();
            for (int i = 2; i < arguments.Count; i++)
            {
                rawInsertionArguments.Add(arguments[i]);
            }

            IReadOnlyList<string> insertionArguments = FlattenArrayAliasArguments(
                rawInsertionArguments,
                objectMemberAliasMap);
            if (deleteCount > 0)
            {
                RemoveArrayMemberIndexRange(memberAliasMap, insertionIndex, deleteCount);
            }

            ShiftArrayMemberIndexesFrom(memberAliasMap, insertionIndex, insertionArguments.Count);
            for (int argumentIndex = 0; argumentIndex < insertionArguments.Count; argumentIndex++)
            {
                string resolvedAlias = ResolveAssignmentAliasCandidate(
                    insertionArguments[argumentIndex],
                    localAliasMap,
                    objectMemberAliasMap);
                if (!IsPotentialFunctionAliasName(resolvedAlias))
                {
                    continue;
                }

                string memberKey = (insertionIndex + argumentIndex).ToString(System.Globalization.CultureInfo.InvariantCulture);
                memberAliasMap[memberKey] = resolvedAlias;
            }
        }

        private static void CopyArrayMemberAliases(
            Dictionary<string, string> targetMemberAliasMap,
            IReadOnlyDictionary<string, string> sourceMemberAliasMap)
        {
            if (targetMemberAliasMap == null || sourceMemberAliasMap == null)
            {
                return;
            }

            foreach ((_, string aliasName) in EnumerateArrayMemberAliasesInIndexOrder(sourceMemberAliasMap))
            {
                string memberKey = GetNextArrayMemberIndex(targetMemberAliasMap).ToString(System.Globalization.CultureInfo.InvariantCulture);
                targetMemberAliasMap[memberKey] = aliasName;
            }
        }

        private static IEnumerable<(int Index, string AliasName)> EnumerateArrayMemberAliasesInIndexOrder(
            IReadOnlyDictionary<string, string> sourceMemberAliasMap)
        {
            if (sourceMemberAliasMap == null)
            {
                yield break;
            }

            var sortedAliases = new List<(int Index, string AliasName)>();
            foreach (KeyValuePair<string, string> sourceMemberAlias in sourceMemberAliasMap)
            {
                if (int.TryParse(sourceMemberAlias.Key, out int parsedIndex) && parsedIndex >= 0)
                {
                    sortedAliases.Add((parsedIndex, sourceMemberAlias.Value));
                }
            }

            sortedAliases.Sort((left, right) => left.Index.CompareTo(right.Index));
            for (int i = 0; i < sortedAliases.Count; i++)
            {
                yield return sortedAliases[i];
            }
        }

        private static void CopyObjectMemberAliases(
            IDictionary<string, string> targetMemberAliasMap,
            IReadOnlyDictionary<string, string> sourceMemberAliasMap)
        {
            if (targetMemberAliasMap == null || sourceMemberAliasMap == null)
            {
                return;
            }

            foreach (KeyValuePair<string, string> sourceMemberAlias in sourceMemberAliasMap)
            {
                targetMemberAliasMap[sourceMemberAlias.Key] = sourceMemberAlias.Value;
            }
        }

        private static void ShiftArrayMemberIndexes(IDictionary<string, string> memberAliasMap, int offset)
        {
            if (memberAliasMap == null || memberAliasMap.Count == 0 || offset <= 0)
            {
                return;
            }

            var shiftedMembers = new List<(string OldKey, string NewKey, string AliasName)>();
            foreach (KeyValuePair<string, string> memberAlias in memberAliasMap)
            {
                if (!int.TryParse(memberAlias.Key, out int parsedIndex) || parsedIndex < 0)
                {
                    continue;
                }

                shiftedMembers.Add((
                    memberAlias.Key,
                    (parsedIndex + offset).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    memberAlias.Value));
            }

            shiftedMembers.Sort((left, right) => string.CompareOrdinal(right.OldKey, left.OldKey));
            for (int i = 0; i < shiftedMembers.Count; i++)
            {
                memberAliasMap.Remove(shiftedMembers[i].OldKey);
                memberAliasMap[shiftedMembers[i].NewKey] = shiftedMembers[i].AliasName;
            }
        }

        private static void ShiftArrayMemberIndexesFrom(IDictionary<string, string> memberAliasMap, int startIndex, int offset)
        {
            if (memberAliasMap == null || memberAliasMap.Count == 0 || offset <= 0 || startIndex < 0)
            {
                return;
            }

            var shiftedMembers = new List<(string OldKey, string NewKey, string AliasName)>();
            foreach (KeyValuePair<string, string> memberAlias in memberAliasMap)
            {
                if (!int.TryParse(memberAlias.Key, out int parsedIndex) || parsedIndex < startIndex)
                {
                    continue;
                }

                shiftedMembers.Add((
                    memberAlias.Key,
                    (parsedIndex + offset).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    memberAlias.Value));
            }

            shiftedMembers.Sort((left, right) => string.CompareOrdinal(right.OldKey, left.OldKey));
            for (int i = 0; i < shiftedMembers.Count; i++)
            {
                memberAliasMap.Remove(shiftedMembers[i].OldKey);
                memberAliasMap[shiftedMembers[i].NewKey] = shiftedMembers[i].AliasName;
            }
        }

        private static void RemoveArrayMemberIndexRange(IDictionary<string, string> memberAliasMap, int startIndex, int count)
        {
            if (memberAliasMap == null || memberAliasMap.Count == 0 || startIndex < 0 || count <= 0)
            {
                return;
            }

            var removedKeys = new List<string>();
            foreach (KeyValuePair<string, string> memberAlias in memberAliasMap)
            {
                if (int.TryParse(memberAlias.Key, out int parsedIndex)
                    && parsedIndex >= startIndex
                    && parsedIndex < startIndex + count)
                {
                    removedKeys.Add(memberAlias.Key);
                }
            }

            for (int i = 0; i < removedKeys.Count; i++)
            {
                memberAliasMap.Remove(removedKeys[i]);
            }
        }

        private static bool TryResolveBracketVariableAliasCandidate(
            string value,
            IReadOnlyDictionary<string, string> localAliasMap,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap,
            out string aliasName)
        {
            aliasName = string.Empty;
            if (TryResolveIndexedObjectAliasCandidate(
                    value,
                    localAliasMap,
                    objectMemberAliasMap,
                    out string indexedAlias))
            {
                aliasName = indexedAlias;
                return true;
            }

            if (string.IsNullOrWhiteSpace(value)
                || localAliasMap == null
                || localAliasMap.Count == 0)
            {
                return false;
            }

            foreach (Match match in BracketMemberAliasVariablePattern.Matches(value))
            {
                string variableName = NormalizeFunctionAliasArgument(match.Groups["name"]?.Value);
                if (string.IsNullOrWhiteSpace(variableName)
                    || !localAliasMap.TryGetValue(variableName, out string mappedAlias))
                {
                    continue;
                }

                string normalizedAlias = NormalizeFunctionAliasArgument(mappedAlias).TrimEnd(';');
                if (!IsPotentialFunctionAliasName(normalizedAlias))
                {
                    continue;
                }

                aliasName = normalizedAlias;
                return true;
            }

            return false;
        }

        private static bool IsFunctionExpressionText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value.StartsWith("function", StringComparison.OrdinalIgnoreCase)
                || value.Contains("=>", StringComparison.Ordinal);
        }

        private static string ResolveFunctionExpressionAliasCandidate(
            string value,
            IReadOnlyDictionary<string, string> localAliasMap,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            if (TryResolveFunctionBodyMemberInvocationAlias(
                    value,
                    localAliasMap,
                    objectMemberAliasMap,
                    out string memberAlias))
            {
                return memberAlias;
            }

            if (TryResolveExpressionBodiedArrowAlias(
                    value,
                    localAliasMap,
                    objectMemberAliasMap,
                    out string arrowAlias))
            {
                return arrowAlias;
            }

            foreach (string functionAliasName in EnumerateFunctionAliasNames(value))
            {
                if (IsPotentialFunctionAliasName(functionAliasName))
                {
                    return functionAliasName;
                }
            }

            foreach (Match match in FunctionBodyAliasInvocationPattern.Matches(value))
            {
                string aliasName = NormalizeFunctionAliasArgument(match.Groups["name"]?.Value);
                if (IsPotentialFunctionAliasName(aliasName))
                {
                    return aliasName;
                }
            }

            foreach (Match match in BracketMemberAliasInvocationPattern.Matches(value))
            {
                string aliasName = NormalizeFunctionAliasArgument(match.Groups["name"]?.Value);
                if (IsPotentialFunctionAliasName(aliasName))
                {
                    return aliasName;
                }
            }

            foreach (Match match in BracketMemberAliasInvokerInvocationPattern.Matches(value))
            {
                string aliasName = NormalizeFunctionAliasArgument(match.Groups["name"]?.Value);
                if (IsPotentialFunctionAliasName(aliasName))
                {
                    return aliasName;
                }
            }

            return string.Empty;
        }

        private static bool TryResolveFunctionBodyMemberInvocationAlias(
            string value,
            IReadOnlyDictionary<string, string> localAliasMap,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap,
            out string aliasName)
        {
            aliasName = string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            foreach (Match match in MemberAliasInvokerInvocationPattern.Matches(value))
            {
                string candidateAliasName = NormalizeFunctionAliasArgument(match.Groups["name"]?.Value);
                if (!IsPotentialFunctionAliasName(candidateAliasName))
                {
                    continue;
                }

                aliasName = candidateAliasName;
                return true;
            }

            foreach (Match match in MemberAliasInvocationPattern.Matches(value))
            {
                string candidateAliasName = NormalizeFunctionAliasArgument(match.Groups["name"]?.Value);
                if (!IsPotentialFunctionAliasName(candidateAliasName))
                {
                    continue;
                }

                aliasName = candidateAliasName;
                return true;
            }

            foreach (Match match in BracketMemberAliasInvokerInvocationPattern.Matches(value))
            {
                string candidateAliasName = NormalizeFunctionAliasArgument(match.Groups["name"]?.Value);
                if (!IsPotentialFunctionAliasName(candidateAliasName))
                {
                    continue;
                }

                aliasName = candidateAliasName;
                return true;
            }

            foreach (Match match in BracketMemberAliasInvocationPattern.Matches(value))
            {
                string candidateAliasName = NormalizeFunctionAliasArgument(match.Groups["name"]?.Value);
                if (!IsPotentialFunctionAliasName(candidateAliasName))
                {
                    continue;
                }

                aliasName = candidateAliasName;
                return true;
            }

            foreach (Match match in FunctionBodyReturnMemberAliasPattern.Matches(value))
            {
                string candidateAliasName = NormalizeFunctionAliasArgument(match.Groups["name"]?.Value);
                if (!IsPotentialFunctionAliasName(candidateAliasName))
                {
                    continue;
                }

                aliasName = candidateAliasName;
                return true;
            }

            foreach (Match match in FunctionBodyReturnBracketMemberAliasPattern.Matches(value))
            {
                string ownerName = NormalizeFunctionAliasArgument(match.Groups["owner"]?.Value).TrimEnd(';');
                string expression = match.Groups["expr"]?.Value;
                if (!TryResolveBracketIndexKey(expression, localAliasMap, out string candidateAliasName))
                {
                    continue;
                }

                if (TryResolveObjectMemberAlias(
                        ownerName,
                        candidateAliasName,
                        objectMemberAliasMap,
                        out string objectMemberAlias))
                {
                    aliasName = objectMemberAlias;
                    return true;
                }

                if (IsPotentialFunctionAliasName(candidateAliasName))
                {
                    aliasName = candidateAliasName;
                    return true;
                }
            }

            foreach (Match match in FunctionBodyReturnAliasVariablePattern.Matches(value))
            {
                string variableName = NormalizeFunctionAliasArgument(match.Groups["name"]?.Value).TrimEnd(';');
                if (string.IsNullOrWhiteSpace(variableName))
                {
                    continue;
                }

                if (localAliasMap != null
                    && localAliasMap.TryGetValue(variableName, out string mappedAlias))
                {
                    string normalizedMappedAlias = NormalizeFunctionAliasArgument(mappedAlias).TrimEnd(';');
                    if (IsPotentialFunctionAliasName(normalizedMappedAlias))
                    {
                        aliasName = normalizedMappedAlias;
                        return true;
                    }
                }

                if (IsPotentialFunctionAliasName(variableName))
                {
                    aliasName = variableName;
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveExpressionBodiedArrowAlias(
            string value,
            IReadOnlyDictionary<string, string> localAliasMap,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap,
            out string aliasName)
        {
            aliasName = string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            int arrowIndex = value.IndexOf("=>", StringComparison.Ordinal);
            if (arrowIndex < 0 || arrowIndex >= value.Length - 2)
            {
                return false;
            }

            string expression = value[(arrowIndex + 2)..].Trim();
            if (string.IsNullOrWhiteSpace(expression) || expression.StartsWith("{", StringComparison.Ordinal))
            {
                return false;
            }

            string resolvedAlias = ResolveAssignmentAliasCandidate(
                expression,
                localAliasMap,
                objectMemberAliasMap);
            if (!IsPotentialFunctionAliasName(resolvedAlias))
            {
                return false;
            }

            aliasName = resolvedAlias;
            return true;
        }

        private static IEnumerable<string> EnumerateCanonicalAliasCandidates(
            string candidate,
            IReadOnlyDictionary<string, string> localAliasMap,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap)
        {
            string normalizedCandidate = NormalizeOptionalChainingAliasAccess(
                NormalizeFunctionAliasArgument(candidate)).TrimEnd(';');
            if (string.IsNullOrWhiteSpace(normalizedCandidate))
            {
                yield break;
            }

            if (TryTrimCallbackInvokerTargetExpression(normalizedCandidate, out string callbackTargetExpression)
                && !string.Equals(callbackTargetExpression, normalizedCandidate, StringComparison.OrdinalIgnoreCase))
            {
                foreach (string callbackTargetCandidate in EnumerateCanonicalAliasCandidates(
                             callbackTargetExpression,
                             localAliasMap,
                             objectMemberAliasMap))
                {
                    yield return callbackTargetCandidate;
                }
            }

            foreach (string conditionalBranch in EnumerateConditionalExpressionBranches(normalizedCandidate))
            {
                foreach (string branchCandidate in EnumerateCanonicalAliasCandidates(
                             conditionalBranch,
                             localAliasMap,
                             objectMemberAliasMap))
                {
                    yield return branchCandidate;
                }
            }

            foreach (string logicalBranch in EnumerateLogicalExpressionBranches(normalizedCandidate))
            {
                foreach (string branchCandidate in EnumerateCanonicalAliasCandidates(
                             logicalBranch,
                             localAliasMap,
                             objectMemberAliasMap))
                {
                    yield return branchCandidate;
                }
            }

            foreach (string sequenceTail in EnumerateSequenceExpressionTailCandidates(normalizedCandidate))
            {
                foreach (string sequenceCandidate in EnumerateCanonicalAliasCandidates(
                             sequenceTail,
                             localAliasMap,
                             objectMemberAliasMap))
                {
                    yield return sequenceCandidate;
                }
            }

            if (IsFunctionExpressionText(normalizedCandidate))
            {
                foreach (string returnExpression in EnumerateFunctionReturnExpressions(normalizedCandidate))
                {
                    foreach (string returnCandidate in EnumerateCanonicalAliasCandidates(
                                 returnExpression,
                                 localAliasMap,
                                 objectMemberAliasMap))
                    {
                        yield return returnCandidate;
                    }
                }
            }

            if (TryResolveNoArgumentFunctionCallAliasCandidate(
                    normalizedCandidate,
                    localAliasMap,
                    out string noArgumentCallAlias))
            {
                yield return noArgumentCallAlias;
                yield break;
            }

            if (TryResolveIndexedObjectAliasCandidate(
                    normalizedCandidate,
                    localAliasMap,
                    objectMemberAliasMap,
                    out string objectAlias))
            {
                yield return objectAlias;
                yield break;
            }

            if (TryResolveDottedObjectAliasCandidate(
                    normalizedCandidate,
                    objectMemberAliasMap,
                    out string dottedObjectAlias))
            {
                yield return dottedObjectAlias;
                yield break;
            }

            yield return normalizedCandidate;

            if (localAliasMap == null || localAliasMap.Count == 0)
            {
                yield break;
            }

            string current = normalizedCandidate;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { normalizedCandidate };
            while (localAliasMap.TryGetValue(current, out string mappedAlias))
            {
                string normalizedMappedAlias = NormalizeFunctionAliasArgument(mappedAlias).TrimEnd(';');
                if (string.IsNullOrWhiteSpace(normalizedMappedAlias)
                    || !IsPotentialFunctionAliasName(normalizedMappedAlias)
                    || !seen.Add(normalizedMappedAlias))
                {
                    break;
                }

                yield return normalizedMappedAlias;
                current = normalizedMappedAlias;
            }
        }

        private static IEnumerable<string> EnumerateScriptAliasCandidatesCore(string scriptName)
        {
            yield return scriptName;
            foreach (string memberAliasCandidate in EnumerateMemberAliasCandidates(scriptName))
            {
                yield return memberAliasCandidate;
            }

            string transportStem = TrimTransportSuffix(scriptName);
            if (!string.Equals(transportStem, scriptName, StringComparison.OrdinalIgnoreCase))
            {
                yield return transportStem;
                foreach (string memberAliasCandidate in EnumerateMemberAliasCandidates(transportStem))
                {
                    yield return memberAliasCandidate;
                }
            }

            string fileName = TrimPathPrefix(transportStem);
            if (!string.Equals(fileName, scriptName, StringComparison.OrdinalIgnoreCase))
            {
                yield return fileName;
                foreach (string memberAliasCandidate in EnumerateMemberAliasCandidates(fileName))
                {
                    yield return memberAliasCandidate;
                }
            }

            string fileStem = TrimFileExtension(fileName);
            if (!string.Equals(fileStem, fileName, StringComparison.OrdinalIgnoreCase))
            {
                yield return fileStem;
                foreach (string memberAliasCandidate in EnumerateMemberAliasCandidates(fileStem))
                {
                    yield return memberAliasCandidate;
                }
            }
        }

        private static IEnumerable<string> EnumerateMemberAliasCandidates(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            string normalizedValue = NormalizeFunctionAliasArgument(value).TrimEnd(';');
            if (string.IsNullOrWhiteSpace(normalizedValue))
            {
                yield break;
            }

            int memberSeparatorIndex = normalizedValue.LastIndexOf('.');
            if (memberSeparatorIndex > 0 && memberSeparatorIndex < normalizedValue.Length - 1)
            {
                string memberLeafName = NormalizeFunctionAliasArgument(
                    normalizedValue[(memberSeparatorIndex + 1)..]).TrimEnd(';');
                if (IsPotentialFunctionAliasName(memberLeafName))
                {
                    yield return memberLeafName;
                }
            }

            foreach (Match match in MemberAliasReferencePattern.Matches(normalizedValue))
            {
                string memberReferenceName = NormalizeFunctionAliasArgument(match.Groups["name"]?.Value).TrimEnd(';');
                if (IsPotentialFunctionAliasName(memberReferenceName))
                {
                    yield return memberReferenceName;
                }
            }

            foreach (Match match in BracketMemberAliasPattern.Matches(normalizedValue))
            {
                string bracketMemberName = NormalizeFunctionAliasArgument(match.Groups["name"]?.Value).TrimEnd(';');
                if (IsPotentialFunctionAliasName(bracketMemberName))
                {
                    yield return bracketMemberName;
                }
            }

            foreach (string bracketExpressionAlias in EnumerateBracketExpressionAliasCandidates(normalizedValue))
            {
                yield return bracketExpressionAlias;
            }
        }

        private static IEnumerable<string> EnumerateBracketExpressionAliasCandidates(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            int cursor = 0;
            while (cursor < value.Length)
            {
                int openIndex = value.IndexOf('[', cursor);
                if (openIndex < 0 || openIndex >= value.Length - 1)
                {
                    yield break;
                }

                int closeIndex = FindMatchingCloseBracket(value, openIndex);
                if (closeIndex <= openIndex)
                {
                    cursor = openIndex + 1;
                    continue;
                }

                string expression = value[(openIndex + 1)..closeIndex];
                if (TryResolveStaticBracketExpressionAlias(expression, out string aliasName))
                {
                    yield return aliasName;
                }

                cursor = closeIndex + 1;
            }
        }

        private static bool TryResolveMemberLeafAliasCandidate(
            string value,
            IReadOnlyDictionary<string, string> localAliasMap,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap,
            out string aliasName)
        {
            aliasName = string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (TryResolveIndexedObjectAliasCandidate(value, localAliasMap, objectMemberAliasMap, out string indexedAlias))
            {
                aliasName = indexedAlias;
                return true;
            }

            if (TryResolveDottedObjectAliasCandidate(value, objectMemberAliasMap, out string dottedAlias))
            {
                aliasName = dottedAlias;
                return true;
            }

            foreach (string candidate in EnumerateMemberAliasCandidates(value))
            {
                string normalizedCandidate = NormalizeFunctionAliasArgument(candidate).TrimEnd(';');
                if (string.IsNullOrWhiteSpace(normalizedCandidate))
                {
                    continue;
                }

                if (IsPotentialFunctionAliasName(normalizedCandidate))
                {
                    aliasName = normalizedCandidate;
                    return true;
                }

                if (localAliasMap != null
                    && localAliasMap.TryGetValue(normalizedCandidate, out string mappedAlias))
                {
                    string normalizedMappedAlias = NormalizeFunctionAliasArgument(mappedAlias).TrimEnd(';');
                    if (IsPotentialFunctionAliasName(normalizedMappedAlias))
                    {
                        aliasName = normalizedMappedAlias;
                        return true;
                    }
                }
            }

            return false;
        }

        private static IEnumerable<string> EnumerateFunctionReturnExpressions(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            foreach (Match match in FunctionBodyReturnExpressionPattern.Matches(value))
            {
                string expression = match.Groups["expr"]?.Value;
                if (!string.IsNullOrWhiteSpace(expression))
                {
                    yield return expression.Trim();
                }
            }
        }

        private static IEnumerable<string> EnumerateConditionalExpressionBranches(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            string normalizedValue = StripOuterBalancedParentheses(value.Trim());
            int questionIndex = FindTopLevelCharacter(normalizedValue, '?');
            if (questionIndex <= 0 || questionIndex >= normalizedValue.Length - 1)
            {
                yield break;
            }

            int colonIndex = FindMatchingConditionalColon(normalizedValue, questionIndex);
            if (colonIndex <= questionIndex || colonIndex >= normalizedValue.Length - 1)
            {
                yield break;
            }

            string trueBranch = normalizedValue[(questionIndex + 1)..colonIndex].Trim();
            string falseBranch = normalizedValue[(colonIndex + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(trueBranch))
            {
                yield return trueBranch;
            }

            if (!string.IsNullOrWhiteSpace(falseBranch))
            {
                yield return falseBranch;
            }
        }

        private static IEnumerable<string> EnumerateLogicalExpressionBranches(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            IReadOnlyList<string> branches = SplitTopLevelByLogicalOperators(
                StripOuterBalancedParentheses(value.Trim()));
            if (branches.Count <= 1)
            {
                yield break;
            }

            for (int i = 0; i < branches.Count; i++)
            {
                string branch = branches[i]?.Trim();
                if (!string.IsNullOrWhiteSpace(branch))
                {
                    yield return branch;
                }
            }
        }

        private static IEnumerable<string> EnumerateSequenceExpressionTailCandidates(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            IReadOnlyList<string> expressions = SplitTopLevelByComma(
                StripOuterBalancedParentheses(value.Trim()));
            if (expressions.Count <= 1)
            {
                yield break;
            }

            string tailExpression = expressions[^1]?.Trim();
            if (!string.IsNullOrWhiteSpace(tailExpression))
            {
                yield return tailExpression;
            }
        }

        private static int FindMatchingConditionalColon(string value, int questionIndex)
        {
            if (string.IsNullOrWhiteSpace(value) || questionIndex < 0 || questionIndex >= value.Length)
            {
                return -1;
            }

            int nestedConditionalDepth = 0;
            int groupingDepth = 0;
            char quote = '\0';
            for (int i = questionIndex + 1; i < value.Length; i++)
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

                if (groupingDepth > 0)
                {
                    continue;
                }

                if (current == '?')
                {
                    nestedConditionalDepth++;
                    continue;
                }

                if (current != ':')
                {
                    continue;
                }

                if (nestedConditionalDepth == 0)
                {
                    return i;
                }

                nestedConditionalDepth--;
            }

            return -1;
        }

        private static IReadOnlyList<string> SplitTopLevelByLogicalOperators(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Array.Empty<string>();
            }

            var tokens = new List<string>();
            int tokenStart = 0;
            int groupingDepth = 0;
            char quote = '\0';
            for (int i = 0; i < value.Length - 1; i++)
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

                if (groupingDepth > 0)
                {
                    continue;
                }

                string operatorText = value.Substring(i, Math.Min(2, value.Length - i));
                if (operatorText != "||" && operatorText != "&&" && operatorText != "??")
                {
                    continue;
                }

                tokens.Add(value[tokenStart..i].Trim());
                i++;
                tokenStart = i + 1;
            }

            if (tokens.Count == 0)
            {
                return Array.Empty<string>();
            }

            tokens.Add(value[tokenStart..].Trim());
            return tokens;
        }

        private static int FindMatchingCloseBracket(string value, int openIndex)
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

                if (current == '[')
                {
                    depth++;
                    continue;
                }

                if (current != ']')
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

        private static bool TryResolveStaticBracketExpressionAlias(string expression, out string aliasName)
        {
            aliasName = string.Empty;
            if (string.IsNullOrWhiteSpace(expression))
            {
                return false;
            }

            string normalizedExpression = StripOuterBalancedParentheses(expression.Trim());
            if (TryReadQuotedLiteral(normalizedExpression, out string quotedAliasName)
                && IsPotentialFunctionAliasName(quotedAliasName))
            {
                aliasName = quotedAliasName;
                return true;
            }

            IReadOnlyList<string> tokens = SplitTopLevelByPlus(normalizedExpression);
            if (tokens.Count <= 1)
            {
                return false;
            }

            var builder = new StringBuilder();
            bool allNumericTokens = true;
            int numericSum = 0;
            for (int i = 0; i < tokens.Count; i++)
            {
                string token = StripOuterBalancedParentheses(tokens[i]).Trim();
                if (TryReadQuotedLiteral(token, out string quotedToken))
                {
                    allNumericTokens = false;
                    builder.Append(quotedToken);
                    continue;
                }

                if (int.TryParse(token, out int numericToken))
                {
                    numericSum += numericToken;
                    builder.Append(numericToken);
                    continue;
                }

                return false;
            }

            string resolvedAliasName = NormalizeFunctionAliasArgument(builder.ToString()).TrimEnd(';');
            if (!IsPotentialFunctionAliasName(resolvedAliasName))
            {
                return false;
            }

            aliasName = resolvedAliasName;
            return true;
        }

        private static bool TryResolveBracketIndexKeyLiteral(string expression, out string key)
        {
            key = string.Empty;
            if (string.IsNullOrWhiteSpace(expression))
            {
                return false;
            }

            string normalizedExpression = StripOuterBalancedParentheses(expression.Trim());
            if (TryReadQuotedLiteral(normalizedExpression, out string quotedKey)
                && !string.IsNullOrWhiteSpace(quotedKey))
            {
                key = NormalizeFunctionAliasArgument(quotedKey).TrimEnd(';');
                return !string.IsNullOrWhiteSpace(key);
            }

            IReadOnlyList<string> tokens = SplitTopLevelByPlus(normalizedExpression);
            if (tokens.Count == 0)
            {
                return false;
            }

            var builder = new StringBuilder();
            bool allNumericTokens = true;
            int numericSum = 0;
            for (int i = 0; i < tokens.Count; i++)
            {
                string token = StripOuterBalancedParentheses(tokens[i]).Trim();
                if (TryReadQuotedLiteral(token, out string quotedToken))
                {
                    allNumericTokens = false;
                    builder.Append(quotedToken);
                    continue;
                }

                if (int.TryParse(token, out int numericToken))
                {
                    numericSum += numericToken;
                    builder.Append(numericToken);
                    continue;
                }

                return false;
            }

            string resolvedKey = allNumericTokens
                ? numericSum.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : NormalizeFunctionAliasArgument(builder.ToString()).TrimEnd(';');
            if (string.IsNullOrWhiteSpace(resolvedKey))
            {
                return false;
            }

            key = resolvedKey;
            return true;
        }

        private static IReadOnlyList<string> SplitTopLevelByComma(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Array.Empty<string>();
            }

            var tokens = new List<string>();
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

                if (current != ',' || groupingDepth > 0)
                {
                    continue;
                }

                tokens.Add(value[tokenStart..i].Trim());
                tokenStart = i + 1;
            }

            tokens.Add(value[tokenStart..].Trim());
            return tokens;
        }

        private static bool TryParseObjectLiteralMember(
            string member,
            IReadOnlyDictionary<string, string> localAliasMap,
            out string key,
            out string value)
        {
            key = string.Empty;
            value = string.Empty;
            if (string.IsNullOrWhiteSpace(member))
            {
                return false;
            }

            int separatorIndex = FindTopLevelCharacter(member, ':');
            if (separatorIndex <= 0 || separatorIndex >= member.Length - 1)
            {
                string shorthandMember = StripOuterBalancedParentheses(member.Trim());
                if (!IsPotentialFunctionAliasName(shorthandMember))
                {
                    return false;
                }

                key = NormalizeFunctionAliasArgument(shorthandMember).TrimEnd(';');
                value = key;
                return !string.IsNullOrWhiteSpace(key);
            }

            string rawKey = member[..separatorIndex].Trim();
            string rawValue = member[(separatorIndex + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(rawKey) || string.IsNullOrWhiteSpace(rawValue))
            {
                return false;
            }

            if (rawKey.Length >= 2
                && rawKey[0] == '['
                && rawKey[^1] == ']'
                && TryResolveBracketIndexKey(rawKey[1..^1], localAliasMap, out string computedKey))
            {
                key = NormalizeFunctionAliasArgument(computedKey).TrimEnd(';');
            }
            else if (TryReadQuotedLiteral(rawKey, out string quotedKey))
            {
                key = NormalizeFunctionAliasArgument(quotedKey);
            }
            else
            {
                key = NormalizeFunctionAliasArgument(rawKey);
            }

            if (!IsPotentialFunctionAliasName(key))
            {
                return false;
            }

            value = rawValue;
            return true;
        }

        private static bool TryParseObjectDestructuringAliasMember(
            string member,
            out string memberKey,
            out string localName)
        {
            memberKey = string.Empty;
            localName = string.Empty;
            if (string.IsNullOrWhiteSpace(member))
            {
                return false;
            }

            string normalizedMember = StripOuterBalancedParentheses(member.Trim());
            if (TryParseSpreadAliasMember(normalizedMember, out _))
            {
                return false;
            }

            int separatorIndex = FindTopLevelCharacter(normalizedMember, ':');
            string rawMemberKey = separatorIndex > 0
                ? normalizedMember[..separatorIndex].Trim()
                : normalizedMember;
            string rawLocalName = separatorIndex > 0 && separatorIndex < normalizedMember.Length - 1
                ? normalizedMember[(separatorIndex + 1)..].Trim()
                : normalizedMember;

            if (TryReadQuotedLiteral(rawMemberKey, out string quotedMemberKey))
            {
                rawMemberKey = quotedMemberKey;
            }

            memberKey = NormalizeFunctionAliasArgument(rawMemberKey).TrimEnd(';');
            localName = NormalizeFunctionAliasArgument(rawLocalName).TrimEnd(';');
            return IsPotentialFunctionAliasName(memberKey)
                   && IsPotentialFunctionAliasName(localName);
        }

        private static int FindTopLevelCharacter(string value, char token)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return -1;
            }

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

                if (groupingDepth == 0 && current == token)
                {
                    return i;
                }
            }

            return -1;
        }

        private static IReadOnlyList<string> SplitTopLevelByPlus(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Array.Empty<string>();
            }

            var tokens = new List<string>();
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

                if (groupingDepth == 0 && current == '+')
                {
                    tokens.Add(value[tokenStart..i]);
                    tokenStart = i + 1;
                }
            }

            tokens.Add(value[tokenStart..]);
            return tokens;
        }

        private static string StripOuterBalancedParentheses(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string current = value.Trim();
            while (current.Length >= 2 && current[0] == '(' && current[^1] == ')')
            {
                int closeIndex = FindMatchingCloseParenthesis(current, 0);
                if (closeIndex != current.Length - 1)
                {
                    break;
                }

                current = current[1..^1].Trim();
            }

            return current;
        }

        private static bool TryReadQuotedLiteral(string value, out string literal)
        {
            literal = string.Empty;
            if (string.IsNullOrWhiteSpace(value) || value.Length < 2)
            {
                return false;
            }

            char quote = value[0];
            if ((quote != '"' && quote != '\'') || value[^1] != quote)
            {
                return false;
            }

            string innerValue = value[1..^1];
            if (innerValue.IndexOf(quote) >= 0)
            {
                return false;
            }

            literal = innerValue;
            return true;
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

            foreach (var (_, arguments) in EnumerateFunctionCalls(scriptName))
            {
                for (int argumentIndex = 0; argumentIndex < arguments.Count; argumentIndex++)
                {
                    string normalizedArgument = NormalizeFunctionAliasArgument(arguments[argumentIndex]);
                    if (!IsPotentialAliasArgument(normalizedArgument, argumentIndex))
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

        private static bool TryTrimCallbackInvokerTargetExpression(string value, out string targetExpression)
        {
            targetExpression = string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalizedValue = NormalizeFunctionAliasArgument(value).TrimEnd(';');
            int openIndex = normalizedValue.IndexOf('(');
            if (openIndex <= 0)
            {
                return false;
            }

            string callPrefix = normalizedValue[..openIndex].TrimEnd();
            if (callPrefix.EndsWith(".call", StringComparison.OrdinalIgnoreCase))
            {
                targetExpression = callPrefix[..^".call".Length].TrimEnd();
            }
            else if (callPrefix.EndsWith(".apply", StringComparison.OrdinalIgnoreCase))
            {
                targetExpression = callPrefix[..^".apply".Length].TrimEnd();
            }
            else if (callPrefix.EndsWith(".bind", StringComparison.OrdinalIgnoreCase))
            {
                targetExpression = callPrefix[..^".bind".Length].TrimEnd();
            }
            else
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(targetExpression);
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

        private static IEnumerable<(string FunctionName, IReadOnlyList<string> Arguments)> EnumerateFunctionCalls(
            string value,
            bool includeNested = true)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            int openIndex = value.IndexOf('(');
            while (openIndex >= 0)
            {
                int closeIndex = FindMatchingCloseParenthesis(value, openIndex);
                if (closeIndex < openIndex)
                {
                    openIndex = value.IndexOf('(', openIndex + 1);
                    continue;
                }

                string functionName = ReadFunctionName(value, openIndex);
                string argumentText = closeIndex == openIndex + 1
                    ? string.Empty
                    : value[(openIndex + 1)..closeIndex];
                var arguments = new List<string>(SplitFunctionArguments(argumentText));
                if (!string.IsNullOrWhiteSpace(functionName))
                {
                    yield return (functionName, arguments);
                }

                if (!includeNested)
                {
                    openIndex = value.IndexOf('(', openIndex + 1);
                    continue;
                }

                foreach ((string FunctionName, IReadOnlyList<string> Arguments) nestedCall in EnumerateFunctionCalls(argumentText))
                {
                    yield return nestedCall;
                }

                openIndex = value.IndexOf('(', openIndex + 1);
            }
        }

        private static IEnumerable<string> EnumerateFunctionAliasNames(string scriptName)
        {
            if (string.IsNullOrWhiteSpace(scriptName))
            {
                yield break;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach ((string functionName, _) in EnumerateFunctionCalls(scriptName))
            {
                string normalizedFunctionName = NormalizeFunctionAliasArgument(functionName);
                if (!IsPotentialFunctionAliasName(normalizedFunctionName))
                {
                    continue;
                }

                if (seen.Add(normalizedFunctionName))
                {
                    yield return normalizedFunctionName;
                }
            }

            foreach (Match match in BracketMemberAliasInvocationPattern.Matches(scriptName))
            {
                string bracketMemberName = NormalizeFunctionAliasArgument(match.Groups["name"]?.Value);
                if (!IsPotentialFunctionAliasName(bracketMemberName))
                {
                    continue;
                }

                if (seen.Add(bracketMemberName))
                {
                    yield return bracketMemberName;
                }
            }
        }

        private static void CollectTimerCallbackPublications(
            string value,
            int inheritedDelayMs,
            ICollection<ScriptAliasPublication> publications,
            ISet<(string ScriptName, int DelayMs)> seen,
            IReadOnlyDictionary<string, string> localAliasMap,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap,
            IReadOnlyDictionary<string, IReadOnlyList<string>> localAliasCandidateMap)
        {
            if (string.IsNullOrWhiteSpace(value) || publications == null || seen == null)
            {
                return;
            }

            foreach ((string FunctionName, IReadOnlyList<string> Arguments) call in EnumerateFunctionCalls(value, includeNested: false))
            {
                if (!IsScriptCallbackFunctionName(call.FunctionName)
                    || call.Arguments.Count == 0
                    || !TryResolveCallbackCallDelayMs(call.FunctionName, call.Arguments, out int callbackDelayMs))
                {
                    continue;
                }

                int dueDelayMs = inheritedDelayMs >= int.MaxValue - callbackDelayMs
                    ? int.MaxValue
                    : inheritedDelayMs + callbackDelayMs;
                int firstDelayCandidateIndex = IsDelayedCallbackFunctionName(call.FunctionName) && call.Arguments.Count > 1
                    ? 1
                    : call.Arguments.Count;
                for (int i = 0; i < call.Arguments.Count; i++)
                {
                    string argument = NormalizeFunctionAliasArgument(call.Arguments[i]);
                    if (string.IsNullOrWhiteSpace(argument)
                        || (i >= firstDelayCandidateIndex && TryParsePositiveDelayMs(argument, out _)))
                    {
                        continue;
                    }

                    if (TryResolveNoArgumentFunctionCallAliasCandidates(
                            argument,
                            localAliasCandidateMap,
                            out IReadOnlyList<string> noArgumentCallAliases))
                    {
                        for (int aliasIndex = 0; aliasIndex < noArgumentCallAliases.Count; aliasIndex++)
                        {
                            AddPublication(
                                noArgumentCallAliases[aliasIndex],
                                dueDelayMs,
                                publications,
                                seen,
                                localAliasMap,
                                objectMemberAliasMap);
                        }

                        continue;
                    }

                    foreach (string canonicalArgument in EnumerateCanonicalAliasCandidates(
                                 argument,
                                 localAliasMap,
                                 objectMemberAliasMap))
                    {
                        string untimedArgument = RemoveNestedTimerCallbackCalls(canonicalArgument);
                        if (!string.IsNullOrWhiteSpace(untimedArgument))
                        {
                            AddPublication(
                                untimedArgument,
                                dueDelayMs,
                                publications,
                                seen,
                                localAliasMap,
                                objectMemberAliasMap);
                        }

                        CollectTimerCallbackPublications(
                            canonicalArgument,
                            dueDelayMs,
                            publications,
                            seen,
                            localAliasMap,
                            objectMemberAliasMap,
                            localAliasCandidateMap);
                    }
                }
            }
        }

        private static bool TryResolveNoArgumentFunctionCallAliasCandidates(
            string value,
            IReadOnlyDictionary<string, IReadOnlyList<string>> localAliasCandidateMap,
            out IReadOnlyList<string> aliasNames)
        {
            aliasNames = Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(value)
                || localAliasCandidateMap == null
                || localAliasCandidateMap.Count == 0)
            {
                return false;
            }

            string normalizedValue = NormalizeFunctionAliasArgument(value).TrimEnd(';');
            if (string.IsNullOrWhiteSpace(normalizedValue) || !normalizedValue.EndsWith(")", StringComparison.Ordinal))
            {
                return false;
            }

            int openIndex = normalizedValue.IndexOf('(');
            if (openIndex <= 0 || FindMatchingCloseParenthesis(normalizedValue, openIndex) != normalizedValue.Length - 1)
            {
                return false;
            }

            string argumentText = normalizedValue[(openIndex + 1)..^1];
            if (!string.IsNullOrWhiteSpace(argumentText))
            {
                return false;
            }

            string functionName = ReadFunctionName(normalizedValue, openIndex);
            if (string.IsNullOrWhiteSpace(functionName)
                || !localAliasCandidateMap.TryGetValue(functionName, out IReadOnlyList<string> mappedAliases)
                || mappedAliases == null
                || mappedAliases.Count == 0)
            {
                return false;
            }

            aliasNames = mappedAliases;
            return true;
        }

        private static bool TryResolveCallbackCallDelayMs(
            string functionName,
            IReadOnlyList<string> arguments,
            out int delayMs)
        {
            delayMs = 0;
            if (arguments == null || arguments.Count == 0)
            {
                return false;
            }

            if (!IsDelayedCallbackFunctionName(functionName))
            {
                return IsZeroDelayCallbackFunctionName(functionName);
            }

            bool foundDelay = arguments.Count == 1;
            int firstDelayCandidateIndex = arguments.Count > 1 ? 1 : arguments.Count;
            for (int i = firstDelayCandidateIndex; i < arguments.Count; i++)
            {
                string argument = NormalizeFunctionAliasArgument(arguments[i]);
                if (!TryParsePositiveDelayMs(argument, out int parsedDelayMs))
                {
                    continue;
                }

                delayMs = Math.Max(delayMs, parsedDelayMs);
                foundDelay = true;
            }

            return foundDelay;
        }

        private static bool TryParsePositiveDelayMs(string value, out int parsedInt)
        {
            if (int.TryParse(value, out parsedInt) && parsedInt > 0)
            {
                return true;
            }

            if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsedDouble)
                && parsedDouble > 0)
            {
                parsedInt = checked((int)Math.Round(parsedDouble, MidpointRounding.AwayFromZero));
                return parsedInt > 0;
            }

            parsedInt = 0;
            return false;
        }

        private static void AddPublication(
            string scriptName,
            int delayMs,
            ICollection<ScriptAliasPublication> publications,
            ISet<(string ScriptName, int DelayMs)> seen,
            IReadOnlyDictionary<string, string> localAliasMap,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap)
        {
            string normalizedScriptName = scriptName?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedScriptName))
            {
                return;
            }

            int normalizedDelayMs = Math.Max(0, delayMs);
            var aliasCandidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string candidate in EnumerateScriptAliasCandidates(
                         normalizedScriptName,
                         localAliasMap ?? BuildLocalAliasMap(normalizedScriptName),
                         objectMemberAliasMap ?? BuildObjectMemberAliasMap(
                             normalizedScriptName,
                             localAliasMap ?? BuildLocalAliasMap(normalizedScriptName))))
            {
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                IReadOnlyList<string> parsedScriptNames = QuestRuntimeManager.ParseScriptNames(candidate);
                if (parsedScriptNames.Count > 1
                    || (parsedScriptNames.Count == 1
                        && !string.Equals(parsedScriptNames[0], candidate, StringComparison.OrdinalIgnoreCase)))
                {
                    for (int i = 0; i < parsedScriptNames.Count; i++)
                    {
                        string parsedName = parsedScriptNames[i]?.Trim();
                        if (!IsPotentialAliasArgument(parsedName, argumentIndex: 0))
                        {
                            continue;
                        }

                        foreach (string canonicalCandidate in EnumerateCanonicalAliasCandidates(
                                     parsedName,
                                     localAliasMap,
                                     objectMemberAliasMap))
                        {
                            if (IsPotentialAliasArgument(canonicalCandidate, argumentIndex: 0))
                            {
                                aliasCandidates.Add(canonicalCandidate);
                            }
                        }
                    }

                    continue;
                }

                string normalizedCandidate = candidate.Trim();
                if (!IsPotentialAliasArgument(normalizedCandidate, argumentIndex: 0))
                {
                    continue;
                }

                foreach (string canonicalCandidate in EnumerateCanonicalAliasCandidates(
                             normalizedCandidate,
                             localAliasMap,
                             objectMemberAliasMap))
                {
                    if (IsPotentialAliasArgument(canonicalCandidate, argumentIndex: 0))
                    {
                        aliasCandidates.Add(canonicalCandidate);
                    }
                }
            }

            if (aliasCandidates.Count == 0)
            {
                aliasCandidates.Add(normalizedScriptName);
            }

            foreach (string aliasCandidate in aliasCandidates)
            {
                var key = (aliasCandidate, normalizedDelayMs);
                if (!seen.Add(key))
                {
                    continue;
                }

                publications.Add(new ScriptAliasPublication(aliasCandidate, normalizedDelayMs));
            }
        }

        private static string RemoveNestedTimerCallbackCalls(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value);
            foreach ((int StartIndex, int EndIndex) range in EnumerateTimerCallbackCallRanges(value))
            {
                for (int i = range.StartIndex; i <= range.EndIndex && i < builder.Length; i++)
                {
                    builder[i] = ' ';
                }
            }

            return builder.ToString().Trim();
        }

        private static IEnumerable<(int StartIndex, int EndIndex)> EnumerateTimerCallbackCallRanges(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            int openIndex = value.IndexOf('(');
            while (openIndex >= 0)
            {
                int closeIndex = FindMatchingCloseParenthesis(value, openIndex);
                if (closeIndex <= openIndex)
                {
                    openIndex = value.IndexOf('(', openIndex + 1);
                    continue;
                }

                int functionStartIndex = FindFunctionNameStart(value, openIndex);
                string functionName = functionStartIndex >= 0
                    ? value[functionStartIndex..openIndex].Trim()
                    : string.Empty;
                if (IsScriptCallbackFunctionName(functionName))
                {
                    yield return (functionStartIndex, closeIndex);
                }

                openIndex = value.IndexOf('(', openIndex + 1);
            }
        }

        private static int FindFunctionNameStart(string value, int openIndex)
        {
            if (string.IsNullOrWhiteSpace(value) || openIndex <= 0)
            {
                return -1;
            }

            int endIndex = openIndex - 1;
            while (endIndex >= 0 && char.IsWhiteSpace(value[endIndex]))
            {
                endIndex--;
            }

            int startIndex = endIndex;
            while (startIndex >= 0)
            {
                char current = value[startIndex];
                if (!char.IsLetterOrDigit(current) && current is not ('_' or '.'))
                {
                    break;
                }

                startIndex--;
            }

            return startIndex < endIndex ? startIndex + 1 : -1;
        }

        private static string ReadFunctionName(string value, int openIndex)
        {
            if (string.IsNullOrWhiteSpace(value) || openIndex <= 0)
            {
                return string.Empty;
            }

            int endIndex = openIndex - 1;
            while (endIndex >= 0 && char.IsWhiteSpace(value[endIndex]))
            {
                endIndex--;
            }

            int startIndex = endIndex;
            while (startIndex >= 0)
            {
                char current = value[startIndex];
                if (!char.IsLetterOrDigit(current) && current is not ('_' or '.'))
                {
                    break;
                }

                startIndex--;
            }

            return startIndex < endIndex
                ? value[(startIndex + 1)..(endIndex + 1)].Trim()
                : string.Empty;
        }

        private static bool IsScriptCallbackFunctionName(string functionName)
        {
            return IsDelayedCallbackFunctionName(functionName)
                || IsZeroDelayCallbackFunctionName(functionName);
        }

        private static bool IsPotentialFunctionAliasName(string functionName)
        {
            if (string.IsNullOrWhiteSpace(functionName))
            {
                return false;
            }

            string leafName = functionName.Trim();
            int memberSeparatorIndex = leafName.LastIndexOf('.');
            if (memberSeparatorIndex >= 0 && memberSeparatorIndex < leafName.Length - 1)
            {
                leafName = leafName[(memberSeparatorIndex + 1)..];
            }

            if (string.IsNullOrWhiteSpace(leafName)
                || IsScriptCallbackFunctionName(leafName))
            {
                return false;
            }

            switch (leafName.ToLowerInvariant())
            {
                case "function":
                case "if":
                case "for":
                case "while":
                case "switch":
                case "case":
                case "return":
                case "catch":
                case "new":
                case "call":
                case "apply":
                case "bind":
                    return false;
                default:
                    return true;
            }
        }

        private static bool TryResolveIndexedObjectAliasCandidate(
            string value,
            IReadOnlyDictionary<string, string> localAliasMap,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap,
            out string aliasName)
        {
            aliasName = string.Empty;
            if (!TryParseIndexedObjectAccess(value, out string objectName, out string indexExpression))
            {
                return false;
            }

            if (objectMemberAliasMap == null
                || !objectMemberAliasMap.TryGetValue(objectName, out IReadOnlyDictionary<string, string> memberAliasMap)
                || memberAliasMap == null
                || memberAliasMap.Count == 0)
            {
                return false;
            }

            if (!TryResolveBracketIndexKey(indexExpression, localAliasMap, out string memberKey)
                || string.IsNullOrWhiteSpace(memberKey))
            {
                return false;
            }

            if (!memberAliasMap.TryGetValue(memberKey, out string resolvedAlias))
            {
                return false;
            }

            string normalizedAlias = NormalizeFunctionAliasArgument(resolvedAlias).TrimEnd(';');
            if (!IsPotentialFunctionAliasName(normalizedAlias))
            {
                return false;
            }

            aliasName = normalizedAlias;
            return true;
        }

        private static bool TryResolveNoArgumentFunctionCallAliasCandidate(
            string value,
            IReadOnlyDictionary<string, string> localAliasMap,
            out string aliasName)
        {
            aliasName = string.Empty;
            if (string.IsNullOrWhiteSpace(value)
                || localAliasMap == null
                || localAliasMap.Count == 0)
            {
                return false;
            }

            string normalizedValue = NormalizeFunctionAliasArgument(value).TrimEnd(';');
            if (string.IsNullOrWhiteSpace(normalizedValue) || !normalizedValue.EndsWith(")", StringComparison.Ordinal))
            {
                return false;
            }

            int openIndex = normalizedValue.IndexOf('(');
            if (openIndex <= 0 || FindMatchingCloseParenthesis(normalizedValue, openIndex) != normalizedValue.Length - 1)
            {
                return false;
            }

            string argumentText = normalizedValue[(openIndex + 1)..^1];
            if (!string.IsNullOrWhiteSpace(argumentText))
            {
                return false;
            }

            string functionName = ReadFunctionName(normalizedValue, openIndex);
            if (string.IsNullOrWhiteSpace(functionName)
                || !localAliasMap.TryGetValue(functionName, out string mappedAlias))
            {
                return false;
            }

            string normalizedAlias = NormalizeFunctionAliasArgument(mappedAlias).TrimEnd(';');
            if (!IsPotentialFunctionAliasName(normalizedAlias))
            {
                return false;
            }

            aliasName = normalizedAlias;
            return true;
        }

        private static bool TryResolveDottedObjectAliasCandidate(
            string value,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap,
            out string aliasName)
        {
            aliasName = string.Empty;
            if (!TryParseDottedObjectAccess(value, out string objectName, out string memberKey))
            {
                return false;
            }

            return TryResolveObjectMemberAlias(objectName, memberKey, objectMemberAliasMap, out aliasName);
        }

        private static bool TryResolveObjectMemberAlias(
            string objectName,
            string memberKey,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap,
            out string aliasName)
        {
            aliasName = string.Empty;
            string normalizedObjectName = NormalizeFunctionAliasArgument(objectName).TrimEnd(';');
            string normalizedMemberKey = NormalizeFunctionAliasArgument(memberKey).TrimEnd(';');
            if (string.IsNullOrWhiteSpace(normalizedObjectName)
                || string.IsNullOrWhiteSpace(normalizedMemberKey)
                || objectMemberAliasMap == null
                || !objectMemberAliasMap.TryGetValue(normalizedObjectName, out IReadOnlyDictionary<string, string> memberAliasMap)
                || memberAliasMap == null
                || !memberAliasMap.TryGetValue(normalizedMemberKey, out string mappedAlias))
            {
                return false;
            }

            string normalizedAlias = NormalizeFunctionAliasArgument(mappedAlias).TrimEnd(';');
            if (!IsPotentialFunctionAliasName(normalizedAlias))
            {
                return false;
            }

            aliasName = normalizedAlias;
            return true;
        }

        private static bool TryResolveBracketIndexKey(
            string indexExpression,
            IReadOnlyDictionary<string, string> localAliasMap,
            out string key)
        {
            key = string.Empty;
            if (string.IsNullOrWhiteSpace(indexExpression))
            {
                return false;
            }

            string normalizedIndexExpression = StripOuterBalancedParentheses(indexExpression.Trim());
            if (TryResolveBracketIndexKeyLiteral(normalizedIndexExpression, out string staticIndexKey))
            {
                key = staticIndexKey;
                return true;
            }

            if (TryResolveStaticBracketExpressionAlias(normalizedIndexExpression, out string staticKey))
            {
                key = staticKey;
                return true;
            }

            string indexVariable = NormalizeFunctionAliasArgument(normalizedIndexExpression).TrimEnd(';');
            if (string.IsNullOrWhiteSpace(indexVariable))
            {
                return false;
            }

            if (localAliasMap != null
                && localAliasMap.TryGetValue(indexVariable, out string mappedAlias))
            {
                string normalizedMappedAlias = NormalizeFunctionAliasArgument(mappedAlias).TrimEnd(';');
                if (!string.IsNullOrWhiteSpace(normalizedMappedAlias))
                {
                    key = normalizedMappedAlias;
                    return true;
                }
            }

            key = indexVariable;
            return true;
        }

        private static bool TryParseIndexedObjectAccess(
            string value,
            out string objectName,
            out string indexExpression)
        {
            objectName = string.Empty;
            indexExpression = string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalizedValue = NormalizeFunctionAliasArgument(value).TrimEnd(';');
            if (string.IsNullOrWhiteSpace(normalizedValue))
            {
                return false;
            }

            int openIndex = normalizedValue.IndexOf('[');
            if (openIndex <= 0)
            {
                return false;
            }

            int closeIndex = FindMatchingCloseBracket(normalizedValue, openIndex);
            if (closeIndex <= openIndex)
            {
                return false;
            }

            string suffix = normalizedValue[(closeIndex + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(suffix)
                && !suffix.StartsWith(".", StringComparison.Ordinal)
                && !suffix.StartsWith("(", StringComparison.Ordinal))
            {
                return false;
            }

            objectName = NormalizeFunctionAliasArgument(normalizedValue[..openIndex]).TrimEnd(';');
            indexExpression = normalizedValue[(openIndex + 1)..closeIndex];
            return IsPotentialFunctionAliasName(objectName)
                   && !string.IsNullOrWhiteSpace(indexExpression);
        }

        private static bool TryParseDottedObjectAccess(
            string value,
            out string objectName,
            out string memberKey)
        {
            objectName = string.Empty;
            memberKey = string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalizedValue = NormalizeFunctionAliasArgument(value).TrimEnd(';');
            int separatorIndex = normalizedValue.IndexOf('.');
            if (separatorIndex <= 0 || separatorIndex >= normalizedValue.Length - 1)
            {
                return false;
            }

            string suffix = normalizedValue[(separatorIndex + 1)..].Trim();
            if (suffix.IndexOf('.') >= 0 || suffix.IndexOfAny(new[] { '(', '[', ']', ' ', '\t' }) >= 0)
            {
                return false;
            }

            objectName = NormalizeFunctionAliasArgument(normalizedValue[..separatorIndex]).TrimEnd(';');
            memberKey = NormalizeFunctionAliasArgument(suffix).TrimEnd(';');
            return IsPotentialFunctionAliasName(objectName)
                   && IsPotentialFunctionAliasName(memberKey);
        }

        private static bool IsDelayedCallbackFunctionName(string functionName)
        {
            if (string.IsNullOrWhiteSpace(functionName))
            {
                return false;
            }

            string leafName = functionName.Trim();
            int memberSeparatorIndex = leafName.LastIndexOf('.');
            if (memberSeparatorIndex >= 0 && memberSeparatorIndex < leafName.Length - 1)
            {
                leafName = leafName[(memberSeparatorIndex + 1)..];
            }

            return leafName.Equals("setTimeout", StringComparison.OrdinalIgnoreCase)
                || leafName.Equals("setInterval", StringComparison.OrdinalIgnoreCase)
                || leafName.Equals("setTimer", StringComparison.OrdinalIgnoreCase)
                || leafName.Equals("setDelay", StringComparison.OrdinalIgnoreCase)
                || leafName.Equals("schedule", StringComparison.OrdinalIgnoreCase)
                || leafName.StartsWith("schedule", StringComparison.OrdinalIgnoreCase)
                || (leafName.StartsWith("set", StringComparison.OrdinalIgnoreCase)
                    && (leafName.EndsWith("Timer", StringComparison.OrdinalIgnoreCase)
                        || leafName.EndsWith("Timeout", StringComparison.OrdinalIgnoreCase)
                        || leafName.EndsWith("Delay", StringComparison.OrdinalIgnoreCase)));
        }

        private static bool IsZeroDelayCallbackFunctionName(string functionName)
        {
            if (string.IsNullOrWhiteSpace(functionName))
            {
                return false;
            }

            string leafName = functionName.Trim();
            int memberSeparatorIndex = leafName.LastIndexOf('.');
            if (memberSeparatorIndex >= 0 && memberSeparatorIndex < leafName.Length - 1)
            {
                leafName = leafName[(memberSeparatorIndex + 1)..];
            }

            return leafName.Equals("setImmediate", StringComparison.OrdinalIgnoreCase)
                || leafName.Equals("requestAnimationFrame", StringComparison.OrdinalIgnoreCase)
                || leafName.Equals("queueMicrotask", StringComparison.OrdinalIgnoreCase)
                || leafName.Equals("then", StringComparison.OrdinalIgnoreCase)
                || leafName.Equals("catch", StringComparison.OrdinalIgnoreCase)
                || leafName.Equals("finally", StringComparison.OrdinalIgnoreCase)
                || leafName.Equals("done", StringComparison.OrdinalIgnoreCase)
                || leafName.Equals("fail", StringComparison.OrdinalIgnoreCase)
                || leafName.Equals("always", StringComparison.OrdinalIgnoreCase)
                || leafName.Equals("nextTick", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeFunctionAliasArgument(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Trim().Trim('"', '\'').Trim();
        }

        private static string NormalizeOptionalChainingAliasAccess(string value)
        {
            if (string.IsNullOrWhiteSpace(value)
                || value.IndexOf("?.", StringComparison.Ordinal) < 0)
            {
                return value;
            }

            return value
                .Replace("?.[", "[", StringComparison.Ordinal)
                .Replace("?.", ".", StringComparison.Ordinal);
        }

        private static bool IsPotentialAliasArgument(string value, int argumentIndex)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            bool hasAliasCharacter = false;
            bool hasDigit = false;
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (char.IsLetter(current) || current is '_' or '-' or '/' or '\\' or '.')
                {
                    hasAliasCharacter = true;
                    continue;
                }

                if (char.IsDigit(current))
                {
                    hasDigit = true;
                    continue;
                }

                if (current == '(' || current == ')' || current == '"' || current == '\'' || current == '[' || current == ']')
                {
                    continue;
                }

                return false;
            }

            switch (value.Trim().ToLowerInvariant())
            {
                case "function":
                case "true":
                case "false":
                case "null":
                case "undefined":
                case "this":
                    return false;
            }

            return hasAliasCharacter || (argumentIndex == 0 && hasDigit && value.Trim().Length > 1);
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
