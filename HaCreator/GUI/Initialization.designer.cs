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
            this.button_initialise = new System.Windows.Forms.Button();
            this.versionBox = new System.Windows.Forms.ComboBox();
            this.toolStripProgressBar1 = new System.Windows.Forms.ToolStripProgressBar();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.textBox2 = new System.Windows.Forms.TextBox();
            this.pathBox = new System.Windows.Forms.ComboBox();
            this.button2 = new System.Windows.Forms.Button();
            this.button_checkMapErrors = new System.Windows.Forms.Button();
            this.ClientTypeBox = new System.Windows.Forms.ComboBox();
            this.SuspendLayout();
            // 
            // button_initialise
            // 
            this.button_initialise.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.button_initialise.Location = new System.Drawing.Point(5, 65);
            this.button_initialise.Name = "button_initialise";
            this.button_initialise.Size = new System.Drawing.Size(186, 28);
            this.button_initialise.TabIndex = 1;
            this.button_initialise.Text = "Initialize";
            this.button_initialise.Click += new System.EventHandler(this.button_initialise_Click);
            // 
            // versionBox
            // 
            this.versionBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.versionBox.FormattingEnabled = true;
            this.versionBox.Items.AddRange(new object[] {
            "GMS",
            "EMS , MSEA , KMS",
            "BMS , JMS",
            "Auto-Detect"});
            this.versionBox.Location = new System.Drawing.Point(74, 38);
            this.versionBox.Name = "versionBox";
            this.versionBox.Size = new System.Drawing.Size(237, 21);
            this.versionBox.TabIndex = 3;
            // 
            // toolStripProgressBar1
            // 
            this.toolStripProgressBar1.Name = "toolStripProgressBar1";
            this.toolStripProgressBar1.Size = new System.Drawing.Size(150, 16);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(6, 40);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(62, 13);
            this.label2.TabIndex = 8;
            this.label2.Text = "Encryption";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(6, 15);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(49, 13);
            this.label3.TabIndex = 9;
            this.label3.Text = "MS Path";
            // 
            // textBox2
            // 
            this.textBox2.Location = new System.Drawing.Point(5, 99);
            this.textBox2.Name = "textBox2";
            this.textBox2.ReadOnly = true;
            this.textBox2.Size = new System.Drawing.Size(354, 22);
            this.textBox2.TabIndex = 2;
            // 
            // pathBox
            // 
            this.pathBox.FormattingEnabled = true;
            this.pathBox.Location = new System.Drawing.Point(74, 12);
            this.pathBox.Name = "pathBox";
            this.pathBox.Size = new System.Drawing.Size(237, 21);
            this.pathBox.TabIndex = 13;
            // 
            // button2
            // 
            this.button2.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.button2.Location = new System.Drawing.Point(315, 11);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(54, 21);
            this.button2.TabIndex = 14;
            this.button2.Text = "...";
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // button_checkMapErrors
            // 
            this.button_checkMapErrors.Location = new System.Drawing.Point(197, 65);
            this.button_checkMapErrors.Name = "button_checkMapErrors";
            this.button_checkMapErrors.Size = new System.Drawing.Size(172, 28);
            this.button_checkMapErrors.TabIndex = 15;
            this.button_checkMapErrors.Text = "Check map errors";
            this.button_checkMapErrors.UseVisualStyleBackColor = true;
            this.button_checkMapErrors.Click += new System.EventHandler(this.debugButton_Click);
            // 
            // ClientTypeBox
            // 
            this.ClientTypeBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ClientTypeBox.FormattingEnabled = true;
            this.ClientTypeBox.Items.AddRange(new object[] {
            "32 bit",
            "64 bit"});
            this.ClientTypeBox.Location = new System.Drawing.Point(315, 38);
            this.ClientTypeBox.Name = "ClientTypeBox";
            this.ClientTypeBox.Size = new System.Drawing.Size(54, 21);
            this.ClientTypeBox.TabIndex = 16;
            // 
            // Initialization
            // 
            this.AccessibleRole = System.Windows.Forms.AccessibleRole.ScrollBar;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(372, 133);
            this.Controls.Add(this.ClientTypeBox);
            this.Controls.Add(this.button_checkMapErrors);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.pathBox);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.versionBox);
            this.Controls.Add(this.textBox2);
            this.Controls.Add(this.button_initialise);
            this.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.Name = "Initialization";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "HaCreator";
            this.Load += new System.EventHandler(this.Initialization_Load);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.Initialization_KeyDown);
            this.ResumeLayout(false);
            this.PerformLayout();

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
        private System.Windows.Forms.ComboBox ClientTypeBox;
    }
}

