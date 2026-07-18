using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Forms = System.Windows.Forms;
using HaCreator.MapEditor.AI;

namespace HaCreator.GUI.EditorPanels
{
    /// <summary>
    /// Dialog for configuring an OpenAI-compatible AI endpoint.
    /// </summary>
    public partial class AISettingsDialog : Window
    {
        private const string AutoReasoningEffort = "Auto (model default)";

        private readonly List<OpenAIModelInfo> _endpointModels = new List<OpenAIModelInfo>();
        private bool _connectionTested;
        private bool _endpointModelsLoaded;
        private bool _initializing;
        private bool _loadingModels;
        private bool _updatingModelCatalog;
        private bool _updatingReasoningEffort;
        private bool _isClosed;

        private Forms.FormStartPosition startPosition = Forms.FormStartPosition.CenterParent;
        public Forms.FormStartPosition StartPosition
        {
            get => startPosition;
            set
            {
                startPosition = value;
                WindowStartupLocation = value == Forms.FormStartPosition.CenterScreen
                    ? WindowStartupLocation.CenterScreen
                    : WindowStartupLocation.CenterOwner;
            }
        }

        public AISettingsDialog()
        {
            InitializeComponent();
            EditorPanelLocalizer.Attach(this);
            cboModel.AddHandler(TextBoxBase.TextChangedEvent,
                new TextChangedEventHandler((sender, args) => CboModel_TextChanged(sender, args)));
            if (Program.HaEditorWindow?.IsVisible == true)
                Owner = Program.HaEditorWindow;
            LoadSettings();
        }

        private void LoadSettings()
        {
            _initializing = true;
            try
            {
                txtBaseUrl.Text = AISettings.BaseUrl;
                txtApiKey.Password = AISettings.ApiKey;
                cboModel.Text = AISettings.Model;
                cboApiDialect.SelectedIndex = AISettings.Protocol == AIEndpointProtocol.Responses ? 1 : 0;
                chkStrictSchemas.IsChecked = AISettings.StrictSchemas;
                chkAutoApply.IsChecked = AISettings.AutoApplyCommands;

                RebuildModelCatalog(Array.Empty<OpenAIModelInfo>());
                UpdateReasoningEffortChoices(FindModelChoice(AISettings.Model), AISettings.ReasoningEffort);
                UpdateStatusFromSettings();
            }
            finally
            {
                _initializing = false;
            }
        }

        private async void AISettingsDialog_Loaded(object sender, RoutedEventArgs e)
        {
            await RefreshEndpointModelsAsync();
        }

        private void AISettingsDialog_Closed(object sender, EventArgs e)
        {
            _isClosed = true;
        }

        private async void BtnRefreshModels_Click(object sender, RoutedEventArgs e)
        {
            await RefreshEndpointModelsAsync();
        }

        private async void TxtBaseUrl_Leave(object sender, EventArgs e)
        {
            await RefreshEndpointModelsAsync();
        }

        private async void TxtApiKey_Leave(object sender, EventArgs e)
        {
            await RefreshEndpointModelsAsync();
        }

        private void CboModel_SelectedIndexChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingModelCatalog)
                return;

            if (cboModel.SelectedItem is ModelChoice choice)
            {
                _updatingModelCatalog = true;
                try
                {
                    cboModel.Text = choice.ModelId;
                }
                finally
                {
                    _updatingModelCatalog = false;
                }
            }

