namespace HaRepacker.GUI.Panels.SubPanels
{
    partial class FieldLimitPanel
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FieldLimitPanel));
            this.listView_fieldLimitType = new System.Windows.Forms.ListView();
            this.SuspendLayout();
            // 
            // listView_fieldLimitType
            // 
            resources.ApplyResources(this.listView_fieldLimitType, "listView_fieldLimitType");
            this.listView_fieldLimitType.CheckBoxes = true;
            this.listView_fieldLimitType.FullRowSelect = true;
            this.listView_fieldLimitType.GridLines = true;
            this.listView_fieldLimitType.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
            this.listView_fieldLimitType.Name = "listView_fieldLimitType";
            this.listView_fieldLimitType.ShowGroups = false;
            this.listView_fieldLimitType.ShowItemToolTips = true;
            this.listView_fieldLimitType.UseCompatibleStateImageBehavior = false;
            this.listView_fieldLimitType.View = System.Windows.Forms.View.Details;
            this.listView_fieldLimitType.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.ListView_fieldLimitType_ItemCheck);
            this.listView_fieldLimitType.ItemChecked += new System.Windows.Forms.ItemCheckedEventHandler(this.ListView_fieldLimitType_ItemChecked);
            // 
            // FieldLimitPanel
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.listView_fieldLimitType);
            this.Name = "FieldLimitPanel";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListView listView_fieldLimitType;
    }
}
