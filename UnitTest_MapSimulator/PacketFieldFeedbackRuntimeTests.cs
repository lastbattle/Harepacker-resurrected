using HaCreator.MapSimulator.Interaction;
using System.Collections.Generic;
using Xunit;

namespace UnitTest_MapSimulator;

public class PacketFieldFeedbackRuntimeTests
{
    [Fact]
    public void ApplyIncomingWhisper_AddsSwindleWarningChat()
    {
        PacketFieldFeedbackRuntime runtime = new();
        List<(string Text, int ChatType)> messages = new();

        bool handled = runtime.TryApplyPacket(
            PacketFieldFeedbackPacketKind.Whisper,
            PacketFieldFeedbackRuntime.BuildIncomingWhisperPayload("Trader", 3, fromAdmin: false, "visit www scam.com to buy mesos"),
            currentTick: 100,
            new PacketFieldFeedbackCallbacks
            {
                AddClientChatMessage = (text, chatType, _) => messages.Add((text, chatType))
            },
            out _);

        Assert.True(handled);
        Assert.Collection(
            messages,
            entry => Assert.Equal(16, entry.ChatType),
            entry =>
            {
                Assert.Equal(8, entry.ChatType);
                Assert.Contains("scam", entry.Text);
            });
    }

    [Fact]
    public void ApplyPartyGroupMessage_AddsSwindleWarningButFriendChatDoesNot()
    {
        PacketFieldFeedbackRuntime runtime = new();
        List<(string Text, int ChatType)> messages = new();
        PacketFieldFeedbackCallbacks callbacks = new()
        {
            AddClientChatMessage = (text, chatType, _) => messages.Add((text, chatType))
        };

        bool handledParty = runtime.TryApplyPacket(
            PacketFieldFeedbackPacketKind.GroupMessage,
            PacketFieldFeedbackRuntime.BuildGroupMessagePayload(1, "Leader", "drop first for account trade"),
            currentTick: 100,
            callbacks,
            out _);
        bool handledFriend = runtime.TryApplyPacket(
            PacketFieldFeedbackPacketKind.GroupMessage,
            PacketFieldFeedbackRuntime.BuildGroupMessagePayload(0, "Buddy", "drop first for account trade"),
            currentTick: 101,
            callbacks,
            out _);

        Assert.True(handledParty);
        Assert.True(handledFriend);
        Assert.Equal(3, messages.Count);
        Assert.Equal(2, messages[0].ChatType);
        Assert.Equal(8, messages[1].ChatType);
        Assert.Equal(3, messages[2].ChatType);
    }

    [Fact]
    public void ApplySwindleWarnings_RespectsClientLikeCooldown()
    {
        PacketFieldFeedbackRuntime runtime = new();
        List<(string Text, int ChatType)> messages = new();
        PacketFieldFeedbackCallbacks callbacks = new()
        {
            AddClientChatMessage = (text, chatType, _) => messages.Add((text, chatType))
        };

        runtime.TryApplyPacket(
            PacketFieldFeedbackPacketKind.Whisper,
            PacketFieldFeedbackRuntime.BuildIncomingWhisperPayload("Trader", 1, fromAdmin: false, "password and mesos"),
            currentTick: 100,
            callbacks,
            out _);
        runtime.TryApplyPacket(
            PacketFieldFeedbackPacketKind.Whisper,
            PacketFieldFeedbackRuntime.BuildIncomingWhisperPayload("Trader", 1, fromAdmin: false, "paypal and account"),
            currentTick: 5000,
            callbacks,
            out _);
        runtime.TryApplyPacket(
            PacketFieldFeedbackPacketKind.Whisper,
            PacketFieldFeedbackRuntime.BuildIncomingWhisperPayload("Trader", 1, fromAdmin: false, "website and account"),
            currentTick: 10100,
            callbacks,
            out _);

        int warningCount = 0;
        foreach ((_, int chatType) in messages)
        {
            if (chatType == 8)
            {
                warningCount++;
            }
        }

        Assert.Equal(2, warningCount);
    }

    [Fact]
    public void ApplySummonFieldEffect_RequestsSummonSound()
    {
        PacketFieldFeedbackRuntime runtime = new();
        byte? playedEffectId = null;
        bool visualRequested = false;

        bool handled = runtime.TryApplyPacket(
            PacketFieldFeedbackPacketKind.FieldEffect,
            PacketFieldFeedbackRuntime.BuildSummonFieldEffectPayload(13, 120, 240),
            currentTick: 100,
            new PacketFieldFeedbackCallbacks
            {
                ShowSummonEffectVisual = (_, _, _) =>
                {
                    visualRequested = true;
                    return true;
                },
                PlaySummonEffectSound = effectId =>
                {
                    playedEffectId = effectId;
                    return true;
                }
            },
            out _);

        Assert.True(handled);
        Assert.True(visualRequested);
        Assert.Equal((byte)13, playedEffectId);
    }
}
