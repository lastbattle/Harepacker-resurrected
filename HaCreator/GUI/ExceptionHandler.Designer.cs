namespace HaCreator.GUI
{
    partial class ExceptionHandler
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
            this.crashMessageLabel = new System.Windows.Forms.Label();
            this.restartButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // crashMessageLabel
            // 
            this.crashMessageLabel.AutoEllipsis = true;
            this.crashMessageLabel.Location = new System.Drawing.Point(2, 9);
            this.crashMessageLabel.Name = "crashMessageLabel";
            this.crashMessageLabel.Size = new System.Drawing.Size(540, 42);
            this.crashMessageLabel.TabIndex = 0;
            // 
            // restartButton
            // 
            this.restartButton.Location = new System.Drawing.Point(203, 54);
            this.restartButton.Name = "restartButton";
            this.restartButton.Size = new System.Drawing.Size(153, 43);
            this.restartButton.TabIndex = 1;
            this.restartButton.UseVisualStyleBackColor = true;
            // 
            // ExceptionHandler
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(554, 109);
            this.Controls.Add(this.restartButton);
            this.Controls.Add(this.crashMessageLabel);
            this.Name = "ExceptionHandler";
            this.Text = "OH SHI-";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label crashMessageLabel;
        private System.Windows.Forms.Button restartButton;
    }
}