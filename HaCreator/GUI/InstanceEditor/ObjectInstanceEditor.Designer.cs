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
            ((System.ComponentModel.ISupportInitialize)(this.xInput)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.yInput)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.zInput)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.rxInt)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.ryInt)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.cxInt)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.cyInt)).BeginInit();
            this.SuspendLayout();
            // 
            // pathLabel
            // 
            this.pathLabel.Location = new System.Drawing.Point(0, 9);
            this.pathLabel.Name = "pathLabel";
            this.pathLabel.Size = new System.Drawing.Size(303, 37);
            this.pathLabel.TabIndex = 0;
            this.pathLabel.Text = "label1";
            this.pathLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // xInput
            // 
            this.xInput.Location = new System.Drawing.Point(79, 52);
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
            this.xInput.Size = new System.Drawing.Size(59, 20);
            this.xInput.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(63, 55);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(14, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "X";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(63, 81);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(14, 13);
            this.label2.TabIndex = 4;
            this.label2.Text = "Y";
            // 
            // yInput
            // 
            this.yInput.Location = new System.Drawing.Point(79, 78);
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
            this.yInput.Size = new System.Drawing.Size(59, 20);
            this.yInput.TabIndex = 1;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(63, 107);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(14, 13);
            this.label3.TabIndex = 6;
            this.label3.Text = "Z";
            // 
            // zInput
            // 
            this.zInput.Location = new System.Drawing.Point(79, 104);
            this.zInput.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.zInput.Name = "zInput";
            this.zInput.Size = new System.Drawing.Size(59, 20);
            this.zInput.TabIndex = 2;
            // 
            // okButton
            // 
            this.okButton.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.okButton.Location = new System.Drawing.Point(83, 291);
            this.okButton.Name = "okButton";
            this.okButton.Size = new System.Drawing.Size(76, 28);
            this.okButton.TabIndex = 23;
            this.okButton.Text = "OK";
            this.okButton.Click += new System.EventHandler(this.okButton_Click);
            // 
            // cancelButton
            // 
            this.cancelButton.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.cancelButton.Location = new System.Drawing.Point(165, 291);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(73, 28);
            this.cancelButton.TabIndex = 24;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.Click += new System.EventHandler(this.cancelButton_Click);
            // 
            // nameBox
            // 
            this.nameBox.Enabled = false;
            this.nameBox.Location = new System.Drawing.Point(66, 130);
            this.nameBox.Name = "nameBox";
            this.nameBox.Size = new System.Drawing.Size(93, 20);
            this.nameBox.TabIndex = 4;
            // 
            // rBox
            // 
            this.rBox.AutoSize = true;
            this.rBox.Location = new System.Drawing.Point(15, 156);
            this.rBox.Name = "rBox";
            this.rBox.Size = new System.Drawing.Size(34, 17);
            this.rBox.TabIndex = 5;
            this.rBox.Text = "R";
            // 
            // flowBox
            // 
            this.flowBox.AutoSize = true;
            this.flowBox.Location = new System.Drawing.Point(176, 52);
            this.flowBox.Name = "flowBox";
            this.flowBox.Size = new System.Drawing.Size(48, 17);
            this.flowBox.TabIndex = 8;
            this.flowBox.Text = "Flow";
            // 
            // rxBox
            // 
            this.rxBox.AutoSize = true;
            this.rxBox.Location = new System.Drawing.Point(176, 73);
            this.rxBox.Name = "rxBox";
            this.rxBox.Size = new System.Drawing.Size(41, 17);
            this.rxBox.TabIndex = 9;
            this.rxBox.Text = "RX";
            this.rxBox.CheckedChanged += new System.EventHandler(this.enablingCheckBox_CheckChanged);
            // 
            // ryBox
            // 
            this.ryBox.AutoSize = true;
            this.ryBox.Location = new System.Drawing.Point(176, 99);
            this.ryBox.Name = "ryBox";
            this.ryBox.Size = new System.Drawing.Size(41, 17);
            this.ryBox.TabIndex = 11;
            this.ryBox.Text = "RY";
            this.ryBox.CheckedChanged += new System.EventHandler(this.enablingCheckBox_CheckChanged);
            // 
            // cxBox
            // 
            this.cxBox.AutoSize = true;
            this.cxBox.Location = new System.Drawing.Point(176, 125);
            this.cxBox.Name = "cxBox";
            this.cxBox.Size = new System.Drawing.Size(40, 17);
            this.cxBox.TabIndex = 13;
            this.cxBox.Text = "CX";
            this.cxBox.CheckedChanged += new System.EventHandler(this.enablingCheckBox_CheckChanged);
            // 
            // cyBox
            // 
            this.cyBox.AutoSize = true;
            this.cyBox.Location = new System.Drawing.Point(176, 151);
            this.cyBox.Name = "cyBox";
            this.cyBox.Size = new System.Drawing.Size(40, 17);
            this.cyBox.TabIndex = 15;
            this.cyBox.Text = "CY";
            this.cyBox.CheckedChanged += new System.EventHandler(this.enablingCheckBox_CheckChanged);
            // 
            // rxInt
            // 
            this.rxInt.Enabled = false;
            this.rxInt.Location = new System.Drawing.Point(220, 71);
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
            this.rxInt.Size = new System.Drawing.Size(62, 20);
            this.rxInt.TabIndex = 10;
            // 
            // ryInt
            // 
            this.ryInt.Enabled = false;
            this.ryInt.Location = new System.Drawing.Point(220, 97);
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
            this.ryInt.Size = new System.Drawing.Size(62, 20);
            this.ryInt.TabIndex = 12;
            // 
            // cxInt
            // 
            this.cxInt.Enabled = false;
            this.cxInt.Location = new System.Drawing.Point(220, 123);
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
            this.cxInt.Size = new System.Drawing.Size(62, 20);
            this.cxInt.TabIndex = 14;
            // 
            // cyInt
            // 
            this.cyInt.Enabled = false;
            this.cyInt.Location = new System.Drawing.Point(220, 149);
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
            this.cyInt.Size = new System.Drawing.Size(62, 20);
            this.cyInt.TabIndex = 16;
            // 
            // nameEnable
            // 
            this.nameEnable.AutoSize = true;
            this.nameEnable.Location = new System.Drawing.Point(8, 132);
            this.nameEnable.Name = "nameEnable";
            this.nameEnable.Size = new System.Drawing.Size(54, 17);
            this.nameEnable.TabIndex = 3;
            this.nameEnable.Text = "Name";
            this.nameEnable.CheckedChanged += new System.EventHandler(this.enablingCheckBox_CheckChanged);
            // 
            // hideBox
            // 
            this.hideBox.AutoSize = true;
            this.hideBox.Location = new System.Drawing.Point(51, 156);
            this.hideBox.Name = "hideBox";
            this.hideBox.Size = new System.Drawing.Size(48, 17);
            this.hideBox.TabIndex = 6;
            this.hideBox.Text = "Hide";
            // 
            // reactorBox
            // 
            this.reactorBox.AutoSize = true;
            this.reactorBox.Location = new System.Drawing.Point(102, 156);
            this.reactorBox.Name = "reactorBox";
            this.reactorBox.Size = new System.Drawing.Size(64, 17);
            this.reactorBox.TabIndex = 7;
            this.reactorBox.Text = "Reactor";
            // 
            // questList
            // 
            this.questList.Enabled = false;
            this.questList.FormattingEnabled = true;
            this.questList.Location = new System.Drawing.Point(128, 203);
            this.questList.Name = "questList";
            this.questList.Size = new System.Drawing.Size(123, 82);
            this.questList.TabIndex = 22;
            // 
            // questAdd
            // 
            this.questAdd.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.questAdd.Enabled = false;
            this.questAdd.Location = new System.Drawing.Point(67, 224);
            this.questAdd.Name = "questAdd";
            this.questAdd.Size = new System.Drawing.Size(55, 23);
            this.questAdd.TabIndex = 20;
            this.questAdd.Text = "Add";
            this.questAdd.Click += new System.EventHandler(this.questAdd_Click);
            // 
            // questRemove
            // 
            this.questRemove.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.questRemove.Enabled = false;
            this.questRemove.Location = new System.Drawing.Point(67, 253);
            this.questRemove.Name = "questRemove";
            this.questRemove.Size = new System.Drawing.Size(55, 23);
            this.questRemove.TabIndex = 21;
            this.questRemove.Text = "Remove";
            this.questRemove.Click += new System.EventHandler(this.questRemove_Click);
            // 
            // questEnable
            // 
            this.questEnable.AutoSize = true;
            this.questEnable.Location = new System.Drawing.Point(67, 203);
            this.questEnable.Name = "questEnable";
            this.questEnable.Size = new System.Drawing.Size(54, 17);
            this.questEnable.TabIndex = 19;
            this.questEnable.Text = "Quest";
            this.questEnable.CheckedChanged += new System.EventHandler(this.enablingCheckBox_CheckChanged);
            // 
            // tagsEnable
            // 
            this.tagsEnable.AutoSize = true;
            this.tagsEnable.Location = new System.Drawing.Point(67, 179);
            this.tagsEnable.Name = "tagsEnable";
            this.tagsEnable.Size = new System.Drawing.Size(50, 17);
            this.tagsEnable.TabIndex = 17;
            this.tagsEnable.Text = "Tags";
            this.tagsEnable.CheckedChanged += new System.EventHandler(this.enablingCheckBox_CheckChanged);
            // 
            // tagsBox
            // 
            this.tagsBox.Enabled = false;
            this.tagsBox.Location = new System.Drawing.Point(128, 177);
            this.tagsBox.Name = "tagsBox";
            this.tagsBox.Size = new System.Drawing.Size(93, 20);
            this.tagsBox.TabIndex = 18;
            // 
            // ObjectInstanceEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(315, 333);
            this.Controls.Add(this.tagsEnable);
            this.Controls.Add(this.tagsBox);
            this.Controls.Add(this.questEnable);
            this.Controls.Add(this.questRemove);
            this.Controls.Add(this.questAdd);
            this.Controls.Add(this.questList);
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
    }
}