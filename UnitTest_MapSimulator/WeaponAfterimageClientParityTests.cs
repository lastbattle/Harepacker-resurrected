using HaCreator.MapSimulator.Pools;
using System.Reflection;

namespace UnitTest_MapSimulator
{
    public sealed class WeaponAfterimageClientParityTests
    {
        private const int CharacterId = 321654987;
        private const int PageFireChargeSkillId = 1211004;
        private const int BlastSkillId = 1221009;
        private const int EvanDragonBreathSkillId = 22121000;

        private static readonly MethodInfo ApplyResetMaskMethod =
            typeof(RemoteUserPacketCodec).GetMethod(
                "ApplyResetMask",
                BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("RemoteUserPacketCodec.ApplyResetMask was not found.");

        [Fact]
        public void TryParseTemporaryStatSet_DecodesScopedWeaponChargeSkillId()
        {
            byte[] payload = BuildTemporaryStatSetPacket(
                CharacterId,
                maskWord0: 1 << 2,
                encodedPayloadBytes:
                [
                    ..Int32(0),
                    ..Int32(PageFireChargeSkillId)
                ],
                delay: 9);

            bool parsed = RemoteUserPacketCodec.TryParseTemporaryStatSet(payload, out RemoteUserTemporaryStatSetPacket packet, out string? error);

            Assert.True(parsed, error);
            Assert.Equal(PageFireChargeSkillId, packet.TemporaryStats.KnownState.ChargeSkillId);
        }

        [Fact]
        public void ApplyResetMask_ClearsKnownStateChargeSkillIdWhenWeaponChargeBitDrops()
        {
            byte[] payload = BuildTemporaryStatSetPacket(
                CharacterId,
                maskWord0: (1 << 0) | (1 << 2),
                encodedPayloadBytes:
                [
                    45,
                    ..Int32(0),
                    ..Int32(PageFireChargeSkillId)
                ],
                delay: 12);
            Assert.True(RemoteUserPacketCodec.TryParseTemporaryStatSet(payload, out RemoteUserTemporaryStatSetPacket packet, out string? error), error);

            RemoteUserTemporaryStatSnapshot maskedSnapshot = ApplyResetMask(
                packet.TemporaryStats,
                [1, 0, 0, 0]);

            Assert.Equal(45, maskedSnapshot.KnownState.Speed);
            Assert.Null(maskedSnapshot.KnownState.ChargeSkillId);
        }

        [Fact]
        public void TryParseMeleeAttack_OfficialPacket_PreservesEnvelopeAndMobHits()
        {
            const int actionCode = 17;
            const int bulletItemId = 2070001;
            byte[] payload = BuildOfficialMeleeAttackPacket(
                CharacterId,
                BlastSkillId,
                actionCode,
                hitCount: 1,
                damagePerMob: 2,
                actionSpeed: 9,
                masteryPercent: 85,
                bulletItemId: bulletItemId,
                serialAttackFlags: 0x20,
                mobHits:
                [
                    new OfficialMobHit(
                        mobId: 9300184,
                        hitAction: 6,
                        damageEntries:
                        [
                            new OfficialDamageEntry(0x81, 12345),
                            new OfficialDamageEntry(0x01, 23456)
                        ])
                ]);

            bool parsed = RemoteUserPacketCodec.TryParseMeleeAttack(payload, out RemoteUserMeleeAttackPacket packet, out string? error);

            Assert.True(parsed, error);
            Assert.Equal(1, packet.HitCount);
            Assert.Equal(2, packet.DamagePerMob);
            Assert.Equal(9, packet.ActionSpeed);
            Assert.Equal(bulletItemId, packet.BulletItemId);
            Assert.Equal((byte?)0x20, packet.SerialAttackFlags);
            Assert.True(packet.IsSerialAttack);
            Assert.Equal(actionCode, packet.ActionCode);
            Assert.Single(packet.MobHits);

            RemoteUserMeleeAttackMobHit mobHit = packet.MobHits[0];
            Assert.Equal(9300184, mobHit.MobId);
            Assert.Equal((byte)6, mobHit.HitAction);
            Assert.Equal(2, mobHit.DamageEntries.Count);
            Assert.Equal((byte?)0x81, mobHit.DamageEntries[0].HitFlag);
            Assert.Equal(12345, mobHit.DamageEntries[0].Damage);
            Assert.Equal((byte?)0x01, mobHit.DamageEntries[1].HitFlag);
            Assert.Equal(23456, mobHit.DamageEntries[1].Damage);
        }

        [Fact]
        public void TryParseMeleeAttack_OfficialPacket_PreservesReleaseFollowUpValue()
        {
            byte[] payload = BuildOfficialMeleeAttackPacket(
                CharacterId,
                EvanDragonBreathSkillId,
                actionCode: 59,
                hitCount: 1,
                damagePerMob: 1,
                actionSpeed: 7,
                masteryPercent: 66,
                bulletItemId: 0,
                serialAttackFlags: 0,
                mobHits:
                [
                    new OfficialMobHit(
                        mobId: 9300185,
                        hitAction: 3,
                        damageEntries:
                        [
                            new OfficialDamageEntry(0x00, 54321)
                        ])
                ],
                preparedSkillReleaseFollowUpValue: 1440);

            bool parsed = RemoteUserPacketCodec.TryParseMeleeAttack(payload, out RemoteUserMeleeAttackPacket packet, out string? error);

            Assert.True(parsed, error);
            Assert.Equal(1440, packet.PreparedSkillReleaseFollowUpValue);
            Assert.Single(packet.MobHits);
            Assert.Single(packet.MobHits[0].DamageEntries);
            Assert.Equal(54321, packet.MobHits[0].DamageEntries[0].Damage);
        }

        private static RemoteUserTemporaryStatSnapshot ApplyResetMask(
            RemoteUserTemporaryStatSnapshot snapshot,
            int[] remainingMaskWords)
        {
            object? result = ApplyResetMaskMethod.Invoke(null, [snapshot, remainingMaskWords]);
            return Assert.IsType<RemoteUserTemporaryStatSnapshot>(result);
        }

        private static byte[] BuildTemporaryStatSetPacket(
            int characterId,
            int maskWord0,
            byte[] encodedPayloadBytes,
            ushort delay)
        {
            List<byte> bytes = new();
            bytes.AddRange(Int32(characterId));
            bytes.AddRange(Int32(maskWord0));
            bytes.AddRange(Int32(0));
            bytes.AddRange(Int32(0));
            bytes.AddRange(Int32(0));
            bytes.AddRange(encodedPayloadBytes);
            bytes.AddRange(UInt16(delay));
            return bytes.ToArray();
        }

        private static byte[] BuildOfficialMeleeAttackPacket(
            int characterId,
            int skillId,
            int actionCode,
            int hitCount,
            int damagePerMob,
            int actionSpeed,
            int masteryPercent,
            int bulletItemId,
            byte serialAttackFlags,
            IReadOnlyList<OfficialMobHit> mobHits,
            int? preparedSkillReleaseFollowUpValue = null)
        {
            List<byte> bytes = new();
            bytes.AddRange(Int32(characterId));
            bytes.Add((byte)((hitCount << 4) | (damagePerMob & 0x0F)));
            bytes.Add(200);
            bytes.Add(1);
            bytes.AddRange(Int32(skillId));
            bytes.Add(serialAttackFlags);
            bytes.AddRange(UInt16((ushort)actionCode));
            bytes.Add((byte)actionSpeed);
            bytes.Add((byte)masteryPercent);
            bytes.AddRange(Int32(bulletItemId));

            foreach (OfficialMobHit mobHit in mobHits)
            {
                bytes.AddRange(Int32(mobHit.MobId));
                if (mobHit.MobId == 0)
                {
                    continue;
                }

                bytes.Add(mobHit.HitAction);
                foreach (OfficialDamageEntry damageEntry in mobHit.DamageEntries)
                {
                    bytes.Add(damageEntry.HitFlag);
                    bytes.AddRange(Int32(damageEntry.Damage));
                }
            }

            if (preparedSkillReleaseFollowUpValue.HasValue)
            {
                bytes.AddRange(Int32(preparedSkillReleaseFollowUpValue.Value));
            }

            return bytes.ToArray();
        }

        private static byte[] Int32(int value) => BitConverter.GetBytes(value);

        private static byte[] UInt16(ushort value) => BitConverter.GetBytes(value);

        private readonly record struct OfficialMobHit(
            int MobId,
            byte HitAction,
            IReadOnlyList<OfficialDamageEntry> DamageEntries);

        private readonly record struct OfficialDamageEntry(byte HitFlag, int Damage);
    }
}
