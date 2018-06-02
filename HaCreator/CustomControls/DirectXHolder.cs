/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace HaCreator
{
    public partial class DirectXHolder : UserControl
    {
        public DirectXHolder()
        {
            InitializeComponent();
            SetStyle(ControlStyles.Opaque | ControlStyles.AllPaintingInWmPaint, true);
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            base.OnKeyDown(new KeyEventArgs(keyData));
            return true;
        }
    }
}
