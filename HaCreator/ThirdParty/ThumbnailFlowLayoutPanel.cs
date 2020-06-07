/* 
  koolk's Map Editor
 
  Copyright (c) 2009-2013 koolk

  This software is provided 'as-is', without any express or implied
  warranty. In no event will the authors be held liable for any damages
  arising from the use of this software.

  Permission is granted to anyone to use this software for any purpose,
  including commercial applications, and to alter it and redistribute it
  freely, subject to the following restrictions:

     1. The origin of this software must not be misrepresented; you must not
     claim that you wrote the original software. If you use this software
     in a product, an acknowledgment in the product documentation would be
     appreciated but is not required.

     2. Altered source versions must be plainly marked as such, and must not be
     misrepresented as being the original software.

     3. This notice may not be removed or altered from any source
     distribution.
*/

using System;
using System.Windows.Forms;
using System.Drawing;

namespace HaCreator.ThirdParty
{
    public class ThumbnailFlowLayoutPanel : FlowLayoutPanel
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public ThumbnailFlowLayoutPanel()
        {
        }

        protected override Point ScrollToControl(Control activeControl)
        {
            return this.AutoScrollPosition;
        }

        public ImageViewer Add(Bitmap bitmap, String name, bool Text)
        {
            ImageViewer imageViewer = new ImageViewer();
            imageViewer.Dock = DockStyle.Left;

            if (bitmap == null)
            {
                Bitmap fallbackBmp = global::HaCreator.Properties.Resources.placeholder;

                imageViewer.Image = fallbackBmp; // fallback in case its null
                imageViewer.Width = fallbackBmp.Width + 8;
                imageViewer.Height = fallbackBmp.Height + 8 + ((Text) ? 12 : 0);
            }
            else
            {
                imageViewer.Image = new Bitmap(bitmap); // Copying the bitmap for thread safety
                imageViewer.Width = bitmap.Width + 8;
                imageViewer.Height = bitmap.Height + 8 + ((Text) ? 12 : 0);
            }
            imageViewer.IsText = Text;
            imageViewer.Name = name;
            imageViewer.IsThumbnail = false;

            Controls.Add(imageViewer);

            return imageViewer;
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // ThumbnailFlowLayoutPanel
            // 
            this.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.ResumeLayout(false);
        }
    }
}