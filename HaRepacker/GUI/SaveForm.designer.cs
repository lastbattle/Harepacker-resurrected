using HaRepacker.GUI.Input;

namespace HaRepacker.GUI
{
    partial class SaveForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SaveForm));
            encryptionBox = new System.Windows.Forms.ComboBox();
            saveButton = new System.Windows.Forms.Button();
            label1 = new System.Windows.Forms.Label();
            label2 = new System.Windows.Forms.Label();
            checkBox_64BitFile = new System.Windows.Forms.CheckBox();
            versionBox = new IntegerInput();
            groupBox1 = new System.Windows.Forms.GroupBox();
            groupBox_wzSaveSelection = new System.Windows.Forms.GroupBox();
            radioButton1 = new System.Windows.Forms.RadioButton();
            radioButton_wzFile = new System.Windows.Forms.RadioButton();
            groupBox1.SuspendLayout();
            groupBox_wzSaveSelection.SuspendLayout();
            SuspendLayout();
            // 
            // encryptionBox
            // 
            encryptionBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            encryptionBox.FormattingEnabled = true;
            resources.ApplyResources(encryptionBox, "encryptionBox");
            encryptionBox.Name = "encryptionBox";
            encryptionBox.SelectedIndexChanged += encryptionBox_SelectedIndexChanged;
            // 
            // saveButton
            // 
            resources.ApplyResources(saveButton, "saveButton");
            saveButton.Name = "saveButton";
            saveButton.UseVisualStyleBackColor = true;
            saveButton.Click += SaveButton_Click;
            // 
            // label1
            // 
            resources.ApplyResources(label1, "label1");
            label1.Name = "label1";
            // 
            // label2
            // 
            resources.ApplyResources(label2, "label2");
            label2.Name = "label2";
            // 
            // checkBox_64BitFile
            // 
            resources.ApplyResources(checkBox_64BitFile, "checkBox_64BitFile");
            checkBox_64BitFile.Name = "checkBox_64BitFile";
            checkBox_64BitFile.UseVisualStyleBackColor = true;
            checkBox_64BitFile.CheckedChanged += checkBox_64BitFile_CheckedChanged;
            // 
            // versionBox
            // 
            resources.ApplyResources(versionBox, "versionBox");
            versionBox.Name = "versionBox";
            versionBox.Value = 0;
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(groupBox_wzSaveSelection);
            groupBox1.Controls.Add(saveButton);
            groupBox1.Controls.Add(radioButton1);
            groupBox1.Controls.Add(radioButton_wzFile);
            resources.ApplyResources(groupBox1, "groupBox1");
            groupBox1.Name = "groupBox1";
            groupBox1.TabStop = false;
            // 
            // groupBox_wzSaveSelection
            // 
            groupBox_wzSaveSelection.Controls.Add(label2);
            groupBox_wzSaveSelection.Controls.Add(checkBox_64BitFile);
            groupBox_wzSaveSelection.Controls.Add(label1);
            groupBox_wzSaveSelection.Controls.Add(versionBox);
            groupBox_wzSaveSelection.Controls.Add(encryptionBox);
            resources.ApplyResources(groupBox_wzSaveSelection, "groupBox_wzSaveSelection");
            groupBox_wzSaveSelection.Name = "groupBox_wzSaveSelection";
            groupBox_wzSaveSelection.TabStop = false;
            // 
            // radioButton1
            // 
            resources.ApplyResources(radioButton1, "radioButton1");
            radioButton1.Name = "radioButton1";
            radioButton1.UseVisualStyleBackColor = true;
            radioButton1.CheckedChanged += FileFormat_CheckedChanged;
            // 
            // radioButton_wzFile
            // 
            resources.ApplyResources(radioButton_wzFile, "radioButton_wzFile");
            radioButton_wzFile.Checked = true;
            radioButton_wzFile.Name = "radioButton_wzFile";
            radioButton_wzFile.TabStop = true;
            radioButton_wzFile.UseVisualStyleBackColor = true;
            radioButton_wzFile.CheckedChanged += FileFormat_CheckedChanged;
            // 
            // SaveForm
            // 
            resources.ApplyResources(this, "$this");
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            Controls.Add(groupBox1);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            Name = "SaveForm";
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            groupBox_wzSaveSelection.ResumeLayout(false);
            groupBox_wzSaveSelection.PerformLayout();
            ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button saveButton;
        public System.Windows.Forms.ComboBox encryptionBox;
        private IntegerInput versionBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.CheckBox checkBox_64BitFile;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.RadioButton radioButton1;
        private System.Windows.Forms.RadioButton radioButton_wzFile;
        private System.Windows.Forms.GroupBox groupBox_wzSaveSelection;
    }
}