using HaCreator.MapSimulator.Character;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class LoginCharacterRosterEntry
    {
        public LoginCharacterRosterEntry(
            CharacterBuild build,
            int fieldMapId,
            string fieldDisplayName,
            bool canDelete = true,
            int? previousWorldRank = null,
            int? previousJobRank = null,
            LoginAvatarLook avatarLook = null,
            byte[] avatarLookPacket = null,
            int portal = 0)
        {
            Build = build ?? throw new ArgumentNullException(nameof(build));
            FieldMapId = fieldMapId;
            FieldDisplayName = fieldDisplayName ?? string.Empty;
            CanDelete = canDelete;
            PreviousWorldRank = previousWorldRank;
            PreviousJobRank = previousJobRank;
            AvatarLook = LoginAvatarLookCodec.CloneLook(avatarLook);
            AvatarLookPacket = avatarLookPacket != null ? (byte[])avatarLookPacket.Clone() : null;
            Portal = portal;
        }

        public CharacterBuild Build { get; }
        public int FieldMapId { get; }
        public string FieldDisplayName { get; }
        public bool CanDelete { get; }
        public int? PreviousWorldRank { get; }
        public int? PreviousJobRank { get; }
        public LoginAvatarLook AvatarLook { get; }
        public byte[] AvatarLookPacket { get; }
        public int Portal { get; }

        public CharacterBuild CreateRuntimeBuild(CharacterLoader loader)
        {
            if (loader != null && AvatarLook != null)
            {
                return loader.LoadFromAvatarLook(AvatarLook, Build);
            }

            if (loader != null &&
                AvatarLookPacket?.Length > 0 &&
                LoginAvatarLookCodec.TryDecode(AvatarLookPacket, out LoginAvatarLook avatarLook, out _))
            {
                return loader.LoadFromAvatarLook(avatarLook, Build);
            }

            return Build.Clone();
        }
    }

    /// <summary>
    /// Owns the simulator-side roster list and request validation used by the login shell.
    /// </summary>
    public sealed class LoginCharacterRosterManager
    {
        public const int EntriesPerPage = 3;
        public const int MaxCharacterSlotCount = 15;

        private readonly List<LoginCharacterRosterEntry> _entries = new();

        public IReadOnlyList<LoginCharacterRosterEntry> Entries => _entries;

        public int SelectedIndex { get; private set; } = -1;
        public int SlotCount { get; private set; }
        public int BuyCharacterCount { get; private set; }
        public int PageIndex { get; private set; }
        public int PageCount => DisplaySlotCount == 0 ? 0 : ((DisplaySlotCount - 1) / EntriesPerPage) + 1;
        public int DisplaySlotCount => Math.Max(_entries.Count, SlotCount + BuyCharacterCount);
        public int EmptySlotCount => Math.Max(0, SlotCount - _entries.Count);

        public LoginCharacterRosterEntry SelectedEntry =>
            SelectedIndex >= 0 && SelectedIndex < _entries.Count
                ? _entries[SelectedIndex]
                : null;

        public void SetEntries(
            IEnumerable<LoginCharacterRosterEntry> entries,
            int slotCount = 0,
            int buyCharacterCount = 0)
        {
            _entries.Clear();
            if (entries != null)
            {
                _entries.AddRange(entries.Where(entry => entry?.Build != null));
            }

            SelectedIndex = _entries.Count > 0 ? 0 : -1;
            SlotCount = NormalizeSlotCount(slotCount, _entries.Count);
            BuyCharacterCount = NormalizeBuyCharacterCount(buyCharacterCount);
            PageIndex = GetPageIndexForSelection(SelectedIndex);
        }

        public bool Select(int index)
        {
            if (index < 0 || index >= _entries.Count)
            {
                return false;
            }

            SelectedIndex = index;
            PageIndex = GetPageIndexForSelection(index);
            return true;
        }

        public bool SelectPage(int pageIndex)
        {
            if (PageCount <= 0)
            {
                return false;
            }

            int normalizedPageIndex = ((pageIndex % PageCount) + PageCount) % PageCount;
            int pageStartIndex = normalizedPageIndex * EntriesPerPage;
            int pageEntryCount = Math.Clamp(_entries.Count - pageStartIndex, 0, EntriesPerPage);

            PageIndex = normalizedPageIndex;
            if (pageEntryCount > 0)
            {
                SelectedIndex = pageStartIndex;
            }
            else
            {
                SelectedIndex = -1;
            }

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

            PageIndex = GetPageIndexForSelection(SelectedIndex);

            return true;
        }

        public LoginCharacterRosterSlotKind GetDisplaySlotKind(int displayIndex)
        {
            if (displayIndex < 0 || displayIndex >= DisplaySlotCount)
            {
                return LoginCharacterRosterSlotKind.Hidden;
            }

            if (displayIndex < _entries.Count)
            {
                return LoginCharacterRosterSlotKind.Character;
            }

            if (displayIndex < SlotCount)
            {
                return LoginCharacterRosterSlotKind.Empty;
            }

            if (displayIndex < SlotCount + BuyCharacterCount)
            {
                return LoginCharacterRosterSlotKind.BuyCharacter;
            }

            return LoginCharacterRosterSlotKind.Hidden;
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

        private int GetPageIndexForSelection(int selectedIndex)
        {
            if (selectedIndex < 0 || DisplaySlotCount <= 0)
            {
                return 0;
            }

            return Math.Clamp(selectedIndex / EntriesPerPage, 0, Math.Max(0, PageCount - 1));
        }

        private static int NormalizeSlotCount(int slotCount, int entryCount)
        {
            int normalized = slotCount > 0 ? slotCount : entryCount;
            return Math.Clamp(normalized, 0, MaxCharacterSlotCount);
        }

        private static int NormalizeBuyCharacterCount(int buyCharacterCount)
        {
            return Math.Clamp(buyCharacterCount, 0, MaxCharacterSlotCount);
        }
    }

    public enum LoginCharacterRosterSlotKind
    {
        Hidden = 0,
        Character = 1,
        Empty = 2,
        BuyCharacter = 3
    }
}
