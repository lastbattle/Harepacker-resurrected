using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;

namespace HaCreator.MapSimulator.UI.Controls {

    /// <summary>
    /// An image container grid that allows for easy creation of game UIs without
    /// the low level height, width, x, y, etc. calculations.
    /// </summary>
    public class HaUIGrid : IHaUIRenderable {

        private readonly int rows;
        private readonly int columns;

        private readonly HaUIInfo _info;

        private List<KeyValuePair<Point, IHaUIRenderable>> gridContent;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="rows"></param>
        /// <param name="columns"></param>
        public HaUIGrid(int rows, int columns) {
            this.rows = rows;
            this.columns = columns;
            this._info = new HaUIInfo();
            this.gridContent = new List<KeyValuePair<Point, IHaUIRenderable>>();
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="rows"></param>
        /// <param name="columns"></param>
        /// <param name="info"></param>
        public HaUIGrid(int rows, int columns, HaUIInfo info) {
            this.rows = rows;
            this.columns = columns;
            this._info = info;
            this.gridContent = new List<KeyValuePair<Point, IHaUIRenderable>>();
        }

        public void AddRenderable(IHaUIRenderable renderable) {
            AddRenderable(0, 0, renderable);
        }

        public void AddRenderable(int row, int column, IHaUIRenderable renderable) {
            if (row < 0 || row >= rows)
                throw new ArgumentException("Invalid row index", nameof(row));

            if (column < 0 || column >= columns)
                throw new ArgumentException("Invalid column index", nameof(column));

            gridContent.Add(new KeyValuePair<Point, IHaUIRenderable>(new Point(column, row), renderable));
        }

        public Bitmap Render() {
            HaUISize thisGridSize = GetSize();
            int cellWidth = thisGridSize.Width;
            int cellHeight = thisGridSize.Height;

            Bitmap gridBitmap = new Bitmap(cellWidth * columns, cellHeight * rows);
            using (Graphics g = Graphics.FromImage(gridBitmap)) {
                g.Clear(Color.Transparent);

                foreach (var pair in gridContent) {
                    Bitmap drawImage = pair.Value.Render();
                    HaUISize contentSize = pair.Value.GetSize();
                    HaUIInfo subUIInfo = pair.Value.GetInfo();
                    Point subUIKey = pair.Key;

                    int alignmentXOffset = HaUIHelper.CalculateAlignmentOffset(contentSize.Width, cellWidth, subUIInfo.HorizontalAlignment);
                    int alignmentYOffset = HaUIHelper.CalculateAlignmentOffset(contentSize.Height, cellHeight, subUIInfo.VerticalAlignment);

                    int x = subUIKey.X * cellWidth - alignmentXOffset;
                    int y = subUIKey.Y * cellHeight - alignmentYOffset;

                    //Debug.WriteLine("Drawing {0}x{1} at x: {2}, y: {3}. W: {4}, H: {5   }", drawImage.Width, drawImage.Height, x, y, cellWidth, cellHeight);

                    // Account for Margins
                    //int marginWidth = x - subUIInfo.Margins.Left - subUIInfo.Margins.Right;
                    //int marginHeight = y - subUIInfo.Margins.Top - subUIInfo.Margins.Bottom;

                    g.DrawImage(drawImage, x, y);
                }
            }

            return gridBitmap;
        }

        public HaUISize GetSize() {
            int width = gridContent.Select(pair => pair.Value).ToList().Max(r => r.GetSize().Width + r.GetInfo().Margins.Left + r.GetInfo().Margins.Right) * columns;
            int height = gridContent.Select(pair => pair.Value).ToList().Max(r => r.GetSize().Height + r.GetInfo().Margins.Top + r.GetInfo().Margins.Bottom) * rows;

            // normalise to MinHeight and MinWidth
            return new HaUISize(
                Math.Max(_info.MinWidth, width),
                Math.Max(_info.MinHeight, height));
        }

        public HaUIInfo GetInfo() {
            return _info;
        }
    }
}