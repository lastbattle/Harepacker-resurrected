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
        private readonly Bitmap defaultImage;

        private readonly Dictionary<string, List<Bitmap>> imageList;

        public PortalGameImageInfo(Bitmap defaultImage, Dictionary<string, List<Bitmap>> imageList)
        {
            this.defaultImage = defaultImage;
            this.imageList = imageList;
        }

        public Bitmap DefaultImage
        {
            get { return defaultImage; }
        }

        public List<Bitmap> this[string name]
        {
            get
            {
                if (!imageList.ContainsKey(name))
                    return imageList["default"];
                return imageList[name];
            }
        }

        public IDictionaryEnumerator GetEnumerator()
        {
            return imageList.GetEnumerator();
        }
    }
}
