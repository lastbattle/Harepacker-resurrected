using System.Collections.Generic;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Pools;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class RemoteUserTemporaryStatAvatarEffectParityTests
    {
        private const int TemporaryStatBitSpeed = 0;
        private const int TemporaryStatBitWeaponCharge = 2;

        [Fact]
        public void CanUseRemoteEnergyChargeAvatarEffect_BuccaneerBelowFullChargeBoundary_IsFalse()
        {
            SkillData skill = CreateEnergyChargeAvatarEffectSkill("100+5*x");

            bool canUse = RemoteUserActorPool.CanUseRemoteEnergyChargeAvatarEffectForTesting(
                5110001,
                weaponChargeValue: 104,
                skill);

            Assert.False(canUse);
        }

        [Fact]
        public void CanUseRemoteEnergyChargeAvatarEffect_BuccaneerAtFullChargeBoundary_IsTrue()
        {
            SkillData skill = CreateEnergyChargeAvatarEffectSkill("100+5*x");

            bool canUse = RemoteUserActorPool.CanUseRemoteEnergyChargeAvatarEffectForTesting(
                5110001,
                weaponChargeValue: 105,
                skill);

            Assert.True(canUse);
        }

        [Fact]
        public void CanUseRemoteEnergyChargeAvatarEffect_ThunderBreakerBelowFullChargeBoundary_IsFalse()
        {
            SkillData skill = CreateEnergyChargeAvatarEffectSkill("150+10*x");

            bool canUse = RemoteUserActorPool.CanUseRemoteEnergyChargeAvatarEffectForTesting(
                15100004,
                weaponChargeValue: 159,
                skill);

            Assert.False(canUse);
        }

        [Fact]
        public void CanUseRemoteEnergyChargeAvatarEffect_ThunderBreakerAtFullChargeBoundary_IsTrue()
        {
            SkillData skill = CreateEnergyChargeAvatarEffectSkill("150+10*x");

            bool canUse = RemoteUserActorPool.CanUseRemoteEnergyChargeAvatarEffectForTesting(
                15100004,
                weaponChargeValue: 160,
                skill);

            Assert.True(canUse);
        }

        [Fact]
        public void ApplyResetMask_ClearsWeaponChargeValueWhenWeaponChargeBitIsReset()
        {
            int[] activeMask = BuildMask(TemporaryStatBitWeaponCharge);
            RemoteUserTemporaryStatKnownState knownState = default with
            {
                ChargeSkillId = 1211004,
                WeaponChargeValue = 200
            };
            RemoteUserTemporaryStatSnapshot snapshot = new(
                EncodedLength: 0,
                MaskWords: activeMask,
                RawPayload: new byte[24],
                KnownState: knownState,
                HasWeaponCharge: true,
                WeaponChargePayloadOffset: -1);

            RemoteUserTemporaryStatSnapshot masked =
                RemoteUserPacketCodec.ApplyResetMask(snapshot, activeMask);

            Assert.False(masked.HasWeaponCharge);
            Assert.Null(masked.KnownState.ChargeSkillId);
            Assert.Null(masked.KnownState.WeaponChargeValue);
        }

        [Fact]
        public void ApplyResetMask_PreservesWeaponChargeValueWhenWeaponChargeBitRemainsActive()
        {
            int[] activeMask = BuildMask(TemporaryStatBitWeaponCharge);
            int[] resetMask = BuildMask(TemporaryStatBitSpeed);
            RemoteUserTemporaryStatKnownState knownState = default with
            {
                WeaponChargeValue = 180
            };
            RemoteUserTemporaryStatSnapshot snapshot = new(
                EncodedLength: 0,
                MaskWords: activeMask,
                RawPayload: new byte[24],
                KnownState: knownState,
                HasWeaponCharge: true,
                WeaponChargePayloadOffset: -1);

            RemoteUserTemporaryStatSnapshot masked =
                RemoteUserPacketCodec.ApplyResetMask(snapshot, resetMask);

            Assert.True(masked.HasWeaponCharge);
            Assert.Equal(180, masked.KnownState.WeaponChargeValue);
        }

        [Fact]
        public void ResolveRemoteEnergyChargeMinimumFullChargeValue_UsesWzFormulaLevelOneBoundary()
        {
            SkillData buccaneer = CreateEnergyChargeAvatarEffectSkill("100+5*x");
            SkillData thunderBreaker = CreateEnergyChargeAvatarEffectSkill("150+10*x");

            int buccaneerBoundary = RemoteUserActorPool.ResolveRemoteEnergyChargeMinimumFullChargeValueForTesting(
                5110001,
                buccaneer);
            int thunderBreakerBoundary = RemoteUserActorPool.ResolveRemoteEnergyChargeMinimumFullChargeValueForTesting(
                15100004,
                thunderBreaker);

            Assert.Equal(105, buccaneerBoundary);
            Assert.Equal(160, thunderBreakerBoundary);
        }

        private static SkillData CreateEnergyChargeAvatarEffectSkill(string thresholdFormula)
        {
            return new SkillData
            {
                UsesEnergyChargeRuntime = true,
                FullChargeEffectName = "bodyAttack",
                EnergyChargeThresholdFormula = thresholdFormula,
                AffectedEffect = new SkillAnimation
                {
                    Frames = new List<SkillFrame> { new() { Delay = 100 } }
                }
            };
        }

        private static int[] BuildMask(int bitIndex)
        {
            int wordIndex = bitIndex / 32;
            int bitOffset = bitIndex % 32;
            var maskWords = new int[4];
            maskWords[wordIndex] = 1 << bitOffset;
            return maskWords;
        }
    }
}
