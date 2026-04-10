using System;
using System.Collections.Generic;
using System.Linq;
using HaCreator.MapEditor;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.Loaders;
using HaCreator.MapSimulator.Animation;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Interaction;
using System.Runtime.CompilerServices;
using System.Text;

namespace HaCreator.MapSimulator.Character.Skills
{
    /// <summary>
    /// Manages active skills, projectiles, buffs, and cooldowns
    /// </summary>
    public class SkillManager
    {
        internal enum CooldownUiPresentationKind
        {
            Default,
            VehicleDurability
        }

        internal readonly record struct CooldownUiState(
            int RemainingMs,
            int DurationMs,
            float Progress,
            string CounterText,
            string TooltipStateText,
            bool DisplayInCooldownUi,
            bool SuppressProgressOverlay,
            bool SuppressCounterText,
            CooldownUiPresentationKind PresentationKind,
            int CurrentValue,
            int MaxValue);

        private sealed class CooldownUiPresentationState
        {
            public CooldownUiPresentationKind Kind { get; init; }
            public int CurrentValue { get; set; }
            public int MaxValue { get; set; }
        }

        private sealed class SkillMountState
        {
            public int SkillId { get; init; }
            public int MountItemId { get; init; }
            public CharacterPart PreviousMount { get; init; }
        }

        internal enum AttackResolutionMode
        {
            Melee,
            Magic,
            Ranged,
            Projectile
        }

        private enum AttackTargetSelectionMode
        {
            Default,
            PacketOwnedTimeBomb
        }

        internal readonly record struct PacketOwnedTimeBombTargetSortKey(
            int PreferredRank,
            float PreferredPositionDistance,
            float AreaDistance,
            float VerticalDistance,
            float ForwardPenalty);

        private enum SkillMovementFamily
        {
            None,
            Teleport,
            Rush,
            FlyingRush,
            JumpRush,
            Backstep,
            BoundJump
        }

        private sealed class QueuedFollowUpAttack
        {
            public int SkillId { get; init; }
            public int Level { get; init; }
            public int ExecuteTime { get; init; }
            public int? TargetMobId { get; init; }
            public Vector2? TargetPosition { get; init; }
            public bool FacingRight { get; init; }
            public int RequiredWeaponCode { get; init; }
            public int ShootRange0 { get; init; }
            public ShootAmmoSelection ResolvedShootAmmoSelection { get; init; }
            public bool ShootAmmoBypassActive { get; init; }
        }

        private sealed class DeferredSkillPayload
        {
            public SkillData Skill { get; init; }
            public int Level { get; init; }
            public int ExecuteTime { get; init; }
            public bool IsQueuedFinalAttack { get; init; }
            public bool IsQueuedSparkAttack { get; init; }
            public bool RevalidateSkillLevelOnExecute { get; init; }
            public bool QueueFollowUps { get; init; }
            public int? PreferredTargetMobId { get; init; }
            public Vector2? PreferredTargetPosition { get; init; }
            public bool FacingRight { get; init; }
            public Vector2? AttackOrigin { get; init; }
            public string ActionName { get; init; }
            public int? RawActionCode { get; init; }
            public int? AttackActionType { get; init; }
            public int? ActionSpeed { get; init; }
            public float? StoredVerticalLaunchSpeed { get; init; }
            public int ShootRange0 { get; init; }
            public ShootAmmoSelection ResolvedShootAmmoSelection { get; init; }
            public bool ShootAmmoBypassActive { get; init; }
        }

        private sealed class DeferredMovingShootExecutionState
        {
            public int SkillId { get; init; }
            public int ExecuteTime { get; init; }
            public Point LiveWorldPosition { get; init; }
            public string ActionName { get; init; }
            public int? RawActionCode { get; init; }
            public int? AttackActionType { get; init; }
            public int? ActionSpeed { get; init; }
            public int RepeatCount { get; set; }
            public int CountLimit { get; init; }
        }

        private sealed class PendingProjectileSpawn
        {
            public ActiveProjectile Projectile { get; init; }
            public int ExecuteTime { get; init; }
        }

        private sealed class QueuedSparkAttack
        {
            public int SkillId { get; init; }
            public int Level { get; init; }
            public int ExecuteTime { get; init; }
            public int? SourceMobId { get; init; }
            public Vector2 SourcePosition { get; init; }
            public Vector2? PreferredTargetPosition { get; init; }
            public bool FacingRight { get; init; }
            public int RequiredWeaponCode { get; init; }
            public ShootAmmoSelection ResolvedShootAmmoSelection { get; init; }
            public bool ShootAmmoBypassActive { get; init; }
        }

        private sealed class QueuedSerialAttack
        {
            public int SkillId { get; init; }
            public int Level { get; init; }
            public int ExecuteTime { get; init; }
            public bool FacingRight { get; init; }
            public int RequiredWeaponCode { get; init; }
            public int? PreferredTargetMobId { get; init; }
            public Vector2 PreferredTargetPosition { get; init; }
        }

        private sealed class QueuedSummonAttack
        {
            public long SequenceId { get; init; }
            public int AttackStartedAt { get; init; } = int.MinValue;
            public int ExecuteTime { get; init; }
            public int SummonObjectId { get; init; }
            public int SkillId { get; init; }
            public int Level { get; init; }
            public SkillData SkillData { get; init; }
            public SkillLevelData LevelData { get; init; }
            public bool FacingRight { get; init; }
            public Vector2 Origin { get; init; }
            public string AttackBranchName { get; init; }
            public int AttackCount { get; init; }
            public int? DamagePercentOverride { get; init; }
            public int TargetOrderOffset { get; init; }
            public bool IsSg88ManualAttackBatch { get; init; }
            public bool IsSg88ManualFollowUpBatch { get; init; }
            public int Sg88ManualRequestTime { get; init; } = int.MinValue;
            public int[] TargetMobIds { get; init; } = Array.Empty<int>();
        }

        private sealed class LocalSummonTileEffectDisplay
        {
            public SkillAnimation Animation { get; init; }
            public Rectangle Area { get; init; }
            public int StartTime { get; init; }
            public int EndTime { get; init; }
            public byte StartAlpha { get; init; }
            public byte EndAlpha { get; init; }

            public bool IsActive(int currentTime) => currentTime >= StartTime && currentTime < EndTime;

            public bool IsExpired(int currentTime) => currentTime >= EndTime;

            public float GetAlpha(int currentTime)
            {
                if (EndTime <= StartTime)
                {
                    return EndAlpha / 255f;
                }

                float progress = MathHelper.Clamp(
                    (currentTime - StartTime) / (float)(EndTime - StartTime),
                    0f,
                    1f);
                float alpha = MathHelper.Lerp(StartAlpha, EndAlpha, progress);
                return MathHelper.Clamp(alpha / 255f, 0f, 1f);
            }
        }

        internal readonly record struct SummonAttackBatch(int StartIndex, int TargetCount, int DelayMs);

        private sealed class RocketBoosterState
        {
            public SkillData Skill { get; init; }
            public int Level { get; init; }
            public bool FacingRight { get; init; }
            public bool UsesTankStartup { get; init; }
            public float StoredVerticalLaunchSpeed { get; init; }
            public int LaunchStartTime { get; set; }
            public int StartupActionDurationMs { get; set; }
            public bool StoredLaunchMarkerCleared { get; set; }
            public int LandingAttackTime { get; set; } = int.MinValue;
            public int RecoveryDurationMs { get; set; }
            public bool RecoveryEffectRequested { get; set; }
        }

        private sealed class CycloneState
        {
            public SkillData Skill { get; init; }
            public int Level { get; init; }
            public int ExpireTime { get; init; }
            public int NextAttackTime { get; set; }
        }

        private sealed class SwallowState
        {
            public int SkillId { get; init; }
            public int ParentSkillId { get; init; }
            public int Level { get; init; }
            public int TargetMobId { get; init; }
            public bool PendingAbsorbOutcome { get; set; }
            public int PendingAbsorbExpireTime { get; init; }
            public bool IsDigesting { get; set; }
            public int DigestStartTime { get; set; }
            public int DigestCompleteTime { get; set; }
            public int NextWriggleTime { get; set; }
        }

        public readonly record struct SwallowAbsorbRequest(int VisibleSkillId, int TargetMobId, int SkillLevel, int RequestedAt);
        public readonly record struct Sg88ManualAttackRequest(
            int SummonObjectId,
            int SkillId,
            int RequestedAt,
            int PrimaryTargetMobId,
            int[] TargetMobIds,
            int BaseDelayMs,
            int FollowUpDelayMs);

        private sealed class RepeatSkillSustainState
        {
            public int SkillId { get; init; }
            public int ReturnSkillId { get; init; }
            public int StartTime { get; set; }
            public bool RestrictToNormalAttack { get; init; }
            public bool ShowHudBar { get; set; }
            public int HudBarStartTime { get; set; }
            public string HudSkinKey { get; init; } = "KeyDownBar";
            public int HudGaugeDurationMs { get; init; }
            public bool Sg88AssistArmed { get; set; }
            public bool IsDone { get; set; }
            public int BranchPoint { get; set; }
            public bool PendingModeEndRequest { get; set; }
            public int PendingModeEndRequestTime { get; set; } = int.MinValue;
            public int LastAttackStartTime { get; set; } = int.MinValue;
        }

        private sealed class ClientSkillTimer
        {
            public int SkillId { get; init; }
            public string Source { get; init; }
            public int ExpireTime { get; init; }
            public int TimerKey { get; init; }
            public Action<int> OnExpired { get; init; }
        }

        private readonly record struct Sg88ManualCandidate(int Index, Rectangle Hitbox, Vector2 Center);

        private sealed class ActiveAffectedSkillPassive
        {
            public int SkillId { get; init; }
            public int Level { get; set; }
            public int NextTriggerTime { get; set; }
        }

        private sealed class EnergyChargeRuntimeState
        {
            public int SkillId { get; init; }
            public int Level { get; set; }
            public int CurrentCharge { get; set; }
            public int MaxCharge { get; set; }
            public bool IsFullyCharged { get; set; }
        }

        public readonly struct ClientSkillTimerExpiration
        {
            public ClientSkillTimerExpiration(int skillId, string source, int expireTime, int timerKey = 0)
            {
                SkillId = skillId;
                Source = source;
                ExpireTime = expireTime;
                TimerKey = timerKey;
            }

            public int SkillId { get; }
            public string Source { get; }
            public int ExpireTime { get; }
            public int TimerKey { get; }
        }

        private sealed class BuffTemporaryStatPresentation
        {
            public string Label { get; init; }
            public string DisplayName { get; init; }
            public string IconKey { get; init; }
            public int SortOrder { get; init; }
            public int PrimaryPriority { get; init; }
        }

        private const string GenericBuffIconKey = "united/buff";
        private const string AnniversaryBuffIconKey = "united/anniversary";
        private const string ClientTimerSourceBuffExpire = "buff-expire";
        private const string ClientTimerSourceCycloneExpire = "cyclone-expire";
        private const string ClientTimerSourcePreparedRelease = "prepared-release";
        private const string ClientTimerSourceRepeatSustainEnd = "repeat-sustain-end";
        private const string ClientTimerSourceSwallowDigest = "swallow-digest";
        private const string ClientTimerSourceRepeatModeEndAck = "repeat-mode-end-ack";
        private const string ClientTimerSourceSkillZoneExpire = "skill-zone-expire";
        private const string ClientTimerSourceSummonExpire = "summon-expire";
        private const float GroundedSummonVisualYOffset = 25f;
        private const int WildHunterSwallowSkillId = 33101005;
        private const int WildHunterSwallowBuffSkillId = 33101006;
        private const int WildHunterSwallowAttackSkillId = 33101007;
        private const int WindWalkSkillId = 11101005;
        private const int NightLordFlashJumpSkillId = 4111006;
        private const int ShadowerFlashJumpSkillId = 4211009;
        private const int DualBladeFlashJumpSkillId = 4321003;
        private const int NightWalkerFlashJumpSkillId = 14101004;
        private const int AuraDotTickIntervalMs = 1000;
        private const int ExclusiveRequestThrottleMs = 300;
        private const int MovingShootAntiRepeatHorizontalThresholdPx = 6;
        private const int MovingShootAntiRepeatVerticalThresholdPx = 150;
        private const int MovingShootAttackActionTypeMelee = 0;
        private const int MovingShootAttackActionTypeShoot = 1;
        private const int MovingShootAttackActionTypeMagic = 2;
        private const int MovingShootActionSpeedMinDegree = 2;
        private const int MovingShootActionSpeedMaxDegree = 10;
        private const int ClientMovingShootBaseSpeedOffsetSkillId = 4001334;
        private const int ClientMovingShootAntiRepeatBypassSkillId = 33121009;
        private enum ClientRandomMovingShootActionFamily
        {
            None,
            Unsupported,
            Shoot,
            OneHandedStab,
            TwoHandedStab,
            OneHandedSwing,
            TwoHandedSwing,
            PolearmSwing
        }
        private static readonly string[] ClientRandomMovingShootShootFamilyActionNames =
        {
            "shoot1",
            "shoot2",
            "shootF"
        };
        private static readonly string[] ClientRandomMovingShootOneHandedStabActionNames =
        {
            "stabO1",
            "stabO2",
            "stabOF"
        };
        private static readonly string[] ClientRandomMovingShootTwoHandedStabActionNames =
        {
            "stabT1",
            "stabT2",
            "stabTF"
        };
        private static readonly string[] ClientRandomMovingShootOneHandedSwingActionNames =
        {
            "swingO1",
            "swingO2",
            "swingO3",
            "swingOF"
        };
        private static readonly string[] ClientRandomMovingShootTwoHandedSwingActionNames =
        {
            "swingT1",
            "swingT2",
            "swingT3",
            "swingTF"
        };
        private static readonly string[] ClientRandomMovingShootPolearmSwingActionNames =
        {
            "swingP1",
            "swingP2",
            "swingPF",
            "swingP1PoleArm",
            "swingP2PoleArm"
        };
        private static readonly HashSet<int> ClientMovingShootSpeedModifierIgnoreSkillIds = new()
        {
            4311003,
            4321001,
            4331000,
            4331005,
            4341001,
            4341003
        };
        private const int EnergyChargePointsPerSuccessfulHit = 10;
        private const int GenericBuffSortOrder = 999;
        private const int GenericBuffPrimaryPriority = 999;
        private const int ClientSecondaryStatSortOrderBase = 100;
        private const int ClientSecondaryStatSortOrderStep = 10;
        private const string MaxHpBuffLabel = "MaxHP";
        private const string MaxMpBuffLabel = "MaxMP";
        private const string CraftBuffLabel = "Craft";
        private const string StrengthBuffLabel = "STR";
        private const string DexterityBuffLabel = "DEX";
        private const string IntelligenceBuffLabel = "INT";
        private const string LuckBuffLabel = "LUK";
        private const string BoosterBuffLabel = "Booster";
        private const string StanceBuffLabel = "Stance";
        private const string InvincibleBuffLabel = "Invincible";
        private const string CriticalRateBuffLabel = "CriticalRate";
        private const string DamageReductionBuffLabel = "DamageReduction";
        private const string DamRBuffLabel = "DamR";
        private const string TransformBuffLabel = "Transform";
        private const string ExperienceBuffLabel = "ExperienceRate";
        private const string DropRateBuffLabel = "DropRate";
        private const string MesoRateBuffLabel = "MesoRate";
        private const string BossDamageBuffLabel = "BossDamage";
        private const string IgnoreDefenseBuffLabel = "IgnoreDefense";
        private const string CriticalDamageBuffLabel = "CriticalDamage";
        private const string ComboBuffLabel = "Combo";
        private const string ChargeBuffLabel = "Charge";
        private const string AuraBuffLabel = "Aura";
        private const string ShadowPartnerBuffLabel = "ShadowPartner";
        private const string DebuffResistanceBuffLabel = "DebuffResistance";
        private const string RecoveryBuffLabel = "Recovery";
        private const string AllStatsBuffLabel = "AllStats";
        private const string HyperBodyBuffLabel = "HyperBody";
        private const string SharpEyesBuffLabel = "SharpEyes";
        private const string SoulArrowBuffLabel = "SoulArrow";
        private const string DarkSightBuffLabel = "DarkSight";
        private const string MagicGuardBuffLabel = "MagicGuard";
        private const string BlessBuffLabel = "Bless";
        private const string MapleWarriorBuffLabel = "MapleWarrior";
        private const string DashBuffLabel = "Dash";

        private static readonly IReadOnlyDictionary<string, int> ClientSecondaryStatSortOrderCatalog =
            BuildClientSecondaryStatSortOrderCatalog(
                "MagicGuard",
                "DarkSight",
                "Booster",
                "PowerGuard",
                "MaxHP",
                "MaxMP",
                "Invincible",
                "SoulArrow",
                "ComboCounter",
                "WeaponCharge",
                "HolySymbol",
                "MesoUp",
                "ShadowPartner",
                "Morph",
                "Regen",
                "BasicStatUp",
                "Stance",
                "SharpEyes",
                "Holyshield",
                "Aura",
                "SuperBody",
                "MesoUpByItem",
                "ItemUpByItem",
                "DamR");

        private static readonly Dictionary<string, BuffTemporaryStatPresentation> TemporaryStatPresentationCatalog =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // Sort order follows the known `UI/BuffIcon.img/buff/*` property order first.
                // Non-BuffIcon families then follow the recovered v95 `SecondaryStat` layout in
                // `docs/GMS v95.h`, using the earliest matching client-owned temp-stat family where possible.
                ["EVA"] = CreateTemporaryStatPresentation("EVA", "Avoidability", "buff/incEVA", 10, 210),
                ["PAD"] = CreateTemporaryStatPresentation("PAD", "Physical Attack", "buff/incPAD", 20, 220),
                ["PDD"] = CreateTemporaryStatPresentation("PDD", "Physical Defense", "buff/incPDD", 30, 230),
                ["MAD"] = CreateTemporaryStatPresentation("MAD", "Magic Attack", "buff/incMAD", 40, 240),
                ["MDD"] = CreateTemporaryStatPresentation("MDD", "Magic Defense", "buff/incMDD", 50, 250),
                ["ACC"] = CreateTemporaryStatPresentation("ACC", "Accuracy", "buff/incACC", 60, 260),
                [CraftBuffLabel] = CreateTemporaryStatPresentation(CraftBuffLabel, "Craft", "buff/incCraft", 70, 270),
                ["Jump"] = CreateTemporaryStatPresentation("Jump", "Jump", "buff/incJump", 80, 280),
                ["Speed"] = CreateTemporaryStatPresentation("Speed", "Speed", "buff/incSpeed", 90, 290),
                [StrengthBuffLabel] = CreateTemporaryStatPresentation(StrengthBuffLabel, "Strength", null, 365, 900),
                [DexterityBuffLabel] = CreateTemporaryStatPresentation(DexterityBuffLabel, "Dexterity", null, 370, 901),
                [IntelligenceBuffLabel] = CreateTemporaryStatPresentation(IntelligenceBuffLabel, "Intelligence", null, 375, 902),
                [LuckBuffLabel] = CreateTemporaryStatPresentation(LuckBuffLabel, "Luck", null, 380, 903),
                [DashBuffLabel] = CreateTemporaryStatPresentation(DashBuffLabel, "Dash", null, 75, 75),
                [MagicGuardBuffLabel] = CreateTemporaryStatPresentation(MagicGuardBuffLabel, "Magic Guard", null, ResolveClientSecondaryStatSortOrder("MagicGuard", 100), 100),
                [DarkSightBuffLabel] = CreateTemporaryStatPresentation(DarkSightBuffLabel, "Dark Sight", null, ResolveClientSecondaryStatSortOrder("DarkSight", 110), 110),
                [BoosterBuffLabel] = CreateTemporaryStatPresentation(BoosterBuffLabel, "Booster", null, ResolveClientSecondaryStatSortOrder("Booster", 120), 120),
                [DamageReductionBuffLabel] = CreateTemporaryStatPresentation(DamageReductionBuffLabel, "Damage Reduction", null, ResolveClientSecondaryStatSortOrder("PowerGuard", 130), 130),
                [DamRBuffLabel] = CreateTemporaryStatPresentation(DamRBuffLabel, "Damage Reduction", null, ResolveClientSecondaryStatSortOrder("DamR", 330), 330),
                [MaxHpBuffLabel] = CreateTemporaryStatPresentation(MaxHpBuffLabel, "Max HP", null, ResolveClientSecondaryStatSortOrder("MaxHP", 140), 310),
                [MaxMpBuffLabel] = CreateTemporaryStatPresentation(MaxMpBuffLabel, "Max MP", null, ResolveClientSecondaryStatSortOrder("MaxMP", 150), 311),
                [InvincibleBuffLabel] = CreateTemporaryStatPresentation(InvincibleBuffLabel, "Invincible", null, ResolveClientSecondaryStatSortOrder("Invincible", 160), 160),
                [SoulArrowBuffLabel] = CreateTemporaryStatPresentation(SoulArrowBuffLabel, "Soul Arrow", null, ResolveClientSecondaryStatSortOrder("SoulArrow", 170), 170),
                [BlessBuffLabel] = CreateTemporaryStatPresentation(BlessBuffLabel, "Bless", null, 175, 175),
                [ComboBuffLabel] = CreateTemporaryStatPresentation(ComboBuffLabel, "Combo", null, ResolveClientSecondaryStatSortOrder("ComboCounter", 180), 180),
                [ChargeBuffLabel] = CreateTemporaryStatPresentation(ChargeBuffLabel, "Charge", null, ResolveClientSecondaryStatSortOrder("WeaponCharge", 190), 190),
                [ExperienceBuffLabel] = CreateTemporaryStatPresentation(ExperienceBuffLabel, "EXP Rate", null, ResolveClientSecondaryStatSortOrder("HolySymbol", 200), 200),
                [MesoRateBuffLabel] = CreateTemporaryStatPresentation(MesoRateBuffLabel, "Meso Rate", null, ResolveClientSecondaryStatSortOrder("MesoUp", 210), 210),
                [ShadowPartnerBuffLabel] = CreateTemporaryStatPresentation(ShadowPartnerBuffLabel, "Shadow Partner", null, ResolveClientSecondaryStatSortOrder("ShadowPartner", 220), 220),
                [TransformBuffLabel] = CreateTemporaryStatPresentation(TransformBuffLabel, "Transform", null, ResolveClientSecondaryStatSortOrder("Morph", 230), 230),
                [RecoveryBuffLabel] = CreateTemporaryStatPresentation(RecoveryBuffLabel, "Recovery", null, ResolveClientSecondaryStatSortOrder("Regen", 240), 240),
                [MapleWarriorBuffLabel] = CreateTemporaryStatPresentation(MapleWarriorBuffLabel, "Maple Warrior", null, ResolveClientSecondaryStatSortOrder("BasicStatUp", 245), 245),
                [AllStatsBuffLabel] = CreateTemporaryStatPresentation(AllStatsBuffLabel, "All Stats", null, ResolveClientSecondaryStatSortOrder("BasicStatUp", 250), 250),
                [StanceBuffLabel] = CreateTemporaryStatPresentation(StanceBuffLabel, "Stance", null, ResolveClientSecondaryStatSortOrder("Stance", 260), 260),
                [SharpEyesBuffLabel] = CreateTemporaryStatPresentation(SharpEyesBuffLabel, "Sharp Eyes", null, ResolveClientSecondaryStatSortOrder("SharpEyes", 270), 270),
                [CriticalDamageBuffLabel] = CreateTemporaryStatPresentation(CriticalDamageBuffLabel, "Critical Damage", null, 272, 272),
                [CriticalRateBuffLabel] = CreateTemporaryStatPresentation(CriticalRateBuffLabel, "Critical Rate", null, 275, 275),
                [DebuffResistanceBuffLabel] = CreateTemporaryStatPresentation(DebuffResistanceBuffLabel, "Debuff Resistance", null, ResolveClientSecondaryStatSortOrder("Holyshield", 280), 280),
                [DropRateBuffLabel] = CreateTemporaryStatPresentation(DropRateBuffLabel, "Drop Rate", null, ResolveClientSecondaryStatSortOrder("ItemUpByItem", 285), 285),
                [BossDamageBuffLabel] = CreateTemporaryStatPresentation(BossDamageBuffLabel, "Boss Damage", null, 286, 286),
                [IgnoreDefenseBuffLabel] = CreateTemporaryStatPresentation(IgnoreDefenseBuffLabel, "Ignore DEF", null, 287, 287),
                [AuraBuffLabel] = CreateTemporaryStatPresentation(AuraBuffLabel, "Aura", null, ResolveClientSecondaryStatSortOrder("Aura", 290), 290),
                [HyperBodyBuffLabel] = CreateTemporaryStatPresentation(HyperBodyBuffLabel, "Hyper Body", null, ResolveClientSecondaryStatSortOrder("SuperBody", 300), 145)
            };

        private static IReadOnlyDictionary<string, BuffTemporaryStatPresentation> ResolvedTemporaryStatPresentationCatalog =
            TemporaryStatPresentationCatalog;
        private static IReadOnlyDictionary<string, BuffIconCatalogEntry> ResolvedBuffIconCatalog =
            new Dictionary<string, BuffIconCatalogEntry>(StringComparer.OrdinalIgnoreCase);

        private static readonly (string Label, string[] Fragments)[] TemporaryStatTextAliases =
        {
            ("PAD", new[] { "physical attack", "weapon attack", "attack power", "weapon att", "w.att", "w. att", "w att", "p.att", "p. att", "p atk", "patk", "watk", "w atk", "pad:" }),
            ("PDD", new[] { "physical defense", "weapon defense", "weapon def", "weapon guard", "w.def", "w. def", "w def", "p.def", "p. def", "p def", "pdd:" }),
            ("MAD", new[] { "magic attack", "magic att", "spell attack", "m.att", "m. att", "m att", "m.atk", "m. atk", "m atk", "matk", "mad:" }),
            ("MDD", new[] { "magic defense", "magic def", "spell defense", "m.def", "m. def", "m def", "mdd:" }),
            ("ACC", new[] { "accuracy", "acc", "acc:" }),
            ("EVA", new[] { "avoidability", "avoid", "eva", "eva:" }),
            (CraftBuffLabel, new[] { "craft", "incraft" }),
            ("Jump", new[] { "jump" }),
            ("Speed", new[] { "speed", "haste" }),
            (StrengthBuffLabel, new[] { "strength", "str increased", "str:" }),
            (DexterityBuffLabel, new[] { "dexterity", "dex increased", "dex:" }),
            (IntelligenceBuffLabel, new[] { "intelligence", "int increased", "int:" }),
            (LuckBuffLabel, new[] { "luk increased", "luk:" }),
            (MaxHpBuffLabel, new[] { "max hp", "maximum hp", "maxhp", "mhp" }),
            (MaxMpBuffLabel, new[] { "max mp", "maximum mp", "maxmp", "mmp" }),
            (CriticalRateBuffLabel, new[] { "critical rate", "critical hit rate", "critical chance", "crit rate", "critical" }),
            (BoosterBuffLabel, new[] { "booster", "attack speed", "weapon speed" }),
            (StanceBuffLabel, new[] { "stance", "knockback resist", "knock-back resist", "knockback immunity", "kb resist" }),
            (InvincibleBuffLabel, new[] { "invincible", "invincib", "invincibility", "immune to damage", "cannot be hit" }),
            (DamageReductionBuffLabel, new[] { "damage reduction", "dmg reduction", "reduce damage", "reduced damage", "barrier", "shield", "meso guard", "combo barrier", "power guard", "achilles" }),
            (TransformBuffLabel, new[] { "transform", "morph", "ride", "vehicle", "siege", "tank" }),
              (ExperienceBuffLabel, new[] { "holy symbol", "experience", "experience points", "exp gained", "exp rate", "exp +" }),
              (DropRateBuffLabel, new[] { "item drop", "item drop rate", "drop rate", "drop +" }),
              (MesoRateBuffLabel, new[] { "meso up", "meso rate", "mesos obtained", "meso obtained", "meso +" }),
              (BossDamageBuffLabel, new[] { "boss damage", "damage to bosses", "boss monster damage", "bdr" }),
              (IgnoreDefenseBuffLabel, new[] { "ignore defense", "ignore enemy defense", "ignore monster defense", "ignore mob defense", "ignore damage reduction", "ignore mob damage reduction", "ignore def", "ignoremobpdpr", "ignoremobdamr" }),
              (CriticalDamageBuffLabel, new[] { "critical damage", "critical dmg", "crit damage", "crit dmg" }),
            (ComboBuffLabel, new[] { "combo attack", "combo orb", "combo counter", "combo" }),
            (ChargeBuffLabel, new[] { "charge", "elemental charge", "energy charge" }),
            (AuraBuffLabel, new[] { "aura", "dark aura", "blue aura", "yellow aura", "body boost" }),
            (ShadowPartnerBuffLabel, new[] { "shadow partner", "mirror image", "shadow image", "clone" }),
            (DebuffResistanceBuffLabel, new[] { "holy shield", "abnormal status", "abnormal status resistance", "status ailment", "status resistance", "resistance to status", "debuff immunity", "status immunity" }),
            (RecoveryBuffLabel, new[] { "hp recovery", "mp recovery", "recover hp", "recover mp", "recovers hp", "recovers mp", "regeneration", "regen", "heals hp", "heals mp" }),
            (AllStatsBuffLabel, new[] { "all stats", "all stat", "all attributes", "str dex int luk", "all players' stats", "all player's stats" }),
            (HyperBodyBuffLabel, new[] { "hyper body" }),
            (SharpEyesBuffLabel, new[] { "sharp eyes", "weak spot" }),
            (SoulArrowBuffLabel, new[] { "soul arrow", "spirit javelin", "shadow claw", "without consuming arrows", "without consuming bullets", "without consuming throwing stars" }),
            (DarkSightBuffLabel, new[] { "dark sight", "wind walk", "stealth", "hide in the shadows" }),
            (MagicGuardBuffLabel, new[] { "magic guard", "damage dealt to you affects your mp instead of your hp" }),
            (BlessBuffLabel, new[] { "bless", "blessing" }),
            (MapleWarriorBuffLabel, new[] { "maple warrior", "echo of hero", "echo of the hero" }),
            (DashBuffLabel, new[] { "dash activation", "temporarily boost speed and jump" })
        };

        private static readonly IReadOnlyDictionary<string, string[]> AuthoredPropertyTemporaryStatLabels =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["pad"] = new[] { "PAD" },
                ["padX"] = new[] { "PAD" },
                ["epad"] = new[] { "PAD" },
                ["indiePad"] = new[] { "PAD" },
                ["mad"] = new[] { "MAD" },
                ["madX"] = new[] { "MAD" },
                ["emad"] = new[] { "MAD" },
                ["indieMad"] = new[] { "MAD" },
                ["pdd"] = new[] { "PDD" },
                ["pddX"] = new[] { "PDD" },
                ["epdd"] = new[] { "PDD" },
                ["pddR"] = new[] { "PDD" },
                ["mdd"] = new[] { "MDD" },
                ["mddX"] = new[] { "MDD" },
                ["emdd"] = new[] { "MDD" },
                ["mddR"] = new[] { "MDD" },
                ["acc"] = new[] { "ACC" },
                ["accX"] = new[] { "ACC" },
                ["indieAcc"] = new[] { "ACC" },
                ["accR"] = new[] { "ACC" },
                ["ar"] = new[] { "ACC" },
                ["eva"] = new[] { "EVA" },
                ["evaX"] = new[] { "EVA" },
                ["indieEva"] = new[] { "EVA" },
                ["evaR"] = new[] { "EVA" },
                ["er"] = new[] { "EVA" },
                ["speed"] = new[] { "Speed" },
                ["indieSpeed"] = new[] { "Speed" },
                ["jump"] = new[] { "Jump" },
                ["indieJump"] = new[] { "Jump" },
                ["str"] = new[] { StrengthBuffLabel },
                ["strX"] = new[] { StrengthBuffLabel },
                ["dex"] = new[] { DexterityBuffLabel },
                ["dexX"] = new[] { DexterityBuffLabel },
                ["int"] = new[] { IntelligenceBuffLabel },
                ["intX"] = new[] { IntelligenceBuffLabel },
                ["luk"] = new[] { LuckBuffLabel },
                ["lukX"] = new[] { LuckBuffLabel },
                ["emhp"] = new[] { MaxHpBuffLabel },
                ["indieMhp"] = new[] { MaxHpBuffLabel },
                ["mhpX"] = new[] { MaxHpBuffLabel },
                ["mhpR"] = new[] { MaxHpBuffLabel },
                ["indieMhpR"] = new[] { MaxHpBuffLabel },
                ["emmp"] = new[] { MaxMpBuffLabel },
                ["indieMmp"] = new[] { MaxMpBuffLabel },
                ["mmpX"] = new[] { MaxMpBuffLabel },
                ["mmpR"] = new[] { MaxMpBuffLabel },
                ["indieMmpR"] = new[] { MaxMpBuffLabel },
                ["cr"] = new[] { CriticalRateBuffLabel },
                ["criticaldamageMin"] = new[] { CriticalDamageBuffLabel },
                ["criticalDamageMin"] = new[] { CriticalDamageBuffLabel },
                ["criticaldamageMax"] = new[] { CriticalDamageBuffLabel },
                ["criticalDamageMax"] = new[] { CriticalDamageBuffLabel },
                ["damR"] = new[] { DamRBuffLabel },
                ["indieDamR"] = new[] { DamRBuffLabel },
                ["indieAllStat"] = new[] { AllStatsBuffLabel },
                ["asrR"] = new[] { DebuffResistanceBuffLabel },
                ["indieAsrR"] = new[] { DebuffResistanceBuffLabel },
                ["terR"] = new[] { DebuffResistanceBuffLabel },
                ["indieTerR"] = new[] { DebuffResistanceBuffLabel },
                ["expR"] = new[] { ExperienceBuffLabel },
                ["dropR"] = new[] { DropRateBuffLabel },
                ["mesoR"] = new[] { MesoRateBuffLabel },
                ["bdR"] = new[] { BossDamageBuffLabel },
                ["ignoreMobpdpR"] = new[] { IgnoreDefenseBuffLabel },
                ["ignoreMobDamR"] = new[] { IgnoreDefenseBuffLabel },
                ["hp"] = new[] { RecoveryBuffLabel },
                ["mp"] = new[] { RecoveryBuffLabel }
            };

        private static readonly IReadOnlyDictionary<string, string> AuthoredPropertyFamilyDisplayNameOverrides =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["mhpR"] = "Max HP",
                ["mhpX"] = "Max HP",
                ["mmpR"] = "Max MP",
                ["mmpX"] = "Max MP",
                ["pddR"] = "Physical Defense",
                ["mddR"] = "Magic Defense",
                ["accR"] = "Accuracy",
                ["evaR"] = "Avoidability",
                ["asrR"] = "Abnormal Status Resistance",
                ["indieAsrR"] = "Abnormal Status Resistance",
                ["terR"] = "Elemental Resistance",
                ["indieTerR"] = "Elemental Resistance",
                ["criticaldamageMin"] = "Critical Damage (Min)",
                ["criticalDamageMin"] = "Critical Damage (Min)",
                ["criticaldamageMax"] = "Critical Damage (Max)",
                ["criticalDamageMax"] = "Critical Damage (Max)",
                ["damR"] = "Damage Reduction",
                ["indieDamR"] = "Independent Damage Reduction",
                ["expR"] = "Bonus EXP",
                ["dropR"] = "Drop Rate",
                ["mesoR"] = "Meso Rate",
                ["bdR"] = "Boss Damage",
                ["ignoreMobpdpR"] = "Ignore Enemy DEF",
                ["ignoreMobDamR"] = "Ignore Enemy Damage Reduction"
            };

        #region Constants

        // Hotkey slot counts
        public const int PRIMARY_SLOT_COUNT = 8;       // Skill1-8 (indices 0-7)
        public const int FUNCTION_SLOT_COUNT = 12;     // F1-F12 (indices 8-19)
        public const int CTRL_SLOT_COUNT = 8;          // Ctrl+1-8 (indices 20-27)
        public const int TOTAL_SLOT_COUNT = PRIMARY_SLOT_COUNT + FUNCTION_SLOT_COUNT + CTRL_SLOT_COUNT;

        // Slot index offsets
        public const int FUNCTION_SLOT_OFFSET = PRIMARY_SLOT_COUNT;
        public const int CTRL_SLOT_OFFSET = PRIMARY_SLOT_COUNT + FUNCTION_SLOT_COUNT;

        // `CUIStatusBar::CQuickSlot::CompareValidateFuncKeyMappedInfo` only keeps
        // quick-slot items whose live family is allowed on the status-bar surface.
        private static readonly HashSet<int> QuickSlotVisibleUseItemFamilies = new()
        {
            200, 201, 202, 205, 210, 212, 221, 226, 227, 236, 238, 245
        };

        private const int QuickSlotCashSlotItemType = 9;
        private const int QuickSlotEtcCashItemType = 6;

        #endregion

        #region Properties

        private readonly SkillLoader _loader;
        private readonly PlayerCharacter _player;
        private Func<float, float, float, FootholdLine> _footholdLookup;
        private Func<SkillData, bool> _fieldSkillRestrictionEvaluator;
        private Func<SkillData, string> _fieldSkillRestrictionMessageProvider;
        private Func<int, CharacterPart> _tamingMobLoader;
        private Func<MapInfo> _currentMapInfoProvider;
        private Func<SkillData, string> _skillCancelRestrictionMessageProvider;
        private Func<IReadOnlyList<ActiveSummon>> _externalFriendlySupportSummonsProvider;
        private Func<int, bool> _externalCastBlockedEvaluator;
        private Func<int, string> _additionalStateRestrictionMessageProvider;

        // Active state
        private readonly List<ActiveProjectile> _projectiles = new();
        private readonly List<ActiveBuff> _buffs = new();
        private readonly HashSet<int> _externalAreaSupportBuffIds = new();
        private readonly HashSet<int> _affectedSkillSupportBuffIds = new();
        private readonly List<ActiveSkillZone> _skillZones = new();
        private readonly List<ActiveSummon> _summons = new();
        private readonly List<ActiveHitEffect> _hitEffects = new();
        private readonly List<LocalSummonTileEffectDisplay> _summonTileEffects = new();
        private readonly Dictionary<int, ActiveAffectedSkillPassive> _activeAffectedSkillPassives = new();
        private readonly Dictionary<int, int> _cooldowns = new(); // skillId -> lastCastTime
        private readonly Dictionary<int, int> _serverCooldownExpireTimes = new(); // skillId -> absolute expire tick
        private readonly HashSet<int> _pendingCooldownCompletionNotifications = new();
        private readonly HashSet<int> _expiredAuthoritativeCooldowns = new();
        private readonly Dictionary<int, CooldownUiPresentationState> _cooldownUiPresentations = new();
        private EnergyChargeRuntimeState _activeEnergyChargeRuntime;
        private PreparedSkill _preparedSkill;
        private int _lastPreparedSkillExclusiveRequestTime = int.MinValue;
        private SkillCastInfo _currentCast;
        private float? _activeSkillDamageScaleOverride;
        private SkillMountState _activeSkillMount;
        private bool _buffControlledFlyingAbility;
        private int _activeMapFlyingRepeatAvatarEffectSkillId;
        private bool _packetOwnedNextShootExJablinArmed;

        // Skill book
        private readonly Dictionary<int, int> _skillLevels = new(); // skillId -> level
        private readonly Dictionary<int, int> _skillMasterLevels = new(); // skillId -> master level
        private List<SkillData> _availableSkills = new();

        // Hotkeys - supports 28 total slots:
        // 0-7: Primary slots (Skill1-8)
        // 8-19: Function key slots (F1-F12)
        // 20-27: Ctrl+Number slots (Ctrl+1-8)
        private readonly Dictionary<int, int> _skillHotkeys = new(); // slotIndex -> skillId
        private readonly Dictionary<int, int> _macroHotkeys = new(); // slotIndex -> macro index
        private readonly Dictionary<int, ItemHotkeyBinding> _itemHotkeys = new(); // slotIndex -> item binding
        private IInventoryRuntime _inventoryRuntime;

        // Counters
        private int _nextProjectileId = 1;
        private int _nextSummonId = 1;

        private static readonly Random Random = new();

        // Callbacks
        public Action<SkillCastInfo> OnSkillCast;
        public Action<SkillData, string> OnFieldSkillCastRejected;
        public Action<SkillData, int, int> OnSkillCooldownStarted;
        public Action<SkillData, int, int> OnSkillCooldownBlocked;
        public Action<SkillData, int> OnSkillCooldownCompleted;
        public Action<ActiveProjectile, MobItem> OnProjectileHit;
        public ShootAmmoSelection LastResolvedShootAmmoSelection { get; private set; }
        private bool? _shootAmmoBypassTemporaryStatOverride;
        public Action<ActiveBuff> OnBuffApplied;
        public Action<ActiveBuff> OnBuffExpired;
        public Action<PreparedSkill> OnPreparedSkillStarted;
        public Action<PreparedSkill> OnPreparedSkillReleased;
        public Action<int, string> OnClientSkillTimerExpired;
        public Action<IReadOnlyList<ClientSkillTimerExpiration>> OnClientSkillTimersExpiredBatch;
        public Action<int, int, int> OnClientSkillCancelRequested;
        public Action<int, int> OnClientSkillEffectRequested;
        public Action<int, int, int> OnRepeatSkillModeEndRequested;
        public Action<SwallowAbsorbRequest> OnSwallowAbsorbRequested;
        public Action<Sg88ManualAttackRequest> OnSg88ManualAttackRequested;
        public Action<Rectangle, int, int, int> OnAttackAreaResolved;
        public Action<string> OnMacroPartyNotifyRequested;
        public Action<int, int, int> OnExternalAreaDamageSharingApplied;
        public Func<int, InventoryType, int, bool> OnItemHotkeyUseRequested;

        // References
        private MobPool _mobPool;
        private DropPool _dropPool;
        private CombatEffects _combatEffects;
        private AnimationEffects _animationEffects;
        private SoundManager _soundManager;
        private Func<int, SkillMacro> _macroResolver;
        private Func<int, string> _externalStateRestrictionMessageProvider;
        private readonly WildHunterSwallowAbsorbOutcomeBuffer _swallowAbsorbOutcomeBuffer = new();

        #endregion

        #region Initialization

        public SkillManager(SkillLoader loader, PlayerCharacter player)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            _player = player ?? throw new ArgumentNullException(nameof(player));
        }

        public void ConfigureBuffIconCatalog(IReadOnlyDictionary<string, BuffIconCatalogEntry> buffIconCatalog)
        {
            if (buffIconCatalog == null || buffIconCatalog.Count == 0)
            {
                ResolvedTemporaryStatPresentationCatalog = TemporaryStatPresentationCatalog;
                ResolvedBuffIconCatalog = new Dictionary<string, BuffIconCatalogEntry>(StringComparer.OrdinalIgnoreCase);
                return;
            }

            var resolvedCatalog = new Dictionary<string, BuffTemporaryStatPresentation>(
                TemporaryStatPresentationCatalog.Count,
                StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, BuffTemporaryStatPresentation> entry in TemporaryStatPresentationCatalog)
            {
                resolvedCatalog[entry.Key] = ResolveTemporaryStatPresentation(entry.Value, buffIconCatalog);
            }

            ResolvedTemporaryStatPresentationCatalog = resolvedCatalog;
            ResolvedBuffIconCatalog = new Dictionary<string, BuffIconCatalogEntry>(buffIconCatalog, StringComparer.OrdinalIgnoreCase);
        }

        public void SetMobPool(MobPool mobPool) => _mobPool = mobPool;
        public void SetDropPool(DropPool dropPool) => _dropPool = dropPool;
        public void SetCombatEffects(CombatEffects effects) => _combatEffects = effects;
        public void SetAnimationEffects(AnimationEffects effects) => _animationEffects = effects;
        public void SetSoundManager(SoundManager soundManager) => _soundManager = soundManager;
        public void SetFootholdLookup(Func<float, float, float, FootholdLine> footholdLookup) => _footholdLookup = footholdLookup;
        public void SetFieldSkillRestrictionEvaluator(Func<SkillData, bool> evaluator) => _fieldSkillRestrictionEvaluator = evaluator;
        public void SetFieldSkillRestrictionMessageProvider(Func<SkillData, string> provider) => _fieldSkillRestrictionMessageProvider = provider;
        public void SetSkillCancelRestrictionMessageProvider(Func<SkillData, string> provider) => _skillCancelRestrictionMessageProvider = provider;
        public void SetTamingMobLoader(Func<int, CharacterPart> loader) => _tamingMobLoader = loader;
        public void SetCurrentMapInfoProvider(Func<MapInfo> provider) => _currentMapInfoProvider = provider;
        public void SetExternalFriendlySupportSummonsProvider(Func<IReadOnlyList<ActiveSummon>> provider) => _externalFriendlySupportSummonsProvider = provider;
        public void SetMacroResolver(Func<int, SkillMacro> macroResolver) => _macroResolver = macroResolver;
        public void SetExternalCastBlockedEvaluator(Func<int, bool> evaluator) => _externalCastBlockedEvaluator = evaluator;
        public void SetExternalStateRestrictionMessageProvider(Func<int, string> provider) => _externalStateRestrictionMessageProvider = provider;
        public void SetAdditionalStateRestrictionMessageProvider(Func<int, string> provider) => _additionalStateRestrictionMessageProvider = provider;

        public int ResolveClientCancelFamilyRemainingDurationMs(int skillId, int currentTime)
        {
            if (skillId <= 0)
            {
                return 0;
            }

            int[] cancelSkillIds = ResolveClientCancelRequestSkillIds(skillId);
            if (cancelSkillIds.Length == 0)
            {
                return 0;
            }

            HashSet<int> cancelFamily = new(cancelSkillIds);
            int remainingMs = 0;

            foreach (ActiveBuff buff in _buffs)
            {
                if (buff?.SkillId > 0 && cancelFamily.Contains(buff.SkillId))
                {
                    remainingMs = Math.Max(remainingMs, buff.GetRemainingTime(currentTime));
                }
            }

            foreach (ClientSkillTimer timer in _clientSkillTimers)
            {
                if (timer == null
                    || timer.SkillId <= 0
                    || !cancelFamily.Contains(timer.SkillId)
                    || string.Equals(timer.Source, ClientTimerSourceSummonExpire, StringComparison.Ordinal))
                {
                    continue;
                }

                remainingMs = Math.Max(remainingMs, Math.Max(0, timer.ExpireTime - currentTime));
            }

            foreach (ActiveSummon summon in _summons)
            {
                if (summon?.SkillId > 0
                    && !summon.IsPendingRemoval
                    && cancelFamily.Contains(summon.SkillId))
                {
                    remainingMs = Math.Max(remainingMs, summon.GetRemainingTime(currentTime));
                }
            }

            return remainingMs;
        }

        internal static int RouteExpiredLocalSummonTimerBatchToClientCancel(
            IReadOnlyList<ClientSkillTimerExpiration> expirations,
            Action<int, int> primeExpiredLocalSummon,
            Func<int, IReadOnlyList<int>> resolveCancelRequestSkillIds,
            Func<int, int, bool> requestClientSkillCancel)
        {
            if (expirations == null || expirations.Count == 0)
            {
                return 0;
            }

            List<ClientSkillTimerExpiration> summonExpirations = null;
            foreach (ClientSkillTimerExpiration expiration in expirations)
            {
                if (expiration.SkillId <= 0
                    || expiration.TimerKey <= 0
                    || !string.Equals(expiration.Source, ClientTimerSourceSummonExpire, StringComparison.Ordinal))
                {
                    continue;
                }

                summonExpirations ??= new List<ClientSkillTimerExpiration>();
                summonExpirations.Add(expiration);
                primeExpiredLocalSummon?.Invoke(expiration.TimerKey, expiration.ExpireTime);
            }

            if (summonExpirations == null || requestClientSkillCancel == null)
            {
                return 0;
            }

            int routedCount = 0;
            HashSet<int> routedCancelFamilies = new();
            foreach (ClientSkillTimerExpiration expiration in summonExpirations)
            {
                int cancelFamilyKey = ResolveClientCancelFamilyBatchKey(expiration.SkillId, resolveCancelRequestSkillIds);
                if (!routedCancelFamilies.Add(cancelFamilyKey))
                {
                    continue;
                }

                if (requestClientSkillCancel(expiration.SkillId, expiration.ExpireTime))
                {
                    routedCount++;
                }
            }

            return routedCount;
        }

        internal static int ResolveClientCancelFamilyBatchKey(int skillId, Func<int, IReadOnlyList<int>> resolveCancelRequestSkillIds)
        {
            if (skillId <= 0)
            {
                return 0;
            }

            IReadOnlyList<int> cancelSkillIds = resolveCancelRequestSkillIds?.Invoke(skillId);
            int resolvedKey = int.MaxValue;
            if (cancelSkillIds != null)
            {
                for (int i = 0; i < cancelSkillIds.Count; i++)
                {
                    int cancelSkillId = cancelSkillIds[i];
                    if (cancelSkillId > 0 && cancelSkillId < resolvedKey)
                    {
                        resolvedKey = cancelSkillId;
                    }
                }
            }

            return resolvedKey == int.MaxValue ? skillId : resolvedKey;
        }

        public void SetInventoryRuntime(IInventoryRuntime inventoryRuntime)
        {
            _inventoryRuntime = inventoryRuntime;
            RevalidateHotkeys();
        }

        /// <summary>
        /// Load skills for player's job
        /// </summary>
        public void LoadSkillsForJob(int jobId)
        {
            ClearActiveSkillState(clearBuffs: true);

            // Standard jobs should keep their full advancement chain available (Beginner -> current job),
            // while special admin jobs stay on their focused single-book behavior.
            _availableSkills = IsFocusedSingleBookJob(jobId)
                ? _loader.LoadSkillsForJob(jobId)
                : _loader.LoadSkillsForJobPath(jobId);

            var validSkillIds = new HashSet<int>(_availableSkills.Select(skill => skill.SkillId));

            foreach (int obsoleteSkillId in _skillLevels.Keys.Where(skillId => !validSkillIds.Contains(skillId)).ToList())
            {
                _skillLevels.Remove(obsoleteSkillId);
            }

            foreach (int hotkeySlot in _skillHotkeys
                         .Where(entry => !validSkillIds.Contains(entry.Value))
                         .Select(entry => entry.Key)
                         .ToList())
            {
                _skillHotkeys.Remove(hotkeySlot);
            }

            foreach (int hotkeySlot in _macroHotkeys.Keys.ToList())
            {
                if (IsValidMacroHotkeyBinding(_macroHotkeys[hotkeySlot]))
                    continue;

                _macroHotkeys.Remove(hotkeySlot);
            }

            foreach (int cooldownSkillId in _cooldowns.Keys.Where(skillId => !validSkillIds.Contains(skillId)).ToList())
            {
                _cooldowns.Remove(cooldownSkillId);
                _serverCooldownExpireTimes.Remove(cooldownSkillId);
                _pendingCooldownCompletionNotifications.Remove(cooldownSkillId);
                _expiredAuthoritativeCooldowns.Remove(cooldownSkillId);
                _cooldownUiPresentations.Remove(cooldownSkillId);
            }

            // Initialize skill levels to 0 (unlearned)
            foreach (var skill in _availableSkills)
            {
                if (!_skillLevels.ContainsKey(skill.SkillId))
                {
                    _skillLevels[skill.SkillId] = 0;
                }
            }
        }

        /// <summary>
        /// Load the full player skill catalog from Skill.wz.
        /// </summary>
        public void LoadAllSkills()
        {
            _availableSkills = _loader.LoadAllSkills();

            foreach (var skill in _availableSkills)
            {
                if (!_skillLevels.ContainsKey(skill.SkillId))
                {
                    _skillLevels[skill.SkillId] = 0;
                }
            }
        }

        /// <summary>
        /// Set skill level
        /// </summary>
        public void SetSkillLevel(int skillId, int level)
        {
            if (level <= 0)
            {
                _skillLevels.Remove(skillId);
                _skillMasterLevels.Remove(skillId);
            }
            else
            {
                _skillLevels[skillId] = level;
            }

            RevalidateHotkeys();
        }

        /// <summary>
        /// Get skill level
        /// </summary>
        public int GetSkillLevel(int skillId)
        {
            return _skillLevels.TryGetValue(skillId, out int level) ? level : 0;
        }

        public void SetSkillMasterLevel(int skillId, int level)
        {
            if (level <= 0)
            {
                _skillMasterLevels.Remove(skillId);
                return;
            }

            _skillMasterLevels[skillId] = level;
        }

        public int GetSkillMasterLevel(int skillId)
        {
            return _skillMasterLevels.TryGetValue(skillId, out int level) ? level : 0;
        }

        public void LearnAllActiveSkills()
        {
            foreach (var skill in _availableSkills)
            {
                if (skill == null || skill.IsPassive || skill.Invisible || skill.SuppressesStandaloneActiveCast)
                    continue;

                SetSkillLevel(skill.SkillId, Math.Max(1, skill.MaxLevel));
            }
        }

        /// <summary>
        /// Set skill hotkey by absolute slot index (0-27)
        /// </summary>
        public void SetHotkey(int slotIndex, int skillId)
        {
            TrySetHotkey(slotIndex, skillId);
        }

        /// <summary>
        /// Try to set a skill hotkey by absolute slot index (0-27).
        /// Returns false when the requested skill is not a learned, visible active skill.
        /// </summary>
        public bool TrySetHotkey(int slotIndex, int skillId)
        {
            if (slotIndex < 0 || slotIndex >= TOTAL_SLOT_COUNT)
                return false;

            if (skillId <= 0)
            {
                _skillHotkeys.Remove(slotIndex);
                _macroHotkeys.Remove(slotIndex);
                _itemHotkeys.Remove(slotIndex);
                return true;
            }

            if (!CanAssignHotkeySkill(skillId))
                return false;

            _macroHotkeys.Remove(slotIndex);
            _itemHotkeys.Remove(slotIndex);
            _skillHotkeys[slotIndex] = skillId;
            return true;
        }

        public bool TrySetMacroHotkey(int slotIndex, int macroIndex)
        {
            if (slotIndex < 0 || slotIndex >= TOTAL_SLOT_COUNT)
                return false;

            if (macroIndex < 0)
            {
                _macroHotkeys.Remove(slotIndex);
                return true;
            }

            if (!IsValidMacroHotkeyBinding(macroIndex))
                return false;

            _skillHotkeys.Remove(slotIndex);
            _itemHotkeys.Remove(slotIndex);
            _macroHotkeys[slotIndex] = macroIndex;
            return true;
        }

        /// <summary>
        /// Try to set an item hotkey by absolute slot index (0-27).
        /// Only consumable and cash inventory entries with a live stack can occupy quick slots.
        /// </summary>
        public bool TrySetItemHotkey(int slotIndex, int itemId, InventoryType inventoryType = InventoryType.NONE)
        {
            if (slotIndex < 0 || slotIndex >= TOTAL_SLOT_COUNT)
                return false;

            if (itemId <= 0)
            {
                _itemHotkeys.Remove(slotIndex);
                return true;
            }

            if (!TryResolveAssignableHotkeyItem(itemId, inventoryType, out InventoryType resolvedInventoryType))
                return false;

            SetItemHotkeyBinding(slotIndex, itemId, resolvedInventoryType);
            return true;
        }

        /// <summary>
        /// Get skill on hotkey by absolute slot index (0-27)
        /// </summary>
        public int GetHotkeySkill(int slotIndex)
        {
            if (!_skillHotkeys.TryGetValue(slotIndex, out int skillId))
                return 0;

            if (CanAssignHotkeySkill(skillId))
                return skillId;

            _skillHotkeys.Remove(slotIndex);
            return 0;
        }

        public int GetHotkeyItem(int slotIndex)
        {
            ItemHotkeyBinding binding = GetHotkeyItemBinding(slotIndex);
            return binding?.ItemId ?? 0;
        }

        public InventoryType GetHotkeyItemInventoryType(int slotIndex)
        {
            ItemHotkeyBinding binding = GetHotkeyItemBinding(slotIndex);
            return binding?.InventoryType ?? InventoryType.NONE;
        }

        public int GetHotkeyItemCount(int slotIndex)
        {
            ItemHotkeyBinding binding = GetHotkeyItemBinding(slotIndex);
            if (binding == null)
                return 0;

            return _inventoryRuntime?.GetItemCount(binding.InventoryType, binding.ItemId) ?? 0;
        }

        public int GetHotkeyMacroIndex(int slotIndex)
        {
            if (!_macroHotkeys.TryGetValue(slotIndex, out int macroIndex))
                return -1;

            if (IsValidMacroHotkeyBinding(macroIndex))
                return macroIndex;

            _macroHotkeys.Remove(slotIndex);
            return -1;
        }

        public bool HasMacroHotkey(int slotIndex)
        {
            return GetHotkeyMacroIndex(slotIndex) >= 0;
        }

        /// <summary>
        /// Set primary hotkey (slots 0-7, used by Skill1-8 keys)
        /// </summary>
        public void SetPrimaryHotkey(int index, int skillId)
        {
            if (index >= 0 && index < PRIMARY_SLOT_COUNT)
                SetHotkey(index, skillId);
        }

        /// <summary>
        /// Get primary hotkey skill (slots 0-7)
        /// </summary>
        public int GetPrimaryHotkey(int index)
        {
            return index >= 0 && index < PRIMARY_SLOT_COUNT ? GetHotkeySkill(index) : 0;
        }

        /// <summary>
        /// Set function key hotkey (F1-F12, slots 8-19)
        /// </summary>
        public void SetFunctionHotkey(int index, int skillId)
        {
            if (index >= 0 && index < FUNCTION_SLOT_COUNT)
                SetHotkey(FUNCTION_SLOT_OFFSET + index, skillId);
        }

        /// <summary>
        /// Get function key hotkey skill (F1-F12, slots 8-19)
        /// </summary>
        public int GetFunctionHotkey(int index)
        {
            return index >= 0 && index < FUNCTION_SLOT_COUNT ? GetHotkeySkill(FUNCTION_SLOT_OFFSET + index) : 0;
        }

        /// <summary>
        /// Set Ctrl+Number hotkey (Ctrl+1-8, slots 20-27)
        /// </summary>
        public void SetCtrlHotkey(int index, int skillId)
        {
            if (index >= 0 && index < CTRL_SLOT_COUNT)
                SetHotkey(CTRL_SLOT_OFFSET + index, skillId);
        }

        /// <summary>
        /// Get Ctrl+Number hotkey skill (Ctrl+1-8, slots 20-27)
        /// </summary>
        public int GetCtrlHotkey(int index)
        {
            return index >= 0 && index < CTRL_SLOT_COUNT ? GetHotkeySkill(CTRL_SLOT_OFFSET + index) : 0;
        }

        /// <summary>
        /// Clear a hotkey slot
        /// </summary>
        public void ClearHotkey(int slotIndex)
        {
            _skillHotkeys.Remove(slotIndex);
            _macroHotkeys.Remove(slotIndex);
            _itemHotkeys.Remove(slotIndex);
        }

        /// <summary>
        /// Get the slot index where a skill is assigned (or -1 if not found)
        /// </summary>
        public int FindSkillSlot(int skillId)
        {
            RevalidateHotkeys();

            foreach (var kv in _skillHotkeys)
            {
                if (kv.Value == skillId)
                    return kv.Key;
            }
            return -1;
        }

        public bool IsSkillAssignedToDirectHotkey(int skillId)
        {
            return FindSkillSlot(skillId) >= 0;
        }

        /// <summary>
        /// Get all hotkey configurations for saving
        /// Returns dictionary of slotIndex -> skillId
        /// </summary>
        public Dictionary<int, int> GetAllHotkeys()
        {
            RevalidateHotkeys();
            return new Dictionary<int, int>(_skillHotkeys);
        }

        public Dictionary<int, ItemHotkeyBinding> GetAllItemHotkeys()
        {
            RevalidateHotkeys();
            Dictionary<int, ItemHotkeyBinding> copy = new();
            foreach (var kv in _itemHotkeys)
            {
                copy[kv.Key] = kv.Value?.Clone();
            }

            return copy;
        }

        public Dictionary<int, int> GetAllMacroHotkeys()
        {
            RevalidateHotkeys();
            return new Dictionary<int, int>(_macroHotkeys);
        }

        /// <summary>
        /// Load all hotkey configurations
        /// </summary>
        public void LoadHotkeys(Dictionary<int, int> hotkeys)
        {
            _skillHotkeys.Clear();
            if (hotkeys == null) return;

            foreach (var kv in hotkeys)
            {
                TrySetHotkey(kv.Key, kv.Value);
            }
        }

        public void LoadMacroHotkeys(Dictionary<int, int> hotkeys)
        {
            _macroHotkeys.Clear();
            if (hotkeys == null)
                return;

            foreach (var kv in hotkeys)
            {
                TrySetMacroHotkey(kv.Key, kv.Value);
            }
        }

        public void LoadItemHotkeys(Dictionary<int, ItemHotkeyBinding> hotkeys)
        {
            _itemHotkeys.Clear();
            if (hotkeys == null)
                return;

            foreach (var kv in hotkeys)
            {
                if (kv.Value == null)
                    continue;

                if (kv.Key < 0 || kv.Key >= TOTAL_SLOT_COUNT)
                    continue;

                if (TryResolveStoredHotkeyItem(kv.Value.ItemId, kv.Value.InventoryType, out InventoryType resolvedInventoryType))
                {
                    SetItemHotkeyBinding(kv.Key, kv.Value.ItemId, resolvedInventoryType);
                }
            }
        }

        /// <summary>
        /// Whether the given skill can appear in a quick slot.
        /// Mirrors the client skill-side validation by rejecting unlearned, passive, and hidden skills.
        /// </summary>
        public bool CanAssignHotkeySkill(int skillId)
        {
            if (skillId <= 0)
                return false;

            SkillData skill = FindKnownSkillData(skillId);
            if (skill == null || skill.IsPassive || skill.Invisible || skill.SuppressesStandaloneActiveCast)
                return false;

            if (!IsSkillAllowedForCurrentJob(skill))
                return false;

            return GetSkillLevel(skillId) > 0;
        }

        /// <summary>
        /// Revalidates quick-slot assignments against the current learned skill state.
        /// Returns the number of removed stale assignments.
        /// </summary>
        public int RevalidateHotkeys()
        {
            return RevalidateHotkeys(0, TOTAL_SLOT_COUNT);
        }

        /// <summary>
        /// Revalidates the requested hotkey range against the current learned-skill and
        /// live inventory state.
        /// </summary>
        public int RevalidateHotkeys(int startSlotIndex, int slotCount)
        {
            if (slotCount <= 0)
                return 0;

            int normalizedStart = Math.Clamp(startSlotIndex, 0, TOTAL_SLOT_COUNT);
            int normalizedEnd = Math.Clamp(startSlotIndex + slotCount, normalizedStart, TOTAL_SLOT_COUNT);
            int removed = 0;

            foreach (int slotIndex in _skillHotkeys.Keys.ToList())
            {
                if (!IsHotkeySlotInRange(slotIndex, normalizedStart, normalizedEnd))
                    continue;

                if (CanAssignHotkeySkill(_skillHotkeys[slotIndex]))
                    continue;

                _skillHotkeys.Remove(slotIndex);
                removed++;
            }

            foreach (int slotIndex in _itemHotkeys.Keys.ToList())
            {
                if (!IsHotkeySlotInRange(slotIndex, normalizedStart, normalizedEnd))
                    continue;

                if (IsValidHotkeyItemBinding(_itemHotkeys[slotIndex]))
                    continue;

                _itemHotkeys.Remove(slotIndex);
                removed++;
            }

            foreach (int slotIndex in _macroHotkeys.Keys.ToList())
            {
                if (!IsHotkeySlotInRange(slotIndex, normalizedStart, normalizedEnd))
                    continue;

                if (IsValidMacroHotkeyBinding(_macroHotkeys[slotIndex]))
                    continue;

                _macroHotkeys.Remove(slotIndex);
                removed++;
            }

            return removed;
        }

        /// <summary>
        /// Clear all hotkeys
        /// </summary>
        public void ClearAllHotkeys()
        {
            _skillHotkeys.Clear();
            _macroHotkeys.Clear();
            _itemHotkeys.Clear();
        }

        private SkillData FindKnownSkillData(int skillId)
        {
            foreach (var skill in _availableSkills)
            {
                if (skill?.SkillId == skillId)
                    return skill;
            }

            return _loader?.LoadSkill(skillId);
        }

        private bool TryResolveAssignableHotkeyItem(int itemId, InventoryType inventoryType, out InventoryType resolvedInventoryType)
        {
            resolvedInventoryType = inventoryType != InventoryType.NONE
                ? inventoryType
                : InventoryTypeExtensions.GetByType((byte)(itemId / 1000000)) ?? InventoryType.NONE;

            if (!IsQuickSlotVisibleItemFamily(itemId, resolvedInventoryType))
                return false;

            return (_inventoryRuntime?.GetItemCount(resolvedInventoryType, itemId) ?? 0) > 0;
        }

        private static bool TryResolveStoredHotkeyItem(int itemId, InventoryType inventoryType, out InventoryType resolvedInventoryType)
        {
            resolvedInventoryType = inventoryType != InventoryType.NONE
                ? inventoryType
                : InventoryTypeExtensions.GetByType((byte)(itemId / 1000000)) ?? InventoryType.NONE;

            return itemId > 0 && IsQuickSlotVisibleItemFamily(itemId, resolvedInventoryType);
        }

        private bool IsValidHotkeyItemBinding(ItemHotkeyBinding binding)
        {
            if (binding == null || !IsQuickSlotVisibleItemFamily(binding.ItemId, binding.InventoryType))
                return false;

            return _inventoryRuntime == null || _inventoryRuntime.GetItemCount(binding.InventoryType, binding.ItemId) > 0;
        }

        private ItemHotkeyBinding GetHotkeyItemBinding(int slotIndex)
        {
            if (!_itemHotkeys.TryGetValue(slotIndex, out ItemHotkeyBinding binding))
                return null;

            if (IsValidHotkeyItemBinding(binding))
                return binding;

            _itemHotkeys.Remove(slotIndex);
            return null;
        }

        private void SetItemHotkeyBinding(int slotIndex, int itemId, InventoryType inventoryType)
        {
            _skillHotkeys.Remove(slotIndex);
            _macroHotkeys.Remove(slotIndex);
            _itemHotkeys[slotIndex] = new ItemHotkeyBinding
            {
                ItemId = itemId,
                InventoryType = inventoryType
            };
        }

        private bool IsValidMacroHotkeyBinding(int macroIndex)
        {
            SkillMacro macro = _macroResolver?.Invoke(macroIndex);
            return macroIndex >= 0 && macro != null && macro.IsEnabled;
        }

        private static bool IsHotkeySlotInRange(int slotIndex, int startInclusive, int endExclusive)
        {
            return slotIndex >= startInclusive && slotIndex < endExclusive;
        }

        internal static bool IsQuickSlotVisibleItemFamily(int itemId, InventoryType inventoryType)
        {
            if (itemId <= 0)
                return false;

            int itemFamily = itemId / 10000;
            return inventoryType switch
            {
                InventoryType.USE => QuickSlotVisibleUseItemFamilies.Contains(itemFamily) || IsImmediateMobSummonItem(itemId),
                InventoryType.CASH => ResolveClientCashSlotItemType(itemId) == QuickSlotCashSlotItemType ||
                                      ResolveClientEtcCashItemType(itemId) == QuickSlotEtcCashItemType,
                _ => false
            };
        }

        private static bool IsImmediateMobSummonItem(int itemId)
        {
            return itemId / 1000 == 2109 || itemId == 2100067;
        }

        internal static int ResolveClientEtcCashItemType(int itemId)
        {
            int cashSlotItemType = ResolveClientCashSlotItemType(itemId);
            return cashSlotItemType is 1 or 2 or 3 or 4 or 5 or 6 or 7 or 36 or 37 or 40 or 42 or 46 or 55 or 58 or 59 or 60 or 63 or 77
                ? cashSlotItemType
                : 0;
        }

        internal static int ResolveClientCashSlotItemType(int itemId)
        {
            return itemId / 10000 switch
            {
                500 => 8,
                501 => 9,
                502 => 10,
                503 => 11,
                504 => 22,
                505 => itemId % 10 == 0 ? 23 : (uint)(itemId % 10 - 1) <= 8 ? 24 : 0,
                506 => itemId / 1000 == 5061 ? 65 : itemId / 1000 == 5062 ? 74 : (itemId % 10) switch
                {
                    0 => 25,
                    1 => 26,
                    2 or 3 => 27,
                    _ => 0
                },
                507 => (itemId % 10000 / 1000) switch
                {
                    1 => 12,
                    2 => 13,
                    4 => 45,
                    5 => (itemId % 10) switch
                    {
                        0 => 47,
                        1 => 48,
                        2 => 49,
                        3 => 50,
                        4 => 51,
                        5 => 52,
                        _ => 14
                    },
                    6 => 14,
                    7 => 61,
                    8 => 15,
                    _ => 0
                },
                508 => 18,
                509 => 21,
                510 => 20,
                512 => 16,
                513 => 7,
                514 => 4,
                515 => (itemId / 1000) switch
                {
                    5150 or 5151 or 5154 => 1,
                    5152 => itemId / 100 == 51520 ? 2 : itemId / 100 == 51521 ? 35 : 0,
                    5153 => 3,
                    _ => 0
                },
                516 => 6,
                517 => 10000 * (itemId / 10000) != itemId ? 0 : 17,
                518 => 5,
                519 => 28,
                520 => 19,
                522 => 40,
                523 => 29,
                524 => 30,
                525 => 37 - (itemId % 5251000 != 100 ? 1 : 0),
                528 => itemId / 1000 == 5280 ? 33 : itemId / 1000 == 5281 ? 34 : 0,
                530 => 41,
                533 => 31,
                537 => 32,
                538 => 42,
                539 => 43,
                540 => itemId / 1000 == 5400 ? 53 : itemId / 1000 == 5401 ? 54 : 0,
                542 => itemId / 1000 == 5420 ? 55 : 0,
                543 => (uint)(itemId / 1000 - 5431) <= 1 ? 66 : 0,
                545 => itemId / 1000 != 5451 ? 38 : 60,
                546 => 58,
                547 => 39,
                549 => 59,
                550 => 62,
                551 => 63,
                552 => 64,
                553 => 72,
                557 => 67,
                561 => 71,
                562 => 73,
                564 => 77,
                566 => 78,
                _ => 0
            };
        }

        #endregion

        #region Skill Casting

        private sealed class QueuedSkillRequest
        {
            public int SkillId { get; init; }
            public int EarliestExecuteTime { get; init; }
        }

        // Skill queue for macro execution
        private readonly Queue<QueuedSkillRequest> _skillQueue = new();
        private readonly Queue<QueuedFollowUpAttack> _queuedFollowUpAttacks = new();
        private readonly Queue<DeferredSkillPayload> _deferredSkillPayloads = new();
        private readonly List<PendingProjectileSpawn> _pendingProjectileSpawns = new();
        private readonly List<QueuedSummonAttack> _queuedSummonAttacks = new();
        private long _nextQueuedSummonAttackSequenceId = 1;
        private QueuedSerialAttack _queuedSerialAttack;
        private QueuedSparkAttack _queuedSparkAttack;
        private DeferredMovingShootExecutionState _lastDeferredMovingShootExecution;
        private readonly List<ClientSkillTimer> _clientSkillTimers = new();
        private int _lastQueuedSkillTime = 0;
        private const int SKILL_QUEUE_DELAY = 100; // ms between queued skill attempts
        private const int FOLLOW_UP_ATTACK_DELAY = 90;
        private const int SPARK_SKILL_ID = 15111006;
        private const int ROCKET_BOOSTER_SKILL_ID = 35101004;
        private const int ROCKET_BOOSTER_RECOVERY_EFFECT_SKILL_ID = 35100004;
        private const int WildHunterJaguarRiderSkillId = 33001001;
        private const int WildHunterJaguarJumpSkillId = 33001002;
        private const int ROCKET_BOOSTER_LANDING_RECOVERY_MS = 500;
        private const int BATTLESHIP_TAMING_MOB_ID = 1932000;
        private const int BATTLESHIP_SKILL_ID = 5221006;
        private const int MECHANIC_TAMING_MOB_ID = 1932016;
        private const int MECHANIC_KEYDOWN_MAX_DURATION_MS = 8000;
        private const int CLIENT_VEHICLE_OWNERSHIP_GRACE_WINDOW_MS = 1000;
        private const int CYCLONE_SKILL_ID = 32121003;
        private const int MINE_SKILL_ID = 33101008;
        private const int SG88_SKILL_ID = 35121003;
        private const int TESLA_COIL_SKILL_ID = 35111002;
        private const int TeslaMinimumImpactDelayMs = 300;
        private const int SATELLITE_SKILL_ID = 35111001;
        private const int ENHANCED_SATELLITE_SKILL_ID = 35111009;
        private const int SATELLITE_2_SKILL_ID = 35111010;
        private const int HEALING_ROBOT_SKILL_ID = 35111011;
        private const int BATTLE_MAGE_DARK_AURA_SKILL_ID = 32001003;
        private const int BATTLE_MAGE_BLUE_AURA_SKILL_ID = 32101002;
        private const int BATTLE_MAGE_YELLOW_AURA_SKILL_ID = 32101003;
        private const int ExternalAreaSupportBuffIdBase = -1500000000;
        private const int AffectedSkillSupportBuffIdBase = -1600000000;
        private const int MINE_DEPLOY_INTERVAL_MS = 1500;
        private const int MineInitialDeployLeadMs = 1000;
        private const int CYCLONE_ATTACK_INTERVAL_MS = 1000;
        private const int SMOKE_BOMB_SKILL_ID = 4221006;
        private const int SG88_KEYDOWN_BAR_START_DELAY_MS = 810;
        private const int SG88_ASSIST_ARM_DELAY_MS = 2790;
        private const int SG88_ASSIST_REMOVE_DELAY_MS = 2520;
        private const int SG88_MANUAL_CLUSTER_FOLLOW_UP_DELAY_MS = 120;
        private const int SG88_MANUAL_TRAPEZOID_SLICE_WIDTH_PX = 20;
        private const int SG88_MANUAL_TRAPEZOID_SLOPE_RATIO = 4;
        private const int RepeatSkillTankSiegeId = 35121013;
        private const int RepeatSkillTankModeId = 35121005;
        private const int RepeatSkillSiegeId = 35111004;
        private const int SIEGE_REPEAT_ATTACK_DELAY_MS = 180;
        private const int TANK_REPEAT_ATTACK_DELAY_MS = 420;
        private const int OWNER_ATTACK_TARGET_MEMORY_MS = 1500;
        private const int SUMMON_BODY_CONTACT_COOLDOWN_MS = 700;
        private const int SUMMON_HIT_PERIOD_DURATION_MS = 1500;
        private const int SUMMON_PASSIVE_EFFECT_COOLDOWN_MS = 240;
        private const int BEHOLDER_SUMMON_SKILL_ID = 1321007;
        private const int BEHOLDER_HEAL_SKILL_ID = 1320008;
        private const int BEHOLDER_BUFF_SKILL_ID = 1320009;
        private const int BEHOLDER_BUFF_PDD_ID = -13200090;
        private const int BEHOLDER_BUFF_MDD_ID = -13200091;
        private const int BEHOLDER_BUFF_ACC_ID = -13200092;
        private const int BEHOLDER_BUFF_EVA_ID = -13200093;
        private const int BEHOLDER_BUFF_PAD_ID = -13200094;
        private static readonly int[] PassiveWeaponSpecificStatWeaponCodes =
        {
            30, 31, 32, 33, 34, 36, 37, 38, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 52, 53, 56, 58
        };
        private const float TeslaTriangleMinimumEdgeLengthSq = 16f;
        private static readonly int[] WildHunterJaguarTamingMobCandidateIds =
        {
            1932030,
            1932031,
            1932032,
            1932033,
            1932036,
            1932015
        };

        private RocketBoosterState _rocketBoosterState;
        private CycloneState _cycloneState;
        private SwallowState _swallowState;
        private int _mineMovementDirection;
        private int _mineMovementStartTime;
        private RepeatSkillSustainState _activeRepeatSkillSustain;
        private int _lastClientVehicleValidSkillId;
        private int _clientVehicleValidCount;
        private int _clientVehicleValidStartTime = int.MinValue;
        private readonly Dictionary<int, int> _recentOwnerAttackTargetTimes = new();

        /// <summary>
        /// Queue a skill for execution (used by skill macros)
        /// </summary>
        public void QueueSkill(int skillId, int earliestExecuteTime = 0)
        {
            if (skillId > 0)
            {
                _skillQueue.Enqueue(new QueuedSkillRequest
                {
                    SkillId = skillId,
                    EarliestExecuteTime = earliestExecuteTime
                });
            }
        }

        /// <summary>
        /// Clear the skill queue
        /// </summary>
        public void ClearSkillQueue()
        {
            _skillQueue.Clear();
        }

        /// <summary>
        /// Process queued skills (called from Update)
        /// </summary>
        private void ProcessSkillQueue(int currentTime)
        {
            if (_skillQueue.Count == 0)
                return;

            if (_currentCast?.IsComplete == false || _preparedSkill != null)
                return;

            // Rate limit queue processing
            if (currentTime - _lastQueuedSkillTime < SKILL_QUEUE_DELAY)
                return;

            // Try to cast the next queued skill
            while (_skillQueue.Count > 0)
            {
                QueuedSkillRequest queuedSkill = _skillQueue.Peek();
                if (currentTime < queuedSkill.EarliestExecuteTime)
                    return;

                int skillId = queuedSkill.SkillId;

                if (TryCastSkill(skillId, currentTime))
                {
                    _skillQueue.Dequeue();
                    _lastQueuedSkillTime = currentTime;
                    break; // Only cast one skill per frame
                }
                else if (!CanCastSkill(skillId, currentTime))
                {
                    // Can't cast this skill (on cooldown, no MP, etc.)
                    // Remove it from queue to avoid blocking
                    _skillQueue.Dequeue();
                }
                else
                {
                    // Skill might be castable later, keep it in queue
                    break;
                }
            }
        }

        /// <summary>
        /// Try to cast a skill
        /// </summary>
        public bool TryCastSkill(int skillId, int currentTime)
        {
            return TryCastSkill(skillId, currentTime, ownerHotkeySlot: -1, ownerInputToken: 0);
        }

        public bool TryCastPacketOwnedFuncKeySkill(int skillId, int currentTime, int ownerInputToken)
        {
            return TryCastSkill(skillId, currentTime, ownerHotkeySlot: -1, ownerInputToken);
        }

        public void ArmPacketOwnedExJablin()
        {
            _packetOwnedNextShootExJablinArmed = true;
        }

        public bool TryApplyPacketOwnedTimeBombAttack(
            int skillId,
            int currentTime,
            Vector2 timeBombPosition,
            out SkillData skill,
            out int level,
            out string errorMessage)
        {
            return TryApplyPacketOwnedMeleeAttackCore(
                skillId,
                currentTime,
                rawActionCode: null,
                preferredTargetPositionOverride: timeBombPosition,
                attackOriginOverride: timeBombPosition,
                attackTargetSelectionMode: AttackTargetSelectionMode.PacketOwnedTimeBomb,
                out skill,
                out level,
                out _,
                out errorMessage);
        }

        public bool TryApplyPacketOwnedMeleeAttack(int skillId, int currentTime, out SkillData skill, out int level, out string errorMessage)
        {
            return TryApplyPacketOwnedMeleeAttackCore(
                skillId,
                currentTime,
                rawActionCode: null,
                preferredTargetPositionOverride: null,
                attackOriginOverride: null,
                attackTargetSelectionMode: AttackTargetSelectionMode.Default,
                out skill,
                out level,
                out _,
                out errorMessage);
        }

        public bool TryApplyPacketOwnedMeleeAttack(
            int skillId,
            int currentTime,
            int? rawActionCode,
            Vector2? preferredTargetPositionOverride,
            Vector2? attackOriginOverride,
            out SkillData skill,
            out int level,
            out string errorMessage)
        {
            return TryApplyPacketOwnedMeleeAttackCore(
                skillId,
                currentTime,
                rawActionCode,
                preferredTargetPositionOverride,
                attackOriginOverride,
                AttackTargetSelectionMode.Default,
                out skill,
                out level,
                out _,
                out errorMessage);
        }

        public bool TryApplyPacketOwnedMeleeAttack(
            int skillId,
            int currentTime,
            int? rawActionCode,
            out SkillData skill,
            out int level,
            out string resolvedActionName,
            out string errorMessage)
        {
            return TryApplyPacketOwnedMeleeAttackCore(
                skillId,
                currentTime,
                rawActionCode,
                preferredTargetPositionOverride: null,
                attackOriginOverride: null,
                AttackTargetSelectionMode.Default,
                out skill,
                out level,
                out resolvedActionName,
                out errorMessage);
        }

        private bool TryApplyPacketOwnedMeleeAttackCore(
            int skillId,
            int currentTime,
            int? rawActionCode,
            Vector2? preferredTargetPositionOverride,
            Vector2? attackOriginOverride,
            AttackTargetSelectionMode attackTargetSelectionMode,
            out SkillData skill,
            out int level,
            out string resolvedActionName,
            out string errorMessage)
        {
            skill = null;
            level = 0;
            resolvedActionName = null;
            errorMessage = null;

            if (skillId <= 0)
            {
                errorMessage = "Packet-owned skill payload did not contain a valid skill id.";
                return false;
            }

            skill = GetSkillData(skillId) ?? _loader?.LoadSkill(skillId);
            if (skill == null)
            {
                errorMessage = $"Skill {skillId} is not available in the loaded skill data.";
                return false;
            }

            level = GetSkillLevel(skillId);
            if (level <= 0)
            {
                errorMessage = $"Skill {skill.Name ?? skillId.ToString()} is not learned on the local character.";
                return false;
            }

            SkillLevelData levelData = skill.GetLevel(level);
            if (levelData == null)
            {
                errorMessage = $"Skill {skill.Name ?? skillId.ToString()} does not expose level data for level {level}.";
                return false;
            }

            resolvedActionName = ResolvePacketOwnedMeleeAttackActionName(
                skill,
                rawActionCode,
                _player?.CurrentActionName,
                candidate => _player?.CanRenderAction(candidate) == true);
            TriggerSkillAnimation(skill, currentTime, resolvedActionName);
            ProcessMeleeAttack(
                skill,
                level,
                currentTime,
                _player.FacingRight,
                queueFollowUps: false,
                preferredTargetPosition: preferredTargetPositionOverride,
                attackOriginOverride: attackOriginOverride,
                attackTargetSelectionMode: attackTargetSelectionMode);
            return true;
        }

        private bool TryCastSkill(int skillId, int currentTime, int ownerHotkeySlot, int ownerInputToken)
        {
            int level = GetSkillLevel(skillId);
            if (level <= 0)
                return false;

            var skill = GetSkillData(skillId);
            if (skill == null)
                return false;

            SkillLevelData levelData = skill.GetLevel(level);

            if (_preparedSkill != null && _preparedSkill.SkillId == skillId)
            {
                if (!CanInputSourceControlPreparedSkill(_preparedSkill, ownerHotkeySlot, ownerInputToken))
                {
                    return false;
                }

                if (RejectClientSkillCancellation(_preparedSkill.SkillData ?? skill))
                {
                    return false;
                }

                RecordPreparedSkillExclusiveRequest(currentTime);
                ReleasePreparedSkill(currentTime);
                return true;
            }

            if (TryToggleRepeatSkillManualAssist(skillId, currentTime))
            {
                return true;
            }

            string stateRestrictionMessage = GetStateRestrictionMessage(skill, currentTime);
            if (!string.IsNullOrWhiteSpace(stateRestrictionMessage))
            {
                OnFieldSkillCastRejected?.Invoke(skill, stateRestrictionMessage);
                return false;
            }

            if (_fieldSkillRestrictionEvaluator != null && !_fieldSkillRestrictionEvaluator(skill))
            {
                string message = _fieldSkillRestrictionMessageProvider?.Invoke(skill);
                if (!string.IsNullOrWhiteSpace(message))
                {
                    OnFieldSkillCastRejected?.Invoke(skill, message);
                }
                return false;
            }

            string shootAttackRestrictionMessage = GetShootAttackRestrictionMessage(skill, levelData);
            if (!string.IsNullOrWhiteSpace(shootAttackRestrictionMessage))
            {
                OnFieldSkillCastRejected?.Invoke(skill, shootAttackRestrictionMessage);
                return false;
            }

            int cooldownRemainingMs = GetCooldownRemaining(skillId, currentTime);
            if (cooldownRemainingMs > 0)
            {
                OnSkillCooldownBlocked?.Invoke(skill, cooldownRemainingMs, currentTime);
                return false;
            }

            // Check if can cast
            if (!CanCastSkill(skillId, currentTime))
                return false;

            if (ShouldThrottlePreparedSkillRequest(skill, currentTime))
                return false;

            // Start casting
            StartCast(skill, level, currentTime, ownerHotkeySlot, ownerInputToken);
            return true;
        }

        /// <summary>
        /// Try to cast skill on hotkey
        /// </summary>
        public bool TryCastHotkey(int keyIndex, int currentTime, int ownerInputToken = 0)
        {
            int macroIndex = GetHotkeyMacroIndex(keyIndex);
            if (macroIndex >= 0)
                return TryExecuteMacro(macroIndex, currentTime);

            ItemHotkeyBinding itemBinding = GetHotkeyItemBinding(keyIndex);
            if (itemBinding != null)
                return OnItemHotkeyUseRequested?.Invoke(itemBinding.ItemId, itemBinding.InventoryType, currentTime) == true;

            int skillId = GetHotkeySkill(keyIndex);
            if (skillId <= 0)
                return false;

            return TryCastSkill(skillId, currentTime, keyIndex, ownerInputToken);
        }

        public void ReleaseHotkeyIfActive(int keyIndex, int currentTime, int ownerInputToken = 0)
        {
            if (_preparedSkill?.IsKeydownSkill != true)
                return;

            if (GetHotkeyMacroIndex(keyIndex) >= 0 || GetHotkeyItemBinding(keyIndex) != null)
                return;

            int skillId = GetHotkeySkill(keyIndex);
            if (skillId > 0
                && skillId == _preparedSkill.SkillId
                && CanInputSourceControlPreparedSkill(_preparedSkill, keyIndex, ownerInputToken))
            {
                if (RejectClientSkillCancellation(_preparedSkill.SkillData))
                {
                    return;
                }

                RecordPreparedSkillExclusiveRequest(currentTime);
                ReleasePreparedSkill(currentTime);
            }
        }

        public void ReleasePacketOwnedFuncKeySkillIfActive(int skillId, int currentTime, int ownerInputToken)
        {
            if (_preparedSkill?.IsKeydownSkill != true)
                return;

            if (skillId <= 0
                || _preparedSkill.SkillId != skillId
                || !CanInputSourceControlPreparedSkill(_preparedSkill, ownerHotkeySlot: -1, ownerInputToken))
            {
                return;
            }

            if (RejectClientSkillCancellation(_preparedSkill.SkillData))
            {
                return;
            }

            RecordPreparedSkillExclusiveRequest(currentTime);
            ReleasePreparedSkill(currentTime);
        }

        public bool TryExecuteMacro(int macroIndex, int currentTime)
        {
            SkillMacro macro = _macroResolver?.Invoke(macroIndex);
            if (macro == null || !macro.IsEnabled)
                return false;

            ClearSkillQueue();

            int skillCount = 0;
            foreach (int skillId in macro.SkillIds ?? Array.Empty<int>())
            {
                if (skillId <= 0 || !CanAssignHotkeySkill(skillId))
                    continue;

                QueueSkill(skillId, currentTime + (skillCount * SKILL_QUEUE_DELAY));
                skillCount++;
            }

            if (skillCount == 0)
                return false;

            if (macro.NotifyParty && !string.IsNullOrWhiteSpace(macro.Name))
            {
                OnMacroPartyNotifyRequested?.Invoke(macro.Name);
            }

            return true;
        }

        public void ReleaseActiveKeydownSkill(int currentTime)
        {
            if (_preparedSkill?.IsKeydownSkill == true)
            {
                if (RejectClientSkillCancellation(_preparedSkill.SkillData))
                {
                    return;
                }

                RecordPreparedSkillExclusiveRequest(currentTime);
                ReleasePreparedSkill(currentTime);
            }
        }

        public bool RequestClientSkillCancel(int skillId, int currentTime, bool enforceFieldCancelRestrictions = false)
        {
            if (skillId <= 0)
            {
                return false;
            }

            int[] cancelSkillIds = ResolveClientCancelRequestSkillIds(skillId);
            if (cancelSkillIds.Length == 0)
            {
                return false;
            }

            if (enforceFieldCancelRestrictions)
            {
                SkillData restrictedSkill = ResolveClientCancelRestrictionSkill(skillId, cancelSkillIds);
                if (RejectClientSkillCancellation(restrictedSkill))
                {
                    return false;
                }
            }

            bool stateChanged = false;
            foreach (int cancelSkillId in cancelSkillIds)
            {
                stateChanged |= CancelClientOwnedSkillState(cancelSkillId, currentTime);
            }

            bool sideEffectsApplied = ApplyClientSkillCancelSideEffects(cancelSkillIds, currentTime);
            if (!stateChanged && !sideEffectsApplied)
            {
                return false;
            }

            RecordPreparedSkillExclusiveRequest(currentTime);
            foreach (int cancelSkillId in cancelSkillIds)
            {
                OnClientSkillCancelRequested?.Invoke(cancelSkillId, skillId, currentTime);
            }

            return true;
        }

        /// <summary>
        /// Check if skill can be cast
        /// </summary>
        public bool CanCastSkill(int skillId, int currentTime)
        {
            int level = GetSkillLevel(skillId);
            if (level <= 0)
                return false;

            var skill = GetSkillData(skillId);
            if (skill == null)
                return false;

            // Check passive
            if (skill.IsPassive)
                return false;

            if (skill.SuppressesStandaloneActiveCast)
                return false;

            if (!IsSkillAllowedForCurrentJob(skill))
                return false;

            if (!string.IsNullOrWhiteSpace(GetStateRestrictionMessage(skill, currentTime)))
                return false;

            if (_externalCastBlockedEvaluator?.Invoke(currentTime) == true)
                return false;

            if (_fieldSkillRestrictionEvaluator != null && !_fieldSkillRestrictionEvaluator(skill))
                return false;

            if (CanToggleRepeatSkillManualAssist(skillId))
                return true;

            if (IsSkillCastBlockedByRepeatSkillSustain())
                return false;

            // Check cooldown
            if (IsOnCooldown(skillId, currentTime))
                return false;

            // Check MP
            var levelData = skill.GetLevel(level);
            if (levelData == null)
                return false;

            if (!CanAffordSkillCost(levelData))
                return false;

            if (!string.IsNullOrWhiteSpace(GetShootAttackRestrictionMessage(skill, levelData)))
                return false;

            if (ShouldUseSmoothingMovingShoot(skill) && HasPendingDeferredMovingShootPayload(currentTime))
                return false;

            // Check if already casting
            if (_currentCast != null && !_currentCast.IsComplete)
                return false;

            if (_preparedSkill != null)
                return false;

            // Attack skills cannot be cast while on ladder/rope/swimming (buffs and heals are allowed)
            if (skill.IsAttack && !_player.CanAttack)
                return false;

            return true;
        }

        /// <summary>
        /// Check cooldown
        /// </summary>
        public bool IsOnCooldown(int skillId, int currentTime)
        {
            return GetCooldownRemaining(skillId, currentTime) > 0;
        }

        /// <summary>
        /// Get remaining cooldown
        /// </summary>
        public int GetCooldownRemaining(int skillId, int currentTime)
        {
            if (_serverCooldownExpireTimes.TryGetValue(skillId, out int expireTime))
            {
                int overrideRemaining = Math.Max(0, expireTime - currentTime);
                if (overrideRemaining <= 0)
                {
                    MarkAuthoritativeCooldownExpired(skillId);
                }

                return overrideRemaining;
            }

            if (_expiredAuthoritativeCooldowns.Contains(skillId))
            {
                return 0;
            }

            if (!_cooldowns.TryGetValue(skillId, out int lastCast))
                return 0;

            var skill = GetSkillData(skillId);
            int level = GetSkillLevel(skillId);
            var levelData = skill?.GetLevel(level);

            if (levelData == null || levelData.Cooldown <= 0)
                return 0;

            int remaining = levelData.Cooldown - (currentTime - lastCast);
            return Math.Max(0, remaining);
        }

        public int GetCooldownDuration(int skillId, int currentTime)
        {
            if (_serverCooldownExpireTimes.TryGetValue(skillId, out int expireTime))
            {
                if (expireTime <= currentTime)
                {
                    MarkAuthoritativeCooldownExpired(skillId);
                    return 0;
                }
                else if (_cooldowns.TryGetValue(skillId, out int startTime))
                {
                    return Math.Max(0, expireTime - startTime);
                }
                else
                {
                    return Math.Max(0, expireTime - currentTime);
                }
            }

            if (_expiredAuthoritativeCooldowns.Contains(skillId))
            {
                return 0;
            }

            if (!_cooldowns.TryGetValue(skillId, out _))
            {
                return 0;
            }

            var skill = GetSkillData(skillId);
            int level = GetSkillLevel(skillId);
            var levelData = skill?.GetLevel(level);
            return Math.Max(0, levelData?.Cooldown ?? 0);
        }

        internal bool TryGetCooldownUiState(int skillId, int currentTime, out CooldownUiState state)
        {
            state = default;

            int remainingMs = GetCooldownRemaining(skillId, currentTime);
            if (remainingMs <= 0)
            {
                return false;
            }

            int durationMs = Math.Max(remainingMs, GetCooldownDuration(skillId, currentTime));
            if (_cooldownUiPresentations.TryGetValue(skillId, out CooldownUiPresentationState presentation)
                && presentation?.Kind == CooldownUiPresentationKind.VehicleDurability)
            {
                int currentValue = Math.Max(0, presentation.CurrentValue);
                int maxValue = Math.Max(currentValue, presentation.MaxValue);
                float progress = maxValue > 0
                    ? Math.Clamp(currentValue / (float)maxValue, 0f, 1f)
                    : 0f;
                string tooltipStateText = maxValue > 0
                    ? $"Durability: {currentValue}/{maxValue}"
                    : $"Durability: {currentValue}";
                state = new CooldownUiState(
                    remainingMs,
                    durationMs,
                    progress,
                    currentValue.ToString(),
                    tooltipStateText,
                    DisplayInCooldownUi: false,
                    SuppressProgressOverlay: true,
                    SuppressCounterText: false,
                    PresentationKind: CooldownUiPresentationKind.VehicleDurability,
                    CurrentValue: currentValue,
                    MaxValue: maxValue);
                return true;
            }

            string counterText = Math.Max(1, (int)Math.Ceiling(remainingMs / 1000f)).ToString();
            state = new CooldownUiState(
                remainingMs,
                durationMs,
                durationMs > 0 ? Math.Clamp(remainingMs / (float)durationMs, 0f, 1f) : 1f,
                counterText,
                null,
                DisplayInCooldownUi: true,
                SuppressProgressOverlay: false,
                SuppressCounterText: false,
                PresentationKind: CooldownUiPresentationKind.Default,
                CurrentValue: 0,
                MaxValue: 0);
            return true;
        }

        internal bool TryGetCooldownMaskVisualState(int skillId, int currentTime, out int frameIndex, out string remainingText)
        {
            frameIndex = 15;
            remainingText = string.Empty;

            return TryGetCooldownUiState(skillId, currentTime, out CooldownUiState cooldownState)
                && TryResolveCooldownMaskVisualState(cooldownState, out frameIndex, out remainingText);
        }

        internal static bool TryResolveCooldownMaskVisualState(
            CooldownUiState cooldownState,
            out int frameIndex,
            out string remainingText)
        {
            frameIndex = 15;
            remainingText = string.Empty;

            if (!cooldownState.DisplayInCooldownUi)
            {
                return false;
            }

            int remainingMs = Math.Max(0, cooldownState.RemainingMs);
            if (remainingMs <= 0)
            {
                return false;
            }

            int durationMs = Math.Max(remainingMs, cooldownState.DurationMs);
            if (durationMs <= 0)
            {
                return false;
            }

            if (!cooldownState.SuppressProgressOverlay)
            {
                frameIndex = ResolveCooldownMaskFrameIndex(remainingMs, durationMs);
            }

            remainingText = cooldownState.SuppressCounterText
                ? string.Empty
                : string.IsNullOrWhiteSpace(cooldownState.CounterText)
                    ? Math.Max(1, (int)Math.Ceiling(remainingMs / 1000f)).ToString()
                    : cooldownState.CounterText;
            return true;
        }

        internal static int ResolveCooldownMaskFrameIndex(int remainingMs, int durationMs)
        {
            int clampedRemainingMs = Math.Max(0, remainingMs);
            int resolvedDurationMs = Math.Max(clampedRemainingMs, durationMs);

            if (resolvedDurationMs <= 0)
            {
                return 15;
            }

            float progress = 1f - Math.Clamp(clampedRemainingMs / (float)resolvedDurationMs, 0f, 1f);
            return Math.Clamp((int)Math.Floor(progress * 16f), 0, 15);
        }

        internal static float ResolveCooldownMaskFallbackFillRatio(int frameIndex)
        {
            int clampedFrameIndex = Math.Clamp(frameIndex, 0, 15);
            return Math.Clamp((16 - clampedFrameIndex) / 16f, 0f, 1f);
        }

        public void SetAuthoritativeVehicleDurabilityPresentation(int skillId, int currentValue, int maxValue)
        {
            if (skillId <= 0)
            {
                return;
            }

            _cooldownUiPresentations[skillId] = new CooldownUiPresentationState
            {
                Kind = CooldownUiPresentationKind.VehicleDurability,
                CurrentValue = Math.Max(0, currentValue),
                MaxValue = Math.Max(0, maxValue)
            };
        }

        public void ClearAuthoritativeCooldownPresentation(int skillId)
        {
            if (skillId <= 0)
            {
                return;
            }

            _cooldownUiPresentations.Remove(skillId);
        }

        /// <summary>
        /// Get the tick count when the current cooldown started.
        /// </summary>
        public bool TryGetCooldownStartTime(int skillId, out int startTime)
        {
            return _cooldowns.TryGetValue(skillId, out startTime);
        }

        public IReadOnlyList<int> GetActiveCooldownSkillIds(int currentTime)
        {
            if (_cooldowns.Count == 0)
            {
                return Array.Empty<int>();
            }

            List<int> activeSkillIds = new List<int>(_cooldowns.Count);
            foreach (int skillId in _cooldowns.Keys)
            {
                if (GetCooldownRemaining(skillId, currentTime) > 0)
                {
                    activeSkillIds.Add(skillId);
                }
            }

            return activeSkillIds;
        }

        public void SetServerCooldownRemaining(int skillId, int remainingMs, int currentTime)
        {
            if (skillId <= 0)
            {
                return;
            }

            if (remainingMs <= 0)
            {
                ClearServerCooldown(skillId, currentTime);
                return;
            }

            bool hadActiveCooldown = GetCooldownRemaining(skillId, currentTime) > 0;
            int cooldownStartTime = hadActiveCooldown && _cooldowns.TryGetValue(skillId, out int existingStartTime)
                ? existingStartTime
                : currentTime;
            int expireTime = unchecked(currentTime + remainingMs);
            _expiredAuthoritativeCooldowns.Remove(skillId);
            _cooldowns[skillId] = cooldownStartTime;
            _serverCooldownExpireTimes[skillId] = expireTime;
            _pendingCooldownCompletionNotifications.Add(skillId);

            SkillData skill = GetSkillData(skillId);
            if (!hadActiveCooldown && skill != null)
            {
                OnSkillCooldownStarted?.Invoke(skill, remainingMs, currentTime);
            }
        }

        public void ClearServerCooldown(int skillId, int currentTime = int.MinValue)
        {
            if (skillId <= 0)
            {
                return;
            }

            bool hadActiveCooldown = currentTime != int.MinValue && GetCooldownRemaining(skillId, currentTime) > 0;
            _serverCooldownExpireTimes.Remove(skillId);
            _pendingCooldownCompletionNotifications.Remove(skillId);
            _expiredAuthoritativeCooldowns.Remove(skillId);
            _cooldownUiPresentations.Remove(skillId);
            _cooldowns.Remove(skillId);

            if (!hadActiveCooldown)
            {
                return;
            }

            SkillData skill = GetSkillData(skillId);
            if (skill != null)
            {
                OnSkillCooldownCompleted?.Invoke(skill, currentTime);
            }
        }

        internal void ApplyAuthoritativeCooldownSnapshot(IReadOnlyDictionary<int, int> remainingSecondsBySkillId, int currentTime)
        {
            if (remainingSecondsBySkillId == null)
            {
                return;
            }

            HashSet<int> authoritativeSkillIds = new(remainingSecondsBySkillId.Count);
            foreach ((int skillId, int remainingSeconds) in remainingSecondsBySkillId)
            {
                if (skillId <= 0)
                {
                    continue;
                }

                authoritativeSkillIds.Add(skillId);
                int remainingMs = Math.Max(0, remainingSeconds) * 1000;
                bool hadActiveCooldown = GetCooldownRemaining(skillId, currentTime) > 0;
                int cooldownStartTime = hadActiveCooldown && _cooldowns.TryGetValue(skillId, out int existingStartTime)
                    ? existingStartTime
                    : currentTime;

                _pendingCooldownCompletionNotifications.Remove(skillId);
                _expiredAuthoritativeCooldowns.Remove(skillId);
                _cooldownUiPresentations.Remove(skillId);

                if (remainingMs <= 0)
                {
                    _serverCooldownExpireTimes.Remove(skillId);
                    _cooldowns.Remove(skillId);
                    continue;
                }

                _cooldowns[skillId] = cooldownStartTime;
                _serverCooldownExpireTimes[skillId] = unchecked(currentTime + remainingMs);
                _pendingCooldownCompletionNotifications.Add(skillId);
            }

            foreach (int activeSkillId in GetActiveCooldownSkillIds(currentTime).ToList())
            {
                if (authoritativeSkillIds.Contains(activeSkillId))
                {
                    continue;
                }

                _serverCooldownExpireTimes.Remove(activeSkillId);
                _pendingCooldownCompletionNotifications.Remove(activeSkillId);
                _expiredAuthoritativeCooldowns.Remove(activeSkillId);
                _cooldownUiPresentations.Remove(activeSkillId);
                _cooldowns.Remove(activeSkillId);
            }
        }

        internal void ApplyAuthoritativeSkillRecordSnapshot(IReadOnlyDictionary<int, int> skillLevelsBySkillId)
        {
            if (skillLevelsBySkillId == null)
            {
                return;
            }

            HashSet<int> authoritativeSkillIds = new(skillLevelsBySkillId.Count);
            foreach ((int skillId, int level) in skillLevelsBySkillId)
            {
                if (skillId <= 0)
                {
                    continue;
                }

                authoritativeSkillIds.Add(skillId);
                if (level <= 0)
                {
                    _skillLevels.Remove(skillId);
                    _skillMasterLevels.Remove(skillId);
                    continue;
                }

                _skillLevels[skillId] = level;
            }

            foreach (int activeSkillId in _skillLevels.Keys.ToList())
            {
                if (authoritativeSkillIds.Contains(activeSkillId))
                {
                    continue;
                }

                _skillLevels.Remove(activeSkillId);
                _skillMasterLevels.Remove(activeSkillId);
            }

            RevalidateHotkeys();
        }

        internal void ApplyAuthoritativeSkillMasterLevelSnapshot(IReadOnlyDictionary<int, int> skillMasterLevelsBySkillId)
        {
            if (skillMasterLevelsBySkillId == null)
            {
                return;
            }

            HashSet<int> authoritativeSkillIds = new(skillMasterLevelsBySkillId.Count);
            foreach ((int skillId, int masterLevel) in skillMasterLevelsBySkillId)
            {
                if (skillId <= 0)
                {
                    continue;
                }

                authoritativeSkillIds.Add(skillId);
                if (masterLevel <= 0)
                {
                    _skillMasterLevels.Remove(skillId);
                    continue;
                }

                _skillMasterLevels[skillId] = masterLevel;
            }

            foreach (int activeSkillId in _skillMasterLevels.Keys.ToList())
            {
                if (!authoritativeSkillIds.Contains(activeSkillId))
                {
                    _skillMasterLevels.Remove(activeSkillId);
                }
            }
        }

        private void StartCast(SkillData skill, int level, int currentTime, int ownerHotkeySlot = -1, int ownerInputToken = 0)
        {
            var levelData = skill.GetLevel(level);
            if (levelData == null)
                return;

            bool usesReleaseTriggeredKeydown = UsesReleaseTriggeredKeydownExecution(skill);
            bool usesPreparedKeydownFlow = skill.IsKeydownSkill || usesReleaseTriggeredKeydown;

            _currentCast = new SkillCastInfo
            {
                SkillId = skill.SkillId,
                Level = level,
                SkillData = skill,
                LevelData = levelData,
                EffectAnimation = GetInitialCastEffect(skill),
                SecondaryEffectAnimation = GetInitialCastSecondaryEffect(skill),
                CastTime = currentTime,
                CasterId = 0, // Player ID
                CasterX = _player.X,
                CasterY = _player.Y,
                FacingRight = _player.FacingRight
            };

            string prepareActionName = (usesPreparedKeydownFlow || skill.IsPrepareSkill)
                ? GetPrepareActionName(skill)
                : null;
            prepareActionName = ResolvePreparedAvatarActionName(skill, prepareActionName);
            int repeatReturnSkillId = ResolveRepeatSkillReturnSkillId(skill.SkillId);

            bool defersExecutionResourceConsumption = DefersExecutionResourceConsumption(skill);
            if (!defersExecutionResourceConsumption)
            {
                ApplySkillCooldown(skill, levelData, currentTime);
            }

            ApplySkillMount(skill, levelData);
            TriggerSkillAnimation(skill, currentTime, prepareActionName);
            BeginRepeatSkillSustain(skill, levelData, currentTime, repeatReturnSkillId);
            if (usesPreparedKeydownFlow)
            {
                _player.BeginSustainedSkillAnimation(prepareActionName);
            }

            if (_currentCast != null && TryApplyClientOwnedAvatarEffect(skill, currentTime))
            {
                _currentCast.SuppressEffectAnimation = true;
            }
            PlayCastSound(skill);
            OnSkillCast?.Invoke(_currentCast);

            if (usesPreparedKeydownFlow)
            {
                BeginPreparedSkill(skill, level, currentTime, ownerHotkeySlot, ownerInputToken);
                return;
            }

            if (!defersExecutionResourceConsumption && !TryConsumeSkillResources(skill, levelData))
                return;

            if (skill.IsPrepareSkill)
            {
                BeginPreparedSkill(skill, level, currentTime, ownerHotkeySlot, ownerInputToken);
                return;
            }

            ExecuteSkillPayload(skill, level, currentTime);
        }

        private void ApplySkillCooldown(SkillData skill, SkillLevelData levelData, int currentTime)
        {
            if (levelData.Cooldown > 0)
            {
                _expiredAuthoritativeCooldowns.Remove(skill.SkillId);
                _cooldowns[skill.SkillId] = currentTime;
                _serverCooldownExpireTimes.Remove(skill.SkillId);
                _pendingCooldownCompletionNotifications.Add(skill.SkillId);
                OnSkillCooldownStarted?.Invoke(skill, levelData.Cooldown, currentTime);
            }
        }

        private bool TryConsumeSkillResources(SkillData skill, SkillLevelData levelData, int additionalHpCost = 0)
        {
            if (levelData == null || !CanAffordSkillCost(levelData, additionalHpCost))
                return false;

            if (!string.IsNullOrWhiteSpace(GetShootAttackRestrictionMessage(skill, levelData)))
                return false;

            if (!TryConsumeShootAmmo(skill, levelData))
                return false;

            _player.MP = Math.Max(0, _player.MP - levelData.MpCon);

            int totalHpCost = Math.Max(0, levelData.HpCon) + Math.Max(0, additionalHpCost);
            if (totalHpCost > 0)
            {
                _player.HP = Math.Max(1, _player.HP - totalHpCost);
            }

            return true;
        }

        private bool CanAffordSkillCost(SkillLevelData levelData, int additionalHpCost = 0)
        {
            if (levelData == null)
                return false;

            if (_player.MP < levelData.MpCon)
                return false;

            int totalHpCost = Math.Max(0, levelData.HpCon) + Math.Max(0, additionalHpCost);
            return _player.HP > totalHpCost;
        }

        private string GetShootAttackRestrictionMessage(SkillData skill, SkillLevelData levelData = null)
        {
            if (!RequiresClientShootAttackValidation(skill))
            {
                return null;
            }

            if (!HasRequiredShootAttackState(skill))
            {
                return "This skill requires its supporting buff to be active.";
            }

            int skillId = skill.SkillId;
            if (IsShootSkillNotUsingShootingWeapon(skillId))
            {
                return null;
            }

            int weaponCode = GetEquippedWeaponCode();
            if (!IsValidShootingWeaponForSkill(skillId, weaponCode))
            {
                return "This skill requires the correct ranged weapon.";
            }

            if (IsShootSkillNotConsumingBullet(skillId) || HasShootAmmoBypassTemporaryStat())
            {
                return null;
            }

            int requiredAmmoCount = ResolveRequiredShootAmmoCount(levelData);
            return HasCompatibleShootAmmo(skillId, levelData, weaponCode, requiredAmmoCount)
                ? null
                : GetShootAmmoRestrictionMessage(weaponCode);
        }

        private static bool RequiresClientShootAttackValidation(SkillData skill)
        {
            return skill?.IsAttack == true && skill.AttackType == SkillAttackType.Ranged;
        }

        internal static int[] ResolveClientShootAttackRequiredSkillIds(SkillData skill)
        {
            if (!RequiresClientShootAttackValidation(skill)
                || skill?.RequiredSkillIds == null
                || skill.RequiredSkillIds.Length == 0)
            {
                return Array.Empty<int>();
            }

            return skill.RequiredSkillIds
                .Where(requiredSkillId => requiredSkillId > 0)
                .Distinct()
                .ToArray();
        }

        private bool HasRequiredShootAttackState(SkillData skill)
        {
            return HasRequiredShootAttackState(skill, HasActiveBuff);
        }

        internal static bool HasRequiredShootAttackState(SkillData skill, Func<int, bool> hasActiveBuff)
        {
            int[] requiredSkillIds = ResolveClientShootAttackRequiredSkillIds(skill);
            if (requiredSkillIds.Length == 0)
            {
                return true;
            }

            if (hasActiveBuff == null)
            {
                return false;
            }

            for (int i = 0; i < requiredSkillIds.Length; i++)
            {
                if (!hasActiveBuff(requiredSkillIds[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private bool HasShootAmmoBypassTemporaryStat()
        {
            return _shootAmmoBypassTemporaryStatOverride
                ?? HasActiveTemporaryStatLabel(SoulArrowBuffLabel);
        }

        private bool TryConsumeShootAmmo(SkillData skill, SkillLevelData levelData)
        {
            if (!RequiresClientShootAttackValidation(skill))
            {
                LastResolvedShootAmmoSelection = null;
                return true;
            }

            int skillId = skill.SkillId;
            if (IsShootSkillNotUsingShootingWeapon(skillId)
                || IsShootSkillNotConsumingBullet(skillId)
                || HasShootAmmoBypassTemporaryStat())
            {
                LastResolvedShootAmmoSelection = null;
                return true;
            }

            if (_inventoryRuntime == null)
            {
                LastResolvedShootAmmoSelection = null;
                return true;
            }

            int weaponCode = GetEquippedWeaponCode();
            int requiredAmmoCount = ResolveRequiredShootAmmoCount(levelData);
            if (!TryResolveCompatibleShootAmmoSelection(skillId, levelData, weaponCode, requiredAmmoCount, out ShootAmmoSelection selection))
            {
                LastResolvedShootAmmoSelection = selection;
                return false;
            }

            LastResolvedShootAmmoSelection = selection;
            return _inventoryRuntime.TryConsumeItemAtSlot(InventoryType.USE, selection.UseSlotIndex, selection.UseItemId, requiredAmmoCount);
        }

        private bool HasCompatibleShootAmmo(int skillId, SkillLevelData levelData, int weaponCode, int requiredAmmoCount)
        {
            if (_inventoryRuntime == null)
            {
                LastResolvedShootAmmoSelection = null;
                return true;
            }

            bool resolved = TryResolveCompatibleShootAmmoSelection(skillId, levelData, weaponCode, requiredAmmoCount, out ShootAmmoSelection selection);
            LastResolvedShootAmmoSelection = selection;
            return resolved;
        }

        private bool TryResolveCompatibleShootAmmoSelection(
            int skillId,
            SkillLevelData levelData,
            int weaponCode,
            int requiredAmmoCount,
            out ShootAmmoSelection selection)
        {
            selection = null;

            if (_inventoryRuntime == null)
            {
                return true;
            }

            IReadOnlyList<InventorySlotData> useSlots = _inventoryRuntime.GetSlots(InventoryType.USE);
            if (useSlots == null || _player?.Build == null)
            {
                return false;
            }

            int equippedWeaponItemId = _player.Build.GetWeapon()?.ItemId ?? 0;
            int requiredAmmoItemId = ResolveSkillSpecificShootAmmoItemId(skillId, levelData);
            IReadOnlyList<InventorySlotData> cashSlots = _inventoryRuntime.GetSlots(InventoryType.CASH);

            return ClientShootAmmoResolver.TryResolveSelection(
                useSlots,
                cashSlots,
                weaponCode,
                equippedWeaponItemId,
                requiredAmmoCount,
                requiredAmmoItemId,
                out selection);
        }

        private static int ResolveRequiredShootAmmoCount(SkillLevelData levelData, bool hasShadowPartner = false)
        {
            int requiredAmmoCount = Math.Max(0, levelData?.BulletConsume ?? 0);
            if (requiredAmmoCount <= 0)
            {
                requiredAmmoCount = Math.Max(1, levelData?.BulletCount ?? 1);
            }

            if (hasShadowPartner)
            {
                requiredAmmoCount *= 2;
            }

            return Math.Max(1, requiredAmmoCount);
        }

        private int ResolveRequiredShootAmmoCount(SkillLevelData levelData)
        {
            return ResolveRequiredShootAmmoCount(levelData, HasActiveTemporaryStatLabel(ShadowPartnerBuffLabel));
        }

        private static int ResolveSkillSpecificShootAmmoItemId(int skillId, SkillLevelData levelData)
        {
            if (levelData?.ProjectileItemConsume > 0)
            {
                return levelData.ProjectileItemConsume;
            }

            return skillId switch
            {
                5211004 => 2331000,
                5211005 => 2331001,
                _ => 0
            };
        }

        private static bool IsValidShootingWeaponForSkill(int skillId, int weaponCode)
        {
            return ResolveShootWeaponFamily(skillId) switch
            {
                3 => weaponCode is 45 or 46,
                4 => weaponCode == 47,
                5 => weaponCode == 49,
                _ => true
            };
        }

        private static int ResolveShootWeaponFamily(int skillId)
        {
            int root = Math.Abs(skillId) / 10000;
            while (root >= 10)
            {
                root /= 10;
            }

            return root;
        }

        private static bool IsCompatibleShootAmmoItem(int weaponCode, int itemId)
        {
            int thousandFamily = itemId / 1000;
            int tenThousandFamily = itemId / 10000;

            return weaponCode switch
            {
                45 => thousandFamily == 2060,
                46 => thousandFamily == 2061,
                47 => tenThousandFamily == 207,
                49 => tenThousandFamily == 233,
                _ => false
            };
        }

        private SkillData ResolveClientCancelRestrictionSkill(int requestedSkillId, IReadOnlyList<int> cancelSkillIds)
        {
            if (_preparedSkill != null && DoesPreparedSkillMatchClientCancelRequest(_preparedSkill, requestedSkillId))
            {
                return _preparedSkill.SkillData;
            }

            if (requestedSkillId > 0)
            {
                SkillData requestedSkill = GetSkillData(requestedSkillId);
                if (requestedSkill != null)
                {
                    return requestedSkill;
                }
            }

            if (cancelSkillIds != null)
            {
                for (int i = 0; i < cancelSkillIds.Count; i++)
                {
                    SkillData cancelSkill = GetSkillData(cancelSkillIds[i]);
                    if (cancelSkill != null)
                    {
                        return cancelSkill;
                    }
                }
            }

            return null;
        }

        private bool RejectClientSkillCancellation(SkillData skill)
        {
            string restrictionMessage = _skillCancelRestrictionMessageProvider?.Invoke(skill);
            if (string.IsNullOrWhiteSpace(restrictionMessage))
            {
                return false;
            }

            if (skill != null)
            {
                OnFieldSkillCastRejected?.Invoke(skill, restrictionMessage);
            }

            return true;
        }

        private static string GetShootAmmoRestrictionMessage(int weaponCode)
        {
            return weaponCode switch
            {
                45 or 46 => "You do not have any arrows for this skill.",
                47 => "You do not have any throwing stars for this skill.",
                49 => "You do not have any bullets for this skill.",
                _ => "You do not have any compatible ammunition for this skill."
            };
        }

        private static bool IsShootSkillNotUsingShootingWeapon(int skillId)
        {
            return skillId is 11101004
                or 4121003
                or 4221003
                or 5121002
                or 15111006
                or 15111007
                or 21100004
                or 21110004
                or 21120006
                or 33101007;
        }

        private static bool IsShootSkillNotConsumingBullet(int skillId)
        {
            return IsShootSkillNotUsingShootingWeapon(skillId)
                || skillId is 3101003
                    or 3201003
                    or 4111004
                    or 14101006
                    or 33101002
                    or 35001001
                    or 35001004
                    or 35101009
                    or 35101010
                    or 35111004
                    or 35111015
                    or 35121005
                    or 35121012
                    or 35121013;
        }

        private static bool DefersExecutionResourceConsumption(SkillData skill)
        {
            return IsRocketBoosterSkill(skill);
        }

        private void TriggerSkillAnimation(SkillData skill, int currentTime, string actionNameOverride = null)
        {
            string actionName = ResolveSkillActionName(skill, actionNameOverride);

            _player.ApplySkillAvatarTransform(skill.SkillId, actionName, skill?.MorphId ?? 0);
            _player.TriggerSkillAnimation(actionName);
            UpdateMeleeAfterImageState(skill, actionName, currentTime);
        }

        private void UpdateMeleeAfterImageState(SkillData skill, string actionName, int currentTime)
        {
            if (skill == null)
            {
                _player.ClearMeleeAfterImage();
                return;
            }

            WeaponPart weapon = _player.Build?.GetWeapon();
            int masteryPercent = GetMastery(weapon);
            int chargeElement = ResolveActiveAfterImageChargeElement();
            if (_loader.TryResolveMeleeAfterImageAction(skill, weapon, actionName, _player.Level, masteryPercent, chargeElement, out MeleeAfterImageAction afterImageAction))
            {
                _player.ApplyMeleeAfterImage(skill.SkillId, actionName, afterImageAction, currentTime);
            }
            else
            {
                _player.ClearMeleeAfterImage();
            }
        }

        public void UpdateBasicMeleeAfterImageState(string actionName, int currentTime)
        {
            WeaponPart weapon = _player.Build?.GetWeapon();
            int masteryPercent = GetMastery(weapon);
            int chargeElement = ResolveActiveAfterImageChargeElement();
            if (_loader.TryResolveMeleeAfterImageAction(null, weapon, actionName, _player.Level, masteryPercent, chargeElement, out MeleeAfterImageAction afterImageAction))
            {
                _player.ApplyMeleeAfterImage(0, actionName, afterImageAction, currentTime);
            }
            else
            {
                _player.ClearMeleeAfterImage();
            }
        }

        private int ResolveActiveAfterImageChargeElement()
        {
            for (int i = _buffs.Count - 1; i >= 0; i--)
            {
                if (AfterImageChargeSkillResolver.TryGetChargeElement(_buffs[i]?.SkillId ?? 0, out int chargeElement))
                {
                    return chargeElement;
                }
            }

            return 0;
        }

        private static string ResolveSkillActionName(SkillData skill, string actionNameOverride = null)
        {
            if (!string.IsNullOrWhiteSpace(actionNameOverride))
                return actionNameOverride;

            if (!string.IsNullOrWhiteSpace(skill?.ActionName))
                return skill.ActionName;

            return skill?.AttackType switch
            {
                SkillAttackType.Ranged => "shoot1",
                SkillAttackType.Magic => "swingO1",
                _ => "attack1"
            };
        }

        internal static string ResolvePacketOwnedMeleeAttackActionName(
            SkillData skill,
            int? rawActionCode,
            string currentActionName,
            Func<string, bool> canRenderAction = null)
        {
            string fallbackCandidate = null;
            foreach (string candidate in EnumeratePacketOwnedMeleeAttackActionCandidates(skill, rawActionCode, currentActionName))
            {
                fallbackCandidate ??= candidate;
                if (canRenderAction == null || canRenderAction(candidate))
                {
                    return candidate;
                }
            }

            return fallbackCandidate;
        }

        internal static IEnumerable<string> EnumeratePacketOwnedMeleeAttackActionCandidates(
            SkillData skill,
            int? rawActionCode,
            string currentActionName)
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string candidate in EnumeratePacketOwnedMeleeAttackCandidateVariants(
                         rawActionCode.HasValue && CharacterPart.TryGetActionStringFromCode(rawActionCode.Value, out string rawActionName)
                             ? rawActionName
                             : null))
            {
                if (yielded.Add(candidate))
                {
                    yield return candidate;
                }
            }

            if (skill?.ActionNames != null)
            {
                foreach (string actionName in skill.ActionNames)
                {
                    foreach (string candidate in EnumeratePacketOwnedMeleeAttackCandidateVariants(actionName))
                    {
                        if (yielded.Add(candidate))
                        {
                            yield return candidate;
                        }
                    }
                }
            }

            foreach (string candidate in EnumeratePacketOwnedMeleeAttackCandidateVariants(skill?.ActionName))
            {
                if (yielded.Add(candidate))
                {
                    yield return candidate;
                }
            }

            foreach (string candidate in EnumeratePacketOwnedMeleeAttackCandidateVariants(currentActionName))
            {
                if (yielded.Add(candidate))
                {
                    yield return candidate;
                }
            }

            string resolvedDefaultActionName = ResolveSkillActionName(skill);
            foreach (string candidate in EnumeratePacketOwnedMeleeAttackCandidateVariants(resolvedDefaultActionName))
            {
                if (yielded.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }

        private static IEnumerable<string> EnumeratePacketOwnedMeleeAttackCandidateVariants(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                yield break;
            }

            string normalizedActionName = actionName.Trim();
            yield return normalizedActionName;

            foreach (string lookupActionName in CharacterPart.GetActionLookupStrings(normalizedActionName))
            {
                if (!string.Equals(lookupActionName, normalizedActionName, StringComparison.OrdinalIgnoreCase))
                {
                    yield return lookupActionName;
                }
            }

            foreach (string meleeFamilyAlias in EnumeratePacketOwnedMeleeAttackFamilyAliases(normalizedActionName))
            {
                yield return meleeFamilyAlias;
            }
        }

        internal static IEnumerable<string> EnumeratePacketOwnedMeleeAttackFamilyAliases(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                yield break;
            }

            if (string.Equals(actionName, "attack1", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string candidate in new[]
                {
                    "stabO1", "stabO2", "stabOF",
                    "stabT1", "stabT2", "stabTF",
                    "proneStab"
                })
                {
                    yield return candidate;
                }

                yield break;
            }

            if (string.Equals(actionName, "attack2", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string candidate in new[]
                {
                    "swingO1", "swingO2", "swingO3", "swingOF",
                    "swingT1", "swingT2", "swingT3", "swingTF",
                    "swingP1", "swingP2", "swingPF"
                })
                {
                    yield return candidate;
                }

                yield break;
            }

            if (actionName.StartsWith("stabO", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "stabD1", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string candidate in new[] { "stabO1", "stabO2", "stabOF", "stabD1" })
                {
                    yield return candidate;
                }

                yield break;
            }

            if (actionName.StartsWith("stabT", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string candidate in new[] { "stabT1", "stabT2", "stabTF" })
                {
                    yield return candidate;
                }

                yield break;
            }

            if (actionName.StartsWith("swingO", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "swingD1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "swingD2", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string candidate in new[] { "swingO1", "swingO2", "swingO3", "swingOF", "swingD1", "swingD2" })
                {
                    yield return candidate;
                }

                yield break;
            }

            if (actionName.StartsWith("swingT", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string candidate in new[] { "swingT1", "swingT2", "swingT3", "swingTF", "swingT2PoleArm" })
                {
                    yield return candidate;
                }

                yield break;
            }

            if (actionName.StartsWith("swingP", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "doubleSwing", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "tripleSwing", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string candidate in new[]
                {
                    "swingP1", "swingP2", "swingPF",
                    "swingP1PoleArm", "swingP2PoleArm",
                    "doubleSwing", "tripleSwing"
                })
                {
                    yield return candidate;
                }

                yield break;
            }

            if (actionName.StartsWith("shoot", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "shotC1", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string candidate in new[] { "shoot1", "shoot2", "shootF", "shotC1" })
                {
                    yield return candidate;
                }
            }
        }

        private void ApplySkillMount(SkillData skill, SkillLevelData levelData)
        {
            int mountItemId = ResolveSkillMountItemId(skill, levelData);
            if (_player.Build == null || _tamingMobLoader == null || mountItemId <= 0)
            {
                return;
            }

            CharacterPart mountPart = _tamingMobLoader(mountItemId);
            if (mountPart?.Slot != EquipSlot.TamingMob)
            {
                return;
            }

            _player.NotifyTamingMobOwnershipHandledExternally();

            if (_activeSkillMount != null && _activeSkillMount.MountItemId == mountItemId)
            {
                _player.Build.Equip(mountPart);
                _activeSkillMount = new SkillMountState
                {
                    SkillId = skill.SkillId,
                    MountItemId = mountItemId,
                    PreviousMount = _activeSkillMount.PreviousMount
                };
                return;
            }

            CharacterPart previousMount = _activeSkillMount?.PreviousMount;
            if (previousMount == null)
            {
                _player.Build.Equipment.TryGetValue(EquipSlot.TamingMob, out previousMount);
            }

            _player.Build.Equip(mountPart);
            _activeSkillMount = new SkillMountState
            {
                SkillId = skill.SkillId,
                MountItemId = mountItemId,
                PreviousMount = previousMount
            };
        }

        private int ResolveSkillMountItemId(SkillData skill, SkillLevelData levelData)
        {
            if (levelData?.ItemConNo > 0)
            {
                return levelData.ItemConNo;
            }

            if (ClientOwnedVehicleSkillClassifier.IsClientOwnedVehicleActionSkill(skill, levelData))
            {
                return BATTLESHIP_TAMING_MOB_ID;
            }

            if (TryResolveClientVehicleOwnershipMountItemId(skill, out int mountItemId))
            {
                return mountItemId;
            }

            return UsesMechanicVehicleMount(skill)
                ? MECHANIC_TAMING_MOB_ID
                : 0;
        }

        private bool TryResolveClientVehicleOwnershipMountItemId(SkillData skill, out int mountItemId)
        {
            mountItemId = 0;
            if (skill == null)
            {
                return false;
            }

            if (ClientOwnedVehicleSkillClassifier.IsClientOwnedVehicleActionSkill(skill))
            {
                mountItemId = BATTLESHIP_TAMING_MOB_ID;
                return true;
            }

            if (ClientOwnedVehicleSkillClassifier.IsClientOwnedVehicleStateSkill(skill))
            {
                mountItemId = MECHANIC_TAMING_MOB_ID;
                return true;
            }

            if (ClientOwnedVehicleSkillClassifier.UsesMechanicVehicleMountSkill(skill))
            {
                mountItemId = MECHANIC_TAMING_MOB_ID;
                return true;
            }

            if (skill.ClientInfoType != 13)
            {
                return false;
            }

            if (UsesMechanicVehicleOwnershipBuff(skill))
            {
                mountItemId = MECHANIC_TAMING_MOB_ID;
                return true;
            }

            // WZ only exposes Battleship as a vehicle-type buff, but Character/TamingMob/01932000
            // carries the ship-specific cannon and torpedo action families used by its board-only skills.
            if (skill.SkillId == 5221006)
            {
                mountItemId = BATTLESHIP_TAMING_MOB_ID;
                return true;
            }

            if (skill.SkillId == WildHunterJaguarRiderSkillId)
            {
                mountItemId = ResolveWildHunterJaguarMountItemId();
                return mountItemId > 0;
            }

            return false;
        }

        private bool TryResolveClientVehicleOwnershipMountPart(SkillData skill, int skillId, out CharacterPart mountPart)
        {
            mountPart = null;
            int resolvedSkillId = skill?.SkillId ?? skillId;
            SkillData resolvedSkill = skill ?? GetSkillData(resolvedSkillId);

            if (resolvedSkill != null && TryResolveClientVehicleOwnershipMountItemId(resolvedSkill, out int mountItemId))
            {
                mountPart = GetLoadedTamingMobPart(mountItemId);
                if (mountPart?.Slot == EquipSlot.TamingMob)
                {
                    return true;
                }
            }

            if (UsesEquippedClientOwnedVehicleMountFallback(resolvedSkill, resolvedSkillId))
            {
                mountPart = GetEquippedTamingMobPart();
                return mountPart?.Slot == EquipSlot.TamingMob;
            }

            if (resolvedSkill?.ClientInfoType == 13)
            {
                mountPart = GetEquippedTamingMobPart();
                return mountPart?.Slot == EquipSlot.TamingMob;
            }

            return false;
        }

        private CharacterPart GetLoadedTamingMobPart(int itemId)
        {
            return itemId > 0 ? _tamingMobLoader?.Invoke(itemId) : null;
        }

        private CharacterPart GetEquippedTamingMobPart()
        {
            return _player?.Build?.Equipment != null
                && _player.Build.Equipment.TryGetValue(EquipSlot.TamingMob, out CharacterPart mountPart)
                ? mountPart
                : null;
        }

        private int ResolveWildHunterJaguarMountItemId()
        {
            if (_activeSkillMount != null
                && IsWildHunterJaguarTamingMobItemId(_activeSkillMount.MountItemId))
            {
                return _activeSkillMount.MountItemId;
            }

            if (_player?.Build?.Equipment != null
                && _player.Build.Equipment.TryGetValue(EquipSlot.TamingMob, out CharacterPart equippedMount)
                && IsWildHunterJaguarTamingMobPart(equippedMount))
            {
                return equippedMount.ItemId;
            }

            foreach (int candidateItemId in WildHunterJaguarTamingMobCandidateIds)
            {
                CharacterPart candidateMount = _tamingMobLoader?.Invoke(candidateItemId);
                if (IsWildHunterJaguarTamingMobPart(candidateMount))
                {
                    return candidateItemId;
                }
            }

            return 0;
        }

        private static bool IsWildHunterJaguarTamingMobPart(CharacterPart mountPart)
        {
            return mountPart?.Slot == EquipSlot.TamingMob
                   && IsWildHunterJaguarTamingMobItemId(mountPart.ItemId);
        }

        private static bool IsWildHunterJaguarTamingMobItemId(int itemId)
        {
            foreach (int candidateItemId in WildHunterJaguarTamingMobCandidateIds)
            {
                if (candidateItemId == itemId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool UsesMechanicVehicleOwnershipBuff(SkillData skill)
        {
            if (skill == null || !IsMechanicSkill(skill.SkillId))
            {
                return false;
            }

            // Skill/3500.img/35001002 is the client-owned "Mech: Prototype" vehicle toggle.
            // It has vehicle-type metadata and a mount/unmount description, but no action branch,
            // so it still needs to claim the shared Mechanic TamingMob owner through the skill-mount seam.
            return skill.SkillId == 35001002
                   || (skill.ClientInfoType == 13
                       && ContainsAny(
                           $"{skill.Name} {skill.Description}",
                           "mount/unmount",
                           "summon and mount",
                           "prototype mech"));
        }

        private static bool UsesEquippedClientOwnedVehicleMountFallback(SkillData skill, int skillId)
        {
            return IsClientOwnedVehicleToggleSkill(skillId)
                   || UsesRideDescriptionOwnedVehicleBuff(skill);
        }

        private static bool UsesRideDescriptionOwnedVehicleBuff(SkillData skill)
        {
            // Some ride-granting buffs do not publish `info/type = 13`, but WZ still
            // exposes them as timed invisible ride buffs through their name/description surface.
            // Confirmed examples include `Skill/000.img/0001016`, `Skill/000.img/0001069`,
            // `Skill/1000.img/10001017`, and `Skill/8000.img/80001016`.
            return ClientOwnedVehicleSkillClassifier.LooksLikeClientOwnedRideDescriptionBuff(skill);
        }

        private bool UsesMechanicVehicleMount(SkillData skill)
        {
            if (skill == null || !IsMechanicSkill(skill.SkillId))
            {
                return false;
            }

            CharacterPart mechanicMount = _tamingMobLoader?.Invoke(MECHANIC_TAMING_MOB_ID);
            foreach (string actionName in EnumerateMechanicVehicleActionNames(skill))
            {
                if (MechanicMountSupportsSkillAction(mechanicMount, actionName)
                    || IsMechanicVehicleActionName(actionName))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MechanicMountSupportsSkillAction(CharacterPart mechanicMount, string actionName)
        {
            if (mechanicMount?.Slot != EquipSlot.TamingMob || string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            if (mechanicMount.TamingMobActionFrameOwner?.SupportsAction(mechanicMount, actionName) == true)
            {
                return true;
            }

            if (mechanicMount.GetAnimation(actionName) != null)
            {
                return true;
            }

            string tankVariant = $"tank_{actionName}";
            if (mechanicMount.GetAnimation(tankVariant) != null)
            {
                return true;
            }

            return string.Equals(actionName, "ride2", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "getoff2", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "ladder2", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "rope2", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "herbalism_mechanic", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "mining_mechanic", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMechanicSkill(int skillId)
        {
            int skillBookId = skillId / 10000;
            return skillBookId >= 3500 && skillBookId <= 3512;
        }

        private static IEnumerable<string> EnumerateMechanicVehicleActionNames(SkillData skill)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (skill?.ActionNames != null)
            {
                foreach (string actionName in skill.ActionNames)
                {
                    if (!string.IsNullOrWhiteSpace(actionName) && seen.Add(actionName))
                    {
                        yield return actionName;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(skill?.ActionName))
            {
                string actionName = skill.ActionName;
                if (seen.Add(actionName))
                {
                    yield return actionName;
                }
            }

            if (!string.IsNullOrWhiteSpace(skill?.PrepareActionName))
            {
                string actionName = skill.PrepareActionName;
                if (seen.Add(actionName))
                {
                    yield return actionName;
                }
            }

            if (!string.IsNullOrWhiteSpace(skill?.KeydownActionName))
            {
                string actionName = skill.KeydownActionName;
                if (seen.Add(actionName))
                {
                    yield return actionName;
                }
            }

            if (!string.IsNullOrWhiteSpace(skill?.KeydownEndActionName))
            {
                string actionName = skill.KeydownEndActionName;
                if (seen.Add(actionName))
                {
                    yield return actionName;
                }
            }
        }

        private static bool IsMechanicVehicleActionName(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            return actionName.StartsWith("tank_", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("siege_", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("flamethrower", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("rbooster", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("gatlingshot", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("drillrush", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("earthslug", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("rpunch", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("mbooster", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("msummon", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "mRush", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "ride2", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "getoff2", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "herbalism_mechanic", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "mining_mechanic", StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateClientOwnedVehicleTamingMobState(int currentTime)
        {
            if (_player == null)
            {
                return;
            }

            CharacterPart mountPart = null;
            bool isActive = false;

            for (int i = _buffs.Count - 1; i >= 0; i--)
            {
                SkillData activeVehicleSkill = _buffs[i]?.SkillData;
                if (!IsClientOwnedVehicleSkill(activeVehicleSkill))
                {
                    continue;
                }

                if (TryResolveClientVehicleOwnershipMountPart(activeVehicleSkill, activeVehicleSkill.SkillId, out mountPart))
                {
                    isActive = true;
                    break;
                }
            }

            if (!isActive
                && IsClientVehicleOwnershipGraceWindowActive(currentTime)
                && TryResolveClientVehicleOwnershipMountPart(null, _lastClientVehicleValidSkillId, out mountPart))
            {
                isActive = true;
            }

            if (!isActive)
            {
                string currentActionName = _player.CurrentActionName;
                int currentActionMountItemId = ResolveClientOwnedVehicleCurrentActionMountItemId(
                    currentActionName,
                    _activeSkillMount?.MountItemId ?? 0,
                    GetEquippedTamingMobPart()?.ItemId ?? 0);
                if (currentActionMountItemId > 0)
                {
                    mountPart = GetLoadedTamingMobPart(currentActionMountItemId);
                    isActive = SupportsClientOwnedVehicleMountedStateForCurrentAction(mountPart, currentActionName);
                }

                if (!isActive)
                {
                    // WZ still authors additional renderable resistance and battleship actions on
                    // their shared TamingMob assets beyond the explicit client-confirmed name lists.
                    // When a known owner is already active or equipped, preserve that owner through
                    // any directly renderable mounted action on the same asset instead of dropping
                    // back to the body solely because the action name was not enumerated yet.
                    mountPart = ResolveKnownClientOwnedVehicleCurrentActionMountPart(
                        currentActionName,
                        GetLoadedTamingMobPart(_activeSkillMount?.MountItemId ?? 0),
                        GetEquippedTamingMobPart());
                    isActive = mountPart?.Slot == EquipSlot.TamingMob;
                }
            }

            if (!isActive)
            {
                int transformMountItemId = ResolveClientOwnedVehicleAvatarTransformMountItemId(
                    _player.GetActiveAvatarTransformSkillId());
                if (transformMountItemId > 0)
                {
                    mountPart = GetLoadedTamingMobPart(transformMountItemId);
                    isActive = SupportsClientOwnedVehicleMountedStateForCurrentAction(mountPart, _player.CurrentActionName);
                }
            }

            if (!isActive)
            {
                _clientVehicleValidCount = 0;
                _clientVehicleValidStartTime = int.MinValue;
                _lastClientVehicleValidSkillId = 0;
            }

            _player.SetClientOwnedVehicleTamingMobState(mountPart, isActive);
        }

        private bool IsClientVehicleOwnershipGraceWindowActive(int currentTime)
        {
            return IsClientVehicleOwnershipGraceWindowActive(
                _clientVehicleValidCount,
                _clientVehicleValidStartTime,
                currentTime);
        }

        internal static bool IsClientVehicleOwnershipGraceWindowActive(
            int validCount,
            int validStartTime,
            int currentTime)
        {
            return validCount > 0
                   && currentTime >= validStartTime
                   && currentTime - validStartTime <= CLIENT_VEHICLE_OWNERSHIP_GRACE_WINDOW_MS;
        }

        internal static bool UsesClientOwnedVehicleTrackingSkill(SkillData skill)
        {
            return ClientOwnedVehicleSkillClassifier.IsWzAuthoredClientOwnedVehicleBuff(skill)
                   || ClientOwnedVehicleSkillClassifier.IsClientOwnedVehicleActionSkill(skill)
                   || ClientOwnedVehicleSkillClassifier.IsClientOwnedVehicleStateSkill(skill)
                   || ClientOwnedVehicleSkillClassifier.UsesMechanicVehicleMountSkill(skill)
                   || ClientOwnedVehicleSkillClassifier.IsClientOwnedVehicleValidSupportSkill(skill)
                   || UsesEquippedClientOwnedVehicleMountFallback(skill, skill?.SkillId ?? 0);
        }

        private static bool IsClientOwnedVehicleSkill(SkillData skill)
        {
            return UsesClientOwnedVehicleTrackingSkill(skill);
        }

        private static bool IsClientOwnedVehicleToggleSkill(int skillId)
        {
            return skillId == 1004
                   || skillId == 10001004
                   || skillId == 20001004
                   || skillId == 20011004
                   || skillId == 30001004;
        }

        internal static int ResolveClientOwnedVehicleCurrentActionMountItemId(
            string actionName,
            int activeSkillMountItemId,
            int equippedMountItemId)
        {
            int activeOwnerMountItemId = NormalizeClientOwnedVehicleCurrentActionMountItemId(activeSkillMountItemId);
            int equippedOwnerMountItemId = NormalizeClientOwnedVehicleCurrentActionMountItemId(equippedMountItemId);

            if (string.IsNullOrWhiteSpace(actionName))
            {
                return activeOwnerMountItemId > 0
                    ? activeOwnerMountItemId
                    : equippedOwnerMountItemId;
            }

            if (activeOwnerMountItemId > 0
                && IsClientOwnedVehicleCurrentActionOwnedByMount(actionName, activeOwnerMountItemId))
            {
                return activeOwnerMountItemId;
            }

            if (equippedOwnerMountItemId > 0
                && IsClientOwnedVehicleCurrentActionOwnedByMount(actionName, equippedOwnerMountItemId))
            {
                return equippedOwnerMountItemId;
            }

            if (ClientOwnedVehicleSkillClassifier.IsOwnerlessMechanicVehicleInferenceActionName(
                actionName,
                includeTransformStates: true))
            {
                return MECHANIC_TAMING_MOB_ID;
            }

            return ClientOwnedVehicleSkillClassifier.IsBattleshipMountedActionName(actionName)
                ? BATTLESHIP_TAMING_MOB_ID
                : 0;
        }

        private static int NormalizeClientOwnedVehicleCurrentActionMountItemId(int mountItemId)
        {
            return mountItemId == BATTLESHIP_TAMING_MOB_ID || mountItemId == MECHANIC_TAMING_MOB_ID
                ? mountItemId
                : 0;
        }

        internal static int ResolveClientOwnedVehicleAvatarTransformMountItemId(int activeAvatarTransformSkillId)
        {
            return IsMechanicAvatarTransformMountOwnerSkillId(activeAvatarTransformSkillId)
                ? MECHANIC_TAMING_MOB_ID
                : 0;
        }

        private static bool IsMechanicAvatarTransformMountOwnerSkillId(int skillId)
        {
            if (ClientOwnedVehicleSkillClassifier.IsMechanicVehicleTransformSkillId(skillId))
            {
                return true;
            }

            // These skills currently reach the same built-in avatar-transform seam through their
            // WZ-authored Mechanic action names, so keep the shared 1932016 owner latched while the
            // transform is active instead of dropping back to the base body mid-cast.
            return skillId is 35101005
                or 35111002
                or 35111005
                or 35111011
                or 35121012;
        }

        internal static bool SupportsClientOwnedVehicleMountedStateForCurrentAction(
            CharacterPart mountPart,
            string actionName)
        {
            if (mountPart?.Slot != EquipSlot.TamingMob)
            {
                return false;
            }

            if (CharacterAssembler.SupportsTamingMobAction(mountPart, actionName))
            {
                return true;
            }

            if (mountPart.ItemId == MECHANIC_TAMING_MOB_ID
                && ClientOwnedVehicleSkillClassifier.IsMechanicVehicleOwnedCurrentActionName(
                    actionName,
                    includeTransformStates: true))
            {
                return true;
            }

            // Client-owned vehicle ownership is established before this seam. Preserve the
            // canonical mount owner through blank current-action ticks instead of dropping the
            // mounted state solely because no visible action name was published for that frame.
            return string.IsNullOrWhiteSpace(actionName)
                   && NormalizeClientOwnedVehicleCurrentActionMountItemId(mountPart.ItemId) > 0;
        }

        private static bool IsClientOwnedVehicleCurrentActionOwnedByMount(string actionName, int mountItemId)
        {
            return mountItemId switch
            {
                BATTLESHIP_TAMING_MOB_ID => ClientOwnedVehicleSkillClassifier.IsBattleshipVehicleOwnedCurrentActionName(
                    actionName,
                    includeSupportActions: true),
                MECHANIC_TAMING_MOB_ID => ClientOwnedVehicleSkillClassifier.IsMechanicVehicleOwnedCurrentActionName(
                    actionName,
                    includeTransformStates: true),
                _ => false
            };
        }

        private static CharacterPart ResolveKnownClientOwnedVehicleCurrentActionMountPart(
            string actionName,
            CharacterPart activeOwnerMountPart,
            CharacterPart equippedOwnerMountPart)
        {
            if (SupportsKnownClientOwnedVehicleCurrentAction(activeOwnerMountPart, actionName))
            {
                return activeOwnerMountPart;
            }

            return SupportsKnownClientOwnedVehicleCurrentAction(equippedOwnerMountPart, actionName)
                ? equippedOwnerMountPart
                : null;
        }

        private static bool SupportsKnownClientOwnedVehicleCurrentAction(CharacterPart mountPart, string actionName)
        {
            return mountPart?.Slot == EquipSlot.TamingMob
                   && NormalizeClientOwnedVehicleCurrentActionMountItemId(mountPart.ItemId) > 0
                   && CharacterAssembler.SupportsTamingMobAction(mountPart, actionName);
        }

        private void TransferSkillMountOwnership(int previousSkillId, int nextSkillId)
        {
            if (_activeSkillMount == null || _activeSkillMount.SkillId != previousSkillId)
            {
                return;
            }

            _activeSkillMount = new SkillMountState
            {
                SkillId = nextSkillId,
                MountItemId = _activeSkillMount.MountItemId,
                PreviousMount = _activeSkillMount.PreviousMount
            };
        }

        private void ClearSkillMount()
        {
            if (_activeSkillMount == null || _player.Build == null)
            {
                return;
            }

            _player.NotifyTamingMobOwnershipHandledExternally();

            if (_activeSkillMount.PreviousMount != null)
            {
                _player.Build.Equip(_activeSkillMount.PreviousMount);
            }
            else
            {
                _player.Build.Unequip(EquipSlot.TamingMob);
            }

            _activeSkillMount = null;
        }

        private void ClearSkillMount(int skillId)
        {
            if (_activeSkillMount != null && _activeSkillMount.SkillId == skillId)
            {
                ClearSkillMount();
            }
        }

        private void PlayCastSound(SkillData skill)
        {
            if (skill == null || _soundManager == null)
                return;

            string soundKey = _loader.EnsureCastSoundRegistered(skill, _soundManager);
            if (!string.IsNullOrEmpty(soundKey))
            {
                _soundManager.PlaySound(soundKey);
            }
        }

        private void PlayRepeatSound(SkillData skill)
        {
            if (skill == null || _soundManager == null)
                return;

            string soundKey = _loader.EnsureRepeatSoundRegistered(skill, _soundManager);
            if (!string.IsNullOrEmpty(soundKey))
            {
                _soundManager.PlaySound(soundKey);
            }
        }

        private static bool IsRepeatSkillSustainFamily(int skillId)
        {
            return skillId == RepeatSkillSiegeId
                   || skillId == RepeatSkillTankModeId
                   || skillId == RepeatSkillTankSiegeId
                   || skillId == SG88_SKILL_ID;
        }

        private static int ResolveRepeatSkillTimeoutEffectRequestSkillId(int skillId)
        {
            return skillId switch
            {
                RepeatSkillSiegeId => 35110004,
                RepeatSkillTankSiegeId => 35120013,
                _ => 0
            };
        }

        private bool CanToggleRepeatSkillManualAssist(int skillId)
        {
            return skillId == SG88_SKILL_ID
                   && _currentCast?.IsComplete != false
                   && _preparedSkill == null
                   && FindActiveSummon(skillId) != null;
        }

        private bool TryToggleRepeatSkillManualAssist(int skillId, int currentTime)
        {
            if (!CanToggleRepeatSkillManualAssist(skillId))
            {
                return false;
            }

            ActiveSummon summon = FindActiveSummon(skillId);
            if (summon == null)
            {
                return false;
            }

            bool wasManualAssistEnabled = summon.ManualAssistEnabled;
            summon.ManualAssistEnabled = !summon.ManualAssistEnabled;
            summon.LastAttackTime = currentTime;
            summon.LastAttackAnimationStartTime = int.MinValue;
            if (_activeRepeatSkillSustain?.SkillId == skillId)
            {
                _activeRepeatSkillSustain.LastAttackStartTime = currentTime;
                if (wasManualAssistEnabled)
                {
                    _activeRepeatSkillSustain.ShowHudBar = false;
                    _activeRepeatSkillSustain.Sg88AssistArmed = true;
                    _activeRepeatSkillSustain.IsDone = true;
                    _activeRepeatSkillSustain.BranchPoint = 2;
                }
                else
                {
                    summon.ManualAssistEnabled = false;
                    _activeRepeatSkillSustain.StartTime = currentTime;
                    _activeRepeatSkillSustain.ShowHudBar = false;
                    _activeRepeatSkillSustain.HudBarStartTime = currentTime + SG88_KEYDOWN_BAR_START_DELAY_MS;
                    _activeRepeatSkillSustain.Sg88AssistArmed = false;
                    _activeRepeatSkillSustain.IsDone = false;
                    _activeRepeatSkillSustain.BranchPoint = 0;
                }
            }
            return true;
        }

        private bool CancelPreparedSkill(int requestedSkillId, int currentTime)
        {
            if (_preparedSkill == null || !DoesPreparedSkillMatchClientCancelRequest(_preparedSkill, requestedSkillId))
            {
                return false;
            }

            RecordPreparedSkillExclusiveRequest(currentTime);
            PreparedSkill prepared = _preparedSkill;
            int preparedSkillId = _preparedSkill.SkillId;
            _preparedSkill = null;
            CancelClientSkillTimers(preparedSkillId, ClientTimerSourcePreparedRelease);
            _player.EndSustainedSkillAnimation();
            _player.ClearSkillAvatarTransform(preparedSkillId);
            ClearSkillMount(preparedSkillId);
            _currentCast = null;
            OnPreparedSkillReleased?.Invoke(prepared);
            return true;
        }

        private void ClearPreparedSkillForForcedLocalRelease(int currentTime)
        {
            if (_preparedSkill == null)
            {
                return;
            }

            RecordPreparedSkillExclusiveRequest(currentTime);
            int preparedSkillId = _preparedSkill.SkillId;
            CancelClientSkillTimers(preparedSkillId, ClientTimerSourcePreparedRelease);
            _player.EndSustainedSkillAnimation();
            _player.ClearSkillAvatarTransform(preparedSkillId);
            ClearSkillMount(preparedSkillId);
            _preparedSkill = null;
            _currentCast = null;
        }

        private bool ApplyClientSkillCancelSideEffects(IEnumerable<int> skillIds, int currentTime)
        {
            if (skillIds == null)
            {
                return false;
            }

            bool vehicleCancelRecorded = false;
            bool keydownCleanupApplied = false;

            foreach (int skillId in skillIds)
            {
                if (skillId <= 0)
                {
                    continue;
                }

                if (!vehicleCancelRecorded)
                {
                    vehicleCancelRecorded = RecordVehicleValidCancel(skillId, currentTime);
                }

                if (!keydownCleanupApplied)
                {
                    keydownCleanupApplied = CleanupKeydownPrepareAnimationOnCancel(skillId);
                }

                if (vehicleCancelRecorded && keydownCleanupApplied)
                {
                    break;
                }
            }

            return vehicleCancelRecorded || keydownCleanupApplied;
        }

        private bool RecordVehicleValidCancel(int skillId, int currentTime)
        {
            SkillData skill = GetSkillData(skillId);
            int trackingSkillId = ResolveClientOwnedVehicleTrackingSkillId(
                skillId,
                skill,
                HasActiveClientOwnedVehicleTrackingOwner);
            if (trackingSkillId <= 0)
            {
                return false;
            }

            if (_clientVehicleValidCount <= 0)
            {
                _clientVehicleValidStartTime = currentTime;
            }

            _lastClientVehicleValidSkillId = trackingSkillId;
            _clientVehicleValidCount++;
            return true;
        }

        internal static int ResolveClientOwnedVehicleTrackingSkillId(
            int skillId,
            SkillData skill,
            Func<int, bool> isVehicleOwnerActive)
        {
            if (ClientOwnedVehicleSkillClassifier.IsClientOwnedVehicleValidSupportSkill(skill)
                && isVehicleOwnerActive?.Invoke(BATTLESHIP_SKILL_ID) == true)
            {
                return BATTLESHIP_SKILL_ID;
            }

            if (skill != null && UsesClientOwnedVehicleTrackingSkill(skill))
            {
                return skill.SkillId;
            }

            if (skillId > 0 && isVehicleOwnerActive?.Invoke(skillId) == true)
            {
                return skillId;
            }

            if (IsClientOwnedVehicleToggleSkill(skillId))
            {
                return skillId;
            }

            return 0;
        }

        private bool HasActiveClientOwnedVehicleTrackingOwner(int ownerSkillId)
        {
            if (ownerSkillId <= 0)
            {
                return false;
            }

            if (_activeSkillMount?.SkillId == ownerSkillId)
            {
                return true;
            }

            for (int i = _buffs.Count - 1; i >= 0; i--)
            {
                SkillData activeBuffSkill = _buffs[i]?.SkillData;
                if (activeBuffSkill?.SkillId == ownerSkillId
                    && IsClientOwnedVehicleSkill(activeBuffSkill))
                {
                    return true;
                }
            }

            return false;
        }

        private bool CleanupKeydownPrepareAnimationOnCancel(int skillId)
        {
            SkillData skill = GetSkillData(skillId);
            if (skill?.IsKeydownSkill != true || PreservesPrepareAnimationOnClientCancel(skillId))
            {
                return false;
            }

            bool clearedCurrentCast = false;
            _player.EndSustainedSkillAnimation();

            if (_currentCast != null
                && DoesClientCancelMatchSkillId(_currentCast.SkillId, skillId))
            {
                _currentCast = null;
                clearedCurrentCast = true;
            }

            return clearedCurrentCast || DoesPreparedSkillMatchClientCancelRequest(_preparedSkill, skillId);
        }

        private bool CancelClientOwnedSkillState(int requestedSkillId, int currentTime)
        {
            bool stateChanged = false;
            stateChanged |= CancelPreparedSkill(requestedSkillId, currentTime);
            stateChanged |= CancelActiveBuffByClientRequest(requestedSkillId, currentTime);
            stateChanged |= CancelRepeatSkillSustainByClientRequest(requestedSkillId, currentTime);
            stateChanged |= CancelCycloneByClientRequest(requestedSkillId);
            stateChanged |= CancelActiveSummonsByClientRequest(requestedSkillId);
            stateChanged |= CancelSkillZonesByClientRequest(requestedSkillId);

            if (_swallowState != null && DoesSwallowSkillRequestMatchState(requestedSkillId))
            {
                ClearSwallowState();
                stateChanged = true;
            }

            return stateChanged;
        }

        private bool CancelActiveBuffByClientRequest(int requestedSkillId, int currentTime)
        {
            bool removedBuff = false;
            for (int i = _buffs.Count - 1; i >= 0; i--)
            {
                if (!DoesClientCancelMatchSkillId(_buffs[i].SkillId, requestedSkillId))
                {
                    continue;
                }

                RemoveBuffAt(i, currentTime, playFinish: true);
                removedBuff = true;
            }

            return removedBuff;
        }

        private bool CancelRepeatSkillSustainByClientRequest(int requestedSkillId, int currentTime)
        {
            if (_activeRepeatSkillSustain == null || !DoesClientCancelMatchSkillId(_activeRepeatSkillSustain.SkillId, requestedSkillId))
            {
                return false;
            }

            RepeatSkillSustainState sustain = _activeRepeatSkillSustain;
            CancelClientSkillTimers(sustain.SkillId, ClientTimerSourceRepeatSustainEnd);

            if (sustain.SkillId == RepeatSkillTankSiegeId && sustain.ReturnSkillId > 0)
            {
                BeginPendingTankSiegeModeEndRequest(sustain, currentTime, emitTimeoutEffect: false);
                return true;
            }

            _activeRepeatSkillSustain = null;

            ActiveSummon summon = FindActiveSummon(sustain.SkillId);
            if (summon != null && sustain.SkillId == SG88_SKILL_ID)
            {
                RemoveSummon(summon, cancelTimer: true);
            }

            CompleteRepeatSkillSustainTeardown(sustain, currentTime, emitTimeoutEffect: false);
            return true;
        }

        private void CompleteRepeatSkillSustainTeardown(RepeatSkillSustainState sustain, int currentTime, bool emitTimeoutEffect)
        {
            if (sustain == null || !_player.HasSkillAvatarTransform(sustain.SkillId))
            {
                return;
            }

            if (emitTimeoutEffect)
            {
                int timeoutEffectSkillId = ResolveRepeatSkillTimeoutEffectRequestSkillId(sustain.SkillId);
                if (timeoutEffectSkillId > 0)
                {
                    OnClientSkillEffectRequested?.Invoke(timeoutEffectSkillId, sustain.SkillId);
                    PlayCastSound(GetSkillData(sustain.SkillId));
                }
            }

            _player.ClearSkillAvatarTransform(sustain.SkillId);
            TransferSkillMountOwnership(sustain.SkillId, sustain.ReturnSkillId);

            if (sustain.ReturnSkillId > 0 && _player.IsAlive)
            {
                _player.ApplySkillAvatarTransform(sustain.ReturnSkillId, actionName: null);
                _player.ForceStand();
                SkillData returnSkill = GetSkillData(sustain.ReturnSkillId);
                BeginRepeatSkillSustain(returnSkill, returnSkill?.GetLevel(GetSkillLevel(sustain.ReturnSkillId)), currentTime, 0);
            }
        }

        private bool CancelCycloneByClientRequest(int requestedSkillId)
        {
            if (_cycloneState == null || !DoesClientCancelMatchSkillId(_cycloneState.Skill?.SkillId ?? 0, requestedSkillId))
            {
                return false;
            }

            StopCyclone();
            return true;
        }

        private bool CancelSkillZonesByClientRequest(int requestedSkillId)
        {
            int removedCount = 0;
            for (int i = _skillZones.Count - 1; i >= 0; i--)
            {
                ActiveSkillZone zone = _skillZones[i];
                if (!DoesClientCancelMatchSkillId(zone?.SkillId ?? 0, requestedSkillId))
                {
                    continue;
                }

                RemoveSkillZoneAt(i, cancelTimer: true);
                removedCount++;
            }

            return removedCount > 0;
        }

        private bool CancelActiveSummonsByClientRequest(int requestedSkillId)
        {
            int removedCount = 0;
            for (int i = _summons.Count - 1; i >= 0; i--)
            {
                ActiveSummon summon = _summons[i];
                if (!DoesClientCancelMatchSkillId(summon?.SkillId ?? 0, requestedSkillId))
                {
                    continue;
                }

                if (ShouldPreservePendingRemovalSummonOnClientCancel(summon))
                {
                    removedCount++;
                    continue;
                }

                RemoveSummonAt(i, cancelTimer: true);
                removedCount++;
            }

            return removedCount > 0;
        }

        internal static bool ShouldPreservePendingRemovalSummonOnClientCancel(ActiveSummon summon)
        {
            return summon?.IsPendingRemoval == true && summon.ExpiryActionTriggered;
        }

        private bool DoesPreparedSkillMatchClientCancelRequest(PreparedSkill prepared, int requestedSkillId)
        {
            if (prepared == null)
            {
                return false;
            }

            if (requestedSkillId <= 0)
            {
                return true;
            }

            return DoesClientCancelMatchSkillId(prepared.SkillId, requestedSkillId);
        }

        private bool DoesClientCancelMatchSkillId(int activeSkillId, int requestedSkillId)
        {
            return ClientSkillCancelResolver.DoesClientCancelMatchSkillId(
                activeSkillId,
                requestedSkillId,
                GetSkillData,
                _availableSkills);
        }

        private int[] ResolveClientCancelRequestSkillIds(int skillId)
        {
            if (skillId <= 0)
            {
                return Array.Empty<int>();
            }

            HashSet<int> resolvedSkillIds = new()
            {
                skillId
            };

            foreach (int resolvedSkillId in ClientSkillCancelResolver.ResolveCancelRequestSkillIds(
                         skillId,
                         GetSkillData,
                         _availableSkills))
            {
                if (resolvedSkillId > 0)
                {
                    resolvedSkillIds.Add(resolvedSkillId);
                }
            }

            return resolvedSkillIds.ToArray();
        }

        internal IReadOnlyList<int> ResolveClientCancelRequestSkillIdsForParity(int skillId)
        {
            return ResolveClientCancelRequestSkillIds(skillId);
        }

        private static bool PreservesPrepareAnimationOnClientCancel(int skillId)
        {
            return skillId == 3121004
                   || skillId == 5221004
                   || skillId == 13111002
                   || skillId == 33121009
                   || skillId == 35001001
                   || skillId == 35101009;
        }

        private ActiveSummon FindActiveSummon(int skillId)
        {
            return _summons.FirstOrDefault(summon => summon?.SkillId == skillId);
        }

        private int ResolveRepeatSkillReturnSkillId(int skillId)
        {
            return skillId == RepeatSkillTankSiegeId && _player.HasSkillAvatarTransform(RepeatSkillTankModeId)
                ? RepeatSkillTankModeId
                : 0;
        }

        private void BeginRepeatSkillSustain(SkillData skill, SkillLevelData levelData, int currentTime, int returnSkillId)
        {
            if (!IsRepeatSkillSustainFamily(skill?.SkillId ?? 0) || levelData == null)
            {
                ClearRepeatSkillSustain();
                return;
            }

            int durationMs = levelData.Time > 0 ? levelData.Time * 1000 : 0;
            bool usesTimer = skill.SkillId == 35111004 || skill.SkillId == 35121013;
            if (usesTimer && durationMs <= 0)
            {
                ClearRepeatSkillSustain();
                return;
            }

            _activeRepeatSkillSustain = new RepeatSkillSustainState
            {
                SkillId = skill.SkillId,
                ReturnSkillId = returnSkillId,
                StartTime = currentTime,
                RestrictToNormalAttack = skill.OnlyNormalAttackInState,
                HudGaugeDurationMs = skill.SkillId == SG88_SKILL_ID ? 2000 : 0,
                HudSkinKey = skill.SkillId == SG88_SKILL_ID ? "KeyDownBar4" : "KeyDownBar",
                HudBarStartTime = currentTime + SG88_KEYDOWN_BAR_START_DELAY_MS,
                Sg88AssistArmed = skill.SkillId != SG88_SKILL_ID,
                IsDone = skill.SkillId != SG88_SKILL_ID,
                LastAttackStartTime = currentTime
            };

            if (usesTimer && durationMs > 0)
            {
                RegisterClientSkillTimer(
                    skill.SkillId,
                    ClientTimerSourceRepeatSustainEnd,
                    currentTime + durationMs,
                    ExpireRepeatSkillSustain);
            }
        }

        private void ClearRepeatSkillSustain()
        {
            if (_activeRepeatSkillSustain == null)
                return;

            CancelClientSkillTimers(_activeRepeatSkillSustain.SkillId, ClientTimerSourceRepeatSustainEnd);
            CancelClientSkillTimers(_activeRepeatSkillSustain.SkillId, ClientTimerSourceRepeatModeEndAck);
            _activeRepeatSkillSustain = null;
        }

        private void ExpireRepeatSkillSustain(int currentTime)
        {
            if (_activeRepeatSkillSustain == null)
                return;

            RepeatSkillSustainState sustain = _activeRepeatSkillSustain;

            if (!_player.HasSkillAvatarTransform(sustain.SkillId))
            {
                _activeRepeatSkillSustain = null;
                return;
            }

            if (sustain.SkillId == RepeatSkillTankSiegeId && sustain.ReturnSkillId > 0)
            {
                BeginPendingTankSiegeModeEndRequest(sustain, currentTime, emitTimeoutEffect: true);
                return;
            }

            _activeRepeatSkillSustain = null;
            CompleteRepeatSkillSustainTeardown(sustain, currentTime, emitTimeoutEffect: true);
        }

        private bool IsSkillCastBlockedByRepeatSkillSustain()
        {
            if (_activeRepeatSkillSustain == null || !_activeRepeatSkillSustain.RestrictToNormalAttack)
            {
                return false;
            }

            return _player.HasSkillAvatarTransform(_activeRepeatSkillSustain.SkillId);
        }

        private void UpdateRepeatSkillSustain(int currentTime)
        {
            if (_activeRepeatSkillSustain == null)
            {
                return;
            }

            if (_activeRepeatSkillSustain.SkillId == RepeatSkillTankSiegeId)
            {
                UpdateTankSiegeRepeatSkillSustain(currentTime);
                return;
            }

            if (_activeRepeatSkillSustain.SkillId != SG88_SKILL_ID)
            {
                return;
            }

            ActiveSummon summon = FindActiveSummon(SG88_SKILL_ID);
            if (summon == null)
            {
                ClearRepeatSkillSustain();
                return;
            }

            int elapsed = currentTime - _activeRepeatSkillSustain.StartTime;
            if (!_activeRepeatSkillSustain.IsDone)
            {
                if (!_activeRepeatSkillSustain.ShowHudBar && elapsed >= SG88_KEYDOWN_BAR_START_DELAY_MS)
                {
                    _activeRepeatSkillSustain.ShowHudBar = true;
                    _activeRepeatSkillSustain.BranchPoint = 1;
                }

                if (elapsed >= SG88_ASSIST_ARM_DELAY_MS)
                {
                    summon.ManualAssistEnabled = true;
                    summon.LastAttackTime = currentTime;
                    summon.LastAttackAnimationStartTime = int.MinValue;
                    _activeRepeatSkillSustain.Sg88AssistArmed = true;
                    _activeRepeatSkillSustain.IsDone = true;
                    _activeRepeatSkillSustain.ShowHudBar = false;
                    _activeRepeatSkillSustain.BranchPoint = 2;
                    _activeRepeatSkillSustain.LastAttackStartTime = currentTime;
                }

                return;
            }

            if (summon.ManualAssistEnabled || currentTime - _activeRepeatSkillSustain.LastAttackStartTime < SG88_ASSIST_REMOVE_DELAY_MS)
            {
                return;
            }

            RemoveSummon(summon, cancelTimer: true);
            int returnSkillId = _player.HasSkillAvatarTransform(RepeatSkillTankModeId) ? RepeatSkillTankModeId : 0;
            ClearRepeatSkillSustain();

            if (returnSkillId > 0)
            {
                SkillData returnSkill = GetSkillData(returnSkillId);
                BeginRepeatSkillSustain(returnSkill, returnSkill?.GetLevel(GetSkillLevel(returnSkillId)), currentTime, 0);
            }
        }

        private void UpdateTankSiegeRepeatSkillSustain(int currentTime)
        {
            if (_activeRepeatSkillSustain == null || _activeRepeatSkillSustain.SkillId != RepeatSkillTankSiegeId)
            {
                return;
            }

            if (!_activeRepeatSkillSustain.PendingModeEndRequest)
            {
                return;
            }

            if (TryResolvePendingTankSiegeModeEndFromObservedReturnMode(currentTime))
            {
                return;
            }

            if (!_player.HasSkillAvatarTransform(RepeatSkillTankSiegeId))
            {
                CancelClientSkillTimers(RepeatSkillTankSiegeId, ClientTimerSourceRepeatModeEndAck);
                _activeRepeatSkillSustain = null;
            }
        }

        private bool TryResolvePendingTankSiegeModeEndFromObservedReturnMode(int currentTime)
        {
            if (_activeRepeatSkillSustain == null
                || _activeRepeatSkillSustain.SkillId != RepeatSkillTankSiegeId
                || !_activeRepeatSkillSustain.PendingModeEndRequest)
            {
                return false;
            }

            int returnSkillId = _activeRepeatSkillSustain.ReturnSkillId;
            if (returnSkillId <= 0 || !_player.HasSkillAvatarTransform(returnSkillId))
            {
                return false;
            }

            CancelClientSkillTimers(RepeatSkillTankSiegeId, ClientTimerSourceRepeatModeEndAck);
            SkillData returnSkill = GetSkillData(returnSkillId);
            SkillLevelData returnLevelData = returnSkill?.GetLevel(GetSkillLevel(returnSkillId));
            _activeRepeatSkillSustain = null;
            BeginRepeatSkillSustain(returnSkill, returnLevelData, currentTime, 0);
            return true;
        }

        private void BeginPendingTankSiegeModeEndRequest(RepeatSkillSustainState sustain, int currentTime, bool emitTimeoutEffect)
        {
            if (sustain == null || sustain.SkillId != RepeatSkillTankSiegeId || sustain.ReturnSkillId <= 0)
            {
                return;
            }

            if (sustain.PendingModeEndRequest)
            {
                return;
            }

            if (emitTimeoutEffect)
            {
                int timeoutEffectSkillId = ResolveRepeatSkillTimeoutEffectRequestSkillId(sustain.SkillId);
                if (timeoutEffectSkillId > 0)
                {
                    OnClientSkillEffectRequested?.Invoke(timeoutEffectSkillId, sustain.SkillId);
                    PlayCastSound(GetSkillData(sustain.SkillId));
                }
            }

            CancelClientSkillTimers(sustain.SkillId, ClientTimerSourceRepeatModeEndAck);
            sustain.PendingModeEndRequest = true;
            sustain.PendingModeEndRequestTime = currentTime;
            sustain.IsDone = true;
            sustain.BranchPoint = 2;
            OnRepeatSkillModeEndRequested?.Invoke(sustain.SkillId, sustain.ReturnSkillId, currentTime);

            if (OnRepeatSkillModeEndRequested == null)
            {
                RegisterClientSkillTimer(
                    sustain.SkillId,
                    ClientTimerSourceRepeatModeEndAck,
                    currentTime + 1,
                    ExpireRepeatSkillModeEndAck);
            }
        }

        private void ExpireRepeatSkillModeEndAck(int currentTime)
        {
            CompletePendingTankSiegeModeEndRequest(currentTime);
        }

        public bool TryAcknowledgeRepeatSkillModeEndRequest(int skillId, int currentTime, int requestedAt = int.MinValue)
        {
            if (_activeRepeatSkillSustain == null
                || _activeRepeatSkillSustain.SkillId != RepeatSkillTankSiegeId
                || !_activeRepeatSkillSustain.PendingModeEndRequest)
            {
                return false;
            }

            if (requestedAt != int.MinValue
                && _activeRepeatSkillSustain.PendingModeEndRequestTime != requestedAt)
            {
                return false;
            }

            if (skillId > 0
                && skillId != _activeRepeatSkillSustain.SkillId
                && skillId != _activeRepeatSkillSustain.ReturnSkillId)
            {
                return false;
            }

            RegisterClientSkillTimer(
                RepeatSkillTankSiegeId,
                ClientTimerSourceRepeatModeEndAck,
                currentTime,
                ExpireRepeatSkillModeEndAck);
            return true;
        }

        public bool HasPendingRepeatSkillModeEndRequest(int skillId, int returnSkillId, int requestedAt = int.MinValue)
        {
            return _activeRepeatSkillSustain != null
                   && _activeRepeatSkillSustain.SkillId == RepeatSkillTankSiegeId
                   && _activeRepeatSkillSustain.PendingModeEndRequest
                   && (requestedAt == int.MinValue || _activeRepeatSkillSustain.PendingModeEndRequestTime == requestedAt)
                   && _activeRepeatSkillSustain.SkillId == skillId
                   && _activeRepeatSkillSustain.ReturnSkillId == returnSkillId;
        }

        public int GetPendingRepeatSkillModeEndFallbackDelayMs(int skillId, int returnSkillId, int requestedAt = int.MinValue)
        {
            if (!HasPendingRepeatSkillModeEndRequest(skillId, returnSkillId, requestedAt))
            {
                return 0;
            }

            return GetSkillAnimationDuration(GetSkillData(skillId)?.Effect) ?? 0;
        }

        private void CompletePendingTankSiegeModeEndRequest(int currentTime)
        {
            if (_activeRepeatSkillSustain == null
                || _activeRepeatSkillSustain.SkillId != RepeatSkillTankSiegeId
                || !_activeRepeatSkillSustain.PendingModeEndRequest)
            {
                return;
            }

            CancelClientSkillTimers(RepeatSkillTankSiegeId, ClientTimerSourceRepeatModeEndAck);

            if (TryResolvePendingTankSiegeModeEndFromObservedReturnMode(currentTime))
            {
                return;
            }

            if (!_player.HasSkillAvatarTransform(RepeatSkillTankSiegeId))
            {
                _activeRepeatSkillSustain = null;
                return;
            }

            int returnSkillId = _activeRepeatSkillSustain.ReturnSkillId;
            _player.ClearSkillAvatarTransform(RepeatSkillTankSiegeId);
            TransferSkillMountOwnership(RepeatSkillTankSiegeId, returnSkillId);

            if (returnSkillId <= 0 || !_player.IsAlive)
            {
                _activeRepeatSkillSustain = null;
                return;
            }

            _player.ApplySkillAvatarTransform(returnSkillId, actionName: null);
            _player.ForceStand();
            SkillData returnSkill = GetSkillData(returnSkillId);
            BeginRepeatSkillSustain(returnSkill, returnSkill?.GetLevel(GetSkillLevel(returnSkillId)), currentTime, 0);
        }

        private int ResolveRepeatSkillNormalAttackSkillId()
        {
            if (_activeRepeatSkillSustain?.SkillId == RepeatSkillTankSiegeId
                && _activeRepeatSkillSustain.PendingModeEndRequest)
            {
                return 0;
            }

            if (_player.HasSkillAvatarTransform(RepeatSkillTankSiegeId))
                return RepeatSkillTankSiegeId;

            if (_player.HasSkillAvatarTransform(RepeatSkillSiegeId))
                return RepeatSkillSiegeId;

            if (_player.HasSkillAvatarTransform(RepeatSkillTankModeId))
                return RepeatSkillTankModeId;

            return 0;
        }

        private static int GetRepeatSkillNormalAttackDelayMs(int skillId)
        {
            return skillId switch
            {
                RepeatSkillTankModeId => TANK_REPEAT_ATTACK_DELAY_MS,
                RepeatSkillSiegeId => SIEGE_REPEAT_ATTACK_DELAY_MS,
                RepeatSkillTankSiegeId => SIEGE_REPEAT_ATTACK_DELAY_MS,
                _ => 0
            };
        }

        private static string ResolveRepeatSkillNormalAttackActionName(int skillId)
        {
            return skillId switch
            {
                RepeatSkillTankModeId => "tank",
                RepeatSkillSiegeId => "siege",
                RepeatSkillTankSiegeId => "tank_siegeattack",
                _ => "attack1"
            };
        }

        private bool TryExecuteRepeatSkillNormalAttack(int currentTime)
        {
            int skillId = ResolveRepeatSkillNormalAttackSkillId();
            if (skillId <= 0)
            {
                return false;
            }

            SkillData skill = GetSkillData(skillId);
            int level = GetSkillLevel(skillId);
            SkillLevelData levelData = skill?.GetLevel(level);
            if (skill == null || levelData == null || _currentCast?.IsComplete == false || _preparedSkill != null)
            {
                return false;
            }

            if (!CanAffordSkillCost(levelData))
            {
                return false;
            }

            if (_activeRepeatSkillSustain != null && _activeRepeatSkillSustain.SkillId == skillId)
            {
                int attackDelayMs = GetRepeatSkillNormalAttackDelayMs(skillId);
                if (attackDelayMs > 0 && currentTime - _activeRepeatSkillSustain.LastAttackStartTime < attackDelayMs)
                {
                    return false;
                }

                _activeRepeatSkillSustain.LastAttackStartTime = currentTime;
            }

            if (!TryConsumeSkillResources(skill, levelData))
            {
                return false;
            }

            _player.TriggerSkillAnimation(ResolveRepeatSkillNormalAttackActionName(skillId));
            ExecuteSkillPayload(skill, level, currentTime, queueFollowUps: false, allowDeferredExecution: false);
            return true;
        }

        private PreparedSkill BuildRepeatSkillHudSnapshot()
        {
            if (_activeRepeatSkillSustain == null
                || !_activeRepeatSkillSustain.ShowHudBar
                || _activeRepeatSkillSustain.HudGaugeDurationMs <= 0)
            {
                return null;
            }

            SkillData skill = GetSkillData(_activeRepeatSkillSustain.SkillId);
            return new PreparedSkill
            {
                SkillId = _activeRepeatSkillSustain.SkillId,
                SkillData = skill,
                Level = GetSkillLevel(_activeRepeatSkillSustain.SkillId),
                StartTime = _activeRepeatSkillSustain.HudBarStartTime,
                Duration = _activeRepeatSkillSustain.HudGaugeDurationMs,
                ShowHudBar = true,
                HudGaugeDurationMs = _activeRepeatSkillSustain.HudGaugeDurationMs,
                HudSkinKey = _activeRepeatSkillSustain.HudSkinKey,
                HudTextVariant = ResolvePreparedSkillHudTextVariant(skill),
                IsKeydownSkill = false
            };
        }

        private void ExecuteSkillPayload(
            SkillData skill,
            int level,
            int currentTime,
            bool queueFollowUps = true,
            int? preferredTargetMobId = null,
            Vector2? preferredTargetPositionOverride = null,
            bool allowDeferredExecution = true,
            bool revalidateDeferredExecutionSkillLevel = false,
            bool isQueuedFinalAttack = false,
            bool isQueuedSparkAttack = false,
            bool? facingRightOverride = null,
            Vector2? attackOriginOverride = null,
            int shootRange0Override = 0,
            float? damageScaleOverride = null,
            string currentActionNameOverride = null,
            int? currentRawActionCodeOverride = null)
        {
            bool facingRight = facingRightOverride ?? _player.FacingRight;
            float? previousDamageScaleOverride = _activeSkillDamageScaleOverride;
            bool appliedDamageScaleOverride = damageScaleOverride.HasValue;
            if (appliedDamageScaleOverride)
            {
                _activeSkillDamageScaleOverride = damageScaleOverride.Value;
            }

            try
            {
                if (allowDeferredExecution
                    && TryScheduleDeferredSkillPayload(
                        skill,
                        level,
                        currentTime,
                        queueFollowUps,
                        preferredTargetMobId,
                        preferredTargetPositionOverride,
                        facingRight,
                        revalidateDeferredExecutionSkillLevel,
                        isQueuedFinalAttack,
                        isQueuedSparkAttack,
                        attackOriginOverride,
                        shootRange0Override))
                {
                    return;
                }

                if (TryExecuteClientSkillBranch(skill, level, currentTime))
                {
                    return;
                }

                if (ShouldExecuteMovementBranch(skill))
                {
                    ExecuteMovementSkill(skill, level, currentTime);
                }

                if (skill.IsBuff)
                {
                    ApplyBuff(skill, level, currentTime);
                }

                if (skill.IsHeal)
                {
                    ApplyHeal(skill, level);
                }

                if (skill.IsSummon)
                {
                    SpawnSummon(skill, level, currentTime);
                }

                ExecuteAttackPayload(
                    skill,
                    level,
                    currentTime,
                    queueFollowUps,
                    preferredTargetMobId,
                    preferredTargetPositionOverride,
                    allowDeferredExecution: false,
                    revalidateDeferredExecutionSkillLevel: false,
                    isQueuedFinalAttack: isQueuedFinalAttack,
                    facingRightOverride: facingRight,
                    attackOriginOverride: attackOriginOverride,
                    shootRange0Override: shootRange0Override,
                    currentActionNameOverride: currentActionNameOverride,
                    currentRawActionCodeOverride: currentRawActionCodeOverride);
            }
            finally
            {
                if (appliedDamageScaleOverride)
                {
                    _activeSkillDamageScaleOverride = previousDamageScaleOverride;
                }
            }
        }

        private void ExecuteAttackPayload(
            SkillData skill,
            int level,
            int currentTime,
            bool queueFollowUps = true,
            int? preferredTargetMobId = null,
            Vector2? preferredTargetPositionOverride = null,
            bool allowDeferredExecution = true,
            bool revalidateDeferredExecutionSkillLevel = false,
            bool isQueuedFinalAttack = false,
            bool isQueuedSparkAttack = false,
            bool? facingRightOverride = null,
            Vector2? attackOriginOverride = null,
            int shootRange0Override = 0,
            string currentActionNameOverride = null,
            int? currentRawActionCodeOverride = null)
        {
            bool facingRight = facingRightOverride ?? _player.FacingRight;
            bool forceCriticalForAttack = ConsumePacketOwnedExJablinIfApplicable(skill);

            if (allowDeferredExecution
                && TryScheduleDeferredSkillPayload(
                    skill,
                    level,
                    currentTime,
                    queueFollowUps,
                    preferredTargetMobId,
                    preferredTargetPositionOverride,
                    facingRight,
                    revalidateDeferredExecutionSkillLevel,
                    false,
                    isQueuedSparkAttack,
                    attackOriginOverride,
                    shootRange0Override))
            {
                return;
            }

            if (!skill.IsAttack)
                return;

            if (TryExecuteMesoExplosionAttack(skill, level, currentTime, facingRight))
                return;

            if (skill.Projectile != null)
            {
                SpawnProjectile(
                    skill,
                    level,
                    currentTime,
                    facingRight,
                    queueFollowUps,
                    preferredTargetMobId,
                    attackOriginOverride,
                    preferredTargetPositionOverride,
                    isQueuedFinalAttack,
                    isQueuedSparkAttack,
                    forceCriticalForAttack);
                return;
            }

            if (skill.AttackType == SkillAttackType.Magic)
            {
                ProcessMagicAttack(skill, level, currentTime, facingRight, queueFollowUps, preferredTargetMobId, preferredTargetPositionOverride, attackOriginOverride, currentActionNameOverride, currentRawActionCodeOverride);
                return;
            }

            if (skill.AttackType == SkillAttackType.Ranged)
            {
                ProcessNoProjectileRangedAttack(skill, level, currentTime, facingRight, queueFollowUps, preferredTargetMobId, preferredTargetPositionOverride, attackOriginOverride, shootRange0Override, forceCriticalForAttack, currentActionNameOverride, currentRawActionCodeOverride);
                return;
            }

            ProcessMeleeAttack(skill, level, currentTime, facingRight, queueFollowUps, preferredTargetMobId, preferredTargetPositionOverride, attackOriginOverride, currentActionNameOverride, currentRawActionCodeOverride);
        }

        private bool TryExecuteClientSkillBranch(SkillData skill, int level, int currentTime)
        {
            if (skill == null)
            {
                return false;
            }

            if (IsAttackTriggeredChainBuffSkill(skill, level))
            {
                ApplyBuff(skill, level, currentTime);
                return true;
            }

            if (IsInvincibleZoneSkill(skill))
            {
                return TryExecuteInvincibleZoneBranch(skill, level, currentTime);
            }

            if (IsSwallowSkill(skill))
            {
                return TryExecuteSwallowBranch(skill, level, currentTime);
            }

            if (skill.SkillId == CYCLONE_SKILL_ID)
            {
                StartCyclone(skill, level, currentTime);
                return true;
            }

            return false;
        }

        private bool TryExecuteInvincibleZoneBranch(SkillData skill, int level, int currentTime)
        {
            SkillLevelData levelData = skill?.GetLevel(level);
            if (skill == null || levelData == null)
            {
                return true;
            }

            Rectangle worldBounds = GetWorldAttackHitbox(skill, level, levelData, AttackResolutionMode.Melee, _player.FacingRight);
            if (worldBounds.Width <= 0 || worldBounds.Height <= 0)
            {
                return true;
            }

            StartSkillZone(skill, level, levelData, currentTime, worldBounds);
            return true;
        }

        private bool TryExecuteSwallowBranch(SkillData skill, int level, int currentTime)
        {
            SkillLevelData levelData = skill?.GetLevel(level);
            if (skill == null || levelData == null)
            {
                return true;
            }

            if (skill.SkillId == WildHunterSwallowSkillId)
            {
                return TryExecuteWildHunterSwallow(skill, level, levelData, currentTime);
            }

            if (skill.SkillId == WildHunterSwallowBuffSkillId)
            {
                return TryExecuteWildHunterSwallowDigest(skill, currentTime);
            }

            if (skill.SkillId == WildHunterSwallowAttackSkillId)
            {
                return TryExecuteWildHunterSwallowAttack(skill, level, levelData, currentTime);
            }

            Rectangle worldHitbox = GetWorldAttackHitbox(skill, level, levelData, AttackResolutionMode.Melee, _player.FacingRight);
            if (worldHitbox.Width <= 0 || worldHitbox.Height <= 0)
            {
                return true;
            }

            MobItem target = ResolveTargetsInHitbox(
                    worldHitbox,
                    currentTime,
                    maxTargets: 1,
                    AttackResolutionMode.Melee,
                    _player.FacingRight,
                    preferredTargetMobId: null)
                .FirstOrDefault();

            if (target == null)
            {
                ClearSwallowState();
                return true;
            }

            SpawnHitEffect(skill, target.MovementInfo?.X ?? _player.X, (target.MovementInfo?.Y ?? _player.Y) - 20f, currentTime);
            HandleMobDeath(target, currentTime, MobDeathType.Swallowed);

            if (ShouldApplySwallowBuff(skill, levelData))
            {
                ApplyBuff(skill, level, currentTime);
                ActiveBuff activeBuff = _buffs.LastOrDefault(buff => buff.SkillId == skill.SkillId);
                _swallowState = activeBuff != null
                    ? new SwallowState
                    {
                        SkillId = skill.SkillId
                    }
                    : null;
            }
            else
            {
                ClearSwallowState();
            }

            return true;
        }

        private bool TryExecuteWildHunterSwallow(SkillData skill, int level, SkillLevelData levelData, int currentTime)
        {
            if (HasConfirmedWildHunterSwallow())
            {
                return true;
            }

            if (HasPendingWildHunterSwallowAbsorb())
            {
                return true;
            }

            Rectangle worldHitbox = GetWorldAttackHitbox(skill, level, levelData, AttackResolutionMode.Melee, _player.FacingRight);
            if (worldHitbox.Width <= 0 || worldHitbox.Height <= 0)
            {
                return true;
            }

            MobItem target = ResolveTargetsInHitbox(
                    worldHitbox,
                    currentTime,
                    maxTargets: 1,
                    AttackResolutionMode.Melee,
                    _player.FacingRight,
                    preferredTargetMobId: null)
                .FirstOrDefault();

            if (target == null)
            {
                ClearSwallowState();
                return true;
            }

            _swallowState = new SwallowState
            {
                SkillId = skill.SkillId,
                ParentSkillId = skill.SkillId,
                Level = level,
                TargetMobId = target.PoolId,
                PendingAbsorbOutcome = true,
                PendingAbsorbExpireTime = currentTime + WildHunterSwallowParity.ResolveAbsorbOutcomeTimeoutMs(
                    GetActionAnimationDurationMs(skill),
                    GetSkillAnimationDuration(skill.Effect) ?? 0)
            };

            if (_swallowAbsorbOutcomeBuffer.TryConsume(DoesSwallowSkillRequestMatchState, target.PoolId, currentTime, out bool bufferedSuccess))
            {
                CompleteWildHunterSwallowAbsorb(bufferedSuccess, currentTime);
                return true;
            }

            OnSwallowAbsorbRequested?.Invoke(new SwallowAbsorbRequest(skill.SkillId, target.PoolId, level, currentTime));
            if (OnSwallowAbsorbRequested == null)
            {
                CompleteWildHunterSwallowAbsorb(success: true, currentTime);
            }

            return true;
        }

        private bool TryExecuteWildHunterSwallowDigest(SkillData skill, int currentTime)
        {
            if (!HasConfirmedWildHunterSwallow())
            {
                return true;
            }

            MobItem target = GetSwallowTarget();
            if (target?.AI == null || target.AI.IsDead)
            {
                RequestClientSkillCancel(_swallowState?.ParentSkillId ?? WildHunterSwallowSkillId, currentTime);
                return true;
            }

            if (_swallowState.IsDigesting)
            {
                return true;
            }

            target.AI.ApplyStatusEffect(
                MobStatusEffect.Stun,
                WildHunterSwallowParity.GetSuspensionDurationMs(),
                currentTime);
            SpawnHitEffect(
                skill ?? GetSkillData(_swallowState.ParentSkillId),
                target.MovementInfo?.X ?? _player.X,
                (target.MovementInfo?.Y ?? _player.Y) - 20f,
                currentTime);
            ActivateWildHunterSwallowDigestState(currentTime);
            return true;
        }

        private bool TryExecuteWildHunterSwallowAttack(SkillData skill, int level, SkillLevelData levelData, int currentTime)
        {
            if (!HasConfirmedWildHunterSwallow())
            {
                return true;
            }

            MobItem target = GetSwallowTarget();
            if (target == null)
            {
                RequestClientSkillCancel(_swallowState?.ParentSkillId ?? WildHunterSwallowSkillId, currentTime);
                return true;
            }

            SpawnHitEffect(skill, target.MovementInfo?.X ?? _player.X, (target.MovementInfo?.Y ?? _player.Y) - 20f, currentTime);
            HandleMobDeath(target, currentTime, MobDeathType.Swallowed);
            ClearSwallowState();
            ProcessNoProjectileRangedAttack(skill, level, currentTime, _player.FacingRight, queueFollowUps: false);
            return true;
        }

        private static bool ShouldApplySwallowBuff(SkillData skill, SkillLevelData levelData)
        {
            return levelData?.Time > 0
                   && (skill?.IsBuff == true
                       || skill?.Type == SkillType.Buff
                       || skill?.Type == SkillType.PartyBuff);
        }

        private bool TryExecuteMesoExplosionAttack(SkillData skill, int level, int currentTime, bool facingRight)
        {
            if (skill?.IsMesoExplosion != true || _dropPool == null)
                return false;

            SkillLevelData levelData = skill.GetLevel(level);
            if (levelData == null)
                return true;

            Rectangle worldHitbox = GetWorldAttackHitbox(skill, level, levelData, AttackResolutionMode.Melee, facingRight);
            if (worldHitbox.Width <= 0 || worldHitbox.Height <= 0)
                return true;

            List<DropItem> explosiveDrops = _dropPool.GetExplosiveDropInRect(
                worldHitbox.Left + (worldHitbox.Width * 0.5f),
                worldHitbox.Top + (worldHitbox.Height * 0.5f),
                worldHitbox.Width,
                worldHitbox.Height,
                playerId: _player.Build?.Id ?? 0,
                currentTime,
                maxCount: Math.Max(1, levelData.AttackCount),
                enforceOwnership: false);

            if (explosiveDrops.Count == 0)
                return true;

            _dropPool.ConsumeMesosForExplosion(explosiveDrops, currentTime);

            foreach (DropItem explosiveDrop in explosiveDrops)
            {
                SpawnHitEffect(skill, explosiveDrop.X, explosiveDrop.Y, currentTime, facingRight);
            }

            if (_mobPool == null)
                return true;

            List<MobItem> targets = ResolveTargetsInHitbox(
                worldHitbox,
                currentTime,
                Math.Max(1, levelData.MobCount),
                AttackResolutionMode.Melee,
                facingRight,
                null);

            if (targets.Count == 0)
                return true;

            int attackCount = Math.Min(Math.Max(1, explosiveDrops.Count), Math.Max(1, levelData.AttackCount));
            var mobsToKill = new List<MobItem>();

            for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
            {
                MobItem mob = targets[targetIndex];
                bool died = ApplySkillAttackToMob(
                    skill,
                    level,
                    levelData,
                    mob,
                    currentTime,
                    attackCount,
                    AttackResolutionMode.Melee,
                    skill.HitEffect,
                    facingRight,
                    targetIndex);

                if (died && !mobsToKill.Contains(mob))
                {
                    mobsToKill.Add(mob);
                }
            }

            foreach (MobItem mob in mobsToKill)
            {
                HandleMobDeath(mob, currentTime);
            }

            return true;
        }

        private bool TryScheduleDeferredSkillPayload(
            SkillData skill,
            int level,
            int currentTime,
            bool queueFollowUps,
            int? preferredTargetMobId,
            Vector2? preferredTargetPositionOverride,
            bool facingRight,
            bool revalidateSkillLevelOnExecute,
            bool isQueuedFinalAttack,
            bool isQueuedSparkAttack,
            Vector2? attackOriginOverride = null,
            int shootRange0Override = 0)
        {
            if (skill == null)
            {
                return false;
            }

            if (IsRocketBoosterSkill(skill))
            {
                string rocketBoosterStartupActionName = ResolveRocketBoosterStartupActionName();
                float rocketBoosterLaunchSpeed = ResolveRocketBoosterLaunchVerticalSpeed(skill, level);
                CancelRocketBoosterState(playExitAction: false);
                _deferredSkillPayloads.Enqueue(new DeferredSkillPayload
                {
                    Skill = skill,
                    Level = level,
                    ExecuteTime = currentTime + GetRocketBoosterLaunchDelayMs(skill, rocketBoosterStartupActionName),
                    // `CUserLocal::TryDoingRocketBooster` re-reads the live learned level at
                    // launch time after the startup action completes instead of trusting the
                    // keypress snapshot that armed the deferred branch.
                    RevalidateSkillLevelOnExecute = true,
                    QueueFollowUps = false,
                    PreferredTargetMobId = preferredTargetMobId,
                    PreferredTargetPosition = preferredTargetPositionOverride,
                    FacingRight = facingRight,
                    ActionName = rocketBoosterStartupActionName,
                    StoredVerticalLaunchSpeed = rocketBoosterLaunchSpeed,
                    IsQueuedFinalAttack = isQueuedFinalAttack,
                    IsQueuedSparkAttack = isQueuedSparkAttack,
                    ShootRange0 = shootRange0Override,
                    ShootAmmoBypassActive = HasShootAmmoBypassTemporaryStat()
                });
                return true;
            }

            if (!ShouldUseSmoothingMovingShoot(skill))
            {
                return false;
            }

            Vector2 attackOrigin = attackOriginOverride ?? ResolveDeferredMovingShootOrigin(currentTime, facingRight);
            int? currentRawActionCode = _player?.TryGetCurrentClientRawActionCode(out int rawActionCode) == true
                ? rawActionCode
                : null;
            int queuedAttackActionType = ResolveQueuedMovingShootAttackActionType(
                skill,
                _player?.CurrentActionName,
                currentRawActionCode);
            (string queuedActionName, int? queuedRawActionCode) = ResolveQueuedMovingShootEntryAction(
                skill,
                _player?.CurrentActionName,
                currentRawActionCode,
                queuedAttackActionType,
                _player?.Build?.GetWeapon()?.WeaponType);
            if (UsesClientRandomShootAttackActionTable(skill)
                && string.IsNullOrWhiteSpace(queuedActionName)
                && !queuedRawActionCode.HasValue)
            {
                // `TryDoingSmoothingMovingShootAttackPrepare` aborts before arming
                // `m_movingShootEntry` when `get_random_shoot_attack_action(...)`
                // cannot resolve a valid shared-table owner.
                return false;
            }
            preferredTargetMobId ??= ResolveDeferredPreferredTargetMobId(
                skill,
                level,
                currentTime,
                facingRight,
                attackOrigin,
                shootRange0Override,
                queuedActionName,
                queuedRawActionCode);
            Vector2? preferredTargetPosition = preferredTargetPositionOverride
                ?? ResolveDeferredPreferredTargetPosition(preferredTargetMobId, currentTime);
            int equippedWeaponCode = GetEquippedWeaponCode();
            int queuedActionSpeed = ResolveQueuedMovingShootActionSpeed(skill);

            _deferredSkillPayloads.Enqueue(new DeferredSkillPayload
            {
                Skill = skill,
                Level = level,
                ExecuteTime = currentTime + GetMovingShootDelayMs(skill),
                RevalidateSkillLevelOnExecute = revalidateSkillLevelOnExecute,
                QueueFollowUps = queueFollowUps,
                PreferredTargetMobId = preferredTargetMobId,
                PreferredTargetPosition = preferredTargetPosition,
                FacingRight = facingRight,
                AttackOrigin = attackOrigin,
                // `m_movingShootEntry` keeps the queue-time attack action metadata instead of
                // re-reading a later avatar state once the delayed shot finally executes.
                ActionName = queuedActionName,
                RawActionCode = queuedRawActionCode,
                AttackActionType = queuedAttackActionType,
                ActionSpeed = queuedActionSpeed,
                IsQueuedFinalAttack = isQueuedFinalAttack,
                IsQueuedSparkAttack = isQueuedSparkAttack,
                ShootRange0 = shootRange0Override,
                // `m_movingShootEntry` keeps the queued bullet slot metadata even while a
                // no-consume buff such as Soul Arrow is active; the consume bypass itself
                // rides alongside that snapshot as separate entry state.
                ResolvedShootAmmoSelection = ResolveQueuedShootAmmoSelectionSnapshot(
                    skill,
                    skill.GetLevel(level),
                    equippedWeaponCode,
                    ignoreAmmoBypassTemporaryStat: true) ?? LastResolvedShootAmmoSelection?.Snapshot(),
                ShootAmmoBypassActive = HasShootAmmoBypassTemporaryStat()
            });
            return true;
        }

        internal static Vector2 ResolveCurrentShootAttackOrigin(
            Vector2 playerPosition,
            bool playerFacingRight,
            bool desiredFacingRight,
            Point? handMovePoint,
            Point? handPoint,
            Point? bodyOrigin)
        {
            Point? shootPoint = handMovePoint ?? handPoint ?? bodyOrigin;
            if (shootPoint.HasValue)
            {
                if (playerFacingRight != desiredFacingRight)
                {
                    float mirroredOffsetX = playerPosition.X - shootPoint.Value.X;
                    return new Vector2(playerPosition.X + mirroredOffsetX, shootPoint.Value.Y);
                }

                return new Vector2(shootPoint.Value.X, shootPoint.Value.Y);
            }

            return playerPosition;
        }

        internal static bool ResolveQueuedProjectileLaunchFacing(ActiveProjectile projectile)
        {
            if (projectile == null)
            {
                return false;
            }

            return projectile.QueuedFacingRight ?? projectile.FacingRight;
        }

        internal static bool ResolveQueuedProjectilePresentationFacing(ActiveProjectile projectile)
        {
            if (projectile == null)
            {
                return false;
            }

            return (projectile.IsQueuedFinalAttack || projectile.IsQueuedSparkAttack)
                ? projectile.QueuedFacingRight ?? projectile.FacingRight
                : projectile.FacingRight;
        }

        internal static bool ResolveProjectileFrameShouldFlip(ActiveProjectile projectile, SkillFrame frame)
        {
            return ResolveQueuedProjectilePresentationFacing(projectile) ^ (frame?.Flip ?? false);
        }

        internal static Vector2 ResolveProjectileAttackOrigin(
            Vector2 playerPosition,
            bool playerFacingRight,
            bool desiredFacingRight,
            Point? handMovePoint,
            Point? handPoint,
            Point? bodyOrigin,
            Vector2? attackOriginOverride,
            out bool usesAuthoredShootPoint)
        {
            if (attackOriginOverride.HasValue)
            {
                usesAuthoredShootPoint = true;
                return attackOriginOverride.Value;
            }

            usesAuthoredShootPoint = handMovePoint.HasValue || handPoint.HasValue || bodyOrigin.HasValue;
            return ResolveCurrentShootAttackOrigin(
                playerPosition,
                playerFacingRight,
                desiredFacingRight,
                handMovePoint,
                handPoint,
                bodyOrigin);
        }

        private Vector2 ResolveCurrentShootAttackOrigin(int currentTime, bool facingRight)
        {
            if (_player == null)
            {
                return Vector2.Zero;
            }

            return ResolveCurrentShootAttackOrigin(
                new Vector2(_player.X, _player.Y),
                _player.FacingRight,
                facingRight,
                _player.TryGetCurrentBodyMapPoint("handMove", currentTime),
                _player.TryGetCurrentBodyMapPoint("hand", currentTime),
                _player.TryGetCurrentBodyOrigin(currentTime));
        }

        private Vector2 ResolveProjectileAttackOrigin(int currentTime, bool facingRight, Vector2? attackOriginOverride, out bool usesAuthoredShootPoint)
        {
            if (_player == null)
            {
                usesAuthoredShootPoint = attackOriginOverride.HasValue;
                return attackOriginOverride ?? Vector2.Zero;
            }

            return ResolveProjectileAttackOrigin(
                new Vector2(_player.X, _player.Y),
                _player.FacingRight,
                facingRight,
                _player.TryGetCurrentBodyMapPoint("handMove", currentTime),
                _player.TryGetCurrentBodyMapPoint("hand", currentTime),
                _player.TryGetCurrentBodyOrigin(currentTime),
                attackOriginOverride,
                out usesAuthoredShootPoint);
        }

        internal static float ResolveProjectileSpawnY(
            float attackOriginY,
            bool usesAuthoredShootPoint,
            float fallbackShootPointYOffset)
        {
            return usesAuthoredShootPoint
                ? attackOriginY
                : attackOriginY + fallbackShootPointYOffset;
        }

        private float ResolveClientFallbackShootAttackPointYOffset(SkillData skill)
        {
            return ClientShootAttackFamilyResolver.ResolveFallbackShootAttackPointYOffset(
                skill?.SkillId ?? 0,
                _player?.Build?.Job ?? skill?.Job ?? 0,
                ResolveCurrentClientShootAttackVehicleId());
        }

        private int ResolveCurrentClientShootAttackVehicleId()
        {
            return ResolveClientOwnedVehicleCurrentActionMountItemId(
                _player?.CurrentActionName,
                _activeSkillMount?.MountItemId ?? 0,
                GetEquippedTamingMobPart()?.ItemId ?? 0);
        }

        private Vector2 ResolveDeferredMovingShootOrigin(int currentTime, bool facingRight)
        {
            return ResolveCurrentShootAttackOrigin(currentTime, facingRight);
        }

        private int? ResolveDeferredPreferredTargetMobId(
            SkillData skill,
            int level,
            int currentTime,
            bool facingRight,
            Vector2? attackOriginOverride = null,
            int shootRange0Override = 0,
            string currentActionNameOverride = null,
            int? currentRawActionCodeOverride = null)
        {
            if (_mobPool == null || skill?.IsAttack != true)
            {
                return null;
            }

            SkillLevelData levelData = skill.GetLevel(level);
            if (levelData == null)
            {
                return null;
            }

            AttackResolutionMode mode = ResolveDeferredTargetingMode(skill);
            int? preferredTargetMobId = ResolveDeferredPreferredTargetMobIdForMode(
                skill,
                level,
                levelData,
                currentTime,
                facingRight,
                attackOriginOverride,
                mode,
                shootRange0Override,
                currentActionNameOverride,
                currentRawActionCodeOverride);
            if (preferredTargetMobId.HasValue)
            {
                return preferredTargetMobId;
            }

            if (mode == AttackResolutionMode.Melee && ShouldTryMeleeBeforeRangedFallback(skill))
            {
                return ResolveDeferredPreferredTargetMobIdForMode(
                    skill,
                    level,
                    levelData,
                    currentTime,
                    facingRight,
                    attackOriginOverride,
                    AttackResolutionMode.Ranged,
                    shootRange0Override,
                    currentActionNameOverride,
                    currentRawActionCodeOverride);
            }

            return null;
        }

        private int? ResolveDeferredPreferredTargetMobIdForMode(
            SkillData skill,
            int level,
            SkillLevelData levelData,
            int currentTime,
            bool facingRight,
            Vector2? attackOriginOverride,
            AttackResolutionMode mode,
            int shootRange0Override = 0,
            string currentActionNameOverride = null,
            int? currentRawActionCodeOverride = null)
        {
            Rectangle worldHitbox = GetWorldAttackHitbox(
                skill,
                level,
                levelData,
                mode,
                facingRight,
                attackOriginOverride,
                shootRange0Override,
                currentActionNameOverride,
                currentRawActionCodeOverride);
            if (worldHitbox.Width <= 0 || worldHitbox.Height <= 0)
            {
                return null;
            }

            return ResolveTargetsInHitbox(worldHitbox, currentTime, 1, mode, facingRight, null, null, attackOriginOverride)
                .FirstOrDefault()
                ?.PoolId;
        }

        private static AttackResolutionMode ResolveDeferredTargetingMode(SkillData skill, int? queuedAttackActionType = null)
        {
            if (TryResolveDeferredTargetingModeFromQueuedAttackActionType(skill, queuedAttackActionType, out AttackResolutionMode queuedMode))
            {
                return queuedMode;
            }

            if (skill?.AttackType == SkillAttackType.Magic)
            {
                return AttackResolutionMode.Magic;
            }

            if (skill?.Projectile != null)
            {
                return AttackResolutionMode.Projectile;
            }

            return skill?.AttackType == SkillAttackType.Ranged
                ? ShouldTryMeleeBeforeRangedFallback(skill)
                    ? AttackResolutionMode.Melee
                    : AttackResolutionMode.Ranged
                : AttackResolutionMode.Melee;
        }

        private static bool TryResolveDeferredTargetingModeFromQueuedAttackActionType(
            SkillData skill,
            int? queuedAttackActionType,
            out AttackResolutionMode mode)
        {
            mode = AttackResolutionMode.Melee;
            if (!queuedAttackActionType.HasValue)
            {
                return false;
            }

            switch (queuedAttackActionType.Value)
            {
                case MovingShootAttackActionTypeMagic:
                    mode = AttackResolutionMode.Magic;
                    return true;
                case MovingShootAttackActionTypeShoot:
                    mode = skill?.Projectile != null
                        ? AttackResolutionMode.Projectile
                        : AttackResolutionMode.Ranged;
                    return true;
                case MovingShootAttackActionTypeMelee:
                    mode = AttackResolutionMode.Melee;
                    return true;
                default:
                    return false;
            }
        }

        private static int ResolveQueuedMovingShootAttackActionType(
            SkillData skill,
            string currentActionName,
            int? currentRawActionCode)
        {
            if (TryResolveMovingShootAttackActionTypeFromCurrentAction(
                    currentActionName,
                    currentRawActionCode,
                    out int currentActionType))
            {
                return currentActionType;
            }

            return ResolveQueuedMovingShootAttackActionType(skill);
        }

        private static int ResolveQueuedMovingShootAttackActionType(SkillData skill)
        {
            AttackResolutionMode mode = ResolveDeferredTargetingMode(skill);
            return mode == AttackResolutionMode.Magic
                ? MovingShootAttackActionTypeMagic
                : mode == AttackResolutionMode.Melee
                    ? MovingShootAttackActionTypeMelee
                    : MovingShootAttackActionTypeShoot;
        }

        internal static bool TryResolveMovingShootAttackActionTypeFromCurrentAction(
            string currentActionName,
            int? currentRawActionCode,
            out int attackActionType)
        {
            attackActionType = default;
            if (TryResolveMovingShootAttackActionTypeFromRawActionCode(currentRawActionCode, out attackActionType))
            {
                return true;
            }

            string resolvedActionName = currentActionName;
            if (string.IsNullOrWhiteSpace(resolvedActionName)
                && currentRawActionCode.HasValue
                && CharacterPart.TryGetActionStringFromCode(currentRawActionCode.Value, out string mappedActionName))
            {
                resolvedActionName = mappedActionName;
            }

            if (string.IsNullOrWhiteSpace(resolvedActionName))
            {
                return false;
            }

            string normalizedActionName = resolvedActionName.Trim();
            if (normalizedActionName.StartsWith("magic", StringComparison.OrdinalIgnoreCase))
            {
                attackActionType = MovingShootAttackActionTypeMagic;
                return true;
            }

            if (normalizedActionName.StartsWith("shoot", StringComparison.OrdinalIgnoreCase)
                || normalizedActionName.StartsWith("shot", StringComparison.OrdinalIgnoreCase))
            {
                attackActionType = MovingShootAttackActionTypeShoot;
                return true;
            }

            if (normalizedActionName.StartsWith("swing", StringComparison.OrdinalIgnoreCase)
                || normalizedActionName.StartsWith("stab", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedActionName, "proneStab", StringComparison.OrdinalIgnoreCase))
            {
                attackActionType = MovingShootAttackActionTypeMelee;
                return true;
            }

            ClientRandomMovingShootActionFamily actionFamily = ResolveClientRandomMovingShootActionFamilyFromActionName(normalizedActionName);
            switch (actionFamily)
            {
                case ClientRandomMovingShootActionFamily.Shoot:
                    attackActionType = MovingShootAttackActionTypeShoot;
                    return true;
                case ClientRandomMovingShootActionFamily.OneHandedStab:
                case ClientRandomMovingShootActionFamily.TwoHandedStab:
                case ClientRandomMovingShootActionFamily.OneHandedSwing:
                case ClientRandomMovingShootActionFamily.TwoHandedSwing:
                case ClientRandomMovingShootActionFamily.PolearmSwing:
                    attackActionType = MovingShootAttackActionTypeMelee;
                    return true;
            }

            return false;
        }

        private static bool TryResolveMovingShootAttackActionTypeFromRawActionCode(
            int? currentRawActionCode,
            out int attackActionType)
        {
            attackActionType = default;
            if (!currentRawActionCode.HasValue)
            {
                return false;
            }

            switch (currentRawActionCode.Value)
            {
                case 22:
                case 23:
                case 24:
                case 45:
                    attackActionType = MovingShootAttackActionTypeShoot;
                    return true;
                case 48:
                case 49:
                case 50:
                case 51:
                case 52:
                case 53:
                    attackActionType = MovingShootAttackActionTypeMagic;
                    return true;
                case >= 4 and <= 21:
                case 25:
                    attackActionType = MovingShootAttackActionTypeMelee;
                    return true;
                default:
                    return false;
            }
        }

        private int ResolveQueuedMovingShootActionSpeed(SkillData skill)
        {
            int weaponAttackSpeed = Math.Max(0, _player?.Build?.GetWeapon()?.AttackSpeed ?? 0);
            int boosterAttackSpeedDelta = GetBuffStat(BuffStatType.Booster);
            int auraBoosterDelta = ResolveQueuedMovingShootAuraBoosterDelta(_buffs);
            return ResolveQueuedMovingShootActionSpeedDegree(
                weaponAttackSpeed,
                skillId: skill?.SkillId ?? 0,
                weaponBoosterDelta: boosterAttackSpeedDelta,
                auraBoosterDelta: auraBoosterDelta);
        }

        internal int ResolveSharedSkillUseDelayRate(int skillId)
        {
            int weaponAttackSpeed = Math.Max(0, _player?.Build?.GetWeapon()?.AttackSpeed ?? 0);
            int boosterAttackSpeedDelta = GetBuffStat(BuffStatType.Booster);
            int auraBoosterDelta = ResolveQueuedMovingShootAuraBoosterDelta(_buffs);
            int actionSpeedDegree = ResolveQueuedMovingShootActionSpeedDegree(
                weaponAttackSpeed,
                skillId,
                weaponBoosterDelta: boosterAttackSpeedDelta,
                auraBoosterDelta: auraBoosterDelta);
            actionSpeedDegree = Math.Clamp(actionSpeedDegree, MovingShootActionSpeedMinDegree, MovingShootActionSpeedMaxDegree);
            return (1000 * (actionSpeedDegree + 10)) / 16;
        }

        internal static int ResolveQueuedMovingShootAuraBoosterDelta(IEnumerable<ActiveBuff> activeBuffs)
        {
            if (activeBuffs == null)
            {
                return 0;
            }

            ActiveBuff yellowAura = activeBuffs
                .FirstOrDefault(buff => buff?.SkillId == BATTLE_MAGE_YELLOW_AURA_SKILL_ID);
            return yellowAura?.LevelData?.Y ?? 0;
        }

        internal static int ResolveQueuedMovingShootActionSpeedDegree(
            int weaponAttackSpeed,
            int skillId,
            int weaponBoosterDelta = 0,
            int partyBoosterDelta = 0,
            int auraBoosterDelta = 0,
            int frozenPercent = 0)
        {
            // `CUserLocal::TryDoingSmoothingMovingShootAttackPrepare` stores
            // `m_movingShootEntry.nActionSpeed` from `get_attack_speed_degree(...)`,
            // which clamps to [2,10], applies the live booster deltas, and carries a small
            // client-owned skill-exception table that bypasses those temporary-stat modifiers.
            int resolvedWeaponBoosterDelta = weaponBoosterDelta;
            int resolvedPartyBoosterDelta = partyBoosterDelta;
            int resolvedFrozenPercent = frozenPercent;
            int resolvedWeaponAttackSpeed = weaponAttackSpeed;

            if (ClientMovingShootSpeedModifierIgnoreSkillIds.Contains(skillId))
            {
                resolvedWeaponBoosterDelta = 0;
                resolvedPartyBoosterDelta = 0;
                resolvedFrozenPercent = 0;
            }
            else if (skillId == ClientMovingShootBaseSpeedOffsetSkillId)
            {
                resolvedWeaponAttackSpeed -= 2;
            }

            int resolvedActionSpeed = resolvedWeaponAttackSpeed
                                      + resolvedWeaponBoosterDelta
                                      + resolvedPartyBoosterDelta
                                      + auraBoosterDelta;
            if (resolvedFrozenPercent > 0 && resolvedActionSpeed < MovingShootActionSpeedMaxDegree)
            {
                resolvedActionSpeed += (resolvedFrozenPercent * (MovingShootActionSpeedMaxDegree - resolvedActionSpeed)) / 100;
            }

            return Math.Clamp(
                resolvedActionSpeed,
                MovingShootActionSpeedMinDegree,
                MovingShootActionSpeedMaxDegree);
        }

        internal static (string ActionName, int? RawActionCode) ResolveQueuedMovingShootEntryAction(
            SkillData skill,
            string currentActionName,
            int? currentRawActionCode,
            int? queuedAttackActionType = null,
            string currentWeaponType = null,
            Func<int, int> nextCandidateIndex = null)
        {
            var mappedCandidates = EnumerateQueuedMovingShootEntryMappedActionCandidates(
                    skill,
                    queuedAttackActionType,
                    currentActionName,
                    currentRawActionCode,
                    currentWeaponType)
                .ToArray();
            if (mappedCandidates.Length > 0)
            {
                int candidateIndex = ResolveQueuedMovingShootEntryCandidateIndex(
                    mappedCandidates.Length,
                    nextCandidateIndex);
                return mappedCandidates[candidateIndex];
            }

            if (UsesClientRandomShootAttackActionTable(skill))
            {
                // Client skill type 3 does not fall back to the avatar's unrelated
                // live action owner when the shared random-action helper fails.
                return (null, null);
            }

            string fallbackActionName = null;
            if (skill?.ActionNames != null)
            {
                foreach (string candidate in skill.ActionNames)
                {
                    if (string.IsNullOrWhiteSpace(candidate))
                    {
                        continue;
                    }

                    fallbackActionName ??= candidate;
                    if (CharacterPart.TryGetClientRawActionCode(candidate, out int rawActionCode))
                    {
                        return (candidate, rawActionCode);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(fallbackActionName))
            {
                return (fallbackActionName, null);
            }

            if (!string.IsNullOrWhiteSpace(skill?.ActionName))
            {
                if (CharacterPart.TryGetClientRawActionCode(skill.ActionName, out int rawActionCode))
                {
                    return (skill.ActionName, rawActionCode);
                }

                return (skill.ActionName, null);
            }

            if (currentRawActionCode.HasValue
                && CharacterPart.TryGetActionStringFromCode(currentRawActionCode.Value, out string queuedActionName))
            {
                return (queuedActionName, currentRawActionCode);
            }

            return (currentActionName, currentRawActionCode);
        }

        private static int ResolveQueuedMovingShootEntryCandidateIndex(int candidateCount, Func<int, int> nextCandidateIndex)
        {
            if (candidateCount <= 1)
            {
                return 0;
            }

            int requestedIndex = (nextCandidateIndex ?? Random.Next)(candidateCount);
            if (requestedIndex < 0)
            {
                return 0;
            }

            return requestedIndex >= candidateCount
                ? candidateCount - 1
                : requestedIndex;
        }

        internal static IEnumerable<(string ActionName, int? RawActionCode)> EnumerateQueuedMovingShootEntryMappedActionCandidates(
            SkillData skill,
            int? queuedAttackActionType = null,
            string currentActionName = null,
            int? currentRawActionCode = null,
            string currentWeaponType = null)
        {
            if (UsesClientRandomShootAttackActionTable(skill))
            {
                foreach ((string ActionName, int RawActionCode) candidate in EnumerateQueuedMovingShootEntryClientRandomActionCandidates(
                             queuedAttackActionType,
                             currentActionName,
                             currentRawActionCode,
                             currentWeaponType))
                {
                    yield return candidate;
                }

                yield break;
            }

            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string actionName in EnumerateQueuedMovingShootEntrySkillActionCandidates(skill))
            {
                foreach (string lookupActionName in CharacterPart.GetActionLookupStrings(actionName))
                {
                    if (string.IsNullOrWhiteSpace(lookupActionName)
                        || !yielded.Add(lookupActionName)
                        || !CharacterPart.TryGetClientRawActionCode(lookupActionName, out int rawActionCode))
                    {
                        continue;
                    }

                    yield return (lookupActionName, rawActionCode);
                }
            }
        }

        internal static IEnumerable<(string ActionName, int RawActionCode)> EnumerateQueuedMovingShootEntryClientRandomActionCandidates(
            int? queuedAttackActionType,
            string currentActionName = null,
            int? currentRawActionCode = null,
            string currentWeaponType = null)
        {
            ClientRandomMovingShootActionFamily actionFamily = ResolveClientRandomMovingShootActionFamily(
                queuedAttackActionType,
                currentActionName,
                currentRawActionCode,
                currentWeaponType);
            if (actionFamily == ClientRandomMovingShootActionFamily.None)
            {
                yield break;
            }

            foreach ((string ActionName, int RawActionCode) candidate in EnumerateClientRandomMovingShootActionFamilyActions(actionFamily))
            {
                yield return candidate;
            }
        }

        private static IEnumerable<(string ActionName, int RawActionCode)> EnumerateClientRandomMovingShootActionFamilyActions(
            ClientRandomMovingShootActionFamily actionFamily)
        {
            string[] actionNames = actionFamily switch
            {
                ClientRandomMovingShootActionFamily.Shoot => ClientRandomMovingShootShootFamilyActionNames,
                ClientRandomMovingShootActionFamily.OneHandedStab => ClientRandomMovingShootOneHandedStabActionNames,
                ClientRandomMovingShootActionFamily.TwoHandedStab => ClientRandomMovingShootTwoHandedStabActionNames,
                ClientRandomMovingShootActionFamily.OneHandedSwing => ClientRandomMovingShootOneHandedSwingActionNames,
                ClientRandomMovingShootActionFamily.TwoHandedSwing => ClientRandomMovingShootTwoHandedSwingActionNames,
                ClientRandomMovingShootActionFamily.PolearmSwing => ClientRandomMovingShootPolearmSwingActionNames,
                _ => null
            };

            if (actionNames == null)
            {
                yield break;
            }

            foreach (string actionName in actionNames)
            {
                if (CharacterPart.TryGetClientRawActionCode(actionName, out int rawActionCode))
                {
                    yield return (actionName, rawActionCode);
                }
            }
        }

        private static ClientRandomMovingShootActionFamily ResolveClientRandomMovingShootActionFamily(
            int? queuedAttackActionType,
            string currentActionName,
            int? currentRawActionCode,
            string currentWeaponType)
        {
            ClientRandomMovingShootActionFamily requiredFamily = ResolveRequiredClientRandomMovingShootActionFamily(queuedAttackActionType);
            string normalizedActionName = NormalizeDeferredMovingShootActionOwnerName(currentActionName, currentRawActionCode);
            if (!string.IsNullOrWhiteSpace(normalizedActionName))
            {
                ClientRandomMovingShootActionFamily currentActionFamily = ResolveClientRandomMovingShootActionFamilyFromActionName(normalizedActionName);
                if (IsCompatibleClientRandomMovingShootActionFamily(currentActionFamily, requiredFamily))
                {
                    return currentActionFamily;
                }
            }

            string normalizedWeaponType = currentWeaponType?.Trim().ToLowerInvariant();
            ClientRandomMovingShootActionFamily weaponFamily = normalizedWeaponType switch
            {
                "bow" or "crossbow" or "claw" or "gun" or "double bowgun" or "cannon" => ClientRandomMovingShootActionFamily.Shoot,
                "dagger" => ClientRandomMovingShootActionFamily.OneHandedStab,
                // The shared body action surface still publishes distinct `stabT*` and
                // `swingP*` families, so keep spear and polearm inference separate when
                // the queued moving-shoot owner has to fall back to weapon family.
                "spear" => ClientRandomMovingShootActionFamily.TwoHandedStab,
                "polearm" => ClientRandomMovingShootActionFamily.PolearmSwing,
                "2h sword" or "2h axe" or "2h blunt" => ClientRandomMovingShootActionFamily.TwoHandedSwing,
                "1h sword" or "1h axe" or "1h blunt" or "katara" or "wand" or "staff" or "knuckle" => ClientRandomMovingShootActionFamily.OneHandedSwing,
                _ => ClientRandomMovingShootActionFamily.None
            };

            return IsCompatibleClientRandomMovingShootActionFamily(weaponFamily, requiredFamily)
                ? weaponFamily
                : ClientRandomMovingShootActionFamily.None;
        }

        private static ClientRandomMovingShootActionFamily ResolveClientRandomMovingShootActionFamilyFromActionName(string normalizedActionName)
        {
            if (string.IsNullOrWhiteSpace(normalizedActionName))
            {
                return ClientRandomMovingShootActionFamily.None;
            }

            if (normalizedActionName.StartsWith("shoot", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedActionName, "shotC1", StringComparison.OrdinalIgnoreCase))
            {
                return ClientRandomMovingShootActionFamily.Shoot;
            }

            if (normalizedActionName.StartsWith("stabO", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedActionName, "stabD1", StringComparison.OrdinalIgnoreCase))
            {
                return ClientRandomMovingShootActionFamily.OneHandedStab;
            }

            if (normalizedActionName.StartsWith("stabT", StringComparison.OrdinalIgnoreCase))
            {
                return ClientRandomMovingShootActionFamily.TwoHandedStab;
            }

            if (normalizedActionName.StartsWith("swingP", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedActionName, "doubleSwing", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedActionName, "tripleSwing", StringComparison.OrdinalIgnoreCase))
            {
                return ClientRandomMovingShootActionFamily.PolearmSwing;
            }

            if (normalizedActionName.StartsWith("swingT", StringComparison.OrdinalIgnoreCase))
            {
                return ClientRandomMovingShootActionFamily.TwoHandedSwing;
            }

            if (normalizedActionName.StartsWith("swingO", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedActionName, "swingD1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedActionName, "swingD2", StringComparison.OrdinalIgnoreCase))
            {
                return ClientRandomMovingShootActionFamily.OneHandedSwing;
            }

            return ClientRandomMovingShootActionFamily.None;
        }

        private static ClientRandomMovingShootActionFamily ResolveRequiredClientRandomMovingShootActionFamily(int? queuedAttackActionType)
        {
            return queuedAttackActionType switch
            {
                MovingShootAttackActionTypeShoot => ClientRandomMovingShootActionFamily.Shoot,
                MovingShootAttackActionTypeMagic => ClientRandomMovingShootActionFamily.Unsupported,
                MovingShootAttackActionTypeMelee => ClientRandomMovingShootActionFamily.OneHandedSwing,
                _ => ClientRandomMovingShootActionFamily.None
            };
        }

        private static bool IsCompatibleClientRandomMovingShootActionFamily(
            ClientRandomMovingShootActionFamily candidateFamily,
            ClientRandomMovingShootActionFamily requiredFamily)
        {
            if (candidateFamily == ClientRandomMovingShootActionFamily.None)
            {
                return false;
            }

            if (requiredFamily == ClientRandomMovingShootActionFamily.Unsupported)
            {
                return false;
            }

            if (requiredFamily == ClientRandomMovingShootActionFamily.None)
            {
                return true;
            }

            if (requiredFamily == ClientRandomMovingShootActionFamily.Shoot)
            {
                return candidateFamily == ClientRandomMovingShootActionFamily.Shoot;
            }

            return candidateFamily != ClientRandomMovingShootActionFamily.Shoot;
        }

        internal static bool UsesClientRandomShootAttackActionTable(SkillData skill)
        {
            // `get_random_shoot_attack_action(...)` ignores appointed `action/*` rows for
            // client skill type 3, and non-type-3 moving-shoot skills also fall back to
            // the shared table when WZ did not appoint an action at all.
            return skill == null
                || skill.ClientInfoType == 3
                || !HasClientAppointedMovingShootAction(skill);
        }

        private static bool HasClientAppointedMovingShootAction(SkillData skill)
        {
            if (!string.IsNullOrWhiteSpace(skill?.ActionName))
            {
                return true;
            }

            if (skill?.ActionNames == null)
            {
                return false;
            }

            foreach (string actionName in skill.ActionNames)
            {
                if (!string.IsNullOrWhiteSpace(actionName))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<string> EnumerateQueuedMovingShootEntrySkillActionCandidates(SkillData skill)
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (skill?.ActionNames != null)
            {
                foreach (string actionName in skill.ActionNames)
                {
                    if (!string.IsNullOrWhiteSpace(actionName) && yielded.Add(actionName))
                    {
                        yield return actionName;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(skill?.ActionName) && yielded.Add(skill.ActionName))
            {
                yield return skill.ActionName;
            }
        }

        internal static IEnumerable<string> EnumerateQueuedMovingShootEntryActionCandidates(
            SkillData skill,
            string currentActionName,
            int? currentRawActionCode)
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (skill?.ActionNames != null)
            {
                foreach (string actionName in skill.ActionNames)
                {
                    if (!string.IsNullOrWhiteSpace(actionName) && yielded.Add(actionName))
                    {
                        yield return actionName;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(skill?.ActionName) && yielded.Add(skill.ActionName))
            {
                yield return skill.ActionName;
            }

            if (currentRawActionCode.HasValue
                && CharacterPart.TryGetActionStringFromCode(currentRawActionCode.Value, out string rawActionName)
                && yielded.Add(rawActionName))
            {
                yield return rawActionName;
            }

            if (!string.IsNullOrWhiteSpace(currentActionName) && yielded.Add(currentActionName))
            {
                yield return currentActionName;
            }
        }

        private Vector2? ResolveDeferredPreferredTargetPosition(int? preferredTargetMobId, int currentTime)
        {
            if (!preferredTargetMobId.HasValue || _mobPool == null)
            {
                return null;
            }

            MobItem preferredTarget = _mobPool.ActiveMobs.FirstOrDefault(mob => mob?.PoolId == preferredTargetMobId.Value);
            if (preferredTarget == null)
            {
                return null;
            }

            Rectangle targetHitbox = GetMobHitbox(preferredTarget, currentTime);
            if (!targetHitbox.IsEmpty)
            {
                return new Vector2(
                    targetHitbox.Left + (targetHitbox.Width * 0.5f),
                    targetHitbox.Top + (targetHitbox.Height * 0.5f));
            }

            return new Vector2(preferredTarget.CurrentX, preferredTarget.CurrentY);
        }

        internal static bool ShouldTryMeleeBeforeRangedFallback(SkillData skill)
        {
            if (skill?.AttackType != SkillAttackType.Ranged || skill.Projectile != null)
            {
                return false;
            }

            return skill.SkillId switch
            {
                5001003 or 5210000 => true,
                _ => !IsClientShootAttackNotSwitchedToMeleeAttack(skill.SkillId)
            };
        }

        private static bool IsClientShootAttackNotSwitchedToMeleeAttack(int skillId)
        {
            return ClientShootAttackFamilyResolver.UsesShootLaneWithoutMeleeFallback(skillId);
        }

        private void BeginPreparedSkill(SkillData skill, int level, int currentTime, int ownerHotkeySlot = -1, int ownerInputToken = 0)
        {
            var levelData = skill.GetLevel(level);
            int durationMs = ResolvePreparedSkillStartupDuration(skill, levelData);
            PreparedSkillHudRules.PreparedSkillHudProfile hudProfile = PreparedSkillHudRules.ResolveProfile(skill?.SkillId ?? 0);
            bool usesReleaseTriggeredKeydown = UsesReleaseTriggeredKeydownExecution(skill);
            RecordPreparedSkillExclusiveRequest(currentTime);

            _preparedSkill = new PreparedSkill
            {
                SkillId = skill.SkillId,
                Level = level,
                OwnerHotkeySlot = ownerHotkeySlot,
                OwnerInputToken = ownerInputToken,
                StartTime = currentTime,
                Duration = durationMs,
                MaxHoldDurationMs = ResolveKeyDownMaxHoldDuration(skill?.SkillId ?? 0),
                HudGaugeDurationMs = ResolvePreparedSkillHudGaugeDuration(skill, durationMs, hudProfile.GaugeDurationMs),
                HudSkinKey = hudProfile.SkinKey,
                HudTextVariant = ResolvePreparedSkillHudTextVariant(skill),
                ShowHudBar = hudProfile.Visible,
                ShowHudText = hudProfile.ShowText,
                HudSurface = hudProfile.Surface,
                SkillData = skill,
                LevelData = levelData,
                IsKeydownSkill = PreparedSkillHudRules.ResolveKeyDownSkillState(
                    skill.SkillId,
                    skill.IsKeydownSkill || usesReleaseTriggeredKeydown)
            };

            if (!_preparedSkill.IsKeydownSkill && _preparedSkill.Duration > 0)
            {
                RegisterClientSkillTimer(
                    _preparedSkill.SkillId,
                    ClientTimerSourcePreparedRelease,
                    currentTime + _preparedSkill.Duration,
                    ReleasePreparedSkill);
            }
            else if (_preparedSkill.IsKeydownSkill && _preparedSkill.Duration <= 0)
            {
                BeginPreparedSkillHold(_preparedSkill, currentTime);
            }

            OnPreparedSkillStarted?.Invoke(_preparedSkill);
        }

        private bool ShouldThrottlePreparedSkillRequest(SkillData skill, int currentTime)
        {
            if (skill == null)
            {
                return false;
            }

            bool isPreparedRequest = skill.IsPrepareSkill
                || skill.IsKeydownSkill
                || UsesReleaseTriggeredKeydownExecution(skill);
            return isPreparedRequest
                   && _lastPreparedSkillExclusiveRequestTime != int.MinValue
                   && currentTime - _lastPreparedSkillExclusiveRequestTime < ExclusiveRequestThrottleMs;
        }

        private void RecordPreparedSkillExclusiveRequest(int currentTime)
        {
            _lastPreparedSkillExclusiveRequestTime = currentTime;
        }

        private static bool CanInputSourceControlPreparedSkill(PreparedSkill prepared, int ownerHotkeySlot, int ownerInputToken)
        {
            if (prepared == null)
            {
                return false;
            }

            int activeOwnerHotkeySlot = prepared.OwnerHotkeySlot;
            if (activeOwnerHotkeySlot < 0)
            {
                return ownerHotkeySlot < 0
                    && (prepared.OwnerInputToken == 0 || ownerInputToken == prepared.OwnerInputToken);
            }

            if (ownerHotkeySlot != activeOwnerHotkeySlot)
            {
                return false;
            }

            return prepared.OwnerInputToken == 0 || ownerInputToken == prepared.OwnerInputToken;
        }

        internal static bool TryEnterPreparedSkillHoldState(
            PreparedSkill prepared,
            int currentTime,
            int repeatInterval,
            bool armAtFullStrength = false)
        {
            if (prepared == null || prepared.IsHolding)
            {
                return false;
            }

            if (armAtFullStrength)
            {
                int armedDurationMs = prepared.Duration > 0
                    ? prepared.Duration
                    : prepared.HudGaugeDurationMs;
                if (armedDurationMs > 0)
                {
                    prepared.StartTime = currentTime - armedDurationMs;
                }
            }

            prepared.IsHolding = true;
            prepared.HoldStartTime = currentTime;
            prepared.LastRepeatTime = currentTime - Math.Max(1, repeatInterval);
            return true;
        }

        private void ActivatePreparedSkillHoldRuntime(PreparedSkill prepared, int currentTime)
        {
            if (prepared == null)
            {
                return;
            }

            if (prepared.MaxHoldDurationMs > 0)
            {
                RegisterClientSkillTimer(
                    prepared.SkillId,
                    ClientTimerSourcePreparedRelease,
                    currentTime + prepared.MaxHoldDurationMs,
                    ReleasePreparedSkill);
            }

            UpdateCurrentCastEffect(
                GetHoldCastEffect(prepared.SkillData),
                GetHoldCastSecondaryEffect(prepared.SkillData),
                currentTime);
            _player.BeginSustainedSkillAnimation(
                ResolvePreparedAvatarActionName(
                    prepared.SkillData,
                    GetKeydownActionName(prepared.SkillData)));
        }

        private void BeginPreparedSkillHold(PreparedSkill prepared, int currentTime)
        {
            if (!TryEnterPreparedSkillHoldState(
                    prepared,
                    currentTime,
                    GetKeydownRepeatInterval(prepared?.SkillData)))
            {
                return;
            }

            ActivatePreparedSkillHoldRuntime(prepared, currentTime);
        }

        public void NotifyLocalCriticalHit(int currentTime)
        {
            if (_preparedSkill?.IsKeydownSkill != true
                || !PreparedSkillHudRules.ArmsAtFullStrengthOnCriticalHit(_preparedSkill.SkillId))
            {
                return;
            }

            if (!TryEnterPreparedSkillHoldState(
                    _preparedSkill,
                    currentTime,
                    GetKeydownRepeatInterval(_preparedSkill.SkillData),
                    armAtFullStrength: true))
            {
                return;
            }

            ActivatePreparedSkillHoldRuntime(_preparedSkill, currentTime);
        }

        internal static int ResolvePreparedSkillStartupDuration(SkillData skill, SkillLevelData levelData)
        {
            if (UsesReleaseTriggeredKeydownExecution(skill))
            {
                if (HasAuthoredPreparedStartup(skill))
                {
                    return Math.Max(0, skill?.PrepareDurationMs ?? 0);
                }

                return 0;
            }

            if (skill?.PrepareDurationMs > 0)
                return skill.PrepareDurationMs;

            if (levelData == null)
                return 750;

            if (levelData.Time > 0 && levelData.Time <= 5)
                return levelData.Time * 1000;

            if (levelData.X > 0 && levelData.X <= 5000)
                return levelData.X;

            if (levelData.Y > 0 && levelData.Y <= 5000)
                return levelData.Y;

            return skill.Projectile != null ? 900 : 750;
        }

        private static bool HasAuthoredPreparedStartup(SkillData skill)
        {
            return skill != null
                   && (!string.IsNullOrWhiteSpace(skill.PrepareActionName)
                       || skill.PrepareEffect != null
                       || skill.PrepareSecondaryEffect != null
                       || skill.PrepareDurationMs > 0);
        }

        private static PreparedSkillHudRules.PreparedSkillHudProfile ResolveKeyDownHudProfile(int skillId)
        {
            return PreparedSkillHudRules.ResolveProfile(skillId);
        }

        private static int ResolvePreparedSkillHudGaugeDuration(SkillData skill, int prepareDurationMs, int explicitGaugeDurationMs)
        {
            return PreparedSkillHudRules.ResolvePreparedGaugeDuration(
                skill?.SkillId ?? 0,
                explicitGaugeDurationMs,
                prepareDurationMs);
        }

        private void ReleasePreparedSkill(int currentTime)
        {
            if (_preparedSkill == null)
                return;

            PreparedSkill prepared = _preparedSkill;
            _preparedSkill = null;
            CancelClientSkillTimers(prepared.SkillId, ClientTimerSourcePreparedRelease);
            bool usesReleaseTriggeredKeydown = UsesReleaseTriggeredKeydownExecution(prepared.SkillData);

            if (prepared.IsKeydownSkill)
            {
                _player.EndSustainedSkillAnimation();
                UpdateCurrentCastEffect(
                    prepared.SkillData?.KeydownEndEffect,
                    prepared.SkillData?.KeydownEndSecondaryEffect,
                    currentTime);
                bool hadTransform = _player.HasSkillAvatarTransform(prepared.SkillId);
                _player.ClearSkillAvatarTransform(prepared.SkillId);

                string endActionName = ResolveKeydownEndAvatarActionName(prepared.SkillData);
                if (!hadTransform
                    && !string.IsNullOrWhiteSpace(endActionName)
                    && _player.IsAlive)
                {
                    _player.TriggerSkillAnimation(endActionName);
                }

                int releaseChargeElapsedMs = ResolvePreparedSkillReleaseChargeElapsedMs(prepared, currentTime);
                float releaseDamageScale = ResolvePreparedSkillReleaseDamageScale(prepared, currentTime);
                bool supportsPartialReleaseDamage = UsesPreparedSkillChargeDamageScaling(prepared.SkillData);
                bool canExecuteReleasePayload = prepared.IsHolding
                    || (usesReleaseTriggeredKeydown && releaseChargeElapsedMs > 0);
                if (usesReleaseTriggeredKeydown
                    && canExecuteReleasePayload
                    && prepared.LevelData != null
                    && TryConsumePreparedSkillReleaseResources(prepared, currentTime))
                {
                    ExecuteSkillPayload(
                        prepared.SkillData,
                        prepared.Level,
                        currentTime,
                        damageScaleOverride: supportsPartialReleaseDamage ? releaseDamageScale : null);
                }

                ClearSkillMount(prepared.SkillId);
                OnPreparedSkillReleased?.Invoke(prepared);
                return;
            }

            ExecuteSkillPayload(prepared.SkillData, prepared.Level, currentTime);
            _player.ClearSkillAvatarTransform(prepared.SkillId);
            ClearSkillMount(prepared.SkillId);
            OnPreparedSkillReleased?.Invoke(prepared);
        }

        private void UpdateKeydownSkill(int currentTime)
        {
            if (_preparedSkill?.IsKeydownSkill != true)
                return;

            PreparedSkill prepared = _preparedSkill;
            if (!prepared.IsHolding)
            {
                if (prepared.Duration > 0 && prepared.Elapsed(currentTime) < prepared.Duration)
                    return;

                BeginPreparedSkillHold(prepared, currentTime);
            }

            if (!_player.IsAlive || !_player.CanAttack)
            {
                ReleasePreparedSkill(currentTime);
                return;
            }

            if (!string.IsNullOrWhiteSpace(GetStateRestrictionMessage(prepared.SkillData, currentTime)))
            {
                ReleasePreparedSkill(currentTime);
                return;
            }

            if (_fieldSkillRestrictionEvaluator != null && !_fieldSkillRestrictionEvaluator(prepared.SkillData))
            {
                ReleasePreparedSkill(currentTime);
                return;
            }

            if (UsesReleaseTriggeredKeydownExecution(prepared.SkillData))
            {
                return;
            }

            int repeatInterval = GetKeydownRepeatInterval(prepared.SkillData);
            while (_preparedSkill != null && currentTime - prepared.LastRepeatTime >= repeatInterval)
            {
                if (!TryConsumeSkillResources(prepared.SkillData, prepared.LevelData))
                {
                    ReleasePreparedSkill(currentTime);
                    return;
                }

                PlayRepeatSound(prepared.SkillData);
                ExecuteSkillPayload(prepared.SkillData, prepared.Level, currentTime);
                prepared.LastRepeatTime += repeatInterval;
            }
        }

        private static string GetPrepareActionName(SkillData skill)
        {
            if (!string.IsNullOrWhiteSpace(skill?.PrepareActionName))
                return skill.PrepareActionName;

            return GetKeydownActionName(skill);
        }

        private string ResolvePreparedAvatarActionName(SkillData skill, string prepareActionName)
        {
            if (_player == null || string.IsNullOrWhiteSpace(prepareActionName))
            {
                return prepareActionName;
            }

            foreach (string candidate in EnumeratePreparedAvatarActionCandidates(skill, prepareActionName))
            {
                if (_player.CanRenderAction(candidate))
                {
                    return candidate;
                }
            }

            return prepareActionName;
        }

        private static IEnumerable<string> EnumeratePreparedAvatarActionCandidates(SkillData skill, string prepareActionName)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string candidate in EnumeratePreparedAvatarActionCandidatesCore(skill, prepareActionName))
            {
                if (string.IsNullOrWhiteSpace(candidate) || !seen.Add(candidate))
                {
                    continue;
                }

                yield return candidate;
            }
        }

        private static IEnumerable<string> EnumeratePreparedAvatarActionCandidatesCore(SkillData skill, string prepareActionName)
        {
            foreach (string candidate in EnumerateSkillSpecificPreparedAvatarActionCandidates(skill, prepareActionName))
            {
                yield return candidate;
            }

            yield return prepareActionName;

            string actionName = ResolveSkillActionName(skill);
            if (!string.IsNullOrWhiteSpace(actionName))
            {
                yield return actionName;

                foreach (string normalizedActionName in EnumerateNormalizedPreparedActionNames(actionName))
                {
                    yield return normalizedActionName;
                }
            }

            foreach (string normalizedPrepareActionName in EnumerateNormalizedPreparedActionNames(prepareActionName))
            {
                yield return normalizedPrepareActionName;
            }
        }

        private static IEnumerable<string> EnumerateNormalizedPreparedActionNames(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                yield break;
            }

            string normalized = actionName.Trim();
            yield return normalized;

            if (normalized.EndsWith("Loop", StringComparison.OrdinalIgnoreCase))
            {
                yield return normalized.Substring(0, normalized.Length - "Loop".Length);
            }

            if (normalized.EndsWith("Prep", StringComparison.OrdinalIgnoreCase))
            {
                string baseName = normalized.Substring(0, normalized.Length - "Prep".Length);
                yield return baseName;
                yield return baseName + "Loop";
            }

            if (normalized.EndsWith("Prepare", StringComparison.OrdinalIgnoreCase))
            {
                yield return normalized.Substring(0, normalized.Length - "Prepare".Length);
            }

            if (normalized.EndsWith("_prep", StringComparison.OrdinalIgnoreCase))
            {
                yield return normalized.Substring(0, normalized.Length - "_prep".Length);
            }

            if (normalized.EndsWith("_prepare", StringComparison.OrdinalIgnoreCase))
            {
                yield return normalized.Substring(0, normalized.Length - "_prepare".Length);
            }

            if (normalized.EndsWith("_pre", StringComparison.OrdinalIgnoreCase))
            {
                yield return normalized.Substring(0, normalized.Length - "_pre".Length);
            }
        }

        private static string GetKeydownActionName(SkillData skill)
        {
            if (skill?.SkillId == 14111006)
                return "darkTornado";

            if (!string.IsNullOrWhiteSpace(skill?.KeydownActionName))
                return skill.KeydownActionName;

            if (PreservesPrepareActionDuringHold(skill))
                return skill.PrepareActionName;

            return ResolveSkillActionName(skill);
        }

        private static string GetKeydownEndActionName(SkillData skill)
        {
            if (skill?.SkillId == 14111006)
                return "darkTornado_after";

            if (!string.IsNullOrWhiteSpace(skill?.KeydownEndActionName))
                return skill.KeydownEndActionName;

            return null;
        }

        private string ResolveKeydownEndAvatarActionName(SkillData skill)
        {
            string endActionName = GetKeydownEndActionName(skill);
            if (_player == null || string.IsNullOrWhiteSpace(endActionName))
            {
                return endActionName;
            }

            foreach (string candidate in EnumerateKeydownEndAvatarActionCandidates(skill, endActionName))
            {
                if (_player.CanRenderAction(candidate))
                {
                    return candidate;
                }
            }

            return endActionName;
        }

        private static IEnumerable<string> EnumerateKeydownEndAvatarActionCandidates(SkillData skill, string endActionName)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string candidate in EnumerateKeydownEndAvatarActionCandidatesCore(skill, endActionName))
            {
                if (string.IsNullOrWhiteSpace(candidate) || !seen.Add(candidate))
                {
                    continue;
                }

                yield return candidate;
            }
        }

        private static IEnumerable<string> EnumerateKeydownEndAvatarActionCandidatesCore(SkillData skill, string endActionName)
        {
            yield return endActionName;

            foreach (string normalizedEndActionName in EnumerateNormalizedKeydownEndActionNames(endActionName))
            {
                yield return normalizedEndActionName;
            }

            string actionName = ResolveSkillActionName(skill);
            if (!string.IsNullOrWhiteSpace(actionName))
            {
                yield return actionName;

                foreach (string normalizedActionName in EnumerateNormalizedKeydownEndActionNames(actionName))
                {
                    yield return normalizedActionName;
                }
            }
        }

        private static IEnumerable<string> EnumerateNormalizedKeydownEndActionNames(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                yield break;
            }

            string normalized = actionName.Trim();
            yield return normalized;

            if (normalized.EndsWith("End", StringComparison.OrdinalIgnoreCase))
            {
                yield return normalized.Substring(0, normalized.Length - "End".Length);
            }

            if (normalized.EndsWith("_end", StringComparison.OrdinalIgnoreCase))
            {
                yield return normalized.Substring(0, normalized.Length - "_end".Length);
            }

            if (normalized.EndsWith("After", StringComparison.OrdinalIgnoreCase))
            {
                yield return normalized.Substring(0, normalized.Length - "After".Length);
            }

            if (normalized.EndsWith("_after", StringComparison.OrdinalIgnoreCase))
            {
                yield return normalized.Substring(0, normalized.Length - "_after".Length);
            }
        }

        private static IEnumerable<string> EnumerateSkillSpecificPreparedAvatarActionCandidates(SkillData skill, string prepareActionName)
        {
            if (skill?.SkillId == 14111006)
            {
                yield return "darkTornado_pre";

                if (string.Equals(prepareActionName, "darkTornado", StringComparison.OrdinalIgnoreCase))
                {
                    yield return "darkTornado";
                }
            }

            if (skill?.SkillId == 33101005)
            {
                yield return "swallow_pre";

                if (string.Equals(prepareActionName, "swallow", StringComparison.OrdinalIgnoreCase))
                {
                    yield return "swallow_loop";
                }
            }

            if (skill?.SkillId == 32121003)
            {
                yield return "cyclone_pre";
                yield return "cyclone";
            }

            if (skill?.SkillId == 5311002)
            {
                yield return "noiseWave_pre";
                yield return "noiseWave_ing";
            }

            if (skill?.SkillId == 23121000)
            {
                yield return "dualVulcanPrep";
                yield return "dualVulcanLoop";
            }
        }

        internal static IReadOnlyList<string> EnumerateSkillSpecificPreparedAvatarActionCandidatesForTesting(
            int skillId,
            string prepareActionName)
        {
            return EnumerateSkillSpecificPreparedAvatarActionCandidates(
                    new SkillData
                    {
                        SkillId = skillId
                    },
                    prepareActionName)
                .ToArray();
        }

        internal static IReadOnlyList<string> EnumeratePreparedAvatarActionCandidatesForTesting(
            int skillId,
            string prepareActionName,
            string actionName = null)
        {
            return EnumeratePreparedAvatarActionCandidates(
                    new SkillData
                    {
                        SkillId = skillId,
                        ActionName = actionName
                    },
                    prepareActionName)
                .ToArray();
        }

        private static int ResolveKeyDownMaxHoldDuration(int skillId)
        {
            return skillId switch
            {
                35001001 => MECHANIC_KEYDOWN_MAX_DURATION_MS,
                35101009 => MECHANIC_KEYDOWN_MAX_DURATION_MS,
                _ => 0
            };
        }

        private static bool UsesReleaseTriggeredKeydownExecution(SkillData skill)
        {
            return PreparedSkillHudRules.UsesReleaseTriggeredExecution(skill?.SkillId ?? 0);
        }

        private static bool UsesPreparedSkillChargeDamageScaling(SkillData skill)
        {
            return PreparedSkillHudRules.UsesChargeDamageScaling(skill?.SkillId ?? 0);
        }

        private static int ResolvePreparedSkillReleaseChargeElapsedMs(PreparedSkill prepared, int currentTime)
        {
            if (prepared == null)
            {
                return 0;
            }

            int chargeWindowMs = prepared.Duration > 0
                ? prepared.Duration
                : prepared.HudGaugeDurationMs;
            return PreparedSkillHudRules.ResolveReleaseChargeElapsedMs(
                prepared.SkillId,
                prepared.Elapsed(currentTime),
                chargeWindowMs);
        }

        private bool TryConsumePreparedSkillReleaseResources(PreparedSkill prepared, int currentTime)
        {
            if (prepared?.LevelData == null)
            {
                return false;
            }

            int maxHp = _player?.MaxHP ?? 0;
            int currentHp = _player?.HP ?? 0;
            int additionalHpCost = ResolvePreparedSkillReleaseHpCost(prepared, currentTime, maxHp);
            string restrictionMessage = ResolvePreparedSkillReleaseRestrictionMessage(
                prepared.SkillData,
                prepared.LevelData,
                currentHp,
                maxHp,
                additionalHpCost);
            if (!string.IsNullOrWhiteSpace(restrictionMessage))
            {
                OnFieldSkillCastRejected?.Invoke(prepared.SkillData, restrictionMessage);
                return false;
            }

            return TryConsumeSkillResources(
                prepared.SkillData,
                prepared.LevelData,
                additionalHpCost);
        }

        internal static int ResolvePreparedSkillReleaseHpCost(PreparedSkill prepared, int currentTime, int maxHp)
        {
            if (prepared == null)
            {
                return 0;
            }

            int chargeWindowMs = prepared.Duration > 0
                ? prepared.Duration
                : prepared.HudGaugeDurationMs;
            int releaseChargeElapsedMs = ResolvePreparedSkillReleaseChargeElapsedMs(prepared, currentTime);
            return ResolvePreparedSkillReleaseHpCost(
                prepared.SkillData,
                prepared.LevelData,
                maxHp,
                releaseChargeElapsedMs,
                chargeWindowMs);
        }

        internal static int ResolvePreparedSkillReleaseHpCost(
            SkillData skill,
            SkillLevelData levelData,
            int maxHp,
            int releaseChargeElapsedMs,
            int chargeWindowMs)
        {
            if (skill?.SkillId != 4341002
                || levelData == null
                || maxHp <= 0
                || levelData.X <= 0
                || releaseChargeElapsedMs <= 0
                || chargeWindowMs <= 0)
            {
                return 0;
            }

            int scaledPercent = releaseChargeElapsedMs * levelData.X / chargeWindowMs;
            return scaledPercent > 0
                ? scaledPercent * maxHp / 100
                : 0;
        }

        internal static string ResolvePreparedSkillReleaseRestrictionMessage(
            SkillData skill,
            SkillLevelData levelData,
            int currentHp,
            int maxHp,
            int additionalHpCost)
        {
            if (skill?.SkillId != 4341002
                || levelData == null
                || currentHp <= 0
                || maxHp <= 0)
            {
                return null;
            }

            int totalHpCost = Math.Max(0, levelData.HpCon) + Math.Max(0, additionalHpCost);
            if (totalHpCost <= 0 || currentHp > totalHpCost)
            {
                return null;
            }

            return MapleStoryStringPool.GetOrFallback(
                0x0B44,
                "You don't have enough HP to use this skill.");
        }

        private static float ResolvePreparedSkillReleaseDamageScale(PreparedSkill prepared, int currentTime)
        {
            if (prepared == null)
            {
                return 1f;
            }

            if (prepared.IsHolding || !UsesPreparedSkillChargeDamageScaling(prepared.SkillData))
            {
                return 1f;
            }

            int chargeWindowMs = prepared.Duration > 0
                ? prepared.Duration
                : prepared.HudGaugeDurationMs;
            if (chargeWindowMs <= 0)
            {
                return 1f;
            }

            int elapsedMs = ResolvePreparedSkillReleaseChargeElapsedMs(prepared, currentTime);
            if (elapsedMs <= 0)
            {
                return 0f;
            }

            return Math.Clamp(elapsedMs / (float)chargeWindowMs, 0f, 1f);
        }

        private static bool PreservesPrepareActionDuringHold(SkillData skill)
        {
            return skill != null
                   && skill.HasChargingSkillMetadata
                   && UsesReleaseTriggeredKeydownExecution(skill)
                   && string.IsNullOrWhiteSpace(skill.KeydownActionName)
                   && !string.IsNullOrWhiteSpace(skill.PrepareActionName);
        }

        private static PreparedSkillHudTextVariant ResolvePreparedSkillHudTextVariant(SkillData skill)
        {
            return PreparedSkillHudRules.ResolveTextVariant(skill?.SkillId ?? 0);
        }

        private static SkillAnimation GetInitialCastEffect(SkillData skill)
        {
            if (skill == null)
                return null;

            return skill.IsKeydownSkill
                ? skill.PrepareEffect ?? skill.Effect
                : skill.Effect;
        }

        private static SkillAnimation GetInitialCastSecondaryEffect(SkillData skill)
        {
            if (skill == null)
                return null;

            return skill.IsKeydownSkill
                ? skill.PrepareSecondaryEffect
                : skill.EffectSecondary;
        }

        private static SkillAnimation GetHoldCastEffect(SkillData skill)
        {
            if (skill == null)
                return null;

            return skill.RepeatEffect ?? skill.KeydownEffect;
        }

        private static SkillAnimation GetHoldCastSecondaryEffect(SkillData skill)
        {
            if (skill == null)
                return null;

            return skill.RepeatSecondaryEffect ?? skill.KeydownSecondaryEffect;
        }

        private void UpdateCurrentCastEffect(SkillAnimation animation, SkillAnimation secondaryAnimation, int currentTime)
        {
            if (_currentCast == null)
                return;

            _currentCast.EffectAnimation = animation;
            _currentCast.SecondaryEffectAnimation = secondaryAnimation;
            _currentCast.CastTime = currentTime;
            _currentCast.IsComplete = false;
        }

        private static int GetKeydownRepeatInterval(SkillData skill)
        {
            if (skill?.RepeatDurationMs > 0)
                return skill.RepeatDurationMs;

            if (skill?.KeydownRepeatIntervalMs > 0)
                return skill.KeydownRepeatIntervalMs;

            if (skill?.KeydownDurationMs > 0)
                return skill.KeydownDurationMs;

            return 90;
        }

        private bool TryApplyClientOwnedAvatarEffect(SkillData skill, int currentTime)
        {
            if (_player == null || skill == null)
            {
                return false;
            }

            if (UsesTransientMovementAvatarEffect(skill))
            {
                return _player.ApplyTransientSkillAvatarEffect(
                    skill.SkillId,
                    skill.Effect,
                    skill.EffectSecondary,
                    currentTime,
                    skill.StopEffect,
                    skill.StopSecondaryEffect);
            }

            return UsesEffectToAvatarLayerBuffFallback(skill);
        }

        private static bool UsesTransientMovementAvatarEffect(SkillData skill)
        {
            if (!HasClientOwnedAvatarEffectBranches(skill) || skill?.IsBuff == true)
            {
                return false;
            }

            if (IsDoubleJumpAction(skill.ActionName))
            {
                return true;
            }

            string movementActionName = ResolveMovementActionName(skill);
            SkillMovementFamily movementFamily = ResolveMovementFamily(skill, movementActionName);
            return movementFamily != SkillMovementFamily.None
                   && movementFamily != SkillMovementFamily.Teleport;
        }

        private static bool IsDoubleJumpAction(string actionName)
        {
            return !string.IsNullOrWhiteSpace(actionName)
                   && actionName.IndexOf("doublejump", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static IEnumerable<string> EnumerateMovementActionCandidates(SkillData skill)
        {
            if (skill == null)
            {
                yield break;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(skill.PrepareActionName) && seen.Add(skill.PrepareActionName))
            {
                yield return skill.PrepareActionName;
            }

            if (skill.ActionNames != null)
            {
                foreach (string actionName in skill.ActionNames)
                {
                    if (!string.IsNullOrWhiteSpace(actionName) && seen.Add(actionName))
                    {
                        yield return actionName;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(skill.ActionName) && seen.Add(skill.ActionName))
            {
                yield return skill.ActionName;
            }
        }

        private static bool IsBoundJumpActionName(string actionName)
        {
            return ActionTextContains(actionName, "doublejump")
                   || ActionTextContains(actionName, "flash jump")
                   || ActionTextContains(actionName, "archerdoublejump")
                   || ActionTextContains(actionName, "backspin")
                   || ActionTextContains(actionName, "assaulter")
                   || ActionTextContains(actionName, "screw");
        }

        private static bool ActionTextContains(string actionName, string value)
        {
            return !string.IsNullOrWhiteSpace(actionName)
                   && !string.IsNullOrWhiteSpace(value)
                   && actionName.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool UsesFlightBuffAvatarEffectFallback(SkillData skill)
        {
            return skill?.IsBuff == true
                   && HasClientOwnedAvatarEffectBranches(skill)
                   && !skill.HasPersistentAvatarEffect
                   && SkillGrantsFlyingAbility(skill);
        }

        private static string ResolveMovementActionName(SkillData skill)
        {
            if (skill == null)
            {
                return null;
            }

            foreach (string candidate in EnumerateMovementActionCandidates(skill))
            {
                if (ActionTextContains(candidate, "teleport"))
                {
                    return candidate;
                }
            }

            foreach (string candidate in EnumerateMovementActionCandidates(skill))
            {
                if (ActionTextContains(candidate, "backstep"))
                {
                    return candidate;
                }
            }

            foreach (string candidate in EnumerateMovementActionCandidates(skill))
            {
                if (IsBoundJumpActionName(candidate))
                {
                    return candidate;
                }
            }

            foreach (string candidate in EnumerateMovementActionCandidates(skill))
            {
                if (ActionTextContains(candidate, "fly")
                    || ActionTextContains(candidate, "flying")
                    || ActionTextContains(candidate, "rocket"))
                {
                    return candidate;
                }
            }

            foreach (string candidate in EnumerateMovementActionCandidates(skill))
            {
                if (ActionTextContains(candidate, "jump")
                    || ActionTextContains(candidate, "hop"))
                {
                    return candidate;
                }
            }

            if (skill.CasterMove && !string.IsNullOrWhiteSpace(skill.PrepareActionName))
            {
                return skill.PrepareActionName;
            }

            return skill.ActionName;
        }

        private static bool ShouldExecuteMovementBranch(SkillData skill)
        {
            if (skill == null)
            {
                return false;
            }

            if (skill.IsMovement)
            {
                return true;
            }

            if (!skill.CasterMove)
            {
                return false;
            }

            if (IsExplicitBoundJumpSkill(skill) || skill.ClientInfoType == 40 || skill.ClientDelayMs > 0)
            {
                return true;
            }

            string movementActionName = ResolveMovementActionName(skill);
            return ResolveMovementFamily(skill, movementActionName) != SkillMovementFamily.None;
        }

        private static SkillMovementFamily ResolveMovementFamily(SkillData skill, string movementActionName)
        {
            if (skill == null)
            {
                return SkillMovementFamily.None;
            }

            string[] candidateActions = EnumerateMovementActionCandidates(skill).ToArray();
            if (candidateActions.Any(candidate => ActionTextContains(candidate, "teleport"))
                || ActionTextContains(movementActionName, "teleport")
                || SkillTextContains(skill, "teleport"))
            {
                return SkillMovementFamily.Teleport;
            }

            if (candidateActions.Any(candidate => ActionTextContains(candidate, "backstep"))
                || ActionTextContains(movementActionName, "backstep"))
            {
                return SkillMovementFamily.Backstep;
            }

            if (skill.ClientInfoType == 40
                || candidateActions.Any(IsBoundJumpActionName)
                || IsBoundJumpActionName(movementActionName)
                || IsExplicitBoundJumpSkill(skill)
                || SkillTextContains(skill, "flash jump"))
            {
                return SkillMovementFamily.BoundJump;
            }

            // Some client-moved rush families, such as Dual Blade's Tornado Spin
            // (Skill/432.img/4321000), only advertise movement through info/casterMove
            // + info/type=41 and do not publish a normal top-level action string.
            if (skill.CasterMove && skill.ClientInfoType == 41)
            {
                return SkillMovementFamily.Rush;
            }

            if (candidateActions.Any(candidate =>
                    ActionTextContains(candidate, "fly")
                    || ActionTextContains(candidate, "flying")
                    || ActionTextContains(candidate, "rocket"))
                || ActionTextContains(movementActionName, "fly")
                || ActionTextContains(movementActionName, "flying")
                || ActionTextContains(movementActionName, "rocket")
                || SkillTextContains(skill, "fly")
                || SkillTextContains(skill, "flying")
                || SkillTextContains(skill, "rocket"))
            {
                return SkillMovementFamily.FlyingRush;
            }

            if (candidateActions.Any(candidate =>
                    ActionTextContains(candidate, "jump")
                    || ActionTextContains(candidate, "hop"))
                || ActionTextContains(movementActionName, "jump")
                || ActionTextContains(movementActionName, "hop")
                || SkillTextContains(skill, "jump")
                || SkillTextContains(skill, "hop"))
            {
                return SkillMovementFamily.JumpRush;
            }

            return !string.IsNullOrWhiteSpace(movementActionName) || candidateActions.Length > 0
                ? SkillMovementFamily.Rush
                : SkillMovementFamily.None;
        }

        private void ExecuteMovementSkill(SkillData skill, int level, int currentTime)
        {
            var levelData = skill.GetLevel(level);
            if (levelData == null)
                return;

            string movementActionName = ResolveMovementActionName(skill);
            SkillMovementFamily movementFamily = ResolveMovementFamily(skill, movementActionName);
            if (movementFamily == SkillMovementFamily.None)
            {
                return;
            }

            int horizontalRange = Math.Max(
                Math.Max(levelData.RangeR, levelData.RangeL),
                Math.Max(levelData.Range, Math.Max(levelData.X, levelData.Y)));

            if (horizontalRange <= 0)
                horizontalRange = 120;

            float direction = _player.FacingRight ? 1f : -1f;
            float moveDirection = movementFamily == SkillMovementFamily.Backstep ? -direction : direction;
            float targetX = _player.X + (horizontalRange * moveDirection);

            if (movementFamily == SkillMovementFamily.Teleport)
            {
                _player.SetPosition(targetX, _player.Y);
                TryExecuteTeleportMasteryBodyAttack(skill, currentTime);
                return;
            }

            if (movementFamily == SkillMovementFamily.BoundJump)
            {
                if (!CanStartBoundJump(skill, currentTime))
                {
                    return;
                }

                ExecuteBoundJumpMovement(skill, level, levelData, movementActionName, moveDirection, horizontalRange);
                return;
            }

            bool isJumpRush = movementFamily == SkillMovementFamily.JumpRush;
            bool isFlyingRush = movementFamily == SkillMovementFamily.FlyingRush;
            bool isDoubleJump = ActionTextContains(movementActionName, "doublejump")
                                || ActionTextContains(movementActionName, "archerdoublejump");

            float rushSpeed = Math.Max(250f, horizontalRange * (isDoubleJump ? 3f : 2.5f));
            if (isJumpRush)
            {
                float jumpPower = (_player.Build?.JumpPower ?? 100) / 100f;
                float jumpRushVerticalSpeed = Math.Max(
                    isDoubleJump ? 220f : 140f,
                    Math.Max(levelData.RangeY, Math.Max(levelData.Y, levelData.RangeBottom - levelData.RangeTop)));

                if (_player.Physics?.IsOnFoothold() == true)
                {
                    _player.Physics.Jump();
                }

                float currentVerticalVelocity = (float)(_player.Physics?.VelocityY ?? 0d);
                _player.Physics.SetVelocity(
                    rushSpeed * moveDirection,
                    Math.Min(
                        currentVerticalVelocity,
                        -Math.Max(jumpRushVerticalSpeed, HaCreator.MapSimulator.Physics.CVecCtrl.JumpVelocity * jumpPower)));
                return;
            }

            float verticalSpeed = isFlyingRush ? -60f : (float)_player.Physics.VelocityY;
            _player.Physics.SetVelocity(rushSpeed * moveDirection, verticalSpeed);
        }

        private void ExecuteBoundJumpMovement(
            SkillData skill,
            int level,
            SkillLevelData levelData,
            string movementActionName,
            float moveDirection,
            int horizontalRange)
        {
            if (TryResolveClientBoundJumpImpact(skill?.SkillId ?? 0, level, moveDirection >= 0f, out float clientImpactX, out float clientImpactY))
            {
                ApplyClientMovementImpact(clientImpactX, clientImpactY);
                return;
            }

            int clientScale = Math.Max(1, (level + 1) / 2);
            bool usesDoubleJumpArc = ActionTextContains(movementActionName, "doublejump")
                                     || ActionTextContains(movementActionName, "archerdoublejump")
                                     || ActionTextContains(movementActionName, "flash jump")
                                     || skill.SkillId == WildHunterJaguarJumpSkillId
                                     || skill.SkillId == 3101003;

            int defaultHorizontalDistance = usesDoubleJumpArc
                ? 250 + (20 * clientScale)
                : 350 + (40 * clientScale);
            int defaultVerticalImpulse = usesDoubleJumpArc
                ? 350 + (40 * clientScale)
                : 250 + (20 * clientScale);

            int authoredVerticalImpulse = Math.Max(
                levelData.RangeY,
                Math.Max(
                    levelData.Y,
                    Math.Max(
                        Math.Abs(levelData.RangeTop),
                        Math.Max(levelData.RangeBottom - levelData.RangeTop, horizontalRange / 2))));

            float jumpPower = (_player.Build?.JumpPower ?? 100) / 100f;
            float horizontalSpeed = Math.Max(defaultHorizontalDistance, horizontalRange) * 1.1f;
            float upwardSpeed = Math.Max(
                defaultVerticalImpulse,
                Math.Max(authoredVerticalImpulse, HaCreator.MapSimulator.Physics.CVecCtrl.JumpVelocity * jumpPower));

            ApplyClientMovementImpact(horizontalSpeed * moveDirection, -upwardSpeed);
        }

        private static bool TryResolveClientBoundJumpVelocity(
            int skillId,
            int level,
            bool facingRight,
            out float horizontalVelocity,
            out float upwardSpeed)
        {
            horizontalVelocity = 0f;
            upwardSpeed = 0f;

            int effectiveLevel = Math.Max(0, level);
            int scale;
            switch (skillId)
            {
                case WildHunterJaguarJumpSkillId:
                    // `CUserLocal::DoActiveSkill_BoundJump` uses the Wild Hunter Jaguar Jump
                    // branch's `nSLV / 4` growth table, not the steeper flash-jump fallback.
                    scale = effectiveLevel / 4;
                    horizontalVelocity = 250f + (20f * scale);
                    upwardSpeed = 350f + (40f * scale);
                    break;

                case WindWalkSkillId:
                    scale = effectiveLevel / 4;
                    horizontalVelocity = 350f + (40f * scale);
                    upwardSpeed = 250f + (20f * scale);
                    break;

                case NightLordFlashJumpSkillId:
                case ShadowerFlashJumpSkillId:
                case DualBladeFlashJumpSkillId:
                    scale = effectiveLevel / 4;
                    horizontalVelocity = 350f + (40f * scale);
                    upwardSpeed = 250f + (20f * scale);
                    break;

                case NightWalkerFlashJumpSkillId:
                    // The recovered `DoActiveSkill_BoundJump` branch lists Cygnus
                    // Flash Jump (`14101004`) with the same `nSLV / 4` growth row
                    // as the other directly-owned flash-jump skills.
                    scale = effectiveLevel / 4;
                    horizontalVelocity = 350f + (40f * scale);
                    upwardSpeed = 250f + (20f * scale);
                    break;

                default:
                    return false;
            }

            if (!facingRight)
            {
                horizontalVelocity = -horizontalVelocity;
            }

            return true;
        }

        internal static bool TryResolveClientBoundJumpImpact(
            int skillId,
            int level,
            bool facingRight,
            out float impactX,
            out float impactY)
        {
            impactX = 0f;
            impactY = 0f;
            if (!TryResolveClientBoundJumpVelocity(skillId, level, facingRight, out float horizontalVelocity, out float upwardSpeed))
            {
                return false;
            }

            impactX = horizontalVelocity;
            impactY = -upwardSpeed;
            return true;
        }

        private bool CanStartBoundJump(SkillData skill, int currentTime)
        {
            if (skill == null || _player?.Physics == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(GetStateRestrictionMessage(skill, currentTime)))
            {
                return false;
            }

            if (_player.Physics.IsOnLadderOrRope
                || (_player.Physics.IsFreeFalling() && _player.Physics.IsFalling()))
            {
                return false;
            }

            if (IsRocketBoosterSkill(skill))
            {
                return CanStartRocketBooster(skill, currentTime);
            }

            if (skill.SkillId == WildHunterJaguarJumpSkillId)
            {
                return _player.Physics.IsOnFoothold()
                       && !_player.Physics.IsInSwimArea
                       && !_player.Physics.IsUserFlying();
            }

            if (skill.SkillId == WindWalkSkillId)
            {
                return _player.Physics.IsOnFoothold();
            }

            if (skill.ClientInfoType == 40 || IsExplicitBoundJumpSkill(skill))
            {
                return RequiresAirborneBoundJumpStart(skill)
                    ? !_player.Physics.IsOnFoothold()
                    : _player.Physics.IsOnFoothold();
            }

            return true;
        }

        private void TryExecuteTeleportMasteryBodyAttack(SkillData teleportSkill, int currentTime)
        {
            if (teleportSkill == null)
            {
                return;
            }

            SkillData masterySkill = FindTeleportMasterySkill(teleportSkill.SkillId);
            if (masterySkill == null)
            {
                return;
            }

            int masteryLevel = GetSkillLevel(masterySkill.SkillId);
            if (masteryLevel <= 0)
            {
                return;
            }

            if (masterySkill.AttackType == SkillAttackType.Magic || masterySkill.IsMagicDamageSkill)
            {
                ProcessMagicAttack(masterySkill, masteryLevel, currentTime, _player.FacingRight, queueFollowUps: false);
                return;
            }

            ProcessMeleeAttack(masterySkill, masteryLevel, currentTime, _player.FacingRight, queueFollowUps: false);
        }

        private SkillData FindTeleportMasterySkill(int teleportSkillId)
        {
            if (teleportSkillId <= 0)
            {
                return null;
            }

            foreach (SkillData skill in _availableSkills)
            {
                if (skill?.UsesAffectedSkillBodyAttack != true || !skill.LinksAffectedSkill(teleportSkillId))
                {
                    continue;
                }

                return skill;
            }

            return null;
        }

        private void ExecuteRocketBoosterLaunch(
            SkillData skill,
            int level,
            int currentTime,
            bool facingRight,
            string startupActionName = null,
            float? storedVerticalLaunchSpeed = null)
        {
            SkillLevelData levelData = skill?.GetLevel(level);
            if (levelData == null)
            {
                return;
            }

            float upwardSpeed = storedVerticalLaunchSpeed.GetValueOrDefault();
            if (upwardSpeed <= 0f)
            {
                upwardSpeed = ResolveRocketBoosterLaunchVerticalSpeed(skill, level);
            }

            bool usesTankStartup = string.Equals(startupActionName, "tank_rbooster_pre", StringComparison.OrdinalIgnoreCase);
            string launchActionName = usesTankStartup ? "tank_rbooster_pre" : "rbooster_pre";
            int startupActionDurationMs = GetActionAnimationDurationMs(launchActionName);

            _player.FacingRight = facingRight;
            _player.Physics.FacingRight = facingRight;
            _player.ApplySkillAvatarTransform(skill.SkillId, launchActionName);
            ApplyClientMovementImpact(ResolveRocketBoosterImpactX(), ResolveRocketBoosterImpactY(upwardSpeed));
            _rocketBoosterState = new RocketBoosterState
            {
                Skill = skill,
                Level = level,
                FacingRight = facingRight,
                UsesTankStartup = usesTankStartup,
                StoredVerticalLaunchSpeed = upwardSpeed,
                LaunchStartTime = currentTime,
                StartupActionDurationMs = Math.Max(0, startupActionDurationMs)
            };
        }

        private void ApplyClientMovementImpact(float impactX, float impactY)
        {
            if (_player?.Physics == null)
            {
                return;
            }

            _player.Physics.Impact(impactX, impactY);
            if (impactY < 0f)
            {
                _player.Physics.CurrentAction = HaCreator.MapSimulator.Physics.MoveAction.Jump;
            }
        }

        private void ProcessDeferredSkillPayloads(int currentTime)
        {
            while (_deferredSkillPayloads.Count > 0)
            {
                DeferredSkillPayload pending = _deferredSkillPayloads.Peek();
                if (pending.ExecuteTime > currentTime)
                {
                    return;
                }

                if (pending.Skill == null)
                {
                    _deferredSkillPayloads.Dequeue();
                    continue;
                }

                if (ShouldUseSmoothingMovingShoot(pending.Skill)
                    && HasDueQueuedAttackTriggeredFollowUp(currentTime))
                {
                    return;
                }

                _deferredSkillPayloads.Dequeue();
                int pendingLevel = pending.Level;
                if (pending.RevalidateSkillLevelOnExecute)
                {
                    int liveLevel = ResolveQueuedExecutionSkillLevel(pending.Skill.SkillId);
                    if (ShouldUseSmoothingMovingShoot(pending.Skill)
                        && ShouldCancelDeferredMovingShootForSkillLevel(pending.Level, liveLevel))
                    {
                        continue;
                    }

                    if (liveLevel <= 0)
                    {
                        if (IsRocketBoosterSkill(pending.Skill))
                        {
                            CancelRocketBoosterState(playExitAction: false);
                        }

                        continue;
                    }

                    pendingLevel = liveLevel;
                }

                if (IsRocketBoosterSkill(pending.Skill))
                {
                    TryBeginRocketBoosterLaunch(
                        pending.Skill,
                        pendingLevel,
                        currentTime,
                        pending.FacingRight,
                        pending.ActionName,
                        pending.StoredVerticalLaunchSpeed);
                    continue;
                }

                int? preferredTargetMobId = pending.PreferredTargetMobId;
                if (ShouldUseSmoothingMovingShoot(pending.Skill))
                {
                    if (ShouldSuppressDeferredMovingShootExecution(pending.Skill, pending, currentTime))
                    {
                        continue;
                    }

                    preferredTargetMobId = ResolveDeferredExecutionPreferredTargetMobId(
                        pending.Skill,
                        pendingLevel,
                        pending,
                        currentTime,
                        pending.ShootRange0);
                    RegisterDeferredMovingShootExecution(pending, currentTime);
                }

                if (!TryRefreshDeferredQueuedShootAttackExecutionSelection(
                        pending,
                        pendingLevel,
                        out ShootAmmoSelection refreshedQueuedSelection))
                {
                    continue;
                }

                ShootAmmoSelection previousResolvedShootAmmoSelection = LastResolvedShootAmmoSelection?.Snapshot();
                bool? previousShootAmmoBypassOverride = _shootAmmoBypassTemporaryStatOverride;
                try
                {
                    LastResolvedShootAmmoSelection = refreshedQueuedSelection?.Snapshot();
                    _shootAmmoBypassTemporaryStatOverride = pending.ShootAmmoBypassActive;
                    ExecuteSkillPayload(
                        pending.Skill,
                        pendingLevel,
                        currentTime,
                        pending.QueueFollowUps,
                        preferredTargetMobId,
                        pending.PreferredTargetPosition,
                        allowDeferredExecution: false,
                        revalidateDeferredExecutionSkillLevel: false,
                        isQueuedFinalAttack: pending.IsQueuedFinalAttack,
                        isQueuedSparkAttack: pending.IsQueuedSparkAttack,
                        facingRightOverride: pending.FacingRight,
                        attackOriginOverride: pending.AttackOrigin,
                        shootRange0Override: pending.ShootRange0,
                        currentActionNameOverride: pending.ActionName,
                        currentRawActionCodeOverride: pending.RawActionCode);
                }
                finally
                {
                    LastResolvedShootAmmoSelection = previousResolvedShootAmmoSelection;
                    _shootAmmoBypassTemporaryStatOverride = previousShootAmmoBypassOverride;
                }
            }
        }

        private bool HasDueQueuedAttackTriggeredFollowUp(int currentTime)
        {
            if ((_currentCast != null && !_currentCast.IsComplete) || _preparedSkill != null)
            {
                return false;
            }

            return HasDueQueuedFollowUpAttack(currentTime)
                   || HasDueQueuedSerialAttack(currentTime)
                   || HasDueQueuedSparkAttack(currentTime);
        }

        private bool HasDueQueuedFollowUpAttack(int currentTime)
        {
            return _queuedFollowUpAttacks.Count > 0 && _queuedFollowUpAttacks.Peek().ExecuteTime <= currentTime;
        }

        private bool HasDueQueuedSparkAttack(int currentTime)
        {
            return _queuedSparkAttack != null && _queuedSparkAttack.ExecuteTime <= currentTime;
        }

        private bool HasDueQueuedSerialAttack(int currentTime)
        {
            return _queuedSerialAttack != null && _queuedSerialAttack.ExecuteTime <= currentTime;
        }

        private int? ResolveDeferredExecutionPreferredTargetMobId(
            SkillData skill,
            int level,
            DeferredSkillPayload pending,
            int currentTime,
            int shootRange0Override = 0)
        {
            if (_mobPool == null || skill == null)
            {
                return pending?.PreferredTargetMobId;
            }

            MobItem preferredTarget = TryGetAttackableMobById(pending?.PreferredTargetMobId);
            if (preferredTarget != null)
            {
                return preferredTarget.PoolId;
            }

            if (pending?.PreferredTargetPosition.HasValue != true)
            {
                return pending?.PreferredTargetMobId;
            }

            SkillLevelData levelData = skill.GetLevel(level);
            if (levelData == null)
            {
                return pending.PreferredTargetMobId;
            }

            AttackResolutionMode mode = ResolveDeferredTargetingMode(skill);
            int? resolvedTargetMobId = ResolveDeferredPreferredTargetMobIdForStoredPosition(
                skill,
                level,
                levelData,
                currentTime,
                pending.FacingRight,
                pending.AttackOrigin,
                pending.PreferredTargetPosition.Value,
                mode,
                shootRange0Override,
                pending.ActionName,
                pending.RawActionCode);
            if (resolvedTargetMobId.HasValue)
            {
                return resolvedTargetMobId;
            }

            if (mode == AttackResolutionMode.Melee && ShouldTryMeleeBeforeRangedFallback(skill))
            {
                return ResolveDeferredPreferredTargetMobIdForStoredPosition(
                    skill,
                    level,
                    levelData,
                    currentTime,
                    pending.FacingRight,
                    pending.AttackOrigin,
                    pending.PreferredTargetPosition.Value,
                    AttackResolutionMode.Ranged,
                    shootRange0Override,
                    pending.ActionName,
                    pending.RawActionCode);
            }

            return pending.PreferredTargetMobId;
        }

        private int? ResolveDeferredPreferredTargetMobIdForStoredPosition(
            SkillData skill,
            int level,
            SkillLevelData levelData,
            int currentTime,
            bool facingRight,
            Vector2? attackOriginOverride,
            Vector2 preferredTargetPosition,
            AttackResolutionMode mode,
            int shootRange0Override = 0,
            string currentActionNameOverride = null,
            int? currentRawActionCodeOverride = null)
        {
            Rectangle worldHitbox = GetWorldAttackHitbox(
                skill,
                level,
                levelData,
                mode,
                facingRight,
                attackOriginOverride,
                shootRange0Override,
                currentActionNameOverride,
                currentRawActionCodeOverride);
            if (worldHitbox.Width <= 0 || worldHitbox.Height <= 0 || _mobPool == null)
            {
                return null;
            }

            return _mobPool.ActiveMobs
                .Where(IsMobAttackable)
                .Select(mob => new { Mob = mob, Hitbox = GetMobHitbox(mob, currentTime) })
                .Where(entry => !entry.Hitbox.IsEmpty && worldHitbox.Intersects(entry.Hitbox))
                .Select(entry =>
                {
                    Vector2 center = new(
                        entry.Hitbox.Left + (entry.Hitbox.Width * 0.5f),
                        entry.Hitbox.Top + (entry.Hitbox.Height * 0.5f));
                    return new
                    {
                        entry.Mob,
                        Distance = Vector2.DistanceSquared(center, preferredTargetPosition)
                    };
                })
                .OrderBy(entry => entry.Distance)
                .Select(entry => (int?)entry.Mob.PoolId)
                .FirstOrDefault();
        }

        private MobItem TryGetAttackableMobById(int? mobId)
        {
            if (!mobId.HasValue || _mobPool == null)
            {
                return null;
            }

            MobItem mob = _mobPool.ActiveMobs.FirstOrDefault(candidate => candidate?.PoolId == mobId.Value);
            return IsMobAttackable(mob) ? mob : null;
        }

        private bool ShouldSuppressDeferredMovingShootExecution(SkillData skill, DeferredSkillPayload pending, int currentTime)
        {
            if (skill == null || pending == null || skill.SkillId == 33121009 || _lastDeferredMovingShootExecution == null)
            {
                return false;
            }

            if (_lastDeferredMovingShootExecution.SkillId != skill.SkillId)
            {
                return false;
            }

            if (!MatchesDeferredMovingShootExecutionMetadata(
                    pending.AttackActionType,
                    _lastDeferredMovingShootExecution.AttackActionType)
                || !MatchesDeferredMovingShootExecutionMetadata(
                    pending.ActionSpeed,
                    _lastDeferredMovingShootExecution.ActionSpeed)
                || !MatchesDeferredMovingShootActionOwner(
                    pending.ActionName,
                    pending.RawActionCode,
                    _lastDeferredMovingShootExecution.ActionName,
                    _lastDeferredMovingShootExecution.RawActionCode))
            {
                return false;
            }

            int antiRepeatWindow = GetMovingShootDelayMs(skill);
            if (antiRepeatWindow <= 0
                || currentTime - _lastDeferredMovingShootExecution.ExecuteTime >= antiRepeatWindow)
            {
                return false;
            }

            Point liveWorldPosition = ResolveDeferredMovingShootLiveWorldPosition();
            if (!IsWithinMovingShootAntiRepeatThreshold(
                _lastDeferredMovingShootExecution.LiveWorldPosition,
                liveWorldPosition))
            {
                return false;
            }

            if (_lastDeferredMovingShootExecution.RepeatCount < _lastDeferredMovingShootExecution.CountLimit)
            {
                _lastDeferredMovingShootExecution.RepeatCount++;
                return false;
            }

            return true;
        }

        internal static bool ShouldCancelDeferredMovingShootForSkillLevel(int queuedLevel, int liveLevel)
        {
            // `CUserLocal::TryDoingSmoothingMovingShootAttack` drops the deferred shot
            // when the queued `m_movingShootEntry.nSLV` no longer matches the live
            // learned level at execution time.
            return liveLevel <= 0 || liveLevel != queuedLevel;
        }

        private void RegisterDeferredMovingShootExecution(DeferredSkillPayload pending, int currentTime)
        {
            if (pending == null)
            {
                return;
            }

            _lastDeferredMovingShootExecution = new DeferredMovingShootExecutionState
            {
                SkillId = pending.Skill?.SkillId ?? 0,
                ExecuteTime = currentTime,
                LiveWorldPosition = ResolveDeferredMovingShootLiveWorldPosition(),
                ActionName = pending.ActionName,
                RawActionCode = pending.RawActionCode,
                AttackActionType = pending.AttackActionType,
                ActionSpeed = pending.ActionSpeed,
                RepeatCount = 0,
                CountLimit = ResolveMovingShootAntiRepeatCountLimit(pending)
            };
        }

        private static bool MatchesDeferredMovingShootExecutionMetadata(int? queuedValue, int? previousValue)
        {
            if (!queuedValue.HasValue || !previousValue.HasValue)
            {
                return true;
            }

            return queuedValue.Value == previousValue.Value;
        }

        internal static bool MatchesDeferredMovingShootActionOwner(
            string queuedActionName,
            int? queuedRawActionCode,
            string previousActionName,
            int? previousRawActionCode)
        {
            string normalizedQueuedOwner = NormalizeDeferredMovingShootActionOwnerName(queuedActionName, queuedRawActionCode);
            string normalizedPreviousOwner = NormalizeDeferredMovingShootActionOwnerName(previousActionName, previousRawActionCode);
            if (string.IsNullOrWhiteSpace(normalizedQueuedOwner) || string.IsNullOrWhiteSpace(normalizedPreviousOwner))
            {
                return true;
            }

            return string.Equals(normalizedQueuedOwner, normalizedPreviousOwner, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeDeferredMovingShootActionOwnerName(string actionName, int? rawActionCode)
        {
            if (rawActionCode.HasValue
                && CharacterPart.TryGetActionStringFromCode(rawActionCode.Value, out string mappedActionName)
                && !string.IsNullOrWhiteSpace(mappedActionName))
            {
                return mappedActionName;
            }

            return string.IsNullOrWhiteSpace(actionName)
                ? null
                : actionName.Trim();
        }

        internal static int ResolveMovingShootAntiRepeatCountLimit(SkillData skill)
        {
            // `CUserLocal::TryDoingSmoothingMovingShootAttack` bypasses `CAntiRepeat::TryRepeat`
            // entirely for skill 33121009 before the repeated-position gate runs. WZ still
            // authors that skill as `info/movingAttack=1` plus `info/rapidAttack=1`, but the
            // confirmed client bypass is skill-owned rather than a generic rapid-attack rule.
            return skill?.SkillId == ClientMovingShootAntiRepeatBypassSkillId
                ? int.MaxValue
                : skill?.IsRapidAttack == true
                    ? 1
                    : 0;
        }

        private static int ResolveMovingShootAntiRepeatCountLimit(DeferredSkillPayload pending)
        {
            return ResolveMovingShootAntiRepeatCountLimit(pending?.Skill);
        }

        private Point ResolveDeferredMovingShootLiveWorldPosition()
        {
            return new Point(
                (int)MathF.Round(_player?.X ?? 0f),
                (int)MathF.Round(_player?.Y ?? 0f));
        }

        private static bool IsWithinMovingShootAntiRepeatThreshold(Point previousPosition, Point currentPosition)
        {
            // `CAntiRepeat::TryRepeat` blocks repeated delayed moving-shot execution while
            // the live VecCtrl world position stays within +/-5 px horizontally and +/-149 px vertically.
            int deltaX = previousPosition.X - currentPosition.X;
            int deltaY = previousPosition.Y - currentPosition.Y;
            return deltaX > -MovingShootAntiRepeatHorizontalThresholdPx
                   && deltaX < MovingShootAntiRepeatHorizontalThresholdPx
                   && deltaY > -MovingShootAntiRepeatVerticalThresholdPx
                   && deltaY < MovingShootAntiRepeatVerticalThresholdPx;
        }

        private void ProcessPendingProjectileSpawns(int currentTime)
        {
            if (_pendingProjectileSpawns.Count == 0)
            {
                return;
            }

            while (_pendingProjectileSpawns.Count > 0)
            {
                PendingProjectileSpawn pending = _pendingProjectileSpawns[0];
                if (pending.ExecuteTime > currentTime)
                {
                    return;
                }

                _pendingProjectileSpawns.RemoveAt(0);
                if (pending.Projectile != null)
                {
                    RefreshQueuedProjectileFireTimeOrigin(pending.Projectile, currentTime);
                    if (!TryRefreshQueuedProjectileFireTimeMetadata(pending.Projectile))
                    {
                        continue;
                    }

                    _projectiles.Add(pending.Projectile);
                }
            }
        }

        private void RefreshQueuedProjectileFireTimeOrigin(ActiveProjectile projectile, int currentTime)
        {
            if (_player == null || projectile == null)
            {
                return;
            }

            SkillData skill = GetSkillData(projectile.SkillId);
            if (!ShouldRefreshQueuedProjectileFireTimeOrigin(projectile, skill))
            {
                return;
            }

            Vector2 refreshedOrigin = ResolveProjectileAttackOrigin(
                currentTime,
                ResolveQueuedProjectileLaunchFacing(projectile),
                attackOriginOverride: null,
                out bool usesAuthoredShootPoint);
            ApplyQueuedProjectileFireTimeOrigin(
                projectile,
                refreshedOrigin,
                usesAuthoredShootPoint,
                ResolveClientFallbackShootAttackPointYOffset(skill));

            float speed = GetProjectileSpeed(projectile.Data, projectile.LevelData);
            TryAimProjectileAtTarget(projectile, currentTime, speed);
        }

        internal static bool ShouldRefreshQueuedProjectileFireTimeOrigin(ActiveProjectile projectile, SkillData skill)
        {
            return projectile != null
                   && !projectile.HasExplicitAttackOriginOverride
                   && (projectile.IsQueuedFinalAttack || projectile.IsQueuedSparkAttack)
                   && skill?.IsAttack == true
                   && RequiresClientShootAttackValidation(skill);
        }

        internal static void ApplyQueuedProjectileFireTimeOrigin(
            ActiveProjectile projectile,
            Vector2 refreshedOrigin,
            bool usesAuthoredShootPoint,
            float fallbackShootPointYOffset = -20f)
        {
            if (projectile == null)
            {
                return;
            }

            Vector2 preservedOwnerRelativeOffset = ResolveQueuedProjectileOwnerRelativeLaunchOffset(projectile);
            float projectileBaseY = ResolveProjectileSpawnY(
                refreshedOrigin.Y,
                usesAuthoredShootPoint,
                fallbackShootPointYOffset);

            projectile.UsesAuthoredShootPoint = usesAuthoredShootPoint;
            projectile.FallbackShootPointYOffset = fallbackShootPointYOffset;
            projectile.OwnerX = refreshedOrigin.X;
            projectile.OwnerY = refreshedOrigin.Y;
            projectile.X = refreshedOrigin.X + preservedOwnerRelativeOffset.X;
            projectile.Y = projectileBaseY + preservedOwnerRelativeOffset.Y;
            projectile.PreviousX = projectile.X;
            projectile.PreviousY = projectile.Y;
        }

        internal static Vector2 ResolveQueuedProjectileOwnerRelativeLaunchOffset(ActiveProjectile projectile)
        {
            if (projectile == null)
            {
                return Vector2.Zero;
            }

            float ownerRelativeX = projectile.X - projectile.OwnerX;
            float ownerRelativeY = projectile.Y - projectile.OwnerY;
            if (!projectile.UsesAuthoredShootPoint)
            {
                ownerRelativeY -= projectile.FallbackShootPointYOffset;
            }

            return new Vector2(ownerRelativeX, ownerRelativeY);
        }

        internal static bool DoesQueuedFollowUpWeaponMatch(int requiredWeaponCode, int equippedWeaponCode)
        {
            return requiredWeaponCode <= 0 || equippedWeaponCode == requiredWeaponCode;
        }

        private bool TryRefreshQueuedProjectileFireTimeMetadata(ActiveProjectile projectile)
        {
            if (projectile == null)
            {
                return false;
            }

            if ((!projectile.IsQueuedFinalAttack && !projectile.IsQueuedSparkAttack)
                || _player?.Build == null)
            {
                return true;
            }

            SkillData skill = GetSkillData(projectile.SkillId);
            if (!RequiresClientShootAttackValidation(skill))
            {
                return true;
            }

            int liveWeaponCode = GetEquippedWeaponCode();
            int liveWeaponItemId = _player.Build.GetWeapon()?.ItemId ?? 0;
            if (!TryResolveQueuedProjectileFireTimeWeaponContext(
                    projectile,
                    liveWeaponCode,
                    liveWeaponItemId,
                    out int effectiveWeaponCode,
                    out int effectiveWeaponItemId))
            {
                projectile.ResolvedShootAmmoSelection = null;
                return false;
            }

            projectile.ResolvedShootWeaponCode = effectiveWeaponCode;
            projectile.ResolvedShootWeaponItemId = effectiveWeaponItemId;

            if (projectile.ResolvedShootAmmoSelection == null
                || _inventoryRuntime == null
                || IsShootSkillNotUsingShootingWeapon(skill.SkillId)
                || IsShootSkillNotConsumingBullet(skill.SkillId))
            {
                return true;
            }

            bool resolved = TryRefreshQueuedProjectileFireTimeSelectionForExecution(
                projectile,
                _inventoryRuntime.GetSlots(InventoryType.USE),
                _inventoryRuntime.GetSlots(InventoryType.CASH),
                effectiveWeaponCode,
                effectiveWeaponItemId,
                ResolveRequiredShootAmmoCount(projectile.LevelData),
                ResolveSkillSpecificShootAmmoItemId(skill.SkillId, projectile.LevelData),
                out ShootAmmoSelection refreshedSelection);
            projectile.ResolvedShootAmmoSelection = refreshedSelection;
            return resolved;
        }

        internal static bool TryResolveQueuedProjectileFireTimeWeaponContext(
            ActiveProjectile projectile,
            int liveWeaponCode,
            int liveWeaponItemId,
            out int effectiveWeaponCode,
            out int effectiveWeaponItemId)
        {
            effectiveWeaponCode = projectile?.ResolvedShootWeaponCode > 0
                ? projectile.ResolvedShootWeaponCode
                : liveWeaponCode;
            effectiveWeaponItemId = projectile?.ResolvedShootWeaponItemId > 0
                ? projectile.ResolvedShootWeaponItemId
                : liveWeaponItemId;
            return effectiveWeaponCode > 0 && effectiveWeaponItemId > 0;
        }

        internal static bool TryRefreshQueuedProjectileFireTimeSelectionForExecution(
            ActiveProjectile projectile,
            IReadOnlyList<InventorySlotData> useSlots,
            IReadOnlyList<InventorySlotData> cashSlots,
            int liveWeaponCode,
            int liveWeaponItemId,
            int requiredAmmoCount,
            int requiredSkillAmmoItemId,
            out ShootAmmoSelection refreshedSelection)
        {
            refreshedSelection = projectile?.ResolvedShootAmmoSelection?.Snapshot();
            if (projectile == null
                || (!projectile.IsQueuedFinalAttack && !projectile.IsQueuedSparkAttack))
            {
                return true;
            }

            if (projectile?.ResolvedShootAmmoSelection == null
                || liveWeaponCode <= 0
                || liveWeaponItemId <= 0)
            {
                return false;
            }

            return ClientShootAmmoResolver.TryRefreshQueuedSelectionForExecution(
                projectile.ResolvedShootAmmoSelection,
                useSlots,
                cashSlots,
                liveWeaponCode,
                liveWeaponItemId,
                requiredAmmoCount,
                requiredSkillAmmoItemId,
                requiresUseAmmo: true,
                out refreshedSelection);
        }

        private void UpdateRocketBooster(int currentTime)
        {
            if (_rocketBoosterState == null)
            {
                return;
            }

            _player.FacingRight = _rocketBoosterState.FacingRight;
            _player.Physics.FacingRight = _rocketBoosterState.FacingRight;

            if (!CanMaintainRocketBooster(_rocketBoosterState.Skill, currentTime))
            {
                CancelRocketBoosterState(playExitAction: _player?.IsAlive == true);
                return;
            }

            if (_rocketBoosterState.LandingAttackTime != int.MinValue)
            {
                int landingRecoveryElapsed = currentTime - _rocketBoosterState.LandingAttackTime;
                if (!_rocketBoosterState.RecoveryEffectRequested
                    && landingRecoveryElapsed >= ROCKET_BOOSTER_LANDING_RECOVERY_MS)
                {
                    OnClientSkillEffectRequested?.Invoke(ROCKET_BOOSTER_RECOVERY_EFFECT_SKILL_ID, _rocketBoosterState.Skill.SkillId);
                    _rocketBoosterState.RecoveryEffectRequested = true;
                }

                if (landingRecoveryElapsed >= Math.Max(
                        ROCKET_BOOSTER_LANDING_RECOVERY_MS,
                        _rocketBoosterState.RecoveryDurationMs))
                {
                    CancelRocketBoosterState(playExitAction: false);
                }

                return;
            }

            if (!HasRocketBoosterStartupActionCompleted(_rocketBoosterState, currentTime))
            {
                return;
            }

            // The client keeps `m_nRocketBoosterVY` armed until the startup one-time action
            // finishes, then clears it on the next upkeep tick before touchdown attack
            // resolution can begin.
            if (!_rocketBoosterState.StoredLaunchMarkerCleared)
            {
                _rocketBoosterState.StoredLaunchMarkerCleared = true;
                return;
            }

            if (_player.Physics?.IsOnFoothold() != true)
            {
                return;
            }

            SkillLevelData levelData = _rocketBoosterState.Skill.GetLevel(_rocketBoosterState.Level);
            if (levelData != null)
            {
                SpawnHitEffect(_rocketBoosterState.Skill, _player.X, _player.Y, currentTime, _rocketBoosterState.FacingRight);
                ProcessMeleeAttack(
                    _rocketBoosterState.Skill,
                    _rocketBoosterState.Level,
                    currentTime,
                    _rocketBoosterState.FacingRight,
                    queueFollowUps: false,
                    preferredTargetMobId: null);
            }

            int exitActionDurationMs = GetRocketBoosterExitActionDurationMs(_rocketBoosterState);
            _player.ClearSkillAvatarTransform(_rocketBoosterState.Skill.SkillId);
            _rocketBoosterState.RecoveryDurationMs = exitActionDurationMs;
            _rocketBoosterState.RecoveryEffectRequested = false;
            _rocketBoosterState.LandingAttackTime = currentTime;
        }

        private bool CanStartRocketBooster(SkillData skill, int currentTime)
        {
            if (skill == null || _player?.Physics == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(GetStateRestrictionMessage(skill, currentTime)))
            {
                return false;
            }

            if (!_player.Physics.IsOnFoothold()
                || _player.Physics.IsOnLadderOrRope
                || (_player.Physics.IsFreeFalling() && _player.Physics.IsFalling())
                || _player.Physics.IsInSwimArea
                || _player.Physics.IsUserFlying())
            {
                return false;
            }

            return (_fieldSkillRestrictionEvaluator == null || _fieldSkillRestrictionEvaluator(skill))
                   && ResolveRocketBoosterLaunchVerticalSpeed(skill, GetSkillLevel(skill.SkillId)) > 0f;
        }

        private bool CanMaintainRocketBooster(SkillData skill, int currentTime)
        {
            if (skill == null || _player == null)
            {
                return false;
            }

            if (!_player.IsAlive || !_player.CanAttack)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(GetStateRestrictionMessage(skill, currentTime)))
            {
                return false;
            }

            return _fieldSkillRestrictionEvaluator == null || _fieldSkillRestrictionEvaluator(skill);
        }

        private void CancelRocketBoosterState(bool playExitAction)
        {
            const int rocketBoosterSkillId = ROCKET_BOOSTER_SKILL_ID;

            if (playExitAction)
            {
                _player?.ClearSkillAvatarTransform(rocketBoosterSkillId);
            }
            else if (_player?.HasSkillAvatarTransform(rocketBoosterSkillId) == true)
            {
                _player.ClearSkillAvatarTransform();
            }

            ClearSkillMount(rocketBoosterSkillId);
            _rocketBoosterState = null;
        }

        private void StartCyclone(SkillData skill, int level, int currentTime)
        {
            SkillLevelData levelData = skill?.GetLevel(level);
            if (levelData == null)
            {
                return;
            }

            int durationMs = Math.Max(CYCLONE_ATTACK_INTERVAL_MS, levelData.Time > 0 ? levelData.Time * 1000 : 0);
            _cycloneState = new CycloneState
            {
                Skill = skill,
                Level = level,
                ExpireTime = currentTime + durationMs,
                NextAttackTime = currentTime + CYCLONE_ATTACK_INTERVAL_MS
            };

            RegisterClientSkillTimer(
                skill.SkillId,
                ClientTimerSourceCycloneExpire,
                _cycloneState.ExpireTime,
                StopCycloneFromClientTimer);
            _player.BeginSustainedSkillAnimation(ResolveSkillActionName(skill));
        }

        private void UpdateCyclone(int currentTime)
        {
            if (_cycloneState == null)
            {
                return;
            }

            if (!_player.IsAlive
                || !_player.CanAttack
                || !string.IsNullOrWhiteSpace(GetStateRestrictionMessage(_cycloneState.Skill, currentTime))
                || (_fieldSkillRestrictionEvaluator != null && !_fieldSkillRestrictionEvaluator(_cycloneState.Skill))
                || !IsCycloneActionActive())
            {
                StopCyclone();
                return;
            }

            while (_cycloneState != null && currentTime >= _cycloneState.NextAttackTime)
            {
                _player.BeginSustainedSkillAnimation(ResolveSkillActionName(_cycloneState.Skill));
                ProcessMeleeAttack(_cycloneState.Skill, _cycloneState.Level, currentTime, _player.FacingRight, queueFollowUps: false);
                _cycloneState.NextAttackTime += CYCLONE_ATTACK_INTERVAL_MS;
            }
        }

        private void StopCycloneFromClientTimer(int currentTime)
        {
            StopCyclone();
        }

        private void StopCyclone()
        {
            if (_cycloneState == null)
            {
                return;
            }

            CancelClientSkillTimers(_cycloneState.Skill?.SkillId ?? 0, ClientTimerSourceCycloneExpire);
            _cycloneState = null;
            _player.EndSustainedSkillAnimation();
        }

        private float ResolveRocketBoosterLaunchVerticalSpeed(SkillData skill, int level)
        {
            if (skill == null)
            {
                return 260f;
            }

            SkillLevelData levelData = skill.GetLevel(level);
            if (TryResolveClientRocketBoosterLaunchSpeed(level, out float clientUpwardSpeed))
            {
                return clientUpwardSpeed;
            }

            if (levelData == null)
            {
                return 260f;
            }

            int horizontalRange = Math.Max(
                Math.Max(levelData.RangeR, levelData.RangeL),
                Math.Max(levelData.Range, Math.Max(levelData.X, levelData.Y)));

            if (horizontalRange <= 0)
            {
                horizontalRange = 160;
            }

            return Math.Max(260f, Math.Abs(levelData.RangeTop) * 3f);
        }

        internal static bool TryResolveClientRocketBoosterLaunchSpeed(int level, out float upwardSpeed)
        {
            int scale = Math.Max(0, level) / 4;
            upwardSpeed = 250f + (20f * scale);
            return upwardSpeed > 0f;
        }

        internal static float ResolveRocketBoosterImpactX()
        {
            return 0f;
        }

        internal static float ResolveRocketBoosterImpactY(float upwardSpeed)
        {
            return -Math.Max(0f, upwardSpeed);
        }

        private static bool HasRocketBoosterStartupActionCompleted(RocketBoosterState state, int currentTime)
        {
            if (state == null || state.StartupActionDurationMs <= 0)
            {
                return true;
            }

            return currentTime - state.LaunchStartTime >= state.StartupActionDurationMs;
        }

        private int GetRocketBoosterLaunchDelayMs(SkillData skill, string startupActionName = null)
        {
            int actionDuration = GetActionAnimationDurationMs(startupActionName);
            if (actionDuration > 0)
            {
                return Math.Max(120, actionDuration);
            }

            return Math.Max(120, GetActionAnimationDurationMs(skill));
        }

        private int GetMovingShootDelayMs(SkillData skill)
        {
            int delay = 0;

            if (skill?.ClientDelayMs > 0)
            {
                delay = skill.ClientDelayMs;
            }

            if (skill?.PrepareDurationMs > 0)
            {
                delay = Math.Max(delay, skill.PrepareDurationMs);
            }

            int movementActionDuration = GetMovementActionDurationMs(skill);
            if (movementActionDuration > 0)
            {
                delay = Math.Max(delay, movementActionDuration);
            }

            int fallbackDelay = skill?.Effect?.Frames?.Count > 0
                ? skill.Effect.Frames[0].Delay
                : GetActionAnimationLeadDelayMs(skill);
            delay = Math.Max(delay, fallbackDelay);

            return Math.Max(60, delay <= 0 ? 90 : delay);
        }

        private int GetMovementActionDurationMs(SkillData skill)
        {
            if (_player.Assembler == null)
            {
                return 0;
            }

            int duration = 0;
            foreach (string actionName in EnumerateMovementActionCandidates(skill))
            {
                var animation = _player.Assembler.GetAnimation(actionName);
                if (animation == null || animation.Length == 0)
                {
                    continue;
                }

                int actionDuration = 0;
                foreach (var frame in animation)
                {
                    actionDuration += frame.Duration;
                }

                duration = Math.Max(duration, actionDuration);
            }

            return duration;
        }

        private int GetActionAnimationLeadDelayMs(SkillData skill)
        {
            if (_player.Assembler == null)
            {
                return 0;
            }

            string actionName = ResolveSkillActionName(skill);
            var animation = _player.Assembler.GetAnimation(actionName);
            if (animation == null || animation.Length == 0)
            {
                return 0;
            }

            return animation[0].Duration;
        }

        private int GetActionAnimationDurationMs(SkillData skill)
        {
            if (_player.Assembler == null)
            {
                return 0;
            }

            string actionName = ResolveSkillActionName(skill);
            var animation = _player.Assembler.GetAnimation(actionName);
            if (animation == null || animation.Length == 0)
            {
                return 0;
            }

            int duration = 0;
            foreach (var frame in animation)
            {
                duration += frame.Duration;
            }

            return duration;
        }

        private int GetActionAnimationDurationMs(string actionName)
        {
            if (_player.Assembler == null || string.IsNullOrWhiteSpace(actionName))
            {
                return 0;
            }

            var animation = _player.Assembler.GetAnimation(actionName);
            if (animation == null || animation.Length == 0)
            {
                return 0;
            }

            int duration = 0;
            foreach (var frame in animation)
            {
                duration += frame.Duration;
            }

            return duration;
        }

        private static bool IsRocketBoosterSkill(SkillData skill)
        {
            return skill?.SkillId == ROCKET_BOOSTER_SKILL_ID;
        }

        private string ResolveRocketBoosterStartupActionName()
        {
            return _player?.HasSkillAvatarTransform(RepeatSkillTankModeId) == true
                   || string.Equals(_player?.CurrentActionName, "tank_rbooster_pre", StringComparison.OrdinalIgnoreCase)
                ? "tank_rbooster_pre"
                : "rbooster_pre";
        }

        private int GetRocketBoosterExitActionDurationMs(RocketBoosterState state)
        {
            return GetActionAnimationDurationMs(ResolveRocketBoosterExitActionName(state));
        }

        private static string ResolveRocketBoosterExitActionName(RocketBoosterState state)
        {
            return state?.UsesTankStartup == true
                ? "tank_rbooster_after"
                : "rbooster_after";
        }

        private static bool IsExplicitBoundJumpSkill(SkillData skill)
        {
            return skill != null
                   && (skill.SkillId == WindWalkSkillId
                       || skill.SkillId == NightLordFlashJumpSkillId
                       || skill.SkillId == ShadowerFlashJumpSkillId
                       || skill.SkillId == DualBladeFlashJumpSkillId);
        }

        internal static bool RequiresAirborneBoundJumpStart(SkillData skill)
        {
            if (skill == null)
            {
                return false;
            }

            if (skill.SkillId == WindWalkSkillId
                || skill.SkillId == WildHunterJaguarJumpSkillId
                || skill.SkillId == ROCKET_BOOSTER_SKILL_ID)
            {
                return false;
            }

            if (skill.ClientInfoType == 40)
            {
                return skill.AvailableInJumpingState;
            }

            return IsExplicitBoundJumpSkill(skill);
        }

        internal static bool AllowsClientOwnedOneTimeActionDuringSmoothingMovingShootPrepare(SkillData skill)
        {
            // `TryDoingSmoothingMovingShootAttackPrepare` only bypasses the active one-time
            // action gate for client skill type 3 before it clears the current clip and arms
            // the delayed moving-shot entry.
            return skill?.ClientInfoType == 3
                   && ShouldUseSmoothingMovingShoot(skill);
        }

        private static bool ShouldUseSmoothingMovingShoot(SkillData skill)
        {
            return skill?.CasterMove == true
                   && skill.IsAttack
                   && !skill.IsMovement
                   && !IsRocketBoosterSkill(skill)
                   && (skill.Projectile != null || skill.AttackType == SkillAttackType.Ranged);
        }

        private bool HasPendingDeferredMovingShootPayload(int currentTime)
        {
            if (_deferredSkillPayloads.Count == 0)
            {
                return false;
            }

            foreach (DeferredSkillPayload payload in _deferredSkillPayloads)
            {
                if (payload?.ExecuteTime > currentTime
                    && ShouldUseSmoothingMovingShoot(payload.Skill))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool SkillTextContains(SkillData skill, string value)
        {
            return skill?.ActionName?.Contains(value, StringComparison.OrdinalIgnoreCase) == true
                   || skill?.Name?.Contains(value, StringComparison.OrdinalIgnoreCase) == true;
        }

        private static bool IsSwallowSkill(SkillData skill)
        {
            return skill?.IsSwallowFamilySkill == true;
        }

        #endregion

        #region Basic Attack Methods (TryDoingMeleeAttack, TryDoingShoot, TryDoingMagicAttack)

        /// <summary>
        /// TryDoingMeleeAttack - Close range attack (matching CUserLocal::TryDoingMeleeAttack from client)
        /// </summary>
        public bool TryDoingMeleeAttack(int currentTime)
        {
            // Cannot attack while on ladder/rope/swimming
            if (!_player.CanAttack)
                return false;

            if (TryExecuteRepeatSkillNormalAttack(currentTime))
                return true;

            if (_mobPool == null)
                return false;

            // Trigger attack animation
            _player.TriggerSkillAnimation("swingO1");

            // Define melee hitbox (relative to player facing)
            int hitWidth = 80;
            int hitHeight = 60;
            int offsetX = _player.FacingRight ? 10 : -(hitWidth + 10);

            var worldHitbox = new Rectangle(
                (int)_player.X + offsetX,
                (int)_player.Y - hitHeight - 10,
                hitWidth,
                hitHeight);
            NotifyAttackAreaResolved(worldHitbox, currentTime, skillId: 0);

            int hitCount = 0;
            int maxTargets = 3; // Can hit up to 3 mobs
            var mobsToKill = new List<MobItem>();

            foreach (var mob in _mobPool.ActiveMobs)
            {
                if (hitCount >= maxTargets)
                    break;

                // Skip dead or removed mobs
                if (mob?.AI == null || mob.AI.IsDead)
                    continue;

                var mobHitbox = GetMobHitbox(mob);
                if (!worldHitbox.Intersects(mobHitbox))
                    continue;

                // Calculate basic melee damage
                int damage = CalculateBasicDamage();

                bool died = mob.ApplyDamage(damage, currentTime, damage > 100, _player.X, _player.Y, damageType: MobDamageType.Physical);
                ApplyMobReflectDamage(mob, currentTime, MobDamageType.Physical);
                RecordOwnerAttackTarget(mob, currentTime);
                RegisterEnergyChargeHit(currentTime);

                // Apply knockback effect if mob didn't die
                if (!died && mob.MovementInfo != null)
                {
                    float knockbackForce = 6f + (damage / 50f); // Scale knockback with damage
                    knockbackForce = Math.Min(knockbackForce, 12f); // Cap at 12
                    bool knockRight = _player.FacingRight; // Knock away from player
                    mob.MovementInfo.ApplyKnockback(knockbackForce, knockRight);
                }

                if (_combatEffects != null)
                {
                    Vector2 damageAnchor = mob.GetDamageNumberAnchor();

                    // Notify HP bar system
                    _combatEffects.OnMobDamaged(mob, currentTime);

                    _combatEffects.AddDamageNumber(
                        damage,
                        damageAnchor.X,
                        damageAnchor.Y,
                        damage > 100,
                        false,
                        currentTime,
                        hitCount);
                }

                // Queue mob for death (can't modify collection during iteration)
                if (died)
                {
                    mobsToKill.Add(mob);
                }

                hitCount++;
            }

            // Kill mobs after iteration is complete
            foreach (var mob in mobsToKill)
            {
                HandleMobDeath(mob, currentTime);
            }

            return hitCount > 0;
        }

        /// <summary>
        /// TryDoingShoot - Ranged projectile attack (matching CUserLocal::TryDoingShoot from client)
        /// </summary>
        public bool TryDoingShoot(int currentTime)
        {
            // Cannot attack while on ladder/rope/swimming
            if (!_player.CanAttack)
                return false;

            if (TryExecuteRepeatSkillNormalAttack(currentTime))
                return true;

            // Trigger shooting animation
            _player.TriggerSkillAnimation("shoot1");

            // Create a basic projectile
            var proj = new ActiveProjectile
            {
                Id = _nextProjectileId++,
                SkillId = 0, // Basic attack
                SkillLevel = 1,
                Data = CreateBasicProjectileData(),
                LevelData = null,
                X = _player.X,
                Y = _player.Y - 25, // Hand height
                PreviousX = _player.X,
                PreviousY = _player.Y - 25,
                FacingRight = _player.FacingRight,
                SpawnTime = currentTime,
                OwnerId = 0,
                OwnerX = _player.X,
                OwnerY = _player.Y
            };

            // Set velocity
            float speed = 8.0f;
            proj.VelocityX = _player.FacingRight ? speed : -speed;
            proj.VelocityY = 0;

            _projectiles.Add(proj);

            return true;
        }

        /// <summary>
        /// TryDoingMagicAttack - Magic attack with effect (matching CUserLocal::TryDoingMagicAttack from client)
        /// </summary>
        public bool TryDoingMagicAttack(int currentTime)
        {
            // Cannot attack while on ladder/rope/swimming
            if (!_player.CanAttack)
                return false;

            if (TryExecuteRepeatSkillNormalAttack(currentTime))
                return true;

            if (_mobPool == null)
                return false;

            // Trigger magic attack animation
            _player.TriggerSkillAnimation("swingO1");

            // Consume MP for magic attack
            int mpCost = 10;
            if (_player.MP < mpCost)
                return false;

            _player.MP -= mpCost;

            // Magic attack has larger range but single target
            int hitWidth = 120;
            int hitHeight = 80;
            int offsetX = _player.FacingRight ? 20 : -(hitWidth + 20);

            var worldHitbox = new Rectangle(
                (int)_player.X + offsetX,
                (int)_player.Y - hitHeight - 10,
                hitWidth,
                hitHeight);
            NotifyAttackAreaResolved(worldHitbox, currentTime, skillId: 0);

            MobItem closestMob = null;
            float closestDist = float.MaxValue;

            foreach (var mob in _mobPool.ActiveMobs)
            {
                // Skip dead or removed mobs
                if (mob?.AI == null || mob.AI.IsDead)
                    continue;

                var mobHitbox = GetMobHitbox(mob);
                if (!worldHitbox.Intersects(mobHitbox))
                    continue;

                float dist = Math.Abs((mob.MovementInfo?.X ?? 0) - _player.X);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestMob = mob;
                }
            }

            if (closestMob == null)
                return false;

            // Calculate magic damage (higher than melee but single target)
            int damage = CalculateBasicDamage() + 50;
            bool isCritical = Random.Next(100) < 20; // 20% crit chance
            if (isCritical)
                damage = (int)(damage * 1.5f);

            bool died = closestMob.ApplyDamage(damage, currentTime, isCritical, _player.X, _player.Y, damageType: MobDamageType.Magical);
            ApplyMobReflectDamage(closestMob, currentTime, MobDamageType.Magical);
            RecordOwnerAttackTarget(closestMob, currentTime);
            RegisterEnergyChargeHit(currentTime);

            // Apply knockback effect if mob didn't die
            if (!died && closestMob.MovementInfo != null)
            {
                float knockbackForce = 4f + (damage / 80f); // Magic has less knockback
                knockbackForce = Math.Min(knockbackForce, 8f); // Cap at 8
                bool knockRight = _player.FacingRight;
                closestMob.MovementInfo.ApplyKnockback(knockbackForce, knockRight);
            }

            if (_combatEffects != null)
            {
                Vector2 damageAnchor = closestMob.GetDamageNumberAnchor();

                // Notify HP bar system
                _combatEffects.OnMobDamaged(closestMob, currentTime);

                _combatEffects.AddDamageNumber(
                    damage,
                    damageAnchor.X,
                    damageAnchor.Y,
                    isCritical,
                    false,
                    currentTime,
                    0);
            }

            // If mob died, notify mob pool
            if (died && _mobPool != null)
            {
                HandleMobDeath(closestMob, currentTime);
            }

            return true;
        }

        /// <summary>
        /// Handles mob death effects, sounds, and pool removal
        /// </summary>
        private void HandleMobDeath(MobItem mob, int currentTime)
        {
            HandleMobDeath(mob, currentTime, MobDeathType.Killed);
        }

        private void HandleMobDeath(MobItem mob, int currentTime, MobDeathType deathType)
        {
            if (mob == null)
                return;

            _recentOwnerAttackTargetTimes.Remove(mob.PoolId);

            // Play death sound FIRST, before any cleanup
            mob.PlayDieSound();

            // Trigger death effects
            if (_combatEffects != null)
            {
                _combatEffects.AddDeathEffectForMob(mob, currentTime);
                _combatEffects.RemoveMobHPBar(mob.PoolId);
            }

            // Remove from mob pool LAST
            _mobPool?.KillMob(mob, deathType);
        }

        /// <summary>
        /// TryDoingRandomAttack - Randomly selects and performs one of the three attack types
        /// </summary>
        public bool TryDoingRandomAttack(int currentTime)
        {
            int attackType = Random.Next(3);

            return attackType switch
            {
                0 => TryDoingMeleeAttack(currentTime),
                1 => TryDoingShoot(currentTime),
                2 => TryDoingMagicAttack(currentTime),
                _ => TryDoingMeleeAttack(currentTime)
            };
        }

        /// <summary>
        /// Calculate basic attack damage without skill
        /// </summary>
        private int CalculateBasicDamage()
        {
            int baseAttack = _player.Build?.Attack ?? 10;
            var weapon = _player.Build?.GetWeapon();
            if (weapon != null)
            {
                baseAttack += weapon.Attack;
            }

            // Variance 0.9 - 1.1
            float variance = 0.9f + (float)Random.NextDouble() * 0.2f;

            return Math.Max(1, (int)(baseAttack * variance));
        }

        /// <summary>
        /// Create basic projectile data for shooting
        /// </summary>
        private ProjectileData CreateBasicProjectileData()
        {
            return new ProjectileData
            {
                Speed = 8.0f,
                Piercing = false,
                MaxHits = 1,
                LifeTime = 2000 // 2 seconds
            };
        }

        #endregion

        #region Attack Processing

        private void ProcessMeleeAttack(
            SkillData skill,
            int level,
            int currentTime,
            bool facingRight,
            bool queueFollowUps = true,
            int? preferredTargetMobId = null,
            Vector2? preferredTargetPosition = null,
            Vector2? attackOriginOverride = null,
            string currentActionNameOverride = null,
            int? currentRawActionCodeOverride = null,
            AttackTargetSelectionMode attackTargetSelectionMode = AttackTargetSelectionMode.Default)
        {
            ProcessDirectionalAttack(skill, level, currentTime, AttackResolutionMode.Melee, facingRight, queueFollowUps, preferredTargetMobId, preferredTargetPosition, attackOriginOverride, 0, false, currentActionNameOverride, currentRawActionCodeOverride, attackTargetSelectionMode);
        }

        private void ProcessMagicAttack(
            SkillData skill,
            int level,
            int currentTime,
            bool facingRight,
            bool queueFollowUps = true,
            int? preferredTargetMobId = null,
            Vector2? preferredTargetPosition = null,
            Vector2? attackOriginOverride = null,
            string currentActionNameOverride = null,
            int? currentRawActionCodeOverride = null)
        {
            ProcessDirectionalAttack(skill, level, currentTime, AttackResolutionMode.Magic, facingRight, queueFollowUps, preferredTargetMobId, preferredTargetPosition, attackOriginOverride, 0, false, currentActionNameOverride, currentRawActionCodeOverride);
        }

        private void ProcessRangedAttack(
            SkillData skill,
            int level,
            int currentTime,
            bool facingRight,
            bool queueFollowUps = true,
            int? preferredTargetMobId = null,
            Vector2? preferredTargetPosition = null,
            Vector2? attackOriginOverride = null,
            int shootRange0Override = 0,
            bool forceCriticalForAttack = false,
            string currentActionNameOverride = null,
            int? currentRawActionCodeOverride = null)
        {
            ProcessDirectionalAttack(skill, level, currentTime, AttackResolutionMode.Ranged, facingRight, queueFollowUps, preferredTargetMobId, preferredTargetPosition, attackOriginOverride, shootRange0Override, forceCriticalForAttack, currentActionNameOverride, currentRawActionCodeOverride);
        }

        private void ProcessNoProjectileRangedAttack(
            SkillData skill,
            int level,
            int currentTime,
            bool facingRight,
            bool queueFollowUps = true,
            int? preferredTargetMobId = null,
            Vector2? preferredTargetPosition = null,
            Vector2? attackOriginOverride = null,
            int shootRange0Override = 0,
            bool forceCriticalForAttack = false,
            string currentActionNameOverride = null,
            int? currentRawActionCodeOverride = null)
        {
            if (!ShouldTryMeleeBeforeRangedFallback(skill))
            {
                ProcessRangedAttack(skill, level, currentTime, facingRight, queueFollowUps, preferredTargetMobId, preferredTargetPosition, attackOriginOverride, shootRange0Override, forceCriticalForAttack, currentActionNameOverride, currentRawActionCodeOverride);
                return;
            }

            if (TryProcessDirectionalAttack(
                    skill,
                    level,
                    currentTime,
                    AttackResolutionMode.Melee,
                    facingRight,
                    queueFollowUps,
                    preferredTargetMobId,
                    preferredTargetPosition,
                    attackOriginOverride,
                    shootRange0Override,
                    forceCriticalForAttack,
                    currentActionNameOverride,
                    currentRawActionCodeOverride))
            {
                return;
            }

            TryProcessDirectionalAttack(
                skill,
                level,
                currentTime,
                AttackResolutionMode.Ranged,
                facingRight,
                queueFollowUps,
                preferredTargetMobId,
                preferredTargetPosition,
                attackOriginOverride,
                shootRange0Override,
                forceCriticalForAttack,
                currentActionNameOverride,
                currentRawActionCodeOverride);
        }

        private void ProcessDirectionalAttack(
            SkillData skill,
            int level,
            int currentTime,
            AttackResolutionMode mode,
            bool facingRight,
            bool queueFollowUps,
            int? preferredTargetMobId,
            Vector2? preferredTargetPosition,
            Vector2? attackOriginOverride,
            int shootRange0Override = 0,
            bool forceCriticalForAttack = false,
            string currentActionNameOverride = null,
            int? currentRawActionCodeOverride = null,
            AttackTargetSelectionMode attackTargetSelectionMode = AttackTargetSelectionMode.Default)
        {
            TryProcessDirectionalAttack(skill, level, currentTime, mode, facingRight, queueFollowUps, preferredTargetMobId, preferredTargetPosition, attackOriginOverride, shootRange0Override, forceCriticalForAttack, currentActionNameOverride, currentRawActionCodeOverride, attackTargetSelectionMode);
        }

        private bool TryProcessDirectionalAttack(
            SkillData skill,
            int level,
            int currentTime,
            AttackResolutionMode mode,
            bool facingRight,
            bool queueFollowUps,
            int? preferredTargetMobId,
            Vector2? preferredTargetPosition,
            Vector2? attackOriginOverride,
            int shootRange0Override = 0,
            bool forceCriticalForAttack = false,
            string currentActionNameOverride = null,
            int? currentRawActionCodeOverride = null,
            AttackTargetSelectionMode attackTargetSelectionMode = AttackTargetSelectionMode.Default)
        {
            if (_mobPool == null)
                return false;

            var levelData = skill.GetLevel(level);
            if (levelData == null)
                return false;

            Vector2 attackOrigin = attackOriginOverride ?? new Vector2(_player.X, _player.Y);
            Rectangle worldHitbox = GetWorldAttackHitbox(skill, level, levelData, mode, facingRight, attackOrigin, shootRange0Override, currentActionNameOverride, currentRawActionCodeOverride);
            NotifyAttackAreaResolved(worldHitbox, currentTime, skill.SkillId);
            int maxTargets = Math.Max(1, levelData.MobCount);
            int attackCount = Math.Max(1, levelData.AttackCount);
            List<MobItem> targets = ResolveTargetsInHitbox(
                worldHitbox,
                currentTime,
                maxTargets,
                mode,
                facingRight,
                preferredTargetMobId,
                preferredTargetPosition,
                attackOrigin,
                attackTargetSelectionMode);
            if (targets.Count == 0)
                return false;

            if (queueFollowUps && ShouldUseQueuedSerialAttack(skill))
            {
                QueueOrReplaceSerialAttack(skill, level, currentTime, targets, facingRight);
                targets = targets.Take(1).ToList();
            }

            if (queueFollowUps)
            {
                TryQueueFollowUpAttack(skill, currentTime, targets[0].PoolId, facingRight);
            }

            TryQueueAttackTriggeredBuffProc(skill, currentTime, targets[0], facingRight);

            var mobsToKill = new List<MobItem>();
            for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
            {
                MobItem mob = targets[targetIndex];
                bool died = ApplySkillAttackToMob(
                    skill,
                    level,
                    levelData,
                    mob,
                    currentTime,
                    attackCount,
                    mode,
                    skill.HitEffect,
                    facingRight,
                    targetIndex,
                    forceCriticalForAttack);

                if (died && !mobsToKill.Contains(mob))
                {
                    mobsToKill.Add(mob);
                }
            }

            foreach (MobItem mob in mobsToKill)
            {
                HandleMobDeath(mob, currentTime);
            }

            return true;
        }

        private Rectangle GetWorldAttackHitbox(
            SkillData skill,
            int level,
            SkillLevelData levelData,
            AttackResolutionMode mode,
            bool facingRight,
            Vector2? attackOriginOverride = null,
            int shootRange0Override = 0,
            string currentActionNameOverride = null,
            int? currentRawActionCodeOverride = null)
        {
            Rectangle hitbox = Rectangle.Empty;
            if (mode == AttackResolutionMode.Melee)
            {
                if (!skill.TryGetExplicitAttackRange(level, facingRight, out hitbox))
                {
                    TryGetMeleeAfterImageHitbox(skill, facingRight, out hitbox, currentActionNameOverride, currentRawActionCodeOverride);
                }
            }
            else
            {
                hitbox = skill.GetAttackRange(level, facingRight);
            }

            if (hitbox.Width <= 0 || hitbox.Height <= 0)
            {
                hitbox = mode switch
                {
                    AttackResolutionMode.Magic => GetDefaultMagicHitbox(skill, levelData, facingRight),
                    AttackResolutionMode.Ranged or AttackResolutionMode.Projectile => GetDefaultRangedHitbox(skill, levelData, facingRight, shootRange0Override),
                    _ => GetDefaultMeleeHitbox(skill, levelData, facingRight)
                };
            }

            Vector2 attackOrigin = attackOriginOverride ?? new Vector2(_player.X, _player.Y);

            return new Rectangle(
                (int)attackOrigin.X + hitbox.X,
                (int)attackOrigin.Y + hitbox.Y,
                Math.Max(1, hitbox.Width),
                Math.Max(1, hitbox.Height));
        }

        private bool TryGetMeleeAfterImageHitbox(
            SkillData skill,
            bool facingRight,
            out Rectangle hitbox,
            string currentActionNameOverride = null,
            int? currentRawActionCodeOverride = null)
        {
            hitbox = Rectangle.Empty;

            WeaponPart weapon = _player.Build?.GetWeapon();
            string resolvedActionName = currentActionNameOverride;
            if (string.IsNullOrWhiteSpace(resolvedActionName) && currentRawActionCodeOverride.HasValue)
            {
                resolvedActionName = ResolvePacketOwnedMeleeAttackActionName(
                    skill,
                    currentRawActionCodeOverride,
                    _player?.CurrentActionName,
                    candidate => _player?.CanRenderAction(candidate) != false);
            }

            if (!_loader.TryResolveMeleeAfterImageAction(
                    skill,
                    weapon,
                    resolvedActionName ?? _player.CurrentActionName,
                    _player.Level,
                    GetMastery(weapon),
                    ResolveActiveAfterImageChargeElement(),
                    out MeleeAfterImageAction afterImageAction)
                || afterImageAction == null)
            {
                afterImageAction = _loader.ApplyClientMeleeRangeOverride(
                    null,
                    skill?.SkillId ?? 0,
                    currentRawActionCodeOverride,
                    facingRight: true);
                if (afterImageAction == null || !afterImageAction.HasRange)
                {
                    return false;
                }
            }
            else
            {
                afterImageAction = _loader.ApplyClientMeleeRangeOverride(
                    afterImageAction,
                    skill?.SkillId ?? 0,
                    currentRawActionCodeOverride,
                    facingRight: true);
                if (afterImageAction == null || !afterImageAction.HasRange)
                {
                    return false;
                }
            }

            hitbox = afterImageAction.Range;
            if (!facingRight)
            {
                hitbox = new Rectangle(
                    -(hitbox.X + hitbox.Width),
                    hitbox.Y,
                    hitbox.Width,
                    hitbox.Height);
            }

            return true;
        }

        private List<MobItem> ResolveTargetsInHitbox(
            Rectangle worldHitbox,
            int currentTime,
            int maxTargets,
            AttackResolutionMode mode,
            bool facingRight,
            int? preferredTargetMobId,
            Vector2? preferredTargetPosition = null,
            Vector2? attackOriginOverride = null,
            AttackTargetSelectionMode attackTargetSelectionMode = AttackTargetSelectionMode.Default)
        {
            if (_mobPool == null || maxTargets <= 0)
                return new List<MobItem>();

            Vector2 attackOrigin = attackOriginOverride ?? new Vector2(_player.X, _player.Y);
            float areaCenterX = worldHitbox.Left + worldHitbox.Width * 0.5f;
            float areaCenterY = worldHitbox.Top + worldHitbox.Height * 0.5f;

            if (attackTargetSelectionMode == AttackTargetSelectionMode.PacketOwnedTimeBomb)
            {
                return _mobPool.ActiveMobs
                    .Where(IsMobAttackable)
                    .Select(mob => new { Mob = mob, Hitbox = GetMobHitbox(mob, currentTime) })
                    .Where(entry => !entry.Hitbox.IsEmpty && worldHitbox.Intersects(entry.Hitbox))
                    .Select(entry => new
                    {
                        entry.Mob,
                        SortKey = CreatePacketOwnedTimeBombTargetSortKey(
                            worldHitbox,
                            entry.Hitbox,
                            attackOrigin,
                            facingRight,
                            entry.Mob.PoolId,
                            preferredTargetMobId,
                            preferredTargetPosition)
                    })
                    .OrderBy(entry => entry.SortKey.PreferredRank)
                    .ThenBy(entry => entry.SortKey.PreferredPositionDistance)
                    .ThenBy(entry => entry.SortKey.AreaDistance)
                    .ThenBy(entry => entry.SortKey.VerticalDistance)
                    .ThenBy(entry => entry.SortKey.ForwardPenalty)
                    .Take(maxTargets)
                    .Select(entry => entry.Mob)
                    .ToList();
            }

            return _mobPool.ActiveMobs
                .Where(IsMobAttackable)
                .Select(mob => new { Mob = mob, Hitbox = GetMobHitbox(mob, currentTime) })
                .Where(entry => !entry.Hitbox.IsEmpty && worldHitbox.Intersects(entry.Hitbox))
                .Select(entry =>
                {
                    float mobCenterX = entry.Hitbox.Left + entry.Hitbox.Width * 0.5f;
                    float mobCenterY = entry.Hitbox.Top + entry.Hitbox.Height * 0.5f;
                    float deltaX = mobCenterX - attackOrigin.X;
                    float forwardDistance = facingRight ? deltaX : -deltaX;
                    float forwardPenalty = forwardDistance < 0f ? 100000f + MathF.Abs(forwardDistance) : forwardDistance;
                    float verticalDistance = MathF.Abs(mobCenterY - attackOrigin.Y);
                    float areaDistance = Vector2.Distance(
                        new Vector2(mobCenterX, mobCenterY),
                        new Vector2(areaCenterX, areaCenterY));

                    return new
                    {
                        entry.Mob,
                        Preferred = preferredTargetMobId.HasValue && entry.Mob.PoolId == preferredTargetMobId.Value ? 0 : 1,
                        PreferredPositionDistance = preferredTargetPosition.HasValue
                            ? Vector2.DistanceSquared(new Vector2(mobCenterX, mobCenterY), preferredTargetPosition.Value)
                            : float.MaxValue,
                        Primary = mode switch
                        {
                            AttackResolutionMode.Magic => areaDistance,
                            AttackResolutionMode.Ranged => forwardPenalty,
                            _ => forwardPenalty
                        },
                        Secondary = mode switch
                        {
                            AttackResolutionMode.Magic => forwardPenalty,
                            AttackResolutionMode.Ranged => verticalDistance,
                            _ => areaDistance
                        },
                        Tertiary = mode == AttackResolutionMode.Ranged ? areaDistance : verticalDistance
                    };
                })
                .OrderBy(entry => entry.Preferred)
                .ThenBy(entry => entry.PreferredPositionDistance)
                .ThenBy(entry => entry.Primary)
                .ThenBy(entry => entry.Secondary)
                .ThenBy(entry => entry.Tertiary)
                .Take(maxTargets)
                .Select(entry => entry.Mob)
                .ToList();
        }

        internal static PacketOwnedTimeBombTargetSortKey CreatePacketOwnedTimeBombTargetSortKey(
            Rectangle worldHitbox,
            Rectangle targetHitbox,
            Vector2 attackOrigin,
            bool facingRight,
            int mobPoolId,
            int? preferredTargetMobId,
            Vector2? preferredTargetPosition)
        {
            float mobCenterX = targetHitbox.Left + targetHitbox.Width * 0.5f;
            float mobCenterY = targetHitbox.Top + targetHitbox.Height * 0.5f;
            float areaCenterX = worldHitbox.Left + worldHitbox.Width * 0.5f;
            float areaCenterY = worldHitbox.Top + worldHitbox.Height * 0.5f;
            float deltaX = mobCenterX - attackOrigin.X;
            float forwardDistance = facingRight ? deltaX : -deltaX;
            float forwardPenalty = forwardDistance < 0f ? 100000f + MathF.Abs(forwardDistance) : forwardDistance;

            return new PacketOwnedTimeBombTargetSortKey(
                PreferredRank: preferredTargetMobId.HasValue && mobPoolId == preferredTargetMobId.Value ? 0 : 1,
                PreferredPositionDistance: preferredTargetPosition.HasValue
                    ? Vector2.DistanceSquared(new Vector2(mobCenterX, mobCenterY), preferredTargetPosition.Value)
                    : float.MaxValue,
                AreaDistance: Vector2.Distance(
                    new Vector2(mobCenterX, mobCenterY),
                    new Vector2(areaCenterX, areaCenterY)),
                VerticalDistance: MathF.Abs(mobCenterY - attackOrigin.Y),
                ForwardPenalty: forwardPenalty);
        }

        private static bool IsMobAttackable(MobItem mob)
        {
            return mob?.AI != null
                && !mob.IsProtectedFromPlayerDamage
                && mob.AI.State != MobAIState.Death
                && mob.AI.State != MobAIState.Removed;
        }

        private bool ApplySkillAttackToMob(
            SkillData skill,
            int level,
            SkillLevelData levelData,
            MobItem mob,
            int currentTime,
            int attackCount,
            AttackResolutionMode mode,
            SkillAnimation impactAnimation,
            bool impactFacingRight,
            int targetOrder = 0,
            bool forceCritical = false)
        {
            if (!IsMobAttackable(mob))
                return false;

            int? damagePercentOverride = ResolveTargetDamagePercentOverride(skill, levelData, targetOrder);
            for (int i = 0; i < attackCount; i++)
            {
                int damage = CalculateSkillDamage(skill, level, damagePercentOverride);
                bool isCritical = forceCritical || IsSkillCritical(levelData);
                if (isCritical)
                {
                    damage = (int)MathF.Round(damage * 1.5f);
                }

                MobDamageType damageType = ResolveMobDamageType(skill);
                bool died = mob.ApplyDamage(damage, currentTime, isCritical, _player.X, _player.Y, damageType: damageType);
                ApplyMobReflectDamage(mob, currentTime, damageType);
                RecordOwnerAttackTarget(mob, currentTime);
                RegisterEnergyChargeHit(currentTime);

                ShowSkillDamageNumber(mob, damage, isCritical, currentTime, i);
                SpawnHitEffect(
                    skill.SkillId,
                    impactAnimation ?? skill.HitEffect,
                    GetMobImpactX(mob),
                    GetMobImpactY(mob),
                    impactFacingRight,
                    currentTime);

                if (died)
                    return true;

                ApplySkillKnockback(mode, mob, damage, impactFacingRight);

                if (!IsMobAttackable(mob))
                    return true;
            }

            ApplyMobStatusFromSkill(skill, levelData, mob, currentTime);
            return mob.AI?.State == MobAIState.Death;
        }

        private bool ConsumePacketOwnedExJablinIfApplicable(SkillData skill)
        {
            if (!_packetOwnedNextShootExJablinArmed
                || skill == null
                || (skill.Projectile == null && skill.AttackType != SkillAttackType.Ranged))
            {
                return false;
            }

            _packetOwnedNextShootExJablinArmed = false;
            return true;
        }

        private void ApplyMobStatusFromSkill(SkillData skill, SkillLevelData levelData, MobItem mob, int currentTime)
        {
            if (skill == null || levelData == null || mob?.AI == null || !IsMobAttackable(mob))
                return;

            ApplyMobStatusFromSkillCore(skill, levelData, mob.AI, currentTime);
        }

        internal void ApplyInferredMobStatusesFromSkill(SkillData skill, SkillLevelData levelData, MobAI mobAI, int currentTime, int fallbackDamage = 0)
        {
            if (skill == null || levelData == null || mobAI == null)
            {
                return;
            }

            ApplyMobStatusFromSkillCore(skill, levelData, mobAI, currentTime, fallbackDamage);
        }

        private void ApplyMobStatusFromSkillCore(SkillData skill, SkillLevelData levelData, MobAI mobAI, int currentTime, int fallbackDamage = 0)
        {
            if (skill == null || levelData == null || mobAI == null)
            {
                return;
            }

            string searchText = BuildSkillSearchText(skill);
            string debuffToken = skill.DebuffMessageToken ?? string.Empty;
            int durationMs = Math.Max(1, ResolveStatusDurationMs(levelData));
            int dotDurationMs = Math.Max(1, ResolveStatusDurationMs(levelData, preferDotDuration: true));
            int dotTickIntervalMs = ResolveDotTickIntervalMs(levelData);
            int propPercent = levelData.Prop > 0 ? Math.Min(100, levelData.Prop) : 100;
            bool allowStatusPackage = ShouldApplyMobStatusPackage(propPercent);
            int resolvedFallbackDamage = fallbackDamage > 0
                ? fallbackDamage
                : Math.Max(1, CalculateSkillDamage(skill, levelData.Level));

            if (MatchesMobStatusKeywords(searchText, "poison", "venom") || skill.Element == SkillElement.Poison)
            {
                MobStatusEffect poisonEffect = searchText.Contains("venom", StringComparison.OrdinalIgnoreCase)
                    ? MobStatusEffect.Venom
                    : MobStatusEffect.Poison;
                int dotValue = ResolveDotStatusMagnitude(levelData, fallback: Math.Max(1, resolvedFallbackDamage / 4));
                TryApplyInferredMobStatus(mobAI, poisonEffect, dotDurationMs, currentTime, dotValue, dotTickIntervalMs, allowStatusPackage);
            }

            if (MatchesMobStatusKeywords(searchText, "burn", "flame"))
            {
                int burnValue = ResolveDotStatusMagnitude(levelData, fallback: Math.Max(1, resolvedFallbackDamage / 5));
                TryApplyInferredMobStatus(mobAI, MobStatusEffect.Burned, dotDurationMs, currentTime, burnValue, dotTickIntervalMs, allowStatusPackage);
            }

            if (MatchesMobStatusKeywords(searchText, "freeze", "ice", "blizzard", "frost") || skill.Element == SkillElement.Ice)
            {
                TryApplyInferredMobStatus(mobAI, MobStatusEffect.Freeze, durationMs, currentTime, 0, tickIntervalMs: 1000, allowStatusPackage);
            }

            if (MatchesMobStatusKeywords(searchText, "stun", "paraly", "shock"))
            {
                TryApplyInferredMobStatus(mobAI, MobStatusEffect.Stun, durationMs, currentTime, 0, tickIntervalMs: 1000, allowStatusPackage);
            }

            if (MatchesMobStatusKeywords(searchText, "seal"))
            {
                TryApplyInferredMobStatus(mobAI, MobStatusEffect.Seal, durationMs, currentTime, 0, tickIntervalMs: 1000, allowStatusPackage);
            }

            if (MatchesMobStatusKeywords(searchText, "blind"))
            {
                TryApplyInferredMobStatus(mobAI, MobStatusEffect.Blind, durationMs, currentTime, 0, tickIntervalMs: 1000, allowStatusPackage);
            }

            if (MatchesMobStatusKeywords(searchText, "dark", "darkness"))
            {
                int magnitude = ResolveStatusMagnitude(levelData, fallback: 20);
                TryApplyInferredMobStatus(mobAI, MobStatusEffect.Darkness, durationMs, currentTime, magnitude, tickIntervalMs: 1000, allowStatusPackage);
            }

            if (MatchesMobStatusKeywords(searchText, "slow", "web"))
            {
                int slowPercent = ResolveSlowStatusMagnitude(levelData);
                TryApplyInferredMobStatus(mobAI, MobStatusEffect.Web, durationMs, currentTime, slowPercent, tickIntervalMs: 1000, allowStatusPackage);
            }

            if (MatchesMobStatusKeywords(searchText, "weak"))
            {
                int weaknessPercent = ResolveStatusMagnitude(levelData, fallback: 20);
                TryApplyInferredMobStatus(mobAI, MobStatusEffect.Weakness, durationMs, currentTime, weaknessPercent, tickIntervalMs: 1000, allowStatusPackage);
            }

            ApplyExplicitMobDebuffMetadata(skill, levelData, mobAI, searchText, debuffToken, durationMs, currentTime, allowStatusPackage);
            ApplyStatDrivenMobDebuffs(skill, levelData, mobAI, searchText, debuffToken, durationMs, currentTime, allowStatusPackage);
        }

        private static bool ShouldApplyMobStatusPackage(int propPercent)
        {
            if (propPercent <= 0)
            {
                return false;
            }

            return propPercent >= 100 || Random.Next(100) < propPercent;
        }

        private static void TryApplyInferredMobStatus(
            MobAI mobAI,
            MobStatusEffect effect,
            int durationMs,
            int currentTime,
            int value,
            int tickIntervalMs,
            bool allowStatusPackage)
        {
            if (mobAI == null || !allowStatusPackage)
                return;

            mobAI.ApplyStatusEffect(effect, durationMs, currentTime, value, tickIntervalMs);
        }

        private static void ApplyStatDrivenMobDebuffs(
            SkillData skill,
            SkillLevelData levelData,
            MobAI mobAI,
            string searchText,
            string debuffToken,
            int durationMs,
            int currentTime,
            bool allowStatusPackage)
        {
            if (mobAI == null || levelData == null || !IsMobDebuffSkill(skill, debuffToken, searchText))
                return;

            int attackReduction = ResolveAttackDebuffMagnitude(levelData, searchText);
            if (attackReduction > 0)
            {
                TryApplyInferredMobStatus(mobAI, MobStatusEffect.PADamage, durationMs, currentTime, -attackReduction, tickIntervalMs: 1000, allowStatusPackage);
                TryApplyInferredMobStatus(mobAI, MobStatusEffect.MADamage, durationMs, currentTime, -attackReduction, tickIntervalMs: 1000, allowStatusPackage);
            }

            int physicalDefenseReduction = ResolvePhysicalDefenseDebuffMagnitude(levelData, searchText);
            if (physicalDefenseReduction > 0)
            {
                TryApplyInferredMobStatus(mobAI, MobStatusEffect.PDamage, durationMs, currentTime, -physicalDefenseReduction, tickIntervalMs: 1000, allowStatusPackage);
            }

            int magicDefenseReduction = ResolveMagicDefenseDebuffMagnitude(levelData, searchText);
            if (magicDefenseReduction > 0)
            {
                TryApplyInferredMobStatus(mobAI, MobStatusEffect.MDamage, durationMs, currentTime, -magicDefenseReduction, tickIntervalMs: 1000, allowStatusPackage);
            }

            int accuracyReduction = ResolveAccuracyDebuffMagnitude(levelData, searchText);
            if (accuracyReduction > 0)
            {
                TryApplyInferredMobStatus(mobAI, MobStatusEffect.ACC, durationMs, currentTime, -accuracyReduction, tickIntervalMs: 1000, allowStatusPackage);
            }

            int evasionReduction = ResolveEvasionDebuffMagnitude(levelData, searchText);
            if (evasionReduction > 0)
            {
                TryApplyInferredMobStatus(mobAI, MobStatusEffect.EVA, durationMs, currentTime, -evasionReduction, tickIntervalMs: 1000, allowStatusPackage);
            }
        }

        private static string BuildSkillSearchText(SkillData skill)
        {
            return string.Join(" ",
                skill?.Name ?? string.Empty,
                skill?.Description ?? string.Empty,
                skill?.ActionName ?? string.Empty,
                skill?.DebuffMessageToken ?? string.Empty,
                skill?.MinionAttack ?? string.Empty);
        }

        private void ApplyMobReflectDamage(MobItem mob, int currentTime, MobDamageType damageType)
        {
            if (mob?.AI == null || _player == null || !_player.IsAlive)
            {
                return;
            }

            int reflectedDamage = mob.AI.CalculateReflectedDamageToAttacker(mob.AI.LastDamageTaken, damageType);
            if (reflectedDamage > 0)
            {
                reflectedDamage = ResolveIncomingDamageAfterActiveBuffs(reflectedDamage, currentTime);
                _player.TakeDamage(reflectedDamage, 0f, 0f);
            }
        }

        private static MobDamageType ResolveMobDamageType(SkillData skill)
        {
            if (skill == null)
            {
                return MobDamageType.Physical;
            }

            return skill.Type == SkillType.Magic
                || skill.AttackType == SkillAttackType.Magic
                || skill.IsMagicDamageSkill
                || skill.Element != SkillElement.Physical
                ? MobDamageType.Magical
                : MobDamageType.Physical;
        }

        private static bool MatchesMobStatusKeywords(string searchText, params string[] keywords)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return false;

            foreach (string keyword in keywords)
            {
                if (!string.IsNullOrWhiteSpace(keyword)
                    && searchText.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ApplyExplicitMobDebuffMetadata(
            SkillData skill,
            SkillLevelData levelData,
            MobAI mobAI,
            string searchText,
            string debuffToken,
            int durationMs,
            int currentTime,
            bool allowStatusPackage)
        {
            if (mobAI == null || levelData == null || string.IsNullOrWhiteSpace(debuffToken))
                return;

            if (debuffToken.IndexOf("incapacitate", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                TryApplyInferredMobStatus(mobAI, MobStatusEffect.Stun, durationMs, currentTime, 1, tickIntervalMs: 1000, allowStatusPackage);
            }

            if (debuffToken.IndexOf("buffLimit", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (allowStatusPackage)
                {
                    CancelMobBuffStatuses(mobAI);
                }

                TryApplyInferredMobStatus(mobAI, MobStatusEffect.SealSkill, durationMs, currentTime, 1, tickIntervalMs: 1000, allowStatusPackage);
            }

            if (debuffToken.IndexOf("restrict", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                TryApplyInferredMobStatus(mobAI, MobStatusEffect.Web, durationMs, currentTime, 100, tickIntervalMs: 1000, allowStatusPackage);
            }

            if (debuffToken.IndexOf("attackLimit", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                TryApplyInferredMobStatus(mobAI, MobStatusEffect.Stun, durationMs, currentTime, 1, tickIntervalMs: 1000, allowStatusPackage);
            }

            if (debuffToken.IndexOf("haste", StringComparison.OrdinalIgnoreCase) >= 0
                && IsMobDebuffSkill(skill, debuffToken, searchText))
            {
                int speedPercent = ResolveMobSpeedStatusMagnitude(levelData);
                TryApplyInferredMobStatus(mobAI, MobStatusEffect.Speed, durationMs, currentTime, speedPercent, tickIntervalMs: 1000, allowStatusPackage);
            }

            if (debuffToken.IndexOf("mindControl", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                TryApplyInferredMobStatus(mobAI, MobStatusEffect.Hypnotize, durationMs, currentTime, 1, tickIntervalMs: 1000, allowStatusPackage);
            }

            if (debuffToken.IndexOf("polymorph", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                TryApplyInferredMobStatus(mobAI, MobStatusEffect.Doom, durationMs, currentTime, 1, tickIntervalMs: 1000, allowStatusPackage);
                TryApplyInferredMobStatus(mobAI, MobStatusEffect.Seal, durationMs, currentTime, 1, tickIntervalMs: 1000, allowStatusPackage);
            }

            if (debuffToken.IndexOf("amplifyDamage", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                int amplifiedDamage = ResolveStatusMagnitude(levelData, fallback: 10);
                TryApplyInferredMobStatus(mobAI, MobStatusEffect.Ambush, durationMs, currentTime, amplifiedDamage, tickIntervalMs: 1000, allowStatusPackage);
            }

            if (debuffToken.IndexOf("elementalWeaken", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                int weakenValue = ResolveStatusMagnitude(levelData, fallback: 20);
                TryApplyInferredMobStatus(mobAI, MobStatusEffect.Neutralise, durationMs, currentTime, weakenValue, tickIntervalMs: 1000, allowStatusPackage);
            }

            if (debuffToken.IndexOf("incTargetPDP", StringComparison.OrdinalIgnoreCase) >= 0 &&
                debuffToken.IndexOf("incTargetMDP", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                int defenseIncrease = ResolveStatusMagnitude(levelData, fallback: 10);
                TryApplyInferredMobStatus(mobAI, MobStatusEffect.PDamage, durationMs, currentTime, defenseIncrease, tickIntervalMs: 1000, allowStatusPackage);
                TryApplyInferredMobStatus(mobAI, MobStatusEffect.MDamage, durationMs, currentTime, defenseIncrease, tickIntervalMs: 1000, allowStatusPackage);

                if (debuffToken.IndexOf("incTargetEXP", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    debuffToken.IndexOf("incTargetReward", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    int rewardBonusPercent = ResolveShowdownRewardMagnitude(levelData, defenseIncrease);
                    TryApplyInferredMobStatus(mobAI, MobStatusEffect.Showdown, durationMs, currentTime, rewardBonusPercent, tickIntervalMs: 1000, allowStatusPackage);
                }
            }
        }

        private static int ResolveShowdownRewardMagnitude(SkillLevelData levelData, int fallback)
        {
            if (levelData == null)
                return Math.Max(1, fallback);

            if (levelData.X != 0)
                return Math.Abs(levelData.X);

            if (levelData.Y != 0)
                return Math.Abs(levelData.Y);

            return Math.Max(1, fallback);
        }

        private static bool IsMobDebuffSkill(SkillData skill, string debuffToken, string searchText)
        {
            if (skill == null)
                return false;

            return skill.Type == SkillType.Debuff
                || !string.IsNullOrWhiteSpace(debuffToken)
                || MatchesMobStatusKeywords(searchText,
                    "reduce target",
                    "enemy",
                    "monster",
                    "bind",
                    "buff block",
                    "showdown");
        }

        private static void CancelMobBuffStatuses(MobAI mobAI)
        {
            if (mobAI == null)
                return;

            mobAI.ClearPositiveStatusEffects();
        }

        private static int ResolveStatusDurationMs(SkillLevelData levelData, bool preferDotDuration = false)
        {
            if (levelData == null)
                return 1000;

            if (preferDotDuration && levelData.DotTime > 0)
                return levelData.DotTime * 1000;

            if (levelData.Time > 0)
                return levelData.Time * 1000;

            return 4000;
        }

        private static int ResolveDotTickIntervalMs(SkillLevelData levelData)
        {
            if (levelData?.DotInterval > 0)
                return levelData.DotInterval * 1000;

            return 1000;
        }

        private static int ResolveDotStatusMagnitude(SkillLevelData levelData, int fallback)
        {
            if (levelData?.DotDamage > 0)
                return levelData.DotDamage;

            return ResolveStatusMagnitude(levelData, fallback);
        }

        private static int ResolveStatusMagnitude(SkillLevelData levelData, int fallback)
        {
            if (levelData == null)
                return Math.Max(1, fallback);

            int[] candidates =
            {
                Math.Abs(levelData.X),
                Math.Abs(levelData.Y),
                Math.Abs(levelData.Z),
                Math.Abs(levelData.PAD),
                Math.Abs(levelData.MAD),
                Math.Abs(levelData.PDD),
                Math.Abs(levelData.MDD),
                Math.Abs(levelData.ACC),
                Math.Abs(levelData.EVA)
            };

            foreach (int candidate in candidates)
            {
                if (candidate > 0)
                    return candidate;
            }

            return Math.Max(1, fallback);
        }

        private static int ResolveAttackDebuffMagnitude(SkillLevelData levelData, string searchText)
        {
            if (levelData == null)
                return 0;

            if (levelData.PAD != 0 || levelData.MAD != 0)
                return MaxAbs(levelData.PAD, levelData.MAD);

            if (MatchesMobStatusKeywords(searchText, "enemy attack", "their attack", "reduce target dam", "reducing attack"))
                return MaxAbs(levelData.X, levelData.Y, levelData.Z);

            return 0;
        }

        private static int ResolvePhysicalDefenseDebuffMagnitude(SkillLevelData levelData, string searchText)
        {
            if (levelData == null)
                return 0;

            if (levelData.PDD != 0)
                return Math.Abs(levelData.PDD);

            if (MatchesMobStatusKeywords(searchText, "defense", "armor", "reduce target pdp"))
                return MaxAbs(levelData.Y, levelData.X, levelData.Z);

            return 0;
        }

        private static int ResolveMagicDefenseDebuffMagnitude(SkillLevelData levelData, string searchText)
        {
            if (levelData == null)
                return 0;

            if (levelData.MDD != 0)
                return Math.Abs(levelData.MDD);

            if (MatchesMobStatusKeywords(searchText, "magic defense", "mdef", "reduce target mdp"))
                return MaxAbs(levelData.Y, levelData.X, levelData.Z);

            if (MatchesMobStatusKeywords(searchText, "defense", "armor", "reduce target mdp"))
                return MaxAbs(levelData.Y, levelData.X, levelData.Z);

            return 0;
        }

        private static int ResolveAccuracyDebuffMagnitude(SkillLevelData levelData, string searchText)
        {
            if (levelData == null)
                return 0;

            if (levelData.ACC != 0)
                return Math.Abs(levelData.ACC);

            if (MatchesMobStatusKeywords(searchText, "accuracy", "reduce target acc"))
                return MaxAbs(levelData.Z, levelData.X, levelData.Y);

            return 0;
        }

        private static int ResolveEvasionDebuffMagnitude(SkillLevelData levelData, string searchText)
        {
            if (levelData == null)
                return 0;

            if (levelData.EVA != 0)
                return Math.Abs(levelData.EVA);

            if (MatchesMobStatusKeywords(searchText, "avoidability", "avoid", "evasion", "reduce target eva"))
                return MaxAbs(levelData.Z, levelData.Y, levelData.X);

            return 0;
        }

        private static int ResolveSlowStatusMagnitude(SkillLevelData levelData)
        {
            if (levelData == null)
                return 40;

            if (levelData.Speed != 0)
                return Math.Abs(levelData.Speed);

            return ResolveStatusMagnitude(levelData, fallback: 40);
        }

        private static int ResolveMobSpeedStatusMagnitude(SkillLevelData levelData)
        {
            if (levelData == null)
            {
                return 20;
            }

            if (levelData.Speed != 0)
            {
                return Math.Abs(levelData.Speed);
            }

            return MaxAbs(levelData.X, levelData.Y, levelData.Z) switch
            {
                > 0 and var magnitude => magnitude,
                _ => 20
            };
        }

        private static int MaxAbs(params int[] values)
        {
            int best = 0;
            if (values == null)
                return best;

            foreach (int value in values)
            {
                best = Math.Max(best, Math.Abs(value));
            }

            return best;
        }

        private static bool IsSkillCritical(SkillLevelData levelData)
        {
            return levelData?.CriticalRate > 0 && Random.Next(100) < levelData.CriticalRate;
        }

        private void ShowSkillDamageNumber(MobItem mob, int damage, bool isCritical, int currentTime, int hitIndex)
        {
            if (isCritical)
            {
                NotifyLocalCriticalHit(currentTime);
            }

            if (_combatEffects == null)
                return;

            Vector2 damageAnchor = mob.GetDamageNumberAnchor();
            _combatEffects.OnMobDamaged(mob, currentTime);
            _combatEffects.AddDamageNumber(
                damage,
                damageAnchor.X,
                damageAnchor.Y,
                isCritical,
                false,
                currentTime,
                hitIndex);
        }

        private void ApplySkillKnockback(AttackResolutionMode mode, MobItem mob, int damage, bool knockRight)
        {
            if (mob?.MovementInfo == null)
                return;

            float knockbackForce = mode switch
            {
                AttackResolutionMode.Magic => 4f + (damage / 80f),
                AttackResolutionMode.Ranged => 5f + (damage / 95f),
                AttackResolutionMode.Projectile => 5f + (damage / 95f),
                _ => 6f + (damage / 60f)
            };

            float cap = mode switch
            {
                AttackResolutionMode.Magic => 8f,
                AttackResolutionMode.Ranged => 10f,
                AttackResolutionMode.Projectile => 10f,
                _ => 12f
            };

            mob.MovementInfo.ApplyKnockback(Math.Min(knockbackForce, cap), knockRight);
        }

        private float GetMobImpactX(MobItem mob)
        {
            if (mob == null)
                return 0f;

            return mob.MovementInfo?.X ?? mob.CurrentX;
        }

        private float GetMobImpactY(MobItem mob)
        {
            if (mob == null)
                return 0f;

            float baseY = mob.MovementInfo?.Y ?? mob.CurrentY;
            return baseY - Math.Max(20f, mob.GetVisualHeight() * 0.5f);
        }

        private Vector2 GetMobHitboxCenter(MobItem mob, int currentTime)
        {
            Rectangle hitbox = GetMobHitbox(mob, currentTime);
            if (!hitbox.IsEmpty)
            {
                return new Vector2(
                    hitbox.Left + hitbox.Width * 0.5f,
                    hitbox.Top + hitbox.Height * 0.5f);
            }

            return new Vector2(GetMobImpactX(mob), GetMobImpactY(mob));
        }

        private Rectangle GetMobHitbox(MobItem mob, int currentTime)
        {
            if (mob == null)
                return Rectangle.Empty;

            Rectangle bodyHitbox = mob.GetBodyHitbox(currentTime);
            if (!bodyHitbox.IsEmpty)
                return bodyHitbox;

            if (mob.MovementInfo == null)
                return Rectangle.Empty;

            return new Rectangle(
                (int)mob.MovementInfo.X - 20,
                (int)mob.MovementInfo.Y - 50,
                40,
                50);
        }

        private Rectangle GetMobHitbox(MobItem mob)
        {
            return GetMobHitbox(mob, Environment.TickCount);
        }

        private int CalculateSkillDamage(SkillData skill, int level, int? damagePercentOverride = null)
        {
            if (skill == null)
                return 1;

            var levelData = skill.GetLevel(level);
            if (levelData == null)
                return 1;

            // Base attack calculation
            int baseAttack = skill.AttackType == SkillAttackType.Magic
                ? _player.Build?.MagicAttack ?? 10
                : _player.Build?.Attack ?? 10;
            var weapon = _player.Build?.GetWeapon();
            if (weapon != null && skill.AttackType != SkillAttackType.Magic)
            {
                baseAttack += weapon.Attack;
            }

            // Apply skill damage multiplier
            int damagePercent = damagePercentOverride ?? levelData.Damage;
            float multiplier = damagePercent / 100f;
            if (multiplier <= 0f)
                multiplier = 1f;

            int outgoingDamagePercent = ResolveOutgoingDamageBuffPercent();
            if (outgoingDamagePercent > 0)
            {
                multiplier *= 1f + outgoingDamagePercent / 100f;
            }

            if (_activeSkillDamageScaleOverride.HasValue && _activeSkillDamageScaleOverride.Value > 0f)
            {
                multiplier *= _activeSkillDamageScaleOverride.Value;
            }

            // Variance
            float variance = 0.9f + (float)Random.NextDouble() * 0.2f;

            int damage = (int)(baseAttack * multiplier * variance);

            return Math.Max(1, damage);
        }

        private int ResolveOutgoingDamageBuffPercent()
        {
            int bestPercent = 0;

            foreach (ActiveBuff buff in _buffs)
            {
                if (buff?.LevelData == null
                    || buff.SkillData == null
                    || !SummonRuntimeRules.HasMinionAbilityToken(buff.SkillData.MinionAbility, "amplifyDamage"))
                {
                    continue;
                }

                bestPercent = Math.Max(bestPercent, Math.Max(0, buff.LevelData.X));
            }

            return bestPercent;
        }

        private static readonly IReadOnlyDictionary<int, double[]> ClientTargetDamageMultiplierTables = new Dictionary<int, double[]>
        {
            // Client-owned `SKILLENTRY::AdjustDamageDecRate` tables from MapleStory.exe v95.
            [5121002] = new[] { 1d, 0.7d, 0.49d, 0.343d, 0.2401d, 0.16807d, 0d, 0d },
            [3201005] = new[] { 1d, 0.9d, 0.81d, 0.729d, 0.6561d, 0.59049d, 0.531441d, 0.478296d },
            [3221001] = new[] { 1d, 1.2d, 1.44d, 1.728d, 2.0736d, 2.48832d, 0d, 0d },
            [33101001] = new[] { 1d, 1.2d, 1.44d, 1.728d, 2.0736d, 2.48832d, 0d, 0d },
            [4341004] = new[] { 1d, 1d, 1d, 0.95d, 0.95d, 0.95d, 0.9d, 0.9d }
        };

        internal static int? ResolveTargetDamagePercentOverride(SkillData skill, SkillLevelData levelData, int targetOrder)
        {
            if (skill == null || levelData == null || targetOrder < 0)
            {
                return null;
            }

            return skill.SkillId switch
            {
                3101005 when levelData.X > 0 => levelData.X,
                _ when skill.ChainAttackPenalty && levelData.X > 0 => Math.Max(1, (int)MathF.Round(levelData.Damage * ResolveChainDamageDecayMultiplier(levelData.X, targetOrder))),
                _ when TryResolveClientTargetDamageMultiplier(skill.SkillId, targetOrder, out double multiplier)
                    => Math.Max(0, (int)Math.Round(levelData.Damage * multiplier)),
                _ => null
            };
        }

        private static bool TryResolveClientTargetDamageMultiplier(int skillId, int targetOrder, out double multiplier)
        {
            multiplier = 0d;
            if (targetOrder < 0
                || !ClientTargetDamageMultiplierTables.TryGetValue(skillId, out double[] table)
                || table == null
                || targetOrder >= table.Length)
            {
                return false;
            }

            multiplier = table[targetOrder];
            return true;
        }

        private static float ResolveChainDamageDecayMultiplier(int decayPerTargetPercent, int targetOrder)
        {
            if (targetOrder <= 0 || decayPerTargetPercent <= 0)
            {
                return 1f;
            }

            int remainingPercent = Math.Max(0, 100 - (targetOrder * decayPerTargetPercent));
            return remainingPercent / 100f;
        }

        /// <summary>
        /// Spawn a hit effect at the specified position
        /// </summary>
        private void SpawnHitEffect(SkillData skill, float x, float y, int currentTime, bool? facingRightOverride = null)
        {
            if (skill == null)
                return;

            SpawnHitEffect(skill.SkillId, skill.HitEffect, x, y, facingRightOverride ?? _player.FacingRight, currentTime);
        }

        private void SpawnHitEffect(int skillId, SkillAnimation animation, float x, float y, bool facingRight, int currentTime)
        {
            if (animation == null)
                return;

            _hitEffects.Add(new ActiveHitEffect
            {
                SkillId = skillId,
                X = x,
                Y = y,
                StartTime = currentTime,
                Animation = animation,
                FacingRight = facingRight
            });
        }

        #endregion

        #region Projectile System

        private void SpawnProjectile(
            SkillData skill,
            int level,
            int currentTime,
            bool facingRight,
            bool queueFollowUps = true,
            int? preferredTargetMobId = null,
            Vector2? attackOriginOverride = null,
            Vector2? preferredTargetPositionOverride = null,
            bool isQueuedFinalAttack = false,
            bool isQueuedSparkAttack = false,
            bool forceCriticalForAttack = false)
        {
            var levelData = skill.GetLevel(level);
            int bulletCount = levelData?.BulletCount ?? 1;
            float speed = GetProjectileSpeed(skill.Projectile, levelData);
            Vector2 attackOrigin = ResolveProjectileAttackOrigin(
                currentTime,
                facingRight,
                attackOriginOverride,
                out bool usesAuthoredShootPoint);
            float fallbackShootPointYOffset = ResolveClientFallbackShootAttackPointYOffset(skill);
            float projectileSpawnY = ResolveProjectileSpawnY(
                attackOrigin.Y,
                usesAuthoredShootPoint,
                fallbackShootPointYOffset);
            ShootAmmoSelection resolvedShootAmmoSelection = LastResolvedShootAmmoSelection?.Snapshot();
            int resolvedShootWeaponCode = GetEquippedWeaponCode();
            int resolvedShootWeaponItemId = _player?.Build?.GetWeapon()?.ItemId ?? 0;
            List<int> releaseSchedule = BuildProjectileReleaseSchedule(levelData, bulletCount);
            List<int?> projectileTargetAssignments = ResolveMultiTargetProjectileAssignments(
                skill,
                level,
                levelData,
                currentTime,
                facingRight,
                bulletCount,
                preferredTargetMobId,
                preferredTargetPositionOverride,
                attackOrigin);

            for (int i = 0; i < bulletCount; i++)
            {
                int spawnTime = currentTime + (i < releaseSchedule.Count ? releaseSchedule[i] : 0);
                int? projectilePreferredTargetMobId = i < projectileTargetAssignments.Count
                    ? projectileTargetAssignments[i]
                    : preferredTargetMobId;
                Vector2? liveProjectilePreferredTargetPosition = ResolveDeferredPreferredTargetPosition(projectilePreferredTargetMobId, currentTime);
                Vector2? projectilePreferredTargetPosition = ResolveDeferredProjectileTargetPositionSnapshot(
                    preferredTargetPositionOverride,
                    liveProjectilePreferredTargetPosition,
                    preferStoredTargetPosition: !skill.MultiTargeting || !liveProjectilePreferredTargetPosition.HasValue);
                var proj = new ActiveProjectile
                {
                    Id = _nextProjectileId++,
                    SkillId = skill.SkillId,
                    SkillLevel = level,
                    Data = skill.Projectile,
                    LevelData = levelData,
                    X = attackOrigin.X,
                    Y = projectileSpawnY,
                    PreviousX = attackOrigin.X,
                    PreviousY = projectileSpawnY,
                    FacingRight = facingRight,
                    SpawnTime = spawnTime,
                    OwnerId = 0,
                    OwnerX = attackOrigin.X,
                    OwnerY = attackOrigin.Y,
                    UsesAuthoredShootPoint = usesAuthoredShootPoint,
                    FallbackShootPointYOffset = fallbackShootPointYOffset,
                    HasExplicitAttackOriginOverride = attackOriginOverride.HasValue,
                    PreferredTargetMobId = projectilePreferredTargetMobId,
                    PreferredTargetPosition = projectilePreferredTargetPosition,
                    PreferStoredTargetPosition = attackOriginOverride.HasValue
                        && preferredTargetPositionOverride.HasValue
                        && (!skill.MultiTargeting || !liveProjectilePreferredTargetPosition.HasValue)
                        && ShouldUseSmoothingMovingShoot(skill),
                    ResolvedShootAmmoSelection = resolvedShootAmmoSelection?.Snapshot(),
                    ResolvedShootWeaponCode = resolvedShootWeaponCode,
                    ResolvedShootWeaponItemId = resolvedShootWeaponItemId,
                    AllowFollowUpQueue = queueFollowUps,
                    ForceCritical = forceCriticalForAttack,
                    QueuedFacingRight = facingRight,
                    IsQueuedFinalAttack = isQueuedFinalAttack,
                    IsQueuedSparkAttack = isQueuedSparkAttack
                };

                // Set velocity
                proj.VelocityX = facingRight ? speed : -speed;
                proj.VelocityY = 0;

                bool aimedAtTarget = TryAimProjectileAtTarget(proj, currentTime, speed);

                // Preserve the existing spread fallback when no live target is available.
                if (!aimedAtTarget && bulletCount > 1)
                {
                    float spreadAngle = (i - (bulletCount - 1) / 2f) * 10f * MathF.PI / 180f;
                    proj.VelocityX = speed * MathF.Cos(spreadAngle) * (facingRight ? 1 : -1);
                    proj.VelocityY = speed * MathF.Sin(spreadAngle);
                }

                if (spawnTime <= currentTime)
                {
                    _projectiles.Add(proj);
                }
                else
                {
                    InsertPendingProjectileSpawn(proj, spawnTime);
                }
            }
        }

        private List<int?> ResolveMultiTargetProjectileAssignments(
            SkillData skill,
            int level,
            SkillLevelData levelData,
            int currentTime,
            bool facingRight,
            int bulletCount,
            int? preferredTargetMobId,
            Vector2? preferredTargetPositionOverride,
            Vector2 attackOrigin)
        {
            var assignments = new List<int?>(Math.Max(0, bulletCount));
            if (bulletCount <= 0)
            {
                return assignments;
            }

            if (!skill.MultiTargeting || _mobPool == null)
            {
                for (int i = 0; i < bulletCount; i++)
                {
                    assignments.Add(preferredTargetMobId);
                }

                return assignments;
            }

            int selectionCount = Math.Max(bulletCount, Math.Max(1, levelData?.MobCount ?? 1));
            Rectangle targetingHitbox = GetWorldAttackHitbox(
                skill,
                level,
                levelData,
                AttackResolutionMode.Ranged,
                facingRight,
                attackOrigin);
            List<MobItem> orderedTargets = ResolveTargetsInHitbox(
                targetingHitbox,
                currentTime,
                selectionCount,
                AttackResolutionMode.Ranged,
                facingRight,
                preferredTargetMobId,
                preferredTargetPositionOverride,
                attackOrigin);

            var assignedTargetIds = new List<int>(orderedTargets.Count);
            var seenTargetIds = new HashSet<int>();
            foreach (MobItem target in orderedTargets)
            {
                if (target != null && seenTargetIds.Add(target.PoolId))
                {
                    assignedTargetIds.Add(target.PoolId);
                }
            }

            if (preferredTargetMobId.HasValue
                && seenTargetIds.Add(preferredTargetMobId.Value)
                && FindAttackableMobByPoolId(preferredTargetMobId.Value, currentTime) != null)
            {
                assignedTargetIds.Insert(0, preferredTargetMobId.Value);
            }

            for (int i = 0; i < bulletCount; i++)
            {
                if (i < assignedTargetIds.Count)
                {
                    assignments.Add(assignedTargetIds[i]);
                }
                else if (assignedTargetIds.Count > 0)
                {
                    assignments.Add(assignedTargetIds[0]);
                }
                else
                {
                    assignments.Add(preferredTargetMobId);
                }
            }

            return assignments;
        }

        internal static Vector2? ResolveDeferredProjectileTargetPositionSnapshot(
            Vector2? storedPreferredTargetPosition,
            Vector2? livePreferredTargetPosition,
            bool preferStoredTargetPosition = true)
        {
            // `m_movingShootEntry` carries one queued world point, but client-shaped
            // multi-target volleys should still keep their per-projectile live target
            // snapshots when those targets were resolved before spawn.
            return preferStoredTargetPosition
                ? storedPreferredTargetPosition ?? livePreferredTargetPosition
                : livePreferredTargetPosition ?? storedPreferredTargetPosition;
        }

        private bool TryAimProjectileAtTarget(ActiveProjectile proj, int currentTime, float speed)
        {
            if (proj == null)
            {
                return false;
            }

            if (TryResolveProjectilePreferredTargetPoint(proj, currentTime, out Vector2 targetPoint))
            {
                float deltaX = targetPoint.X - proj.X;
                float deltaY = targetPoint.Y - proj.Y;
                float distance = MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
                if (distance <= 0.001f)
                {
                    return false;
                }

                proj.VelocityX = deltaX / distance * speed;
                proj.VelocityY = deltaY / distance * speed;
                proj.FacingRight = proj.VelocityX >= 0f;
                return true;
            }

            return false;
        }

        internal static bool ShouldPreferStoredProjectileTargetPosition(ActiveProjectile proj)
        {
            return proj?.PreferStoredTargetPosition == true && proj.PreferredTargetPosition.HasValue;
        }

        internal static bool TryResolvePreferredProjectileTargetPoint(
            ActiveProjectile proj,
            Vector2? liveTargetPoint,
            out Vector2 targetPoint)
        {
            targetPoint = default;
            if (ShouldPreferStoredProjectileTargetPosition(proj))
            {
                targetPoint = proj.PreferredTargetPosition.Value;
                return true;
            }

            if (liveTargetPoint.HasValue)
            {
                targetPoint = liveTargetPoint.Value;
                return true;
            }

            if (proj?.PreferredTargetPosition.HasValue == true)
            {
                targetPoint = proj.PreferredTargetPosition.Value;
                return true;
            }

            return false;
        }

        private bool TryResolveProjectilePreferredTargetPoint(ActiveProjectile proj, int currentTime, out Vector2 targetPoint)
        {
            Vector2? liveTargetPoint = null;
            if (proj?.PreferredTargetMobId is int preferredTargetMobId)
            {
                MobItem target = FindAttackableMobByPoolId(preferredTargetMobId, currentTime);
                if (target != null)
                {
                    Rectangle targetHitbox = GetMobHitbox(target, currentTime);
                    if (!targetHitbox.IsEmpty)
                    {
                        liveTargetPoint = new Vector2(
                            targetHitbox.Left + targetHitbox.Width * 0.5f,
                            targetHitbox.Top + targetHitbox.Height * 0.5f);
                    }
                }
            }

            return TryResolvePreferredProjectileTargetPoint(proj, liveTargetPoint, out targetPoint);
        }

        private static float GetStoredPreferredTargetDistanceSq(ActiveProjectile proj, float centerX, float centerY)
        {
            if (proj?.PreferredTargetPosition.HasValue != true)
            {
                return float.MaxValue;
            }

            Vector2 preferredTargetPosition = proj.PreferredTargetPosition.Value;
            float preferredDeltaX = centerX - preferredTargetPosition.X;
            float preferredDeltaY = centerY - preferredTargetPosition.Y;
            return (preferredDeltaX * preferredDeltaX) + (preferredDeltaY * preferredDeltaY);
        }

        private bool HasLivePreferredProjectileTarget(ActiveProjectile proj, int currentTime, int maxHits)
        {
            if (ShouldPreferStoredProjectileTargetPosition(proj))
            {
                return false;
            }

            if (proj?.PreferredTargetMobId is not int preferredMobId)
            {
                return false;
            }

            MobItem preferredTarget = FindAttackableMobByPoolId(preferredMobId, currentTime);
            return preferredTarget != null && proj.CanHitMob(preferredTarget.PoolId, maxHits);
        }

        internal static List<int> BuildProjectileReleaseSchedule(SkillLevelData levelData, int bulletCount)
        {
            var releaseSchedule = new List<int>(Math.Max(0, bulletCount));
            if (bulletCount <= 0)
            {
                return releaseSchedule;
            }

            List<int> delayEntries = levelData?.ProjectileSpawnDelaysMs;
            if (delayEntries == null || delayEntries.Count == 0)
            {
                for (int i = 0; i < bulletCount; i++)
                {
                    releaseSchedule.Add(0);
                }

                return releaseSchedule;
            }

            bool firstProjectileUsesDelay = delayEntries.Count >= bulletCount;
            int delayIndex = 0;
            int cumulativeDelay = 0;

            for (int i = 0; i < bulletCount; i++)
            {
                // WZ mixes `ballDelay` layouts: some skills provide one interval per follow-up shot,
                // while others include the initial release delay as the first entry.
                if (i == 0 && !firstProjectileUsesDelay)
                {
                    releaseSchedule.Add(0);
                    continue;
                }

                int mappedIndex = Math.Min(delayIndex, delayEntries.Count - 1);
                cumulativeDelay += Math.Max(0, delayEntries[mappedIndex]);
                releaseSchedule.Add(cumulativeDelay);
                delayIndex++;
            }

            return releaseSchedule;
        }

        private void InsertPendingProjectileSpawn(ActiveProjectile projectile, int executeTime)
        {
            var pending = new PendingProjectileSpawn
            {
                Projectile = projectile,
                ExecuteTime = executeTime
            };

            int insertIndex = _pendingProjectileSpawns.Count;
            while (insertIndex > 0 && _pendingProjectileSpawns[insertIndex - 1].ExecuteTime > executeTime)
            {
                insertIndex--;
            }

            _pendingProjectileSpawns.Insert(insertIndex, pending);
        }

        private static float GetProjectileSpeed(ProjectileData projectileData, SkillLevelData levelData)
        {
            float speed = levelData?.BulletSpeed > 0
                ? levelData.BulletSpeed
                : projectileData?.Speed ?? 0f;
            return speed > 0f ? speed : 400f;
        }

        private void UpdateProjectiles(int currentTime, float deltaTime)
        {
            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                var proj = _projectiles[i];
                UpdateProjectileBehavior(proj, currentTime);
                proj.Update(deltaTime, currentTime);

                // Check for expired
                if (proj.IsExpired)
                {
                    _projectiles.RemoveAt(i);
                    continue;
                }

                // Skip if exploding (just playing animation)
                if (proj.IsExploding)
                    continue;

                // Check mob collisions
                if (!proj.VisualOnly && _mobPool != null)
                {
                    CheckProjectileCollisions(proj, currentTime);
                }
            }
        }

        private void UpdateProjectileBehavior(ActiveProjectile proj, int currentTime)
        {
            if (proj?.Data == null || proj.IsExploding || _mobPool == null)
                return;

            if (!HasHomingBehavior(proj))
                return;

            MobItem target = FindHomingProjectileTarget(proj, currentTime);
            if (target == null)
                return;

            Rectangle targetHitbox = GetMobHitbox(target, currentTime);
            float targetX = targetHitbox.Left + targetHitbox.Width * 0.5f;
            float targetY = targetHitbox.Top + targetHitbox.Height * 0.5f;
            float deltaX = targetX - proj.X;
            float deltaY = targetY - proj.Y;
            float distance = MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
            if (distance <= 0.001f)
                return;

            float speed = GetProjectileSpeed(proj.Data, proj.LevelData);
            proj.VelocityX = deltaX / distance * speed;
            proj.VelocityY = deltaY / distance * speed;
            proj.FacingRight = proj.VelocityX >= 0f;
        }

        private MobItem FindHomingProjectileTarget(ActiveProjectile proj, int currentTime)
        {
            int maxHits = GetEffectiveProjectileHitLimit(proj);
            bool hasLivePreferredTarget = HasLivePreferredProjectileTarget(proj, currentTime, maxHits);
            if (!ShouldPreferStoredProjectileTargetPosition(proj)
                && proj?.PreferredTargetMobId is int preferredMobId)
            {
                MobItem preferredTarget = FindAttackableMobByPoolId(preferredMobId, currentTime);
                if (preferredTarget != null && proj.CanHitMob(preferredTarget.PoolId, maxHits))
                {
                    return preferredTarget;
                }
            }

            return _mobPool.ActiveMobs
                .Where(IsMobAttackable)
                .Where(mob => proj.CanHitMob(mob.PoolId, maxHits))
                .Select(mob => new { Mob = mob, Hitbox = GetMobHitbox(mob, currentTime) })
                .Where(entry => !entry.Hitbox.IsEmpty)
                .Select(entry =>
                {
                    float centerX = entry.Hitbox.Left + entry.Hitbox.Width * 0.5f;
                    float centerY = entry.Hitbox.Top + entry.Hitbox.Height * 0.5f;
                    float deltaX = centerX - proj.X;
                    float deltaY = centerY - proj.Y;
                    float distanceSq = deltaX * deltaX + deltaY * deltaY;
                    float forward = proj.VelocityX >= 0f ? deltaX : -deltaX;
                    float forwardPenalty = forward < 0f ? 100000f + MathF.Abs(forward) : forward;
                    float preferredDistanceSq = hasLivePreferredTarget
                        ? float.MaxValue
                        : GetStoredPreferredTargetDistanceSq(proj, centerX, centerY);
                    return new
                    {
                        entry.Mob,
                        PreferredDistanceSq = preferredDistanceSq,
                        DistanceSq = distanceSq,
                        ForwardPenalty = forwardPenalty
                    };
                })
                .OrderBy(entry => entry.PreferredDistanceSq)
                .ThenBy(entry => entry.ForwardPenalty)
                .ThenBy(entry => entry.DistanceSq)
                .Select(entry => entry.Mob)
                .FirstOrDefault();
        }

        private void CheckProjectileCollisions(ActiveProjectile proj, int currentTime)
        {
            if (_mobPool == null)
                return;

            SkillData skill = GetSkillData(proj.SkillId);
            if (skill == null)
            {
                proj.IsExpired = true;
                return;
            }

            Rectangle projHitbox = GetProjectileHitbox(proj, currentTime);
            NotifyAttackAreaResolved(projHitbox, currentTime, proj.SkillId);
            int maxTargets = GetEffectiveProjectileHitLimit(proj);
            int attackCount = Math.Max(1, proj.LevelData?.AttackCount ?? 1);
            var mobsToKill = new List<MobItem>();

            List<MobItem> collisionTargets = ResolveProjectileCollisionTargets(proj, projHitbox, currentTime, maxTargets)
                .ToList();
            if (collisionTargets.Count == 0)
                return;

            bool shouldQueueSerialAttack = proj.AllowFollowUpQueue && ShouldUseQueuedSerialAttack(skill);
            if (shouldQueueSerialAttack)
            {
                QueueOrReplaceSerialAttack(skill, proj.SkillLevel, currentTime, collisionTargets, proj.FacingRight);
                collisionTargets = collisionTargets.Take(1).ToList();
            }

            foreach (MobItem mob in collisionTargets)
            {
                if (!proj.CanHitMob(mob.PoolId, maxTargets))
                    continue;

                proj.RegisterHit(mob.PoolId, currentTime, maxTargets);

                bool died = ApplySkillAttackToMob(
                    skill,
                    proj.SkillLevel,
                    proj.LevelData,
                    mob,
                    currentTime,
                    attackCount,
                    AttackResolutionMode.Projectile,
                    proj.Data.HitAnimation ?? skill.HitEffect,
                    proj.FacingRight,
                    proj.HitCount - 1,
                    proj.ForceCritical);

                if (proj.AllowFollowUpQueue)
                {
                    TryQueueFollowUpAttack(skill, currentTime, mob.PoolId, proj.FacingRight);
                    proj.AllowFollowUpQueue = false;
                }

                TryQueueAttackTriggeredBuffProc(skill, currentTime, mob, proj.FacingRight);

                if (skill.RectBasedOnTarget)
                {
                    ApplyProjectileTargetRectSplash(
                        proj,
                        skill,
                        mob,
                        currentTime,
                        attackCount,
                        maxTargets,
                        mobsToKill);
                }

                if (skill.ChainAttack && !ShouldUseQueuedSerialAttack(skill))
                {
                    ApplyProjectileChainAttack(
                        proj,
                        skill,
                        mob,
                        currentTime,
                        attackCount,
                        maxTargets,
                        mobsToKill);
                    proj.IsExpired = true;
                    break;
                }

                if (ShouldDetonateProjectileOnImpact(proj))
                {
                    ApplyProjectileExplosionSplash(
                        proj,
                        skill,
                        currentTime,
                        attackCount,
                        maxTargets,
                        mobsToKill);
                    break;
                }

                if (died && !mobsToKill.Contains(mob))
                {
                    mobsToKill.Add(mob);
                }

                OnProjectileHit?.Invoke(proj, mob);

                if (proj.IsExploding || proj.IsExpired || proj.HitCount >= maxTargets)
                    break;
            }

            foreach (MobItem mob in mobsToKill)
            {
                HandleMobDeath(mob, currentTime);
            }
        }

        private void ApplyProjectileChainAttack(
            ActiveProjectile proj,
            SkillData skill,
            MobItem anchorMob,
            int currentTime,
            int attackCount,
            int maxTargets,
            List<MobItem> mobsToKill)
        {
            if (_mobPool == null || proj?.LevelData == null || skill == null || anchorMob == null)
                return;

            int remainingHits = maxTargets - proj.HitCount;
            if (remainingHits <= 0)
                return;

            Vector2 previousPoint = GetMobHitboxCenter(anchorMob, currentTime);
            foreach (MobItem mob in ResolveProjectileChainTargets(proj, anchorMob, currentTime, remainingHits, maxTargets))
            {
                if (!proj.CanHitMob(mob.PoolId, maxTargets))
                    continue;

                Vector2 currentPoint = GetMobHitboxCenter(mob, currentTime);
                bool impactFacingRight = currentPoint.X >= previousPoint.X;
                proj.RegisterHit(mob.PoolId, currentTime, maxTargets);

                bool died = ApplySkillAttackToMob(
                    skill,
                    proj.SkillLevel,
                    proj.LevelData,
                    mob,
                    currentTime,
                    attackCount,
                    AttackResolutionMode.Projectile,
                    proj.Data.HitAnimation ?? skill.HitEffect,
                    impactFacingRight,
                    proj.HitCount - 1,
                    proj.ForceCritical);

                if (died && !mobsToKill.Contains(mob))
                {
                    mobsToKill.Add(mob);
                }

                previousPoint = currentPoint;

                if (proj.IsExpired || proj.HitCount >= maxTargets)
                    break;
            }
        }

        private void ApplyProjectileTargetRectSplash(
            ActiveProjectile proj,
            SkillData skill,
            MobItem anchorMob,
            int currentTime,
            int attackCount,
            int maxTargets,
            List<MobItem> mobsToKill)
        {
            if (_mobPool == null || proj?.LevelData == null || skill == null || anchorMob == null)
                return;

            var anchorQueue = new Queue<MobItem>();
            var usedAnchors = new HashSet<int> { anchorMob.PoolId };
            anchorQueue.Enqueue(anchorMob);

            while (anchorQueue.Count > 0)
            {
                MobItem currentAnchor = anchorQueue.Dequeue();
                int remainingHits = maxTargets - proj.HitCount;
                if (remainingHits <= 0)
                    break;

                foreach (MobItem mob in ResolveProjectileTargetRectTargets(proj, skill, currentAnchor, currentTime, remainingHits, maxTargets))
                {
                    if (!proj.CanHitMob(mob.PoolId, maxTargets))
                        continue;

                    proj.RegisterHit(mob.PoolId, currentTime, maxTargets);

                    bool died = ApplySkillAttackToMob(
                        skill,
                        proj.SkillLevel,
                        proj.LevelData,
                        mob,
                        currentTime,
                        attackCount,
                        AttackResolutionMode.Projectile,
                        proj.Data.HitAnimation ?? skill.HitEffect,
                        proj.FacingRight,
                        proj.HitCount - 1,
                        proj.ForceCritical);

                    if (died && !mobsToKill.Contains(mob))
                    {
                        mobsToKill.Add(mob);
                    }

                    if (skill.MultiTargeting && usedAnchors.Add(mob.PoolId))
                    {
                        anchorQueue.Enqueue(mob);
                    }

                    if (proj.IsExpired || proj.HitCount >= maxTargets)
                        break;
                }

                if (!skill.MultiTargeting || proj.IsExpired || proj.HitCount >= maxTargets)
                    break;
            }
        }

        private void ApplyProjectileExplosionSplash(
            ActiveProjectile proj,
            SkillData skill,
            int currentTime,
            int attackCount,
            int maxTargets,
            List<MobItem> mobsToKill)
        {
            if (proj?.Data == null)
                return;

            float radius = proj.Data.ExplosionRadius;
            if (radius > 0f)
            {
                var explosionBounds = new Rectangle(
                    (int)MathF.Round(proj.X - radius),
                    (int)MathF.Round(proj.Y - radius),
                    Math.Max(1, (int)MathF.Round(radius * 2f)),
                    Math.Max(1, (int)MathF.Round(radius * 2f)));
                NotifyAttackAreaResolved(explosionBounds, currentTime, proj.SkillId);
            }

            if (!proj.IsExploding && !proj.IsExpired)
            {
                proj.Explode(currentTime);
            }

            int remainingHits = maxTargets - proj.HitCount;
            if (remainingHits <= 0 || _mobPool == null)
                return;

            foreach (MobItem mob in ResolveProjectileExplosionTargets(proj, currentTime, remainingHits, maxTargets))
            {
                if (!proj.CanHitMob(mob.PoolId, maxTargets))
                    continue;

                proj.RegisterHit(mob.PoolId, currentTime, maxTargets);

                bool died = ApplySkillAttackToMob(
                    skill,
                    proj.SkillLevel,
                    proj.LevelData,
                    mob,
                    currentTime,
                    attackCount,
                    AttackResolutionMode.Projectile,
                    proj.Data.HitAnimation ?? skill.HitEffect,
                    proj.FacingRight,
                    proj.HitCount - 1,
                    proj.ForceCritical);

                if (died && !mobsToKill.Contains(mob))
                {
                    mobsToKill.Add(mob);
                }

                if (proj.IsExpired || proj.HitCount >= maxTargets)
                    break;
            }
        }

        private List<MobItem> ResolveProjectileCollisionTargets(ActiveProjectile proj, Rectangle projHitbox, int currentTime, int maxTargets)
        {
            if (_mobPool == null || maxTargets <= 0)
                return new List<MobItem>();

            Vector2 travel = GetProjectileCollisionTravelVector(proj);
            float speedSq = travel.LengthSquared();
            float speed = speedSq > 0.001f ? MathF.Sqrt(speedSq) : 0f;
            bool hasLivePreferredTarget = HasLivePreferredProjectileTarget(proj, currentTime, maxTargets);

            return _mobPool.ActiveMobs
                .Where(IsMobAttackable)
                .Where(mob => proj.CanHitMob(mob.PoolId, maxTargets))
                .Select(mob => new { Mob = mob, Hitbox = GetMobHitbox(mob, currentTime) })
                .Where(entry => !entry.Hitbox.IsEmpty && projHitbox.Intersects(entry.Hitbox))
                .Select(entry =>
                {
                    float centerX = entry.Hitbox.Left + entry.Hitbox.Width * 0.5f;
                    float centerY = entry.Hitbox.Top + entry.Hitbox.Height * 0.5f;
                    float deltaX = centerX - proj.PreviousX;
                    float deltaY = centerY - proj.PreviousY;
                    float progress = speedSq > 0.001f
                        ? ((deltaX * travel.X) + (deltaY * travel.Y)) / speedSq
                        : MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
                    float progressPenalty = progress < 0f ? 100000f + MathF.Abs(progress) : progress;
                    float lateralOffset = speed > 0.001f
                        ? MathF.Abs((deltaX * travel.Y) - (deltaY * travel.X)) / speed
                        : 0f;
                    float currentDeltaX = centerX - proj.X;
                    float currentDeltaY = centerY - proj.Y;
                    float distanceSq = (currentDeltaX * currentDeltaX) + (currentDeltaY * currentDeltaY);
                    float preferredDistanceSq = hasLivePreferredTarget
                        ? float.MaxValue
                        : GetStoredPreferredTargetDistanceSq(proj, centerX, centerY);

                    return new
                    {
                        entry.Mob,
                        Preferred = !ShouldPreferStoredProjectileTargetPosition(proj)
                            && proj.PreferredTargetMobId.HasValue
                            && entry.Mob.PoolId == proj.PreferredTargetMobId.Value
                                ? 0
                                : 1,
                        PreferredDistanceSq = preferredDistanceSq,
                        ProgressPenalty = progressPenalty,
                        LateralOffset = lateralOffset,
                        DistanceSq = distanceSq
                    };
                })
                .OrderBy(entry => entry.Preferred)
                .ThenBy(entry => entry.PreferredDistanceSq)
                .ThenBy(entry => entry.ProgressPenalty)
                .ThenBy(entry => entry.LateralOffset)
                .ThenBy(entry => entry.DistanceSq)
                .Take(maxTargets)
                .Select(entry => entry.Mob)
                .ToList();
        }

        private List<MobItem> ResolveProjectileExplosionTargets(ActiveProjectile proj, int currentTime, int maxTargets, int hitLimit)
        {
            if (_mobPool == null || proj?.Data == null || maxTargets <= 0)
                return new List<MobItem>();

            float radius = proj.Data.ExplosionRadius;
            if (radius <= 0f)
                return new List<MobItem>();

            float radiusSq = radius * radius;
            bool hasLivePreferredTarget = HasLivePreferredProjectileTarget(proj, currentTime, hitLimit);
            return _mobPool.ActiveMobs
                .Where(IsMobAttackable)
                .Where(mob => proj.CanHitMob(mob.PoolId, hitLimit))
                .Select(mob => new { Mob = mob, Hitbox = GetMobHitbox(mob, currentTime) })
                .Where(entry => !entry.Hitbox.IsEmpty)
                .Select(entry =>
                {
                    float centerX = entry.Hitbox.Left + entry.Hitbox.Width * 0.5f;
                    float centerY = entry.Hitbox.Top + entry.Hitbox.Height * 0.5f;
                    float deltaX = centerX - proj.X;
                    float deltaY = centerY - proj.Y;
                    float distanceSq = (deltaX * deltaX) + (deltaY * deltaY);
                    float preferredDistanceSq = hasLivePreferredTarget
                        ? float.MaxValue
                        : GetStoredPreferredTargetDistanceSq(proj, centerX, centerY);
                    return new { entry.Mob, PreferredDistanceSq = preferredDistanceSq, DistanceSq = distanceSq };
                })
                .Where(entry => entry.DistanceSq <= radiusSq)
                .OrderBy(entry => entry.PreferredDistanceSq)
                .ThenBy(entry => entry.DistanceSq)
                .Take(maxTargets)
                .Select(entry => entry.Mob)
                .ToList();
        }

        private List<MobItem> ResolveProjectileTargetRectTargets(
            ActiveProjectile proj,
            SkillData skill,
            MobItem anchorMob,
            int currentTime,
            int maxTargets,
            int hitLimit)
        {
            if (_mobPool == null || proj?.LevelData == null || skill == null || anchorMob == null || maxTargets <= 0)
                return new List<MobItem>();

            Rectangle anchorHitbox = GetMobHitbox(anchorMob, currentTime);
            if (anchorHitbox.IsEmpty)
                return new List<MobItem>();

            Rectangle targetRect = skill.GetAttackRange(proj.SkillLevel, proj.FacingRight);
            if (targetRect.Width <= 0 || targetRect.Height <= 0)
            {
                targetRect = GetDefaultMagicHitbox(skill, proj.LevelData, proj.FacingRight);
            }

            float anchorX = anchorHitbox.Left + anchorHitbox.Width * 0.5f;
            float anchorY = anchorHitbox.Top + anchorHitbox.Height * 0.5f;
            Rectangle worldRect = new Rectangle(
                (int)MathF.Round(anchorX + targetRect.X),
                (int)MathF.Round(anchorY + targetRect.Y),
                Math.Max(1, targetRect.Width),
                Math.Max(1, targetRect.Height));
            bool hasLivePreferredTarget = HasLivePreferredProjectileTarget(proj, currentTime, hitLimit);

            return _mobPool.ActiveMobs
                .Where(IsMobAttackable)
                .Where(mob => mob.PoolId != anchorMob.PoolId && proj.CanHitMob(mob.PoolId, hitLimit))
                .Select(mob => new { Mob = mob, Hitbox = GetMobHitbox(mob, currentTime) })
                .Where(entry => !entry.Hitbox.IsEmpty && worldRect.Intersects(entry.Hitbox))
                .Select(entry =>
                {
                    float centerX = entry.Hitbox.Left + entry.Hitbox.Width * 0.5f;
                    float centerY = entry.Hitbox.Top + entry.Hitbox.Height * 0.5f;
                    float deltaX = centerX - anchorX;
                    float deltaY = centerY - anchorY;
                    float forward = proj.FacingRight ? deltaX : -deltaX;
                    float forwardPenalty = forward < 0f ? 100000f + MathF.Abs(forward) : forward;
                    float distanceSq = (deltaX * deltaX) + (deltaY * deltaY);
                    float preferredDistanceSq = hasLivePreferredTarget
                        ? float.MaxValue
                        : GetStoredPreferredTargetDistanceSq(proj, centerX, centerY);

                    return new
                    {
                        entry.Mob,
                        PreferredDistanceSq = preferredDistanceSq,
                        ForwardPenalty = forwardPenalty,
                        DistanceSq = distanceSq,
                        VerticalDistance = MathF.Abs(deltaY)
                    };
                })
                .OrderBy(entry => entry.PreferredDistanceSq)
                .ThenBy(entry => entry.ForwardPenalty)
                .ThenBy(entry => entry.DistanceSq)
                .ThenBy(entry => entry.VerticalDistance)
                .Take(maxTargets)
                .Select(entry => entry.Mob)
                .ToList();
        }

        private List<MobItem> ResolveProjectileChainTargets(
            ActiveProjectile proj,
            MobItem anchorMob,
            int currentTime,
            int maxTargets,
            int hitLimit)
        {
            if (_mobPool == null || proj?.LevelData == null || anchorMob == null || maxTargets <= 0)
                return new List<MobItem>();

            Vector2 origin = GetMobHitboxCenter(anchorMob, currentTime);
            float bounceRange = Math.Max(40f, proj.LevelData.Range > 0 ? proj.LevelData.Range : 150f);
            var excludedMobIds = new HashSet<int>(proj.HitMobIds) { anchorMob.PoolId };
            return ResolveSequentialChainTargets(
                origin,
                bounceRange,
                maxTargets,
                excludedMobIds,
                preferredOrderByMobId: null,
                currentTime,
                mob => proj.CanHitMob(mob.PoolId, hitLimit));
        }

        private List<MobItem> ResolveSequentialChainTargets(
            Vector2 origin,
            float bounceRange,
            int maxTargets,
            HashSet<int> excludedMobIds,
            Dictionary<int, int> preferredOrderByMobId,
            int currentTime,
            Func<MobItem, bool> additionalFilter = null)
        {
            if (_mobPool == null || maxTargets <= 0)
                return new List<MobItem>();

            float bounceRangeSq = bounceRange * bounceRange;
            var chainedTargets = new List<MobItem>(maxTargets);
            var excluded = excludedMobIds != null
                ? new HashSet<int>(excludedMobIds)
                : new HashSet<int>();

            for (int bounce = 0; bounce < maxTargets; bounce++)
            {
                MobItem bestMob = null;
                Vector2 bestPoint = origin;
                float bestDistanceSq = float.MaxValue;
                int bestPreferredOrder = int.MaxValue;
                float bestVerticalDistance = float.MaxValue;

                foreach (MobItem mob in _mobPool.ActiveMobs)
                {
                    if (!IsMobAttackable(mob)
                        || excluded.Contains(mob.PoolId)
                        || (additionalFilter != null && !additionalFilter(mob)))
                    {
                        continue;
                    }

                    Rectangle hitbox = GetMobHitbox(mob, currentTime);
                    if (hitbox.IsEmpty)
                        continue;

                    float centerX = hitbox.Left + hitbox.Width * 0.5f;
                    float centerY = hitbox.Top + hitbox.Height * 0.5f;
                    float deltaX = centerX - origin.X;
                    float deltaY = centerY - origin.Y;
                    float distanceSq = (deltaX * deltaX) + (deltaY * deltaY);
                    if (distanceSq > bounceRangeSq)
                        continue;

                    int preferredOrder = int.MaxValue;
                    if (preferredOrderByMobId != null
                        && preferredOrderByMobId.TryGetValue(mob.PoolId, out int mappedOrder))
                    {
                        preferredOrder = mappedOrder;
                    }

                    float verticalDistance = MathF.Abs(deltaY);
                    if (preferredOrder > bestPreferredOrder
                        || (preferredOrder == bestPreferredOrder && distanceSq > bestDistanceSq)
                        || (preferredOrder == bestPreferredOrder && Math.Abs(distanceSq - bestDistanceSq) < 0.001f && verticalDistance >= bestVerticalDistance))
                    {
                        continue;
                    }

                    bestMob = mob;
                    bestPoint = new Vector2(centerX, centerY);
                    bestDistanceSq = distanceSq;
                    bestPreferredOrder = preferredOrder;
                    bestVerticalDistance = verticalDistance;
                }

                if (bestMob == null)
                    break;

                chainedTargets.Add(bestMob);
                excluded.Add(bestMob.PoolId);
                origin = bestPoint;
            }

            return chainedTargets;
        }

        private static int GetEffectiveProjectileHitLimit(ActiveProjectile proj)
        {
            return Math.Max(1, Math.Max(proj?.LevelData?.MobCount ?? 0, proj?.Data?.MaxHits ?? 0));
        }

        private static bool HasHomingBehavior(ActiveProjectile proj)
        {
            return proj?.Data != null
                   && (proj.Data.Homing || proj.Data.Behavior == ProjectileBehavior.Homing);
        }

        private static bool ShouldDetonateProjectileOnImpact(ActiveProjectile proj)
        {
            return proj?.Data != null
                   && (proj.Data.Behavior == ProjectileBehavior.Exploding
                       || proj.Data.ExplosionRadius > 0f
                       || proj.Data.ExplosionAnimation != null);
        }

        private Rectangle GetProjectileHitbox(ActiveProjectile proj, int currentTime)
        {
            if (proj?.Data == null)
                return Rectangle.Empty;

            SkillAnimation animation = proj.IsExploding ? proj.Data.ExplosionAnimation : proj.Data.Animation;
            int animationTime = proj.IsExploding ? currentTime - proj.ExplodeTime : currentTime - proj.SpawnTime;
            SkillFrame frame = animation?.GetFrameAtTime(animationTime);
            Rectangle currentFrameBounds = GetProjectileFrameBounds(proj, frame, proj.X, proj.Y);
            Rectangle previousFrameBounds = proj.IsExploding
                ? currentFrameBounds
                : GetProjectileFrameBounds(proj, frame, proj.PreviousX, proj.PreviousY);

            return UnionRectangles(currentFrameBounds, previousFrameBounds);
        }

        private Rectangle GetProjectileFrameBounds(ActiveProjectile proj, SkillFrame frame, float anchorX, float anchorY)
        {
            if (proj == null)
            {
                return Rectangle.Empty;
            }

            int width = frame?.Bounds.Width ?? 0;
            int height = frame?.Bounds.Height ?? 0;
            if (width <= 0 || height <= 0)
            {
                return new Rectangle((int)anchorX - 10, (int)anchorY - 10, 20, 20);
            }

            bool shouldFlip = ResolveProjectileFrameShouldFlip(proj, frame);
            int drawX = GetFrameDrawX((int)MathF.Round(anchorX), frame, shouldFlip);
            int drawY = (int)MathF.Round(anchorY) - frame.Origin.Y;

            return new Rectangle(
                drawX,
                drawY,
                Math.Max(12, width),
                Math.Max(12, height));
        }

        private static Rectangle UnionRectangles(Rectangle first, Rectangle second)
        {
            if (first.IsEmpty)
            {
                return second;
            }

            if (second.IsEmpty)
            {
                return first;
            }

            int left = Math.Min(first.Left, second.Left);
            int top = Math.Min(first.Top, second.Top);
            int right = Math.Max(first.Right, second.Right);
            int bottom = Math.Max(first.Bottom, second.Bottom);

            return new Rectangle(
                left,
                top,
                Math.Max(1, right - left),
                Math.Max(1, bottom - top));
        }

        private static Vector2 GetProjectileCollisionTravelVector(ActiveProjectile proj)
        {
            if (proj == null)
            {
                return Vector2.Zero;
            }

            Vector2 travel = new Vector2(proj.X - proj.PreviousX, proj.Y - proj.PreviousY);
            if (travel.LengthSquared() > 0.001f)
            {
                return travel;
            }

            return new Vector2(proj.VelocityX, proj.VelocityY);
        }
        public IReadOnlyList<ActiveProjectile> ActiveProjectiles => _projectiles;
        #endregion
        #region Summon System
        private void SpawnSummon(SkillData skill, int level, int currentTime)
        {
            var levelData = skill.GetLevel(level);
            if (levelData == null)
                return;

            List<ActiveSummon> sameSkillSummons = _summons
                .Where(summon => summon?.SkillId == skill.SkillId)
                .ToList();
            int maxConcurrentSummons = ResolveMaxConcurrentSummons(skill, levelData);

            if (maxConcurrentSummons <= 1)
            {
                for (int i = _summons.Count - 1; i >= 0; i--)
                {
                    if (_summons[i].SkillId == skill.SkillId)
                    {
                        RemoveSummonAt(i, cancelTimer: true);
                    }
                }
            }
            else if (IsSatelliteSummonSkill(skill.SkillId) && sameSkillSummons.Count >= maxConcurrentSummons)
            {
                RemoveSummonsBySkill(skill.SkillId);
                return;
            }
            else if (skill.SkillId == TESLA_COIL_SKILL_ID)
            {
                RemoveSummonsBySkill(skill.SkillId);
                sameSkillSummons.Clear();
            }

            int durationMs = ResolveSummonDurationMs(skill, levelData, level);
            int summonCountToSpawn = ResolveSummonSpawnCount(skill, maxConcurrentSummons, sameSkillSummons.Count);
            for (int spawnIndex = 0; spawnIndex < summonCountToSpawn; spawnIndex++)
            {
                int instanceIndex = sameSkillSummons.Count + spawnIndex;
                Vector2 spawnPosition = SummonMovementResolver.ResolveSpawnPositionForInstance(
                    skill.SkillId,
                    skill.SummonMovementStyle,
                    skill.SummonSpawnDistanceX,
                    _player.Position,
                    _player.FacingRight,
                    instanceIndex,
                    summonCountToSpawn);
                spawnPosition = SettleSummonOnFoothold(skill.SummonMovementStyle, spawnPosition);

                var summon = new ActiveSummon
                {
                    ObjectId = _nextSummonId++,
                    SummonSlotIndex = instanceIndex,
                    SkillId = skill.SkillId,
                    Level = level,
                    StartTime = currentTime,
                    Duration = durationMs,
                    LastAttackTime = currentTime,
                    MoveAbility = skill.SummonMoveAbility,
                    MovementStyle = skill.SummonMovementStyle,
                    SpawnDistanceX = skill.SummonSpawnDistanceX,
                    AnchorX = spawnPosition.X,
                    AnchorY = spawnPosition.Y,
                    PreviousPositionX = spawnPosition.X,
                    PreviousPositionY = spawnPosition.Y,
                    PositionX = spawnPosition.X,
                    PositionY = spawnPosition.Y,
                    SkillData = skill,
                    LevelData = levelData,
                    FacingRight = _player.FacingRight,
                    AssistType = ResolveSummonAssistType(skill),
                    ManualAssistEnabled = skill.SkillId != SG88_SKILL_ID,
                    LastStateChangeTime = currentTime,
                    ActorState = SummonActorState.Spawn,
                    MaxHealth = ResolveSummonMaxHealth(levelData),
                    CurrentHealth = ResolveSummonMaxHealth(levelData),
                    TeslaCoilState = skill.SkillId == TESLA_COIL_SKILL_ID ? (byte)1 : (byte)0
                };

                UpdateSummonPosition(summon, currentTime, 0f);
                _summons.Add(summon);
                RegisterSummonExpiryTimer(summon);
                SyncSummonPuppet(summon, currentTime);
            }
        }

        private bool TryBeginRocketBoosterLaunch(
            SkillData skill,
            int level,
            int currentTime,
            bool facingRight,
            string startupActionName,
            float? storedVerticalLaunchSpeed)
        {
            if (!CanStartRocketBooster(skill, currentTime))
            {
                CancelRocketBoosterState(playExitAction: false);
                return false;
            }

            SkillLevelData levelData = skill?.GetLevel(level);
            if (levelData == null || !TryConsumeSkillResources(skill, levelData))
            {
                CancelRocketBoosterState(playExitAction: false);
                return false;
            }

            ApplySkillCooldown(skill, levelData, currentTime);
            ExecuteRocketBoosterLaunch(skill, level, currentTime, facingRight, startupActionName, storedVerticalLaunchSpeed);
            return true;
        }

        private void RemoveSummonsBySkill(int skillId)
        {
            for (int i = _summons.Count - 1; i >= 0; i--)
            {
                if (_summons[i]?.SkillId != skillId)
                {
                    continue;
                }

                RemoveSummonAt(i, cancelTimer: true);
            }
        }

        private static int ResolveSummonDurationMs(SkillData skill, SkillLevelData levelData, int level)
        {
            return SummonRuntimeRules.ResolveDurationMs(skill, levelData, level);
        }

        private static int ResolveMaxConcurrentSummons(SkillData skill, SkillLevelData levelData)
        {
            if (skill == null)
            {
                return 1;
            }

            if (skill.SkillId == TESLA_COIL_SKILL_ID)
            {
                return 3;
            }

            if (IsSatelliteSummonSkill(skill.SkillId))
            {
                return Math.Max(1, levelData?.X ?? 1);
            }

            return 1;
        }

        private static int ResolveSummonSpawnCount(SkillData skill, int maxConcurrentSummons, int activeCount)
        {
            if (skill == null)
            {
                return 1;
            }

            if (skill.SkillId == TESLA_COIL_SKILL_ID)
            {
                return maxConcurrentSummons;
            }

            if (maxConcurrentSummons > 1)
            {
                return Math.Max(0, maxConcurrentSummons - activeCount) > 0 ? 1 : 0;
            }

            return 1;
        }

        private static bool IsSatelliteSummonSkill(int skillId)
        {
            return SummonRuntimeRules.IsSatelliteSummonSkill(skillId);
        }

        public void NotifyOwnerDamaged(MobItem sourceMob, int currentTime)
        {
            if (sourceMob == null || _summons.Count == 0)
            {
                return;
            }

            foreach (ActiveSummon summon in _summons.ToArray())
            {
                if (summon == null
                    || summon.IsExpired(currentTime)
                    || summon.IsPendingRemoval
                    || summon.AssistType != SummonAssistType.TargetedAttack
                    || currentTime - summon.LastAttackTime < GetSummonAttackInterval(summon.SkillData, summon.Level)
                    || IsSummonAttackBlockedByOwnerState(summon))
                {
                    continue;
                }

                if (!ProcessSummonAttack(
                        summon,
                        currentTime,
                        new[] { sourceMob },
                        maxTargetsOverride: 1,
                        damagePercentOverride: null))
                {
                    continue;
                }

                summon.LastAttackTime = currentTime;
                if (TryResolveSelfDestructSummon(summon, currentTime))
                {
                    break;
                }
            }
        }

        private void UpdateSummons(int currentTime, float deltaTime)
        {
            _mobPool?.UpdatePuppets(currentTime);

            for (int i = _summons.Count - 1; i >= 0; i--)
            {
                var summon = _summons[i];
                if (summon == null)
                {
                    _summons.RemoveAt(i);
                    continue;
                }

                if (summon.IsPendingRemoval && currentTime >= summon.PendingRemovalTime)
                {
                    RemoveSummonAt(i, cancelTimer: false);
                    continue;
                }

                AdvanceSummonHitPeriod(summon, currentTime);
                summon.PreviousPositionX = summon.PositionX;
                summon.PreviousPositionY = summon.PositionY;
                UpdateSummonPosition(summon, currentTime, deltaTime);
                UpdateSummonActorStateAfterMovement(summon, currentTime);
                SyncSummonPuppet(summon, currentTime);

                if (TryResolveSummonBodyContact(summon, currentTime))
                {
                    continue;
                }

                if (summon.IsPendingRemoval)
                {
                    if (ShouldDeferSummonRemovalPlayback(summon, currentTime))
                    {
                        continue;
                    }

                    summon.ActorState = SummonActorState.Die;
                    continue;
                }

                if (IsSummonActionLockedByDamageHitPeriod(summon))
                {
                    continue;
                }

                if (summon.AssistType == SummonAssistType.TargetedAttack)
                {
                    summon.ActorState = SummonActorState.Idle;
                    continue;
                }

                if (!HasSummonActionIntervalElapsed(summon, currentTime))
                {
                    summon.ActorState = ResolveIdleSummonActorState(summon, currentTime);
                    continue;
                }

                if (IsSummonAttackBlockedByOwnerState(summon))
                {
                    summon.ActorState = ResolveIdleSummonActorState(summon, currentTime);
                    continue;
                }

                bool performedAction = summon.AssistType switch
                {
                    SummonAssistType.Support => ProcessSummonSupport(summon, currentTime),
                    SummonAssistType.SummonAction => ProcessSummonAction(summon, currentTime),
                    _ => ProcessSummonAttackDispatch(summon, currentTime)
                };
                if (!performedAction)
                {
                    continue;
                }

                summon.LastAttackTime = currentTime;
                TryResolveSelfDestructSummon(summon, currentTime);
            }

            UpdateExternalSupportSummons(currentTime);
        }

        private void UpdateExternalSupportSummons(int currentTime)
        {
            IReadOnlyList<ActiveSummon> externalSupportSummons = _externalFriendlySupportSummonsProvider?.Invoke();
            if (externalSupportSummons == null || externalSupportSummons.Count == 0)
            {
                return;
            }

            foreach (ActiveSummon summon in externalSupportSummons)
            {
                if (summon?.SkillData == null
                    || summon.LevelData == null
                    || summon.IsPendingRemoval
                    || summon.IsExpired(currentTime)
                    || summon.AssistType != SummonAssistType.Support)
                {
                    continue;
                }

                if (!HasSummonActionIntervalElapsed(summon, currentTime))
                {
                    continue;
                }

                if (!ProcessSummonSupport(summon, currentTime))
                {
                    continue;
                }

                summon.LastAttackTime = currentTime;
            }
        }

        private bool TryTriggerExpiredSelfDestructSummon(ActiveSummon summon, int currentTime)
        {
            if (summon?.SkillData?.SelfDestructMinion != true
                || summon.ExpiryActionTriggered
                || !summon.HasReachedNaturalExpiry(currentTime))
            {
                return false;
            }

            summon.ExpiryActionTriggered = true;

            bool performedAction = summon.AssistType switch
            {
                SummonAssistType.Support => ProcessSummonSupport(summon, currentTime),
                SummonAssistType.SummonAction => ProcessSummonAction(summon, currentTime),
                _ => ProcessSummonAttackDispatch(summon, currentTime)
            };

            if (performedAction)
            {
                summon.LastAttackTime = currentTime;
            }
            else if (summon.SkillData.SummonAttackAnimation?.Frames.Count > 0
                     || summon.SkillData.SummonRemovalAnimation?.Frames.Count > 0)
            {
                summon.LastAttackAnimationStartTime = currentTime;
            }

            if (!summon.IsPendingRemoval)
            {
                TryResolveSelfDestructSummon(summon, currentTime);
            }

            return true;
        }

        private void UpdateSummonActorStateAfterMovement(ActiveSummon summon, int currentTime)
        {
            if (summon == null)
            {
                return;
            }

            if (summon.IsPendingRemoval)
            {
                if (ShouldDeferSummonRemovalPlayback(summon, currentTime))
                {
                    return;
                }

                SetSummonActorState(summon, SummonActorState.Die, currentTime);
                return;
            }

            SkillAnimation hitAnimation = ResolveSummonHitPlaybackAnimation(summon);
            if (hitAnimation?.Frames.Count > 0 && summon.LastHitAnimationStartTime != int.MinValue)
            {
                int hitDuration = hitAnimation.TotalDuration > 0
                    ? hitAnimation.TotalDuration
                    : hitAnimation.Frames.Sum(frame => frame.Delay);
                int hitElapsed = currentTime - summon.LastHitAnimationStartTime;
                if (hitElapsed >= 0 && hitElapsed < hitDuration)
                {
                    SetSummonActorState(summon, SummonActorState.Hit, currentTime);
                    return;
                }
            }

            if (summon.LastAttackAnimationStartTime != int.MinValue)
            {
                SkillAnimation attackAnimation = ResolveSummonAttackPlaybackAnimation(summon);
                int prepareDuration = SummonRuntimeRules.ResolveSummonActionPrepareDurationMs(
                    summon.SkillData,
                    summon.CurrentAnimationBranchName);
                int attackElapsed = currentTime - summon.LastAttackAnimationStartTime;
                if (prepareDuration > 0 && attackElapsed >= 0 && attackElapsed < prepareDuration)
                {
                    SetSummonActorState(summon, SummonActorState.Prepare, currentTime);
                    return;
                }

                if (attackAnimation?.Frames.Count > 0)
                {
                    int attackDuration = attackAnimation.TotalDuration > 0
                        ? attackAnimation.TotalDuration
                        : attackAnimation.Frames.Sum(frame => frame.Delay);
                    int attackAnimationElapsed = attackElapsed - prepareDuration;
                    if (attackAnimationElapsed >= 0 && attackAnimationElapsed < attackDuration)
                    {
                        SetSummonActorState(summon, SummonActorState.Attack, currentTime);
                        return;
                    }
                }
            }

            SetSummonActorState(summon, ResolveIdleSummonActorState(summon, currentTime), currentTime);
        }

        private SummonActorState ResolveIdleSummonActorState(ActiveSummon summon, int currentTime)
        {
            if (summon?.SkillData == null)
            {
                return SummonActorState.Idle;
            }

            if (summon.SkillId == TESLA_COIL_SKILL_ID
                && summon.SkillData.SummonAttackPrepareAnimation?.Frames.Count > 0
                && (summon.TeslaCoilState == 1
                    || summon.TeslaCoilState == 2
                    || summon.LastAttackAnimationStartTime == int.MinValue))
            {
                return SummonActorState.Prepare;
            }

            if (summon.ActorState == SummonActorState.Spawn)
            {
                SkillAnimation spawnAnimation = summon.SkillData.SummonSpawnAnimation;
                int spawnDuration = spawnAnimation?.TotalDuration ?? 0;
                if (spawnDuration <= 0 && spawnAnimation?.Frames.Count > 0)
                {
                    spawnDuration = spawnAnimation.Frames.Sum(frame => frame.Delay);
                }

                if (spawnDuration > 0 && currentTime - summon.StartTime < spawnDuration)
                {
                    return SummonActorState.Spawn;
                }
            }

            return SummonActorState.Idle;
        }

        private void SetSummonActorState(ActiveSummon summon, SummonActorState state, int currentTime)
        {
            if (summon == null)
            {
                return;
            }

            if (state == SummonActorState.Idle && ShouldClearSupportSummonSuspend(summon, currentTime))
            {
                summon.SupportSuspendUntilTime = int.MinValue;
            }

            if (state != SummonActorState.Attack && state != SummonActorState.Prepare)
            {
                summon.CurrentAnimationBranchName = null;
            }

            if (summon.ActorState == state)
            {
                return;
            }

            summon.ActorState = state;
            summon.LastStateChangeTime = currentTime;
        }

        private bool TryResolveSummonBodyContact(ActiveSummon summon, int currentTime)
        {
            if (_mobPool?.ActiveMobs == null
                || summon == null
                || summon.IsPendingRemoval
                || summon.HitPeriodRemainingMs != 0
                || !ShouldRegisterSummonPuppet(summon.SkillData)
                || currentTime - summon.LastBodyContactTime < SUMMON_BODY_CONTACT_COOLDOWN_MS)
            {
                return false;
            }

            Rectangle summonHitbox = GetSummonContactBounds(summon, currentTime);
            if (summonHitbox.IsEmpty)
            {
                return false;
            }

            foreach (MobItem mob in _mobPool.ActiveMobs)
            {
                if (!IsMobAttackable(mob)
                    || mob.AI?.IsTargetingSummoned != true
                    || mob.AI.Target.TargetId != summon.ObjectId)
                {
                    continue;
                }

                Rectangle mobHitbox = mob.GetBodyHitbox(currentTime);
                if (mobHitbox.IsEmpty || !mobHitbox.Intersects(summonHitbox))
                {
                    continue;
                }

                summon.LastBodyContactTime = currentTime;
                int damage = ResolveSummonBodyContactDamage(mob);
                ApplySummonDamage(summon, damage, currentTime, allowSelfDestructFinalAction: true);
                return true;
            }

            return false;
        }

        private static int ResolveSummonBodyContactDamage(MobItem mob)
        {
            int baseDamage = SummonDamageRuntimeRules.ResolveBodyContactBaseDamage(
                mob?.MobData?.PADamage ?? 0,
                mob?.AI?.GetCurrentAttack()?.Damage ?? 0,
                mob?.MobData?.MADamage ?? 0);
            int resolvedDamage = mob?.AI?.CalculateOutgoingDamage(baseDamage, MobDamageType.Physical) ?? baseDamage;
            return Math.Max(1, resolvedDamage);
        }

        private bool ProcessSummonAttackDispatch(ActiveSummon summon, int currentTime)
        {
            if (summon?.SkillId == TESLA_COIL_SKILL_ID && TryProcessTeslaCoilAttack(summon, currentTime))
            {
                return true;
            }

            if (summon != null)
            {
                summon.CurrentAnimationBranchName = null;
            }

            IEnumerable<MobItem> candidateTargets = summon?.AssistType == SummonAssistType.OwnerAttackTargeted
                ? ResolveRecentOwnerAttackTargets(currentTime)
                : _mobPool?.ActiveMobs;
            if (candidateTargets == null)
            {
                return false;
            }

            int selfDestructDamage = summon.SkillData.SelfDestructMinion
                ? summon.SkillData.ResolveSummonSelfDestructionDamagePercent(summon.Level)
                : 0;
            int? damagePercentOverride = selfDestructDamage > 0 ? selfDestructDamage : null;
            bool performedAttack = ProcessSummonAttack(summon, currentTime, candidateTargets, damagePercentOverride: damagePercentOverride);
            if (performedAttack)
            {
                if (summon.SkillId == SG88_SKILL_ID && summon.AssistType == SummonAssistType.ManualAttack)
                {
                    summon.ManualAssistEnabled = false;
                }

                UpdateRepeatSkillSustainFromSummonAttack(summon, currentTime);
            }

            return performedAttack;
        }

        private static int ResolveSummonMaxHealth(SkillLevelData levelData)
        {
            return Math.Max(1, levelData?.HP ?? 1);
        }

        private static void AdvanceSummonHitPeriod(ActiveSummon summon, int currentTime)
        {
            if (summon == null)
            {
                return;
            }

            if (summon.LastHitPeriodUpdateTime == int.MinValue)
            {
                summon.LastHitPeriodUpdateTime = currentTime;
                return;
            }

            int elapsed = Math.Max(0, currentTime - summon.LastHitPeriodUpdateTime);
            summon.LastHitPeriodUpdateTime = currentTime;
            if (elapsed <= 0 || summon.HitPeriodRemainingMs == 0)
            {
                return;
            }

            if (summon.HitPeriodRemainingMs > 0)
            {
                summon.HitPeriodRemainingMs = Math.Max(0, summon.HitPeriodRemainingMs - elapsed);
            }
            else
            {
                summon.HitPeriodRemainingMs = Math.Min(0, summon.HitPeriodRemainingMs + elapsed);
            }

            summon.HitFlashCounter += (uint)Math.Max(1, (elapsed + 29) / 30);
        }

        private static bool IsSummonActionLockedByDamageHitPeriod(ActiveSummon summon)
        {
            return summon?.HitPeriodRemainingMs > 0;
        }

        private static int ResolveSummonHitPeriodDurationMs(ActiveSummon summon)
        {
            // Client `CSummoned::SetDamaged` / `CSummoned::OnHit` use a fixed signed
            // hit-period window of +/-1500 ms; the hit animation itself stays separate.
            return SUMMON_HIT_PERIOD_DURATION_MS;
        }

        private static Color ResolveSummonDrawColor(ActiveSummon summon)
        {
            if (summon?.HitPeriodRemainingMs == 0)
            {
                return Color.White;
            }

            return (summon.HitFlashCounter & 3u) < 2u
                ? new Color(128, 128, 128)
                : Color.White;
        }

        private bool ApplySummonDamage(ActiveSummon summon, int damage, int currentTime, bool allowSelfDestructFinalAction)
        {
            if (summon == null || summon.IsPendingRemoval)
            {
                return false;
            }

            StartSummonHitReaction(summon, currentTime);
            PlaySummonIncDecHpFeedback(summon, damage, currentTime);

            if (summon.SkillData?.SelfDestructMinion == true && allowSelfDestructFinalAction)
            {
                summon.ExpiryActionTriggered = true;
                bool performedAttack = ProcessSummonAttackDispatch(summon, currentTime);
                if (performedAttack)
                {
                    summon.LastAttackTime = currentTime;
                }

                if (!summon.IsPendingRemoval)
                {
                    TryResolveSelfDestructSummon(summon, currentTime);
                }

                return true;
            }

            summon.MaxHealth = Math.Max(1, summon.MaxHealth);
            summon.CurrentHealth = SummonDamageRuntimeRules.ResolveRemainingHealth(
                summon.CurrentHealth,
                summon.MaxHealth,
                damage);
            if (summon.CurrentHealth > 0)
            {
                return true;
            }

            QueueSummonRemoval(summon, currentTime);
            return true;
        }

        private void StartSummonHitReaction(ActiveSummon summon, int currentTime)
        {
            if (summon == null)
            {
                return;
            }

            summon.LastHitAnimationStartTime = currentTime;
            summon.HitPeriodRemainingMs = ResolveSummonHitPeriodDurationMs(summon);
            summon.LastHitPeriodUpdateTime = currentTime;
            SetSummonActorState(summon, SummonActorState.Hit, currentTime);

            Vector2 summonPosition = GetSummonPosition(summon);
            if (summon.SkillData?.HitEffect != null)
            {
                SpawnHitEffect(
                    summon.SkillId,
                    summon.SkillData.HitEffect,
                    summonPosition.X,
                    summonPosition.Y - 20f,
                    summon.FacingRight,
                    currentTime);
            }
        }

        private void PlaySummonIncDecHpFeedback(ActiveSummon summon, int delta, int currentTime)
        {
            if (_combatEffects == null || summon == null)
            {
                return;
            }

            Rectangle hitbox = GetSummonHitbox(summon, currentTime);
            float x = summon.PositionX;
            float y = !hitbox.IsEmpty ? hitbox.Top : summon.PositionY - 40f;
            if (delta > 0)
            {
                _combatEffects.AddPartyDamage(delta, x, y, isCritical: false, currentTime);
                return;
            }

            _combatEffects.AddMiss(x, y, currentTime);
        }

        private void QueueSummonRemoval(ActiveSummon summon, int currentTime)
        {
            if (summon == null || summon.IsPendingRemoval)
            {
                return;
            }

            CancelSummonExpiryTimer(summon);
            RemoveSummonPuppet(summon);
            summon.RemovalAnimationStartTime = currentTime;
            summon.PendingRemovalTime = currentTime + Math.Max(
                1,
                GetSkillAnimationDuration(_loader.ResolveSummonActionAnimation(
                    summon.SkillData,
                    summon.Level,
                    summon.SkillData?.SummonRemovalBranchName,
                    summon.SkillData?.SummonRemovalAnimation))
                ?? GetSkillAnimationDuration(ResolveSummonHitPlaybackAnimation(summon))
                ?? GetSkillAnimationDuration(ResolveSummonAttackPlaybackAnimation(summon))
                ?? summon.SkillData?.HitEffect?.TotalDuration
                ?? 1);
            SetSummonActorState(summon, SummonActorState.Die, currentTime);
        }

        private bool ProcessSummonAttack(
            ActiveSummon summon,
            int currentTime,
            IEnumerable<MobItem> candidateTargets,
            int? maxTargetsOverride = null,
            int? damagePercentOverride = null)
        {
            if (_mobPool == null || summon?.SkillData == null || candidateTargets == null)
                return false;

            int maxTargets = Math.Max(1, maxTargetsOverride ?? (summon.SkillData.SummonMobCountOverride > 0
                ? summon.SkillData.SummonMobCountOverride
                : summon.LevelData?.MobCount ?? 1));
            int attackCount = Math.Max(1, damagePercentOverride.HasValue
                ? 1
                : summon.SkillData.SummonAttackCountOverride > 0
                ? summon.SkillData.SummonAttackCountOverride
                : summon.LevelData?.AttackCount ?? 1);
            var summonCenter = GetSummonPosition(summon);
            Rectangle summonBounds = GetSummonAttackBounds(summon);
            NotifyAttackAreaResolved(summonBounds, currentTime, summon.SkillId);
            var resolvedTargets = new List<MobItem>();

            foreach (MobItem mob in ResolveSummonAttackTargetOrder(summon, candidateTargets, currentTime))
            {
                if (resolvedTargets.Count >= maxTargets)
                    break;

                Rectangle mobHitbox = GetMobHitbox(mob, currentTime);
                if (!IsMobInSummonAttackRange(summon, mobHitbox))
                    continue;

                resolvedTargets.Add(mob);
            }

            if (resolvedTargets.Count <= 0)
            {
                return false;
            }

            UpdateSummonFacingTowardMob(summon, resolvedTargets[0], currentTime);
            summon.CurrentAnimationBranchName = SummonRuntimeRules.ResolveLocalAttackBranch(summon.SkillData);
            summon.LastAttackAnimationStartTime = currentTime;
            ArmLocalSummonOneTimeActionFallback(summon, currentTime);
            SetSummonActorState(summon, SummonActorState.Attack, currentTime);
            SpawnSummonAttackProjectiles(summon, resolvedTargets, currentTime);

            if (TryQueueSg88ManualClusterAttacks(
                    summon,
                    resolvedTargets,
                    currentTime,
                    attackCount,
                    summonCenter,
                    damagePercentOverride))
            {
                return true;
            }

            bool queuedAnyAttack = false;
            for (int targetIndex = 0; targetIndex < resolvedTargets.Count; targetIndex++)
            {
                MobItem target = resolvedTargets[targetIndex];
                int targetMobId = target?.PoolId ?? 0;
                if (targetMobId <= 0)
                {
                    continue;
                }

                var queuedAttack = new QueuedSummonAttack
                {
                    SequenceId = _nextQueuedSummonAttackSequenceId++,
                    AttackStartedAt = currentTime,
                    ExecuteTime = currentTime + ResolveSummonAttackExecutionDelayMs(summon, summonCenter, target, currentTime, targetIndex),
                    SummonObjectId = summon.ObjectId,
                    SkillId = summon.SkillId,
                    Level = summon.Level,
                    SkillData = summon.SkillData,
                    LevelData = summon.LevelData,
                    FacingRight = summon.FacingRight,
                    Origin = summonCenter,
                    AttackBranchName = summon.CurrentAnimationBranchName,
                    AttackCount = attackCount,
                    DamagePercentOverride = damagePercentOverride,
                    TargetOrderOffset = targetIndex,
                    TargetMobIds = new[] { targetMobId }
                };

                queuedAnyAttack = true;
                if (queuedAttack.ExecuteTime > currentTime)
                {
                    _queuedSummonAttacks.Add(queuedAttack);
                }
                else
                {
                    bool tileOverlayRegisteredForAttack = false;
                    ResolveQueuedSummonAttack(queuedAttack, currentTime, ref tileOverlayRegisteredForAttack);
                }
            }

            return queuedAnyAttack;
        }

        private bool TryQueueSg88ManualClusterAttacks(
            ActiveSummon summon,
            IReadOnlyList<MobItem> resolvedTargets,
            int currentTime,
            int attackCount,
            Vector2 summonCenter,
            int? damagePercentOverride)
        {
            if (summon?.SkillId != SG88_SKILL_ID
                || summon.AssistType != SummonAssistType.ManualAttack
                || resolvedTargets == null
                || resolvedTargets.Count <= 0)
            {
                return false;
            }

            int baseDelayMs = ResolveSummonAttackExecutionDelayMs(summon, resolvedTargets, currentTime);
            int[] resolvedTargetMobIds = resolvedTargets
                .Select(static target => target?.PoolId ?? 0)
                .Where(static mobId => mobId > 0)
                .ToArray();
            if (resolvedTargetMobIds.Length <= 0)
            {
                return false;
            }

            int requestTime = currentTime;
            int followUpDelayMs = ResolveSg88ManualFollowUpDelayMs(resolvedTargetMobIds.Length);
            int followUpExecuteTime = followUpDelayMs > 0
                ? currentTime + baseDelayMs + followUpDelayMs
                : int.MinValue;
            ApplySg88ManualAttackRequestBookkeeping(
                summon,
                requestTime,
                resolvedTargetMobIds,
                followUpExecuteTime);

            bool queuedAnyAttack = false;
            foreach (SummonAttackBatch batch in ResolveSg88ManualAttackBatches(resolvedTargets.Count))
            {
                int[] targetMobIds = resolvedTargets
                    .Skip(batch.StartIndex)
                    .Take(batch.TargetCount)
                    .Select(static target => target?.PoolId ?? 0)
                    .Where(static mobId => mobId > 0)
                    .ToArray();
                if (targetMobIds.Length <= 0)
                {
                    continue;
                }

                var queuedAttack = new QueuedSummonAttack
                {
                    SequenceId = _nextQueuedSummonAttackSequenceId++,
                    AttackStartedAt = requestTime,
                    ExecuteTime = currentTime + baseDelayMs + batch.DelayMs,
                    SummonObjectId = summon.ObjectId,
                    SkillId = summon.SkillId,
                    Level = summon.Level,
                    SkillData = summon.SkillData,
                    LevelData = summon.LevelData,
                    FacingRight = summon.FacingRight,
                    Origin = summonCenter,
                    AttackBranchName = summon.CurrentAnimationBranchName,
                    AttackCount = attackCount,
                    DamagePercentOverride = damagePercentOverride,
                    TargetOrderOffset = batch.StartIndex,
                    IsSg88ManualAttackBatch = true,
                    IsSg88ManualFollowUpBatch = batch.DelayMs > 0,
                    Sg88ManualRequestTime = requestTime,
                    TargetMobIds = targetMobIds
                };

                queuedAnyAttack = true;
                if (queuedAttack.ExecuteTime > currentTime)
                {
                    _queuedSummonAttacks.Add(queuedAttack);
                }
                else
                {
                    bool tileOverlayRegisteredForAttack = false;
                    ResolveQueuedSummonAttack(queuedAttack, currentTime, ref tileOverlayRegisteredForAttack);
                }
            }

            if (queuedAnyAttack)
            {
                OnSg88ManualAttackRequested?.Invoke(new Sg88ManualAttackRequest(
                    summon.ObjectId,
                    summon.SkillId,
                    requestTime,
                    resolvedTargetMobIds[0],
                    resolvedTargetMobIds,
                    baseDelayMs,
                    followUpDelayMs));
            }
            else
            {
                ClearPendingSg88ManualAttackRequestBookkeeping(summon);
            }

            return queuedAnyAttack;
        }

        internal static SummonAttackBatch[] ResolveSg88ManualAttackBatches(int targetCount)
        {
            if (targetCount <= 0)
            {
                return Array.Empty<SummonAttackBatch>();
            }

            if (targetCount == 1)
            {
                return new[]
                {
                    new SummonAttackBatch(StartIndex: 0, TargetCount: 1, DelayMs: 0)
                };
            }

            return new[]
            {
                new SummonAttackBatch(StartIndex: 0, TargetCount: 1, DelayMs: 0),
                new SummonAttackBatch(
                    StartIndex: 1,
                    TargetCount: targetCount - 1,
                    DelayMs: SG88_MANUAL_CLUSTER_FOLLOW_UP_DELAY_MS)
            };
        }

        internal static int ResolveSg88ManualFollowUpDelayMs(int targetCount)
        {
            return targetCount > 1 ? SG88_MANUAL_CLUSTER_FOLLOW_UP_DELAY_MS : 0;
        }

        internal static void ApplySg88ManualAttackRequestBookkeeping(
            ActiveSummon summon,
            int requestedAt,
            IReadOnlyList<int> targetMobIds,
            int followUpExecuteTime)
        {
            if (summon == null)
            {
                return;
            }

            int[] resolvedTargetMobIds = targetMobIds?
                .Where(static mobId => mobId > 0)
                .ToArray() ?? Array.Empty<int>();

            summon.PendingManualAttackRequest = resolvedTargetMobIds.Length > 0;
            summon.PendingManualAttackRequestedAt = resolvedTargetMobIds.Length > 0 ? requestedAt : int.MinValue;
            summon.PendingManualAttackPrimaryTargetMobId = resolvedTargetMobIds.Length > 0
                ? resolvedTargetMobIds[0]
                : 0;
            summon.PendingManualAttackTargetMobIds = resolvedTargetMobIds;
            summon.PendingManualAttackConfirmedTargetMobIds = Array.Empty<int>();
            summon.PendingManualAttackFollowUpAt = resolvedTargetMobIds.Length > 1
                ? followUpExecuteTime
                : int.MinValue;
        }

        internal static void ClearPendingSg88ManualAttackRequestBookkeeping(ActiveSummon summon)
        {
            if (summon == null)
            {
                return;
            }

            summon.PendingManualAttackRequest = false;
            summon.PendingManualAttackRequestedAt = int.MinValue;
            summon.PendingManualAttackPrimaryTargetMobId = 0;
            summon.PendingManualAttackTargetMobIds = Array.Empty<int>();
            summon.PendingManualAttackConfirmedTargetMobIds = Array.Empty<int>();
            summon.PendingManualAttackFollowUpAt = int.MinValue;
        }

        internal static bool TryResolvePendingSg88ManualAttackRequestBookkeeping(
            ActiveSummon summon,
            int requestedAt,
            int currentTime)
        {
            if (summon == null || !summon.PendingManualAttackRequest)
            {
                return false;
            }

            if (requestedAt != int.MinValue && summon.PendingManualAttackRequestedAt != requestedAt)
            {
                return false;
            }

            summon.LastManualAttackResolvedTime = currentTime;
            ClearPendingSg88ManualAttackRequestBookkeeping(summon);
            return true;
        }

        public bool TryResolveSwallowAbsorbRequest(int skillId, int targetMobId, bool success, int currentTime)
        {
            if (_swallowState == null || !_swallowState.PendingAbsorbOutcome)
            {
                if (ShouldBufferWildHunterSwallowAbsorbOutcome(skillId))
                {
                    _swallowAbsorbOutcomeBuffer.Store(
                        skillId,
                        targetMobId,
                        success,
                        currentTime,
                        WildHunterSwallowParity.ResolveBufferedAbsorbOutcomeLifetimeMs());
                }

                return false;
            }

            if (!DoesSwallowSkillRequestMatchState(skillId)
                || _swallowState.TargetMobId != targetMobId)
            {
                return false;
            }

            CompleteWildHunterSwallowAbsorb(success, currentTime);
            return true;
        }

        private void CompleteWildHunterSwallowAbsorb(bool success, int currentTime)
        {
            if (_swallowState == null || !_swallowState.PendingAbsorbOutcome)
            {
                return;
            }

            MobItem target = GetSwallowTarget();
            if (!success || target?.AI == null || target.AI.IsDead)
            {
                RequestClientSkillCancel(_swallowState.ParentSkillId > 0 ? _swallowState.ParentSkillId : _swallowState.SkillId, currentTime);
                return;
            }

            _swallowState.PendingAbsorbOutcome = false;
            target.AI.ApplyStatusEffect(
                MobStatusEffect.Stun,
                WildHunterSwallowParity.GetSuspensionDurationMs(),
                currentTime);

            SpawnHitEffect(
                GetSkillData(_swallowState.ParentSkillId > 0 ? _swallowState.ParentSkillId : _swallowState.SkillId),
                target.MovementInfo?.X ?? _player.X,
                (target.MovementInfo?.Y ?? _player.Y) - 20f,
                currentTime);
            ActivateWildHunterSwallowDigestState(currentTime);
        }

        private bool DoesSwallowSkillRequestMatchState(int requestedSkillId)
        {
            if (_swallowState == null || requestedSkillId <= 0)
            {
                return false;
            }

            int parentSkillId = _swallowState.ParentSkillId > 0
                ? _swallowState.ParentSkillId
                : _swallowState.SkillId;
            if (requestedSkillId == parentSkillId || requestedSkillId == _swallowState.SkillId)
            {
                return true;
            }

            SkillData parentSkill = GetSkillData(parentSkillId);
            return IsVisibleSwallowFamilyRequest(requestedSkillId, parentSkill);
        }

        private void ActivateWildHunterSwallowDigestState(int currentTime)
        {
            if (_swallowState == null || _swallowState.IsDigesting)
            {
                return;
            }

            _swallowState.IsDigesting = true;
            _swallowState.DigestStartTime = currentTime;
            _swallowState.DigestCompleteTime = currentTime + WildHunterSwallowParity.DigestDurationMs;
            _swallowState.NextWriggleTime = currentTime + WildHunterSwallowParity.WriggleIntervalMs;

            RegisterClientSkillTimer(
                _swallowState.ParentSkillId,
                ClientTimerSourceSwallowDigest,
                _swallowState.DigestCompleteTime,
                CompleteSwallowDigestFromClientTimer);
        }

        private bool IsWildHunterSwallowWriggleActionActive()
        {
            if (_preparedSkill?.SkillId == WildHunterSwallowSkillId)
            {
                return true;
            }

            return IsWildHunterSwallowWriggleActionActive(
                _player?.State == PlayerState.Attacking,
                _player?.CurrentActionName);
        }

        internal static bool IsWildHunterSwallowWriggleActionActive(bool isAttacking, string currentActionName)
        {
            if (!isAttacking || string.IsNullOrWhiteSpace(currentActionName))
            {
                return false;
            }

            return string.Equals(currentActionName, "swallow_pre", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(currentActionName, "swallow_loop", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(currentActionName, "swallow", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsCycloneActionActive()
        {
            if (_cycloneState?.Skill == null || _player == null)
            {
                return false;
            }

            return IsBattleMageCycloneBodyAttackActionActive(
                _player.State == PlayerState.Attacking,
                _player.CurrentActionName);
        }

        internal static bool IsBattleMageCycloneBodyAttackActionActive(bool isAttacking, string currentActionName)
        {
            if (!isAttacking || string.IsNullOrWhiteSpace(currentActionName))
            {
                return false;
            }

            return string.Equals(currentActionName, "cyclone_pre", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(currentActionName, "cyclone", StringComparison.OrdinalIgnoreCase);
        }

        private int ResolveSummonAttackExecutionDelayMs(ActiveSummon summon, IReadOnlyCollection<MobItem> targets, int currentTime)
        {
            if (summon?.SkillData == null || targets == null || targets.Count == 0)
            {
                return 0;
            }

            Vector2 summonCenter = GetSummonPosition(summon);
            int delayMs = 0;
            foreach (MobItem target in targets)
            {
                delayMs = Math.Max(delayMs, ResolveSummonAttackExecutionDelayMs(summon, summonCenter, target, currentTime));
            }

            return delayMs;
        }

        private int ResolveSummonAttackExecutionDelayMs(ActiveSummon summon, Vector2 source, MobItem target, int currentTime)
        {
            return ResolveSummonAttackExecutionDelayMs(summon, source, target, currentTime, 0);
        }

        private int ResolveSummonAttackExecutionDelayMs(ActiveSummon summon, Vector2 source, MobItem target, int currentTime, int targetOrder)
        {
            if (summon?.SkillData == null || target == null)
            {
                return 0;
            }

            string attackBranchName = summon.CurrentAnimationBranchName;
            int delayMs = ResolveSummonImpactAuthoredDelayMs(summon.SkillData, targetOrder, attackBranchName);
            delayMs = SummonRuntimeRules.ResolveSummonImpactExecutionDelayMs(
                summon.SkillData,
                delayMs,
                attackBranchName);
            return delayMs;
        }

        private void SpawnSummonAttackProjectiles(ActiveSummon summon, IReadOnlyList<MobItem> targets, int currentTime)
        {
            IReadOnlyList<SkillAnimation> projectileAnimations = summon?.SkillData?.GetSummonProjectileAnimations(summon.CurrentAnimationBranchName);
            if (projectileAnimations == null
                || projectileAnimations.Count == 0
                || targets == null
                || targets.Count == 0)
            {
                return;
            }

            Vector2 source = GetSummonPosition(summon);
            Vector2 projectileSource = SummonImpactPresentationResolver.ResolveSourceAnchor(source);
            for (int i = 0; i < targets.Count; i++)
            {
                MobItem target = targets[i];
                if (target == null)
                {
                    continue;
                }

                Vector2 impactPosition = ResolveSummonImpactDisplayPosition(
                    summon.SkillData,
                    i,
                    summon.CurrentAnimationBranchName,
                    target,
                    source,
                    currentTime);
                int impactDelayMs = Math.Max(60, ResolveSummonAttackExecutionDelayMs(summon, source, target, currentTime, i));
                SpawnSummonProjectileVisual(
                    summon.SkillId,
                    summon.Level,
                    summon.LevelData,
                    projectileAnimations,
                    projectileSource,
                    impactPosition,
                    currentTime,
                    impactDelayMs,
                    i,
                    summon.ObjectId);
            }
        }

        private void SpawnTeslaCoilAttackProjectiles(
            IReadOnlyList<ActiveSummon> teslaCoils,
            IReadOnlyList<MobItem> targets,
            IReadOnlyList<int> targetImpactDelaysMs,
            int currentTime)
        {
            if (teslaCoils == null || teslaCoils.Count == 0 || targets == null || targets.Count == 0)
            {
                return;
            }

            Vector2[] assignedSources = ResolveTeslaProjectileSources(teslaCoils);
            for (int i = 0; i < teslaCoils.Count; i++)
            {
                ActiveSummon teslaCoil = teslaCoils[i];
                IReadOnlyList<SkillAnimation> projectileAnimations = teslaCoil?.SkillData?.GetSummonProjectileAnimations(
                    teslaCoil.CurrentAnimationBranchName);
                if (projectileAnimations == null || projectileAnimations.Count == 0)
                {
                    continue;
                }

                Vector2 source = i < assignedSources.Length
                    ? assignedSources[i]
                    : GetSummonPosition(teslaCoil);
                MobItem target = targets
                    .OrderBy(candidate => Vector2.DistanceSquared(GetMobHitboxCenter(candidate, currentTime), source))
                    .FirstOrDefault();
                if (target == null)
                {
                    continue;
                }

                int targetIndex = -1;
                for (int targetCursor = 0; targetCursor < targets.Count; targetCursor++)
                {
                    if (ReferenceEquals(targets[targetCursor], target))
                    {
                        targetIndex = targetCursor;
                        break;
                    }
                }
                Vector2 impactPosition = ResolveSummonImpactDisplayPosition(
                    teslaCoil.SkillData,
                    targetIndex >= 0 ? targetIndex : 0,
                    teslaCoil.CurrentAnimationBranchName,
                    target,
                    source,
                    currentTime);
                int impactDelayMs = targetIndex >= 0 && targetImpactDelaysMs != null && targetIndex < targetImpactDelaysMs.Count
                    ? Math.Max(60, targetImpactDelaysMs[targetIndex])
                    : Math.Max(60, ResolveTeslaTargetImpactDelayMs(teslaCoil, target, currentTime, targetIndex >= 0 ? targetIndex : 0));
                SpawnSummonProjectileVisual(
                    teslaCoil.SkillId,
                    teslaCoil.Level,
                    teslaCoil.LevelData,
                    projectileAnimations,
                    SummonImpactPresentationResolver.ResolveSourceAnchor(source),
                    impactPosition,
                    currentTime,
                    impactDelayMs,
                    i,
                    teslaCoil.ObjectId);
            }
        }

        private void SpawnSummonProjectileVisual(
            int skillId,
            int level,
            SkillLevelData levelData,
            IReadOnlyList<SkillAnimation> projectileAnimations,
            Vector2 source,
            Vector2 target,
            int currentTime,
            int impactDelayMs,
            int variantIndex,
            int ownerId = 0)
        {
            if (projectileAnimations == null || projectileAnimations.Count == 0)
            {
                return;
            }

            SkillAnimation animation = projectileAnimations[Math.Abs(variantIndex) % projectileAnimations.Count];
            if (animation?.Frames.Count <= 0)
            {
                return;
            }

            int lifeTime = Math.Max(60, impactDelayMs);
            float durationSeconds = lifeTime / 1000f;
            if (durationSeconds <= 0f)
            {
                return;
            }

            Vector2 delta = target - source;
            _projectiles.Add(new ActiveProjectile
            {
                Id = _nextProjectileId++,
                SkillId = skillId,
                SkillLevel = level,
                Data = new ProjectileData
                {
                    Animation = animation,
                    Speed = delta.Length() / durationSeconds,
                    LifeTime = lifeTime
                },
                LevelData = levelData,
                X = source.X,
                Y = source.Y,
                PreviousX = source.X,
                PreviousY = source.Y,
                VelocityX = delta.X / durationSeconds,
                VelocityY = delta.Y / durationSeconds,
                FacingRight = delta.X >= 0f,
                SpawnTime = currentTime,
                OwnerId = ownerId,
                OwnerX = source.X,
                OwnerY = source.Y,
                VisualOnly = true
            });
        }

        private static SummonImpactPresentation ResolveSummonTargetImpactPresentation(
            SkillData skill,
            int targetOrder,
            string attackBranchName = null)
        {
            return skill?.GetSummonTargetHitPresentation(targetOrder, attackBranchName);
        }

        private static SkillAnimation ResolveSummonTargetImpactAnimation(
            SkillData skill,
            int targetOrder,
            string attackBranchName = null)
        {
            return ResolveSummonTargetImpactPresentation(skill, targetOrder, attackBranchName)?.Animation;
        }

        private static int ResolveSummonImpactAuthoredDelayMs(
            SkillData skill,
            int targetOrder,
            string attackBranchName = null)
        {
            SummonImpactPresentation presentation = ResolveSummonTargetImpactPresentation(skill, targetOrder, attackBranchName);
            return presentation?.HitAfterMs > 0
                ? presentation.HitAfterMs
                : Math.Max(0, skill?.ResolveSummonAttackAfterMs(attackBranchName) ?? skill?.SummonAttackHitDelayMs ?? 0);
        }

        private Vector2 ResolveSummonImpactDisplayPosition(
            SkillData skill,
            int targetOrder,
            string attackBranchName,
            MobItem mob,
            Vector2 summonCenter,
            int currentTime)
        {
            if (mob == null)
            {
                return summonCenter;
            }

            SummonImpactPresentation presentation = ResolveSummonTargetImpactPresentation(skill, targetOrder, attackBranchName);
            int? projectilePositionCode = skill?.ResolveSummonProjectilePositionCode(attackBranchName, targetOrder);
            Vector2 fallbackTargetPosition = new(
                mob.MovementInfo?.X ?? summonCenter.X,
                (mob.MovementInfo?.Y ?? summonCenter.Y) - 20f);
            return SummonImpactPresentationResolver.ResolveImpactPosition(
                presentation,
                GetMobHitbox(mob, currentTime),
                summonCenter,
                fallbackTargetPosition,
                projectilePositionCode);
        }

        private int ResolveTeslaTargetImpactDelayMs(ActiveSummon summon, MobItem target, int currentTime, int targetOrder = 0)
        {
            if (summon?.SkillId != TESLA_COIL_SKILL_ID)
            {
                return Math.Max(0, ResolveSummonAttackExecutionDelayMs(
                    summon,
                    GetSummonPosition(summon),
                    target,
                    currentTime,
                    targetOrder));
            }

            int authoredDelayMs = ResolveSummonImpactAuthoredDelayMs(
                summon?.SkillData,
                targetOrder,
                summon?.CurrentAnimationBranchName);
            int attackDelayWindowMs = authoredDelayMs > 0
                ? Math.Max(TeslaMinimumImpactDelayMs, authoredDelayMs)
                : ResolveTeslaAttackDelayWindowMs(summon);
            return ResolveTeslaImpactDelayMs(attackDelayWindowMs);
        }

        private int ResolveTeslaImpactDelayMs(int attackDelayMs)
        {
            int clampedDelayMs = Math.Max(TeslaMinimumImpactDelayMs, attackDelayMs);
            int jitterWindowMs = Math.Max(0, clampedDelayMs - TeslaMinimumImpactDelayMs);
            if (jitterWindowMs <= 0)
            {
                return TeslaMinimumImpactDelayMs;
            }

            return TeslaMinimumImpactDelayMs + Random.Next(jitterWindowMs);
        }

        private IEnumerable<MobItem> OrderSummonTargetsByDistance(ActiveSummon summon, IEnumerable<MobItem> candidateTargets, int currentTime)
        {
            if (summon == null || candidateTargets == null)
            {
                return Enumerable.Empty<MobItem>();
            }

            Vector2 summonCenter = GetSummonPosition(summon);
            return candidateTargets
                .Where(IsMobAttackable)
                .Select(mob =>
                {
                    Vector2 mobCenter = GetMobHitboxCenter(mob, currentTime);
                    float deltaX = mobCenter.X - summonCenter.X;
                    float deltaY = mobCenter.Y - summonCenter.Y;
                    return new
                    {
                        Mob = mob,
                        DistanceSq = (deltaX * deltaX) + (deltaY * deltaY),
                        ForwardPenalty = summon.FacingRight ? (deltaX < 0f ? 1 : 0) : (deltaX > 0f ? 1 : 0),
                        VerticalDistance = MathF.Abs(deltaY)
                    };
                })
                .OrderBy(entry => entry.DistanceSq)
                .ThenBy(entry => entry.ForwardPenalty)
                .ThenBy(entry => entry.VerticalDistance)
                .Select(entry => entry.Mob);
        }

        internal static int[] ResolveSg88ManualAttackTargetOrder(
            Vector2 summonPosition,
            Vector2 ownerPosition,
            IReadOnlyList<Rectangle> candidateHitboxes,
            Rectangle primaryClusterRange)
        {
            if (candidateHitboxes == null || candidateHitboxes.Count == 0)
            {
                return Array.Empty<int>();
            }

            var candidates = candidateHitboxes
                .Select((hitbox, index) => new Sg88ManualCandidate(
                    index,
                    hitbox,
                    new Vector2(hitbox.Center.X, hitbox.Center.Y)))
                .Where(entry => !entry.Hitbox.IsEmpty)
                .ToList();
            if (candidates.Count == 0)
            {
                return Array.Empty<int>();
            }

            if (!TryResolveSg88ManualPrimaryTarget(
                    summonPosition,
                    ownerPosition,
                    primaryClusterRange,
                    candidates,
                    out int primaryTargetIndex,
                    out bool preferLeft))
            {
                return Array.Empty<int>();
            }

            var primaryTarget = candidates.First(entry => entry.Index == primaryTargetIndex);
            Rectangle primaryHitbox = primaryTarget.Hitbox;
            Rectangle primaryRect = ResolveSg88ManualPrimaryTargetRect(summonPosition, primaryClusterRange, preferLeft);
            Point primaryHitPoint = ResolveSg88ManualPrimaryHitPoint(primaryHitbox, primaryRect);
            Rectangle clusterBounds = ResolveSg88ManualClusterBounds(primaryHitPoint, primaryClusterRange);

            return candidates
                .Where(entry => entry.Index == primaryTarget.Index
                    || clusterBounds.IsEmpty
                    || clusterBounds.Intersects(entry.Hitbox))
                .OrderBy(entry => entry.Index == primaryTarget.Index ? 0 : 1)
                .ThenBy(entry => Vector2.DistanceSquared(entry.Center, new Vector2(primaryHitPoint.X, primaryHitPoint.Y)))
                .ThenBy(entry => Vector2.DistanceSquared(entry.Center, ownerPosition))
                .ThenBy(entry => Vector2.DistanceSquared(entry.Center, summonPosition))
                .ThenBy(entry => entry.Index)
                .Select(entry => entry.Index)
                .ToArray();
        }

        internal static Rectangle ResolveSg88ManualPrimaryTargetRect(
            Vector2 summonPosition,
            Rectangle primaryClusterRange,
            bool preferLeft)
        {
            int anchorX = (int)MathF.Round(summonPosition.X);
            int anchorY = ResolveSg88ManualPrimaryHitLineY(summonPosition, primaryClusterRange);
            int nearX = preferLeft
                ? anchorX + primaryClusterRange.Y
                : anchorX - primaryClusterRange.Y;
            int farX = preferLeft
                ? anchorX + primaryClusterRange.X
                : anchorX + primaryClusterRange.Right;
            int left = Math.Min(nearX, farX);
            int right = Math.Max(nearX, farX);
            return new Rectangle(left, anchorY, Math.Max(1, right - left), 1);
        }

        internal static Point ResolveSg88ManualPrimaryHitPoint(Rectangle hitbox, Rectangle primaryRect)
        {
            if (hitbox.IsEmpty || primaryRect.IsEmpty)
            {
                return Point.Zero;
            }

            int left = primaryRect.Left;
            int right = primaryRect.Right;
            int top = primaryRect.Top;
            int bottom = primaryRect.Bottom;

            if (left >= hitbox.Right)
            {
                left = hitbox.Right - 1;
            }
            else if (right <= hitbox.Left)
            {
                right = hitbox.Left + 1;
            }

            if (top >= hitbox.Bottom)
            {
                top = hitbox.Bottom - 1;
            }
            else if (bottom <= hitbox.Top)
            {
                bottom = hitbox.Top + 1;
            }

            var constrainedRect = new Rectangle(
                left,
                top,
                Math.Max(1, right - left),
                Math.Max(1, bottom - top));
            Rectangle intersection = Rectangle.Intersect(constrainedRect, hitbox);
            if (intersection.IsEmpty)
            {
                return Point.Zero;
            }

            return new Point(
                (intersection.Left + intersection.Right) / 2,
                (intersection.Top + intersection.Bottom) / 2);
        }

        internal static int ResolveSg88ManualPrimaryHitLineY(Vector2 summonPosition, Rectangle primaryClusterRange)
        {
            return (int)MathF.Round(summonPosition.Y) + primaryClusterRange.Y;
        }

        internal static bool IntersectsSg88ManualPrimaryTrapezoid(
            Rectangle hitbox,
            Vector2 summonPosition,
            Rectangle primaryClusterRange,
            bool preferLeft)
        {
            if (hitbox.IsEmpty || primaryClusterRange.IsEmpty)
            {
                return false;
            }

            int anchorX = (int)MathF.Round(summonPosition.X);
            int anchorY = ResolveSg88ManualPrimaryHitLineY(summonPosition, primaryClusterRange);
            int trapezoidNearX = preferLeft
                ? anchorX + primaryClusterRange.Y
                : anchorX - primaryClusterRange.Y;
            int trapezoidFarX = preferLeft
                ? anchorX + primaryClusterRange.X
                : anchorX + primaryClusterRange.Right;
            int expandingDelta = trapezoidNearX - anchorX;
            int shrinkingDelta = anchorX - trapezoidNearX;
            int currentX = trapezoidNearX;

            while (preferLeft ? currentX > trapezoidFarX : currentX < trapezoidFarX)
            {
                int verticalDelta;
                Rectangle attackStrip;
                if (preferLeft)
                {
                    int stripRight = currentX;
                    verticalDelta = shrinkingDelta;
                    currentX -= SG88_MANUAL_TRAPEZOID_SLICE_WIDTH_PX;
                    shrinkingDelta += SG88_MANUAL_TRAPEZOID_SLICE_WIDTH_PX;
                    expandingDelta -= SG88_MANUAL_TRAPEZOID_SLICE_WIDTH_PX;
                    int stripLeft = Math.Max(trapezoidFarX, currentX);
                    attackStrip = new Rectangle(
                        stripLeft,
                        anchorY - verticalDelta / SG88_MANUAL_TRAPEZOID_SLOPE_RATIO,
                        Math.Max(1, stripRight - stripLeft),
                        Math.Max(1, (verticalDelta / SG88_MANUAL_TRAPEZOID_SLOPE_RATIO) * 2));
                }
                else
                {
                    int stripLeft = currentX;
                    currentX += SG88_MANUAL_TRAPEZOID_SLICE_WIDTH_PX;
                    verticalDelta = expandingDelta;
                    shrinkingDelta -= SG88_MANUAL_TRAPEZOID_SLICE_WIDTH_PX;
                    expandingDelta += SG88_MANUAL_TRAPEZOID_SLICE_WIDTH_PX;
                    int stripRight = Math.Min(currentX, trapezoidFarX);
                    attackStrip = new Rectangle(
                        stripLeft,
                        anchorY - verticalDelta / SG88_MANUAL_TRAPEZOID_SLOPE_RATIO,
                        Math.Max(1, stripRight - stripLeft),
                        Math.Max(1, (verticalDelta / SG88_MANUAL_TRAPEZOID_SLOPE_RATIO) * 2));
                }

                if (!Rectangle.Intersect(hitbox, attackStrip).IsEmpty)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveSg88ManualPrimaryTarget(
            Vector2 summonPosition,
            Vector2 ownerPosition,
            Rectangle primaryClusterRange,
            IReadOnlyList<Sg88ManualCandidate> candidates,
            out int primaryTargetIndex,
            out bool preferLeft)
        {
            foreach (Vector2 searchOrigin in new[] { ownerPosition, summonPosition })
            {
                var candidate = candidates
                    .Select(entry =>
                    {
                        Vector2 center = entry.Center;
                        float deltaY = center.Y - searchOrigin.Y;
                        return new
                        {
                            entry.Index,
                            entry.Hitbox,
                            entry.Center,
                            SearchDistanceSq = Vector2.DistanceSquared(center, searchOrigin),
                            VerticalDistance = MathF.Abs(deltaY),
                            SummonDistanceSq = Vector2.DistanceSquared(center, summonPosition)
                        };
                    })
                    .OrderBy(entry => entry.SearchDistanceSq)
                    .ThenBy(entry => entry.VerticalDistance)
                    .ThenBy(entry => entry.SummonDistanceSq)
                    .ThenBy(entry => entry.Index)
                    .FirstOrDefault();
                if (candidate == null)
                {
                    continue;
                }

                if (IntersectsSg88ManualPrimaryTrapezoid(candidate.Hitbox, summonPosition, primaryClusterRange, preferLeft: true))
                {
                    primaryTargetIndex = candidate.Index;
                    preferLeft = true;
                    return true;
                }

                if (IntersectsSg88ManualPrimaryTrapezoid(candidate.Hitbox, summonPosition, primaryClusterRange, preferLeft: false))
                {
                    primaryTargetIndex = candidate.Index;
                    preferLeft = false;
                    return true;
                }
            }

            primaryTargetIndex = 0;
            preferLeft = false;
            return false;
        }

        internal static Rectangle ResolveSg88ManualClusterBounds(Point primaryHitPoint, Rectangle primaryClusterRange)
        {
            if (primaryClusterRange.IsEmpty)
            {
                return Rectangle.Empty;
            }

            return new Rectangle(
                primaryHitPoint.X + primaryClusterRange.X,
                primaryHitPoint.Y + primaryClusterRange.Y,
                primaryClusterRange.Width,
                primaryClusterRange.Height);
        }

        private bool ApplySummonAttackToMob(
            SkillData skill,
            SkillLevelData levelData,
            int level,
            bool facingRight,
            MobItem mob,
            int currentTime,
            int attackCount,
            Vector2 summonCenter,
            int? damagePercentOverride,
            int targetOrder,
            out bool attackApplied)
        {
            attackApplied = false;
            if (skill == null || levelData == null || !IsMobAttackable(mob))
            {
                return false;
            }

            attackApplied = true;
            for (int i = 0; i < attackCount; i++)
            {
                int damage = CalculateSkillDamage(skill, level, damagePercentOverride);
                bool isCritical = IsSkillCritical(levelData);
                if (isCritical)
                {
                    damage = (int)MathF.Round(damage * 1.5f);
                }

                bool died = mob.ApplyDamage(damage, currentTime, isCritical, _player.X, _player.Y, damageType: ResolveMobDamageType(skill));
                ApplyMobReflectDamage(mob, currentTime, ResolveMobDamageType(skill));
                ShowSkillDamageNumber(mob, damage, isCritical, currentTime, i);
                Vector2 impactPosition = ResolveSummonImpactDisplayPosition(
                    skill,
                    targetOrder,
                    null,
                    mob,
                    summonCenter,
                    currentTime);
                SpawnHitEffect(
                    skill.SkillId,
                    ResolveSummonTargetImpactAnimation(skill, targetOrder, null) ?? skill.HitEffect,
                    impactPosition.X,
                    impactPosition.Y,
                    facingRight,
                    currentTime);

                if (died)
                {
                    return true;
                }

                ApplySkillKnockback(AttackResolutionMode.Melee, mob, damage, facingRight);
                if (!IsMobAttackable(mob))
                {
                    return true;
                }
            }

            ApplyMobStatusFromSkill(skill, levelData, mob, currentTime);
            return mob.AI?.State == MobAIState.Death;
        }

        private bool IsSummonAttackBlockedByOwnerState(ActiveSummon summon)
        {
            if (summon?.SkillData == null)
                return false;

            if (summon.SkillId == SG88_SKILL_ID && !summon.ManualAssistEnabled)
                return true;

            bool ignoresOwnerAttackStateRestrictions =
                SummonMovementResolver.CanAttackWhileOwnerIsOnLadderOrRope(summon.SkillData.SkillId);
            if ((_player.State == PlayerState.Ladder || _player.State == PlayerState.Rope)
                && !ignoresOwnerAttackStateRestrictions)
            {
                return true;
            }

            bool ownerIsHidden = HasActiveTemporaryStatLabel(DarkSightBuffLabel);
            bool ownerHasBlockingVehicleMount = IsSummonOwnerUsingBlockingVehicleMount();
            return (ownerIsHidden || ownerHasBlockingVehicleMount)
                && !SummonMovementResolver.CanAttackWhileOwnerIsHiddenOrMounted(summon.SkillData.SkillId);
        }

        private bool IsSummonOwnerUsingBlockingVehicleMount()
        {
            CharacterPart mountPart = GetEquippedTamingMobPart();
            return mountPart?.Slot == EquipSlot.TamingMob
                   && !IsWildHunterJaguarTamingMobPart(mountPart);
        }

        private bool ProcessSummonSupport(ActiveSummon summon, int currentTime)
        {
            if (summon?.LevelData == null || summon.SkillData == null)
            {
                return false;
            }

            if (currentTime < summon.NextSupportTime)
            {
                return false;
            }

            if (IsSupportSummonSuspended(summon, currentTime))
            {
                return false;
            }

            if (string.Equals(summon.SkillData?.SummonCondition, "whenUserLieDown", StringComparison.OrdinalIgnoreCase)
                && _player.State != PlayerState.Sitting)
            {
                return false;
            }

            if (summon.SkillId == BEHOLDER_SUMMON_SKILL_ID)
            {
                return ProcessBeholderSupport(summon, currentTime);
            }

            if (HasMinionAbilityToken(summon.SkillData.MinionAbility, "heal"))
            {
                return ProcessSummonHealSupport(summon, currentTime);
            }

            if (RemoteAffectedAreaSupportResolver.IsFriendlyPlayerAreaSkill(summon.SkillData, summon.LevelData))
            {
                return ProcessFriendlySummonBuffSupport(summon, currentTime);
            }

            return ProcessSummonMobSupport(summon, currentTime);
        }

        private bool ProcessBeholderSupport(ActiveSummon summon, int currentTime)
        {
            if (summon == null)
            {
                return false;
            }

            if ((summon.NextHealTime == int.MinValue || currentTime >= summon.NextHealTime)
                && TryProcessBeholderHealSupport(summon, currentTime))
            {
                return true;
            }

            if (summon.NextBuffTime != int.MinValue && currentTime < summon.NextBuffTime)
            {
                return false;
            }

            return TryProcessBeholderBuffSupport(summon, currentTime);
        }

        private bool TryProcessBeholderHealSupport(ActiveSummon summon, int currentTime)
        {
            if (summon == null || _player == null || _player.HP >= _player.MaxHP)
            {
                return false;
            }

            int healLevel = GetSkillLevel(BEHOLDER_HEAL_SKILL_ID);
            SkillData healSkill = GetSkillData(BEHOLDER_HEAL_SKILL_ID);
            SkillLevelData healLevelData = healSkill?.GetLevel(healLevel);
            if (healLevelData == null || healLevelData.HP <= 0)
            {
                return false;
            }

            int previousHp = _player.HP;
            _player.Recover(healLevelData.HP, 0);
            if (_player.HP <= previousHp)
            {
                return false;
            }

            summon.NextHealTime = currentTime + Math.Max(0, healLevelData.X * 1000);
            summon.LastAttackAnimationStartTime = currentTime;
            summon.CurrentAnimationBranchName = SummonRuntimeRules.ResolveBeholderHealBranch(summon.SkillData);
            ArmSupportSummonSuspend(summon, currentTime);
            ArmLocalSummonOneTimeActionFallback(summon, currentTime);
            SetSummonActorState(summon, SummonActorState.Attack, currentTime);
            return true;
        }

        private bool TryProcessBeholderBuffSupport(ActiveSummon summon, int currentTime)
        {
            int buffLevel = GetSkillLevel(BEHOLDER_BUFF_SKILL_ID);
            SkillData buffSkill = GetSkillData(BEHOLDER_BUFF_SKILL_ID);
            SkillLevelData buffLevelData = buffSkill?.GetLevel(buffLevel);
            if (buffLevelData == null || buffLevelData.Time <= 0 || _player?.Build == null)
            {
                return false;
            }

            BeholderBuffCandidate? selectedBuff = SelectBeholderBuffCandidate(
                summon,
                buffLevelData,
                Random.Next());
            if (!selectedBuff.HasValue)
            {
                return false;
            }

            BeholderBuffCandidate candidate = selectedBuff.Value;
            ApplySyntheticSummonBuff(
                candidate.BuffId,
                candidate.Name,
                buffSkill,
                candidate.LevelData,
                currentTime);

            summon.NextBuffTime = currentTime + Math.Max(0, buffLevelData.X * 1000);
            summon.LastAttackAnimationStartTime = currentTime;
            summon.CurrentAnimationBranchName = candidate.AnimationBranchName;
            ArmSupportSummonSuspend(summon, currentTime);
            ArmLocalSummonOneTimeActionFallback(summon, currentTime);
            SetSummonActorState(summon, SummonActorState.Attack, currentTime);
            return true;
        }

        private bool ProcessSummonHealSupport(ActiveSummon summon, int currentTime)
        {
            Rectangle supportBounds = GetSummonAttackBounds(summon);
            Rectangle playerHitbox = _player.GetHitbox();
            if (!supportBounds.IsEmpty
                && !playerHitbox.IsEmpty
                && !supportBounds.Intersects(playerHitbox))
            {
                return false;
            }

            int hpHeal = ResolveSupportSummonHpHeal(summon);
            int mpHeal = summon.LevelData.MP;

            if (summon.LevelData.X > 0 && hpHeal <= 0)
            {
                hpHeal = summon.LevelData.X;
            }

            bool healed = false;
            if (hpHeal > 0)
            {
                int previousHp = _player.HP;
                _player.Recover(hpHeal, 0);
                int newHp = _player.HP;
                healed = newHp > _player.HP;
                healed = newHp > previousHp;
            }

            if (mpHeal > 0)
            {
                int previousMp = _player.MP;
                _player.Recover(0, mpHeal);
                healed |= _player.MP > previousMp;
            }

            if (!healed)
            {
                return false;
            }

            int rehealLockMs = ResolveSupportSummonRehealLockMs(summon);
            if (rehealLockMs > 0)
            {
                summon.NextSupportTime = currentTime + rehealLockMs;
            }

            summon.CurrentAnimationBranchName = SummonRuntimeRules.ResolveLocalSupportBranch(
                summon.SkillData,
                preferHealFirst: true);
            ArmSupportSummonSuspend(summon, currentTime);
            if (summon.SkillData.HitEffect != null)
            {
                SpawnHitEffect(
                    summon.SkillData.SkillId,
                    summon.SkillData.HitEffect,
                    _player.X,
                    _player.Y - 35f,
                    _player.FacingRight,
                    currentTime);
            }

            summon.LastAttackAnimationStartTime = currentTime;
            ArmLocalSummonOneTimeActionFallback(summon, currentTime);
            SetSummonActorState(summon, SummonActorState.Attack, currentTime);
            return true;
        }

        private bool TryProcessTeslaCoilAttack(ActiveSummon summon, int currentTime)
        {
            if (summon?.SkillData == null || _mobPool?.ActiveMobs == null)
            {
                return false;
            }

            List<ActiveSummon> teslaCoils = GetActiveTeslaCoils(currentTime);
            if (teslaCoils.Count < 3 || !ReferenceEquals(teslaCoils[0], summon))
            {
                return false;
            }

            if (!TryBuildTeslaCoilTriangle(teslaCoils, out Vector2 left, out Vector2 apex, out Vector2 right))
            {
                return false;
            }

            Point[] trianglePoints =
            {
                new((int)MathF.Round(left.X), (int)MathF.Round(left.Y)),
                new((int)MathF.Round(apex.X), (int)MathF.Round(apex.Y)),
                new((int)MathF.Round(right.X), (int)MathF.Round(right.Y))
            };

            int maxTargets = Math.Max(
                1,
                summon.SkillData.SummonMobCountOverride > 0
                    ? summon.SkillData.SummonMobCountOverride
                    : summon.LevelData?.MobCount ?? 1);
            Vector2 triangleCenter = (left + apex + right) / 3f;
            Rectangle triangleBounds = GetTriangleBounds(left, apex, right);
            NotifyAttackAreaResolved(triangleBounds, currentTime, summon.SkillId);

            List<MobItem> resolvedTargets = _mobPool.ActiveMobs
                .Where(IsMobAttackable)
                .Select(mob => new
                {
                    Mob = mob,
                    Hitbox = GetMobHitbox(mob, currentTime),
                    Center = GetMobHitboxCenter(mob, currentTime)
                })
                .Where(entry => !entry.Hitbox.IsEmpty && DoesRectangleIntersectTriangle(entry.Hitbox, left, apex, right))
                .OrderBy(entry => Vector2.DistanceSquared(entry.Center, triangleCenter))
                .ThenBy(entry => MathF.Abs(entry.Center.Y - triangleCenter.Y))
                .Select(entry => entry.Mob)
                .Take(maxTargets)
                .ToList();
            if (resolvedTargets.Count == 0)
            {
                return false;
            }

            SpawnTeslaTriangleEffect(summon.SkillData, triangleCenter, summon.FacingRight, currentTime);
            string teslaAttackBranchName = SummonRuntimeRules.ResolvePacketAttackBranch(
                summon.SkillData,
                packetAction: 6);

            foreach (ActiveSummon teslaCoil in teslaCoils)
            {
                MobItem targetForCoil = resolvedTargets
                    .OrderBy(target => Vector2.DistanceSquared(GetMobHitboxCenter(target, currentTime), GetSummonPosition(teslaCoil)))
                    .FirstOrDefault();
                UpdateSummonFacingTowardMob(teslaCoil, targetForCoil, currentTime);
                teslaCoil.CurrentAnimationBranchName = teslaAttackBranchName;
                teslaCoil.TeslaCoilState = 2;
                teslaCoil.TeslaTrianglePoints = trianglePoints.ToArray();
                teslaCoil.LastAttackTime = currentTime;
                teslaCoil.LastAttackAnimationStartTime = currentTime;
                ArmLocalSummonOneTimeActionFallback(teslaCoil, currentTime);
                SetSummonActorState(teslaCoil, SummonActorState.Attack, currentTime);
            }

            int selfDestructDamage = summon.SkillData.SelfDestructMinion
                ? summon.SkillData.ResolveSummonSelfDestructionDamagePercent(summon.Level)
                : 0;
            int? damagePercentOverride = selfDestructDamage > 0 ? selfDestructDamage : null;
            int attackCount = Math.Max(1, damagePercentOverride.HasValue
                ? 1
                : summon.SkillData.SummonAttackCountOverride > 0
                ? summon.SkillData.SummonAttackCountOverride
                : summon.LevelData?.AttackCount ?? 1);

            List<int> targetImpactDelaysMs = new(resolvedTargets.Count);
            for (int i = 0; i < resolvedTargets.Count; i++)
            {
                MobItem target = resolvedTargets[i];
                if (target?.PoolId <= 0)
                {
                    continue;
                }

                int executeTime = currentTime + ResolveTeslaTargetImpactDelayMs(summon, target, currentTime);
                var queuedAttack = new QueuedSummonAttack
                {
                    SequenceId = _nextQueuedSummonAttackSequenceId++,
                    AttackStartedAt = currentTime,
                    ExecuteTime = executeTime,
                    SummonObjectId = summon.ObjectId,
                    SkillId = summon.SkillId,
                    Level = summon.Level,
                    SkillData = summon.SkillData,
                    LevelData = summon.LevelData,
                    FacingRight = summon.FacingRight,
                    Origin = triangleCenter,
                    AttackBranchName = summon.CurrentAnimationBranchName,
                    AttackCount = attackCount,
                    DamagePercentOverride = damagePercentOverride,
                    TargetOrderOffset = i,
                    TargetMobIds = new[] { target.PoolId }
                };

                targetImpactDelaysMs.Add(executeTime - currentTime);
                if (queuedAttack.ExecuteTime > currentTime)
                {
                    _queuedSummonAttacks.Add(queuedAttack);
                }
                else
                {
                    bool tileOverlayRegisteredForAttack = false;
                    ResolveQueuedSummonAttack(queuedAttack, currentTime, ref tileOverlayRegisteredForAttack);
                }
            }

            if (targetImpactDelaysMs.Count == 0)
            {
                return false;
            }

            SpawnTeslaCoilAttackProjectiles(teslaCoils, resolvedTargets, targetImpactDelaysMs, currentTime);

            return true;
        }

        private void SpawnTeslaTriangleEffect(SkillData skill, Vector2 triangleCenter, bool facingRight, int currentTime)
        {
            SkillAnimation animation = _loader.ResolveSummonActionAnimation(skill, skillLevel: 1, "attackTriangle");
            if (animation?.Frames.Count <= 0)
            {
                return;
            }

            SpawnHitEffect(skill.SkillId, animation, triangleCenter.X, triangleCenter.Y, facingRight, currentTime);
        }

        private bool ProcessSummonMobSupport(ActiveSummon summon, int currentTime)
        {
            if (_mobPool?.ActiveMobs == null || _mobPool.ActiveMobs.Count == 0)
            {
                return false;
            }

            int maxTargets = Math.Max(1, summon.SkillData.SummonMobCountOverride > 0
                ? summon.SkillData.SummonMobCountOverride
                : summon.LevelData?.MobCount ?? 1);
            int affectedCount = 0;
            Rectangle supportBounds = GetSummonAttackBounds(summon);
            NotifyAttackAreaResolved(supportBounds, currentTime, summon.SkillId);

            foreach (MobItem mob in OrderSummonTargetsByDistance(summon, _mobPool.ActiveMobs, currentTime))
            {
                if (affectedCount >= maxTargets)
                {
                    break;
                }

                Rectangle mobHitbox = GetMobHitbox(mob, currentTime);
                if (!IsMobInSummonAttackRange(summon, mobHitbox))
                {
                    continue;
                }

                ApplyMobStatusFromSkill(summon.SkillData, summon.LevelData, mob, currentTime);
                affectedCount++;
            }

            if (affectedCount <= 0)
            {
                return false;
            }

            summon.CurrentAnimationBranchName = SummonRuntimeRules.ResolveLocalSupportBranch(
                summon.SkillData,
                preferHealFirst: false);
            ArmSupportSummonSuspend(summon, currentTime);
            summon.LastAttackAnimationStartTime = currentTime;
            ArmLocalSummonOneTimeActionFallback(summon, currentTime);
            SetSummonActorState(summon, SummonActorState.Attack, currentTime);
            if (summon.SkillData.HitEffect != null)
            {
                Vector2 summonPosition = GetSummonPosition(summon);
                SpawnHitEffect(
                    summon.SkillData.SkillId,
                    summon.SkillData.HitEffect,
                    summonPosition.X,
                    summonPosition.Y - 20f,
                    summon.FacingRight,
                    currentTime);
            }

            return true;
        }

        private bool ProcessSummonAction(ActiveSummon summon, int currentTime)
        {
            if (summon?.SkillData == null)
            {
                return false;
            }

            summon.CurrentAnimationBranchName = SummonRuntimeRules.ResolveLocalSummonActionBranch(summon.SkillData);
            summon.LastAttackAnimationStartTime = currentTime;
            ArmLocalSummonOneTimeActionFallback(summon, currentTime);
            SetSummonActorState(summon, SummonActorState.Attack, currentTime);
            if (summon.SkillData.HitEffect != null)
            {
                Vector2 summonPosition = GetSummonPosition(summon);
                SpawnHitEffect(
                    summon.SkillData.SkillId,
                    summon.SkillData.HitEffect,
                    summonPosition.X,
                    summonPosition.Y - 20f,
                    summon.FacingRight,
                    currentTime);
            }

            return true;
        }

        private static bool IsSupportSummonSuspended(ActiveSummon summon, int currentTime)
        {
            return summon != null
                   && summon.SupportSuspendUntilTime != int.MinValue
                   && currentTime < summon.SupportSuspendUntilTime;
        }

        private static void ArmSupportSummonSuspend(ActiveSummon summon, int currentTime)
        {
            if (summon?.SkillData == null)
            {
                return;
            }

            bool preferHealFirst = SummonRuntimeRules.HasMinionAbilityToken(
                summon.SkillData.MinionAbility,
                "heal");
            if (!SummonRuntimeRules.ShouldTrackSupportSuspendWindow(
                    summon.SkillData,
                    summon.AssistType,
                    preferHealFirst,
                    summon.CurrentAnimationBranchName))
            {
                return;
            }

            int suspendDurationMs = ResolveSupportSummonSuspendDurationMs(summon);
            summon.SupportSuspendUntilTime = suspendDurationMs > 0
                ? currentTime + suspendDurationMs
                : int.MinValue;
        }

        private static int ResolveSupportSummonSuspendDurationMs(ActiveSummon summon)
        {
            if (summon?.SkillData == null)
            {
                return 0;
            }

            bool preferHealFirst = SummonRuntimeRules.HasMinionAbilityToken(
                summon.SkillData.MinionAbility,
                "heal");
            int suspendDuration = SummonRuntimeRules.ResolveSupportSuspendDurationMs(
                summon.SkillData,
                preferHealFirst,
                explicitBranchName: summon.CurrentAnimationBranchName);
            if (suspendDuration <= 0)
            {
                suspendDuration = summon.SkillId == HEALING_ROBOT_SKILL_ID
                    ? GetSkillAnimationDuration(summon.SkillData.SummonAttackAnimation)
                      ?? summon.SkillData.SummonAttackHitDelayMs
                    : 0;
            }

            if (suspendDuration <= 0)
            {
                suspendDuration = summon.SkillId == HEALING_ROBOT_SKILL_ID
                    ? summon.SkillData.HitEffect?.TotalDuration ?? 0
                    : 0;
            }

            return Math.Max(0, suspendDuration);
        }

        private SkillAnimation ResolveSummonAttackPlaybackAnimation(ActiveSummon summon)
        {
            SkillData skill = summon?.SkillData;
            if (skill == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(summon.CurrentAnimationBranchName))
            {
                SkillAnimation branchAnimation = _loader.ResolveSummonActionAnimation(
                    skill,
                    summon.Level,
                    summon.CurrentAnimationBranchName);
                if (branchAnimation?.Frames.Count > 0)
                {
                    return branchAnimation;
                }

                SkillAnimation retryAnimation = ResolveSummonEmptyActionRetryAnimation(summon);
                if (retryAnimation?.Frames.Count > 0)
                {
                    return retryAnimation;
                }

                return null;
            }

            if (!string.IsNullOrWhiteSpace(skill.SummonAttackBranchName))
            {
                SkillAnimation attackBranchAnimation = _loader.ResolveSummonActionAnimation(
                    skill,
                    summon.Level,
                    skill.SummonAttackBranchName,
                    skill.SummonAttackAnimation);
                if (attackBranchAnimation?.Frames.Count > 0)
                {
                    return attackBranchAnimation;
                }
            }

            if (skill.SummonAttackAnimation?.Frames.Count > 0)
            {
                return skill.SummonAttackAnimation;
            }

            return ResolveSummonEmptyActionRetryAnimation(summon);
        }

        private SkillAnimation ResolveSummonEmptyActionRetryAnimation(ActiveSummon summon)
        {
            SkillData skill = summon?.SkillData;
            if (skill == null)
            {
                return null;
            }

            string retryBranchName = SummonRuntimeRules.ResolveEmptyActionRetryBranch(skill);
            if (string.IsNullOrWhiteSpace(retryBranchName))
            {
                return null;
            }

            SkillAnimation retryAnimation = _loader.ResolveSummonActionAnimation(
                skill,
                summon.Level,
                retryBranchName);
            return retryAnimation?.Frames.Count > 0
                ? retryAnimation
                : null;
        }

        private void ArmLocalSummonOneTimeActionFallback(ActiveSummon summon, int currentTime)
        {
            if (summon?.SkillData == null)
            {
                return;
            }

            int actionCode = SummonRuntimeRules.ResolveLocalAttackActionCode(
                summon.SkillData,
                summon.AssistType,
                summon.CurrentAnimationBranchName);
            if (actionCode <= 0)
            {
                return;
            }

            SkillAnimation actionAnimation = _loader.ResolveSummonActionAnimation(
                summon.SkillData,
                summon.Level,
                summon.CurrentAnimationBranchName);
            if (actionAnimation?.Frames.Count <= 0)
            {
                actionAnimation = ResolveSummonEmptyActionRetryAnimation(summon);
            }

            int duration = GetSkillAnimationDuration(actionAnimation) ?? 0;
            if (actionAnimation?.Frames.Count <= 0 || duration <= 0)
            {
                return;
            }

            summon.OneTimeActionFallbackAnimation = actionAnimation;
            summon.OneTimeActionFallbackStartTime = currentTime;
            summon.OneTimeActionFallbackAnimationTime = 0;
            summon.OneTimeActionFallbackEndTime = currentTime + duration;
        }

        private SkillAnimation ResolveSummonHitPlaybackAnimation(ActiveSummon summon)
        {
            SkillData skill = summon?.SkillData;
            if (skill == null)
            {
                return null;
            }

            string hitBranchName = SummonRuntimeRules.ResolveHitPlaybackBranch(skill);
            if (!string.IsNullOrWhiteSpace(hitBranchName))
            {
                SkillAnimation hitBranchAnimation = _loader.ResolveSummonActionAnimation(
                    skill,
                    summon.Level,
                    hitBranchName,
                    skill.SummonHitAnimation);
                if (hitBranchAnimation?.Frames.Count > 0)
                {
                    return hitBranchAnimation;
                }
            }

            return skill.SummonHitAnimation;
        }

        private static bool ShouldClearSupportSummonSuspend(ActiveSummon summon, int currentTime)
        {
            return SummonRuntimeRules.ShouldClearHealingRobotSupportSuspend(
                summon,
                currentTime,
                HEALING_ROBOT_SKILL_ID);
        }

        private bool ProcessFriendlySummonBuffSupport(ActiveSummon summon, int currentTime)
        {
            if (summon?.SkillData == null || summon.LevelData == null || _player == null)
            {
                return false;
            }

            Rectangle supportBounds = GetSummonAttackBounds(summon);
            Rectangle playerHitbox = _player.GetHitbox();
            if (!supportBounds.IsEmpty
                && !playerHitbox.IsEmpty
                && !supportBounds.Intersects(playerHitbox))
            {
                return false;
            }

            SkillLevelData buffLevelData = ResolveFriendlySupportSummonBuffLevelData(summon);
            if (buffLevelData == null || buffLevelData.Time <= 0)
            {
                return false;
            }

            int syntheticBuffId = -Math.Abs(summon.SkillId);
            if (HasActiveBuff(syntheticBuffId))
            {
                return true;
            }

            ApplySyntheticSummonBuff(
                syntheticBuffId,
                $"{summon.SkillData.Name} Support",
                summon.SkillData,
                buffLevelData,
                currentTime);

            summon.CurrentAnimationBranchName = SummonRuntimeRules.ResolveLocalSupportBranch(
                summon.SkillData,
                preferHealFirst: false);
            ArmSupportSummonSuspend(summon, currentTime);
            summon.LastAttackAnimationStartTime = currentTime;
            ArmLocalSummonOneTimeActionFallback(summon, currentTime);
            SetSummonActorState(summon, SummonActorState.Attack, currentTime);
            if (summon.SkillData.HitEffect != null)
            {
                Vector2 summonPosition = GetSummonPosition(summon);
                SpawnHitEffect(
                    summon.SkillData.SkillId,
                    summon.SkillData.HitEffect,
                    summonPosition.X,
                    summonPosition.Y - 20f,
                    summon.FacingRight,
                    currentTime);
            }

            return true;
        }

        private IEnumerable<MobItem> ResolveSummonAttackTargetOrder(ActiveSummon summon, IEnumerable<MobItem> candidateTargets, int currentTime)
        {
            if (summon?.SkillId == SG88_SKILL_ID && summon.AssistType == SummonAssistType.ManualAttack)
            {
                return ResolveSg88ManualAttackTargets(summon, candidateTargets, currentTime);
            }

            return OrderSummonTargetsByDistance(summon, candidateTargets, currentTime);
        }

        private IEnumerable<MobItem> ResolveSg88ManualAttackTargets(ActiveSummon summon, IEnumerable<MobItem> candidateTargets, int currentTime)
        {
            if (summon == null || candidateTargets == null)
            {
                return Enumerable.Empty<MobItem>();
            }

            Rectangle summonBounds = GetSummonAttackBounds(summon);
            if (summonBounds.IsEmpty)
            {
                return OrderSummonTargetsByDistance(summon, candidateTargets, currentTime);
            }

            Vector2 summonCenter = GetSummonPosition(summon);
            Vector2 ownerPosition = new(_player.X, _player.Y);
            SkillLevelData levelData = summon.LevelData;
            Rectangle primaryClusterRange = summon.SkillData.GetAttackRange(levelData?.Level ?? summon.Level, facingRight: true);
            var candidates = candidateTargets
                .Where(IsMobAttackable)
                .Select(mob => new
                {
                    Mob = mob,
                    Hitbox = GetMobHitbox(mob, currentTime)
                })
                .Where(entry => !entry.Hitbox.IsEmpty && summonBounds.Intersects(entry.Hitbox))
                .Select(entry => new
                {
                    entry.Mob,
                    entry.Hitbox,
                    Center = GetMobHitboxCenter(entry.Mob, currentTime)
                })
                .ToList();
            if (candidates.Count == 0)
            {
                return Enumerable.Empty<MobItem>();
            }

            int[] orderedIndices = ResolveSg88ManualAttackTargetOrder(
                summonCenter,
                ownerPosition,
                candidates.Select(entry => entry.Hitbox).ToArray(),
                primaryClusterRange);
            if (orderedIndices.Length == 0)
            {
                return Enumerable.Empty<MobItem>();
            }

            return orderedIndices
                .Where(index => index >= 0 && index < candidates.Count)
                .Select(index => candidates[index].Mob);
        }

        private int ResolveSupportSummonHpHeal(ActiveSummon summon)
        {
            if (summon?.LevelData == null)
            {
                return 0;
            }

            if (summon.SkillId == HEALING_ROBOT_SKILL_ID && summon.LevelData.HP > 0)
            {
                return Math.Max(1, _player.MaxHP * summon.LevelData.HP / 100);
            }

            return summon.LevelData.HP;
        }

        private static int ResolveSupportSummonRehealLockMs(ActiveSummon summon)
        {
            if (summon?.LevelData == null)
            {
                return 0;
            }

            return summon.LevelData.X > 0 ? summon.LevelData.X * 1000 : 0;
        }

        private readonly record struct BeholderBuffCandidate(
            int BuffId,
            string Name,
            string AnimationBranchName,
            SkillLevelData LevelData);

        private BeholderBuffCandidate? SelectBeholderBuffCandidate(
            ActiveSummon summon,
            SkillLevelData buffLevelData,
            int randomValue)
        {
            if (buffLevelData == null)
            {
                return null;
            }

            string preferredBranchName = ResolveBeholderPreferredBranchName(
                summon?.SkillData,
                buffLevelData,
                _buffs,
                randomValue);
            if (!string.IsNullOrWhiteSpace(preferredBranchName))
            {
                foreach (BeholderBuffCandidate candidate in EnumerateBeholderBuffCandidates(buffLevelData))
                {
                    if (string.Equals(candidate.AnimationBranchName, preferredBranchName, StringComparison.OrdinalIgnoreCase)
                        && !HasActiveBuff(candidate.BuffId))
                    {
                        return candidate;
                    }
                }

                foreach (BeholderBuffCandidate candidate in EnumerateBeholderBuffCandidates(buffLevelData))
                {
                    if (string.Equals(candidate.AnimationBranchName, preferredBranchName, StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }
                }
            }

            foreach (BeholderBuffCandidate candidate in EnumerateBeholderBuffCandidates(buffLevelData))
            {
                if (!HasActiveBuff(candidate.BuffId))
                {
                    return candidate;
                }
            }

            foreach (BeholderBuffCandidate candidate in EnumerateBeholderBuffCandidates(buffLevelData))
            {
                return candidate;
            }

            return null;
        }

        internal static string ResolveBeholderPreferredBranchName(
            SkillData summonSkill,
            SkillLevelData buffLevelData,
            IEnumerable<ActiveBuff> activeBuffs,
            int randomValue)
        {
            if (buffLevelData == null)
            {
                return null;
            }

            return SummonRuntimeRules.ResolveBeholderBuffBranchName(
                summonSkill,
                buffLevelData,
                ResolveActiveBeholderBuffMagnitude(activeBuffs, BEHOLDER_BUFF_PAD_ID, static levelData => levelData.PAD),
                ResolveActiveBeholderBuffMagnitude(activeBuffs, BEHOLDER_BUFF_PDD_ID, static levelData => levelData.PDD),
                ResolveActiveBeholderBuffMagnitude(activeBuffs, BEHOLDER_BUFF_MDD_ID, static levelData => levelData.MDD),
                ResolveActiveBeholderBuffMagnitude(activeBuffs, BEHOLDER_BUFF_ACC_ID, static levelData => levelData.ACC),
                ResolveActiveBeholderBuffMagnitude(activeBuffs, BEHOLDER_BUFF_EVA_ID, static levelData => levelData.EVA),
                randomValue);
        }

        private static int ResolveActiveBeholderBuffMagnitude(
            IEnumerable<ActiveBuff> activeBuffs,
            int buffId,
            Func<SkillLevelData, int> magnitudeSelector)
        {
            if (activeBuffs == null
                || buffId == 0
                || magnitudeSelector == null)
            {
                return 0;
            }

            int magnitude = 0;
            foreach (ActiveBuff activeBuff in activeBuffs)
            {
                if (activeBuff?.SkillId != buffId || activeBuff.LevelData == null)
                {
                    continue;
                }

                magnitude = Math.Max(magnitude, Math.Max(0, magnitudeSelector(activeBuff.LevelData)));
            }

            return magnitude;
        }

        private IEnumerable<BeholderBuffCandidate> EnumerateBeholderBuffCandidates(SkillLevelData buffLevelData)
        {
            if (buffLevelData.PDD > 0)
            {
                yield return new BeholderBuffCandidate(
                    BuffId: BEHOLDER_BUFF_PDD_ID,
                    Name: "Beholder Buff (PDD)",
                    AnimationBranchName: "skill2",
                    LevelData: new SkillLevelData { Level = buffLevelData.Level, Time = buffLevelData.Time, PDD = buffLevelData.PDD });
            }

            if (buffLevelData.MDD > 0)
            {
                yield return new BeholderBuffCandidate(
                    BuffId: BEHOLDER_BUFF_MDD_ID,
                    Name: "Beholder Buff (MDD)",
                    AnimationBranchName: "skill3",
                    LevelData: new SkillLevelData { Level = buffLevelData.Level, Time = buffLevelData.Time, MDD = buffLevelData.MDD });
            }

            if (buffLevelData.ACC > 0)
            {
                yield return new BeholderBuffCandidate(
                    BuffId: BEHOLDER_BUFF_ACC_ID,
                    Name: "Beholder Buff (ACC)",
                    AnimationBranchName: "skill4",
                    LevelData: new SkillLevelData { Level = buffLevelData.Level, Time = buffLevelData.Time, ACC = buffLevelData.ACC });
            }

            if (buffLevelData.EVA > 0)
            {
                yield return new BeholderBuffCandidate(
                    BuffId: BEHOLDER_BUFF_EVA_ID,
                    Name: "Beholder Buff (EVA)",
                    AnimationBranchName: "skill5",
                    LevelData: new SkillLevelData { Level = buffLevelData.Level, Time = buffLevelData.Time, EVA = buffLevelData.EVA });
            }

            if (buffLevelData.PAD > 0)
            {
                yield return new BeholderBuffCandidate(
                    BuffId: BEHOLDER_BUFF_PAD_ID,
                    Name: "Beholder Buff (PAD)",
                    AnimationBranchName: "skill6",
                    LevelData: new SkillLevelData { Level = buffLevelData.Level, Time = buffLevelData.Time, PAD = buffLevelData.PAD });
            }
        }

        private void ApplySyntheticSummonBuff(int syntheticBuffId, string buffName, SkillData sourceSkill, SkillLevelData levelData, int currentTime)
        {
            if (levelData == null || levelData.Time <= 0)
            {
                return;
            }

            CancelActiveBuff(syntheticBuffId, currentTime, playFinish: false);

            var buff = new ActiveBuff
            {
                SkillId = syntheticBuffId,
                Level = Math.Max(1, levelData.Level),
                StartTime = currentTime,
                Duration = levelData.Time * 1000,
                SkillData = new SkillData
                {
                    SkillId = syntheticBuffId,
                    Name = buffName,
                    Description = sourceSkill?.Name ?? buffName,
                    MinionAbility = sourceSkill?.MinionAbility
                },
                LevelData = levelData
            };

            _buffs.Add(buff);
            OnBuffApplied?.Invoke(buff);
            RegisterClientSkillTimer(syntheticBuffId, ClientTimerSourceBuffExpire, currentTime + buff.Duration, ExpireBuffFromClientTimer);
            ApplyBuffStats(buff, true);
            RefreshBuffControlledFlyingAbility();
        }

        private static string ResolveSupplementalSummonAnimationBranch(SkillData skill, params string[] preferredBranchNames)
        {
            if (skill?.SummonNamedAnimations == null || preferredBranchNames == null)
            {
                return null;
            }

            foreach (string branchName in preferredBranchNames)
            {
                if (!string.IsNullOrWhiteSpace(branchName) && skill.SummonNamedAnimations.ContainsKey(branchName))
                {
                    return branchName;
                }
            }

            bool requestedSupportBranch = preferredBranchNames.Any(branchName =>
                string.Equals(branchName, "heal", StringComparison.OrdinalIgnoreCase)
                || string.Equals(branchName, "support", StringComparison.OrdinalIgnoreCase));
            if (requestedSupportBranch && skill.SummonNamedAnimations.ContainsKey("stand"))
            {
                return "stand";
            }

            return null;
        }

        private static SkillLevelData ResolveFriendlySupportSummonBuffLevelData(ActiveSummon summon)
        {
            if (summon?.SkillData == null || summon.LevelData == null)
            {
                return null;
            }

            SkillLevelData projected = RemoteAffectedAreaSupportResolver.CreateProjectedSupportBuffLevelData(summon.LevelData);
            if (projected != null)
            {
                projected.Time = projected.Time > 0 ? projected.Time : summon.LevelData.Time;
                return projected;
            }

            if (SummonRuntimeRules.HasMinionAbilityToken(summon.SkillData.MinionAbility, "amplifyDamage")
                && summon.LevelData.X > 0
                && summon.LevelData.Time > 0)
            {
                return new SkillLevelData
                {
                    Level = summon.LevelData.Level,
                    Time = summon.LevelData.Time,
                    X = summon.LevelData.X
                };
            }

            return null;
        }

        private bool TryResolveSelfDestructSummon(ActiveSummon summon, int currentTime)
        {
            if (summon?.SkillData?.SelfDestructMinion != true)
            {
                return false;
            }

            return TryBeginNaturalExpirySummonRemoval(summon, currentTime);
        }

        private bool TryBeginNaturalExpirySummonRemoval(ActiveSummon summon, int currentTime)
        {
            if (summon == null || summon.IsPendingRemoval)
            {
                return false;
            }

            summon.ExpiryActionTriggered = true;
            CancelSummonExpiryTimer(summon);
            RemoveSummonPuppet(summon);
            summon.LastAttackAnimationStartTime = currentTime;
            int actionDuration = ResolveSummonPendingRemovalActionDurationMs(summon);
            int removalDuration = ResolveSummonRemovalPlaybackDurationMs(summon);
            summon.RemovalAnimationStartTime = actionDuration > 0
                ? currentTime + actionDuration
                : currentTime;
            summon.PendingRemovalTime = summon.RemovalAnimationStartTime + removalDuration;

            if (actionDuration <= 0)
            {
                summon.ActorState = SummonActorState.Die;
                summon.LastStateChangeTime = currentTime;
            }

            return true;
        }

        private static bool ShouldDeferSummonRemovalPlayback(ActiveSummon summon, int currentTime)
        {
            return summon?.IsPendingRemoval == true
                && summon.RemovalAnimationStartTime != int.MinValue
                && currentTime < summon.RemovalAnimationStartTime;
        }

        private int ResolveSummonPendingRemovalActionDurationMs(ActiveSummon summon)
        {
            if (summon?.SkillData == null)
            {
                return 0;
            }

            int prepareDuration = SummonRuntimeRules.ResolveSummonActionPrepareDurationMs(
                summon.SkillData,
                summon.CurrentAnimationBranchName);
            SkillAnimation actionAnimation = ResolveSummonPendingRemovalActionAnimation(summon);
            int actionDuration = GetSkillAnimationDuration(actionAnimation) ?? 0;
            return actionDuration > 0 ? prepareDuration + actionDuration : 0;
        }

        private SkillAnimation ResolveSummonPendingRemovalActionAnimation(ActiveSummon summon)
        {
            SkillData skill = summon?.SkillData;
            if (skill == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(summon.CurrentAnimationBranchName)
                && _loader.ResolveSummonActionAnimation(skill, summon.Level, summon.CurrentAnimationBranchName) is SkillAnimation namedAnimation
                && namedAnimation.Frames.Count > 0)
            {
                return namedAnimation;
            }

            if (!string.IsNullOrWhiteSpace(summon.CurrentAnimationBranchName))
            {
                SkillAnimation retryAnimation = ResolveSummonEmptyActionRetryAnimation(summon);
                if (retryAnimation?.Frames.Count > 0)
                {
                    return retryAnimation;
                }

                return null;
            }

            string removalBranchName = SummonRuntimeRules.ResolveRemovalPlaybackBranch(skill);
            if (!string.IsNullOrWhiteSpace(removalBranchName))
            {
                SkillAnimation removalAnimation = _loader.ResolveSummonActionAnimation(
                    skill,
                    summon.Level,
                    removalBranchName,
                    skill.SummonRemovalAnimation);
                if (removalAnimation?.Frames.Count > 0)
                {
                    return removalAnimation;
                }
            }

            return ResolveSummonAttackPlaybackAnimation(summon);
        }

        private int ResolveSummonRemovalPlaybackDurationMs(ActiveSummon summon)
        {
            return Math.Max(
                1,
                GetSkillAnimationDuration(_loader.ResolveSummonActionAnimation(
                    summon?.SkillData,
                    summon?.Level ?? 0,
                    SummonRuntimeRules.ResolveRemovalPlaybackBranch(summon?.SkillData),
                    summon?.SkillData?.SummonRemovalAnimation))
                ?? GetSkillAnimationDuration(ResolveSummonHitPlaybackAnimation(summon))
                ?? GetSkillAnimationDuration(ResolveSummonPendingRemovalActionAnimation(summon))
                ?? summon?.SkillData?.HitEffect?.TotalDuration
                ?? 1);
        }

        private void CancelSummonExpiryTimer(ActiveSummon summon)
        {
            if (summon?.SkillId <= 0 || summon.ObjectId <= 0)
            {
                return;
            }

            CancelClientSkillTimers(
                summon.SkillId,
                ClientTimerSourceSummonExpire,
                timerKey: summon.ObjectId,
                matchTimerKey: true);
        }

        private void RegisterSummonExpiryTimer(ActiveSummon summon)
        {
            if (summon?.SkillId <= 0 || summon.Duration <= 0)
            {
                return;
            }

            RegisterClientSkillTimer(
                summon.SkillId,
                ClientTimerSourceSummonExpire,
                summon.StartTime + summon.Duration,
                expireTime => ExpireSummonFromClientTimer(summon.ObjectId, expireTime),
                replaceExisting: false,
                timerKey: summon.ObjectId);
        }

        private void ExpireSummonFromClientTimer(int summonObjectId, int currentTime)
        {
            int summonIndex = FindActiveSummonIndexByObjectId(summonObjectId);
            if (summonIndex < 0)
            {
                return;
            }

            ActiveSummon summon = _summons[summonIndex];
            if (summon == null)
            {
                _summons.RemoveAt(summonIndex);
                return;
            }

            if (summon.IsPendingRemoval)
            {
                return;
            }

            if (TryRouteExpiredLocalSummonToClientCancel(summon, currentTime))
            {
                return;
            }

            RemoveSummonAt(summonIndex, cancelTimer: false);
        }

        private bool TryRouteExpiredLocalSummonToClientCancel(ActiveSummon summon, int currentTime)
        {
            if (!TryPrimeExpiredLocalSummonNaturalExpiry(summon, currentTime))
            {
                return false;
            }

            RequestClientSkillCancel(summon.SkillId, currentTime);
            return true;
        }

        private bool TryPrimeExpiredLocalSummonNaturalExpiry(int summonObjectId, int currentTime)
        {
            int summonIndex = FindActiveSummonIndexByObjectId(summonObjectId);
            if (summonIndex < 0)
            {
                return false;
            }

            return TryPrimeExpiredLocalSummonNaturalExpiry(_summons[summonIndex], currentTime);
        }

        private bool TryPrimeExpiredLocalSummonNaturalExpiry(ActiveSummon summon, int currentTime)
        {
            if (summon == null
                || summon.IsPendingRemoval
                || summon.ExpiryActionTriggered
                || !summon.HasReachedNaturalExpiry(currentTime))
            {
                return false;
            }

            if (TryTriggerExpiredSelfDestructSummon(summon, currentTime))
            {
                return true;
            }

            return TryBeginNaturalExpirySummonRemoval(summon, currentTime);
        }

        private static bool UsesReactiveDamageTriggerSummon(SkillData skill)
        {
            return SummonRuntimeRules.UsesReactiveDamageTriggerSummon(skill);
        }

        private static bool UsesOwnerAttackTargetedSummon(SkillData skill)
        {
            return skill != null && SummonRuntimeRules.IsSatelliteSummonSkill(skill.SkillId);
        }

        private void UpdateRepeatSkillSustainFromSummonAttack(ActiveSummon summon, int currentTime)
        {
            if (summon == null
                || summon.SkillId != SG88_SKILL_ID
                || _activeRepeatSkillSustain == null
                || _activeRepeatSkillSustain.SkillId != summon.SkillId)
            {
                return;
            }

            _activeRepeatSkillSustain.LastAttackStartTime = currentTime;
            _activeRepeatSkillSustain.IsDone = true;
            _activeRepeatSkillSustain.BranchPoint = Math.Max(_activeRepeatSkillSustain.BranchPoint, 2);
        }

        private void RecordOwnerAttackTarget(MobItem mob, int currentTime)
        {
            if (mob?.AI == null || mob.PoolId <= 0)
            {
                return;
            }

            _recentOwnerAttackTargetTimes[mob.PoolId] = currentTime;
        }

        private IEnumerable<MobItem> ResolveRecentOwnerAttackTargets(int currentTime)
        {
            if (_mobPool?.ActiveMobs == null || _recentOwnerAttackTargetTimes.Count == 0)
            {
                yield break;
            }

            foreach (int mobId in _recentOwnerAttackTargetTimes
                         .Where(entry => currentTime - entry.Value <= OWNER_ATTACK_TARGET_MEMORY_MS)
                         .OrderByDescending(entry => entry.Value)
                         .Select(entry => entry.Key)
                         .ToArray())
            {
                MobItem mob = FindAttackableMobByPoolId(mobId, currentTime);
                if (mob == null)
                {
                    _recentOwnerAttackTargetTimes.Remove(mobId);
                    continue;
                }

                yield return mob;
            }
        }

        private static SummonAssistType ResolveSummonAssistType(SkillData skill)
        {
            return SummonRuntimeRules.ResolveAssistType(skill);
        }

        private static bool ShouldRegisterSummonPuppet(SkillData skill)
        {
            return SummonRuntimeRules.ShouldRegisterPuppet(skill);
        }

        private static bool HasMinionAbilityToken(string minionAbility, string token)
        {
            return SummonRuntimeRules.HasMinionAbilityToken(minionAbility, token);
        }

        private List<ActiveSummon> GetActiveTeslaCoils(int currentTime)
        {
            return _summons
                .Where(candidate => candidate?.SkillId == TESLA_COIL_SKILL_ID
                                    && !candidate.IsPendingRemoval
                                    && !candidate.IsExpired(currentTime))
                .OrderBy(candidate => candidate.SummonSlotIndex >= 0 ? candidate.SummonSlotIndex : int.MaxValue)
                .ThenBy(candidate => candidate.StartTime)
                .ThenBy(candidate => candidate.ObjectId)
                .Take(3)
                .ToList();
        }

        private void UpdateSummonFacingTowardMob(ActiveSummon summon, MobItem target, int currentTime)
        {
            if (summon == null || target == null)
            {
                return;
            }

            Vector2 summonPosition = GetSummonPosition(summon);
            Vector2 targetCenter = GetMobHitboxCenter(target, currentTime);
            if (MathF.Abs(targetCenter.X - summonPosition.X) <= 0.5f)
            {
                return;
            }

            summon.FacingRight = targetCenter.X >= summonPosition.X;
        }

        private Vector2 GetSummonPosition(ActiveSummon summon)
        {
            return new Vector2(summon.PositionX, summon.PositionY);
        }

        private static int GetSummonAttackInterval(SkillData skill, int level)
        {
            return Math.Max(90, skill?.ResolveSummonAttackIntervalMs(level) ?? 1000);
        }

        private static bool HasSummonActionIntervalElapsed(ActiveSummon summon, int currentTime)
        {
            if (summon == null)
            {
                return false;
            }

            // Client `TryDoingSitdownHealing` polls Healing Robot every seated frame and lets
            // `CSummoned::TryDoingHealingRobot` enforce the per-user `x`-second reheal lock.
            if (summon.SkillId == HEALING_ROBOT_SKILL_ID
                && summon.AssistType == SummonAssistType.Support)
            {
                return true;
            }

            return currentTime - summon.LastAttackTime >= GetSummonAttackInterval(summon.SkillData, summon.Level);
        }

        private bool IsMobInSummonAttackRange(ActiveSummon summon, Rectangle mobHitbox)
        {
            if (summon?.SkillData == null || mobHitbox.IsEmpty)
            {
                return false;
            }

            Rectangle worldHitbox = GetSummonAttackBounds(summon);
            if (!worldHitbox.IsEmpty)
            {
                return worldHitbox.Intersects(mobHitbox);
            }

            Vector2 summonPosition = GetSummonPosition(summon);
            Point centerOffset = summon.SkillData.GetSummonAttackCircleCenterOffset(summon.FacingRight);
            Vector2 circleCenter = new(
                summonPosition.X + centerOffset.X,
                summonPosition.Y + centerOffset.Y);
            return DoesRectangleIntersectCircle(mobHitbox, circleCenter, summon.SkillData.SummonAttackRadius);
        }

        private Rectangle GetSummonAttackBounds(ActiveSummon summon)
        {
            if (summon?.SkillData == null)
            {
                return Rectangle.Empty;
            }

            Vector2 summonPosition = GetSummonPosition(summon);
            Rectangle localHitbox = summon.AssistType == SummonAssistType.Support
                ? SummonRuntimeRules.ResolveSupportOwnedRange(
                    summon.SkillData,
                    summon.FacingRight,
                    summon.CurrentAnimationBranchName)
                : summon.SkillData.TryGetSummonAttackRange(
                    summon.FacingRight,
                    summon.CurrentAnimationBranchName,
                    out Rectangle branchRange)
                    ? branchRange
                    : summon.SkillData.GetSummonAttackRange(summon.FacingRight);
            if (!localHitbox.IsEmpty)
            {
                return new Rectangle(
                    (int)summonPosition.X + localHitbox.X,
                    (int)summonPosition.Y + localHitbox.Y,
                    localHitbox.Width,
                    localHitbox.Height);
            }

            int attackRadius = summon.SkillData.ResolveSummonAttackRadius(summon.CurrentAnimationBranchName);
            if (attackRadius > 0)
            {
                Point centerOffset = summon.SkillData.GetSummonAttackCircleCenterOffset(
                    summon.FacingRight,
                    summon.CurrentAnimationBranchName);
                int radius = (int)MathF.Ceiling(attackRadius);
                return new Rectangle(
                    (int)summonPosition.X + centerOffset.X - radius,
                    (int)summonPosition.Y + centerOffset.Y - radius,
                    Math.Max(1, radius * 2),
                    Math.Max(1, radius * 2));
            }

            return new Rectangle(
                (int)summonPosition.X - 90,
                (int)summonPosition.Y - 70,
                180,
                100);
        }

        private static bool DoesRectangleIntersectCircle(Rectangle rectangle, Vector2 circleCenter, float radius)
        {
            float closestX = Math.Clamp(circleCenter.X, rectangle.Left, rectangle.Right);
            float closestY = Math.Clamp(circleCenter.Y, rectangle.Top, rectangle.Bottom);
            float deltaX = circleCenter.X - closestX;
            float deltaY = circleCenter.Y - closestY;
            return deltaX * deltaX + deltaY * deltaY <= radius * radius;
        }

        private static bool TryBuildTeslaCoilTriangle(
            IReadOnlyList<ActiveSummon> coils,
            out Vector2 left,
            out Vector2 apex,
            out Vector2 right)
        {
            left = Vector2.Zero;
            apex = Vector2.Zero;
            right = Vector2.Zero;
            if (coils == null || coils.Count < 3)
            {
                return false;
            }

            ActiveSummon[] orderedCoils = coils
                .Where(coil => coil != null)
                .OrderBy(coil => coil.SummonSlotIndex >= 0 ? coil.SummonSlotIndex : int.MaxValue)
                .ThenBy(coil => coil.StartTime)
                .ThenBy(coil => coil.ObjectId)
                .Take(3)
                .ToArray();
            if (orderedCoils.Length < 3)
            {
                return false;
            }

            left = new Vector2(orderedCoils[0].PositionX, orderedCoils[0].PositionY);
            apex = new Vector2(orderedCoils[1].PositionX, orderedCoils[1].PositionY);
            right = new Vector2(orderedCoils[2].PositionX, orderedCoils[2].PositionY);
            return Vector2.DistanceSquared(left, apex) >= TeslaTriangleMinimumEdgeLengthSq
                && Vector2.DistanceSquared(apex, right) >= TeslaTriangleMinimumEdgeLengthSq
                && Vector2.DistanceSquared(left, right) >= TeslaTriangleMinimumEdgeLengthSq;
        }

        private static Vector2[] ResolveTeslaProjectileSources(IReadOnlyList<ActiveSummon> coils)
        {
            if (coils == null || coils.Count == 0)
            {
                return Array.Empty<Vector2>();
            }

            Vector2[] triangleVertices = ResolveTeslaTriangleVerticesOrFallback(coils);
            if (triangleVertices.Length == 0)
            {
                return Array.Empty<Vector2>();
            }

            Vector2[] assignedSources = new Vector2[coils.Count];
            List<Vector2> remainingVertices = triangleVertices.ToList();
            for (int i = 0; i < coils.Count; i++)
            {
                ActiveSummon coil = coils[i];
                Vector2 summonPosition = coil == null
                    ? Vector2.Zero
                    : new Vector2(coil.PositionX, coil.PositionY);
                if (remainingVertices.Count == 0)
                {
                    assignedSources[i] = summonPosition;
                    continue;
                }

                int nearestIndex = 0;
                float nearestDistance = Vector2.DistanceSquared(summonPosition, remainingVertices[0]);
                for (int vertexIndex = 1; vertexIndex < remainingVertices.Count; vertexIndex++)
                {
                    float distance = Vector2.DistanceSquared(summonPosition, remainingVertices[vertexIndex]);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestIndex = vertexIndex;
                    }
                }

                assignedSources[i] = remainingVertices[nearestIndex];
                remainingVertices.RemoveAt(nearestIndex);
            }

            return assignedSources;
        }

        private static Vector2[] ResolveTeslaTriangleVerticesOrFallback(IReadOnlyList<ActiveSummon> coils)
        {
            if (TryResolveTeslaTriangleVertices(coils, out Vector2 left, out Vector2 apex, out Vector2 right))
            {
                return new[] { left, apex, right };
            }

            if (TryBuildTeslaCoilTriangle(coils, out Vector2 fallbackLeft, out Vector2 fallbackApex, out Vector2 fallbackRight))
            {
                return new[] { fallbackLeft, fallbackApex, fallbackRight };
            }

            return coils
                .Where(static coil => coil != null)
                .Select(static coil => new Vector2(coil.PositionX, coil.PositionY))
                .Take(3)
                .ToArray();
        }

        private static bool TryResolveTeslaTriangleVertices(
            IReadOnlyList<ActiveSummon> coils,
            out Vector2 left,
            out Vector2 apex,
            out Vector2 right)
        {
            left = Vector2.Zero;
            apex = Vector2.Zero;
            right = Vector2.Zero;
            if (coils == null || coils.Count == 0)
            {
                return false;
            }

            Point[] trianglePoints = coils
                .Select(static coil => coil?.TeslaTrianglePoints)
                .FirstOrDefault(static points => points?.Length >= 3);
            if (trianglePoints == null || trianglePoints.Length < 3)
            {
                return false;
            }

            left = new Vector2(trianglePoints[0].X, trianglePoints[0].Y);
            apex = new Vector2(trianglePoints[1].X, trianglePoints[1].Y);
            right = new Vector2(trianglePoints[2].X, trianglePoints[2].Y);
            return true;
        }

        private int ResolveTeslaAttackDelayWindowMs(ActiveSummon summon)
        {
            if (summon?.SkillData == null)
            {
                return TeslaMinimumImpactDelayMs;
            }

            return Math.Max(
                TeslaMinimumImpactDelayMs,
                GetSummonAttackInterval(summon.SkillData, summon.Level));
        }

        private static Rectangle GetTriangleBounds(Vector2 a, Vector2 b, Vector2 c)
        {
            int left = (int)MathF.Floor(MathF.Min(a.X, MathF.Min(b.X, c.X)));
            int top = (int)MathF.Floor(MathF.Min(a.Y, MathF.Min(b.Y, c.Y)));
            int right = (int)MathF.Ceiling(MathF.Max(a.X, MathF.Max(b.X, c.X)));
            int bottom = (int)MathF.Ceiling(MathF.Max(a.Y, MathF.Max(b.Y, c.Y)));
            return new Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
        }

        private static bool DoesRectangleIntersectTriangle(Rectangle rectangle, Vector2 a, Vector2 b, Vector2 c)
        {
            if (rectangle.IsEmpty)
            {
                return false;
            }

            Vector2 topLeft = new(rectangle.Left, rectangle.Top);
            Vector2 topRight = new(rectangle.Right, rectangle.Top);
            Vector2 bottomRight = new(rectangle.Right, rectangle.Bottom);
            Vector2 bottomLeft = new(rectangle.Left, rectangle.Bottom);
            Vector2[] corners = { topLeft, topRight, bottomRight, bottomLeft };

            if (corners.Any(point => IsPointInTriangle(point, a, b, c)))
            {
                return true;
            }

            if (IsPointInRectangle(a, rectangle) || IsPointInRectangle(b, rectangle) || IsPointInRectangle(c, rectangle))
            {
                return true;
            }

            Vector2[] trianglePoints = { a, b, c };
            for (int triangleIndex = 0; triangleIndex < trianglePoints.Length; triangleIndex++)
            {
                Vector2 triangleStart = trianglePoints[triangleIndex];
                Vector2 triangleEnd = trianglePoints[(triangleIndex + 1) % trianglePoints.Length];
                for (int rectIndex = 0; rectIndex < corners.Length; rectIndex++)
                {
                    Vector2 rectStart = corners[rectIndex];
                    Vector2 rectEnd = corners[(rectIndex + 1) % corners.Length];
                    if (DoSegmentsIntersect(triangleStart, triangleEnd, rectStart, rectEnd))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsPointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(point, a, b);
            float d2 = Cross(point, b, c);
            float d3 = Cross(point, c, a);
            bool hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNegative && hasPositive);
        }

        private static bool IsPointInRectangle(Vector2 point, Rectangle rectangle)
        {
            return point.X >= rectangle.Left
                && point.X <= rectangle.Right
                && point.Y >= rectangle.Top
                && point.Y <= rectangle.Bottom;
        }

        private static bool DoSegmentsIntersect(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
        {
            float d1 = Cross(a1, a2, b1);
            float d2 = Cross(a1, a2, b2);
            float d3 = Cross(b1, b2, a1);
            float d4 = Cross(b1, b2, a2);

            if (((d1 > 0f && d2 < 0f) || (d1 < 0f && d2 > 0f))
                && ((d3 > 0f && d4 < 0f) || (d3 < 0f && d4 > 0f)))
            {
                return true;
            }

            return (MathF.Abs(d1) <= float.Epsilon && IsPointOnSegment(a1, a2, b1))
                || (MathF.Abs(d2) <= float.Epsilon && IsPointOnSegment(a1, a2, b2))
                || (MathF.Abs(d3) <= float.Epsilon && IsPointOnSegment(b1, b2, a1))
                || (MathF.Abs(d4) <= float.Epsilon && IsPointOnSegment(b1, b2, a2));
        }

        private static bool IsPointOnSegment(Vector2 start, Vector2 end, Vector2 point)
        {
            return point.X >= MathF.Min(start.X, end.X)
                && point.X <= MathF.Max(start.X, end.X)
                && point.Y >= MathF.Min(start.Y, end.Y)
                && point.Y <= MathF.Max(start.Y, end.Y);
        }

        private static float Cross(Vector2 origin, Vector2 first, Vector2 second)
        {
            return (first.X - origin.X) * (second.Y - origin.Y)
                 - (first.Y - origin.Y) * (second.X - origin.X);
        }

        private void NotifyAttackAreaResolved(Rectangle worldHitbox, int currentTime, int skillId, int damage = 1)
        {
            if (worldHitbox.Width <= 0 || worldHitbox.Height <= 0)
            {
                return;
            }

            OnAttackAreaResolved?.Invoke(worldHitbox, currentTime, skillId, Math.Max(1, damage));
        }

        private void UpdateMine(int currentTime)
        {
            SkillData mineSkill = GetSkillData(MINE_SKILL_ID);
            int mineLevel = GetSkillLevel(MINE_SKILL_ID);
            if (mineSkill == null
                || mineLevel <= 0
                || !_player.IsAlive
                || _player.Build == null
                || !CanAutoDeployMine(currentTime)
                || !string.IsNullOrWhiteSpace(GetStateRestrictionMessage(mineSkill, currentTime))
                || (_fieldSkillRestrictionEvaluator != null && !_fieldSkillRestrictionEvaluator(mineSkill)))
            {
                ResetMineMovementState();
                return;
            }

            int moveDirection = GetMineMovementDirection();
            UpdateMineMovementState(moveDirection, currentTime);

            if (moveDirection == 0 || currentTime - _mineMovementStartTime < MINE_DEPLOY_INTERVAL_MS)
            {
                return;
            }

            SkillLevelData levelData = mineSkill.GetLevel(mineLevel);
            if (levelData == null || !TryConsumeSkillResources(mineSkill, levelData))
            {
                return;
            }

            SpawnSummon(mineSkill, mineLevel, currentTime);
            _mineMovementStartTime = currentTime;
        }

        private bool CanAutoDeployMine(int currentTime)
        {
            if (_currentMapInfoProvider?.Invoke()?.town == true)
            {
                return false;
            }

            CharacterPart mountedStatePart = _player?.ResolveMountedStateTamingMobPart()
                ?? GetEquippedTamingMobPart();
            if (CanAutoDeployWildHunterMine(mountedStatePart, _player?.CurrentActionName))
            {
                return true;
            }

            int activeSkillMountSkillId = _activeSkillMount?.SkillId ?? 0;
            int activeSkillMountItemId = _activeSkillMount?.MountItemId ?? 0;
            int equippedMountItemId = GetEquippedTamingMobPart()?.ItemId ?? 0;
            bool jaguarRideOwnershipActive = HasActiveClientOwnedVehicleTrackingOwner(WildHunterJaguarRiderSkillId)
                || IsActiveWildHunterJaguarRiderMount(activeSkillMountSkillId, activeSkillMountItemId)
                || (IsClientVehicleOwnershipGraceWindowActive(currentTime)
                    && _lastClientVehicleValidSkillId == WildHunterJaguarRiderSkillId);
            return CanAutoDeployWildHunterMineWithoutVisibleMountedAction(
                _player?.CurrentActionName,
                jaguarRideOwnershipActive,
                ResolveWildHunterMineHiddenFallbackMountItemId(
                    activeSkillMountSkillId,
                    activeSkillMountItemId,
                    equippedMountItemId));
        }

        internal static bool CanAutoDeployWildHunterMine(CharacterPart mountedStatePart, string actionName)
        {
            int mountedVehicleId = FollowCharacterEligibilityResolver.ResolveMountedVehicleId(
                mountedStatePart,
                actionName,
                mechanicMode: null,
                activeMountedRenderOwner: mountedStatePart);
            return IsWildHunterJaguarTamingMobItemId(mountedVehicleId);
        }

        internal static bool CanAutoDeployWildHunterMineWithoutVisibleMountedAction(
            string actionName,
            bool jaguarRideOwnershipActive,
            int jaguarMountItemId)
        {
            // Client `TryDoingMine` gates on the live riding-vehicle id; when no mounted action is
            // currently visible, only keep the simulator fallback while Jaguar Rider ownership is
            // still active (or in the existing short grace window), not merely because a jaguar is equipped.
            return string.IsNullOrWhiteSpace(actionName)
                   && jaguarRideOwnershipActive
                   && IsWildHunterJaguarTamingMobItemId(jaguarMountItemId);
        }

        internal static int ResolveWildHunterMineHiddenFallbackMountItemId(
            int activeSkillMountSkillId,
            int activeSkillMountItemId,
            int equippedMountItemId)
        {
            if (IsActiveWildHunterJaguarRiderMount(activeSkillMountSkillId, activeSkillMountItemId))
            {
                return activeSkillMountItemId;
            }

            return IsWildHunterJaguarTamingMobItemId(equippedMountItemId)
                ? equippedMountItemId
                : 0;
        }

        internal static bool IsActiveWildHunterJaguarRiderMount(int activeSkillMountSkillId, int activeSkillMountItemId)
        {
            return activeSkillMountSkillId == WildHunterJaguarRiderSkillId
                   && IsWildHunterJaguarTamingMobItemId(activeSkillMountItemId);
        }

        private void UpdateMineMovementState(int moveDirection, int currentTime)
        {
            if (_mineMovementDirection == 0 && moveDirection != 0)
            {
                _mineMovementDirection = moveDirection;
                _mineMovementStartTime = currentTime - MineInitialDeployLeadMs;
                return;
            }

            if (moveDirection != _mineMovementDirection)
            {
                _mineMovementDirection = moveDirection;
                _mineMovementStartTime = currentTime;
            }
        }

        private int GetMineMovementDirection()
        {
            if (_player?.Physics == null)
            {
                return 0;
            }

            return ResolveWildHunterMineMovementDirection(
                _player.Physics.IsOnFoothold(),
                _player.HorizontalInputDirection,
                _player.Physics.IsStopped());
        }

        internal static int ResolveWildHunterMineMovementDirection(
            bool isOnFoothold,
            int inputDirection,
            bool isStopped)
        {
            if (!isOnFoothold || isStopped)
            {
                return 0;
            }

            return inputDirection > 0
                ? 1
                : inputDirection < 0
                    ? -1
                    : 0;
        }

        private void ResetMineMovementState()
        {
            _mineMovementDirection = 0;
            _mineMovementStartTime = 0;
        }

        private void SyncSummonPuppet(ActiveSummon summon, int currentTime)
        {
            if (_mobPool == null || summon == null)
                return;

            if (summon.IsPendingRemoval || !ShouldRegisterSummonPuppet(summon.SkillData))
            {
                _mobPool.RemovePuppet(summon.ObjectId);
                return;
            }

            Vector2 summonPosition = GetSummonPosition(summon);
            int expirationTime = summon.Duration > 0 ? summon.StartTime + summon.Duration : 0;
            summon.SummonSlotIndex = GetSummonSlotIndex(summon, currentTime);
            float aggroRange = Math.Max(220f, Math.Abs(summon.PositionX - _player.X) + 170f);

            _mobPool.RegisterPuppet(new PuppetInfo
            {
                ObjectId = summon.ObjectId,
                SummonSlotIndex = summon.SummonSlotIndex,
                X = summonPosition.X,
                Y = summonPosition.Y,
                Hitbox = GetSummonHitbox(summon, currentTime),
                IsGrounded = IsSummonGroundedForMobJumpAttack(summon),
                OwnerId = _player.Build?.Id ?? 0,
                AggroValue = 1,
                AggroRange = aggroRange,
                ExpirationTime = expirationTime,
                IsActive = true
            });

            _mobPool.LetMobChasePuppet(summonPosition.X, summonPosition.Y, aggroRange, summon.ObjectId);
        }

        private void RemoveSummonPuppet(ActiveSummon summon)
        {
            if (_mobPool == null || summon == null)
                return;

            _mobPool.RemovePuppet(summon.ObjectId);
        }

        private int FindActiveSummonIndexByObjectId(int objectId)
        {
            for (int i = 0; i < _summons.Count; i++)
            {
                if (_summons[i]?.ObjectId == objectId)
                {
                    return i;
                }
            }

            return -1;
        }

        private void RemoveSummon(ActiveSummon summon, bool cancelTimer)
        {
            if (summon == null)
            {
                return;
            }

            int summonIndex = FindActiveSummonIndexByObjectId(summon.ObjectId);
            if (summonIndex >= 0)
            {
                RemoveSummonAt(summonIndex, cancelTimer);
            }
        }

        private void RemoveSummonAt(int index, bool cancelTimer)
        {
            if (index < 0 || index >= _summons.Count)
            {
                return;
            }

            ActiveSummon summon = _summons[index];
            _summons.RemoveAt(index);
            RemoveSummonPuppet(summon);

            if (cancelTimer)
            {
                CancelSummonExpiryTimer(summon);
            }
        }

        private void ClearSummonPuppets()
        {
            if (_mobPool == null)
                return;

            foreach (ActiveSummon summon in _summons)
            {
                _mobPool.RemovePuppet(summon.ObjectId);
            }
        }

        private int GetSummonSlotIndex(ActiveSummon summon, int currentTime)
        {
            if (summon == null)
            {
                return -1;
            }

            int slotIndex = 0;
            foreach (ActiveSummon candidate in _summons)
            {
                if (candidate == null
                    || candidate.IsPendingRemoval
                    || candidate.IsExpired(currentTime))
                {
                    continue;
                }

                if (ReferenceEquals(candidate, summon))
                {
                    return slotIndex;
                }

                slotIndex++;
            }

            return -1;
        }

        public IReadOnlyList<ActiveSummon> ActiveSummons => _summons;

        #endregion

        public bool TryConsumeSummonByObjectId(int objectId)
        {
            if (objectId <= 0)
                return false;

            for (int i = _summons.Count - 1; i >= 0; i--)
            {
                ActiveSummon summon = _summons[i];
                if (summon?.ObjectId != objectId)
                    continue;

                RemoveSummonAt(i, cancelTimer: true);
                return true;
            }

            return false;
        }

        public bool TryDamageSummonByObjectId(int objectId, int damage, int currentTime)
        {
            if (objectId <= 0)
            {
                return false;
            }

            for (int i = _summons.Count - 1; i >= 0; i--)
            {
                ActiveSummon summon = _summons[i];
                if (summon?.ObjectId != objectId)
                {
                    continue;
                }

                return ApplySummonDamage(summon, Math.Max(1, damage), currentTime, allowSelfDestructFinalAction: true);
            }

            return false;
        }

        private void UpdateSummonPosition(ActiveSummon summon, int currentTime, float deltaTime)
        {
            if (summon == null)
                return;

            if (summon.NeedsAnchorReset && SummonMovementResolver.IsAnchorBound(summon.MovementStyle))
            {
                Vector2 respawnPosition = SummonMovementResolver.ResolveSpawnPosition(
                    summon.MovementStyle,
                    summon.SpawnDistanceX,
                    _player.Position,
                    _player.FacingRight);
                respawnPosition = SettleSummonOnFoothold(summon.MovementStyle, respawnPosition);

                summon.AnchorX = respawnPosition.X;
                summon.AnchorY = respawnPosition.Y;
                summon.PositionX = respawnPosition.X;
                summon.PositionY = respawnPosition.Y;
                summon.NeedsAnchorReset = false;
            }

            float elapsedSeconds = Math.Max(0f, (currentTime - summon.StartTime) / 1000f);
            Vector2 playerPosition = _player.Position;
            Vector2 targetPosition = summon.MovementStyle switch
            {
                SummonMovementStyle.GroundFollow => new Vector2(
                    playerPosition.X + (_player.FacingRight ? 70f : -70f),
                    playerPosition.Y - 25f),
                SummonMovementStyle.HoverFollow => new Vector2(
                    playerPosition.X + (_player.FacingRight ? 60f : -60f) + MathF.Sin(elapsedSeconds * 2.1f + summon.ObjectId) * 14f,
                    playerPosition.Y - 65f + MathF.Cos(elapsedSeconds * 3.3f + summon.ObjectId * 0.5f) * 8f),
                SummonMovementStyle.DriftAroundOwner => new Vector2(
                    playerPosition.X + MathF.Cos(elapsedSeconds * 1.6f + summon.ObjectId) * 65f,
                    playerPosition.Y - 52f + MathF.Sin(elapsedSeconds * 2.8f + summon.ObjectId * 0.75f) * 18f),
                SummonMovementStyle.HoverAroundAnchor => new Vector2(
                    summon.AnchorX + MathF.Sin(elapsedSeconds * 1.3f + summon.ObjectId) * 80f,
                    summon.AnchorY - 35f + MathF.Cos(elapsedSeconds * 2.0f + summon.ObjectId * 0.35f) * 16f),
                _ => new Vector2(summon.AnchorX, summon.AnchorY)
            };
            targetPosition = SettleSummonOnFoothold(summon.MovementStyle, targetPosition);

            if (deltaTime <= 0f)
            {
                summon.PositionX = targetPosition.X;
                summon.PositionY = targetPosition.Y;
            }
            else if (summon.MovementStyle == SummonMovementStyle.GroundFollow
                     || summon.MovementStyle == SummonMovementStyle.HoverFollow)
            {
                float followSpeed = summon.MovementStyle == SummonMovementStyle.GroundFollow ? 220f : 260f;
                summon.PositionX = MoveTowards(summon.PositionX, targetPosition.X, followSpeed * deltaTime);
                summon.PositionY = MoveTowards(summon.PositionY, targetPosition.Y, (followSpeed + 40f) * deltaTime);
            }
            else
            {
                summon.PositionX = targetPosition.X;
                summon.PositionY = targetPosition.Y;
            }

            if (summon.MovementStyle == SummonMovementStyle.GroundFollow
                || summon.MovementStyle == SummonMovementStyle.HoverFollow
                || summon.MovementStyle == SummonMovementStyle.DriftAroundOwner)
            {
                summon.FacingRight = _player.FacingRight;
            }

            if ((summon.MovementStyle == SummonMovementStyle.Stationary
                 || summon.MovementStyle == SummonMovementStyle.HoverAroundAnchor)
                && currentTime - summon.LastPassiveEffectTime >= SUMMON_PASSIVE_EFFECT_COOLDOWN_MS)
            {
                float movedDistanceSq = Vector2.DistanceSquared(
                    new Vector2(summon.PreviousPositionX, summon.PreviousPositionY),
                    new Vector2(summon.PositionX, summon.PositionY));
                if (movedDistanceSq >= 36f)
                {
                    SkillAnimation passiveEffect = summon.SkillData?.Effect ?? summon.SkillData?.AffectedEffect;
                    if (passiveEffect?.Frames.Count > 0)
                    {
                        SpawnHitEffect(
                            summon.SkillId,
                            passiveEffect,
                            summon.PositionX,
                            summon.PositionY,
                            summon.FacingRight,
                            currentTime);
                        summon.LastPassiveEffectTime = currentTime;
                    }
                }
            }
        }

        private Rectangle GetSummonHitbox(ActiveSummon summon, int currentTime)
        {
            if (summon == null)
                return Rectangle.Empty;

            int elapsed = currentTime - summon.StartTime;
            SkillAnimation animation = ResolveSummonAnimation(summon, currentTime, elapsed, out int animationTime)
                ?? summon.SkillData?.AffectedEffect
                ?? summon.SkillData?.Effect;
            SkillFrame frame = animation?.GetFrameAtTime(animationTime);
            if (frame?.Texture == null)
            {
                Vector2 position = GetSummonPosition(summon);
                return new Rectangle((int)position.X - 24, (int)position.Y - 60, 48, 60);
            }

            Vector2 summonPosition = GetSummonPosition(summon);
            bool shouldFlip = summon.FacingRight ^ frame.Flip;
            int drawX = GetFrameDrawX((int)summonPosition.X, frame, shouldFlip);
            int drawY = (int)summonPosition.Y - frame.Origin.Y;
            return new Rectangle(
                drawX,
                drawY,
                Math.Max(1, frame.Texture.Width),
                Math.Max(1, frame.Texture.Height));
        }

        private Rectangle GetSummonContactBounds(ActiveSummon summon, int currentTime)
        {
            Rectangle currentBounds = GetSummonHitbox(summon, currentTime);
            if (summon == null || currentBounds.IsEmpty)
            {
                return currentBounds;
            }

            int deltaX = (int)MathF.Round(summon.PreviousPositionX - summon.PositionX);
            int deltaY = (int)MathF.Round(summon.PreviousPositionY - summon.PositionY);
            if (deltaX == 0 && deltaY == 0)
            {
                return currentBounds;
            }

            Rectangle previousBounds = new Rectangle(
                currentBounds.X + deltaX,
                currentBounds.Y + deltaY,
                currentBounds.Width,
                currentBounds.Height);
            return UnionRectangles(currentBounds, previousBounds);
        }

        private static bool IsSummonGroundedForMobJumpAttack(ActiveSummon summon)
        {
            if (summon == null)
            {
                return false;
            }

            return summon.MovementStyle == SummonMovementStyle.Stationary
                || summon.MovementStyle == SummonMovementStyle.GroundFollow;
        }

        private Vector2 SettleSummonOnFoothold(SummonMovementStyle movementStyle, Vector2 targetPosition)
        {
            if (_footholdLookup == null)
            {
                return targetPosition;
            }

            bool needsGrounding = movementStyle == SummonMovementStyle.Stationary
                || movementStyle == SummonMovementStyle.GroundFollow
                || movementStyle == SummonMovementStyle.HoverAroundAnchor;
            if (!needsGrounding)
            {
                return targetPosition;
            }

            float searchRange = movementStyle == SummonMovementStyle.GroundFollow ? 80f : 140f;
            FootholdLine foothold = _footholdLookup(targetPosition.X, targetPosition.Y + GroundedSummonVisualYOffset, searchRange);
            if (foothold == null)
            {
                return targetPosition;
            }

            float minX = Math.Min(foothold.FirstDot.X, foothold.SecondDot.X);
            float maxX = Math.Max(foothold.FirstDot.X, foothold.SecondDot.X);
            float groundedX = Math.Clamp(targetPosition.X, minX, maxX);
            float groundedY = Board.CalculateYOnFoothold(foothold, groundedX) - GroundedSummonVisualYOffset;
            return new Vector2(groundedX, groundedY);
        }

        #region Buff System

        private void ApplyBuff(SkillData skill, int level, int currentTime)
        {
            var levelData = skill.GetLevel(level);
            if (levelData == null || levelData.Time <= 0)
                return;

            CancelReplacedBuffsOnApply(skill.SkillId, currentTime);

            var buff = new ActiveBuff
            {
                SkillId = skill.SkillId,
                Level = level,
                StartTime = currentTime,
                Duration = levelData.Time * 1000, // Convert seconds to ms
                SkillData = skill,
                LevelData = levelData
            };

            _buffs.Add(buff);
            _player.ApplySkillAvatarTransform(skill.SkillId, actionName: null, morphTemplateId: skill.MorphId);
            UpdateClientOwnedAvatarEffectSuppression(skill, suppress: true);
            BeginEnergyChargeRuntime(buff, currentTime);
            if (ShouldApplyBuffAvatarEffectOnActivation(skill))
            {
                _player.ApplySkillAvatarEffect(skill.SkillId, ResolveBuffAvatarEffectSkill(skill), currentTime);
            }
            ApplyShadowPartner(buff, currentTime);
            OnBuffApplied?.Invoke(buff);
            RegisterClientSkillTimer(skill.SkillId, ClientTimerSourceBuffExpire, currentTime + buff.Duration, ExpireBuffFromClientTimer);

            // Apply buff effects to player stats
            ApplyBuffStats(buff, true);
            RefreshBuffControlledFlyingAbility();
        }

        private void ApplyBuffStats(ActiveBuff buff, bool apply)
        {
            var levelData = buff.LevelData;
            if (levelData == null || _player.Build == null)
                return;

            int modifier = apply ? 1 : -1;

            _player.Build.Attack += levelData.PAD * modifier;
            _player.Build.Defense += levelData.PDD * modifier;
            _player.Build.MagicAttack += levelData.MAD * modifier;
            _player.Build.MagicDefense += levelData.MDD * modifier;
            _player.Build.Accuracy += levelData.ACC * modifier;
            _player.Build.Avoidability += levelData.EVA * modifier;
            _player.Build.Speed += levelData.Speed * modifier;
            _player.Build.JumpPower += levelData.Jump * modifier;
        }

        public bool TryApplyConsumableBuff(int itemId, string itemName, string itemDescription, SkillLevelData levelData, int currentTime)
        {
            if (itemId <= 0 || levelData == null || levelData.Time <= 0 || _player?.Build == null)
            {
                return false;
            }

            bool hasTemporaryStats =
                levelData.X != 0 ||
                levelData.PAD != 0 ||
                levelData.AttackPercent != 0 ||
                levelData.MAD != 0 ||
                levelData.MagicAttackPercent != 0 ||
                levelData.PDD != 0 ||
                levelData.MDD != 0 ||
                levelData.DefensePercent != 0 ||
                levelData.MagicDefensePercent != 0 ||
                levelData.ACC != 0 ||
                levelData.EVA != 0 ||
                levelData.AccuracyPercent != 0 ||
                levelData.AvoidabilityPercent != 0 ||
                levelData.Speed != 0 ||
                levelData.SpeedPercent != 0 ||
                levelData.Jump != 0 ||
                levelData.HP != 0 ||
                levelData.MP != 0 ||
                levelData.CriticalRate != 0 ||
                levelData.EnhancedPAD != 0 ||
                levelData.EnhancedMAD != 0 ||
                levelData.EnhancedPDD != 0 ||
                levelData.EnhancedMDD != 0 ||
                levelData.EnhancedMaxHP != 0 ||
                levelData.EnhancedMaxMP != 0 ||
                levelData.IndieMaxHP != 0 ||
                levelData.IndieMaxMP != 0 ||
                levelData.MaxHPPercent != 0 ||
                levelData.MaxMPPercent != 0 ||
                levelData.DefensePercent != 0 ||
                levelData.MagicDefensePercent != 0 ||
                levelData.AccuracyPercent != 0 ||
                levelData.AvoidabilityPercent != 0 ||
                levelData.AllStat != 0 ||
                levelData.AbnormalStatusResistance != 0 ||
                levelData.ElementalResistance != 0 ||
                levelData.ExperienceRate != 0 ||
                levelData.DropRate != 0 ||
                levelData.MesoRate != 0 ||
                levelData.STR != 0 ||
                levelData.DEX != 0 ||
                levelData.INT != 0 ||
                levelData.LUK != 0;
            if (!hasTemporaryStats)
            {
                return false;
            }

            int syntheticBuffId = -Math.Abs(itemId);
            CancelActiveBuff(syntheticBuffId, currentTime, playFinish: false);

            var buff = new ActiveBuff
            {
                SkillId = syntheticBuffId,
                Level = 1,
                StartTime = currentTime,
                Duration = levelData.Time * 1000,
                SkillData = new SkillData
                {
                    SkillId = syntheticBuffId,
                    Name = string.IsNullOrWhiteSpace(itemName) ? $"Item {itemId}" : itemName,
                    Description = itemDescription ?? string.Empty
                },
                LevelData = levelData
            };

            _buffs.Add(buff);
            OnBuffApplied?.Invoke(buff);
            RegisterClientSkillTimer(syntheticBuffId, ClientTimerSourceBuffExpire, currentTime + buff.Duration, ExpireBuffFromClientTimer);
            ApplyBuffStats(buff, true);
            RefreshBuffControlledFlyingAbility();
            return true;
        }

        public bool ApplyOrRefreshExternalAreaSupportBuff(
            int areaObjectId,
            SkillData sourceSkill,
            IReadOnlyCollection<SkillData> supportSkills,
            SkillLevelData levelData,
            int currentTime,
            int durationMs)
        {
            SkillLevelData projectedLevelData = RemoteAffectedAreaSupportResolver.CreateProjectedSupportBuffLevelData(
                sourceSkill,
                levelData,
                supportSkills?.ToArray() ?? Array.Empty<SkillData>());
            if (areaObjectId <= 0 || projectedLevelData == null)
            {
                return false;
            }

            int syntheticBuffId = ResolveExternalAreaSupportBuffId(areaObjectId);
            int normalizedDurationMs = Math.Max(500, durationMs);
            SkillData syntheticSkillData = BuildExternalAreaSupportBuffSkillData(sourceSkill, supportSkills, projectedLevelData, syntheticBuffId);
            ActiveBuff existingBuff = _buffs.LastOrDefault(buff => buff.SkillId == syntheticBuffId);
            if (existingBuff != null)
            {
                bool previouslyHadPersistentAvatarEffect = existingBuff.SkillData?.HasPersistentAvatarEffect == true;
                int previousAvatarEffectSignature = ComputePersistentAvatarEffectSignature(existingBuff.SkillData);
                int nextAvatarEffectSignature = ComputePersistentAvatarEffectSignature(syntheticSkillData);
                if (!ProjectedSupportBuffStatsMatch(existingBuff.LevelData, projectedLevelData))
                {
                    ApplyBuffStats(existingBuff, false);
                    existingBuff.LevelData = projectedLevelData;
                    ApplyBuffStats(existingBuff, true);
                }

                existingBuff.StartTime = currentTime;
                existingBuff.Duration = normalizedDurationMs;
                existingBuff.Level = Math.Max(1, projectedLevelData.Level);
                existingBuff.SkillData = syntheticSkillData;
                _externalAreaSupportBuffIds.Add(syntheticBuffId);
                if (ShouldRefreshProjectedSupportAvatarEffect(
                        previouslyHadPersistentAvatarEffect,
                        syntheticSkillData.HasPersistentAvatarEffect,
                        _player.HasSkillAvatarEffect(syntheticBuffId),
                        previousAvatarEffectSignature,
                        nextAvatarEffectSignature))
                {
                    _player.ApplySkillAvatarEffect(syntheticBuffId, syntheticSkillData, currentTime);
                }
                else if (previouslyHadPersistentAvatarEffect
                         && !syntheticSkillData.HasPersistentAvatarEffect
                         && _player.HasSkillAvatarEffect(syntheticBuffId))
                {
                    _player.ClearSkillAvatarEffect(syntheticBuffId, currentTime, playFinish: false);
                }

                RefreshBuffControlledFlyingAbility();
                return true;
            }

            var buff = new ActiveBuff
            {
                SkillId = syntheticBuffId,
                Level = Math.Max(1, projectedLevelData.Level),
                StartTime = currentTime,
                Duration = normalizedDurationMs,
                SkillData = syntheticSkillData,
                LevelData = projectedLevelData
            };

            _buffs.Add(buff);
            _externalAreaSupportBuffIds.Add(syntheticBuffId);
            if (syntheticSkillData.HasPersistentAvatarEffect)
            {
                _player.ApplySkillAvatarEffect(buff.SkillId, syntheticSkillData, currentTime);
            }

            OnBuffApplied?.Invoke(buff);
            ApplyBuffStats(buff, true);
            RefreshBuffControlledFlyingAbility();
            return true;
        }

        public void SyncExternalAreaSupportBuffs(ISet<int> activeAreaObjectIds, int currentTime)
        {
            if (_externalAreaSupportBuffIds.Count == 0)
            {
                return;
            }

            int[] trackedBuffIds = _externalAreaSupportBuffIds.ToArray();
            for (int i = 0; i < trackedBuffIds.Length; i++)
            {
                int syntheticBuffId = trackedBuffIds[i];
                if (TryGetExternalAreaObjectId(syntheticBuffId, out int areaObjectId)
                    && activeAreaObjectIds != null
                    && activeAreaObjectIds.Contains(areaObjectId))
                {
                    continue;
                }

                CancelActiveBuff(syntheticBuffId, currentTime, playFinish: false);
                _externalAreaSupportBuffIds.Remove(syntheticBuffId);
            }
        }

        internal int ResolveIncomingDamageAfterActiveBuffs(int damage, int currentTime)
        {
            if (damage <= 0)
            {
                return 0;
            }

            int reducedDamage = damage;
            int damageReductionPercent = GetActiveDamageReductionPercent(currentTime);
            if (damageReductionPercent > 0)
            {
                reducedDamage = Math.Max(1, (int)MathF.Round(reducedDamage * (100 - damageReductionPercent) / 100f));
            }

            NotifyExternalAreaDamageSharing(damage, currentTime);

            int redirectedDamageToMpPercent = GetActiveDamageToMpRedirectPercent(currentTime);
            if (redirectedDamageToMpPercent > 0 && _player?.Build != null)
            {
                int mpRedirectAmount = (int)MathF.Round(reducedDamage * redirectedDamageToMpPercent / 100f);
                if (mpRedirectAmount > 0)
                {
                    int consumedMp = Math.Min(Math.Max(0, _player.Build.MP), mpRedirectAmount);
                    if (consumedMp > 0)
                    {
                        _player.Build.MP -= consumedMp;
                        reducedDamage -= consumedMp;
                    }
                }
            }

            return reducedDamage;
        }

        private void NotifyExternalAreaDamageSharing(int incomingDamage, int currentTime)
        {
            if (incomingDamage <= 0 || OnExternalAreaDamageSharingApplied == null || _externalAreaSupportBuffIds.Count == 0)
            {
                return;
            }

            int[] trackedBuffIds = _externalAreaSupportBuffIds.ToArray();
            for (int i = 0; i < trackedBuffIds.Length; i++)
            {
                int syntheticBuffId = trackedBuffIds[i];
                ActiveBuff buff = _buffs.LastOrDefault(activeBuff => activeBuff.SkillId == syntheticBuffId);
                if (buff == null
                    || buff.IsExpired(currentTime)
                    || !TryGetExternalAreaObjectId(syntheticBuffId, out int areaObjectId))
                {
                    continue;
                }

                int sharedDamage = ResolveExternalAreaDamageSharingAmount(buff, incomingDamage);
                if (sharedDamage <= 0)
                {
                    continue;
                }

                OnExternalAreaDamageSharingApplied(areaObjectId, sharedDamage, currentTime);
            }
        }

        private static int ResolveExternalAreaDamageSharingAmount(ActiveBuff buff, int incomingDamage)
        {
            if (buff?.SkillData == null || buff.LevelData == null || incomingDamage <= 0)
            {
                return 0;
            }

            if (!ContainsAny(buff.SkillData.AffectedSkillEffect, "partyDamageSharing"))
            {
                return 0;
            }

            int distributedShareRate = buff.LevelData.X > 0 ? buff.LevelData.X : buff.LevelData.DamageReductionRate;
            if (distributedShareRate <= 0)
            {
                return 0;
            }

            return Math.Max(1, (int)MathF.Round(incomingDamage * Math.Clamp(distributedShareRate, 0, 100) / 100f));
        }

        private void ApplyShadowPartner(ActiveBuff buff, int currentTime)
        {
            if (buff?.SkillData == null)
            {
                return;
            }

            if (buff.SkillData.HasShadowPartnerActionAnimations)
            {
                _player.ApplyShadowPartner(buff.SkillId, buff.SkillData, currentTime);
            }

            if (buff.SkillData.UsesMirrorHelperActor)
            {
                _player.ApplyMirrorImage(buff.SkillId, currentTime);
            }
        }

        public bool CancelActiveBuff(int skillId)
        {
            return RequestClientSkillCancel(skillId, Environment.TickCount, enforceFieldCancelRestrictions: true);
        }

        public void CancelAllActiveBuffs(int currentTime)
        {
            for (int i = _buffs.Count - 1; i >= 0; i--)
            {
                RemoveBuffAt(i, currentTime, playFinish: false);
            }
        }

        private bool CancelActiveBuff(int skillId, int currentTime, bool playFinish)
        {
            for (int i = _buffs.Count - 1; i >= 0; i--)
            {
                var buff = _buffs[i];
                if (buff.SkillId != skillId)
                {
                    continue;
                }

                RemoveBuffAt(i, currentTime, playFinish);
                return true;
            }

            return false;
        }

        private void RemoveBuffAt(int index, int currentTime, bool playFinish)
        {
            var buff = _buffs[index];
            _externalAreaSupportBuffIds.Remove(buff.SkillId);
            _affectedSkillSupportBuffIds.Remove(buff.SkillId);
            ApplyBuffStats(buff, false);
            if (buff.SkillData?.HasShadowPartnerActionAnimations == true)
            {
                _player.ClearShadowPartner(buff.SkillId);
            }

            if (buff.SkillData?.UsesMirrorHelperActor == true)
            {
                _player.ClearMirrorImage(buff.SkillId);
            }

            _player.ClearSkillAvatarTransform(buff.SkillId);
            _player.ClearSkillAvatarEffect(buff.SkillId, currentTime, playFinish);
            UpdateClientOwnedAvatarEffectSuppression(buff.SkillData, suppress: false);
            ClearEnergyChargeRuntime(buff.SkillId, currentTime);
            ClearSkillMount(buff.SkillId);
            if (_swallowState?.SkillId == buff.SkillId)
            {
                ClearSwallowState();
            }

            foreach (int skillId in _activeAffectedSkillPassives
                         .Where(entry => GetSkillData(entry.Key)?.LinksAffectedSkill(buff.SkillId) == true)
                         .Select(entry => entry.Key)
                         .ToList())
            {
                _activeAffectedSkillPassives.Remove(skillId);
            }

            _buffs.RemoveAt(index);
            CancelClientSkillTimers(buff.SkillId, ClientTimerSourceBuffExpire);
            RefreshBuffControlledFlyingAbility();
            OnBuffExpired?.Invoke(buff);
        }

        private void ExpireBuffFromClientTimer(int currentTime)
        {
            bool removedBuff = false;
            for (int i = _buffs.Count - 1; i >= 0; i--)
            {
                if (_buffs[i].IsExpired(currentTime))
                {
                    RemoveBuffAt(i, currentTime, playFinish: true);
                    removedBuff = true;
                }
            }

            if (removedBuff)
            {
                RefreshBuffControlledFlyingAbility();
            }
        }

        private void UpdateBuffs(int currentTime)
        {
            UpdateClientSkillTimers(currentTime);
        }

        private void RefreshBuffControlledFlyingAbility()
        {
            if (_player?.Physics == null)
                return;

            bool hasFlyingBuff = _buffs.Any(buff => SkillGrantsFlyingAbility(buff.SkillData));
            if (hasFlyingBuff)
            {
                _player.Physics.HasFlyingAbility = true;
            }
            else if (_buffControlledFlyingAbility)
            {
                _player.Physics.HasFlyingAbility = false;
            }

            _buffControlledFlyingAbility = hasFlyingBuff;
        }

        private static bool SkillGrantsFlyingAbility(SkillData skill)
        {
            return ActionGrantsFlyingAbility(skill?.ActionName)
                || ActionGrantsFlyingAbility(skill?.PrepareActionName)
                || ActionGrantsFlyingAbility(skill?.KeydownActionName);
        }

        private void UpdateMapFlyingRepeatAvatarEffect(int currentTime)
        {
            if (_player == null)
            {
                _activeMapFlyingRepeatAvatarEffectSkillId = 0;
                return;
            }

            int skillId = ResolveMapFlyingRepeatAvatarEffectSkillId();
            if (skillId <= 0 || !ShouldShowMapFlyingRepeatAvatarEffect())
            {
                ClearMapFlyingRepeatAvatarEffect(currentTime);
                return;
            }

            if (_activeMapFlyingRepeatAvatarEffectSkillId == skillId
                && _player.HasSkillAvatarEffect(skillId))
            {
                return;
            }

            SkillData skill = GetSkillData(skillId);
            SkillData avatarEffectSkill = CreateMapFlyingRepeatAvatarEffectSkill(skill);
            if (avatarEffectSkill == null)
            {
                ClearMapFlyingRepeatAvatarEffect(currentTime);
                return;
            }

            if (_activeMapFlyingRepeatAvatarEffectSkillId > 0
                && _activeMapFlyingRepeatAvatarEffectSkillId != skillId)
            {
                _player.ClearSkillAvatarEffect(_activeMapFlyingRepeatAvatarEffectSkillId, currentTime, playFinish: false);
            }

            _player.ApplySkillAvatarEffect(skillId, avatarEffectSkill, currentTime);
            _activeMapFlyingRepeatAvatarEffectSkillId = skillId;
        }

        private int ResolveMapFlyingRepeatAvatarEffectSkillId()
        {
            ActiveBuff activeBuff = _buffs.LastOrDefault(buff => IsMapFlyingRepeatAvatarEffectSkill(buff?.SkillData));
            if (activeBuff?.SkillId > 0)
            {
                return activeBuff.SkillId;
            }

            SkillData skill = _availableSkills.LastOrDefault(IsMapFlyingRepeatAvatarEffectSkill);
            return skill?.SkillId ?? 0;
        }

        private bool ShouldShowMapFlyingRepeatAvatarEffect()
        {
            return _player?.Physics?.IsUserFlying() == true
                   && !_player.Physics.IsOnFoothold()
                   && !_player.Physics.IsOnLadderOrRope
                   && _player.State == PlayerState.Flying;
        }

        private static bool IsMapFlyingRepeatAvatarEffectSkill(SkillData skill)
        {
            return skill?.SkillId > 0
                   && skill.Invisible
                   && skill.SkillId % 10000 == 1026
                   && !skill.HasPersistentAvatarEffect
                   && (skill.RepeatEffect != null || skill.RepeatSecondaryEffect != null);
        }

        private static SkillData CreateMapFlyingRepeatAvatarEffectSkill(SkillData skill)
        {
            if (!IsMapFlyingRepeatAvatarEffectSkill(skill))
            {
                return null;
            }

            SkillAnimation overlayAnimation = null;
            SkillAnimation overlaySecondaryAnimation = null;
            SkillAnimation underFaceAnimation = null;
            SkillAnimation underFaceSecondaryAnimation = null;

            AssignAvatarBuffEffectPlane(
                CreateLoopingAvatarEffect(skill.RepeatEffect),
                ref overlayAnimation,
                ref overlaySecondaryAnimation,
                ref underFaceAnimation,
                ref underFaceSecondaryAnimation);
            AssignAvatarBuffEffectPlane(
                CreateLoopingAvatarEffect(skill.RepeatSecondaryEffect),
                ref overlayAnimation,
                ref overlaySecondaryAnimation,
                ref underFaceAnimation,
                ref underFaceSecondaryAnimation);

            if (overlayAnimation == null
                && overlaySecondaryAnimation == null
                && underFaceAnimation == null
                && underFaceSecondaryAnimation == null)
            {
                return null;
            }

            return new SkillData
            {
                SkillId = skill.SkillId,
                AvatarOverlayEffect = overlayAnimation,
                AvatarOverlaySecondaryEffect = overlaySecondaryAnimation,
                AvatarUnderFaceEffect = underFaceAnimation,
                AvatarUnderFaceSecondaryEffect = underFaceSecondaryAnimation,
                HideAvatarEffectOnRotateAction = true
            };
        }

        private void ClearMapFlyingRepeatAvatarEffect(int currentTime)
        {
            if (_player != null && _activeMapFlyingRepeatAvatarEffectSkillId > 0)
            {
                _player.ClearSkillAvatarEffect(_activeMapFlyingRepeatAvatarEffectSkillId, currentTime, playFinish: false);
            }

            _activeMapFlyingRepeatAvatarEffectSkillId = 0;
        }

        private bool HasActiveBuff(int skillId)
        {
            return skillId > 0 && _buffs.Any(buff => buff.SkillId == skillId);
        }

        private void CancelReplacedBuffsOnApply(int skillId, int currentTime)
        {
            for (int i = _buffs.Count - 1; i >= 0; i--)
            {
                ActiveBuff activeBuff = _buffs[i];
                if (activeBuff == null
                    || !ShouldReplaceExistingBuffOnApply(skillId, activeBuff.SkillId))
                {
                    continue;
                }

                RemoveBuffAt(i, currentTime, playFinish: true);
            }
        }

        private static bool ShouldReplaceExistingBuffOnApply(int castingSkillId, int activeSkillId)
        {
            if (castingSkillId <= 0 || activeSkillId <= 0)
            {
                return false;
            }

            if (castingSkillId == activeSkillId)
            {
                return true;
            }

            return !(IsBattleMageAuraSkill(castingSkillId) && IsBattleMageAuraSkill(activeSkillId));
        }

        private static bool IsBattleMageAuraSkill(SkillData skill)
        {
            return IsBattleMageAuraSkill(skill?.SkillId ?? 0);
        }

        private static bool IsBattleMageAuraSkill(int skillId)
        {
            return skillId == BATTLE_MAGE_DARK_AURA_SKILL_ID
                   || skillId == BATTLE_MAGE_BLUE_AURA_SKILL_ID
                   || skillId == BATTLE_MAGE_YELLOW_AURA_SKILL_ID;
        }

        private static SkillData ResolveBuffAvatarEffectSkill(SkillData skill)
        {
            if (!UsesEffectToAvatarLayerBuffFallback(skill))
            {
                return skill;
            }

            if (UsesBlessingArmorAvatarEffectFallback(skill))
            {
                return new SkillData
                {
                    SkillId = skill.SkillId,
                    AvatarUnderFaceEffect = CreateLoopingAvatarEffect(skill.RepeatEffect)
                                            ?? CreateLoopingAvatarEffect(skill.RepeatSecondaryEffect),
                    AvatarUnderFaceSecondaryEffect = skill.RepeatEffect != null && skill.RepeatSecondaryEffect != null
                        ? CreateLoopingAvatarEffect(skill.RepeatSecondaryEffect)
                        : null
                };
            }

            bool usesSwallowFallback = UsesSwallowBuffAvatarEffectFallback(skill);
            SkillAnimation overlayAnimation = null;
            SkillAnimation overlaySecondaryAnimation = null;
            SkillAnimation underFaceAnimation = null;
            SkillAnimation underFaceSecondaryAnimation = null;

            if (usesSwallowFallback)
            {
                underFaceAnimation = CreateLoopingAvatarEffect(skill.Effect)
                                     ?? CreateLoopingAvatarEffect(skill.EffectSecondary);
            }
            else
            {
                AssignAvatarBuffEffectPlane(
                    CreateLoopingAvatarEffect(skill.Effect),
                    ref overlayAnimation,
                    ref overlaySecondaryAnimation,
                    ref underFaceAnimation,
                    ref underFaceSecondaryAnimation);
                AssignAvatarBuffEffectPlane(
                    CreateLoopingAvatarEffect(skill.EffectSecondary),
                    ref overlayAnimation,
                    ref overlaySecondaryAnimation,
                    ref underFaceAnimation,
                    ref underFaceSecondaryAnimation);
            }

            return new SkillData
            {
                SkillId = skill.SkillId,
                AvatarOverlayEffect = overlayAnimation,
                AvatarOverlaySecondaryEffect = overlaySecondaryAnimation,
                AvatarUnderFaceEffect = underFaceAnimation,
                AvatarUnderFaceSecondaryEffect = underFaceSecondaryAnimation,
                HideAvatarEffectOnLadderOrRope = usesSwallowFallback,
                HideAvatarEffectOnRotateAction = skill.HideAvatarEffectOnRotateAction
                                                 || UsesFlightBuffAvatarEffectFallback(skill)
            };
        }

        private void BeginEnergyChargeRuntime(ActiveBuff buff, int currentTime)
        {
            if (!UsesDeferredEnergyChargeAvatarEffect(buff?.SkillData))
            {
                return;
            }

            _activeEnergyChargeRuntime = new EnergyChargeRuntimeState
            {
                SkillId = buff.SkillId,
                Level = buff.Level,
                CurrentCharge = 0,
                MaxCharge = ResolveEnergyChargeThreshold(buff.SkillData, buff.LevelData, buff.Level),
                IsFullyCharged = false
            };

            _player.ClearSkillAvatarEffect(buff.SkillId, currentTime, playFinish: false);
        }

        private void ClearEnergyChargeRuntime(int skillId, int currentTime)
        {
            if (_activeEnergyChargeRuntime == null || _activeEnergyChargeRuntime.SkillId != skillId)
            {
                return;
            }

            _player.ClearSkillAvatarEffect(skillId, currentTime, playFinish: false);
            _activeEnergyChargeRuntime = null;
        }

        private static int ResolveEnergyChargeThreshold(SkillData skill, SkillLevelData levelData, int level)
        {
            int threshold = skill?.ResolveEnergyChargeThreshold(level > 0 ? level : levelData?.Level ?? 1)
                ?? (levelData?.X > 0 ? levelData.X : 100);
            return Math.Max(1, threshold);
        }

        private void RegisterEnergyChargeHit(int currentTime, int hitCount = 1)
        {
            if (_activeEnergyChargeRuntime == null
                || _activeEnergyChargeRuntime.IsFullyCharged
                || hitCount <= 0)
            {
                return;
            }

            ActiveBuff activeBuff = _buffs.LastOrDefault(buff => buff.SkillId == _activeEnergyChargeRuntime.SkillId);
            if (activeBuff?.SkillData == null || !UsesDeferredEnergyChargeAvatarEffect(activeBuff.SkillData))
            {
                _activeEnergyChargeRuntime = null;
                return;
            }

            int chargeDelta = Math.Max(1, hitCount) * EnergyChargePointsPerSuccessfulHit;
            _activeEnergyChargeRuntime.CurrentCharge = Math.Min(
                _activeEnergyChargeRuntime.MaxCharge,
                _activeEnergyChargeRuntime.CurrentCharge + chargeDelta);

            if (_activeEnergyChargeRuntime.CurrentCharge < _activeEnergyChargeRuntime.MaxCharge)
            {
                return;
            }

            _activeEnergyChargeRuntime.IsFullyCharged = true;
            _player.ApplySkillAvatarEffect(activeBuff.SkillId, activeBuff.SkillData, currentTime);
        }

        private static bool ShouldApplyBuffAvatarEffectOnActivation(SkillData skill)
        {
            return skill != null
                   && !UsesDeferredEnergyChargeAvatarEffect(skill);
        }

        private static bool UsesEffectToAvatarLayerBuffFallback(SkillData skill)
        {
            return UsesBlessingArmorAvatarEffectFallback(skill)
                   || UsesFlightBuffAvatarEffectFallback(skill)
                   || UsesInvisibleBuffAvatarEffectFallback(skill)
                   || UsesReflectBuffAvatarEffectFallback(skill)
                   || UsesSwallowBuffAvatarEffectFallback(skill)
                   || UsesTimedSingleEffectBuffAvatarEffectFallback(skill)
                   || UsesTimedEffectPairBuffAvatarEffectFallback(skill);
        }

        private static bool UsesDeferredEnergyChargeAvatarEffect(SkillData skill)
        {
            // Energy Charge promotes its full-charge avatar layer from a separate client-owned state,
            // not immediately when the buff is first applied.
            return skill?.IsBuff == true
                   && skill.UsesEnergyChargeRuntime
                   && !string.IsNullOrWhiteSpace(skill.FullChargeEffectName)
                   && skill.HasPersistentAvatarEffect;
        }

        private static bool UsesBlessingArmorAvatarEffectFallback(SkillData skill)
        {
            return skill?.IsBuff == true
                   && skill.HasBlessingArmorMetadata
                   && (skill.RepeatEffect != null || skill.RepeatSecondaryEffect != null)
                   && !skill.HasPersistentAvatarEffect;
        }

        private static bool UsesInvisibleBuffAvatarEffectFallback(SkillData skill)
        {
            return skill?.IsBuff == true
                   && skill.Invisible
                   && HasClientOwnedAvatarEffectBranches(skill)
                   && !HasAffectedAvatarEffectBranches(skill)
                   && !skill.HasPersistentAvatarEffect
                   && !IsSwallowSkill(skill);
        }

        private static bool UsesSwallowBuffAvatarEffectFallback(SkillData skill)
        {
            return skill?.IsBuff == true
                   && skill.Invisible
                   && HasClientOwnedAvatarEffectBranches(skill)
                   && !skill.HasPersistentAvatarEffect
                   && IsSwallowSkill(skill);
        }

        private static bool UsesReflectBuffAvatarEffectFallback(SkillData skill)
        {
            return skill?.IsBuff == true
                   && skill.ReflectsIncomingDamage
                   && HasClientOwnedAvatarEffectBranches(skill)
                   && !HasAffectedAvatarEffectBranches(skill)
                   && !skill.HasPersistentAvatarEffect;
        }

        private static bool UsesTimedEffectPairBuffAvatarEffectFallback(SkillData skill)
        {
            return skill?.IsBuff == true
                   && skill.Effect != null
                   && skill.EffectSecondary != null
                   && !HasAffectedAvatarEffectBranches(skill)
                   && !skill.HasPersistentAvatarEffect
                   && !skill.IsMovement
                   && !skill.IsPrepareSkill
                   && !skill.IsSummon;
        }

        private static bool UsesTimedSingleEffectBuffAvatarEffectFallback(SkillData skill)
        {
            return skill?.IsBuff == true
                   && HasClientOwnedAvatarEffectBranches(skill)
                   && (skill.Effect == null || skill.EffectSecondary == null)
                   && !HasAffectedAvatarEffectBranches(skill)
                   && !skill.HasPersistentAvatarEffect
                   && !skill.IsMovement
                   && !skill.IsPrepareSkill
                   && !skill.IsSummon;
        }

        private static bool HasAffectedAvatarEffectBranches(SkillData skill)
        {
            return skill?.AffectedEffect != null || skill?.AffectedSecondaryEffect != null;
        }

        private static bool HasClientOwnedAvatarEffectBranches(SkillData skill)
        {
            return skill?.Effect != null || skill?.EffectSecondary != null;
        }

        private static SkillAnimation CreateLoopingAvatarEffect(SkillAnimation animation)
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

        private static void AssignAvatarBuffEffectPlane(
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

            bool prefersUnderFace = ClientOwnedAvatarEffectParity.PrefersUnderFaceAvatarEffectPlane(animation);
            if (prefersUnderFace)
            {
                underFaceAnimation ??= animation;
                underFaceSecondaryAnimation ??= animation == underFaceAnimation ? null : animation;
                return;
            }

            overlayAnimation ??= animation;
            overlaySecondaryAnimation ??= animation == overlayAnimation ? null : animation;
        }

        private void UpdateClientOwnedAvatarEffectSuppression(SkillData skill, bool suppress)
        {
            if (_player == null || !UsesClientOwnedAvatarEffectSuppression(skill))
            {
                return;
            }

            _player.SetSkillAvatarEffectRenderSuppressed(skill.SkillId, suppress);
        }

        private static bool UsesClientOwnedAvatarEffectSuppression(SkillData skill)
        {
            return skill?.HasMorphMetadata == true;
        }

        private static bool ActionGrantsFlyingAbility(string actionName)
        {
            return !string.IsNullOrWhiteSpace(actionName)
                && (actionName.IndexOf("fly", StringComparison.OrdinalIgnoreCase) >= 0
                    || actionName.IndexOf("flying", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public IReadOnlyList<ActiveBuff> ActiveBuffs => _buffs;

        internal static StatusBarBuffEntry ResolveStatusBarBuffEntryForParity(
            SkillData skillData,
            SkillLevelData levelData,
            int skillId = 1,
            int startTime = 0,
            int durationMs = 30000,
            int currentTime = 0)
        {
            var buff = new ActiveBuff
            {
                SkillId = skillId,
                SkillData = skillData,
                LevelData = levelData,
                StartTime = startTime,
                Duration = durationMs
            };

            IReadOnlyList<BuffTemporaryStatPresentation> temporaryStats = GetBuffTemporaryStatPresentation(buff);
            ISet<string> directBuffIconEligibleLabels = BuildDirectBuffIconEligibleLabelSet(levelData);
            BuffTemporaryStatPresentation authoredExplicitFamilyOwnerTemporaryStat = ResolveAuthoredExplicitFamilyOwnerTemporaryStat(
                levelData,
                temporaryStats);
            BuffTemporaryStatPresentation familyOwnerTemporaryStat = authoredExplicitFamilyOwnerTemporaryStat
                ?? ResolveFamilyOwnerTemporaryStat(temporaryStats);
            BuffTemporaryStatPresentation trayLaneTemporaryStat = ResolvePrimaryTemporaryStat(temporaryStats);
            BuffTemporaryStatPresentation iconOwnerTemporaryStat = ResolveIconOwnerTemporaryStat(
                temporaryStats,
                trayLaneTemporaryStat,
                familyOwnerTemporaryStat,
                directBuffIconEligibleLabels);
            bool preferExplicitFamilyTrayOwner = iconOwnerTemporaryStat == null
                && authoredExplicitFamilyOwnerTemporaryStat != null;
            if (preferExplicitFamilyTrayOwner)
            {
                trayLaneTemporaryStat = authoredExplicitFamilyOwnerTemporaryStat;
            }

            BuffIconCatalogEntry supplementalBuffIconEntry = ResolveSupplementalBuffIconEntry(buff, trayLaneTemporaryStat);
            BuffTemporaryStatPresentation displayOwnerTemporaryStat = ResolveDisplayOwnerTemporaryStat(
                trayLaneTemporaryStat,
                iconOwnerTemporaryStat,
                familyOwnerTemporaryStat,
                directBuffIconEligibleLabels);
            string authoredFamilyDisplayName = ResolveAuthoredFamilyDisplayName(
                levelData,
                temporaryStats,
                displayOwnerTemporaryStat);
            IReadOnlyList<string> temporaryStatDisplayNames = ResolveAuthoredTemporaryStatDisplayNames(levelData, temporaryStats);
            bool preferTemporaryStatIcon = iconOwnerTemporaryStat != null
                || supplementalBuffIconEntry != null;

            return new StatusBarBuffEntry
            {
                SkillId = skillId,
                SkillName = skillData?.Name ?? skillId.ToString(),
                Description = skillData?.Description ?? string.Empty,
                IconKey = ResolveBuffIconKey(iconOwnerTemporaryStat, supplementalBuffIconEntry),
                IconTexture = preferTemporaryStatIcon ? null : skillData?.IconTexture,
                StartTime = startTime,
                DurationMs = durationMs,
                RemainingMs = Math.Max(0, durationMs - (currentTime - startTime)),
                SortOrder = ResolveStatusBarBuffSortOrder(
                    temporaryStats,
                    trayLaneTemporaryStat,
                    supplementalBuffIconEntry,
                    preferExplicitFamilyTrayOwner),
                FamilyDisplayName = ResolveBuffFamilyDisplayName(authoredFamilyDisplayName, displayOwnerTemporaryStat, supplementalBuffIconEntry),
                TemporaryStatLabels = temporaryStats.Select(stat => stat.Label).ToArray(),
                TemporaryStatDisplayNames = temporaryStatDisplayNames
            };
        }

        public IReadOnlyList<StatusBarBuffEntry> GetStatusBarBuffEntries(int currentTime)
        {
            if (_buffs.Count == 0)
            {
                return Array.Empty<StatusBarBuffEntry>();
            }

            var entries = new List<StatusBarBuffEntry>(_buffs.Count);
            foreach (var buff in _buffs)
            {
                IReadOnlyList<BuffTemporaryStatPresentation> temporaryStats = GetBuffTemporaryStatPresentation(buff);
                ISet<string> directBuffIconEligibleLabels = BuildDirectBuffIconEligibleLabelSet(buff?.LevelData);
                BuffTemporaryStatPresentation authoredExplicitFamilyOwnerTemporaryStat = ResolveAuthoredExplicitFamilyOwnerTemporaryStat(
                    buff?.LevelData,
                    temporaryStats);
                BuffTemporaryStatPresentation familyOwnerTemporaryStat = authoredExplicitFamilyOwnerTemporaryStat
                    ?? ResolveFamilyOwnerTemporaryStat(temporaryStats);
                BuffTemporaryStatPresentation trayLaneTemporaryStat = ResolvePrimaryTemporaryStat(temporaryStats);
                BuffTemporaryStatPresentation iconOwnerTemporaryStat = ResolveIconOwnerTemporaryStat(
                    temporaryStats,
                    trayLaneTemporaryStat,
                    familyOwnerTemporaryStat,
                    directBuffIconEligibleLabels);
                bool preferExplicitFamilyTrayOwner = iconOwnerTemporaryStat == null
                    && authoredExplicitFamilyOwnerTemporaryStat != null;
                if (preferExplicitFamilyTrayOwner)
                {
                    trayLaneTemporaryStat = authoredExplicitFamilyOwnerTemporaryStat;
                }

                BuffIconCatalogEntry supplementalBuffIconEntry = ResolveSupplementalBuffIconEntry(buff, trayLaneTemporaryStat);
                BuffTemporaryStatPresentation displayOwnerTemporaryStat = ResolveDisplayOwnerTemporaryStat(
                    trayLaneTemporaryStat,
                    iconOwnerTemporaryStat,
                    familyOwnerTemporaryStat,
                    directBuffIconEligibleLabels);
                string authoredFamilyDisplayName = ResolveAuthoredFamilyDisplayName(
                    buff?.LevelData,
                    temporaryStats,
                    displayOwnerTemporaryStat);
                IReadOnlyList<string> temporaryStatDisplayNames = ResolveAuthoredTemporaryStatDisplayNames(buff?.LevelData, temporaryStats);
                bool preferTemporaryStatIcon = iconOwnerTemporaryStat != null
                    || supplementalBuffIconEntry != null;
                entries.Add(new StatusBarBuffEntry
                {
                    SkillId = buff.SkillId,
                    SkillName = buff.SkillData?.Name ?? buff.SkillId.ToString(),
                    Description = buff.SkillData?.Description ?? string.Empty,
                    IconKey = ResolveBuffIconKey(iconOwnerTemporaryStat, supplementalBuffIconEntry),
                    IconTexture = preferTemporaryStatIcon ? null : buff.SkillData?.IconTexture,
                    StartTime = buff.StartTime,
                    DurationMs = buff.Duration,
                    RemainingMs = buff.GetRemainingTime(currentTime),
                    SortOrder = ResolveStatusBarBuffSortOrder(
                        temporaryStats,
                        trayLaneTemporaryStat,
                        supplementalBuffIconEntry,
                        preferExplicitFamilyTrayOwner),
                    FamilyDisplayName = ResolveBuffFamilyDisplayName(authoredFamilyDisplayName, displayOwnerTemporaryStat, supplementalBuffIconEntry),
                    TemporaryStatLabels = temporaryStats.Select(stat => stat.Label).ToArray(),
                    TemporaryStatDisplayNames = temporaryStatDisplayNames
                });
            }

            return entries
                .OrderBy(entry => entry.SortOrder)
                .ThenByDescending(entry => entry.StartTime)
                .ThenBy(entry => entry.SkillId)
                .ToArray();
        }

        /// <summary>
        /// Get total buff stat bonus
        /// </summary>
        public int GetBuffStat(BuffStatType stat)
        {
            int total = 0;
            foreach (var buff in _buffs)
            {
                var data = buff.LevelData;
                if (data == null) continue;

                total += stat switch
                {
                    BuffStatType.Strength => data.STR + data.AllStat,
                    BuffStatType.Dexterity => data.DEX + data.AllStat,
                    BuffStatType.Intelligence => data.INT + data.AllStat,
                    BuffStatType.Luck => data.LUK + data.AllStat,
                    BuffStatType.Attack => data.PAD + ResolveEnhancedStatBonus(buff.SkillData, data.EnhancedPAD),
                    BuffStatType.AttackPercent => data.AttackPercent,
                    BuffStatType.MagicAttack => data.MAD + ResolveEnhancedStatBonus(buff.SkillData, data.EnhancedMAD),
                    BuffStatType.MagicAttackPercent => data.MagicAttackPercent,
                    BuffStatType.Defense => data.PDD + ResolveEnhancedStatBonus(buff.SkillData, data.EnhancedPDD),
                    BuffStatType.MagicDefense => data.MDD + ResolveEnhancedStatBonus(buff.SkillData, data.EnhancedMDD),
                    BuffStatType.DefensePercent => data.DefensePercent,
                    BuffStatType.MagicDefensePercent => data.MagicDefensePercent,
                    BuffStatType.Accuracy => data.ACC,
                    BuffStatType.AccuracyPercent => data.AccuracyPercent,
                    BuffStatType.Avoidability => data.EVA,
                    BuffStatType.AvoidabilityPercent => data.AvoidabilityPercent,
                    BuffStatType.Speed => data.Speed,
                    BuffStatType.SpeedPercent => data.SpeedPercent,
                    BuffStatType.Jump => data.Jump,
                    BuffStatType.MaxHP => ResolveEnhancedStatBonus(buff.SkillData, data.EnhancedMaxHP) + data.IndieMaxHP,
                    BuffStatType.MaxMP => ResolveEnhancedStatBonus(buff.SkillData, data.EnhancedMaxMP) + data.IndieMaxMP,
                    BuffStatType.MaxHPPercent => data.MaxHPPercent,
                    BuffStatType.MaxMPPercent => data.MaxMPPercent,
                    BuffStatType.CriticalRate => data.CriticalRate,
                    BuffStatType.Booster => ResolveBoosterStatBonus(buff.SkillData, data),
                    _ => 0
                };

                total += ResolveDescriptionBackedBuffStatBonus(_player?.Build, buff?.SkillData, data, stat);
            }
            return total;
        }

        internal static int ResolveDescriptionBackedBuffStatBonus(
            CharacterBuild build,
            SkillData skill,
            SkillLevelData levelData,
            BuffStatType stat)
        {
            if (build == null || levelData == null)
            {
                return 0;
            }

            if (IsMapleWarriorLikeBasicStatPercentBuff(skill, levelData))
            {
                int percent = Math.Max(0, levelData.X);
                return stat switch
                {
                    BuffStatType.Strength => build.STR * percent / 100,
                    BuffStatType.Dexterity => build.DEX * percent / 100,
                    BuffStatType.Intelligence => build.INT * percent / 100,
                    BuffStatType.Luck => build.LUK * percent / 100,
                    _ => 0
                };
            }

            return 0;
        }

        private static bool IsMapleWarriorLikeBasicStatPercentBuff(SkillData skill, SkillLevelData levelData)
        {
            if (skill == null || levelData == null || levelData.X <= 0)
            {
                return false;
            }

            if (HasAllPrimaryStatBonus(levelData) || levelData.AllStat != 0)
            {
                return false;
            }

            if (string.Equals(skill.Name?.Trim(), "Maple Warrior", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string descriptionSurface = SkillDataTextSurface.GetDescriptionSurface(skill);
            return descriptionSurface.IndexOf("all stats", StringComparison.OrdinalIgnoreCase) >= 0
                   && descriptionSurface.IndexOf("#x%", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int ResolveBoosterStatBonus(SkillData skill, SkillLevelData levelData)
        {
            if (levelData == null || levelData.X == 0 || skill == null)
            {
                return 0;
            }

            return ContainsAny(
                    $"{skill.Name} {SkillDataTextSurface.GetDescriptionSurface(skill)}",
                    "booster",
                    "attack speed",
                    "weapon speed")
                ? levelData.X
                : 0;
        }


        /// <summary>
        /// Check if buff type is active
        /// </summary>
        public bool HasBuff(int skillId)
        {
            foreach (var buff in _buffs)
            {
                if (buff.SkillId == skillId)
                    return true;
            }
            return false;
        }

        private static IReadOnlyList<BuffTemporaryStatPresentation> GetBuffTemporaryStatPresentation(ActiveBuff buff)
        {
            var levelData = buff?.LevelData;
            SkillData skill = buff?.SkillData;
            if (levelData == null && skill == null)
            {
                return Array.Empty<BuffTemporaryStatPresentation>();
            }

            var temporaryStats = new List<BuffTemporaryStatPresentation>(8);

            void Track(int value, string label)
            {
                if (value <= 0)
                {
                    return;
                }

                if (ResolvedTemporaryStatPresentationCatalog.TryGetValue(label, out BuffTemporaryStatPresentation presentation))
                {
                    temporaryStats.Add(presentation);
                }
            }

            if (levelData != null)
            {
                Track(levelData.PAD, "PAD");
                Track(levelData.PDD, "PDD");
                Track(levelData.MAD, "MAD");
                Track(levelData.MDD, "MDD");
                Track(levelData.ACC, "ACC");
                Track(levelData.EVA, "EVA");
                Track(levelData.Speed, "Speed");
                Track(levelData.Jump, "Jump");
                TrackIfMissing(temporaryStats, levelData.AttackPercent > 0, "PAD");
                TrackIfMissing(temporaryStats, levelData.MagicAttackPercent > 0, "MAD");
                TrackIfMissing(temporaryStats, levelData.DefensePercent > 0, "PDD");
                TrackIfMissing(temporaryStats, levelData.MagicDefensePercent > 0, "MDD");
                TrackIfMissing(temporaryStats, levelData.AccuracyPercent > 0, "ACC");
                TrackIfMissing(temporaryStats, levelData.AvoidabilityPercent > 0, "EVA");
                TrackIfMissing(temporaryStats, levelData.SpeedPercent > 0, "Speed");
                TrackIfMissing(temporaryStats, ShouldTrackIndividualPrimaryStats(levelData) && levelData.STR > 0, StrengthBuffLabel);
                TrackIfMissing(temporaryStats, ShouldTrackIndividualPrimaryStats(levelData) && levelData.DEX > 0, DexterityBuffLabel);
                TrackIfMissing(temporaryStats, ShouldTrackIndividualPrimaryStats(levelData) && levelData.INT > 0, IntelligenceBuffLabel);
                TrackIfMissing(temporaryStats, ShouldTrackIndividualPrimaryStats(levelData) && levelData.LUK > 0, LuckBuffLabel);
                Track(levelData.EnhancedPAD, "PAD");
                Track(levelData.EnhancedMAD, "MAD");
                Track(levelData.EnhancedPDD, "PDD");
                Track(levelData.EnhancedMDD, "MDD");
                Track(levelData.EnhancedMaxHP, MaxHpBuffLabel);
                Track(levelData.EnhancedMaxMP, MaxMpBuffLabel);
                Track(levelData.IndieMaxHP, MaxHpBuffLabel);
                Track(levelData.IndieMaxMP, MaxMpBuffLabel);
                Track(levelData.MaxHPPercent, MaxHpBuffLabel);
                Track(levelData.MaxMPPercent, MaxMpBuffLabel);
                Track(levelData.CriticalRate, CriticalRateBuffLabel);
                TrackIfMissing(
                    temporaryStats,
                    levelData.CriticalDamageMin > 0 || levelData.CriticalDamageMax > 0,
                    CriticalDamageBuffLabel);
                TrackIfMissing(
                    temporaryStats,
                    levelData.DamageReductionRate > 0 && HasAuthoredTemporaryStatProperty(levelData, "damR", "indieDamR"),
                    DamRBuffLabel);
                TrackIfMissing(temporaryStats, levelData.DamageReductionRate > 0, DamageReductionBuffLabel);
                TrackIfMissing(temporaryStats, levelData.AllStat > 0 || HasAllPrimaryStatBonus(levelData), AllStatsBuffLabel);
                TrackIfMissing(
                    temporaryStats,
                    levelData.AbnormalStatusResistance > 0 || levelData.ElementalResistance > 0,
                    DebuffResistanceBuffLabel);
                TrackIfMissing(temporaryStats, levelData.ExperienceRate > 0, ExperienceBuffLabel);
                TrackIfMissing(temporaryStats, levelData.DropRate > 0, DropRateBuffLabel);
                TrackIfMissing(temporaryStats, levelData.MesoRate > 0, MesoRateBuffLabel);
                TrackIfMissing(temporaryStats, levelData.BossDamageRate > 0, BossDamageBuffLabel);
                TrackIfMissing(temporaryStats, levelData.IgnoreDefenseRate > 0, IgnoreDefenseBuffLabel);
                TrackIfMissing(temporaryStats, levelData.HP > 0 || levelData.MP > 0, RecoveryBuffLabel);
            }

            string combinedText = BuildCombinedTemporaryStatText(skill);

            TrackDescriptionTemporaryStats(temporaryStats, combinedText);
            TrackIdentityTemporaryStats(temporaryStats, skill, levelData, combinedText);

            if (HasExplicitDamageReductionTemporaryStatOwner(levelData))
            {
                RemoveTemporaryStatLabel(temporaryStats, DamageReductionBuffLabel);
            }

            TrackIfMissing(
                temporaryStats,
                skill?.IsRapidAttack == true
                    || ContainsAny(combinedText, "booster", "attack speed", "weapon speed"),
                BoosterBuffLabel);
            TrackIfMissing(
                temporaryStats,
                ContainsAny(combinedText, "stance", "knockback resist", "knock-back resist", "knockback immunity"),
                StanceBuffLabel);
            TrackIfMissing(temporaryStats,
                skill?.HasInvincibleMetadata == true
                || string.Equals(skill?.ZoneType, "invincible", StringComparison.OrdinalIgnoreCase)
                || ContainsAny(combinedText, "invincible", "invincib", "immune to damage", "cannot be hit"),
                InvincibleBuffLabel);
            bool isMagicGuardMetadata = skill?.RedirectsDamageToMp == true;
            TrackIfMissing(
                temporaryStats,
                skill?.ReflectsIncomingDamage == true
                    || ContainsAny(skill?.AffectedSkillEffect, "guard", "reflect", "shield", "barrier", "reduction", "reduce")
                    || ContainsAny(
                        combinedText,
                        "damage reduction",
                        "reduce damage",
                        "barrier",
                        "shield",
                        "meso guard",
                        "combo barrier",
                        "power guard",
                        "achilles")
                    || (!isMagicGuardMetadata && ContainsAny(combinedText, "guard")),
                DamageReductionBuffLabel);
            TrackIfMissing(
                temporaryStats,
                ContainsAny(combinedText, "all stats", "all stat", "all players' stats", "all player's stats"),
                AllStatsBuffLabel);
            TrackIfMissing(
                temporaryStats,
                skill?.HasMorphMetadata == true
                    || skill?.MorphId > 0
                    || skill?.UsesTamingMobMount == true
                    || skill?.ClientInfoType == 13
                    || skill?.HasPersistentAvatarEffect == true
                    || skill?.FixedState == true
                    || skill?.CanNotMoveInState == true
                    || skill?.OnlyNormalAttackInState == true
                    || skill?.SpecialNormalAttackInState == true
                    || ContainsAny(combinedText, "transform", "morph", "ride", "vehicle", "siege", "tank"),
                TransformBuffLabel);

            IReadOnlyDictionary<string, int> authoredOrder = BuildAuthoredTemporaryStatOrder(levelData, temporaryStats);
            return temporaryStats
                .OrderBy(stat => GetAuthoredTemporaryStatOrder(authoredOrder, stat))
                .ThenBy(stat => stat.SortOrder)
                .ThenBy(stat => stat.Label, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        internal static IReadOnlyList<RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState> BuildAnimationDisplayerTemporaryStatAvatarEffectStatesForParity(
            IEnumerable<ActiveBuff> activeBuffs)
        {
            if (activeBuffs == null)
            {
                return Array.Empty<RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState>();
            }

            var orderedStates = new List<(RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState State, int PrimaryPriority, int SortOrder, int Index)>();
            int index = 0;
            foreach (ActiveBuff activeBuff in activeBuffs)
            {
                if (TryCreateAnimationDisplayerTemporaryStatAvatarEffectStateForParity(
                        activeBuff,
                        out RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState state,
                        out int primaryPriority,
                        out int sortOrder))
                {
                    orderedStates.Add((state, primaryPriority, sortOrder, index));
                }

                index++;
            }

            return orderedStates
                .OrderBy(candidate => candidate.PrimaryPriority)
                .ThenBy(candidate => candidate.SortOrder)
                .ThenBy(candidate => candidate.Index)
                .Select(candidate => candidate.State)
                .ToArray();
        }

        private static bool TryCreateAnimationDisplayerTemporaryStatAvatarEffectStateForParity(
            ActiveBuff activeBuff,
            out RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState state,
            out int primaryPriority,
            out int sortOrder)
        {
            state = null;
            primaryPriority = int.MaxValue;
            sortOrder = int.MaxValue;

            SkillData sourceSkill = activeBuff?.SkillData;
            if (sourceSkill == null)
            {
                return false;
            }

            IReadOnlyList<BuffTemporaryStatPresentation> temporaryStats = GetBuffTemporaryStatPresentation(activeBuff);
            BuffTemporaryStatPresentation familyOwnerTemporaryStat = ResolveFamilyOwnerTemporaryStat(temporaryStats);
            if (familyOwnerTemporaryStat == null)
            {
                return false;
            }

            SkillData paritySkill = ResolveBuffAvatarEffectSkill(sourceSkill);
            SkillAnimation overlayAnimation = paritySkill?.AvatarOverlayEffect;
            SkillAnimation overlaySecondaryAnimation = paritySkill?.AvatarOverlaySecondaryEffect;
            SkillAnimation underFaceAnimation = paritySkill?.AvatarUnderFaceEffect;
            SkillAnimation underFaceSecondaryAnimation = paritySkill?.AvatarUnderFaceSecondaryEffect;

            if (overlayAnimation == null
                && overlaySecondaryAnimation == null
                && underFaceAnimation == null
                && underFaceSecondaryAnimation == null)
            {
                overlayAnimation = CreateLoopingAvatarEffect(paritySkill?.AffectedEffect);
                overlaySecondaryAnimation = CreateLoopingAvatarEffect(paritySkill?.AffectedSecondaryEffect);
                if (overlayAnimation == null && overlaySecondaryAnimation == null)
                {
                    AssignAvatarBuffEffectPlane(
                        CreateLoopingAvatarEffect(paritySkill?.Effect),
                        ref overlayAnimation,
                        ref overlaySecondaryAnimation,
                        ref underFaceAnimation,
                        ref underFaceSecondaryAnimation);
                    AssignAvatarBuffEffectPlane(
                        CreateLoopingAvatarEffect(paritySkill?.EffectSecondary),
                        ref overlayAnimation,
                        ref overlaySecondaryAnimation,
                        ref underFaceAnimation,
                        ref underFaceSecondaryAnimation);
                    AssignAvatarBuffEffectPlane(
                        CreateLoopingAvatarEffect(paritySkill?.RepeatEffect),
                        ref overlayAnimation,
                        ref overlaySecondaryAnimation,
                        ref underFaceAnimation,
                        ref underFaceSecondaryAnimation);
                    AssignAvatarBuffEffectPlane(
                        CreateLoopingAvatarEffect(paritySkill?.RepeatSecondaryEffect),
                        ref overlayAnimation,
                        ref overlaySecondaryAnimation,
                        ref underFaceAnimation,
                        ref underFaceSecondaryAnimation);
                }
            }

            bool hasParityAnimations = overlayAnimation != null
                || overlaySecondaryAnimation != null
                || underFaceAnimation != null
                || underFaceSecondaryAnimation != null
                || paritySkill?.AffectedEffect != null
                || paritySkill?.AffectedSecondaryEffect != null
                || paritySkill?.Effect != null
                || paritySkill?.EffectSecondary != null
                || paritySkill?.RepeatEffect != null
                || paritySkill?.RepeatSecondaryEffect != null;
            if (!hasParityAnimations)
            {
                return false;
            }

            primaryPriority = familyOwnerTemporaryStat.PrimaryPriority > 0
                ? familyOwnerTemporaryStat.PrimaryPriority
                : (familyOwnerTemporaryStat.SortOrder > 0 ? familyOwnerTemporaryStat.SortOrder : GenericBuffPrimaryPriority);
            sortOrder = familyOwnerTemporaryStat.SortOrder > 0
                ? familyOwnerTemporaryStat.SortOrder
                : int.MaxValue;
            state = new RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState
            {
                SkillId = paritySkill.SkillId,
                Skill = paritySkill,
                OverlayAnimation = overlayAnimation,
                OverlaySecondaryAnimation = overlaySecondaryAnimation,
                UnderFaceAnimation = underFaceAnimation,
                UnderFaceSecondaryAnimation = underFaceSecondaryAnimation,
                AnimationStartTime = activeBuff.StartTime
            };
            return true;
        }

        private static bool HasExplicitDamageReductionTemporaryStatOwner(SkillLevelData levelData)
        {
            return levelData != null
                && levelData.DamageReductionRate > 0
                && HasAuthoredTemporaryStatProperty(levelData, "damR", "indieDamR");
        }

        private static void RemoveTemporaryStatLabel(
            ICollection<BuffTemporaryStatPresentation> temporaryStats,
            string label)
        {
            if (temporaryStats is not List<BuffTemporaryStatPresentation> mutableTemporaryStats
                || string.IsNullOrWhiteSpace(label))
            {
                return;
            }

            mutableTemporaryStats.RemoveAll(existing =>
                existing != null
                && string.Equals(existing.Label, label, StringComparison.OrdinalIgnoreCase));
        }

        private static BuffTemporaryStatPresentation ResolvePrimaryTemporaryStat(IReadOnlyList<BuffTemporaryStatPresentation> temporaryStats)
        {
            if (temporaryStats == null || temporaryStats.Count == 0)
            {
                return null;
            }

            BuffTemporaryStatPresentation primaryTemporaryStat = null;
            int bestPrimaryPriority = int.MaxValue;
            int bestSortOrder = int.MaxValue;
            for (int i = 0; i < temporaryStats.Count; i++)
            {
                BuffTemporaryStatPresentation candidate = temporaryStats[i];
                if (candidate == null)
                {
                    continue;
                }

                int candidatePrimaryPriority = candidate.PrimaryPriority > 0
                    ? candidate.PrimaryPriority
                    : (candidate.SortOrder > 0 ? candidate.SortOrder : GenericBuffPrimaryPriority);
                int candidateSortOrder = candidate.SortOrder > 0 ? candidate.SortOrder : int.MaxValue;
                if (primaryTemporaryStat == null
                    || candidatePrimaryPriority < bestPrimaryPriority
                    || (candidatePrimaryPriority == bestPrimaryPriority && candidateSortOrder < bestSortOrder))
                {
                    primaryTemporaryStat = candidate;
                    bestPrimaryPriority = candidatePrimaryPriority;
                    bestSortOrder = candidateSortOrder;
                }
            }

            return primaryTemporaryStat ?? temporaryStats[0];
        }

        private static BuffTemporaryStatPresentation ResolveFamilyOwnerTemporaryStat(IReadOnlyList<BuffTemporaryStatPresentation> temporaryStats)
        {
            if (temporaryStats == null || temporaryStats.Count == 0)
            {
                return null;
            }

            for (int i = 0; i < temporaryStats.Count; i++)
            {
                BuffTemporaryStatPresentation candidate = temporaryStats[i];
                if (candidate != null && !IsGenericStatTemporaryStatLabel(candidate.Label))
                {
                    return candidate;
                }
            }

            for (int i = 0; i < temporaryStats.Count; i++)
            {
                if (temporaryStats[i] != null)
                {
                    return temporaryStats[i];
                }
            }

            return null;
        }

        private static BuffTemporaryStatPresentation ResolveAuthoredExplicitFamilyOwnerTemporaryStat(
            SkillLevelData levelData,
            IReadOnlyList<BuffTemporaryStatPresentation> temporaryStats)
        {
            if (levelData?.AuthoredPropertyOrder == null
                || levelData.AuthoredPropertyOrder.Count == 0
                || temporaryStats == null
                || temporaryStats.Count == 0)
            {
                return null;
            }

            var activeTemporaryStatsByLabel = temporaryStats
                .Where(stat => stat != null && !string.IsNullOrWhiteSpace(stat.Label))
                .GroupBy(stat => stat.Label, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            if (activeTemporaryStatsByLabel.Count == 0)
            {
                return null;
            }

            foreach (string propertyName in levelData.AuthoredPropertyOrder)
            {
                if (string.IsNullOrWhiteSpace(propertyName)
                    || !TryResolveAuthoredPropertyFamilyDisplayName(propertyName, out _)
                    || !AuthoredPropertyTemporaryStatLabels.TryGetValue(propertyName, out string[] labels)
                    || labels == null)
                {
                    continue;
                }

                for (int i = 0; i < labels.Length; i++)
                {
                    string label = labels[i];
                    if (!string.IsNullOrWhiteSpace(label)
                        && activeTemporaryStatsByLabel.TryGetValue(label, out BuffTemporaryStatPresentation temporaryStat))
                    {
                        return temporaryStat;
                    }
                }
            }

            return null;
        }

        private static bool IsGenericStatTemporaryStatLabel(string label)
        {
            return string.Equals(label, "PAD", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(label, "PDD", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(label, "MAD", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(label, "MDD", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(label, "ACC", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(label, "EVA", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(label, "Speed", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(label, "Jump", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(label, StrengthBuffLabel, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(label, DexterityBuffLabel, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(label, IntelligenceBuffLabel, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(label, LuckBuffLabel, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(label, MaxHpBuffLabel, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(label, MaxMpBuffLabel, StringComparison.OrdinalIgnoreCase);
        }

        private static int ResolveStatusBarBuffSortOrder(
            IReadOnlyList<BuffTemporaryStatPresentation> temporaryStats,
            BuffTemporaryStatPresentation primaryTemporaryStat,
            BuffIconCatalogEntry supplementalBuffIconEntry,
            bool preferPrimaryTemporaryStatSortOrder = false)
        {
            if (preferPrimaryTemporaryStatSortOrder && primaryTemporaryStat?.SortOrder > 0)
            {
                return primaryTemporaryStat.SortOrder;
            }

            int bestSortOrder = int.MaxValue;
            if (temporaryStats != null)
            {
                for (int i = 0; i < temporaryStats.Count; i++)
                {
                    BuffTemporaryStatPresentation presentation = temporaryStats[i];
                    if (presentation?.SortOrder > 0 && presentation.SortOrder < bestSortOrder)
                    {
                        bestSortOrder = presentation.SortOrder;
                    }
                }
            }

            if (bestSortOrder != int.MaxValue)
            {
                return bestSortOrder;
            }

            if (primaryTemporaryStat == null)
            {
                return supplementalBuffIconEntry?.SortOrder > 0
                    ? supplementalBuffIconEntry.SortOrder
                    : GenericBuffSortOrder;
            }

            return primaryTemporaryStat.PrimaryPriority > 0
                ? primaryTemporaryStat.PrimaryPriority
                : (primaryTemporaryStat.SortOrder > 0 ? primaryTemporaryStat.SortOrder : GenericBuffPrimaryPriority);
        }

        private static IReadOnlyDictionary<string, int> BuildClientSecondaryStatSortOrderCatalog(params string[] labels)
        {
            var catalog = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (labels == null)
            {
                return catalog;
            }

            for (int i = 0; i < labels.Length; i++)
            {
                string label = labels[i];
                if (string.IsNullOrWhiteSpace(label) || catalog.ContainsKey(label))
                {
                    continue;
                }

                catalog[label] = ClientSecondaryStatSortOrderBase + (i * ClientSecondaryStatSortOrderStep);
            }

            return catalog;
        }

        private static int ResolveClientSecondaryStatSortOrder(string secondaryStatLabel, int fallbackSortOrder)
        {
            return !string.IsNullOrWhiteSpace(secondaryStatLabel)
                && ClientSecondaryStatSortOrderCatalog.TryGetValue(secondaryStatLabel, out int sortOrder)
                ? sortOrder
                : fallbackSortOrder;
        }

        private static string ResolveBuffIconKey(
            BuffTemporaryStatPresentation primaryTemporaryStat,
            BuffIconCatalogEntry supplementalBuffIconEntry = null)
        {
            if (primaryTemporaryStat == null)
            {
                return supplementalBuffIconEntry?.IconKey ?? GenericBuffIconKey;
            }

            return string.IsNullOrWhiteSpace(primaryTemporaryStat.IconKey)
                ? (supplementalBuffIconEntry?.IconKey ?? GenericBuffIconKey)
                : primaryTemporaryStat.IconKey;
        }

        private static string ResolveBuffFamilyDisplayName(
            string authoredFamilyDisplayName,
            BuffTemporaryStatPresentation primaryTemporaryStat,
            BuffIconCatalogEntry supplementalBuffIconEntry)
        {
            if (!string.IsNullOrWhiteSpace(authoredFamilyDisplayName))
            {
                return authoredFamilyDisplayName;
            }

            if (!string.IsNullOrWhiteSpace(primaryTemporaryStat?.DisplayName))
            {
                return primaryTemporaryStat.DisplayName;
            }

            if (!string.IsNullOrWhiteSpace(supplementalBuffIconEntry?.DisplayName))
            {
                return supplementalBuffIconEntry.DisplayName;
            }

            return TryGetBuffIconCatalogEntry(GenericBuffIconKey, out BuffIconCatalogEntry genericBuffIconEntry)
                ? genericBuffIconEntry.DisplayName
                : string.Empty;
        }

        private static string ResolveAuthoredFamilyDisplayName(
            SkillLevelData levelData,
            IReadOnlyCollection<BuffTemporaryStatPresentation> temporaryStats,
            BuffTemporaryStatPresentation familyOwnerTemporaryStat)
        {
            if (levelData?.AuthoredPropertyOrder == null
                || levelData.AuthoredPropertyOrder.Count == 0
                || temporaryStats == null
                || temporaryStats.Count == 0
                || string.IsNullOrWhiteSpace(familyOwnerTemporaryStat?.Label))
            {
                return null;
            }

            var activeLabels = new HashSet<string>(
                temporaryStats
                    .Where(stat => stat != null)
                    .Select(stat => stat.Label),
                StringComparer.OrdinalIgnoreCase);
            if (activeLabels.Count == 0)
            {
                return null;
            }

            string resolvedDisplayName = null;
            foreach (string propertyName in levelData.AuthoredPropertyOrder)
            {
                if (string.IsNullOrWhiteSpace(propertyName)
                    || !AuthoredPropertyTemporaryStatLabels.TryGetValue(propertyName, out string[] labels)
                    || labels == null
                    || !labels.Any(activeLabels.Contains)
                    || !labels.Any(label => string.Equals(label, familyOwnerTemporaryStat.Label, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (TryResolveAuthoredPropertyFamilyDisplayName(propertyName, out string displayName))
                {
                    return displayName;
                }
            }

            return resolvedDisplayName;
        }

        private static IReadOnlyList<string> ResolveAuthoredTemporaryStatDisplayNames(
            SkillLevelData levelData,
            IReadOnlyList<BuffTemporaryStatPresentation> temporaryStats)
        {
            if (temporaryStats == null || temporaryStats.Count == 0)
            {
                return Array.Empty<string>();
            }

            var resolvedDisplayNames = new List<string>(temporaryStats.Count);
            var seenDisplayNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var representedLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var activeTemporaryStatsByLabel = temporaryStats
                .Where(stat => stat != null && !string.IsNullOrWhiteSpace(stat.Label))
                .GroupBy(stat => stat.Label, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            if (levelData?.AuthoredPropertyOrder != null && levelData.AuthoredPropertyOrder.Count > 0 && activeTemporaryStatsByLabel.Count > 0)
            {
                foreach (string propertyName in levelData.AuthoredPropertyOrder)
                {
                    if (!TryResolveAuthoredTemporaryStatDisplayName(propertyName, activeTemporaryStatsByLabel, representedLabels, out string displayName))
                    {
                        continue;
                    }

                    if (seenDisplayNames.Add(displayName))
                    {
                        resolvedDisplayNames.Add(displayName);
                    }
                }
            }

            foreach (BuffTemporaryStatPresentation temporaryStat in temporaryStats)
            {
                if (!string.IsNullOrWhiteSpace(temporaryStat?.Label) && representedLabels.Contains(temporaryStat.Label))
                {
                    continue;
                }

                string displayName = temporaryStat?.DisplayName;
                if (!string.IsNullOrWhiteSpace(displayName) && seenDisplayNames.Add(displayName))
                {
                    resolvedDisplayNames.Add(displayName);
                }
            }

            return resolvedDisplayNames;
        }

        private static bool TryResolveAuthoredTemporaryStatDisplayName(
            string propertyName,
            IReadOnlyDictionary<string, BuffTemporaryStatPresentation> activeTemporaryStatsByLabel,
            ISet<string> representedLabels,
            out string displayName)
        {
            displayName = null;
            if (string.IsNullOrWhiteSpace(propertyName)
                || activeTemporaryStatsByLabel == null
                || activeTemporaryStatsByLabel.Count == 0
                || !AuthoredPropertyTemporaryStatLabels.TryGetValue(propertyName, out string[] labels)
                || labels == null)
            {
                return false;
            }

            if (AuthoredPropertyFamilyDisplayNameOverrides.TryGetValue(propertyName, out string overrideDisplayName)
                && !string.IsNullOrWhiteSpace(overrideDisplayName))
            {
                TrackRepresentedLabels(labels, representedLabels, activeTemporaryStatsByLabel);
                displayName = overrideDisplayName;
                return true;
            }

            if (TryResolveAuthoredPropertyTooltipDisplayName(propertyName, out string authoredDisplayName))
            {
                TrackRepresentedLabels(labels, representedLabels, activeTemporaryStatsByLabel);
                displayName = authoredDisplayName;
                return true;
            }

            foreach (string label in labels)
            {
                if (string.IsNullOrWhiteSpace(label)
                    || !activeTemporaryStatsByLabel.TryGetValue(label, out BuffTemporaryStatPresentation temporaryStat)
                    || string.IsNullOrWhiteSpace(temporaryStat?.DisplayName))
                {
                    continue;
                }

                representedLabels?.Add(label);
                displayName = temporaryStat.DisplayName;
                return true;
            }

            return false;
        }

        private static bool TryResolveAuthoredPropertyFamilyDisplayName(string propertyName, out string displayName)
        {
            displayName = null;
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                return false;
            }

            if (AuthoredPropertyFamilyDisplayNameOverrides.TryGetValue(propertyName, out string overrideDisplayName)
                && !string.IsNullOrWhiteSpace(overrideDisplayName))
            {
                displayName = overrideDisplayName;
                return true;
            }

            return ShouldUseFallbackPropertyDisplayName(propertyName, includeIndependentStats: true)
                && SkillDataLoader.TryResolveFallbackStatPresentation(propertyName, 1, out string fallbackDisplayName, out _)
                && !string.IsNullOrWhiteSpace(displayName = fallbackDisplayName);
        }

        private static bool TryResolveAuthoredPropertyTooltipDisplayName(string propertyName, out string displayName)
        {
            displayName = null;
            return ShouldUseFallbackPropertyDisplayName(propertyName, includeIndependentStats: true)
                && SkillDataLoader.TryResolveFallbackStatPresentation(propertyName, 1, out string fallbackDisplayName, out _)
                && !string.IsNullOrWhiteSpace(displayName = fallbackDisplayName);
        }

        private static bool ShouldUseFallbackPropertyDisplayName(string propertyName, bool includeIndependentStats)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                return false;
            }

            if (propertyName.EndsWith("R", StringComparison.OrdinalIgnoreCase)
                || propertyName.IndexOf("criticaldamage", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return includeIndependentStats
                && propertyName.StartsWith("indie", StringComparison.OrdinalIgnoreCase);
        }

        private static void TrackRepresentedLabels(
            IEnumerable<string> labels,
            ISet<string> representedLabels,
            IReadOnlyDictionary<string, BuffTemporaryStatPresentation> activeTemporaryStatsByLabel)
        {
            if (labels == null || representedLabels == null || activeTemporaryStatsByLabel == null)
            {
                return;
            }

            foreach (string label in labels)
            {
                if (!string.IsNullOrWhiteSpace(label) && activeTemporaryStatsByLabel.ContainsKey(label))
                {
                    representedLabels.Add(label);
                }
            }
        }

        internal static (string FamilyDisplayName, IReadOnlyList<string> TemporaryStatDisplayNames) ResolveStatusBarBuffTooltipPresentationForParity(
            SkillLevelData levelData,
            params string[] temporaryStatLabels)
        {
            if (temporaryStatLabels == null || temporaryStatLabels.Length == 0)
            {
                return (string.Empty, Array.Empty<string>());
            }

            BuffTemporaryStatPresentation[] temporaryStats = temporaryStatLabels
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .Select(label => ResolvedTemporaryStatPresentationCatalog.TryGetValue(label, out BuffTemporaryStatPresentation presentation)
                    ? presentation
                    : CreateTemporaryStatPresentation(label, label, null, GenericBuffSortOrder, GenericBuffPrimaryPriority))
                .ToArray();
            if (temporaryStats.Length == 0)
            {
                return (string.Empty, Array.Empty<string>());
            }

            ISet<string> directBuffIconEligibleLabels = BuildDirectBuffIconEligibleLabelSet(levelData);
            BuffTemporaryStatPresentation authoredExplicitFamilyOwnerTemporaryStat = ResolveAuthoredExplicitFamilyOwnerTemporaryStat(
                levelData,
                temporaryStats);
            BuffTemporaryStatPresentation trayLaneTemporaryStat = ResolvePrimaryTemporaryStat(temporaryStats);
            BuffTemporaryStatPresentation familyOwnerTemporaryStat = authoredExplicitFamilyOwnerTemporaryStat
                ?? ResolveFamilyOwnerTemporaryStat(temporaryStats);
            BuffTemporaryStatPresentation iconOwnerTemporaryStat = ResolveIconOwnerTemporaryStat(
                temporaryStats,
                trayLaneTemporaryStat,
                familyOwnerTemporaryStat,
                directBuffIconEligibleLabels);
            if (iconOwnerTemporaryStat == null && authoredExplicitFamilyOwnerTemporaryStat != null)
            {
                trayLaneTemporaryStat = authoredExplicitFamilyOwnerTemporaryStat;
            }

            BuffTemporaryStatPresentation displayOwnerTemporaryStat = ResolveDisplayOwnerTemporaryStat(
                trayLaneTemporaryStat,
                iconOwnerTemporaryStat,
                familyOwnerTemporaryStat,
                directBuffIconEligibleLabels);
            string authoredFamilyDisplayName = ResolveAuthoredFamilyDisplayName(
                levelData,
                temporaryStats,
                displayOwnerTemporaryStat);
            string familyDisplayName = ResolveBuffFamilyDisplayName(
                authoredFamilyDisplayName,
                displayOwnerTemporaryStat,
                supplementalBuffIconEntry: null);
            IReadOnlyList<string> temporaryStatDisplayNames = ResolveAuthoredTemporaryStatDisplayNames(levelData, temporaryStats);
            return (familyDisplayName, temporaryStatDisplayNames);
        }

        private static BuffTemporaryStatPresentation CreateTemporaryStatPresentation(
            string label,
            string displayName,
            string iconKey,
            int sortOrder,
            int primaryPriority)
        {
            return new BuffTemporaryStatPresentation
            {
                Label = label,
                DisplayName = displayName,
                IconKey = iconKey,
                SortOrder = sortOrder,
                PrimaryPriority = primaryPriority
            };
        }

        private static void TrackIfMissing(
            ICollection<BuffTemporaryStatPresentation> temporaryStats,
            bool condition,
            string label)
        {
            if (!condition
                || temporaryStats == null
                || !ResolvedTemporaryStatPresentationCatalog.TryGetValue(label, out BuffTemporaryStatPresentation presentation)
                || temporaryStats.Any(existing => string.Equals(existing.Label, label, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            temporaryStats.Add(presentation);
        }

        private static void TrackDescriptionTemporaryStats(
            ICollection<BuffTemporaryStatPresentation> temporaryStats,
            string combinedText)
        {
            if (temporaryStats == null || string.IsNullOrWhiteSpace(combinedText))
            {
                return;
            }

            foreach ((string label, string[] fragments) in TemporaryStatTextAliases)
            {
                TrackIfMissing(temporaryStats, ContainsAny(combinedText, fragments), label);
            }
        }

        private static void TrackIdentityTemporaryStats(
            ICollection<BuffTemporaryStatPresentation> temporaryStats,
            SkillData skill,
            SkillLevelData levelData,
            string combinedText)
        {
            if (temporaryStats == null)
            {
                return;
            }

            bool hasLevelData = levelData != null;

            bool isHyperBody = ContainsAny(combinedText, "hyper body");
            TrackIfMissing(temporaryStats, isHyperBody, HyperBodyBuffLabel);
            TrackIfMissing(
                temporaryStats,
                isHyperBody && hasLevelData && (levelData.EnhancedMaxHP > 0 || levelData.MaxHPPercent > 0 || levelData.X > 0),
                MaxHpBuffLabel);
            TrackIfMissing(
                temporaryStats,
                isHyperBody && hasLevelData && (levelData.EnhancedMaxMP > 0 || levelData.MaxMPPercent > 0 || levelData.Y > 0),
                MaxMpBuffLabel);

            bool isMapleWarrior = ContainsAny(combinedText, "maple warrior", "echo of hero", "echo of the hero");
            TrackIfMissing(temporaryStats, isMapleWarrior, MapleWarriorBuffLabel);
            TrackIfMissing(
                temporaryStats,
                isMapleWarrior && hasLevelData && (levelData.AllStat > 0 || HasAllPrimaryStatBonus(levelData) || levelData.X > 0 || levelData.Y > 0 || levelData.Z > 0),
                AllStatsBuffLabel);

            bool isBless = ContainsAny(combinedText, "bless", "blessing");
            TrackIfMissing(temporaryStats, isBless, BlessBuffLabel);
            TrackIfMissing(temporaryStats, isBless && hasLevelData && levelData.ACC > 0, "ACC");
            TrackIfMissing(temporaryStats, isBless && hasLevelData && levelData.EVA > 0, "EVA");
            TrackIfMissing(
                temporaryStats,
                isBless && hasLevelData && (levelData.PAD > 0 || levelData.EnhancedPAD > 0 || levelData.X > 0),
                "PAD");
            TrackIfMissing(
                temporaryStats,
                isBless && hasLevelData && (levelData.MAD > 0 || levelData.Y > 0),
                "MAD");

            bool isSharpEyes = ContainsAny(combinedText, "sharp eyes", "weak spot");
            TrackIfMissing(temporaryStats, isSharpEyes, SharpEyesBuffLabel);
            TrackIfMissing(
                temporaryStats,
                isSharpEyes && hasLevelData && (levelData.CriticalRate > 0 || levelData.X > 0),
                CriticalRateBuffLabel);
            TrackIfMissing(
                temporaryStats,
                isSharpEyes
                    && hasLevelData
                    && (levelData.CriticalDamageMin > 0 || levelData.CriticalDamageMax > 0 || levelData.Y > 0),
                CriticalDamageBuffLabel);

            bool isMagicGuard = skill?.RedirectsDamageToMp == true
                || ContainsAny(combinedText, "magic guard", "damage dealt to you affects your mp instead of your hp");
            TrackIfMissing(temporaryStats, isMagicGuard, MagicGuardBuffLabel);

            bool isSoulArrow = ContainsAny(combinedText, "soul arrow", "spirit javelin", "shadow claw");
            TrackIfMissing(temporaryStats, isSoulArrow, SoulArrowBuffLabel);

            bool isDash = ContainsAny(combinedText, "dash activation", "temporarily boost speed and jump");
            TrackIfMissing(temporaryStats, isDash, DashBuffLabel);
            TrackIfMissing(temporaryStats, isDash && hasLevelData && (levelData.Speed > 0 || levelData.X > 0), "Speed");
            TrackIfMissing(temporaryStats, isDash && hasLevelData && (levelData.Jump > 0 || levelData.Y > 0), "Jump");

            bool isHaste = ContainsAny(combinedText, "haste");
            TrackIfMissing(temporaryStats, isHaste, "Speed");
            TrackIfMissing(temporaryStats, isHaste, "Jump");

            bool isBooster = skill?.IsRapidAttack == true || ContainsAny(combinedText, "booster", "attack speed", "weapon speed");
            TrackIfMissing(temporaryStats, isBooster, BoosterBuffLabel);

            TrackIfMissing(temporaryStats, ContainsAny(combinedText, "combo attack", "combo orb", "combo counter", "combo"), ComboBuffLabel);
            bool isCharge = skill?.UsesEnergyChargeRuntime == true
                || !string.IsNullOrWhiteSpace(skill?.FullChargeEffectName)
                || ContainsAny(combinedText, "charge", "elemental charge", "energy charge");
            TrackIfMissing(temporaryStats, isCharge, ChargeBuffLabel);
            bool isAura = string.Equals(skill?.DotType, "aura", StringComparison.OrdinalIgnoreCase)
                || ContainsAny(combinedText, "aura", "dark aura", "blue aura", "yellow aura", "body boost");
            TrackIfMissing(temporaryStats, isAura, AuraBuffLabel);
            TrackIfMissing(
                temporaryStats,
                skill?.HasShadowPartnerActionAnimations == true
                    || ContainsAny(combinedText, "shadow partner", "mirror image", "shadow image", "clone"),
                ShadowPartnerBuffLabel);
            TrackIfMissing(
                temporaryStats,
                (hasLevelData && (levelData.AbnormalStatusResistance > 0 || levelData.ElementalResistance > 0))
                    || ContainsAny(combinedText, "holy shield", "abnormal status", "status ailment", "status immunity"),
                DebuffResistanceBuffLabel);
            TrackIfMissing(temporaryStats, ContainsAny(combinedText, "holy symbol", "experience", "experience points", "exp gained", "exp rate", "exp +"), ExperienceBuffLabel);
            TrackIfMissing(temporaryStats, ContainsAny(combinedText, "item drop", "drop rate", "drop +"), DropRateBuffLabel);
            TrackIfMissing(temporaryStats, ContainsAny(combinedText, "meso up", "mesos obtained", "meso obtained", "meso +"), MesoRateBuffLabel);
            TrackIfMissing(temporaryStats, ContainsAny(combinedText, "dark sight", "wind walk", "stealth", "hide in the shadows"), DarkSightBuffLabel);
            TrackIfMissing(temporaryStats, ContainsAny(combinedText, "recover hp", "recover mp", "hp recovery", "mp recovery", "regeneration", "regen"), RecoveryBuffLabel);
        }

        private static bool HasAllPrimaryStatBonus(SkillLevelData levelData)
        {
            return levelData != null
                && levelData.STR > 0
                && levelData.DEX > 0
                && levelData.INT > 0
                && levelData.LUK > 0;
        }

        private static bool ShouldTrackIndividualPrimaryStats(SkillLevelData levelData)
        {
            return levelData != null
                && !HasCollapsedAllStatsPayload(levelData)
                && (levelData.STR > 0 || levelData.DEX > 0 || levelData.INT > 0 || levelData.LUK > 0);
        }

        private static bool HasCollapsedAllStatsPayload(SkillLevelData levelData)
        {
            return levelData != null
                && (levelData.AllStat > 0 || HasAllPrimaryStatBonus(levelData));
        }

        private static bool ContainsAny(string sourceText, params string[] fragments)
        {
            if (string.IsNullOrWhiteSpace(sourceText) || fragments == null)
            {
                return false;
            }

            string normalizedSourceText = NormalizeTemporaryStatMatchText(sourceText);
            foreach (string fragment in fragments)
            {
                if (string.IsNullOrWhiteSpace(fragment))
                {
                    continue;
                }

                if (sourceText.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                string normalizedFragment = NormalizeTemporaryStatMatchText(fragment);
                if (!string.IsNullOrWhiteSpace(normalizedFragment)
                    && normalizedSourceText.IndexOf(normalizedFragment, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeTemporaryStatMatchText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            char[] buffer = new char[text.Length];
            int length = 0;
            foreach (char character in text)
            {
                if (char.IsLetterOrDigit(character))
                {
                    buffer[length++] = char.ToLowerInvariant(character);
                }
            }

            return length == 0
                ? string.Empty
                : new string(buffer, 0, length);
        }

        private static BuffIconCatalogEntry ResolveSupplementalBuffIconEntry(
            ActiveBuff buff,
            BuffTemporaryStatPresentation primaryTemporaryStat)
        {
            if (buff?.SkillData == null
                || (primaryTemporaryStat != null && !string.IsNullOrWhiteSpace(primaryTemporaryStat.IconKey)))
            {
                return null;
            }

            string combinedText = BuildCombinedTemporaryStatText(buff.SkillData);
            if (ContainsAny(combinedText, "anniversary")
                && TryGetBuffIconCatalogEntry(AnniversaryBuffIconKey, out BuffIconCatalogEntry anniversaryEntry))
            {
                return anniversaryEntry;
            }

            return null;
        }

        private static BuffTemporaryStatPresentation ResolveIconOwnerTemporaryStat(
            IReadOnlyList<BuffTemporaryStatPresentation> temporaryStats,
            BuffTemporaryStatPresentation primaryTemporaryStat,
            BuffTemporaryStatPresentation familyOwnerTemporaryStat,
            ISet<string> directBuffIconEligibleLabels)
        {
            if (HasResolvedExplicitBuffIcon(primaryTemporaryStat, directBuffIconEligibleLabels))
            {
                return primaryTemporaryStat;
            }

            if (HasResolvedExplicitBuffIcon(familyOwnerTemporaryStat, directBuffIconEligibleLabels))
            {
                return familyOwnerTemporaryStat;
            }

            if (temporaryStats == null || temporaryStats.Count == 0)
            {
                return null;
            }

            for (int i = 0; i < temporaryStats.Count; i++)
            {
                BuffTemporaryStatPresentation candidate = temporaryStats[i];
                if (HasResolvedExplicitBuffIcon(candidate, directBuffIconEligibleLabels))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static BuffTemporaryStatPresentation ResolveDisplayOwnerTemporaryStat(
            BuffTemporaryStatPresentation trayLaneTemporaryStat,
            BuffTemporaryStatPresentation iconOwnerTemporaryStat,
            BuffTemporaryStatPresentation familyOwnerTemporaryStat,
            ISet<string> directBuffIconEligibleLabels)
        {
            if (HasResolvedExplicitBuffIcon(iconOwnerTemporaryStat, directBuffIconEligibleLabels))
            {
                return iconOwnerTemporaryStat;
            }

            if (HasResolvedExplicitBuffIcon(trayLaneTemporaryStat, directBuffIconEligibleLabels))
            {
                return trayLaneTemporaryStat;
            }

            return familyOwnerTemporaryStat ?? trayLaneTemporaryStat ?? iconOwnerTemporaryStat;
        }

        private static bool HasResolvedBuffIcon(BuffTemporaryStatPresentation presentation)
        {
            return presentation != null && !string.IsNullOrWhiteSpace(presentation.IconKey);
        }

        private static bool HasResolvedExplicitBuffIcon(
            BuffTemporaryStatPresentation presentation,
            ISet<string> directBuffIconEligibleLabels)
        {
            if (!HasResolvedBuffIcon(presentation))
            {
                return false;
            }

            return directBuffIconEligibleLabels != null
                && directBuffIconEligibleLabels.Contains(presentation.Label);
        }

        private static string BuildCombinedTemporaryStatText(SkillData skill)
        {
            if (skill == null)
            {
                return string.Empty;
            }

            return string.Join(
                " ",
                new[]
                {
                    skill.Name ?? string.Empty,
                    SkillDataTextSurface.GetDescriptionSurface(skill),
                    skill.DotType ?? string.Empty,
                    skill.ActionName ?? string.Empty,
                    string.Join(" ", skill.ActionNames ?? Array.Empty<string>()),
                    skill.PrepareActionName ?? string.Empty,
                    skill.KeydownActionName ?? string.Empty,
                    skill.KeydownEndActionName ?? string.Empty,
                    skill.FullChargeEffectName ?? string.Empty,
                    skill.TriggerCondition ?? string.Empty,
                    skill.ZoneType ?? string.Empty,
                    skill.DebuffMessageToken ?? string.Empty,
                    skill.AffectedSkillEffect ?? string.Empty
                }.Where(text => !string.IsNullOrWhiteSpace(text)));
        }

        private static bool TryGetBuffIconCatalogEntry(string iconKey, out BuffIconCatalogEntry entry)
        {
            entry = null;
            return !string.IsNullOrWhiteSpace(iconKey)
                && ResolvedBuffIconCatalog != null
                && ResolvedBuffIconCatalog.TryGetValue(iconKey, out entry)
                && entry != null;
        }

        private static BuffTemporaryStatPresentation ResolveTemporaryStatPresentation(
            BuffTemporaryStatPresentation basePresentation,
            IReadOnlyDictionary<string, BuffIconCatalogEntry> buffIconCatalog)
        {
            if (basePresentation == null
                || string.IsNullOrWhiteSpace(basePresentation.IconKey)
                || buffIconCatalog == null
                || !buffIconCatalog.TryGetValue(basePresentation.IconKey, out BuffIconCatalogEntry buffIconEntry)
                || buffIconEntry == null)
            {
                return basePresentation;
            }

            string displayName = string.IsNullOrWhiteSpace(buffIconEntry.DisplayName)
                ? basePresentation.DisplayName
                : buffIconEntry.DisplayName;
            int sortOrder = buffIconEntry.SortOrder > 0
                ? buffIconEntry.SortOrder
                : basePresentation.SortOrder;

            if (string.Equals(displayName, basePresentation.DisplayName, StringComparison.Ordinal)
                && sortOrder == basePresentation.SortOrder)
            {
                return basePresentation;
            }

            return CreateTemporaryStatPresentation(
                basePresentation.Label,
                displayName,
                basePresentation.IconKey,
                sortOrder,
                basePresentation.PrimaryPriority);
        }

        private static IReadOnlyDictionary<string, int> BuildAuthoredTemporaryStatOrder(
            SkillLevelData levelData,
            IReadOnlyCollection<BuffTemporaryStatPresentation> temporaryStats)
        {
            if (levelData?.AuthoredPropertyOrder == null
                || levelData.AuthoredPropertyOrder.Count == 0
                || temporaryStats == null
                || temporaryStats.Count == 0)
            {
                return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            }

            var activeLabels = new HashSet<string>(
                temporaryStats.Select(stat => stat.Label),
                StringComparer.OrdinalIgnoreCase);
            var authoredOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int nextOrder = 0;

            foreach (string propertyName in levelData.AuthoredPropertyOrder)
            {
                if (string.IsNullOrWhiteSpace(propertyName)
                    || !AuthoredPropertyTemporaryStatLabels.TryGetValue(propertyName, out string[] labels))
                {
                    continue;
                }

                foreach (string label in labels)
                {
                    if (activeLabels.Contains(label) && !authoredOrder.ContainsKey(label))
                    {
                        authoredOrder[label] = nextOrder++;
                    }
                }
            }

            return authoredOrder;
        }

        private static ISet<string> BuildAuthoredTemporaryStatLabelSet(
            SkillLevelData levelData,
            IReadOnlyCollection<BuffTemporaryStatPresentation> temporaryStats)
        {
            IReadOnlyDictionary<string, int> authoredOrder = BuildAuthoredTemporaryStatOrder(levelData, temporaryStats);
            return authoredOrder.Count == 0
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(authoredOrder.Keys, StringComparer.OrdinalIgnoreCase);
        }

        private static ISet<string> BuildDirectBuffIconEligibleLabelSet(SkillLevelData levelData)
        {
            var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (levelData == null)
            {
                return labels;
            }

            void Track(bool condition, string label)
            {
                if (condition && !string.IsNullOrWhiteSpace(label))
                {
                    labels.Add(label);
                }
            }

            Track(
                ShouldUseDirectBuffIconFamily(
                    levelData,
                    levelData.PAD > 0 || levelData.AttackPercent > 0 || levelData.EnhancedPAD > 0,
                    "pad", "padX", "epad"),
                "PAD");
            Track(
                ShouldUseDirectBuffIconFamily(
                    levelData,
                    levelData.PDD > 0 || levelData.DefensePercent > 0 || levelData.EnhancedPDD > 0,
                    "pdd", "pddX", "epdd", "pddR"),
                "PDD");
            Track(
                ShouldUseDirectBuffIconFamily(
                    levelData,
                    levelData.MAD > 0 || levelData.MagicAttackPercent > 0 || levelData.EnhancedMAD > 0,
                    "mad", "madX", "emad"),
                "MAD");
            Track(
                ShouldUseDirectBuffIconFamily(
                    levelData,
                    levelData.MDD > 0 || levelData.MagicDefensePercent > 0 || levelData.EnhancedMDD > 0,
                    "mdd", "mddX", "emdd", "mddR"),
                "MDD");
            Track(
                ShouldUseDirectBuffIconFamily(
                    levelData,
                    levelData.ACC > 0 || levelData.AccuracyPercent > 0,
                    "acc", "accX", "accR", "ar"),
                "ACC");
            Track(
                ShouldUseDirectBuffIconFamily(
                    levelData,
                    levelData.EVA > 0 || levelData.AvoidabilityPercent > 0,
                    "eva", "evaX", "evaR", "er"),
                "EVA");
            Track(
                ShouldUseDirectBuffIconFamily(
                    levelData,
                    levelData.Speed > 0 || levelData.SpeedPercent > 0,
                    "speed"),
                "Speed");
            Track(
                ShouldUseDirectBuffIconFamily(
                    levelData,
                    levelData.Jump > 0,
                    "jump"),
                "Jump");
            Track(
                HasAuthoredTemporaryStatProperty(levelData, "incCraft"),
                CraftBuffLabel);

            return labels;
        }

        private static bool ShouldUseDirectBuffIconFamily(
            SkillLevelData levelData,
            bool parsedValuePresent,
            params string[] directPropertyNames)
        {
            if (levelData?.AuthoredPropertyOrder == null || levelData.AuthoredPropertyOrder.Count == 0)
            {
                return parsedValuePresent;
            }

            return HasAuthoredTemporaryStatProperty(levelData, directPropertyNames);
        }

        private static int GetAuthoredTemporaryStatOrder(
            IReadOnlyDictionary<string, int> authoredOrder,
            BuffTemporaryStatPresentation presentation)
        {
            return presentation != null
                && authoredOrder != null
                && authoredOrder.TryGetValue(presentation.Label, out int order)
                ? order
                : int.MaxValue;
        }

        private static bool HasAuthoredTemporaryStatProperty(SkillLevelData levelData, params string[] propertyNames)
        {
            if (levelData?.AuthoredPropertyOrder == null
                || levelData.AuthoredPropertyOrder.Count == 0
                || propertyNames == null
                || propertyNames.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < propertyNames.Length; i++)
            {
                string propertyName = propertyNames[i];
                if (string.IsNullOrWhiteSpace(propertyName))
                {
                    continue;
                }

                for (int j = 0; j < levelData.AuthoredPropertyOrder.Count; j++)
                {
                    if (string.Equals(levelData.AuthoredPropertyOrder[j], propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        #endregion

        #region Heal

        private void ApplyHeal(SkillData skill, int level)
        {
            var levelData = skill.GetLevel(level);
            if (levelData == null)
                return;

            // Calculate heal amount
            int hpHeal = levelData.HP;
            int mpHeal = levelData.MP;

            // Some heals are percentage based
            if (levelData.X > 0)
            {
                hpHeal = _player.MaxHP * levelData.X / 100;
            }

            // Apply heal
            _player.Recover(hpHeal, mpHeal);
        }

        #endregion

        #region Passive Skills

        /// <summary>
        /// Get passive skill bonus
        /// </summary>
        public int GetPassiveBonus(BuffStatType stat)
        {
            int total = 0;
            int weaponCode = GetWeaponCode(_player?.Build?.GetWeapon()?.ItemId ?? 0);
            bool hasChargeBuff = HasActiveTemporaryStatLabel(ChargeBuffLabel);

            foreach (var skill in _availableSkills)
            {
                if (!skill.IsPassive)
                    continue;

                int level = GetSkillLevel(skill.SkillId);
                if (level <= 0)
                    continue;

                var levelData = skill.GetLevel(level);
                if (levelData == null)
                    continue;

                if (!PassiveStatBonusAppliesToCurrentWeapon(skill, levelData, weaponCode, hasChargeBuff))
                {
                    continue;
                }

                total += GetPassiveBonus(skill, levelData, stat);
            }

            return total;
        }

        /// <summary>
        /// Get mastery from passive skills
        /// </summary>
        public int GetMastery()
        {
            return GetMastery(_player?.Build?.GetWeapon());
        }

        public int GetMastery(WeaponPart weapon)
        {
            int mastery = 10; // Base mastery
            int weaponCode = GetWeaponCode(weapon?.ItemId ?? 0);
            bool hasChargeBuff = HasActiveTemporaryStatLabel(ChargeBuffLabel);

            foreach (var skill in _availableSkills)
            {
                if (!skill.IsPassive)
                    continue;

                int level = GetSkillLevel(skill.SkillId);
                if (level <= 0)
                    continue;

                var levelData = skill.GetLevel(level);
                TryApplyMastery(skill, levelData, weaponCode, hasChargeBuff, sourceMustBeActive: false, ref mastery);
            }

            foreach (ActiveBuff buff in _buffs)
            {
                TryApplyMastery(buff?.SkillData, buff?.LevelData, weaponCode, hasChargeBuff, sourceMustBeActive: true, ref mastery);
            }

            foreach (ActiveSummon summon in _summons)
            {
                TryApplyMastery(summon?.SkillData, summon?.LevelData, weaponCode, hasChargeBuff, sourceMustBeActive: true, ref mastery);
            }

            return mastery;
        }

        private bool TryApplyMastery(
            SkillData skill,
            SkillLevelData levelData,
            int weaponCode,
            bool hasChargeBuff,
            bool sourceMustBeActive,
            ref int mastery)
        {
            if (levelData?.Mastery <= mastery
                || !SkillMasteryAppliesToWeapon(skill, weaponCode, hasChargeBuff, sourceMustBeActive)
                || (RequiresMechanicVehicleState(skill) && !IsMechanicVehiclePassiveActive(skill)))
            {
                return false;
            }

            mastery = levelData.Mastery;
            return true;
        }

        private bool HasActiveTemporaryStatLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return false;
            }

            return _buffs.Any(
                buff => GetBuffTemporaryStatPresentation(buff).Any(
                    presentation => string.Equals(
                        presentation.Label,
                        label,
                        StringComparison.OrdinalIgnoreCase)));
        }

        private static bool SkillMasteryAppliesToWeapon(SkillData skill, int weaponCode, bool hasChargeBuff, bool sourceMustBeActive)
        {
            if (skill == null || weaponCode <= 0)
                return false;

            if (RequiresChargeStateForMastery(skill) && !hasChargeBuff)
            {
                return false;
            }

            if (sourceMustBeActive && MatchesKnownActiveStateMasterySkill(skill.SkillId, weaponCode))
            {
                return true;
            }

            string name = NormalizeMasteryText(skill.Name);
            string description = NormalizeMasteryText(SkillDataTextSurface.GetDescriptionSurface(skill));
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(description))
                return MatchesKnownStringlessMasterySkill(skill.SkillId, weaponCode);

            bool mentions(string token)
            {
                return name.Contains(token, StringComparison.Ordinal)
                       || description.Contains(token, StringComparison.Ordinal);
            }

            bool mentionsGenericWeaponMastery = mentions("weapon mastery");
            bool mentionsOneHandedBluntAndAxe = mentions("one handed blunt and axe");
            bool mentionsSwordAndAxe = mentions("swords and axes");
            bool mentionsSwordAndBlunt = mentions("swords and blunt");
            bool mentionsSpearAndPolearm = mentions("spears and polearms");
            bool mentionsStaffOnly = mentions("staff mastery");
            bool mentionsWandOnly = mentions("wand mastery");
            bool mentionsGenericSpellMastery = mentions("spell mastery") || mentions("magic mastery");
            bool mentionsFamilyWithGenericWeaponMastery(params string[] familyTokens)
            {
                if (!mentionsGenericWeaponMastery || familyTokens == null)
                {
                    return false;
                }

                foreach (string familyToken in familyTokens)
                {
                    if (!string.IsNullOrWhiteSpace(familyToken) && mentions(familyToken))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool matchesWeapon = weaponCode switch
            {
                30 or 40 => mentions("sword mastery")
                            || mentions("sword expert")
                            || mentionsGenericWeaponMastery && (mentionsSwordAndAxe || mentionsSwordAndBlunt)
                            || mentionsFamilyWithGenericWeaponMastery("sword", "swords"),
                31 => mentions("axe mastery")
                      || mentions("axe expert")
                      || mentionsGenericWeaponMastery && mentionsSwordAndAxe
                      || mentionsFamilyWithGenericWeaponMastery("axe", "axes")
                      || mentionsOneHandedBluntAndAxe,
                41 => mentions("axe mastery")
                      || mentions("axe expert")
                      || mentionsGenericWeaponMastery && mentionsSwordAndAxe
                      || mentionsFamilyWithGenericWeaponMastery("axe", "axes"),
                32 => mentions("blunt weapon mastery")
                      || mentions("blunt weapon expert")
                      || mentions("bw mastery")
                      || mentions("bw expert")
                      || mentionsGenericWeaponMastery && mentionsSwordAndBlunt
                      || mentionsFamilyWithGenericWeaponMastery("blunt weapon", "blunt weapons", "mace", "maces")
                      || mentionsOneHandedBluntAndAxe,
                42 => mentions("blunt weapon mastery")
                      || mentions("blunt weapon expert")
                      || mentions("bw mastery")
                      || mentions("bw expert")
                      || mentionsGenericWeaponMastery && mentionsSwordAndBlunt
                      || mentionsFamilyWithGenericWeaponMastery("blunt weapon", "blunt weapons", "mace", "maces"),
                33 => mentions("dagger mastery")
                      || mentions("dagger expert")
                      || mentions("katara mastery")
                      || mentions("katara expert")
                      || mentions("daggers and kataras")
                      || mentionsFamilyWithGenericWeaponMastery("dagger", "daggers", "katara", "kataras"),
                34 => mentions("katara mastery")
                      || mentions("katara expert")
                      || mentions("daggers and kataras")
                      || mentionsFamilyWithGenericWeaponMastery("katara", "kataras"),
                36 => mentions("cane mastery")
                      || mentions("cane expert")
                      || mentionsFamilyWithGenericWeaponMastery("cane", "canes"),
                37 => mentionsGenericSpellMastery || mentionsWandOnly,
                // WZ `String/Skill.img` keeps true staff books explicit (`Staff Mastery`) while
                // Battle Mage `Barricade Mastery` is authored as one-handed blunt/axe mastery.
                38 => mentionsGenericSpellMastery
                      || mentionsStaffOnly
                      || mentionsFamilyWithGenericWeaponMastery("staff", "staves"),
                43 => mentions("spear mastery")
                      || mentions("spear expert")
                      || mentionsGenericWeaponMastery && mentionsSpearAndPolearm
                      || mentionsFamilyWithGenericWeaponMastery("spear", "spears"),
                44 => mentions("polearm mastery")
                      || mentions("polearm expert")
                      || mentions("pole arm mastery")
                      || mentions("pole arm expert")
                      || mentionsGenericWeaponMastery && mentionsSpearAndPolearm
                      || mentions("high mastery")
                      || mentionsFamilyWithGenericWeaponMastery("polearm", "polearms", "pole arm", "pole arms"),
                45 => mentions("bow mastery")
                      || mentions("bow expert")
                      || mentionsFamilyWithGenericWeaponMastery("bow", "bows"),
                46 => mentions("crossbow mastery")
                      || mentions("crossbow expert")
                      || mentions("cross bow mastery")
                      || mentions("cross bow expert")
                      || mentionsFamilyWithGenericWeaponMastery("crossbow", "crossbows", "cross bow", "cross bows"),
                47 => mentions("claw mastery")
                      || mentions("claw expert")
                      || mentions("throwing star mastery")
                      || mentions("throwing stars")
                      || mentionsFamilyWithGenericWeaponMastery("claw", "claws", "throwing star", "throwing stars"),
                48 => mentions("knuckle mastery")
                      || mentions("knuckle expert")
                      || mentionsFamilyWithGenericWeaponMastery("knuckle", "knuckles"),
                49 => mentions("gun mastery")
                      || mentions("gun expert")
                      || mentions("pistol mastery")
                      || mentions("pistol expert")
                      || mentions("mechanic mastery")
                      || mentions("extreme mech")
                      || mentions("mech")
                      || mentionsFamilyWithGenericWeaponMastery("gun", "guns", "pistol", "pistols"),
                52 => mentions("dual bowguns mastery")
                      || mentions("dual bowguns expert")
                      || mentions("dual bowgun mastery")
                      || mentions("dual bowgun expert")
                      || mentionsFamilyWithGenericWeaponMastery("dual bowgun", "dual bowguns"),
                53 => mentions("cannon mastery")
                      || mentions("cannon expert")
                      || mentions("hand cannon mastery")
                      || mentions("hand cannon expert")
                      || mentionsFamilyWithGenericWeaponMastery("cannon", "cannons", "hand cannon", "hand cannons"),
                56 => mentions("shining rod mastery")
                      || mentions("shining rod expert")
                      || mentionsFamilyWithGenericWeaponMastery("shining rod", "shining rods"),
                58 => mentions("soul shooter mastery")
                      || mentions("soul shooter expert")
                      || mentionsFamilyWithGenericWeaponMastery("soul shooter", "soul shooters"),
                _ => false
            };

            return matchesWeapon;
        }

        private static bool MatchesKnownActiveStateMasterySkill(int skillId, int weaponCode)
        {
            return skillId switch
            {
                // WZ keeps Beholder's mastery bonus on the active summon skill itself:
                // `skill/132.img/skill/1321007/common/mastery = 2*x`, while
                // `String/Skill.img/1321007/desc` only says it raises "weapon mastery".
                // Dark Knight's advancement path is the same spear/polearm branch as `1300000 Weapon Mastery`,
                // so only those weapon families should inherit the summon-gated bonus.
                1321007 => weaponCode is 43 or 44,
                _ => false
            };
        }

        private static bool RequiresChargeStateForMastery(SkillData skill)
        {
            return skill?.SkillId == 1220010;
        }

        private static bool MatchesKnownStringlessMasterySkill(int skillId, int weaponCode)
        {
            return skillId switch
            {
                51100001 => weaponCode == 48,
                // `skill/5112.img/skill/51120001/common/mastery = 55+u(x/2)` is present,
                // but `String/Skill.img` does not publish a matching entry, so keep the
                // Buccaneer-era expert book on the knuckle family via its observed job branch.
                51120001 => weaponCode == 48,
                5700000 => weaponCode == 49,
                _ => false
            };
        }

        private static string NormalizeMasteryText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string flattened = value.Replace("\\r", " ", StringComparison.Ordinal)
                .Replace("\\n", " ", StringComparison.Ordinal)
                .ToLowerInvariant();

            StringBuilder builder = new(flattened.Length);
            bool previousWasSpace = false;
            foreach (char character in flattened)
            {
                char normalized = char.IsLetterOrDigit(character) ? character : ' ';
                if (normalized == ' ')
                {
                    if (previousWasSpace)
                    {
                        continue;
                    }

                    previousWasSpace = true;
                    builder.Append(normalized);
                    continue;
                }

                previousWasSpace = false;
                builder.Append(normalized);
            }

            return builder.ToString().Trim();
        }

        private bool PassiveStatBonusAppliesToCurrentWeapon(
            SkillData skill,
            SkillLevelData levelData,
            int weaponCode,
            bool hasChargeBuff)
        {
            if (skill == null || levelData == null)
            {
                return false;
            }

            if (RequiresMechanicVehicleState(skill) && !IsMechanicVehiclePassiveActive(skill))
            {
                return false;
            }

            if (!PassiveStatBonusRequiresWeaponMatch(skill))
            {
                return true;
            }

            return SkillMasteryAppliesToWeapon(
                skill,
                weaponCode,
                hasChargeBuff,
                sourceMustBeActive: false);
        }

        private static bool PassiveStatBonusRequiresWeaponMatch(SkillData skill)
        {
            if (skill == null)
            {
                return false;
            }

            foreach (int weaponCode in PassiveWeaponSpecificStatWeaponCodes)
            {
                if (SkillMasteryAppliesToWeapon(skill, weaponCode, hasChargeBuff: true, sourceMustBeActive: false))
                {
                    return true;
                }
            }

            return false;
        }

        private int GetPassiveBonus(SkillData skill, SkillLevelData levelData, BuffStatType stat)
        {
            int total = stat switch
            {
                BuffStatType.Strength => levelData.STR + levelData.AllStat,
                BuffStatType.Dexterity => levelData.DEX + levelData.AllStat,
                BuffStatType.Intelligence => levelData.INT + levelData.AllStat,
                BuffStatType.Luck => levelData.LUK + levelData.AllStat,
                BuffStatType.Attack => levelData.PAD,
                BuffStatType.AttackPercent => levelData.AttackPercent,
                BuffStatType.MagicAttack => levelData.MAD,
                BuffStatType.MagicAttackPercent => levelData.MagicAttackPercent,
                BuffStatType.Defense => levelData.PDD,
                BuffStatType.MagicDefense => levelData.MDD,
                BuffStatType.DefensePercent => levelData.DefensePercent,
                BuffStatType.MagicDefensePercent => levelData.MagicDefensePercent,
                BuffStatType.Accuracy => levelData.ACC,
                BuffStatType.AccuracyPercent => levelData.AccuracyPercent,
                BuffStatType.Avoidability => levelData.EVA,
                BuffStatType.AvoidabilityPercent => levelData.AvoidabilityPercent,
                BuffStatType.Speed => levelData.Speed,
                BuffStatType.SpeedPercent => levelData.SpeedPercent,
                BuffStatType.Jump => levelData.Jump,
                BuffStatType.MaxHPPercent => levelData.MaxHPPercent,
                BuffStatType.MaxMPPercent => levelData.MaxMPPercent,
                BuffStatType.CriticalRate => levelData.CriticalRate,
                BuffStatType.Booster => levelData.X, // Usually attack speed
                _ => 0
            };

            total += stat switch
            {
                BuffStatType.Attack => ResolveEnhancedStatBonus(skill, levelData.EnhancedPAD),
                BuffStatType.MagicAttack => ResolveEnhancedStatBonus(skill, levelData.EnhancedMAD),
                BuffStatType.Defense => ResolveEnhancedStatBonus(skill, levelData.EnhancedPDD),
                BuffStatType.MagicDefense => ResolveEnhancedStatBonus(skill, levelData.EnhancedMDD),
                BuffStatType.MaxHP => ResolveEnhancedStatBonus(skill, levelData.EnhancedMaxHP) + levelData.IndieMaxHP,
                BuffStatType.MaxMP => ResolveEnhancedStatBonus(skill, levelData.EnhancedMaxMP) + levelData.IndieMaxMP,
                _ => 0
            };

            return total;
        }

        private static int ResolveExternalAreaSupportBuffId(int areaObjectId)
        {
            return ExternalAreaSupportBuffIdBase - areaObjectId;
        }

        internal int GetActiveAbnormalStatusResistancePercent(int currentTime)
        {
            return SumActiveBuffAbnormalStatusResistance(_buffs, currentTime);
        }

        internal int GetActiveElementalResistancePercent(int currentTime)
        {
            return SumActiveBuffElementalResistance(_buffs, currentTime);
        }

        internal int GetActiveExperienceRatePercent(int currentTime)
        {
            return SumActiveBuffBonusRate(_buffs, currentTime, static levelData => levelData?.ExperienceRate ?? 0);
        }

        internal int GetActiveDropRatePercent(int currentTime)
        {
            return SumActiveBuffBonusRate(_buffs, currentTime, static levelData => levelData?.DropRate ?? 0);
        }

        internal int GetActiveMesoRatePercent(int currentTime)
        {
            return SumActiveBuffBonusRate(_buffs, currentTime, static levelData => levelData?.MesoRate ?? 0);
        }

        private static bool TryGetExternalAreaObjectId(int syntheticBuffId, out int areaObjectId)
        {
            areaObjectId = 0;
            if (syntheticBuffId >= ExternalAreaSupportBuffIdBase)
            {
                return false;
            }

            long resolvedAreaObjectId = (long)ExternalAreaSupportBuffIdBase - syntheticBuffId;
            if (resolvedAreaObjectId <= 0 || resolvedAreaObjectId > int.MaxValue)
            {
                return false;
            }

            areaObjectId = (int)resolvedAreaObjectId;
            return true;
        }

        private static SkillData BuildExternalAreaSupportBuffSkillData(
            SkillData sourceSkill,
            IReadOnlyCollection<SkillData> supportSkills,
            SkillLevelData projectedLevelData,
            int syntheticBuffId)
        {
            string mergedName = sourceSkill?.Name;
            string mergedDescription = sourceSkill?.Description ?? string.Empty;
            string mergedAffectedSkillEffect = sourceSkill?.AffectedSkillEffect;
            string mergedMinionAbility = sourceSkill?.MinionAbility;
            bool redirectsDamageToMp = sourceSkill?.RedirectsDamageToMp == true;
            bool hasMagicStealMetadata = sourceSkill?.HasMagicStealMetadata == true;
            bool reflectsIncomingDamage = sourceSkill?.ReflectsIncomingDamage == true;
            bool hasInvincibleMetadata = sourceSkill?.HasInvincibleMetadata == true;
            string zoneType = sourceSkill?.ZoneType;
            SkillAnimation avatarOverlayAnimation = null;
            SkillAnimation avatarOverlaySecondaryAnimation = null;
            SkillAnimation avatarUnderFaceAnimation = null;
            SkillAnimation avatarUnderFaceSecondaryAnimation = null;

            AssignExternalAreaSupportAvatarEffectPlanes(
                sourceSkill,
                ref avatarOverlayAnimation,
                ref avatarOverlaySecondaryAnimation,
                ref avatarUnderFaceAnimation,
                ref avatarUnderFaceSecondaryAnimation);

            if (supportSkills != null)
            {
                foreach (SkillData supportSkill in supportSkills)
                {
                    if (supportSkill == null)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(mergedName) && !string.IsNullOrWhiteSpace(supportSkill.Name))
                    {
                        mergedName = supportSkill.Name;
                    }

                    if (!string.IsNullOrWhiteSpace(supportSkill.Description)
                        && mergedDescription.IndexOf(supportSkill.Description, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        mergedDescription = string.IsNullOrWhiteSpace(mergedDescription)
                            ? supportSkill.Description
                            : $"{mergedDescription} {supportSkill.Description}";
                    }

                    if (string.IsNullOrWhiteSpace(mergedAffectedSkillEffect) && !string.IsNullOrWhiteSpace(supportSkill.AffectedSkillEffect))
                    {
                        mergedAffectedSkillEffect = supportSkill.AffectedSkillEffect;
                    }

                    if (string.IsNullOrWhiteSpace(mergedMinionAbility) && !string.IsNullOrWhiteSpace(supportSkill.MinionAbility))
                    {
                        mergedMinionAbility = supportSkill.MinionAbility;
                    }

                    redirectsDamageToMp |= supportSkill.RedirectsDamageToMp;
                    hasMagicStealMetadata |= supportSkill.HasMagicStealMetadata;
                    reflectsIncomingDamage |= supportSkill.ReflectsIncomingDamage;
                    hasInvincibleMetadata |= supportSkill.HasInvincibleMetadata;
                    if (string.IsNullOrWhiteSpace(zoneType) && !string.IsNullOrWhiteSpace(supportSkill.ZoneType))
                    {
                        zoneType = supportSkill.ZoneType;
                    }

                    AssignExternalAreaSupportAvatarEffectPlanes(
                        supportSkill,
                        ref avatarOverlayAnimation,
                        ref avatarOverlaySecondaryAnimation,
                        ref avatarUnderFaceAnimation,
                        ref avatarUnderFaceSecondaryAnimation);
                }
            }

            if (RemoteAffectedAreaSupportResolver.ResolveDerivedProjectedOutgoingDamageRate(
                    sourceSkill,
                    projectedLevelData,
                    supportSkills) > 0)
            {
                mergedMinionAbility = AppendMinionAbilityToken(mergedMinionAbility, "amplifyDamage");
            }

            return new SkillData
            {
                SkillId = syntheticBuffId,
                Name = mergedName ?? $"Remote support area {Math.Abs(syntheticBuffId)}",
                Description = mergedDescription,
                IconTexture = sourceSkill?.IconTexture,
                RedirectsDamageToMp = redirectsDamageToMp,
                HasMagicStealMetadata = hasMagicStealMetadata,
                ReflectsIncomingDamage = reflectsIncomingDamage,
                HasInvincibleMetadata = hasInvincibleMetadata,
                MinionAbility = mergedMinionAbility,
                ZoneType = zoneType,
                AffectedSkillEffect = mergedAffectedSkillEffect,
                AvatarOverlayEffect = avatarOverlayAnimation,
                AvatarOverlaySecondaryEffect = avatarOverlaySecondaryAnimation,
                AvatarUnderFaceEffect = avatarUnderFaceAnimation,
                AvatarUnderFaceSecondaryEffect = avatarUnderFaceSecondaryAnimation
            };
        }

        internal static bool ShouldRefreshProjectedSupportAvatarEffect(
            bool previouslyHadPersistentAvatarEffect,
            bool currentlyHasPersistentAvatarEffect,
            bool hasActiveEffect,
            int previousSignature,
            int nextSignature)
        {
            return currentlyHasPersistentAvatarEffect
                   && (!previouslyHadPersistentAvatarEffect
                       || !hasActiveEffect
                       || previousSignature != nextSignature);
        }

        internal static int ComputePersistentAvatarEffectSignature(SkillData skill)
        {
            if (skill?.HasPersistentAvatarEffect != true)
            {
                return 0;
            }

            var hash = new HashCode();
            AppendPersistentAvatarEffectSignature(ref hash, skill.AvatarOverlayEffect);
            AppendPersistentAvatarEffectSignature(ref hash, skill.AvatarOverlaySecondaryEffect);
            AppendPersistentAvatarEffectSignature(ref hash, skill.AvatarUnderFaceEffect);
            AppendPersistentAvatarEffectSignature(ref hash, skill.AvatarUnderFaceSecondaryEffect);
            return hash.ToHashCode();
        }

        private static void AppendPersistentAvatarEffectSignature(ref HashCode hash, SkillAnimation animation)
        {
            hash.Add(animation != null);
            if (animation == null)
            {
                return;
            }

            hash.Add(animation.Name, StringComparer.OrdinalIgnoreCase);
            hash.Add(animation.Origin);
            hash.Add(animation.ZOrder);
            hash.Add(animation.PositionCode ?? 0);
            hash.Add(animation.Loop);
            hash.Add(animation.TotalDuration);
            hash.Add(animation.Frames?.Count ?? 0);

            if (animation.Frames == null)
            {
                return;
            }

            for (int i = 0; i < animation.Frames.Count; i++)
            {
                SkillFrame frame = animation.Frames[i];
                hash.Add(frame != null);
                if (frame == null)
                {
                    continue;
                }

                hash.Add(frame.Origin);
                hash.Add(frame.Delay);
                hash.Add(frame.Bounds);
                hash.Add(frame.Flip);
                hash.Add(frame.Z);
                hash.Add(frame.AlphaStart);
                hash.Add(frame.AlphaEnd);
                hash.Add(frame.Texture != null ? RuntimeHelpers.GetHashCode(frame.Texture) : 0);
            }
        }

        private static string AppendMinionAbilityToken(string minionAbility, string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return minionAbility;
            }

            if (SummonRuntimeRules.HasMinionAbilityToken(minionAbility, token))
            {
                return minionAbility;
            }

            return string.IsNullOrWhiteSpace(minionAbility)
                ? token
                : $"{minionAbility}&&{token}";
        }

        private static void AssignExternalAreaSupportAvatarEffectPlanes(
            SkillData skill,
            ref SkillAnimation overlayAnimation,
            ref SkillAnimation overlaySecondaryAnimation,
            ref SkillAnimation underFaceAnimation,
            ref SkillAnimation underFaceSecondaryAnimation)
        {
            if (skill == null)
            {
                return;
            }

            AssignAvatarBuffEffectPlane(
                CreateLoopingAvatarEffect(skill.AffectedEffect),
                ref overlayAnimation,
                ref overlaySecondaryAnimation,
                ref underFaceAnimation,
                ref underFaceSecondaryAnimation);
            AssignAvatarBuffEffectPlane(
                CreateLoopingAvatarEffect(skill.AffectedSecondaryEffect),
                ref overlayAnimation,
                ref overlaySecondaryAnimation,
                ref underFaceAnimation,
                ref underFaceSecondaryAnimation);
        }

        internal int GetActiveDamageReductionPercent(int currentTime)
        {
            return SumActiveBuffDamageReductionRate(_buffs, currentTime);
        }

        internal int GetActiveDamageToMpRedirectPercent(int currentTime)
        {
            int redirectPercent = 0;
            foreach (ActiveBuff buff in _buffs)
            {
                if (buff == null
                    || buff.IsExpired(currentTime)
                    || buff.SkillData?.RedirectsDamageToMp != true)
                {
                    continue;
                }

                redirectPercent = Math.Max(
                    redirectPercent,
                    Math.Clamp(buff.LevelData?.X ?? 0, 0, 100));
            }

            return redirectPercent;
        }

        internal static int SumActiveBuffDamageReductionRate(IEnumerable<ActiveBuff> buffs, int currentTime)
        {
            return SumActiveBuffResistance(
                buffs,
                currentTime,
                static levelData => levelData?.DamageReductionRate ?? 0);
        }

        internal static int SumActiveBuffAbnormalStatusResistance(IEnumerable<ActiveBuff> buffs, int currentTime)
        {
            return SumActiveBuffResistance(
                buffs,
                currentTime,
                static levelData => levelData?.AbnormalStatusResistance ?? 0);
        }

        internal static int SumActiveBuffElementalResistance(IEnumerable<ActiveBuff> buffs, int currentTime)
        {
            return SumActiveBuffResistance(
                buffs,
                currentTime,
                static levelData => levelData?.ElementalResistance ?? 0);
        }

        private static int SumActiveBuffResistance(
            IEnumerable<ActiveBuff> buffs,
            int currentTime,
            Func<SkillLevelData, int> selector)
        {
            if (buffs == null || selector == null)
            {
                return 0;
            }

            int total = 0;
            foreach (ActiveBuff buff in buffs)
            {
                if (buff == null || buff.IsExpired(currentTime))
                {
                    continue;
                }

                total += Math.Max(0, selector(buff.LevelData));
            }

            return Math.Clamp(total, 0, 100);
        }

        private static int SumActiveBuffBonusRate(
            IEnumerable<ActiveBuff> buffs,
            int currentTime,
            Func<SkillLevelData, int> selector)
        {
            if (buffs == null || selector == null)
            {
                return 0;
            }

            int total = 0;
            foreach (ActiveBuff buff in buffs)
            {
                if (buff == null || buff.IsExpired(currentTime))
                {
                    continue;
                }

                total += Math.Max(0, selector(buff.LevelData));
            }

            return Math.Max(0, total);
        }

        private static bool ProjectedSupportBuffStatsMatch(SkillLevelData left, SkillLevelData right)
        {
            if (left == null || right == null)
            {
                return left == right;
            }

            return left.PAD == right.PAD
                   && left.AttackPercent == right.AttackPercent
                   && left.MAD == right.MAD
                   && left.MagicAttackPercent == right.MagicAttackPercent
                   && left.PDD == right.PDD
                   && left.MDD == right.MDD
                   && left.DefensePercent == right.DefensePercent
                   && left.MagicDefensePercent == right.MagicDefensePercent
                   && left.ACC == right.ACC
                   && left.EVA == right.EVA
                   && left.AccuracyPercent == right.AccuracyPercent
                   && left.AvoidabilityPercent == right.AvoidabilityPercent
                   && left.Speed == right.Speed
                   && left.SpeedPercent == right.SpeedPercent
                   && left.Jump == right.Jump
                   && left.STR == right.STR
                   && left.DEX == right.DEX
                   && left.INT == right.INT
                   && left.LUK == right.LUK
                   && left.CriticalRate == right.CriticalRate
                   && left.EnhancedPAD == right.EnhancedPAD
                   && left.EnhancedMAD == right.EnhancedMAD
                   && left.EnhancedPDD == right.EnhancedPDD
                   && left.EnhancedMDD == right.EnhancedMDD
                   && left.EnhancedMaxHP == right.EnhancedMaxHP
                   && left.EnhancedMaxMP == right.EnhancedMaxMP
                   && left.IndieMaxHP == right.IndieMaxHP
                   && left.IndieMaxMP == right.IndieMaxMP
                   && left.MaxHPPercent == right.MaxHPPercent
                   && left.MaxMPPercent == right.MaxMPPercent
                   && left.DefensePercent == right.DefensePercent
                   && left.MagicDefensePercent == right.MagicDefensePercent
                   && left.AccuracyPercent == right.AccuracyPercent
                   && left.AvoidabilityPercent == right.AvoidabilityPercent
                   && left.AllStat == right.AllStat
                   && left.DamageReductionRate == right.DamageReductionRate
                   && left.AbnormalStatusResistance == right.AbnormalStatusResistance
                   && left.ElementalResistance == right.ElementalResistance
                   && left.ExperienceRate == right.ExperienceRate
                   && left.DropRate == right.DropRate
                   && left.MesoRate == right.MesoRate
                   && left.BossDamageRate == right.BossDamageRate
                   && left.IgnoreDefenseRate == right.IgnoreDefenseRate
                   && left.X == right.X
                   && left.Y == right.Y
                   && left.Z == right.Z;
        }

        private int ResolveEnhancedStatBonus(SkillData skill, int amount)
        {
            if (amount <= 0)
            {
                return 0;
            }

            return !RequiresMechanicVehicleState(skill) || IsMechanicVehiclePassiveActive(skill)
                ? amount
                : 0;
        }

        private bool IsMechanicVehiclePassiveActive(SkillData skill)
        {
            if (!RequiresMechanicVehicleState(skill))
            {
                return false;
            }

            if (_player?.Build?.Equipment == null
                || !_player.Build.Equipment.TryGetValue(EquipSlot.TamingMob, out CharacterPart mountPart))
            {
                return false;
            }

            return mountPart?.Slot == EquipSlot.TamingMob
                   && mountPart.ItemId == MECHANIC_TAMING_MOB_ID;
        }

        private static bool RequiresMechanicVehicleState(SkillData skill)
        {
            if (skill == null)
            {
                return false;
            }

            string name = (skill.Name ?? string.Empty).Trim().ToLowerInvariant();
            string description = (skill.Description ?? string.Empty).Trim().ToLowerInvariant();
            return name.Contains("extreme mech")
                   || description.Contains("your mech");
        }

        public void LearnAllNonHiddenSkills()
        {
            foreach (var skill in _availableSkills)
            {
                if (skill == null || skill.Invisible || skill.SuppressesStandaloneActiveCast)
                    continue;

                SetSkillLevel(skill.SkillId, Math.Max(1, skill.MaxLevel));
            }
        }

        #endregion

        #region Update

        public void Update(int currentTime, float deltaTime)
        {
            UpdateCooldownNotifications(currentTime);
            UpdateClientSkillTimers(currentTime);
            UpdateRepeatSkillSustain(currentTime);
            UpdateSwallowState(currentTime);
            UpdateSkillZones(currentTime);
            UpdateAffectedSkillPassives(currentTime);

            // Update current cast
            if (_currentCast != null)
            {
                // Check if cast animation is complete
                var effectAnimation = _currentCast.SuppressEffectAnimation
                    ? null
                    : _currentCast.EffectAnimation ?? _currentCast.SkillData?.Effect;
                if (effectAnimation != null)
                {
                    if (effectAnimation.IsComplete(_currentCast.AnimationTime))
                    {
                        _currentCast.IsComplete = true;
                    }
                }
                else
                {
                    // No effect animation, complete after delay
                    if (_currentCast.AnimationTime > 500)
                    {
                        _currentCast.IsComplete = true;
                    }
                }
            }

            if (_preparedSkill?.IsKeydownSkill == true)
            {
                UpdateKeydownSkill(currentTime);
            }
            else if (_preparedSkill != null && _preparedSkill.Progress(currentTime) >= 1f)
            {
                ReleasePreparedSkill(currentTime);
            }

            ProcessPendingProjectileSpawns(currentTime);
            ProcessDeferredSkillPayloads(currentTime);
            UpdateRocketBooster(currentTime);
            UpdateCyclone(currentTime);
            ProcessQueuedFollowUpAttacks(currentTime);
            ProcessQueuedSerialAttack(currentTime);
            ProcessQueuedSparkAttack(currentTime);
            ProcessQueuedSummonAttacks(currentTime);
            UpdateClientOwnedVehicleTamingMobState(currentTime);
            UpdateMapFlyingRepeatAvatarEffect(currentTime);

            // Process skill queue (for macros)
            ProcessSkillQueue(currentTime);

            // Update projectiles
            UpdateProjectiles(currentTime, deltaTime);

            // Update summons
            UpdateSummons(currentTime, deltaTime);
            UpdateMine(currentTime);

            // Update hit effects (remove expired)
            UpdateHitEffects(currentTime);
            UpdateSummonTileEffects(currentTime);
        }

        private void UpdateCooldownNotifications(int currentTime)
        {
            if (_pendingCooldownCompletionNotifications.Count == 0)
            {
                return;
            }

            foreach (int skillId in _pendingCooldownCompletionNotifications.ToList())
            {
                SkillData skill = GetSkillData(skillId);
                if (skill == null || GetCooldownRemaining(skillId, currentTime) > 0)
                {
                    continue;
                }

                _pendingCooldownCompletionNotifications.Remove(skillId);
                if (_expiredAuthoritativeCooldowns.Remove(skillId))
                {
                    _serverCooldownExpireTimes.Remove(skillId);
                    _cooldowns.Remove(skillId);
                }

                OnSkillCooldownCompleted?.Invoke(skill, currentTime);
            }
        }

        private void MarkAuthoritativeCooldownExpired(int skillId)
        {
            _serverCooldownExpireTimes.Remove(skillId);
            _expiredAuthoritativeCooldowns.Add(skillId);
            _pendingCooldownCompletionNotifications.Add(skillId);
        }

        private void ClearSwallowState()
        {
            int swallowSkillId = _swallowState?.ParentSkillId ?? _swallowState?.SkillId ?? 0;
            MobItem target = GetSwallowTarget();
            target?.AI?.RemoveStatusEffect(MobStatusEffect.Stun);
            _swallowState = null;
            if (swallowSkillId > 0)
            {
                CancelClientSkillTimers(swallowSkillId, ClientTimerSourceSwallowDigest);
            }
        }

        private void RequestActiveSwallowCancel(int currentTime)
        {
            if (_swallowState == null)
            {
                return;
            }

            int skillId = _swallowState.ParentSkillId > 0 ? _swallowState.ParentSkillId : _swallowState.SkillId;
            if (skillId > 0)
            {
                RequestClientSkillCancel(skillId, currentTime);
            }
        }

        private void UpdateSwallowState(int currentTime)
        {
            _swallowAbsorbOutcomeBuffer.PruneExpired(currentTime);

            if (_swallowState == null)
            {
                return;
            }

            if (_swallowState.PendingAbsorbOutcome)
            {
                MobItem pendingTarget = GetSwallowTarget();
                if (pendingTarget?.AI == null || pendingTarget.AI.IsDead)
                {
                    RequestActiveSwallowCancel(currentTime);
                }
                else if (_swallowState.PendingAbsorbExpireTime > 0
                         && currentTime >= _swallowState.PendingAbsorbExpireTime)
                {
                    RequestActiveSwallowCancel(currentTime);
                }

                return;
            }

            if (!_swallowState.IsDigesting)
            {
                return;
            }

            MobItem target = GetSwallowTarget();
            if (target?.AI == null || target.AI.IsDead)
            {
                if (_swallowState.TargetMobId > 0)
                {
                    RequestClientSkillCancel(_swallowState.ParentSkillId > 0 ? _swallowState.ParentSkillId : _swallowState.SkillId, currentTime);
                }
                else
                {
                    ClearSwallowState();
                }
                return;
            }

            if (!IsWildHunterSwallowWriggleActionActive())
            {
                return;
            }

            _swallowState.NextWriggleTime = WildHunterSwallowParity.AdvanceWriggleSchedule(
                _swallowState.NextWriggleTime,
                _swallowState.DigestCompleteTime,
                currentTime,
                pulseTime =>
                {
                    target.AI.ApplyStatusEffect(
                        MobStatusEffect.Stun,
                        WildHunterSwallowParity.GetSuspensionDurationMs(),
                        pulseTime);
                    SpawnHitEffect(
                        GetSkillData(_swallowState.SkillId),
                        target.MovementInfo?.X ?? _player.X,
                        (target.MovementInfo?.Y ?? _player.Y) - 20f,
                        pulseTime);
                });
        }

        private void CompleteSwallowDigestFromClientTimer(int currentTime)
        {
            if (_swallowState == null)
            {
                return;
            }

            MobItem target = GetSwallowTarget();
            if (target?.AI == null || target.AI.IsDead)
            {
                RequestClientSkillCancel(_swallowState.ParentSkillId > 0 ? _swallowState.ParentSkillId : _swallowState.SkillId, currentTime);
                return;
            }

            SkillData swallowSkill = GetSkillData(_swallowState.ParentSkillId > 0 ? _swallowState.ParentSkillId : _swallowState.SkillId);
            int swallowLevel = _swallowState.Level;
            SkillData digestBuffSkill = GetSkillData(WildHunterSwallowBuffSkillId);
            int digestBuffLevel = GetSkillLevel(WildHunterSwallowBuffSkillId);
            if (digestBuffLevel <= 0)
            {
                digestBuffLevel = swallowLevel;
            }

            HandleMobDeath(target, currentTime, MobDeathType.Swallowed);
            ClearSwallowState();

            if (digestBuffSkill?.GetLevel(digestBuffLevel) != null)
            {
                ApplyBuff(digestBuffSkill, digestBuffLevel, currentTime);
            }
            else if (ShouldApplySwallowBuff(swallowSkill, swallowSkill?.GetLevel(swallowLevel)))
            {
                ApplyBuff(swallowSkill, swallowLevel, currentTime);
            }
        }

        private MobItem GetSwallowTarget()
        {
            if (_swallowState == null || _mobPool == null)
            {
                return null;
            }

            return _mobPool.ActiveMobs.FirstOrDefault(mob => mob.PoolId == _swallowState.TargetMobId);
        }

        private bool HasPendingWildHunterSwallowAbsorb()
        {
            MobItem target = GetSwallowTarget();
            return _swallowState != null
                   && _swallowState.PendingAbsorbOutcome
                   && (_swallowState.ParentSkillId == WildHunterSwallowSkillId || _swallowState.SkillId == WildHunterSwallowSkillId)
                   && target?.AI != null
                   && !target.AI.IsDead;
        }

        private bool ShouldBufferWildHunterSwallowAbsorbOutcome(int skillId)
        {
            if (skillId <= 0)
            {
                return false;
            }

            if (skillId == WildHunterSwallowSkillId
                || skillId == WildHunterSwallowBuffSkillId
                || skillId == WildHunterSwallowAttackSkillId)
            {
                return true;
            }

            SkillData swallowSkill = GetSkillData(WildHunterSwallowSkillId);
            return IsVisibleSwallowFamilyRequest(skillId, swallowSkill);
        }

        internal static bool IsVisibleSwallowFamilyRequest(int requestedSkillId, SkillData visibleSwallowSkill)
        {
            if (requestedSkillId <= 0 || visibleSwallowSkill == null)
            {
                return false;
            }

            return requestedSkillId == visibleSwallowSkill.SkillId
                   || visibleSwallowSkill.LinksDummySkill(requestedSkillId);
        }

        private bool HasConfirmedWildHunterSwallow()
        {
            MobItem target = GetSwallowTarget();
            return _swallowState != null
                   && !_swallowState.PendingAbsorbOutcome
                   && (_swallowState.ParentSkillId == WildHunterSwallowSkillId || _swallowState.SkillId == WildHunterSwallowSkillId)
                   && target?.AI != null
                   && !target.AI.IsDead;
        }

        private void UpdateSkillZones(int currentTime)
        {
            for (int i = _skillZones.Count - 1; i >= 0; i--)
            {
                if (_skillZones[i].IsExpired(currentTime))
                {
                    RemoveSkillZoneAt(i, cancelTimer: false);
                }
            }
        }

        private void StartSkillZone(
            SkillData skill,
            int level,
            SkillLevelData levelData,
            int currentTime,
            Rectangle worldBounds)
        {
            if (skill == null || levelData == null)
            {
                return;
            }

            RemoveSkillZonesBySkill(skill.SkillId, cancelTimer: true);

            int durationMs = levelData.Time > 0 ? levelData.Time * 1000 : 0;
            if (durationMs <= 0)
            {
                return;
            }

            _skillZones.Add(new ActiveSkillZone
            {
                SkillId = skill.SkillId,
                Level = level,
                StartTime = currentTime,
                Duration = durationMs,
                X = _player.X,
                Y = _player.Y,
                SkillData = skill,
                LevelData = levelData,
                Animation = ResolveInvincibleZoneAnimation(skill),
                WorldBounds = worldBounds
            });

            RegisterClientSkillTimer(
                skill.SkillId,
                ClientTimerSourceSkillZoneExpire,
                currentTime + durationMs,
                expireTime => ExpireSkillZoneFromClientTimer(skill.SkillId, expireTime));
        }

        private void ExpireSkillZoneFromClientTimer(int skillId, int currentTime)
        {
            RemoveExpiredSkillZonesBySkill(skillId, currentTime);
        }

        private void RemoveExpiredSkillZonesBySkill(int skillId, int currentTime)
        {
            for (int i = _skillZones.Count - 1; i >= 0; i--)
            {
                ActiveSkillZone zone = _skillZones[i];
                if (zone?.SkillId != skillId || !zone.IsExpired(currentTime))
                {
                    continue;
                }

                RemoveSkillZoneAt(i, cancelTimer: false);
            }
        }

        private void RemoveSkillZonesBySkill(int skillId, bool cancelTimer)
        {
            for (int i = _skillZones.Count - 1; i >= 0; i--)
            {
                if (_skillZones[i]?.SkillId != skillId)
                {
                    continue;
                }

                RemoveSkillZoneAt(i, cancelTimer);
            }
        }

        private void RemoveSkillZoneAt(int index, bool cancelTimer)
        {
            ActiveSkillZone zone = _skillZones[index];
            _skillZones.RemoveAt(index);

            if (cancelTimer && zone?.SkillId > 0)
            {
                CancelClientSkillTimers(zone.SkillId, ClientTimerSourceSkillZoneExpire);
            }
        }

        private void UpdateAffectedSkillPassives(int currentTime)
        {
            if (_mobPool == null)
            {
                _activeAffectedSkillPassives.Clear();
            }

            HashSet<int> activeSupportBuffIds = null;

            if (_player != null)
            {
                foreach (SkillData skill in _availableSkills)
                {
                    if (!TryResolveAffectedSkillPassiveRuntime(skill, out int level, out SkillLevelData levelData))
                    {
                        continue;
                    }

                    SkillData[] supportSkills = ResolveAffectedSkillPassiveSupportSkills(skill);
                    SkillLevelData effectiveLevelData = ResolveAffectedSkillPassiveSupportLevelData(levelData, supportSkills);
                    if (!UsesProjectedSupportAffectedSkillPassive(skill, effectiveLevelData, supportSkills))
                    {
                        continue;
                    }

                    int syntheticBuffId = ResolveAffectedSkillSupportBuffId(skill.SkillId);
                    ApplyOrRefreshAffectedSkillSupportBuff(skill, supportSkills, effectiveLevelData, currentTime, syntheticBuffId);
                    activeSupportBuffIds ??= new HashSet<int>();
                    activeSupportBuffIds.Add(syntheticBuffId);
                }
            }

            if (activeSupportBuffIds == null)
            {
                ClearAffectedSkillSupportBuffs(currentTime);
            }
            else
            {
                foreach (int syntheticBuffId in _affectedSkillSupportBuffIds.Where(id => !activeSupportBuffIds.Contains(id)).ToArray())
                {
                    CancelActiveBuff(syntheticBuffId, currentTime, playFinish: false);
                    _affectedSkillSupportBuffIds.Remove(syntheticBuffId);
                }
            }

            if (_mobPool == null)
            {
                return;
            }

            HashSet<int> activeSkillIds = null;

            foreach (SkillData skill in _availableSkills)
            {
                if (!UsesAuraDotAffectedSkillPassive(skill))
                {
                    continue;
                }

                if (!TryResolveAffectedSkillPassiveRuntime(skill, out int level, out _))
                {
                    _activeAffectedSkillPassives.Remove(skill.SkillId);
                    continue;
                }

                if (!_activeAffectedSkillPassives.TryGetValue(skill.SkillId, out ActiveAffectedSkillPassive passive))
                {
                    passive = new ActiveAffectedSkillPassive
                    {
                        SkillId = skill.SkillId,
                        Level = level,
                        NextTriggerTime = currentTime
                    };
                    _activeAffectedSkillPassives[skill.SkillId] = passive;
                }
                else
                {
                    passive.Level = level;
                }

                if (currentTime >= passive.NextTriggerTime)
                {
                    ExecuteAffectedAuraDotPassive(skill, level, currentTime);
                    passive.NextTriggerTime = currentTime + AuraDotTickIntervalMs;
                }

                activeSkillIds ??= new HashSet<int>();
                activeSkillIds.Add(skill.SkillId);
            }

            if (activeSkillIds == null)
            {
                _activeAffectedSkillPassives.Clear();
                return;
            }

            foreach (int skillId in _activeAffectedSkillPassives.Keys.Where(skillId => !activeSkillIds.Contains(skillId)).ToList())
            {
                _activeAffectedSkillPassives.Remove(skillId);
            }
        }

        private bool TryResolveAffectedSkillPassiveRuntime(
            SkillData skill,
            out int level,
            out SkillLevelData levelData)
        {
            level = 0;
            levelData = null;

            if (!TryResolveAffectedSkillPassiveRuntimeLevel(
                    skill,
                    GetSkillLevel,
                    _buffs,
                    out level))
            {
                return false;
            }

            levelData = skill.GetLevel(level);
            return levelData != null;
        }

        private SkillData[] ResolveAffectedSkillPassiveSupportSkills(SkillData skill)
        {
            if (skill == null)
            {
                return Array.Empty<SkillData>();
            }

            var supportSkills = new List<SkillData>();
            var visitedSkillIds = new HashSet<int>();
            CollectAffectedSkillPassiveSupportSkills(skill, supportSkills, visitedSkillIds);

            return supportSkills.ToArray();
        }

        private void CollectAffectedSkillPassiveSupportSkills(
            SkillData skill,
            ICollection<SkillData> supportSkills,
            ISet<int> visitedSkillIds)
        {
            if (skill == null)
            {
                return;
            }

            foreach (int linkedSkillId in RemoteAffectedAreaSupportResolver.EnumerateRemoteAffectedAreaLinkedSkillIds(skill))
            {
                if (linkedSkillId <= 0 || visitedSkillIds?.Add(linkedSkillId) != true)
                {
                    continue;
                }

                SkillData supportSkill = GetSkillData(linkedSkillId);
                if (supportSkill == null)
                {
                    continue;
                }

                supportSkills?.Add(supportSkill);
                CollectAffectedSkillPassiveSupportSkills(supportSkill, supportSkills, visitedSkillIds);
            }
        }

        private SkillLevelData ResolveAffectedSkillPassiveSupportLevelData(
            SkillLevelData primaryLevelData,
            params SkillData[] supportSkills)
        {
            return ResolveAffectedSkillPassiveSupportLevelData(
                primaryLevelData,
                ResolveAffectedSkillPassiveLinkedSupportLevelData,
                supportSkills);
        }

        private SkillLevelData ResolveAffectedSkillPassiveLinkedSupportLevelData(SkillData skill)
        {
            if (skill?.UsesAffectedSkillPassiveData == true
                && TryResolveAffectedSkillPassiveRuntimeLevel(
                    skill,
                    GetSkillLevel,
                    _buffs,
                    out int affectedPassiveLevel))
            {
                return skill.GetLevel(affectedPassiveLevel);
            }

            if (!TryResolveAffectedSkillPassiveLinkedSkillRuntimeLevel(
                    skill,
                    GetSkillLevel,
                    _buffs,
                    out int level))
            {
                return null;
            }

            return skill.GetLevel(level);
        }

        internal static bool TryResolveAffectedSkillPassiveRuntimeLevel(
            SkillData skill,
            Func<int, int> getSkillLevel,
            IEnumerable<ActiveBuff> activeBuffs,
            out int level)
        {
            level = 0;

            if (skill?.UsesAffectedSkillPassiveData != true || getSkillLevel == null)
            {
                return false;
            }

            if (TryResolveAffectedSkillPassiveLevel(skill, getSkillLevel(skill.SkillId), out level))
            {
                return true;
            }

            if (activeBuffs != null)
            {
                foreach (int ownerSkillId in skill.GetAffectedSkillIds())
                {
                    ActiveBuff ownerBuff = activeBuffs.LastOrDefault(buff => buff?.SkillId == ownerSkillId);
                    if (ownerBuff?.Level > 0
                        && TryResolveAffectedSkillPassiveLevel(skill, ownerBuff.Level, out level))
                    {
                        return true;
                    }
                }
            }

            foreach (int ownerSkillId in skill.GetAffectedSkillIds())
            {
                if (TryResolveAffectedSkillPassiveLevel(skill, getSkillLevel(ownerSkillId), out level))
                {
                    return true;
                }
            }

            return false;
        }

        internal static SkillLevelData ResolveAffectedSkillPassiveSupportLevelData(
            SkillLevelData primaryLevelData,
            Func<SkillData, SkillLevelData> resolveSupportLevelData,
            params SkillData[] supportSkills)
        {
            if (supportSkills == null || supportSkills.Length == 0)
            {
                return primaryLevelData;
            }

            var levelDataEntries = new List<SkillLevelData>
            {
                primaryLevelData
            };

            for (int i = 0; i < supportSkills.Length; i++)
            {
                SkillLevelData supportLevelData = resolveSupportLevelData?.Invoke(supportSkills[i]);
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

                SkillLevelData supportLevelData = resolveSupportLevelData?.Invoke(supportSkill);
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

        internal static bool TryResolveAffectedSkillPassiveLinkedSkillRuntimeLevel(
            SkillData skill,
            Func<int, int> getSkillLevel,
            IEnumerable<ActiveBuff> activeBuffs,
            out int level)
        {
            level = 0;

            if (skill == null || getSkillLevel == null)
            {
                return false;
            }

            if (activeBuffs != null)
            {
                ActiveBuff activeBuff = activeBuffs.LastOrDefault(buff => buff?.SkillId == skill.SkillId);
                if (activeBuff?.Level > 0
                    && TryResolveAffectedSkillPassiveLevel(skill, activeBuff.Level, out level))
                {
                    return true;
                }
            }

            return TryResolveAffectedSkillPassiveLevel(skill, getSkillLevel(skill.SkillId), out level);
        }

        private static bool TryResolveAffectedSkillPassiveLevel(
            SkillData skill,
            int candidateLevel,
            out int resolvedLevel)
        {
            resolvedLevel = 0;
            if (skill == null || candidateLevel <= 0)
            {
                return false;
            }

            SkillLevelData candidateLevelData = skill.GetLevel(candidateLevel);
            if (candidateLevelData == null)
            {
                return false;
            }

            resolvedLevel = candidateLevelData.Level > 0
                ? candidateLevelData.Level
                : candidateLevel;
            return true;
        }

        private void ApplyOrRefreshAffectedSkillSupportBuff(
            SkillData sourceSkill,
            IReadOnlyCollection<SkillData> supportSkills,
            SkillLevelData levelData,
            int currentTime,
            int syntheticBuffId)
        {
            SkillLevelData projectedLevelData = RemoteAffectedAreaSupportResolver.CreateProjectedSupportBuffLevelData(
                sourceSkill,
                levelData,
                supportSkills?.ToArray() ?? Array.Empty<SkillData>());
            if (projectedLevelData == null)
            {
                return;
            }

            int durationMs = Math.Max(500, Math.Max(projectedLevelData.Time * 1000, 1000));
            SkillData syntheticSkillData = BuildExternalAreaSupportBuffSkillData(sourceSkill, supportSkills, projectedLevelData, syntheticBuffId);
            ActiveBuff existingBuff = _buffs.LastOrDefault(buff => buff?.SkillId == syntheticBuffId);
            if (existingBuff != null)
            {
                bool previouslyHadPersistentAvatarEffect = existingBuff.SkillData?.HasPersistentAvatarEffect == true;
                int previousAvatarEffectSignature = ComputePersistentAvatarEffectSignature(existingBuff.SkillData);
                int nextAvatarEffectSignature = ComputePersistentAvatarEffectSignature(syntheticSkillData);
                if (!ProjectedSupportBuffStatsMatch(existingBuff.LevelData, projectedLevelData))
                {
                    ApplyBuffStats(existingBuff, false);
                    existingBuff.LevelData = projectedLevelData;
                    ApplyBuffStats(existingBuff, true);
                }

                existingBuff.StartTime = currentTime;
                existingBuff.Duration = durationMs;
                existingBuff.Level = Math.Max(1, projectedLevelData.Level);
                existingBuff.SkillData = syntheticSkillData;
                _affectedSkillSupportBuffIds.Add(syntheticBuffId);
                if (ShouldRefreshProjectedSupportAvatarEffect(
                        previouslyHadPersistentAvatarEffect,
                        syntheticSkillData.HasPersistentAvatarEffect,
                        _player.HasSkillAvatarEffect(syntheticBuffId),
                        previousAvatarEffectSignature,
                        nextAvatarEffectSignature))
                {
                    _player.ApplySkillAvatarEffect(syntheticBuffId, syntheticSkillData, currentTime);
                }
                else if (previouslyHadPersistentAvatarEffect
                         && !syntheticSkillData.HasPersistentAvatarEffect
                         && _player.HasSkillAvatarEffect(syntheticBuffId))
                {
                    _player.ClearSkillAvatarEffect(syntheticBuffId, currentTime, playFinish: false);
                }

                RefreshBuffControlledFlyingAbility();
                return;
            }

            var buff = new ActiveBuff
            {
                SkillId = syntheticBuffId,
                Level = Math.Max(1, projectedLevelData.Level),
                StartTime = currentTime,
                Duration = durationMs,
                SkillData = syntheticSkillData,
                LevelData = projectedLevelData
            };

            _buffs.Add(buff);
            _affectedSkillSupportBuffIds.Add(syntheticBuffId);
            if (syntheticSkillData.HasPersistentAvatarEffect)
            {
                _player.ApplySkillAvatarEffect(buff.SkillId, syntheticSkillData, currentTime);
            }

            OnBuffApplied?.Invoke(buff);
            ApplyBuffStats(buff, true);
            RefreshBuffControlledFlyingAbility();
        }

        private void ClearAffectedSkillSupportBuffs(int currentTime)
        {
            if (_affectedSkillSupportBuffIds.Count == 0)
            {
                return;
            }

            foreach (int syntheticBuffId in _affectedSkillSupportBuffIds.ToArray())
            {
                CancelActiveBuff(syntheticBuffId, currentTime, playFinish: false);
                _affectedSkillSupportBuffIds.Remove(syntheticBuffId);
            }
        }

        private void ExecuteAffectedAuraDotPassive(SkillData skill, int level, int currentTime)
        {
            SkillLevelData levelData = skill?.GetLevel(level);
            if (skill == null || levelData == null || _mobPool == null)
            {
                return;
            }

            Rectangle worldHitbox = GetWorldAttackHitbox(skill, level, levelData, AttackResolutionMode.Magic, _player.FacingRight);
            if (worldHitbox.IsEmpty)
            {
                return;
            }

            NotifyAttackAreaResolved(worldHitbox, currentTime, skill.SkillId);

            List<MobItem> targets = ResolveTargetsInHitbox(
                worldHitbox,
                currentTime,
                Math.Max(1, _mobPool.ActiveMobs.Count),
                AttackResolutionMode.Magic,
                _player.FacingRight,
                preferredTargetMobId: null);

            foreach (MobItem mob in targets)
            {
                if (!IsMobAttackable(mob) || mob.AI == null)
                {
                    continue;
                }

                int damage = Math.Max(1, CalculateSkillDamage(skill, level));
                damage = Math.Min(damage, Math.Max(0, mob.AI.CurrentHp - 1));
                if (damage <= 0)
                {
                    continue;
                }

                mob.ApplyDamage(damage, currentTime, isCritical: false, _player.X, _player.Y, damageType: MobDamageType.Physical);
                ApplyMobReflectDamage(mob, currentTime, MobDamageType.Physical);
                ShowSkillDamageNumber(mob, damage, isCritical: false, currentTime, hitIndex: 0);
                SpawnHitEffect(
                    skill.SkillId,
                    skill.HitEffect,
                    GetMobImpactX(mob),
                    GetMobImpactY(mob),
                    _player.FacingRight,
                    currentTime);
            }
        }

        public bool IsPlayerProtectedByClientSkillZone(int currentTime)
        {
            return IsPointInsideActiveZone(_player.X, _player.Y, currentTime, "invincible");
        }

        private bool IsPointInsideActiveZone(float worldX, float worldY, int currentTime, params string[] zoneTypes)
        {
            for (int i = 0; i < _skillZones.Count; i++)
            {
                ActiveSkillZone zone = _skillZones[i];
                if (zone.IsExpired(currentTime))
                {
                    continue;
                }

                if (zone.SkillData == null
                    || !zone.Contains(worldX, worldY)
                    || !MatchesZoneType(zone.SkillData.ZoneType, zoneTypes))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static bool MatchesZoneType(string zoneType, params string[] expectedZoneTypes)
        {
            if (string.IsNullOrWhiteSpace(zoneType) || expectedZoneTypes == null)
            {
                return false;
            }

            foreach (string expectedZoneType in expectedZoneTypes)
            {
                if (string.Equals(zoneType, expectedZoneType, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void RegisterClientSkillTimer(int skillId, string source, int expireTime, Action<int> onExpired, bool replaceExisting = true, int timerKey = 0)
        {
            if (skillId <= 0 || string.IsNullOrWhiteSpace(source) || onExpired == null)
                return;

            if (replaceExisting)
            {
                CancelClientSkillTimers(skillId, source, timerKey, matchTimerKey: timerKey != 0);
            }

            _clientSkillTimers.Add(new ClientSkillTimer
            {
                SkillId = skillId,
                Source = source,
                ExpireTime = expireTime,
                TimerKey = timerKey,
                OnExpired = onExpired
            });
        }

        private void CancelClientSkillTimers(int skillId, string source = null, int timerKey = 0, bool matchTimerKey = false)
        {
            for (int i = _clientSkillTimers.Count - 1; i >= 0; i--)
            {
                ClientSkillTimer timer = _clientSkillTimers[i];
                if (timer.SkillId != skillId)
                    continue;

                if (!string.IsNullOrWhiteSpace(source)
                    && !string.Equals(timer.Source, source, StringComparison.Ordinal))
                {
                    continue;
                }

                if (matchTimerKey && timer.TimerKey != timerKey)
                {
                    continue;
                }

                _clientSkillTimers.RemoveAt(i);
            }
        }

        private void UpdateClientSkillTimers(int currentTime)
        {
            if (_clientSkillTimers.Count == 0)
                return;

            List<ClientSkillTimer> expiredTimers = null;
            for (int i = _clientSkillTimers.Count - 1; i >= 0; i--)
            {
                ClientSkillTimer timer = _clientSkillTimers[i];
                if (timer.ExpireTime > currentTime)
                    continue;

                expiredTimers ??= new List<ClientSkillTimer>();
                expiredTimers.Add(timer);
                _clientSkillTimers.RemoveAt(i);
            }

            if (expiredTimers == null)
                return;

            ClientSkillTimer[] orderedTimers = expiredTimers
                .OrderBy(timer => timer.ExpireTime)
                .ThenBy(GetClientSkillTimerPriority)
                .ThenBy(timer => timer.SkillId)
                .ToArray();

            ClientSkillTimerExpiration[] expirations = orderedTimers
                .Select(timer => new ClientSkillTimerExpiration(timer.SkillId, timer.Source, timer.ExpireTime, timer.TimerKey))
                .ToArray();

            OnClientSkillTimersExpiredBatch?.Invoke(expirations);
            RouteExpiredLocalSummonTimerBatchToClientCancel(
                expirations,
                static (_, _) => { },
                ResolveClientCancelRequestSkillIds,
                (skillId, tickCount) => RequestClientSkillCancel(skillId, tickCount));

            foreach (ClientSkillTimer timer in orderedTimers)
            {
                OnClientSkillTimerExpired?.Invoke(timer.SkillId, timer.Source);
                if (string.Equals(timer.Source, ClientTimerSourceSummonExpire, StringComparison.Ordinal)
                    && timer.TimerKey > 0)
                {
                    continue;
                }

                timer.OnExpired(currentTime);
            }
        }

        private static int GetClientSkillTimerPriority(ClientSkillTimer timer)
        {
            if (timer == null)
                return 0;

            return string.Equals(timer.Source, ClientTimerSourceRepeatSustainEnd, StringComparison.Ordinal)
                ? 0
                : string.Equals(timer.Source, ClientTimerSourceRepeatModeEndAck, StringComparison.Ordinal)
                    ? 1
                    : 2;
        }

        private void UpdateHitEffects(int currentTime)
        {
            for (int i = _hitEffects.Count - 1; i >= 0; i--)
            {
                if (_hitEffects[i].IsExpired(currentTime))
                {
                    _hitEffects.RemoveAt(i);
                }
            }
        }

        private void ProcessQueuedFollowUpAttacks(int currentTime)
        {
            while (_queuedFollowUpAttacks.Count > 0)
            {
                if (_queuedFollowUpAttacks.Peek().ExecuteTime > currentTime)
                    return;

                if ((_currentCast != null && !_currentCast.IsComplete) || _preparedSkill != null)
                    return;

                ExecuteQueuedFollowUpAttack(_queuedFollowUpAttacks.Dequeue(), currentTime);
            }
        }

        private void ProcessQueuedSparkAttack(int currentTime)
        {
            if (_queuedSparkAttack == null || _queuedSparkAttack.ExecuteTime > currentTime)
                return;

            if ((_currentCast != null && !_currentCast.IsComplete) || _preparedSkill != null)
                return;

            QueuedSparkAttack queuedAttack = _queuedSparkAttack;
            _queuedSparkAttack = null;
            ExecuteQueuedSparkAttack(queuedAttack, currentTime);
        }

        private void ProcessQueuedSerialAttack(int currentTime)
        {
            if (_queuedSerialAttack == null || _queuedSerialAttack.ExecuteTime > currentTime)
                return;

            if ((_currentCast != null && !_currentCast.IsComplete) || _preparedSkill != null)
                return;

            QueuedSerialAttack queuedAttack = _queuedSerialAttack;
            _queuedSerialAttack = null;
            ExecuteQueuedSerialAttack(queuedAttack, currentTime);
        }

        private void ProcessQueuedSummonAttacks(int currentTime)
        {
            QueuedSummonAttack[] dueAttacks = _queuedSummonAttacks
                .Where(queuedAttack => queuedAttack != null && queuedAttack.ExecuteTime <= currentTime)
                .OrderBy(queuedAttack => queuedAttack.ExecuteTime)
                .ThenBy(queuedAttack => queuedAttack.AttackStartedAt)
                .ThenBy(queuedAttack => queuedAttack.TargetOrderOffset)
                .ThenBy(queuedAttack => queuedAttack.SequenceId)
                .ToArray();
            if (dueAttacks.Length == 0)
            {
                return;
            }

            _queuedSummonAttacks.RemoveAll(queuedAttack => queuedAttack != null && queuedAttack.ExecuteTime <= currentTime);
            int? activeSummonObjectId = null;
            int? activeAttackStartedAt = null;
            bool tileOverlayRegisteredForAttack = false;
            foreach (QueuedSummonAttack queuedAttack in dueAttacks)
            {
                bool startsNewAttackCycle = activeSummonObjectId != queuedAttack.SummonObjectId
                                            || activeAttackStartedAt != queuedAttack.AttackStartedAt;
                if (startsNewAttackCycle)
                {
                    activeSummonObjectId = queuedAttack.SummonObjectId;
                    activeAttackStartedAt = queuedAttack.AttackStartedAt;
                    tileOverlayRegisteredForAttack = false;
                }

                ResolveQueuedSummonAttack(queuedAttack, currentTime, ref tileOverlayRegisteredForAttack);
            }
        }

        private void ExecuteQueuedFollowUpAttack(QueuedFollowUpAttack queuedAttack, int currentTime)
        {
            if (queuedAttack == null)
                return;

            if (!TryResolveQueuedExecutionSkillData(queuedAttack.SkillId, out SkillData skill, out int level, out SkillLevelData levelData))
                return;

            if (!DoesQueuedFollowUpWeaponMatch(queuedAttack.RequiredWeaponCode, GetEquippedWeaponCode()))
                return;

            if (!TryRefreshQueuedShootAttackExecutionSelection(
                    skill,
                    level,
                    queuedAttack.ResolvedShootAmmoSelection,
                    queuedAttack.ShootAmmoBypassActive,
                    out ShootAmmoSelection refreshedSelection))
            {
                return;
            }

            ExecuteQueuedShootAttackPath(refreshedSelection, queuedAttack.ShootAmmoBypassActive, () =>
            {
                BeginQueuedFollowUpCast(
                    skill,
                    level,
                    levelData,
                    currentTime,
                    queuedAttack.FacingRight);
                Vector2? attackOrigin = RequiresClientShootAttackValidation(skill)
                    ? ResolveCurrentShootAttackOrigin(currentTime, queuedAttack.FacingRight)
                    : null;
                ExecuteSkillPayload(
                    skill,
                    level,
                    currentTime,
                    queueFollowUps: false,
                    preferredTargetMobId: queuedAttack.TargetMobId,
                    preferredTargetPositionOverride: queuedAttack.TargetPosition,
                    revalidateDeferredExecutionSkillLevel: true,
                    isQueuedFinalAttack: true,
                    facingRightOverride: queuedAttack.FacingRight,
                    attackOriginOverride: attackOrigin,
                    shootRange0Override: queuedAttack.ShootRange0);
            });
        }

        private void ExecuteQueuedSparkAttack(QueuedSparkAttack queuedAttack, int currentTime)
        {
            if (queuedAttack == null)
                return;

            if (!TryResolveQueuedExecutionSkillData(queuedAttack.SkillId, out SkillData skill, out int level, out SkillLevelData levelData)
                || !IsAttackTriggeredChainBuffSkill(skill, level))
                return;

            if (!_buffs.Any(buff => buff.SkillId == queuedAttack.SkillId))
                return;

            if (!DoesQueuedFollowUpWeaponMatch(queuedAttack.RequiredWeaponCode, GetEquippedWeaponCode()))
                return;

            if (!TryRefreshQueuedShootAttackExecutionSelection(
                    skill,
                    level,
                    queuedAttack.ResolvedShootAmmoSelection,
                    queuedAttack.ShootAmmoBypassActive,
                    out ShootAmmoSelection refreshedSelection))
            {
                return;
            }

            Vector2 attackOrigin = ResolveQueuedSparkAttackOrigin(queuedAttack, currentTime);
            ExecuteQueuedShootAttackPath(refreshedSelection, queuedAttack.ShootAmmoBypassActive, () =>
            {
                BeginQueuedFollowUpCast(skill, level, levelData, currentTime, queuedAttack.FacingRight);
                ExecuteAttackPayload(
                    skill,
                    level,
                    currentTime,
                    queueFollowUps: false,
                    preferredTargetMobId: queuedAttack.SourceMobId,
                    preferredTargetPositionOverride: queuedAttack.PreferredTargetPosition,
                    allowDeferredExecution: true,
                    revalidateDeferredExecutionSkillLevel: true,
                    isQueuedSparkAttack: true,
                    facingRightOverride: queuedAttack.FacingRight,
                    attackOriginOverride: attackOrigin);
            });
        }

        private void ExecuteQueuedShootAttackPath(
            ShootAmmoSelection resolvedShootAmmoSelection,
            bool shootAmmoBypassActive,
            Action execute)
        {
            ShootAmmoSelection previousResolvedShootAmmoSelection = LastResolvedShootAmmoSelection?.Snapshot();
            bool? previousShootAmmoBypassOverride = _shootAmmoBypassTemporaryStatOverride;
            try
            {
                LastResolvedShootAmmoSelection = resolvedShootAmmoSelection?.Snapshot();
                _shootAmmoBypassTemporaryStatOverride = shootAmmoBypassActive;
                execute?.Invoke();
            }
            finally
            {
                LastResolvedShootAmmoSelection = previousResolvedShootAmmoSelection;
                _shootAmmoBypassTemporaryStatOverride = previousShootAmmoBypassOverride;
            }
        }

        private bool TryRefreshQueuedShootAttackExecutionSelection(
            SkillData skill,
            int level,
            ShootAmmoSelection queuedSelection,
            bool shootAmmoBypassActive,
            out ShootAmmoSelection refreshedSelection)
        {
            refreshedSelection = queuedSelection?.Snapshot();

            if (skill == null
                || !RequiresClientShootAttackValidation(skill)
                || IsShootSkillNotUsingShootingWeapon(skill.SkillId)
                || IsShootSkillNotConsumingBullet(skill.SkillId)
                || _inventoryRuntime == null
                || _player?.Build == null)
            {
                return true;
            }

            int weaponCode = GetEquippedWeaponCode();
            int weaponItemId = _player.Build.GetWeapon()?.ItemId ?? 0;
            if (weaponCode <= 0 || weaponItemId <= 0)
            {
                refreshedSelection = null;
                return false;
            }

            SkillLevelData levelData = skill.GetLevel(level);
            int requiredAmmoCount = ResolveRequiredShootAmmoCount(levelData);
            int requiredAmmoItemId = ResolveSkillSpecificShootAmmoItemId(skill.SkillId, levelData);
            return ClientShootAmmoResolver.TryRefreshQueuedSelectionForExecution(
                queuedSelection,
                _inventoryRuntime.GetSlots(InventoryType.USE),
                _inventoryRuntime.GetSlots(InventoryType.CASH),
                weaponCode,
                weaponItemId,
                requiredAmmoCount,
                requiredAmmoItemId,
                requiresUseAmmo: true,
                out refreshedSelection);
        }

        private bool TryRefreshDeferredQueuedShootAttackExecutionSelection(
            DeferredSkillPayload pending,
            int level,
            out ShootAmmoSelection refreshedSelection)
        {
            refreshedSelection = pending?.ResolvedShootAmmoSelection?.Snapshot();
            if (pending == null || (!pending.IsQueuedFinalAttack && !pending.IsQueuedSparkAttack))
            {
                return true;
            }

            return TryRefreshQueuedShootAttackExecutionSelection(
                pending.Skill,
                level,
                pending.ResolvedShootAmmoSelection,
                pending.ShootAmmoBypassActive,
                out refreshedSelection);
        }

        private void ResolveQueuedSummonAttack(
            QueuedSummonAttack queuedAttack,
            int currentTime,
            ref bool tileOverlayRegisteredForAttack)
        {
            if (_mobPool?.ActiveMobs == null
                || queuedAttack?.SkillData == null
                || queuedAttack.LevelData == null
                || queuedAttack.TargetMobIds == null
                || queuedAttack.TargetMobIds.Length == 0)
            {
                return;
            }

            List<MobItem> mobsToKill = new();
            int targetOrder = Math.Max(0, queuedAttack.TargetOrderOffset);
            foreach (int mobId in queuedAttack.TargetMobIds)
            {
                MobItem mob = FindAttackableMobByPoolId(mobId, currentTime);
                if (mob == null)
                {
                    continue;
                }

                bool died = ApplySummonAttackToMob(
                    queuedAttack.SkillData,
                    queuedAttack.LevelData,
                    queuedAttack.Level,
                    queuedAttack.FacingRight,
                    mob,
                    currentTime,
                    queuedAttack.AttackCount,
                    queuedAttack.Origin,
                    queuedAttack.DamagePercentOverride,
                    targetOrder,
                    out bool attackApplied);
                if (attackApplied)
                {
                    if (!tileOverlayRegisteredForAttack)
                    {
                        TryRegisterLocalClientOwnedAttackTileOverlay(queuedAttack, mob, targetOrder, currentTime);
                        tileOverlayRegisteredForAttack = true;
                    }

                    TryRegisterLocalClientOwnedReactiveAttackChainEffect(queuedAttack, mob, currentTime);
                }

                if (died)
                {
                    mobsToKill.Add(mob);
                }

                targetOrder++;
            }

            foreach (MobItem mob in mobsToKill)
            {
                HandleMobDeath(mob, currentTime);
            }

            UpdateSg88ManualAttackBookkeepingAfterBatch(queuedAttack, currentTime);
        }

        private void TryRegisterLocalClientOwnedAttackTileOverlay(
            QueuedSummonAttack queuedAttack,
            MobItem target,
            int targetOrder,
            int currentTime)
        {
            if (queuedAttack?.SkillData == null
                || target == null
                || !SummonClientPostEffectRules.ShouldRegisterAttackTileOverlay(
                    queuedAttack.SkillId,
                    queuedAttack.SkillData.ZoneAnimation))
            {
                return;
            }

            Vector2 targetAnchor = ResolveSummonImpactDisplayPosition(
                queuedAttack.SkillData,
                targetOrder,
                queuedAttack.AttackBranchName,
                target,
                queuedAttack.Origin,
                currentTime);
            Rectangle area = SummonClientPostEffectRules.BuildAttackTileOverlayArea(
                targetAnchor,
                queuedAttack.SkillData,
                queuedAttack.AttackBranchName);
            if (area.Width <= 0 || area.Height <= 0)
            {
                return;
            }

            const int tileDelayMs = 200;
            const int tileDurationMs = 500;
            int attackDelayMs = SummonClientPostEffectRules.ResolvePostAttackEffectDelayMs(
                queuedAttack.SkillData,
                queuedAttack.AttackBranchName);
            int queuedImpactDelayMs = Math.Max(0, currentTime - queuedAttack.AttackStartedAt);
            int remainingDelayMs = Math.Max(0, attackDelayMs - queuedImpactDelayMs);
            _summonTileEffects.Add(new LocalSummonTileEffectDisplay
            {
                Animation = queuedAttack.SkillData.ZoneAnimation,
                Area = area,
                StartTime = currentTime + remainingDelayMs + tileDelayMs,
                EndTime = currentTime + remainingDelayMs + tileDelayMs + tileDurationMs,
                StartAlpha = 128,
                EndAlpha = byte.MaxValue
            });
        }

        private void TryRegisterLocalClientOwnedReactiveAttackChainEffect(
            QueuedSummonAttack queuedAttack,
            MobItem target,
            int currentTime)
        {
            if (_animationEffects == null
                || queuedAttack?.SkillData == null
                || target == null
                || !SummonClientPostEffectRules.ShouldRegisterReactiveAttackChainEffect(
                    queuedAttack.SkillId,
                    queuedAttack.SkillData))
            {
                return;
            }

            Rectangle targetHitbox = GetMobHitbox(target, currentTime);
            (Vector2 source, Vector2 chainTarget) = SummonClientPostEffectRules.ResolveReactiveAttackChainEndpoints(
                queuedAttack.Origin,
                targetHitbox,
                queuedAttack.FacingRight);
            const int chainDurationMs = 270;
            _animationEffects.AddBlueLightning(source, chainTarget, chainDurationMs, currentTime);
        }

        public bool TryResolvePendingSg88ManualAttackRequest(int summonObjectId, int requestedAt, int currentTime)
        {
            ActiveSummon summon = _summons.FirstOrDefault(candidate =>
                candidate?.ObjectId == summonObjectId
                && candidate.SkillId == SG88_SKILL_ID
                && candidate.AssistType == SummonAssistType.ManualAttack
                && !candidate.IsPendingRemoval);

            return TryResolvePendingSg88ManualAttackRequestBookkeeping(summon, requestedAt, currentTime);
        }

        public bool TryResolvePendingSg88ManualAttackPacket(
            int summonObjectId,
            IReadOnlyList<int> packetTargetMobIds,
            int currentTime)
        {
            return TryResolvePendingSg88ManualAttackPacket(
                summonObjectId,
                packetTargetMobIds,
                currentTime,
                out _);
        }

        public bool TryResolvePendingSg88ManualAttackPacket(
            int summonObjectId,
            IReadOnlyList<int> packetTargetMobIds,
            int currentTime,
            out int resolvedRequestedAt)
        {
            resolvedRequestedAt = int.MinValue;
            ActiveSummon summon = _summons.FirstOrDefault(candidate =>
                candidate?.ObjectId == summonObjectId
                && candidate.SkillId == SG88_SKILL_ID
                && candidate.AssistType == SummonAssistType.ManualAttack
                && !candidate.IsPendingRemoval);
            if (summon == null || !summon.PendingManualAttackRequest)
            {
                return false;
            }

            bool shouldResolve = TryAdvancePendingSg88ManualAttackPacketConfirmation(
                summon,
                packetTargetMobIds,
                out int[] confirmedTargetMobIds);
            summon.PendingManualAttackConfirmedTargetMobIds = confirmedTargetMobIds;
            if (!shouldResolve)
            {
                return false;
            }

            summon.LastManualAttackResolvedTime = currentTime;
            resolvedRequestedAt = summon.PendingManualAttackRequestedAt;
            ClearPendingSg88ManualAttackRequestBookkeeping(summon);
            return true;
        }

        internal static bool TryAdvancePendingSg88ManualAttackPacketConfirmation(
            ActiveSummon summon,
            IReadOnlyList<int> packetTargetMobIds,
            out int[] confirmedTargetMobIds)
        {
            confirmedTargetMobIds = Array.Empty<int>();
            if (summon == null || !summon.PendingManualAttackRequest)
            {
                return false;
            }

            int[] expectedTargetMobIds = summon.PendingManualAttackTargetMobIds?
                .Where(static mobId => mobId > 0)
                .Distinct()
                .ToArray() ?? Array.Empty<int>();
            if (expectedTargetMobIds.Length == 0)
            {
                return false;
            }

            HashSet<int> confirmedTargetSet = new(
                summon.PendingManualAttackConfirmedTargetMobIds?
                    .Where(static mobId => mobId > 0)
                    .Distinct()
                    ?? Enumerable.Empty<int>());

            foreach (int mobId in packetTargetMobIds ?? Array.Empty<int>())
            {
                if (mobId > 0 && Array.IndexOf(expectedTargetMobIds, mobId) >= 0)
                {
                    confirmedTargetSet.Add(mobId);
                }
            }

            confirmedTargetMobIds = expectedTargetMobIds
                .Where(confirmedTargetSet.Contains)
                .ToArray();
            return confirmedTargetMobIds.Length == expectedTargetMobIds.Length;
        }

        private void UpdateSg88ManualAttackBookkeepingAfterBatch(QueuedSummonAttack queuedAttack, int currentTime)
        {
            if (queuedAttack == null
                || !queuedAttack.IsSg88ManualAttackBatch
                || queuedAttack.SummonObjectId <= 0)
            {
                return;
            }

            ActiveSummon summon = _summons.FirstOrDefault(candidate =>
                candidate?.ObjectId == queuedAttack.SummonObjectId
                && candidate.SkillId == SG88_SKILL_ID
                && candidate.AssistType == SummonAssistType.ManualAttack
                && !candidate.IsPendingRemoval);
            if (summon == null || !summon.PendingManualAttackRequest)
            {
                return;
            }

            if (queuedAttack.Sg88ManualRequestTime != int.MinValue
                && summon.PendingManualAttackRequestedAt != queuedAttack.Sg88ManualRequestTime)
            {
                return;
            }

            bool shouldResolveRequest = queuedAttack.IsSg88ManualFollowUpBatch
                || summon.PendingManualAttackFollowUpAt == int.MinValue
                || currentTime >= summon.PendingManualAttackFollowUpAt;
            if (shouldResolveRequest)
            {
                TryResolvePendingSg88ManualAttackRequestBookkeeping(summon, queuedAttack.Sg88ManualRequestTime, currentTime);
            }
        }

        private void ExecuteQueuedSerialAttack(QueuedSerialAttack queuedAttack, int currentTime)
        {
            if (queuedAttack == null)
                return;

            if (!TryResolveQueuedExecutionSkillData(queuedAttack.SkillId, out SkillData skill, out int level, out SkillLevelData levelData)
                || !ShouldUseQueuedSerialAttack(skill))
                return;

            if (!DoesQueuedFollowUpWeaponMatch(queuedAttack.RequiredWeaponCode, GetEquippedWeaponCode()))
                return;

            Vector2 attackOrigin = ResolveQueuedSerialAttackOrigin(queuedAttack, currentTime);
            List<MobItem> targets = ResolveQueuedSerialTargets(queuedAttack, levelData, currentTime);
            if (targets.Count == 0)
                return;

            BeginQueuedFollowUpCast(skill, level, levelData, currentTime, queuedAttack.FacingRight);

            AttackResolutionMode mode = GetAttackResolutionMode(skill);
            int attackCount = Math.Max(1, levelData.AttackCount);
            List<MobItem> mobsToKill = new();
            for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
            {
                MobItem mob = targets[targetIndex];
                int deferredTargetOrder = ResolveDeferredSerialTargetOrder(targetIndex);
                bool died = ApplySkillAttackToMob(
                    skill,
                    level,
                    levelData,
                    mob,
                    currentTime,
                    attackCount,
                    mode,
                    skill.HitEffect,
                    queuedAttack.FacingRight,
                    deferredTargetOrder);

                if (died && !mobsToKill.Contains(mob))
                {
                    mobsToKill.Add(mob);
                }
            }

            foreach (MobItem mob in mobsToKill)
            {
                HandleMobDeath(mob, currentTime);
            }

            return;
        }

        private static int ResolveDeferredSerialTargetOrder(int targetIndex)
        {
            // Deferred serial branches begin after the opener has already consumed target order 0.
            return Math.Max(0, targetIndex + 1);
        }

        #endregion

        #region Draw

        public void DrawProjectiles(SpriteBatch spriteBatch, int mapShiftX, int mapShiftY,
            int centerX, int centerY, int currentTime)
        {
            foreach (var proj in _projectiles)
            {
                DrawProjectile(spriteBatch, proj, mapShiftX, mapShiftY, centerX, centerY, currentTime);
            }
        }

        private void DrawProjectile(SpriteBatch spriteBatch, ActiveProjectile proj,
            int mapShiftX, int mapShiftY, int centerX, int centerY, int currentTime)
        {
            SkillAnimation anim;
            int animTime;

            if (proj.IsExploding)
            {
                anim = proj.Data.ExplosionAnimation;
                animTime = currentTime - proj.ExplodeTime;
            }
            else
            {
                anim = proj.Data.Animation;
                animTime = currentTime - proj.SpawnTime;
            }

            SkillAnimation resolvedAmmoVisual = ResolveProjectileVisualAnimation(proj);
            if (resolvedAmmoVisual?.Frames?.Count > 0)
            {
                anim = resolvedAmmoVisual;
            }

            if (anim == null)
                return;

            var frame = anim.GetFrameAtTime(animTime);
            if (frame?.Texture == null)
                return;

            int screenX = (int)proj.X - mapShiftX + centerX;
            int screenY = (int)proj.Y - mapShiftY + centerY;

            bool shouldFlip = ResolveProjectileFrameShouldFlip(proj, frame);

            frame.Texture.DrawBackground(spriteBatch, null, null,
                GetFrameDrawX(screenX, frame, shouldFlip), screenY - frame.Origin.Y,
                Color.White, shouldFlip, null);
        }

        private SkillAnimation ResolveProjectileVisualAnimation(ActiveProjectile projectile)
        {
            int visualItemId = ResolveProjectileVisualItemId(projectile);
            return visualItemId > 0 ? _loader?.LoadItemBulletAnimation(visualItemId) : null;
        }

        internal static int ResolveProjectileVisualItemId(ActiveProjectile projectile)
        {
            if (projectile == null || projectile.IsExploding)
            {
                return 0;
            }

            bool usesQueuedFollowUpVisual = projectile.IsQueuedFinalAttack || projectile.IsQueuedSparkAttack;
            bool lacksAuthoredProjectileVisual = projectile.Data?.Animation?.Frames?.Count <= 0;
            if (!usesQueuedFollowUpVisual && !lacksAuthoredProjectileVisual)
            {
                return 0;
            }

            ShootAmmoSelection selection = projectile.ResolvedShootAmmoSelection;
            if (usesQueuedFollowUpVisual)
            {
                return ResolveQueuedProjectileVisualItemId(selection);
            }

            if (selection?.CashItemId > 0)
            {
                return selection.CashItemId;
            }

            return selection?.UseItemId ?? 0;
        }

        internal static int ResolveQueuedProjectileVisualItemId(ShootAmmoSelection selection)
        {
            if (selection == null)
            {
                return 0;
            }

            // `GetProperBulletPosition` re-resolves the live CASH lane at fire time. Once that
            // slot refresh fails, the delayed queued branch falls back to the preserved USE art.
            if (selection.HasCashAmmo)
            {
                return selection.CashItemId;
            }

            if (selection.HasUseAmmo)
            {
                return selection.UseItemId;
            }

            if (selection.UseItemId > 0)
            {
                return selection.UseItemId;
            }

            return selection.CashItemId > 0 ? selection.CashItemId : 0;
        }

        public void DrawBackgroundEffects(SpriteBatch spriteBatch, int mapShiftX, int mapShiftY,
            int centerX, int centerY, int currentTime)
        {
            if (_currentCast != null && !_currentCast.IsComplete)
            {
                DrawCastEffects(spriteBatch, _currentCast, mapShiftX, mapShiftY, centerX, centerY, drawBackground: true);
            }
        }

        public void DrawEffects(SpriteBatch spriteBatch, int mapShiftX, int mapShiftY,
            int centerX, int centerY, int currentTime)
        {
            if (_currentCast != null && !_currentCast.IsComplete)
            {
                DrawCastEffects(spriteBatch, _currentCast, mapShiftX, mapShiftY, centerX, centerY, drawBackground: false);
            }

            // Draw affected effects for active buffs (looping on character)
            foreach (var buff in _buffs)
            {
                DrawAffectedEffect(spriteBatch, buff, mapShiftX, mapShiftY, centerX, centerY, currentTime);
            }

            foreach (var zone in _skillZones)
            {
                DrawSkillZoneEffect(spriteBatch, zone, mapShiftX, mapShiftY, centerX, centerY, currentTime);
            }

            foreach (var summon in _summons)
            {
                DrawSummonEffect(spriteBatch, summon, mapShiftX, mapShiftY, centerX, centerY, currentTime);
            }

            foreach (var tileEffect in _summonTileEffects)
            {
                DrawSummonTileEffect(spriteBatch, tileEffect, mapShiftX, mapShiftY, centerX, centerY, currentTime);
            }

            // Draw hit effects
            foreach (var hitEffect in _hitEffects)
            {
                DrawHitEffect(spriteBatch, hitEffect, mapShiftX, mapShiftY, centerX, centerY, currentTime);
            }
        }

        /// <summary>
        /// Draw a hit effect at its position
        /// </summary>
        private void DrawHitEffect(SpriteBatch spriteBatch, ActiveHitEffect hitEffect,
            int mapShiftX, int mapShiftY, int centerX, int centerY, int currentTime)
        {
            if (hitEffect.Animation == null)
                return;

            var frame = hitEffect.Animation.GetFrameAtTime(hitEffect.AnimationTime(currentTime));
            if (frame?.Texture == null)
                return;

            int screenX = (int)hitEffect.X - mapShiftX + centerX;
            int screenY = (int)hitEffect.Y - mapShiftY + centerY;

            bool shouldFlip = hitEffect.FacingRight ^ frame.Flip;

            frame.Texture.DrawBackground(spriteBatch, null, null,
                GetFrameDrawX(screenX, frame, shouldFlip), screenY - frame.Origin.Y,
                Color.White, shouldFlip, null);
        }

        private void DrawSummonTileEffect(
            SpriteBatch spriteBatch,
            LocalSummonTileEffectDisplay tileEffect,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int currentTime)
        {
            if (tileEffect?.Animation?.Frames.Count <= 0 || !tileEffect.IsActive(currentTime))
            {
                return;
            }

            SkillFrame frame = tileEffect.Animation.GetFrameAtTime(Math.Max(0, currentTime - tileEffect.StartTime));
            if (frame?.Texture == null)
            {
                return;
            }

            int tileWidth = Math.Max(1, frame.Texture.Width);
            int tileHeight = Math.Max(1, frame.Texture.Height);
            bool shouldFlip = frame.Flip;
            Color tint = Color.White * tileEffect.GetAlpha(currentTime);

            for (int worldY = tileEffect.Area.Top; worldY < tileEffect.Area.Bottom; worldY += tileHeight)
            {
                for (int worldX = tileEffect.Area.Left; worldX < tileEffect.Area.Right; worldX += tileWidth)
                {
                    int screenX = worldX - mapShiftX + centerX;
                    int screenY = worldY - mapShiftY + centerY;
                    frame.Texture.DrawBackground(spriteBatch, null, null,
                        GetFrameDrawX(screenX, frame, shouldFlip), screenY - frame.Origin.Y,
                        tint, shouldFlip, null);
                }
            }
        }

        private void DrawSkillZoneEffect(SpriteBatch spriteBatch, ActiveSkillZone zone,
            int mapShiftX, int mapShiftY, int centerX, int centerY, int currentTime)
        {
            SkillFrame frame = zone?.Animation?.GetFrameAtTime(zone.AnimationTime(currentTime));
            if (frame?.Texture == null)
            {
                return;
            }

            int tileWidth = Math.Max(1, frame.Bounds.Width);
            int tileHeight = Math.Max(1, frame.Bounds.Height);
            int columns = Math.Max(1, (int)Math.Ceiling(zone.WorldBounds.Width / (float)tileWidth));
            int rows = Math.Max(1, (int)Math.Ceiling(zone.WorldBounds.Height / (float)tileHeight));
            int startX = zone.WorldBounds.Left + tileWidth / 2;
            int startY = zone.WorldBounds.Top + tileHeight / 2;

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    int worldX = startX + (column * tileWidth);
                    int worldY = startY + (row * tileHeight);
                    int screenX = worldX - mapShiftX + centerX;
                    int screenY = worldY - mapShiftY + centerY;
                    bool shouldFlip = frame.Flip;

                    frame.Texture.DrawBackground(spriteBatch, null, null,
                        GetFrameDrawX(screenX, frame, shouldFlip), screenY - frame.Origin.Y,
                        Color.White, shouldFlip, null);
                }
            }
        }

        private void DrawCastEffects(SpriteBatch spriteBatch, SkillCastInfo cast,
            int mapShiftX, int mapShiftY, int centerX, int centerY, bool drawBackground)
        {
            if (cast?.SuppressEffectAnimation == true)
                return;

            int screenX = (int)cast.CasterX - mapShiftX + centerX;
            int screenY = (int)cast.CasterY - mapShiftY + centerY;

            foreach (SkillAnimation effect in EnumerateCastEffects(cast))
            {
                if (effect == null || (effect.ZOrder < 0) != drawBackground)
                {
                    continue;
                }

                SkillFrame frame = effect.GetFrameAtTime(cast.AnimationTime);
                if (frame?.Texture == null)
                {
                    continue;
                }

                bool shouldFlip = cast.FacingRight ^ frame.Flip;
                frame.Texture.DrawBackground(spriteBatch, null, null,
                    GetFrameDrawX(screenX, frame, shouldFlip), screenY - frame.Origin.Y,
                    Color.White, shouldFlip, null);
            }
        }

        private static IEnumerable<SkillAnimation> EnumerateCastEffects(SkillCastInfo cast)
        {
            if (cast?.EffectAnimation != null)
            {
                yield return cast.EffectAnimation;
            }

            if (cast?.SecondaryEffectAnimation != null)
            {
                yield return cast.SecondaryEffectAnimation;
            }
        }

        /// <summary>
        /// Draw affected effect for active buff (loops while buff is active)
        /// </summary>
        private void DrawAffectedEffect(SpriteBatch spriteBatch, ActiveBuff buff,
            int mapShiftX, int mapShiftY, int centerX, int centerY, int currentTime)
        {
            DrawAffectedAnimation(
                spriteBatch,
                buff.SkillData?.AffectedEffect,
                buff.StartTime,
                mapShiftX,
                mapShiftY,
                centerX,
                centerY,
                currentTime);
            DrawAffectedAnimation(
                spriteBatch,
                buff.SkillData?.AffectedSecondaryEffect,
                buff.StartTime,
                mapShiftX,
                mapShiftY,
                centerX,
                centerY,
                currentTime);
        }

        private void DrawAffectedAnimation(
            SpriteBatch spriteBatch,
            SkillAnimation affected,
            int startTime,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int currentTime)
        {
            if (affected == null)
            {
                return;
            }

            int animTime = currentTime - startTime;
            SkillFrame frame = affected.GetFrameAtTime(animTime);
            if (frame?.Texture == null)
            {
                return;
            }

            int screenX = (int)_player.X - mapShiftX + centerX;
            int screenY = (int)_player.Y - mapShiftY + centerY;
            bool shouldFlip = _player.FacingRight ^ frame.Flip;

            frame.Texture.DrawBackground(spriteBatch, null, null,
                GetFrameDrawX(screenX, frame, shouldFlip), screenY - frame.Origin.Y,
                Color.White, shouldFlip, null);
        }

        private void DrawSummonEffect(SpriteBatch spriteBatch, ActiveSummon summon,
            int mapShiftX, int mapShiftY, int centerX, int centerY, int currentTime)
        {
            int elapsed = currentTime - summon.StartTime;
            var animation = ResolveSummonAnimation(summon, currentTime, elapsed, out int animationTime)
                ?? summon.SkillData?.AffectedEffect
                ?? summon.SkillData?.Effect;
            if (animation == null)
                return;

            var frame = animation.GetFrameAtTime(animationTime);
            if (frame?.Texture == null)
                return;

            Vector2 summonPosition = GetSummonPosition(summon);
            int screenX = (int)summonPosition.X - mapShiftX + centerX;
            int screenY = (int)summonPosition.Y - mapShiftY + centerY;
            bool shouldFlip = summon.FacingRight ^ frame.Flip;

            frame.Texture.DrawBackground(spriteBatch, null, null,
                GetFrameDrawX(screenX, frame, shouldFlip), screenY - frame.Origin.Y,
                ResolveSummonDrawColor(summon), shouldFlip, null);
        }

        private SkillAnimation ResolveSummonAnimation(ActiveSummon summon, int currentTime, int elapsedTime, out int animationTime)
        {
            animationTime = Math.Max(0, elapsedTime);
            var skill = summon?.SkillData;
            if (skill == null)
                return null;

            string spawnBranchName = SummonRuntimeRules.ResolveSpawnPlaybackBranch(skill);
            var spawnAnimation = _loader.ResolveSummonActionAnimation(
                                     skill,
                                     summon?.Level ?? 0,
                                     spawnBranchName,
                                     skill.SummonSpawnAnimation)
                                 ?? skill.SummonSpawnAnimation;
            if (spawnAnimation?.Frames.Count > 0)
            {
                int spawnDuration = spawnAnimation.TotalDuration > 0
                    ? spawnAnimation.TotalDuration
                    : spawnAnimation.Frames.Sum(frame => frame.Delay);
                if (spawnDuration > 0 && elapsedTime < spawnDuration)
                {
                    animationTime = elapsedTime;
                    return spawnAnimation;
                }

                animationTime = Math.Max(0, elapsedTime - spawnDuration);
            }

            string removalBranchName = SummonRuntimeRules.ResolveRemovalPlaybackBranch(skill);
            var removalAnimation = _loader.ResolveSummonActionAnimation(
                                       skill,
                                       summon?.Level ?? 0,
                                       removalBranchName,
                                       skill.SummonRemovalAnimation)
                                   ?? skill.SummonRemovalAnimation;
            if (removalAnimation?.Frames.Count > 0
                && summon != null
                && summon.RemovalAnimationStartTime != int.MinValue)
            {
                int removalElapsed = currentTime - summon.RemovalAnimationStartTime;
                int removalDuration = GetSkillAnimationDuration(removalAnimation) ?? 0;
                if (removalElapsed >= 0 && removalDuration > 0 && removalElapsed < removalDuration)
                {
                    animationTime = removalElapsed;
                    return removalAnimation;
                }
            }

            var hitAnimation = ResolveSummonHitPlaybackAnimation(summon);
            if (hitAnimation?.Frames.Count > 0
                && summon != null
                && summon.LastHitAnimationStartTime != int.MinValue)
            {
                int hitElapsed = currentTime - summon.LastHitAnimationStartTime;
                int hitDuration = GetSkillAnimationDuration(hitAnimation) ?? 0;
                if (hitElapsed >= 0 && hitDuration > 0 && hitElapsed < hitDuration)
                {
                    animationTime = hitElapsed;
                    return hitAnimation;
                }
            }

            if (summon?.OneTimeActionFallbackAnimation?.Frames.Count > 0
                && SummonedPool.TryResolveOneTimeActionFallbackPlayback(summon, currentTime, out int fallbackAnimationTime))
            {
                animationTime = fallbackAnimationTime;
                return summon.OneTimeActionFallbackAnimation;
            }

            string prepareBranchName = SummonRuntimeRules.ResolvePreparePlaybackBranch(skill);
            var prepareAnimation = _loader.ResolveSummonActionAnimation(
                                       skill,
                                       summon?.Level ?? 0,
                                       prepareBranchName,
                                       skill.SummonAttackPrepareAnimation)
                                   ?? skill.SummonAttackPrepareAnimation;
            if (prepareAnimation?.Frames.Count > 0
                && summon?.ActorState == SummonActorState.Prepare
                && ((skill.SkillId == TESLA_COIL_SKILL_ID
                     && (summon.TeslaCoilState == 1
                         || summon.TeslaCoilState == 2
                         || summon.LastAttackAnimationStartTime == int.MinValue))
                    || skill.SkillId != TESLA_COIL_SKILL_ID))
            {
                int prepareElapsed = Math.Max(0, currentTime - summon.LastStateChangeTime);
                int prepareDuration = GetSkillAnimationDuration(prepareAnimation) ?? 0;
                if (prepareDuration <= 0 || prepareElapsed < prepareDuration)
                {
                    animationTime = prepareElapsed;
                    return prepareAnimation;
                }
            }

            SkillAnimation branchAnimation = !string.IsNullOrWhiteSpace(summon?.CurrentAnimationBranchName)
                ? _loader.ResolveSummonActionAnimation(skill, summon.Level, summon.CurrentAnimationBranchName)
                : null;
            SkillAnimation retryAttackAnimation = branchAnimation?.Frames.Count > 0
                ? null
                : ResolveSummonEmptyActionRetryAnimation(summon);

            bool hasActionPlayback = branchAnimation?.Frames.Count > 0
                                     || retryAttackAnimation?.Frames.Count > 0;
            var attackAnimation = branchAnimation?.Frames.Count > 0
                ? branchAnimation
                : retryAttackAnimation?.Frames.Count > 0
                    ? retryAttackAnimation
                    : string.IsNullOrWhiteSpace(summon?.CurrentAnimationBranchName)
                        ? skill.SummonAttackAnimation
                        : null;
            if (attackAnimation?.Frames.Count > 0 && summon != null)
            {
                int attackElapsed = currentTime - summon.LastAttackAnimationStartTime;
                int prepareDuration = GetSkillAnimationDuration(prepareAnimation) ?? 0;
                int attackDuration = GetSkillAnimationDuration(attackAnimation) ?? 0;
                int attackSequenceDuration = (hasActionPlayback ? 0 : prepareDuration) + attackDuration;
                if (attackElapsed >= 0 && attackSequenceDuration > 0 && attackElapsed < attackSequenceDuration)
                {
                    if (!hasActionPlayback
                        && prepareAnimation?.Frames.Count > 0
                        && attackElapsed < prepareDuration)
                    {
                        animationTime = attackElapsed;
                        return prepareAnimation;
                    }

                    animationTime = hasActionPlayback
                        ? attackElapsed
                        : Math.Max(0, attackElapsed - prepareDuration);
                    return attackAnimation;
                }
            }

            string idleBranchName = SummonRuntimeRules.ResolveIdlePlaybackBranch(skill);
            SkillAnimation idleAnimation = _loader.ResolveSummonActionAnimation(
                                               skill,
                                               summon?.Level ?? 0,
                                               idleBranchName,
                                               skill.SummonAnimation)
                                           ?? skill.SummonAnimation;
            if (idleAnimation?.Frames.Count > 0)
            {
                return idleAnimation;
            }

            animationTime = Math.Max(0, elapsedTime);
            return spawnAnimation;
        }

        private static int? GetSkillAnimationDuration(SkillAnimation animation)
        {
            if (animation?.Frames == null || animation.Frames.Count <= 0)
            {
                return null;
            }

            return animation.TotalDuration > 0
                ? animation.TotalDuration
                : animation.Frames.Sum(frame => frame.Delay);
        }

        #endregion

        #region Utility

        public SkillData GetSkillData(int skillId)
        {
            return FindKnownSkillData(skillId);
        }

        private static bool IsInvincibleZoneSkill(SkillData skill)
        {
            return skill != null
                   && (skill.SkillId == SMOKE_BOMB_SKILL_ID
                       || string.Equals(skill.ZoneType, "invincible", StringComparison.OrdinalIgnoreCase));
        }

        internal static SkillAnimation ResolveInvincibleZoneAnimation(SkillData skill)
        {
            if (skill?.ZoneAnimation?.Frames.Count > 0)
            {
                return skill.ZoneAnimation;
            }

            if (IsInvincibleZoneSkill(skill) && skill?.AvatarOverlayEffect?.Frames.Count > 0)
            {
                return skill.AvatarOverlayEffect;
            }

            return null;
        }

        private int ResolveQueuedExecutionSkillLevel(int skillId)
        {
            // The client re-reads the learned level when deferred branches actually fire instead of
            // trusting the level snapshot captured when the request was first armed.
            return GetSkillLevel(skillId);
        }

        private bool TryResolveQueuedExecutionSkillData(
            int skillId,
            out SkillData skill,
            out int level,
            out SkillLevelData levelData)
        {
            skill = GetSkillData(skillId);
            level = ResolveQueuedExecutionSkillLevel(skillId);
            levelData = null;

            if (skill == null || level <= 0)
            {
                return false;
            }

            levelData = skill.GetLevel(level);
            return levelData != null;
        }

        private static bool UsesAuraDotAffectedSkillPassive(SkillData skill)
        {
            return skill?.UsesAffectedSkillPassiveData == true
                   && string.Equals(skill.AffectedSkillEffect, "dot", StringComparison.OrdinalIgnoreCase)
                   && string.Equals(skill.DotType, "aura", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool UsesProjectedSupportAffectedSkillPassive(
            SkillData skill,
            SkillLevelData levelData,
            IReadOnlyCollection<SkillData> supportSkills = null)
        {
            return skill?.UsesAffectedSkillPassiveData == true
                   && !skill.UsesAffectedSkillBodyAttack
                   && !UsesAuraDotAffectedSkillPassive(skill)
                   && RemoteAffectedAreaSupportResolver.IsFriendlyPlayerAreaSkill(skill, supportSkills, levelData)
                   && RemoteAffectedAreaSupportResolver.CreateProjectedSupportBuffLevelData(
                       skill,
                       levelData,
                       supportSkills?.ToArray() ?? Array.Empty<SkillData>()) != null;
        }

        internal static int ResolveAffectedSkillSupportBuffId(int skillId)
        {
            return AffectedSkillSupportBuffIdBase - Math.Abs(skillId);
        }

        private static bool IsAttackTriggeredChainBuffSkill(SkillData skill, int level)
        {
            SkillLevelData levelData = skill?.GetLevel(level);
            return skill != null
                   && skill.SkillId == SPARK_SKILL_ID
                   && skill.ChainAttack
                   && string.Equals(skill.TriggerCondition, "attack", StringComparison.OrdinalIgnoreCase)
                   && levelData?.Time > 0;
        }

        private static bool ShouldUseQueuedSerialAttack(SkillData skill)
        {
            return skill?.ChainAttack == true
                   && skill.ChainAttackPenalty
                   && string.IsNullOrWhiteSpace(skill.TriggerCondition);
        }

        private static AttackResolutionMode GetAttackResolutionMode(SkillData skill)
        {
            if (skill?.Projectile != null)
                return AttackResolutionMode.Projectile;

            return skill?.AttackType switch
            {
                SkillAttackType.Magic => AttackResolutionMode.Magic,
                SkillAttackType.Ranged => AttackResolutionMode.Ranged,
                _ => AttackResolutionMode.Melee
            };
        }

        private bool IsSkillAllowedForCurrentJob(SkillData skill)
        {
            if (skill == null)
                return false;

            return IsSkillAllowedForJob(skill.Job, _player.Build?.Job ?? 0);
        }

        internal static bool IsSkillAllowedForJob(int skillJob, int currentJob)
        {
            if (!IsAdminSkillJob(skillJob))
                return true;

            return currentJob switch
            {
                910 => skillJob == 900 || skillJob == 910,
                900 => skillJob == 900,
                _ => false
            };
        }

        private void TryQueueFollowUpAttack(SkillData triggerSkill, int currentTime, int? targetMobId, bool facingRight)
        {
            if (triggerSkill?.FinalAttackTriggers == null || triggerSkill.FinalAttackTriggers.Count == 0)
                return;

            int equippedWeaponCode = GetEquippedWeaponCode();
            if (equippedWeaponCode <= 0)
                return;

            foreach ((int followUpSkillId, HashSet<int> allowedWeaponCodes) in triggerSkill.FinalAttackTriggers)
            {
                if (allowedWeaponCodes == null || !allowedWeaponCodes.Contains(equippedWeaponCode))
                    continue;

                int followUpLevel = GetSkillLevel(followUpSkillId);
                if (followUpLevel <= 0)
                    continue;

                SkillData followUpSkill = GetSkillData(followUpSkillId);
                SkillLevelData followUpLevelData = followUpSkill?.GetLevel(followUpLevel);
                if (followUpLevelData == null || followUpLevelData.Prop <= 0)
                    continue;

                if (Random.Next(100) >= Math.Clamp(followUpLevelData.Prop, 0, 100))
                    continue;

                QueueOrReplaceFollowUpAttack(new QueuedFollowUpAttack
                {
                    SkillId = followUpSkillId,
                    Level = followUpLevel,
                    ExecuteTime = currentTime + FOLLOW_UP_ATTACK_DELAY,
                    TargetMobId = targetMobId,
                    TargetPosition = ResolveDeferredPreferredTargetPosition(targetMobId, currentTime),
                    FacingRight = facingRight,
                    RequiredWeaponCode = equippedWeaponCode,
                    // `CUserLocal::TryDoingFinalAttack` re-enters direct ranged final attacks
                    // through `TryDoingShootAttack(..., 65, ...)` instead of the generic shoot lane.
                    ShootRange0 = ClientShootAttackFamilyResolver.ResolveQueuedFinalAttackShootRange0(followUpSkill),
                    ResolvedShootAmmoSelection = ResolveQueuedShootAmmoSelectionSnapshot(
                        followUpSkill,
                        followUpLevelData,
                        equippedWeaponCode,
                        ignoreAmmoBypassTemporaryStat: true),
                    ShootAmmoBypassActive = HasShootAmmoBypassTemporaryStat()
                });
            }
        }

        private void QueueOrReplaceFollowUpAttack(QueuedFollowUpAttack queuedAttack)
        {
            if (queuedAttack == null)
                return;

            // The client keeps one pending final-attack slot and overwrites it when a newer proc wins.
            _queuedFollowUpAttacks.Clear();
            ClearDeferredQueuedFinalAttackPayloads();
            ClearPendingQueuedProjectileSpawns(isQueuedFinalAttack: true, isQueuedSparkAttack: false);
            _queuedFollowUpAttacks.Enqueue(queuedAttack);
        }

        private void ClearDeferredQueuedFinalAttackPayloads()
        {
            if (_deferredSkillPayloads.Count == 0)
            {
                return;
            }

            DeferredSkillPayload[] retainedPayloads = _deferredSkillPayloads
                .Where(payload => payload?.IsQueuedFinalAttack != true)
                .ToArray();
            if (retainedPayloads.Length == _deferredSkillPayloads.Count)
            {
                return;
            }

            _deferredSkillPayloads.Clear();
            foreach (DeferredSkillPayload payload in retainedPayloads)
            {
                _deferredSkillPayloads.Enqueue(payload);
            }
        }

        private void ClearDeferredQueuedSparkPayloads()
        {
            if (_deferredSkillPayloads.Count == 0)
            {
                return;
            }

            DeferredSkillPayload[] retainedPayloads = _deferredSkillPayloads
                .Where(payload => payload?.IsQueuedSparkAttack != true)
                .ToArray();
            if (retainedPayloads.Length == _deferredSkillPayloads.Count)
            {
                return;
            }

            _deferredSkillPayloads.Clear();
            foreach (DeferredSkillPayload payload in retainedPayloads)
            {
                _deferredSkillPayloads.Enqueue(payload);
            }
        }

        private ShootAmmoSelection ResolveQueuedShootAmmoSelectionSnapshot(
            SkillData skill,
            SkillLevelData levelData,
            int weaponCode,
            bool ignoreAmmoBypassTemporaryStat = false)
        {
            if (skill == null
                || !RequiresClientShootAttackValidation(skill)
                || IsShootSkillNotUsingShootingWeapon(skill.SkillId)
                || IsShootSkillNotConsumingBullet(skill.SkillId)
                || (!ignoreAmmoBypassTemporaryStat && HasShootAmmoBypassTemporaryStat())
                || weaponCode <= 0)
            {
                return null;
            }

            return TryResolveCompatibleShootAmmoSelection(
                    skill.SkillId,
                    levelData,
                    weaponCode,
                    ResolveRequiredShootAmmoCount(levelData),
                    out ShootAmmoSelection selection)
                ? selection?.Snapshot()
                : null;
        }

        private void ClearPendingQueuedProjectileSpawns(bool isQueuedFinalAttack, bool isQueuedSparkAttack)
        {
            if (_pendingProjectileSpawns.Count == 0)
            {
                return;
            }

            _pendingProjectileSpawns.RemoveAll(pending =>
                pending?.Projectile != null
                && ((isQueuedFinalAttack && pending.Projectile.IsQueuedFinalAttack)
                    || (isQueuedSparkAttack && pending.Projectile.IsQueuedSparkAttack)));
        }

        private void QueueOrReplaceSerialAttack(
            SkillData skill,
            int level,
            int currentTime,
            List<MobItem> resolvedTargets,
            bool facingRight)
        {
            if (!ShouldUseQueuedSerialAttack(skill) || resolvedTargets == null || resolvedTargets.Count == 0)
                return;

            int equippedWeaponCode = GetEquippedWeaponCode();
            if (equippedWeaponCode <= 0)
                return;

            MobItem preferredTarget = resolvedTargets[0];
            // The client keeps a single pending serial-attack slot, separate from final-attack and spark queues.
            _queuedSerialAttack = new QueuedSerialAttack
            {
                SkillId = skill.SkillId,
                Level = level,
                ExecuteTime = currentTime + FOLLOW_UP_ATTACK_DELAY,
                FacingRight = facingRight,
                RequiredWeaponCode = equippedWeaponCode,
                PreferredTargetMobId = preferredTarget?.PoolId,
                PreferredTargetPosition = preferredTarget != null
                    ? new Vector2(GetMobImpactX(preferredTarget), GetMobImpactY(preferredTarget))
                    : new Vector2(_player.X, _player.Y)
            };
        }

        private void TryQueueAttackTriggeredBuffProc(SkillData triggerSkill, int currentTime, MobItem sourceMob, bool facingRight)
        {
            if (triggerSkill == null || sourceMob == null || triggerSkill.SkillId == SPARK_SKILL_ID)
                return;

            ActiveBuff sparkBuff = _buffs.LastOrDefault(buff => IsAttackTriggeredChainBuffSkill(buff.SkillData, buff.Level));
            if (sparkBuff == null)
                return;

            int equippedWeaponCode = GetEquippedWeaponCode();
            if (equippedWeaponCode <= 0)
                return;

            ClearDeferredQueuedSparkPayloads();
            ClearPendingQueuedProjectileSpawns(isQueuedFinalAttack: false, isQueuedSparkAttack: true);
            _queuedSparkAttack = new QueuedSparkAttack
            {
                SkillId = sparkBuff.SkillId,
                Level = sparkBuff.Level,
                ExecuteTime = currentTime + FOLLOW_UP_ATTACK_DELAY,
                SourceMobId = sourceMob.PoolId,
                SourcePosition = new Vector2(GetMobImpactX(sourceMob), GetMobImpactY(sourceMob)),
                PreferredTargetPosition = ResolveDeferredPreferredTargetPosition(sourceMob.PoolId, currentTime),
                FacingRight = facingRight,
                RequiredWeaponCode = equippedWeaponCode,
                ResolvedShootAmmoSelection = ResolveQueuedShootAmmoSelectionSnapshot(
                    sparkBuff.SkillData,
                    sparkBuff.SkillData?.GetLevel(sparkBuff.Level),
                    equippedWeaponCode,
                    ignoreAmmoBypassTemporaryStat: true),
                ShootAmmoBypassActive = HasShootAmmoBypassTemporaryStat()
            };
        }

        private Vector2 ResolveQueuedSparkAttackOrigin(QueuedSparkAttack queuedAttack, int currentTime)
        {
            if (queuedAttack == null)
                return new Vector2(_player.X, _player.Y);

            if (queuedAttack.SourceMobId is int sourceMobId)
            {
                MobItem sourceMob = FindAttackableMobByPoolId(sourceMobId, currentTime);
                if (sourceMob != null)
                {
                    return GetMobHitboxCenter(sourceMob, currentTime);
                }
            }

            return queuedAttack.SourcePosition;
        }

        private Vector2 ResolveQueuedSerialAttackOrigin(QueuedSerialAttack queuedAttack, int currentTime)
        {
            if (queuedAttack == null)
                return new Vector2(_player.X, _player.Y);

            if (queuedAttack.PreferredTargetMobId is int preferredTargetMobId)
            {
                MobItem preferredTarget = FindAttackableMobByPoolId(preferredTargetMobId, currentTime);
                if (preferredTarget != null)
                {
                    return GetMobHitboxCenter(preferredTarget, currentTime);
                }
            }

            return queuedAttack.PreferredTargetPosition;
        }

        private List<MobItem> ResolveQueuedSerialTargets(QueuedSerialAttack queuedAttack, SkillLevelData levelData, int currentTime)
        {
            if (_mobPool == null || levelData == null || queuedAttack == null)
                return new List<MobItem>();

            int maxTargets = Math.Max(1, levelData.MobCount - 1);
            float bounceRange = Math.Max(40f, levelData.Range > 0 ? levelData.Range : 150f);
            MobItem preferredTarget = queuedAttack.PreferredTargetMobId.HasValue
                ? FindAttackableMobByPoolId(queuedAttack.PreferredTargetMobId.Value, currentTime)
                : null;
            Vector2 origin = ResolveQueuedSerialAttackOrigin(queuedAttack, currentTime);

            var excludedMobIds = new HashSet<int>();
            if (preferredTarget != null)
            {
                excludedMobIds.Add(preferredTarget.PoolId);
            }
            else if (queuedAttack.PreferredTargetMobId.HasValue)
            {
                excludedMobIds.Add(queuedAttack.PreferredTargetMobId.Value);
            }

            return ResolveSequentialChainTargets(
                origin,
                bounceRange,
                maxTargets,
                excludedMobIds,
                preferredOrderByMobId: null,
                currentTime);
        }

        private void BeginQueuedFollowUpCast(
            SkillData skill,
            int level,
            SkillLevelData levelData,
            int currentTime,
            bool facingRight)
        {
            if (_player == null || skill == null || levelData == null)
            {
                return;
            }

            _player.FacingRight = facingRight;
            if (_player.Physics != null)
            {
                _player.Physics.FacingRight = facingRight;
            }

            _currentCast = new SkillCastInfo
            {
                SkillId = skill.SkillId,
                Level = level,
                SkillData = skill,
                LevelData = levelData,
                EffectAnimation = GetInitialCastEffect(skill),
                SecondaryEffectAnimation = GetInitialCastSecondaryEffect(skill),
                CastTime = currentTime,
                CasterId = 0,
                CasterX = _player.X,
                CasterY = _player.Y,
                FacingRight = facingRight
            };

            TriggerSkillAnimation(skill, currentTime);
            if (_currentCast != null && TryApplyClientOwnedAvatarEffect(skill, currentTime))
            {
                _currentCast.SuppressEffectAnimation = true;
            }

            PlayCastSound(skill);
            OnSkillCast?.Invoke(_currentCast);
        }

        private int GetEquippedWeaponCode()
        {
            int itemId = _player.Build?.GetWeapon()?.ItemId ?? 0;
            return GetWeaponCode(itemId);
        }

        private static int GetWeaponCode(int itemId)
        {
            return itemId > 0 ? Math.Abs(itemId / 10000) % 100 : 0;
        }

        private MobItem FindAttackableMobByPoolId(int poolId, int currentTime)
        {
            if (_mobPool == null || poolId <= 0)
                return null;

            MobItem mob = _mobPool.ActiveMobs.FirstOrDefault(candidate => candidate?.PoolId == poolId && IsMobAttackable(candidate));
            if (mob == null)
                return null;

            return GetMobHitbox(mob, currentTime).IsEmpty ? null : mob;
        }

        private static int GetFrameDrawX(int anchorX, SkillFrame frame, bool shouldFlip)
        {
            if (frame?.Texture == null)
                return anchorX;

            return shouldFlip
                ? anchorX - (frame.Texture.Width - frame.Origin.X)
                : anchorX - frame.Origin.X;
        }

        private Rectangle GetDefaultMeleeHitbox(SkillData skill, SkillLevelData levelData, bool facingRight)
        {
            int width = Math.Max(80, Math.Max(levelData?.Range ?? 0, GetAnimationWidth(skill)));
            int height = Math.Max(60, Math.Max(levelData?.RangeY ?? 0, GetAnimationHeight(skill)));
            int offsetX = facingRight ? 10 : -(width + 10);

            return new Rectangle(offsetX, -height - 10, width, height);
        }

        private Rectangle GetDefaultMagicHitbox(SkillData skill, SkillLevelData levelData, bool facingRight)
        {
            int width = Math.Max(120, Math.Max(levelData?.Range ?? 0, GetAnimationWidth(skill)));
            int height = Math.Max(80, Math.Max(levelData?.RangeY ?? 0, GetAnimationHeight(skill)));
            int offsetX = facingRight ? 20 : -(width + 20);

            return new Rectangle(offsetX, -height - 20, width, height);
        }

        private Rectangle GetDefaultRangedHitbox(SkillData skill, SkillLevelData levelData, bool facingRight, int shootRange0Override = 0)
        {
            int width = Math.Max(160, Math.Max(levelData?.Range ?? 0, GetAnimationWidth(skill)));
            int height = Math.Max(70, Math.Max(levelData?.RangeY ?? 0, GetAnimationHeight(skill)));
            int shootRange0 = Math.Max(25, shootRange0Override);
            int offsetX = facingRight ? shootRange0 : -(width + shootRange0);

            return new Rectangle(offsetX, -height / 2, width, height);
        }

        private static int GetAnimationWidth(SkillData skill)
        {
            return GetMaxFrameDimension(skill?.Effect, frame => frame.Bounds.Width);
        }

        private static int GetAnimationHeight(SkillData skill)
        {
            return GetMaxFrameDimension(skill?.Effect, frame => frame.Bounds.Height);
        }

        private static int GetMaxFrameDimension(SkillAnimation animation, Func<SkillFrame, int> selector)
        {
            if (animation?.Frames == null || animation.Frames.Count == 0)
                return 0;

            int max = 0;
            foreach (var frame in animation.Frames)
            {
                if (frame == null)
                    continue;

                max = Math.Max(max, selector(frame));
            }

            return max;
        }

        public IEnumerable<SkillData> GetLearnedSkills()
        {
            foreach (var skill in _availableSkills)
            {
                if (GetSkillLevel(skill.SkillId) > 0)
                {
                    yield return skill;
                }
            }
        }

        public IEnumerable<SkillData> GetActiveSkills()
        {
            foreach (var skill in _availableSkills)
            {
                if (!skill.IsPassive
                    && !skill.Invisible
                    && !skill.SuppressesStandaloneActiveCast
                    && GetSkillLevel(skill.SkillId) > 0)
                {
                    yield return skill;
                }
            }
        }

        private void UpdateSummonTileEffects(int currentTime)
        {
            for (int i = _summonTileEffects.Count - 1; i >= 0; i--)
            {
                if (_summonTileEffects[i].IsExpired(currentTime))
                {
                    _summonTileEffects.RemoveAt(i);
                }
            }
        }

        public IEnumerable<SkillData> GetAllSkills()
        {
            return _availableSkills;
        }

        public PreparedSkill GetPreparedSkill() => _preparedSkill ?? BuildRepeatSkillHudSnapshot();

        /// <summary>
        /// Full clear - clears everything including skill levels and hotkeys.
        /// Use when completely disposing the skill system.
        /// </summary>
        public void Clear()
        {
            _projectiles.Clear();
            _queuedFollowUpAttacks.Clear();
            _queuedSummonAttacks.Clear();
            _queuedSerialAttack = null;
            _queuedSparkAttack = null;
            _deferredSkillPayloads.Clear();
            _pendingProjectileSpawns.Clear();
            _clientSkillTimers.Clear();
            _skillZones.Clear();
            _rocketBoosterState = null;
            _lastDeferredMovingShootExecution = null;
            _activeRepeatSkillSustain = null;
            _cycloneState = null;
            _swallowState = null;
            _activeAffectedSkillPassives.Clear();
            _affectedSkillSupportBuffIds.Clear();
            _mineMovementDirection = 0;
            _mineMovementStartTime = 0;
            _recentOwnerAttackTargetTimes.Clear();
            ClearMapFlyingRepeatAvatarEffect(Environment.TickCount);
            _player.EndSustainedSkillAnimation();
            ClearSummonPuppets();
            _summons.Clear();
            _hitEffects.Clear();
            _summonTileEffects.Clear();
            if (_preparedSkill != null)
            {
                ClearPreparedSkillForForcedLocalRelease(Environment.TickCount);
            }

            // Remove all buff effects
            foreach (var buff in _buffs)
            {
                ApplyBuffStats(buff, false);
                _player.ClearSkillAvatarTransform(buff.SkillId);
                _player.ClearSkillAvatarEffect(buff.SkillId, Environment.TickCount, playFinish: false);
                UpdateClientOwnedAvatarEffectSuppression(buff.SkillData, suppress: false);
                ClearSkillMount(buff.SkillId);
            }
            _buffs.Clear();
            RefreshBuffControlledFlyingAbility();

            _cooldowns.Clear();
            _serverCooldownExpireTimes.Clear();
            _pendingCooldownCompletionNotifications.Clear();
            _expiredAuthoritativeCooldowns.Clear();
            _cooldownUiPresentations.Clear();
            _currentCast = null;
            _skillLevels.Clear();
            _skillHotkeys.Clear();
            _itemHotkeys.Clear();
            _availableSkills.Clear();
            _player.ClearSkillAvatarTransformAndPlayExitAction();
            _player.ClearAllSkillAvatarEffects(playFinish: false, Environment.TickCount);
            _player.ClearAllTransientSkillAvatarEffects();
            ClearSkillMount();
        }

        /// <summary>
        /// Clear map-specific state but preserve persistent data.
        /// Preserves: skill levels, hotkeys, available skills, cooldowns.
        /// Clears: active projectiles, hit effects, current cast, buffs.
        /// Preserves: active summons and remaining summon durations.
        /// </summary>
        public void ClearMapState()
        {
            _projectiles.Clear();
            _queuedFollowUpAttacks.Clear();
            _queuedSummonAttacks.Clear();
            _queuedSerialAttack = null;
            _queuedSparkAttack = null;
            _deferredSkillPayloads.Clear();
            _pendingProjectileSpawns.Clear();
            _clientSkillTimers.Clear();
            _skillZones.Clear();
            _rocketBoosterState = null;
            _lastDeferredMovingShootExecution = null;
            _activeRepeatSkillSustain = null;
            _cycloneState = null;
            _swallowState = null;
            _activeAffectedSkillPassives.Clear();
            _affectedSkillSupportBuffIds.Clear();
            _mineMovementDirection = 0;
            _mineMovementStartTime = 0;
            _recentOwnerAttackTargetTimes.Clear();
            ClearMapFlyingRepeatAvatarEffect(Environment.TickCount);
            _player.EndSustainedSkillAnimation();
            ClearSummonPuppets();
            _hitEffects.Clear();
            _summonTileEffects.Clear();
            _currentCast = null;
            if (_preparedSkill != null)
            {
                ClearPreparedSkillForForcedLocalRelease(Environment.TickCount);
            }

            foreach (var buff in _buffs)
            {
                ApplyBuffStats(buff, false);
                _player.ClearSkillAvatarTransform(buff.SkillId);
                _player.ClearSkillAvatarEffect(buff.SkillId, Environment.TickCount, playFinish: false);
                UpdateClientOwnedAvatarEffectSuppression(buff.SkillData, suppress: false);
                ClearSkillMount(buff.SkillId);
            }
            _buffs.Clear();
            RefreshBuffControlledFlyingAbility();
            _player.ClearSkillAvatarTransformAndPlayExitAction();
            _player.ClearAllSkillAvatarEffects(playFinish: false, Environment.TickCount);
            _player.ClearAllTransientSkillAvatarEffects();
            ClearSkillMount();

            foreach (var summon in _summons)
            {
                summon.NeedsAnchorReset = SummonMovementResolver.IsAnchorBound(summon.MovementStyle);
            }

            // Clear map-specific references
            _mobPool = null;
            _combatEffects = null;
            _animationEffects = null;

            // Note: We intentionally do NOT clear:
            // - _skillLevels (learned skills persist)
            // - _skillHotkeys (hotkey bindings persist)
            // - _itemHotkeys (item hotkey bindings persist)
            // - _availableSkills (job skills persist)
            // - _cooldowns (debatable - could reset or persist)
        }

        private void ClearActiveSkillState(bool clearBuffs)
        {
            _projectiles.Clear();
            _queuedFollowUpAttacks.Clear();
            _queuedSummonAttacks.Clear();
            _queuedSerialAttack = null;
            _queuedSparkAttack = null;
            _deferredSkillPayloads.Clear();
            _pendingProjectileSpawns.Clear();
            _clientSkillTimers.Clear();
            _skillZones.Clear();
            _rocketBoosterState = null;
            _lastDeferredMovingShootExecution = null;
            _activeRepeatSkillSustain = null;
            _cycloneState = null;
            _swallowState = null;
            _activeAffectedSkillPassives.Clear();
            _affectedSkillSupportBuffIds.Clear();
            _mineMovementDirection = 0;
            _mineMovementStartTime = 0;
            _recentOwnerAttackTargetTimes.Clear();
            ClearMapFlyingRepeatAvatarEffect(Environment.TickCount);
            _player.EndSustainedSkillAnimation();
            ClearSummonPuppets();
            _summons.Clear();
            _hitEffects.Clear();
            _summonTileEffects.Clear();
            _currentCast = null;
            _activeEnergyChargeRuntime = null;
            if (_preparedSkill != null)
            {
                ClearPreparedSkillForForcedLocalRelease(Environment.TickCount);
            }

            if (!clearBuffs)
                return;

            foreach (var buff in _buffs)
            {
                ApplyBuffStats(buff, false);
                _player.ClearSkillAvatarTransform(buff.SkillId);
                _player.ClearSkillAvatarEffect(buff.SkillId, Environment.TickCount, playFinish: false);
                UpdateClientOwnedAvatarEffectSuppression(buff.SkillData, suppress: false);
                ClearSkillMount(buff.SkillId);
            }
            _buffs.Clear();
            RefreshBuffControlledFlyingAbility();
            _player.ClearSkillAvatarTransformAndPlayExitAction();
            _player.ClearAllSkillAvatarEffects(playFinish: false, Environment.TickCount);
            _player.ClearAllTransientSkillAvatarEffects();
            ClearSkillMount();
        }

        private static bool IsFocusedSingleBookJob(int jobId)
        {
            return jobId >= 800 && jobId < 1000;
        }

        private string GetStateRestrictionMessage(SkillData skill, int currentTime)
        {
            string stateRestrictionMessage = PlayerSkillStateRestrictionEvaluator.GetRestrictionMessage(_player, skill, currentTime);
            if (!string.IsNullOrWhiteSpace(stateRestrictionMessage))
            {
                return stateRestrictionMessage;
            }

            string externalRestrictionMessage = _externalStateRestrictionMessageProvider?.Invoke(currentTime);
            if (!string.IsNullOrWhiteSpace(externalRestrictionMessage))
            {
                return externalRestrictionMessage;
            }

            return _additionalStateRestrictionMessageProvider?.Invoke(currentTime);
        }

        private static bool IsAdminSkillJob(int jobId)
        {
            return jobId == 900 || jobId == 910;
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            if (maxDelta <= 0f || Math.Abs(target - current) <= maxDelta)
                return target;

            return current + MathF.Sign(target - current) * maxDelta;
        }

        #endregion
    }
}
