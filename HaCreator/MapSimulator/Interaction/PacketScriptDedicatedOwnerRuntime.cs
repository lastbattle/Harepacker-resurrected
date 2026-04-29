using System;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class PacketScriptDedicatedOwnerRuntime
    {
        private const int SlideMenuExPageSize = 8;

        private PacketScriptMessageRuntime.PacketScriptDedicatedOwnerRequest _activeOwner;
        private int _selectedChoiceIndex = -1;

        internal bool IsActive => _activeOwner != null;

        internal void Clear()
        {
            _activeOwner = null;
            _selectedChoiceIndex = -1;
        }

        internal void Open(PacketScriptMessageRuntime.PacketScriptDedicatedOwnerRequest owner)
        {
            _activeOwner = owner;
            _selectedChoiceIndex = ResolveInitialSelectionIndex(owner);
        }

        internal bool TryBuildSnapshot(out PacketScriptDedicatedOwnerSnapshot snapshot)
        {
            if (_activeOwner == null)
            {
                snapshot = null;
                return false;
            }

            int selectedChoiceIndex = NormalizeChoiceIndex(_activeOwner, _selectedChoiceIndex);
            int pageSize = ResolvePageSize(_activeOwner);
            int currentPage = ResolveCurrentPage(selectedChoiceIndex, pageSize);
            int pageStartIndex = ResolvePageStartIndex(selectedChoiceIndex, pageSize);
            int pageChoiceCount = ResolvePageChoiceCount(_activeOwner.Choices.Count, pageStartIndex, pageSize);
            snapshot = new PacketScriptDedicatedOwnerSnapshot(
                _activeOwner.Kind,
                _activeOwner.Title,
                _activeOwner.PromptText,
                _activeOwner.DetailText,
                _activeOwner.Choices,
                _activeOwner.Mode,
                _activeOwner.InitialSelectionId,
                selectedChoiceIndex,
                pageSize,
                currentPage,
                pageStartIndex,
                pageChoiceCount);
            return true;
        }

        internal bool MoveSelection(int delta)
        {
            if (_activeOwner?.Choices == null || _activeOwner.Choices.Count == 0)
            {
                _selectedChoiceIndex = -1;
                return false;
            }

            int currentIndex = NormalizeChoiceIndex(_activeOwner, _selectedChoiceIndex);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            int nextIndex = IsSlideMenuEx(_activeOwner)
                ? ResolveSlideMenuPageMoveIndex(_activeOwner, currentIndex, delta)
                : ResolveWrappedMoveIndex(_activeOwner.Choices.Count, currentIndex, delta);
            _selectedChoiceIndex = nextIndex;
            return true;
        }

        internal bool SetSelectedIndex(int index)
        {
            if (_activeOwner?.Choices == null || index < 0 || index >= _activeOwner.Choices.Count)
            {
                return false;
            }

            _selectedChoiceIndex = index;
            return true;
        }

        internal bool SetSelectedPageOffset(int pageOffset)
        {
            if (_activeOwner?.Choices == null || _activeOwner.Choices.Count == 0)
            {
                return false;
            }

            int selectedChoiceIndex = NormalizeChoiceIndex(_activeOwner, _selectedChoiceIndex);
            int pageSize = ResolvePageSize(_activeOwner);
            int pageStartIndex = ResolvePageStartIndex(selectedChoiceIndex, pageSize);
            int pageChoiceCount = ResolvePageChoiceCount(_activeOwner.Choices.Count, pageStartIndex, pageSize);
            if (pageOffset < 0 || pageOffset >= pageChoiceCount)
            {
                return false;
            }

            _selectedChoiceIndex = pageStartIndex + pageOffset;
            return true;
        }

        internal bool TryGetSelectedChoice(out NpcInteractionChoice choice)
        {
            choice = null;
            if (_activeOwner?.Choices == null || _activeOwner.Choices.Count == 0)
            {
                return false;
            }

            int index = NormalizeChoiceIndex(_activeOwner, _selectedChoiceIndex);
            if (index < 0 || index >= _activeOwner.Choices.Count)
            {
                return false;
            }

            choice = _activeOwner.Choices[index];
            return choice != null;
        }

        internal string DescribeStatus()
        {
            if (_activeOwner == null)
            {
                return "Packet-script dedicated owner idle.";
            }

            int selectedIndex = NormalizeChoiceIndex(_activeOwner, _selectedChoiceIndex);
            string selectedLabel =
                selectedIndex >= 0 && selectedIndex < _activeOwner.Choices.Count
                    ? _activeOwner.Choices[selectedIndex]?.Label ?? "(null)"
                    : "(none)";
            int pageSize = ResolvePageSize(_activeOwner);
            int currentPage = ResolveCurrentPage(selectedIndex, pageSize);
            string pageStatus = pageSize > 0
                ? $", page={currentPage + 1}/{ResolvePageCount(_activeOwner.Choices.Count, pageSize)}"
                : string.Empty;
            return
                $"Packet-script dedicated owner {_activeOwner.Kind}: title=\"{_activeOwner.Title}\", " +
                $"choices={_activeOwner.Choices.Count}, selected={selectedIndex}{pageStatus}, label=\"{selectedLabel}\".";
        }

        private static int ResolveInitialSelectionIndex(PacketScriptMessageRuntime.PacketScriptDedicatedOwnerRequest owner)
        {
            if (owner?.Choices == null || owner.Choices.Count == 0)
            {
                return -1;
            }

            for (int i = 0; i < owner.Choices.Count; i++)
            {
                NpcInteractionChoice choice = owner.Choices[i];
                if (choice?.SubmissionNumericValue == owner.InitialSelectionId)
                {
                    return i;
                }
            }

            return 0;
        }

        private static int NormalizeChoiceIndex(PacketScriptMessageRuntime.PacketScriptDedicatedOwnerRequest owner, int index)
        {
            if (owner?.Choices == null || owner.Choices.Count == 0)
            {
                return -1;
            }

            return index >= 0 && index < owner.Choices.Count
                ? index
                : ResolveInitialSelectionIndex(owner);
        }

        private static int ResolveWrappedMoveIndex(int choiceCount, int currentIndex, int delta)
        {
            return ((currentIndex + delta) % choiceCount + choiceCount) % choiceCount;
        }

        private static int ResolveSlideMenuPageMoveIndex(PacketScriptMessageRuntime.PacketScriptDedicatedOwnerRequest owner, int currentIndex, int delta)
        {
            int choiceCount = owner?.Choices?.Count ?? 0;
            if (choiceCount <= 0)
            {
                return -1;
            }

            int currentPage = Math.Max(0, currentIndex / SlideMenuExPageSize);
            int pageCount = ResolvePageCount(choiceCount, SlideMenuExPageSize);
            int nextPage = Math.Clamp(currentPage + Math.Sign(delta), 0, Math.Max(0, pageCount - 1));
            return Math.Min(choiceCount - 1, nextPage * SlideMenuExPageSize);
        }

        private static int ResolveCurrentPage(int selectedChoiceIndex, int pageSize)
        {
            return selectedChoiceIndex >= 0 && pageSize > 0
                ? selectedChoiceIndex / pageSize
                : 0;
        }

        private static int ResolvePageStartIndex(int selectedChoiceIndex, int pageSize)
        {
            return selectedChoiceIndex >= 0 && pageSize > 0
                ? (selectedChoiceIndex / pageSize) * pageSize
                : 0;
        }

        private static int ResolvePageChoiceCount(int choiceCount, int pageStartIndex, int pageSize)
        {
            if (choiceCount <= 0)
            {
                return 0;
            }

            if (pageSize <= 0)
            {
                return choiceCount;
            }

            return Math.Clamp(choiceCount - Math.Max(0, pageStartIndex), 0, pageSize);
        }

        private static int ResolvePageCount(int choiceCount, int pageSize)
        {
            return choiceCount > 0 && pageSize > 0
                ? ((choiceCount - 1) / pageSize) + 1
                : 0;
        }

        private static int ResolvePageSize(PacketScriptMessageRuntime.PacketScriptDedicatedOwnerRequest owner)
        {
            return IsSlideMenuEx(owner) ? SlideMenuExPageSize : 0;
        }

        private static bool IsSlideMenuEx(PacketScriptMessageRuntime.PacketScriptDedicatedOwnerRequest owner)
        {
            return owner?.Kind == PacketScriptMessageRuntime.PacketScriptDedicatedOwnerKind.SlideMenu &&
                   owner.Mode == 0;
        }
    }

    internal sealed record PacketScriptDedicatedOwnerSnapshot(
        PacketScriptMessageRuntime.PacketScriptDedicatedOwnerKind Kind,
        string Title,
        string PromptText,
        string DetailText,
        System.Collections.Generic.IReadOnlyList<NpcInteractionChoice> Choices,
        int Mode,
        int InitialSelectionId,
        int SelectedChoiceIndex,
        int PageSize,
        int CurrentPage,
        int PageStartIndex,
        int PageChoiceCount);
}
