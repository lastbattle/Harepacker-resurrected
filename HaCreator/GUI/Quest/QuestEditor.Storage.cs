using HaSharedLibrary.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.GUI.Quest
{
    public partial class QuestEditor
    {
        private static bool UsesPerQuestStorage()
        {
            return Program.InfoManager.QuestInfos.Values
                .Select(property => property?.ParentImage)
                .Any(IsPerQuestStorageImage);
        }

        private static bool UsesPerQuestStorage(string questId)
        {
            if (Program.InfoManager.QuestInfos.TryGetValue(questId, out WzSubProperty existing))
                return IsPerQuestStorageImage(existing?.ParentImage);

            return UsesPerQuestStorage();
        }

        private static bool IsPerQuestStorageImage(WzImage image)
        {
            if (image == null)
                return false;

            return image["QuestInfo"] is WzSubProperty
                || image["Say"] is WzSubProperty
                || image["Act"] is WzSubProperty
                || image["Check"] is WzSubProperty;
        }

        private static WzImage ResolvePerQuestStorageImage(string questId)
        {
            WzSubProperty[] properties =
            {
                Program.InfoManager.QuestInfos.GetValueOrDefault(questId),
                Program.InfoManager.QuestSays.GetValueOrDefault(questId),
                Program.InfoManager.QuestActs.GetValueOrDefault(questId),
                Program.InfoManager.QuestChecks.GetValueOrDefault(questId),
            };

            return properties
                .Select(property => property?.ParentImage)
                .FirstOrDefault(image => IsPerQuestStorageImage(image));
        }

        private void StorePerQuestData(
            string questId,
            WzSubProperty info,
            WzSubProperty say,
            WzSubProperty act,
            WzSubProperty check)
        {
            WzImage questImage = ResolvePerQuestStorageImage(questId)
                ?? new WzImage($"{questId}.img");

            ReplacePerQuestSection(questImage, "QuestInfo", info);
            ReplacePerQuestSection(questImage, "Say", say);
            ReplacePerQuestSection(questImage, "Act", act);
            ReplacePerQuestSection(questImage, "Check", check);

            UpdateQuestCollection(Program.InfoManager.QuestInfos, questId, info);
            UpdateQuestCollection(Program.InfoManager.QuestSays, questId, say);
            UpdateQuestCollection(Program.InfoManager.QuestActs, questId, act);
            UpdateQuestCollection(Program.InfoManager.QuestChecks, questId, check);

            _unsavedChanges = true;
            PersistPerQuestImage(questImage, questId);
        }

        private static void ReplacePerQuestSection(
            WzImage questImage,
            string sectionName,
            WzSubProperty replacement)
        {
            questImage.RemoveProperty(sectionName);
            if (replacement == null)
                return;

            replacement.Name = sectionName;
            questImage.AddProperty(replacement);
        }

        private static void UpdateQuestCollection(
            IDictionary<string, WzSubProperty> collection,
            string questId,
            WzSubProperty property)
        {
            if (property == null)
                collection.Remove(questId);
            else
                collection[questId] = property;
        }

        private static void MarkQuestImageUpdated(WzImage image)
        {
            if (image == null)
                return;

            if (IsPerQuestStorageImage(image) && Program.DataSource != null)
            {
                string questId = WzInfoTools.RemoveExtension(image.Name);
                Program.DataSource.SaveImage("Quest", image, $"QuestData/{questId}.img");
                return;
            }

            Program.MarkImageUpdated("Quest", image);
        }

        private static void PersistPerQuestImage(WzImage image, string questId)
        {
            if (Program.DataSource != null)
            {
                Program.DataSource.SaveImage("Quest", image, $"QuestData/{questId}.img");
                return;
            }

            Program.MarkImageUpdated("Quest", image);
        }
    }
}
