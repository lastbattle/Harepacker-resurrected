using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace HaCreator.CustomControls
{
    /// <summary>
    /// A fixed-cell wrap panel that only realizes rows intersecting the viewport.
    /// WPF's regular WrapPanel realizes every item, even when the owning ListBox
    /// has virtualization enabled.
    /// </summary>
    public sealed class VirtualizingWrapPanel : VirtualizingPanel, IScrollInfo
    {
        public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register(
            nameof(ItemWidth), typeof(double), typeof(VirtualizingWrapPanel),
            new FrameworkPropertyMetadata(140d, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty ItemHeightProperty = DependencyProperty.Register(
            nameof(ItemHeight), typeof(double), typeof(VirtualizingWrapPanel),
            new FrameworkPropertyMetadata(112d, FrameworkPropertyMetadataOptions.AffectsMeasure));

        private Size extent;
        private Size viewport;
        private Point offset;
        private int itemsPerRow = 1;

        public double ItemWidth
        {
            get => (double)GetValue(ItemWidthProperty);
            set => SetValue(ItemWidthProperty, value);
        }

        public double ItemHeight
        {
            get => (double)GetValue(ItemHeightProperty);
            set => SetValue(ItemHeightProperty, value);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            ItemsControl owner = ItemsControl.GetItemsOwner(this);
            int itemCount = owner?.Items.Count ?? 0;
            double cellWidth = Math.Max(1d, ItemWidth);
            double cellHeight = Math.Max(1d, ItemHeight);
            double availableWidth = ResolveAvailableWidth(availableSize.Width, cellWidth);
            double availableHeight = ResolveAvailableHeight(availableSize.Height, cellHeight);

            itemsPerRow = Math.Max(1, (int)Math.Floor(availableWidth / cellWidth));
            double arrangedCellWidth = availableWidth / itemsPerRow;
            int rowCount = (itemCount + itemsPerRow - 1) / itemsPerRow;
            UpdateScrollInfo(new Size(availableWidth, rowCount * cellHeight),
                new Size(availableWidth, availableHeight));

            (int firstItemIndex, int lastItemIndex) = CalculateRealizationRange(
                itemCount, itemsPerRow, availableHeight, VerticalOffset, cellHeight);

            CleanupItems(firstItemIndex, lastItemIndex);
            RealizeItems(firstItemIndex, lastItemIndex, new Size(arrangedCellWidth, cellHeight));

            return new Size(
                double.IsInfinity(availableSize.Width) ? availableWidth : availableSize.Width,
                double.IsInfinity(availableSize.Height) ? Math.Min(extent.Height, availableHeight) : availableSize.Height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            ItemsControl owner = ItemsControl.GetItemsOwner(this);
            if (owner == null || itemsPerRow <= 0)
                return finalSize;

            double cellHeight = Math.Max(1d, ItemHeight);
            double cellWidth = Math.Max(1d, finalSize.Width / itemsPerRow);
            foreach (UIElement child in InternalChildren)
            {
                int itemIndex = owner.ItemContainerGenerator.IndexFromContainer(child);
                if (itemIndex < 0)
                    continue;

                int row = itemIndex / itemsPerRow;
                int column = itemIndex % itemsPerRow;
                child.Arrange(new Rect(
                    column * cellWidth - HorizontalOffset,
                    row * cellHeight - VerticalOffset,
                    cellWidth,
                    cellHeight));
            }

            return finalSize;
        }

        private void RealizeItems(int firstItemIndex, int lastItemIndex, Size childSize)
        {
            if (firstItemIndex > lastItemIndex)
                return;

            IItemContainerGenerator generator = ItemsControl.GetItemsOwner(this)?.ItemContainerGenerator;
            if (generator == null)
                return;
            GeneratorPosition startPosition = generator.GeneratorPositionFromIndex(firstItemIndex);
            int childIndex = startPosition.Offset == 0 ? startPosition.Index : startPosition.Index + 1;

            using (generator.StartAt(startPosition, GeneratorDirection.Forward, true))
            {
                for (int itemIndex = firstItemIndex; itemIndex <= lastItemIndex; itemIndex++, childIndex++)
                {
                    UIElement child = generator.GenerateNext(out bool newlyRealized) as UIElement;
                    if (child == null)
                        continue;

                    if (newlyRealized)
                    {
                        if (childIndex >= InternalChildren.Count)
                            AddInternalChild(child);
                        else
                            InsertInternalChild(childIndex, child);
                        generator.PrepareItemContainer(child);
                    }

                    child.Measure(childSize);
                }
            }
        }

        private void CleanupItems(int firstItemIndex, int lastItemIndex)
        {
            if (ItemsControl.GetItemsOwner(this)?.ItemContainerGenerator is not IRecyclingItemContainerGenerator generator)
                return;

            for (int childIndex = InternalChildren.Count - 1; childIndex >= 0; childIndex--)
            {
                GeneratorPosition position = new(childIndex, 0);
                int itemIndex = generator.IndexFromGeneratorPosition(position);
                if (itemIndex >= firstItemIndex && itemIndex <= lastItemIndex)
                    continue;

                generator.Recycle(position, 1);
                RemoveInternalChildRange(childIndex, 1);
            }
        }

        internal static (int FirstItemIndex, int LastItemIndex) CalculateRealizationRange(
            int itemCount, int columns, double viewportHeight, double verticalOffset, double itemHeight)
        {
            columns = Math.Max(1, columns);
            itemHeight = Math.Max(1d, itemHeight);
            int firstVisibleRow = Math.Max(0, (int)Math.Floor(verticalOffset / itemHeight));
            int visibleRowCount = Math.Max(1, (int)Math.Ceiling(viewportHeight / itemHeight) + 1);
            int firstItemIndex = Math.Min(itemCount, firstVisibleRow * columns);
            int lastItemIndex = Math.Min(itemCount - 1,
                ((firstVisibleRow + visibleRowCount) * columns) - 1);
            return (firstItemIndex, lastItemIndex);
        }

        private double ResolveAvailableWidth(double value, double fallback)
        {
            if (!double.IsInfinity(value) && value > 0)
                return value;
            if (ScrollOwner?.ViewportWidth > 0)
                return ScrollOwner.ViewportWidth;
            return viewport.Width > 0 ? viewport.Width : fallback;
        }

        private double ResolveAvailableHeight(double value, double fallback)
        {
            if (!double.IsInfinity(value) && value > 0)
                return value;
            if (ScrollOwner?.ViewportHeight > 0)
                return ScrollOwner.ViewportHeight;
            return viewport.Height > 0 ? viewport.Height : fallback * 5;
        }

        private void UpdateScrollInfo(Size newExtent, Size newViewport)
        {
            bool changed = !AreClose(extent, newExtent) || !AreClose(viewport, newViewport);
            extent = newExtent;
            viewport = newViewport;
            offset.X = Clamp(offset.X, 0, Math.Max(0, extent.Width - viewport.Width));
            offset.Y = Clamp(offset.Y, 0, Math.Max(0, extent.Height - viewport.Height));
            if (changed)
                ScrollOwner?.InvalidateScrollInfo();
        }

        private static bool AreClose(Size left, Size right) =>
            Math.Abs(left.Width - right.Width) < 0.1 && Math.Abs(left.Height - right.Height) < 0.1;

        private static double Clamp(double value, double minimum, double maximum) =>
            Math.Max(minimum, Math.Min(maximum, value));

        public bool CanHorizontallyScroll { get; set; }
        public bool CanVerticallyScroll { get; set; }
        public double ExtentWidth => extent.Width;
        public double ExtentHeight => extent.Height;
        public double ViewportWidth => viewport.Width;
        public double ViewportHeight => viewport.Height;
        public double HorizontalOffset => offset.X;
        public double VerticalOffset => offset.Y;
        public ScrollViewer ScrollOwner { get; set; }

        public void LineUp() => SetVerticalOffset(VerticalOffset - ItemHeight);
        public void LineDown() => SetVerticalOffset(VerticalOffset + ItemHeight);
        public void LineLeft() => SetHorizontalOffset(HorizontalOffset - ItemWidth);
        public void LineRight() => SetHorizontalOffset(HorizontalOffset + ItemWidth);
        public void MouseWheelUp() => SetVerticalOffset(VerticalOffset - (ItemHeight * SystemParameters.WheelScrollLines));
        public void MouseWheelDown() => SetVerticalOffset(VerticalOffset + (ItemHeight * SystemParameters.WheelScrollLines));
        public void MouseWheelLeft() => SetHorizontalOffset(HorizontalOffset - (ItemWidth * 3));
        public void MouseWheelRight() => SetHorizontalOffset(HorizontalOffset + (ItemWidth * 3));
        public void PageUp() => SetVerticalOffset(VerticalOffset - ViewportHeight);
        public void PageDown() => SetVerticalOffset(VerticalOffset + ViewportHeight);
        public void PageLeft() => SetHorizontalOffset(HorizontalOffset - ViewportWidth);
        public void PageRight() => SetHorizontalOffset(HorizontalOffset + ViewportWidth);

        public void SetHorizontalOffset(double value)
        {
            if (!CanHorizontallyScroll)
                value = 0;
            value = Clamp(value, 0, Math.Max(0, ExtentWidth - ViewportWidth));
            if (Math.Abs(value - offset.X) < 0.1)
                return;
            offset.X = value;
            InvalidateMeasure();
            ScrollOwner?.InvalidateScrollInfo();
        }

        public void SetVerticalOffset(double value)
        {
            value = Clamp(value, 0, Math.Max(0, ExtentHeight - ViewportHeight));
            if (Math.Abs(value - offset.Y) < 0.1)
                return;
            offset.Y = value;
            InvalidateMeasure();
            ScrollOwner?.InvalidateScrollInfo();
        }

        public Rect MakeVisible(Visual visual, Rect rectangle)
        {
            ItemsControl owner = ItemsControl.GetItemsOwner(this);
            int itemIndex = owner?.ItemContainerGenerator.IndexFromContainer(visual) ?? -1;
            if (itemIndex < 0)
                return rectangle;

            double rowTop = (itemIndex / Math.Max(1, itemsPerRow)) * Math.Max(1d, ItemHeight);
            if (rowTop < VerticalOffset)
                SetVerticalOffset(rowTop);
            else if (rowTop + ItemHeight > VerticalOffset + ViewportHeight)
                SetVerticalOffset(rowTop + ItemHeight - ViewportHeight);
            return new Rect(0, rowTop, ItemWidth, ItemHeight);
        }
    }
}
