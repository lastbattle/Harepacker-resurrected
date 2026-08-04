#nullable enable

using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace HaCreator.GUI.Quest.Graph;

public enum QuestGraphRelationshipErrorCode
{
    None,
    InvalidSource,
    InvalidTarget,
    TargetNotLoaded,
    SelfLink,
    Duplicate,
    Cycle,
    StaleAddress,
    UnsupportedRawShape,
    ApplyFailed,
}

public sealed class QuestGraphRelationshipResult
{
    private QuestGraphRelationshipResult(
        QuestGraphRelationshipOperation? operation,
        QuestGraphRelationshipErrorCode errorCode,
        string error)
    {
        Operation = operation;
        ErrorCode = errorCode;
        Error = error;
    }

    public bool Success => Operation != null;
    public QuestGraphRelationshipOperation? Operation { get; }
    public QuestGraphRelationshipErrorCode ErrorCode { get; }
    public string Error { get; }

    internal static QuestGraphRelationshipResult Ok(QuestGraphRelationshipOperation operation) =>
        new(operation, QuestGraphRelationshipErrorCode.None, string.Empty);

    internal static QuestGraphRelationshipResult Fail(QuestGraphRelationshipErrorCode code, string error) =>
        new(null, code, error);
}

public sealed class QuestGraphRelationshipOperation
{
    private readonly Action _apply;
    private readonly Action _undo;
    private bool _isApplied = true;

    internal QuestGraphRelationshipOperation(
        QuestGraphRelationshipAddress address,
        WzSubProperty changedRawRoot,
        Action apply,
        Action undo,
        bool isApplied = false)
    {
        Address = address;
        ChangedRawRoot = changedRawRoot;
        _apply = apply;
        _undo = undo;
        _isApplied = isApplied;
    }

    public QuestGraphRelationshipAddress Address { get; }
    public WzSubProperty ChangedRawRoot { get; }
    public bool IsApplied => _isApplied;

    public bool TryUndo(out string error) => TryTransition(apply: false, out error);
    public bool TryRedo(out string error) => TryTransition(apply: true, out error);

    private bool TryTransition(bool apply, out string error)
    {
        error = string.Empty;
        if (apply == _isApplied)
            return true;

        try
        {
            (apply ? _apply : _undo)();
            _isApplied = apply;
            return true;
        }
        catch (Exception ex)
        {
            try
            {
                (apply ? _undo : _apply)();
            }
            catch
            {
                // Preserve the original failure; callers receive a failed
                // transition and do not advance the history cursor.
            }
            error = ex.Message;
            return false;
        }
    }
}

/// <summary>
/// Applies graph relationship changes to the editor model and the exact raw
/// Act/Check relationship property. It never invokes the whole-quest writer.
/// </summary>
public static class QuestGraphRelationshipCommand
{
    public static QuestGraphRelationshipResult TryAdd(
        QuestEditorModel source,
        WzSubProperty rawRoot,
        QuestGraphRelationshipKind kind,
        QuestGraphRelationshipPhase phase,
        int targetQuestId,
        QuestStateType questState,
        IEnumerable<QuestEditorModel> quests)
    {
        QuestGraphRelationshipResult? rawValidation = ValidateRawRoot(source, rawRoot);
        if (rawValidation != null)
            return rawValidation;
        QuestGraphRelationshipResult? validation = ValidateNewRelationship(
            source, kind, phase, targetQuestId, questState, quests, ignoredAddress: null);
        if (validation != null)
            return validation;

        return kind switch
        {
            QuestGraphRelationshipKind.NextQuest => AddNextQuest(source, rawRoot, phase, targetQuestId),
            QuestGraphRelationshipKind.CheckQuestRequirement => AddRequirement(source, rawRoot, phase, targetQuestId, questState),
            _ => QuestGraphRelationshipResult.Fail(QuestGraphRelationshipErrorCode.UnsupportedRawShape, "Unsupported relationship kind."),
        };
    }

    public static QuestGraphRelationshipResult TryReplace(
        QuestEditorModel source,
        WzSubProperty rawRoot,
        QuestGraphRelationshipAddress address,
        int targetQuestId,
        QuestStateType questState,
        IEnumerable<QuestEditorModel> quests)
    {
        QuestGraphRelationshipResult? rawValidation = ValidateRawRoot(source, rawRoot);
        if (rawValidation != null)
            return rawValidation;
        if (address.OwnerQuestId != source?.Id)
            return QuestGraphRelationshipResult.Fail(QuestGraphRelationshipErrorCode.StaleAddress, "The graph relationship address no longer matches the selected quest.");

        QuestGraphRelationshipResult? validation = ValidateNewRelationship(
            source, address.Kind, address.Phase, targetQuestId, questState, quests, address);
        if (validation != null)
            return validation;

        return address.Kind switch
        {
            QuestGraphRelationshipKind.NextQuest => ReplaceNextQuest(source, rawRoot, address, targetQuestId),
            QuestGraphRelationshipKind.CheckQuestRequirement => ReplaceRequirement(source, rawRoot, address, targetQuestId, questState),
            _ => QuestGraphRelationshipResult.Fail(QuestGraphRelationshipErrorCode.UnsupportedRawShape, "Unsupported relationship kind."),
        };
    }

