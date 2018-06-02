/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HaCreator.GUI
{
    public partial class CancelableWaitWindow : Form
    {
        private bool finished = false;
        private Thread actionThread;
        public object result = null;

        public CancelableWaitWindow(string message, Func<object> action)
        {
            InitializeComponent();
            this.label1.Text = message;
            actionThread = new Thread(new ParameterizedThreadStart(x =>
            {
                CancelableWaitWindow cww = (CancelableWaitWindow)x;
                cww.result = action();
                cww.Invoke((Action)delegate { cww.EndWait(); });
            }));
        }

        private void button1_Click(object sender, EventArgs e)
        {
            button1.Enabled = false;
            actionThread.Abort();

            // Is the thread stuck?
            if (!actionThread.Join(5000))
            {
                MessageBox.Show("Could not terminate actionThread after 5000ms. This usually means something has gone horribly wrong. The program may now be in an undefined state, try to save your work and restart.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            EndWait();
        }

        public void EndWait()
        {
            finished = true;
            Close();
        }

        private void CancelableWaitWindow_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = !finished;
        }

        private void CancelableWaitWindow_Load(object sender, EventArgs e)
        {
            actionThread.Start(this);
        }
    }
}
