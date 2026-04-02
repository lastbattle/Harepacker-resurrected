using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
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
        private const int PacketOwnedAntiMacroScreenshotFolderModeClientDirectory = 0;
        private const int PacketOwnedAntiMacroScreenshotFolderModeDesktop = 1;
        private const int PacketOwnedAntiMacroScreenshotFolderModeRootDrive = 2;
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
        private string _lastPacketOwnedAntiMacroSubmittedAnswer = string.Empty;
        private bool _packetOwnedAntiMacroComboHeld;
        private bool _packetOwnedAntiMacroAwaitingResult;

        private sealed record PacketOwnedAntiMacroNoticeDefinition(int StringPoolId, string Text, string AvatarCanvasPath);
        private sealed record PacketOwnedAntiMacroChatDefinition(int StringPoolId, string FormatText, bool SaveScreenshot);

        private void RegisterPacketOwnedAntiMacroWindows()
        {
            if (uiWindowManager == null || GraphicsDevice == null)
            {
                return;
            }

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
                ApplyPacketOwnedAntiMacroOwnerVisuals(antiMacroWindow, macroProperty);
                if (_fontChat != null)
                {
                    antiMacroWindow.SetFont(_fontChat);
                }
            }

            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.AdminAntiMacro) is AntiMacroChallengeWindow adminWindow)
            {
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
            if (string.IsNullOrWhiteSpace(answer) || !TryGetActivePacketOwnedAntiMacroWindow(out AntiMacroChallengeWindow window))
            {
                return;
            }

            string submittedAnswer = answer.Trim();
            _lastPacketOwnedAntiMacroSubmittedAnswer = submittedAnswer;
            _lastPacketOwnedAntiMacroSubmittedRemainingMs = Math.Max(0, window.ExpiresAt - Environment.TickCount);
            _packetOwnedAntiMacroAwaitingResult = true;

            window.ClearChallenge();
            _lastPacketOwnedAntiMacroSummary =
                $"Queued anti-macro answer outpacket {PacketOwnedAntiMacroAnswerSubmitOpcode} with remaining={_lastPacketOwnedAntiMacroSubmittedRemainingMs}ms; awaiting packet-owned result resolution.";
        }

        private string DescribePacketOwnedAntiMacroStatus(int currentTickCount)
        {
            string ownerState;
            if (TryGetActivePacketOwnedAntiMacroWindow(out AntiMacroChallengeWindow window))
            {
                int remainingMs = Math.Max(0, window.ExpiresAt - currentTickCount);
                ownerState = $"{window.WindowName} active, remaining={remainingMs}ms, input=\"{window.CurrentInput}\"";
            }
            else
            {
                ownerState = _packetOwnedAntiMacroAwaitingResult
                    ? $"owner pending result, submitted=\"{_lastPacketOwnedAntiMacroSubmittedAnswer}\", remainingAtSubmit={_lastPacketOwnedAntiMacroSubmittedRemainingMs}ms"
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
                : $"notice=\"{_lastPacketOwnedAntiMacroNotice}\"";
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
            _lastPacketOwnedAntiMacroSummary = "Closed packet-owned anti-macro owner.";
            return _lastPacketOwnedAntiMacroSummary;
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
            int expiresAt = Environment.TickCount + PacketOwnedAntiMacroDefaultDurationMs;
            string statusText = answerCount > 0
                ? string.Format(
                    CultureInfo.InvariantCulture,
                    "Packet-authored challenge; {0} answer{1} remaining. Ctrl combo input is held.",
                    answerCount,
                    answerCount == 1 ? string.Empty : "s")
                : "Packet-authored challenge; Ctrl combo input is held.";
            challengeWindow.Configure(challengeTexture, expiresAt, answerCount, statusText);
            challengeWindow.Show();
            uiWindowManager.BringToFront(challengeWindow);

            SetPacketOwnedAntiMacroComboHold(true);
            _packetOwnedAntiMacroAwaitingResult = false;
            _lastPacketOwnedAntiMacroSubmittedAnswer = string.Empty;
            _lastPacketOwnedAntiMacroSubmittedRemainingMs = -1;
            _lastPacketOwnedAntiMacroSummary = $"Opened packet-owned {(adminVariant ? "admin " : string.Empty)}anti-macro challenge and held Ctrl combo input.";
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
                    FormatPacketOwnedAntiMacroStringPoolText(definition.Text, definition.StringPoolId),
                    definition.StringPoolId);
                noticeWindow.ConfigureVisuals(
                    LoadUiCanvasTexture(ResolvePacketOwnedAntiMacroCanvas(definition.AvatarCanvasPath)),
                    CreatePacketOwnedAntiMacroButton(ResolvePacketOwnedAntiMacroSubProperty(PacketOwnedAntiMacroPopupOkButtonPath)),
                    CreatePacketOwnedAntiMacroButton(ResolvePacketOwnedAntiMacroSubProperty(PacketOwnedAntiMacroPopupCancelButtonPath)));
                noticeWindow.Show();
                uiWindowManager.BringToFront(noticeWindow);
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

        private static PacketOwnedAntiMacroNoticeDefinition ResolvePacketOwnedAntiMacroNoticeDefinition(int noticeType, int antiMacroType)
        {
            return (noticeType, antiMacroType) switch
            {
                (0, _) => new PacketOwnedAntiMacroNoticeDefinition(0xC84, "The user cannot be found.", PacketOwnedAntiMacroAdminCanvas0Path),
                (1, _) => new PacketOwnedAntiMacroNoticeDefinition(0xC85, "You cannot use it on a user that isn't in the middle of attack.", PacketOwnedAntiMacroAdminCanvas0Path),
                (2, _) => new PacketOwnedAntiMacroNoticeDefinition(0xC86, "This user has already been tested before.", PacketOwnedAntiMacroAdminCanvas0Path),
                (3, _) => new PacketOwnedAntiMacroNoticeDefinition(0xC87, "This user is currently going through the Lie Detector Test.", PacketOwnedAntiMacroAdminCanvas0Path),
                (7, 2) => new PacketOwnedAntiMacroNoticeDefinition(0xC9A, "You will be sanctioned for using a macro-assisted program.", PacketOwnedAntiMacroAdminCanvas2Path),
                (7, _) => new PacketOwnedAntiMacroNoticeDefinition(0xC89, "The Lie Detector Test confirms that you have been botting. Repeated failure of the test will result in game restrictions.", PacketOwnedAntiMacroAdminCanvas2Path),
                (9, 1) => new PacketOwnedAntiMacroNoticeDefinition(0xC88, "Thank you for cooperating with the Lie Detector Test. You'll be rewarded 5000 mesos for not botting.", PacketOwnedAntiMacroAdminCanvas1Path),
                (9, 2) => new PacketOwnedAntiMacroNoticeDefinition(0x1A65, "Thank you for your cooperation.", PacketOwnedAntiMacroAdminCanvas1Path),
                (9, 3) => new PacketOwnedAntiMacroNoticeDefinition(0xC99, "You have succesfully passed the Lie Detector Test. Thank you for participating!", PacketOwnedAntiMacroAdminCanvas1Path),
                (9, 4) => new PacketOwnedAntiMacroNoticeDefinition(0xC99, "You have succesfully passed the Lie Detector Test. Thank you for participating!", PacketOwnedAntiMacroAdminCanvas1Path),
                (11, _) => new PacketOwnedAntiMacroNoticeDefinition(0xC98, "The user has failed the Lie Detector Test. You'll be rewarded 7000 mesos from the user.", PacketOwnedAntiMacroAdminCanvas1Path),
                _ => new PacketOwnedAntiMacroNoticeDefinition(
                    -1,
                    string.Format(CultureInfo.InvariantCulture, "Anti-macro notice type {0} (mode {1}) reached the simulator bridge.", noticeType, antiMacroType),
                    PacketOwnedAntiMacroAdminCanvas1Path)
            };
        }

        private string SavePacketOwnedAntiMacroScreenshot(string userName)
        {
            string screenshotDirectory = ResolvePacketOwnedAntiMacroScreenshotBaseFolder();
            _lastPacketOwnedAntiMacroScreenshotBaseFolder = screenshotDirectory;
            if (string.IsNullOrWhiteSpace(screenshotDirectory))
            {
                _lastPacketOwnedAntiMacroSummary = "Failed to resolve the packet-owned anti-macro screenshot base folder.";
                return _lastPacketOwnedAntiMacroSummary;
            }

            Directory.CreateDirectory(screenshotDirectory);
            string safeUserName = SanitizePacketOwnedAntiMacroFileName(string.IsNullOrWhiteSpace(userName) ? "AntiMacro" : userName.Trim());
            string filePath = Path.Combine(
                screenshotDirectory,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}_{1:yyyyMMdd_HHmmss}.jpg",
                    safeUserName,
                    DateTime.Now));

            if (!_screenshotManager.TrySaveBackBufferAsJpeg(GraphicsDevice, filePath, out string error))
            {
                _lastPacketOwnedAntiMacroSummary = $"Failed to save packet-owned anti-macro screenshot for {safeUserName}: {error}";
                return _lastPacketOwnedAntiMacroSummary;
            }

            _lastPacketOwnedAntiMacroScreenshotPath = filePath;
            _lastPacketOwnedAntiMacroSummary = $"Saved packet-owned anti-macro screenshot to {filePath}.";
            return _lastPacketOwnedAntiMacroSummary;
        }

        private static string SanitizePacketOwnedAntiMacroFileName(string userName)
        {
            StringBuilder builder = new();
            foreach (char c in userName)
            {
                builder.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c);
            }

            return builder.Length == 0 ? "AntiMacro" : builder.ToString();
        }

        private string ApplyPacketOwnedAntiMacroUserBranch(int mode, int antiMacroType, string userName)
        {
            string resolvedName = string.IsNullOrWhiteSpace(userName) ? "Unknown" : userName.Trim();
            PacketOwnedAntiMacroChatDefinition definition = ResolvePacketOwnedAntiMacroChatDefinition(mode, antiMacroType);
            _lastPacketOwnedAntiMacroChatStringPoolId = definition?.StringPoolId ?? -1;
            string chatText = FormatPacketOwnedAntiMacroClientString(definition?.FormatText, resolvedName);
            if (!string.IsNullOrWhiteSpace(chatText))
            {
                _chat?.AddClientChatMessage(
                    FormatPacketOwnedAntiMacroStringPoolText(chatText, definition.StringPoolId),
                    Environment.TickCount,
                    12);
            }

            if (definition?.SaveScreenshot == true)
            {
                SavePacketOwnedAntiMacroScreenshot(resolvedName);
            }

            _lastPacketOwnedAntiMacroSummary = $"Anti-macro branch mode {mode} for {resolvedName} applied through the simulator packet bridge.";
            return _lastPacketOwnedAntiMacroSummary;
        }

        private static PacketOwnedAntiMacroChatDefinition ResolvePacketOwnedAntiMacroChatDefinition(int mode, int antiMacroType)
        {
            return (mode, antiMacroType) switch
            {
                (PacketOwnedAntiMacroScreenshotReportMode, _) => new PacketOwnedAntiMacroChatDefinition(0xC8E, "%s_The screenshot has been saved. You have been notified of macro-assisted program monitoring.", SaveScreenshot: true),
                (PacketOwnedAntiMacroUserReportMode, 1) => new PacketOwnedAntiMacroChatDefinition(0xC8D, "%s have used the Lie Detector Test.", SaveScreenshot: false),
                (PacketOwnedAntiMacroUserReportMode, 2) => new PacketOwnedAntiMacroChatDefinition(0xC8F, "%s_The screenshot has been saved. The Lie Detector has been activated.", SaveScreenshot: true),
                (PacketOwnedAntiMacroScreenshotMode, 2) => new PacketOwnedAntiMacroChatDefinition(0xC91, "%s_The screenshot has been saved. It appears that you may be using a macro-assisted program.", SaveScreenshot: true),
                (PacketOwnedAntiMacroChatReportMode, 2) => new PacketOwnedAntiMacroChatDefinition(0xC90, "%s_You have passed the Lie Detector Test.", SaveScreenshot: false),
                _ => null
            };
        }

        private static string FormatPacketOwnedAntiMacroClientString(string formatText, string userName)
        {
            if (string.IsNullOrWhiteSpace(formatText))
            {
                return string.Empty;
            }

            string resolvedName = string.IsNullOrWhiteSpace(userName) ? "Unknown" : userName.Trim();
            return formatText
                .Replace("%s", resolvedName, StringComparison.Ordinal)
                .Replace('_', ' ')
                .Trim();
        }

        private static string FormatPacketOwnedAntiMacroStringPoolText(string text, int stringPoolId)
        {
            return stringPoolId > 0
                ? $"{text} [StringPool 0x{stringPoolId:X}]"
                : text;
        }

        private static string ResolvePacketOwnedAntiMacroScreenshotBaseFolder()
        {
            switch (ResolvePacketOwnedAntiMacroScreenshotFolderMode())
            {
                case PacketOwnedAntiMacroScreenshotFolderModeClientDirectory:
                {
                    // Mirrors CScreenShot::GetBaseFolder mode 0, which resolves the executable folder.
                    string processPath = Environment.ProcessPath;
                    if (!string.IsNullOrWhiteSpace(processPath))
                    {
                        string executableDirectory = Path.GetDirectoryName(processPath);
                        return TrimPacketOwnedAntiMacroTrailingDirectorySeparator(executableDirectory);
                    }

                    return TrimPacketOwnedAntiMacroTrailingDirectorySeparator(AppContext.BaseDirectory);
                }

                case PacketOwnedAntiMacroScreenshotFolderModeDesktop:
                    // Mirrors CScreenShot::GetBaseFolder mode 1.
                    return TrimPacketOwnedAntiMacroTrailingDirectorySeparator(
                        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));

                case PacketOwnedAntiMacroScreenshotFolderModeRootDrive:
                    // Mirrors CScreenShot::GetBaseFolder mode 2.
                    return @"C:\";

                default:
                    return string.Empty;
            }
        }

        private static int ResolvePacketOwnedAntiMacroScreenshotFolderMode()
        {
            // The client reads this from CConfig::m_nScreenShotSaveLocation; the simulator keeps the same
            // mode values but sources them from an opt-in environment variable until that config seam exists.
            string configuredValue = Environment.GetEnvironmentVariable("MAPSIM_CLIENT_SCREENSHOT_MODE");
            return int.TryParse(configuredValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedMode)
                && parsedMode is >= PacketOwnedAntiMacroScreenshotFolderModeClientDirectory and <= PacketOwnedAntiMacroScreenshotFolderModeRootDrive
                ? parsedMode
                : PacketOwnedAntiMacroScreenshotFolderModeClientDirectory;
        }

        private static string TrimPacketOwnedAntiMacroTrailingDirectorySeparator(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string root = Path.GetPathRoot(path);
            string trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.IsNullOrEmpty(trimmed) && !string.IsNullOrEmpty(root)
                ? root
                : trimmed;
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
                int candidateLength = reader.ReadInt32();
                if (candidateLength > 0 && candidateLength <= reader.BaseStream.Length - reader.BaseStream.Position)
                {
                    byte[] legacyLengthPrefixedBytes = reader.ReadBytes(candidateLength);
                    if (reader.BaseStream.Position == reader.BaseStream.Length)
                    {
                        return legacyLengthPrefixedBytes;
                    }
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

                _lastPacketOwnedAntiMacroMode = mode;
                _lastPacketOwnedAntiMacroType = antiMacroType;

                switch (mode)
                {
                    case PacketOwnedAntiMacroChallengeMode:
                    {
                        int answerCount = reader.BaseStream.Position < reader.BaseStream.Length
                            ? reader.ReadByte()
                            : 0;
                        byte[] jpegBytes = ReadPacketOwnedAntiMacroCanvasPayload(reader);
                        message = ApplyPacketOwnedAntiMacroChallenge(antiMacroType, answerCount, jpegBytes);
                        return true;
                    }

                    case PacketOwnedAntiMacroDestroyMode:
                    case PacketOwnedAntiMacroResultMode:
                        message = ApplyPacketOwnedAntiMacroCloseResult(mode, antiMacroType);
                        return true;

                    case PacketOwnedAntiMacroScreenshotReportMode:
                    case PacketOwnedAntiMacroUserReportMode:
                    case PacketOwnedAntiMacroScreenshotMode:
                    case PacketOwnedAntiMacroChatReportMode:
                    {
                        string userName = reader.BaseStream.Position < reader.BaseStream.Length
                            ? ReadPacketOwnedMapleString(reader)
                            : string.Empty;
                        message = ApplyPacketOwnedAntiMacroUserBranch(mode, antiMacroType, userName);
                        return true;
                    }

                    default:
                        message = ApplyPacketOwnedAntiMacroNotice(mode, antiMacroType);
                        return true;
                }
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
                    if (resultMode is PacketOwnedAntiMacroDestroyMode or PacketOwnedAntiMacroResultMode)
                    {
                        return ChatCommandHandler.CommandResult.Ok(ApplyPacketOwnedAntiMacroCloseResult(resultMode, antiMacroType));
                    }

                    if (resultMode is PacketOwnedAntiMacroScreenshotReportMode or PacketOwnedAntiMacroUserReportMode or PacketOwnedAntiMacroScreenshotMode or PacketOwnedAntiMacroChatReportMode)
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
    }
}
