/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.Wz
{
    public class PortalGameImageInfo
    {
        private Bitmap defaultImage;

        private Dictionary<string, Bitmap> imageList;

        public PortalGameImageInfo(Bitmap defaultImage, Dictionary<string, Bitmap> imageList)
        {
            this.defaultImage = defaultImage;
            this.imageList = imageList;
        }

        public Bitmap DefaultImage
        {
            get { return defaultImage; }
        }

        public Bitmap this[string name]
        {
            get
            {
                return imageList[name];
            }
        }

        public IDictionaryEnumerator GetEnumerator()
        {
            return imageList.GetEnumerator();
        }
    }
}
