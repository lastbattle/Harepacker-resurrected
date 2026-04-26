using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Pools;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using Microsoft.Xna.Framework.Graphics;

namespace HaCreator.MapSimulator.Loaders
{
    /// <summary>
    /// Position type for mob skill effects based on MobSkill.img "pos" property
    /// </summary>
    public enum MobSkillEffectPosition
    {
        /// <summary>Position at mob location (pos=0)</summary>
        Mob = 0,
        /// <summary>Position at affected target/player (pos=1)</summary>
        Target = 1,
        /// <summary>Mob icon display (pos=2)</summary>
        MobIcon = 2,
        /// <summary>Screen/field effect (pos=3)</summary>
        Screen = 3
    }

    /// <summary>
    /// Loaded mob skill effect data containing animation frames and properties
    /// </summary>
    public class MobSkillEffectData
    {
        /// <summary>Mob skill ID (e.g., 126 for Slow)</summary>
        public int SkillId { get; set; }

        /// <summary>Skill level</summary>
        public int Level { get; set; }

        /// <summary>Animation frames for the "affected" effect (played on player)</summary>
        public List<IDXObject> AffectedFrames { get; set; } = new List<IDXObject>();

        /// <summary>Animation frames for the skill "effect" (played at mob or screen)</summary>
        public List<IDXObject> EffectFrames { get; set; } = new List<IDXObject>();

        /// <summary>Animation frames for the "mob" icon effect</summary>
        public List<IDXObject> MobIconFrames { get; set; } = new List<IDXObject>();

        /// <summary>Position type for the affected effect</summary>
        public MobSkillEffectPosition AffectedPosition { get; set; } = MobSkillEffectPosition.Target;

        /// <summary>Position type for the skill effect</summary>
        public MobSkillEffectPosition EffectPosition { get; set; } = MobSkillEffectPosition.Mob;

        /// <summary>Whether the affected animation should repeat</summary>
        public bool AffectedRepeat { get; set; } = false;

        /// <summary>Duration of the skill effect in seconds</summary>
        public int Time { get; set; }

        /// <summary>Total animation duration for affected effect in ms</summary>
        public int AffectedDuration { get; set; }

        /// <summary>Tile animation for field-owned mist / affected-area visuals</summary>
        public SkillAnimation TileAnimation { get; set; }

        /// <summary>Animation frames for delayed bomb detonation visuals from bombInfo/effect</summary>
        public List<IDXObject> BombEffectFrames { get; set; } = new List<IDXObject>();

        /// <summary>Whether this effect has valid affected frames</summary>
        public bool HasAffectedEffect => AffectedFrames != null && AffectedFrames.Count > 0;

        /// <summary>Whether this effect has valid skill effect frames</summary>
        public bool HasEffect => EffectFrames != null && EffectFrames.Count > 0;

        /// <summary>Whether this effect has valid delayed bomb detonation frames</summary>
        public bool HasBombEffect => BombEffectFrames != null && BombEffectFrames.Count > 0;

        /// <summary>Whether this effect has valid tile frames for field-area rendering</summary>
        public bool HasTileAnimation => TileAnimation?.Frames.Count > 0;
    }

    /// <summary>
    /// Loads mob skill effects (affected animations) from Skill.wz/MobSkill.img.
    /// These are the animations that play on the player when hit by a mob's skill.
    /// </summary>
    public class MobSkillEffectLoader
    {
        private readonly GraphicsDevice _device;
        private readonly TexturePool _texturePool;

        // Cache of loaded mob skill effects: Key = (skillId, level)
        private readonly Dictionary<(int, int), MobSkillEffectData> _cache = new Dictionary<(int, int), MobSkillEffectData>();

        // MobSkill.img reference
        private WzImage _mobSkillImg;
        private bool _initialized = false;

        public MobSkillEffectLoader(GraphicsDevice device, TexturePool texturePool)
        {
            _device = device;
            _texturePool = texturePool;
        }

        /// <summary>
        /// Initialize by loading MobSkill.img reference
        /// </summary>
        public void Initialize()
        {
            if (_initialized)
                return;

            // Load Skill.wz/MobSkill.img
            _mobSkillImg = Program.FindImage("Skill", "MobSkill");

            if (_mobSkillImg == null)
            {
                System.Diagnostics.Debug.WriteLine("[MobSkillEffectLoader] Failed to load Skill/MobSkill.img");
            }
            else
            {
                _mobSkillImg.ParseImage();
                System.Diagnostics.Debug.WriteLine("[MobSkillEffectLoader] Initialized MobSkill.img successfully");
            }

            _initialized = true;
        }

