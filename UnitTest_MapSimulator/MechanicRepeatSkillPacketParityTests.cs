using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Managers;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework.Graphics;

namespace UnitTest_MapSimulator
{
    public sealed class MechanicRepeatSkillPacketParityTests
    {
        private const int TankSiegeSkillId = 35121013;
        private const int TankModeReturnSkillId = 35120013;
        private const int Sg88SkillId = 35121003;

        private static readonly FieldInfo ActiveRepeatSkillSustainField = typeof(SkillManager).GetField(
            "_activeRepeatSkillSustain",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("SkillManager._activeRepeatSkillSustain was not found.");

        private static readonly FieldInfo SummonsField = typeof(SkillManager).GetField(
            "_summons",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("SkillManager._summons was not found.");

        private static readonly Type RepeatSkillSustainStateType = typeof(SkillManager).GetNestedType(
            "RepeatSkillSustainState",
            BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("SkillManager.RepeatSkillSustainState was not found.");

        [Fact]
        public void PacketOwnedMechanicRepeatSkillRuntime_DecodesRepeatSkillModeEndAck()
        {
            byte[] payload = CreatePayload(writer =>
            {
                writer.Write(TankSiegeSkillId);
                writer.Write(TankModeReturnSkillId);
                writer.Write(123456);
            });

            bool decoded = PacketOwnedMechanicRepeatSkillRuntime.TryDecodeRepeatSkillModeEndAck(
                payload,
                out PacketOwnedRepeatSkillModeEndAck ack,
                out string error);

            Assert.True(decoded, error);
            Assert.Equal(TankSiegeSkillId, ack.SkillId);
            Assert.Equal(TankModeReturnSkillId, ack.ReturnSkillId);
            Assert.Equal(123456, ack.RequestedAt);
        }

        [Fact]
        public void PacketOwnedMechanicRepeatSkillRuntime_RejectsSg88ConfirmWithoutRequestTick()
        {
            byte[] payload = CreatePayload(writer =>
            {
                writer.Write(77);
                writer.Write(int.MinValue);
            });

            bool decoded = PacketOwnedMechanicRepeatSkillRuntime.TryDecodeSg88ManualAttackConfirm(
                payload,
                out _,
                out string error);

            Assert.False(decoded);
            Assert.Contains("request tick", error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void TryAcknowledgeRepeatSkillModeEndRequest_RequiresMatchingPendingRequestTick()
        {
            SkillManager manager = CreateManager();
            object sustain = RuntimeHelpers.GetUninitializedObject(RepeatSkillSustainStateType);
            SetMemberValue(sustain, "SkillId", TankSiegeSkillId);
            SetMemberValue(sustain, "ReturnSkillId", TankModeReturnSkillId);
            SetMemberValue(sustain, "PendingModeEndRequest", true);
            SetMemberValue(sustain, "PendingModeEndRequestTime", 777);
            ActiveRepeatSkillSustainField.SetValue(manager, sustain);

            bool mismatched = manager.TryAcknowledgeRepeatSkillModeEndRequest(
                TankSiegeSkillId,
                currentTime: 900,
                requestedAt: 778);
            bool matched = manager.TryAcknowledgeRepeatSkillModeEndRequest(
                TankSiegeSkillId,
                currentTime: 900,
                requestedAt: 777);

            Assert.False(mismatched);
            Assert.True(matched);
        }

        [Fact]
        public void TryResolvePendingSg88ManualAttackRequest_ClearsBookkeepingOnlyForMatchingRequest()
        {
            SkillManager manager = CreateManager();
            ActiveSummon summon = new()
            {
                ObjectId = 42,
                SkillId = Sg88SkillId,
                AssistType = SummonAssistType.ManualAttack,
                PendingManualAttackRequest = true,
                PendingManualAttackRequestedAt = 1200,
                PendingManualAttackPrimaryTargetMobId = 9300184,
                PendingManualAttackTargetMobIds = new[] { 9300184, 9300185 },
                PendingManualAttackFollowUpAt = 1320
            };

            GetSummons(manager).Add(summon);

            bool mismatched = manager.TryResolvePendingSg88ManualAttackRequest(
                summonObjectId: 42,
                requestedAt: 1199,
                currentTime: 1400);

            Assert.False(mismatched);
            Assert.True(summon.PendingManualAttackRequest);

            bool matched = manager.TryResolvePendingSg88ManualAttackRequest(
                summonObjectId: 42,
                requestedAt: 1200,
                currentTime: 1450);

            Assert.True(matched);
            Assert.False(summon.PendingManualAttackRequest);
            Assert.Equal(int.MinValue, summon.PendingManualAttackRequestedAt);
            Assert.Equal(1450, summon.LastManualAttackResolvedTime);
            Assert.Empty(summon.PendingManualAttackTargetMobIds);
        }

        private static SkillManager CreateManager()
        {
            SkillLoader loader = (SkillLoader)RuntimeHelpers.GetUninitializedObject(typeof(SkillLoader));
            PlayerCharacter player = new((GraphicsDevice)null!, (TexturePool)null!, build: null);
            return new SkillManager(loader, player);
        }

        private static List<ActiveSummon> GetSummons(SkillManager manager)
        {
            return (List<ActiveSummon>)(SummonsField.GetValue(manager)
                ?? throw new InvalidOperationException("SkillManager._summons was null."));
        }

        private static byte[] CreatePayload(Action<BinaryWriter> write)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream);
            write(writer);
            writer.Flush();
            return stream.ToArray();
        }

        private static void SetMemberValue(object instance, string memberName, object value)
        {
            PropertyInfo property = instance.GetType().GetProperty(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                property.SetValue(instance, value);
                return;
            }

            FieldInfo field = instance.GetType().GetField(
                $"<{memberName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new InvalidOperationException($"Member {memberName} was not found.");
            }

            field.SetValue(instance, value);
        }
    }
}