    public static QuestGraphRelationshipResult TryRemove(
        QuestEditorModel source,
        WzSubProperty rawRoot,
        QuestGraphRelationshipAddress address)
    {
        QuestGraphRelationshipResult? rawValidation = ValidateRawRoot(source, rawRoot);
        if (rawValidation != null)
            return rawValidation;
        if (source == null || address.OwnerQuestId != source.Id)
            return QuestGraphRelationshipResult.Fail(QuestGraphRelationshipErrorCode.StaleAddress, "The graph relationship address no longer matches the selected quest.");
        if (!Enum.IsDefined(address.Kind) || !Enum.IsDefined(address.Phase))
            return QuestGraphRelationshipResult.Fail(QuestGraphRelationshipErrorCode.StaleAddress, "The relationship address contains an unsupported kind or phase.");

        return address.Kind switch
        {
            QuestGraphRelationshipKind.NextQuest => RemoveNextQuest(source, rawRoot, address),
            QuestGraphRelationshipKind.CheckQuestRequirement => RemoveRequirement(source, rawRoot, address),
            _ => QuestGraphRelationshipResult.Fail(QuestGraphRelationshipErrorCode.UnsupportedRawShape, "Unsupported relationship kind."),
        };
    }

    private static QuestGraphRelationshipResult? ValidateNewRelationship(
        QuestEditorModel? source,
        QuestGraphRelationshipKind kind,
        QuestGraphRelationshipPhase phase,
        int targetQuestId,
        QuestStateType questState,
        IEnumerable<QuestEditorModel>? quests,
        QuestGraphRelationshipAddress? ignoredAddress)
    {
        if (source == null || source.Id <= 0)
            return QuestGraphRelationshipResult.Fail(QuestGraphRelationshipErrorCode.InvalidSource, "The source quest is invalid.");
        if (!Enum.IsDefined(kind) || !Enum.IsDefined(phase))
            return QuestGraphRelationshipResult.Fail(QuestGraphRelationshipErrorCode.InvalidTarget, "The relationship kind or phase is invalid.");
        if (targetQuestId <= 0)
            return QuestGraphRelationshipResult.Fail(QuestGraphRelationshipErrorCode.InvalidTarget, "Select a valid target quest.");
        if (targetQuestId == source.Id)
            return QuestGraphRelationshipResult.Fail(QuestGraphRelationshipErrorCode.SelfLink, "A quest cannot link to itself.");

        QuestEditorModel[] allQuests = quests?.Where(quest => quest != null).ToArray() ?? [];
        if (allQuests.GroupBy(quest => quest.Id).Any(group => group.Count() > 1))
            return QuestGraphRelationshipResult.Fail(QuestGraphRelationshipErrorCode.InvalidSource, "Duplicate quest IDs make relationship validation ambiguous.");
        if (!allQuests.Any(quest => quest.Id == source.Id))
            return QuestGraphRelationshipResult.Fail(QuestGraphRelationshipErrorCode.InvalidSource, "The source quest is not in the loaded quest collection.");
        if (!allQuests.Any(quest => quest.Id == targetQuestId))
            return QuestGraphRelationshipResult.Fail(QuestGraphRelationshipErrorCode.TargetNotLoaded, "The target quest is not loaded.");
        if (kind == QuestGraphRelationshipKind.CheckQuestRequirement && !Enum.IsDefined(questState))
            return QuestGraphRelationshipResult.Fail(QuestGraphRelationshipErrorCode.InvalidTarget, "The required quest state is invalid.");

        IEnumerable<QuestGraphRelationshipAddress> existing = EnumerateEditableRelationships(allQuests);
        if (ignoredAddress == null && kind == QuestGraphRelationshipKind.NextQuest &&
            GetActs(source, phase).Any(act => act?.ActType == QuestEditorActType.NextQuest))
        {
            return QuestGraphRelationshipResult.Fail(QuestGraphRelationshipErrorCode.Duplicate, "That phase already contains nextQuest.");
        }
        bool duplicate = existing.Any(item =>
            !SameAddress(item, ignoredAddress) &&
            item.OwnerQuestId == source.Id &&
            item.Kind == kind &&
            item.Phase == phase &&
            (kind == QuestGraphRelationshipKind.NextQuest || item.TargetQuestId == targetQuestId));
        if (duplicate)
            return QuestGraphRelationshipResult.Fail(QuestGraphRelationshipErrorCode.Duplicate, "That phase already contains this relationship.");

        if (WouldCreateCycle(allQuests, source.Id, targetQuestId, ignoredAddress))
            return QuestGraphRelationshipResult.Fail(QuestGraphRelationshipErrorCode.Cycle, "The relationship would create a quest cycle.");

        return null;
    }

