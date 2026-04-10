using System;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator.UI
{
    internal readonly record struct SkillMacroImeWindowPlacement(
        Point CompositionPoint,
        Rectangle CompositionExcludeArea,
        Point CandidatePoint,
        Rectangle CandidateExcludeArea,
        uint CompositionStyle,
        uint CandidateStyle);

    internal static class SkillMacroImeWindowPlacementLayout
    {
        internal const uint ImeWindowStyleDefault = 0x0000;
        internal const uint ImeWindowStyleRect = 0x0001;
        internal const uint ImeWindowStylePoint = 0x0002;
        internal const uint ImeWindowStyleForcePosition = 0x0020;
        internal const uint ImeWindowStyleCandidatePosition = 0x0040;
        internal const uint ImeWindowStyleExclude = 0x0080;

        internal static SkillMacroImeWindowPlacement Resolve(
            Rectangle nameFieldBounds,
            int textInsetX,
            int lineSpacing,
            int compositionCaretWidth,
            bool useClauseAnchor,
            int clauseAnchorWidth,
            int clauseWidth,
            uint compositionStyle = ImeWindowStyleExclude,
            uint candidateStyle = ImeWindowStyleExclude)
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
                    compositionExcludeArea,
                    compositionStyle,
                    candidateStyle);
            }

            int candidateX = ClampToField(textOriginX + Math.Max(0, clauseAnchorWidth), nameFieldBounds);
            Rectangle candidateExcludeArea = BuildExcludeArea(nameFieldBounds, candidateX, Math.Max(1, clauseWidth));
            return new SkillMacroImeWindowPlacement(
                new Point(compositionX, baselineY),
                compositionExcludeArea,
                new Point(candidateX, baselineY),
                candidateExcludeArea,
                compositionStyle,
                candidateStyle);
        }

        internal static bool TryResolveCandidateWindowFormPlacement(
            ImeCandidateWindowForm windowForm,
            out Point point,
            out Rectangle area,
            out uint style)
        {
            point = Point.Zero;
            area = Rectangle.Empty;
            style = ImeWindowStyleDefault;
            if (windowForm == null || !windowForm.HasPlacementData)
            {
                return false;
            }

            uint normalizedStyle = NormalizeCandidateWindowStyle(windowForm.Style);
            Rectangle normalizedArea = new(
                windowForm.AreaX,
                windowForm.AreaY,
                Math.Max(0, windowForm.AreaWidth),
                Math.Max(0, windowForm.AreaHeight));

            if ((normalizedStyle & (ImeWindowStyleExclude | ImeWindowStyleRect)) != 0
                && (normalizedArea.Width <= 0 || normalizedArea.Height <= 0))
            {
                return false;
            }

            if (normalizedStyle == ImeWindowStyleDefault)
            {
                return false;
            }

            point = new Point(windowForm.CurrentX, windowForm.CurrentY);
            area = normalizedArea;
            style = normalizedStyle;
            return true;
        }

        private static uint NormalizeCandidateWindowStyle(uint style)
        {
            uint normalized = style & (ImeWindowStyleRect
                | ImeWindowStylePoint
                | ImeWindowStyleForcePosition
                | ImeWindowStyleCandidatePosition
                | ImeWindowStyleExclude);

            if ((normalized & ImeWindowStyleExclude) != 0)
            {
                return ImeWindowStyleExclude;
            }

            if ((normalized & ImeWindowStyleRect) != 0)
            {
                return ImeWindowStyleRect;
            }

            if ((normalized & (ImeWindowStyleForcePosition | ImeWindowStyleCandidatePosition | ImeWindowStylePoint)) != 0)
            {
                return normalized & (ImeWindowStyleForcePosition | ImeWindowStyleCandidatePosition | ImeWindowStylePoint);
            }

            return ImeWindowStyleDefault;
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
