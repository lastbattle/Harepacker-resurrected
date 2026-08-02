using System;
using System.Collections.Generic;
using System.Text;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class ClientCurseProcessParity
    {
        private const int InappropriateContentStringPoolId = 0x11D;
        private static readonly Encoding ClientEncoding = Encoding.Default;

        // Recovered from MapleStory.exe v95 `CCurseProcess::s_FilterChars`.
        private static readonly byte[] FilteredCharacters =
        {
            0x20,
            0x5F,
            0x2D,
            0x3A,
            0x09,
            0x5E,
            0x2E,
            0x2C,
            0x2A,
            0x2F,
            0x3B,
            0x21,
            0x5C,
            0x27,
            0x22,
            0x60,
            0x2B
        };

        // Fallback extracted from `Etc/Curse.img` so tests and headless paths keep the
        // client-shaped board filter even when WZ data has not been initialized yet.
        private static readonly string[] FallbackBlockedTerms =
        {
            "aiheh",
            "amput",
            "anak",
            "anal",
            "analsex",
            "anjuk",
            "asscrack",
            "asshole",
            "asskisser",
            "asslover",
            "assmaster",
            "assmuch",
            "assmunch",
            "asswipe",
            "babi",
            "badus",
            "bahlul",
            "balls",
            "ballz",
            "bantaton",
            "bapuk",
            "barua",
            "bastard",
            "batang",
            "bawah",
            "belakang",
            "bengkok",
            "berapi",
            "biaatch",
            "biatch",
            "biiiitch",
            "biiitch",
            "biitch",
            "bijik",
            "biotch",
            "bitch",
            "bitchass",
            "bittch",
            "biyaaatch",
            "biyatch",
            "biyotch",
            "bizatch",
            "biznatch",
            "bllltch",
            "blotch",
            "blowjob",
            "blowme",
            "bltch",
            "blyotch",
            "bodoh",
            "burit",
            "busuk",
            "butoh",
            "buttmunch",
            "buttocks",
            "bytch",
            "c8",
            "cb",
            "ccb",
            "celaka",
            "chebye",
            "cheeby",
            "cheebye",
            "chinhooi",
            "chink",
            "choochie",
            "ciao",
            "cibai",
            "cingkak",
            "cingkolou",
            "cipap",
            "clit",
            "clitoris",
            "cock",
            "condom",
            "cork",
            "cottonpick",
            "cum",
            "cunnt",
            "cunt",
            "damn",
            "deepthroat",
            "dick",
            "dildo",
            "dlldo",
            "doggystyle",
            "dumbfuck",
            "eatme",
            "fag",
            "faggot",
            "fark",
            "fauk",
            "fetish",
            "fuc",
            "fuck",
            "fucker",
            "fuk",
            "fuker",
            "fuuk",
            "gaaay",
            "gaay",
            "gampang",
            "gatal",
            "gay",
            "gizay",
            "goddamn",
            "goddmamn",
            "gook",
            "hanyun",
            "haram",
            "havesex",
            "hayun",
            "henjut",
            "hisap",
            "homo",
            "honggan",
            "hoochie",
            "hooters",
            "iut",
            "jackoff",
            "jalang",
            "jantan",
            "japs",
            "jerkme",
            "jerkoff",
            "jiao",
            "jilat",
            "jiz",
            "jizm",
            "jubur",
            "kanni",
            "katak",
            "kecing",
            "kecut",
            "kelentit",
            "kepala",
            "kerang",
            "kike",
            "knn",
            "kodok",
            "konek",
            "kopek",
            "kote",
            "kulum",
            "kurap",
            "lahanat",
            "lanchiau",
            "lanjut",
            "lebeh",
            "lempuduk",
            "lendir",
            "lesbian",
            "lesbo",
            "lezbo",
            "loyot",
            "mamak",
            "mampus",
            "mangkuk",
            "mastabate",
            "mastarbate",
            "masterbate",
            "masturbate",
            "melancap",
            "miang",
            "missionary",
            "mofucc",
            "mothafuc",
            "mulut",
            "mutha",
            "mytit",
            "nabei",
            "negro",
            "nenen",
            "neraka",
            "ngongkek",
            "niga",
            "nigar",
            "niger",
            "nigga",
            "niggar",
            "nigger",
            "nikah",
            "nipple",
            "nonok",
            "nutsack",
            "nyah",
            "orgasm",
            "orgy",
            "palat",
            "pantat",
            "pelacur",
            "peler",
            "penis",
            "pepek",
            "perempuan",
            "pergi",
            "phuck",
            "pondan",
            "porn",
            "porno",
            "pukek",
            "puki",
            "pussie",
            "pussy",
            "retard",
            "rubmy",
            "schlong",
            "sexfreak",
            "sexmachine",
            "sexual",
            "sexwith",
            "sh1t",
            "shibal",
            "shit",
            "shiz",
            "shlt",
            "sial",
            "siokuntong",
            "spank",
            "sperm",
            "spum",
            "ssh1t",
            "sshit",
            "sshlt",
            "suckme",
            "suckmy",
            "sundal",
            "tantalau",
            "telur",
            "tersengih",
            "tetek",
            "titty",
            "toceng",
            "toli",
            "tonton",
            "tonyok",
            "totok",
            "twat",
            "vagina",
            "wackoff",
            "wanker",
            "whore",
            "yetmeh",
            "yourtit"
        };

        private static readonly Lazy<HashSet<string>> BlockedTerms = new(LoadBlockedTerms);

        internal static bool TryValidateText(string text, out string notice)
        {
            if (ContainsBlockedContent(text))
            {
                notice = GetInappropriateContentNotice();
                return false;
            }

            notice = null;
            return true;
        }

        internal static bool TryProcessString(
            string text,
            bool ignoreNewLine,
            out string processedText,
            out bool filtered,
            out string notice)
        {
            string source = text ?? string.Empty;
            filtered = false;
            notice = null;
            processedText = source;
            if (string.IsNullOrEmpty(source))
            {
                return true;
            }

            if (!TryConvertBlockedContent(source, ignoreNewLine, out string convertedText, out bool converted))
            {
                notice = GetInappropriateContentNotice();
                return false;
            }

            processedText = convertedText;
            filtered = converted;
            return true;
        }

        internal static string GetInappropriateContentNotice()
        {
            return MapleStoryStringPool.GetOrFallback(
                InappropriateContentStringPoolId,
                "Inappropriate Content",
                appendFallbackSuffix: false,
                minimumHexWidth: 0);
        }

        internal static string FilterTextForClientDisplay(string text)
        {
            return ContainsBlockedContent(text)
                ? GetInappropriateContentNotice()
                : text ?? string.Empty;
        }

        private static bool ContainsBlockedContent(string text)
        {
            string normalized = Normalize(text);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            foreach (string blockedTerm in BlockedTerms.Value)
            {
                if (normalized.Contains(blockedTerm, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryConvertBlockedContent(string text, bool ignoreNewLine, out string convertedText, out bool converted)
        {
            convertedText = text ?? string.Empty;
            converted = false;
            if (string.IsNullOrEmpty(convertedText))
            {
                return true;
            }

            string lowered = convertedText.ToLowerInvariant();
            var normalizedBuilder = new StringBuilder(lowered.Length);
            var normalizedToSourceMap = new List<int>(lowered.Length);
            for (int index = 0; index < lowered.Length; index++)
            {
                char value = lowered[index];
                if (ignoreNewLine && (value == '\r' || value == '\n'))
                {
                    continue;
                }

                if (ContainsFilteredCharacter(value))
                {
                    continue;
                }

                normalizedBuilder.Append(value);
                normalizedToSourceMap.Add(index);
            }

            if (normalizedBuilder.Length == 0)
            {
                return true;
            }

            string normalized = normalizedBuilder.ToString();
            bool[] maskedSourceIndices = new bool[lowered.Length];
            foreach (string blockedTerm in BlockedTerms.Value)
            {
                if (string.IsNullOrWhiteSpace(blockedTerm))
                {
                    continue;
                }

                int searchIndex = 0;
                while (searchIndex < normalized.Length)
                {
                    int matchIndex = normalized.IndexOf(blockedTerm, searchIndex, StringComparison.Ordinal);
                    if (matchIndex < 0)
                    {
                        break;
                    }

                    for (int offset = 0; offset < blockedTerm.Length; offset++)
                    {
                        int normalizedIndex = matchIndex + offset;
                        if (normalizedIndex >= 0 && normalizedIndex < normalizedToSourceMap.Count)
                        {
                            int sourceIndex = normalizedToSourceMap[normalizedIndex];
                            if (sourceIndex >= 0 && sourceIndex < maskedSourceIndices.Length)
                            {
                                maskedSourceIndices[sourceIndex] = true;
                            }
                        }
                    }

                    searchIndex = matchIndex + 1;
                }
            }

            char[] output = convertedText.ToCharArray();
            for (int index = 0; index < output.Length; index++)
            {
                if (!maskedSourceIndices[index])
                {
                    continue;
                }

                output[index] = '*';
                converted = true;
            }

            convertedText = converted
                ? new string(output)
                : convertedText;
            return true;
        }

        private static HashSet<string> LoadBlockedTerms()
        {
            HashSet<string> blockedTerms = new(StringComparer.Ordinal);

            TryLoadTermsFromWz(blockedTerms);

            if (blockedTerms.Count == 0)
            {
                foreach (string term in FallbackBlockedTerms)
                {
                    blockedTerms.Add(term);
                }
            }

            return blockedTerms;
        }

        private static void TryLoadTermsFromWz(HashSet<string> blockedTerms)
        {
            try
            {
                WzImage curseImage = Program.FindImage("etc", "Curse.img");
                if (curseImage == null)
                {
                    return;
                }

                bool parsedHere = !curseImage.Parsed;
                if (parsedHere)
                {
                    curseImage.ParseImage();
                }

                try
                {
                    foreach (WzStringProperty property in curseImage.WzProperties)
                    {
                        AddBlockedTerm(blockedTerms, property?.Value);
                    }
                }
                finally
                {
                    if (parsedHere)
                    {
                        curseImage.UnparseImage();
                    }
                }
            }
            catch
            {
                // Keep the fallback list alive when WZ data is unavailable.
            }
        }

        private static void AddBlockedTerm(HashSet<string> blockedTerms, string rawValue)
        {
            string term = ExtractBlockedTerm(rawValue);
            string normalized = Normalize(term);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                blockedTerms.Add(normalized);
            }
        }

        private static string ExtractBlockedTerm(string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return string.Empty;
            }

            int separatorIndex = rawValue.IndexOf(',');
            return separatorIndex >= 0
                ? rawValue[..separatorIndex]
                : rawValue;
        }

        private static string Normalize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            byte[] sourceBytes = ClientEncoding.GetBytes(text.ToLowerInvariant());
            byte[] filteredBytes = new byte[sourceBytes.Length];
            int count = 0;

            foreach (byte value in sourceBytes)
            {
                if (ContainsFilteredCharacter(value))
                {
                    continue;
                }

                filteredBytes[count++] = value;
            }

            return count == 0
                ? string.Empty
                : ClientEncoding.GetString(filteredBytes, 0, count);
        }

        private static bool ContainsFilteredCharacter(byte value)
        {
            foreach (byte filteredCharacter in FilteredCharacters)
            {
                if (filteredCharacter == value)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsFilteredCharacter(char value)
        {
            if (value > byte.MaxValue)
            {
                return false;
            }

            return ContainsFilteredCharacter((byte)value);
        }
    }
}
