using System;
using System.Linq;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using HaSharedLibrary;
using HaSharedLibrary.Util;
using HaSharedLibrary.Wz;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HaCreator.MapSimulator.Pools;
using Point = Microsoft.Xna.Framework.Point;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace HaCreator.MapSimulator.Loaders
{
    /// <summary>
    /// Handles loading of life objects (Mobs, NPCs) for MapSimulator.
    /// Extracted from MapSimulatorLoader for better code organization.
    /// </summary>
    public static class LifeLoader
    {
        private sealed class CachedMobAttackAssets
        {
            public readonly Dictionary<string, MobAnimationSet.AttackInfoMetadata> AttackMetadata = new(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, List<MobAnimationSet.AttackHitEffectEntry>> AttackHitEffects = new(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, List<IDXObject>> AttackProjectileEffects = new(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, List<IDXObject>> AttackEffects = new(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, List<IDXObject>> AttackWarningEffects = new(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, List<MobAnimationSet.AttackEffectNode>> AttackExtraEffects = new(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<int, List<IDXObject>> AngerGaugeAnimations = new();
            public List<IDXObject> AngerGaugeEffect;
            public string AngerGaugeEffectPath;
        }

        private sealed class CachedMobActionAssets
        {
            public readonly Dictionary<int, CachedMobActionEntry> ActionEntriesByClientSlot = new();
            public readonly Dictionary<string, CachedMobActionEntry> ActionEntriesByAuthoredName = new(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class CachedMobActionEntry
        {
            public string ActionName { get; init; }
            public int? ClientActionSlot { get; init; }
            public int SourcePriority { get; init; }
            public List<WzCanvasProperty> FrameCanvases { get; init; }
            public List<int> FrameDelays { get; init; }
            public List<MobAnimationSet.FrameMetadata> FrameMetadata { get; init; }
            public List<CachedMobFrameOverlay> FrameOverlays { get; init; }
            public MobAnimationSet.ActionSpeakMetadata ActionSpeakMetadata { get; init; }
        }

        private sealed class CachedMobFrameOverlay
        {
            public string Name { get; init; }
            public string LayerZ { get; init; }
            public List<WzCanvasProperty> FrameCanvases { get; init; }
            public List<int> FrameDelays { get; init; }
        }

        private sealed class MobImgEntry
        {
            public MobImgEntry(string templateId, WzImage image, IReadOnlyList<WzImageProperty> wzProperties)
            {
                TemplateId = templateId;
                Image = image;
                WzProperties = wzProperties;
            }

            public string TemplateId { get; }
            public WzImage Image { get; }
            public IReadOnlyList<WzImageProperty> WzProperties { get; }
        }

        private static readonly ConditionalWeakTable<GraphicsDevice, ConcurrentDictionary<string, Lazy<CachedMobActionAssets>>> _cachedMobActionAssetsByDevice = new();
        private static readonly ConditionalWeakTable<GraphicsDevice, ConcurrentDictionary<string, Lazy<CachedMobAttackAssets>>> _cachedMobAttackAssetsByDevice = new();
        private static readonly ConcurrentDictionary<string, Lazy<MobImgEntry>> _cachedMobImgEntries = new(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, byte> _missingMobSoundIds = new(StringComparer.Ordinal);
        private const int MobClientActionSlotCount = 43;
        private const int MobClientAttackActionSlotStart = 13;
        private const int MobClientAttackActionSlotEnd = 21;
        private const int MobClientSkillActionSlotStart = 22;
        private const int MobClientSkillActionSlotEnd = 38;

        // Recovered client-owned slot surface from CActionMan/CMob seams:
        // - CMob::CMob allocates 43 action slots (0x2B)
        // - CUIMonsterBook::LoadMobAction loads attack 13..21 and skill 22..38 buckets
        // - CUIMonsterBook::LoadMobAction also explicitly probes 7..12 buckets
        // Unknown slots remain explicit null entries until fully recovered.
        private static readonly IReadOnlyDictionary<int, string> _mobClientCanonicalActionNamesBySlot =
            BuildMobClientCanonicalActionNamesBySlot();

        private static readonly string[] _mobClientActionNamesBySlot = BuildMobClientActionNamesBySlot();

        // These slots are explicitly client-owned by the 43-slot contract, but native names are not yet recovered.
        private static readonly IReadOnlyCollection<int> _mobClientUnresolvedActionSlots = Array.Empty<int>();

        private static readonly IReadOnlyDictionary<string, int> _mobClientActionSlotsByName =
            BuildMobClientActionSlotsByName();

        private static readonly IReadOnlyDictionary<string, int> _mobClientActionSlotAliasesByName =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                // WZ mob roots still ship non-indexed hit/die branches on some templates;
                // canonicalize them onto the client-owned slot surface.
                ["hit"] = 3,
                ["die"] = 4,
                // Preserve the pre-slot-expansion simulator alias for MoveAction2RawAction case 16.
                ["moveaction16"] = 39
            };
        private sealed class CachedDoomMobAssets
        {
            public MobAnimationSet AnimationSet { get; init; }
            public Rectangle Footprint { get; init; }
        }

        private static IReadOnlyDictionary<int, string> BuildMobClientCanonicalActionNamesBySlot()
        {
            var mapping = new Dictionary<int, string>
            {
                [0] = "stand",
                [1] = "move",
                [2] = "fly",
                [3] = "hit1",
                [4] = "die1",
                [5] = "regen",
                // Recovered from `_dynamic_initializer_for__s_sMobAction__`: slot 6 uses StringPool id 0x0453.
                [6] = "bomb",
                // Recovered from `_dynamic_initializer_for__s_sMobAction__`:
                // slot 7 -> StringPool id 0x0442 (`hit1`),
                // slot 8 -> StringPool id 0x0443 (`hit2`),
                // slot 11 -> StringPool id 0x0451 (`die2`).
                [7] = "hit1",
                [8] = "hit2",
                [9] = "hit2",
                [10] = "die2",
                [11] = "die2",
                // Recovered from `_dynamic_initializer_for__s_sMobAction__`: slot 12 uses StringPool id 0x0452.
                [12] = "dieF",
                // `CMob::MoveAction2RawAction` case 16 resolves to slot 39; WZ action roots use `chase`.
                [39] = "chase"
            };

            for (int attackSlot = MobClientAttackActionSlotStart; attackSlot <= MobClientAttackActionSlotEnd; attackSlot++)
            {
                int attackIndex = attackSlot - MobClientAttackActionSlotStart + 1;
                mapping[attackSlot] = $"attack{attackIndex}";
            }

            for (int skillSlot = MobClientSkillActionSlotStart; skillSlot <= MobClientSkillActionSlotEnd; skillSlot++)
            {
                int skillIndex = skillSlot - MobClientSkillActionSlotStart + 1;
                mapping[skillSlot] = $"skill{skillIndex}";
            }

            // Recovered from `_dynamic_initializer_for__s_sMobAction__`:
            // slot 40 -> StringPool id 0x14F8 (`rollingSpin`)
            // slot 41 -> StringPool id 0x1AD3 (`siege_pre`)
            // slot 42 -> StringPool id 0x165D (`tornadoDashStop`)
            mapping[40] = "rollingSpin";
            mapping[41] = "siege_pre";
            mapping[42] = "tornadoDashStop";

            return mapping;
        }

        private static string[] BuildMobClientActionNamesBySlot()
        {
            var namesBySlot = new string[MobClientActionSlotCount];
            foreach (KeyValuePair<int, string> entry in _mobClientCanonicalActionNamesBySlot)
            {
                if (entry.Key >= 0 && entry.Key < namesBySlot.Length)
                {
                    namesBySlot[entry.Key] = entry.Value;
                }
            }

            return namesBySlot;
        }

        private static readonly ConditionalWeakTable<GraphicsDevice, Lazy<CachedDoomMobAssets>> _cachedDoomMobAssetsByDevice = new();

        private static IReadOnlyDictionary<string, int> BuildMobClientActionSlotsByName()
        {
            var slotsByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<int, string> pair in _mobClientCanonicalActionNamesBySlot.OrderBy(pair => pair.Key))
            {
                // Keep pre-existing primary-slot resolution stable for duplicated names.
                if (pair.Key is 7 or 8 or 11)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(pair.Value))
                {
                    slotsByName.TryAdd(pair.Value, pair.Key);
                }
            }

            foreach (int duplicateSlot in new[] { 7, 8, 11 })
            {
                if (_mobClientCanonicalActionNamesBySlot.TryGetValue(duplicateSlot, out string duplicateName) &&
                    !string.IsNullOrWhiteSpace(duplicateName))
                {
                    slotsByName.TryAdd(duplicateName, duplicateSlot);
                }
            }

            return slotsByName;
        }

        #region Mob
        /// <summary>
        /// Creates a MobItem with separate animations for each action (stand, move, fly, etc.)
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="mobInstance"></param>
        /// <param name="UserScreenScaleFactor"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <returns></returns>
        public static MobItem CreateMobFromProperty(
            TexturePool texturePool, MobInstance mobInstance, float UserScreenScaleFactor,
            GraphicsDevice device, SoundManager soundManager, ConcurrentBag<WzObject> usedProps)
        {
            MobInfo mobInfo = (MobInfo)mobInstance.BaseInfo;
            MobImgEntry source = GetMobImgEntry(mobInfo);

            // Create animation set to store frames per action
            MobAnimationSet animationSet = new MobAnimationSet();
            CachedDoomMobAssets doomAssets = GetOrBuildDoomMobAssets(texturePool, device, usedProps);
            CachedMobActionAssets cachedActionAssets = GetOrBuildCachedMobActionAssets(texturePool, mobInfo, source, device, usedProps);
            CachedMobAttackAssets cachedAttackAssets = GetOrBuildCachedMobAttackAssets(texturePool, mobInfo, source, device, usedProps);
            ApplyCachedMobActionAssets(animationSet, cachedActionAssets, texturePool, mobInstance.X, mobInstance.Y, device);
            ApplyCachedMobAttackAssets(animationSet, cachedAttackAssets);

            System.Drawing.Color color_foreGround = System.Drawing.Color.White; // mob foreground color
            NameTooltipItem nameTooltip = null;
            bool hasBossHpBar = mobInfo?.MobData?.HpTagColor > 0;
            if (!hasBossHpBar)
            {
                nameTooltip = MapSimulatorLoader.CreateNPCMobNameTooltip(
                    mobInstance.MobInfo.Name, mobInstance.X, mobInstance.Y, color_foreGround,
                    texturePool, UserScreenScaleFactor, device);
            }

            var mobItem = new MobItem(
                mobInstance,
                animationSet,
                nameTooltip,
                doomAssets?.AnimationSet);

            // Load mob-specific sounds from Sound.wz/Mob.img/{mobId}/
            mobItem.SetSoundManager(soundManager);
            LoadMobSounds(mobItem, mobInfo.ID, soundManager);

            return mobItem;
        }

        internal static MobAnimationSet CreateMobAttackPresentationSet(
            TexturePool texturePool,
            GraphicsDevice device,
            string mobTemplateId)
        {
            if (texturePool == null || device == null || string.IsNullOrWhiteSpace(mobTemplateId))
            {
                return null;
            }

            string normalizedTemplateId = NormalizeMobTemplateId(mobTemplateId);
            MobInfo mobInfo = MobInfo.Get(normalizedTemplateId.TrimStart('0'));
            if (mobInfo == null)
            {
                return null;
            }

            MobImgEntry source = GetMobImgEntry(mobInfo);
            CachedMobAttackAssets cachedAttackAssets = GetOrBuildCachedMobAttackAssets(
                texturePool,
                mobInfo,
                source,
                device,
                usedProps: null);
            if (cachedAttackAssets == null)
            {
                return null;
            }

            var animationSet = new MobAnimationSet();
            ApplyCachedMobAttackAssets(animationSet, cachedAttackAssets);
            return animationSet;
        }

        internal static string ResolveMobCharDamSoundKey(
            SoundManager soundManager,
            string mobTemplateId,
            int damageSoundIndex)
        {
            if (soundManager == null || string.IsNullOrWhiteSpace(mobTemplateId))
            {
                return null;
            }

            WzImage mobSoundImage = Program.FindImage("Sound", "Mob");
            if (mobSoundImage == null)
            {
                return null;
            }

            string normalizedTemplateId = NormalizeMobTemplateId(mobTemplateId);
            WzSubProperty mobSounds = mobSoundImage[normalizedTemplateId] as WzSubProperty;
            if (mobSounds == null)
            {
                return null;
            }

            string soundName = damageSoundIndex >= 2 ? "CharDam2" : "CharDam1";
            return RegisterSoundFromProperty(soundManager, normalizedTemplateId, soundName, mobSounds[soundName]);
        }

        private static CachedMobActionAssets GetOrBuildCachedMobActionAssets(
            TexturePool texturePool,
            MobInfo mobInfo,
            MobImgEntry source,
            GraphicsDevice device,
            ConcurrentBag<WzObject> usedProps)
        {
            if (mobInfo == null || source == null || device == null)
            {
                return null;
            }

            ConcurrentDictionary<string, Lazy<CachedMobActionAssets>> cacheByMobId =
                _cachedMobActionAssetsByDevice.GetValue(device, _ => new ConcurrentDictionary<string, Lazy<CachedMobActionAssets>>(StringComparer.Ordinal));

            Lazy<CachedMobActionAssets> lazyAssets = cacheByMobId.GetOrAdd(
                mobInfo.ID,
                _ => new Lazy<CachedMobActionAssets>(
                    () => BuildCachedMobActionAssets(texturePool, source, device, usedProps),
                    System.Threading.LazyThreadSafetyMode.ExecutionAndPublication));

            return lazyAssets.Value;
        }

        private static CachedMobAttackAssets GetOrBuildCachedMobAttackAssets(
            TexturePool texturePool,
            MobInfo mobInfo,
            MobImgEntry source,
            GraphicsDevice device,
            ConcurrentBag<WzObject> usedProps)
        {
            if (mobInfo == null || source == null || device == null)
            {
                return null;
            }

            ConcurrentDictionary<string, Lazy<CachedMobAttackAssets>> cacheByMobId =
                _cachedMobAttackAssetsByDevice.GetValue(device, _ => new ConcurrentDictionary<string, Lazy<CachedMobAttackAssets>>(StringComparer.Ordinal));

            Lazy<CachedMobAttackAssets> lazyAssets = cacheByMobId.GetOrAdd(
                mobInfo.ID,
                _ => new Lazy<CachedMobAttackAssets>(
                    () => BuildCachedMobAttackAssets(texturePool, mobInfo, source, device, usedProps),
                    System.Threading.LazyThreadSafetyMode.ExecutionAndPublication));

            return lazyAssets.Value;
        }

        private static MobImgEntry GetMobImgEntry(MobInfo mobInfo)
        {
            if (mobInfo == null || string.IsNullOrWhiteSpace(mobInfo.ID))
            {
                return null;
            }

            string templateId = NormalizeMobTemplateId(mobInfo.ID);
            Lazy<MobImgEntry> lazyEntry = _cachedMobImgEntries.GetOrAdd(
                templateId,
                _ => new Lazy<MobImgEntry>(
                    () => BuildMobImgEntry(templateId, new HashSet<string>(StringComparer.Ordinal)),
                    System.Threading.LazyThreadSafetyMode.ExecutionAndPublication));

            return lazyEntry.Value;
        }

        private static MobImgEntry BuildMobImgEntry(string templateId, HashSet<string> activeTemplateIds)
        {
            templateId = NormalizeMobTemplateId(templateId);
            if (!activeTemplateIds.Add(templateId))
            {
                return null;
            }

            WzImage mobImage = Program.FindImage("Mob", templateId + ".img");
            if (mobImage == null)
            {
                activeTemplateIds.Remove(templateId);
                return null;
            }

            if (!mobImage.Parsed)
            {
                mobImage.ParseImage();
            }

            var mergedProperties = new List<WzImageProperty>(mobImage.WzProperties);
            var currentNames = new HashSet<string>(
                mergedProperties.Select(property => property.Name),
                StringComparer.OrdinalIgnoreCase);

            string linkedTemplateId = GetMobImgEntryLinkTemplateId(mobImage);
            if (!string.IsNullOrWhiteSpace(linkedTemplateId))
            {
                MobImgEntry linkedEntry = BuildMobImgEntry(linkedTemplateId, activeTemplateIds);
                if (linkedEntry != null)
                {
                    foreach (WzImageProperty linkedProperty in linkedEntry.WzProperties)
                    {
                        if (linkedProperty == null ||
                            string.Equals(linkedProperty.Name, "info", StringComparison.OrdinalIgnoreCase) ||
                            currentNames.Contains(linkedProperty.Name))
                        {
                            continue;
                        }

                        mergedProperties.Add(linkedProperty);
                        currentNames.Add(linkedProperty.Name);
                    }
                }
            }

            activeTemplateIds.Remove(templateId);
            return new MobImgEntry(templateId, mobImage, mergedProperties);
        }

        private static string GetMobImgEntryLinkTemplateId(WzImage mobImage)
        {
            WzImageProperty linkProperty = WzInfoTools.GetRealProperty(mobImage?["info"]?["link"]);
            if (linkProperty is WzStringProperty stringLink &&
                !string.IsNullOrWhiteSpace(stringLink.Value))
            {
                return NormalizeMobTemplateId(stringLink.Value);
            }

            return null;
        }

        private static string NormalizeMobTemplateId(string templateId)
        {
            return WzInfoTools.AddLeadingZeros(templateId?.Trim() ?? string.Empty, 7);
        }

        private static CachedDoomMobAssets GetOrBuildDoomMobAssets(
            TexturePool texturePool,
            GraphicsDevice device,
            ConcurrentBag<WzObject> usedProps)
        {
            if (texturePool == null || device == null)
            {
                return null;
            }

            Lazy<CachedDoomMobAssets> lazyAssets = _cachedDoomMobAssetsByDevice.GetValue(
                device,
                _ => new Lazy<CachedDoomMobAssets>(
                    () => BuildDoomMobAssets(texturePool, device, usedProps),
                    System.Threading.LazyThreadSafetyMode.ExecutionAndPublication));

            return lazyAssets.Value;
        }

        private static CachedDoomMobAssets BuildDoomMobAssets(
            TexturePool texturePool,
            GraphicsDevice device,
            ConcurrentBag<WzObject> usedProps)
        {
            WzImage doomImage = Program.FindImage("Mob", "0100101.img");
            if (doomImage == null)
            {
                return null;
            }

            if (!doomImage.Parsed)
            {
                doomImage.ParseImage();
            }

            var animationSet = new MobAnimationSet();
            foreach (WzImageProperty childProperty in doomImage.WzProperties)
            {
                if (childProperty is not WzSubProperty mobStateProperty)
                {
                    continue;
                }

                string actionName = mobStateProperty.Name.ToLowerInvariant();
                if (actionName != "stand" &&
                    actionName != "move" &&
                    actionName != "hit" &&
                    actionName != "hit1" &&
                    actionName != "die" &&
                    actionName != "die1")
                {
                    continue;
                }

                List<IDXObject> actionFrames = MapSimulatorLoader.LoadFrames(texturePool, mobStateProperty, 0, 0, device, usedProps);
                if (actionFrames.Count > 0)
                {
                    animationSet.AddAnimation(actionName, actionFrames);
                }
            }

            if (animationSet.ActionCount <= 0)
            {
                return null;
            }

            return new CachedDoomMobAssets
            {
                AnimationSet = animationSet,
                Footprint = ResolveDoomFootprint(doomImage)
            };
        }

        private static Rectangle ResolveDoomFootprint(WzImage doomImage)
        {
            if (doomImage?["stand"]?["0"] is not WzCanvasProperty standFrame)
            {
                return new Rectangle(-16, -34, 35, 34);
            }

            Point lt = ResolveCanvasVector(standFrame["lt"], -16, -34);
            Point rb = ResolveCanvasVector(standFrame["rb"], 19, 0);
            int width = Math.Max(1, rb.X - lt.X);
            int height = Math.Max(1, rb.Y - lt.Y);
            return new Rectangle(lt.X, lt.Y, width, height);
        }

        private static Point ResolveCanvasVector(WzImageProperty property, int fallbackX, int fallbackY)
        {
            if (property is WzVectorProperty vectorProperty)
            {
                return new Point(vectorProperty.X.Value, vectorProperty.Y.Value);
            }

            return new Point(fallbackX, fallbackY);
        }

        private static CachedMobActionAssets BuildCachedMobActionAssets(
            TexturePool texturePool,
            MobImgEntry source,
            GraphicsDevice device,
            ConcurrentBag<WzObject> usedProps)
        {
            var cached = new CachedMobActionAssets();
            foreach (WzImageProperty childProperty in source.WzProperties)
            {
                if (childProperty is not WzSubProperty mobStateProperty)
                {
                    continue;
                }

                string authoredActionName = mobStateProperty.Name.ToLowerInvariant();
                if (!ShouldLoadMobActionFrames(authoredActionName))
                {
                    continue;
                }

                MobAnimationSet.ActionSpeakMetadata actionSpeakMetadata = BuildMobActionSpeakMetadata(mobStateProperty["speak"]);
                List<WzCanvasProperty> frameCanvases = BuildMobActionFrameCanvases(mobStateProperty);
                if (frameCanvases.Count <= 0 && actionSpeakMetadata == null)
                {
                    continue;
                }

                List<MobAnimationSet.FrameMetadata> frameMetadata = frameCanvases.Count > 0
                    ? BuildMobActionFrameMetadata(mobStateProperty)
                    : new List<MobAnimationSet.FrameMetadata>();
                List<int> frameDelays = frameCanvases.Count > 0
                    ? BuildMobActionFrameDelays(frameCanvases)
                    : new List<int>();
                List<CachedMobFrameOverlay> frameOverlays = frameCanvases.Count > 0
                    ? BuildMobActionFrameOverlays(mobStateProperty)
                    : new List<CachedMobFrameOverlay>();

                if (frameCanvases.Count > 0)
                {
                    if (frameMetadata.Count != frameCanvases.Count)
                    {
                        frameMetadata = AlignFrameMetadataToFrames(frameCanvases.Count, frameMetadata);
                    }

                    if (ShouldAppendReversePlayback(mobStateProperty))
                    {
                        AppendReversePlayback(frameCanvases, frameDelays, frameMetadata);
                        foreach (CachedMobFrameOverlay overlay in frameOverlays)
                        {
                            AppendReversePlayback(overlay.FrameCanvases, overlay.FrameDelays, null);
                        }
                    }
                }

                if (TryResolveMobClientActionSlot(authoredActionName, out int clientActionSlot))
                {
                    string resolvedActionName = ResolveMobClientActionName(clientActionSlot) ?? authoredActionName;
                    var resolvedEntry = new CachedMobActionEntry
                    {
                        ActionName = resolvedActionName,
                        ClientActionSlot = clientActionSlot,
                        SourcePriority = string.Equals(authoredActionName, resolvedActionName, StringComparison.OrdinalIgnoreCase) ? 1 : 0,
                        FrameCanvases = frameCanvases,
                        FrameDelays = frameDelays,
                        FrameMetadata = frameMetadata,
                        FrameOverlays = frameOverlays,
                        ActionSpeakMetadata = actionSpeakMetadata
                    };

                    if (!cached.ActionEntriesByClientSlot.TryGetValue(clientActionSlot, out CachedMobActionEntry existingEntry)
                        || existingEntry == null
                        || resolvedEntry.SourcePriority >= existingEntry.SourcePriority)
                    {
                        cached.ActionEntriesByClientSlot[clientActionSlot] = resolvedEntry;
                    }

                    // Keep authored-name lookups available even when canonical slot naming differs (e.g. hit -> hit1).
                    if (!string.Equals(authoredActionName, resolvedActionName, StringComparison.OrdinalIgnoreCase))
                    {
                        cached.ActionEntriesByAuthoredName[authoredActionName] = new CachedMobActionEntry
                        {
                            ActionName = authoredActionName,
                            ClientActionSlot = clientActionSlot,
                            SourcePriority = resolvedEntry.SourcePriority,
                            FrameCanvases = frameCanvases,
                            FrameDelays = frameDelays,
                            FrameMetadata = frameMetadata,
                            FrameOverlays = frameOverlays,
                            ActionSpeakMetadata = actionSpeakMetadata
                        };
                    }

                    continue;
                }

                cached.ActionEntriesByAuthoredName[authoredActionName] = new CachedMobActionEntry
                {
                    ActionName = authoredActionName,
                    SourcePriority = 0,
                    FrameCanvases = frameCanvases,
                    FrameDelays = frameDelays,
                    FrameMetadata = frameMetadata,
                    FrameOverlays = frameOverlays,
                    ActionSpeakMetadata = actionSpeakMetadata
                };
            }

            return cached;
        }

        private static CachedMobAttackAssets BuildCachedMobAttackAssets(
            TexturePool texturePool,
            MobInfo mobInfo,
            MobImgEntry source,
            GraphicsDevice device,
            ConcurrentBag<WzObject> usedProps)
        {
            var cached = new CachedMobAttackAssets();

            foreach (WzImageProperty childProperty in source.WzProperties)
            {
                if (childProperty is not WzSubProperty mobStateProperty)
                {
                    continue;
                }

                string authoredActionName = mobStateProperty.Name.ToLowerInvariant();
                string actionName = ResolveMobClientActionNameOrAuthored(authoredActionName);

                if (actionName == "angergaugeeffect")
                {
                    List<IDXObject> effectFrames = MapSimulatorLoader.LoadFrames(texturePool, mobStateProperty, 0, 0, device, usedProps);
                    if (effectFrames.Count > 0)
                    {
                        cached.AngerGaugeEffect = effectFrames;
                        string loadedEffectPath = MobAngerGaugeBurstParity.ResolveLoadedEffectPath(
                            mobInfo.ID,
                            mobStateProperty.FullPath);
                        cached.AngerGaugeEffectPath = MobAngerGaugeBurstParity.ResolveOwnerEffectPath(
                            mobInfo.ID,
                            loadedEffectPath);
                    }

                    continue;
                }

                if (actionName == "angergaugeanimation")
                {
                    foreach (WzImageProperty gaugeStageProperty in mobStateProperty.WzProperties)
                    {
                        if (!int.TryParse(gaugeStageProperty.Name, out int stageIndex))
                        {
                            continue;
                        }

                        List<IDXObject> gaugeFrames = MapSimulatorLoader.LoadFrames(texturePool, gaugeStageProperty, 0, 0, device, usedProps);
                        if (gaugeFrames.Count > 0)
                        {
                            cached.AngerGaugeAnimations[stageIndex] = gaugeFrames;
                        }
                    }

                    continue;
                }

                if (!ShouldLoadAttackSupportAssetsForAction(actionName))
                {
                    continue;
                }

                WzImageProperty infoProperty = WzInfoTools.GetRealProperty(mobStateProperty["info"]);
                WzSubProperty infoNode = infoProperty as WzSubProperty;
                MobAnimationSet.AttackInfoMetadata attackInfo = BuildAttackInfoMetadata(infoProperty, mobStateProperty);
                if (attackInfo != null)
                {
                    cached.AttackMetadata[actionName] = attackInfo;
                }

                IReadOnlyList<(WzImageProperty HitNode, int SourceFrameIndex, bool IsAttackFrameOwned)> hitNodes =
                    ResolveAttackHitNodes(infoProperty, mobStateProperty);
                int hitAnimationSourceFrameIndex = hitNodes.Count > 0 ? hitNodes[0].SourceFrameIndex : 0;
                if (attackInfo != null)
                {
                    attackInfo.HitAnimationSourceFrameIndex = hitAnimationSourceFrameIndex;
                }

                foreach ((WzImageProperty hitNode, int sourceFrameIndex, bool isAttackFrameOwned) in hitNodes)
                {
                    List<IDXObject> hitFrames = MapSimulatorLoader.LoadFrames(texturePool, hitNode, 0, 0, device, usedProps);
                    if (hitFrames.Count > 0)
                    {
                        if (isAttackFrameOwned)
                        {
                            attackInfo?.RegisterFrameOwnedHitMetadataRange(sourceFrameIndex, hitFrames.Count);
                        }

                        AddCachedAttackHitEffect(
                            cached,
                            actionName,
                            hitFrames,
                            sourceFrameIndex,
                            isAttackFrameOwned);
                        System.Diagnostics.Debug.WriteLine($"[LifeLoader] Loaded {hitFrames.Count} hit effect frames for mob {mobInfo.ID} {actionName}");
                    }
                }

                WzImageProperty ballNode = infoNode?["ball"] ?? mobStateProperty["ball"];
                if (ballNode != null)
                {
                    List<IDXObject> ballFrames = MapSimulatorLoader.LoadFrames(texturePool, ballNode, 0, 0, device, usedProps);
                    if (ballFrames.Count > 0)
                    {
                        cached.AttackProjectileEffects[actionName] = ballFrames;
                        System.Diagnostics.Debug.WriteLine($"[LifeLoader] Loaded {ballFrames.Count} projectile frames for mob {mobInfo.ID} {actionName}");
                    }
                }

                List<WzImageProperty> attackEffectProperties = GetAttackEffectProperties(infoNode);
                Dictionary<string, int> plainEffectGrouping = BuildPlainAttackEffectGrouping(
                    attackEffectProperties
                        .Where(property =>
                            TryGetAttackEffectNodeOrder(property?.Name, out int order) &&
                            order >= 0 &&
                            !HasStructuredAttackEffectMetadata(property as WzSubProperty))
                        .Select(property => property.Name));

                foreach (WzImageProperty attackEffectProperty in attackEffectProperties)
                {
                    bool isPrimaryEffect = string.Equals(attackEffectProperty.Name, "effect", StringComparison.OrdinalIgnoreCase);
                    if (TryBuildAttackEffectNode(texturePool, attackEffectProperty, device, usedProps, out var structuredEffectNode))
                    {
                        AddCachedAttackExtraEffect(cached, actionName, structuredEffectNode);
                        continue;
                    }

                    if (TryBuildPlainAttackEffectNode(texturePool, attackEffectProperty, device, usedProps, out var plainEffectNode))
                    {
                        if (plainEffectGrouping.TryGetValue(attackEffectProperty.Name, out int groupIndex))
                        {
                            plainEffectNode.RangeGroupIndex = groupIndex;
                            plainEffectNode.RangeGroupCount = plainEffectGrouping.Count;
                            AddCachedAttackExtraEffect(cached, actionName, plainEffectNode);
                            continue;
                        }

                        if (isPrimaryEffect)
                        {
                            cached.AttackEffects[actionName] = plainEffectNode.Sequences[0];
                        }

                        continue;
                    }

                    if (!isPrimaryEffect)
                    {
                        continue;
                    }

                    List<IDXObject> effectFrames = MapSimulatorLoader.LoadFrames(texturePool, attackEffectProperty, 0, 0, device, usedProps);
                    if (effectFrames.Count > 0)
                    {
                        cached.AttackEffects[actionName] = effectFrames;
                    }
                }

                WzImageProperty warningNode = infoNode?["areaWarning"];
                if (warningNode != null)
                {
                    List<IDXObject> warningFrames = MapSimulatorLoader.LoadFrames(texturePool, warningNode, 0, 0, device, usedProps);
                    if (warningFrames.Count > 0)
                    {
                        cached.AttackWarningEffects[actionName] = warningFrames;
                    }
                }
            }

            return cached;
        }

        private static void ApplyCachedMobActionAssets(
            MobAnimationSet animationSet,
            CachedMobActionAssets cachedAssets,
            TexturePool texturePool,
            int x,
            int y,
            GraphicsDevice device)
        {
            if (animationSet == null || cachedAssets == null || texturePool == null || device == null)
            {
                return;
            }

            var appliedActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<int, CachedMobActionEntry> entry in cachedAssets.ActionEntriesByClientSlot.OrderBy(pair => pair.Key))
            {
                CachedMobActionEntry actionEntry = entry.Value;
                if (actionEntry == null)
                {
                    continue;
                }

                string actionName = actionEntry.ActionName;
                if (string.IsNullOrWhiteSpace(actionName))
                {
                    actionName = ResolveMobClientActionName(entry.Key) ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(actionName))
                {
                    continue;
                }

                if (actionEntry.FrameCanvases != null && actionEntry.FrameCanvases.Count > 0)
                {
                    List<IDXObject> actionFrames = InstantiateMobActionFrames(
                        texturePool,
                        actionEntry.FrameCanvases,
                        actionEntry.FrameDelays,
                        x,
                        y,
                        device);
                    animationSet.AddAnimation(actionName, actionFrames);

                    if (actionEntry.FrameMetadata != null && actionEntry.FrameMetadata.Count > 0)
                    {
                        animationSet.SetFrameMetadata(actionName, actionEntry.FrameMetadata);
                    }

                    ApplyCachedMobFrameOverlays(animationSet, actionName, actionEntry, texturePool, x, y, device);
                }

                ApplyCachedMobActionSpeakMetadata(animationSet, actionName, actionEntry);

                appliedActions.Add(actionName);
            }

            foreach (KeyValuePair<string, CachedMobActionEntry> entry in cachedAssets.ActionEntriesByAuthoredName)
            {
                CachedMobActionEntry actionEntry = entry.Value;
                if (actionEntry == null)
                {
                    continue;
                }

                string actionName = actionEntry.ActionName ?? entry.Key;
                if (string.IsNullOrWhiteSpace(actionName) || appliedActions.Contains(actionName))
                {
                    continue;
                }

                if (actionEntry.FrameCanvases != null && actionEntry.FrameCanvases.Count > 0)
                {
                    List<IDXObject> actionFrames = InstantiateMobActionFrames(
                        texturePool,
                        actionEntry.FrameCanvases,
                        actionEntry.FrameDelays,
                        x,
                        y,
                        device);
                    animationSet.AddAnimation(actionName, actionFrames);

                    if (actionEntry.FrameMetadata != null && actionEntry.FrameMetadata.Count > 0)
                    {
                        animationSet.SetFrameMetadata(actionName, actionEntry.FrameMetadata);
                    }

                    ApplyCachedMobFrameOverlays(animationSet, actionName, actionEntry, texturePool, x, y, device);
                }

                ApplyCachedMobActionSpeakMetadata(animationSet, actionName, actionEntry);
            }
        }

        private static void ApplyCachedMobActionSpeakMetadata(
            MobAnimationSet animationSet,
            string actionName,
            CachedMobActionEntry actionEntry)
        {
            if (animationSet == null ||
                string.IsNullOrWhiteSpace(actionName) ||
                actionEntry?.ActionSpeakMetadata == null)
            {
                return;
            }

            animationSet.SetActionSpeakMetadata(actionName, actionEntry.ActionSpeakMetadata);
        }

        private static void ApplyCachedMobFrameOverlays(
            MobAnimationSet animationSet,
            string actionName,
            CachedMobActionEntry actionEntry,
            TexturePool texturePool,
            int x,
            int y,
            GraphicsDevice device)
        {
            if (animationSet == null ||
                string.IsNullOrWhiteSpace(actionName) ||
                actionEntry?.FrameOverlays == null ||
                actionEntry.FrameOverlays.Count == 0)
            {
                return;
            }

            foreach (CachedMobFrameOverlay overlay in actionEntry.FrameOverlays)
            {
                List<IDXObject> overlayFrames = InstantiateMobActionFrames(
                    texturePool,
                    overlay.FrameCanvases,
                    overlay.FrameDelays,
                    x,
                    y,
                    device);
                animationSet.AddFrameOverlay(
                    actionName,
                    new MobAnimationSet.FrameOverlay
                    {
                        Name = overlay.Name,
                        LayerZ = overlay.LayerZ,
                        Frames = overlayFrames
                    });
            }
        }

        private static void ApplyCachedMobAttackAssets(MobAnimationSet animationSet, CachedMobAttackAssets cachedAssets)
        {
            if (animationSet == null || cachedAssets == null)
            {
                return;
            }

            foreach (KeyValuePair<int, List<IDXObject>> entry in cachedAssets.AngerGaugeAnimations)
            {
                animationSet.SetAngerGaugeAnimation(entry.Key, entry.Value);
            }

            if (cachedAssets.AngerGaugeEffect != null)
            {
                animationSet.SetAngerGaugeEffect(cachedAssets.AngerGaugeEffect, cachedAssets.AngerGaugeEffectPath);
            }

            foreach (KeyValuePair<string, MobAnimationSet.AttackInfoMetadata> entry in cachedAssets.AttackMetadata)
            {
                animationSet.SetAttackInfoMetadata(entry.Key, entry.Value);
            }

            foreach (KeyValuePair<string, List<MobAnimationSet.AttackHitEffectEntry>> entry in cachedAssets.AttackHitEffects)
            {
                foreach (MobAnimationSet.AttackHitEffectEntry hitEffectEntry in entry.Value)
                {
                    animationSet.AddAttackHitEffect(
                        entry.Key,
                        hitEffectEntry.Frames,
                        hitEffectEntry.SourceFrameIndex,
                        hitEffectEntry.IsAttackFrameOwned,
                        hitEffectEntry.UsesAttackInfoHitEffect);
                }
            }

            foreach (KeyValuePair<string, List<IDXObject>> entry in cachedAssets.AttackProjectileEffects)
            {
                animationSet.AddAttackProjectileEffect(entry.Key, entry.Value);
            }

            foreach (KeyValuePair<string, List<IDXObject>> entry in cachedAssets.AttackEffects)
            {
                animationSet.AddAttackEffect(entry.Key, entry.Value);
            }

            foreach (KeyValuePair<string, List<IDXObject>> entry in cachedAssets.AttackWarningEffects)
            {
                animationSet.AddAttackWarningEffect(entry.Key, entry.Value);
            }

            foreach (KeyValuePair<string, List<MobAnimationSet.AttackEffectNode>> entry in cachedAssets.AttackExtraEffects)
            {
                foreach (MobAnimationSet.AttackEffectNode effectNode in entry.Value)
                {
                    animationSet.AddAttackExtraEffect(entry.Key, effectNode);
                }
            }
        }

        private static void AddCachedAttackExtraEffect(
            CachedMobAttackAssets cachedAssets,
            string actionName,
            MobAnimationSet.AttackEffectNode effectNode)
        {
            if (cachedAssets == null || string.IsNullOrEmpty(actionName) || effectNode == null)
            {
                return;
            }

            if (!cachedAssets.AttackExtraEffects.TryGetValue(actionName, out List<MobAnimationSet.AttackEffectNode> effectNodes))
            {
                effectNodes = new List<MobAnimationSet.AttackEffectNode>();
                cachedAssets.AttackExtraEffects[actionName] = effectNodes;
            }

            effectNodes.Add(effectNode);
        }

        private static void AddCachedAttackHitEffect(
            CachedMobAttackAssets cachedAssets,
            string actionName,
            List<IDXObject> hitFrames,
            int sourceFrameIndex,
            bool isAttackFrameOwned,
            bool usesAttackInfoHitEffect = false)
        {
            if (cachedAssets == null || string.IsNullOrEmpty(actionName) || hitFrames == null || hitFrames.Count == 0)
            {
                return;
            }

            if (!cachedAssets.AttackHitEffects.TryGetValue(actionName, out List<MobAnimationSet.AttackHitEffectEntry> hitEffectEntries))
            {
                hitEffectEntries = new List<MobAnimationSet.AttackHitEffectEntry>();
                cachedAssets.AttackHitEffects[actionName] = hitEffectEntries;
            }

            hitEffectEntries.Add(new MobAnimationSet.AttackHitEffectEntry
            {
                Frames = hitFrames,
                SourceFrameIndex = sourceFrameIndex,
                IsAttackFrameOwned = isAttackFrameOwned,
                UsesAttackInfoHitEffect = usesAttackInfoHitEffect
            });
        }

        /// <summary>
        /// Loads mob-specific sounds from Sound.wz/Mob.img/{mobId}/
        /// </summary>
        private static void LoadMobSounds(MobItem mobItem, string mobId, SoundManager soundManager)
        {
            if (string.IsNullOrEmpty(mobId))
            {
                System.Diagnostics.Debug.WriteLine($"[LifeLoader] LoadMobSounds: mobId is null or empty");
                return;
            }
            WzImage mobSoundImage = Program.FindImage("Sound", "Mob");
            if (mobSoundImage == null)
            {
                System.Diagnostics.Debug.WriteLine($"[LifeLoader] LoadMobSounds: Sound/Mob.img not found!");
                return;
            }

            // Look for the mob's sound directory (e.g., "0100100")
            WzSubProperty mobSounds = mobSoundImage[mobId.PadLeft(7, '0')] as WzSubProperty;
            if (mobSounds == null)
            {
                if (_missingMobSoundIds.TryAdd(mobId, 0))
                {
                    System.Diagnostics.Debug.WriteLine($"[LifeLoader] LoadMobSounds: Mob '{mobId}' not found in Mob.img. Available: {string.Join(", ", mobSoundImage.WzProperties?.Take(10).Select(p => p.Name) ?? new string[0])}...");
                }
                return;
            }

            // Load Damage sound
            string damageSE = RegisterSoundFromProperty(soundManager, mobId, "Damage", mobSounds["Damage"]);

            // Load Die sound
            string dieSE = RegisterSoundFromProperty(soundManager, mobId, "Die", mobSounds["Die"]);

            // Load Attack1 sound
            string attack1SE = RegisterSoundFromProperty(soundManager, mobId, "Attack1", mobSounds["Attack1"]);

            // Load Attack2 sound
            string attack2SE = RegisterSoundFromProperty(soundManager, mobId, "Attack2", mobSounds["Attack2"]);

            // Load CharDam1 sound (character damage when mob hits player)
            string charDam1SE = RegisterSoundFromProperty(soundManager, mobId, "CharDam1", mobSounds["CharDam1"]);

            // Load CharDam2 sound
            string charDam2SE = RegisterSoundFromProperty(soundManager, mobId, "CharDam2", mobSounds["CharDam2"]);

            // Set sounds on mob item
            if (damageSE != null || dieSE != null)
            {
                mobItem.SetSounds(damageSE, dieSE);
            }

            if (attack1SE != null || attack2SE != null)
            {
                mobItem.SetAttackSounds(attack1SE, attack2SE);
            }

            if (charDam1SE != null || charDam2SE != null)
            {
                mobItem.SetCharDamSounds(charDam1SE, charDam2SE);
            }
        }

        /// <summary>
        /// Helper method to load a sound from a WZ property (handles UOL links)
        /// </summary>
        private static string RegisterSoundFromProperty(SoundManager soundManager, string mobId, string soundName, WzImageProperty prop)
        {
            if (prop == null || soundManager == null)
                return null;

            WzBinaryProperty soundProp = prop as WzBinaryProperty
                ?? (prop as WzUOLProperty)?.LinkValue as WzBinaryProperty;

            if (soundProp != null)
            {
                string soundKey = $"Mob:{mobId}:{soundName}";
                soundManager.RegisterSound(soundKey, soundProp);
                return soundKey;
            }
            return null;
        }

        internal static MobAnimationSet.AttackInfoMetadata BuildAttackInfoMetadata(
            WzImageProperty infoProperty,
            WzImageProperty attackStateProperty = null)
        {
            WzSubProperty infoNode = WzInfoTools.GetRealProperty(infoProperty) as WzSubProperty;
            if (infoNode == null)
            {
                return null;
            }

            WzImageProperty infoHitNode = WzInfoTools.GetRealProperty(infoNode["hit"]);
            int explicitInfoHitAttach = ReadOptionalInt(infoHitNode, int.MinValue, "attach", "bHitAttach", "hitAttach");
            int explicitInfoFacingAttach = ReadOptionalInt(
                infoHitNode,
                int.MinValue,
                "attachfacing",
                "bFacingAttach",
                "bFacingAttatch",
                "facingAttach");
            int infoAliasHitAttach = ReadOptionalInt(infoNode, int.MinValue, "attach", "bHitAttach", "hitAttach");
            int infoAliasFacingAttach = ReadOptionalInt(
                infoNode,
                int.MinValue,
                "attachfacing",
                "bFacingAttach",
                "bFacingAttatch",
                "facingAttach");
            int resolvedFacingAttach = explicitInfoFacingAttach != int.MinValue
                ? explicitInfoFacingAttach
                : infoAliasFacingAttach != int.MinValue
                    ? infoAliasFacingAttach
                    : int.MinValue;
            bool facingAttach = resolvedFacingAttach > 0;
            int resolvedHitAttach = explicitInfoHitAttach != int.MinValue
                ? explicitInfoHitAttach
                : infoAliasHitAttach != int.MinValue
                    ? infoAliasHitAttach
                    : int.MinValue;
            bool hitAttach = resolvedHitAttach != int.MinValue
                ? resolvedHitAttach > 0
                : facingAttach;
            WzSubProperty effectNode = WzInfoTools.GetRealProperty(infoNode["effect"]) as WzSubProperty;
            bool effectFacingAttach = ReadOptionalInt(
                effectNode,
                0,
                "attachfacing",
                "bFacingAttach",
                "bFacingAttatch",
                "facingAttach") > 0;

            var metadata = new MobAnimationSet.AttackInfoMetadata
            {
                AttackType = InfoTool.GetInt(infoNode["type"], -1),
                MagicAttack = ReadOptionalInt(infoNode, 0, "magicAttack", "bMagicAttack") > 0,
                HitAttach = hitAttach,
                HasHitAttachMetadata = resolvedHitAttach != int.MinValue || resolvedFacingAttach != int.MinValue,
                FacingAttach = facingAttach,
                HasFacingAttachMetadata = resolvedFacingAttach != int.MinValue,
                HitAfterMs = Math.Max(0, ReadOptionalInt(infoHitNode, 0, "hitAfter")),
                HasHitAfterMetadata = WzInfoTools.GetRealProperty(infoHitNode?["hitAfter"]) != null,
                EffectFacingAttach = effectFacingAttach,
                EffectAfter = InfoTool.GetInt(infoNode["effectAfter"], 0),
                AttackAfter = InfoTool.GetInt(infoNode["attackAfter"], 0),
                RandDelayAttack = InfoTool.GetInt(infoNode["randDelayAttack"], 0),
                HasPrimaryEffect = infoNode["effect"] != null,
                HasAreaWarning = infoNode["areaWarning"] != null,
                IsRushAttack = InfoTool.GetInt(infoNode["rush"], 0) > 0,
                IsJumpAttack = InfoTool.GetInt(infoNode["jumpAttack"], 0) > 0,
                Tremble = InfoTool.GetInt(infoNode["tremble"], 0) > 0,
                IsAngerAttack = InfoTool.GetInt(infoNode["AngerAttack"], 0) > 0,
                IsSpecialAttack = InfoTool.GetInt(infoNode["specialAttack"], 0) > 0
            };

            foreach ((int frameIndex, bool attach) in ReadIndexedHitMetadataFlags(
                         infoHitNode,
                         "attach",
                         "bHitAttach",
                         "hitAttach"))
            {
                metadata.FrameHitAttachOverrides[frameIndex] = attach;
            }

            foreach ((int frameIndex, bool attach) in ReadIndexedAttackFrameHitMetadataFlags(
                         attackStateProperty,
                         "attach",
                         "bHitAttach",
                         "hitAttach"))
            {
                metadata.FrameHitAttachOverrides[frameIndex] = attach;
            }

            foreach ((int frameIndex, bool attachFacing) in ReadIndexedHitMetadataFlags(
                         infoHitNode,
                         "attachfacing",
                         "bFacingAttach",
                         "bFacingAttatch",
                         "facingAttach"))
            {
                metadata.FrameFacingAttachOverrides[frameIndex] = attachFacing;
            }

            foreach ((int frameIndex, bool attachFacing) in ReadIndexedAttackFrameHitMetadataFlags(
                         attackStateProperty,
                         "attachfacing",
                         "bFacingAttach",
                         "bFacingAttatch",
                         "facingAttach"))
            {
                metadata.FrameFacingAttachOverrides[frameIndex] = attachFacing;
            }

            WzSubProperty rangeNode = infoNode["range"] as WzSubProperty;
            if (rangeNode != null)
            {
                WzVectorProperty sp = rangeNode["sp"] as WzVectorProperty;
                if (sp != null)
                {
                    metadata.HasRangeOrigin = true;
                    metadata.RangeOrigin = new Point(sp.X.Value, sp.Y.Value);
                }

                metadata.RangeRadius = InfoTool.GetInt(rangeNode["r"], 0);

                WzVectorProperty lt = rangeNode["lt"] as WzVectorProperty;
                WzVectorProperty rb = rangeNode["rb"] as WzVectorProperty;
                if (lt != null && rb != null)
                {
                    int left = System.Math.Min(lt.X.Value, rb.X.Value);
                    int right = System.Math.Max(lt.X.Value, rb.X.Value);
                    int top = System.Math.Min(lt.Y.Value, rb.Y.Value);
                    int bottom = System.Math.Max(lt.Y.Value, rb.Y.Value);
                    metadata.HasRangeBounds = true;
                    metadata.RangeBounds = new Rectangle(left, top, System.Math.Max(1, right - left), System.Math.Max(1, bottom - top));
                }

                metadata.StartOffset = InfoTool.GetInt(rangeNode["start"], 0);
                metadata.AreaCount = InfoTool.GetInt(rangeNode["areaCount"], 0);
                metadata.AttackCount = InfoTool.GetInt(rangeNode["attackCount"], 0);
            }

            return metadata;
        }

        internal static WzImageProperty ResolveAttackHitNode(WzImageProperty infoProperty, WzImageProperty attackStateProperty = null)
        {
            return ResolveAttackHitNode(infoProperty, attackStateProperty, out _);
        }

        internal static WzImageProperty ResolveAttackHitNode(
            WzImageProperty infoProperty,
            WzImageProperty attackStateProperty,
            out int hitAnimationSourceFrameIndex)
        {
            foreach ((WzImageProperty hitNode, int sourceFrameIndex, _) in EnumerateAttackHitNodes(infoProperty, attackStateProperty))
            {
                if (!HasRenderableHitFrames(hitNode))
                {
                    continue;
                }

                hitAnimationSourceFrameIndex = sourceFrameIndex;
                return hitNode;
            }

            hitAnimationSourceFrameIndex = 0;
            return null;
        }

        internal static IReadOnlyList<(WzImageProperty HitNode, int SourceFrameIndex, bool IsAttackFrameOwned)> ResolveAttackHitNodes(
            WzImageProperty infoProperty,
            WzImageProperty attackStateProperty)
        {
            var hitNodes = new List<(WzImageProperty HitNode, int SourceFrameIndex, bool IsAttackFrameOwned)>();
            foreach ((WzImageProperty hitNode, int sourceFrameIndex, bool isAttackFrameOwned) in EnumerateAttackHitNodes(infoProperty, attackStateProperty))
            {
                if (!HasRenderableHitFrames(hitNode))
                {
                    continue;
                }

                hitNodes.Add((hitNode, sourceFrameIndex, isAttackFrameOwned));
            }

            return hitNodes;
        }

        internal static bool ShouldLoadAttackSupportAssetsForAction(string actionName)
        {
            return !string.IsNullOrWhiteSpace(actionName)
                   && (actionName.StartsWith("attack", StringComparison.Ordinal)
                       || actionName.StartsWith("skill", StringComparison.Ordinal));
        }

        private static int ReadOptionalInt(WzImageProperty parent, int defaultValue, params string[] propertyNames)
        {
            if (parent == null || propertyNames == null)
            {
                return defaultValue;
            }

            foreach (string propertyName in propertyNames)
            {
                if (string.IsNullOrWhiteSpace(propertyName))
                {
                    continue;
                }

                WzImageProperty property = WzInfoTools.GetRealProperty(parent[propertyName]);
                if (property != null)
                {
                    return InfoTool.GetInt(property, defaultValue);
                }
            }

            return defaultValue;
        }

        private static IEnumerable<(WzImageProperty HitNode, int SourceFrameIndex, bool IsAttackFrameOwned)> EnumerateAttackHitNodes(
            WzImageProperty infoProperty,
            WzImageProperty attackStateProperty)
        {
            WzSubProperty infoNode = WzInfoTools.GetRealProperty(infoProperty) as WzSubProperty;
            WzImageProperty infoHitNode = WzInfoTools.GetRealProperty(infoNode?["hit"]);
            if (infoHitNode != null)
            {
                yield return (infoHitNode, 0, false);
            }

            foreach ((WzImageProperty frameHitNode, int sourceFrameIndex) in EnumerateAttackFrameHitNodes(attackStateProperty))
            {
                yield return (frameHitNode, sourceFrameIndex, true);
            }
        }

        private static bool ShouldLoadMobActionFrames(string actionName)
        {
            return !string.IsNullOrWhiteSpace(actionName) &&
                   !string.Equals(actionName, "info", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(actionName, "angergaugeeffect", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(actionName, "angergaugeanimation", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool TryResolveMobClientActionSlot(string actionName, out int slot)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                slot = -1;
                return false;
            }

            string normalizedActionName = actionName.Trim();
            if (_mobClientActionSlotsByName.TryGetValue(normalizedActionName, out slot))
            {
                return true;
            }

            return _mobClientActionSlotAliasesByName.TryGetValue(normalizedActionName, out slot);
        }

        internal static string ResolveMobClientActionName(int slot)
        {
            return slot >= 0 && slot < _mobClientActionNamesBySlot.Length
                ? _mobClientActionNamesBySlot[slot]
                : null;
        }

        internal static IReadOnlyList<string> GetMobClientActionNamesBySlotForTests()
        {
            return _mobClientActionNamesBySlot;
        }

        internal static IReadOnlyCollection<int> GetMobClientUnresolvedActionSlotsForTests()
        {
            return _mobClientUnresolvedActionSlots;
        }

        private static string ResolveMobClientActionNameOrAuthored(string actionName)
        {
            if (!TryResolveMobClientActionSlot(actionName, out int slot))
            {
                return actionName?.ToLowerInvariant() ?? string.Empty;
            }

            return ResolveMobClientActionName(slot) ?? (actionName?.ToLowerInvariant() ?? string.Empty);
        }

        private static List<MobAnimationSet.FrameMetadata> BuildMobActionFrameMetadata(WzImageProperty source)
        {
            var metadata = new List<MobAnimationSet.FrameMetadata>();
            AppendMobActionFrameMetadata(source, metadata);
            return metadata;
        }

        private static IEnumerable<(int FrameIndex, bool Value)> ReadIndexedHitMetadataFlags(
            WzImageProperty infoHitNode,
            params string[] propertyNames)
        {
            foreach ((int frameIndex, WzImageProperty metadataNode) in EnumerateIndexedInfoHitMetadataNodes(infoHitNode))
            {
                int value = ReadOptionalInt(metadataNode, int.MinValue, propertyNames);
                if (value == int.MinValue)
                {
                    continue;
                }

                yield return (frameIndex, value > 0);
            }
        }

        private static IEnumerable<(int FrameIndex, bool Value)> ReadIndexedAttackFrameHitMetadataFlags(
            WzImageProperty attackStateProperty,
            params string[] propertyNames)
        {
            foreach ((int frameIndex, WzImageProperty metadataNode) in EnumerateIndexedAttackFrameHitMetadataNodes(attackStateProperty))
            {
                int value = ReadOptionalInt(metadataNode, int.MinValue, propertyNames);
                if (value == int.MinValue)
                {
                    continue;
                }

                yield return (frameIndex, value > 0);
            }
        }

        private static void AppendMobActionFrameMetadata(WzImageProperty source, List<MobAnimationSet.FrameMetadata> metadata)
        {
            if (source == null || metadata == null)
            {
                return;
            }

            source = WzInfoTools.GetRealProperty(source);

            if (source is WzCanvasProperty canvasProperty)
            {
                metadata.Add(BuildMobFrameMetadata(canvasProperty));
                return;
            }

            if (source is not WzSubProperty)
            {
                return;
            }

            foreach (WzImageProperty frameProperty in EnumerateMobActionFrameProperties(source))
            {
                if (TryResolveCanvasProperty(frameProperty, out WzCanvasProperty resolvedCanvas))
                {
                    metadata.Add(BuildMobFrameMetadata(resolvedCanvas));
                }
            }
        }

        internal static MobAnimationSet.FrameMetadata BuildMobFrameMetadataForTests(WzCanvasProperty canvasProperty)
        {
            return BuildMobFrameMetadata(canvasProperty);
        }

        private static MobAnimationSet.FrameMetadata BuildMobFrameMetadata(WzCanvasProperty canvasProperty)
        {
            System.Drawing.PointF originPoint = canvasProperty?.GetCanvasOriginPosition() ?? System.Drawing.PointF.Empty;
            Point origin = new Point((int)originPoint.X, (int)originPoint.Y);
            Point canvasSize = new Point(
                Math.Max(1, canvasProperty?.PngProperty?.Width ?? 1),
                Math.Max(1, canvasProperty?.PngProperty?.Height ?? 1));
            Rectangle fallbackBounds = new Rectangle(
                -origin.X,
                -origin.Y,
                canvasSize.X,
                canvasSize.Y);

            Rectangle frameBounds = TryBuildRect(canvasProperty?["lt"], canvasProperty?["rb"], out Rectangle authoredBounds)
                ? authoredBounds
                : fallbackBounds;

            Point? headAnchor = TryGetVector(canvasProperty?["head"]);
            List<Rectangle> clientMultiBodyBounds = TryGetBodyBounds(canvasProperty, "multiRect");
            List<Rectangle> clientBodyBounds = TryGetBodyBounds(canvasProperty, "rect");
            List<Rectangle> multiBodyBounds = CombineBodyBounds(clientMultiBodyBounds, clientBodyBounds);
            Rectangle bodyBounds = ResolvePrimaryBodyBounds(frameBounds, clientBodyBounds);
            Rectangle clientBodyBoundsUnion = ResolveClientBodyBounds(clientBodyBounds);
            int? alphaStart = TryGetOptionalInt(canvasProperty?["a0"]);
            int? alphaEnd = TryGetOptionalInt(canvasProperty?["a1"]);
            bool hasAlphaRange = alphaStart.HasValue || alphaEnd.HasValue;
            byte resolvedAlphaStart = (byte)Math.Clamp(alphaStart ?? byte.MaxValue, byte.MinValue, byte.MaxValue);
            byte resolvedAlphaEnd = (byte)Math.Clamp(alphaEnd ?? resolvedAlphaStart, byte.MinValue, byte.MaxValue);
            int? layerZ = TryGetOptionalInt(canvasProperty?["z"]);

            return new MobAnimationSet.FrameMetadata
            {
                FrameOrigin = origin,
                CanvasSize = canvasSize,
                VisualBounds = fallbackBounds,
                FrameBounds = frameBounds,
                BodyBounds = bodyBounds,
                HasHeadAnchor = headAnchor.HasValue,
                HeadAnchor = headAnchor ?? Point.Zero,
                MultiBodyBounds = multiBodyBounds,
                ClientMultiBodyBounds = clientMultiBodyBounds,
                ClientBodyBounds = clientBodyBoundsUnion,
                HasAlphaRange = hasAlphaRange,
                AlphaStart = resolvedAlphaStart,
                AlphaEnd = resolvedAlphaEnd,
                HasLayerZ = layerZ.HasValue,
                LayerZ = layerZ ?? -1
            };
        }

        private static List<MobAnimationSet.FrameMetadata> AlignFrameMetadataToFrames(
            int frameCount,
            List<MobAnimationSet.FrameMetadata> frameMetadata)
        {
            var aligned = new List<MobAnimationSet.FrameMetadata>(frameCount);
            if (frameCount <= 0)
            {
                return aligned;
            }

            if (frameMetadata == null || frameMetadata.Count == 0)
            {
                for (int i = 0; i < frameCount; i++)
                {
                    aligned.Add(CreateFallbackFrameMetadata());
                }

                return aligned;
            }

            for (int i = 0; i < frameCount; i++)
            {
                aligned.Add(i < frameMetadata.Count ? frameMetadata[i] : frameMetadata[^1]);
            }

            return aligned;
        }

        private static MobAnimationSet.FrameMetadata CreateFallbackFrameMetadata()
        {
            return new MobAnimationSet.FrameMetadata
            {
                FrameOrigin = Point.Zero,
                CanvasSize = new Point(1, 1),
                VisualBounds = new Rectangle(0, 0, 1, 1),
                FrameBounds = new Rectangle(0, 0, 1, 1),
                BodyBounds = new Rectangle(0, 0, 1, 1),
                HasHeadAnchor = false,
                HeadAnchor = Point.Zero,
                MultiBodyBounds = null,
                ClientMultiBodyBounds = null,
                ClientBodyBounds = Rectangle.Empty,
                HasAlphaRange = false,
                AlphaStart = byte.MaxValue,
                AlphaEnd = byte.MaxValue,
                HasLayerZ = false,
                LayerZ = -1
            };
        }

        private static bool TryResolveCanvasProperty(WzImageProperty frameProperty, out WzCanvasProperty canvasProperty)
        {
            canvasProperty = frameProperty as WzCanvasProperty;
            if (canvasProperty != null)
            {
                return true;
            }

            if (frameProperty is WzUOLProperty uolProperty && uolProperty.LinkValue is WzCanvasProperty linkedCanvas)
            {
                canvasProperty = linkedCanvas;
                return true;
            }

            return false;
        }

        private static Point? TryGetVector(WzImageProperty property)
        {
            if (property is WzVectorProperty vectorProperty)
            {
                return new Point(vectorProperty.X.Value, vectorProperty.Y.Value);
            }

            return null;
        }

        private static int? TryGetOptionalInt(WzImageProperty property)
        {
            property = WzInfoTools.GetRealProperty(property);
            if (property == null)
            {
                return null;
            }

            int value = InfoTool.GetInt(property, int.MinValue);
            return value != int.MinValue ? value : null;
        }

        private static bool TryBuildRect(WzImageProperty ltProperty, WzImageProperty rbProperty, out Rectangle rectangle)
        {
            Point? lt = TryGetVector(ltProperty);
            Point? rb = TryGetVector(rbProperty);
            if (!lt.HasValue || !rb.HasValue)
            {
                rectangle = Rectangle.Empty;
                return false;
            }

            int left = Math.Min(lt.Value.X, rb.Value.X);
            int top = Math.Min(lt.Value.Y, rb.Value.Y);
            int right = Math.Max(lt.Value.X, rb.Value.X);
            int bottom = Math.Max(lt.Value.Y, rb.Value.Y);
            rectangle = new Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
            return true;
        }

        private static List<Rectangle> TryGetBodyBounds(WzCanvasProperty canvasProperty, params string[] propertyNames)
        {
            if (canvasProperty == null || propertyNames == null)
            {
                return null;
            }

            var bounds = new List<Rectangle>();
            foreach (string propertyName in propertyNames)
            {
                if (string.IsNullOrWhiteSpace(propertyName))
                {
                    continue;
                }

                if (WzInfoTools.GetRealProperty(canvasProperty[propertyName]) is not WzSubProperty rectContainer)
                {
                    continue;
                }

                foreach (WzImageProperty childProperty in rectContainer.WzProperties)
                {
                    WzImageProperty resolvedProperty = WzInfoTools.GetRealProperty(childProperty);
                    if (resolvedProperty is not WzSubProperty rectProperty)
                    {
                        continue;
                    }

                    if (TryBuildRect(rectProperty["lt"], rectProperty["rb"], out Rectangle rect))
                    {
                        bounds.Add(rect);
                    }
                }
            }

            return bounds.Count > 0 ? bounds : null;
        }

        private static List<Rectangle> CombineBodyBounds(params List<Rectangle>[] bodyBounds)
        {
            if (bodyBounds == null || bodyBounds.Length == 0)
            {
                return null;
            }

            List<Rectangle> combined = null;
            foreach (List<Rectangle> source in bodyBounds)
            {
                if (source == null || source.Count == 0)
                {
                    continue;
                }

                combined ??= new List<Rectangle>();
                combined.AddRange(source);
            }

            return combined?.Count > 0 ? combined : null;
        }

        private static Rectangle ResolvePrimaryBodyBounds(Rectangle fallbackBounds, IReadOnlyList<Rectangle> bodyBounds)
        {
            if (bodyBounds == null || bodyBounds.Count == 0)
            {
                return fallbackBounds;
            }

            Rectangle resolved = bodyBounds[0];
            for (int i = 1; i < bodyBounds.Count; i++)
            {
                resolved = Rectangle.Union(resolved, bodyBounds[i]);
            }

            return resolved.IsEmpty ? fallbackBounds : resolved;
        }

        private static Rectangle ResolveClientBodyBounds(IReadOnlyList<Rectangle> bodyBounds)
        {
            if (bodyBounds == null || bodyBounds.Count == 0)
            {
                return Rectangle.Empty;
            }

            Rectangle resolved = bodyBounds[0];
            for (int i = 1; i < bodyBounds.Count; i++)
            {
                resolved = Rectangle.Union(resolved, bodyBounds[i]);
            }

            return resolved;
        }

        private static bool ShouldAppendReversePlayback(WzSubProperty mobStateProperty)
        {
            WzImageProperty infoProperty = WzInfoTools.GetRealProperty(mobStateProperty?["info"]);
            WzSubProperty infoNode = infoProperty as WzSubProperty;
            // CActionMan::LoadMobAction checks an action-level replay flag (StringPool id 0x049F),
            // which is authored as `zigzag` on many mob actions. Keep the older `reverse` seam too.
            int reverseFlag = ReadOptionalInt(mobStateProperty, 0, "reverse", "zigzag");
            reverseFlag = Math.Max(reverseFlag, ReadOptionalInt(infoNode, 0, "reverse", "zigzag"));
            reverseFlag = Math.Max(reverseFlag, ReadOptionalInt(infoNode?["range"], 0, "reverse", "zigzag"));
            return reverseFlag != 0;
        }

        internal static bool ShouldAppendReversePlaybackForTests(WzSubProperty mobStateProperty)
        {
            return ShouldAppendReversePlayback(mobStateProperty);
        }

        internal static void AppendReversePlaybackForTests<T>(List<T> items)
        {
            AppendReversePlayback(items, null);
        }

        private static List<WzCanvasProperty> BuildMobActionFrameCanvases(WzImageProperty source)
        {
            var frameCanvases = new List<WzCanvasProperty>();
            AppendMobActionFrameCanvases(source, frameCanvases);
            return frameCanvases;
        }

        private static List<CachedMobFrameOverlay> BuildMobActionFrameOverlays(WzSubProperty actionProperty)
        {
            // CActionMan::LoadMobAction queries each action child as IWzCanvas while building
            // MOBACTIONFRAMEENTRY rows. Rare nonnumeric branches such as stand/face/face are
            // separate authored data, not frame entries owned by this cache seam.
            return new List<CachedMobFrameOverlay>();
        }

        internal static int CountMobActionFrameOverlaysForTests(WzSubProperty actionProperty)
        {
            return BuildMobActionFrameOverlays(actionProperty).Count;
        }

        internal static string ResolveMobOverlayLayerZForTests(WzCanvasProperty canvasProperty)
        {
            return ResolveMobOverlayLayerZ(canvasProperty);
        }

        internal static MobAnimationSet.ActionSpeakMetadata BuildMobActionSpeakMetadataForTests(WzImageProperty speakProperty)
        {
            return BuildMobActionSpeakMetadata(speakProperty);
        }

        private static MobAnimationSet.ActionSpeakMetadata BuildMobActionSpeakMetadata(WzImageProperty speakProperty)
        {
            if (WzInfoTools.GetRealProperty(speakProperty) is not WzSubProperty speakNode)
            {
                return null;
            }

            List<string> messages = ReadMobSpeakMessages(speakNode);
            if (messages.Count == 0)
            {
                return null;
            }

            return new MobAnimationSet.ActionSpeakMetadata
            {
                Probability = Math.Clamp(ReadOptionalInt(speakNode, 100, "prob"), 0, 100),
                ChatBalloon = Math.Max(0, ReadOptionalInt(speakNode, 0, "chataBalloon", "chatBalloon")),
                FloatNotice = Math.Max(0, ReadOptionalInt(speakNode, 0, "floatNotice")),
                HpThreshold = Math.Max(0, ReadOptionalInt(speakNode, 0, "hp")),
                Messages = messages
            };
        }

        private static List<string> ReadMobSpeakMessages(WzSubProperty speakNode)
        {
            var messages = new List<string>();
            if (speakNode?.WzProperties == null)
            {
                return messages;
            }

            foreach (WzImageProperty childProperty in speakNode.WzProperties
                         .Where(property => int.TryParse(property?.Name, out _))
                         .OrderBy(property => int.Parse(property.Name)))
            {
                WzImageProperty resolvedProperty = WzInfoTools.GetRealProperty(childProperty);
                if (resolvedProperty is WzStringProperty stringProperty &&
                    !string.IsNullOrWhiteSpace(stringProperty.Value))
                {
                    messages.Add(stringProperty.Value);
                }
            }

            return messages;
        }

        private static string ResolveMobOverlayLayerZ(WzCanvasProperty canvasProperty)
        {
            WzImageProperty zProperty = WzInfoTools.GetRealProperty(canvasProperty?["z"]);
            return zProperty is WzStringProperty stringProperty &&
                   !string.IsNullOrWhiteSpace(stringProperty.Value)
                ? stringProperty.Value.Trim()
                : null;
        }

        private static void AppendMobActionFrameCanvases(WzImageProperty source, List<WzCanvasProperty> frameCanvases)
        {
            if (source == null || frameCanvases == null)
            {
                return;
            }

            source = WzInfoTools.GetRealProperty(source);

            if (TryResolveCanvasProperty(source, out WzCanvasProperty canvasProperty))
            {
                frameCanvases.Add(canvasProperty);
                return;
            }

            if (source is not WzSubProperty)
            {
                return;
            }

            foreach (WzImageProperty frameProperty in EnumerateMobActionFrameProperties(source))
            {
                if (TryResolveCanvasProperty(frameProperty, out WzCanvasProperty resolvedCanvas))
                {
                    frameCanvases.Add(resolvedCanvas);
                }
            }
        }

        private static IEnumerable<WzImageProperty> EnumerateMobActionFrameProperties(WzImageProperty source)
        {
            if (source is not WzSubProperty sourceProperty || sourceProperty.WzProperties == null)
            {
                yield break;
            }

            foreach (WzImageProperty childProperty in sourceProperty.WzProperties
                         .Where(property => int.TryParse(property?.Name, out _))
                         .OrderBy(property => int.Parse(property.Name)))
            {
                WzImageProperty resolvedProperty = WzInfoTools.GetRealProperty(childProperty);
                if (resolvedProperty != null)
                {
                    yield return resolvedProperty;
                }
            }
        }

        internal static int CountMobActionFrameCanvasesForTests(WzImageProperty actionProperty)
        {
            return BuildMobActionFrameCanvases(actionProperty).Count;
        }

        internal static int CountMobActionFrameMetadataForTests(WzImageProperty actionProperty)
        {
            return BuildMobActionFrameMetadata(actionProperty).Count;
        }

        private const int MobClientActionFrameFallbackDelayMs = 120;

        private static List<int> BuildMobActionFrameDelays(
            IReadOnlyList<WzCanvasProperty> frameCanvases,
            int fallbackDelay = MobClientActionFrameFallbackDelayMs)
        {
            var frameDelays = new List<int>(frameCanvases?.Count ?? 0);
            if (frameCanvases == null)
            {
                return frameDelays;
            }

            for (int i = 0; i < frameCanvases.Count; i++)
            {
                frameDelays.Add((int)InfoTool.GetOptionalInt(frameCanvases[i]?["delay"], fallbackDelay));
            }

            return frameDelays;
        }

        internal static IReadOnlyList<int> BuildMobActionFrameDelaysForTests(
            IReadOnlyList<WzCanvasProperty> frameCanvases)
        {
            return BuildMobActionFrameDelays(frameCanvases);
        }

        private static List<IDXObject> InstantiateMobActionFrames(
            TexturePool texturePool,
            IReadOnlyList<WzCanvasProperty> frameCanvases,
            IReadOnlyList<int> frameDelays,
            int x,
            int y,
            GraphicsDevice device)
        {
            var frames = new List<IDXObject>(frameCanvases?.Count ?? 0);
            if (frameCanvases == null)
            {
                return frames;
            }

            for (int i = 0; i < frameCanvases.Count; i++)
            {
                WzCanvasProperty canvasProperty = frameCanvases[i];
                if (canvasProperty == null)
                {
                    continue;
                }

                EnsureMobCanvasTextureLoaded(texturePool, canvasProperty, device);
                System.Drawing.PointF origin = canvasProperty.GetCanvasOriginPosition();
                Texture2D texture = canvasProperty.MSTag as Texture2D;
                if (texture == null)
                {
                    continue;
                }

                int delay = i < (frameDelays?.Count ?? 0) ? frameDelays[i] : 100;
                frames.Add(new DXObject(
                    x - (int)origin.X,
                    y - (int)origin.Y,
                    texture,
                    delay));
            }

            return frames;
        }

        private static void EnsureMobCanvasTextureLoaded(TexturePool texturePool, WzCanvasProperty canvasProperty, GraphicsDevice device)
        {
            if (canvasProperty?.MSTag != null)
            {
                return;
            }

            string canvasBitmapPath = canvasProperty.FullPath;
            Texture2D textureFromCache = texturePool.GetTexture(canvasBitmapPath);
            if (textureFromCache != null)
            {
                canvasProperty.MSTag = textureFromCache;
                return;
            }

            using var bitmap = canvasProperty.GetLinkedWzCanvasBitmap();
            if (bitmap != null)
            {
                canvasProperty.MSTag = bitmap.ToTexture2D(device);
            }

            if (canvasProperty.MSTag is Texture2D texture)
            {
                texturePool.AddTextureToPool(canvasBitmapPath, texture);
            }
        }

        private static void AppendReversePlayback(
            List<WzCanvasProperty> frames,
            List<int> frameDelays,
            List<MobAnimationSet.FrameMetadata> frameMetadata)
        {
            if (frames == null || frames.Count <= 2)
            {
                return;
            }

            int originalCount = frames.Count;
            for (int i = originalCount - 2; i >= 1; i--)
            {
                frames.Add(frames[i]);
                if (frameDelays != null && i < frameDelays.Count)
                {
                    frameDelays.Add(frameDelays[i]);
                }

                if (frameMetadata != null && i < frameMetadata.Count)
                {
                    frameMetadata.Add(frameMetadata[i]);
                }
            }
        }

        private static void AppendReversePlayback<T>(List<T> items, List<MobAnimationSet.FrameMetadata> _)
        {
            if (items == null || items.Count <= 2)
            {
                return;
            }

            int originalCount = items.Count;
            for (int i = originalCount - 2; i >= 1; i--)
            {
                items.Add(items[i]);
            }
        }

        private static IEnumerable<(WzImageProperty HitNode, int SourceFrameIndex)> EnumerateAttackFrameHitNodes(WzImageProperty attackStateProperty)
        {
            WzSubProperty attackStateNode = WzInfoTools.GetRealProperty(attackStateProperty) as WzSubProperty;
            if (attackStateNode == null)
            {
                yield break;
            }

            foreach (WzImageProperty frameProperty in attackStateNode.WzProperties
                         .Where(property => int.TryParse(property?.Name, out _))
                         .OrderBy(property => int.Parse(property.Name)))
            {
                WzImageProperty frameHitNode = WzInfoTools.GetRealProperty(frameProperty["hit"]);
                if (frameHitNode != null)
                {
                    yield return (frameHitNode, int.Parse(frameProperty.Name));
                }
            }
        }

        private static IEnumerable<(int FrameIndex, WzImageProperty MetadataNode)> EnumerateIndexedInfoHitMetadataNodes(WzImageProperty infoHitNode)
        {
            WzImageProperty resolvedInfoHitNode = WzInfoTools.GetRealProperty(infoHitNode);
            if (resolvedInfoHitNode == null)
            {
                yield break;
            }

            foreach (WzImageProperty frameProperty in resolvedInfoHitNode.WzProperties
                         .Where(property => int.TryParse(property?.Name, out _))
                         .OrderBy(property => int.Parse(property.Name)))
            {
                if (!int.TryParse(frameProperty.Name, out int frameIndex))
                {
                    continue;
                }

                WzImageProperty resolvedFrameProperty = WzInfoTools.GetRealProperty(frameProperty);
                if (resolvedFrameProperty == null)
                {
                    continue;
                }

                WzImageProperty nestedHitNode = WzInfoTools.GetRealProperty(resolvedFrameProperty["hit"]);
                yield return (frameIndex, nestedHitNode ?? resolvedFrameProperty);
            }
        }

        private static IEnumerable<(int FrameIndex, WzImageProperty MetadataNode)> EnumerateIndexedAttackFrameHitMetadataNodes(WzImageProperty attackStateProperty)
        {
            WzSubProperty attackStateNode = WzInfoTools.GetRealProperty(attackStateProperty) as WzSubProperty;
            if (attackStateNode == null)
            {
                yield break;
            }

            foreach (WzImageProperty frameProperty in attackStateNode.WzProperties
                         .Where(property => int.TryParse(property?.Name, out _))
                         .OrderBy(property => int.Parse(property.Name)))
            {
                if (!int.TryParse(frameProperty.Name, out int frameIndex))
                {
                    continue;
                }

                WzImageProperty resolvedFrameProperty = WzInfoTools.GetRealProperty(frameProperty);
                WzImageProperty frameHitNode = WzInfoTools.GetRealProperty(resolvedFrameProperty?["hit"]);
                if (frameHitNode != null)
                {
                    yield return (frameIndex, frameHitNode);
                }
            }
        }

        private static bool HasRenderableHitFrames(WzImageProperty hitNode)
        {
            if (hitNode == null)
            {
                return false;
            }

            return hitNode.WzProperties?.Any(property => int.TryParse(property?.Name, out _)) == true;
        }

        private static bool TryBuildAttackEffectNode(
            TexturePool texturePool,
            WzImageProperty infoChild,
            GraphicsDevice device,
            ConcurrentBag<WzObject> usedProps,
            out MobAnimationSet.AttackEffectNode effectNode)
        {
            effectNode = null;
            if (infoChild == null || !infoChild.Name.StartsWith("effect", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string suffix = infoChild.Name.Substring("effect".Length);
            if (suffix.Length > 0 && !int.TryParse(suffix, out _))
            {
                return false;
            }

            WzSubProperty effectProperty = WzInfoTools.GetRealProperty(infoChild) as WzSubProperty;
            if (effectProperty == null || !HasStructuredAttackEffectMetadata(effectProperty))
            {
                return false;
            }

            effectNode = new MobAnimationSet.AttackEffectNode
            {
                Name = infoChild.Name,
                EffectType = InfoTool.GetInt(effectProperty["effectType"], 0),
                EffectDistance = InfoTool.GetInt(effectProperty["effectDistance"], 0),
                RandomPos = InfoTool.GetInt(effectProperty["randomPos"], 0) > 0,
                Delay = InfoTool.GetInt(effectProperty["delay"], 0),
                Start = InfoTool.GetInt(effectProperty["start"], 0),
                Interval = InfoTool.GetInt(effectProperty["interval"], 0),
                Count = InfoTool.GetInt(effectProperty["count"], 0),
                Duration = InfoTool.GetInt(effectProperty["duration"], 0),
                Fall = InfoTool.GetInt(effectProperty["fall"], 0),
                OffsetX = InfoTool.GetInt(effectProperty["x"], 0),
                OffsetY = InfoTool.GetInt(effectProperty["y"], 0)
            };

            WzVectorProperty lt = effectProperty["lt"] as WzVectorProperty;
            WzVectorProperty rb = effectProperty["rb"] as WzVectorProperty;
            if (lt != null && rb != null)
            {
                int left = Math.Min(lt.X.Value, rb.X.Value);
                int right = Math.Max(lt.X.Value, rb.X.Value);
                int top = Math.Min(lt.Y.Value, rb.Y.Value);
                int bottom = Math.Max(lt.Y.Value, rb.Y.Value);
                effectNode.HasRangeBounds = true;
                effectNode.RangeBounds = new Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
            }

            foreach (WzImageProperty sequenceProperty in GetAttackEffectSequenceProperties(effectProperty))
            {
                TryAddAttackEffectSequenceFrames(effectNode.Sequences, texturePool, sequenceProperty, device, usedProps);
            }

            if (effectNode.Sequences.Count == 0)
            {
                TryAddAttackEffectSequenceFrames(effectNode.Sequences, texturePool, effectProperty, device, usedProps);
            }

            return effectNode.Sequences.Count > 0;
        }

        private static bool TryBuildPlainAttackEffectNode(
            TexturePool texturePool,
            WzImageProperty infoChild,
            GraphicsDevice device,
            ConcurrentBag<WzObject> usedProps,
            out MobAnimationSet.AttackEffectNode effectNode)
        {
            effectNode = null;
            if (infoChild == null || !TryGetAttackEffectNodeOrder(infoChild.Name, out int groupIndex))
            {
                return false;
            }

            WzImageProperty effectProperty = WzInfoTools.GetRealProperty(infoChild) ?? infoChild;
            List<IDXObject> frames = MapSimulatorLoader.LoadFrames(texturePool, effectProperty, 0, 0, device, usedProps);
            if (frames.Count == 0)
            {
                return false;
            }

            effectNode = new MobAnimationSet.AttackEffectNode
            {
                Name = infoChild.Name,
                UseRangeGroupPlacement = true,
                RangeGroupIndex = groupIndex
            };
            effectNode.Sequences.Add(frames);
            return true;
        }

        private static bool HasStructuredAttackEffectMetadata(WzSubProperty effectProperty)
        {
            return effectProperty != null &&
                   (effectProperty["effectType"] != null ||
                    effectProperty["effectDistance"] != null ||
                    effectProperty["randomPos"] != null ||
                    effectProperty["lt"] != null ||
                    effectProperty["rb"] != null ||
                    effectProperty["start"] != null ||
                    effectProperty["interval"] != null ||
                    effectProperty["count"] != null ||
                    effectProperty["duration"] != null ||
                    effectProperty["fall"] != null ||
                    effectProperty["x"] != null ||
                    effectProperty["y"] != null);
        }

        internal static IReadOnlyList<WzImageProperty> GetAttackEffectSequenceProperties(WzSubProperty effectProperty)
        {
            var sequenceProperties = new List<WzImageProperty>();
            if (effectProperty?.WzProperties == null)
            {
                return sequenceProperties;
            }

            foreach (WzImageProperty child in effectProperty.WzProperties)
            {
                WzImageProperty resolvedChild = WzInfoTools.GetRealProperty(child) ?? child;
                if (resolvedChild == null || !int.TryParse(resolvedChild.Name, out _))
                {
                    continue;
                }

                sequenceProperties.Add(resolvedChild);
            }

            sequenceProperties.Sort((left, right) =>
            {
                int leftIndex = int.TryParse(left?.Name, out int parsedLeftIndex) ? parsedLeftIndex : int.MaxValue;
                int rightIndex = int.TryParse(right?.Name, out int parsedRightIndex) ? parsedRightIndex : int.MaxValue;
                return leftIndex.CompareTo(rightIndex);
            });
            return sequenceProperties;
        }

        private static void TryAddAttackEffectSequenceFrames(
            List<List<IDXObject>> sequences,
            TexturePool texturePool,
            WzImageProperty sequenceProperty,
            GraphicsDevice device,
            ConcurrentBag<WzObject> usedProps)
        {
            if (sequences == null || sequenceProperty == null)
            {
                return;
            }

            WzImageProperty resolvedSequenceProperty = WzInfoTools.GetRealProperty(sequenceProperty) ?? sequenceProperty;
            List<IDXObject> sequenceFrames = MapSimulatorLoader.LoadFrames(texturePool, resolvedSequenceProperty, 0, 0, device, usedProps);
            if (sequenceFrames.Count > 0)
            {
                sequences.Add(sequenceFrames);
            }
        }

        internal static Dictionary<string, int> BuildPlainAttackEffectGrouping(IEnumerable<string> effectPropertyNames)
        {
            var orderedNames = effectPropertyNames?
                .Where(name => TryGetAttackEffectNodeOrder(name, out int order) && order >= 0)
                .ToList() ?? new List<string>();
            var grouping = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (orderedNames.Count == 0)
            {
                return grouping;
            }

            bool hasPrimaryEffect = orderedNames.Any(IsPrimaryAttackEffectNodeName);
            bool hasNumberedEffects = orderedNames.Any(name => !IsPrimaryAttackEffectNodeName(name));
            if (!hasNumberedEffects)
            {
                return grouping;
            }

            int groupIndex = 0;
            for (int i = 0; i < orderedNames.Count; i++)
            {
                string name = orderedNames[i];
                if (hasPrimaryEffect && IsPrimaryAttackEffectNodeName(name))
                {
                    continue;
                }

                grouping[name] = groupIndex++;
            }

            return grouping;
        }

        private static bool IsPrimaryAttackEffectNodeName(string name)
        {
            return string.Equals(name, "effect", StringComparison.OrdinalIgnoreCase);
        }

        private static List<WzImageProperty> GetAttackEffectProperties(WzSubProperty infoNode)
        {
            var effects = new List<WzImageProperty>();
            if (infoNode?.WzProperties == null)
            {
                return effects;
            }

            foreach (WzImageProperty child in infoNode.WzProperties)
            {
                if (child == null || !child.Name.StartsWith("effect", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                effects.Add(child);
            }

            effects.Sort((left, right) => GetAttackEffectNodeSortKey(left?.Name).CompareTo(GetAttackEffectNodeSortKey(right?.Name)));
            return effects;
        }

        private static int GetAttackEffectNodeSortKey(string name)
        {
            return TryGetAttackEffectNodeOrder(name, out int order) ? order : int.MaxValue;
        }

        private static bool TryGetAttackEffectNodeOrder(string name, out int order)
        {
            order = -1;
            if (string.IsNullOrEmpty(name) || !name.StartsWith("effect", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string suffix = name.Substring("effect".Length);
            if (suffix.Length == 0)
            {
                order = 0;
                return true;
            }

            if (!int.TryParse(suffix, out int suffixIndex))
            {
                return false;
            }

            order = suffixIndex + 1;
            return true;
        }
        #endregion

        #region NPC
        /// <summary>
        /// NPC
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="npcInstance"></param>
        /// <param name="UserScreenScaleFactor"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <returns></returns>
        public static NpcItem CreateNpcFromProperty(
            TexturePool texturePool, NpcInstance npcInstance, float UserScreenScaleFactor,
            GraphicsDevice device, ConcurrentBag<WzObject> usedProps, bool includeTooltips = true,
            CharacterGender? localPlayerGender = null,
            bool hasQuestCheckContext = false,
            Func<int, QuestStateType> questStateProvider = null,
            Func<int, string> questRecordValueProvider = null,
            int requestedClientActionSetIndex = NpcClientActionSetLoader.AutomaticClientActionSetIndex)
        {
            NpcInfo npcInfo = (NpcInfo)npcInstance.BaseInfo;
            WzImage source = NpcImgEntryResolver.Resolve(npcInfo);

            // Match the client-owned NPC action seam: resolve a single action set, then materialize only that set's actions.
            NpcAnimationSet animationSet = NpcClientActionSetLoader.LoadAnimationSet(
                texturePool,
                npcInstance,
                device,
                usedProps,
                localPlayerGender,
                hasQuestCheckContext,
                questStateProvider,
                questRecordValueProvider,
                requestedClientActionSetIndex);
            if (animationSet.IsHiddenToLocalUser || animationSet.ActionCount == 0)
            {
                // Fallback for maps where automatic NPC action-set selection resolves
                // to a hidden/empty conditional branch.
                NpcAnimationSet rootAnimationSet = NpcClientActionSetLoader.LoadAnimationSet(
                    texturePool,
                    npcInstance,
                    device,
                    usedProps,
                    localPlayerGender,
                    hasQuestCheckContext,
                    questStateProvider,
                    questRecordValueProvider,
                    NpcClientActionSetLoader.RootClientActionSetIndex);

                if (!rootAnimationSet.IsHiddenToLocalUser && rootAnimationSet.ActionCount > 0)
                {
                    animationSet = rootAnimationSet;
                }
                else
                {
                    WzCanvasProperty fallbackCanvas = WzInfoTools.GetNpcImage(source);
                    List<IDXObject> fallbackFrames = fallbackCanvas != null
                        ? MapSimulatorLoader.LoadFrames(
                            texturePool,
                            fallbackCanvas,
                            npcInstance.X,
                            npcInstance.Y,
                            device,
                            usedProps,
                            fallbackDelay: NpcClientActionSetLoader.DefaultNpcFrameDelay)
                        : new List<IDXObject>();

                    if (fallbackFrames.Count > 0)
                    {
                        var fallbackAnimationSet = new NpcAnimationSet
                        {
                            ClientActionSetIndex = NpcClientActionSetLoader.RootClientActionSetIndex,
                            IsHiddenToLocalUser = false
                        };
                        fallbackAnimationSet.AddAnimation(AnimationKeys.Stand, fallbackFrames);
                        animationSet = fallbackAnimationSet;
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            NameTooltipItem nameTooltip = null;
            NameTooltipItem npcDescTooltip = null;
            if (includeTooltips)
            {
                System.Drawing.Color color_foreGround = System.Drawing.Color.FromArgb(255, 255, 255, 0); // gold npc foreground color

                nameTooltip = MapSimulatorLoader.CreateNPCMobNameTooltip(
                    npcInstance.NpcInfo.StringName, npcInstance.X, npcInstance.Y, color_foreGround,
                    texturePool, UserScreenScaleFactor, device);

                const int NPC_FUNC_Y_POS = 17;

                npcDescTooltip = MapSimulatorLoader.CreateNPCMobNameTooltip(
                    npcInstance.NpcInfo.StringFunc, npcInstance.X, npcInstance.Y + NPC_FUNC_Y_POS, color_foreGround,
                    texturePool, UserScreenScaleFactor, device);
            }

            return new NpcItem(
                npcInstance,
                animationSet,
                nameTooltip,
                npcDescTooltip,
                LoadNpcIdleSpeech(source?["info"]?["speak"]));
        }

        private static IReadOnlyList<string> LoadNpcIdleSpeech(WzImageProperty speakProperty)
        {
            if (speakProperty == null)
            {
                return Array.Empty<string>();
            }

            var lines = new List<string>();
            AppendNpcIdleSpeech(lines, speakProperty);
            return lines;
        }

        private static void AppendNpcIdleSpeech(ICollection<string> lines, WzImageProperty property)
        {
            if (property == null)
            {
                return;
            }

            if (property is WzStringProperty stringProp)
            {
                if (!string.IsNullOrWhiteSpace(stringProp.Value))
                {
                    lines.Add(stringProp.Value.Trim());
                }

                return;
            }

            if (property.WzProperties == null)
            {
                return;
            }

            for (int i = 0; i < property.WzProperties.Count; i++)
            {
                AppendNpcIdleSpeech(lines, property.WzProperties[i]);
            }
        }
        #endregion
    }
}
