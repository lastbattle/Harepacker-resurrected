namespace HaRepacker.GUI.Interaction
{
    partial class LoadBox
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LoadBox));
            this.pgb_loading = new System.Windows.Forms.ProgressBar();
            this.btn_accept = new System.Windows.Forms.Button();
            this.lb_process = new System.Windows.Forms.Label();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.lb_title = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // pgb_loading
            // 
            this.pgb_loading.Location = new System.Drawing.Point(12, 147);
            this.pgb_loading.Name = "pgb_loading";
            this.pgb_loading.Size = new System.Drawing.Size(473, 20);
            this.pgb_loading.Step = 1;
            this.pgb_loading.TabIndex = 0;
            // 
            // btn_accept
            // 
            this.btn_accept.BackColor = System.Drawing.SystemColors.ControlDarkDark;
            this.btn_accept.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btn_accept.Enabled = false;
            this.btn_accept.FlatAppearance.BorderSize = 0;
            this.btn_accept.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_accept.ForeColor = System.Drawing.SystemColors.ControlLight;
            this.btn_accept.Location = new System.Drawing.Point(190, 121);
            this.btn_accept.Name = "btn_accept";
            this.btn_accept.Size = new System.Drawing.Size(120, 22);
            this.btn_accept.TabIndex = 1;
            this.btn_accept.Text = "Loading...";
            this.btn_accept.UseVisualStyleBackColor = false;
            this.btn_accept.Click += new System.EventHandler(this.btn_accept_Click);
            // 
            // lb_process
            // 
            this.lb_process.AutoSize = true;
            this.lb_process.Location = new System.Drawing.Point(9, 170);
            this.lb_process.Name = "lb_process";
            this.lb_process.Size = new System.Drawing.Size(53, 13);
            this.lb_process.TabIndex = 2;
            this.lb_process.Text = "Prosses...";
            this.lb_process.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // pictureBox1
            // 
            this.pictureBox1.Image = global::HaRepacker.Properties.Resources.orangeMushroom_Jump;
            this.pictureBox1.Location = new System.Drawing.Point(218, 51);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(64, 70);
            this.pictureBox1.TabIndex = 3;
            this.pictureBox1.TabStop = false;
            // 
            // lb_title
            // 
            this.lb_title.Font = new System.Drawing.Font("Montserrat Medium", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lb_title.Location = new System.Drawing.Point(12, 20);
            this.lb_title.Name = "lb_title";
            this.lb_title.Size = new System.Drawing.Size(476, 28);
            this.lb_title.TabIndex = 4;
            this.lb_title.Text = "Saving .wz";
            this.lb_title.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // LoadBox
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(500, 190);
            this.Controls.Add(this.lb_title);
            this.Controls.Add(this.pictureBox1);
            this.Controls.Add(this.lb_process);
            this.Controls.Add(this.btn_accept);
            this.Controls.Add(this.pgb_loading);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "LoadBox";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Loading...";
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ProgressBar pgb_loading;
        private System.Windows.Forms.Button btn_accept;
        private System.Windows.Forms.Label lb_process;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.Label lb_title;
    }
}