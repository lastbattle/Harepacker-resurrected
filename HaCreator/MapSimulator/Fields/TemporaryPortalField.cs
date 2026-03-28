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
        private const int TownPortalOpeningDurationMs = 1700;
        private const int TownPortalRemovalFadeDurationMs = 1000;
        private const float PortalInteractRangeX = 40f;
        private const float PortalInteractRangeY = 60f;
        private const byte RemoteTownPortalOverlayState = 1;

        private readonly TexturePool _texturePool;
        private readonly GraphicsDevice _graphicsDevice;
        private readonly List<TemporaryPortal> _portals = new();
        private readonly Dictionary<uint, RemoteTownPortalState> _remoteTownPortals = new();
        private readonly Dictionary<RemoteOpenGateKey, RemoteOpenGateState> _remoteOpenGates = new();
        private readonly Dictionary<uint, RemoteTownPortalFieldMetadata> _remoteTownPortalFieldMetadata = new();
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
            bool remoteTownPortalsChanged = AdvanceRemoteTownPortalPhases(currentTime);

            if (_portals.Count == 0)
            {
                if (remoteTownPortalsChanged)
                {
                    SyncRemoteTownPortalVisuals();
                }

                return;
            }

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

            if (remoteTownPortalsChanged)
            {
                SyncRemoteTownPortalVisuals();
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
            int currentTime,
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

                    ApplyRemoteTownPortalCreate(townCreate, currentMapId, currentTime, townPortalDestination);
                    result = $"Applied {RemotePortalPacketCodec.DescribePacketType(packetType)} for owner {townCreate.OwnerCharacterId}.";
                    return true;

                case (int)RemotePortalPacketType.TownPortalRemove:
                    if (!RemotePortalPacketCodec.TryParseTownPortalRemoved(payload, out RemoteTownPortalRemovedPacket townRemove, out string townRemoveError))
                    {
                        result = townRemoveError;
                        return false;
                    }

                    bool removedTownPortal = ApplyRemoteTownPortalRemove(townRemove, currentTime);
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
            int currentTime,
            RemoteTownPortalResolvedDestination? destination)
        {
            if (destination.HasValue)
            {
                RememberRemoteTownPortalFieldMetadata(packet.OwnerCharacterId, currentMapId, packet.X, packet.Y, destination.Value);
            }

            RemoteTownPortalResolvedDestination? resolvedDestination = ResolveRemoteTownPortalDestination(
                packet.OwnerCharacterId,
                currentMapId,
                packet.X,
                packet.Y,
                destination,
                _remoteTownPortals.TryGetValue(packet.OwnerCharacterId, out RemoteTownPortalState existingState) ? existingState : null);

            _remoteTownPortals[packet.OwnerCharacterId] = new RemoteTownPortalState(
                packet.OwnerCharacterId,
                packet.State,
                currentMapId,
                packet.X,
                packet.Y,
                resolvedDestination,
                packet.State == 0 ? RemoteTownPortalVisualPhase.Opening : RemoteTownPortalVisualPhase.Stable,
                currentTime,
                null,
                null);
            SyncRemoteTownPortalVisuals();
        }

        private bool ApplyRemoteTownPortalRemove(RemoteTownPortalRemovedPacket packet, int currentTime)
        {
            if (!_remoteTownPortals.TryGetValue(packet.OwnerCharacterId, out RemoteTownPortalState state))
            {
                return false;
            }

            if (packet.State == 0)
            {
                RemoteTownPortalRemovalSnapshot removalSnapshot = CaptureRemoteTownPortalRemovalSnapshot(state, currentTime);
                _remoteTownPortals[packet.OwnerCharacterId] = state with
                {
                    Phase = RemoteTownPortalVisualPhase.Removing,
                    PhaseStartedAt = currentTime,
                    RemovalState = packet.State,
                    RemovalSnapshot = removalSnapshot
                };
            }
            else
            {
                _remoteTownPortals.Remove(packet.OwnerCharacterId);
            }

            SyncRemoteTownPortalVisuals();
            return true;
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

        private RemoteTownPortalRemovalSnapshot CaptureRemoteTownPortalRemovalSnapshot(RemoteTownPortalState state, int currentTime)
        {
            if (state.RemovalSnapshot != null)
                return state.RemovalSnapshot;

            if (!EnsureMysticDoorVisuals(out PortalVisualSet currentMapVisuals, out PortalVisualSet townVisuals, out PortalVisualSet frameVisuals))
                return null;

            IDXObject fieldMainFrame = ResolveRemoteTownPortalMainFrame(state, useTownVisuals: false, currentTime, currentMapVisuals, townVisuals);
            IDXObject townMainFrame = state.Destination.HasValue
                ? ResolveRemoteTownPortalMainFrame(state, useTownVisuals: true, currentTime, currentMapVisuals, townVisuals)
                : null;
            IDXObject fieldFrameFrame = ShouldDrawRemoteTownPortalFrame(state, useTownVisuals: false)
                ? frameVisuals.GetFrameForTick(currentTime, state.PhaseStartedAt, loop: true)
                : null;

            return new RemoteTownPortalRemovalSnapshot(fieldMainFrame, fieldFrameFrame, townMainFrame);
        }

        private static IDXObject ResolveRemoteTownPortalMainFrame(
            RemoteTownPortalState state,
            bool useTownVisuals,
            int currentTime,
            PortalVisualSet currentMapVisuals,
            PortalVisualSet townVisuals)
        {
            PortalVisualSet mainVisuals = useTownVisuals ? townVisuals : currentMapVisuals;
            bool loop = state.Phase != RemoteTownPortalVisualPhase.Opening || useTownVisuals;
            return mainVisuals.GetFrameForTick(currentTime, state.PhaseStartedAt, loop);
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
            List<BaseDXDrawableItem> drawables = new();
            PortalVisualSet mainVisuals = useTownVisuals ? townVisuals : currentMapVisuals;
            RemoteTownPortalRemovalSnapshot removalSnapshot = state.RemovalSnapshot;

            switch (state.Phase)
            {
                case RemoteTownPortalVisualPhase.Opening when !useTownVisuals:
                    drawables.Add(mainVisuals.CreateOpeningDrawable(x, y, state.PhaseStartedAt));
                    break;

                case RemoteTownPortalVisualPhase.Removing:
                    IDXObject removalMainFrame = useTownVisuals ? removalSnapshot?.TownMainFrame : removalSnapshot?.FieldMainFrame;
                    if (removalMainFrame != null)
                    {
                        drawables.Add(mainVisuals.CreateSnapshotFadeDrawable(x, y, removalMainFrame, state.PhaseStartedAt, TownPortalRemovalFadeDurationMs));
                    }
                    else
                    {
                        drawables.Add(mainVisuals.CreateFadeDrawable(x, y, state.PhaseStartedAt, TownPortalRemovalFadeDurationMs));
                    }
                    break;

                default:
                    drawables.Add(mainVisuals.CreateLoopDrawable(x, y, state.PhaseStartedAt));
                    break;
            }

            bool shouldDrawFrame = ShouldDrawRemoteTownPortalFrame(state, useTownVisuals);

            if (shouldDrawFrame)
            {
                if (state.Phase == RemoteTownPortalVisualPhase.Removing)
                {
                    if (removalSnapshot?.FieldFrameFrame != null)
                    {
                        drawables.Add(frameVisuals.CreateSnapshotFadeDrawable(x, y, removalSnapshot.FieldFrameFrame, state.PhaseStartedAt, TownPortalRemovalFadeDurationMs));
                    }
                    else
                    {
                        drawables.Add(frameVisuals.CreateFadeDrawable(x, y, state.PhaseStartedAt, TownPortalRemovalFadeDurationMs));
                    }
                }
                else
                {
                    drawables.Add(frameVisuals.CreateLoopDrawable(x, y, state.PhaseStartedAt));
                }
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

        private static bool ShouldDrawRemoteTownPortalFrame(RemoteTownPortalState state, bool useTownVisuals)
        {
            return !useTownVisuals
                   && state.Phase != RemoteTownPortalVisualPhase.Opening
                   && state.State >= RemoteTownPortalOverlayState;
        }

        private bool AdvanceRemoteTownPortalPhases(int currentTime)
        {
            if (_remoteTownPortals.Count == 0)
            {
                return false;
            }

            bool changed = false;
            List<uint> ownersToRemove = null;
            List<uint> owners = _remoteTownPortals.Keys.ToList();

            foreach (uint ownerId in owners)
            {
                if (!_remoteTownPortals.TryGetValue(ownerId, out RemoteTownPortalState state))
                {
                    continue;
                }

                switch (state.Phase)
                {
                    case RemoteTownPortalVisualPhase.Opening:
                        if (unchecked(currentTime - state.PhaseStartedAt) >= TownPortalOpeningDurationMs)
                        {
                            _remoteTownPortals[ownerId] = state with
                            {
                                Phase = RemoteTownPortalVisualPhase.Stable,
                                PhaseStartedAt = currentTime
                            };
                            changed = true;
                        }
                        break;

                    case RemoteTownPortalVisualPhase.Removing:
                        if (unchecked(currentTime - state.PhaseStartedAt) >= TownPortalRemovalFadeDurationMs)
                        {
                            ownersToRemove ??= new List<uint>();
                            ownersToRemove.Add(ownerId);
                            changed = true;
                        }
                        break;
                }
            }

            if (ownersToRemove != null)
            {
                foreach (uint ownerId in ownersToRemove)
                {
                    _remoteTownPortals.Remove(ownerId);
                }
            }

            return changed;
        }

        private void RememberRemoteTownPortalFieldMetadata(
            uint ownerCharacterId,
            int sourceMapId,
            short sourceX,
            short sourceY,
            RemoteTownPortalResolvedDestination destination)
        {
            _remoteTownPortalFieldMetadata[ownerCharacterId] = new RemoteTownPortalFieldMetadata(
                sourceMapId,
                sourceX,
                sourceY,
                destination.MapId);
        }

        private RemoteTownPortalResolvedDestination? ResolveRemoteTownPortalDestination(
            uint ownerCharacterId,
            int currentMapId,
            short packetX,
            short packetY,
            RemoteTownPortalResolvedDestination? incomingDestination,
            RemoteTownPortalState? existingState)
        {
            if (_remoteTownPortalFieldMetadata.TryGetValue(ownerCharacterId, out RemoteTownPortalFieldMetadata metadata))
            {
                if (currentMapId == metadata.TownMapId && currentMapId != metadata.SourceMapId)
                {
                    return new RemoteTownPortalResolvedDestination(metadata.SourceMapId, metadata.SourceX, metadata.SourceY);
                }
            }

            if (incomingDestination.HasValue)
            {
                return incomingDestination;
            }

            if (existingState.HasValue && existingState.Value.Destination.HasValue)
            {
                return existingState.Value.Destination.Value;
            }

            return null;
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
            private readonly IDXObject[] _frames;

            public PortalVisualSet(TemporaryPortalKind kind, List<IDXObject> frames)
            {
                Kind = kind;
                _frames = frames?.ToArray() ?? throw new ArgumentNullException(nameof(frames));
            }

            public TemporaryPortalKind Kind { get; }

            public BaseDXDrawableItem CreateDrawable(float x, float y)
            {
                return CreateLoopDrawable(x, y, startedAt: 0);
            }

            public BaseDXDrawableItem CreateLoopDrawable(float x, float y, int startedAt)
            {
                var drawable = new PortalAnimationDrawable(_frames, loop: true, startedAt: startedAt)
                {
                    Position = new Point(-(int)MathF.Round(x), -(int)MathF.Round(y))
                };
                return drawable;
            }

            public BaseDXDrawableItem CreateOpeningDrawable(float x, float y, int startedAt)
            {
                var drawable = new PortalAnimationDrawable(_frames, loop: false, startedAt: startedAt)
                {
                    Position = new Point(-(int)MathF.Round(x), -(int)MathF.Round(y))
                };
                return drawable;
            }

            public BaseDXDrawableItem CreateFadeDrawable(float x, float y, int startedAt, int durationMs)
            {
                var drawable = new PortalAnimationDrawable(_frames, loop: false, startedAt: startedAt, fadeStartedAt: startedAt, fadeDurationMs: durationMs)
                {
                    Position = new Point(-(int)MathF.Round(x), -(int)MathF.Round(y))
                };
                return drawable;
            }

            public BaseDXDrawableItem CreateSnapshotFadeDrawable(float x, float y, IDXObject frame, int startedAt, int durationMs)
            {
                var drawable = new PortalSnapshotFadeDrawable(frame, startedAt, durationMs)
                {
                    Position = new Point(-(int)MathF.Round(x), -(int)MathF.Round(y))
                };
                return drawable;
            }

            public IDXObject GetFrameForTick(int tickCount, int startedAt, bool loop)
            {
                return PortalAnimationDrawable.ResolveFrame(_frames, loop, startedAt, tickCount);
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
            RemoteTownPortalResolvedDestination? Destination,
            RemoteTownPortalVisualPhase Phase,
            int PhaseStartedAt,
            byte? RemovalState,
            RemoteTownPortalRemovalSnapshot RemovalSnapshot);

        private sealed record RemoteTownPortalRemovalSnapshot(
            IDXObject FieldMainFrame,
            IDXObject FieldFrameFrame,
            IDXObject TownMainFrame);

        private readonly record struct RemoteTownPortalFieldMetadata(
            int SourceMapId,
            short SourceX,
            short SourceY,
            int TownMapId);

        private readonly record struct RemoteOpenGateState(
            uint OwnerCharacterId,
            byte State,
            int MapId,
            short X,
            short Y,
            bool IsFirstSlot,
            uint PartyId);

        internal enum RemoteTownPortalVisualPhase
        {
            Opening,
            Stable,
            Removing
        }

        private sealed class PortalAnimationDrawable : BaseDXDrawableItem
        {
            private readonly IDXObject[] _frames;
            private readonly bool _loop;
            private readonly int _startedAt;
            private readonly int _fadeStartedAt;
            private readonly int _fadeDurationMs;

            public PortalAnimationDrawable(IReadOnlyList<IDXObject> frames, bool loop, int startedAt, int fadeStartedAt = -1, int fadeDurationMs = 0)
                : base(frames?.ToList() ?? throw new ArgumentNullException(nameof(frames)), false)
            {
                _frames = frames.ToArray();
                _loop = loop;
                _startedAt = startedAt;
                _fadeStartedAt = fadeStartedAt;
                _fadeDurationMs = fadeDurationMs;
            }

            public override void Draw(
                SpriteBatch sprite,
                SkeletonMeshRenderer skeletonMeshRenderer,
                GameTime gameTime,
                int mapShiftX,
                int mapShiftY,
                int centerX,
                int centerY,
                ReflectionDrawableBoundary drawReflectionInfo,
                RenderParameters renderParameters,
                int TickCount)
            {
                if (_frames.Length == 0)
                {
                    return;
                }

                int shiftCenteredX = mapShiftX - centerX;
                int shiftCenteredY = mapShiftY - centerY;
                IDXObject drawFrame = GetFrameForTick(TickCount);

                if (!IsFrameWithinView(drawFrame, shiftCenteredX, shiftCenteredY, renderParameters.RenderWidth, renderParameters.RenderHeight))
                {
                    return;
                }

                float alpha = GetAlpha(TickCount);
                if (alpha <= 0f)
                {
                    return;
                }

                if (alpha >= 0.999f)
                {
                    drawFrame.DrawObject(
                        sprite,
                        skeletonMeshRenderer,
                        gameTime,
                        shiftCenteredX - Position.X,
                        shiftCenteredY - Position.Y,
                        flip: false,
                        drawReflectionInfo);
                    return;
                }

                int drawX = drawFrame.X - (shiftCenteredX - Position.X);
                int drawY = drawFrame.Y - (shiftCenteredY - Position.Y);
                drawFrame.DrawBackground(
                    sprite,
                    skeletonMeshRenderer,
                    gameTime,
                    drawX,
                    drawY,
                    Color.White * alpha,
                    flip: false,
                    drawReflectionInfo);
            }

            private IDXObject GetFrameForTick(int tickCount)
            {
                return ResolveFrame(_frames, _loop, _startedAt, tickCount);
            }

            internal static IDXObject ResolveFrame(IReadOnlyList<IDXObject> frames, bool loop, int startedAt, int tickCount)
            {
                if (frames == null || frames.Count == 0)
                    throw new ArgumentException("Animation frame collection cannot be empty.", nameof(frames));

                if (frames.Count == 1)
                {
                    return frames[0];
                }

                int elapsed = Math.Max(0, unchecked(tickCount - startedAt));
                int accumulated = 0;

                if (loop)
                {
                    int cycleDuration = 0;
                    for (int i = 0; i < frames.Count; i++)
                    {
                        cycleDuration += Math.Max(1, frames[i].Delay);
                    }

                    if (cycleDuration > 0)
                    {
                        elapsed %= cycleDuration;
                    }
                }

                for (int i = 0; i < frames.Count; i++)
                {
                    accumulated += Math.Max(1, frames[i].Delay);
                    if (elapsed < accumulated)
                    {
                        return frames[i];
                    }
                }

                return frames[^1];
            }

            private float GetAlpha(int tickCount)
            {
                if (_fadeStartedAt < 0 || _fadeDurationMs <= 0)
                {
                    return 1f;
                }

                float progress = Math.Clamp((tickCount - _fadeStartedAt) / (float)_fadeDurationMs, 0f, 1f);
                return 1f - progress;
            }
        }

        private sealed class PortalSnapshotFadeDrawable : BaseDXDrawableItem
        {
            private readonly IDXObject _frame;
            private readonly int _fadeStartedAt;
            private readonly int _fadeDurationMs;

            public PortalSnapshotFadeDrawable(IDXObject frame, int fadeStartedAt, int fadeDurationMs)
                : base(frame ?? throw new ArgumentNullException(nameof(frame)), false)
            {
                _frame = frame;
                _fadeStartedAt = fadeStartedAt;
                _fadeDurationMs = fadeDurationMs;
            }

            public override void Draw(
                SpriteBatch sprite,
                SkeletonMeshRenderer skeletonMeshRenderer,
                GameTime gameTime,
                int mapShiftX,
                int mapShiftY,
                int centerX,
                int centerY,
                ReflectionDrawableBoundary drawReflectionInfo,
                RenderParameters renderParameters,
                int TickCount)
            {
                int shiftCenteredX = mapShiftX - centerX;
                int shiftCenteredY = mapShiftY - centerY;

                if (!IsFrameWithinView(_frame, shiftCenteredX, shiftCenteredY, renderParameters.RenderWidth, renderParameters.RenderHeight))
                {
                    return;
                }

                float alpha = Math.Clamp(1f - ((TickCount - _fadeStartedAt) / (float)_fadeDurationMs), 0f, 1f);
                if (alpha <= 0f)
                {
                    return;
                }

                if (alpha >= 0.999f)
                {
                    _frame.DrawObject(
                        sprite,
                        skeletonMeshRenderer,
                        gameTime,
                        shiftCenteredX - Position.X,
                        shiftCenteredY - Position.Y,
                        flip: false,
                        drawReflectionInfo);
                    return;
                }

                int drawX = _frame.X - (shiftCenteredX - Position.X);
                int drawY = _frame.Y - (shiftCenteredY - Position.Y);
                _frame.DrawBackground(
                    sprite,
                    skeletonMeshRenderer,
                    gameTime,
                    drawX,
                    drawY,
                    Color.White * alpha,
                    flip: false,
                    drawReflectionInfo);
            }
        }

        private static bool IsMysticDoorSkill(SkillCastInfo castInfo)
        {
            return castInfo?.SkillData != null
                   && string.Equals(castInfo.SkillData.Name, MysticDoorSkillName, StringComparison.OrdinalIgnoreCase);
        }

        internal static RemoteTownPortalVisualPhase AdvanceRemoteTownPortalPhaseForTesting(RemoteTownPortalVisualPhase phase, int phaseStartedAt, int currentTime)
        {
            if (phase == RemoteTownPortalVisualPhase.Opening
                && unchecked(currentTime - phaseStartedAt) >= TownPortalOpeningDurationMs)
            {
                return RemoteTownPortalVisualPhase.Stable;
            }

            if (phase == RemoteTownPortalVisualPhase.Removing
                && unchecked(currentTime - phaseStartedAt) >= TownPortalRemovalFadeDurationMs)
            {
                return RemoteTownPortalVisualPhase.Removing;
            }

            return phase;
        }

        internal static RemoteTownPortalResolvedDestination? ResolveRemoteTownPortalDestinationForTesting(
            int currentMapId,
            RemoteTownPortalResolvedDestination? incomingDestination,
            int sourceMapId,
            short sourceX,
            short sourceY,
            int townMapId)
        {
            if (currentMapId == townMapId && currentMapId != sourceMapId)
            {
                return new RemoteTownPortalResolvedDestination(sourceMapId, sourceX, sourceY);
            }

            return incomingDestination;
        }
    }
}
