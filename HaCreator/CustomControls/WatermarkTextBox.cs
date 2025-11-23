using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HaCreator.CustomControls
{
    public class WatermarkTextBox : TextBox
    {
        /// <summary>
        /// The text that will be presented as the watermak hint
        /// </summary>
        private string _watermarkText = "Type here";
        /// <summary>
        /// Gets or Sets the text that will be presented as the watermak hint
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string WatermarkText
        {
            get { return _watermarkText; }
            set { _watermarkText = value; }
        }

        /// <summary>
        /// Whether watermark effect is enabled or not
        /// </summary>
        private bool _watermarkActive = true;
        /// <summary>
        /// Gets or Sets whether watermark effect is enabled or not
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool WatermarkActive
        {
            get { return _watermarkActive; }
            set { _watermarkActive = value; }
        }

        /// <summary>
        /// Create a new TextBox that supports watermak hint
        /// </summary>
        public WatermarkTextBox()
        {
            this._watermarkActive = true;
            this.Text = _watermarkText;
            this.ForeColor = Color.Gray;

            GotFocus += (source, e) =>
            {
                RemoveWatermak();
            };

            LostFocus += (source, e) =>
            {
                ApplyWatermark();
            };

        }

        /// <summary>
        /// Remove watermark from the textbox
        /// </summary>
        public void RemoveWatermak()
        {
            if (this._watermarkActive)
            {
                this._watermarkActive = false;
                this.Text = "";
                this.ForeColor = Color.Black;
            }
        }

        /// <summary>
        /// Applywatermak immediately
        /// </summary>
        public void ApplyWatermark()
        {
            if (!this._watermarkActive && string.IsNullOrEmpty(this.Text)
                || ForeColor == Color.Gray)
            {
                this._watermarkActive = true;
                this.Text = _watermarkText;
                this.ForeColor = Color.Gray;
            }
        }

        /// <summary>
        /// Apply watermak to the textbox. 
        /// </summary>
        /// <param name="newText">Text to apply</param>
        public void ApplyWatermark(string newText)
        {
            WatermarkText = newText;
            ApplyWatermark();
        }

        protected override void OnTextChanged(EventArgs e)
        {
            if (WatermarkActive)
            {
                return;
            }

            base.OnTextChanged(e);
        }
    }
}
