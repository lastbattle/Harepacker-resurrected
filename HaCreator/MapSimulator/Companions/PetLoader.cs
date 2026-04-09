using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Render;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Companions
{
    public sealed class PetDefinition
    {
        internal const string ClientMultiPetHangActionName = "hangMulti";

        public int ItemId { get; init; }
        public string Name { get; init; }
        public int ChatBalloonStyle { get; init; }
        public IDXObject Icon { get; init; }
        public IDXObject IconRaw { get; init; }
        internal IReadOnlyDictionary<PetAutoSpeechEvent, string[]> EventSpeechLines { get; init; } =
            new Dictionary<PetAutoSpeechEvent, string[]>();
        internal string[] RandomIdleActions { get; set; } = Array.Empty<string>();
        internal PetCommandDefinition[] Commands { get; init; } = Array.Empty<PetCommandDefinition>();
        internal PetDialogFeedbackDefinition SlangFeedback { get; init; }
        internal IReadOnlyDictionary<int, PetDialogFeedbackDefinition> FoodFeedback { get; init; } =
            new Dictionary<int, PetDialogFeedbackDefinition>();
        internal IReadOnlyDictionary<int, (int MinLevel, int MaxLevel)> FoodFeedbackLevelRanges { get; init; } =
            new Dictionary<int, (int MinLevel, int MaxLevel)>();
        internal PetAnimationSet Animations { get; init; } = new PetAnimationSet();
    }

    internal sealed class PetReactionDefinition
    {
        public string ActionName { get; init; }
        public string[] SpeechLines { get; init; } = Array.Empty<string>();
    }

    internal sealed class PetDialogFeedbackDefinition
    {
        public string[] SuccessLines { get; init; } = Array.Empty<string>();
        public string[] FailureLines { get; init; } = Array.Empty<string>();
    }

    internal sealed class PetCommandDefinition
    {
        public string[] Triggers { get; init; } = Array.Empty<string>();
        public int SuccessProbability { get; init; }
        public int ClosenessDelta { get; init; }
        public int LevelMin { get; init; }
        public int LevelMax { get; init; }
        public PetReactionDefinition SuccessReaction { get; init; }
        public PetReactionDefinition FailureReaction { get; init; }
    }

    internal sealed class PetLoader
    {
        private readonly record struct PetAnimationCacheKey(int PetItemId, int PetWearItemId);
        private readonly record struct PetActionCacheKey(int PetItemId, int PetWearItemId, string ActionName);

        private static readonly string[] SupportedActions =
        {
            "stand0",
            "stand1",
            "move",
            "jump",
            "hang",
            "fly",
            "rest0",
            "chat",
            "angry",
            "cry",
            "alert",
            "stretch",
            "prone",
            "hungry",
            "poor"
        };

        private static readonly IReadOnlyDictionary<string, string[]> ActionLookupCandidates =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["stand0"] = new[] { "stand0", "stand", "stand1" },
                ["stand1"] = new[] { "stand1", "stand0", "stand" },
                ["move"] = new[] { "move", "walk" },
                ["jump"] = new[] { "jump", "fly" },
                ["hang"] = new[] { "hang" },
                ["fly"] = new[] { "fly", "jump" },
                ["rest0"] = new[] { "rest0", "rest", "nap" },
                ["chat"] = new[] { "chat", "say", "speak" },
                ["angry"] = new[] { "angry", "no", "tedious" },
                ["cry"] = new[] { "cry", "stunned", "no" },
                ["alert"] = new[] { "alert", "hand" },
                ["stretch"] = new[] { "stretch", "love" },
                ["prone"] = new[] { "prone", "nap", "rest0" },
                ["hungry"] = new[] { "hungry" },
                ["poor"] = new[] { "poor", "dung" }
            };

        private static readonly string[] RandomIdleActionCandidates =
        {
            "chat",
            "alert",
            "stretch",
            "prone",
            "rest0"
        };

        private static readonly IReadOnlyDictionary<PetAutoSpeechEvent, string> StructuredEventPropertyNames =
            new Dictionary<PetAutoSpeechEvent, string>
            {
                [PetAutoSpeechEvent.LevelUp] = "e_levelup",
                [PetAutoSpeechEvent.PreLevelUp] = "e_prelevelup",
                [PetAutoSpeechEvent.Rest] = "e_rest",
                [PetAutoSpeechEvent.HpAlert] = "e_HPAlert",
                [PetAutoSpeechEvent.NoHpPotion] = "e_NOHPPotion",
                [PetAutoSpeechEvent.NoMpPotion] = "e_NOMPPotion"
            };

        private readonly GraphicsDevice _device;
        private readonly Dictionary<int, PetDefinition> _cache = new();
        private readonly Dictionary<PetAnimationCacheKey, PetAnimationSet> _animationSetCache = new();
        private readonly Dictionary<PetActionCacheKey, List<IDXObject>> _actionCache = new();
        private List<IDXObject> _clientMultiPetHangFrames;

        public PetLoader(GraphicsDevice device)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
        }

        public PetDefinition Load(int petItemId)
        {
            if (_cache.TryGetValue(petItemId, out PetDefinition cached))
            {
                return cached;
            }

            WzImage petImage = global::HaCreator.Program.FindImage("Item", $"Pet/{petItemId}.img");
            if (petImage == null)
            {
                return null;
            }

            petImage.ParseImage();
            Dictionary<string, string> dialogStrings = LoadPetDialogStrings(petItemId);

            PetAnimationSet animations = LoadAnimationSet(petItemId);
            var definition = new PetDefinition
            {
                ItemId = petItemId,
                Name = LoadPetName(petItemId) ?? $"Pet_{petItemId}",
                ChatBalloonStyle = GetIntValue(petImage["info"]?["chatBalloon"]) ?? 0,
                Icon = LoadInfoIcon(petImage, "icon"),
                IconRaw = LoadInfoIcon(petImage, "iconRaw"),
                EventSpeechLines = LoadEventSpeechLines(dialogStrings),
                Commands = LoadInteractCommands(petImage, dialogStrings),
                SlangFeedback = LoadDialogFeedback(dialogStrings, "s"),
                FoodFeedback = LoadFoodFeedback(dialogStrings),
                FoodFeedbackLevelRanges = LoadFoodFeedbackLevelRanges(petImage),
                Animations = animations
            };

            if (definition.Animations.ActionCount == 0)
            {
                return null;
            }

            definition.RandomIdleActions = RandomIdleActionCandidates
                .Where(candidate => definition.Animations.GetAvailableActions()
                    .Any(action => string.Equals(action, candidate, StringComparison.OrdinalIgnoreCase)))
                .ToArray();

            _cache[petItemId] = definition;
            return definition;
        }

        internal PetAnimationSet LoadAnimationSet(int petItemId, int petWearItemId = 0)
        {
            PetAnimationCacheKey cacheKey = new(petItemId, Math.Max(0, petWearItemId));
            if (_animationSetCache.TryGetValue(cacheKey, out PetAnimationSet cached))
            {
                return cached;
            }

            WzImage petImage = global::HaCreator.Program.FindImage("Item", $"Pet/{petItemId}.img");
            if (petImage == null)
            {
                return new PetAnimationSet();
            }

            petImage.ParseImage();
            var animations = new PetAnimationSet();
            foreach (string action in SupportedActions)
            {
                List<IDXObject> frames = LoadActionFrames(petImage, petItemId, action, cacheKey.PetWearItemId);
                if (frames.Count > 0)
                {
                    animations.AddAnimation(action, frames);
                }
            }

            List<IDXObject> clientMultiPetHangFrames = LoadClientMultiPetHangFrames();
            if (clientMultiPetHangFrames.Count > 0)
            {
                animations.AddAnimation(PetDefinition.ClientMultiPetHangActionName, clientMultiPetHangFrames);
            }

            _animationSetCache[cacheKey] = animations;
            return animations;
        }

        private string LoadPetName(int petItemId)
        {
            WzImage stringImage = global::HaCreator.Program.FindImage("String", "Pet.img");
            if (stringImage == null)
            {
                return null;
            }

            stringImage.ParseImage();
            if (stringImage[petItemId.ToString()] is not WzSubProperty petString)
            {
                return null;
            }

            return (petString["name"] as WzStringProperty)?.Value;
        }

        private static Dictionary<string, string> LoadPetDialogStrings(int petItemId)
        {
            WzImage petDialogImage = global::HaCreator.Program.FindImage("String", "PetDialog.img");
            if (petDialogImage == null)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            petDialogImage.ParseImage();
            if (petDialogImage[petItemId.ToString()] is not WzSubProperty petDialogProperty)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            return petDialogProperty.WzProperties
                .OfType<WzStringProperty>()
                .Where(property => !string.IsNullOrWhiteSpace(property.Name))
                .GroupBy(property => property.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.Last().Value?.Trim() ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase);
        }

        private static IReadOnlyDictionary<PetAutoSpeechEvent, string[]> LoadEventSpeechLines(
            IReadOnlyDictionary<string, string> dialogStrings)
        {
            var eventLines = new Dictionary<PetAutoSpeechEvent, string[]>();
            foreach ((PetAutoSpeechEvent eventType, string propertyName) in StructuredEventPropertyNames)
            {
                string[] lines = ExtractStructuredEventSpeechLines(propertyName, dialogStrings);
                if (lines.Length > 0)
                {
                    eventLines[eventType] = lines;
                }
            }

            if (!eventLines.ContainsKey(PetAutoSpeechEvent.Rest))
            {
                string[] idleFallbackLines = LoadLegacyIdleSpeechLines(dialogStrings);
                if (idleFallbackLines.Length > 0)
                {
                    eventLines[PetAutoSpeechEvent.Rest] = idleFallbackLines;
                }
            }

            return eventLines;
        }

        private static string[] ExtractStructuredEventSpeechLines(string propertyName, IReadOnlyDictionary<string, string> dialogStrings)
        {
            if (dialogStrings == null ||
                !dialogStrings.TryGetValue(propertyName, out string value) ||
                string.IsNullOrWhiteSpace(value))
            {
                return Array.Empty<string>();
            }

            return value
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private static string[] LoadLegacyIdleSpeechLines(IReadOnlyDictionary<string, string> dialogStrings)
        {
            if (dialogStrings == null || dialogStrings.Count == 0)
            {
                return Array.Empty<string>();
            }

            var fallbackLines = new List<string>();
            foreach ((string key, string value) in dialogStrings)
            {
                string eventNames = value?.Trim();
                if (string.IsNullOrWhiteSpace(eventNames) ||
                    eventNames.IndexOf("talk", StringComparison.OrdinalIgnoreCase) < 0 &&
                    eventNames.IndexOf("chat", StringComparison.OrdinalIgnoreCase) < 0 &&
                    eventNames.IndexOf("say", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                string prefix = key + "_s";
                foreach ((string siblingKey, string siblingValue) in dialogStrings)
                {
                    if (siblingKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(siblingValue))
                    {
                        fallbackLines.Add(siblingValue.Trim());
                    }
                }
            }

            return fallbackLines
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private static PetCommandDefinition[] LoadInteractCommands(WzImage petImage, IReadOnlyDictionary<string, string> dialogStrings)
        {
            if (petImage?["interact"] is not WzSubProperty interactProperty)
            {
                return Array.Empty<PetCommandDefinition>();
            }

            var commands = new List<PetCommandDefinition>();
            foreach (WzSubProperty interactEntry in interactProperty.WzProperties.OfType<WzSubProperty>().OrderBy(GetFrameOrder))
            {
                string commandKey = (interactEntry["command"] as WzStringProperty)?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(commandKey))
                {
                    continue;
                }

                string[] triggers = ResolveCommandTriggers(commandKey, dialogStrings);
                if (triggers.Length == 0)
                {
                    continue;
                }

                commands.Add(new PetCommandDefinition
                {
                    Triggers = triggers,
                    SuccessProbability = Math.Clamp(GetIntValue(interactEntry["prob"]) ?? 100, 0, 100),
                    ClosenessDelta = Math.Max(0, GetIntValue(interactEntry["inc"]) ?? 0),
                    LevelMin = GetIntValue(interactEntry["l0"]) ?? 0,
                    LevelMax = GetIntValue(interactEntry["l1"]) ?? 250,
                    SuccessReaction = LoadReaction(interactEntry["success"], dialogStrings),
                    FailureReaction = LoadReaction(interactEntry["fail"], dialogStrings)
                });
            }

            return commands.ToArray();
        }

        private static string[] ResolveCommandTriggers(string commandKey, IReadOnlyDictionary<string, string> dialogStrings)
        {
            if (dialogStrings == null ||
                !dialogStrings.TryGetValue(commandKey, out string triggerText) ||
                string.IsNullOrWhiteSpace(triggerText))
            {
                return Array.Empty<string>();
            }

            return triggerText
                .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(trigger => trigger.Trim())
                .Where(trigger => !string.IsNullOrWhiteSpace(trigger))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static PetReactionDefinition LoadReaction(WzImageProperty property, IReadOnlyDictionary<string, string> dialogStrings)
        {
            if (property is not WzSubProperty reactionProperty)
            {
                return null;
            }

            WzSubProperty firstReaction = reactionProperty.WzProperties.OfType<WzSubProperty>().OrderBy(GetFrameOrder).FirstOrDefault();
            if (firstReaction == null)
            {
                return null;
            }

            string actionName = (firstReaction["act"] as WzStringProperty)?.Value?.Trim();
            string[] speechLines = firstReaction.WzProperties
                .Where(child => child.Name != "act")
                .OrderBy(GetFrameOrder)
                .Select(child => (child as WzStringProperty)?.Value?.Trim())
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Select(key => dialogStrings != null && dialogStrings.TryGetValue(key, out string resolvedLine)
                    ? resolvedLine?.Trim()
                    : null)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            return new PetReactionDefinition
            {
                ActionName = actionName,
                SpeechLines = speechLines
            };
        }

        private static IReadOnlyDictionary<int, PetDialogFeedbackDefinition> LoadFoodFeedback(
            IReadOnlyDictionary<string, string> dialogStrings)
        {
            var feedback = new Dictionary<int, PetDialogFeedbackDefinition>();
            for (int variant = 1; variant <= 4; variant++)
            {
                PetDialogFeedbackDefinition entry = LoadDialogFeedback(dialogStrings, $"f{variant}");
                if (entry != null)
                {
                    feedback[variant] = entry;
                }
            }

            return feedback;
        }

        private static IReadOnlyDictionary<int, (int MinLevel, int MaxLevel)> LoadFoodFeedbackLevelRanges(WzImage petImage)
        {
            if (petImage?["interact"] is not WzSubProperty interactProperty)
            {
                return new Dictionary<int, (int MinLevel, int MaxLevel)>();
            }

            var ranges = new Dictionary<int, (int MinLevel, int MaxLevel)>();
            foreach (WzSubProperty interactEntry in interactProperty.WzProperties.OfType<WzSubProperty>())
            {
                string commandKey = (interactEntry["command"] as WzStringProperty)?.Value?.Trim();
                if (!TryResolveFoodFeedbackTier(commandKey, out int variant))
                {
                    continue;
                }

                int minLevel = Math.Clamp(GetIntValue(interactEntry["l0"]) ?? 1, 1, 30);
                int maxLevel = Math.Clamp(GetIntValue(interactEntry["l1"]) ?? minLevel, minLevel, 30);
                if (ranges.TryGetValue(variant, out (int MinLevel, int MaxLevel) existing))
                {
                    ranges[variant] = (Math.Min(existing.MinLevel, minLevel), Math.Max(existing.MaxLevel, maxLevel));
                }
                else
                {
                    ranges[variant] = (minLevel, maxLevel);
                }
            }

            return ranges;
        }

        private static PetDialogFeedbackDefinition LoadDialogFeedback(
            IReadOnlyDictionary<string, string> dialogStrings,
            string prefix)
        {
            if (dialogStrings == null || string.IsNullOrWhiteSpace(prefix))
            {
                return null;
            }

            string[] successLines = ResolveFeedbackLines(dialogStrings, prefix + "_s");
            string[] failureLines = ResolveFeedbackLines(dialogStrings, prefix + "_f");

            if (successLines.Length == 0 && failureLines.Length == 0)
            {
                successLines = ResolveFeedbackLines(dialogStrings, prefix);
            }

            return successLines.Length == 0 && failureLines.Length == 0
                ? null
                : new PetDialogFeedbackDefinition
                {
                    SuccessLines = successLines,
                    FailureLines = failureLines
                };
        }

        private static string[] ResolveFeedbackLines(
            IReadOnlyDictionary<string, string> dialogStrings,
            string key)
        {
            if (dialogStrings == null || string.IsNullOrWhiteSpace(key))
            {
                return Array.Empty<string>();
            }

            var lines = new List<string>();
            if (dialogStrings.TryGetValue(key, out string value) && !string.IsNullOrWhiteSpace(value))
            {
                lines.AddRange(SplitFeedbackLines(value));
            }

            foreach ((string siblingKey, string siblingValue) in dialogStrings
                         .Where(static pair => !string.IsNullOrWhiteSpace(pair.Key))
                         .OrderBy(pair => GetFeedbackKeySortOrder(pair.Key, key))
                         .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (!IsNumberedFeedbackKey(siblingKey, key) || string.IsNullOrWhiteSpace(siblingValue))
                {
                    continue;
                }

                lines.AddRange(SplitFeedbackLines(siblingValue));
            }

            return lines
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private static IEnumerable<string> SplitFeedbackLines(string value)
        {
            return value
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line));
        }

        private static bool IsNumberedFeedbackKey(string candidateKey, string prefix)
        {
            if (string.IsNullOrWhiteSpace(candidateKey) ||
                string.IsNullOrWhiteSpace(prefix) ||
                !candidateKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                candidateKey.Length <= prefix.Length)
            {
                return false;
            }

            return candidateKey.AsSpan(prefix.Length).ToString().All(char.IsDigit);
        }

        private static int GetFeedbackKeySortOrder(string candidateKey, string prefix)
        {
            if (!IsNumberedFeedbackKey(candidateKey, prefix))
            {
                return int.MaxValue;
            }

            return int.TryParse(candidateKey.Substring(prefix.Length), out int order)
                ? order
                : int.MaxValue;
        }

        private static bool TryResolveFoodFeedbackTier(string commandKey, out int variant)
        {
            variant = 0;
            if (string.IsNullOrWhiteSpace(commandKey) ||
                commandKey.Length < 2 ||
                !int.TryParse(commandKey.AsSpan(1), out int commandIndex) ||
                !string.Equals(commandKey.Substring(0, 1), "c", StringComparison.OrdinalIgnoreCase) ||
                commandIndex < 1 ||
                commandIndex > 4)
            {
                return false;
            }

            variant = commandIndex;
            return true;
        }

        private List<IDXObject> LoadActionFrames(WzImage petImage, int petItemId, string actionName, int petWearItemId)
        {
            PetActionCacheKey cacheKey = new(petItemId, petWearItemId, actionName ?? string.Empty);
            if (_actionCache.TryGetValue(cacheKey, out List<IDXObject> cachedFrames))
            {
                return cachedFrames;
            }

            WzImageProperty actionNode = ResolvePetActionNode(petImage, actionName);
            if (actionNode == null)
            {
                List<IDXObject> emptyFrames = new();
                _actionCache[cacheKey] = emptyFrames;
                return emptyFrames;
            }

            List<IDXObject> baseFrames = LoadActionFrames(actionNode, defaultDelay: 100);
            if (baseFrames.Count == 0 || petWearItemId <= 0)
            {
                _actionCache[cacheKey] = baseFrames;
                return baseFrames;
            }

            WzImageProperty petWearActionNode = ResolvePetWearActionNode(petWearItemId, petItemId, actionName);
            if (petWearActionNode == null)
            {
                _actionCache[cacheKey] = baseFrames;
                return baseFrames;
            }

            List<IDXObject> overlayFrames = LoadActionFrames(
                petWearActionNode,
                baseFrames.Select(frame => frame?.Delay ?? 100).ToList(),
                defaultDelay: 100);
            if (overlayFrames.Count == 0)
            {
                _actionCache[cacheKey] = baseFrames;
                return baseFrames;
            }

            var composedFrames = new List<IDXObject>(baseFrames.Count);
            for (int i = 0; i < baseFrames.Count; i++)
            {
                IDXObject baseFrame = baseFrames[i];
                IDXObject overlayFrame = i < overlayFrames.Count ? overlayFrames[i] : null;
                composedFrames.Add(overlayFrame == null ? baseFrame : new CompositePetFrame(baseFrame, overlayFrame));
            }

            _actionCache[cacheKey] = composedFrames;
            return composedFrames;
        }

        private WzImageProperty ResolvePetActionNode(WzImage petImage, string requestedAction)
        {
            if (petImage == null || string.IsNullOrWhiteSpace(requestedAction))
            {
                return null;
            }

            foreach (string candidate in EnumerateActionCandidates(requestedAction))
            {
                WzImageProperty property = ResolveActionProperty(petImage[candidate]);
                if (property != null)
                {
                    return property;
                }
            }

            return null;
        }

        private static IEnumerable<string> EnumerateActionCandidates(string requestedAction)
        {
            if (string.IsNullOrWhiteSpace(requestedAction))
            {
                yield break;
            }

            if (ActionLookupCandidates.TryGetValue(requestedAction, out string[] candidates))
            {
                for (int i = 0; i < candidates.Length; i++)
                {
                    yield return candidates[i];
                }
            }
            else
            {
                yield return requestedAction;
            }
        }

        private static WzImageProperty ResolveActionProperty(WzImageProperty property)
        {
            if (property is WzUOLProperty uol)
            {
                return ResolveActionProperty(uol.LinkValue as WzImageProperty);
            }

            if (property is WzStringProperty stringProperty && !string.IsNullOrWhiteSpace(stringProperty.Value))
            {
                return ResolveActionProperty(stringProperty.Parent?[stringProperty.Value] as WzImageProperty);
            }

            return property;
        }

        private WzImageProperty ResolvePetWearActionNode(int petWearItemId, int petItemId, string requestedAction)
        {
            if (petWearItemId <= 0 || petItemId <= 0)
            {
                return null;
            }

            WzImage petWearImage = global::HaCreator.Program.FindImage("Character", $"PetEquip/{petWearItemId:D8}.img");
            if (petWearImage == null)
            {
                return null;
            }

            petWearImage.ParseImage();
            WzSubProperty resolvedRoot = petWearImage[petItemId.ToString("D7")] as WzSubProperty
                ?? petWearImage[petItemId.ToString("D8")] as WzSubProperty;
            if (resolvedRoot == null)
            {
                return null;
            }

            foreach (string candidate in EnumerateActionCandidates(requestedAction))
            {
                WzImageProperty property = ResolveActionProperty(resolvedRoot[candidate]);
                if (property != null)
                {
                    return property;
                }
            }

            return null;
        }

        private List<IDXObject> LoadActionFrames(WzImageProperty actionNode, int defaultDelay)
        {
            return LoadActionFrames(actionNode, fallbackDelays: null, defaultDelay);
        }

        private List<IDXObject> LoadActionFrames(WzImageProperty actionNode, IReadOnlyList<int> fallbackDelays, int defaultDelay)
        {
            var frames = new List<IDXObject>();
            if (actionNode == null)
            {
                return frames;
            }

            foreach (WzImageProperty child in actionNode.WzProperties.OrderBy(GetFrameOrder))
            {
                WzCanvasProperty canvas = ResolveCanvasProperty(child);

                if (canvas == null)
                {
                    continue;
                }

                int frameIndex = frames.Count;
                int fallbackDelay = fallbackDelays != null && frameIndex < fallbackDelays.Count
                    ? fallbackDelays[frameIndex]
                    : defaultDelay;
                IDXObject frame = LoadTexture(canvas, fallbackDelay);
                if (frame != null)
                {
                    frames.Add(frame);
                }
            }

            return frames;
        }

        private static WzCanvasProperty ResolveCanvasProperty(WzImageProperty property)
        {
            if (property is WzCanvasProperty canvasProperty)
            {
                return canvasProperty;
            }

            if (property is WzUOLProperty uol)
            {
                return ResolveCanvasProperty(uol.LinkValue as WzImageProperty);
            }

            return null;
        }

        private List<IDXObject> LoadClientMultiPetHangFrames()
        {
            if (_clientMultiPetHangFrames != null)
            {
                return _clientMultiPetHangFrames;
            }

            WzImage effectImage = global::HaCreator.Program.FindImage("Effect", "PetEff.img");
            if (effectImage == null)
            {
                _clientMultiPetHangFrames = new List<IDXObject>();
                return _clientMultiPetHangFrames;
            }

            effectImage.ParseImage();
            _clientMultiPetHangFrames = effectImage["Basic"]?["hang"] is WzSubProperty multiPetHangNode
                ? LoadActionFrames(multiPetHangNode, defaultDelay: 100)
                : new List<IDXObject>();

            return _clientMultiPetHangFrames;
        }

        private IDXObject LoadInfoIcon(WzImage petImage, string iconName)
        {
            if (petImage?["info"] is not WzSubProperty info)
            {
                return null;
            }

            if (info[iconName] is not WzCanvasProperty canvas)
            {
                return null;
            }

            return LoadTexture(canvas);
        }

        private IDXObject LoadTexture(WzCanvasProperty canvas, int defaultDelay = 100)
        {
            if (canvas?.PngProperty == null)
            {
                return null;
            }

            try
            {
                var bitmap = canvas.GetLinkedWzCanvasBitmap();
                if (bitmap == null)
                {
                    return null;
                }

                var texture = bitmap.ToTexture2DAndDispose(_device);
                if (texture == null)
                {
                    return null;
                }

                int delay = GetIntValue(canvas["delay"]) ?? Math.Max(1, defaultDelay);
                return new DXObject(0, 0, texture, delay)
                {
                    Tag = canvas
                };
            }
            catch
            {
                return null;
            }
        }

        private static int GetFrameOrder(WzImageProperty property)
        {
            return int.TryParse(property?.Name, out int index) ? index : int.MaxValue;
        }

        private static int? GetIntValue(WzImageProperty prop)
        {
            return prop switch
            {
                WzIntProperty intProp => intProp.Value,
                WzShortProperty shortProp => shortProp.Value,
                WzLongProperty longProp => (int)longProp.Value,
                WzStringProperty strProp => int.TryParse(strProp.Value, out int value) ? value : null,
                _ => null
            };
        }

        private sealed class CompositePetFrame : IDXObject
        {
            private readonly IDXObject _baseFrame;
            private readonly IDXObject _overlayFrame;

            public CompositePetFrame(IDXObject baseFrame, IDXObject overlayFrame)
            {
                _baseFrame = baseFrame ?? throw new ArgumentNullException(nameof(baseFrame));
                _overlayFrame = overlayFrame ?? throw new ArgumentNullException(nameof(overlayFrame));
            }

            public int Delay => _overlayFrame.Delay > 0 ? _overlayFrame.Delay : _baseFrame.Delay;
            public int X => Math.Min(_baseFrame.X, _overlayFrame.X);
            public int Y => Math.Min(_baseFrame.Y, _overlayFrame.Y);
            public int Width => Math.Max(_baseFrame.X + _baseFrame.Width, _overlayFrame.X + _overlayFrame.Width) - X;
            public int Height => Math.Max(_baseFrame.Y + _baseFrame.Height, _overlayFrame.Y + _overlayFrame.Height) - Y;
            public object Tag { get; set; }
            public Texture2D Texture => _baseFrame.Texture;

            public void DrawObject(SpriteBatch sprite, SkeletonMeshRenderer meshRenderer, Microsoft.Xna.Framework.GameTime gameTime, int mapShiftX, int mapShiftY, bool flip, ReflectionDrawableBoundary drawReflectionInfo)
            {
                _baseFrame.DrawObject(sprite, meshRenderer, gameTime, mapShiftX, mapShiftY, flip, drawReflectionInfo);
                _overlayFrame.DrawObject(sprite, meshRenderer, gameTime, mapShiftX, mapShiftY, flip, drawReflectionInfo);
            }

            public void DrawBackground(SpriteBatch sprite, SkeletonMeshRenderer meshRenderer, Microsoft.Xna.Framework.GameTime gameTime, int x, int y, Microsoft.Xna.Framework.Color color, bool flip, ReflectionDrawableBoundary drawReflectionInfo)
            {
                _baseFrame.DrawBackground(sprite, meshRenderer, gameTime, x, y, color, flip, drawReflectionInfo);
                _overlayFrame.DrawBackground(sprite, meshRenderer, gameTime, x, y, color, flip, drawReflectionInfo);
            }
        }
    }
}
