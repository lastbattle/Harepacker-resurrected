using HaCreator.MapSimulator.Character;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class LoginCharacterRosterEntry
    {
        public LoginCharacterRosterEntry(CharacterBuild build, int fieldMapId, bool canDelete = true)
        {
            Build = build ?? throw new ArgumentNullException(nameof(build));
            FieldMapId = fieldMapId;
            CanDelete = canDelete;
        }

        public CharacterBuild Build { get; }
        public int FieldMapId { get; }
        public bool CanDelete { get; }
    }

    /// <summary>
    /// Owns the simulator-side roster list and request validation used by the login shell.
    /// </summary>
    public sealed class LoginCharacterRosterManager
    {
        private readonly List<LoginCharacterRosterEntry> _entries = new();

        public IReadOnlyList<LoginCharacterRosterEntry> Entries => _entries;

        public int SelectedIndex { get; private set; } = -1;

        public LoginCharacterRosterEntry SelectedEntry =>
            SelectedIndex >= 0 && SelectedIndex < _entries.Count
                ? _entries[SelectedIndex]
                : null;

        public void SetEntries(IEnumerable<LoginCharacterRosterEntry> entries)
        {
            _entries.Clear();
            if (entries != null)
            {
                _entries.AddRange(entries.Where(entry => entry?.Build != null));
            }

            SelectedIndex = _entries.Count > 0 ? 0 : -1;
        }

        public bool Select(int index)
        {
            if (index < 0 || index >= _entries.Count)
            {
                return false;
            }

            SelectedIndex = index;
            return true;
        }

        public bool DeleteSelected(out LoginCharacterRosterEntry deletedEntry)
        {
            deletedEntry = SelectedEntry;
            if (deletedEntry == null || !deletedEntry.CanDelete)
            {
                return false;
            }

            _entries.RemoveAt(SelectedIndex);
            if (_entries.Count == 0)
            {
                SelectedIndex = -1;
            }
            else if (SelectedIndex >= _entries.Count)
            {
                SelectedIndex = _entries.Count - 1;
            }

            return true;
        }

        public bool CanRequestSelection(LoginRuntimeManager runtime, out string message)
        {
            if (_entries.Count == 0)
            {
                message = "Character roster is empty.";
                return false;
            }

            if (SelectedEntry == null)
            {
                message = "Select a character first.";
                return false;
            }

            if (runtime == null)
            {
                message = "Login runtime is unavailable.";
                return false;
            }

            if (runtime.CurrentStep != LoginStep.CharacterSelect &&
                runtime.CurrentStep != LoginStep.ViewAllCharacters)
            {
                message = "Character selection is only available from the roster step.";
                return false;
            }

            if (!runtime.CharacterSelectReady)
            {
                message = "Character selection is waiting for SelectWorldResult.";
                return false;
            }

            if (runtime.PendingStep.HasValue)
            {
                message = $"Login step change to {runtime.PendingStep.Value} is still pending.";
                return false;
            }

            if (runtime.FieldEntryRequested)
            {
                message = "Field entry has already been requested.";
                return false;
            }

            message = $"Ready to enter with {SelectedEntry.Build.Name}.";
            return true;
        }
    }
}
