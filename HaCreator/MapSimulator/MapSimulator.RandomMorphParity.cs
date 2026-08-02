using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;
using MapleLib.PacketLib;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int RandomMorphRequestOpcode = 184;
        private const int RandomMorphBlockedThrottleMs = 500;

        private bool _randomMorphRequestSent;
        private int _randomMorphRequestSentTick = int.MinValue;
        private int _lastRandomMorphOutboundOpcode = -1;
        private byte[] _lastRandomMorphOutboundPayload = Array.Empty<byte>();
        private string _lastRandomMorphOutboundSummary = "Random morph outbound idle.";

        internal readonly record struct RandomMorphInboundResultResolutionForTesting(
            bool RequestSent,
            int RequestSentTick,
            byte ResultCode,
            byte DetailCode,
            string ClientNotice);

        private void WireRandomMorphWindow()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.RandomMorph) is not RandomMorphWindow randomMorphWindow)
            {
                return;
            }

            randomMorphWindow.SetFont(_fontChat);
            randomMorphWindow.MorphRequestSubmitted = HandleRandomMorphDialogRequestSubmitted;
        }

        private bool TryUseRandomMorphInventoryItem(int itemId, InventoryType inventoryType, int currentTime, int? slotIndex = null)
        {
            if (!IsRandomMorphDialogItem(itemId)
                || inventoryType == InventoryType.NONE
                || uiWindowManager?.InventoryWindow is not UI.IInventoryRuntime inventoryWindow)
            {
                return false;
            }

            string fieldItemRestrictionMessage = GetFieldItemUseRestrictionMessage(inventoryType, itemId, 1);
            if (!string.IsNullOrWhiteSpace(fieldItemRestrictionMessage))
            {
                ShowFieldRestrictionMessage(fieldItemRestrictionMessage);
                return true;
            }

            int slotPosition = ResolveRandomMorphInventoryPosition(inventoryWindow, inventoryType, itemId, slotIndex);
            if (slotPosition <= 0)
            {
                return false;
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.RandomMorph) is not RandomMorphWindow randomMorphWindow)
            {
                _lastRandomMorphOutboundSummary = "Random morph dialog owner is unavailable in this UI build.";
                return true;
            }

            if (randomMorphWindow.IsVisible)
            {
                _lastRandomMorphOutboundSummary =
                    $"Random morph dialog launch for item {itemId} stayed suppressed because the Random Morph owner already holds the unique modeless slot.";
                return true;
            }

            string blockingOwner = GetVisibleUniqueModelessOwner(MapSimulatorWindowNames.RandomMorph);
            if (!string.IsNullOrWhiteSpace(blockingOwner))
            {
                _lastRandomMorphOutboundSummary = $"Random morph dialog launch for item {itemId} stayed suppressed because unique modeless owner '{blockingOwner}' is already visible.";
                return true;
            }

            randomMorphWindow.PrepareForShow(slotPosition, itemId);
            ShowWindow(MapSimulatorWindowNames.RandomMorph, randomMorphWindow, trackDirectionModeOwner: true);
            _fieldRuleRuntime?.RegisterSuccessfulItemUse(
                ShouldTrackFieldConsumeItemCooldown(inventoryType, default, default),
                currentTime);
            return true;
        }

        private bool HandleRandomMorphDialogRequestSubmitted(RandomMorphDialogRequest request)
        {
            int currentTick = Environment.TickCount;
            string targetName = request.TargetName ?? string.Empty;
            if (targetName.Length <= 0)
            {
                _lastRandomMorphOutboundSummary = "Random morph send request stayed local because the target name was empty.";
                return false;
            }

            if (IsRandomMorphRequestBlocked(currentTick))
            {
                string blockedNotice = RandomMorphDialogText.GetRequestBlockedNotice();
                ShowUtilityFeedbackMessage(blockedNotice);
                _lastRandomMorphOutboundSummary =
                    $"Random morph send request for item {request.ItemId} to '{targetName}' was blocked by the recovered CWvsContext request latch/throttle. {blockedNotice}";
                return false;
            }

            byte[] payload = BuildRandomMorphRequestPayload(currentTick, request.InventoryPosition, request.ItemId, targetName);
            _lastRandomMorphOutboundOpcode = RandomMorphRequestOpcode;
            _lastRandomMorphOutboundPayload = payload;
            _lastRandomMorphOutboundSummary = DispatchRandomMorphRequest(request, payload);
            MarkRandomMorphRequestSent(currentTick);
            ShowUtilityFeedbackMessage(_lastRandomMorphOutboundSummary);
            return true;
        }

        private bool IsRandomMorphDialogItem(int itemId)
        {
            return itemId > 0 && InventoryItemMetadataResolver.IsRandomMorphItem(itemId);
        }

        private int ResolveRandomMorphInventoryPosition(
            UI.IInventoryRuntime inventoryWindow,
            InventoryType inventoryType,
            int itemId,
            int? preferredSlotIndex)
        {
            if (inventoryWindow == null || inventoryType == InventoryType.NONE || itemId <= 0)
            {
                return 0;
            }

            IReadOnlyList<UI.InventorySlotData> slots = inventoryWindow.GetSlots(inventoryType);
            if (slots == null || slots.Count <= 0)
            {
                return 0;
            }

            if (preferredSlotIndex is int lockedSlotIndex
                && lockedSlotIndex >= 0
                && lockedSlotIndex < slots.Count)
            {
                UI.InventorySlotData preferredSlot = slots[lockedSlotIndex];
                if (preferredSlot?.ItemId == itemId
                    && !preferredSlot.IsDisabled
                    && Math.Max(0, preferredSlot.Quantity) > 0)
                {
                    return lockedSlotIndex + 1;
                }
            }

            for (int i = 0; i < slots.Count; i++)
            {
                UI.InventorySlotData slot = slots[i];
                if (slot?.ItemId != itemId || slot.IsDisabled || Math.Max(0, slot.Quantity) <= 0)
                {
                    continue;
                }

                return i + 1;
            }

            return 0;
        }

        private bool IsRandomMorphRequestBlocked(int currentTick)
        {
            if (_randomMorphRequestSent)
            {
                return true;
            }

            return _randomMorphRequestSentTick != int.MinValue
                && unchecked(currentTick - _randomMorphRequestSentTick) < RandomMorphBlockedThrottleMs;
        }

        private void MarkRandomMorphRequestSent(int currentTick)
        {
            _randomMorphRequestSent = true;
            _randomMorphRequestSentTick = currentTick;
        }

        private void ClearRandomMorphRequestLatch(int currentTick)
        {
            _randomMorphRequestSent = false;
            _randomMorphRequestSentTick = currentTick;
        }

        private bool TryApplyRandomMorphResultPayload(byte[] payload, out string message)
        {
            RandomMorphInboundResultResolutionForTesting resolution =
                ResolveRandomMorphInboundResultForTesting(Environment.TickCount, payload);
            ClearRandomMorphRequestLatch(resolution.RequestSentTick);
            bool consumedQuestStartLatch = TryConsumePacketOwnedQuestResultStartQuestLatchFromSharedExclusiveReset();

            string summary =
                $"CWvsContext::OnRandomMorphRes(123) cleared the recovered exclusive-request latch and refreshed the throttle timestamp from inbound result code {resolution.ResultCode}.";
            if (consumedQuestStartLatch)
            {
                summary = $"{summary} The same shared exclusive-reset event also cleared the packet-owned StartQuest follow-up latch.";
            }

            message = string.IsNullOrWhiteSpace(resolution.ClientNotice)
                ? summary
                : $"{summary} {resolution.ClientNotice}";
            return true;
        }

        private bool TryApplyRandomMorphRequestAckPayload(byte[] payload, out string message)
        {
            int currentTick = Environment.TickCount;
            ClearRandomMorphRequestLatch(currentTick);
            bool consumedQuestStartLatch = TryConsumePacketOwnedQuestResultStartQuestLatchFromSharedExclusiveReset();
            message = payload == null || payload.Length == 0
                ? "Random morph request ack cleared the recovered exclusive-request latch and refreshed the 500 ms throttle timestamp through the simulator packet seam."
                : $"Random morph request ack cleared the recovered exclusive-request latch and refreshed the 500 ms throttle timestamp with {payload.Length} byte(s) of simulator-local result context.";
            if (consumedQuestStartLatch)
            {
                message = $"{message} The same shared exclusive-reset event also cleared the packet-owned StartQuest follow-up latch.";
            }

            return true;
        }

        internal static RandomMorphInboundResultResolutionForTesting ResolveRandomMorphInboundResultForTesting(int currentTick, byte[] payload)
        {
            payload ??= Array.Empty<byte>();
            byte resultCode = payload.Length > 0 ? payload[0] : (byte)0;
            byte detailCode = payload.Length > 1 ? payload[1] : (byte)0;
            string clientNotice = ResolveRandomMorphClientNotice(payload, resultCode, detailCode);
            return new RandomMorphInboundResultResolutionForTesting(
                RequestSent: false,
                RequestSentTick: currentTick,
                ResultCode: resultCode,
                DetailCode: detailCode,
                ClientNotice: clientNotice);
        }

        internal static string ResolveRandomMorphClientNoticeForTesting(byte[] payload)
        {
            payload ??= Array.Empty<byte>();
            byte resultCode = payload.Length > 0 ? payload[0] : (byte)0;
            byte detailCode = payload.Length > 1 ? payload[1] : (byte)0;
            return ResolveRandomMorphClientNotice(payload, resultCode, detailCode);
        }

        private static string ResolveRandomMorphClientNotice(byte[] payload, byte resultCode, byte detailCode)
        {
            if (resultCode != 1)
            {
                return null;
            }

            return detailCode switch
            {
                2 => TryDecodeRandomMorphTargetName(payload, out string targetName)
                    ? RandomMorphDialogText.FormatTargetNotFoundNotice(targetName)
                    : RandomMorphDialogText.FormatTargetNotFoundNotice(string.Empty),
                3 => RandomMorphDialogText.GetTownOnlyNotice(appendFallbackSuffix: false),
                _ => null
            };
        }

        private static bool TryDecodeRandomMorphTargetName(byte[] payload, out string targetName)
        {
            targetName = string.Empty;
            payload ??= Array.Empty<byte>();
            const int stringOffset = sizeof(byte) + sizeof(byte);
            if (payload.Length < stringOffset + sizeof(ushort))
            {
                return false;
            }

            ushort byteLength = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(stringOffset, sizeof(ushort)));
            int contentOffset = stringOffset + sizeof(ushort);
            if (payload.Length < contentOffset + byteLength)
            {
                return false;
            }

            targetName = Encoding.Default.GetString(payload, contentOffset, byteLength);
            return true;
        }

        internal static byte[] BuildRandomMorphRequestPayload(int currentTick, int inventoryPosition, int itemId, string targetName)
        {
            string normalizedTargetName = targetName ?? string.Empty;
            byte[] encodedTargetName = Encoding.Default.GetBytes(normalizedTargetName);
            int targetLength = Math.Min(encodedTargetName.Length, ushort.MaxValue);
            using PacketWriter writer = new();
            writer.WriteInt(currentTick);
            writer.Write((short)Math.Clamp(inventoryPosition, 0, short.MaxValue));
            writer.WriteInt(itemId);
            writer.Write((ushort)targetLength);
            writer.Write(encodedTargetName, 0, targetLength);
            return writer.ToArray();
        }

        private string DispatchRandomMorphRequest(RandomMorphDialogRequest request, byte[] payload)
        {
            payload ??= Array.Empty<byte>();
            string payloadHex = payload.Length > 0 ? Convert.ToHexString(payload) : "<empty>";
            string summary =
                $"Mirrored CUIRandomMorphDlg::_SendMorphRequest as opcode {RandomMorphRequestOpcode} [{payloadHex}] " +
                $"for USE slot {Math.Max(0, request.InventoryPosition)} item {request.ItemId} target '{request.TargetName}'.";

            if (_localUtilityOfficialSessionBridge.TrySendOutboundPacket(RandomMorphRequestOpcode, payload, out string bridgeStatus))
            {
                return $"{summary} Dispatched it through the live official-session bridge. {bridgeStatus}";
            }

            if (_localUtilityPacketOutbox.TrySendOutboundPacket(RandomMorphRequestOpcode, payload, out string outboxStatus))
            {
                return $"{summary} Dispatched it through the generic packet outbox after the live bridge path was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus}";
            }

            if (_localUtilityOfficialSessionBridge.IsRunning
                && _localUtilityOfficialSessionBridge.TryQueueOutboundPacket(RandomMorphRequestOpcode, payload, out string queuedBridgeStatus))
            {
                return $"{summary} Queued it for deferred official-session injection after the live bridge path was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus} Deferred bridge: {queuedBridgeStatus}";
            }

            if (_localUtilityPacketOutbox.TryQueueOutboundPacket(RandomMorphRequestOpcode, payload, out string queuedOutboxStatus))
            {
                return $"{summary} Queued it for deferred generic packet outbox delivery after the live bridge path was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus} Deferred outbox: {queuedOutboxStatus}";
            }

            return $"{summary} It remained simulator-owned because neither the live bridge nor the packet outbox accepted opcode {RandomMorphRequestOpcode}. Bridge: {bridgeStatus} Outbox: {outboxStatus}";
        }
    }
}
