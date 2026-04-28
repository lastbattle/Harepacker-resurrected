using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Loaders;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int PacketOwnedLogoutGiftEntryCount = 3;
        private const int PacketOwnedLogoutGiftSelectionOpcode = 313;
        private static readonly string[] PacketOwnedLogoutGiftUiImageNames =
        {
            "UIWindow.img",
            "UIWindow2.img"
        };

        private const int PacketOwnedLogoutGiftDialogFrameStringPoolId = 0x16AA;
        private const string PacketOwnedLogoutGiftDialogFrameFallbackPath = "UI/UIWindow.img/LogoutGift/backgrnd";
        private const string PacketOwnedLogoutGiftUiPath = "LogoutGift/backgrnd";
        private const int PacketOwnedLogoutGiftButtonPathStringPoolId = 0x146;
        private const string PacketOwnedLogoutGiftButtonFallbackPath = "UI/UIWindow.img/LogoutGift/BtSelect";
        private const string PacketOwnedLogoutGiftButtonClientUolFallbackPath = "UI/Login.img/CharSelect/BtSelect";
        private const string PacketOwnedLogoutGiftButtonUiPath = "LogoutGift/BtSelect";
        private const int PacketOwnedLogoutGiftCompletionStringPoolId = 0x16AB;
        private const string PacketOwnedLogoutGiftCompletionFallbackText = "Congratulations! Please come back in 3 days. Thank you!";
        private const int PacketOwnedLogoutGiftSpecialItemFamily = 91;
        private const int PacketOwnedLogoutGiftPredictQuitContextDwordIndex = 4137;
        private const int PacketOwnedLogoutGiftCommodityContextDwordIndex = 4138;
        private const int PacketOwnedLogoutGiftOwnerCommodityFieldByteOffset = 0xAF8;
        private const int PacketOwnedLogoutGiftPrecursorContextDwordIndex = PacketOwnedLogoutGiftPredictQuitContextDwordIndex - 1;
        private const int PacketOwnedLogoutGiftPrecursorContextSlotCount = 3;
        private const int PacketOwnedLogoutGiftPrecursorFirstContextDwordIndex = PacketOwnedLogoutGiftPredictQuitContextDwordIndex - PacketOwnedLogoutGiftPrecursorContextSlotCount;
        private const int PacketOwnedLogoutGiftPrecursorLastContextDwordIndex = PacketOwnedLogoutGiftPredictQuitContextDwordIndex - 1;
        private const int PacketOwnedLogoutGiftPredictQuitContextByteOffset = 0x40A4;
        private const int PacketOwnedLogoutGiftCommodityContextByteOffset = 0x40A8;
        private const int PacketOwnedLogoutGiftPrecursorFirstContextByteOffset =
            PacketOwnedLogoutGiftPredictQuitContextByteOffset - (PacketOwnedLogoutGiftPrecursorContextSlotCount * sizeof(int));
        private const int PacketOwnedLogoutGiftPrecursorLastContextByteOffset =
            PacketOwnedLogoutGiftPredictQuitContextByteOffset - sizeof(int);
        private const string PacketOwnedLogoutGiftPrecursorFirstContextSymbol = "CWvsContext::dword_4098";
        private const string PacketOwnedLogoutGiftPrecursorSecondContextSymbol = "CWvsContext::dword_409C";
        private const string PacketOwnedLogoutGiftPrecursorThirdContextSymbol = "CWvsContext::dword_40A0";

        private readonly int[] _packetOwnedLogoutGiftCommoditySerialNumbers = new int[PacketOwnedLogoutGiftEntryCount];
        private readonly int[] _packetOwnedLogoutGiftOwnerCommoditySerialNumbers = new int[PacketOwnedLogoutGiftEntryCount];
        private bool _packetOwnedLogoutGiftHasConfig;
        private int _packetOwnedLogoutGiftSelectedIndex;
        private byte[] _packetOwnedLogoutGiftLeadingOpaqueBytes = Array.Empty<byte>();
        private int[] _packetOwnedLogoutGiftLeadingOpaqueInt32Values = Array.Empty<int>();
        private PacketOwnedLogoutGiftContextField[] _packetOwnedLogoutGiftLeadingContextFields = Array.Empty<PacketOwnedLogoutGiftContextField>();
        private byte[] _packetOwnedLogoutGiftTrailingOpaqueBytes = Array.Empty<byte>();
        private int[] _packetOwnedLogoutGiftTrailingOpaqueInt32Values = Array.Empty<int>();
        private PacketOwnedLogoutGiftContextField? _packetOwnedLogoutGiftPredictQuitContextField;
        private PacketOwnedLogoutGiftContextField[] _packetOwnedLogoutGiftCommodityContextFields = Array.Empty<PacketOwnedLogoutGiftContextField>();
        private bool _packetOwnedLogoutGiftHasPredictQuitFlag;
        private int _packetOwnedLogoutGiftPredictQuitRawValue;
        private int _lastPacketOwnedLogoutGiftRefreshTick = int.MinValue;
        private int _lastPacketOwnedLogoutGiftSelectionTick = int.MinValue;
        private int _lastPacketOwnedLogoutGiftSelectionRequestIndex = -1;
        private string _lastPacketOwnedLogoutGiftSummary = "Packet-owned logout gift idle.";
        private string _lastPacketOwnedLogoutGiftLaunchSource = string.Empty;
        private PacketOwnedLogoutGiftContinuation _packetOwnedLogoutGiftPendingContinuation;
        private bool _packetOwnedLogoutGiftOwnerInstantiated;
        private Texture2D _packetOwnedLogoutGiftFrameTexture;
        private LogoutGiftButtonSkin _packetOwnedLogoutGiftButtonSkin;

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

            window.ConfigureVisualAssets(
                LoadPacketOwnedLogoutGiftFrameTexture(),
                LoadPacketOwnedLogoutGiftButtonSkin());
            window.Position = ResolvePacketOwnedLogoutGiftWindowPosition(window);
            window.SetSnapshotProvider(BuildPacketOwnedLogoutGiftSnapshot);
            window.SetItemIconProvider(LoadPacketOwnedLogoutGiftItemIcon);
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
            int[] displayedCommoditySerialNumbers = ResolvePacketOwnedLogoutGiftDisplayedCommoditySerialNumbers(
                _packetOwnedLogoutGiftCommoditySerialNumbers,
                _packetOwnedLogoutGiftOwnerCommoditySerialNumbers,
                _packetOwnedLogoutGiftOwnerInstantiated);
            for (int i = 0; i < PacketOwnedLogoutGiftEntryCount; i++)
            {
                int commoditySerialNumber = Math.Max(0, displayedCommoditySerialNumbers[i]);
                int itemId = 0;
                long price = 0;
                int count = 0;
                bool onSale = false;
                string itemName = string.Empty;
                if (commoditySerialNumber > 0
                    && AdminShopDialogUI.TryResolveCommodityBySerialNumber(commoditySerialNumber, out itemId, out price, out count, out onSale))
                {
                    if (IsPacketOwnedLogoutGiftSpecialItem(itemId))
                    {
                        TryResolvePacketOwnedLogoutGiftSpecialItemName(itemId, out itemName);
                    }
                    else
                    {
                        InventoryItemMetadataResolver.TryResolveItemName(itemId, out itemName);
                    }
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
            string launchSuffix = string.IsNullOrWhiteSpace(_lastPacketOwnedLogoutGiftLaunchSource)
                ? string.Empty
                : $" Launch source: {_lastPacketOwnedLogoutGiftLaunchSource}.";
            string selectionSuffix = _lastPacketOwnedLogoutGiftSelectionTick == int.MinValue
                ? string.Empty
                : $" Last simulated outpacket {PacketOwnedLogoutGiftSelectionOpcode} slot {_lastPacketOwnedLogoutGiftSelectionRequestIndex + 1} at tick {_lastPacketOwnedLogoutGiftSelectionTick.ToString(CultureInfo.InvariantCulture)}.";
            string contextSuffix = BuildPacketOwnedLogoutGiftContextOwnershipSuffix();
            string detail = $"{_lastPacketOwnedLogoutGiftSummary}{contextSuffix}{launchSuffix}{selectionSuffix}";

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

            PacketOwnedLogoutGiftOwnerAvailability ownerAvailability = ResolveCurrentPacketOwnedLogoutGiftOwnerAvailability();
            if (ownerAvailability != PacketOwnedLogoutGiftOwnerAvailability.Available)
            {
                return BuildPacketOwnedLogoutGiftOwnerUnavailableMessage(
                    ownerAvailability,
                    "Logout-gift selection is unavailable because the client would not surface the owner.");
            }

            _packetOwnedLogoutGiftSelectedIndex = index;
            int[] displayedCommoditySerialNumbers = ResolvePacketOwnedLogoutGiftDisplayedCommoditySerialNumbers(
                _packetOwnedLogoutGiftCommoditySerialNumbers,
                _packetOwnedLogoutGiftOwnerCommoditySerialNumbers,
                _packetOwnedLogoutGiftOwnerInstantiated);
            int commoditySerialNumber = Math.Max(0, displayedCommoditySerialNumbers[index]);
            _lastPacketOwnedLogoutGiftSelectionRequestIndex = index;
            _lastPacketOwnedLogoutGiftSelectionTick = Environment.TickCount;
            string commoditySuffix = commoditySerialNumber > 0
                ? $" Commodity SN {commoditySerialNumber.ToString(CultureInfo.InvariantCulture)} remains cached for preview."
                : " The cached slot is empty, but the client still emits the slot index.";
            uiWindowManager?.HideWindow(MapSimulatorWindowNames.LogoutGift);
            string followUpMessage = ShowPacketOwnedLogoutGiftCompletionDialog();
            int buttonId = LogoutGiftWindow.GetClientSelectButtonId(index);
            _lastPacketOwnedLogoutGiftSummary =
                $"Simulated CUILogoutGift::OnButtonClicked outpacket {PacketOwnedLogoutGiftSelectionOpcode} with slot index {index.ToString(CultureInfo.InvariantCulture)} (button {buttonId.ToString(CultureInfo.InvariantCulture)}).{commoditySuffix} {DispatchPacketOwnedLogoutGiftSelectionRequest(buttonId)} {followUpMessage}";
            NotifyEventAlarmOwnerActivity("packet-owned logout gift");
            return _lastPacketOwnedLogoutGiftSummary;
        }

        private string ClosePacketOwnedLogoutGiftWindow()
        {
            uiWindowManager?.HideWindow(MapSimulatorWindowNames.LogoutGift);
            string completionMessage = ShowPacketOwnedLogoutGiftCompletionDialog();
            _lastPacketOwnedLogoutGiftSummary =
                $"Closed the packet-owned logout-gift owner. {completionMessage}";
            NotifyEventAlarmOwnerActivity("packet-owned logout gift");
            return _lastPacketOwnedLogoutGiftSummary;
        }

        private bool TryLaunchPacketOwnedLogoutGiftForUtilityQuit(string launchSource, out string message)
        {
            return TryShowPacketOwnedLogoutGiftDialog(
                PacketOwnedLogoutGiftContinuation.ExitSimulator,
                launchSource,
                out message);
        }

        private bool TryHandleConfirmedUtilityQuit(out string message)
        {
            return TryHandleConfirmedUtilityQuit(
                "Game menu quit",
                "CWvsContext::UI_Menu",
                out message);
        }

        private bool TryHandleConfirmedUtilityQuit(string launchSource, string clientCaller, out string message)
        {
            RegisterPacketOwnedLogoutGiftWindow();
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.LogoutGift) is LogoutGiftWindow existingWindow
                && IsPacketOwnedLogoutGiftOwnerSingletonPresent(_packetOwnedLogoutGiftOwnerInstantiated, existingWindow.IsVisible))
            {
                message = $"CUILogoutGift::TryShowLogoutGiftDialog returned 0 because the logout-gift singleton is already instantiated; matching `{clientCaller}`, the simulator suppresses the quit continuation until the existing owner completes.";
                _lastPacketOwnedLogoutGiftSummary = message;
                NotifyEventAlarmOwnerActivity("packet-owned logout gift");
                return true;
            }

            if (TryLaunchPacketOwnedLogoutGiftForUtilityQuit(launchSource, out message))
            {
                return true;
            }

            message = string.IsNullOrWhiteSpace(message)
                ? $"{clientCaller} quit flow continued directly because CUILogoutGift::TryShowLogoutGiftDialog returned 1 without surfacing a modal owner."
                : $"{message} {clientCaller} therefore continues the quit flow because the client TryShowLogoutGiftDialog branch returns 1 when no singleton blocks the continuation.";
            _lastPacketOwnedLogoutGiftSummary = message;
            return false;
        }

        private void ContinueConfirmedUtilityQuitThroughLogoutGift()
        {
            if (TryHandleConfirmedUtilityQuit(out string message))
            {
                ShowUtilityFeedbackMessage(message);
                return;
            }

            ShowUtilityFeedbackMessage(message);
            Exit();
        }

        private bool TryHandlePacketOwnedLogoutGiftSystemCloseShortcut(
            KeyboardState newKeyboardState,
            KeyboardState oldKeyboardState,
            bool isWindowActive)
        {
            if (!ShouldRoutePacketOwnedLogoutGiftSystemCloseShortcut(
                    isWindowActive,
                    newKeyboardState.IsKeyDown(Keys.F4),
                    oldKeyboardState.IsKeyDown(Keys.F4),
                    newKeyboardState.IsKeyDown(Keys.LeftAlt),
                    newKeyboardState.IsKeyDown(Keys.RightAlt)))
            {
                return false;
            }

            if (TryHandleConfirmedUtilityQuit(
                    "Alt+F4 / WM_SYSCOMMAND(SC_CLOSE)",
                    "CWndMan::ProcessKey / CWvsApp::WindowProc close accelerator",
                    out string message))
            {
                ShowUtilityFeedbackMessage(message);
                return true;
            }

            ShowUtilityFeedbackMessage(message);
            Exit();
            return true;
        }

        private bool TryApplyPacketOwnedLogoutGiftPayload(byte[] payload, out string message)
        {
            _lastPacketOwnedLogoutGiftRefreshTick = Environment.TickCount;

            if (!IsPacketOwnedLogoutGiftFieldStageActive())
            {
                message =
                    "Ignored packet 432 because the client routes `CWvsContext::OnLogoutGift` only through `CField::OnPacket`, and the current simulator stage is not an active `CField` owner.";
                _lastPacketOwnedLogoutGiftSummary = message;
                NotifyEventAlarmOwnerActivity("packet-owned logout gift");
                return false;
            }

            LogoutGiftWindow window = uiWindowManager?.GetWindow(MapSimulatorWindowNames.LogoutGift) as LogoutGiftWindow;
            bool ownerVisible = window?.IsVisible == true;
            bool ownerSingletonPresent = IsPacketOwnedLogoutGiftOwnerSingletonPresent(_packetOwnedLogoutGiftOwnerInstantiated, ownerVisible);

            PacketOwnedLogoutGiftRefreshDisposition refreshDisposition = ResolvePacketOwnedLogoutGiftRefreshDisposition(
                _packetOwnedLogoutGiftHasConfig,
                ownerSingletonPresent,
                ownerVisible,
                ResolveCurrentPacketOwnedLogoutGiftOwnerAvailability() == PacketOwnedLogoutGiftOwnerAvailability.Available);

            switch (refreshDisposition)
            {
                case PacketOwnedLogoutGiftRefreshDisposition.MissingConfig:
                    message = "CWvsContext::OnLogoutGift routed, but no logout-gift commodity cache is available from the current SetField state.";
                    _lastPacketOwnedLogoutGiftSummary = message;
                    NotifyEventAlarmOwnerActivity("packet-owned logout gift");
                    return false;
                case PacketOwnedLogoutGiftRefreshDisposition.NoInstantiatedOwner:
                {
                    string hiddenWindowPayloadSuffix = payload != null && payload.Length > 0
                        ? $" Ignored {payload.Length.ToString(CultureInfo.InvariantCulture)} unexpected payload byte(s) because `CWvsContext::OnLogoutGift` only calls `CUILogoutGift::Update(1)` on an existing instance."
                        : string.Empty;
                    message =
                        $"CWvsContext::OnLogoutGift refreshed the cached commodity SNs {FormatPacketOwnedLogoutGiftCommodityList()}, but no instantiated CUILogoutGift singleton exists to update. The simulator keeps the cache staged for the next TryShowLogoutGiftDialog-owned launch instead of surfacing the owner directly from packet 432.{hiddenWindowPayloadSuffix}";
                    _lastPacketOwnedLogoutGiftSummary = message;
                    NotifyEventAlarmOwnerActivity("packet-owned logout gift");
                    return true;
                }
                case PacketOwnedLogoutGiftRefreshDisposition.NoOwnerAllowed:
                {
                    uiWindowManager?.HideWindow(MapSimulatorWindowNames.LogoutGift);
                    string ignoredPayloadSuffix = payload != null && payload.Length > 0
                        ? $" Ignored {payload.Length.ToString(CultureInfo.InvariantCulture)} unexpected payload byte(s) because the client bridge only refreshes the existing owner."
                        : string.Empty;
                    string preservedTrailingSuffix = HasPacketOwnedLogoutGiftOpaqueTail()
                        ? $" Preserved {DescribePacketOwnedLogoutGiftOpaqueTail()} around the client `CWvsContext::OnSetLogoutGiftConfig` cache (`m_bPredictQuit` + `m_anLogoutGiftCommoditySN[3]`)."
                        : string.Empty;
                    string unavailableMessage = BuildPacketOwnedLogoutGiftOwnerUnavailableMessage(
                        ResolveCurrentPacketOwnedLogoutGiftOwnerAvailability(),
                        "CWvsContext::OnLogoutGift arrived, but `CUILogoutGift::TryShowLogoutGiftDialog` would keep the owner closed.");
                    message =
                        $"{unavailableMessage} The cached commodity SNs remain {FormatPacketOwnedLogoutGiftCommodityList()}.{preservedTrailingSuffix}{ignoredPayloadSuffix}";
                    _lastPacketOwnedLogoutGiftSummary = message;
                    NotifyEventAlarmOwnerActivity("packet-owned logout gift");
                    return true;
                }
                case PacketOwnedLogoutGiftRefreshDisposition.RefreshHiddenInstantiatedOwner:
                {
                    CopyPacketOwnedLogoutGiftContextCacheToOwnerLocalCache();
                    string hiddenPayloadSuffix = payload != null && payload.Length > 0
                        ? $" Ignored {payload.Length.ToString(CultureInfo.InvariantCulture)} unexpected payload byte(s) because the client bridge only refreshes the existing owner."
                        : string.Empty;
                    string hiddenTrailingSuffix = HasPacketOwnedLogoutGiftOpaqueTail()
                        ? $" Preserved {DescribePacketOwnedLogoutGiftOpaqueTail()} around the client `CWvsContext::OnSetLogoutGiftConfig` cache (`m_bPredictQuit` + `m_anLogoutGiftCommoditySN[3]`)."
                        : string.Empty;
                    message =
                        $"CWvsContext::OnLogoutGift refreshed the instantiated logout-gift singleton using cached commodity SNs {FormatPacketOwnedLogoutGiftCommodityList()} while the visible chooser remained closed behind the StringPool 0x{PacketOwnedLogoutGiftCompletionStringPoolId.ToString("X", CultureInfo.InvariantCulture)} completion dialog.{hiddenTrailingSuffix}{hiddenPayloadSuffix}";
                    _lastPacketOwnedLogoutGiftSummary = message;
                    NotifyEventAlarmOwnerActivity("packet-owned logout gift");
                    return true;
                }
            }

            CopyPacketOwnedLogoutGiftContextCacheToOwnerLocalCache();
            window.Position = ResolvePacketOwnedLogoutGiftWindowPosition(window);

            string payloadSuffix = payload != null && payload.Length > 0
                ? $" Ignored {payload.Length.ToString(CultureInfo.InvariantCulture)} unexpected payload byte(s) because the client bridge only refreshes the existing owner."
                : string.Empty;
            string trailingSuffix = HasPacketOwnedLogoutGiftOpaqueTail()
                ? $" Preserved {DescribePacketOwnedLogoutGiftOpaqueTail()} around the client `CWvsContext::OnSetLogoutGiftConfig` cache (`m_bPredictQuit` + `m_anLogoutGiftCommoditySN[3]`)."
                : string.Empty;
            message =
                $"CWvsContext::OnLogoutGift refreshed the active logout-gift owner using cached commodity SNs {FormatPacketOwnedLogoutGiftCommodityList()} after `CUILogoutGift::TryShowLogoutGiftDialog` had already surfaced it.{trailingSuffix}{payloadSuffix}";
            _lastPacketOwnedLogoutGiftSummary = message;
            NotifyEventAlarmOwnerActivity("packet-owned logout gift");
            return true;
        }

        private bool TryShowPacketOwnedLogoutGiftDialog(PacketOwnedLogoutGiftContinuation continuation, string launchSource, out string message)
        {
            RegisterPacketOwnedLogoutGiftWindow();
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.LogoutGift) is LogoutGiftWindow existingWindow
                && IsPacketOwnedLogoutGiftOwnerSingletonPresent(_packetOwnedLogoutGiftOwnerInstantiated, existingWindow.IsVisible))
            {
                message = "CUILogoutGift::TryShowLogoutGiftDialog skipped because the logout-gift singleton is already instantiated.";
                _lastPacketOwnedLogoutGiftSummary = message;
                return false;
            }

            if (!_packetOwnedLogoutGiftHasConfig)
            {
                message = "CUILogoutGift::TryShowLogoutGiftDialog skipped because the current SetField state has not cached logout-gift commodity serial numbers.";
                _lastPacketOwnedLogoutGiftSummary = message;
                return false;
            }

            PacketOwnedLogoutGiftOwnerAvailability ownerAvailability = ResolveCurrentPacketOwnedLogoutGiftOwnerAvailability();
            if (ownerAvailability != PacketOwnedLogoutGiftOwnerAvailability.Available)
            {
                message = BuildPacketOwnedLogoutGiftOwnerUnavailableMessage(
                    ownerAvailability,
                    "CUILogoutGift::TryShowLogoutGiftDialog skipped.");
                _lastPacketOwnedLogoutGiftSummary = message;
                return false;
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.LogoutGift) is not LogoutGiftWindow window)
            {
                message = "CUILogoutGift::TryShowLogoutGiftDialog could not present because the simulator logout-gift owner is unavailable.";
                _lastPacketOwnedLogoutGiftSummary = message;
                return false;
            }

            _packetOwnedLogoutGiftPendingContinuation = continuation;
            _lastPacketOwnedLogoutGiftLaunchSource = string.IsNullOrWhiteSpace(launchSource) ? "simulator" : launchSource.Trim();
            _packetOwnedLogoutGiftOwnerInstantiated = true;
            CopyPacketOwnedLogoutGiftContextCacheToOwnerLocalCache();
            window.Position = ResolvePacketOwnedLogoutGiftWindowPosition(window);
            ShowWindow(
                MapSimulatorWindowNames.LogoutGift,
                window,
                trackDirectionModeOwner: true);
            uiWindowManager?.BringToFront(window);

            message = $"CUILogoutGift::TryShowLogoutGiftDialog presented the dedicated owner from {_lastPacketOwnedLogoutGiftLaunchSource} using cached commodity SNs {FormatPacketOwnedLogoutGiftCommodityList()}.";
            _lastPacketOwnedLogoutGiftSummary = message;
            NotifyEventAlarmOwnerActivity("packet-owned logout gift");
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
                out int predictQuitRawValue,
                out int[] commoditySerialNumbers,
                out byte[] leadingOpaqueBytes,
                out int[] leadingOpaqueInt32Values,
                out byte[] trailingOpaqueBytes,
                out int[] trailingOpaqueInt32Values,
                out int logoutGiftConfigOffset,
                out string decodeError))
            {
                ResetPacketOwnedLogoutGiftRuntimeState(
                    clearConfig: true,
                    hideWindow: true,
                    summary: decodeError);
                return;
            }

            for (int i = 0; i < PacketOwnedLogoutGiftEntryCount; i++)
            {
                _packetOwnedLogoutGiftCommoditySerialNumbers[i] = commoditySerialNumbers[i];
            }

            _packetOwnedLogoutGiftLeadingOpaqueBytes = leadingOpaqueBytes ?? Array.Empty<byte>();
            _packetOwnedLogoutGiftLeadingOpaqueInt32Values = leadingOpaqueInt32Values ?? Array.Empty<int>();
            _packetOwnedLogoutGiftLeadingContextFields = DecodePacketOwnedLogoutGiftLeadingContextFields(_packetOwnedLogoutGiftLeadingOpaqueInt32Values);
            _packetOwnedLogoutGiftTrailingOpaqueBytes = trailingOpaqueBytes ?? Array.Empty<byte>();
            _packetOwnedLogoutGiftTrailingOpaqueInt32Values = trailingOpaqueInt32Values ?? Array.Empty<int>();
            _packetOwnedLogoutGiftPredictQuitContextField = DecodePacketOwnedLogoutGiftPredictQuitContextField(predictQuitRawValue);
            _packetOwnedLogoutGiftCommodityContextFields = DecodePacketOwnedLogoutGiftCommodityContextFields(_packetOwnedLogoutGiftCommoditySerialNumbers);
            _packetOwnedLogoutGiftHasPredictQuitFlag = true;
            _packetOwnedLogoutGiftPredictQuitRawValue = predictQuitRawValue;
            _packetOwnedLogoutGiftHasConfig = HasDecodedPacketOwnedLogoutGiftConfig(commoditySerialNumbers);
            _packetOwnedLogoutGiftSelectedIndex = ResolveFirstPacketOwnedLogoutGiftSelection();
            bool hasCommodity = HasAnyPacketOwnedLogoutGiftCommodity(commoditySerialNumbers);
            string precursorContextSummary = DescribePacketOwnedLogoutGiftLeadingContextFields(_packetOwnedLogoutGiftLeadingContextFields);
            string configOffsetSummary = logoutGiftConfigOffset > 0
                ? $" at tail offset {logoutGiftConfigOffset.ToString(CultureInfo.InvariantCulture)}"
                : string.Empty;
            string mappedPrecursorContextSuffix = string.IsNullOrWhiteSpace(precursorContextSummary)
                ? string.Empty
                : $" Mapped contiguous precursor dword slots immediately before `CWvsContext::m_bPredictQuit`: {precursorContextSummary}.";
            _lastPacketOwnedLogoutGiftSummary = _packetOwnedLogoutGiftHasConfig
                ? HasPacketOwnedLogoutGiftOpaqueTail()
                    ? $"Split the character-data SetField tail into {DescribePacketOwnedLogoutGiftOpaqueTail()} around the client `CWvsContext::OnSetLogoutGiftConfig` cache{configOffsetSummary} (`m_bPredictQuit={predictQuitRawValue.ToString(CultureInfo.InvariantCulture)}` and commodity SNs at dword[{PacketOwnedLogoutGiftCommodityContextDwordIndex.ToString(CultureInfo.InvariantCulture)}..{(PacketOwnedLogoutGiftCommodityContextDwordIndex + PacketOwnedLogoutGiftEntryCount - 1).ToString(CultureInfo.InvariantCulture)}]): {FormatPacketOwnedLogoutGiftCommodityList()}.{(hasCommodity ? string.Empty : " All three commodity slots are zero, but the decoded cache remains owned by the logout-gift context fields.")}{mappedPrecursorContextSuffix}"
                    : hasCommodity
                        ? $"Decoded `CWvsContext::OnSetLogoutGiftConfig` from character-data SetField with `m_bPredictQuit={predictQuitRawValue.ToString(CultureInfo.InvariantCulture)}` and commodity SNs {FormatPacketOwnedLogoutGiftCommodityList()}. Packet 432 now refreshes the dedicated owner instead of leaving the values hidden in stage payload tail bytes.{mappedPrecursorContextSuffix}"
                        : $"Decoded the explicit `CWvsContext::OnSetLogoutGiftConfig` payload from SetField with `m_bPredictQuit={predictQuitRawValue.ToString(CultureInfo.InvariantCulture)}` and all three commodity slots zero; the simulator preserves the client cache as present rather than collapsing it to missing config.{mappedPrecursorContextSuffix}"
                : "Character-data SetField did not decode a complete logout-gift cache payload.";
            NotifyEventAlarmOwnerActivity("packet-owned logout gift");

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

        internal static int[] ResolvePacketOwnedLogoutGiftDisplayedCommoditySerialNumbers(
            IReadOnlyList<int> contextCommoditySerialNumbers,
            IReadOnlyList<int> ownerCommoditySerialNumbers,
            bool ownerInstantiated)
        {
            int[] displayedCommoditySerialNumbers = new int[PacketOwnedLogoutGiftEntryCount];
            IReadOnlyList<int> source = ownerInstantiated
                ? ownerCommoditySerialNumbers
                : contextCommoditySerialNumbers;

            for (int i = 0; i < PacketOwnedLogoutGiftEntryCount; i++)
            {
                displayedCommoditySerialNumbers[i] = source != null && i < source.Count
                    ? source[i]
                    : 0;
            }

            return displayedCommoditySerialNumbers;
        }

        private void CopyPacketOwnedLogoutGiftContextCacheToOwnerLocalCache()
        {
            for (int i = 0; i < PacketOwnedLogoutGiftEntryCount; i++)
            {
                _packetOwnedLogoutGiftOwnerCommoditySerialNumbers[i] = i < _packetOwnedLogoutGiftCommoditySerialNumbers.Length
                    ? _packetOwnedLogoutGiftCommoditySerialNumbers[i]
                    : 0;
            }
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
                _packetOwnedLogoutGiftTrailingOpaqueBytes = Array.Empty<byte>();
                _packetOwnedLogoutGiftTrailingOpaqueInt32Values = Array.Empty<int>();
                _packetOwnedLogoutGiftPredictQuitContextField = null;
                _packetOwnedLogoutGiftCommodityContextFields = Array.Empty<PacketOwnedLogoutGiftContextField>();
                _packetOwnedLogoutGiftHasPredictQuitFlag = false;
                _packetOwnedLogoutGiftPredictQuitRawValue = 0;
                Array.Clear(_packetOwnedLogoutGiftOwnerCommoditySerialNumbers, 0, _packetOwnedLogoutGiftOwnerCommoditySerialNumbers.Length);
                _lastPacketOwnedLogoutGiftSelectionRequestIndex = -1;
                _lastPacketOwnedLogoutGiftSelectionTick = int.MinValue;
                _lastPacketOwnedLogoutGiftLaunchSource = string.Empty;
                _packetOwnedLogoutGiftOwnerInstantiated = false;
                _packetOwnedLogoutGiftPendingContinuation = PacketOwnedLogoutGiftContinuation.None;
            }

            if (hideWindow)
            {
                uiWindowManager?.HideWindow(MapSimulatorWindowNames.LogoutGift);
            }

            if (!string.IsNullOrWhiteSpace(summary))
            {
                _lastPacketOwnedLogoutGiftSummary = summary;
                NotifyEventAlarmOwnerActivity("packet-owned logout gift");
            }
        }

        private Texture2D LoadPacketOwnedLogoutGiftFrameTexture()
        {
            if (_packetOwnedLogoutGiftFrameTexture != null || GraphicsDevice == null)
            {
                return _packetOwnedLogoutGiftFrameTexture;
            }

            string resourcePath = ResolvePacketOwnedLogoutGiftDialogFrameResourcePath();
            if (TryDecodePacketOwnedLogoutGiftDialogFrameResourcePath(resourcePath, out string preferredImageName, out string preferredPropertyPath))
            {
                _packetOwnedLogoutGiftFrameTexture = TryLoadPacketOwnedLogoutGiftFrameTexture(preferredImageName, preferredPropertyPath);
                if (_packetOwnedLogoutGiftFrameTexture != null
                    || Array.Exists(PacketOwnedLogoutGiftUiImageNames, imageName => string.Equals(imageName, preferredImageName, StringComparison.OrdinalIgnoreCase)))
                {
                    return _packetOwnedLogoutGiftFrameTexture;
                }
            }

            foreach (string imageName in PacketOwnedLogoutGiftUiImageNames)
            {
                _packetOwnedLogoutGiftFrameTexture = TryLoadPacketOwnedLogoutGiftFrameTexture(imageName, PacketOwnedLogoutGiftUiPath);
                if (_packetOwnedLogoutGiftFrameTexture != null)
                {
                    break;
                }
            }

            return _packetOwnedLogoutGiftFrameTexture;
        }

        private LogoutGiftButtonSkin LoadPacketOwnedLogoutGiftButtonSkin()
        {
            if (_packetOwnedLogoutGiftButtonSkin != null || GraphicsDevice == null)
            {
                return _packetOwnedLogoutGiftButtonSkin;
            }

            foreach (string resourcePath in EnumeratePacketOwnedLogoutGiftButtonResourcePathCandidates())
            {
                _packetOwnedLogoutGiftButtonSkin = TryLoadPacketOwnedLogoutGiftButtonSkinFromResourcePath(resourcePath);
                if (_packetOwnedLogoutGiftButtonSkin != null)
                {
                    return _packetOwnedLogoutGiftButtonSkin;
                }
            }

            foreach (string imageName in PacketOwnedLogoutGiftUiImageNames)
            {
                _packetOwnedLogoutGiftButtonSkin = TryLoadPacketOwnedLogoutGiftButtonSkin(imageName, PacketOwnedLogoutGiftButtonUiPath);
                if (_packetOwnedLogoutGiftButtonSkin != null)
                {
                    break;
                }
            }

            return _packetOwnedLogoutGiftButtonSkin;
        }

        private Texture2D TryLoadPacketOwnedLogoutGiftFrameTexture(string imageName, string propertyPath)
        {
            if (GraphicsDevice == null || string.IsNullOrWhiteSpace(imageName) || string.IsNullOrWhiteSpace(propertyPath))
            {
                return null;
            }

            WzImage uiWindowImage = global::HaCreator.Program.FindImage("UI", imageName.Trim());
            WzCanvasProperty backgroundCanvas = uiWindowImage?[propertyPath.Trim()] as WzCanvasProperty;
            return LoadUiCanvasTexture(backgroundCanvas);
        }

        private LogoutGiftButtonSkin TryLoadPacketOwnedLogoutGiftButtonSkinFromResourcePath(string resourcePath)
        {
            if (!TryDecodePacketOwnedLogoutGiftDialogFrameResourcePath(resourcePath, out string imageName, out string propertyPath))
            {
                return null;
            }

            return TryLoadPacketOwnedLogoutGiftButtonSkin(imageName, propertyPath);
        }

        private LogoutGiftButtonSkin TryLoadPacketOwnedLogoutGiftButtonSkin(string imageName, string propertyPath)
        {
            if (GraphicsDevice == null || string.IsNullOrWhiteSpace(imageName) || string.IsNullOrWhiteSpace(propertyPath))
            {
                return null;
            }

            WzImage uiWindowImage = global::HaCreator.Program.FindImage("UI", imageName.Trim());
            WzSubProperty buttonProperty = uiWindowImage?[propertyPath.Trim()] as WzSubProperty;
            if (buttonProperty == null)
            {
                return null;
            }

            Texture2D normal = LoadPacketOwnedLogoutGiftButtonStateTexture(buttonProperty, "normal", "1", "0");
            Texture2D hovered = LoadPacketOwnedLogoutGiftButtonStateTexture(buttonProperty, "mouseOver", "3", "1");
            Texture2D pressed = LoadPacketOwnedLogoutGiftButtonStateTexture(buttonProperty, "pressed", "2", "3");
            Texture2D disabled = LoadPacketOwnedLogoutGiftButtonStateTexture(buttonProperty, "disabled", "0", "4");
            Texture2D keyFocused = LoadPacketOwnedLogoutGiftButtonStateTexture(buttonProperty, "keyFocused", "1", "0");
            if (normal == null && hovered == null && pressed == null && disabled == null && keyFocused == null)
            {
                return null;
            }

            return new LogoutGiftButtonSkin(
                normal,
                hovered,
                pressed,
                disabled,
                keyFocused);
        }

        private Texture2D LoadPacketOwnedLogoutGiftButtonStateTexture(
            WzSubProperty buttonProperty,
            string namedState,
            params string[] indexedFallbacks)
        {
            if (buttonProperty == null)
            {
                return null;
            }

            if (buttonProperty[namedState] is WzCanvasProperty directCanvas)
            {
                return LoadUiCanvasTexture(directCanvas);
            }

            if (buttonProperty[namedState] is WzSubProperty stateProperty)
            {
                Texture2D namedTexture = LoadPacketOwnedLogoutGiftIndexedButtonTexture(stateProperty, "0");
                if (namedTexture != null)
                {
                    return namedTexture;
                }

                foreach (string candidate in indexedFallbacks)
                {
                    Texture2D indexedTexture = LoadPacketOwnedLogoutGiftIndexedButtonTexture(stateProperty, candidate);
                    if (indexedTexture != null)
                    {
                        return indexedTexture;
                    }
                }
            }

            foreach (string candidate in indexedFallbacks)
            {
                Texture2D indexedTexture = LoadPacketOwnedLogoutGiftIndexedButtonTexture(buttonProperty, candidate);
                if (indexedTexture != null)
                {
                    return indexedTexture;
                }
            }

            return null;
        }

        private Texture2D LoadPacketOwnedLogoutGiftIndexedButtonTexture(WzSubProperty property, string childName)
        {
            if (property == null || string.IsNullOrWhiteSpace(childName))
            {
                return null;
            }

            return LoadUiCanvasTexture(property[childName.Trim()] as WzCanvasProperty);
        }

        private Texture2D LoadPacketOwnedLogoutGiftItemIcon(int itemId)
        {
            return IsPacketOwnedLogoutGiftSpecialItem(itemId)
                ? LoadPacketOwnedLogoutGiftSpecialItemIcon(itemId)
                : LoadInventoryItemIcon(itemId);
        }

        private Texture2D LoadPacketOwnedLogoutGiftSpecialItemIcon(int itemId)
        {
            if (GraphicsDevice == null || !TryResolvePacketOwnedLogoutGiftSpecialItemProperty(itemId, out WzSubProperty itemProperty))
            {
                return null;
            }

            WzCanvasProperty iconCanvas = itemProperty?["icon"] as WzCanvasProperty;
            return iconCanvas?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(GraphicsDevice);
        }

        private static bool TryResolvePacketOwnedLogoutGiftSpecialItemName(int itemId, out string itemName)
        {
            itemName = null;
            if (!TryResolvePacketOwnedLogoutGiftSpecialItemProperty(itemId, out WzSubProperty itemProperty))
            {
                return false;
            }

            itemName = (itemProperty?["name"] as WzStringProperty)?.Value;
            return !string.IsNullOrWhiteSpace(itemName);
        }

        private static bool TryResolvePacketOwnedLogoutGiftSpecialItemProperty(int itemId, out WzSubProperty itemProperty)
        {
            itemProperty = null;
            if (!IsPacketOwnedLogoutGiftSpecialItem(itemId))
            {
                return false;
            }

            WzImage specialImage = global::HaCreator.Program.FindImage("Item", $"Special/{itemId / 10000:D4}");
            if (specialImage == null)
            {
                return false;
            }

            specialImage.ParseImage();
            itemProperty = specialImage[itemId.ToString("D7", CultureInfo.InvariantCulture)] as WzSubProperty;
            return itemProperty != null;
        }

        private static bool IsPacketOwnedLogoutGiftSpecialItem(int itemId)
        {
            return itemId / 100000 == PacketOwnedLogoutGiftSpecialItemFamily;
        }

        private static string DescribePacketOwnedLogoutGiftOpaqueTailSegment(
            byte[] opaqueBytes,
            int[] alignedInt32Values,
            string placementLabel)
        {
            if (opaqueBytes == null || opaqueBytes.Length == 0)
            {
                return string.Empty;
            }

            string hex = Convert.ToHexString(opaqueBytes);
            if (alignedInt32Values == null || alignedInt32Values.Length == 0)
            {
                return $"{opaqueBytes.Length.ToString(CultureInfo.InvariantCulture)} {placementLabel} opaque byte(s) [0x{hex}]";
            }

            List<string> values = new(alignedInt32Values.Length);
            foreach (int value in alignedInt32Values)
            {
                values.Add(value.ToString(CultureInfo.InvariantCulture));
            }

            return $"{opaqueBytes.Length.ToString(CultureInfo.InvariantCulture)} {placementLabel} opaque byte(s) [0x{hex}] / aligned int32 [{string.Join(", ", values)}]";
        }

        private bool HasPacketOwnedLogoutGiftOpaqueTail()
        {
            return (_packetOwnedLogoutGiftLeadingOpaqueBytes?.Length ?? 0) > 0
                || (_packetOwnedLogoutGiftTrailingOpaqueBytes?.Length ?? 0) > 0;
        }

        private string DescribePacketOwnedLogoutGiftOpaqueTail()
        {
            string leading = DescribePacketOwnedLogoutGiftOpaqueTailSegment(
                _packetOwnedLogoutGiftLeadingOpaqueBytes,
                _packetOwnedLogoutGiftLeadingOpaqueInt32Values,
                "pre-`OnSetLogoutGiftConfig`");
            string trailing = DescribePacketOwnedLogoutGiftOpaqueTailSegment(
                _packetOwnedLogoutGiftTrailingOpaqueBytes,
                _packetOwnedLogoutGiftTrailingOpaqueInt32Values,
                "post-`OnSetLogoutGiftConfig`");
            if (string.IsNullOrWhiteSpace(leading))
            {
                return string.IsNullOrWhiteSpace(trailing) ? "no adjacent trailing bytes" : trailing;
            }

            if (string.IsNullOrWhiteSpace(trailing))
            {
                return leading;
            }

            return $"{leading}; {trailing}";
        }

        private string BuildPacketOwnedLogoutGiftSubtitle(int entryCount)
        {
            string stagePrefix = ResolveCurrentPacketOwnedLogoutGiftOwnerAvailability() == PacketOwnedLogoutGiftOwnerAvailability.StageNotField
                ? "Stage!=CField (TryShowLogoutGiftDialog blocked) "
                : string.Empty;
            string predictQuitPrefix = _packetOwnedLogoutGiftHasPredictQuitFlag
                ? $"CWvsContext m_bPredictQuit={(_packetOwnedLogoutGiftPredictQuitRawValue != 0 ? "true" : "false")} (TryShowLogoutGiftDialog gate) "
                : string.Empty;
            string commodityContextSuffix = BuildPacketOwnedLogoutGiftCommodityContextSuffix();
            return HasPacketOwnedLogoutGiftOpaqueTail()
                ? $"{stagePrefix}{predictQuitPrefix}cached {entryCount} logout-gift slots plus {DescribePacketOwnedLogoutGiftOpaqueTail()}.{commodityContextSuffix}"
                : $"{stagePrefix}{predictQuitPrefix}cached {entryCount} logout-gift commodity slot(s).{commodityContextSuffix}";
        }

        private bool ShouldShowPacketOwnedLogoutGiftOwner()
        {
            return ResolveCurrentPacketOwnedLogoutGiftOwnerAvailability() == PacketOwnedLogoutGiftOwnerAvailability.Available;
        }

        private PacketOwnedLogoutGiftOwnerAvailability ResolveCurrentPacketOwnedLogoutGiftOwnerAvailability()
        {
            return ResolvePacketOwnedLogoutGiftOwnerAvailability(
                IsPacketOwnedLogoutGiftFieldStageActive(),
                _packetOwnedLogoutGiftHasPredictQuitFlag,
                _packetOwnedLogoutGiftPredictQuitRawValue);
        }

        private bool IsPacketOwnedLogoutGiftFieldStageActive()
        {
            return ResolvePacketOwnedLogoutGiftFieldStageActive(
                _gameState?.IsLoginMap == true,
                _gameState?.IsCashShopMap == true,
                ShouldRunCashServicePacketInbox(),
                IsPacketOwnedLogoutGiftStageWindowVisible(MapSimulatorWindowNames.CashShopStage),
                IsPacketOwnedLogoutGiftStageWindowVisible(MapSimulatorWindowNames.MtsStatus));
        }

        private bool HasVisiblePacketOwnedLogoutGiftNonFieldStage()
        {
            return IsPacketOwnedLogoutGiftStageWindowVisible(MapSimulatorWindowNames.CashShopStage)
                || IsPacketOwnedLogoutGiftStageWindowVisible(MapSimulatorWindowNames.MtsStatus);
        }

        private bool IsPacketOwnedLogoutGiftStageWindowVisible(string windowName)
        {
            return !string.IsNullOrWhiteSpace(windowName)
                && uiWindowManager?.GetWindow(windowName)?.IsVisible == true;
        }

        internal static string ResolvePacketOwnedLogoutGiftDialogFrameResourcePath()
        {
            return MapleStoryStringPool.GetOrFallback(
                PacketOwnedLogoutGiftDialogFrameStringPoolId,
                PacketOwnedLogoutGiftDialogFrameFallbackPath);
        }

        internal static string ResolvePacketOwnedLogoutGiftButtonResourcePath()
        {
            return MapleStoryStringPool.GetOrFallback(
                PacketOwnedLogoutGiftButtonPathStringPoolId,
                PacketOwnedLogoutGiftButtonFallbackPath);
        }

        internal static bool TryDecodePacketOwnedLogoutGiftDialogFrameResourcePath(
            string resourcePath,
            out string imageName,
            out string propertyPath)
        {
            imageName = null;
            propertyPath = null;
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                return false;
            }

            string normalized = resourcePath.Trim().Replace('\\', '/');
            const string categoryPrefix = "UI/";
            if (normalized.StartsWith(categoryPrefix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[categoryPrefix.Length..];
            }

            int separatorIndex = normalized.IndexOf('/');
            if (separatorIndex <= 0 || separatorIndex >= normalized.Length - 1)
            {
                return false;
            }

            imageName = normalized[..separatorIndex];
            propertyPath = normalized[(separatorIndex + 1)..];
            return !string.IsNullOrWhiteSpace(imageName) && !string.IsNullOrWhiteSpace(propertyPath);
        }

        internal static string BuildPacketOwnedLogoutGiftCompletionMessage()
        {
            return MapleStoryStringPool.GetOrFallback(
                PacketOwnedLogoutGiftCompletionStringPoolId,
                PacketOwnedLogoutGiftCompletionFallbackText,
                appendFallbackSuffix: true,
                minimumHexWidth: 4);
        }

        internal static LoginUtilityDialogFrameVariant ResolvePacketOwnedLogoutGiftCompletionDialogFrameVariant()
        {
            return LoginUtilityDialogFrameVariant.UtilDlgNotice;
        }

        private string ShowPacketOwnedLogoutGiftCompletionDialog()
        {
            string completionMessage = BuildPacketOwnedLogoutGiftCompletionMessage();
            if (TryEnsurePacketOwnedLogoutGiftCompletionDialogOwner())
            {
                ShowLoginUtilityDialog(
                    "Logout Gift",
                    completionMessage,
                    LoginUtilityDialogButtonLayout.Ok,
                    LoginUtilityDialogAction.LogoutGiftCompletion,
                    frameVariant: ResolvePacketOwnedLogoutGiftCompletionDialogFrameVariant(),
                    trackDirectionModeOwner: true);
                return
                    $"Surfaced the client follow-up util dialog through the shared LoginUtilityDialog owner (StringPool 0x{PacketOwnedLogoutGiftCompletionStringPoolId.ToString("X", CultureInfo.InvariantCulture)}): {completionMessage}";
            }

            string continuationSuffix = CompletePacketOwnedLogoutGiftContinuation();
            DestroyPacketOwnedLogoutGiftOwnerSingleton();
            return
                $"Client follow-up util dialog owner was unavailable, so the simulator kept StringPool 0x{PacketOwnedLogoutGiftCompletionStringPoolId.ToString("X", CultureInfo.InvariantCulture)} local: {completionMessage}{continuationSuffix}";
        }

        private bool TryEnsurePacketOwnedLogoutGiftCompletionDialogOwner()
        {
            if (uiWindowManager == null || GraphicsDevice == null)
            {
                return false;
            }

            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.LoginUtilityDialog) is LoginUtilityDialogWindow)
            {
                return true;
            }

            WzImage uiWindow2Image = global::HaCreator.Program.FindImage("UI", "UIWindow2.img");
            WzImage loginImage = global::HaCreator.Program.FindImage("UI", "Login.img");
            WzImage basicImage = global::HaCreator.Program.FindImage("UI", "Basic.img");
            WzImage soundUIImage = global::HaCreator.Program.FindImage("Sound", "UI.img");
            UIWindowLoader.RegisterLoginUtilityDialogWindow(
                uiWindowManager,
                uiWindow2Image,
                loginImage,
                basicImage,
                soundUIImage,
                GraphicsDevice,
                _renderParams.RenderWidth,
                _renderParams.RenderHeight);
            return uiWindowManager.GetWindow(MapSimulatorWindowNames.LoginUtilityDialog) is LoginUtilityDialogWindow;
        }

        private void HandlePacketOwnedLogoutGiftCompletionDialogDismissed()
        {
            DestroyPacketOwnedLogoutGiftOwnerSingleton();
            string continuationSuffix = CompletePacketOwnedLogoutGiftContinuation();
            _lastPacketOwnedLogoutGiftSummary = string.IsNullOrWhiteSpace(continuationSuffix)
                ? $"Dismissed the logout-gift completion util dialog (StringPool 0x{PacketOwnedLogoutGiftCompletionStringPoolId.ToString("X", CultureInfo.InvariantCulture)}) and destroyed the instantiated logout-gift singleton."
                : $"Dismissed the logout-gift completion util dialog (StringPool 0x{PacketOwnedLogoutGiftCompletionStringPoolId.ToString("X", CultureInfo.InvariantCulture)}) and destroyed the instantiated logout-gift singleton.{continuationSuffix}";
            NotifyEventAlarmOwnerActivity("packet-owned logout gift");

            if (string.IsNullOrWhiteSpace(continuationSuffix))
            {
                ShowUtilityFeedbackMessage(_lastPacketOwnedLogoutGiftSummary);
            }
        }

        private string DispatchPacketOwnedLogoutGiftSelectionRequest(int buttonId)
        {
            if (!TryBuildPacketOwnedLogoutGiftSelectionPayload(buttonId, out byte[] payload, out int slotIndex))
            {
                return $"Skipped opcode {PacketOwnedLogoutGiftSelectionOpcode} because button {buttonId.ToString(CultureInfo.InvariantCulture)} is outside the client CUILogoutGift::OnButtonClicked select range 1000..1002.";
            }

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
            byte[] payload = new byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(payload, index);
            return payload;
        }

        internal static bool TryBuildPacketOwnedLogoutGiftSelectionPayload(int buttonId, out byte[] payload, out int slotIndex)
        {
            payload = Array.Empty<byte>();
            if (!LogoutGiftWindow.TryResolveClientSelectButtonIndex(buttonId, out slotIndex))
            {
                return false;
            }

            payload = BuildPacketOwnedLogoutGiftSelectionPayload(slotIndex);
            return true;
        }

        internal static bool IsPacketOwnedLogoutGiftOwnerSingletonPresent(bool ownerInstantiated, bool ownerVisible)
        {
            return ownerInstantiated || ownerVisible;
        }

        internal static bool HasDecodedPacketOwnedLogoutGiftConfig(int[] commoditySerialNumbers)
        {
            return commoditySerialNumbers != null && commoditySerialNumbers.Length == PacketOwnedLogoutGiftEntryCount;
        }

        internal static bool HasAnyPacketOwnedLogoutGiftCommodity(int[] commoditySerialNumbers)
        {
            if (commoditySerialNumbers == null)
            {
                return false;
            }

            for (int i = 0; i < commoditySerialNumbers.Length; i++)
            {
                if (commoditySerialNumbers[i] > 0)
                {
                    return true;
                }
            }

            return false;
        }

        internal static PacketOwnedLogoutGiftRefreshDisposition ResolvePacketOwnedLogoutGiftRefreshDisposition(
            bool hasConfig,
            bool ownerSingletonPresent,
            bool ownerVisible,
            bool shouldShowOwner)
        {
            if (!hasConfig)
            {
                return PacketOwnedLogoutGiftRefreshDisposition.MissingConfig;
            }

            if (ownerSingletonPresent)
            {
                return ownerVisible
                    ? PacketOwnedLogoutGiftRefreshDisposition.RefreshVisibleOwner
                    : PacketOwnedLogoutGiftRefreshDisposition.RefreshHiddenInstantiatedOwner;
            }

            return shouldShowOwner
                ? PacketOwnedLogoutGiftRefreshDisposition.NoInstantiatedOwner
                : PacketOwnedLogoutGiftRefreshDisposition.NoOwnerAllowed;
        }

        internal static PacketOwnedLogoutGiftOwnerAvailability ResolvePacketOwnedLogoutGiftOwnerAvailability(
            bool isFieldStageActive,
            bool hasPredictQuitFlag,
            int predictQuitRawValue)
        {
            if (!isFieldStageActive)
            {
                return PacketOwnedLogoutGiftOwnerAvailability.StageNotField;
            }

            if (hasPredictQuitFlag && predictQuitRawValue == 0)
            {
                return PacketOwnedLogoutGiftOwnerAvailability.PredictQuitFalse;
            }

            return PacketOwnedLogoutGiftOwnerAvailability.Available;
        }

        internal static bool ResolvePacketOwnedLogoutGiftFieldStageActive(
            bool isLoginMap,
            bool isCashShopMap,
            bool hasVisibleCashServiceOwner,
            bool isCashShopStageVisible,
            bool isMtsStageVisible)
        {
            if (isLoginMap || isCashShopMap || hasVisibleCashServiceOwner)
            {
                return false;
            }

            return !isCashShopStageVisible && !isMtsStageVisible;
        }

        internal static bool ShouldRoutePacketOwnedLogoutGiftSystemCloseShortcut(
            bool isWindowActive,
            bool f4Down,
            bool previousF4Down,
            bool leftAltDown,
            bool rightAltDown)
        {
            return isWindowActive
                && f4Down
                && !previousF4Down
                && (leftAltDown || rightAltDown);
        }

        internal static string DescribePacketOwnedLogoutGiftLeadingInt32Values(IReadOnlyList<int> leadingOpaqueInt32Values)
        {
            if (leadingOpaqueInt32Values == null || leadingOpaqueInt32Values.Count == 0)
            {
                return string.Empty;
            }

            List<string> values = new(leadingOpaqueInt32Values.Count);
            foreach (int value in leadingOpaqueInt32Values)
            {
                values.Add(value.ToString(CultureInfo.InvariantCulture));
            }

            return string.Join(", ", values);
        }

        internal static PacketOwnedLogoutGiftContextField DecodePacketOwnedLogoutGiftPredictQuitContextField(int predictQuitRawValue)
        {
            return new PacketOwnedLogoutGiftContextField(
                PacketOwnedLogoutGiftPredictQuitContextDwordIndex,
                PacketOwnedLogoutGiftPredictQuitContextByteOffset,
                predictQuitRawValue,
                "CWvsContext::m_bPredictQuit");
        }

        internal static PacketOwnedLogoutGiftContextField[] DecodePacketOwnedLogoutGiftLeadingContextFields(int[] leadingOpaqueInt32Values)
        {
            if (leadingOpaqueInt32Values == null || leadingOpaqueInt32Values.Length == 0)
            {
                return Array.Empty<PacketOwnedLogoutGiftContextField>();
            }

            int mappedValueCount = leadingOpaqueInt32Values.Length;
            int firstDwordIndex = PacketOwnedLogoutGiftPredictQuitContextDwordIndex - mappedValueCount;
            PacketOwnedLogoutGiftContextField[] fields = new PacketOwnedLogoutGiftContextField[mappedValueCount];
            for (int i = 0; i < mappedValueCount; i++)
            {
                int dwordIndex = firstDwordIndex + i;
                fields[i] = new PacketOwnedLogoutGiftContextField(
                    dwordIndex,
                    ResolvePacketOwnedLogoutGiftContextByteOffset(dwordIndex),
                    leadingOpaqueInt32Values[i],
                    ResolvePacketOwnedLogoutGiftLeadingContextSemanticName(dwordIndex));
            }

            return fields;
        }

        internal static PacketOwnedLogoutGiftContextField[] DecodePacketOwnedLogoutGiftCommodityContextFields(int[] commoditySerialNumbers)
        {
            if (commoditySerialNumbers == null || commoditySerialNumbers.Length == 0)
            {
                return Array.Empty<PacketOwnedLogoutGiftContextField>();
            }

            PacketOwnedLogoutGiftContextField[] fields = new PacketOwnedLogoutGiftContextField[commoditySerialNumbers.Length];
            for (int i = 0; i < commoditySerialNumbers.Length; i++)
            {
                int dwordIndex = PacketOwnedLogoutGiftCommodityContextDwordIndex + i;
                fields[i] = new PacketOwnedLogoutGiftContextField(
                    dwordIndex,
                    ResolvePacketOwnedLogoutGiftContextByteOffset(dwordIndex),
                    commoditySerialNumbers[i],
                    $"CWvsContext::m_anLogoutGiftCommoditySN[{i}]");
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
                        : !string.IsNullOrWhiteSpace(field.SemanticName)
                            ? $"{field.SemanticName}@0x{field.ByteOffset.ToString("X", CultureInfo.InvariantCulture)}={field.Value.ToString(CultureInfo.InvariantCulture)}"
                            : $"CWvsContext dword[{field.DwordIndex.ToString(CultureInfo.InvariantCulture)}]@0x{field.ByteOffset.ToString("X", CultureInfo.InvariantCulture)}={field.Value.ToString(CultureInfo.InvariantCulture)}");
            }

            return string.Join(", ", parts);
        }

        private string BuildPacketOwnedLogoutGiftCommodityContextSuffix()
        {
            string description = DescribePacketOwnedLogoutGiftCommodityContextFields(_packetOwnedLogoutGiftCommodityContextFields);
            return string.IsNullOrWhiteSpace(description)
                ? string.Empty
                : $" Client cache {description}.";
        }

        private string BuildPacketOwnedLogoutGiftContextOwnershipSuffix()
        {
            string predictQuit = _packetOwnedLogoutGiftPredictQuitContextField.HasValue
                ? DescribePacketOwnedLogoutGiftLeadingContextFields(new[] { _packetOwnedLogoutGiftPredictQuitContextField.Value })
                : string.Empty;
            string commodity = DescribePacketOwnedLogoutGiftCommodityContextFields(_packetOwnedLogoutGiftCommodityContextFields);
            List<string> parts = new(3);
            string leading = DescribePacketOwnedLogoutGiftLeadingInt32Values(_packetOwnedLogoutGiftLeadingOpaqueInt32Values);
            if (!string.IsNullOrWhiteSpace(leading))
            {
                parts.Add($"pre-`OnSetLogoutGiftConfig` aligned tail int32 [{leading}]");
            }

            string precursorContext = DescribePacketOwnedLogoutGiftLeadingContextFields(_packetOwnedLogoutGiftLeadingContextFields);
            if (!string.IsNullOrWhiteSpace(precursorContext))
            {
                parts.Add(precursorContext);
            }

            string trailing = DescribePacketOwnedLogoutGiftLeadingInt32Values(_packetOwnedLogoutGiftTrailingOpaqueInt32Values);
            if (!string.IsNullOrWhiteSpace(trailing))
            {
                parts.Add($"post-`OnSetLogoutGiftConfig` aligned tail int32 [{trailing}]");
            }

            if (!string.IsNullOrWhiteSpace(predictQuit))
            {
                parts.Add(predictQuit);
            }

            if (!string.IsNullOrWhiteSpace(commodity))
            {
                parts.Add(commodity);
                parts.Add(DescribePacketOwnedLogoutGiftOwnerCommodityFields(_packetOwnedLogoutGiftCommodityContextFields));
            }

            return parts.Count == 0
                ? string.Empty
                : $" Client cache ownership: {string.Join("; ", parts)}.";
        }

        private static int ResolvePacketOwnedLogoutGiftContextByteOffset(int dwordIndex)
        {
            if (dwordIndex >= PacketOwnedLogoutGiftCommodityContextDwordIndex)
            {
                return PacketOwnedLogoutGiftCommodityContextByteOffset
                    + ((dwordIndex - PacketOwnedLogoutGiftCommodityContextDwordIndex) * sizeof(int));
            }

            return PacketOwnedLogoutGiftPredictQuitContextByteOffset
                - ((PacketOwnedLogoutGiftPredictQuitContextDwordIndex - dwordIndex) * sizeof(int));
        }

        private static string ResolvePacketOwnedLogoutGiftLeadingContextSemanticName(int dwordIndex)
        {
            int byteOffset = ResolvePacketOwnedLogoutGiftContextByteOffset(dwordIndex);
            return dwordIndex switch
            {
                PacketOwnedLogoutGiftPrecursorFirstContextDwordIndex => $"{PacketOwnedLogoutGiftPrecursorFirstContextSymbol} (dword[{dwordIndex.ToString(CultureInfo.InvariantCulture)}], pre-`m_bPredictQuit`)",
                PacketOwnedLogoutGiftPrecursorFirstContextDwordIndex + 1 => $"{PacketOwnedLogoutGiftPrecursorSecondContextSymbol} (dword[{dwordIndex.ToString(CultureInfo.InvariantCulture)}], pre-`m_bPredictQuit`)",
                PacketOwnedLogoutGiftPrecursorLastContextDwordIndex => $"{PacketOwnedLogoutGiftPrecursorThirdContextSymbol} (dword[{dwordIndex.ToString(CultureInfo.InvariantCulture)}], pre-`m_bPredictQuit`)",
                _ => $"CWvsContext::dword_{byteOffset.ToString("X4", CultureInfo.InvariantCulture)} (dword[{dwordIndex.ToString(CultureInfo.InvariantCulture)}], pre-`m_bPredictQuit`, unresolved semantic)",
            };
        }

        private static IEnumerable<string> EnumeratePacketOwnedLogoutGiftButtonResourcePathCandidates()
        {
            HashSet<string> emitted = new(StringComparer.OrdinalIgnoreCase);

            string stringPoolPath = ResolvePacketOwnedLogoutGiftButtonResourcePath();
            if (!string.IsNullOrWhiteSpace(stringPoolPath) && emitted.Add(stringPoolPath))
            {
                yield return stringPoolPath;
            }

            if (emitted.Add(PacketOwnedLogoutGiftButtonFallbackPath))
            {
                yield return PacketOwnedLogoutGiftButtonFallbackPath;
            }

            if (emitted.Add(PacketOwnedLogoutGiftButtonClientUolFallbackPath))
            {
                yield return PacketOwnedLogoutGiftButtonClientUolFallbackPath;
            }
        }

        private static string DescribePacketOwnedLogoutGiftCommodityContextFields(IReadOnlyList<PacketOwnedLogoutGiftContextField> fields)
        {
            if (fields == null || fields.Count == 0)
            {
                return string.Empty;
            }

            List<string> parts = new(fields.Count);
            foreach (PacketOwnedLogoutGiftContextField field in fields)
            {
                parts.Add($"{field.SemanticName}@0x{field.ByteOffset.ToString("X", CultureInfo.InvariantCulture)}={field.Value.ToString(CultureInfo.InvariantCulture)}");
            }

            return string.Join(", ", parts);
        }

        internal static string DescribePacketOwnedLogoutGiftOwnerCommodityFields(IReadOnlyList<PacketOwnedLogoutGiftContextField> contextFields)
        {
            if (contextFields == null || contextFields.Count == 0)
            {
                return string.Empty;
            }

            List<string> parts = new(contextFields.Count);
            for (int i = 0; i < contextFields.Count; i++)
            {
                PacketOwnedLogoutGiftContextField field = contextFields[i];
                int byteOffset = PacketOwnedLogoutGiftOwnerCommodityFieldByteOffset + (i * sizeof(int));
                parts.Add($"CUILogoutGift::m_aCommodityID[{i.ToString(CultureInfo.InvariantCulture)}]@0x{byteOffset.ToString("X", CultureInfo.InvariantCulture)}={field.Value.ToString(CultureInfo.InvariantCulture)}");
            }

            return $"owner-local copy {string.Join(", ", parts)}";
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

        private static string BuildPacketOwnedLogoutGiftOwnerUnavailableMessage(
            PacketOwnedLogoutGiftOwnerAvailability availability,
            string prefix)
        {
            return availability switch
            {
                PacketOwnedLogoutGiftOwnerAvailability.StageNotField =>
                    $"{prefix} The active simulator stage is not a `CField`, so the client `CUILogoutGift::TryShowLogoutGiftDialog` branch would return 1 without creating the singleton.",
                PacketOwnedLogoutGiftOwnerAvailability.PredictQuitFalse =>
                    $"{prefix} CWvsContext::m_bPredictQuit is false, so the client would not surface the owner.",
                _ => prefix
            };
        }

        private void DestroyPacketOwnedLogoutGiftOwnerSingleton()
        {
            _packetOwnedLogoutGiftOwnerInstantiated = false;
        }

        private string CompletePacketOwnedLogoutGiftContinuation()
        {
            PacketOwnedLogoutGiftContinuation continuation = _packetOwnedLogoutGiftPendingContinuation;
            _packetOwnedLogoutGiftPendingContinuation = PacketOwnedLogoutGiftContinuation.None;

            switch (continuation)
            {
                case PacketOwnedLogoutGiftContinuation.ExitSimulator:
                    Exit();
                    return " Simulator quit continuation executed after the logout-gift owner completed.";
                default:
                    return string.Empty;
            }
        }
    }

    internal readonly record struct PacketOwnedLogoutGiftContextField(int DwordIndex, int ByteOffset, int Value, string SemanticName = null);
    internal enum PacketOwnedLogoutGiftRefreshDisposition
    {
        MissingConfig = 0,
        NoOwnerAllowed = 1,
        NoInstantiatedOwner = 2,
        RefreshVisibleOwner = 3,
        RefreshHiddenInstantiatedOwner = 4,
    }

    internal enum PacketOwnedLogoutGiftOwnerAvailability
    {
        Available = 0,
        StageNotField = 1,
        PredictQuitFalse = 2,
    }

    internal enum PacketOwnedLogoutGiftContinuation
    {
        None = 0,
        ExitSimulator = 1,
    }
}
