using System;
using System.Windows.Forms;
using System.Drawing;

namespace HaCreator.CustomControls
{
    public class ThumbnailFlowLayoutPanel : FlowLayoutPanel
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public ThumbnailFlowLayoutPanel()
        {
            InitializeComponent();
            this.FlowDirection = FlowDirection.LeftToRight;
            this.WrapContents = true; // Allow wrapping to multiple rows
            this.AutoScroll = true;
        }

        protected override Point ScrollToControl(Control activeControl)
        {
            return this.AutoScrollPosition;
        }

        public ImageViewer Add(Bitmap bitmap, string name, bool Text)
        {
            ImageViewer imageViewer = new ImageViewer();
            imageViewer.Dock = DockStyle.None; // Remove DockStyle.Left, let FlowLayoutPanel handle layout

            if (bitmap == null)
            {
                Bitmap fallbackBmp = global::HaCreator.Properties.Resources.placeholder;
                imageViewer.Image = fallbackBmp;
                imageViewer.Width = fallbackBmp.Width + 8;
                imageViewer.Height = Math.Max(fallbackBmp.Height + 8 + (Text ? 16 : 0), 32);
            }
            else
            {
                imageViewer.Image = new Bitmap(bitmap);
                imageViewer.Width = bitmap.Width + 8;
                imageViewer.Height = Math.Max(bitmap.Height + 8 + (Text ? 16 : 0), 32);
            }
            imageViewer.IsText = Text;
            imageViewer.Name = name;
            imageViewer.IsThumbnail = true;

            // Force a minimum height in case something else sets it to 0
            if (imageViewer.Height < 32)
                imageViewer.Height = 32;

            // Set a minimum size to ensure visibility
            imageViewer.MinimumSize = new Size(32, 32);

            Controls.Add(imageViewer);
            imageViewer.Invalidate();
            imageViewer.Refresh();

            System.Diagnostics.Debug.WriteLine($"[ThumbnailFlowLayoutPanel] Added '{name}' with size {imageViewer.Width}x{imageViewer.Height}, min {imageViewer.MinimumSize.Width}x{imageViewer.MinimumSize.Height}");

            return imageViewer;
        }

        public void Remove(ImageViewer imageViewer)
        {
            Controls.Remove(imageViewer);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // ThumbnailFlowLayoutPanel
            // 
            this.Dock = System.Windows.Forms.DockStyle.Fill;
            this.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.WrapContents = true; // Enable wrapping for multiple rows
            this.AutoScroll = true;
            this.ResumeLayout(false);

        }
    }
}