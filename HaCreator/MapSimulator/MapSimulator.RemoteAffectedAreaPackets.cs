using System;
using System.Collections.Generic;
using System.Linq;
using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Pools;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int RemoteAffectedAreaFallbackTickMs = 1000;
        private readonly Dictionary<int, int> _remoteAffectedAreaBattlefieldOwnerTeams = new();
        private readonly Dictionary<int, Fields.MonsterCarnivalTeam> _remoteAffectedAreaMonsterCarnivalOwnerTeams = new();
        private readonly Dictionary<int, string> _remoteAffectedAreaOwnerNames = new();
        private readonly Dictionary<int, string> _remoteAffectedAreaOwnerNamesByAreaObjectId = new();
        private readonly Dictionary<int, int> _remoteAffectedAreaBattlefieldOwnerTeamsByAreaObjectId = new();
        private readonly Dictionary<int, Fields.MonsterCarnivalTeam> _remoteAffectedAreaMonsterCarnivalOwnerTeamsByAreaObjectId = new();
        private readonly Dictionary<int, bool> _remoteAffectedAreaEnemyOwnersByAreaObjectId = new();
        private readonly Dictionary<int, int> _remoteAffectedAreaLocalPlayerTickTimes = new();

        private readonly record struct RemoteAffectedAreaSkillRuntime(
            SkillData Skill,
            SkillLevelData LevelData);

        private ChatCommandHandler.CommandResult ApplyRemoteAffectedAreaPacketCommand(int packetType, byte[] payload)
        {
            if (_affectedAreaPool == null || _mapBoard?.MapInfo == null)
            {
                return ChatCommandHandler.CommandResult.Error("Remote affected-area pool is unavailable until a field is loaded.");
            }

            if (!_remoteAffectedAreaPacketRuntime.TryApplyPacket(
                    packetType,
                    payload,
                    ApplyRemoteAffectedAreaCreatePacket,
                    RemoveRemoteAffectedArea,
                    out string result))
            {
                return ChatCommandHandler.CommandResult.Error(result ?? $"Failed to apply remote affected-area packet {packetType}.");
            }

            return ChatCommandHandler.CommandResult.Ok($"{result} {DescribeRemoteAffectedAreaStatus()}");
        }

        private bool ApplyRemoteAffectedAreaCreatePacket(RemoteAffectedAreaCreatedPacket packet)
        {
            if (packet.SkillId <= 0)
            {
                return false;
            }

            int ownerId = unchecked((int)packet.OwnerCharacterId);
            if (RemoteAffectedAreaSupportResolver.IsAreaBuffItemType(packet.Type))
            {
                bool applied = UpsertRemoteAreaBuffItemAffectedArea(
                    packet.ObjectId,
                    packet.Type,
                    ownerId,
                    packet.SkillId,
                    packet.Bounds,
                    packet.StartDelayUnits,
                    packet.ElementAttribute,
                    packet.Phase);
                if (applied)
                {
                    ResetRemoteAffectedAreaObjectRuntimeState(packet.ObjectId);
                    CacheRemoteAffectedAreaOwnerRuntimeState(ownerId);
                    CacheRemoteAffectedAreaOwnerRuntimeState(packet.ObjectId, ownerId);
                }

                return applied;
            }

            bool appliedPlayerOrMob = packet.SkillId >= 10000
                ? UpsertRemotePlayerAffectedArea(
                    packet.ObjectId,
                    packet.Type,
                    ownerId,
                    packet.SkillId,
                    packet.SkillLevel,
                    packet.Bounds,
                    packet.StartDelayUnits,
                    packet.ElementAttribute,
                    packet.Phase,
                    ShouldUseRemoteAffectedAreaPvpLevelData())
                : UpsertRemoteMobAffectedArea(
                    packet.ObjectId,
                    packet.Type,
                    ownerId,
                    packet.SkillId,
                    packet.SkillLevel,
                    packet.Bounds,
                    packet.StartDelayUnits,
                    packet.ElementAttribute,
                    packet.Phase);
            if (appliedPlayerOrMob)
            {
                ResetRemoteAffectedAreaObjectRuntimeState(packet.ObjectId);
                CacheRemoteAffectedAreaOwnerRuntimeState(ownerId);
                CacheRemoteAffectedAreaOwnerRuntimeState(packet.ObjectId, ownerId);
            }

            return appliedPlayerOrMob;
        }

        internal static void ResetRemoteAffectedAreaObjectRuntimeState(
            int areaObjectId,
            IDictionary<int, string> cachedOwnerNamesByAreaObjectId,
            IDictionary<int, int> cachedBattlefieldOwnerTeamsByAreaObjectId,
            IDictionary<int, Fields.MonsterCarnivalTeam> cachedMonsterCarnivalOwnerTeamsByAreaObjectId,
            IDictionary<int, bool> cachedEnemyOwnersByAreaObjectId,
            IDictionary<int, int> cachedLocalPlayerTickTimes)
        {
            if (areaObjectId <= 0)
            {
                return;
            }

            // Packet create for an object id starts a fresh per-area ownership/tick lifetime.
            cachedOwnerNamesByAreaObjectId?.Remove(areaObjectId);
            cachedBattlefieldOwnerTeamsByAreaObjectId?.Remove(areaObjectId);
            cachedMonsterCarnivalOwnerTeamsByAreaObjectId?.Remove(areaObjectId);
            cachedEnemyOwnersByAreaObjectId?.Remove(areaObjectId);
            cachedLocalPlayerTickTimes?.Remove(areaObjectId);
        }

        private void ResetRemoteAffectedAreaObjectRuntimeState(int areaObjectId)
        {
            ResetRemoteAffectedAreaObjectRuntimeState(
                areaObjectId,
                _remoteAffectedAreaOwnerNamesByAreaObjectId,
                _remoteAffectedAreaBattlefieldOwnerTeamsByAreaObjectId,
                _remoteAffectedAreaMonsterCarnivalOwnerTeamsByAreaObjectId,
                _remoteAffectedAreaEnemyOwnersByAreaObjectId,
                _remoteAffectedAreaLocalPlayerTickTimes);
        }

        private string DescribeRemoteAffectedAreaStatus()
        {
            return _affectedAreaPool == null
                ? "Remote affected-area pool unavailable."
                : $"Remote affected-area pool count={_affectedAreaPool.Count}.";
        }

        private void BindRemoteAffectedAreaPacketField()
        {
            _remoteAffectedAreaPacketRuntime.BindField(_mapBoard?.MapInfo?.id ?? -1, ClearRemoteAffectedAreas);
        }

        internal static bool TryResolveMonsterCarnivalAffectedAreaOwnerTeam(
            int ownerId,
            IReadOnlyDictionary<int, Fields.MonsterCarnivalTeam> cachedOwnerTeams,
            Func<int, string> resolveOwnerName,
            Func<string, Fields.MonsterCarnivalTeam?> resolveCharacterTeam,
            out Fields.MonsterCarnivalTeam team)
        {
            team = default;
            if (ownerId <= 0)
            {
                return false;
            }

            string ownerName = resolveOwnerName?.Invoke(ownerId);
            if (string.IsNullOrWhiteSpace(ownerName))
            {
                if (cachedOwnerTeams != null
                    && cachedOwnerTeams.TryGetValue(ownerId, out Fields.MonsterCarnivalTeam cachedTeam))
                {
                    team = cachedTeam;
                    return true;
                }

                return false;
            }

            Fields.MonsterCarnivalTeam? resolvedTeam = resolveCharacterTeam?.Invoke(ownerName.Trim());
            if (!resolvedTeam.HasValue)
            {
                if (cachedOwnerTeams != null
                    && cachedOwnerTeams.TryGetValue(ownerId, out Fields.MonsterCarnivalTeam cachedTeam))
                {
                    team = cachedTeam;
                    return true;
                }

                return false;
            }

            team = resolvedTeam.Value;
            return true;
        }

        internal static bool TryResolveBattlefieldAffectedAreaOwnerTeam(
            int ownerId,
            int? localTeamId,
            IReadOnlyDictionary<int, int> cachedOwnerTeams,
            Func<int, int?> resolveRuntimeOwnerTeamId,
            out int ownerTeamId)
        {
            ownerTeamId = default;
            if (ownerId <= 0 || !localTeamId.HasValue)
            {
                return false;
            }

            int? runtimeOwnerTeamId = resolveRuntimeOwnerTeamId?.Invoke(ownerId);
            if (runtimeOwnerTeamId.HasValue && runtimeOwnerTeamId.Value >= 0)
            {
                ownerTeamId = runtimeOwnerTeamId.Value;
                return true;
            }

            if (cachedOwnerTeams != null
                && cachedOwnerTeams.TryGetValue(ownerId, out int cachedOwnerTeamId))
            {
                ownerTeamId = cachedOwnerTeamId;
                return true;
            }

            return false;
        }

        internal static bool TryResolveBattlefieldAffectedAreaOwnerTeamWithAreaSnapshot(
            int ownerId,
            int areaObjectId,
            int? localTeamId,
            IReadOnlyDictionary<int, int> cachedOwnerTeams,
            IReadOnlyDictionary<int, int> cachedAreaOwnerTeams,
            Func<int, int?> resolveRuntimeOwnerTeamId,
            out int ownerTeamId)
        {
            ownerTeamId = default;
            if (ownerId <= 0 || !localTeamId.HasValue)
            {
                return false;
            }

            int? runtimeOwnerTeamId = resolveRuntimeOwnerTeamId?.Invoke(ownerId);
            if (runtimeOwnerTeamId.HasValue && runtimeOwnerTeamId.Value >= 0)
            {
                ownerTeamId = runtimeOwnerTeamId.Value;
                return true;
            }

            if (areaObjectId > 0
                && cachedAreaOwnerTeams != null
                && cachedAreaOwnerTeams.TryGetValue(areaObjectId, out int cachedAreaOwnerTeamId))
            {
                ownerTeamId = cachedAreaOwnerTeamId;
                return true;
            }

            if (cachedOwnerTeams != null
                && cachedOwnerTeams.TryGetValue(ownerId, out int cachedOwnerTeamId))
            {
                ownerTeamId = cachedOwnerTeamId;
                return true;
            }

            return false;
        }

        internal static bool TryResolveMonsterCarnivalAffectedAreaOwnerTeamWithAreaSnapshot(
            int ownerId,
            int areaObjectId,
            IReadOnlyDictionary<int, Fields.MonsterCarnivalTeam> cachedOwnerTeams,
            IReadOnlyDictionary<int, Fields.MonsterCarnivalTeam> cachedAreaOwnerTeams,
            Func<int, string> resolveOwnerName,
            Func<string, Fields.MonsterCarnivalTeam?> resolveCharacterTeam,
            out Fields.MonsterCarnivalTeam team)
        {
            team = default;
            if (ownerId <= 0)
            {
                return false;
            }

            string ownerName = resolveOwnerName?.Invoke(ownerId);
            if (!string.IsNullOrWhiteSpace(ownerName))
            {
                Fields.MonsterCarnivalTeam? liveResolvedTeam = resolveCharacterTeam?.Invoke(ownerName.Trim());
                if (liveResolvedTeam.HasValue)
                {
                    team = liveResolvedTeam.Value;
                    return true;
                }
            }

            if (areaObjectId > 0
                && cachedAreaOwnerTeams != null
                && cachedAreaOwnerTeams.TryGetValue(areaObjectId, out Fields.MonsterCarnivalTeam cachedAreaTeam))
            {
                team = cachedAreaTeam;
                return true;
            }

            if (cachedOwnerTeams != null
                && cachedOwnerTeams.TryGetValue(ownerId, out Fields.MonsterCarnivalTeam cachedOwnerTeam))
            {
                team = cachedOwnerTeam;
                return true;
            }

            return false;
        }

        internal static Fields.MonsterCarnivalTeam? ResolveMonsterCarnivalAffectedAreaOwnerTeamSnapshot(
            int ownerId,
            int areaObjectId,
            IReadOnlyDictionary<int, string> cachedOwnerNamesByAreaObjectId,
            IReadOnlyDictionary<int, string> cachedOwnerNames,
            IReadOnlyDictionary<int, Fields.MonsterCarnivalTeam> cachedOwnerTeams,
            Func<int, string> resolveLiveOwnerName,
            Func<string, Fields.MonsterCarnivalTeam?> resolveCharacterTeam)
        {
            if (ownerId <= 0)
            {
                return null;
            }

            string ownerName = ResolveRemoteAffectedAreaOwnerName(
                areaObjectId,
                ownerId,
                cachedOwnerNamesByAreaObjectId,
                cachedOwnerNames,
                resolveLiveOwnerName);
            if (!string.IsNullOrWhiteSpace(ownerName))
            {
                Fields.MonsterCarnivalTeam? resolvedByName = resolveCharacterTeam?.Invoke(ownerName.Trim());
                if (resolvedByName.HasValue)
                {
                    return resolvedByName.Value;
                }
            }

            return cachedOwnerTeams != null
                   && cachedOwnerTeams.TryGetValue(ownerId, out Fields.MonsterCarnivalTeam cachedOwnerTeam)
                ? cachedOwnerTeam
                : null;
        }

        internal static void CacheRemoteAffectedAreaOwnerRuntimeState(
            int ownerId,
            IDictionary<int, string> cachedOwnerNames,
            IDictionary<int, int> cachedBattlefieldOwnerTeams,
            IDictionary<int, Fields.MonsterCarnivalTeam> cachedCarnivalOwnerTeams,
            Func<int, string> resolveLiveOwnerName,
            Func<int, int?> resolveRuntimeBattlefieldOwnerTeamId,
            int? localBattlefieldTeamId,
            Func<string, Fields.MonsterCarnivalTeam?> resolveMonsterCarnivalOwnerTeamByName)
        {
            if (ownerId <= 0)
            {
                return;
            }

            string ownerName = resolveLiveOwnerName?.Invoke(ownerId);
            if (!string.IsNullOrWhiteSpace(ownerName))
            {
                cachedOwnerNames?[ownerId] = ownerName.Trim();
            }

            if (localBattlefieldTeamId.HasValue
                && TryResolveBattlefieldAffectedAreaOwnerTeam(
                    ownerId,
                    localBattlefieldTeamId,
                    cachedBattlefieldOwnerTeams as IReadOnlyDictionary<int, int>,
                    resolveRuntimeBattlefieldOwnerTeamId,
                    out int ownerBattlefieldTeamId))
            {
                cachedBattlefieldOwnerTeams?[ownerId] = ownerBattlefieldTeamId;
            }

            string ownerNameForCarnival = !string.IsNullOrWhiteSpace(ownerName)
                ? ownerName.Trim()
                : ResolveRemoteAffectedAreaOwnerName(
                    ownerId,
                    cachedOwnerNames as IReadOnlyDictionary<int, string>,
                    resolveLiveOwnerName);
            if (string.IsNullOrWhiteSpace(ownerNameForCarnival))
            {
                return;
            }

            Fields.MonsterCarnivalTeam? ownerCarnivalTeam =
                resolveMonsterCarnivalOwnerTeamByName?.Invoke(ownerNameForCarnival);
            if (ownerCarnivalTeam.HasValue)
            {
                cachedCarnivalOwnerTeams?[ownerId] = ownerCarnivalTeam.Value;
            }
        }

        private void CacheRemoteAffectedAreaOwnerRuntimeState(int ownerId)
        {
            Effects.BattlefieldField battlefield = _specialFieldRuntime?.SpecialEffects?.Battlefield;
            CacheRemoteAffectedAreaOwnerRuntimeState(
                ownerId,
                _remoteAffectedAreaOwnerNames,
                _remoteAffectedAreaBattlefieldOwnerTeams,
                _remoteAffectedAreaMonsterCarnivalOwnerTeams,
                ResolveLiveRemoteAffectedAreaOwnerName,
                ResolveRuntimeBattlefieldAffectedAreaOwnerTeamId,
                battlefield?.LocalTeamId,
                ResolveMonsterCarnivalAffectedAreaOwnerTeamByName);
        }

        private void CacheRemoteAffectedAreaOwnerRuntimeState(int areaObjectId, int ownerId)
        {
            if (areaObjectId <= 0 || ownerId <= 0)
            {
                return;
            }

            CacheRemoteAffectedAreaOwnerRuntimeState(ownerId);

            string liveOwnerName = ResolveLiveRemoteAffectedAreaOwnerName(ownerId)?.Trim();
            bool hasAreaOwnerNameSnapshot =
                _remoteAffectedAreaOwnerNamesByAreaObjectId.TryGetValue(areaObjectId, out string existingAreaOwnerName)
                && !string.IsNullOrWhiteSpace(existingAreaOwnerName);

            if (!string.IsNullOrWhiteSpace(liveOwnerName))
            {
                _remoteAffectedAreaOwnerNamesByAreaObjectId[areaObjectId] = liveOwnerName;
                _remoteAffectedAreaOwnerNames[ownerId] = liveOwnerName;
            }
            else if (!hasAreaOwnerNameSnapshot
                     && _remoteAffectedAreaOwnerNames.TryGetValue(ownerId, out string cachedOwnerName)
                     && !string.IsNullOrWhiteSpace(cachedOwnerName))
            {
                _remoteAffectedAreaOwnerNamesByAreaObjectId[areaObjectId] = cachedOwnerName.Trim();
            }

            int? liveOwnerBattlefieldTeamId = ResolveRuntimeBattlefieldAffectedAreaOwnerTeamId(ownerId);
            bool hasAreaBattlefieldTeamSnapshot =
                _remoteAffectedAreaBattlefieldOwnerTeamsByAreaObjectId.TryGetValue(areaObjectId, out _);
            if (liveOwnerBattlefieldTeamId.HasValue && liveOwnerBattlefieldTeamId.Value >= 0)
            {
                _remoteAffectedAreaBattlefieldOwnerTeamsByAreaObjectId[areaObjectId] = liveOwnerBattlefieldTeamId.Value;
                _remoteAffectedAreaBattlefieldOwnerTeams[ownerId] = liveOwnerBattlefieldTeamId.Value;
            }
            else if (!hasAreaBattlefieldTeamSnapshot
                     && _remoteAffectedAreaBattlefieldOwnerTeams.TryGetValue(ownerId, out int cachedBattlefieldOwnerTeamId))
            {
                _remoteAffectedAreaBattlefieldOwnerTeamsByAreaObjectId[areaObjectId] = cachedBattlefieldOwnerTeamId;
            }

            Fields.MonsterCarnivalTeam? liveOwnerCarnivalTeam =
                ResolveMonsterCarnivalAffectedAreaOwnerTeamSnapshot(
                    ownerId,
                    areaObjectId,
                    _remoteAffectedAreaOwnerNamesByAreaObjectId,
                    _remoteAffectedAreaOwnerNames,
                    _remoteAffectedAreaMonsterCarnivalOwnerTeams,
                    ResolveLiveRemoteAffectedAreaOwnerName,
                    ResolveMonsterCarnivalAffectedAreaOwnerTeamByName);

            bool hasAreaCarnivalTeamSnapshot =
                _remoteAffectedAreaMonsterCarnivalOwnerTeamsByAreaObjectId.TryGetValue(areaObjectId, out _);
            if (liveOwnerCarnivalTeam.HasValue)
            {
                _remoteAffectedAreaMonsterCarnivalOwnerTeamsByAreaObjectId[areaObjectId] = liveOwnerCarnivalTeam.Value;
                _remoteAffectedAreaMonsterCarnivalOwnerTeams[ownerId] = liveOwnerCarnivalTeam.Value;
            }
            else if (!hasAreaCarnivalTeamSnapshot
                     && _remoteAffectedAreaMonsterCarnivalOwnerTeams.TryGetValue(ownerId, out Fields.MonsterCarnivalTeam cachedCarnivalOwnerTeam))
            {
                _remoteAffectedAreaMonsterCarnivalOwnerTeamsByAreaObjectId[areaObjectId] = cachedCarnivalOwnerTeam;
            }
        }

        private void UpdateRemoteAffectedAreaGameplay(int currentTime)
        {
            if (_affectedAreaPool == null)
            {
                return;
            }

            var activeProjectedSupportAreaIds = _playerManager?.Skills != null
                ? new System.Collections.Generic.HashSet<int>()
                : null;
            var activeAreaIdsForRuntimePruning = new System.Collections.Generic.HashSet<int>();
            var activeAreaOwnerIds = new System.Collections.Generic.HashSet<int>();

            foreach (ActiveAffectedArea area in _affectedAreaPool.ActiveAreas.ToArray())
            {
                if (area?.IsActive(currentTime) != true)
                {
                    continue;
                }

                activeAreaIdsForRuntimePruning.Add(area.ObjectId);
                if (area.OwnerId > 0)
                {
                    activeAreaOwnerIds.Add(area.OwnerId);
                }

                CacheRemoteAffectedAreaOwnerRuntimeState(area.ObjectId, area.OwnerId);

                switch (area.SourceKind)
                {
                    case AffectedAreaSourceKind.MobSkill:
                        UpdateRemoteMobAffectedAreaGameplay(area, currentTime);
                        break;

                    case AffectedAreaSourceKind.PlayerSkill:
                        UpdateRemotePlayerAffectedAreaGameplay(area, currentTime, activeProjectedSupportAreaIds);
                        break;

                    case AffectedAreaSourceKind.AreaBuffItem:
                        break;
                }
            }

            PruneRemoteAffectedAreaLocalPlayerTickTimes(activeAreaIdsForRuntimePruning);
            PruneRemoteAffectedAreaOwnerAreaRuntimeSnapshots(activeAreaIdsForRuntimePruning);
            PruneRemoteAffectedAreaOwnerRuntimeState(activeAreaOwnerIds);
            _playerManager?.Skills?.SyncExternalAreaSupportBuffs(activeProjectedSupportAreaIds, currentTime);
        }

        private void UpdateRemoteMobAffectedAreaGameplay(ActiveAffectedArea area, int currentTime)
        {
            PlayerCharacter player = _playerManager?.Player;
            if (player == null || !player.IsAlive || !area.Contains(player.X, player.Y))
            {
                return;
            }

            MobSkillRuntimeData runtimeData = ResolveMobSkillRuntimeData(area.SkillId, Math.Max(1, area.SkillLevel));
            int intervalMs = Math.Max(250, runtimeData?.IntervalMs > 0 ? runtimeData.IntervalMs : RemoteAffectedAreaFallbackTickMs);
            if (!_affectedAreaPool.TryBeginGameplayTick(area, currentTime, intervalMs))
            {
                return;
            }

            if (runtimeData != null)
            {
                _playerManager?.TryApplyMobSkillStatus(
                    area.SkillId,
                    runtimeData,
                    currentTime,
                    area.WorldBounds.Center.X,
                    area.ElementAttribute);
            }

            _fieldEffects?.TriggerDamageMist(0.35f, Math.Max(250, intervalMs), currentTime);
        }

        private void UpdateRemotePlayerAffectedAreaGameplay(
            ActiveAffectedArea area,
            int currentTime,
            System.Collections.Generic.ISet<int> activeProjectedSupportAreaIds)
        {
            bool preferPvpLevelData = ShouldUseRemoteAffectedAreaPvpLevelData();
            SkillData skill = _playerManager?.SkillLoader?.LoadSkill(area.SkillId);
            if (skill == null)
            {
                return;
            }

            SkillData[] supportSkills = ResolveRemoteAffectedAreaSupportSkills(skill);
            SkillLevelData levelData = ResolveRemoteAffectedAreaSkillLevel(skill, area.SkillLevel, preferPvpLevelData);
            SkillLevelData supportLevelData = ResolveRemoteAffectedAreaSupportLevelData(levelData, preferPvpLevelData, area.SkillLevel, supportSkills);
            SkillLevelData effectiveLevelData = supportLevelData ?? levelData;
            if (effectiveLevelData == null)
            {
                return;
            }

            TryApplyRemotePlayerSupportAffectedAreaGameplay(
                area,
                skill,
                supportSkills,
                effectiveLevelData,
                currentTime,
                activeProjectedSupportAreaIds);

            RemoteAffectedAreaSkillRuntime[] hostileSkillRuntimes =
                ResolveRemoteAffectedAreaHostileSkillRuntimes(skill, levelData, area.SkillLevel, preferPvpLevelData, supportSkills);
            if (hostileSkillRuntimes.Length == 0)
            {
                return;
            }

            RemoteAffectedAreaSkillRuntime[] localPlayerHostileSkillRuntimes =
                FilterRemoteAffectedAreaLocalPlayerHostileSkillRuntimes(hostileSkillRuntimes);
            PlayerCharacter player = _playerManager?.Player;
            bool canAffectLocalPlayer =
                localPlayerHostileSkillRuntimes.Length > 0
                && player != null
                && player.IsAlive
                && area.Contains(player.X, player.Y)
                && IsAffectedAreaOwnerEnemyInPvpContext(area.ObjectId, area.OwnerId);
            bool canAffectMobs = _mobPool?.ActiveMobs != null && _mobPool.ActiveMobs.Count > 0;
            if (!canAffectLocalPlayer && !canAffectMobs)
            {
                return;
            }

            int hostileTickIntervalMs = ResolveRemoteAffectedAreaHostileTickIntervalMs(
                hostileSkillRuntimes.Select(static runtime => runtime.LevelData));
            bool shouldReplayMobTick = canAffectMobs
                                       && _affectedAreaPool.TryBeginGameplayTick(
                                           area,
                                           currentTime,
                                           hostileTickIntervalMs);
            bool shouldReplayLocalPlayerTick = false;
            if (canAffectLocalPlayer)
            {
                int localHostileTickIntervalMs = ResolveRemoteAffectedAreaHostileTickIntervalMs(
                    localPlayerHostileSkillRuntimes.Select(static runtime => runtime.LevelData));
                shouldReplayLocalPlayerTick = TryBeginRemoteAffectedAreaLocalPlayerTick(
                    area.ObjectId,
                    currentTime,
                    localHostileTickIntervalMs);
            }

            if (!shouldReplayMobTick && !shouldReplayLocalPlayerTick)
            {
                return;
            }

            SkillManager skillManager = _playerManager?.Skills;
            if (skillManager == null && shouldReplayLocalPlayerTick)
            {
                return;
            }

            if (shouldReplayLocalPlayerTick)
            {
                ApplyRemoteHostileAffectedAreaToLocalPlayer(localPlayerHostileSkillRuntimes, currentTime);
            }

            if (!shouldReplayMobTick || skillManager == null)
            {
                return;
            }

            foreach (MobItem mob in _mobPool.ActiveMobs)
            {
                if (mob?.AI == null || mob.AI.IsDead || !area.Contains(mob.CurrentX, mob.CurrentY))
                {
                    continue;
                }

                for (int i = 0; i < hostileSkillRuntimes.Length; i++)
                {
                    RemoteAffectedAreaSkillRuntime hostileRuntime = hostileSkillRuntimes[i];
                    int fallbackDamage = ResolveRemoteAffectedAreaFallbackDamage(hostileRuntime.Skill, hostileRuntime.LevelData);
                    skillManager.ApplyInferredMobStatusesFromSkill(
                        hostileRuntime.Skill,
                        hostileRuntime.LevelData,
                        mob.AI,
                        currentTime,
                        fallbackDamage);
                }
            }
        }

        private bool TryBeginRemoteAffectedAreaLocalPlayerTick(
            int areaObjectId,
            int currentTime,
            int intervalMs)
        {
            if (areaObjectId <= 0)
            {
                return false;
            }

            int normalizedIntervalMs = Math.Max(100, intervalMs);
            if (_remoteAffectedAreaLocalPlayerTickTimes.TryGetValue(areaObjectId, out int nextTickTime)
                && currentTime < nextTickTime)
            {
                return false;
            }

            _remoteAffectedAreaLocalPlayerTickTimes[areaObjectId] = currentTime + normalizedIntervalMs;
            return true;
        }

        private void PruneRemoteAffectedAreaLocalPlayerTickTimes(System.Collections.Generic.ISet<int> activeAreaIds)
        {
            if (_remoteAffectedAreaLocalPlayerTickTimes.Count == 0)
            {
                return;
            }

            if (activeAreaIds == null || activeAreaIds.Count == 0)
            {
                _remoteAffectedAreaLocalPlayerTickTimes.Clear();
                return;
            }

            foreach (int areaObjectId in _remoteAffectedAreaLocalPlayerTickTimes.Keys.ToArray())
            {
                if (activeAreaIds.Contains(areaObjectId))
                {
                    continue;
                }

                _remoteAffectedAreaLocalPlayerTickTimes.Remove(areaObjectId);
            }
        }

        private void PruneRemoteAffectedAreaOwnerAreaRuntimeSnapshots(System.Collections.Generic.ISet<int> activeAreaIds)
        {
            if (activeAreaIds == null || activeAreaIds.Count == 0)
            {
                _remoteAffectedAreaOwnerNamesByAreaObjectId.Clear();
                _remoteAffectedAreaBattlefieldOwnerTeamsByAreaObjectId.Clear();
                _remoteAffectedAreaMonsterCarnivalOwnerTeamsByAreaObjectId.Clear();
                _remoteAffectedAreaEnemyOwnersByAreaObjectId.Clear();
                return;
            }

            foreach (int areaObjectId in _remoteAffectedAreaOwnerNamesByAreaObjectId.Keys.ToArray())
            {
                if (!activeAreaIds.Contains(areaObjectId))
                {
                    _remoteAffectedAreaOwnerNamesByAreaObjectId.Remove(areaObjectId);
                }
            }

            foreach (int areaObjectId in _remoteAffectedAreaBattlefieldOwnerTeamsByAreaObjectId.Keys.ToArray())
            {
                if (!activeAreaIds.Contains(areaObjectId))
                {
                    _remoteAffectedAreaBattlefieldOwnerTeamsByAreaObjectId.Remove(areaObjectId);
                }
            }

            foreach (int areaObjectId in _remoteAffectedAreaMonsterCarnivalOwnerTeamsByAreaObjectId.Keys.ToArray())
            {
                if (!activeAreaIds.Contains(areaObjectId))
                {
                    _remoteAffectedAreaMonsterCarnivalOwnerTeamsByAreaObjectId.Remove(areaObjectId);
                }
            }

            foreach (int areaObjectId in _remoteAffectedAreaEnemyOwnersByAreaObjectId.Keys.ToArray())
            {
                if (!activeAreaIds.Contains(areaObjectId))
                {
                    _remoteAffectedAreaEnemyOwnersByAreaObjectId.Remove(areaObjectId);
                }
            }
        }

        internal static bool ShouldRetainRemoteAffectedAreaOwnerRuntimeState(
            int ownerId,
            System.Collections.Generic.ISet<int> activeOwnerIds,
            Func<int, string> resolveLiveOwnerName,
            Func<int, int?> resolveRuntimeBattlefieldOwnerTeamId)
        {
            if (ownerId <= 0)
            {
                return false;
            }

            if (activeOwnerIds?.Contains(ownerId) == true)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(resolveLiveOwnerName?.Invoke(ownerId)))
            {
                return true;
            }

            int? battlefieldTeamId = resolveRuntimeBattlefieldOwnerTeamId?.Invoke(ownerId);
            return battlefieldTeamId.HasValue && battlefieldTeamId.Value >= 0;
        }

        private void PruneRemoteAffectedAreaOwnerRuntimeState(System.Collections.Generic.ISet<int> activeOwnerIds)
        {
            if (_remoteAffectedAreaOwnerNames.Count == 0
                && _remoteAffectedAreaBattlefieldOwnerTeams.Count == 0
                && _remoteAffectedAreaMonsterCarnivalOwnerTeams.Count == 0)
            {
                return;
            }

            if (activeOwnerIds == null || activeOwnerIds.Count == 0)
            {
                _remoteAffectedAreaOwnerNames.Clear();
                _remoteAffectedAreaBattlefieldOwnerTeams.Clear();
                _remoteAffectedAreaMonsterCarnivalOwnerTeams.Clear();
                return;
            }

            var candidateOwnerIds = new System.Collections.Generic.HashSet<int>();
            foreach (int ownerId in _remoteAffectedAreaOwnerNames.Keys)
            {
                candidateOwnerIds.Add(ownerId);
            }

            foreach (int ownerId in _remoteAffectedAreaBattlefieldOwnerTeams.Keys)
            {
                candidateOwnerIds.Add(ownerId);
            }

            foreach (int ownerId in _remoteAffectedAreaMonsterCarnivalOwnerTeams.Keys)
            {
                candidateOwnerIds.Add(ownerId);
            }

            foreach (int ownerId in candidateOwnerIds)
            {
                if (ShouldRetainRemoteAffectedAreaOwnerRuntimeState(
                        ownerId,
                        activeOwnerIds,
                        ResolveLiveRemoteAffectedAreaOwnerName,
                        ResolveRuntimeBattlefieldAffectedAreaOwnerTeamId))
                {
                    continue;
                }

                _remoteAffectedAreaOwnerNames.Remove(ownerId);
                _remoteAffectedAreaBattlefieldOwnerTeams.Remove(ownerId);
                _remoteAffectedAreaMonsterCarnivalOwnerTeams.Remove(ownerId);
            }
        }

        private static RemoteAffectedAreaSkillRuntime[] FilterRemoteAffectedAreaLocalPlayerHostileSkillRuntimes(
            RemoteAffectedAreaSkillRuntime[] hostileSkillRuntimes)
        {
            if (hostileSkillRuntimes == null || hostileSkillRuntimes.Length == 0)
            {
                return Array.Empty<RemoteAffectedAreaSkillRuntime>();
            }

            var filtered = new System.Collections.Generic.List<RemoteAffectedAreaSkillRuntime>(hostileSkillRuntimes.Length);
            for (int i = 0; i < hostileSkillRuntimes.Length; i++)
            {
                RemoteAffectedAreaSkillRuntime runtime = hostileSkillRuntimes[i];
                if (!CanRemoteAffectedAreaHostileRuntimeAffectLocalPlayer(runtime.Skill, runtime.LevelData))
                {
                    continue;
                }

                filtered.Add(runtime);
            }

            return filtered.Count > 0
                ? filtered.ToArray()
                : Array.Empty<RemoteAffectedAreaSkillRuntime>();
        }

        internal static bool CanRemoteAffectedAreaHostileRuntimeAffectLocalPlayer(
            SkillData skill,
            SkillLevelData levelData)
        {
            if (skill == null || levelData == null)
            {
                return false;
            }

            if (RemoteAffectedAreaSupportResolver.ResolveHostilePlayerAreaStatuses(skill, levelData).Count > 0)
            {
                return RemoteAffectedAreaSupportResolver.ShouldProjectHostileStatusesToLocalPlayer(skill, levelData);
            }

            if (ResolveRemoteAffectedAreaFallbackDamage(skill, levelData) <= 0)
            {
                return false;
            }

            if (RemoteAffectedAreaSupportResolver.IsFriendlyPlayerAreaSkill(skill, levelData))
            {
                return false;
            }

            // Keep fallback-only local-player projection narrower than mob replay:
            // explicit self/party-targeted area skills remain support-owned unless
            // hostile status metadata above already classified them.
            if (skill.Target == SkillTarget.Self || skill.Target == SkillTarget.Party)
            {
                return false;
            }

            return RemoteAffectedAreaSupportResolver.IsHostilePlayerAreaSkill(skill, levelData);
        }

        private bool TryApplyRemotePlayerSupportAffectedAreaGameplay(
            ActiveAffectedArea area,
            SkillData skill,
            SkillData[] supportSkills,
            SkillLevelData levelData,
            int currentTime,
            System.Collections.Generic.ISet<int> activeProjectedSupportAreaIds)
        {
            if (RemoteAffectedAreaSupportResolver.ResolveDisposition(skill, supportSkills, levelData) != RemotePlayerAffectedAreaDisposition.FriendlySupport)
            {
                return false;
            }

            PlayerCharacter player = _playerManager?.Player;
            if (player == null || !player.IsAlive || !area.Contains(player.X, player.Y))
            {
                return true;
            }

            int localPlayerId = player.Build?.Id ?? 0;
            if (!RemoteAffectedAreaSupportResolver.CanAffectLocalPlayer(
                    skill,
                    supportSkills,
                    localPlayerId,
                    area.OwnerId,
                    IsAffectedAreaOwnerPartyMember(area.ObjectId, area.OwnerId),
                    IsAffectedAreaOwnerSameTeamMember(area.ObjectId, area.OwnerId),
                    levelData))
            {
                return true;
            }

            SkillManager skillManager = _playerManager?.Skills;
            if (skillManager != null
                && RemoteAffectedAreaSupportResolver.HasProjectableSupportBuffMetadata(levelData)
                && skillManager.ApplyOrRefreshExternalAreaSupportBuff(
                    area.ObjectId,
                    skill,
                    supportSkills,
                    levelData,
                    currentTime,
                    RemoteAffectedAreaFallbackTickMs + 250))
            {
                activeProjectedSupportAreaIds?.Add(area.ObjectId);
            }

            if (RemoteAffectedAreaSupportResolver.IsInvincibleZone(skill, supportSkills))
            {
                return true;
            }

            bool isRecoveryZone = RemoteAffectedAreaSupportResolver.IsRecoveryZone(skill, levelData);
            bool isStatusCleansingZone = RemoteAffectedAreaSupportResolver.IsStatusCleansingZone(skill, supportSkills);
            if (!isRecoveryZone && !isStatusCleansingZone)
            {
                return true;
            }

            if (!_affectedAreaPool.TryBeginGameplayTick(area, currentTime, RemoteAffectedAreaFallbackTickMs))
            {
                return true;
            }

            if (isStatusCleansingZone)
            {
                _playerManager?.ClearMobStatuses(RemoteAffectedAreaSupportResolver.GetSupportedAreaCleanseEffects());
            }

            if (!isRecoveryZone)
            {
                return true;
            }

            int hpHeal = levelData.HP;
            int mpHeal = levelData.MP;
            if (levelData.X > 0)
            {
                hpHeal = player.MaxHP * levelData.X / 100;
            }

            if (hpHeal <= 0 && mpHeal <= 0)
            {
                return true;
            }

            _playerManager?.Heal(hpHeal, mpHeal);
            return true;
        }

        private SkillData[] ResolveRemoteAffectedAreaSupportSkills(SkillData skill)
        {
            if (skill == null)
            {
                return Array.Empty<SkillData>();
            }

            var supportSkills = new System.Collections.Generic.List<SkillData>();
            var visitedSkillIds = new System.Collections.Generic.HashSet<int>();
            CollectRemoteAffectedAreaSupportSkills(skill, supportSkills, visitedSkillIds);
            return supportSkills.ToArray();
        }

        private void CollectRemoteAffectedAreaSupportSkills(
            SkillData skill,
            System.Collections.Generic.ICollection<SkillData> supportSkills,
            System.Collections.Generic.ISet<int> visitedSkillIds)
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

            supportSkills?.Add(skill);

            foreach (int linkedSkillId in RemoteAffectedAreaSupportResolver.EnumerateRemoteAffectedAreaLinkedSkillIds(skill))
            {
                SkillData affectedSkill = _playerManager?.SkillLoader?.LoadSkill(linkedSkillId);
                if (affectedSkill != null)
                {
                    CollectRemoteAffectedAreaSupportSkills(affectedSkill, supportSkills, visitedSkillIds);
                }
            }

            foreach (int parentSkillId in _playerManager?.SkillLoader?.FindAffectedSkillParentIds(skillId) ?? Array.Empty<int>())
            {
                SkillData parentSkill = _playerManager?.SkillLoader?.LoadSkill(parentSkillId);
                if (parentSkill != null)
                {
                    CollectRemoteAffectedAreaSupportSkills(parentSkill, supportSkills, visitedSkillIds);
                }
            }

            foreach (int parentSkillId in _playerManager?.SkillLoader?.FindDummySkillParentIds(skillId) ?? Array.Empty<int>())
            {
                SkillData parentSkill = _playerManager?.SkillLoader?.LoadSkill(parentSkillId);
                if (parentSkill != null)
                {
                    CollectRemoteAffectedAreaSupportSkills(parentSkill, supportSkills, visitedSkillIds);
                }
            }
        }

        internal static int ResolveRemoteAffectedAreaSupportSkillLevel(
            int runtimeSkillLevel,
            SkillLevelData primaryLevelData)
        {
            if (runtimeSkillLevel > 0)
            {
                return runtimeSkillLevel;
            }

            if (primaryLevelData?.Level > 0)
            {
                return primaryLevelData.Level;
            }

            return 1;
        }

        internal static SkillLevelData ResolveRemoteAffectedAreaSupportLevelData(
            SkillLevelData primaryLevelData,
            bool preferPvpLevelData,
            int runtimeSkillLevel,
            params SkillData[] supportSkills)
        {
            if (supportSkills == null || supportSkills.Length == 0)
            {
                return primaryLevelData;
            }

            SkillData primarySkill = null;
            for (int i = 0; i < supportSkills.Length; i++)
            {
                if (supportSkills[i] != null)
                {
                    primarySkill = supportSkills[i];
                    break;
                }
            }

            SkillLevelData primaryProjectedLevelData =
                RemoteAffectedAreaSupportResolver.CreateProjectedSupportBuffLevelData(
                    primarySkill,
                    primaryLevelData,
                    supportSkills);
            var projectedLevelDataEntries = new System.Collections.Generic.List<SkillLevelData>();
            if (primaryProjectedLevelData != null)
            {
                projectedLevelDataEntries.Add(primaryProjectedLevelData);
            }
            else if (primaryLevelData != null)
            {
                projectedLevelDataEntries.Add(primaryLevelData);
            }

            int derivedDamageReductionRate = primaryProjectedLevelData?.DamageReductionRate
                                             ?? primaryLevelData?.DamageReductionRate
                                             ?? 0;
            int primarySkillId = primarySkill?.SkillId ?? 0;
            for (int i = 0; i < supportSkills.Length; i++)
            {
                SkillData supportSkill = supportSkills[i];
                if (supportSkill == null)
                {
                    continue;
                }

                int resolvedSupportSkillLevel = ResolveRemoteAffectedAreaSupportSkillLevel(runtimeSkillLevel, primaryLevelData);
                SkillLevelData supportLevelData = ResolveRemoteAffectedAreaSkillLevel(
                    supportSkill,
                    resolvedSupportSkillLevel,
                    preferPvpLevelData);
                derivedDamageReductionRate = Math.Max(
                    derivedDamageReductionRate,
                    RemoteAffectedAreaSupportResolver.ResolveDerivedProjectedDamageReductionRate(supportSkill, supportLevelData));

                if (supportSkill.SkillId > 0 && supportSkill.SkillId == primarySkillId)
                {
                    continue;
                }

                SkillLevelData supportProjectedLevelData =
                    RemoteAffectedAreaSupportResolver.CreateProjectedSupportBuffLevelData(
                        supportSkill,
                        supportLevelData,
                        supportSkills);
                if (supportProjectedLevelData != null)
                {
                    projectedLevelDataEntries.Add(supportProjectedLevelData);
                }
            }

            SkillLevelData projectedLevelData =
                RemoteAffectedAreaSupportResolver.CreateProjectedSupportBuffLevelData(projectedLevelDataEntries.ToArray())
                ?? primaryProjectedLevelData
                ?? primaryLevelData;
            if (projectedLevelData == null)
            {
                return null;
            }

            if (derivedDamageReductionRate <= projectedLevelData.DamageReductionRate)
            {
                return projectedLevelData;
            }

            SkillLevelData derivedProjection = projectedLevelData.ShallowClone();
            derivedProjection.DamageReductionRate = derivedDamageReductionRate;
            return derivedProjection;
        }

        private static RemoteAffectedAreaSkillRuntime[] ResolveRemoteAffectedAreaHostileSkillRuntimes(
            SkillData primarySkill,
            SkillLevelData primaryLevelData,
            int skillLevel,
            bool preferPvpLevelData,
            params SkillData[] supportSkills)
        {
            var skillEntries = new System.Collections.Generic.List<(SkillData Skill, SkillLevelData LevelData)>();
            if (primarySkill != null && primaryLevelData != null)
            {
                skillEntries.Add((primarySkill, primaryLevelData));
            }

            if (supportSkills != null)
            {
                for (int i = 0; i < supportSkills.Length; i++)
                {
                    SkillData supportSkill = supportSkills[i];
                    if (supportSkill == null || ReferenceEquals(supportSkill, primarySkill))
                    {
                        continue;
                    }

                    SkillLevelData supportLevelData = ResolveRemoteAffectedAreaSkillLevel(
                        supportSkill,
                        skillLevel,
                        preferPvpLevelData);
                    if (supportLevelData != null)
                    {
                        skillEntries.Add((supportSkill, supportLevelData));
                    }
                }
            }

            var hostileEntries = RemoteAffectedAreaSupportResolver.FilterHostileSkillEntries(skillEntries);
            if (hostileEntries.Count == 0)
            {
                return Array.Empty<RemoteAffectedAreaSkillRuntime>();
            }

            var hostileRuntimes = new RemoteAffectedAreaSkillRuntime[hostileEntries.Count];
            for (int i = 0; i < hostileEntries.Count; i++)
            {
                (SkillData runtimeSkill, SkillLevelData runtimeLevelData) = hostileEntries[i];
                hostileRuntimes[i] = new RemoteAffectedAreaSkillRuntime(runtimeSkill, runtimeLevelData);
            }

            return hostileRuntimes;
        }

        private static SkillLevelData ResolveRemoteAffectedAreaSkillLevel(
            SkillData skill,
            int level,
            bool preferPvpLevelData)
        {
            return RemoteAffectedAreaSupportResolver.ResolveRemoteAffectedAreaSkillLevel(
                skill,
                level,
                preferPvpLevelData);
        }

        private static int ResolveRemoteAffectedAreaFallbackDamage(SkillData skill, SkillLevelData levelData)
        {
            if (levelData == null)
            {
                return 0;
            }

            int damage = Math.Max(levelData.Damage, levelData.DotDamage);
            if (damage <= 0)
            {
                return 0;
            }

            int attackCount = Math.Max(1, levelData.AttackCount);
            int scaledDamage = Math.Max(1, damage * attackCount);
            return skill?.IsMagicDamageSkill == true
                ? scaledDamage
                : Math.Max(1, scaledDamage / 2);
        }

        internal static int ResolveRemoteAffectedAreaHostileTickIntervalMs(
            System.Collections.Generic.IEnumerable<SkillLevelData> levelDataEntries)
        {
            int intervalMs = 0;
            if (levelDataEntries != null)
            {
                foreach (SkillLevelData levelData in levelDataEntries)
                {
                    if (levelData?.DotInterval <= 0)
                    {
                        continue;
                    }

                    int candidateIntervalMs = levelData.DotInterval * 1000;
                    intervalMs = intervalMs <= 0
                        ? candidateIntervalMs
                        : Math.Min(intervalMs, candidateIntervalMs);
                }
            }

            return Math.Max(250, intervalMs > 0 ? intervalMs : RemoteAffectedAreaFallbackTickMs);
        }

        private bool IsRemoteAffectedAreaProtectionActive(int currentTime)
        {
            PlayerCharacter player = _playerManager?.Player;
            if (player == null || _affectedAreaPool == null)
            {
                return false;
            }

            bool preferPvpLevelData = ShouldUseRemoteAffectedAreaPvpLevelData();
            int localPlayerId = player.Build?.Id ?? 0;
            foreach (ActiveAffectedArea area in _affectedAreaPool.ActiveAreas)
            {
                if (area?.SourceKind != AffectedAreaSourceKind.PlayerSkill
                    || !area.IsActive(currentTime)
                    || !area.Contains(player.X, player.Y))
                {
                    continue;
                }

                SkillData skill = _playerManager?.SkillLoader?.LoadSkill(area.SkillId);
                SkillData[] supportSkills = ResolveRemoteAffectedAreaSupportSkills(skill);
                SkillLevelData levelData = ResolveRemoteAffectedAreaSkillLevel(skill, area.SkillLevel, preferPvpLevelData);
                SkillLevelData supportLevelData = ResolveRemoteAffectedAreaSupportLevelData(levelData, preferPvpLevelData, area.SkillLevel, supportSkills);
                SkillLevelData effectiveLevelData = supportLevelData ?? levelData;
                if (!RemoteAffectedAreaSupportResolver.IsInvincibleZone(skill, supportSkills)
                    || !RemoteAffectedAreaSupportResolver.CanAffectLocalPlayer(
                        skill,
                        supportSkills,
                        localPlayerId,
                        area.OwnerId,
                        IsAffectedAreaOwnerPartyMember(area.ObjectId, area.OwnerId),
                        IsAffectedAreaOwnerSameTeamMember(area.ObjectId, area.OwnerId),
                        effectiveLevelData))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private void ApplyRemoteHostileAffectedAreaToLocalPlayer(
            RemoteAffectedAreaSkillRuntime[] hostileSkillRuntimes,
            int currentTime)
        {
            PlayerCharacter player = _playerManager?.Player;
            SkillManager skillManager = _playerManager?.Skills;
            if (player == null || !player.IsAlive || skillManager == null || hostileSkillRuntimes == null || hostileSkillRuntimes.Length == 0)
            {
                return;
            }

            int incomingDamage = 0;
            for (int i = 0; i < hostileSkillRuntimes.Length; i++)
            {
                incomingDamage += ResolveRemoteAffectedAreaFallbackDamage(
                    hostileSkillRuntimes[i].Skill,
                    hostileSkillRuntimes[i].LevelData);
            }

            if (incomingDamage > 0)
            {
                int resolvedDamage = skillManager.ResolveIncomingDamageAfterActiveBuffs(incomingDamage, currentTime);
                if (resolvedDamage > 0)
                {
                    player.TakeDamage(resolvedDamage, 0f, 0f);
                }
            }

            for (int i = 0; i < hostileSkillRuntimes.Length; i++)
            {
                _playerManager.TryApplyRemoteAffectedAreaPlayerSkillStatus(
                    hostileSkillRuntimes[i].Skill,
                    hostileSkillRuntimes[i].LevelData,
                    currentTime);
            }
        }

        private void ApplyRemoteAffectedAreaDamageShareToOwner(int areaObjectId, int sharedDamage, int currentTime)
        {
            if (areaObjectId <= 0
                || sharedDamage <= 0
                || _affectedAreaPool?.TryGetArea(areaObjectId, out ActiveAffectedArea area) != true
                || area?.SourceKind != AffectedAreaSourceKind.PlayerSkill
                || !area.IsActive(currentTime)
                || area.OwnerId <= 0
                || _remoteUserPool?.TryGetActor(area.OwnerId, out RemoteUserActor ownerActor) != true
                || ownerActor?.Build == null)
            {
                return;
            }

            ownerActor.Build.HP = Math.Max(0, ownerActor.Build.HP - sharedDamage);
            if (ownerActor.IsVisibleInWorld)
            {
                _combatEffects?.AddPartyDamage(
                    sharedDamage,
                    ownerActor.Position.X,
                    ownerActor.Position.Y - 60f,
                    isCritical: false,
                    currentTime);
            }
        }

        private bool IsAffectedAreaOwnerPartyMember(int areaObjectId, int ownerId)
        {
            if (ownerId <= 0)
            {
                return false;
            }

            string ownerName = ResolveRemoteAffectedAreaOwnerName(areaObjectId, ownerId);
            return !string.IsNullOrWhiteSpace(ownerName)
                   && _socialListRuntime.IsTrackedPartyMember(ownerName);
        }

        private bool IsAffectedAreaOwnerSameTeamMember(int areaObjectId, int ownerId)
        {
            if (ownerId <= 0)
            {
                return false;
            }

            if (TryResolveBattlefieldAffectedAreaOwnerTeam(areaObjectId, ownerId, out int ownerBattlefieldTeamId, out int localBattlefieldTeamId))
            {
                return ownerBattlefieldTeamId == localBattlefieldTeamId;
            }

            Fields.MonsterCarnivalField carnival = _specialFieldRuntime?.Minigames?.MonsterCarnival;
            if (TryResolveMonsterCarnivalAffectedAreaOwnerTeam(areaObjectId, ownerId, out Fields.MonsterCarnivalTeam ownerCarnivalTeam))
            {
                return ownerCarnivalTeam == carnival.LocalTeam;
            }

            return false;
        }

        private bool ShouldUseRemoteAffectedAreaPvpLevelData()
        {
            return _specialFieldRuntime?.SpecialEffects?.Battlefield?.IsActive == true
                   || _specialFieldRuntime?.Minigames?.MonsterCarnival?.IsVisible == true;
        }

        private bool IsAffectedAreaOwnerEnemyInPvpContext(int areaObjectId, int ownerId)
        {
            PlayerCharacter player = _playerManager?.Player;
            int localPlayerId = player?.Build?.Id ?? 0;
            if (ownerId <= 0 || localPlayerId <= 0 || ownerId == localPlayerId)
            {
                return false;
            }

            bool hasPvpOwnershipContext =
                _specialFieldRuntime?.SpecialEffects?.Battlefield?.IsActive == true
                || _specialFieldRuntime?.Minigames?.MonsterCarnival?.IsVisible == true;
            if (!hasPvpOwnershipContext)
            {
                return false;
            }

            bool hasResolvedOwnerTeam = false;
            bool resolvedOwnerIsEnemy = false;
            if (TryResolveBattlefieldAffectedAreaOwnerTeam(areaObjectId, ownerId, out int ownerBattlefieldTeamId, out int localBattlefieldTeamId))
            {
                hasResolvedOwnerTeam = true;
                resolvedOwnerIsEnemy = ownerBattlefieldTeamId != localBattlefieldTeamId;
            }
            else
            {
                Fields.MonsterCarnivalField carnival = _specialFieldRuntime?.Minigames?.MonsterCarnival;
                if (TryResolveMonsterCarnivalAffectedAreaOwnerTeam(areaObjectId, ownerId, out Fields.MonsterCarnivalTeam ownerCarnivalTeam))
                {
                    hasResolvedOwnerTeam = true;
                    resolvedOwnerIsEnemy = ownerCarnivalTeam != carnival.LocalTeam;
                }
            }

            if (hasResolvedOwnerTeam && areaObjectId > 0)
            {
                _remoteAffectedAreaEnemyOwnersByAreaObjectId[areaObjectId] = resolvedOwnerIsEnemy;
            }

            // When owner-team reconstruction is unavailable, only keep enemy-local
            // replay for areas with explicit hostile WZ metadata.
            bool areaOwnerIsEnemySnapshot = false;
            bool hasAreaOwnerEnemySnapshot =
                areaObjectId > 0
                && _remoteAffectedAreaEnemyOwnersByAreaObjectId.TryGetValue(areaObjectId, out areaOwnerIsEnemySnapshot);
            return ResolveRemoteAffectedAreaOwnerEnemyDecision(
                hasResolvedOwnerTeam,
                resolvedOwnerIsEnemy,
                hasAreaOwnerEnemySnapshot,
                areaOwnerIsEnemySnapshot,
                hasExplicitHostileMetadata: HasExplicitHostileRemoteAffectedAreaMetadataForLocalPlayer(areaObjectId),
                hasExplicitFriendlyMetadata: HasExplicitFriendlyRemoteAffectedAreaMetadataForLocalPlayer(areaObjectId),
                ownerIsPartyMember: IsAffectedAreaOwnerPartyMember(areaObjectId, ownerId));
        }

        private bool HasExplicitHostileRemoteAffectedAreaMetadataForLocalPlayer(int areaObjectId)
        {
            if (areaObjectId <= 0
                || _affectedAreaPool?.TryGetArea(areaObjectId, out ActiveAffectedArea area) != true
                || area?.SourceKind != AffectedAreaSourceKind.PlayerSkill)
            {
                return false;
            }

            bool preferPvpLevelData = ShouldUseRemoteAffectedAreaPvpLevelData();
            SkillData skill = _playerManager?.SkillLoader?.LoadSkill(area.SkillId);
            if (skill == null)
            {
                return false;
            }

            SkillData[] supportSkills = ResolveRemoteAffectedAreaSupportSkills(skill);
            SkillLevelData levelData = ResolveRemoteAffectedAreaSkillLevel(skill, area.SkillLevel, preferPvpLevelData);
            if (levelData == null)
            {
                return false;
            }

            RemoteAffectedAreaSkillRuntime[] hostileSkillRuntimes =
                ResolveRemoteAffectedAreaHostileSkillRuntimes(skill, levelData, area.SkillLevel, preferPvpLevelData, supportSkills);
            if (hostileSkillRuntimes.Length <= 0)
            {
                return false;
            }

            return FilterRemoteAffectedAreaLocalPlayerHostileSkillRuntimes(hostileSkillRuntimes).Length > 0;
        }

        private bool HasExplicitFriendlyRemoteAffectedAreaMetadataForLocalPlayer(int areaObjectId)
        {
            if (areaObjectId <= 0
                || _affectedAreaPool?.TryGetArea(areaObjectId, out ActiveAffectedArea area) != true
                || area?.SourceKind != AffectedAreaSourceKind.PlayerSkill)
            {
                return false;
            }

            bool preferPvpLevelData = ShouldUseRemoteAffectedAreaPvpLevelData();
            SkillData skill = _playerManager?.SkillLoader?.LoadSkill(area.SkillId);
            if (skill == null)
            {
                return false;
            }

            SkillData[] supportSkills = ResolveRemoteAffectedAreaSupportSkills(skill);
            SkillLevelData levelData = ResolveRemoteAffectedAreaSkillLevel(skill, area.SkillLevel, preferPvpLevelData);
            SkillLevelData effectiveLevelData = ResolveRemoteAffectedAreaSupportLevelData(levelData, preferPvpLevelData, area.SkillLevel, supportSkills);
            if (effectiveLevelData == null)
            {
                return false;
            }

            return RemoteAffectedAreaSupportResolver.IsFriendlyPlayerAreaSkill(
                skill,
                supportSkills,
                effectiveLevelData);
        }

        internal static bool ShouldAssumeRemoteAffectedAreaOwnerIsEnemyFromHostileMetadata(
            bool hasResolvedOwnerTeam,
            bool hasExplicitHostileMetadata,
            bool hasExplicitFriendlyMetadata = false,
            bool ownerIsPartyMember = false)
        {
            return !hasResolvedOwnerTeam
                   && hasExplicitHostileMetadata
                   && !hasExplicitFriendlyMetadata
                   && !ownerIsPartyMember;
        }

        internal static bool ResolveRemoteAffectedAreaOwnerEnemyDecision(
            bool hasResolvedOwnerTeam,
            bool resolvedOwnerIsEnemy,
            bool hasAreaOwnerEnemySnapshot,
            bool areaOwnerIsEnemySnapshot,
            bool hasExplicitHostileMetadata,
            bool hasExplicitFriendlyMetadata = false,
            bool ownerIsPartyMember = false)
        {
            if (hasResolvedOwnerTeam)
            {
                return resolvedOwnerIsEnemy;
            }

            if (hasAreaOwnerEnemySnapshot)
            {
                return areaOwnerIsEnemySnapshot;
            }

            return ShouldAssumeRemoteAffectedAreaOwnerIsEnemyFromHostileMetadata(
                hasResolvedOwnerTeam: false,
                hasExplicitHostileMetadata,
                hasExplicitFriendlyMetadata,
                ownerIsPartyMember);
        }

        private bool TryResolveBattlefieldAffectedAreaOwnerTeam(
            int areaObjectId,
            int ownerId,
            out int ownerTeamId,
            out int localTeamId)
        {
            ownerTeamId = default;
            localTeamId = default;

            Effects.BattlefieldField battlefield = _specialFieldRuntime?.SpecialEffects?.Battlefield;
            if (battlefield?.IsActive != true || !battlefield.LocalTeamId.HasValue)
            {
                return false;
            }

            localTeamId = battlefield.LocalTeamId.Value;
            bool resolved = TryResolveBattlefieldAffectedAreaOwnerTeamWithAreaSnapshot(
                ownerId,
                areaObjectId,
                battlefield.LocalTeamId,
                _remoteAffectedAreaBattlefieldOwnerTeams,
                _remoteAffectedAreaBattlefieldOwnerTeamsByAreaObjectId,
                ResolveRuntimeBattlefieldAffectedAreaOwnerTeamId,
                out ownerTeamId);
            if (resolved)
            {
                _remoteAffectedAreaBattlefieldOwnerTeams[ownerId] = ownerTeamId;
                if (areaObjectId > 0)
                {
                    _remoteAffectedAreaBattlefieldOwnerTeamsByAreaObjectId[areaObjectId] = ownerTeamId;
                }
            }

            return resolved;
        }

        private int? ResolveRuntimeBattlefieldAffectedAreaOwnerTeamId(int candidateOwnerId)
        {
            Effects.BattlefieldField battlefield = _specialFieldRuntime?.SpecialEffects?.Battlefield;
            if (battlefield?.TryGetAssignedTeamId(candidateOwnerId, out int assignedTeamId) == true)
            {
                return assignedTeamId;
            }

            return _remoteUserPool?.TryGetActor(candidateOwnerId, out RemoteUserActor actor) == true
                   && actor?.BattlefieldTeamId.HasValue == true
                ? actor.BattlefieldTeamId.Value
                : null;
        }

        private bool TryResolveMonsterCarnivalAffectedAreaOwnerTeam(int areaObjectId, int ownerId, out Fields.MonsterCarnivalTeam team)
        {
            team = default;
            Fields.MonsterCarnivalField carnival = _specialFieldRuntime?.Minigames?.MonsterCarnival;
            if (carnival?.IsVisible != true)
            {
                return false;
            }

            bool resolved = TryResolveMonsterCarnivalAffectedAreaOwnerTeamWithAreaSnapshot(
                ownerId,
                areaObjectId,
                _remoteAffectedAreaMonsterCarnivalOwnerTeams,
                _remoteAffectedAreaMonsterCarnivalOwnerTeamsByAreaObjectId,
                candidateOwnerId => ResolveRemoteAffectedAreaOwnerName(areaObjectId, candidateOwnerId),
                ResolveMonsterCarnivalAffectedAreaOwnerTeamByName,
                out team);
            if (resolved)
            {
                _remoteAffectedAreaMonsterCarnivalOwnerTeams[ownerId] = team;
                if (areaObjectId > 0)
                {
                    _remoteAffectedAreaMonsterCarnivalOwnerTeamsByAreaObjectId[areaObjectId] = team;
                }
            }

            return resolved;
        }

        internal static string ResolveRemoteAffectedAreaOwnerName(
            int ownerId,
            IReadOnlyDictionary<int, string> cachedOwnerNames,
            Func<int, string> resolveLiveOwnerName)
        {
            if (ownerId <= 0)
            {
                return null;
            }

            string liveOwnerName = resolveLiveOwnerName?.Invoke(ownerId);
            if (!string.IsNullOrWhiteSpace(liveOwnerName))
            {
                return liveOwnerName.Trim();
            }

            return cachedOwnerNames != null
                   && cachedOwnerNames.TryGetValue(ownerId, out string cachedOwnerName)
                   && !string.IsNullOrWhiteSpace(cachedOwnerName)
                ? cachedOwnerName
                : null;
        }

        internal static string ResolveRemoteAffectedAreaOwnerName(
            int areaObjectId,
            int ownerId,
            IReadOnlyDictionary<int, string> cachedOwnerNamesByAreaObjectId,
            IReadOnlyDictionary<int, string> cachedOwnerNames,
            Func<int, string> resolveLiveOwnerName)
        {
            if (ownerId <= 0)
            {
                return null;
            }

            string liveOwnerName = resolveLiveOwnerName?.Invoke(ownerId);
            if (!string.IsNullOrWhiteSpace(liveOwnerName))
            {
                return liveOwnerName.Trim();
            }

            if (areaObjectId > 0
                && cachedOwnerNamesByAreaObjectId != null
                && cachedOwnerNamesByAreaObjectId.TryGetValue(areaObjectId, out string cachedAreaOwnerName)
                && !string.IsNullOrWhiteSpace(cachedAreaOwnerName))
            {
                return cachedAreaOwnerName.Trim();
            }

            return cachedOwnerNames != null
                   && cachedOwnerNames.TryGetValue(ownerId, out string cachedOwnerName)
                   && !string.IsNullOrWhiteSpace(cachedOwnerName)
                ? cachedOwnerName.Trim()
                : null;
        }

        private string ResolveRemoteAffectedAreaOwnerName(int ownerId)
        {
            string ownerName = ResolveRemoteAffectedAreaOwnerName(
                ownerId,
                _remoteAffectedAreaOwnerNames,
                ResolveLiveRemoteAffectedAreaOwnerName);
            if (!string.IsNullOrWhiteSpace(ownerName))
            {
                _remoteAffectedAreaOwnerNames[ownerId] = ownerName;
            }

            return ownerName;
        }

        private string ResolveRemoteAffectedAreaOwnerName(int areaObjectId, int ownerId)
        {
            string ownerName = ResolveRemoteAffectedAreaOwnerName(
                areaObjectId,
                ownerId,
                _remoteAffectedAreaOwnerNamesByAreaObjectId,
                _remoteAffectedAreaOwnerNames,
                ResolveLiveRemoteAffectedAreaOwnerName);
            if (!string.IsNullOrWhiteSpace(ownerName))
            {
                _remoteAffectedAreaOwnerNames[ownerId] = ownerName;
                if (areaObjectId > 0)
                {
                    _remoteAffectedAreaOwnerNamesByAreaObjectId[areaObjectId] = ownerName;
                }

                return ownerName;
            }

            return null;
        }

        private string ResolveLiveRemoteAffectedAreaOwnerName(int ownerId)
        {
            return ownerId > 0 && _remoteUserPool.TryGetActor(ownerId, out RemoteUserActor actor)
                ? actor?.Name
                : null;
        }

        private Fields.MonsterCarnivalTeam? ResolveMonsterCarnivalAffectedAreaOwnerTeamByName(string characterName)
        {
            Fields.MonsterCarnivalField carnival = _specialFieldRuntime?.Minigames?.MonsterCarnival;
            return carnival?.IsVisible == true
                   && carnival.TryResolveCharacterTeam(characterName, out Fields.MonsterCarnivalTeam team)
                ? team
                : null;
        }
    }
}
