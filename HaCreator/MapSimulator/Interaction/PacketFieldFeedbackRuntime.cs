using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum PacketFieldFeedbackPacketKind
    {
        GroupMessage = 150,
        Whisper = 151,
        CoupleMessage = 152,
        FieldEffect = 154,
        FieldObstacleOnOff = 155,
        FieldObstacleOnOffStatus = 156,
        WarnMessage = 157,
        PlayJukeBox = 158,
        FieldObstacleAllReset = 159,
        TransferFieldReqIgnored = 160,
        TransferChannelReqIgnored = 161,
        DestroyClock = 163,
        SummonItemUnavailable = 164,
        ZakumTimer = 170,
        HontailTimer = 171,
        ChaosZakumTimer = 172,
        HontaleTimer = 173,
        FieldFadeOutForce = 174,
    }

    internal sealed class PacketFieldFeedbackCallbacks
    {
        internal Action<string, int, string> AddClientChatMessage { get; init; }
        internal Action<string> ShowUtilityFeedback { get; init; }
        internal Action<string> ShowModalWarning { get; init; }
        internal Action<string> RememberWhisperTarget { get; init; }
        internal Action<int, int> TriggerTremble { get; init; }
        internal Action ClearFieldFade { get; init; }
        internal Action<string> RequestBgm { get; init; }
        internal Func<string, bool> PlayFieldSound { get; init; }
        internal Func<string, bool?, int, int?, bool> SetObjectTagState { get; init; }
        internal Func<byte, int, int, bool> ShowSummonEffectVisual { get; init; }
        internal Func<string, bool> ShowScreenEffectVisual { get; init; }
        internal Func<int, int, int, bool> ShowRewardRouletteVisual { get; init; }
        internal Func<int, string> ResolveMobName { get; init; }
        internal Func<int, string> ResolveMapName { get; init; }
        internal Func<int, string> ResolveItemName { get; init; }
        internal Func<int, string> ResolveChannelName { get; init; }
        internal Func<string, bool> IsBlacklistedName { get; init; }
        internal Func<string, bool> IsBlockedFriendName { get; init; }
        internal Func<int, int, int, bool> QueueMapTransfer { get; init; }
    }

    internal sealed class PacketFieldFeedbackRuntime
    {
        private const int BossHpDisplayDurationMs = 10000;
        private const int BossHpBarWidth = 288;
        private const int BossHpBarHeight = 16;
        private const int BossHpFramePadding = 4;

        private readonly Dictionary<string, bool> _obstacleStates = new(StringComparer.OrdinalIgnoreCase);
        private Texture2D _pixelTexture;
        private string _statusMessage = "Packet-owned field feedback idle.";
        private string _lastWarningMessage = string.Empty;
        private string _lastFieldEffectSummary = string.Empty;
        private string _lastBgmDescriptor = string.Empty;
        private string _lastFieldSoundDescriptor = string.Empty;
        private string _lastTransferFailureMessage = string.Empty;
        private string _lastWhisperTarget = string.Empty;
        private string _lastJukeboxSummary = string.Empty;
        private string _lastBossTimerSummary = string.Empty;
        private BossHpState _bossHpState;

        internal void Initialize(GraphicsDevice graphicsDevice)
        {
            if (_pixelTexture == null && graphicsDevice != null)
            {
                _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
                _pixelTexture.SetData(new[] { Color.White });
            }
        }

        internal void Clear()
        {
            _obstacleStates.Clear();
            _bossHpState = null;
            _statusMessage = "Packet-owned field feedback cleared.";
            _lastWarningMessage = string.Empty;
            _lastFieldEffectSummary = string.Empty;
            _lastBgmDescriptor = string.Empty;
            _lastFieldSoundDescriptor = string.Empty;
            _lastTransferFailureMessage = string.Empty;
            _lastWhisperTarget = string.Empty;
            _lastJukeboxSummary = string.Empty;
            _lastBossTimerSummary = string.Empty;
        }

        internal void Update(int currentTick)
        {
            if (_bossHpState != null && currentTick >= _bossHpState.ExpiresAtTick)
            {
                _bossHpState = null;
            }
        }

        internal string DescribeStatus(int currentTick)
        {
            string bossHpStatus = _bossHpState == null
                ? "bosshp=idle"
                : string.Format(
                    CultureInfo.InvariantCulture,
                    "bosshp={0} {1}/{2}",
                    _bossHpState.Name,
                    _bossHpState.CurrentHp,
                    _bossHpState.MaxHp);

            string obstacleStatus = _obstacleStates.Count == 0
                ? "obstacles=none"
                : $"obstacles={string.Join(", ", _obstacleStates.OrderBy(static pair => pair.Key).Take(4).Select(static pair => $"{pair.Key}:{(pair.Value ? "on" : "off")}"))}";
            string bossTimerStatus = string.IsNullOrWhiteSpace(_lastBossTimerSummary)
                ? "bosstimer=none"
                : $"bosstimer=\"{TrimForStatus(_lastBossTimerSummary)}\"";

            return string.Join(
                "; ",
                _statusMessage,
                string.IsNullOrWhiteSpace(_lastWhisperTarget) ? "whisper=none" : $"whisper={_lastWhisperTarget}",
                string.IsNullOrWhiteSpace(_lastWarningMessage) ? "warn=none" : $"warn=\"{TrimForStatus(_lastWarningMessage)}\"",
                string.IsNullOrWhiteSpace(_lastFieldEffectSummary) ? "effect=none" : $"effect=\"{TrimForStatus(_lastFieldEffectSummary)}\"",
                string.IsNullOrWhiteSpace(_lastBgmDescriptor) ? "bgm=default" : $"bgm={_lastBgmDescriptor}",
                string.IsNullOrWhiteSpace(_lastFieldSoundDescriptor) ? "fieldsound=none" : $"fieldsound={_lastFieldSoundDescriptor}",
                string.IsNullOrWhiteSpace(_lastTransferFailureMessage) ? "transfer=none" : $"transfer=\"{TrimForStatus(_lastTransferFailureMessage)}\"",
                string.IsNullOrWhiteSpace(_lastJukeboxSummary) ? "jukebox=none" : $"jukebox=\"{TrimForStatus(_lastJukeboxSummary)}\"",
                bossTimerStatus,
                obstacleStatus,
                bossHpStatus);
        }

        internal void Draw(SpriteBatch spriteBatch, SpriteFont font, int renderWidth, int currentTick)
        {
            if (spriteBatch == null || font == null || _pixelTexture == null)
            {
                return;
            }

            DrawBossHp(spriteBatch, font, renderWidth, currentTick);
        }

        internal static byte[] BuildWhisperAvailabilityPayload(string target, bool available)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)34);
            WriteMapleString(writer, target);
            writer.Write(available ? (byte)1 : (byte)0);
            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildWhisperLocationPayload(byte subtype, string target, byte result, int value)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write(subtype);
            WriteMapleString(writer, target);
            writer.Write(result);
            writer.Write(value);
            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildCoupleNoticePayload(string message)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)4);
            writer.Write(string.IsNullOrWhiteSpace(message) ? (byte)0 : (byte)1);
            if (!string.IsNullOrWhiteSpace(message))
            {
                WriteMapleString(writer, message);
            }

            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildCoupleChatPayload(string sender, string message)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)5);
            WriteMapleString(writer, sender);
            writer.Write((byte)0);
            WriteMapleString(writer, message);
            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildJukeBoxPayload(int itemId, string owner)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write(itemId);
            WriteMapleString(writer, owner);
            writer.Flush();
            return stream.ToArray();
        }

        internal bool TryApplyPacket(
            PacketFieldFeedbackPacketKind kind,
            byte[] payload,
            int currentTick,
            PacketFieldFeedbackCallbacks callbacks,
            out string message)
        {
            payload ??= Array.Empty<byte>();
            try
            {
                return kind switch
                {
                    PacketFieldFeedbackPacketKind.GroupMessage => TryApplyGroupMessage(payload, currentTick, callbacks, out message),
                    PacketFieldFeedbackPacketKind.Whisper => TryApplyWhisper(payload, currentTick, callbacks, out message),
                    PacketFieldFeedbackPacketKind.CoupleMessage => TryApplyCoupleMessage(payload, currentTick, callbacks, out message),
                    PacketFieldFeedbackPacketKind.FieldEffect => TryApplyFieldEffect(payload, currentTick, callbacks, out message),
                    PacketFieldFeedbackPacketKind.FieldObstacleOnOff => TryApplyObstaclePacket(payload, currentTick, callbacks, batchMode: false, out message),
                    PacketFieldFeedbackPacketKind.FieldObstacleOnOffStatus => TryApplyObstaclePacket(payload, currentTick, callbacks, batchMode: true, out message),
                    PacketFieldFeedbackPacketKind.FieldObstacleAllReset => TryApplyObstacleReset(currentTick, callbacks, out message),
                    PacketFieldFeedbackPacketKind.WarnMessage => TryApplyWarnMessage(payload, callbacks, out message),
                    PacketFieldFeedbackPacketKind.PlayJukeBox => TryApplyJukeBox(payload, currentTick, callbacks, out message),
                    PacketFieldFeedbackPacketKind.TransferFieldReqIgnored => TryApplyTransferFieldIgnored(payload, currentTick, callbacks, out message),
                    PacketFieldFeedbackPacketKind.TransferChannelReqIgnored => TryApplyTransferChannelIgnored(payload, currentTick, callbacks, out message),
                    PacketFieldFeedbackPacketKind.SummonItemUnavailable => TryApplySummonItemUnavailable(payload, currentTick, callbacks, out message),
                    PacketFieldFeedbackPacketKind.DestroyClock => ApplyDestroyClock(out message),
                    PacketFieldFeedbackPacketKind.ZakumTimer => TryApplyBossTimer("Zakum", payload, currentTick, callbacks, out message),
                    PacketFieldFeedbackPacketKind.HontailTimer => TryApplyBossTimer("Horntail", payload, currentTick, callbacks, out message),
                    PacketFieldFeedbackPacketKind.ChaosZakumTimer => TryApplyBossTimer("Chaos Zakum", payload, currentTick, callbacks, out message),
                    PacketFieldFeedbackPacketKind.HontaleTimer => TryApplyBossTimer("Hontale", payload, currentTick, callbacks, out message),
                    PacketFieldFeedbackPacketKind.FieldFadeOutForce => TryApplyFieldFadeOutForce(payload, callbacks, out message),
                    _ => Unsupported(kind, out message),
                };
            }
            catch (EndOfStreamException ex)
            {
                message = $"Packet-owned field feedback payload ended early: {ex.Message}";
                return false;
            }
            catch (IOException ex)
            {
                message = $"Packet-owned field feedback payload could not be read: {ex.Message}";
                return false;
            }
        }

        internal static byte[] BuildMapleStringPayload(string text)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            WriteMapleString(writer, text);
            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildGroupMessagePayload(byte family, string sender, string message)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write(family);
            WriteMapleString(writer, sender);
            WriteMapleString(writer, message);
            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildIncomingWhisperPayload(string sender, byte channelId, bool fromAdmin, string message)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)18);
            WriteMapleString(writer, sender);
            writer.Write(channelId);
            writer.Write(fromAdmin ? (byte)1 : (byte)0);
            WriteMapleString(writer, message);
            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildWhisperResultPayload(string target, bool success)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)10);
            WriteMapleString(writer, target);
            writer.Write(success ? (byte)1 : (byte)0);
            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildWarnMessagePayload(string text)
        {
            return BuildMapleStringPayload(text);
        }

        internal static byte[] BuildObstaclePayload(string tag, int state)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            WriteMapleString(writer, tag);
            writer.Write(state);
            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildObstacleStatusPayload(IReadOnlyDictionary<string, int> states)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write(states?.Count ?? 0);
            if (states != null)
            {
                foreach ((string tag, int state) in states)
                {
                    WriteMapleString(writer, tag);
                    writer.Write(state);
                }
            }

            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildBossHpFieldEffectPayload(int mobId, int currentHp, int maxHp, byte colorCode, byte phase)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)5);
            writer.Write(mobId);
            writer.Write(currentHp);
            writer.Write(maxHp);
            writer.Write(colorCode);
            writer.Write(phase);
            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildSummonFieldEffectPayload(byte effectId, int x, int y)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)0);
            writer.Write(effectId);
            writer.Write(x);
            writer.Write(y);
            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildScreenFieldEffectPayload(string descriptor)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)3);
            WriteMapleString(writer, descriptor);
            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildRewardRouletteFieldEffectPayload(int rewardId, int step, int total)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)7);
            writer.Write(rewardId);
            writer.Write(step);
            writer.Write(total);
            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildTrembleFieldEffectPayload(byte force, int durationMs)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)1);
            writer.Write(force);
            writer.Write(durationMs);
            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildSoundFieldEffectPayload(string descriptor)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)4);
            WriteMapleString(writer, descriptor);
            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildBgmFieldEffectPayload(string descriptor)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)6);
            WriteMapleString(writer, descriptor);
            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildBossTimerPayload(byte mode, int value)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write(mode);
            writer.Write(value);
            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildFadeOutForcePayload(int fadeKey)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write(fadeKey);
            writer.Flush();
            return stream.ToArray();
        }

        private static bool Unsupported(PacketFieldFeedbackPacketKind kind, out string message)
        {
            message = $"Unsupported packet-owned field feedback kind: {kind}.";
            return false;
        }

        private bool TryApplyGroupMessage(byte[] payload, int currentTick, PacketFieldFeedbackCallbacks callbacks, out string message)
        {
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
            byte family = reader.ReadByte();
            string sender = ReadMapleString(reader);
            string body = ReadMapleString(reader);

            if (!TryResolveGroupFamily(family, out int chatLogType, out string prefix))
            {
                message = $"Unsupported group message family {family}.";
                return false;
            }

            if (ShouldSuppressBlacklistedGroupMessage(family, sender, callbacks))
            {
                _statusMessage = $"Suppressed packet-owned {prefix.Trim('[', ']')} chat from blacklisted sender {sender}.";
                message = _statusMessage;
                return true;
            }

            string text = $"{prefix} {sender}: {body}".Trim();
            callbacks?.AddClientChatMessage?.Invoke(text, chatLogType, null);
            _statusMessage = $"Applied packet-owned {prefix.Trim('[', ']')} chat from {sender}.";
            message = _statusMessage;
            return true;
        }

        private bool TryApplyWhisper(byte[] payload, int currentTick, PacketFieldFeedbackCallbacks callbacks, out string message)
        {
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
            byte subtype = reader.ReadByte();
            switch (subtype)
            {
                case 18:
                    {
                        string sender = ReadMapleString(reader);
                        byte channelId = reader.ReadByte();
                        bool fromAdmin = reader.ReadByte() != 0;
                        string body = ReadMapleString(reader);
                        if (!fromAdmin && ShouldSuppressIncomingWhisper(sender, callbacks))
                        {
                            _statusMessage = $"Suppressed packet-owned whisper from blocked sender {sender}.";
                            message = _statusMessage;
                            return true;
                        }

                        string channelText = channelId > 0
                            ? callbacks?.ResolveChannelName?.Invoke(channelId) ?? $"Ch. {channelId}"
                            : string.Empty;
                        string prefix = fromAdmin ? "[GM Whisper]" : "[Whisper]";
                        string text = string.IsNullOrWhiteSpace(channelText)
                            ? $"{prefix} {sender}: {body}"
                            : $"{prefix} {sender} ({channelText}): {body}";
                        callbacks?.AddClientChatMessage?.Invoke(text, 16, sender);
                        callbacks?.RememberWhisperTarget?.Invoke(sender);
                        _lastWhisperTarget = sender;
                        _statusMessage = $"Applied packet-owned incoming whisper from {sender}.";
                        message = _statusMessage;
                        return true;
                    }
                case 10:
                case 138:
                    {
                        string target = ReadMapleString(reader);
                        bool success = reader.ReadByte() != 0;
                        if (success)
                        {
                            callbacks?.RememberWhisperTarget?.Invoke(target);
                            _lastWhisperTarget = target;
                            _statusMessage = $"Applied packet-owned whisper target update for {target}.";
                        }
                        else
                        {
                            callbacks?.AddClientChatMessage?.Invoke($"[System] {target} is unavailable for whisper.", 12, null);
                            _statusMessage = $"Applied packet-owned whisper failure for {target}.";
                        }

                        message = _statusMessage;
                        return true;
                    }
                case 9:
                case 72:
                    {
                        string target = ReadMapleString(reader);
                        byte result = reader.ReadByte();
                        int value = reader.ReadInt32();
                        string resolved = result switch
                        {
                            1 => BuildWhisperLocationMessage(target, value, callbacks),
                            2 => $"{target} could not be found.",
                            3 => $"{target} is on {(callbacks?.ResolveChannelName?.Invoke(value) ?? $"Ch. {value}")}.",
                            4 => $"{target} cannot be followed right now.",
                            _ => $"{target} returned whisper result {result}."
                        };
                        bool queuedTransfer = false;
                        if (subtype == 9
                            && result == 1
                            && TryReadWhisperFindTransferPosition(reader, stream, out int transferX, out int transferY))
                        {
                            queuedTransfer = callbacks?.QueueMapTransfer?.Invoke(value, transferX, transferY) == true;
                        }

                        callbacks?.AddClientChatMessage?.Invoke($"[System] {resolved}", queuedTransfer ? 7 : 12, null);
                        _statusMessage = queuedTransfer
                            ? $"Applied packet-owned whisper chase response for {target} and queued map transfer."
                            : $"Applied packet-owned whisper location response for {target}.";
                        message = _statusMessage;
                        return true;
                    }
                case 34:
                    {
                        string target = ReadMapleString(reader);
                        bool available = reader.ReadByte() != 0;
                        string text = available
                            ? $"[System] {target.ToLowerInvariant()} is available for whisper."
                            : $"[System] {target} is no longer available for whisper.";
                        callbacks?.AddClientChatMessage?.Invoke(text, 12, available ? target : null);
                        _statusMessage = $"Applied packet-owned whisper availability notice for {target}.";
                        message = _statusMessage;
                        return true;
                    }
                default:
                    message = $"Unsupported whisper subtype {subtype}.";
                    return false;
            }
        }

        private bool TryApplyCoupleMessage(byte[] payload, int currentTick, PacketFieldFeedbackCallbacks callbacks, out string message)
        {
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
            byte subtype = reader.ReadByte();
            switch (subtype)
            {
                case 4:
                    {
                        bool hasMessage = reader.ReadByte() != 0;
                        if (!hasMessage)
                        {
                            callbacks?.AddClientChatMessage?.Invoke("[System] Couple notice is unavailable.", 12, null);
                            _statusMessage = "Applied packet-owned empty couple notice.";
                            message = _statusMessage;
                            return true;
                        }

                        string body = ReadMapleString(reader);
                        callbacks?.AddClientChatMessage?.Invoke($"[Couple] {body}", 6, null);
                        _statusMessage = "Applied packet-owned couple notice.";
                        message = _statusMessage;
                        return true;
                    }
                case 5:
                    {
                        string sender = ReadMapleString(reader);
                        reader.ReadByte();
                        string body = ReadMapleString(reader);
                        callbacks?.AddClientChatMessage?.Invoke($"[Couple] {sender}: {body}", 6, null);
                        _statusMessage = $"Applied packet-owned couple chat from {sender}.";
                        message = _statusMessage;
                        return true;
                    }
                default:
                    message = $"Unsupported couple-message subtype {subtype}.";
                    return false;
            }
        }

        private bool TryApplyFieldEffect(byte[] payload, int currentTick, PacketFieldFeedbackCallbacks callbacks, out string message)
        {
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
            byte effectType = reader.ReadByte();
            switch (effectType)
            {
                case 0:
                    {
                        byte effectId = reader.ReadByte();
                        int x = reader.ReadInt32();
                        int y = reader.ReadInt32();
                        bool shown = callbacks?.ShowSummonEffectVisual?.Invoke(effectId, x, y) == true;
                        _lastFieldEffectSummary = $"summon effect #{effectId} at ({x}, {y})";
                        _statusMessage = shown
                            ? $"Applied packet-owned summon effect #{effectId}."
                            : $"Applied packet-owned summon effect #{effectId} without a resolved visual.";
                        message = _statusMessage;
                        return true;
                    }
                case 1:
                    {
                        byte force = reader.ReadByte();
                        int durationMs = reader.ReadInt32();
                        callbacks?.TriggerTremble?.Invoke(force, durationMs);
                        _lastFieldEffectSummary = $"tremble force={force} duration={durationMs}ms";
                        _statusMessage = "Applied packet-owned field tremble.";
                        message = _statusMessage;
                        return true;
                    }
                case 2:
                    {
                        string tag = ReadMapleString(reader);
                        callbacks?.SetObjectTagState?.Invoke(tag, null, 0, currentTick);
                        _lastFieldEffectSummary = $"object-state push for '{tag}'";
                        _statusMessage = "Applied packet-owned field object-state push.";
                        message = _statusMessage;
                        return true;
                    }
                case 3:
                    {
                        string descriptor = ReadMapleString(reader);
                        bool shown = callbacks?.ShowScreenEffectVisual?.Invoke(descriptor) == true;
                        _lastFieldEffectSummary = $"screen effect '{descriptor}'";
                        if (!shown)
                        {
                            callbacks?.ShowUtilityFeedback?.Invoke($"Packet-owned screen effect: {descriptor}");
                        }

                        _statusMessage = shown
                            ? "Applied packet-owned screen effect."
                            : "Applied packet-owned screen effect without a resolved visual.";
                        message = _statusMessage;
                        return true;
                    }
                case 4:
                    {
                        string descriptor = ReadMapleString(reader);
                        bool played = callbacks?.PlayFieldSound?.Invoke(descriptor) == true;
                        _lastFieldSoundDescriptor = descriptor;
                        _lastFieldEffectSummary = $"field sound '{descriptor}'";
                        _statusMessage = played
                            ? $"Applied packet-owned field sound {descriptor}."
                            : $"Field sound {descriptor} could not be resolved.";
                        message = _statusMessage;
                        return played;
                    }
                case 5:
                    {
                        int mobId = reader.ReadInt32();
                        int currentHp = reader.ReadInt32();
                        int maxHp = reader.ReadInt32();
                        byte colorCode = reader.ReadByte();
                        byte phase = reader.ReadByte();
                        _bossHpState = new BossHpState(
                            mobId,
                            callbacks?.ResolveMobName?.Invoke(mobId) ?? $"Mob {mobId}",
                            Math.Max(0, currentHp),
                            Math.Max(1, maxHp),
                            colorCode,
                            phase,
                            currentTick + BossHpDisplayDurationMs);
                        _lastFieldEffectSummary = $"boss hp tag {_bossHpState.Name} {_bossHpState.CurrentHp}/{_bossHpState.MaxHp}";
                        _statusMessage = $"Applied packet-owned boss HP tag for {_bossHpState.Name}.";
                        message = _statusMessage;
                        return true;
                    }
                case 6:
                    {
                        string descriptor = ReadMapleString(reader);
                        callbacks?.RequestBgm?.Invoke(descriptor);
                        _lastBgmDescriptor = descriptor;
                        _lastFieldEffectSummary = $"bgm '{descriptor}'";
                        _statusMessage = $"Applied packet-owned field BGM swap to {descriptor}.";
                        message = _statusMessage;
                        return true;
                    }
                case 7:
                    {
                        int rewardId = reader.ReadInt32();
                        int step = reader.ReadInt32();
                        int total = reader.ReadInt32();
                        bool shown = callbacks?.ShowRewardRouletteVisual?.Invoke(rewardId, step, total) == true;
                        _lastFieldEffectSummary = $"reward roulette reward={rewardId} step={step} total={total}";
                        _statusMessage = shown
                            ? "Applied packet-owned reward roulette state."
                            : "Applied packet-owned reward roulette state without a resolved visual.";
                        message = _statusMessage;
                        return true;
                    }
                default:
                    message = $"Unsupported field-effect type {effectType}.";
                    return false;
            }
        }

        private bool TryApplyObstaclePacket(byte[] payload, int currentTick, PacketFieldFeedbackCallbacks callbacks, bool batchMode, out string message)
        {
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
            int appliedCount = 0;

            if (!batchMode)
            {
                string tag = ReadMapleString(reader);
                int state = reader.ReadInt32();
                ApplyObstacleState(tag, state != 0, currentTick, callbacks);
                appliedCount = 1;
            }
            else
            {
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    string tag = ReadMapleString(reader);
                    int state = reader.ReadInt32();
                    ApplyObstacleState(tag, state != 0, currentTick, callbacks);
                    appliedCount++;
                }
            }

            _statusMessage = appliedCount == 1
                ? "Applied packet-owned obstacle state."
                : $"Applied {appliedCount} packet-owned obstacle states.";
            message = _statusMessage;
            return appliedCount > 0;
        }

        private bool TryApplyObstacleReset(int currentTick, PacketFieldFeedbackCallbacks callbacks, out string message)
        {
            foreach (string tag in _obstacleStates.Keys.ToList())
            {
                callbacks?.SetObjectTagState?.Invoke(tag, false, 0, currentTick);
                _obstacleStates[tag] = false;
            }

            _statusMessage = _obstacleStates.Count == 0
                ? "No packet-owned obstacle states were active."
                : $"Reset {_obstacleStates.Count} packet-owned obstacle state(s).";
            message = _statusMessage;
            return true;
        }

        private bool TryApplyWarnMessage(byte[] payload, PacketFieldFeedbackCallbacks callbacks, out string message)
        {
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
            string text = ReadMapleString(reader);
            _lastWarningMessage = text;
            callbacks?.ShowModalWarning?.Invoke(text);
            _statusMessage = "Applied packet-owned warning dialog.";
            message = _statusMessage;
            return true;
        }

        private bool TryApplyJukeBox(byte[] payload, int currentTick, PacketFieldFeedbackCallbacks callbacks, out string message)
        {
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
            int itemId = reader.ReadInt32();
            string owner = ReadMapleString(reader);
            string itemName = callbacks?.ResolveItemName?.Invoke(itemId) ?? $"Item {itemId}";
            string text = $"[System] {owner} played {itemName} through the field jukebox.";
            callbacks?.AddClientChatMessage?.Invoke(text, 12, null);
            _lastJukeboxSummary = $"{owner} -> {itemName}";
            _statusMessage = $"Applied packet-owned jukebox request for {itemName}.";
            message = _statusMessage;
            return true;
        }

        private bool TryApplyTransferFieldIgnored(byte[] payload, int currentTick, PacketFieldFeedbackCallbacks callbacks, out string message)
        {
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
            byte reason = reader.ReadByte();
            string text = reason switch
            {
                1 => "Another field transfer is already pending.",
                2 => "This map cannot be entered right now.",
                3 or 5 => "The requested field transfer is unavailable.",
                4 => "This field transfer was blocked by a packet-owned client notice.",
                6 => "The current field rules block map transfer right now.",
                7 => "The transfer portal is not ready yet.",
                8 => "The transfer request was ignored by the client warning path.",
                _ => $"Field transfer request was ignored with reason {reason}."
            };
            _lastTransferFailureMessage = text;
            callbacks?.AddClientChatMessage?.Invoke($"[System] {text}", 12, null);
            if (reason is 4 or 8)
            {
                callbacks?.ShowModalWarning?.Invoke(text);
            }

            _statusMessage = "Applied packet-owned transfer-field failure feedback.";
            message = _statusMessage;
            return true;
        }

        private bool TryApplyTransferChannelIgnored(byte[] payload, int currentTick, PacketFieldFeedbackCallbacks callbacks, out string message)
        {
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
            byte reason = reader.ReadByte();
            string text = reason switch
            {
                1 => "Another channel transfer is already pending.",
                2 => "The selected channel is unavailable.",
                3 => "The current field blocks channel change.",
                4 => "The selected channel rejected the transfer.",
                5 => "Channel change is unavailable right now.",
                _ => $"Channel transfer request was ignored with reason {reason}."
            };
            _lastTransferFailureMessage = text;
            callbacks?.AddClientChatMessage?.Invoke($"[System] {text}", 12, null);
            _statusMessage = "Applied packet-owned transfer-channel failure feedback.";
            message = _statusMessage;
            return true;
        }

        private bool TryApplySummonItemUnavailable(byte[] payload, int currentTick, PacketFieldFeedbackCallbacks callbacks, out string message)
        {
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
            bool ignored = reader.ReadByte() != 0;
            string text = ignored
                ? "Summon item use was ignored."
                : "The summon item cannot be used in this field.";
            callbacks?.AddClientChatMessage?.Invoke($"[System] {text}", 12, null);
            if (!ignored)
            {
                callbacks?.ShowModalWarning?.Invoke(text);
            }

            _statusMessage = "Applied packet-owned summon-item availability feedback.";
            message = _statusMessage;
            return true;
        }

        private bool ApplyDestroyClock(out string message)
        {
            _lastBossTimerSummary = "destroyed";
            _statusMessage = "Applied packet-owned destroy-clock teardown.";
            message = _statusMessage;
            return true;
        }

        private bool TryApplyBossTimer(string bossName, byte[] payload, int currentTick, PacketFieldFeedbackCallbacks callbacks, out string message)
        {
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
            byte mode = reader.ReadByte();
            int value = reader.ReadInt32();
            string text = mode switch
            {
                0 when value > 0 => $"{bossName} timer update: {value} remaining.",
                0 => $"{bossName} timer expired.",
                1 => $"{bossName} warning stage {value}.",
                2 => $"{bossName} emergency stage {value}.",
                _ => $"{bossName} timer packet mode {mode} value {value}."
            };
            callbacks?.AddClientChatMessage?.Invoke($"[System] {text}", 12, null);
            _lastBossTimerSummary = text;
            _statusMessage = $"Applied packet-owned {bossName} timer feedback.";
            message = _statusMessage;
            return true;
        }

        private bool TryApplyFieldFadeOutForce(byte[] payload, PacketFieldFeedbackCallbacks callbacks, out string message)
        {
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
            int fadeKey = reader.ReadInt32();
            callbacks?.ClearFieldFade?.Invoke();
            _statusMessage = $"Applied packet-owned forced fade teardown for key {fadeKey}.";
            message = _statusMessage;
            return true;
        }

        private void ApplyObstacleState(string tag, bool isEnabled, int currentTick, PacketFieldFeedbackCallbacks callbacks)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return;
            }

            _obstacleStates[tag] = isEnabled;
            callbacks?.SetObjectTagState?.Invoke(tag, isEnabled, 0, currentTick);
        }

        private static string BuildWhisperLocationMessage(string target, int value, PacketFieldFeedbackCallbacks callbacks)
        {
            string mapName = callbacks?.ResolveMapName?.Invoke(value);
            return string.IsNullOrWhiteSpace(mapName)
                ? $"{target} is in map {value}."
                : $"{target} is in {mapName}.";
        }

        private static bool TryResolveGroupFamily(byte family, out int chatLogType, out string prefix)
        {
            (chatLogType, prefix) = family switch
            {
                0 => (3, "[Friend]"),
                1 => (2, "[Party]"),
                2 => (4, "[Guild]"),
                3 => (5, "[Association]"),
                6 => (26, "[Expedition]"),
                _ => (-1, string.Empty)
            };
            return chatLogType >= 0;
        }

        private static bool ShouldSuppressBlacklistedGroupMessage(byte family, string sender, PacketFieldFeedbackCallbacks callbacks)
        {
            if (string.IsNullOrWhiteSpace(sender) || callbacks?.IsBlacklistedName == null)
            {
                return false;
            }

            return family switch
            {
                0 or 2 or 3 or 6 => callbacks.IsBlacklistedName(sender),
                _ => false
            };
        }

        private static bool ShouldSuppressIncomingWhisper(string sender, PacketFieldFeedbackCallbacks callbacks)
        {
            if (string.IsNullOrWhiteSpace(sender))
            {
                return false;
            }

            return callbacks?.IsBlacklistedName?.Invoke(sender) == true
                || callbacks?.IsBlockedFriendName?.Invoke(sender) == true;
        }

        private static bool TryReadWhisperFindTransferPosition(BinaryReader reader, Stream stream, out int x, out int y)
        {
            x = 0;
            y = 0;
            if (stream == null || stream.Length - stream.Position < (sizeof(int) * 2))
            {
                return false;
            }

            x = reader.ReadInt32();
            y = reader.ReadInt32();
            return true;
        }

        private void DrawBossHp(SpriteBatch spriteBatch, SpriteFont font, int renderWidth, int currentTick)
        {
            if (_bossHpState == null)
            {
                return;
            }

            float alpha = MathHelper.Clamp((_bossHpState.ExpiresAtTick - currentTick) / (float)BossHpDisplayDurationMs, 0f, 1f);
            if (alpha <= 0f)
            {
                return;
            }

            Rectangle frameBounds = new(
                Math.Max(0, (renderWidth - (BossHpBarWidth + (BossHpFramePadding * 2))) / 2),
                56,
                BossHpBarWidth + (BossHpFramePadding * 2),
                44);
            Rectangle barBounds = new(
                frameBounds.X + BossHpFramePadding,
                frameBounds.Y + 20,
                BossHpBarWidth,
                BossHpBarHeight);
            int fillWidth = _bossHpState.MaxHp <= 0
                ? 0
                : (int)Math.Round(Math.Clamp(_bossHpState.CurrentHp / (float)_bossHpState.MaxHp, 0f, 1f) * barBounds.Width);

            Color frameColor = new Color(14, 18, 28, 220) * alpha;
            Color borderColor = new Color(227, 205, 110) * alpha;
            Color fillColor = ResolveBossHpColor(_bossHpState.ColorCode) * alpha;
            Color backColor = new Color(54, 17, 17, 220) * alpha;
            Color textColor = Color.White * alpha;

            spriteBatch.Draw(_pixelTexture, frameBounds, frameColor);
            DrawBorder(spriteBatch, frameBounds, borderColor);
            spriteBatch.Draw(_pixelTexture, barBounds, backColor);
            if (fillWidth > 0)
            {
                spriteBatch.Draw(_pixelTexture, new Rectangle(barBounds.X, barBounds.Y, fillWidth, barBounds.Height), fillColor);
            }

            string title = _bossHpState.Phase > 0
                ? $"{_bossHpState.Name} P{_bossHpState.Phase}"
                : _bossHpState.Name;
            string hpText = string.Format(CultureInfo.InvariantCulture, "{0}/{1}", _bossHpState.CurrentHp, _bossHpState.MaxHp);
            spriteBatch.DrawString(font, title, new Vector2(frameBounds.X + 8, frameBounds.Y + 4), textColor, 0f, Vector2.Zero, 0.54f, SpriteEffects.None, 0f);
            Vector2 hpTextSize = font.MeasureString(hpText) * 0.48f;
            spriteBatch.DrawString(
                font,
                hpText,
                new Vector2(frameBounds.Right - hpTextSize.X - 8, frameBounds.Y + 22),
                textColor,
                0f,
                Vector2.Zero,
                0.48f,
                SpriteEffects.None,
                0f);
        }

        private void DrawBorder(SpriteBatch spriteBatch, Rectangle bounds, Color color)
        {
            spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), color);
            spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), color);
            spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), color);
            spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), color);
        }

        private static Color ResolveBossHpColor(byte colorCode)
        {
            return colorCode switch
            {
                1 => new Color(255, 88, 88),
                2 => new Color(255, 180, 72),
                3 => new Color(127, 206, 255),
                4 => new Color(194, 132, 255),
                _ => new Color(255, 76, 76)
            };
        }

        private static string TrimForStatus(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string compact = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return compact.Length <= 72 ? compact : $"{compact[..69]}...";
        }

        private static string ReadMapleString(BinaryReader reader)
        {
            ushort length = reader.ReadUInt16();
            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
            {
                throw new EndOfStreamException("Packet string ended before the declared Maple string length.");
            }

            return Encoding.Default.GetString(bytes);
        }

        private static void WriteMapleString(BinaryWriter writer, string text)
        {
            byte[] bytes = Encoding.Default.GetBytes(text ?? string.Empty);
            writer.Write((ushort)bytes.Length);
            writer.Write(bytes);
        }

        private sealed record BossHpState(
            int MobId,
            string Name,
            int CurrentHp,
            int MaxHp,
            byte ColorCode,
            byte Phase,
            int ExpiresAtTick);
    }
}
