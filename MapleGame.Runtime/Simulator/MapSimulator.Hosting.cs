using HaCreator.MapSimulator.Contracts;
using System.Threading;
using System;
using System.Linq;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator : IGameSession
    {
        private CancellationToken hostCancellation;
        private readonly GameSessionOptions sessionOptions;
        private readonly IRuntimeDataServices runtimeServices;
        private ref RuntimeMapDefinition _runtimeMapDefinition => ref ContentTarget.Definition;
        private ref MapleLib.WzLib.WzStructure.MapInfo _mapInfo => ref ContentTarget.Info;
        private Pools.TexturePool sceneryTexturePool => ContentTarget.SceneryTextures;
        private Loaders.UILoaderResourceCache uiResourceCache => ContentTarget.UiResourceCache;
        private readonly System.Collections.Generic.Dictionary<int, MapleLib.WzLib.WzStructure.MapInfo> mapMetadata = new();

        private MapleLib.WzLib.WzStructure.MapInfo GetMapMetadata(int mapId)
        {
            if (_mapInfo?.id == mapId) return _mapInfo;
            if (mapMetadata.TryGetValue(mapId, out var cached)) return cached;
            if (_mapProvider == null) return null;
            using RuntimeMapDefinition map = LoadTargetMap(mapId);
            if (map == null) return null;
            var info = map.CreateMapInfo();
            mapMetadata.Add(mapId, info);
            return info;
        }

        private void SetMapDefinition(RuntimeMapDefinition map)
        {
            ArgumentNullException.ThrowIfNull(map);
            RuntimeMapDefinition ownedMap = map.Clone();
            MapleLib.WzLib.WzStructure.MapInfo info = null;
            Physics.RuntimePhysicsSnapshot physics;
            Entities.RuntimePortal[] portals;
            try
            {
                info = ownedMap.CreateMapInfo();
                physics = Physics.RuntimePhysicsSnapshot.Create(ownedMap);
                portals = ownedMap.Portals.Select(portal => new Entities.RuntimePortal(portal)).ToArray();
            }
            catch
            {
                info?.Image?.Dispose();
                ownedMap.Dispose();
                throw;
            }

            var previousMap = _runtimeMapDefinition;
            var previousInfo = _mapInfo;
            _runtimeMapDefinition = ownedMap;
            _mapInfo = info;
            _runtimePhysicsSnapshot = physics;
            _runtimePortals = portals;
            previousMap?.Dispose();
            previousInfo?.Image?.Dispose();
        }

        private System.Collections.Generic.IReadOnlyList<Entities.RuntimePortal> LoadTargetPortals(int mapId)
        {
            if (_mapProvider == null) return Array.Empty<Entities.RuntimePortal>();
            using RuntimeMapDefinition map = LoadTargetMap(mapId);
            return map?.Portals.Select(portal => new Entities.RuntimePortal(portal)).ToArray()
                ?? Array.Empty<Entities.RuntimePortal>();
        }

        private RuntimeMapDefinition LoadTargetMap(int mapId)
        {
            try { return _mapProvider.Load(mapId, hostCancellation); }
            catch (System.IO.IOException error)
            {
                runtimeServices.Diagnostics.Report($"Unable to load map {mapId}.", error);
                return null;
            }
        }

        // These stages decode assets and upload GPU resources together. Execute them on
        // the game thread until CPU decoding is separated from device-bound upload.
        private void RunContentStage(Action stage)
        {
            hostCancellation.ThrowIfCancellationRequested();
            stage();
            hostCancellation.ThrowIfCancellationRequested();
        }

        private void DisposeSessionResources()
        {
            try { _stagingContent?.Dispose(); }
            finally
            {
                try { _activeContent?.Dispose(); }
                finally
                {
                    try { _texturePool.Dispose(); }
                    finally
                    {
                        foreach (var metadata in mapMetadata.Values) metadata.Image?.Dispose();
                        mapMetadata.Clear();
                    }
                }
            }
        }

        private void DisposeConstructorOwnedResourcesAfterFailure()
        {
            IDisposable[] resources =
            {
                _packetScriptOfficialSessionBridge,
                _transportOfficialSessionBridge,
                _snowBallOfficialSessionBridge,
                _coconutOfficialSessionBridge,
                _memoryGameOfficialSessionBridge,
                _socialRoomEmployeeOfficialSessionBridge,
                _tradingRoomOfficialSessionBridge,
                _monsterCarnivalOfficialSessionBridge,
                _guildBossOfficialSessionBridge,
                _massacreOfficialSessionBridge,
                _dojoOfficialSessionBridge,
                _partyRaidOfficialSessionBridge,
                _tournamentOfficialSessionBridge,
                _cookieHouseOfficialSessionBridge,
                _loginOfficialSessionBridge,
                _cashShopOfficialSessionBridge,
                _mtsOfficialSessionBridge,
                _reactorPoolOfficialSessionBridge,
                _summonedOfficialSessionBridge,
                _adminShopOfficialSessionBridge,
                _localUtilityOfficialSessionBridge,
                _messengerOfficialSessionBridge,
                _mapleTvOfficialSessionBridge,
                _familyOfficialSessionBridge,
                _guildBbsOfficialSessionBridge,
                _remoteUserOfficialSessionBridge,
                _expeditionIntermediaryOfficialSessionBridge,
                _fieldMessageBoxOfficialSessionBridge,
                _mapTransferOfficialSessionBridge,
                _packetFieldOfficialSessionBridge,
                _rockPaperScissorsOfficialSessionBridge,
                _socialListOfficialSessionBridge,
                _socialRoomMerchantOfficialSessionBridge,
                _chatFallbackMeasureGraphics,
                _chatFallbackFont
            };

            foreach (IDisposable resource in resources)
            {
                if (resource == null)
                {
                    continue;
                }

                try
                {
                    resource.Dispose();
                }
                catch (Exception cleanupError)
                {
                    runtimeServices.Diagnostics.Report("MapSimulator constructor-owned resource cleanup failed.", cleanupError);
                }
            }

            try
            {
                _chatFallbackMeasureBitmap.Dispose();
            }
            catch (Exception cleanupError)
            {
                runtimeServices.Diagnostics.Report("MapSimulator constructor bitmap cleanup failed.", cleanupError);
            }
        }

        void IGameSession.Run(CancellationToken cancellationToken)
        {
            hostCancellation = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            Run();
        }
    }
}
