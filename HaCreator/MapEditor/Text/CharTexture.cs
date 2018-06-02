/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.MapEditor.Text
{
    internal class CharTexture
    {
        internal Texture2D texture;
        internal int w;
        internal int h;

        internal CharTexture(Texture2D texture, int w, int h)
        {
            this.texture = texture;
            this.w = w;
            this.h = h;
        }
    }
}
