namespace HaCreator.GUI.InstanceEditor
{
    partial class ObjectInstanceEditor
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
            this.pathLabel = new System.Windows.Forms.Label();
            this.xInput = new System.Windows.Forms.NumericUpDown();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.yInput = new System.Windows.Forms.NumericUpDown();
            this.label3 = new System.Windows.Forms.Label();
            this.zInput = new System.Windows.Forms.NumericUpDown();
            this.okButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.nameBox = new System.Windows.Forms.TextBox();
            this.rBox = new System.Windows.Forms.CheckBox();
            this.flowBox = new System.Windows.Forms.CheckBox();
            this.rxBox = new System.Windows.Forms.CheckBox();
            this.ryBox = new System.Windows.Forms.CheckBox();
            this.cxBox = new System.Windows.Forms.CheckBox();
            this.cyBox = new System.Windows.Forms.CheckBox();
            this.rxInt = new System.Windows.Forms.NumericUpDown();
            this.ryInt = new System.Windows.Forms.NumericUpDown();
            this.cxInt = new System.Windows.Forms.NumericUpDown();
            this.cyInt = new System.Windows.Forms.NumericUpDown();
            this.nameEnable = new System.Windows.Forms.CheckBox();
            this.hideBox = new System.Windows.Forms.CheckBox();
            this.reactorBox = new System.Windows.Forms.CheckBox();
            this.questList = new System.Windows.Forms.ListBox();
            this.questAdd = new System.Windows.Forms.Button();
            this.questRemove = new System.Windows.Forms.Button();
            this.questEnable = new System.Windows.Forms.CheckBox();
            this.tagsEnable = new System.Windows.Forms.CheckBox();
            this.tagsBox = new System.Windows.Forms.TextBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.flipBox = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.xInput)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.yInput)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.zInput)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.rxInt)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.ryInt)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.cxInt)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.cyInt)).BeginInit();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // pathLabel
            // 
            this.pathLabel.Location = new System.Drawing.Point(0, 0);
            this.pathLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.pathLabel.Name = "pathLabel";
            this.pathLabel.Size = new System.Drawing.Size(469, 151);
            this.pathLabel.TabIndex = 0;
            this.pathLabel.Text = "label1";
            this.pathLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // xInput
            // 
            this.xInput.Location = new System.Drawing.Point(33, 155);
            this.xInput.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.xInput.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.xInput.Minimum = new decimal(new int[] {
            -2147483648,
            0,
            0,
            -2147483648});
            this.xInput.Name = "xInput";
            this.xInput.Size = new System.Drawing.Size(88, 29);
            this.xInput.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(9, 159);
            this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(20, 23);
            this.label1.TabIndex = 2;
            this.label1.Text = "X";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(9, 198);
            this.label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(19, 23);
            this.label2.TabIndex = 4;
            this.label2.Text = "Y";
            // 
            // yInput
            // 
            this.yInput.Location = new System.Drawing.Point(33, 193);
            this.yInput.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.yInput.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.yInput.Minimum = new decimal(new int[] {
            -2147483648,
            0,
            0,
            -2147483648});
            this.yInput.Name = "yInput";
            this.yInput.Size = new System.Drawing.Size(88, 29);
            this.yInput.TabIndex = 1;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(9, 237);
            this.label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(20, 23);
            this.label3.TabIndex = 6;
            this.label3.Text = "Z";
            // 
            // zInput
            // 
            this.zInput.Location = new System.Drawing.Point(33, 233);
            this.zInput.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.zInput.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.zInput.Name = "zInput";
            this.zInput.Size = new System.Drawing.Size(88, 29);
            this.zInput.TabIndex = 2;
            // 
            // okButton
            // 
            this.okButton.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.okButton.Location = new System.Drawing.Point(3, 579);
            this.okButton.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.okButton.Name = "okButton";
            this.okButton.Size = new System.Drawing.Size(225, 62);
            this.okButton.TabIndex = 23;
            this.okButton.Text = "OK";
            this.okButton.Click += new System.EventHandler(this.okButton_Click);
            // 
            // cancelButton
            // 
            this.cancelButton.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.cancelButton.Location = new System.Drawing.Point(244, 579);
            this.cancelButton.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(225, 62);
            this.cancelButton.TabIndex = 24;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.Click += new System.EventHandler(this.cancelButton_Click);
            // 
            // nameBox
            // 
            this.nameBox.Enabled = false;
            this.nameBox.Location = new System.Drawing.Point(99, 276);
            this.nameBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.nameBox.Name = "nameBox";
            this.nameBox.Size = new System.Drawing.Size(158, 29);
            this.nameBox.TabIndex = 4;
            // 
            // rBox
            // 
            this.rBox.AutoSize = true;
            this.rBox.Location = new System.Drawing.Point(15, 318);
            this.rBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.rBox.Name = "rBox";
            this.rBox.Size = new System.Drawing.Size(46, 27);
            this.rBox.TabIndex = 5;
            this.rBox.Text = "R";
            // 
            // flowBox
            // 
            this.flowBox.AutoSize = true;
            this.flowBox.Location = new System.Drawing.Point(284, 159);
            this.flowBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.flowBox.Name = "flowBox";
            this.flowBox.Size = new System.Drawing.Size(70, 27);
            this.flowBox.TabIndex = 8;
            this.flowBox.Text = "Flow";
            // 
            // rxBox
            // 
            this.rxBox.AutoSize = true;
            this.rxBox.Location = new System.Drawing.Point(284, 191);
            this.rxBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.rxBox.Name = "rxBox";
            this.rxBox.Size = new System.Drawing.Size(56, 27);
            this.rxBox.TabIndex = 9;
            this.rxBox.Text = "RX";
            this.rxBox.CheckedChanged += new System.EventHandler(this.enablingCheckBox_CheckChanged);
            // 
            // ryBox
            // 
            this.ryBox.AutoSize = true;
            this.ryBox.Location = new System.Drawing.Point(284, 229);
            this.ryBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.ryBox.Name = "ryBox";
            this.ryBox.Size = new System.Drawing.Size(55, 27);
            this.ryBox.TabIndex = 11;
            this.ryBox.Text = "RY";
            this.ryBox.CheckedChanged += new System.EventHandler(this.enablingCheckBox_CheckChanged);
            // 
            // cxBox
            // 
            this.cxBox.AutoSize = true;
            this.cxBox.Location = new System.Drawing.Point(284, 269);
            this.cxBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.cxBox.Name = "cxBox";
            this.cxBox.Size = new System.Drawing.Size(57, 27);
            this.cxBox.TabIndex = 13;
            this.cxBox.Text = "CX";
            this.cxBox.CheckedChanged += new System.EventHandler(this.enablingCheckBox_CheckChanged);
            // 
            // cyBox
            // 
            this.cyBox.AutoSize = true;
            this.cyBox.Location = new System.Drawing.Point(284, 307);
            this.cyBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.cyBox.Name = "cyBox";
            this.cyBox.Size = new System.Drawing.Size(56, 27);
            this.cyBox.TabIndex = 15;
            this.cyBox.Text = "CY";
            this.cyBox.CheckedChanged += new System.EventHandler(this.enablingCheckBox_CheckChanged);
            // 
            // rxInt
            // 
            this.rxInt.Enabled = false;
            this.rxInt.Location = new System.Drawing.Point(350, 187);
            this.rxInt.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.rxInt.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.rxInt.Minimum = new decimal(new int[] {
            -2147483648,
            0,
            0,
            -2147483648});
            this.rxInt.Name = "rxInt";
            this.rxInt.Size = new System.Drawing.Size(93, 29);
            this.rxInt.TabIndex = 10;
            // 
            // ryInt
            // 
            this.ryInt.Enabled = false;
            this.ryInt.Location = new System.Drawing.Point(350, 227);
            this.ryInt.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.ryInt.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.ryInt.Minimum = new decimal(new int[] {
            -2147483648,
            0,
            0,
            -2147483648});
            this.ryInt.Name = "ryInt";
            this.ryInt.Size = new System.Drawing.Size(93, 29);
            this.ryInt.TabIndex = 12;
            // 
            // cxInt
            // 
            this.cxInt.Enabled = false;
            this.cxInt.Location = new System.Drawing.Point(350, 265);
            this.cxInt.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.cxInt.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.cxInt.Minimum = new decimal(new int[] {
            -2147483648,
            0,
            0,
            -2147483648});
            this.cxInt.Name = "cxInt";
            this.cxInt.Size = new System.Drawing.Size(93, 29);
            this.cxInt.TabIndex = 14;
            // 
            // cyInt
            // 
            this.cyInt.Enabled = false;
            this.cyInt.Location = new System.Drawing.Point(350, 305);
            this.cyInt.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.cyInt.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.cyInt.Minimum = new decimal(new int[] {
            -2147483648,
            0,
            0,
            -2147483648});
            this.cyInt.Name = "cyInt";
            this.cyInt.Size = new System.Drawing.Size(93, 29);
            this.cyInt.TabIndex = 16;
            // 
            // nameEnable
            // 
            this.nameEnable.AutoSize = true;
            this.nameEnable.Location = new System.Drawing.Point(14, 279);
            this.nameEnable.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.nameEnable.Name = "nameEnable";
            this.nameEnable.Size = new System.Drawing.Size(82, 27);
            this.nameEnable.TabIndex = 3;
            this.nameEnable.Text = "Name";
            this.nameEnable.CheckedChanged += new System.EventHandler(this.enablingCheckBox_CheckChanged);
            // 
            // hideBox
            // 
            this.hideBox.AutoSize = true;
            this.hideBox.Location = new System.Drawing.Point(75, 318);
            this.hideBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.hideBox.Name = "hideBox";
            this.hideBox.Size = new System.Drawing.Size(71, 27);
            this.hideBox.TabIndex = 6;
            this.hideBox.Text = "Hide";
            // 
            // reactorBox
            // 
            this.reactorBox.AutoSize = true;
            this.reactorBox.Location = new System.Drawing.Point(156, 318);
            this.reactorBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.reactorBox.Name = "reactorBox";
            this.reactorBox.Size = new System.Drawing.Size(94, 27);
            this.reactorBox.TabIndex = 7;
            this.reactorBox.Text = "Reactor";
            // 
            // questList
            // 
            this.questList.Enabled = false;
            this.questList.FormattingEnabled = true;
            this.questList.ItemHeight = 23;
            this.questList.Location = new System.Drawing.Point(99, 14);
            this.questList.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.questList.Name = "questList";
            this.questList.Size = new System.Drawing.Size(360, 96);
            this.questList.TabIndex = 22;
            // 
            // questAdd
            // 
            this.questAdd.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.questAdd.Enabled = false;
            this.questAdd.Location = new System.Drawing.Point(8, 48);
            this.questAdd.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.questAdd.Name = "questAdd";
            this.questAdd.Size = new System.Drawing.Size(82, 34);
            this.questAdd.TabIndex = 20;
            this.questAdd.Text = "Add";
            this.questAdd.Click += new System.EventHandler(this.questAdd_Click);
            // 
            // questRemove
            // 
            this.questRemove.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.questRemove.Enabled = false;
            this.questRemove.Location = new System.Drawing.Point(8, 82);
            this.questRemove.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.questRemove.Name = "questRemove";
            this.questRemove.Size = new System.Drawing.Size(82, 34);
            this.questRemove.TabIndex = 21;
            this.questRemove.Text = "Remove";
            this.questRemove.Click += new System.EventHandler(this.questRemove_Click);
            // 
            // questEnable
            // 
            this.questEnable.AutoSize = true;
            this.questEnable.Location = new System.Drawing.Point(9, 14);
            this.questEnable.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.questEnable.Name = "questEnable";
            this.questEnable.Size = new System.Drawing.Size(81, 27);
            this.questEnable.TabIndex = 19;
            this.questEnable.Text = "Quest";
            this.questEnable.CheckedChanged += new System.EventHandler(this.enablingCheckBox_CheckChanged);
            // 
            // tagsEnable
            // 
            this.tagsEnable.AutoSize = true;
            this.tagsEnable.Location = new System.Drawing.Point(14, 414);
            this.tagsEnable.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.tagsEnable.Name = "tagsEnable";
            this.tagsEnable.Size = new System.Drawing.Size(69, 27);
            this.tagsEnable.TabIndex = 17;
            this.tagsEnable.Text = "Tags";
            this.tagsEnable.CheckedChanged += new System.EventHandler(this.enablingCheckBox_CheckChanged);
            // 
            // tagsBox
            // 
            this.tagsBox.Enabled = false;
            this.tagsBox.Location = new System.Drawing.Point(98, 411);
            this.tagsBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.tagsBox.Name = "tagsBox";
            this.tagsBox.Size = new System.Drawing.Size(160, 29);
            this.tagsBox.TabIndex = 18;
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.questEnable);
            this.panel1.Controls.Add(this.questAdd);
            this.panel1.Controls.Add(this.questRemove);
            this.panel1.Controls.Add(this.questList);
            this.panel1.Location = new System.Drawing.Point(4, 447);
            this.panel1.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(465, 123);
            this.panel1.TabIndex = 25;
            // 
            // flipBox
            // 
            this.flipBox.AutoSize = true;
            this.flipBox.Location = new System.Drawing.Point(14, 360);
            this.flipBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.flipBox.Name = "flipBox";
            this.flipBox.Size = new System.Drawing.Size(62, 27);
            this.flipBox.TabIndex = 26;
            this.flipBox.Text = "Flip";
            // 
            // ObjectInstanceEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(144F, 144F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(472, 644);
            this.Controls.Add(this.flipBox);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.tagsEnable);
            this.Controls.Add(this.tagsBox);
            this.Controls.Add(this.reactorBox);
            this.Controls.Add(this.hideBox);
            this.Controls.Add(this.nameEnable);
            this.Controls.Add(this.cyInt);
            this.Controls.Add(this.cxInt);
            this.Controls.Add(this.ryInt);
            this.Controls.Add(this.rxInt);
            this.Controls.Add(this.cyBox);
            this.Controls.Add(this.cxBox);
            this.Controls.Add(this.ryBox);
            this.Controls.Add(this.rxBox);
            this.Controls.Add(this.flowBox);
            this.Controls.Add(this.rBox);
            this.Controls.Add(this.nameBox);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.okButton);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.zInput);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.yInput);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.xInput);
            this.Controls.Add(this.pathLabel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ObjectInstanceEditor";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Object";
            ((System.ComponentModel.ISupportInitialize)(this.xInput)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.yInput)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.zInput)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.rxInt)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.ryInt)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.cxInt)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.cyInt)).EndInit();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label pathLabel;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.NumericUpDown xInput;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.NumericUpDown yInput;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.NumericUpDown zInput;
        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.TextBox nameBox;
        private System.Windows.Forms.CheckBox rBox;
        private System.Windows.Forms.CheckBox rxBox;
        private System.Windows.Forms.CheckBox flowBox;
        private System.Windows.Forms.CheckBox ryBox;
        private System.Windows.Forms.CheckBox cxBox;
        private System.Windows.Forms.CheckBox cyBox;
        private System.Windows.Forms.NumericUpDown rxInt;
        private System.Windows.Forms.NumericUpDown ryInt;
        private System.Windows.Forms.NumericUpDown cxInt;
        private System.Windows.Forms.NumericUpDown cyInt;
        private System.Windows.Forms.CheckBox nameEnable;
        private System.Windows.Forms.CheckBox hideBox;
        private System.Windows.Forms.CheckBox reactorBox;
        private System.Windows.Forms.ListBox questList;
        private System.Windows.Forms.Button questAdd;
        private System.Windows.Forms.Button questRemove;
        private System.Windows.Forms.CheckBox questEnable;
        private System.Windows.Forms.CheckBox tagsEnable;
        private System.Windows.Forms.TextBox tagsBox;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.CheckBox flipBox;
    }
}