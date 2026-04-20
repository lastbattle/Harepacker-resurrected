using HaCreator.MapSimulator.Interaction;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace UnitTest_MapSimulator;

public sealed class PacketFieldStateObjectStateDecodeParityTests
{
    [Fact]
    public void TryApplyPacket_OnSetObjectState_RejectsNegativeStateBelowReplaySentinel()
    {
        PacketFieldStateRuntime runtime = new();
        int publishCalls = 0;

        bool applied = runtime.TryApplyPacket(
            169,
            BuildObjectStatePayload("gateTag", -2),
            currentTick: 100,
            (tag, enabled, transition, now) =>
            {
                publishCalls++;
                return true;
            },
            _ => null,
            null,
            out string message);

        Assert.False(applied);
        Assert.Equal(0, publishCalls);
        Assert.Contains("state -2", message);
    }

    [Fact]
    public void TryApplyPacket_OnSetObjectState_InvalidNegativeState_DoesNotOverwriteCachedPacketState()
    {
        PacketFieldStateRuntime runtime = new();
        List<bool?> publishedStates = new();

        bool initialApplied = runtime.TryApplyPacket(
            169,
            BuildObjectStatePayload("doorTag", 1),
            currentTick: 100,
            (tag, enabled, transition, now) =>
            {
                publishedStates.Add(enabled);
                return true;
            },
            _ => null,
            null,
            out _);

        bool invalidApplied = runtime.TryApplyPacket(
            169,
            BuildObjectStatePayload("doorTag", -2),
            currentTick: 101,
            (tag, enabled, transition, now) =>
            {
                publishedStates.Add(enabled);
                return true;
            },
            _ => null,
            null,
            out _);

        bool replayApplied = runtime.TryApplyPacket(
            169,
            BuildObjectStatePayload("doorTag", -1),
            currentTick: 102,
            (tag, enabled, transition, now) =>
            {
                publishedStates.Add(enabled);
                return true;
            },
            _ => null,
            null,
            out _);

        Assert.True(initialApplied);
        Assert.False(invalidApplied);
        Assert.True(replayApplied);
        Assert.Equal(new bool?[] { true, true }, publishedStates);
    }

    private static byte[] BuildObjectStatePayload(string tag, int state)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);

        byte[] tagBytes = Encoding.Default.GetBytes(tag ?? string.Empty);
        writer.Write((ushort)tagBytes.Length);
        writer.Write(tagBytes);
        writer.Write(state);
        writer.Flush();
        return stream.ToArray();
    }
}
