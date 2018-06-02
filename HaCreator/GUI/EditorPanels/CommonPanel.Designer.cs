namespace HaCreator.GUI.EditorPanels
{
    partial class CommonPanel
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
            this.miscItemsContainer = new HaCreator.ThirdParty.ThumbnailFlowLayoutPanel();
            this.SuspendLayout();
            // 
            // miscItemsContainer
            // 
            this.miscItemsContainer.BackColor = System.Drawing.Color.White;
            this.miscItemsContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.miscItemsContainer.Location = new System.Drawing.Point(0, 0);
            this.miscItemsContainer.Name = "miscItemsContainer";
            this.miscItemsContainer.Size = new System.Drawing.Size(284, 435);
            this.miscItemsContainer.TabIndex = 2;
            // 
            // CommonPanel
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(284, 435);
            this.CloseButton = false;
            this.CloseButtonVisible = false;
            this.Controls.Add(this.miscItemsContainer);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(177)));
            this.Name = "CommonPanel";
            this.ShowIcon = false;
            this.Text = "Common";
            this.ResumeLayout(false);

        }

        #endregion

        private ThirdParty.ThumbnailFlowLayoutPanel miscItemsContainer;
    }
}