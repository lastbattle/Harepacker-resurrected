/* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Windows.Forms;
using System.Drawing;
using Microsoft.VisualBasic;

namespace HaCreator.ThirdParty.TabPages
{
    /// <summary>
    /// This is the control that is responsible for rendering a specific tab.
    /// </summary>
    internal class TabPageControl : TabBaseControl
    {

        #region "Initialization"
        private TabPage Page;
        public TabPageControl(TabPage page)
        {
            this.Page = page;
            page.ParentChanged += new TabPage.ParentChangedEventHandler(Page_ParentChanged);
            TextChanged += new EventHandler(TabPageControl_TextChanged);
            this.SetStyle(ControlStyles.SupportsTransparentBackColor, true);

            this.InitializeClose();
        }
        #endregion

        #region "Close / Selection Logic"
        public event OnCloseEventHandler OnClose;
        public delegate void OnCloseEventHandler(TabPage page);

        private Label CloseLink;

        private void InitializeClose()
        {
            this.CloseLink = new Label();
            this.CloseLink.AutoSize = false;
            this.CloseLink.Dock = DockStyle.Right;
            this.CloseLink.Font = new Font(new FontFamily("Microsoft Sans Serif"), (float)8.25, FontStyle.Bold, GraphicsUnit.Point);
            this.CloseLink.Visible = false;
            this.CloseLink.Width = 16;
            this.CloseLink.BackColor = Color.Transparent;
            this.CloseLink.Text = "X";
            this.CloseLink.TextAlign = ContentAlignment.MiddleCenter;
            this.CloseLink.Click += new System.EventHandler(CloseLink_Click);
            this.CloseLink.MouseEnter += new System.EventHandler(CloseLink_MouseEnter);
            this.CloseLink.MouseLeave += new System.EventHandler(CloseLink_MouseLeave);
            this.Controls.Add(this.CloseLink);
        }

        public void Close()
        {
            if (OnClose != null)
            {
                OnClose(this.Page);
            }
        }

        private void CloseLink_Click(object sender, System.EventArgs e)
        {
            Close();
        }

        private void CloseLink_MouseEnter(object sender, System.EventArgs e)
        {
            this.CloseLink.ForeColor = Color.Crimson;
        }

        private void CloseLink_MouseLeave(object sender, System.EventArgs e)
        {
            this.CloseLink.ForeColor = this.ForeColor;
        }

        public ContextMenuStrip Menu;
        protected override void OnClick(EventArgs e)
        {
            this.Pages.CurrentPage = this.Page;
            if (((MouseEventArgs)e).Button == MouseButtons.Right && Menu != null)
                Menu.Show(this, new Point(((MouseEventArgs)e).X, ((MouseEventArgs)e).Y));
        }
        #endregion

        #region "Rendering"
        public bool IsCurrent
        {
            get { return this.CloseLink.Visible; }
            set
            {
                if ((value != this.CloseLink.Visible))
                {
                    this.CloseLink.Visible = value;
                    this.Invalidate();
                }
            }
        }

        protected override void OnPaint(System.Windows.Forms.PaintEventArgs e)
        {
            if ((this.Pages == null)) return;


            // If we're the current page, paint a big tab.
            if ((object.ReferenceEquals(this.Pages.CurrentPage, this.Page)))
            {
                this.PaintBackground(e, new Rectangle(0, 0, this.Width, this.Height), new RectangleF(0, 0, this.Width, this.Height / 2), AddColor(this.Pages.TabColor, 100), this.Pages.TabColor, true);
            }
            else
            {
                // Otherwise, do what we always do.
                base.OnPaint(e);
            }

            // Now we'll paint the text.
            StringFormat fmt = new StringFormat(StringFormatFlags.NoWrap);
            fmt.Alignment = StringAlignment.Near;
            fmt.LineAlignment = StringAlignment.Center;
            fmt.Trimming = StringTrimming.EllipsisCharacter;

            using (SolidBrush textBrush = new SolidBrush(this.ForeColor))
            {
                if ((object.ReferenceEquals(this.Pages.CurrentPage, this.Page)))
                {
                    e.Graphics.DrawString(this.Text, this.Font, textBrush, new RectangleF(TopMargin, 0, this.Width - this.CloseLink.Width - TopMargin, this.Height), fmt);
                }
                else
                {
                    e.Graphics.DrawString(this.Text, this.Font, textBrush, new RectangleF(TopMargin, 0, this.Width - 1 - TopMargin, this.Height), fmt);
                }
            }
        }
        #endregion

        #region "Tooltips"
        private string myToolTip;
        public string ToolTip
        {
            get { return myToolTip; }
            set
            {
                myToolTip = value;
                this.SetTooltip();
            }
        }

        private void SetTooltip()
        {
            if ((this.Page.Parent != null))
            {
                this.Page.Parent.ToolTips.SetToolTip(this, this.Text + Constants.vbNewLine + this.ToolTip);
                this.Page.Parent.ToolTips.SetToolTip(this.CloseLink, "Close page.");
            }
        }

        private void Page_ParentChanged()
        {
            this.SetTooltip();
        }

        private void TabPageControl_TextChanged(object sender, System.EventArgs e)
        {
            this.SetTooltip();
        }
        #endregion

    }
}