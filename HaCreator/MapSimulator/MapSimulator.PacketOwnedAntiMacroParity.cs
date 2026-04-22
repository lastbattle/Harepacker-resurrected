using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.Interaction;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SD = System.Drawing;
using SDG = System.Drawing.Graphics;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int PacketOwnedAntiMacroPacketType = 1011;
        private const int PacketOwnedAntiMacroChallengeMode = 6;
        private const int PacketOwnedAntiMacroDestroyMode = 7;
        private const int PacketOwnedAntiMacroResultMode = 9;
        private const int PacketOwnedAntiMacroScreenshotReportMode = 4;
        private const int PacketOwnedAntiMacroUserReportMode = 5;
        private const int PacketOwnedAntiMacroScreenshotMode = 8;
        private const int PacketOwnedAntiMacroChatReportMode = 10;
        private const int PacketOwnedAntiMacroNoticeMode = 11;
        private const int PacketOwnedAntiMacroAnswerSubmitOpcode = 117;
        private const int PacketOwnedAntiMacroDefaultDurationMs = 60000;
        private static readonly Point PacketOwnedAntiMacroBackground2Offset = new(3, 7);
        private static readonly Point PacketOwnedAntiMacroBackground3Offset = new(5, 23);
        private const string PacketOwnedAntiMacroPopupCanvasPath = "Macro/popup";
        private const string PacketOwnedAntiMacroPopupOkButtonPath = "Macro/btOK";
        private const string PacketOwnedAntiMacroPopupCancelButtonPath = "Macro/btCancle";
        private const string PacketOwnedAntiMacroAdminCanvas0Path = "Macro/admin/0";
        private const string PacketOwnedAntiMacroAdminCanvas1Path = "Macro/admin/1";
        private const string PacketOwnedAntiMacroAdminCanvas2Path = "Macro/admin/2";
        private const string PacketOwnedAntiMacroNormalStepPath = "step1";
        private const string PacketOwnedAntiMacroAdminStepPath = "step1_admin";

        private string _lastPacketOwnedAntiMacroSummary = "Packet-owned anti-macro idle.";
        private string _lastPacketOwnedAntiMacroNotice = string.Empty;
        private string _lastPacketOwnedAntiMacroScreenshotPath = string.Empty;
        private string _lastPacketOwnedAntiMacroScreenshotBaseFolder = string.Empty;
        private int _lastPacketOwnedAntiMacroMode = -1;
        private int _lastPacketOwnedAntiMacroType = -1;
        private int _lastPacketOwnedAntiMacroNoticeStringPoolId = -1;
        private int _lastPacketOwnedAntiMacroChatStringPoolId = -1;
        private int _lastPacketOwnedAntiMacroSubmittedRemainingMs = -1;
        private int _packetOwnedAntiMacroCurrentRemainingMs;
        private string _lastPacketOwnedAntiMacroSubmittedAnswer = string.Empty;
        private bool _packetOwnedAntiMacroComboHeld;
        private bool _packetOwnedAntiMacroAwaitingResult;
        private bool _lastPacketOwnedAntiMacroAuthoritativeRoundTrip;
        private string _lastPacketOwnedAntiMacroResultSource = string.Empty;
        private string _lastPacketOwnedAntiMacroResultPayloadHex = string.Empty;
        private int _lastPacketOwnedAntiMacroSubmittedTick = int.MinValue;
        private int _lastPacketOwnedAntiMacroResultTick = int.MinValue;
        private int _lastPacketOwnedAntiMacroRoundTripLatencyMs = -1;
        private PacketOwnedAntiMacroSubmitTransportPath _lastPacketOwnedAntiMacroSubmitTransportPath;
        private byte[] _lastPacketOwnedAntiMacroSubmittedRawPacket = Array.Empty<byte>();
        private int _lastPacketOwnedAntiMacroSubmitBridgeSentOrdinal = -1;
        private string _lastPacketOwnedAntiMacroSubmitExpectedSource = string.Empty;
        private int _lastPacketOwnedAntiMacroSubmitBridgeReceivedOrdinal = -1;

        private sealed record PacketOwnedAntiMacroNoticeDefinition(int StringPoolId, string Text, string AvatarCanvasPath);
        private sealed record PacketOwnedAntiMacroChatDefinition(int StringPoolId, string FormatText, bool SaveScreenshot);
        internal readonly record struct PacketOwnedAntiMacroNoticeMapping(int StringPoolId, string AvatarCanvasPath);
        internal readonly record struct PacketOwnedAntiMacroChatMapping(int StringPoolId, bool SaveScreenshot);
        private enum PacketOwnedAntiMacroSubmitTransportPath
        {
            None = 0,
            SimulatorOwned,
            OfficialSessionBridge,
            PacketOutbox,
            DeferredOfficialSessionBridge,
            DeferredPacketOutbox
        }

        internal static bool IsPacketOwnedAntiMacroChallengeLaunchMode(int mode)
        {
            return mode == PacketOwnedAntiMacroChallengeMode;
        }

        internal static bool IsPacketOwnedAntiMacroCloseResultMode(int mode)
        {
            // `CWvsContext::OnAntiMacroResult` tears down challenge ownership
            // and combo hold only on 7/9 before routing notice text.
            return mode is PacketOwnedAntiMacroDestroyMode
                or PacketOwnedAntiMacroResultMode;
        }

        internal static bool IsPacketOwnedAntiMacroSubmitTerminalMode(int mode)
        {
            // Mode 11 still belongs to the terminal result family for submit
            // completion, but it is notice-only in `OnAntiMacroResult`.
            return mode is PacketOwnedAntiMacroDestroyMode
                or PacketOwnedAntiMacroResultMode
                or PacketOwnedAntiMacroNoticeMode;
        }

        internal static bool ShouldResetPacketOwnedAntiMacroRemainingQuestionOnResultMode(int mode)
        {
            // `CWvsContext::OnAntiMacroResult` zeroes m_tRemainAntiMacroQuestion only
            // on the close/result teardown family (modes 7 and 9).
            return mode is PacketOwnedAntiMacroDestroyMode
                or PacketOwnedAntiMacroResultMode;
        }

        internal static bool IsPacketOwnedAntiMacroUserBranchMode(int mode)
        {
            return mode is PacketOwnedAntiMacroScreenshotReportMode
                or PacketOwnedAntiMacroUserReportMode
                or PacketOwnedAntiMacroScreenshotMode
                or PacketOwnedAntiMacroChatReportMode;
        }

        internal static bool TryPreparePacketOwnedAntiMacroSubmittedAnswer(string answer, out string submittedAnswer)
        {
            if (string.IsNullOrWhiteSpace(answer))
            {
                submittedAnswer = string.Empty;
                return false;
            }

            // `CUIAntiMacro::SetRet` trims only to reject all-whitespace submissions,
            // then re-reads the original edit text for opcode 117.
            submittedAnswer = answer;
            return true;
        }

        internal static string ResolvePacketOwnedAntiMacroUserBranchName(string userName)
        {
            // `CWvsContext::OnAntiMacroResult` forwards the decoded ZXString directly to
            // chat formatting and screenshot naming, so preserve whitespace and empty values.
            return userName ?? string.Empty;
        }

        private void RegisterPacketOwnedAntiMacroWindows()
        {
            if (uiWindowManager == null || GraphicsDevice == null)
            {
                return;
            }

            IntPtr simulatorWindowHandle = Window?.Handle ?? IntPtr.Zero;
            WzSubProperty macroProperty = (Program.FindImage("UI", "UIWindow2.img")?["Macro"] as WzSubProperty);
            WzCanvasProperty popupCanvas = ResolvePacketOwnedAntiMacroCanvas(PacketOwnedAntiMacroPopupCanvasPath);
            Texture2D popupTexture = LoadUiCanvasTexture(popupCanvas);

            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.AntiMacro) == null)
            {
                AntiMacroChallengeWindow window = new(MapSimulatorWindowNames.AntiMacro, adminVariant: false, GraphicsDevice);
                window.Position = ResolvePacketOwnedAntiMacroWindowPosition(window);
                window.SubmitRequested += HandlePacketOwnedAntiMacroAnswerSubmitted;
                uiWindowManager.RegisterCustomWindow(window);
            }

            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.AdminAntiMacro) == null)
            {
                AntiMacroChallengeWindow window = new(MapSimulatorWindowNames.AdminAntiMacro, adminVariant: true, GraphicsDevice);
                window.Position = ResolvePacketOwnedAntiMacroWindowPosition(window);
                window.SubmitRequested += HandlePacketOwnedAntiMacroAnswerSubmitted;
                uiWindowManager.RegisterCustomWindow(window);
            }

            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.AntiMacroNotice) == null)
            {
                AntiMacroNoticeWindow window = new(
                    MapSimulatorWindowNames.AntiMacroNotice,
                    popupTexture)
                {
                    Position = ResolvePacketOwnedAntiMacroNoticeWindowPosition(popupTexture)
                };
                window.CloseRequested += HandlePacketOwnedAntiMacroNoticeClosed;
                uiWindowManager.RegisterCustomWindow(window);
            }

            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.AntiMacro) is AntiMacroChallengeWindow antiMacroWindow)
            {
                antiMacroWindow.TryAttachNativeEditHost(simulatorWindowHandle);
                ApplyPacketOwnedAntiMacroOwnerVisuals(antiMacroWindow, macroProperty);
                if (_fontChat != null)
                {
                    antiMacroWindow.SetFont(_fontChat);
                }
            }

            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.AdminAntiMacro) is AntiMacroChallengeWindow adminWindow)
            {
                adminWindow.TryAttachNativeEditHost(simulatorWindowHandle);
                ApplyPacketOwnedAntiMacroOwnerVisuals(adminWindow, macroProperty);
                if (_fontChat != null)
                {
                    adminWindow.SetFont(_fontChat);
                }
            }

            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.AntiMacroNotice) is AntiMacroNoticeWindow noticeWindow)
            {
                noticeWindow.Position = ResolvePacketOwnedAntiMacroNoticeWindowPosition(popupTexture);
                if (_fontChat != null)
                {
                    noticeWindow.SetFont(_fontChat);
                }

                ConfigurePacketOwnedAntiMacroNoticeVisuals(noticeWindow, PacketOwnedAntiMacroAdminCanvas0Path);
            }
        }

        private void RefreshPacketOwnedAntiMacroWindowPositions()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.AntiMacro) is AntiMacroChallengeWindow antiMacroWindow)
            {
                antiMacroWindow.Position = ResolvePacketOwnedAntiMacroWindowPosition(antiMacroWindow);
                antiMacroWindow.TryAttachNativeEditHost(Window?.Handle ?? IntPtr.Zero);
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.AdminAntiMacro) is AntiMacroChallengeWindow adminWindow)
            {
                adminWindow.Position = ResolvePacketOwnedAntiMacroWindowPosition(adminWindow);
                adminWindow.TryAttachNativeEditHost(Window?.Handle ?? IntPtr.Zero);
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.AntiMacroNotice) is AntiMacroNoticeWindow noticeWindow)
            {
                noticeWindow.Position = ResolvePacketOwnedAntiMacroNoticeWindowPosition(noticeWindow.FrameSize);
            }
        }

        private void ApplyPacketOwnedAntiMacroOwnerVisuals(AntiMacroChallengeWindow window, WzSubProperty macroProperty)
        {
            if (window == null || macroProperty == null)
            {
                return;
            }

            string stepPath = window.IsAdminVariant
                ? PacketOwnedAntiMacroAdminStepPath
                : PacketOwnedAntiMacroNormalStepPath;
            WzSubProperty stepProperty = macroProperty[stepPath] as WzSubProperty;
            if (stepProperty == null)
            {
                return;
            }

            Texture2D frameTexture = ComposePacketOwnedAntiMacroFrameTexture(stepProperty);
            UIObject submitButton = CreatePacketOwnedAntiMacroButton(stepProperty["btOK"] as WzSubProperty);
            (Texture2D[] digitTextures, Point[] digitOrigins, Texture2D commaTexture, Point commaOrigin) = LoadPacketOwnedAntiMacroDigits(macroProperty["num1"] as WzSubProperty);

            window.ConfigureVisualAssets(
                frameTexture,
                digitTextures,
                digitOrigins,
                commaTexture,
                commaOrigin,
                submitButton);
            window.Position = ResolvePacketOwnedAntiMacroWindowPosition(window);
        }

        private Point ResolvePacketOwnedAntiMacroWindowPosition(AntiMacroChallengeWindow window)
        {
            Point frameSize = window?.ActiveFrameSize ?? Point.Zero;
            int width = frameSize.X > 0 ? frameSize.X : 265;
            int height = frameSize.Y > 0 ? frameSize.Y : 250;
            return new Point(
                Math.Max(24, (_renderParams.RenderWidth / 2) - (width / 2)),
                Math.Max(24, (_renderParams.RenderHeight / 2) - (height / 2)));
        }

        private Point ResolvePacketOwnedAntiMacroNoticeWindowPosition(Texture2D frameTexture)
        {
            Point frameSize = frameTexture == null
                ? new Point(260, 131)
                : new Point(frameTexture.Width, frameTexture.Height);
            return ResolvePacketOwnedAntiMacroNoticeWindowPosition(frameSize);
        }

        private Point ResolvePacketOwnedAntiMacroNoticeWindowPosition(Point frameSize)
        {
            int width = frameSize.X > 0 ? frameSize.X : 260;
            int height = frameSize.Y > 0 ? frameSize.Y : 131;
            return new Point(
                Math.Max(24, (_renderParams.RenderWidth / 2) - (width / 2)),
                Math.Max(24, (_renderParams.RenderHeight / 2) - (height / 2)));
        }

        private void ConfigurePacketOwnedAntiMacroNoticeVisuals(AntiMacroNoticeWindow noticeWindow, string avatarCanvasPath)
        {
            if (noticeWindow == null)
            {
                return;
            }

            WzCanvasProperty avatarCanvas = ResolvePacketOwnedAntiMacroCanvas(avatarCanvasPath);
            noticeWindow.ConfigureVisuals(
                LoadUiCanvasTexture(avatarCanvas),
                ResolvePacketOwnedAntiMacroCanvasOrigin(avatarCanvas),
                CreatePacketOwnedAntiMacroButton(ResolvePacketOwnedAntiMacroSubProperty(PacketOwnedAntiMacroPopupOkButtonPath)),
                CreatePacketOwnedAntiMacroButton(ResolvePacketOwnedAntiMacroSubProperty(PacketOwnedAntiMacroPopupCancelButtonPath)));
        }

        private Texture2D ComposePacketOwnedAntiMacroFrameTexture(WzSubProperty stepProperty)
        {
            if (GraphicsDevice == null || stepProperty == null)
            {
                return null;
            }

            WzCanvasProperty outerCanvas = stepProperty["backgrnd"] as WzCanvasProperty;
            WzCanvasProperty middleCanvas = stepProperty["backgrnd2"] as WzCanvasProperty;
            WzCanvasProperty innerCanvas = stepProperty["backgrnd3"] as WzCanvasProperty;

            if (outerCanvas == null || middleCanvas == null || innerCanvas == null)
            {
                return null;
            }

            using SD.Bitmap outerBitmap = outerCanvas.GetLinkedWzCanvasBitmap();
            using SD.Bitmap middleBitmap = middleCanvas.GetLinkedWzCanvasBitmap();
            using SD.Bitmap innerBitmap = innerCanvas.GetLinkedWzCanvasBitmap();
            if (outerBitmap == null || middleBitmap == null || innerBitmap == null)
            {
                return null;
            }

            using SDG graphics = SDG.FromImage(outerBitmap);
            graphics.DrawImageUnscaled(middleBitmap, PacketOwnedAntiMacroBackground2Offset.X, PacketOwnedAntiMacroBackground2Offset.Y);
            graphics.DrawImageUnscaled(innerBitmap, PacketOwnedAntiMacroBackground3Offset.X, PacketOwnedAntiMacroBackground3Offset.Y);
            return outerBitmap.ToTexture2DAndDispose(GraphicsDevice);
        }

        private UIObject CreatePacketOwnedAntiMacroButton(WzSubProperty buttonProperty)
        {
            if (buttonProperty == null || GraphicsDevice == null)
            {
                return null;
            }

            try
            {
                return new UIObject(buttonProperty, null, null, flip: false, Point.Zero, GraphicsDevice);
            }
            catch
            {
                return null;
            }
        }

        private (Texture2D[] DigitTextures, Point[] DigitOrigins, Texture2D CommaTexture, Point CommaOrigin) LoadPacketOwnedAntiMacroDigits(WzSubProperty digitProperty)
        {
            Texture2D[] digitTextures = new Texture2D[10];
            Point[] digitOrigins = new Point[10];
            Texture2D commaTexture = null;
            Point commaOrigin = Point.Zero;

            if (digitProperty == null)
            {
                return (digitTextures, digitOrigins, commaTexture, commaOrigin);
            }

            for (int i = 0; i < digitTextures.Length; i++)
            {
                WzCanvasProperty canvas = digitProperty[i.ToString()] as WzCanvasProperty;
                digitTextures[i] = LoadUiCanvasTexture(canvas);
                digitOrigins[i] = ResolvePacketOwnedAntiMacroCanvasOrigin(canvas);
            }

            WzCanvasProperty commaCanvas = digitProperty["comma"] as WzCanvasProperty;
            commaTexture = LoadUiCanvasTexture(commaCanvas);
            commaOrigin = ResolvePacketOwnedAntiMacroCanvasOrigin(commaCanvas);
            return (digitTextures, digitOrigins, commaTexture, commaOrigin);
        }

        private static Point ResolvePacketOwnedAntiMacroCanvasOrigin(WzCanvasProperty canvasProperty)
        {
            WzVectorProperty origin = canvasProperty?["origin"] as WzVectorProperty;
            return origin == null
                ? Point.Zero
                : new Point(origin.X.Value, origin.Y.Value);
        }

        private void HandlePacketOwnedAntiMacroAnswerSubmitted(string answer)
        {
            if (!TryPreparePacketOwnedAntiMacroSubmittedAnswer(answer, out string submittedAnswer)
                || !TryGetActivePacketOwnedAntiMacroWindow(out AntiMacroChallengeWindow window))
            {
                return;
            }

            _lastPacketOwnedAntiMacroSubmittedAnswer = submittedAnswer;
            _lastPacketOwnedAntiMacroSubmittedRemainingMs = Math.Max(0, window.ExpiresAt - Environment.TickCount);
            _packetOwnedAntiMacroCurrentRemainingMs = _lastPacketOwnedAntiMacroSubmittedRemainingMs;
            _packetOwnedAntiMacroAwaitingResult = true;
            _lastPacketOwnedAntiMacroSubmittedTick = Environment.TickCount;
            _lastPacketOwnedAntiMacroResultTick = int.MinValue;
            _lastPacketOwnedAntiMacroRoundTripLatencyMs = -1;
            _lastPacketOwnedAntiMacroResultPayloadHex = string.Empty;
            _lastPacketOwnedAntiMacroSubmitBridgeSentOrdinal = _localUtilityOfficialSessionBridge.SentCount;
            _lastPacketOwnedAntiMacroSubmitBridgeReceivedOrdinal = _localUtilityOfficialSessionBridge.ReceivedCount;
            _lastPacketOwnedAntiMacroSubmitExpectedSource = ResolvePacketOwnedAntiMacroExpectedResultSource(
                _localUtilityOfficialSessionBridge.ActiveRemoteEndpoint);

            window.ClearChallenge();
            if (TrySendPacketOwnedAntiMacroAnswerToOfficialSession(
                submittedAnswer,
                _lastPacketOwnedAntiMacroSubmittedRemainingMs,
                out string dispatchStatus,
                out string payloadHex,
                out bool externallyObserved,
                out PacketOwnedAntiMacroSubmitTransportPath transportPath,
                out byte[] rawPacket))
            {
                _lastPacketOwnedAntiMacroSubmitTransportPath = transportPath;
                _lastPacketOwnedAntiMacroSubmittedRawPacket = rawPacket;
                _lastPacketOwnedAntiMacroSummary =
                    externallyObserved
                        ? $"Queued anti-macro answer outpacket {PacketOwnedAntiMacroAnswerSubmitOpcode} [{payloadHex}] with remaining={_lastPacketOwnedAntiMacroSubmittedRemainingMs}ms and handed it off to an external transport path. {dispatchStatus}"
                        : $"Queued anti-macro answer outpacket {PacketOwnedAntiMacroAnswerSubmitOpcode} [{payloadHex}] with remaining={_lastPacketOwnedAntiMacroSubmittedRemainingMs}ms and staged it for later external transport. {dispatchStatus}";
                return;
            }

            _lastPacketOwnedAntiMacroSubmitTransportPath = PacketOwnedAntiMacroSubmitTransportPath.SimulatorOwned;
            _lastPacketOwnedAntiMacroSubmittedRawPacket = rawPacket;
            _lastPacketOwnedAntiMacroSummary =
                $"Queued anti-macro answer outpacket {PacketOwnedAntiMacroAnswerSubmitOpcode} [{payloadHex}] with remaining={_lastPacketOwnedAntiMacroSubmittedRemainingMs}ms; no local-utility transport path accepted it, so the request remains simulator-owned while awaiting packet-owned result resolution. {dispatchStatus}";
        }

        private string DescribePacketOwnedAntiMacroStatus(int currentTickCount)
        {
            _packetOwnedAntiMacroCurrentRemainingMs = ResolvePacketOwnedAntiMacroCurrentRemainingMs(currentTickCount);

            string ownerState;
            if (TryGetActivePacketOwnedAntiMacroWindow(out AntiMacroChallengeWindow window))
            {
                int remainingMs = Math.Max(0, window.ExpiresAt - currentTickCount);
                ownerState = $"{window.WindowName} active, remaining={remainingMs}ms, input=\"{window.CurrentInput}\"";
            }
            else
            {
                string transportState = DescribePacketOwnedAntiMacroAwaitingTransportState();
                string sourceExpectation = string.IsNullOrWhiteSpace(_lastPacketOwnedAntiMacroSubmitExpectedSource)
                    ? "expectedSource=any-official-session"
                    : $"expectedSource={_lastPacketOwnedAntiMacroSubmitExpectedSource}";
                ownerState = _packetOwnedAntiMacroAwaitingResult
                    ? $"owner pending result, submitted=\"{_lastPacketOwnedAntiMacroSubmittedAnswer}\", remainingAtSubmit={_lastPacketOwnedAntiMacroSubmittedRemainingMs}ms, transport={transportState}, {sourceExpectation}"
                    : "owner inactive";
            }

            string screenshotState = string.IsNullOrWhiteSpace(_lastPacketOwnedAntiMacroScreenshotPath)
                ? "no packet-authored screenshot saved"
                : $"last screenshot={_lastPacketOwnedAntiMacroScreenshotPath}";
            string screenshotBaseFolderState = string.IsNullOrWhiteSpace(_lastPacketOwnedAntiMacroScreenshotBaseFolder)
                ? "baseFolder=unresolved"
                : $"baseFolder={_lastPacketOwnedAntiMacroScreenshotBaseFolder}";
            string noticeState = string.IsNullOrWhiteSpace(_lastPacketOwnedAntiMacroNotice)
                ? "no notice"
                : IsPacketOwnedAntiMacroNoticeVisible()
                    ? $"notice active=\"{_lastPacketOwnedAntiMacroNotice}\""
                    : $"lastNotice=\"{_lastPacketOwnedAntiMacroNotice}\" (closed)";
            string resultSourceState = string.IsNullOrWhiteSpace(_lastPacketOwnedAntiMacroResultSource)
                ? "resultSource=none"
                : _lastPacketOwnedAntiMacroAuthoritativeRoundTrip
                    ? $"resultSource={_lastPacketOwnedAntiMacroResultSource} (authoritative round-trip, latency={_lastPacketOwnedAntiMacroRoundTripLatencyMs}ms, payload={_lastPacketOwnedAntiMacroResultPayloadHex})"
                    : $"resultSource={_lastPacketOwnedAntiMacroResultSource}";
            return $"Anti-macro mode={_lastPacketOwnedAntiMacroMode}, type={_lastPacketOwnedAntiMacroType}, comboHeld={_packetOwnedAntiMacroComboHeld}, {ownerState}, {noticeState}, {screenshotState}, {screenshotBaseFolderState}, {resultSourceState}. {_lastPacketOwnedAntiMacroSummary}";
        }

        private string ClearPacketOwnedAntiMacro(bool releaseCombo, bool preserveAuthoritativeSubmitAwaitingState = false)
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.AntiMacro) is AntiMacroChallengeWindow normalWindow)
            {
                normalWindow.ClearChallenge();
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.AdminAntiMacro) is AntiMacroChallengeWindow adminWindow)
            {
                adminWindow.ClearChallenge();
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.AntiMacroNotice) is AntiMacroNoticeWindow noticeWindow)
            {
                noticeWindow.Hide();
            }

            if (releaseCombo)
            {
                SetPacketOwnedAntiMacroComboHold(false);
            }

            if (!preserveAuthoritativeSubmitAwaitingState)
            {
                _packetOwnedAntiMacroAwaitingResult = false;
                _lastPacketOwnedAntiMacroSubmittedAnswer = string.Empty;
                _lastPacketOwnedAntiMacroSubmittedRemainingMs = -1;
                _packetOwnedAntiMacroCurrentRemainingMs = 0;
                _lastPacketOwnedAntiMacroSubmitTransportPath = PacketOwnedAntiMacroSubmitTransportPath.None;
                _lastPacketOwnedAntiMacroSubmittedRawPacket = Array.Empty<byte>();
                _lastPacketOwnedAntiMacroSubmitBridgeSentOrdinal = -1;
                _lastPacketOwnedAntiMacroSubmitBridgeReceivedOrdinal = -1;
                _lastPacketOwnedAntiMacroSubmitExpectedSource = string.Empty;
            }

            _lastPacketOwnedAntiMacroAuthoritativeRoundTrip = false;
            _lastPacketOwnedAntiMacroSubmittedTick = int.MinValue;
            _lastPacketOwnedAntiMacroResultTick = int.MinValue;
            _lastPacketOwnedAntiMacroRoundTripLatencyMs = -1;
            _lastPacketOwnedAntiMacroResultPayloadHex = string.Empty;
            _lastPacketOwnedAntiMacroSummary = preserveAuthoritativeSubmitAwaitingState
                ? "Closed packet-owned anti-macro owner while keeping pending official-session submit tracking."
                : "Closed packet-owned anti-macro owner.";
            return _lastPacketOwnedAntiMacroSummary;
        }

        private int ResolvePacketOwnedAntiMacroCurrentRemainingMs(int currentTickCount)
        {
            if (TryGetActivePacketOwnedAntiMacroWindow(out AntiMacroChallengeWindow window))
            {
                return Math.Max(0, window.ExpiresAt - currentTickCount);
            }

            if (_packetOwnedAntiMacroAwaitingResult)
            {
                return ResolvePacketOwnedAntiMacroAwaitingRemainingMs(
                    _lastPacketOwnedAntiMacroSubmittedRemainingMs,
                    _lastPacketOwnedAntiMacroSubmittedTick,
                    currentTickCount);
            }

            return Math.Max(0, _packetOwnedAntiMacroCurrentRemainingMs);
        }

        private void SetPacketOwnedAntiMacroComboHold(bool held)
        {
            _packetOwnedAntiMacroComboHeld = held;
            _playerManager?.Input?.SetCtrlComboSuppressed(held);
        }

        private string ApplyPacketOwnedAntiMacroChallenge(int antiMacroType, int firstAttemptFlag, byte[] jpegBytes)
        {
            RegisterPacketOwnedAntiMacroWindows();

            bool adminVariant = antiMacroType == 2;
            string windowName = adminVariant ? MapSimulatorWindowNames.AdminAntiMacro : MapSimulatorWindowNames.AntiMacro;
            if (uiWindowManager?.GetWindow(windowName) is not AntiMacroChallengeWindow challengeWindow)
            {
                _lastPacketOwnedAntiMacroSummary = $"Anti-macro launch for type {antiMacroType} could not resolve a simulator owner.";
                return _lastPacketOwnedAntiMacroSummary;
            }

            uiWindowManager.HideWindow(adminVariant ? MapSimulatorWindowNames.AntiMacro : MapSimulatorWindowNames.AdminAntiMacro);

            Texture2D challengeTexture = TryDecodePacketOwnedAntiMacroCanvas(jpegBytes);
            int remainingMs = ResolvePacketOwnedAntiMacroLaunchRemainingMs(_packetOwnedAntiMacroCurrentRemainingMs, firstAttemptFlag);
            int expiresAt = Environment.TickCount + remainingMs;
            bool firstAttempt = firstAttemptFlag != 0;
            string statusText = firstAttempt
                ? "Packet-authored challenge; first attempt. Ctrl combo input is held."
                : "Packet-authored challenge; retry attempt. Ctrl combo input is held.";
            challengeWindow.Configure(challengeTexture, expiresAt, firstAttemptFlag, statusText);
            ShowWindow(
                windowName,
                challengeWindow,
                trackDirectionModeOwner: true);

            SetPacketOwnedAntiMacroComboHold(true);
            _packetOwnedAntiMacroCurrentRemainingMs = remainingMs;
            _packetOwnedAntiMacroAwaitingResult = false;
            _lastPacketOwnedAntiMacroSubmittedAnswer = string.Empty;
            _lastPacketOwnedAntiMacroSubmittedRemainingMs = -1;
            _lastPacketOwnedAntiMacroSubmitTransportPath = PacketOwnedAntiMacroSubmitTransportPath.None;
            _lastPacketOwnedAntiMacroSubmittedRawPacket = Array.Empty<byte>();
            _lastPacketOwnedAntiMacroSubmitBridgeSentOrdinal = -1;
            _lastPacketOwnedAntiMacroSubmitBridgeReceivedOrdinal = -1;
            _lastPacketOwnedAntiMacroSubmitExpectedSource = string.Empty;
            _lastPacketOwnedAntiMacroAuthoritativeRoundTrip = false;
            _lastPacketOwnedAntiMacroSubmittedTick = int.MinValue;
            _lastPacketOwnedAntiMacroResultTick = int.MinValue;
            _lastPacketOwnedAntiMacroRoundTripLatencyMs = -1;
            _lastPacketOwnedAntiMacroResultPayloadHex = string.Empty;
            _lastPacketOwnedAntiMacroSummary =
                $"Opened packet-owned {(adminVariant ? "admin " : string.Empty)}anti-macro challenge with remaining={remainingMs}ms and held Ctrl combo input.";
            return _lastPacketOwnedAntiMacroSummary;
        }

        private Texture2D TryDecodePacketOwnedAntiMacroCanvas(byte[] jpegBytes)
        {
            if (GraphicsDevice == null || jpegBytes == null || jpegBytes.Length == 0)
            {
                return null;
            }

            try
            {
                using MemoryStream stream = new(jpegBytes, writable: false);
                using SD.Bitmap bitmap = new(stream);
                return bitmap.ToTexture2D(GraphicsDevice);
            }
            catch
            {
                return null;
            }
        }

        private string ApplyPacketOwnedAntiMacroNotice(int noticeType, int antiMacroType)
        {
            RegisterPacketOwnedAntiMacroWindows();

            PacketOwnedAntiMacroNoticeDefinition definition = ResolvePacketOwnedAntiMacroNoticeDefinition(noticeType, antiMacroType);
            _lastPacketOwnedAntiMacroNotice = definition.Text;
            _lastPacketOwnedAntiMacroNoticeStringPoolId = definition.StringPoolId;

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.AntiMacroNotice) is AntiMacroNoticeWindow noticeWindow)
            {
                noticeWindow.Configure(
                    definition.Text,
                    definition.StringPoolId);
                ConfigurePacketOwnedAntiMacroNoticeVisuals(noticeWindow, definition.AvatarCanvasPath);
                noticeWindow.Position = ResolvePacketOwnedAntiMacroNoticeWindowPosition(noticeWindow.FrameSize);
                ShowWindow(
                    MapSimulatorWindowNames.AntiMacroNotice,
                    noticeWindow,
                    trackDirectionModeOwner: true);
            }

            _lastPacketOwnedAntiMacroSummary = $"Opened anti-macro notice owner for type {noticeType} / mode {antiMacroType}.";
            return _lastPacketOwnedAntiMacroSummary;
        }

        private string ApplyPacketOwnedAntiMacroCloseResult(int mode, int antiMacroType, bool preserveAuthoritativeSubmitAwaitingState = false)
        {
            string clearSummary = ClearPacketOwnedAntiMacro(
                releaseCombo: true,
                preserveAuthoritativeSubmitAwaitingState: preserveAuthoritativeSubmitAwaitingState);
            string noticeSummary = ApplyPacketOwnedAntiMacroNotice(mode, antiMacroType);
            _lastPacketOwnedAntiMacroSummary = $"{clearSummary} {noticeSummary}";
            return _lastPacketOwnedAntiMacroSummary;
        }

        internal static int ResolvePacketOwnedAntiMacroLaunchRemainingMs(int currentRemainingMs, int firstAttemptFlag)
        {
            // `CWvsContext::OnAntiMacroResult` only reseeds the client timer to 60000ms
            // when the decoded question counter is non-zero; otherwise it reuses the
            // current context-owned anti-macro remaining time.
            return firstAttemptFlag != 0
                ? PacketOwnedAntiMacroDefaultDurationMs
                : Math.Max(0, currentRemainingMs);
        }

        internal static bool ShouldPreferPacketOwnedAntiMacroDeferredBridge(
            bool officialSessionBridgeEnabled,
            bool hasAttachedClient,
            bool hasConnectedSession)
        {
            // `CUIAntiMacro::SetRet` and `CUIAdminAntiMacro::SetRet` always target the
            // live Maple socket. When the bridge already has an attached client but is
            // still waiting for init/crypto completion, keep opcode 117 on that bridge
            // instead of leaking it onto the generic local-utility outbox first.
            return officialSessionBridgeEnabled
                && hasAttachedClient
                && !hasConnectedSession;
        }

        internal static bool IsPacketOwnedAntiMacroAuthoritativeResultSource(string source)
        {
            return !string.IsNullOrWhiteSpace(source)
                && source.StartsWith("official-session:", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsPacketOwnedAntiMacroResultSourceMatch(string source, string expectedSource)
        {
            if (!IsPacketOwnedAntiMacroAuthoritativeResultSource(source))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(expectedSource))
            {
                return true;
            }

            return string.Equals(
                ResolvePacketOwnedAntiMacroResultSource(source),
                ResolvePacketOwnedAntiMacroResultSource(expectedSource),
                StringComparison.OrdinalIgnoreCase);
        }

        internal static bool ShouldCompletePacketOwnedAntiMacroAuthoritativeRoundTrip(
            int mode,
            bool wasAwaitingResult,
            string source,
            bool hasAuthoritativeSubmitTransport)
        {
            return wasAwaitingResult
                && hasAuthoritativeSubmitTransport
                && IsPacketOwnedAntiMacroAuthoritativeResultSource(source)
                && IsPacketOwnedAntiMacroSubmitTerminalMode(mode);
        }

        internal static bool ShouldKeepPacketOwnedAntiMacroAwaitingAuthoritativeResult(
            bool wasAwaitingResult,
            bool authoritativeRoundTrip,
            bool hasPendingAuthoritativeSubmitTransport)
        {
            return wasAwaitingResult
                && !authoritativeRoundTrip
                && hasPendingAuthoritativeSubmitTransport;
        }

        internal static bool HasPendingAuthoritativeSubmitTransportState(
            bool usesOfficialSessionBridgeTransport,
            bool hasSubmittedRawPacket,
            bool bridgeHasQueuedPacket,
            bool bridgeHasSentPacket)
        {
            return usesOfficialSessionBridgeTransport
                && hasSubmittedRawPacket
                && (bridgeHasQueuedPacket || bridgeHasSentPacket);
        }

        private bool HasPacketOwnedAntiMacroAuthoritativeSubmitTransport(string resultSource)
        {
            if (_lastPacketOwnedAntiMacroSubmitTransportPath != PacketOwnedAntiMacroSubmitTransportPath.OfficialSessionBridge
                && _lastPacketOwnedAntiMacroSubmitTransportPath != PacketOwnedAntiMacroSubmitTransportPath.DeferredOfficialSessionBridge)
            {
                return false;
            }

            if (!IsPacketOwnedAntiMacroResultSourceMatch(resultSource, _lastPacketOwnedAntiMacroSubmitExpectedSource)
                || _lastPacketOwnedAntiMacroSubmittedRawPacket == null
                || _lastPacketOwnedAntiMacroSubmittedRawPacket.Length == 0)
            {
                return false;
            }

            return _localUtilityOfficialSessionBridge.HasSentOutboundPacketSince(
                PacketOwnedAntiMacroAnswerSubmitOpcode,
                _lastPacketOwnedAntiMacroSubmittedRawPacket,
                Math.Max(0, _lastPacketOwnedAntiMacroSubmitBridgeSentOrdinal));
        }

        private bool HasPacketOwnedAntiMacroPendingAuthoritativeSubmitTransport()
        {
            bool usesOfficialSessionBridgeTransport =
                _lastPacketOwnedAntiMacroSubmitTransportPath == PacketOwnedAntiMacroSubmitTransportPath.OfficialSessionBridge
                || _lastPacketOwnedAntiMacroSubmitTransportPath == PacketOwnedAntiMacroSubmitTransportPath.DeferredOfficialSessionBridge;
            bool hasSubmittedRawPacket =
                _lastPacketOwnedAntiMacroSubmittedRawPacket != null
                && _lastPacketOwnedAntiMacroSubmittedRawPacket.Length > 0;
            bool bridgeHasQueuedPacket = hasSubmittedRawPacket
                && _localUtilityOfficialSessionBridge.HasQueuedOutboundPacket(
                    PacketOwnedAntiMacroAnswerSubmitOpcode,
                    _lastPacketOwnedAntiMacroSubmittedRawPacket);
            bool bridgeHasSentPacket = hasSubmittedRawPacket
                && _localUtilityOfficialSessionBridge.HasSentOutboundPacket(
                    PacketOwnedAntiMacroAnswerSubmitOpcode,
                    _lastPacketOwnedAntiMacroSubmittedRawPacket);

            // Keep authoritative submit tracking only while opcode 117 is either
            // still queued on the official-session bridge or has actually been
            // injected into the bridged Maple socket.
            return HasPendingAuthoritativeSubmitTransportState(
                usesOfficialSessionBridgeTransport,
                hasSubmittedRawPacket,
                bridgeHasQueuedPacket,
                bridgeHasSentPacket);
        }

        private bool HasPacketOwnedAntiMacroAuthoritativeResultEvidence(string resultSource, IReadOnlyList<byte> payload)
        {
            if (!HasPacketOwnedAntiMacroAuthoritativeSubmitTransport(resultSource)
                || payload == null
                || payload.Count == 0)
            {
                return false;
            }

            return _localUtilityOfficialSessionBridge.HasReceivedInboundPacketPayloadSince(
                PacketOwnedAntiMacroPacketType,
                payload,
                Math.Max(0, _lastPacketOwnedAntiMacroSubmitBridgeReceivedOrdinal));
        }

        internal static int ResolvePacketOwnedAntiMacroRoundTripLatencyMs(int submittedTick, int resultTick)
        {
            if (submittedTick == int.MinValue || resultTick == int.MinValue)
            {
                return -1;
            }

            unchecked
            {
                return Math.Max(0, resultTick - submittedTick);
            }
        }

        internal static int ResolvePacketOwnedAntiMacroAwaitingRemainingMs(
            int remainingAtSubmitMs,
            int submittedTick,
            int currentTick)
        {
            if (remainingAtSubmitMs <= 0)
            {
                return 0;
            }

            int elapsedMs = ResolvePacketOwnedAntiMacroRoundTripLatencyMs(submittedTick, currentTick);
            if (elapsedMs < 0)
            {
                return Math.Max(0, remainingAtSubmitMs);
            }

            return Math.Max(0, remainingAtSubmitMs - elapsedMs);
        }

        internal static PacketOwnedAntiMacroNoticeMapping ResolvePacketOwnedAntiMacroNoticeMappingForTest(int noticeType, int antiMacroType)
        {
            PacketOwnedAntiMacroNoticeDefinition definition = ResolvePacketOwnedAntiMacroNoticeDefinition(noticeType, antiMacroType);
            return new PacketOwnedAntiMacroNoticeMapping(definition.StringPoolId, definition.AvatarCanvasPath);
        }

        internal static bool TryResolvePacketOwnedAntiMacroChatMappingForTest(
            int mode,
            int antiMacroType,
            out PacketOwnedAntiMacroChatMapping mapping)
        {
            PacketOwnedAntiMacroChatDefinition definition = ResolvePacketOwnedAntiMacroChatDefinition(mode, antiMacroType);
            if (definition == null)
            {
                mapping = default;
                return false;
            }

            mapping = new PacketOwnedAntiMacroChatMapping(definition.StringPoolId, definition.SaveScreenshot);
            return true;
        }

        private static PacketOwnedAntiMacroNoticeDefinition ResolvePacketOwnedAntiMacroNoticeDefinition(int noticeType, int antiMacroType)
        {
            return (noticeType, antiMacroType) switch
            {
                (0, _) => new PacketOwnedAntiMacroNoticeDefinition(
                    AntiMacroOwnerStringPoolText.NoticeUserNotFoundStringPoolId,
                    AntiMacroOwnerStringPoolText.GetResolvedOrFallback(AntiMacroOwnerStringPoolText.NoticeUserNotFoundStringPoolId),
                    PacketOwnedAntiMacroAdminCanvas0Path),
                (1, _) => new PacketOwnedAntiMacroNoticeDefinition(
                    AntiMacroOwnerStringPoolText.NoticeTargetNotAttackingStringPoolId,
                    AntiMacroOwnerStringPoolText.GetResolvedOrFallback(AntiMacroOwnerStringPoolText.NoticeTargetNotAttackingStringPoolId),
                    PacketOwnedAntiMacroAdminCanvas0Path),
                (2, _) => new PacketOwnedAntiMacroNoticeDefinition(
                    AntiMacroOwnerStringPoolText.NoticeAlreadyTestedStringPoolId,
                    AntiMacroOwnerStringPoolText.GetResolvedOrFallback(AntiMacroOwnerStringPoolText.NoticeAlreadyTestedStringPoolId),
                    PacketOwnedAntiMacroAdminCanvas0Path),
                (3, _) => new PacketOwnedAntiMacroNoticeDefinition(
                    AntiMacroOwnerStringPoolText.NoticeTargetAlreadyTestingStringPoolId,
                    AntiMacroOwnerStringPoolText.GetResolvedOrFallback(AntiMacroOwnerStringPoolText.NoticeTargetAlreadyTestingStringPoolId),
                    PacketOwnedAntiMacroAdminCanvas0Path),
                (7, 2) => new PacketOwnedAntiMacroNoticeDefinition(
                    AntiMacroOwnerStringPoolText.NoticeMacroSanctionStringPoolId,
                    AntiMacroOwnerStringPoolText.GetResolvedOrFallback(AntiMacroOwnerStringPoolText.NoticeMacroSanctionStringPoolId),
                    PacketOwnedAntiMacroAdminCanvas2Path),
                (7, _) => new PacketOwnedAntiMacroNoticeDefinition(
                    AntiMacroOwnerStringPoolText.NoticeFailureRestrictionStringPoolId,
                    AntiMacroOwnerStringPoolText.GetResolvedOrFallback(AntiMacroOwnerStringPoolText.NoticeFailureRestrictionStringPoolId),
                    PacketOwnedAntiMacroAdminCanvas2Path),
                (9, 1) => new PacketOwnedAntiMacroNoticeDefinition(
                    AntiMacroOwnerStringPoolText.NoticeRewardThanksStringPoolId,
                    AntiMacroOwnerStringPoolText.GetResolvedOrFallback(AntiMacroOwnerStringPoolText.NoticeRewardThanksStringPoolId),
                    PacketOwnedAntiMacroAdminCanvas1Path),
                (9, 2) => new PacketOwnedAntiMacroNoticeDefinition(
                    AntiMacroOwnerStringPoolText.NoticeAdminThanksStringPoolId,
                    AntiMacroOwnerStringPoolText.GetResolvedOrFallback(AntiMacroOwnerStringPoolText.NoticeAdminThanksStringPoolId),
                    PacketOwnedAntiMacroAdminCanvas1Path),
                (9, 3) => new PacketOwnedAntiMacroNoticeDefinition(
                    AntiMacroOwnerStringPoolText.NoticePassedThanksStringPoolId,
                    AntiMacroOwnerStringPoolText.GetResolvedOrFallback(AntiMacroOwnerStringPoolText.NoticePassedThanksStringPoolId),
                    PacketOwnedAntiMacroAdminCanvas1Path),
                (9, 4) => new PacketOwnedAntiMacroNoticeDefinition(
                    AntiMacroOwnerStringPoolText.NoticePassedThanksStringPoolId,
                    AntiMacroOwnerStringPoolText.GetResolvedOrFallback(AntiMacroOwnerStringPoolText.NoticePassedThanksStringPoolId),
                    PacketOwnedAntiMacroAdminCanvas1Path),
                (9, _) => new PacketOwnedAntiMacroNoticeDefinition(
                    AntiMacroOwnerStringPoolText.NoticePassedThanksStringPoolId,
                    AntiMacroOwnerStringPoolText.GetResolvedOrFallback(AntiMacroOwnerStringPoolText.NoticePassedThanksStringPoolId),
                    PacketOwnedAntiMacroAdminCanvas1Path),
                (11, _) => new PacketOwnedAntiMacroNoticeDefinition(
                    AntiMacroOwnerStringPoolText.NoticeUserFailedRewardStringPoolId,
                    AntiMacroOwnerStringPoolText.GetResolvedOrFallback(AntiMacroOwnerStringPoolText.NoticeUserFailedRewardStringPoolId),
                    PacketOwnedAntiMacroAdminCanvas1Path),
                _ => new PacketOwnedAntiMacroNoticeDefinition(
                    -1,
                    string.Format(CultureInfo.InvariantCulture, "Anti-macro notice type {0} (mode {1}) reached the simulator bridge.", noticeType, antiMacroType),
                    PacketOwnedAntiMacroAdminCanvas1Path)
            };
        }

        private string SavePacketOwnedAntiMacroScreenshot(string userName)
        {
            _lastPacketOwnedAntiMacroScreenshotPath = string.Empty;
            string screenshotDirectory = PacketOwnedAntiMacroScreenshotPathResolver.ResolveBaseFolder();
            _lastPacketOwnedAntiMacroScreenshotBaseFolder = screenshotDirectory;
            if (string.IsNullOrWhiteSpace(screenshotDirectory))
            {
                _lastPacketOwnedAntiMacroSummary = "Failed to resolve the packet-owned anti-macro screenshot base folder.";
                return _lastPacketOwnedAntiMacroSummary;
            }

            Directory.CreateDirectory(screenshotDirectory);
            string rawUserName = userName ?? string.Empty;
            string filePath = PacketOwnedAntiMacroScreenshotPathResolver.BuildFilePath(
                screenshotDirectory,
                rawUserName,
                DateTime.Now);

            if (!_screenshotManager.TrySaveBackBufferAsJpeg(GraphicsDevice, filePath, out string error))
            {
                string fallbackFilePath = PacketOwnedAntiMacroScreenshotPathResolver.BuildFallbackSafeFilePath(
                    screenshotDirectory,
                    rawUserName,
                    DateTime.Now);
                string fallbackError = string.Empty;
                bool canTryFallback =
                    !string.Equals(
                        filePath,
                        fallbackFilePath,
                        StringComparison.OrdinalIgnoreCase);
                if (!canTryFallback
                    || !_screenshotManager.TrySaveBackBufferAsJpeg(GraphicsDevice, fallbackFilePath, out fallbackError))
                {
                    string resolvedError = canTryFallback
                        ? $"{error}; fallback save also failed: {fallbackError}"
                        : error;
                    _lastPacketOwnedAntiMacroSummary = $"Failed to save packet-owned anti-macro screenshot for {rawUserName}: {resolvedError}";
                    return _lastPacketOwnedAntiMacroSummary;
                }

                _lastPacketOwnedAntiMacroScreenshotPath = fallbackFilePath;
                _lastPacketOwnedAntiMacroSummary =
                    $"Saved packet-owned anti-macro screenshot to {fallbackFilePath} after client-shaped raw-name save failed ({error}).";
                return _lastPacketOwnedAntiMacroSummary;
            }

            _lastPacketOwnedAntiMacroScreenshotPath = filePath;
            _lastPacketOwnedAntiMacroSummary = $"Saved packet-owned anti-macro screenshot to {filePath}.";
            return _lastPacketOwnedAntiMacroSummary;
        }

        private string ApplyPacketOwnedAntiMacroUserBranch(int mode, int antiMacroType, string userName)
        {
            string resolvedName = ResolvePacketOwnedAntiMacroUserBranchName(userName);
            PacketOwnedAntiMacroChatDefinition definition = ResolvePacketOwnedAntiMacroChatDefinition(mode, antiMacroType);
            _lastPacketOwnedAntiMacroChatStringPoolId = definition?.StringPoolId ?? -1;
            string chatText = AntiMacroOwnerStringPoolText.FormatUserBranchText(
                definition?.StringPoolId ?? -1,
                definition?.FormatText,
                resolvedName);
            if (!string.IsNullOrWhiteSpace(chatText))
            {
                _chat?.AddClientChatMessage(
                    chatText,
                    Environment.TickCount,
                    12);
            }

            if (definition?.SaveScreenshot == true)
            {
                string screenshotSummary = SavePacketOwnedAntiMacroScreenshot(resolvedName);
                _lastPacketOwnedAntiMacroSummary =
                    $"Anti-macro branch mode {mode} for {resolvedName} applied through the simulator packet bridge. {screenshotSummary}";
                return _lastPacketOwnedAntiMacroSummary;
            }

            _lastPacketOwnedAntiMacroSummary = $"Anti-macro branch mode {mode} for {resolvedName} applied through the simulator packet bridge.";
            return _lastPacketOwnedAntiMacroSummary;
        }

        private static PacketOwnedAntiMacroChatDefinition ResolvePacketOwnedAntiMacroChatDefinition(int mode, int antiMacroType)
        {
            return (mode, antiMacroType) switch
            {
                (PacketOwnedAntiMacroScreenshotReportMode, _) => new PacketOwnedAntiMacroChatDefinition(
                    AntiMacroOwnerStringPoolText.ChatScreenshotReportStringPoolId,
                    AntiMacroOwnerStringPoolText.GetResolvedOrFallback(AntiMacroOwnerStringPoolText.ChatScreenshotReportStringPoolId),
                    SaveScreenshot: true),
                (PacketOwnedAntiMacroUserReportMode, 1) => new PacketOwnedAntiMacroChatDefinition(
                    AntiMacroOwnerStringPoolText.ChatAdminLaunchStringPoolId,
                    AntiMacroOwnerStringPoolText.GetResolvedOrFallback(AntiMacroOwnerStringPoolText.ChatAdminLaunchStringPoolId),
                    SaveScreenshot: false),
                (PacketOwnedAntiMacroUserReportMode, 2) => new PacketOwnedAntiMacroChatDefinition(
                    AntiMacroOwnerStringPoolText.ChatAdminActivateStringPoolId,
                    AntiMacroOwnerStringPoolText.GetResolvedOrFallback(AntiMacroOwnerStringPoolText.ChatAdminActivateStringPoolId),
                    SaveScreenshot: true),
                (PacketOwnedAntiMacroScreenshotMode, 2) => new PacketOwnedAntiMacroChatDefinition(
                    AntiMacroOwnerStringPoolText.ChatAdminScreenshotSavedStringPoolId,
                    AntiMacroOwnerStringPoolText.GetResolvedOrFallback(AntiMacroOwnerStringPoolText.ChatAdminScreenshotSavedStringPoolId),
                    SaveScreenshot: true),
                (PacketOwnedAntiMacroChatReportMode, 2) => new PacketOwnedAntiMacroChatDefinition(
                    AntiMacroOwnerStringPoolText.ChatAdminPassedStringPoolId,
                    AntiMacroOwnerStringPoolText.GetResolvedOrFallback(AntiMacroOwnerStringPoolText.ChatAdminPassedStringPoolId),
                    SaveScreenshot: false),
                _ => null
            };
        }

        private bool TrySendPacketOwnedAntiMacroAnswerToOfficialSession(
            string submittedAnswer,
            int remainingMs,
            out string status,
            out string payloadHex,
            out bool externallyObserved,
            out PacketOwnedAntiMacroSubmitTransportPath transportPath,
            out byte[] rawPacket)
        {
            byte[] payload = BuildPacketOwnedAntiMacroAnswerPayload(submittedAnswer);
            payloadHex = BitConverter.ToString(payload).Replace("-", string.Empty);
            externallyObserved = false;
            transportPath = PacketOwnedAntiMacroSubmitTransportPath.None;
            rawPacket = BuildPacketOwnedAntiMacroAnswerRawPacket(payload);

            string bridgeStatus;
            if (_localUtilityOfficialSessionBridge.TrySendOutboundPacket(
                PacketOwnedAntiMacroAnswerSubmitOpcode,
                payload,
                out bridgeStatus))
            {
                status = $"Mirrored the client anti-macro submit path with CWvsContext remaining={Math.Max(0, remainingMs)}ms. {bridgeStatus}";
                externallyObserved = true;
                transportPath = PacketOwnedAntiMacroSubmitTransportPath.OfficialSessionBridge;
                return true;
            }

            bool preferDeferredBridge = ShouldPreferPacketOwnedAntiMacroDeferredBridge(
                _localUtilityOfficialSessionBridgeEnabled,
                _localUtilityOfficialSessionBridge.HasAttachedClient,
                _localUtilityOfficialSessionBridge.HasConnectedSession);

            string deferredBridgeStatus = "Official-session bridge deferred delivery is disabled.";
            if (preferDeferredBridge
                && _localUtilityOfficialSessionBridge.TryQueueOutboundPacket(
                    PacketOwnedAntiMacroAnswerSubmitOpcode,
                    payload,
                    out deferredBridgeStatus))
            {
                status =
                    $"Mirrored the client anti-macro submit path with CWvsContext remaining={Math.Max(0, remainingMs)}ms and queued opcode {PacketOwnedAntiMacroAnswerSubmitOpcode} on the deferred official-session bridge while the attached Maple client is still waiting for init/crypto completion. Bridge: {bridgeStatus} Deferred bridge: {deferredBridgeStatus}";
                transportPath = PacketOwnedAntiMacroSubmitTransportPath.DeferredOfficialSessionBridge;
                return true;
            }

            string outboxStatus;
            if (_localUtilityPacketOutbox.TrySendOutboundPacket(
                PacketOwnedAntiMacroAnswerSubmitOpcode,
                payload,
                out outboxStatus))
            {
                status =
                    $"Mirrored the client anti-macro submit path with CWvsContext remaining={Math.Max(0, remainingMs)}ms and dispatched opcode {PacketOwnedAntiMacroAnswerSubmitOpcode} through the generic local-utility outbox after the live bridge path was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus}";
                externallyObserved = true;
                transportPath = PacketOwnedAntiMacroSubmitTransportPath.PacketOutbox;
                return true;
            }

            if (_localUtilityOfficialSessionBridgeEnabled
                && _localUtilityOfficialSessionBridge.TryQueueOutboundPacket(
                    PacketOwnedAntiMacroAnswerSubmitOpcode,
                    payload,
                    out deferredBridgeStatus))
            {
                status =
                    $"Mirrored the client anti-macro submit path with CWvsContext remaining={Math.Max(0, remainingMs)}ms and queued opcode {PacketOwnedAntiMacroAnswerSubmitOpcode} for deferred official-session injection after the live bridge path was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus} Deferred bridge: {deferredBridgeStatus}";
                transportPath = PacketOwnedAntiMacroSubmitTransportPath.DeferredOfficialSessionBridge;
                return true;
            }

            string queuedOutboxStatus;
            if (_localUtilityPacketOutbox.TryQueueOutboundPacket(
                    PacketOwnedAntiMacroAnswerSubmitOpcode,
                    payload,
                    out queuedOutboxStatus))
            {
                status =
                    $"Mirrored the client anti-macro submit path with CWvsContext remaining={Math.Max(0, remainingMs)}ms and queued opcode {PacketOwnedAntiMacroAnswerSubmitOpcode} for deferred generic local-utility outbox delivery after the live bridge path was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus} Deferred bridge: {deferredBridgeStatus} Deferred outbox: {queuedOutboxStatus}";
                transportPath = PacketOwnedAntiMacroSubmitTransportPath.DeferredPacketOutbox;
                return true;
            }

            status =
                $"Neither the live local-utility bridge nor the deferred official-session bridge queue nor the generic outbox transport or deferred outbox queue accepted opcode {PacketOwnedAntiMacroAnswerSubmitOpcode}. Bridge: {bridgeStatus} Outbox: {outboxStatus} Deferred official bridge: {deferredBridgeStatus} Deferred outbox: {queuedOutboxStatus}";
            transportPath = PacketOwnedAntiMacroSubmitTransportPath.SimulatorOwned;
            return false;
        }

        private string DescribePacketOwnedAntiMacroAwaitingTransportState()
        {
            if (!_packetOwnedAntiMacroAwaitingResult)
            {
                return "inactive";
            }

            if (_lastPacketOwnedAntiMacroSubmitTransportPath == PacketOwnedAntiMacroSubmitTransportPath.None)
            {
                return "unknown";
            }

            if (_lastPacketOwnedAntiMacroSubmittedRawPacket == null
                || _lastPacketOwnedAntiMacroSubmittedRawPacket.Length == 0)
            {
                return DescribePacketOwnedAntiMacroSubmitTransportPath(_lastPacketOwnedAntiMacroSubmitTransportPath);
            }

            return _lastPacketOwnedAntiMacroSubmitTransportPath switch
            {
                PacketOwnedAntiMacroSubmitTransportPath.OfficialSessionBridge
                    => _localUtilityOfficialSessionBridge.WasLastSentOutboundPacket(
                        PacketOwnedAntiMacroAnswerSubmitOpcode,
                        _lastPacketOwnedAntiMacroSubmittedRawPacket)
                        ? "injected into the live Maple session"
                        : "handed to the live Maple-session bridge",
                PacketOwnedAntiMacroSubmitTransportPath.PacketOutbox
                    => _localUtilityPacketOutbox.WasLastSentOutboundPacket(
                        PacketOwnedAntiMacroAnswerSubmitOpcode,
                        _lastPacketOwnedAntiMacroSubmittedRawPacket)
                        ? "sent through the local-utility packet outbox"
                        : "handed to the local-utility packet outbox",
                PacketOwnedAntiMacroSubmitTransportPath.DeferredOfficialSessionBridge
                    => DescribePacketOwnedAntiMacroDeferredBridgeTransportState(),
                PacketOwnedAntiMacroSubmitTransportPath.DeferredPacketOutbox
                    => DescribePacketOwnedAntiMacroDeferredOutboxTransportState(),
                _ => DescribePacketOwnedAntiMacroSubmitTransportPath(_lastPacketOwnedAntiMacroSubmitTransportPath)
            };
        }

        private string DescribePacketOwnedAntiMacroDeferredBridgeTransportState()
        {
            if (_localUtilityOfficialSessionBridge.HasQueuedOutboundPacket(
                PacketOwnedAntiMacroAnswerSubmitOpcode,
                _lastPacketOwnedAntiMacroSubmittedRawPacket))
            {
                return _localUtilityOfficialSessionBridge.HasAttachedClient
                    ? "still queued on the deferred official-session bridge while the attached Maple client is waiting for crypto init"
                    : "still queued on the deferred official-session bridge awaiting a Maple client/session";
            }

            if (_localUtilityOfficialSessionBridge.WasLastSentOutboundPacket(
                PacketOwnedAntiMacroAnswerSubmitOpcode,
                _lastPacketOwnedAntiMacroSubmittedRawPacket))
            {
                return "left the deferred official-session queue and was injected into the live Maple session";
            }

            if (_localUtilityOfficialSessionBridge.HasSentOutboundPacket(
                PacketOwnedAntiMacroAnswerSubmitOpcode,
                _lastPacketOwnedAntiMacroSubmittedRawPacket))
            {
                return "left the deferred official-session queue and was injected into the live Maple session";
            }

            return _localUtilityOfficialSessionBridge.HasConnectedSession
                ? "left the deferred official-session queue without a confirmed opcode 117 injection record"
                : "no longer present in the deferred official-session queue";
        }

        private string DescribePacketOwnedAntiMacroDeferredOutboxTransportState()
        {
            if (_localUtilityPacketOutbox.HasQueuedOutboundPacket(
                PacketOwnedAntiMacroAnswerSubmitOpcode,
                _lastPacketOwnedAntiMacroSubmittedRawPacket))
            {
                return "still queued on the deferred local-utility packet outbox";
            }

            if (_localUtilityPacketOutbox.WasLastSentOutboundPacket(
                PacketOwnedAntiMacroAnswerSubmitOpcode,
                _lastPacketOwnedAntiMacroSubmittedRawPacket))
            {
                return "left the deferred local-utility packet outbox queue for delivery";
            }

            return "no longer present in the deferred local-utility packet outbox queue";
        }

        private static string DescribePacketOwnedAntiMacroSubmitTransportPath(PacketOwnedAntiMacroSubmitTransportPath transportPath)
        {
            return transportPath switch
            {
                PacketOwnedAntiMacroSubmitTransportPath.SimulatorOwned => "simulator-owned",
                PacketOwnedAntiMacroSubmitTransportPath.OfficialSessionBridge => "live official-session bridge",
                PacketOwnedAntiMacroSubmitTransportPath.PacketOutbox => "generic packet outbox",
                PacketOwnedAntiMacroSubmitTransportPath.DeferredOfficialSessionBridge => "deferred official-session bridge",
                PacketOwnedAntiMacroSubmitTransportPath.DeferredPacketOutbox => "deferred packet outbox",
                _ => "none"
            };
        }

        private static byte[] BuildPacketOwnedAntiMacroAnswerPayload(string submittedAnswer)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            WritePacketOwnedAntiMacroMapleString(writer, submittedAnswer);
            writer.Flush();
            return stream.ToArray();
        }

        private static byte[] BuildPacketOwnedAntiMacroAnswerRawPacket(IReadOnlyList<byte> payload)
        {
            int payloadLength = payload?.Count ?? 0;
            byte[] raw = new byte[sizeof(ushort) + payloadLength];
            BitConverter.GetBytes((ushort)PacketOwnedAntiMacroAnswerSubmitOpcode).CopyTo(raw, 0);
            for (int i = 0; i < payloadLength; i++)
            {
                raw[sizeof(ushort) + i] = payload[i];
            }

            return raw;
        }

        private static void WritePacketOwnedAntiMacroMapleString(BinaryWriter writer, string text)
        {
            string resolvedText = text ?? string.Empty;
            byte[] bytes = Encoding.Default.GetBytes(resolvedText);
            writer.Write((ushort)bytes.Length);
            writer.Write(bytes);
        }

        private static WzCanvasProperty ResolvePacketOwnedAntiMacroCanvas(string path)
        {
            return ResolvePacketOwnedAntiMacroNode(path) as WzCanvasProperty;
        }

        private static WzSubProperty ResolvePacketOwnedAntiMacroSubProperty(string path)
        {
            return ResolvePacketOwnedAntiMacroNode(path) as WzSubProperty;
        }

        private static WzImageProperty ResolvePacketOwnedAntiMacroNode(string path)
        {
            WzImage image = Program.FindImage("UI", "UIWindow2.img");
            if (image == null || string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            string[] segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            WzImageProperty current = image[segments[0]];
            for (int i = 1; i < segments.Length && current != null; i++)
            {
                current = current[segments[i]];
            }

            return current;
        }

        private static byte[] ReadPacketOwnedAntiMacroCanvasPayload(BinaryReader reader)
        {
            if (reader == null || reader.BaseStream.Position >= reader.BaseStream.Length)
            {
                return Array.Empty<byte>();
            }

            long start = reader.BaseStream.Position;
            long remaining = reader.BaseStream.Length - start;
            if (remaining >= sizeof(int))
            {
                uint candidateLength = reader.ReadUInt32();
                if (candidateLength <= reader.BaseStream.Length - reader.BaseStream.Position)
                {
                    return reader.ReadBytes((int)candidateLength);
                }

                reader.BaseStream.Position = start;
            }

            return reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position));
        }

        private bool TryApplyPacketOwnedAntiMacroPayload(byte[] payload, out string message)
        {
            return TryApplyPacketOwnedAntiMacroPayload(payload, out message, source: null);
        }

        private bool TryApplyPacketOwnedAntiMacroPayload(byte[] payload, out string message, string source)
        {
            message = "Anti-macro payload is missing.";
            if (payload == null || payload.Length < 2)
            {
                return false;
            }

            try
            {
                bool wasAwaitingResult = _packetOwnedAntiMacroAwaitingResult;
                string resolvedSource = ResolvePacketOwnedAntiMacroResultSource(source);
                _lastPacketOwnedAntiMacroResultSource = resolvedSource;
                _lastPacketOwnedAntiMacroAuthoritativeRoundTrip = false;
                _lastPacketOwnedAntiMacroResultPayloadHex = string.Empty;

                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream);
                int mode = reader.ReadByte();
                int antiMacroType = reader.ReadByte();
                _packetOwnedAntiMacroCurrentRemainingMs = ResolvePacketOwnedAntiMacroCurrentRemainingMs(Environment.TickCount);

                _lastPacketOwnedAntiMacroMode = mode;
                _lastPacketOwnedAntiMacroType = antiMacroType;

                if (IsPacketOwnedAntiMacroChallengeLaunchMode(mode))
                {
                    int firstAttemptFlag = reader.BaseStream.Position < reader.BaseStream.Length
                        ? reader.ReadByte()
                        : 0;
                    byte[] jpegBytes = ReadPacketOwnedAntiMacroCanvasPayload(reader);
                    message = AppendPacketOwnedAntiMacroResultSourceSummary(
                        ApplyPacketOwnedAntiMacroChallenge(antiMacroType, firstAttemptFlag, jpegBytes),
                        payload,
                        resolvedSource,
                        authoritativeRoundTrip: false);
                    return true;
                }

                if (IsPacketOwnedAntiMacroCloseResultMode(mode))
                {
                    bool hasAuthoritativeResultEvidence = HasPacketOwnedAntiMacroAuthoritativeResultEvidence(resolvedSource, payload);
                    bool authoritativeRoundTrip = ShouldCompletePacketOwnedAntiMacroAuthoritativeRoundTrip(
                        mode,
                        wasAwaitingResult,
                        resolvedSource,
                        hasAuthoritativeResultEvidence);
                    bool shouldKeepAwaitingAuthoritativeResult = ShouldKeepPacketOwnedAntiMacroAwaitingAuthoritativeResult(
                        wasAwaitingResult,
                        authoritativeRoundTrip,
                        HasPacketOwnedAntiMacroPendingAuthoritativeSubmitTransport());
                    message = AppendPacketOwnedAntiMacroResultSourceSummary(
                        ApplyPacketOwnedAntiMacroCloseResult(
                            mode,
                            antiMacroType,
                            preserveAuthoritativeSubmitAwaitingState: shouldKeepAwaitingAuthoritativeResult),
                        payload,
                        resolvedSource,
                        authoritativeRoundTrip,
                        awaitingTerminalResult: shouldKeepAwaitingAuthoritativeResult);
                    if (ShouldResetPacketOwnedAntiMacroRemainingQuestionOnResultMode(mode))
                    {
                        _packetOwnedAntiMacroCurrentRemainingMs = 0;
                    }

                    return true;
                }

                if (IsPacketOwnedAntiMacroUserBranchMode(mode))
                {
                    string userName = reader.BaseStream.Position < reader.BaseStream.Length
                        ? ReadPacketOwnedMapleString(reader)
                        : string.Empty;
                    bool hasAuthoritativeResultEvidence = HasPacketOwnedAntiMacroAuthoritativeResultEvidence(resolvedSource, payload);
                    bool authoritativeRoundTrip = ShouldCompletePacketOwnedAntiMacroAuthoritativeRoundTrip(
                        mode,
                        wasAwaitingResult,
                        resolvedSource,
                        hasAuthoritativeResultEvidence);
                    bool shouldKeepAwaitingAuthoritativeResult = ShouldKeepPacketOwnedAntiMacroAwaitingAuthoritativeResult(
                        wasAwaitingResult,
                        authoritativeRoundTrip,
                        HasPacketOwnedAntiMacroPendingAuthoritativeSubmitTransport());
                    message = AppendPacketOwnedAntiMacroResultSourceSummary(
                        ApplyPacketOwnedAntiMacroUserBranch(mode, antiMacroType, userName),
                        payload,
                        resolvedSource,
                        authoritativeRoundTrip,
                        awaitingTerminalResult: shouldKeepAwaitingAuthoritativeResult);
                    return true;
                }

                bool hasNoticeAuthoritativeResultEvidence = HasPacketOwnedAntiMacroAuthoritativeResultEvidence(resolvedSource, payload);
                bool noticeAuthoritativeRoundTrip = ShouldCompletePacketOwnedAntiMacroAuthoritativeRoundTrip(
                    mode,
                    wasAwaitingResult,
                    resolvedSource,
                    hasNoticeAuthoritativeResultEvidence);
                bool shouldKeepAwaitingNoticeAuthoritativeResult = ShouldKeepPacketOwnedAntiMacroAwaitingAuthoritativeResult(
                    wasAwaitingResult,
                    noticeAuthoritativeRoundTrip,
                    HasPacketOwnedAntiMacroPendingAuthoritativeSubmitTransport());
                if (IsPacketOwnedAntiMacroSubmitTerminalMode(mode)
                    && wasAwaitingResult
                    && !shouldKeepAwaitingNoticeAuthoritativeResult)
                {
                    _packetOwnedAntiMacroAwaitingResult = false;
                }

                message = AppendPacketOwnedAntiMacroResultSourceSummary(
                    ApplyPacketOwnedAntiMacroNotice(mode, antiMacroType),
                    payload,
                    resolvedSource,
                    noticeAuthoritativeRoundTrip,
                    awaitingTerminalResult: shouldKeepAwaitingNoticeAuthoritativeResult);
                return true;
            }
            catch (Exception ex)
            {
                message = $"Anti-macro payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedAntiMacroCommand(string[] args)
        {
            int currentTickCount = Environment.TickCount;
            if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(DescribePacketOwnedAntiMacroStatus(currentTickCount));
            }

            switch (args[0].ToLowerInvariant())
            {
                case "clear":
                case "close":
                    return ChatCommandHandler.CommandResult.Ok(ClearPacketOwnedAntiMacro(releaseCombo: true));

                case "launch":
                    if (args.Length < 2)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /localutility antimacro launch <normal|admin> [first|retry]");
                    }

                    bool adminVariant = string.Equals(args[1], "admin", StringComparison.OrdinalIgnoreCase);
                    int firstAttemptFlag = args.Length >= 3 && string.Equals(args[2], "retry", StringComparison.OrdinalIgnoreCase)
                        ? 0
                        : 1;
                    return ChatCommandHandler.CommandResult.Ok(ApplyPacketOwnedAntiMacroChallenge(adminVariant ? 2 : 1, firstAttemptFlag, null));

                case "notice":
                    if (args.Length < 2 || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int noticeType))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /localutility antimacro notice <noticeType> [antiMacroType]");
                    }

                    int noticeMode = 0;
                    if (args.Length >= 3 && !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out noticeMode))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /localutility antimacro notice <noticeType> [antiMacroType]");
                    }

                    _lastPacketOwnedAntiMacroMode = noticeType;
                    _lastPacketOwnedAntiMacroType = noticeMode;
                    return ChatCommandHandler.CommandResult.Ok(ApplyPacketOwnedAntiMacroNotice(noticeType, noticeMode));

                case "result":
                    if (args.Length < 2 || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int resultMode))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /localutility antimacro result <mode> [antiMacroType] [userName]");
                    }

                    int antiMacroType = 0;
                    if (args.Length >= 3 && !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out antiMacroType))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /localutility antimacro result <mode> [antiMacroType] [userName]");
                    }

                    _lastPacketOwnedAntiMacroMode = resultMode;
                    _lastPacketOwnedAntiMacroType = antiMacroType;
                    string userName = args.Length >= 4 ? string.Join(" ", args, 3, args.Length - 3) : string.Empty;
                    if (IsPacketOwnedAntiMacroCloseResultMode(resultMode))
                    {
                        return ChatCommandHandler.CommandResult.Ok(ApplyPacketOwnedAntiMacroCloseResult(resultMode, antiMacroType));
                    }

                    if (IsPacketOwnedAntiMacroUserBranchMode(resultMode))
                    {
                        return ChatCommandHandler.CommandResult.Ok(ApplyPacketOwnedAntiMacroUserBranch(resultMode, antiMacroType, userName));
                    }

                    return ChatCommandHandler.CommandResult.Ok(ApplyPacketOwnedAntiMacroNotice(resultMode, antiMacroType));

                default:
                    return ChatCommandHandler.CommandResult.Error("Usage: /localutility antimacro [status|launch <normal|admin> [first|retry]|notice <noticeType> [antiMacroType]|result <mode> [antiMacroType] [userName]|clear]");
            }
        }

        private bool TryGetActivePacketOwnedAntiMacroWindow(out AntiMacroChallengeWindow window)
        {
            window = null;
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.AdminAntiMacro) is AntiMacroChallengeWindow adminWindow && adminWindow.IsVisible)
            {
                window = adminWindow;
                return true;
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.AntiMacro) is AntiMacroChallengeWindow normalWindow && normalWindow.IsVisible)
            {
                window = normalWindow;
                return true;
            }

            return false;
        }

        private void HandlePacketOwnedAntiMacroNoticeClosed(int responseCode)
        {
            _lastPacketOwnedAntiMacroSummary = responseCode == 2
                ? "Dismissed anti-macro notice with Cancel."
                : "Dismissed anti-macro notice with OK.";
        }

        private string AppendPacketOwnedAntiMacroResultSourceSummary(
            string summary,
            IReadOnlyList<byte> payload,
            string source,
            bool authoritativeRoundTrip,
            bool awaitingTerminalResult = false)
        {
            _lastPacketOwnedAntiMacroResultSource = source ?? string.Empty;
            _lastPacketOwnedAntiMacroAuthoritativeRoundTrip = authoritativeRoundTrip;
            _lastPacketOwnedAntiMacroResultPayloadHex = authoritativeRoundTrip && payload != null
                ? Convert.ToHexString(payload is byte[] bytes ? bytes : payload.ToArray())
                : string.Empty;

            if (string.IsNullOrWhiteSpace(source))
            {
                return summary;
            }

            string sourceSummary;
            if (authoritativeRoundTrip)
            {
                _lastPacketOwnedAntiMacroResultTick = Environment.TickCount;
                _lastPacketOwnedAntiMacroRoundTripLatencyMs = ResolvePacketOwnedAntiMacroRoundTripLatencyMs(
                    _lastPacketOwnedAntiMacroSubmittedTick,
                    _lastPacketOwnedAntiMacroResultTick);
                _packetOwnedAntiMacroAwaitingResult = false;
                sourceSummary = $" Result arrived from {source} and completed the official-session anti-macro round-trip in {_lastPacketOwnedAntiMacroRoundTripLatencyMs}ms.";
            }
            else
            {
                sourceSummary = awaitingTerminalResult
                    ? $" Result arrived from {source}; submit parity is still awaiting an authoritative anti-macro terminal result source/mode (7/9/11)."
                    : $" Result arrived from {source}.";
            }

            _lastPacketOwnedAntiMacroSummary = $"{summary}{sourceSummary}";
            return _lastPacketOwnedAntiMacroSummary;
        }

        internal static string ResolvePacketOwnedAntiMacroResultSource(string source)
        {
            return string.IsNullOrWhiteSpace(source)
                ? string.Empty
                : source.Trim();
        }

        private static string ResolvePacketOwnedAntiMacroExpectedResultSource(string remoteEndpoint)
        {
            return string.IsNullOrWhiteSpace(remoteEndpoint)
                ? string.Empty
                : $"official-session:{remoteEndpoint.Trim()}";
        }

        private bool IsPacketOwnedAntiMacroNoticeVisible()
        {
            return uiWindowManager?.GetWindow(MapSimulatorWindowNames.AntiMacroNotice) is AntiMacroNoticeWindow noticeWindow
                && noticeWindow.IsVisible;
        }
    }
}
