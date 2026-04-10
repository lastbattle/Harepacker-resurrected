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
        private PacketOwnedAntiMacroSubmitTransportPath _lastPacketOwnedAntiMacroSubmitTransportPath;
        private byte[] _lastPacketOwnedAntiMacroSubmittedRawPacket = Array.Empty<byte>();

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
            return mode is PacketOwnedAntiMacroDestroyMode or PacketOwnedAntiMacroResultMode;
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
                    LoadUiCanvasTexture(ResolvePacketOwnedAntiMacroCanvas(PacketOwnedAntiMacroPopupCanvasPath)))
                {
                    Position = new Point(
                        Math.Max(24, (_renderParams.RenderWidth / 2) - 130),
                        Math.Max(24, (_renderParams.RenderHeight / 2) - 65))
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
                if (_fontChat != null)
                {
                    noticeWindow.SetFont(_fontChat);
                }

                noticeWindow.ConfigureVisuals(
                    LoadUiCanvasTexture(ResolvePacketOwnedAntiMacroCanvas(PacketOwnedAntiMacroAdminCanvas0Path)),
                    CreatePacketOwnedAntiMacroButton(ResolvePacketOwnedAntiMacroSubProperty(PacketOwnedAntiMacroPopupOkButtonPath)),
                    CreatePacketOwnedAntiMacroButton(ResolvePacketOwnedAntiMacroSubProperty(PacketOwnedAntiMacroPopupCancelButtonPath)));
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
                ownerState = _packetOwnedAntiMacroAwaitingResult
                    ? $"owner pending result, submitted=\"{_lastPacketOwnedAntiMacroSubmittedAnswer}\", remainingAtSubmit={_lastPacketOwnedAntiMacroSubmittedRemainingMs}ms, transport={transportState}"
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
            return $"Anti-macro mode={_lastPacketOwnedAntiMacroMode}, type={_lastPacketOwnedAntiMacroType}, comboHeld={_packetOwnedAntiMacroComboHeld}, {ownerState}, {noticeState}, {screenshotState}, {screenshotBaseFolderState}. {_lastPacketOwnedAntiMacroSummary}";
        }

        private string ClearPacketOwnedAntiMacro(bool releaseCombo)
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

            _packetOwnedAntiMacroAwaitingResult = false;
            _lastPacketOwnedAntiMacroSubmittedAnswer = string.Empty;
            _lastPacketOwnedAntiMacroSubmittedRemainingMs = -1;
            _packetOwnedAntiMacroCurrentRemainingMs = 0;
            _lastPacketOwnedAntiMacroSubmitTransportPath = PacketOwnedAntiMacroSubmitTransportPath.None;
            _lastPacketOwnedAntiMacroSubmittedRawPacket = Array.Empty<byte>();
            _lastPacketOwnedAntiMacroSummary = "Closed packet-owned anti-macro owner.";
            return _lastPacketOwnedAntiMacroSummary;
        }

        private int ResolvePacketOwnedAntiMacroCurrentRemainingMs(int currentTickCount)
        {
            if (TryGetActivePacketOwnedAntiMacroWindow(out AntiMacroChallengeWindow window))
            {
                return Math.Max(0, window.ExpiresAt - currentTickCount);
            }

            return Math.Max(0, _packetOwnedAntiMacroCurrentRemainingMs);
        }

        private void SetPacketOwnedAntiMacroComboHold(bool held)
        {
            _packetOwnedAntiMacroComboHeld = held;
            _playerManager?.Input?.SetCtrlComboSuppressed(held);
        }

        private string ApplyPacketOwnedAntiMacroChallenge(int antiMacroType, int answerCount, byte[] jpegBytes)
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
            int remainingMs = ResolvePacketOwnedAntiMacroLaunchRemainingMs(_packetOwnedAntiMacroCurrentRemainingMs, answerCount);
            int expiresAt = Environment.TickCount + remainingMs;
            string statusText = answerCount > 0
                ? string.Format(
                    CultureInfo.InvariantCulture,
                    "Packet-authored challenge; {0} answer{1} remaining. Ctrl combo input is held.",
                    answerCount,
                    answerCount == 1 ? string.Empty : "s")
                : "Packet-authored challenge; Ctrl combo input is held.";
            challengeWindow.Configure(challengeTexture, expiresAt, answerCount, statusText);
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
                noticeWindow.ConfigureVisuals(
                    LoadUiCanvasTexture(ResolvePacketOwnedAntiMacroCanvas(definition.AvatarCanvasPath)),
                    CreatePacketOwnedAntiMacroButton(ResolvePacketOwnedAntiMacroSubProperty(PacketOwnedAntiMacroPopupOkButtonPath)),
                    CreatePacketOwnedAntiMacroButton(ResolvePacketOwnedAntiMacroSubProperty(PacketOwnedAntiMacroPopupCancelButtonPath)));
                ShowWindow(
                    MapSimulatorWindowNames.AntiMacroNotice,
                    noticeWindow,
                    trackDirectionModeOwner: true);
            }

            _lastPacketOwnedAntiMacroSummary = $"Opened anti-macro notice owner for type {noticeType} / mode {antiMacroType}.";
            return _lastPacketOwnedAntiMacroSummary;
        }

        private string ApplyPacketOwnedAntiMacroCloseResult(int mode, int antiMacroType)
        {
            string clearSummary = ClearPacketOwnedAntiMacro(releaseCombo: true);
            string noticeSummary = ApplyPacketOwnedAntiMacroNotice(mode, antiMacroType);
            _lastPacketOwnedAntiMacroSummary = $"{clearSummary} {noticeSummary}";
            return _lastPacketOwnedAntiMacroSummary;
        }

        internal static int ResolvePacketOwnedAntiMacroLaunchRemainingMs(int currentRemainingMs, int answerCount)
        {
            // `CWvsContext::OnAntiMacroResult` only reseeds the client timer to 60000ms
            // when the decoded question counter is non-zero; otherwise it reuses the
            // current context-owned anti-macro remaining time.
            return answerCount != 0
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
            string safeUserName = PacketOwnedAntiMacroScreenshotPathResolver.SanitizeUserName(userName);
            string filePath = PacketOwnedAntiMacroScreenshotPathResolver.BuildFilePath(
                screenshotDirectory,
                safeUserName,
                DateTime.Now);

            if (!_screenshotManager.TrySaveBackBufferAsJpeg(GraphicsDevice, filePath, out string error))
            {
                _lastPacketOwnedAntiMacroSummary = $"Failed to save packet-owned anti-macro screenshot for {safeUserName}: {error}";
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

            return _localUtilityOfficialSessionBridge.HasConnectedSession
                ? "left the deferred official-session queue and is awaiting an authoritative anti-macro result"
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
            message = "Anti-macro payload is missing.";
            if (payload == null || payload.Length < 2)
            {
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream);
                int mode = reader.ReadByte();
                int antiMacroType = reader.ReadByte();
                _packetOwnedAntiMacroCurrentRemainingMs = ResolvePacketOwnedAntiMacroCurrentRemainingMs(Environment.TickCount);

                _lastPacketOwnedAntiMacroMode = mode;
                _lastPacketOwnedAntiMacroType = antiMacroType;

                if (IsPacketOwnedAntiMacroChallengeLaunchMode(mode))
                {
                    int answerCount = reader.BaseStream.Position < reader.BaseStream.Length
                        ? reader.ReadByte()
                        : 0;
                    byte[] jpegBytes = ReadPacketOwnedAntiMacroCanvasPayload(reader);
                    message = ApplyPacketOwnedAntiMacroChallenge(antiMacroType, answerCount, jpegBytes);
                    return true;
                }

                if (IsPacketOwnedAntiMacroCloseResultMode(mode))
                {
                    message = ApplyPacketOwnedAntiMacroCloseResult(mode, antiMacroType);
                    return true;
                }

                if (IsPacketOwnedAntiMacroUserBranchMode(mode))
                {
                    string userName = reader.BaseStream.Position < reader.BaseStream.Length
                        ? ReadPacketOwnedMapleString(reader)
                        : string.Empty;
                    message = ApplyPacketOwnedAntiMacroUserBranch(mode, antiMacroType, userName);
                    return true;
                }

                message = ApplyPacketOwnedAntiMacroNotice(mode, antiMacroType);
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
                    int answerCount = args.Length >= 3 && string.Equals(args[2], "retry", StringComparison.OrdinalIgnoreCase)
                        ? 1
                        : 3;
                    return ChatCommandHandler.CommandResult.Ok(ApplyPacketOwnedAntiMacroChallenge(adminVariant ? 2 : 1, answerCount, null));

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

        private bool IsPacketOwnedAntiMacroNoticeVisible()
        {
            return uiWindowManager?.GetWindow(MapSimulatorWindowNames.AntiMacroNotice) is AntiMacroNoticeWindow noticeWindow
                && noticeWindow.IsVisible;
        }
    }
}
