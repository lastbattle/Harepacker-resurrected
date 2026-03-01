/* Copyright (C) 2022 LastBattle
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

namespace HaSharedLibrary.Render
{
    public class ReflectionDrawableBoundary
    {
        private ushort gradient;
        private ushort alpha;
        private string objectForOverlay = "";
        private bool reflection;
        private bool alphaTest;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="gradient"></param>
        /// <param name="alpha"></param>
        /// <param name="objectForOverlay"></param>
        /// <param name="reflection"></param>
        /// <param name="alphaTest"></param>
        public ReflectionDrawableBoundary(ushort gradient, ushort alpha, string objectForOverlay, bool reflection, bool alphaTest)
        {
            //this.lt = lt;
            //this.rb = rb;
            //this.offset = offset;
            this.gradient = gradient;
            this.alpha = alpha;
            this.objectForOverlay = objectForOverlay;
            this.reflection = reflection;
            this.alphaTest = alphaTest;
        }

        #region Inherited Members
        #endregion

        #region Custom Members
        /*public Vector2 Lt
        {
            get { return lt; }
            set { this.lt = value; }
        }

        public Vector2 Rb
        {
            get { return rb; }
            set { this.rb = value; }
        }

        public Vector2 Offset
        {
            get { return offset; }
            set { this.offset = value; }
        }*/

        public ushort Gradient
        {
            get { return this.gradient; }
            set { this.gradient = value; }
        }

        public ushort Alpha
        {
            get { return alpha; }
            set { this.alpha = value; }
        }

        public string ObjectForOverlay
        {
            get { return objectForOverlay; }
            set { objectForOverlay = value; }
        }

        public bool Reflection
        {
            get { return reflection; }
            set { reflection = value; }
        }

        public bool AlphaTest
        {
            get { return alphaTest; }
            set { alphaTest = value; }
        }
        #endregion
    }
}
