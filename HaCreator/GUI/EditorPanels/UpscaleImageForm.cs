using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HaCreator.GUI.EditorPanels
{
    public partial class UpscaleImageForm : System.Windows.Forms.Form
    {
        private bool _bNotUserClosing = false;


        private bool _bUserAcceptedImage = false;
        /// <summary>
        /// The return values for the user selection
        /// </summary>
        public bool UserAcceptedImage
        {
            get { return _bUserAcceptedImage; }
            private set { this._bUserAcceptedImage = value; }
        }


        private Bitmap _upscaledImage = null;
        public Bitmap UpscaledImage
        {
            get { return _upscaledImage; }
            private set { this._upscaledImage = value; }
        }


        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="beforeImage"></param>
        public UpscaleImageForm(Bitmap beforeImage)
        {
            InitializeComponent();

            // disallow enable until its rendered
            this.button_ok.Enabled = false;

            this.FormClosing += OnFormClosing;
            this.KeyDown += Load_KeyDown;
            this.Load += UpscaleImageForm_Load;

            this.pictureBox_before.Image = beforeImage;
        }

        #region Window events
        /// <summary>
        /// On load
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <returns></returns>
        private async void UpscaleImageForm_Load(object sender, EventArgs e)
        {
            if (this.pictureBox_before.Image != null)
            {
                Bitmap returnImage = await AiSingleImageUpscale((Bitmap)this.pictureBox_before.Image, 0.25f);

                this._upscaledImage = returnImage;
                this.pictureBox_after.Image = returnImage;

                // disallow enable until its rendered
                this.button_ok.Enabled = true;
            }
        }

        /// <summary>
        /// The form is being closed by the user (e.g., clicking the X button)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && !_bNotUserClosing)
            {
                _bUserAcceptedImage = false;
            }
            this.pictureBox_after.Image = null;
            this.pictureBox_before.Image = null;
        }

        private void Load_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                _bUserAcceptedImage = false;
                Close();
            }
            else if (e.KeyCode == Keys.Enter)
            {
                button_ok_Click(null, null);
            }
        }
        #endregion

        #region UI Events
        /// <summary>
        /// Ok ok accepted click
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void button_ok_Click(object sender, EventArgs e)
        {
            _bUserAcceptedImage = true;
            _bNotUserClosing = true;
            Close();

            await Task.Run(async () =>
            {
                await Task.Delay(2000); // fix image not unloaded
            });
        }

        /// <summary>
        /// Ok cancel click
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_cancel_Click(object sender, EventArgs e)
        {
            Close();
        }
        #endregion

        #region Image upscale
        private async Task<Bitmap> AiSingleImageUpscale(Bitmap inputImage, float downscaleFactorAfter)
        {
            const float SCALE_UP_FACTOR = 4; // factor to scale up with neural networks

            // Create temporary directories
            string pathIn = Path.Combine(Path.GetTempPath(), "HaCreator_ImageUpscaleInput_" + Guid.NewGuid().ToString());
            string pathOut = Path.Combine(Path.GetTempPath(), "HaCreator_ImageUpscaleOutput_" + Guid.NewGuid().ToString());

            try
            {
                Directory.CreateDirectory(pathIn);
                Directory.CreateDirectory(pathOut);

                // Save input image
                string inputFilePath = Path.Combine(pathIn, "input.png");
                inputImage.Save(inputFilePath, System.Drawing.Imaging.ImageFormat.Png);

                // Upscale image
                await RealESRGAN_AI_Upscale.EsrganNcnn.Run(pathIn, pathOut, (int)SCALE_UP_FACTOR);

                // Load upscaled image
                string outputFilePath = Path.Combine(pathOut, "input.png");
                using (Bitmap upscaledBitmap = new Bitmap(outputFilePath))
                {
                    // Downscale if necessary
                    if (downscaleFactorAfter != 1)
                    {
                        int newWidth = (int)(upscaledBitmap.Width * downscaleFactorAfter);
                        int newHeight = (int)(upscaledBitmap.Height * downscaleFactorAfter);

                        Bitmap finalBitmap = new Bitmap(newWidth, newHeight);
                        using (Graphics g = Graphics.FromImage(finalBitmap))
                        {
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

                            g.DrawImage(upscaledBitmap, 0, 0, newWidth, newHeight);
                        }

                        return finalBitmap;
                    }
                    else
                    {
                        return new Bitmap(upscaledBitmap);
                    }
                }
            }
            finally
            {
                // Clean up temporary directories
                if (Directory.Exists(pathIn))
                    Directory.Delete(pathIn, true);
                if (Directory.Exists(pathOut))
                    Directory.Delete(pathOut, true);
            }
        }
        #endregion
    }
}
