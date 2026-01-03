using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;

namespace HaCreator.MapSimulator.MapObjects.UIObject.Controls {

    /// <summary>
    /// A stackpanel for bitmap images that allows for easy creation of game UIs without
    /// the low level height, width, x, y, etc. calculations.
    /// </summary>
    public class HaUIStackPanel : IHaUIRenderable {
        private List<IHaUIRenderable> childrens;
        private HaUIStackOrientation orientation;

        private HaUIInfo _info;

        public HaUIStackPanel(HaUIStackOrientation orientation) {
            this.orientation = orientation;
            this.childrens = new List<IHaUIRenderable>();

            this._info = new HaUIInfo() { };
        }

        public HaUIStackPanel(HaUIStackOrientation orientation, HaUIInfo info) {
            this.orientation = orientation;
            this.childrens = new List<IHaUIRenderable>();

            this._info = info;
        }

        public void AddRenderable(IHaUIRenderable children) {
            this.childrens.Add(children);
        }

        /// <summary>
        /// Sets the orientation of the StackPanel
        /// </summary>
        /// <param name="orientation"></param>
        public void SetOrientation(HaUIStackOrientation orientation) {
            this.orientation = orientation;
        }

        public Bitmap Render() {
            HaUISize size = GetSize();
            int totalWidth = size.Width;
            int totalHeight = size.Height;

            Bitmap bitmap = new Bitmap(totalWidth, totalHeight);
            using (Graphics g = Graphics.FromImage(bitmap)) {
                g.Clear(Color.Transparent);

                int offset = orientation == HaUIStackOrientation.Horizontal ? _info.Margins.Left : _info.Margins.Top; // Initial offset based on the panel's margin
                foreach (IHaUIRenderable child in childrens) {
                    Bitmap childBitmap = child.Render();
                    HaUIInfo subUIInfo = child.GetInfo();

                    if (orientation == HaUIStackOrientation.Horizontal) {
                        int alignmentYOffset = HaUIHelper.CalculateAlignmentOffset(totalHeight, childBitmap.Height, subUIInfo.VerticalAlignment);

                        int x = offset + subUIInfo.Margins.Left;
                        int y = alignmentYOffset + subUIInfo.Margins.Top;

                        g.DrawImage(childBitmap, x, y);  // Adjust for vertical alignment and margin

                        offset += childBitmap.Width + subUIInfo.Margins.Left + subUIInfo.Margins.Right;  // Adjust for margin

                        //Debug.WriteLine("Drawing {0}x{1} at x: {2}, y: {3}. W: {4}, H: {5   }", childBitmap.Width, childBitmap.Height, x, y, totalWidth, totalHeight);
                    }
                    else {
                        int alignmentXOffset = HaUIHelper.CalculateAlignmentOffset(totalWidth, childBitmap.Width, subUIInfo.HorizontalAlignment);

                        int x = alignmentXOffset + subUIInfo.Margins.Left;
                        int y = offset + subUIInfo.Margins.Top;

                        g.DrawImage(childBitmap, x, y);  // Adjust for horizontal alignment and margin

                        offset += childBitmap.Height + subUIInfo.Margins.Top + subUIInfo.Margins.Bottom;  // Adjust for margin

                        //Debug.WriteLine("Drawing {0}x{1} at x: {2}, y: {3}. W: {4}, H: {5   }", childBitmap.Width, childBitmap.Height, x, y, totalWidth, totalHeight);
                    }
                }
            }

            return bitmap;
        }

        public HaUISize GetSize() {
            if (orientation == HaUIStackOrientation.Horizontal) {
                HaUISize allChildSizes = new HaUISize(
                    childrens.Sum(r => r.GetSize().Width + r.GetInfo().Margins.Left + r.GetInfo().Margins.Right),
                    childrens.Max(r => r.GetSize().Height + r.GetInfo().Margins.Top + r.GetInfo().Margins.Bottom));

                // normalise to MinHeight and MinWidth
                return new HaUISize(
                    Math.Max(_info.MinWidth, allChildSizes.Width), 
                    Math.Max(_info.MinHeight, allChildSizes.Height));
            }
            else // StackOrientation.Vertical
            {
                HaUISize allChildSizes = new HaUISize(
                    childrens.Max(r => r.GetSize().Width + r.GetInfo().Margins.Left + r.GetInfo().Margins.Right),
                    childrens.Sum(r => r.GetSize().Height + r.GetInfo().Margins.Top + r.GetInfo().Margins.Bottom));

                // normalise to MinHeight and MinWidth
                return new HaUISize(
                    Math.Max(_info.MinWidth, allChildSizes.Width),
                    Math.Max(_info.MinHeight, allChildSizes.Height));
            }
        }

        public HaUIInfo GetInfo() {
            return _info;
        }
    }
}