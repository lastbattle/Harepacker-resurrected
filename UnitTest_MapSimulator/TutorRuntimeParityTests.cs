using System;
using System.IO;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Pools;
using Xunit;

namespace UnitTest_MapSimulator;

public sealed class TutorRuntimeParityTests
{
    [Fact]
    public void SnapshotActiveDisplayTutorVariants_RehireKeepsClientTutorSlotOrderAcrossOwners()
    {
        ResetSharedTutorState();
        try
        {
            var runtime = new TutorRuntime();
            runtime.ApplySharedHireRequestForCharacter(
                TutorRuntime.CygnusTutorSkillId,
                TutorRuntime.CygnusTutorHeight,
                currentTick: 1000,
                runtimeCharacterId: 111111);
            runtime.ApplySharedHireRequestForCharacter(
                TutorRuntime.AranTutorSkillId,
                TutorRuntime.AranTutorHeight,
                currentTick: 1001,
                runtimeCharacterId: 222222);
            runtime.ApplySharedHireRequestForCharacter(
                TutorRuntime.CygnusTutorSkillId,
                TutorRuntime.CygnusTutorHeight,
                currentTick: 1002,
                runtimeCharacterId: 111111);

            var variants = runtime.SnapshotActiveDisplayTutorVariants();

            int aranIndex = FindFirstSkillIndex(variants, TutorRuntime.AranTutorSkillId);
            int cygnusIndex = FindFirstSkillIndex(variants, TutorRuntime.CygnusTutorSkillId);
            Assert.True(aranIndex >= 0, "Aran tutor variant should be present.");
            Assert.True(cygnusIndex >= 0, "Cygnus tutor variant should be present.");
            Assert.True(
                aranIndex < cygnusIndex,
                $"Expected Aran slot order before Cygnus after Cygnus rehire, but got Aran={aranIndex}, Cygnus={cygnusIndex}.");
        }
        finally
        {
            ResetSharedTutorState();
        }
    }

    [Fact]
    public void RemoteUserOfficialSessionBridge_NonV95Build_AllowsTutorInferenceWithoutV95OwnerTableShortcut()
    {
        using var bridge = new RemoteUserOfficialSessionBridgeManager();
        byte[] firstRawPacket = BuildRemoteTutorHireRawPacket(opcode: 260, characterId: 300001, enabled: true);
        byte[] secondRawPacket = BuildRemoteTutorHireRawPacket(opcode: 260, characterId: 300001, enabled: false);

        bool firstDecoded = bridge.TryDecodeInboundRemoteUserPacketForTesting(
            firstRawPacket,
            "official-session:v96:127.0.0.1",
            out _);
        bool secondDecoded = bridge.TryDecodeInboundRemoteUserPacketForTesting(
            secondRawPacket,
            "official-session:v96:127.0.0.1",
            out RemoteUserOfficialSessionBridgeMessage mappedMessage);

        Assert.False(firstDecoded);
        Assert.True(secondDecoded);
        Assert.NotNull(mappedMessage);
        Assert.Equal(260, mappedMessage.Opcode);
        Assert.Equal((int)RemoteUserPacketType.UserTutorHire, mappedMessage.PacketType);
    }

    [Fact]
    public void RemoteUserOfficialSessionBridge_V95Build_KnownNonTutorOpcodeStaysBlocked()
    {
        using var bridge = new RemoteUserOfficialSessionBridgeManager();
        byte[] rawPacket = BuildRemoteTutorHireRawPacket(opcode: 260, characterId: 300001, enabled: true);

        bool decoded = bridge.TryDecodeInboundRemoteUserPacketForTesting(
            rawPacket,
            "official-session:v95:127.0.0.1",
            out _);

        Assert.False(decoded);
    }

    private static void ResetSharedTutorState()
    {
        TutorRuntime.ResetSharedClientTutorSkillSlots();
        TutorRuntime.ResetSharedRegisteredTutorVariants();
        TutorRuntime.ResetSharedTutorMessages();
    }

    private static int FindFirstSkillIndex(System.Collections.Generic.IReadOnlyList<TutorVariantSnapshot> variants, int skillId)
    {
        for (int i = 0; i < variants.Count; i++)
        {
            if (variants[i].SkillId == skillId)
            {
                return i;
            }
        }

        return -1;
    }

    private static byte[] BuildRemoteTutorHireRawPacket(ushort opcode, int characterId, bool enabled)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(opcode);
        writer.Write(characterId);
        writer.Write((byte)(enabled ? 1 : 0));
        writer.Flush();
        return stream.ToArray();
    }
}
