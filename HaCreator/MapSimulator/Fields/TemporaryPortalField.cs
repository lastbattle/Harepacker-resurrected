using HaCreator;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Loaders;
using HaCreator.MapSimulator.Pools;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
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
        private const int DefaultOpenGateOpeningDurationMs = 1960;
        private const int DefaultOpenGateRemovalDurationMs = 1800;
        private const int DefaultTownPortalOpeningDurationMs = 1800;
        private const int TownPortalRemovalFadeDurationMs = 1000;
        private const float PortalInteractRangeX = 40f;
        private const float PortalInteractRangeY = 60f;
        private const byte RemoteTownPortalOverlayState = 1;

        private readonly TexturePool _texturePool;
        private readonly GraphicsDevice _graphicsDevice;
        private readonly List<TemporaryPortal> _portals = new();
        private readonly Dictionary<uint, RemoteTownPortalState> _remoteTownPortals = new();
        private readonly Dictionary<RemoteOpenGateKey, RemoteOpenGateState> _remoteOpenGates = new();
        private readonly Dictionary<RemoteTownPortalOwnerTownKey, RemoteTownPortalFieldMetadata> _remoteTownPortalFieldMetadata = new();
        private readonly Dictionary<RemoteTownPortalOwnerTownKey, Dictionary<int, Dictionary<RemoteTownPortalObservationSource, RemoteTownPortalOwnerFieldObservation>>> _remoteTownPortalOwnerFieldObservations = new();
        private readonly Dictionary<uint, Dictionary<int, Dictionary<RemoteTownPortalObservationSource, RemoteTownPortalPendingOwnerFieldObservation>>> _remoteTownPortalPendingOwnerFieldObservations = new();
        private readonly Dictionary<uint, RemoteTownPortalRuntimePair> _remoteTownPortalRuntimes = new();
        private readonly Dictionary<RemoteOpenGateKey, RemoteOpenGateRuntime> _remoteOpenGateRuntimes = new();
        private PortalVisualSet _openGateOpeningVisuals;
        private PortalVisualSet _openGateSoloVisuals;
        private PortalVisualSet _openGateLinkedVisuals;
        private PortalVisualSet _openGateRemovalVisuals;
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
            bool remoteOpenGatesChanged = AdvanceRemoteOpenGatePhases(currentTime);

            if (_portals.Count == 0)
            {
                if (remoteTownPortalsChanged)
                {
                    SyncRemoteTownPortalVisuals();
                }

                if (remoteOpenGatesChanged)
                {
                    SyncRemoteOpenGateVisuals();
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

            if (remoteOpenGatesChanged)
            {
                SyncRemoteOpenGateVisuals();
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
            _remoteTownPortalRuntimes.Clear();
            _remoteOpenGateRuntimes.Clear();
            ClearRemoteTownPortalInferenceState();
            RemovePortalsBySource(TemporaryPortalSource.RemoteTownPortalPool);
            RemovePortalsBySource(TemporaryPortalSource.RemoteOpenGatePool);
        }

        private void ClearRemoteTownPortalInferenceState()
        {
            ClearRemoteTownPortalInferenceState(
                _remoteTownPortalFieldMetadata,
                _remoteTownPortalOwnerFieldObservations,
                _remoteTownPortalPendingOwnerFieldObservations);
        }

        public void RememberRemoteTownPortalOwnerFieldObservation(
            uint ownerCharacterId,
            int sourceMapId,
            float sourceX,
            float sourceY,
            RemoteTownPortalResolvedDestination destination,
            int recordedAt,
            RemoteTownPortalObservationSource observationSource = RemoteTownPortalObservationSource.MovementSnapshot)
        {
            if (ownerCharacterId == 0 || sourceMapId <= 0 || destination.MapId <= 0 || sourceMapId == destination.MapId)
            {
                return;
            }

            RememberRemoteTownPortalOwnerFieldObservation(
                ownerCharacterId,
                sourceMapId,
                sourceX,
                sourceY,
                destination.MapId,
                observationSource,
                recordedAt);
        }

        public void RememberPendingRemoteTownPortalOwnerFieldObservation(
            uint ownerCharacterId,
            int sourceMapId,
            float sourceX,
            float sourceY,
            int recordedAt,
            RemoteTownPortalObservationSource observationSource = RemoteTownPortalObservationSource.MovementSnapshot)
        {
            if (ownerCharacterId == 0 || sourceMapId <= 0)
            {
                return;
            }

            UpsertPendingRemoteTownPortalOwnerFieldObservation(
                ownerCharacterId,
                sourceMapId,
                sourceX,
                sourceY,
                observationSource,
                recordedAt);
        }

        private void RefreshActiveRemoteTownPortalDestination(
            uint ownerCharacterId,
            int townMapId,
            int? metadataRecordedAt = null,
            bool allowMetadataRefresh = true)
        {
            if (ownerCharacterId == 0
                || townMapId <= 0
                || !_remoteTownPortals.TryGetValue(ownerCharacterId, out RemoteTownPortalState state)
                || state.MapId != townMapId
                || state.Phase == RemoteTownPortalVisualPhase.Removing)
            {
                return;
            }

            RemoteTownPortalOwnerTownKey key = new(ownerCharacterId, townMapId);
            RemoteTownPortalFieldMetadata? metadata = _remoteTownPortalFieldMetadata.TryGetValue(key, out RemoteTownPortalFieldMetadata metadataValue)
                ? metadataValue
                : null;
            int? preferredSourceMapId = TryResolvePreferredRemoteTownPortalSourceMapId(
                key,
                state,
                metadata,
                out RemoteTownPortalOwnerFieldObservation? ownerObservation,
                out int? resolvedPreferredSourceMapId)
                ? resolvedPreferredSourceMapId
                : null;
            RemoteTownPortalResolvedDestination? refreshedDestination = ResolveRemoteTownPortalDestination(
                townMapId,
                incomingDestination: null,
                existingState: state,
                metadata,
                ownerObservation,
                preferredSourceMapId);
            if (allowMetadataRefresh
                && metadataRecordedAt.HasValue
                && refreshedDestination.HasValue
                && ShouldRefreshRemoteTownPortalInferredMetadata(metadata, ownerObservation, refreshedDestination.Value))
            {
                UpsertRemoteTownPortalObservedFieldMetadata(
                    ownerCharacterId,
                    townMapId,
                    refreshedDestination.Value,
                    metadataRecordedAt.Value,
                    ownerObservation.Value.ObservationSource,
                    refreshActiveDestination: false);
                metadata = _remoteTownPortalFieldMetadata.TryGetValue(key, out metadataValue)
                    ? metadataValue
                    : null;
            }

            if (AreEquivalentRemoteTownPortalDestinations(state.Destination, refreshedDestination))
            {
                return;
            }

            _remoteTownPortals[ownerCharacterId] = state with
            {
                Destination = refreshedDestination
            };
            SyncRemoteTownPortalVisuals();
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

                    ApplyRemoteOpenGateCreate(openGateCreate, currentMapId, currentTime);
                    result = $"Applied {RemotePortalPacketCodec.DescribePacketType(packetType)} for owner {openGateCreate.OwnerCharacterId} slot {(openGateCreate.IsFirstSlot ? 1 : 2)}.";
                    return true;

                case (int)RemotePortalPacketType.OpenGateRemove:
                    if (!RemotePortalPacketCodec.TryParseOpenGateRemoved(payload, out RemoteOpenGateRemovedPacket openGateRemove, out string openGateRemoveError))
                    {
                        result = openGateRemoveError;
                        return false;
                    }

                    bool removedOpenGate = ApplyRemoteOpenGateRemove(openGateRemove, currentTime);
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

            if (!EnsureOpenGateVisuals(out _, out _, out PortalVisualSet visuals, out _))
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

                destination = new TemporaryPortalDestination(target.MapId, target.X, target.Y, portal.DelayMs, portal.X, portal.Y);
                return true;
            }

            if (!portal.DirectDestinationMapId.HasValue)
                return false;

            destination = new TemporaryPortalDestination(
                portal.DirectDestinationMapId.Value,
                portal.DirectDestinationX ?? portal.X,
                portal.DirectDestinationY ?? portal.Y,
                portal.DelayMs,
                portal.X,
                portal.Y);
            return true;
        }

        private void ApplyRemoteTownPortalCreate(
            RemoteTownPortalCreatedPacket packet,
            int currentMapId,
            int currentTime,
            RemoteTownPortalResolvedDestination? destination)
        {
            destination = NormalizeRemoteTownPortalIncomingDestination(currentMapId, destination);
            AdoptPendingRemoteTownPortalOwnerFieldObservations(packet.OwnerCharacterId, currentMapId);
            RemoteTownPortalState? existingState = _remoteTownPortals.TryGetValue(packet.OwnerCharacterId, out RemoteTownPortalState existingStateValue)
                ? existingStateValue
                : null;
            RemoteTownPortalState? townScopedExistingState = FilterRemoteTownPortalStateByTownMap(existingState, currentMapId);
            int? preferredSourceMapId = ResolveRemoteTownPortalPreferredSourceMapId(packet.OwnerCharacterId, currentMapId, townScopedExistingState);
            if (destination.HasValue)
            {
                RememberRemoteTownPortalFieldMetadata(packet.OwnerCharacterId, currentMapId, packet.X, packet.Y, destination.Value, currentTime);
                AdoptPendingRemoteTownPortalOwnerFieldObservations(packet.OwnerCharacterId, destination.Value.MapId);
                preferredSourceMapId = ResolveRemoteTownPortalPreferredSourceMapId(packet.OwnerCharacterId, currentMapId, townScopedExistingState);
            }

            RemoteTownPortalResolvedDestination? resolvedDestination = ResolveRemoteTownPortalDestination(
                packet.OwnerCharacterId,
                currentMapId,
                destination,
                townScopedExistingState);
            RemoteTownPortalDestinationResolutionKind resolutionKind = resolvedDestination.HasValue
                ? RemoteTownPortalDestinationResolutionKind.OwnerOrCachedObservation
                : RemoteTownPortalDestinationResolutionKind.None;
            if (!resolvedDestination.HasValue
                && TryResolveRemoteTownPortalCurrentMapReturnDestination(currentMapId, out RemoteTownPortalResolvedDestination sourceMapFallbackDestination))
            {
                resolvedDestination = sourceMapFallbackDestination;
                resolutionKind = RemoteTownPortalDestinationResolutionKind.SourceMapReturnFallback;
                RememberRemoteTownPortalObservedFieldMetadata(
                    packet.OwnerCharacterId,
                    currentMapId,
                    sourceMapFallbackDestination,
                    currentTime,
                    RemoteTownPortalObservationSource.WzReturnMapFallback);
            }

            if (!resolvedDestination.HasValue
                && TryResolveRemoteTownPortalWzFallbackDestination(currentMapId, preferredSourceMapId, out RemoteTownPortalResolvedDestination fallbackDestination))
            {
                resolvedDestination = fallbackDestination;
                resolutionKind = RemoteTownPortalDestinationResolutionKind.WzPreferredOrUniqueFallback;
                RememberRemoteTownPortalObservedFieldMetadata(
                    packet.OwnerCharacterId,
                    currentMapId,
                    fallbackDestination,
                    currentTime,
                    RemoteTownPortalObservationSource.WzReturnMapFallback);
            }

            if (!destination.HasValue
                && resolvedDestination.HasValue
                && ShouldRememberRemoteTownPortalPacketCastMetadataFromResolvedDestination(
                    resolutionKind,
                    currentMapId,
                    resolvedDestination.Value))
            {
                RememberRemoteTownPortalFieldMetadata(
                    packet.OwnerCharacterId,
                    currentMapId,
                    packet.X,
                    packet.Y,
                    resolvedDestination.Value,
                    currentTime);
                AdoptPendingRemoteTownPortalOwnerFieldObservations(
                    packet.OwnerCharacterId,
                    resolvedDestination.Value.MapId);
            }

            if (!destination.HasValue
                && resolvedDestination.HasValue
                && resolutionKind == RemoteTownPortalDestinationResolutionKind.OwnerOrCachedObservation)
            {
                RemoteTownPortalObservationSource observationSource = ResolveRemoteTownPortalResolvedDestinationObservationSource(
                    packet.OwnerCharacterId,
                    currentMapId,
                    townScopedExistingState,
                    resolvedDestination.Value);
                RememberRemoteTownPortalObservedFieldMetadata(
                    packet.OwnerCharacterId,
                    currentMapId,
                    resolvedDestination.Value,
                    currentTime,
                    observationSource);
            }

            RemoteTownPortalVisualPhase phase = ResolveRemoteTownPortalCreatePhase(packet.State, townScopedExistingState);
            byte resolvedState = ResolveRemoteTownPortalCreateState(packet.State, townScopedExistingState);
            _remoteTownPortals[packet.OwnerCharacterId] = new RemoteTownPortalState(
                packet.OwnerCharacterId,
                resolvedState,
                currentMapId,
                packet.X,
                packet.Y,
                resolvedDestination,
                phase,
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

        private void ApplyRemoteOpenGateCreate(RemoteOpenGateCreatedPacket packet, int currentMapId, int currentTime)
        {
            RemoteOpenGateKey key = new(packet.OwnerCharacterId, packet.IsFirstSlot);
            RemoteOpenGateState? existingState = _remoteOpenGates.TryGetValue(key, out RemoteOpenGateState existingStateValue)
                ? existingStateValue
                : null;
            (byte resolvedState, RemoteOpenGateVisualPhase phase, int phaseStartedAt) = ResolveRemoteOpenGateCreateLifecycle(
                packet.State,
                existingState,
                currentTime);

            _remoteOpenGates[key] = new RemoteOpenGateState(
                packet.OwnerCharacterId,
                resolvedState,
                currentMapId,
                packet.X,
                packet.Y,
                packet.IsFirstSlot,
                packet.PartyId,
                phase,
                phaseStartedAt);

            if (phase == RemoteOpenGateVisualPhase.Stable)
            {
                PromoteRemoteOpenGatePartnerAfterPartnerCreate(packet.OwnerCharacterId, packet.IsFirstSlot, currentTime);
            }

            SyncRemoteOpenGateVisuals();
        }

        private static (byte ResolvedState, RemoteOpenGateVisualPhase Phase, int PhaseStartedAt) ResolveRemoteOpenGateCreateLifecycle(
            byte packetState,
            RemoteOpenGateState? existingState,
            int currentTime)
        {
            if (!existingState.HasValue)
            {
                return packetState == 0
                    ? (packetState, RemoteOpenGateVisualPhase.Opening, currentTime)
                    : (packetState, RemoteOpenGateVisualPhase.Stable, currentTime);
            }

            RemoteOpenGateState existing = existingState.Value;
            if (existing.Phase == RemoteOpenGateVisualPhase.Removing)
            {
                if (packetState == 0 && existing.State != 0)
                {
                    byte stableState = existing.State == 0 ? (byte)1 : existing.State;
                    return (stableState, RemoteOpenGateVisualPhase.Stable, existing.PhaseStartedAt);
                }

                return packetState == 0
                    ? (packetState, RemoteOpenGateVisualPhase.Opening, currentTime)
                    : (packetState, RemoteOpenGateVisualPhase.Stable, currentTime);
            }

            if (packetState == 0)
            {
                if (existing.Phase == RemoteOpenGateVisualPhase.Stable || existing.State != 0)
                {
                    byte stableState = existing.State == 0 ? (byte)1 : existing.State;
                    return (stableState, RemoteOpenGateVisualPhase.Stable, existing.PhaseStartedAt);
                }

                return (packetState, RemoteOpenGateVisualPhase.Opening, existing.PhaseStartedAt);
            }

            return existing.Phase == RemoteOpenGateVisualPhase.Stable
                ? (packetState, RemoteOpenGateVisualPhase.Stable, existing.PhaseStartedAt)
                : (packetState, RemoteOpenGateVisualPhase.Stable, currentTime);
        }

        private bool ApplyRemoteOpenGateRemove(RemoteOpenGateRemovedPacket packet, int currentTime)
        {
            RemoteOpenGateKey key = new(packet.OwnerCharacterId, packet.IsFirstSlot);
            if (!_remoteOpenGates.TryGetValue(key, out RemoteOpenGateState state))
            {
                return false;
            }

            if (packet.State == 0)
            {
                _remoteOpenGates[key] = state with
                {
                    Phase = RemoteOpenGateVisualPhase.Removing,
                    PhaseStartedAt = currentTime
                };
            }
            else
            {
                _remoteOpenGates.Remove(key);
            }

            RestartRemoteOpenGatePartnerAfterPartnerLoss(packet.OwnerCharacterId, packet.IsFirstSlot, currentTime);
            SyncRemoteOpenGateVisuals();
            return true;
        }

        private void RestartRemoteOpenGatePartnerAfterPartnerLoss(uint ownerCharacterId, bool removedFirstSlot, int currentTime)
        {
            RemoteOpenGateKey partnerKey = new(ownerCharacterId, !removedFirstSlot);
            if (!_remoteOpenGates.TryGetValue(partnerKey, out RemoteOpenGateState partner))
            {
                return;
            }

            (RemoteOpenGateVisualPhase phase, int phaseStartedAt) = ResolveRemoteOpenGatePartnerPromotion(partner, currentTime);
            if (phase == partner.Phase && phaseStartedAt == partner.PhaseStartedAt)
            {
                return;
            }

            _remoteOpenGates[partnerKey] = partner with
            {
                Phase = phase,
                PhaseStartedAt = phaseStartedAt
            };
        }

        private void PromoteRemoteOpenGatePartnerAfterPartnerCreate(uint ownerCharacterId, bool createdFirstSlot, int currentTime)
        {
            RemoteOpenGateKey partnerKey = new(ownerCharacterId, !createdFirstSlot);
            if (!_remoteOpenGates.TryGetValue(partnerKey, out RemoteOpenGateState partner))
            {
                return;
            }

            (RemoteOpenGateVisualPhase phase, int phaseStartedAt) = ResolveRemoteOpenGatePartnerPromotion(partner, currentTime);
            if (phase == partner.Phase && phaseStartedAt == partner.PhaseStartedAt)
            {
                return;
            }

            _remoteOpenGates[partnerKey] = partner with
            {
                Phase = phase,
                PhaseStartedAt = phaseStartedAt
            };
        }

        private static (RemoteOpenGateVisualPhase Phase, int PhaseStartedAt) ResolveRemoteOpenGatePartnerPromotion(
            RemoteOpenGateState partner,
            int currentTime)
        {
            if (partner.Phase == RemoteOpenGateVisualPhase.Removing)
            {
                return (partner.Phase, partner.PhaseStartedAt);
            }

            if (partner.Phase == RemoteOpenGateVisualPhase.Stable)
            {
                return (partner.Phase, partner.PhaseStartedAt);
            }

            return (RemoteOpenGateVisualPhase.Stable, currentTime);
        }

        private void SyncRemoteTownPortalVisuals()
        {
            if (!EnsureMysticDoorVisuals(out PortalVisualSet currentMapVisuals, out PortalVisualSet townVisuals, out PortalVisualSet frameVisuals))
                return;

            HashSet<uint> activeOwners = new();

            foreach (RemoteTownPortalState state in _remoteTownPortals.Values.OrderBy(portal => portal.OwnerCharacterId))
            {
                activeOwners.Add(state.OwnerCharacterId);
                _remoteTownPortalRuntimes.TryGetValue(state.OwnerCharacterId, out RemoteTownPortalRuntimePair runtime);

                RemoteTownPortalRuntime fieldRuntime = UpsertRemoteTownPortalRuntime(
                    runtime?.FieldRuntime,
                    state,
                    state.MapId,
                    state.X,
                    state.Y,
                    useTownVisuals: false,
                    currentMapVisuals,
                    townVisuals,
                    frameVisuals);
                TemporaryPortal fieldPortal = fieldRuntime.Portal;

                if (!state.Destination.HasValue)
                {
                    fieldPortal.LinkedPortalId = null;
                    RemoveRemoteTownPortalRuntime(runtime?.TownRuntime);
                    _remoteTownPortalRuntimes[state.OwnerCharacterId] = new RemoteTownPortalRuntimePair(fieldRuntime, null);
                    continue;
                }

                RemoteTownPortalResolvedDestination destination = state.Destination.Value;
                RemoteTownPortalRuntime townRuntime = UpsertRemoteTownPortalRuntime(
                    runtime?.TownRuntime,
                    state,
                    destination.MapId,
                    destination.X,
                    destination.Y,
                    useTownVisuals: true,
                    currentMapVisuals,
                    townVisuals,
                    frameVisuals);
                TemporaryPortal townPortal = townRuntime.Portal;

                if (ShouldLinkRemoteTownPortal(state))
                {
                    fieldPortal.LinkedPortalId = townPortal.Id;
                    townPortal.LinkedPortalId = fieldPortal.Id;
                }
                else
                {
                    fieldPortal.LinkedPortalId = null;
                    townPortal.LinkedPortalId = null;
                }

                _remoteTownPortalRuntimes[state.OwnerCharacterId] = new RemoteTownPortalRuntimePair(fieldRuntime, townRuntime);
            }

            foreach (uint ownerId in _remoteTownPortalRuntimes.Keys.Except(activeOwners).ToArray())
            {
                if (_remoteTownPortalRuntimes.TryGetValue(ownerId, out RemoteTownPortalRuntimePair runtime))
                {
                    RemoveRemoteTownPortalRuntime(runtime.FieldRuntime);
                    RemoveRemoteTownPortalRuntime(runtime.TownRuntime);
                }

                _remoteTownPortalRuntimes.Remove(ownerId);
            }
        }

        private RemoteTownPortalRuntime UpsertRemoteTownPortalRuntime(
            RemoteTownPortalRuntime runtime,
            RemoteTownPortalState state,
            int mapId,
            float x,
            float y,
            bool useTownVisuals,
            PortalVisualSet currentMapVisuals,
            PortalVisualSet townVisuals,
            PortalVisualSet frameVisuals)
        {
            if (runtime == null)
            {
                BaseDXDrawableItem mainDrawable = CreateRemoteTownPortalMainDrawable(state, x, y, useTownVisuals, currentMapVisuals, townVisuals);
                BaseDXDrawableItem frameDrawable = CreateRemoteTownPortalFrameDrawable(state, x, y, useTownVisuals, frameVisuals);
                var mainSlot = new PortalDrawableSlot(mainDrawable, x, y);
                PortalDrawableSlot frameSlot = null;
                if (!useTownVisuals)
                {
                    frameSlot = new PortalDrawableSlot(frameDrawable ?? mainDrawable, x, y);
                    frameSlot.SetDrawable(frameDrawable);
                }
                var createdPortal = new TemporaryPortal(
                    _nextPortalId++,
                    TemporaryPortalKind.MysticDoor,
                    TemporaryPortalSource.RemoteTownPortalPool,
                    mapId,
                    x,
                    y,
                    int.MaxValue,
                    CrossMapPortalTeleportDelayMs,
                    frameSlot == null
                        ? new BaseDXDrawableItem[] { mainSlot }
                        : new BaseDXDrawableItem[] { mainSlot, frameSlot })
                {
                    OwnerCharacterId = state.OwnerCharacterId
                };
                _portals.Add(createdPortal);
                return new RemoteTownPortalRuntime(createdPortal, mainSlot, frameSlot);
            }

            TemporaryPortal portal = runtime.Portal;
            portal.MapId = mapId;
            portal.X = x;
            portal.Y = y;
            portal.OwnerCharacterId = state.OwnerCharacterId;
            runtime.MainSlot.SetPosition(x, y);
            runtime.MainSlot.SetDrawable(CreateRemoteTownPortalMainDrawable(state, x, y, useTownVisuals, currentMapVisuals, townVisuals));
            if (runtime.FrameSlot != null)
            {
                runtime.FrameSlot.SetPosition(x, y);
                runtime.FrameSlot.SetDrawable(CreateRemoteTownPortalFrameDrawable(state, x, y, useTownVisuals, frameVisuals));
            }

            return runtime;
        }

        private void RemoveRemoteTownPortalRuntime(RemoteTownPortalRuntime runtime)
        {
            if (runtime?.Portal == null)
                return;

            RemovePortal(runtime.Portal);
        }

        private void SyncRemoteOpenGateVisuals()
        {
            if (!EnsureOpenGateVisuals(out PortalVisualSet openingVisuals, out PortalVisualSet soloVisuals, out PortalVisualSet linkedVisuals, out PortalVisualSet removalVisuals))
                return;

            HashSet<RemoteOpenGateKey> activeKeys = new();

            foreach (RemoteOpenGateState state in _remoteOpenGates.Values.OrderBy(portal => portal.OwnerCharacterId).ThenBy(portal => portal.IsFirstSlot ? 0 : 1))
            {
                RemoteOpenGateKey key = new(state.OwnerCharacterId, state.IsFirstSlot);
                activeKeys.Add(key);
                _remoteOpenGateRuntimes.TryGetValue(key, out RemoteOpenGateRuntime runtime);
                bool hasPartner = _remoteOpenGates.TryGetValue(new RemoteOpenGateKey(state.OwnerCharacterId, !state.IsFirstSlot), out RemoteOpenGateState partner)
                                  && HasRemoteOpenGateSameMapPartner(state.MapId, partner.MapId, partner.Phase);
                bool hasStablePartner = hasPartner && partner.Phase == RemoteOpenGateVisualPhase.Stable;
                _remoteOpenGateRuntimes[key] = UpsertRemoteOpenGateRuntime(
                    runtime,
                    state,
                    hasStablePartner,
                    openingVisuals,
                    soloVisuals,
                    linkedVisuals,
                    removalVisuals);
            }

            foreach (RemoteOpenGateKey staleKey in _remoteOpenGateRuntimes.Keys.Except(activeKeys).ToArray())
            {
                RemovePortal(_remoteOpenGateRuntimes[staleKey].Portal);
                _remoteOpenGateRuntimes.Remove(staleKey);
            }

            foreach ((RemoteOpenGateKey key, RemoteOpenGateRuntime runtime) in _remoteOpenGateRuntimes)
            {
                RemoteOpenGateKey targetKey = new(key.OwnerCharacterId, !key.IsFirstSlot);
                bool hasTargetState = _remoteOpenGates.TryGetValue(targetKey, out RemoteOpenGateState targetState);
                runtime.Portal.LinkedPortalId = _remoteOpenGates.TryGetValue(key, out RemoteOpenGateState state)
                    && ShouldLinkRemoteOpenGatePortal(state.Phase, hasTargetState, targetState.Phase)
                    && HasRemoteOpenGateSameMapPartner(state.MapId, targetState.MapId, targetState.Phase)
                    && _remoteOpenGateRuntimes.TryGetValue(targetKey, out RemoteOpenGateRuntime target)
                    ? target.Portal.Id
                    : null;
            }
        }

        private static RemoteTownPortalState? FilterRemoteTownPortalStateByTownMap(
            RemoteTownPortalState? existingState,
            int townMapId)
        {
            if (!existingState.HasValue
                || existingState.Value.MapId != townMapId
                || existingState.Value.Phase == RemoteTownPortalVisualPhase.Removing)
            {
                return null;
            }

            return existingState;
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

        private int GetTownPortalOpeningDurationMs()
        {
            return _mysticDoorCurrentMapVisuals?.DurationMs > 0
                ? _mysticDoorCurrentMapVisuals.DurationMs
                : DefaultTownPortalOpeningDurationMs;
        }

        private BaseDXDrawableItem[] CreateRemoteTownPortalDrawables(
            RemoteTownPortalState state,
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

            return drawables.ToArray();
        }

        private BaseDXDrawableItem CreateRemoteTownPortalMainDrawable(
            RemoteTownPortalState state,
            float x,
            float y,
            bool useTownVisuals,
            PortalVisualSet currentMapVisuals,
            PortalVisualSet townVisuals)
        {
            PortalVisualSet mainVisuals = useTownVisuals ? townVisuals : currentMapVisuals;
            RemoteTownPortalRemovalSnapshot removalSnapshot = state.RemovalSnapshot;

            return state.Phase switch
            {
                RemoteTownPortalVisualPhase.Opening when !useTownVisuals => mainVisuals.CreateOpeningDrawable(x, y, state.PhaseStartedAt),
                RemoteTownPortalVisualPhase.Removing => (useTownVisuals ? removalSnapshot?.TownMainFrame : removalSnapshot?.FieldMainFrame) is IDXObject removalMainFrame
                    ? mainVisuals.CreateSnapshotFadeDrawable(x, y, removalMainFrame, state.PhaseStartedAt, TownPortalRemovalFadeDurationMs)
                    : mainVisuals.CreateFadeDrawable(x, y, state.PhaseStartedAt, TownPortalRemovalFadeDurationMs),
                _ => mainVisuals.CreateLoopDrawable(x, y, state.PhaseStartedAt)
            };
        }

        private BaseDXDrawableItem CreateRemoteTownPortalFrameDrawable(
            RemoteTownPortalState state,
            float x,
            float y,
            bool useTownVisuals,
            PortalVisualSet frameVisuals)
        {
            if (!ShouldDrawRemoteTownPortalFrame(state, useTownVisuals))
            {
                return null;
            }

            if (state.Phase == RemoteTownPortalVisualPhase.Removing)
            {
                return state.RemovalSnapshot?.FieldFrameFrame is IDXObject removalFrame
                    ? frameVisuals.CreateSnapshotFadeDrawable(x, y, removalFrame, state.PhaseStartedAt, TownPortalRemovalFadeDurationMs)
                    : frameVisuals.CreateFadeDrawable(x, y, state.PhaseStartedAt, TownPortalRemovalFadeDurationMs);
            }

            return frameVisuals.CreateLoopDrawable(x, y, state.PhaseStartedAt);
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
                        if (unchecked(currentTime - state.PhaseStartedAt) >= GetTownPortalOpeningDurationMs())
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

        private bool AdvanceRemoteOpenGatePhases(int currentTime)
        {
            if (_remoteOpenGates.Count == 0)
            {
                return false;
            }

            bool changed = false;
            List<RemoteOpenGateKey> keysToRemove = null;
            List<RemoteOpenGateKey> keys = _remoteOpenGates.Keys.ToList();

            foreach (RemoteOpenGateKey key in keys)
            {
                if (!_remoteOpenGates.TryGetValue(key, out RemoteOpenGateState state))
                {
                    continue;
                }

                switch (state.Phase)
                {
                    case RemoteOpenGateVisualPhase.Opening:
                        if (unchecked(currentTime - state.PhaseStartedAt) >= GetOpenGateOpeningDurationMs())
                        {
                            _remoteOpenGates[key] = state with
                            {
                                Phase = RemoteOpenGateVisualPhase.Stable,
                                PhaseStartedAt = currentTime
                            };
                            changed = true;
                        }
                        break;

                    case RemoteOpenGateVisualPhase.Removing:
                        if (unchecked(currentTime - state.PhaseStartedAt) >= GetOpenGateRemovalDurationMs())
                        {
                            keysToRemove ??= new List<RemoteOpenGateKey>();
                            keysToRemove.Add(key);
                            changed = true;
                        }
                        break;
                }
            }

            if (keysToRemove != null)
            {
                foreach (RemoteOpenGateKey key in keysToRemove)
                {
                    _remoteOpenGates.Remove(key);
                }
            }

            return changed;
        }

        private void RememberRemoteTownPortalFieldMetadata(
            uint ownerCharacterId,
            int sourceMapId,
            short sourceX,
            short sourceY,
            RemoteTownPortalResolvedDestination destination,
            int recordedAt)
        {
            RemoteTownPortalOwnerTownKey key = new(ownerCharacterId, destination.MapId);
            _remoteTownPortalFieldMetadata[key] = new RemoteTownPortalFieldMetadata(
                sourceMapId,
                sourceX,
                sourceY,
                destination.MapId,
                RemoteTownPortalObservationSource.PacketCast,
                recordedAt);
            UpsertRemoteTownPortalOwnerFieldObservation(
                ownerCharacterId,
                sourceMapId,
                sourceX,
                sourceY,
                destination.MapId,
                RemoteTownPortalObservationSource.PacketCast,
                recordedAt,
                refreshActiveDestination: false);
            RefreshActiveRemoteTownPortalDestination(ownerCharacterId, destination.MapId, recordedAt);
        }

        private void RememberRemoteTownPortalObservedFieldMetadata(
            uint ownerCharacterId,
            int townMapId,
            RemoteTownPortalResolvedDestination resolvedDestination,
            int recordedAt,
            RemoteTownPortalObservationSource observationSource)
        {
            UpsertRemoteTownPortalObservedFieldMetadata(
                ownerCharacterId,
                townMapId,
                resolvedDestination,
                recordedAt,
                observationSource,
                refreshActiveDestination: true);
        }

        private RemoteTownPortalObservationSource ResolveRemoteTownPortalResolvedDestinationObservationSource(
            uint ownerCharacterId,
            int townMapId,
            RemoteTownPortalState? existingState,
            RemoteTownPortalResolvedDestination resolvedDestination)
        {
            RemoteTownPortalOwnerTownKey key = new(ownerCharacterId, townMapId);
            RemoteTownPortalFieldMetadata? metadata = _remoteTownPortalFieldMetadata.TryGetValue(key, out RemoteTownPortalFieldMetadata metadataValue)
                ? metadataValue
                : null;
            if (!TryResolvePreferredRemoteTownPortalSourceMapId(
                    key,
                    existingState,
                    metadata,
                    out RemoteTownPortalOwnerFieldObservation? ownerObservation,
                    out _)
                || !ownerObservation.HasValue)
            {
                return RemoteTownPortalObservationSource.InferredSourceField;
            }

            RemoteTownPortalOwnerFieldObservation observation = ownerObservation.Value;
            return observation.SourceMapId == resolvedDestination.MapId
                && Math.Abs(observation.SourceX - resolvedDestination.X) < 0.01f
                && Math.Abs(observation.SourceY - resolvedDestination.Y) < 0.01f
                    ? observation.ObservationSource
                    : RemoteTownPortalObservationSource.InferredSourceField;
        }

        private void UpsertRemoteTownPortalObservedFieldMetadata(
            uint ownerCharacterId,
            int townMapId,
            RemoteTownPortalResolvedDestination resolvedDestination,
            int recordedAt,
            RemoteTownPortalObservationSource observationSource,
            bool refreshActiveDestination)
        {
            if (ownerCharacterId == 0
                || townMapId <= 0
                || resolvedDestination.MapId <= 0
                || townMapId == resolvedDestination.MapId)
            {
                return;
            }

            RemoteTownPortalOwnerTownKey key = new(ownerCharacterId, townMapId);
            if (_remoteTownPortalFieldMetadata.TryGetValue(key, out RemoteTownPortalFieldMetadata existingMetadata))
            {
                if (existingMetadata.ObservationSource == RemoteTownPortalObservationSource.PacketCast)
                {
                    return;
                }

                if (existingMetadata.SourceMapId == resolvedDestination.MapId
                    && Math.Abs(existingMetadata.SourceX - resolvedDestination.X) < 0.01f
                    && Math.Abs(existingMetadata.SourceY - resolvedDestination.Y) < 0.01f
                    && CompareRemoteTownPortalSameSourceCoordinateAuthority(
                        existingMetadata.ObservationSource,
                        existingMetadata.RecordedAt,
                        observationSource,
                        recordedAt) >= 0)
                {
                    return;
                }
            }

            _remoteTownPortalFieldMetadata[key] = new RemoteTownPortalFieldMetadata(
                resolvedDestination.MapId,
                resolvedDestination.X,
                resolvedDestination.Y,
                townMapId,
                observationSource,
                recordedAt);
            UpsertRemoteTownPortalOwnerFieldObservation(
                ownerCharacterId,
                resolvedDestination.MapId,
                resolvedDestination.X,
                resolvedDestination.Y,
                townMapId,
                observationSource,
                recordedAt,
                refreshActiveDestination: false);
            if (refreshActiveDestination)
            {
                RefreshActiveRemoteTownPortalDestination(ownerCharacterId, townMapId, recordedAt, allowMetadataRefresh: false);
            }
        }

        private void RememberRemoteTownPortalOwnerFieldObservation(
            uint ownerCharacterId,
            int sourceMapId,
            float sourceX,
            float sourceY,
            int townMapId,
            RemoteTownPortalObservationSource observationSource,
            int recordedAt)
        {
            UpsertRemoteTownPortalOwnerFieldObservation(
                ownerCharacterId,
                sourceMapId,
                sourceX,
                sourceY,
                townMapId,
                observationSource,
                recordedAt,
                refreshActiveDestination: true);
        }

        private void UpsertRemoteTownPortalOwnerFieldObservation(
            uint ownerCharacterId,
            int sourceMapId,
            float sourceX,
            float sourceY,
            int townMapId,
            RemoteTownPortalObservationSource observationSource,
            int recordedAt,
            bool refreshActiveDestination)
        {
            RemoteTownPortalOwnerTownKey key = new(ownerCharacterId, townMapId);
            if (!_remoteTownPortalOwnerFieldObservations.TryGetValue(key, out Dictionary<int, Dictionary<RemoteTownPortalObservationSource, RemoteTownPortalOwnerFieldObservation>> observationsBySourceMap))
            {
                observationsBySourceMap = new Dictionary<int, Dictionary<RemoteTownPortalObservationSource, RemoteTownPortalOwnerFieldObservation>>();
                _remoteTownPortalOwnerFieldObservations[key] = observationsBySourceMap;
            }

            if (!observationsBySourceMap.TryGetValue(sourceMapId, out Dictionary<RemoteTownPortalObservationSource, RemoteTownPortalOwnerFieldObservation> observationsBySource))
            {
                observationsBySource = new Dictionary<RemoteTownPortalObservationSource, RemoteTownPortalOwnerFieldObservation>();
                observationsBySourceMap[sourceMapId] = observationsBySource;
            }

            if (observationsBySource.TryGetValue(observationSource, out RemoteTownPortalOwnerFieldObservation existingObservation)
                && !ShouldReplaceRemoteTownPortalOwnerObservation(
                    existingObservation.SourceMapId,
                    existingObservation.TownMapId,
                    existingObservation.ObservationSource,
                    existingObservation.RecordedAt,
                    sourceMapId,
                    townMapId,
                    observationSource,
                    recordedAt))
            {
                return;
            }

            observationsBySource[observationSource] = new RemoteTownPortalOwnerFieldObservation(
                sourceMapId,
                sourceX,
                sourceY,
                townMapId,
                observationSource,
                recordedAt);
            if (refreshActiveDestination)
            {
                RefreshActiveRemoteTownPortalDestination(ownerCharacterId, townMapId, recordedAt);
            }
        }

        private void UpsertPendingRemoteTownPortalOwnerFieldObservation(
            uint ownerCharacterId,
            int sourceMapId,
            float sourceX,
            float sourceY,
            RemoteTownPortalObservationSource observationSource,
            int recordedAt)
        {
            if (!_remoteTownPortalPendingOwnerFieldObservations.TryGetValue(ownerCharacterId, out Dictionary<int, Dictionary<RemoteTownPortalObservationSource, RemoteTownPortalPendingOwnerFieldObservation>> observationsBySourceMap))
            {
                observationsBySourceMap = new Dictionary<int, Dictionary<RemoteTownPortalObservationSource, RemoteTownPortalPendingOwnerFieldObservation>>();
                _remoteTownPortalPendingOwnerFieldObservations[ownerCharacterId] = observationsBySourceMap;
            }

            if (!observationsBySourceMap.TryGetValue(sourceMapId, out Dictionary<RemoteTownPortalObservationSource, RemoteTownPortalPendingOwnerFieldObservation> observationsBySource))
            {
                observationsBySource = new Dictionary<RemoteTownPortalObservationSource, RemoteTownPortalPendingOwnerFieldObservation>();
                observationsBySourceMap[sourceMapId] = observationsBySource;
            }

            if (observationsBySource.TryGetValue(observationSource, out RemoteTownPortalPendingOwnerFieldObservation existingObservation)
                && CompareRemoteTownPortalSameSourceCoordinateAuthority(
                    existingObservation.ObservationSource,
                    existingObservation.RecordedAt,
                    observationSource,
                    recordedAt) >= 0)
            {
                return;
            }

            observationsBySource[observationSource] = new RemoteTownPortalPendingOwnerFieldObservation(
                sourceMapId,
                sourceX,
                sourceY,
                observationSource,
                recordedAt);
        }

        private void AdoptPendingRemoteTownPortalOwnerFieldObservations(uint ownerCharacterId, int townMapId)
        {
            if (ownerCharacterId == 0
                || townMapId <= 0
                || !_remoteTownPortalPendingOwnerFieldObservations.TryGetValue(ownerCharacterId, out Dictionary<int, Dictionary<RemoteTownPortalObservationSource, RemoteTownPortalPendingOwnerFieldObservation>> observationsBySourceMap)
                || observationsBySourceMap.Count == 0)
            {
                return;
            }

            List<int> adoptedSourceMaps = null;
            foreach ((int sourceMapId, Dictionary<RemoteTownPortalObservationSource, RemoteTownPortalPendingOwnerFieldObservation> observationsBySource) in observationsBySourceMap)
            {
                int? resolvedTownMapId = TryResolveRemoteTownPortalTownMapForSourceMap(sourceMapId, out int resolvedTownMap)
                    ? resolvedTownMap
                    : null;
                bool canAdoptFromResolvedTownMap = resolvedTownMapId.HasValue && resolvedTownMapId.Value == townMapId;
                bool canAdoptFromOwnerTownIdentity = !canAdoptFromResolvedTownMap
                    && CanAdoptPendingRemoteTownPortalObservationFromOwnerTownIdentity(
                        ownerCharacterId,
                        townMapId,
                        sourceMapId);
                if (!canAdoptFromResolvedTownMap && !canAdoptFromOwnerTownIdentity)
                {
                    continue;
                }

                foreach (RemoteTownPortalPendingOwnerFieldObservation observation in observationsBySource.Values)
                {
                    UpsertRemoteTownPortalOwnerFieldObservation(
                        ownerCharacterId,
                        observation.SourceMapId,
                        observation.SourceX,
                        observation.SourceY,
                        townMapId,
                        observation.ObservationSource,
                        observation.RecordedAt,
                        refreshActiveDestination: false);
                }

                adoptedSourceMaps ??= new List<int>();
                adoptedSourceMaps.Add(sourceMapId);
            }

            if (adoptedSourceMaps == null)
            {
                return;
            }

            foreach (int sourceMapId in adoptedSourceMaps)
            {
                observationsBySourceMap.Remove(sourceMapId);
            }

            if (observationsBySourceMap.Count == 0)
            {
                _remoteTownPortalPendingOwnerFieldObservations.Remove(ownerCharacterId);
            }
        }

        private bool CanAdoptPendingRemoteTownPortalObservationFromOwnerTownIdentity(
            uint ownerCharacterId,
            int townMapId,
            int sourceMapId)
        {
            RemoteTownPortalOwnerTownKey key = new(ownerCharacterId, townMapId);
            RemoteTownPortalFieldMetadata? metadata = _remoteTownPortalFieldMetadata.TryGetValue(key, out RemoteTownPortalFieldMetadata metadataValue)
                ? metadataValue
                : null;
            RemoteTownPortalState? existingState = _remoteTownPortals.TryGetValue(ownerCharacterId, out RemoteTownPortalState existingStateValue)
                && existingStateValue.MapId == townMapId
                ? existingStateValue
                : null;
            return CanAdoptPendingRemoteTownPortalObservationFromOwnerTownIdentity(
                townMapId,
                sourceMapId,
                metadata,
                existingState);
        }

        private static bool CanAdoptPendingRemoteTownPortalObservationFromOwnerTownIdentity(
            int townMapId,
            int sourceMapId,
            RemoteTownPortalFieldMetadata? metadata,
            RemoteTownPortalState? existingState)
        {
            if (townMapId <= 0 || sourceMapId <= 0 || townMapId == sourceMapId)
            {
                return false;
            }

            if (metadata.HasValue
                && metadata.Value.TownMapId == townMapId
                && metadata.Value.SourceMapId == sourceMapId)
            {
                return true;
            }

            return existingState.HasValue
                   && existingState.Value.MapId == townMapId
                   && existingState.Value.Destination.HasValue
                   && existingState.Value.Destination.Value.MapId == sourceMapId;
        }

        private RemoteTownPortalResolvedDestination? ResolveRemoteTownPortalDestination(
            uint ownerCharacterId,
            int currentMapId,
            RemoteTownPortalResolvedDestination? incomingDestination,
            RemoteTownPortalState? existingState)
        {
            RemoteTownPortalOwnerTownKey key = new(ownerCharacterId, currentMapId);
            RemoteTownPortalFieldMetadata? metadata = _remoteTownPortalFieldMetadata.TryGetValue(key, out RemoteTownPortalFieldMetadata metadataValue)
                ? metadataValue
                : null;
            int? preferredSourceMapId = TryResolvePreferredRemoteTownPortalSourceMapId(
                key,
                existingState,
                metadata,
                out RemoteTownPortalOwnerFieldObservation? ownerObservation,
                out int? resolvedPreferredSourceMapId)
                ? resolvedPreferredSourceMapId
                : null;
            return ResolveRemoteTownPortalDestination(
                currentMapId,
                incomingDestination,
                existingState,
                metadata,
                ownerObservation,
                preferredSourceMapId);
        }

        private int? ResolveRemoteTownPortalPreferredSourceMapId(
            uint ownerCharacterId,
            int currentMapId,
            RemoteTownPortalState? existingState)
        {
            RemoteTownPortalOwnerTownKey key = new(ownerCharacterId, currentMapId);
            RemoteTownPortalFieldMetadata? metadata = _remoteTownPortalFieldMetadata.TryGetValue(key, out RemoteTownPortalFieldMetadata metadataValue)
                ? metadataValue
                : null;
            if (!TryResolvePreferredRemoteTownPortalSourceMapId(
                    key,
                    existingState,
                    metadata,
                    out RemoteTownPortalOwnerFieldObservation? ownerObservation,
                    out int? preferredSourceMapId))
            {
                return null;
            }

            return preferredSourceMapId;
        }

        private bool TryResolvePreferredRemoteTownPortalSourceMapId(
            RemoteTownPortalOwnerTownKey key,
            RemoteTownPortalState? existingState,
            RemoteTownPortalFieldMetadata? metadata,
            out RemoteTownPortalOwnerFieldObservation? ownerObservation,
            out int? preferredSourceMapId)
        {
            ownerObservation = null;
            preferredSourceMapId = null;
            List<RemoteTownPortalOwnerFieldObservation[]> candidatesBySourceMap;
            if (_remoteTownPortalOwnerFieldObservations.TryGetValue(key, out Dictionary<int, Dictionary<RemoteTownPortalObservationSource, RemoteTownPortalOwnerFieldObservation>> observationsBySourceMap)
                && observationsBySourceMap.Count > 0)
            {
                candidatesBySourceMap = observationsBySourceMap.Values
                    .Select(observationsBySource => observationsBySource.Values.ToArray())
                    .Where(observations => observations.Length > 0)
                    .ToList();
            }
            else
            {
                candidatesBySourceMap = new List<RemoteTownPortalOwnerFieldObservation[]>();
            }

            preferredSourceMapId = SelectPreferredRemoteTownPortalSourceMapId(
                existingState,
                metadata,
                candidatesBySourceMap);
            if (!preferredSourceMapId.HasValue)
            {
                return false;
            }

            ownerObservation = candidatesBySourceMap.Count > 0
                ? SelectPreferredRemoteTownPortalOwnerObservationForSourceMap(candidatesBySourceMap, preferredSourceMapId.Value)
                : null;
            return ownerObservation.HasValue
                || (metadata.HasValue && metadata.Value.SourceMapId == preferredSourceMapId.Value)
                || (existingState.HasValue
                    && existingState.Value.Destination.HasValue
                    && existingState.Value.Destination.Value.MapId == preferredSourceMapId.Value);
        }

        private static RemoteTownPortalResolvedDestination? ResolveRemoteTownPortalDestination(
            int currentMapId,
            RemoteTownPortalResolvedDestination? incomingDestination,
            RemoteTownPortalState? existingState,
            RemoteTownPortalFieldMetadata? metadata,
            RemoteTownPortalOwnerFieldObservation? ownerObservation,
            int? preferredSourceMapId = null)
        {
            incomingDestination = NormalizeRemoteTownPortalIncomingDestination(currentMapId, incomingDestination);
            RemoteTownPortalResolvedDestination? metadataDestination = metadata.HasValue
                && (!preferredSourceMapId.HasValue || metadata.Value.SourceMapId == preferredSourceMapId.Value)
                ? ResolveRemoteTownPortalObservedFieldDestination(
                    currentMapId,
                    metadata.Value.SourceMapId,
                    metadata.Value.SourceX,
                    metadata.Value.SourceY,
                    metadata.Value.TownMapId)
                : null;
            RemoteTownPortalResolvedDestination? observationDestination = ownerObservation.HasValue
                && (!preferredSourceMapId.HasValue || ownerObservation.Value.SourceMapId == preferredSourceMapId.Value)
                ? ResolveRemoteTownPortalObservedFieldDestination(
                    currentMapId,
                    ownerObservation.Value.SourceMapId,
                    ownerObservation.Value.SourceX,
                    ownerObservation.Value.SourceY,
                    ownerObservation.Value.TownMapId)
                : null;

            if (metadataDestination.HasValue && ownerObservation.HasValue && observationDestination.HasValue)
            {
                return ChooseRemoteTownPortalResolvedDestination(
                    metadata.Value,
                    metadataDestination,
                    ownerObservation.Value,
                    observationDestination);
            }

            if (metadataDestination.HasValue)
            {
                return metadataDestination.Value;
            }

            if (observationDestination.HasValue)
            {
                return observationDestination.Value;
            }

            if (preferredSourceMapId.HasValue
                && existingState.HasValue
                && existingState.Value.Destination.HasValue
                && existingState.Value.Destination.Value.MapId == preferredSourceMapId.Value)
            {
                return existingState.Value.Destination.Value;
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

        private static RemoteTownPortalResolvedDestination? NormalizeRemoteTownPortalIncomingDestination(
            int currentMapId,
            RemoteTownPortalResolvedDestination? incomingDestination)
        {
            if (!incomingDestination.HasValue)
            {
                return null;
            }

            return incomingDestination.Value.MapId > 0 && incomingDestination.Value.MapId != currentMapId
                ? incomingDestination
                : null;
        }

        private static RemoteTownPortalResolvedDestination? ResolveRemoteTownPortalObservedFieldDestination(
            int currentMapId,
            int sourceMapId,
            float sourceX,
            float sourceY,
            int townMapId)
        {
            if (currentMapId == townMapId && currentMapId != sourceMapId)
            {
                return new RemoteTownPortalResolvedDestination(sourceMapId, sourceX, sourceY);
            }

            return null;
        }

        private static bool TryResolveRemoteTownPortalCurrentMapReturnDestination(
            int currentMapId,
            out RemoteTownPortalResolvedDestination destination)
        {
            destination = default;
            if (currentMapId <= 0
                || !TryResolveRemoteTownPortalTownMapForSourceMap(currentMapId, out int townMapId)
                || townMapId <= 0
                || townMapId == currentMapId
                || HasRemoteTownPortalPointInMap(currentMapId)
                || !TryResolveRemoteTownPortalTownDestinationFallbackPosition(townMapId, out float townX, out float townY))
            {
                return false;
            }

            destination = new RemoteTownPortalResolvedDestination(townMapId, townX, townY);
            return true;
        }

        private static bool TryResolveRemoteTownPortalWzFallbackDestination(
            int townMapId,
            int? preferredSourceMapId,
            out RemoteTownPortalResolvedDestination destination)
        {
            destination = default;
            if (preferredSourceMapId.HasValue)
            {
                if (TryResolveRemoteTownPortalPreferredWzFallbackDestination(
                        townMapId,
                        preferredSourceMapId.Value,
                        out destination,
                        out bool preferredSourceValidated))
                {
                    return true;
                }

                if (preferredSourceValidated)
                {
                    return false;
                }
            }

            if (!TryResolveUniqueRemoteTownPortalWzFallbackSourceMap(townMapId, out int sourceMapId)
                || !TryResolveRemoteTownPortalSourceFallbackPosition(sourceMapId, out float sourceX, out float sourceY))
            {
                return false;
            }

            destination = new RemoteTownPortalResolvedDestination(sourceMapId, sourceX, sourceY);
            return true;
        }

        private static bool CanRememberRemoteTownPortalPacketCastMetadataFromResolvedFallback(
            int currentMapId,
            RemoteTownPortalResolvedDestination destination)
        {
            return CanRememberRemoteTownPortalPacketCastMetadataFromResolvedFallback(
                currentMapId,
                destination.MapId,
                HasRemoteTownPortalPointInMap(currentMapId),
                TryResolveRemoteTownPortalTownMapForSourceMap(currentMapId, out int currentMapReturnTownMapId),
                currentMapReturnTownMapId);
        }

        private static bool CanRememberRemoteTownPortalPacketCastMetadataFromResolvedFallback(
            int currentMapId,
            int resolvedDestinationMapId,
            bool currentMapHasTownPortalPoint,
            bool hasCurrentMapReturnTownMap,
            int currentMapReturnTownMapId)
        {
            return currentMapId > 0
                   && resolvedDestinationMapId > 0
                   && resolvedDestinationMapId != currentMapId
                   && !currentMapHasTownPortalPoint
                   && hasCurrentMapReturnTownMap
                   && currentMapReturnTownMapId == resolvedDestinationMapId;
        }

        private static bool ShouldRememberRemoteTownPortalPacketCastMetadataFromResolvedDestination(
            RemoteTownPortalDestinationResolutionKind resolutionKind,
            int currentMapId,
            RemoteTownPortalResolvedDestination resolvedDestination)
        {
            return ShouldRememberRemoteTownPortalPacketCastMetadataFromResolvedDestination(
                resolutionKind == RemoteTownPortalDestinationResolutionKind.SourceMapReturnFallback,
                currentMapId,
                resolvedDestination.MapId,
                HasRemoteTownPortalPointInMap(currentMapId),
                TryResolveRemoteTownPortalTownMapForSourceMap(currentMapId, out int currentMapReturnTownMapId),
                currentMapReturnTownMapId);
        }

        private static bool ShouldRememberRemoteTownPortalPacketCastMetadataFromResolvedDestination(
            bool resolvedFromSourceMapReturnFallback,
            int currentMapId,
            int resolvedDestinationMapId,
            bool currentMapHasTownPortalPoint,
            bool hasCurrentMapReturnTownMap,
            int currentMapReturnTownMapId)
        {
            return resolvedFromSourceMapReturnFallback
                   && CanRememberRemoteTownPortalPacketCastMetadataFromResolvedFallback(
                       currentMapId,
                       resolvedDestinationMapId,
                       currentMapHasTownPortalPoint,
                       hasCurrentMapReturnTownMap,
                       currentMapReturnTownMapId);
        }

        private static bool TryResolveRemoteTownPortalPreferredWzFallbackDestination(
            int townMapId,
            int preferredSourceMapId,
            out RemoteTownPortalResolvedDestination destination,
            out bool preferredSourceValidated)
        {
            destination = default;
            preferredSourceValidated = false;
            if (preferredSourceMapId <= 0
                || preferredSourceMapId == townMapId
                || !TryResolveRemoteTownPortalTownMapForSourceMap(preferredSourceMapId, out int configuredTownMapId)
                || configuredTownMapId != townMapId)
            {
                return false;
            }

            preferredSourceValidated = true;
            if (!TryResolveRemoteTownPortalSourceFallbackPosition(preferredSourceMapId, out float sourceX, out float sourceY))
            {
                return false;
            }

            destination = new RemoteTownPortalResolvedDestination(preferredSourceMapId, sourceX, sourceY);
            return true;
        }

        private static bool TryResolveUniqueRemoteTownPortalWzFallbackSourceMap(int townMapId, out int sourceMapId)
        {
            sourceMapId = -1;
            if (townMapId <= 0 || Program.InfoManager?.MapsCache == null)
            {
                return false;
            }

            int matchCount = 0;
            foreach ((string mapIdKey, Tuple<WzImage, string, string, string, MapleLib.WzLib.WzStructure.MapInfo> cachedMap) in Program.InfoManager.MapsCache)
            {
                if (!int.TryParse(mapIdKey, out int candidateSourceMapId)
                    || candidateSourceMapId <= 0
                    || candidateSourceMapId == townMapId)
                {
                    continue;
                }

                if (!TryResolveRemoteTownPortalConfiguredTownMapFromCachedMap(cachedMap, out int candidateTownMapId))
                {
                    continue;
                }

                if (candidateTownMapId != townMapId)
                {
                    continue;
                }

                matchCount++;
                if (matchCount > 1)
                {
                    sourceMapId = -1;
                    return false;
                }

                sourceMapId = candidateSourceMapId;
            }

            return matchCount == 1;
        }

        private static int NormalizeRemoteTownPortalConfiguredReturnMap(int returnMapId, int forcedReturnMapId)
        {
            if (returnMapId > 0 && returnMapId != MapConstants.MaxMap)
            {
                return returnMapId;
            }

            return forcedReturnMapId > 0 && forcedReturnMapId != MapConstants.MaxMap
                ? forcedReturnMapId
                : -1;
        }

        private static bool TryResolveRemoteTownPortalSourceFallbackPosition(
            int sourceMapId,
            out float sourceX,
            out float sourceY)
        {
            sourceX = 0f;
            sourceY = 0f;
            WzImage mapImage = TryGetMapImageForRemoteTownPortalMetadataLookup(sourceMapId);
            if (mapImage == null)
            {
                return false;
            }

            bool shouldUnparse = !mapImage.Parsed;
            try
            {
                if (!mapImage.Parsed)
                {
                    mapImage.ParseImage();
                }

                return TryResolveRemoteTownPortalSourceFallbackPortalPosition(mapImage, out sourceX, out sourceY);
            }
            finally
            {
                if (shouldUnparse)
                {
                    mapImage.UnparseImage();
                }
            }
        }

        private static bool TryResolveRemoteTownPortalTownDestinationFallbackPosition(
            int townMapId,
            out float townX,
            out float townY)
        {
            townX = 0f;
            townY = 0f;
            WzImage townMapImage = TryGetMapImageForRemoteTownPortalMetadataLookup(townMapId);
            if (townMapImage == null)
            {
                return false;
            }

            bool shouldUnparse = !townMapImage.Parsed;
            try
            {
                if (!townMapImage.Parsed)
                {
                    townMapImage.ParseImage();
                }

                return TryResolveRemoteTownPortalTownDestinationPortalPosition(townMapImage, out townX, out townY);
            }
            finally
            {
                if (shouldUnparse)
                {
                    townMapImage.UnparseImage();
                }
            }
        }

        private static bool TryResolveRemoteTownPortalTownDestinationPortalPosition(
            WzImage mapImage,
            out float townX,
            out float townY)
        {
            townX = 0f;
            townY = 0f;
            if (mapImage?["portal"] is not WzSubProperty portalParent)
            {
                return false;
            }

            return TrySelectRemoteTownPortalTownDestinationFallbackPosition(
                EnumerateRemoteTownPortalPortalCandidates(portalParent),
                out townX,
                out townY);
        }

        internal static bool TrySelectRemoteTownPortalTownDestinationFallbackPosition(
            WzSubProperty portalParent,
            out float townX,
            out float townY)
        {
            townX = 0f;
            townY = 0f;
            if (portalParent == null)
            {
                return false;
            }

            return TrySelectRemoteTownPortalTownDestinationFallbackPosition(
                EnumerateRemoteTownPortalPortalCandidates(portalParent),
                out townX,
                out townY);
        }

        private static bool TryResolveRemoteTownPortalSourceFallbackPortalPosition(
            WzImage mapImage,
            out float sourceX,
            out float sourceY)
        {
            sourceX = 0f;
            sourceY = 0f;
            if (mapImage?["portal"] is not WzSubProperty portalParent)
            {
                return false;
            }

            return TrySelectRemoteTownPortalSourceFallbackPosition(
                EnumerateRemoteTownPortalPortalCandidates(portalParent),
                out sourceX,
                out sourceY);
        }

        private static IEnumerable<RemoteTownPortalPortalCandidate> EnumerateRemoteTownPortalPortalCandidates(WzSubProperty portalParent)
        {
            foreach (WzSubProperty portal in portalParent.WzProperties.OfType<WzSubProperty>())
            {
                yield return new RemoteTownPortalPortalCandidate(
                    InfoTool.GetOptionalInt(portal["pt"]),
                    InfoTool.GetOptionalString(portal["pn"]),
                    InfoTool.GetInt(portal["x"]),
                    InfoTool.GetInt(portal["y"]));
            }
        }

        private static bool TrySelectRemoteTownPortalTownDestinationFallbackPosition(
            IEnumerable<RemoteTownPortalPortalCandidate> candidates,
            out float townX,
            out float townY)
        {
            townX = 0f;
            townY = 0f;
            RemoteTownPortalPortalCandidate? startPortal = null;
            RemoteTownPortalPortalCandidate? fallbackPortal = null;

            foreach (RemoteTownPortalPortalCandidate candidate in candidates ?? Enumerable.Empty<RemoteTownPortalPortalCandidate>())
            {
                fallbackPortal ??= candidate;
                if (IsRemoteTownPortalTownPointPortal(candidate.PortalTypeId, candidate.PortalName))
                {
                    townX = candidate.X;
                    townY = candidate.Y;
                    return true;
                }

                if (!startPortal.HasValue && IsRemoteTownPortalSourceFallbackStartPortal(candidate.PortalTypeId, candidate.PortalName))
                {
                    startPortal = candidate;
                }
            }

            RemoteTownPortalPortalCandidate? selectedFallbackPortal = startPortal ?? fallbackPortal;
            if (!selectedFallbackPortal.HasValue)
            {
                return false;
            }

            townX = selectedFallbackPortal.Value.X;
            townY = selectedFallbackPortal.Value.Y;
            return true;
        }

        private static bool TrySelectRemoteTownPortalSourceFallbackPosition(
            IEnumerable<RemoteTownPortalPortalCandidate> candidates,
            out float sourceX,
            out float sourceY)
        {
            sourceX = 0f;
            sourceY = 0f;
            RemoteTownPortalPortalCandidate? fallbackPortal = null;

            foreach (RemoteTownPortalPortalCandidate candidate in candidates ?? Enumerable.Empty<RemoteTownPortalPortalCandidate>())
            {
                fallbackPortal ??= candidate;
                if (IsRemoteTownPortalSourceFallbackStartPortal(candidate.PortalTypeId, candidate.PortalName))
                {
                    sourceX = candidate.X;
                    sourceY = candidate.Y;
                    return true;
                }
            }

            if (!fallbackPortal.HasValue)
            {
                return false;
            }

            sourceX = fallbackPortal.Value.X;
            sourceY = fallbackPortal.Value.Y;
            return true;
        }

        private static bool HasRemoteTownPortalPointInMap(int mapId)
        {
            WzImage mapImage = TryGetMapImageForRemoteTownPortalMetadataLookup(mapId);
            if (mapImage == null)
            {
                return false;
            }

            bool shouldUnparse = !mapImage.Parsed;
            try
            {
                if (!mapImage.Parsed)
                {
                    mapImage.ParseImage();
                }

                if (mapImage["portal"] is not WzSubProperty portalParent)
                {
                    return false;
                }

                foreach (WzSubProperty portal in portalParent.WzProperties.OfType<WzSubProperty>())
                {
                    int? portalTypeId = InfoTool.GetOptionalInt(portal["pt"]);
                    string portalName = InfoTool.GetOptionalString(portal["pn"]);
                    if (IsRemoteTownPortalTownPointPortal(portalTypeId, portalName))
                    {
                        return true;
                    }
                }

                return false;
            }
            finally
            {
                if (shouldUnparse)
                {
                    mapImage.UnparseImage();
                }
            }
        }

        private static bool IsRemoteTownPortalTownPointPortal(int? portalTypeId, string portalName)
        {
            if (portalTypeId.HasValue)
            {
                if (portalTypeId.Value == (int)PortalType.TownPortalPoint)
                {
                    return true;
                }

                if (Program.InfoManager?.PortalEditor_TypeById != null
                    && portalTypeId.Value >= 0
                    && portalTypeId.Value < Program.InfoManager.PortalEditor_TypeById.Count
                    && Program.InfoManager.PortalEditor_TypeById[portalTypeId.Value] == PortalType.TownPortalPoint)
                {
                    return true;
                }
            }

            return string.Equals(portalName, PortalType.TownPortalPoint.ToCode(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRemoteTownPortalSourceFallbackStartPortal(int? portalTypeId, string portalName)
        {
            if (portalTypeId.HasValue)
            {
                if (portalTypeId.Value == (int)PortalType.StartPoint)
                {
                    return true;
                }

                if (Program.InfoManager?.PortalEditor_TypeById != null
                    && portalTypeId.Value >= 0
                    && portalTypeId.Value < Program.InfoManager.PortalEditor_TypeById.Count
                    && Program.InfoManager.PortalEditor_TypeById[portalTypeId.Value] == PortalType.StartPoint)
                {
                    return true;
                }
            }

            return string.Equals(portalName, PortalType.StartPoint.ToCode(), StringComparison.OrdinalIgnoreCase);
        }

        internal static bool TryResolveRemoteTownPortalConfiguredTownMapForSourceMap(int sourceMapId, out int townMapId)
        {
            return TryResolveRemoteTownPortalTownMapForSourceMap(sourceMapId, out townMapId);
        }

        private static bool TryResolveRemoteTownPortalTownMapForSourceMap(int sourceMapId, out int townMapId)
        {
            townMapId = -1;
            if (sourceMapId <= 0)
            {
                return false;
            }

            string mapIdKey = sourceMapId.ToString().PadLeft(9, '0');
            if (Program.InfoManager?.MapsCache != null
                && Program.InfoManager.MapsCache.TryGetValue(mapIdKey, out Tuple<WzImage, string, string, string, MapleLib.WzLib.WzStructure.MapInfo> cachedMap)
                && TryResolveRemoteTownPortalConfiguredTownMapFromCachedMap(cachedMap, out townMapId))
            {
                return true;
            }

            WzImage mapImage = TryGetMapImageForRemoteTownPortalMetadataLookup(sourceMapId);
            return TryResolveRemoteTownPortalConfiguredTownMapFromMapImage(mapImage, out townMapId);
        }

        private static bool TryResolveRemoteTownPortalConfiguredTownMapFromCachedMap(
            Tuple<WzImage, string, string, string, MapleLib.WzLib.WzStructure.MapInfo> cachedMap,
            out int townMapId)
        {
            townMapId = -1;
            if (cachedMap?.Item5 != null)
            {
                int configuredReturnMap = NormalizeRemoteTownPortalConfiguredReturnMap(
                    cachedMap.Item5.returnMap,
                    cachedMap.Item5.forcedReturn);
                if (configuredReturnMap > 0)
                {
                    townMapId = configuredReturnMap;
                    return true;
                }
            }

            return TryResolveRemoteTownPortalConfiguredTownMapFromMapImage(cachedMap?.Item1, out townMapId);
        }

        private static bool TryResolveRemoteTownPortalConfiguredTownMapFromMapImage(
            WzImage mapImage,
            out int townMapId)
        {
            townMapId = -1;
            if (mapImage == null)
            {
                return false;
            }

            bool shouldUnparse = !mapImage.Parsed;
            try
            {
                if (!mapImage.Parsed)
                {
                    mapImage.ParseImage();
                }

                if (mapImage["info"] is not WzSubProperty infoProperty)
                {
                    return false;
                }

                int configuredReturnMap = NormalizeRemoteTownPortalConfiguredReturnMap(
                    (infoProperty["returnMap"] as WzIntProperty)?.GetInt() ?? 0,
                    (infoProperty["forcedReturn"] as WzIntProperty)?.GetInt() ?? 0);
                if (configuredReturnMap <= 0)
                {
                    return false;
                }

                townMapId = configuredReturnMap;
                return true;
            }
            finally
            {
                if (shouldUnparse)
                {
                    mapImage.UnparseImage();
                }
            }
        }

        private static WzImage TryGetMapImageForRemoteTownPortalMetadataLookup(int mapId)
        {
            string mapIdKey = mapId.ToString().PadLeft(9, '0');
            if (Program.InfoManager?.MapsCache != null
                && Program.InfoManager.MapsCache.TryGetValue(mapIdKey, out Tuple<WzImage, string, string, string, MapleLib.WzLib.WzStructure.MapInfo> cachedMap)
                && cachedMap?.Item1 != null)
            {
                return cachedMap.Item1;
            }

            if (Program.DataSource == null)
            {
                return null;
            }

            string folderNum = mapIdKey[0].ToString();
            return Program.DataSource.GetImageByPath($"Map/Map/Map{folderNum}/{mapIdKey}.img")
                   ?? Program.DataSource.GetImage("Map", $"Map/Map{folderNum}/{mapIdKey}.img");
        }

        private static int? SelectPreferredRemoteTownPortalSourceMapId(
            RemoteTownPortalState? existingState,
            RemoteTownPortalFieldMetadata? metadata,
            IEnumerable<RemoteTownPortalOwnerFieldObservation[]> ownerObservationsBySourceMap)
        {
            if (ownerObservationsBySourceMap == null)
            {
                return null;
            }

            List<RemoteTownPortalOwnerFieldObservation[]> candidatesBySourceMap = ownerObservationsBySourceMap
                .Where(group => group != null && group.Length > 0)
                .ToList();
            RemoteTownPortalOwnerFieldObservation? selectedSourceObservation = candidatesBySourceMap.Count > 0
                ? SelectPreferredRemoteTownPortalOwnerObservationForSourceSelection(candidatesBySourceMap)
                : null;
            RemoteTownPortalOwnerFieldObservation? existingSourceObservation = CreateRemoteTownPortalExistingSourceObservation(existingState);
            if (existingSourceObservation.HasValue
                && (!selectedSourceObservation.HasValue
                    || CompareRemoteTownPortalObservationQuality(
                        existingSourceObservation.Value.ObservationSource,
                        existingSourceObservation.Value.RecordedAt,
                        selectedSourceObservation.Value.ObservationSource,
                        selectedSourceObservation.Value.RecordedAt) > 0))
            {
                selectedSourceObservation = existingSourceObservation;
            }

            if (!metadata.HasValue)
            {
                return selectedSourceObservation?.SourceMapId;
            }

            if (!selectedSourceObservation.HasValue)
            {
                return metadata.Value.SourceMapId;
            }

            if (metadata.Value.SourceMapId == selectedSourceObservation.Value.SourceMapId)
            {
                return metadata.Value.SourceMapId;
            }

            int comparison = CompareRemoteTownPortalObservationQuality(
                metadata.Value.ObservationSource,
                metadata.Value.RecordedAt,
                selectedSourceObservation.Value.ObservationSource,
                selectedSourceObservation.Value.RecordedAt);
            return comparison >= 0
                ? metadata.Value.SourceMapId
                : selectedSourceObservation.Value.SourceMapId;
        }

        private static RemoteTownPortalOwnerFieldObservation? CreateRemoteTownPortalExistingSourceObservation(RemoteTownPortalState? existingState)
        {
            if (!existingState.HasValue
                || existingState.Value.Phase == RemoteTownPortalVisualPhase.Removing
                || !existingState.Value.Destination.HasValue)
            {
                return null;
            }

            RemoteTownPortalResolvedDestination destination = existingState.Value.Destination.Value;
            if (destination.MapId <= 0 || destination.MapId == existingState.Value.MapId)
            {
                return null;
            }

            return new RemoteTownPortalOwnerFieldObservation(
                destination.MapId,
                destination.X,
                destination.Y,
                existingState.Value.MapId,
                RemoteTownPortalObservationSource.InferredSourceField,
                existingState.Value.PhaseStartedAt);
        }

        private static RemoteTownPortalOwnerFieldObservation? SelectPreferredRemoteTownPortalOwnerObservationForSourceMap(
            IEnumerable<RemoteTownPortalOwnerFieldObservation[]> ownerObservationsBySourceMap,
            int sourceMapId)
        {
            foreach (RemoteTownPortalOwnerFieldObservation[] sourceObservations in ownerObservationsBySourceMap)
            {
                if (sourceObservations.Length == 0 || sourceObservations[0].SourceMapId != sourceMapId)
                {
                    continue;
                }

                return SelectPreferredRemoteTownPortalCoordinateObservation(sourceObservations);
            }

            return null;
        }

        private static RemoteTownPortalOwnerFieldObservation? SelectPreferredRemoteTownPortalOwnerObservationForSourceSelection(
            IEnumerable<RemoteTownPortalOwnerFieldObservation[]> ownerObservationsBySourceMap)
        {
            RemoteTownPortalOwnerFieldObservation? preferredObservation = null;
            foreach (RemoteTownPortalOwnerFieldObservation[] sourceObservations in ownerObservationsBySourceMap)
            {
                RemoteTownPortalOwnerFieldObservation? candidate = SelectPreferredRemoteTownPortalSourceSelectionObservation(sourceObservations);
                if (!candidate.HasValue)
                {
                    continue;
                }

                if (!preferredObservation.HasValue
                    || CompareRemoteTownPortalObservationQuality(
                        candidate.Value.ObservationSource,
                        candidate.Value.RecordedAt,
                        preferredObservation.Value.ObservationSource,
                        preferredObservation.Value.RecordedAt) > 0)
                {
                    preferredObservation = candidate.Value;
                }
            }

            return preferredObservation;
        }

        private static RemoteTownPortalOwnerFieldObservation? SelectPreferredRemoteTownPortalCoordinateObservation(
            IEnumerable<RemoteTownPortalOwnerFieldObservation> ownerObservations)
        {
            RemoteTownPortalOwnerFieldObservation? preferredObservation = null;
            foreach (RemoteTownPortalOwnerFieldObservation candidate in ownerObservations)
            {
                if (!preferredObservation.HasValue
                    || CompareRemoteTownPortalSameSourceCoordinateAuthority(
                        preferredObservation.Value.ObservationSource,
                        preferredObservation.Value.RecordedAt,
                        candidate.ObservationSource,
                        candidate.RecordedAt) < 0)
                {
                    preferredObservation = candidate;
                }
            }

            return preferredObservation;
        }

        private static RemoteTownPortalOwnerFieldObservation? SelectPreferredRemoteTownPortalSourceSelectionObservation(
            IEnumerable<RemoteTownPortalOwnerFieldObservation> ownerObservations)
        {
            RemoteTownPortalOwnerFieldObservation? preferredObservation = null;
            foreach (RemoteTownPortalOwnerFieldObservation candidate in ownerObservations)
            {
                if (!preferredObservation.HasValue
                    || CompareRemoteTownPortalObservationQuality(
                        candidate.ObservationSource,
                        candidate.RecordedAt,
                        preferredObservation.Value.ObservationSource,
                        preferredObservation.Value.RecordedAt) > 0)
                {
                    preferredObservation = candidate;
                }
            }

            return preferredObservation;
        }

        private static RemoteTownPortalResolvedDestination? ChooseRemoteTownPortalResolvedDestination(
            RemoteTownPortalFieldMetadata metadata,
            RemoteTownPortalResolvedDestination? metadataDestination,
            RemoteTownPortalOwnerFieldObservation ownerObservation,
            RemoteTownPortalResolvedDestination? observationDestination)
        {
            if (metadataDestination.HasValue && observationDestination.HasValue)
            {
                if (metadata.SourceMapId == ownerObservation.SourceMapId)
                {
                    int sameSourceComparison = CompareRemoteTownPortalSameSourceCoordinateAuthority(
                        metadata.ObservationSource,
                        metadata.RecordedAt,
                        ownerObservation.ObservationSource,
                        ownerObservation.RecordedAt);
                    return sameSourceComparison >= 0
                        ? metadataDestination.Value
                        : observationDestination.Value;
                }

                int comparison = CompareRemoteTownPortalObservationQuality(
                    metadata.ObservationSource,
                    metadata.RecordedAt,
                    ownerObservation.ObservationSource,
                    ownerObservation.RecordedAt);
                return comparison >= 0
                    ? metadataDestination.Value
                    : observationDestination.Value;
            }

            if (metadataDestination.HasValue)
            {
                return metadataDestination.Value;
            }

            if (observationDestination.HasValue)
            {
                return observationDestination.Value;
            }

            return null;
        }

        private static RemoteTownPortalVisualPhase ResolveRemoteTownPortalCreatePhase(
            byte packetState,
            RemoteTownPortalState? existingState)
        {
            if (!existingState.HasValue || existingState.Value.Phase == RemoteTownPortalVisualPhase.Removing)
            {
                return packetState == 0
                    ? RemoteTownPortalVisualPhase.Opening
                    : RemoteTownPortalVisualPhase.Stable;
            }

            if (existingState.Value.State == 0)
            {
                return RemoteTownPortalVisualPhase.Stable;
            }

            if (packetState == 0)
            {
                return RemoteTownPortalVisualPhase.Opening;
            }

            return RemoteTownPortalVisualPhase.Stable;
        }

        private static byte ResolveRemoteTownPortalCreateState(
            byte packetState,
            RemoteTownPortalState? existingState)
        {
            if (!existingState.HasValue || existingState.Value.Phase == RemoteTownPortalVisualPhase.Removing)
            {
                return packetState;
            }

            if (existingState.Value.State == 0)
            {
                return RemoteTownPortalOverlayState;
            }

            return existingState.Value.State;
        }

        private static bool AreEquivalentRemoteTownPortalDestinations(
            RemoteTownPortalResolvedDestination? left,
            RemoteTownPortalResolvedDestination? right)
        {
            if (!left.HasValue || !right.HasValue)
            {
                return left.HasValue == right.HasValue;
            }

            return left.Value.MapId == right.Value.MapId
                && Math.Abs(left.Value.X - right.Value.X) < 0.01f
                && Math.Abs(left.Value.Y - right.Value.Y) < 0.01f;
        }

        private static bool ShouldLinkRemoteTownPortal(RemoteTownPortalState state)
        {
            return state.Destination.HasValue
                   && state.Phase != RemoteTownPortalVisualPhase.Removing;
        }

        private static bool ShouldRefreshRemoteTownPortalInferredMetadata(
            RemoteTownPortalFieldMetadata? metadata,
            RemoteTownPortalOwnerFieldObservation? ownerObservation,
            RemoteTownPortalResolvedDestination refreshedDestination)
        {
            if (!ownerObservation.HasValue)
            {
                return false;
            }

            if (metadata.HasValue && metadata.Value.ObservationSource == RemoteTownPortalObservationSource.PacketCast)
            {
                return false;
            }

            RemoteTownPortalOwnerFieldObservation observation = ownerObservation.Value;
            if (refreshedDestination.MapId != observation.SourceMapId
                || Math.Abs(refreshedDestination.X - observation.SourceX) >= 0.01f
                || Math.Abs(refreshedDestination.Y - observation.SourceY) >= 0.01f)
            {
                return false;
            }

            if (!metadata.HasValue)
            {
                return true;
            }

            RemoteTownPortalFieldMetadata cachedMetadata = metadata.Value;
            return cachedMetadata.SourceMapId != refreshedDestination.MapId
                || Math.Abs(cachedMetadata.SourceX - refreshedDestination.X) >= 0.01f
                || Math.Abs(cachedMetadata.SourceY - refreshedDestination.Y) >= 0.01f;
        }

        private static bool ShouldReplaceRemoteTownPortalOwnerObservation(
            int existingSourceMapId,
            int existingTownMapId,
            RemoteTownPortalObservationSource existingObservationSource,
            int existingRecordedAt,
            int sourceMapId,
            int townMapId,
            RemoteTownPortalObservationSource observationSource,
            int recordedAt)
        {
            if (existingTownMapId != townMapId)
            {
                return true;
            }

            if (existingSourceMapId == sourceMapId)
            {
                int sameSourceAuthorityComparison = CompareRemoteTownPortalSameSourceCoordinateAuthority(
                    existingObservationSource,
                    existingRecordedAt,
                    observationSource,
                    recordedAt);
                return sameSourceAuthorityComparison < 0;
            }

            int qualityComparison = CompareRemoteTownPortalObservationQuality(
                observationSource,
                recordedAt: 0,
                existingObservationSource,
                recordedAtOther: 0);
            if (qualityComparison != 0)
            {
                return qualityComparison > 0;
            }

            if (existingObservationSource == observationSource
                && recordedAt != existingRecordedAt)
            {
                return CompareRemoteTownPortalRecordedAt(recordedAt, existingRecordedAt) > 0;
            }

            return existingSourceMapId != sourceMapId
                || existingObservationSource != observationSource;
        }

        private static int CompareRemoteTownPortalObservationQuality(
            RemoteTownPortalObservationSource observationSource,
            int recordedAt,
            RemoteTownPortalObservationSource otherObservationSource,
            int recordedAtOther)
        {
            int priority = GetRemoteTownPortalObservationPriority(observationSource);
            int otherPriority = GetRemoteTownPortalObservationPriority(otherObservationSource);
            if (priority != otherPriority)
            {
                return priority.CompareTo(otherPriority);
            }

            if (recordedAt != recordedAtOther)
            {
                return CompareRemoteTownPortalRecordedAt(recordedAt, recordedAtOther);
            }

            return 0;
        }

        private static int CompareRemoteTownPortalRecordedAt(int recordedAt, int otherRecordedAt)
        {
            if (recordedAt == otherRecordedAt)
            {
                return 0;
            }

            return unchecked(recordedAt - otherRecordedAt) > 0 ? 1 : -1;
        }

        private static int CompareRemoteTownPortalSameSourceCoordinateAuthority(
            RemoteTownPortalObservationSource metadataObservationSource,
            int metadataRecordedAt,
            RemoteTownPortalObservationSource observationSource,
            int observationRecordedAt)
        {
            int metadataPositionAuthority = GetRemoteTownPortalPositionAuthority(metadataObservationSource);
            int observationPositionAuthority = GetRemoteTownPortalPositionAuthority(observationSource);
            if (metadataPositionAuthority != observationPositionAuthority)
            {
                return metadataPositionAuthority.CompareTo(observationPositionAuthority);
            }

            if (metadataRecordedAt != observationRecordedAt)
            {
                return CompareRemoteTownPortalRecordedAt(metadataRecordedAt, observationRecordedAt);
            }

            return CompareRemoteTownPortalObservationQuality(
                metadataObservationSource,
                metadataRecordedAt,
                observationSource,
                observationRecordedAt);
        }

        private static int GetRemoteTownPortalObservationPriority(RemoteTownPortalObservationSource observationSource)
        {
            return observationSource switch
            {
                RemoteTownPortalObservationSource.WzReturnMapFallback => 0,
                RemoteTownPortalObservationSource.MovementSnapshot => 1,
                RemoteTownPortalObservationSource.InferredSourceField => 2,
                RemoteTownPortalObservationSource.LastLiveLeaveField => 3,
                RemoteTownPortalObservationSource.EnterField => 4,
                RemoteTownPortalObservationSource.FollowTransfer => 5,
                RemoteTownPortalObservationSource.SkillCast => 5,
                RemoteTownPortalObservationSource.PacketCast => 6,
                _ => 0
            };
        }

        private static int GetRemoteTownPortalPositionAuthority(RemoteTownPortalObservationSource observationSource)
        {
            return observationSource switch
            {
                RemoteTownPortalObservationSource.WzReturnMapFallback => 0,
                RemoteTownPortalObservationSource.PacketCast => 3,
                RemoteTownPortalObservationSource.SkillCast => 3,
                RemoteTownPortalObservationSource.EnterField => 2,
                RemoteTownPortalObservationSource.FollowTransfer => 2,
                RemoteTownPortalObservationSource.LastLiveLeaveField => 1,
                RemoteTownPortalObservationSource.InferredSourceField => 1,
                RemoteTownPortalObservationSource.MovementSnapshot => 1,
                _ => 0
            };
        }

        private RemoteOpenGateRuntime UpsertRemoteOpenGateRuntime(
            RemoteOpenGateRuntime runtime,
            RemoteOpenGateState state,
            bool hasStablePartner,
            PortalVisualSet openingVisuals,
            PortalVisualSet soloVisuals,
            PortalVisualSet linkedVisuals,
            PortalVisualSet removalVisuals)
        {
            BaseDXDrawableItem drawable = CreateRemoteOpenGateDrawable(
                state,
                hasStablePartner,
                openingVisuals,
                soloVisuals,
                linkedVisuals,
                removalVisuals);

            if (runtime == null)
            {
                var mainSlot = new PortalDrawableSlot(drawable, state.X, state.Y);
                var portal = new TemporaryPortal(
                    _nextPortalId++,
                    TemporaryPortalKind.OpenGate,
                    TemporaryPortalSource.RemoteOpenGatePool,
                    state.MapId,
                    state.X,
                    state.Y,
                    int.MaxValue,
                    OpenGateTeleportDelayMs,
                    mainSlot)
                {
                    OwnerCharacterId = state.OwnerCharacterId,
                    PartyId = state.PartyId,
                    IsPrimarySlot = state.IsFirstSlot
                };
                _portals.Add(portal);
                return new RemoteOpenGateRuntime(portal, mainSlot);
            }

            runtime.Portal.MapId = state.MapId;
            runtime.Portal.X = state.X;
            runtime.Portal.Y = state.Y;
            runtime.Portal.OwnerCharacterId = state.OwnerCharacterId;
            runtime.Portal.PartyId = state.PartyId;
            runtime.Portal.IsPrimarySlot = state.IsFirstSlot;
            runtime.MainSlot.SetPosition(state.X, state.Y);
            runtime.MainSlot.SetDrawable(drawable);
            return runtime;
        }

        private static RemoteOpenGateVisualMode ResolveRemoteOpenGateVisualMode(RemoteOpenGateState state, bool hasStablePartner)
        {
            return state.Phase switch
            {
                RemoteOpenGateVisualPhase.Opening => RemoteOpenGateVisualMode.Opening,
                RemoteOpenGateVisualPhase.Removing => RemoteOpenGateVisualMode.Removing,
                _ when hasStablePartner => RemoteOpenGateVisualMode.Linked,
                _ => RemoteOpenGateVisualMode.Solo
            };
        }

        private static bool ShouldLinkRemoteOpenGatePortal(
            RemoteOpenGateVisualPhase phase,
            bool hasPartner,
            RemoteOpenGateVisualPhase partnerPhase)
        {
            return phase == RemoteOpenGateVisualPhase.Stable
                   && hasPartner
                   && partnerPhase == RemoteOpenGateVisualPhase.Stable;
        }

        private static bool HasRemoteOpenGateSameMapPartner(
            int mapId,
            int partnerMapId,
            RemoteOpenGateVisualPhase partnerPhase)
        {
            return mapId == partnerMapId
                   && partnerPhase != RemoteOpenGateVisualPhase.Removing;
        }

        private static BaseDXDrawableItem CreateRemoteOpenGateDrawable(
            RemoteOpenGateState state,
            bool hasStablePartner,
            PortalVisualSet openingVisuals,
            PortalVisualSet soloVisuals,
            PortalVisualSet linkedVisuals,
            PortalVisualSet removalVisuals)
        {
            return ResolveRemoteOpenGateVisualMode(state, hasStablePartner) switch
            {
                RemoteOpenGateVisualMode.Opening => openingVisuals.CreateOpeningDrawable(state.X, state.Y, state.PhaseStartedAt),
                RemoteOpenGateVisualMode.Linked => linkedVisuals.CreateLoopDrawable(state.X, state.Y, state.PhaseStartedAt),
                RemoteOpenGateVisualMode.Removing => removalVisuals.CreateOpeningDrawable(state.X, state.Y, state.PhaseStartedAt),
                _ => soloVisuals.CreateLoopDrawable(state.X, state.Y, state.PhaseStartedAt)
            };
        }

        private bool EnsureOpenGateVisuals(
            out PortalVisualSet openingVisuals,
            out PortalVisualSet soloVisuals,
            out PortalVisualSet linkedVisuals,
            out PortalVisualSet removalVisuals)
        {
            openingVisuals = _openGateOpeningVisuals;
            soloVisuals = _openGateSoloVisuals;
            linkedVisuals = _openGateLinkedVisuals;
            removalVisuals = _openGateRemovalVisuals;
            if (openingVisuals != null && soloVisuals != null && linkedVisuals != null && removalVisuals != null)
            {
                return true;
            }

            WzImage skillImage = Program.FindImage("Skill", "3510.img");
            if (skillImage?["skill"]?["35101005"] is not WzSubProperty skillProperty)
            {
                return false;
            }

            openingVisuals = LoadPortalVisualSet(skillProperty["cDoor"], TemporaryPortalKind.OpenGate);
            soloVisuals = LoadPortalVisualSet(skillProperty["sDoor"], TemporaryPortalKind.OpenGate);
            linkedVisuals = LoadPortalVisualSet(skillProperty["mDoor"], TemporaryPortalKind.OpenGate);
            removalVisuals = LoadPortalVisualSet(skillProperty["eDoor"], TemporaryPortalKind.OpenGate);
            if (openingVisuals == null || soloVisuals == null || linkedVisuals == null || removalVisuals == null)
            {
                return false;
            }

            _openGateOpeningVisuals = openingVisuals;
            _openGateSoloVisuals = soloVisuals;
            _openGateLinkedVisuals = linkedVisuals;
            _openGateRemovalVisuals = removalVisuals;
            return true;
        }

        private int GetOpenGateOpeningDurationMs()
        {
            return _openGateOpeningVisuals?.DurationMs > 0
                ? _openGateOpeningVisuals.DurationMs
                : DefaultOpenGateOpeningDurationMs;
        }

        private int GetOpenGateRemovalDurationMs()
        {
            return _openGateRemovalVisuals?.DurationMs > 0
                ? _openGateRemovalVisuals.DurationMs
                : DefaultOpenGateRemovalDurationMs;
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

        private static void ClearRemoteTownPortalInferenceState(
            IDictionary<RemoteTownPortalOwnerTownKey, RemoteTownPortalFieldMetadata> fieldMetadata,
            IDictionary<RemoteTownPortalOwnerTownKey, Dictionary<int, Dictionary<RemoteTownPortalObservationSource, RemoteTownPortalOwnerFieldObservation>>> ownerFieldObservations,
            IDictionary<uint, Dictionary<int, Dictionary<RemoteTownPortalObservationSource, RemoteTownPortalPendingOwnerFieldObservation>>> pendingOwnerFieldObservations)
        {
            fieldMetadata?.Clear();
            ownerFieldObservations?.Clear();
            pendingOwnerFieldObservations?.Clear();
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
            public TemporaryPortalDestination(int mapId, float x, float y, int delayMs, float sourceX, float sourceY)
            {
                MapId = mapId;
                X = x;
                Y = y;
                DelayMs = delayMs;
                SourceX = sourceX;
                SourceY = sourceY;
            }

            public int MapId { get; }
            public float X { get; }
            public float Y { get; }
            public int DelayMs { get; }
            public float SourceX { get; }
            public float SourceY { get; }
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
                DurationMs = _frames.Sum(frame => Math.Max(1, frame.Delay));
            }

            public TemporaryPortalKind Kind { get; }
            public int DurationMs { get; }

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
            public int MapId { get; set; }
            public float X { get; set; }
            public float Y { get; set; }
            public int ExpireTime { get; }
            public int DelayMs { get; }
            public IReadOnlyList<BaseDXDrawableItem> Drawables { get; set; }
            public int? LinkedPortalId { get; set; }
            public int? DirectDestinationMapId { get; set; }
            public float? DirectDestinationX { get; set; }
            public float? DirectDestinationY { get; set; }
            public uint? OwnerCharacterId { get; set; }
            public uint? PartyId { get; set; }
            public bool IsPrimarySlot { get; set; }
        }

        private readonly record struct RemoteOpenGateKey(uint OwnerCharacterId, bool IsFirstSlot);
        private readonly record struct RemoteTownPortalOwnerTownKey(uint OwnerCharacterId, int TownMapId);
        private readonly record struct RemoteTownPortalPortalCandidate(int? PortalTypeId, string PortalName, float X, float Y);

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
            float SourceX,
            float SourceY,
            int TownMapId,
            RemoteTownPortalObservationSource ObservationSource,
            int RecordedAt);

        private readonly record struct RemoteTownPortalPendingOwnerFieldObservation(
            int SourceMapId,
            float SourceX,
            float SourceY,
            RemoteTownPortalObservationSource ObservationSource,
            int RecordedAt);

        private readonly record struct RemoteTownPortalOwnerFieldObservation(
            int SourceMapId,
            float SourceX,
            float SourceY,
            int TownMapId,
            RemoteTownPortalObservationSource ObservationSource,
            int RecordedAt);

        private sealed record RemoteTownPortalRuntimePair(
            RemoteTownPortalRuntime FieldRuntime,
            RemoteTownPortalRuntime TownRuntime);

        private sealed record RemoteTownPortalRuntime(
            TemporaryPortal Portal,
            PortalDrawableSlot MainSlot,
            PortalDrawableSlot FrameSlot);

        private readonly record struct RemoteOpenGateState(
            uint OwnerCharacterId,
            byte State,
            int MapId,
            short X,
            short Y,
            bool IsFirstSlot,
            uint PartyId,
            RemoteOpenGateVisualPhase Phase,
            int PhaseStartedAt);

        private sealed record RemoteOpenGateRuntime(
            TemporaryPortal Portal,
            PortalDrawableSlot MainSlot);

        internal enum RemoteTownPortalVisualPhase
        {
            Opening,
            Stable,
            Removing
        }

        internal enum RemoteTownPortalObservationSource
        {
            MovementSnapshot,
            InferredSourceField,
            LastLiveLeaveField,
            EnterField,
            FollowTransfer,
            SkillCast,
            PacketCast,
            WzReturnMapFallback
        }

        internal enum RemoteTownPortalDestinationResolutionKind
        {
            None,
            OwnerOrCachedObservation,
            SourceMapReturnFallback,
            WzPreferredOrUniqueFallback
        }

        internal enum RemoteOpenGateVisualPhase
        {
            Opening,
            Stable,
            Removing
        }

        internal enum RemoteOpenGateVisualMode
        {
            Opening,
            Solo,
            Linked,
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

        private sealed class PortalDrawableSlot : BaseDXDrawableItem
        {
            private BaseDXDrawableItem _drawable;

            public PortalDrawableSlot(BaseDXDrawableItem drawable, float x, float y)
                : base((drawable ?? throw new ArgumentNullException(nameof(drawable))).Frame0, false)
            {
                Position = new Point(-(int)MathF.Round(x), -(int)MathF.Round(y));
                _drawable = drawable;
            }

            public void SetPosition(float x, float y)
            {
                Position = new Point(-(int)MathF.Round(x), -(int)MathF.Round(y));
            }

            public void SetDrawable(BaseDXDrawableItem drawable)
            {
                _drawable = drawable;
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
                if (_drawable == null)
                {
                    return;
                }

                _drawable.Position = Position;
                _drawable.Draw(
                    sprite,
                    skeletonMeshRenderer,
                    gameTime,
                    mapShiftX,
                    mapShiftY,
                    centerX,
                    centerY,
                    drawReflectionInfo,
                    renderParameters,
                    TickCount);
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
                && unchecked(currentTime - phaseStartedAt) >= DefaultTownPortalOpeningDurationMs)
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

        internal static (bool MetadataCleared, bool ObservationsCleared, bool PendingCleared) ClearRemoteTownPortalInferenceStateForTesting()
        {
            Dictionary<RemoteTownPortalOwnerTownKey, RemoteTownPortalFieldMetadata> fieldMetadata = new()
            {
                [new RemoteTownPortalOwnerTownKey(1, 100000000)] = new RemoteTownPortalFieldMetadata(
                    SourceMapId: 101000000,
                    SourceX: 10,
                    SourceY: 20,
                    TownMapId: 100000000,
                    ObservationSource: RemoteTownPortalObservationSource.WzReturnMapFallback,
                    RecordedAt: 1)
            };
            Dictionary<RemoteTownPortalOwnerTownKey, Dictionary<int, Dictionary<RemoteTownPortalObservationSource, RemoteTownPortalOwnerFieldObservation>>> ownerFieldObservations = new()
            {
                [new RemoteTownPortalOwnerTownKey(1, 100000000)] = new Dictionary<int, Dictionary<RemoteTownPortalObservationSource, RemoteTownPortalOwnerFieldObservation>>
                {
                    [101000000] = new Dictionary<RemoteTownPortalObservationSource, RemoteTownPortalOwnerFieldObservation>
                    {
                        [RemoteTownPortalObservationSource.MovementSnapshot] = new RemoteTownPortalOwnerFieldObservation(
                            SourceMapId: 101000000,
                            SourceX: 30,
                            SourceY: 40,
                            TownMapId: 100000000,
                            ObservationSource: RemoteTownPortalObservationSource.MovementSnapshot,
                            RecordedAt: 2)
                    }
                }
            };
            Dictionary<uint, Dictionary<int, Dictionary<RemoteTownPortalObservationSource, RemoteTownPortalPendingOwnerFieldObservation>>> pendingOwnerFieldObservations = new()
            {
                [1] = new Dictionary<int, Dictionary<RemoteTownPortalObservationSource, RemoteTownPortalPendingOwnerFieldObservation>>
                {
                    [101000000] = new Dictionary<RemoteTownPortalObservationSource, RemoteTownPortalPendingOwnerFieldObservation>
                    {
                        [RemoteTownPortalObservationSource.EnterField] = new RemoteTownPortalPendingOwnerFieldObservation(
                            SourceMapId: 101000000,
                            SourceX: 50,
                            SourceY: 60,
                            ObservationSource: RemoteTownPortalObservationSource.EnterField,
                            RecordedAt: 3)
                    }
                }
            };

            ClearRemoteTownPortalInferenceState(fieldMetadata, ownerFieldObservations, pendingOwnerFieldObservations);
            return (fieldMetadata.Count == 0, ownerFieldObservations.Count == 0, pendingOwnerFieldObservations.Count == 0);
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

            return NormalizeRemoteTownPortalIncomingDestination(currentMapId, incomingDestination);
        }

        internal static RemoteTownPortalResolvedDestination? ResolveRemoteTownPortalObservedFieldDestinationForTesting(
            int currentMapId,
            int sourceMapId,
            float sourceX,
            float sourceY,
            int townMapId)
        {
            return ResolveRemoteTownPortalObservedFieldDestination(currentMapId, sourceMapId, sourceX, sourceY, townMapId);
        }

        internal static RemoteTownPortalVisualPhase ResolveRemoteTownPortalCreatePhaseForTesting(
            byte packetState,
            bool hasExistingState,
            byte existingPacketState,
            RemoteTownPortalVisualPhase existingPhase)
        {
            RemoteTownPortalState? existingState = hasExistingState
                ? new RemoteTownPortalState(
                    OwnerCharacterId: 1,
                    State: existingPacketState,
                    MapId: 100000000,
                    X: 0,
                    Y: 0,
                    Destination: null,
                    Phase: existingPhase,
                    PhaseStartedAt: 0,
                    RemovalState: null,
                    RemovalSnapshot: null)
                : null;
            return ResolveRemoteTownPortalCreatePhase(packetState, existingState);
        }

        internal static byte ResolveRemoteTownPortalCreateStateForTesting(
            byte packetState,
            bool hasExistingState,
            byte existingPacketState,
            RemoteTownPortalVisualPhase existingPhase)
        {
            RemoteTownPortalState? existingState = hasExistingState
                ? new RemoteTownPortalState(
                    OwnerCharacterId: 1,
                    State: existingPacketState,
                    MapId: 100000000,
                    X: 0,
                    Y: 0,
                    Destination: null,
                    Phase: existingPhase,
                    PhaseStartedAt: 0,
                    RemovalState: null,
                    RemovalSnapshot: null)
                : null;
            return ResolveRemoteTownPortalCreateState(packetState, existingState);
        }

        internal static bool HasActiveRemoteTownPortalStateForTownMapForTesting(
            int existingStateMapId,
            RemoteTownPortalVisualPhase existingPhase,
            int townMapId)
        {
            RemoteTownPortalState existingState = new(
                OwnerCharacterId: 1,
                State: 1,
                MapId: existingStateMapId,
                X: 0,
                Y: 0,
                Destination: new RemoteTownPortalResolvedDestination(101000000, 10, 20),
                Phase: existingPhase,
                PhaseStartedAt: 0,
                RemovalState: null,
                RemovalSnapshot: null);
            return FilterRemoteTownPortalStateByTownMap(existingState, townMapId).HasValue;
        }

        internal static RemoteTownPortalVisualPhase ResolveRemoteTownPortalCreatePhaseForTesting(
            byte packetState,
            bool hasExistingState,
            RemoteTownPortalVisualPhase existingPhase)
        {
            return ResolveRemoteTownPortalCreatePhaseForTesting(packetState, hasExistingState, existingPacketState: 1, existingPhase);
        }

        internal static RemoteTownPortalResolvedDestination? ChooseRemoteTownPortalResolvedDestinationForTesting(
            int currentMapId,
            bool hasMetadata,
            int metadataSourceMapId,
            float metadataSourceX,
            float metadataSourceY,
            int metadataTownMapId,
            RemoteTownPortalObservationSource metadataObservationSource,
            int metadataRecordedAt,
            bool hasObservation,
            int observationSourceMapId,
            float observationSourceX,
            float observationSourceY,
            int observationTownMapId,
            RemoteTownPortalObservationSource observationSource,
            int observationRecordedAt)
        {
            RemoteTownPortalResolvedDestination? metadataDestination = hasMetadata
                ? ResolveRemoteTownPortalObservedFieldDestination(currentMapId, metadataSourceMapId, metadataSourceX, metadataSourceY, metadataTownMapId)
                : null;
            RemoteTownPortalResolvedDestination? observationDestination = hasObservation
                ? ResolveRemoteTownPortalObservedFieldDestination(currentMapId, observationSourceMapId, observationSourceX, observationSourceY, observationTownMapId)
                : null;

            if (!metadataDestination.HasValue && !observationDestination.HasValue)
            {
                return null;
            }

            RemoteTownPortalFieldMetadata metadata = new(
                metadataSourceMapId,
                metadataSourceX,
                metadataSourceY,
                metadataTownMapId,
                metadataObservationSource,
                metadataRecordedAt);
            RemoteTownPortalOwnerFieldObservation observation = new(
                observationSourceMapId,
                observationSourceX,
                observationSourceY,
                observationTownMapId,
                observationSource,
                observationRecordedAt);
            return ChooseRemoteTownPortalResolvedDestination(metadata, metadataDestination, observation, observationDestination);
        }

        internal static RemoteTownPortalResolvedDestination? ResolveRemoteTownPortalDestinationFromCachedStateForTesting(
            int currentMapId,
            bool hasIncomingDestination,
            int incomingDestinationMapId,
            float incomingDestinationX,
            float incomingDestinationY,
            bool hasExistingDestination,
            int existingDestinationMapId,
            float existingDestinationX,
            float existingDestinationY,
            bool hasMetadata,
            int metadataSourceMapId,
            float metadataSourceX,
            float metadataSourceY,
            int metadataTownMapId,
            RemoteTownPortalObservationSource metadataObservationSource,
            int metadataRecordedAt,
            bool hasObservation,
            int observationSourceMapId,
            float observationSourceX,
            float observationSourceY,
            int observationTownMapId,
            RemoteTownPortalObservationSource observationSource,
            int observationRecordedAt)
        {
            RemoteTownPortalResolvedDestination? incomingDestination = hasIncomingDestination
                ? new RemoteTownPortalResolvedDestination(incomingDestinationMapId, incomingDestinationX, incomingDestinationY)
                : null;
            RemoteTownPortalState? existingState = hasExistingDestination
                ? new RemoteTownPortalState(
                    OwnerCharacterId: 1,
                    State: 1,
                    MapId: currentMapId,
                    X: 0,
                    Y: 0,
                    Destination: new RemoteTownPortalResolvedDestination(existingDestinationMapId, existingDestinationX, existingDestinationY),
                    Phase: RemoteTownPortalVisualPhase.Stable,
                    PhaseStartedAt: 0,
                    RemovalState: null,
                    RemovalSnapshot: null)
                : null;
            RemoteTownPortalFieldMetadata? metadata = hasMetadata
                ? new RemoteTownPortalFieldMetadata(
                    metadataSourceMapId,
                    metadataSourceX,
                    metadataSourceY,
                    metadataTownMapId,
                    metadataObservationSource,
                    metadataRecordedAt)
                : null;
            RemoteTownPortalOwnerFieldObservation? observation = hasObservation
                ? new RemoteTownPortalOwnerFieldObservation(
                    observationSourceMapId,
                    observationSourceX,
                    observationSourceY,
                    observationTownMapId,
                    observationSource,
                    observationRecordedAt)
                : null;
            return ResolveRemoteTownPortalDestination(
                currentMapId,
                incomingDestination,
                existingState,
                metadata,
                observation);
        }

        internal static RemoteTownPortalResolvedDestination? ResolveRemoteTownPortalDestinationFromObservedCandidatesForTesting(
            int currentMapId,
            bool hasIncomingDestination,
            int incomingDestinationMapId,
            float incomingDestinationX,
            float incomingDestinationY,
            bool hasExistingDestination,
            int existingDestinationMapId,
            float existingDestinationX,
            float existingDestinationY,
            bool hasMetadata,
            int metadataSourceMapId,
            float metadataSourceX,
            float metadataSourceY,
            int metadataTownMapId,
            RemoteTownPortalObservationSource metadataObservationSource,
            int metadataRecordedAt,
            params (int SourceMapId, float SourceX, float SourceY, int TownMapId, RemoteTownPortalObservationSource ObservationSource, int RecordedAt)[] observations)
        {
            RemoteTownPortalResolvedDestination? incomingDestination = hasIncomingDestination
                ? new RemoteTownPortalResolvedDestination(incomingDestinationMapId, incomingDestinationX, incomingDestinationY)
                : null;
            RemoteTownPortalState? existingState = hasExistingDestination
                ? new RemoteTownPortalState(
                    OwnerCharacterId: 1,
                    State: 1,
                    MapId: currentMapId,
                    X: 0,
                    Y: 0,
                    Destination: new RemoteTownPortalResolvedDestination(existingDestinationMapId, existingDestinationX, existingDestinationY),
                    Phase: RemoteTownPortalVisualPhase.Stable,
                    PhaseStartedAt: 0,
                    RemovalState: null,
                    RemovalSnapshot: null)
                : null;
            RemoteTownPortalFieldMetadata? metadata = hasMetadata
                ? new RemoteTownPortalFieldMetadata(
                    metadataSourceMapId,
                    metadataSourceX,
                    metadataSourceY,
                    metadataTownMapId,
                    metadataObservationSource,
                    metadataRecordedAt)
                : null;
            List<RemoteTownPortalOwnerFieldObservation[]> observationGroups = observations
                .Select(observation => new RemoteTownPortalOwnerFieldObservation(
                    observation.SourceMapId,
                    observation.SourceX,
                    observation.SourceY,
                    observation.TownMapId,
                    observation.ObservationSource,
                    observation.RecordedAt))
                .GroupBy(observation => observation.SourceMapId)
                .Select(group => group.ToArray())
                .ToList();
            int? preferredSourceMapId = SelectPreferredRemoteTownPortalSourceMapId(
                existingState,
                metadata,
                observationGroups);
            RemoteTownPortalOwnerFieldObservation? ownerObservation = preferredSourceMapId.HasValue
                ? SelectPreferredRemoteTownPortalOwnerObservationForSourceMap(observationGroups, preferredSourceMapId.Value)
                : null;
            return ResolveRemoteTownPortalDestination(
                currentMapId,
                incomingDestination,
                existingState,
                metadata,
                ownerObservation,
                preferredSourceMapId);
        }

        internal static int? ResolvePreferredRemoteTownPortalSourceMapIdForTesting(
            int currentMapId,
            bool hasExistingDestination,
            int existingDestinationMapId,
            float existingDestinationX,
            float existingDestinationY,
            bool hasMetadata,
            int metadataSourceMapId,
            float metadataSourceX,
            float metadataSourceY,
            int metadataTownMapId,
            RemoteTownPortalObservationSource metadataObservationSource,
            int metadataRecordedAt,
            params (int SourceMapId, float SourceX, float SourceY, int TownMapId, RemoteTownPortalObservationSource ObservationSource, int RecordedAt)[] observations)
        {
            RemoteTownPortalState? existingState = hasExistingDestination
                ? new RemoteTownPortalState(
                    OwnerCharacterId: 1,
                    State: 1,
                    MapId: currentMapId,
                    X: 0,
                    Y: 0,
                    Destination: new RemoteTownPortalResolvedDestination(existingDestinationMapId, existingDestinationX, existingDestinationY),
                    Phase: RemoteTownPortalVisualPhase.Stable,
                    PhaseStartedAt: 0,
                    RemovalState: null,
                    RemovalSnapshot: null)
                : null;
            RemoteTownPortalFieldMetadata? metadata = hasMetadata
                ? new RemoteTownPortalFieldMetadata(
                    metadataSourceMapId,
                    metadataSourceX,
                    metadataSourceY,
                    metadataTownMapId,
                    metadataObservationSource,
                    metadataRecordedAt)
                : null;
            List<RemoteTownPortalOwnerFieldObservation[]> observationGroups = observations
                .Select(observation => new RemoteTownPortalOwnerFieldObservation(
                    observation.SourceMapId,
                    observation.SourceX,
                    observation.SourceY,
                    observation.TownMapId,
                    observation.ObservationSource,
                    observation.RecordedAt))
                .GroupBy(observation => observation.SourceMapId)
                .Select(group => group.ToArray())
                .ToList();
            return SelectPreferredRemoteTownPortalSourceMapId(
                existingState,
                metadata,
                observationGroups);
        }

        internal static int? ResolvePreferredRemoteTownPortalSourceMapIdForTesting(
            int currentMapId,
            bool hasExistingDestination,
            int existingStateMapId,
            int existingDestinationMapId,
            float existingDestinationX,
            float existingDestinationY,
            bool hasMetadata,
            int metadataSourceMapId,
            float metadataSourceX,
            float metadataSourceY,
            int metadataTownMapId,
            RemoteTownPortalObservationSource metadataObservationSource,
            int metadataRecordedAt,
            params (int SourceMapId, float SourceX, float SourceY, int TownMapId, RemoteTownPortalObservationSource ObservationSource, int RecordedAt)[] observations)
        {
            RemoteTownPortalState? existingState = hasExistingDestination
                ? new RemoteTownPortalState(
                    OwnerCharacterId: 1,
                    State: 1,
                    MapId: existingStateMapId,
                    X: 0,
                    Y: 0,
                    Destination: new RemoteTownPortalResolvedDestination(existingDestinationMapId, existingDestinationX, existingDestinationY),
                    Phase: RemoteTownPortalVisualPhase.Stable,
                    PhaseStartedAt: 0,
                    RemovalState: null,
                    RemovalSnapshot: null)
                : null;
            RemoteTownPortalFieldMetadata? metadata = hasMetadata
                ? new RemoteTownPortalFieldMetadata(
                    metadataSourceMapId,
                    metadataSourceX,
                    metadataSourceY,
                    metadataTownMapId,
                    metadataObservationSource,
                    metadataRecordedAt)
                : null;
            List<RemoteTownPortalOwnerFieldObservation[]> observationGroups = observations
                .Select(observation => new RemoteTownPortalOwnerFieldObservation(
                    observation.SourceMapId,
                    observation.SourceX,
                    observation.SourceY,
                    observation.TownMapId,
                    observation.ObservationSource,
                    observation.RecordedAt))
                .GroupBy(observation => observation.SourceMapId)
                .Select(group => group.ToArray())
                .ToList();
            return SelectPreferredRemoteTownPortalSourceMapId(
                FilterRemoteTownPortalStateByTownMap(existingState, currentMapId),
                metadata,
                observationGroups);
        }

        internal static int? ResolvePreferredRemoteTownPortalSourceMapIdWithExistingPhaseForTesting(
            int currentMapId,
            bool hasExistingDestination,
            int existingDestinationMapId,
            float existingDestinationX,
            float existingDestinationY,
            RemoteTownPortalVisualPhase existingPhase,
            bool hasMetadata,
            int metadataSourceMapId,
            float metadataSourceX,
            float metadataSourceY,
            int metadataTownMapId,
            RemoteTownPortalObservationSource metadataObservationSource,
            int metadataRecordedAt,
            params (int SourceMapId, float SourceX, float SourceY, int TownMapId, RemoteTownPortalObservationSource ObservationSource, int RecordedAt)[] observations)
        {
            RemoteTownPortalState? existingState = hasExistingDestination
                ? new RemoteTownPortalState(
                    OwnerCharacterId: 1,
                    State: 1,
                    MapId: currentMapId,
                    X: 0,
                    Y: 0,
                    Destination: new RemoteTownPortalResolvedDestination(existingDestinationMapId, existingDestinationX, existingDestinationY),
                    Phase: existingPhase,
                    PhaseStartedAt: 0,
                    RemovalState: null,
                    RemovalSnapshot: null)
                : null;
            RemoteTownPortalFieldMetadata? metadata = hasMetadata
                ? new RemoteTownPortalFieldMetadata(
                    metadataSourceMapId,
                    metadataSourceX,
                    metadataSourceY,
                    metadataTownMapId,
                    metadataObservationSource,
                    metadataRecordedAt)
                : null;
            List<RemoteTownPortalOwnerFieldObservation[]> observationGroups = observations
                .Select(observation => new RemoteTownPortalOwnerFieldObservation(
                    observation.SourceMapId,
                    observation.SourceX,
                    observation.SourceY,
                    observation.TownMapId,
                    observation.ObservationSource,
                    observation.RecordedAt))
                .GroupBy(observation => observation.SourceMapId)
                .Select(group => group.ToArray())
                .ToList();
            return SelectPreferredRemoteTownPortalSourceMapId(
                existingState,
                metadata,
                observationGroups);
        }

        internal static RemoteTownPortalResolvedDestination? ResolveRemoteTownPortalDestinationFromPendingObservedCandidatesForTesting(
            int currentMapId,
            bool hasIncomingDestination,
            int incomingDestinationMapId,
            float incomingDestinationX,
            float incomingDestinationY,
            bool hasExistingDestination,
            int existingDestinationMapId,
            float existingDestinationX,
            float existingDestinationY,
            bool hasMetadata,
            int metadataSourceMapId,
            float metadataSourceX,
            float metadataSourceY,
            int metadataTownMapId,
            RemoteTownPortalObservationSource metadataObservationSource,
            int metadataRecordedAt,
            params (int SourceMapId, float SourceX, float SourceY, int ResolvedTownMapId, RemoteTownPortalObservationSource ObservationSource, int RecordedAt)[] pendingObservations)
        {
            RemoteTownPortalResolvedDestination? incomingDestination = hasIncomingDestination
                ? new RemoteTownPortalResolvedDestination(incomingDestinationMapId, incomingDestinationX, incomingDestinationY)
                : null;
            RemoteTownPortalState? existingState = hasExistingDestination
                ? new RemoteTownPortalState(
                    OwnerCharacterId: 1,
                    State: 1,
                    MapId: currentMapId,
                    X: 0,
                    Y: 0,
                    Destination: new RemoteTownPortalResolvedDestination(existingDestinationMapId, existingDestinationX, existingDestinationY),
                    Phase: RemoteTownPortalVisualPhase.Stable,
                    PhaseStartedAt: 0,
                    RemovalState: null,
                    RemovalSnapshot: null)
                : null;
            RemoteTownPortalFieldMetadata? metadata = hasMetadata
                ? new RemoteTownPortalFieldMetadata(
                    metadataSourceMapId,
                    metadataSourceX,
                    metadataSourceY,
                    metadataTownMapId,
                    metadataObservationSource,
                    metadataRecordedAt)
                : null;
            List<RemoteTownPortalOwnerFieldObservation[]> observationGroups = pendingObservations
                .Where(observation => observation.ResolvedTownMapId == currentMapId)
                .Select(observation => new RemoteTownPortalOwnerFieldObservation(
                    observation.SourceMapId,
                    observation.SourceX,
                    observation.SourceY,
                    currentMapId,
                    observation.ObservationSource,
                    observation.RecordedAt))
                .GroupBy(observation => observation.SourceMapId)
                .Select(group => group.ToArray())
                .ToList();
            int? preferredSourceMapId = SelectPreferredRemoteTownPortalSourceMapId(
                existingState,
                metadata,
                observationGroups);
            RemoteTownPortalOwnerFieldObservation? ownerObservation = preferredSourceMapId.HasValue
                ? SelectPreferredRemoteTownPortalOwnerObservationForSourceMap(observationGroups, preferredSourceMapId.Value)
                : null;
            return ResolveRemoteTownPortalDestination(
                currentMapId,
                incomingDestination,
                existingState,
                metadata,
                ownerObservation,
                preferredSourceMapId);
        }

        internal static RemoteTownPortalResolvedDestination? ResolveUniqueRemoteTownPortalWzFallbackDestinationForTesting(
            int townMapId,
            params (int SourceMapId, int ReturnMapId, int ForcedReturnMapId, bool HasSourcePosition, float SourceX, float SourceY)[] candidates)
        {
            if (townMapId <= 0 || candidates == null || candidates.Length == 0)
            {
                return null;
            }

            (int SourceMapId, int ReturnMapId, int ForcedReturnMapId, bool HasSourcePosition, float SourceX, float SourceY)? selected = null;
            int matchCount = 0;
            foreach ((int SourceMapId, int ReturnMapId, int ForcedReturnMapId, bool HasSourcePosition, float SourceX, float SourceY) candidate in candidates)
            {
                if (candidate.SourceMapId <= 0 || candidate.SourceMapId == townMapId)
                {
                    continue;
                }

                if (NormalizeRemoteTownPortalConfiguredReturnMap(candidate.ReturnMapId, candidate.ForcedReturnMapId) != townMapId)
                {
                    continue;
                }

                matchCount++;
                selected = candidate;
            }

            if (matchCount != 1 || selected?.HasSourcePosition != true)
            {
                return null;
            }

            return new RemoteTownPortalResolvedDestination(
                selected.Value.SourceMapId,
                selected.Value.SourceX,
                selected.Value.SourceY);
        }

        internal static RemoteTownPortalResolvedDestination? ResolveRemoteTownPortalWzFallbackDestinationWithPreferredSourceForTesting(
            int townMapId,
            bool hasPreferredSourceMap,
            int preferredSourceMapId,
            bool preferredSourceResolvesToTownMap,
            bool preferredSourceHasPosition,
            float preferredSourceX,
            float preferredSourceY,
            params (int SourceMapId, int ReturnMapId, int ForcedReturnMapId, bool HasSourcePosition, float SourceX, float SourceY)[] candidates)
        {
            if (hasPreferredSourceMap
                && preferredSourceMapId > 0
                && preferredSourceMapId != townMapId
                && preferredSourceResolvesToTownMap)
            {
                return preferredSourceHasPosition
                    ? new RemoteTownPortalResolvedDestination(
                        preferredSourceMapId,
                        preferredSourceX,
                        preferredSourceY)
                    : null;
            }

            return ResolveUniqueRemoteTownPortalWzFallbackDestinationForTesting(townMapId, candidates);
        }

        internal static RemoteTownPortalResolvedDestination? ResolveRemoteTownPortalCurrentMapReturnDestinationForTesting(
            int currentMapId,
            bool currentMapHasTownPortalPoint,
            bool hasCurrentMapReturnTownMap,
            int currentMapReturnTownMapId,
            bool returnTownHasDestinationPosition,
            float returnTownDestinationX,
            float returnTownDestinationY)
        {
            if (currentMapId <= 0
                || currentMapHasTownPortalPoint
                || !hasCurrentMapReturnTownMap
                || currentMapReturnTownMapId <= 0
                || currentMapReturnTownMapId == currentMapId
                || !returnTownHasDestinationPosition)
            {
                return null;
            }

            return new RemoteTownPortalResolvedDestination(
                currentMapReturnTownMapId,
                returnTownDestinationX,
                returnTownDestinationY);
        }

        internal static int NormalizeRemoteTownPortalConfiguredReturnMapForTesting(
            int returnMapId,
            int forcedReturnMapId)
        {
            return NormalizeRemoteTownPortalConfiguredReturnMap(returnMapId, forcedReturnMapId);
        }

        internal static bool CanRememberRemoteTownPortalPacketCastMetadataFromResolvedFallbackForTesting(
            int currentMapId,
            int resolvedDestinationMapId,
            bool currentMapHasTownPortalPoint,
            bool hasCurrentMapReturnTownMap,
            int currentMapReturnTownMapId)
        {
            return CanRememberRemoteTownPortalPacketCastMetadataFromResolvedFallback(
                currentMapId,
                resolvedDestinationMapId,
                currentMapHasTownPortalPoint,
                hasCurrentMapReturnTownMap,
                currentMapReturnTownMapId);
        }

        internal static bool ShouldRememberRemoteTownPortalPacketCastMetadataFromResolvedDestinationForTesting(
            bool resolvedFromSourceMapReturnFallback,
            int currentMapId,
            int resolvedDestinationMapId,
            bool currentMapHasTownPortalPoint,
            bool hasCurrentMapReturnTownMap,
            int currentMapReturnTownMapId)
        {
            return ShouldRememberRemoteTownPortalPacketCastMetadataFromResolvedDestination(
                resolvedFromSourceMapReturnFallback,
                currentMapId,
                resolvedDestinationMapId,
                currentMapHasTownPortalPoint,
                hasCurrentMapReturnTownMap,
                currentMapReturnTownMapId);
        }

        internal static bool ShouldRememberRemoteTownPortalPacketCastMetadataForResolutionKindForTesting(
            RemoteTownPortalDestinationResolutionKind resolutionKind,
            int currentMapId,
            int resolvedDestinationMapId,
            bool currentMapHasTownPortalPoint,
            bool hasCurrentMapReturnTownMap,
            int currentMapReturnTownMapId)
        {
            return ShouldRememberRemoteTownPortalPacketCastMetadataFromResolvedDestination(
                resolutionKind == RemoteTownPortalDestinationResolutionKind.SourceMapReturnFallback,
                currentMapId,
                resolvedDestinationMapId,
                currentMapHasTownPortalPoint,
                hasCurrentMapReturnTownMap,
                currentMapReturnTownMapId);
        }

        internal static bool IsRemoteTownPortalSourceFallbackStartPortalForTesting(int? portalTypeId, string portalName)
        {
            return IsRemoteTownPortalSourceFallbackStartPortal(portalTypeId, portalName);
        }

        internal static bool TrySelectRemoteTownPortalTownDestinationFallbackPositionForTesting(
            out float townX,
            out float townY,
            params (int? PortalTypeId, string PortalName, float X, float Y)[] portals)
        {
            return TrySelectRemoteTownPortalTownDestinationFallbackPosition(
                portals?.Select(portal => new RemoteTownPortalPortalCandidate(
                    portal.PortalTypeId,
                    portal.PortalName,
                    portal.X,
                    portal.Y)),
                out townX,
                out townY);
        }

        internal static bool TrySelectRemoteTownPortalSourceFallbackPositionForTesting(
            out float sourceX,
            out float sourceY,
            params (int? PortalTypeId, string PortalName, float X, float Y)[] portals)
        {
            return TrySelectRemoteTownPortalSourceFallbackPosition(
                portals?.Select(portal => new RemoteTownPortalPortalCandidate(
                    portal.PortalTypeId,
                    portal.PortalName,
                    portal.X,
                    portal.Y)),
                out sourceX,
                out sourceY);
        }

        internal static bool ShouldReplaceRemoteTownPortalOwnerObservationForTesting(
            int existingSourceMapId,
            int existingTownMapId,
            RemoteTownPortalObservationSource existingObservationSource,
            int existingRecordedAt,
            int sourceMapId,
            int townMapId,
            RemoteTownPortalObservationSource newObservationSource,
            int newRecordedAt)
        {
            RemoteTownPortalOwnerFieldObservation existingObservation = new(
                existingSourceMapId,
                0,
                0,
                existingTownMapId,
                existingObservationSource,
                existingRecordedAt);

            return ShouldReplaceRemoteTownPortalOwnerObservation(
                existingObservation.SourceMapId,
                existingObservation.TownMapId,
                existingObservation.ObservationSource,
                existingObservation.RecordedAt,
                sourceMapId,
                townMapId,
                newObservationSource,
                newRecordedAt);
        }

        internal static bool ShouldRefreshRemoteTownPortalInferredMetadataForTesting(
            bool hasMetadata,
            int metadataSourceMapId,
            float metadataSourceX,
            float metadataSourceY,
            int metadataTownMapId,
            RemoteTownPortalObservationSource metadataObservationSource,
            int metadataRecordedAt,
            bool hasObservation,
            int observationSourceMapId,
            float observationSourceX,
            float observationSourceY,
            int observationTownMapId,
            RemoteTownPortalObservationSource observationSource,
            int observationRecordedAt,
            int refreshedDestinationMapId,
            float refreshedDestinationX,
            float refreshedDestinationY)
        {
            RemoteTownPortalFieldMetadata? metadata = hasMetadata
                ? new RemoteTownPortalFieldMetadata(
                    metadataSourceMapId,
                    metadataSourceX,
                    metadataSourceY,
                    metadataTownMapId,
                    metadataObservationSource,
                    metadataRecordedAt)
                : null;
            RemoteTownPortalOwnerFieldObservation? observation = hasObservation
                ? new RemoteTownPortalOwnerFieldObservation(
                    observationSourceMapId,
                    observationSourceX,
                    observationSourceY,
                    observationTownMapId,
                    observationSource,
                    observationRecordedAt)
                : null;

            return ShouldRefreshRemoteTownPortalInferredMetadata(
                metadata,
                observation,
                new RemoteTownPortalResolvedDestination(
                    refreshedDestinationMapId,
                    refreshedDestinationX,
                    refreshedDestinationY));
        }

        internal static bool CanAdoptPendingRemoteTownPortalObservationFromOwnerTownIdentityForTesting(
            int townMapId,
            int sourceMapId,
            bool hasMetadata,
            int metadataSourceMapId,
            int metadataTownMapId,
            bool hasExistingState,
            int existingStateMapId,
            bool hasExistingDestination,
            int existingDestinationMapId)
        {
            RemoteTownPortalFieldMetadata? metadata = hasMetadata
                ? new RemoteTownPortalFieldMetadata(
                    metadataSourceMapId,
                    SourceX: 0,
                    SourceY: 0,
                    metadataTownMapId,
                    RemoteTownPortalObservationSource.InferredSourceField,
                    RecordedAt: 0)
                : null;
            RemoteTownPortalState? existingState = hasExistingState
                ? new RemoteTownPortalState(
                    OwnerCharacterId: 1,
                    State: 1,
                    MapId: existingStateMapId,
                    X: 0,
                    Y: 0,
                    Destination: hasExistingDestination
                        ? new RemoteTownPortalResolvedDestination(existingDestinationMapId, 0, 0)
                        : null,
                    Phase: RemoteTownPortalVisualPhase.Stable,
                    PhaseStartedAt: 0,
                    RemovalState: null,
                    RemovalSnapshot: null)
                : null;
            return CanAdoptPendingRemoteTownPortalObservationFromOwnerTownIdentity(
                townMapId,
                sourceMapId,
                metadata,
                existingState);
        }

        internal static bool ShouldLinkRemoteTownPortalForTesting(
            RemoteTownPortalVisualPhase phase,
            bool hasDestination)
        {
            return hasDestination
                   && phase != RemoteTownPortalVisualPhase.Removing;
        }

        internal static RemoteOpenGateVisualPhase AdvanceRemoteOpenGatePhaseForTesting(RemoteOpenGateVisualPhase phase, int phaseStartedAt, int currentTime)
        {
            if (phase == RemoteOpenGateVisualPhase.Opening
                && unchecked(currentTime - phaseStartedAt) >= DefaultOpenGateOpeningDurationMs)
            {
                return RemoteOpenGateVisualPhase.Stable;
            }

            if (phase == RemoteOpenGateVisualPhase.Removing
                && unchecked(currentTime - phaseStartedAt) >= DefaultOpenGateRemovalDurationMs)
            {
                return RemoteOpenGateVisualPhase.Removing;
            }

            return phase;
        }

        internal static RemoteOpenGateVisualMode ResolveRemoteOpenGateVisualModeForTesting(RemoteOpenGateVisualPhase phase, bool hasPartner)
        {
            RemoteOpenGateState state = new(0, 0, 0, 0, 0, false, 0, phase, 0);
            return ResolveRemoteOpenGateVisualMode(state, hasPartner);
        }

        internal static RemoteOpenGateVisualMode ResolveRemoteOpenGateVisualModeForTesting(
            RemoteOpenGateVisualPhase phase,
            bool hasPartner,
            RemoteOpenGateVisualPhase partnerPhase)
        {
            bool hasStablePartner = hasPartner && partnerPhase == RemoteOpenGateVisualPhase.Stable;
            return ResolveRemoteOpenGateVisualModeForTesting(phase, hasStablePartner);
        }

        internal static bool ShouldLinkRemoteOpenGatePortalForTesting(
            RemoteOpenGateVisualPhase phase,
            bool hasPartner,
            RemoteOpenGateVisualPhase partnerPhase)
        {
            return ShouldLinkRemoteOpenGatePortal(phase, hasPartner, partnerPhase);
        }

        internal static bool HasRemoteOpenGateSameMapPartnerForTesting(
            int mapId,
            int partnerMapId,
            RemoteOpenGateVisualPhase partnerPhase)
        {
            return HasRemoteOpenGateSameMapPartner(mapId, partnerMapId, partnerPhase);
        }

        internal static RemoteOpenGateVisualPhase ResolveRemoteOpenGatePartnerLossPhaseForTesting(
            RemoteOpenGateVisualPhase partnerPhase)
        {
            RemoteOpenGateState partner = new(
                OwnerCharacterId: 1,
                State: 1,
                MapId: 1,
                X: 0,
                Y: 0,
                IsFirstSlot: true,
                PartyId: 0,
                Phase: partnerPhase,
                PhaseStartedAt: 0);
            return ResolveRemoteOpenGatePartnerPromotion(partner, currentTime: 0).Phase;
        }

        internal static RemoteOpenGateVisualPhase ResolveRemoteOpenGatePartnerCreatePhaseForTesting(
            RemoteOpenGateVisualPhase partnerPhase)
        {
            RemoteOpenGateState partner = new(
                OwnerCharacterId: 1,
                State: 1,
                MapId: 1,
                X: 0,
                Y: 0,
                IsFirstSlot: true,
                PartyId: 0,
                Phase: partnerPhase,
                PhaseStartedAt: 0);
            return ResolveRemoteOpenGatePartnerPromotion(partner, currentTime: 0).Phase;
        }

        internal static (byte ResolvedState, RemoteOpenGateVisualPhase Phase, int PhaseStartedAt) ResolveRemoteOpenGateCreateLifecycleForTesting(
            byte packetState,
            bool hasExistingState,
            byte existingState,
            RemoteOpenGateVisualPhase existingPhase,
            int existingPhaseStartedAt,
            int currentTime)
        {
            RemoteOpenGateState? state = hasExistingState
                ? new RemoteOpenGateState(
                    OwnerCharacterId: 1,
                    State: existingState,
                    MapId: 1,
                    X: 0,
                    Y: 0,
                    IsFirstSlot: true,
                    PartyId: 0,
                    Phase: existingPhase,
                    PhaseStartedAt: existingPhaseStartedAt)
                : null;
            return ResolveRemoteOpenGateCreateLifecycle(packetState, state, currentTime);
        }

        internal static int ResolveRemoteOpenGatePartnerLossPhaseStartedAtForTesting(
            RemoteOpenGateVisualPhase partnerPhase,
            int partnerPhaseStartedAt,
            int currentTime)
        {
            RemoteOpenGateState partner = new(
                OwnerCharacterId: 1,
                State: 1,
                MapId: 1,
                X: 0,
                Y: 0,
                IsFirstSlot: true,
                PartyId: 0,
                Phase: partnerPhase,
                PhaseStartedAt: partnerPhaseStartedAt);
            return ResolveRemoteOpenGatePartnerPromotion(partner, currentTime).PhaseStartedAt;
        }

        internal static int ResolveRemoteOpenGatePartnerCreatePhaseStartedAtForTesting(
            RemoteOpenGateVisualPhase partnerPhase,
            int partnerPhaseStartedAt,
            int currentTime)
        {
            RemoteOpenGateState partner = new(
                OwnerCharacterId: 1,
                State: 1,
                MapId: 1,
                X: 0,
                Y: 0,
                IsFirstSlot: true,
                PartyId: 0,
                Phase: partnerPhase,
                PhaseStartedAt: partnerPhaseStartedAt);
            return ResolveRemoteOpenGatePartnerPromotion(partner, currentTime).PhaseStartedAt;
        }
    }
}
