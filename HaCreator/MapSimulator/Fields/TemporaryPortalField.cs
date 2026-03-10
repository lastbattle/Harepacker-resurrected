using HaCreator;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Loaders;
using HaCreator.MapSimulator.Pools;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Fields
{
    internal sealed class TemporaryPortalField
    {
        private const int OpenGateSkillId = 35101005;
        private const string MysticDoorSkillName = "Mystic Door";
        private const int OpenGateMaxPortalCount = 2;
        private const int OpenGateDefaultDurationMs = 30_000;
        private const int OpenGateTeleportDelayMs = 120;
        private const int CrossMapPortalTeleportDelayMs = 0;
        private const float PortalInteractRangeX = 40f;
        private const float PortalInteractRangeY = 60f;

        private readonly TexturePool _texturePool;
        private readonly GraphicsDevice _graphicsDevice;
        private readonly List<TemporaryPortal> _portals = new();
        private PortalVisualSet _openGateVisuals;
        private PortalVisualSet _mysticDoorCurrentMapVisuals;
        private PortalVisualSet _mysticDoorTownVisuals;
        private int _nextPortalId = 1;

        public TemporaryPortalField(TexturePool texturePool, GraphicsDevice graphicsDevice)
        {
            _texturePool = texturePool ?? throw new ArgumentNullException(nameof(texturePool));
            _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        }

        public void Update(int currentTime)
        {
            if (_portals.Count == 0)
                return;

            for (int i = _portals.Count - 1; i >= 0; i--)
            {
                if (_portals[i].ExpireTime > currentTime)
                    continue;

                int removedId = _portals[i].Id;
                _portals.RemoveAt(i);

                foreach (TemporaryPortal portal in _portals)
                {
                    if (portal.LinkedPortalId == removedId)
                    {
                        portal.LinkedPortalId = null;
                    }
                }
            }
        }

        public void DrawCurrentMap(
            int mapId,
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            int mapShiftX,
            int mapShiftY,
            int mapCenterX,
            int mapCenterY,
            RenderParameters renderParameters,
            int tickCount)
        {
            foreach (TemporaryPortal portal in _portals)
            {
                if (portal.MapId != mapId)
                    continue;

                portal.Drawable.Draw(
                    spriteBatch,
                    skeletonMeshRenderer,
                    gameTime,
                    mapShiftX,
                    mapShiftY,
                    mapCenterX,
                    mapCenterY,
                    null,
                    renderParameters,
                    tickCount);
            }
        }

        public bool TryUseLinkedPortal(int mapId, float playerX, float playerY, out TemporaryPortalDestination destination)
        {
            destination = default;
            TemporaryPortal nearestPortal = null;
            float nearestDistance = float.MaxValue;

            foreach (TemporaryPortal portal in _portals)
            {
                if (portal.MapId != mapId || portal.LinkedPortalId is null)
                    continue;

                float dx = Math.Abs(playerX - portal.X);
                float dy = Math.Abs(playerY - portal.Y);
                if (dx > PortalInteractRangeX || dy > PortalInteractRangeY)
                    continue;

                TemporaryPortal target = GetPortalById(portal.LinkedPortalId.Value);
                if (target == null)
                    continue;

                float distance = dx + dy;
                if (distance >= nearestDistance)
                    continue;

                nearestDistance = distance;
                nearestPortal = portal;
                destination = new TemporaryPortalDestination(target.MapId, target.X, target.Y, portal.DelayMs);
            }

            if (nearestPortal != null)
            {
                return true;
            }
            return false;
        }

        public bool TryCreateMysticDoor(
            SkillCastInfo castInfo,
            int currentMapId,
            int returnMapId,
            float returnPortalX,
            float returnPortalY)
        {
            if (!IsMysticDoorSkill(castInfo) || currentMapId < 0 || returnMapId < 0)
                return false;

            if (!EnsureMysticDoorVisuals(out PortalVisualSet currentMapVisuals, out PortalVisualSet townVisuals))
                return false;

            int expireTime = castInfo.CastTime + GetPortalDurationMs(castInfo);
            RemovePortalsByKind(TemporaryPortalKind.MysticDoor);

            var fieldDoor = new TemporaryPortal(
                _nextPortalId++,
                TemporaryPortalKind.MysticDoor,
                currentMapId,
                castInfo.CasterX,
                castInfo.CasterY,
                expireTime,
                CrossMapPortalTeleportDelayMs,
                currentMapVisuals.CreateDrawable(castInfo.CasterX, castInfo.CasterY));

            var townDoor = new TemporaryPortal(
                _nextPortalId++,
                TemporaryPortalKind.MysticDoor,
                returnMapId,
                returnPortalX,
                returnPortalY,
                expireTime,
                CrossMapPortalTeleportDelayMs,
                townVisuals.CreateDrawable(returnPortalX, returnPortalY));

            fieldDoor.LinkedPortalId = townDoor.Id;
            townDoor.LinkedPortalId = fieldDoor.Id;

            _portals.Add(fieldDoor);
            _portals.Add(townDoor);
            return true;
        }

        public bool TryCreateOpenGate(SkillCastInfo castInfo, int currentMapId)
        {
            if (castInfo == null || castInfo.SkillId != OpenGateSkillId)
                return false;

            PortalVisualSet visuals = EnsureOpenGateVisuals();
            if (visuals == null)
                return false;

            int expireTime = castInfo.CastTime + GetPortalDurationMs(castInfo);
            var portal = new TemporaryPortal(
                _nextPortalId++,
                TemporaryPortalKind.OpenGate,
                currentMapId,
                castInfo.CasterX,
                castInfo.CasterY,
                expireTime,
                OpenGateTeleportDelayMs,
                visuals.CreateDrawable(castInfo.CasterX, castInfo.CasterY));

            List<TemporaryPortal> openGates = _portals
                .Where(existing => existing.Kind == TemporaryPortalKind.OpenGate && existing.MapId == currentMapId)
                .OrderBy(existing => existing.ExpireTime)
                .ThenBy(existing => existing.Id)
                .ToList();

            while (openGates.Count >= OpenGateMaxPortalCount)
            {
                RemovePortal(openGates[0]);
                openGates.RemoveAt(0);
            }

            _portals.Add(portal);
            openGates.Add(portal);
            RelinkOpenGates(openGates);
            return true;
        }

        private void RemovePortalsByKind(TemporaryPortalKind kind)
        {
            _portals.RemoveAll(portal => portal.Kind == kind);
        }

        private void RelinkOpenGates(List<TemporaryPortal> openGates)
        {
            foreach (TemporaryPortal portal in openGates)
            {
                portal.LinkedPortalId = null;
            }

            if (openGates.Count != 2)
                return;

            openGates[0].LinkedPortalId = openGates[1].Id;
            openGates[1].LinkedPortalId = openGates[0].Id;
        }

        private void RemovePortal(TemporaryPortal portal)
        {
            _portals.Remove(portal);
            foreach (TemporaryPortal existing in _portals)
            {
                if (existing.LinkedPortalId == portal.Id)
                {
                    existing.LinkedPortalId = null;
                }
            }
        }

        private TemporaryPortal GetPortalById(int portalId)
        {
            for (int i = 0; i < _portals.Count; i++)
            {
                if (_portals[i].Id == portalId)
                    return _portals[i];
            }

            return null;
        }

        private bool EnsureMysticDoorVisuals(out PortalVisualSet currentMapVisuals, out PortalVisualSet townVisuals)
        {
            currentMapVisuals = _mysticDoorCurrentMapVisuals;
            townVisuals = _mysticDoorTownVisuals;
            if (currentMapVisuals != null && townVisuals != null)
                return true;

            if (!TryLoadMysticDoorSkillProperty(out WzSubProperty skillProperty))
                return false;

            currentMapVisuals = LoadPortalVisualSet(skillProperty["cDoor"], TemporaryPortalKind.MysticDoor);
            townVisuals = LoadPortalVisualSet(skillProperty["mDoor"], TemporaryPortalKind.MysticDoor);
            if (currentMapVisuals == null || townVisuals == null)
                return false;

            _mysticDoorCurrentMapVisuals = currentMapVisuals;
            _mysticDoorTownVisuals = townVisuals;
            return true;
        }

        private PortalVisualSet EnsureOpenGateVisuals()
        {
            if (_openGateVisuals != null)
                return _openGateVisuals;

            WzImage skillImage = Program.FindImage("Skill", "3510.img");
            if (skillImage?["skill"]?["35101005"] is not WzSubProperty skillProperty)
                return null;

            _openGateVisuals = LoadPortalVisualSet(skillProperty["mDoor"], TemporaryPortalKind.OpenGate);
            return _openGateVisuals;
        }

        private PortalVisualSet LoadPortalVisualSet(WzImageProperty framesProperty, TemporaryPortalKind kind)
        {
            if (framesProperty == null)
                return null;

            var usedProps = new ConcurrentBag<WzObject>();
            List<IDXObject> frames = MapSimulatorLoader.LoadFrames(_texturePool, framesProperty, 0, 0, _graphicsDevice, usedProps);
            if (frames.Count == 0)
                return null;

            return new PortalVisualSet(kind, frames);
        }

        private static bool TryLoadMysticDoorSkillProperty(out WzSubProperty skillProperty)
        {
            skillProperty = null;

            foreach ((string imageName, string skillId) in new[] { ("231.img", "2311002"), ("000.img", "0008001") })
            {
                WzImage skillImage = Program.FindImage("Skill", imageName);
                if (skillImage?["skill"]?[skillId] is not WzSubProperty candidate)
                    continue;

                if (candidate["cDoor"] == null || candidate["mDoor"] == null)
                    continue;

                skillProperty = candidate;
                return true;
            }

            return false;
        }

        private static int GetPortalDurationMs(SkillCastInfo castInfo)
        {
            int seconds = castInfo.LevelData?.Time ?? 0;
            if (seconds <= 0)
                return OpenGateDefaultDurationMs;

            return seconds * 1000;
        }

        internal readonly struct TemporaryPortalDestination
        {
            public TemporaryPortalDestination(int mapId, float x, float y, int delayMs)
            {
                MapId = mapId;
                X = x;
                Y = y;
                DelayMs = delayMs;
            }

            public int MapId { get; }
            public float X { get; }
            public float Y { get; }
            public int DelayMs { get; }
        }

        internal enum TemporaryPortalKind
        {
            OpenGate,
            MysticDoor
        }

        private sealed class PortalVisualSet
        {
            private readonly List<IDXObject> _frames;

            public PortalVisualSet(TemporaryPortalKind kind, List<IDXObject> frames)
            {
                Kind = kind;
                _frames = frames;
            }

            public TemporaryPortalKind Kind { get; }

            public BaseDXDrawableItem CreateDrawable(float x, float y)
            {
                var drawable = new BaseDXDrawableItem(_frames, false)
                {
                    Position = new Point(-(int)MathF.Round(x), -(int)MathF.Round(y))
                };
                return drawable;
            }
        }

        internal sealed class TemporaryPortal
        {
            public TemporaryPortal(
                int id,
                TemporaryPortalKind kind,
                int mapId,
                float x,
                float y,
                int expireTime,
                int delayMs,
                BaseDXDrawableItem drawable)
            {
                Id = id;
                Kind = kind;
                MapId = mapId;
                X = x;
                Y = y;
                ExpireTime = expireTime;
                DelayMs = delayMs;
                Drawable = drawable;
            }

            public int Id { get; }
            public TemporaryPortalKind Kind { get; }
            public int MapId { get; }
            public float X { get; }
            public float Y { get; }
            public int ExpireTime { get; }
            public int DelayMs { get; }
            public BaseDXDrawableItem Drawable { get; }
            public int? LinkedPortalId { get; set; }
        }

        private static bool IsMysticDoorSkill(SkillCastInfo castInfo)
        {
            return castInfo?.SkillData != null
                   && string.Equals(castInfo.SkillData.Name, MysticDoorSkillName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
