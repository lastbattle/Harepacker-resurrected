using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int PacketOwnedLogoutGiftEntryCount = 3;
        private const int PacketOwnedLogoutGiftSelectionOpcode = 313;
        private const string PacketOwnedLogoutGiftUiPath = "LogoutGift/backgrnd";
        private const int PacketOwnedLogoutGiftCompletionStringPoolId = 0x16AB;
        private const string PacketOwnedLogoutGiftCompletionFallbackText = "Congratulations! Please come back in 3 days. Thank you!";
        private const int PacketOwnedLogoutGiftPredictQuitContextDwordIndex = 4137;
        private const int PacketOwnedLogoutGiftCommodityContextDwordIndex = 4138;

        private readonly int[] _packetOwnedLogoutGiftCommoditySerialNumbers = new int[PacketOwnedLogoutGiftEntryCount];
        private bool _packetOwnedLogoutGiftHasConfig;
        private int _packetOwnedLogoutGiftSelectedIndex;
        private byte[] _packetOwnedLogoutGiftLeadingOpaqueBytes = Array.Empty<byte>();
        private int[] _packetOwnedLogoutGiftLeadingOpaqueInt32Values = Array.Empty<int>();
        private PacketOwnedLogoutGiftContextField[] _packetOwnedLogoutGiftLeadingContextFields = Array.Empty<PacketOwnedLogoutGiftContextField>();
        private bool _packetOwnedLogoutGiftHasPredictQuitFlag;
        private int _packetOwnedLogoutGiftPredictQuitRawValue;
        private int _lastPacketOwnedLogoutGiftRefreshTick = int.MinValue;
        private int _lastPacketOwnedLogoutGiftSelectionTick = int.MinValue;
        private int _lastPacketOwnedLogoutGiftSelectionRequestIndex = -1;
        private string _lastPacketOwnedLogoutGiftSummary = "Packet-owned logout gift idle.";
        private Texture2D _packetOwnedLogoutGiftFrameTexture;

        private void RegisterPacketOwnedLogoutGiftWindow()
        {
            if (uiWindowManager == null || GraphicsDevice == null)
            {
                return;
            }

            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.LogoutGift) is not LogoutGiftWindow window)
            {
                window = new LogoutGiftWindow(GraphicsDevice)
                {
                    Position = ResolvePacketOwnedLogoutGiftWindowPosition(null)
                };
                uiWindowManager.RegisterCustomWindow(window);
            }

            window.ConfigureVisualAssets(LoadPacketOwnedLogoutGiftFrameTexture());
            window.Position = ResolvePacketOwnedLogoutGiftWindowPosition(window);
            window.SetSnapshotProvider(BuildPacketOwnedLogoutGiftSnapshot);
            window.SetItemIconProvider(LoadInventoryItemIcon);
            window.SetActionHandlers(HandlePacketOwnedLogoutGiftSelection, ClosePacketOwnedLogoutGiftWindow, ShowUtilityFeedbackMessage);
            if (_fontChat != null)
            {
                window.SetFont(_fontChat);
            }
        }

        private Point ResolvePacketOwnedLogoutGiftWindowPosition(LogoutGiftWindow window)
        {
            Point frameSize = window?.ActiveFrameSize ?? new Point(250, 236);
            return new Point(
                Math.Max(24, (_renderParams.RenderWidth / 2) - (frameSize.X / 2)),
                Math.Max(24, (_renderParams.RenderHeight / 2) - (frameSize.Y / 2)));
        }

        private LogoutGiftOwnerSnapshot BuildPacketOwnedLogoutGiftSnapshot()
        {
            List<LogoutGiftEntrySnapshot> entries = new(PacketOwnedLogoutGiftEntryCount);
            for (int i = 0; i < PacketOwnedLogoutGiftEntryCount; i++)
            {
                int commoditySerialNumber = Math.Max(0, _packetOwnedLogoutGiftCommoditySerialNumbers[i]);
                int itemId = 0;
                long price = 0;
                int count = 0;
                bool onSale = false;
                string itemName = string.Empty;
                if (commoditySerialNumber > 0
                    && AdminShopDialogUI.TryResolveCommodityBySerialNumber(commoditySerialNumber, out itemId, out price, out count, out onSale))
                {
                    InventoryItemMetadataResolver.TryResolveItemName(itemId, out itemName);
                }

                entries.Add(new LogoutGiftEntrySnapshot
                {
                    CommoditySerialNumber = commoditySerialNumber,
                    ItemId = itemId,
                    ItemName = itemName,
                    Price = price,
                    Count = count,
                    OnSale = onSale
                });
            }

            string subtitle = _packetOwnedLogoutGiftHasConfig
                ? BuildPacketOwnedLogoutGiftSubtitle(entries.Count)
                : "Waiting for logout-gift commodity data from SetField.";
            string selectionSuffix = _lastPacketOwnedLogoutGiftSelectionTick == int.MinValue
                ? string.Empty
                : $" Last simulated outpacket {PacketOwnedLogoutGiftSelectionOpcode} slot {_lastPacketOwnedLogoutGiftSelectionRequestIndex + 1} at tick {_lastPacketOwnedLogoutGiftSelectionTick.ToString(CultureInfo.InvariantCulture)}.";
            string detail = $"{_lastPacketOwnedLogoutGiftSummary}{selectionSuffix}";

            return new LogoutGiftOwnerSnapshot
            {
                Title = "Logout Gift",
                Subtitle = subtitle,
                Detail = detail,
                SelectedIndex = Math.Clamp(_packetOwnedLogoutGiftSelectedIndex, 0, Math.Max(0, entries.Count - 1)),
                Entries = entries
            };
        }

        private string HandlePacketOwnedLogoutGiftSelection(int index)
        {
            if (index < 0 || index >= PacketOwnedLogoutGiftEntryCount)
            {
                return "Logout-gift selection is outside the client owner slot range.";
            }

            if (!ShouldShowPacketOwnedLogoutGiftOwner())
            {
                return "Logout-gift selection is unavailable because CWvsContext::m_bPredictQuit is false, so the client would not surface the owner.";
            }

            _packetOwnedLogoutGiftSelectedIndex = index;
            int commoditySerialNumber = Math.Max(0, _packetOwnedLogoutGiftCommoditySerialNumbers[index]);
            _lastPacketOwnedLogoutGiftSelectionRequestIndex = index;
            _lastPacketOwnedLogoutGiftSelectionTick = Environment.TickCount;
            string commoditySuffix = commoditySerialNumber > 0
                ? $" Commodity SN {commoditySerialNumber.ToString(CultureInfo.InvariantCulture)} remains cached for preview."
                : " The cached slot is empty, but the client still emits the slot index.";
            string followUpMessage = BuildPacketOwnedLogoutGiftCompletionMessage();
            uiWindowManager?.HideWindow(MapSimulatorWindowNames.LogoutGift);
            _lastPacketOwnedLogoutGiftSummary =
                $"Simulated CUILogoutGift::OnButtonClicked outpacket {PacketOwnedLogoutGiftSelectionOpcode} with slot index {index.ToString(CultureInfo.InvariantCulture)} (button {1000 + index}).{commoditySuffix} {DispatchPacketOwnedLogoutGiftSelectionRequest(index)} Client follow-up util dialog (StringPool 0x{PacketOwnedLogoutGiftCompletionStringPoolId.ToString("X", CultureInfo.InvariantCulture)}): {followUpMessage}";
            return _lastPacketOwnedLogoutGiftSummary;
        }

        private string ClosePacketOwnedLogoutGiftWindow()
        {
            uiWindowManager?.HideWindow(MapSimulatorWindowNames.LogoutGift);
            string completionMessage = BuildPacketOwnedLogoutGiftCompletionMessage();
            _lastPacketOwnedLogoutGiftSummary =
                $"Closed the packet-owned logout-gift owner. Client TryShowLogoutGiftDialog follow-up util dialog (StringPool 0x{PacketOwnedLogoutGiftCompletionStringPoolId.ToString("X", CultureInfo.InvariantCulture)}): {completionMessage}";
            return _lastPacketOwnedLogoutGiftSummary;
        }

        private bool TryApplyPacketOwnedLogoutGiftPayload(byte[] payload, out string message)
        {
            RegisterPacketOwnedLogoutGiftWindow();
            _lastPacketOwnedLogoutGiftRefreshTick = Environment.TickCount;

            if (!_packetOwnedLogoutGiftHasConfig)
            {
                message = "CWvsContext::OnLogoutGift routed, but no logout-gift commodity cache is available from the current SetField state.";
                _lastPacketOwnedLogoutGiftSummary = message;
                return false;
            }

            if (!ShouldShowPacketOwnedLogoutGiftOwner())
            {
                uiWindowManager?.HideWindow(MapSimulatorWindowNames.LogoutGift);
                string ignoredPayloadSuffix = payload != null && payload.Length > 0
                    ? $" Ignored {payload.Length.ToString(CultureInfo.InvariantCulture)} unexpected payload byte(s) because the client bridge only refreshes the existing owner."
                    : string.Empty;
                string preservedTrailingSuffix = _packetOwnedLogoutGiftLeadingOpaqueBytes.Length > 0
                    ? $" Preserved {DescribePacketOwnedLogoutGiftLeadingTail()} ahead of the client 12-byte logout-gift cache."
                    : string.Empty;
                message =
                    $"CWvsContext::OnLogoutGift arrived, but `CUILogoutGift::TryShowLogoutGiftDialog` would keep the owner closed because CWvsContext::m_bPredictQuit is false while the cached commodity SNs remain {FormatPacketOwnedLogoutGiftCommodityList()}.{preservedTrailingSuffix}{ignoredPayloadSuffix}";
                _lastPacketOwnedLogoutGiftSummary = message;
                return true;
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.LogoutGift) is not LogoutGiftWindow window)
            {
                message = "CWvsContext::OnLogoutGift routed, but the simulator logout-gift owner is unavailable.";
                _lastPacketOwnedLogoutGiftSummary = message;
                return false;
            }

            window.Position = ResolvePacketOwnedLogoutGiftWindowPosition(window);
            ShowWindow(
                MapSimulatorWindowNames.LogoutGift,
                window,
                trackDirectionModeOwner: ShouldTrackInheritedDirectionModeOwner());

            string payloadSuffix = payload != null && payload.Length > 0
                ? $" Ignored {payload.Length.ToString(CultureInfo.InvariantCulture)} unexpected payload byte(s) because the client bridge only refreshes the existing owner."
                : string.Empty;
            string trailingSuffix = _packetOwnedLogoutGiftLeadingOpaqueBytes.Length > 0
                ? $" Preserved {DescribePacketOwnedLogoutGiftLeadingTail()} ahead of the client 12-byte logout-gift cache."
                : string.Empty;
            message =
                $"CWvsContext::OnLogoutGift refreshed the dedicated logout-gift owner using cached commodity SNs {FormatPacketOwnedLogoutGiftCommodityList()} and the same CWvsContext::m_bPredictQuit gate that `CUILogoutGift::TryShowLogoutGiftDialog` checks before modal show.{trailingSuffix}{payloadSuffix}";
            _lastPacketOwnedLogoutGiftSummary = message;
            return true;
        }

        private void UpdatePacketOwnedLogoutGiftConfigFromSetField(PacketSetFieldPacket packet)
        {
            if (!packet.HasCharacterData)
            {
                ResetPacketOwnedLogoutGiftRuntimeState(clearConfig: true, hideWindow: true, summary: "Cleared logout-gift cache because the latest SetField branch did not include character data.");
                return;
            }

            byte[] trailingPayload = packet.TrailingPayload ?? Array.Empty<byte>();
            if (trailingPayload.Length == 0)
            {
                ResetPacketOwnedLogoutGiftRuntimeState(clearConfig: true, hideWindow: true, summary: "Cleared logout-gift cache because the current character-data SetField branch did not carry logout-gift bytes.");
                return;
            }

            if (!PacketStageTransitionRuntime.TryDecodeTrailingLogoutGiftConfigPayload(
                trailingPayload,
                out int[] commoditySerialNumbers,
                out byte[] leadingOpaqueBytes,
                out int[] leadingOpaqueInt32Values,
                out string decodeError))
            {
                ResetPacketOwnedLogoutGiftRuntimeState(
                    clearConfig: true,
                    hideWindow: true,
                    summary: decodeError);
                return;
            }

            bool hasCommodity = false;
            for (int i = 0; i < PacketOwnedLogoutGiftEntryCount; i++)
            {
                _packetOwnedLogoutGiftCommoditySerialNumbers[i] = commoditySerialNumbers[i];
                hasCommodity |= _packetOwnedLogoutGiftCommoditySerialNumbers[i] > 0;
            }

            _packetOwnedLogoutGiftLeadingOpaqueBytes = leadingOpaqueBytes ?? Array.Empty<byte>();
            _packetOwnedLogoutGiftLeadingOpaqueInt32Values = leadingOpaqueInt32Values ?? Array.Empty<int>();
            _packetOwnedLogoutGiftLeadingContextFields = DecodePacketOwnedLogoutGiftLeadingContextFields(_packetOwnedLogoutGiftLeadingOpaqueInt32Values);
            _packetOwnedLogoutGiftHasPredictQuitFlag = TryResolvePacketOwnedLogoutGiftPredictQuit(
                _packetOwnedLogoutGiftLeadingContextFields,
                out _packetOwnedLogoutGiftPredictQuitRawValue);
            _packetOwnedLogoutGiftHasConfig = hasCommodity;
            _packetOwnedLogoutGiftSelectedIndex = ResolveFirstPacketOwnedLogoutGiftSelection();
            _lastPacketOwnedLogoutGiftSummary = hasCommodity
                ? _packetOwnedLogoutGiftLeadingOpaqueBytes.Length > 0
                    ? $"Split the character-data SetField tail into {DescribePacketOwnedLogoutGiftLeadingTail()} plus the client `CWvsContext` logout-gift cache at dword[{PacketOwnedLogoutGiftCommodityContextDwordIndex.ToString(CultureInfo.InvariantCulture)}..{(PacketOwnedLogoutGiftCommodityContextDwordIndex + PacketOwnedLogoutGiftEntryCount - 1).ToString(CultureInfo.InvariantCulture)}]: {FormatPacketOwnedLogoutGiftCommodityList()}."
                    : $"Cached logout-gift commodity SNs from character-data SetField: {FormatPacketOwnedLogoutGiftCommodityList()}. Packet 432 now refreshes the dedicated owner instead of leaving the values hidden in stage payload tail bytes."
                : _packetOwnedLogoutGiftLeadingOpaqueBytes.Length > 0
                    ? $"Decoded the trailing client 12-byte logout-gift cache after preserving {DescribePacketOwnedLogoutGiftLeadingTail()}, but all three commodity slots were zero."
                    : "Decoded the explicit logout-gift cache payload from SetField, but all three commodity slots were zero.";

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.LogoutGift) is LogoutGiftWindow window && window.IsVisible)
            {
                window.Position = ResolvePacketOwnedLogoutGiftWindowPosition(window);
            }
        }

        private int ResolveFirstPacketOwnedLogoutGiftSelection()
        {
            for (int i = 0; i < _packetOwnedLogoutGiftCommoditySerialNumbers.Length; i++)
            {
                if (_packetOwnedLogoutGiftCommoditySerialNumbers[i] > 0)
                {
                    return i;
                }
            }

            return 0;
        }

        private string FormatPacketOwnedLogoutGiftCommodityList()
        {
            List<string> parts = new(PacketOwnedLogoutGiftEntryCount);
            for (int i = 0; i < PacketOwnedLogoutGiftEntryCount; i++)
            {
                parts.Add(_packetOwnedLogoutGiftCommoditySerialNumbers[i].ToString(CultureInfo.InvariantCulture));
            }

            return string.Join(", ", parts);
        }

        private void ResetPacketOwnedLogoutGiftRuntimeState(bool clearConfig, bool hideWindow, string summary)
        {
            if (clearConfig)
            {
                Array.Clear(_packetOwnedLogoutGiftCommoditySerialNumbers, 0, _packetOwnedLogoutGiftCommoditySerialNumbers.Length);
                _packetOwnedLogoutGiftHasConfig = false;
                _packetOwnedLogoutGiftSelectedIndex = 0;
                _packetOwnedLogoutGiftLeadingOpaqueBytes = Array.Empty<byte>();
                _packetOwnedLogoutGiftLeadingOpaqueInt32Values = Array.Empty<int>();
                _packetOwnedLogoutGiftLeadingContextFields = Array.Empty<PacketOwnedLogoutGiftContextField>();
                _packetOwnedLogoutGiftHasPredictQuitFlag = false;
                _packetOwnedLogoutGiftPredictQuitRawValue = 0;
                _lastPacketOwnedLogoutGiftSelectionRequestIndex = -1;
                _lastPacketOwnedLogoutGiftSelectionTick = int.MinValue;
            }

            if (hideWindow)
            {
                uiWindowManager?.HideWindow(MapSimulatorWindowNames.LogoutGift);
            }

            if (!string.IsNullOrWhiteSpace(summary))
            {
                _lastPacketOwnedLogoutGiftSummary = summary;
            }
        }

        private Texture2D LoadPacketOwnedLogoutGiftFrameTexture()
        {
            if (_packetOwnedLogoutGiftFrameTexture != null || GraphicsDevice == null)
            {
                return _packetOwnedLogoutGiftFrameTexture;
            }

            WzImage uiWindowImage = global::HaCreator.Program.FindImage("UI", "UIWindow.img");
            WzCanvasProperty backgroundCanvas = uiWindowImage?[PacketOwnedLogoutGiftUiPath] as WzCanvasProperty;
            _packetOwnedLogoutGiftFrameTexture = LoadUiCanvasTexture(backgroundCanvas);
            return _packetOwnedLogoutGiftFrameTexture;
        }

        private string DescribePacketOwnedLogoutGiftLeadingTail()
        {
            if (_packetOwnedLogoutGiftLeadingOpaqueBytes == null || _packetOwnedLogoutGiftLeadingOpaqueBytes.Length == 0)
            {
                return "no adjacent trailing bytes";
            }

            string hex = Convert.ToHexString(_packetOwnedLogoutGiftLeadingOpaqueBytes);
            if (_packetOwnedLogoutGiftLeadingOpaqueInt32Values == null || _packetOwnedLogoutGiftLeadingOpaqueInt32Values.Length == 0)
            {
                return $"{_packetOwnedLogoutGiftLeadingOpaqueBytes.Length.ToString(CultureInfo.InvariantCulture)} adjacent trailing byte(s) [0x{hex}]";
            }

            List<string> values = new(_packetOwnedLogoutGiftLeadingOpaqueInt32Values.Length);
            foreach (int value in _packetOwnedLogoutGiftLeadingOpaqueInt32Values)
            {
                values.Add(value.ToString(CultureInfo.InvariantCulture));
            }

            string contextFieldDescription = DescribePacketOwnedLogoutGiftLeadingContextFields(_packetOwnedLogoutGiftLeadingContextFields);
            return string.IsNullOrWhiteSpace(contextFieldDescription)
                ? $"{_packetOwnedLogoutGiftLeadingOpaqueBytes.Length.ToString(CultureInfo.InvariantCulture)} adjacent trailing byte(s) [0x{hex}] / int32 [{string.Join(", ", values)}]"
                : $"{_packetOwnedLogoutGiftLeadingOpaqueBytes.Length.ToString(CultureInfo.InvariantCulture)} adjacent trailing byte(s) [0x{hex}] / int32 [{string.Join(", ", values)}] => {contextFieldDescription}";
        }

        private string BuildPacketOwnedLogoutGiftSubtitle(int entryCount)
        {
            string predictQuitPrefix = _packetOwnedLogoutGiftHasPredictQuitFlag
                ? $"CWvsContext m_bPredictQuit={(_packetOwnedLogoutGiftPredictQuitRawValue != 0 ? "true" : "false")} (TryShowLogoutGiftDialog gate) "
                : string.Empty;
            return _packetOwnedLogoutGiftLeadingOpaqueBytes.Length > 0
                ? $"{predictQuitPrefix}cached {entryCount} logout-gift slots plus {DescribePacketOwnedLogoutGiftLeadingTail()}."
                : $"{predictQuitPrefix}cached {entryCount} logout-gift commodity slot(s).";
        }

        private bool ShouldShowPacketOwnedLogoutGiftOwner()
        {
            return !_packetOwnedLogoutGiftHasPredictQuitFlag || _packetOwnedLogoutGiftPredictQuitRawValue != 0;
        }

        private static string BuildPacketOwnedLogoutGiftCompletionMessage()
        {
            return MapleStoryStringPool.GetOrFallback(
                PacketOwnedLogoutGiftCompletionStringPoolId,
                PacketOwnedLogoutGiftCompletionFallbackText,
                appendFallbackSuffix: true,
                minimumHexWidth: 4);
        }

        private string DispatchPacketOwnedLogoutGiftSelectionRequest(int index)
        {
            byte[] payload = BuildPacketOwnedLogoutGiftSelectionPayload(index);
            string payloadHex = Convert.ToHexString(payload);
            if (_localUtilityOfficialSessionBridge.TrySendOutboundPacket(PacketOwnedLogoutGiftSelectionOpcode, payload, out string dispatchStatus))
            {
                return $"Dispatched [{payloadHex}] through the live local-utility bridge. {dispatchStatus}";
            }

            if (_localUtilityPacketOutbox.TrySendOutboundPacket(PacketOwnedLogoutGiftSelectionOpcode, payload, out string outboxStatus))
            {
                return $"Dispatched [{payloadHex}] through the generic local-utility outbox after the live bridge path was unavailable. Bridge: {dispatchStatus} Outbox: {outboxStatus}";
            }

            string bridgeDeferredStatus = "Official-session bridge deferred delivery is disabled.";
            if (_localUtilityOfficialSessionBridgeEnabled
                && _localUtilityOfficialSessionBridge.TryQueueOutboundPacket(PacketOwnedLogoutGiftSelectionOpcode, payload, out bridgeDeferredStatus))
            {
                return $"Queued [{payloadHex}] for deferred live official-session injection after the immediate bridge path was unavailable. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred official bridge: {bridgeDeferredStatus}";
            }

            if (_localUtilityPacketOutbox.TryQueueOutboundPacket(PacketOwnedLogoutGiftSelectionOpcode, payload, out string queuedStatus))
            {
                return $"Queued [{payloadHex}] for deferred generic local-utility outbox delivery after the immediate bridge path was unavailable. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred official bridge: {bridgeDeferredStatus} Deferred outbox: {queuedStatus}";
            }

            return $"Kept [{payloadHex}] simulator-local because neither the live local-utility bridge nor the generic outbox nor either deferred queue accepted opcode {PacketOwnedLogoutGiftSelectionOpcode}. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred official bridge: {bridgeDeferredStatus} Deferred outbox: {queuedStatus}";
        }

        internal static byte[] BuildPacketOwnedLogoutGiftSelectionPayload(int index)
        {
            return BitConverter.GetBytes(index);
        }

        internal static PacketOwnedLogoutGiftContextField[] DecodePacketOwnedLogoutGiftLeadingContextFields(int[] leadingOpaqueInt32Values)
        {
            if (leadingOpaqueInt32Values == null || leadingOpaqueInt32Values.Length == 0)
            {
                return Array.Empty<PacketOwnedLogoutGiftContextField>();
            }

            int startDwordIndex = PacketOwnedLogoutGiftCommodityContextDwordIndex - leadingOpaqueInt32Values.Length;
            if (startDwordIndex < 0)
            {
                return Array.Empty<PacketOwnedLogoutGiftContextField>();
            }

            PacketOwnedLogoutGiftContextField[] fields = new PacketOwnedLogoutGiftContextField[leadingOpaqueInt32Values.Length];
            for (int i = 0; i < leadingOpaqueInt32Values.Length; i++)
            {
                int dwordIndex = startDwordIndex + i;
                fields[i] = new PacketOwnedLogoutGiftContextField(
                    dwordIndex,
                    dwordIndex * sizeof(int),
                    leadingOpaqueInt32Values[i],
                    dwordIndex == PacketOwnedLogoutGiftPredictQuitContextDwordIndex
                        ? "CWvsContext::m_bPredictQuit"
                        : null);
            }

            return fields;
        }

        internal static string DescribePacketOwnedLogoutGiftLeadingContextFields(IReadOnlyList<PacketOwnedLogoutGiftContextField> fields)
        {
            if (fields == null || fields.Count == 0)
            {
                return string.Empty;
            }

            List<string> parts = new(fields.Count);
            foreach (PacketOwnedLogoutGiftContextField field in fields)
            {
                parts.Add(
                    string.Equals(field.SemanticName, "CWvsContext::m_bPredictQuit", StringComparison.Ordinal)
                        ? $"{field.SemanticName}@0x{field.ByteOffset.ToString("X", CultureInfo.InvariantCulture)}={field.Value.ToString(CultureInfo.InvariantCulture)} ({(field.Value != 0 ? "true" : "false")}, TryShowLogoutGiftDialog gate)"
                        : $"CWvsContext dword[{field.DwordIndex.ToString(CultureInfo.InvariantCulture)}]@0x{field.ByteOffset.ToString("X", CultureInfo.InvariantCulture)}={field.Value.ToString(CultureInfo.InvariantCulture)}");
            }

            return string.Join(", ", parts);
        }

        private static bool TryResolvePacketOwnedLogoutGiftPredictQuit(
            IReadOnlyList<PacketOwnedLogoutGiftContextField> fields,
            out int rawValue)
        {
            rawValue = 0;
            if (fields == null)
            {
                return false;
            }

            foreach (PacketOwnedLogoutGiftContextField field in fields)
            {
                if (field.DwordIndex != PacketOwnedLogoutGiftPredictQuitContextDwordIndex)
                {
                    continue;
                }

                rawValue = field.Value;
                return true;
            }

            return false;
        }
    }

    internal readonly record struct PacketOwnedLogoutGiftContextField(int DwordIndex, int ByteOffset, int Value, string SemanticName = null);
}
