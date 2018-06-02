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
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;
using System.Drawing.Drawing2D;

namespace HaCreator.ThirdParty
{
    public partial class ImageViewer : UserControl
    {
        private Image m_Image;
        private string m_ImageLocation;

        private bool m_IsThumbnail;
        private bool m_IsActive;

        public int maxWidth;
        public int maxHeight;

        public bool IsText;

        public ImageViewer()
        {
            m_IsThumbnail = false;
            m_IsActive = false;

            InitializeComponent();
        }

        public Image Image
        {
            set { m_Image = value; }
            get { return m_Image; }
        }

        public string ImageLocation
        {
            set { m_ImageLocation = value; }
            get { return m_ImageLocation; }
        }

        public bool IsActive
        {
            set 
            { 
                m_IsActive = value;
                this.Invalidate();
            }
            get { return m_IsActive; }
        }

        public bool IsThumbnail
        {
            set { m_IsThumbnail = value; }
            get { return m_IsThumbnail; }
        }

       /* public void ImageSizeChanged(object sender, ThumbnailImageEventArgs e)
        {
            this.Width = e.Size;
            this.Height = e.Size;
            this.Invalidate();
        }*/

        public void LoadImage(string imageFilename, int width, int height)
        {
            Image tempImage = Image.FromFile(imageFilename);
            m_ImageLocation = imageFilename;

            int dw = tempImage.Width;
            int dh = tempImage.Height;
            int tw = width;
            int th = height;
            double zw = (tw / (double)dw);
            double zh = (th / (double)dh);
            double z = (zw <= zh) ? zw : zh;
            dw = (int)(dw * z);
            dh = (int)(dh * z);

            m_Image = new Bitmap(dw, dh);
            Graphics g = Graphics.FromImage(m_Image);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(tempImage, 0, 0, dw, dh);
            g.Dispose();

            tempImage.Dispose();
        }

        private float CalculateMP()
        {
            float mp = 1;
            float dx = 0, dy = 0;
            if (MaxWidth > 0 && m_Image.Width > MaxWidth)
            {
                dx = (float)MaxWidth / m_Image.Width;
            }
            if (MaxHeight > 0 && m_Image.Height > MaxHeight)
            {
                dy = (float)MaxHeight / m_Image.Height;
            }

            if (dx != 0 && dy == 0) mp = dx;
            else if (dy != 0 && dx == 0) mp = dy;
            else if (dx != 0 && dy != 0) mp = Math.Min(dx, dy);
            return mp;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            if (g == null) return;
            if (m_Image == null) return;

            float mp = CalculateMP();

            g.Clear(Color.White);
            g.DrawImage(m_Image, 4, 4, m_Image.Width * mp, m_Image.Height * mp);
            if (m_IsActive)
            {
                g.DrawRectangle(new Pen(Color.White, 1), 3, 3, m_Image.Width * mp + 2, m_Image.Height * mp + 2);
                g.DrawRectangle(new Pen(Color.Blue, 2), 1, 1, m_Image.Width * mp + 6, m_Image.Height * mp + 6);
            }
            if (IsText)
                g.DrawString(Name, new System.Drawing.Font("Microsoft Sans Serif", 8.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(177))), Brushes.Black, 0, m_Image.Height * mp + 7);
            /*g.DrawRectangle(new Pen(Color.Gray), dl, dt, dw, dh);

            if (m_IsThumbnail)
            for (int j = 0; j < 3; j++)
            {
                g.DrawLine(new Pen(Color.DarkGray),
                    new Point(dl + 3, dt + dh + 1 + j),
                    new Point(dl + dw + 3, dt + dh + 1 + j));
                g.DrawLine(new Pen(Color.DarkGray),
                    new Point(dl + dw + 1 + j, dt + 3),
                    new Point(dl + dw + 1 + j, dt + dh + 3));
            }


            g.DrawImage(m_Image, dl, dt, dw, dh);

            if (m_IsActive)
            {
                g.DrawRectangle(new Pen(Color.White, 1), dl, dt, dw, dh);
                g.DrawRectangle(new Pen(Color.Blue, 2), dl-2, dt-2, dw+4, dh+4);
            }*/
        }

        private void OnResize(object sender, EventArgs e)
        {
            this.Invalidate();
        }

        public static void item_MouseUp(object sender, EventArgs e)
        {
            ((ImageViewer)sender).IsActive = false;
        }

        public int MaxHeight
        {
            get { return maxHeight; }
            set
            {
                maxHeight = value;
                Height = (int)((Height - 20) * CalculateMP() + 20);
            }
        }

        public int MaxWidth
        {
            get { return maxWidth; }
            set
            {
                maxWidth = value;
                Width = (int)((Width - 8) * CalculateMP() + 8);
            }
        }

    }
}
