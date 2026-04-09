using System;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator.UI
{
    internal readonly record struct SkillMacroImeCandidateWindowMetrics(int Width, int Height, int CellWidth, int RowHeight);

    internal static class SkillMacroImeCandidateWindowLayout
    {
        internal const int ClientViewportWidth = 800;
        internal const int ClientViewportHeight = 600;
        internal const int VerticalOverflowThreshold = 266;

        internal static SkillMacroImeCandidateWindowMetrics MeasureVertical(int fontHeight, int pageSize, int widestEntryWidth)
        {
            int safeFontHeight = Math.Max(0, fontHeight);
            int safePageSize = Math.Max(0, pageSize);
            int safeEntryWidth = Math.Max(0, widestEntryWidth);
            int rowHeight = safeFontHeight + 1;
            int width = safeEntryWidth > VerticalOverflowThreshold
                ? ClientViewportWidth
                : safeEntryWidth + safeFontHeight + 7;
            int height = (safePageSize * rowHeight) + 3;
            return new SkillMacroImeCandidateWindowMetrics(width, height, 0, rowHeight);
        }

        internal static SkillMacroImeCandidateWindowMetrics MeasureVerticalClientOwnerExact(int fontHeight, int pageSize, int widestEntryWidth)
        {
            int safeFontHeight = Math.Max(0, fontHeight);
            int safePageSize = Math.Max(0, pageSize);
            int safeEntryWidth = Math.Max(0, widestEntryWidth);
            int rowHeight = safeFontHeight + 1;
            int width = safeEntryWidth > VerticalOverflowThreshold
                ? ClientViewportWidth
                : safeEntryWidth;
            width += safeFontHeight + 7;
            int height = (safePageSize * rowHeight) + 3;
            return new SkillMacroImeCandidateWindowMetrics(width, height, 0, rowHeight);
        }

        internal static SkillMacroImeCandidateWindowMetrics MeasureHorizontal(int fontHeight, int pageSize)
        {
            int safeFontHeight = Math.Max(0, fontHeight);
            int safePageSize = Math.Max(0, pageSize);
            int cellWidth = 2 * (safeFontHeight + 4);
            int width = (cellWidth * safePageSize) + 4;
            int height = safeFontHeight + 10;
            return new SkillMacroImeCandidateWindowMetrics(width, height, cellWidth, 0);
        }

        internal static int HitTestCandidate(
            Rectangle bounds,
            Point point,
            bool vertical,
            int visibleCount,
            int rowHeight,
            int cellWidth)
        {
            if (visibleCount <= 0 || !bounds.Contains(point))
            {
                return -1;
            }

            if (vertical)
            {
                int safeRowHeight = Math.Max(1, rowHeight);
                int relativeY = point.Y - (bounds.Y + 2);
                if (relativeY < 0)
                {
                    return -1;
                }

                int rowIndex = relativeY / safeRowHeight;
                return rowIndex >= 0 && rowIndex < visibleCount
                    ? rowIndex
                    : -1;
            }

            int safeCellWidth = Math.Max(1, cellWidth);
            int relativeX = point.X - (bounds.X + 3);
            if (relativeX < 0)
            {
                return -1;
            }

            int columnIndex = relativeX / safeCellWidth;
            return columnIndex >= 0 && columnIndex < visibleCount
                ? columnIndex
                : -1;
        }
    }
}