    private static QuestGraphRelationshipResult? ValidateRawRoot(QuestEditorModel? source, WzSubProperty? rawRoot)
    {
        if (source == null || source.Id <= 0)
            return QuestGraphRelationshipResult.Fail(QuestGraphRelationshipErrorCode.InvalidSource, "The source quest is invalid.");
        if (rawRoot == null || !string.Equals(rawRoot.Name, source.Id.ToString(), StringComparison.Ordinal))
            return QuestGraphRelationshipResult.Fail(QuestGraphRelationshipErrorCode.UnsupportedRawShape, "The raw relationship root does not belong to the source quest.");
        return null;
    }

    public static IEnumerable<QuestGraphRelationshipAddress> EnumerateEditableRelationships(IEnumerable<QuestEditorModel> quests)
    {
        foreach (QuestEditorModel quest in quests ?? [])
        {
            foreach (QuestGraphRelationshipAddress address in EnumeratePhase(quest, QuestGraphRelationshipPhase.Start))
                yield return address;
            foreach (QuestGraphRelationshipAddress address in EnumeratePhase(quest, QuestGraphRelationshipPhase.End))
                yield return address;
        }
    }

    private static IEnumerable<QuestGraphRelationshipAddress> EnumeratePhase(QuestEditorModel quest, QuestGraphRelationshipPhase phase)
    {
        ObservableCollection<QuestEditorActInfoModel> acts = GetActs(quest, phase);
        for (int index = 0; index < acts.Count; index++)
        {
            QuestEditorActInfoModel act = acts[index];
            if (act?.ActType == QuestEditorActType.NextQuest && act.Amount is > 0 and <= int.MaxValue)
                yield return new QuestGraphRelationshipAddress(quest.Id, QuestGraphRelationshipKind.NextQuest, phase, (int)act.Amount, modelIndex: index);
        }

        ObservableCollection<QuestEditorCheckInfoModel> checks = GetChecks(quest, phase);
        for (int modelIndex = 0; modelIndex < checks.Count; modelIndex++)
        {
            QuestEditorCheckInfoModel check = checks[modelIndex];
            if (check?.CheckType != QuestEditorCheckType.Quest)
                continue;
            for (int requirementIndex = 0; requirementIndex < check.QuestReqs.Count; requirementIndex++)
            {
                QuestEditorQuestReqModel requirement = check.QuestReqs[requirementIndex];
                if (requirement != null && requirement.QuestId > 0)
                {
                    yield return new QuestGraphRelationshipAddress(
                        quest.Id,
                        QuestGraphRelationshipKind.CheckQuestRequirement,
                        phase,
                        requirement.QuestId,
                        requirement.QuestState,
                        modelIndex,
                        requirementIndex);
                }
            }
        }
    }

    private static bool WouldCreateCycle(
        QuestEditorModel[] quests,
        int sourceId,
        int targetId,
        QuestGraphRelationshipAddress? ignoredAddress)
    {
        var adjacency = quests.ToDictionary(quest => quest.Id, _ => new List<int>());
        foreach (QuestGraphRelationshipAddress item in EnumerateEditableRelationships(quests))
        {
            if (SameAddress(item, ignoredAddress) || !adjacency.ContainsKey(item.OwnerQuestId))
                continue;
            adjacency[item.OwnerQuestId].Add(item.TargetQuestId);
        }
        adjacency[sourceId].Add(targetId);

        var pending = new Stack<int>();
        var visited = new HashSet<int>();
        pending.Push(targetId);
        while (pending.Count > 0)
        {
            int current = pending.Pop();
            if (current == sourceId)
                return true;
            if (!visited.Add(current) || !adjacency.TryGetValue(current, out List<int>? next))
                continue;
            foreach (int value in next)
                pending.Push(value);
        }
        return false;
    }

    private static bool SameAddress(QuestGraphRelationshipAddress item, QuestGraphRelationshipAddress? other) =>
        other != null && item.OwnerQuestId == other.OwnerQuestId && item.Kind == other.Kind &&
        item.Phase == other.Phase && item.ModelIndex == other.ModelIndex &&
        item.RequirementIndex == other.RequirementIndex;

