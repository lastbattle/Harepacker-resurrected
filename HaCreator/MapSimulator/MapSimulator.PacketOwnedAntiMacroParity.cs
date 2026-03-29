using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using SD = System.Drawing;

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
        private const int PacketOwnedAntiMacroDefaultDurationMs = 60000;

        private string _lastPacketOwnedAntiMacroSummary = "Packet-owned anti-macro idle.";
        private string _lastPacketOwnedAntiMacroNotice = string.Empty;
        private string _lastPacketOwnedAntiMacroScreenshotPath = string.Empty;
        private int _lastPacketOwnedAntiMacroMode = -1;
        private int _lastPacketOwnedAntiMacroType = -1;
        private bool _packetOwnedAntiMacroComboHeld;

        private void RegisterPacketOwnedAntiMacroWindows()
        {
            if (uiWindowManager == null || GraphicsDevice == null)
            {
                return;
            }

            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.AntiMacro) == null)
            {
                AntiMacroChallengeWindow window = new(MapSimulatorWindowNames.AntiMacro, adminVariant: false, GraphicsDevice)
                {
                    Position = new Point(
                        Math.Max(24, (_renderParams.RenderWidth / 2) - 179),
                        Math.Max(24, (_renderParams.RenderHeight / 2) - 143))
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
        }

        private void HandlePacketOwnedAntiMacroAnswerSubmitted(string answer)
        {
            if (string.IsNullOrWhiteSpace(answer))
            {
                return;
            }

            _lastPacketOwnedAntiMacroSummary = $"Submitted anti-macro answer \"{answer.Trim()}\" through the simulator owner. Packet-authored result resolution is still required to clear the challenge.";
            ShowUtilityFeedbackMessage(_lastPacketOwnedAntiMacroSummary);
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
                ownerState = "owner inactive";
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

            if (releaseCombo)
            {
                SetPacketOwnedAntiMacroComboHold(false);
            }

            _lastPacketOwnedAntiMacroSummary = "Closed packet-owned anti-macro owner.";
            return _lastPacketOwnedAntiMacroSummary;
        }

        private void SetPacketOwnedAntiMacroComboHold(bool held)
        {
            _packetOwnedAntiMacroComboHeld = held;
            _playerManager?.Input?.SetCtrlComboSuppressed(held);
        }

        private string ApplyPacketOwnedAntiMacroChallenge(int antiMacroType, bool firstChallenge, int durationMs, byte[] jpegBytes)
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
            int expiresAt = Environment.TickCount + Math.Max(1000, durationMs);
            string statusText = firstChallenge
                ? "Packet-authored initial challenge; Ctrl combo input is held."
                : "Packet-authored retry challenge; Ctrl combo input remains held.";
            challengeWindow.Configure(challengeTexture, expiresAt, firstChallenge, statusText);
            challengeWindow.Show();
            uiWindowManager.BringToFront(challengeWindow);

            SetPacketOwnedAntiMacroComboHold(true);
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
            _lastPacketOwnedAntiMacroNotice = ResolvePacketOwnedAntiMacroNoticeText(noticeType, antiMacroType);
            _lastPacketOwnedAntiMacroSummary = $"Anti-macro notice type {noticeType} / mode {antiMacroType} routed to simulator feedback.";
            ShowUtilityFeedbackMessage(_lastPacketOwnedAntiMacroNotice);
            return _lastPacketOwnedAntiMacroSummary;
        }

        private static string ResolvePacketOwnedAntiMacroNoticeText(int noticeType, int antiMacroType)
        {
            return (noticeType, antiMacroType) switch
            {
                (0, _) => "Anti-macro challenge acknowledged.",
                (1, _) => "Anti-macro challenge failed.",
                (2, _) => "Anti-macro challenge timed out.",
                (3, _) => "Anti-macro challenge was cancelled.",
                (7, 2) => "Admin anti-macro challenge ended.",
                (7, _) => "Anti-macro challenge ended.",
                (9, 1) => "Anti-macro result marked the target as suspicious.",
                (9, 2) => "Admin anti-macro result marked the target as suspicious.",
                (9, 3) => "Anti-macro report branch completed.",
                (9, 4) => "Anti-macro screenshot branch completed.",
                (11, _) => "Anti-macro report was rejected.",
                _ => string.Format(CultureInfo.InvariantCulture, "Anti-macro notice type {0} (mode {1}) reached the simulator bridge.", noticeType, antiMacroType)
            };
        }

        private string SavePacketOwnedAntiMacroScreenshot(string userName)
        {
            string screenshotDirectory = Path.Combine(Environment.CurrentDirectory, "AntiMacroShots");
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
            string chatText = ResolvePacketOwnedAntiMacroChatText(mode, antiMacroType, resolvedName);
            if (!string.IsNullOrWhiteSpace(chatText))
            {
                _chat?.AddClientChatMessage(chatText, Environment.TickCount, 12);
            }

            if (mode is PacketOwnedAntiMacroScreenshotReportMode or PacketOwnedAntiMacroScreenshotMode
                || (mode == PacketOwnedAntiMacroUserReportMode && antiMacroType == 2))
            {
                SavePacketOwnedAntiMacroScreenshot(resolvedName);
            }

            _lastPacketOwnedAntiMacroSummary = $"Anti-macro branch mode {mode} for {resolvedName} applied through the simulator packet bridge.";
            return _lastPacketOwnedAntiMacroSummary;
        }

        private static string ResolvePacketOwnedAntiMacroChatText(int mode, int antiMacroType, string userName)
        {
            return (mode, antiMacroType) switch
            {
                (PacketOwnedAntiMacroScreenshotReportMode, _) => $"[AntiMacro] Saved report evidence for {userName}.",
                (PacketOwnedAntiMacroUserReportMode, 1) => $"[AntiMacro] Report completed for {userName}.",
                (PacketOwnedAntiMacroUserReportMode, 2) => $"[AntiMacro] Admin report completed for {userName}.",
                (PacketOwnedAntiMacroScreenshotMode, 2) => $"[AntiMacro] Saved screenshot evidence for {userName}.",
                (PacketOwnedAntiMacroChatReportMode, 2) => $"[AntiMacro] Report branch acknowledged {userName}.",
                _ => string.Format(CultureInfo.InvariantCulture, "[AntiMacro] mode {0} applied for {1}.", mode, userName)
            };
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
                        bool firstChallenge = reader.BaseStream.Position < reader.BaseStream.Length && reader.ReadByte() != 0;
                        int durationMs = reader.BaseStream.Position + sizeof(int) <= reader.BaseStream.Length
                            ? reader.ReadInt32()
                            : PacketOwnedAntiMacroDefaultDurationMs;
                        byte[] jpegBytes = Array.Empty<byte>();
                        if (reader.BaseStream.Position + sizeof(int) <= reader.BaseStream.Length)
                        {
                            int jpegLength = reader.ReadInt32();
                            if (jpegLength > 0 && reader.BaseStream.Position + jpegLength <= reader.BaseStream.Length)
                            {
                                jpegBytes = reader.ReadBytes(jpegLength);
                            }
                        }

                        message = ApplyPacketOwnedAntiMacroChallenge(antiMacroType, firstChallenge, durationMs, jpegBytes);
                        return true;
                    }

                    case PacketOwnedAntiMacroDestroyMode:
                    case PacketOwnedAntiMacroResultMode:
                        message = ClearPacketOwnedAntiMacro(releaseCombo: true);
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
                    bool firstChallenge = args.Length < 3 || !string.Equals(args[2], "retry", StringComparison.OrdinalIgnoreCase);
                    return ChatCommandHandler.CommandResult.Ok(ApplyPacketOwnedAntiMacroChallenge(adminVariant ? 2 : 1, firstChallenge, PacketOwnedAntiMacroDefaultDurationMs, null));

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
                        return ChatCommandHandler.CommandResult.Ok(ClearPacketOwnedAntiMacro(releaseCombo: true));
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
    }
}
