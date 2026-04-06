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
        private const string PacketOwnedLogoutGiftUiPath = "LogoutGift/backgrnd";

        private readonly int[] _packetOwnedLogoutGiftCommoditySerialNumbers = new int[PacketOwnedLogoutGiftEntryCount];
        private bool _packetOwnedLogoutGiftHasConfig;
        private int _packetOwnedLogoutGiftSelectedIndex;
        private int _packetOwnedLogoutGiftLeadingOpaqueByteCount;
        private int _lastPacketOwnedLogoutGiftRefreshTick = int.MinValue;
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
                ? _packetOwnedLogoutGiftLeadingOpaqueByteCount > 0
                    ? $"CWvsContext cached {entries.Count} logout-gift slots plus {_packetOwnedLogoutGiftLeadingOpaqueByteCount.ToString(CultureInfo.InvariantCulture)} adjacent trailing byte(s)."
                    : $"CWvsContext cached {entries.Count} logout-gift commodity slot(s)."
                : "Waiting for logout-gift commodity data from SetField.";
            string detail = _lastPacketOwnedLogoutGiftSummary;

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

            _packetOwnedLogoutGiftSelectedIndex = index;
            int commoditySerialNumber = Math.Max(0, _packetOwnedLogoutGiftCommoditySerialNumbers[index]);
            if (commoditySerialNumber <= 0)
            {
                _lastPacketOwnedLogoutGiftSummary = $"Logout-gift slot {index + 1} is empty.";
                return _lastPacketOwnedLogoutGiftSummary;
            }

            string navigation = ApplyPacketOwnedGoToCommoditySn(commoditySerialNumber);
            _lastPacketOwnedLogoutGiftSummary = $"Selected logout-gift slot {index + 1} (SN {commoditySerialNumber.ToString(CultureInfo.InvariantCulture)}). {navigation}";
            return _lastPacketOwnedLogoutGiftSummary;
        }

        private string ClosePacketOwnedLogoutGiftWindow()
        {
            uiWindowManager?.HideWindow(MapSimulatorWindowNames.LogoutGift);
            return "Closed the packet-owned logout-gift owner.";
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
            string trailingSuffix = _packetOwnedLogoutGiftLeadingOpaqueByteCount > 0
                ? $" Preserved {_packetOwnedLogoutGiftLeadingOpaqueByteCount.ToString(CultureInfo.InvariantCulture)} adjacent SetField tail byte(s) ahead of the client 12-byte logout-gift cache."
                : string.Empty;
            message = $"CWvsContext::OnLogoutGift refreshed the dedicated logout-gift owner using cached commodity SNs {FormatPacketOwnedLogoutGiftCommodityList()}.{trailingSuffix}{payloadSuffix}";
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
                out int leadingOpaqueByteCount,
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

            _packetOwnedLogoutGiftLeadingOpaqueByteCount = leadingOpaqueByteCount;
            _packetOwnedLogoutGiftHasConfig = hasCommodity;
            _packetOwnedLogoutGiftSelectedIndex = ResolveFirstPacketOwnedLogoutGiftSelection();
            _lastPacketOwnedLogoutGiftSummary = hasCommodity
                ? leadingOpaqueByteCount > 0
                    ? $"Split the character-data SetField tail into {leadingOpaqueByteCount.ToString(CultureInfo.InvariantCulture)} preserved opaque byte(s) plus the client 12-byte logout-gift cache: {FormatPacketOwnedLogoutGiftCommodityList()}."
                    : $"Cached logout-gift commodity SNs from character-data SetField: {FormatPacketOwnedLogoutGiftCommodityList()}. Packet 432 now refreshes the dedicated owner instead of leaving the values hidden in stage payload tail bytes."
                : leadingOpaqueByteCount > 0
                    ? $"Decoded the trailing client 12-byte logout-gift cache after preserving {leadingOpaqueByteCount.ToString(CultureInfo.InvariantCulture)} adjacent opaque SetField byte(s), but all three commodity slots were zero."
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
                _packetOwnedLogoutGiftLeadingOpaqueByteCount = 0;
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
    }
}
