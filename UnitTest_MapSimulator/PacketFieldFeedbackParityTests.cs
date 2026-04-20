using HaCreator.MapSimulator.Interaction;
using System;
using System.Collections.Generic;
using System.Linq;

namespace UnitTest_MapSimulator
{
    public class PacketFieldFeedbackParityTests
    {
        [Fact]
        public void WhisperFindSubtype9_ResultOne_UsesClientLocationChatLogType()
        {
            List<(string Text, int ChatLogType)> chat = new();
            PacketFieldFeedbackCallbacks callbacks = CreateCallbacks(
                addClientChatMessage: (text, chatLogType, _) => chat.Add((text, chatLogType)));

            bool applied = ApplyWhisperPacket(
                PacketFieldFeedbackRuntime.BuildWhisperLocationPayload(9, "Target", 1, 100000000),
                callbacks,
                out _);

            Assert.True(applied);
            Assert.Single(chat);
            Assert.Equal(7, chat[0].ChatLogType);
        }

        [Fact]
        public void WhisperFindSubtype9_NotFound_UsesClientFailureChatLogType()
        {
            List<(string Text, int ChatLogType)> chat = new();
            PacketFieldFeedbackCallbacks callbacks = CreateCallbacks(
                addClientChatMessage: (text, chatLogType, _) => chat.Add((text, chatLogType)));

            bool applied = ApplyWhisperPacket(
                PacketFieldFeedbackRuntime.BuildWhisperLocationPayload(9, "Target", 2, 0),
                callbacks,
                out _);

            Assert.True(applied);
            Assert.Single(chat);
            Assert.Equal(12, chat[0].ChatLogType);
        }

        [Fact]
        public void WhisperFindSubtype9_ChaseTransferRequiresArmedRequest()
        {
            int queueCount = 0;
            PacketFieldFeedbackCallbacks callbacks = CreateCallbacks(
                consumeWhisperChaseTransferRequest: () => false,
                queueMapTransfer: (_, _, _) =>
                {
                    queueCount++;
                    return true;
                });

            byte[] payload = PacketFieldFeedbackRuntime.BuildWhisperLocationPayload(9, "Target", 1, 100000000)
                .Concat(BitConverter.GetBytes(321))
                .Concat(BitConverter.GetBytes(654))
                .ToArray();
            bool applied = ApplyWhisperPacket(payload, callbacks, out _);

            Assert.True(applied);
            Assert.Equal(0, queueCount);
        }

        [Fact]
        public void WhisperFindSubtype9_ChaseTransferQueuesWhenArmed()
        {
            int queueCount = 0;
            int queuedMap = 0;
            int queuedX = 0;
            int queuedY = 0;
            PacketFieldFeedbackCallbacks callbacks = CreateCallbacks(
                consumeWhisperChaseTransferRequest: () => true,
                queueMapTransfer: (mapId, x, y) =>
                {
                    queueCount++;
                    queuedMap = mapId;
                    queuedX = x;
                    queuedY = y;
                    return true;
                });

            byte[] payload = PacketFieldFeedbackRuntime.BuildWhisperLocationPayload(9, "Target", 1, 100000123)
                .Concat(BitConverter.GetBytes(456))
                .Concat(BitConverter.GetBytes(789))
                .ToArray();
            bool applied = ApplyWhisperPacket(payload, callbacks, out _);

            Assert.True(applied);
            Assert.Equal(1, queueCount);
            Assert.Equal(100000123, queuedMap);
            Assert.Equal(456, queuedX);
            Assert.Equal(789, queuedY);
        }

        [Fact]
        public void WhisperFindReplySubtype72_RoutesToUserListOwnerWithoutChat()
        {
            int chatCount = 0;
            int userListUpdateCount = 0;
            int invalidateCount = 0;
            string lastTarget = string.Empty;
            string lastLocation = string.Empty;
            byte lastResult = 0;
            int lastValue = 0;
            int chaseConsumeCount = 0;
            PacketFieldFeedbackCallbacks callbacks = CreateCallbacks(
                addClientChatMessage: (_, _, _) => chatCount++,
                updateWhisperUserListLocation: (target, locationText, result, value) =>
                {
                    userListUpdateCount++;
                    lastTarget = target;
                    lastLocation = locationText;
                    lastResult = result;
                    lastValue = value;
                },
                invalidateWhisperUserListWindow: () => invalidateCount++,
                consumeWhisperChaseTransferRequest: () =>
                {
                    chaseConsumeCount++;
                    return true;
                });

            bool applied = ApplyWhisperPacket(
                PacketFieldFeedbackRuntime.BuildWhisperLocationPayload(72, "Target", 1, 100000000),
                callbacks,
                out _);

            Assert.True(applied);
            Assert.Equal(0, chatCount);
            Assert.Equal(1, userListUpdateCount);
            Assert.Equal(1, invalidateCount);
            Assert.Equal("Target", lastTarget);
            Assert.False(string.IsNullOrWhiteSpace(lastLocation));
            Assert.Equal((byte)1, lastResult);
            Assert.Equal(100000000, lastValue);
            Assert.Equal(0, chaseConsumeCount);
        }

        private static PacketFieldFeedbackCallbacks CreateCallbacks(
            Action<string, int, string> addClientChatMessage = null,
            Func<int, int, int, bool> queueMapTransfer = null,
            Func<bool> consumeWhisperChaseTransferRequest = null,
            Action<string, string, byte, int> updateWhisperUserListLocation = null,
            Action invalidateWhisperUserListWindow = null)
        {
            return new PacketFieldFeedbackCallbacks
            {
                AddClientChatMessage = addClientChatMessage,
                QueueMapTransfer = queueMapTransfer,
                ConsumeWhisperChaseTransferRequest = consumeWhisperChaseTransferRequest,
                UpdateWhisperUserListLocation = updateWhisperUserListLocation,
                InvalidateWhisperUserListWindow = invalidateWhisperUserListWindow,
                ResolveMapName = mapId => mapId == 100000000 ? "Henesys" : string.Empty,
                HasMapTransferTarget = mapId => mapId > 0,
                ResolveChannelName = channel => $"Ch. {channel}"
            };
        }

        private static bool ApplyWhisperPacket(byte[] payload, PacketFieldFeedbackCallbacks callbacks, out string message)
        {
            PacketFieldFeedbackRuntime runtime = new();
            return runtime.TryApplyPacket(
                PacketFieldFeedbackPacketKind.Whisper,
                payload,
                currentTick: 0,
                callbacks,
                out message);
        }
    }
}
