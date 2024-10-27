namespace HaCreator.GUI.Input
{
    partial class NameValueInput
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
            textBox_input = new System.Windows.Forms.TextBox();
            label_name = new System.Windows.Forms.Label();
            label_value = new System.Windows.Forms.Label();
            numericUpDown_input = new System.Windows.Forms.NumericUpDown();
            button_ok = new System.Windows.Forms.Button();
            button_cancel = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)numericUpDown_input).BeginInit();
            SuspendLayout();
            // 
            // textBox_input
            // 
            textBox_input.Location = new System.Drawing.Point(101, 17);
            textBox_input.Name = "textBox_input";
            textBox_input.Size = new System.Drawing.Size(330, 23);
            textBox_input.TabIndex = 0;
            // 
            // label_name
            // 
            label_name.AutoSize = true;
            label_name.Location = new System.Drawing.Point(12, 20);
            label_name.Name = "label_name";
            label_name.Size = new System.Drawing.Size(42, 15);
            label_name.TabIndex = 1;
            label_name.Text = "Name:";
            // 
            // label_value
            // 
            label_value.AutoSize = true;
            label_value.Location = new System.Drawing.Point(12, 49);
            label_value.Name = "label_value";
            label_value.Size = new System.Drawing.Size(38, 15);
            label_value.TabIndex = 3;
            label_value.Text = "Value:";
            // 
            // numericUpDown_input
            // 
            numericUpDown_input.Location = new System.Drawing.Point(101, 47);
            numericUpDown_input.Name = "numericUpDown_input";
            numericUpDown_input.Size = new System.Drawing.Size(330, 23);
            numericUpDown_input.TabIndex = 4;
            // 
            // button_ok
            // 
            button_ok.Location = new System.Drawing.Point(3, 76);
            button_ok.Name = "button_ok";
            button_ok.Size = new System.Drawing.Size(216, 76);
            button_ok.TabIndex = 5;
            button_ok.Text = "OK";
            button_ok.UseVisualStyleBackColor = true;
            button_ok.Click += button_ok_Click;
            // 
            // button_cancel
            // 
            button_cancel.Location = new System.Drawing.Point(225, 76);
            button_cancel.Name = "button_cancel";
            button_cancel.Size = new System.Drawing.Size(216, 76);
            button_cancel.TabIndex = 6;
            button_cancel.Text = "Cancel";
            button_cancel.UseVisualStyleBackColor = true;
            button_cancel.Click += button_cancel_Click;
            // 
            // NameValueInputForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(443, 153);
            Controls.Add(button_cancel);
            Controls.Add(button_ok);
            Controls.Add(numericUpDown_input);
            Controls.Add(label_value);
            Controls.Add(label_name);
            Controls.Add(textBox_input);
            Name = "NameValueInputForm";
            Text = "Input";
            KeyDown += Load_KeyDown;
            ((System.ComponentModel.ISupportInitialize)numericUpDown_input).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.TextBox textBox_input;
        private System.Windows.Forms.Label label_name;
        private System.Windows.Forms.Label label_value;
        private System.Windows.Forms.NumericUpDown numericUpDown_input;
        private System.Windows.Forms.Button button_ok;
        private System.Windows.Forms.Button button_cancel;
    }
}