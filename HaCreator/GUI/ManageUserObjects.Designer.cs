namespace HaCreator.GUI
{
    partial class ManageUserObjects
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
            this.objsList = new System.Windows.Forms.ListBox();
            this.removeBtn = new System.Windows.Forms.Button();
            this.searchBtn = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // objsList
            // 
            this.objsList.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
            this.objsList.FormattingEnabled = true;
            this.objsList.Location = new System.Drawing.Point(12, 12);
            this.objsList.Name = "objsList";
            this.objsList.Size = new System.Drawing.Size(260, 238);
            this.objsList.TabIndex = 0;
            this.objsList.DrawItem += new System.Windows.Forms.DrawItemEventHandler(this.objsList_DrawItem);
            this.objsList.SelectedIndexChanged += new System.EventHandler(this.objsList_SelectedIndexChanged);
            // 
            // removeBtn
            // 
            this.removeBtn.Enabled = false;
            this.removeBtn.Location = new System.Drawing.Point(278, 12);
            this.removeBtn.Name = "removeBtn";
            this.removeBtn.Size = new System.Drawing.Size(156, 37);
            this.removeBtn.TabIndex = 1;
            this.removeBtn.Text = "Remove Object";
            this.removeBtn.UseVisualStyleBackColor = true;
            this.removeBtn.Click += new System.EventHandler(this.removeBtn_Click);
            // 
            // searchBtn
            // 
            this.searchBtn.Enabled = false;
            this.searchBtn.Location = new System.Drawing.Point(278, 55);
            this.searchBtn.Name = "searchBtn";
            this.searchBtn.Size = new System.Drawing.Size(156, 37);
            this.searchBtn.TabIndex = 2;
            this.searchBtn.Text = "Search all usages";
            this.searchBtn.UseVisualStyleBackColor = true;
            this.searchBtn.Click += new System.EventHandler(this.searchBtn_Click);
            // 
            // label1
            // 
            this.label1.Location = new System.Drawing.Point(28, 261);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(406, 37);
            this.label1.TabIndex = 3;
            this.label1.Text = "Do NOT Remove objects that are in use somewhere on some map; If you are not sure," +
    " click the \"Search all usages\" button.";
            // 
            // ManageUserObjects
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(446, 307);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.searchBtn);
            this.Controls.Add(this.removeBtn);
            this.Controls.Add(this.objsList);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.KeyPreview = true;
            this.Name = "ManageUserObjects";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "User Objects";
            this.Load += new System.EventHandler(this.ManageUserObjects_Load);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.ManageUserObjects_KeyDown);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListBox objsList;
        private System.Windows.Forms.Button removeBtn;
        private System.Windows.Forms.Button searchBtn;
        private System.Windows.Forms.Label label1;
    }
}