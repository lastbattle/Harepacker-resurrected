/* Copyright (C) 2022 LastBattle
* https://github.com/lastbattle/Harepacker-resurrected
* 
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaSharedLibrary.Render
{
    public interface IBaseDXDrawableItem
    {
        bool IsFrameWithinView(IDXObject frame, int shiftCenteredX, int shiftCenteredY, int width, int height);

        void CopyObjectPosition(IBaseDXDrawableItem copySrc);

        Point Position { get; set; }
    }
}
