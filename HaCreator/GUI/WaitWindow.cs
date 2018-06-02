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
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HaCreator.GUI
{
    public partial class WaitWindow : Form
    {
        private bool finished = false;

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
