/* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Collections;

namespace HaCreator.ThirdParty.TabPages
{
    /// <summary>
    /// This is a tab page control which behaves kinda like IE's.
    /// </summary>
    /// <remarks></remarks>
    [Description("A tab control simlar to IE 7's."), ToolboxBitmap(typeof(TabControl))]
    public class PageCollection : FlickerFreeControl, IList<TabPage>
    {

        #region "Events"
        /// <summary>
        /// Indicates that the specified page has been added.
        /// </summary>
        /// <param name="page">The page which has been added.</param>
        public event PageAddedEventHandler PageAdded;
        public delegate void PageAddedEventHandler(TabPage page);

        /// <summary>
        /// Indicates that the specified page has been removed.
        /// </summary>
        /// <param name="page">The page which was removed.</param>
        public event PageRemovedEventHandler PageRemoved;
        public delegate void PageRemovedEventHandler(TabPage page);

        /// <summary>
        /// Indicates that the currently selected page has changed.
        /// </summary>
        /// <param name="currentPage">The currently selected page.</param>
        /// <param name="previousPage">The previously selected page.</param>
        public event CurrentPageChangedEventHandler CurrentPageChanged;
        public delegate void CurrentPageChangedEventHandler(TabPage currentPage, TabPage previousPage);

        /// <summary>
        /// Indicates that the specified page is closing.  This is raised before the page is
        /// closed and removed.  Setting cancel to true will prevent the page from being removed.
        /// </summary>
        public event PageClosingEventHandler PageClosing;
        public delegate void PageClosingEventHandler(TabPage page, ref bool cancel);

        public PageCollection()
        {
            CurrentPageChanged += new CurrentPageChangedEventHandler(TabPages_CurrentPageChanged);
            FontChanged += new EventHandler(PageCollection_FontChanged);
            PageAdded += new PageAddedEventHandler(TabPages_PageAdded);
            PageRemoved += new PageRemovedEventHandler(TabPages_PageRemoved);
            PageContainer = new PagePanel(this);
            myTabView = new TabView(this);
        }


        private void OnPageAdded(TabPage page)
        {
            if ((this.CurrentPage == null)) this.CurrentPage = page;
            if (PageAdded != null)
            {
                PageAdded(page);
            }
        }

        private void OnPageRemoved(TabPage page, int index)
        {
            if ((object.ReferenceEquals(page, this.CurrentPage)))
            {
                if ((index >= Count)) index = Count - 1;
                if ((index >= 0))
                {
                    CurrentPage = this[index];
                }
                else
                {
                    CurrentPage = null;
                }
            }

            if (PageRemoved != null)
            {
                PageRemoved(page);
            }
        }

        protected void OnPageClosing(TabPage page)
        {
            bool cancel = false;
            if (PageClosing != null)
            {
                PageClosing(page, ref cancel);
            }
            if ((cancel)) return;

            this.Remove(page);
        }
        #endregion

        #region "Fields"
        private List<TabPage> myPages = new List<TabPage>();
        private PagePanel PageContainer;
        internal ToolTip ToolTips = new ToolTip();
        private TabView myTabView;
        #endregion

        #region "Properties"
        private int myTopMargin = 3;
        /// <summary>
        /// TopMargin specifies the height difference between the active tab and the non-active tabs.
        /// </summary>
        /// <value>An integer representing the height difference.</value>
        [System.ComponentModel.Description("Specifies the height difference between the active tab and the non-active tabs."), System.ComponentModel.Category("Appearance")]
        public int TopMargin
        {
            get { return myTopMargin; }
            set
            {
                myTopMargin = value;
                this.myTabView.Invalidate();
            }
        }

        private TabPage myCurrentPage = null;
        /// <summary>
        /// Sets the current/active tab page.
        /// </summary>
        public TabPage CurrentPage
        {
            get { return myCurrentPage; }
            set
            {
                if ((object.ReferenceEquals(value, myCurrentPage))) return;

                TabPage previousPage = myCurrentPage;
                myCurrentPage = value;
                if (CurrentPageChanged != null)
                {
                    CurrentPageChanged(myCurrentPage, previousPage);
                }
            }
        }

        private Color myTabColor = Color.LightSteelBlue;
        /// <summary>
        /// Gets or sets the color used to calculate the gradient for the tabs.
        /// </summary>
        [System.ComponentModel.Browsable(true), System.ComponentModel.Category("Appearance"), System.ComponentModel.Description("Gets or sets the color used to calculate the gradient for the tabs.")]
        public Color TabColor
        {
            get { return myTabColor; }
            set { myTabColor = value; }
        }
        #endregion

        #region "IList Implementation"
        public void Add(TabPage item)
        {
            myPages.Add(item);
            OnPageAdded(item);
        }

        public void Clear()
        {
            for (int i = 0; i <= myPages.Count - 1; i++)
            {
                // We don't want to call OnPageRemoved, since this is a clear.
                if (PageRemoved != null)
                {
                    PageRemoved(myPages[i]);
                }
            }
            myPages.Clear();
        }

        public bool Contains(TabPage item)
        {
            return myPages.Contains(item);
        }

        public void CopyTo(TabPage[] array, int arrayIndex)
        {
            myPages.CopyTo(array, arrayIndex);
        }

        public int Count
        {
            get { return myPages.Count; }
        }

        public bool IsReadOnly
        {
            get { return true; }
        }

        public bool Remove(TabPage item)
        {
            int index = myPages.IndexOf(item);
            if (myPages.Count == 1) return false;
            RemoveAt(index);
            if (index == myPages.Count) index--;
            CurrentPage = myPages[index];
            if (CurrentPageChanged != null)
                CurrentPageChanged(CurrentPage, item);
            return true;
        }

        public System.Collections.Generic.IEnumerator<TabPage> GetEnumerator()
        {
            return myPages.GetEnumerator();
        }

        public int IndexOf(TabPage item)
        {
            return myPages.IndexOf(item);
        }

        public void Insert(int index, TabPage item)
        {
            myPages.Insert(index, item);
            OnPageAdded(item);
        }

        public TabPage this[int index]
        {
            get { return myPages[index]; }
            set
            {
                TabPage prevControl = myPages[index];
                if ((!object.ReferenceEquals(prevControl, value)))
                {
                    myPages[index] = value;
                    if ((!object.ReferenceEquals(prevControl, value) && value != null)) OnPageRemoved(prevControl, index);
                    OnPageAdded(value);
                }
            }
        }

        public void RemoveAt(int index)
        {
            if ((index < 0)) return;


            TabPage page = this[index];
            myPages.RemoveAt(index);
            OnPageRemoved(page, index);
        }

        public System.Collections.IEnumerator GetNonGenericEnumerator()
        {
            return ((IEnumerable)myPages).GetEnumerator();
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetNonGenericEnumerator();
        }
        #endregion

        #region "Event Handling"

        private void TabPages_CurrentPageChanged(TabPage currentPage, TabPage previousPage)
        {
            this.SuspendPainting = true;
            this.PageContainer.Controls.Clear();
            if ((currentPage != null))
            {
                currentPage.Control.Dock = DockStyle.Fill;
                currentPage.Control.Visible = true;
                this.PageContainer.Controls.Add(currentPage.Control);
            }
            this.SuspendPainting = false;
        }

        private void PageCollection_FontChanged(object sender, System.EventArgs e)
        {
            this.myTabView.Height = this.Font.Height + 8;
        }

        // Set the page's parent.
        private void TabPages_PageAdded(TabPage page)
        {
            page.SetParent(this);
            page.TabPageCtl.OnClose += OnPageClosing;
        }

        // Unset the page's parent.
        private void TabPages_PageRemoved(TabPage page)
        {
            if ((this.PageContainer.Contains(page.Control))) this.PageContainer.Controls.Clear();
            page.SetParent(null);
            page.TabPageCtl.OnClose -= OnPageClosing;
        }
        #endregion

        #region "Page Panel"
        /// <summary>
        /// The container for the page/control belonging to the current tab.
        /// </summary>
        private class PagePanel : Panel
        {

            private PageCollection Pages;

            public PagePanel(PageCollection pages)
            {
                this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);
                this.SetStyle(ControlStyles.UserPaint, true);
                this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
                this.Pages = pages;
                this.Dock = DockStyle.Fill;
                this.Padding = new Padding(1, 0, 1, 1);
                this.Pages.Controls.Add(this);
                this.BringToFront();
            }

            protected override void OnPaintBackground(System.Windows.Forms.PaintEventArgs e)
            {
            }

            protected override void OnPaint(System.Windows.Forms.PaintEventArgs e)
            {
                e.Graphics.Clear(Color.White);
                using (Pen borderPen = TabBaseControl.GetBorderPen(Pages.TabColor))
                {
                    e.Graphics.DrawLine(borderPen, 0, 0, 0, this.Height - 1);
                    e.Graphics.DrawLine(borderPen, 0, this.Height - 1, this.Width - 1, this.Height - 1);
                    e.Graphics.DrawLine(borderPen, this.Width - 1, 0, this.Width - 1, this.Height - 1);
                }
            }
        }
        #endregion

    }
}