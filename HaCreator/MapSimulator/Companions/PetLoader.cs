using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework.Graphics;
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
        internal PetAnimationSet Animations { get; } = new PetAnimationSet();
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
        public int LevelMin { get; init; }
        public int LevelMax { get; init; }
        public PetReactionDefinition SuccessReaction { get; init; }
        public PetReactionDefinition FailureReaction { get; init; }
    }

    internal sealed class PetLoader
    {
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
                FoodFeedback = LoadFoodFeedback(dialogStrings)
            };

            foreach (string action in SupportedActions)
            {
                if (petImage[action] is WzSubProperty actionNode)
                {
                    List<IDXObject> frames = LoadActionFrames(actionNode);
                    if (frames.Count > 0)
                    {
                        definition.Animations.AddAnimation(action, frames);
                    }
                }
            }

            List<IDXObject> clientMultiPetHangFrames = LoadClientMultiPetHangFrames();
            if (clientMultiPetHangFrames.Count > 0)
            {
                definition.Animations.AddAnimation(PetDefinition.ClientMultiPetHangActionName, clientMultiPetHangFrames);
            }

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
            if (dialogStrings == null ||
                string.IsNullOrWhiteSpace(key) ||
                !dialogStrings.TryGetValue(key, out string value) ||
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

        private List<IDXObject> LoadActionFrames(WzSubProperty actionNode)
        {
            var frames = new List<IDXObject>();

            foreach (WzImageProperty child in actionNode.WzProperties.OrderBy(GetFrameOrder))
            {
                WzCanvasProperty canvas = child as WzCanvasProperty;
                if (canvas == null && child is WzUOLProperty uol)
                {
                    canvas = uol.LinkValue as WzCanvasProperty;
                }

                if (canvas == null)
                {
                    continue;
                }

                IDXObject frame = LoadTexture(canvas);
                if (frame != null)
                {
                    frames.Add(frame);
                }
            }

            return frames;
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
                ? LoadActionFrames(multiPetHangNode)
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

        private IDXObject LoadTexture(WzCanvasProperty canvas)
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

                int delay = GetIntValue(canvas["delay"]) ?? 100;
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
    }
}
