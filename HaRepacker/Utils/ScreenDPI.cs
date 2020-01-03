/* Copyright (C) 2020 LastBattle
* https://github.com/lastbattle/Harepacker-resurrected
* 
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaRepacker.Utils
{
    public class ScreenDPI
    {
        /// <summary>
        /// Gets the screen scale factor of the current user
        /// </summary>
        /// <returns></returns>
        public static double GetScreenScaleFactor()
        {
            double resHeight = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height; 
            double actualHeight = System.Windows.SystemParameters.PrimaryScreenHeight; 
            double ratio = actualHeight / resHeight;
            double dpi = resHeight / actualHeight;

            // thank these niggas
            // https://stackoverflow.com/questions/1918877/how-can-i-get-the-dpi-in-wpf

            /*            var dpiXProperty = typeof(System.Windows.SystemParameters).GetProperty("DpiX", BindingFlags.NonPublic | BindingFlags.Static);
            var dpiYProperty = typeof(System.Windows.SystemParameters).GetProperty("Dpi", BindingFlags.NonPublic | BindingFlags.Static);
            int dpiX = (int)dpiXProperty.GetValue(null, null);
            int dpiY = (int)dpiYProperty.GetValue(null, null);
            double scaleFactor = dpiX / 96d;*/

            return dpi;
        }
    }
}
