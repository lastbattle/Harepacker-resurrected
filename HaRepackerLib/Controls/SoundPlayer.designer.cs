namespace HaRepackerLib.Controls
{
    partial class SoundPlayer
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SoundPlayer));
            this.containerPanel = new System.Windows.Forms.Panel();
            this.LoopBox = new System.Windows.Forms.CheckBox();
            this.TimeBar = new System.Windows.Forms.TrackBar();
            this.CurrentPositionLabel = new System.Windows.Forms.Label();
            this.LengthLabel = new System.Windows.Forms.Label();
            this.PauseButton = new System.Windows.Forms.Button();
            this.PlayButton = new System.Windows.Forms.Button();
            this.AudioTimer = new System.Windows.Forms.Timer(this.components);
            this.containerPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.TimeBar)).BeginInit();
            this.SuspendLayout();
            // 
            // containerPanel
            // 
            resources.ApplyResources(this.containerPanel, "containerPanel");
            this.containerPanel.Controls.Add(this.LoopBox);
            this.containerPanel.Controls.Add(this.TimeBar);
            this.containerPanel.Controls.Add(this.CurrentPositionLabel);
            this.containerPanel.Controls.Add(this.LengthLabel);
            this.containerPanel.Controls.Add(this.PauseButton);
            this.containerPanel.Controls.Add(this.PlayButton);
            this.containerPanel.Name = "containerPanel";
            // 
            // LoopBox
            // 
            resources.ApplyResources(this.LoopBox, "LoopBox");
            this.LoopBox.Name = "LoopBox";
            this.LoopBox.CheckedChanged += new System.EventHandler(this.LoopBox_CheckedChanged);
            // 
            // TimeBar
            // 
            resources.ApplyResources(this.TimeBar, "TimeBar");
            this.TimeBar.Name = "TimeBar";
            this.TimeBar.TickStyle = System.Windows.Forms.TickStyle.None;
            this.TimeBar.Scroll += new System.EventHandler(this.TimeBar_Scroll);
            // 
            // CurrentPositionLabel
            // 
            resources.ApplyResources(this.CurrentPositionLabel, "CurrentPositionLabel");
            this.CurrentPositionLabel.BackColor = System.Drawing.Color.Transparent;
            this.CurrentPositionLabel.Name = "CurrentPositionLabel";
            // 
            // LengthLabel
            // 
            resources.ApplyResources(this.LengthLabel, "LengthLabel");
            this.LengthLabel.BackColor = System.Drawing.Color.Transparent;
            this.LengthLabel.Name = "LengthLabel";
            // 
            // PauseButton
            // 
            resources.ApplyResources(this.PauseButton, "PauseButton");
            this.PauseButton.FlatAppearance.BorderSize = 0;
            this.PauseButton.Image = global::HaRepackerLib.Properties.Resources.Pause;
            this.PauseButton.Name = "PauseButton";
            this.PauseButton.UseVisualStyleBackColor = true;
            this.PauseButton.Click += new System.EventHandler(this.PauseButton_Click);
            // 
            // PlayButton
            // 
            resources.ApplyResources(this.PlayButton, "PlayButton");
            this.PlayButton.BackColor = System.Drawing.SystemColors.Control;
            this.PlayButton.FlatAppearance.BorderSize = 0;
            this.PlayButton.Image = global::HaRepackerLib.Properties.Resources.Play;
            this.PlayButton.Name = "PlayButton";
            this.PlayButton.UseVisualStyleBackColor = false;
            this.PlayButton.Click += new System.EventHandler(this.PlayButton_Click);
            // 
            // AudioTimer
            // 
            this.AudioTimer.Tick += new System.EventHandler(this.AudioTimer_Tick);
            // 
            // SoundPlayer
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.containerPanel);
            this.Name = "SoundPlayer";
            this.containerPanel.ResumeLayout(false);
            this.containerPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.TimeBar)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel containerPanel;
        private System.Windows.Forms.CheckBox LoopBox;
        private System.Windows.Forms.TrackBar TimeBar;
        private System.Windows.Forms.Label CurrentPositionLabel;
        private System.Windows.Forms.Label LengthLabel;
        private System.Windows.Forms.Button PauseButton;
        private System.Windows.Forms.Button PlayButton;
        private System.Windows.Forms.Timer AudioTimer;

    }
}
