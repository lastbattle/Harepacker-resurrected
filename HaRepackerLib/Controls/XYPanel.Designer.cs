namespace HaRepackerLib
{
    partial class XYPanel
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
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.xBox = new HaRepackerLib.Controls.IntegerInput();
            this.yBox = new HaRepackerLib.Controls.IntegerInput();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(3, 6);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(17, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "X:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(3, 32);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(17, 13);
            this.label2.TabIndex = 1;
            this.label2.Text = "Y:";
            // 
            // xBox
            // 
            this.xBox.Location = new System.Drawing.Point(26, 3);
            this.xBox.Name = "xBox";
            this.xBox.Size = new System.Drawing.Size(59, 20);
            this.xBox.TabIndex = 4;
            this.xBox.Value = 0;
            // 
            // yBox
            // 
            this.yBox.Location = new System.Drawing.Point(26, 29);
            this.yBox.Name = "yBox";
            this.yBox.Size = new System.Drawing.Size(59, 20);
            this.yBox.TabIndex = 5;
            this.yBox.Value = 0;
            // 
            // XYPanel
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.yBox);
            this.Controls.Add(this.xBox);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.MaximumSize = new System.Drawing.Size(90, 53);
            this.MinimumSize = new System.Drawing.Size(90, 53);
            this.Name = "XYPanel";
            this.Size = new System.Drawing.Size(90, 53);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private HaRepackerLib.Controls.IntegerInput xBox;
        private HaRepackerLib.Controls.IntegerInput yBox;
    }
}
