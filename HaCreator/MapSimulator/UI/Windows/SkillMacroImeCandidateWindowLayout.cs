using System;
using HaCreator.MapSimulator;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator.UI
{
    internal readonly record struct SkillMacroImeCandidateWindowMetrics(int Width, int Height, int CellWidth, int RowHeight);

    internal static class SkillMacroImeCandidateWindowLayout
    {
        internal const int ClientViewportWidth = 800;
        internal const int ClientViewportHeight = 600;
        internal const int VerticalOverflowThreshold = 266;
        internal const uint CandidateWindowStyleRect = 0x0001;
        internal const uint CandidateWindowStylePoint = 0x0002;
        internal const uint CandidateWindowStyleForcePosition = 0x0020;
        internal const uint CandidateWindowStyleCandidatePosition = 0x0040;
        internal const uint CandidateWindowStyleExclude = 0x0080;

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

        internal static Rectangle ResolveClientOwnerBounds(
            int viewportWidth,
            int viewportHeight,
            int width,
            int height,
            Point preferredOrigin,
            int overflowY)
        {
            int safeViewportWidth = Math.Max(1, viewportWidth);
            int safeViewportHeight = Math.Max(1, viewportHeight);
            int safeWidth = Math.Max(1, width);
            int safeHeight = Math.Max(1, height);

            int x = preferredOrigin.X;
            if (x < 0)
            {
                x = 0;
            }

            if (x + safeWidth > safeViewportWidth)
            {
                x = safeViewportWidth - safeWidth;
            }

            int y = preferredOrigin.Y;
            if (y + safeHeight > safeViewportHeight)
            {
                y = overflowY;
            }

            return new Rectangle(x, y, safeWidth, safeHeight);
        }

        internal static bool TryResolveWindowFormOrigin(
            ImeCandidateWindowForm windowForm,
            int viewportWidth,
            int viewportHeight,
            int height,
            out Point origin)
        {
            if (windowForm == null || !windowForm.HasPlacementData)
            {
                origin = Point.Zero;
                return false;
            }

            int safeViewportHeight = Math.Max(1, viewportHeight);
            uint style = windowForm.Style;

            int x = windowForm.CurrentX;
            int y = windowForm.CurrentY;

            if ((style & CandidateWindowStyleExclude) != 0 && windowForm.AreaWidth > 0 && windowForm.AreaHeight > 0)
            {
                x = windowForm.CurrentX;
                y = windowForm.AreaY + windowForm.AreaHeight + 1;
                if (y + height > safeViewportHeight)
                {
                    y = windowForm.AreaY - height - 1;
                }
            }
            else if ((style & (CandidateWindowStyleForcePosition | CandidateWindowStyleCandidatePosition | CandidateWindowStylePoint)) != 0)
            {
                y = windowForm.CurrentY + 1;
            }
            else if ((style & CandidateWindowStyleRect) != 0 && windowForm.AreaWidth > 0 && windowForm.AreaHeight > 0)
            {
                x = windowForm.AreaX;
                y = windowForm.AreaY + windowForm.AreaHeight + 1;
                if (y + height > safeViewportHeight)
                {
                    y = windowForm.AreaY - height - 1;
                }
            }
            else
            {
                origin = Point.Zero;
                return false;
            }

            origin = new Point(x, y);
            return true;
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
