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
            @"(?:^|[;\{\(\s,])(?<object>[A-Za-z_][A-Za-z0-9_]*)\s*\.\s*(?<method>push|unshift|splice|shift|pop)\s*\(",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ArrayNoArgumentOrderingAliasCallPattern = new(
            @"(?:^|[;\{\(\s,])(?<object>[A-Za-z_][A-Za-z0-9_]*)\s*\.\s*(?<method>reverse|sort)\s*\(",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ArrayOrderingAliasAssignmentPattern = new(
            @"(?:^|[;\{\(\s,])(?:(?:var|let|const)\s+)?(?<lhs>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<source>[A-Za-z_][A-Za-z0-9_]*)\s*\.\s*(?<method>reverse|toReversed|sort|toSorted)\s*\(",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ArrayCopyMutatorAliasAssignmentPattern = new(
            @"(?:^|[;\{\(\s,])(?:(?:var|let|const)\s+)?(?<lhs>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<source>[A-Za-z_][A-Za-z0-9_]*)\s*\.\s*(?<method>toSpliced|with)\s*\(",
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

        private static readonly Regex ObjectFromEntriesAliasAssignmentPattern = new(
            @"(?:^|[;\{\(\s,])(?:(?:var|let|const)\s+)?(?<lhs>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*Object\s*\.\s*fromEntries\s*\(",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ObjectStaticWrapperAliasAssignmentPattern = new(
            @"(?:^|[;\{\(\s,])(?:(?:var|let|const)\s+)?(?<lhs>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*Object\s*\.\s*(?<method>freeze|seal|preventExtensions)\s*\(",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ObjectKeyOrValuesAliasAssignmentPattern = new(
            @"(?:^|[;\{\(\s,])(?:(?:var|let|const)\s+)?(?<lhs>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*Object\s*\.\s*(?<method>keys|values)\s*\(",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ObjectEntriesMapAliasAssignmentPattern = new(
            @"(?:^|[;\{\(\s,])(?:(?:var|let|const)\s+)?(?<lhs>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*Object\s*\.\s*entries\s*\(",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ObjectStaticReduceAliasAssignmentPattern = new(
            @"(?:^|[;\{\(\s,])(?:(?:var|let|const)\s+)?(?<lhs>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*Object\s*\.\s*(?<method>keys|values|entries)\s*\(",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ArrayStaticTransformAliasAssignmentPattern = new(
            @"(?:^|[;\{\(\s,])(?:(?:var|let|const)\s+)?(?<lhs>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<source>[A-Za-z_][A-Za-z0-9_]*)\s*\.\s*(?<method>map|filter|flatMap|flat)\s*\(",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ArrayReduceAliasAssignmentPattern = new(
            @"(?:^|[;\{\(\s,])(?:(?:var|let|const)\s+)?(?<lhs>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<source>[A-Za-z_][A-Za-z0-9_]*)\s*\.\s*reduce\s*\(",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ArrayIteratorAliasCallPattern = new(
            @"(?:^|[;\{\(\s,])(?<source>[A-Za-z_][A-Za-z0-9_]*)\s*\.\s*(?:forEach|map|filter|flatMap|some|every|find|findIndex)\s*\(",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ArrayReduceIteratorAliasCallPattern = new(
            @"(?:^|[;\{\(\s,])(?<source>[A-Za-z_][A-Za-z0-9_]*)\s*\.\s*reduce\s*\(",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ForOfAliasLoopPattern = new(
            @"(?:^|[;\{\(\s])for\s*\(\s*(?:(?:var|let|const)\s+)?(?<item>[A-Za-z_][A-Za-z0-9_]*)\s+of\s+(?<source>[A-Za-z_][A-Za-z0-9_]*)\s*\)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ArrayFactoryAliasAssignmentPattern = new(
            @"(?:^|[;\{\(\s,])(?:(?:var|let|const)\s+)?(?<lhs>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*Array\s*\.\s*(?<method>of|from)\s*\(",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex CollectionConstructorAliasAssignmentPattern = new(
            @"(?:^|[;\{\(\s,])(?:(?:var|let|const)\s+)?(?<lhs>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?:new\s+)?(?<method>Set|Array|Map)\s*\(",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        internal readonly record struct PublishedTagMutation(
            IReadOnlyList<string> TagsToEnable,
            IReadOnlyList<string> TagsToDisable);

        internal readonly record struct ScriptAliasPublication(
            string ScriptName,
            int DelayMs);

        private readonly record struct ArrayAliasMutationOperation(
            int SourceIndex,
            string ObjectName,
            string MethodName,
            IReadOnlyList<string> Arguments);

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

            IReadOnlyDictionary<string, string> localAliasMap = BuildLocalAliasMap(scriptName);
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
                    foreach (int parsedDelayMs in EnumeratePositiveDelayMsCandidates(argument, localAliasMap))
                    {
                        delayMs = Math.Max(delayMs, parsedDelayMs);
                        foundDelay = true;
                    }
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

                    string arrayElement = StripOuterBalancedParentheses(elements[i]?.Trim());
                    if (arrayElement.StartsWith("...", StringComparison.Ordinal)
                        && TryParseCollectionFactoryCall(arrayElement[3..], out string spreadFactoryMethod, out IReadOnlyList<string> spreadFactoryArguments))
                    {
                        var spreadFactoryAliases = new List<string>();
                        AppendCollectionFactoryAliasArguments(
                            spreadFactoryAliases,
                            spreadFactoryMethod,
                            spreadFactoryArguments,
                            objectMemberAliasMap);
                        for (int spreadFactoryAliasIndex = 0; spreadFactoryAliasIndex < spreadFactoryAliases.Count; spreadFactoryAliasIndex++)
                        {
                            string resolvedSpreadFactoryAlias = ResolveAssignmentAliasCandidate(
                                spreadFactoryAliases[spreadFactoryAliasIndex],
                                localAliasMap,
                                objectMemberAliasMap);
                            if (!IsPotentialFunctionAliasName(resolvedSpreadFactoryAlias))
                            {
                                continue;
                            }

                            string spreadMemberKey = GetNextArrayMemberIndex(memberAliasMap).ToString(System.Globalization.CultureInfo.InvariantCulture);
                            memberAliasMap[spreadMemberKey] = resolvedSpreadFactoryAlias;
                        }

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

            foreach ((string TargetName, string WrappedValue) wrapperAssignment in EnumerateObjectStaticWrapperAliasAssignments(scriptName))
            {
                if (!IsPotentialFunctionAliasName(wrapperAssignment.TargetName)
                    || string.IsNullOrWhiteSpace(wrapperAssignment.WrappedValue))
                {
                    continue;
                }

                Dictionary<string, string> targetMemberAliasMap = GetOrCreateMemberAliasMap(
                    objectMemberAliasMap,
                    wrapperAssignment.TargetName);

                string wrappedValue = StripOuterBalancedParentheses(wrapperAssignment.WrappedValue.Trim());
                string sourceName = NormalizeFunctionAliasArgument(wrappedValue).TrimEnd(';');
                if (objectMemberAliasMap.TryGetValue(sourceName, out IReadOnlyDictionary<string, string> sourceMemberAliasMap))
                {
                    CopyObjectMemberAliases(targetMemberAliasMap, sourceMemberAliasMap);
                    continue;
                }

                if (wrappedValue.Length >= 2 && wrappedValue[0] == '{' && wrappedValue[^1] == '}')
                {
                    AddObjectLiteralAliasMembers(
                        targetMemberAliasMap,
                        wrappedValue[1..^1],
                        localAliasMap,
                        objectMemberAliasMap);
                    continue;
                }

                if (wrappedValue.Length >= 2 && wrappedValue[0] == '[' && wrappedValue[^1] == ']')
                {
                    AddArrayLiteralAliasMembers(
                        targetMemberAliasMap,
                        wrappedValue[1..^1],
                        localAliasMap,
                        objectMemberAliasMap);
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

            foreach (ArrayAliasMutationOperation mutation in EnumerateArrayAliasMutationOperations(scriptName))
            {
                if (!IsPotentialFunctionAliasName(mutation.ObjectName))
                {
                    continue;
                }

                if (mutation.MethodName.Equals("reverse", StringComparison.OrdinalIgnoreCase)
                    || mutation.MethodName.Equals("sort", StringComparison.OrdinalIgnoreCase))
                {
                    if (objectMemberAliasMap.TryGetValue(mutation.ObjectName, out IReadOnlyDictionary<string, string> existingMemberAliasMap)
                        && existingMemberAliasMap is Dictionary<string, string> existingArrayMemberAliasMap)
                    {
                        if (mutation.MethodName.Equals("sort", StringComparison.OrdinalIgnoreCase))
                        {
                            SortArrayMemberAliases(existingArrayMemberAliasMap);
                        }
                        else
                        {
                            ReverseArrayMemberAliases(existingArrayMemberAliasMap);
                        }
                    }

                    continue;
                }

                if (mutation.MethodName.Equals("shift", StringComparison.OrdinalIgnoreCase)
                    || mutation.MethodName.Equals("pop", StringComparison.OrdinalIgnoreCase))
                {
                    if (mutation.Arguments.Count == 0
                        && objectMemberAliasMap.TryGetValue(mutation.ObjectName, out IReadOnlyDictionary<string, string> existingMemberAliasMap)
                        && existingMemberAliasMap is Dictionary<string, string> existingArrayMemberAliasMap)
                    {
                        ApplyArrayRemovalAliasMutation(existingArrayMemberAliasMap, mutation.MethodName);
                    }

                    continue;
                }

                if (mutation.Arguments.Count == 0)
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

            foreach ((string TargetName, IReadOnlyList<string> Arguments) fromEntriesAssignment in EnumerateObjectFromEntriesAliasAssignments(scriptName))
            {
                if (!IsPotentialFunctionAliasName(fromEntriesAssignment.TargetName)
                    || fromEntriesAssignment.Arguments.Count != 1)
                {
                    continue;
                }

                Dictionary<string, string> memberAliasMap = GetOrCreateMemberAliasMap(
                    objectMemberAliasMap,
                    fromEntriesAssignment.TargetName);
                ApplyEntryCollectionAliasMembers(
                    memberAliasMap,
                    fromEntriesAssignment.Arguments[0],
                    localAliasMap,
                    objectMemberAliasMap);
            }

            foreach ((string TargetName, string SourceName) valuesAssignment in EnumerateObjectValuesAliasAssignments(scriptName))
            {
                if (!IsPotentialFunctionAliasName(valuesAssignment.TargetName)
                    || !IsPotentialFunctionAliasName(valuesAssignment.SourceName)
                    || !objectMemberAliasMap.TryGetValue(valuesAssignment.SourceName, out IReadOnlyDictionary<string, string> sourceMemberAliasMap))
                {
                    continue;
                }

                Dictionary<string, string> targetMemberAliasMap = GetOrCreateMemberAliasMap(
                    objectMemberAliasMap,
                    valuesAssignment.TargetName);
                CopyObjectValuesAsArrayMemberAliases(targetMemberAliasMap, sourceMemberAliasMap);
            }

            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectKeyAliasMap =
                BuildObjectKeyAliasMap(scriptName, localAliasMap, objectMemberAliasMap);
            foreach ((string TargetName, string SourceName, string MethodName, IReadOnlyList<string> Arguments) reduceAssignment in EnumerateObjectStaticReduceAliasAssignments(scriptName))
            {
                if (!IsPotentialFunctionAliasName(reduceAssignment.TargetName)
                    || !IsPotentialFunctionAliasName(reduceAssignment.SourceName)
                    || reduceAssignment.Arguments.Count < 2)
                {
                    continue;
                }

                Dictionary<string, string> targetMemberAliasMap = GetOrCreateMemberAliasMap(
                    objectMemberAliasMap,
                    reduceAssignment.TargetName);

                if (reduceAssignment.MethodName.Equals("values", StringComparison.OrdinalIgnoreCase)
                    && IsRecoverableStaticArrayReduceToArray(reduceAssignment.Arguments)
                    && objectMemberAliasMap.TryGetValue(reduceAssignment.SourceName, out IReadOnlyDictionary<string, string> valuesSourceMemberAliasMap))
                {
                    CopyObjectValuesAsArrayMemberAliases(targetMemberAliasMap, valuesSourceMemberAliasMap);
                    continue;
                }

                if (reduceAssignment.MethodName.Equals("keys", StringComparison.OrdinalIgnoreCase)
                    && IsRecoverableStaticArrayReduceToArray(reduceAssignment.Arguments))
                {
                    IReadOnlyDictionary<string, string> keysSourceMap;
                    if (!objectKeyAliasMap.TryGetValue(reduceAssignment.SourceName, out keysSourceMap)
                        && !objectMemberAliasMap.TryGetValue(reduceAssignment.SourceName, out keysSourceMap))
                    {
                        continue;
                    }

                    CopyObjectKeysAsArrayMemberAliases(targetMemberAliasMap, keysSourceMap);
                    continue;
                }

                if (reduceAssignment.MethodName.Equals("entries", StringComparison.OrdinalIgnoreCase)
                    && IsRecoverableStaticArrayReduceToObject(reduceAssignment.Arguments)
                    && objectMemberAliasMap.TryGetValue(reduceAssignment.SourceName, out IReadOnlyDictionary<string, string> entriesSourceMemberAliasMap))
                {
                    CopyObjectMemberAliases(targetMemberAliasMap, entriesSourceMemberAliasMap);
                }
            }

            foreach ((string TargetName, string SourceName) keysAssignment in EnumerateObjectKeysAliasAssignments(scriptName))
            {
                if (!IsPotentialFunctionAliasName(keysAssignment.TargetName)
                    || !IsPotentialFunctionAliasName(keysAssignment.SourceName))
                {
                    continue;
                }

                IReadOnlyDictionary<string, string> sourceKeyAliasMap;
                if (!objectKeyAliasMap.TryGetValue(keysAssignment.SourceName, out sourceKeyAliasMap)
                    && !objectMemberAliasMap.TryGetValue(keysAssignment.SourceName, out sourceKeyAliasMap))
                {
                    continue;
                }

                Dictionary<string, string> targetMemberAliasMap = GetOrCreateMemberAliasMap(
                    objectMemberAliasMap,
                    keysAssignment.TargetName);
                CopyObjectKeysAsArrayMemberAliases(targetMemberAliasMap, sourceKeyAliasMap);
            }

            foreach ((string TargetName, string SourceName, bool ProjectKeys) entriesMapAssignment in EnumerateObjectEntriesMapAliasAssignments(scriptName))
            {
                if (!IsPotentialFunctionAliasName(entriesMapAssignment.TargetName)
                    || !IsPotentialFunctionAliasName(entriesMapAssignment.SourceName))
                {
                    continue;
                }

                IReadOnlyDictionary<string, string> sourceEntriesMap;
                if (entriesMapAssignment.ProjectKeys)
                {
                    if (!objectKeyAliasMap.TryGetValue(entriesMapAssignment.SourceName, out sourceEntriesMap))
                    {
                        continue;
                    }
                }
                else if (!objectMemberAliasMap.TryGetValue(entriesMapAssignment.SourceName, out sourceEntriesMap))
                {
                    continue;
                }

                Dictionary<string, string> targetMemberAliasMap = GetOrCreateMemberAliasMap(
                    objectMemberAliasMap,
                    entriesMapAssignment.TargetName);
                CopyObjectValuesAsArrayMemberAliases(targetMemberAliasMap, sourceEntriesMap);
            }

            foreach ((string TargetName, string SourceName) entriesAssignment in EnumerateObjectEntriesAliasAssignments(scriptName))
            {
                if (!IsPotentialFunctionAliasName(entriesAssignment.TargetName)
                    || !IsPotentialFunctionAliasName(entriesAssignment.SourceName)
                    || !objectMemberAliasMap.TryGetValue(entriesAssignment.SourceName, out IReadOnlyDictionary<string, string> sourceMemberAliasMap))
                {
                    continue;
                }

                Dictionary<string, string> targetMemberAliasMap = GetOrCreateMemberAliasMap(
                    objectMemberAliasMap,
                    entriesAssignment.TargetName);
                CopyObjectMemberAliases(targetMemberAliasMap, sourceMemberAliasMap);
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

            foreach ((string TargetName, string SourceName, string MethodName) transformAssignment in EnumerateArrayStaticTransformAliasAssignments(scriptName))
            {
                if (!IsPotentialFunctionAliasName(transformAssignment.TargetName)
                    || !IsPotentialFunctionAliasName(transformAssignment.SourceName)
                    || !objectMemberAliasMap.TryGetValue(transformAssignment.SourceName, out IReadOnlyDictionary<string, string> sourceMemberAliasMap))
                {
                    continue;
                }

                Dictionary<string, string> targetMemberAliasMap = GetOrCreateMemberAliasMap(
                    objectMemberAliasMap,
                    transformAssignment.TargetName);
                if (!transformAssignment.TargetName.Equals(transformAssignment.SourceName, StringComparison.OrdinalIgnoreCase))
                {
                    CopyArrayMemberAliases(targetMemberAliasMap, sourceMemberAliasMap);
                }
            }

            foreach ((string TargetName, string SourceName, IReadOnlyList<string> Arguments) reduceAssignment in EnumerateArrayReduceAliasAssignments(scriptName))
            {
                if (!IsPotentialFunctionAliasName(reduceAssignment.TargetName)
                    || !IsPotentialFunctionAliasName(reduceAssignment.SourceName)
                    || !objectMemberAliasMap.TryGetValue(reduceAssignment.SourceName, out IReadOnlyDictionary<string, string> sourceMemberAliasMap))
                {
                    continue;
                }

                Dictionary<string, string> targetMemberAliasMap = GetOrCreateMemberAliasMap(
                    objectMemberAliasMap,
                    reduceAssignment.TargetName);

                if (IsRecoverableStaticArrayReduceToObject(reduceAssignment.Arguments))
                {
                    if (!reduceAssignment.TargetName.Equals(reduceAssignment.SourceName, StringComparison.OrdinalIgnoreCase))
                    {
                        CopyObjectMemberAliases(targetMemberAliasMap, sourceMemberAliasMap);
                    }

                    continue;
                }

                if (!IsRecoverableStaticArrayReduceToArray(reduceAssignment.Arguments))
                {
                    continue;
                }

                if (!reduceAssignment.TargetName.Equals(reduceAssignment.SourceName, StringComparison.OrdinalIgnoreCase))
                {
                    CopyArrayMemberAliases(targetMemberAliasMap, sourceMemberAliasMap);
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

            foreach ((string TargetName, string SourceName, string MethodName) orderingAssignment in EnumerateArrayOrderingAliasAssignments(scriptName))
            {
                if (!IsPotentialFunctionAliasName(orderingAssignment.TargetName)
                    || !IsPotentialFunctionAliasName(orderingAssignment.SourceName)
                    || !objectMemberAliasMap.TryGetValue(orderingAssignment.SourceName, out IReadOnlyDictionary<string, string> sourceMemberAliasMap))
                {
                    continue;
                }

                Dictionary<string, string> targetMemberAliasMap = GetOrCreateMemberAliasMap(
                    objectMemberAliasMap,
                    orderingAssignment.TargetName);
                if (!orderingAssignment.TargetName.Equals(orderingAssignment.SourceName, StringComparison.OrdinalIgnoreCase))
                {
                    CopyArrayMemberAliases(targetMemberAliasMap, sourceMemberAliasMap);
                }

                if (orderingAssignment.MethodName.Equals("toReversed", StringComparison.OrdinalIgnoreCase))
                {
                    ReverseArrayMemberAliases(targetMemberAliasMap);
                    continue;
                }

                if (orderingAssignment.MethodName.Equals("toSorted", StringComparison.OrdinalIgnoreCase)
                    || orderingAssignment.MethodName.Equals("sort", StringComparison.OrdinalIgnoreCase))
                {
                    SortArrayMemberAliases(targetMemberAliasMap);
                }
            }

            foreach ((string TargetName, string SourceName, string MethodName, IReadOnlyList<string> Arguments) copyMutationAssignment in EnumerateArrayCopyMutatorAliasAssignments(scriptName))
            {
                if (!IsPotentialFunctionAliasName(copyMutationAssignment.TargetName)
                    || !IsPotentialFunctionAliasName(copyMutationAssignment.SourceName)
                    || !objectMemberAliasMap.TryGetValue(copyMutationAssignment.SourceName, out IReadOnlyDictionary<string, string> sourceMemberAliasMap))
                {
                    continue;
                }

                Dictionary<string, string> targetMemberAliasMap = GetOrCreateMemberAliasMap(
                    objectMemberAliasMap,
                    copyMutationAssignment.TargetName);
                if (!copyMutationAssignment.TargetName.Equals(copyMutationAssignment.SourceName, StringComparison.OrdinalIgnoreCase))
                {
                    CopyArrayMemberAliases(targetMemberAliasMap, sourceMemberAliasMap);
                }

                if (copyMutationAssignment.MethodName.Equals("toSpliced", StringComparison.OrdinalIgnoreCase))
                {
                    ApplyArraySpliceAliasMutation(
                        targetMemberAliasMap,
                        copyMutationAssignment.Arguments,
                        localAliasMap,
                        objectMemberAliasMap);
                    continue;
                }

                if (copyMutationAssignment.MethodName.Equals("with", StringComparison.OrdinalIgnoreCase))
                {
                    ApplyArrayWithAliasMutation(
                        targetMemberAliasMap,
                        copyMutationAssignment.Arguments,
                        localAliasMap,
                        objectMemberAliasMap);
                }
            }

            foreach ((string TargetName, string MethodName, IReadOnlyList<string> Arguments) collectionConstructor in EnumerateCollectionConstructorAliasAssignments(scriptName))
            {
                if (!IsPotentialFunctionAliasName(collectionConstructor.TargetName)
                    || collectionConstructor.Arguments.Count == 0)
                {
                    continue;
                }

                Dictionary<string, string> targetMemberAliasMap = GetOrCreateMemberAliasMap(
                    objectMemberAliasMap,
                    collectionConstructor.TargetName);
                if (collectionConstructor.MethodName.Equals("Map", StringComparison.OrdinalIgnoreCase))
                {
                    if (collectionConstructor.Arguments.Count == 1)
                    {
                        ApplyEntryCollectionAliasMembers(
                            targetMemberAliasMap,
                            collectionConstructor.Arguments[0],
                            localAliasMap,
                            objectMemberAliasMap);
                    }

                    continue;
                }

                CopyCollectionFactoryAliasArguments(
                    targetMemberAliasMap,
                    collectionConstructor.MethodName,
                    collectionConstructor.Arguments,
                    localAliasMap,
                    objectMemberAliasMap);
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

        private static void AddObjectLiteralAliasMembers(
            IDictionary<string, string> memberAliasMap,
            string objectBody,
            IReadOnlyDictionary<string, string> localAliasMap,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap)
        {
            if (memberAliasMap == null || string.IsNullOrWhiteSpace(objectBody))
            {
                return;
            }

            foreach (string objectMember in SplitTopLevelByComma(objectBody))
            {
                if (TryParseSpreadAliasMember(objectMember, out string spreadSourceName)
                    && objectMemberAliasMap != null
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
        }

        private static void AddArrayLiteralAliasMembers(
            IDictionary<string, string> memberAliasMap,
            string arrayBody,
            IReadOnlyDictionary<string, string> localAliasMap,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap)
        {
            if (memberAliasMap == null || string.IsNullOrWhiteSpace(arrayBody))
            {
                return;
            }

            IReadOnlyList<string> elements = SplitTopLevelByComma(arrayBody);
            for (int i = 0; i < elements.Count; i++)
            {
                if (TryParseSpreadAliasMember(elements[i], out string spreadSourceName)
                    && objectMemberAliasMap != null
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
        }

        private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> BuildObjectKeyAliasMap(
            string scriptName,
            IReadOnlyDictionary<string, string> localAliasMap,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap)
        {
            var objectKeyAliasMap =
                new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(scriptName))
            {
                return objectKeyAliasMap;
            }

            foreach (Match match in ObjectLiteralAssignmentPattern.Matches(scriptName))
            {
                string objectName = NormalizeFunctionAliasArgument(match.Groups["lhs"]?.Value);
                string objectBody = match.Groups["body"]?.Value;
                if (!IsPotentialFunctionAliasName(objectName) || string.IsNullOrWhiteSpace(objectBody))
                {
                    continue;
                }

                Dictionary<string, string> keyAliasMap = GetOrCreateMemberAliasMap(objectKeyAliasMap, objectName);
                foreach (string objectMember in SplitTopLevelByComma(objectBody))
                {
                    if (TryParseSpreadAliasMember(objectMember, out string spreadSourceName))
                    {
                        if (objectKeyAliasMap.TryGetValue(spreadSourceName, out IReadOnlyDictionary<string, string> spreadKeyAliasMap))
                        {
                            CopyObjectMemberAliases(keyAliasMap, spreadKeyAliasMap);
                            continue;
                        }

                        if (objectMemberAliasMap != null
                            && objectMemberAliasMap.TryGetValue(spreadSourceName, out IReadOnlyDictionary<string, string> spreadMemberAliasMap))
                        {
                            CopyObjectKeysAsAliasValues(keyAliasMap, spreadMemberAliasMap);
                        }

                        continue;
                    }

                    if (!TryParseObjectLiteralMember(
                            objectMember,
                            localAliasMap,
                            out string memberKey,
                            out _)
                        || !IsPotentialFunctionAliasName(memberKey))
                    {
                        continue;
                    }

                    keyAliasMap[memberKey] = memberKey;
                }
            }

            foreach (Match match in ObjectMemberAliasAssignmentPattern.Matches(scriptName))
            {
                string objectName = NormalizeFunctionAliasArgument(match.Groups["object"]?.Value).TrimEnd(';');
                if (!IsPotentialFunctionAliasName(objectName))
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

                if (!IsPotentialFunctionAliasName(memberKey))
                {
                    continue;
                }

                Dictionary<string, string> keyAliasMap = GetOrCreateMemberAliasMap(objectKeyAliasMap, objectName);
                keyAliasMap[memberKey] = memberKey;
            }

            foreach ((string TargetName, string WrappedValue) wrapperAssignment in EnumerateObjectStaticWrapperAliasAssignments(scriptName))
            {
                if (!IsPotentialFunctionAliasName(wrapperAssignment.TargetName)
                    || string.IsNullOrWhiteSpace(wrapperAssignment.WrappedValue))
                {
                    continue;
                }

                Dictionary<string, string> targetKeyAliasMap = GetOrCreateMemberAliasMap(
                    objectKeyAliasMap,
                    wrapperAssignment.TargetName);

                string wrappedValue = StripOuterBalancedParentheses(wrapperAssignment.WrappedValue.Trim());
                string sourceName = NormalizeFunctionAliasArgument(wrappedValue).TrimEnd(';');
                if (objectKeyAliasMap.TryGetValue(sourceName, out IReadOnlyDictionary<string, string> sourceKeyAliasMap))
                {
                    CopyObjectMemberAliases(targetKeyAliasMap, sourceKeyAliasMap);
                    continue;
                }

                if (objectMemberAliasMap != null
                    && objectMemberAliasMap.TryGetValue(sourceName, out IReadOnlyDictionary<string, string> sourceMemberAliasMap))
                {
                    CopyObjectKeysAsAliasValues(targetKeyAliasMap, sourceMemberAliasMap);
                    continue;
                }

                if (wrappedValue.Length >= 2 && wrappedValue[0] == '{' && wrappedValue[^1] == '}')
                {
                    AddObjectLiteralKeyAliasMembers(
                        targetKeyAliasMap,
                        wrappedValue[1..^1],
                        localAliasMap);
                }
            }

            return objectKeyAliasMap;
        }

        private static void AddObjectLiteralKeyAliasMembers(
            IDictionary<string, string> keyAliasMap,
            string objectBody,
            IReadOnlyDictionary<string, string> localAliasMap)
        {
            if (keyAliasMap == null || string.IsNullOrWhiteSpace(objectBody))
            {
                return;
            }

            foreach (string objectMember in SplitTopLevelByComma(objectBody))
            {
                if (!TryParseObjectLiteralMember(
                        objectMember,
                        localAliasMap,
                        out string memberKey,
                        out _)
                    || !IsPotentialFunctionAliasName(memberKey))
                {
                    continue;
                }

                keyAliasMap[memberKey] = memberKey;
            }
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

            if (TryResolveInlineAssignmentAliasCandidates(
                    normalizedValue,
                    localAliasMap,
                    objectMemberAliasMap,
                    out IReadOnlyList<string> inlineAssignmentAliases))
            {
                for (int i = 0; i < inlineAssignmentAliases.Count; i++)
                {
                    AddAlias(inlineAssignmentAliases[i]);
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

            if (TryUnwrapImmediateCallbackInvocation(normalizedValue, out string immediateCallbackExpression))
            {
                foreach (string immediateCallbackAlias in ResolveAssignmentAliasCandidates(
                             immediateCallbackExpression,
                             localAliasMap,
                             objectMemberAliasMap))
                {
                    AddAlias(immediateCallbackAlias);
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

            if (TryResolveObjectNoArgumentCallAliasCandidate(
                    normalizedValue,
                    localAliasMap,
                    objectMemberAliasMap,
                    out string objectNoArgumentCallAlias))
            {
                AddAlias(objectNoArgumentCallAlias);
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

            if (TryResolveInlineAssignmentAliasCandidates(
                    normalizedValue,
                    localAliasMap,
                    objectMemberAliasMap,
                    out IReadOnlyList<string> inlineAssignmentAliases)
                && inlineAssignmentAliases.Count > 0)
            {
                return inlineAssignmentAliases[0];
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

            if (TryUnwrapImmediateCallbackInvocation(normalizedValue, out string immediateCallbackExpression))
            {
                string immediateCallbackAlias = ResolveAssignmentAliasCandidate(
                    immediateCallbackExpression,
                    localAliasMap,
                    objectMemberAliasMap);
                if (!string.IsNullOrWhiteSpace(immediateCallbackAlias))
                {
                    return immediateCallbackAlias;
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

            if (TryResolveObjectNoArgumentCallAliasCandidate(
                    normalizedValue,
                    localAliasMap,
                    objectMemberAliasMap,
                    out string objectNoArgumentCallAlias))
            {
                return objectNoArgumentCallAlias;
            }

            if (TryResolveNoArgumentFunctionCallAliasCandidate(
                    normalizedValue,
                    localAliasMap,
                    out string noArgumentCallAlias))
            {
                return noArgumentCallAlias;
            }

            if (TryResolveArrayMethodObjectAliasCandidate(
                    normalizedValue,
                    localAliasMap,
                    objectMemberAliasMap,
                    out string arrayMethodAlias))
            {
                return arrayMethodAlias;
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

            if (TryResolveStaticStringAliasExpression(
                    normalizedValue,
                    localAliasMap,
                    out string staticStringAlias))
            {
                return staticStringAlias;
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

        private static IReadOnlyList<ArrayAliasMutationOperation> EnumerateArrayAliasMutationOperations(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Array.Empty<ArrayAliasMutationOperation>();
            }

            var operations = new List<ArrayAliasMutationOperation>();
            foreach (Match match in ArrayMutatorAliasCallPattern.Matches(value))
            {
                string objectName = NormalizeFunctionAliasArgument(match.Groups["object"]?.Value);
                string methodName = NormalizeFunctionAliasArgument(match.Groups["method"]?.Value);
                if ((methodName.Equals("shift", StringComparison.OrdinalIgnoreCase)
                        || methodName.Equals("pop", StringComparison.OrdinalIgnoreCase))
                    && IsFunctionCallAssignmentRightHandSide(value, match.Groups["object"].Index))
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
                operations.Add(new ArrayAliasMutationOperation(
                    match.Index,
                    objectName,
                    methodName,
                    new List<string>(SplitFunctionArguments(argumentText))));
            }

            foreach (Match match in ArrayNoArgumentOrderingAliasCallPattern.Matches(value))
            {
                string objectName = NormalizeFunctionAliasArgument(match.Groups["object"]?.Value);
                string methodName = NormalizeFunctionAliasArgument(match.Groups["method"]?.Value);
                if (!IsPotentialFunctionAliasName(objectName))
                {
                    continue;
                }

                int openIndex = value.IndexOf('(', match.Index);
                if (openIndex < 0)
                {
                    continue;
                }

                int closeIndex = FindMatchingCloseParenthesis(value, openIndex);
                if (closeIndex <= openIndex || !string.IsNullOrWhiteSpace(value[(openIndex + 1)..closeIndex]))
                {
                    continue;
                }

                operations.Add(new ArrayAliasMutationOperation(
                    match.Index,
                    objectName,
                    methodName,
                    Array.Empty<string>()));
            }

            operations.Sort((left, right) => left.SourceIndex.CompareTo(right.SourceIndex));
            return operations.Count == 0 ? Array.Empty<ArrayAliasMutationOperation>() : operations;
        }

        private static bool IsFunctionCallAssignmentRightHandSide(string value, int expressionStartIndex)
        {
            if (string.IsNullOrWhiteSpace(value) || expressionStartIndex <= 0 || expressionStartIndex > value.Length)
            {
                return false;
            }

            for (int i = expressionStartIndex - 1; i >= 0; i--)
            {
                char current = value[i];
                if (char.IsWhiteSpace(current))
                {
                    continue;
                }

                if (current == '=')
                {
                    return i == 0 || value[i - 1] is not ('=' or '!' or '<' or '>');
                }

                return false;
            }

            return false;
        }

        private static IEnumerable<(string TargetName, string SourceName, string MethodName)> EnumerateArrayOrderingAliasAssignments(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            foreach (Match match in ArrayOrderingAliasAssignmentPattern.Matches(value))
            {
                string targetName = NormalizeFunctionAliasArgument(match.Groups["lhs"]?.Value).TrimEnd(';');
                string sourceName = NormalizeFunctionAliasArgument(match.Groups["source"]?.Value).TrimEnd(';');
                string methodName = NormalizeFunctionAliasArgument(match.Groups["method"]?.Value).TrimEnd(';');
                if (!IsPotentialFunctionAliasName(targetName)
                    || !IsPotentialFunctionAliasName(sourceName)
                    || string.IsNullOrWhiteSpace(methodName))
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

                if (string.IsNullOrWhiteSpace(value[(openIndex + 1)..closeIndex]))
                {
                    yield return (targetName, sourceName, methodName);
                }
            }
        }

        private static IEnumerable<(string TargetName, string SourceName, string MethodName, IReadOnlyList<string> Arguments)> EnumerateArrayCopyMutatorAliasAssignments(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            foreach (Match match in ArrayCopyMutatorAliasAssignmentPattern.Matches(value))
            {
                string targetName = NormalizeFunctionAliasArgument(match.Groups["lhs"]?.Value).TrimEnd(';');
                string sourceName = NormalizeFunctionAliasArgument(match.Groups["source"]?.Value).TrimEnd(';');
                string methodName = NormalizeFunctionAliasArgument(match.Groups["method"]?.Value).TrimEnd(';');
                if (!IsPotentialFunctionAliasName(targetName)
                    || !IsPotentialFunctionAliasName(sourceName)
                    || string.IsNullOrWhiteSpace(methodName))
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
                yield return (targetName, sourceName, methodName, new List<string>(SplitFunctionArguments(argumentText)));
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

        private static IEnumerable<(string TargetName, IReadOnlyList<string> Arguments)> EnumerateObjectFromEntriesAliasAssignments(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            foreach (Match match in ObjectFromEntriesAliasAssignmentPattern.Matches(value))
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

        private static IEnumerable<(string TargetName, string WrappedValue)> EnumerateObjectStaticWrapperAliasAssignments(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            foreach (Match match in ObjectStaticWrapperAliasAssignmentPattern.Matches(value))
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

                IReadOnlyList<string> arguments = new List<string>(SplitFunctionArguments(value[(openIndex + 1)..closeIndex]));
                if (arguments.Count == 1)
                {
                    yield return (targetName, arguments[0]);
                }
            }
        }

        private static IEnumerable<(string TargetName, string SourceName)> EnumerateObjectValuesAliasAssignments(string value)
        {
            foreach ((string TargetName, string SourceName, string MethodName) assignment in EnumerateObjectKeyOrValuesAliasAssignments(value))
            {
                if (assignment.MethodName.Equals("values", StringComparison.OrdinalIgnoreCase))
                {
                    yield return (assignment.TargetName, assignment.SourceName);
                }
            }
        }

        private static IEnumerable<(string TargetName, string SourceName)> EnumerateObjectKeysAliasAssignments(string value)
        {
            foreach ((string TargetName, string SourceName, string MethodName) assignment in EnumerateObjectKeyOrValuesAliasAssignments(value))
            {
                if (assignment.MethodName.Equals("keys", StringComparison.OrdinalIgnoreCase))
                {
                    yield return (assignment.TargetName, assignment.SourceName);
                }
            }
        }

        private static IEnumerable<(string TargetName, string SourceName, string MethodName)> EnumerateObjectKeyOrValuesAliasAssignments(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            foreach (Match match in ObjectKeyOrValuesAliasAssignmentPattern.Matches(value))
            {
                string targetName = NormalizeFunctionAliasArgument(match.Groups["lhs"]?.Value).TrimEnd(';');
                string methodName = NormalizeFunctionAliasArgument(match.Groups["method"]?.Value).TrimEnd(';');
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

                var arguments = new List<string>(SplitFunctionArguments(value[(openIndex + 1)..closeIndex]));
                if (arguments.Count != 1)
                {
                    continue;
                }

                string sourceName = NormalizeFunctionAliasArgument(arguments[0]).TrimEnd(';');
                if (IsPotentialFunctionAliasName(sourceName))
                {
                    yield return (targetName, sourceName, methodName);
                }
            }
        }

        private static IEnumerable<(string TargetName, string SourceName, bool ProjectKeys)> EnumerateObjectEntriesMapAliasAssignments(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            foreach (Match match in ObjectEntriesMapAliasAssignmentPattern.Matches(value))
            {
                string targetName = NormalizeFunctionAliasArgument(match.Groups["lhs"]?.Value).TrimEnd(';');
                if (!IsPotentialFunctionAliasName(targetName))
                {
                    continue;
                }

                int entriesOpenIndex = value.IndexOf('(', match.Index);
                if (entriesOpenIndex < 0)
                {
                    continue;
                }

                int entriesCloseIndex = FindMatchingCloseParenthesis(value, entriesOpenIndex);
                if (entriesCloseIndex <= entriesOpenIndex)
                {
                    continue;
                }

                var entriesArguments = new List<string>(SplitFunctionArguments(value[(entriesOpenIndex + 1)..entriesCloseIndex]));
                if (entriesArguments.Count != 1)
                {
                    continue;
                }

                string sourceName = NormalizeFunctionAliasArgument(entriesArguments[0]).TrimEnd(';');
                if (!IsPotentialFunctionAliasName(sourceName))
                {
                    continue;
                }

                int mapMemberIndex = SkipWhitespace(value, entriesCloseIndex + 1);
                const string mapMember = ".map";
                if (mapMemberIndex < 0
                    || mapMemberIndex + mapMember.Length > value.Length
                    || !value.AsSpan(mapMemberIndex, mapMember.Length).Equals(mapMember.AsSpan(), StringComparison.Ordinal))
                {
                    continue;
                }

                int mapOpenIndex = SkipWhitespace(value, mapMemberIndex + mapMember.Length);
                if (mapOpenIndex < 0 || mapOpenIndex >= value.Length || value[mapOpenIndex] != '(')
                {
                    continue;
                }

                int mapCloseIndex = FindMatchingCloseParenthesis(value, mapOpenIndex);
                if (mapCloseIndex <= mapOpenIndex)
                {
                    continue;
                }

                var mapArguments = new List<string>(SplitFunctionArguments(value[(mapOpenIndex + 1)..mapCloseIndex]));
                if (mapArguments.Count != 1
                    || !TryResolveObjectEntriesMapProjection(mapArguments[0], out bool projectKeys))
                {
                    continue;
                }

                yield return (targetName, sourceName, projectKeys);
            }
        }

        private static IEnumerable<(string TargetName, string SourceName, string MethodName, IReadOnlyList<string> Arguments)> EnumerateObjectStaticReduceAliasAssignments(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            foreach (Match match in ObjectStaticReduceAliasAssignmentPattern.Matches(value))
            {
                string targetName = NormalizeFunctionAliasArgument(match.Groups["lhs"]?.Value).TrimEnd(';');
                string methodName = NormalizeFunctionAliasArgument(match.Groups["method"]?.Value).TrimEnd(';');
                if (!IsPotentialFunctionAliasName(targetName) || string.IsNullOrWhiteSpace(methodName))
                {
                    continue;
                }

                int staticOpenIndex = value.IndexOf('(', match.Index);
                if (staticOpenIndex < 0)
                {
                    continue;
                }

                int staticCloseIndex = FindMatchingCloseParenthesis(value, staticOpenIndex);
                if (staticCloseIndex <= staticOpenIndex)
                {
                    continue;
                }

                var staticArguments = new List<string>(SplitFunctionArguments(value[(staticOpenIndex + 1)..staticCloseIndex]));
                if (staticArguments.Count != 1)
                {
                    continue;
                }

                string sourceName = NormalizeFunctionAliasArgument(staticArguments[0]).TrimEnd(';');
                if (!IsPotentialFunctionAliasName(sourceName))
                {
                    continue;
                }

                int reduceMemberIndex = SkipWhitespace(value, staticCloseIndex + 1);
                const string reduceMember = ".reduce";
                if (reduceMemberIndex < 0
                    || reduceMemberIndex + reduceMember.Length > value.Length
                    || !value.AsSpan(reduceMemberIndex, reduceMember.Length).Equals(reduceMember.AsSpan(), StringComparison.Ordinal))
                {
                    continue;
                }

                int reduceOpenIndex = SkipWhitespace(value, reduceMemberIndex + reduceMember.Length);
                if (reduceOpenIndex < 0 || reduceOpenIndex >= value.Length || value[reduceOpenIndex] != '(')
                {
                    continue;
                }

                int reduceCloseIndex = FindMatchingCloseParenthesis(value, reduceOpenIndex);
                if (reduceCloseIndex <= reduceOpenIndex)
                {
                    continue;
                }

                yield return (
                    targetName,
                    sourceName,
                    methodName,
                    new List<string>(SplitFunctionArguments(value[(reduceOpenIndex + 1)..reduceCloseIndex])));
            }
        }

        private static IEnumerable<(string TargetName, string SourceName)> EnumerateObjectEntriesAliasAssignments(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            foreach (Match match in ObjectEntriesMapAliasAssignmentPattern.Matches(value))
            {
                string targetName = NormalizeFunctionAliasArgument(match.Groups["lhs"]?.Value).TrimEnd(';');
                if (!IsPotentialFunctionAliasName(targetName))
                {
                    continue;
                }

                int entriesOpenIndex = value.IndexOf('(', match.Index);
                if (entriesOpenIndex < 0)
                {
                    continue;
                }

                int entriesCloseIndex = FindMatchingCloseParenthesis(value, entriesOpenIndex);
                if (entriesCloseIndex <= entriesOpenIndex || SkipWhitespace(value, entriesCloseIndex + 1) >= 0)
                {
                    continue;
                }

                var entriesArguments = new List<string>(SplitFunctionArguments(value[(entriesOpenIndex + 1)..entriesCloseIndex]));
                if (entriesArguments.Count != 1)
                {
                    continue;
                }

                string sourceName = NormalizeFunctionAliasArgument(entriesArguments[0]).TrimEnd(';');
                if (IsPotentialFunctionAliasName(sourceName))
                {
                    yield return (targetName, sourceName);
                }
            }
        }

        private static IEnumerable<(string TargetName, string SourceName, string MethodName)> EnumerateArrayStaticTransformAliasAssignments(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            foreach (Match match in ArrayStaticTransformAliasAssignmentPattern.Matches(value))
            {
                string targetName = NormalizeFunctionAliasArgument(match.Groups["lhs"]?.Value).TrimEnd(';');
                string sourceName = NormalizeFunctionAliasArgument(match.Groups["source"]?.Value).TrimEnd(';');
                string methodName = NormalizeFunctionAliasArgument(match.Groups["method"]?.Value).TrimEnd(';');
                if (!IsPotentialFunctionAliasName(targetName)
                    || !IsPotentialFunctionAliasName(sourceName)
                    || string.IsNullOrWhiteSpace(methodName))
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
                if (IsRecoverableStaticArrayTransform(methodName, arguments))
                {
                    yield return (targetName, sourceName, methodName);
                }
            }
        }

        private static IEnumerable<(string TargetName, string SourceName, IReadOnlyList<string> Arguments)> EnumerateArrayReduceAliasAssignments(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            foreach (Match match in ArrayReduceAliasAssignmentPattern.Matches(value))
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

                yield return (targetName, sourceName, new List<string>(SplitFunctionArguments(value[(openIndex + 1)..closeIndex])));
            }
        }

        private static bool TryResolveObjectEntriesMapProjection(string callback, out bool projectKeys)
        {
            projectKeys = false;
            if (string.IsNullOrWhiteSpace(callback))
            {
                return false;
            }

            string parameterText;
            string bodyExpression;
            string normalizedCallback = StripOuterBalancedParentheses(callback.Trim());
            if (TryParseArrowCallback(normalizedCallback, out parameterText, out bodyExpression))
            {
                return TryResolveObjectEntriesProjectionFromParts(parameterText, bodyExpression, out projectKeys);
            }

            if (TryParseSingleParameterFunctionCallback(normalizedCallback, out parameterText, out IReadOnlyList<string> returnExpressions))
            {
                for (int i = 0; i < returnExpressions.Count; i++)
                {
                    if (TryResolveObjectEntriesProjectionFromParts(parameterText, returnExpressions[i], out projectKeys))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsRecoverableStaticArrayTransform(string methodName, IReadOnlyList<string> arguments)
        {
            if (string.IsNullOrWhiteSpace(methodName))
            {
                return false;
            }

            if (methodName.Equals("flat", StringComparison.OrdinalIgnoreCase))
            {
                return arguments == null
                    || arguments.Count == 0
                    || (arguments.Count == 1
                        && TryResolveBracketIndexKeyLiteral(arguments[0], out string depth)
                        && string.Equals(depth, "1", StringComparison.Ordinal));
            }

            if (arguments == null || arguments.Count != 1)
            {
                return false;
            }

            string callback = StripOuterBalancedParentheses(arguments[0]?.Trim());
            if (methodName.Equals("filter", StringComparison.OrdinalIgnoreCase)
                && (callback.Equals("Boolean", StringComparison.OrdinalIgnoreCase)
                    || callback.Equals("String", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return TryResolveIdentityCallback(callback);
        }

        private static bool IsRecoverableStaticArrayReduceToArray(IReadOnlyList<string> arguments)
        {
            if (arguments == null || arguments.Count < 2)
            {
                return false;
            }

            string initialValue = StripOuterBalancedParentheses(arguments[1]?.Trim());
            if (!initialValue.Equals("[]", StringComparison.Ordinal)
                && !initialValue.Equals("Array()", StringComparison.OrdinalIgnoreCase)
                && !initialValue.Equals("new Array()", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return TryResolveArrayReducePushIdentityCallback(arguments[0]);
        }

        private static bool IsRecoverableStaticArrayReduceToObject(IReadOnlyList<string> arguments)
        {
            if (arguments == null || arguments.Count < 2)
            {
                return false;
            }

            string initialValue = StripOuterBalancedParentheses(arguments[1]?.Trim());
            return initialValue.Equals("{}", StringComparison.Ordinal)
                && TryResolveObjectReduceAssignIdentityCallback(arguments[0]);
        }

        private static bool TryResolveArrayReducePushIdentityCallback(string callback)
        {
            if (string.IsNullOrWhiteSpace(callback))
            {
                return false;
            }

            string normalizedCallback = StripOuterBalancedParentheses(callback.Trim());
            if (TryParseArrowCallback(normalizedCallback, out string parameterText, out string bodyExpression)
                && TryResolveArrayReducePushIdentityCallbackFromParts(parameterText, bodyExpression, normalizedCallback))
            {
                return true;
            }

            return TryParseTwoParameterFunctionCallback(normalizedCallback, out parameterText, out string bodyText)
                && TryResolveArrayReducePushIdentityCallbackFromParts(parameterText, bodyText, bodyText);
        }

        private static bool TryResolveObjectReduceAssignIdentityCallback(string callback)
        {
            if (string.IsNullOrWhiteSpace(callback))
            {
                return false;
            }

            string normalizedCallback = StripOuterBalancedParentheses(callback.Trim());
            if (TryParseArrowCallback(normalizedCallback, out string parameterText, out string bodyExpression)
                && TryResolveObjectReduceAssignIdentityCallbackFromParts(parameterText, bodyExpression, normalizedCallback))
            {
                return true;
            }

            return TryParseTwoParameterFunctionCallback(normalizedCallback, out parameterText, out string bodyText)
                && TryResolveObjectReduceAssignIdentityCallbackFromParts(parameterText, bodyText, bodyText);
        }

        private static bool TryResolveArrayReducePushIdentityCallbackFromParts(
            string parameterText,
            string bodyExpression,
            string callbackBodyText)
        {
            IReadOnlyList<string> parameters = SplitTopLevelByComma(
                StripOuterBalancedParentheses(parameterText?.Trim()));
            if (parameters.Count < 2)
            {
                return false;
            }

            string accumulatorName = NormalizeFunctionAliasArgument(parameters[0]).TrimEnd(';');
            string itemName = NormalizeFunctionAliasArgument(parameters[1]).TrimEnd(';');
            if (!IsPotentialFunctionAliasName(accumulatorName) || !IsPotentialFunctionAliasName(itemName))
            {
                return false;
            }

            return ReducerBodyPushesItemAndReturnsAccumulator(
                accumulatorName,
                itemName,
                bodyExpression,
                callbackBodyText);
        }

        private static bool TryResolveObjectReduceAssignIdentityCallbackFromParts(
            string parameterText,
            string bodyExpression,
            string callbackBodyText)
        {
            IReadOnlyList<string> parameters = SplitTopLevelByComma(
                StripOuterBalancedParentheses(parameterText?.Trim()));
            if (parameters.Count < 2)
            {
                return false;
            }

            string accumulatorName = NormalizeFunctionAliasArgument(parameters[0]).TrimEnd(';');
            string entryParameter = StripOuterBalancedParentheses(parameters[1]?.Trim());
            if (!IsPotentialFunctionAliasName(accumulatorName) || string.IsNullOrWhiteSpace(entryParameter))
            {
                return false;
            }

            return ReducerBodyReturnsAccumulator(accumulatorName, bodyExpression, callbackBodyText)
                && ReducerBodyContainsEntryObjectAssignment(accumulatorName, entryParameter, callbackBodyText);
        }

        private static bool ReducerBodyPushesItemAndReturnsAccumulator(
            string accumulatorName,
            string itemName,
            string bodyExpression,
            string callbackBodyText)
        {
            return ReducerBodyReturnsAccumulator(accumulatorName, bodyExpression, callbackBodyText)
                && ReducerBodyContainsAccumulatorPush(accumulatorName, itemName, callbackBodyText);
        }

        private static bool ReducerBodyReturnsAccumulator(
            string accumulatorName,
            string bodyExpression,
            string callbackBodyText)
        {
            string normalizedBody = StripOuterBalancedParentheses(bodyExpression?.Trim()).TrimEnd(';');
            bool returnsAccumulator = NormalizeFunctionAliasArgument(normalizedBody)
                .Equals(accumulatorName, StringComparison.OrdinalIgnoreCase);
            foreach (string sequenceTail in EnumerateSequenceExpressionTailCandidates(normalizedBody))
            {
                if (NormalizeFunctionAliasArgument(sequenceTail).TrimEnd(';')
                    .Equals(accumulatorName, StringComparison.OrdinalIgnoreCase))
                {
                    returnsAccumulator = true;
                    break;
                }
            }

            if (!returnsAccumulator)
            {
                foreach (string returnExpression in EnumerateFunctionReturnExpressions(callbackBodyText))
                {
                    if (NormalizeFunctionAliasArgument(returnExpression).TrimEnd(';')
                        .Equals(accumulatorName, StringComparison.OrdinalIgnoreCase))
                    {
                        returnsAccumulator = true;
                        break;
                    }
                }
            }

            return returnsAccumulator;
        }

        private static bool ReducerBodyContainsAccumulatorPush(
            string accumulatorName,
            string itemName,
            string callbackBodyText)
        {
            if (string.IsNullOrWhiteSpace(accumulatorName)
                || string.IsNullOrWhiteSpace(itemName)
                || string.IsNullOrWhiteSpace(callbackBodyText))
            {
                return false;
            }

            string pattern = $@"\b{Regex.Escape(accumulatorName)}\s*\.\s*push\s*\(\s*{Regex.Escape(itemName)}\s*\)";
            return Regex.IsMatch(callbackBodyText, pattern, RegexOptions.CultureInvariant);
        }

        private static bool ReducerBodyContainsEntryObjectAssignment(
            string accumulatorName,
            string entryParameter,
            string callbackBodyText)
        {
            if (string.IsNullOrWhiteSpace(accumulatorName)
                || string.IsNullOrWhiteSpace(entryParameter)
                || string.IsNullOrWhiteSpace(callbackBodyText))
            {
                return false;
            }

            if (entryParameter.Length >= 2 && entryParameter[0] == '[' && entryParameter[^1] == ']')
            {
                IReadOnlyList<string> names = SplitTopLevelByComma(entryParameter[1..^1]);
                if (names.Count < 2)
                {
                    return false;
                }

                string keyName = NormalizeFunctionAliasArgument(names[0]).TrimEnd(';');
                string valueName = NormalizeFunctionAliasArgument(names[1]).TrimEnd(';');
                if (!IsPotentialFunctionAliasName(keyName) || !IsPotentialFunctionAliasName(valueName))
                {
                    return false;
                }

                string destructuredPattern = $@"\b{Regex.Escape(accumulatorName)}\s*\[\s*{Regex.Escape(keyName)}\s*\]\s*=\s*{Regex.Escape(valueName)}\b";
                return Regex.IsMatch(callbackBodyText, destructuredPattern, RegexOptions.CultureInvariant);
            }

            string entryName = NormalizeFunctionAliasArgument(entryParameter).TrimEnd(';');
            if (!IsPotentialFunctionAliasName(entryName))
            {
                return false;
            }

            string indexedPattern = $@"\b{Regex.Escape(accumulatorName)}\s*\[\s*{Regex.Escape(entryName)}\s*\[\s*0\s*\]\s*\]\s*=\s*{Regex.Escape(entryName)}\s*\[\s*1\s*\]";
            return Regex.IsMatch(callbackBodyText, indexedPattern, RegexOptions.CultureInvariant);
        }

        private static bool TryResolveObjectEntriesProjectionFromParts(string parameterText, string bodyExpression, out bool projectKeys)
        {
            projectKeys = false;
            if (string.IsNullOrWhiteSpace(parameterText) || string.IsNullOrWhiteSpace(bodyExpression))
            {
                return false;
            }

            string normalizedParameter = StripOuterBalancedParentheses(parameterText.Trim());
            string normalizedBody = StripOuterBalancedParentheses(bodyExpression.Trim()).TrimEnd(';');
            if (normalizedParameter.Length >= 2 && normalizedParameter[0] == '[' && normalizedParameter[^1] == ']')
            {
                IReadOnlyList<string> destructuredNames = SplitTopLevelByComma(normalizedParameter[1..^1]);
                for (int i = 0; i < destructuredNames.Count && i < 2; i++)
                {
                    string destructuredName = NormalizeFunctionAliasArgument(destructuredNames[i]).TrimEnd(';');
                    if (IsPotentialFunctionAliasName(destructuredName)
                        && normalizedBody.Equals(destructuredName, StringComparison.OrdinalIgnoreCase))
                    {
                        projectKeys = i == 0;
                        return true;
                    }
                }

                return false;
            }

            string parameterName = NormalizeFunctionAliasArgument(normalizedParameter).TrimEnd(';');
            if (!IsPotentialFunctionAliasName(parameterName))
            {
                return false;
            }

            if (TryParseIndexedObjectAccess(normalizedBody, out string objectName, out string indexExpression)
                && objectName.Equals(parameterName, StringComparison.OrdinalIgnoreCase)
                && TryResolveBracketIndexKeyLiteral(indexExpression, out string entryIndex))
            {
                if (string.Equals(entryIndex, "0", StringComparison.Ordinal))
                {
                    projectKeys = true;
                    return true;
                }

                if (string.Equals(entryIndex, "1", StringComparison.Ordinal))
                {
                    projectKeys = false;
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveIdentityCallback(string callback)
        {
            if (string.IsNullOrWhiteSpace(callback))
            {
                return false;
            }

            string normalizedCallback = StripOuterBalancedParentheses(callback.Trim());
            if (TryParseArrowCallback(normalizedCallback, out string parameterText, out string bodyExpression))
            {
                return IsIdentityCallbackBody(parameterText, bodyExpression);
            }

            if (TryParseSingleParameterFunctionCallback(normalizedCallback, out parameterText, out IReadOnlyList<string> returnExpressions))
            {
                for (int i = 0; i < returnExpressions.Count; i++)
                {
                    if (IsIdentityCallbackBody(parameterText, returnExpressions[i]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsIdentityCallbackBody(string parameterText, string bodyExpression)
        {
            string parameterName = NormalizeFunctionAliasArgument(StripOuterBalancedParentheses(parameterText?.Trim())).TrimEnd(';');
            string body = StripOuterBalancedParentheses(bodyExpression?.Trim()).TrimEnd(';');
            return IsPotentialFunctionAliasName(parameterName)
                && body.Equals(parameterName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseArrowCallback(string callback, out string parameterText, out string bodyExpression)
        {
            parameterText = string.Empty;
            bodyExpression = string.Empty;
            if (string.IsNullOrWhiteSpace(callback))
            {
                return false;
            }

            int arrowIndex = callback.IndexOf("=>", StringComparison.Ordinal);
            if (arrowIndex <= 0 || arrowIndex >= callback.Length - 2)
            {
                return false;
            }

            parameterText = callback[..arrowIndex].Trim();
            string body = callback[(arrowIndex + 2)..].Trim();
            if (body.StartsWith("{", StringComparison.Ordinal) && body.EndsWith("}", StringComparison.Ordinal))
            {
                foreach (string returnExpression in EnumerateFunctionReturnExpressions("function()" + body))
                {
                    bodyExpression = returnExpression;
                    return !string.IsNullOrWhiteSpace(bodyExpression);
                }

                return false;
            }

            bodyExpression = body;
            return !string.IsNullOrWhiteSpace(parameterText) && !string.IsNullOrWhiteSpace(bodyExpression);
        }

        private static bool TryParseSingleParameterFunctionCallback(
            string callback,
            out string parameterText,
            out IReadOnlyList<string> returnExpressions)
        {
            parameterText = string.Empty;
            returnExpressions = Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(callback)
                || !callback.StartsWith("function", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            int openIndex = callback.IndexOf('(');
            if (openIndex < 0)
            {
                return false;
            }

            int closeIndex = FindMatchingCloseParenthesis(callback, openIndex);
            if (closeIndex <= openIndex)
            {
                return false;
            }

            IReadOnlyList<string> parameters = SplitTopLevelByComma(callback[(openIndex + 1)..closeIndex]);
            if (parameters.Count != 1)
            {
                return false;
            }

            parameterText = parameters[0];
            var expressions = new List<string>(EnumerateFunctionReturnExpressions(callback));
            if (expressions.Count == 0)
            {
                return false;
            }

            returnExpressions = expressions;
            return true;
        }

        private static bool TryParseTwoParameterFunctionCallback(
            string callback,
            out string parameterText,
            out string bodyText)
        {
            parameterText = string.Empty;
            bodyText = string.Empty;
            if (string.IsNullOrWhiteSpace(callback)
                || !callback.StartsWith("function", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            int openIndex = callback.IndexOf('(');
            if (openIndex < 0)
            {
                return false;
            }

            int closeIndex = FindMatchingCloseParenthesis(callback, openIndex);
            if (closeIndex <= openIndex)
            {
                return false;
            }

            IReadOnlyList<string> parameters = SplitTopLevelByComma(callback[(openIndex + 1)..closeIndex]);
            if (parameters.Count < 2)
            {
                return false;
            }

            int bodyOpenIndex = SkipWhitespace(callback, closeIndex + 1);
            if (bodyOpenIndex < 0 || bodyOpenIndex >= callback.Length || callback[bodyOpenIndex] != '{')
            {
                return false;
            }

            int bodyCloseIndex = FindMatchingCloseBrace(callback, bodyOpenIndex);
            if (bodyCloseIndex <= bodyOpenIndex)
            {
                return false;
            }

            parameterText = callback[(openIndex + 1)..closeIndex];
            bodyText = callback[(bodyOpenIndex + 1)..bodyCloseIndex];
            return !string.IsNullOrWhiteSpace(parameterText) && !string.IsNullOrWhiteSpace(bodyText);
        }

        private static int SkipWhitespace(string value, int startIndex)
        {
            if (string.IsNullOrEmpty(value) || startIndex < 0 || startIndex >= value.Length)
            {
                return -1;
            }

            for (int i = startIndex; i < value.Length; i++)
            {
                if (!char.IsWhiteSpace(value[i]))
                {
                    return i;
                }
            }

            return -1;
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

        private static IEnumerable<(string TargetName, string MethodName, IReadOnlyList<string> Arguments)> EnumerateCollectionConstructorAliasAssignments(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            foreach (Match match in CollectionConstructorAliasAssignmentPattern.Matches(value))
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

        private static int GetNextArrayMemberIndex(IEnumerable<KeyValuePair<string, string>> memberAliasMap)
        {
            if (memberAliasMap == null)
            {
                return 0;
            }

            int nextIndex = 0;
            foreach (KeyValuePair<string, string> memberAlias in memberAliasMap)
            {
                if (int.TryParse(memberAlias.Key, out int parsedIndex) && parsedIndex >= nextIndex)
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

        private static void ApplyEntryCollectionAliasMembers(
            IDictionary<string, string> memberAliasMap,
            string entryCollection,
            IReadOnlyDictionary<string, string> localAliasMap,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap)
        {
            if (memberAliasMap == null || string.IsNullOrWhiteSpace(entryCollection))
            {
                return;
            }

            string normalizedCollection = StripOuterBalancedParentheses(entryCollection.Trim());
            string sourceName = NormalizeFunctionAliasArgument(normalizedCollection).TrimEnd(';');
            if (IsPotentialFunctionAliasName(sourceName)
                && objectMemberAliasMap != null
                && objectMemberAliasMap.TryGetValue(sourceName, out IReadOnlyDictionary<string, string> sourceMemberAliasMap))
            {
                CopyObjectMemberAliases(memberAliasMap, sourceMemberAliasMap);
                return;
            }

            if (TryParseObjectEntriesCall(normalizedCollection, out string entriesSourceName)
                && objectMemberAliasMap != null
                && objectMemberAliasMap.TryGetValue(entriesSourceName, out sourceMemberAliasMap))
            {
                CopyObjectMemberAliases(memberAliasMap, sourceMemberAliasMap);
                return;
            }

            if (TryParseObjectEntriesIdentityTransformCall(normalizedCollection, out entriesSourceName)
                && objectMemberAliasMap != null
                && objectMemberAliasMap.TryGetValue(entriesSourceName, out sourceMemberAliasMap))
            {
                CopyObjectMemberAliases(memberAliasMap, sourceMemberAliasMap);
                return;
            }

            if (TryParseCollectionFactoryCall(normalizedCollection, out string factoryMethod, out IReadOnlyList<string> factoryArguments))
            {
                if (factoryMethod.Equals("Map", StringComparison.OrdinalIgnoreCase) && factoryArguments.Count == 1)
                {
                    ApplyEntryCollectionAliasMembers(
                        memberAliasMap,
                        factoryArguments[0],
                        localAliasMap,
                        objectMemberAliasMap);
                }

                return;
            }

            if (normalizedCollection.Length < 2 || normalizedCollection[0] != '[' || normalizedCollection[^1] != ']')
            {
                return;
            }

            foreach (string rawEntry in SplitTopLevelByComma(normalizedCollection[1..^1]))
            {
                string entry = StripOuterBalancedParentheses(rawEntry.Trim());
                if (entry.Length < 2 || entry[0] != '[' || entry[^1] != ']')
                {
                    continue;
                }

                IReadOnlyList<string> pair = SplitTopLevelByComma(entry[1..^1]);
                if (pair.Count < 2
                    || !TryResolveEntryKeyAlias(pair[0], localAliasMap, out string memberKey))
                {
                    continue;
                }

                string resolvedAlias = ResolveAssignmentAliasCandidate(
                    pair[1],
                    localAliasMap,
                    objectMemberAliasMap);
                if (IsPotentialFunctionAliasName(resolvedAlias))
                {
                    memberAliasMap[memberKey] = resolvedAlias;
                }
            }
        }

        private static bool TryParseObjectEntriesCall(string value, out string sourceName)
        {
            sourceName = string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalizedValue = StripOuterBalancedParentheses(value.Trim());
            const string prefix = "Object.entries";
            if (!normalizedValue.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            int openIndex = SkipWhitespace(normalizedValue, prefix.Length);
            if (openIndex < 0 || openIndex >= normalizedValue.Length || normalizedValue[openIndex] != '(')
            {
                return false;
            }

            int closeIndex = FindMatchingCloseParenthesis(normalizedValue, openIndex);
            if (closeIndex <= openIndex || SkipWhitespace(normalizedValue, closeIndex + 1) >= 0)
            {
                return false;
            }

            IReadOnlyList<string> arguments = new List<string>(SplitFunctionArguments(normalizedValue[(openIndex + 1)..closeIndex]));
            if (arguments.Count != 1)
            {
                return false;
            }

            sourceName = NormalizeFunctionAliasArgument(arguments[0]).TrimEnd(';');
            return IsPotentialFunctionAliasName(sourceName);
        }

        private static bool TryParseObjectEntriesIdentityTransformCall(string value, out string sourceName)
        {
            sourceName = string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalizedValue = StripOuterBalancedParentheses(value.Trim());
            const string prefix = "Object.entries";
            if (!normalizedValue.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            int entriesOpenIndex = SkipWhitespace(normalizedValue, prefix.Length);
            if (entriesOpenIndex < 0 || entriesOpenIndex >= normalizedValue.Length || normalizedValue[entriesOpenIndex] != '(')
            {
                return false;
            }

            int entriesCloseIndex = FindMatchingCloseParenthesis(normalizedValue, entriesOpenIndex);
            if (entriesCloseIndex <= entriesOpenIndex)
            {
                return false;
            }

            IReadOnlyList<string> entriesArguments = new List<string>(SplitFunctionArguments(normalizedValue[(entriesOpenIndex + 1)..entriesCloseIndex]));
            if (entriesArguments.Count != 1)
            {
                return false;
            }

            string parsedSourceName = NormalizeFunctionAliasArgument(entriesArguments[0]).TrimEnd(';');
            if (!IsPotentialFunctionAliasName(parsedSourceName))
            {
                return false;
            }

            bool recoveredTransform = false;
            int cursor = SkipWhitespace(normalizedValue, entriesCloseIndex + 1);
            while (cursor >= 0)
            {
                if (TryConsumeObjectEntriesIdentityTransformMember(
                        normalizedValue,
                        cursor,
                        out int nextCursor,
                        out bool memberRecovered))
                {
                    recoveredTransform |= memberRecovered;
                    cursor = SkipWhitespace(normalizedValue, nextCursor);
                    continue;
                }

                return false;
            }

            if (!recoveredTransform)
            {
                return false;
            }

            sourceName = parsedSourceName;
            return true;
        }

        private static bool TryConsumeObjectEntriesIdentityTransformMember(
            string value,
            int memberIndex,
            out int nextIndex,
            out bool recoveredTransform)
        {
            nextIndex = memberIndex;
            recoveredTransform = false;
            if (string.IsNullOrWhiteSpace(value)
                || memberIndex < 0
                || memberIndex >= value.Length
                || value[memberIndex] != '.')
            {
                return false;
            }

            int nameStart = memberIndex + 1;
            int nameEnd = nameStart;
            while (nameEnd < value.Length && (char.IsLetterOrDigit(value[nameEnd]) || value[nameEnd] == '_'))
            {
                nameEnd++;
            }

            if (nameEnd <= nameStart)
            {
                return false;
            }

            string methodName = value[nameStart..nameEnd];
            int openIndex = SkipWhitespace(value, nameEnd);
            if (openIndex < 0 || openIndex >= value.Length || value[openIndex] != '(')
            {
                return false;
            }

            int closeIndex = FindMatchingCloseParenthesis(value, openIndex);
            if (closeIndex <= openIndex)
            {
                return false;
            }

            var arguments = new List<string>(SplitFunctionArguments(value[(openIndex + 1)..closeIndex]));
            if (methodName.Equals("map", StringComparison.OrdinalIgnoreCase))
            {
                if (arguments.Count != 1 || !TryResolveObjectEntriesIdentityPairProjection(arguments[0]))
                {
                    return false;
                }

                recoveredTransform = true;
                nextIndex = closeIndex + 1;
                return true;
            }

            if (methodName.Equals("filter", StringComparison.OrdinalIgnoreCase))
            {
                if (!IsRecoverableStaticArrayTransform(methodName, arguments))
                {
                    return false;
                }

                recoveredTransform = true;
                nextIndex = closeIndex + 1;
                return true;
            }

            return false;
        }

        private static bool TryResolveObjectEntriesIdentityPairProjection(string callback)
        {
            if (string.IsNullOrWhiteSpace(callback))
            {
                return false;
            }

            string normalizedCallback = StripOuterBalancedParentheses(callback.Trim());
            if (TryResolveIdentityCallback(normalizedCallback))
            {
                return true;
            }

            if (TryParseArrowCallback(normalizedCallback, out string parameterText, out string bodyExpression))
            {
                return TryResolveObjectEntriesIdentityPairProjectionFromParts(parameterText, bodyExpression);
            }

            if (TryParseSingleParameterFunctionCallback(normalizedCallback, out parameterText, out IReadOnlyList<string> returnExpressions))
            {
                for (int i = 0; i < returnExpressions.Count; i++)
                {
                    if (TryResolveObjectEntriesIdentityPairProjectionFromParts(parameterText, returnExpressions[i]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryResolveObjectEntriesIdentityPairProjectionFromParts(string parameterText, string bodyExpression)
        {
            if (string.IsNullOrWhiteSpace(parameterText) || string.IsNullOrWhiteSpace(bodyExpression))
            {
                return false;
            }

            string normalizedParameter = StripOuterBalancedParentheses(parameterText.Trim());
            string normalizedBody = StripOuterBalancedParentheses(bodyExpression.Trim()).TrimEnd(';');
            if (normalizedBody.Length < 2 || normalizedBody[0] != '[' || normalizedBody[^1] != ']')
            {
                return false;
            }

            IReadOnlyList<string> bodyElements = SplitTopLevelByComma(normalizedBody[1..^1]);
            if (bodyElements.Count != 2)
            {
                return false;
            }

            if (normalizedParameter.Length >= 2 && normalizedParameter[0] == '[' && normalizedParameter[^1] == ']')
            {
                IReadOnlyList<string> destructuredNames = SplitTopLevelByComma(normalizedParameter[1..^1]);
                if (destructuredNames.Count < 2)
                {
                    return false;
                }

                string keyName = NormalizeFunctionAliasArgument(destructuredNames[0]).TrimEnd(';');
                string valueName = NormalizeFunctionAliasArgument(destructuredNames[1]).TrimEnd(';');
                string bodyKeyName = NormalizeFunctionAliasArgument(bodyElements[0]).TrimEnd(';');
                string bodyValueName = NormalizeFunctionAliasArgument(bodyElements[1]).TrimEnd(';');
                return IsPotentialFunctionAliasName(keyName)
                    && IsPotentialFunctionAliasName(valueName)
                    && bodyKeyName.Equals(keyName, StringComparison.OrdinalIgnoreCase)
                    && bodyValueName.Equals(valueName, StringComparison.OrdinalIgnoreCase);
            }

            string parameterName = NormalizeFunctionAliasArgument(normalizedParameter).TrimEnd(';');
            if (!IsPotentialFunctionAliasName(parameterName))
            {
                return false;
            }

            return IsObjectEntriesIndexedProjection(parameterName, bodyElements[0], "0")
                && IsObjectEntriesIndexedProjection(parameterName, bodyElements[1], "1");
        }

        private static bool IsObjectEntriesIndexedProjection(string parameterName, string bodyElement, string expectedIndex)
        {
            string normalizedBodyElement = StripOuterBalancedParentheses(bodyElement?.Trim()).TrimEnd(';');
            return TryParseIndexedObjectAccess(normalizedBodyElement, out string objectName, out string indexExpression)
                && objectName.Equals(parameterName, StringComparison.OrdinalIgnoreCase)
                && TryResolveBracketIndexKeyLiteral(indexExpression, out string entryIndex)
                && entryIndex.Equals(expectedIndex, StringComparison.Ordinal);
        }

        private static bool TryResolveEntryKeyAlias(
            string value,
            IReadOnlyDictionary<string, string> localAliasMap,
            out string memberKey)
        {
            memberKey = string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalizedValue = StripOuterBalancedParentheses(value.Trim());
            if (TryResolveBracketIndexKeyLiteral(normalizedValue, out string literalKey))
            {
                memberKey = NormalizeFunctionAliasArgument(literalKey).TrimEnd(';');
                return IsPotentialFunctionAliasName(memberKey);
            }

            string localName = NormalizeFunctionAliasArgument(normalizedValue).TrimEnd(';');
            if (localAliasMap != null
                && localAliasMap.TryGetValue(localName, out string mappedKey))
            {
                memberKey = NormalizeFunctionAliasArgument(mappedKey).TrimEnd(';');
                return IsPotentialFunctionAliasName(memberKey);
            }

            memberKey = localName;
            return IsPotentialFunctionAliasName(memberKey);
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

                if (argument.StartsWith("...", StringComparison.Ordinal)
                    && TryParseCollectionFactoryCall(argument[3..], out string spreadFactoryMethod, out IReadOnlyList<string> spreadFactoryArguments))
                {
                    AppendCollectionFactoryAliasArguments(
                        flattenedArguments,
                        spreadFactoryMethod,
                        spreadFactoryArguments,
                        objectMemberAliasMap);
                    continue;
                }

                if (TryParseCollectionFactoryCall(argument, out string factoryMethod, out IReadOnlyList<string> factoryArguments))
                {
                    AppendCollectionFactoryAliasArguments(
                        flattenedArguments,
                        factoryMethod,
                        factoryArguments,
                        objectMemberAliasMap);
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

        private static void CopyCollectionFactoryAliasArguments(
            IDictionary<string, string> targetMemberAliasMap,
            string methodName,
            IReadOnlyList<string> arguments,
            IReadOnlyDictionary<string, string> localAliasMap,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap)
        {
            if (targetMemberAliasMap == null || arguments == null || arguments.Count == 0)
            {
                return;
            }

            var aliasArguments = new List<string>();
            AppendCollectionFactoryAliasArguments(
                aliasArguments,
                methodName,
                arguments,
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

        private static void AppendCollectionFactoryAliasArguments(
            ICollection<string> targetArguments,
            string methodName,
            IReadOnlyList<string> arguments,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap)
        {
            if (targetArguments == null || arguments == null || arguments.Count == 0)
            {
                return;
            }

            string normalizedMethodName = NormalizeFunctionAliasArgument(methodName);
            if (normalizedMethodName.Equals("Set", StringComparison.OrdinalIgnoreCase)
                && arguments.Count == 1)
            {
                string sourceName = NormalizeFunctionAliasArgument(arguments[0]).TrimEnd(';');
                if (objectMemberAliasMap != null
                    && objectMemberAliasMap.TryGetValue(sourceName, out IReadOnlyDictionary<string, string> sourceMemberAliasMap))
                {
                    foreach ((_, string aliasName) in EnumerateArrayMemberAliasesInIndexOrder(sourceMemberAliasMap))
                    {
                        if (!ContainsOrdinalIgnoreCase(targetArguments, aliasName))
                        {
                            targetArguments.Add(aliasName);
                        }
                    }

                    return;
                }
            }

            IReadOnlyList<string> aliasArguments = FlattenArrayAliasArguments(arguments, objectMemberAliasMap);
            for (int argumentIndex = 0; argumentIndex < aliasArguments.Count; argumentIndex++)
            {
                string aliasArgument = aliasArguments[argumentIndex];
                if (normalizedMethodName.Equals("Array", StringComparison.OrdinalIgnoreCase)
                    && aliasArguments.Count == 1
                    && int.TryParse(
                        StripOuterBalancedParentheses(aliasArgument).Trim(),
                        System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out _))
                {
                    continue;
                }

                if (normalizedMethodName.Equals("Set", StringComparison.OrdinalIgnoreCase)
                    && ContainsOrdinalIgnoreCase(targetArguments, aliasArgument))
                {
                    continue;
                }

                targetArguments.Add(aliasArgument);
            }
        }

        private static bool ContainsOrdinalIgnoreCase(IEnumerable<string> values, string candidate)
        {
            if (values == null || string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            foreach (string value in values)
            {
                if (string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseCollectionFactoryCall(
            string value,
            out string methodName,
            out IReadOnlyList<string> arguments)
        {
            methodName = string.Empty;
            arguments = Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalizedValue = StripOuterBalancedParentheses(value.Trim());
            if (normalizedValue.StartsWith("new ", StringComparison.Ordinal))
            {
                normalizedValue = normalizedValue[4..].TrimStart();
            }

            int openIndex = FindTrailingCallOpenParenthesis(normalizedValue);
            if (openIndex <= 0 || FindMatchingCloseParenthesis(normalizedValue, openIndex) != normalizedValue.Length - 1)
            {
                return false;
            }

            string candidateMethodName = NormalizeFunctionAliasArgument(normalizedValue[..openIndex]).TrimEnd(';');
            if (!candidateMethodName.Equals("Set", StringComparison.OrdinalIgnoreCase)
                && !candidateMethodName.Equals("Array", StringComparison.OrdinalIgnoreCase)
                && !candidateMethodName.Equals("Map", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            methodName = candidateMethodName;
            arguments = new List<string>(SplitFunctionArguments(normalizedValue[(openIndex + 1)..^1]));
            return arguments.Count > 0;
        }

        private static bool TryResolveInlineAssignmentAliasCandidates(
            string value,
            IReadOnlyDictionary<string, string> localAliasMap,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap,
            out IReadOnlyList<string> aliasNames)
        {
            aliasNames = Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalizedValue = StripOuterBalancedParentheses(value.Trim());
            int assignmentIndex = FindTopLevelAssignmentOperator(normalizedValue);
            if (assignmentIndex <= 0 || assignmentIndex >= normalizedValue.Length - 1)
            {
                return false;
            }

            string leftExpression = NormalizeOptionalChainingAliasAccess(
                NormalizeFunctionAliasArgument(normalizedValue[..assignmentIndex])).TrimEnd(';');
            if (string.IsNullOrWhiteSpace(leftExpression)
                || (!IsPotentialFunctionAliasName(leftExpression)
                    && !TryParseIndexedObjectAccess(leftExpression, out _, out _)
                    && !TryParseDottedObjectAccess(leftExpression, out _, out _)))
            {
                return false;
            }

            string rightExpression = normalizedValue[(assignmentIndex + 1)..].Trim();
            IReadOnlyList<string> resolvedAliases = ResolveAssignmentAliasCandidates(
                rightExpression,
                localAliasMap,
                objectMemberAliasMap);
            if (resolvedAliases.Count == 0)
            {
                return false;
            }

            aliasNames = resolvedAliases;
            return true;
        }

        private static int FindTopLevelAssignmentOperator(string value)
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

                if (groupingDepth > 0 || current != '=')
                {
                    continue;
                }

                char previous = i > 0 ? value[i - 1] : '\0';
                char next = i + 1 < value.Length ? value[i + 1] : '\0';
                if (previous is '=' or '!' or '<' or '>' || next is '=' or '>')
                {
                    continue;
                }

                return i;
            }

            return -1;
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
            if (memberAliasMap == null || arguments == null || arguments.Count == 0)
            {
                return;
            }

            if (!TryResolveBracketIndexKey(arguments[0], localAliasMap, out string indexKey)
                || !int.TryParse(indexKey, out int insertionIndex)
                || insertionIndex < 0)
            {
                return;
            }

            List<string> arrayAliases = CopyArrayAliasesToList(memberAliasMap);
            int normalizedIndex = Math.Min(insertionIndex, arrayAliases.Count);
            int deleteCount = Math.Max(0, arrayAliases.Count - normalizedIndex);
            if (arguments.Count >= 2
                && TryResolveBracketIndexKey(arguments[1], localAliasMap, out string deleteCountKey)
                && int.TryParse(deleteCountKey, out int parsedDeleteCount)
                && parsedDeleteCount >= 0)
            {
                deleteCount = Math.Min(parsedDeleteCount, Math.Max(0, arrayAliases.Count - normalizedIndex));
            }
            else if (arguments.Count >= 2)
            {
                deleteCount = 0;
            }

            var rawInsertionArguments = new List<string>();
            for (int i = Math.Min(2, arguments.Count); i < arguments.Count; i++)
            {
                rawInsertionArguments.Add(arguments[i]);
            }

            IReadOnlyList<string> insertionArguments = FlattenArrayAliasArguments(
                rawInsertionArguments,
                objectMemberAliasMap);
            var resolvedInsertionAliases = new List<string>();
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

                resolvedInsertionAliases.Add(resolvedAlias);
            }

            arrayAliases.RemoveRange(normalizedIndex, deleteCount);
            arrayAliases.InsertRange(normalizedIndex, resolvedInsertionAliases);
            ReplaceArrayMemberAliases(memberAliasMap, arrayAliases);
        }

        private static void ApplyArrayWithAliasMutation(
            IDictionary<string, string> memberAliasMap,
            IReadOnlyList<string> arguments,
            IReadOnlyDictionary<string, string> localAliasMap,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap)
        {
            if (memberAliasMap == null
                || arguments == null
                || arguments.Count != 2
                || !TryResolveBracketIndexKey(arguments[0], localAliasMap, out string indexKey)
                || !int.TryParse(indexKey, out int index)
                || index < 0)
            {
                return;
            }

            string resolvedAlias = ResolveAssignmentAliasCandidate(
                arguments[1],
                localAliasMap,
                objectMemberAliasMap);
            if (!IsPotentialFunctionAliasName(resolvedAlias))
            {
                return;
            }

            var arrayAliases = CopyArrayAliasesToList(memberAliasMap);
            if (index >= arrayAliases.Count)
            {
                return;
            }

            arrayAliases[index] = resolvedAlias;
            ReplaceArrayMemberAliases(memberAliasMap, arrayAliases);
        }

        private static List<string> CopyArrayAliasesToList(IEnumerable<KeyValuePair<string, string>> memberAliasMap)
        {
            var aliases = new List<string>();
            foreach ((_, string aliasName) in EnumerateArrayMemberAliasesInIndexOrder(memberAliasMap))
            {
                aliases.Add(aliasName);
            }

            return aliases;
        }

        private static void ReplaceArrayMemberAliases(IDictionary<string, string> memberAliasMap, IReadOnlyList<string> aliases)
        {
            if (memberAliasMap == null)
            {
                return;
            }

            var removedKeys = new List<string>();
            foreach (KeyValuePair<string, string> memberAlias in memberAliasMap)
            {
                if (int.TryParse(memberAlias.Key, out int parsedIndex) && parsedIndex >= 0)
                {
                    removedKeys.Add(memberAlias.Key);
                }
            }

            for (int i = 0; i < removedKeys.Count; i++)
            {
                memberAliasMap.Remove(removedKeys[i]);
            }

            if (aliases == null)
            {
                return;
            }

            for (int i = 0; i < aliases.Count; i++)
            {
                if (IsPotentialFunctionAliasName(aliases[i]))
                {
                    memberAliasMap[i.ToString(System.Globalization.CultureInfo.InvariantCulture)] = aliases[i];
                }
            }
        }

        private static void CopyArrayMemberAliases(
            IDictionary<string, string> targetMemberAliasMap,
            IEnumerable<KeyValuePair<string, string>> sourceMemberAliasMap)
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

        private static void CopyObjectValuesAsArrayMemberAliases(
            Dictionary<string, string> targetMemberAliasMap,
            IReadOnlyDictionary<string, string> sourceMemberAliasMap)
        {
            if (targetMemberAliasMap == null || sourceMemberAliasMap == null)
            {
                return;
            }

            foreach (KeyValuePair<string, string> sourceMemberAlias in sourceMemberAliasMap)
            {
                string memberKey = GetNextArrayMemberIndex(targetMemberAliasMap).ToString(System.Globalization.CultureInfo.InvariantCulture);
                targetMemberAliasMap[memberKey] = sourceMemberAlias.Value;
            }
        }

        private static void CopyObjectKeysAsAliasValues(
            IDictionary<string, string> targetMemberAliasMap,
            IReadOnlyDictionary<string, string> sourceMemberAliasMap)
        {
            if (targetMemberAliasMap == null || sourceMemberAliasMap == null)
            {
                return;
            }

            foreach (KeyValuePair<string, string> sourceMemberAlias in sourceMemberAliasMap)
            {
                if (IsPotentialFunctionAliasName(sourceMemberAlias.Key))
                {
                    targetMemberAliasMap[sourceMemberAlias.Key] = sourceMemberAlias.Key;
                }
            }
        }

        private static void CopyObjectKeysAsArrayMemberAliases(
            IDictionary<string, string> targetMemberAliasMap,
            IReadOnlyDictionary<string, string> sourceMemberAliasMap)
        {
            if (targetMemberAliasMap == null || sourceMemberAliasMap == null)
            {
                return;
            }

            foreach (KeyValuePair<string, string> sourceMemberAlias in sourceMemberAliasMap)
            {
                if (!IsPotentialFunctionAliasName(sourceMemberAlias.Key))
                {
                    continue;
                }

                string memberKey = GetNextArrayMemberIndex(targetMemberAliasMap).ToString(System.Globalization.CultureInfo.InvariantCulture);
                targetMemberAliasMap[memberKey] = sourceMemberAlias.Key;
            }
        }

        private static IEnumerable<(int Index, string AliasName)> EnumerateArrayMemberAliasesInIndexOrder(
            IEnumerable<KeyValuePair<string, string>> sourceMemberAliasMap)
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

        private static void ApplyArrayRemovalAliasMutation(IDictionary<string, string> memberAliasMap, string methodName)
        {
            if (memberAliasMap == null || memberAliasMap.Count == 0)
            {
                return;
            }

            if (methodName.Equals("pop", StringComparison.OrdinalIgnoreCase))
            {
                int? lastIndex = TryGetLastArrayMemberIndex(memberAliasMap);
                if (lastIndex.HasValue)
                {
                    memberAliasMap.Remove(lastIndex.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }

                return;
            }

            if (!methodName.Equals("shift", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            int? firstIndex = TryGetFirstArrayMemberIndex(memberAliasMap);
            if (!firstIndex.HasValue)
            {
                return;
            }

            var shiftedMembers = new List<(string OldKey, string NewKey, string AliasName)>();
            foreach (KeyValuePair<string, string> memberAlias in memberAliasMap)
            {
                if (!int.TryParse(memberAlias.Key, out int parsedIndex) || parsedIndex < firstIndex.Value)
                {
                    continue;
                }

                if (parsedIndex == firstIndex.Value)
                {
                    shiftedMembers.Add((memberAlias.Key, string.Empty, memberAlias.Value));
                    continue;
                }

                shiftedMembers.Add((
                    memberAlias.Key,
                    (parsedIndex - 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    memberAlias.Value));
            }

            shiftedMembers.Sort((left, right) => left.OldKey.Length != right.OldKey.Length
                ? right.OldKey.Length.CompareTo(left.OldKey.Length)
                : string.CompareOrdinal(right.OldKey, left.OldKey));
            for (int i = 0; i < shiftedMembers.Count; i++)
            {
                memberAliasMap.Remove(shiftedMembers[i].OldKey);
            }

            for (int i = 0; i < shiftedMembers.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(shiftedMembers[i].NewKey))
                {
                    memberAliasMap[shiftedMembers[i].NewKey] = shiftedMembers[i].AliasName;
                }
            }
        }

        private static void ReverseArrayMemberAliases(IDictionary<string, string> memberAliasMap)
        {
            if (memberAliasMap == null || memberAliasMap.Count <= 1)
            {
                return;
            }

            var indexedAliases = new List<(int Index, string AliasName)>();
            foreach (KeyValuePair<string, string> memberAlias in memberAliasMap)
            {
                if (int.TryParse(memberAlias.Key, out int parsedIndex) && parsedIndex >= 0)
                {
                    indexedAliases.Add((parsedIndex, memberAlias.Value));
                }
            }

            if (indexedAliases.Count <= 1)
            {
                return;
            }

            indexedAliases.Sort((left, right) => left.Index.CompareTo(right.Index));
            for (int i = 0; i < indexedAliases.Count; i++)
            {
                memberAliasMap.Remove(indexedAliases[i].Index.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            for (int i = 0; i < indexedAliases.Count; i++)
            {
                string memberKey = indexedAliases[i].Index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                memberAliasMap[memberKey] = indexedAliases[indexedAliases.Count - i - 1].AliasName;
            }
        }

        private static void SortArrayMemberAliases(IDictionary<string, string> memberAliasMap)
        {
            if (memberAliasMap == null || memberAliasMap.Count <= 1)
            {
                return;
            }

            var indexedAliases = new List<(int Index, string AliasName)>();
            foreach (KeyValuePair<string, string> memberAlias in memberAliasMap)
            {
                if (int.TryParse(memberAlias.Key, out int parsedIndex) && parsedIndex >= 0)
                {
                    indexedAliases.Add((parsedIndex, memberAlias.Value));
                }
            }

            if (indexedAliases.Count <= 1)
            {
                return;
            }

            indexedAliases.Sort((left, right) =>
            {
                int aliasComparison = string.CompareOrdinal(left.AliasName, right.AliasName);
                return aliasComparison != 0
                    ? aliasComparison
                    : left.Index.CompareTo(right.Index);
            });

            foreach ((int Index, _) in indexedAliases)
            {
                memberAliasMap.Remove(Index.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            for (int i = 0; i < indexedAliases.Count; i++)
            {
                string memberKey = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                memberAliasMap[memberKey] = indexedAliases[i].AliasName;
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

            string normalizedValue = StripOuterBalancedParentheses(value.Trim());
            return normalizedValue.StartsWith("function", StringComparison.OrdinalIgnoreCase)
                || normalizedValue.Contains("=>", StringComparison.Ordinal);
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

            string normalizedValue = StripOuterBalancedParentheses(value.Trim());
            if (TryResolveFunctionBodyMemberInvocationAlias(
                    normalizedValue,
                    localAliasMap,
                    objectMemberAliasMap,
                    out string memberAlias))
            {
                return memberAlias;
            }

            if (TryResolveExpressionBodiedArrowAlias(
                    normalizedValue,
                    localAliasMap,
                    objectMemberAliasMap,
                    out string arrowAlias))
            {
                return arrowAlias;
            }

            foreach (string functionAliasName in EnumerateFunctionAliasNames(normalizedValue))
            {
                if (IsPotentialFunctionAliasName(functionAliasName))
                {
                    return functionAliasName;
                }
            }

            foreach (Match match in FunctionBodyAliasInvocationPattern.Matches(normalizedValue))
            {
                string aliasName = NormalizeFunctionAliasArgument(match.Groups["name"]?.Value);
                if (IsPotentialFunctionAliasName(aliasName))
                {
                    return aliasName;
                }
            }

            foreach (Match match in BracketMemberAliasInvocationPattern.Matches(normalizedValue))
            {
                string aliasName = NormalizeFunctionAliasArgument(match.Groups["name"]?.Value);
                if (IsPotentialFunctionAliasName(aliasName))
                {
                    return aliasName;
                }
            }

            foreach (Match match in BracketMemberAliasInvokerInvocationPattern.Matches(normalizedValue))
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

            if (TryResolveInlineAssignmentAliasCandidates(
                    normalizedCandidate,
                    localAliasMap,
                    objectMemberAliasMap,
                    out IReadOnlyList<string> inlineAssignmentAliases))
            {
                for (int i = 0; i < inlineAssignmentAliases.Count; i++)
                {
                    yield return inlineAssignmentAliases[i];
                }
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

            if (TryUnwrapImmediateCallbackInvocation(normalizedCandidate, out string immediateCallbackExpression))
            {
                foreach (string immediateCallbackCandidate in EnumerateCanonicalAliasCandidates(
                             immediateCallbackExpression,
                             localAliasMap,
                             objectMemberAliasMap))
                {
                    yield return immediateCallbackCandidate;
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

            if (TryResolveObjectNoArgumentCallAliasCandidate(
                    normalizedCandidate,
                    localAliasMap,
                    objectMemberAliasMap,
                    out string objectNoArgumentCallAlias))
            {
                yield return objectNoArgumentCallAlias;
                yield break;
            }

            if (TryResolveNoArgumentFunctionCallAliasCandidate(
                    normalizedCandidate,
                    localAliasMap,
                    out string noArgumentCallAlias))
            {
                yield return noArgumentCallAlias;
                yield break;
            }

            if (TryResolveArrayMethodObjectAliasCandidate(
                    normalizedCandidate,
                    localAliasMap,
                    objectMemberAliasMap,
                    out string arrayMethodAlias))
            {
                yield return arrayMethodAlias;
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

            if (TryResolveBracketIndexKeyLiteral(normalizedCandidate, out string literalAliasCandidate)
                && IsPotentialFunctionAliasName(literalAliasCandidate))
            {
                yield return literalAliasCandidate;
                yield break;
            }

            if (TryParseIndexedObjectAccess(normalizedCandidate, out _, out _))
            {
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

            if (TryResolveArrayMethodObjectAliasCandidate(value, localAliasMap, objectMemberAliasMap, out string arrayMethodAlias))
            {
                aliasName = arrayMethodAlias;
                return true;
            }

            if (TryResolveDottedObjectAliasCandidate(value, objectMemberAliasMap, out string dottedAlias))
            {
                aliasName = dottedAlias;
                return true;
            }

            if (TryResolveObjectNoArgumentCallAliasCandidate(value, localAliasMap, objectMemberAliasMap, out string objectCallAlias))
            {
                aliasName = objectCallAlias;
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
            return TryResolveStaticBracketExpressionAlias(expression, null, out aliasName);
        }

        private static bool TryResolveStaticBracketExpressionAlias(
            string expression,
            IReadOnlyDictionary<string, string> localAliasMap,
            out string aliasName)
        {
            aliasName = string.Empty;
            if (string.IsNullOrWhiteSpace(expression))
            {
                return false;
            }

            string normalizedExpression = StripOuterBalancedParentheses(expression.Trim());
            if (!TryResolveStaticStringAliasExpression(normalizedExpression, localAliasMap, out string resolvedAliasName))
            {
                return false;
            }

            aliasName = resolvedAliasName;
            return true;
        }

        private static bool TryResolveStaticStringAliasExpression(
            string expression,
            IReadOnlyDictionary<string, string> localAliasMap,
            out string aliasName)
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

            if (TryResolveStaticTemplateLiteralAlias(normalizedExpression, localAliasMap, out string templateAliasName)
                && IsPotentialFunctionAliasName(templateAliasName))
            {
                aliasName = templateAliasName;
                return true;
            }

            IReadOnlyList<string> tokens = SplitTopLevelByPlus(normalizedExpression);
            if (tokens.Count <= 1)
            {
                if (TryResolveStaticStringAliasToken(normalizedExpression, localAliasMap, out string tokenAlias)
                    && IsPotentialFunctionAliasName(tokenAlias))
                {
                    aliasName = tokenAlias;
                    return true;
                }

                return false;
            }

            var builder = new StringBuilder();
            for (int i = 0; i < tokens.Count; i++)
            {
                string token = StripOuterBalancedParentheses(tokens[i]).Trim();
                if (!TryResolveStaticStringAliasToken(token, localAliasMap, out string resolvedToken))
                {
                    return false;
                }

                builder.Append(resolvedToken);
            }

            string resolvedAliasName = NormalizeFunctionAliasArgument(builder.ToString()).TrimEnd(';');
            if (!IsPotentialFunctionAliasName(resolvedAliasName))
            {
                return false;
            }

            aliasName = resolvedAliasName;
            return true;
        }

        private static bool TryResolveStaticStringAliasToken(
            string expression,
            IReadOnlyDictionary<string, string> localAliasMap,
            out string value)
        {
            value = string.Empty;
            if (string.IsNullOrWhiteSpace(expression))
            {
                return false;
            }

            string normalizedExpression = StripOuterBalancedParentheses(expression.Trim());
            if (TryReadQuotedLiteral(normalizedExpression, out string quotedToken))
            {
                value = quotedToken;
                return true;
            }

            if (TryResolveStaticTemplateLiteralAlias(normalizedExpression, localAliasMap, out string templateToken))
            {
                value = templateToken;
                return true;
            }

            if (int.TryParse(normalizedExpression, out int numericToken))
            {
                value = numericToken.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }

            string localName = NormalizeFunctionAliasArgument(normalizedExpression).TrimEnd(';');
            if (localAliasMap != null
                && localAliasMap.TryGetValue(localName, out string mappedAlias)
                && (TryResolveStaticStringAliasExpression(mappedAlias, localAliasMap, out string mappedValue)
                    || TryResolveStaticStringAliasToken(mappedAlias, localAliasMap, out mappedValue)))
            {
                value = mappedValue;
                return true;
            }

            return false;
        }

        private static bool TryResolveStaticTemplateLiteralAlias(
            string expression,
            IReadOnlyDictionary<string, string> localAliasMap,
            out string aliasName)
        {
            aliasName = string.Empty;
            if (string.IsNullOrWhiteSpace(expression)
                || expression.Length < 2
                || expression[0] != '`'
                || expression[^1] != '`')
            {
                return false;
            }

            string innerValue = expression[1..^1];
            var builder = new StringBuilder();
            for (int i = 0; i < innerValue.Length; i++)
            {
                char current = innerValue[i];
                if (current == '`')
                {
                    return false;
                }

                if (current == '\\')
                {
                    if (i + 1 >= innerValue.Length)
                    {
                        return false;
                    }

                    builder.Append(innerValue[i + 1]);
                    i++;
                    continue;
                }

                if (current != '$' || i + 1 >= innerValue.Length || innerValue[i + 1] != '{')
                {
                    builder.Append(current);
                    continue;
                }

                int expressionStart = i + 2;
                int expressionEnd = FindMatchingTemplateExpressionCloseBrace(innerValue, expressionStart);
                if (expressionEnd < expressionStart)
                {
                    return false;
                }

                string interpolationExpression = innerValue[expressionStart..expressionEnd];
                if (!TryResolveStaticStringAliasExpression(
                            interpolationExpression,
                            localAliasMap,
                            out string interpolationValue)
                    && !TryResolveStaticStringAliasToken(
                            interpolationExpression,
                            localAliasMap,
                            out interpolationValue))
                {
                    return false;
                }

                builder.Append(interpolationValue);
                i = expressionEnd;
            }

            aliasName = NormalizeFunctionAliasArgument(builder.ToString()).TrimEnd(';');
            return IsPotentialFunctionAliasName(aliasName);
        }

        private static int FindMatchingTemplateExpressionCloseBrace(string value, int expressionStart)
        {
            if (string.IsNullOrWhiteSpace(value) || expressionStart < 0 || expressionStart >= value.Length)
            {
                return -1;
            }

            int depth = 1;
            char quote = '\0';
            for (int i = expressionStart; i < value.Length; i++)
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

                if (current == '"' || current == '\'' || current == '`')
                {
                    quote = current;
                    continue;
                }

                if (current == '{')
                {
                    depth++;
                    continue;
                }

                if (current != '}')
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

        private static IReadOnlyList<(char Operator, string Term)> SplitTopLevelByAdditiveOperators(string value)
        {
            return SplitTopLevelByOperators(value, new[] { '+', '-' });
        }

        private static IReadOnlyList<(char Operator, string Factor)> SplitTopLevelByMultiplicativeOperators(string value)
        {
            return SplitTopLevelByOperators(value, new[] { '*', '/' });
        }

        private static IReadOnlyList<(char Operator, string Term)> SplitTopLevelByOperators(string value, IReadOnlyList<char> operators)
        {
            if (string.IsNullOrWhiteSpace(value) || operators == null || operators.Count == 0)
            {
                return Array.Empty<(char Operator, string Term)>();
            }

            var tokens = new List<(char Operator, string Term)>();
            int tokenStart = 0;
            char currentOperator = '+';
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

                if (current is '"' or '\'' or '`')
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

                if (groupingDepth > 0 || !ContainsOperator(operators, current))
                {
                    continue;
                }

                if ((current == '+' || current == '-')
                    && IsUnarySign(value, i, tokenStart))
                {
                    continue;
                }

                tokens.Add((currentOperator, value[tokenStart..i].Trim()));
                currentOperator = current;
                tokenStart = i + 1;
            }

            if (tokens.Count == 0)
            {
                return Array.Empty<(char Operator, string Term)>();
            }

            tokens.Add((currentOperator, value[tokenStart..].Trim()));
            return tokens;
        }

        private static bool ContainsOperator(IReadOnlyList<char> operators, char value)
        {
            for (int i = 0; i < operators.Count; i++)
            {
                if (operators[i] == value)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsUnarySign(string value, int signIndex, int tokenStart)
        {
            for (int i = signIndex - 1; i >= tokenStart; i--)
            {
                char previous = value[i];
                if (char.IsWhiteSpace(previous))
                {
                    continue;
                }

                return previous is '(' or '[' or '{' or '+' or '-' or '*' or '/';
            }

            return true;
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
            if ((quote != '"' && quote != '\'' && quote != '`') || value[^1] != quote)
            {
                return false;
            }

            string innerValue = value[1..^1];
            if (quote == '`'
                && innerValue.IndexOf("${", StringComparison.Ordinal) >= 0)
            {
                return false;
            }

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

        private static int FindMatchingCloseBrace(string value, int openIndex)
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

                if (current == '{')
                {
                    depth++;
                    continue;
                }

                if (current != '}')
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
            int openIndex = FindTrailingCallOpenParenthesis(normalizedValue);
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

        private static int FindTrailingCallOpenParenthesis(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || !value.TrimEnd().EndsWith(")", StringComparison.Ordinal))
            {
                return -1;
            }

            int cursor = value.Length - 1;
            while (cursor >= 0 && char.IsWhiteSpace(value[cursor]))
            {
                cursor--;
            }

            int openIndex = value.LastIndexOf('(', cursor);
            while (openIndex > 0)
            {
                if (FindMatchingCloseParenthesis(value, openIndex) == cursor)
                {
                    return openIndex;
                }

                openIndex = value.LastIndexOf('(', openIndex - 1);
            }

            return -1;
        }

        private static bool TryUnwrapImmediateCallbackInvocation(string value, out string callbackExpression)
        {
            callbackExpression = string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalizedValue = NormalizeFunctionAliasArgument(value).TrimEnd(';');
            if (string.IsNullOrWhiteSpace(normalizedValue) || !normalizedValue.EndsWith(")", StringComparison.Ordinal))
            {
                return false;
            }

            int callOpenIndex = FindTrailingCallOpenParenthesis(normalizedValue);
            if (callOpenIndex <= 0
                || FindMatchingCloseParenthesis(normalizedValue, callOpenIndex) != normalizedValue.Length - 1)
            {
                return false;
            }

            string argumentText = normalizedValue[(callOpenIndex + 1)..^1];
            if (!string.IsNullOrWhiteSpace(argumentText))
            {
                return false;
            }

            string invokedExpression = StripOuterBalancedParentheses(normalizedValue[..callOpenIndex].Trim());
            if (!IsFunctionExpressionText(invokedExpression))
            {
                return false;
            }

            callbackExpression = invokedExpression;
            return true;
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

            CollectStaticArrayIteratorTimerCallbackPublications(
                value,
                inheritedDelayMs,
                publications,
                seen,
                localAliasMap,
                objectMemberAliasMap);

            string directCallbackScanValue = RemoveStaticArrayIteratorCallbackBodies(value);
            foreach ((string FunctionName, IReadOnlyList<string> Arguments) call in EnumerateFunctionCalls(directCallbackScanValue, includeNested: false))
            {
                if (!IsScriptCallbackFunctionName(call.FunctionName)
                    || call.Arguments.Count == 0
                    || !TryResolveCallbackCallDelayCandidates(
                        call.FunctionName,
                        call.Arguments,
                        localAliasMap,
                        out IReadOnlyList<int> callbackDelayCandidates))
                {
                    continue;
                }

                int firstDelayCandidateIndex = IsDelayedCallbackFunctionName(call.FunctionName) && call.Arguments.Count > 1
                    ? 1
                    : call.Arguments.Count;
                for (int delayIndex = 0; delayIndex < callbackDelayCandidates.Count; delayIndex++)
                {
                    int callbackDelayMs = callbackDelayCandidates[delayIndex];
                    int dueDelayMs = inheritedDelayMs >= int.MaxValue - callbackDelayMs
                        ? int.MaxValue
                        : inheritedDelayMs + callbackDelayMs;
                    for (int i = 0; i < call.Arguments.Count; i++)
                    {
                        string argument = NormalizeFunctionAliasArgument(call.Arguments[i]);
                        if (string.IsNullOrWhiteSpace(argument)
                            || (i >= firstDelayCandidateIndex && IsRecoverableDelayArgument(argument, localAliasMap)))
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
        }

        private static void CollectStaticArrayIteratorTimerCallbackPublications(
            string value,
            int inheritedDelayMs,
            ICollection<ScriptAliasPublication> publications,
            ISet<(string ScriptName, int DelayMs)> seen,
            IReadOnlyDictionary<string, string> localAliasMap,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap)
        {
            if (string.IsNullOrWhiteSpace(value)
                || publications == null
                || seen == null
                || objectMemberAliasMap == null
                || objectMemberAliasMap.Count == 0)
            {
                return;
            }

            foreach (Match match in ArrayIteratorAliasCallPattern.Matches(value))
            {
                string sourceName = NormalizeFunctionAliasArgument(match.Groups["source"]?.Value).TrimEnd(';');
                if (!TryGetStaticArrayAliasValues(sourceName, objectMemberAliasMap, out IReadOnlyList<string> aliasValues))
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

                IReadOnlyList<string> arguments = new List<string>(SplitFunctionArguments(value[(openIndex + 1)..closeIndex]));
                if (arguments.Count == 0)
                {
                    continue;
                }

                if (!TryParseIteratorCallback(arguments[0], out string itemName, out string callbackBody))
                {
                    continue;
                }

                CollectIteratorBodyTimerCallbackPublications(
                    callbackBody,
                    itemName,
                    aliasValues,
                    inheritedDelayMs,
                    publications,
                    seen,
                    localAliasMap,
                    objectMemberAliasMap);
            }

            foreach (Match match in ArrayReduceIteratorAliasCallPattern.Matches(value))
            {
                string sourceName = NormalizeFunctionAliasArgument(match.Groups["source"]?.Value).TrimEnd(';');
                if (!TryGetStaticArrayAliasValues(sourceName, objectMemberAliasMap, out IReadOnlyList<string> aliasValues))
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

                IReadOnlyList<string> arguments = new List<string>(SplitFunctionArguments(value[(openIndex + 1)..closeIndex]));
                if (arguments.Count == 0)
                {
                    continue;
                }

                if (!TryParseReduceIteratorCallback(arguments[0], out string itemName, out string callbackBody))
                {
                    continue;
                }

                CollectIteratorBodyTimerCallbackPublications(
                    callbackBody,
                    itemName,
                    aliasValues,
                    inheritedDelayMs,
                    publications,
                    seen,
                    localAliasMap,
                    objectMemberAliasMap);
            }

            foreach (Match match in ForOfAliasLoopPattern.Matches(value))
            {
                string itemName = NormalizeFunctionAliasArgument(match.Groups["item"]?.Value).TrimEnd(';');
                string sourceName = NormalizeFunctionAliasArgument(match.Groups["source"]?.Value).TrimEnd(';');
                if (!IsPotentialFunctionAliasName(itemName)
                    || !TryGetStaticArrayAliasValues(sourceName, objectMemberAliasMap, out IReadOnlyList<string> aliasValues))
                {
                    continue;
                }

                int bodyStart = SkipWhitespace(value, match.Index + match.Length);
                if (bodyStart < 0 || bodyStart >= value.Length)
                {
                    continue;
                }

                string body;
                if (value[bodyStart] == '{')
                {
                    int bodyEnd = FindMatchingCloseBrace(value, bodyStart);
                    if (bodyEnd <= bodyStart)
                    {
                        continue;
                    }

                    body = value[(bodyStart + 1)..bodyEnd];
                }
                else
                {
                    int bodyEnd = value.IndexOf(';', bodyStart);
                    if (bodyEnd < bodyStart)
                    {
                        continue;
                    }

                    body = value[bodyStart..bodyEnd];
                }

                CollectIteratorBodyTimerCallbackPublications(
                    body,
                    itemName,
                    aliasValues,
                    inheritedDelayMs,
                    publications,
                    seen,
                    localAliasMap,
                    objectMemberAliasMap);
            }
        }

        private static string RemoveStaticArrayIteratorCallbackBodies(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            var ranges = new List<(int Start, int End)>();
            foreach (Match match in ArrayIteratorAliasCallPattern.Matches(value))
            {
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

                int endIndex = closeIndex + 1;
                int semicolonIndex = SkipWhitespace(value, endIndex);
                if (semicolonIndex >= 0 && semicolonIndex < value.Length && value[semicolonIndex] == ';')
                {
                    endIndex = semicolonIndex + 1;
                }

                ranges.Add((match.Index, endIndex));
            }

            foreach (Match match in ArrayReduceIteratorAliasCallPattern.Matches(value))
            {
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

                int endIndex = closeIndex + 1;
                int semicolonIndex = SkipWhitespace(value, endIndex);
                if (semicolonIndex >= 0 && semicolonIndex < value.Length && value[semicolonIndex] == ';')
                {
                    endIndex = semicolonIndex + 1;
                }

                ranges.Add((match.Index, endIndex));
            }

            foreach (Match match in ForOfAliasLoopPattern.Matches(value))
            {
                int bodyStart = SkipWhitespace(value, match.Index + match.Length);
                if (bodyStart < 0 || bodyStart >= value.Length)
                {
                    continue;
                }

                int endIndex;
                if (value[bodyStart] == '{')
                {
                    int bodyEnd = FindMatchingCloseBrace(value, bodyStart);
                    if (bodyEnd <= bodyStart)
                    {
                        continue;
                    }

                    endIndex = bodyEnd + 1;
                }
                else
                {
                    int bodyEnd = value.IndexOf(';', bodyStart);
                    if (bodyEnd < bodyStart)
                    {
                        continue;
                    }

                    endIndex = bodyEnd + 1;
                }

                ranges.Add((match.Index, endIndex));
            }

            if (ranges.Count == 0)
            {
                return value;
            }

            var builder = new StringBuilder(value);
            for (int i = 0; i < ranges.Count; i++)
            {
                int start = Math.Max(0, ranges[i].Start);
                int end = Math.Min(value.Length, ranges[i].End);
                for (int index = start; index < end; index++)
                {
                    builder[index] = ' ';
                }
            }

            return builder.ToString();
        }

        private static bool TryGetStaticArrayAliasValues(
            string sourceName,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap,
            out IReadOnlyList<string> aliasValues)
        {
            aliasValues = Array.Empty<string>();
            if (!IsPotentialFunctionAliasName(sourceName)
                || objectMemberAliasMap == null
                || !objectMemberAliasMap.TryGetValue(sourceName, out IReadOnlyDictionary<string, string> sourceMemberAliasMap))
            {
                return false;
            }

            var values = new List<string>();
            foreach ((_, string aliasName) in EnumerateArrayMemberAliasesInIndexOrder(sourceMemberAliasMap))
            {
                string normalizedAlias = NormalizeFunctionAliasArgument(aliasName).TrimEnd(';');
                if (IsPotentialFunctionAliasName(normalizedAlias))
                {
                    values.Add(normalizedAlias);
                }
            }

            aliasValues = values.Count == 0 ? Array.Empty<string>() : values;
            return aliasValues.Count > 0;
        }

        private static bool TryParseIteratorCallback(
            string callback,
            out string itemName,
            out string callbackBody)
        {
            itemName = string.Empty;
            callbackBody = string.Empty;
            if (string.IsNullOrWhiteSpace(callback))
            {
                return false;
            }

            string normalizedCallback = StripOuterBalancedParentheses(callback.Trim());
            if (TryParseIteratorArrowCallback(normalizedCallback, out string parameterText, out string bodyExpression))
            {
                itemName = NormalizeFunctionAliasArgument(
                    StripOuterBalancedParentheses(parameterText?.Trim())).TrimEnd(';');
                callbackBody = StripOuterBalancedParentheses(bodyExpression?.Trim()).TrimEnd(';');
                return IsPotentialFunctionAliasName(itemName) && !string.IsNullOrWhiteSpace(callbackBody);
            }

            if (TryParseSingleParameterFunctionBodyCallback(normalizedCallback, out parameterText, out string bodyText))
            {
                itemName = NormalizeFunctionAliasArgument(parameterText).TrimEnd(';');
                callbackBody = bodyText;
                return IsPotentialFunctionAliasName(itemName) && !string.IsNullOrWhiteSpace(callbackBody);
            }

            return false;
        }

        private static bool TryParseReduceIteratorCallback(
            string callback,
            out string itemName,
            out string callbackBody)
        {
            itemName = string.Empty;
            callbackBody = string.Empty;
            if (string.IsNullOrWhiteSpace(callback))
            {
                return false;
            }

            string normalizedCallback = StripOuterBalancedParentheses(callback.Trim());
            if (TryParseArrowCallback(normalizedCallback, out string parameterText, out string bodyExpression))
            {
                IReadOnlyList<string> parameters = SplitTopLevelByComma(
                    StripOuterBalancedParentheses(parameterText?.Trim()));
                if (parameters.Count < 2)
                {
                    return false;
                }

                itemName = NormalizeFunctionAliasArgument(parameters[1]).TrimEnd(';');
                callbackBody = StripOuterBalancedParentheses(bodyExpression?.Trim()).TrimEnd(';');
                return IsPotentialFunctionAliasName(itemName) && !string.IsNullOrWhiteSpace(callbackBody);
            }

            if (TryParseTwoParameterFunctionCallback(normalizedCallback, out parameterText, out string bodyText))
            {
                IReadOnlyList<string> parameters = SplitTopLevelByComma(
                    StripOuterBalancedParentheses(parameterText?.Trim()));
                if (parameters.Count < 2)
                {
                    return false;
                }

                itemName = NormalizeFunctionAliasArgument(parameters[1]).TrimEnd(';');
                callbackBody = bodyText;
                return IsPotentialFunctionAliasName(itemName) && !string.IsNullOrWhiteSpace(callbackBody);
            }

            return false;
        }

        private static bool TryParseIteratorArrowCallback(
            string callback,
            out string parameterText,
            out string bodyText)
        {
            parameterText = string.Empty;
            bodyText = string.Empty;
            if (string.IsNullOrWhiteSpace(callback))
            {
                return false;
            }

            int arrowIndex = callback.IndexOf("=>", StringComparison.Ordinal);
            if (arrowIndex <= 0 || arrowIndex >= callback.Length - 2)
            {
                return false;
            }

            parameterText = callback[..arrowIndex].Trim();
            string body = callback[(arrowIndex + 2)..].Trim();
            if (body.StartsWith("{", StringComparison.Ordinal) && body.EndsWith("}", StringComparison.Ordinal))
            {
                bodyText = body[1..^1];
            }
            else
            {
                bodyText = body;
            }

            return !string.IsNullOrWhiteSpace(parameterText) && !string.IsNullOrWhiteSpace(bodyText);
        }

        private static bool TryParseSingleParameterFunctionBodyCallback(
            string callback,
            out string parameterText,
            out string bodyText)
        {
            parameterText = string.Empty;
            bodyText = string.Empty;
            if (string.IsNullOrWhiteSpace(callback)
                || !callback.StartsWith("function", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            int openIndex = callback.IndexOf('(');
            if (openIndex < 0)
            {
                return false;
            }

            int closeIndex = FindMatchingCloseParenthesis(callback, openIndex);
            if (closeIndex <= openIndex)
            {
                return false;
            }

            IReadOnlyList<string> parameters = SplitTopLevelByComma(callback[(openIndex + 1)..closeIndex]);
            if (parameters.Count != 1)
            {
                return false;
            }

            int bodyOpenIndex = SkipWhitespace(callback, closeIndex + 1);
            if (bodyOpenIndex < 0 || bodyOpenIndex >= callback.Length || callback[bodyOpenIndex] != '{')
            {
                return false;
            }

            int bodyCloseIndex = FindMatchingCloseBrace(callback, bodyOpenIndex);
            if (bodyCloseIndex <= bodyOpenIndex)
            {
                return false;
            }

            parameterText = parameters[0];
            bodyText = callback[(bodyOpenIndex + 1)..bodyCloseIndex];
            return !string.IsNullOrWhiteSpace(parameterText) && !string.IsNullOrWhiteSpace(bodyText);
        }

        private static void CollectIteratorBodyTimerCallbackPublications(
            string callbackBody,
            string itemName,
            IReadOnlyList<string> aliasValues,
            int inheritedDelayMs,
            ICollection<ScriptAliasPublication> publications,
            ISet<(string ScriptName, int DelayMs)> seen,
            IReadOnlyDictionary<string, string> localAliasMap,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap)
        {
            if (string.IsNullOrWhiteSpace(callbackBody)
                || string.IsNullOrWhiteSpace(itemName)
                || aliasValues == null
                || aliasValues.Count == 0)
            {
                return;
            }

            string normalizedItemName = NormalizeFunctionAliasArgument(itemName).TrimEnd(';');
            if (!IsPotentialFunctionAliasName(normalizedItemName))
            {
                return;
            }

            foreach ((string FunctionName, IReadOnlyList<string> Arguments) call in EnumerateFunctionCalls(callbackBody, includeNested: false))
            {
                if (!IsScriptCallbackFunctionName(call.FunctionName)
                    || call.Arguments.Count == 0
                    || !TryResolveCallbackCallDelayCandidates(
                        call.FunctionName,
                        call.Arguments,
                        localAliasMap,
                        out IReadOnlyList<int> callbackDelayCandidates))
                {
                    continue;
                }

                int firstDelayCandidateIndex = IsDelayedCallbackFunctionName(call.FunctionName) && call.Arguments.Count > 1
                    ? 1
                    : call.Arguments.Count;
                for (int delayIndex = 0; delayIndex < callbackDelayCandidates.Count; delayIndex++)
                {
                    int callbackDelayMs = callbackDelayCandidates[delayIndex];
                    int dueDelayMs = inheritedDelayMs >= int.MaxValue - callbackDelayMs
                        ? int.MaxValue
                        : inheritedDelayMs + callbackDelayMs;
                    for (int argumentIndex = 0; argumentIndex < call.Arguments.Count; argumentIndex++)
                    {
                        string argument = NormalizeFunctionAliasArgument(call.Arguments[argumentIndex]).TrimEnd(';');
                        if (string.IsNullOrWhiteSpace(argument)
                            || (argumentIndex >= firstDelayCandidateIndex && IsRecoverableDelayArgument(argument, localAliasMap)))
                        {
                            continue;
                        }

                        if (argument.Equals(normalizedItemName, StringComparison.OrdinalIgnoreCase))
                        {
                            for (int aliasIndex = 0; aliasIndex < aliasValues.Count; aliasIndex++)
                            {
                                AddPublication(
                                    aliasValues[aliasIndex],
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
                            if (!canonicalArgument.Equals(normalizedItemName, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            for (int aliasIndex = 0; aliasIndex < aliasValues.Count; aliasIndex++)
                            {
                                AddPublication(
                                    aliasValues[aliasIndex],
                                    dueDelayMs,
                                    publications,
                                    seen,
                                    localAliasMap,
                                    objectMemberAliasMap);
                            }
                        }
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
            IReadOnlyDictionary<string, string> localAliasMap,
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
                foreach (int parsedDelayMs in EnumeratePositiveDelayMsCandidates(argument, localAliasMap))
                {
                    delayMs = Math.Max(delayMs, parsedDelayMs);
                    foundDelay = true;
                }
            }

            return foundDelay;
        }

        private static bool TryResolveCallbackCallDelayCandidates(
            string functionName,
            IReadOnlyList<string> arguments,
            IReadOnlyDictionary<string, string> localAliasMap,
            out IReadOnlyList<int> delayCandidates)
        {
            delayCandidates = Array.Empty<int>();
            if (arguments == null || arguments.Count == 0)
            {
                return false;
            }

            if (!IsDelayedCallbackFunctionName(functionName))
            {
                if (!IsZeroDelayCallbackFunctionName(functionName))
                {
                    return false;
                }

                delayCandidates = new[] { 0 };
                return true;
            }

            if (arguments.Count == 1)
            {
                delayCandidates = new[] { 0 };
                return true;
            }

            var candidates = new List<int>();
            var seenCandidates = new HashSet<int>();
            for (int i = 1; i < arguments.Count; i++)
            {
                string argument = NormalizeFunctionAliasArgument(arguments[i]);
                foreach (int delayCandidate in EnumeratePositiveDelayMsCandidates(argument, localAliasMap))
                {
                    if (seenCandidates.Add(delayCandidate))
                    {
                        candidates.Add(delayCandidate);
                    }
                }
            }

            delayCandidates = candidates.Count == 0 ? Array.Empty<int>() : candidates;
            return candidates.Count > 0;
        }

        private static bool IsRecoverableDelayArgument(
            string value,
            IReadOnlyDictionary<string, string> localAliasMap)
        {
            foreach (int _ in EnumeratePositiveDelayMsCandidates(value, localAliasMap))
            {
                return true;
            }

            return false;
        }

        private static IEnumerable<int> EnumeratePositiveDelayMsCandidates(
            string value,
            IReadOnlyDictionary<string, string> localAliasMap)
        {
            return EnumeratePositiveDelayMsCandidates(
                value,
                localAliasMap,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        private static IEnumerable<int> EnumeratePositiveDelayMsCandidates(
            string value,
            IReadOnlyDictionary<string, string> localAliasMap,
            ISet<string> seenDelayExpressions)
        {
            string normalizedValue = NormalizeFunctionAliasArgument(value).TrimEnd(';');
            if (string.IsNullOrWhiteSpace(normalizedValue))
            {
                yield break;
            }

            if (TryResolvePositiveDelayMs(normalizedValue, localAliasMap, seenDelayExpressions, out int parsedDelayMs))
            {
                yield return parsedDelayMs;
                yield break;
            }

            foreach (string conditionalBranch in EnumerateConditionalExpressionBranches(normalizedValue))
            {
                foreach (int branchDelay in EnumeratePositiveDelayMsCandidates(
                             conditionalBranch,
                             localAliasMap,
                             seenDelayExpressions))
                {
                    yield return branchDelay;
                }
            }

            foreach (string logicalBranch in EnumerateLogicalExpressionBranches(normalizedValue))
            {
                foreach (int branchDelay in EnumeratePositiveDelayMsCandidates(
                             logicalBranch,
                             localAliasMap,
                             seenDelayExpressions))
                {
                    yield return branchDelay;
                }
            }

            foreach (string sequenceTail in EnumerateSequenceExpressionTailCandidates(normalizedValue))
            {
                foreach (int branchDelay in EnumeratePositiveDelayMsCandidates(
                             sequenceTail,
                             localAliasMap,
                             seenDelayExpressions))
                {
                    yield return branchDelay;
                }
            }
        }

        private static bool TryResolvePositiveDelayMs(
            string value,
            IReadOnlyDictionary<string, string> localAliasMap,
            out int parsedInt)
        {
            return TryResolvePositiveDelayMs(
                value,
                localAliasMap,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                out parsedInt);
        }

        private static bool TryResolvePositiveDelayMs(
            string value,
            IReadOnlyDictionary<string, string> localAliasMap,
            ISet<string> seenDelayExpressions,
            out int parsedInt)
        {
            parsedInt = 0;
            string normalizedValue = NormalizeFunctionAliasArgument(value).TrimEnd(';');
            if (string.IsNullOrWhiteSpace(normalizedValue))
            {
                return false;
            }

            if (TryParsePositiveDelayMs(normalizedValue, out parsedInt))
            {
                return true;
            }

            if (localAliasMap != null
                && localAliasMap.TryGetValue(normalizedValue, out string mappedDelay)
                && seenDelayExpressions != null
                && seenDelayExpressions.Add(normalizedValue))
            {
                try
                {
                    if (TryResolvePositiveDelayMs(mappedDelay, localAliasMap, seenDelayExpressions, out parsedInt))
                    {
                        return true;
                    }
                }
                finally
                {
                    seenDelayExpressions.Remove(normalizedValue);
                }
            }

            if (TryResolveStaticDelayFunctionExpression(
                    normalizedValue,
                    localAliasMap,
                    seenDelayExpressions,
                    out parsedInt))
            {
                return true;
            }

            return TryResolveArithmeticDelayExpression(
                normalizedValue,
                localAliasMap,
                seenDelayExpressions,
                out parsedInt);
        }

        private static bool TryResolveArithmeticDelayExpression(
            string value,
            IReadOnlyDictionary<string, string> localAliasMap,
            ISet<string> seenDelayExpressions,
            out int parsedInt)
        {
            parsedInt = 0;
            IReadOnlyList<(char Operator, string Term)> additiveTerms =
                SplitTopLevelByAdditiveOperators(StripOuterBalancedParentheses(value));
            if (additiveTerms.Count <= 1)
            {
                IReadOnlyList<(char Operator, string Factor)> factors =
                    SplitTopLevelByMultiplicativeOperators(StripOuterBalancedParentheses(value));
                if (factors.Count <= 1
                    || !TryResolveMultiplicativeDelayTerm(value, localAliasMap, seenDelayExpressions, out double multiplicativeDelay))
                {
                    return false;
                }

                parsedInt = multiplicativeDelay >= int.MaxValue
                    ? int.MaxValue
                    : checked((int)Math.Round(multiplicativeDelay, MidpointRounding.AwayFromZero));
                return parsedInt > 0;
            }

            double delayValue = 0;
            for (int i = 0; i < additiveTerms.Count; i++)
            {
                if (!TryResolveMultiplicativeDelayTerm(
                        additiveTerms[i].Term,
                        localAliasMap,
                        seenDelayExpressions,
                        out double termDelay))
                {
                    return false;
                }

                delayValue = additiveTerms[i].Operator == '-'
                    ? delayValue - termDelay
                    : delayValue + termDelay;
            }

            if (delayValue <= 0)
            {
                return false;
            }

            parsedInt = delayValue >= int.MaxValue
                ? int.MaxValue
                : checked((int)Math.Round(delayValue, MidpointRounding.AwayFromZero));
            return parsedInt > 0;
        }

        private static bool TryResolveMultiplicativeDelayTerm(
            string value,
            IReadOnlyDictionary<string, string> localAliasMap,
            ISet<string> seenDelayExpressions,
            out double delayValue)
        {
            delayValue = 0;
            IReadOnlyList<(char Operator, string Factor)> factors =
                SplitTopLevelByMultiplicativeOperators(StripOuterBalancedParentheses(value));
            if (factors.Count <= 1)
            {
                string normalizedValue = NormalizeFunctionAliasArgument(value).TrimEnd(';');
                if (!TryResolvePositiveDelayMs(normalizedValue, localAliasMap, seenDelayExpressions, out int parsedInt))
                {
                    return false;
                }

                delayValue = parsedInt;
                return true;
            }

            double product = 1;
            for (int i = 0; i < factors.Count; i++)
            {
                string factor = NormalizeFunctionAliasArgument(factors[i].Factor).TrimEnd(';');
                if (!TryResolvePositiveDelayMs(factor, localAliasMap, seenDelayExpressions, out int parsedFactor))
                {
                    return false;
                }

                if (factors[i].Operator == '/')
                {
                    if (parsedFactor == 0)
                    {
                        return false;
                    }

                    product /= parsedFactor;
                }
                else
                {
                    product *= parsedFactor;
                }
            }

            if (product <= 0)
            {
                return false;
            }

            delayValue = product;
            return true;
        }

        private static bool TryResolveStaticDelayFunctionExpression(
            string value,
            IReadOnlyDictionary<string, string> localAliasMap,
            ISet<string> seenDelayExpressions,
            out int parsedInt)
        {
            parsedInt = 0;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalizedValue = StripOuterBalancedParentheses(value.Trim());
            int openIndex = FindTrailingCallOpenParenthesis(normalizedValue);
            if (openIndex <= 0 || FindMatchingCloseParenthesis(normalizedValue, openIndex) != normalizedValue.Length - 1)
            {
                return false;
            }

            string functionName = NormalizeFunctionAliasArgument(normalizedValue[..openIndex]).TrimEnd(';');
            var arguments = new List<string>(SplitFunctionArguments(normalizedValue[(openIndex + 1)..^1]));
            if (arguments.Count == 0)
            {
                return false;
            }

            var values = new List<double>(arguments.Count);
            for (int i = 0; i < arguments.Count; i++)
            {
                if (!TryResolveStaticDelayNumericValue(
                        arguments[i],
                        localAliasMap,
                        seenDelayExpressions,
                        out double argumentValue))
                {
                    return false;
                }

                values.Add(argumentValue);
            }

            double resolvedValue;
            switch (functionName)
            {
                case "Number":
                    if (values.Count != 1)
                    {
                        return false;
                    }

                    resolvedValue = values[0];
                    break;
                case "parseInt":
                case "parseFloat":
                    if (values.Count is < 1 or > 2)
                    {
                        return false;
                    }

                    resolvedValue = values[0];
                    break;
                case "Math.max":
                    resolvedValue = values[0];
                    for (int i = 1; i < values.Count; i++)
                    {
                        resolvedValue = Math.Max(resolvedValue, values[i]);
                    }

                    break;
                case "Math.min":
                    resolvedValue = values[0];
                    for (int i = 1; i < values.Count; i++)
                    {
                        resolvedValue = Math.Min(resolvedValue, values[i]);
                    }

                    break;
                case "Math.round":
                    if (values.Count != 1)
                    {
                        return false;
                    }

                    resolvedValue = Math.Round(values[0], MidpointRounding.AwayFromZero);
                    break;
                case "Math.floor":
                    if (values.Count != 1)
                    {
                        return false;
                    }

                    resolvedValue = Math.Floor(values[0]);
                    break;
                case "Math.ceil":
                    if (values.Count != 1)
                    {
                        return false;
                    }

                    resolvedValue = Math.Ceiling(values[0]);
                    break;
                case "Math.trunc":
                    if (values.Count != 1)
                    {
                        return false;
                    }

                    resolvedValue = Math.Truncate(values[0]);
                    break;
                default:
                    return false;
            }

            if (resolvedValue <= 0)
            {
                return false;
            }

            parsedInt = resolvedValue >= int.MaxValue
                ? int.MaxValue
                : checked((int)Math.Round(resolvedValue, MidpointRounding.AwayFromZero));
            return parsedInt > 0;
        }

        private static bool TryResolveStaticDelayNumericValue(
            string value,
            IReadOnlyDictionary<string, string> localAliasMap,
            ISet<string> seenDelayExpressions,
            out double numericValue)
        {
            numericValue = 0;
            string normalizedValue = NormalizeFunctionAliasArgument(value).TrimEnd(';');
            if (string.IsNullOrWhiteSpace(normalizedValue))
            {
                return false;
            }

            if (double.TryParse(
                    normalizedValue,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double parsedDouble)
                && parsedDouble >= 0)
            {
                numericValue = parsedDouble;
                return true;
            }

            if (TryResolvePositiveDelayMs(normalizedValue, localAliasMap, seenDelayExpressions, out int parsedInt))
            {
                numericValue = parsedInt;
                return true;
            }

            return false;
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

        private static bool TryResolveArrayMethodObjectAliasCandidate(
            string value,
            IReadOnlyDictionary<string, string> localAliasMap,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> objectMemberAliasMap,
            out string aliasName)
        {
            aliasName = string.Empty;
            if (string.IsNullOrWhiteSpace(value)
                || objectMemberAliasMap == null
                || objectMemberAliasMap.Count == 0)
            {
                return false;
            }

            string normalizedValue = NormalizeFunctionAliasArgument(value).TrimEnd(';');
            int openIndex = FindTrailingCallOpenParenthesis(normalizedValue);
            if (openIndex <= 0 || FindMatchingCloseParenthesis(normalizedValue, openIndex) != normalizedValue.Length - 1)
            {
                return false;
            }

            string callPrefix = normalizedValue[..openIndex].TrimEnd();
            int separatorIndex = callPrefix.LastIndexOf('.');
            if (separatorIndex <= 0 || separatorIndex >= callPrefix.Length - 1)
            {
                return false;
            }

            string objectName = NormalizeFunctionAliasArgument(callPrefix[..separatorIndex]).TrimEnd(';');
            string methodName = NormalizeFunctionAliasArgument(callPrefix[(separatorIndex + 1)..]).TrimEnd(';');
            if (!IsPotentialFunctionAliasName(objectName)
                || !objectMemberAliasMap.TryGetValue(objectName, out IReadOnlyDictionary<string, string> memberAliasMap)
                || memberAliasMap == null
                || memberAliasMap.Count == 0)
            {
                return false;
            }

            string argumentText = normalizedValue[(openIndex + 1)..^1];
            string memberKey;
            if (methodName.Equals("at", StringComparison.OrdinalIgnoreCase))
            {
                var arguments = new List<string>(SplitFunctionArguments(argumentText));
                if (arguments.Count != 1
                    || !TryResolveBracketIndexKey(arguments[0], localAliasMap, out memberKey))
                {
                    return false;
                }

                if (int.TryParse(memberKey, out int parsedIndex) && parsedIndex < 0)
                {
                    int? lastIndex = TryGetLastArrayMemberIndex(memberAliasMap);
                    if (!lastIndex.HasValue)
                    {
                        return false;
                    }

                    memberKey = (lastIndex.Value + parsedIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }
            else if (methodName.Equals("pop", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(argumentText))
                {
                    return false;
                }

                int? lastIndex = TryGetLastArrayMemberIndex(memberAliasMap);
                if (!lastIndex.HasValue)
                {
                    return false;
                }

                memberKey = lastIndex.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (methodName.Equals("shift", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(argumentText))
                {
                    return false;
                }

                int? firstIndex = TryGetFirstArrayMemberIndex(memberAliasMap);
                if (!firstIndex.HasValue)
                {
                    return false;
                }

                memberKey = firstIndex.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                return false;
            }

            return TryResolveObjectMemberAlias(objectName, memberKey, objectMemberAliasMap, out aliasName);
        }

        private static int? TryGetFirstArrayMemberIndex(IEnumerable<KeyValuePair<string, string>> memberAliasMap)
        {
            if (memberAliasMap == null)
            {
                return null;
            }

            int? firstIndex = null;
            foreach (KeyValuePair<string, string> memberAlias in memberAliasMap)
            {
                if (!int.TryParse(memberAlias.Key, out int parsedIndex) || parsedIndex < 0)
                {
                    continue;
                }

                if (!firstIndex.HasValue || parsedIndex < firstIndex.Value)
                {
                    firstIndex = parsedIndex;
                }
            }

            return firstIndex;
        }

        private static int? TryGetLastArrayMemberIndex(IEnumerable<KeyValuePair<string, string>> memberAliasMap)
        {
            if (memberAliasMap == null)
            {
                return null;
            }

            int? lastIndex = null;
            foreach (KeyValuePair<string, string> memberAlias in memberAliasMap)
            {
                if (!int.TryParse(memberAlias.Key, out int parsedIndex) || parsedIndex < 0)
                {
                    continue;
                }

                if (!lastIndex.HasValue || parsedIndex > lastIndex.Value)
                {
                    lastIndex = parsedIndex;
                }
            }

            return lastIndex;
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

        private static bool TryResolveObjectNoArgumentCallAliasCandidate(
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

            string normalizedValue = NormalizeFunctionAliasArgument(value).TrimEnd(';');
            if (!normalizedValue.EndsWith(")", StringComparison.Ordinal))
            {
                return false;
            }

            int openIndex = FindTrailingCallOpenParenthesis(normalizedValue);
            if (openIndex <= 0 || FindMatchingCloseParenthesis(normalizedValue, openIndex) != normalizedValue.Length - 1)
            {
                return false;
            }

            string argumentText = normalizedValue[(openIndex + 1)..^1];
            if (!string.IsNullOrWhiteSpace(argumentText))
            {
                return false;
            }

            string calleeExpression = NormalizeOptionalChainingAliasAccess(normalizedValue[..openIndex].Trim());
            if (TryParseDottedObjectAccess(calleeExpression, out string objectName, out string memberKey))
            {
                return TryResolveObjectMemberAlias(objectName, memberKey, objectMemberAliasMap, out aliasName);
            }

            if (!TryParseIndexedObjectAccess(calleeExpression, out objectName, out string indexExpression)
                || !TryResolveBracketIndexKey(indexExpression, localAliasMap, out memberKey))
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

            if (TryResolveStaticBracketExpressionAlias(normalizedIndexExpression, localAliasMap, out string staticKey))
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
