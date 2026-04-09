using System;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator.UI
{
    internal readonly record struct SkillMacroImeWindowPlacement(
        Point CompositionPoint,
        Rectangle CompositionExcludeArea,
        Point CandidatePoint,
        Rectangle CandidateExcludeArea);

    internal static class SkillMacroImeWindowPlacementLayout
    {
        internal static SkillMacroImeWindowPlacement Resolve(
            Rectangle nameFieldBounds,
            int textInsetX,
            int lineSpacing,
            int compositionCaretWidth,
            bool useClauseAnchor,
            int clauseAnchorWidth,
            int clauseWidth)
        {
            int safeInsetX = Math.Max(0, textInsetX);
            int safeLineSpacing = Math.Max(0, lineSpacing);
            int textOriginX = nameFieldBounds.X + safeInsetX;
            int baselineY = nameFieldBounds.Y + safeLineSpacing + 1;

            int compositionX = ClampToField(textOriginX + Math.Max(0, compositionCaretWidth), nameFieldBounds);
            Rectangle compositionExcludeArea = BuildExcludeArea(nameFieldBounds, compositionX, 1);

            if (!useClauseAnchor)
            {
                return new SkillMacroImeWindowPlacement(
                    new Point(compositionX, baselineY),
                    compositionExcludeArea,
                    new Point(compositionX, baselineY),
                    compositionExcludeArea);
            }

            int candidateX = ClampToField(textOriginX + Math.Max(0, clauseAnchorWidth), nameFieldBounds);
            Rectangle candidateExcludeArea = BuildExcludeArea(nameFieldBounds, candidateX, Math.Max(1, clauseWidth));
            return new SkillMacroImeWindowPlacement(
                new Point(compositionX, baselineY),
                compositionExcludeArea,
                new Point(candidateX, baselineY),
                candidateExcludeArea);
        }

        private static int ClampToField(int x, Rectangle bounds)
        {
            if (bounds.Width <= 0)
            {
                return bounds.X;
            }

            return Math.Clamp(x, bounds.X, bounds.Right - 1);
        }

        private static Rectangle BuildExcludeArea(Rectangle bounds, int x, int requestedWidth)
        {
            int safeX = ClampToField(x, bounds);
            int maxWidth = Math.Max(1, bounds.Right - safeX);
            int width = Math.Clamp(requestedWidth, 1, maxWidth);
            int height = Math.Max(1, bounds.Height);
            return new Rectangle(safeX, bounds.Y, width, height);
        }
    }
}
