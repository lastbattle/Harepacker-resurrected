using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Companions;
using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Loaders;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Physics;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Pools
{
    public enum RemoteRelationshipOverlayType
    {
        Generic = 0,
        Couple = 1,
        Friendship = 2,
        NewYearCard = 3,
        Marriage = 4
    }

    /// <summary>
    /// Shared owner for remote user actors. This gives the simulator a single seam
    /// for remote avatar look decode, map insertion/removal, world rendering,
    /// helper markers, team state, chairs, mounts, and prepared-skill overlays.
    /// </summary>
    public sealed class RemoteUserActorPool
    {
        public readonly record struct RemoteSkillUsePresentation(
            int CharacterId,
            int SkillId,
            int? ActionSpeed,
            bool FacingRight,
            int CurrentTime,
            IReadOnlyList<string> BranchNames = null,
            Vector2? WorldOrigin = null,
            bool FollowOwnerPosition = true,
            bool FollowOwnerFacing = true,
            int? DelayRateOverride = null);
        public readonly record struct RemoteUpgradeTombPresentation(
            int CharacterId,
            int ItemId,
            Vector2 Position,
            int CurrentTime);
        public readonly record struct RemoteHitFeedbackPresentation(
            int CharacterId,
            Vector2 Position,
            int Delta,
            int CurrentTime);

        private readonly record struct RemoteMechanicModePresentation(
            string StandActionName,
            string WalkActionName,
            string AttackActionName,
            string ProneActionName);

        public sealed class RemoteTemporaryStatAvatarEffectState
        {
            public int SkillId { get; init; }
            public SkillData Skill { get; init; }
            public SkillAnimation OverlayAnimation { get; init; }
            public SkillAnimation OverlaySecondaryAnimation { get; init; }
            public SkillAnimation UnderFaceAnimation { get; init; }
            public SkillAnimation UnderFaceSecondaryAnimation { get; init; }
            public int AnimationStartTime { get; init; }
        }

        public sealed class RemoteShadowPartnerPresentationState
        {
            public Point CurrentClientOffsetPx { get; set; }
            public Point ClientOffsetStartPx { get; set; }
            public Point ClientOffsetTargetPx { get; set; }
            public int ClientOffsetTransitionStartTime { get; set; }
            public bool IsInitialized { get; set; }
            public bool IsActionInitialized { get; set; }
            public string ObservedPlayerActionName { get; set; }
            public PlayerState ObservedPlayerState { get; set; }
            public bool ObservedPlayerFacingRight { get; set; }
            public int? ObservedRawActionCode { get; set; }
            public int ObservedPlayerActionTriggerTime { get; set; } = int.MinValue;
            public string CurrentActionName { get; set; }
            public SkillAnimation CurrentPlaybackAnimation { get; set; }
            public int CurrentActionStartTime { get; set; }
            public bool CurrentFacingRight { get; set; }
            public string PendingActionName { get; set; }
            public SkillAnimation PendingPlaybackAnimation { get; set; }
            public int PendingActionReadyTime { get; set; }
            public bool PendingFacingRight { get; set; }
            public bool PendingForceReplay { get; set; }
            public string QueuedActionName { get; set; }
            public SkillAnimation QueuedPlaybackAnimation { get; set; }
            public bool QueuedFacingRight { get; set; }
            public bool QueuedForceReplay { get; set; }
        }

        public sealed class RemotePacketOwnedEmotionState
        {
            public int ItemId { get; init; }
            public int EmotionId { get; init; }
            public string EmotionName { get; init; }
            public bool ByItemOption { get; init; }
            public SkillAnimation EffectAnimation { get; init; }
            public int AnimationStartTime { get; init; }
            public int ExpireTime { get; init; }
        }

        public sealed class RemoteHitState
        {
            public sbyte AttackIndex { get; init; }
            public int Damage { get; init; }
            public int? MobTemplateId { get; init; }
            public bool MobHitFacingLeft { get; init; }
            public bool HasMobHit { get; init; }
            public bool PowerGuard { get; init; }
            public int? MobId { get; init; }
            public byte? MobHitAction { get; init; }
            public short? MobHitX { get; init; }
            public short? MobHitY { get; init; }
            public byte IncDecType { get; init; }
            public byte HitFlags { get; init; }
            public int HpDelta { get; init; }
            public int? SkillId { get; init; }
            public int PacketTime { get; init; }
        }

        public sealed class RemoteTransientSkillUseAvatarEffectState
        {
            public int RegistrationKey { get; init; }
            public SkillAnimation OverlayAnimation { get; init; }
            public SkillAnimation UnderFaceAnimation { get; init; }
            public int AnimationStartTime { get; init; }
        }

        private sealed class PendingRemoteTransientSkillUseAvatarEffectState
        {
            public int RegistrationKey { get; init; }
            public SkillAnimation OverlayAnimation { get; init; }
            public SkillAnimation UnderFaceAnimation { get; init; }
        }

        internal readonly record struct PortableChairPairParticipant(
            int CharacterId,
            PortableChair Chair,
            Vector2 Position,
            bool FacingRight,
            int? PreferredPairCharacterId,
            bool IsChairSessionActive,
            bool IsVisibleInWorld,
            bool IsRelationshipOverlaySuppressed);

        internal readonly record struct PortableChairPairRecord(
            int CharacterId,
            int ItemId,
            int? PreferredPairCharacterId,
            int? PairCharacterId = null,
            int Status = 0)
        {
            public bool IsActive => Status != 0 && PairCharacterId.HasValue && PairCharacterId.Value > 0;
        }

        private readonly record struct PortableChairPairCandidate(
            PortableChairPairParticipant Left,
            PortableChairPairParticipant Right,
            int Priority,
            float Score);

        private const int MinimumMeleeAfterImageFadeDurationMs = 60;
        private const float FollowDriverGroundHorizontalOffset = 50f;
        private const float FollowDriverLadderRopeVerticalOffset = 30f;
        private const float RemoteDragonGroundSideOffset = 42f;
        private const float RemoteDragonGroundVerticalOffset = -12f;
        private const float RemoteDragonLadderSideOffset = 34f;
        private const float RemoteDragonLadderVerticalOffset = 18f;
        private const float RemoteDragonKeyDownBarHalfWidth = 36f;
        private const float RemoteDragonKeyDownBarVerticalGap = 30f;
        private const int MechanicTamingMobItemId = 1932016;
        private const float CarryItemEffectOrbitRadiusX = 26f;
        private const float CarryItemEffectOrbitRadiusY = 16f;
        private const float CarryItemEffectBaseVerticalOffset = -46f;
        private const int CarryItemEffectOrbitDurationMs = 2000;
        private const int CarryItemEffectMaximumCount = 99;
        private const int CarryItemEffectAnimationOffsetMs = 120;
        private const int RelationshipOverlayVisibleRangeX = 700;
        private const int RelationshipOverlayVisibleRangeY = 500;
        private const int RelationshipOverlayNearRangeX = 100;
        private const int RelationshipOverlayNearRangeY = 100;
        private const int NewYearCardOverlayNearRangeX = 250;
        private const int NewYearCardOverlayNearRangeY = 250;
        private const int NewYearCardDefaultItemId = 4300000;
        private const int RemoteShadowPartnerClientSideOffsetPx = 30;
        private const int RemoteShadowPartnerClientBackActionOffsetYPx = 50;
        private const int RemoteShadowPartnerTransitionDurationMs = 200;
        private const int RemoteShadowPartnerAttackDelayMs = 90;
        private const int RemoteMovingShootNoPrepareAnimationSkillId = 33121009;
        private const int RemoteReceiveHpGaugeWidth = 46;
        private const int RemoteReceiveHpGaugeHeight = 5;
        private const int RemoteReceiveHpGaugeVerticalPadding = 4;
        private static readonly int[] RemoteShadowPartnerSkillIds =
        {
            4111002,
            4211008,
            14111000
        };
        private static readonly int[] RemoteSoulArrowSkillIds =
        {
            4121006,
            33101003,
            13101003,
            3201004,
            3101004
        };
        private static readonly int[] RemoteAuraSkillIds =
        {
            RemoteUserTemporaryStatKnownState.DarkAuraSkillId,
            RemoteUserTemporaryStatKnownState.BlueAuraSkillId,
            RemoteUserTemporaryStatKnownState.YellowAuraSkillId
        };
        private static readonly int[] RemoteBarrierSkillIds =
        {
            21120007,
            23111005,
            20011010,
            20001010,
            10001010
        };
        private static readonly int[] RemoteBlessingArmorSkillIds =
        {
            RemoteUserTemporaryStatKnownState.PaladinBlessingArmorSkillId,
            RemoteUserTemporaryStatKnownState.BishopBlessingArmorSkillId
        };
        private static readonly int[] RemoteWeaponChargeSkillIds =
        {
            1211004,
            1211006,
            1211008,
            1221004,
            15101006,
            21111005
        };
        private static readonly HashSet<int> RemotePreparedSkillUseEffectSkillIds = new()
        {
            35001001,
            35101009
        };
        private static readonly Color RemoteShadowPartnerTint = new(255, 255, 255, 150);
        private static readonly EquipSlot[] BattlefieldAppearanceSlots =
        {
            EquipSlot.Cap,
            EquipSlot.Coat,
            EquipSlot.Longcoat,
            EquipSlot.Pants,
            EquipSlot.Shoes,
            EquipSlot.Glove,
            EquipSlot.Cape,
        };
        private static readonly Dictionary<int, RemoteDragonHudMetadata> RemoteDragonHudMetadataCache = new();
        private static readonly IComparer<RemoteUserActor> VisibleWorldActorComparer = Comparer<RemoteUserActor>.Create(static (left, right) =>
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left is null)
            {
                return 1;
            }

            if (right is null)
            {
                return -1;
            }

            int yComparison = left.Position.Y.CompareTo(right.Position.Y);
            if (yComparison != 0)
            {
                return yComparison;
            }

            return StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
        });
        private static readonly IReadOnlyDictionary<int, RemoteMechanicModePresentation> RemoteMechanicModePresentationBySkillId =
            new Dictionary<int, RemoteMechanicModePresentation>
            {
                [35001001] = new("tank_stand", "tank_walk", "tank", "tank_prone"),
                [35101004] = new("tank_stand", "tank_walk", "tank", "tank_prone"),
                [35101009] = new("tank_stand", "tank_walk", "tank", "tank_prone"),
                [35121003] = new("tank_stand", "tank_walk", "tank", "tank_prone"),
                [35121005] = new("tank_stand", "tank_walk", "tank", "tank_prone"),
                [35121009] = new("tank_stand", "tank_walk", "tank", "tank_prone"),
                [35121010] = new("tank_stand", "tank_walk", "tank", "tank_prone"),
                [35111004] = new("siege_stand", "siege_stand", "siege", "siege_stand"),
                [35121013] = new("tank_siegestand", "tank_siegestand", "tank_siegeattack", "tank_siegestand")
            };

        private readonly Dictionary<int, RemoteUserActor> _actorsById = new();
        private readonly Dictionary<string, int> _actorIdsByName = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, PortableChairPairRecord> _portableChairPairRecordsByCharacterId = new();
        private int? _localPortableChairPreferredPairCharacterId;
        private readonly Dictionary<RemoteRelationshipOverlayType, Dictionary<int, RemoteUserRelationshipRecord>> _relationshipRecordsByOwnerCharacterId = new();
        private readonly Dictionary<RemoteRelationshipOverlayType, Dictionary<RemoteRelationshipRecordDispatchKey, int>> _relationshipRecordOwnerByDispatchKey = new();
        private readonly List<RemoteUserActor> _visibleWorldActorsBuffer = new();
        private readonly List<StatusBarPreparedSkillRenderData> _preparedSkillWorldOverlayBuffer = new();
        private readonly List<MinimapUI.TrackedUserMarker> _helperMarkerBuffer = new();
        private readonly HashSet<(int LeftId, int RightId)> _renderedCouplePairsBuffer = new();
        private readonly HashSet<(RemoteRelationshipOverlayType Type, int ItemId, int LeftId, int RightId)> _renderedItemEffectPairsBuffer = new();
        private readonly Dictionary<int, List<PendingRemoteTransientSkillUseAvatarEffectState>> _pendingTransientSkillUseAvatarEffectsByCharacterId = new();
        private int _preparedSkillWorldOverlayCount;
        private int _helperMarkerCount;
        private CharacterLoader _loader;
        private SkillLoader _skillLoader;
        private Texture2D _pixelTexture;
        private GraphicsDevice _pixelTextureDevice;

        public int Count => _actorsById.Count;
        public IEnumerable<RemoteUserActor> Actors => _actorsById.Values;
        public event Action<RemoteSkillUsePresentation> SkillUseRegistered;
        public event Action<RemoteUpgradeTombPresentation> UpgradeTombEffectRegistered;
        public event Action<RemoteHitFeedbackPresentation> HitFeedbackRegistered;
        public int PreparedSkillWorldOverlayCount => _preparedSkillWorldOverlayCount;
        public int HelperMarkerCount => _helperMarkerCount;
        public Action<int, string> ActorRemovedCallback { get; set; }

        public void Initialize(CharacterLoader loader, SkillLoader skillLoader)
        {
            _loader = loader;
            _skillLoader = skillLoader;
            EnsureRelationshipRecordTablesInitialized();
        }

        public void Clear()
        {
            foreach (RemoteUserActor actor in _actorsById.Values.ToArray())
            {
                ClearActorFollowLinks(actor);
                NotifyActorRemoved(actor.CharacterId, actor.Name);
            }

            _actorsById.Clear();
            _actorIdsByName.Clear();
            _pendingTransientSkillUseAvatarEffectsByCharacterId.Clear();
            _portableChairPairRecordsByCharacterId.Clear();
            _localPortableChairPreferredPairCharacterId = null;
            ClearRelationshipRecordTables();
        }

        public void RemoveBySourceTag(string sourceTag)
        {
            if (string.IsNullOrWhiteSpace(sourceTag))
            {
                return;
            }

            foreach (int characterId in _actorsById.Values
                .Where(actor => string.Equals(actor.SourceTag, sourceTag.Trim(), StringComparison.OrdinalIgnoreCase))
                .Select(actor => actor.CharacterId)
                .ToArray())
            {
                if (_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
                {
                    ClearActorFollowLinks(actor);
                    ClearPortableChairPairRecord(characterId);
                    NotifyActorRemoved(actor.CharacterId, actor.Name);
                    _actorIdsByName.Remove(actor.Name);
                    _actorsById.Remove(characterId);
                    _pendingTransientSkillUseAvatarEffectsByCharacterId.Remove(characterId);
                }
            }
        }

        public void RemoveBySourceTagExcept(string sourceTag, IReadOnlySet<int> keepCharacterIds)
        {
            if (string.IsNullOrWhiteSpace(sourceTag))
            {
                return;
            }

            keepCharacterIds ??= new HashSet<int>();
            foreach (int characterId in _actorsById.Values
                .Where(actor => string.Equals(actor.SourceTag, sourceTag.Trim(), StringComparison.OrdinalIgnoreCase)
                    && !keepCharacterIds.Contains(actor.CharacterId))
                .Select(actor => actor.CharacterId)
                .ToArray())
            {
                if (_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
                {
                    ClearActorFollowLinks(actor);
                    ClearPortableChairPairRecord(characterId);
                    NotifyActorRemoved(actor.CharacterId, actor.Name);
                    _actorIdsByName.Remove(actor.Name);
                    _actorsById.Remove(characterId);
                }
            }
        }

        public bool TryGetActor(int characterId, out RemoteUserActor actor)
        {
            return _actorsById.TryGetValue(characterId, out actor);
        }

        public bool TryGetActorByName(string name, out RemoteUserActor actor)
        {
            actor = null;
            return !string.IsNullOrWhiteSpace(name)
                   && _actorIdsByName.TryGetValue(name.Trim(), out int characterId)
                   && _actorsById.TryGetValue(characterId, out actor);
        }

        public bool TryGetPosition(string name, out Vector2 position)
        {
            if (TryGetActorByName(name, out RemoteUserActor actor))
            {
                position = actor.Position;
                return true;
            }

            position = default;
            return false;
        }

        public bool TryAddOrUpdate(
            int characterId,
            CharacterBuild build,
            Vector2 position,
            out string message,
            bool facingRight = true,
            string actionName = null,
            string sourceTag = null,
            bool isVisibleInWorld = true)
        {
            message = null;
            if (characterId <= 0)
            {
                message = "Remote character ID must be positive.";
                return false;
            }

            if (build == null)
            {
                message = "Remote character build is required.";
                return false;
            }

            build.Id = characterId;
            if (string.IsNullOrWhiteSpace(build.Name))
            {
                build.Name = $"Remote{characterId}";
            }

            if (_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                string previousName = actor.Name;
                actor.BeginMeleeAfterImageFade(Environment.TickCount);
                ResetBattlefieldAppearanceState(actor);
                actor.Build = build;
                actor.Name = build.Name.Trim();
                actor.Position = position;
                actor.FacingRight = facingRight;
                SetActorAction(actor, actionName, actor.Build.ActivePortableChair != null, Environment.TickCount);
                actor.SourceTag = string.IsNullOrWhiteSpace(sourceTag) ? actor.SourceTag : sourceTag.Trim();
                actor.IsVisibleInWorld = isVisibleInWorld;
                SyncTemporaryStatPresentation(actor);
                RegisterMeleeAfterImage(actor, 0, actor.ActionName, Environment.TickCount, 10, 0);
                UpdateNameLookup(previousName, actor.Name, characterId);
                SyncRelationshipOverlaysFromRecords(actor.CharacterId, Environment.TickCount);
                ApplyPendingTransientSkillUseAvatarEffects(actor, Environment.TickCount);
                return true;
            }

            RemoteUserActor created = new RemoteUserActor(
                characterId,
                build.Name.Trim(),
                build,
                position,
                facingRight,
                NormalizeActionName(actionName, build.ActivePortableChair != null),
                string.IsNullOrWhiteSpace(sourceTag) ? "remote" : sourceTag.Trim(),
                isVisibleInWorld);
            created.BaseActionName = created.ActionName;
            RegisterMeleeAfterImage(created, 0, created.ActionName, Environment.TickCount, 10, 0);
            _actorsById[characterId] = created;
            _actorIdsByName[created.Name] = characterId;
            SyncTemporaryStatPresentation(created);
            SyncRelationshipOverlaysFromRecords(created.CharacterId, Environment.TickCount);
            ApplyPendingTransientSkillUseAvatarEffects(created, Environment.TickCount);
            return true;
        }

        public bool TryAddOrUpdateAvatarLook(
            int characterId,
            string name,
            LoginAvatarLook avatarLook,
            CharacterBuild template,
            Vector2 position,
            out string message,
            bool facingRight,
            string actionName,
            string sourceTag,
            bool isVisibleInWorld)
        {
            message = null;
            if (_loader == null)
            {
                message = "Character loader is not available.";
                return false;
            }

            if (avatarLook == null)
            {
                message = "AvatarLook payload is required.";
                return false;
            }

            CharacterBuild build = _loader.LoadFromAvatarLook(avatarLook, template);
            if (build == null)
            {
                message = "AvatarLook could not be converted into a remote character build.";
                return false;
            }

            ClearTemplateOnlyProfileState(build);

            if (!string.IsNullOrWhiteSpace(name))
            {
                build.Name = name.Trim();
            }

            return TryAddOrUpdate(
                characterId,
                build,
                position,
                out message,
                facingRight,
                actionName,
                sourceTag,
                isVisibleInWorld);
        }

        private static void ClearTemplateOnlyProfileState(CharacterBuild build)
        {
            if (build == null)
            {
                return;
            }

            // AvatarLook packets do not author these profile-window fields. Clear them so
            // remote inspect does not inherit unrelated local-player metadata from the
            // loader template used for visual decode fallbacks.
            build.ActivePortableChair = null;
            build.HasAuthoritativeProfileLevel = false;
            build.HasAuthoritativeProfileJob = false;
            build.HasAuthoritativeProfileGuild = false;
            build.HasAuthoritativeProfileAlliance = false;
            build.HasAuthoritativeProfileFame = false;
            build.HasAuthoritativeProfileWorldRank = false;
            build.HasAuthoritativeProfileJobRank = false;
            build.HasAuthoritativeProfileRide = false;
            build.HasAuthoritativeProfileTraits = false;
            build.HasAuthoritativeProfilePendantSlot = false;
            build.HasAuthoritativeProfilePocketSlot = false;
            build.HasAuthoritativeProfileMedal = false;
            build.HasAuthoritativeProfileCollection = false;
            build.GuildName = string.Empty;
            build.AllianceName = string.Empty;
            build.Fame = 0;
            build.WorldRank = 0;
            build.JobRank = 0;
            build.HasMonsterRiding = false;
            build.HasPendantSlotExtension = false;
            build.HasPocketSlot = false;
            build.TraitCharisma = 0;
            build.TraitInsight = 0;
            build.TraitWill = 0;
            build.TraitCraft = 0;
            build.TraitSense = 0;
            build.TraitCharm = 0;
        }

        public bool TryApplyProfileMetadata(
            int characterId,
            int? level,
            string guildName,
            int? jobId,
            out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor) || actor?.Build == null)
            {
                message = $"Remote user {characterId} does not exist.";
                return false;
            }

            CharacterBuild build = actor.Build;
            if (level.HasValue && level.Value > 0)
            {
                build.Level = Math.Max(1, level.Value);
                build.HasAuthoritativeProfileLevel = true;
            }

            if (jobId.HasValue && jobId.Value >= 0)
            {
                build.Job = jobId.Value;
                build.JobName = SkillDataLoader.GetJobName(jobId.Value);
                build.HasAuthoritativeProfileJob = true;
            }

            if (guildName != null)
            {
                build.GuildName = string.IsNullOrWhiteSpace(guildName) ? string.Empty : guildName.Trim();
                build.HasAuthoritativeProfileGuild = true;
            }

            message = $"Remote user {characterId} profile metadata applied.";
            return true;
        }

        public bool TryApplyGuildMark(
            int characterId,
            int markBackgroundId,
            int markBackgroundColor,
            int markId,
            int markColor,
            out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor) || actor?.Build == null)
            {
                message = $"Remote user {characterId} does not exist.";
                return false;
            }

            actor.Build.GuildMarkBackgroundId = markBackgroundId > 0 ? markBackgroundId : null;
            actor.Build.GuildMarkBackgroundColor = markBackgroundColor;
            actor.Build.GuildMarkId = markId > 0 ? markId : null;
            actor.Build.GuildMarkColor = markColor;
            message = $"Remote user {characterId} guild mark applied.";
            return true;
        }

        public bool HasActiveMarriageRelationshipRecord(int characterId)
        {
            if (characterId <= 0)
            {
                return false;
            }

            EnsureRelationshipRecordTablesInitialized();
            Dictionary<int, RemoteUserRelationshipRecord> marriageTable = GetRelationshipRecordTable(RemoteRelationshipOverlayType.Marriage);
            if (marriageTable.TryGetValue(characterId, out RemoteUserRelationshipRecord directRecord) &&
                directRecord.IsActive)
            {
                return true;
            }

            foreach (KeyValuePair<int, RemoteUserRelationshipRecord> entry in marriageTable)
            {
                RemoteUserRelationshipRecord marriageRecord = entry.Value;
                if (!marriageRecord.IsActive)
                {
                    continue;
                }

                if (ResolveMarriagePairCharacterId(entry.Key, marriageRecord) == characterId)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryMove(int characterId, Vector2 position, bool? facingRight, string actionName, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            actor.Position = position;
            if (facingRight.HasValue)
            {
                actor.FacingRight = facingRight.Value;
            }

            if (!string.IsNullOrWhiteSpace(actionName))
            {
                actor.BeginMeleeAfterImageFade(Environment.TickCount);
                SetActorAction(
                    actor,
                    actionName,
                    actor.Build.ActivePortableChair != null,
                    Environment.TickCount,
                    forceReplay: true,
                    rawActionCode: CharacterPart.TryGetClientRawActionCode(actionName, out int actionCode) ? actionCode : null);
                RegisterMeleeAfterImage(actor, 0, actor.ActionName, Environment.TickCount, 10, 0);
            }

            return true;
        }

        public bool TryApplyFollowCharacter(
            int characterId,
            int driverId,
            bool transferField,
            Vector2? transferPosition,
            int localCharacterId,
            Vector2 localCharacterPosition,
            out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            if (driverId > 0 && driverId == characterId)
            {
                message = $"Remote character {characterId} cannot follow itself.";
                return false;
            }

            int previousDriverId = actor.FollowDriverId;
            ClearDriverPassengerLink(previousDriverId, characterId);

            if (driverId > 0)
            {
                actor.FollowDriverId = driverId;
                AssignDriverPassengerLink(driverId, characterId);
                return true;
            }

            actor.FollowDriverId = 0;
            if (transferField && transferPosition.HasValue)
            {
                actor.Position = transferPosition.Value;
            }
            else if (previousDriverId > 0)
            {
                if (previousDriverId == localCharacterId)
                {
                    actor.Position = localCharacterPosition;
                }
                else if (_actorsById.TryGetValue(previousDriverId, out RemoteUserActor previousDriver))
                {
                    actor.Position = previousDriver.Position;
                }
            }

            return true;
        }

        public bool TryAssignLocalPassengerToDriver(int driverId, int localPassengerId, out string message)
        {
            message = null;
            if (driverId <= 0)
            {
                message = "Remote driver ID must be positive.";
                return false;
            }

            if (localPassengerId <= 0)
            {
                message = "Local passenger ID must be positive.";
                return false;
            }

            if (!_actorsById.TryGetValue(driverId, out _))
            {
                message = $"Remote driver {driverId} does not exist.";
                return false;
            }

            foreach (RemoteUserActor actor in _actorsById.Values)
            {
                if (actor.FollowPassengerId == localPassengerId)
                {
                    actor.FollowPassengerId = 0;
                }
            }

            AssignDriverPassengerLink(driverId, localPassengerId);
            return true;
        }

        public bool TryClearLocalPassengerFromDriver(int driverId, int localPassengerId, out string message)
        {
            message = null;
            if (driverId <= 0)
            {
                message = "Remote driver ID must be positive.";
                return false;
            }

            if (localPassengerId <= 0)
            {
                message = "Local passenger ID must be positive.";
                return false;
            }

            if (!_actorsById.TryGetValue(driverId, out RemoteUserActor driverActor))
            {
                message = $"Remote driver {driverId} does not exist.";
                return false;
            }

            if (driverActor.FollowPassengerId == localPassengerId)
            {
                driverActor.FollowPassengerId = 0;
            }

            return true;
        }

        public bool TrySetAction(int characterId, string actionName, bool? facingRight, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            actor.BeginMeleeAfterImageFade(Environment.TickCount);
            SetActorAction(
                actor,
                actionName,
                actor.Build.ActivePortableChair != null,
                Environment.TickCount,
                forceReplay: true,
                rawActionCode: CharacterPart.TryGetClientRawActionCode(actionName, out int actionCode) ? actionCode : null);
            if (facingRight.HasValue)
            {
                actor.FacingRight = facingRight.Value;
            }

            actor.MovementDrivenActionSelection = false;
            RegisterMeleeAfterImage(actor, 0, actor.ActionName, Environment.TickCount, 10, 0);
            return true;
        }

        public bool TryRegisterMeleeAfterImage(
            int characterId,
            int skillId,
            string actionName,
            int? actionCode,
            int masteryPercent,
            int chargeSkillId,
            int? actionSpeed,
            int? preparedSkillReleaseFollowUpValue,
            bool? facingRight,
            int currentTime,
            out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            if (facingRight.HasValue)
            {
                actor.FacingRight = facingRight.Value;
            }

            ApplyRemotePreparedSkillRelease(actor, skillId, preparedSkillReleaseFollowUpValue);

            SkillData skill = null;
            if (skillId > 0 && _skillLoader != null)
            {
                skill = _skillLoader.LoadSkill(skillId);
            }

            int chargeElement = ResolveRemoteAfterImageChargeElement(actor, chargeSkillId);
            string resolvedActionName = ResolveRemoteMeleeActionName(
                actor,
                skill,
                actionName,
                actionCode,
                masteryPercent,
                chargeElement);

            if (!string.IsNullOrWhiteSpace(resolvedActionName))
            {
                actor.BeginMeleeAfterImageFade(currentTime);
                SetActorAction(
                    actor,
                    resolvedActionName,
                    actor.Build.ActivePortableChair != null,
                    currentTime,
                    forceReplay: true,
                    rawActionCode: actionCode);
            }
            RegisterMeleeAfterImage(actor, skillId, actor.ActionName, currentTime, masteryPercent, chargeElement, actionCode);
            if (skillId > 0)
            {
                SkillUseRegistered?.Invoke(new RemoteSkillUsePresentation(
                    actor.CharacterId,
                    skillId,
                    actionSpeed,
                    actor.FacingRight,
                    currentTime));
            }

            return true;
        }

        public bool TryApplyTransientSkillUseAvatarEffect(
            int characterId,
            int registrationKey,
            SkillAnimation overlayAnimation,
            SkillAnimation underFaceAnimation,
            int currentTime,
            out string message)
        {
            message = null;
            if (registrationKey <= 0 || (overlayAnimation == null && underFaceAnimation == null))
            {
                message = "Transient skill-use avatar effect data is incomplete.";
                return false;
            }

            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            ApplyTransientSkillUseAvatarEffect(actor, registrationKey, overlayAnimation, underFaceAnimation, currentTime);
            return true;
        }

        public bool TryQueueTransientSkillUseAvatarEffect(
            int characterId,
            int registrationKey,
            SkillAnimation overlayAnimation,
            SkillAnimation underFaceAnimation,
            int currentTime,
            out string message)
        {
            message = null;
            if (characterId <= 0)
            {
                message = "Remote character ID must be positive.";
                return false;
            }

            if (registrationKey <= 0 || (overlayAnimation == null && underFaceAnimation == null))
            {
                message = "Transient skill-use avatar effect data is incomplete.";
                return false;
            }

            if (_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                ApplyTransientSkillUseAvatarEffect(actor, registrationKey, overlayAnimation, underFaceAnimation, currentTime);
                return true;
            }

            if (!_pendingTransientSkillUseAvatarEffectsByCharacterId.TryGetValue(characterId, out var pendingEffects))
            {
                pendingEffects = new List<PendingRemoteTransientSkillUseAvatarEffectState>();
                _pendingTransientSkillUseAvatarEffectsByCharacterId[characterId] = pendingEffects;
            }

            pendingEffects.RemoveAll(effect => effect?.RegistrationKey == registrationKey);
            pendingEffects.Add(new PendingRemoteTransientSkillUseAvatarEffectState
            {
                RegistrationKey = registrationKey,
                OverlayAnimation = overlayAnimation,
                UnderFaceAnimation = underFaceAnimation
            });
            message = $"Queued transient skill-use avatar effect for remote character {characterId}.";
            return true;
        }

        public bool TryQueueTransientSkillUseAvatarEffect(
            int characterId,
            int registrationKey,
            SkillAnimation overlayAnimation,
            SkillAnimation underFaceAnimation,
            out string message)
        {
            return TryQueueTransientSkillUseAvatarEffect(
                characterId,
                registrationKey,
                overlayAnimation,
                underFaceAnimation,
                Environment.TickCount,
                out message);
        }

        private static void ApplyTransientSkillUseAvatarEffect(
            RemoteUserActor actor,
            int registrationKey,
            SkillAnimation overlayAnimation,
            SkillAnimation underFaceAnimation,
            int currentTime)
        {
            actor.TransientSkillUseAvatarEffects.RemoveAll(effect => effect?.RegistrationKey == registrationKey);
            actor.TransientSkillUseAvatarEffects.Add(new RemoteTransientSkillUseAvatarEffectState
            {
                RegistrationKey = registrationKey,
                OverlayAnimation = overlayAnimation,
                UnderFaceAnimation = underFaceAnimation,
                AnimationStartTime = currentTime
            });
        }

        private void ApplyPendingTransientSkillUseAvatarEffects(RemoteUserActor actor, int currentTime)
        {
            if (actor == null ||
                !_pendingTransientSkillUseAvatarEffectsByCharacterId.TryGetValue(actor.CharacterId, out var pendingEffects) ||
                pendingEffects == null ||
                pendingEffects.Count == 0)
            {
                return;
            }

            for (int i = 0; i < pendingEffects.Count; i++)
            {
                PendingRemoteTransientSkillUseAvatarEffectState pendingEffect = pendingEffects[i];
                if (pendingEffect == null)
                {
                    continue;
                }

                ApplyTransientSkillUseAvatarEffect(
                    actor,
                    pendingEffect.RegistrationKey,
                    pendingEffect.OverlayAnimation,
                    pendingEffect.UnderFaceAnimation,
                    currentTime);
            }

            _pendingTransientSkillUseAvatarEffectsByCharacterId.Remove(actor.CharacterId);
        }

        public bool TryApplyMoveAction(int characterId, byte moveAction, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            actor.LastMoveActionRaw = moveAction;
            actor.FacingRight = DecodeFacingRight(moveAction);
            actor.BeginMeleeAfterImageFade(Environment.TickCount);
            SetActorAction(
                actor,
                ResolveActionName(actor, MoveActionFromRaw(moveAction)),
                actor.Build.ActivePortableChair != null,
                Environment.TickCount,
                rawActionCode: moveAction);
            actor.MovementDrivenActionSelection = true;
            RegisterMeleeAfterImage(actor, 0, actor.ActionName, Environment.TickCount, 10, 0);
            return true;
        }

        public bool TryApplyMoveSnapshot(int characterId, PlayerMovementSyncSnapshot movementSnapshot, byte moveAction, int currentTime, out string message)
        {
            message = null;
            if (movementSnapshot == null)
            {
                message = "Remote movement snapshot is required.";
                return false;
            }

            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            actor.MovementSnapshot = movementSnapshot;
            actor.LastMoveActionRaw = moveAction;
            actor.MovementDrivenActionSelection = true;
            ApplyMovementSnapshot(actor, currentTime);
            return true;
        }

        public bool TrySetHelperMarker(int characterId, MinimapUI.HelperMarkerType? markerType, bool showDirectionOverlay, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            actor.HelperMarkerType = markerType;
            actor.ShowDirectionOverlay = showDirectionOverlay;
            return true;
        }

        public bool TrySetBattlefieldTeam(int characterId, int? teamId, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            actor.BattlefieldTeamId = teamId;
            return true;
        }

        public void SyncBattlefieldAppearance(BattlefieldField battlefield)
        {
            foreach (RemoteUserActor actor in _actorsById.Values)
            {
                SyncBattlefieldAppearance(actor, battlefield);
            }
        }

        public bool TrySetPortableChair(
            int characterId,
            int? chairItemId,
            out string message,
            int? pairCharacterId = null,
            bool syncPairRecordFromChairState = false)
        {
            message = null;
            if (_loader == null)
            {
                message = "Character loader is not available.";
                return false;
            }

            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            ClearPortableChairMountState(actor);
            if (!chairItemId.HasValue || chairItemId.Value <= 0)
            {
                actor.Build.ActivePortableChair = null;
                actor.PreferredPortableChairPairCharacterId = null;
                if (syncPairRecordFromChairState)
                {
                    SyncPortableChairRecordFromChairState(characterId, chairItemId: null, preferredPairCharacterId: null);
                }

                SetActorAction(actor, CharacterPart.GetActionString(CharacterAction.Stand1), allowSitFallback: false, Environment.TickCount);
                SyncTemporaryStatPresentation(actor);
                actor.ClearMeleeAfterImage();
                return true;
            }

            PortableChair chair = _loader.LoadPortableChair(chairItemId.Value);
            if (chair == null)
            {
                message = $"Portable chair {chairItemId.Value} could not be loaded.";
                return false;
            }

            actor.Build.ActivePortableChair = chair;
            actor.PreferredPortableChairPairCharacterId = pairCharacterId;
            if (syncPairRecordFromChairState)
            {
                SyncPortableChairRecordFromChairState(actor.CharacterId, chair.ItemId, pairCharacterId);
            }

            ApplyPortableChairMount(actor, chair);
            SetActorAction(actor, "sit", allowSitFallback: true, Environment.TickCount);
            SyncTemporaryStatPresentation(actor);
            actor.ClearMeleeAfterImage();
            return true;
        }

        public bool TryApplyPortableChairRecordAdd(
            RemoteUserPortableChairRecordAddPacket packet,
            out string message)
        {
            message = null;
            if (packet.CharacterId <= 0)
            {
                message = "Portable-chair record add requires a positive character ID.";
                return false;
            }

            if (packet.ChairItemId <= 0)
            {
                message = "Portable-chair record add requires a positive chair item ID.";
                return false;
            }

            TryApplyPortableChairRecordRemove(
                new RemoteUserPortableChairRecordRemovePacket(packet.CharacterId),
                out _);
            SyncPortableChairPairRecord(
                packet.CharacterId,
                packet.ChairItemId,
                ResolvePortableChairPairPreference(packet.CharacterId));

            message = _actorsById.ContainsKey(packet.CharacterId)
                ? $"Remote user {packet.CharacterId} couple-chair record applied."
                : $"Stored remote couple-chair record for inactive owner {packet.CharacterId}.";
            return true;
        }

        public bool TryApplyPortableChairRecordRemove(
            RemoteUserPortableChairRecordRemovePacket packet,
            out string message)
        {
            message = null;
            if (!_portableChairPairRecordsByCharacterId.TryGetValue(packet.CharacterId, out PortableChairPairRecord existingRecord))
            {
                message = $"No remote couple-chair record matched character {packet.CharacterId}.";
                return false;
            }

            if (existingRecord.PairCharacterId.HasValue
                && existingRecord.PairCharacterId.Value > 0
                && _portableChairPairRecordsByCharacterId.TryGetValue(existingRecord.PairCharacterId.Value, out PortableChairPairRecord partnerRecord))
            {
                _portableChairPairRecordsByCharacterId[existingRecord.PairCharacterId.Value] = partnerRecord with
                {
                    PairCharacterId = null,
                    Status = 0
                };
            }

            ClearPortableChairPairRecord(packet.CharacterId);
            message = $"Removed remote couple-chair record for {packet.CharacterId}.";
            return true;
        }

        public bool TrySetMount(int characterId, int? tamingMobItemId, out string message)
        {
            message = null;
            if (_loader == null)
            {
                message = "Character loader is not available.";
                return false;
            }

            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            if (!tamingMobItemId.HasValue || tamingMobItemId.Value <= 0)
            {
                actor.Build.Unequip(EquipSlot.TamingMob);
                SyncTemporaryStatPresentation(actor);
                RegisterMeleeAfterImage(actor, 0, actor.ActionName, Environment.TickCount, 10, 0);
                return true;
            }

            CharacterPart mountPart = _loader.LoadEquipment(tamingMobItemId.Value);
            if (mountPart?.Slot != EquipSlot.TamingMob)
            {
                message = $"Item {tamingMobItemId.Value} is not a taming mob mount.";
                return false;
            }

            actor.Build.Equip(mountPart);
            SyncTemporaryStatPresentation(actor);
            RegisterMeleeAfterImage(actor, 0, actor.ActionName, Environment.TickCount, 10, 0);
            return true;
        }

        public bool TryApplyActiveEffectItem(RemoteUserActiveEffectItemPacket packet, int currentTime, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(packet.CharacterId, out RemoteUserActor actor))
            {
                message = $"Remote user {packet.CharacterId} does not exist.";
                return false;
            }

            if (!packet.ItemId.HasValue || packet.ItemId.Value <= 0)
            {
                ClearPacketOwnedEmotionState(actor);
                message = $"Remote user {packet.CharacterId} active effect item cleared.";
                return true;
            }

            if (!PacketOwnedAvatarEmotionResolver.TryResolveItemEmotion(
                    packet.ItemId.Value,
                    currentTime,
                    out PacketOwnedAvatarEmotionSelection selection,
                    out bool byItemOption,
                    out string error))
            {
                ClearPacketOwnedEmotionState(actor);
                message = error ?? $"Remote user active effect item {packet.ItemId.Value} has no supported simulator presentation.";
                return true;
            }

            ApplyPacketOwnedEmotionState(actor, packet.ItemId.Value, selection, byItemOption, currentTime);
            string sourceText = byItemOption ? "item-option emotion" : "random-emotion item";
            message = $"Remote user {packet.CharacterId} active effect item {packet.ItemId.Value} applied as {sourceText} '{selection.EmotionName}' ({selection.EmotionId}).";
            return true;
        }

        public bool TryApplyEnterFieldAvatarPresentation(
            RemoteUserEnterFieldPacket packet,
            int currentTime,
            out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(packet.CharacterId, out RemoteUserActor actor))
            {
                message = $"Remote user {packet.CharacterId} does not exist.";
                return false;
            }

            actor.CarryItemEffectId = packet.CarryItemEffect is > 0
                ? packet.CarryItemEffect
                : null;
            actor.CompletedSetItemId = Math.Max(0, packet.CompletedSetItemId);
            return TryApplyActiveEffectItem(
                new RemoteUserActiveEffectItemPacket(packet.CharacterId, packet.ActiveEffectItemId),
                currentTime,
                out message);
        }

        public bool TrySetItemEffect(int characterId, int? itemId, int? pairCharacterId, int currentTime, out string message)
        {
            return TrySetItemEffect(
                characterId,
                RemoteRelationshipOverlayType.Generic,
                itemId,
                pairCharacterId,
                currentTime,
                out message);
        }

        public bool TrySetItemEffect(
            int characterId,
            RemoteRelationshipOverlayType relationshipType,
            int? itemId,
            int? pairCharacterId,
            int currentTime,
            out string message)
        {
            return TrySetItemEffect(
                characterId,
                relationshipType,
                itemId,
                pairCharacterId,
                currentTime,
                out message,
                itemSerial: null,
                pairItemSerial: null);
        }

        private bool TrySetItemEffect(
            int characterId,
            RemoteRelationshipOverlayType relationshipType,
            int? itemId,
            int? pairCharacterId,
            int currentTime,
            out string message,
            long? itemSerial,
            long? pairItemSerial)
        {
            message = null;
            if (_loader == null)
            {
                message = "Character loader is not available.";
                return false;
            }

            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            if (!itemId.HasValue || itemId.Value <= 0)
            {
                actor.RelationshipOverlays.Remove(relationshipType);
                return true;
            }

            int resolvedItemId = itemId.Value;
            ItemEffectAnimationSet effect = _loader.LoadItemEffectAnimationSet(resolvedItemId);
            if (relationshipType == RemoteRelationshipOverlayType.NewYearCard
                && effect == null
                && resolvedItemId != NewYearCardDefaultItemId)
            {
                resolvedItemId = NewYearCardDefaultItemId;
                effect = _loader.LoadItemEffectAnimationSet(resolvedItemId);
            }

            if (effect == null && relationshipType != RemoteRelationshipOverlayType.NewYearCard)
            {
                message = $"Item effect {resolvedItemId} could not be loaded from Effect/ItemEff.img.";
                return false;
            }

            actor.RelationshipOverlays[relationshipType] = new RemoteRelationshipOverlayState
            {
                RelationshipType = relationshipType,
                ItemId = resolvedItemId,
                ItemSerial = itemSerial,
                PairItemSerial = pairItemSerial,
                PairCharacterId = pairCharacterId,
                Effect = NormalizeRelationshipOverlayEffect(
                    effect,
                    relationshipType,
                    shouldLoop: relationshipType != RemoteRelationshipOverlayType.Generic),
                StartTime = currentTime
            };
            return true;
        }

        public bool TryApplyAvatarModified(
            RemoteUserAvatarModifiedPacket packet,
            int currentTime,
            out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(packet.CharacterId, out RemoteUserActor actor))
            {
                message = $"Remote user {packet.CharacterId} does not exist.";
                return false;
            }

            if (packet.AvatarLook != null)
            {
                if (_loader == null)
                {
                    message = "Character loader is not available.";
                    return false;
                }

                CharacterBuild updatedBuild = _loader.LoadFromAvatarLook(packet.AvatarLook, actor.Build?.Clone());
                if (updatedBuild == null)
                {
                    message = $"AvatarLook refresh for remote user {packet.CharacterId} could not be applied.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(actor.Name))
                {
                    updatedBuild.Name = actor.Name.Trim();
                }

                actor.BeginMeleeAfterImageFade(currentTime);
                actor.Build = updatedBuild;
                actor.BaseActionName = NormalizeActionName(actor.BaseActionName, actor.Build.ActivePortableChair != null);
                if (actor.Build.ActivePortableChair != null)
                {
                    ApplyPortableChairMount(actor, actor.Build.ActivePortableChair);
                    SyncPortableChairRecordFromChairState(
                        actor.CharacterId,
                        actor.Build.ActivePortableChair.ItemId,
                        ResolvePortableChairPairPreference(actor.CharacterId));
                }
                else
                {
                    ClearPortableChairPairRecord(actor.CharacterId);
                    ClearPortableChairMountState(actor);
                }

                SyncTemporaryStatPresentation(actor);
                RegisterMeleeAfterImage(actor, 0, actor.ActionName, currentTime, 10, 0);
            }

            if (packet.Speed.HasValue && actor.Build != null)
            {
                actor.Build.Speed = packet.Speed.Value;
            }

            if (packet.CarryItemEffect.HasValue)
            {
                actor.CarryItemEffectId = packet.CarryItemEffect.Value > 0
                    ? packet.CarryItemEffect
                    : null;
            }

            actor.CompletedSetItemId = packet.CompletedSetItemId;

            if (!TryApplyAvatarModifiedRelationshipRecord(
                    actor,
                    RemoteRelationshipOverlayType.Couple,
                    packet.CoupleRecord,
                    currentTime,
                    out message))
            {
                return false;
            }

            if (!TryApplyAvatarModifiedRelationshipRecord(
                    actor,
                    RemoteRelationshipOverlayType.Friendship,
                    packet.FriendshipRecord,
                    currentTime,
                    out message))
            {
                return false;
            }

            if (!TryApplyAvatarModifiedRelationshipRecord(
                    actor,
                    RemoteRelationshipOverlayType.NewYearCard,
                    packet.NewYearCardRecord,
                    currentTime,
                    out message))
            {
                return false;
            }

            RemoteUserRelationshipRecord marriageRecord = packet.MarriageRecord.IsActive
                ? packet.MarriageRecord with
                {
                    PairCharacterId = ResolveMarriagePairCharacterId(packet.CharacterId, packet.MarriageRecord)
                }
                : packet.MarriageRecord;
            if (!TryApplyAvatarModifiedRelationshipRecord(
                    actor,
                    RemoteRelationshipOverlayType.Marriage,
                    marriageRecord,
                    currentTime,
                    out message))
            {
                return false;
            }

            message = $"Remote user {packet.CharacterId} avatar-modified state applied.";
            return true;
        }

        public bool TryApplyRelationshipRecordAdd(
            RemoteUserRelationshipRecordPacket packet,
            int currentTime,
            out string message)
        {
            message = null;
            if (!packet.RelationshipRecord.IsActive)
            {
                message = $"{packet.RelationshipType} relationship record is not active.";
                return false;
            }

            int? ownerCharacterId = packet.RelationshipRecord.CharacterId;
            if (!ownerCharacterId.HasValue || ownerCharacterId.Value <= 0)
            {
                message = $"{packet.RelationshipType} relationship record did not specify a valid owner character ID.";
                return false;
            }

            EnsureRelationshipRecordTablesInitialized();
            Dictionary<int, RemoteUserRelationshipRecord> recordTable = GetRelationshipRecordTable(packet.RelationshipType);
            Dictionary<RemoteRelationshipRecordDispatchKey, int> dispatchTable = GetRelationshipRecordDispatchOwnerTable(packet.RelationshipType);
            RemoteUserRelationshipRecord normalizedRecord = NormalizeRelationshipRecordAdd(packet, recordTable, dispatchTable);
            int? pairCharacterId = packet.RelationshipType == RemoteRelationshipOverlayType.Marriage
                ? ResolveMarriagePairCharacterId(ownerCharacterId.Value, normalizedRecord)
                : normalizedRecord.PairCharacterId;
            normalizedRecord = normalizedRecord with
            {
                PairCharacterId = pairCharacterId
            };
            RemoveRelationshipRecordDispatchKeysForOwner(packet.RelationshipType, ownerCharacterId.Value);
            recordTable[ownerCharacterId.Value] = normalizedRecord;
            RegisterRelationshipRecordDispatchKey(packet.RelationshipType, packet.DispatchKey, ownerCharacterId.Value);
            RefreshRelationshipOverlays(packet.RelationshipType, currentTime);

            message = _actorsById.ContainsKey(ownerCharacterId.Value)
                ? $"Remote user {ownerCharacterId.Value} {packet.RelationshipType} relationship record applied."
                : $"Stored remote {packet.RelationshipType} relationship record for inactive owner {ownerCharacterId.Value}.";
            return true;
        }

        private static RemoteUserRelationshipRecord NormalizeRelationshipRecordAdd(
            RemoteUserRelationshipRecordPacket packet,
            IReadOnlyDictionary<int, RemoteUserRelationshipRecord> recordTable,
            IReadOnlyDictionary<RemoteRelationshipRecordDispatchKey, int> dispatchTable)
        {
            RemoteUserRelationshipRecord normalizedRecord = packet.RelationshipRecord;
            if (packet.PayloadKind != RemoteRelationshipRecordAddPayloadKind.PairLookup
                || packet.RelationshipType is not (RemoteRelationshipOverlayType.Couple or RemoteRelationshipOverlayType.Friendship))
            {
                return normalizedRecord;
            }

            int ownerCharacterId = normalizedRecord.CharacterId ?? 0;
            if (ownerCharacterId <= 0
                || recordTable == null
                || !recordTable.TryGetValue(ownerCharacterId, out RemoteUserRelationshipRecord existingRecord)
                || !existingRecord.IsActive)
            {
                existingRecord = default;
            }

            long? dispatchSerial = packet.DispatchKey.Kind == RemoteRelationshipRecordDispatchKeyKind.LargeIntegerSerial
                ? packet.DispatchKey.Serial
                : null;
            long? pairLookupSerial = packet.PairLookupSerial;
            int? pairCharacterId = normalizedRecord.PairCharacterId;
            if ((!pairCharacterId.HasValue || pairCharacterId.Value <= 0)
                && pairLookupSerial.HasValue
                && dispatchTable != null
                && dispatchTable.TryGetValue(
                    new RemoteRelationshipRecordDispatchKey(
                        RemoteRelationshipRecordDispatchKeyKind.LargeIntegerSerial,
                        pairLookupSerial,
                        CharacterId: null),
                    out int matchedOwnerCharacterId)
                && matchedOwnerCharacterId > 0
                && matchedOwnerCharacterId != ownerCharacterId)
            {
                pairCharacterId = matchedOwnerCharacterId;
            }

            return normalizedRecord with
            {
                ItemId = normalizedRecord.ItemId > 0 ? normalizedRecord.ItemId : existingRecord.ItemId,
                ItemSerial = normalizedRecord.ItemSerial
                    ?? (dispatchSerial.HasValue ? dispatchSerial : existingRecord.ItemSerial),
                PairItemSerial = normalizedRecord.PairItemSerial
                    ?? (pairLookupSerial.HasValue ? pairLookupSerial : existingRecord.PairItemSerial),
                PairCharacterId = pairCharacterId ?? existingRecord.PairCharacterId
            };
        }

        public bool TryApplyRelationshipRecordRemove(
            RemoteUserRelationshipRecordRemovePacket packet,
            out string message)
        {
            message = null;
            EnsureRelationshipRecordTablesInitialized();
            Dictionary<int, RemoteUserRelationshipRecord> recordTable = GetRelationshipRecordTable(packet.RelationshipType);
            Dictionary<RemoteRelationshipRecordDispatchKey, int> dispatchTable = GetRelationshipRecordDispatchOwnerTable(packet.RelationshipType);
            List<int> removedOwnerCharacterIds = new();
            if (packet.DispatchKey.HasValue
                && dispatchTable.TryGetValue(packet.DispatchKey, out int dispatchOwnerCharacterId)
                && recordTable.ContainsKey(dispatchOwnerCharacterId))
            {
                RemoveRelationshipRecordOwner(packet.RelationshipType, dispatchOwnerCharacterId, recordTable);
                removedOwnerCharacterIds.Add(dispatchOwnerCharacterId);
            }

            foreach (KeyValuePair<int, RemoteUserRelationshipRecord> entry in recordTable.ToArray())
            {
                int ownerCharacterId = entry.Key;
                if (removedOwnerCharacterIds.Contains(ownerCharacterId))
                {
                    continue;
                }

                RemoteUserRelationshipRecord record = entry.Value;
                int? candidatePairCharacterId = packet.RelationshipType == RemoteRelationshipOverlayType.Marriage
                    ? ResolveMarriagePairCharacterId(ownerCharacterId, record)
                    : record.PairCharacterId;
                if (!DoesRelationshipRecordRemovalMatch(
                        packet,
                        ownerCharacterId,
                        record.ItemSerial,
                        record.PairItemSerial,
                        candidatePairCharacterId))
                {
                    continue;
                }

                RemoveRelationshipRecordOwner(packet.RelationshipType, ownerCharacterId, recordTable);
                removedOwnerCharacterIds.Add(ownerCharacterId);
            }

            int removedCount = removedOwnerCharacterIds.Count;
            if (removedCount == 0)
            {
                string discriminator = packet.ItemSerial.HasValue
                    ? $"serial {packet.ItemSerial.Value}"
                    : packet.CharacterId.HasValue
                        ? $"character {packet.CharacterId.Value}"
                        : "record";
                message = $"No remote {packet.RelationshipType} relationship overlay matched {discriminator}.";
                return false;
            }

                message = removedCount == 1
                ? $"Removed 1 {packet.RelationshipType} relationship overlay."
                : $"Removed {removedCount} {packet.RelationshipType} relationship overlays.";

            RefreshRelationshipOverlays(packet.RelationshipType, Environment.TickCount);
            return true;
        }

        public bool TryApplyTemporaryStatSnapshot(
            int characterId,
            RemoteUserTemporaryStatSnapshot temporaryStats,
            ushort delay,
            out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote user {characterId} does not exist.";
                return false;
            }

            actor.TemporaryStats = temporaryStats;
            actor.TemporaryStatDelay = delay;
            SyncTemporaryStatPresentation(actor);
            message = temporaryStats.HasPayload
                ? $"Remote user {characterId} temporary-stat snapshot applied."
                : $"Remote user {characterId} temporary-stat snapshot cleared.";
            return true;
        }

        public bool TryApplyTemporaryStatSet(
            RemoteUserTemporaryStatSetPacket packet,
            out string message)
        {
            return TryApplyTemporaryStatSnapshot(
                packet.CharacterId,
                packet.TemporaryStats,
                packet.Delay,
                out message);
        }

        public bool TryApplyTemporaryStatReset(
            RemoteUserTemporaryStatResetPacket packet,
            out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(packet.CharacterId, out RemoteUserActor actor))
            {
                message = $"Remote user {packet.CharacterId} does not exist.";
                return false;
            }

            int[] currentMaskWords = actor.TemporaryStats.MaskWords ?? Array.Empty<int>();
            int[] resetMaskWords = packet.MaskWords ?? Array.Empty<int>();
            int maskWordCount = Math.Max(currentMaskWords.Length, resetMaskWords.Length);
            if (maskWordCount == 0)
            {
                actor.TemporaryStats = default;
                actor.TemporaryStatDelay = 0;
                SyncTemporaryStatPresentation(actor);
                message = $"Remote user {packet.CharacterId} temporary-stat mask cleared.";
                return true;
            }

            int[] remainingMaskWords = new int[maskWordCount];
            for (int i = 0; i < maskWordCount; i++)
            {
                int currentWord = i < currentMaskWords.Length ? currentMaskWords[i] : 0;
                int resetWord = i < resetMaskWords.Length ? resetMaskWords[i] : 0;
                int remainingWord = currentWord & ~resetWord;
                remainingMaskWords[i] = remainingWord;
            }

            actor.TemporaryStats = RemoteUserPacketCodec.ApplyResetMask(actor.TemporaryStats, remainingMaskWords);
            actor.TemporaryStatDelay = 0;
            SyncTemporaryStatPresentation(actor);
            message = actor.TemporaryStats.HasActiveMaskBits
                ? $"Remote user {packet.CharacterId} temporary-stat mask updated."
                : $"Remote user {packet.CharacterId} temporary-stat mask cleared.";
            return true;
        }

        public bool TrySetPreparedSkill(
            int characterId,
            int skillId,
            string skillName,
            int durationMs,
            string skinKey,
            bool isKeydownSkill,
            bool isHolding,
            int gaugeDurationMs,
            int maxHoldDurationMs,
            PreparedSkillHudTextVariant textVariant,
            bool showText,
            int currentTime,
            out string message,
            int prepareDurationMs = 0,
            bool autoEnterHold = false)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            actor.PreparedSkill = new RemotePreparedSkillState
            {
                SkillId = skillId,
                SkillName = PreparedSkillHudRules.ResolveDisplayName(skillId, skillName),
                SkinKey = string.IsNullOrWhiteSpace(skinKey) ? "KeyDownBar" : skinKey.Trim(),
                DurationMs = Math.Max(0, durationMs),
                PrepareDurationMs = Math.Max(0, prepareDurationMs),
                GaugeDurationMs = gaugeDurationMs > 0 ? gaugeDurationMs : Math.Max(0, durationMs),
                StartTime = currentTime,
                IsKeydownSkill = isKeydownSkill,
                IsHolding = isHolding,
                AutoEnterHold = autoEnterHold,
                MaxHoldDurationMs = Math.Max(0, maxHoldDurationMs),
                TextVariant = textVariant,
                ShowText = showText
            };

            if (RemotePreparedSkillUseEffectSkillIds.Contains(skillId))
            {
                string branchName = isHolding ? "keydown" : "prepare";
                SkillUseRegistered?.Invoke(new RemoteSkillUsePresentation(
                    actor.CharacterId,
                    skillId,
                    null,
                    actor.FacingRight,
                    currentTime,
                    new[] { branchName }));
            }

            return true;
        }

        public bool TryApplyMovingShootAttackPrepare(
            RemoteUserMovingShootAttackPreparePacket packet,
            int currentTime,
            out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(packet.CharacterId, out RemoteUserActor actor))
            {
                message = $"Remote character {packet.CharacterId} does not exist.";
                return false;
            }

            actor.FacingRight = packet.FacingRight;
            actor.MovingShootPreparedSkillId = packet.SkillId;

            if (packet.SkillId != RemoteMovingShootNoPrepareAnimationSkillId)
            {
                actor.PreparedSkill = null;
            }

            if (!string.IsNullOrWhiteSpace(packet.ActionName))
            {
                actor.BeginMeleeAfterImageFade(currentTime);
                SetActorAction(
                    actor,
                    packet.ActionName,
                    actor.Build?.ActivePortableChair != null,
                    currentTime,
                    forceReplay: true,
                    rawActionCode: packet.ActionCode);
            }

            actor.MovementDrivenActionSelection = false;
            RegisterMeleeAfterImage(
                actor,
                packet.SkillId,
                actor.ActionName,
                currentTime,
                masteryPercent: 10,
                chargeElement: 0,
                rawActionCode: packet.ActionCode);

            if (packet.SkillId > 0 && packet.SkillId != RemoteMovingShootNoPrepareAnimationSkillId)
            {
                SkillUseRegistered?.Invoke(new RemoteSkillUsePresentation(
                    actor.CharacterId,
                    packet.SkillId,
                    packet.ActionSpeed,
                    actor.FacingRight,
                    currentTime));
            }

            message = $"Remote user {packet.CharacterId} moving-shoot prepare applied for skill {packet.SkillId}.";
            return true;
        }

        public bool TryApplyEmotion(RemoteUserEmotionPacket packet, int currentTime, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(packet.CharacterId, out RemoteUserActor actor))
            {
                message = $"Remote user {packet.CharacterId} is not active.";
                return false;
            }

            if (!PacketOwnedAvatarEmotionResolver.TryResolveEmotionName(packet.EmotionId, out string emotionName))
            {
                message = $"Remote user {packet.CharacterId} emotion id {packet.EmotionId} is not supported by the shared avatar-emotion owner.";
                return false;
            }

            ApplyRemoteEmotionState(
                actor,
                itemId: 0,
                packet.EmotionId,
                emotionName,
                packet.ByItemOption,
                currentTime,
                packet.DurationMs,
                loadEffectAnimation: false);
            message = $"Remote user {packet.CharacterId} emotion '{emotionName}' ({packet.EmotionId}) applied for {Math.Max(0, packet.DurationMs)} ms.";
            return true;
        }

        public bool TryApplyHit(RemoteUserHitPacket packet, int currentTime, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(packet.CharacterId, out RemoteUserActor actor))
            {
                message = $"Remote user {packet.CharacterId} is not active.";
                return false;
            }

            actor.LastHit = new RemoteHitState
            {
                AttackIndex = packet.AttackIndex,
                Damage = packet.Damage,
                MobTemplateId = packet.MobTemplateId,
                MobHitFacingLeft = packet.MobHitFacingLeft,
                HasMobHit = packet.HasMobHit,
                PowerGuard = packet.PowerGuard,
                MobId = packet.MobId,
                MobHitAction = packet.MobHitAction,
                MobHitX = packet.MobHitX,
                MobHitY = packet.MobHitY,
                IncDecType = packet.IncDecType,
                HitFlags = packet.HitFlags,
                HpDelta = packet.HpDelta,
                SkillId = packet.SkillId,
                PacketTime = currentTime
            };

            if (packet.HpDelta > 0)
            {
                if (actor.PacketOwnedEmotion?.ByItemOption != true
                    && PacketOwnedAvatarEmotionResolver.TryResolveEmotionName(1, out string hitEmotionName))
                {
                    ApplyRemoteEmotionState(
                        actor,
                        itemId: 0,
                        emotionId: 1,
                        emotionName: hitEmotionName,
                        byItemOption: false,
                        currentTime,
                        durationMs: 1500,
                        loadEffectAnimation: false);
                }

                HitFeedbackRegistered?.Invoke(new RemoteHitFeedbackPresentation(
                    packet.CharacterId,
                    ResolveStandardWorldAnchor(actor, currentTime, verticalOffset: 24f),
                    -packet.HpDelta,
                    currentTime));
                message = $"Remote user {packet.CharacterId} hit packet applied for {packet.HpDelta} damage.";
                return true;
            }

            if (packet.HpDelta == 0)
            {
                HitFeedbackRegistered?.Invoke(new RemoteHitFeedbackPresentation(
                    packet.CharacterId,
                    ResolveStandardWorldAnchor(actor, currentTime, verticalOffset: 24f),
                    0,
                    currentTime));
            }

            if (packet.SkillId is > 0)
            {
                SkillUseRegistered?.Invoke(new RemoteSkillUsePresentation(
                    packet.CharacterId,
                    packet.SkillId.Value,
                    null,
                    actor.FacingRight,
                    currentTime));
            }

            message = packet.SkillId is > 0
                ? $"Remote user {packet.CharacterId} hit packet stored and registered skill effect {packet.SkillId.Value}."
                : $"Remote user {packet.CharacterId} hit packet stored.";
            return true;
        }

        public bool TryApplyEffect(RemoteUserEffectPacket packet, int currentTime, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(packet.CharacterId, out RemoteUserActor actor))
            {
                message = $"Remote user {packet.CharacterId} is not active.";
                return false;
            }

            switch (packet.KnownSubtype)
            {
                case RemoteUserEffectSubtype.IncDecHp:
                    int delta = packet.Int32Value.GetValueOrDefault();
                    HitFeedbackRegistered?.Invoke(new RemoteHitFeedbackPresentation(
                        packet.CharacterId,
                        ResolveStandardWorldAnchor(actor, currentTime, verticalOffset: 24f),
                        delta,
                        currentTime));
                    message = $"Remote user {packet.CharacterId} effect subtype {packet.EffectType} registered inc/dec HP delta {delta}.";
                    return true;

                case RemoteUserEffectSubtype.QuestDeliveryStart:
                    int itemId = packet.Int32Value.GetValueOrDefault();
                    if (itemId <= 0)
                    {
                        message = $"Remote user {packet.CharacterId} quest-delivery item ID {itemId} is invalid.";
                        return false;
                    }

                    actor.PacketOwnedQuestDeliveryEffectItemId = itemId;
                    message = $"Remote user {packet.CharacterId} effect subtype {packet.EffectType} started quest-delivery presentation with item {itemId}.";
                    return true;

                case RemoteUserEffectSubtype.QuestDeliveryEnd:
                    actor.PacketOwnedQuestDeliveryEffectItemId = null;
                    message = $"Remote user {packet.CharacterId} effect subtype {packet.EffectType} cleared quest-delivery presentation.";
                    return true;

                default:
                    message = $"Remote user effect subtype {packet.EffectType} is not supported by the shared remote-user owner.";
                    return false;
            }
        }

        public bool TryApplyUpgradeTombEffect(RemoteUserUpgradeTombPacket packet, int currentTime, out string message)
        {
            message = null;
            if (!_actorsById.ContainsKey(packet.CharacterId))
            {
                message = $"Remote user {packet.CharacterId} is not active.";
                return false;
            }

            UpgradeTombEffectRegistered?.Invoke(new RemoteUpgradeTombPresentation(
                packet.CharacterId,
                packet.ItemId,
                new Vector2(packet.PositionX, packet.PositionY),
                currentTime));
            message = $"Remote user {packet.CharacterId} upgrade tomb effect registered at ({packet.PositionX}, {packet.PositionY}).";
            return true;
        }

        public bool TryApplyReceiveHp(RemoteUserReceiveHpPacket packet, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(packet.CharacterId, out RemoteUserActor actor))
            {
                message = $"Remote user {packet.CharacterId} is not active.";
                return false;
            }

            if (packet.MaxHp <= 0)
            {
                message = $"Remote user {packet.CharacterId} receive-HP max HP {packet.MaxHp} is invalid.";
                return false;
            }

            int currentHp = Math.Clamp(packet.CurrentHp, 0, packet.MaxHp);
            actor.PartyCurrentHp = currentHp;
            actor.PartyMaxHp = packet.MaxHp;
            actor.PartyHpPercent = Math.Clamp((currentHp * 100) / packet.MaxHp, 0, 100);
            actor.PartyHpGaugePos = Math.Clamp((RemoteReceiveHpGaugeWidth * currentHp) / packet.MaxHp, 0, RemoteReceiveHpGaugeWidth);
            message = $"Remote user {packet.CharacterId} receive-HP gauge updated to {currentHp}/{packet.MaxHp}.";
            return true;
        }

        public bool TryApplyThrowGrenade(RemoteUserThrowGrenadePacket packet, int currentTime, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(packet.CharacterId, out RemoteUserActor actor))
            {
                message = $"Remote user {packet.CharacterId} is not active.";
                return false;
            }

            actor.LastThrowGrenadeSkillId = packet.SkillId;
            actor.LastThrowGrenadeId = packet.GrenadeId;
            actor.LastThrowGrenadeTarget = new Point(packet.X, packet.Y);
            actor.LastThrowGrenadeKeyDownTime = packet.KeyDownTime;
            actor.LastThrowGrenadePacketTime = currentTime;
            SkillUseRegistered?.Invoke(new RemoteSkillUsePresentation(
                actor.CharacterId,
                packet.SkillId,
                null,
                actor.FacingRight,
                currentTime,
                new[] { "special" },
                new Vector2(packet.X, packet.Y),
                FollowOwnerPosition: false,
                FollowOwnerFacing: false,
                DelayRateOverride: 1000));
            message = $"Remote user {packet.CharacterId} throw-grenade packet stored for skill {packet.SkillId} at ({packet.X}, {packet.Y}).";
            return true;
        }

        public bool TryClearPreparedSkill(int characterId, int currentTime, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            if (actor.PreparedSkill != null
                && RemotePreparedSkillUseEffectSkillIds.Contains(actor.PreparedSkill.SkillId))
            {
                SkillUseRegistered?.Invoke(new RemoteSkillUsePresentation(
                    actor.CharacterId,
                    actor.PreparedSkill.SkillId,
                    null,
                    actor.FacingRight,
                    currentTime,
                    new[] { "keydownend" }));
            }

            actor.PreparedSkill = null;
            actor.MovingShootPreparedSkillId = 0;
            return true;
        }

        public bool TrySetWorldVisibility(int characterId, bool isVisibleInWorld, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            actor.IsVisibleInWorld = isVisibleInWorld;
            return true;
        }

        public bool TryRemove(int characterId, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            ClearActorFollowLinks(actor);
            ClearPortableChairPairRecord(characterId);
            NotifyActorRemoved(actor.CharacterId, actor.Name);
            _actorsById.Remove(characterId);
            _actorIdsByName.Remove(actor.Name);
            _pendingTransientSkillUseAvatarEffectsByCharacterId.Remove(characterId);
            return true;
        }

        private void NotifyActorRemoved(int characterId, string name)
        {
            ActorRemovedCallback?.Invoke(characterId, name);
        }

        public void Update(int currentTime, PlayerCharacter localPlayer = null)
        {
            foreach (RemoteUserActor actor in _actorsById.Values)
            {
                if (actor.FollowDriverId <= 0 && actor.MovementSnapshot != null)
                {
                    ApplyMovementSnapshot(actor, currentTime);
                }

                ApplyFollowDriverState(actor, localPlayer);

                if (actor.PreparedSkill != null
                    && (actor.PreparedSkill.DurationMs > 0 || actor.PreparedSkill.AutoEnterHold))
                {
                    if (!TryResolvePreparedSkillPhase(actor.PreparedSkill, currentTime, out _, out _, out _, out _, out _))
                    {
                        actor.PreparedSkill = null;
                    }
                }

                foreach (KeyValuePair<RemoteRelationshipOverlayType, RemoteRelationshipOverlayState> overlayEntry in actor.RelationshipOverlays.ToArray())
                {
                    RemoteRelationshipOverlayState overlay = overlayEntry.Value;
                    if (overlay?.Effect == null
                        || overlay.RelationshipType != RemoteRelationshipOverlayType.Generic
                        || overlay.Effect.TotalDurationMs <= 0
                        || currentTime - overlay.StartTime < overlay.Effect.TotalDurationMs)
                    {
                        continue;
                    }

                    actor.RelationshipOverlays.Remove(overlayEntry.Key);
                }

                UpdatePacketOwnedEmotionState(actor, currentTime);
                UpdateTransientSkillUseAvatarEffects(actor, currentTime);
                actor.UpdateMeleeAfterImage(currentTime);
            }
        }

        public void SyncPortableChairPairState(PlayerCharacter player)
        {
            if (player == null)
            {
                return;
            }

            player.ClearPortableChairExternalOwnerPair();
            PortableChair chair = player.Build?.ActivePortableChair;
            bool requestsExternalPair = chair?.IsCoupleChair == true;
            player.SetPortableChairPairRequestActive(requestsExternalPair);
            int localCharacterId = player.Build?.Id ?? 0;
            Dictionary<int, int> pairMap = BuildPortableChairPairMap(player, preferVisibleOnly: true);
            if (requestsExternalPair)
            {
                if (pairMap.TryGetValue(localCharacterId, out int pairCharacterId)
                    && _actorsById.TryGetValue(pairCharacterId, out RemoteUserActor pairActor))
                {
                    player.SetPortableChairExternalPair(pairActor.Position, pairActor.FacingRight);
                }
                else
                {
                    player.ClearPortableChairExternalPair();
                }
            }

            if (TryResolvePortableChairOwnerForLocalPlayer(player, pairMap, out RemoteUserActor ownerActor))
            {
                player.SetPortableChairExternalOwnerPair(
                    ownerActor.Build.ActivePortableChair,
                    ownerActor.Position,
                    ownerActor.FacingRight);
            }
        }

        public int? LocalPortableChairPreferredPairCharacterId => _localPortableChairPreferredPairCharacterId;

        public void ClearLocalPortableChairPairPreference()
        {
            _localPortableChairPreferredPairCharacterId = null;
        }

        public bool TrySetLocalPortableChairPairPreference(PlayerCharacter localPlayer, int? pairCharacterId, out string message)
        {
            message = null;
            if (pairCharacterId is null or <= 0)
            {
                _localPortableChairPreferredPairCharacterId = null;
                message = "Cleared the preferred couple-chair partner.";
                return true;
            }

            if (localPlayer?.Build == null)
            {
                message = "Local player runtime is not available.";
                return false;
            }

            int localCharacterId = localPlayer.Build.Id;
            if (pairCharacterId.Value == localCharacterId)
            {
                message = "The local player cannot pair with itself.";
                return false;
            }

            if (!_actorsById.TryGetValue(pairCharacterId.Value, out RemoteUserActor actor))
            {
                message = $"Remote user {pairCharacterId.Value} is not active.";
                return false;
            }

            _localPortableChairPreferredPairCharacterId = pairCharacterId.Value;

            PortableChair localChair = localPlayer.Build.ActivePortableChair;
            PortableChair remoteChair = actor.Build?.ActivePortableChair;
            if (localChair?.IsCoupleChair == true
                && remoteChair?.IsCoupleChair == true
                && localChair.ItemId == remoteChair.ItemId)
            {
                message = $"Preferred couple-chair partner set to {actor.Name} ({actor.CharacterId}).";
            }
            else
            {
                message = $"Preferred couple-chair partner set to {actor.Name} ({actor.CharacterId}); pairing will apply once both users share a valid couple-chair state.";
            }

            return true;
        }

        public IReadOnlyList<StatusBarPreparedSkillRenderData> BuildPreparedSkillWorldOverlays(int currentTime)
        {
            _preparedSkillWorldOverlayCount = 0;
            foreach (RemoteUserActor actor in _actorsById.Values)
            {
                RemotePreparedSkillState prepared = actor.PreparedSkill;
                if (!actor.IsVisibleInWorld
                    || prepared == null
                    || PreparedSkillHudRules.IsDragonOverlaySkill(prepared.SkillId))
                {
                    continue;
                }

                StatusBarPreparedSkillRenderData overlay = BuildPreparedSkillWorldOverlay(actor, currentTime, _preparedSkillWorldOverlayCount);
                if (overlay == null || PreparedSkillHudRules.IsDragonOverlaySkill(overlay.SkillId))
                {
                    continue;
                }

                _preparedSkillWorldOverlayCount++;
            }

            return _preparedSkillWorldOverlayBuffer;
        }

        private StatusBarPreparedSkillRenderData BuildPreparedSkillWorldOverlay(RemoteUserActor actor, int currentTime, int bufferIndex)
        {
            RemotePreparedSkillState prepared = actor?.PreparedSkill;
            if (prepared == null)
            {
                return null;
            }

            if (!TryResolvePreparedSkillPhase(
                    prepared,
                    currentTime,
                    out int remainingMs,
                    out int duration,
                    out float progress,
                    out bool isHolding,
                    out int holdElapsedMs))
            {
                return null;
            }

            StatusBarPreparedSkillRenderData overlay = GetOrCreatePreparedSkillWorldOverlay(bufferIndex);
            overlay.SkillId = prepared.SkillId;
            overlay.SkillName = prepared.SkillName;
            overlay.SkinKey = prepared.SkinKey;
            overlay.Surface = PreparedSkillHudSurface.World;
            overlay.RemainingMs = remainingMs;
            overlay.DurationMs = duration;
            overlay.GaugeDurationMs = prepared.GaugeDurationMs > 0 ? prepared.GaugeDurationMs : duration;
            overlay.Progress = progress;
            overlay.IsKeydownSkill = prepared.IsKeydownSkill;
            overlay.IsPreparingPhase = prepared.AutoEnterHold && !isHolding && prepared.PrepareDurationMs > 0;
            overlay.IsHolding = isHolding;
            overlay.PrepareRemainingMs = overlay.IsPreparingPhase ? remainingMs : 0;
            overlay.HoldElapsedMs = holdElapsedMs;
            overlay.MaxHoldDurationMs = prepared.MaxHoldDurationMs;
            overlay.TextVariant = prepared.TextVariant;
            overlay.ShowText = prepared.ShowText && !PreparedSkillHudRules.IsDragonOverlaySkill(prepared.SkillId);
            if (!TryResolvePreparedSkillWorldAnchor(actor, prepared, currentTime, isHolding, out Vector2 worldAnchor))
            {
                return null;
            }

            overlay.WorldAnchor = worldAnchor;
            return overlay;
        }

        internal static bool TryResolvePreparedSkillPhase(
            RemotePreparedSkillState prepared,
            int currentTime,
            out int remainingMs,
            out int durationMs,
            out float progress,
            out bool isHolding,
            out int holdElapsedMs)
        {
            remainingMs = 0;
            durationMs = 0;
            progress = 0f;
            isHolding = false;
            holdElapsedMs = 0;

            if (prepared == null)
            {
                return false;
            }

            int elapsed = Math.Max(0, currentTime - prepared.StartTime);
            if (prepared.AutoEnterHold)
            {
                if (prepared.PrepareDurationMs > 0 && elapsed < prepared.PrepareDurationMs)
                {
                    durationMs = prepared.PrepareDurationMs;
                    remainingMs = Math.Max(0, prepared.PrepareDurationMs - elapsed);
                    progress = durationMs > 0
                        ? MathHelper.Clamp(elapsed / (float)durationMs, 0f, 1f)
                        : 0f;
                    return true;
                }

                int holdDurationMs = Math.Max(0, prepared.MaxHoldDurationMs);
                holdElapsedMs = Math.Max(0, elapsed - prepared.PrepareDurationMs);
                if (holdDurationMs > 0 && holdElapsedMs >= holdDurationMs)
                {
                    return false;
                }

                durationMs = holdDurationMs;
                remainingMs = holdDurationMs > 0
                    ? Math.Max(0, holdDurationMs - holdElapsedMs)
                    : 0;
                progress = holdDurationMs > 0
                    ? MathHelper.Clamp(holdElapsedMs / (float)holdDurationMs, 0f, 1f)
                    : 1f;
                isHolding = true;
                return true;
            }

            durationMs = Math.Max(0, prepared.DurationMs);
            if (durationMs > 0 && elapsed >= durationMs)
            {
                return false;
            }

            remainingMs = durationMs > 0
                ? Math.Max(0, durationMs - elapsed)
                : 0;
            progress = durationMs > 0
                ? MathHelper.Clamp(elapsed / (float)durationMs, 0f, 1f)
                : 0f;
            isHolding = prepared.IsHolding;
            holdElapsedMs = isHolding ? elapsed : 0;
            return true;
        }

        private static bool TryResolvePreparedSkillWorldAnchor(
            RemoteUserActor actor,
            RemotePreparedSkillState prepared,
            int currentTime,
            bool isHolding,
            out Vector2 anchor)
        {
            anchor = Vector2.Zero;
            if (actor == null)
            {
                return false;
            }

            if (prepared != null
                && PreparedSkillHudRules.IsDragonOverlaySkill(prepared.SkillId))
            {
                if (TryResolveRemoteDragonKeyDownBarAnchor(actor, prepared, currentTime, isHolding, out Vector2 dragonAnchor))
                {
                    anchor = dragonAnchor;
                    return true;
                }

                return false;
            }

            anchor = ResolveStandardPreparedSkillWorldAnchor(actor, currentTime);
            return true;
        }

        private static Vector2 ResolveStandardPreparedSkillWorldAnchor(RemoteUserActor actor, int currentTime)
        {
            return ResolveStandardWorldAnchor(actor, currentTime, 18f);
        }

        private static Vector2 ResolveStandardWorldAnchor(RemoteUserActor actor, int currentTime, float verticalOffset)
        {
            AssembledFrame frame = actor.Assembler?.GetFrameAtTime(actor.ActionName, currentTime)
                ?? actor.Assembler?.GetFrameAtTime(CharacterPart.GetActionString(CharacterAction.Stand1), currentTime);
            if (frame != null)
            {
                float topY = actor.Position.Y - frame.FeetOffset + frame.Bounds.Top;
                return new Vector2(actor.Position.X, topY - verticalOffset);
            }

            return new Vector2(actor.Position.X, actor.Position.Y - (verticalOffset + 62f));
        }

        private static bool TryResolveRemoteDragonKeyDownBarAnchor(
            RemoteUserActor actor,
            RemotePreparedSkillState prepared,
            int currentTime,
            bool isHolding,
            out Vector2 anchor)
        {
            anchor = Vector2.Zero;
            if (actor?.Build == null
                || !TryResolveRemoteDragonHudMetadata(actor.Build.Job, out RemoteDragonHudMetadata metadata))
            {
                return false;
            }

            AssembledFrame ownerFrame = actor.Assembler?.GetFrameAtTime(actor.ActionName, currentTime)
                ?? actor.Assembler?.GetFrameAtTime(CharacterPart.GetActionString(CharacterAction.Stand1), currentTime);
            float ownerBodyOriginY = ownerFrame != null
                ? actor.Position.Y - ownerFrame.FeetOffset
                : actor.Position.Y;

            string dragonActionName = ResolveRemoteDragonActionName(
                prepared,
                isHolding,
                actor.ActionName,
                actor.BaseActionRawCode,
                metadata);
            int dragonActionElapsedMs = ResolveRemoteDragonActionElapsedMs(
                prepared,
                currentTime,
                dragonActionName,
                isHolding,
                actor.BaseActionStartTime);
            int dragonFrameHeight = metadata.ResolveFrameHeight(dragonActionName, dragonActionElapsedMs);
            Vector2 dragonAnchor = ResolveRemoteDragonAnchor(
                actor,
                ownerFrame,
                actor.ActionName,
                actor.BaseActionRawCode,
                ownerBodyOriginY,
                dragonActionName,
                dragonActionElapsedMs,
                metadata);
            anchor = new Vector2(
                dragonAnchor.X - RemoteDragonKeyDownBarHalfWidth,
                dragonAnchor.Y - dragonFrameHeight - RemoteDragonKeyDownBarVerticalGap);
            return true;
        }

        private static Vector2 ResolveRemoteDragonAnchor(
            RemoteUserActor actor,
            AssembledFrame ownerFrame,
            string ownerActionName,
            int? ownerRawActionCode,
            float ownerBodyOriginY,
            string dragonActionName,
            int dragonActionElapsedMs,
            RemoteDragonHudMetadata metadata)
        {
            float side = actor.FacingRight ? -1f : 1f;
            int originX = metadata.ResolveOriginX(dragonActionName, dragonActionElapsedMs);

            if (UsesRemoteDragonLadderAnchor(ownerActionName, ownerRawActionCode))
            {
                float ladderHorizontalOffset = Math.Max(RemoteDragonLadderSideOffset, originX * 0.45f);
                return new Vector2(
                    actor.Position.X + (side * ladderHorizontalOffset),
                    ownerBodyOriginY + RemoteDragonLadderVerticalOffset);
            }

            float groundHorizontalOffset = Math.Max(RemoteDragonGroundSideOffset, originX * 0.55f);
            if (ownerFrame != null && !ownerFrame.Bounds.IsEmpty)
            {
                Rectangle bounds = ownerFrame.Bounds;
                float anchorX = actor.FacingRight
                    ? actor.Position.X + bounds.Left + side * groundHorizontalOffset
                    : actor.Position.X + bounds.Right + side * groundHorizontalOffset;
                return new Vector2(anchorX, actor.Position.Y + RemoteDragonGroundVerticalOffset);
            }

            return new Vector2(
                actor.Position.X + (side * groundHorizontalOffset),
                ownerBodyOriginY + RemoteDragonGroundVerticalOffset);
        }

        private static bool TryResolveRemoteDragonHudMetadata(int jobId, out RemoteDragonHudMetadata metadata)
        {
            metadata = default;
            int dragonJob = jobId switch
            {
                >= 2200 and <= 2218 => jobId,
                _ => 0
            };
            if (dragonJob == 0)
            {
                return false;
            }

            if (RemoteDragonHudMetadataCache.TryGetValue(dragonJob, out metadata))
            {
                return true;
            }

            WzImage image = FindRemoteDragonHudImage(dragonJob);
            if (image == null)
            {
                return false;
            }

            var actionTimelines = new Dictionary<string, RemoteDragonHudAnimationTimeline>(StringComparer.OrdinalIgnoreCase);
            int standOriginX = 79;
            int moveOriginX = standOriginX;

            foreach (string actionName in DragonActionLoader.EnumerateRenderableImageActionNames(image))
            {
                WzSubProperty actionNode = DragonActionLoader.FindActionNode(image, actionName);
                if (actionNode == null
                    || !TryReadRemoteDragonFrameMetrics(actionNode, out int originX, out RemoteDragonHudAnimationTimeline timeline))
                {
                    continue;
                }

                actionTimelines[actionName] = timeline;
                if (string.Equals(actionName, "stand", StringComparison.OrdinalIgnoreCase))
                {
                    standOriginX = originX;
                }
                else if (string.Equals(actionName, "move", StringComparison.OrdinalIgnoreCase))
                {
                    moveOriginX = originX;
                }
            }

            if (actionTimelines.Count == 0)
            {
                return false;
            }

            metadata = new RemoteDragonHudMetadata(standOriginX, moveOriginX, actionTimelines);
            RemoteDragonHudMetadataCache[dragonJob] = metadata;
            return true;
        }

        internal static bool TryResolveRemoteDragonHudMetadataForTesting(int jobId, out RemoteDragonHudMetadata metadata)
        {
            return TryResolveRemoteDragonHudMetadata(jobId, out metadata);
        }

        private static WzImage FindRemoteDragonHudImage(int dragonJob)
        {
            if (dragonJob <= 0)
            {
                return null;
            }

            string resolvedImagePath = DragonActionLoader.ResolveDragonImagePath(dragonJob);
            if (!string.IsNullOrWhiteSpace(resolvedImagePath)
                && resolvedImagePath.StartsWith("Skill/", StringComparison.OrdinalIgnoreCase))
            {
                string relativeImagePath = resolvedImagePath.Substring("Skill/".Length);
                WzImage resolvedImage = global::HaCreator.Program.FindImage("Skill", relativeImagePath);
                if (resolvedImage != null)
                {
                    return resolvedImage;
                }
            }

            return global::HaCreator.Program.FindImage("Skill", $"Dragon/{dragonJob}.img")
                ?? global::HaCreator.Program.FindImage("Skill", $"{dragonJob}.img");
        }

        private static bool TryReadRemoteDragonFrameMetrics(
            WzSubProperty actionNode,
            out int originX,
            out RemoteDragonHudAnimationTimeline timeline)
        {
            originX = 0;
            timeline = default;

            List<RemoteDragonHudFrameMetrics> frames = new();
            foreach (WzCanvasProperty frame in actionNode.WzProperties
                         .OfType<WzCanvasProperty>()
                         .OrderBy(static canvas => ParseRemoteDragonFrameIndex(canvas.Name)))
            {
                WzCanvasProperty metadataCanvas = ResolveRemoteDragonMetadataCanvas(frame);
                if (metadataCanvas?["origin"] is not WzVectorProperty origin
                    || metadataCanvas["lt"] is not WzVectorProperty lt
                    || metadataCanvas["rb"] is not WzVectorProperty rb)
                {
                    continue;
                }

                int height = Math.Max(1, rb.Y.Value - lt.Y.Value);
                int delayMs = Math.Max(1, GetWzIntValue(metadataCanvas["delay"]) ?? 100);
                frames.Add(new RemoteDragonHudFrameMetrics(origin.X.Value, height, delayMs));
                if (frames.Count == 1)
                {
                    originX = origin.X.Value;
                }
            }

            if (frames.Count == 0)
            {
                return false;
            }

            timeline = new RemoteDragonHudAnimationTimeline(
                IsRemoteDragonActionLooping(actionNode.Name),
                frames);
            return true;
        }

        private static WzCanvasProperty ResolveRemoteDragonMetadataCanvas(WzCanvasProperty canvas)
        {
            if (canvas == null)
            {
                return null;
            }

            try
            {
                return canvas.GetLinkedWzImageProperty() as WzCanvasProperty ?? canvas;
            }
            catch
            {
                return canvas;
            }
        }

        private static bool IsRemoteDragonActionLooping(string actionName)
        {
            return string.Equals(actionName, "stand", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "move", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(actionName)
                    && actionName.EndsWith("_prepare", StringComparison.OrdinalIgnoreCase));
        }

        private static int? GetWzIntValue(WzImageProperty property)
        {
            return property switch
            {
                WzIntProperty intProperty => intProperty.Value,
                WzShortProperty shortProperty => shortProperty.Value,
                WzLongProperty longProperty => (int)longProperty.Value,
                _ => null
            };
        }

        private static int ParseRemoteDragonFrameIndex(string value)
        {
            return int.TryParse(value, out int parsed) ? parsed : int.MaxValue;
        }

        internal static string ResolveRemoteDragonActionName(
            RemotePreparedSkillState prepared,
            bool isHolding,
            string ownerActionName,
            int? ownerRawActionCode,
            RemoteDragonHudMetadata metadata)
        {
            if (TryResolveRemoteExplicitDragonActionName(ownerActionName, ownerRawActionCode, metadata, out string explicitActionName))
            {
                return explicitActionName;
            }

            if (isHolding)
            {
                if (ShouldUseRemoteDragonMoveAction(ownerActionName, ownerRawActionCode)
                    && metadata.HasAction("move"))
                {
                    return "move";
                }

                return "stand";
            }

            return prepared?.SkillId switch
            {
                22121000 => "icebreathe_prepare",
                22151001 => "breathe_prepare",
                _ => "stand"
            };
        }

        internal static bool TryResolveRemoteExplicitDragonActionName(
            string ownerActionName,
            int? ownerRawActionCode,
            RemoteDragonHudMetadata metadata,
            out string actionName)
        {
            actionName = null;
            if (metadata.ActionTimelines == null || metadata.ActionTimelines.Count == 0)
            {
                return false;
            }

            if (ownerRawActionCode.HasValue
                && DragonActionLoader.TryGetClientActionNameFromRawActionCode(ownerRawActionCode.Value, out string rawActionName)
                && IsExplicitRemoteDragonAction(rawActionName)
                && metadata.HasAction(rawActionName))
            {
                actionName = rawActionName;
                return true;
            }

            foreach (string candidate in EnumerateRemoteDragonActionCandidates(ownerActionName))
            {
                if (IsExplicitRemoteDragonAction(candidate) && metadata.HasAction(candidate))
                {
                    actionName = candidate;
                    return true;
                }
            }

            return false;
        }

        internal static bool ShouldUseRemoteDragonMoveAction(string ownerActionName, int? ownerRawActionCode = null)
        {
            string normalized = ResolveRemoteDragonOwnerActionName(ownerActionName, ownerRawActionCode);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            return normalized.Equals("jump", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("ladder", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("rope", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("move", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("walk", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("swim", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("fly", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool UsesRemoteDragonLadderAnchor(string ownerActionName, int? ownerRawActionCode = null)
        {
            string normalized = ResolveRemoteDragonOwnerActionName(ownerActionName, ownerRawActionCode);
            return string.Equals(normalized, "ladder", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "rope", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveRemoteDragonOwnerActionName(string ownerActionName, int? ownerRawActionCode)
        {
            if (ownerRawActionCode.HasValue
                && CharacterPart.TryGetActionStringFromCode(ownerRawActionCode.Value, out string rawActionName)
                && !string.IsNullOrWhiteSpace(rawActionName))
            {
                return NormalizeActionName(rawActionName, allowSitFallback: false);
            }

            return NormalizeActionName(ownerActionName, allowSitFallback: false);
        }

        private static IEnumerable<string> EnumerateRemoteDragonActionCandidates(string ownerActionName)
        {
            if (string.IsNullOrWhiteSpace(ownerActionName))
            {
                yield break;
            }

            string normalized = NormalizeActionName(ownerActionName, allowSitFallback: false);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                yield break;
            }

            yield return normalized;

            if (string.Equals(normalized, "stand1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "stand2", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "alert", StringComparison.OrdinalIgnoreCase))
            {
                yield return "stand";
                yield break;
            }

            if (string.Equals(normalized, "walk1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "walk2", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "jump", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "fly", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "ladder", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "rope", StringComparison.OrdinalIgnoreCase))
            {
                yield return "move";
            }
        }

        private static bool IsExplicitRemoteDragonAction(string actionName)
        {
            return !string.IsNullOrWhiteSpace(actionName)
                && !string.Equals(actionName, "stand", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(actionName, "move", StringComparison.OrdinalIgnoreCase);
        }

        internal static int ResolveRemoteDragonActionElapsedMs(
            RemotePreparedSkillState prepared,
            int currentTime,
            string actionName,
            bool isHolding,
            int ownerActionStartTime = int.MinValue)
        {
            if (prepared == null)
            {
                return 0;
            }

            bool useOwnerActionTimeline = IsExplicitRemoteDragonAction(actionName)
                && ownerActionStartTime != int.MinValue;
            int resolvedActionStartTime = useOwnerActionTimeline
                ? ownerActionStartTime
                : ResolveRemoteDragonPhaseStartTime(prepared, isHolding);

            bool actionChanged = !string.Equals(prepared.DragonActionName, actionName, StringComparison.OrdinalIgnoreCase);
            bool ownerTimelineChanged = useOwnerActionTimeline
                && prepared.DragonOwnerActionStartTime != ownerActionStartTime;
            bool phaseTimelineChanged = !useOwnerActionTimeline
                && prepared.DragonActionStartTime != resolvedActionStartTime;
            if (actionChanged
                || ownerTimelineChanged
                || phaseTimelineChanged
                || prepared.DragonActionStartTime == int.MinValue)
            {
                prepared.DragonActionName = actionName;
                prepared.DragonActionStartTime = resolvedActionStartTime;
                prepared.DragonOwnerActionStartTime = useOwnerActionTimeline
                    ? ownerActionStartTime
                    : int.MinValue;
            }

            return Math.Max(0, currentTime - prepared.DragonActionStartTime);
        }

        private static int ResolveRemoteDragonPhaseStartTime(RemotePreparedSkillState prepared, bool isHolding)
        {
            if (prepared == null)
            {
                return 0;
            }

            return isHolding
                ? prepared.StartTime + Math.Max(0, prepared.PrepareDurationMs)
                : prepared.StartTime;
        }

        public IReadOnlyList<MinimapUI.TrackedUserMarker> BuildHelperMarkers()
        {
            _helperMarkerCount = 0;
            foreach (RemoteUserActor actor in _actorsById.Values)
            {
                if (!ShouldIncludePacketAuthoredMinimapHelper(
                        actor?.IsVisibleInWorld == true,
                        actor?.HiddenLikeClient == true,
                        actor?.HelperMarkerType.HasValue == true,
                        actor?.BattlefieldTeamId))
                {
                    continue;
                }

                bool hasFriendshipOverlay = actor.RelationshipOverlays.ContainsKey(RemoteRelationshipOverlayType.Friendship);
                bool hasCoupleOverlay = actor.RelationshipOverlays.ContainsKey(RemoteRelationshipOverlayType.Couple);
                bool hasMarriageOverlay = actor.RelationshipOverlays.ContainsKey(RemoteRelationshipOverlayType.Marriage);
                MinimapUI.HelperMarkerType markerType = ResolvePacketAuthoredMinimapHelperMarker(
                    actor.HelperMarkerType,
                    hasFriendshipOverlay,
                    hasCoupleOverlay,
                    hasMarriageOverlay);

                MinimapUI.TrackedUserMarker marker = GetOrCreateHelperMarker(_helperMarkerCount++);
                marker.WorldX = actor.Position.X;
                marker.WorldY = actor.Position.Y;
                marker.MarkerType = markerType;
                marker.ShowDirectionOverlay = actor.ShowDirectionOverlay;
                marker.TooltipText = actor.Name;
            }

            return _helperMarkerBuffer;
        }

        internal static bool ShouldIncludePacketAuthoredMinimapHelper(
            bool isVisibleInWorld,
            bool hiddenLikeClient,
            bool hasExplicitHelperMarker,
            int? battlefieldTeamId)
        {
            return isVisibleInWorld
                && !hiddenLikeClient
                && (hasExplicitHelperMarker || battlefieldTeamId.HasValue);
        }

        internal static MinimapUI.HelperMarkerType ResolvePacketAuthoredMinimapHelperMarker(
            MinimapUI.HelperMarkerType? explicitHelperMarkerType,
            bool hasFriendshipOverlay,
            bool hasCoupleOverlay,
            bool hasMarriageOverlay)
        {
            if (explicitHelperMarkerType.HasValue)
            {
                return explicitHelperMarkerType.Value;
            }

            return hasFriendshipOverlay || hasCoupleOverlay || hasMarriageOverlay
                ? MinimapUI.HelperMarkerType.Friend
                : MinimapUI.HelperMarkerType.Another;
        }

        public void Draw(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int tickCount,
            SpriteFont font,
            PlayerCharacter localPlayer = null,
            StatusBarUI statusBarUi = null)
        {
            List<RemoteUserActor> visibleActors = BuildVisibleWorldActorBuffer();
            _renderedCouplePairsBuffer.Clear();
            _renderedItemEffectPairsBuffer.Clear();

            for (int i = 0; i < visibleActors.Count; i++)
            {
                RemoteUserActor actor = visibleActors[i];
                AssembledFrame frame = actor.Assembler.GetFrameAtTime(actor.ActionName, tickCount)
                    ?? actor.Assembler.GetFrameAtTime(CharacterPart.GetActionString(CharacterAction.Stand1), tickCount);
                if (frame == null)
                {
                    continue;
                }

                int screenX = (int)Math.Round(actor.Position.X) - mapShiftX + centerX;
                int screenY = (int)Math.Round(actor.Position.Y) - mapShiftY + centerY;
                DrawPortableChairCoupleMidpointEffects(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    localPlayer,
                    mapShiftX,
                    mapShiftY,
                    centerX,
                    centerY,
                    tickCount,
                    drawFrontLayers: false,
                    _renderedCouplePairsBuffer);
                DrawPortableChairCoupleSharedLayers(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    localPlayer,
                    screenX,
                    screenY,
                    tickCount,
                    drawFrontLayers: false);
                DrawMeleeAfterImage(spriteBatch, skeletonMeshRenderer, actor, screenX, screenY, tickCount);
                DrawCarryItemEffect(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    screenX,
                    screenY,
                    tickCount,
                    drawFrontLayers: false);
                DrawCompletedSetItemEffect(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    screenX,
                    screenY,
                    tickCount,
                    drawFrontLayers: false);
                if (statusBarUi != null
                    && actor.PreparedSkill != null
                    && PreparedSkillHudRules.IsDragonOverlaySkill(actor.PreparedSkill.SkillId))
                {
                    StatusBarPreparedSkillRenderData preparedOverlay = BuildPreparedSkillWorldOverlay(actor, tickCount, 0);
                    if (preparedOverlay != null)
                    {
                    statusBarUi.DrawPreparedSkillWorldOverlay(
                        spriteBatch,
                        mapShiftX,
                        mapShiftY,
                        centerX,
                        centerY,
                        tickCount,
                        preparedOverlay);
                    }
                }

                DrawRemoteActorFrame(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    frame,
                    screenX,
                    screenY,
                    tickCount);
                DrawRelationshipOverlays(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    font,
                    localPlayer,
                    mapShiftX,
                    mapShiftY,
                    centerX,
                    centerY,
                    screenX,
                    screenY,
                    tickCount,
                    _renderedItemEffectPairsBuffer);
                DrawPortableChairCoupleSharedLayers(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    localPlayer,
                    screenX,
                    screenY,
                    tickCount,
                    drawFrontLayers: true);
                DrawPortableChairCoupleMidpointEffects(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    localPlayer,
                    mapShiftX,
                    mapShiftY,
                    centerX,
                    centerY,
                    tickCount,
                    drawFrontLayers: true,
                    _renderedCouplePairsBuffer);
                DrawCarryItemEffect(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    screenX,
                    screenY,
                    tickCount,
                    drawFrontLayers: true);
                DrawCompletedSetItemEffect(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    screenX,
                    screenY,
                    tickCount,
                    drawFrontLayers: true);
                DrawRemoteTemporaryStatAvatarEffects(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    screenX,
                    screenY,
                    tickCount,
                    drawFrontLayers: true);
                DrawRemoteTransientSkillUseAvatarEffects(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    screenX,
                    screenY,
                    tickCount,
                    drawFrontLayers: true);
                DrawRemotePacketOwnedEmotionEffect(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    screenX,
                    screenY,
                    tickCount);

                float topY = screenY - frame.FeetOffset + frame.Bounds.Top;
                DrawReceiveHpGauge(spriteBatch, actor, screenX, topY);

                if (font == null)
                {
                    continue;
                }

                IReadOnlyList<string> labelLines = BuildWorldActorLabelLines(actor);
                if (labelLines.Count == 0)
                {
                    continue;
                }

                float labelTopY = topY - ((labelLines.Count * font.LineSpacing) + ((labelLines.Count - 1) * 2f)) - 10f;
                if (actor.PartyHpGaugePos.HasValue)
                {
                    labelTopY -= RemoteReceiveHpGaugeHeight + RemoteReceiveHpGaugeVerticalPadding;
                }
                for (int lineIndex = 0; lineIndex < labelLines.Count; lineIndex++)
                {
                    string line = labelLines[lineIndex];
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    Vector2 textSize = font.MeasureString(line);
                    Texture2D guildMarkBackground = null;
                    Texture2D guildMark = null;
                    bool drawGuildMark = lineIndex == 0
                        && !string.IsNullOrWhiteSpace(actor.Build?.GuildName)
                        && TryResolveGuildMarkTextures(spriteBatch.GraphicsDevice, actor, out guildMarkBackground, out guildMark);
                    float guildMarkWidth = drawGuildMark
                        ? Math.Max(guildMarkBackground?.Width ?? 0, guildMark?.Width ?? 0)
                        : 0f;
                    float totalWidth = textSize.X + (drawGuildMark ? guildMarkWidth + 4f : 0f);
                    Vector2 textPosition = new(
                        screenX - (totalWidth / 2f) + (drawGuildMark ? guildMarkWidth + 4f : 0f),
                        labelTopY + (lineIndex * (font.LineSpacing + 2f)));
                    Color textColor = lineIndex == labelLines.Count - 1
                        ? ResolveNameColor(actor)
                        : new Color(176, 226, 255);
                    if (drawGuildMark)
                    {
                        float iconLeft = textPosition.X - guildMarkWidth - 4f;
                        DrawGuildMarkIcon(
                            spriteBatch,
                            guildMarkBackground,
                            guildMark,
                            new Vector2(iconLeft, textPosition.Y + ((font.LineSpacing - Math.Max(guildMarkBackground?.Height ?? 0, guildMark?.Height ?? 0)) * 0.5f)));
                    }

                    DrawOutlinedText(spriteBatch, font, line, textPosition, Color.Black, textColor);
                }
            }
        }

        private void DrawReceiveHpGauge(SpriteBatch spriteBatch, RemoteUserActor actor, int screenX, float topY)
        {
            if (spriteBatch?.GraphicsDevice == null
                || actor?.PartyHpGaugePos is not int gaugePos)
            {
                return;
            }

            Texture2D pixelTexture = GetPixelTexture(spriteBatch.GraphicsDevice);
            if (pixelTexture == null)
            {
                return;
            }

            int left = screenX - (RemoteReceiveHpGaugeWidth / 2);
            int top = (int)Math.Round(topY) - RemoteReceiveHpGaugeHeight - RemoteReceiveHpGaugeVerticalPadding;
            spriteBatch.Draw(
                pixelTexture,
                new Rectangle(left - 1, top - 1, RemoteReceiveHpGaugeWidth + 2, RemoteReceiveHpGaugeHeight + 2),
                new Color(20, 20, 20, 210));
            spriteBatch.Draw(
                pixelTexture,
                new Rectangle(left, top, RemoteReceiveHpGaugeWidth, RemoteReceiveHpGaugeHeight),
                new Color(72, 32, 36, 220));

            if (gaugePos > 0)
            {
                spriteBatch.Draw(
                    pixelTexture,
                    new Rectangle(left, top, gaugePos, RemoteReceiveHpGaugeHeight),
                    new Color(208, 60, 70, 235));
            }
        }

        private Texture2D GetPixelTexture(GraphicsDevice device)
        {
            if (device == null)
            {
                return null;
            }

            if (_pixelTexture == null || _pixelTexture.IsDisposed || !ReferenceEquals(_pixelTextureDevice, device))
            {
                _pixelTexture = new Texture2D(device, 1, 1);
                _pixelTexture.SetData(new[] { Color.White });
                _pixelTextureDevice = device;
            }

            return _pixelTexture;
        }

        private List<RemoteUserActor> BuildVisibleWorldActorBuffer()
        {
            _visibleWorldActorsBuffer.Clear();
            foreach (RemoteUserActor actor in _actorsById.Values)
            {
                if (actor.IsVisibleInWorld)
                {
                    _visibleWorldActorsBuffer.Add(actor);
                }
            }

            if (_visibleWorldActorsBuffer.Count > 1)
            {
                _visibleWorldActorsBuffer.Sort(VisibleWorldActorComparer);
            }

            return _visibleWorldActorsBuffer;
        }

        private StatusBarPreparedSkillRenderData GetOrCreatePreparedSkillWorldOverlay(int index)
        {
            while (_preparedSkillWorldOverlayBuffer.Count <= index)
            {
                _preparedSkillWorldOverlayBuffer.Add(new StatusBarPreparedSkillRenderData());
            }

            return _preparedSkillWorldOverlayBuffer[index];
        }

        private MinimapUI.TrackedUserMarker GetOrCreateHelperMarker(int index)
        {
            while (_helperMarkerBuffer.Count <= index)
            {
                _helperMarkerBuffer.Add(new MinimapUI.TrackedUserMarker());
            }

            return _helperMarkerBuffer[index];
        }

        private void DrawPortableChairCoupleMidpointEffects(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            RemoteUserActor actor,
            PlayerCharacter localPlayer,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int currentTime,
            bool drawFrontLayers,
            ISet<(int LeftId, int RightId)> renderedPairs)
        {
            PortableChair chair = actor?.Build?.ActivePortableChair;
            if (chair?.IsCoupleChair != true
                || chair.CoupleMidpointLayers == null
                || chair.CoupleMidpointLayers.Count == 0)
            {
                return;
            }

            if (TryResolvePortableChairPairWithLocalPlayer(actor, chair, localPlayer, out _, out _))
            {
                return;
            }

            RemoteUserActor partnerActor = FindPortableChairPairActor(
                chair,
                actor.CharacterId,
                actor.FacingRight,
                actor.Position.X,
                actor.Position.Y,
                skipCharacterId: actor.CharacterId,
                preferVisibleOnly: true);
            if (partnerActor == null)
            {
                return;
            }

            var pairKey = actor.CharacterId < partnerActor.CharacterId
                ? (actor.CharacterId, partnerActor.CharacterId)
                : (partnerActor.CharacterId, actor.CharacterId);
            if (drawFrontLayers)
            {
                if (!renderedPairs.Add(pairKey))
                {
                    return;
                }
            }
            else if (renderedPairs.Contains(pairKey))
            {
                return;
            }

            int midpointScreenX = (int)Math.Round((actor.Position.X + partnerActor.Position.X) * 0.5f) - mapShiftX + centerX;
            int midpointScreenY = (int)Math.Round((actor.Position.Y + partnerActor.Position.Y) * 0.5f)
                - mapShiftY
                + centerY
                + PlayerCharacter.PortableChairCoupleMidpointScreenYOffset;
            int animationTime = currentTime;
            for (int i = 0; i < chair.CoupleMidpointLayers.Count; i++)
            {
                PortableChairLayer layer = chair.CoupleMidpointLayers[i];
                if ((layer.RelativeZ > 0) != drawFrontLayers)
                {
                    continue;
                }

                CharacterFrame layerFrame = PlayerCharacter.GetPortableChairLayerFrameAtTime(layer, animationTime);
                PlayerCharacter.DrawPortableChairLayerFrame(
                    spriteBatch,
                    skeletonMeshRenderer,
                    layerFrame,
                    midpointScreenX,
                    midpointScreenY,
                    actor.FacingRight);
            }
        }

        private void DrawPortableChairCoupleSharedLayers(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            RemoteUserActor actor,
            PlayerCharacter localPlayer,
            int screenX,
            int screenY,
            int currentTime,
            bool drawFrontLayers)
        {
            PortableChair chair = actor?.Build?.ActivePortableChair;
            if (chair?.IsCoupleChair != true
                || chair.CoupleSharedLayers == null
                || chair.CoupleSharedLayers.Count == 0)
            {
                return;
            }

            bool hasPair = TryResolvePortableChairPairWithLocalPlayer(actor, chair, localPlayer, out _, out _);
            if (!hasPair)
            {
                hasPair = FindPortableChairPairActor(
                              chair,
                              actor.CharacterId,
                              actor.FacingRight,
                              actor.Position.X,
                              actor.Position.Y,
                              skipCharacterId: actor.CharacterId,
                              preferVisibleOnly: true) != null;
            }

            if (!hasPair)
            {
                return;
            }

            PlayerCharacter.DrawPortableChairLayers(
                spriteBatch,
                skeletonMeshRenderer,
                chair.CoupleSharedLayers,
                screenX,
                screenY,
                actor.FacingRight,
                currentTime,
                drawFrontLayers);
        }

        private void DrawRelationshipOverlays(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            RemoteUserActor actor,
            SpriteFont font,
            PlayerCharacter localPlayer,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int screenX,
            int screenY,
            int currentTime,
            ISet<(RemoteRelationshipOverlayType Type, int ItemId, int LeftId, int RightId)> renderedPairs)
        {
            if (actor?.RelationshipOverlays == null || actor.RelationshipOverlays.Count == 0)
            {
                return;
            }

            foreach (RemoteRelationshipOverlayState overlay in actor.RelationshipOverlays.Values.OrderBy(static value => value.RelationshipType))
            {
                DrawRelationshipOverlay(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    overlay,
                    font,
                    localPlayer,
                    mapShiftX,
                    mapShiftY,
                    centerX,
                    centerY,
                    screenX,
                    screenY,
                    currentTime,
                    renderedPairs);
            }
        }

        private void DrawCompletedSetItemEffect(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            RemoteUserActor actor,
            int screenX,
            int screenY,
            int currentTime,
            bool drawFrontLayers)
        {
            if (_loader == null
                || actor?.CompletedSetItemId <= 0)
            {
                return;
            }

            ItemEffectAnimationSet effect = _loader.LoadCompletedSetItemEffectAnimationSet(actor.CompletedSetItemId);
            if (effect?.OwnerLayers == null || effect.OwnerLayers.Count == 0)
            {
                return;
            }

            foreach (PortableChairLayer layer in effect.OwnerLayers)
            {
                if ((layer.RelativeZ > 0) != drawFrontLayers)
                {
                    continue;
                }

                CharacterFrame frame = PlayerCharacter.GetPortableChairLayerFrameAtTime(layer, currentTime);
                PlayerCharacter.DrawPortableChairLayerFrame(
                    spriteBatch,
                    skeletonMeshRenderer,
                    frame,
                    screenX,
                    screenY,
                    actor.FacingRight);
            }
        }

        private void DrawCarryItemEffect(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            RemoteUserActor actor,
            int screenX,
            int screenY,
            int currentTime,
            bool drawFrontLayers)
        {
            if (_loader == null
                || actor?.CarryItemEffectId is not int carryCount
                || carryCount <= 0)
            {
                return;
            }

            CarryItemEffectDefinition effect = _loader.LoadCarryItemEffectDefinition();
            if (effect?.IsReady != true)
            {
                return;
            }

            (int totalTokenCount, int tensTokenCount) = ResolveCarryItemEffectTokenCounts(carryCount);
            if (totalTokenCount <= 0)
            {
                return;
            }

            for (int index = 0; index < totalTokenCount; index++)
            {
                PortableChairLayer layer = ResolveCarryItemEffectLayer(effect, index, tensTokenCount);
                if (layer?.Animation == null)
                {
                    continue;
                }

                float phase = ((currentTime % CarryItemEffectOrbitDurationMs) / (float)CarryItemEffectOrbitDurationMs)
                    + (index / (float)Math.Max(1, totalTokenCount));
                float angle = MathHelper.TwoPi * (phase - (float)Math.Floor(phase));
                bool isFrontLayer = Math.Sin(angle) >= 0f;
                if (isFrontLayer != drawFrontLayers)
                {
                    continue;
                }

                float orbitX = (float)Math.Cos(angle) * CarryItemEffectOrbitRadiusX;
                float orbitY = CarryItemEffectBaseVerticalOffset + ((float)Math.Sin(angle) * CarryItemEffectOrbitRadiusY);
                if (!actor.FacingRight)
                {
                    orbitX = -orbitX;
                }

                CharacterFrame frame = PlayerCharacter.GetPortableChairLayerFrameAtTime(
                    layer,
                    currentTime + (index * CarryItemEffectAnimationOffsetMs));
                PlayerCharacter.DrawPortableChairLayerFrame(
                    spriteBatch,
                    skeletonMeshRenderer,
                    frame,
                    screenX + (int)Math.Round(orbitX),
                    screenY + (int)Math.Round(orbitY),
                    actor.FacingRight);
            }
        }

        private void DrawRelationshipOverlay(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            RemoteUserActor actor,
            RemoteRelationshipOverlayState overlay,
            SpriteFont font,
            PlayerCharacter localPlayer,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int screenX,
            int screenY,
            int currentTime,
            ISet<(RemoteRelationshipOverlayType Type, int ItemId, int LeftId, int RightId)> renderedPairs)
        {
            if (actor == null || overlay == null)
            {
                return;
            }

            int elapsedTime = Math.Max(0, currentTime - overlay.StartTime);
            bool usesClientRelationshipAdmission = UsesClientRelationshipAdmission(overlay.RelationshipType);
            int relationshipStatus = usesClientRelationshipAdmission
                ? 0
                : 1;
            int partnerCharacterId = 0;
            Vector2 partnerPosition = Vector2.Zero;
            bool partnerFacingRight = actor.FacingRight;
            if (TryResolveRelationshipOverlayPair(
                    actor,
                    overlay,
                    localPlayer,
                    out int resolvedPartnerCharacterId,
                    out Vector2 resolvedPartnerPosition,
                    out bool resolvedPartnerFacingRight))
            {
                partnerCharacterId = resolvedPartnerCharacterId;
                partnerPosition = resolvedPartnerPosition;
                partnerFacingRight = resolvedPartnerFacingRight;
                if (usesClientRelationshipAdmission)
                {
                    relationshipStatus = ResolveRelationshipOverlayStatus(
                        actor.Position,
                        partnerPosition,
                        overlay.RelationshipType,
                        IsRelationshipOverlaySuppressed(actor),
                        IsRelationshipOverlayPartnerSuppressed(localPlayer, partnerCharacterId, actor.CharacterId));
                }
            }
            else if (usesClientRelationshipAdmission)
            {
                relationshipStatus = 0;
            }

            if ((relationshipStatus & 1) != 0 && overlay.Effect != null)
            {
                DrawItemEffectLayers(
                    spriteBatch,
                    skeletonMeshRenderer,
                    overlay.Effect.OwnerLayers,
                    screenX,
                    screenY,
                    actor.FacingRight,
                    elapsedTime);
            }

            bool canDrawSharedRelationshipSurface = partnerCharacterId > 0 && (relationshipStatus & 2) != 0;
            bool hasSharedLayers = overlay.Effect?.SharedLayers != null && overlay.Effect.SharedLayers.Count > 0;
            if (!canDrawSharedRelationshipSurface || !hasSharedLayers)
            {
                return;
            }

            int leftId = Math.Min(actor.CharacterId, partnerCharacterId);
            int rightId = Math.Max(actor.CharacterId, partnerCharacterId);
            if (!renderedPairs.Add((overlay.RelationshipType, overlay.ItemId, leftId, rightId)))
            {
                return;
            }

            int midpointScreenX = (int)Math.Round((actor.Position.X + partnerPosition.X) * 0.5f) - mapShiftX + centerX;
            int midpointScreenY = (int)Math.Round((actor.Position.Y + partnerPosition.Y) * 0.5f) - mapShiftY + centerY
                + PlayerCharacter.PortableChairCoupleMidpointScreenYOffset;

            DrawItemEffectLayers(
                spriteBatch,
                skeletonMeshRenderer,
                overlay.Effect.SharedLayers,
                midpointScreenX,
                midpointScreenY,
                partnerFacingRight,
                elapsedTime);
        }

        private static void DrawItemEffectLayers(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            IEnumerable<PortableChairLayer> layers,
            int screenX,
            int screenY,
            bool facingRight,
            int elapsedTime)
        {
            if (layers == null)
            {
                return;
            }

            foreach (PortableChairLayer layer in layers)
            {
                CharacterFrame frame = PlayerCharacter.GetPortableChairLayerFrameAtTime(layer, elapsedTime);
                PlayerCharacter.DrawPortableChairLayerFrame(spriteBatch, skeletonMeshRenderer, frame, screenX, screenY, facingRight);
            }
        }

        public static int ResolveRelationshipOverlayStatus(
            Vector2 ownerPosition,
            Vector2 partnerPosition,
            RemoteRelationshipOverlayType relationshipType,
            bool ownerSuppressed,
            bool partnerSuppressed)
        {
            if (ownerSuppressed || partnerSuppressed)
            {
                return 0;
            }

            (int nearRangeX, int nearRangeY) = relationshipType switch
            {
                RemoteRelationshipOverlayType.NewYearCard => (NewYearCardOverlayNearRangeX, NewYearCardOverlayNearRangeY),
                _ => (RelationshipOverlayNearRangeX, RelationshipOverlayNearRangeY)
            };

            int deltaX = Math.Abs((int)Math.Round(ownerPosition.X - partnerPosition.X));
            int deltaY = Math.Abs((int)Math.Round(ownerPosition.Y - partnerPosition.Y));
            int status = 0;
            if (deltaX < RelationshipOverlayVisibleRangeX && deltaY < RelationshipOverlayVisibleRangeY)
            {
                status = 1;
            }

            if (deltaX < nearRangeX && deltaY < nearRangeY)
            {
                status |= 2;
            }

            return status;
        }

        internal static (int TotalTokenCount, int TensTokenCount) ResolveCarryItemEffectTokenCounts(int carryCount)
        {
            int clampedCount = Math.Clamp(carryCount, 0, CarryItemEffectMaximumCount);
            int tensTokenCount = clampedCount / 10;
            int totalTokenCount = tensTokenCount + (clampedCount % 10);
            return (totalTokenCount, tensTokenCount);
        }

        internal static Point ResolveCarryItemEffectOrbitOffset(
            int currentTime,
            int index,
            int totalTokenCount,
            bool facingRight,
            out bool isFrontLayer)
        {
            float phase = ((currentTime % CarryItemEffectOrbitDurationMs) / (float)CarryItemEffectOrbitDurationMs)
                + (index / (float)Math.Max(1, totalTokenCount));
            float angle = MathHelper.TwoPi * (phase - (float)Math.Floor(phase));
            isFrontLayer = Math.Sin(angle) >= 0f;

            float orbitX = (float)Math.Cos(angle) * CarryItemEffectOrbitRadiusX;
            float orbitY = CarryItemEffectBaseVerticalOffset + ((float)Math.Sin(angle) * CarryItemEffectOrbitRadiusY);
            if (!facingRight)
            {
                orbitX = -orbitX;
            }

            return new Point(
                (int)Math.Round(orbitX),
                (int)Math.Round(orbitY));
        }

        internal static int ResolveCarryItemEffectAnimationTime(int currentTime, int index)
        {
            return currentTime + (index * CarryItemEffectAnimationOffsetMs);
        }

        private bool TryResolveRelationshipOverlayPair(
            RemoteUserActor ownerActor,
            RemoteRelationshipOverlayState overlay,
            PlayerCharacter localPlayer,
            out int partnerCharacterId,
            out Vector2 partnerPosition,
            out bool partnerFacingRight)
        {
            partnerCharacterId = 0;
            partnerPosition = Vector2.Zero;
            partnerFacingRight = ownerActor?.FacingRight ?? true;
            if (ownerActor == null || overlay == null)
            {
                return false;
            }

            int localCharacterId = localPlayer?.Build?.Id ?? 0;
            if (overlay.PairCharacterId.HasValue
                && localCharacterId > 0
                && overlay.PairCharacterId.Value == localCharacterId
                && localPlayer?.IsAlive == true
                && (!UsesClientRelationshipAdmission(overlay.RelationshipType)
                    || !IsRelationshipOverlaySuppressed(localPlayer, localCharacterId, ownerActor.CharacterId)))
            {
                partnerCharacterId = localCharacterId;
                partnerPosition = localPlayer.Position;
                partnerFacingRight = localPlayer.FacingRight;
                return true;
            }

            if (overlay.PairCharacterId.HasValue
                && overlay.PairCharacterId.Value > 0
                && overlay.PairCharacterId.Value != ownerActor.CharacterId
                && _actorsById.TryGetValue(overlay.PairCharacterId.Value, out RemoteUserActor explicitPartner)
                && explicitPartner.IsVisibleInWorld
                && (!UsesClientRelationshipAdmission(overlay.RelationshipType)
                    || !IsRelationshipOverlaySuppressed(explicitPartner)))
            {
                partnerCharacterId = explicitPartner.CharacterId;
                partnerPosition = explicitPartner.Position;
                partnerFacingRight = explicitPartner.FacingRight;
                return true;
            }

            RemoteUserActor fallbackPartner = _actorsById.Values
                .Where(candidate => candidate.IsVisibleInWorld
                    && candidate.CharacterId != ownerActor.CharacterId
                    && (!UsesClientRelationshipAdmission(overlay.RelationshipType)
                        || !IsRelationshipOverlaySuppressed(candidate))
                    && candidate.RelationshipOverlays.TryGetValue(overlay.RelationshipType, out RemoteRelationshipOverlayState candidateOverlay)
                    && DoesRelationshipOverlayStateMatch(
                        ownerActor.CharacterId,
                        overlay,
                        candidate.CharacterId,
                        candidateOverlay))
                .OrderBy(candidate =>
                {
                    candidate.RelationshipOverlays.TryGetValue(overlay.RelationshipType, out RemoteRelationshipOverlayState candidateOverlay);
                    return GetRelationshipOverlayPairCandidatePriority(
                        ownerActor.CharacterId,
                        overlay,
                        candidate.CharacterId,
                        candidateOverlay);
                })
                .ThenBy(candidate => Vector2.DistanceSquared(candidate.Position, ownerActor.Position))
                .FirstOrDefault();
            if (fallbackPartner == null)
            {
                return false;
            }

            partnerCharacterId = fallbackPartner.CharacterId;
            partnerPosition = fallbackPartner.Position;
            partnerFacingRight = fallbackPartner.FacingRight;
            return true;
        }

        private static bool UsesClientRelationshipAdmission(RemoteRelationshipOverlayType relationshipType)
        {
            return relationshipType != RemoteRelationshipOverlayType.Generic;
        }

        private static int GetRelationshipOverlayPairCandidatePriority(
            int ownerCharacterId,
            RemoteRelationshipOverlayState ownerOverlay,
            int candidateCharacterId,
            RemoteRelationshipOverlayState candidateOverlay)
        {
            if (ownerOverlay == null || candidateOverlay == null)
            {
                return int.MaxValue;
            }

            if (DoesRelationshipOverlayStateMatch(
                    ownerCharacterId,
                    ownerOverlay,
                    candidateCharacterId,
                    candidateOverlay))
            {
                return 0;
            }

            if (ownerOverlay.PairCharacterId.HasValue
                && ownerOverlay.PairCharacterId.Value > 0
                && ownerOverlay.PairCharacterId.Value == candidateCharacterId)
            {
                return 1;
            }

            if (candidateOverlay.PairCharacterId.HasValue
                && candidateOverlay.PairCharacterId.Value > 0
                && candidateOverlay.PairCharacterId.Value == ownerCharacterId)
            {
                return 2;
            }

            return 3;
        }

        private static bool DoesRelationshipOverlayStateMatch(
            int ownerCharacterId,
            RemoteRelationshipOverlayState ownerOverlay,
            int candidateCharacterId,
            RemoteRelationshipOverlayState candidateOverlay)
        {
            if (ownerOverlay == null
                || candidateOverlay == null
                || ownerOverlay.RelationshipType != candidateOverlay.RelationshipType
                || ownerOverlay.ItemId <= 0
                || ownerOverlay.ItemId != candidateOverlay.ItemId
                || ownerCharacterId <= 0
                || candidateCharacterId <= 0
                || ownerCharacterId == candidateCharacterId)
            {
                return false;
            }

            return ownerOverlay.RelationshipType switch
            {
                RemoteRelationshipOverlayType.Couple => DoRingRelationshipOverlayStatesMatch(
                    ownerCharacterId,
                    ownerOverlay,
                    candidateCharacterId,
                    candidateOverlay),
                RemoteRelationshipOverlayType.Friendship => DoRingRelationshipOverlayStatesMatch(
                    ownerCharacterId,
                    ownerOverlay,
                    candidateCharacterId,
                    candidateOverlay),
                RemoteRelationshipOverlayType.NewYearCard => DoNewYearCardRelationshipOverlayStatesMatch(
                    ownerCharacterId,
                    ownerOverlay,
                    candidateCharacterId,
                    candidateOverlay),
                RemoteRelationshipOverlayType.Marriage => DoMarriageRelationshipOverlayStatesMatch(
                    ownerCharacterId,
                    ownerOverlay,
                    candidateCharacterId,
                    candidateOverlay),
                _ => true
            };
        }

        private static bool DoRingRelationshipOverlayStatesMatch(
            int ownerCharacterId,
            RemoteRelationshipOverlayState ownerOverlay,
            int candidateCharacterId,
            RemoteRelationshipOverlayState candidateOverlay)
        {
            return ownerOverlay.ItemSerial.HasValue
                && ownerOverlay.PairItemSerial.HasValue
                && candidateOverlay.ItemSerial.HasValue
                && candidateOverlay.PairItemSerial.HasValue
                && ownerOverlay.PairItemSerial.Value == candidateOverlay.ItemSerial.Value
                && candidateOverlay.PairItemSerial.Value == ownerOverlay.ItemSerial.Value
                && RelationshipRecordTargetsCharacter(ownerOverlay.PairCharacterId, candidateCharacterId)
                && RelationshipRecordTargetsCharacter(candidateOverlay.PairCharacterId, ownerCharacterId);
        }

        private static bool DoNewYearCardRelationshipOverlayStatesMatch(
            int ownerCharacterId,
            RemoteRelationshipOverlayState ownerOverlay,
            int candidateCharacterId,
            RemoteRelationshipOverlayState candidateOverlay)
        {
            return ownerOverlay.ItemSerial.HasValue
                && candidateOverlay.ItemSerial.HasValue
                && ownerOverlay.ItemSerial.Value == candidateOverlay.ItemSerial.Value
                && RelationshipRecordTargetsCharacter(ownerOverlay.PairCharacterId, candidateCharacterId)
                && RelationshipRecordTargetsCharacter(candidateOverlay.PairCharacterId, ownerCharacterId);
        }

        private static bool DoMarriageRelationshipOverlayStatesMatch(
            int ownerCharacterId,
            RemoteRelationshipOverlayState ownerOverlay,
            int candidateCharacterId,
            RemoteRelationshipOverlayState candidateOverlay)
        {
            return RelationshipRecordTargetsCharacter(ownerOverlay.PairCharacterId, candidateCharacterId)
                && RelationshipRecordTargetsCharacter(candidateOverlay.PairCharacterId, ownerCharacterId);
        }

        private static bool IsRelationshipOverlaySuppressed(PlayerCharacter player, int expectedCharacterId, int ownerCharacterId)
        {
            if (player?.Build == null
                || !player.IsAlive
                || player.Build.Id <= 0
                || player.Build.Id != expectedCharacterId
                || player.Build.Id == ownerCharacterId)
            {
                return true;
            }

            return player.HasActiveMorphTransform
                || IsRelationshipOverlayGhostAction(player.CurrentActionName);
        }

        private static bool IsRelationshipOverlaySuppressed(RemoteUserActor actor)
        {
            if (actor == null || !actor.IsVisibleInWorld)
            {
                return true;
            }

            return actor.HasMorphTemplate
                || IsRelationshipOverlayGhostAction(actor.ActionName);
        }

        private void ApplyPacketOwnedEmotionState(
            RemoteUserActor actor,
            int itemId,
            PacketOwnedAvatarEmotionSelection selection,
            bool byItemOption,
            int currentTime)
        {
            ApplyRemoteEmotionState(
                actor,
                itemId,
                selection.EmotionId,
                selection.EmotionName,
                byItemOption,
                currentTime,
                durationMs: 0,
                loadEffectAnimation: true);
        }

        private void ApplyRemoteEmotionState(
            RemoteUserActor actor,
            int itemId,
            int emotionId,
            string emotionName,
            bool byItemOption,
            int currentTime,
            int durationMs,
            bool loadEffectAnimation)
        {
            if (actor == null)
            {
                return;
            }

            SkillAnimation effectAnimation = loadEffectAnimation
                ? _loader?.LoadPacketOwnedEmotionEffectAnimation(emotionName)
                : null;
            int expireDuration = effectAnimation?.TotalDuration > 0
                ? effectAnimation.TotalDuration
                : Math.Max(0, durationMs);
            int expireTime = expireDuration > 0
                ? currentTime + expireDuration
                : 0;
            actor.PacketOwnedEmotion = new RemotePacketOwnedEmotionState
            {
                ItemId = itemId,
                EmotionId = emotionId,
                EmotionName = emotionName,
                ByItemOption = byItemOption,
                EffectAnimation = effectAnimation,
                AnimationStartTime = currentTime,
                ExpireTime = expireTime
            };

            if (actor.Assembler != null)
            {
                actor.Assembler.FaceExpressionName = emotionName;
            }
        }

        private static void ClearPacketOwnedEmotionState(RemoteUserActor actor)
        {
            if (actor == null)
            {
                return;
            }

            actor.PacketOwnedEmotion = null;
            if (actor.Assembler != null)
            {
                actor.Assembler.FaceExpressionName = "default";
            }
        }

        private static void UpdatePacketOwnedEmotionState(RemoteUserActor actor, int currentTime)
        {
            if (actor?.PacketOwnedEmotion == null)
            {
                return;
            }

            if (actor.PacketOwnedEmotion.ExpireTime > 0
                && currentTime >= actor.PacketOwnedEmotion.ExpireTime)
            {
                ClearPacketOwnedEmotionState(actor);
            }
        }

        private bool IsRelationshipOverlayPartnerSuppressed(PlayerCharacter localPlayer, int partnerCharacterId, int ownerCharacterId)
        {
            if (partnerCharacterId <= 0 || partnerCharacterId == ownerCharacterId)
            {
                return true;
            }

            if (localPlayer?.Build?.Id == partnerCharacterId)
            {
                return IsRelationshipOverlaySuppressed(localPlayer, partnerCharacterId, ownerCharacterId);
            }

            return !_actorsById.TryGetValue(partnerCharacterId, out RemoteUserActor actor)
                   || IsRelationshipOverlaySuppressed(actor);
        }

        private static bool IsRelationshipOverlayGhostAction(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            return string.Equals(actionName, CharacterPart.GetActionString(CharacterAction.Ghost), StringComparison.OrdinalIgnoreCase)
                || actionName.StartsWith("ghost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "darksight", StringComparison.OrdinalIgnoreCase);
        }

        internal static PortableChairLayer ResolveCarryItemEffectLayer(
            CarryItemEffectDefinition effect,
            int index,
            int tensTokenCount)
        {
            if (index < tensTokenCount)
            {
                return effect.BundleLayer ?? effect.SingleLayerA ?? effect.SingleLayerB;
            }

            int singleIndex = index - tensTokenCount;
            if ((singleIndex & 1) == 0)
            {
                return effect.SingleLayerA ?? effect.SingleLayerB ?? effect.BundleLayer;
            }

            return effect.SingleLayerB ?? effect.SingleLayerA ?? effect.BundleLayer;
        }

        private static ItemEffectAnimationSet NormalizeRelationshipOverlayEffect(
            ItemEffectAnimationSet source,
            RemoteRelationshipOverlayType relationshipType,
            bool shouldLoop)
        {
            ItemEffectAnimationSet cloned = CloneRelationshipOverlayEffect(source, shouldLoop);
            if (cloned == null)
            {
                return null;
            }

            if (relationshipType == RemoteRelationshipOverlayType.NewYearCard
                && cloned.SharedLayers.Count == 0
                && cloned.OwnerLayers.Count > 0)
            {
                cloned.SharedLayers.AddRange(cloned.OwnerLayers);
                cloned.OwnerLayers.Clear();
            }

            return cloned;
        }

        private static ItemEffectAnimationSet CloneRelationshipOverlayEffect(ItemEffectAnimationSet source, bool shouldLoop)
        {
            if (source == null)
            {
                return null;
            }

            return new ItemEffectAnimationSet
            {
                ItemId = source.ItemId,
                OwnerLayers = CloneRelationshipOverlayLayers(source.OwnerLayers, shouldLoop),
                SharedLayers = CloneRelationshipOverlayLayers(source.SharedLayers, shouldLoop)
            };
        }

        private static List<PortableChairLayer> CloneRelationshipOverlayLayers(
            IEnumerable<PortableChairLayer> layers,
            bool shouldLoop)
        {
            List<PortableChairLayer> clones = new();
            if (layers == null)
            {
                return clones;
            }

            foreach (PortableChairLayer layer in layers)
            {
                if (layer == null)
                {
                    continue;
                }

                CharacterAnimation animation = null;
                if (layer.Animation != null)
                {
                    animation = new CharacterAnimation
                    {
                        Action = layer.Animation.Action,
                        ActionName = layer.Animation.ActionName,
                        Frames = layer.Animation.Frames,
                        Loop = shouldLoop
                    };
                    animation.CalculateTotalDuration();
                }

                clones.Add(new PortableChairLayer
                {
                    Name = layer.Name,
                    Animation = animation,
                    RelativeZ = layer.RelativeZ,
                    PositionHint = layer.PositionHint
                });
            }

            return clones;
        }

        public string DescribeStatus()
        {
            if (_actorsById.Count == 0)
            {
                return $"Remote user pool empty. {DescribeRelationshipRecordTableStatus()}";
            }

            return $"Remote user pool active, count={_actorsById.Count}, users={string.Join("; ", _actorsById.Values.OrderBy(static value => value.CharacterId).Select(static value => value.Describe()))}. {DescribeRelationshipRecordTableStatus()}";
        }

        private void AssignDriverPassengerLink(int driverId, int passengerId)
        {
            if (!_actorsById.TryGetValue(driverId, out RemoteUserActor driverActor))
            {
                return;
            }

            int previousPassengerId = driverActor.FollowPassengerId;
            if (previousPassengerId > 0
                && previousPassengerId != passengerId
                && _actorsById.TryGetValue(previousPassengerId, out RemoteUserActor previousPassenger)
                && previousPassenger.FollowDriverId == driverId)
            {
                previousPassenger.FollowDriverId = 0;
            }

            driverActor.FollowPassengerId = passengerId;
        }

        private void ClearDriverPassengerLink(int driverId, int passengerId)
        {
            if (driverId <= 0
                || !_actorsById.TryGetValue(driverId, out RemoteUserActor driverActor)
                || driverActor.FollowPassengerId != passengerId)
            {
                return;
            }

            driverActor.FollowPassengerId = 0;
        }

        private void ClearActorFollowLinks(RemoteUserActor actor)
        {
            if (actor == null)
            {
                return;
            }

            ClearDriverPassengerLink(actor.FollowDriverId, actor.CharacterId);
            if (actor.FollowPassengerId > 0
                && _actorsById.TryGetValue(actor.FollowPassengerId, out RemoteUserActor passengerActor)
                && passengerActor.FollowDriverId == actor.CharacterId)
            {
                passengerActor.FollowDriverId = 0;
            }

            actor.FollowDriverId = 0;
            actor.FollowPassengerId = 0;
        }

        private bool TryApplyAvatarModifiedRelationshipRecord(
            RemoteUserActor actor,
            RemoteRelationshipOverlayType relationshipType,
            RemoteUserRelationshipRecord relationshipRecord,
            int currentTime,
            out string message)
        {
            if (actor == null)
            {
                message = "Remote actor is required.";
                return false;
            }

            EnsureRelationshipRecordTablesInitialized();
            Dictionary<int, RemoteUserRelationshipRecord> recordTable = GetRelationshipRecordTable(relationshipType);
            if (relationshipRecord.IsActive)
            {
                int? pairCharacterId = relationshipType == RemoteRelationshipOverlayType.Marriage
                    ? ResolveMarriagePairCharacterId(actor.CharacterId, relationshipRecord)
                    : relationshipRecord.PairCharacterId;
                recordTable[actor.CharacterId] = relationshipRecord with
                {
                    CharacterId = actor.CharacterId,
                    PairCharacterId = pairCharacterId
                };
            }
            else
            {
                recordTable.Remove(actor.CharacterId);
            }

            RefreshRelationshipOverlays(relationshipType, currentTime);
            message = $"Remote user {actor.CharacterId} {relationshipType} relationship state refreshed.";
            return true;
        }

        private static int? ResolveMarriagePairCharacterId(int ownerCharacterId, RemoteUserRelationshipRecord relationshipRecord)
        {
            if (!relationshipRecord.IsActive)
            {
                return null;
            }

            if (relationshipRecord.CharacterId.HasValue
                && relationshipRecord.CharacterId.Value > 0
                && relationshipRecord.CharacterId.Value != ownerCharacterId)
            {
                return relationshipRecord.CharacterId.Value;
            }

            if (relationshipRecord.PairCharacterId.HasValue
                && relationshipRecord.PairCharacterId.Value > 0
                && relationshipRecord.PairCharacterId.Value != ownerCharacterId)
            {
                return relationshipRecord.PairCharacterId.Value;
            }

            return relationshipRecord.PairCharacterId;
        }

        private void SyncRelationshipOverlaysFromRecords(int ownerCharacterId, int currentTime)
        {
            if (!_actorsById.TryGetValue(ownerCharacterId, out RemoteUserActor actor))
            {
                return;
            }

            SyncRelationshipOverlayFromRecord(actor, RemoteRelationshipOverlayType.Couple, currentTime);
            SyncRelationshipOverlayFromRecord(actor, RemoteRelationshipOverlayType.Friendship, currentTime);
            SyncRelationshipOverlayFromRecord(actor, RemoteRelationshipOverlayType.NewYearCard, currentTime);
            SyncRelationshipOverlayFromRecord(actor, RemoteRelationshipOverlayType.Marriage, currentTime);
        }

        private void SyncRelationshipOverlayFromRecord(RemoteUserActor actor, RemoteRelationshipOverlayType relationshipType, int currentTime)
        {
            Dictionary<int, RemoteUserRelationshipRecord> recordTable = GetRelationshipRecordTable(relationshipType);
            if (!recordTable.TryGetValue(actor.CharacterId, out RemoteUserRelationshipRecord record)
                || !record.IsActive
                || record.ItemId <= 0)
            {
                actor.RelationshipOverlays.Remove(relationshipType);
                return;
            }

            int? pairCharacterId = ResolveRelationshipOverlayPairCharacterIdFromRecord(actor.CharacterId, relationshipType, record);
            if (!pairCharacterId.HasValue || pairCharacterId.Value <= 0)
            {
                actor.RelationshipOverlays.Remove(relationshipType);
                return;
            }

            TrySetItemEffect(
                actor.CharacterId,
                relationshipType,
                record.ItemId,
                pairCharacterId,
                currentTime,
                out _,
                record.ItemSerial,
                record.PairItemSerial);
        }

        private void RefreshRelationshipOverlays(RemoteRelationshipOverlayType relationshipType, int currentTime)
        {
            foreach (RemoteUserActor actor in _actorsById.Values)
            {
                SyncRelationshipOverlayFromRecord(actor, relationshipType, currentTime);
            }
        }

        private int? ResolveRelationshipOverlayPairCharacterIdFromRecord(
            int ownerCharacterId,
            RemoteRelationshipOverlayType relationshipType,
            RemoteUserRelationshipRecord relationshipRecord)
        {
            int? explicitPairCharacterId = relationshipType == RemoteRelationshipOverlayType.Marriage
                ? ResolveMarriagePairCharacterId(ownerCharacterId, relationshipRecord)
                : relationshipRecord.PairCharacterId;
            if (relationshipType == RemoteRelationshipOverlayType.Generic)
            {
                return explicitPairCharacterId;
            }

            if (TryFindMatchedRemoteRelationshipRecordOwner(
                    ownerCharacterId,
                    relationshipType,
                    relationshipRecord,
                    explicitPairCharacterId,
                    out int matchedRemoteCharacterId))
            {
                return matchedRemoteCharacterId;
            }

            if (explicitPairCharacterId.HasValue
                && explicitPairCharacterId.Value > 0
                && _actorsById.ContainsKey(explicitPairCharacterId.Value))
            {
                return null;
            }

            return explicitPairCharacterId;
        }

        private bool TryFindMatchedRemoteRelationshipRecordOwner(
            int ownerCharacterId,
            RemoteRelationshipOverlayType relationshipType,
            RemoteUserRelationshipRecord relationshipRecord,
            int? explicitPairCharacterId,
            out int matchedRemoteCharacterId)
        {
            matchedRemoteCharacterId = 0;
            Dictionary<int, RemoteUserRelationshipRecord> recordTable = GetRelationshipRecordTable(relationshipType);
            if (recordTable.Count == 0)
            {
                return false;
            }

            if (explicitPairCharacterId.HasValue
                && explicitPairCharacterId.Value > 0
                && explicitPairCharacterId.Value != ownerCharacterId
                && recordTable.TryGetValue(explicitPairCharacterId.Value, out RemoteUserRelationshipRecord explicitPartnerRecord)
                && DoRelationshipRecordsMatch(
                    relationshipType,
                    ownerCharacterId,
                    relationshipRecord,
                    explicitPairCharacterId.Value,
                    explicitPartnerRecord))
            {
                matchedRemoteCharacterId = explicitPairCharacterId.Value;
                return true;
            }

            foreach (KeyValuePair<int, RemoteUserRelationshipRecord> entry in recordTable
                .OrderByDescending(candidate => explicitPairCharacterId.HasValue && candidate.Key == explicitPairCharacterId.Value)
                .ThenBy(candidate => candidate.Key))
            {
                if (entry.Key == ownerCharacterId)
                {
                    continue;
                }

                if (!DoRelationshipRecordsMatch(
                        relationshipType,
                        ownerCharacterId,
                        relationshipRecord,
                        entry.Key,
                        entry.Value))
                {
                    continue;
                }

                matchedRemoteCharacterId = entry.Key;
                return true;
            }

            return false;
        }

        private void EnsureRelationshipRecordTablesInitialized()
        {
            EnsureRelationshipRecordTable(RemoteRelationshipOverlayType.Couple);
            EnsureRelationshipRecordTable(RemoteRelationshipOverlayType.Friendship);
            EnsureRelationshipRecordTable(RemoteRelationshipOverlayType.NewYearCard);
            EnsureRelationshipRecordTable(RemoteRelationshipOverlayType.Marriage);
        }

        private void EnsureRelationshipRecordTable(RemoteRelationshipOverlayType relationshipType)
        {
            if (!_relationshipRecordsByOwnerCharacterId.ContainsKey(relationshipType))
            {
                _relationshipRecordsByOwnerCharacterId[relationshipType] = new Dictionary<int, RemoteUserRelationshipRecord>();
            }

            if (!_relationshipRecordOwnerByDispatchKey.ContainsKey(relationshipType))
            {
                _relationshipRecordOwnerByDispatchKey[relationshipType] = new Dictionary<RemoteRelationshipRecordDispatchKey, int>();
            }
        }

        private void ClearRelationshipRecordTables()
        {
            foreach (Dictionary<int, RemoteUserRelationshipRecord> table in _relationshipRecordsByOwnerCharacterId.Values)
            {
                table.Clear();
            }

            foreach (Dictionary<RemoteRelationshipRecordDispatchKey, int> table in _relationshipRecordOwnerByDispatchKey.Values)
            {
                table.Clear();
            }
        }

        private Dictionary<int, RemoteUserRelationshipRecord> GetRelationshipRecordTable(RemoteRelationshipOverlayType relationshipType)
        {
            EnsureRelationshipRecordTable(relationshipType);
            return _relationshipRecordsByOwnerCharacterId[relationshipType];
        }

        private Dictionary<RemoteRelationshipRecordDispatchKey, int> GetRelationshipRecordDispatchOwnerTable(RemoteRelationshipOverlayType relationshipType)
        {
            EnsureRelationshipRecordTable(relationshipType);
            return _relationshipRecordOwnerByDispatchKey[relationshipType];
        }

        private void RegisterRelationshipRecordDispatchKey(
            RemoteRelationshipOverlayType relationshipType,
            RemoteRelationshipRecordDispatchKey dispatchKey,
            int ownerCharacterId)
        {
            if (!dispatchKey.HasValue || ownerCharacterId <= 0)
            {
                return;
            }

            GetRelationshipRecordDispatchOwnerTable(relationshipType)[dispatchKey] = ownerCharacterId;
        }

        private void RemoveRelationshipRecordDispatchKeysForOwner(RemoteRelationshipOverlayType relationshipType, int ownerCharacterId)
        {
            if (ownerCharacterId <= 0)
            {
                return;
            }

            Dictionary<RemoteRelationshipRecordDispatchKey, int> dispatchTable = GetRelationshipRecordDispatchOwnerTable(relationshipType);
            foreach (RemoteRelationshipRecordDispatchKey dispatchKey in dispatchTable
                .Where(entry => entry.Value == ownerCharacterId)
                .Select(entry => entry.Key)
                .ToArray())
            {
                dispatchTable.Remove(dispatchKey);
            }
        }

        private void RemoveRelationshipRecordOwner(
            RemoteRelationshipOverlayType relationshipType,
            int ownerCharacterId,
            IDictionary<int, RemoteUserRelationshipRecord> recordTable)
        {
            recordTable?.Remove(ownerCharacterId);
            RemoveRelationshipRecordDispatchKeysForOwner(relationshipType, ownerCharacterId);
            if (_actorsById.TryGetValue(ownerCharacterId, out RemoteUserActor ownerActor))
            {
                ownerActor.RelationshipOverlays.Remove(relationshipType);
            }
        }

        private string DescribeRelationshipRecordTableStatus()
        {
            EnsureRelationshipRecordTablesInitialized();
            int coupleCount = GetRelationshipRecordTable(RemoteRelationshipOverlayType.Couple).Count;
            int friendshipCount = GetRelationshipRecordTable(RemoteRelationshipOverlayType.Friendship).Count;
            int newYearCount = GetRelationshipRecordTable(RemoteRelationshipOverlayType.NewYearCard).Count;
            int marriageCount = GetRelationshipRecordTable(RemoteRelationshipOverlayType.Marriage).Count;
            return $"Relationship record tables: couple={coupleCount}, friendship={friendshipCount}, newYear={newYearCount}, marriage={marriageCount}.";
        }

        private static bool DoesRelationshipRecordRemovalMatch(
            RemoteUserRelationshipRecordRemovePacket packet,
            int ownerCharacterId,
            long? itemSerial,
            long? pairItemSerial,
            int? pairCharacterId)
        {
            if (packet.CharacterId.HasValue && packet.CharacterId.Value > 0 && packet.CharacterId.Value == ownerCharacterId)
            {
                return true;
            }

            if (packet.CharacterId.HasValue
                && packet.CharacterId.Value > 0
                && pairCharacterId.HasValue
                && pairCharacterId.Value == packet.CharacterId.Value)
            {
                return true;
            }

            if (!packet.ItemSerial.HasValue)
            {
                return false;
            }

            long packetItemSerial = packet.ItemSerial.Value;
            return itemSerial == packetItemSerial || pairItemSerial == packetItemSerial;
        }

        internal static bool DoRelationshipRecordsMatch(
            RemoteRelationshipOverlayType relationshipType,
            int ownerCharacterId,
            RemoteUserRelationshipRecord ownerRecord,
            int partnerCharacterId,
            RemoteUserRelationshipRecord partnerRecord)
        {
            if (!ownerRecord.IsActive
                || !partnerRecord.IsActive
                || ownerCharacterId <= 0
                || partnerCharacterId <= 0
                || ownerCharacterId == partnerCharacterId)
            {
                return false;
            }

            return relationshipType switch
            {
                RemoteRelationshipOverlayType.Couple => DoRingRelationshipRecordsMatch(
                    ownerCharacterId,
                    ownerRecord,
                    partnerCharacterId,
                    partnerRecord),
                RemoteRelationshipOverlayType.Friendship => DoRingRelationshipRecordsMatch(
                    ownerCharacterId,
                    ownerRecord,
                    partnerCharacterId,
                    partnerRecord),
                RemoteRelationshipOverlayType.NewYearCard => DoNewYearCardRelationshipRecordsMatch(
                    ownerCharacterId,
                    ownerRecord,
                    partnerCharacterId,
                    partnerRecord),
                RemoteRelationshipOverlayType.Marriage => DoMarriageRelationshipRecordsMatch(
                    ownerCharacterId,
                    ownerRecord,
                    partnerCharacterId,
                    partnerRecord),
                _ => false
            };
        }

        private static bool DoRingRelationshipRecordsMatch(
            int ownerCharacterId,
            RemoteUserRelationshipRecord ownerRecord,
            int partnerCharacterId,
            RemoteUserRelationshipRecord partnerRecord)
        {
            return ownerRecord.ItemId > 0
                && ownerRecord.ItemId == partnerRecord.ItemId
                && ownerRecord.ItemSerial.HasValue
                && ownerRecord.PairItemSerial.HasValue
                && partnerRecord.ItemSerial.HasValue
                && partnerRecord.PairItemSerial.HasValue
                && ownerRecord.PairItemSerial.Value == partnerRecord.ItemSerial.Value
                && partnerRecord.PairItemSerial.Value == ownerRecord.ItemSerial.Value
                && RelationshipRecordTargetsCharacter(ownerRecord.PairCharacterId, partnerCharacterId)
                && RelationshipRecordTargetsCharacter(partnerRecord.PairCharacterId, ownerCharacterId);
        }

        private static bool DoNewYearCardRelationshipRecordsMatch(
            int ownerCharacterId,
            RemoteUserRelationshipRecord ownerRecord,
            int partnerCharacterId,
            RemoteUserRelationshipRecord partnerRecord)
        {
            return ownerRecord.ItemId > 0
                && ownerRecord.ItemId == partnerRecord.ItemId
                && ownerRecord.ItemSerial.HasValue
                && partnerRecord.ItemSerial.HasValue
                && ownerRecord.ItemSerial.Value == partnerRecord.ItemSerial.Value
                && RelationshipRecordTargetsCharacter(ownerRecord.PairCharacterId, partnerCharacterId)
                && RelationshipRecordTargetsCharacter(partnerRecord.PairCharacterId, ownerCharacterId);
        }

        private static bool DoMarriageRelationshipRecordsMatch(
            int ownerCharacterId,
            RemoteUserRelationshipRecord ownerRecord,
            int partnerCharacterId,
            RemoteUserRelationshipRecord partnerRecord)
        {
            return ownerRecord.ItemId > 0
                && ownerRecord.ItemId == partnerRecord.ItemId
                && ResolveMarriagePairCharacterId(ownerCharacterId, ownerRecord) == partnerCharacterId
                && ResolveMarriagePairCharacterId(partnerCharacterId, partnerRecord) == ownerCharacterId;
        }

        private static bool RelationshipRecordTargetsCharacter(int? candidateCharacterId, int expectedCharacterId)
        {
            return !candidateCharacterId.HasValue
                || candidateCharacterId.Value <= 0
                || candidateCharacterId.Value == expectedCharacterId;
        }

        private static void DrawOutlinedText(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 position, Color shadowColor, Color textColor)
        {
            Vector2[] offsets =
            {
                new Vector2(-1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, -1f),
                new Vector2(0f, 1f)
            };

            foreach (Vector2 offset in offsets)
            {
                spriteBatch.DrawString(font, text, position + offset, shadowColor);
            }

            spriteBatch.DrawString(font, text, position, textColor);
        }

        private static void DrawGuildMarkIcon(
            SpriteBatch spriteBatch,
            Texture2D backgroundTexture,
            Texture2D markTexture,
            Vector2 position)
        {
            if (backgroundTexture != null)
            {
                spriteBatch.Draw(backgroundTexture, position, Color.White);
            }

            if (markTexture == null)
            {
                return;
            }

            if (backgroundTexture == null)
            {
                spriteBatch.Draw(markTexture, position, Color.White);
                return;
            }

            Vector2 markPosition = new(
                position.X + ((backgroundTexture.Width - markTexture.Width) * 0.5f),
                position.Y + ((backgroundTexture.Height - markTexture.Height) * 0.5f));
            spriteBatch.Draw(markTexture, markPosition, Color.White);
        }

        private static bool TryResolveGuildMarkTextures(
            GraphicsDevice device,
            RemoteUserActor actor,
            out Texture2D backgroundTexture,
            out Texture2D markTexture)
        {
            backgroundTexture = null;
            markTexture = null;
            CharacterBuild build = actor?.Build;
            if (device == null
                || build?.GuildMarkBackgroundId is not int backgroundId || backgroundId <= 0
                || build.GuildMarkId is not int markId || markId <= 0)
            {
                return false;
            }

            int backgroundColor = build.GuildMarkBackgroundColor ?? 1;
            int markColor = build.GuildMarkColor ?? 1;
            backgroundTexture = GuildMarkTextureCache.GetBackgroundTexture(device, backgroundId, backgroundColor);
            markTexture = GuildMarkTextureCache.GetMarkTexture(device, markId, markColor);
            return backgroundTexture != null || markTexture != null;
        }

        private static Color ResolveNameColor(RemoteUserActor actor)
        {
            return actor.BattlefieldTeamId switch
            {
                0 => new Color(255, 232, 170),
                1 => new Color(255, 189, 189),
                2 => new Color(185, 229, 255),
                _ => Color.White
            };
        }

        internal static IReadOnlyList<string> BuildWorldActorLabelLines(RemoteUserActor actor)
        {
            List<string> lines = new();
            if (actor == null)
            {
                return lines;
            }

            string guildName = actor.Build?.GuildName?.Trim();
            if (!string.IsNullOrWhiteSpace(guildName))
            {
                lines.Add(guildName);
            }

            string name = actor.Name?.Trim();
            if (!string.IsNullOrWhiteSpace(name))
            {
                lines.Add(name);
            }

            return lines;
        }

        internal static IReadOnlyDictionary<int, int> ResolvePortableChairPairings(
            IEnumerable<PortableChairPairParticipant> participants,
            bool preferVisibleOnly)
        {
            IEnumerable<PortableChairPairRecord> pairRecords = participants?
                .Select(static participant => new PortableChairPairRecord(
                    participant.CharacterId,
                    participant.Chair?.ItemId ?? 0,
                    participant.PreferredPairCharacterId));
            return ResolvePortableChairPairings(participants, pairRecords, preferVisibleOnly);
        }

        internal static IReadOnlyDictionary<int, int> ResolvePortableChairPairings(
            IEnumerable<PortableChairPairParticipant> participants,
            IEnumerable<PortableChairPairRecord> pairRecords,
            bool preferVisibleOnly)
        {
            Dictionary<int, int> pairs = new();
            foreach (PortableChairPairRecord record in ResolvePortableChairPairRecords(participants, pairRecords, preferVisibleOnly).Values)
            {
                if (!record.IsActive
                    || !record.PairCharacterId.HasValue
                    || pairs.ContainsKey(record.CharacterId)
                    || pairs.ContainsKey(record.PairCharacterId.Value))
                {
                    continue;
                }

                pairs[record.CharacterId] = record.PairCharacterId.Value;
                pairs[record.PairCharacterId.Value] = record.CharacterId;
            }

            return pairs;
        }

        internal static IReadOnlyDictionary<int, PortableChairPairRecord> ResolvePortableChairPairRecords(
            IEnumerable<PortableChairPairParticipant> participants,
            IEnumerable<PortableChairPairRecord> pairRecords,
            bool preferVisibleOnly)
        {
            IReadOnlyDictionary<int, PortableChairPairRecord> recordMap = pairRecords?
                .Where(static record => record.CharacterId > 0 && record.ItemId > 0)
                .GroupBy(static record => record.CharacterId)
                .ToDictionary(
                    static group => group.Key,
                    static group => group.Last() with
                    {
                        PairCharacterId = null,
                        Status = 0
                    })
                ?? new Dictionary<int, PortableChairPairRecord>();
            List<PortableChairPairParticipant> resolvedParticipants = participants?
                .Where(participant =>
                    participant.CharacterId > 0
                    && participant.Chair?.IsCoupleChair == true
                    && participant.IsChairSessionActive
                    && recordMap.TryGetValue(participant.CharacterId, out PortableChairPairRecord record)
                    && participant.Chair.ItemId == record.ItemId)
                .Select(participant =>
                {
                    PortableChairPairRecord record = recordMap[participant.CharacterId];
                    return new PortableChairPairParticipant(
                        participant.CharacterId,
                        participant.Chair,
                        participant.Position,
                        participant.FacingRight,
                        record.PreferredPairCharacterId,
                        participant.IsChairSessionActive,
                        participant.IsVisibleInWorld,
                        participant.IsRelationshipOverlaySuppressed);
                })
                .OrderBy(static participant => participant.CharacterId)
                .ToList()
                ?? new List<PortableChairPairParticipant>();
            if (resolvedParticipants.Count < 2)
            {
                return new Dictionary<int, PortableChairPairRecord>(recordMap);
            }

            List<PortableChairPairCandidate> candidates = new();
            for (int i = 0; i < resolvedParticipants.Count - 1; i++)
            {
                PortableChairPairParticipant left = resolvedParticipants[i];
                for (int j = i + 1; j < resolvedParticipants.Count; j++)
                {
                    PortableChairPairParticipant right = resolvedParticipants[j];
                    if (!TryBuildPortableChairPairCandidate(left, right, preferVisibleOnly, out PortableChairPairCandidate candidate))
                    {
                        continue;
                    }

                    candidates.Add(candidate);
                }
            }

            Dictionary<int, PortableChairPairRecord> resolvedRecords = new(recordMap);
            foreach (PortableChairPairCandidate candidate in candidates
                .OrderBy(static candidate => candidate.Priority)
                .ThenBy(static candidate => candidate.Score)
                .ThenBy(static candidate => candidate.Left.CharacterId)
                .ThenBy(static candidate => candidate.Right.CharacterId))
            {
                if (!resolvedRecords.TryGetValue(candidate.Left.CharacterId, out PortableChairPairRecord leftRecord)
                    || !resolvedRecords.TryGetValue(candidate.Right.CharacterId, out PortableChairPairRecord rightRecord)
                    || leftRecord.IsActive
                    || rightRecord.IsActive)
                {
                    continue;
                }

                int status = ResolvePortableChairPairStatus(candidate.Left, candidate.Right);
                if (status == 0)
                {
                    continue;
                }

                resolvedRecords[candidate.Left.CharacterId] = leftRecord with
                {
                    PairCharacterId = candidate.Right.CharacterId,
                    Status = status
                };
                resolvedRecords[candidate.Right.CharacterId] = rightRecord with
                {
                    PairCharacterId = candidate.Left.CharacterId,
                    Status = status
                };
            }

            return resolvedRecords;
        }

        private Dictionary<int, int> BuildPortableChairPairMap(PlayerCharacter localPlayer, bool preferVisibleOnly)
        {
            List<PortableChairPairParticipant> participants = new(_actorsById.Count + 1);
            foreach (RemoteUserActor actor in _actorsById.Values)
            {
                PortableChair chair = actor?.Build?.ActivePortableChair;
                if (chair?.IsCoupleChair != true)
                {
                    continue;
                }

                participants.Add(new PortableChairPairParticipant(
                    actor.CharacterId,
                    chair,
                    actor.Position,
                    actor.FacingRight,
                    actor.PreferredPortableChairPairCharacterId,
                    IsPortableChairPairSessionActive(actor.Build?.ActivePortableChair, actor.BaseActionName),
                    actor.IsVisibleInWorld,
                    IsRelationshipOverlaySuppressed(actor)));
            }

            PortableChair localChair = localPlayer?.Build?.ActivePortableChair;
            int localCharacterId = localPlayer?.Build?.Id ?? 0;
            if (localChair?.IsCoupleChair == true
                && localPlayer.IsAlive
                && localCharacterId > 0)
            {
                participants.Add(new PortableChairPairParticipant(
                    localCharacterId,
                    localChair,
                    localPlayer.Position,
                    localPlayer.FacingRight,
                    null,
                    IsPortableChairPairSessionActive(localChair, localPlayer.CurrentActionName),
                    true,
                    IsRelationshipOverlaySuppressed(localPlayer, localCharacterId, ownerCharacterId: 0)));
            }

            IReadOnlyDictionary<int, PortableChairPairRecord> pairRecords = ResolvePortableChairPairRecords(
                participants,
                BuildPortableChairPairRecords(localPlayer),
                preferVisibleOnly);
            SyncResolvedPortableChairPairRecords(pairRecords);

            Dictionary<int, int> pairMap = new();
            foreach ((int characterId, PortableChairPairRecord record) in pairRecords)
            {
                if (!record.IsActive || !record.PairCharacterId.HasValue || pairMap.ContainsKey(characterId))
                {
                    continue;
                }

                pairMap[characterId] = record.PairCharacterId.Value;
            }

            return pairMap;
        }

        private IEnumerable<PortableChairPairRecord> BuildPortableChairPairRecords(PlayerCharacter localPlayer)
        {
            foreach (PortableChairPairRecord record in _portableChairPairRecordsByCharacterId.Values)
            {
                yield return record with
                {
                    PreferredPairCharacterId = ResolvePortableChairPairPreference(record.CharacterId) ?? record.PreferredPairCharacterId
                };
            }

            PortableChair localChair = localPlayer?.Build?.ActivePortableChair;
            int localCharacterId = localPlayer?.Build?.Id ?? 0;
            if (localChair?.IsCoupleChair == true && localCharacterId > 0)
            {
                yield return new PortableChairPairRecord(
                    localCharacterId,
                    localChair.ItemId,
                    _localPortableChairPreferredPairCharacterId);
            }
        }

        private int? ResolvePortableChairPairPreference(int characterId)
        {
            if (characterId <= 0)
            {
                return null;
            }

            if (_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                return actor.PreferredPortableChairPairCharacterId;
            }

            return null;
        }

        private void SyncPortableChairPairRecord(int characterId, int chairItemId, int? preferredPairCharacterId)
        {
            if (characterId <= 0 || chairItemId <= 0)
            {
                ClearPortableChairPairRecord(characterId);
                return;
            }

            _portableChairPairRecordsByCharacterId[characterId] = new PortableChairPairRecord(
                characterId,
                chairItemId,
                preferredPairCharacterId);
        }

        private void SyncPortableChairRecordFromChairState(int characterId, int? chairItemId, int? preferredPairCharacterId)
        {
            PortableChairPairRecord? record = ResolvePortableChairPairRecordFromChairState(
                characterId,
                chairItemId,
                preferredPairCharacterId);
            if (!record.HasValue)
            {
                ClearPortableChairPairRecord(characterId);
                return;
            }

            _portableChairPairRecordsByCharacterId[characterId] = record.Value;
        }

        internal static PortableChairPairRecord? ResolvePortableChairPairRecordFromChairState(
            int characterId,
            int? chairItemId,
            int? preferredPairCharacterId)
        {
            int resolvedChairItemId = chairItemId ?? 0;
            if (characterId <= 0 || resolvedChairItemId <= 0 || resolvedChairItemId / 1000 != 3012)
            {
                return null;
            }

            return new PortableChairPairRecord(
                characterId,
                resolvedChairItemId,
                preferredPairCharacterId);
        }

        private void ClearPortableChairPairRecord(int characterId)
        {
            if (characterId > 0)
            {
                _portableChairPairRecordsByCharacterId.Remove(characterId);
            }
        }

        private void SyncResolvedPortableChairPairRecords(IReadOnlyDictionary<int, PortableChairPairRecord> pairRecords)
        {
            if (pairRecords == null)
            {
                return;
            }

            foreach ((int characterId, PortableChairPairRecord record) in pairRecords)
            {
                if (!_portableChairPairRecordsByCharacterId.TryGetValue(characterId, out PortableChairPairRecord existingRecord))
                {
                    continue;
                }

                _portableChairPairRecordsByCharacterId[characterId] = existingRecord with
                {
                    PairCharacterId = record.PairCharacterId,
                    Status = record.Status
                };
            }
        }

        private static bool TryBuildPortableChairPairCandidate(
            PortableChairPairParticipant left,
            PortableChairPairParticipant right,
            bool preferVisibleOnly,
            out PortableChairPairCandidate candidate)
        {
            candidate = default;
            if (left.CharacterId == right.CharacterId
                || left.Chair?.IsCoupleChair != true
                || right.Chair?.IsCoupleChair != true
                || left.Chair.ItemId <= 0
                || left.Chair.ItemId != right.Chair.ItemId
                || !left.IsChairSessionActive
                || !right.IsChairSessionActive
                || (preferVisibleOnly && (!left.IsVisibleInWorld || !right.IsVisibleInWorld))
                || left.IsRelationshipOverlaySuppressed
                || right.IsRelationshipOverlaySuppressed
                || !PlayerCharacter.IsPortableChairActualPairActive(
                    left.Chair,
                    left.FacingRight,
                    left.Position.X,
                    left.Position.Y,
                    right.FacingRight,
                    right.Position.X,
                    right.Position.Y))
            {
                return false;
            }

            int priority = ResolvePortableChairPairPriority(left, right);
            float score = ComputePortableChairPairScore(left, right);
            candidate = new PortableChairPairCandidate(left, right, priority, score);
            return true;
        }

        private static int ResolvePortableChairPairPriority(
            PortableChairPairParticipant left,
            PortableChairPairParticipant right)
        {
            bool leftPrefersRight = left.PreferredPairCharacterId == right.CharacterId;
            bool rightPrefersLeft = right.PreferredPairCharacterId == left.CharacterId;
            return (leftPrefersRight, rightPrefersLeft) switch
            {
                (true, true) => 0,
                (true, false) => 1,
                (false, true) => 1,
                _ => 2
            };
        }

        private static int ResolvePortableChairPairStatus(
            PortableChairPairParticipant left,
            PortableChairPairParticipant right)
        {
            return PlayerCharacter.IsPortableChairActualPairActive(
                left.Chair,
                left.FacingRight,
                left.Position.X,
                left.Position.Y,
                right.FacingRight,
                right.Position.X,
                right.Position.Y)
                ? 3
                : 0;
        }

        private static float ComputePortableChairPairScore(
            PortableChairPairParticipant left,
            PortableChairPairParticipant right)
        {
            Point expectedRightOffset = PlayerCharacter.ResolvePortableChairPairOffset(left.Chair, left.FacingRight);
            Point expectedLeftOffset = PlayerCharacter.ResolvePortableChairPairOffset(right.Chair, right.FacingRight);
            Vector2 expectedRightPosition = left.Position + new Vector2(expectedRightOffset.X, expectedRightOffset.Y);
            Vector2 expectedLeftPosition = right.Position + new Vector2(expectedLeftOffset.X, expectedLeftOffset.Y);
            return Vector2.DistanceSquared(expectedRightPosition, right.Position)
                + Vector2.DistanceSquared(expectedLeftPosition, left.Position);
        }

        private static bool IsPortableChairPairSessionActive(PortableChair chair, string actionName)
        {
            return chair?.IsCoupleChair == true
                   && !string.IsNullOrWhiteSpace(actionName)
                   && actionName.StartsWith("sit", StringComparison.OrdinalIgnoreCase);
        }

        private bool TryResolvePortableChairPairWithLocalPlayer(
            RemoteUserActor actor,
            PortableChair chair,
            PlayerCharacter localPlayer,
            out Vector2 partnerPosition,
            out bool partnerFacingRight)
        {
            partnerPosition = Vector2.Zero;
            partnerFacingRight = false;
            if (actor == null
                || chair?.IsCoupleChair != true
                || localPlayer?.Build == null
                || !localPlayer.IsAlive)
            {
                return false;
            }

            Dictionary<int, int> pairMap = BuildPortableChairPairMap(localPlayer, preferVisibleOnly: true);
            int localCharacterId = localPlayer.Build.Id;
            if (!pairMap.TryGetValue(actor.CharacterId, out int pairCharacterId)
                || pairCharacterId != localCharacterId)
            {
                return false;
            }

            partnerPosition = localPlayer.Position;
            partnerFacingRight = localPlayer.FacingRight;
            return true;
        }

        private bool TryResolvePortableChairOwnerForLocalPlayer(PlayerCharacter localPlayer, out RemoteUserActor ownerActor)
        {
            return TryResolvePortableChairOwnerForLocalPlayer(
                localPlayer,
                BuildPortableChairPairMap(localPlayer, preferVisibleOnly: true),
                out ownerActor);
        }

        private bool TryResolvePortableChairOwnerForLocalPlayer(
            PlayerCharacter localPlayer,
            IReadOnlyDictionary<int, int> pairMap,
            out RemoteUserActor ownerActor)
        {
            ownerActor = null;
            if (localPlayer?.Build == null || !localPlayer.IsAlive)
            {
                return false;
            }

            int localCharacterId = localPlayer.Build.Id;
            if (localCharacterId <= 0)
            {
                return false;
            }

            return pairMap != null
                   && pairMap.TryGetValue(localCharacterId, out int ownerCharacterId)
                   && ownerCharacterId != localCharacterId
                   && _actorsById.TryGetValue(ownerCharacterId, out ownerActor)
                   && ownerActor.IsVisibleInWorld;
        }

        private RemoteUserActor FindPortableChairPairActor(
            PortableChair chair,
            int ownerCharacterId,
            bool ownerFacingRight,
            float ownerX,
            float ownerY,
            int skipCharacterId,
            bool preferVisibleOnly)
        {
            if (chair?.IsCoupleChair != true)
            {
                return null;
            }

            Dictionary<int, int> pairMap = BuildPortableChairPairMap(localPlayer: null, preferVisibleOnly);
            return pairMap.TryGetValue(ownerCharacterId, out int pairCharacterId)
                   && pairCharacterId != skipCharacterId
                   && _actorsById.TryGetValue(pairCharacterId, out RemoteUserActor pairActor)
                ? pairActor
                : null;
        }

        private void SyncBattlefieldAppearance(RemoteUserActor actor, BattlefieldField battlefield)
        {
            if (actor?.Build == null)
            {
                return;
            }

            int? assignedTeamId = battlefield != null
                && battlefield.TryGetAssignedTeamId(actor.CharacterId, out int resolvedTeamId)
                ? resolvedTeamId
                : actor.BattlefieldTeamId;
            if (battlefield?.IsActive != true)
            {
                RestoreBattlefieldAppearance(actor, clearTeamId: false);
                return;
            }

            if (actor.BattlefieldAppliedTeamId == assignedTeamId
                && (!assignedTeamId.HasValue || actor.BattlefieldOriginalEquipment != null))
            {
                return;
            }

            if (!assignedTeamId.HasValue
                || !battlefield.TryGetAssignedTeamLookPreset(actor.CharacterId, out BattlefieldField.BattlefieldTeamLookPreset preset))
            {
                RestoreBattlefieldAppearance(actor, clearTeamId: false);
                actor.BattlefieldAppliedTeamId = assignedTeamId;
                actor.BattlefieldTeamId = assignedTeamId;
                return;
            }

            EnsureBattlefieldOriginalAppearanceSnapshot(actor);
            if (actor.BattlefieldOriginalSpeed == null)
            {
                actor.BattlefieldOriginalSpeed = actor.Build.Speed;
            }

            foreach (EquipSlot slot in BattlefieldAppearanceSlots)
            {
                actor.Build.Unequip(slot);
            }

            if (preset.EquipmentItemIds.ContainsKey(EquipSlot.Longcoat))
            {
                actor.Build.Unequip(EquipSlot.Coat);
                actor.Build.Unequip(EquipSlot.Pants);
            }
            else if (preset.EquipmentItemIds.ContainsKey(EquipSlot.Coat))
            {
                actor.Build.Unequip(EquipSlot.Longcoat);
            }

            foreach (KeyValuePair<EquipSlot, int> entry in preset.EquipmentItemIds)
            {
                CharacterPart part = _loader?.LoadEquipment(entry.Value);
                if (part != null)
                {
                    actor.Build.Equip(part);
                }
            }

            if (preset.MoveSpeed.HasValue)
            {
                actor.Build.Speed = preset.MoveSpeed.Value;
            }

            actor.RefreshAssembler();
            actor.BattlefieldAppliedTeamId = assignedTeamId;
            actor.BattlefieldTeamId = assignedTeamId;
        }

        private static void EnsureBattlefieldOriginalAppearanceSnapshot(RemoteUserActor actor)
        {
            if (actor.BattlefieldOriginalEquipment != null || actor?.Build == null)
            {
                return;
            }

            actor.BattlefieldOriginalEquipment = new Dictionary<EquipSlot, CharacterPart>();
            actor.BattlefieldOriginalSpeed = actor.Build.Speed;
            foreach (EquipSlot slot in BattlefieldAppearanceSlots)
            {
                if (actor.Build.Equipment.TryGetValue(slot, out CharacterPart part) && part != null)
                {
                    actor.BattlefieldOriginalEquipment[slot] = part;
                }
            }
        }

        private static void RestoreBattlefieldAppearance(RemoteUserActor actor, bool clearTeamId)
        {
            if (actor?.Build == null)
            {
                return;
            }

            if (actor.BattlefieldOriginalEquipment == null)
            {
                actor.BattlefieldAppliedTeamId = null;
                if (clearTeamId)
                {
                    actor.BattlefieldTeamId = null;
                }
                return;
            }

            foreach (EquipSlot slot in BattlefieldAppearanceSlots)
            {
                actor.Build.Unequip(slot);
            }

            foreach (KeyValuePair<EquipSlot, CharacterPart> entry in actor.BattlefieldOriginalEquipment)
            {
                actor.Build.Equip(entry.Value);
            }

            if (actor.BattlefieldOriginalSpeed.HasValue)
            {
                actor.Build.Speed = actor.BattlefieldOriginalSpeed.Value;
            }

            actor.RefreshAssembler();
            ResetBattlefieldAppearanceState(actor);
            if (clearTeamId)
            {
                actor.BattlefieldTeamId = null;
            }
        }

        private static void ResetBattlefieldAppearanceState(RemoteUserActor actor)
        {
            if (actor == null)
            {
                return;
            }

            actor.BattlefieldOriginalEquipment = null;
            actor.BattlefieldOriginalSpeed = null;
            actor.BattlefieldAppliedTeamId = null;
        }

        private static string NormalizeActionName(string actionName, bool allowSitFallback)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return allowSitFallback
                    ? "sit"
                    : CharacterPart.GetActionString(CharacterAction.Stand1);
            }

            return actionName.Trim();
        }

        private void SyncTemporaryStatPresentation(RemoteUserActor actor)
        {
            if (actor == null)
            {
                return;
            }

            RemoteUserTemporaryStatKnownState knownState = actor.TemporaryStats.KnownState;
            CharacterPart overrideAvatarPart = null;
            CharacterPart overrideTamingMobPart = null;
            if (knownState.MorphId is int morphTemplateId && morphTemplateId > 0)
            {
                overrideAvatarPart = _loader?.LoadMorph(morphTemplateId);
                if (overrideAvatarPart?.Type != CharacterPartType.Morph)
                {
                    overrideAvatarPart = actor.TemporaryStatAvatarOverridePart?.Type == CharacterPartType.Morph
                        && actor.TemporaryStatAvatarOverridePart.ItemId == morphTemplateId
                        ? actor.TemporaryStatAvatarOverridePart
                        : null;
                }
            }

            if (TryResolveMechanicTamingMobOverrideItemId(knownState, actor.BaseActionName, out int mechanicTamingMobItemId))
            {
                overrideTamingMobPart = _loader?.LoadEquipment(mechanicTamingMobItemId);
                if (overrideTamingMobPart?.Slot != EquipSlot.TamingMob)
                {
                    overrideTamingMobPart = actor.TemporaryStatTamingMobOverridePart?.Slot == EquipSlot.TamingMob
                        && actor.TemporaryStatTamingMobOverridePart.ItemId == mechanicTamingMobItemId
                        ? actor.TemporaryStatTamingMobOverridePart
                        : null;
                }
            }

            actor.TemporaryStatAvatarOverridePart = overrideAvatarPart;
            actor.TemporaryStatTamingMobOverridePart = overrideTamingMobPart;
            int? previousShadowPartnerSkillId = actor.TemporaryStatShadowPartnerSkillId;
            ResolveRemoteShadowPartnerSkill(actor, knownState, out int? shadowPartnerSkillId, out SkillData shadowPartnerSkill);
            actor.TemporaryStatShadowPartnerSkillId = shadowPartnerSkillId;
            actor.TemporaryStatShadowPartnerSkill = shadowPartnerSkill;
            if (previousShadowPartnerSkillId != shadowPartnerSkillId)
            {
                actor.ShadowPartnerPresentation = null;
            }
            ResolveRemoteSoulArrowSkill(actor, knownState, out int? soulArrowSkillId, out SkillData soulArrowSkill);
            actor.TemporaryStatSoulArrowEffect = UpdateRemoteTemporaryStatAvatarEffectState(
                actor.TemporaryStatSoulArrowEffect,
                soulArrowSkillId,
                soulArrowSkill,
                Environment.TickCount);
            ResolveRemoteWeaponChargeSkill(actor, knownState, out int? weaponChargeSkillId, out SkillData weaponChargeSkill);
            actor.TemporaryStatWeaponChargeEffect = UpdateRemoteTemporaryStatAvatarEffectState(
                actor.TemporaryStatWeaponChargeEffect,
                weaponChargeSkillId,
                weaponChargeSkill,
                Environment.TickCount);
            ResolveRemoteAuraSkill(actor, knownState, out int? auraSkillId, out SkillData auraSkill);
            actor.TemporaryStatAuraEffect = UpdateRemoteTemporaryStatAvatarEffectState(
                actor.TemporaryStatAuraEffect,
                auraSkillId,
                auraSkill,
                Environment.TickCount);
            ResolveRemoteBarrierSkill(actor, knownState, out int? barrierSkillId, out SkillData barrierSkill);
            actor.TemporaryStatBarrierEffect = UpdateRemoteTemporaryStatAvatarEffectState(
                actor.TemporaryStatBarrierEffect,
                barrierSkillId,
                barrierSkill,
                Environment.TickCount);
            ResolveRemoteBlessingArmorSkill(actor, knownState, out int? blessingArmorSkillId, out SkillData blessingArmorSkill);
            actor.TemporaryStatBlessingArmorEffect = UpdateRemoteTemporaryStatAvatarEffectState(
                actor.TemporaryStatBlessingArmorEffect,
                blessingArmorSkillId,
                blessingArmorSkill,
                Environment.TickCount);
            ResolveRemoteRepeatEffectSkill(actor, knownState, out int? repeatEffectSkillId, out SkillData repeatEffectSkill);
            actor.TemporaryStatRepeatEffect = UpdateRemoteTemporaryStatAvatarEffectState(
                actor.TemporaryStatRepeatEffect,
                repeatEffectSkillId,
                repeatEffectSkill,
                Environment.TickCount);
            ResolveRemoteMagicShieldSkill(actor, knownState, out int? magicShieldSkillId, out SkillData magicShieldSkill);
            actor.TemporaryStatMagicShieldEffect = UpdateRemoteTemporaryStatAvatarEffectState(
                actor.TemporaryStatMagicShieldEffect,
                magicShieldSkillId,
                magicShieldSkill,
                Environment.TickCount);
            ResolveRemoteFinalCutSkill(actor, knownState, out int? finalCutSkillId, out SkillData finalCutSkill);
            actor.TemporaryStatFinalCutEffect = UpdateRemoteTemporaryStatAvatarEffectState(
                actor.TemporaryStatFinalCutEffect,
                finalCutSkillId,
                finalCutSkill,
                Environment.TickCount);
            actor.HasMorphTemplate = overrideAvatarPart?.Type == CharacterPartType.Morph;
            actor.HiddenLikeClient = knownState.IsHiddenLikeClient;
            actor.ActionName = ResolveClientVisibleActionName(actor.BaseActionName, knownState);
            actor.RidingVehicleId = ResolveRemoteRidingVehicleId(actor);
            actor.RefreshAssembler();
        }

        private static void SetActorAction(
            RemoteUserActor actor,
            string actionName,
            bool allowSitFallback,
            int currentTime = int.MinValue,
            bool forceReplay = false,
            int? rawActionCode = null)
        {
            if (actor == null)
            {
                return;
            }

            string normalizedActionName = ResolvePortableChairVisibleActionName(
                actor,
                NormalizeActionName(actionName, allowSitFallback),
                allowSitFallback);
            bool baseActionChanged = !string.Equals(actor.BaseActionName, normalizedActionName, StringComparison.OrdinalIgnoreCase);
            bool rawActionChanged = actor.BaseActionRawCode != rawActionCode;

            actor.BaseActionName = normalizedActionName;
            actor.BaseActionRawCode = rawActionCode;
            if (currentTime != int.MinValue && (forceReplay || baseActionChanged || rawActionChanged))
            {
                actor.BaseActionStartTime = currentTime;
            }

            actor.ActionName = ResolveClientVisibleActionName(actor.BaseActionName, actor.TemporaryStats.KnownState);
            actor.RidingVehicleId = ResolveRemoteRidingVehicleId(actor);
        }

        private static string ResolvePortableChairVisibleActionName(
            RemoteUserActor actor,
            string actionName,
            bool allowSitFallback)
        {
            PortableChair chair = actor?.Build?.ActivePortableChair;
            if (!allowSitFallback
                || chair == null
                || !string.Equals(actionName, CharacterPart.GetActionString(CharacterAction.Sit), StringComparison.OrdinalIgnoreCase))
            {
                return actionName;
            }

            return PlayerCharacter.ResolvePortableChairActionName(chair);
        }

        private static int ResolveRemoteRidingVehicleId(RemoteUserActor actor)
        {
            if (actor == null)
            {
                return 0;
            }

            CharacterPart mountPart = null;
            actor.Build?.Equipment?.TryGetValue(EquipSlot.TamingMob, out mountPart);
            return FollowCharacterEligibilityResolver.ResolveMountedVehicleId(
                mountPart,
                actor.ActionName,
                actor.TemporaryStats.KnownState.MechanicMode);
        }

        internal static string ResolveClientVisibleActionName(
            string baseActionName,
            RemoteUserTemporaryStatKnownState knownState)
        {
            string normalized = NormalizeActionName(baseActionName, allowSitFallback: false);
            if (TryResolveMechanicVisibleActionName(normalized, knownState.MechanicMode, out string mechanicActionName)
                && !knownState.IsHiddenLikeClient)
            {
                return mechanicActionName;
            }

            if (!knownState.IsHiddenLikeClient)
            {
                return normalized;
            }

            if (string.IsNullOrWhiteSpace(normalized)
                || normalized.StartsWith("ghost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "darksight", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "dead", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            return normalized switch
            {
                "stand1" or "stand2" or "alert" => "ghoststand",
                "sit" => "ghostsit",
                "walk1" or "walk2" => "ghostwalk",
                "jump" => "ghostjump",
                "ladder" => "ghostladder",
                "rope" => "ghostrope",
                "swim" or "fly" => "ghostfly",
                "prone" or "proneStab" => "ghostproneStab",
                _ when IsHiddenLikeAttackAction(normalized) => "ghoststand",
                _ => normalized
            };
        }

        internal static bool TryResolveMechanicTamingMobOverrideItemId(
            RemoteUserTemporaryStatKnownState knownState,
            string baseActionName,
            out int tamingMobItemId)
        {
            tamingMobItemId = 0;
            if (!ClientOwnedVehicleSkillClassifier.IsExplicitMechanicVehiclePresentationSkillId(knownState.MechanicMode))
            {
                return false;
            }

            if (!ClientOwnedVehicleSkillClassifier.SupportsExplicitMechanicVehiclePresentationCurrentAction(baseActionName))
            {
                return false;
            }

            tamingMobItemId = MechanicTamingMobItemId;
            return true;
        }

        internal static bool TryResolveMechanicVisibleActionName(
            string baseActionName,
            int? mechanicMode,
            out string actionName)
        {
            actionName = null;
            string normalized = NormalizeActionName(baseActionName, allowSitFallback: false);
            if (string.IsNullOrWhiteSpace(normalized)
                || !TryResolveRemoteMechanicModePresentation(mechanicMode, out RemoteMechanicModePresentation presentation))
            {
                return false;
            }

            if (ClientOwnedVehicleSkillClassifier.IsMechanicVehicleActionName(normalized, includeTransformStates: true))
            {
                actionName = normalized;
                return true;
            }

            if (ClientOwnedVehicleSkillClassifier.IsExplicitMechanicVehiclePresentationActionName(normalized))
            {
                actionName = normalized;
                return true;
            }

            actionName = normalized switch
            {
                "stand1" or "stand2" or "alert" or "sit" => presentation.StandActionName,
                "walk1" or "walk2" => presentation.WalkActionName,
                "jump" => presentation.StandActionName,
                "ladder" => "ladder2",
                "rope" => "rope2",
                "swim" => presentation.StandActionName,
                "fly" => presentation.StandActionName,
                "prone" or "proneStab" => presentation.ProneActionName,
                "hit" => presentation.StandActionName,
                _ when IsHiddenLikeAttackAction(normalized) => presentation.AttackActionName,
                _ => normalized
            };

            if (string.Equals(normalized, "ladder", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "rope", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return true;
        }

        private static bool TryResolveRemoteMechanicModePresentation(
            int? mechanicMode,
            out RemoteMechanicModePresentation presentation)
        {
            if (mechanicMode.HasValue
                && RemoteMechanicModePresentationBySkillId.TryGetValue(mechanicMode.Value, out presentation))
            {
                return true;
            }

            presentation = default;
            return false;
        }

        private static bool IsHiddenLikeAttackAction(string actionName)
        {
            return actionName.StartsWith("attack", StringComparison.OrdinalIgnoreCase)
                || actionName.StartsWith("stab", StringComparison.OrdinalIgnoreCase)
                || actionName.StartsWith("swing", StringComparison.OrdinalIgnoreCase)
                || actionName.StartsWith("shoot", StringComparison.OrdinalIgnoreCase)
                || actionName.StartsWith("shot", StringComparison.OrdinalIgnoreCase)
                || actionName.StartsWith("magic", StringComparison.OrdinalIgnoreCase);
        }

        internal static IReadOnlyList<int> EnumerateRemoteShadowPartnerSkillIds(int jobId)
        {
            int preferredSkillId = jobId switch
            {
                >= 1410 and <= 1412 => 14111000,
                >= 420 and <= 422 => 4211008,
                >= 410 and <= 412 => 4111002,
                _ => 0
            };

            var orderedSkillIds = new List<int>(RemoteShadowPartnerSkillIds.Length);
            if (preferredSkillId > 0)
            {
                orderedSkillIds.Add(preferredSkillId);
            }

            for (int i = 0; i < RemoteShadowPartnerSkillIds.Length; i++)
            {
                int skillId = RemoteShadowPartnerSkillIds[i];
                if (skillId != preferredSkillId)
                {
                    orderedSkillIds.Add(skillId);
                }
            }

            return orderedSkillIds;
        }

        internal static string ResolveRemoteShadowPartnerActionName(
            IReadOnlyDictionary<string, SkillAnimation> actionAnimations,
            string actionName,
            PlayerState state,
            string fallbackActionName = null,
            string weaponType = null,
            int? rawActionCode = null,
            IReadOnlySet<string> supportedRawActionNames = null)
        {
            if (actionAnimations == null || actionAnimations.Count == 0)
            {
                return null;
            }

            foreach (string candidate in ShadowPartnerClientActionResolver.EnumerateClientMappedCandidates(
                         actionName,
                         state,
                         fallbackActionName,
                         weaponType,
                         rawActionCode,
                         supportedRawActionNames))
            {
                if (!string.IsNullOrWhiteSpace(candidate)
                    && actionAnimations.ContainsKey(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static SkillAnimation ResolveRemoteShadowPartnerPlaybackAnimation(
            IReadOnlyDictionary<string, SkillAnimation> actionAnimations,
            string resolvedActionName,
            string actionName,
            int rawActionCode,
            IReadOnlySet<string> supportedRawActionNames = null)
        {
            string rawActionName = null;
            if (rawActionCode >= 0)
            {
                CharacterPart.TryGetActionStringFromCode(rawActionCode, out rawActionName);
            }

            return ShadowPartnerClientActionResolver.ResolvePlaybackAnimation(
                actionAnimations,
                resolvedActionName,
                actionName,
                rawActionName,
                supportedRawActionNames);
        }

        private void ResolveRemoteShadowPartnerSkill(
            RemoteUserActor actor,
            RemoteUserTemporaryStatKnownState knownState,
            out int? skillId,
            out SkillData skill)
        {
            skillId = null;
            skill = null;
            if (!knownState.HasShadowPartner || _skillLoader == null)
            {
                return;
            }

            int jobId = actor?.Build?.Job ?? 0;
            IReadOnlyList<int> candidateSkillIds = EnumerateRemoteShadowPartnerSkillIds(jobId);
            for (int i = 0; i < candidateSkillIds.Count; i++)
            {
                int candidateSkillId = candidateSkillIds[i];
                SkillData candidateSkill = _skillLoader.LoadSkill(candidateSkillId);
                if (candidateSkill?.HasShadowPartnerActionAnimations != true)
                {
                    continue;
                }

                skillId = candidateSkillId;
                skill = candidateSkill;
                return;
            }
        }

        internal static IReadOnlyList<int> EnumerateRemoteSoulArrowSkillIds(int jobId, bool preferSpiritJavelin)
        {
            int preferredSkillId = preferSpiritJavelin
                ? 4121006
                : jobId switch
            {
                >= 3310 and <= 3312 => 33101003,
                >= 1310 and <= 1312 => 13101003,
                >= 320 and <= 322 => 3201004,
                >= 310 and <= 312 => 3101004,
                >= 410 and <= 412 => 4121006,
                _ => 0
            };

            return EnumeratePreferredSkillIds(RemoteSoulArrowSkillIds, preferredSkillId);
        }

        internal static IReadOnlyList<int> EnumerateRemoteWeaponChargeSkillIds(int jobId, int? preferredSkillId)
        {
            int resolvedPreferredSkillId = preferredSkillId.GetValueOrDefault();
            if (resolvedPreferredSkillId <= 0 || !AfterImageChargeSkillResolver.IsKnownChargeSkillId(resolvedPreferredSkillId))
            {
                resolvedPreferredSkillId = jobId switch
                {
                    >= 1210 and <= 1212 => 1211004,
                    >= 1220 and <= 1222 => 1221004,
                    >= 1510 and <= 1512 => 15101006,
                    >= 2110 and <= 2112 => 21111005,
                    _ => 0
                };
            }

            return EnumeratePreferredSkillIds(RemoteWeaponChargeSkillIds, resolvedPreferredSkillId);
        }

        internal static IReadOnlyList<int> EnumerateRemoteBarrierSkillIds(int jobId)
        {
            int preferredSkillId = jobId switch
            {
                >= 2110 and <= 2112 => 21120007,
                >= 2310 and <= 2312 => 23111005,
                >= 2200 and <= 2218 => 20011010,
                >= 2100 and <= 2109 => 20001010,
                >= 1000 and <= 1512 => 10001010,
                2001 => 20011010,
                2000 => 20001010,
                _ => 0
            };

            return EnumeratePreferredSkillIds(RemoteBarrierSkillIds, preferredSkillId);
        }

        internal static IReadOnlyList<int> EnumerateRemoteAuraSkillIds(int jobId, int? preferredSkillId)
        {
            int resolvedPreferredSkillId = preferredSkillId.GetValueOrDefault();
            if (resolvedPreferredSkillId <= 0)
            {
                resolvedPreferredSkillId = jobId switch
                {
                    >= 3210 and <= 3212 => RemoteUserTemporaryStatKnownState.BlueAuraSkillId,
                    >= 3200 and <= 3202 => RemoteUserTemporaryStatKnownState.DarkAuraSkillId,
                    _ => 0
                };
            }

            return EnumeratePreferredSkillIds(RemoteAuraSkillIds, resolvedPreferredSkillId);
        }

        internal static IReadOnlyList<int> EnumerateRemoteBlessingArmorSkillIds(int jobId, int? preferredSkillId)
        {
            int resolvedPreferredSkillId = preferredSkillId.GetValueOrDefault();
            if (resolvedPreferredSkillId <= 0)
            {
                resolvedPreferredSkillId = jobId switch
                {
                    >= 1220 and <= 1222 => RemoteUserTemporaryStatKnownState.PaladinBlessingArmorSkillId,
                    >= 2310 and <= 2312 => RemoteUserTemporaryStatKnownState.BishopBlessingArmorSkillId,
                    _ => 0
                };
            }

            return EnumeratePreferredSkillIds(RemoteBlessingArmorSkillIds, resolvedPreferredSkillId);
        }

        private void ResolveRemoteSoulArrowSkill(
            RemoteUserActor actor,
            RemoteUserTemporaryStatKnownState knownState,
            out int? skillId,
            out SkillData skill)
        {
            skillId = null;
            skill = null;
            if (!knownState.HasSoulArrow || _skillLoader == null)
            {
                return;
            }

            foreach (int candidateSkillId in EnumerateRemoteSoulArrowSkillIds(actor?.Build?.Job ?? 0, knownState.HasSpiritJavelin))
            {
                SkillData candidateSkill = _skillLoader.LoadSkill(candidateSkillId);
                if (!HasRemoteTemporaryStatAvatarEffect(candidateSkill))
                {
                    continue;
                }

                skillId = candidateSkillId;
                skill = candidateSkill;
                return;
            }
        }

        private void ResolveRemoteWeaponChargeSkill(
            RemoteUserActor actor,
            RemoteUserTemporaryStatKnownState knownState,
            out int? skillId,
            out SkillData skill)
        {
            skillId = null;
            skill = null;
            if (_skillLoader == null)
            {
                return;
            }

            int? resolvedChargeSkillId = ResolveChargeSkillIdFromTemporaryStats(
                actor?.TemporaryStats ?? default,
                ResolvePreferredRemoteWeaponChargeSkillId(actor?.Build?.Job ?? 0));

            if (!resolvedChargeSkillId.HasValue)
            {
                return;
            }

            foreach (int candidateSkillId in EnumerateRemoteWeaponChargeSkillIds(actor?.Build?.Job ?? 0, resolvedChargeSkillId))
            {
                SkillData candidateSkill = _skillLoader.LoadSkill(candidateSkillId);
                if (!HasRemoteTemporaryStatAvatarEffect(candidateSkill))
                {
                    continue;
                }

                skillId = candidateSkillId;
                skill = candidateSkill;
                return;
            }
        }

        private void ResolveRemoteBarrierSkill(
            RemoteUserActor actor,
            RemoteUserTemporaryStatKnownState knownState,
            out int? skillId,
            out SkillData skill)
        {
            skillId = null;
            skill = null;
            if (!knownState.HasBarrier || _skillLoader == null)
            {
                return;
            }

            foreach (int candidateSkillId in EnumerateRemoteBarrierSkillIds(actor?.Build?.Job ?? 0))
            {
                SkillData candidateSkill = _skillLoader.LoadSkill(candidateSkillId);
                if (!HasRemoteTemporaryStatAvatarEffect(candidateSkill))
                {
                    continue;
                }

                skillId = candidateSkillId;
                skill = candidateSkill;
                return;
            }
        }

        private void ResolveRemoteAuraSkill(
            RemoteUserActor actor,
            RemoteUserTemporaryStatKnownState knownState,
            out int? skillId,
            out SkillData skill)
        {
            skillId = null;
            skill = null;
            if (!knownState.ActiveAuraSkillId.HasValue || _skillLoader == null)
            {
                return;
            }

            foreach (int candidateSkillId in EnumerateRemoteAuraSkillIds(actor?.Build?.Job ?? 0, knownState.ActiveAuraSkillId))
            {
                SkillData candidateSkill = _skillLoader.LoadSkill(candidateSkillId);
                if (!HasRemoteTemporaryStatAvatarEffect(candidateSkill))
                {
                    continue;
                }

                skillId = candidateSkillId;
                skill = candidateSkill;
                return;
            }
        }

        private void ResolveRemoteBlessingArmorSkill(
            RemoteUserActor actor,
            RemoteUserTemporaryStatKnownState knownState,
            out int? skillId,
            out SkillData skill)
        {
            skillId = null;
            skill = null;
            if (_skillLoader == null)
            {
                return;
            }

            int? preferredSkillId = knownState.ResolveBlessingArmorSkillId(actor?.Build?.Job ?? 0);
            if (!preferredSkillId.HasValue || preferredSkillId.Value <= 0)
            {
                return;
            }

            foreach (int candidateSkillId in EnumerateRemoteBlessingArmorSkillIds(actor?.Build?.Job ?? 0, preferredSkillId))
            {
                SkillData candidateSkill = _skillLoader.LoadSkill(candidateSkillId);
                if (!HasRemoteTemporaryStatAvatarEffect(candidateSkill))
                {
                    continue;
                }

                skillId = candidateSkillId;
                skill = candidateSkill;
                return;
            }
        }

        private void ResolveRemoteRepeatEffectSkill(
            RemoteUserActor actor,
            RemoteUserTemporaryStatKnownState knownState,
            out int? skillId,
            out SkillData skill)
        {
            ResolveRemotePayloadDrivenEffectSkill(knownState.RepeatEffectSkillId, out skillId, out skill);
        }

        private void ResolveRemoteMagicShieldSkill(
            RemoteUserActor actor,
            RemoteUserTemporaryStatKnownState knownState,
            out int? skillId,
            out SkillData skill)
        {
            ResolveRemotePayloadDrivenEffectSkill(knownState.MagicShieldSkillId, out skillId, out skill);
        }

        private void ResolveRemoteFinalCutSkill(
            RemoteUserActor actor,
            RemoteUserTemporaryStatKnownState knownState,
            out int? skillId,
            out SkillData skill)
        {
            ResolveRemotePayloadDrivenEffectSkill(knownState.FinalCutSkillId, out skillId, out skill);
        }

        private void ResolveRemotePayloadDrivenEffectSkill(
            int? payloadSkillId,
            out int? skillId,
            out SkillData skill)
        {
            skillId = null;
            skill = null;
            if (_skillLoader == null
                || !payloadSkillId.HasValue
                || payloadSkillId.Value <= 0)
            {
                return;
            }

            SkillData candidateSkill = _skillLoader.LoadSkill(payloadSkillId.Value);
            if (!HasRemoteTemporaryStatAvatarEffect(candidateSkill))
            {
                return;
            }

            skillId = payloadSkillId.Value;
            skill = candidateSkill;
        }

        private static IReadOnlyList<int> EnumeratePreferredSkillIds(IReadOnlyList<int> skillIds, int preferredSkillId)
        {
            var orderedSkillIds = new List<int>(skillIds?.Count ?? 0);
            if (preferredSkillId > 0)
            {
                orderedSkillIds.Add(preferredSkillId);
            }

            if (skillIds == null)
            {
                return orderedSkillIds;
            }

            for (int i = 0; i < skillIds.Count; i++)
            {
                int skillId = skillIds[i];
                if (skillId != preferredSkillId)
                {
                    orderedSkillIds.Add(skillId);
                }
            }

            return orderedSkillIds;
        }

        private static bool HasRemoteTemporaryStatAvatarEffect(SkillData skill)
        {
            return skill?.AffectedEffect != null
                   || skill?.AffectedSecondaryEffect != null
                   || skill?.Effect != null
                   || skill?.EffectSecondary != null
                   || skill?.RepeatEffect != null
                   || skill?.RepeatSecondaryEffect != null;
        }

        private static RemoteTemporaryStatAvatarEffectState UpdateRemoteTemporaryStatAvatarEffectState(
            RemoteTemporaryStatAvatarEffectState existingState,
            int? skillId,
            SkillData skill,
            int currentTime)
        {
            if (!skillId.HasValue
                || skill == null
                || !TryCreateRemoteTemporaryStatAvatarEffectState(
                    skillId.Value,
                    skill,
                    existingState?.AnimationStartTime ?? currentTime,
                    out RemoteTemporaryStatAvatarEffectState nextState))
            {
                return null;
            }

            return existingState?.SkillId == skillId.Value
                ? new RemoteTemporaryStatAvatarEffectState
                {
                    SkillId = nextState.SkillId,
                    Skill = nextState.Skill,
                    OverlayAnimation = nextState.OverlayAnimation,
                    OverlaySecondaryAnimation = nextState.OverlaySecondaryAnimation,
                    UnderFaceAnimation = nextState.UnderFaceAnimation,
                    UnderFaceSecondaryAnimation = nextState.UnderFaceSecondaryAnimation,
                    AnimationStartTime = existingState.AnimationStartTime
                }
                : nextState;
        }

        internal static bool TryCreateRemoteTemporaryStatAvatarEffectState(
            int skillId,
            SkillData skill,
            int animationStartTime,
            out RemoteTemporaryStatAvatarEffectState state)
        {
            state = null;
            if (!HasRemoteTemporaryStatAvatarEffect(skill))
            {
                return false;
            }

            SkillAnimation overlayAnimation = null;
            SkillAnimation overlaySecondaryAnimation = null;
            SkillAnimation underFaceAnimation = null;
            SkillAnimation underFaceSecondaryAnimation = null;

            if (skill.AffectedEffect != null || skill.AffectedSecondaryEffect != null)
            {
                AssignRemoteTemporaryStatAvatarEffectPlane(
                    CreateLoopingRemoteTemporaryStatAvatarEffect(skill.AffectedEffect),
                    ref overlayAnimation,
                    ref overlaySecondaryAnimation,
                    ref underFaceAnimation,
                    ref underFaceSecondaryAnimation);
                AssignRemoteTemporaryStatAvatarEffectPlane(
                    CreateLoopingRemoteTemporaryStatAvatarEffect(skill.AffectedSecondaryEffect),
                    ref overlayAnimation,
                    ref overlaySecondaryAnimation,
                    ref underFaceAnimation,
                    ref underFaceSecondaryAnimation);
            }
            else
            {
                AssignRemoteTemporaryStatAvatarEffectPlane(
                    CreateLoopingRemoteTemporaryStatAvatarEffect(skill.Effect),
                    ref overlayAnimation,
                    ref overlaySecondaryAnimation,
                    ref underFaceAnimation,
                    ref underFaceSecondaryAnimation);
                AssignRemoteTemporaryStatAvatarEffectPlane(
                    CreateLoopingRemoteTemporaryStatAvatarEffect(skill.EffectSecondary),
                    ref overlayAnimation,
                    ref overlaySecondaryAnimation,
                    ref underFaceAnimation,
                    ref underFaceSecondaryAnimation);
                AssignRemoteTemporaryStatAvatarEffectPlane(
                    CreateLoopingRemoteTemporaryStatAvatarEffect(skill.RepeatEffect),
                    ref overlayAnimation,
                    ref overlaySecondaryAnimation,
                    ref underFaceAnimation,
                    ref underFaceSecondaryAnimation);
                AssignRemoteTemporaryStatAvatarEffectPlane(
                    CreateLoopingRemoteTemporaryStatAvatarEffect(skill.RepeatSecondaryEffect),
                    ref overlayAnimation,
                    ref overlaySecondaryAnimation,
                    ref underFaceAnimation,
                    ref underFaceSecondaryAnimation);
            }

            if (overlayAnimation == null
                && overlaySecondaryAnimation == null
                && underFaceAnimation == null
                && underFaceSecondaryAnimation == null)
            {
                return false;
            }

            state = new RemoteTemporaryStatAvatarEffectState
            {
                SkillId = skillId,
                Skill = skill,
                OverlayAnimation = overlayAnimation,
                OverlaySecondaryAnimation = overlaySecondaryAnimation,
                UnderFaceAnimation = underFaceAnimation,
                UnderFaceSecondaryAnimation = underFaceSecondaryAnimation,
                AnimationStartTime = animationStartTime
            };
            return true;
        }

        private static SkillAnimation CreateLoopingRemoteTemporaryStatAvatarEffect(SkillAnimation animation)
        {
            if (animation == null)
            {
                return null;
            }

            return new SkillAnimation
            {
                Name = animation.Name,
                Frames = new List<SkillFrame>(animation.Frames),
                Loop = true,
                Origin = animation.Origin,
                ZOrder = animation.ZOrder,
                PositionCode = animation.PositionCode
            };
        }

        private static void AssignRemoteTemporaryStatAvatarEffectPlane(
            SkillAnimation animation,
            ref SkillAnimation overlayAnimation,
            ref SkillAnimation overlaySecondaryAnimation,
            ref SkillAnimation underFaceAnimation,
            ref SkillAnimation underFaceSecondaryAnimation)
        {
            if (animation == null)
            {
                return;
            }

            if (ClientOwnedAvatarEffectParity.PrefersUnderFaceAvatarEffectPlane(animation))
            {
                underFaceAnimation ??= animation;
                underFaceSecondaryAnimation ??= animation == underFaceAnimation ? null : animation;
                return;
            }

            overlayAnimation ??= animation;
            overlaySecondaryAnimation ??= animation == overlayAnimation ? null : animation;
        }

        private static PlayerState ResolveRemoteShadowPartnerState(string actionName)
        {
            string normalized = NormalizeActionName(actionName, allowSitFallback: false);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return PlayerState.Standing;
            }

            if (normalized.StartsWith("walk", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Walking;
            }

            if (string.Equals(normalized, "jump", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Jumping;
            }

            if (string.Equals(normalized, "ladder", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Ladder;
            }

            if (string.Equals(normalized, "rope", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Rope;
            }

            if (string.Equals(normalized, "sit", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Sitting;
            }

            if (string.Equals(normalized, "prone", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "proneStab", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Prone;
            }

            if (string.Equals(normalized, "swim", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Swimming;
            }

            if (string.Equals(normalized, "fly", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "fly2", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "fly2Move", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "fly2Skill", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Flying;
            }

            if (string.Equals(normalized, "dead", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Dead;
            }

            if (string.Equals(normalized, "alert", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "hit", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Hit;
            }

            if (IsHiddenLikeAttackAction(normalized))
            {
                return PlayerState.Attacking;
            }

            return PlayerState.Standing;
        }

        internal static bool ShouldDrawRemoteShadowPartnerForTesting(
            bool hasShadowPartnerActionAnimations,
            bool hiddenLikeClient,
            bool hasMorphTemplate,
            bool hasMechanicMode,
            int? skillId,
            int? rawActionCode)
        {
            return hasShadowPartnerActionAnimations
                && !hiddenLikeClient
                && !hasMechanicMode
                && ShadowPartnerClientActionResolver.ShouldRenderClientShadowPartner(skillId, rawActionCode);
        }

        private static bool ShouldDrawRemoteShadowPartner(RemoteUserActor actor, int? rawActionCode = null)
        {
            return ShouldDrawRemoteShadowPartnerForTesting(
                actor?.TemporaryStatShadowPartnerSkill?.HasShadowPartnerActionAnimations == true,
                actor?.HiddenLikeClient == true,
                actor?.HasMorphTemplate == true,
                actor?.TemporaryStats.KnownState.MechanicMode.HasValue == true,
                actor?.TemporaryStatShadowPartnerSkillId,
                rawActionCode);
        }

        private static void DrawRemoteTemporaryStatAvatarEffects(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            RemoteUserActor actor,
            int screenX,
            int screenY,
            int currentTime,
            bool drawFrontLayers)
        {
            if (actor == null || actor.HiddenLikeClient)
            {
                return;
            }

            DrawRemoteTemporaryStatAvatarEffectState(
                spriteBatch,
                skeletonRenderer,
                actor,
                actor.TemporaryStatBarrierEffect,
                screenX,
                screenY,
                currentTime,
                drawFrontLayers);
            DrawRemoteTemporaryStatAvatarEffectState(
                spriteBatch,
                skeletonRenderer,
                actor,
                actor.TemporaryStatBlessingArmorEffect,
                screenX,
                screenY,
                currentTime,
                drawFrontLayers);
            DrawRemoteTemporaryStatAvatarEffectState(
                spriteBatch,
                skeletonRenderer,
                actor,
                actor.TemporaryStatRepeatEffect,
                screenX,
                screenY,
                currentTime,
                drawFrontLayers);
            DrawRemoteTemporaryStatAvatarEffectState(
                spriteBatch,
                skeletonRenderer,
                actor,
                actor.TemporaryStatAuraEffect,
                screenX,
                screenY,
                currentTime,
                drawFrontLayers);
            DrawRemoteTemporaryStatAvatarEffectState(
                spriteBatch,
                skeletonRenderer,
                actor,
                actor.TemporaryStatMagicShieldEffect,
                screenX,
                screenY,
                currentTime,
                drawFrontLayers);
            DrawRemoteTemporaryStatAvatarEffectState(
                spriteBatch,
                skeletonRenderer,
                actor,
                actor.TemporaryStatSoulArrowEffect,
                screenX,
                screenY,
                currentTime,
                drawFrontLayers);
            DrawRemoteTemporaryStatAvatarEffectState(
                spriteBatch,
                skeletonRenderer,
                actor,
                actor.TemporaryStatWeaponChargeEffect,
                screenX,
                screenY,
                currentTime,
                drawFrontLayers);
            DrawRemoteTemporaryStatAvatarEffectState(
                spriteBatch,
                skeletonRenderer,
                actor,
                actor.TemporaryStatFinalCutEffect,
                screenX,
                screenY,
                currentTime,
                drawFrontLayers);
        }

        private static void DrawRemoteTransientSkillUseAvatarEffects(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            RemoteUserActor actor,
            int screenX,
            int screenY,
            int currentTime,
            bool drawFrontLayers)
        {
            if (actor?.TransientSkillUseAvatarEffects == null || actor.HiddenLikeClient)
            {
                return;
            }

            for (int i = 0; i < actor.TransientSkillUseAvatarEffects.Count; i++)
            {
                RemoteTransientSkillUseAvatarEffectState state = actor.TransientSkillUseAvatarEffects[i];
                if (state == null)
                {
                    continue;
                }

                int elapsedTime = Math.Max(0, currentTime - state.AnimationStartTime);
                SkillAnimation animation = drawFrontLayers
                    ? state.OverlayAnimation
                    : state.UnderFaceAnimation;
                DrawRemoteTemporaryStatAvatarEffectAnimation(
                    spriteBatch,
                    skeletonRenderer,
                    actor,
                    animation,
                    screenX,
                    screenY,
                    elapsedTime);
            }
        }

        private static void DrawRemoteTemporaryStatAvatarEffectState(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            RemoteUserActor actor,
            RemoteTemporaryStatAvatarEffectState state,
            int screenX,
            int screenY,
            int currentTime,
            bool drawFrontLayers)
        {
            if (state == null)
            {
                return;
            }

            int elapsedTime = Math.Max(0, currentTime - state.AnimationStartTime);
            if (drawFrontLayers)
            {
                DrawRemoteTemporaryStatAvatarEffectAnimation(spriteBatch, skeletonRenderer, actor, state.OverlayAnimation, screenX, screenY, elapsedTime);
                DrawRemoteTemporaryStatAvatarEffectAnimation(spriteBatch, skeletonRenderer, actor, state.OverlaySecondaryAnimation, screenX, screenY, elapsedTime);
                return;
            }

            DrawRemoteTemporaryStatAvatarEffectAnimation(spriteBatch, skeletonRenderer, actor, state.UnderFaceAnimation, screenX, screenY, elapsedTime);
            DrawRemoteTemporaryStatAvatarEffectAnimation(spriteBatch, skeletonRenderer, actor, state.UnderFaceSecondaryAnimation, screenX, screenY, elapsedTime);
        }

        private static void DrawRemoteTemporaryStatAvatarEffectAnimation(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            RemoteUserActor actor,
            SkillAnimation animation,
            int screenX,
            int screenY,
            int elapsedTime)
        {
            if (animation == null)
            {
                return;
            }

            SkillFrame frame = animation.GetFrameAtTime(elapsedTime);
            if (frame?.Texture == null)
            {
                return;
            }

            bool shouldFlip = actor.FacingRight ^ frame.Flip;
            int drawX = shouldFlip
                ? screenX - (frame.Texture.Width - frame.Origin.X)
                : screenX - frame.Origin.X;
            int drawY = screenY - frame.Origin.Y;
            frame.Texture.DrawBackground(spriteBatch, skeletonRenderer, null, drawX, drawY, Color.White, shouldFlip, null);
        }

        private static void DrawRemotePacketOwnedEmotionEffect(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            RemoteUserActor actor,
            int screenX,
            int screenY,
            int currentTime)
        {
            RemotePacketOwnedEmotionState state = actor?.PacketOwnedEmotion;
            if (state?.EffectAnimation == null)
            {
                return;
            }

            int elapsedTime = Math.Max(0, currentTime - state.AnimationStartTime);
            DrawRemoteTemporaryStatAvatarEffectAnimation(
                spriteBatch,
                skeletonRenderer,
                actor,
                state.EffectAnimation,
                screenX,
                screenY,
                elapsedTime);
        }

        private static void DrawRemoteActorFrame(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            RemoteUserActor actor,
            AssembledFrame frame,
            int screenX,
            int screenY,
            int currentTime)
        {
            if (frame?.Parts == null || frame.Parts.Count == 0)
            {
                return;
            }

            int adjustedY = screenY - frame.FeetOffset;
            int underFaceInsertionIndex = GetUnderFaceInsertionIndex(frame.Parts);
            bool underFaceDrawn = false;

            for (int i = 0; i < frame.Parts.Count; i++)
            {
                if (!underFaceDrawn && i == underFaceInsertionIndex)
                {
                    DrawRemoteTemporaryStatAvatarEffects(
                        spriteBatch,
                        skeletonRenderer,
                        actor,
                        screenX,
                        screenY,
                        currentTime,
                        drawFrontLayers: false);
                    DrawRemoteTransientSkillUseAvatarEffects(
                        spriteBatch,
                        skeletonRenderer,
                        actor,
                        screenX,
                        screenY,
                        currentTime,
                        drawFrontLayers: false);
                    DrawRemoteShadowPartner(
                        spriteBatch,
                        skeletonRenderer,
                        actor,
                        screenX,
                        screenY,
                        currentTime);
                    underFaceDrawn = true;
                }

                DrawRemoteAssembledPart(spriteBatch, skeletonRenderer, frame.Parts[i], screenX, adjustedY, actor.FacingRight, Color.White);
            }

            if (!underFaceDrawn)
            {
                DrawRemoteTemporaryStatAvatarEffects(
                    spriteBatch,
                    skeletonRenderer,
                    actor,
                    screenX,
                    screenY,
                    currentTime,
                    drawFrontLayers: false);
                DrawRemoteTransientSkillUseAvatarEffects(
                    spriteBatch,
                    skeletonRenderer,
                    actor,
                    screenX,
                    screenY,
                    currentTime,
                    drawFrontLayers: false);
                DrawRemoteShadowPartner(
                    spriteBatch,
                    skeletonRenderer,
                    actor,
                    screenX,
                    screenY,
                    currentTime);
            }
        }

        private static void DrawRemoteAssembledPart(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            AssembledPart part,
            int screenX,
            int adjustedY,
            bool flip,
            Color tint)
        {
            if (part?.Texture == null || !part.IsVisible)
            {
                return;
            }

            int partX = flip
                ? screenX - part.OffsetX - part.Texture.Width
                : screenX + part.OffsetX;
            int partY = adjustedY + part.OffsetY;
            Color partColor = part.Tint != Color.White ? part.Tint : tint;
            part.Texture.DrawBackground(spriteBatch, skeletonRenderer, null, partX, partY, partColor, flip, null);
        }

        private static int GetUnderFaceInsertionIndex(List<AssembledPart> parts)
        {
            if (parts == null || parts.Count == 0)
            {
                return 0;
            }

            int fallbackIndex = parts.Count;
            for (int i = 0; i < parts.Count; i++)
            {
                CharacterPartType partType = parts[i].PartType;
                if (partType == CharacterPartType.Head)
                {
                    fallbackIndex = i + 1;
                    continue;
                }

                if (partType == CharacterPartType.Face
                    || partType == CharacterPartType.Hair
                    || partType == CharacterPartType.Cap
                    || partType == CharacterPartType.CapOverHair
                    || partType == CharacterPartType.CapBelowAccessory
                    || partType == CharacterPartType.Accessory
                    || partType == CharacterPartType.AccessoryOverHair
                    || partType == CharacterPartType.Face_Accessory
                    || partType == CharacterPartType.Eye_Accessory
                    || partType == CharacterPartType.Earrings)
                {
                    return i;
                }
            }

            return fallbackIndex;
        }

        private static void DrawRemoteShadowPartner(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            RemoteUserActor actor,
            int screenX,
            int screenY,
            int currentTime)
        {
            string baseActionName = string.IsNullOrWhiteSpace(actor.BaseActionName)
                ? ResolveActionName(actor, MoveActionFromRaw(actor.LastMoveActionRaw))
                : actor.BaseActionName;
            PlayerState state = ResolveRemoteShadowPartnerState(baseActionName);
            int? rawActionCode = ResolveRemoteShadowPartnerObservedRawActionCode(actor, state, baseActionName);
            if (!ShouldDrawRemoteShadowPartner(actor, rawActionCode))
            {
                return;
            }

            UpdateRemoteShadowPartnerPresentation(actor, baseActionName, state, currentTime);
            if (actor.ShadowPartnerPresentation?.CurrentPlaybackAnimation?.TryGetFrameAtTime(
                    Math.Max(0, currentTime - actor.ShadowPartnerPresentation.CurrentActionStartTime),
                    out SkillFrame frame,
                    out int frameElapsedMs) != true
                || frame?.Texture == null)
            {
                return;
            }

            bool facingRight = actor.ShadowPartnerPresentation.CurrentFacingRight;
            bool flip = facingRight ^ frame.Flip;
            Point clientOffset = ResolveRemoteShadowPartnerCurrentClientOffset(
                actor,
                baseActionName,
                state,
                actor.FacingRight,
                rawActionCode,
                currentTime);
            int horizontalOffsetPx = ShadowPartnerClientActionResolver.ResolveHorizontalOffsetPx(
                actor.ShadowPartnerPresentation.CurrentPlaybackAnimation,
                actor.TemporaryStatShadowPartnerSkill.ShadowPartnerHorizontalOffsetPx);
            int drawX = screenX + clientOffset.X + (facingRight ? -horizontalOffsetPx : horizontalOffsetPx);
            drawX = flip
                ? drawX - (frame.Texture.Width - frame.Origin.X)
                : drawX - frame.Origin.X;

            int drawY = screenY + clientOffset.Y - frame.Origin.Y;
            Color frameTint = RemoteShadowPartnerTint * ResolveRemoteShadowPartnerFrameAlpha(frame, frameElapsedMs);
            frame.Texture.DrawBackground(spriteBatch, skeletonMeshRenderer, null, drawX, drawY, frameTint, flip, null);
        }

        private static void UpdateTransientSkillUseAvatarEffects(RemoteUserActor actor, int currentTime)
        {
            if (actor?.TransientSkillUseAvatarEffects == null || actor.TransientSkillUseAvatarEffects.Count == 0)
            {
                return;
            }

            for (int i = actor.TransientSkillUseAvatarEffects.Count - 1; i >= 0; i--)
            {
                RemoteTransientSkillUseAvatarEffectState state = actor.TransientSkillUseAvatarEffects[i];
                if (state == null || IsTransientSkillUseAvatarEffectExpired(state, currentTime))
                {
                    actor.TransientSkillUseAvatarEffects.RemoveAt(i);
                }
            }
        }

        private static bool IsTransientSkillUseAvatarEffectExpired(RemoteTransientSkillUseAvatarEffectState state, int currentTime)
        {
            int elapsedTime = Math.Max(0, currentTime - state.AnimationStartTime);
            bool overlayComplete = state.OverlayAnimation == null || state.OverlayAnimation.IsComplete(elapsedTime);
            bool underFaceComplete = state.UnderFaceAnimation == null || state.UnderFaceAnimation.IsComplete(elapsedTime);
            return overlayComplete && underFaceComplete;
        }

        internal static void UpdateRemoteShadowPartnerPresentation(
            RemoteUserActor actor,
            string observedPlayerActionName,
            PlayerState state,
            int currentTime)
        {
            int? rawActionCode = ResolveRemoteShadowPartnerObservedRawActionCode(actor, state, observedPlayerActionName);
            if (!ShouldDrawRemoteShadowPartner(actor, rawActionCode))
            {
                return;
            }

            actor.ShadowPartnerPresentation ??= new RemoteShadowPartnerPresentationState();
            RemoteShadowPartnerPresentationState presentation = actor.ShadowPartnerPresentation;
            int playbackRawActionCode = ResolveRemoteShadowPartnerPlaybackRawActionCode(actor, rawActionCode);
            int actionTriggerTime = ResolveRemoteShadowPartnerObservedActionTriggerTime(actor, state);
            string fallbackActionName = CharacterPart.GetActionString(CharacterAction.Stand1);
            string resolvedObservedAction = ResolveRemoteShadowPartnerActionName(
                actor.TemporaryStatShadowPartnerSkill.ShadowPartnerActionAnimations,
                observedPlayerActionName,
                state,
                fallbackActionName,
                actor.Build?.GetWeapon()?.WeaponType,
                rawActionCode,
                actor.TemporaryStatShadowPartnerSkill.ShadowPartnerSupportedRawActionNames);
            SkillAnimation resolvedObservedPlayback = ResolveRemoteShadowPartnerPlaybackAnimation(
                actor.TemporaryStatShadowPartnerSkill.ShadowPartnerActionAnimations,
                resolvedObservedAction,
                observedPlayerActionName,
                playbackRawActionCode,
                actor.TemporaryStatShadowPartnerSkill.ShadowPartnerSupportedRawActionNames);

            if (!presentation.IsActionInitialized)
            {
                presentation.IsActionInitialized = true;
                presentation.ObservedPlayerActionName = observedPlayerActionName;
                presentation.ObservedPlayerState = state;
                presentation.ObservedPlayerFacingRight = actor.FacingRight;
                presentation.ObservedRawActionCode = rawActionCode;
                presentation.ObservedPlayerActionTriggerTime = actionTriggerTime;

                string createActionName = ResolveRemoteShadowPartnerCreateActionName(
                    actor.TemporaryStatShadowPartnerSkill.ShadowPartnerActionAnimations,
                    state);
                if (!string.IsNullOrWhiteSpace(createActionName))
                {
                    SetRemoteShadowPartnerAction(
                        actor,
                        createActionName,
                        currentTime,
                        actor.FacingRight,
                        ResolveRemoteShadowPartnerPlaybackAnimation(
                            actor.TemporaryStatShadowPartnerSkill.ShadowPartnerActionAnimations,
                            createActionName,
                            observedPlayerActionName,
                            playbackRawActionCode,
                            actor.TemporaryStatShadowPartnerSkill.ShadowPartnerSupportedRawActionNames));

                    if (!string.IsNullOrWhiteSpace(resolvedObservedAction))
                    {
                        presentation.QueuedActionName = resolvedObservedAction;
                        presentation.QueuedPlaybackAnimation = resolvedObservedPlayback;
                        presentation.QueuedFacingRight = actor.FacingRight;
                        presentation.QueuedForceReplay = state == PlayerState.Attacking && actionTriggerTime != int.MinValue;
                    }

                    return;
                }

                SetRemoteShadowPartnerAction(
                    actor,
                    resolvedObservedAction,
                    currentTime,
                    actor.FacingRight,
                    resolvedObservedPlayback,
                    forceRestartWhenSameAction: state == PlayerState.Attacking && actionTriggerTime != int.MinValue);
                return;
            }

            if (TryAdvanceRemoteShadowPartnerQueuedAction(actor, currentTime))
            {
                return;
            }

            bool observedChanged = !string.Equals(observedPlayerActionName, presentation.ObservedPlayerActionName, StringComparison.OrdinalIgnoreCase)
                                  || state != presentation.ObservedPlayerState
                                  || actor.FacingRight != presentation.ObservedPlayerFacingRight
                                  || rawActionCode != presentation.ObservedRawActionCode
                                  || actionTriggerTime != presentation.ObservedPlayerActionTriggerTime;
            if (observedChanged)
            {
                presentation.ObservedPlayerActionName = observedPlayerActionName;
                presentation.ObservedPlayerState = state;
                presentation.ObservedPlayerFacingRight = actor.FacingRight;
                presentation.ObservedRawActionCode = rawActionCode;
                presentation.ObservedPlayerActionTriggerTime = actionTriggerTime;

                if (ShadowPartnerClientActionResolver.IsAttackAction(observedPlayerActionName))
                {
                    if (!string.IsNullOrWhiteSpace(resolvedObservedAction))
                    {
                        presentation.PendingActionName = resolvedObservedAction;
                        presentation.PendingPlaybackAnimation = resolvedObservedPlayback;
                        presentation.PendingActionReadyTime = currentTime + ResolveRemoteShadowPartnerAttackDelayMs(actor, resolvedObservedAction);
                        presentation.PendingFacingRight = actor.FacingRight;
                        presentation.PendingForceReplay = true;
                    }
                }
                else
                {
                    if (ShouldHoldRemoteShadowPartnerCurrentAction(actor, currentTime))
                    {
                        presentation.QueuedActionName = resolvedObservedAction;
                        presentation.QueuedPlaybackAnimation = resolvedObservedPlayback;
                        presentation.QueuedFacingRight = actor.FacingRight;
                        presentation.QueuedForceReplay = false;
                    }
                    else
                    {
                        SetRemoteShadowPartnerAction(
                            actor,
                            resolvedObservedAction,
                            currentTime,
                            actor.FacingRight,
                            resolvedObservedPlayback,
                            preserveTimingWhenOnlyFacingChanges: true);
                    }

                    presentation.PendingActionName = null;
                    presentation.PendingPlaybackAnimation = null;
                    presentation.PendingForceReplay = false;
                }
            }

            if (!string.IsNullOrWhiteSpace(presentation.PendingActionName)
                && currentTime >= presentation.PendingActionReadyTime)
            {
                string pendingActionName = presentation.PendingActionName;
                SkillAnimation pendingPlaybackAnimation = presentation.PendingPlaybackAnimation;
                bool pendingFacingRight = presentation.PendingFacingRight;
                bool pendingForceReplay = presentation.PendingForceReplay;
                presentation.PendingActionName = null;
                presentation.PendingPlaybackAnimation = null;
                presentation.PendingForceReplay = false;

                if (ShouldHoldRemoteShadowPartnerCurrentAction(actor, currentTime))
                {
                    presentation.QueuedActionName = pendingActionName;
                    presentation.QueuedPlaybackAnimation = pendingPlaybackAnimation;
                    presentation.QueuedFacingRight = pendingFacingRight;
                    presentation.QueuedForceReplay = pendingForceReplay;
                }
                else
                {
                    SetRemoteShadowPartnerAction(
                        actor,
                        pendingActionName,
                        currentTime,
                        pendingFacingRight,
                        pendingPlaybackAnimation,
                        forceRestartWhenSameAction: pendingForceReplay);
                }
            }

            if (string.IsNullOrWhiteSpace(presentation.CurrentActionName))
            {
                string fallbackAction = ResolveRemoteShadowPartnerFallbackAction(observedPlayerActionName, state);
                string resolvedFallbackAction = ResolveRemoteShadowPartnerActionName(
                    actor.TemporaryStatShadowPartnerSkill.ShadowPartnerActionAnimations,
                    fallbackAction,
                    state,
                    fallbackActionName,
                    actor.Build?.GetWeapon()?.WeaponType,
                    rawActionCode,
                    actor.TemporaryStatShadowPartnerSkill.ShadowPartnerSupportedRawActionNames);
                SetRemoteShadowPartnerAction(
                    actor,
                    resolvedFallbackAction,
                    currentTime,
                    actor.FacingRight,
                    ResolveRemoteShadowPartnerPlaybackAnimation(
                        actor.TemporaryStatShadowPartnerSkill.ShadowPartnerActionAnimations,
                        resolvedFallbackAction,
                        fallbackAction,
                        playbackRawActionCode,
                        actor.TemporaryStatShadowPartnerSkill.ShadowPartnerSupportedRawActionNames));
            }
        }

        private static Point ResolveRemoteShadowPartnerCurrentClientOffset(
            RemoteUserActor actor,
            string observedPlayerActionName,
            PlayerState state,
            bool facingRight,
            int? rawActionCode,
            int currentTime)
        {
            if (actor == null)
            {
                return Point.Zero;
            }

            Point targetOffset = ShadowPartnerClientActionResolver.ResolveClientTargetOffset(
                observedPlayerActionName,
                state,
                facingRight,
                RemoteShadowPartnerClientSideOffsetPx,
                RemoteShadowPartnerClientBackActionOffsetYPx,
                rawActionCode,
                actor.HasMorphTemplate);
            actor.ShadowPartnerPresentation ??= new RemoteShadowPartnerPresentationState();
            if (!actor.ShadowPartnerPresentation.IsInitialized)
            {
                actor.ShadowPartnerPresentation.CurrentClientOffsetPx = targetOffset;
                actor.ShadowPartnerPresentation.ClientOffsetStartPx = targetOffset;
                actor.ShadowPartnerPresentation.ClientOffsetTargetPx = targetOffset;
                actor.ShadowPartnerPresentation.ClientOffsetTransitionStartTime = currentTime;
                actor.ShadowPartnerPresentation.IsInitialized = true;
                return targetOffset;
            }

            if (targetOffset != actor.ShadowPartnerPresentation.ClientOffsetTargetPx)
            {
                actor.ShadowPartnerPresentation.ClientOffsetStartPx = actor.ShadowPartnerPresentation.CurrentClientOffsetPx;
                actor.ShadowPartnerPresentation.ClientOffsetTargetPx = targetOffset;
                actor.ShadowPartnerPresentation.ClientOffsetTransitionStartTime = currentTime;
            }

            actor.ShadowPartnerPresentation.CurrentClientOffsetPx = ShadowPartnerClientActionResolver.InterpolateClientOffset(
                actor.ShadowPartnerPresentation.ClientOffsetStartPx,
                actor.ShadowPartnerPresentation.ClientOffsetTargetPx,
                actor.ShadowPartnerPresentation.ClientOffsetTransitionStartTime,
                currentTime,
                RemoteShadowPartnerTransitionDurationMs);
            return actor.ShadowPartnerPresentation.CurrentClientOffsetPx;
        }

        private static bool TryAdvanceRemoteShadowPartnerQueuedAction(RemoteUserActor actor, int currentTime)
        {
            RemoteShadowPartnerPresentationState presentation = actor?.ShadowPartnerPresentation;
            if (presentation == null || string.IsNullOrWhiteSpace(presentation.QueuedActionName))
            {
                return false;
            }

            if (ShouldHoldRemoteShadowPartnerCurrentAction(actor, currentTime))
            {
                return false;
            }

            string queuedActionName = presentation.QueuedActionName;
            SkillAnimation queuedPlaybackAnimation = presentation.QueuedPlaybackAnimation;
            bool queuedFacingRight = presentation.QueuedFacingRight;
            bool queuedForceReplay = presentation.QueuedForceReplay;
            presentation.QueuedActionName = null;
            presentation.QueuedPlaybackAnimation = null;
            presentation.QueuedForceReplay = false;
            SetRemoteShadowPartnerAction(
                actor,
                queuedActionName,
                currentTime,
                queuedFacingRight,
                queuedPlaybackAnimation,
                forceRestartWhenSameAction: queuedForceReplay);
            return true;
        }

        private static void SetRemoteShadowPartnerAction(
            RemoteUserActor actor,
            string actionName,
            int currentTime,
            bool facingRight,
            SkillAnimation playbackAnimation,
            bool preserveTimingWhenOnlyFacingChanges = false,
            bool forceRestartWhenSameAction = false)
        {
            if (actor?.ShadowPartnerPresentation == null
                || actor.TemporaryStatShadowPartnerSkill?.ShadowPartnerActionAnimations == null
                || string.IsNullOrWhiteSpace(actionName)
                || !actor.TemporaryStatShadowPartnerSkill.ShadowPartnerActionAnimations.ContainsKey(actionName))
            {
                return;
            }

            RemoteShadowPartnerPresentationState presentation = actor.ShadowPartnerPresentation;
            if (string.Equals(presentation.CurrentActionName, actionName, StringComparison.OrdinalIgnoreCase))
            {
                presentation.CurrentPlaybackAnimation = playbackAnimation
                    ?? ResolveRemoteShadowPartnerPlaybackAnimation(
                        actor.TemporaryStatShadowPartnerSkill.ShadowPartnerActionAnimations,
                        actionName,
                        presentation.ObservedPlayerActionName,
                        presentation.ObservedRawActionCode.GetValueOrDefault(-1),
                        actor.TemporaryStatShadowPartnerSkill.ShadowPartnerSupportedRawActionNames);
                if (forceRestartWhenSameAction)
                {
                    presentation.CurrentActionStartTime = currentTime;
                    presentation.CurrentFacingRight = facingRight;
                    return;
                }

                if (!preserveTimingWhenOnlyFacingChanges || presentation.CurrentFacingRight == facingRight)
                {
                    return;
                }

                presentation.CurrentFacingRight = facingRight;
                return;
            }

            presentation.CurrentActionName = actionName;
            presentation.CurrentPlaybackAnimation = playbackAnimation
                ?? ResolveRemoteShadowPartnerPlaybackAnimation(
                    actor.TemporaryStatShadowPartnerSkill.ShadowPartnerActionAnimations,
                    actionName,
                    presentation.ObservedPlayerActionName,
                    presentation.ObservedRawActionCode.GetValueOrDefault(-1),
                    actor.TemporaryStatShadowPartnerSkill.ShadowPartnerSupportedRawActionNames);
            presentation.CurrentActionStartTime = currentTime;
            presentation.CurrentFacingRight = facingRight;
        }

        private static bool ShouldHoldRemoteShadowPartnerCurrentAction(RemoteUserActor actor, int currentTime)
        {
            RemoteShadowPartnerPresentationState presentation = actor?.ShadowPartnerPresentation;
            if (presentation?.CurrentPlaybackAnimation?.Frames == null
                || presentation.CurrentPlaybackAnimation.Frames.Count == 0
                || !ShadowPartnerClientActionResolver.IsBlockingAction(presentation.CurrentActionName))
            {
                return false;
            }

            int elapsedTime = Math.Max(0, currentTime - presentation.CurrentActionStartTime);
            return !presentation.CurrentPlaybackAnimation.IsComplete(elapsedTime);
        }

        private static int ResolveRemoteShadowPartnerAttackDelayMs(RemoteUserActor actor, string actionName)
        {
            return ShadowPartnerClientActionResolver.ResolveAttackDelayMs(
                actor?.TemporaryStatShadowPartnerSkill?.ShadowPartnerActionAnimations,
                actionName,
                RemoteShadowPartnerAttackDelayMs);
        }

        internal static int? ResolveRemoteShadowPartnerObservedRawActionCode(
            RemoteUserActor actor,
            PlayerState state,
            string observedPlayerActionName)
        {
            if (actor == null)
            {
                return null;
            }

            if (actor.BaseActionRawCode.HasValue
                && (state == PlayerState.Attacking
                    || ShouldPreferRemoteShadowPartnerBaseRawAction(actor, observedPlayerActionName)))
            {
                return actor.BaseActionRawCode;
            }

            return actor.LastMoveActionRaw;
        }

        internal static int ResolveRemoteShadowPartnerPlaybackRawActionCode(RemoteUserActor actor, int? observedRawActionCode)
        {
            return observedRawActionCode ?? actor?.LastMoveActionRaw ?? 0;
        }

        private static bool ShouldPreferRemoteShadowPartnerBaseRawAction(
            RemoteUserActor actor,
            string observedPlayerActionName)
        {
            if (actor == null || !actor.BaseActionRawCode.HasValue)
            {
                return false;
            }

            if (!actor.MovementDrivenActionSelection)
            {
                return true;
            }

            string normalizedObservedActionName = NormalizeActionName(observedPlayerActionName, allowSitFallback: false);
            if (string.IsNullOrWhiteSpace(normalizedObservedActionName))
            {
                return false;
            }

            string normalizedBaseActionName = NormalizeActionName(actor.BaseActionName, allowSitFallback: false);
            if (string.Equals(normalizedBaseActionName, normalizedObservedActionName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string weaponType = actor.Build?.GetWeapon()?.WeaponType;

            if (CharacterPart.TryGetActionStringFromCode(actor.BaseActionRawCode.Value, out string rawActionName))
            {
                string normalizedRawActionName = NormalizeActionName(rawActionName, allowSitFallback: false);
                if (string.Equals(normalizedRawActionName, normalizedObservedActionName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (ShadowPartnerHelperActionFamiliesMatch(
                        rawActionName,
                        observedPlayerActionName,
                        weaponType,
                        actor.BaseActionRawCode,
                        weaponType,
                        actor.BaseActionRawCode))
                {
                    return true;
                }
            }

            return ShadowPartnerHelperActionFamiliesMatch(
                actor.BaseActionName,
                observedPlayerActionName,
                weaponType,
                actor.BaseActionRawCode,
                weaponType,
                actor.BaseActionRawCode);
        }

        internal static bool ShadowPartnerHelperActionFamiliesMatch(
            string leftActionName,
            string rightActionName,
            string leftWeaponType = null,
            int? leftRawActionCode = null,
            string rightWeaponType = null,
            int? rightRawActionCode = null)
        {
            if (string.IsNullOrWhiteSpace(leftActionName) || string.IsNullOrWhiteSpace(rightActionName))
            {
                return false;
            }

            var leftCandidates = CollectShadowPartnerHelperActionIdentityCandidates(leftActionName, leftWeaponType, leftRawActionCode);
            var rightCandidates = CollectShadowPartnerHelperActionIdentityCandidates(rightActionName, rightWeaponType, rightRawActionCode);
            return leftCandidates.Overlaps(rightCandidates);
        }

        private static HashSet<string> CollectShadowPartnerHelperActionIdentityCandidates(
            string actionName,
            string weaponType = null,
            int? rawActionCode = null)
        {
            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string normalizedActionName = NormalizeActionName(actionName, allowSitFallback: false);
            if (!string.IsNullOrWhiteSpace(normalizedActionName)
                && !ShadowPartnerClientActionResolver.ShouldSuppressRawBackedGenericAttackIdentityCandidate(
                    normalizedActionName,
                    rawActionCode))
            {
                candidates.Add(normalizedActionName);
            }

            PlayerState state = ResolveShadowPartnerActionIdentityState(normalizedActionName);
            foreach (string candidate in ShadowPartnerClientActionResolver.EnumerateHelperIdentityCandidates(
                         actionName,
                         state,
                         weaponType,
                         rawActionCode))
            {
                string normalizedCandidate = NormalizeActionName(candidate, allowSitFallback: false);
                if (!string.IsNullOrWhiteSpace(normalizedCandidate))
                {
                    candidates.Add(normalizedCandidate);
                }
            }

            return candidates;
        }

        private static PlayerState ResolveShadowPartnerActionIdentityState(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return PlayerState.Standing;
            }

            if (string.Equals(actionName, "jump", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Jumping;
            }

            if (string.Equals(actionName, "ladder", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "ladder2", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Ladder;
            }

            if (string.Equals(actionName, "rope", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "rope2", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Rope;
            }

            if (string.Equals(actionName, "sit", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Sitting;
            }

            if (string.Equals(actionName, "prone", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "proneStab", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "prone2", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Prone;
            }

            if (string.Equals(actionName, "swim", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Swimming;
            }

            if (string.Equals(actionName, "fly", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "fly2", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "fly2Move", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "fly2Skill", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "ghostfly", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Flying;
            }

            if (string.Equals(actionName, "dead", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Dead;
            }

            if (actionName.StartsWith("walk", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "move", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Walking;
            }

            if (actionName.StartsWith("alert", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "hit", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Hit;
            }

            return ShadowPartnerClientActionResolver.IsAttackAction(actionName)
                ? PlayerState.Attacking
                : PlayerState.Standing;
        }

        private static int ResolveRemoteShadowPartnerObservedActionTriggerTime(RemoteUserActor actor, PlayerState state)
        {
            if (actor == null || state != PlayerState.Attacking)
            {
                return int.MinValue;
            }

            return actor.BaseActionStartTime;
        }

        private static string ResolveRemoteShadowPartnerCreateActionName(
            IReadOnlyDictionary<string, SkillAnimation> actionAnimations,
            PlayerState state)
        {
            return ShadowPartnerClientActionResolver.ResolveCreateActionName(actionAnimations, state);
        }

        private static string ResolveRemoteShadowPartnerFallbackAction(string observedPlayerActionName, PlayerState state)
        {
            return state switch
            {
                PlayerState.Walking => "walk1",
                PlayerState.Jumping or PlayerState.Falling => "jump",
                PlayerState.Ladder => "ladder",
                PlayerState.Rope => "rope",
                PlayerState.Swimming or PlayerState.Flying => "fly",
                PlayerState.Sitting => "sit",
                PlayerState.Prone => "prone",
                PlayerState.Dead => "dead",
                PlayerState.Attacking when !string.IsNullOrWhiteSpace(observedPlayerActionName) => observedPlayerActionName,
                _ => "stand1"
            };
        }

        private static float ResolveRemoteShadowPartnerFrameAlpha(SkillFrame frame, int frameElapsedMs)
        {
            if (frame == null)
            {
                return 1f;
            }

            int delay = Math.Max(1, frame.Delay);
            if (frame.AlphaStart == frame.AlphaEnd || delay <= 1)
            {
                return MathHelper.Clamp(frame.AlphaStart / 255f, 0f, 1f);
            }

            float progress = MathHelper.Clamp(frameElapsedMs / (float)delay, 0f, 1f);
            float animatedAlpha = MathHelper.Lerp(frame.AlphaStart, frame.AlphaEnd, progress);
            return MathHelper.Clamp(animatedAlpha / 255f, 0f, 1f);
        }

        private void RegisterMeleeAfterImage(
            RemoteUserActor actor,
            int skillId,
            string actionName,
            int currentTime,
            int masteryPercent,
            int chargeElement,
            int? rawActionCode = null)
        {
            if (_skillLoader == null
                || actor?.Build == null
                || actor.Assembler == null)
            {
                actor?.ClearMeleeAfterImage();
                return;
            }

            WeaponPart weapon = actor.Build.GetWeapon();
            SkillData skill = skillId > 0 ? _skillLoader.LoadSkill(skillId) : null;
            if (!_skillLoader.TryResolveMeleeAfterImageAction(
                    skill,
                    weapon,
                    EnumerateRemoteAfterImageActionNames(actor, skill, actionName),
                    actor.Build.Level,
                    masteryPercent,
                    chargeElement,
                    out MeleeAfterImageAction afterImageAction))
            {
                afterImageAction = _skillLoader.ApplyClientMeleeRangeOverride(null, skillId, rawActionCode, actor.FacingRight);
                if (afterImageAction != null)
                {
                    actor.ApplyMeleeAfterImage(
                        skillId,
                        actionName,
                        afterImageAction,
                        currentTime,
                        actor.FacingRight,
                        GetActionDuration(actor.Assembler, actionName),
                        GetAfterImageFadeDuration(actor.Assembler, actionName));
                    return;
                }

                if (actor.MeleeAfterImage?.FadeStartTime < 0)
                {
                    actor.ClearMeleeAfterImage();
                }

                return;
            }

            afterImageAction = _skillLoader.ApplyClientMeleeRangeOverride(afterImageAction, skillId, rawActionCode, actor.FacingRight);

            actor.ApplyMeleeAfterImage(
                skillId,
                actionName,
                afterImageAction,
                currentTime,
                actor.FacingRight,
                GetActionDuration(actor.Assembler, actionName),
                GetAfterImageFadeDuration(actor.Assembler, actionName));
        }

        private static void ApplyRemotePreparedSkillRelease(
            RemoteUserActor actor,
            int skillId,
            int? preparedSkillReleaseFollowUpValue)
        {
            if (actor?.PreparedSkill == null
                || !PreparedSkillHudRules.TryResolveRemotePreparedSkillReleaseOwner(
                    skillId,
                    preparedSkillReleaseFollowUpValue,
                    out int preparedSkillId)
                || actor.PreparedSkill.SkillId != preparedSkillId)
            {
                return;
            }

            actor.PreparedSkill = null;
        }

        private string ResolveRemoteMeleeActionName(
            RemoteUserActor actor,
            SkillData skill,
            string actionName,
            int? rawActionCode,
            int masteryPercent,
            int chargeElement)
        {
            if (actor?.Build == null)
            {
                return actionName;
            }

            WeaponPart weapon = actor.Build.GetWeapon();
            foreach (string candidate in EnumerateRemoteActionResolutionCandidates(actor, skill, actionName, rawActionCode))
            {
                if (_skillLoader != null
                    && _skillLoader.TryResolveMeleeAfterImageAction(
                        skill,
                        weapon,
                        candidate,
                        actor.Build.Level,
                        masteryPercent,
                        chargeElement,
                        out _,
                        out string matchedAfterImageActionName))
                {
                    string preferredActionName = ResolvePreferredRemoteMeleeActionName(actor, candidate, matchedAfterImageActionName);
                    if (!string.IsNullOrWhiteSpace(preferredActionName))
                    {
                        return preferredActionName;
                    }
                }

                if (actor.Assembler?.GetAnimation(candidate)?.Length > 0)
                {
                    return candidate;
                }
            }

            if (_skillLoader != null
                && actor?.Build != null
                && _skillLoader.TryResolveUniqueMeleeAfterImageActionName(
                    skill,
                    actor.Build.GetWeapon(),
                    EnumerateRemoteActionResolutionCandidates(actor, skill, actionName, rawActionCode),
                    actor.Build.Level,
                    masteryPercent,
                    chargeElement,
                    out string uniqueAfterImageActionName))
            {
                string preferredActionName = ResolvePreferredRemoteMeleeActionName(actor, actionName, uniqueAfterImageActionName);
                if (!string.IsNullOrWhiteSpace(preferredActionName))
                {
                    return preferredActionName;
                }
            }

            if (_skillLoader != null
                && actor?.Build != null
                && _skillLoader.TryResolveRenderableMeleeAfterImageActionName(
                    skill,
                    actor.Build.GetWeapon(),
                    EnumerateRemoteActionResolutionCandidates(actor, skill, actionName, rawActionCode),
                    actor.Build.Level,
                    masteryPercent,
                    chargeElement,
                    candidate => actor.Assembler?.GetAnimation(candidate)?.Length > 0,
                    out string renderableAfterImageActionName))
            {
                string preferredActionName = ResolvePreferredRemoteMeleeActionName(actor, actionName, renderableAfterImageActionName);
                if (!string.IsNullOrWhiteSpace(preferredActionName))
                {
                    return preferredActionName;
                }
            }

            return actionName;
        }

        private static string ResolvePreferredRemoteMeleeActionName(
            RemoteUserActor actor,
            string requestedActionName,
            string matchedAfterImageActionName)
        {
            string normalizedRequestedActionName = string.IsNullOrWhiteSpace(requestedActionName)
                ? null
                : requestedActionName.Trim();
            string normalizedMatchedActionName = string.IsNullOrWhiteSpace(matchedAfterImageActionName)
                ? null
                : matchedAfterImageActionName.Trim();

            if (!string.IsNullOrWhiteSpace(normalizedMatchedActionName)
                && actor?.Assembler?.GetAnimation(normalizedMatchedActionName)?.Length > 0)
            {
                return normalizedMatchedActionName;
            }

            if (!string.IsNullOrWhiteSpace(normalizedRequestedActionName)
                && actor?.Assembler?.GetAnimation(normalizedRequestedActionName)?.Length > 0)
            {
                return normalizedRequestedActionName;
            }

            return normalizedMatchedActionName ?? normalizedRequestedActionName;
        }

        private static IEnumerable<string> EnumerateRemoteAfterImageActionNames(
            RemoteUserActor actor,
            SkillData skill,
            string actionName)
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(actionName) && yielded.Add(actionName.Trim()))
            {
                yield return actionName.Trim();
            }

            if (skill?.ActionNames != null)
            {
                foreach (string skillActionName in skill.ActionNames)
                {
                    if (!string.IsNullOrWhiteSpace(skillActionName) && yielded.Add(skillActionName.Trim()))
                    {
                        yield return skillActionName.Trim();
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(skill?.ActionName) && yielded.Add(skill.ActionName.Trim()))
            {
                yield return skill.ActionName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(actor?.ActionName) && yielded.Add(actor.ActionName.Trim()))
            {
                yield return actor.ActionName.Trim();
            }
        }

        private static IEnumerable<string> EnumerateRemoteActionResolutionCandidates(
            RemoteUserActor actor,
            SkillData skill,
            string actionName,
            int? rawActionCode)
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(actionName) && yielded.Add(actionName.Trim()))
            {
                yield return actionName.Trim();
            }

            if (rawActionCode.HasValue
                && CharacterPart.TryGetActionStringFromCode(rawActionCode.Value, out string resolvedCodeActionName)
                && !string.IsNullOrWhiteSpace(resolvedCodeActionName)
                && yielded.Add(resolvedCodeActionName))
            {
                yield return resolvedCodeActionName;
            }

            if (skill?.ActionNames != null)
            {
                foreach (string skillActionName in skill.ActionNames)
                {
                    if (!string.IsNullOrWhiteSpace(skillActionName) && yielded.Add(skillActionName.Trim()))
                    {
                        yield return skillActionName.Trim();
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(skill?.ActionName) && yielded.Add(skill.ActionName.Trim()))
            {
                yield return skill.ActionName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(actor?.ActionName) && yielded.Add(actor.ActionName.Trim()))
            {
                yield return actor.ActionName.Trim();
            }
        }

        private static int ResolveRemoteAfterImageChargeElement(RemoteUserActor actor, int chargeSkillId)
        {
            if (AfterImageChargeSkillResolver.TryGetChargeElement(chargeSkillId, out int explicitChargeElement))
            {
                return explicitChargeElement;
            }

            if (AfterImageChargeSkillResolver.TryGetChargeElement(
                    actor?.TemporaryStats.KnownState.ChargeSkillId ?? 0,
                    out int knownStateChargeElement))
            {
                return knownStateChargeElement;
            }

            int? resolvedChargeSkillId = ResolveChargeSkillIdFromTemporaryStats(
                actor?.TemporaryStats ?? default,
                ResolvePreferredRemoteWeaponChargeSkillId(actor?.Build?.Job ?? 0));
            return resolvedChargeSkillId.HasValue
                && AfterImageChargeSkillResolver.TryGetChargeElement(
                    resolvedChargeSkillId.Value,
                    out int payloadChargeElement)
                ? payloadChargeElement
                : 0;
        }

        internal static int? ResolveChargeSkillIdFromTemporaryStats(
            RemoteUserTemporaryStatSnapshot snapshot,
            int preferredSkillId)
        {
            if (snapshot.KnownState.ChargeSkillId.HasValue)
            {
                return snapshot.KnownState.ChargeSkillId;
            }

            if (snapshot.RawPayload == null
                || snapshot.RawPayload.Length < (sizeof(int) * 4) + sizeof(int))
            {
                return null;
            }

            if (snapshot.WeaponChargePayloadOffset >= 0
                && snapshot.WeaponChargePayloadOffset <= snapshot.RawPayload.Length - sizeof(int)
                && AfterImageChargeSkillResolver.TryResolveChargeSkillIdFromTemporaryStatPayload(
                    snapshot.RawPayload,
                    snapshot.WeaponChargePayloadOffset,
                    preferredSkillId,
                    out int scopedChargeSkillId))
            {
                return scopedChargeSkillId;
            }

            return AfterImageChargeSkillResolver.TryResolveChargeSkillIdFromTemporaryStatPayload(
                snapshot.RawPayload,
                sizeof(int) * 4,
                preferredSkillId,
                out int payloadChargeSkillId)
                ? payloadChargeSkillId
                : null;
        }

        private static int ResolvePreferredRemoteWeaponChargeSkillId(int jobId)
        {
            IReadOnlyList<int> candidateSkillIds = EnumerateRemoteWeaponChargeSkillIds(jobId, preferredSkillId: null);
            return candidateSkillIds != null && candidateSkillIds.Count > 0
                ? candidateSkillIds[0]
                : 0;
        }

        private static int GetActionDuration(CharacterAssembler assembler, string actionName)
        {
            AssembledFrame[] animation = assembler?.GetAnimation(actionName);
            if (animation == null || animation.Length == 0)
            {
                return 0;
            }

            int duration = 0;
            foreach (AssembledFrame frame in animation)
            {
                duration += Math.Max(0, frame?.Duration ?? 0);
            }

            return duration;
        }

        private static int GetAfterImageFadeDuration(CharacterAssembler assembler, string actionName)
        {
            return Math.Max(MinimumMeleeAfterImageFadeDurationMs, GetActionDuration(assembler, actionName) / 4);
        }

        private static void DrawMeleeAfterImage(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            RemoteUserActor actor,
            int screenX,
            int screenY,
            int currentTime)
        {
            if (actor?.Assembler == null || actor.MeleeAfterImage?.AfterImageAction?.FrameSets == null)
            {
                return;
            }

            RemoteMeleeAfterImageState state = actor.MeleeAfterImage;
            bool activeAction = state.FadeStartTime < 0
                && string.Equals(actor.ActionName, state.ActionName, StringComparison.OrdinalIgnoreCase);
            int frameIndex = state.LastFrameIndex;
            IReadOnlyList<AfterimageRenderableLayer> layers = state.LastResolvedLayers;

            if (activeAction)
            {
                int animationTime = Math.Max(0, currentTime - state.AnimationStartTime);
                int lastFrameIndex = state.LastFrameIndex;
                IReadOnlyList<AfterimageRenderableLayer> lastResolvedLayers = state.LastResolvedLayers;
                MeleeAfterimagePlaybackResolver.RefreshSnapshotCache(
                    actor.Assembler,
                    state.ActionName,
                    state.AfterImageAction,
                    animationTime,
                    ref lastFrameIndex,
                    ref lastResolvedLayers);
                state.LastFrameIndex = lastFrameIndex;
                state.LastResolvedLayers = lastResolvedLayers;
                frameIndex = state.LastFrameIndex;
                layers = state.LastResolvedLayers;
            }

            if (layers == null || layers.Count == 0)
            {
                return;
            }

            float fadeAlpha = 1f;
            if (!activeAction && state.FadeStartTime >= 0)
            {
                int fadeElapsed = Math.Max(0, currentTime - state.FadeStartTime);
                if (fadeElapsed >= state.FadeDuration)
                {
                    actor.ClearMeleeAfterImage();
                    return;
                }

                fadeAlpha = 1f - (fadeElapsed / (float)Math.Max(1, state.FadeDuration));
            }

            foreach (AfterimageRenderableLayer layer in layers)
            {
                SkillFrame frame = layer.Frame;
                if (frame?.Texture == null)
                {
                    continue;
                }

                Color tint = Color.White * MathHelper.Clamp(layer.Alpha * fadeAlpha, 0f, 1f);
                bool shouldFlip = state.FacingRight ^ frame.Flip;
                int drawX = shouldFlip
                    ? screenX - (frame.Texture.Width - frame.Origin.X)
                    : screenX - frame.Origin.X;
                int drawY = screenY - frame.Origin.Y;
                frame.Texture.DrawBackground(spriteBatch, skeletonMeshRenderer, null, drawX, drawY, tint, shouldFlip, null);
            }
        }

        private void ApplyMovementSnapshot(RemoteUserActor actor, int currentTime)
        {
            string previousActionName = actor.ActionName;
            PassivePositionSnapshot sampled = actor.MovementSnapshot.SampleAtTime(currentTime);
            actor.Position = new Vector2(sampled.X, sampled.Y);
            actor.FacingRight = sampled.FacingRight;
            actor.CurrentFootholdId = sampled.FootholdId;

            if (actor.MovementDrivenActionSelection)
            {
                SetActorAction(
                    actor,
                    ResolveActionName(actor, sampled.Action),
                    actor.Build.ActivePortableChair != null,
                    currentTime,
                    rawActionCode: actor.LastMoveActionRaw);
                if (!string.Equals(previousActionName, actor.ActionName, StringComparison.OrdinalIgnoreCase))
                {
                    actor.BeginMeleeAfterImageFade(currentTime);
                    RegisterMeleeAfterImage(actor, 0, actor.ActionName, currentTime, 10, 0);
                }
            }
        }

        private void ApplyFollowDriverState(RemoteUserActor actor, PlayerCharacter localPlayer)
        {
            if (actor == null || actor.FollowDriverId <= 0)
            {
                return;
            }

            if (!TryResolveFollowDriverWorldState(actor, localPlayer, out Vector2 position, out bool facingRight))
            {
                return;
            }

            actor.Position = position;
            actor.FacingRight = facingRight;
        }

        private bool TryResolveFollowDriverWorldState(
            RemoteUserActor actor,
            PlayerCharacter localPlayer,
            out Vector2 position,
            out bool facingRight)
        {
            position = default;
            facingRight = actor?.FacingRight ?? true;
            if (actor == null || actor.FollowDriverId <= 0)
            {
                return false;
            }

            if (TryResolveLocalFollowDriverState(actor.FollowDriverId, localPlayer, out position, out facingRight))
            {
                return true;
            }

            if (!_actorsById.TryGetValue(actor.FollowDriverId, out RemoteUserActor driverActor))
            {
                return false;
            }

            return TryResolveFollowDriverOffsetPosition(
                driverActor.Position,
                driverActor.FacingRight,
                driverActor.ActionName,
                out position,
                out facingRight);
        }

        private static bool TryResolveLocalFollowDriverState(
            int driverCharacterId,
            PlayerCharacter localPlayer,
            out Vector2 position,
            out bool facingRight)
        {
            position = default;
            facingRight = localPlayer?.FacingRight ?? true;
            if (localPlayer?.Build?.Id != driverCharacterId)
            {
                return false;
            }

            return TryResolveFollowDriverOffsetPosition(
                localPlayer.Position,
                localPlayer.FacingRight,
                localPlayer.CurrentActionName,
                out position,
                out facingRight);
        }

        internal static bool TryResolveFollowDriverOffsetPosition(
            Vector2 driverPosition,
            bool driverFacingRight,
            string driverActionName,
            out Vector2 followerPosition,
            out bool followerFacingRight)
        {
            followerFacingRight = driverFacingRight;
            if (IsFollowDriverLadderOrRopeAction(driverActionName))
            {
                followerPosition = new Vector2(
                    driverPosition.X,
                    driverPosition.Y + FollowDriverLadderRopeVerticalOffset);
                return true;
            }

            followerPosition = new Vector2(
                driverPosition.X + (driverFacingRight ? FollowDriverGroundHorizontalOffset : -FollowDriverGroundHorizontalOffset),
                driverPosition.Y);
            return true;
        }

        private static bool IsFollowDriverLadderOrRopeAction(string actionName)
        {
            return string.Equals(actionName, "ladder", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "rope", StringComparison.OrdinalIgnoreCase);
        }

        private static MoveAction MoveActionFromRaw(byte moveAction)
        {
            int normalized = (moveAction >> 1) & 0x0F;
            return Enum.IsDefined(typeof(MoveAction), normalized)
                ? (MoveAction)normalized
                : MoveAction.Stand;
        }

        private static bool DecodeFacingRight(byte moveAction)
        {
            return (moveAction & 1) == 0;
        }

        private static string ResolveActionName(RemoteUserActor actor, MoveAction moveAction)
        {
            bool hasPortableChair = actor?.Build?.ActivePortableChair != null;
            return moveAction switch
            {
                MoveAction.Walk => "walk1",
                MoveAction.Jump or MoveAction.Fall => "jump",
                MoveAction.Ladder => "ladder",
                MoveAction.Rope => "rope",
                MoveAction.Swim => "swim",
                MoveAction.Fly => "fly",
                MoveAction.Attack => string.IsNullOrWhiteSpace(actor?.BaseActionName) ? "alert" : actor.BaseActionName,
                MoveAction.Hit => "alert",
                MoveAction.Die => "dead",
                _ => hasPortableChair ? "sit" : CharacterPart.GetActionString(CharacterAction.Stand1)
            };
        }

        private void UpdateNameLookup(string previousName, string currentName, int characterId)
        {
            if (!string.IsNullOrWhiteSpace(previousName)
                && !string.Equals(previousName, currentName, StringComparison.OrdinalIgnoreCase))
            {
                _actorIdsByName.Remove(previousName);
            }

            _actorIdsByName[currentName] = characterId;
        }

        private void ApplyPortableChairMount(RemoteUserActor actor, PortableChair chair)
        {
            if (_loader == null
                || actor?.Build == null
                || chair?.TamingMobItemId is not int tamingMobItemId
                || tamingMobItemId <= 0)
            {
                return;
            }

            CharacterPart mountPart = _loader.LoadEquipment(tamingMobItemId);
            if (mountPart?.Slot != EquipSlot.TamingMob)
            {
                return;
            }

            actor.Build.Equipment.TryGetValue(EquipSlot.TamingMob, out CharacterPart previousMount);
            actor.PortableChairPreviousMount = previousMount;
            actor.PortableChairAppliedMount = true;
            actor.Build.Equip(mountPart);
        }

        private static void ClearPortableChairMountState(RemoteUserActor actor)
        {
            if (actor?.Build == null || !actor.PortableChairAppliedMount)
            {
                return;
            }

            if (actor.PortableChairPreviousMount != null)
            {
                actor.Build.Equip(actor.PortableChairPreviousMount);
            }
            else
            {
                actor.Build.Unequip(EquipSlot.TamingMob);
            }

            actor.PortableChairPreviousMount = null;
            actor.PortableChairAppliedMount = false;
        }
    }

    public sealed class RemoteUserActor
    {
        public RemoteUserActor(
            int characterId,
            string name,
            CharacterBuild build,
            Vector2 position,
            bool facingRight,
            string actionName,
            string sourceTag,
            bool isVisibleInWorld)
        {
            CharacterId = characterId;
            Name = name;
            Build = build;
            Position = position;
            FacingRight = facingRight;
            BaseActionName = actionName;
            ActionName = actionName;
            SourceTag = sourceTag;
            IsVisibleInWorld = isVisibleInWorld;
            RefreshAssembler();
        }

        public int CharacterId { get; }
        public string Name { get; set; }
        public CharacterBuild Build { get; set; }
        public CharacterAssembler Assembler { get; private set; }
        public Vector2 Position { get; set; }
        public bool FacingRight { get; set; }
        public string BaseActionName { get; set; }
        public int BaseActionStartTime { get; set; } = int.MinValue;
        public int? BaseActionRawCode { get; set; }
        public string ActionName { get; set; }
        public string SourceTag { get; set; }
        public bool IsVisibleInWorld { get; set; }
        public MinimapUI.HelperMarkerType? HelperMarkerType { get; set; }
        public bool ShowDirectionOverlay { get; set; } = true;
        public int? BattlefieldTeamId { get; set; }
        public RemotePreparedSkillState PreparedSkill { get; set; }
        public CharacterPart PortableChairPreviousMount { get; set; }
        public bool PortableChairAppliedMount { get; set; }
        public int? PreferredPortableChairPairCharacterId { get; set; }
        public Dictionary<RemoteRelationshipOverlayType, RemoteRelationshipOverlayState> RelationshipOverlays { get; } = new();
        public RemoteUserTemporaryStatSnapshot TemporaryStats { get; set; }
        public ushort TemporaryStatDelay { get; set; }
        public PlayerMovementSyncSnapshot MovementSnapshot { get; set; }
        public byte LastMoveActionRaw { get; set; }
        public int CurrentFootholdId { get; set; }
        public bool MovementDrivenActionSelection { get; set; }
        public bool HasMorphTemplate { get; set; }
        public bool HiddenLikeClient { get; set; }
        public int RidingVehicleId { get; set; }
        public CharacterPart TemporaryStatAvatarOverridePart { get; set; }
        public CharacterPart TemporaryStatTamingMobOverridePart { get; set; }
        public int? TemporaryStatShadowPartnerSkillId { get; set; }
        public SkillData TemporaryStatShadowPartnerSkill { get; set; }
        public RemoteUserActorPool.RemoteShadowPartnerPresentationState ShadowPartnerPresentation { get; set; }
        public RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState TemporaryStatAuraEffect { get; set; }
        public RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState TemporaryStatSoulArrowEffect { get; set; }
        public RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState TemporaryStatWeaponChargeEffect { get; set; }
        public RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState TemporaryStatBarrierEffect { get; set; }
        public RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState TemporaryStatBlessingArmorEffect { get; set; }
        public RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState TemporaryStatRepeatEffect { get; set; }
        public RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState TemporaryStatMagicShieldEffect { get; set; }
        public RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState TemporaryStatFinalCutEffect { get; set; }
        public RemoteUserActorPool.RemotePacketOwnedEmotionState PacketOwnedEmotion { get; set; }
        public List<RemoteUserActorPool.RemoteTransientSkillUseAvatarEffectState> TransientSkillUseAvatarEffects { get; } = new();
        public int MovingShootPreparedSkillId { get; set; }
        public int? PartyCurrentHp { get; set; }
        public int? PartyMaxHp { get; set; }
        public int? PartyHpPercent { get; set; }
        public int? PartyHpGaugePos { get; set; }
        public int? PacketOwnedQuestDeliveryEffectItemId { get; set; }
        public RemoteUserActorPool.RemoteHitState LastHit { get; set; }
        public int? LastThrowGrenadeSkillId { get; set; }
        public int? LastThrowGrenadeId { get; set; }
        public Point? LastThrowGrenadeTarget { get; set; }
        public int LastThrowGrenadeKeyDownTime { get; set; }
        public int LastThrowGrenadePacketTime { get; set; } = int.MinValue;
        public int? CarryItemEffectId { get; set; }
        public int CompletedSetItemId { get; set; }
        public Dictionary<EquipSlot, CharacterPart> BattlefieldOriginalEquipment { get; set; }
        public float? BattlefieldOriginalSpeed { get; set; }
        public int? BattlefieldAppliedTeamId { get; set; }
        public RemoteMeleeAfterImageState MeleeAfterImage { get; private set; }
        public PacketOwnedUserSummonRegistry PacketOwnedSummons { get; } = new();
        public int FollowDriverId { get; set; }
        public int FollowPassengerId { get; set; }

        public void RefreshAssembler()
        {
            Assembler = new CharacterAssembler(Build);
            if (Assembler != null)
            {
                Assembler.OverrideAvatarPart = TemporaryStatAvatarOverridePart;
                Assembler.OverrideTamingMobPart = TemporaryStatTamingMobOverridePart;
                Assembler.FaceExpressionName = PacketOwnedEmotion?.EmotionName ?? "default";
            }
        }

        public void ApplyMeleeAfterImage(
            int skillId,
            string actionName,
            MeleeAfterImageAction afterImageAction,
            int currentTime,
            bool facingRight,
            int actionDuration,
            int fadeDuration)
        {
            if (afterImageAction == null
                || string.IsNullOrWhiteSpace(actionName)
                || ((afterImageAction.FrameSets == null || afterImageAction.FrameSets.Count == 0) && !afterImageAction.HasRange))
            {
                MeleeAfterImage = null;
                return;
            }

            MeleeAfterImage = new RemoteMeleeAfterImageState
            {
                SkillId = skillId,
                ActionName = actionName,
                AfterImageAction = afterImageAction,
                AnimationStartTime = currentTime,
                ActivationStartTime = currentTime + ClientMeleeAfterimageRangeResolver.ResolveActivationDelayMs(
                    afterImageAction,
                    Assembler?.GetAnimation(actionName)),
                FacingRight = facingRight,
                ActionDuration = actionDuration,
                FadeDuration = Math.Max(60, fadeDuration)
            };
        }

        public void BeginMeleeAfterImageFade(int currentTime)
        {
            if (MeleeAfterImage == null || MeleeAfterImage.FadeStartTime >= 0)
            {
                return;
            }

            int lastFrameIndex = MeleeAfterImage.LastFrameIndex;
            IReadOnlyList<AfterimageRenderableLayer> lastResolvedLayers = MeleeAfterImage.LastResolvedLayers;
            MeleeAfterimagePlaybackResolver.CaptureFadeSnapshotOrClearCache(
                Assembler,
                MeleeAfterImage.ActionName,
                MeleeAfterImage.AfterImageAction,
                Math.Max(0, currentTime - MeleeAfterImage.AnimationStartTime),
                ref lastFrameIndex,
                ref lastResolvedLayers);
            MeleeAfterImage.LastFrameIndex = lastFrameIndex;
            MeleeAfterImage.LastResolvedLayers = lastResolvedLayers;

            MeleeAfterImage.FadeStartTime = currentTime;
        }

        public void UpdateMeleeAfterImage(int currentTime)
        {
            if (MeleeAfterImage == null)
            {
                return;
            }

            if (currentTime < MeleeAfterImage.ActivationStartTime)
            {
                if (!string.Equals(ActionName, MeleeAfterImage.ActionName, StringComparison.OrdinalIgnoreCase)
                    || (MeleeAfterImage.ActionDuration > 0
                        && currentTime - MeleeAfterImage.AnimationStartTime >= MeleeAfterImage.ActionDuration))
                {
                    MeleeAfterImage = null;
                }

                return;
            }

            if (MeleeAfterImage.FadeStartTime >= 0)
            {
                if (currentTime - MeleeAfterImage.FadeStartTime >= MeleeAfterImage.FadeDuration)
                {
                    MeleeAfterImage = null;
                }

                return;
            }

            if (!string.Equals(ActionName, MeleeAfterImage.ActionName, StringComparison.OrdinalIgnoreCase))
            {
                BeginMeleeAfterImageFade(currentTime);
                return;
            }

            if (MeleeAfterImage.ActionDuration > 0
                && currentTime - MeleeAfterImage.AnimationStartTime >= MeleeAfterImage.ActionDuration)
            {
                BeginMeleeAfterImageFade(currentTime);
            }
        }

        public void ClearMeleeAfterImage()
        {
            MeleeAfterImage = null;
        }

        public string Describe()
        {
            string helperText = HelperMarkerType?.ToString() ?? "none";
            string teamText = BattlefieldTeamId?.ToString() ?? "none";
            string preparedText = PreparedSkill != null ? PreparedSkill.SkillId.ToString() : "none";
            string chairPairText = PreferredPortableChairPairCharacterId?.ToString() ?? "none";
            string itemEffectText = RelationshipOverlays.Count > 0
                ? string.Join(
                    ",",
                    RelationshipOverlays
                        .OrderBy(static entry => entry.Key)
                        .Select(static entry =>
                            $"{entry.Key}:{entry.Value.ItemId}:{entry.Value.PairCharacterId?.ToString() ?? "none"}"))
                : "none";
            string carryItemEffectText = CarryItemEffectId?.ToString() ?? "none";
            string setItemText = CompletedSetItemId > 0 ? CompletedSetItemId.ToString() : "none";
            string activeEffectText = PacketOwnedEmotion != null
                ? $"{PacketOwnedEmotion.ItemId}:{PacketOwnedEmotion.EmotionName}"
                : "none";
            string ridingVehicleText = RidingVehicleId > 0 ? RidingVehicleId.ToString() : "none";
            string shadowPartnerText = TemporaryStatShadowPartnerSkillId?.ToString() ?? "none";
            string followDriverText = FollowDriverId > 0 ? FollowDriverId.ToString() : "none";
            string followPassengerText = FollowPassengerId > 0 ? FollowPassengerId.ToString() : "none";
            return $"{CharacterId}:{Name}@({Position.X:0},{Position.Y:0}) action={ActionName} source={SourceTag} helper={helperText} team={teamText} prep={preparedText} chairPair={chairPairText} itemEffect={itemEffectText} activeEffect={activeEffectText} carry={carryItemEffectText} setItem={setItemText} ridingVehicle={ridingVehicleText} shadowPartner={shadowPartnerText} followDriver={followDriverText} followPassenger={followPassengerText}";
        }
    }

        public sealed class RemoteMeleeAfterImageState
        {
            public int SkillId { get; init; }
            public string ActionName { get; init; }
            public MeleeAfterImageAction AfterImageAction { get; init; }
            public int AnimationStartTime { get; set; }
            public int ActivationStartTime { get; init; }
            public bool FacingRight { get; init; }
            public int ActionDuration { get; init; }
            public int FadeDuration { get; init; }
            public int FadeStartTime { get; set; } = -1;
            public int LastFrameIndex { get; set; } = -1;
            public IReadOnlyList<AfterimageRenderableLayer> LastResolvedLayers { get; set; } = Array.Empty<AfterimageRenderableLayer>();
        }

    public sealed class RemotePreparedSkillState
    {
        public int SkillId { get; init; }
        public string SkillName { get; init; }
        public string SkinKey { get; init; } = "KeyDownBar";
        public int DurationMs { get; init; }
        public int PrepareDurationMs { get; init; }
        public int GaugeDurationMs { get; init; }
        public int StartTime { get; init; }
        public bool IsKeydownSkill { get; init; }
        public bool IsHolding { get; init; }
        public bool AutoEnterHold { get; init; }
        public int MaxHoldDurationMs { get; init; }
        public PreparedSkillHudTextVariant TextVariant { get; init; }
        public bool ShowText { get; init; } = true;
        public string DragonActionName { get; set; }
        public int DragonActionStartTime { get; set; } = int.MinValue;
        public int DragonOwnerActionStartTime { get; set; } = int.MinValue;
    }

    public sealed class RemoteRelationshipOverlayState
    {
        public RemoteRelationshipOverlayType RelationshipType { get; init; }
        public int ItemId { get; init; }
        public long? ItemSerial { get; init; }
        public long? PairItemSerial { get; init; }
        public int? PairCharacterId { get; init; }
        public ItemEffectAnimationSet Effect { get; init; }
        public int StartTime { get; init; }
    }

    internal readonly struct RemoteDragonHudFrameMetrics
    {
        public RemoteDragonHudFrameMetrics(int originX, int height, int delayMs)
        {
            OriginX = Math.Max(0, originX);
            Height = Math.Max(1, height);
            DelayMs = Math.Max(1, delayMs);
        }

        public int OriginX { get; }
        public int Height { get; }
        public int DelayMs { get; }
    }

    internal readonly struct RemoteDragonHudAnimationTimeline
    {
        public RemoteDragonHudAnimationTimeline(bool loop, IReadOnlyList<RemoteDragonHudFrameMetrics> frames)
        {
            Loop = loop;
            Frames = frames ?? throw new ArgumentNullException(nameof(frames));
            TotalDurationMs = Math.Max(0, frames.Sum(static frame => Math.Max(1, frame.DelayMs)));
        }

        public bool Loop { get; }
        public IReadOnlyList<RemoteDragonHudFrameMetrics> Frames { get; }
        public int TotalDurationMs { get; }

        public int ResolveFrameHeight(int elapsedMs)
        {
            return ResolveFrameMetrics(elapsedMs).Height;
        }

        public int ResolveOriginX(int elapsedMs)
        {
            return ResolveFrameMetrics(elapsedMs).OriginX;
        }

        private RemoteDragonHudFrameMetrics ResolveFrameMetrics(int elapsedMs)
        {
            if (Frames == null || Frames.Count == 0)
            {
                return new RemoteDragonHudFrameMetrics(0, 1, 1);
            }

            if (Frames.Count == 1)
            {
                return Frames[0];
            }

            int remainingMs = Math.Max(0, elapsedMs);
            if (Loop && TotalDurationMs > 0)
            {
                remainingMs %= TotalDurationMs;
            }

            for (int i = 0; i < Frames.Count; i++)
            {
                RemoteDragonHudFrameMetrics frame = Frames[i];
                if (remainingMs < frame.DelayMs || i == Frames.Count - 1)
                {
                    return frame;
                }

                remainingMs -= frame.DelayMs;
            }

            return Frames[Frames.Count - 1];
        }
    }

    internal readonly struct RemoteDragonHudMetadata
    {
        public RemoteDragonHudMetadata(
            int standOriginX,
            int moveOriginX,
            IReadOnlyDictionary<string, RemoteDragonHudAnimationTimeline> actionTimelines)
        {
            StandOriginX = standOriginX;
            MoveOriginX = moveOriginX;
            ActionTimelines = actionTimelines ?? throw new ArgumentNullException(nameof(actionTimelines));
        }

        public int StandOriginX { get; }
        public int MoveOriginX { get; }
        public IReadOnlyDictionary<string, RemoteDragonHudAnimationTimeline> ActionTimelines { get; }

        public bool HasAction(string actionName)
        {
            return !string.IsNullOrWhiteSpace(actionName)
                && ActionTimelines.ContainsKey(actionName);
        }

        public int ResolveOriginX(string actionName, int elapsedMs = 0)
        {
            if (!string.IsNullOrWhiteSpace(actionName)
                && ActionTimelines.TryGetValue(actionName, out RemoteDragonHudAnimationTimeline actionTimeline))
            {
                int actionOriginX = actionTimeline.ResolveOriginX(elapsedMs);
                if (actionOriginX > 0)
                {
                    return actionOriginX;
                }
            }

            if (string.Equals(actionName, "move", StringComparison.OrdinalIgnoreCase) && MoveOriginX > 0)
            {
                return MoveOriginX;
            }

            return StandOriginX > 0 ? StandOriginX : 79;
        }

        public int ResolveFrameHeight(string actionName, int elapsedMs)
        {
            if (!string.IsNullOrWhiteSpace(actionName)
                && ActionTimelines.TryGetValue(actionName, out RemoteDragonHudAnimationTimeline actionTimeline))
            {
                return actionTimeline.ResolveFrameHeight(elapsedMs);
            }

            if (ActionTimelines.TryGetValue("stand", out RemoteDragonHudAnimationTimeline standTimeline))
            {
                return standTimeline.ResolveFrameHeight(elapsedMs);
            }

            return 1;
        }
    }
}
