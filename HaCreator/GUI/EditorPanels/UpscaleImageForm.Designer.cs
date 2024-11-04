using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace HaCreator.GUI.EditorPanels
{
    partial class UpscaleImageForm
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
            this.components = new Container();
            ComponentResourceManager resources = new ComponentResourceManager(typeof(UpscaleImageForm));

            // Main TableLayoutPanel
            this.tableLayoutPanel_main = new TableLayoutPanel();
            this.tableLayoutPanel_main.Dock = DockStyle.Fill;
            this.tableLayoutPanel_main.ColumnCount = 1;
            this.tableLayoutPanel_main.RowCount = 2;
            this.tableLayoutPanel_main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.tableLayoutPanel_main.RowStyles.Add(new RowStyle(SizeType.Percent, 90F));
            this.tableLayoutPanel_main.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));

            // Images TableLayoutPanel
            this.tableLayoutPanel_images = new TableLayoutPanel();
            this.tableLayoutPanel_images.Dock = DockStyle.Fill;
            this.tableLayoutPanel_images.ColumnCount = 2;
            this.tableLayoutPanel_images.RowCount = 2;
            this.tableLayoutPanel_images.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            this.tableLayoutPanel_images.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            this.tableLayoutPanel_images.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            this.tableLayoutPanel_images.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // PictureBoxes
            this.pictureBox_before = new PictureBox();
            this.pictureBox_before.Dock = DockStyle.Fill;
            this.pictureBox_before.SizeMode = PictureBoxSizeMode.Zoom;

            this.pictureBox_after = new PictureBox();
            this.pictureBox_after.Dock = DockStyle.Fill;
            this.pictureBox_after.SizeMode = PictureBoxSizeMode.Zoom;

            // Labels
            this.label_before = new Label();
            this.label_before.Text = "Before";
            this.label_before.Dock = DockStyle.Fill;
            this.label_before.TextAlign = ContentAlignment.MiddleCenter;

            this.label_after = new Label();
            this.label_after.Text = "After";
            this.label_after.Dock = DockStyle.Fill;
            this.label_after.TextAlign = ContentAlignment.MiddleCenter;

            // Buttons FlowLayoutPanel
            this.flowLayoutPanel_buttons = new FlowLayoutPanel();
            this.flowLayoutPanel_buttons.Dock = DockStyle.Fill;
            this.flowLayoutPanel_buttons.FlowDirection = FlowDirection.RightToLeft;

            this.button_cancel = new Button();
            this.button_cancel.Text = "Cancel";
            this.button_cancel.Click += new EventHandler(this.button_cancel_Click);

            this.button_ok = new Button();
            this.button_ok.Text = "OK";
            this.button_ok.Click += new EventHandler(this.button_ok_Click);

            // Add controls to layouts
            this.tableLayoutPanel_images.Controls.Add(this.label_before, 0, 0);
            this.tableLayoutPanel_images.Controls.Add(this.label_after, 1, 0);
            this.tableLayoutPanel_images.Controls.Add(this.pictureBox_before, 0, 1);
            this.tableLayoutPanel_images.Controls.Add(this.pictureBox_after, 1, 1);

            this.flowLayoutPanel_buttons.Controls.Add(this.button_cancel);
            this.flowLayoutPanel_buttons.Controls.Add(this.button_ok);

            this.tableLayoutPanel_main.Controls.Add(this.tableLayoutPanel_images, 0, 0);
            this.tableLayoutPanel_main.Controls.Add(this.flowLayoutPanel_buttons, 0, 1);

            // Form properties
            this.ClientSize = new Size(800, 600);
            this.Controls.Add(this.tableLayoutPanel_main);
            this.Name = "UpscaleImageForm";
            this.Text = "Upscale Image";

            // Finalize
            this.tableLayoutPanel_main.ResumeLayout(false);
            this.tableLayoutPanel_images.ResumeLayout(false);
            this.flowLayoutPanel_buttons.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel tableLayoutPanel_main;
        private TableLayoutPanel tableLayoutPanel_images;
        private Label label_before;
        private Label label_after;
        private FlowLayoutPanel flowLayoutPanel_buttons;
        private PictureBox pictureBox_before;
        private PictureBox pictureBox_after;
        private Button button_ok;
        private Button button_cancel;
        private BindingSource bindingSource1;
    }
}