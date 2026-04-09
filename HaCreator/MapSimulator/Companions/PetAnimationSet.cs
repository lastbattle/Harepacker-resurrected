using HaCreator.MapSimulator.Animation;
using HaSharedLibrary.Render.DX;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Companions
{
    internal static class PetActionAliases
    {
        private static readonly string[] KnownActions =
        {
            "stand0",
            "stand1",
            "stand",
            "move",
            "walk",
            "jump",
            "hang",
            "fly",
            "rest0",
            "rest1",
            "rest",
            "nap",
            "sleep",
            "chat",
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
            "angry_short",
            "hands",
            "what",
            "good",
            "goodboy",
            "happy",
            "surprise",
            "bewildered",
            "complain",
            "donno",
            "yes",
            "smile",
            "sneer",
            "hug",
            "front",
            "lonely",
            "jumpfly",
            "change",
            "transform",
            "warp"
        };

        private static readonly IReadOnlyDictionary<string, string[]> LookupCandidates =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["stand0"] = new[] { "stand0", "stand", "stand1" },
                ["stand1"] = new[] { "stand1", "stand0", "stand" },
                ["stand"] = new[] { "stand", "stand1", "stand0" },
                ["move"] = new[] { "move", "walk" },
                ["walk"] = new[] { "walk", "move" },
                ["jump"] = new[] { "jump", "jumpfly", "fly" },
                ["hang"] = new[] { "hang" },
                ["fly"] = new[] { "fly", "jumpfly", "jump" },
                ["jumpfly"] = new[] { "jumpfly", "fly", "jump" },
                ["rest0"] = new[] { "rest0", "rest1", "rest", "nap", "sleep", "prone" },
                ["rest1"] = new[] { "rest1", "rest0", "rest", "nap", "sleep", "prone" },
                ["rest"] = new[] { "rest", "rest0", "rest1", "nap", "sleep", "prone" },
                ["nap"] = new[] { "nap", "sleep", "prone", "rest0", "rest1", "rest" },
                ["sleep"] = new[] { "sleep", "nap", "prone", "rest0", "rest1", "rest" },
                ["chat"] = new[] { "chat", "say" },
                ["say"] = new[] { "say", "chat" },
                ["angry"] = new[] { "angry", "no", "tedious" },
                ["angry_short"] = new[] { "angry_short", "angry", "no", "tedious" },
                ["no"] = new[] { "no", "angry", "cry", "tedious", "stunned" },
                ["yes"] = new[] { "yes", "smile", "love", "chat" },
                ["tedious"] = new[] { "tedious", "angry", "no" },
                ["cry"] = new[] { "cry", "stunned", "no" },
                ["stunned"] = new[] { "stunned", "cry", "no" },
                ["alert"] = new[] { "alert", "eye", "birdeye", "hand" },
                ["eye"] = new[] { "eye", "birdeye", "alert", "hand" },
                ["birdeye"] = new[] { "birdeye", "eye", "alert", "hand" },
                ["hand"] = new[] { "hand", "hands", "alert" },
                ["hands"] = new[] { "hands", "hand", "alert" },
                ["stretch"] = new[] { "stretch", "love" },
                ["love"] = new[] { "love", "stretch" },
                ["prone"] = new[] { "prone", "nap", "sleep", "rest0", "rest1", "rest" },
                ["hungry"] = new[] { "hungry", "imhungry" },
                ["imhungry"] = new[] { "imhungry", "hungry" },
                ["poor"] = new[] { "poor", "dung" },
                ["dung"] = new[] { "dung", "poor" },
                ["rise"] = new[] { "rise" },
                ["eat"] = new[] { "eat" },
                ["play"] = new[] { "play", "mischief", "love" },
                ["mischief"] = new[] { "mischief", "play", "melong", "merong", "love" },
                ["scratch"] = new[] { "scratch", "play", "love" },
                ["sit"] = new[] { "sit", "rest0", "rest1", "rest", "prone" },
                ["melong"] = new[] { "melong", "merong", "play", "love" },
                ["merong"] = new[] { "merong", "melong", "play", "love" },
                ["sulk"] = new[] { "sulk", "ignore", "puling", "nothing", "angry", "no", "tedious" },
                ["ignore"] = new[] { "ignore", "sulk", "nothing", "puling", "no", "angry" },
                ["nothing"] = new[] { "nothing", "ignore", "sulk", "puling", "bewildered" },
                ["puling"] = new[] { "puling", "sulk", "ignore", "nothing", "cry", "no" },
                ["what"] = new[] { "what", "surprise", "bewildered", "donno", "complain" },
                ["good"] = new[] { "good", "goodboy", "happy", "yes", "smile", "love" },
                ["goodboy"] = new[] { "goodboy", "good", "happy", "yes", "smile", "love" },
                ["happy"] = new[] { "happy", "good", "goodboy", "yes", "smile", "love" },
                ["surprise"] = new[] { "surprise", "what", "bewildered", "alert" },
                ["bewildered"] = new[] { "bewildered", "what", "surprise", "donno" },
                ["complain"] = new[] { "complain", "donno", "what", "cry" },
                ["donno"] = new[] { "donno", "complain", "what", "bewildered" },
                ["smile"] = new[] { "smile", "yes", "love", "chat" },
                ["sneer"] = new[] { "sneer", "angry", "cry", "no" },
                ["hug"] = new[] { "hug", "love" },
                ["front"] = new[] { "front", "stand0", "stand1", "stand" },
                ["lonely"] = new[] { "lonely", "cry", "poor", "sulk" },
                ["change"] = new[] { "change", "transform", "warp" },
                ["transform"] = new[] { "transform", "change", "warp" },
                ["warp"] = new[] { "warp", "change", "transform" }
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
            return KnownActions;
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
