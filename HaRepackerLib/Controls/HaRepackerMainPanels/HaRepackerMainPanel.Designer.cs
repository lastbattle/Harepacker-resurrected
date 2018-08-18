namespace HaRepackerLib.Controls.HaRepackerMainPanels
{
    partial class HaRepackerMainPanel
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(HaRepackerMainPanel));
            this.MainSplitContainer = new System.Windows.Forms.SplitContainer();
            this.DataTree = new TreeViewMS.TreeViewMS();
            this.nextLoopTime_comboBox = new System.Windows.Forms.ComboBox();
            this.nextLoopTime_label = new System.Windows.Forms.Label();
            this.planePosition_comboBox = new System.Windows.Forms.ComboBox();
            this.cartesianPlane_checkBox = new System.Windows.Forms.CheckBox();
            this.button_animateSelectedCanvas = new System.Windows.Forms.Button();
            this.vectorPanel = new HaRepackerLib.XYPanel();
            this.saveSoundButton = new System.Windows.Forms.Button();
            this.saveImageButton = new System.Windows.Forms.Button();
            this.changeSoundButton = new System.Windows.Forms.Button();
            this.mp3Player = new HaRepackerLib.Controls.SoundPlayer();
            this.changeImageButton = new System.Windows.Forms.Button();
            this.applyChangesButton = new System.Windows.Forms.Button();
            this.nameBox = new HaRepackerLib.ChangableTextbox();
            this.textPropBox = new System.Windows.Forms.TextBox();
            this.pictureBoxPanel = new System.Windows.Forms.Panel();
            this.cartesianPlaneX = new System.Windows.Forms.Panel();
            this.cartesianPlaneY = new System.Windows.Forms.Panel();
            this.canvasPropBox = new System.Windows.Forms.PictureBox();
            this.fieldLimitPanel1 = new HaRepackerLib.Controls.FieldLimitPanel();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.selectionLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.mainProgressBar = new System.Windows.Forms.ToolStripProgressBar();
            this.secondaryProgressBar = new System.Windows.Forms.ToolStripProgressBar();
            this.toolStripStatusLabel_additionalInfo = new System.Windows.Forms.ToolStripStatusLabel();
            this.findStrip = new System.Windows.Forms.ToolStrip();
            this.btnFindAll = new System.Windows.Forms.ToolStripButton();
            this.btnFindNext = new System.Windows.Forms.ToolStripButton();
            this.findBox = new System.Windows.Forms.ToolStripTextBox();
            this.btnRestart = new System.Windows.Forms.ToolStripButton();
            this.btnClose = new System.Windows.Forms.ToolStripButton();
            this.btnOptions = new System.Windows.Forms.ToolStripButton();
            this.MainDockPanel = new WeifenLuo.WinFormsUI.Docking.DockPanel();
            this.timerImgSequence = new System.Windows.Forms.Timer(this.components);
            this.panel1 = new System.Windows.Forms.Panel();
            ((System.ComponentModel.ISupportInitialize)(this.MainSplitContainer)).BeginInit();
            this.MainSplitContainer.Panel1.SuspendLayout();
            this.MainSplitContainer.Panel2.SuspendLayout();
            this.MainSplitContainer.SuspendLayout();
            this.pictureBoxPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.canvasPropBox)).BeginInit();
            this.statusStrip.SuspendLayout();
            this.findStrip.SuspendLayout();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // MainSplitContainer
            // 
            resources.ApplyResources(this.MainSplitContainer, "MainSplitContainer");
            this.MainSplitContainer.Name = "MainSplitContainer";
            // 
            // MainSplitContainer.Panel1
            // 
            this.MainSplitContainer.Panel1.Controls.Add(this.DataTree);
            resources.ApplyResources(this.MainSplitContainer.Panel1, "MainSplitContainer.Panel1");
            // 
            // MainSplitContainer.Panel2
            // 
            this.MainSplitContainer.Panel2.Controls.Add(this.nextLoopTime_comboBox);
            this.MainSplitContainer.Panel2.Controls.Add(this.nextLoopTime_label);
            this.MainSplitContainer.Panel2.Controls.Add(this.planePosition_comboBox);
            this.MainSplitContainer.Panel2.Controls.Add(this.cartesianPlane_checkBox);
            this.MainSplitContainer.Panel2.Controls.Add(this.button_animateSelectedCanvas);
            this.MainSplitContainer.Panel2.Controls.Add(this.vectorPanel);
            this.MainSplitContainer.Panel2.Controls.Add(this.saveSoundButton);
            this.MainSplitContainer.Panel2.Controls.Add(this.saveImageButton);
            this.MainSplitContainer.Panel2.Controls.Add(this.changeSoundButton);
            this.MainSplitContainer.Panel2.Controls.Add(this.mp3Player);
            this.MainSplitContainer.Panel2.Controls.Add(this.changeImageButton);
            this.MainSplitContainer.Panel2.Controls.Add(this.applyChangesButton);
            this.MainSplitContainer.Panel2.Controls.Add(this.nameBox);
            this.MainSplitContainer.Panel2.Controls.Add(this.pictureBoxPanel);
            this.MainSplitContainer.Panel2.Controls.Add(this.fieldLimitPanel1);
            this.MainSplitContainer.SplitterMoved += new System.Windows.Forms.SplitterEventHandler(this.MainSplitContainer_SplitterMoved);
            // 
            // DataTree
            // 
            resources.ApplyResources(this.DataTree, "DataTree");
            this.DataTree.Name = "DataTree";
            this.DataTree.SelectedNodes = ((System.Collections.ArrayList)(resources.GetObject("DataTree.SelectedNodes")));
            this.DataTree.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.DataTree_AfterSelect);
            this.DataTree.DoubleClick += new System.EventHandler(this.DataTree_DoubleClick);
            this.DataTree.KeyDown += new System.Windows.Forms.KeyEventHandler(this.DataTree_KeyDown);
            // 
            // nextLoopTime_comboBox
            // 
            this.nextLoopTime_comboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            resources.ApplyResources(this.nextLoopTime_comboBox, "nextLoopTime_comboBox");
            this.nextLoopTime_comboBox.FormattingEnabled = true;
            this.nextLoopTime_comboBox.Items.AddRange(new object[] {
            resources.GetString("nextLoopTime_comboBox.Items"),
            resources.GetString("nextLoopTime_comboBox.Items1"),
            resources.GetString("nextLoopTime_comboBox.Items2"),
            resources.GetString("nextLoopTime_comboBox.Items3"),
            resources.GetString("nextLoopTime_comboBox.Items4")});
            this.nextLoopTime_comboBox.Name = "nextLoopTime_comboBox";
            this.nextLoopTime_comboBox.SelectedIndexChanged += new System.EventHandler(this.nextLoopTime_comboBox_SelectedIndexChanged);
            // 
            // nextLoopTime_label
            // 
            resources.ApplyResources(this.nextLoopTime_label, "nextLoopTime_label");
            this.nextLoopTime_label.Name = "nextLoopTime_label";
            // 
            // planePosition_comboBox
            // 
            this.planePosition_comboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            resources.ApplyResources(this.planePosition_comboBox, "planePosition_comboBox");
            this.planePosition_comboBox.FormattingEnabled = true;
            this.planePosition_comboBox.Items.AddRange(new object[] {
            resources.GetString("planePosition_comboBox.Items"),
            resources.GetString("planePosition_comboBox.Items1"),
            resources.GetString("planePosition_comboBox.Items2"),
            resources.GetString("planePosition_comboBox.Items3"),
            resources.GetString("planePosition_comboBox.Items4"),
            resources.GetString("planePosition_comboBox.Items5"),
            resources.GetString("planePosition_comboBox.Items6"),
            resources.GetString("planePosition_comboBox.Items7"),
            resources.GetString("planePosition_comboBox.Items8")});
            this.planePosition_comboBox.Name = "planePosition_comboBox";
            // 
            // cartesianPlane_checkBox
            // 
            resources.ApplyResources(this.cartesianPlane_checkBox, "cartesianPlane_checkBox");
            this.cartesianPlane_checkBox.Checked = true;
            this.cartesianPlane_checkBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cartesianPlane_checkBox.Name = "cartesianPlane_checkBox";
            this.cartesianPlane_checkBox.UseVisualStyleBackColor = true;
            this.cartesianPlane_checkBox.CheckedChanged += new System.EventHandler(this.cartesianPlane_checkBox_CheckedChanged);
            // 
            // button_animateSelectedCanvas
            // 
            this.button_animateSelectedCanvas.BackColor = System.Drawing.SystemColors.ControlDarkDark;
            this.button_animateSelectedCanvas.FlatAppearance.BorderColor = System.Drawing.SystemColors.Control;
            resources.ApplyResources(this.button_animateSelectedCanvas, "button_animateSelectedCanvas");
            this.button_animateSelectedCanvas.ForeColor = System.Drawing.SystemColors.ControlLight;
            this.button_animateSelectedCanvas.Name = "button_animateSelectedCanvas";
            this.button_animateSelectedCanvas.UseVisualStyleBackColor = false;
            this.button_animateSelectedCanvas.Click += new System.EventHandler(this.button_animateSelectedCanvas_Click);
            // 
            // vectorPanel
            // 
            resources.ApplyResources(this.vectorPanel, "vectorPanel");
            this.vectorPanel.Name = "vectorPanel";
            this.vectorPanel.X = 0;
            this.vectorPanel.Y = 0;
            // 
            // saveSoundButton
            // 
            this.saveSoundButton.BackColor = System.Drawing.SystemColors.ControlDarkDark;
            resources.ApplyResources(this.saveSoundButton, "saveSoundButton");
            this.saveSoundButton.ForeColor = System.Drawing.SystemColors.ControlLight;
            this.saveSoundButton.Name = "saveSoundButton";
            this.saveSoundButton.UseVisualStyleBackColor = false;
            this.saveSoundButton.Click += new System.EventHandler(this.saveSoundButton_Click);
            // 
            // saveImageButton
            // 
            this.saveImageButton.BackColor = System.Drawing.SystemColors.ControlDarkDark;
            resources.ApplyResources(this.saveImageButton, "saveImageButton");
            this.saveImageButton.ForeColor = System.Drawing.SystemColors.ControlLight;
            this.saveImageButton.Name = "saveImageButton";
            this.saveImageButton.UseVisualStyleBackColor = false;
            this.saveImageButton.Click += new System.EventHandler(this.saveImageButton_Click);
            // 
            // changeSoundButton
            // 
            this.changeSoundButton.BackColor = System.Drawing.SystemColors.ControlDarkDark;
            resources.ApplyResources(this.changeSoundButton, "changeSoundButton");
            this.changeSoundButton.ForeColor = System.Drawing.SystemColors.ControlLight;
            this.changeSoundButton.Name = "changeSoundButton";
            this.changeSoundButton.UseVisualStyleBackColor = false;
            this.changeSoundButton.Click += new System.EventHandler(this.changeSoundButton_Click);
            // 
            // mp3Player
            // 
            resources.ApplyResources(this.mp3Player, "mp3Player");
            this.mp3Player.Name = "mp3Player";
            this.mp3Player.SoundProperty = null;
            // 
            // changeImageButton
            // 
            this.changeImageButton.BackColor = System.Drawing.SystemColors.ControlDarkDark;
            resources.ApplyResources(this.changeImageButton, "changeImageButton");
            this.changeImageButton.ForeColor = System.Drawing.SystemColors.ControlLight;
            this.changeImageButton.Name = "changeImageButton";
            this.changeImageButton.UseVisualStyleBackColor = false;
            this.changeImageButton.Click += new System.EventHandler(this.changeImageButton_Click);
            // 
            // applyChangesButton
            // 
            this.applyChangesButton.BackColor = System.Drawing.SystemColors.ControlDarkDark;
            resources.ApplyResources(this.applyChangesButton, "applyChangesButton");
            this.applyChangesButton.ForeColor = System.Drawing.SystemColors.ControlLight;
            this.applyChangesButton.Name = "applyChangesButton";
            this.applyChangesButton.UseVisualStyleBackColor = false;
            this.applyChangesButton.Click += new System.EventHandler(this.applyChangesButton_Click);
            // 
            // nameBox
            // 
            this.nameBox.ButtonEnabled = false;
            resources.ApplyResources(this.nameBox, "nameBox");
            this.nameBox.Name = "nameBox";
            this.nameBox.ButtonClicked += new System.EventHandler(this.nameBox_ButtonClicked);
            // 
            // textPropBox
            // 
            resources.ApplyResources(this.textPropBox, "textPropBox");
            this.textPropBox.Name = "textPropBox";
            // 
            // pictureBoxPanel
            // 
            resources.ApplyResources(this.pictureBoxPanel, "pictureBoxPanel");
            this.pictureBoxPanel.BackColor = System.Drawing.SystemColors.Control;
            this.pictureBoxPanel.Controls.Add(this.panel1);
            this.pictureBoxPanel.Controls.Add(this.textPropBox);
            this.pictureBoxPanel.Name = "pictureBoxPanel";
            // 
            // cartesianPlaneX
            // 
            this.cartesianPlaneX.BackColor = System.Drawing.SystemColors.ScrollBar;
            resources.ApplyResources(this.cartesianPlaneX, "cartesianPlaneX");
            this.cartesianPlaneX.Name = "cartesianPlaneX";
            // 
            // cartesianPlaneY
            // 
            this.cartesianPlaneY.BackColor = System.Drawing.SystemColors.ScrollBar;
            resources.ApplyResources(this.cartesianPlaneY, "cartesianPlaneY");
            this.cartesianPlaneY.Name = "cartesianPlaneY";
            // 
            // canvasPropBox
            // 
            resources.ApplyResources(this.canvasPropBox, "canvasPropBox");
            this.canvasPropBox.Name = "canvasPropBox";
            this.canvasPropBox.TabStop = false;
            // 
            // fieldLimitPanel1
            // 
            resources.ApplyResources(this.fieldLimitPanel1, "fieldLimitPanel1");
            this.fieldLimitPanel1.Name = "fieldLimitPanel1";
            // 
            // statusStrip
            // 
            this.statusStrip.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.selectionLabel,
            this.mainProgressBar,
            this.secondaryProgressBar,
            this.toolStripStatusLabel_additionalInfo});
            resources.ApplyResources(this.statusStrip, "statusStrip");
            this.statusStrip.Name = "statusStrip";
            // 
            // selectionLabel
            // 
            this.selectionLabel.Name = "selectionLabel";
            resources.ApplyResources(this.selectionLabel, "selectionLabel");
            // 
            // mainProgressBar
            // 
            this.mainProgressBar.Name = "mainProgressBar";
            resources.ApplyResources(this.mainProgressBar, "mainProgressBar");
            // 
            // secondaryProgressBar
            // 
            this.secondaryProgressBar.Name = "secondaryProgressBar";
            resources.ApplyResources(this.secondaryProgressBar, "secondaryProgressBar");
            // 
            // toolStripStatusLabel_additionalInfo
            // 
            this.toolStripStatusLabel_additionalInfo.Margin = new System.Windows.Forms.Padding(200, 3, 0, 2);
            this.toolStripStatusLabel_additionalInfo.Name = "toolStripStatusLabel_additionalInfo";
            resources.ApplyResources(this.toolStripStatusLabel_additionalInfo, "toolStripStatusLabel_additionalInfo");
            // 
            // findStrip
            // 
            resources.ApplyResources(this.findStrip, "findStrip");
            this.findStrip.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.findStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnFindAll,
            this.btnFindNext,
            this.findBox,
            this.btnRestart,
            this.btnClose,
            this.btnOptions});
            this.findStrip.Name = "findStrip";
            this.findStrip.VisibleChanged += new System.EventHandler(this.findStrip_VisibleChanged);
            // 
            // btnFindAll
            // 
            this.btnFindAll.Image = global::HaRepackerLib.Properties.Resources.find;
            resources.ApplyResources(this.btnFindAll, "btnFindAll");
            this.btnFindAll.Name = "btnFindAll";
            this.btnFindAll.Click += new System.EventHandler(this.btnFindAll_Click);
            // 
            // btnFindNext
            // 
            this.btnFindNext.Image = global::HaRepackerLib.Properties.Resources.arrow_right;
            resources.ApplyResources(this.btnFindNext, "btnFindNext");
            this.btnFindNext.Name = "btnFindNext";
            this.btnFindNext.Click += new System.EventHandler(this.btnFindNext_Click);
            // 
            // findBox
            // 
            resources.ApplyResources(this.findBox, "findBox");
            this.findBox.Name = "findBox";
            this.findBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.findBox_KeyDown);
            this.findBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.findBox_KeyPress);
            this.findBox.TextChanged += new System.EventHandler(this.findBox_TextChanged);
            // 
            // btnRestart
            // 
            this.btnRestart.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnRestart.Image = global::HaRepackerLib.Properties.Resources.undo;
            resources.ApplyResources(this.btnRestart, "btnRestart");
            this.btnRestart.Name = "btnRestart";
            this.btnRestart.Click += new System.EventHandler(this.btnRestart_Click);
            // 
            // btnClose
            // 
            this.btnClose.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnClose.Image = global::HaRepackerLib.Properties.Resources.red_x1;
            resources.ApplyResources(this.btnClose, "btnClose");
            this.btnClose.Name = "btnClose";
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // btnOptions
            // 
            this.btnOptions.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            resources.ApplyResources(this.btnOptions, "btnOptions");
            this.btnOptions.Name = "btnOptions";
            this.btnOptions.Click += new System.EventHandler(this.btnOptions_Click);
            // 
            // MainDockPanel
            // 
            this.MainDockPanel.DockBackColor = System.Drawing.SystemColors.Control;
            this.MainDockPanel.DocumentStyle = WeifenLuo.WinFormsUI.Docking.DocumentStyle.DockingWindow;
            resources.ApplyResources(this.MainDockPanel, "MainDockPanel");
            this.MainDockPanel.Name = "MainDockPanel";
            // 
            // timerImgSequence
            // 
            this.timerImgSequence.Tick += new System.EventHandler(this.timerImgSequence_Tick);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.cartesianPlaneX);
            this.panel1.Controls.Add(this.canvasPropBox);
            this.panel1.Controls.Add(this.cartesianPlaneY);
            resources.ApplyResources(this.panel1, "panel1");
            this.panel1.Name = "panel1";
            // 
            // HaRepackerMainPanel
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.findStrip);
            this.Controls.Add(this.MainDockPanel);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.MainSplitContainer);
            this.Name = "HaRepackerMainPanel";
            this.SizeChanged += new System.EventHandler(this.HaRepackerMainPanel_SizeChanged);
            this.MainSplitContainer.Panel1.ResumeLayout(false);
            this.MainSplitContainer.Panel2.ResumeLayout(false);
            this.MainSplitContainer.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.MainSplitContainer)).EndInit();
            this.MainSplitContainer.ResumeLayout(false);
            this.pictureBoxPanel.ResumeLayout(false);
            this.pictureBoxPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.canvasPropBox)).EndInit();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.findStrip.ResumeLayout(false);
            this.findStrip.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }
        #endregion

        private System.Windows.Forms.SplitContainer MainSplitContainer;
        private System.Windows.Forms.PictureBox canvasPropBox;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel selectionLabel;
        private System.Windows.Forms.Panel pictureBoxPanel;
        private System.Windows.Forms.TextBox textPropBox;
        private SoundPlayer mp3Player;
        public System.Windows.Forms.ToolStripProgressBar secondaryProgressBar;
        public System.Windows.Forms.ToolStripProgressBar mainProgressBar;
        private ChangableTextbox nameBox;
        private System.Windows.Forms.Button saveImageButton;
        private System.Windows.Forms.Button changeSoundButton;
        private System.Windows.Forms.Button changeImageButton;
        private System.Windows.Forms.Button applyChangesButton;
        private System.Windows.Forms.Button saveSoundButton;
        private System.Windows.Forms.ToolStripButton btnFindNext;
        private System.Windows.Forms.ToolStripTextBox findBox;
        private System.Windows.Forms.ToolStripButton btnClose;
        private System.Windows.Forms.ToolStripButton btnRestart;
        private System.Windows.Forms.ToolStripButton btnFindAll;
        private System.Windows.Forms.ToolStripButton btnOptions;
        public System.Windows.Forms.ToolStrip findStrip;
        public TreeViewMS.TreeViewMS DataTree;
        private WeifenLuo.WinFormsUI.Docking.DockPanel MainDockPanel;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel_additionalInfo;
        private XYPanel vectorPanel;
        private System.Windows.Forms.Panel cartesianPlaneX;
        private System.Windows.Forms.Panel cartesianPlaneY;
        private FieldLimitPanel fieldLimitPanel1;
        private System.Windows.Forms.Button button_animateSelectedCanvas;
        private System.Windows.Forms.CheckBox cartesianPlane_checkBox;
        private System.Windows.Forms.ComboBox planePosition_comboBox;
        private System.Windows.Forms.Label nextLoopTime_label;
        private System.Windows.Forms.ComboBox nextLoopTime_comboBox;
        private System.Windows.Forms.Timer timerImgSequence;
        private System.Windows.Forms.Panel panel1;
    }
}
