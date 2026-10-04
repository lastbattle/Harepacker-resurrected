using MapleLib.WzLib.WzStructure.Data.MapStructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.GUI.Cutscene
{
    public enum CutsceneValidationSeverity
    {
        Warning,
        Error
    }

    public enum CutsceneValidationCode
    {
        UnsupportedType,
        MissingVisual,
        MissingSound,
        InvalidField,
        MissingAction,
        MissingAppearance,
        InvalidMotionDuration,
        NegativeStart,
        NegativeDuration,
        DuplicateEventId,
        TriggerOutsideMap,
        DuplicateTriggerId
    }

    public sealed class CutsceneValidationIssue
    {
        public CutsceneValidationCode Code { get; init; }
        public CutsceneValidationSeverity Severity { get; init; }
        public CutsceneSceneModel Scene { get; init; }
        public CutsceneEventModel Event { get; init; }
        public MapDirectionEvent Trigger { get; init; }
        public string Message { get; set; }
        public override string ToString() => Message;
    }

    public static class CutsceneValidator
    {
        public static IReadOnlyList<CutsceneValidationIssue> ValidateScenes(
            IEnumerable<CutsceneSceneModel> scenes,
            Func<CutsceneSceneModel, string, bool> visualExists,
            Func<string, bool> soundExists)
        {
            List<CutsceneValidationIssue> issues = new();
            foreach (CutsceneSceneModel scene in scenes ?? Enumerable.Empty<CutsceneSceneModel>())
            {
                foreach (IGrouping<string, CutsceneEventModel> duplicateGroup in scene.Events
                    .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                    .Where(group => group.Count() > 1))
                {
                    issues.Add(new CutsceneValidationIssue
                    {
                        Code = CutsceneValidationCode.DuplicateEventId,
                        Severity = CutsceneValidationSeverity.Error,
                        Scene = scene,
                        Event = duplicateGroup.First()
                    });
                }

                foreach (CutsceneEventModel item in scene.Events)
                {
                    if (!Enum.IsDefined(typeof(ReservedSceneEventType), item.Type))
                        Add(issues, CutsceneValidationCode.UnsupportedType, CutsceneValidationSeverity.Warning, scene, item);
                    if (item.Start < 0)
                        Add(issues, CutsceneValidationCode.NegativeStart, CutsceneValidationSeverity.Error, scene, item);
                    if (item.Duration < 0)
                        Add(issues, CutsceneValidationCode.NegativeDuration, CutsceneValidationSeverity.Error, scene, item);

                    switch ((ReservedSceneEventType)item.Type)
                    {
                        case ReservedSceneEventType.Visual:
                            if (string.IsNullOrWhiteSpace(item.Visual) || visualExists?.Invoke(scene, item.Visual) == false)
                                Add(issues, CutsceneValidationCode.MissingVisual, CutsceneValidationSeverity.Error, scene, item);
                            if (!string.IsNullOrWhiteSpace(item.Sound) && soundExists?.Invoke(item.Sound) == false)
                                Add(issues, CutsceneValidationCode.MissingSound, CutsceneValidationSeverity.Error, scene, item);
                            if ((item.X1 != 0 || item.Y1 != 0) && item.Duration <= 0)
                                Add(issues, CutsceneValidationCode.InvalidMotionDuration, CutsceneValidationSeverity.Error, scene, item);
                            break;
                        case ReservedSceneEventType.FieldTransition:
                            if (item.Field <= 0)
                                Add(issues, CutsceneValidationCode.InvalidField, CutsceneValidationSeverity.Error, scene, item);
                            break;
                        case ReservedSceneEventType.CharacterAppearance:
                            if (string.IsNullOrWhiteSpace(item.Appearance))
                                Add(issues, CutsceneValidationCode.MissingAppearance, CutsceneValidationSeverity.Error, scene, item);
                            break;
                        case ReservedSceneEventType.CharacterAction:
                            if (string.IsNullOrWhiteSpace(item.Action))
                                Add(issues, CutsceneValidationCode.MissingAction, CutsceneValidationSeverity.Error, scene, item);
                            break;
                        case ReservedSceneEventType.Sound:
                            if (string.IsNullOrWhiteSpace(item.Sound) || soundExists?.Invoke(item.Sound) == false)
                                Add(issues, CutsceneValidationCode.MissingSound, CutsceneValidationSeverity.Error, scene, item);
                            break;
                    }
                }
            }
            return issues;
        }

        public static IReadOnlyList<CutsceneValidationIssue> ValidateTriggers(MapDirectionInfo directionInfo, int mapWidth, int mapHeight)
        {
            if (directionInfo == null)
                return Array.Empty<CutsceneValidationIssue>();
            List<CutsceneValidationIssue> issues = new();
            foreach (IGrouping<string, MapDirectionEvent> duplicateGroup in directionInfo.Events
                .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1))
            {
                issues.Add(new CutsceneValidationIssue
                {
                    Code = CutsceneValidationCode.DuplicateTriggerId,
                    Severity = CutsceneValidationSeverity.Error,
                    Trigger = duplicateGroup.First()
                });
            }

            int halfWidth = Math.Max(1, mapWidth / 2);
            int halfHeight = Math.Max(1, mapHeight / 2);
            foreach (MapDirectionEvent trigger in directionInfo.Events)
            {
                if (Math.Abs(trigger.X) > halfWidth || Math.Abs(trigger.Y) > halfHeight)
                {
                    issues.Add(new CutsceneValidationIssue
                    {
                        Code = CutsceneValidationCode.TriggerOutsideMap,
                        Severity = CutsceneValidationSeverity.Error,
                        Trigger = trigger
                    });
                }
            }
            return issues;
        }

        private static void Add(List<CutsceneValidationIssue> issues, CutsceneValidationCode code,
            CutsceneValidationSeverity severity, CutsceneSceneModel scene, CutsceneEventModel item)
        {
            issues.Add(new CutsceneValidationIssue
            {
                Code = code,
                Severity = severity,
                Scene = scene,
                Event = item
            });
        }
    }
}
