namespace HaCreator.MapSimulator
{
    partial class MapSimulator
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // MapSimulator
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(784, 562);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "MapSimulator";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.Text = "MapSimulator";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MapSimulator_FormClosing);
            this.Load += new System.EventHandler(this.MapSimulator_Load);
            this.Resize += new System.EventHandler(this.MapSimulator_Resize);
            this.ResumeLayout(false);

        }

        #endregion
    }
}