    private static QuestGraphRelationshipResult AddNextQuest(
        QuestEditorModel source,
        WzSubProperty rawRoot,
        QuestGraphRelationshipPhase phase,
        int targetId)
    {
        if (!TryGetOrCreatePhase(rawRoot, phase, out WzSubProperty? rawPhase, out bool createdPhase, out string error))
            return QuestGraphRelationshipResult.Fail(QuestGraphRelationshipErrorCode.UnsupportedRawShape, error);
        if (rawPhase!["nextQuest"] != null)
            return QuestGraphRelationshipResult.Fail(QuestGraphRelationshipErrorCode.Duplicate, "The raw phase already contains nextQuest.");

        ObservableCollection<QuestEditorActInfoModel> acts = GetActs(source, phase);
        QuestEditorActInfoModel model = new() { ActType = QuestEditorActType.NextQuest, Amount = targetId };
        WzIntProperty raw = new("nextQuest", targetId);
        int modelIndex = acts.Count;
        int rawIndex = rawPhase.WzProperties.Count;

        Action apply = () =>
        {
            Ensure(!acts.Contains(model) && !rawPhase.WzProperties.Contains(raw), "The nextQuest add operation is stale.");
            if (createdPhase && !rawRoot.WzProperties.Contains(rawPhase!))
                rawRoot.WzProperties.Add(rawPhase!);
            if (!acts.Contains(model)) acts.Insert(Math.Min(modelIndex, acts.Count), model);
            if (!rawPhase.WzProperties.Contains(raw)) rawPhase.WzProperties.Insert(Math.Min(rawIndex, rawPhase.WzProperties.Count), raw);
        };
        Action undo = () =>
        {
            Ensure(acts.Contains(model) && model.Amount == targetId && rawPhase.WzProperties.Contains(raw) && raw.Value == targetId,
                "The nextQuest add operation changed outside graph history.");
            acts.Remove(model);
            rawPhase.WzProperties.Remove(raw);
            if (createdPhase && rawPhase.WzProperties.Count == 0) rawRoot.WzProperties.Remove(rawPhase);
        };

        try
        {
            apply();
            QuestGraphRelationshipAddress address = new(source.Id, QuestGraphRelationshipKind.NextQuest, phase, targetId, modelIndex: modelIndex);
            return QuestGraphRelationshipResult.Ok(new QuestGraphRelationshipOperation(address, rawRoot, apply, undo, isApplied: true));
        }
        catch (Exception ex)
        {
            try { undo(); } catch { }
            return QuestGraphRelationshipResult.Fail(QuestGraphRelationshipErrorCode.ApplyFailed, ex.Message);
        }
    }

    private static QuestGraphRelationshipResult ReplaceNextQuest(
        QuestEditorModel source,
        WzSubProperty rawRoot,
        QuestGraphRelationshipAddress address,
        int targetId)
    {
        if (!TryResolveNextQuest(source, rawRoot, address, out QuestEditorActInfoModel? model, out WzIntProperty? raw, out string error))
            return QuestGraphRelationshipResult.Fail(QuestGraphRelationshipErrorCode.StaleAddress, error);
        long oldModel = model!.Amount;
        int oldRaw = raw!.Value;
        Action apply = () =>
        {
            Ensure(model.Amount == oldModel && raw.Value == oldRaw, "The nextQuest edit operation is stale.");
            model.Amount = targetId;
            raw.Value = targetId;
        };
        Action undo = () =>
        {
            Ensure(model.Amount == targetId && raw.Value == targetId, "The nextQuest edit operation changed outside graph history.");
            model.Amount = oldModel;
            raw.Value = oldRaw;
        };
        return ApplyOperation(new(address with { TargetQuestId = targetId }, rawRoot, apply, undo));
    }

    private static QuestGraphRelationshipResult RemoveNextQuest(
        QuestEditorModel source,
        WzSubProperty rawRoot,
        QuestGraphRelationshipAddress address)
    {
        if (!TryResolveNextQuest(source, rawRoot, address, out QuestEditorActInfoModel? model, out WzIntProperty? raw, out string error))
            return QuestGraphRelationshipResult.Fail(QuestGraphRelationshipErrorCode.StaleAddress, error);
        ObservableCollection<QuestEditorActInfoModel> acts = GetActs(source, address.Phase);
        WzSubProperty rawPhase = (WzSubProperty)raw!.Parent;
        int modelIndex = acts.IndexOf(model!);
        int rawIndex = rawPhase.WzProperties.IndexOf(raw);
        int phaseIndex = rawRoot.WzProperties.IndexOf(rawPhase);
        bool removeEmptyPhase = rawPhase.WzProperties.Count == 1 && acts.Count == 1;
        Action apply = () =>
        {
            Ensure(acts.Contains(model!) && rawPhase.WzProperties.Contains(raw) && model!.Amount == address.TargetQuestId && raw.Value == address.TargetQuestId,
                "The nextQuest remove operation is stale.");
            acts.Remove(model!);
            rawPhase.WzProperties.Remove(raw);
            if (removeEmptyPhase && rawPhase.WzProperties.Count == 0)
                rawRoot.WzProperties.Remove(rawPhase);
        };
        Action undo = () =>
        {
            Ensure(!acts.Contains(model!) && !rawPhase.WzProperties.Contains(raw) &&
                   !acts.Any(item => item?.ActType == QuestEditorActType.NextQuest),
                "The nextQuest remove operation changed outside graph history.");
            Ensure(!rawRoot.WzProperties.Any(item => item != rawPhase && string.Equals(item.Name, PhaseName(address.Phase), StringComparison.OrdinalIgnoreCase)),
                "The raw nextQuest phase was replaced outside graph history.");
            if (removeEmptyPhase && !rawRoot.WzProperties.Contains(rawPhase))
                rawRoot.WzProperties.Insert(Math.Min(phaseIndex, rawRoot.WzProperties.Count), rawPhase);
            if (!acts.Contains(model!)) acts.Insert(Math.Min(modelIndex, acts.Count), model!);
            if (!rawPhase.WzProperties.Contains(raw)) rawPhase.WzProperties.Insert(Math.Min(rawIndex, rawPhase.WzProperties.Count), raw);
        };
        return ApplyOperation(new(address, rawRoot, apply, undo));
    }

