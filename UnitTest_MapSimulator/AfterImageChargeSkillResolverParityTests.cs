using HaCreator.MapSimulator.Pools;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class AfterImageChargeSkillResolverParityTests
    {
        [Fact]
        public void ResolveChargeSkillIdFromTemporaryStats_NoHintPayload_UsesPreferredKnownChargeSkill()
        {
            RemoteUserTemporaryStatSnapshot snapshot = CreateSnapshot(
                hasWeaponCharge: true,
                rawPayload: new byte[24]);

            int? resolved = RemoteUserActorPool.ResolveChargeSkillIdFromTemporaryStats(
                snapshot,
                preferredSkillId: 1211008);

            Assert.Equal(1211008, resolved);
        }

        [Fact]
        public void TryResolveChargeElementFromTemporaryStats_NoHintPayload_UsesPreferredKnownChargeElement()
        {
            RemoteUserTemporaryStatSnapshot snapshot = CreateSnapshot(
                hasWeaponCharge: true,
                rawPayload: new byte[24]);

            bool resolved = RemoteUserActorPool.TryResolveChargeElementFromTemporaryStats(
                snapshot,
                preferredSkillId: 1211004,
                out int chargeElement);

            Assert.True(resolved);
            Assert.Equal(2, chargeElement);
        }

        [Fact]
        public void TryResolveChargeSkillIdFromKnownTemporaryStatPayload_NoHintPayload_UsesPreferredKnownChargeSkill()
        {
            byte[] rawPayload = new byte[24];

            bool resolved = RemoteUserPacketCodec.TryResolveChargeSkillIdFromKnownTemporaryStatPayloadForTesting(
                rawPayload,
                weaponChargeMetadataOffset: -1,
                preferredSkillId: 1221004,
                out int chargeSkillId);

            Assert.True(resolved);
            Assert.Equal(1221004, chargeSkillId);
        }

        [Fact]
        public void ReconcileChargeSkillIdFromPriorSnapshot_NoHintPayload_CarriesPriorKnownChargeSkill()
        {
            RemoteUserTemporaryStatSnapshot incoming = CreateSnapshot(
                hasWeaponCharge: true,
                rawPayload: new byte[24]);
            RemoteUserTemporaryStatSnapshot prior = CreateSnapshot(
                hasWeaponCharge: true,
                rawPayload: new byte[24],
                knownChargeSkillId: 1221004);

            RemoteUserTemporaryStatSnapshot reconciled =
                RemoteUserActorPool.ReconcileChargeSkillIdFromPriorSnapshotForTesting(incoming, prior);

            Assert.Equal(1221004, reconciled.KnownState.ChargeSkillId);
        }

        [Fact]
        public void ReconcileChargeSkillIdFromPriorSnapshot_WithoutWeaponCharge_DoesNotCarryPriorSkill()
        {
            RemoteUserTemporaryStatSnapshot incoming = CreateSnapshot(
                hasWeaponCharge: false,
                rawPayload: new byte[24]);
            RemoteUserTemporaryStatSnapshot prior = CreateSnapshot(
                hasWeaponCharge: true,
                rawPayload: new byte[24],
                knownChargeSkillId: 1221004);

            RemoteUserTemporaryStatSnapshot reconciled =
                RemoteUserActorPool.ReconcileChargeSkillIdFromPriorSnapshotForTesting(incoming, prior);

            Assert.Null(reconciled.KnownState.ChargeSkillId);
        }

        [Fact]
        public void ReconcileChargeSkillIdFromPriorSnapshot_WithIncomingKnownSkill_DoesNotOverride()
        {
            RemoteUserTemporaryStatSnapshot incoming = CreateSnapshot(
                hasWeaponCharge: true,
                rawPayload: new byte[24],
                knownChargeSkillId: 1211006);
            RemoteUserTemporaryStatSnapshot prior = CreateSnapshot(
                hasWeaponCharge: true,
                rawPayload: new byte[24],
                knownChargeSkillId: 1221004);

            RemoteUserTemporaryStatSnapshot reconciled =
                RemoteUserActorPool.ReconcileChargeSkillIdFromPriorSnapshotForTesting(incoming, prior);

            Assert.Equal(1211006, reconciled.KnownState.ChargeSkillId);
        }

        private static RemoteUserTemporaryStatSnapshot CreateSnapshot(
            bool hasWeaponCharge,
            byte[] rawPayload,
            int? knownChargeSkillId = null)
        {
            RemoteUserTemporaryStatKnownState knownState = default;
            if (knownChargeSkillId.HasValue)
            {
                knownState = knownState with
                {
                    ChargeSkillId = knownChargeSkillId
                };
            }

            return new RemoteUserTemporaryStatSnapshot(
                EncodedLength: rawPayload?.Length ?? 0,
                MaskWords: new int[4],
                RawPayload: rawPayload,
                KnownState: knownState,
                HasWeaponCharge: hasWeaponCharge,
                WeaponChargePayloadOffset: -1);
        }
    }
}
