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
        private static string AutoReasoningEffort =>
            EditorPanelLocalizer.Text("AISettings_AutoReasoningEffort", "Auto (model default)");

        private readonly List<OpenAIModelInfo> _endpointModels = new List<OpenAIModelInfo>();
        private readonly Dictionary<string, string> _reasoningByModel = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private string _lastModelId;
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
            cboImageModel.AddHandler(TextBoxBase.TextChangedEvent,
                new TextChangedEventHandler((sender, args) => OnSettingsChanged(sender, args)));
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
                cboImageModel.Text = AISettings.ImageModel;
                cboApiDialect.SelectedIndex = AISettings.Protocol == AIEndpointProtocol.Responses ? 1 : 0;
                chkStrictSchemas.IsChecked = AISettings.StrictSchemas;
                chkAutoApply.IsChecked = AISettings.AutoApplyCommands;
                txtMaxToolTurns.Text = AISettings.MaxToolTurns.ToString();
                txtMaxOutputTokens.Text = AISettings.MaxOutputTokens.ToString();

                RebuildModelCatalog(Array.Empty<OpenAIModelInfo>());
                _lastModelId = AISettings.Model;
                _reasoningByModel[_lastModelId] = AISettings.GetReasoningEffortForModel(_lastModelId);
                UpdateReasoningEffortChoices(FindModelChoice(AISettings.Model), _reasoningByModel[_lastModelId]);
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

            ModelChoice selectedChoice = cboModel.SelectedItem as ModelChoice;
            if (selectedChoice != null)
            {
                RememberSelectedReasoning();
                _updatingModelCatalog = true;
                try
                {
                    cboModel.Text = selectedChoice.ModelId;
                }
                finally
                {
                    _updatingModelCatalog = false;
                }
            }

            var modelId = selectedChoice?.ModelId ?? cboModel.Text.Trim();
            if (AISettings.IsAstraModel(modelId))
                cboApiDialect.SelectedIndex = 1;
            _lastModelId = modelId;
            if (!_reasoningByModel.TryGetValue(modelId, out var remembered))
                remembered = AISettings.GetReasoningEffortForModel(modelId);
            UpdateReasoningEffortChoices(selectedChoice ?? FindModelChoice(modelId), remembered);
            InvalidateConnectionTest();
        }

        private void CboModel_TextChanged(object sender, EventArgs e)
        {
            if (_initializing || _updatingModelCatalog)
                return;

            var selectedChoice = cboModel.SelectedItem as ModelChoice;
            var enteredText = cboModel.Text.Trim();
            var modelId = selectedChoice != null &&
                (string.Equals(enteredText, selectedChoice.ModelId, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(enteredText, selectedChoice.ToString(), StringComparison.OrdinalIgnoreCase))
                    ? selectedChoice.ModelId
                    : enteredText;
            if (AISettings.IsAstraModel(modelId))
                cboApiDialect.SelectedIndex = 1;
            _lastModelId = modelId;
            if (!_reasoningByModel.TryGetValue(modelId, out var remembered))
                remembered = AISettings.GetReasoningEffortForModel(modelId);
            UpdateReasoningEffortChoices(
                string.Equals(modelId, selectedChoice?.ModelId, StringComparison.OrdinalIgnoreCase)
                    ? selectedChoice
                    : FindModelChoice(modelId),
                remembered);
            InvalidateConnectionTest();
        }

        private void CboImageModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingModelCatalog)
                return;

            if (cboImageModel.SelectedItem is ModelChoice choice)
            {
                _updatingModelCatalog = true;
                try
                {
                    cboImageModel.Text = choice.ModelId;
                }
                finally
                {
                    _updatingModelCatalog = false;
                }
            }

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
            var selectedImageModel = cboImageModel.Text.Trim();
            var choices = new List<ModelChoice>();
            var builtInSource = EditorPanelLocalizer.Text("AISettings_ModelSourceBuiltIn", "Built-in");
            var builtInEndpointSource = EditorPanelLocalizer.Text("AISettings_ModelSourceBuiltInEndpoint", "Built-in + endpoint");
            var endpointSource = EditorPanelLocalizer.Text("AISettings_ModelSourceEndpoint", "Endpoint");
            var imageChoices = AISettings.AvailableImageModels
                .Select(model => new ModelChoice(model, builtInSource))
                .ToList();

            foreach (var model in AISettings.AvailableModels)
                choices.Add(new ModelChoice(model, builtInSource));

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
                        builtInEndpointSource,
                        endpointModel.ReasoningEfforts);
                }
                else
                {
                    choices.Add(new ModelChoice(
                        endpointModel.Id,
                        endpointSource,
                        endpointModel.ReasoningEfforts));
                }

                if (!imageChoices.Any(choice => string.Equals(choice.ModelId, endpointModel.Id, StringComparison.OrdinalIgnoreCase)))
                    imageChoices.Add(new ModelChoice(endpointModel.Id, endpointSource, endpointModel.ReasoningEfforts));
            }

            _updatingModelCatalog = true;
            try
            {
                cboModel.Items.Clear();
                foreach (var choice in choices)
                    cboModel.Items.Add(choice);

                cboImageModel.Items.Clear();
                foreach (var choice in imageChoices)
                    cboImageModel.Items.Add(choice);

                var selectedIndex = choices.FindIndex(choice =>
                    string.Equals(choice.ModelId, selectedModel, StringComparison.OrdinalIgnoreCase));
                cboModel.SelectedIndex = selectedIndex;
                cboModel.Text = selectedIndex >= 0 ? choices[selectedIndex].ModelId : selectedModel;

                var selectedImageIndex = imageChoices.FindIndex(choice =>
                    string.Equals(choice.ModelId, selectedImageModel, StringComparison.OrdinalIgnoreCase));
                cboImageModel.SelectedIndex = selectedImageIndex;
                cboImageModel.Text = selectedImageIndex >= 0 ? imageChoices[selectedImageIndex].ModelId : selectedImageModel;
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
                lblModelsStatus.Text = EditorPanelLocalizer.Format(
                    "AISettings_BuiltInPresetsPending", AISettings.AvailableModels.Length);
                return;
            }

            lblModelsStatus.Text = _endpointModels.Count == 0
                ? EditorPanelLocalizer.Format("AISettings_BuiltInPresetsOnly", AISettings.AvailableModels.Length)
                : EditorPanelLocalizer.Format("AISettings_ModelCatalogSummary", AISettings.AvailableModels.Length,
                    _endpointModels.Count, totalModelCount);
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
            var detectedEfforts = choice != null && choice.ReasoningEfforts.Count > 0
                ? choice.ReasoningEfforts
                : InferReasoningEfforts(choice?.ModelId ?? cboModel.Text);
            var efforts = AISettings.AvailableReasoningEfforts
                .Where(effort => detectedEfforts.Contains(effort, StringComparer.OrdinalIgnoreCase))
                .ToList();

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
            cboReasoningEffort.IsEnabled = efforts.Count > 0;

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

            if (AISettings.IsAstraModel(modelId))
                return AISettings.AstraReasoningEfforts;

            var normalized = modelId.Trim().ToLowerInvariant();
            var supportsReasoning = normalized.Contains("gpt-5") ||
                normalized.StartsWith("o1", StringComparison.Ordinal) ||
                normalized.StartsWith("o3", StringComparison.Ordinal) ||
                normalized.StartsWith("o4", StringComparison.Ordinal) ||
                normalized.Contains("codex") ||
                normalized.Contains("claude-opus-5") ||
                normalized.Contains("claude-opus-4.8") ||
                normalized.Contains("claude-sonnet-5") ||
                normalized.Contains("grok-4.5") ||
                normalized.Contains("glm-5.2") ||
                normalized.Contains("deepseek-v4") ||
                normalized.Contains("gemini-3.6-flash") ||
                normalized.Contains("muse-spark-1.2") ||
                normalized.Contains("kimi-k3");
            if (!supportsReasoning)
                return Array.Empty<string>();

            var efforts = new List<string> { "minimal", "low", "medium", "high" };
            efforts.Add("xhigh");
            return efforts;
        }

        private void RememberSelectedReasoning()
        {
            if (string.IsNullOrWhiteSpace(_lastModelId) || _updatingReasoningEffort)
                return;
            _reasoningByModel[_lastModelId] = GetSelectedReasoningEffort();
        }

        private void OnSettingsChanged(object sender, RoutedEventArgs e)
        {
            if (_initializing || _updatingReasoningEffort || _updatingModelCatalog)
                return;

            if (ReferenceEquals(sender, cboReasoningEffort))
                RememberSelectedReasoning();
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
                var model = GetSelectedModelId();
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

                if (!int.TryParse(txtMaxToolTurns.Text, out var maxToolTurns) || maxToolTurns < 1 || maxToolTurns > 200 ||
                    !int.TryParse(txtMaxOutputTokens.Text, out var maxOutputTokens) || maxOutputTokens < 256 || maxOutputTokens > 1000000)
                {
                    lblStatus.Text = EditorPanelLocalizer.Text("AISettings_InvalidLimits", "Enter valid limits: 1–200 tool turns and 256–1,000,000 output tokens.");
                    lblStatus.Foreground = Brushes.Red;
                    return;
                }

                var client = new OpenAICompatibleClient(new OpenAICompatibleOptions
                {
                    BaseUrl = baseUrl,
                    ApiKey = txtApiKey.Password.Trim(),
                    Model = AISettings.NormalizeModelId(baseUrl, model),
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
                    lblStatus.Text = string.IsNullOrWhiteSpace(client.LastTestError)
                        ? EditorPanelLocalizer.Text("AISettings_ConnectionFailed", "Connection failed. Check the endpoint, model, and API key.")
                        : EditorPanelLocalizer.Format("AISettings_ConnectionFailedDetail", client.LastTestError);
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
            return AISettings.IsAstraModel(GetSelectedModelId()) || cboApiDialect.SelectedIndex == 1
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

        private string GetSelectedModelId()
        {
            var text = cboModel.Text.Trim();
            return cboModel.SelectedItem is ModelChoice choice &&
                (string.Equals(text, choice.ModelId, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(text, choice.ToString(), StringComparison.OrdinalIgnoreCase))
                ? choice.ModelId
                : text;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (!_connectionTested)
                return;

            AISettings.BaseUrl = txtBaseUrl.Text.Trim();
            AISettings.ApiKey = txtApiKey.Password.Trim();
            AISettings.Model = AISettings.NormalizeModelId(AISettings.BaseUrl, GetSelectedModelId());
            AISettings.ImageModel = cboImageModel.Text.Trim();
            AISettings.Protocol = GetSelectedProtocol();
            AISettings.ReasoningEffort = GetSelectedReasoningEffort();
            AISettings.SetReasoningEffortForModel(AISettings.Model, GetSelectedReasoningEffort());
            AISettings.StrictSchemas = chkStrictSchemas.IsChecked == true;
            AISettings.AutoApplyCommands = chkAutoApply.IsChecked == true;
            if (int.TryParse(txtMaxToolTurns.Text, out var maxToolTurns))
                AISettings.MaxToolTurns = maxToolTurns;
            if (int.TryParse(txtMaxOutputTokens.Text, out var maxOutputTokens))
                AISettings.MaxOutputTokens = maxOutputTokens;

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
