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

namespace HaCreator.MapEditor
{
    public class GraphicsDeviceService : IGraphicsDeviceService
    {
        private GraphicsDevice device;

        public GraphicsDeviceService(GraphicsDevice device)
        {
            this.device = device;
            device.Disposing += new EventHandler<EventArgs>(device_Disposing);
            device.DeviceResetting += new EventHandler<EventArgs>(device_DeviceResetting);
            device.DeviceReset += new EventHandler<EventArgs>(device_DeviceReset);
            if (DeviceCreated != null) DeviceCreated.Invoke(device, new EventArgs());
        }

        void device_DeviceReset(object sender, EventArgs e)
        {
            if (DeviceReset != null) DeviceReset.Invoke(sender, e);
        }

        void device_DeviceResetting(object sender, EventArgs e)
        {
            if (DeviceResetting != null) DeviceResetting.Invoke(sender, e);
        }

        void device_Disposing(object sender, EventArgs e)
        {
            if (DeviceDisposing != null) DeviceDisposing.Invoke(sender, e);
        }

        public GraphicsDevice GraphicsDevice
        {
            get { return device; }
        }

        public event EventHandler<EventArgs> DeviceCreated;
        public event EventHandler<EventArgs> DeviceDisposing;
        public event EventHandler<EventArgs> DeviceReset;
        public event EventHandler<EventArgs> DeviceResetting;
    }
}
