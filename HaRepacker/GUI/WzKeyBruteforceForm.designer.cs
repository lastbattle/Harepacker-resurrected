using HaRepacker.GUI.Input;

namespace HaRepacker.GUI
{
    partial class WzKeyBruteforceForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(WzKeyBruteforceForm));
            button_startStop = new System.Windows.Forms.Button();
            label1 = new System.Windows.Forms.Label();
            label_ivTries = new System.Windows.Forms.Label();
            label3 = new System.Windows.Forms.Label();
            label_duration = new System.Windows.Forms.Label();
            label2 = new System.Windows.Forms.Label();
            label_key = new System.Windows.Forms.Label();
            SuspendLayout();
            // 
            // button_startStop
            // 
            resources.ApplyResources(button_startStop, "button_startStop");
            button_startStop.Name = "button_startStop";
            button_startStop.UseVisualStyleBackColor = true;
            button_startStop.Click += button_startStop_Click;
            // 
            // label1
            // 
            resources.ApplyResources(label1, "label1");
            label1.Name = "label1";
            // 
            // label_ivTries
            // 
            resources.ApplyResources(label_ivTries, "label_ivTries");
            label_ivTries.Name = "label_ivTries";
            // 
            // label3
            // 
            resources.ApplyResources(label3, "label3");
            label3.Name = "label3";
            // 
            // label_duration
            // 
            resources.ApplyResources(label_duration, "label_duration");
            label_duration.Name = "label_duration";
            // 
            // label2
            // 
            resources.ApplyResources(label2, "label2");
            label2.Name = "label2";
            // 
            // label_key
            // 
            resources.ApplyResources(label_key, "label_key");
            label_key.Name = "label_key";
            // 
            // WzKeyBruteforceForm
            // 
            resources.ApplyResources(this, "$this");
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            Controls.Add(label_key);
            Controls.Add(label2);
            Controls.Add(label_duration);
            Controls.Add(label3);
            Controls.Add(label_ivTries);
            Controls.Add(label1);
            Controls.Add(button_startStop);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            Name = "WzKeyBruteforceForm";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Button button_startStop;
        private IntegerInput versionBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label_ivTries;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label_duration;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label_key;
    }
}