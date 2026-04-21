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
        public int ChatMessageType { get; init; }
        public string SoundDescriptor { get; init; } = string.Empty;
        public int SoundVolume { get; init; }
    }

    internal static class PacketOwnedRewardResultRuntime
    {
        public const int RandomMesoBagStatusBarChatType = 12;
        public const int RandomMesoBagSoundVolume = 100;
        private const int MesoGiveSucceededStringPoolId = 0x32E;
        private const int MesoGiveFailedStringPoolId = 0x32F;
        private const int UtilDlgNoticeBackgroundStringPoolId = 0x03D0;
        private const int UtilDlgDefaultSoundStringPoolId = 0x04F8;
        private const int UtilDlgCloseButtonStringPoolId = 0x1961;
        private const int UtilDlgOkButtonStringPoolId = 0x1963;
        private const int UtilDlgSeparatorStringPoolId = 0x1965;
        private const int UtilDlgCenterStringPoolId = 0x1966;
        private const int UtilDlgBottomStringPoolId = 0x1967;
        private const int UtilDlgTopStringPoolId = 0x196F;
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
            return TryDecodeRandomMesoBagSucceeded(payload, out result, out _, out error);
        }

        public static bool TryDecodeRandomMesoBagSucceeded(
            byte[] payload,
            out PacketOwnedRandomMesoBagResult result,
            out int trailingByteCount,
            out string error)
        {
            result = null;
            trailingByteCount = 0;
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
                trailingByteCount = (int)Math.Max(0, stream.Length - stream.Position);

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

        public static bool TryDecodeMesoGiveSucceeded(byte[] payload, out uint mesoAmount, out string error)
        {
            return TryDecodeMesoGiveSucceeded(payload, out mesoAmount, out _, out error);
        }

        public static bool TryDecodeMesoGiveSucceeded(
            byte[] payload,
            out uint mesoAmount,
            out int trailingByteCount,
            out string error)
        {
            mesoAmount = 0;
            trailingByteCount = 0;
            error = null;

            if (payload == null || payload.Length < sizeof(uint))
            {
                error = "Meso-give success payload must contain the mesos amount.";
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream, Encoding.ASCII, leaveOpen: false);

                mesoAmount = reader.ReadUInt32();
                trailingByteCount = (int)Math.Max(0, stream.Length - stream.Position);

                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException)
            {
                error = $"Meso-give success payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        public static bool TryDecodeMesoGiveFailed(
            byte[] payload,
            out int trailingByteCount,
            out string error)
        {
            trailingByteCount = payload?.Length ?? 0;
            error = null;
            return true;
        }

        public static bool TryDecodeRandomMesoBagFailed(
            byte[] payload,
            out int trailingByteCount,
            out string error)
        {
            trailingByteCount = payload?.Length ?? 0;
            error = null;
            return true;
        }

        public static string FormatMesoGiveSucceededText(uint mesoAmount)
        {
            string format = ResolveTextFormat(
                MesoGiveSucceededStringPoolId,
                "You have received {0:N0} mesos.",
                1);
            return FormatInvariant(format, (ulong)mesoAmount);
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

        public static string GetUtilDlgNoticeSoundDescriptor()
        {
            return ResolveAssetPath(
                UtilDlgDefaultSoundStringPoolId,
                "Sound/UI.img/DlgNotice");
        }

        public static string GetUtilDlgNoticeBackgroundResourcePath()
        {
            return ResolveAssetPath(
                UtilDlgNoticeBackgroundStringPoolId,
                "UI/UIWindow2.img/UtilDlgEx/notice");
        }

        public static string GetUtilDlgNoticeTopResourcePath()
        {
            return ResolveAssetPath(
                UtilDlgTopStringPoolId,
                "UI/UIWindow.img/UtilDlgEx/t");
        }

        public static string GetUtilDlgNoticeCenterResourcePath()
        {
            return ResolveAssetPath(
                UtilDlgCenterStringPoolId,
                "UI/UIWindow.img/UtilDlgEx/c");
        }

        public static string GetUtilDlgNoticeBottomResourcePath()
        {
            return ResolveAssetPath(
                UtilDlgBottomStringPoolId,
                "UI/UIWindow.img/UtilDlgEx/s");
        }

        public static string GetUtilDlgNoticeSeparatorResourcePath()
        {
            return ResolveAssetPath(
                UtilDlgSeparatorStringPoolId,
                "UI/UIWindow.img/UtilDlgEx/line");
        }

        public static string GetUtilDlgNoticeOkButtonResourcePath()
        {
            return ResolveAssetPath(
                UtilDlgOkButtonStringPoolId,
                "UI/UIWindow.img/UtilDlgEx/BtOK");
        }

        public static string GetUtilDlgNoticeCloseButtonResourcePath()
        {
            return ResolveAssetPath(
                UtilDlgCloseButtonStringPoolId,
                "UI/UIWindow.img/UtilDlgEx/BtClose");
        }

        public static PacketOwnedRandomMesoBagPresentation CreateRandomMesoBagPresentation(byte rank, int mesoAmount)
        {
            int normalizedRank = NormalizeRandomMesoBagClientRank(rank);
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
                ChatMessageType = RandomMesoBagStatusBarChatType,
                SoundDescriptor = ResolveRandomMesoBagSound(normalizedRank),
                SoundVolume = RandomMesoBagSoundVolume
            };
        }

        public static string GetRandomMesoBagDialogResourcePath(int rank)
        {
            return ResolveRandomMesoBagDialogResourcePath(NormalizeRandomMesoBagClientRank(rank));
        }

        public static string GetRandomMesoBagOkButtonResourcePath()
        {
            return ResolveAssetPath(
                RandomMesoBagOkButtonStringPoolId,
                "UI/UIWindow.img/RandomMesoBag/BtOk");
        }

        public static float ResolveClientSoundVolumeScale(int clientVolume)
        {
            return Math.Clamp(clientVolume, 0, 100) / 100f;
        }

        internal static bool IsUtilDlgNoticeShellResourcePath(string resourcePath)
        {
            string normalizedPath = NormalizeAssetPath(resourcePath);
            return !string.IsNullOrWhiteSpace(normalizedPath)
                && normalizedPath.EndsWith("/utildlgex/notice", StringComparison.OrdinalIgnoreCase);
        }

        internal static int NormalizeRandomMesoBagClientRank(int rank)
        {
            return rank switch
            {
                2 => 2,
                3 => 3,
                4 => 4,
                _ => 1
            };
        }

        internal static string GetAssetParentPath(string resourcePath, int levels = 1)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                return string.Empty;
            }

            string normalizedPath = NormalizeAssetPath(resourcePath);
            int trimLevels = Math.Max(1, levels);
            for (int i = 0; i < trimLevels; i++)
            {
                int separatorIndex = normalizedPath.LastIndexOf('/');
                if (separatorIndex <= 0)
                {
                    return string.Empty;
                }

                normalizedPath = normalizedPath[..separatorIndex];
            }

            return normalizedPath;
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

            return ResolveAssetPath(
                stringPoolId,
                $"UI/UIWindow.img/RandomMesoBag/Back{NormalizeRandomMesoBagClientRank(rank)}");
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

            return ResolveAssetPath(stringPoolId, fallback);
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

        private static string ResolveAssetPath(int stringPoolId, string fallbackPath)
        {
            string resolved = MapleStoryStringPool.GetOrFallback(stringPoolId, fallbackPath);
            if (string.IsNullOrWhiteSpace(resolved) || !LooksLikeAssetPath(resolved))
            {
                return fallbackPath;
            }

            return resolved.Replace('\\', '/');
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

        private static string NormalizeAssetPath(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Replace('\\', '/');
        }
    }
}
