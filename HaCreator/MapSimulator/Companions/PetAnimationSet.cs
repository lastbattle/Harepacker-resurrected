using HaCreator.MapSimulator.Animation;
using HaSharedLibrary.Render.DX;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Companions
{
    internal static class PetActionAliases
    {
        // CActionMan::LoadPetAction uses these fixed s_sPetAction slots for nAction 0..8
        // before falling back to CPetTemplate::GetActionName for template-specific actions.
        // The order is decoded from the client initializer at 0xafa510.
        private static readonly string[] ClientBaseActionNames =
        {
            "move",
            "stand0",
            "stand1",
            "jump",
            "fly",
            "hungry",
            "rest0",
            "rest1",
            "hang"
        };

        private static readonly string[] SupplementalKnownActions =
        {
            "stand",
            "walk",
            "stand2",
            "stand3",
            "stand4",
            "stand5",
            "stand6",
            "move1",
            "move2",
            "chat",
            "rest1",
            "rest",
            "nap",
            "sleep",
            "say",
            "angry",
            "no",
            "tedious",
            "cry",
            "stunned",
            "alert",
            "eye",
            "birdeye",
            "hand",
            "stretch",
            "love",
            "prone",
            "hungry",
            "imhungry",
            "poor",
            "dung",
            "rise",
            "eat",
            "play",
            "mischief",
            "scratch",
            "sit",
            "melong",
            "merong",
            "sulk",
            "ignore",
            "nothing",
            "puling",
            "pulling",
            "angry_short",
            "angry2",
            "hands",
            "what",
            "question",
            "good",
            "goodboy",
            "happy",
            "surprise",
            "bewildered",
            "complain",
            "donno",
            "dunno",
            "dontknow",
            "warn",
            "start",
            "yes",
            "smile",
            "song",
            "dance",
            "sneer",
            "hug",
            "front",
            "lonely",
            "charming",
            "glitter",
            "cute",
            "christmas",
            "fart",
            "vomit",
            "jumpfly",
            "change",
            "transform",
            "warp",
            "transformaction",
            "ok",
            "confuse",
            "irrelevant",
            "shock",
            "roll",
            "panic",
            "fake",
            "fire",
            "hul",
            "poop",
            "flyr",
            "swim",
            "buff",
            "heal",
            "lat",
            "late",
            "me",
            "tundere",
            "tsundere",
            "sad",
            "kiss",
            "shout",
            "joy",
            "blush",
            "upset",
            "hide",
            "sigh",
            "proud",
            "burp"
        };

        private static readonly string[] KnownActions = ClientBaseActionNames
            .Concat(SupplementalKnownActions)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        private static readonly IReadOnlyDictionary<string, string[]> LookupCandidates =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["stand0"] = new[] { "stand0", "stand", "stand1" },
                ["stand1"] = new[] { "stand1", "stand0", "stand" },
                ["stand2"] = new[] { "stand2", "stand1", "stand0", "stand" },
                ["stand3"] = new[] { "stand3", "stand2", "stand1", "stand0", "stand" },
                ["stand4"] = new[] { "stand4", "stand3", "stand2", "stand1", "stand0", "stand" },
                ["stand5"] = new[] { "stand5", "stand4", "stand3", "stand2", "stand1", "stand0", "stand" },
                ["stand6"] = new[] { "stand6", "stand5", "stand4", "stand3", "stand2", "stand1", "stand0", "stand" },
                ["stand"] = new[] { "stand", "stand1", "stand0" },
                ["move"] = new[] { "move", "move1", "move2", "walk" },
                ["move1"] = new[] { "move1", "move2", "move", "walk" },
                ["move2"] = new[] { "move2", "move1", "move", "walk" },
                ["walk"] = new[] { "walk", "move", "move1", "move2" },
                ["jump"] = new[] { "jump", "jumpfly", "fly" },
                ["hang"] = new[] { "hang" },
                ["fly"] = new[] { "fly", "jumpfly", "jump", "swim" },
                ["flyr"] = new[] { "flyr", "fly", "jumpfly", "jump", "swim" },
                ["jumpfly"] = new[] { "jumpfly", "fly", "flyr", "jump", "swim" },
                ["swim"] = new[] { "swim", "fly", "jumpfly", "move" },
                ["rest0"] = new[] { "rest0", "rest1", "rest", "nap", "sleep", "prone" },
                ["rest1"] = new[] { "rest1", "rest0", "rest", "nap", "sleep", "prone" },
                ["rest"] = new[] { "rest", "rest0", "rest1", "nap", "sleep", "prone" },
                ["nap"] = new[] { "nap", "sleep", "prone", "rest0", "rest1", "rest" },
                ["sleep"] = new[] { "sleep", "nap", "prone", "rest0", "rest1", "rest" },
                ["lat"] = new[] { "lat", "late", "sleep", "nap", "rest0", "rest1", "rest" },
                ["late"] = new[] { "late", "lat", "sleep", "nap", "rest0", "rest1", "rest" },
                ["chat"] = new[] { "chat", "say" },
                ["say"] = new[] { "say", "chat" },
                ["angry"] = new[] { "angry", "no", "tedious" },
                ["angry_short"] = new[] { "angry_short", "angry", "no", "tedious" },
                ["angry2"] = new[] { "angry2", "angry_short", "angry", "no", "tedious" },
                ["no"] = new[] { "no", "angry", "cry", "tedious", "stunned" },
                ["yes"] = new[] { "yes", "ok", "smile", "love", "chat" },
                ["ok"] = new[] { "ok", "yes", "good", "goodboy", "happy", "smile", "love", "chat" },
                ["tedious"] = new[] { "tedious", "angry", "no" },
                ["cry"] = new[] { "cry", "stunned", "no" },
                ["sad"] = new[] { "sad", "cry", "lonely", "poor", "sulk", "no" },
                ["sigh"] = new[] { "sigh", "sad", "cry", "sulk", "poor", "no" },
                ["stunned"] = new[] { "stunned", "cry", "no" },
                ["alert"] = new[] { "alert", "eye", "birdeye", "hand" },
                ["eye"] = new[] { "eye", "birdeye", "alert", "hand" },
                ["birdeye"] = new[] { "birdeye", "eye", "alert", "hand" },
                ["warn"] = new[] { "warn", "alert", "eye", "birdeye", "hand" },
                ["hand"] = new[] { "hand", "hands", "alert" },
                ["hands"] = new[] { "hands", "hand", "alert" },
                ["stretch"] = new[] { "stretch", "love" },
                ["love"] = new[] { "love", "stretch" },
                ["prone"] = new[] { "prone", "nap", "sleep", "rest0", "rest1", "rest" },
                ["hungry"] = new[] { "hungry", "imhungry" },
                ["imhungry"] = new[] { "imhungry", "hungry" },
                ["poor"] = new[] { "poor", "dung", "poop" },
                ["dung"] = new[] { "dung", "poop", "poor" },
                ["poop"] = new[] { "poop", "dung", "poor" },
                ["rise"] = new[] { "rise" },
                ["eat"] = new[] { "eat" },
                ["play"] = new[] { "play", "mischief", "love" },
                ["mischief"] = new[] { "mischief", "play", "melong", "merong", "love" },
                ["scratch"] = new[] { "scratch", "play", "love" },
                ["sit"] = new[] { "sit", "rest0", "rest1", "rest", "prone" },
                ["melong"] = new[] { "melong", "merong", "play", "love" },
                ["merong"] = new[] { "merong", "melong", "play", "love" },
                ["sulk"] = new[] { "sulk", "ignore", "nothing", "puling", "hide", "upset", "irrelevant", "angry", "no", "tedious" },
                ["upset"] = new[] { "upset", "sulk", "hide", "irrelevant", "ignore", "nothing", "puling", "angry", "no", "tedious", "sad" },
                ["ignore"] = new[] { "ignore", "irrelevant", "hide", "nothing", "sulk", "puling", "upset", "no", "angry" },
                ["nothing"] = new[] { "nothing", "ignore", "irrelevant", "hide", "sulk", "puling", "upset", "bewildered" },
                ["irrelevant"] = new[] { "irrelevant", "ignore", "hide", "upset", "nothing", "sulk", "puling", "bewildered" },
                ["hide"] = new[] { "hide", "irrelevant", "ignore", "upset", "nothing", "sulk", "puling", "bewildered" },
                ["puling"] = new[] { "puling", "pulling", "sulk", "ignore", "irrelevant", "hide", "nothing", "upset", "cry", "no" },
                ["pulling"] = new[] { "pulling", "puling", "sulk", "ignore", "irrelevant", "hide", "nothing", "upset", "cry", "no" },
                ["shout"] = new[] { "shout", "chat", "say", "alert", "hand" },
                ["what"] = new[] { "what", "question", "confuse", "surprise", "bewildered", "donno", "complain" },
                ["question"] = new[] { "question", "what", "confuse", "surprise", "bewildered", "donno", "complain" },
                ["confuse"] = new[] { "confuse", "bewildered", "what", "surprise", "donno", "nothing" },
                ["panic"] = new[] { "panic", "surprise", "what", "question", "confuse", "bewildered", "alert" },
                ["shock"] = new[] { "shock", "surprise", "panic", "stunned", "alert" },
                ["good"] = new[] { "good", "goodboy", "happy", "ok", "yes", "smile", "love" },
                ["goodboy"] = new[] { "goodboy", "good", "happy", "ok", "yes", "smile", "love" },
                ["happy"] = new[] { "happy", "good", "goodboy", "ok", "yes", "smile", "love" },
                ["joy"] = new[] { "joy", "happy", "good", "goodboy", "ok", "yes", "smile", "love" },
                ["proud"] = new[] { "proud", "good", "goodboy", "happy", "joy", "ok", "yes", "smile", "love" },
                ["surprise"] = new[] { "surprise", "confuse", "what", "bewildered", "alert" },
                ["bewildered"] = new[] { "bewildered", "confuse", "what", "surprise", "donno" },
                ["complain"] = new[] { "complain", "donno", "what", "confuse", "cry" },
                ["donno"] = new[] { "donno", "complain", "confuse", "what", "bewildered" },
                ["dunno"] = new[] { "dunno", "donno", "complain", "confuse", "what", "bewildered" },
                ["dontknow"] = new[] { "dontknow", "donno", "dunno", "complain", "confuse", "what", "bewildered" },
                ["smile"] = new[] { "smile", "ok", "yes", "love", "chat" },
                ["blush"] = new[] { "blush", "smile", "love", "cute", "happy" },
                ["song"] = new[] { "song", "chat", "say", "happy", "love" },
                ["dance"] = new[] { "dance", "play", "charming", "glitter", "happy", "love" },
                ["sneer"] = new[] { "sneer", "angry", "cry", "no" },
                ["hug"] = new[] { "hug", "love" },
                ["kiss"] = new[] { "kiss", "love", "hug", "cute", "smile" },
                ["front"] = new[] { "front", "stand0", "stand1", "stand" },
                ["lonely"] = new[] { "lonely", "cry", "poor", "sulk" },
                ["me"] = new[] { "me", "front", "chat", "love" },
                ["tundere"] = new[] { "tundere", "tsundere", "sulk", "ignore", "angry", "no" },
                ["tsundere"] = new[] { "tsundere", "tundere", "sulk", "ignore", "angry", "no" },
                ["charming"] = new[] { "charming", "dance", "cute", "love", "happy" },
                ["glitter"] = new[] { "glitter", "dance", "christmas", "love" },
                ["cute"] = new[] { "cute", "love", "happy", "smile", "charming" },
                ["christmas"] = new[] { "christmas", "glitter", "love", "happy", "cute" },
                ["fart"] = new[] { "fart" },
                ["vomit"] = new[] { "vomit", "burp", "eat", "dung", "poop" },
                ["burp"] = new[] { "burp", "vomit", "eat", "dung", "poop" },
                ["roll"] = new[] { "roll", "play", "jump", "move" },
                ["fake"] = new[] { "fake", "panic", "surprise", "confuse", "nothing" },
                ["fire"] = new[] { "fire", "alert", "panic", "angry" },
                ["hul"] = new[] { "hul", "surprise", "panic", "stunned", "alert" },
                ["buff"] = new[] { "buff", "heal", "transform", "transformaction" },
                ["heal"] = new[] { "heal", "buff", "transform", "transformaction" },
                ["start"] = new[] { "start", "rise", "stand0", "stand1", "stand" },
                ["change"] = new[] { "change", "transform", "transformaction", "warp" },
                ["transform"] = new[] { "transform", "transformaction", "change", "warp" },
                ["transformaction"] = new[] { "transformaction", "transform", "change", "warp" },
                ["warp"] = new[] { "warp", "transform", "transformaction", "change" }
            };

        private static readonly IReadOnlyDictionary<string, string> CanonicalLookupKeys = LookupCandidates.Keys
            .Select(static key => new KeyValuePair<string, string>(NormalizeActionName(key), key))
            .Where(static pair => !string.IsNullOrWhiteSpace(pair.Key))
            .GroupBy(static pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.First().Value,
                StringComparer.Ordinal);

        private static readonly IReadOnlyDictionary<string, string[]> ReverseLookupCandidates = BuildReverseLookupCandidates();

        internal static IEnumerable<string> EnumerateCandidates(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                yield break;
            }

            string lookupActionName = null;
            if (LookupCandidates.ContainsKey(actionName))
            {
                lookupActionName = actionName;
            }
            else
            {
                string normalizedActionName = NormalizeActionName(actionName);
                if (!string.IsNullOrWhiteSpace(normalizedActionName) &&
                    CanonicalLookupKeys.TryGetValue(normalizedActionName, out string canonicalActionName) &&
                    !string.Equals(canonicalActionName, actionName, StringComparison.OrdinalIgnoreCase))
                {
                    lookupActionName = canonicalActionName;
                }
            }

            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(lookupActionName) &&
                LookupCandidates.TryGetValue(lookupActionName, out string[] candidates))
            {
                foreach (string candidate in EnumerateAliasCandidates(lookupActionName, candidates, yielded))
                {
                    yield return candidate;
                }

                if (!string.Equals(lookupActionName, actionName, StringComparison.OrdinalIgnoreCase) &&
                    yielded.Add(actionName))
                {
                    yield return actionName;
                }
            }
            else if (yielded.Add(actionName))
            {
                yield return actionName;
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(lookupActionName) &&
                ReverseLookupCandidates.TryGetValue(lookupActionName, out string[] reverseCandidates))
            {
                foreach (string reverseCandidate in reverseCandidates)
                {
                    if (yielded.Add(reverseCandidate))
                    {
                        yield return reverseCandidate;
                    }
                }
            }
        }

        private static IEnumerable<string> EnumerateAliasCandidates(
            string actionName,
            IEnumerable<string> candidates,
            HashSet<string> yielded)
        {
            foreach (string candidate in candidates ?? Array.Empty<string>())
            {
                if (yielded.Add(candidate))
                {
                    yield return candidate;
                }
            }

            if (ReverseLookupCandidates.TryGetValue(actionName, out string[] reverseCandidates))
            {
                foreach (string reverseCandidate in reverseCandidates)
                {
                    if (yielded.Add(reverseCandidate))
                    {
                        yield return reverseCandidate;
                    }
                }
            }
        }

        private static IReadOnlyDictionary<string, string[]> BuildReverseLookupCandidates()
        {
            var reverseLookupCandidates = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach ((string actionName, string[] candidates) in LookupCandidates)
            {
                if (string.IsNullOrWhiteSpace(actionName) || candidates == null)
                {
                    continue;
                }

                foreach (string candidate in candidates)
                {
                    if (string.IsNullOrWhiteSpace(candidate) ||
                        string.Equals(candidate, actionName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!reverseLookupCandidates.TryGetValue(candidate, out List<string> reverseCandidates))
                    {
                        reverseCandidates = new List<string>();
                        reverseLookupCandidates[candidate] = reverseCandidates;
                    }

                    if (!reverseCandidates.Contains(actionName, StringComparer.OrdinalIgnoreCase))
                    {
                        reverseCandidates.Add(actionName);
                    }
                }
            }

            return reverseLookupCandidates.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToArray(),
                StringComparer.OrdinalIgnoreCase);
        }

        internal static IEnumerable<string> EnumerateReverseCandidatesForTesting(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName) ||
                !ReverseLookupCandidates.TryGetValue(actionName, out string[] reverseCandidates))
            {
                yield break;
            }

            for (int i = 0; i < reverseCandidates.Length; i++)
            {
                yield return reverseCandidates[i];
            }
        }

        internal static IEnumerable<string> EnumerateKnownActions()
        {
            return KnownActions;
        }

        internal static IEnumerable<string> EnumerateClientBaseActions()
        {
            return ClientBaseActionNames;
        }

        internal static string NormalizeActionName(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return string.Empty;
            }

            return new string(actionName
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
        }

        internal static string NormalizeActionStem(string actionName)
        {
            string normalizedActionName = NormalizeActionName(actionName);
            if (string.IsNullOrWhiteSpace(normalizedActionName))
            {
                return string.Empty;
            }

            int stemLength = normalizedActionName.Length;
            while (stemLength > 0 && char.IsDigit(normalizedActionName[stemLength - 1]))
            {
                stemLength--;
            }

            return stemLength > 0
                ? normalizedActionName.Substring(0, stemLength)
                : normalizedActionName;
        }
    }

    internal sealed class PetAnimationSet : AnimationSetBase
    {
        internal void AddMissingAliasAnimations()
        {
            foreach (string actionName in PetActionAliases.EnumerateKnownActions().Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (HasAnimation(actionName))
                {
                    continue;
                }

                List<IDXObject> frames = PetActionAliases.EnumerateCandidates(actionName)
                    .Where(candidate => !string.Equals(candidate, actionName, StringComparison.OrdinalIgnoreCase))
                    .Select(candidate => _animations.TryGetValue(candidate.ToLowerInvariant(), out List<IDXObject> candidateFrames)
                        ? candidateFrames
                        : null)
                    .FirstOrDefault(candidateFrames => candidateFrames?.Count > 0);
                if (frames != null)
                {
                    AddAnimation(actionName, frames);
                }
            }
        }

        protected override bool TryGetFallbackFrames(string requestedAction, out List<IDXObject> frames)
        {
            if (requestedAction == "idle" &&
                (TryGetFrames("stand1", out frames) ||
                 TryGetFrames("stand0", out frames) ||
                 TryGetFrames("stand", out frames)))
            {
                return true;
            }

            foreach (string candidate in PetActionAliases.EnumerateCandidates(requestedAction))
            {
                if (string.Equals(candidate, requestedAction, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (TryGetFrames(candidate, out frames))
                {
                    return true;
                }
            }

            if (TryGetNormalizedFrames(requestedAction, out frames))
            {
                return true;
            }

            if (TryGetNormalizedStemFrames(requestedAction, out frames))
            {
                return true;
            }

            return TryGetFrames("stand1", out frames) || TryGetFrames("stand0", out frames);
        }

        private bool TryGetFrames(string action, out List<IDXObject> frames)
        {
            frames = null;
            return action != null && _animations.TryGetValue(action, out frames) && frames?.Count > 0;
        }

        private bool TryGetNormalizedFrames(string requestedAction, out List<IDXObject> frames)
        {
            frames = null;

            string normalizedRequestedAction = PetActionAliases.NormalizeActionName(requestedAction);
            if (string.IsNullOrWhiteSpace(normalizedRequestedAction))
            {
                return false;
            }

            foreach ((string actionName, List<IDXObject> candidateFrames) in _animations)
            {
                if (candidateFrames?.Count <= 0)
                {
                    continue;
                }

                if (!string.Equals(
                        PetActionAliases.NormalizeActionName(actionName),
                        normalizedRequestedAction,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                frames = candidateFrames;
                return true;
            }

            return false;
        }

        private bool TryGetNormalizedStemFrames(string requestedAction, out List<IDXObject> frames)
        {
            frames = null;

            string normalizedRequestedStem = PetActionAliases.NormalizeActionStem(requestedAction);
            if (string.IsNullOrWhiteSpace(normalizedRequestedStem))
            {
                return false;
            }

            foreach ((string actionName, List<IDXObject> candidateFrames) in _animations)
            {
                if (candidateFrames?.Count <= 0)
                {
                    continue;
                }

                if (!string.Equals(
                        PetActionAliases.NormalizeActionStem(actionName),
                        normalizedRequestedStem,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                frames = candidateFrames;
                return true;
            }

            return false;
        }
    }
}
