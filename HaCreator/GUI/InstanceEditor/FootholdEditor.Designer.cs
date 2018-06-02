namespace HaCreator.GUI.InstanceEditor
{
    partial class FootholdEditor
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
            this.forceInt = new System.Windows.Forms.NumericUpDown();
            this.forceEnable = new System.Windows.Forms.CheckBox();
            this.pieceEnable = new System.Windows.Forms.CheckBox();
            this.pieceInt = new System.Windows.Forms.NumericUpDown();
            this.cantThroughBox = new System.Windows.Forms.CheckBox();
            this.forbidFallDownBox = new System.Windows.Forms.CheckBox();
            this.okButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.forceInt)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pieceInt)).BeginInit();
            this.SuspendLayout();
            // 
            // forceInt
            // 
            this.forceInt.Location = new System.Drawing.Point(69, 12);
            this.forceInt.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.forceInt.Minimum = new decimal(new int[] {
            -2147483648,
            0,
            0,
            -2147483648});
            this.forceInt.Name = "forceInt";
            this.forceInt.Size = new System.Drawing.Size(123, 20);
            this.forceInt.TabIndex = 1;
            // 
            // forceEnable
            // 
            this.forceEnable.AutoSize = true;
            this.forceEnable.Location = new System.Drawing.Point(12, 14);
            this.forceEnable.Name = "forceEnable";
            this.forceEnable.Size = new System.Drawing.Size(53, 17);
            this.forceEnable.TabIndex = 0;
            this.forceEnable.Text = "Force";
            this.forceEnable.CheckedChanged += new System.EventHandler(this.forceEnable_CheckedChanged);
            // 
            // pieceEnable
            // 
            this.pieceEnable.AutoSize = true;
            this.pieceEnable.Location = new System.Drawing.Point(12, 40);
            this.pieceEnable.Name = "pieceEnable";
            this.pieceEnable.Size = new System.Drawing.Size(53, 17);
            this.pieceEnable.TabIndex = 2;
            this.pieceEnable.Text = "Piece";
            // 
            // pieceInt
            // 
            this.pieceInt.Location = new System.Drawing.Point(69, 38);
            this.pieceInt.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.pieceInt.Minimum = new decimal(new int[] {
            -2147483648,
            0,
            0,
            -2147483648});
            this.pieceInt.Name = "pieceInt";
            this.pieceInt.Size = new System.Drawing.Size(123, 20);
            this.pieceInt.TabIndex = 3;
            // 
            // cantThroughBox
            // 
            this.cantThroughBox.AutoSize = true;
            this.cantThroughBox.Location = new System.Drawing.Point(12, 64);
            this.cantThroughBox.Name = "cantThroughBox";
            this.cantThroughBox.Size = new System.Drawing.Size(91, 17);
            this.cantThroughBox.TabIndex = 4;
            this.cantThroughBox.Text = "Cant Through";
            // 
            // forbidFallDownBox
            // 
            this.forbidFallDownBox.AutoSize = true;
            this.forbidFallDownBox.Location = new System.Drawing.Point(108, 64);
            this.forbidFallDownBox.Name = "forbidFallDownBox";
            this.forbidFallDownBox.Size = new System.Drawing.Size(105, 17);
            this.forbidFallDownBox.TabIndex = 5;
            this.forbidFallDownBox.Text = "Forbid Fall Down";
            // 
            // okButton
            // 
            this.okButton.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.okButton.Location = new System.Drawing.Point(33, 85);
            this.okButton.Name = "okButton";
            this.okButton.Size = new System.Drawing.Size(71, 26);
            this.okButton.TabIndex = 6;
            this.okButton.Text = "OK";
            this.okButton.Click += new System.EventHandler(this.okButton_Click);
            // 
            // cancelButton
            // 
            this.cancelButton.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.cancelButton.Location = new System.Drawing.Point(110, 85);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(71, 26);
            this.cancelButton.TabIndex = 7;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.Click += new System.EventHandler(this.cancelButton_Click);
            // 
            // FootholdEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(218, 123);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.okButton);
            this.Controls.Add(this.forbidFallDownBox);
            this.Controls.Add(this.cantThroughBox);
            this.Controls.Add(this.pieceEnable);
            this.Controls.Add(this.pieceInt);
            this.Controls.Add(this.forceEnable);
            this.Controls.Add(this.forceInt);
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FootholdEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Foothold";
            ((System.ComponentModel.ISupportInitialize)(this.forceInt)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pieceInt)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.NumericUpDown forceInt;
        private System.Windows.Forms.CheckBox forceEnable;
        private System.Windows.Forms.CheckBox pieceEnable;
        private System.Windows.Forms.NumericUpDown pieceInt;
        private System.Windows.Forms.CheckBox cantThroughBox;
        private System.Windows.Forms.CheckBox forbidFallDownBox;
        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.Button cancelButton;
    }
}