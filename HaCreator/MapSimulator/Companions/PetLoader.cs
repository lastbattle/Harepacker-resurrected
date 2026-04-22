using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Render;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using SD = System.Drawing;
using SDG = System.Drawing.Graphics;

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
        internal const int ClientPetActionDefaultDelay = 180;

        private readonly record struct PetActionFrame(string Name, IDXObject Frame);
        private readonly record struct PetAnimationCacheKey(int PetItemId, int PetWearItemId);
        private readonly record struct PetActionCacheKey(int PetItemId, int PetWearItemId, string ActionName);
        private sealed class PetImageEntry
        {
            internal PetImageEntry(int templateId, string sourcePath, WzImage imageRoot, WzImageProperty propertyRoot)
            {
                TemplateId = Math.Max(0, templateId);
                SourcePath = sourcePath ?? string.Empty;
                ImageRoot = imageRoot;
                PropertyRoot = propertyRoot;
            }

            internal int TemplateId { get; }
            internal string SourcePath { get; }
            internal WzImage ImageRoot { get; }
            internal WzImageProperty PropertyRoot { get; }

            internal IEnumerable<WzImageProperty> Children => ImageRoot?.WzProperties ?? PropertyRoot?.WzProperties ?? Enumerable.Empty<WzImageProperty>();

            internal WzImageProperty this[string name] => ImageRoot != null ? ImageRoot[name] : PropertyRoot?[name];
        }

        private static readonly string[] RandomIdleActionCandidates =
        {
            "chat",
            "say",
            "alert",
            "hand",
            "stretch",
            "love",
            "prone",
            "nap",
            "rest0",
            "rest"
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
        private readonly Dictionary<int, PetImageEntry> _petImgEntryCache = new();
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

            PetImageEntry petImgEntry = ResolvePetImgEntry(petItemId);
            WzImage petImage = petImgEntry?.ImageRoot;
            if (petImage == null)
            {
                return null;
            }

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

            PetImageEntry petImgEntry = ResolvePetImgEntry(petItemId);
            if (petImgEntry == null)
            {
                return new PetAnimationSet();
            }

            var animations = new PetAnimationSet();
            foreach (string action in EnumerateActionLoadOrder(petImgEntry, petItemId, cacheKey.PetWearItemId))
            {
                List<IDXObject> frames = LoadActionFrames(petImgEntry, petItemId, action, cacheKey.PetWearItemId);
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

            animations.AddMissingAliasAnimations();
            _animationSetCache[cacheKey] = animations;
            return animations;
        }

        private PetImageEntry ResolvePetImgEntry(int petItemId)
        {
            int cacheKey = Math.Max(0, petItemId);
            if (_petImgEntryCache.TryGetValue(cacheKey, out PetImageEntry cachedEntry))
            {
                return cachedEntry;
            }

            PetImageEntry entry = petItemId > 0
                ? ResolvePetTemplateImgEntry(petItemId)
                : ResolveBasicPetEffectImgEntry();
            if (entry != null)
            {
                _petImgEntryCache[cacheKey] = entry;
            }

            return entry;
        }

        private static PetImageEntry ResolvePetTemplateImgEntry(int petItemId)
        {
            string petImagePath = FormatPetImagePath(petItemId);
            WzImage petImage = global::HaCreator.Program.FindImage("Item", petImagePath);
            if (petImage == null)
            {
                return null;
            }

            petImage.ParseImage();
            return new PetImageEntry(petItemId, petImagePath, petImage, null);
        }

        private static PetImageEntry ResolveBasicPetEffectImgEntry()
        {
            WzImage effectImage = global::HaCreator.Program.FindImage("Effect", "PetEff.img");
            if (effectImage == null)
            {
                return null;
            }

            effectImage.ParseImage();
            return effectImage["Basic"] is WzImageProperty basicRoot
                ? new PetImageEntry(0, "Effect/PetEff.img/Basic", null, basicRoot)
                : null;
        }

        private static string FormatPetImagePath(int petItemId)
        {
            return $"Pet/{Math.Max(0, petItemId)}.img";
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

        private List<IDXObject> LoadActionFrames(PetImageEntry petImgEntry, int petItemId, string actionName, int petWearItemId)
        {
            PetActionCacheKey cacheKey = new(petItemId, petWearItemId, actionName ?? string.Empty);
            if (_actionCache.TryGetValue(cacheKey, out List<IDXObject> cachedFrames))
            {
                return cachedFrames;
            }

            WzImageProperty actionNode = ResolvePetActionNode(petImgEntry, actionName);
            if (actionNode == null)
            {
                List<IDXObject> emptyFrames = new();
                _actionCache[cacheKey] = emptyFrames;
                return emptyFrames;
            }

            List<PetActionFrame> baseFrames = LoadNamedActionFrames(actionNode, fallbackDelayByFrameName: null, defaultDelay: ClientPetActionDefaultDelay);
            if (baseFrames.Count == 0)
            {
                List<IDXObject> emptyFrames = new();
                _actionCache[cacheKey] = emptyFrames;
                return emptyFrames;
            }

            if (petWearItemId <= 0)
            {
                List<IDXObject> loadedBaseFrames = baseFrames.Select(static entry => entry.Frame).ToList();
                _actionCache[cacheKey] = loadedBaseFrames;
                return loadedBaseFrames;
            }

            WzImageProperty petWearActionNode = ResolvePetWearActionNode(petWearItemId, petItemId, actionName);
            if (petWearActionNode == null)
            {
                List<IDXObject> loadedBaseFrames = baseFrames.Select(static entry => entry.Frame).ToList();
                _actionCache[cacheKey] = loadedBaseFrames;
                return loadedBaseFrames;
            }

            var baseDelayByFrameName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (PetActionFrame baseFrame in baseFrames)
            {
                if (!string.IsNullOrWhiteSpace(baseFrame.Name) && baseFrame.Frame != null)
                {
                    baseDelayByFrameName[baseFrame.Name] = ResolveComposedPetWearFrameDelay(baseFrame.Frame, null);
                }
            }

            List<PetActionFrame> overlayFrames = LoadNamedActionFrames(
                petWearActionNode,
                baseDelayByFrameName,
                defaultDelay: ClientPetActionDefaultDelay);
            if (overlayFrames.Count == 0)
            {
                List<IDXObject> loadedBaseFrames = baseFrames.Select(static entry => entry.Frame).ToList();
                _actionCache[cacheKey] = loadedBaseFrames;
                return loadedBaseFrames;
            }

            var overlayFramesByName = new Dictionary<string, IDXObject>(StringComparer.OrdinalIgnoreCase);
            foreach (PetActionFrame overlayFrame in overlayFrames)
            {
                if (!string.IsNullOrWhiteSpace(overlayFrame.Name) &&
                    overlayFrame.Frame != null &&
                    !overlayFramesByName.ContainsKey(overlayFrame.Name))
                {
                    overlayFramesByName[overlayFrame.Name] = overlayFrame.Frame;
                }
            }

            var composedFrames = new List<IDXObject>(baseFrames.Count);
            List<IDXObject> overlayFrameOrder = overlayFrames.Select(static frame => frame.Frame).ToList();
            for (int baseFrameIndex = 0; baseFrameIndex < baseFrames.Count; baseFrameIndex++)
            {
                PetActionFrame baseFrame = baseFrames[baseFrameIndex];
                IDXObject overlayFrame = ResolvePetWearOverlayFrame(
                    overlayFramesByName,
                    overlayFrameOrder,
                    baseFrame.Name,
                    baseFrameIndex);
                composedFrames.Add(overlayFrame == null ? baseFrame.Frame : ComposePetWearFrame(baseFrame.Frame, overlayFrame));
            }

            _actionCache[cacheKey] = composedFrames;
            return composedFrames;
        }

        internal static IDXObject ResolvePetWearOverlayFrame(
            IReadOnlyDictionary<string, IDXObject> overlayFramesByName,
            IReadOnlyList<IDXObject> overlayFramesByIndex,
            string baseFrameName,
            int baseFrameIndex)
        {
            if (!string.IsNullOrWhiteSpace(baseFrameName) &&
                overlayFramesByName != null &&
                overlayFramesByName.TryGetValue(baseFrameName, out IDXObject namedFrame) &&
                namedFrame != null)
            {
                return namedFrame;
            }

            if (overlayFramesByIndex != null &&
                baseFrameIndex >= 0 &&
                baseFrameIndex < overlayFramesByIndex.Count)
            {
                return overlayFramesByIndex[baseFrameIndex];
            }

            return null;
        }

        private WzImageProperty ResolvePetActionNode(PetImageEntry petImgEntry, string requestedAction)
        {
            if (petImgEntry == null || string.IsNullOrWhiteSpace(requestedAction))
            {
                return null;
            }

            WzImageProperty[] candidateProperties = petImgEntry.Children
                .Where(static property => property != null)
                .ToArray();
            if (candidateProperties.Length == 0)
            {
                return null;
            }

            string resolvedActionName = ResolveActionNameForLookup(
                candidateProperties.Select(static property => property.Name),
                requestedAction);
            if (string.IsNullOrWhiteSpace(resolvedActionName))
            {
                return null;
            }

            return ResolveActionProperty(petImgEntry[resolvedActionName] ??
                                         candidateProperties.FirstOrDefault(property =>
                                             string.Equals(property.Name, resolvedActionName, StringComparison.Ordinal)));
        }

        private static IEnumerable<string> EnumerateActionCandidates(string requestedAction)
        {
            if (string.IsNullOrWhiteSpace(requestedAction))
            {
                yield break;
            }

            bool yielded = false;
            foreach (string candidate in PetActionAliases.EnumerateCandidates(requestedAction))
            {
                yielded = true;
                yield return candidate;
            }

            if (!yielded)
            {
                yield return requestedAction;
            }
        }

        private IEnumerable<string> EnumerateActionLoadOrder(PetImageEntry petImgEntry, int petItemId, int petWearItemId)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string action in PetActionAliases.EnumerateKnownActions())
            {
                if (seen.Add(action))
                {
                    yield return action;
                }
            }

            foreach (string action in EnumerateRenderableActionNames(petImgEntry))
            {
                if (seen.Add(action))
                {
                    yield return action;
                }
            }

            foreach (string action in EnumerateRenderableActionNames(ResolvePetWearRoot(petWearItemId, petItemId)))
            {
                if (seen.Add(action))
                {
                    yield return action;
                }
            }
        }

        private static IEnumerable<string> EnumerateRenderableActionNames(PetImageEntry imageEntry)
        {
            if (imageEntry == null)
            {
                yield break;
            }

            foreach (WzImageProperty child in imageEntry.Children)
            {
                if (child == null ||
                    string.IsNullOrWhiteSpace(child.Name) ||
                    !HasRenderableFrames(ResolveActionProperty(child)))
                {
                    continue;
                }

                yield return child.Name;
            }
        }

        private static IEnumerable<string> EnumerateRenderableActionNames(WzImageProperty actionRoot)
        {
            if (actionRoot == null)
            {
                yield break;
            }

            foreach (WzImageProperty child in actionRoot.WzProperties)
            {
                if (child == null ||
                    string.IsNullOrWhiteSpace(child.Name) ||
                    !HasRenderableFrames(ResolveActionProperty(child)))
                {
                    continue;
                }

                yield return child.Name;
            }
        }

        private static bool HasRenderableFrames(WzImageProperty property)
        {
            if (property is WzCanvasProperty)
            {
                return true;
            }

            return property != null &&
                   property.WzProperties != null &&
                   property.WzProperties.Any(child => ResolveCanvasProperty(child) != null);
        }

        private static WzImageProperty ResolveActionProperty(WzImageProperty property)
        {
            if (property is WzUOLProperty uol)
            {
                return ResolveActionProperty(uol.LinkValue as WzImageProperty);
            }

            if (property is WzStringProperty stringProperty && !string.IsNullOrWhiteSpace(stringProperty.Value))
            {
                return ResolveActionProperty(ResolveLinkedPropertyPath(stringProperty.Parent, stringProperty.Value));
            }

            return property;
        }

        private static WzImageProperty ResolveLinkedPropertyPath(WzObject context, string path)
        {
            if (context == null || string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            string normalizedPath = path.Replace('\\', '/').Trim();
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return null;
            }

            if (context is WzImageProperty propertyContext)
            {
                WzImageProperty resolvedFromProperty = ResolveLinkedPropertyPath(propertyContext, normalizedPath);
                if (resolvedFromProperty != null)
                {
                    return resolvedFromProperty;
                }
            }

            if (context is WzImage imageContext)
            {
                imageContext.ParseImage();
                return imageContext.GetFromPath(normalizedPath);
            }

            return null;
        }

        private static WzImageProperty ResolveLinkedPropertyPath(WzImageProperty context, string path)
        {
            if (context == null || string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            string normalizedPath = path.Replace('\\', '/').Trim();
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return null;
            }

            if (!normalizedPath.Contains('/'))
            {
                return context[normalizedPath] ?? context.ParentImage?.GetFromPath(normalizedPath);
            }

            string[] segments = normalizedPath
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return null;
            }

            WzObject current = normalizedPath.StartsWith("../", StringComparison.Ordinal) ? context.Parent : context;
            foreach (string segment in segments)
            {
                if (string.Equals(segment, ".", StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(segment, "..", StringComparison.Ordinal))
                {
                    current = current?.Parent;
                    continue;
                }

                current = current switch
                {
                    WzImageProperty currentProperty => currentProperty[segment],
                    WzImage currentImage => currentImage.GetFromPath(segment),
                    _ => null
                };

                if (current == null)
                {
                    return context.ParentImage?.GetFromPath(normalizedPath);
                }
            }

            return current as WzImageProperty;
        }

        private WzImageProperty ResolvePetWearActionNode(int petWearItemId, int petItemId, string requestedAction)
        {
            if (petWearItemId <= 0 || petItemId <= 0)
            {
                return null;
            }

            WzSubProperty resolvedRoot = ResolvePetWearRoot(petWearItemId, petItemId);
            if (resolvedRoot == null)
            {
                return null;
            }

            WzImageProperty[] candidateProperties = resolvedRoot.WzProperties
                .Where(static property => property != null)
                .ToArray();
            if (candidateProperties.Length == 0)
            {
                return null;
            }

            string resolvedActionName = ResolveActionNameForLookup(
                candidateProperties.Select(static property => property.Name),
                requestedAction);
            if (string.IsNullOrWhiteSpace(resolvedActionName))
            {
                return null;
            }

            return ResolveActionProperty(resolvedRoot[resolvedActionName] ??
                                         candidateProperties.FirstOrDefault(property =>
                                             string.Equals(property.Name, resolvedActionName, StringComparison.Ordinal)));
        }

        internal static string ResolveActionNameForLookup(
            IEnumerable<string> actionNames,
            string requestedAction)
        {
            if (actionNames == null || string.IsNullOrWhiteSpace(requestedAction))
            {
                return null;
            }

            string[] resolvedActionNames = actionNames
                .Where(static name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (resolvedActionNames.Length == 0)
            {
                return null;
            }

            foreach (string candidate in EnumerateActionCandidates(requestedAction))
            {
                string resolvedCandidateName = ResolveCanonicalActionName(resolvedActionNames, candidate);
                if (!string.IsNullOrWhiteSpace(resolvedCandidateName))
                {
                    return resolvedCandidateName;
                }
            }

            return ResolveCanonicalActionName(resolvedActionNames, requestedAction);
        }

        private static string ResolveCanonicalActionName(
            IReadOnlyList<string> candidateActionNames,
            string requestedAction)
        {
            if (candidateActionNames == null || string.IsNullOrWhiteSpace(requestedAction))
            {
                return null;
            }

            for (int i = 0; i < candidateActionNames.Count; i++)
            {
                string candidateActionName = candidateActionNames[i];
                if (string.Equals(candidateActionName, requestedAction, StringComparison.Ordinal))
                {
                    return candidateActionName;
                }
            }

            string normalizedRequestedAction = PetActionAliases.NormalizeActionName(requestedAction);
            if (!string.IsNullOrWhiteSpace(normalizedRequestedAction))
            {
                for (int i = 0; i < candidateActionNames.Count; i++)
                {
                    string candidateActionName = candidateActionNames[i];
                    if (!string.Equals(
                            PetActionAliases.NormalizeActionName(candidateActionName),
                            normalizedRequestedAction,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    return candidateActionName;
                }
            }

            string normalizedRequestedStem = PetActionAliases.NormalizeActionStem(requestedAction);
            if (string.IsNullOrWhiteSpace(normalizedRequestedStem))
            {
                return null;
            }

            for (int i = 0; i < candidateActionNames.Count; i++)
            {
                string candidateActionName = candidateActionNames[i];
                if (!string.Equals(
                        PetActionAliases.NormalizeActionStem(candidateActionName),
                        normalizedRequestedStem,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                return candidateActionName;
            }

            return null;
        }

        private WzSubProperty ResolvePetWearRoot(int petWearItemId, int petItemId)
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
            return ResolveActionProperty(petWearImage[petItemId.ToString("D7")] ?? petWearImage[petItemId.ToString("D8")]) as WzSubProperty;
        }

        private List<IDXObject> LoadActionFrames(WzImageProperty actionNode, int defaultDelay)
        {
            return LoadNamedActionFrames(actionNode, fallbackDelayByFrameName: null, defaultDelay)
                .Select(static entry => entry.Frame)
                .ToList();
        }

        private List<PetActionFrame> LoadNamedActionFrames(
            WzImageProperty actionNode,
            IReadOnlyDictionary<string, int> fallbackDelayByFrameName,
            int defaultDelay)
        {
            var frames = new List<PetActionFrame>();
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

                string frameName = child.Name ?? string.Empty;
                int fallbackDelay = fallbackDelayByFrameName != null &&
                                    !string.IsNullOrWhiteSpace(frameName) &&
                                    fallbackDelayByFrameName.TryGetValue(frameName, out int resolvedFallbackDelay)
                    ? resolvedFallbackDelay
                    : defaultDelay;
                IDXObject frame = LoadTexture(canvas, fallbackDelay);
                if (frame != null)
                {
                    frames.Add(new PetActionFrame(frameName, frame));
                }
            }

            return frames;
        }

        private static WzCanvasProperty ResolveCanvasProperty(WzImageProperty property)
        {
            WzImageProperty resolvedProperty = ResolveActionProperty(property);
            if (resolvedProperty is WzCanvasProperty canvasProperty)
            {
                return canvasProperty;
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
                ? LoadActionFrames(multiPetHangNode, defaultDelay: ClientPetActionDefaultDelay)
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

        private IDXObject ComposePetWearFrame(IDXObject baseFrame, IDXObject overlayFrame)
        {
            if (baseFrame?.Tag is not WzCanvasProperty baseCanvas ||
                overlayFrame?.Tag is not WzCanvasProperty overlayCanvas)
            {
                return baseFrame;
            }

            try
            {
                using SD.Bitmap baseBitmap = baseCanvas.GetLinkedWzCanvasBitmap();
                using SD.Bitmap overlayBitmap = overlayCanvas.GetLinkedWzCanvasBitmap();
                if (!TryGetBitmapDimensions(baseBitmap, out int baseWidth, out int baseHeight) ||
                    !TryGetBitmapDimensions(overlayBitmap, out int overlayWidth, out int overlayHeight))
                {
                    return baseFrame;
                }

                Rectangle baseBounds = ResolveCanvasBounds(baseCanvas, baseWidth, baseHeight);
                Rectangle overlayBounds = ResolveCanvasBounds(overlayCanvas, overlayWidth, overlayHeight);
                Rectangle composedBounds = Rectangle.FromLTRB(
                    Math.Min(baseBounds.Left, overlayBounds.Left),
                    Math.Min(baseBounds.Top, overlayBounds.Top),
                    Math.Max(baseBounds.Right, overlayBounds.Right),
                    Math.Max(baseBounds.Bottom, overlayBounds.Bottom));

                using var composedBitmap = new SD.Bitmap(Math.Max(1, composedBounds.Width), Math.Max(1, composedBounds.Height));
                using (SDG graphics = SDG.FromImage(composedBitmap))
                {
                    graphics.Clear(SD.Color.Transparent);
                    graphics.DrawImage(baseBitmap, baseBounds.X - composedBounds.X, baseBounds.Y - composedBounds.Y);
                    graphics.DrawImage(overlayBitmap, overlayBounds.X - composedBounds.X, overlayBounds.Y - composedBounds.Y);
                }

                Texture2D texture = composedBitmap.ToTexture2DAndDispose(_device);
                if (texture == null)
                {
                    return baseFrame;
                }

                int delay = ResolveComposedPetWearFrameDelay(baseFrame, overlayFrame);
                return new DXObject(new PointF(-composedBounds.X, -composedBounds.Y), texture, delay)
                {
                    Tag = baseCanvas
                };
            }
            catch
            {
                return baseFrame;
            }
        }

        internal static int ResolveComposedPetWearFrameDelay(IDXObject baseFrame, IDXObject overlayFrame)
        {
            if (baseFrame?.Delay > 0)
            {
                return baseFrame.Delay;
            }

            if (overlayFrame?.Delay > 0)
            {
                return overlayFrame.Delay;
            }

            return ClientPetActionDefaultDelay;
        }

        private static Rectangle ResolveCanvasBounds(WzCanvasProperty canvas, int width, int height)
        {
            Point canvasOrigin = ResolveCanvasOrigin(canvas);
            Point? authoredLt = TryResolveVector(canvas?["lt"], out Point lt) ? lt : null;
            Point? authoredRb = TryResolveVector(canvas?["rb"], out Point rb) ? rb : null;
            return ResolveCanvasBoundsFromMetadata(authoredLt, authoredRb, canvasOrigin, width, height);
        }

        internal static Rectangle ResolveCanvasBoundsFromMetadata(
            Point? authoredLt,
            Point? authoredRb,
            Point origin,
            int width,
            int height)
        {
            if (width <= 0 || height <= 0)
            {
                return Rectangle.Empty;
            }

            int left = authoredLt?.X ?? -origin.X;
            int top = authoredLt?.Y ?? -origin.Y;
            int right = authoredRb?.X ?? left + width;
            int bottom = authoredRb?.Y ?? top + height;

            if (right <= left)
            {
                right = left + width;
            }

            if (bottom <= top)
            {
                bottom = top + height;
            }

            return Rectangle.FromLTRB(left, top, right, bottom);
        }

        private static Point ResolveCanvasOrigin(WzCanvasProperty canvas)
        {
            System.Drawing.PointF canvasOrigin = canvas?.GetCanvasOriginPosition() ?? default;
            return new Point((int)Math.Round(canvasOrigin.X), (int)Math.Round(canvasOrigin.Y));
        }

        private static bool TryResolveVector(WzImageProperty property, out Point vector)
        {
            vector = default;
            if (property is not WzVectorProperty vectorProperty)
            {
                return false;
            }

            vector = new Point(vectorProperty.X.Value, vectorProperty.Y.Value);
            return true;
        }

        private static bool TryGetBitmapDimensions(SD.Bitmap bitmap, out int width, out int height)
        {
            width = bitmap?.Width ?? 0;
            height = bitmap?.Height ?? 0;
            return width > 0 && height > 0;
        }
    }
}
