using HaCreator.MapSimulator.Managers;
using HaSharedLibrary.Render.DX;
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

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int PacketOwnedComboDisplayDurationMs = 5000;
        private const int PacketOwnedComboBannerAnchorX = -50;
        private const int PacketOwnedComboBannerAnchorY = 180;
        private const int PacketOwnedComboDigitBaseAnchorX = -50;
        private const int PacketOwnedComboDigitSpacingPx = 23;
        private const int PacketOwnedComboDigitEvenAnchorY = 169;
        private const int PacketOwnedComboDigitOddAnchorY = 165;
        private const int PacketOwnedComboCommandAnchorX = 0;
        private const int PacketOwnedComboCommandAttackAnchorY = 215;
        private const int PacketOwnedComboCommandBuffAnchorY = 250;
        private const int PacketOwnedComboCommandBuffSoloAnchorY = 215;
        private const int PacketOwnedComboFlyDurationMs = 300;
        private const int PacketOwnedComboFlySecondaryDelayMs = 50;
        private const int PacketOwnedComboFlyPeakTimeMs = 300;
        private const int PacketOwnedComboFlyBigAmplitudePx = 10;
        private const int PacketOwnedComboFlyNormalAmplitudePx = 4;
        private readonly ComboCounterPacketInboxManager _comboCounterPacketInbox = new();
        private readonly Dictionary<string, List<IDXObject>> _packetOwnedComboAnimationCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly PacketOwnedComboCommandBinding[] _packetOwnedComboCommandBindings =
        {
            new(30, 21100004, "ComboSmash", 21100005, "ComboDrain"),
            new(100, 21110004, "ComboFenrir", 21100005, "ComboDrain"),
            new(200, 21120006, "ComboTempest", 21120007, "ComboBarrier")
        };

        private bool _comboCounterPacketInboxEnabled = EnablePacketConnectionsByDefault;
        private int _comboCounterPacketInboxConfiguredPort = ComboCounterPacketInboxManager.DefaultPort;
        private PacketOwnedComboDisplayState _packetOwnedComboState;

        private void LoadPacketOwnedComboAssets()
        {
            _packetOwnedComboAnimationCache.Clear();
        }

        private void UpdatePacketOwnedComboState(int currentTickCount)
        {
            if (_packetOwnedComboState == null)
            {
                return;
            }

            if (_packetOwnedComboState.IsExpired(currentTickCount))
            {
                _packetOwnedComboState = null;
            }
        }

        private void DrawPacketOwnedComboState(int currentTickCount)
        {
            if (_packetOwnedComboState == null || _spriteBatch == null)
            {
                return;
            }

            float alpha = _packetOwnedComboState.GetAlpha(currentTickCount);
            if (alpha <= 0f)
            {
                return;
            }

            int anchorX = _renderParams.RenderWidth / 2;
            DrawPacketOwnedComboAnimatedLayer(_packetOwnedComboState.BannerFrames, anchorX + PacketOwnedComboBannerAnchorX, PacketOwnedComboBannerAnchorY, currentTickCount, alpha);

            for (int i = 0; i < _packetOwnedComboState.Digits.Count; i++)
            {
                PacketOwnedComboDigitState digit = _packetOwnedComboState.Digits[i];
                int anchorY = ((_packetOwnedComboState.DigitCount & 1) != (i & 1))
                    ? PacketOwnedComboDigitOddAnchorY
                    : PacketOwnedComboDigitEvenAnchorY;
                int anchorOffsetX = PacketOwnedComboDigitBaseAnchorX - (i * PacketOwnedComboDigitSpacingPx);
                float verticalOffset = ResolvePacketOwnedComboFlyOffset(
                    currentTickCount - _packetOwnedComboState.StartedAtTick,
                    digit.UseBigAmplitude ? PacketOwnedComboFlyBigAmplitudePx : PacketOwnedComboFlyNormalAmplitudePx);
                DrawPacketOwnedComboAnimatedLayer(
                    digit.Frames,
                    anchorX + anchorOffsetX,
                    anchorY - (int)Math.Round(verticalOffset),
                    currentTickCount,
                    alpha);
            }

            if (_packetOwnedComboState.CommandAttackFrames?.Count > 0)
            {
                DrawPacketOwnedComboAnimatedLayer(
                    _packetOwnedComboState.CommandAttackFrames,
                    anchorX + PacketOwnedComboCommandAnchorX,
                    PacketOwnedComboCommandAttackAnchorY,
                    currentTickCount,
                    alpha);
            }

            if (_packetOwnedComboState.CommandBuffFrames?.Count > 0)
            {
                int buffAnchorY = _packetOwnedComboState.CommandAttackFrames?.Count > 0
                    ? PacketOwnedComboCommandBuffAnchorY
                    : PacketOwnedComboCommandBuffSoloAnchorY;
                DrawPacketOwnedComboAnimatedLayer(
                    _packetOwnedComboState.CommandBuffFrames,
                    anchorX + PacketOwnedComboCommandAnchorX,
                    buffAnchorY,
                    currentTickCount,
                    alpha);
            }
        }

        private void DrawPacketOwnedComboAnimatedLayer(List<IDXObject> frames, int anchorX, int anchorY, int currentTickCount, float alpha)
        {
            if (frames == null || frames.Count == 0)
            {
                return;
            }

            IDXObject frame = ResolvePacketOwnedComboFrame(frames, currentTickCount, _packetOwnedComboState?.StartedAtTick ?? currentTickCount);
            if (frame?.Texture == null)
            {
                return;
            }

            Vector2 position = new(anchorX - frame.X, anchorY - frame.Y);
            _spriteBatch.Draw(frame.Texture, position, Color.White * Math.Clamp(alpha, 0f, 1f));
        }

        private static IDXObject ResolvePacketOwnedComboFrame(IReadOnlyList<IDXObject> frames, int currentTickCount, int startedAtTick)
        {
            if (frames == null || frames.Count == 0)
            {
                return null;
            }

            int elapsed = Math.Max(0, currentTickCount - startedAtTick);
            int totalDuration = frames.Sum(static frame => Math.Max(1, frame?.Delay ?? 1));
            if (totalDuration <= 0)
            {
                return frames[^1];
            }

            int cursor = 0;
            int clampedElapsed = Math.Min(elapsed, totalDuration - 1);
            foreach (IDXObject frame in frames)
            {
                cursor += Math.Max(1, frame?.Delay ?? 1);
                if (clampedElapsed < cursor)
                {
                    return frame;
                }
            }

            return frames[^1];
        }

        private static float ResolvePacketOwnedComboFlyOffset(int elapsedMs, int amplitudePx)
        {
            if (elapsedMs <= 0)
            {
                return 0f;
            }

            if (elapsedMs < PacketOwnedComboFlySecondaryDelayMs)
            {
                return amplitudePx * (elapsedMs / (float)PacketOwnedComboFlySecondaryDelayMs);
            }

            if (elapsedMs < PacketOwnedComboFlyPeakTimeMs)
            {
                float progress = (elapsedMs - PacketOwnedComboFlySecondaryDelayMs) / (float)(PacketOwnedComboFlyPeakTimeMs - PacketOwnedComboFlySecondaryDelayMs);
                return amplitudePx * (1f - progress);
            }

            return 0f;
        }

        private void EnsureComboCounterPacketInboxState(bool shouldRun)
        {
            if (!shouldRun || !_comboCounterPacketInboxEnabled)
            {
                if (_comboCounterPacketInbox.IsRunning)
                {
                    _comboCounterPacketInbox.Stop();
                }

                return;
            }

            if (_comboCounterPacketInbox.IsRunning && _comboCounterPacketInbox.Port == _comboCounterPacketInboxConfiguredPort)
            {
                return;
            }

            if (_comboCounterPacketInbox.IsRunning)
            {
                _comboCounterPacketInbox.Stop();
            }

            try
            {
                _comboCounterPacketInbox.Start(_comboCounterPacketInboxConfiguredPort);
            }
            catch (Exception ex)
            {
                _comboCounterPacketInbox.Stop();
                _chat?.AddErrorMessage($"Combo packet inbox failed to start: {ex.Message}", currTickCount);
            }
        }

        private void DrainComboCounterPacketInbox()
        {
            while (_comboCounterPacketInbox.TryDequeue(out ComboCounterPacketInboxMessage message))
            {
                if (message == null)
                {
                    continue;
                }

                bool applied = TryApplyPacketOwnedComboPacket(message.PacketType, message.Payload, out string detail);
                _comboCounterPacketInbox.RecordDispatchResult(message, applied, detail);
                if (!string.IsNullOrWhiteSpace(detail))
                {
                    if (applied)
                    {
                        _chat?.AddSystemMessage(detail, currTickCount);
                    }
                    else
                    {
                        _chat?.AddErrorMessage(detail, currTickCount);
                    }
                }
            }
        }

        private string DescribeComboCounterPacketInboxStatus()
        {
            string enabledText = _comboCounterPacketInboxEnabled ? "enabled" : "disabled";
            string listeningText = _comboCounterPacketInbox.IsRunning
                ? $"listening on 127.0.0.1:{_comboCounterPacketInbox.Port}"
                : $"configured for 127.0.0.1:{_comboCounterPacketInboxConfiguredPort}";
            string activeComboText = _packetOwnedComboState == null
                ? "No packet-owned combo HUD is active."
                : $"Combo HUD showing {_packetOwnedComboState.ComboCount} ({DescribePacketOwnedComboTier(_packetOwnedComboState.ComboLevel)}).";
            return $"Combo packet inbox {enabledText}, {listeningText}, received {_comboCounterPacketInbox.ReceivedCount} packet(s). {activeComboText}";
        }

        private bool TryApplyPacketOwnedComboPacket(int packetType, byte[] payload, out string message)
        {
            if (packetType != ComboCounterPacketInboxManager.IncComboResponsePacketType)
            {
                message = $"Unsupported combo packet type {packetType}.";
                return false;
            }

            return TryApplyPacketOwnedComboPayload(payload, out message);
        }

        private bool TryApplyPacketOwnedComboPayload(byte[] payload, out string message)
        {
            message = null;
            if (payload == null || payload.Length < sizeof(int))
            {
                message = "Combo payload must contain an Int32 combo count.";
                return false;
            }

            try
            {
                using var stream = new MemoryStream(payload, writable: false);
                using var reader = new BinaryReader(stream, Encoding.Default, leaveOpen: false);
                int comboCount = reader.ReadInt32();
                message = ApplyPacketOwnedComboCount(comboCount, currTickCount);
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException)
            {
                message = $"Combo payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private string ApplyPacketOwnedComboCount(int comboCount, int currentTickCount)
        {
            if (comboCount <= 0)
            {
                _packetOwnedComboState = null;
                return "Cleared the packet-owned combo HUD.";
            }

            int comboLevel = ResolvePacketOwnedComboLevel(comboCount);
            if (!TryBuildPacketOwnedComboDigits(comboCount, comboLevel, out List<PacketOwnedComboDigitState> digits, out int digitCount))
            {
                _packetOwnedComboState = null;
                return $"Combo count {comboCount} could not resolve any WZ-backed digit layers.";
            }

            List<IDXObject> bannerFrames = ResolvePacketOwnedComboAnimationFrames($"Combo/{comboLevel}/Combo");
            PacketOwnedComboCommandBinding? commandBinding = _packetOwnedComboCommandBindings.FirstOrDefault(binding => binding.TriggerCount == comboCount);
            List<IDXObject> commandAttackFrames = null;
            List<IDXObject> commandBuffFrames = null;
            string commandSummary = string.Empty;
            if (commandBinding.HasValue)
            {
                PacketOwnedComboCommandBinding binding = commandBinding.Value;
                int attackSkillLevel = Math.Max(0, _playerManager?.Skills?.GetSkillLevel(binding.AttackSkillId) ?? 0);
                int buffSkillLevel = Math.Max(0, _playerManager?.Skills?.GetSkillLevel(binding.BuffSkillId) ?? 0);
                if (attackSkillLevel > 0)
                {
                    commandAttackFrames = ResolvePacketOwnedComboCommandFrames(binding.AttackSkillId, binding.AttackPathName);
                }

                if (buffSkillLevel > 0)
                {
                    commandBuffFrames = ResolvePacketOwnedComboCommandFrames(binding.BuffSkillId, binding.BuffPathName);
                }

                if (commandAttackFrames?.Count > 0 || commandBuffFrames?.Count > 0)
                {
                    commandSummary = $" Spawned combo-command overlay(s):{(commandAttackFrames?.Count > 0 ? $" {binding.AttackPathName}" : string.Empty)}{(commandBuffFrames?.Count > 0 ? $" {binding.BuffPathName}" : string.Empty)}.";
                }
            }

            _packetOwnedComboState = new PacketOwnedComboDisplayState(
                comboCount,
                comboLevel,
                currentTickCount,
                digits,
                digitCount,
                bannerFrames,
                commandAttackFrames,
                commandBuffFrames);
            return $"Applied packet-owned combo count {comboCount} ({DescribePacketOwnedComboTier(comboLevel)}).{commandSummary}";
        }

        private bool TryBuildPacketOwnedComboDigits(int comboCount, int comboLevel, out List<PacketOwnedComboDigitState> digits, out int digitCount)
        {
            digits = new List<PacketOwnedComboDigitState>();
            digitCount = 0;

            int[] digitValues = new int[5];
            int workingValue = comboCount;
            int rightMostNonZeroIndex = -1;
            for (int i = 0; i < digitValues.Length; i++)
            {
                digitValues[i] = Math.Abs(workingValue % 10);
                if (rightMostNonZeroIndex == -1 && digitValues[i] > 0)
                {
                    rightMostNonZeroIndex = i;
                }

                digitCount++;
                workingValue /= 10;
                if (workingValue == 0)
                {
                    break;
                }
            }

            for (int i = 0; i < digitCount; i++)
            {
                List<IDXObject> frames = ResolvePacketOwnedComboAnimationFrames($"Combo/{comboLevel}/{digitValues[i]}");
                if (frames?.Count == 0)
                {
                    digits.Clear();
                    digitCount = 0;
                    return false;
                }

                digits.Add(new PacketOwnedComboDigitState(digitValues[i], i > rightMostNonZeroIndex, frames));
            }

            return digits.Count > 0;
        }

        private List<IDXObject> ResolvePacketOwnedComboAnimationFrames(string propertyPath)
        {
            if (string.IsNullOrWhiteSpace(propertyPath))
            {
                return null;
            }

            if (_packetOwnedComboAnimationCache.TryGetValue(propertyPath, out List<IDXObject> cachedFrames))
            {
                return cachedFrames;
            }

            WzImage basicEffectImage = Program.FindImage("Effect", "BasicEff.img");
            List<IDXObject> frames = LoadPacketOwnedAnimationFrames(ResolvePacketOwnedPropertyPath(basicEffectImage, propertyPath), fallbackDelay: 120);
            if (frames?.Count > 0)
            {
                _packetOwnedComboAnimationCache[propertyPath] = frames;
                return frames;
            }

            return null;
        }

        private List<IDXObject> ResolvePacketOwnedComboCommandFrames(int skillId, string fallbackPathName)
        {
            string numericPath = $"ComboCommand/{skillId}";
            List<IDXObject> frames = ResolvePacketOwnedComboAnimationFrames(numericPath);
            if (frames?.Count > 0)
            {
                return frames;
            }

            return ResolvePacketOwnedComboAnimationFrames($"ComboCommand/{fallbackPathName}");
        }

        private static int ResolvePacketOwnedComboLevel(int comboCount)
        {
            if (comboCount >= 200)
            {
                return 3;
            }

            if (comboCount >= 100)
            {
                return 2;
            }

            return comboCount >= 30 ? 1 : 0;
        }

        private static string DescribePacketOwnedComboTier(int comboLevel)
        {
            return comboLevel switch
            {
                3 => "tier 3 (200+ combo)",
                2 => "tier 2 (100+ combo)",
                1 => "tier 1 (30+ combo)",
                _ => "tier 0 (<30 combo)"
            };
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedComboCommand(string[] args)
        {
            if (args == null || args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info($"{DescribeComboCounterPacketInboxStatus()} {_comboCounterPacketInbox.LastStatus}");
            }

            if (string.Equals(args[0], "clear", StringComparison.OrdinalIgnoreCase))
            {
                _packetOwnedComboState = null;
                return ChatCommandHandler.CommandResult.Ok("Cleared the packet-owned combo HUD.");
            }

            if (string.Equals(args[0], "set", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2 || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int comboCount))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /combopacket [status|clear|set <count>|packet <inccombo> [payloadhex=..|payloadb64=..]|packetraw <inccombo> <hex>|inbox [status|start [port]|stop|packet <inccombo> [payloadhex=..|payloadb64=..]|packetraw <inccombo> <hex>]]");
                }

                return ChatCommandHandler.CommandResult.Ok(ApplyPacketOwnedComboCount(comboCount, currTickCount));
            }

            if (string.Equals(args[0], "inbox", StringComparison.OrdinalIgnoreCase))
            {
                return HandlePacketOwnedComboInboxCommand(args);
            }

            bool rawHex = string.Equals(args[0], "packetraw", StringComparison.OrdinalIgnoreCase);
            if (!rawHex && !string.Equals(args[0], "packet", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /combopacket [status|clear|set <count>|packet <inccombo> [payloadhex=..|payloadb64=..]|packetraw <inccombo> <hex>|inbox [status|start [port]|stop|packet <inccombo> [payloadhex=..|payloadb64=..]|packetraw <inccombo> <hex>]]");
            }

            if (args.Length < 2 || !ComboCounterPacketInboxManager.TryParsePacketType(args[1], out int packetType))
            {
                return ChatCommandHandler.CommandResult.Error("Combo packet type must be inccombo, combo, or 0x44C.");
            }

            byte[] payload = Array.Empty<byte>();
            if (rawHex)
            {
                if (args.Length < 3 || !TryDecodeHexBytes(string.Join(string.Empty, args.Skip(2)), out payload))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /combopacket packetraw <inccombo> <hex>");
                }
            }
            else if (args.Length > 2 && !TryParseBinaryPayloadArgument(args[2], out payload, out string payloadError))
            {
                return ChatCommandHandler.CommandResult.Error(payloadError ?? "Usage: /combopacket packet <inccombo> [payloadhex=..|payloadb64=..]");
            }

            return TryApplyPacketOwnedComboPacket(packetType, payload, out string message)
                ? ChatCommandHandler.CommandResult.Ok(message)
                : ChatCommandHandler.CommandResult.Error(message);
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedComboInboxCommand(string[] args)
        {
            if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info($"{DescribeComboCounterPacketInboxStatus()} {_comboCounterPacketInbox.LastStatus}");
            }

            if (string.Equals(args[1], "start", StringComparison.OrdinalIgnoreCase))
            {
                int port = ComboCounterPacketInboxManager.DefaultPort;
                if (args.Length >= 3 && (!int.TryParse(args[2], out port) || port <= 0 || port > ushort.MaxValue))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /combopacket inbox [status|start [port]|stop|packet <inccombo> [payloadhex=..|payloadb64=..]|packetraw <inccombo> <hex>]");
                }

                _comboCounterPacketInboxConfiguredPort = port;
                _comboCounterPacketInboxEnabled = true;
                EnsureComboCounterPacketInboxState(shouldRun: true);
                return ChatCommandHandler.CommandResult.Ok($"{DescribeComboCounterPacketInboxStatus()} {_comboCounterPacketInbox.LastStatus}");
            }

            if (string.Equals(args[1], "stop", StringComparison.OrdinalIgnoreCase))
            {
                _comboCounterPacketInboxEnabled = false;
                EnsureComboCounterPacketInboxState(shouldRun: false);
                return ChatCommandHandler.CommandResult.Ok($"{DescribeComboCounterPacketInboxStatus()} {_comboCounterPacketInbox.LastStatus}");
            }

            bool rawHex = string.Equals(args[1], "packetraw", StringComparison.OrdinalIgnoreCase);
            if (!rawHex && !string.Equals(args[1], "packet", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /combopacket inbox [status|start [port]|stop|packet <inccombo> [payloadhex=..|payloadb64=..]|packetraw <inccombo> <hex>]");
            }

            if (args.Length < 3 || !ComboCounterPacketInboxManager.TryParsePacketType(args[2], out int packetType))
            {
                return ChatCommandHandler.CommandResult.Error("Combo packet type must be inccombo, combo, or 0x44C.");
            }

            byte[] payload = Array.Empty<byte>();
            if (rawHex)
            {
                if (args.Length < 4 || !TryDecodeHexBytes(string.Join(string.Empty, args.Skip(3)), out payload))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /combopacket inbox packetraw <inccombo> <hex>");
                }
            }
            else if (args.Length > 3 && !TryParseBinaryPayloadArgument(args[3], out payload, out string payloadError))
            {
                return ChatCommandHandler.CommandResult.Error(payloadError ?? "Usage: /combopacket inbox packet <inccombo> [payloadhex=..|payloadb64=..]");
            }

            if (payload.Length == 0)
            {
                payload = ComboCounterPacketInboxManager.BuildComboCountPayload(0);
            }

            if (!TryApplyPacketOwnedComboPacket(packetType, payload, out string message))
            {
                return ChatCommandHandler.CommandResult.Error(message);
            }

            return ChatCommandHandler.CommandResult.Ok(message);
        }

        private readonly record struct PacketOwnedComboCommandBinding(
            int TriggerCount,
            int AttackSkillId,
            string AttackPathName,
            int BuffSkillId,
            string BuffPathName);

        private sealed class PacketOwnedComboDisplayState
        {
            public PacketOwnedComboDisplayState(
                int comboCount,
                int comboLevel,
                int startedAtTick,
                List<PacketOwnedComboDigitState> digits,
                int digitCount,
                List<IDXObject> bannerFrames,
                List<IDXObject> commandAttackFrames,
                List<IDXObject> commandBuffFrames)
            {
                ComboCount = comboCount;
                ComboLevel = comboLevel;
                StartedAtTick = startedAtTick;
                Digits = digits ?? new List<PacketOwnedComboDigitState>();
                DigitCount = digitCount;
                BannerFrames = bannerFrames;
                CommandAttackFrames = commandAttackFrames;
                CommandBuffFrames = commandBuffFrames;
            }

            public int ComboCount { get; }
            public int ComboLevel { get; }
            public int StartedAtTick { get; }
            public int DigitCount { get; }
            public List<PacketOwnedComboDigitState> Digits { get; }
            public List<IDXObject> BannerFrames { get; }
            public List<IDXObject> CommandAttackFrames { get; }
            public List<IDXObject> CommandBuffFrames { get; }

            public bool IsExpired(int currentTickCount)
            {
                return currentTickCount - StartedAtTick >= PacketOwnedComboDisplayDurationMs;
            }

            public float GetAlpha(int currentTickCount)
            {
                int elapsed = Math.Max(0, currentTickCount - StartedAtTick);
                if (elapsed >= PacketOwnedComboDisplayDurationMs)
                {
                    return 0f;
                }

                return 1f - (elapsed / (float)PacketOwnedComboDisplayDurationMs);
            }
        }

        private readonly record struct PacketOwnedComboDigitState(
            int Digit,
            bool UseBigAmplitude,
            List<IDXObject> Frames);
    }
}
