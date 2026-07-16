using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace HaCreator.GUI.EditorPanels
{
    partial class AISettingsDialog
    {
        private IContainer components = null;

        private GroupBox grpEndpoint;
        private GroupBox grpModelCatalog;
        private GroupBox grpRequestOptions;
        private GroupBox grpMapEditing;

        private Label lblBaseUrl;
        private Label lblApiKey;
        private Label lblModel;
        private Label lblModelHelp;
        private Label lblModelsStatus;
        private Label lblApiDialect;
        private Label lblReasoningEffort;
        private Label lblReasoningStatus;
        private Label lblStatus;

        private TextBox txtBaseUrl;
        private TextBox txtApiKey;
        private ComboBox cboModel;
        private ComboBox cboApiDialect;
        private ComboBox cboReasoningEffort;
        private CheckBox chkStrictSchemas;
        private CheckBox chkAutoApply;

        private Button btnRefreshModels;
        private Button btnTest;
        private Button btnSave;
        private Button btnCancel;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();

            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            grpEndpoint = new GroupBox();
            lblBaseUrl = new Label();
            txtBaseUrl = new TextBox();
            lblApiKey = new Label();
            txtApiKey = new TextBox();
            grpModelCatalog = new GroupBox();
            lblModel = new Label();
            cboModel = new ComboBox();
            btnRefreshModels = new Button();
            lblModelsStatus = new Label();
            lblModelHelp = new Label();
            grpRequestOptions = new GroupBox();
            lblApiDialect = new Label();
            cboApiDialect = new ComboBox();
            lblReasoningEffort = new Label();
            cboReasoningEffort = new ComboBox();
            lblReasoningStatus = new Label();
            grpMapEditing = new GroupBox();
            chkStrictSchemas = new CheckBox();
            chkAutoApply = new CheckBox();
            btnTest = new Button();
            lblStatus = new Label();
            btnSave = new Button();
            btnCancel = new Button();
            grpEndpoint.SuspendLayout();
            grpModelCatalog.SuspendLayout();
            grpRequestOptions.SuspendLayout();
            grpMapEditing.SuspendLayout();
            SuspendLayout();
            //
            // grpEndpoint
            //
            grpEndpoint.Controls.Add(lblBaseUrl);
            grpEndpoint.Controls.Add(txtBaseUrl);
            grpEndpoint.Controls.Add(lblApiKey);
            grpEndpoint.Controls.Add(txtApiKey);
            grpEndpoint.Location = new Point(12, 12);
            grpEndpoint.Name = "grpEndpoint";
            grpEndpoint.Size = new Size(586, 112);
            grpEndpoint.TabIndex = 0;
            grpEndpoint.TabStop = false;
            grpEndpoint.Text = "Endpoint";
            //
            // lblBaseUrl
            //
            lblBaseUrl.Location = new Point(15, 29);
            lblBaseUrl.Name = "lblBaseUrl";
            lblBaseUrl.Size = new Size(120, 20);
            lblBaseUrl.TabIndex = 0;
            lblBaseUrl.Text = "Base URL:";
            //
            // txtBaseUrl
            //
            txtBaseUrl.Location = new Point(145, 26);
            txtBaseUrl.Name = "txtBaseUrl";
            txtBaseUrl.PlaceholderText = "https://api.openai.com/v1";
            txtBaseUrl.Size = new Size(420, 23);
            txtBaseUrl.TabIndex = 1;
            txtBaseUrl.TextChanged += OnSettingsChanged;
            txtBaseUrl.Leave += TxtBaseUrl_Leave;
            //
            // lblApiKey
            //
            lblApiKey.Location = new Point(15, 71);
            lblApiKey.Name = "lblApiKey";
            lblApiKey.Size = new Size(120, 20);
            lblApiKey.TabIndex = 2;
            lblApiKey.Text = "API Key:";
            //
            // txtApiKey
            //
            txtApiKey.Location = new Point(145, 68);
            txtApiKey.Name = "txtApiKey";
            txtApiKey.Size = new Size(420, 23);
            txtApiKey.TabIndex = 3;
            txtApiKey.UseSystemPasswordChar = true;
            txtApiKey.TextChanged += OnSettingsChanged;
            txtApiKey.Leave += TxtApiKey_Leave;
            //
            // grpModelCatalog
            //
            grpModelCatalog.Controls.Add(lblModel);
            grpModelCatalog.Controls.Add(cboModel);
            grpModelCatalog.Controls.Add(btnRefreshModels);
            grpModelCatalog.Controls.Add(lblModelsStatus);
            grpModelCatalog.Controls.Add(lblModelHelp);
            grpModelCatalog.Location = new Point(12, 134);
            grpModelCatalog.Name = "grpModelCatalog";
            grpModelCatalog.Size = new Size(586, 132);
            grpModelCatalog.TabIndex = 1;
            grpModelCatalog.TabStop = false;
            grpModelCatalog.Text = "Model catalog";
            //
            // lblModel
            //
            lblModel.Location = new Point(15, 31);
            lblModel.Name = "lblModel";
            lblModel.Size = new Size(120, 20);
            lblModel.TabIndex = 0;
            lblModel.Text = "Model:";
            //
            // cboModel
            //
            cboModel.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            cboModel.AutoCompleteSource = AutoCompleteSource.ListItems;
            cboModel.Location = new Point(145, 28);
            cboModel.MaxDropDownItems = 18;
            cboModel.Name = "cboModel";
            cboModel.Size = new Size(320, 23);
            cboModel.TabIndex = 1;
            cboModel.SelectedIndexChanged += CboModel_SelectedIndexChanged;
            cboModel.TextChanged += CboModel_TextChanged;
            //
            // btnRefreshModels
            //
            btnRefreshModels.Location = new Point(475, 28);
            btnRefreshModels.Name = "btnRefreshModels";
            btnRefreshModels.Size = new Size(90, 25);
            btnRefreshModels.TabIndex = 2;
            btnRefreshModels.Text = "Refresh";
            btnRefreshModels.Click += BtnRefreshModels_Click;
            //
            // lblModelsStatus
            //
            lblModelsStatus.AutoEllipsis = true;
            lblModelsStatus.ForeColor = Color.Gray;
            lblModelsStatus.Location = new Point(145, 61);
            lblModelsStatus.Name = "lblModelsStatus";
            lblModelsStatus.Size = new Size(420, 22);
            lblModelsStatus.TabIndex = 3;
            //
            // lblModelHelp
            //
            lblModelHelp.ForeColor = Color.Gray;
            lblModelHelp.Location = new Point(145, 88);
            lblModelHelp.Name = "lblModelHelp";
            lblModelHelp.Size = new Size(420, 20);
            lblModelHelp.TabIndex = 4;
            lblModelHelp.Text = "Built-in and endpoint models share one list. Type a custom model ID if needed.";
            //
            // grpRequestOptions
            //
            grpRequestOptions.Controls.Add(lblApiDialect);
            grpRequestOptions.Controls.Add(cboApiDialect);
            grpRequestOptions.Controls.Add(lblReasoningEffort);
            grpRequestOptions.Controls.Add(cboReasoningEffort);
            grpRequestOptions.Controls.Add(lblReasoningStatus);
            grpRequestOptions.Location = new Point(12, 276);
            grpRequestOptions.Name = "grpRequestOptions";
            grpRequestOptions.Size = new Size(586, 115);
            grpRequestOptions.TabIndex = 2;
            grpRequestOptions.TabStop = false;
            grpRequestOptions.Text = "Request options";
            //
            // lblApiDialect
            //
            lblApiDialect.Location = new Point(15, 31);
            lblApiDialect.Name = "lblApiDialect";
            lblApiDialect.Size = new Size(120, 20);
            lblApiDialect.TabIndex = 0;
            lblApiDialect.Text = "API Dialect:";
            //
            // cboApiDialect
            //
            cboApiDialect.DropDownStyle = ComboBoxStyle.DropDownList;
            cboApiDialect.Items.AddRange(new object[] { "Chat Completions", "Responses" });
            cboApiDialect.Location = new Point(145, 28);
            cboApiDialect.Name = "cboApiDialect";
            cboApiDialect.Size = new Size(190, 23);
            cboApiDialect.TabIndex = 1;
            cboApiDialect.SelectedIndexChanged += OnSettingsChanged;
            //
            // lblReasoningEffort
            //
            lblReasoningEffort.Location = new Point(15, 72);
            lblReasoningEffort.Name = "lblReasoningEffort";
            lblReasoningEffort.Size = new Size(120, 20);
            lblReasoningEffort.TabIndex = 2;
            lblReasoningEffort.Text = "Reasoning Effort:";
            //
            // cboReasoningEffort
            //
            cboReasoningEffort.DropDownStyle = ComboBoxStyle.DropDownList;
            cboReasoningEffort.Location = new Point(145, 69);
            cboReasoningEffort.Name = "cboReasoningEffort";
            cboReasoningEffort.Size = new Size(190, 23);
            cboReasoningEffort.TabIndex = 3;
            cboReasoningEffort.SelectedIndexChanged += OnSettingsChanged;
            //
            // lblReasoningStatus
            //
            lblReasoningStatus.AutoEllipsis = true;
            lblReasoningStatus.ForeColor = Color.Gray;
            lblReasoningStatus.Location = new Point(350, 69);
            lblReasoningStatus.Name = "lblReasoningStatus";
            lblReasoningStatus.Size = new Size(215, 35);
            lblReasoningStatus.TabIndex = 4;
            //
            // grpMapEditing
            //
            grpMapEditing.Controls.Add(chkStrictSchemas);
            grpMapEditing.Controls.Add(chkAutoApply);
            grpMapEditing.Location = new Point(12, 401);
            grpMapEditing.Name = "grpMapEditing";
            grpMapEditing.Size = new Size(586, 70);
            grpMapEditing.TabIndex = 3;
            grpMapEditing.TabStop = false;
            grpMapEditing.Text = "Map editing behavior";
            //
            // chkStrictSchemas
            //
            chkStrictSchemas.Location = new Point(15, 28);
            chkStrictSchemas.Name = "chkStrictSchemas";
            chkStrictSchemas.Size = new Size(220, 20);
            chkStrictSchemas.TabIndex = 0;
            chkStrictSchemas.Text = "Use strict tool schemas";
            chkStrictSchemas.CheckedChanged += OnSettingsChanged;
            //
            // chkAutoApply
            //
            chkAutoApply.Location = new Point(285, 28);
            chkAutoApply.Name = "chkAutoApply";
            chkAutoApply.Size = new Size(280, 20);
            chkAutoApply.TabIndex = 1;
            chkAutoApply.Text = "Apply generated map edits automatically";
            chkAutoApply.CheckedChanged += OnSettingsChanged;
            //
            // btnTest
            //
            btnTest.Location = new Point(12, 486);
            btnTest.Name = "btnTest";
            btnTest.Size = new Size(130, 30);
            btnTest.TabIndex = 4;
            btnTest.Text = "Test Connection";
            btnTest.Click += BtnTest_Click;
            //
            // lblStatus
            //
            lblStatus.AutoEllipsis = true;
            lblStatus.ForeColor = Color.Gray;
            lblStatus.Location = new Point(155, 485);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(443, 38);
            lblStatus.TabIndex = 5;
            //
            // btnSave
            //
            btnSave.Enabled = false;
            btnSave.Location = new Point(408, 555);
            btnSave.Name = "btnSave";
            btnSave.Size = new Size(90, 30);
            btnSave.TabIndex = 6;
            btnSave.Text = "Save";
            btnSave.Click += BtnSave_Click;
            //
            // btnCancel
            //
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.Location = new Point(508, 555);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(90, 30);
            btnCancel.TabIndex = 7;
            btnCancel.Text = "Cancel";
            //
            // AISettingsDialog
            //
            AcceptButton = btnSave;
            CancelButton = btnCancel;
            ClientSize = new Size(610, 586);
            Controls.Add(grpEndpoint);
            Controls.Add(grpModelCatalog);
            Controls.Add(grpRequestOptions);
            Controls.Add(grpMapEditing);
            Controls.Add(btnTest);
            Controls.Add(lblStatus);
            Controls.Add(btnSave);
            Controls.Add(btnCancel);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "AISettingsDialog";
            StartPosition = FormStartPosition.CenterParent;
            Text = "AI Settings";
            Shown += AISettingsDialog_Shown;
            grpEndpoint.ResumeLayout(false);
            grpEndpoint.PerformLayout();
            grpModelCatalog.ResumeLayout(false);
            grpRequestOptions.ResumeLayout(false);
            grpMapEditing.ResumeLayout(false);
            ResumeLayout(false);
        }
    }
}
