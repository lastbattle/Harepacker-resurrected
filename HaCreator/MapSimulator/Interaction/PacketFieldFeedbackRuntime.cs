using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using BinaryReader = MapleLib.PacketLib.PacketReader;
using BinaryWriter = MapleLib.PacketLib.PacketWriter;
namespace HaCreator.MapSimulator.Interaction
{
    internal enum PacketFieldFeedbackPacketKind
    {
        Clock = 1000,
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
        internal Func<int> GetCurrentChannelId { get; init; }
        internal Func<string> GetLastOutgoingWhisperText { get; init; }
        internal Func<string> GetLastOutgoingWhisperTarget { get; init; }
        internal Action<int, int> TriggerTremble { get; init; }
        internal Action ClearFieldFade { get; init; }
        internal Action<string> RequestBgm { get; init; }
        internal Func<string, bool> PlayFieldSound { get; init; }
        internal Func<byte, int, int, bool> PlaySummonEffectSound { get; init; }
        internal Func<string, bool?, int, int?, bool> SetObjectTagState { get; init; }
        internal Func<byte, int, int, bool> ShowSummonEffectVisual { get; init; }
        internal Func<string, bool> ShowScreenEffectVisual { get; init; }
        internal Func<int, int, int, bool> ShowRewardRouletteVisual { get; init; }
        internal Func<int, string> ResolveMobName { get; init; }
        internal Func<int, int?> ResolveMobMaxHp { get; init; }
        internal Func<int, string> ResolveMapName { get; init; }
        internal Func<int, bool> HasMapTransferTarget { get; init; }
        internal Func<int, string> ResolveItemName { get; init; }
        internal Func<int, string> ResolveChannelName { get; init; }
        internal Func<string, bool> IsBlacklistedName { get; init; }
        internal Func<string, bool> IsBlockedFriendName { get; init; }
        internal Func<bool> IsUnderCover { get; init; }
        internal Func<int, int, int, bool> QueueMapTransfer { get; init; }
        internal Func<bool> ConsumeWhisperChaseTransferRequest { get; init; }
        internal Action<string, string, byte, int> UpdateWhisperUserListLocation { get; init; }
        internal Action<string> ShowBlowWeatherMessage { get; init; }
        internal Func<IReadOnlyList<PacketFieldSwindleWarningEntry>> ResolveSwindleWarnings { get; init; }
        internal Action<PacketFieldBossTimerVisualState> ShowBossTimerClock { get; init; }
        internal Action ClearBossTimerClock { get; init; }
        internal Action<PacketFieldClockVisualState> ShowFieldClock { get; init; }
        internal Action ClearFieldClock { get; init; }
        internal Func<bool> RestoreFieldPropertyClock { get; init; }
        internal Action InvalidateWhisperUserListWindow { get; init; }
    }

    internal sealed record PacketFieldSwindleWarningEntry(
        int GroupId,
        IReadOnlyList<string> Keywords,
        IReadOnlyList<string> WarningTexts);

    internal sealed record PacketFieldBossTimerVisualState(
        string TimerKey,
        string Label,
        int DurationSeconds,
        int StartedAtTick);

    internal enum PacketFieldClockVisualKind
    {
        Realtime,
        Countdown
    }

    internal enum PacketFieldClockVisualVariant
    {
        Default,
        FieldProperty,
        Event,
        CakePieSmall,
        CakePieLarge
    }

    internal sealed record PacketFieldClockVisualBounds(
        int X,
        int Y,
        int Width,
        int Height);

    internal sealed record PacketFieldClockVisualState(
        PacketFieldClockVisualKind Kind,
        PacketFieldClockVisualVariant Variant,
        int StartedAtTick,
        int DurationSeconds,
        bool IsPm,
        int Hour,
        int Minute,
        int Second,
        string Label,
        PacketFieldClockVisualBounds Bounds = null);

    internal sealed class PacketFieldFeedbackRuntime
    {
        private const int BossHpDisplayDurationMs = 10000;
        private const int BossHpBarWidth = 288;
        private const int BossHpBarHeight = 16;
        private const int BossHpFramePadding = 4;
        private const int ZakumTimerBeforeStringPoolId = 0x107E;
        private const int ZakumTimerWarningStringPoolId = 0x107F;
        private const int ZakumTimerExpiredStringPoolId = 0x1080;
        private const int HorntailTimerBeforeStringPoolId = 0x1081;
        private const int HorntailTimerWarningStringPoolId = 0x1082;
        private const int HorntailTimerExpiredStringPoolId = 0x1083;
        private const int HontaleTimerStageTwoStringPoolId = 0x16EF;
        private const int HontaleTimerStageOneStringPoolId = 0x16F0;
        private const int HontaleTimerStageZeroStringPoolId = 0x16F1;
        private const int WhisperLocationUnavailableStringPoolId = 0x9A;
        private const int WhisperSentStringPoolId = 0x9F;
        private const int WhisperChannelStringPoolId = 0x9B;
        private const int WhisperNotFoundStringPoolId = 0x9C;
        private const int WhisperLocationStringPoolId = 0x9D;
        private const int WhisperHiddenFieldStringPoolId = 0x9E;
        private const int WhisperUserListFormatStringPoolId = 0x2D7;
        private const int WhisperUserListNotFoundStringPoolId = 0x1A2D;
        private const int WhisperUserListHiddenFieldStringPoolId = 0x18E0;
        private const int WhisperUnderCoverMapIdSuffixStringPoolId = 0x731;
        private const int IncomingWhisperSameChannelStringPoolId = 0x72E;
        private const int IncomingWhisperOtherChannelStringPoolId = 0x72F;
        private const int OutgoingWhisperLogStringPoolId = 0x730;
        private const int CoupleSharedChatStringPoolId = 0x72D;
        private const int CoupleNoticeUnavailableStringPoolId = 0xA1;
        private const int JukeBoxMessageStringPoolId = 0x1AC3;
        private const int TransferFieldIgnoredPortalClosedStringPoolId = 0x181;
        private const int TransferFieldIgnoredUnavailableStringPoolId = 0xBD3;
        private const int TransferFieldIgnoredBlockedStringPoolId = 0x1A83;
        private const int TransferFieldIgnoredModalBlockedStringPoolId = 0xBD4;
        private const int TransferFieldIgnoredRuleBlockedStringPoolId = 0x155B;
        private const int TransferFieldIgnoredPortalNotReadyStringPoolId = 0x168B;
        private const int TransferFieldIgnoredWarningModalStringPoolId = 0xBEF;
        private const int TransferChannelIgnoredPendingStringPoolId = 0xD30;
        private const int TransferChannelIgnoredUnavailableStringPoolId = 0xD31;
        private const int TransferChannelIgnoredFieldBlockedStringPoolId = 0x1299;
        private const int TransferChannelIgnoredRejectedStringPoolId = 0x12DA;
        private const int TransferChannelIgnoredDelayedStringPoolId = 0x12DC;
        private const int SummonItemUnavailableNoticeStringPoolId = 0x121;
        private const string ZakumTimerBeforeFallback = "The Zakum Shrine will close if you do not summon Zakum in {0} minutes.";
        private const string ZakumTimerWarningFallback = "The Zakum Shrine will close in {0} minutes.";
        private const string ZakumTimerExpiredFallback = "The Zakum Shrine has closed.";
        private const string HorntailTimerBeforeFallback = "The Horntail's Cave will close if you do not summon Horntail in {0} minutes.";
        private const string HorntailTimerWarningFallback = "The Horntail's Cave will close in {0} minutes.";
        private const string HorntailTimerExpiredFallback = "The Horntail's Cave has closed.";
        private const string HontaleTimerStageTwoFallback = "The Horntail Expedition has ended.";
        private const string HontaleTimerStageOneFallback = "The Horntail Expedition will end in {0} min(s).";
        private const string HontaleTimerStageZeroFallback = "The Horntail Expedition will end if it does not start within {0} min(s) of entering.";
        private const string WhisperLocationUnavailableFallback = "{0} cannot be followed right now.";
        private const string WhisperDisabledFallback = "{0} have currently disabled whispers.";
        private const string WhisperSentFallback = "You have whispered to '{0}'";
        private const string WhisperChannelFallback = "{0} is on {1}.";
        private const string WhisperNotFoundFallback = "{0} could not be found.";
        private const string WhisperLocationFallback = "{0} is in {1}.";
        private const string WhisperHiddenFieldFallback = "{0} is in a hidden field.";
        private const string WhisperUserListEntryFallback = "{0}: {1}";
        private const string WhisperUserListNotFoundFallback = "Not found.";
        private const string WhisperUserListHiddenFieldFallback = "Hidden field.";
        private const string WhisperUnderCoverMapIdSuffixFallback = " ({0:000000000})";
        private const string IncomingWhisperSameChannelFallback = "{0}: {1}";
        private const string IncomingWhisperOtherChannelFallback = "{0} ({1}): {2}";
        private const string OutgoingWhisperLogFallback = "> {0}: {1}";
        private const string CoupleSharedChatFallback = "{0}: {1}";
        private const string CoupleNoticeUnavailableFallback = "Couple notice is unavailable.";
        private const string JukeBoxMessageFallback = "{0} played {1} through the field jukebox.";
        private const string TransferFieldIgnoredPortalClosedFallback = "The portal is closed for now.";
        private const string TransferFieldIgnoredUnavailableFallback = "You cannot go to that place.";
        private const string TransferFieldIgnoredBlockedFallback = "The requested field transfer is unavailable.";
        private const string TransferFieldIgnoredModalBlockedFallback = "This map cannot be entered right now.";
        private const string TransferFieldIgnoredRuleBlockedFallback = "The current field rules block map transfer right now.";
        private const string TransferFieldIgnoredPortalNotReadyFallback = "The transfer portal is not ready yet.";
        private const string TransferFieldIgnoredWarningModalFallback = "The transfer request was ignored by the client warning path.";
        private const string TransferChannelIgnoredPendingFallback = "Another channel transfer is already pending.";
        private const string TransferChannelIgnoredUnavailableFallback = "The selected channel is unavailable.";
        private const string TransferChannelIgnoredFieldBlockedFallback = "The current field blocks channel change.";
        private const string TransferChannelIgnoredRejectedFallback = "The selected channel rejected the transfer.";
        private const string TransferChannelIgnoredDelayedFallback = "Channel change is unavailable right now.";
        private const string SummonItemUnavailableNoticeFallback = "The summon item cannot be used in this field.";
        private const int HorntailBossHpFirstBodyMobId = 8810118;
        private const int HorntailBossHpLastBodyMobId = 8810122;
        private static readonly uint SwindleCodePage = ResolveSwindleCodePage();
        private static readonly Encoding SwindleEncoding = ResolveSwindleEncoding();
        // Recovered from CCurseProcess::s_FilterChars in the v95 client.
        private static readonly byte[] SwindleFilteredCharacters =
        {
            0x20, // space
            0x5F, // _
            0x2D, // -
            0x3A, // :
            0x09, // \t
            0x5E, // ^
            0x2E, // .
            0x2C, // ,
            0x2A, // *
            0x2F, // /
            0x3B, // ;
            0x21, // !
            0x5C, // \
            0x27, // '
            0x22, // "
            0x60, // `
            0x2B  // +
        };

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
        private PacketFieldBossTimerVisualState _bossTimerVisualState;
        private string _lastFieldClockSummary = string.Empty;
        private PacketFieldClockVisualState _clockVisualState;
        private bool _fieldClockEventFlag;
        private int _nextSwindleWarningTick;
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
            _bossTimerVisualState = null;
            _lastFieldClockSummary = string.Empty;
            _clockVisualState = null;
            _fieldClockEventFlag = false;
            _nextSwindleWarningTick = 0;
        }

