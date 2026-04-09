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

        private int _expiresAtTick;
        private string _title = string.Empty;
        private string _problemText = string.Empty;
        private string _hintText = string.Empty;
        private int _answer;
        private int _questionNumber;

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
                return "Packet-owned initial quiz idle.";
            }

            int remainingSeconds = (GetRemainingMs(currentTickCount) + 999) / 1000;
            return $"Packet-owned initial quiz active: question {_questionNumber}, {remainingSeconds}s remaining, title={FormatQuotedValue(_title)}.";
        }

        internal bool TryBuildOwnerSnapshot(int currentTickCount, out InitialQuizOwnerSnapshot snapshot)
        {
            if (!IsActive(currentTickCount))
            {
                snapshot = null;
                return false;
            }

            int remainingMs = GetRemainingMs(currentTickCount);
            snapshot = new InitialQuizOwnerSnapshot(
                _title,
                _problemText,
                _hintText,
                _answer,
                _questionNumber,
                (remainingMs + 999) / 1000,
                remainingMs);
            return true;
        }

        internal void Clear()
        {
            _expiresAtTick = 0;
            _title = string.Empty;
            _problemText = string.Empty;
            _hintText = string.Empty;
            _answer = 0;
            _questionNumber = 0;
        }

        internal string ApplyClientOwnerState(
            string title,
            string problemText,
            string hintText,
            int answer,
            int questionNumber,
            int remainingSeconds,
            int currentTickCount)
        {
            _title = title ?? string.Empty;
            _problemText = problemText ?? string.Empty;
            _hintText = hintText ?? string.Empty;
            _answer = answer;
            _questionNumber = Math.Max(0, questionNumber);
            _expiresAtTick = currentTickCount + (Math.Max(0, remainingSeconds) * 1000);
            return
                $"Synced packet-authored initial quiz owner: question {_questionNumber}, {Math.Max(0, remainingSeconds)}s remaining, title={FormatQuotedValue(_title)}.";
        }

        internal bool TryApplyPayload(byte[] payload, int currentTickCount, out string message)
        {
            payload ??= Array.Empty<byte>();
            if (payload.Length == 0)
            {
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
                    message = "Cleared the packet-owned initial quiz timer.";
                    return true;
                }

                if (mode != RequestMode)
                {
                    message = $"Initial-quiz payload used unsupported mode {mode}.";
                    return false;
                }

                message = ApplyClientOwnerState(
                    ReadMapleString(reader),
                    ReadMapleString(reader),
                    ReadMapleString(reader),
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    currentTickCount)
                    .Replace("Synced", "Started", StringComparison.Ordinal);
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is IOException || ex is ArgumentException)
            {
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
    }

    internal sealed record InitialQuizOwnerSnapshot(
        string Title,
        string ProblemText,
        string HintText,
        int CorrectAnswer,
        int QuestionNumber,
        int RemainingSeconds,
        int RemainingMs);
}