    private static QuestGraphRelationshipResult AddRequirement(
        QuestEditorModel source,
        WzSubProperty rawRoot,
        QuestGraphRelationshipPhase phase,
        int targetId,
        QuestStateType state)
    {
        ObservableCollection<QuestEditorCheckInfoModel> checks = GetChecks(source, phase);
        QuestEditorCheckInfoModel? check = checks.FirstOrDefault(item => item.CheckType == QuestEditorCheckType.Quest);
        if (checks.Count(item => item.CheckType == QuestEditorCheckType.Quest) > 1)
            return QuestGraphRelationshipResult.Fail(QuestGraphRelationshipErrorCode.UnsupportedRawShape, "The phase contains multiple quest requirement groups.");

        if (!TryGetOrCreatePhase(rawRoot, phase, out WzSubProperty? rawPhase, out bool createdPhase, out string error))
            return QuestGraphRelationshipResult.Fail(QuestGraphRelationshipErrorCode.UnsupportedRawShape, error);

        if (!TryGetUniqueChild(rawPhase!, "quest", out WzImageProperty? existingQuest, out error))
            return QuestGraphRelationshipResult.Fail(QuestGraphRelationshipErrorCode.UnsupportedRawShape, error);
        if (existingQuest != null && existingQuest is not WzSubProperty)
            return QuestGraphRelationshipResult.Fail(QuestGraphRelationshipErrorCode.UnsupportedRawShape, "The raw quest relationship has an unsupported type.");

        WzSubProperty? rawQuest = existingQuest as WzSubProperty;
        bool createdQuest = rawQuest == null;
        if (rawQuest == null)
        {
            if (check != null && check.QuestReqs.Count > 0)
                return QuestGraphRelationshipResult.Fail(QuestGraphRelationshipErrorCode.UnsupportedRawShape, "The model and raw quest requirements do not match.");
            rawQuest = new WzSubProperty("quest");
            rawPhase!.AddProperty(rawQuest);
        }
        else if (!TryValidateRequirementParity(check, rawQuest, out error))
        {
            return QuestGraphRelationshipResult.Fail(QuestGraphRelationshipErrorCode.UnsupportedRawShape, error);
        }

        bool createdCheck = check == null;
        check ??= new QuestEditorCheckInfoModel(QuestEditorCheckType.Quest);
        int checkIndex = createdCheck ? checks.Count : checks.IndexOf(check);
        QuestEditorQuestReqModel model = new() { QuestId = targetId, QuestState = state };
        int requirementIndex = check.QuestReqs.Count;
        string rawName = NextNumericName(rawQuest!);
        WzSubProperty rawRequirement = CreateRawRequirement(rawName, targetId, state);
        int rawIndex = rawQuest!.WzProperties.Count;

        Action apply = () =>
        {
            Ensure(!check.QuestReqs.Contains(model) && !rawQuest.WzProperties.Contains(rawRequirement),
                "The quest requirement add operation is stale.");
            if (createdPhase && !rawRoot.WzProperties.Contains(rawPhase!))
                rawRoot.WzProperties.Add(rawPhase!);
            if (createdQuest && !rawPhase!.WzProperties.Contains(rawQuest))
                rawPhase.WzProperties.Add(rawQuest);
            if (createdCheck && !checks.Contains(check)) checks.Insert(Math.Min(checkIndex, checks.Count), check);
            if (!check.QuestReqs.Contains(model)) check.QuestReqs.Insert(Math.Min(requirementIndex, check.QuestReqs.Count), model);
            if (!rawQuest.WzProperties.Contains(rawRequirement)) rawQuest.WzProperties.Insert(Math.Min(rawIndex, rawQuest.WzProperties.Count), rawRequirement);
        };
        Action undo = () =>
        {
            Ensure(check.QuestReqs.Contains(model) && model.QuestId == targetId && model.QuestState == state &&
                   rawQuest.WzProperties.Contains(rawRequirement),
                "The quest requirement add operation changed outside graph history.");
            check.QuestReqs.Remove(model);
            rawQuest.WzProperties.Remove(rawRequirement);
            if (createdCheck && check.QuestReqs.Count == 0) checks.Remove(check);
            if (createdQuest && rawQuest.WzProperties.Count == 0) rawPhase!.WzProperties.Remove(rawQuest);
            if (createdPhase && rawPhase!.WzProperties.Count == 0) rawRoot.WzProperties.Remove(rawPhase);
        };

        QuestGraphRelationshipAddress address = new(source.Id, QuestGraphRelationshipKind.CheckQuestRequirement, phase, targetId, state, checkIndex, requirementIndex);
        return ApplyOperation(new(address, rawRoot, apply, undo));
    }

