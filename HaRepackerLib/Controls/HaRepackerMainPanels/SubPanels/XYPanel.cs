/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System.Windows.Forms;

namespace HaRepackerLib
{
    public partial class XYPanel : UserControl
    {
        public XYPanel()
        {
            InitializeComponent();
        }

        public int X
        {
            get { return xBox.Value; }
            set { xBox.Value = value; }
        }

        public int Y
        {
            get { return yBox.Value; }
            set { yBox.Value = value; }
        }
    }
}
