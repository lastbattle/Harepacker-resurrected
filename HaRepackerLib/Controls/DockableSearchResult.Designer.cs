namespace HaRepackerLib.Controls
{
    partial class DockableSearchResult
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DockableSearchResult));
            this.searchResultsBox = new System.Windows.Forms.ListBox();
            this.SuspendLayout();
            // 
            // searchResultsBox
            // 
            resources.ApplyResources(this.searchResultsBox, "searchResultsBox");
            this.searchResultsBox.FormattingEnabled = true;
            this.searchResultsBox.Name = "searchResultsBox";
            this.searchResultsBox.SelectedIndexChanged += new System.EventHandler(this.searchResultsBox_SelectedIndexChanged);
            // 
            // DockableSearchResult
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.searchResultsBox);
            this.Name = "DockableSearchResult";
            this.ResumeLayout(false);

        }

        #endregion

        public System.Windows.Forms.ListBox searchResultsBox;


    }
}
