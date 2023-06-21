/* Copyright(c) 2023, LastBattle https://github.com/lastbattle/Harepacker-resurrected

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;

namespace HaCreator.MapSimulator.MapObjects.UIObject.Controls {

    /// <summary>
    /// An image container grid that allows for easy creation of game UIs without
    /// the low level height, width, x, y, etc. calculations.
    /// </summary>
    public class HaUIGrid : IHaUIRenderable {

        private int rows;
        private int columns;

        private HaUIInfo _info;

        private Dictionary<Point, IHaUIRenderable> gridContent;

        public HaUIGrid(int rows, int columns) {
            this.rows = rows;
            this.columns = columns;
            this._info = new HaUIInfo();
            this.gridContent = new Dictionary<Point, IHaUIRenderable>();
        }

        public HaUIGrid(int rows, int columns, HaUIInfo info) {
            this.rows = rows;
            this.columns = columns;
            this._info = info;
            this.gridContent = new Dictionary<Point, IHaUIRenderable>();
        }

        public void AddRenderable(IHaUIRenderable renderable) {
            AddRenderable(0, 0, renderable);
        }

        public void AddRenderable(int row, int column, IHaUIRenderable renderable) {
            if (row < 0 || row >= rows)
                throw new ArgumentException("Invalid row index", nameof(row));

            if (column < 0 || column >= columns)
                throw new ArgumentException("Invalid column index", nameof(column));

            gridContent[new Point(column, row)] = renderable;
        }

        public Bitmap Render() {
            HaUISize thisSize = GetSize();
            int cellWidth = thisSize.Width;
            int cellHeight = thisSize.Height;

            Bitmap gridBitmap = new Bitmap(cellWidth * columns, cellHeight * rows);
            using (Graphics g = Graphics.FromImage(gridBitmap)) {
                g.Clear(Color.Transparent);

                foreach (var pair in gridContent) {
                    HaUISize contentSize = pair.Value.GetSize();

                    int x = pair.Key.X * cellWidth + HaUIHelper.CalculateAlignmentOffset(contentSize.Width, cellWidth, pair.Value.GetInfo().HorizontalAlignment);
                    int y = pair.Key.Y * cellHeight + HaUIHelper.CalculateAlignmentOffset(contentSize.Height, cellHeight, pair.Value.GetInfo().VerticalAlignment);

                    // Account for Margins
                    //int marginWidth = x - pair.Value.GetInfo().Margins.Left - pair.Value.GetInfo().Margins.Right;
                    //int marginHeight = y - pair.Value.GetInfo().Margins.Top - pair.Value.GetInfo().Margins.Bottom;

                    g.DrawImage(pair.Value.Render(), x, y);
                }
            }

            return gridBitmap;
        }

        public HaUISize GetSize() {
            int width = gridContent.Values.Max(r => r.GetSize().Width + r.GetInfo().Margins.Left + r.GetInfo().Margins.Right) * columns;
            int height = gridContent.Values.Max(r => r.GetSize().Height + r.GetInfo().Margins.Top + r.GetInfo().Margins.Bottom) * rows;

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