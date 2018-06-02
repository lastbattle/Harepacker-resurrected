/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using MapleLib.WzLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.Wz
{
    public class WzMainDirectory
    {
        private WzFile file;
        private WzDirectory directory;

        public WzMainDirectory(WzFile file)
        {
            this.file = file;
            this.directory = file.WzDirectory;
        }

        public WzMainDirectory(WzFile file, WzDirectory directory)
        {
            this.file = file;
            this.directory = directory;
        }

        public WzFile File { get { return file; } }
        public WzDirectory MainDir { get { return directory; } }
    }
}
