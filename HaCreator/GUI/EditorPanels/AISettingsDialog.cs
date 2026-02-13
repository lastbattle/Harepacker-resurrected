using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using HaCreator.MapEditor.AI;

namespace HaCreator.GUI.EditorPanels
{
    /// <summary>
    /// Dialog for configuring AI API settings (OpenRouter and OpenCode)
    /// </summary>
    public class AISettingsDialog : Form
    {
        // Provider selection
        private ComboBox cboProvider;

        // OpenRouter controls
        private Panel pnlOpenRouter;
        private TextBox txtApiKey;
        private ComboBox cboModel;
        private LinkLabel lnkGetKey;

        // OpenCode controls
        private Panel pnlOpenCode;
        private TextBox txtOpenCodeHost;
        private NumericUpDown numOpenCodePort;
        private ComboBox cboOpenCodeModel;
        private CheckBox chkAutoStart;
        private LinkLabel lnkOpenCodeHelp;

        // Common controls
        private Button btnSave;
        private Button btnCancel;
        private Button btnTest;
        private Label lblStatus;

        // Track if connection has been successfully tested
        private bool _connectionTested = false;

        public AISettingsDialog()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void InitializeComponent()
        {
            this.Text = "AI Settings";
            this.Size = new Size(520, 380);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            // Provider selection
            var lblProvider = new Label
            {
                Text = "AI Provider:",
                Location = new Point(20, 20),
                Size = new Size(100, 20)
            };

            cboProvider = new ComboBox
            {
                Location = new Point(120, 17),
                Size = new Size(200, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboProvider.Items.Add("OpenRouter (Cloud API)");
            cboProvider.Items.Add("OpenCode (Local Server)");
            cboProvider.SelectedIndexChanged += CboProvider_SelectedIndexChanged;

            // === OpenRouter Panel ===
            pnlOpenRouter = new Panel
            {
                Location = new Point(10, 55),
                Size = new Size(485, 200),
                Visible = true
            };

            var lblApiKey = new Label
            {
                Text = "OpenRouter API Key:",
                Location = new Point(10, 10),
                Size = new Size(150, 20)
            };

            txtApiKey = new TextBox
            {
                Location = new Point(10, 35),
                Size = new Size(460, 25),
                UseSystemPasswordChar = true
            };
            txtApiKey.TextChanged += OnSettingsChanged;

            lnkGetKey = new LinkLabel
            {
                Text = "Get your API key from openrouter.ai",
                Location = new Point(10, 65),
                Size = new Size(250, 20)
            };
            lnkGetKey.LinkClicked += LnkGetKey_LinkClicked;

            var lblModel = new Label
            {
                Text = "AI Model:",
                Location = new Point(10, 95),
                Size = new Size(150, 20)
            };

            cboModel = new ComboBox
            {
                Location = new Point(10, 120),
                Size = new Size(350, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboModel.Items.AddRange(AISettings.AvailableModels);
            cboModel.SelectedIndexChanged += OnSettingsChanged;

            pnlOpenRouter.Controls.AddRange(new Control[]
            {
                lblApiKey, txtApiKey, lnkGetKey,
                lblModel, cboModel
            });

            // === OpenCode Panel ===
            pnlOpenCode = new Panel
            {
                Location = new Point(10, 55),
                Size = new Size(485, 200),
                Visible = false
            };

            var lblOpenCodeHost = new Label
            {
                Text = "OpenCode Server Host:",
                Location = new Point(10, 10),
                Size = new Size(150, 20)
            };

            txtOpenCodeHost = new TextBox
            {
                Location = new Point(10, 35),
                Size = new Size(250, 25)
            };
            txtOpenCodeHost.TextChanged += OnSettingsChanged;

            var lblOpenCodePort = new Label
            {
                Text = "Port:",
                Location = new Point(280, 38),
                Size = new Size(40, 20)
            };

            numOpenCodePort = new NumericUpDown
            {
                Location = new Point(320, 35),
                Size = new Size(80, 25),
                Minimum = 1,
                Maximum = 65535,
                Value = 4096
            };
            numOpenCodePort.ValueChanged += OnSettingsChanged;

            lnkOpenCodeHelp = new LinkLabel
            {
                Text = "OpenCode requires 'opencode serve' running locally",
                Location = new Point(10, 65),
                Size = new Size(350, 20)
            };
            lnkOpenCodeHelp.LinkClicked += (s, e) =>
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://opencode.ai/docs/server/",
                    UseShellExecute = true
                });
            };

            var lblOpenCodeModel = new Label
            {
                Text = "AI Model:",
                Location = new Point(10, 95),
                Size = new Size(150, 20)
            };

            cboOpenCodeModel = new ComboBox
            {
                Location = new Point(10, 120),
                Size = new Size(350, 25),
                DropDownStyle = ComboBoxStyle.DropDown // Allow custom models
            };
            cboOpenCodeModel.Items.AddRange(AISettings.AvailableOpenCodeModels);
            cboOpenCodeModel.TextChanged += OnSettingsChanged;

            chkAutoStart = new CheckBox
            {
                Text = "Auto-start server if not running",
                Location = new Point(10, 155),
                Size = new Size(250, 20),
                Checked = true
            };

            var lblOpenCodeNote = new Label
            {
                Text = "Note: OpenCode uses OAuth authentication. Run 'opencode auth' first.",
                Location = new Point(10, 180),
                Size = new Size(460, 20),
                ForeColor = Color.Gray
            };

            var btnRegenTools = new Button
            {
                Text = "Regenerate Tools",
                Location = new Point(280, 152),
                Size = new Size(120, 25)
            };
            btnRegenTools.Click += BtnRegenTools_Click;

            pnlOpenCode.Controls.AddRange(new Control[]
            {
                lblOpenCodeHost, txtOpenCodeHost,
                lblOpenCodePort, numOpenCodePort,
                lnkOpenCodeHelp,
                lblOpenCodeModel, cboOpenCodeModel,
                chkAutoStart, btnRegenTools,
                lblOpenCodeNote
            });

            // === Common Controls ===
            btnTest = new Button
            {
                Text = "Test Connection",
                Location = new Point(20, 265),
                Size = new Size(130, 28)
            };
            btnTest.Click += BtnTest_Click;

            lblStatus = new Label
            {
                Text = "",
                Location = new Point(160, 270),
                Size = new Size(340, 20),
                ForeColor = Color.Gray
            };

            btnSave = new Button
            {
                Text = "Save",
                Location = new Point(310, 305),
                Size = new Size(90, 30),
                DialogResult = DialogResult.OK,
                Enabled = false // Disabled until connection is tested
            };
            btnSave.Click += BtnSave_Click;

            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(410, 305),
                Size = new Size(80, 30),
                DialogResult = DialogResult.Cancel
            };

            this.Controls.AddRange(new Control[]
            {
                lblProvider, cboProvider,
                pnlOpenRouter,
                pnlOpenCode,
                btnTest, lblStatus,
                btnSave, btnCancel
            });

            this.AcceptButton = btnSave;
            this.CancelButton = btnCancel;
        }

