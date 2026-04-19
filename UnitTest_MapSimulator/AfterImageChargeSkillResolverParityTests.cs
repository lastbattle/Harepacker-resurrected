using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Pools;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class AfterImageChargeSkillResolverParityTests
    {
        [Fact]
        public void ElementAttributeToken_LightningAliasL_MapsToChargeElement3()
        {
            bool resolved = AfterImageChargeSkillResolver.TryResolveChargeElementFromElementAttributeToken("l", out int chargeElement);

            Assert.True(resolved);
            Assert.Equal(3, chargeElement);
        }

        [Fact]
        public void PreferredChargeSkill_ThunderBreakerJob_UsesLightningAliasToken()
        {
            int preferredSkillId = RemoteUserActorPool.ResolvePreferredRemoteAfterImageChargeSkillId(
                jobId: 1510,
                skillId: 15111006,
                skillElementAttributeToken: "l");

            Assert.Equal(15101006, preferredSkillId);
        }

        [Fact]
        public void ResolveChargeSkillIdFromTemporaryStats_PrefersMetadataScopedWindowBeforeBroadPayloadScan()
        {
            byte[] payload = BuildWeaponChargePayloadWithConflictingTail();
            RemoteUserTemporaryStatSnapshot snapshot = new(
                EncodedLength: payload.Length,
                MaskWords: new[] { 1 << 2, 0, 0, 0 },
                RawPayload: payload,
                KnownState: default,
                HasWeaponCharge: true,
                WeaponChargePayloadOffset: 20);

            int? resolvedSkillId = RemoteUserActorPool.ResolveChargeSkillIdFromTemporaryStats(snapshot, preferredSkillId: 0);

            Assert.True(resolvedSkillId.HasValue);
            Assert.Equal(1211008, resolvedSkillId.Value);
        }

        [Fact]
        public void ResolveChargeSkillIdFromTemporaryStatPayload_MetadataScopedMixedIdsWithoutPreference_PreservesEarliestScopedCandidate()
        {
            byte[] payload = BuildWeaponChargePayloadWithConflictingScopedWindow();

            bool resolved = AfterImageChargeSkillResolver.TryResolveChargeSkillIdFromTemporaryStatPayload(
                payload,
                startOffset: 20,
                preferredSkillId: 0,
                maxScanBytes: AfterImageChargeSkillResolver.ChargeMetadataScopedScanBytes,
                out int chargeSkillId);

            Assert.True(resolved);
            Assert.Equal(1211008, chargeSkillId);
        }

        [Fact]
        public void ResolveChargeSkillIdFromTemporaryStatPayload_BroadMixedIdsWithoutPreference_RemainsAmbiguous()
        {
            byte[] payload = BuildWeaponChargePayloadWithConflictingScopedWindow();

            bool resolved = AfterImageChargeSkillResolver.TryResolveChargeSkillIdFromTemporaryStatPayload(
                payload,
                startOffset: 20,
                preferredSkillId: 0,
                maxScanBytes: 0,
                out int chargeSkillId);

            Assert.False(resolved);
            Assert.Equal(0, chargeSkillId);
        }

        [Fact]
        public void ResolveChargeSkillIdFromTemporaryStats_MetadataScopedEmptyBroadMixed_UsesNearestMetadataCandidate()
        {
            byte[] payload = BuildWeaponChargePayloadWithBroadMixedTailOutsideScopedWindow();
            RemoteUserTemporaryStatSnapshot snapshot = new(
                EncodedLength: payload.Length,
                MaskWords: new[] { 1 << 2, 0, 0, 0 },
                RawPayload: payload,
                KnownState: default,
                HasWeaponCharge: true,
                WeaponChargePayloadOffset: 20);

            int? resolvedSkillId = RemoteUserActorPool.ResolveChargeSkillIdFromTemporaryStats(snapshot, preferredSkillId: 0);

            Assert.True(resolvedSkillId.HasValue);
            Assert.Equal(1211008, resolvedSkillId.Value);
        }

        [Fact]
        public void DecodeOfficialRemoteTemporaryStats_RecoversChargeSkillFromScopedWindow()
        {
            byte[] payload = BuildWeaponChargePayloadWithConflictingTail();

            RemoteUserTemporaryStatSnapshot snapshot = RemoteUserPacketCodec.DecodeOfficialRemoteTemporaryStats(
                payload,
                temporaryStatOffset: 0,
                avatarLookOffset: payload.Length);

            Assert.True(snapshot.HasWeaponCharge);
            Assert.Equal(20, snapshot.WeaponChargePayloadOffset);
            Assert.Equal(1211008, snapshot.KnownState.ChargeSkillId);
        }

        [Fact]
        public void DecodeOfficialRemoteTemporaryStats_MetadataScopedEmptyBroadMixed_UsesNearestMetadataCandidate()
        {
            byte[] payload = BuildWeaponChargePayloadWithBroadMixedTailOutsideScopedWindow();

            RemoteUserTemporaryStatSnapshot snapshot = RemoteUserPacketCodec.DecodeOfficialRemoteTemporaryStats(
                payload,
                temporaryStatOffset: 0,
                avatarLookOffset: payload.Length);

            Assert.True(snapshot.HasWeaponCharge);
            Assert.Equal(20, snapshot.WeaponChargePayloadOffset);
            Assert.Equal(1211008, snapshot.KnownState.ChargeSkillId);
        }

        private static byte[] BuildWeaponChargePayloadWithConflictingScopedWindow()
        {
            byte[] payload = new byte[36];
            WriteInt32(payload, 0, 1 << 2); // WeaponCharge active in the first mask word.
            WriteInt32(payload, 16, 0); // Raw weapon charge value (not a known charge skill id).
            WriteInt32(payload, 20, unchecked((int)0x01020304)); // Metadata offset entry is unknown.
            WriteInt32(payload, 24, 1211008); // First scoped known candidate.
            WriteInt32(payload, 28, 1221004); // Conflicting scoped known candidate.
            return payload;
        }

        private static byte[] BuildWeaponChargePayloadWithConflictingTail()
        {
            byte[] payload = new byte[48];
            WriteInt32(payload, 0, 1 << 2); // WeaponCharge active in the first mask word.
            WriteInt32(payload, 16, 0); // Raw weapon charge value (not a known charge skill id).
            WriteInt32(payload, 20, unchecked((int)0x55667788)); // Metadata offset entry is unknown.
            WriteInt32(payload, 24, 1211008); // Nearby scoped charge skill id (lightning).
            WriteInt32(payload, 40, 1221004); // Distant conflicting holy id outside scoped window.
            return payload;
        }

        private static byte[] BuildWeaponChargePayloadWithBroadMixedTailOutsideScopedWindow()
        {
            byte[] payload = new byte[56];
            WriteInt32(payload, 0, 1 << 2); // WeaponCharge active in the first mask word.
            WriteInt32(payload, 16, 0); // Raw weapon charge value (not a known charge skill id).
            WriteInt32(payload, 20, unchecked((int)0x11223344)); // Metadata offset entry is unknown.
            WriteInt32(payload, 40, 1211008); // First broad-scan known candidate (nearest to metadata offset).
            WriteInt32(payload, 44, 1221004); // Conflicting broad-scan known candidate.
            return payload;
        }

        private static void WriteInt32(byte[] payload, int offset, int value)
        {
            payload[offset] = (byte)(value & 0xFF);
            payload[offset + 1] = (byte)((value >> 8) & 0xFF);
            payload[offset + 2] = (byte)((value >> 16) & 0xFF);
            payload[offset + 3] = (byte)((value >> 24) & 0xFF);
        }
    }
}