    private static QuestGraphRelationshipResult ReplaceRequirement(
        QuestEditorModel source,
        WzSubProperty rawRoot,
        QuestGraphRelationshipAddress address,
        int targetId,
        QuestStateType state)
    {
        if (!TryResolveRequirement(source, rawRoot, address, out QuestEditorQuestReqModel? model, out WzIntProperty? rawId, out WzIntProperty? rawState, out _, out string error))
            return QuestGraphRelationshipResult.Fail(QuestGraphRelationshipErrorCode.StaleAddress, error);
        int oldId = model!.QuestId;
        QuestStateType oldState = model.QuestState;
        int oldRawId = rawId!.Value;
        int oldRawState = rawState!.Value;
        Action apply = () =>
        {
            Ensure(model.QuestId == oldId && model.QuestState == oldState && rawId.Value == oldRawId && rawState.Value == oldRawState,
                "The quest requirement edit operation is stale.");
            model.QuestId = targetId;
            model.QuestState = state;
            rawId.Value = targetId;
            rawState.Value = (int)state;
        };
        Action undo = () =>
        {
            Ensure(model.QuestId == targetId && model.QuestState == state && rawId.Value == targetId && rawState.Value == (int)state,
                "The quest requirement edit operation changed outside graph history.");
            model.QuestId = oldId;
            model.QuestState = oldState;
            rawId.Value = oldRawId;
            rawState.Value = oldRawState;
        };
        QuestGraphRelationshipAddress updated = address with { TargetQuestId = targetId, QuestState = state };
        return ApplyOperation(new(updated, rawRoot, apply, undo));
    }

    private static QuestGraphRelationshipResult RemoveRequirement(
        QuestEditorModel source,
        WzSubProperty rawRoot,
        QuestGraphRelationshipAddress address)
    {
        if (!TryResolveRequirement(source, rawRoot, address, out QuestEditorQuestReqModel? model, out _, out _, out WzSubProperty? rawRequirement, out string error))
            return QuestGraphRelationshipResult.Fail(QuestGraphRelationshipErrorCode.StaleAddress, error);
        ObservableCollection<QuestEditorCheckInfoModel> checks = GetChecks(source, address.Phase);
        QuestEditorCheckInfoModel check = checks[address.ModelIndex];
        WzSubProperty rawQuest = (WzSubProperty)rawRequirement!.Parent;
        WzSubProperty rawPhase = (WzSubProperty)rawQuest.Parent;
        int rawIndex = rawQuest.WzProperties.IndexOf(rawRequirement);
        int requirementIndex = check.QuestReqs.IndexOf(model!);
        int checkIndex = checks.IndexOf(check);
        int questIndex = rawPhase.WzProperties.IndexOf(rawQuest);
        int phaseIndex = rawRoot.WzProperties.IndexOf(rawPhase);
        bool removeEmptyCheck = check.QuestReqs.Count == 1;
        bool removeEmptyQuest = rawQuest.WzProperties.Count == 1;
        bool removeEmptyPhase = removeEmptyQuest && rawPhase.WzProperties.Count == 1;
        Action apply = () =>
        {
            Ensure(check.QuestReqs.Contains(model!) && rawQuest.WzProperties.Contains(rawRequirement),
                "The quest requirement remove operation is stale.");
            check.QuestReqs.Remove(model!);
            rawQuest.WzProperties.Remove(rawRequirement);
            if (removeEmptyCheck && check.QuestReqs.Count == 0) checks.Remove(check);
            if (removeEmptyQuest && rawQuest.WzProperties.Count == 0) rawPhase.WzProperties.Remove(rawQuest);
            if (removeEmptyPhase && rawPhase.WzProperties.Count == 0) rawRoot.WzProperties.Remove(rawPhase);
        };
        Action undo = () =>
        {
            Ensure(!check.QuestReqs.Contains(model!) && !rawQuest.WzProperties.Contains(rawRequirement),
                "The quest requirement remove operation changed outside graph history.");
            Ensure(!rawRoot.WzProperties.Any(item => item != rawPhase && string.Equals(item.Name, PhaseName(address.Phase), StringComparison.OrdinalIgnoreCase)),
                "The raw requirement phase was replaced outside graph history.");
            if (removeEmptyPhase && !rawRoot.WzProperties.Contains(rawPhase))
                rawRoot.WzProperties.Insert(Math.Min(phaseIndex, rawRoot.WzProperties.Count), rawPhase);
            if (removeEmptyQuest && !rawPhase.WzProperties.Contains(rawQuest))
                rawPhase.WzProperties.Insert(Math.Min(questIndex, rawPhase.WzProperties.Count), rawQuest);
            if (removeEmptyCheck && !checks.Contains(check))
                checks.Insert(Math.Min(checkIndex, checks.Count), check);
            if (!check.QuestReqs.Contains(model!)) check.QuestReqs.Insert(Math.Min(requirementIndex, check.QuestReqs.Count), model!);
            if (!rawQuest.WzProperties.Contains(rawRequirement)) rawQuest.WzProperties.Insert(Math.Min(rawIndex, rawQuest.WzProperties.Count), rawRequirement);
        };
        return ApplyOperation(new(address, rawRoot, apply, undo));
    }

