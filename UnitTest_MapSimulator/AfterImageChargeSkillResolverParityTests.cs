using System;
using HaCreator.MapSimulator.Pools;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class AfterImageChargeSkillResolverParityTests
    {
        [Fact]
        public void ResolveChargeSkillIdFromTemporaryStats_PrefersMaskBaseElementValueBeforeFarKnownSkillId()
        {
            int[] words = new int[12];
            words[4] = 5; // mask-base-local WeaponCharge element value
            words[10] = 1211008; // far known charge skill id
            byte[] payload = BuildPayload(words);
            RemoteUserTemporaryStatSnapshot snapshot = new(
                EncodedLength: payload.Length,
                MaskWords: new int[4],
                RawPayload: payload,
                KnownState: default,
                HasWeaponCharge: true,
                WeaponChargePayloadOffset: -1);

            int? resolvedChargeSkillId = RemoteUserActorPool.ResolveChargeSkillIdFromTemporaryStats(
                snapshot,
                preferredSkillId: 0);

            Assert.Equal(1221004, resolvedChargeSkillId);
        }

        [Fact]
        public void TryResolveChargeElementFromTemporaryStats_PrefersMaskBaseElementValueBeforeFarKnownSkillId()
        {
            int[] words = new int[12];
            words[4] = 2; // mask-base-local WeaponCharge element value
            words[10] = 1211008; // far known charge skill id
            byte[] payload = BuildPayload(words);
            RemoteUserTemporaryStatSnapshot snapshot = new(
                EncodedLength: payload.Length,
                MaskWords: new int[4],
                RawPayload: payload,
                KnownState: default,
                HasWeaponCharge: true,
                WeaponChargePayloadOffset: -1);

            bool resolved = RemoteUserActorPool.TryResolveChargeElementFromTemporaryStats(
                snapshot,
                preferredSkillId: 0,
                out int chargeElement);

            Assert.True(resolved);
            Assert.Equal(2, chargeElement);
        }

        [Fact]
        public void TryResolveChargeSkillIdFromKnownTemporaryStatPayload_PrefersMaskBaseElementValueBeforeFarKnownSkillId()
        {
            int[] words = new int[12];
            words[4] = 5; // mask-base-local WeaponCharge element value
            words[10] = 1211008; // far known charge skill id
            byte[] payload = BuildPayload(words);

            bool resolved = RemoteUserPacketCodec.TryResolveChargeSkillIdFromKnownTemporaryStatPayloadForTesting(
                payload,
                weaponChargeMetadataOffset: -1,
                out int chargeSkillId);

            Assert.True(resolved);
            Assert.Equal(1221004, chargeSkillId);
        }

        private static byte[] BuildPayload(int[] words)
        {
            byte[] payload = new byte[words.Length * sizeof(int)];
            for (int i = 0; i < words.Length; i++)
            {
                byte[] encodedWord = BitConverter.GetBytes(words[i]);
                Buffer.BlockCopy(encodedWord, 0, payload, i * sizeof(int), sizeof(int));
            }

            return payload;
        }
    }
}
