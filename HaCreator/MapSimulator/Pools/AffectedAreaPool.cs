using System;
using System.Collections.Generic;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Loaders;
using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace HaCreator.MapSimulator.Pools
{
    public enum AffectedAreaSourceKind
    {
        PlayerSkill = 0,
        MobSkill = 1,
        AreaBuffItem = 2
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
        private readonly GraphicsDevice _graphicsDevice;
        private readonly Dictionary<int, ActiveAffectedArea> _areas = new();
        private readonly Dictionary<int, SkillAnimation> _areaBuffItemFogAnimationCache = new();

        public AffectedAreaPool(SkillLoader skillLoader, MobSkillEffectLoader mobSkillEffectLoader, GraphicsDevice graphicsDevice = null)
        {
            _skillLoader = skillLoader;
            _mobSkillEffectLoader = mobSkillEffectLoader;
            _graphicsDevice = graphicsDevice;
        }

        public IEnumerable<ActiveAffectedArea> ActiveAreas => _areas.Values;
        public int Count => _areas.Count;

        public bool TryGetArea(int objectId, out ActiveAffectedArea area)
        {
            return _areas.TryGetValue(objectId, out area);
        }

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
                AffectedAreaSourceKind.AreaBuffItem => BuildAreaBuffItemArea(createInfo, currentTime),
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
            Func<int, SkillData> loadLinkedSkill = _skillLoader == null
                ? null
                : skillId => _skillLoader.LoadSkill(skillId);
            int durationSeconds = ResolvePlayerSkillAreaDurationSeconds(
                skill,
                levelData,
                loadLinkedSkill,
                ResolveSkillLevel,
                createInfo.SkillLevel);
            int expireTime = ResolveExpireTime(startTime, durationSeconds);

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

        private ActiveAffectedArea BuildAreaBuffItemArea(AffectedAreaCreateInfo createInfo, int currentTime)
        {
            WzSubProperty itemProperty = LoadItemProperty(createInfo.SkillId);
            SkillAnimation animation = LoadAreaBuffItemFogAnimation(createInfo.SkillId, itemProperty);
            if (animation == null)
            {
                return null;
            }

            int startTime = ResolveStartTime(currentTime, createInfo.StartDelayUnits);
            int expireTime = ResolveAreaBuffItemExpireTime(startTime, itemProperty, createInfo.SkillId, animation);
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
                ZoneType = "fog",
                Animation = animation,
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

        internal static int ResolvePlayerSkillAreaDurationSeconds(
            SkillData skill,
            SkillLevelData levelData,
            Func<int, SkillData> loadSkill,
            Func<SkillData, int, SkillLevelData> resolveLevelData,
            int skillLevel)
        {
            int durationSeconds = ResolveSkillAreaLevelDurationSeconds(levelData);
            if (skill == null || loadSkill == null || resolveLevelData == null)
            {
                return durationSeconds;
            }

            var visitedSkillIds = new HashSet<int>();
            CollectLinkedPlayerSkillAreaDurationSeconds(
                skill,
                loadSkill,
                resolveLevelData,
                Math.Max(1, skillLevel),
                visitedSkillIds,
                ref durationSeconds);
            return durationSeconds;
        }

        private static void CollectLinkedPlayerSkillAreaDurationSeconds(
            SkillData skill,
            Func<int, SkillData> loadSkill,
            Func<SkillData, int, SkillLevelData> resolveLevelData,
            int skillLevel,
            ISet<int> visitedSkillIds,
            ref int durationSeconds)
        {
            if (skill == null)
            {
                return;
            }

            int skillId = skill.SkillId;
            if (skillId > 0 && visitedSkillIds?.Add(skillId) != true)
            {
                return;
            }

            foreach (int linkedSkillId in RemoteAffectedAreaSupportResolver.EnumerateRemoteAffectedAreaLinkedSkillIds(skill))
            {
                SkillData linkedSkill = loadSkill(linkedSkillId);
                if (linkedSkill == null)
                {
                    continue;
                }

                SkillLevelData linkedLevelData = resolveLevelData(linkedSkill, skillLevel);
                durationSeconds = Math.Max(durationSeconds, ResolveSkillAreaLevelDurationSeconds(linkedLevelData));
                CollectLinkedPlayerSkillAreaDurationSeconds(
                    linkedSkill,
                    loadSkill,
                    resolveLevelData,
                    skillLevel,
                    visitedSkillIds,
                    ref durationSeconds);
            }
        }

        private static int ResolveSkillAreaLevelDurationSeconds(SkillLevelData levelData)
        {
            return levelData == null
                ? 0
                : Math.Max(levelData.Time, levelData.DotTime);
        }

        private static int ResolveExpireTime(int currentTime, int durationSeconds)
        {
            return durationSeconds > 0 ? currentTime + (durationSeconds * 1000) : 0;
        }

        private static int ResolveAreaBuffItemExpireTime(
            int startTime,
            WzSubProperty itemProperty,
            int itemId,
            SkillAnimation animation)
        {
            int durationMs = ResolveAreaBuffItemDurationMs(itemProperty, itemId, animation);
            return durationMs > 0 ? startTime + durationMs : 0;
        }

        internal static int ResolveAreaBuffItemDurationMs(
            WzSubProperty itemProperty,
            int itemId,
            SkillAnimation animation = null)
        {
            string itemDescription = itemId > 0 && InventoryItemMetadataResolver.TryResolveItemDescription(itemId, out string resolvedDescription)
                ? resolvedDescription
                : null;
            return AreaBuffItemMetadataResolver.ResolveDurationMs(
                itemProperty,
                itemDescription,
                LoadLinkedAreaBuffItemProperty,
                LoadLinkedAreaBuffItemDescription,
                LoadLinkedAreaBuffPathProperty,
                animation);
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

        private SkillAnimation LoadAreaBuffItemFogAnimation(int itemId, WzSubProperty itemProperty = null)
        {
            if (itemId <= 0 || _graphicsDevice == null)
            {
                return null;
            }

            if (_areaBuffItemFogAnimationCache.TryGetValue(itemId, out SkillAnimation cachedAnimation))
            {
                return cachedAnimation;
            }

            itemProperty ??= LoadItemProperty(itemId);
            WzImageProperty fogNode = (itemProperty?["effect"] as WzSubProperty)?["darkFog"];
            SkillAnimation animation = LoadCanvasAnimation(fogNode);
            if (animation == null)
            {
                return null;
            }

            animation.Loop = true;
            _areaBuffItemFogAnimationCache[itemId] = animation;
            return animation;
        }

        private static WzSubProperty LoadItemProperty(int itemId)
        {
            if (!InventoryItemMetadataResolver.TryResolveImageSource(itemId, out string category, out string imagePath))
            {
                return null;
            }

            WzImage itemImage = global::HaCreator.Program.FindImage(category, imagePath);
            if (itemImage == null)
            {
                return null;
            }

            itemImage.ParseImage();
            string itemNodeName = category == "Character" ? itemId.ToString("D8") : itemId.ToString("D7");
            return itemImage[itemNodeName] as WzSubProperty;
        }

        private static WzSubProperty LoadLinkedAreaBuffItemProperty(string itemInfoPath)
        {
            if (string.IsNullOrWhiteSpace(itemInfoPath))
            {
                return null;
            }

            string normalizedPath = itemInfoPath.Trim().Replace('\\', '/');
            if (normalizedPath.StartsWith("Item/", StringComparison.OrdinalIgnoreCase))
            {
                normalizedPath = normalizedPath.Substring("Item/".Length);
            }

            string[] segments = normalizedPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (TryResolveLinkedItemNodeNameFromPath(segments, out string itemNodeName)
                && TryResolveLinkedItemImagePath(segments, out string imagePath))
            {
                WzImage linkedImage = global::HaCreator.Program.FindImage("Item", imagePath);
                if (linkedImage == null)
                {
                    return null;
                }

                linkedImage.ParseImage();
                return linkedImage[itemNodeName] as WzSubProperty;
            }

            if (!TryResolveLinkedItemNodeName(segments.Length > 0 ? segments[^1] : normalizedPath, out string fallbackNodeName)
                && !TryResolveLinkedItemNodeNameFromPath(segments, out fallbackNodeName)
                || !int.TryParse(fallbackNodeName, out int fallbackItemId))
            {
                return null;
            }

            return LoadItemProperty(fallbackItemId);
        }

        private static string LoadLinkedAreaBuffItemDescription(string itemInfoPath)
        {
            if (!TryResolveLinkedItemId(itemInfoPath, out int itemId))
            {
                return null;
            }

            return InventoryItemMetadataResolver.TryResolveItemDescription(itemId, out string description)
                ? description
                : null;
        }

        private static WzImageProperty LoadLinkedAreaBuffPathProperty(string linkedPath)
        {
            string normalizedPath = linkedPath?.Trim().Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return null;
            }

            string[] segments = normalizedPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 3)
            {
                return null;
            }

            string category = segments[0];
            int imageSegmentIndex = -1;
            for (int i = 1; i < segments.Length; i++)
            {
                if (segments[i].EndsWith(".img", StringComparison.OrdinalIgnoreCase))
                {
                    imageSegmentIndex = i;
                    break;
                }
            }

            if (imageSegmentIndex <= 0 || imageSegmentIndex >= segments.Length - 1)
            {
                return null;
            }

            string imagePath = string.Join("/", segments, 1, imageSegmentIndex);
            WzImage image = global::HaCreator.Program.FindImage(category, imagePath);
            if (image == null)
            {
                return null;
            }

            image.ParseImage();
            string propertyPath = string.Join("/", segments, imageSegmentIndex + 1, segments.Length - imageSegmentIndex - 1);
            if (string.IsNullOrWhiteSpace(propertyPath))
            {
                return null;
            }

            WzImageProperty current = image[propertyPath.Split('/')[0]];
            string[] propertySegments = propertyPath.Split('/');
            for (int i = 1; i < propertySegments.Length && current != null; i++)
            {
                current = current[propertySegments[i]];
            }

            return current;
        }

        private static bool TryResolveLinkedItemId(string itemInfoPath, out int itemId)
        {
            itemId = 0;
            string normalizedPath = itemInfoPath?.Trim().Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return false;
            }

            string[] segments = normalizedPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            string candidate = segments.Length > 0 ? segments[^1] : normalizedPath;
            if (!TryResolveLinkedItemNodeName(candidate, out string itemNodeName)
                && !TryResolveLinkedItemNodeNameFromPath(segments, out itemNodeName))
            {
                return false;
            }

            return int.TryParse(itemNodeName, out itemId) && itemId > 0;
        }

        private static bool TryResolveLinkedItemImagePath(string[] pathSegments, out string imagePath)
        {
            imagePath = null;
            if (pathSegments == null || pathSegments.Length == 0)
            {
                return false;
            }

            for (int i = pathSegments.Length - 1; i >= 0; i--)
            {
                string segment = pathSegments[i];
                if (string.IsNullOrWhiteSpace(segment)
                    || !segment.EndsWith(".img", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (i <= 0)
                {
                    return false;
                }

                imagePath = $"{pathSegments[i - 1]}/{segment}";
                return true;
            }

            return false;
        }

        private static bool TryResolveLinkedItemNodeNameFromPath(string[] pathSegments, out string itemNodeName)
        {
            itemNodeName = null;
            if (pathSegments == null || pathSegments.Length == 0)
            {
                return false;
            }

            for (int i = pathSegments.Length - 1; i >= 0; i--)
            {
                if (TryResolveLinkedItemNodeName(pathSegments[i], out itemNodeName))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveLinkedItemNodeName(string value, out string itemNodeName)
        {
            itemNodeName = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string trimmedValue = value.Trim();
            if (trimmedValue.EndsWith(".img", StringComparison.OrdinalIgnoreCase))
            {
                trimmedValue = trimmedValue.Substring(0, trimmedValue.Length - 4);
            }

            if (!int.TryParse(trimmedValue, out int itemId) || itemId <= 0)
            {
                return false;
            }

            itemNodeName = itemId >= 10000000 ? itemId.ToString("D8") : itemId.ToString("D7");
            return true;
        }

        private SkillAnimation LoadCanvasAnimation(WzImageProperty node)
        {
            if (node == null)
            {
                return null;
            }

            SkillAnimation animation = new SkillAnimation
            {
                Name = node.Name
            };

            List<(int index, SkillFrame frame)> orderedFrames = new();
            foreach (WzImageProperty child in node.WzProperties)
            {
                if (!int.TryParse(child.Name, out int frameIndex))
                {
                    continue;
                }

                SkillFrame frame = LoadCanvasFrame(child);
                if (frame == null)
                {
                    continue;
                }

                orderedFrames.Add((frameIndex, frame));
            }

            if (orderedFrames.Count == 0)
            {
                return null;
            }

            orderedFrames.Sort(static (left, right) => left.index.CompareTo(right.index));
            foreach ((_, SkillFrame frame) in orderedFrames)
            {
                animation.Frames.Add(frame);
            }

            animation.CalculateDuration();
            animation.Loop = ResolveAnimationLoop(node);
            return animation;
        }

        private static bool ResolveAnimationLoop(WzImageProperty node)
        {
            if (node == null)
            {
                return false;
            }

            return GetInt(node, "loop") != 0
                   || GetInt(node, "repeat") != 0
                   || GetInt(node, "r") != 0;
        }

        private SkillFrame LoadCanvasFrame(WzImageProperty frameNode)
        {
            WzCanvasProperty canvas = frameNode as WzCanvasProperty ?? frameNode?.GetLinkedWzImageProperty() as WzCanvasProperty;
            if (canvas == null)
            {
                return null;
            }

            var bitmap = canvas.GetLinkedWzCanvasBitmap();
            Texture2D texture = bitmap?.ToTexture2DAndDispose(_graphicsDevice);
            if (texture == null)
            {
                return null;
            }

            System.Drawing.PointF origin = canvas.GetCanvasOriginPosition();
            return new SkillFrame
            {
                Texture = new DXObject(0, 0, texture)
                {
                    Tag = canvas.FullPath
                },
                Origin = new Point((int)origin.X, (int)origin.Y),
                Bounds = new Rectangle(0, 0, texture.Width, texture.Height),
                Delay = GetInt(frameNode, "delay", 100),
                Flip = GetInt(frameNode, "flip") == 1
            };
        }

        private static int GetInt(WzImageProperty node, string name, int defaultValue = 0)
        {
            WzImageProperty valueProperty = node?[name];
            return valueProperty switch
            {
                WzIntProperty intProperty => intProperty.Value,
                WzShortProperty shortProperty => shortProperty.Value,
                WzLongProperty longProperty => (int)longProperty.Value,
                _ => defaultValue
            };
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
