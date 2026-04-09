using HaCreator.MapSimulator.Interaction;
using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace HaCreator.MapSimulator.Managers
{
    internal sealed class PacketOwnedRandomMesoBagResult
    {
        public byte Rank { get; init; }
        public int MesoAmount { get; init; }
    }

    internal sealed class PacketOwnedRandomMesoBagPresentation
    {
        public int Rank { get; init; }
        public string BackgroundKey { get; init; } = "Back1";
        public string DialogResourcePath { get; init; } = string.Empty;
        public string OkButtonResourcePath { get; init; } = string.Empty;
        public string DescriptionText { get; init; } = string.Empty;
        public string AmountText { get; init; } = string.Empty;
        public string ChatLineText { get; init; } = string.Empty;
        public string SoundDescriptor { get; init; } = string.Empty;
    }

    internal static class PacketOwnedRewardResultRuntime
    {
        private const int MesoGiveSucceededStringPoolId = 0x32E;
        private const int MesoGiveFailedStringPoolId = 0x32F;
        private const int RandomMesoBagDialogRank1StringPoolId = 0x17A9;
        private const int RandomMesoBagDialogRank2StringPoolId = 0x17AA;
        private const int RandomMesoBagDialogRank3StringPoolId = 0x17AB;
        private const int RandomMesoBagDialogRank4StringPoolId = 0x17AC;
        private const int RandomMesoBagMessageRank1StringPoolId = 0x17AF;
        private const int RandomMesoBagMessageRank2StringPoolId = 0x17B0;
        private const int RandomMesoBagMessageRank3StringPoolId = 0x17B1;
        private const int RandomMesoBagMessageRank4StringPoolId = 0x17B2;
        private const int RandomMesoBagChatTemplateStringPoolId = 0x17B4;
        private const int RandomMesoBagFailedStringPoolId = 0x17B3;
        private const int RandomMesoBagOkButtonStringPoolId = 0x17AD;
        private const int RandomMesoBagSoundRank1StringPoolId = 0x17B6;
        private const int RandomMesoBagSoundRank2StringPoolId = 0x17B7;
        private const int RandomMesoBagSoundRank3StringPoolId = 0x17B8;
        private const int RandomMesoBagSoundRank4StringPoolId = 0x17B9;

        public static bool TryDecodeRandomMesoBagSucceeded(byte[] payload, out PacketOwnedRandomMesoBagResult result, out string error)
        {
            result = null;
            error = null;

            if (payload == null || payload.Length < sizeof(byte) + sizeof(int))
            {
                error = "Random-mesobag success payload must contain a rank byte and mesos amount.";
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream, Encoding.ASCII, leaveOpen: false);

                byte rank = reader.ReadByte();
                int mesoAmount = reader.ReadInt32();
                if (stream.Position != stream.Length)
                {
                    error = $"Random-mesobag success payload has {stream.Length - stream.Position} trailing byte(s).";
                    return false;
                }

                result = new PacketOwnedRandomMesoBagResult
                {
                    Rank = rank,
                    MesoAmount = mesoAmount
                };
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException)
            {
                error = $"Random-mesobag success payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        public static string FormatMesoGiveSucceededText(int mesoAmount)
        {
            string format = ResolveTextFormat(
                MesoGiveSucceededStringPoolId,
                "You have received {0:N0} mesos.",
                1);
            return FormatInvariant(format, mesoAmount);
        }

        public static string GetMesoGiveFailedText()
        {
            return ResolvePlainText(
                MesoGiveFailedStringPoolId,
                "You have failed to use the meso bag.");
        }

        public static string GetRandomMesoBagFailedText()
        {
            return ResolvePlainText(
                RandomMesoBagFailedStringPoolId,
                "You have failed to use the Random Meso Sack.");
        }

        public static PacketOwnedRandomMesoBagPresentation CreateRandomMesoBagPresentation(byte rank, int mesoAmount)
        {
            int normalizedRank = Math.Clamp(rank <= 0 ? 1 : rank, 1, 4);
            string dialogResourcePath = ResolveRandomMesoBagDialogResourcePath(normalizedRank);
            return new PacketOwnedRandomMesoBagPresentation
            {
                Rank = normalizedRank,
                BackgroundKey = ResolveRandomMesoBagBackgroundKey(normalizedRank, dialogResourcePath),
                DialogResourcePath = dialogResourcePath,
                OkButtonResourcePath = GetRandomMesoBagOkButtonResourcePath(),
                DescriptionText = ResolveRandomMesoBagDescription(normalizedRank),
                AmountText = mesoAmount.ToString("N0", CultureInfo.InvariantCulture),
                ChatLineText = FormatInvariant(
                    ResolveTextFormat(
                        RandomMesoBagChatTemplateStringPoolId,
                        "You obtained {0:N0} mesos from the Random Meso Sack.",
                        1),
                    mesoAmount),
                SoundDescriptor = ResolveRandomMesoBagSound(normalizedRank)
            };
        }

        public static string GetRandomMesoBagDialogResourcePath(int rank)
        {
            return ResolveRandomMesoBagDialogResourcePath(Math.Clamp(rank, 1, 4));
        }

        public static string GetRandomMesoBagOkButtonResourcePath()
        {
            return ResolvePlainText(
                RandomMesoBagOkButtonStringPoolId,
                "UI/UIWindow.img/RandomMesoBag/BtOk");
        }

        private static string ResolveRandomMesoBagDialogResourcePath(int rank)
        {
            int stringPoolId = rank switch
            {
                2 => RandomMesoBagDialogRank2StringPoolId,
                3 => RandomMesoBagDialogRank3StringPoolId,
                4 => RandomMesoBagDialogRank4StringPoolId,
                _ => RandomMesoBagDialogRank1StringPoolId
            };

            return ResolvePlainText(
                stringPoolId,
                $"UI/UIWindow.img/RandomMesoBag/Back{Math.Clamp(rank, 1, 4)}");
        }

        private static string ResolveRandomMesoBagBackgroundKey(int rank, string dialogResourcePath)
        {
            if (!string.IsNullOrWhiteSpace(dialogResourcePath))
            {
                int separatorIndex = Math.Max(
                    dialogResourcePath.LastIndexOf('/'),
                    dialogResourcePath.LastIndexOf('\\'));
                string resourceName = separatorIndex >= 0
                    ? dialogResourcePath[(separatorIndex + 1)..]
                    : dialogResourcePath;
                if (!string.IsNullOrWhiteSpace(resourceName))
                {
                    return resourceName;
                }
            }

            return $"Back{Math.Clamp(rank, 1, 4)}";
        }

        private static string ResolveRandomMesoBagDescription(int rank)
        {
            int stringPoolId = rank switch
            {
                2 => RandomMesoBagMessageRank2StringPoolId,
                3 => RandomMesoBagMessageRank3StringPoolId,
                4 => RandomMesoBagMessageRank4StringPoolId,
                _ => RandomMesoBagMessageRank1StringPoolId
            };

            return ResolvePlainText(
                stringPoolId,
                rank switch
                {
                    2 => "An adequate amount of mesos!",
                    3 => "A large amount of mesos!",
                    4 => "A huge amount of mesos!",
                    _ => "A small amount of mesos!"
                });
        }

        private static string ResolveRandomMesoBagSound(int rank)
        {
            int stringPoolId = rank switch
            {
                2 => RandomMesoBagSoundRank2StringPoolId,
                3 => RandomMesoBagSoundRank3StringPoolId,
                4 => RandomMesoBagSoundRank4StringPoolId,
                _ => RandomMesoBagSoundRank1StringPoolId
            };

            string fallback = rank switch
            {
                2 => "Sound/Item.img/02000011/Use",
                3 => "Sound/Item.img/02022108/Use",
                4 => "Sound/Item.img/02022109/Use",
                _ => "Sound/Item.img/02000010/Use"
            };

            return ResolvePlainText(stringPoolId, fallback);
        }

        private static string ResolveTextFormat(int stringPoolId, string fallbackFormat, int maxPlaceholderCount)
        {
            string format = MapleStoryStringPool.GetCompositeFormatOrFallback(
                stringPoolId,
                fallbackFormat,
                maxPlaceholderCount,
                out bool usedResolvedText);
            return usedResolvedText && LooksLikeAssetPath(format)
                ? fallbackFormat
                : format;
        }

        private static string ResolvePlainText(int stringPoolId, string fallbackText)
        {
            string resolved = MapleStoryStringPool.GetOrFallback(stringPoolId, fallbackText);
            return LooksLikeAssetPath(resolved)
                ? fallbackText
                : resolved;
        }

        private static string FormatInvariant(string format, params object[] values)
        {
            return string.Format(CultureInfo.InvariantCulture, format, values);
        }

        private static bool LooksLikeAssetPath(string value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && (value.Contains("UI/", StringComparison.OrdinalIgnoreCase)
                    || value.Contains("UI\\", StringComparison.OrdinalIgnoreCase)
                    || value.Contains("Sound/", StringComparison.OrdinalIgnoreCase)
                    || value.Contains("Sound\\", StringComparison.OrdinalIgnoreCase)
                    || value.Contains(".img/", StringComparison.OrdinalIgnoreCase)
                    || value.Contains(".img\\", StringComparison.OrdinalIgnoreCase));
        }
    }
}
