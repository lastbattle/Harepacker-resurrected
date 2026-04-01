using System;
using System.Linq;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Managers;
using HaSharedLibrary;
using HaSharedLibrary.Wz;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
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
            public readonly Dictionary<string, List<IDXObject>> AttackHitEffects = new(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, List<IDXObject>> AttackProjectileEffects = new(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, List<IDXObject>> AttackEffects = new(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, List<IDXObject>> AttackWarningEffects = new(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, List<MobAnimationSet.AttackEffectNode>> AttackExtraEffects = new(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<int, List<IDXObject>> AngerGaugeAnimations = new();
            public List<IDXObject> AngerGaugeEffect;
        }

        private static readonly ConditionalWeakTable<GraphicsDevice, ConcurrentDictionary<string, Lazy<CachedMobAttackAssets>>> _cachedMobAttackAssetsByDevice = new();
        private sealed class CachedDoomMobAssets
        {
            public MobAnimationSet AnimationSet { get; init; }
            public Rectangle Footprint { get; init; }
        }

        private static readonly ConditionalWeakTable<GraphicsDevice, Lazy<CachedDoomMobAssets>> _cachedDoomMobAssetsByDevice = new();

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
            WzImage source = mobInfo.LinkedWzImage;

            // Create animation set to store frames per action
            MobAnimationSet animationSet = new MobAnimationSet();
            CachedDoomMobAssets doomAssets = GetOrBuildDoomMobAssets(texturePool, device, usedProps);
            CachedMobAttackAssets cachedAttackAssets = GetOrBuildCachedMobAttackAssets(texturePool, mobInfo, source, device, usedProps);
            ApplyCachedMobAttackAssets(animationSet, cachedAttackAssets);

            foreach (WzImageProperty childProperty in source.WzProperties)
            {
                if (childProperty is WzSubProperty mobStateProperty) // issue with 867119250, Eluna map mobs
                {
                    string actionName = mobStateProperty.Name.ToLower();

                    switch (actionName)
                    {
                        case "info": // info/speak/0 WzStringProperty - skip info node
                            break;

                        case "angergaugeeffect":
                            break;

                        case "angergaugeanimation":
                            break;

                        case "stand":
                        case "move":
                        case "walk":
                        case "fly":
                        case "jump":
                        case "hit1":
                        case "die1":
                        case "die2":
                        case "attack1":
                        case "attack2":
                        case "attack3":
                        case "skill1":
                        case "skill2":
                        case "skill3":
                        case "chase":
                        case "regen":
                            {
                                // Load frames for this specific action
                                List<IDXObject> actionFrames = MapSimulatorLoader.LoadFrames(texturePool, mobStateProperty, mobInstance.X, mobInstance.Y, device, usedProps);
                                if (actionFrames.Count > 0)
                                {
                                    animationSet.AddAnimation(actionName, actionFrames);
                                }

                                // Load hit effect frames for attack actions (attack1/info/hit, attack2/info/hit, etc.)
                                if (actionName.StartsWith("attack"))
                                {
                                    // Attack support assets are cached per mob ID and applied above.
                                }
                                break;
                            }

                        default:
                            {
                                // For unknown actions, still load them in case they're needed
                                List<IDXObject> actionFrames = MapSimulatorLoader.LoadFrames(texturePool, mobStateProperty, mobInstance.X, mobInstance.Y, device, usedProps);
                                if (actionFrames.Count > 0)
                                {
                                    animationSet.AddAnimation(actionName, actionFrames);
                                }
                                break;
                            }
                    }
                }
            }

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

        private static CachedMobAttackAssets GetOrBuildCachedMobAttackAssets(
            TexturePool texturePool,
            MobInfo mobInfo,
            WzImage source,
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
                    actionName != "hit1" &&
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

        private static CachedMobAttackAssets BuildCachedMobAttackAssets(
            TexturePool texturePool,
            MobInfo mobInfo,
            WzImage source,
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

                string actionName = mobStateProperty.Name.ToLowerInvariant();

                if (actionName == "angergaugeeffect")
                {
                    List<IDXObject> effectFrames = MapSimulatorLoader.LoadFrames(texturePool, mobStateProperty, 0, 0, device, usedProps);
                    if (effectFrames.Count > 0)
                    {
                        cached.AngerGaugeEffect = effectFrames;
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

                if (!actionName.StartsWith("attack", StringComparison.Ordinal))
                {
                    continue;
                }

                WzImageProperty infoProperty = WzInfoTools.GetRealProperty(mobStateProperty["info"]);
                WzSubProperty infoNode = infoProperty as WzSubProperty;
                MobAnimationSet.AttackInfoMetadata attackInfo = BuildAttackInfoMetadata(infoProperty);
                if (attackInfo != null)
                {
                    cached.AttackMetadata[actionName] = attackInfo;
                }

                WzImageProperty hitNode = ResolveAttackHitNode(infoProperty);
                if (hitNode != null)
                {
                    List<IDXObject> hitFrames = MapSimulatorLoader.LoadFrames(texturePool, hitNode, 0, 0, device, usedProps);
                    if (hitFrames.Count > 0)
                    {
                        cached.AttackHitEffects[actionName] = hitFrames;
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
                animationSet.SetAngerGaugeEffect(cachedAssets.AngerGaugeEffect);
            }

            foreach (KeyValuePair<string, MobAnimationSet.AttackInfoMetadata> entry in cachedAssets.AttackMetadata)
            {
                animationSet.SetAttackInfoMetadata(entry.Key, entry.Value);
            }

            foreach (KeyValuePair<string, List<IDXObject>> entry in cachedAssets.AttackHitEffects)
            {
                animationSet.AddAttackHitEffect(entry.Key, entry.Value);
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
                System.Diagnostics.Debug.WriteLine($"[LifeLoader] LoadMobSounds: Mob '{mobId}' not found in Mob.img. Available: {string.Join(", ", mobSoundImage.WzProperties?.Take(10).Select(p => p.Name) ?? new string[0])}...");
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

        internal static MobAnimationSet.AttackInfoMetadata BuildAttackInfoMetadata(WzImageProperty infoProperty)
        {
            WzSubProperty infoNode = WzInfoTools.GetRealProperty(infoProperty) as WzSubProperty;
            if (infoNode == null)
            {
                return null;
            }

            WzSubProperty hitNode = ResolveAttackHitNode(infoNode) as WzSubProperty;
            int nestedHitAttach = ReadOptionalInt(hitNode, int.MinValue, "attach", "bHitAttach", "hitAttach");
            int nestedFacingAttach = ReadOptionalInt(
                hitNode,
                int.MinValue,
                "attachfacing",
                "bFacingAttach",
                "bFacingAttatch",
                "facingAttach");
            int infoHitAttach = ReadOptionalInt(infoNode, int.MinValue, "attach", "bHitAttach", "hitAttach");
            int infoFacingAttach = ReadOptionalInt(
                infoNode,
                int.MinValue,
                "attachfacing",
                "bFacingAttach",
                "bFacingAttatch",
                "facingAttach");
            bool facingAttach = nestedFacingAttach != int.MinValue
                ? nestedFacingAttach > 0
                : infoFacingAttach > 0;
            bool hitAttach = nestedHitAttach != int.MinValue
                ? nestedHitAttach > 0
                : infoHitAttach != int.MinValue
                    ? infoHitAttach > 0
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
                HitAttach = hitAttach,
                FacingAttach = facingAttach,
                EffectFacingAttach = effectFacingAttach,
                EffectAfter = InfoTool.GetInt(infoNode["effectAfter"], 0),
                AttackAfter = InfoTool.GetInt(infoNode["attackAfter"], 0),
                RandDelayAttack = InfoTool.GetInt(infoNode["randDelayAttack"], 0),
                HasPrimaryEffect = infoNode["effect"] != null,
                HasAreaWarning = infoNode["areaWarning"] != null,
                IsRushAttack = InfoTool.GetInt(infoNode["rush"], 0) > 0,
                IsJumpAttack = InfoTool.GetInt(infoNode["jumpAttack"], 0) > 0,
                Tremble = InfoTool.GetInt(infoNode["tremble"], 0) > 0,
                IsAngerAttack = InfoTool.GetInt(infoNode["AngerAttack"], 0) > 0
            };

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

        internal static WzImageProperty ResolveAttackHitNode(WzImageProperty infoProperty)
        {
            WzSubProperty infoNode = WzInfoTools.GetRealProperty(infoProperty) as WzSubProperty;
            return WzInfoTools.GetRealProperty(infoNode?["hit"]);
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

            WzSubProperty effectProperty = infoChild as WzSubProperty;
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

            for (int sequenceIndex = 0; ; sequenceIndex++)
            {
                WzImageProperty sequenceProperty = effectProperty[sequenceIndex.ToString()];
                if (sequenceProperty == null)
                {
                    break;
                }

                List<IDXObject> sequenceFrames = MapSimulatorLoader.LoadFrames(texturePool, sequenceProperty, 0, 0, device, usedProps);
                if (sequenceFrames.Count > 0)
                {
                    effectNode.Sequences.Add(sequenceFrames);
                }
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

            List<IDXObject> frames = MapSimulatorLoader.LoadFrames(texturePool, infoChild, 0, 0, device, usedProps);
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
            GraphicsDevice device, ConcurrentBag<WzObject> usedProps, bool includeTooltips = true)
        {
            NpcInfo npcInfo = (NpcInfo)npcInstance.BaseInfo;
            WzImage source = npcInfo.LinkedWzImage;

            // Create animation set to store frames by action (stand, speak, blink, etc.)
            NpcAnimationSet animationSet = new NpcAnimationSet();

            foreach (WzImageProperty childProperty in source.WzProperties)
            {
                WzSubProperty npcStateProperty = (WzSubProperty)childProperty;
                switch (npcStateProperty.Name)
                {
                    case "info": // info/speak/0 WzStringProperty
                        {
                            break;
                        }
                    default:
                        {
                            // Load frames for this action and store by action name
                            List<IDXObject> actionFrames = MapSimulatorLoader.LoadFrames(texturePool, npcStateProperty, npcInstance.X, npcInstance.Y, device, usedProps);
                            if (actionFrames.Count > 0)
                            {
                                animationSet.AddAnimation(npcStateProperty.Name, actionFrames);
                            }
                            break;
                        }
                }
            }
            if (animationSet.ActionCount == 0) // fix japan ms v186, (9000021.img「ガガ」) なぜだ？;(
                return null;

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
                LoadNpcIdleSpeech(source["info"]?["speak"]));
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
