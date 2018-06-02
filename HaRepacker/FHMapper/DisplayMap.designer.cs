namespace Footholds
{
    partial class DisplayMap
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DisplayMap));
            this.MapPBox = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.MapPBox)).BeginInit();
            this.SuspendLayout();
            // 
            // MapPBox
            // 
            resources.ApplyResources(this.MapPBox, "MapPBox");
            this.MapPBox.Name = "MapPBox";
            this.MapPBox.TabStop = false;
            this.MapPBox.MouseClick += new System.Windows.Forms.MouseEventHandler(this.MapPBox_MouseClick);
            this.MapPBox.MouseDown += new System.Windows.Forms.MouseEventHandler(this.MapPBox_MouseDown);
            this.MapPBox.MouseMove += new System.Windows.Forms.MouseEventHandler(this.MapPBox_MouseMove);
            this.MapPBox.MouseUp += new System.Windows.Forms.MouseEventHandler(this.MapPBox_MouseUp);
            // 
            // DisplayMap
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.MapPBox);
            this.Name = "DisplayMap";
            this.ShowIcon = false;
            this.Load += new System.EventHandler(this.DisplayMap_Load);
            this.MouseMove += new System.Windows.Forms.MouseEventHandler(this.DisplayMap_MouseMove);
            this.Resize += new System.EventHandler(this.DisplayMap_Resize);
            ((System.ComponentModel.ISupportInitialize)(this.MapPBox)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.PictureBox MapPBox;
    }
}