            UpdateReasoningEffortChoices(FindModelChoice(cboModel.Text));
            InvalidateConnectionTest();
        }

        private void CboModel_TextChanged(object sender, EventArgs e)
        {
            if (_initializing || _updatingModelCatalog)
                return;

            UpdateReasoningEffortChoices(FindModelChoice(cboModel.Text));
            InvalidateConnectionTest();
        }

        private async Task RefreshEndpointModelsAsync()
        {
            if (_loadingModels || _isClosed)
                return;

            var baseUrl = txtBaseUrl.Text.Trim();
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                _endpointModelsLoaded = false;
                _endpointModels.Clear();
                RebuildModelCatalog(_endpointModels);
                lblModelsStatus.Text = EditorPanelLocalizer.Text("AISettings_EnterBaseUrl", "Enter a base URL to discover endpoint models.");
                return;
            }

            _loadingModels = true;
            btnRefreshModels.IsEnabled = false;
            lblModelsStatus.Text = EditorPanelLocalizer.Text("AISettings_DiscoveringModels", "Discovering models from endpoint...");
            lblModelsStatus.Foreground = Brushes.Gray;

            try
            {
                var client = new OpenAICompatibleClient(new OpenAICompatibleOptions
                {
                    BaseUrl = baseUrl,
                    ApiKey = txtApiKey.Password.Trim(),
                    Protocol = GetSelectedProtocol(),
                    Timeout = TimeSpan.FromSeconds(30)
                });

                IReadOnlyList<OpenAIModelInfo> models;
                using (client)
                {
                    models = await client.GetModelCatalogAsync();
                }

                if (_isClosed)
                    return;

                _endpointModels.Clear();
                _endpointModels.AddRange(models);
                _endpointModelsLoaded = true;
                RebuildModelCatalog(_endpointModels);
                lblModelsStatus.Text = models.Count == 0
                    ? EditorPanelLocalizer.Text("AISettings_NoEndpointModels", "No endpoint models were returned. Built-in presets remain available.")
                    : EditorPanelLocalizer.Format("AISettings_ModelsDiscovered", models.Count);
                lblModelsStatus.Foreground = Brushes.Gray;
            }
            catch (Exception ex)
            {
                if (!_isClosed)
                {
                    _endpointModels.Clear();
                    _endpointModelsLoaded = true;
                    RebuildModelCatalog(_endpointModels);
                    lblModelsStatus.Text = EditorPanelLocalizer.Format("AISettings_DiscoveryError", ex.Message);
                    lblModelsStatus.Foreground = Brushes.DarkOrange;
                }
            }
            finally
            {
                _loadingModels = false;
                if (!_isClosed)
                    btnRefreshModels.IsEnabled = true;
            }
        }

        private void RebuildModelCatalog(IReadOnlyList<OpenAIModelInfo> endpointModels)
        {
            var selectedModel = cboModel.Text.Trim();
            var choices = new List<ModelChoice>();

            foreach (var model in AISettings.AvailableModels)
                choices.Add(new ModelChoice(model, "Built-in"));

            foreach (var endpointModel in endpointModels)
            {
                if (string.IsNullOrWhiteSpace(endpointModel.Id))
                    continue;

                var existingIndex = choices.FindIndex(choice =>
                    string.Equals(choice.ModelId, endpointModel.Id, StringComparison.OrdinalIgnoreCase));
                if (existingIndex >= 0)
                {
                    choices[existingIndex] = new ModelChoice(
                        endpointModel.Id,
                        "Built-in + endpoint",
                        endpointModel.ReasoningEfforts);
                }
                else
                {
                    choices.Add(new ModelChoice(
                        endpointModel.Id,
                        "Endpoint",
                        endpointModel.ReasoningEfforts));
                }
            }

            _updatingModelCatalog = true;
            try
            {
                cboModel.Items.Clear();
                foreach (var choice in choices)
                    cboModel.Items.Add(choice);

                var selectedIndex = choices.FindIndex(choice =>
                    string.Equals(choice.ModelId, selectedModel, StringComparison.OrdinalIgnoreCase));
                cboModel.SelectedIndex = selectedIndex;
                cboModel.Text = selectedIndex >= 0 ? choices[selectedIndex].ModelId : selectedModel;
            }
            finally
            {
                _updatingModelCatalog = false;
            }

            UpdateReasoningEffortChoices(FindModelChoice(selectedModel));
            UpdateModelCatalogStatus(choices.Count);
        }

        private void UpdateModelCatalogStatus(int totalModelCount)
        {
            if (!_endpointModelsLoaded)
            {
                lblModelsStatus.Text = $"{AISettings.AvailableModels.Length} built-in presets. Endpoint discovery starts automatically.";
                return;
            }

            lblModelsStatus.Text = _endpointModels.Count == 0
                ? $"{AISettings.AvailableModels.Length} built-in presets available. No endpoint models discovered."
                : $"{AISettings.AvailableModels.Length} built-in + {_endpointModels.Count} endpoint; {totalModelCount} unique model(s).";
        }

        private ModelChoice FindModelChoice(string modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId))
                return null;

            foreach (var item in cboModel.Items.OfType<ModelChoice>())
            {
                if (string.Equals(item.ModelId, modelId.Trim(), StringComparison.OrdinalIgnoreCase))
                    return item;
            }

            return null;
        }

        private void UpdateReasoningEffortChoices(ModelChoice choice, string preferredReasoning = null)
        {
            if (cboReasoningEffort == null)
                return;

            var selectedReasoning = preferredReasoning ?? GetSelectedReasoningEffort();
            var efforts = choice != null && choice.ReasoningEfforts.Count > 0
                ? choice.ReasoningEfforts
                : InferReasoningEfforts(choice?.ModelId);

            _updatingReasoningEffort = true;
            try
            {
                cboReasoningEffort.Items.Clear();
                cboReasoningEffort.Items.Add(AutoReasoningEffort);
                foreach (var effort in efforts)
                    cboReasoningEffort.Items.Add(effort);

                var selectedIndex = string.IsNullOrWhiteSpace(selectedReasoning)
                    ? 0
                    : cboReasoningEffort.Items.IndexOf(selectedReasoning.ToLowerInvariant());
                cboReasoningEffort.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
            }
            finally
            {
                _updatingReasoningEffort = false;
            }

            if (choice != null && choice.ReasoningEfforts.Count > 0)
            {
                lblReasoningStatus.Text = EditorPanelLocalizer.Format("AISettings_EndpointReasoning", string.Join(", ", choice.ReasoningEfforts));
            }
            else if (efforts.Count > 0)
            {
                lblReasoningStatus.Text = EditorPanelLocalizer.Format("AISettings_DetectedReasoning", string.Join(", ", efforts));
            }
            else
            {
                lblReasoningStatus.Text = EditorPanelLocalizer.Text("AISettings_AutoReasoningOnly", "Auto only; this model does not advertise reasoning levels.");
            }
        }

        private static IReadOnlyList<string> InferReasoningEfforts(string modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId))
                return Array.Empty<string>();

            var normalized = modelId.Trim().ToLowerInvariant();
            var supportsReasoning = normalized.Contains("gpt-5") ||
                normalized.StartsWith("o1", StringComparison.Ordinal) ||
                normalized.StartsWith("o3", StringComparison.Ordinal) ||
                normalized.StartsWith("o4", StringComparison.Ordinal) ||
                normalized.Contains("codex");
            if (!supportsReasoning)
                return Array.Empty<string>();

            var efforts = new List<string> { "low", "medium", "high" };
            if (normalized.Contains("codex"))
                efforts.Add("xhigh");
            return efforts;
        }

        private void OnSettingsChanged(object sender, RoutedEventArgs e)
        {
            if (_initializing || _updatingReasoningEffort || _updatingModelCatalog)
                return;

            InvalidateConnectionTest();
        }

        private void InvalidateConnectionTest()
        {
            _connectionTested = false;
            btnSave.IsEnabled = false;
            lblStatus.Text = EditorPanelLocalizer.Text("AISettings_TestRequired", "Test connection required before saving.");
            lblStatus.Foreground = Brushes.Gray;
        }

        private void UpdateStatusFromSettings()
        {
            if (AISettings.IsConfigured)
            {
                lblStatus.Text = EditorPanelLocalizer.Format("AISettings_Configured", AISettings.GetStatusDescription());
                lblStatus.Foreground = Brushes.Green;
            }
            else
            {
                lblStatus.Text = EditorPanelLocalizer.Text("AISettings_NotConfigured", "Not configured.");
                lblStatus.Foreground = Brushes.Gray;
            }
        }

        private async void BtnTest_Click(object sender, RoutedEventArgs e)
        {
            btnTest.IsEnabled = false;
            lblStatus.Text = EditorPanelLocalizer.Text("AISettings_Testing", "Testing connection...");
            lblStatus.Foreground = Brushes.Gray;

            try
            {
                var baseUrl = txtBaseUrl.Text.Trim();
                var model = cboModel.Text.Trim();
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    lblStatus.Text = EditorPanelLocalizer.Text("AISettings_BaseUrlRequired", "Please enter an API base URL first.");
                    lblStatus.Foreground = Brushes.Red;
                    return;
                }

                if (string.IsNullOrWhiteSpace(model))
                {
                    lblStatus.Text = EditorPanelLocalizer.Text("AISettings_ModelRequired", "Please select or enter a model first.");
                    lblStatus.Foreground = Brushes.Red;
                    return;
                }

                var client = new OpenAICompatibleClient(new OpenAICompatibleOptions
                {
                    BaseUrl = baseUrl,
                    ApiKey = txtApiKey.Password.Trim(),
                    Model = model,
                    Protocol = GetSelectedProtocol(),
                    ReasoningEffort = GetSelectedReasoningEffort(),
                    StrictSchemas = chkStrictSchemas.IsChecked == true
                });

                bool success;
                using (client)
                {
                    success = await client.TestConnectionAsync();
                }

                if (success)
                {
                    lblStatus.Text = EditorPanelLocalizer.Text("AISettings_ConnectionSuccessful", "Connection successful!");
                    lblStatus.Foreground = Brushes.Green;
                    _connectionTested = true;
                    btnSave.IsEnabled = true;
                }
                else
                {
                    lblStatus.Text = EditorPanelLocalizer.Text("AISettings_ConnectionFailed", "Connection failed. Check the endpoint, model, and API key.");
                    lblStatus.Foreground = Brushes.Red;
                    _connectionTested = false;
                    btnSave.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = EditorPanelLocalizer.Format("AISettings_Error", ex.Message);
                lblStatus.Foreground = Brushes.Red;
                _connectionTested = false;
                btnSave.IsEnabled = false;
            }
            finally
            {
                btnTest.IsEnabled = true;
            }
        }

        private AIEndpointProtocol GetSelectedProtocol()
        {
            return cboApiDialect.SelectedIndex == 1
                ? AIEndpointProtocol.Responses
                : AIEndpointProtocol.ChatCompletions;
        }

        private string GetSelectedReasoningEffort()
        {
            var selected = cboReasoningEffort.SelectedItem?.ToString();
            return string.Equals(selected, AutoReasoningEffort, StringComparison.Ordinal)
                ? string.Empty
                : selected ?? string.Empty;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (!_connectionTested)
                return;

            AISettings.BaseUrl = txtBaseUrl.Text.Trim();
            AISettings.ApiKey = txtApiKey.Password.Trim();
            AISettings.Model = cboModel.Text.Trim();
            AISettings.Protocol = GetSelectedProtocol();
            AISettings.ReasoningEffort = GetSelectedReasoningEffort();
            AISettings.StrictSchemas = chkStrictSchemas.IsChecked == true;
            AISettings.AutoApplyCommands = chkAutoApply.IsChecked == true;

            DialogResult = true;
        }

        private sealed class ModelChoice
        {
            public ModelChoice(string modelId, string source, IReadOnlyList<string> reasoningEfforts = null)
            {
                ModelId = modelId;
                Source = source;
                ReasoningEfforts = reasoningEfforts ?? Array.Empty<string>();
            }

            public string ModelId { get; }
            public string Source { get; }
            public IReadOnlyList<string> ReasoningEfforts { get; }

            public override string ToString()
            {
                return $"{Source}: {ModelId}";
            }
        }
    }
}
