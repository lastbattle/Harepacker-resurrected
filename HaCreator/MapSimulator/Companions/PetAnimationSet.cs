using HaCreator.MapSimulator.Animation;
using HaSharedLibrary.Render.DX;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Companions
{
    internal static class PetActionAliases
    {
        private static readonly IReadOnlyDictionary<string, string[]> LookupCandidates =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["stand0"] = new[] { "stand0", "stand", "stand1" },
                ["stand1"] = new[] { "stand1", "stand0", "stand" },
                ["stand"] = new[] { "stand", "stand1", "stand0" },
                ["move"] = new[] { "move", "walk" },
                ["walk"] = new[] { "walk", "move" },
                ["jump"] = new[] { "jump", "fly" },
                ["hang"] = new[] { "hang" },
                ["fly"] = new[] { "fly", "jump" },
                ["rest0"] = new[] { "rest0", "rest", "nap", "prone" },
                ["rest"] = new[] { "rest", "rest0", "nap", "prone" },
                ["nap"] = new[] { "nap", "prone", "rest0", "rest" },
                ["chat"] = new[] { "chat", "say" },
                ["say"] = new[] { "say", "chat" },
                ["angry"] = new[] { "angry", "no", "tedious" },
                ["no"] = new[] { "no", "angry", "cry", "tedious", "stunned" },
                ["tedious"] = new[] { "tedious", "angry", "no" },
                ["cry"] = new[] { "cry", "stunned", "no" },
                ["stunned"] = new[] { "stunned", "cry", "no" },
                ["alert"] = new[] { "alert", "hand" },
                ["hand"] = new[] { "hand", "alert" },
                ["stretch"] = new[] { "stretch", "love" },
                ["love"] = new[] { "love", "stretch" },
                ["prone"] = new[] { "prone", "nap", "rest0", "rest" },
                ["hungry"] = new[] { "hungry" },
                ["poor"] = new[] { "poor", "dung" },
                ["dung"] = new[] { "dung", "poor" },
                ["rise"] = new[] { "rise" }
            };

        internal static IEnumerable<string> EnumerateCandidates(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                yield break;
            }

            if (!LookupCandidates.TryGetValue(actionName, out string[] candidates))
            {
                yield return actionName;
                yield break;
            }

            for (int i = 0; i < candidates.Length; i++)
            {
                yield return candidates[i];
            }
        }

        internal static IEnumerable<string> EnumerateKnownActions()
        {
            return LookupCandidates.Keys;
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

            return TryGetFrames("stand1", out frames) || TryGetFrames("stand0", out frames);
        }

        private bool TryGetFrames(string action, out List<IDXObject> frames)
        {
            frames = null;
            return action != null && _animations.TryGetValue(action, out frames) && frames?.Count > 0;
        }
    }
}