        /// <summary>
        /// Load mob skill effect for a specific skill ID and level
        /// </summary>
        /// <param name="skillId">Mob skill ID (e.g., 126 for Slow, 200 for Summon)</param>
        /// <param name="level">Skill level (1-based)</param>
        /// <returns>Loaded effect data or null if not found</returns>
        public MobSkillEffectData LoadMobSkillEffect(int skillId, int level = 1)
        {
            if (!_initialized)
                Initialize();

            if (_mobSkillImg == null)
                return null;

            // Check cache first
            var cacheKey = (skillId, level);
            if (_cache.TryGetValue(cacheKey, out var cached))
                return cached;

            // Load from WZ
            var effectData = LoadMobSkillEffectInternal(skillId, level);
            if (effectData != null)
            {
                _cache[cacheKey] = effectData;
            }

            return effectData;
        }

        private MobSkillEffectData LoadMobSkillEffectInternal(int skillId, int level)
        {
            // MobSkill.img structure: MobSkill.img/{skillId}/level/{level}/affected
            var skillNode = _mobSkillImg[skillId.ToString()];
            if (skillNode == null)
            {
                System.Diagnostics.Debug.WriteLine($"[MobSkillEffectLoader] Skill {skillId} not found in MobSkill.img");
                return null;
            }

            var levelNode = skillNode["level"];
            if (levelNode == null)
            {
                System.Diagnostics.Debug.WriteLine($"[MobSkillEffectLoader] No level node for skill {skillId}");
                return null;
            }

            if (MobSkillLevelResolver.ResolveLevelNode(levelNode as WzSubProperty, level) == null)
            {
                System.Diagnostics.Debug.WriteLine($"[MobSkillEffectLoader] Level {level} not found for skill {skillId}");
                return null;
            }

            var effectData = new MobSkillEffectData
            {
                SkillId = skillId,
                Level = level
            };

            // Load affected effect (plays on player)
            var levelProperty = levelNode as WzSubProperty;
            var affectedNode = MobSkillLevelResolver.FindInheritedProperty(levelProperty, level, "affected");
            if (affectedNode != null)
            {
                var usedProps = new ConcurrentBag<WzObject>();
                effectData.AffectedFrames = MapSimulatorLoader.LoadFrames(_texturePool, affectedNode, 0, 0, _device, usedProps);

                // Get position type
                var posNode = affectedNode["pos"];
                if (posNode != null)
                {
                    int pos = ((WzIntProperty)posNode).Value;
                    effectData.AffectedPosition = (MobSkillEffectPosition)pos;
                }

                // Get repeat flag
                var repeatNode = affectedNode["repeat"];
                if (repeatNode != null)
                {
                    effectData.AffectedRepeat = ((WzIntProperty)repeatNode).Value == 1;
                }

                // Calculate total animation duration
                int totalDuration = 0;
                foreach (var frame in effectData.AffectedFrames)
                {
                    totalDuration += frame.Delay > 0 ? frame.Delay : 100;
                }
                effectData.AffectedDuration = totalDuration;

                System.Diagnostics.Debug.WriteLine($"[MobSkillEffectLoader] Loaded {effectData.AffectedFrames.Count} affected frames for skill {skillId} level {level} (pos={effectData.AffectedPosition}, duration={totalDuration}ms)");
            }

            // Load skill effect (plays at mob or screen)
            var effectNode = MobSkillLevelResolver.FindInheritedProperty(levelProperty, level, "effect");
            if (effectNode != null)
            {
                var usedProps = new ConcurrentBag<WzObject>();
                effectData.EffectFrames = MapSimulatorLoader.LoadFrames(_texturePool, effectNode, 0, 0, _device, usedProps);

                // Get position type
                var posNode = effectNode["pos"];
                if (posNode != null)
                {
                    int pos = ((WzIntProperty)posNode).Value;
                    effectData.EffectPosition = (MobSkillEffectPosition)pos;
                }
            }

            var tileNode = MobSkillLevelResolver.FindInheritedProperty(levelProperty, level, "tile");
            if (tileNode != null)
            {
                effectData.TileAnimation = LoadTileAnimation(tileNode);
            }

            var bombInfoNode = MobSkillLevelResolver.FindInheritedProperty(levelProperty, level, "bombInfo") as WzSubProperty;
            var bombEffectNode = bombInfoNode?["effect"];
            if (bombEffectNode != null)
            {
                var usedProps = new ConcurrentBag<WzObject>();
                effectData.BombEffectFrames = MapSimulatorLoader.LoadFrames(_texturePool, bombEffectNode, 0, 0, _device, usedProps);
            }

            // Load mob icon effect
            var mobNode = MobSkillLevelResolver.FindInheritedProperty(levelProperty, level, "mob");
            if (mobNode != null)
            {
                var usedProps = new ConcurrentBag<WzObject>();
                effectData.MobIconFrames = MapSimulatorLoader.LoadFrames(_texturePool, mobNode, 0, 0, _device, usedProps);
            }

            // Get skill duration
            var timeNode = MobSkillLevelResolver.FindInheritedProperty(levelProperty, level, "time");
            if (timeNode != null)
            {
                effectData.Time = ((WzIntProperty)timeNode).Value;
            }

            return effectData;
        }

