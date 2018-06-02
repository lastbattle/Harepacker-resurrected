namespace HaCreator.MapEditor
{
    partial class MultiBoard
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.vScrollBar = new System.Windows.Forms.VScrollBar();
            this.hScrollBar = new System.Windows.Forms.HScrollBar();
            this.DxContainer = new HaCreator.DirectXHolder();
            this.SuspendLayout();
            // 
            // vScrollBar
            // 
            this.vScrollBar.Enabled = false;
            this.vScrollBar.Location = new System.Drawing.Point(129, 0);
            this.vScrollBar.Name = "vScrollBar";
            this.vScrollBar.Size = new System.Drawing.Size(19, 122);
            this.vScrollBar.TabIndex = 1;
            this.vScrollBar.Scroll += new System.Windows.Forms.ScrollEventHandler(this.vScrollBar_Scroll);
            // 
            // hScrollBar
            // 
            this.hScrollBar.Enabled = false;
            this.hScrollBar.Location = new System.Drawing.Point(0, 131);
            this.hScrollBar.Name = "hScrollBar";
            this.hScrollBar.Size = new System.Drawing.Size(150, 19);
            this.hScrollBar.TabIndex = 2;
            this.hScrollBar.Scroll += new System.Windows.Forms.ScrollEventHandler(this.hScrollBar_Scroll);
            // 
            // DxContainer
            // 
            this.DxContainer.AllowDrop = true;
            this.DxContainer.Location = new System.Drawing.Point(3, 3);
            this.DxContainer.Name = "DxContainer";
            this.DxContainer.Size = new System.Drawing.Size(105, 100);
            this.DxContainer.TabIndex = 0;
            this.DxContainer.DragDrop += new System.Windows.Forms.DragEventHandler(this.DxContainer_DragDrop);
            this.DxContainer.DragEnter += new System.Windows.Forms.DragEventHandler(this.DxContainer_DragEnter);
            this.DxContainer.KeyDown += new System.Windows.Forms.KeyEventHandler(this.DxContainer_KeyDown);
            this.DxContainer.MouseClick += new System.Windows.Forms.MouseEventHandler(this.DxContainer_MouseClick);
            this.DxContainer.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.DxContainer_MouseDoubleClick);
            this.DxContainer.MouseDown += new System.Windows.Forms.MouseEventHandler(this.DxContainer_MouseDown);
            this.DxContainer.MouseMove += new System.Windows.Forms.MouseEventHandler(this.DxContainer_MouseMove);
            this.DxContainer.MouseUp += new System.Windows.Forms.MouseEventHandler(this.DxContainer_MouseUp);
            // 
            // MultiBoard
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.hScrollBar);
            this.Controls.Add(this.vScrollBar);
            this.Controls.Add(this.DxContainer);
            this.Name = "MultiBoard";
            this.Size = new System.Drawing.Size(278, 262);
            this.ResumeLayout(false);

        }

        #endregion

        private DirectXHolder DxContainer;
        private System.Windows.Forms.VScrollBar vScrollBar;
        private System.Windows.Forms.HScrollBar hScrollBar;
    }
}
