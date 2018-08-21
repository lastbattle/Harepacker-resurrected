namespace HaRepacker.GUI
{
    partial class SearchSelectionForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SearchSelectionForm));
            this.listBox_items = new System.Windows.Forms.ListBox();
            this.SuspendLayout();
            // 
            // listBox_items
            // 
            resources.ApplyResources(this.listBox_items, "listBox_items");
            this.listBox_items.FormattingEnabled = true;
            this.listBox_items.Name = "listBox_items";
            this.listBox_items.SelectedIndexChanged += new System.EventHandler(this.listBox_items_SelectedIndexChanged);
            // 
            // SearchSelectionForm
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.listBox_items);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "SearchSelectionForm";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListBox listBox_items;
    }
}