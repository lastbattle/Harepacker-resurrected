using System;
using System.Collections.Generic;
using System.Globalization;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class TrunkDialogClientParityText
    {
        internal readonly record struct ConfirmationStep(int StringPoolId, string OwnerCall, string Text);

        internal const int SendGetPreConfirmStringPoolId = 0x1246;
        internal const int SendPutPreConfirmStringPoolId = 0x1245;
        internal const int SendPutSharableOnceConfirmStringPoolId = 0x169D;
        internal const int SendPutSharableOnceBlockedStringPoolId = 0x169E;
        internal const int SendGetNoCostConfirmStringPoolId = 0x036C;
        internal const int SendGetCostConfirmStringPoolId = 0x036D;
        internal const int SendPutNoCostConfirmStringPoolId = 0x036E;
        internal const int SendPutCostConfirmStringPoolId = 0x036F;
        internal const int SendPutAskItemCountStringPoolId = 0x0370;

        internal static string ResolveSendGetPreConfirm()
        {
            return MapleStoryStringPool.GetOrFallback(
                SendGetPreConfirmStringPoolId,
                "Would you like to retrieve this item from storage?",
                appendFallbackSuffix: true);
        }

        internal static string ResolveSendGetCostConfirm(int mesoCost)
        {
            if (mesoCost <= 0)
            {
                return MapleStoryStringPool.GetOrFallback(
                    SendGetNoCostConfirmStringPoolId,
                    "Would you like to retrieve this item?",
                    appendFallbackSuffix: true);
            }

            return ResolveNumericTemplate(
                SendGetCostConfirmStringPoolId,
                "Recovering the items will cost {0} mesos.\r\nAre you sure you want to recover this?",
                mesoCost);
        }

        internal static string ResolveSendPutPreConfirm(bool sharableOnce)
        {
            return MapleStoryStringPool.GetOrFallback(
                sharableOnce ? SendPutSharableOnceConfirmStringPoolId : SendPutPreConfirmStringPoolId,
                sharableOnce
                    ? "This cash item can only be shared once. Continue storing this item?"
                    : "Would you like to store this item?",
                appendFallbackSuffix: true);
        }

        internal static string ResolveSendPutAskItemCountPrompt()
        {
            return MapleStoryStringPool.GetOrFallback(
                SendPutAskItemCountStringPoolId,
                "Please enter the quantity to store.",
                appendFallbackSuffix: true);
        }

        internal static string ResolveSendPutCostConfirm(int mesoCost)
        {
            if (mesoCost <= 0)
            {
                return MapleStoryStringPool.GetOrFallback(
                    SendPutNoCostConfirmStringPoolId,
                    "Would you like to store this item?",
                    appendFallbackSuffix: true);
            }

            return ResolveNumericTemplate(
                SendPutCostConfirmStringPoolId,
                "Storing this will cost {0} mesos.\r\nAre you sure you want to store this?",
                mesoCost);
        }

        internal static string ResolveSharableOnceBlockedNotice()
        {
            return MapleStoryStringPool.GetOrFallback(
                SendPutSharableOnceBlockedStringPoolId,
                "This sharable-once item can no longer be moved.",
                appendFallbackSuffix: true);
        }

        internal static bool IsSharableOnceOwnershipBlocked(InventorySlotData slotData)
        {
            if (slotData == null)
            {
                return false;
            }

            if (slotData.IsCashOwnershipLocked)
            {
                return true;
            }

            return slotData.CashItemSerialNumber.GetValueOrDefault() > 0
                && slotData.OwnerAccountId.GetValueOrDefault() > 0;
        }

        internal static bool RequiresOwnershipPreConfirm(InventorySlotData slotData)
        {
            if (slotData == null)
            {
                return false;
            }

            return slotData.CashItemSerialNumber.GetValueOrDefault() > 0
                || slotData.OwnerAccountId.GetValueOrDefault() > 0
                || slotData.OwnerCharacterId.GetValueOrDefault() > 0
                || slotData.IsCashOwnershipLocked;
        }

        internal static string BuildSendGetConfirmationBody(InventorySlotData slotData, int mesoCost)
        {
            string costConfirm = ResolveSendGetCostConfirm(mesoCost);
            if (!RequiresOwnershipPreConfirm(slotData))
            {
                return costConfirm;
            }

            return $"{ResolveSendGetPreConfirm()}\r\n\r\n{costConfirm}";
        }

        internal static IReadOnlyList<ConfirmationStep> BuildSendGetConfirmationChoreography(InventorySlotData slotData, int mesoCost)
        {
            List<ConfirmationStep> steps = new(2);
            if (RequiresOwnershipPreConfirm(slotData))
            {
                steps.Add(new ConfirmationStep(
                    SendGetPreConfirmStringPoolId,
                    "CUtilDlg::YesNo",
                    ResolveSendGetPreConfirm()));
            }

            int costStringPoolId = mesoCost <= 0
                ? SendGetNoCostConfirmStringPoolId
                : SendGetCostConfirmStringPoolId;
            steps.Add(new ConfirmationStep(
                costStringPoolId,
                "CUtilDlg::YesNo",
                ResolveSendGetCostConfirm(mesoCost)));
            return steps;
        }

        internal static string BuildSendPutConfirmationBody(InventorySlotData slotData, bool treatSingly, int availableQuantity, int mesoCost, bool includeAskCount = true)
        {
            string preConfirm = RequiresOwnershipPreConfirm(slotData)
                ? $"{ResolveSendPutPreConfirm(slotData?.CashItemSerialNumber.GetValueOrDefault() > 0)}\r\n\r\n"
                : string.Empty;
            string askCount = includeAskCount && !treatSingly && availableQuantity > 1
                ? $"{ResolveSendPutAskItemCountPrompt()}\r\n\r\n"
                : string.Empty;

            return $"{preConfirm}{askCount}{ResolveSendPutCostConfirm(mesoCost)}";
        }

        internal static IReadOnlyList<ConfirmationStep> BuildSendPutConfirmationChoreography(InventorySlotData slotData, bool treatSingly, int availableQuantity, int mesoCost, bool includeAskCount = true)
        {
            List<ConfirmationStep> steps = new(3);
            bool sharableOnce = slotData?.CashItemSerialNumber.GetValueOrDefault() > 0;
            if (RequiresOwnershipPreConfirm(slotData))
            {
                steps.Add(new ConfirmationStep(
                    sharableOnce ? SendPutSharableOnceConfirmStringPoolId : SendPutPreConfirmStringPoolId,
                    "CUtilDlg::YesNo",
                    ResolveSendPutPreConfirm(sharableOnce)));
            }

            if (includeAskCount && !treatSingly && availableQuantity > 1)
            {
                steps.Add(new ConfirmationStep(
                    SendPutAskItemCountStringPoolId,
                    "CUtilDlg::AskNumber",
                    ResolveSendPutAskItemCountPrompt()));
            }

            int costStringPoolId = mesoCost <= 0
                ? SendPutNoCostConfirmStringPoolId
                : SendPutCostConfirmStringPoolId;
            steps.Add(new ConfirmationStep(
                costStringPoolId,
                "CUtilDlg::YesNo",
                ResolveSendPutCostConfirm(mesoCost)));
            return steps;
        }

        internal static string ToInlineText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return text
                .Replace("\r", " ", StringComparison.Ordinal)
                .Replace("\n", " ", StringComparison.Ordinal)
                .Trim();
        }

        internal static string DescribeConfirmationChoreography(IReadOnlyList<ConfirmationStep> steps)
        {
            if (steps == null || steps.Count == 0)
            {
                return "none";
            }

            List<string> parts = new(steps.Count);
            foreach (ConfirmationStep step in steps)
            {
                parts.Add($"{step.OwnerCall} StringPool 0x{step.StringPoolId.ToString("X", CultureInfo.InvariantCulture)} \"{ToInlineText(step.Text)}\"");
            }

            return string.Join(" -> ", parts);
        }

        private static string ResolveNumericTemplate(int stringPoolId, string fallbackFormat, int value)
        {
            string template = MapleStoryStringPool.GetCompositeFormatOrFallback(
                stringPoolId,
                fallbackFormat,
                1,
                out _);
            if (string.IsNullOrWhiteSpace(template))
            {
                return string.Format(CultureInfo.InvariantCulture, fallbackFormat, value);
            }

            if (template.Contains("%d", StringComparison.Ordinal))
            {
                return template.Replace("%d", value.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
            }

            return string.Format(CultureInfo.InvariantCulture, template, value);
        }
    }
}
