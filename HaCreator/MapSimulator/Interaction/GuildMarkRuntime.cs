using System;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class GuildMarkRuntime
    {
        internal const int IntroAnimationDurationMs = 3180;
        private readonly GuildMarkCatalogData _catalog = GuildMarkCatalog.GetCatalog();

        private bool _isOpen;
        private int _elapsedMs;
        private GuildMarkSelection _selection = new(1000, 1, 2000, 1, 0);
        private GuildMarkSelection? _committedSelection;
        private int _comboIndex;
        private string _statusMessage = "Guild mark dialog is idle.";

        internal string Open()
        {
            _isOpen = true;
            _elapsedMs = 0;
            _selection = _committedSelection ?? new GuildMarkSelection(
                _catalog.DefaultBackgroundId,
                1,
                _catalog.DefaultMarkId,
                1,
                _catalog.ResolveFamilyIndex(_catalog.DefaultMarkId));
            _comboIndex = _selection.ComboIndex;
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

            _selection = _selection with
            {
                MarkBackground = _catalog.MoveBackground(_selection.MarkBackground, delta)
            };
            return BuildSelectionMessage("background");
        }

        internal string MoveBackgroundColor(int delta)
        {
            if (!IsInteractive)
            {
                return "Guild mark controls unlock after the intro animation finishes.";
            }

            _selection = _selection with
            {
                MarkBackgroundColor = Wrap(_selection.MarkBackgroundColor + delta, 1, 16)
            };
            return BuildSelectionMessage("background color");
        }

        internal string MoveMark(int delta)
        {
            if (!IsInteractive)
            {
                return "Guild mark controls unlock after the intro animation finishes.";
            }

            _selection = _selection with
            {
                Mark = _catalog.MoveMark(_selection.Mark, _comboIndex, delta)
            };
            return BuildSelectionMessage("symbol");
        }

        internal string MoveMarkColor(int delta)
        {
            if (!IsInteractive)
            {
                return "Guild mark controls unlock after the intro animation finishes.";
            }

            _selection = _selection with
            {
                MarkColor = Wrap(_selection.MarkColor + delta, 1, 16)
            };
            return BuildSelectionMessage("symbol color");
        }

        internal string CycleCombo()
        {
            if (!IsInteractive)
            {
                return "Guild mark controls unlock after the intro animation finishes.";
            }

            _comboIndex = (_comboIndex + 1) % Math.Max(1, _catalog.Families.Count);
            GuildMarkFamilyInfo family = _catalog.ResolveFamilyByIndex(_comboIndex);
            int mark = family.MarkIds.Count > 0 && family.MarkIds.Contains(_selection.Mark)
                ? _selection.Mark
                : family.MarkIds[0];
            _selection = _selection with
            {
                Mark = mark,
                ComboIndex = _comboIndex
            };

            _statusMessage = $"Guild mark symbol family switched to {family.Label} (group {family.Group}).";
            return _statusMessage;
        }

        internal string Confirm()
        {
            if (!_isOpen)
            {
                return "No guild-mark dialog is active.";
            }

            _isOpen = false;
            _committedSelection = _selection with { ComboIndex = _comboIndex };
            _statusMessage = $"Accepted guild mark selection bg={_selection.MarkBackground}:{_selection.MarkBackgroundColor}, mark={_selection.Mark}:{_selection.MarkColor}. The dialog now reuses the committed emblem across the guild owners, but server-backed persistence and guild-cost commit still remain outside the simulator.";
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
            GuildMarkFamilyInfo family = _catalog.ResolveFamilyByIndex(_comboIndex);
            return $"Guild mark dialog {(_isOpen ? (IsInteractive ? "interactive" : "intro") : "idle")}: bg={_selection.MarkBackground}:{_selection.MarkBackgroundColor}, mark={_selection.Mark}:{_selection.MarkColor}, family={family.Label}. {_statusMessage}";
        }

        internal GuildMarkSnapshot BuildSnapshot()
        {
            GuildMarkFamilyInfo family = _catalog.ResolveFamilyByIndex(_comboIndex);
            return new GuildMarkSnapshot
            {
                IsOpen = _isOpen,
                IsInteractive = IsInteractive,
                ComboLabel = family.Label,
                ComboGroup = family.Group,
                MarkBackground = _selection.MarkBackground,
                MarkBackgroundColor = _selection.MarkBackgroundColor,
                Mark = _selection.Mark,
                MarkColor = _selection.MarkColor,
                BackgroundName = _catalog.ResolveBackgroundName(_selection.MarkBackground),
                MarkName = _catalog.ResolveMarkName(_selection.Mark),
                IntroRemainingMs = Math.Max(0, IntroAnimationDurationMs - _elapsedMs),
                StatusMessage = _statusMessage
            };
        }

        internal GuildMarkSelection? GetCommittedSelection()
        {
            return _committedSelection;
        }

        private bool IsInteractive => _isOpen && _elapsedMs >= IntroAnimationDurationMs;

        private string BuildSelectionMessage(string changedPart)
        {
            _statusMessage = $"Guild mark {changedPart} updated to bg={_selection.MarkBackground}:{_selection.MarkBackgroundColor}, mark={_selection.Mark}:{_selection.MarkColor}.";
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
        public string BackgroundName { get; init; } = string.Empty;
        public string MarkName { get; init; } = string.Empty;
        public string StatusMessage { get; init; } = string.Empty;
    }
}
