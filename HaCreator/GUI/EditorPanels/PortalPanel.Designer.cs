namespace HaCreator.GUI.EditorPanels
{
    partial class PortalPanel
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
            this.portalImageContainer = new HaCreator.ThirdParty.ThumbnailFlowLayoutPanel();
            this.SuspendLayout();
            // 
            // portalImageContainer
            // 
            this.portalImageContainer.AutoScroll = true;
            this.portalImageContainer.BackColor = System.Drawing.Color.White;
            this.portalImageContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.portalImageContainer.Location = new System.Drawing.Point(0, 0);
            this.portalImageContainer.Name = "portalImageContainer";
            this.portalImageContainer.Size = new System.Drawing.Size(284, 458);
            this.portalImageContainer.TabIndex = 2;
            // 
            // PortalPanel
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(284, 458);
            this.CloseButton = false;
            this.CloseButtonVisible = false;
            this.Controls.Add(this.portalImageContainer);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(177)));
            this.Name = "PortalPanel";
            this.ShowIcon = false;
            this.Text = "Portals";
            this.ResumeLayout(false);

        }

        #endregion

        private ThirdParty.ThumbnailFlowLayoutPanel portalImageContainer;
    }
}