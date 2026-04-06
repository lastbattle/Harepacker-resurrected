using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator
{
    public sealed class ImeCandidateWindowForm
    {
        public static readonly ImeCandidateWindowForm Empty = new(0, 0, 0, 0, 0, 0, 0);

        public ImeCandidateWindowForm(uint style, int currentX, int currentY, int areaX, int areaY, int areaWidth, int areaHeight)
        {
            Style = style;
            CurrentX = currentX;
            CurrentY = currentY;
            AreaX = areaX;
            AreaY = areaY;
            AreaWidth = Math.Max(0, areaWidth);
            AreaHeight = Math.Max(0, areaHeight);
        }

        public uint Style { get; }
        public int CurrentX { get; }
        public int CurrentY { get; }
        public int AreaX { get; }
        public int AreaY { get; }
        public int AreaWidth { get; }
        public int AreaHeight { get; }
        public bool HasPlacementData => Style != 0 || CurrentX != 0 || CurrentY != 0 || AreaWidth > 0 || AreaHeight > 0;
    }

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
        public static readonly ImeCandidateListState Empty = new(Array.Empty<string>(), 0, 0, -1, false, -1, ImeCandidateWindowForm.Empty);

        public ImeCandidateListState(
            IReadOnlyList<string> candidates,
            int pageStart,
            int pageSize,
            int selection,
            bool vertical,
            int listIndex = -1,
            ImeCandidateWindowForm windowForm = null)
        {
            Candidates = candidates ?? Array.Empty<string>();
            PageStart = Math.Max(0, pageStart);
            PageSize = Math.Max(0, pageSize);
            Selection = selection;
            Vertical = vertical;
            ListIndex = listIndex;
            WindowForm = windowForm ?? ImeCandidateWindowForm.Empty;
        }

        public IReadOnlyList<string> Candidates { get; }
        public int PageStart { get; }
        public int PageSize { get; }
        public int Selection { get; }
        public bool Vertical { get; }
        public int ListIndex { get; }
        public ImeCandidateWindowForm WindowForm { get; }
        public bool HasCandidates => Candidates.Count > 0;
    }
}
