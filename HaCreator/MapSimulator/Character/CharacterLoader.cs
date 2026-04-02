using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

        // Cache for loaded parts
        private readonly Dictionary<int, BodyPart> _bodyCache = new();
        private readonly Dictionary<int, FacePart> _faceCache = new();
        private readonly Dictionary<int, HairPart> _hairCache = new();
        private readonly Dictionary<int, CharacterPart> _equipCache = new();
        private readonly Dictionary<int, CharacterPart> _morphCache = new();
        private readonly Dictionary<int, PortableChair> _portableChairCache = new();
        private readonly Dictionary<int, ItemEffectAnimationSet> _itemEffectCache = new();
        private readonly Dictionary<int, ItemEffectAnimationSet> _completedSetEffectCache = new();
        private readonly Dictionary<RemoteRelationshipOverlayType, RelationshipTextTagStyle> _relationshipTextTagCache = new();
        private readonly Dictionary<CharacterGender, StarterAvatarRandomizationCatalog> _starterAvatarCatalogCache = new();
        private CarryItemEffectDefinition _carryItemEffectCache;

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
            // Try WzFile first if available
            if (_characterWz?.WzDirectory != null)
            {
                var img = _characterWz.WzDirectory[imgName] as WzImage;
                if (img != null)
                    return img;
            }

            // Fall back to Program.FindImage (works with .img file loading)
            return Program.FindImage("Character", imgName);
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

            LoadPartAnimations(body, imgNode as WzImage);
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

            LoadPartAnimations(head, imgNode as WzImage);
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

            var morphPart = new CharacterPart
            {
                ItemId = morphTemplateId,
                Name = $"Morph_{morphTemplateId:D4}",
                Type = CharacterPartType.Morph,
                Slot = EquipSlot.None,
                IsSuperManMorph = GetMorphSuperManFlag(morphImage)
            };

            PopulateMorphAnimations(morphPart, morphTemplateId, morphImage);
            if (morphPart.Animations.Count == 0)
            {
                return null;
            }

            _morphCache[morphTemplateId] = morphPart;
            return morphPart;
        }

        internal static bool CanResolveMorphTemplate(int morphTemplateId)
        {
            if (morphTemplateId <= 0)
            {
                return false;
            }

            var checkedTemplateIds = new HashSet<int>();
            foreach (int candidateTemplateId in EnumerateMorphTemplateCandidates(morphTemplateId, exactMorphImage: null))
            {
                if (!checkedTemplateIds.Add(candidateTemplateId))
                {
                    continue;
                }

                if (Program.FindImage("Morph", candidateTemplateId.ToString("D4") + ".img") != null)
                {
                    return true;
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
                    IsSuperManMorph = GetMorphSuperManFlag(candidateImage)
                };

                LoadPartAnimations(candidatePart, candidateImage, includeAttackActions: false);
                MergeMissingAnimations(morphPart, candidatePart);
            }
        }

        private static IEnumerable<int> EnumerateMorphTemplateCandidates(int morphTemplateId, WzImage exactMorphImage)
        {
            var seen = new HashSet<int>();

            foreach (int candidate in EnumerateMorphLinkChain(morphTemplateId, exactMorphImage, seen))
            {
                yield return candidate;
            }

            foreach (int candidate in EnumeratePairedMorphTemplateCandidates(morphTemplateId))
            {
                if (seen.Add(candidate))
                {
                    yield return candidate;
                }
            }

            yield return morphTemplateId;

            int[] familyBases =
            {
                (morphTemplateId / 10) * 10,
                (morphTemplateId / 100) * 100,
                (morphTemplateId / 1000) * 1000
            };

            foreach (int candidate in familyBases)
            {
                if (candidate > 0 && seen.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }

        private static IEnumerable<int> EnumerateMorphLinkChain(int morphTemplateId, WzImage exactMorphImage, HashSet<int> seen)
        {
            if (morphTemplateId <= 0 || seen == null || !seen.Add(morphTemplateId))
            {
                yield break;
            }

            yield return morphTemplateId;

            WzImage morphImage = exactMorphImage ?? Program.FindImage("Morph", morphTemplateId.ToString("D4") + ".img");
            int linkedTemplateId = GetMorphLinkTemplateId(morphImage);
            if (linkedTemplateId <= 0 || linkedTemplateId == morphTemplateId)
            {
                yield break;
            }

            foreach (int linkedCandidate in EnumerateMorphLinkChain(linkedTemplateId, exactMorphImage: null, seen))
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
            if (targetPart?.Animations == null || sourcePart?.Animations == null)
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

        public PortableChair LoadPortableChair(int itemId)
        {
            if (_portableChairCache.TryGetValue(itemId, out PortableChair cached))
            {
                return cached;
            }

            if (InventoryItemMetadataResolver.ResolveInventoryType(itemId) != MapleLib.WzLib.WzStructure.Data.ItemStructure.InventoryType.SETUP
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
                SitActionId = GetIntValue(info?["sitAction"]),
                TamingMobItemId = GetIntValue(info?["tamingMob"]),
                IsCoupleChair = itemId / 1000 == 3012,
                CoupleDistanceX = GetIntValue(info?["distanceX"]),
                CoupleDistanceY = GetIntValue(info?["distanceY"]),
                CoupleMaxDiff = GetIntValue(info?["maxDiff"]),
                CoupleDirection = GetIntValue(info?["direction"])
            };

            LoadPortableChairLayers(itemProperty, chair.Layers);
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

            var effectSet = new ItemEffectAnimationSet
            {
                ItemId = itemId
            };

            foreach (ItemEffectLayerSource layerSource in ResolveItemEffectLayerSources(itemEffectProperty))
            {
                PortableChairLayer layer = LoadPortableChairLayer(layerSource.LayerProperty, layerSource.LayerName, loop: false);
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

            _itemEffectCache[itemId] = effectSet;
            return effectSet;
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

                CharacterFrame frame = LoadFrame(canvas, child.Name);
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
                System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Character dir lookup: {charDirObj?.GetType().Name ?? "NULL"}");

                // Then get Face subdirectory from it
                if (charDirObj is MapleLib.Img.VirtualWzDirectory virtualCharDir)
                {
                    // Get Face subdirectory
                    var faceSubDir = virtualCharDir["Face"];
                    System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Face subdir: {faceSubDir?.GetType().Name ?? "NULL"}");

                    if (faceSubDir is MapleLib.Img.VirtualWzDirectory virtualFaceDir)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Face VirtualDir path: {virtualFaceDir.FilesystemPath}");
                        System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Face VirtualDir has {virtualFaceDir.WzImages.Count} images:");
                        foreach (var img in virtualFaceDir.WzImages.Take(5))
                        {
                            System.Diagnostics.Debug.WriteLine($"  - {img.Name}");
                        }

                        imgNode = virtualFaceDir[imgName] as WzImage;
                        System.Diagnostics.Debug.WriteLine($"[CharacterLoader] VirtualFaceDir[{imgName}] = {imgNode?.Name ?? "NULL"}");
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

            // Debug: show top-level nodes in face image
            System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Face image nodes:");
            foreach (var prop in img.WzProperties)
            {
                System.Diagnostics.Debug.WriteLine($"  - {prop.Name} ({prop.GetType().Name})");
                // Show subnodes for first expression
                if (prop is WzSubProperty subProp && prop.Name == "default")
                {
                    foreach (var child in subProp.WzProperties)
                    {
                        System.Diagnostics.Debug.WriteLine($"    - {child.Name} ({child.GetType().Name})");
                        // Show one more level
                        if (child is WzSubProperty childSub)
                        {
                            foreach (var grandchild in childSub.WzProperties)
                            {
                                System.Diagnostics.Debug.WriteLine($"      - {grandchild.Name} ({grandchild.GetType().Name})");
                            }
                        }
                    }
                }
            }

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
                        System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Face expression '{expr}' loaded with {anim.Frames.Count} frames");
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
                        if (frame != null) anim.Frames.Add(frame);
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
                                if (frame != null) anim.Frames.Add(frame);
                            }
                        }
                    }
                    // Also check if face is directly a canvas (expression/face is a single frame)
                    else if (faceNode is WzCanvasProperty faceCanvas)
                    {
                        var frame = LoadFrame(faceCanvas, "0");
                        if (frame != null) anim.Frames.Add(frame);
                    }
                }
            }
            else if (node is WzCanvasProperty canvas)
            {
                var frame = LoadFrame(canvas, "0");
                if (frame != null) anim.Frames.Add(frame);
            }

            anim.CalculateTotalDuration();
            return anim;
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
                // Try direct image lookup via Program.FindImage
                System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Trying direct Hair lookup: Hair/{imgName}");
                imgNode = Program.FindImage("Character", $"Hair/{imgName}");

                if (imgNode == null)
                {
                    // Try ImgFileSystemManager directly if available
                    var dataSource = Program.DataSource;
                    if (dataSource != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Trying DataSource for Hair...");
                        imgNode = dataSource.GetImage("Character", $"Hair/{imgName}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Hair lookup result: {imgNode?.Name ?? "NULL"}");
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

            // Debug: show top-level nodes in hair image
            System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Hair image nodes:");
            foreach (var prop in img.WzProperties)
            {
                System.Diagnostics.Debug.WriteLine($"  - {prop.Name} ({prop.GetType().Name})");
                // Show subnodes for stand1
                if (prop is WzSubProperty subProp && prop.Name == "stand1")
                {
                    foreach (var child in subProp.WzProperties)
                    {
                        System.Diagnostics.Debug.WriteLine($"    - {child.Name} ({child.GetType().Name})");
                        // Show one more level for hair subnode
                        if (child is WzSubProperty childSub && child.Name == "hair")
                        {
                            foreach (var grandchild in childSub.WzProperties)
                            {
                                System.Diagnostics.Debug.WriteLine($"      - {grandchild.Name} ({grandchild.GetType().Name})");
                            }
                        }
                    }
                }
            }

            // Load regular hair animations - include attack animations for proper character posing
            var allActions = BuildActionLoadOrder(img.WzProperties.Select(prop => prop.Name), includeAttackActions: true);
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
                            System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Hair action '{action}' loaded with {anim.Frames.Count} frames");
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
                            System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Hair overHead '{action}' loaded with {anim.Frames.Count} frames");
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

            System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Hair loaded with {hair.Animations.Count} animations");
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
                return cached?.Clone();

            // Determine equipment folder based on ID range
            string folder = GetEquipmentFolder(itemId);
            if (folder == null)
                return null;

            string imgName = itemId.ToString("D8") + ".img";
            WzImage imgNode = null;

            // Try WzFile first
            var equipDir = _characterWz?.WzDirectory?[folder];
            if (equipDir != null)
            {
                imgNode = equipDir[imgName] as WzImage;
            }

            // Fall back to Program.FindImage
            if (imgNode == null)
            {
                imgNode = Program.FindImage("Character", $"{folder}/{imgName}");
            }

            System.Diagnostics.Debug.WriteLine($"[CharacterLoader] LoadEquipment id={itemId}, folder={folder}, found={imgNode != null}");

            if (imgNode == null)
                return null;

            CharacterPart part;
            EquipSlot slot = GetEquipSlot(itemId);

            // Create appropriate part type
            if (folder == "Weapon")
            {
                part = LoadWeapon(imgNode as WzImage, itemId);
            }
            else
            {
                part = new CharacterPart
                {
                    ItemId = itemId,
                    Name = GetItemName(imgNode as WzImage) ?? $"Equip_{itemId}",
                    Type = GetPartType(folder),
                    Slot = slot
                };

                LoadPartAnimations(part, imgNode as WzImage);
            }

            if (part != null)
            {
                // Load info
                LoadEquipInfo(part, imgNode as WzImage);
                _equipCache[itemId] = part;
            }

            return part?.Clone();
        }

        private WeaponPart LoadWeapon(WzImage img, int itemId)
        {
            if (img == null) return null;

            var weapon = new WeaponPart
            {
                ItemId = itemId,
                Name = GetItemName(img) ?? $"Weapon_{itemId}",
                Type = CharacterPartType.Weapon,
                Slot = EquipSlot.Weapon
            };

            img.ParseImage();

            System.Diagnostics.Debug.WriteLine($"[CharacterLoader] LoadWeapon id={itemId}, img nodes:");
            foreach (var prop in img.WzProperties)
            {
                System.Diagnostics.Debug.WriteLine($"  - {prop.Name} ({prop.GetType().Name})");
            }

            // Load weapon info
            var info = img["info"];
            if (info != null)
            {
                LoadEquipInfo(weapon, img);
                weapon.AttackSpeed = GetIntValue(info["attackSpeed"]) ?? 6;
                weapon.Attack = weapon.BonusWeaponAttack;
                weapon.WeaponType = ResolveWeaponType(itemId);
                weapon.AfterImageType = GetStringValue(info["afterImage"]);
                weapon.IsTwoHanded = GetIntValue(info["twoHanded"]) == 1;
                System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Weapon info: attackSpeed={weapon.AttackSpeed}, PAD={weapon.Attack}, twoHanded={weapon.IsTwoHanded}");
            }

            // Load animations - weapon structure is action/frame/weapon (e.g., stand1/0/weapon)
            LoadWeaponAnimations(weapon, img);

            System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Weapon loaded with {weapon.Animations.Count} animations");

            return weapon;
        }

        /// <summary>
        /// Load weapon animations - handles weapon-specific structure
        /// Weapon structure: action/frame/weapon (e.g., stand1/0/weapon)
        /// </summary>
        private void LoadWeaponAnimations(WeaponPart weapon, WzImage img)
        {
            if (img == null) return;

            // Combine standard and attack actions
            var allActions = BuildActionLoadOrder(img.WzProperties.Select(prop => prop.Name), includeAttackActions: true);

            foreach (var action in allActions)
            {
                var actionNode = img[action];
                if (actionNode == null) continue;

                var anim = LoadWeaponAnimation(actionNode, action);
                if (anim != null && anim.Frames.Count > 0)
                {
                    anim.ActionName = action;
                    anim.Action = CharacterPart.ParseActionString(action);
                    weapon.Animations[action] = anim;
                    System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Weapon action '{action}' loaded with {anim.Frames.Count} frames");
                }
            }
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
                // Debug first frame structure
                var frame0 = actionSub["0"];
                if (frame0 is WzSubProperty frame0Sub)
                {
                    System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Weapon {actionName}/0 nodes:");
                    foreach (var child in frame0Sub.WzProperties)
                    {
                        System.Diagnostics.Debug.WriteLine($"    - {child.Name} ({child.GetType().Name})");
                    }
                }

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
            System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Weapon frame {frameName} (action={actionName}) z-layer: {zLayer}");

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

        private void LoadEquipInfo(CharacterPart part, WzImage img)
        {
            if (img == null) return;

            var info = img["info"];
            if (info == null) return;

            part.VSlot = GetStringValue(info["vslot"]);
            part.ISlot = GetStringValue(info["islot"]);
            part.IsCash = GetIntValue(info["cash"]) == 1;
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
            part.IsNotForSale = GetIntValue(info["notSale"]) == 1;
            part.IsAccountSharable = GetIntValue(info["accountSharable"]) == 1;
            part.HasAccountShareTag = GetIntValue(info["accountShareTag"]) == 1;
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
                >= 130 and < 170 => "Weapon",
                180 => "TamingMob",
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
                >= 130 and < 170 => EquipSlot.Weapon,
                180 => EquipSlot.TamingMob,
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

        private void LoadPartAnimations(CharacterPart part, WzImage img, bool includeAttackActions = true)
        {
            if (img == null) return;

            img.ParseImage();

            // Debug: list top-level nodes in the image
            System.Diagnostics.Debug.WriteLine($"[CharacterLoader] LoadPartAnimations for {part.Name}, nodes:");
            foreach (var prop in img.WzProperties)
            {
                System.Diagnostics.Debug.WriteLine($"  - {prop.Name} ({prop.GetType().Name})");
            }

            // Debug head structure specifically
            bool isHead = part.Name.Contains("Head");
            if (isHead)
            {
                var stand1Node = img["stand1"];
                if (stand1Node is WzSubProperty stand1Sub)
                {
                    System.Diagnostics.Debug.WriteLine($"[CharacterLoader] HEAD stand1 contents:");
                    foreach (var child in stand1Sub.WzProperties)
                    {
                        System.Diagnostics.Debug.WriteLine($"  - {child.Name} ({child.GetType().Name})");
                    }
                }
            }

            var actionsToLoad = BuildActionLoadOrder(img.WzProperties.Select(prop => prop.Name), includeAttackActions);

            foreach (var action in actionsToLoad)
            {
                var actionNode = img[action];
                if (actionNode != null)
                {
                    // Pass debug context for head to see structure
                    var anim = LoadAnimation(actionNode, isHead && action == "stand1" ? $"{part.Name}/{action}" : null);
                    if (anim != null && anim.Frames.Count > 0)
                    {
                        anim.ActionName = action;
                        anim.Action = CharacterPart.ParseActionString(action);
                        part.Animations[action] = anim;
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"[CharacterLoader] {part.Name} has {part.Animations.Count} animations");
        }

        private static IReadOnlyList<string> BuildActionLoadOrder(IEnumerable<string> availableActionNames, bool includeAttackActions)
        {
            var orderedActions = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddAction(string actionName)
            {
                if (string.IsNullOrWhiteSpace(actionName) || !seen.Add(actionName) || !LooksLikeActionName(actionName))
                {
                    return;
                }

                orderedActions.Add(actionName);
            }

            foreach (string action in StandardActions)
            {
                AddAction(action);
            }

            if (includeAttackActions)
            {
                foreach (string action in AttackActions)
                {
                    AddAction(action);
                }
            }

            if (availableActionNames != null)
            {
                foreach (string action in availableActionNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
                {
                    AddAction(action);
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

        private CharacterAnimation LoadAnimation(WzImageProperty node, string debugContext = null)
        {
            if (node == null) return null;

            if (node is WzUOLProperty actionUol && actionUol.LinkValue is WzImageProperty linkedActionNode)
            {
                return LoadAnimation(linkedActionNode, debugContext);
            }

            var anim = new CharacterAnimation();

            // Debug: show what's inside the action node
            if (debugContext != null && node is WzSubProperty debugSub)
            {
                System.Diagnostics.Debug.WriteLine($"[LoadAnimation] {debugContext} contents:");
                foreach (var child in debugSub.WzProperties)
                {
                    System.Diagnostics.Debug.WriteLine($"  - {child.Name} ({child.GetType().Name})");
                }
            }

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
                        var frame = LoadFrame(resolvedFrameCanvas, i.ToString());
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
                    System.Diagnostics.Debug.WriteLine($"[LoadAnimation] Found frames but no canvas, checking for 'head' inside each frame");
                    for (int i = 0; i < 100; i++)
                    {
                        var frameNode = subProp[i.ToString()];
                        if (frameNode == null)
                            break;

                        if (frameNode is WzSubProperty frameSub)
                        {
                            // Debug: show what's inside frame 0
                            if (i == 0)
                            {
                                System.Diagnostics.Debug.WriteLine($"[LoadAnimation] Frame 0 contents:");
                                foreach (var fc in frameSub.WzProperties)
                                {
                                    System.Diagnostics.Debug.WriteLine($"  - {fc.Name} ({fc.GetType().Name})");
                                }
                            }

                            // Look for "head" property inside the frame
                            var headProp = frameSub["head"];
                            WzCanvasProperty headCanvas = null;

                            // Resolve UOL (User Object Link) to get actual canvas
                            if (headProp is WzUOLProperty uol)
                            {
                                var resolved = uol.LinkValue;
                                System.Diagnostics.Debug.WriteLine($"[LoadAnimation] Resolved UOL to: {resolved?.GetType().Name ?? "NULL"}");
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
                                var frame = LoadFrame(headCanvas, i.ToString());
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
                                        var frame = LoadFrame(foundCanvas, child.Name);
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
                    System.Diagnostics.Debug.WriteLine($"[LoadAnimation] No direct frames, checking for 'head' subnode");
                    var headNode = subProp["head"];
                    System.Diagnostics.Debug.WriteLine($"[LoadAnimation] headNode: {headNode?.GetType().Name ?? "NULL"}");

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

        private CharacterFrame LoadFrame(WzCanvasProperty canvas, string frameName)
        {
            if (canvas == null) return null;

            var texture = LoadTexture(canvas);
            if (texture == null) return null;

            // Default to 200ms for character animations (MapleStory typically uses 200-300ms per frame)
            // The delay may be overridden by the parent frame node in LoadBodyFrameWithSubParts
            var frame = new CharacterFrame
            {
                Texture = texture,
                Delay = GetIntValue(canvas["delay"]) ?? 200
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
                52 => "double bowgun",
                53 => "cannon",
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