        private void LnkGetKey_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://openrouter.ai/keys",
                UseShellExecute = true
            });
        }

        private void LoadSettings()
        {
            // Load provider selection
            cboProvider.SelectedIndex = AISettings.Provider == AIProvider.OpenCode ? 1 : 0;

            // Load OpenRouter settings
            txtApiKey.Text = AISettings.ApiKey;
            cboModel.SelectedItem = AISettings.Model;
            if (cboModel.SelectedIndex < 0 && cboModel.Items.Count > 0)
                cboModel.SelectedIndex = 0;

            // Load OpenCode settings
            txtOpenCodeHost.Text = AISettings.OpenCodeHost;
            numOpenCodePort.Value = AISettings.OpenCodePort;
            cboOpenCodeModel.Text = AISettings.OpenCodeModel;
            chkAutoStart.Checked = AISettings.OpenCodeAutoStart;

            UpdatePanelVisibility();
            UpdateStatusFromSettings();
        }

        private void CboProvider_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdatePanelVisibility();
            InvalidateConnectionTest();
        }

        private void OnSettingsChanged(object sender, EventArgs e)
        {
            InvalidateConnectionTest();
        }

        private void InvalidateConnectionTest()
        {
            _connectionTested = false;
            btnSave.Enabled = false;
            lblStatus.Text = "Test connection required before saving.";
            lblStatus.ForeColor = Color.Gray;
        }

        private void UpdatePanelVisibility()
        {
            bool isOpenCode = cboProvider.SelectedIndex == 1;
            pnlOpenRouter.Visible = !isOpenCode;
            pnlOpenCode.Visible = isOpenCode;
        }

        private void UpdateStatusFromSettings()
        {
            if (AISettings.IsConfigured)
            {
                lblStatus.Text = $"Configured: {AISettings.GetStatusDescription()}";
                lblStatus.ForeColor = Color.Green;
            }
            else
            {
                lblStatus.Text = "Not configured.";
                lblStatus.ForeColor = Color.Gray;
            }
        }

        private async void BtnTest_Click(object sender, EventArgs e)
        {
            btnTest.Enabled = false;
            lblStatus.Text = "Testing connection...";
            lblStatus.ForeColor = Color.Gray;

            try
            {
                bool success;
                bool isOpenCode = cboProvider.SelectedIndex == 1;

                if (isOpenCode)
                {
                    // Test OpenCode connection
                    var host = txtOpenCodeHost.Text.Trim();
                    var port = (int)numOpenCodePort.Value;
                    var autoStart = chkAutoStart.Checked;

                    if (string.IsNullOrWhiteSpace(host))
                    {
                        lblStatus.Text = "Please enter the OpenCode server host.";
                        lblStatus.ForeColor = Color.Red;
                        return;
                    }

                    if (autoStart)
                    {
                        lblStatus.Text = "Starting OpenCode server...";
                        lblStatus.ForeColor = Color.Gray;
                        lblStatus.Refresh();
                    }

                    var client = new OpenCodeClient(host, port, cboOpenCodeModel.Text, autoStart);
                    success = await client.TestConnectionAsync();

                    if (success)
                    {
                        var statusMsg = OpenCodeClient.IsManagedServerRunning
                            ? "OpenCode connection successful! (server auto-started)"
                            : "OpenCode connection successful!";
                        lblStatus.Text = statusMsg;
                        lblStatus.ForeColor = Color.Green;
                        _connectionTested = true;
                        btnSave.Enabled = true;
                    }
                    else
                    {
                        lblStatus.Text = autoStart
                            ? $"Failed to start/connect to OpenCode. Is 'opencode' installed?"
                            : $"Cannot connect to OpenCode at {host}:{port}. Is 'opencode serve' running?";
                        lblStatus.ForeColor = Color.Red;
                        _connectionTested = false;
                        btnSave.Enabled = false;
                    }
                }
                else
                {
                    // Test OpenRouter connection
                    if (string.IsNullOrWhiteSpace(txtApiKey.Text))
                    {
                        lblStatus.Text = "Please enter an API key first.";
                        lblStatus.ForeColor = Color.Red;
                        return;
                    }

                    var client = new OpenRouterClient(txtApiKey.Text, cboModel.SelectedItem?.ToString() ?? AISettings.Model);
                    success = await client.TestConnectionAsync();

                    if (success)
                    {
                        lblStatus.Text = "OpenRouter connection successful!";
                        lblStatus.ForeColor = Color.Green;
                        _connectionTested = true;
                        btnSave.Enabled = true;
                    }
                    else
                    {
                        lblStatus.Text = "Connection failed. Check your API key.";
                        lblStatus.ForeColor = Color.Red;
                        _connectionTested = false;
                        btnSave.Enabled = false;
                    }
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Error: {ex.Message}";
                lblStatus.ForeColor = Color.Red;
                _connectionTested = false;
                btnSave.Enabled = false;
            }
            finally
            {
                btnTest.Enabled = true;
            }
        }

        private void BtnRegenTools_Click(object sender, EventArgs e)
        {
            try
            {
                // Find project root by looking for .opencode folder
                var dir = AppDomain.CurrentDomain.BaseDirectory;
                while (!string.IsNullOrEmpty(dir))
                {
                    if (System.IO.Directory.Exists(System.IO.Path.Combine(dir, ".opencode")))
                        break;
                    var parent = System.IO.Directory.GetParent(dir);
                    if (parent == null) { dir = null; break; }
                    dir = parent.FullName;
                }

                if (string.IsNullOrEmpty(dir))
                {
                    // Fall back to prompting user
                    using (var fbd = new FolderBrowserDialog())
                    {
                        fbd.Description = "Select project root folder (containing .opencode folder)";
                        if (fbd.ShowDialog() != DialogResult.OK) return;
                        dir = fbd.SelectedPath;
                    }
                }

                var toolDir = System.IO.Path.Combine(dir, ".opencode", "tool");
                var count = OpenCodeToolGenerator.GenerateAllTools(toolDir);
                MessageBox.Show($"Generated {count} tool files in:\n{toolDir}", "Tools Regenerated",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error regenerating tools: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            bool isOpenCode = cboProvider.SelectedIndex == 1;

            // Save provider selection
            AISettings.Provider = isOpenCode ? AIProvider.OpenCode : AIProvider.OpenRouter;

            if (isOpenCode)
            {
                // Save OpenCode settings
                AISettings.OpenCodeHost = txtOpenCodeHost.Text.Trim();
                AISettings.OpenCodePort = (int)numOpenCodePort.Value;
                AISettings.OpenCodeModel = cboOpenCodeModel.Text.Trim();
                AISettings.OpenCodeAutoStart = chkAutoStart.Checked;
            }
            else
            {
                // Save OpenRouter settings
                AISettings.ApiKey = txtApiKey.Text.Trim();
                AISettings.Model = cboModel.SelectedItem?.ToString() ?? AISettings.AvailableModels[0];
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
