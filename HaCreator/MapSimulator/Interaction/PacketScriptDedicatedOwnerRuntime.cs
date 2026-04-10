using System;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class PacketScriptDedicatedOwnerRuntime
    {
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
            snapshot = new PacketScriptDedicatedOwnerSnapshot(
                _activeOwner.Kind,
                _activeOwner.Title,
                _activeOwner.PromptText,
                _activeOwner.DetailText,
                _activeOwner.Choices,
                _activeOwner.Mode,
                _activeOwner.InitialSelectionId,
                selectedChoiceIndex);
            return true;
        }

        internal bool MoveSelection(int delta)
        {
            if (_activeOwner?.Choices == null || _activeOwner.Choices.Count == 0)
            {
                _selectedChoiceIndex = -1;
                return false;
            }

            int choiceCount = _activeOwner.Choices.Count;
            int currentIndex = NormalizeChoiceIndex(_activeOwner, _selectedChoiceIndex);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            int nextIndex = ((currentIndex + delta) % choiceCount + choiceCount) % choiceCount;
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
    }

    internal sealed record PacketScriptDedicatedOwnerSnapshot(
        PacketScriptMessageRuntime.PacketScriptDedicatedOwnerKind Kind,
        string Title,
        string PromptText,
        string DetailText,
        System.Collections.Generic.IReadOnlyList<NpcInteractionChoice> Choices,
        int Mode,
        int InitialSelectionId,
        int SelectedChoiceIndex);
}
