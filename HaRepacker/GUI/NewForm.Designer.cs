using HaRepacker.GUI.Input;

namespace HaRepacker.GUI
{
    partial class NewForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(NewForm));
            this.nameBox = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.regBox = new System.Windows.Forms.RadioButton();
            this.listBox = new System.Windows.Forms.RadioButton();
            this.copyrightBox = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.okButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.label5 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.encryptionBox = new System.Windows.Forms.ComboBox();
            this.versionBox = new IntegerInput();
            this.radioButton_hotfix = new System.Windows.Forms.RadioButton();
            this.SuspendLayout();
            // 
            // nameBox
            // 
            resources.ApplyResources(this.nameBox, "nameBox");
            this.nameBox.Name = "nameBox";
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // regBox
            // 
            resources.ApplyResources(this.regBox, "regBox");
            this.regBox.Checked = true;
            this.regBox.Name = "regBox";
            this.regBox.TabStop = true;
            this.regBox.UseVisualStyleBackColor = true;
            this.regBox.CheckedChanged += new System.EventHandler(this.regBox_CheckedChanged);
            // 
            // listBox
            // 
            resources.ApplyResources(this.listBox, "listBox");
            this.listBox.Name = "listBox";
            this.listBox.UseVisualStyleBackColor = true;
            this.listBox.CheckedChanged += new System.EventHandler(this.listwz_CheckedChanged);
            // 
            // copyrightBox
            // 
            resources.ApplyResources(this.copyrightBox, "copyrightBox");
            this.copyrightBox.Name = "copyrightBox";
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
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
            // label5
            // 
            resources.ApplyResources(this.label5, "label5");
            this.label5.Name = "label5";
            // 
            // label6
            // 
            resources.ApplyResources(this.label6, "label6");
            this.label6.Name = "label6";
            // 
            // encryptionBox
            // 
            this.encryptionBox.FormattingEnabled = true;
            resources.ApplyResources(this.encryptionBox, "encryptionBox");
            this.encryptionBox.Name = "encryptionBox";
            // 
            // versionBox
            // 
            resources.ApplyResources(this.versionBox, "versionBox");
            this.versionBox.Name = "versionBox";
            this.versionBox.Value = 1;
            // 
            // radioButton_hotfix
            // 
            resources.ApplyResources(this.radioButton_hotfix, "radioButton_hotfix");
            this.radioButton_hotfix.Name = "radioButton_hotfix";
            this.radioButton_hotfix.UseVisualStyleBackColor = true;
            this.radioButton_hotfix.CheckedChanged += new System.EventHandler(this.DataWZ_CheckedChanged);
            // 
            // NewForm
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.radioButton_hotfix);
            this.Controls.Add(this.versionBox);
            this.Controls.Add(this.encryptionBox);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.okButton);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.copyrightBox);
            this.Controls.Add(this.listBox);
            this.Controls.Add(this.regBox);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.nameBox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "NewForm";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox nameBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.RadioButton regBox;
        private System.Windows.Forms.RadioButton listBox;
        private System.Windows.Forms.TextBox copyrightBox;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.ComboBox encryptionBox;
        private IntegerInput versionBox;
        private System.Windows.Forms.RadioButton radioButton_hotfix;
    }
}