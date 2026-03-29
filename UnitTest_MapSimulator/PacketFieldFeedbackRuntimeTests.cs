using HaCreator.MapSimulator.Interaction;

namespace UnitTest_MapSimulator;

public sealed class PacketFieldFeedbackRuntimeTests
{
    [Fact]
    public void TryApplyPacket_FieldEffectVisualBranches_InvokeExistingCallbacks()
    {
        var runtime = new PacketFieldFeedbackRuntime();
        bool summonShown = false;
        bool screenShown = false;
        bool rouletteShown = false;

        var callbacks = new PacketFieldFeedbackCallbacks
        {
            ShowSummonEffectVisual = (effectId, x, y) =>
            {
                summonShown = effectId == 4 && x == 120 && y == 240;
                return true;
            },
            ShowScreenEffectVisual = descriptor =>
            {
                screenShown = descriptor == "BossClear";
                return true;
            },
            ShowRewardRouletteVisual = (rewardId, step, total) =>
            {
                rouletteShown = rewardId == 3010000 && step == 2 && total == 5;
                return true;
            }
        };

        bool summonApplied = runtime.TryApplyPacket(
            PacketFieldFeedbackPacketKind.FieldEffect,
            PacketFieldFeedbackRuntime.BuildSummonFieldEffectPayload(4, 120, 240),
            currentTick: 1000,
            callbacks,
            out _);
        bool screenApplied = runtime.TryApplyPacket(
            PacketFieldFeedbackPacketKind.FieldEffect,
            PacketFieldFeedbackRuntime.BuildScreenFieldEffectPayload("BossClear"),
            currentTick: 1000,
            callbacks,
            out _);
        bool rouletteApplied = runtime.TryApplyPacket(
            PacketFieldFeedbackPacketKind.FieldEffect,
            PacketFieldFeedbackRuntime.BuildRewardRouletteFieldEffectPayload(3010000, 2, 5),
            currentTick: 1000,
            callbacks,
            out _);

        Assert.True(summonApplied);
        Assert.True(screenApplied);
        Assert.True(rouletteApplied);
        Assert.True(summonShown);
        Assert.True(screenShown);
        Assert.True(rouletteShown);
    }

    [Fact]
    public void TryApplyPacket_BossTimersRemainChatOnlyStatus()
    {
        var runtime = new PacketFieldFeedbackRuntime();
        string lastChat = string.Empty;

        var callbacks = new PacketFieldFeedbackCallbacks
        {
            AddClientChatMessage = (text, _, _) => lastChat = text
        };

        bool applied = runtime.TryApplyPacket(
            PacketFieldFeedbackPacketKind.ZakumTimer,
            PacketFieldFeedbackRuntime.BuildBossTimerPayload(0, 30),
            currentTick: 1000,
            callbacks,
            out string message);

        Assert.True(applied);
        Assert.Equal("Applied packet-owned Zakum timer feedback.", message);
        Assert.Equal("[System] Zakum timer update: 30 remaining.", lastChat);
        Assert.Contains("bosstimer=\"Zakum timer update: 30 remaining.\"", runtime.DescribeStatus(1000));
        Assert.Contains("bosstimer=\"Zakum timer update: 30 remaining.\"", runtime.DescribeStatus(32000));
    }
}