        internal void Update(int currentTick)
        {
            if (_bossHpState != null && currentTick >= _bossHpState.ExpiresAtTick)
            {
                _bossHpState = null;
            }

            if (_bossTimerVisualState != null
                && GetBossTimerRemainingSeconds(_bossTimerVisualState, currentTick) <= 0)
            {
                _bossTimerVisualState = null;
            }

            if (_clockVisualState != null
                && _clockVisualState.Kind == PacketFieldClockVisualKind.Countdown
                && GetFieldClockRemainingSeconds(_clockVisualState, currentTick) <= 0)
            {
                ClearFieldClock(callbacks: null);
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
            string clockStatus = string.IsNullOrWhiteSpace(_lastFieldClockSummary)
                ? "clock=none"
                : $"clock=\"{TrimForStatus(_lastFieldClockSummary)}\"";

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
                clockStatus,
                obstacleStatus,
                bossHpStatus);
        }

        internal bool RestoreFieldPropertyClock(
            int x,
            int y,
            int width,
            int height,
            byte hour,
            byte minute,
            byte second,
            int currentTick,
            PacketFieldFeedbackCallbacks callbacks,
            out string message)
        {
            if (x == 0 || y == 0 || width == 0 || height == 0)
            {
                message = "Map-authored clock property is missing one of x, y, width, or height.";
                return false;
            }

            PacketFieldClockVisualState baseState = CreateRealtimeClockVisualState(hour, minute, second, currentTick);
            PacketFieldClockVisualState state = baseState with
            {
                Variant = PacketFieldClockVisualVariant.FieldProperty,
                Label = "field-property clock",
                Bounds = new PacketFieldClockVisualBounds(x, y, width, height)
            };
            _clockVisualState = state;
            _fieldClockEventFlag = false;
            _lastFieldClockSummary = string.Format(
                CultureInfo.InvariantCulture,
                "field-property clock ({0},{1},{2},{3}) {4} {5:00}:{6:00}:{7:00}",
                x,
                y,
                width,
                height,
                state.IsPm ? "PM" : "AM",
                state.Hour,
                state.Minute,
                state.Second);
            callbacks?.ShowFieldClock?.Invoke(state);
            _statusMessage = "Restored packet-owned field-property clock.";
            message = _statusMessage;
            return true;
        }

