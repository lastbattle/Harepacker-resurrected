/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;

namespace HaCreator.CustomControls
{
    public class AlphaColorPicker : UserControl
    {
        private Color color = Color.White;
        private Brush brush = new SolidBrush(Color.White);
        private Rectangle rect;

        public AlphaColorPicker()
        {
            InitializeComponent();
            rect = new Rectangle(new Point(0, 0),new Size(Size.Width - 1, Size.Height - 1));
        }

        public Color Color { get { return color; } set { color = value; brush = new SolidBrush(color); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.FillRectangle(brush, rect);
            e.Graphics.DrawRectangle(Pens.Black, rect);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            WPFColorPickerLib.ColorDialog dialog = new WPFColorPickerLib.ColorDialog(System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B));
            dialog.Topmost = true;
            dialog.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
            if (!(bool)dialog.ShowDialog()) return;
            Color = Color.FromArgb(dialog.SelectedColor.A, dialog.SelectedColor.R, dialog.SelectedColor.G, dialog.SelectedColor.B);
            Invalidate();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // AlphaColorPicker
            // 
            this.MaximumSize = new System.Drawing.Size(16, 16);
            this.MinimumSize = new System.Drawing.Size(16, 16);
            this.Name = "AlphaColorPicker";
            this.Size = new System.Drawing.Size(16, 16);
            this.ResumeLayout(false);

        }
    }
}
