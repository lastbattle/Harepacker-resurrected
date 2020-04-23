using HaRepacker.GUI.Input;

namespace HaRepacker.GUI
{
    partial class CustomWZEncryptionInputBox
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CustomWZEncryptionInputBox));
            this.saveButton = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.textBox_byte0 = new System.Windows.Forms.TextBox();
            this.textBox_byte1 = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.textBox_byte2 = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.textBox_byte3 = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // saveButton
            // 
            resources.ApplyResources(this.saveButton, "saveButton");
            this.saveButton.Name = "saveButton";
            this.saveButton.UseVisualStyleBackColor = true;
            this.saveButton.Click += new System.EventHandler(this.saveButton_Click);
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // textBox_byte0
            // 
            resources.ApplyResources(this.textBox_byte0, "textBox_byte0");
            this.textBox_byte0.Name = "textBox_byte0";
            // 
            // textBox_byte1
            // 
            resources.ApplyResources(this.textBox_byte1, "textBox_byte1");
            this.textBox_byte1.Name = "textBox_byte1";
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // textBox_byte2
            // 
            resources.ApplyResources(this.textBox_byte2, "textBox_byte2");
            this.textBox_byte2.Name = "textBox_byte2";
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // textBox_byte3
            // 
            resources.ApplyResources(this.textBox_byte3, "textBox_byte3");
            this.textBox_byte3.Name = "textBox_byte3";
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            // 
            // CustomWZEncryptionInputBox
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.textBox_byte3);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.textBox_byte2);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.textBox_byte1);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.textBox_byte0);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.saveButton);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "CustomWZEncryptionInputBox";
            this.Load += new System.EventHandler(this.SaveForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button saveButton;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textBox_byte0;
        private System.Windows.Forms.TextBox textBox_byte1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textBox_byte2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textBox_byte3;
        private System.Windows.Forms.Label label4;
    }
}