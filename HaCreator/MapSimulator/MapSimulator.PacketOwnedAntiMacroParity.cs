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
        private static readonly Point PacketOwnedAntiMacroBackground2Offset = new(3, 7);
        private static readonly Point PacketOwnedAntiMacroBackground3Offset = new(5, 23);
        private const string PacketOwnedAntiMacroPopupCanvasPath = "Macro/popup";
        private const string PacketOwnedAntiMacroPopupOkButtonPath = "Macro/btOK";
        private const string PacketOwnedAntiMacroPopupCancelButtonPath = "Macro/btCancle";
        private const string PacketOwnedAntiMacroAdminCanvas0Path = "Macro/admin/0";
        private const string PacketOwnedAntiMacroAdminCanvas1Path = "Macro/admin/1";
        private const string PacketOwnedAntiMacroAdminCanvas2Path = "Macro/admin/2";

        private string _lastPacketOwnedAntiMacroSummary = "Packet-owned anti-macro idle.";
        private string _lastPacketOwnedAntiMacroNotice = string.Empty;
        private string _lastPacketOwnedAntiMacroScreenshotPath = string.Empty;
        private int _lastPacketOwnedAntiMacroMode = -1;
        private int _lastPacketOwnedAntiMacroType = -1;
        private int _lastPacketOwnedAntiMacroNoticeStringPoolId = -1;
        private int _lastPacketOwnedAntiMacroChatStringPoolId = -1;
        private int _lastPacketOwnedAntiMacroSubmittedRemainingMs = -1;
        private string _lastPacketOwnedAntiMacroSubmittedAnswer = string.Empty;
        private bool _packetOwnedAntiMacroComboHeld;
        private bool _packetOwnedAntiMacroAwaitingResult;

        private sealed record PacketOwnedAntiMacroNoticeDefinition(int StringPoolId, string Text, string AvatarCanvasPath);
        private sealed record PacketOwnedAntiMacroChatDefinition(int StringPoolId, string Text, bool SaveScreenshot);

        private void RegisterPacketOwnedAntiMacroWindows()
        {
            if (uiWindowManager == null || GraphicsDevice == null)
            {
                return;
            }

            WzSubProperty macroProperty = (Program.FindImage("UI", "UIWindow2.img")?["Macro"] as WzSubProperty);

            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.AntiMacro) == null)
            {
                AntiMacroChallengeWindow window = new(MapSimulatorWindowNames.AntiMacro, adminVariant: false, GraphicsDevice)
                {
                    Position = new Point(
                        Math.Max(24, (_renderParams.RenderWidth / 2) - 132),
                        Math.Max(24, (_renderParams.RenderHeight / 2) - 125))
                };
                window.SubmitRequested += HandlePacketOwnedAntiMacroAnswerSubmitted;
                uiWindowManager.RegisterCustomWindow(window);
            }

            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.AdminAntiMacro) == null)
            {
                AntiMacroChallengeWindow window = new(MapSimulatorWindowNames.AdminAntiMacro, adminVariant: true, GraphicsDevice)
                {
                    Position = new Point(
                        Math.Max(24, (_renderParams.RenderWidth / 2) - 179),
                        Math.Max(24, (_renderParams.RenderHeight / 2) - 143))
                };
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

            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.AdminAntiMacro) is AntiMacroChallengeWindow adminWindow && _fontChat != null)
            {
                adminWindow.SetFont(_fontChat);
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
            if (window == null || window.IsAdminVariant || macroProperty == null)
            {
                return;
            }

            WzSubProperty step1Property = macroProperty["step1"] as WzSubProperty;
            if (step1Property == null)
            {
                return;
            }

            Texture2D frameTexture = ComposePacketOwnedAntiMacroFrameTexture(step1Property);
            UIObject submitButton = CreatePacketOwnedAntiMacroButton(step1Property["btOK"] as WzSubProperty);
            (Texture2D[] digitTextures, Point[] digitOrigins, Texture2D commaTexture, Point commaOrigin) = LoadPacketOwnedAntiMacroDigits(macroProperty["num1"] as WzSubProperty);

            window.ConfigureVisualAssets(
                frameTexture,
                digitTextures,
                digitOrigins,
                commaTexture,
                commaOrigin,
                submitButton);
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
            string noticeState = string.IsNullOrWhiteSpace(_lastPacketOwnedAntiMacroNotice)
                ? "no notice"
                : $"notice=\"{_lastPacketOwnedAntiMacroNotice}\"";
            return $"Anti-macro mode={_lastPacketOwnedAntiMacroMode}, type={_lastPacketOwnedAntiMacroType}, comboHeld={_packetOwnedAntiMacroComboHeld}, {ownerState}, {noticeState}, {screenshotState}. {_lastPacketOwnedAntiMacroSummary}";
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
                (0, _) => new PacketOwnedAntiMacroNoticeDefinition(0xC84, "Anti-macro challenge acknowledged.", PacketOwnedAntiMacroAdminCanvas0Path),
                (1, _) => new PacketOwnedAntiMacroNoticeDefinition(0xC85, "Anti-macro challenge failed.", PacketOwnedAntiMacroAdminCanvas0Path),
                (2, _) => new PacketOwnedAntiMacroNoticeDefinition(0xC86, "Anti-macro challenge timed out.", PacketOwnedAntiMacroAdminCanvas0Path),
                (3, _) => new PacketOwnedAntiMacroNoticeDefinition(0xC87, "Anti-macro challenge was cancelled.", PacketOwnedAntiMacroAdminCanvas0Path),
                (7, 2) => new PacketOwnedAntiMacroNoticeDefinition(0xC9A, "Admin anti-macro challenge ended.", PacketOwnedAntiMacroAdminCanvas2Path),
                (7, _) => new PacketOwnedAntiMacroNoticeDefinition(0xC89, "Anti-macro challenge ended.", PacketOwnedAntiMacroAdminCanvas2Path),
                (9, 1) => new PacketOwnedAntiMacroNoticeDefinition(0xC88, "Anti-macro result marked the target as suspicious.", PacketOwnedAntiMacroAdminCanvas1Path),
                (9, 2) => new PacketOwnedAntiMacroNoticeDefinition(0x1A65, "Admin anti-macro result marked the target as suspicious.", PacketOwnedAntiMacroAdminCanvas1Path),
                (9, 3) => new PacketOwnedAntiMacroNoticeDefinition(0xC99, "Anti-macro report branch completed.", PacketOwnedAntiMacroAdminCanvas1Path),
                (9, 4) => new PacketOwnedAntiMacroNoticeDefinition(0xC99, "Anti-macro screenshot branch completed.", PacketOwnedAntiMacroAdminCanvas1Path),
                (11, _) => new PacketOwnedAntiMacroNoticeDefinition(0xC98, "Anti-macro report was rejected.", PacketOwnedAntiMacroAdminCanvas1Path),
                _ => new PacketOwnedAntiMacroNoticeDefinition(
                    -1,
                    string.Format(CultureInfo.InvariantCulture, "Anti-macro notice type {0} (mode {1}) reached the simulator bridge.", noticeType, antiMacroType),
                    PacketOwnedAntiMacroAdminCanvas1Path)
            };
        }

        private string SavePacketOwnedAntiMacroScreenshot(string userName)
        {
            string screenshotDirectory = ResolvePacketOwnedAntiMacroScreenshotBaseFolder();
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
            PacketOwnedAntiMacroChatDefinition definition = ResolvePacketOwnedAntiMacroChatDefinition(mode, antiMacroType, resolvedName);
            _lastPacketOwnedAntiMacroChatStringPoolId = definition?.StringPoolId ?? -1;
            if (!string.IsNullOrWhiteSpace(definition?.Text))
            {
                _chat?.AddClientChatMessage(
                    FormatPacketOwnedAntiMacroStringPoolText(definition.Text, definition.StringPoolId),
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

        private static PacketOwnedAntiMacroChatDefinition ResolvePacketOwnedAntiMacroChatDefinition(int mode, int antiMacroType, string userName)
        {
            return (mode, antiMacroType) switch
            {
                (PacketOwnedAntiMacroScreenshotReportMode, _) => new PacketOwnedAntiMacroChatDefinition(0xC8E, $"Saved report evidence for {userName}.", SaveScreenshot: true),
                (PacketOwnedAntiMacroUserReportMode, 1) => new PacketOwnedAntiMacroChatDefinition(0xC8D, $"Report completed for {userName}.", SaveScreenshot: false),
                (PacketOwnedAntiMacroUserReportMode, 2) => new PacketOwnedAntiMacroChatDefinition(0xC8F, $"Admin report completed for {userName}.", SaveScreenshot: true),
                (PacketOwnedAntiMacroScreenshotMode, 2) => new PacketOwnedAntiMacroChatDefinition(0xC91, $"Saved screenshot evidence for {userName}.", SaveScreenshot: true),
                (PacketOwnedAntiMacroChatReportMode, 2) => new PacketOwnedAntiMacroChatDefinition(0xC90, $"Report branch acknowledged {userName}.", SaveScreenshot: false),
                _ => null
            };
        }

        private static string FormatPacketOwnedAntiMacroStringPoolText(string text, int stringPoolId)
        {
            return stringPoolId > 0
                ? $"{text} [StringPool 0x{stringPoolId:X}]"
                : text;
        }

        private static string ResolvePacketOwnedAntiMacroScreenshotBaseFolder()
        {
            string picturesFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (string.IsNullOrWhiteSpace(picturesFolder))
            {
                return Path.Combine(Environment.CurrentDirectory, "MapleStory");
            }

            return Path.Combine(picturesFolder, "MapleStory");
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
