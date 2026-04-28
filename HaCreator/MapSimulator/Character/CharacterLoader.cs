using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace HaCreator.MapSimulator.Character
{
    /// <summary>
    /// Loads character parts from Character.wz
    /// </summary>
    public class CharacterLoader
    {
        private const bool EnableAnimationFallbackDiagnostics = false;
        private const int DefaultWizetHatId = 1002140;
        private const int DefaultWizetSuitId = 1042003;
        private const int DefaultWizetPantsId = 1062007;
        private const int DefaultWizetSuitcaseId = 1322013;
        private const int DefaultPanLidShieldId = 1092008;
        private const int DefaultLeatherSandalsId = 1072005;
        private const int DefaultMaleFaceId = 20000;
        private const int DefaultFemaleFaceId = 21000;
        private const int DefaultMaleHairId = 30000;
        private const int DefaultFemaleHairId = 31000;
        private const int LoginStarterAvatarSelectionCount = 8;
        private const int StarterFaceSearchSpan = 100;
        private const int StarterHairSearchSpan = 100;
        private const int CarryItemEffectBundleItemId = 1112916;
        private const int CarryItemEffectSingleAItemId = 1112924;
        private const int CarryItemEffectSingleBItemId = 1112926;
        private const int ClientFallbackMaleTopId = 1040036;
        private const int ClientFallbackFemaleTopId = 1041046;
        private const int ClientFallbackMaleBottomId = 1060026;
        private const int ClientFallbackFemaleBottomId = 1061039;
        private const int BlockedCapItemId = 1002186;
        private const int BlockedEarringsItemId = 1032024;
        private const int BlockedEyeAccessoryItemId = 1022079;
        private const int BlockedShoesItemId = 1072153;
        private const int BlockedGloveItemId = 1082102;
        private const int BlockedCapeItemId = 1102039;
        private const int BlockedShieldItemId = 1092067;
        private const int BlockedWeaponStickerItemIdA = 1702099;
        private const int BlockedWeaponStickerItemIdB = 1702190;
        private const int MechanicTamingMobItemId = 1932016;
        private const int ClientMorphReplayTailStringPoolId = 0x049F;
        private const string ClientMorphReplayTailFallbackName = "zigzag";

        private static readonly SkinColor[] PreferredStarterSkins =
        {
            SkinColor.Light,
            SkinColor.Tan,
            SkinColor.Dark,
            SkinColor.Pale,
            SkinColor.Blue
        };

        private readonly WzFile _characterWz;
        private readonly GraphicsDevice _device;
        private readonly TexturePool _texturePool;
        private readonly Dictionary<string, WzImage> _characterImageCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, CharacterAnimation> _animationCache = new(StringComparer.OrdinalIgnoreCase);

        // Cache for loaded parts
        private readonly Dictionary<int, BodyPart> _bodyCache = new();
        private readonly Dictionary<int, FacePart> _faceCache = new();
        private readonly Dictionary<int, HairPart> _hairCache = new();
        private readonly Dictionary<int, CharacterPart> _equipCache = new();
        private readonly Dictionary<int, CharacterPart> _morphCache = new();
        private readonly Dictionary<int, MorphImageEntry> _morphImageEntryCache = new();
        private readonly Dictionary<MorphActionCacheKey, CharacterAnimation> _morphActionCache = new();
        private readonly Dictionary<int, PortableChair> _portableChairCache = new();
        private readonly Dictionary<int, ItemEffectAnimationSet> _itemEffectCache = new();
        private readonly Dictionary<int, ItemEffectAnimationSet> _activeEffectItemEffectCache = new();
        private readonly Dictionary<int, ItemEffectAnimationSet> _completedSetEffectCache = new();
        private readonly Dictionary<RemoteRelationshipOverlayType, RelationshipTextTagStyle> _relationshipTextTagCache = new();
        private readonly Dictionary<CharacterGender, StarterAvatarRandomizationCatalog> _starterAvatarCatalogCache = new();
        private CarryItemEffectDefinition _carryItemEffectCache;
        private readonly HashSet<string> _loggedAnimationFallbackKeys = new(StringComparer.Ordinal);

        // Standard actions to load
        private static readonly string[] StandardActions = new[]
        {
            "stand1", "stand2", "walk1", "walk2", "jump", "sit", "prone",
            "ladder", "rope", "swim", "fly", "alert", "heal"
        };

        // Attack actions
        private static readonly string[] AttackActions = new[]
        {
            "stabO1", "stabO2", "stabOF", "stabT1", "stabT2", "stabTF",
            "swingO1", "swingO2", "swingO3", "swingOF", "swingT1", "swingT2", "swingT3", "swingTF",
            "swingP1", "swingP2", "swingPF", "shoot1", "shoot2", "shootF", "proneStab"
        };

        private static readonly HashSet<string> NonActionProperties = new(StringComparer.OrdinalIgnoreCase)
        {
            "info",
            "icon",
            "iconRaw"
        };

        private sealed class StarterAvatarRandomizationCatalog
        {
            public IReadOnlyList<SkinColor> Skins { get; init; } = Array.Empty<SkinColor>();
            public IReadOnlyList<int> FaceIds { get; init; } = Array.Empty<int>();
            public IReadOnlyList<int> HairIds { get; init; } = Array.Empty<int>();
            public IReadOnlyList<int> CoatIds { get; init; } = Array.Empty<int>();
            public IReadOnlyList<int> PantsIds { get; init; } = Array.Empty<int>();
            public IReadOnlyList<int> ShoesIds { get; init; } = Array.Empty<int>();
            public IReadOnlyList<int> WeaponIds { get; init; } = Array.Empty<int>();
        }

        public sealed class LoginStarterAvatarCatalog
        {
            public IReadOnlyList<SkinColor> Skins { get; init; } = Array.Empty<SkinColor>();
            public IReadOnlyList<int> FaceIds { get; init; } = Array.Empty<int>();
            public IReadOnlyList<int> HairStyleIds { get; init; } = Array.Empty<int>();
            public IReadOnlyList<int> HairIds => HairStyleIds;
            public IReadOnlyList<int> HairColorIndices { get; init; } = Array.Empty<int>();
            public IReadOnlyList<int> CoatIds { get; init; } = Array.Empty<int>();
            public IReadOnlyList<int> PantsIds { get; init; } = Array.Empty<int>();
            public IReadOnlyList<int> ShoesIds { get; init; } = Array.Empty<int>();
            public IReadOnlyList<int> WeaponIds { get; init; } = Array.Empty<int>();
        }

        internal sealed class CharacterActionMergeInput
        {
            public string ActionName { get; init; } = CharacterPart.GetActionString(CharacterAction.Stand1);
            public IReadOnlyDictionary<EquipSlot, CharacterPart> Equipment { get; init; } = new Dictionary<EquipSlot, CharacterPart>();
            public CharacterPart WeaponSticker { get; init; }
            public CharacterPart ActiveTamingMobPart { get; init; }
        }

        private readonly record struct MorphActionCacheKey(int TemplateId, string ActionName);

        private sealed class CharacterImageEntry
        {
            public WzImage BaseImage { get; init; }
            public WzObject ActionSourceRoot { get; init; }
            public CharacterImageEntryMetadata Metadata { get; init; }
            public bool HasWeeklyVariant { get; init; }
            public bool UsesWeeklyVariantOverride { get; init; }
            public int ResolvedWeeklyVariantIndex { get; init; } = -1;
        }

        private sealed class CharacterImageEntryMetadata
        {
            public string ISlot { get; init; }
            public string VSlot { get; init; }
            public string Sfx { get; init; }
            public bool IsCash { get; init; }
            public bool HasWeeklyVariant { get; init; }
            public string WeaponAfterImageType { get; init; }
            public int WeaponWalkFrameCount { get; init; }
            public int WeaponStandFrameCount { get; init; }
            public int WeaponAttackFrameCount { get; init; }
            public int WeaponAttackSpeed { get; init; } = 6;
        }

        private sealed class MorphImageEntry
        {
            public int TemplateId { get; init; }
            public int LinkedTemplateId { get; init; }
            public bool IsSuperManMorph { get; init; }
            public IReadOnlyDictionary<string, WzImageProperty> ActionNodes { get; init; } =
                new Dictionary<string, WzImageProperty>(StringComparer.OrdinalIgnoreCase);
            public IReadOnlySet<string> AvailableActionNames { get; init; } =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public CharacterLoader(WzFile characterWz, GraphicsDevice device, TexturePool texturePool)
        {
            _characterWz = characterWz; // Can be null - will use Program.FindImage fallback
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _texturePool = texturePool;
        }

        /// <summary>
        /// Get a character image by name, using WzFile if available or Program.FindImage fallback
        /// </summary>
        private WzImage GetCharacterImage(string imgName)
        {
            if (string.IsNullOrWhiteSpace(imgName))
            {
                return null;
            }

            if (_characterImageCache.TryGetValue(imgName, out WzImage cachedImage))
            {
                return cachedImage;
            }

            WzImage resolvedImage = null;

            // Try WzFile first if available
            if (_characterWz?.WzDirectory != null)
            {
                resolvedImage = _characterWz.WzDirectory[imgName] as WzImage;
            }

            resolvedImage ??= Program.FindImage("Character", imgName);
            _characterImageCache[imgName] = resolvedImage;
            return resolvedImage;
        }

        #region Body/Head Loading

        /// <summary>
        /// Load body part (00002xxx.img)
        /// </summary>
        public BodyPart LoadBody(SkinColor skin)
        {
            int bodyId = 2000 + (int)skin;
            if (_bodyCache.TryGetValue(bodyId, out var cached))
                return cached;

            string imgName = bodyId.ToString("D8") + ".img";
            var imgNode = GetCharacterImage(imgName);

            System.Diagnostics.Debug.WriteLine($"[CharacterLoader] LoadBody skin={skin}, imgName={imgName}, found={imgNode != null}");

            if (imgNode == null)
                return null;

            var body = new BodyPart
            {
                ItemId = bodyId,
                Name = $"Body_{skin}",
                Type = CharacterPartType.Body,
                SkinColor = skin,
                IsHead = false
            };

            LoadPartAnimations(body, imgNode as WzImage, includeAttackActions: false);
            _bodyCache[bodyId] = body;
            return body;
        }

        /// <summary>
        /// Load head part (00012xxx.img)
        /// </summary>
        public BodyPart LoadHead(SkinColor skin)
        {
            int headId = 12000 + (int)skin;
            if (_bodyCache.TryGetValue(headId, out var cached))
                return cached;

            string imgName = headId.ToString("D8") + ".img";
            var imgNode = GetCharacterImage(imgName);

            System.Diagnostics.Debug.WriteLine($"[CharacterLoader] LoadHead skin={skin}, imgName={imgName}, found={imgNode != null}");

            if (imgNode == null)
                return null;

            var head = new BodyPart
            {
                ItemId = headId,
                Name = $"Head_{skin}",
                Type = CharacterPartType.Head,
                SkinColor = skin,
                IsHead = true
            };

            LoadPartAnimations(head, imgNode as WzImage, includeAttackActions: false);
            _bodyCache[headId] = head;
            return head;
        }

        #endregion

        #region Portable Chair Loading

        public CharacterPart LoadMorph(int morphTemplateId)
        {
            if (morphTemplateId <= 0)
            {
                return null;
            }

            if (_morphCache.TryGetValue(morphTemplateId, out CharacterPart cached))
            {
                return cached;
            }

            string imageName = morphTemplateId.ToString("D4") + ".img";
            WzImage morphImage = Program.FindImage("Morph", imageName);
            MorphImageEntry morphImageEntry = GetMorphImageEntry(morphTemplateId, morphImage);
            if (morphImageEntry == null || morphImageEntry.ActionNodes.Count == 0)
            {
                return null;
            }

            var morphPart = new CharacterPart
            {
                ItemId = morphTemplateId,
                Name = $"Morph_{morphTemplateId:D4}",
                Type = CharacterPartType.Morph,
                Slot = EquipSlot.None,
                IsSuperManMorph = morphImageEntry.IsSuperManMorph
            };

            morphPart.AvailableAnimations = new HashSet<string>(
                morphImageEntry.AvailableActionNames?.ToArray() ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
            morphPart.AnimationResolver = actionName => LoadMorphActionAnimation(morphTemplateId, actionName);
            foreach (string actionName in BuildEagerActionLoadOrder(morphPart.AvailableAnimations, includeAttackActions: false))
            {
                CharacterAnimation animation = LoadMorphActionAnimation(morphTemplateId, actionName);
                if (animation?.Frames?.Count > 0)
                {
                    morphPart.Animations[actionName] = animation;
                }
            }

            if (morphPart.Animations.Count == 0)
            {
                return null;
            }

            AttachMorphActionFrameOwner(morphPart);
            _morphCache[morphTemplateId] = morphPart;
            return morphPart;
        }

        private MorphImageEntry GetMorphImageEntry(int morphTemplateId, WzImage exactMorphImage = null)
        {
            return GetMorphImageEntry(morphTemplateId, exactMorphImage, new HashSet<int>());
        }

        private MorphImageEntry GetMorphImageEntry(
            int morphTemplateId,
            WzImage exactMorphImage,
            ISet<int> activeTemplateIds)
        {
            if (morphTemplateId <= 0)
            {
                return null;
            }

            if (exactMorphImage == null
                && _morphImageEntryCache.TryGetValue(morphTemplateId, out MorphImageEntry cachedEntry))
            {
                return cachedEntry;
            }

            if (activeTemplateIds != null && !activeTemplateIds.Add(morphTemplateId))
            {
                return null;
            }

            try
            {
                var actionNodes = new Dictionary<string, WzImageProperty>(StringComparer.OrdinalIgnoreCase);
                bool isSuperManMorph = false;
                int linkedTemplateId = 0;

                foreach (int candidateTemplateId in EnumerateMorphTemplateCandidates(morphTemplateId, exactMorphImage))
                {
                    WzImage candidateImage = candidateTemplateId == morphTemplateId
                        ? exactMorphImage ?? Program.FindImage("Morph", candidateTemplateId.ToString("D4") + ".img")
                        : Program.FindImage("Morph", candidateTemplateId.ToString("D4") + ".img");
                    if (candidateImage == null)
                    {
                        continue;
                    }

                    candidateImage.ParseImage();

                    if (candidateTemplateId == morphTemplateId)
                    {
                        linkedTemplateId = GetMorphLinkTemplateId(candidateImage);
                    }

                    if (ShouldUseMorphTemplateInfo(morphTemplateId, candidateTemplateId)
                        && GetMorphSuperManFlag(candidateImage))
                    {
                        isSuperManMorph = true;
                    }

                    foreach (WzImageProperty property in candidateImage.WzProperties)
                    {
                        if (property != null
                            && LooksLikePublishedMorphAction(property)
                            && !actionNodes.ContainsKey(property.Name))
                        {
                            actionNodes[property.Name] = property;
                        }
                    }
                }

                if (actionNodes.Count == 0)
                {
                    return null;
                }

                var entry = new MorphImageEntry
                {
                    TemplateId = morphTemplateId,
                    LinkedTemplateId = linkedTemplateId,
                    IsSuperManMorph = isSuperManMorph,
                    ActionNodes = actionNodes,
                    AvailableActionNames = new HashSet<string>(actionNodes.Keys, StringComparer.OrdinalIgnoreCase)
                };

                _morphImageEntryCache[morphTemplateId] = entry;
                return entry;
            }
            finally
            {
                activeTemplateIds?.Remove(morphTemplateId);
            }
        }

        private CharacterAnimation LoadMorphActionAnimation(int morphTemplateId, string actionName)
        {
            if (morphTemplateId <= 0 || string.IsNullOrWhiteSpace(actionName))
            {
                return null;
            }

            MorphActionCacheKey cacheKey = new(morphTemplateId, actionName);
            if (_morphActionCache.TryGetValue(cacheKey, out CharacterAnimation cachedAnimation))
            {
                return cachedAnimation;
            }

            MorphImageEntry morphImageEntry = GetMorphImageEntry(morphTemplateId);
            if (morphImageEntry == null
                || morphImageEntry.ActionNodes == null
                || !morphImageEntry.ActionNodes.TryGetValue(actionName, out WzImageProperty actionNode))
            {
                return null;
            }

            CharacterAnimation animation = LoadMorphAnimation(actionNode, actionName);
            if (animation?.Frames?.Count > 0)
            {
                _morphActionCache[cacheKey] = animation;
                return animation;
            }

            return null;
        }

        private CharacterAnimation LoadMorphAnimation(WzImageProperty node, string actionName)
        {
            if (node == null || string.IsNullOrWhiteSpace(actionName))
            {
                return null;
            }

            if (node is WzUOLProperty actionUol && actionUol.LinkValue is WzImageProperty linkedActionNode)
            {
                return LoadMorphAnimation(linkedActionNode, actionName);
            }

            var animation = new CharacterAnimation
            {
                ActionName = actionName,
                Action = CharacterPart.ParseActionString(actionName)
            };

            if (node is WzCanvasProperty canvas)
            {
                CharacterFrame frame = LoadMorphFrame(canvas, node, "0", frameUol: null);
                if (frame != null)
                {
                    animation.Frames.Add(frame);
                }
            }
            else if (node is WzSubProperty subProperty)
            {
                foreach (KeyValuePair<string, WzImageProperty> frameEntry in EnumerateMorphFrameNodes(subProperty))
                {
                    CharacterFrame frame = LoadMorphFrameEntry(frameEntry.Value, frameEntry.Key);
                    if (frame != null)
                    {
                        animation.Frames.Add(frame);
                    }
                }

                if (ShouldAppendMorphReplayTail(subProperty))
                {
                    AppendMorphReplayTail(animation);
                }
            }

            animation.CalculateTotalDuration();
            return animation.Frames.Count > 0 ? animation : null;
        }

        private static IEnumerable<KeyValuePair<string, WzImageProperty>> EnumerateMorphFrameNodes(WzSubProperty actionNode)
        {
            if (actionNode == null)
            {
                yield break;
            }

            // CActionMan::LoadMorphAction enumerates the morph action property instead of
            // assuming dense 0..N frame keys, so sparse authored rows still publish frames.
            foreach (WzImageProperty frameNode in actionNode.WzProperties ?? Enumerable.Empty<WzImageProperty>())
            {
                if (frameNode != null && int.TryParse(frameNode.Name, out _))
                {
                    yield return new KeyValuePair<string, WzImageProperty>(frameNode.Name, frameNode);
                }
            }
        }

        internal static string[] EnumerateMorphFrameNamesForTesting(WzSubProperty actionNode)
        {
            return EnumerateMorphFrameNodes(actionNode)
                .Select(static frameEntry => frameEntry.Key)
                .ToArray();
        }

        private CharacterFrame LoadMorphFrameEntry(WzImageProperty frameNode, string frameName)
        {
            if (frameNode == null)
            {
                return null;
            }

            if (TryResolveMorphFrameCanvas(frameNode, out WzCanvasProperty frameCanvas))
            {
                return LoadMorphFrame(
                    frameCanvas,
                    frameNode,
                    frameName,
                    (frameNode as WzUOLProperty)?.Value);
            }

            return null;
        }

        private CharacterFrame LoadMorphFrame(
            WzCanvasProperty canvas,
            WzImageProperty metadataNode,
            string frameName,
            string frameUol)
        {
            if (canvas == null)
            {
                return null;
            }

            IDXObject texture = LoadTexture(canvas);
            if (texture == null)
            {
                return null;
            }

            Point origin = ResolveFrameOrigin(metadataNode, canvas);
            CharacterFrame frame = new()
            {
                Texture = texture,
                Origin = origin,
                Delay = ResolveFrameInt(metadataNode, canvas, "delay", 120),
                Z = ResolveZLayer(GetStringValue(metadataNode?["z"]) ?? GetStringValue(canvas["z"]), frameName),
                Bounds = ResolveFrameBounds(metadataNode, canvas, texture, origin),
                FrameUol = frameUol
            };

            AddFrameMapPoints(frame, metadataNode, canvas);
            return frame;
        }

        private static void AddFrameMapPoints(CharacterFrame frame, WzImageProperty metadataNode, WzCanvasProperty canvas)
        {
            if (frame == null)
            {
                return;
            }

            WzImageProperty resolvedMetadataNode = ResolveFrameMetadataProperty(metadataNode, canvas);
            if (resolvedMetadataNode?["map"] is WzSubProperty mapSubProperty)
            {
                foreach (WzImageProperty mapPoint in mapSubProperty.WzProperties)
                {
                    if (mapPoint is WzVectorProperty vectorProperty)
                    {
                        frame.Map[mapPoint.Name] = new Point(vectorProperty.X.Value, vectorProperty.Y.Value);
                    }
                }
            }

            foreach (WzImageProperty child in resolvedMetadataNode?.WzProperties ?? canvas?.WzProperties ?? Enumerable.Empty<WzImageProperty>())
            {
                if (child is not WzVectorProperty vectorProperty
                    || frame.Map.ContainsKey(child.Name)
                    || string.Equals(child.Name, "origin", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(child.Name, "lt", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(child.Name, "rb", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                frame.Map[child.Name] = new Point(vectorProperty.X.Value, vectorProperty.Y.Value);
            }
        }

        private static WzImageProperty ResolveFrameMetadataProperty(WzImageProperty frameNode, WzCanvasProperty canvas)
        {
            if (frameNode != null
                && (frameNode is WzCanvasProperty
                    || frameNode["origin"] != null
                    || frameNode["delay"] != null
                    || frameNode["lt"] != null
                    || frameNode["rb"] != null
                    || frameNode["head"] != null
                    || frameNode["z"] != null))
            {
                return frameNode;
            }

            return canvas;
        }

        private static int ResolveFrameInt(WzImageProperty metadataNode, WzCanvasProperty canvas, string propertyName, int defaultValue)
        {
            WzImageProperty resolvedMetadataNode = ResolveFrameMetadataProperty(metadataNode, canvas);
            return GetIntValue(resolvedMetadataNode?[propertyName])
                ?? GetIntValue(canvas?[propertyName])
                ?? defaultValue;
        }

        private static Point ResolveFrameOrigin(WzImageProperty metadataNode, WzCanvasProperty canvas)
        {
            Point? metadataOrigin = GetVectorValue(ResolveFrameMetadataProperty(metadataNode, canvas)?["origin"]);
            if (metadataOrigin.HasValue)
            {
                return metadataOrigin.Value;
            }

            Point? canvasOrigin = GetVectorValue(canvas?["origin"]);
            return canvasOrigin ?? Point.Zero;
        }

        private static Rectangle ResolveFrameBounds(
            WzImageProperty metadataNode,
            WzCanvasProperty canvas,
            IDXObject texture,
            Point origin)
        {
            WzImageProperty resolvedMetadataNode = ResolveFrameMetadataProperty(metadataNode, canvas);
            Point? lt = GetVectorValue(resolvedMetadataNode?["lt"]) ?? GetVectorValue(canvas?["lt"]);
            Point? rb = GetVectorValue(resolvedMetadataNode?["rb"]) ?? GetVectorValue(canvas?["rb"]);
            if (lt.HasValue && rb.HasValue)
            {
                int left = lt.Value.X;
                int top = lt.Value.Y;
                int width = Math.Max(1, rb.Value.X - left);
                int height = Math.Max(1, rb.Value.Y - top);
                return new Rectangle(left, top, width, height);
            }

            return new Rectangle(-origin.X, -origin.Y, texture?.Width ?? 0, texture?.Height ?? 0);
        }

        private static Point? GetVectorValue(WzImageProperty property)
        {
            if (property is WzVectorProperty vectorProperty)
            {
                return new Point(vectorProperty.X.Value, vectorProperty.Y.Value);
            }

            return null;
        }

        private static bool ShouldAppendMorphReplayTail(WzImageProperty actionNode)
        {
            if (actionNode == null)
            {
                return false;
            }

            string replayTailFlagName = MapleStoryStringPool.GetOrFallback(
                ClientMorphReplayTailStringPoolId,
                ClientMorphReplayTailFallbackName);
            return ResolveFrameInt(actionNode, canvas: null, replayTailFlagName, 0) != 0;
        }

        internal static void AppendMorphReplayTailForTesting(CharacterAnimation animation)
        {
            AppendMorphReplayTail(animation);
        }

        private static void AppendMorphReplayTail(CharacterAnimation animation)
        {
            if (animation?.Frames == null || animation.Frames.Count < 3)
            {
                return;
            }

            int lastInteriorFrameIndex = animation.Frames.Count - 2;
            for (int i = lastInteriorFrameIndex; i >= 1; i--)
            {
                animation.Frames.Add(animation.Frames[i].Clone());
            }
        }

        internal static bool CanResolveMorphTemplate(int morphTemplateId)
        {
            return CanResolveMorphTemplate(morphTemplateId, Array.Empty<string>());
        }

        internal static bool CanResolveMorphTemplate(int morphTemplateId, IReadOnlyList<string> requestedActionNames)
        {
            if (morphTemplateId <= 0)
            {
                return false;
            }

            var availableActionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool hasCandidateImage = false;

            var checkedTemplateIds = new HashSet<int>();
            foreach (int candidateTemplateId in EnumerateMorphTemplateCandidates(morphTemplateId, exactMorphImage: null))
            {
                if (!checkedTemplateIds.Add(candidateTemplateId))
                {
                    continue;
                }

                WzImage candidateImage = Program.FindImage("Morph", candidateTemplateId.ToString("D4") + ".img");
                if (candidateImage == null)
                {
                    continue;
                }

                hasCandidateImage = true;
                candidateImage.ParseImage();
                foreach (string publishedActionName in BuildPublishedMorphActionSet(candidateImage))
                {
                    availableActionNames.Add(publishedActionName);
                }
            }

            if (!hasCandidateImage || availableActionNames.Count == 0)
            {
                return false;
            }

            if (requestedActionNames == null || requestedActionNames.Count == 0)
            {
                return true;
            }

            var morphPart = new CharacterPart
            {
                Type = CharacterPartType.Morph,
                Animations = new Dictionary<string, CharacterAnimation>(StringComparer.OrdinalIgnoreCase),
                AvailableAnimations = new HashSet<string>(availableActionNames, StringComparer.OrdinalIgnoreCase)
            };

            foreach (string requestedActionName in requestedActionNames)
            {
                if (string.IsNullOrWhiteSpace(requestedActionName))
                {
                    continue;
                }

                foreach (string candidateActionName in MorphClientActionResolver.EnumerateClientActionAliases(morphPart, requestedActionName))
                {
                    if (availableActionNames.Contains(candidateActionName))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void PopulateMorphAnimations(CharacterPart morphPart, int morphTemplateId, WzImage exactMorphImage)
        {
            if (morphPart == null)
            {
                return;
            }

            var loadedTemplateIds = new HashSet<int>();
            foreach (int candidateTemplateId in EnumerateMorphTemplateCandidates(morphTemplateId, exactMorphImage))
            {
                if (!loadedTemplateIds.Add(candidateTemplateId))
                {
                    continue;
                }

                WzImage candidateImage = candidateTemplateId == morphTemplateId
                    ? exactMorphImage
                    : Program.FindImage("Morph", candidateTemplateId.ToString("D4") + ".img");
                if (candidateImage == null)
                {
                    continue;
                }

                var candidatePart = new CharacterPart
                {
                    ItemId = candidateTemplateId,
                    Name = $"Morph_{candidateTemplateId:D4}",
                    Type = CharacterPartType.Morph,
                    Slot = EquipSlot.None,
                    IsSuperManMorph = ShouldUseMorphTemplateInfo(morphTemplateId, candidateTemplateId)
                        && GetMorphSuperManFlag(candidateImage)
                };

                LoadPartAnimations(candidatePart, candidateImage, includeAttackActions: false);
                ApplyPublishedMorphActionFilter(candidatePart, candidateImage);
                if (candidatePart.IsSuperManMorph)
                {
                    morphPart.IsSuperManMorph = true;
                }

                MergeMissingAnimations(morphPart, candidatePart);
            }
        }

        private static IEnumerable<int> EnumerateMorphTemplateCandidates(int morphTemplateId, WzImage exactMorphImage)
        {
            var seen = new HashSet<int>();

            foreach (int rootCandidate in EnumerateMorphTemplateRootCandidates(morphTemplateId))
            {
                bool allowLinkInheritance = rootCandidate == morphTemplateId;
                WzImage rootImage = rootCandidate == morphTemplateId
                    ? exactMorphImage
                    : null;

                foreach (int candidate in EnumerateMorphLinkChain(rootCandidate, rootImage, seen, allowLinkInheritance))
                {
                    yield return candidate;
                }
            }
        }

        internal static IReadOnlyList<int> EnumerateMorphTemplateCandidatesForTesting(
            int morphTemplateId,
            Func<int, int> linkedTemplateResolver)
        {
            var seen = new HashSet<int>();
            var candidates = new List<int>();

            foreach (int rootCandidate in EnumerateMorphTemplateRootCandidates(morphTemplateId))
            {
                bool allowLinkInheritance = rootCandidate == morphTemplateId;
                foreach (int candidate in EnumerateMorphLinkChain(rootCandidate, linkedTemplateResolver, seen, allowLinkInheritance))
                {
                    candidates.Add(candidate);
                }
            }

            return candidates;
        }

        private static IEnumerable<int> EnumerateMorphTemplateRootCandidates(int morphTemplateId)
        {
            if (morphTemplateId <= 0)
            {
                yield break;
            }

            yield return morphTemplateId;

            foreach (int candidate in EnumeratePairedMorphTemplateCandidates(morphTemplateId))
            {
                yield return candidate;
            }

            int[] familyBases =
            {
                (morphTemplateId / 10) * 10,
                (morphTemplateId / 100) * 100,
                (morphTemplateId / 1000) * 1000
            };

            foreach (int candidate in familyBases)
            {
                if (candidate > 0)
                {
                    yield return candidate;
                }
            }
        }

        private static IEnumerable<int> EnumerateMorphLinkChain(
            int morphTemplateId,
            WzImage exactMorphImage,
            HashSet<int> seen,
            bool allowLinkInheritance)
        {
            if (morphTemplateId <= 0 || seen == null || !seen.Add(morphTemplateId))
            {
                yield break;
            }

            yield return morphTemplateId;

            // Keep `info/link` inheritance anchored to the requested morph template.
            // Paired/family fallback roots are simulator-side backstops and should not
            // recursively pull their own link chains.
            if (!allowLinkInheritance)
            {
                yield break;
            }

            WzImage morphImage = exactMorphImage ?? Program.FindImage("Morph", morphTemplateId.ToString("D4") + ".img");
            int linkedTemplateId = GetMorphLinkTemplateId(morphImage);
            if (linkedTemplateId <= 0 || linkedTemplateId == morphTemplateId)
            {
                yield break;
            }

            foreach (int linkedCandidate in EnumerateMorphLinkChain(
                         linkedTemplateId,
                         exactMorphImage: null,
                         seen,
                         allowLinkInheritance: true))
            {
                yield return linkedCandidate;
            }
        }

        private static IEnumerable<int> EnumerateMorphLinkChain(
            int morphTemplateId,
            Func<int, int> linkedTemplateResolver,
            HashSet<int> seen,
            bool allowLinkInheritance)
        {
            if (morphTemplateId <= 0 || seen == null || !seen.Add(morphTemplateId))
            {
                yield break;
            }

            yield return morphTemplateId;

            if (!allowLinkInheritance)
            {
                yield break;
            }

            int linkedTemplateId = linkedTemplateResolver?.Invoke(morphTemplateId) ?? 0;
            if (linkedTemplateId <= 0 || linkedTemplateId == morphTemplateId)
            {
                yield break;
            }

            foreach (int linkedCandidate in EnumerateMorphLinkChain(
                         linkedTemplateId,
                         linkedTemplateResolver,
                         seen,
                         allowLinkInheritance: true))
            {
                yield return linkedCandidate;
            }
        }

        private static IEnumerable<int> EnumeratePairedMorphTemplateCandidates(int morphTemplateId)
        {
            // WZ shows paired 100x/110x morph families with matching action sets
            // (for example 1001 <-> 1101 and 1003 <-> 1103), so prefer that sibling
            // before the coarser id -> id0 -> id00 -> id000 truncation fallback.
            if (morphTemplateId >= 1000 && morphTemplateId < 1200)
            {
                int pairedTemplateId = morphTemplateId >= 1100
                    ? morphTemplateId - 100
                    : morphTemplateId + 100;
                if (pairedTemplateId > 0)
                {
                    yield return pairedTemplateId;
                }
            }
        }

        private static bool ShouldUseMorphTemplateInfo(int morphTemplateId, int candidateTemplateId)
        {
            // `CActionMan::GetMorphImgEntry` merges linked morph action properties but
            // skips the linked `info` branch. Keep simulator-only paired/family fallback
            // metadata out of the requested morph entry for the same reason.
            return morphTemplateId > 0 && candidateTemplateId == morphTemplateId;
        }

        internal static bool ShouldUseMorphTemplateInfoForTesting(
            int morphTemplateId,
            int candidateTemplateId)
        {
            return ShouldUseMorphTemplateInfo(morphTemplateId, candidateTemplateId);
        }

        private static int GetMorphLinkTemplateId(WzImage morphImage)
        {
            if (morphImage == null)
            {
                return 0;
            }

            morphImage.ParseImage();

            if (morphImage["info"] is not WzSubProperty infoNode)
            {
                return 0;
            }

            if (GetIntValue(infoNode["link"]) is int linkedTemplateId && linkedTemplateId > 0)
            {
                return linkedTemplateId;
            }

            if (GetStringValue(infoNode["link"]) is string linkedTemplateText
                && int.TryParse(linkedTemplateText, out linkedTemplateId)
                && linkedTemplateId > 0)
            {
                return linkedTemplateId;
            }

            return 0;
        }

        private static bool GetMorphSuperManFlag(WzImage morphImage)
        {
            if (morphImage == null)
            {
                return false;
            }

            morphImage.ParseImage();

            if (morphImage["info"] is not WzSubProperty infoNode)
            {
                return false;
            }

            return GetIntValue(infoNode["superman"]) is int superManFlag && superManFlag != 0;
        }

        private static void MergeMissingAnimations(CharacterPart targetPart, CharacterPart sourcePart)
        {
            if (targetPart == null || sourcePart == null)
            {
                return;
            }

            if (targetPart.AvailableAnimations == null)
            {
                targetPart.AvailableAnimations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            foreach (string availableAction in sourcePart.AvailableAnimations ?? Enumerable.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(availableAction))
                {
                    targetPart.AvailableAnimations.Add(availableAction);
                }
            }

            if (targetPart.Animations == null || sourcePart.Animations == null)
            {
                return;
            }

            foreach (KeyValuePair<string, CharacterAnimation> sourceAnimation in sourcePart.Animations)
            {
                if (!targetPart.Animations.ContainsKey(sourceAnimation.Key) && sourceAnimation.Value?.Frames?.Count > 0)
                {
                    targetPart.Animations[sourceAnimation.Key] = sourceAnimation.Value;
                }
            }
        }

        internal static void MergeMissingAnimationsForTesting(CharacterPart targetPart, CharacterPart sourcePart)
        {
            MergeMissingAnimations(targetPart, sourcePart);
        }

        private static void ApplyPublishedMorphActionFilter(CharacterPart morphPart, WzObject actionSourceRoot)
        {
            if (morphPart == null || actionSourceRoot == null)
            {
                return;
            }

            HashSet<string> publishedActions = BuildPublishedMorphActionSet(actionSourceRoot);
            morphPart.AvailableAnimations = publishedActions;

            if (morphPart.Animations == null || morphPart.Animations.Count == 0)
            {
                return;
            }

            foreach (string actionName in morphPart.Animations.Keys.ToArray())
            {
                if (!publishedActions.Contains(actionName))
                {
                    morphPart.Animations.Remove(actionName);
                }
            }
        }

        private static HashSet<string> BuildPublishedMorphActionSet(WzObject actionSourceRoot)
        {
            var publishedActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (actionSourceRoot == null)
            {
                return publishedActions;
            }

            foreach (WzImageProperty property in EnumerateChildProperties(actionSourceRoot))
            {
                if (property != null
                    && LooksLikePublishedMorphAction(property)
                    && !string.IsNullOrWhiteSpace(property.Name))
                {
                    publishedActions.Add(property.Name);
                }
            }

            return publishedActions;
        }

        internal static IReadOnlyCollection<string> BuildPublishedMorphActionSetForTesting(WzObject actionSourceRoot)
        {
            return BuildPublishedMorphActionSet(actionSourceRoot);
        }

        public PortableChair LoadPortableChair(int itemId)
        {
            if (_portableChairCache.TryGetValue(itemId, out PortableChair cached))
            {
                return cached;
            }

            if (!IsPortableChairItemId(itemId)
                || InventoryItemMetadataResolver.ResolveInventoryType(itemId) != MapleLib.WzLib.WzStructure.Data.ItemStructure.InventoryType.SETUP
                || !InventoryItemMetadataResolver.TryResolveImageSource(itemId, out string category, out string imagePath))
            {
                return null;
            }

            WzImage itemImage = Program.FindImage(category, imagePath);
            if (itemImage == null)
            {
                return null;
            }

            itemImage.ParseImage();
            string itemNodeName = itemId.ToString("D7");
            WzSubProperty itemProperty = itemImage[itemNodeName] as WzSubProperty;
            if (itemProperty == null)
            {
                return null;
            }

            WzSubProperty info = itemProperty["info"] as WzSubProperty;
            var chair = new PortableChair
            {
                ItemId = itemId,
                Name = ResolvePortableChairName(itemId),
                Description = ResolvePortableChairDescription(itemId),
                RecoveryHp = Math.Max(0, GetIntValue(info?["recoveryHP"]) ?? 0),
                RecoveryMp = Math.Max(0, GetIntValue(info?["recoveryMP"]) ?? 0),
                RequiredLevel = Math.Max(0, GetIntValue(info?["reqLevel"]) ?? GetIntValue(info?["reqLEV"]) ?? GetIntValue(info?["lv"]) ?? 0),
                SitActionId = GetIntValue(info?["sitAction"]),
                TamingMobItemId = GetIntValue(info?["tamingMob"]),
                IsCoupleChair = itemId / 1000 == 3012,
                CoupleDistanceX = GetIntValue(info?["distanceX"]),
                CoupleDistanceY = GetIntValue(info?["distanceY"]),
                CoupleMaxDiff = GetIntValue(info?["maxDiff"]),
                CoupleDirection = GetIntValue(info?["direction"])
            };

            LoadPortableChairLayers(itemProperty, chair.Layers);
            LoadPortableChairExpressionLayers(itemProperty, chair);
            LoadPortableChairItemEffectLayers(itemId, chair);

            if (chair.Layers.Count == 0
                && chair.CoupleSharedLayers.Count == 0
                && chair.CoupleMidpointLayers.Count == 0)
            {
                return null;
            }

            _portableChairCache[itemId] = chair;
            return chair;
        }

        internal static bool IsPortableChairItemId(int itemId)
        {
            return itemId > 0 && itemId / 10000 == 301;
        }

        public ItemEffectAnimationSet LoadItemEffectAnimationSet(int itemId)
        {
            if (itemId <= 0)
            {
                return null;
            }

            if (_itemEffectCache.TryGetValue(itemId, out ItemEffectAnimationSet cached))
            {
                return cached;
            }

            WzImage itemEffectImage = Program.FindImage("Effect", "ItemEff.img");
            if (itemEffectImage == null)
            {
                return null;
            }

            itemEffectImage.ParseImage();
            WzSubProperty itemEffectProperty = itemEffectImage[itemId.ToString()] as WzSubProperty;
            if (itemEffectProperty == null)
            {
                return null;
            }

            ItemEffectAnimationSet effectSet = CreateItemEffectAnimationSet(itemId, itemEffectProperty);
            if (effectSet == null)
            {
                return null;
            }

            _itemEffectCache[itemId] = effectSet;
            return effectSet;
        }

        public ItemEffectAnimationSet LoadActiveEffectItemAnimationSet(int itemId, out bool followOwner)
        {
            followOwner = false;
            if (itemId <= 0)
            {
                return null;
            }

            if (_activeEffectItemEffectCache.TryGetValue(itemId, out ItemEffectAnimationSet cached))
            {
                followOwner = cached.FollowOwner;
                return cached;
            }

            WzSubProperty effectProperty = ResolveActiveEffectItemEffectProperty(itemId);
            if (effectProperty == null)
            {
                return null;
            }

            followOwner = ResolveActiveEffectItemFollowOwner(effectProperty);
            ItemEffectAnimationSet effectSet = CreateItemEffectAnimationSet(itemId, effectProperty, loop: true);
            if (effectSet == null)
            {
                return null;
            }

            effectSet.FollowOwner = followOwner;
            _activeEffectItemEffectCache[itemId] = effectSet;
            return effectSet;
        }

        public ItemEffectAnimationSet LoadNewYearCardEffectAnimationSet(
            int itemId = RelationshipOverlayClientStringPoolText.NewYearCardDefaultItemId)
        {
            int resolvedItemId = itemId > 0
                ? itemId
                : RelationshipOverlayClientStringPoolText.NewYearCardDefaultItemId;
            if (_itemEffectCache.TryGetValue(resolvedItemId, out ItemEffectAnimationSet cached))
            {
                return cached;
            }

            WzSubProperty itemEffectProperty = ResolveNewYearCardEffectProperty(resolvedItemId);
            if (itemEffectProperty == null
                && resolvedItemId != RelationshipOverlayClientStringPoolText.NewYearCardDefaultItemId)
            {
                resolvedItemId = RelationshipOverlayClientStringPoolText.NewYearCardDefaultItemId;
                if (_itemEffectCache.TryGetValue(resolvedItemId, out cached))
                {
                    return cached;
                }

                itemEffectProperty = ResolveNewYearCardEffectProperty(resolvedItemId);
            }

            if (itemEffectProperty == null)
            {
                return null;
            }

            ItemEffectAnimationSet effectSet = CreateItemEffectAnimationSet(resolvedItemId, itemEffectProperty);
            if (effectSet == null)
            {
                return null;
            }

            _itemEffectCache[resolvedItemId] = effectSet;
            return effectSet;
        }

        private ItemEffectAnimationSet CreateItemEffectAnimationSet(
            int itemId,
            WzSubProperty itemEffectProperty,
            bool loop = false)
        {
            if (itemId <= 0 || itemEffectProperty == null)
            {
                return null;
            }

            var effectSet = new ItemEffectAnimationSet
            {
                ItemId = itemId
            };

            foreach (ItemEffectLayerSource layerSource in ResolveItemEffectLayerSources(itemEffectProperty))
            {
                PortableChairLayer layer = LoadPortableChairLayer(layerSource.LayerProperty, layerSource.LayerName, loop);
                if (layer == null || IsPlaceholderPortableChairLayer(layer))
                {
                    continue;
                }

                if (layerSource.IsSharedLayer)
                {
                    effectSet.SharedLayers.Add(layer);
                }
                else
                {
                    effectSet.OwnerLayers.Add(layer);
                }
            }

            if (effectSet.OwnerLayers.Count == 0 && effectSet.SharedLayers.Count == 0)
            {
                return null;
            }

            return effectSet;
        }

        private static WzSubProperty ResolveActiveEffectItemEffectProperty(int itemId)
        {
            if (!InventoryItemMetadataResolver.TryResolveImageSource(itemId, out string category, out string imagePath))
            {
                return null;
            }

            WzImage itemImage = Program.FindImage(category, imagePath);
            if (itemImage == null)
            {
                return null;
            }

            itemImage.ParseImage();
            foreach (string itemNodeName in ActiveEffectItemMotionBlurResolver.EnumerateItemNodeNames(category, imagePath, itemId))
            {
                if ((itemImage[itemNodeName] as WzSubProperty)?["effect"] is WzSubProperty effectProperty)
                {
                    return effectProperty;
                }
            }

            return null;
        }

        private static bool ResolveActiveEffectItemFollowOwner(WzSubProperty effectProperty)
        {
            if (effectProperty == null)
            {
                return false;
            }

            int? rootFollow = GetIntValue(effectProperty["follow"]);
            if (rootFollow.HasValue)
            {
                return ResolveActiveEffectItemFollowOwnerForParity(rootFollow, Array.Empty<int?>());
            }

            List<int?> layerFollowValues = new();
            foreach (ItemEffectLayerSource layerSource in ResolveItemEffectLayerSources(effectProperty))
            {
                layerFollowValues.Add(GetIntValue(layerSource.LayerProperty?["follow"]));
            }

            return ResolveActiveEffectItemFollowOwnerForParity(null, layerFollowValues);
        }

        internal static bool ResolveActiveEffectItemFollowOwnerForParity(
            int? rootFollowValue,
            IEnumerable<int?> layerFollowValues)
        {
            if (rootFollowValue.HasValue)
            {
                return rootFollowValue.Value != 0;
            }

            if (layerFollowValues == null)
            {
                return false;
            }

            foreach (int? layerFollowValue in layerFollowValues)
            {
                if (layerFollowValue.HasValue)
                {
                    return layerFollowValue.Value != 0;
                }
            }

            return false;
        }

        private static WzSubProperty ResolveNewYearCardEffectProperty(int itemId)
        {
            string effectPath = RelationshipOverlayClientStringPoolText.ResolveNewYearCardEffectPath(itemId);
            if (!TryParseSetItemEffectLink(effectPath, out string imageName, out string propertyPath))
            {
                return null;
            }

            WzImage effectImage = Program.FindImage("Effect", imageName);
            if (effectImage == null)
            {
                return null;
            }

            effectImage.ParseImage();
            return effectImage[propertyPath] as WzSubProperty;
        }

        public ItemEffectAnimationSet LoadCompletedSetItemEffectAnimationSet(int setItemId)
        {
            if (setItemId <= 0)
            {
                return null;
            }

            if (_completedSetEffectCache.TryGetValue(setItemId, out ItemEffectAnimationSet cached))
            {
                return cached;
            }

            WzSubProperty effectProperty = ResolveCompletedSetItemEffectProperty(setItemId);
            if (effectProperty == null)
            {
                return null;
            }

            PortableChairLayer layer = LoadPortableChairLayer(
                effectProperty,
                $"setItem/{setItemId}",
                loop: true);
            if (layer == null || IsPlaceholderPortableChairLayer(layer))
            {
                return null;
            }

            var effectSet = new ItemEffectAnimationSet
            {
                ItemId = setItemId
            };
            effectSet.OwnerLayers.Add(layer);
            _completedSetEffectCache[setItemId] = effectSet;
            return effectSet;
        }

        public RelationshipTextTagStyle LoadRelationshipTextTagStyle(RemoteRelationshipOverlayType relationshipType)
        {
            if (relationshipType != RemoteRelationshipOverlayType.NewYearCard)
            {
                return null;
            }

            if (_relationshipTextTagCache.TryGetValue(relationshipType, out RelationshipTextTagStyle cachedStyle))
            {
                return cachedStyle;
            }

            WzImage nameTagImage = Program.FindImage("UI", "NameTag.img");
            if (nameTagImage == null)
            {
                return null;
            }

            nameTagImage.ParseImage();
            WzSubProperty styleProperty = nameTagImage["11"] as WzSubProperty;
            if (styleProperty == null)
            {
                return null;
            }

            int textColorArgb = InfoTool.GetInt(styleProperty["clr"], unchecked((int)0xFFFFFFFF));
            var style = new RelationshipTextTagStyle
            {
                Left = LoadRelationshipTextTagTexture(styleProperty, "w"),
                Middle = LoadRelationshipTextTagTexture(styleProperty, "c"),
                Right = LoadRelationshipTextTagTexture(styleProperty, "e"),
                TextColor = new Color(unchecked((uint)textColorArgb))
            };

            if (!style.IsReady)
            {
                return null;
            }

            _relationshipTextTagCache[relationshipType] = style;
            return style;
        }

        public CarryItemEffectDefinition LoadCarryItemEffectDefinition()
        {
            if (_carryItemEffectCache?.IsReady == true)
            {
                return _carryItemEffectCache;
            }

            WzImage characterEffectImage = Program.FindImage("Effect", "CharacterEff.img");
            if (characterEffectImage == null)
            {
                return null;
            }

            characterEffectImage.ParseImage();
            var effect = new CarryItemEffectDefinition
            {
                BundleLayer = LoadCharacterEffectLayer(characterEffectImage, CarryItemEffectBundleItemId, "carry/bundle"),
                SingleLayerA = LoadCharacterEffectLayer(characterEffectImage, CarryItemEffectSingleAItemId, "carry/singleA"),
                SingleLayerB = LoadCharacterEffectLayer(characterEffectImage, CarryItemEffectSingleBItemId, "carry/singleB")
            };

            if (!effect.IsReady)
            {
                return null;
            }

            _carryItemEffectCache = effect;
            return effect;
        }

        public SkillAnimation LoadPacketOwnedEmotionEffectAnimation(string emotionName)
        {
            if (string.IsNullOrWhiteSpace(emotionName)
                || string.Equals(emotionName, "default", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return LoadPacketOwnedEmotionEffectAnimationCore($"Etc/EmotionEffect.img/{emotionName}");
        }

        private void LoadPortableChairLayers(WzSubProperty chairProperty, ICollection<PortableChairLayer> layers)
        {
            if (chairProperty == null || layers == null)
            {
                return;
            }

            foreach (WzImageProperty child in chairProperty.WzProperties)
            {
                if (child is not WzSubProperty layerProperty || child.Name.Equals("info", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                PortableChairLayer layer = LoadPortableChairLayer(layerProperty);
                if (layer != null)
                {
                    layers.Add(layer);
                }
            }
        }

        private void LoadPortableChairExpressionLayers(WzSubProperty chairProperty, PortableChair chair)
        {
            if (chairProperty == null || chair == null)
            {
                return;
            }

            if (chairProperty["info"] is not WzSubProperty infoProperty
                || infoProperty["randEffect"] is not WzSubProperty randomEffectProperty)
            {
                return;
            }

            foreach (WzImageProperty child in randomEffectProperty.WzProperties)
            {
                if (!int.TryParse(child.Name, out int effectIndex)
                    || child is not WzSubProperty expressionProperty)
                {
                    continue;
                }

                string expressionName = InfoTool.GetString(expressionProperty["face"]);
                if (string.IsNullOrWhiteSpace(expressionName))
                {
                    continue;
                }

                if (!TryResolvePortableChairExpressionLayerSource(
                        chairProperty,
                        effectIndex,
                        out WzSubProperty expressionLayerSource))
                {
                    continue;
                }

                List<PortableChairLayer> expressionLayers = new();
                LoadPortableChairLayers(expressionLayerSource, expressionLayers);
                if (expressionLayers.Count == 0)
                {
                    continue;
                }

                chair.ExpressionLayers[expressionName.Trim()] = expressionLayers;
            }
        }

        private void LoadPortableChairItemEffectLayers(int itemId, PortableChair chair)
        {
            if (chair == null)
            {
                return;
            }

            WzImage itemEffectImage = Program.FindImage("Effect", "ItemEff.img");
            if (itemEffectImage == null)
            {
                return;
            }

            itemEffectImage.ParseImage();
            WzSubProperty itemEffectProperty = itemEffectImage[itemId.ToString()] as WzSubProperty;
            if (itemEffectProperty == null)
            {
                return;
            }

            foreach (WzImageProperty child in itemEffectProperty.WzProperties)
            {
                if (child is not WzSubProperty layerProperty)
                {
                    continue;
                }

                PortableChairLayer layer = LoadPortableChairLayer(layerProperty, $"itemEff/{child.Name}", loop: true);
                if (layer == null || IsPlaceholderPortableChairLayer(layer))
                {
                    continue;
                }

                if (chair.IsCoupleChair && IsPortableChairCoupleMidpointLayer(child.Name))
                {
                    chair.CoupleMidpointLayers.Add(layer);
                    continue;
                }

                if (chair.IsCoupleChair && IsPortableChairCoupleSharedLayer(child.Name))
                {
                    chair.CoupleSharedLayers.Add(layer);
                    continue;
                }

                chair.Layers.Add(layer);
            }
        }

        private static bool IsPortableChairCoupleMidpointLayer(string layerName)
        {
            return string.Equals(layerName, "0", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryResolvePortableChairExpressionLayerSource(
            WzSubProperty chairProperty,
            int effectIndex,
            out WzSubProperty expressionLayerSource)
        {
            expressionLayerSource = null;
            if (chairProperty == null || effectIndex < 0)
            {
                return false;
            }

            if (effectIndex == 0)
            {
                expressionLayerSource = BuildPortableChairExpressionLayerSource(chairProperty);
                return expressionLayerSource != null;
            }

            if (chairProperty[$"randEffect{effectIndex}"] is not WzSubProperty randomEffectBranch)
            {
                return false;
            }

            expressionLayerSource = BuildPortableChairExpressionLayerSource(randomEffectBranch);
            return expressionLayerSource != null;
        }

        private static WzSubProperty BuildPortableChairExpressionLayerSource(WzSubProperty sourceProperty)
        {
            if (sourceProperty == null)
            {
                return null;
            }

            WzSubProperty effectProperty = sourceProperty["effect"] as WzSubProperty;
            WzSubProperty effect2Property = sourceProperty["effect2"] as WzSubProperty;
            if (effectProperty == null && effect2Property == null)
            {
                return null;
            }

            var expressionLayerSource = new WzSubProperty("expression");
            if (effectProperty != null)
            {
                expressionLayerSource.WzProperties.Add(effectProperty);
            }

            if (effect2Property != null)
            {
                expressionLayerSource.WzProperties.Add(effect2Property);
            }

            return expressionLayerSource;
        }

        private static bool IsPortableChairCoupleSharedLayer(string layerName)
        {
            return string.Equals(layerName, "1", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPlaceholderPortableChairLayer(PortableChairLayer layer)
        {
            CharacterFrame frame = layer?.Animation?.Frames?.FirstOrDefault();
            return frame?.Texture != null
                   && layer.Animation.Frames.Count == 1
                   && frame.Texture.Width <= 4
                   && frame.Texture.Height <= 4;
        }

        private Texture2D LoadRelationshipTextTagTexture(WzSubProperty parentProperty, string childName)
        {
            WzCanvasProperty canvas = parentProperty?[childName] as WzCanvasProperty;
            return canvas?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(_device);
        }

        private PortableChairLayer LoadCharacterEffectLayer(WzImage characterEffectImage, int effectItemId, string layerName)
        {
            if (characterEffectImage?[effectItemId.ToString(CultureInfo.InvariantCulture)] is not WzSubProperty effectProperty)
            {
                return null;
            }

            WzImageProperty layerProperty = effectProperty["0"] ?? effectProperty;
            PortableChairLayer layer = LoadPortableChairLayer(layerProperty, layerName, loop: true);
            return layer == null || IsPlaceholderPortableChairLayer(layer)
                ? null
                : layer;
        }

        private PortableChairLayer LoadPortableChairLayer(WzSubProperty layerProperty)
        {
            return LoadPortableChairLayer(layerProperty, layerProperty?.Name, loop: true);
        }

        private SkillAnimation LoadPacketOwnedEmotionEffectAnimationCore(string effectUol)
        {
            if (!TryResolveEffectAssetUol(effectUol, out string category, out string imageName, out string propertyPath))
            {
                return null;
            }

            WzImage image = Program.FindImage(category, imageName);
            if (image == null)
            {
                return null;
            }

            image.ParseImage();
            WzImageProperty property = ResolveWzProperty(image, propertyPath);
            if (property is not WzSubProperty subProperty)
            {
                return null;
            }

            SkillAnimation animation = CreatePacketOwnedEmotionEffectAnimation(subProperty);
            if (animation == null || animation.Frames.Count == 0)
            {
                return null;
            }

            animation.Name = effectUol;
            animation.Loop = false;
            animation.CalculateDuration();
            return animation;
        }

        private SkillAnimation CreatePacketOwnedEmotionEffectAnimation(WzSubProperty effectProperty)
        {
            if (effectProperty == null)
            {
                return null;
            }

            SkillAnimation animation = new SkillAnimation
            {
                Name = effectProperty.Name,
                Loop = false
            };

            foreach (WzCanvasProperty canvas in effectProperty.WzProperties.OfType<WzCanvasProperty>().OrderBy(static frame => ParsePacketOwnedEmotionFrameIndex(frame.Name)))
            {
                try
                {
                    if (canvas.GetLinkedWzCanvasBitmap() is not { } bitmap)
                    {
                        continue;
                    }

                    Texture2D texture = bitmap.ToTexture2DAndDispose(_device);
                    if (texture == null)
                    {
                        continue;
                    }

                    WzVectorProperty origin = canvas["origin"] as WzVectorProperty;
                    int delay = Math.Max(1, GetPacketOwnedEmotionIntValue(canvas["delay"], defaultValue: 100));
                    DXObject textureObject = new(0, 0, texture, delay)
                    {
                        Tag = canvas
                    };

                    animation.Frames.Add(new SkillFrame
                    {
                        Texture = textureObject,
                        Origin = new Point(origin?.X.Value ?? 0, origin?.Y.Value ?? 0),
                        Delay = delay,
                        Bounds = new Rectangle(0, 0, texture.Width, texture.Height),
                        AlphaStart = Math.Clamp(GetPacketOwnedEmotionIntValue(canvas["a0"], defaultValue: 255), 0, 255),
                        AlphaEnd = Math.Clamp(GetPacketOwnedEmotionIntValue(canvas["a1"], defaultValue: 255), 0, 255)
                    });
                }
                catch
                {
                    // Keep the render-safe subset when EmotionEffect frames are malformed.
                }
            }

            return animation;
        }

        private PortableChairLayer LoadPortableChairLayer(WzImageProperty layerProperty, string layerName, bool loop)
        {
            var animation = new CharacterAnimation
            {
                Action = CharacterAction.Custom,
                ActionName = layerName ?? layerProperty?.Name,
                Loop = loop
            };

            var orderedFrames = new List<(int Index, CharacterFrame Frame)>();
            int? relativeZ = GetIntValue(layerProperty?["z"]);
            foreach (WzImageProperty child in layerProperty.WzProperties)
            {
                if (!int.TryParse(child.Name, out int frameIndex)
                    || !TryResolvePortableChairFrameCanvas(child, out WzCanvasProperty canvas))
                {
                    continue;
                }

                CharacterFrame frame = LoadPortableChairFrame(child, canvas);
                if (frame == null)
                {
                    continue;
                }

                relativeZ ??= GetIntValue(canvas["z"]);
                orderedFrames.Add((frameIndex, frame));
            }

            if (orderedFrames.Count == 0)
            {
                return null;
            }

            orderedFrames.Sort((a, b) => a.Index.CompareTo(b.Index));
            foreach ((int _, CharacterFrame frame) in orderedFrames)
            {
                animation.Frames.Add(frame);
            }

            animation.CalculateTotalDuration();
            return new PortableChairLayer
            {
                Name = layerName ?? layerProperty.Name,
                Animation = animation,
                RelativeZ = relativeZ ?? 0,
                PositionHint = GetIntValue(layerProperty["pos"]) ?? 0
            };
        }

        private CharacterFrame LoadPortableChairFrame(WzImageProperty frameProperty, WzCanvasProperty canvas)
        {
            if (frameProperty == null || canvas == null)
            {
                return null;
            }

            CharacterFrame frame = LoadFrame(
                canvas,
                frameProperty.Name,
                frameProperty is WzUOLProperty uolProperty ? uolProperty.LinkValue?.FullPath : null);
            if (frame == null)
            {
                return null;
            }

            if (frameProperty != canvas)
            {
                if (GetIntValue(frameProperty["delay"]) is int delay)
                {
                    frame.Delay = delay;
                }

                if (frameProperty["origin"] is WzVectorProperty origin)
                {
                    frame.Origin = new Point(origin.X.Value, origin.Y.Value);
                }

                string z = GetStringValue(frameProperty["z"]);
                if (!string.IsNullOrWhiteSpace(z))
                {
                    frame.Z = ResolveZLayer(z, frameProperty.Name);
                }

                frame.Bounds = new Rectangle(
                    -frame.Origin.X,
                    -frame.Origin.Y,
                    frame.Texture?.Width ?? 0,
                    frame.Texture?.Height ?? 0);
            }

            return frame;
        }

        internal static IReadOnlyList<ItemEffectLayerSource> ResolveItemEffectLayerSources(WzSubProperty itemEffectProperty)
        {
            if (itemEffectProperty == null)
            {
                return Array.Empty<ItemEffectLayerSource>();
            }

            List<ItemEffectLayerSource> groupedLayers = new();
            foreach (WzImageProperty child in itemEffectProperty.WzProperties)
            {
                if (child is not WzSubProperty layerProperty
                    || string.Equals(child.Name, "fail", StringComparison.OrdinalIgnoreCase)
                    || !PortableChairLayerHasRenderableFrames(layerProperty))
                {
                    continue;
                }

                groupedLayers.Add(new ItemEffectLayerSource(
                    layerProperty,
                    $"itemEff/{child.Name}",
                    string.Equals(child.Name, "1", StringComparison.OrdinalIgnoreCase)));
            }

            if (groupedLayers.Count > 0)
            {
                return groupedLayers;
            }

            if (PortableChairLayerHasRenderableFrames(itemEffectProperty))
            {
                return new[]
                {
                    new ItemEffectLayerSource(itemEffectProperty, $"itemEff/{itemEffectProperty.Name}", false)
                };
            }

            if (itemEffectProperty["fail"] is WzSubProperty failProperty
                && PortableChairLayerHasRenderableFrames(failProperty))
            {
                return new[]
                {
                    new ItemEffectLayerSource(failProperty, $"itemEff/{itemEffectProperty.Name}/fail", false)
                };
            }

            return Array.Empty<ItemEffectLayerSource>();
        }

        internal static bool PortableChairLayerHasRenderableFrames(WzImageProperty layerProperty)
        {
            if (layerProperty?.WzProperties == null)
            {
                return false;
            }

            foreach (WzImageProperty child in layerProperty.WzProperties)
            {
                if (int.TryParse(child.Name, out _)
                    && TryResolvePortableChairFrameCanvas(child, out _))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool TryResolvePortableChairFrameCanvas(WzImageProperty property, out WzCanvasProperty canvas)
        {
            switch (property)
            {
                case WzCanvasProperty directCanvas:
                    canvas = directCanvas;
                    return true;

                case WzUOLProperty uol when uol.LinkValue is WzCanvasProperty linkedCanvas:
                    canvas = linkedCanvas;
                    return true;

                default:
                    canvas = null;
                    return false;
            }
        }

        internal readonly record struct ItemEffectLayerSource(
            WzImageProperty LayerProperty,
            string LayerName,
            bool IsSharedLayer);

        internal static bool TryParseSetItemEffectLink(
            string effectLink,
            out string imageName,
            out string propertyPath)
        {
            imageName = null;
            propertyPath = null;
            if (string.IsNullOrWhiteSpace(effectLink))
            {
                return false;
            }

            string normalized = effectLink.Trim().Replace('\\', '/');
            const string effectPrefix = "Effect/";
            if (normalized.StartsWith(effectPrefix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[effectPrefix.Length..];
            }

            int separatorIndex = normalized.IndexOf('/');
            if (separatorIndex <= 0 || separatorIndex >= normalized.Length - 1)
            {
                return false;
            }

            imageName = normalized[..separatorIndex].Trim();
            propertyPath = normalized[(separatorIndex + 1)..].Trim();
            return !string.IsNullOrWhiteSpace(imageName)
                   && !string.IsNullOrWhiteSpace(propertyPath);
        }

        private WzSubProperty ResolveCompletedSetItemEffectProperty(int setItemId)
        {
            WzImage setItemInfoImage = Program.FindImage("Etc", "SetItemInfo.img");
            if (setItemInfoImage == null)
            {
                return null;
            }

            setItemInfoImage.ParseImage();
            if (setItemInfoImage[setItemId.ToString(CultureInfo.InvariantCulture)] is not WzSubProperty setInfoProperty)
            {
                return null;
            }

            string effectLink = InfoTool.GetString(setInfoProperty["effectLink"]);
            if (TryParseSetItemEffectLink(effectLink, out string imageName, out string propertyPath))
            {
                WzImage linkedEffectImage = Program.FindImage("Effect", imageName);
                linkedEffectImage?.ParseImage();
                if (linkedEffectImage?[propertyPath] is WzSubProperty linkedEffectProperty
                    && PortableChairLayerHasRenderableFrames(linkedEffectProperty))
                {
                    return linkedEffectProperty;
                }
            }

            WzImage setItemEffectImage = Program.FindImage("Effect", "SetItemInfoEff.img");
            setItemEffectImage?.ParseImage();
            if (setItemEffectImage?[setItemId.ToString(CultureInfo.InvariantCulture)] is WzSubProperty directEffectProperty
                && PortableChairLayerHasRenderableFrames(directEffectProperty))
            {
                return directEffectProperty;
            }

            return null;
        }

        private static string ResolvePortableChairName(int itemId)
        {
            return Program.InfoManager?.ItemNameCache != null
                   && Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo)
                   && !string.IsNullOrWhiteSpace(itemInfo.Item2)
                ? itemInfo.Item2
                : $"Portable Chair {itemId}";
        }

        private static string ResolvePortableChairDescription(int itemId)
        {
            return Program.InfoManager?.ItemNameCache != null
                   && Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo)
                ? itemInfo.Item3
                : null;
        }

        #endregion

        #region Face Loading

        /// <summary>
        /// Load face from Face/xxxxx.img
        /// </summary>
        public FacePart LoadFace(int faceId)
        {
            if (_faceCache.TryGetValue(faceId, out var cached))
                return cached;

            string imgName = faceId.ToString("D8") + ".img";
            WzImage imgNode = null;

            // Try WzFile first
            var faceDir = _characterWz?.WzDirectory?["Face"];
            if (faceDir != null)
            {
                imgNode = faceDir[imgName] as WzImage;
            }

            // Fall back to getting Face directory via Program, then find image in it
            if (imgNode == null)
            {
                // First get the Character directory
                var charDirObj = Program.FindWzObject("Character", "");

                // Then get Face subdirectory from it
                if (charDirObj is MapleLib.Img.VirtualWzDirectory virtualCharDir)
                {
                    // Get Face subdirectory
                    var faceSubDir = virtualCharDir["Face"];

                    if (faceSubDir is MapleLib.Img.VirtualWzDirectory virtualFaceDir)
                    {
                        imgNode = virtualFaceDir[imgName] as WzImage;
                    }
                }
                else if (charDirObj is WzDirectory charWzDir)
                {
                    var faceWzDir = charWzDir["Face"] as WzDirectory;
                    if (faceWzDir != null)
                    {
                        imgNode = faceWzDir[imgName] as WzImage;
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"[CharacterLoader] LoadFace id={faceId}, found={imgNode != null}");

            if (imgNode == null)
                return null;

            var face = new FacePart
            {
                ItemId = faceId,
                Name = GetItemName(imgNode as WzImage) ?? $"Face_{faceId}",
                Type = CharacterPartType.Face,
                Slot = EquipSlot.None
            };

            // Load face expressions
            LoadFaceExpressions(face, imgNode as WzImage);

            _faceCache[faceId] = face;
            return face;
        }

        private void LoadFaceExpressions(FacePart face, WzImage img)
        {
            if (img == null) return;

            img.ParseImage();

            // Standard expressions
            string[] expressions = { "default", "blink", "hit", "smile", "troubled", "cry", "angry", "bewildered", "stunned", "oops" };

            foreach (var expr in expressions)
            {
                var exprNode = img[expr];
                if (exprNode != null)
                {
                    var anim = LoadFaceAnimation(exprNode);
                    if (anim != null && anim.Frames.Count > 0)
                    {
                        anim.ActionName = expr;
                        face.Expressions[expr] = anim;
                    }
                }
            }

            // Also check for numbered face actions
            foreach (WzImageProperty prop in img.WzProperties)
            {
                if (!expressions.Contains(prop.Name) && prop.Name != "info")
                {
                    var anim = LoadFaceAnimation(prop);
                    if (anim != null && anim.Frames.Count > 0)
                    {
                        anim.ActionName = prop.Name;
                        face.Expressions[prop.Name] = anim;
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Face loaded with {face.Expressions.Count} expressions");
        }

        /// <summary>
        /// Load face animation - face structure is expression/face/0 or expression/0
        /// </summary>
        private CharacterAnimation LoadFaceAnimation(WzImageProperty node)
        {
            if (node == null) return null;

            var anim = new CharacterAnimation();
            int? animationDelay = GetIntValue(node["delay"]);
            if (animationDelay.HasValue)
            {
                anim.AuthoredDuration = animationDelay.Value;
            }

            if (node is WzSubProperty subProp)
            {
                // Try direct numbered frames first (expression/0, expression/1)
                for (int i = 0; i < 100; i++)
                {
                    var frameNode = subProp[i.ToString()];
                    if (frameNode == null) break;

                    if (frameNode is WzCanvasProperty frameCanvas)
                    {
                        var frame = LoadFrame(frameCanvas, i.ToString());
                        if (frame != null)
                        {
                            ApplyFaceFrameDelayOverride(frame, frameNode, animationDelay);
                            anim.Frames.Add(frame);
                        }
                    }
                }

                // If no direct frames, check for "face" subnode (expression/face/0)
                if (anim.Frames.Count == 0)
                {
                    var faceNode = subProp["face"];
                    if (faceNode is WzSubProperty faceSub)
                    {
                        for (int i = 0; i < 100; i++)
                        {
                            var frameNode = faceSub[i.ToString()];
                            if (frameNode == null) break;

                            if (frameNode is WzCanvasProperty frameCanvas)
                            {
                                var frame = LoadFrame(frameCanvas, i.ToString());
                                if (frame != null)
                                {
                                    ApplyFaceFrameDelayOverride(frame, frameNode, animationDelay);
                                    anim.Frames.Add(frame);
                                }
                            }
                        }
                    }
                    // Also check if face is directly a canvas (expression/face is a single frame)
                    else if (faceNode is WzCanvasProperty faceCanvas)
                    {
                        var frame = LoadFrame(faceCanvas, "0");
                        if (frame != null)
                        {
                            ApplyFaceFrameDelayOverride(frame, faceNode, animationDelay);
                            anim.Frames.Add(frame);
                        }
                    }
                }
            }
            else if (node is WzCanvasProperty canvas)
            {
                var frame = LoadFrame(canvas, "0");
                if (frame != null)
                {
                    ApplyFaceFrameDelayOverride(frame, node, animationDelay);
                    anim.Frames.Add(frame);
                }
            }

            anim.CalculateTotalDuration();
            return anim;
        }

        private static void ApplyFaceFrameDelayOverride(CharacterFrame frame, WzImageProperty frameNode, int? animationDelay)
        {
            if (frame == null)
            {
                return;
            }

            int? frameDelay = GetIntValue(frameNode?["delay"]);
            if (frameDelay.HasValue)
            {
                frame.Delay = frameDelay.Value;
                return;
            }

            if (animationDelay.HasValue)
            {
                frame.Delay = animationDelay.Value;
            }
        }

        #endregion

        #region Hair Loading

        /// <summary>
        /// Load hair from Hair/xxxxx.img
        /// </summary>
        public HairPart LoadHair(int hairId)
        {
            if (_hairCache.TryGetValue(hairId, out var cached))
                return cached;

            string imgName = hairId.ToString("D8") + ".img";
            WzImage imgNode = null;

            // Try WzFile first
            var hairDir = _characterWz?.WzDirectory?["Hair"];
            if (hairDir != null)
            {
                imgNode = hairDir[imgName] as WzImage;
            }

            // Fall back - try to load hair image directly
            if (imgNode == null)
            {
                imgNode = Program.FindImage("Character", $"Hair/{imgName}");

                if (imgNode == null)
                {
                    var dataSource = Program.DataSource;
                    if (dataSource != null)
                    {
                        imgNode = dataSource.GetImage("Character", $"Hair/{imgName}");
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"[CharacterLoader] LoadHair id={hairId}, found={imgNode != null}");

            if (imgNode == null)
                return null;

            var hair = new HairPart
            {
                ItemId = hairId,
                Name = GetItemName(imgNode as WzImage) ?? $"Hair_{hairId}",
                Type = CharacterPartType.Hair,
                Slot = EquipSlot.None
            };

            LoadHairAnimations(hair, imgNode as WzImage);

            _hairCache[hairId] = hair;
            return hair;
        }

        private void LoadHairAnimations(HairPart hair, WzImage img)
        {
            if (img == null) return;

            img.ParseImage();

            // Load regular hair animations - include attack animations for proper character posing
            var allActions = BuildEagerActionLoadOrder(
                BuildAvailableActionSet(img.WzProperties.Select(prop => prop.Name)),
                includeAttackActions: false);
            foreach (var action in allActions)
            {
                var actionNode = img[action];
                if (actionNode != null)
                {
                    // Check for "hair" subnode - structure is action/hair/0, action/hair/1, etc.
                    var hairNode = actionNode["hair"];
                    if (hairNode != null)
                    {
                        var anim = LoadHairSubAnimation(hairNode);
                        if (anim != null && anim.Frames.Count > 0)
                        {
                            anim.ActionName = action;
                            anim.Action = CharacterPart.ParseActionString(action);
                            hair.Animations[action] = anim;
                        }
                    }

                    // Check for "hairOverHead"
                    var overHeadNode = actionNode["hairOverHead"];
                    if (overHeadNode != null)
                    {
                        var anim = LoadHairSubAnimation(overHeadNode);
                        if (anim != null && anim.Frames.Count > 0)
                        {
                            anim.ActionName = action + "_overHead";
                            // Store separately or merge - for now just log
                        }
                    }

                    // Check for "backHair" or "hairBelowBody"
                    var backNode = actionNode["hairBelowBody"] ?? actionNode["backHair"];
                    if (backNode != null)
                    {
                        hair.HasBackHair = true;
                        var anim = LoadHairSubAnimation(backNode);
                        if (anim != null && anim.Frames.Count > 0)
                        {
                            anim.ActionName = action;
                            hair.BackHairAnimations[action] = anim;
                        }
                    }
                }
            }

        }

        /// <summary>
        /// Load hair sub-animation - structure is hair/0, hair/1, etc. or direct canvas
        /// </summary>
        private CharacterAnimation LoadHairSubAnimation(WzImageProperty node)
        {
            if (node == null) return null;

            var anim = new CharacterAnimation();

            if (node is WzSubProperty subProp)
            {
                // Try numbered frames (hair/0, hair/1)
                for (int i = 0; i < 100; i++)
                {
                    var frameNode = subProp[i.ToString()];
                    if (frameNode == null) break;

                    if (frameNode is WzCanvasProperty frameCanvas)
                    {
                        var frame = LoadFrame(frameCanvas, i.ToString());
                        if (frame != null) anim.Frames.Add(frame);
                    }
                }
            }
            else if (node is WzCanvasProperty canvas)
            {
                // Single frame
                var frame = LoadFrame(canvas, "0");
                if (frame != null) anim.Frames.Add(frame);
            }

            anim.CalculateTotalDuration();
            return anim;
        }

        #endregion

        #region Equipment Loading

        /// <summary>
        /// Load equipment from appropriate folder
        /// </summary>
        public CharacterPart LoadEquipment(int itemId)
        {
            if (_equipCache.TryGetValue(itemId, out var cached))
            {
                int resolvedWeeklyVariantIndex = ResolveClientWeeklyVariantIndex(DateTime.Now.DayOfWeek);
                if (!cached.HasWeeklyVariant || cached.ResolvedWeeklyVariantIndex == resolvedWeeklyVariantIndex)
                {
                    return cached?.Clone();
                }
            }

            // Determine equipment folder based on ID range
            string folder = GetEquipmentFolder(itemId);
            if (folder == null)
                return null;

            CharacterImageEntry imageEntry = GetCharacterImageEntry(itemId, folder);
            System.Diagnostics.Debug.WriteLine($"[CharacterLoader] LoadEquipment id={itemId}, folder={folder}, found={imageEntry?.BaseImage != null}, weeklyOverride={imageEntry?.UsesWeeklyVariantOverride == true}, weeklyIndex={imageEntry?.ResolvedWeeklyVariantIndex ?? -1}");
            if (imageEntry?.BaseImage == null)
                return null;

            CharacterPart part;
            EquipSlot slot = GetEquipSlot(itemId);

            // Create appropriate part type
            if (folder == "Weapon")
            {
                part = LoadWeapon(imageEntry, itemId, slot);
            }
            else
            {
                part = new CharacterPart
                {
                    ItemId = itemId,
                    Name = GetItemName(imageEntry.BaseImage) ?? $"Equip_{itemId}",
                    Type = GetPartType(folder),
                    Slot = slot
                };

                LoadPartAnimations(part, imageEntry.ActionSourceRoot, includeAttackActions: false);
            }

            if (part != null)
            {
                // Load info
                LoadEquipInfo(part, imageEntry);
                part.UsesWeeklyVariantOverride = imageEntry.UsesWeeklyVariantOverride;
                part.ResolvedWeeklyVariantIndex = imageEntry.ResolvedWeeklyVariantIndex;
                AttachTamingMobOverlayResolver(part);
                AttachTamingMobActionFrameOwner(part);
                _equipCache[itemId] = part;
            }

            return part?.Clone();
        }

        public CharacterPart LoadTamingMob(int itemId)
        {
            CharacterPart part = LoadEquipment(itemId);
            AttachTamingMobActionFrameOwner(part);
            return part;
        }

        private WeaponPart LoadWeapon(CharacterImageEntry imageEntry, int itemId, EquipSlot slot)
        {
            WzImage img = imageEntry?.BaseImage;
            if (img == null) return null;

            var weapon = new WeaponPart
            {
                ItemId = itemId,
                Name = GetItemName(img) ?? $"Weapon_{itemId}",
                Type = CharacterPartType.Weapon,
                Slot = slot
            };

            img.ParseImage();

            // Load weapon info
            var info = img["info"];
            if (info != null)
            {
                LoadEquipInfo(weapon, imageEntry);
                CharacterImageEntryMetadata metadata = imageEntry?.Metadata;
                weapon.AttackSpeed = metadata?.WeaponAttackSpeed ?? 6;
                weapon.WalkFrameCount = metadata?.WeaponWalkFrameCount ?? 0;
                weapon.StandFrameCount = metadata?.WeaponStandFrameCount ?? 0;
                weapon.AttackFrameCount = metadata?.WeaponAttackFrameCount ?? 0;
                weapon.Attack = weapon.BonusWeaponAttack;
                weapon.WeaponType = ResolveWeaponType(itemId);
                weapon.AfterImageType = metadata?.WeaponAfterImageType;
                weapon.IsTwoHanded = GetIntValue(info["twoHanded"]) == 1;
            }

            // Load animations - weapon structure is action/frame/weapon (e.g., stand1/0/weapon)
            LoadWeaponAnimations(weapon, imageEntry?.ActionSourceRoot ?? img);

            return weapon;
        }

        /// <summary>
        /// Load weapon animations - handles weapon-specific structure
        /// Weapon structure: action/frame/weapon (e.g., stand1/0/weapon)
        /// </summary>
        private void LoadWeaponAnimations(WeaponPart weapon, WzObject actionSourceRoot)
        {
            if (weapon == null || actionSourceRoot == null) return;

            HashSet<string> availableActions = BuildAvailableActionSet(EnumerateChildProperties(actionSourceRoot).Select(prop => prop.Name));
            weapon.AvailableAnimations = availableActions;
            weapon.AnimationResolver = actionName => LoadWeaponAnimationForAction(actionSourceRoot, actionName);

            var allActions = BuildEagerActionLoadOrder(
                availableActions,
                includeAttackActions: false);

            foreach (var action in allActions)
            {
                var actionNode = GetChildProperty(actionSourceRoot, action);
                if (actionNode == null) continue;

                var anim = LoadWeaponAnimation(actionNode, action);
                if (anim != null && anim.Frames.Count > 0)
                {
                    anim.ActionName = action;
                    anim.Action = CharacterPart.ParseActionString(action);
                    weapon.Animations[action] = anim;
                }
            }
        }

        private CharacterAnimation LoadWeaponAnimationForAction(WzObject actionSourceRoot, string actionName)
        {
            if (actionSourceRoot == null || string.IsNullOrWhiteSpace(actionName))
            {
                return null;
            }

            string cacheKey = $"weapon::{actionSourceRoot.FullPath ?? actionSourceRoot.Name ?? "<unknown>"}::{actionName}";
            if (_animationCache.TryGetValue(cacheKey, out CharacterAnimation cachedAnimation))
            {
                return cachedAnimation;
            }

            WzImageProperty actionNode = GetChildProperty(actionSourceRoot, actionName);
            if (actionNode == null)
            {
                return null;
            }

            CharacterAnimation animation = LoadWeaponAnimation(actionNode, actionName);
            if (animation != null && animation.Frames.Count > 0)
            {
                animation.ActionName = actionName;
                animation.Action = CharacterPart.ParseActionString(actionName);
                _animationCache[cacheKey] = animation;
            }

            return animation;
        }

        /// <summary>
        /// Load weapon animation - structure is action/frame/weapon or action/weapon/frame
        /// </summary>
        private CharacterAnimation LoadWeaponAnimation(WzImageProperty actionNode, string actionName)
        {
            if (actionNode == null) return null;

            var anim = new CharacterAnimation();

            if (actionNode is WzSubProperty actionSub)
            {
                // Try numbered frames (action/0/weapon, action/1/weapon, etc.)
                for (int i = 0; i < 100; i++)
                {
                    var frameNode = actionSub[i.ToString()];
                    if (frameNode == null) break;

                    if (frameNode is WzSubProperty frameSub)
                    {
                        // Look for "weapon" subnode
                        var weaponNode = frameSub["weapon"];
                        if (weaponNode is WzCanvasProperty weaponCanvas)
                        {
                            var frame = LoadWeaponFrame(weaponCanvas, frameSub, i.ToString(), actionName);
                            if (frame != null)
                            {
                                anim.Frames.Add(frame);
                            }
                        }
                        else if (weaponNode is WzUOLProperty weaponUol)
                        {
                            // Resolve UOL link
                            var resolved = weaponUol.LinkValue;
                            if (resolved is WzCanvasProperty resolvedCanvas)
                            {
                                var frame = LoadWeaponFrame(resolvedCanvas, frameSub, i.ToString(), actionName);
                                if (frame != null)
                                {
                                    anim.Frames.Add(frame);
                                }
                            }
                        }
                        // Also check for direct canvas (some weapons have this structure)
                        else
                        {
                            foreach (WzImageProperty child in frameSub.WzProperties)
                            {
                                if (child is WzCanvasProperty childCanvas)
                                {
                                    var frame = LoadWeaponFrame(childCanvas, frameSub, child.Name, actionName);
                                    if (frame != null)
                                    {
                                        anim.Frames.Add(frame);
                                        break; // Take first canvas
                                    }
                                }
                            }
                        }
                    }
                    else if (frameNode is WzCanvasProperty directCanvas)
                    {
                        // Direct canvas at frame level
                        var frame = LoadWeaponFrame(directCanvas, null, i.ToString(), actionName);
                        if (frame != null)
                        {
                            anim.Frames.Add(frame);
                        }
                    }
                }

                // If no frames found, try alternate structure (action/weapon/0, action/weapon/1)
                if (anim.Frames.Count == 0)
                {
                    var weaponNode = actionSub["weapon"];
                    if (weaponNode is WzSubProperty weaponSub)
                    {
                        for (int i = 0; i < 100; i++)
                        {
                            var frameNode = weaponSub[i.ToString()];
                            if (frameNode == null) break;

                            if (frameNode is WzCanvasProperty frameCanvas)
                            {
                                var frame = LoadWeaponFrame(frameCanvas, null, i.ToString(), actionName);
                                if (frame != null)
                                {
                                    anim.Frames.Add(frame);
                                }
                            }
                        }
                    }
                    else if (weaponNode is WzCanvasProperty singleCanvas)
                    {
                        // Single weapon frame
                        var frame = LoadWeaponFrame(singleCanvas, null, "0", actionName);
                        if (frame != null)
                        {
                            anim.Frames.Add(frame);
                        }
                    }
                }
            }

            anim.CalculateTotalDuration();
            return anim;
        }

        /// <summary>
        /// Load a weapon frame with proper z-layer handling.
        /// Weapon z-layer may come from the canvas or parent frame node.
        /// </summary>
        private CharacterFrame LoadWeaponFrame(WzCanvasProperty canvas, WzSubProperty parentFrame, string frameName, string actionName = null)
        {
            if (canvas == null) return null;

            var frame = LoadFrame(canvas, frameName);
            if (frame == null) return null;

            // Get z-layer from canvas first, then parent frame
            string zLayer = ResolveZLayer(GetStringValue(canvas["z"]), frameName);

            if (string.IsNullOrEmpty(zLayer) || !ZMapReference.HasZLayer(zLayer))
            {
                // Try parent frame's z property
                if (parentFrame != null)
                {
                    zLayer = ResolveZLayer(GetStringValue(parentFrame["z"]), frameName);
                }
            }

            // If still no valid z-layer, determine based on action type
            if (string.IsNullOrEmpty(zLayer) || !ZMapReference.HasZLayer(zLayer))
            {
                // Use context-aware z-layer based on animation type
                zLayer = GetDefaultWeaponZLayer(actionName);
            }

            frame.Z = zLayer;

            return frame;
        }

        /// <summary>
        /// Get the default z-layer for a weapon based on the action type.
        /// Standing poses: weapon behind hand (so hand appears to grip it)
        /// Attack poses: weapon in front (so swing is visible)
        /// </summary>
        private static string GetDefaultWeaponZLayer(string actionName)
        {
            if (string.IsNullOrEmpty(actionName))
                return "weaponBelowHand";

            string actionLower = actionName.ToLowerInvariant();

            // Attack animations - weapon should be more visible (in front)
            if (actionLower.StartsWith("swing") ||
                actionLower.StartsWith("stab") ||
                actionLower.StartsWith("shoot") ||
                actionLower.StartsWith("pronestab") ||
                actionLower.StartsWith("attack"))
            {
                return "weapon";
            }

            // Standing/idle poses - weapon behind hand (hand grips weapon)
            if (actionLower.StartsWith("stand") ||
                actionLower.StartsWith("walk") ||
                actionLower.StartsWith("jump") ||
                actionLower.StartsWith("sit") ||
                actionLower.StartsWith("prone") ||
                actionLower.StartsWith("ladder") ||
                actionLower.StartsWith("rope") ||
                actionLower.StartsWith("alert"))
            {
                return "weaponBelowHand";
            }

            // Default to behind hand for unknown actions
            return "weaponBelowHand";
        }

        private void LoadEquipInfo(CharacterPart part, CharacterImageEntry imageEntry)
        {
            WzImage img = imageEntry?.BaseImage;
            if (img == null) return;

            var info = img["info"];
            if (info == null) return;

            CharacterImageEntryMetadata metadata = imageEntry.Metadata;
            part.VSlot = metadata?.VSlot;
            part.ISlot = metadata?.ISlot;
            part.Sfx = metadata?.Sfx;
            part.IsCash = metadata?.IsCash == true;
            part.HasWeeklyVariant = metadata?.HasWeeklyVariant == true;
            part.RequiredJobMask = GetIntValue(info["reqJob"]) ?? 0;
            part.RequiredLevel = GetIntValue(info["reqLevel"]) ?? 0;
            part.RequiredSTR = GetIntValue(info["reqSTR"]) ?? 0;
            part.RequiredDEX = GetIntValue(info["reqDEX"]) ?? 0;
            part.RequiredINT = GetIntValue(info["reqINT"]) ?? 0;
            part.RequiredLUK = GetIntValue(info["reqLUK"]) ?? 0;
            part.RequiredFame = GetIntValue(info["reqPOP"]) ?? 0;
            part.BonusSTR = GetIntValue(info["incSTR"]) ?? 0;
            part.BonusDEX = GetIntValue(info["incDEX"]) ?? 0;
            part.BonusINT = GetIntValue(info["incINT"]) ?? 0;
            part.BonusLUK = GetIntValue(info["incLUK"]) ?? 0;
            part.BonusHP = GetIntValue(info["incMHP"]) ?? 0;
            part.BonusMP = GetIntValue(info["incMMP"]) ?? 0;
            part.BonusHPPercent = GetIntValue(info["incMHPr"]) ?? 0;
            part.BonusMPPercent = GetIntValue(info["incMMPr"]) ?? 0;
            part.BonusWeaponAttack = GetIntValue(info["incPAD"]) ?? 0;
            part.BonusMagicAttack = GetIntValue(info["incMAD"]) ?? 0;
            part.BonusWeaponDefense = GetIntValue(info["incPDD"]) ?? 0;
            part.BonusMagicDefense = GetIntValue(info["incMDD"]) ?? 0;
            part.BonusAccuracy = GetIntValue(info["incACC"]) ?? 0;
            part.BonusAvoidability = GetIntValue(info["incEVA"]) ?? 0;
            part.BonusHands = GetIntValue(info["incCraft"]) ?? 0;
            part.BonusSpeed = GetIntValue(info["incSpeed"]) ?? 0;
            part.BonusJump = GetIntValue(info["incJump"]) ?? 0;
            part.UpgradeSlots = GetIntValue(info["tuc"]) ?? 0;
            part.KnockbackRate = GetIntValue(info["knockback"]) ?? 0;
            part.TradeAvailable = GetIntValue(info["tradeAvailable"]) ?? 0;
            part.IsTradeBlocked = GetIntValue(info["tradeBlock"]) == 1;
            part.IsEquipTradeBlocked = GetIntValue(info["equipTradeBlock"]) == 1;
            part.IsOneOfAKind = GetIntValue(info["only"]) == 1;
            part.IsUniqueEquipItem = GetIntValue(info["onlyEquip"]) == 1;
            part.IsNotForSale = GetIntValue(info["notSale"]) == 1;
            part.IsAccountSharable = GetIntValue(info["accountSharable"]) == 1;
            part.HasAccountShareTag = GetIntValue(info["accountShareTag"]) == 1;
            part.IsNoMoveToLocker = GetIntValue(info["noMoveToLocker"]) == 1;
            part.IsTimeLimited = GetIntValue(info["timeLimited"]) == 1;
            part.MaxDurability = GetIntValue(info["durability"]);
            part.Durability = part.MaxDurability;
            part.SellPrice = GetIntValue(info["price"]) ?? 0;
            part.IsEpic = GetIntValue(info["epic"]) == 1;
            ApplyGrowthInfo(info, part);
            if (info["dateExpire"] is WzStringProperty expirationProperty)
            {
                DateTime? expirationDate = expirationProperty.GetDateTime();
                if (expirationDate.HasValue)
                {
                    part.ExpirationDateUtc = DateTime.SpecifyKind(expirationDate.Value, DateTimeKind.Utc);
                }
            }

            if (Program.InfoManager?.ItemNameCache != null
                && Program.InfoManager.ItemNameCache.TryGetValue(part.ItemId, out Tuple<string, string, string> itemInfo))
            {
                part.ItemCategory = itemInfo.Item1;
                if (!string.IsNullOrWhiteSpace(itemInfo.Item2))
                {
                    part.Name = itemInfo.Item2;
                }

                part.Description = itemInfo.Item3;
            }

            // Load icon
            var iconNode = info["icon"];
            if (iconNode is WzCanvasProperty canvas)
            {
                part.Icon = LoadTexture(canvas);
            }

            var iconRawNode = info["iconRaw"];
            if (iconRawNode is WzCanvasProperty canvasRaw)
            {
                part.IconRaw = LoadTexture(canvasRaw);
            }
        }

        private static void AttachTamingMobActionFrameOwner(CharacterPart part)
        {
            if (part?.Slot != EquipSlot.TamingMob)
            {
                return;
            }

            part.TamingMobActionFrameOwner ??= new TamingMobActionFrameOwner(part.ItemId);
        }

        private static void AttachMorphActionFrameOwner(CharacterPart part)
        {
            if (part?.Type != CharacterPartType.Morph)
            {
                return;
            }

            part.MorphActionFrameOwner ??= new MorphActionFrameOwner(
                part.ItemId,
                actionName => part.AnimationResolver?.Invoke(actionName));
        }

        private void AttachTamingMobOverlayResolver(CharacterPart part)
        {
            if (part?.ItemId / 10000 is not (191 or 192))
            {
                return;
            }

            part.TamingMobActionOverlayResolver ??= (baseVehicleId, actionName) =>
                LoadTamingMobActionSourceAnimation(baseVehicleId, part.ItemId, actionName);
        }

        private CharacterAnimation LoadTamingMobActionSourceAnimation(int baseVehicleId, int sourceItemId, string actionName)
        {
            if (baseVehicleId <= 0 || sourceItemId <= 0 || string.IsNullOrWhiteSpace(actionName))
            {
                return null;
            }

            string imageName = sourceItemId.ToString("D8") + ".img";
            WzImage image = GetCharacterImage($"TamingMob/{imageName}");
            if (image == null)
            {
                return null;
            }

            WzImageProperty actionNode = sourceItemId == baseVehicleId
                ? image[actionName]
                : image[baseVehicleId.ToString(CultureInfo.InvariantCulture)]?[actionName];
            if (actionNode == null)
            {
                return null;
            }

            CharacterAnimation animation = LoadAnimation(actionNode);
            if (animation?.Frames?.Count > 0)
            {
                animation.ActionName = actionName;
                animation.Action = CharacterPart.ParseActionString(actionName);
            }

            return animation;
        }

        private static void ApplyGrowthInfo(WzImageProperty info, CharacterPart part)
        {
            if (info?["level"] is not WzSubProperty levelProperty
                || levelProperty["info"] is not WzSubProperty levelInfoProperty)
            {
                return;
            }

            int growthMaxLevel = 0;
            foreach (WzImageProperty child in levelInfoProperty.WzProperties)
            {
                if (int.TryParse(child?.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedLevel))
                {
                    growthMaxLevel = Math.Max(growthMaxLevel, parsedLevel);
                }
            }

            if (growthMaxLevel <= 0)
            {
                return;
            }

            part.HasGrowthInfo = true;
            part.GrowthLevel = 1;
            part.GrowthMaxLevel = growthMaxLevel;
            part.GrowthExpPercent = 0;
        }

        private string GetEquipmentFolder(int itemId)
        {
            // Equipment ID ranges
            int category = itemId / 10000;
            return category switch
            {
                100 => "Cap",
                101 => "Accessory",  // Face accessory
                102 => "Accessory",  // Eye accessory
                103 => "Earrings",
                104 => "Coat",
                105 => "Longcoat",
                106 => "Pants",
                107 => "Shoes",
                108 => "Glove",
                109 => "Shield",
                110 => "Cape",
                111 => "Ring",
                112 => "Accessory",
                113 => "Accessory",
                114 => "Accessory",
                115 => "Accessory",
                116 => "Accessory",
                118 => "Accessory",
                166 => "Android",
                167 => "Android",
                170 => "Weapon",
                134 => "Weapon",
                >= 130 and < 170 => "Weapon",
                180 => "TamingMob",
                198 => "TamingMob",
                199 => "TamingMob",
                >= 190 and < 200 => "TamingMob",
                _ => null
            };
        }

        private EquipSlot GetEquipSlot(int itemId)
        {
            int category = itemId / 10000;
            return category switch
            {
                100 => EquipSlot.Cap,
                101 => EquipSlot.FaceAccessory,
                102 => EquipSlot.EyeAccessory,
                103 => EquipSlot.Earrings,
                104 => EquipSlot.Coat,
                105 => EquipSlot.Longcoat,
                106 => EquipSlot.Pants,
                107 => EquipSlot.Shoes,
                108 => EquipSlot.Glove,
                109 => EquipSlot.Shield,
                110 => EquipSlot.Cape,
                111 => EquipSlot.Ring1,
                112 => EquipSlot.Pendant,
                113 => EquipSlot.Belt,
                114 => EquipSlot.Medal,
                115 => EquipSlot.Shoulder,
                116 => EquipSlot.Pocket,
                118 => EquipSlot.Badge,
                166 => EquipSlot.Android,
                167 => EquipSlot.AndroidHeart,
                170 => EquipSlot.Weapon,
                134 => EquipSlot.Shield,
                >= 130 and < 170 => EquipSlot.Weapon,
                180 => EquipSlot.TamingMob,
                198 => EquipSlot.TamingMob,
                191 => EquipSlot.Saddle,
                192 => EquipSlot.TamingMobAccessory,
                199 => EquipSlot.TamingMob,
                >= 190 and < 200 => EquipSlot.TamingMob,
                _ => EquipSlot.None
            };
        }

        private CharacterPartType GetPartType(string folder)
        {
            return folder switch
            {
                "Cap" => CharacterPartType.Cap,
                "Accessory" => CharacterPartType.Accessory,
                "Earrings" => CharacterPartType.Earrings,
                "Coat" => CharacterPartType.Coat,
                "Longcoat" => CharacterPartType.Longcoat,
                "Pants" => CharacterPartType.Pants,
                "Shoes" => CharacterPartType.Shoes,
                "Glove" => CharacterPartType.Glove,
                "Shield" => CharacterPartType.Shield,
                "Cape" => CharacterPartType.Cape,
                "Weapon" => CharacterPartType.Weapon,
                "TamingMob" => CharacterPartType.TamingMob,
                "Android" => CharacterPartType.Accessory,
                _ => CharacterPartType.Body
            };
        }

        #endregion

        #region Animation Loading

        private void LoadPartAnimations(CharacterPart part, WzObject actionSourceRoot, bool includeAttackActions = true)
        {
            if (part == null || actionSourceRoot == null) return;

            HashSet<string> availableActions = BuildAvailableActionSet(EnumerateChildProperties(actionSourceRoot).Select(prop => prop.Name));
            part.AvailableAnimations = availableActions;
            part.AnimationResolver = actionName => LoadAnimationForAction(actionSourceRoot, actionName);

            // Startup only needs the common locomotion and attack families. Less common
            // actions are resolved lazily the first time the assembler requests them.
            var actionsToLoad = BuildEagerActionLoadOrder(availableActions, includeAttackActions);

            foreach (var action in actionsToLoad)
            {
                CharacterAnimation anim = LoadAnimationForAction(actionSourceRoot, action);
                if (anim != null && anim.Frames.Count > 0)
                {
                    part.Animations[action] = anim;
                }
            }
        }

        private static HashSet<string> BuildAvailableActionSet(IEnumerable<string> availableActionNames)
        {
            var availableActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (availableActionNames == null)
            {
                return availableActions;
            }

            foreach (string actionName in availableActionNames)
            {
                if (LooksLikeActionName(actionName))
                {
                    availableActions.Add(actionName);
                }
            }

            return availableActions;
        }

        private static IReadOnlyList<string> BuildEagerActionLoadOrder(ISet<string> availableActionNames, bool includeAttackActions)
        {
            var orderedActions = new List<string>();
            if (availableActionNames == null || availableActionNames.Count == 0)
            {
                return orderedActions;
            }

            foreach (string action in StandardActions)
            {
                if (availableActionNames.Contains(action))
                {
                    orderedActions.Add(action);
                }
            }

            if (includeAttackActions)
            {
                foreach (string action in AttackActions)
                {
                    if (availableActionNames.Contains(action))
                    {
                        orderedActions.Add(action);
                    }
                }
            }

            return orderedActions;
        }

        private static bool LooksLikeActionName(string name)
        {
            return !string.IsNullOrWhiteSpace(name)
                   && !NonActionProperties.Contains(name)
                   && !name.StartsWith("_", StringComparison.Ordinal);
        }

        private static bool LooksLikePublishedMorphAction(WzImageProperty actionNode)
        {
            if (actionNode == null || !LooksLikeActionName(actionNode.Name))
            {
                return false;
            }

            if (actionNode is WzUOLProperty actionUol
                && actionUol.GetLinkedWzImageProperty() is WzImageProperty linkedActionNode)
            {
                return LooksLikePublishedMorphActionFrameContainer(linkedActionNode);
            }

            return LooksLikePublishedMorphActionFrameContainer(actionNode);
        }

        private static bool LooksLikePublishedMorphActionFrameContainer(WzImageProperty actionNode)
        {
            foreach (KeyValuePair<string, WzImageProperty> frameEntry in EnumerateMorphFrameNodes(actionNode as WzSubProperty))
            {
                if (TryResolveMorphFrameCanvas(frameEntry.Value, out _))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveMorphFrameCanvas(WzImageProperty frameNode, out WzCanvasProperty frameCanvas)
        {
            frameCanvas = null;
            if (frameNode is WzCanvasProperty directCanvas)
            {
                frameCanvas = directCanvas;
                return true;
            }

            if (frameNode is WzUOLProperty frameUol
                && frameUol.GetLinkedWzImageProperty() is WzCanvasProperty linkedCanvas)
            {
                frameCanvas = linkedCanvas;
                return true;
            }

            return false;
        }

        internal static bool LooksLikePublishedMorphActionForTesting(WzImageProperty actionNode)
        {
            return LooksLikePublishedMorphAction(actionNode);
        }

        [Conditional("DEBUG")]
        private void LogAnimationFallbackOnce(string fallbackKey, WzSubProperty firstFrame, WzObject resolvedUolTarget)
        {
            if (!EnableAnimationFallbackDiagnostics)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(fallbackKey) || !_loggedAnimationFallbackKeys.Add(fallbackKey))
            {
                return;
            }

            string frameContents = firstFrame == null
                ? "none"
                : string.Join(", ", firstFrame.WzProperties.Select(static child => $"{child.Name}:{child.GetType().Name}"));
            string resolvedType = resolvedUolTarget?.GetType().Name ?? "NULL";
            Debug.WriteLine($"[LoadAnimation] Fallback scan for '{fallbackKey}' because direct numbered frames contained no canvas. Frame0 children: {frameContents}. First resolved UOL type: {resolvedType}.");
        }

        private CharacterAnimation LoadAnimationForAction(WzObject actionSourceRoot, string actionName)
        {
            if (actionSourceRoot == null || string.IsNullOrWhiteSpace(actionName))
            {
                return null;
            }

            string cacheKey = $"part::{actionSourceRoot.FullPath ?? actionSourceRoot.Name ?? "<unknown>"}::{actionName}";
            if (_animationCache.TryGetValue(cacheKey, out CharacterAnimation cachedAnimation))
            {
                return cachedAnimation;
            }

            WzImageProperty actionNode = GetChildProperty(actionSourceRoot, actionName);
            if (actionNode == null)
            {
                return null;
            }

            CharacterAnimation animation = LoadAnimation(actionNode);
            if (animation != null && animation.Frames.Count > 0)
            {
                animation.ActionName = actionName;
                animation.Action = CharacterPart.ParseActionString(actionName);
                _animationCache[cacheKey] = animation;
            }

            return animation;
        }

        private CharacterAnimation LoadAnimation(WzImageProperty node, string debugContext = null)
        {
            if (node == null) return null;

            if (node is WzUOLProperty actionUol && actionUol.LinkValue is WzImageProperty linkedActionNode)
            {
                return LoadAnimation(linkedActionNode, debugContext);
            }

            var anim = new CharacterAnimation();

            // Check if node is a direct canvas (single frame)
            if (node is WzCanvasProperty canvas)
            {
                var frame = LoadFrame(canvas, "0");
                if (frame != null)
                {
                    anim.Frames.Add(frame);
                }
            }
            // Check for numbered frame children
            else if (node is WzSubProperty subProp)
            {
                // First, try direct numbered frames (0, 1, 2, ...)
                bool foundDirectFrames = false;
                for (int i = 0; i < 100; i++)
                {
                    var frameNode = subProp[i.ToString()];
                    if (frameNode == null)
                        break;

                    foundDirectFrames = true;
                    if (frameNode is WzCanvasProperty frameCanvas)
                    {
                        var frame = LoadFrame(frameCanvas, i.ToString());
                        if (frame != null)
                        {
                            anim.Frames.Add(frame);
                        }
                    }
                    else if (frameNode is WzUOLProperty frameUol && frameUol.LinkValue is WzCanvasProperty resolvedFrameCanvas)
                    {
                        var frame = LoadFrame(
                            resolvedFrameCanvas,
                            i.ToString(),
                            frameUol.Value);
                        if (frame != null)
                        {
                            anim.Frames.Add(frame);
                        }
                    }
                    else if (frameNode is WzSubProperty frameSub)
                    {
                        // Body images contain multiple parts: body, arm, lHand, rHand, head, etc.
                        // We need to load ALL parts as sub-parts for correct rendering
                        CharacterFrame bodyFrame = LoadBodyFrameWithSubParts(frameSub, i.ToString());
                        if (bodyFrame != null)
                        {
                            anim.Frames.Add(bodyFrame);
                        }
                    }
                }

                // If direct numbered frames found but no canvases added, check for head canvas inside each frame
                // Structure: stand1/0/head, stand1/1/head (for head images)
                if (foundDirectFrames && anim.Frames.Count == 0)
                {
                    string fallbackKey = debugContext
                        ?? node.FullPath
                        ?? node.Name
                        ?? "<unknown>";
                    WzSubProperty firstFrameSub = subProp["0"] as WzSubProperty;
                    WzObject firstResolvedUolTarget = (firstFrameSub?["head"] as WzUOLProperty)?.LinkValue;
                    LogAnimationFallbackOnce(fallbackKey, firstFrameSub, firstResolvedUolTarget);
                    for (int i = 0; i < 100; i++)
                    {
                        var frameNode = subProp[i.ToString()];
                        if (frameNode == null)
                            break;

                        if (frameNode is WzSubProperty frameSub)
                        {
                            // Look for "head" property inside the frame
                            var headProp = frameSub["head"];
                            WzCanvasProperty headCanvas = null;

                            // Resolve UOL (User Object Link) to get actual canvas
                            if (headProp is WzUOLProperty uol)
                            {
                                var resolved = uol.LinkValue;
                                if (resolved is WzCanvasProperty resolvedCanvas)
                                {
                                    headCanvas = resolvedCanvas;
                                }
                            }
                            else if (headProp is WzCanvasProperty directCanvas)
                            {
                                headCanvas = directCanvas;
                            }

                            if (headCanvas != null)
                            {
                                var frame = LoadFrame(
                                    headCanvas,
                                    i.ToString(),
                                    (headProp as WzUOLProperty)?.Value);
                                if (frame != null)
                                {
                                    anim.Frames.Add(frame);
                                }
                            }
                            // Also try looking for first canvas or UOL with any name
                            else
                            {
                                foreach (WzImageProperty child in frameSub.WzProperties)
                                {
                                    WzCanvasProperty foundCanvas = null;
                                    if (child is WzCanvasProperty childCanvas2)
                                    {
                                        foundCanvas = childCanvas2;
                                    }
                                    else if (child is WzUOLProperty childUol2)
                                    {
                                        if (childUol2.LinkValue is WzCanvasProperty resolvedCanvas2)
                                        {
                                            foundCanvas = resolvedCanvas2;
                                        }
                                    }

                                    if (foundCanvas != null)
                                    {
                                        var frame = LoadFrame(
                                            foundCanvas,
                                            child.Name,
                                            (child as WzUOLProperty)?.Value);
                                        if (frame != null)
                                        {
                                            anim.Frames.Add(frame);
                                            break; // Take first canvas
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // If no direct numbered frames, check for "head" subnode (alternate structure)
                if (!foundDirectFrames && anim.Frames.Count == 0)
                {
                    var headNode = subProp["head"];

                    if (headNode is WzSubProperty headSub)
                    {
                        for (int i = 0; i < 100; i++)
                        {
                            var frameNode = headSub[i.ToString()];
                            if (frameNode == null)
                                break;

                            if (frameNode is WzCanvasProperty frameCanvas)
                            {
                                var frame = LoadFrame(frameCanvas, i.ToString());
                                if (frame != null)
                                {
                                    anim.Frames.Add(frame);
                                }
                            }
                        }
                    }
                }
            }

            anim.CalculateTotalDuration();
            return anim;
        }

        private CharacterImageEntry GetCharacterImageEntry(int itemId, string folder, WzImage exactImage = null)
        {
            WzImage baseImage = exactImage ?? GetEquipmentImage(folder, itemId);
            if (baseImage == null)
            {
                return null;
            }

            baseImage.ParseImage();

            bool hasWeeklyVariant = GetIntValue(baseImage["info"]?["weekly"]) == 1;
            WzObject actionSourceRoot = baseImage;
            bool usesWeeklyVariantOverride = false;
            int resolvedWeeklyVariantIndex = -1;

            if (hasWeeklyVariant
                && TryResolveWeeklyActionSource(baseImage, DateTime.Now.DayOfWeek, out WzImageProperty weeklyActionRoot, out resolvedWeeklyVariantIndex))
            {
                actionSourceRoot = weeklyActionRoot;
                usesWeeklyVariantOverride = true;
            }

            return new CharacterImageEntry
            {
                BaseImage = baseImage,
                ActionSourceRoot = actionSourceRoot,
                Metadata = ReadCharacterImageEntryMetadata(baseImage),
                HasWeeklyVariant = hasWeeklyVariant,
                UsesWeeklyVariantOverride = usesWeeklyVariantOverride,
                ResolvedWeeklyVariantIndex = usesWeeklyVariantOverride ? resolvedWeeklyVariantIndex : -1
            };
        }

        private CharacterImageEntryMetadata ReadCharacterImageEntryMetadata(WzImage baseImage)
        {
            if (baseImage == null)
            {
                return new CharacterImageEntryMetadata();
            }

            baseImage.ParseImage();
            WzImageProperty info = baseImage["info"];
            if (info == null)
            {
                return new CharacterImageEntryMetadata();
            }

            return new CharacterImageEntryMetadata
            {
                ISlot = GetStringValue(info["islot"]),
                VSlot = GetStringValue(info["vslot"]),
                Sfx = GetStringValue(info["sfx"]),
                IsCash = GetIntValue(info["cash"]) == 1,
                HasWeeklyVariant = GetIntValue(info["weekly"]) == 1,
                WeaponAfterImageType = GetStringValue(info["afterImage"]),
                WeaponWalkFrameCount = GetIntValue(info["walk"]) ?? 0,
                WeaponStandFrameCount = GetIntValue(info["stand"]) ?? 0,
                WeaponAttackFrameCount = GetIntValue(info["attack"]) ?? 0,
                WeaponAttackSpeed = GetIntValue(info["attackSpeed"]) ?? 6
            };
        }

        private WzImage GetEquipmentImage(string folder, int itemId)
        {
            if (string.IsNullOrWhiteSpace(folder) || itemId <= 0)
            {
                return null;
            }

            string imgName = itemId.ToString("D8") + ".img";
            string cacheKey = $"{folder}/{imgName}";
            if (_characterImageCache.TryGetValue(cacheKey, out WzImage cachedImage))
            {
                return cachedImage;
            }

            var equipDir = _characterWz?.WzDirectory?[folder];
            if (equipDir?[imgName] is WzImage directImage)
            {
                _characterImageCache[cacheKey] = directImage;
                return directImage;
            }

            WzImage resolvedImage = Program.FindImage("Character", $"{folder}/{imgName}");
            _characterImageCache[cacheKey] = resolvedImage;
            return resolvedImage;
        }

        internal static int ResolveClientWeeklyVariantIndex(DayOfWeek dayOfWeek)
        {
            return dayOfWeek switch
            {
                DayOfWeek.Monday => 0,
                DayOfWeek.Tuesday => 1,
                DayOfWeek.Wednesday => 2,
                DayOfWeek.Thursday => 3,
                DayOfWeek.Friday => 4,
                DayOfWeek.Saturday => 5,
                _ => -1
            };
        }

        internal static bool TryResolveWeeklyActionSource(
            WzImage baseImage,
            DayOfWeek dayOfWeek,
            out WzImageProperty weeklyActionRoot,
            out int resolvedWeeklyVariantIndex)
        {
            weeklyActionRoot = null;
            resolvedWeeklyVariantIndex = ResolveClientWeeklyVariantIndex(dayOfWeek);
            if (baseImage == null || resolvedWeeklyVariantIndex < 0)
            {
                return false;
            }

            baseImage.ParseImage();

            WzImageProperty weeklyRoot = baseImage["weekly"];
            if (weeklyRoot == null)
            {
                return false;
            }

            weeklyActionRoot = weeklyRoot[resolvedWeeklyVariantIndex.ToString(CultureInfo.InvariantCulture)];
            return weeklyActionRoot != null;
        }

        private static IEnumerable<WzImageProperty> EnumerateChildProperties(WzObject sourceRoot)
        {
            return sourceRoot switch
            {
                WzImage image => image.WzProperties.Cast<WzImageProperty>(),
                WzImageProperty property => property.WzProperties.Cast<WzImageProperty>(),
                _ => Enumerable.Empty<WzImageProperty>()
            };
        }

        private static WzImageProperty GetChildProperty(WzObject sourceRoot, string name)
        {
            return sourceRoot switch
            {
                WzImage image => image[name],
                WzImageProperty property => property[name],
                _ => null
            };
        }

        private CharacterFrame LoadFrame(WzCanvasProperty canvas, string frameName, string frameUol = null)
        {
            if (canvas == null) return null;

            var texture = LoadTexture(canvas);
            if (texture == null) return null;

            // Default to 200ms for character animations (MapleStory typically uses 200-300ms per frame)
            // The delay may be overridden by the parent frame node in LoadBodyFrameWithSubParts
            var frame = new CharacterFrame
            {
                Texture = texture,
                Delay = GetIntValue(canvas["delay"]) ?? 200,
                FrameUol = frameUol
            };

            // Load origin
            var origin = canvas["origin"];
            if (origin is WzVectorProperty originVec)
            {
                frame.Origin = new Point(originVec.X.Value, originVec.Y.Value);
            }

            // Load z-layer
            frame.Z = ResolveZLayer(GetStringValue(canvas["z"]), frameName);

            // Load map points
            var mapNode = canvas["map"];
            if (mapNode is WzSubProperty mapSub)
            {
                foreach (WzImageProperty mapPoint in mapSub.WzProperties)
                {
                    if (mapPoint is WzVectorProperty vec)
                    {
                        frame.Map[mapPoint.Name] = new Point(vec.X.Value, vec.Y.Value);
                    }
                }
            }

            foreach (WzImageProperty child in canvas.WzProperties)
            {
                if (child is not WzVectorProperty vectorProperty
                    || frame.Map.ContainsKey(child.Name)
                    || string.Equals(child.Name, "origin", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(child.Name, "lt", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(child.Name, "rb", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                frame.Map[child.Name] = new Point(vectorProperty.X.Value, vectorProperty.Y.Value);
            }

            // Calculate bounds
            frame.Bounds = new Rectangle(
                -frame.Origin.X,
                -frame.Origin.Y,
                canvas.PngProperty?.Width ?? 0,
                canvas.PngProperty?.Height ?? 0);

            return frame;
        }

        /// <summary>
        /// Load a body frame with all sub-parts (body, arm, lHand, rHand)
        /// Each sub-part is positioned relative to the body's navel
        /// </summary>
        private CharacterFrame LoadBodyFrameWithSubParts(WzSubProperty frameSub, string frameName)
        {
            if (frameSub == null) return null;

            // List of body sub-parts to load, in z-order (back to front)
            // These are the standard parts in a body animation frame
            string[] subPartNames = { "body", "arm", "lHand", "rHand" };

            CharacterFrame bodyFrame = null;
            Point bodyNavel = Point.Zero;

            // Get the frame delay from the parent frame node (stand1/0/delay), not from sub-parts
            // This is where MapleStory stores the animation timing
            int frameDelay = GetIntValue(frameSub["delay"]) ?? 200; // Default to 200ms for character animations

            // First pass: load the main "body" canvas to get navel reference point
            var bodyCanvas = frameSub["body"] as WzCanvasProperty;
            if (bodyCanvas != null)
            {
                bodyFrame = LoadFrame(bodyCanvas, "body");
                if (bodyFrame != null)
                {
                    bodyNavel = bodyFrame.Map.ContainsKey("navel") ? bodyFrame.Map["navel"] : Point.Zero;
                    // Override with frame-level delay
                    bodyFrame.Delay = frameDelay;
                }
            }

            // Fallback: if no "body" canvas, take first canvas
            if (bodyFrame == null)
            {
                foreach (WzImageProperty child in frameSub.WzProperties)
                {
                    if (child is WzCanvasProperty childCanvas)
                    {
                        bodyFrame = LoadFrame(childCanvas, child.Name);
                        if (bodyFrame != null)
                        {
                            bodyNavel = bodyFrame.Map.ContainsKey("navel") ? bodyFrame.Map["navel"] : Point.Zero;
                            // Override with frame-level delay
                            bodyFrame.Delay = frameDelay;
                            break;
                        }
                    }
                }
            }

            if (bodyFrame == null) return null;

            // Second pass: load all sub-parts
            foreach (var partName in subPartNames)
            {
                var partCanvas = frameSub[partName] as WzCanvasProperty;
                if (partCanvas == null) continue;

                var subPart = LoadSubPart(partCanvas, partName, bodyNavel);
                if (subPart != null)
                {
                    bodyFrame.SubParts.Add(subPart);
                }
            }

            // Extract "hand" map point from "arm" canvas for weapon positioning
            // The arm is positioned relative to body via navel, so:
            // hand_in_body_coords = body.navel - arm.navel + arm.hand
            var armCanvas = frameSub["arm"] as WzCanvasProperty;
            if (armCanvas != null)
            {
                var armMap = armCanvas["map"];
                if (armMap is WzSubProperty armMapSub)
                {
                    Point armNavel = Point.Zero;
                    Point armHand = Point.Zero;
                    bool hasArmHand = false;

                    foreach (WzImageProperty mapPoint in armMapSub.WzProperties)
                    {
                        if (mapPoint is WzVectorProperty vec)
                        {
                            if (mapPoint.Name == "navel")
                                armNavel = new Point(vec.X.Value, vec.Y.Value);
                            else if (mapPoint.Name == "hand")
                            {
                                armHand = new Point(vec.X.Value, vec.Y.Value);
                                hasArmHand = true;
                            }
                        }
                    }

                    // Calculate hand position in body's coordinate system
                    if (hasArmHand)
                    {
                        bodyFrame.Map["hand"] = new Point(
                            bodyNavel.X - armNavel.X + armHand.X,
                            bodyNavel.Y - armNavel.Y + armHand.Y);
                    }
                }
            }

            // Also check for "lHand" (left hand) which may have handMove for weapon motion
            var lHandCanvas = frameSub["lHand"] as WzCanvasProperty;
            if (lHandCanvas != null)
            {
                var lHandMap = lHandCanvas["map"];
                if (lHandMap is WzSubProperty lHandMapSub)
                {
                    Point lHandNavel = Point.Zero;
                    Point lHandMove = Point.Zero;
                    bool hasHandMove = false;

                    foreach (WzImageProperty mapPoint in lHandMapSub.WzProperties)
                    {
                        if (mapPoint is WzVectorProperty vec)
                        {
                            if (mapPoint.Name == "navel")
                                lHandNavel = new Point(vec.X.Value, vec.Y.Value);
                            else if (mapPoint.Name == "handMove")
                            {
                                lHandMove = new Point(vec.X.Value, vec.Y.Value);
                                hasHandMove = true;
                            }
                        }
                    }

                    if (hasHandMove)
                    {
                        bodyFrame.Map["handMove"] = new Point(
                            bodyNavel.X - lHandNavel.X + lHandMove.X,
                            bodyNavel.Y - lHandNavel.Y + lHandMove.Y);
                    }
                }
            }

            return bodyFrame;
        }

        /// <summary>
        /// Load a single sub-part (body, arm, lHand, rHand) with its position relative to body navel
        /// </summary>
        private CharacterSubPart LoadSubPart(WzCanvasProperty canvas, string partName, Point bodyNavel)
        {
            if (canvas == null) return null;

            var texture = LoadTexture(canvas);
            if (texture == null) return null;

            var subPart = new CharacterSubPart
            {
                Name = partName,
                Texture = texture
            };

            // Load origin
            var origin = canvas["origin"];
            if (origin is WzVectorProperty originVec)
            {
                subPart.Origin = new Point(originVec.X.Value, originVec.Y.Value);
            }

            // Load z-layer
            subPart.Z = ResolveZLayer(GetStringValue(canvas["z"]), partName);

            // Load map points
            var mapNode = canvas["map"];
            if (mapNode is WzSubProperty mapSub)
            {
                foreach (WzImageProperty mapPoint in mapSub.WzProperties)
                {
                    if (mapPoint is WzVectorProperty vec)
                    {
                        subPart.Map[mapPoint.Name] = new Point(vec.X.Value, vec.Y.Value);
                    }
                }
            }

            // Calculate offset from body navel
            // Each sub-part has its own navel point that should align with body's navel
            Point partNavel = subPart.Map.ContainsKey("navel") ? subPart.Map["navel"] : Point.Zero;
            subPart.NavelOffset = new Point(
                bodyNavel.X - partNavel.X,
                bodyNavel.Y - partNavel.Y);

            return subPart;
        }

        private IDXObject LoadTexture(WzCanvasProperty canvas)
        {
            if (canvas?.PngProperty == null) return null;

            try
            {
                var bitmap = canvas.GetLinkedWzCanvasBitmap();
                if (bitmap == null) return null;

                var texture = bitmap.ToTexture2DAndDispose(_device);
                if (texture == null) return null;

                var origin = canvas["origin"] as WzVectorProperty;
                int originX = origin?.X.Value ?? 0;
                int originY = origin?.Y.Value ?? 0;
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

        #endregion

        #region Utility

        private string GetItemName(WzImage img)
        {
            if (img == null) return null;

            img.ParseImage();
            var info = img["info"];
            if (info == null) return null;

            // Try to get name from String.wz or embedded
            return GetStringValue(info["name"]);
        }

        private static int? GetIntValue(WzImageProperty prop)
        {
            return prop switch
            {
                WzIntProperty intProp => intProp.Value,
                WzShortProperty shortProp => shortProp.Value,
                WzLongProperty longProp => (int)longProp.Value,
                WzStringProperty strProp => int.TryParse(strProp.Value, out int v) ? v : null,
                _ => null
            };
        }

        private static string GetStringValue(WzImageProperty prop)
        {
            return prop switch
            {
                WzStringProperty strProp => strProp.Value,
                WzIntProperty intProp => intProp.Value.ToString(),
                _ => null
            };
        }

        private static string ResolveZLayer(string zLayer, string fallback)
        {
            if (!string.IsNullOrEmpty(zLayer) && ZMapReference.HasZLayer(zLayer))
            {
                return zLayer;
            }

            if (!string.IsNullOrEmpty(fallback) && ZMapReference.HasZLayer(fallback))
            {
                return fallback;
            }

            return zLayer ?? fallback;
        }

        private static bool TryResolveEffectAssetUol(string uol, out string category, out string imageName, out string propertyPath)
        {
            category = "Etc";
            imageName = null;
            propertyPath = null;

            if (string.IsNullOrWhiteSpace(uol))
            {
                return false;
            }

            string[] segments = uol.Replace('\\', '/').Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 3)
            {
                return false;
            }

            category = segments[0];
            int imageSegmentIndex = Array.FindIndex(segments, segment => segment.EndsWith(".img", StringComparison.OrdinalIgnoreCase));
            if (imageSegmentIndex < 1 || imageSegmentIndex >= segments.Length - 1)
            {
                return false;
            }

            imageName = string.Join("/", segments, 1, imageSegmentIndex);
            propertyPath = string.Join("/", segments, imageSegmentIndex + 1, segments.Length - imageSegmentIndex - 1);
            return !string.IsNullOrWhiteSpace(imageName) && !string.IsNullOrWhiteSpace(propertyPath);
        }

        private static WzImageProperty ResolveWzProperty(WzImage image, string propertyPath)
        {
            if (image == null || string.IsNullOrWhiteSpace(propertyPath))
            {
                return null;
            }

            string[] segments = propertyPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return null;
            }

            WzImageProperty current = image[segments[0]];
            for (int i = 1; i < segments.Length && current != null; i++)
            {
                current = current[segments[i]];
            }

            return current;
        }

        private static int ParsePacketOwnedEmotionFrameIndex(string frameName)
        {
            return int.TryParse(frameName, out int frameIndex) ? frameIndex : int.MaxValue;
        }

        private static int GetPacketOwnedEmotionIntValue(WzImageProperty property, int defaultValue)
        {
            return property switch
            {
                WzIntProperty intProperty => intProperty.Value,
                WzShortProperty shortProperty => shortProperty.Value,
                WzLongProperty longProperty => (int)Math.Clamp(longProperty.Value, int.MinValue, int.MaxValue),
                WzStringProperty stringProperty when int.TryParse(stringProperty.Value, out int parsedValue) => parsedValue,
                _ => defaultValue
            };
        }

        #endregion

        #region Presets

        public sealed class SimulatorDefaultAvatarSelection
        {
            public CharacterGender Gender { get; init; }
            public string Name { get; init; }
            public SkinColor Skin { get; init; }
            public int FaceId { get; init; }
            public int HairId { get; init; }
            public int Level { get; init; }
            public int JobId { get; init; }
            public string JobName { get; init; }
            public IReadOnlyDictionary<EquipSlot, int> EquipmentItemIdsBySlot { get; init; }
        }

        private static string ResolveWeaponType(int itemId)
        {
            int weaponCode = Math.Abs(itemId / 10000) % 100;
            return weaponCode switch
            {
                30 => "1h sword",
                31 => "1h axe",
                32 => "1h blunt",
                33 => "dagger",
                34 => "katara",
                36 => "cane",
                37 => "wand",
                38 => "staff",
                39 => "knuckle",
                40 => "2h sword",
                41 => "2h axe",
                42 => "2h blunt",
                43 => "spear",
                44 => "polearm",
                45 => "bow",
                46 => "crossbow",
                47 => "claw",
                48 => "knuckle",
                49 => "gun",
                50 => "shovel",
                51 => "pickaxe",
                52 => "double bowgun",
                53 => "cannon",
                56 => "shining rod",
                57 => "desperado",
                58 => "soul shooter",
                59 => "ancient bow",
                _ => "weapon"
            };
        }

        /// <summary>
        /// Load a default male character
        /// </summary>
        public CharacterBuild LoadDefaultMale()
        {
            return LoadDefaultAvatar(CharacterGender.Male);
        }

        /// <summary>
        /// Load a default female character
        /// </summary>
        public CharacterBuild LoadDefaultFemale()
        {
            return LoadDefaultAvatar(CharacterGender.Female);
        }

        /// <summary>
        /// Load a random character build
        /// </summary>
        public CharacterBuild LoadRandom()
        {
            CharacterGender gender = Random.Shared.Next(2) == 0 ? CharacterGender.Male : CharacterGender.Female;
            StarterAvatarRandomizationCatalog starterCatalog = GetStarterAvatarRandomizationCatalog(gender);
            SkinColor skin = PickRandomCandidate(starterCatalog.Skins, SkinColor.Light);
            int faceId = PickRandomCandidate(
                starterCatalog.FaceIds,
                gender == CharacterGender.Male ? DefaultMaleFaceId : DefaultFemaleFaceId);
            int hairId = PickRandomCandidate(
                starterCatalog.HairIds,
                gender == CharacterGender.Male ? DefaultMaleHairId : DefaultFemaleHairId);

            CharacterBuild build = new CharacterBuild
            {
                Gender = gender,
                Skin = skin,
                Body = LoadBody(skin),
                Head = LoadHead(skin),
                Face = LoadFace(faceId) ?? LoadFace(gender == CharacterGender.Male ? DefaultMaleFaceId : DefaultFemaleFaceId),
                Hair = LoadHair(hairId) ?? LoadHair(gender == CharacterGender.Male ? DefaultMaleHairId : DefaultFemaleHairId),
                Name = "Random",
                Job = 0,
                JobName = "Beginner"
            };

            AttachEquipmentResolver(build);
            EquipRandomStarterGear(build, starterCatalog);
            return build;
        }

        public LoginStarterAvatarCatalog GetLoginStarterAvatarCatalog(
            LoginCreateCharacterRaceKind race,
            CharacterGender gender)
        {
            return new LoginStarterAvatarCatalog
            {
                Skins = ResolveStarterSkinCandidates(gender, race),
                FaceIds = ResolveStarterFaceCandidates(gender, race),
                HairStyleIds = ResolveStarterHairStyleCandidates(gender, race),
                HairColorIndices = ResolveStarterHairColorCandidates(gender, race),
                CoatIds = ResolveStarterEquipmentCandidates(gender, 4, race),
                PantsIds = ResolveStarterEquipmentCandidates(gender, 5, race),
                ShoesIds = ResolveStarterEquipmentCandidates(gender, 6, race),
                WeaponIds = ResolveStarterEquipmentCandidates(gender, 7, race)
            };
        }

        public int ResolveLoginStarterHairId(
            LoginStarterAvatarCatalog catalog,
            CharacterGender gender,
            int hairStyleIndex,
            int hairColorIndex)
        {
            int baseHairId = PickCatalogValue(
                catalog?.HairStyleIds,
                hairStyleIndex,
                gender == CharacterGender.Male ? DefaultMaleHairId : DefaultFemaleHairId);
            int color = PickCatalogValue(catalog?.HairColorIndices, hairColorIndex, 0);

            int exactHairId = NormalizeHairStyleId(baseHairId) + Math.Max(0, color);
            if (LoadHair(exactHairId) != null)
            {
                return exactHairId;
            }

            if (LoadHair(baseHairId) != null)
            {
                return baseHairId;
            }

            return gender == CharacterGender.Male ? DefaultMaleHairId : DefaultFemaleHairId;
        }

        public int ResolveLoginStarterHairId(CharacterGender gender, int hairStyleId, int hairColorValue)
        {
            int exactHairId = NormalizeHairStyleId(hairStyleId) + Math.Max(0, hairColorValue);
            if (LoadHair(exactHairId) != null)
            {
                return exactHairId;
            }

            if (LoadHair(hairStyleId) != null)
            {
                return hairStyleId;
            }

            return gender == CharacterGender.Male ? DefaultMaleHairId : DefaultFemaleHairId;
        }

        public CharacterBuild LoadLoginStarterBuild(
            CharacterGender gender,
            SkinColor skin,
            int faceId,
            int hairId,
            int coatId,
            int pantsId,
            int shoesId,
            int weaponId)
        {
            var build = new CharacterBuild
            {
                Gender = gender,
                Skin = skin,
                Body = LoadBody(skin),
                Head = LoadHead(skin),
                Face = LoadFace(faceId) ?? LoadFace(gender == CharacterGender.Male ? DefaultMaleFaceId : DefaultFemaleFaceId),
                Hair = LoadHair(hairId) ?? LoadHair(gender == CharacterGender.Male ? DefaultMaleHairId : DefaultFemaleHairId),
                Name = "Starter",
                Job = 0,
                JobName = "Beginner"
            };

            AttachEquipmentResolver(build);
            EquipDefaultItem(build, coatId, nameof(EquipSlot.Coat), DefaultWizetSuitId);
            EquipDefaultItem(build, pantsId, nameof(EquipSlot.Pants), DefaultWizetPantsId);
            EquipDefaultItem(build, shoesId, nameof(EquipSlot.Shoes), DefaultLeatherSandalsId);
            EquipDefaultItem(build, weaponId, nameof(EquipSlot.Weapon), DefaultWizetSuitcaseId);
            return build;
        }

        private StarterAvatarRandomizationCatalog GetStarterAvatarRandomizationCatalog(CharacterGender gender)
        {
            if (_starterAvatarCatalogCache.TryGetValue(gender, out StarterAvatarRandomizationCatalog cachedCatalog))
            {
                return cachedCatalog;
            }

            // Login.img/NewChar and CustomizeChar/* expose eight avatar selection rows plus the dice randomizer,
            // so the simulator limits its non-packet starter pool to the first eight loadable options per classic id family.
            StarterAvatarRandomizationCatalog catalog = new()
            {
                Skins = ResolveStarterSkinCandidates(),
                FaceIds = ResolveStarterFaceCandidates(gender),
                HairIds = ResolveStarterHairCandidates(gender),
                CoatIds = ResolveStarterEquipmentCandidates(gender, 4),
                PantsIds = ResolveStarterEquipmentCandidates(gender, 5),
                ShoesIds = ResolveStarterEquipmentCandidates(gender, 6),
                WeaponIds = ResolveStarterEquipmentCandidates(gender, 7)
            };

            _starterAvatarCatalogCache[gender] = catalog;
            return catalog;
        }

        private IReadOnlyList<SkinColor> ResolveStarterSkinCandidates()
        {
            return ResolveStarterSkinCandidates(CharacterGender.Male, LoginCreateCharacterRaceKind.Explorer);
        }

        private IReadOnlyList<SkinColor> ResolveStarterSkinCandidates(CharacterGender gender, LoginCreateCharacterRaceKind race)
        {
            List<int> makeCharInfoValues = ResolveMakeCharInfoStarterValues(gender, "2", race);
            if (makeCharInfoValues.Count == 0)
            {
                makeCharInfoValues = ResolveMakeCharInfoStarterValues(
                    gender == CharacterGender.Male ? CharacterGender.Female : CharacterGender.Male,
                    "2",
                    race);
            }

            List<SkinColor> skins = new();
            foreach (int value in makeCharInfoValues)
            {
                if (!Enum.IsDefined(typeof(SkinColor), value))
                {
                    continue;
                }

                SkinColor skin = (SkinColor)value;
                if (LoadBody(skin) != null && LoadHead(skin) != null)
                {
                    skins.Add(skin);
                }
            }

            if (skins.Count == 0)
            {
                foreach (SkinColor skin in PreferredStarterSkins)
                {
                    if (LoadBody(skin) != null && LoadHead(skin) != null)
                    {
                        skins.Add(skin);
                    }
                }
            }

            if (skins.Count == 0)
            {
                skins.Add(SkinColor.Light);
            }

            return skins;
        }

        private IReadOnlyList<int> ResolveStarterFaceCandidates(CharacterGender gender)
        {
            return ResolveStarterFaceCandidates(gender, LoginCreateCharacterRaceKind.Explorer);
        }

        private IReadOnlyList<int> ResolveStarterFaceCandidates(CharacterGender gender, LoginCreateCharacterRaceKind race)
        {
            int defaultFaceId = gender == CharacterGender.Male ? DefaultMaleFaceId : DefaultFemaleFaceId;
            List<int> makeCharInfoValues = ResolveMakeCharInfoStarterValues(gender, "0", race);
            if (makeCharInfoValues.Count > 0)
            {
                return makeCharInfoValues;
            }

            int startId = gender == CharacterGender.Male ? DefaultMaleFaceId : DefaultFemaleFaceId;
            return ResolveStarterPartCandidates(
                startId,
                StarterFaceSearchSpan,
                id => LoadFace(id) != null,
                defaultFaceId);
        }

        private IReadOnlyList<int> ResolveStarterHairCandidates(CharacterGender gender)
        {
            return ResolveStarterHairCandidates(gender, LoginCreateCharacterRaceKind.Explorer);
        }

        private IReadOnlyList<int> ResolveStarterHairCandidates(CharacterGender gender, LoginCreateCharacterRaceKind race)
        {
            int defaultHairId = gender == CharacterGender.Male ? DefaultMaleHairId : DefaultFemaleHairId;
            List<int> makeCharInfoValues = ResolveMakeCharInfoStarterValues(gender, "1", race);
            if (makeCharInfoValues.Count > 0)
            {
                return makeCharInfoValues;
            }

            int startId = gender == CharacterGender.Male ? DefaultMaleHairId : DefaultFemaleHairId;
            return ResolveStarterPartCandidates(
                startId,
                StarterHairSearchSpan,
                id => LoadHair(id) != null,
                defaultHairId);
        }

        private IReadOnlyList<int> ResolveStarterHairStyleCandidates(CharacterGender gender, LoginCreateCharacterRaceKind race)
        {
            IReadOnlyList<int> hairIds = ResolveStarterHairCandidates(gender, race);
            List<int> styleIds = new();
            foreach (int hairId in hairIds)
            {
                int styleId = NormalizeHairStyleId(hairId);
                if (!styleIds.Contains(styleId))
                {
                    styleIds.Add(styleId);
                }
            }

            if (styleIds.Count == 0)
            {
                styleIds.Add(gender == CharacterGender.Male ? DefaultMaleHairId : DefaultFemaleHairId);
            }

            return styleIds;
        }

        private IReadOnlyList<int> ResolveStarterHairColorCandidates(CharacterGender gender, LoginCreateCharacterRaceKind race)
        {
            List<int> makeCharInfoValues = ResolveMakeCharInfoStarterValues(gender, "3", race);
            List<int> colorIndices = new();
            foreach (int value in makeCharInfoValues)
            {
                if (value >= 0 && value <= 9 && !colorIndices.Contains(value))
                {
                    colorIndices.Add(value);
                }
            }

            if (colorIndices.Count == 0)
            {
                colorIndices.AddRange(new[] { 0, 1, 2, 3 });
            }

            return colorIndices;
        }

        private IReadOnlyList<int> ResolveStarterEquipmentCandidates(CharacterGender gender, string categoryName, int fallbackItemId)
        {
            return ResolveStarterEquipmentCandidates(gender, categoryName, fallbackItemId, LoginCreateCharacterRaceKind.Explorer);
        }

        private IReadOnlyList<int> ResolveStarterEquipmentCandidates(
            CharacterGender gender,
            string categoryName,
            int fallbackItemId,
            LoginCreateCharacterRaceKind race)
        {
            List<int> candidates = ResolveMakeCharInfoStarterValues(gender, categoryName, race);
            if (candidates.Count == 0)
            {
                candidates.Add(fallbackItemId);
            }

            return candidates;
        }

        private IReadOnlyList<int> ResolveStarterEquipmentCandidates(CharacterGender gender, int categoryIndex)
        {
            return ResolveStarterEquipmentCandidates(gender, categoryIndex, LoginCreateCharacterRaceKind.Explorer);
        }

        private IReadOnlyList<int> ResolveStarterEquipmentCandidates(
            CharacterGender gender,
            int categoryIndex,
            LoginCreateCharacterRaceKind race)
        {
            return categoryIndex switch
            {
                4 => ResolveStarterEquipmentCandidates(gender, "4", DefaultWizetSuitId, race),
                5 => ResolveStarterEquipmentCandidates(gender, "5", DefaultWizetPantsId, race),
                6 => ResolveStarterEquipmentCandidates(gender, "6", DefaultLeatherSandalsId, race),
                7 => ResolveStarterEquipmentCandidates(gender, "7", DefaultWizetSuitcaseId, race),
                _ => Array.Empty<int>()
            };
        }

        private void EquipRandomStarterGear(CharacterBuild build, StarterAvatarRandomizationCatalog starterCatalog)
        {
            if (build == null || starterCatalog == null)
            {
                return;
            }

            int coatId = PickRandomCandidate(starterCatalog.CoatIds, DefaultWizetSuitId);
            int pantsId = PickRandomCandidate(starterCatalog.PantsIds, DefaultWizetPantsId);
            int shoesId = PickRandomCandidate(starterCatalog.ShoesIds, DefaultLeatherSandalsId);
            int weaponId = PickRandomCandidate(starterCatalog.WeaponIds, DefaultWizetSuitcaseId);

            EquipDefaultItem(build, coatId, nameof(EquipSlot.Coat), DefaultWizetSuitId);
            EquipDefaultItem(build, pantsId, nameof(EquipSlot.Pants), DefaultWizetPantsId);
            EquipDefaultItem(build, shoesId, nameof(EquipSlot.Shoes), DefaultLeatherSandalsId);
            EquipDefaultItem(build, weaponId, nameof(EquipSlot.Weapon), DefaultWizetSuitcaseId);
        }

        private List<int> ResolveMakeCharInfoStarterValues(CharacterGender gender, string categoryName)
        {
            return ResolveMakeCharInfoStarterValues(gender, categoryName, LoginCreateCharacterRaceKind.Explorer);
        }

        private List<int> ResolveMakeCharInfoStarterValues(
            CharacterGender gender,
            string categoryName,
            LoginCreateCharacterRaceKind race)
        {
            var candidates = new List<int>();
            WzImage makeCharInfoImage = Program.FindImage("Etc", "MakeCharInfo.img");
            if (makeCharInfoImage == null)
            {
                return candidates;
            }

            WzImageProperty raceNode = makeCharInfoImage[ResolveMakeCharInfoRaceNodeName(race)] as WzImageProperty;
            WzImageProperty genderNode = raceNode?[gender == CharacterGender.Female ? "female" : "male"] as WzImageProperty;
            WzImageProperty categoryNode = genderNode?[categoryName] as WzImageProperty;
            if (categoryNode == null)
            {
                return candidates;
            }

            foreach (WzImageProperty child in categoryNode.WzProperties)
            {
                if (!int.TryParse(child.Name, out _))
                {
                    continue;
                }

                int value = InfoTool.GetInt(child, 0);
                if (categoryName is "2" && Enum.IsDefined(typeof(SkinColor), value))
                {
                    SkinColor skin = (SkinColor)value;
                    if (LoadBody(skin) != null && LoadHead(skin) != null && !candidates.Contains(value))
                    {
                        candidates.Add(value);
                    }
                    continue;
                }

                if (value <= 0)
                {
                    continue;
                }

                bool isValid = categoryName switch
                {
                    "0" => LoadFace(value) != null,
                    "1" => LoadHair(value) != null,
                    _ => LoadEquipment(value) != null
                };

                if (isValid && !candidates.Contains(value))
                {
                    candidates.Add(value);
                }
            }

            return candidates;
        }

        private static string ResolveMakeCharInfoRaceNodeName(LoginCreateCharacterRaceKind race)
        {
            return race switch
            {
                LoginCreateCharacterRaceKind.Cygnus => "1000",
                LoginCreateCharacterRaceKind.Aran => "2000",
                LoginCreateCharacterRaceKind.Evan => "2001",
                LoginCreateCharacterRaceKind.Resistance => "3000",
                _ => "000"
            };
        }

        private static IReadOnlyList<int> ResolveStarterPartCandidates(
            int startId,
            int searchSpan,
            Func<int, bool> isValidCandidate,
            int fallbackId)
        {
            List<int> candidates = new(LoginStarterAvatarSelectionCount);
            for (int id = startId; id < startId + searchSpan && candidates.Count < LoginStarterAvatarSelectionCount; id++)
            {
                if (isValidCandidate(id))
                {
                    candidates.Add(id);
                }
            }

            if (candidates.Count == 0)
            {
                candidates.Add(fallbackId);
            }

            return candidates;
        }

        private static int NormalizeHairStyleId(int hairId)
        {
            return hairId < 0 ? 0 : (hairId / 10) * 10;
        }

        private static int PickCatalogValue(IReadOnlyList<int> values, int index, int fallback)
        {
            return values != null && index >= 0 && index < values.Count
                ? values[index]
                : fallback;
        }

        private static T PickRandomCandidate<T>(IReadOnlyList<T> candidates, T fallback)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return fallback;
            }

            return candidates[Random.Shared.Next(candidates.Count)];
        }

        private CharacterBuild LoadDefaultAvatar(CharacterGender gender)
        {
            SimulatorDefaultAvatarSelection preset = GetDefaultAvatarSelection(gender);
            LoginAvatarLook avatarLook = LoginAvatarLookCodec.CreateLook(
                preset.Gender,
                preset.Skin,
                preset.FaceId,
                preset.HairId,
                preset.EquipmentItemIdsBySlot);

            return LoadFromAvatarLook(avatarLook, CreateDefaultAvatarMetadataTemplate(preset));
        }

        public static SimulatorDefaultAvatarSelection GetDefaultAvatarSelection(CharacterGender gender)
        {
            return gender switch
            {
                CharacterGender.Female => new SimulatorDefaultAvatarSelection
                {
                    Gender = CharacterGender.Female,
                    Name = "Default Female",
                    Skin = SkinColor.Light,
                    FaceId = DefaultFemaleFaceId,
                    HairId = DefaultFemaleHairId,
                    Level = 200,
                    JobId = 910,
                    JobName = "SuperGM",
                    EquipmentItemIdsBySlot = CreateDefaultSimulatorEquipmentBySlot()
                },
                _ => new SimulatorDefaultAvatarSelection
                {
                    Gender = CharacterGender.Male,
                    Name = "Default Male",
                    Skin = SkinColor.Light,
                    FaceId = DefaultMaleFaceId,
                    HairId = DefaultMaleHairId,
                    Level = 200,
                    JobId = 910,
                    JobName = "SuperGM",
                    EquipmentItemIdsBySlot = CreateDefaultSimulatorEquipmentBySlot()
                }
            };
        }

        private static IReadOnlyDictionary<EquipSlot, int> CreateDefaultSimulatorEquipmentBySlot()
        {
            return new Dictionary<EquipSlot, int>
            {
                [EquipSlot.Cap] = DefaultWizetHatId,
                [EquipSlot.Coat] = DefaultWizetSuitId,
                [EquipSlot.Pants] = DefaultWizetPantsId,
                [EquipSlot.Shoes] = DefaultLeatherSandalsId,
                [EquipSlot.Shield] = DefaultPanLidShieldId,
                [EquipSlot.Weapon] = DefaultWizetSuitcaseId
            };
        }

        public CharacterBuild LoadFromAvatarLook(LoginAvatarLook avatarLook, CharacterBuild template = null)
        {
            if (avatarLook == null)
            {
                throw new ArgumentNullException(nameof(avatarLook));
            }

            SimulatorDefaultAvatarSelection fallbackSelection = GetDefaultAvatarSelection(avatarLook.Gender);
            CharacterBuild build = template?.Clone() ?? CreateDefaultAvatarMetadataTemplate(fallbackSelection);
            AttachEquipmentResolver(build);

            build.Gender = avatarLook.Gender;
            build.Skin = avatarLook.Skin;
            build.Body = LoadBody(avatarLook.Skin) ?? LoadBody(fallbackSelection.Skin);
            build.Head = LoadHead(avatarLook.Skin) ?? LoadHead(fallbackSelection.Skin);
            build.Face = LoadFace(avatarLook.FaceId) ?? LoadFace(fallbackSelection.FaceId);
            build.Hair = LoadHair(avatarLook.HairId) ?? LoadHair(fallbackSelection.HairId);
            build.RemotePetItemIds = avatarLook.PetIds?
                .Where(petId => petId > 0)
                .Distinct()
                .ToArray()
                ?? Array.Empty<int>();
            build.Equipment = new Dictionary<EquipSlot, CharacterPart>();
            build.HiddenEquipment = new Dictionary<EquipSlot, CharacterPart>();

            EquipAvatarLookGear(build, avatarLook);
            build.WeaponSticker = LoadAvatarLookWeaponSticker(avatarLook.WeaponStickerItemId);
            if (build.Equipment.Count == 0)
            {
                EquipDefaultSimulatorGear(build, fallbackSelection.EquipmentItemIdsBySlot);
            }

            return build;
        }

        private void AttachEquipmentResolver(CharacterBuild build)
        {
            if (build != null)
            {
                build.EquipmentPartLoader = LoadEquipment;
            }
        }

        internal static CharacterActionMergeInput PrepareActionMergeInput(
            CharacterBuild build,
            string actionName,
            CharacterPart activeTamingMobPart)
        {
            if (build == null)
            {
                return new CharacterActionMergeInput();
            }

            string resolvedActionName = string.IsNullOrWhiteSpace(actionName)
                ? CharacterPart.GetActionString(CharacterAction.Stand1)
                : actionName;
            string requestedActionName = resolvedActionName;

            Dictionary<EquipSlot, CharacterPart> equipment = new();
            if (build.Equipment != null)
            {
                foreach (KeyValuePair<EquipSlot, CharacterPart> entry in build.Equipment)
                {
                    if (entry.Value != null)
                    {
                        equipment[entry.Key] = entry.Value;
                    }
                }
            }

            StripAlwaysSuppressedActionLanes(equipment);

            bool keepMinimalArrowEruptionSet = string.Equals(
                requestedActionName,
                "arrowEruption",
                StringComparison.OrdinalIgnoreCase);

            if (keepMinimalArrowEruptionSet)
            {
                RetainOnlyArrowEruptionLanes(equipment);
            }
            else
            {
                ApplyBodyValidityFallbacks(build, equipment);
            }

            StripBlockedEquipmentIds(equipment);

            CharacterPart weaponSticker = build.WeaponSticker;
            if (IsBlockedWeaponStickerId(weaponSticker?.ItemId ?? 0))
            {
                equipment.Remove(EquipSlot.Weapon);
                weaponSticker = null;
            }

            if (string.Equals(requestedActionName, "coolingeffect", StringComparison.OrdinalIgnoreCase))
            {
                equipment.Remove(EquipSlot.Shield);
                equipment.Remove(EquipSlot.Weapon);
            }

            if (string.Equals(requestedActionName, "somersault", StringComparison.OrdinalIgnoreCase)
                || string.Equals(requestedActionName, "doublefire", StringComparison.OrdinalIgnoreCase))
            {
                weaponSticker = null;
            }

            if (IsGhostAction(requestedActionName))
            {
                RetainOnlyGhostCoreLanes(equipment);
                weaponSticker = null;
            }

            CharacterPart filteredTamingMobPart = activeTamingMobPart;
            if (filteredTamingMobPart?.Slot != EquipSlot.TamingMob)
            {
                equipment.TryGetValue(EquipSlot.TamingMob, out filteredTamingMobPart);
            }

            // Mechanic's shared 1932016 mount asset publishes ordinary locomotion roots
            // such as walk1/stand1, but those should not be rendered as a separate taming-mob
            // under the avatar unless the current action is one of the client-owned vehicle
            // ownership signals that actually puts the character into the mounted presentation.
            if (filteredTamingMobPart?.ItemId == MechanicTamingMobItemId
                && !CharacterAssembler.IsTamingMobRenderOwnershipAction(filteredTamingMobPart, resolvedActionName))
            {
                filteredTamingMobPart = null;
            }

            if (ShouldSuppressShieldAndWeaponForMountedFamily(filteredTamingMobPart))
            {
                equipment.Remove(EquipSlot.Shield);
                equipment.Remove(EquipSlot.Weapon);
                resolvedActionName = RemapMountedFamilyAction(resolvedActionName);
            }

            filteredTamingMobPart = PrepareTamingMobActionMergePart(filteredTamingMobPart, equipment);
            if (filteredTamingMobPart != null)
            {
                equipment[EquipSlot.TamingMob] = filteredTamingMobPart;
            }
            else
            {
                equipment.Remove(EquipSlot.TamingMob);
            }

            return new CharacterActionMergeInput
            {
                ActionName = resolvedActionName,
                Equipment = equipment,
                WeaponSticker = weaponSticker,
                ActiveTamingMobPart = filteredTamingMobPart
            };
        }

        private static void StripAlwaysSuppressedActionLanes(IDictionary<EquipSlot, CharacterPart> equipment)
        {
            if (equipment == null)
            {
                return;
            }

            equipment.Remove(EquipSlot.FaceAccessory);
            equipment.Remove(EquipSlot.Ring1);
            equipment.Remove(EquipSlot.Ring2);
            equipment.Remove(EquipSlot.Ring3);
            equipment.Remove(EquipSlot.Ring4);
        }

        internal static CharacterPart PrepareTamingMobActionMergePart(
            CharacterPart activeTamingMobPart,
            IDictionary<EquipSlot, CharacterPart> equipment)
        {
            if (activeTamingMobPart?.Slot != EquipSlot.TamingMob
                || activeTamingMobPart.ItemId / 10000 != 190
                || equipment == null)
            {
                return activeTamingMobPart;
            }

            CharacterPart[] overlayParts = EnumerateTamingMobActionOverlayParts(activeTamingMobPart, equipment);
            if (overlayParts.Length == 0)
            {
                return activeTamingMobPart;
            }

            CharacterPart mergedMountPart = MergeTamingMobActionOverlayParts(
                activeTamingMobPart,
                overlayParts);

            foreach (CharacterPart overlayPart in overlayParts)
            {
                equipment.Remove(overlayPart.Slot);
            }

            return mergedMountPart;
        }

        internal static CharacterPart[] EnumerateTamingMobActionOverlayParts(
            CharacterPart activeTamingMobPart,
            IDictionary<EquipSlot, CharacterPart> equipment)
        {
            if (activeTamingMobPart?.Slot != EquipSlot.TamingMob || equipment == null)
            {
                return Array.Empty<CharacterPart>();
            }

            return equipment.Values
                .Where(static part => part?.TamingMobActionOverlayResolver != null)
                .Where(part => !ReferenceEquals(part, activeTamingMobPart))
                .Where(part => part.Slot != EquipSlot.TamingMob)
                .OrderBy(GetTamingMobActionOverlayOrder)
                .ThenBy(part => (int)part.Slot)
                .ToArray();
        }

        private static int GetTamingMobActionOverlayOrder(CharacterPart part)
        {
            return part?.Slot switch
            {
                EquipSlot.Saddle => 0,
                EquipSlot.TamingMobAccessory => 1,
                _ => 2
            };
        }

        internal static CharacterPart MergeTamingMobActionOverlayParts(
            CharacterPart activeTamingMobPart,
            IEnumerable<CharacterPart> overlayParts)
        {
            if (activeTamingMobPart?.Slot != EquipSlot.TamingMob || overlayParts == null)
            {
                return activeTamingMobPart;
            }

            Func<int, string, CharacterAnimation>[] overlayResolvers = overlayParts
                .Select(static part => part?.TamingMobActionOverlayResolver)
                .Where(static resolver => resolver != null)
                .ToArray();
            if (overlayResolvers.Length == 0)
            {
                return activeTamingMobPart;
            }

            CharacterPart mergedMountPart = activeTamingMobPart.Clone();
            int baseVehicleId = activeTamingMobPart.ItemId;

            foreach (KeyValuePair<string, CharacterAnimation> entry in activeTamingMobPart.Animations)
            {
                CharacterAnimation mergedAnimation = MergeTamingMobActionAnimations(
                    entry.Value,
                    overlayResolvers.Select(resolver => resolver(baseVehicleId, entry.Key)));
                if (mergedAnimation?.Frames?.Count > 0)
                {
                    mergedMountPart.Animations[entry.Key] = mergedAnimation;
                }
            }

            mergedMountPart.AnimationResolver = actionName =>
            {
                CharacterAnimation baseAnimation = ResolveExactAnimation(activeTamingMobPart, actionName);
                if (baseAnimation?.Frames?.Count <= 0)
                {
                    return null;
                }

                return MergeTamingMobActionAnimations(
                    baseAnimation,
                    overlayResolvers.Select(resolver => resolver(baseVehicleId, actionName)));
            };

            return mergedMountPart;
        }

        internal static CharacterAnimation MergeTamingMobActionAnimations(
            CharacterAnimation baseAnimation,
            IEnumerable<CharacterAnimation> overlayAnimations)
        {
            if (baseAnimation?.Frames?.Count <= 0)
            {
                return null;
            }

            CharacterAnimation mergedAnimation = CloneAnimation(baseAnimation);
            if (overlayAnimations == null)
            {
                return mergedAnimation;
            }

            foreach (CharacterAnimation overlayAnimation in overlayAnimations)
            {
                if (overlayAnimation?.Frames?.Count <= 0)
                {
                    continue;
                }

                int frameCount = Math.Min(mergedAnimation.Frames.Count, overlayAnimation.Frames.Count);
                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    mergedAnimation.Frames[frameIndex] = MergeTamingMobFrames(
                        mergedAnimation.Frames[frameIndex],
                        overlayAnimation.Frames[frameIndex]);
                }
            }

            mergedAnimation.CalculateTotalDuration();
            return mergedAnimation;
        }

        private static CharacterAnimation ResolveExactAnimation(CharacterPart part, string actionName)
        {
            if (part == null || string.IsNullOrWhiteSpace(actionName))
            {
                return null;
            }

            if (part.Animations.TryGetValue(actionName, out CharacterAnimation animation)
                && animation?.Frames?.Count > 0)
            {
                return animation;
            }

            animation = part.AnimationResolver?.Invoke(actionName);
            if (animation?.Frames?.Count > 0)
            {
                part.Animations[actionName] = animation;
                return animation;
            }

            return null;
        }

        private static CharacterAnimation CloneAnimation(CharacterAnimation source)
        {
            if (source == null)
            {
                return null;
            }

            CharacterAnimation clone = new()
            {
                Action = source.Action,
                ActionName = source.ActionName,
                Loop = source.Loop,
                Frames = source.Frames?.Select(static frame => frame?.Clone()).ToList() ?? new List<CharacterFrame>()
            };
            clone.CalculateTotalDuration();
            return clone;
        }

        private static CharacterFrame MergeTamingMobFrames(CharacterFrame baseFrame, CharacterFrame overlayFrame)
        {
            if (baseFrame == null)
            {
                return overlayFrame?.Clone();
            }

            if (overlayFrame == null)
            {
                return baseFrame.Clone();
            }

            CharacterFrame mergedFrame = baseFrame.Clone();
            List<CharacterSubPart> mergedSubParts = ExtractTamingMobFrameSubParts(baseFrame);
            mergedSubParts.AddRange(ExtractTamingMobFrameSubParts(overlayFrame));
            if (mergedSubParts.Count > 0)
            {
                mergedFrame.Texture = null;
                mergedFrame.Z = null;
                mergedFrame.SubParts = mergedSubParts;
            }

            return mergedFrame;
        }

        private static List<CharacterSubPart> ExtractTamingMobFrameSubParts(CharacterFrame frame)
        {
            if (frame == null)
            {
                return new List<CharacterSubPart>();
            }

            if (frame.HasSubParts)
            {
                return frame.SubParts?
                    .Select(CloneCharacterSubPart)
                    .Where(static subPart => subPart != null)
                    .ToList()
                    ?? new List<CharacterSubPart>();
            }

            if (frame.Texture == null)
            {
                return new List<CharacterSubPart>();
            }

            return new List<CharacterSubPart>
            {
                new()
                {
                    Name = string.Empty,
                    Texture = frame.Texture,
                    Origin = frame.Origin,
                    Z = frame.Z,
                    Map = new Dictionary<string, Point>(frame.Map ?? new Dictionary<string, Point>(), StringComparer.OrdinalIgnoreCase),
                    NavelOffset = Point.Zero
                }
            };
        }

        private static CharacterSubPart CloneCharacterSubPart(CharacterSubPart source)
        {
            if (source == null)
            {
                return null;
            }

            return new CharacterSubPart
            {
                Name = source.Name,
                Texture = source.Texture,
                Origin = source.Origin,
                Z = source.Z,
                Map = new Dictionary<string, Point>(source.Map ?? new Dictionary<string, Point>(), StringComparer.OrdinalIgnoreCase),
                NavelOffset = source.NavelOffset
            };
        }

        private static void RetainOnlyArrowEruptionLanes(IDictionary<EquipSlot, CharacterPart> equipment)
        {
            if (equipment == null)
            {
                return;
            }

            // CActionMan::LoadCharacterAction(action 47 / arrowEruption) preserves only
            // avatar array indices 0, 1, 3, and 4. The simulator's equipment dictionary
            // owns only the equip-backed subset of that array, so hair/index 0 is already
            // handled outside this table and the retained lanes are cap, eye accessory,
            // and earrings only.
            HashSet<EquipSlot> allowedSlots = new()
            {
                EquipSlot.Cap,
                EquipSlot.EyeAccessory,
                EquipSlot.Earrings
            };

            foreach (EquipSlot slot in equipment.Keys.ToArray())
            {
                if (!allowedSlots.Contains(slot))
                {
                    equipment.Remove(slot);
                }
            }
        }

        private static void ApplyBodyValidityFallbacks(CharacterBuild build, IDictionary<EquipSlot, CharacterPart> equipment)
        {
            if (build == null || equipment == null)
            {
                return;
            }

            equipment.TryGetValue(EquipSlot.Coat, out CharacterPart coatPart);
            equipment.TryGetValue(EquipSlot.Pants, out CharacterPart pantsPart);

            if (coatPart == null)
            {
                int fallbackTopId = build.Gender == CharacterGender.Female ? ClientFallbackFemaleTopId : ClientFallbackMaleTopId;
                CharacterPart fallbackTop = build.EquipmentPartLoader?.Invoke(fallbackTopId);
                if (fallbackTop?.Slot == EquipSlot.Coat)
                {
                    equipment[EquipSlot.Coat] = fallbackTop;
                    coatPart = fallbackTop;
                }
            }

            if (pantsPart == null && !IsLongcoat(coatPart))
            {
                int fallbackBottomId = build.Gender == CharacterGender.Female ? ClientFallbackFemaleBottomId : ClientFallbackMaleBottomId;
                CharacterPart fallbackBottom = build.EquipmentPartLoader?.Invoke(fallbackBottomId);
                if (fallbackBottom?.Slot == EquipSlot.Pants)
                {
                    equipment[EquipSlot.Pants] = fallbackBottom;
                }
            }
        }

        private static bool IsLongcoat(CharacterPart part)
        {
            return part?.Type == CharacterPartType.Longcoat || (part?.ItemId ?? 0) / 10000 == 105;
        }

        private static void StripBlockedEquipmentIds(IDictionary<EquipSlot, CharacterPart> equipment)
        {
            if (equipment == null)
            {
                return;
            }

            RemoveSlotIfItemMatches(equipment, EquipSlot.Cap, BlockedCapItemId);
            RemoveSlotIfItemMatches(equipment, EquipSlot.Earrings, BlockedEarringsItemId);
            RemoveSlotIfItemMatches(equipment, EquipSlot.EyeAccessory, BlockedEyeAccessoryItemId);
            RemoveSlotIfItemMatches(equipment, EquipSlot.Shoes, BlockedShoesItemId);
            RemoveSlotIfItemMatches(equipment, EquipSlot.Glove, BlockedGloveItemId);
            RemoveSlotIfItemMatches(equipment, EquipSlot.Cape, BlockedCapeItemId);
            RemoveSlotIfItemMatches(equipment, EquipSlot.Shield, BlockedShieldItemId);
        }

        private static void RemoveSlotIfItemMatches(IDictionary<EquipSlot, CharacterPart> equipment, EquipSlot slot, int itemId)
        {
            if (equipment.TryGetValue(slot, out CharacterPart part) && part?.ItemId == itemId)
            {
                equipment.Remove(slot);
            }
        }

        private static bool IsBlockedWeaponStickerId(int itemId)
        {
            return itemId == BlockedWeaponStickerItemIdA || itemId == BlockedWeaponStickerItemIdB;
        }

        private static bool ShouldSuppressShieldAndWeaponForMountedFamily(CharacterPart activeTamingMobPart)
        {
            int vehicleId = activeTamingMobPart?.ItemId ?? 0;
            if (vehicleId <= 0)
            {
                return false;
            }

            return vehicleId / 10000 == 190
                   || vehicleId / 10000 == 193
                   || vehicleId == 1902040
                   || vehicleId == 1902041
                   || vehicleId == 1902042
                   || vehicleId / 1000 == 1983
                   || vehicleId == MechanicTamingMobItemId;
        }

        private static string RemapMountedFamilyAction(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return CharacterPart.GetActionString(CharacterAction.Stand1);
            }

            if (string.Equals(actionName, "msummon", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "msummon2", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "rush2", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "sanctuary", StringComparison.OrdinalIgnoreCase))
            {
                return actionName;
            }

            if (string.Equals(actionName, "shoot6", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "arrowRain", StringComparison.OrdinalIgnoreCase))
            {
                return "arrowRain";
            }

            return "magic1";
        }

        private static bool IsGhostAction(string actionName)
        {
            return !string.IsNullOrWhiteSpace(actionName)
                   && (string.Equals(actionName, "ghost", StringComparison.OrdinalIgnoreCase)
                       || actionName.StartsWith("ghost", StringComparison.OrdinalIgnoreCase));
        }

        private static void RetainOnlyGhostCoreLanes(IDictionary<EquipSlot, CharacterPart> equipment)
        {
            if (equipment == null)
            {
                return;
            }

            HashSet<EquipSlot> allowedSlots = new()
            {
                EquipSlot.Cap,
                EquipSlot.EyeAccessory,
                EquipSlot.Earrings
            };

            foreach (EquipSlot slot in equipment.Keys.ToArray())
            {
                if (!allowedSlots.Contains(slot))
                {
                    equipment.Remove(slot);
                }
            }
        }

        private void EquipDefaultSimulatorGear(CharacterBuild build, IReadOnlyDictionary<EquipSlot, int> equipmentItemIdsBySlot)
        {
            if (build == null || equipmentItemIdsBySlot == null)
                return;

            foreach (KeyValuePair<EquipSlot, int> entry in equipmentItemIdsBySlot)
            {
                EquipDefaultItem(build, entry.Value, entry.Key.ToString());
            }
        }

        private void EquipDefaultItem(CharacterBuild build, int itemId, string label, int? fallbackItemId = null)
        {
            var equipment = LoadEquipment(itemId);
            if (equipment != null)
            {
                build.Equip(equipment);
                System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Equipped default {label}: {equipment.Name} ({itemId})");
                return;
            }

            if (fallbackItemId.HasValue)
            {
                var fallback = LoadEquipment(fallbackItemId.Value);
                if (fallback != null)
                {
                    build.Equip(fallback);
                    System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Falling back to default {label}: {fallback.Name} ({fallbackItemId.Value})");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Failed to load default {label} {itemId} and fallback {fallbackItemId.Value}");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Failed to load default {label} {itemId}");
        }

        private static CharacterBuild CreateDefaultAvatarMetadataTemplate(SimulatorDefaultAvatarSelection selection)
        {
            return new CharacterBuild
            {
                Gender = selection.Gender,
                Skin = selection.Skin,
                Name = selection.Name,
                Level = selection.Level,
                Job = selection.JobId,
                JobName = selection.JobName
            };
        }

        private void EquipAvatarLookGear(CharacterBuild build, LoginAvatarLook avatarLook)
        {
            if (build == null || avatarLook == null)
            {
                return;
            }

            foreach (KeyValuePair<byte, int> entry in avatarLook.VisibleEquipmentByBodyPart.OrderBy(entry => entry.Key))
            {
                TryEquipAvatarLookItem(build, entry.Key, entry.Value, false);
            }

            foreach (KeyValuePair<byte, int> entry in avatarLook.HiddenEquipmentByBodyPart.OrderBy(entry => entry.Key))
            {
                TryEquipAvatarLookItem(build, entry.Key, entry.Value, true);
            }
        }

        private bool TryEquipAvatarLookItem(CharacterBuild build, byte bodyPart, int itemId, bool concealWhenOccupied)
        {
            if (!LoginAvatarLookCodec.TryGetEquipSlot(bodyPart, out EquipSlot slot))
            {
                return false;
            }

            CharacterPart equipment = LoadEquipment(itemId);
            if (equipment == null)
            {
                return false;
            }

            if (concealWhenOccupied && build.Equipment.ContainsKey(slot))
            {
                build.EquipHidden(equipment);
                return build.HiddenEquipment.ContainsKey(slot);
            }

            build.Equip(equipment);
            return build.Equipment.ContainsKey(slot);
        }

        private CharacterPart LoadAvatarLookWeaponSticker(int itemId)
        {
            if (itemId <= 0)
            {
                return null;
            }

            CharacterPart sticker = LoadEquipment(itemId);
            if (sticker == null)
            {
                System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Failed to load AvatarLook weapon sticker {itemId}");
            }

            return sticker;
        }

        #endregion

        #region Cache Management

        public void ClearCache()
        {
            _bodyCache.Clear();
            _faceCache.Clear();
            _hairCache.Clear();
            _equipCache.Clear();
            _starterAvatarCatalogCache.Clear();
        }

        public int GetCacheCount()
        {
            return _bodyCache.Count + _faceCache.Count + _hairCache.Count + _equipCache.Count;
        }

        #endregion
    }
}
