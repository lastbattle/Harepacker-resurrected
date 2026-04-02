using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class GuildMarkRuntime
    {
        internal const int IntroAnimationDurationMs = 3180;
        private static readonly int[] ComboGroups = { 2, 3, 4, 5, 9 };
        private static readonly string[] ComboLabels =
        {
            "Basic symbols",
            "Shield symbols",
            "Crest symbols",
            "Ornament symbols",
            "Legacy symbols"
        };

        private readonly Dictionary<int, int> _lastMarkNumbersByGroup = new()
        {
            [2] = 2007,
            [3] = 3007,
            [4] = 4007,
            [5] = 5007,
            [9] = 9007
        };

        private bool _isOpen;
        private int _elapsedMs;
        private int _markBackground = 1000;
        private int _markBackgroundColor = 1;
        private int _mark = 2000;
        private int _markColor = 1;
        private int _comboIndex;
        private string _statusMessage = "Guild mark dialog is idle.";

        internal string Open()
        {
            _isOpen = true;
            _elapsedMs = 0;
            _markBackground = 1000;
            _markBackgroundColor = 1;
            _mark = 2000;
            _markColor = 1;
            _comboIndex = 0;
            _statusMessage = "Opened the dedicated guild-mark dialog. Intro timing follows the WZ-backed animation plus the client-owned post-animation delay before controls unlock.";
            return _statusMessage;
        }

        internal void Advance(int elapsedMs)
        {
            if (!_isOpen || elapsedMs <= 0)
            {
                return;
            }

            _elapsedMs = Math.Min(IntroAnimationDurationMs, _elapsedMs + elapsedMs);
        }

        internal string MoveBackground(int delta)
        {
            if (!IsInteractive)
            {
                return "Guild mark controls unlock after the intro animation finishes.";
            }

            _markBackground = Wrap(_markBackground + delta, 1000, 1015);
            return BuildSelectionMessage("background");
        }

        internal string MoveBackgroundColor(int delta)
        {
            if (!IsInteractive)
            {
                return "Guild mark controls unlock after the intro animation finishes.";
            }

            _markBackgroundColor = Wrap(_markBackgroundColor + delta, 1, 16);
            return BuildSelectionMessage("background color");
        }

        internal string MoveMark(int delta)
        {
            if (!IsInteractive)
            {
                return "Guild mark controls unlock after the intro animation finishes.";
            }

            int group = ComboGroups[_comboIndex];
            int min = group * 1000;
            int max = _lastMarkNumbersByGroup[group];
            _mark = Wrap(_mark + delta, min, max);
            return BuildSelectionMessage("symbol");
        }

        internal string MoveMarkColor(int delta)
        {
            if (!IsInteractive)
            {
                return "Guild mark controls unlock after the intro animation finishes.";
            }

            _markColor = Wrap(_markColor + delta, 1, 16);
            return BuildSelectionMessage("symbol color");
        }

        internal string CycleCombo()
        {
            if (!IsInteractive)
            {
                return "Guild mark controls unlock after the intro animation finishes.";
            }

            _comboIndex = (_comboIndex + 1) % ComboGroups.Length;
            int group = ComboGroups[_comboIndex];
            _mark = Math.Clamp(_mark, group * 1000, _lastMarkNumbersByGroup[group]);
            if (_mark / 1000 != group)
            {
                _mark = group * 1000;
            }

            _statusMessage = $"Guild mark symbol family switched to {ComboLabels[_comboIndex]} (group {group}).";
            return _statusMessage;
        }

        internal string Confirm()
        {
            if (!_isOpen)
            {
                return "No guild-mark dialog is active.";
            }

            _isOpen = false;
            _statusMessage = $"Accepted guild mark selection bg={_markBackground}:{_markBackgroundColor}, mark={_mark}:{_markColor}. Packet-backed persistence and the real guild-cost commit still remain outside the simulator.";
            return _statusMessage;
        }

        internal string Cancel()
        {
            if (!_isOpen)
            {
                return "No guild-mark dialog is active.";
            }

            _isOpen = false;
            _statusMessage = "Canceled guild mark editing.";
            return _statusMessage;
        }

        internal string DescribeStatus()
        {
            return $"Guild mark dialog {(_isOpen ? (IsInteractive ? "interactive" : "intro") : "idle")}: bg={_markBackground}:{_markBackgroundColor}, mark={_mark}:{_markColor}, family={ComboLabels[_comboIndex]}. {_statusMessage}";
        }

        internal GuildMarkSnapshot BuildSnapshot()
        {
            return new GuildMarkSnapshot
            {
                IsOpen = _isOpen,
                IsInteractive = IsInteractive,
                ComboLabel = ComboLabels[_comboIndex],
                ComboGroup = ComboGroups[_comboIndex],
                MarkBackground = _markBackground,
                MarkBackgroundColor = _markBackgroundColor,
                Mark = _mark,
                MarkColor = _markColor,
                IntroRemainingMs = Math.Max(0, IntroAnimationDurationMs - _elapsedMs),
                StatusMessage = _statusMessage
            };
        }

        private bool IsInteractive => _isOpen && _elapsedMs >= IntroAnimationDurationMs;

        private string BuildSelectionMessage(string changedPart)
        {
            _statusMessage = $"Guild mark {changedPart} updated to bg={_markBackground}:{_markBackgroundColor}, mark={_mark}:{_markColor}.";
            return _statusMessage;
        }

        private static int Wrap(int value, int minimum, int maximum)
        {
            if (value < minimum)
            {
                return maximum;
            }

            if (value > maximum)
            {
                return minimum;
            }

            return value;
        }
    }

    internal sealed class GuildMarkSnapshot
    {
        public bool IsOpen { get; init; }
        public bool IsInteractive { get; init; }
        public int MarkBackground { get; init; }
        public int MarkBackgroundColor { get; init; }
        public int Mark { get; init; }
        public int MarkColor { get; init; }
        public int ComboGroup { get; init; }
        public int IntroRemainingMs { get; init; }
        public string ComboLabel { get; init; } = string.Empty;
        public string StatusMessage { get; init; } = string.Empty;
    }
}