        private SkillAnimation LoadTileAnimation(WzImageProperty tileNode)
        {
            if (tileNode == null)
            {
                return null;
            }

            foreach (WzImageProperty child in tileNode.WzProperties)
            {
                if (!int.TryParse(child.Name, out _))
                {
                    continue;
                }

                SkillAnimation animation = LoadAnimation(child, "tile");
                if (animation.Frames.Count > 0)
                {
                    animation.Loop = true;
                    return animation;
                }
            }

            SkillAnimation fallbackAnimation = LoadAnimation(tileNode, "tile");
            if (fallbackAnimation.Frames.Count > 0)
            {
                fallbackAnimation.Loop = true;
                return fallbackAnimation;
            }

            return null;
        }

        private SkillAnimation LoadAnimation(WzImageProperty node, string name)
        {
            var animation = new SkillAnimation { Name = name };
            if (node == null)
            {
                return animation;
            }

            foreach (WzImageProperty child in node.WzProperties)
            {
                if (!int.TryParse(child.Name, out _))
                {
                    continue;
                }

                var usedProps = new ConcurrentBag<WzObject>();
                List<IDXObject> frames = MapSimulatorLoader.LoadFrames(_texturePool, child, 0, 0, _device, usedProps);
                if (frames.Count == 0)
                {
                    continue;
                }

                int frameDelay = 100;
                if (child["delay"] is WzIntProperty delayProperty)
                {
                    frameDelay = Math.Max(1, delayProperty.Value);
                }

                IDXObject texture = frames[0];
                animation.Frames.Add(new SkillFrame
                {
                    Texture = texture,
                    Delay = frameDelay,
                    Bounds = new Microsoft.Xna.Framework.Rectangle(0, 0, texture.Width, texture.Height),
                    Origin = new Microsoft.Xna.Framework.Point(0, 0),
                    Flip = false
                });
            }

            if (animation.Frames.Count == 0)
            {
                var usedProps = new ConcurrentBag<WzObject>();
                List<IDXObject> frames = MapSimulatorLoader.LoadFrames(_texturePool, node, 0, 0, _device, usedProps);
                foreach (IDXObject texture in frames)
                {
                    animation.Frames.Add(new SkillFrame
                    {
                        Texture = texture,
                        Delay = Math.Max(1, texture.Delay > 0 ? texture.Delay : 100),
                        Bounds = new Microsoft.Xna.Framework.Rectangle(0, 0, texture.Width, texture.Height),
                        Origin = new Microsoft.Xna.Framework.Point(0, 0),
                        Flip = false
                    });
                }
            }

            animation.CalculateDuration();
            return animation;
        }

        /// <summary>
        /// Preload common mob skill effects for faster access during gameplay
        /// </summary>
        public void PreloadCommonSkills()
        {
            if (!_initialized)
                Initialize();

            // Common mob skill IDs to preload
            int[] commonSkillIds = {
                100, 101, 102, 103, 105,  // Physical attacks, summons
                110, 111, 112, 113, 114, 115, // Speed/defense modifiers
                120, 121, 122, 123, 124, 125, 126, 127, 128, 129, // Status effects (slow, seal, etc.)
                131, 132, 133, 134, 135, 136, 137, 138, // More status effects
                140, 141, 142, 143, 144, 145, 146, // Debuffs
                150, 151, 152, 153, 154, 155, 156, 157, // More debuffs
                200 // Summon
            };

            foreach (int skillId in commonSkillIds)
            {
                LoadMobSkillEffect(skillId, 1);
            }

            System.Diagnostics.Debug.WriteLine($"[MobSkillEffectLoader] Preloaded {_cache.Count} mob skill effects");
        }

        /// <summary>
        /// Clear the cache
        /// </summary>
        public void ClearCache()
        {
            _cache.Clear();
        }

        /// <summary>
        /// Get all loaded skill IDs
        /// </summary>
        public IEnumerable<int> LoadedSkillIds => _cache.Keys.Select(k => k.Item1).Distinct();
    }
}
