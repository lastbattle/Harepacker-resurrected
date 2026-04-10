using System;
using System.IO;
using System.Text;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class InitialQuizTimerRuntime
    {
        internal const int PacketType = 43;
        private const byte RequestMode = 0;
        private const byte FailMode = 1;

        private int _boundCharacterId;
        private int _lastObservedRuntimeCharacterId;
        private int _expiresAtTick;
        private string _title = string.Empty;
        private string _problemText = string.Empty;
        private string _hintText = string.Empty;
        private int _minInputByteLength;
        private int _maxInputByteLength;

        internal int BoundCharacterId => _boundCharacterId;
        internal int LastObservedRuntimeCharacterId => _lastObservedRuntimeCharacterId;

        internal bool IsActive(int currentTickCount)
        {
            return GetRemainingMs(currentTickCount) > 0;
        }

        internal int GetRemainingMs(int currentTickCount)
        {
            int remainingMs = _expiresAtTick - currentTickCount;
            return remainingMs > 0 ? remainingMs : 0;
        }

        internal string GetRestrictionMessage(int currentTickCount)
        {
            return IsActive(currentTickCount)
                ? "Channel changes are blocked while the initial quiz is active."
                : null;
        }

        internal string DescribeStatus(int currentTickCount)
        {
            if (!IsActive(currentTickCount))
            {
                return DescribeIdleStatus();
            }

            int remainingSeconds = GetDisplayRemainingSeconds(currentTickCount);
            return $"Context-owned initial quiz active: inputBytes={_minInputByteLength}..{_maxInputByteLength}, {remainingSeconds}s remaining, title={FormatQuotedValue(_title)}, {DescribeCharacterBinding()}";
        }

        internal bool TryBuildOwnerSnapshot(int currentTickCount, out InitialQuizOwnerSnapshot snapshot)
        {
            if (_expiresAtTick <= 0)
            {
                snapshot = null;
                return false;
            }

            int remainingMs = GetRemainingMs(currentTickCount);
            snapshot = new InitialQuizOwnerSnapshot(
                _title,
                _problemText,
                _hintText,
                _minInputByteLength,
                _maxInputByteLength,
                GetDisplayRemainingSeconds(currentTickCount),
                remainingMs);
            return true;
        }

        internal int GetDisplayRemainingSeconds(int currentTickCount)
        {
            return Math.Max(0, GetRemainingMs(currentTickCount) / 1000);
        }

        internal void Clear()
        {
            _boundCharacterId = 0;
            _lastObservedRuntimeCharacterId = 0;
            _expiresAtTick = 0;
            _title = string.Empty;
            _problemText = string.Empty;
            _hintText = string.Empty;
            _minInputByteLength = 0;
            _maxInputByteLength = 0;
        }

        internal string ApplyClientOwnerState(
            string title,
            string problemText,
            string hintText,
            int minInputLength,
            int maxInputLength,
            int remainingSeconds,
            int currentTickCount,
            int runtimeCharacterId)
        {
            BindRuntimeCharacter(runtimeCharacterId);
            _title = title ?? string.Empty;
            _problemText = problemText ?? string.Empty;
            _hintText = hintText ?? string.Empty;
            _minInputByteLength = Math.Max(0, minInputLength) * 2;
            _maxInputByteLength = Math.Max(0, maxInputLength) * 2;
            _expiresAtTick = currentTickCount + (Math.Max(0, remainingSeconds) * 1000);
            return
                $"Synced context-owned initial quiz owner: inputBytes={_minInputByteLength}..{_maxInputByteLength}, {Math.Max(0, remainingSeconds)}s remaining, title={FormatQuotedValue(_title)}, {DescribeCharacterBinding()}";
        }

        internal bool TryApplyClientOwnerState(
            string title,
            string problemText,
            string hintText,
            int minInputLength,
            int maxInputLength,
            int remainingSeconds,
            int currentTickCount,
            int runtimeCharacterId,
            out InitialQuizOwnerApplyDisposition disposition,
            out string message)
        {
            if (HasLiveOwner(currentTickCount))
            {
                disposition = InitialQuizOwnerApplyDisposition.IgnoredReopen;
                message =
                    $"Ignored context-owned initial quiz reopen while the existing owner is still alive: " +
                    $"client `CWvsContext::OnInitialQuiz` only seeds `CUIInitialQuiz` during singleton creation.";
                return true;
            }

            disposition = InitialQuizOwnerApplyDisposition.Started;
            message = ApplyClientOwnerState(
                title,
                problemText,
                hintText,
                minInputLength,
                maxInputLength,
                remainingSeconds,
                currentTickCount,
                runtimeCharacterId)
                .Replace("Synced", "Started", StringComparison.Ordinal);
            return true;
        }

        internal bool TryApplyPayload(
            byte[] payload,
            int currentTickCount,
            int runtimeCharacterId,
            out InitialQuizOwnerApplyDisposition disposition,
            out string message)
        {
            payload ??= Array.Empty<byte>();
            if (payload.Length == 0)
            {
                disposition = InitialQuizOwnerApplyDisposition.None;
                message = "Initial-quiz payload is empty.";
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
                byte mode = reader.ReadByte();
                if (mode == FailMode)
                {
                    Clear();
                    disposition = InitialQuizOwnerApplyDisposition.Cleared;
                    message = "Cleared the context-owned initial quiz owner.";
                    return true;
                }

                if (mode != RequestMode)
                {
                    disposition = InitialQuizOwnerApplyDisposition.None;
                    message = $"Initial-quiz payload used unsupported mode {mode}.";
                    return false;
                }

                string title = ReadMapleString(reader);
                string problemText = ReadMapleString(reader);
                string hintText = ReadMapleString(reader);
                int minInputLength = reader.ReadInt32();
                int maxInputLength = reader.ReadInt32();
                int remainingSeconds = reader.ReadInt32();

                return TryApplyClientOwnerState(
                    title,
                    problemText,
                    hintText,
                    minInputLength,
                    maxInputLength,
                    remainingSeconds,
                    currentTickCount,
                    runtimeCharacterId,
                    out disposition,
                    out message);
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is IOException || ex is ArgumentException)
            {
                disposition = InitialQuizOwnerApplyDisposition.None;
                message = $"Initial-quiz payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private static string ReadMapleString(BinaryReader reader)
        {
            short length = reader.ReadInt16();
            if (length <= 0)
            {
                return string.Empty;
            }

            byte[] bytes = reader.ReadBytes(length);
            return Encoding.Default.GetString(bytes);
        }

        private static string FormatQuotedValue(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "\"\""
                : $"\"{value.Trim()}\"";
        }

        private bool HasLiveOwner(int currentTickCount)
        {
            return GetRemainingMs(currentTickCount) > 0;
        }

        internal void ObserveRuntimeCharacterId(int runtimeCharacterId)
        {
            _lastObservedRuntimeCharacterId = NormalizeCharacterId(runtimeCharacterId);
        }

        internal bool RequiresCharacterReset(int runtimeCharacterId)
        {
            int resolvedCharacterId = NormalizeCharacterId(runtimeCharacterId);
            return _boundCharacterId > 0
                && resolvedCharacterId > 0
                && _boundCharacterId != resolvedCharacterId;
        }

        internal string ResetForRuntimeCharacterChange(int runtimeCharacterId)
        {
            int previousCharacterId = _boundCharacterId;
            int resolvedCharacterId = NormalizeCharacterId(runtimeCharacterId);
            Clear();
            _lastObservedRuntimeCharacterId = resolvedCharacterId;
            return $"Cleared context-owned initial quiz owner after runtime character changed from {previousCharacterId} to {resolvedCharacterId}.";
        }

        private void BindRuntimeCharacter(int runtimeCharacterId)
        {
            int resolvedCharacterId = NormalizeCharacterId(runtimeCharacterId);
            _lastObservedRuntimeCharacterId = resolvedCharacterId;
            if (resolvedCharacterId > 0)
            {
                _boundCharacterId = resolvedCharacterId;
            }
        }

        private string DescribeIdleStatus()
        {
            return _lastObservedRuntimeCharacterId > 0
                ? $"Context-owned initial quiz idle. lastRuntimeCharacter={_lastObservedRuntimeCharacterId}."
                : "Context-owned initial quiz idle.";
        }

        private string DescribeCharacterBinding()
        {
            string boundCharacter = _boundCharacterId > 0 ? _boundCharacterId.ToString() : "unset";
            string runtimeCharacter = _lastObservedRuntimeCharacterId > 0 ? _lastObservedRuntimeCharacterId.ToString() : "unset";
            return $"boundCharacter={boundCharacter}, runtimeCharacter={runtimeCharacter}.";
        }

        private static int NormalizeCharacterId(int runtimeCharacterId)
        {
            return Math.Max(0, runtimeCharacterId);
        }
    }

    internal sealed record InitialQuizOwnerSnapshot(
        string Title,
        string ProblemText,
        string HintText,
        int MinInputByteLength,
        int MaxInputByteLength,
        int RemainingSeconds,
        int RemainingMs);

    internal enum InitialQuizOwnerApplyDisposition
    {
        None,
        Started,
        IgnoredReopen,
        Cleared
    }
}
