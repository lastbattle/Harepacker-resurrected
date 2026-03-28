using System;
using System.Collections.Generic;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Loaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace HaCreator.MapSimulator.Pools
{
    public enum AffectedAreaSourceKind
    {
        PlayerSkill = 0,
        MobSkill = 1
    }

    public readonly struct AffectedAreaCreateInfo
    {
        public AffectedAreaCreateInfo(
            int objectId,
            int type,
            int ownerId,
            int skillId,
            int skillLevel,
            Rectangle worldBounds,
            short startDelayUnits,
            int elementAttribute,
            int phase,
            AffectedAreaSourceKind sourceKind)
        {
            ObjectId = objectId;
            Type = type;
            OwnerId = ownerId;
            SkillId = skillId;
            SkillLevel = skillLevel;
            WorldBounds = worldBounds;
            StartDelayUnits = startDelayUnits;
            ElementAttribute = elementAttribute;
            Phase = phase;
            SourceKind = sourceKind;
        }

        public int ObjectId { get; }
        public int Type { get; }
        public int OwnerId { get; }
        public int SkillId { get; }
        public int SkillLevel { get; }
        public Rectangle WorldBounds { get; }
        public short StartDelayUnits { get; }
        public int ElementAttribute { get; }
        public int Phase { get; }
        public AffectedAreaSourceKind SourceKind { get; }
    }

    public sealed class ActiveAffectedArea
    {
        public int ObjectId { get; init; }
        public int Type { get; init; }
        public int OwnerId { get; init; }
        public int SkillId { get; init; }
        public int SkillLevel { get; init; }
        public int Phase { get; init; }
        public int ElementAttribute { get; init; }
        public int StartTime { get; init; }
        public int ExpireTime { get; init; }
        public int NextGameplayTickTime { get; set; }
        public Rectangle WorldBounds { get; init; }
        public string ZoneType { get; init; }
        public SkillAnimation Animation { get; init; }
        public AffectedAreaSourceKind SourceKind { get; init; }
        public bool IsRemoving { get; set; }
        public int RemoveStartTime { get; set; } = int.MinValue;

        public bool IsExpired(int currentTime)
        {
            return ExpireTime > 0 && currentTime >= ExpireTime;
        }

        public bool IsActive(int currentTime)
        {
            return currentTime >= StartTime
                   && !IsExpired(currentTime)
                   && !IsRemoving;
        }

        public bool IsRenderable(int currentTime)
        {
            return Animation?.Frames.Count > 0
                   && currentTime >= StartTime
                   && !IsExpired(currentTime)
                   && !(IsRemoving && currentTime - RemoveStartTime >= AffectedAreaPool.RemoveFadeDurationMs);
        }

        public bool Contains(float worldX, float worldY)
        {
            return WorldBounds.Contains((int)worldX, (int)worldY);
        }

        public int AnimationTime(int currentTime)
        {
            int anchorTime = IsRemoving ? RemoveStartTime : StartTime;
            return Math.Max(0, currentTime - anchorTime);
        }
    }

    public sealed class AffectedAreaPool
    {
        internal const int RemoveFadeDurationMs = 1000;

        private readonly SkillLoader _skillLoader;
        private readonly MobSkillEffectLoader _mobSkillEffectLoader;
        private readonly Dictionary<int, ActiveAffectedArea> _areas = new();

        public AffectedAreaPool(SkillLoader skillLoader, MobSkillEffectLoader mobSkillEffectLoader)
        {
            _skillLoader = skillLoader;
            _mobSkillEffectLoader = mobSkillEffectLoader;
        }

        public IEnumerable<ActiveAffectedArea> ActiveAreas => _areas.Values;
        public int Count => _areas.Count;

        public bool Upsert(AffectedAreaCreateInfo createInfo, int currentTime)
        {
            if (createInfo.ObjectId <= 0 || createInfo.SkillId <= 0 || createInfo.WorldBounds.Width <= 0 || createInfo.WorldBounds.Height <= 0)
            {
                return false;
            }

            ActiveAffectedArea area = BuildArea(createInfo, currentTime);
            if (area == null)
            {
                return false;
            }

            _areas[createInfo.ObjectId] = area;
            return true;
        }

        public bool Remove(int objectId, int currentTime)
        {
            if (!_areas.TryGetValue(objectId, out ActiveAffectedArea area))
            {
                return false;
            }

            area.IsRemoving = true;
            area.RemoveStartTime = currentTime;
            return true;
        }

        public void Clear()
        {
            _areas.Clear();
        }

        public void Update(int currentTime)
        {
            List<int> removedIds = null;

            foreach ((int objectId, ActiveAffectedArea area) in _areas)
            {
                if (area == null)
                {
                    removedIds ??= new List<int>();
                    removedIds.Add(objectId);
                    continue;
                }

                bool shouldRemove = area.IsExpired(currentTime);
                shouldRemove |= area.IsRemoving && currentTime - area.RemoveStartTime >= RemoveFadeDurationMs;
                if (!shouldRemove)
                {
                    continue;
                }

                removedIds ??= new List<int>();
                removedIds.Add(objectId);
            }

            if (removedIds == null)
            {
                return;
            }

            for (int i = 0; i < removedIds.Count; i++)
            {
                _areas.Remove(removedIds[i]);
            }
        }

        public bool IsPointInsideZone(float worldX, float worldY, int currentTime, params string[] zoneTypes)
        {
            foreach (ActiveAffectedArea area in _areas.Values)
            {
                if (area == null
                    || !area.IsActive(currentTime)
                    || !area.Contains(worldX, worldY)
                    || !MatchesZoneType(area.ZoneType, zoneTypes))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        public bool TryBeginGameplayTick(ActiveAffectedArea area, int currentTime, int intervalMs)
        {
            if (area?.IsActive(currentTime) != true)
            {
                return false;
            }

            int normalizedIntervalMs = Math.Max(100, intervalMs);
            if (currentTime < area.NextGameplayTickTime)
            {
                return false;
            }

            area.NextGameplayTickTime = currentTime + normalizedIntervalMs;
            return true;
        }

        public void Draw(SpriteBatch spriteBatch, int mapShiftX, int mapShiftY, int centerX, int centerY, int currentTime)
        {
            foreach (ActiveAffectedArea area in _areas.Values)
            {
                DrawArea(spriteBatch, area, mapShiftX, mapShiftY, centerX, centerY, currentTime);
            }
        }

        private ActiveAffectedArea BuildArea(AffectedAreaCreateInfo createInfo, int currentTime)
        {
            return createInfo.SourceKind switch
            {
                AffectedAreaSourceKind.PlayerSkill => BuildPlayerSkillArea(createInfo, currentTime),
                AffectedAreaSourceKind.MobSkill => BuildMobSkillArea(createInfo, currentTime),
                _ => null
            };
        }

        private ActiveAffectedArea BuildPlayerSkillArea(AffectedAreaCreateInfo createInfo, int currentTime)
        {
            SkillData skill = _skillLoader?.LoadSkill(createInfo.SkillId);
            if (skill == null)
            {
                return null;
            }

            SkillLevelData levelData = ResolveSkillLevel(skill, createInfo.SkillLevel);
            int startTime = ResolveStartTime(currentTime, createInfo.StartDelayUnits);
            int expireTime = ResolveExpireTime(startTime, levelData?.Time ?? 0);

            return new ActiveAffectedArea
            {
                ObjectId = createInfo.ObjectId,
                Type = createInfo.Type,
                OwnerId = createInfo.OwnerId,
                SkillId = createInfo.SkillId,
                SkillLevel = createInfo.SkillLevel,
                Phase = createInfo.Phase,
                ElementAttribute = createInfo.ElementAttribute,
                StartTime = startTime,
                ExpireTime = expireTime,
                NextGameplayTickTime = startTime,
                WorldBounds = createInfo.WorldBounds,
                ZoneType = skill.ZoneType,
                Animation = skill.ZoneAnimation,
                SourceKind = createInfo.SourceKind
            };
        }

        private ActiveAffectedArea BuildMobSkillArea(AffectedAreaCreateInfo createInfo, int currentTime)
        {
            MobSkillEffectData effectData = _mobSkillEffectLoader?.LoadMobSkillEffect(createInfo.SkillId, Math.Max(1, createInfo.SkillLevel));
            if (effectData == null)
            {
                return null;
            }

            int startTime = ResolveStartTime(currentTime, createInfo.StartDelayUnits);
            int expireTime = ResolveExpireTime(startTime, effectData.Time);

            return new ActiveAffectedArea
            {
                ObjectId = createInfo.ObjectId,
                Type = createInfo.Type,
                OwnerId = createInfo.OwnerId,
                SkillId = createInfo.SkillId,
                SkillLevel = createInfo.SkillLevel,
                Phase = createInfo.Phase,
                ElementAttribute = createInfo.ElementAttribute,
                StartTime = startTime,
                ExpireTime = expireTime,
                NextGameplayTickTime = startTime,
                WorldBounds = createInfo.WorldBounds,
                ZoneType = "mist",
                Animation = effectData.TileAnimation,
                SourceKind = createInfo.SourceKind
            };
        }

        private static SkillLevelData ResolveSkillLevel(SkillData skill, int level)
        {
            if (skill == null)
            {
                return null;
            }

            SkillLevelData levelData = skill.GetLevel(Math.Max(1, level));
            if (levelData != null)
            {
                return levelData;
            }

            for (int i = 1; i <= Math.Max(1, skill.MaxLevel); i++)
            {
                levelData = skill.GetLevel(i);
                if (levelData != null)
                {
                    return levelData;
                }
            }

            return null;
        }

        private static int ResolveExpireTime(int currentTime, int durationSeconds)
        {
            return durationSeconds > 0 ? currentTime + (durationSeconds * 1000) : 0;
        }

        private static int ResolveStartTime(int currentTime, short startDelayUnits)
        {
            return currentTime + (Math.Max(0, (int)startDelayUnits) * 100);
        }

        private static bool MatchesZoneType(string zoneType, params string[] expectedZoneTypes)
        {
            if (string.IsNullOrWhiteSpace(zoneType) || expectedZoneTypes == null)
            {
                return false;
            }

            for (int i = 0; i < expectedZoneTypes.Length; i++)
            {
                string expectedZoneType = expectedZoneTypes[i];
                if (string.IsNullOrWhiteSpace(expectedZoneType))
                {
                    continue;
                }

                if (zoneType.Equals(expectedZoneType, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void DrawArea(SpriteBatch spriteBatch, ActiveAffectedArea area, int mapShiftX, int mapShiftY, int centerX, int centerY, int currentTime)
        {
            if (area?.IsRenderable(currentTime) != true)
            {
                return;
            }

            SkillFrame frame = area.Animation.GetFrameAtTime(area.AnimationTime(currentTime));
            if (frame?.Texture == null)
            {
                return;
            }

            int tileWidth = Math.Max(1, frame.Bounds.Width);
            int tileHeight = Math.Max(1, frame.Bounds.Height);
            int columns = Math.Max(1, (int)Math.Ceiling(area.WorldBounds.Width / (float)tileWidth));
            int rows = Math.Max(1, (int)Math.Ceiling(area.WorldBounds.Height / (float)tileHeight));
            int startX = area.WorldBounds.Left + tileWidth / 2;
            int startY = area.WorldBounds.Top + tileHeight / 2;
            float alpha = area.IsRemoving
                ? MathHelper.Clamp(1f - ((currentTime - area.RemoveStartTime) / (float)RemoveFadeDurationMs), 0f, 1f)
                : 1f;
            Color tint = Color.White * alpha;

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    int worldX = startX + (column * tileWidth);
                    int worldY = startY + (row * tileHeight);
                    int screenX = worldX - mapShiftX + centerX;
                    int screenY = worldY - mapShiftY + centerY;
                    bool shouldFlip = frame.Flip;

                    frame.Texture.DrawBackground(
                        spriteBatch,
                        null,
                        null,
                        GetFrameDrawX(screenX, frame, shouldFlip),
                        screenY - frame.Origin.Y,
                        tint,
                        shouldFlip,
                        null);
                }
            }
        }

        private static int GetFrameDrawX(int screenX, SkillFrame frame, bool shouldFlip)
        {
            if (!shouldFlip)
            {
                return screenX - frame.Origin.X;
            }

            return screenX + frame.Origin.X - frame.Bounds.Width;
        }
    }
}
