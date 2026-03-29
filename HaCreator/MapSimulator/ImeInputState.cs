using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator
{
    public sealed class ImeCompositionState
    {
        public static readonly ImeCompositionState Empty = new(string.Empty, Array.Empty<int>(), -1);

        public ImeCompositionState(string text, IReadOnlyList<int> clauseOffsets, int cursorPosition)
        {
            Text = text ?? string.Empty;
            ClauseOffsets = clauseOffsets ?? Array.Empty<int>();
            CursorPosition = cursorPosition;
        }

        public string Text { get; }
        public IReadOnlyList<int> ClauseOffsets { get; }
        public int CursorPosition { get; }
        public bool HasText => Text.Length > 0;
    }

    public sealed class ImeCandidateListState
    {
        public static readonly ImeCandidateListState Empty = new(Array.Empty<string>(), 0, 0, -1, false);

        public ImeCandidateListState(
            IReadOnlyList<string> candidates,
            int pageStart,
            int pageSize,
            int selection,
            bool vertical)
        {
            Candidates = candidates ?? Array.Empty<string>();
            PageStart = Math.Max(0, pageStart);
            PageSize = Math.Max(0, pageSize);
            Selection = selection;
            Vertical = vertical;
        }

        public IReadOnlyList<string> Candidates { get; }
        public int PageStart { get; }
        public int PageSize { get; }
        public int Selection { get; }
        public bool Vertical { get; }
        public bool HasCandidates => Candidates.Count > 0;
    }
}
