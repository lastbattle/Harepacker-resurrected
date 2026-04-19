using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Physics;
using HaCreator.MapSimulator.UI;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator.Pools
{
    public enum RemoteUserPacketType
    {
        UserFollowCharacter = -1001,
        UserDropPickup = -1002,
        UserProfile = -1003,
        UserChat = -1004,
        UserChatFromOutsideMap = -1005,
        UserTutorHire = -1006,
        UserTutorMessage = -1007,
        UserCoupleRecordAdd = -1101,
        UserCoupleRecordRemove = -1102,
        UserFriendRecordAdd = -1103,
        UserFriendRecordRemove = -1104,
        UserMarriageRecordAdd = -1105,
        UserMarriageRecordRemove = -1106,
        UserNewYearCardRecordAdd = -1107,
        UserNewYearCardRecordRemove = -1108,
        UserCoupleChairRecordAdd = -1109,
        UserCoupleChairRecordRemove = -1110,
        UserEnterField = 179,
        UserLeaveField = 180,
        UserMove = 181,
        UserMoveAction = 182,
        UserHelper = 183,
        UserBattlefieldTeam = 184,
        UserPortableChair = 210,
        UserMount = 211,
        UserPreparedSkill = 212,
        UserPreparedSkillClear = 213,
        UserMeleeAttack = 214,
        UserItemEffect = 215,
        UserAvatarModified = 223,
        UserTemporaryStatSet = 225,
        UserTemporaryStatReset = 226,
        UserMoveOfficial = 210,
        UserAttackOfficial1 = 211,
        UserAttackOfficial2 = 212,
        UserAttackOfficial3 = 213,
        UserAttackOfficial4 = 214,
        UserPreparedSkillOfficial = 215,
        UserMovingShootAttackPrepareOfficial = 216,
        UserPreparedSkillClearOfficial = 217,
        UserHitOfficial = 218,
        UserEmotionOfficial = 219,
        UserActiveEffectItemOfficial = 220,
        UserUpgradeTombOfficial = 221,
        UserPortableChairOfficial = 222,
        UserEffectOfficial = 224,
        UserReceiveHpOfficial = 227,
        UserGuildNameChangedOfficial = 228,
        UserGuildMarkChangedOfficial = 229,
        UserThrowGrenadeOfficial = 230
    }

    public readonly record struct RemoteUserEnterFieldPacket(
        int CharacterId,
        string Name,
        LoginAvatarLook AvatarLook,
        short X,
        short Y,
        bool FacingRight,
        string ActionName,
        bool IsVisibleInWorld,
        int? PortableChairItemId,
        RemoteUserTemporaryStatSnapshot TemporaryStats,
        int? Level = null,
        string GuildName = null,
        int? JobId = null,
        int? CarryItemEffect = null,
        int CompletedSetItemId = 0,
        int? ActiveEffectItemId = null);

    public readonly record struct RemoteUserChatPacket(
        int CharacterId,
        byte ChatType,
        string Text,
        bool OnlyBalloon,
        string OutsideMapCharacterName);

    public readonly record struct RemoteUserTemporaryStatSnapshot(
        int EncodedLength,
        int[] MaskWords,
        byte[] RawPayload,
        RemoteUserTemporaryStatKnownState KnownState,
        bool HasWeaponCharge,
        int WeaponChargePayloadOffset)
    {
        public bool HasPayload => RawPayload != null && RawPayload.Length > 0;

        public bool HasActiveMaskBits
        {
            get
            {
                if (MaskWords == null)
                {
                    return false;
                }

                for (int i = 0; i < MaskWords.Length; i++)
                {
                    if (MaskWords[i] != 0)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public int ActiveMaskBitCount
        {
            get
            {
                if (MaskWords == null)
                {
                    return 0;
                }

                int count = 0;
                for (int i = 0; i < MaskWords.Length; i++)
                {
                    count += CountBits(MaskWords[i]);
                }

                return count;
            }
        }

        private static int CountBits(int value)
        {
            uint remaining = unchecked((uint)value);
            int count = 0;
            while (remaining != 0)
            {
                remaining &= remaining - 1;
                count++;
            }

            return count;
        }
    }

    public readonly record struct RemoteUserTemporaryStatKnownState(
        int? Speed,
        bool HasShadowPartner,
        bool HasDarkSight,
        bool HasSoulArrow,
        bool HasSpiritJavelin,
        int? ChargeSkillId,
        int? RepeatEffectSkillId,
        int? MagicShieldSkillId,
        int? FinalCutSkillId,
        int? MorphId,
        int? GhostId,
        bool HasBarrier,
        bool HasWindWalk,
        int? MechanicMode,
        bool HasDarkAura,
        bool HasBlueAura,
        bool HasYellowAura,
        int? DarkAuraValue,
        int? BlueAuraValue,
        int? YellowAuraValue,
        bool HasBlessingArmor,
        RemoteUserTemporaryStatExtendedState ExtendedState = default)
    {
        public const int DarkAuraSkillId = 32001003;
        public const int BlueAuraSkillId = 32101002;
        public const int YellowAuraSkillId = 32101003;
        public const int AdvancedBlueAuraSkillId = 32110000;
        public const int AdvancedDarkAuraSkillId = 32120000;
        public const int AdvancedYellowAuraSkillId = 32120001;
        public const int PaladinBlessingArmorSkillId = 1220013;
        public const int BishopBlessingArmorSkillId = 2311009;

        public bool HasAnyKnownState =>
            Speed.HasValue
            || HasShadowPartner
            || HasDarkSight
            || HasSoulArrow
            || HasSpiritJavelin
            || ChargeSkillId.HasValue
            || RepeatEffectSkillId.HasValue
            || MagicShieldSkillId.HasValue
            || FinalCutSkillId.HasValue
            || MorphId.HasValue
            || GhostId.HasValue
            || HasBarrier
            || HasWindWalk
            || MechanicMode.HasValue
            || HasDarkAura
            || HasBlueAura
            || HasYellowAura
            || DarkAuraValue.HasValue
            || BlueAuraValue.HasValue
            || YellowAuraValue.HasValue
            || HasBlessingArmor
            || ExtendedState.HasAnyKnownState;

        public bool IsHiddenLikeClient => HasDarkSight || HasWindWalk || GhostId.HasValue;

        public int? ActiveAuraSkillId =>
            ResolveAuraSkillId(HasYellowAura, YellowAuraValue, YellowAuraSkillId, AdvancedYellowAuraSkillId)
            ?? ResolveAuraSkillId(HasBlueAura, BlueAuraValue, BlueAuraSkillId, AdvancedBlueAuraSkillId)
            ?? ResolveAuraSkillId(HasDarkAura, DarkAuraValue, DarkAuraSkillId, AdvancedDarkAuraSkillId);

        private static int? ResolveAuraSkillId(
            bool hasAura,
            int? auraValue,
            int baseSkillId,
            int advancedSkillId)
        {
            if (!hasAura)
            {
                return null;
            }

            int resolvedValue = auraValue.GetValueOrDefault();
            return resolvedValue switch
            {
                DarkAuraSkillId => DarkAuraSkillId,
                BlueAuraSkillId => BlueAuraSkillId,
                YellowAuraSkillId => YellowAuraSkillId,
                AdvancedBlueAuraSkillId => AdvancedBlueAuraSkillId,
                AdvancedDarkAuraSkillId => AdvancedDarkAuraSkillId,
                AdvancedYellowAuraSkillId => AdvancedYellowAuraSkillId,
                _ => resolvedValue > 0 && resolvedValue == advancedSkillId
                    ? advancedSkillId
                    : baseSkillId
            };
        }

        public int? ResolveBlessingArmorSkillId(int jobId) =>
            !HasBlessingArmor
                ? null
                : jobId switch
                {
                    >= 1220 and <= 1222 => PaladinBlessingArmorSkillId,
                    >= 2310 and <= 2312 => BishopBlessingArmorSkillId,
                    _ => BishopBlessingArmorSkillId
                };
    }

    public readonly record struct RemoteUserTemporaryStatExtendedState(
        int? AttractValue,
        int? SpiritJavelinValue,
        int? BanMapValue,
        int? DojangShieldValue,
        int? ReverseInputValue,
        int? RespectPImmuneValue,
        int? RespectMImmuneValue,
        int? DefenseAttValue,
        int? DefenseStateValue,
        bool HasDojangBerserk,
        bool HasDojangInvincible,
        int? StopPortionValue,
        int? StopMotionValue,
        int? FearValue,
        bool HasFlying,
        int? FrozenValue,
        int? SuddenDeathValue,
        byte? CycloneValue,
        bool HasSneak,
        bool HasMorewildDamageUp,
        byte? TrailingDefenseAttByte,
        byte? TrailingDefenseStateByte)
    {
        public bool HasAnyKnownState =>
            AttractValue.HasValue
            || SpiritJavelinValue.HasValue
            || BanMapValue.HasValue
            || DojangShieldValue.HasValue
            || ReverseInputValue.HasValue
            || RespectPImmuneValue.HasValue
            || RespectMImmuneValue.HasValue
            || DefenseAttValue.HasValue
            || DefenseStateValue.HasValue
            || HasDojangBerserk
            || HasDojangInvincible
            || StopPortionValue.HasValue
            || StopMotionValue.HasValue
            || FearValue.HasValue
            || HasFlying
            || FrozenValue.HasValue
            || SuddenDeathValue.HasValue
            || CycloneValue.HasValue
            || HasSneak
            || HasMorewildDamageUp
            || TrailingDefenseAttByte.HasValue
            || TrailingDefenseStateByte.HasValue;

        public bool HasDefenseState =>
            DefenseStateValue.HasValue
            || TrailingDefenseStateByte.HasValue;
    }

    public readonly record struct RemoteUserLeaveFieldPacket(int CharacterId);

    public readonly record struct RemoteUserFollowCharacterPacket(
        int CharacterId,
        int DriverId,
        bool TransferField,
        int? TransferX,
        int? TransferY);
    public readonly record struct RemoteUserMovePacket(int CharacterId, PlayerMovementSyncSnapshot Snapshot, byte MoveAction);
    public readonly record struct RemoteUserMoveActionPacket(int CharacterId, byte MoveAction);
    public readonly record struct RemoteUserPortableChairPacket(int CharacterId, int? ChairItemId, int? PairCharacterId);
    public readonly record struct RemoteUserPortableChairRecordAddPacket(int CharacterId, int ChairItemId);
    public readonly record struct RemoteUserPortableChairRecordRemovePacket(int CharacterId);
    public readonly record struct RemoteUserMountPacket(int CharacterId, int? TamingMobItemId);
    public readonly record struct RemoteUserActiveEffectItemPacket(int CharacterId, int? ItemId);
    public enum RemoteUserEffectSubtype : byte
    {
        GenericUserState = 0,
        SkillUse = 1,
        ItemMake = 18,
        MakerSkill = 19,
        ReservedEffect = 20,
        NoOp = 21,
        EffectByItem = 22,
        MakerResultMessage = 23,
        CarnivalReservedEffect = 24,
        StringEffect = 25,
        ItemSoundStringEffect = 26,
        FieldSound = 27,
        IncubatorMessage = 28,
        IncDecHp = 29,
        QuestDeliveryStart = 30,
        QuestDeliveryEnd = 31,
        EvolRingStatusBar = 32
    }

    public readonly record struct RemoteUserEffectPacket(
        int CharacterId,
        byte EffectType,
        int? Int32Value,
        byte[] RawPayload,
        int? SkillId = null,
        byte? CharacterLevel = null,
        byte? SkillLevel = null,
        string StringValue = null,
        int? SecondaryInt32Value = null)
    {
        public RemoteUserEffectSubtype? KnownSubtype => EffectType switch
        {
            (byte)RemoteUserEffectSubtype.GenericUserState => RemoteUserEffectSubtype.GenericUserState,
            (byte)RemoteUserEffectSubtype.SkillUse => RemoteUserEffectSubtype.SkillUse,
            (byte)RemoteUserEffectSubtype.ItemMake => RemoteUserEffectSubtype.ItemMake,
            (byte)RemoteUserEffectSubtype.MakerSkill => RemoteUserEffectSubtype.MakerSkill,
            (byte)RemoteUserEffectSubtype.ReservedEffect => RemoteUserEffectSubtype.ReservedEffect,
            (byte)RemoteUserEffectSubtype.NoOp => RemoteUserEffectSubtype.NoOp,
            (byte)RemoteUserEffectSubtype.EffectByItem => RemoteUserEffectSubtype.EffectByItem,
            (byte)RemoteUserEffectSubtype.MakerResultMessage => RemoteUserEffectSubtype.MakerResultMessage,
            (byte)RemoteUserEffectSubtype.CarnivalReservedEffect => RemoteUserEffectSubtype.CarnivalReservedEffect,
            (byte)RemoteUserEffectSubtype.StringEffect => RemoteUserEffectSubtype.StringEffect,
            (byte)RemoteUserEffectSubtype.ItemSoundStringEffect => RemoteUserEffectSubtype.ItemSoundStringEffect,
            (byte)RemoteUserEffectSubtype.FieldSound => RemoteUserEffectSubtype.FieldSound,
            (byte)RemoteUserEffectSubtype.IncubatorMessage => RemoteUserEffectSubtype.IncubatorMessage,
            (byte)RemoteUserEffectSubtype.IncDecHp => RemoteUserEffectSubtype.IncDecHp,
            (byte)RemoteUserEffectSubtype.QuestDeliveryStart => RemoteUserEffectSubtype.QuestDeliveryStart,
            (byte)RemoteUserEffectSubtype.QuestDeliveryEnd => RemoteUserEffectSubtype.QuestDeliveryEnd,
            (byte)RemoteUserEffectSubtype.EvolRingStatusBar => RemoteUserEffectSubtype.EvolRingStatusBar,
            _ => null
        };
    }

    public readonly record struct RemoteUserHitPacket(
        int CharacterId,
        sbyte AttackIndex,
        int Damage,
        int? MobTemplateId,
        bool MobHitFacingLeft,
        bool HasMobHit,
        byte MobHitDamagePercent,
        bool PowerGuard,
        int? MobId,
        byte? MobHitAction,
        short? MobHitX,
        short? MobHitY,
        byte IncDecType,
        byte HitFlags,
        int HpDelta,
        int? SkillId,
        byte[] RawTrailingPayload = null,
        string HitString = null);
    public readonly record struct RemoteUserEmotionPacket(int CharacterId, int EmotionId, int DurationMs, bool ByItemOption);
    public readonly record struct RemoteUserUpgradeTombPacket(int CharacterId, int ItemId, int PositionX, int PositionY);
    public readonly record struct RemoteUserReceiveHpPacket(int CharacterId, int CurrentHp, int MaxHp);
    public readonly record struct RemoteUserThrowGrenadePacket(int CharacterId, int X, int Y, int KeyDownTime, int SkillId, int SkillLevel);
    public readonly record struct RemoteUserGuildNameChangedPacket(int CharacterId, string GuildName);
    public readonly record struct RemoteUserGuildMarkChangedPacket(
        int CharacterId,
        int MarkBackgroundId,
        int MarkBackgroundColor,
        int MarkId,
        int MarkColor);
    [Flags]
    public enum RemoteUserProfilePacketFlags
    {
        Level = 1 << 0,
        Job = 1 << 1,
        Guild = 1 << 2,
        Alliance = 1 << 3,
        Fame = 1 << 4,
        WorldRank = 1 << 5,
        JobRank = 1 << 6,
        Ride = 1 << 7,
        PendantSlot = 1 << 8,
        PocketSlot = 1 << 9,
        TraitCharisma = 1 << 10,
        TraitInsight = 1 << 11,
        TraitWill = 1 << 12,
        TraitCraft = 1 << 13,
        TraitSense = 1 << 14,
        TraitCharm = 1 << 15,
        Medal = 1 << 16,
        Collection = 1 << 17
    }

    public readonly record struct RemoteUserProfilePacket(
        int CharacterId,
        int? Level,
        int? JobId,
        string GuildName,
        string AllianceName,
        int? Fame,
        int? WorldRank,
        int? JobRank,
        bool? HasRide,
        bool? HasPendantSlot,
        bool? HasPocketSlot,
        int? TraitCharisma,
        int? TraitInsight,
        int? TraitWill,
        int? TraitCraft,
        int? TraitSense,
        int? TraitCharm,
        bool? HasMedal,
        bool? HasCollection);
    public readonly record struct RemoteUserTemporaryStatSetPacket(int CharacterId, RemoteUserTemporaryStatSnapshot TemporaryStats, ushort Delay);
    public readonly record struct RemoteUserTemporaryStatResetPacket(int CharacterId, int[] MaskWords);
    public readonly record struct RemoteUserPreparedSkillPacket(
        int CharacterId,
        int SkillId,
        int DurationMs,
        int GaugeDurationMs,
        int MaxHoldDurationMs,
        bool IsKeydownSkill,
        bool IsHolding,
        bool AutoEnterHold,
        bool ShowText,
        string SkinKey,
        string SkillName);

    public readonly record struct RemoteUserPreparedSkillClearPacket(int CharacterId);
    public readonly record struct RemoteUserMovingShootAttackPreparePacket(
        int CharacterId,
        int CharacterLevel,
        int SkillLevel,
        int SkillId,
        int ActionCode,
        string ActionName,
        bool FacingRight,
        int ActionSpeed);

    public readonly record struct RemoteUserDropPickupPacket(
        int DropId,
        int ActorId,
        DropPickupActorKind ActorKind,
        string ActorName,
        int FallbackOwnerId = 0,
        short? TargetX = null,
        short? TargetY = null)
    {
        public bool HasExplicitTargetPosition => TargetX.HasValue && TargetY.HasValue;
    }

    public readonly record struct RemoteUserMeleeAttackPacket(
        int CharacterId,
        int SkillId,
        int MasteryPercent,
        int ChargeSkillId,
        int? HitCount,
        int? DamagePerMob,
        int? ActionSpeed,
        int? BulletItemId,
        byte? SerialAttackFlags,
        bool IsSerialAttack,
        int? PreparedSkillReleaseFollowUpValue,
        IReadOnlyList<RemoteUserMeleeAttackMobHit> MobHits,
        bool? FacingRight,
        string ActionName,
        int? ActionCode);
    public readonly record struct RemoteUserMeleeAttackMobHit(
        int MobId,
        byte HitAction,
        IReadOnlyList<RemoteUserMeleeAttackDamageEntry> DamageEntries);
    public readonly record struct RemoteUserMeleeAttackDamageEntry(
        byte? HitFlag,
        int Damage);
    public readonly record struct RemoteUserItemEffectPacket(
        int CharacterId,
        int? ItemId,
        int? PairCharacterId,
        RemoteRelationshipOverlayType RelationshipType);
    public enum RemoteRelationshipRecordDispatchKeyKind
    {
        None = 0,
        LargeIntegerSerial = 1,
        CharacterId = 2,
        NewYearCardSerial = 3
    }

    public readonly record struct RemoteRelationshipRecordDispatchKey(
        RemoteRelationshipRecordDispatchKeyKind Kind,
        long? Serial,
        int? CharacterId)
    {
        public bool HasValue => Kind != RemoteRelationshipRecordDispatchKeyKind.None;
    }
    public readonly record struct RemoteUserRelationshipRecord(
        bool IsActive,
        int ItemId,
        long? ItemSerial,
        long? PairItemSerial,
        int? CharacterId,
        int? PairCharacterId);
    public enum RemoteRelationshipRecordAddPayloadKind
    {
        ExpandedRecord = 0,
        PairLookup = 1
    }
    public readonly record struct RemoteUserRelationshipRecordPacket(
        RemoteRelationshipOverlayType RelationshipType,
        RemoteUserRelationshipRecord RelationshipRecord,
        RemoteRelationshipRecordDispatchKey DispatchKey,
        RemoteRelationshipRecordAddPayloadKind PayloadKind = RemoteRelationshipRecordAddPayloadKind.ExpandedRecord,
        long? PairLookupSerial = null);
    public readonly record struct RemoteUserRelationshipRecordRemovePacket(
        RemoteRelationshipOverlayType RelationshipType,
        RemoteRelationshipRecordDispatchKey DispatchKey,
        long? ItemSerial,
        int? CharacterId);
    public readonly record struct RemoteUserAvatarModifiedPacket(
        int CharacterId,
        LoginAvatarLook AvatarLook,
        int? Speed,
        int? CarryItemEffect,
        RemoteUserRelationshipRecord CoupleRecord,
        RemoteUserRelationshipRecord FriendshipRecord,
        RemoteUserRelationshipRecord MarriageRecord,
        RemoteUserRelationshipRecord NewYearCardRecord,
        int CompletedSetItemId,
        bool AvatarLookModified = false);
    public readonly record struct RemoteUserHelperPacket(int CharacterId, MinimapUI.HelperMarkerType? MarkerType, bool ShowDirectionOverlay);
    public readonly record struct RemoteUserBattlefieldTeamPacket(int CharacterId, int? TeamId);
    public static class RemoteUserPacketCodec
    {
        private static bool HasProfileFlag(int flags, RemoteUserProfilePacketFlags flag)
        {
            return (flags & (int)flag) != 0;
        }

        private const int HelperNumericPayloadLength = sizeof(int) + sizeof(byte) + sizeof(byte);
        private const int HelperNamedPayloadMinimumLength = sizeof(int) + sizeof(byte) + sizeof(byte);
        private const int HelperMapleStringNamedPayloadMinimumLength = sizeof(int) + sizeof(ushort) + sizeof(byte);
        private const byte HelperMarkerClearValue = byte.MaxValue;

        private const int OfficialEnterFieldSuffixLength = sizeof(int) * 6 + sizeof(short) * 2 + sizeof(byte);

        private enum RemoteTemporaryStatMaskBit
        {
            Speed = 0,
            ComboCounter = 1,
            WeaponCharge = 2,
            Stun = 3,
            Darkness = 4,
            Seal = 5,
            Weakness = 6,
            Curse = 7,
            Poison = 8,
            ShadowPartner = 9,
            DarkSight = 10,
            SoulArrow = 11,
            Morph = 12,
            Ghost = 13,
            Attract = 14,
            SpiritJavelin = 15,
            BanMap = 16,
            Barrier = 17,
            DojangShield = 18,
            ReverseInput = 19,
            RespectPImmune = 20,
            RespectMImmune = 21,
            DefenseAtt = 22,
            DefenseState = 23,
            DojangBerserk = 24,
            DojangInvincible = 25,
            WindWalk = 26,
            RepeatEffect = 27,
            StopPortion = 28,
            StopMotion = 29,
            Fear = 30,
            MagicShield = 31,
            Flying = 32,
            Frozen = 33,
            SuddenDeath = 34,
            FinalCut = 35,
            Cyclone = 36,
            Sneak = 37,
            MorewildDamageUp = 38,
            Mechanic = 39,
            DarkAura = 40,
            BlueAura = 41,
            YellowAura = 42,
            BlessingArmor = 43
        }

        private const int NewYearCardDefaultItemId = 4300000;

        public static bool TryParseEnterField(ReadOnlySpan<byte> payload, out RemoteUserEnterFieldPacket packet, out string error)
        {
            packet = default;
            error = null;

            if (TryParseCompactEnterField(payload, out packet, out error))
            {
                return true;
            }

            string compactError = error;
            if (TryParseOfficialEnterField(payload, out packet, out error))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(compactError) && !string.IsNullOrWhiteSpace(error))
            {
                error = $"{error} Compact parse also failed: {compactError}";
            }

            return false;
        }

        public static bool TryParseFollowCharacter(
            ReadOnlySpan<byte> payload,
            out RemoteUserFollowCharacterPacket packet,
            out string error,
            int? characterIdOverride = null)
        {
            packet = default;
            error = null;

            if (characterIdOverride.HasValue)
            {
                if (TryParseOfficialFollowCharacter(payload, characterIdOverride.Value, out packet, out error))
                {
                    return true;
                }

                string officialError = error;
                if (TryParseCompactFollowCharacter(payload, out packet, out error))
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(officialError) && !string.IsNullOrWhiteSpace(error))
                {
                    error = $"{error} Official parse also failed: {officialError}";
                }

                return false;
            }

            return TryParseCompactFollowCharacter(payload, out packet, out error);
        }

        public static bool TryParseClientFollowCharacter(
            ReadOnlySpan<byte> payload,
            int localCharacterId,
            out RemoteUserFollowCharacterPacket packet,
            out string error)
        {
            error = null;
            if (localCharacterId > 0
                && TryParseOfficialFollowCharacter(payload, localCharacterId, out packet, out error))
            {
                return true;
            }

            string officialPayloadError = error;
            if (TryParseCompactFollowCharacter(payload, out packet, out error))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(officialPayloadError) && !string.IsNullOrWhiteSpace(error))
            {
                error = $"{error} Official CUserPool::OnUserCommonPacket parse also failed: {officialPayloadError}";
            }
            else if (!string.IsNullOrWhiteSpace(officialPayloadError))
            {
                error = officialPayloadError;
            }

            return false;
        }

        private static bool TryParseCompactFollowCharacter(ReadOnlySpan<byte> payload, out RemoteUserFollowCharacterPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                var reader = new PacketReader(payload);
                int characterId = reader.ReadInt32();
                int driverId = reader.ReadInt32();
                bool transferField = false;
                int? transferX = null;
                int? transferY = null;

                if (driverId == 0)
                {
                    transferField = reader.ReadByte() != 0;
                    if (transferField)
                    {
                        transferX = reader.ReadInt32();
                        transferY = reader.ReadInt32();
                    }
                }

                if (reader.RemainingLength != 0)
                {
                    error = $"Remote user compact follow packet has {reader.RemainingLength} unread bytes remaining.";
                    return false;
                }

                packet = new RemoteUserFollowCharacterPacket(characterId, driverId, transferField, transferX, transferY);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryParseOfficialFollowCharacter(
            ReadOnlySpan<byte> payload,
            int characterId,
            out RemoteUserFollowCharacterPacket packet,
            out string error)
        {
            packet = default;
            error = null;

            try
            {
                var reader = new PacketReader(payload);
                int driverId = reader.ReadInt32();
                bool transferField = false;
                int? transferX = null;
                int? transferY = null;

                if (driverId == 0)
                {
                    transferField = reader.ReadByte() != 0;
                    if (transferField)
                    {
                        transferX = reader.ReadInt32();
                        transferY = reader.ReadInt32();
                    }
                }

                if (reader.RemainingLength != 0)
                {
                    error = $"Remote user official follow packet has {reader.RemainingLength} unread bytes remaining.";
                    return false;
                }

                packet = new RemoteUserFollowCharacterPacket(characterId, driverId, transferField, transferX, transferY);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryParseCompactEnterField(ReadOnlySpan<byte> payload, out RemoteUserEnterFieldPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                var reader = new PacketReader(payload);
                int characterId = reader.ReadInt32();
                short x = reader.ReadInt16();
                short y = reader.ReadInt16();
                bool facingRight = reader.ReadByte() == 0;
                bool isVisibleInWorld = reader.ReadByte() != 0;
                string name = reader.ReadString8();
                string actionName = reader.ReadString8();
                int avatarLookLength = reader.ReadInt32();
                if (avatarLookLength < 0 || !reader.CanRead(avatarLookLength))
                {
                    error = $"Remote user enter packet AvatarLook length {avatarLookLength} is invalid.";
                    return false;
                }

                byte[] avatarLookPayload = reader.ReadBytes(avatarLookLength);
                if (!LoginAvatarLookCodec.TryDecode(avatarLookPayload, out LoginAvatarLook avatarLook, out string avatarError))
                {
                    error = avatarError ?? "Remote user enter packet AvatarLook payload could not be decoded.";
                    return false;
                }

                packet = new RemoteUserEnterFieldPacket(
                    characterId,
                    name,
                    avatarLook,
                    x,
                    y,
                    facingRight,
                    actionName,
                    isVisibleInWorld,
                    null,
                    default,
                    null,
                    null,
                    null,
                    null,
                    0,
                    null);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryParseOfficialEnterField(ReadOnlySpan<byte> payload, out RemoteUserEnterFieldPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                var reader = new PacketReader(payload);
                int characterId = reader.ReadInt32();
                int level = reader.ReadByte();
                string name = reader.ReadString16();
                string guildName = reader.ReadString16();
                reader.ReadBytes(6);

                int temporaryStatOffset = reader.Offset;
                int avatarLookOffset = FindOfficialAvatarLookOffset(payload, temporaryStatOffset, out error);
                if (avatarLookOffset < 0)
                {
                    return false;
                }

                RemoteUserTemporaryStatSnapshot temporaryStats =
                    DecodeOfficialRemoteTemporaryStats(payload, temporaryStatOffset, avatarLookOffset);

                var avatarReader = new PacketReader(payload.Slice(avatarLookOffset));
                if (!LoginAvatarLookCodec.TryDecode(avatarReader.ReadRemainingBytes(), out LoginAvatarLook avatarLook, out string avatarError))
                {
                    error = avatarError ?? "Remote user official enter packet AvatarLook payload could not be decoded.";
                    return false;
                }

                avatarReader = new PacketReader(payload.Slice(avatarLookOffset));
                byte[] avatarPayload = avatarReader.ReadRemainingBytes();
                if (!LoginAvatarLookCodec.TryDecode(avatarPayload, out avatarLook, out avatarError))
                {
                    error = avatarError ?? "Remote user official enter packet AvatarLook payload could not be decoded.";
                    return false;
                }

                int consumedAvatarBytes = LoginAvatarLookCodec.Encode(avatarLook).Length;
                int remainingBytes = payload.Length - avatarLookOffset - consumedAvatarBytes;
                if (remainingBytes != OfficialEnterFieldSuffixLength)
                {
                    error = $"Remote user official enter packet AvatarLook left {remainingBytes} trailing bytes; expected {OfficialEnterFieldSuffixLength}.";
                    return false;
                }

                avatarReader = new PacketReader(payload.Slice(avatarLookOffset + consumedAvatarBytes));
                avatarReader.ReadInt32();
                avatarReader.ReadInt32();
                int carryItemEffect = avatarReader.ReadInt32();
                int activeEffectItemId = avatarReader.ReadInt32();
                int completedSetItemId = avatarReader.ReadInt32();
                int portableChairItemId = avatarReader.ReadInt32();
                short x = avatarReader.ReadInt16();
                short y = avatarReader.ReadInt16();
                byte moveAction = avatarReader.ReadByte();

                packet = new RemoteUserEnterFieldPacket(
                    characterId,
                    string.IsNullOrWhiteSpace(name) ? $"Remote{characterId}" : name.Trim(),
                    avatarLook,
                    x,
                    y,
                    DecodeFacingRight(moveAction),
                    ResolveActionNameFromMoveAction(moveAction, portableChairItemId),
                    true,
                    portableChairItemId > 0 ? portableChairItemId : null,
                    temporaryStats,
                    level > 0 ? level : null,
                    guildName?.Trim() ?? string.Empty,
                    reader.ReadInt16(),
                    carryItemEffect,
                    completedSetItemId,
                    activeEffectItemId);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseLeaveField(ReadOnlySpan<byte> payload, out RemoteUserLeaveFieldPacket packet, out string error)
        {
            packet = default;
            error = null;
            if (payload.Length != sizeof(int))
            {
                error = $"Remote user leave packet expects 4 bytes but received {payload.Length}.";
                return false;
            }

            int characterId = payload[0]
                | (payload[1] << 8)
                | (payload[2] << 16)
                | (payload[3] << 24);
            packet = new RemoteUserLeaveFieldPacket(characterId);
            return true;
        }

        public static bool TryParseMove(ReadOnlySpan<byte> payload, int currentTime, out RemoteUserMovePacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                var reader = new PacketReader(payload);
                int characterId = reader.ReadInt32();
                if (!TryDecodeMoveSnapshotPayload(
                        payload.Slice(reader.Offset),
                        currentTime,
                        decodePassiveTail: false,
                        requireExactLength: true,
                        out PlayerMovementSyncSnapshot snapshot,
                        out byte moveAction,
                        out error))
                {
                    error = $"Remote user move packet for {characterId} could not be decoded. {error}";
                    return false;
                }

                packet = new RemoteUserMovePacket(characterId, snapshot, moveAction);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseChat(
            ReadOnlySpan<byte> payload,
            bool fromOutsideOfMap,
            out RemoteUserChatPacket packet,
            out string error)
        {
            packet = default;
            error = null;

            try
            {
                var reader = new PacketReader(payload);
                int characterId = reader.ReadInt32();
                byte chatType = reader.ReadByte();
                string text = reader.ReadString16();
                bool onlyBalloon = reader.ReadByte() != 0;
                string outsideMapCharacterName = fromOutsideOfMap
                    ? reader.ReadString16()
                    : string.Empty;

                if (reader.RemainingLength != 0)
                {
                    error = $"Remote user chat packet has {reader.RemainingLength} unread bytes remaining.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    error = "Remote user chat packet text is empty.";
                    return false;
                }

                packet = new RemoteUserChatPacket(
                    characterId,
                    chatType,
                    text.Trim(),
                    onlyBalloon,
                    outsideMapCharacterName?.Trim() ?? string.Empty);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParsePassiveMove(
            ReadOnlySpan<byte> payload,
            int currentTime,
            out PlayerMovementSyncSnapshot snapshot,
            out byte moveAction,
            out string error)
        {
            snapshot = null;
            moveAction = 0;
            error = null;

            try
            {
                if (!TryDecodeMoveSnapshotPayload(
                        payload,
                        currentTime,
                        decodePassiveTail: true,
                        requireExactLength: true,
                        out snapshot,
                        out moveAction,
                        out error))
                {
                    error = $"Passive-move packet could not be decoded. {error}";
                    return false;
                }

                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseMoveAction(ReadOnlySpan<byte> payload, out RemoteUserMoveActionPacket packet, out string error)
        {
            packet = default;
            error = null;
            if (payload.Length != sizeof(int) + sizeof(byte))
            {
                error = $"Remote user move-action packet expects 5 bytes but received {payload.Length}.";
                return false;
            }

            int characterId = payload[0]
                | (payload[1] << 8)
                | (payload[2] << 16)
                | (payload[3] << 24);
            packet = new RemoteUserMoveActionPacket(characterId, payload[4]);
            return true;
        }

        public static bool TryParsePortableChair(ReadOnlySpan<byte> payload, out RemoteUserPortableChairPacket packet, out string error)
        {
            packet = default;
            error = null;
            if (!TryParseOptionalItemPacket(payload, "portable-chair", out int characterId, out int? itemId, out error, out int? pairCharacterId))
            {
                return false;
            }

            packet = new RemoteUserPortableChairPacket(characterId, itemId, pairCharacterId);
            return true;
        }

        public static bool TryParsePortableChairOfficial(ReadOnlySpan<byte> payload, out RemoteUserPortableChairPacket packet, out string error)
        {
            packet = default;
            error = null;
            if (!TryParseOptionalItemPacket(
                    payload,
                    "official portable-chair",
                    out int characterId,
                    out int? itemId,
                    out error,
                    out int? pairCharacterId))
            {
                return false;
            }

            packet = new RemoteUserPortableChairPacket(characterId, itemId, pairCharacterId);
            return true;
        }

        public static bool TryParsePortableChairRecordAdd(
            ReadOnlySpan<byte> payload,
            out RemoteUserPortableChairRecordAddPacket packet,
            out string error)
        {
            packet = default;
            error = null;
            if (payload.Length != sizeof(int) * 2)
            {
                error = $"Remote user couple-chair record add packet expects 8 bytes but received {payload.Length}.";
                return false;
            }

            try
            {
                var reader = new PacketReader(payload);
                int characterId = reader.ReadInt32();
                int chairItemId = reader.ReadInt32();
                if (characterId <= 0)
                {
                    error = $"Remote user couple-chair record add packet character ID {characterId} is invalid.";
                    return false;
                }

                if (chairItemId <= 0)
                {
                    error = $"Remote user couple-chair record add packet chair item ID {chairItemId} is invalid.";
                    return false;
                }

                packet = new RemoteUserPortableChairRecordAddPacket(characterId, chairItemId);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParsePortableChairRecordRemove(
            ReadOnlySpan<byte> payload,
            out RemoteUserPortableChairRecordRemovePacket packet,
            out string error)
        {
            packet = default;
            error = null;
            if (payload.Length != sizeof(int))
            {
                error = $"Remote user couple-chair record remove packet expects 4 bytes but received {payload.Length}.";
                return false;
            }

            try
            {
                var reader = new PacketReader(payload);
                int characterId = reader.ReadInt32();
                if (characterId <= 0)
                {
                    error = $"Remote user couple-chair record remove packet character ID {characterId} is invalid.";
                    return false;
                }

                packet = new RemoteUserPortableChairRecordRemovePacket(characterId);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseMount(ReadOnlySpan<byte> payload, out RemoteUserMountPacket packet, out string error)
        {
            packet = default;
            error = null;
            if (!TryParseOptionalItemPacket(payload, "mount", out int characterId, out int? itemId, out error, out int? _))
            {
                return false;
            }

            packet = new RemoteUserMountPacket(characterId, itemId);
            return true;
        }

        public static bool TryParseActiveEffectItem(ReadOnlySpan<byte> payload, out RemoteUserActiveEffectItemPacket packet, out string error)
        {
            packet = default;
            error = null;
            if (!TryParseOptionalItemPacket(payload, "active-effect-item", out int characterId, out int? itemId, out error, out int? _))
            {
                return false;
            }

            packet = new RemoteUserActiveEffectItemPacket(characterId, itemId);
            return true;
        }

        public static bool TryParseEmotion(ReadOnlySpan<byte> payload, out RemoteUserEmotionPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                var reader = new PacketReader(payload);
                int characterId = reader.ReadInt32();
                int emotionId = reader.ReadInt32();
                int durationMs = reader.ReadInt32();
                bool byItemOption = reader.ReadByte() != 0;
                if (reader.RemainingLength != 0)
                {
                    error = $"Remote user emotion packet has {reader.RemainingLength} unread bytes remaining.";
                    return false;
                }

                if (characterId <= 0)
                {
                    error = $"Remote user emotion packet character ID {characterId} is invalid.";
                    return false;
                }

                if (emotionId < 0)
                {
                    error = $"Remote user emotion id {emotionId} is invalid.";
                    return false;
                }

                packet = new RemoteUserEmotionPacket(characterId, emotionId, durationMs, byItemOption);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseHit(ReadOnlySpan<byte> payload, out RemoteUserHitPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                var reader = new PacketReader(payload);
                int characterId = reader.ReadInt32();
                sbyte attackIndex = unchecked((sbyte)reader.ReadByte());
                int damage = reader.ReadInt32();
                int? mobTemplateId = null;
                bool mobHitFacingLeft = false;
                bool hasMobHit = false;
                byte mobHitDamagePercent = 0;
                bool powerGuard = false;
                int? mobId = null;
                byte? mobHitAction = null;
                short? mobHitX = null;
                short? mobHitY = null;
                byte incDecType = 0;
                byte hitFlags = 0;

                if (attackIndex > -2)
                {
                    mobTemplateId = reader.ReadInt32();
                    mobHitFacingLeft = reader.ReadByte() != 0;
                    mobHitDamagePercent = reader.ReadByte();
                    hasMobHit = mobHitDamagePercent != 0;
                    if (hasMobHit)
                    {
                        powerGuard = reader.ReadByte() != 0;
                        mobId = reader.ReadInt32();
                        mobHitAction = reader.ReadByte();
                        mobHitX = reader.ReadInt16();
                        mobHitY = reader.ReadInt16();
                    }

                    incDecType = reader.ReadByte();
                    hitFlags = reader.ReadByte();
                }

                int hpDelta = reader.ReadInt32();
                int? skillId = null;
                if (hpDelta == -1)
                {
                    skillId = reader.ReadInt32();
                }

                byte[] rawTrailingPayload = null;
                string hitString = null;
                if (reader.RemainingLength != 0)
                {
                    rawTrailingPayload = reader.ReadRemainingBytes();
                    hitString = TryParseOptionalHitString(rawTrailingPayload);
                }

                if (characterId <= 0)
                {
                    error = $"Remote user hit packet character ID {characterId} is invalid.";
                    return false;
                }

                packet = new RemoteUserHitPacket(
                    characterId,
                    attackIndex,
                    damage,
                    mobTemplateId,
                    mobHitFacingLeft,
                    hasMobHit,
                    mobHitDamagePercent,
                    powerGuard,
                    mobId,
                    mobHitAction,
                    mobHitX,
                    mobHitY,
                    incDecType,
                    hitFlags,
                    hpDelta,
                    skillId,
                    rawTrailingPayload,
                    hitString);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static string TryParseOptionalHitString(ReadOnlySpan<byte> payload)
        {
            if (payload.Length == 0)
            {
                return null;
            }

            try
            {
                if (payload.Length >= sizeof(ushort))
                {
                    var reader = new PacketReader(payload);
                    string value = reader.ReadString16();
                    if (reader.RemainingLength == 0 && !string.IsNullOrWhiteSpace(value))
                    {
                        return value.Trim();
                    }
                }
            }
            catch (InvalidOperationException)
            {
            }

            string utf8 = Encoding.UTF8.GetString(payload).TrimEnd('\0').Trim();
            if (string.IsNullOrWhiteSpace(utf8))
            {
                return null;
            }

            for (int i = 0; i < utf8.Length; i++)
            {
                if (char.IsControl(utf8[i]))
                {
                    return null;
                }
            }

            return utf8;
        }

        public static bool TryParseEffect(ReadOnlySpan<byte> payload, out RemoteUserEffectPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                var reader = new PacketReader(payload);
                int characterId = reader.ReadInt32();
                byte effectType = reader.ReadByte();
                byte[] effectPayload = reader.ReadRemainingBytes();

                if (characterId <= 0)
                {
                    error = $"Remote user effect packet character ID {characterId} is invalid.";
                    return false;
                }

                int? int32Value = null;
                int? skillId = null;
                byte? characterLevel = null;
                byte? skillLevel = null;
                string stringValue = null;
                int? secondaryInt32Value = null;
                switch ((RemoteUserEffectSubtype)effectType)
                {
                    case RemoteUserEffectSubtype.GenericUserState:
                    case RemoteUserEffectSubtype.MakerSkill:
                    case RemoteUserEffectSubtype.NoOp:
                    case RemoteUserEffectSubtype.IncubatorMessage:
                    case RemoteUserEffectSubtype.QuestDeliveryEnd:
                    case RemoteUserEffectSubtype.EvolRingStatusBar:
                        if (effectPayload.Length != 0)
                        {
                            error = $"Remote user effect subtype {effectType} expects no trailing payload but received {effectPayload.Length} bytes.";
                            return false;
                        }

                        break;

                    case RemoteUserEffectSubtype.SkillUse:
                        if (effectPayload.Length < sizeof(int) + 2)
                        {
                            error = $"Remote user effect subtype {effectType} expects at least 6 trailing bytes but received {effectPayload.Length}.";
                            return false;
                        }

                        skillId = NormalizeEffectSkillUseSkillId(BinaryPrimitives.ReadInt32LittleEndian(effectPayload.AsSpan(0, sizeof(int))));
                        characterLevel = effectPayload[sizeof(int)];
                        skillLevel = effectPayload[sizeof(int) + 1];
                        break;

                    case RemoteUserEffectSubtype.ReservedEffect:
                    case RemoteUserEffectSubtype.CarnivalReservedEffect:
                    case RemoteUserEffectSubtype.FieldSound:
                        {
                            var payloadReader = new PacketReader(effectPayload);
                            stringValue = payloadReader.ReadString16();
                            if (payloadReader.RemainingLength != 0)
                            {
                                error = $"Remote user effect subtype {effectType} has {payloadReader.RemainingLength} unread bytes remaining.";
                                return false;
                            }

                            break;
                        }

                    case RemoteUserEffectSubtype.ItemMake:
                    case RemoteUserEffectSubtype.EffectByItem:
                    case RemoteUserEffectSubtype.IncDecHp:
                    case RemoteUserEffectSubtype.QuestDeliveryStart:
                        if (effectPayload.Length != sizeof(int))
                        {
                            error = $"Remote user effect subtype {effectType} expects 4 trailing bytes but received {effectPayload.Length}.";
                            return false;
                        }

                        int32Value = BinaryPrimitives.ReadInt32LittleEndian(effectPayload);
                        break;

                    case RemoteUserEffectSubtype.MakerResultMessage:
                        if (effectPayload.Length != sizeof(byte))
                        {
                            error = $"Remote user effect subtype {effectType} expects 1 trailing byte but received {effectPayload.Length}.";
                            return false;
                        }

                        int32Value = effectPayload[0];
                        break;

                    case RemoteUserEffectSubtype.StringEffect:
                        {
                            var payloadReader = new PacketReader(effectPayload);
                            stringValue = payloadReader.ReadString16();
                            secondaryInt32Value = payloadReader.ReadInt32();
                            if (payloadReader.RemainingLength != 0)
                            {
                                error = $"Remote user effect subtype {effectType} has {payloadReader.RemainingLength} unread bytes remaining.";
                                return false;
                            }

                            break;
                        }

                    case RemoteUserEffectSubtype.ItemSoundStringEffect:
                        {
                            var payloadReader = new PacketReader(effectPayload);
                            int32Value = payloadReader.ReadInt32();
                            stringValue = payloadReader.ReadString16();
                            if (payloadReader.RemainingLength != 0)
                            {
                                error = $"Remote user effect subtype {effectType} has {payloadReader.RemainingLength} unread bytes remaining.";
                                return false;
                            }

                            break;
                        }
                }

                packet = new RemoteUserEffectPacket(
                    characterId,
                    effectType,
                    int32Value,
                    effectPayload,
                    skillId,
                    characterLevel,
                    skillLevel,
                    stringValue,
                    secondaryInt32Value);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static int NormalizeEffectSkillUseSkillId(int skillId)
        {
            // CUser::OnEffect remaps these packet-owned Mechanic ids onto the authored skill rows
            // before the client resolves appointed actions or avatar/world effect branches.
            return skillId switch
            {
                35000001 => 35001001,
                35100004 => 35101004,
                35100009 => 35101009,
                35110004 => 35111004,
                35120005 => 35121005,
                35120013 => 35121013,
                _ => skillId
            };
        }

        public static bool TryParseUpgradeTombEffect(ReadOnlySpan<byte> payload, out RemoteUserUpgradeTombPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                var reader = new PacketReader(payload);
                int characterId = reader.ReadInt32();
                int itemId = reader.ReadInt32();
                int positionX = reader.ReadInt32();
                int positionY = reader.ReadInt32();
                if (reader.RemainingLength != 0)
                {
                    error = $"Remote user upgrade-tomb packet has {reader.RemainingLength} unread bytes remaining.";
                    return false;
                }

                if (characterId <= 0)
                {
                    error = $"Remote user upgrade-tomb packet character ID {characterId} is invalid.";
                    return false;
                }

                if (itemId <= 0)
                {
                    error = $"Remote user upgrade-tomb packet item ID {itemId} is invalid.";
                    return false;
                }

                packet = new RemoteUserUpgradeTombPacket(characterId, itemId, positionX, positionY);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseGuildNameChanged(ReadOnlySpan<byte> payload, out RemoteUserGuildNameChangedPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                var reader = new PacketReader(payload);
                int characterId = reader.ReadInt32();
                string guildName = reader.ReadString16();
                if (reader.RemainingLength != 0)
                {
                    error = $"Remote user guild-name packet has {reader.RemainingLength} unread bytes remaining.";
                    return false;
                }

                if (characterId <= 0)
                {
                    error = $"Remote user guild-name packet character ID {characterId} is invalid.";
                    return false;
                }

                packet = new RemoteUserGuildNameChangedPacket(characterId, guildName);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseReceiveHp(ReadOnlySpan<byte> payload, out RemoteUserReceiveHpPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                var reader = new PacketReader(payload);
                int characterId = reader.ReadInt32();
                int currentHp = reader.ReadInt32();
                int maxHp = reader.ReadInt32();
                if (reader.RemainingLength != 0)
                {
                    error = $"Remote user receive-HP packet has {reader.RemainingLength} unread bytes remaining.";
                    return false;
                }

                if (characterId <= 0)
                {
                    error = $"Remote user receive-HP packet character ID {characterId} is invalid.";
                    return false;
                }

                if (currentHp < 0)
                {
                    error = $"Remote user receive-HP packet current HP {currentHp} is invalid.";
                    return false;
                }

                if (maxHp <= 0)
                {
                    error = $"Remote user receive-HP packet max HP {maxHp} is invalid.";
                    return false;
                }

                packet = new RemoteUserReceiveHpPacket(characterId, Math.Min(currentHp, maxHp), maxHp);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseThrowGrenade(ReadOnlySpan<byte> payload, out RemoteUserThrowGrenadePacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                var reader = new PacketReader(payload);
                int characterId = reader.ReadInt32();
                int x = reader.ReadInt32();
                int y = reader.ReadInt32();
                int keyDownTime = reader.ReadInt32();
                int skillId = reader.ReadInt32();
                // CUserRemote::OnThrowGrenade decodes the final int as `nSLV`
                // and forwards it to CUser::ThrowGrenade(nSkillID, nSLV, ...).
                int skillLevel = reader.ReadInt32();
                if (reader.RemainingLength != 0)
                {
                    error = $"Remote user throw-grenade packet has {reader.RemainingLength} unread bytes remaining.";
                    return false;
                }

                if (characterId <= 0)
                {
                    error = $"Remote user throw-grenade packet character ID {characterId} is invalid.";
                    return false;
                }

                if (skillId <= 0)
                {
                    error = $"Remote user throw-grenade packet skill ID {skillId} is invalid.";
                    return false;
                }

                packet = new RemoteUserThrowGrenadePacket(characterId, x, y, Math.Max(0, keyDownTime), skillId, skillLevel);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseGuildMarkChanged(ReadOnlySpan<byte> payload, out RemoteUserGuildMarkChangedPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                var reader = new PacketReader(payload);
                int characterId = reader.ReadInt32();
                int markBackgroundId = reader.ReadInt16();
                int markBackgroundColor = reader.ReadByte();
                int markId = reader.ReadInt16();
                int markColor = reader.ReadByte();
                if (reader.RemainingLength != 0)
                {
                    error = $"Remote user guild-mark packet has {reader.RemainingLength} unread bytes remaining.";
                    return false;
                }

                if (characterId <= 0)
                {
                    error = $"Remote user guild-mark packet character ID {characterId} is invalid.";
                    return false;
                }

                packet = new RemoteUserGuildMarkChangedPacket(
                    characterId,
                    markBackgroundId,
                    markBackgroundColor,
                    markId,
                    markColor);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseProfile(ReadOnlySpan<byte> payload, out RemoteUserProfilePacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                var reader = new PacketReader(payload);
                int characterId = reader.ReadInt32();
                int flags = reader.ReadInt32();

                int? level = HasProfileFlag(flags, RemoteUserProfilePacketFlags.Level) ? reader.ReadInt32() : null;
                int? jobId = HasProfileFlag(flags, RemoteUserProfilePacketFlags.Job) ? reader.ReadInt32() : null;
                string guildName = HasProfileFlag(flags, RemoteUserProfilePacketFlags.Guild) ? reader.ReadString16() : null;
                string allianceName = HasProfileFlag(flags, RemoteUserProfilePacketFlags.Alliance) ? reader.ReadString16() : null;
                int? fame = HasProfileFlag(flags, RemoteUserProfilePacketFlags.Fame) ? reader.ReadInt32() : null;
                int? worldRank = HasProfileFlag(flags, RemoteUserProfilePacketFlags.WorldRank) ? reader.ReadInt32() : null;
                int? jobRank = HasProfileFlag(flags, RemoteUserProfilePacketFlags.JobRank) ? reader.ReadInt32() : null;
                bool? hasRide = HasProfileFlag(flags, RemoteUserProfilePacketFlags.Ride) ? reader.ReadByte() != 0 : null;
                bool? hasPendantSlot = HasProfileFlag(flags, RemoteUserProfilePacketFlags.PendantSlot) ? reader.ReadByte() != 0 : null;
                bool? hasPocketSlot = HasProfileFlag(flags, RemoteUserProfilePacketFlags.PocketSlot) ? reader.ReadByte() != 0 : null;
                int? traitCharisma = HasProfileFlag(flags, RemoteUserProfilePacketFlags.TraitCharisma) ? reader.ReadInt32() : null;
                int? traitInsight = HasProfileFlag(flags, RemoteUserProfilePacketFlags.TraitInsight) ? reader.ReadInt32() : null;
                int? traitWill = HasProfileFlag(flags, RemoteUserProfilePacketFlags.TraitWill) ? reader.ReadInt32() : null;
                int? traitCraft = HasProfileFlag(flags, RemoteUserProfilePacketFlags.TraitCraft) ? reader.ReadInt32() : null;
                int? traitSense = HasProfileFlag(flags, RemoteUserProfilePacketFlags.TraitSense) ? reader.ReadInt32() : null;
                int? traitCharm = HasProfileFlag(flags, RemoteUserProfilePacketFlags.TraitCharm) ? reader.ReadInt32() : null;
                bool? hasMedal = HasProfileFlag(flags, RemoteUserProfilePacketFlags.Medal) ? reader.ReadByte() != 0 : null;
                bool? hasCollection = HasProfileFlag(flags, RemoteUserProfilePacketFlags.Collection) ? reader.ReadByte() != 0 : null;

                if (reader.RemainingLength != 0)
                {
                    error = $"Remote user profile packet has {reader.RemainingLength} unread bytes remaining.";
                    return false;
                }

                if (characterId <= 0)
                {
                    error = $"Remote user profile packet character ID {characterId} is invalid.";
                    return false;
                }

                packet = new RemoteUserProfilePacket(
                    characterId,
                    level,
                    jobId,
                    guildName,
                    allianceName,
                    fame,
                    worldRank,
                    jobRank,
                    hasRide,
                    hasPendantSlot,
                    hasPocketSlot,
                    traitCharisma,
                    traitInsight,
                    traitWill,
                    traitCraft,
                    traitSense,
                    traitCharm,
                    hasMedal,
                    hasCollection);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseTemporaryStatSet(ReadOnlySpan<byte> payload, out RemoteUserTemporaryStatSetPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                var reader = new PacketReader(payload);
                int characterId = reader.ReadInt32();
                byte[] remainingPayload = reader.ReadRemainingBytes();
                if (remainingPayload.Length < (sizeof(int) * 4) + sizeof(ushort))
                {
                    error = $"Remote user temporary-stat set packet expects at least {sizeof(int) + (sizeof(int) * 4) + sizeof(ushort)} bytes but received {payload.Length}.";
                    return false;
                }

                int encodedLength = remainingPayload.Length - sizeof(ushort);
                int[] maskWords = DecodeTemporaryStatMaskWords(remainingPayload.AsSpan(0, sizeof(int) * 4));
                ushort delay = (ushort)(
                    remainingPayload[encodedLength]
                    | (remainingPayload[encodedLength + 1] << 8));
                packet = new RemoteUserTemporaryStatSetPacket(
                    characterId,
                    new RemoteUserTemporaryStatSnapshot(
                        encodedLength,
                        maskWords,
                        remainingPayload.AsSpan(0, encodedLength).ToArray(),
                        DecodeKnownTemporaryStatState(remainingPayload.AsSpan(0, encodedLength), out bool hasWeaponCharge, out int weaponChargePayloadOffset),
                        hasWeaponCharge,
                        weaponChargePayloadOffset),
                    delay);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseTemporaryStatReset(ReadOnlySpan<byte> payload, out RemoteUserTemporaryStatResetPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                var reader = new PacketReader(payload);
                int characterId = reader.ReadInt32();
                if (!reader.CanRead(sizeof(int) * 4))
                {
                    error = "Remote user temporary-stat reset packet is missing the 128-bit mask.";
                    return false;
                }

                int[] maskWords = DecodeTemporaryStatMaskWords(reader.ReadBytes(sizeof(int) * 4));
                if (reader.RemainingLength != 0)
                {
                    error = $"Remote user temporary-stat reset packet has {reader.RemainingLength} unread bytes remaining.";
                    return false;
                }

                packet = new RemoteUserTemporaryStatResetPacket(characterId, maskWords);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseItemEffect(ReadOnlySpan<byte> payload, out RemoteUserItemEffectPacket packet, out string error)
        {
            packet = default;
            error = null;
            if (!TryParseOptionalItemPacket(payload, "item-effect", out int characterId, out int? itemId, out error, out int? pairCharacterId))
            {
                if (!TryParseTypedItemEffectPacket(payload, out packet, out error))
                {
                    return false;
                }

                return true;
            }

            packet = new RemoteUserItemEffectPacket(
                characterId,
                itemId,
                pairCharacterId,
                RemoteRelationshipOverlayType.Generic);
            return true;
        }

        public static bool TryParseAvatarModified(ReadOnlySpan<byte> payload, out RemoteUserAvatarModifiedPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                var reader = new PacketReader(payload);
                int characterId = reader.ReadInt32();
                if (characterId <= 0)
                {
                    error = $"Remote user avatar-modified packet character ID {characterId} is invalid.";
                    return false;
                }

                byte flags = reader.ReadByte();
                LoginAvatarLook avatarLook = null;
                if ((flags & 0x01) != 0)
                {
                    byte[] avatarPayload = payload.Slice(reader.Offset).ToArray();
                    if (!LoginAvatarLookCodec.TryDecode(avatarPayload, out avatarLook, out string avatarError))
                    {
                        error = avatarError ?? "Remote user avatar-modified packet AvatarLook payload could not be decoded.";
                        return false;
                    }

                    reader.ReadBytes(LoginAvatarLookCodec.Encode(avatarLook).Length);
                }

                int? speed = null;
                if ((flags & 0x02) != 0)
                {
                    speed = reader.ReadByte();
                }

                int? carryItemEffect = null;
                if ((flags & 0x04) != 0)
                {
                    carryItemEffect = reader.ReadByte();
                }

                bool hasCoupleRecord = reader.ReadByte() != 0;
                long? coupleItemSerial = null;
                long? couplePairItemSerial = null;
                if (hasCoupleRecord)
                {
                    coupleItemSerial = reader.ReadInt64();
                    couplePairItemSerial = reader.ReadInt64();
                }
                RemoteUserRelationshipRecord coupleRecord = hasCoupleRecord
                    ? new RemoteUserRelationshipRecord(
                        true,
                        ItemId: reader.ReadInt32(),
                        ItemSerial: coupleItemSerial,
                        PairItemSerial: couplePairItemSerial,
                        CharacterId: null,
                        PairCharacterId: null)
                    : default;

                bool hasFriendshipRecord = reader.ReadByte() != 0;
                long? friendshipItemSerial = null;
                long? friendshipPairItemSerial = null;
                if (hasFriendshipRecord)
                {
                    friendshipItemSerial = reader.ReadInt64();
                    friendshipPairItemSerial = reader.ReadInt64();
                }
                RemoteUserRelationshipRecord friendshipRecord = hasFriendshipRecord
                    ? new RemoteUserRelationshipRecord(
                        true,
                        ItemId: reader.ReadInt32(),
                        ItemSerial: friendshipItemSerial,
                        PairItemSerial: friendshipPairItemSerial,
                        CharacterId: null,
                        PairCharacterId: null)
                    : default;

                bool hasMarriageRecord = reader.ReadByte() != 0;
                RemoteUserRelationshipRecord marriageRecord = hasMarriageRecord
                    ? new RemoteUserRelationshipRecord(
                        true,
                        ItemId: 0,
                        ItemSerial: null,
                        PairItemSerial: null,
                        CharacterId: reader.ReadInt32(),
                        PairCharacterId: reader.ReadInt32())
                    : default;

                if (hasMarriageRecord)
                {
                    marriageRecord = marriageRecord with
                    {
                        ItemId = reader.ReadInt32()
                    };
                }

                bool hasNewYearCardRecord = false;
                RemoteUserRelationshipRecord newYearCardRecord = default;
                if (reader.RemainingLength > sizeof(int))
                {
                    hasNewYearCardRecord = reader.ReadByte() != 0;
                    if (hasNewYearCardRecord)
                    {
                        newYearCardRecord = new RemoteUserRelationshipRecord(
                            true,
                            ItemId: NewYearCardDefaultItemId,
                            ItemSerial: reader.ReadInt64(),
                            PairItemSerial: null,
                            CharacterId: null,
                            PairCharacterId: reader.ReadInt32());
                    }
                }

                int completedSetItemId = reader.ReadInt32();
                packet = new RemoteUserAvatarModifiedPacket(
                    characterId,
                    avatarLook,
                    speed,
                    carryItemEffect,
                    hasCoupleRecord ? coupleRecord : default,
                    hasFriendshipRecord ? friendshipRecord : default,
                    hasMarriageRecord ? marriageRecord : default,
                    hasNewYearCardRecord ? newYearCardRecord : default,
                    completedSetItemId,
                    AvatarLookModified: (flags & 0x01) != 0);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseRelationshipRecordAdd(
            int packetType,
            ReadOnlySpan<byte> payload,
            out RemoteUserRelationshipRecordPacket packet,
            out string error)
        {
            packet = default;
            error = null;

            try
            {
                var reader = new PacketReader(payload);
                switch ((RemoteUserPacketType)packetType)
                {
                    case RemoteUserPacketType.UserCoupleRecordAdd:
                    case RemoteUserPacketType.UserFriendRecordAdd:
                    {
                        if (payload.Length == sizeof(int) + sizeof(long) + sizeof(int)
                            || payload.Length == sizeof(int) + sizeof(long) + sizeof(int) + sizeof(long))
                        {
                            int recordOwnerCharacterId = reader.ReadInt32();
                            long pairLookupSerial = reader.ReadInt64();
                            int relationshipItemId = reader.ReadInt32();
                            long dispatchSerial = reader.RemainingLength >= sizeof(long)
                                ? reader.ReadInt64()
                                : pairLookupSerial;
                            packet = new RemoteUserRelationshipRecordPacket(
                                packetType == (int)RemoteUserPacketType.UserCoupleRecordAdd
                                    ? RemoteRelationshipOverlayType.Couple
                                    : RemoteRelationshipOverlayType.Friendship,
                                new RemoteUserRelationshipRecord(
                                    IsActive: true,
                                    ItemId: relationshipItemId,
                                    ItemSerial: null,
                                    PairItemSerial: null,
                                    CharacterId: recordOwnerCharacterId,
                                    PairCharacterId: null),
                                new RemoteRelationshipRecordDispatchKey(
                                    RemoteRelationshipRecordDispatchKeyKind.LargeIntegerSerial,
                                    dispatchSerial,
                                    CharacterId: null),
                                PayloadKind: RemoteRelationshipRecordAddPayloadKind.PairLookup,
                                PairLookupSerial: pairLookupSerial);
                            EnsureRelationshipRecordAddConsumed(ref reader, packetType);
                            return true;
                        }

                        int ownerCharacterId = reader.ReadInt32();
                        int pairCharacterId = reader.ReadInt32();
                        int itemId = reader.ReadInt32();
                        long itemSerial = reader.ReadInt64();
                        long pairItemSerial = reader.ReadInt64();
                        packet = new RemoteUserRelationshipRecordPacket(
                            packetType == (int)RemoteUserPacketType.UserCoupleRecordAdd
                                ? RemoteRelationshipOverlayType.Couple
                                : RemoteRelationshipOverlayType.Friendship,
                            new RemoteUserRelationshipRecord(
                                IsActive: true,
                                ItemId: itemId,
                                ItemSerial: itemSerial,
                                PairItemSerial: pairItemSerial,
                                CharacterId: ownerCharacterId,
                                PairCharacterId: pairCharacterId),
                            new RemoteRelationshipRecordDispatchKey(
                                RemoteRelationshipRecordDispatchKeyKind.LargeIntegerSerial,
                                reader.RemainingLength >= sizeof(long) ? reader.ReadInt64() : pairItemSerial,
                                CharacterId: null),
                            PayloadKind: RemoteRelationshipRecordAddPayloadKind.ExpandedRecord,
                            PairLookupSerial: null);
                        EnsureRelationshipRecordAddConsumed(ref reader, packetType);
                        return true;
                    }

                    case RemoteUserPacketType.UserMarriageRecordAdd:
                    {
                        int ownerCharacterId = reader.ReadInt32();
                        int pairCharacterId = reader.ReadInt32();
                        int itemId = reader.ReadInt32();

                        packet = new RemoteUserRelationshipRecordPacket(
                            RemoteRelationshipOverlayType.Marriage,
                            new RemoteUserRelationshipRecord(
                                IsActive: true,
                                ItemId: itemId,
                                ItemSerial: null,
                                PairItemSerial: null,
                                CharacterId: ownerCharacterId,
                                PairCharacterId: pairCharacterId),
                            new RemoteRelationshipRecordDispatchKey(
                                RemoteRelationshipRecordDispatchKeyKind.CharacterId,
                                Serial: null,
                                reader.RemainingLength >= sizeof(int) ? reader.ReadInt32() : pairCharacterId));
                        EnsureRelationshipRecordAddConsumed(ref reader, packetType);
                        return true;
                    }

                    case RemoteUserPacketType.UserNewYearCardRecordAdd:
                    {
                        int ownerCharacterId = reader.ReadInt32();
                        int pairCharacterId = reader.ReadInt32();
                        long itemSerial = (uint)reader.ReadInt32();
                        if (reader.RemainingLength >= sizeof(int))
                        {
                            _ = reader.ReadInt32();
                        }

                        packet = new RemoteUserRelationshipRecordPacket(
                            RemoteRelationshipOverlayType.NewYearCard,
                            new RemoteUserRelationshipRecord(
                                IsActive: true,
                                ItemId: 4300000,
                                ItemSerial: itemSerial,
                                PairItemSerial: null,
                                CharacterId: ownerCharacterId,
                                PairCharacterId: pairCharacterId),
                            new RemoteRelationshipRecordDispatchKey(
                                RemoteRelationshipRecordDispatchKeyKind.NewYearCardSerial,
                                itemSerial,
                                CharacterId: null));
                        EnsureRelationshipRecordAddConsumed(ref reader, packetType);
                        return true;
                    }

                    default:
                        error = $"Remote user relationship-record add packet type {packetType} is not supported.";
                        return false;
                }
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseRelationshipRecordRemove(
            int packetType,
            ReadOnlySpan<byte> payload,
            out RemoteUserRelationshipRecordRemovePacket packet,
            out string error)
        {
            packet = default;
            error = null;

            try
            {
                var reader = new PacketReader(payload);
                switch ((RemoteUserPacketType)packetType)
                {
                    case RemoteUserPacketType.UserCoupleRecordRemove:
                    case RemoteUserPacketType.UserFriendRecordRemove:
                    {
                        long itemSerial = reader.ReadInt64();
                        EnsureRelationshipRecordRemoveConsumed(ref reader, packetType);

                        packet = new RemoteUserRelationshipRecordRemovePacket(
                            packetType == (int)RemoteUserPacketType.UserCoupleRecordRemove
                                ? RemoteRelationshipOverlayType.Couple
                                : RemoteRelationshipOverlayType.Friendship,
                            new RemoteRelationshipRecordDispatchKey(
                                RemoteRelationshipRecordDispatchKeyKind.LargeIntegerSerial,
                                itemSerial,
                                CharacterId: null),
                            ItemSerial: itemSerial,
                            CharacterId: null);
                        return true;
                    }

                    case RemoteUserPacketType.UserMarriageRecordRemove:
                    {
                        int characterId = reader.ReadInt32();
                        EnsureRelationshipRecordRemoveConsumed(ref reader, packetType);

                        packet = new RemoteUserRelationshipRecordRemovePacket(
                            RemoteRelationshipOverlayType.Marriage,
                            new RemoteRelationshipRecordDispatchKey(
                                RemoteRelationshipRecordDispatchKeyKind.CharacterId,
                                Serial: null,
                                characterId),
                            ItemSerial: null,
                            CharacterId: characterId);
                        return true;
                    }

                    case RemoteUserPacketType.UserNewYearCardRecordRemove:
                    {
                        long itemSerial = (uint)reader.ReadInt32();
                        EnsureRelationshipRecordRemoveConsumed(ref reader, packetType);

                        packet = new RemoteUserRelationshipRecordRemovePacket(
                            RemoteRelationshipOverlayType.NewYearCard,
                            new RemoteRelationshipRecordDispatchKey(
                                RemoteRelationshipRecordDispatchKeyKind.NewYearCardSerial,
                                itemSerial,
                                CharacterId: null),
                            ItemSerial: itemSerial,
                            CharacterId: null);
                        return true;
                    }

                    default:
                        error = $"Remote user relationship-record remove packet type {packetType} is not supported.";
                        return false;
                }
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParsePreparedSkill(ReadOnlySpan<byte> payload, out RemoteUserPreparedSkillPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                var reader = new PacketReader(payload);
                int characterId = reader.ReadInt32();
                int skillId = reader.ReadInt32();
                int durationMs = reader.ReadInt32();
                int gaugeDurationMs = reader.ReadInt32();
                int maxHoldDurationMs = reader.ReadInt32();
                byte flags = reader.ReadByte();
                string skinKey = reader.ReadString8();
                string skillName = reader.ReadString8();
                packet = new RemoteUserPreparedSkillPacket(
                    characterId,
                    skillId,
                    durationMs,
                    gaugeDurationMs,
                    maxHoldDurationMs,
                    (flags & 0x01) != 0,
                    (flags & 0x02) != 0,
                    (flags & 0x08) != 0,
                    (flags & 0x04) != 0,
                    skinKey,
                    skillName);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParsePreparedSkillClear(ReadOnlySpan<byte> payload, out RemoteUserPreparedSkillClearPacket packet, out string error)
        {
            packet = default;
            error = null;
            if (!TryParseLeaveField(payload, out RemoteUserLeaveFieldPacket clearPacket, out error))
            {
                return false;
            }

            packet = new RemoteUserPreparedSkillClearPacket(clearPacket.CharacterId);
            return true;
        }

        public static bool TryParseMovingShootAttackPrepare(
            ReadOnlySpan<byte> payload,
            out RemoteUserMovingShootAttackPreparePacket packet,
            out string error)
        {
            packet = default;
            error = null;

            try
            {
                var reader = new PacketReader(payload);
                int characterId = reader.ReadInt32();
                if (characterId <= 0)
                {
                    error = $"Remote user moving-shoot prepare packet character ID {characterId} is invalid.";
                    return false;
                }

                int characterLevel = reader.ReadByte();
                int skillLevel = reader.ReadByte();
                int skillId = skillLevel != 0 ? reader.ReadInt32() : 0;
                int actionField = (ushort)reader.ReadInt16();
                bool facingRight = ((actionField >> 15) & 1) == 0;
                int actionCode = actionField & 0x7FFF;
                if (actionCode > 0x110)
                {
                    error = $"Remote user moving-shoot prepare packet action code {actionCode} is outside the client action table range.";
                    return false;
                }

                int actionSpeed = reader.ReadByte();
                if (reader.RemainingLength != 0)
                {
                    error = $"Remote user moving-shoot prepare packet has {reader.RemainingLength} unread byte(s) remaining.";
                    return false;
                }

                packet = new RemoteUserMovingShootAttackPreparePacket(
                    characterId,
                    characterLevel,
                    skillLevel,
                    skillId,
                    actionCode,
                    ResolveActionNameFromActionCode(actionCode),
                    facingRight,
                    actionSpeed);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseDropPickup(ReadOnlySpan<byte> payload, out RemoteUserDropPickupPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                var reader = new PacketReader(payload);
                int dropId = reader.ReadInt32();
                int actorId = reader.ReadInt32();
                byte actorKindRaw = reader.ReadByte();
                if (!Enum.IsDefined(typeof(DropPickupActorKind), (int)actorKindRaw))
                {
                    error = $"Remote user drop-pickup packet actor kind {actorKindRaw} is not recognized.";
                    return false;
                }

                string actorName = reader.ReadString8();
                int fallbackOwnerId = 0;
                short? targetX = null;
                short? targetY = null;
                if (reader.RemainingLength == sizeof(int))
                {
                    fallbackOwnerId = reader.ReadInt32();
                }
                else if (reader.RemainingLength == sizeof(int) + (sizeof(short) * 2))
                {
                    fallbackOwnerId = reader.ReadInt32();
                    targetX = reader.ReadInt16();
                    targetY = reader.ReadInt16();
                }
                else if (reader.RemainingLength != 0)
                {
                    error = $"Remote user drop-pickup packet has {reader.RemainingLength} unread bytes remaining.";
                    return false;
                }

                if (dropId <= 0)
                {
                    error = $"Remote user drop-pickup packet drop ID {dropId} is invalid.";
                    return false;
                }

                packet = new RemoteUserDropPickupPacket(
                    dropId,
                    actorId,
                    (DropPickupActorKind)actorKindRaw,
                    actorName,
                    fallbackOwnerId,
                    targetX,
                    targetY);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseMeleeAttack(ReadOnlySpan<byte> payload, out RemoteUserMeleeAttackPacket packet, out string error)
        {
            packet = default;
            error = null;

            if (TryParseCompactMeleeAttack(payload, out packet, out error))
            {
                return true;
            }

            string compactError = error;
            if (TryParseOfficialMeleeAttack(payload, out packet, out error))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(compactError) && !string.IsNullOrWhiteSpace(error))
            {
                error = $"{error} Compact parse also failed: {compactError}";
            }

            return false;
        }

        private static bool TryParseCompactMeleeAttack(ReadOnlySpan<byte> payload, out RemoteUserMeleeAttackPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                var reader = new PacketReader(payload);
                int characterId = reader.ReadInt32();
                int skillId = reader.ReadInt32();
                int masteryPercent = reader.ReadInt32();
                int chargeSkillId = reader.ReadInt32();
                byte facingRaw = reader.ReadByte();
                bool? facingRight = facingRaw switch
                {
                    0 => false,
                    1 => true,
                    byte.MaxValue => null,
                    _ => null
                };
                if (facingRaw != 0 && facingRaw != 1 && facingRaw != byte.MaxValue)
                {
                    error = $"Remote user melee packet facing value {facingRaw} is not recognized.";
                    return false;
                }

                int? actionCode = null;
                string actionName;
                if (reader.RemainingLength == 1)
                {
                    actionCode = reader.ReadByte();
                    actionName = ResolveActionNameFromActionCode(actionCode);
                }
                else
                {
                    actionName = reader.ReadString8();
                    if (reader.CanRead(1))
                    {
                        actionCode = reader.ReadByte();
                    }

                    if (string.IsNullOrWhiteSpace(actionName))
                    {
                        actionName = ResolveActionNameFromActionCode(actionCode);
                    }
                }

                packet = new RemoteUserMeleeAttackPacket(
                    CharacterId: characterId,
                    SkillId: skillId,
                    MasteryPercent: masteryPercent,
                    ChargeSkillId: chargeSkillId,
                    HitCount: null,
                    DamagePerMob: null,
                    ActionSpeed: null,
                    BulletItemId: null,
                    SerialAttackFlags: null,
                    IsSerialAttack: false,
                    PreparedSkillReleaseFollowUpValue: null,
                    MobHits: Array.Empty<RemoteUserMeleeAttackMobHit>(),
                    FacingRight: facingRight,
                    ActionName: actionName,
                    ActionCode: actionCode);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryParseOfficialMeleeAttack(ReadOnlySpan<byte> payload, out RemoteUserMeleeAttackPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                var reader = new PacketReader(payload);
                int characterId = reader.ReadInt32();
                if (characterId <= 0)
                {
                    error = $"Remote user official melee packet character ID {characterId} is invalid.";
                    return false;
                }

                byte attackCountByte = reader.ReadByte();
                int hitCount = attackCountByte >> 4;
                int damagePerMob = attackCountByte & 0x0F;

                reader.ReadByte(); // remote character level
                int skillLevel = reader.ReadByte();
                int skillId = 0;
                if (skillLevel != 0)
                {
                    skillId = reader.ReadInt32();
                }

                if (skillId == 3211006)
                {
                    int passiveSkillLevel = reader.ReadByte();
                    if (passiveSkillLevel != 0)
                    {
                        reader.ReadInt32();
                    }
                }

                byte serialAttackFlags = reader.ReadByte();
                bool isSerialAttack = (serialAttackFlags & 0x20) != 0;

                int actionField = (ushort)reader.ReadInt16();
                bool facingRight = ((actionField >> 15) & 1) == 0;
                int actionCode = actionField & 0x7FFF;
                if (actionCode > 0x110)
                {
                    error = $"Remote user official melee packet action code {actionCode} is outside the client action table range.";
                    return false;
                }

                int actionSpeed = reader.ReadByte();
                int masteryPercent = reader.ReadByte();
                int bulletItemId = reader.ReadInt32();

                IReadOnlyList<RemoteUserMeleeAttackMobHit> mobHits =
                    DecodeOfficialAttackInfoPayload(ref reader, skillId, hitCount, damagePerMob);
                int? preparedSkillReleaseFollowUpValue = DecodeOfficialPostAttackPayload(ref reader, skillId);

                string actionName = ResolveActionNameFromActionCode(actionCode);
                packet = new RemoteUserMeleeAttackPacket(
                    CharacterId: characterId,
                    SkillId: skillId,
                    MasteryPercent: masteryPercent,
                    ChargeSkillId: 0,
                    HitCount: hitCount,
                    DamagePerMob: damagePerMob,
                    ActionSpeed: actionSpeed,
                    BulletItemId: bulletItemId,
                    SerialAttackFlags: serialAttackFlags,
                    IsSerialAttack: isSerialAttack,
                    PreparedSkillReleaseFollowUpValue: preparedSkillReleaseFollowUpValue,
                    MobHits: mobHits,
                    FacingRight: facingRight,
                    ActionName: actionName,
                    ActionCode: actionCode);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseHelper(ReadOnlySpan<byte> payload, out RemoteUserHelperPacket packet, out string error)
        {
            packet = default;
            error = null;
            if (payload.Length == HelperNumericPayloadLength)
            {
                int characterId = payload[0]
                    | (payload[1] << 8)
                    | (payload[2] << 16)
                    | (payload[3] << 24);
                byte markerRaw = payload[4];
                MinimapUI.HelperMarkerType? markerType = markerRaw == HelperMarkerClearValue
                    ? null
                    : Enum.IsDefined(typeof(MinimapUI.HelperMarkerType), (int)markerRaw)
                        ? (MinimapUI.HelperMarkerType)markerRaw
                        : null;
                if (markerRaw != HelperMarkerClearValue && !markerType.HasValue)
                {
                    error = $"Remote user helper marker value {markerRaw} is not recognized.";
                    return false;
                }

                packet = new RemoteUserHelperPacket(characterId, markerType, payload[5] != 0);
                return true;
            }

            return TryParseNamedHelper(payload, out packet, out error);
        }

        public static bool TryBuildHelperPayload(int characterId, string markerName, bool showDirectionOverlay, out byte[] payload, out string error)
        {
            payload = Array.Empty<byte>();
            error = null;

            if (!TryResolveHelperMarkerName(markerName, out MinimapUI.HelperMarkerType? markerType))
            {
                error = "Helper marker must be user, another, friend, guild, guildmaster, match, party, partymaster, usertrader, anothertrader, or clear.";
                return false;
            }

            string normalizedName = ResolveHelperMarkerWzName(markerType);
            if (!markerType.HasValue)
            {
                payload = new byte[HelperNumericPayloadLength];
                payload[0] = (byte)characterId;
                payload[1] = (byte)(characterId >> 8);
                payload[2] = (byte)(characterId >> 16);
                payload[3] = (byte)(characterId >> 24);
                payload[4] = HelperMarkerClearValue;
                payload[5] = showDirectionOverlay ? (byte)1 : (byte)0;
                return true;
            }

            byte[] nameBytes = Encoding.UTF8.GetBytes(normalizedName);
            if (nameBytes.Length <= byte.MaxValue)
            {
                payload = new byte[sizeof(int) + sizeof(byte) + nameBytes.Length + sizeof(byte)];
                payload[0] = (byte)characterId;
                payload[1] = (byte)(characterId >> 8);
                payload[2] = (byte)(characterId >> 16);
                payload[3] = (byte)(characterId >> 24);
                payload[4] = (byte)nameBytes.Length;
                if (nameBytes.Length > 0)
                {
                    Buffer.BlockCopy(nameBytes, 0, payload, 5, nameBytes.Length);
                }

                payload[^1] = showDirectionOverlay ? (byte)1 : (byte)0;
                return true;
            }

            if (nameBytes.Length > ushort.MaxValue)
            {
                error = "Remote user helper marker names must fit in a 16-bit packet string.";
                return false;
            }

            payload = new byte[sizeof(int) + sizeof(ushort) + nameBytes.Length + sizeof(byte)];
            payload[0] = (byte)characterId;
            payload[1] = (byte)(characterId >> 8);
            payload[2] = (byte)(characterId >> 16);
            payload[3] = (byte)(characterId >> 24);
            payload[4] = (byte)nameBytes.Length;
            payload[5] = (byte)(nameBytes.Length >> 8);
            if (nameBytes.Length > 0)
            {
                Buffer.BlockCopy(nameBytes, 0, payload, 6, nameBytes.Length);
            }

            payload[^1] = showDirectionOverlay ? (byte)1 : (byte)0;
            return true;
        }

        internal static bool TryResolveHelperMarkerName(string markerName, out MinimapUI.HelperMarkerType? markerType)
        {
            string normalizedMarkerName = NormalizeHelperMarkerName(markerName);
            markerType = normalizedMarkerName switch
            {
                "another" => MinimapUI.HelperMarkerType.Another,
                "user" => MinimapUI.HelperMarkerType.User,
                "friend" => MinimapUI.HelperMarkerType.Friend,
                "guild" => MinimapUI.HelperMarkerType.Guild,
                "guildmaster" => MinimapUI.HelperMarkerType.GuildMaster,
                "match" => MinimapUI.HelperMarkerType.Match,
                "party" => MinimapUI.HelperMarkerType.Party,
                "partymaster" => MinimapUI.HelperMarkerType.PartyMaster,
                "usertrader" => MinimapUI.HelperMarkerType.UserTrader,
                "anothertrader" => MinimapUI.HelperMarkerType.AnotherTrader,
                "clear" or "none" => null,
                _ => null
            };

            return normalizedMarkerName != null
                && (markerType.HasValue
                    || string.Equals(normalizedMarkerName, "clear", StringComparison.Ordinal)
                    || string.Equals(normalizedMarkerName, "none", StringComparison.Ordinal));
        }

        private static string NormalizeHelperMarkerName(string markerName)
        {
            if (markerName == null)
            {
                return null;
            }

            string normalized = markerName.Trim().Replace('\\', '/');
            if (normalized.Length == 0)
            {
                return string.Empty;
            }

            string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return string.Empty;
            }

            int defaultHelperSegmentIndex = -1;
            for (int i = segments.Length - 1; i >= 0; i--)
            {
                if (!string.Equals(segments[i], "DefaultHelper", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                defaultHelperSegmentIndex = i;
                break;
            }

            if (defaultHelperSegmentIndex >= 0)
            {
                for (int j = defaultHelperSegmentIndex + 1; j < segments.Length; j++)
                {
                    string candidate = NormalizeHelperMarkerNameSegment(segments[j]);
                    if (candidate.Length == 0 || IsNumericHelperMarkerPathSegment(candidate))
                    {
                        continue;
                    }

                    return candidate;
                }
            }

            for (int i = segments.Length - 1; i >= 0; i--)
            {
                string candidate = NormalizeHelperMarkerNameSegment(segments[i]);
                if (candidate.Length == 0)
                {
                    continue;
                }

                if (IsNumericHelperMarkerPathSegment(candidate))
                {
                    continue;
                }

                return candidate;
            }

            return string.Empty;
        }

        private static string NormalizeHelperMarkerNameSegment(string segment)
        {
            string candidate = segment?.Trim();
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return string.Empty;
            }

            int extensionSeparatorIndex = candidate.LastIndexOf('.');
            if (extensionSeparatorIndex > 0)
            {
                candidate = candidate[..extensionSeparatorIndex];
            }

            return candidate.Trim().ToLowerInvariant();
        }

        private static bool IsNumericHelperMarkerPathSegment(string segment)
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                return false;
            }

            for (int i = 0; i < segment.Length; i++)
            {
                if (!char.IsDigit(segment[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryParseNamedHelper(ReadOnlySpan<byte> payload, out RemoteUserHelperPacket packet, out string error)
        {
            packet = default;
            error = null;
            if (payload.Length < HelperNamedPayloadMinimumLength)
            {
                error = $"Remote user helper packet expects the legacy 6-byte payload or a named DefaultHelper payload, but received {payload.Length} bytes.";
                return false;
            }

            int characterId = payload[0]
                | (payload[1] << 8)
                | (payload[2] << 16)
                | (payload[3] << 24);

            int nameStartIndex;
            int nameLength;
            bool uses8BitLengthPayload = payload.Length >= HelperNamedPayloadMinimumLength
                && payload.Length == sizeof(int) + sizeof(byte) + payload[4] + sizeof(byte);
            if (uses8BitLengthPayload)
            {
                nameLength = payload[4];
                nameStartIndex = 5;
            }
            else if (payload.Length >= HelperMapleStringNamedPayloadMinimumLength)
            {
                int mapleStringLength = payload[4] | (payload[5] << 8);
                if (payload.Length != sizeof(int) + sizeof(ushort) + mapleStringLength + sizeof(byte))
                {
                    error = $"Remote user named helper packet length {payload.Length} does not match its marker name length.";
                    return false;
                }

                nameLength = mapleStringLength;
                nameStartIndex = 6;
            }
            else
            {
                error = $"Remote user named helper packet length {payload.Length} does not match its marker name length.";
                return false;
            }

            string markerName = nameLength == 0
                ? "clear"
                : Encoding.UTF8.GetString(payload.Slice(nameStartIndex, nameLength));
            if (!TryResolveHelperMarkerName(markerName, out MinimapUI.HelperMarkerType? markerType))
            {
                error = $"Remote user helper marker '{markerName}' is not recognized.";
                return false;
            }

            packet = new RemoteUserHelperPacket(characterId, markerType, payload[^1] != 0);
            return true;
        }

        private static string ResolveHelperMarkerWzName(MinimapUI.HelperMarkerType? markerType)
        {
            return markerType switch
            {
                MinimapUI.HelperMarkerType.Another => "another",
                MinimapUI.HelperMarkerType.User => "user",
                MinimapUI.HelperMarkerType.Friend => "friend",
                MinimapUI.HelperMarkerType.Guild => "guild",
                MinimapUI.HelperMarkerType.GuildMaster => "guildmaster",
                MinimapUI.HelperMarkerType.Match => "match",
                MinimapUI.HelperMarkerType.Party => "party",
                MinimapUI.HelperMarkerType.PartyMaster => "partymaster",
                MinimapUI.HelperMarkerType.UserTrader => "usertrader",
                MinimapUI.HelperMarkerType.AnotherTrader => "anothertrader",
                _ => "clear"
            };
        }

        public static bool TryParseBattlefieldTeam(ReadOnlySpan<byte> payload, out RemoteUserBattlefieldTeamPacket packet, out string error)
        {
            packet = default;
            error = null;
            if (payload.Length != sizeof(int) * 2)
            {
                error = $"Remote user Battlefield team packet expects 8 bytes but received {payload.Length}.";
                return false;
            }

            int characterId = payload[0]
                | (payload[1] << 8)
                | (payload[2] << 16)
                | (payload[3] << 24);
            int teamId = payload[4]
                | (payload[5] << 8)
                | (payload[6] << 16)
                | (payload[7] << 24);
            packet = new RemoteUserBattlefieldTeamPacket(characterId, teamId < 0 ? null : teamId);
            return true;
        }

        private static bool TryParseOptionalItemPacket(
            ReadOnlySpan<byte> payload,
            string packetName,
            out int characterId,
            out int? itemId,
            out string error,
            out int? pairCharacterId)
        {
            error = null;
            characterId = 0;
            itemId = null;
            pairCharacterId = null;
            if (payload.Length != sizeof(int) * 2
                && payload.Length != sizeof(int) * 3)
            {
                error = $"Remote user {packetName} packet expects 8 or 12 bytes but received {payload.Length}.";
                return false;
            }

            characterId = payload[0]
                | (payload[1] << 8)
                | (payload[2] << 16)
                | (payload[3] << 24);
            int rawItemId = payload[4]
                | (payload[5] << 8)
                | (payload[6] << 16)
                | (payload[7] << 24);
            itemId = rawItemId <= 0 ? null : rawItemId;
            if (payload.Length >= sizeof(int) * 3)
            {
                int rawPairCharacterId = payload[8]
                    | (payload[9] << 8)
                    | (payload[10] << 16)
                    | (payload[11] << 24);
                pairCharacterId = rawPairCharacterId <= 0 ? null : rawPairCharacterId;
            }

            return true;
        }

        private static bool TryParseTypedItemEffectPacket(
            ReadOnlySpan<byte> payload,
            out RemoteUserItemEffectPacket packet,
            out string error)
        {
            packet = default;
            error = null;
            if (payload.Length != (sizeof(int) * 2) + sizeof(byte)
                && payload.Length != (sizeof(int) * 3) + sizeof(byte))
            {
                error = $"Remote user item-effect packet expects 8, 9, 12, or 13 bytes but received {payload.Length}.";
                return false;
            }

            if (!TryParseOptionalItemPacket(
                    payload[..^1],
                    "item-effect",
                    out int characterId,
                    out int? itemId,
                    out error,
                    out int? pairCharacterId))
            {
                return false;
            }

            if (!TryParseRelationshipOverlayType(payload[^1], out RemoteRelationshipOverlayType relationshipType))
            {
                error = $"Remote user item-effect relationship type {payload[^1]} is not recognized.";
                return false;
            }

            packet = new RemoteUserItemEffectPacket(
                characterId,
                itemId,
                pairCharacterId,
                relationshipType);
            return true;
        }

        private static bool TryParseRelationshipOverlayType(byte value, out RemoteRelationshipOverlayType relationshipType)
        {
            relationshipType = value switch
            {
                0 => RemoteRelationshipOverlayType.Generic,
                1 => RemoteRelationshipOverlayType.Couple,
                2 => RemoteRelationshipOverlayType.Friendship,
                3 => RemoteRelationshipOverlayType.NewYearCard,
                4 => RemoteRelationshipOverlayType.Marriage,
                _ => RemoteRelationshipOverlayType.Generic
            };
            return value <= (byte)RemoteRelationshipOverlayType.Marriage;
        }

        private static void EnsureRelationshipRecordAddConsumed(ref PacketReader reader, int packetType)
        {
            if (reader.RemainingLength != 0)
            {
                throw new InvalidOperationException(
                    $"Remote user relationship-record add packet {packetType} has {reader.RemainingLength} unread bytes remaining.");
            }
        }

        private static void EnsureRelationshipRecordRemoveConsumed(ref PacketReader reader, int packetType)
        {
            if (reader.RemainingLength != 0)
            {
                throw new InvalidOperationException(
                    $"Remote user relationship-record remove packet {packetType} has {reader.RemainingLength} unread bytes remaining.");
            }
        }

        private const int MinimumOfficialEnterFieldPostAvatarLookLength =
            (sizeof(int) * 6) + (sizeof(short) * 2) + sizeof(byte);

        internal static RemoteUserTemporaryStatSnapshot DecodeOfficialRemoteTemporaryStats(
            ReadOnlySpan<byte> payload,
            int temporaryStatOffset,
            int avatarLookOffset)
        {
            if (temporaryStatOffset < 0
                || avatarLookOffset < temporaryStatOffset
                || avatarLookOffset > payload.Length)
            {
                return default;
            }

            int encodedLength = avatarLookOffset - temporaryStatOffset;
            if (encodedLength <= 0)
            {
                return default;
            }

            byte[] rawPayload = payload.Slice(temporaryStatOffset, encodedLength).ToArray();
            int[] maskWords = rawPayload.Length >= sizeof(int) * 4
                ? DecodeTemporaryStatMaskWords(rawPayload.AsSpan(0, sizeof(int) * 4))
                : Array.Empty<int>();

            return new RemoteUserTemporaryStatSnapshot(
                encodedLength,
                maskWords,
                rawPayload,
                DecodeKnownTemporaryStatState(rawPayload, out bool hasWeaponCharge, out int weaponChargePayloadOffset),
                hasWeaponCharge,
                weaponChargePayloadOffset);
        }

        private static int[] DecodeTemporaryStatMaskWords(ReadOnlySpan<byte> maskPayload)
        {
            int maskWordCount = maskPayload.Length / sizeof(int);
            int[] maskWords = new int[maskWordCount];
            for (int i = 0; i < maskWords.Length; i++)
            {
                int offset = i * sizeof(int);
                maskWords[i] = maskPayload[offset]
                    | (maskPayload[offset + 1] << 8)
                    | (maskPayload[offset + 2] << 16)
                    | (maskPayload[offset + 3] << 24);
            }

            return maskWords;
        }

        private static RemoteUserTemporaryStatKnownState DecodeKnownTemporaryStatState(
            ReadOnlySpan<byte> rawPayload,
            out bool hasWeaponCharge,
            out int weaponChargePayloadOffset)
        {
            hasWeaponCharge = false;
            weaponChargePayloadOffset = -1;
            if (rawPayload.Length < sizeof(int) * 4)
            {
                return default;
            }

            int[] maskWords = DecodeTemporaryStatMaskWords(rawPayload.Slice(0, sizeof(int) * 4));
            var reader = new PacketReader(rawPayload);
            reader.ReadBytes(sizeof(int) * 4);

            int? speed = null;
            bool hasShadowPartner = false;
            bool hasDarkSight = false;
            bool hasSoulArrow = false;
            bool hasSpiritJavelin = false;
            int? chargeSkillId = null;
            int? repeatEffectSkillId = null;
            int? magicShieldSkillId = null;
            int? finalCutSkillId = null;
            int? morphId = null;
            int? ghostId = null;
            bool hasBarrier = false;
            bool hasWindWalk = false;
            int? mechanicMode = null;
            bool hasDarkAura = false;
            bool hasBlueAura = false;
            bool hasYellowAura = false;
            int? darkAuraValue = null;
            int? blueAuraValue = null;
            int? yellowAuraValue = null;
            bool hasBlessingArmor = false;
            int weaponChargeMetadataOffset = -1;
            int? attractValue = null;
            int? spiritJavelinValue = null;
            int? banMapValue = null;
            int? dojangShieldValue = null;
            int? reverseInputValue = null;
            int? respectPImmuneValue = null;
            int? respectMImmuneValue = null;
            int? defenseAttValue = null;
            int? defenseStateValue = null;
            bool hasDojangBerserk = false;
            bool hasDojangInvincible = false;
            int? stopPortionValue = null;
            int? stopMotionValue = null;
            int? fearValue = null;
            bool hasFlying = false;
            int? frozenValue = null;
            int? suddenDeathValue = null;
            byte? cycloneValue = null;
            bool hasSneak = false;
            bool hasMorewildDamageUp = false;
            byte? trailingDefenseAttByte = null;
            byte? trailingDefenseStateByte = null;

            try
            {
                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.Speed))
                {
                    speed = reader.ReadByte();
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.ComboCounter))
                {
                    reader.ReadByte();
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.WeaponCharge))
                {
                    hasWeaponCharge = true;
                    int weaponChargeValue = reader.ReadInt32();
                    weaponChargeMetadataOffset = reader.Offset;
                    weaponChargePayloadOffset = weaponChargeMetadataOffset;
                    if (AfterImageChargeSkillResolver.IsKnownChargeSkillId(weaponChargeValue))
                    {
                        chargeSkillId = weaponChargeValue;
                    }
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.Stun))
                {
                    reader.ReadInt32();
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.Darkness))
                {
                    reader.ReadInt32();
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.Seal))
                {
                    reader.ReadInt32();
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.Weakness))
                {
                    reader.ReadInt32();
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.Curse))
                {
                    reader.ReadInt32();
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.Poison))
                {
                    reader.ReadInt16();
                    reader.ReadInt32();
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.ShadowPartner))
                {
                    hasShadowPartner = true;
                    reader.ReadInt32();
                    hasShadowPartner = true;
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.DarkSight))
                {
                    hasDarkSight = true;
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.SoulArrow))
                {
                    hasSoulArrow = true;
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.Morph))
                {
                    morphId = (ushort)reader.ReadInt16();
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.Ghost))
                {
                    ghostId = (ushort)reader.ReadInt16();
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.Attract))
                {
                    attractValue = reader.ReadInt32();
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.SpiritJavelin))
                {
                    hasSpiritJavelin = true;
                    hasSoulArrow = true;
                    spiritJavelinValue = reader.ReadInt32();
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.BanMap))
                {
                    banMapValue = reader.ReadInt32();
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.Barrier))
                {
                    hasBarrier = true;
                    reader.ReadInt32();
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.DojangShield))
                {
                    dojangShieldValue = reader.ReadInt32();
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.ReverseInput))
                {
                    reverseInputValue = reader.ReadInt32();
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.RespectPImmune))
                {
                    respectPImmuneValue = reader.ReadInt32();
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.RespectMImmune))
                {
                    respectMImmuneValue = reader.ReadInt32();
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.DefenseAtt))
                {
                    defenseAttValue = reader.ReadInt32();
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.DefenseState))
                {
                    defenseStateValue = reader.ReadInt32();
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.DojangBerserk))
                {
                    hasDojangBerserk = true;
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.DojangInvincible))
                {
                    hasDojangInvincible = true;
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.WindWalk))
                {
                    hasWindWalk = true;
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.RepeatEffect))
                {
                    int repeatEffectValue = reader.ReadInt32();
                    if (repeatEffectValue > 0)
                    {
                        repeatEffectSkillId = repeatEffectValue;
                    }
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.StopPortion))
                {
                    stopPortionValue = reader.ReadInt32();
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.StopMotion))
                {
                    stopMotionValue = reader.ReadInt32();
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.Fear))
                {
                    fearValue = reader.ReadInt32();
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.MagicShield))
                {
                    int magicShieldValue = reader.ReadInt32();
                    if (magicShieldValue > 0)
                    {
                        magicShieldSkillId = magicShieldValue;
                    }
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.Flying))
                {
                    hasFlying = true;
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.Frozen))
                {
                    frozenValue = reader.ReadInt32();
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.SuddenDeath))
                {
                    suddenDeathValue = reader.ReadInt32();
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.FinalCut))
                {
                    int finalCutValue = reader.ReadInt32();
                    if (finalCutValue > 0)
                    {
                        finalCutSkillId = finalCutValue;
                    }
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.Cyclone))
                {
                    cycloneValue = reader.ReadByte();
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.Sneak))
                {
                    hasSneak = true;
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.MorewildDamageUp))
                {
                    hasMorewildDamageUp = true;
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.Mechanic))
                {
                    mechanicMode = reader.ReadInt32();
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.DarkAura))
                {
                    hasDarkAura = true;
                    darkAuraValue = reader.ReadInt32();
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.BlueAura))
                {
                    hasBlueAura = true;
                    blueAuraValue = reader.ReadInt32();
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.YellowAura))
                {
                    hasYellowAura = true;
                    yellowAuraValue = reader.ReadInt32();
                }

                if (IsTemporaryStatActive(maskWords, RemoteTemporaryStatMaskBit.BlessingArmor))
                {
                    hasBlessingArmor = true;
                }

                if (!chargeSkillId.HasValue
                    && TryResolveChargeSkillIdFromKnownTemporaryStatPayload(
                        rawPayload,
                        weaponChargeMetadataOffset,
                        out int recoveredChargeSkillId))
                {
                    chargeSkillId = recoveredChargeSkillId;
                }

                if (reader.RemainingLength > 0)
                {
                    trailingDefenseAttByte = reader.ReadByte();
                }

                if (reader.RemainingLength > 0)
                {
                    trailingDefenseStateByte = reader.ReadByte();
                }
            }
            catch (InvalidOperationException)
            {
                // Keep the raw payload authoritative if the known-state walk encounters
                // a truncated packet or an unmodeled branch.
            }

            return new RemoteUserTemporaryStatKnownState(
                speed,
                hasShadowPartner,
                hasDarkSight,
                hasSoulArrow,
                hasSpiritJavelin,
                chargeSkillId,
                repeatEffectSkillId,
                magicShieldSkillId,
                finalCutSkillId,
                morphId,
                ghostId,
                hasBarrier,
                hasWindWalk,
                mechanicMode,
                hasDarkAura,
                hasBlueAura,
                hasYellowAura,
                darkAuraValue,
                blueAuraValue,
                yellowAuraValue,
                hasBlessingArmor,
                new RemoteUserTemporaryStatExtendedState(
                    attractValue,
                    spiritJavelinValue,
                    banMapValue,
                    dojangShieldValue,
                    reverseInputValue,
                    respectPImmuneValue,
                    respectMImmuneValue,
                    defenseAttValue,
                    defenseStateValue,
                    hasDojangBerserk,
                    hasDojangInvincible,
                    stopPortionValue,
                    stopMotionValue,
                    fearValue,
                    hasFlying,
                    frozenValue,
                    suddenDeathValue,
                    cycloneValue,
                    hasSneak,
                    hasMorewildDamageUp,
                    trailingDefenseAttByte,
                    trailingDefenseStateByte));
        }

        private static bool IsTemporaryStatActive(int[] maskWords, RemoteTemporaryStatMaskBit bit)
        {
            int bitIndex = (int)bit;
            int wordIndex = bitIndex / 32;
            int bitOffset = bitIndex % 32;
            return maskWords != null
                && wordIndex >= 0
                && wordIndex < maskWords.Length
                && ((((uint)maskWords[wordIndex]) >> bitOffset) & 0x1u) != 0;
        }

        private static bool TryResolveChargeSkillIdFromKnownTemporaryStatPayload(
            ReadOnlySpan<byte> rawPayload,
            int weaponChargeMetadataOffset,
            out int chargeSkillId)
        {
            chargeSkillId = 0;
            if (rawPayload.Length < (sizeof(int) * 4) + sizeof(int))
            {
                return false;
            }

            if (weaponChargeMetadataOffset >= 0
                && weaponChargeMetadataOffset <= rawPayload.Length - sizeof(int)
                && AfterImageChargeSkillResolver.TryResolveChargeSkillIdFromTemporaryStatMetadata(
                    rawPayload,
                    weaponChargeMetadataOffset,
                    out chargeSkillId))
            {
                return true;
            }

            if (weaponChargeMetadataOffset >= 0
                && weaponChargeMetadataOffset <= rawPayload.Length - sizeof(int)
                && AfterImageChargeSkillResolver.TryResolveChargeSkillIdFromTemporaryStatPayload(
                    rawPayload,
                    weaponChargeMetadataOffset,
                    preferredSkillId: 0,
                    maxScanBytes: AfterImageChargeSkillResolver.ChargeMetadataScopedScanBytes,
                    out chargeSkillId))
            {
                return true;
            }

            if (weaponChargeMetadataOffset >= 0
                && weaponChargeMetadataOffset <= rawPayload.Length - sizeof(int)
                && AfterImageChargeSkillResolver.TryResolveChargeSkillIdFromTemporaryStatPayload(
                    rawPayload,
                    weaponChargeMetadataOffset,
                    out chargeSkillId))
            {
                return true;
            }

            if (weaponChargeMetadataOffset >= 0
                && weaponChargeMetadataOffset <= rawPayload.Length - sizeof(int)
                && AfterImageChargeSkillResolver.TryResolveNearestChargeSkillIdFromTemporaryStatPayload(
                    rawPayload,
                    weaponChargeMetadataOffset,
                    preferredSkillId: 0,
                    out chargeSkillId))
            {
                return true;
            }

            return AfterImageChargeSkillResolver.TryResolveChargeSkillIdFromTemporaryStatPayload(
                rawPayload,
                sizeof(int) * 4,
                out chargeSkillId);
        }

        internal static RemoteUserTemporaryStatSnapshot ApplyResetMask(
            RemoteUserTemporaryStatSnapshot snapshot,
            int[] remainingMaskWords)
        {
            if (remainingMaskWords == null || remainingMaskWords.Length == 0)
            {
                return default;
            }

            bool hasActiveBits = false;
            for (int i = 0; i < remainingMaskWords.Length; i++)
            {
                hasActiveBits |= remainingMaskWords[i] != 0;
            }

            if (!hasActiveBits)
            {
                return default;
            }

            RemoteUserTemporaryStatKnownState knownState = snapshot.KnownState;
            RemoteUserTemporaryStatExtendedState extendedState = knownState.ExtendedState;
            bool hasSoulArrow = knownState.HasSoulArrow
                && (IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.SoulArrow)
                    || (knownState.HasSpiritJavelin
                        && IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.SpiritJavelin)));
            bool keepDefenseAtt = IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.DefenseAtt);
            bool keepDefenseState = IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.DefenseState);
            RemoteUserTemporaryStatExtendedState maskedExtendedState = new(
                IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.Attract) ? extendedState.AttractValue : null,
                IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.SpiritJavelin) ? extendedState.SpiritJavelinValue : null,
                IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.BanMap) ? extendedState.BanMapValue : null,
                IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.DojangShield) ? extendedState.DojangShieldValue : null,
                IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.ReverseInput) ? extendedState.ReverseInputValue : null,
                IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.RespectPImmune) ? extendedState.RespectPImmuneValue : null,
                IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.RespectMImmune) ? extendedState.RespectMImmuneValue : null,
                keepDefenseAtt ? extendedState.DefenseAttValue : null,
                keepDefenseState ? extendedState.DefenseStateValue : null,
                extendedState.HasDojangBerserk && IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.DojangBerserk),
                extendedState.HasDojangInvincible && IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.DojangInvincible),
                IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.StopPortion) ? extendedState.StopPortionValue : null,
                IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.StopMotion) ? extendedState.StopMotionValue : null,
                IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.Fear) ? extendedState.FearValue : null,
                extendedState.HasFlying && IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.Flying),
                IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.Frozen) ? extendedState.FrozenValue : null,
                IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.SuddenDeath) ? extendedState.SuddenDeathValue : null,
                IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.Cyclone) ? extendedState.CycloneValue : null,
                extendedState.HasSneak && IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.Sneak),
                extendedState.HasMorewildDamageUp && IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.MorewildDamageUp),
                keepDefenseAtt ? extendedState.TrailingDefenseAttByte : null,
                keepDefenseState ? extendedState.TrailingDefenseStateByte : null);
            RemoteUserTemporaryStatKnownState maskedKnownState = new(
                IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.Speed) ? knownState.Speed : null,
                knownState.HasShadowPartner && IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.ShadowPartner),
                knownState.HasDarkSight && IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.DarkSight),
                hasSoulArrow,
                knownState.HasSpiritJavelin && IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.SpiritJavelin),
                IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.WeaponCharge) ? knownState.ChargeSkillId : null,
                IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.RepeatEffect) ? knownState.RepeatEffectSkillId : null,
                IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.MagicShield) ? knownState.MagicShieldSkillId : null,
                IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.FinalCut) ? knownState.FinalCutSkillId : null,
                IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.Morph) ? knownState.MorphId : null,
                IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.Ghost) ? knownState.GhostId : null,
                knownState.HasBarrier && IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.Barrier),
                knownState.HasWindWalk && IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.WindWalk),
                IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.Mechanic) ? knownState.MechanicMode : null,
                knownState.HasDarkAura && IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.DarkAura),
                knownState.HasBlueAura && IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.BlueAura),
                knownState.HasYellowAura && IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.YellowAura),
                IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.DarkAura) ? knownState.DarkAuraValue : null,
                IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.BlueAura) ? knownState.BlueAuraValue : null,
                IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.YellowAura) ? knownState.YellowAuraValue : null,
                knownState.HasBlessingArmor && IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.BlessingArmor),
                maskedExtendedState);

            return snapshot with
            {
                MaskWords = remainingMaskWords,
                KnownState = maskedKnownState,
                HasWeaponCharge = IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.WeaponCharge) && snapshot.HasWeaponCharge,
                WeaponChargePayloadOffset = IsTemporaryStatActive(remainingMaskWords, RemoteTemporaryStatMaskBit.WeaponCharge)
                    ? snapshot.WeaponChargePayloadOffset
                    : -1
            };
        }

        private static int FindOfficialAvatarLookOffset(ReadOnlySpan<byte> payload, int searchStartOffset, out string error)
        {
            error = null;
            // CUserPool::OnUserEnterField feeds the remainder of the payload through
            // SecondaryStat::DecodeForRemote before AvatarLook, so the exact stat blob
            // length depends on the active 128-bit temporary-stat mask. Search for the
            // AvatarLook boundary instead of assuming the earlier empty-mask subset.
            int firstCandidate = searchStartOffset;
            int lastCandidate = payload.Length - OfficialEnterFieldSuffixLength;
            if (firstCandidate > lastCandidate)
            {
                error = "Remote user official enter packet is too short to contain AvatarLook and the spawn suffix.";
                return -1;
            }

            int resolvedOffset = -1;
            for (int avatarLookOffset = firstCandidate; avatarLookOffset <= lastCandidate; avatarLookOffset++)
            {
                byte[] avatarPayload = payload.Slice(avatarLookOffset).ToArray();
                if (!LoginAvatarLookCodec.TryDecode(avatarPayload, out LoginAvatarLook avatarLook, out _))
                {
                    continue;
                }

                int consumedAvatarBytes = LoginAvatarLookCodec.Encode(avatarLook).Length;
                if (payload.Length - avatarLookOffset - consumedAvatarBytes != OfficialEnterFieldSuffixLength)
                {
                    continue;
                }

                if (resolvedOffset >= 0)
                {
                    error = "Remote user official enter packet AvatarLook boundary is ambiguous.";
                    return -1;
                }

                resolvedOffset = avatarLookOffset;
            }

            if (resolvedOffset < 0)
            {
                error = "Remote user official enter packet AvatarLook boundary could not be resolved after the remote secondary-stat payload.";
            }

            return resolvedOffset;
        }

        private static bool DecodeFacingRight(byte moveAction)
        {
            return (moveAction & 1) == 0;
        }

        private static string ResolveActionNameFromMoveAction(byte moveAction, int portableChairItemId)
        {
            if (portableChairItemId > 0)
            {
                return CharacterPart.GetActionString(CharacterAction.Sit);
            }

            return (moveAction >> 1) switch
            {
                1 => CharacterPart.GetActionString(CharacterAction.Walk1),
                4 => CharacterPart.GetActionString(CharacterAction.Alert),
                5 => CharacterPart.GetActionString(CharacterAction.Jump),
                6 => CharacterPart.GetActionString(CharacterAction.Sit),
                17 => CharacterPart.GetActionString(CharacterAction.Ladder),
                18 => CharacterPart.GetActionString(CharacterAction.Rope),
                _ => CharacterPart.GetActionString(CharacterAction.Stand1)
            };
        }

        private static string ResolveActionNameFromActionCode(int? actionCode)
        {
            return actionCode.HasValue && CharacterPart.TryGetActionStringFromCode(actionCode.Value, out string actionName)
                ? actionName
                : null;
        }

        private static IReadOnlyList<RemoteUserMeleeAttackMobHit> DecodeOfficialAttackInfoPayload(
            ref PacketReader reader,
            int skillId,
            int hitCount,
            int damagePerMob)
        {
            if (hitCount <= 0)
            {
                return Array.Empty<RemoteUserMeleeAttackMobHit>();
            }

            List<RemoteUserMeleeAttackMobHit> mobHits = new(hitCount);
            for (int i = 0; i < hitCount; i++)
            {
                int mobId = reader.ReadInt32();
                if (mobId == 0)
                {
                    continue;
                }

                byte hitAction = reader.ReadByte();
                if (skillId == 4211006)
                {
                    int damageEntryCount = reader.ReadByte();
                    List<RemoteUserMeleeAttackDamageEntry> damageEntries = new(damageEntryCount);
                    for (int damageIndex = 0; damageIndex < damageEntryCount; damageIndex++)
                    {
                        damageEntries.Add(new RemoteUserMeleeAttackDamageEntry(
                            HitFlag: null,
                            Damage: reader.ReadInt32()));
                    }

                    mobHits.Add(new RemoteUserMeleeAttackMobHit(mobId, hitAction, damageEntries));
                    continue;
                }

                List<RemoteUserMeleeAttackDamageEntry> standardDamageEntries = new(Math.Max(0, damagePerMob));
                for (int damageIndex = 0; damageIndex < damagePerMob; damageIndex++)
                {
                    standardDamageEntries.Add(new RemoteUserMeleeAttackDamageEntry(
                        HitFlag: reader.ReadByte(),
                        Damage: reader.ReadInt32()));
                }

                mobHits.Add(new RemoteUserMeleeAttackMobHit(mobId, hitAction, standardDamageEntries));
            }

            return mobHits;
        }

        private static int? DecodeOfficialPostAttackPayload(ref PacketReader reader, int skillId)
        {
            if (PreparedSkillHudRules.UsesRemoteReleaseFollowUpPayload(skillId))
            {
                return reader.ReadInt32();
            }

            return null;
        }

        private static bool TryDecodeMoveSnapshot(
            ref PacketReader reader,
            int currentTime,
            bool decodePassiveTail,
            bool decodeClientOptRandomCounts,
            out PlayerMovementSyncSnapshot snapshot,
            out byte moveAction)
        {
            snapshot = null;
            moveAction = 0;

            if (!reader.CanRead(sizeof(short) * 4 + sizeof(byte)))
            {
                return false;
            }

            int startX = reader.ReadInt16();
            int startY = reader.ReadInt16();
            short startVelocityX = reader.ReadInt16();
            short startVelocityY = reader.ReadInt16();
            byte elementCount = reader.ReadByte();

            List<MovePathElement> elements = new(elementCount);
            int currentX = startX;
            int currentY = startY;
            short currentVelocityX = startVelocityX;
            short currentVelocityY = startVelocityY;
            short currentFoothold = 0;
            int cursorTime = currentTime;

            for (int i = 0; i < elementCount; i++)
            {
                if (!reader.CanRead(1))
                {
                    return false;
                }

                byte attr = reader.ReadByte();
                int elementX = currentX;
                int elementY = currentY;
                short elementVelocityX = currentVelocityX;
                short elementVelocityY = currentVelocityY;
                short elementFoothold = 0;
                bool readsCommonMoveSuffix = true;

                switch (attr)
                {
                    case 0:
                    case 5:
                    case 12:
                    case 14:
                    case 35:
                    case 36:
                        elementX = reader.ReadInt16();
                        elementY = reader.ReadInt16();
                        elementVelocityX = reader.ReadInt16();
                        elementVelocityY = reader.ReadInt16();
                        elementFoothold = reader.ReadInt16();
                        if (attr == 12)
                        {
                            reader.ReadInt16();
                        }

                        reader.ReadInt16();
                        reader.ReadInt16();
                        break;
                    case 1:
                    case 2:
                    case 13:
                    case 16:
                    case 18:
                    case 31:
                    case 32:
                    case 33:
                    case 34:
                        elementVelocityX = reader.ReadInt16();
                        elementVelocityY = reader.ReadInt16();
                        elementFoothold = 0;
                        break;
                    case 3:
                    case 4:
                    case 6:
                    case 7:
                    case 8:
                    case 10:
                        elementX = reader.ReadInt16();
                        elementY = reader.ReadInt16();
                        elementFoothold = reader.ReadInt16();
                        elementVelocityX = 0;
                        elementVelocityY = 0;
                        break;
                    case 9:
                        reader.ReadByte();
                        elementVelocityX = 0;
                        elementVelocityY = 0;
                        elementFoothold = 0;
                        readsCommonMoveSuffix = false;
                        break;
                    case 11:
                        elementVelocityX = reader.ReadInt16();
                        elementVelocityY = reader.ReadInt16();
                        reader.ReadInt16();
                        elementFoothold = 0;
                        break;
                    case 17:
                        elementX = reader.ReadInt16();
                        elementY = reader.ReadInt16();
                        elementVelocityX = reader.ReadInt16();
                        elementVelocityY = reader.ReadInt16();
                        break;
                    case 20:
                    case 21:
                    case 22:
                    case 23:
                    case 24:
                    case 25:
                    case 26:
                    case 27:
                    case 28:
                    case 29:
                    case 30:
                        break;
                    default:
                        elementX = 0;
                        elementY = 0;
                        elementVelocityX = 0;
                        elementVelocityY = 0;
                        elementFoothold = 0;
                        break;
                }

                short elapsed = 0;
                if (readsCommonMoveSuffix)
                {
                    moveAction = reader.ReadByte();
                    elapsed = reader.ReadInt16();
                    if (decodeClientOptRandomCounts)
                    {
                        reader.ReadInt16();
                        reader.ReadInt16();
                    }
                }
                else
                {
                    moveAction = 0;
                }

                MoveAction decodedAction = DecodeMoveAction(moveAction);
                bool facingRight = (moveAction & 1) == 0;

                elements.Add(new MovePathElement
                {
                    X = elementX,
                    Y = elementY,
                    VelocityX = elementVelocityX,
                    VelocityY = elementVelocityY,
                    Action = decodedAction,
                    FootholdId = elementFoothold,
                    TimeStamp = cursorTime,
                    Duration = elapsed,
                    FacingRight = facingRight,
                    MovePathAttribute = attr,
                    StatChanged = false
                });

                currentX = elementX;
                currentY = elementY;
                currentVelocityX = elementVelocityX;
                currentVelocityY = elementVelocityY;
                currentFoothold = elementFoothold;
                cursorTime += Math.Max(1, (int)elapsed);
            }

            IReadOnlyList<byte> passiveKeyPadStates = Array.Empty<byte>();
            Rectangle passiveMoveBounds = Rectangle.Empty;
            if (decodePassiveTail)
            {
                passiveKeyPadStates = DecodePassiveMoveKeyPadStates(ref reader);
                short left = reader.ReadInt16();
                short top = reader.ReadInt16();
                short right = reader.ReadInt16();
                short bottom = reader.ReadInt16();
                passiveMoveBounds = new Rectangle(left, top, right - left, bottom - top);
            }

            snapshot = new PlayerMovementSyncSnapshot(
                new PassivePositionSnapshot
                {
                    X = currentX,
                    Y = currentY,
                    VelocityX = currentVelocityX,
                    VelocityY = currentVelocityY,
                    Action = DecodeMoveAction(moveAction),
                    FootholdId = currentFoothold,
                    TimeStamp = cursorTime,
                    FacingRight = (moveAction & 1) == 0,
                    MovePathAttribute = elements.Count > 0
                        ? elements[^1].MovePathAttribute
                        : 0
                },
                elements,
                passiveKeyPadStates,
                passiveMoveBounds);
            return true;
        }

        private static bool TryDecodeMoveSnapshotPayload(
            ReadOnlySpan<byte> payload,
            int currentTime,
            bool decodePassiveTail,
            bool requireExactLength,
            out PlayerMovementSyncSnapshot snapshot,
            out byte moveAction,
            out string error)
        {
            string lastError = null;
            for (int pass = 0; pass < 2; pass++)
            {
                bool decodeClientOptRandomCounts = pass == 1;
                var reader = new PacketReader(payload);
                try
                {
                    if (!TryDecodeMoveSnapshot(
                            ref reader,
                            currentTime,
                            decodePassiveTail,
                            decodeClientOptRandomCounts,
                            out snapshot,
                            out moveAction))
                    {
                        lastError = decodeClientOptRandomCounts
                            ? "Decode with client random counters returned no snapshot."
                            : "Decode without client random counters returned no snapshot.";
                        continue;
                    }

                    if (requireExactLength && reader.RemainingLength != 0)
                    {
                        lastError = decodeClientOptRandomCounts
                            ? $"Decode with client random counters left {reader.RemainingLength} unread bytes."
                            : $"Decode without client random counters left {reader.RemainingLength} unread bytes.";
                        continue;
                    }

                    error = null;
                    return true;
                }
                catch (InvalidOperationException ex)
                {
                    lastError = decodeClientOptRandomCounts
                        ? $"Decode with client random counters failed: {ex.Message}"
                        : $"Decode without client random counters failed: {ex.Message}";
                }
            }

            snapshot = null;
            moveAction = 0;
            error = lastError ?? "Move-path packet could not be decoded.";
            return false;
        }

        private static IReadOnlyList<byte> DecodePassiveMoveKeyPadStates(ref PacketReader reader)
        {
            byte stateCount = reader.ReadByte();
            if (stateCount == 0)
            {
                return Array.Empty<byte>();
            }

            byte[] states = new byte[stateCount];
            byte packedStates = 0;
            for (int i = 0; i < states.Length; i++)
            {
                if ((i & 1) == 0)
                {
                    packedStates = reader.ReadByte();
                    states[i] = (byte)(packedStates & 0x0F);
                }
                else
                {
                    states[i] = (byte)((packedStates >> 4) & 0x0F);
                }
            }

            return states;
        }

        private static MoveAction DecodeMoveAction(byte moveAction)
        {
            int normalized = (moveAction >> 1) & 0x0F;
            return Enum.IsDefined(typeof(MoveAction), normalized)
                ? (MoveAction)normalized
                : MoveAction.Stand;
        }

        private ref struct PacketReader
        {
            private readonly ReadOnlySpan<byte> _buffer;
            private int _offset;

            public PacketReader(ReadOnlySpan<byte> buffer)
            {
                _buffer = buffer;
                _offset = 0;
            }

            public bool CanRead(int byteCount) => _offset + byteCount <= _buffer.Length;

            public byte ReadByte()
            {
                EnsureReadable(sizeof(byte));
                return _buffer[_offset++];
            }

            public short ReadInt16()
            {
                EnsureReadable(sizeof(short));
                short value = (short)(_buffer[_offset] | (_buffer[_offset + 1] << 8));
                _offset += sizeof(short);
                return value;
            }

            public int ReadInt32()
            {
                EnsureReadable(sizeof(int));
                int value = _buffer[_offset]
                    | (_buffer[_offset + 1] << 8)
                    | (_buffer[_offset + 2] << 16)
                    | (_buffer[_offset + 3] << 24);
                _offset += sizeof(int);
                return value;
            }

            public long ReadInt64()
            {
                EnsureReadable(sizeof(long));
                uint low = (uint)ReadInt32();
                uint high = (uint)ReadInt32();
                return ((long)high << 32) | low;
            }

            public byte[] ReadBytes(int length)
            {
                EnsureReadable(length);
                byte[] value = _buffer.Slice(_offset, length).ToArray();
                _offset += length;
                return value;
            }

            public byte[] ReadRemainingBytes()
            {
                return ReadBytes(_buffer.Length - _offset);
            }

            public string ReadString8()
            {
                int length = ReadByte();
                EnsureReadable(length);
                string value = Encoding.UTF8.GetString(_buffer.Slice(_offset, length));
                _offset += length;
                return value;
            }

            public string ReadString16()
            {
                int length = ReadInt16();
                EnsureReadable(length);
                string value = Encoding.Default.GetString(_buffer.Slice(_offset, length));
                _offset += length;
                return value;
            }

            public int Offset => _offset;

            public int RemainingLength => _buffer.Length - _offset;

            private void EnsureReadable(int byteCount)
            {
                if (!CanRead(byteCount))
                {
                    throw new InvalidOperationException("Remote user packet payload ended unexpectedly.");
                }
            }
        }
    }
}
