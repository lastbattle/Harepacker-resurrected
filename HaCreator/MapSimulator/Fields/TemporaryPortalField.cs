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
        private const byte RemoteTownPortalOverlayState = 1;

        private readonly TexturePool _texturePool;
        private readonly GraphicsDevice _graphicsDevice;
        private readonly List<TemporaryPortal> _portals = new();
        private readonly Dictionary<uint, RemoteTownPortalState> _remoteTownPortals = new();
        private readonly Dictionary<RemoteOpenGateKey, RemoteOpenGateState> _remoteOpenGates = new();
        private PortalVisualSet _openGateVisuals;
        private PortalVisualSet _mysticDoorCurrentMapVisuals;
        private PortalVisualSet _mysticDoorTownVisuals;
        private PortalVisualSet _mysticDoorFrameVisuals;
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

                foreach (BaseDXDrawableItem drawable in portal.Drawables)
                {
                    drawable.Draw(
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
        }

        public bool TryUseLinkedPortal(int mapId, float playerX, float playerY, out TemporaryPortalDestination destination)
        {
            destination = default;
            TemporaryPortal nearestPortal = null;
            float nearestDistance = float.MaxValue;

            foreach (TemporaryPortal portal in _portals)
            {
                if (portal.MapId != mapId || (portal.LinkedPortalId is null && !portal.DirectDestinationMapId.HasValue))
                    continue;

                float dx = Math.Abs(playerX - portal.X);
                float dy = Math.Abs(playerY - portal.Y);
                if (dx > PortalInteractRangeX || dy > PortalInteractRangeY)
                    continue;

                if (!TryResolvePortalDestination(portal, out TemporaryPortalDestination candidateDestination))
                    continue;

                float distance = dx + dy;
                if (distance >= nearestDistance)
                    continue;

                nearestDistance = distance;
                nearestPortal = portal;
                destination = candidateDestination;
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

            if (!EnsureMysticDoorVisuals(out PortalVisualSet currentMapVisuals, out PortalVisualSet townVisuals, out _))
                return false;

            int expireTime = castInfo.CastTime + GetPortalDurationMs(castInfo);
            RemovePortalsByKind(TemporaryPortalKind.MysticDoor);

            var fieldDoor = new TemporaryPortal(
                _nextPortalId++,
                TemporaryPortalKind.MysticDoor,
                TemporaryPortalSource.LocalCast,
                currentMapId,
                castInfo.CasterX,
                castInfo.CasterY,
                expireTime,
                CrossMapPortalTeleportDelayMs,
                currentMapVisuals.CreateDrawable(castInfo.CasterX, castInfo.CasterY));

            var townDoor = new TemporaryPortal(
                _nextPortalId++,
                TemporaryPortalKind.MysticDoor,
                TemporaryPortalSource.LocalCast,
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

        public void ClearRemotePortals()
        {
            _remoteTownPortals.Clear();
            _remoteOpenGates.Clear();
            RemovePortalsBySource(TemporaryPortalSource.RemoteTownPortalPool);
            RemovePortalsBySource(TemporaryPortalSource.RemoteOpenGatePool);
        }

        public string DescribeRemotePortalStatus(int currentMapId)
        {
            int townPortalCount = _remoteTownPortals.Values.Count(state => state.MapId == currentMapId);
            int openGateCount = _remoteOpenGates.Values.Count(state => state.MapId == currentMapId);
            return $"Remote portal pools: town={townPortalCount}, opengate={openGateCount}";
        }

        public bool TryApplyRemotePortalPacket(
            int packetType,
            byte[] payload,
            int currentMapId,
            RemoteTownPortalResolvedDestination? townPortalDestination,
            out string result)
        {
            result = null;
            payload ??= Array.Empty<byte>();

            switch (packetType)
            {
                case (int)RemotePortalPacketType.TownPortalCreate:
                    if (!RemotePortalPacketCodec.TryParseTownPortalCreated(payload, out RemoteTownPortalCreatedPacket townCreate, out string townCreateError))
                    {
                        result = townCreateError;
                        return false;
                    }

                    ApplyRemoteTownPortalCreate(townCreate, currentMapId, townPortalDestination);
                    result = $"Applied {RemotePortalPacketCodec.DescribePacketType(packetType)} for owner {townCreate.OwnerCharacterId}.";
                    return true;

                case (int)RemotePortalPacketType.TownPortalRemove:
                    if (!RemotePortalPacketCodec.TryParseTownPortalRemoved(payload, out RemoteTownPortalRemovedPacket townRemove, out string townRemoveError))
                    {
                        result = townRemoveError;
                        return false;
                    }

                    bool removedTownPortal = _remoteTownPortals.Remove(townRemove.OwnerCharacterId);
                    SyncRemoteTownPortalVisuals();
                    result = removedTownPortal
                        ? $"Applied {RemotePortalPacketCodec.DescribePacketType(packetType)} for owner {townRemove.OwnerCharacterId}."
                        : $"Ignored {RemotePortalPacketCodec.DescribePacketType(packetType)} for unknown owner {townRemove.OwnerCharacterId}.";
                    return true;

                case (int)RemotePortalPacketType.OpenGateCreate:
                    if (!RemotePortalPacketCodec.TryParseOpenGateCreated(payload, out RemoteOpenGateCreatedPacket openGateCreate, out string openGateCreateError))
                    {
                        result = openGateCreateError;
                        return false;
                    }

                    ApplyRemoteOpenGateCreate(openGateCreate, currentMapId);
                    result = $"Applied {RemotePortalPacketCodec.DescribePacketType(packetType)} for owner {openGateCreate.OwnerCharacterId} slot {(openGateCreate.IsFirstSlot ? 1 : 2)}.";
                    return true;

                case (int)RemotePortalPacketType.OpenGateRemove:
                    if (!RemotePortalPacketCodec.TryParseOpenGateRemoved(payload, out RemoteOpenGateRemovedPacket openGateRemove, out string openGateRemoveError))
                    {
                        result = openGateRemoveError;
                        return false;
                    }

                    bool removedOpenGate = _remoteOpenGates.Remove(new RemoteOpenGateKey(openGateRemove.OwnerCharacterId, openGateRemove.IsFirstSlot));
                    SyncRemoteOpenGateVisuals();
                    result = removedOpenGate
                        ? $"Applied {RemotePortalPacketCodec.DescribePacketType(packetType)} for owner {openGateRemove.OwnerCharacterId} slot {(openGateRemove.IsFirstSlot ? 1 : 2)}."
                        : $"Ignored {RemotePortalPacketCodec.DescribePacketType(packetType)} for unknown owner {openGateRemove.OwnerCharacterId} slot {(openGateRemove.IsFirstSlot ? 1 : 2)}.";
                    return true;

                default:
                    result = $"Unsupported remote portal packet type {packetType}.";
                    return false;
            }
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
                TemporaryPortalSource.LocalCast,
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

        private void RemovePortalsBySource(TemporaryPortalSource source)
        {
            _portals.RemoveAll(portal => portal.Source == source);
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

        private bool TryResolvePortalDestination(TemporaryPortal portal, out TemporaryPortalDestination destination)
        {
            destination = default;

            if (portal.LinkedPortalId is int linkedPortalId)
            {
                TemporaryPortal target = GetPortalById(linkedPortalId);
                if (target == null)
                    return false;

                destination = new TemporaryPortalDestination(target.MapId, target.X, target.Y, portal.DelayMs);
                return true;
            }

            if (!portal.DirectDestinationMapId.HasValue)
                return false;

            destination = new TemporaryPortalDestination(
                portal.DirectDestinationMapId.Value,
                portal.DirectDestinationX ?? portal.X,
                portal.DirectDestinationY ?? portal.Y,
                portal.DelayMs);
            return true;
        }

        private void ApplyRemoteTownPortalCreate(
            RemoteTownPortalCreatedPacket packet,
            int currentMapId,
            RemoteTownPortalResolvedDestination? destination)
        {
            _remoteTownPortals[packet.OwnerCharacterId] = new RemoteTownPortalState(
                packet.OwnerCharacterId,
                packet.State,
                currentMapId,
                packet.X,
                packet.Y,
                destination);
            SyncRemoteTownPortalVisuals();
        }

        private void ApplyRemoteOpenGateCreate(RemoteOpenGateCreatedPacket packet, int currentMapId)
        {
            _remoteOpenGates[new RemoteOpenGateKey(packet.OwnerCharacterId, packet.IsFirstSlot)] = new RemoteOpenGateState(
                packet.OwnerCharacterId,
                packet.State,
                currentMapId,
                packet.X,
                packet.Y,
                packet.IsFirstSlot,
                packet.PartyId);
            SyncRemoteOpenGateVisuals();
        }

        private void SyncRemoteTownPortalVisuals()
        {
            RemovePortalsBySource(TemporaryPortalSource.RemoteTownPortalPool);

            if (!EnsureMysticDoorVisuals(out PortalVisualSet currentMapVisuals, out PortalVisualSet townVisuals, out PortalVisualSet frameVisuals))
                return;

            foreach (RemoteTownPortalState state in _remoteTownPortals.Values.OrderBy(portal => portal.OwnerCharacterId))
            {
                TemporaryPortal fieldPortal = CreateRemoteTownPortal(
                    state,
                    state.MapId,
                    state.X,
                    state.Y,
                    useTownVisuals: false,
                    currentMapVisuals,
                    townVisuals,
                    frameVisuals);

                _portals.Add(fieldPortal);

                if (!state.Destination.HasValue)
                    continue;

                RemoteTownPortalResolvedDestination destination = state.Destination.Value;
                TemporaryPortal townPortal = CreateRemoteTownPortal(
                    state,
                    destination.MapId,
                    destination.X,
                    destination.Y,
                    useTownVisuals: true,
                    currentMapVisuals,
                    townVisuals,
                    frameVisuals);

                fieldPortal.LinkedPortalId = townPortal.Id;
                townPortal.LinkedPortalId = fieldPortal.Id;

                _portals.Add(townPortal);
            }
        }

        private void SyncRemoteOpenGateVisuals()
        {
            RemovePortalsBySource(TemporaryPortalSource.RemoteOpenGatePool);

            PortalVisualSet visuals = EnsureOpenGateVisuals();
            if (visuals == null)
                return;

            var runtimePortals = new Dictionary<RemoteOpenGateKey, TemporaryPortal>();

            foreach (RemoteOpenGateState state in _remoteOpenGates.Values.OrderBy(portal => portal.OwnerCharacterId).ThenBy(portal => portal.IsFirstSlot ? 0 : 1))
            {
                var portal = new TemporaryPortal(
                    _nextPortalId++,
                    TemporaryPortalKind.OpenGate,
                    TemporaryPortalSource.RemoteOpenGatePool,
                    state.MapId,
                    state.X,
                    state.Y,
                    int.MaxValue,
                    OpenGateTeleportDelayMs,
                    visuals.CreateDrawable(state.X, state.Y))
                {
                    OwnerCharacterId = state.OwnerCharacterId,
                    PartyId = state.PartyId,
                    IsPrimarySlot = state.IsFirstSlot
                };

                _portals.Add(portal);
                runtimePortals[new RemoteOpenGateKey(state.OwnerCharacterId, state.IsFirstSlot)] = portal;
            }

            foreach ((RemoteOpenGateKey key, TemporaryPortal portal) in runtimePortals)
            {
                if (!runtimePortals.TryGetValue(new RemoteOpenGateKey(key.OwnerCharacterId, !key.IsFirstSlot), out TemporaryPortal target))
                    continue;

                portal.LinkedPortalId = target.Id;
            }
        }

        private bool EnsureMysticDoorVisuals(out PortalVisualSet currentMapVisuals, out PortalVisualSet townVisuals, out PortalVisualSet frameVisuals)
        {
            currentMapVisuals = _mysticDoorCurrentMapVisuals;
            townVisuals = _mysticDoorTownVisuals;
            frameVisuals = _mysticDoorFrameVisuals;
            if (currentMapVisuals != null && townVisuals != null && frameVisuals != null)
                return true;

            if (!TryLoadMysticDoorSkillProperty(out WzSubProperty skillProperty))
                return false;

            currentMapVisuals = LoadPortalVisualSet(skillProperty["cDoor"], TemporaryPortalKind.MysticDoor);
            townVisuals = LoadPortalVisualSet(skillProperty["mDoor"], TemporaryPortalKind.MysticDoor);
            frameVisuals = LoadPortalVisualSet(skillProperty["Frame"], TemporaryPortalKind.MysticDoor);
            if (currentMapVisuals == null || townVisuals == null || frameVisuals == null)
                return false;

            _mysticDoorCurrentMapVisuals = currentMapVisuals;
            _mysticDoorTownVisuals = townVisuals;
            _mysticDoorFrameVisuals = frameVisuals;
            return true;
        }

        private TemporaryPortal CreateRemoteTownPortal(
            RemoteTownPortalState state,
            int mapId,
            float x,
            float y,
            bool useTownVisuals,
            PortalVisualSet currentMapVisuals,
            PortalVisualSet townVisuals,
            PortalVisualSet frameVisuals)
        {
            List<BaseDXDrawableItem> drawables = new()
            {
                (useTownVisuals ? townVisuals : currentMapVisuals).CreateDrawable(x, y)
            };

            if (!useTownVisuals && state.State >= RemoteTownPortalOverlayState)
            {
                drawables.Add(frameVisuals.CreateDrawable(x, y));
            }

            return new TemporaryPortal(
                _nextPortalId++,
                TemporaryPortalKind.MysticDoor,
                TemporaryPortalSource.RemoteTownPortalPool,
                mapId,
                x,
                y,
                int.MaxValue,
                CrossMapPortalTeleportDelayMs,
                drawables.ToArray())
            {
                OwnerCharacterId = state.OwnerCharacterId
            };
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

                if (candidate["cDoor"] == null || candidate["mDoor"] == null || candidate["Frame"] == null)
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

        internal readonly struct RemoteTownPortalResolvedDestination
        {
            public RemoteTownPortalResolvedDestination(int mapId, float x, float y)
            {
                MapId = mapId;
                X = x;
                Y = y;
            }

            public int MapId { get; }
            public float X { get; }
            public float Y { get; }
        }

        internal enum TemporaryPortalSource
        {
            LocalCast,
            RemoteTownPortalPool,
            RemoteOpenGatePool
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
            internal TemporaryPortal(
                int id,
                TemporaryPortalKind kind,
                TemporaryPortalSource source,
                int mapId,
                float x,
                float y,
                int expireTime,
                int delayMs,
                params BaseDXDrawableItem[] drawables)
            {
                Id = id;
                Kind = kind;
                Source = source;
                MapId = mapId;
                X = x;
                Y = y;
                ExpireTime = expireTime;
                DelayMs = delayMs;
                Drawables = drawables ?? Array.Empty<BaseDXDrawableItem>();
            }

            public int Id { get; }
            public TemporaryPortalKind Kind { get; }
            internal TemporaryPortalSource Source { get; }
            public int MapId { get; }
            public float X { get; }
            public float Y { get; }
            public int ExpireTime { get; }
            public int DelayMs { get; }
            public IReadOnlyList<BaseDXDrawableItem> Drawables { get; }
            public int? LinkedPortalId { get; set; }
            public int? DirectDestinationMapId { get; set; }
            public float? DirectDestinationX { get; set; }
            public float? DirectDestinationY { get; set; }
            public uint? OwnerCharacterId { get; set; }
            public uint? PartyId { get; set; }
            public bool IsPrimarySlot { get; set; }
        }

        private readonly record struct RemoteOpenGateKey(uint OwnerCharacterId, bool IsFirstSlot);

        private readonly record struct RemoteTownPortalState(
            uint OwnerCharacterId,
            byte State,
            int MapId,
            short X,
            short Y,
            RemoteTownPortalResolvedDestination? Destination);

        private readonly record struct RemoteOpenGateState(
            uint OwnerCharacterId,
            byte State,
            int MapId,
            short X,
            short Y,
            bool IsFirstSlot,
            uint PartyId);

        private static bool IsMysticDoorSkill(SkillCastInfo castInfo)
        {
            return castInfo?.SkillData != null
                   && string.Equals(castInfo.SkillData.Name, MysticDoorSkillName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
