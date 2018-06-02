/* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace HaCreator.ThirdParty.TabPages
{
    /// <summary>
    /// Handles displaying the drop down menu.  This only displays when there are more tabs than can fit on the screen.
    /// </summary>
    /// <remarks>
    /// This does some strange logic to make sure that the if the drop down menu is already displayed, it doesn't re-display
    /// when the button is clicked.  Also of note is the DropdownRenderer class, which makes the dropdown menu render using
    /// the same color scheme as the tab control.
    /// </remarks>
    internal class DropdownButton : TabBaseControl
    {

        #region "Button Logic"
        public enum EArrowDirection
        {
            Left = 225,
            Right = 45,
            Up = 315,
            Down = 135
        }

        private EArrowDirection myArrowDirection;
        public EArrowDirection ArrowDirection
        {
            get { return myArrowDirection; }
            set
            {
                myArrowDirection = value;
                this.Invalidate();
            }
        }

        private bool myIsClicking = false;
        public bool IsClicking
        {
            get { return myIsClicking; }
            set
            {
                if ((myIsClicking != value))
                {
                    myIsClicking = value;
                    this.Invalidate();
                }
            }
        }

        protected override bool IsHighlighted
        {
            get { return !this.DropdownMenu.Visible && base.IsHighlighted; }
        }

        public DropdownButton(TabView view)
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            this.SetStyle(ControlStyles.UserPaint, true);
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);

            DropdownMenu = CreateMenu();
            this.ArrowDirection = DropdownButton.EArrowDirection.Down;
            this.Dock = System.Windows.Forms.DockStyle.Left;
            this.IsClicking = false;
            this.Location = new System.Drawing.Point(0, 0);
            this.Size = new System.Drawing.Size(16, view.Height);
            this.Visible = false;
            view.Pages.ToolTips.SetToolTip(this, "Show all open pages.");
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if ((e.Button == MouseButtons.Left)) this.IsClicking = true;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if ((e.Button == MouseButtons.Left)) this.IsClicking = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if ((this.Pages == null)) return;


            if ((this.IsClicking))
            {
                Rectangle Bounds = new Rectangle(0, TopMargin, this.Width, this.Height - TopMargin - 1);
                this.PaintBackground(e, Bounds, new Rectangle(Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height / 2), Pages.TabColor, AddColor(this.Pages.TabColor, -75), false);
            }
            else
            {
                base.OnPaint(e);
            }

            float size = 5;

            e.Graphics.TranslateTransform((this.Bounds.Width / 2), this.Padding.Top + (this.Bounds.Height / 2));

            e.Graphics.RotateTransform((float)this.ArrowDirection);

            e.Graphics.SmoothingMode = SmoothingMode.None;
            e.Graphics.DrawLines(new Pen(this.ForeColor, 2), new PointF[] { new PointF((float)(-size * 0.75), (float)(-size * 0.25)), new PointF((float)(size * 0.25), (float)(-size * 0.25)), new PointF((float)(size * 0.25), (float)(size * 0.75)) });
        }

        private class DropdownMenuStrip : ContextMenuStrip
        {

            public bool IsVisible = false;
        }

        private DropdownMenuStrip DropdownMenu;

        private DropdownMenuStrip CreateMenu()
        {
            DropdownMenuStrip menu = new DropdownMenuStrip();
            menu.ItemClicked += new ToolStripItemClickedEventHandler(DropdownMenu_ItemClicked);
            menu.VisibleChanged += new EventHandler(DropdownMenu_VisibleChanged);
            return menu;
        }

        protected override void OnClick(System.EventArgs e)
        {
            base.OnClick(e);

            DropdownMenu.IsVisible = !DropdownMenu.IsVisible;
            if ((DropdownMenu.Visible)) return;


            this.DropdownMenu.Items.Clear();
            foreach (TabPage page in this.Pages)
            {
                DropdownMenu.Items.Add(page.Text).Tag = page;
            }

            DropdownMenu.Renderer = new DropdownRenderer(this.Pages.TabColor);
            DropdownMenu.Show(this, new Point(this.Left, this.Bottom), ToolStripDropDownDirection.BelowRight);
        }

        private void DropdownMenu_ItemClicked(object sender, System.Windows.Forms.ToolStripItemClickedEventArgs e)
        {
            Pages.CurrentPage = (TabPage)e.ClickedItem.Tag;
        }

        private void DropdownMenu_VisibleChanged(object sender, System.EventArgs e)
        {
            if ((!DropdownMenu.Visible)) DropdownMenu.IsVisible = this.ClientRectangle.Contains(this.PointToClient(Control.MousePosition));
        }

        #endregion

        #region "Custom Menu Rendering"
        public class DropdownRenderer : ToolStripRenderer
        {
            public Color Theme;

            public DropdownRenderer(Color theme)
            {
                this.Theme = theme;
            }

            protected override void OnRenderToolStripBorder(System.Windows.Forms.ToolStripRenderEventArgs e)
            {
                using (Pen pen = new Pen(AddColor(Theme, -100)))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
                }
            }

            protected override void OnRenderToolStripBackground(System.Windows.Forms.ToolStripRenderEventArgs e)
            {
                e.Graphics.Clear(AddColor(Theme, 100));
                Rectangle bounds = new Rectangle(0, 0, 26, e.ToolStrip.Height);
                using (LinearGradientBrush brush = new LinearGradientBrush(bounds, AddColor(Theme, 50), Theme, LinearGradientMode.Horizontal))
                {
                    e.Graphics.FillRectangle(brush, bounds);
                }
            }

            protected override void OnRenderMenuItemBackground(System.Windows.Forms.ToolStripItemRenderEventArgs e)
            {
                if ((e.Item.Selected))
                {
                    e.Item.ForeColor = Color.Black;
                    Color highlight = default(Color);
                    if ((Theme.B > Theme.R && Theme.B > Theme.G))
                    {
                        highlight = ColorTranslator.FromHtml("#FFC580");
                    }
                    else if ((Theme.G > Theme.R && Theme.G > Theme.B))
                    {
                        highlight = ColorTranslator.FromHtml("#FFFB80");
                    }
                    else
                    {
                        //"#D4EBFF")
                        highlight = ColorTranslator.FromHtml("#A1C8FF");
                    }

                    using (LinearGradientBrush B = new LinearGradientBrush(new Rectangle(e.Item.ContentRectangle.Left, e.Item.ContentRectangle.Top, e.Item.ContentRectangle.Width / 2, e.Item.ContentRectangle.Height), Color.FromArgb(150, highlight), AddColor(highlight, 200), LinearGradientMode.Horizontal))
                    {
                        B.WrapMode = WrapMode.TileFlipXY;
                        e.Graphics.FillRectangle(B, e.Item.ContentRectangle);
                    }

                    using (Pen P = new Pen(AddColor(highlight, -25)))
                    {
                        e.Graphics.DrawRectangle(P, e.Item.ContentRectangle.Left, e.Item.ContentRectangle.Top, e.Item.ContentRectangle.Width - 1, e.Item.ContentRectangle.Height - 1);
                    }
                }
                else
                {
                    e.Item.ForeColor = e.ToolStrip.ForeColor;
                    base.OnRenderMenuItemBackground(e);
                }
            }
        }
        #endregion

    }
}