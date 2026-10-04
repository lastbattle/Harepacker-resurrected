using System;
using System.Collections.Generic;
using System.Globalization;

namespace HaCreator.MapSimulator.Managers
{
    internal static class LoginAccountDialogPacketProfileFormatter
    {
        public static string BuildInlineSummary(LoginAccountDialogPacketProfile profile)
        {
            return BuildSummary(profile, " | ");
        }

        public static string BuildDetailBlock(LoginAccountDialogPacketProfile profile)
        {
            return BuildSummary(profile, "\r\n");
        }

        private static string BuildSummary(LoginAccountDialogPacketProfile profile, string separator)
        {
            if (profile == null)
            {
                return null;
            }

            List<string> details = new();
            Append(details, "Source", string.IsNullOrWhiteSpace(profile.Source) ? null : profile.Source.Trim());
            Append(details, "Result code", profile.ResultCode);
            Append(details, "Secondary code", profile.SecondaryCode);
            Append(details, "Account id", profile.AccountId);
            Append(details, "Character id", profile.CharacterId);
            Append(details, "Gender", FormatGender(profile.Gender));
            Append(details, "Grade code", profile.GradeCode);
            Append(details, "Account flags", profile.AccountFlags.HasValue ? $"0x{profile.AccountFlags.Value:X4}" : null);
            Append(details, "Country id", profile.CountryId);
            Append(details, "Club id", string.IsNullOrWhiteSpace(profile.ClubId) ? null : profile.ClubId.Trim());
            Append(details, "Purchase exp", profile.PurchaseExperience);
            Append(details, "Chat block reason", profile.ChatBlockReason);
            Append(details, "Chat unblock", FormatFileTime(profile.ChatUnblockFileTime));
            Append(details, "Register date", FormatFileTime(profile.RegisterDateFileTime));
            Append(details, "Character count", profile.CharacterCount);
            Append(details, "Client key", FormatClientKey(profile.ClientKey));
            Append(details, "Requested name", string.IsNullOrWhiteSpace(profile.RequestedName) ? null : profile.RequestedName.Trim());
            return details.Count == 0 ? null : string.Join(separator, details);
        }

        private static void Append(List<string> details, string label, object value)
        {
            if (details == null || string.IsNullOrWhiteSpace(label) || value == null)
            {
                return;
            }

            string text = value as string ?? Convert.ToString(value, CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            details.Add($"{label}: {text}");
        }

        private static string FormatGender(byte? gender)
        {
            return gender switch
            {
                0 => "Male (0)",
                1 => "Female (1)",
                byte value => $"Unknown ({value})",
                _ => null,
            };
        }

        private static string FormatFileTime(long? fileTime)
        {
            if (!fileTime.HasValue || fileTime.Value <= 0)
            {
                return null;
            }

            try
            {
                return DateTime.FromFileTimeUtc(fileTime.Value).ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);
            }
            catch (ArgumentOutOfRangeException)
            {
                return fileTime.Value.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static string FormatClientKey(byte[] clientKey)
        {
            if (clientKey == null || clientKey.Length == 0)
            {
                return null;
            }

            return Convert.ToHexString(clientKey);
        }
    }
}
