using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.GUI.Quest
{
    public partial class QuestEditor
    {
        private void EnsureAdditionalProperties(QuestEditorModel quest)
        {
            if (quest == null || quest.AdditionalPropertiesLoaded)
                return;

            quest.AdditionalPropertiesLoaded = true;
            string questId = quest.Id.ToString();
            if (!Program.InfoManager.QuestInfos.TryGetValue(questId, out WzSubProperty info))
                return;

            CaptureAdditionalProperties(
                quest,
                info,
                Program.InfoManager.QuestSays.GetValueOrDefault(questId),
                Program.InfoManager.QuestActs.GetValueOrDefault(questId),
                Program.InfoManager.QuestChecks.GetValueOrDefault(questId));

            foreach (QuestEditorAdditionalPropertyModel property in quest.ModernQuestInfoProperties
                .Concat(quest.ModernSayProperties)
                .Concat(quest.ModernActProperties)
                .Concat(quest.ModernCheckProperties)
                .Concat(quest.AdditionalProperties))
            {
                property.PropertyChanged += (_, _) => _unsavedChanges = true;
            }
        }

        private void CaptureAdditionalProperties(
            QuestEditorModel quest,
            WzSubProperty originalInfo,
            WzSubProperty originalSay,
            WzSubProperty originalAct,
            WzSubProperty originalCheck)
        {
            Tuple<WzSubProperty, WzSubProperty, WzSubProperty, WzSubProperty> modeled =
                saveQuestAsWzImage(quest);

            CaptureSectionDifferences(quest, "QuestInfo", originalInfo, modeled.Item1, Array.Empty<string>(), new Dictionary<int, string>());
            CaptureSectionDifferences(quest, "Say", originalSay, modeled.Item2, Array.Empty<string>(), new Dictionary<int, string>());
            CaptureSectionDifferences(quest, "Act", originalAct, modeled.Item3, Array.Empty<string>(), new Dictionary<int, string>());
            CaptureSectionDifferences(quest, "Check", originalCheck, modeled.Item4, Array.Empty<string>(), new Dictionary<int, string>());
        }

        private static void CaptureSectionDifferences(
            QuestEditorModel quest,
            string section,
            WzImageProperty original,
            WzImageProperty modeled,
            IReadOnlyList<string> parentPath,
            IReadOnlyDictionary<int, string> parentIdentities)
        {
            if (original?.WzProperties == null)
                return;

            foreach (WzImageProperty originalChild in original.WzProperties)
            {
                WzImageProperty modeledChild = modeled?[originalChild.Name];
                if (modeledChild == null)
                {
                    WzImageProperty clone = originalChild.DeepClone();
                    quest.AdditionalPropertyNodes.Add(
                        new QuestEditorAdditionalPropertyNode(section, parentPath.ToArray(), clone, false,
                            new Dictionary<int, string>(parentIdentities)));
                    AddEditableLeaves(quest, section, parentPath, clone);
                    continue;
                }

                string comparedPath = $"{section}/{string.Join("/", parentPath.Append(originalChild.Name))}";
                if (originalChild.PropertyType != modeledChild.PropertyType
                    && QuestEditorKnownPropertyCatalog.ContainsOrHasKnownAncestor(comparedPath))
                {
                    WzImageProperty clone = originalChild.DeepClone();
                    quest.AdditionalPropertyNodes.Add(
                        new QuestEditorAdditionalPropertyNode(section, parentPath.ToArray(), clone, true,
                            new Dictionary<int, string>(parentIdentities)));
                    AddEditableLeaves(quest, section, parentPath, clone);
                    continue;
                }

                if (originalChild.WzProperties != null && modeledChild.WzProperties != null)
                {
                    string[] childPath = parentPath.Append(originalChild.Name).ToArray();
                    var childIdentities = new Dictionary<int, string>(parentIdentities);
                    string identity = GetContainerIdentity(originalChild);
                    if (identity != null)
                        childIdentities[childPath.Length - 1] = identity;
                    CaptureSectionDifferences(quest, section, originalChild, modeledChild, childPath, childIdentities);
                }
            }
        }

        private static void AddEditableLeaves(
            QuestEditorModel quest,
            string section,
            IReadOnlyList<string> parentPath,
            WzImageProperty property)
        {
            string[] propertyPath = parentPath.Append(property.Name).ToArray();
            if (property.WzProperties is { Count: > 0 })
            {
                foreach (WzImageProperty child in property.WzProperties)
                    AddEditableLeaves(quest, section, propertyPath, child);
                return;
            }

            if (property is WzIntProperty
                or WzShortProperty
                or WzLongProperty
                or WzFloatProperty
                or WzDoubleProperty
                or WzStringProperty
                or WzUOLProperty)
            {
                string propertyCatalogPath = $"{section}/{string.Join("/", propertyPath)}";
                bool isKnown = QuestEditorKnownPropertyCatalog.ContainsOrHasKnownAncestor(propertyCatalogPath);
                var propertyModel = new QuestEditorAdditionalPropertyModel(
                    section,
                    string.Join("/", propertyPath),
                    property,
                    isKnown);
                propertyModel.PropertyChanged += (_, _) => quest.ModifiedAdditionalProperties.Add(property);
                if (isKnown)
                    quest.AddModernProperty(section, propertyModel);
                else
                    quest.AddAdditionalProperty(section, propertyModel);
            }
        }

        private static void RestoreAdditionalProperties(
            QuestEditorModel quest,
            WzSubProperty info,
            WzSubProperty say,
            WzSubProperty act,
            WzSubProperty check)
        {
            var sections = new Dictionary<string, WzSubProperty>(StringComparer.OrdinalIgnoreCase)
            {
                ["QuestInfo"] = info,
                ["Say"] = say,
                ["Act"] = act,
                ["Check"] = check,
            };

            // Resolve rich-conversation destinations before mutating any siblings.
            // This keeps multiple reordered sayEx/yesEx/noEx nodes from colliding.
            var replacementNames = new Dictionary<QuestEditorAdditionalPropertyNode, string>();
            foreach (QuestEditorAdditionalPropertyNode node in quest.AdditionalPropertyNodes.Where(node => node.ReplaceExisting))
            {
                WzSubProperty originalParent = ResolveExistingParent(sections, node);
                WzStringProperty richText = FindPreferredTextProperty(node.Property);
                if (originalParent == null || richText == null
                    || quest.ModifiedAdditionalProperties.Contains(richText))
                {
                    continue;
                }

                WzStringProperty matchingConversation = originalParent.WzProperties
                    .OfType<WzStringProperty>()
                    .FirstOrDefault(candidate => candidate.Value == richText.Value);
                if (matchingConversation != null)
                    replacementNames[node] = matchingConversation.Name;
            }

            foreach (QuestEditorAdditionalPropertyNode node in quest.AdditionalPropertyNodes)
            {
                WzSubProperty parent = ResolveExistingParent(sections, node);
                if (parent == null)
                    continue;

                string replacementName = replacementNames.GetValueOrDefault(node, node.Property.Name);
                WzImageProperty existingProperty = parent[replacementName];
                if (existingProperty != null && node.ReplaceExisting)
                {
                    if (existingProperty.WzProperties == null
                        && node.Property.WzProperties == null
                        && !quest.ModifiedAdditionalProperties.Contains(node.Property))
                    {
                        SetCompatibleValue(node.Property, existingProperty.WzValue);
                    }

                    if (existingProperty is WzStringProperty modeledText)
                    {
                        WzStringProperty richText = FindPreferredTextProperty(node.Property);
                        if (!replacementNames.ContainsKey(node) && richText != null
                            && !quest.ModifiedAdditionalProperties.Contains(richText)
                            && modeledText.Value != richText.Value)
                        {
                            WzStringProperty matchingConversation = parent.WzProperties
                                .OfType<WzStringProperty>()
                                .FirstOrDefault(candidate => candidate.Value == richText.Value);
                            if (matchingConversation != null)
                            {
                                existingProperty = matchingConversation;
                                modeledText = matchingConversation;
                                replacementName = matchingConversation.Name;
                            }
                        }
                        if (richText != null && !quest.ModifiedAdditionalProperties.Contains(richText))
                            richText.SetValue(modeledText.Value);
                    }

                    existingProperty.Remove();
                    existingProperty = null;
                }

                if (existingProperty == null)
                {
                    WzImageProperty replacement = node.Property.DeepClone();
                    replacement.Name = replacementName;
                    parent.AddProperty(replacement);
                }
            }
        }

        private static WzSubProperty ResolveExistingParent(
            IReadOnlyDictionary<string, WzSubProperty> sections,
            QuestEditorAdditionalPropertyNode node)
        {
            if (!sections.TryGetValue(node.Section, out WzSubProperty parent))
                return null;

            for (int pathIndex = 0; pathIndex < node.ParentPath.Count; pathIndex++)
            {
                string segment = node.ParentPath[pathIndex];
                WzSubProperty existing = parent[segment] as WzSubProperty;
                if (node.ParentIdentities.TryGetValue(pathIndex, out string expectedIdentity)
                    && GetContainerIdentity(existing) != expectedIdentity)
                {
                    existing = parent.WzProperties
                        .OfType<WzSubProperty>()
                        .FirstOrDefault(candidate => GetContainerIdentity(candidate) == expectedIdentity);
                }

                if (existing == null)
                    return null;

                parent = existing;
            }

            return parent;
        }

        private static string GetContainerIdentity(WzImageProperty property)
        {
            if (property?.WzProperties == null)
                return null;

            string[] identityNames = { "id", "qrID", "recordID", "key" };
            foreach (string identityName in identityNames)
            {
                WzImageProperty identity = property[identityName];
                if (identity != null && identity.WzProperties == null)
                    return $"{identityName}={identity.WzValue}";
            }

            return null;
        }

        private static WzStringProperty FindPreferredTextProperty(WzImageProperty property)
        {
            if (property is WzStringProperty text
                && (text.Name.Equals("msg", StringComparison.OrdinalIgnoreCase)
                    || int.TryParse(text.Name, out _)))
            {
                return text;
            }

            if (property.WzProperties == null)
                return null;

            foreach (WzImageProperty child in property.WzProperties)
            {
                WzStringProperty result = FindPreferredTextProperty(child);
                if (result != null)
                    return result;
            }

            return null;
        }

        private static void SetCompatibleValue(WzImageProperty property, object value)
        {
            object converted = property switch
            {
                WzIntProperty => Convert.ToInt32(value),
                WzShortProperty => Convert.ToInt16(value),
                WzLongProperty => Convert.ToInt64(value),
                WzFloatProperty => Convert.ToSingle(value),
                WzDoubleProperty => Convert.ToDouble(value),
                WzStringProperty => Convert.ToString(value),
                WzUOLProperty => Convert.ToString(value),
                _ => value,
            };
            property.SetValue(converted);
        }
    }
}
