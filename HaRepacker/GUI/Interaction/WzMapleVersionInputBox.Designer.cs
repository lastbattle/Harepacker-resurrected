namespace HaRepacker.GUI.Interaction
{
    partial class WzMapleVersionInputBox
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(WzMapleVersionInputBox));
            this.okButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.label_wzEncrytionType = new System.Windows.Forms.Label();
            this.panel1 = new System.Windows.Forms.Panel();
            this.comboBox_wzEncryptionType = new System.Windows.Forms.ComboBox();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // okButton
            // 
            resources.ApplyResources(this.okButton, "okButton");
            this.okButton.Name = "okButton";
            this.okButton.UseVisualStyleBackColor = true;
            this.okButton.Click += new System.EventHandler(this.okButton_Click);
            // 
            // cancelButton
            // 
            resources.ApplyResources(this.cancelButton, "cancelButton");
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.UseVisualStyleBackColor = true;
            this.cancelButton.Click += new System.EventHandler(this.cancelButton_Click);
            // 
            // label_wzEncrytionType
            // 
            resources.ApplyResources(this.label_wzEncrytionType, "label_wzEncrytionType");
            this.label_wzEncrytionType.Name = "label_wzEncrytionType";
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.comboBox_wzEncryptionType);
            resources.ApplyResources(this.panel1, "panel1");
            this.panel1.Name = "panel1";
            // 
            // comboBox_wzEncryptionType
            // 
            this.comboBox_wzEncryptionType.FormattingEnabled = true;
            resources.ApplyResources(this.comboBox_wzEncryptionType, "comboBox_wzEncryptionType");
            this.comboBox_wzEncryptionType.Name = "comboBox_wzEncryptionType";
            this.comboBox_wzEncryptionType.SelectedIndexChanged += new System.EventHandler(this.comboBox_Encryption_SelectedIndexChanged);
            // 
            // WzMapleVersionInputBox
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.label_wzEncrytionType);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.okButton);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "WzMapleVersionInputBox";
            this.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.keyPress);
            this.panel1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.Label label_wzEncrytionType;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.ComboBox comboBox_wzEncryptionType;
    }
}