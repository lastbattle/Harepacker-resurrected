using System;
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

            if (RemoteAffectedAreaSupportResolver.IsAreaBuffItemType(packet.Type))
            {
                return UpsertRemoteAreaBuffItemAffectedArea(
                    packet.ObjectId,
                    packet.Type,
                    unchecked((int)packet.OwnerCharacterId),
                    packet.SkillId,
                    packet.Bounds,
                    packet.StartDelayUnits,
                    packet.ElementAttribute,
                    packet.Phase);
            }

            return packet.SkillId >= 10000
                ? UpsertRemotePlayerAffectedArea(
                    packet.ObjectId,
                    packet.Type,
                    unchecked((int)packet.OwnerCharacterId),
                    packet.SkillId,
                    packet.SkillLevel,
                    packet.Bounds,
                    packet.StartDelayUnits,
                    packet.ElementAttribute,
                    packet.Phase)
                : UpsertRemoteMobAffectedArea(
                    packet.ObjectId,
                    packet.Type,
                    unchecked((int)packet.OwnerCharacterId),
                    packet.SkillId,
                    packet.SkillLevel,
                    packet.Bounds,
                    packet.StartDelayUnits,
                    packet.ElementAttribute,
                    packet.Phase);
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

        private void UpdateRemoteAffectedAreaGameplay(int currentTime)
        {
            if (_affectedAreaPool == null)
            {
                return;
            }

            var activeProjectedSupportAreaIds = _playerManager?.Skills != null
                ? new System.Collections.Generic.HashSet<int>()
                : null;

            foreach (ActiveAffectedArea area in _affectedAreaPool.ActiveAreas.ToArray())
            {
                if (area?.IsActive(currentTime) != true)
                {
                    continue;
                }

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
            SkillData skill = _playerManager?.SkillLoader?.LoadSkill(area.SkillId);
            if (skill == null)
            {
                return;
            }

            SkillData[] supportSkills = ResolveRemoteAffectedAreaSupportSkills(skill);
            SkillLevelData levelData = ResolveRemoteAffectedAreaSkillLevel(skill, area.SkillLevel);
            SkillLevelData supportLevelData = ResolveRemoteAffectedAreaSupportLevelData(levelData, supportSkills);
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
                ResolveRemoteAffectedAreaHostileSkillRuntimes(skill, levelData, area.SkillLevel, supportSkills);
            if (hostileSkillRuntimes.Length == 0)
            {
                return;
            }

            if (_mobPool?.ActiveMobs == null || _mobPool.ActiveMobs.Count == 0)
            {
                return;
            }

            if (!_affectedAreaPool.TryBeginGameplayTick(area, currentTime, RemoteAffectedAreaFallbackTickMs))
            {
                return;
            }

            SkillManager skillManager = _playerManager?.Skills;
            if (skillManager == null)
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
                    IsAffectedAreaOwnerPartyMember(area.OwnerId),
                    IsAffectedAreaOwnerSameTeamMember(area.OwnerId),
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

            if (RemoteAffectedAreaSupportResolver.IsInvincibleZone(skill))
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
        }

        private static SkillLevelData ResolveRemoteAffectedAreaSupportLevelData(
            SkillLevelData primaryLevelData,
            params SkillData[] supportSkills)
        {
            if (supportSkills == null || supportSkills.Length == 0)
            {
                return primaryLevelData;
            }

            var levelDataEntries = new System.Collections.Generic.List<SkillLevelData>
            {
                primaryLevelData
            };

            for (int i = 0; i < supportSkills.Length; i++)
            {
                SkillData supportSkill = supportSkills[i];
                if (supportSkill == null)
                {
                    continue;
                }

                SkillLevelData supportLevelData = ResolveRemoteAffectedAreaSkillLevel(supportSkill, primaryLevelData?.Level ?? 1);
                if (supportLevelData != null)
                {
                    levelDataEntries.Add(supportLevelData);
                }
            }

            SkillLevelData projectedLevelData =
                RemoteAffectedAreaSupportResolver.CreateProjectedSupportBuffLevelData(levelDataEntries.ToArray()) ?? primaryLevelData;
            if (projectedLevelData == null)
            {
                return null;
            }

            int derivedDamageReductionRate = projectedLevelData.DamageReductionRate;
            for (int i = 0; i < supportSkills.Length; i++)
            {
                SkillData supportSkill = supportSkills[i];
                if (supportSkill == null)
                {
                    continue;
                }

                SkillLevelData supportLevelData = ResolveRemoteAffectedAreaSkillLevel(supportSkill, primaryLevelData?.Level ?? 1);
                derivedDamageReductionRate = Math.Max(
                    derivedDamageReductionRate,
                    RemoteAffectedAreaSupportResolver.ResolveDerivedProjectedDamageReductionRate(supportSkill, supportLevelData));
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

                    SkillLevelData supportLevelData = ResolveRemoteAffectedAreaSkillLevel(supportSkill, skillLevel);
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

        private static SkillLevelData ResolveRemoteAffectedAreaSkillLevel(SkillData skill, int level)
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

        private bool IsRemoteAffectedAreaProtectionActive(int currentTime)
        {
            PlayerCharacter player = _playerManager?.Player;
            if (player == null || _affectedAreaPool == null)
            {
                return false;
            }

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
                SkillLevelData levelData = ResolveRemoteAffectedAreaSkillLevel(skill, area.SkillLevel);
                SkillLevelData supportLevelData = ResolveRemoteAffectedAreaSupportLevelData(levelData, supportSkills);
                SkillLevelData effectiveLevelData = supportLevelData ?? levelData;
                if (!RemoteAffectedAreaSupportResolver.IsInvincibleZone(skill)
                    || !RemoteAffectedAreaSupportResolver.CanAffectLocalPlayer(
                        skill,
                        supportSkills,
                        localPlayerId,
                        area.OwnerId,
                        IsAffectedAreaOwnerPartyMember(area.OwnerId),
                        IsAffectedAreaOwnerSameTeamMember(area.OwnerId),
                        effectiveLevelData))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private bool IsAffectedAreaOwnerPartyMember(int ownerId)
        {
            if (ownerId <= 0 || !_remoteUserPool.TryGetActor(ownerId, out RemoteUserActor actor) || string.IsNullOrWhiteSpace(actor?.Name))
            {
                return false;
            }

            return _socialListRuntime.IsTrackedPartyMember(actor.Name);
        }

        private bool IsAffectedAreaOwnerSameTeamMember(int ownerId)
        {
            if (ownerId <= 0 || !_remoteUserPool.TryGetActor(ownerId, out RemoteUserActor actor))
            {
                return false;
            }

            Effects.BattlefieldField battlefield = _specialFieldRuntime?.SpecialEffects?.Battlefield;
            if (battlefield?.IsActive == true
                && battlefield.LocalTeamId.HasValue
                && battlefield.TryGetAssignedTeamId(ownerId, out int ownerBattlefieldTeamId))
            {
                return ownerBattlefieldTeamId == battlefield.LocalTeamId.Value;
            }

            Fields.MonsterCarnivalField carnival = _specialFieldRuntime?.Minigames?.MonsterCarnival;
            if (carnival?.IsVisible == true
                && !string.IsNullOrWhiteSpace(actor?.Name)
                && carnival.TryResolveCharacterTeam(actor.Name, out Fields.MonsterCarnivalTeam ownerCarnivalTeam))
            {
                return ownerCarnivalTeam == carnival.LocalTeam;
            }

            return false;
        }
    }
}
