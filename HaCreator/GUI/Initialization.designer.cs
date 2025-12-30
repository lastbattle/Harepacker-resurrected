namespace HaCreator.GUI
{
    partial class Initialization
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Initialization));
            button_initialise = new System.Windows.Forms.Button();
            versionBox = new System.Windows.Forms.ComboBox();
            toolStripProgressBar1 = new System.Windows.Forms.ToolStripProgressBar();
            label2 = new System.Windows.Forms.Label();
            label3 = new System.Windows.Forms.Label();
            textBox2 = new System.Windows.Forms.TextBox();
            pathBox = new System.Windows.Forms.ComboBox();
            button2 = new System.Windows.Forms.Button();
            button_checkMapErrors = new System.Windows.Forms.Button();
            label1 = new System.Windows.Forms.Label();
            comboBox_localisation = new System.Windows.Forms.ComboBox();
            label4 = new System.Windows.Forms.Label();
            button_initialiseImg = new System.Windows.Forms.Button();
            button_settings = new System.Windows.Forms.Button();
            SuspendLayout();
            // 
            // button_initialise
            // 
            button_initialise.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            button_initialise.Location = new System.Drawing.Point(4, 137);
            button_initialise.Name = "button_initialise";
            button_initialise.Size = new System.Drawing.Size(117, 28);
            button_initialise.TabIndex = 1;
            button_initialise.Text = "Initialize";
            button_initialise.Click += button_initialise_Click;
            // 
            // versionBox
            // 
            versionBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            versionBox.FormattingEnabled = true;
            versionBox.Items.AddRange(new object[] { "GMS", "EMS , MSEA , KMS", "BMS , JMS", "Auto-Detect" });
            versionBox.Location = new System.Drawing.Point(141, 27);
            versionBox.Name = "versionBox";
            versionBox.Size = new System.Drawing.Size(227, 21);
            versionBox.TabIndex = 3;
            // 
            // toolStripProgressBar1
            // 
            toolStripProgressBar1.Name = "toolStripProgressBar1";
            toolStripProgressBar1.Size = new System.Drawing.Size(150, 16);
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new System.Drawing.Point(5, 29);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(85, 13);
            label2.TabIndex = 8;
            label2.Text = "WZ encryption:";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new System.Drawing.Point(5, 4);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(33, 13);
            label3.TabIndex = 9;
            label3.Text = "Path:";
            // 
            // textBox2
            // 
            textBox2.Location = new System.Drawing.Point(4, 171);
            textBox2.Name = "textBox2";
            textBox2.ReadOnly = true;
            textBox2.Size = new System.Drawing.Size(364, 22);
            textBox2.TabIndex = 2;
            // 
            // pathBox
            // 
            pathBox.FormattingEnabled = true;
            pathBox.Location = new System.Drawing.Point(73, 1);
            pathBox.Name = "pathBox";
            pathBox.Size = new System.Drawing.Size(237, 21);
            pathBox.TabIndex = 13;
            // 
            // button2
            // 
            button2.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            button2.Location = new System.Drawing.Point(314, 0);
            button2.Name = "button2";
            button2.Size = new System.Drawing.Size(54, 21);
            button2.TabIndex = 14;
            button2.Text = "...";
            button2.Click += button2_Click;
            // 
            // button_checkMapErrors
            // 
            button_checkMapErrors.Location = new System.Drawing.Point(257, 137);
            button_checkMapErrors.Name = "button_checkMapErrors";
            button_checkMapErrors.Size = new System.Drawing.Size(111, 28);
            button_checkMapErrors.TabIndex = 15;
            button_checkMapErrors.Text = "Check map errors";
            button_checkMapErrors.UseVisualStyleBackColor = true;
            button_checkMapErrors.Click += debugButton_Click;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Enabled = false;
            label1.Location = new System.Drawing.Point(4, 58);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(102, 13);
            label1.TabIndex = 17;
            label1.Text = "Client localisation:";
            // 
            // comboBox_localisation
            // 
            comboBox_localisation.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            comboBox_localisation.Enabled = false;
            comboBox_localisation.FormattingEnabled = true;
            comboBox_localisation.Items.AddRange(new object[] { "GMS", "EMS , MSEA , KMS", "BMS , JMS", "Auto-Detect" });
            comboBox_localisation.Location = new System.Drawing.Point(140, 56);
            comboBox_localisation.Name = "comboBox_localisation";
            comboBox_localisation.Size = new System.Drawing.Size(228, 21);
            comboBox_localisation.TabIndex = 16;
            // 
            // label4
            // 
            label4.Enabled = false;
            label4.Font = new System.Drawing.Font("Segoe UI", 6.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            label4.Location = new System.Drawing.Point(3, 80);
            label4.Name = "label4";
            label4.Size = new System.Drawing.Size(365, 33);
            label4.TabIndex = 18;
            label4.Text = "Please select the right localisation, as the saved .wz data parameters might be different.";
            //
            // button_initialiseImg
            //
            button_initialiseImg.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            button_initialiseImg.Location = new System.Drawing.Point(127, 137);
            button_initialiseImg.Name = "button_initialiseImg";
            button_initialiseImg.Size = new System.Drawing.Size(116, 28);
            button_initialiseImg.TabIndex = 20;
            button_initialiseImg.Text = "Initialize from .img";
            button_initialiseImg.Click += button_initialiseImg_Click;
            //
            // button_settings
            //
            button_settings.Location = new System.Drawing.Point(314, 56);
            button_settings.Name = "button_settings";
            button_settings.Size = new System.Drawing.Size(54, 21);
            button_settings.TabIndex = 21;
            button_settings.Text = "Settings";
            button_settings.Click += button_settings_Click;
            // 
            // Initialization
            // 
            AccessibleRole = System.Windows.Forms.AccessibleRole.ScrollBar;
            AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            ClientSize = new System.Drawing.Size(372, 196);
            Controls.Add(button_settings);
            Controls.Add(button_initialiseImg);
            Controls.Add(label4);
            Controls.Add(label1);
            Controls.Add(comboBox_localisation);
            Controls.Add(button_checkMapErrors);
            Controls.Add(button2);
            Controls.Add(pathBox);
            Controls.Add(label3);
            Controls.Add(label2);
            Controls.Add(versionBox);
            Controls.Add(textBox2);
            Controls.Add(button_initialise);
            Font = new System.Drawing.Font("Segoe UI", 8.25F);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            KeyPreview = true;
            MaximizeBox = false;
            Name = "Initialization";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "Initialisation";
            Load += Initialization_Load;
            KeyDown += Initialization_KeyDown;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Button button_initialise;
        private System.Windows.Forms.ComboBox versionBox;
        private System.Windows.Forms.ToolStripProgressBar toolStripProgressBar1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textBox2;
        private System.Windows.Forms.ComboBox pathBox;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Button button_checkMapErrors;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox comboBox_localisation;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button button_initialiseImg;
        private System.Windows.Forms.Button button_settings;
    }
}