        internal void ClearFieldClockForMapLoad(PacketFieldFeedbackCallbacks callbacks)
        {
            ClearFieldClock(callbacks);
            _statusMessage = "Cleared packet-owned field clock for map load.";
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
                    PacketFieldFeedbackPacketKind.Clock => TryApplyClock(payload, currentTick, callbacks, out message),
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
                    PacketFieldFeedbackPacketKind.DestroyClock => ApplyDestroyClock(callbacks, out message),
                    PacketFieldFeedbackPacketKind.ZakumTimer => TryApplyBossTimer(
                        "Zakum",
                        payload,
                        currentTick,
                        callbacks,
                        ZakumTimerBeforeStringPoolId,
                        ZakumTimerWarningStringPoolId,
                        ZakumTimerExpiredStringPoolId,
                        ZakumTimerBeforeFallback,
                        ZakumTimerWarningFallback,
                        ZakumTimerExpiredFallback,
                        out message),
                    PacketFieldFeedbackPacketKind.HontailTimer => TryApplyBossTimer(
                        "Horntail",
                        payload,
                        currentTick,
                        callbacks,
                        HorntailTimerBeforeStringPoolId,
                        HorntailTimerWarningStringPoolId,
                        HorntailTimerExpiredStringPoolId,
                        HorntailTimerBeforeFallback,
                        HorntailTimerWarningFallback,
                        HorntailTimerExpiredFallback,
                        out message),
                    PacketFieldFeedbackPacketKind.ChaosZakumTimer => TryApplyBossTimer(
                        "Chaos Zakum",
                        payload,
                        currentTick,
                        callbacks,
                        ZakumTimerBeforeStringPoolId,
                        ZakumTimerWarningStringPoolId,
                        ZakumTimerExpiredStringPoolId,
                        ZakumTimerBeforeFallback,
                        ZakumTimerWarningFallback,
                        ZakumTimerExpiredFallback,
                        out message),
                    PacketFieldFeedbackPacketKind.HontaleTimer => TryApplyHontaleTimer(payload, currentTick, callbacks, out message),
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

        internal static byte[] BuildWhisperBlowWeatherPayload(string sender, byte blowType, string message)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)146);
            WriteMapleString(writer, sender);
            writer.Write(blowType);
            WriteMapleString(writer, message);
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

        internal static byte[] BuildClockRealtimePayload(byte hour, byte minute, byte second)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)1);
            writer.Write(hour);
            writer.Write(minute);
            writer.Write(second);
            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildClockCountdownPayload(int durationSeconds)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)2);
            writer.Write(durationSeconds);
            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildClockEventCountdownPayload(bool show, int durationSeconds)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)3);
            writer.Write(show ? (byte)1 : (byte)0);
            if (show)
            {
                writer.Write(durationSeconds);
            }

            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildClockCakePiePayload(bool show, byte boardType, int durationSeconds)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)100);
            writer.Write(show ? (byte)1 : (byte)0);
            if (show)
            {
                writer.Write(boardType);
                writer.Write(durationSeconds);
            }

            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildHontaleTimerPayload(byte mode, byte value)
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
            string body = NormalizeFieldChatText(ReadMapleString(reader));

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

            string text = FormatFieldFeedbackStringPoolText(
                CoupleSharedChatStringPoolId,
                CoupleSharedChatFallback,
                sender,
                body);
            callbacks?.AddClientChatMessage?.Invoke(text, chatLogType, null);
            TryAddSwindleWarning(body, family == 1, currentTick, callbacks);
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
                            callbacks?.RememberWhisperTarget?.Invoke(sender);
                            _lastWhisperTarget = sender;
                            _statusMessage = $"Suppressed packet-owned whisper from blocked sender {sender}.";
                            message = _statusMessage;
                            return true;
                        }

                        body = NormalizeFieldChatText(body);
                        string text = ResolveIncomingWhisperLogText(sender, channelId, body, callbacks);
                        callbacks?.AddClientChatMessage?.Invoke(text, 16, sender);
                        TryAddSwindleWarning(body, allowGroupFamilyWarning: true, currentTick, callbacks);
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
                        TryAddOutgoingWhisperEcho(target, callbacks);
                        if (!success)
                        {
                            string warning = FormatFieldFeedbackStringPoolText(
                                WhisperLocationUnavailableStringPoolId,
                                WhisperDisabledFallback,
                                target.Trim().ToLowerInvariant());
                            callbacks?.AddClientChatMessage?.Invoke(warning, 12, null);
                            _statusMessage = $"Applied packet-owned whisper failure for {target}.";
                            message = _statusMessage;
                            return true;
                        }

                        _statusMessage = $"Applied packet-owned whisper send-result update for {target}.";
                        message = _statusMessage;
                        return true;
                    }
                case 9:
                case 72:
                    {
                        string target = ReadMapleString(reader);
                        byte result = reader.ReadByte();
                        int value = reader.ReadInt32();
                        bool hiddenFieldResult = subtype == 9
                            && result == 1
                            && IsWhisperHiddenField(value, callbacks);
                        TryBuildWhisperFindMessage(subtype, target, result, value, callbacks, out string resolved);
                        bool chaseTransferArmed = subtype == 9
                            && callbacks?.ConsumeWhisperChaseTransferRequest?.Invoke() == true;
                        bool queuedTransfer = false;
                        if (subtype == 9
                            && result == 1
                            && !hiddenFieldResult
                            && chaseTransferArmed
                            && HasWhisperTransferTarget(value, callbacks)
                            && TryReadWhisperFindTransferPosition(reader, stream, out int transferX, out int transferY))
                        {
                            queuedTransfer = callbacks?.QueueMapTransfer?.Invoke(value, transferX, transferY) == true;
                        }

                        if (subtype == 72)
                        {
                            callbacks?.UpdateWhisperUserListLocation?.Invoke(target, resolved, result, value);
                            callbacks?.InvalidateWhisperUserListWindow?.Invoke();
                            _statusMessage = $"Updated packet-owned whisper find-reply for {target}.";
                            message = _statusMessage;
                            return true;
                        }

                        if (!string.IsNullOrWhiteSpace(resolved))
                        {
                            callbacks?.AddClientChatMessage?.Invoke(
                                resolved,
                                ResolveWhisperFindChatLogType(subtype, result, value, callbacks),
                                null);
                        }

                        _statusMessage = queuedTransfer
                            ? $"Applied packet-owned whisper chase response for {target} and queued map transfer."
                            : $"Applied packet-owned whisper location response for {target}.";
                        message = _statusMessage;
                        return true;
                    }
                case 34:
                    {
                        string target = ReadMapleString(reader);
                        bool disabled = reader.ReadByte() != 0;
                        string text = disabled
                            ? FormatFieldFeedbackStringPoolText(
                                WhisperLocationUnavailableStringPoolId,
                                WhisperDisabledFallback,
                                target.Trim().ToLowerInvariant())
                            : FormatFieldFeedbackStringPoolText(
                                WhisperSentStringPoolId,
                                WhisperSentFallback,
                                target);
                        callbacks?.AddClientChatMessage?.Invoke(text, disabled ? 12 : 1, null);
                        _statusMessage = $"Applied packet-owned whisper availability notice for {target}.";
                        message = _statusMessage;
                        return true;
                    }
                case 146:
                    {
                        ReadMapleString(reader);
                        reader.ReadByte();
                        string text = NormalizeFieldChatText(ReadMapleString(reader));
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            callbacks?.ShowBlowWeatherMessage?.Invoke(text);
                        }

                        _statusMessage = string.IsNullOrWhiteSpace(text)
                            ? "Applied packet-owned whisper blow-weather update without message text."
                            : "Applied packet-owned whisper blow-weather update.";
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
                            callbacks?.AddClientChatMessage?.Invoke(
                                MapleStoryStringPool.GetOrFallback(
                                    CoupleNoticeUnavailableStringPoolId,
                                    CoupleNoticeUnavailableFallback),
                                12,
                                null);
                            _statusMessage = "Applied packet-owned empty couple notice.";
                            message = _statusMessage;
                            return true;
                        }

                        string body = ReadMapleString(reader);
                        body = NormalizeFieldChatText(body);
                        callbacks?.AddClientChatMessage?.Invoke(body, 6, null);
                        _statusMessage = "Applied packet-owned couple notice.";
                        message = _statusMessage;
                        return true;
                    }
                case 5:
                    {
                        string sender = ReadMapleString(reader);
                        reader.ReadByte();
                        string body = ReadMapleString(reader);
                        body = NormalizeFieldChatText(body);
                        callbacks?.AddClientChatMessage?.Invoke(
                            FormatFieldFeedbackStringPoolText(
                                CoupleSharedChatStringPoolId,
                                CoupleSharedChatFallback,
                                sender,
                                body),
                            6,
                            null);
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
                        callbacks?.PlaySummonEffectSound?.Invoke(effectId, x, y);
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
                        (currentHp, maxHp) = ResolveBossHpTagValues(mobId, currentHp, maxHp, callbacks);
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
                        int rewardJobIndex = reader.ReadInt32();
                        int rewardPartIndex = reader.ReadInt32();
                        int rewardLevelIndex = reader.ReadInt32();
                        bool shown = callbacks?.ShowRewardRouletteVisual?.Invoke(rewardJobIndex, rewardPartIndex, rewardLevelIndex) == true;
                        _lastFieldEffectSummary = $"reward roulette job={rewardJobIndex} part={rewardPartIndex} level={rewardLevelIndex}";
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
            string text = FormatFieldFeedbackStringPoolText(
                JukeBoxMessageStringPoolId,
                JukeBoxMessageFallback,
                owner,
                itemName);
            callbacks?.AddClientChatMessage?.Invoke(text, 13, null);
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
                1 => MapleStoryStringPool.GetOrFallback(
                    TransferFieldIgnoredPortalClosedStringPoolId,
                    TransferFieldIgnoredPortalClosedFallback),
                2 => MapleStoryStringPool.GetOrFallback(
                    TransferFieldIgnoredUnavailableStringPoolId,
                    TransferFieldIgnoredUnavailableFallback),
                3 or 5 => MapleStoryStringPool.GetOrFallback(
                    TransferFieldIgnoredBlockedStringPoolId,
                    TransferFieldIgnoredBlockedFallback),
                4 => MapleStoryStringPool.GetOrFallback(
                    TransferFieldIgnoredModalBlockedStringPoolId,
                    TransferFieldIgnoredModalBlockedFallback),
                6 => MapleStoryStringPool.GetOrFallback(
                    TransferFieldIgnoredRuleBlockedStringPoolId,
                    TransferFieldIgnoredRuleBlockedFallback),
                7 => MapleStoryStringPool.GetOrFallback(
                    TransferFieldIgnoredPortalNotReadyStringPoolId,
                    TransferFieldIgnoredPortalNotReadyFallback),
                8 => MapleStoryStringPool.GetOrFallback(
                    TransferFieldIgnoredWarningModalStringPoolId,
                    TransferFieldIgnoredWarningModalFallback),
                _ => $"Field transfer request was ignored with reason {reason}."
            };
            _lastTransferFailureMessage = text;
            if (reason is 4 or 8)
            {
                callbacks?.ShowModalWarning?.Invoke(text);
            }
            else
            {
                callbacks?.AddClientChatMessage?.Invoke(text, 12, null);
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
                1 => MapleStoryStringPool.GetOrFallback(
                    TransferChannelIgnoredPendingStringPoolId,
                    TransferChannelIgnoredPendingFallback),
                2 => MapleStoryStringPool.GetOrFallback(
                    TransferChannelIgnoredUnavailableStringPoolId,
                    TransferChannelIgnoredUnavailableFallback),
                3 => MapleStoryStringPool.GetOrFallback(
                    TransferChannelIgnoredFieldBlockedStringPoolId,
                    TransferChannelIgnoredFieldBlockedFallback),
                4 => MapleStoryStringPool.GetOrFallback(
                    TransferChannelIgnoredRejectedStringPoolId,
                    TransferChannelIgnoredRejectedFallback),
                5 => MapleStoryStringPool.GetOrFallback(
                    TransferChannelIgnoredDelayedStringPoolId,
                    TransferChannelIgnoredDelayedFallback),
                _ => $"Channel transfer request was ignored with reason {reason}."
            };
            _lastTransferFailureMessage = text;
            callbacks?.AddClientChatMessage?.Invoke(text, 12, null);
            _statusMessage = "Applied packet-owned transfer-channel failure feedback.";
            message = _statusMessage;
            return true;
        }

        private bool TryApplySummonItemUnavailable(byte[] payload, int currentTick, PacketFieldFeedbackCallbacks callbacks, out string message)
        {
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
            bool ignored = reader.ReadByte() != 0;
            if (ignored)
            {
                _statusMessage = "Ignored packet-owned summon-item unavailable feedback because the packet flag marked it available.";
                message = _statusMessage;
                return true;
            }

            string text = MapleStoryStringPool.GetOrFallback(
                SummonItemUnavailableNoticeStringPoolId,
                SummonItemUnavailableNoticeFallback);
            callbacks?.ShowModalWarning?.Invoke(text);
            _statusMessage = "Applied packet-owned summon-item availability feedback.";
            message = _statusMessage;
            return true;
        }

        private bool ApplyDestroyClock(PacketFieldFeedbackCallbacks callbacks, out string message)
        {
            _lastBossTimerSummary = "destroyed";
            _bossTimerVisualState = null;
            _lastFieldClockSummary = string.Empty;
            _clockVisualState = null;
            _fieldClockEventFlag = false;
            callbacks?.ClearBossTimerClock?.Invoke();
            callbacks?.ClearFieldClock?.Invoke();
            bool restoredFieldPropertyClock = callbacks?.RestoreFieldPropertyClock?.Invoke() == true;
            _statusMessage = restoredFieldPropertyClock
                ? "Applied packet-owned destroy-clock teardown and restored the map-authored field clock."
                : "Applied packet-owned destroy-clock teardown.";
            message = _statusMessage;
            return true;
        }

        private bool TryApplyClock(byte[] payload, int currentTick, PacketFieldFeedbackCallbacks callbacks, out string message)
        {
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
            if (stream.Length < 1)
            {
                message = "Clock payload is empty.";
                return false;
            }

            byte mode = reader.ReadByte();
            if (mode != 3)
            {
                _fieldClockEventFlag = false;
            }

            switch (mode)
            {
                case 0:
                    {
                        int durationSeconds = reader.ReadInt32();
                        _lastFieldClockSummary = durationSeconds == 0
                            ? "event timer expired"
                            : $"event timer {Math.Abs(durationSeconds)}s";
                        _statusMessage = durationSeconds <= 0
                            ? "Applied packet-owned event-timer expiry update."
                            : "Applied packet-owned event-timer context update.";
                        message = _statusMessage;
                        return true;
                    }
                case 1:
                    {
                        byte hour = reader.ReadByte();
                        byte minute = reader.ReadByte();
                        byte second = reader.ReadByte();
                        PacketFieldClockVisualState state = CreateRealtimeClockVisualState(hour, minute, second, currentTick);
                        _clockVisualState = state;
                        _lastFieldClockSummary = string.Format(
                            CultureInfo.InvariantCulture,
                            "{0} {1:00}:{2:00}:{3:00}",
                            state.IsPm ? "PM" : "AM",
                            state.Hour,
                            state.Minute,
                            state.Second);
                        callbacks?.ShowFieldClock?.Invoke(state);
                        _statusMessage = "Applied packet-owned realtime field-clock update.";
                        message = _statusMessage;
                        return true;
                    }
                case 2:
                    {
                        int durationSeconds = reader.ReadInt32();
                        ClearExistingFieldClock(callbacks);
                        ApplyCountdownClock(PacketFieldClockVisualVariant.Default, durationSeconds, currentTick, callbacks, "field clock countdown");
                        _statusMessage = "Applied packet-owned clock countdown.";
                        message = _statusMessage;
                        return true;
                    }
                case 3:
                    {
                        if (_clockVisualState != null && _fieldClockEventFlag)
                        {
                            ClearFieldClock(callbacks);
                        }
                        else if (_clockVisualState != null)
                        {
                            _statusMessage = "Ignored packet-owned event countdown because a non-event clock is active.";
                            message = _statusMessage;
                            return true;
                        }

                        bool show = reader.ReadByte() != 0;
                        if (!show)
                        {
                            ClearFieldClock(callbacks);
                            _statusMessage = "Cleared packet-owned event countdown clock.";
                            message = _statusMessage;
                            return true;
                        }

                        int durationSeconds = reader.ReadInt32();
                        ApplyCountdownClock(PacketFieldClockVisualVariant.Event, durationSeconds, currentTick, callbacks, "event countdown");
                        _fieldClockEventFlag = _clockVisualState != null;
                        _statusMessage = "Applied packet-owned event countdown clock.";
                        message = _statusMessage;
                        return true;
                    }
                case 100:
                    {
                        ClearExistingFieldClock(callbacks);
                        bool show = reader.ReadByte() != 0;
                        if (!show)
                        {
                            _statusMessage = "Cleared packet-owned cake-pie timerboard.";
                            message = _statusMessage;
                            return true;
                        }

                        byte boardType = reader.ReadByte();
                        int durationSeconds = reader.ReadInt32();
                        PacketFieldClockVisualVariant variant = boardType == 0
                            ? PacketFieldClockVisualVariant.CakePieSmall
                            : PacketFieldClockVisualVariant.CakePieLarge;
                        ApplyCountdownClock(
                            variant,
                            durationSeconds,
                            currentTick,
                            callbacks,
                            boardType == 0 ? "cake pie small timerboard" : "cake pie large timerboard");
                        _statusMessage = "Applied packet-owned cake-pie timerboard.";
                        message = _statusMessage;
                        return true;
                    }
                default:
                    message = $"Unsupported clock mode {mode}.";
                    return false;
            }
        }

        private bool TryApplyBossTimer(
            string bossName,
            byte[] payload,
            int currentTick,
            PacketFieldFeedbackCallbacks callbacks,
            int normalStringPoolId,
            int warningStringPoolId,
            int expiredStringPoolId,
            string normalFallbackFormat,
            string warningFallbackFormat,
            string expiredFallbackText,
            out string message)
        {
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
            byte mode = reader.ReadByte();
            int value = reader.ReadInt32();
            string text = ResolveBossTimerChatText(
                mode,
                value,
                normalStringPoolId,
                warningStringPoolId,
                expiredStringPoolId,
                normalFallbackFormat,
                warningFallbackFormat,
                expiredFallbackText);
            callbacks?.AddClientChatMessage?.Invoke(text, 12, null);
            _lastBossTimerSummary = text;
            TryShowBossTimerClock(
                bossName,
                value,
                mode == 0 ? "before" : "warning",
                currentTick,
                callbacks);
            _statusMessage = $"Applied packet-owned {bossName} timer feedback.";
            message = _statusMessage;
            return true;
        }

        private bool TryApplyHontaleTimer(byte[] payload, int currentTick, PacketFieldFeedbackCallbacks callbacks, out string message)
        {
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
            if (stream.Length < 2)
            {
                message = "Hontale timer payload is shorter than the client packet shape.";
                return false;
            }

            byte mode = reader.ReadByte();
            byte value = reader.ReadByte();
            if (!TryResolveHontaleTimerChatText(mode, value, out string text))
            {
                message = $"Unsupported Hontale timer mode {mode}.";
                return false;
            }

            callbacks?.AddClientChatMessage?.Invoke(text, 12, null);
            _lastBossTimerSummary = text;
            TryShowBossTimerClock(
                "Hontale",
                value,
                mode switch
                {
                    0 => "entry",
                    1 => "warning",
                    _ => "expired"
                },
                currentTick,
                callbacks);
            _statusMessage = "Applied packet-owned Hontale timer feedback.";
            message = _statusMessage;
            return true;
        }

        private void TryShowBossTimerClock(
            string bossName,
            int value,
            string phase,
            int currentTick,
            PacketFieldFeedbackCallbacks callbacks)
        {
            if (value <= 0)
            {
                _bossTimerVisualState = null;
                callbacks?.ClearBossTimerClock?.Invoke();
                return;
            }

            string normalizedBossName = string.IsNullOrWhiteSpace(bossName)
                ? "Boss"
                : bossName.Trim();
            string normalizedPhase = string.IsNullOrWhiteSpace(phase)
                ? "timer"
                : phase.Trim();
            PacketFieldBossTimerVisualState state = new(
                $"{normalizedBossName}:{normalizedPhase}",
                normalizedBossName,
                checked(value * 60),
                currentTick);
            _bossTimerVisualState = state;
            callbacks?.ShowBossTimerClock?.Invoke(state);
        }

        private void ApplyCountdownClock(
            PacketFieldClockVisualVariant variant,
            int durationSeconds,
            int currentTick,
            PacketFieldFeedbackCallbacks callbacks,
            string label)
        {
            if (durationSeconds < 0)
            {
                ClearExistingFieldClock(callbacks);
                return;
            }

            PacketFieldClockVisualState state = new(
                PacketFieldClockVisualKind.Countdown,
                variant,
                currentTick,
                durationSeconds,
                false,
                0,
                0,
                0,
                label);
            _clockVisualState = state;
            _lastFieldClockSummary = string.Format(
                CultureInfo.InvariantCulture,
                "{0} {1:00}:{2:00}",
                label,
                durationSeconds / 60,
                durationSeconds % 60);
            callbacks?.ShowFieldClock?.Invoke(state);
        }

        private void ClearFieldClock(PacketFieldFeedbackCallbacks callbacks)
        {
            _clockVisualState = null;
            _fieldClockEventFlag = false;
            _lastFieldClockSummary = string.Empty;
            callbacks?.ClearFieldClock?.Invoke();
        }

        private void ClearExistingFieldClock(PacketFieldFeedbackCallbacks callbacks)
        {
            if (_clockVisualState == null)
            {
                _fieldClockEventFlag = false;
                _lastFieldClockSummary = string.Empty;
                return;
            }

            ClearFieldClock(callbacks);
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

        private static string BuildWhisperLocationMessage(string target, string locationName)
        {
            return FormatWhisperStringPoolText(
                WhisperLocationStringPoolId,
                WhisperLocationFallback,
                target,
                locationName);
        }

        private static string ResolveIncomingWhisperLogText(string sender, byte channelId, string body, PacketFieldFeedbackCallbacks callbacks)
        {
            int currentChannelId = callbacks?.GetCurrentChannelId?.Invoke() ?? 0;
            if (channelId <= 0 || channelId == currentChannelId)
            {
                return FormatFieldFeedbackStringPoolText(
                    IncomingWhisperSameChannelStringPoolId,
                    IncomingWhisperSameChannelFallback,
                    sender,
                    body);
            }

            string channelName = callbacks?.ResolveChannelName?.Invoke(channelId) ?? $"Ch. {channelId}";
            return FormatFieldFeedbackStringPoolText(
                IncomingWhisperOtherChannelStringPoolId,
                IncomingWhisperOtherChannelFallback,
                sender,
                channelName,
                body);
        }

        private static void TryAddOutgoingWhisperEcho(string target, PacketFieldFeedbackCallbacks callbacks)
        {
            string outgoingText = callbacks?.GetLastOutgoingWhisperText?.Invoke();
            if (string.IsNullOrWhiteSpace(outgoingText))
            {
                return;
            }

            string outgoingTarget = callbacks?.GetLastOutgoingWhisperTarget?.Invoke();
            string resolvedTarget = string.IsNullOrWhiteSpace(outgoingTarget)
                ? target?.Trim() ?? string.Empty
                : outgoingTarget.Trim();
            callbacks?.AddClientChatMessage?.Invoke(
                FormatFieldFeedbackStringPoolText(
                    OutgoingWhisperLogStringPoolId,
                    OutgoingWhisperLogFallback,
                    resolvedTarget,
                    outgoingText.Trim()),
                1,
                null);
        }

        private static bool HasWhisperTransferTarget(int mapId, PacketFieldFeedbackCallbacks callbacks)
        {
            if (mapId <= 0)
            {
                return false;
            }

            return callbacks?.HasMapTransferTarget?.Invoke(mapId) ?? true;
        }

        private static string FormatWhisperStringPoolText(int stringPoolId, string fallbackFormat, params object[] args)
        {
            string format = MapleStoryStringPool.GetCompositeFormatOrFallback(
                stringPoolId,
                fallbackFormat,
                maxPlaceholderCount: args?.Length ?? 0,
                out bool usedResolvedText);
            if (string.IsNullOrWhiteSpace(format))
            {
                format = fallbackFormat;
            }

            try
            {
                return args == null || args.Length == 0
                    ? format
                    : string.Format(CultureInfo.InvariantCulture, format, args);
            }
            catch (FormatException)
            {
                return usedResolvedText
                    ? MapleStoryStringPool.GetOrFallback(
                        stringPoolId,
                        args == null || args.Length == 0
                            ? fallbackFormat
                            : string.Format(CultureInfo.InvariantCulture, fallbackFormat, args))
                    : (args == null || args.Length == 0
                        ? fallbackFormat
                        : string.Format(CultureInfo.InvariantCulture, fallbackFormat, args));
            }
        }

        private static string FormatFieldFeedbackStringPoolText(int stringPoolId, string fallbackFormat, params object[] args)
        {
            return FormatWhisperStringPoolText(stringPoolId, fallbackFormat, args);
        }

        private static string NormalizeFieldChatText(string text)
        {
            return NormalizeFieldChatText(text, static value => IsDbcsLeadByte(value));
        }

        private static string NormalizeFieldChatText(string text, Func<byte, bool> isDbcsLeadByte)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            if (TryEncodeLosslessSwindleText(text, out byte[] encodedText))
            {
                byte[] normalizedBytes = NormalizeFieldChatBytes(encodedText, isDbcsLeadByte);
                return normalizedBytes.Length == 0
                    ? string.Empty
                    : SwindleEncoding.GetString(normalizedBytes).Trim();
            }

            StringBuilder builder = new(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char character = text[i];
                builder.Append(character < 32 || character == 127 ? ' ' : character);
            }

            return builder.ToString().Trim();
        }

        private static byte[] NormalizeFieldChatBytes(ReadOnlySpan<byte> sourceBytes, Func<byte, bool> isDbcsLeadByte)
        {
            if (sourceBytes.Length == 0)
            {
                return Array.Empty<byte>();
            }

            byte[] normalized = sourceBytes.ToArray();
            for (int index = 0; index < normalized.Length;)
            {
                if (isDbcsLeadByte(normalized[index])
                    && index + 1 < normalized.Length)
                {
                    index += 2;
                    continue;
                }

                if (normalized[index] < 0x20 || normalized[index] == 0x7F)
                {
                    normalized[index] = 0x20;
                }

                index++;
            }

            return normalized;
        }

        private static bool TryBuildWhisperFindMessage(
            byte subtype,
            string target,
            byte result,
            int value,
            PacketFieldFeedbackCallbacks callbacks,
            out string text)
        {
            string normalizedTarget = target?.Trim() ?? string.Empty;
            if (subtype == 72)
            {
                switch (result)
                {
                    case 1:
                        if (IsWhisperHiddenField(value, callbacks))
                        {
                            string hiddenText = MapleStoryStringPool.GetOrFallback(
                                WhisperUserListHiddenFieldStringPoolId,
                                WhisperUserListHiddenFieldFallback);
                            text = FormatWhisperUserListText(normalizedTarget, hiddenText);
                            return true;
                        }

                        if (!HasWhisperTransferTarget(value, callbacks))
                        {
                            text = string.Empty;
                            return true;
                        }

                        string mapName = callbacks?.ResolveMapName?.Invoke(value);
                        text = string.IsNullOrWhiteSpace(mapName)
                            ? string.Empty
                            : FormatWhisperUserListText(normalizedTarget, mapName.Trim());
                        return true;
                    case 2:
                        text = FormatWhisperUserListText(
                            normalizedTarget,
                            MapleStoryStringPool.GetOrFallback(WhisperUserListNotFoundStringPoolId, WhisperUserListNotFoundFallback));
                        return true;
                    case 3:
                        string channelName = callbacks?.ResolveChannelName?.Invoke(value);
                        text = string.IsNullOrWhiteSpace(channelName)
                            ? string.Empty
                            : FormatWhisperUserListText(normalizedTarget, channelName.Trim());
                        return true;
                    case 4:
                        text = string.Empty;
                        return true;
                    default:
                        text = string.Empty;
                        return true;
                }
            }

            switch (result)
            {
                case 1:
                    if (IsWhisperHiddenField(value, callbacks))
                    {
                        text = FormatWhisperStringPoolText(
                            subtype == 72 ? WhisperUserListFormatStringPoolId : WhisperHiddenFieldStringPoolId,
                            WhisperHiddenFieldFallback,
                            normalizedTarget);
                        return true;
                    }

                    if (!HasWhisperTransferTarget(value, callbacks))
                    {
                        text = string.Empty;
                        return true;
                    }

                    string mapName = callbacks?.ResolveMapName?.Invoke(value);
                    if (string.IsNullOrWhiteSpace(mapName))
                    {
                        text = string.Empty;
                        return true;
                    }

                    string resolvedLocation = mapName.Trim();
                    string location = BuildWhisperLocationMessage(normalizedTarget, resolvedLocation);
                    if (subtype == 9 && callbacks?.IsUnderCover?.Invoke() == true)
                    {
                        location += FormatWhisperUnderCoverMapIdSuffix(value);
                    }

                    text = subtype == 72
                        ? FormatWhisperStringPoolText(
                            WhisperUserListFormatStringPoolId,
                            WhisperLocationFallback,
                            normalizedTarget,
                            resolvedLocation)
                        : location;
                    return true;
                case 2:
                    text = FormatWhisperStringPoolText(
                        subtype == 72 ? WhisperUserListFormatStringPoolId : WhisperNotFoundStringPoolId,
                        WhisperNotFoundFallback,
                        normalizedTarget);
                    return true;
                case 3:
                    if (subtype != 72)
                    {
                        string channelName = callbacks?.ResolveChannelName?.Invoke(value);
                        text = string.IsNullOrWhiteSpace(channelName)
                            ? FormatWhisperStringPoolText(
                                WhisperLocationUnavailableStringPoolId,
                                WhisperLocationUnavailableFallback,
                                normalizedTarget)
                            : FormatWhisperStringPoolText(
                                WhisperChannelStringPoolId,
                                WhisperChannelFallback,
                                normalizedTarget,
                                channelName.Trim());
                        return true;
                    }

                    text = FormatWhisperStringPoolText(
                        WhisperUserListFormatStringPoolId,
                        WhisperChannelFallback,
                        normalizedTarget,
                        callbacks?.ResolveChannelName?.Invoke(value) ?? $"Ch. {value}");
                    return true;
                case 4:
                    text = FormatWhisperStringPoolText(
                        subtype == 72 ? WhisperUserListFormatStringPoolId : WhisperLocationUnavailableStringPoolId,
                        WhisperLocationUnavailableFallback,
                        normalizedTarget);
                    return true;
                default:
                    text = subtype == 72
                        ? string.Empty
                        : FormatWhisperStringPoolText(
                            WhisperLocationUnavailableStringPoolId,
                            WhisperLocationUnavailableFallback,
                            normalizedTarget);
                    return true;
            }
        }

        private static string FormatWhisperUserListText(string target, string detailText)
        {
            return FormatWhisperStringPoolText(
                WhisperUserListFormatStringPoolId,
                WhisperUserListEntryFallback,
                target,
                detailText ?? string.Empty);
        }

        private static string FormatWhisperUnderCoverMapIdSuffix(int mapId)
        {
            string format = MapleStoryStringPool.GetOrFallback(
                WhisperUnderCoverMapIdSuffixStringPoolId,
                WhisperUnderCoverMapIdSuffixFallback);
            if (string.IsNullOrWhiteSpace(format))
            {
                return string.Format(CultureInfo.InvariantCulture, WhisperUnderCoverMapIdSuffixFallback, mapId);
            }

            return FormatWhisperIntegerPrintfSegment(
                format,
                mapId,
                WhisperUnderCoverMapIdSuffixFallback);
        }

        private static string FormatWhisperIntegerPrintfSegment(string format, int value, string fallbackFormat)
        {
            if (string.IsNullOrWhiteSpace(format))
            {
                return string.Format(CultureInfo.InvariantCulture, fallbackFormat, value);
            }

            if (format.Contains("%09d", StringComparison.Ordinal))
            {
                return format.Replace(
                    "%09d",
                    value.ToString("D9", CultureInfo.InvariantCulture),
                    StringComparison.Ordinal);
            }

            if (format.Contains("%d", StringComparison.Ordinal))
            {
                return format.Replace(
                    "%d",
                    value.ToString(CultureInfo.InvariantCulture),
                    StringComparison.Ordinal);
            }

            try
            {
                return string.Format(CultureInfo.InvariantCulture, format, value);
            }
            catch (FormatException)
            {
                return string.Format(CultureInfo.InvariantCulture, fallbackFormat, value);
            }
        }

        private static bool IsWhisperHiddenField(int mapId, PacketFieldFeedbackCallbacks callbacks)
        {
            if (mapId <= 0)
            {
                return false;
            }

            int mapCategory = Math.Abs(mapId / 1000000) % 100;
            return mapCategory == 9 && callbacks?.IsUnderCover?.Invoke() != true;
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

        private static int ResolveWhisperFindChatLogType(
            byte subtype,
            byte result,
            int value,
            PacketFieldFeedbackCallbacks callbacks)
        {
            if (subtype == 9
                && result == 1
                && HasWhisperTransferTarget(value, callbacks)
                && !IsWhisperHiddenField(value, callbacks))
            {
                return 7;
            }

            return 12;
        }

        private static string ResolveBossTimerChatText(
            byte mode,
            int value,
            int normalStringPoolId,
            int warningStringPoolId,
            int expiredStringPoolId,
            string normalFallbackFormat,
            string warningFallbackFormat,
            string expiredFallbackText)
        {
            if (value == 0)
            {
                return MapleStoryStringPool.GetOrFallback(expiredStringPoolId, expiredFallbackText);
            }

            int stringPoolId = mode != 0 ? warningStringPoolId : normalStringPoolId;
            string fallbackFormat = mode != 0
                ? warningFallbackFormat
                : normalFallbackFormat;
            return FormatStringPoolTimerText(stringPoolId, fallbackFormat, value);
        }

        private static bool TryResolveHontaleTimerChatText(byte mode, byte value, out string text)
        {
            switch (mode)
            {
                case 0:
                    text = FormatStringPoolTimerText(
                        HontaleTimerStageZeroStringPoolId,
                        HontaleTimerStageZeroFallback,
                        value);
                    return true;
                case 1:
                    text = FormatStringPoolTimerText(
                        HontaleTimerStageOneStringPoolId,
                        HontaleTimerStageOneFallback,
                        value);
                    return true;
                case 2:
                    text = FormatStringPoolTimerText(
                        HontaleTimerStageTwoStringPoolId,
                        HontaleTimerStageTwoFallback,
                        value);
                    return true;
                default:
                    text = string.Empty;
                    return false;
            }
        }

        private static string FormatStringPoolTimerText(int stringPoolId, string fallbackFormat, int value)
        {
            string format = MapleStoryStringPool.GetCompositeFormatOrFallback(
                stringPoolId,
                fallbackFormat,
                maxPlaceholderCount: 1,
                out bool usedResolvedText);
            if (string.IsNullOrWhiteSpace(format))
            {
                format = fallbackFormat;
            }

            try
            {
                return string.Format(CultureInfo.InvariantCulture, format, value);
            }
            catch (FormatException)
            {
                return usedResolvedText
                    ? MapleStoryStringPool.GetOrFallback(
                        stringPoolId,
                        string.Format(CultureInfo.InvariantCulture, fallbackFormat, value))
                    : string.Format(CultureInfo.InvariantCulture, fallbackFormat, value);
            }
        }

        private void TryAddSwindleWarning(string message, bool allowGroupFamilyWarning, int currentTick, PacketFieldFeedbackCallbacks callbacks)
        {
            if (!allowGroupFamilyWarning
                || callbacks?.AddClientChatMessage == null
                || currentTick < _nextSwindleWarningTick
                || !TryBuildSwindleWarning(message, callbacks?.ResolveSwindleWarnings?.Invoke(), out string warningText))
            {
                return;
            }

            callbacks.AddClientChatMessage(warningText, 8, null);
            _nextSwindleWarningTick = currentTick + 10000;
        }

        private static bool TryBuildSwindleWarning(
            string message,
            IReadOnlyList<PacketFieldSwindleWarningEntry> warningEntries,
            out string warningText)
        {
            warningText = null;
            string filteredMessageText = FilterSwindleText(message);
            byte[] filteredMessage = TryEncodeLosslessSwindleText(filteredMessageText, out byte[] encodedMessage)
                ? encodedMessage
                : Array.Empty<byte>();
            if (string.IsNullOrWhiteSpace(filteredMessageText)
                || warningEntries == null
                || warningEntries.Count == 0)
            {
                return false;
            }

            foreach (PacketFieldSwindleWarningEntry entry in warningEntries)
            {
                if (entry?.Keywords == null
                    || entry.WarningTexts == null
                    || entry.WarningTexts.Count == 0)
                {
                    continue;
                }

                foreach (string keyword in entry.Keywords)
                {
                    if (string.IsNullOrWhiteSpace(keyword))
                    {
                        continue;
                    }

                    if (ContainsSwindleKeyword(filteredMessage, filteredMessageText, keyword))
                    {
                        warningText = entry.WarningTexts[Random.Shared.Next(entry.WarningTexts.Count)];
                        return !string.IsNullOrWhiteSpace(warningText);
                    }
                }
            }

            return false;
        }

        private static bool ContainsSwindleKeyword(byte[] filteredMessage, string filteredMessageText, string keyword)
        {
            if (string.IsNullOrWhiteSpace(filteredMessageText)
                || string.IsNullOrWhiteSpace(keyword))
            {
                return false;
            }

            string filteredKeyword = FilterSwindleText(keyword);
            if (string.IsNullOrWhiteSpace(filteredKeyword))
            {
                return false;
            }

            if (filteredMessage != null
                && filteredMessage.Length > 0
                && TryEncodeLosslessSwindleText(filteredKeyword, out byte[] keywordBytes)
                && keywordBytes.Length > 0)
            {
                return FindSwindleSubstring(
                    filteredMessage,
                    keywordBytes,
                    static value => IsDbcsLeadByte(value)) >= 0;
            }

            return ContainsSwindleKeywordUnicode(filteredMessageText, filteredKeyword);
        }

        private static string FilterSwindleText(string message)
        {
            byte[] filteredBytes = FilterSwindleBytes(message);
            return filteredBytes.Length == 0
                ? string.Empty
                : SwindleEncoding.GetString(filteredBytes);
        }

        private static byte[] FilterSwindleBytes(string message)
        {
            return FilterSwindleBytes(message, SwindleFilteredCharacters);
        }

        private static byte[] FilterSwindleBytes(string message, ReadOnlySpan<byte> filteredCharacters)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return Array.Empty<byte>();
            }

            return FilterSwindleBytes(
                SwindleEncoding.GetBytes(message),
                filteredCharacters,
                static value => IsDbcsLeadByte(value));
        }

        private static byte[] FilterSwindleBytes(
            ReadOnlySpan<byte> sourceBytes,
            ReadOnlySpan<byte> filteredCharacters,
            Func<byte, bool> isDbcsLeadByte)
        {
            if (sourceBytes.Length == 0)
            {
                return Array.Empty<byte>();
            }

            byte[] filteredBytes = new byte[sourceBytes.Length];
            int count = 0;
            for (int index = 0; index < sourceBytes.Length;)
            {
                byte value = sourceBytes[index];
                int characterLength = GetSwindleCharacterLength(sourceBytes, index, isDbcsLeadByte);
                if (characterLength == 2)
                {
                    filteredBytes[count++] = value;
                    filteredBytes[count++] = sourceBytes[index + 1];
                    index += 2;
                    continue;
                }

                if (value < 0x20)
                {
                    index++;
                    continue;
                }

                if (ContainsFilteredSwindleCharacter(value, filteredCharacters))
                {
                    index++;
                    continue;
                }

                filteredBytes[count++] = value;
                index++;
            }

            if (count == 0)
            {
                return Array.Empty<byte>();
            }

            Array.Resize(ref filteredBytes, count);
            return filteredBytes;
        }

        private static bool ContainsSwindleKeywordUnicode(string filteredMessage, string filteredKeyword)
        {
            if (string.IsNullOrWhiteSpace(filteredMessage)
                || string.IsNullOrWhiteSpace(filteredKeyword))
            {
                return false;
            }

            return filteredMessage.IndexOf(filteredKeyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool TryEncodeLosslessSwindleText(string text, out byte[] bytes)
        {
            bytes = Array.Empty<byte>();
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            byte[] encoded = SwindleEncoding.GetBytes(text);
            if (!string.Equals(SwindleEncoding.GetString(encoded), text, StringComparison.Ordinal))
            {
                return false;
            }

            bytes = encoded;
            return true;
        }

        private static int FindSwindleSubstring(
            byte[] filteredMessage,
            byte[] keywordBytes,
            Func<byte, bool> isDbcsLeadByte)
        {
            if (filteredMessage == null
                || keywordBytes == null
                || filteredMessage.Length == 0
                || keywordBytes.Length == 0)
            {
                return -1;
            }

            for (int start = 0; start < filteredMessage.Length;)
            {
                // Match the client's SearchSubstring guard: it refuses to start from
                // the terminal byte and instead advances by DBCS character boundaries.
                if (start + 1 >= filteredMessage.Length)
                {
                    break;
                }

                if (MatchesSwindleKeyword(filteredMessage, start, keywordBytes, isDbcsLeadByte))
                {
                    return start;
                }

                start += GetSwindleCharacterLength(filteredMessage, start, isDbcsLeadByte);
            }

            return -1;
        }

        private static bool MatchesSwindleKeyword(
            byte[] filteredMessage,
            int start,
            byte[] keywordBytes,
            Func<byte, bool> isDbcsLeadByte)
        {
            if (start < 0
                || filteredMessage == null
                || keywordBytes == null
                || start >= filteredMessage.Length
                || keywordBytes.Length == 0)
            {
                return false;
            }

            int messageIndex = start;
            int keywordIndex = 0;
            while (keywordIndex < keywordBytes.Length)
            {
                if (messageIndex >= filteredMessage.Length)
                {
                    return false;
                }

                int messageLength = GetSwindleCharacterLength(filteredMessage, messageIndex, isDbcsLeadByte);
                int keywordLength = GetSwindleCharacterLength(keywordBytes, keywordIndex, isDbcsLeadByte);
                if (messageLength != keywordLength
                    || messageIndex + messageLength > filteredMessage.Length
                    || keywordIndex + keywordLength > keywordBytes.Length)
                {
                    return false;
                }

                if (messageLength == 2)
                {
                    if (filteredMessage[messageIndex] != keywordBytes[keywordIndex]
                        || filteredMessage[messageIndex + 1] != keywordBytes[keywordIndex + 1])
                    {
                        return false;
                    }
                }
                else if (!IsSwindleByteEqual(filteredMessage[messageIndex], keywordBytes[keywordIndex]))
                {
                    return false;
                }

                messageIndex += messageLength;
                keywordIndex += keywordLength;
            }

            return true;
        }

        private static bool ContainsFilteredSwindleCharacter(byte value, ReadOnlySpan<byte> filteredCharacters)
        {
            foreach (byte filteredCharacter in filteredCharacters)
            {
                if (filteredCharacter == value)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSwindleByteEqual(byte left, byte right)
        {
            return ToSwindleLower(left) == ToSwindleLower(right);
        }

        private static byte ToSwindleLower(byte value)
        {
            return unchecked((byte)MbcsToLower(value));
        }

        private static int GetSwindleCharacterLength(
            ReadOnlySpan<byte> bytes,
            int index,
            Func<byte, bool> isDbcsLeadByte)
        {
            if (index < 0
                || index >= bytes.Length)
            {
                return 1;
            }

            return isDbcsLeadByte != null
                && isDbcsLeadByte(bytes[index])
                && index + 1 < bytes.Length
                ? 2
                : 1;
        }

        internal static string BuildSwindleWarningForTest(
            string message,
            IReadOnlyList<PacketFieldSwindleWarningEntry> warningEntries)
        {
            return TryBuildSwindleWarning(message, warningEntries, out string warningText)
                ? warningText
                : null;
        }

        internal static string FilterSwindleTextForTest(string message)
        {
            return FilterSwindleText(message);
        }

        internal static string FilterSwindleTextForTest(string message, string filteredCharacters)
        {
            byte[] filterTable = string.IsNullOrEmpty(filteredCharacters)
                ? Array.Empty<byte>()
                : SwindleEncoding.GetBytes(filteredCharacters);
            byte[] filteredBytes = FilterSwindleBytes(message, filterTable);
            return filteredBytes.Length == 0
                ? string.Empty
                : SwindleEncoding.GetString(filteredBytes);
        }

        internal static bool ContainsSwindleKeywordForTest(string filteredMessage, string keyword)
        {
            string normalized = FilterSwindleText(filteredMessage);
            byte[] filteredBytes = TryEncodeLosslessSwindleText(normalized, out byte[] encoded)
                ? encoded
                : Array.Empty<byte>();
            return ContainsSwindleKeyword(filteredBytes, normalized, keyword);
        }

        internal static byte[] NormalizeFieldChatBytesForTest(byte[] sourceBytes, Func<byte, bool> isDbcsLeadByte)
        {
            return NormalizeFieldChatBytes(sourceBytes, isDbcsLeadByte);
        }

        internal static string NormalizeFieldChatTextForTest(string text, Func<byte, bool> isDbcsLeadByte)
        {
            return NormalizeFieldChatText(text, isDbcsLeadByte);
        }

        internal static string FilterSwindleCharacterTableForTest()
        {
            return SwindleEncoding.GetString(SwindleFilteredCharacters);
        }

        internal static byte[] FilterSwindleBytesForTest(
            byte[] sourceBytes,
            byte[] filteredCharacters,
            params byte[] dbcsLeadBytes)
        {
            return FilterSwindleBytes(
                sourceBytes,
                filteredCharacters ?? Array.Empty<byte>(),
                value => Array.IndexOf(dbcsLeadBytes ?? Array.Empty<byte>(), value) >= 0);
        }

        internal static bool ContainsSwindleKeywordForTest(
            byte[] filteredMessage,
            byte[] keywordBytes,
            params byte[] dbcsLeadBytes)
        {
            return FindSwindleSubstring(
                filteredMessage,
                keywordBytes,
                value => Array.IndexOf(dbcsLeadBytes ?? Array.Empty<byte>(), value) >= 0) >= 0;
        }

        internal static string BuildWhisperFindMessageForTest(
            byte subtype,
            string target,
            byte result,
            int value,
            PacketFieldFeedbackCallbacks callbacks = null)
        {
            return TryBuildWhisperFindMessage(subtype, target, result, value, callbacks, out string text)
                ? text
                : string.Empty;
        }

        internal static string FormatFieldFeedbackStringPoolTextForTest(int stringPoolId, string fallbackFormat, params object[] args)
        {
            return FormatFieldFeedbackStringPoolText(stringPoolId, fallbackFormat, args);
        }

        [DllImport("kernel32.dll", SetLastError = false)]
        private static extern uint GetACP();

        [DllImport("kernel32.dll", SetLastError = false)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsDBCSLeadByteEx(uint codePage, byte testChar);

        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "_mbctolower")]
        private static extern uint MbcsToLower(uint value);

        private static uint ResolveSwindleCodePage()
        {
            try
            {
                return GetACP();
            }
            catch
            {
                return 0;
            }
        }

        private static Encoding ResolveSwindleEncoding()
        {
            if (SwindleCodePage > 0)
            {
                try
                {
                    return Encoding.GetEncoding((int)SwindleCodePage);
                }
                catch (ArgumentException)
                {
                }
                catch (NotSupportedException)
                {
                }
            }

            return Encoding.Default;
        }

        private static bool IsDbcsLeadByte(byte value)
        {
            if (SwindleCodePage == 0)
            {
                return false;
            }

            return IsDBCSLeadByteEx(SwindleCodePage, value);
        }

        internal static int GetSwindleEncodingCodePageForTest()
        {
            return SwindleEncoding.CodePage;
        }

        internal static int GetSwindleAnsiCodePageForTest()
        {
            return unchecked((int)SwindleCodePage);
        }

        internal static IReadOnlyList<PacketFieldSwindleWarningEntry> CreateSwindleWarningEntriesForTest(
            params (int GroupId, string[] Keywords, string[] Warnings)[] entries)
        {
            if (entries == null || entries.Length == 0)
            {
                return Array.Empty<PacketFieldSwindleWarningEntry>();
            }

            List<PacketFieldSwindleWarningEntry> warningEntries = new(entries.Length);
            foreach ((int groupId, string[] keywords, string[] warnings) in entries)
            {
                if (groupId < 0
                    || keywords == null
                    || warnings == null)
                {
                    continue;
                }

                warningEntries.Add(new PacketFieldSwindleWarningEntry(groupId, keywords, warnings));
            }

            return warningEntries;
        }

        internal static string ResolveBossTimerChatTextForTest(
            byte mode,
            int value,
            int normalStringPoolId,
            int warningStringPoolId,
            int expiredStringPoolId,
            string normalFallbackFormat,
            string warningFallbackFormat,
            string expiredFallbackText)
        {
            return ResolveBossTimerChatText(
                mode,
                value,
                normalStringPoolId,
                warningStringPoolId,
                expiredStringPoolId,
                normalFallbackFormat,
                warningFallbackFormat,
                expiredFallbackText);
        }

        internal static bool TryResolveHontaleTimerChatTextForTest(byte mode, byte value, out string text)
        {
            return TryResolveHontaleTimerChatText(mode, value, out text);
        }

        internal static PacketFieldBossTimerVisualState CreateBossTimerVisualStateForTest(
            string bossName,
            int value,
            string phase)
        {
            if (value <= 0)
            {
                return null;
            }

            string normalizedBossName = string.IsNullOrWhiteSpace(bossName)
                ? "Boss"
                : bossName.Trim();
            string normalizedPhase = string.IsNullOrWhiteSpace(phase)
                ? "timer"
                : phase.Trim();
            return new PacketFieldBossTimerVisualState(
                $"{normalizedBossName}:{normalizedPhase}",
                normalizedBossName,
                checked(value * 60),
                0);
        }

        internal static int GetBossTimerRemainingSecondsForTest(PacketFieldBossTimerVisualState state, int currentTick)
        {
            return GetBossTimerRemainingSeconds(state, currentTick);
        }

        private static int GetBossTimerRemainingSeconds(PacketFieldBossTimerVisualState state, int currentTick)
        {
            if (state == null)
            {
                return 0;
            }

            long elapsedMilliseconds = Math.Max(0, currentTick - state.StartedAtTick);
            long remainingMilliseconds = ((long)Math.Max(0, state.DurationSeconds) * 1000L) - elapsedMilliseconds;
            return (int)Math.Max(0L, remainingMilliseconds / 1000L);
        }

        private static PacketFieldClockVisualState CreateRealtimeClockVisualState(byte hour, byte minute, byte second, int currentTick)
        {
            bool isPm = hour / 12 != 0;
            int normalizedHour = hour % 12;
            if (isPm && normalizedHour == 0)
            {
                normalizedHour = 12;
            }

            return new PacketFieldClockVisualState(
                PacketFieldClockVisualKind.Realtime,
                PacketFieldClockVisualVariant.Default,
                currentTick,
                0,
                isPm,
                normalizedHour,
                minute,
                second,
                "field clock");
        }

        private static int GetFieldClockRemainingSeconds(PacketFieldClockVisualState state, int currentTick)
        {
            if (state == null || state.Kind != PacketFieldClockVisualKind.Countdown)
            {
                return 0;
            }

            long elapsedMilliseconds = Math.Max(0, currentTick - state.StartedAtTick);
            long remainingMilliseconds = ((long)Math.Max(0, state.DurationSeconds) * 1000L) - elapsedMilliseconds;
            return (int)Math.Max(0L, remainingMilliseconds / 1000L);
        }

        private static (bool IsPm, int Hour, int Minute, int Second) ResolveFieldClockDisplayTime(PacketFieldClockVisualState state, int currentTick)
        {
            if (state == null)
            {
                return (false, 0, 0, 0);
            }

            if (state.Kind != PacketFieldClockVisualKind.Realtime)
            {
                int remainingSeconds = GetFieldClockRemainingSeconds(state, currentTick);
                return (false, remainingSeconds / 3600, (remainingSeconds / 60) % 60, remainingSeconds % 60);
            }

            int hour24 = state.IsPm
                ? (state.Hour == 12 ? 12 : state.Hour + 12)
                : (state.Hour == 12 ? 0 : state.Hour);
            int totalSeconds = (((hour24 * 60) + Math.Max(0, state.Minute)) * 60) + Math.Max(0, state.Second);
            totalSeconds += Math.Max(0, currentTick - state.StartedAtTick) / 1000;
            totalSeconds %= 24 * 60 * 60;

            int resolvedHour24 = totalSeconds / 3600;
            int resolvedMinute = (totalSeconds / 60) % 60;
            int resolvedSecond = totalSeconds % 60;
            bool isPm = resolvedHour24 / 12 != 0;
            int resolvedHour = resolvedHour24 % 12;
            if (isPm && resolvedHour == 0)
            {
                resolvedHour = 12;
            }

            return (isPm, resolvedHour, resolvedMinute, resolvedSecond);
        }

        internal static PacketFieldClockVisualState CreateFieldClockVisualStateForTest(byte hour, byte minute, byte second)
        {
            return CreateRealtimeClockVisualState(hour, minute, second, 0);
        }

        internal static int GetFieldClockRemainingSecondsForTest(PacketFieldClockVisualState state, int currentTick)
        {
            return GetFieldClockRemainingSeconds(state, currentTick);
        }

        internal static (bool IsPm, int Hour, int Minute, int Second) ResolveFieldClockDisplayTimeForTest(PacketFieldClockVisualState state, int currentTick)
        {
            return ResolveFieldClockDisplayTime(state, currentTick);
        }

        internal static (int CurrentHp, int MaxHp) ResolveBossHpTagValuesForTest(
            int mobId,
            int currentHp,
            int maxHp,
            PacketFieldFeedbackCallbacks callbacks = null)
        {
            return ResolveBossHpTagValues(mobId, currentHp, maxHp, callbacks);
        }

        private static (int CurrentHp, int MaxHp) ResolveBossHpTagValues(
            int mobId,
            int currentHp,
            int maxHp,
            PacketFieldFeedbackCallbacks callbacks)
        {
            if (mobId < HorntailBossHpFirstBodyMobId || mobId > HorntailBossHpLastBodyMobId)
            {
                return (currentHp, maxHp);
            }

            long totalMaxHp = 0;
            long completedPartHp = 0;
            for (int partMobId = HorntailBossHpFirstBodyMobId; partMobId <= HorntailBossHpLastBodyMobId; partMobId++)
            {
                int? partMaxHp = callbacks?.ResolveMobMaxHp?.Invoke(partMobId);
                if (partMaxHp.GetValueOrDefault() <= 0)
                {
                    return (currentHp, maxHp);
                }

                int scaledPartMaxHp = partMaxHp.Value / 1000;
                totalMaxHp += scaledPartMaxHp;
                if (partMobId > mobId)
                {
                    completedPartHp += scaledPartMaxHp;
                }
            }

            long scaledCurrentHp = (currentHp / 1000L) + completedPartHp;
            return (
                (int)Math.Clamp(scaledCurrentHp, int.MinValue, int.MaxValue),
                (int)Math.Clamp(totalMaxHp, 1L, int.MaxValue));
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
