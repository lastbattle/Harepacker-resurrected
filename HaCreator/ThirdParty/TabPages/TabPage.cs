/* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System.Windows.Forms;

namespace HaCreator.ThirdParty.TabPages
{
    /// <summary>
    /// Encapsulates tab information for a specified control/page.
    /// </summary>
    public class TabPage
    {
        public event ParentChangedEventHandler ParentChanged;
        public delegate void ParentChangedEventHandler();

        public object Tag;

        internal TabPageControl TabPageCtl;

        private PageCollection myParent = null;
        /// <summary>
        /// Gets the tabpages control to which this page belongs.
        /// </summary>
        public PageCollection Parent
        {
            get { return myParent; }
        }

        /// <summary>
        /// Gets or sets the text to be displayed in the tab.
        /// </summary>
        public string Text
        {
            get { return TabPageCtl.Text; }
            set { TabPageCtl.Text = value; }
        }

        /// <summary>
        /// Gets or sets the text to be displayed as the tab's tool-tip (in addition to the tab's text).
        /// </summary>
        public string ToolTip
        {
            get { return TabPageCtl.ToolTip; }
            set { TabPageCtl.ToolTip = value; }
        }

        private Control myControl = null;
        /// <summary>
        /// Gets or sets the control to be displayed when this tab is selected.
        /// </summary>
        public Control Control
        {
            get { return myControl; }
            set { myControl = value; }
        }

        /// <summary>
        /// Gets or sets the context menu on right click
        /// </summary>
        public ContextMenuStrip Menu
        {
            get { return TabPageCtl.Menu; }
            set { TabPageCtl.Menu = value; }
        }

        /// <summary>
        /// Closes this tab (and raises the close event).
        /// </summary>
        public void Close()
        {
            this.TabPageCtl.Close();
        }

        public TabPage(string text, Control control, string toolTip /*default null*/, ContextMenuStrip menu)
        {
            this.TabPageCtl = new TabPageControl(this);
            this.Text = text;
            this.Control = control;
            this.ToolTip = toolTip;
            this.Menu = menu;
        }

        internal void SetParent(PageCollection parent)
        {
            this.myParent = parent;
            if (ParentChanged != null)
            {
                ParentChanged();
            }
        }
    }
}