    private static QuestGraphRelationshipResult ApplyOperation(QuestGraphRelationshipOperation operation)
    {
        try
        {
            if (!operation.TryRedo(out string error))
                return QuestGraphRelationshipResult.Fail(QuestGraphRelationshipErrorCode.ApplyFailed, error);
            return QuestGraphRelationshipResult.Ok(operation);
        }
        catch (Exception ex)
        {
            return QuestGraphRelationshipResult.Fail(QuestGraphRelationshipErrorCode.ApplyFailed, ex.Message);
        }
    }

    private static bool TryResolveNextQuest(
        QuestEditorModel source,
        WzSubProperty rawRoot,
        QuestGraphRelationshipAddress address,
        out QuestEditorActInfoModel? model,
        out WzIntProperty? raw,
        out string error)
    {
        model = null;
        raw = null;
        error = string.Empty;
        ObservableCollection<QuestEditorActInfoModel> acts = GetActs(source, address.Phase);
        if (address.ModelIndex < 0 || address.ModelIndex >= acts.Count || acts[address.ModelIndex]?.ActType != QuestEditorActType.NextQuest)
        {
            error = "The nextQuest model address is stale.";
            return false;
        }
        model = acts[address.ModelIndex];
        if (acts.Count(item => item?.ActType == QuestEditorActType.NextQuest) != 1)
        {
            error = "The phase has an ambiguous number of nextQuest models.";
            return false;
        }
        if (model.Amount != address.TargetQuestId)
        {
            error = "The nextQuest model changed after the graph was built.";
            return false;
        }
        if (!TryGetUniqueChild(rawRoot, PhaseName(address.Phase), out WzImageProperty? rawPhaseProperty, out error) ||
            rawPhaseProperty is not WzSubProperty rawPhase ||
            !TryGetUniqueChild(rawPhase, "nextQuest", out WzImageProperty? rawNextProperty, out error) ||
            rawNextProperty is not WzIntProperty rawValue)
        {
            if (string.IsNullOrEmpty(error))
                error = "The raw nextQuest property is missing or has an unsupported type.";
            return false;
        }
        if (rawValue.Value != address.TargetQuestId)
        {
            error = "The raw nextQuest value changed after the graph was built.";
            return false;
        }
        raw = rawValue;
        return true;
    }

    private static bool TryResolveRequirement(
        QuestEditorModel source,
        WzSubProperty rawRoot,
        QuestGraphRelationshipAddress address,
        out QuestEditorQuestReqModel? model,
        out WzIntProperty? rawId,
        out WzIntProperty? rawState,
        out WzSubProperty? rawRequirement,
        out string error)
    {
        model = null;
        rawId = null;
        rawState = null;
        rawRequirement = null;
        error = string.Empty;
        ObservableCollection<QuestEditorCheckInfoModel> checks = GetChecks(source, address.Phase);
        if (address.ModelIndex < 0 || address.ModelIndex >= checks.Count || checks[address.ModelIndex]?.CheckType != QuestEditorCheckType.Quest)
        {
            error = "The requirement model address is stale.";
            return false;
        }
        QuestEditorCheckInfoModel check = checks[address.ModelIndex];
        if (address.RequirementIndex < 0 || address.RequirementIndex >= check.QuestReqs.Count)
        {
            error = "The quest requirement address is stale.";
            return false;
        }
        model = check.QuestReqs[address.RequirementIndex];
        if (model == null)
        {
            error = "The quest requirement model is unavailable.";
            return false;
        }
        if (model.QuestId != address.TargetQuestId || model.QuestState != address.QuestState)
        {
            error = "The quest requirement changed after the graph was built.";
            return false;
        }

        if (checks.Count(item => item?.CheckType == QuestEditorCheckType.Quest) != 1)
        {
            error = "The phase has an ambiguous number of quest requirement groups.";
            return false;
        }
        if (!TryGetUniqueChild(rawRoot, PhaseName(address.Phase), out WzImageProperty? rawPhaseProperty, out error) ||
            rawPhaseProperty is not WzSubProperty rawPhase ||
            !TryGetUniqueChild(rawPhase, "quest", out WzImageProperty? rawQuestProperty, out error) ||
            rawQuestProperty is not WzSubProperty rawQuest ||
            !TryValidateRequirementParity(check, rawQuest, out error))
        {
            if (string.IsNullOrEmpty(error))
                error = "The raw quest requirement container is missing or unsupported.";
            return false;
        }

        WzSubProperty[] rawItems = rawQuest.WzProperties.OfType<WzSubProperty>().ToArray();
        if (address.RequirementIndex >= rawItems.Length ||
            rawItems[address.RequirementIndex]["id"] is not WzIntProperty id ||
            rawItems[address.RequirementIndex]["state"] is not WzIntProperty state ||
            id.Value != address.TargetQuestId ||
            state.Value != (int?)address.QuestState)
        {
            error = "The raw quest requirement is missing or has an unsupported shape.";
            return false;
        }
        rawRequirement = rawItems[address.RequirementIndex];
        rawId = id;
        rawState = state;
        return true;
    }

