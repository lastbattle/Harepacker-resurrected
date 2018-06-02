/* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace Footholds
{
   public class Portals
    {
        public struct Portal
        {
            public Rectangle Shape;
            public WzSubProperty Data;
        }
    }
}
