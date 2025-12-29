/* Copyright (C) 2024 HaCreator AI Extension
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using HaCreator.MapEditor.AI;

namespace HaCreator.GUI.EditorPanels
{
    /// <summary>
    /// Dialog for configuring OpenRouter API settings
    /// </summary>
    public class AISettingsDialog : Form
    {
        private TextBox txtApiKey;
        private ComboBox cboModel;
        private Button btnSave;
        private Button btnCancel;
        private Button btnTest;
        private Label lblStatus;
        private LinkLabel lnkGetKey;

        public AISettingsDialog()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void InitializeComponent()
        {
            this.Text = "AI Settings - OpenRouter Configuration";
            this.Size = new Size(500, 280);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            var lblApiKey = new Label
            {
                Text = "OpenRouter API Key:",
                Location = new Point(20, 20),
                Size = new Size(150, 20)
            };

            txtApiKey = new TextBox
            {
                Location = new Point(20, 45),
                Size = new Size(440, 25),
                UseSystemPasswordChar = true,
                Text = AISettings.ApiKey
            };

            lnkGetKey = new LinkLabel
            {
                Text = "Get your API key from openrouter.ai",
                Location = new Point(20, 75),
                Size = new Size(250, 20)
            };
            lnkGetKey.LinkClicked += (s, e) =>
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://openrouter.ai/keys",
                    UseShellExecute = true
                });
            };

            var lblModel = new Label
            {
                Text = "AI Model:",
                Location = new Point(20, 105),
                Size = new Size(150, 20)
            };

            cboModel = new ComboBox
            {
                Location = new Point(20, 130),
                Size = new Size(300, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboModel.Items.AddRange(AISettings.AvailableModels);
            cboModel.SelectedItem = AISettings.Model;
            if (cboModel.SelectedIndex < 0 && cboModel.Items.Count > 0)
                cboModel.SelectedIndex = 0;

            btnTest = new Button
            {
                Text = "Test Connection",
                Location = new Point(330, 128),
                Size = new Size(130, 28)
            };
            btnTest.Click += BtnTest_Click;

            lblStatus = new Label
            {
                Text = "",
                Location = new Point(20, 165),
                Size = new Size(440, 20),
                ForeColor = Color.Gray
            };

            btnSave = new Button
            {
                Text = "Save",
                Location = new Point(280, 200),
                Size = new Size(90, 30),
                DialogResult = DialogResult.OK
            };
            btnSave.Click += BtnSave_Click;

            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(380, 200),
                Size = new Size(80, 30),
                DialogResult = DialogResult.Cancel
            };

            this.Controls.AddRange(new Control[]
            {
                lblApiKey, txtApiKey, lnkGetKey,
                lblModel, cboModel, btnTest,
                lblStatus,
                btnSave, btnCancel
            });

            this.AcceptButton = btnSave;
            this.CancelButton = btnCancel;
        }

        private void LoadSettings()
        {
            txtApiKey.Text = AISettings.ApiKey;
            if (!string.IsNullOrEmpty(AISettings.ApiKey))
            {
                lblStatus.Text = "API key is configured.";
                lblStatus.ForeColor = Color.Green;
            }
        }

        private async void BtnTest_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtApiKey.Text))
            {
                lblStatus.Text = "Please enter an API key first.";
                lblStatus.ForeColor = Color.Red;
                return;
            }

            btnTest.Enabled = false;
            lblStatus.Text = "Testing connection...";
            lblStatus.ForeColor = Color.Gray;

            try
            {
                var client = new OpenRouterClient(txtApiKey.Text, cboModel.SelectedItem?.ToString() ?? AISettings.Model);
                var success = await client.TestConnectionAsync();

                if (success)
                {
                    lblStatus.Text = "Connection successful!";
                    lblStatus.ForeColor = Color.Green;
                }
                else
                {
                    lblStatus.Text = "Connection failed. Check your API key.";
                    lblStatus.ForeColor = Color.Red;
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Error: {ex.Message}";
                lblStatus.ForeColor = Color.Red;
            }
            finally
            {
                btnTest.Enabled = true;
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            AISettings.ApiKey = txtApiKey.Text.Trim();
            AISettings.Model = cboModel.SelectedItem?.ToString() ?? AISettings.AvailableModels[0];
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