    private static bool TryGetOrCreatePhase(
        WzSubProperty rawRoot,
        QuestGraphRelationshipPhase phase,
        out WzSubProperty? rawPhase,
        out bool created,
        out string error)
    {
        rawPhase = null;
        created = false;
        error = string.Empty;
        if (rawRoot == null)
        {
            error = "The raw quest relationship root is unavailable.";
            return false;
        }
        string name = PhaseName(phase);
        if (!TryGetUniqueChild(rawRoot, name, out WzImageProperty? existing, out error))
            return false;
        if (existing != null && existing is not WzSubProperty)
        {
            error = $"The raw phase '{name}' has an unsupported type.";
            return false;
        }
        rawPhase = existing as WzSubProperty;
        if (rawPhase == null)
        {
            rawPhase = new WzSubProperty(name);
            created = true;
        }
        return true;
    }

    private static bool TryGetUniqueChild(
        WzSubProperty parent,
        string name,
        out WzImageProperty? child,
        out string error)
    {
        child = null;
        error = string.Empty;
        WzImageProperty[] matches = parent.WzProperties
            .Where(property => string.Equals(property?.Name, name, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matches.Length > 1)
        {
            error = $"The raw relationship contains duplicate '{name}' containers.";
            return false;
        }
        child = matches.SingleOrDefault();
        return true;
    }

    private static bool TryValidateRequirementParity(
        QuestEditorCheckInfoModel? check,
        WzSubProperty rawQuest,
        out string error)
    {
        error = string.Empty;
        QuestEditorQuestReqModel[] models = check?.QuestReqs.Where(item => item != null).ToArray() ?? [];
        if (models.Length != (check?.QuestReqs.Count ?? 0))
        {
            error = "The model quest requirements contain unsupported null entries.";
            return false;
        }

        WzSubProperty[] rawItems = rawQuest.WzProperties.OfType<WzSubProperty>().ToArray();
        if (rawItems.Length != rawQuest.WzProperties.Count || rawItems.Length != models.Length)
        {
            error = "The model and raw quest requirement counts do not match.";
            return false;
        }

        for (int index = 0; index < models.Length; index++)
        {
            if (rawItems[index]["id"] is not WzIntProperty id ||
                rawItems[index]["state"] is not WzIntProperty state ||
                id.Value != models[index].QuestId ||
                state.Value != (int)models[index].QuestState)
            {
                error = "The model and raw quest requirement order or values do not match.";
                return false;
            }
        }
        return true;
    }

    private static QuestGraphRelationshipResult ApplyOperation(
        QuestGraphRelationshipAddress address,
        WzSubProperty rawRoot,
        Action apply,
        Action undo) => ApplyOperation(new QuestGraphRelationshipOperation(address, rawRoot, apply, undo));

    private static string PhaseName(QuestGraphRelationshipPhase phase) =>
        phase == QuestGraphRelationshipPhase.Start ? "0" : "1";

    private static ObservableCollection<QuestEditorActInfoModel> GetActs(QuestEditorModel quest, QuestGraphRelationshipPhase phase) =>
        phase == QuestGraphRelationshipPhase.Start ? quest.ActStartInfo : quest.ActEndInfo;

    private static ObservableCollection<QuestEditorCheckInfoModel> GetChecks(QuestEditorModel quest, QuestGraphRelationshipPhase phase) =>
        phase == QuestGraphRelationshipPhase.Start ? quest.CheckStartInfo : quest.CheckEndInfo;

    private static string NextNumericName(WzSubProperty parent)
    {
        int next = parent.WzProperties
            .Select(property => int.TryParse(property.Name, out int value) ? value : -1)
            .DefaultIfEmpty(-1)
            .Max() + 1;
        return next.ToString();
    }

    private static WzSubProperty CreateRawRequirement(string name, int targetId, QuestStateType state)
    {
        WzSubProperty result = new(name);
        result.AddProperty(new WzIntProperty("id", targetId));
        result.AddProperty(new WzIntProperty("state", (int)state));
        return result;
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
