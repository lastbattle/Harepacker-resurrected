/* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System.Runtime.InteropServices;
using System.Windows.Forms;
using System;
using System.Threading;

namespace HaCreator.ThirdParty.TabPages
{
    /// <summary>
    /// This is the underlying control which eliminates flickering by disabling painting of all
    /// child and sub-controls.
    /// </summary>
    public abstract class FlickerFreeControl : Control
    {

        private const int WM_SETREDRAW = 0xb;
        [DllImport("User32", EntryPoint = "SendMessage", CharSet = CharSet.Auto, SetLastError = true, ExactSpelling = true)]
        private static extern bool SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        /// <summary>
        /// This is your good old SendMessage function.  We'll be sending the magic WM_SETREDRAW message which suspends and resumes painting for this and all sub controls.  Sweeto.
        /// </summary>

        private int mySuspendPainting = 0;
        /// <summary>
        /// Stop painting while multiple changes are made to this and/or nested controls.
        /// </summary>
        /// <value>True to suspend painting.  False to resume painting.</value>
        /// <remarks>This actually increments a counter for each time it is set to true and decrements that 
        /// counter for each time it is set to false.  This is to prevent painting from resuming earlier than
        /// we might wish.</remarks>
        protected bool SuspendPainting
        {
            get { return mySuspendPainting > 0; }
            set
            {
                if ((!this.Visible || !this.IsHandleCreated)) return;

                if ((value))
                {
                    mySuspendPainting += 1;
                    if ((Interlocked.Increment(ref mySuspendPainting) == 1))
                    {
                        SendMessage(this.Handle, WM_SETREDRAW, 0, 0);
                    }
                }
                else
                {
                    if ((Interlocked.Decrement(ref mySuspendPainting) == 0))
                    {
                        SendMessage(this.Handle, WM_SETREDRAW, 1, 0);
                        this.Invalidate(true);
                    }
                }
            }
        }

        public FlickerFreeControl()
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            this.SetStyle(ControlStyles.UserPaint, true);
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        }
    }
}