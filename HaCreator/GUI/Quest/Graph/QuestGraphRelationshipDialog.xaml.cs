#nullable enable

using MapleLib.WzLib.WzStructure.Data.QuestStructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace HaCreator.GUI.Quest.Graph;

public enum QuestGraphRelationshipDraftKind
{
    StartNextQuest,
    CompletionNextQuest,
    StartRequirement,
    CompletionRequirement,
}

public sealed record QuestGraphRelationshipDraft(
    QuestGraphRelationshipDraftKind Kind,
    int TargetQuestId,
    QuestStateType QuestState);

public partial class QuestGraphRelationshipDialog : Window
{
    private sealed record DisplayOption<T>(T Value, string Label)
    {
        public override string ToString() => Label;
    }

    private readonly QuestEditorModel _sourceQuest;

    public QuestGraphRelationshipDialog(
        QuestEditorModel sourceQuest,
        IEnumerable<QuestEditorModel> quests,
        QuestGraphRelationshipDraft? initialValue = null,
        bool lockRelationshipType = false)
    {
        _sourceQuest = sourceQuest ?? throw new ArgumentNullException(nameof(sourceQuest));
        InitializeComponent();

        SourceQuestText.Text = FormatQuest(sourceQuest);
        RelationshipTypeBox.ItemsSource = CreateKindOptions();
        RelationshipTypeBox.IsEnabled = !lockRelationshipType;
        DisplayOption<int>[] targetOptions = (quests ?? [])
            .Where(quest => quest != null && quest.Id != sourceQuest.Id)
            .OrderBy(quest => quest.Id)
            .Select(quest => new DisplayOption<int>(quest.Id, FormatQuest(quest)))
            .ToArray();
        if (initialValue is { TargetQuestId: > 0 } && targetOptions.All(option => option.Value != initialValue.TargetQuestId))
        {
            targetOptions = targetOptions
                .Append(new DisplayOption<int>(
                    initialValue.TargetQuestId,
                    $"{initialValue.TargetQuestId} · {QuestTextExtension.Get("QuestEditor_GraphDangling")}"))
                .ToArray();
        }
        TargetQuestBox.ItemsSource = targetOptions;
        StateBox.ItemsSource = CreateStateOptions();

        QuestGraphRelationshipDraft value = initialValue ?? new(
            QuestGraphRelationshipDraftKind.CompletionNextQuest,
            0,
            QuestStateType.Completed);
        SelectValue(RelationshipTypeBox, value.Kind);
        SelectValue(TargetQuestBox, value.TargetQuestId);
        SelectValue(StateBox, value.QuestState);
        UpdateStateVisibility();
        UpdatePreview();
    }

    public QuestGraphRelationshipDraft? Result { get; private set; }

    private static DisplayOption<QuestGraphRelationshipDraftKind>[] CreateKindOptions() =>
    [
        new(QuestGraphRelationshipDraftKind.CompletionNextQuest, QuestTextExtension.Get("QuestEditor_GraphRelationshipNextCompletion")),
        new(QuestGraphRelationshipDraftKind.StartNextQuest, QuestTextExtension.Get("QuestEditor_GraphRelationshipNextStart")),
        new(QuestGraphRelationshipDraftKind.StartRequirement, QuestTextExtension.Get("QuestEditor_GraphRelationshipRequirementStart")),
        new(QuestGraphRelationshipDraftKind.CompletionRequirement, QuestTextExtension.Get("QuestEditor_GraphRelationshipRequirementCompletion")),
    ];

    private static DisplayOption<QuestStateType>[] CreateStateOptions() =>
    [
        new(QuestStateType.Not_Started, QuestTextExtension.Get("QuestEditor_GraphQuestStateNotStarted")),
        new(QuestStateType.Started, QuestTextExtension.Get("QuestEditor_GraphQuestStateStarted")),
        new(QuestStateType.Completed, QuestTextExtension.Get("QuestEditor_GraphQuestStateCompleted")),
        new(QuestStateType.PartyQuest, QuestTextExtension.Get("QuestEditor_GraphQuestStatePartyQuest")),
        new(QuestStateType.No, QuestTextExtension.Get("QuestEditor_GraphQuestStateNo")),
        new(QuestStateType.Impossible, QuestTextExtension.Get("QuestEditor_GraphQuestStateImpossible")),
    ];

    private static void SelectValue<T>(ComboBox comboBox, T value)
    {
        comboBox.SelectedItem = comboBox.Items
            .Cast<DisplayOption<T>>()
            .FirstOrDefault(option => EqualityComparer<T>.Default.Equals(option.Value, value));
    }

    private static string FormatQuest(QuestEditorModel quest) =>
        string.IsNullOrWhiteSpace(quest.Name) ? quest.Id.ToString() : $"{quest.Id} · {quest.Name}";

    private bool IsRequirement => SelectedKind is
        QuestGraphRelationshipDraftKind.StartRequirement or
        QuestGraphRelationshipDraftKind.CompletionRequirement;

    private QuestGraphRelationshipDraftKind? SelectedKind =>
        (RelationshipTypeBox.SelectedItem as DisplayOption<QuestGraphRelationshipDraftKind>)?.Value;

    private int? SelectedTarget =>
        (TargetQuestBox.SelectedItem as DisplayOption<int>)?.Value;

    private QuestStateType? SelectedState =>
        (StateBox.SelectedItem as DisplayOption<QuestStateType>)?.Value;

    private void RelationshipTypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateStateVisibility();
        UpdatePreview();
    }

    private void TargetQuestBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdatePreview();
    private void StateBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdatePreview();

    private void UpdateStateVisibility()
    {
        Visibility visibility = IsRequirement ? Visibility.Visible : Visibility.Collapsed;
        StateLabel.Visibility = visibility;
        StateBox.Visibility = visibility;
    }

    private void UpdatePreview()
    {
        ApplyButton.IsEnabled = SelectedKind.HasValue && SelectedTarget.HasValue && (!IsRequirement || SelectedState.HasValue);
        string target = TargetQuestBox.SelectedItem?.ToString() ?? QuestTextExtension.Get("QuestEditor_GraphRelationshipSelectTarget");
        string kind = RelationshipTypeBox.SelectedItem?.ToString() ?? string.Empty;
        string state = IsRequirement ? $" · {StateBox.SelectedItem}" : string.Empty;
        PreviewText.Text = QuestTextExtension.Get(
            "QuestEditor_GraphRelationshipPreviewFormat",
            FormatQuest(_sourceQuest),
            target,
            kind,
            state);
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (!SelectedKind.HasValue || !SelectedTarget.HasValue || (IsRequirement && !SelectedState.HasValue))
            return;

        Result = new QuestGraphRelationshipDraft(
            SelectedKind.Value,
            SelectedTarget.Value,
            IsRequirement ? SelectedState!.Value : QuestStateType.Completed);
        DialogResult = true;
    }
}
