/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System.Windows.Forms;

namespace HaCreator.GUI
{
    public partial class WaitWindow : Form
    {
        private bool finished = false;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message"></param>
        public WaitWindow(string message)
        {
            InitializeComponent();
            this.label1.Text = message;
        }

        private void WaitWindow_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = !finished;
        }

        public void EndWait()
        {
            finished = true;
            Close();
        }
    }
}
