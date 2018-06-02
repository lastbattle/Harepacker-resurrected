/* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Windows.Forms;
using System.Drawing;

namespace HaCreator.ThirdParty.TabPages
{
    internal class TabView : FlickerFreeControl
    {

        #region "Construction"
        const int MaxWidth = 250;
        const int MinWidth = 90;

        internal PageCollection Pages;

        private DropdownButton DropdownButton;

        public TabView(PageCollection parent)
        {
            parent.CurrentPageChanged += new PageCollection.CurrentPageChangedEventHandler(MyParent_CurrentPageChanged);
            parent.PageAdded += new PageCollection.PageAddedEventHandler(MyParent_PageAdded);
            parent.PageRemoved += new PageCollection.PageRemovedEventHandler(MyParent_PageRemoved);
            this.Pages = parent;
            this.Dock = DockStyle.Top;
            this.Height = 25;
            parent.Controls.Add(this);
            this.DropdownButton = new DropdownButton(this);
            this.SendToBack();
        }
        #endregion

        #region "Resize Logic"
        private int leftMostVisibleIndex = 0;

        private Control[] GetVisibleTabs()
        {
            if ((Pages.Count == 0)) return new TabPageControl[0];

            int numVisibleTabs = Math.Max(1, Math.Min(this.Width / MinWidth, Pages.Count));
            int currentIndex = Pages.IndexOf(Pages.CurrentPage);

            this.DropdownButton.Visible = numVisibleTabs < Pages.Count;

            leftMostVisibleIndex = Math.Min(leftMostVisibleIndex, Pages.Count - numVisibleTabs);
            if ((currentIndex < leftMostVisibleIndex))
            {
                leftMostVisibleIndex = Math.Min(currentIndex, Pages.Count - 1 - numVisibleTabs);
            }
            else if ((currentIndex >= leftMostVisibleIndex + numVisibleTabs))
            {
                leftMostVisibleIndex = Math.Max(currentIndex - numVisibleTabs + 1, 0);
            }

            Control[] controls = null;
            int tabWidth = 0;
            int tabLeft = 0;
            int i = 0;

            if ((this.DropdownButton.Visible))
            {
                controls = new Control[numVisibleTabs + 1];
                this.DropdownButton.Height = this.Height;
                controls[0] = this.DropdownButton;
                tabWidth = Math.Min(MaxWidth, (this.Width - this.DropdownButton.Width) / numVisibleTabs);
                tabLeft = this.DropdownButton.Width;
                i = 1;
            }
            else
            {
                controls = new Control[numVisibleTabs];
                tabWidth = Math.Min(MaxWidth, this.Width / numVisibleTabs);
                tabLeft = 0;
                i = 0;
            }

            int totalWidth = tabLeft;
            Control ctl = null;
            int j = 0;

            while ((i < controls.Length))
            {
                ctl = Pages[leftMostVisibleIndex + j].TabPageCtl;
                controls[i] = ctl;
                ctl.Location = new Point(tabLeft, 0);
                ctl.Size = new Size(tabWidth, this.Height);
                ctl.Visible = true;

                tabLeft += tabWidth;
                totalWidth += tabWidth;
                i += 1;
                j += 1;
            }

            ctl.Width = Math.Min(ctl.Width + this.Width - ctl.Right, MaxWidth);

            return controls;
        }

        private void RefreshTabs()
        {
            this.SuspendPainting = true;
            this.Controls.Clear();
            this.Controls.AddRange(GetVisibleTabs());
            this.SuspendPainting = false;
        }

        protected override void OnResize(System.EventArgs e)
        {
            base.OnResize(e);
            this.RefreshTabs();
        }
        #endregion

        #region "Page Events"
        private void MyParent_CurrentPageChanged(TabPage currentPage, TabPage previousPage)
        {
            this.SuspendPainting = true;
            if ((currentPage != null)) currentPage.TabPageCtl.IsCurrent = true;
            if ((previousPage != null)) previousPage.TabPageCtl.IsCurrent = false;
            this.RefreshTabs();
            this.SuspendPainting = false;
        }

        private void MyParent_PageAdded(TabPage page)
        {
            page.TabPageCtl.Width = MaxWidth;
            this.RefreshTabs();
        }

        private void MyParent_PageRemoved(TabPage page)
        {
            this.RefreshTabs();
        }
        #endregion

        #region "Rendering"
        protected override void OnPaint(System.Windows.Forms.PaintEventArgs e)
        {
            if ((this.Pages == null)) return;


            int totalWidth = 0;
            foreach (Control ctl in this.Controls)
            {
                totalWidth += ctl.Width;
            }

            using (Pen borderPen = TabBaseControl.GetBorderPen(this.Pages.TabColor))
            {
                e.Graphics.DrawLine(borderPen, totalWidth, this.Height - 1, this.Width, this.Height - 1);
            }
        }
        #endregion

    }
}