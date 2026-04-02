using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.UI;
using MapleLib.PacketLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class PacketScriptMessageRuntime
    {
        private const short OutboundScriptAnswerOpcode = 65;
        private string _statusMessage = "Packet-owned script-message idle.";
        private PacketScriptPromptContext _activePromptContext;

        internal void Clear()
        {
            _activePromptContext = null;
            _statusMessage = "Packet-owned script-message cleared.";
        }

        internal string DescribeStatus()
        {
            return _statusMessage;
        }

        internal bool TryDecode(
            byte[] payload,
            Func<int, NpcItem> findNpcById,
            NpcItem activeNpc,
            out PacketScriptMessageOpenRequest request,
            out string message)
        {
            request = null;
            payload ??= Array.Empty<byte>();
            if (payload.Length == 0)
            {
                message = "Script-message payload is empty.";
                return false;
            }

            try
            {
                using var stream = new MemoryStream(payload, writable: false);
                using var reader = new BinaryReader(stream, Encoding.Default, leaveOpen: false);
                int speakerTypeId = reader.ReadByte();
                int speakerTemplateId = reader.ReadInt32();
                byte messageType = reader.ReadByte();
                byte param = reader.ReadByte();

                PacketScriptSpeaker speaker = ResolveSpeaker(speakerTypeId, speakerTemplateId, findNpcById, activeNpc);
                NpcInteractionEntry entry = messageType switch
                {
                    0 => DecodeSay(reader, speaker, ref speakerTemplateId, param),
                    1 => DecodeSayImage(reader, speaker, param),
                    2 => DecodeAskYesNo(reader, speaker, param, false),
                    3 => DecodeAskText(reader, speaker, param),
                    4 => DecodeAskNumber(reader, speaker, param),
                    5 => DecodeAskMenu(reader, speaker, param),
                    6 => DecodeAskQuiz(reader, speaker, param),
                    7 => DecodeAskSpeedQuiz(reader, speaker, param),
                    8 => DecodeAskAvatar(reader, speaker, param, false),
                    9 => DecodeAskAvatar(reader, speaker, param, true),
                    10 => DecodeAskPet(reader, speaker, param, false),
                    11 => DecodeAskPet(reader, speaker, param, true),
                    12 => DecodeAskSlideMenu(reader, speaker, param),
                    13 => DecodeAskYesNo(reader, speaker, param, true),
                    14 => DecodeAskBoxText(reader, speaker, param),
                    15 => DecodeAskSlideMenu(reader, speaker, param),
                    _ => DecodeUnsupported(reader, speaker, messageType, param)
                };

                if (stream.Position != stream.Length)
                {
                    entry = AppendTrailingByteNotice(entry, (int)(stream.Length - stream.Position));
                }

                request = new PacketScriptMessageOpenRequest(
                    new NpcInteractionState
                    {
                        NpcName = speaker.DisplayName,
                        Entries = new[] { entry },
                        SelectedEntryId = entry.EntryId,
                        PresentationStyle = NpcInteractionPresentationStyle.PacketScriptUtilDialog
                    },
                    speaker.NpcId);
                if (entry == null)
                {
                    request = new PacketScriptMessageOpenRequest(null, speaker.NpcId, CloseExistingDialog: true);
                    _activePromptContext = null;
                    _statusMessage = $"Closed packet-authored script dialog for {speaker.DisplayName}.";
                    message = _statusMessage;
                    return true;
                }

                _activePromptContext = new PacketScriptPromptContext(
                    speaker.DisplayName,
                    entry.EntryId,
                    entry.Title,
                    messageType,
                    param,
                    speaker.TemplateId,
                    speaker.NpcId);

                _statusMessage = $"Opened packet-authored script dialog: {entry.Title} for {speaker.DisplayName}.";
                message = _statusMessage;
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is IOException || ex is ArgumentException)
            {
                message = $"Script-message payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private static NpcInteractionEntry DecodeSay(BinaryReader reader, PacketScriptSpeaker speaker, ref int speakerTemplateId, byte param)
        {
            if ((param & 4) != 0)
            {
                speakerTemplateId = reader.ReadInt32();
                speaker = speaker.WithOverrideTemplateId(speakerTemplateId);
            }

            string rawText = ReadMapleString(reader);
            bool hasPrev = reader.ReadByte() != 0;
            bool hasNext = reader.ReadByte() != 0;
            return CreateEntry(
                "Talk",
                BuildSpeakerSubtitle(speaker, "Say", param),
                rawText,
                AppendMetadata(
                    NpcDialogueTextFormatter.Format(rawText),
                    BuildNavigationMetadata(hasPrev, hasNext)),
                null);
        }

        private static NpcInteractionEntry DecodeSayImage(BinaryReader reader, PacketScriptSpeaker speaker, byte param)
        {
            int count = reader.ReadByte();
            List<string> imagePaths = new();
            for (int i = 0; i < count; i++)
            {
                string imagePath = ReadMapleString(reader);
                if (!string.IsNullOrWhiteSpace(imagePath))
                {
                    imagePaths.Add(imagePath.Trim());
                }
            }

            string body = imagePaths.Count == 0
                ? "Packet-authored say-image prompt did not include any WZ image paths."
                : string.Join("\n", imagePaths.Select((path, index) => $"Image {index + 1}: {path}"));
            return CreateEntry(
                "Image Talk",
                BuildSpeakerSubtitle(speaker, "SayImage", param),
                body,
                AppendMetadata(
                    body,
                    $"Client `OnSayImage` opened {imagePaths.Count} image entr{(imagePaths.Count == 1 ? "y" : "ies")} through `CUtilDlgEx::SetUtilDlgEx_IMAGE`."),
                new[]
                {
                    CreateNumericResponseChoice("Continue", "Continue", 1),
                    CreateNumericResponseChoice("Cancel", "Cancel", -1)
                });
        }

        private static NpcInteractionEntry DecodeAskYesNo(BinaryReader reader, PacketScriptSpeaker speaker, byte param, bool isQuestPrompt)
        {
            string rawText = ReadMapleString(reader);
            List<NpcInteractionChoice> choices = new()
            {
                CreateResponseChoice("Yes", "Yes", 1),
                CreateResponseChoice("No", "No", 0)
            };

            return CreateEntry(
                isQuestPrompt ? "Quest Prompt" : "Yes / No",
                BuildSpeakerSubtitle(speaker, isQuestPrompt ? "AskYesNo(quest)" : "AskYesNo", param),
                rawText,
                AppendMetadata(
                    NpcDialogueTextFormatter.Format(rawText),
                    "Client path shows dedicated Yes / No buttons for this prompt."),
                choices);
        }

        private static NpcInteractionEntry DecodeAskText(BinaryReader reader, PacketScriptSpeaker speaker, byte param)
        {
            string rawText = ReadMapleString(reader);
            string defaultText = ReadMapleString(reader);
            short minLength = reader.ReadInt16();
            short maxLength = reader.ReadInt16();
            return CreateEntry(
                "Text Input",
                BuildSpeakerSubtitle(speaker, "AskText", param),
                rawText,
                AppendMetadata(
                    NpcDialogueTextFormatter.Format(rawText),
                    $"Default text: {FormatQuotedValue(defaultText)}",
                    $"Accepted length: {minLength} to {maxLength} character(s)."),
                null,
                new NpcInteractionInputRequest
                {
                    Kind = NpcInteractionInputKind.Text,
                    DefaultValue = defaultText,
                    MinLength = Math.Max(0, (int)minLength),
                    MaxLength = Math.Max(Math.Max(0, (int)minLength), (int)maxLength)
                });
        }

        private static NpcInteractionEntry DecodeAskNumber(BinaryReader reader, PacketScriptSpeaker speaker, byte param)
        {
            string rawText = ReadMapleString(reader);
            int defaultValue = reader.ReadInt32();
            int minValue = reader.ReadInt32();
            int maxValue = reader.ReadInt32();
            return CreateEntry(
                "Number Input",
                BuildSpeakerSubtitle(speaker, "AskNumber", param),
                rawText,
                AppendMetadata(
                    NpcDialogueTextFormatter.Format(rawText),
                    $"Default value: {defaultValue}",
                    $"Accepted range: {minValue} to {maxValue}."),
                null,
                new NpcInteractionInputRequest
                {
                    Kind = NpcInteractionInputKind.Number,
                    DefaultValue = defaultValue.ToString(),
                    MinValue = minValue,
                    MaxValue = maxValue
                });
        }

        private static NpcInteractionEntry DecodeAskMenu(BinaryReader reader, PacketScriptSpeaker speaker, byte param)
        {
            string rawText = ReadMapleString(reader);
            NpcInlineSelection[] selections = NpcDialogueTextFormatter.ExtractInlineSelections(rawText);
            List<NpcInteractionChoice> choices = selections
                .Select(selection =>
                {
                    string label = NpcDialogueTextFormatter.Format(selection.Label);
                    return CreateResponseChoice(label, $"{label} (id={selection.SelectionId})", selection.SelectionId);
                })
                .ToList();

            return CreateEntry(
                "Menu",
                BuildSpeakerSubtitle(speaker, "AskMenu", param),
                rawText,
                AppendMetadata(
                    NpcDialogueTextFormatter.Format(rawText),
                    choices.Count == 0
                        ? "No inline menu selections were decoded from the packet text."
                        : $"Decoded {choices.Count} inline menu selection(s)."),
                choices);
        }

        private static NpcInteractionEntry DecodeAskAvatar(BinaryReader reader, PacketScriptSpeaker speaker, byte param, bool isMembershopAvatar)
        {
            string rawText = ReadMapleString(reader);
            int count = reader.ReadByte();
            List<int> optionItemIds = new(count);
            for (int i = 0; i < count; i++)
            {
                optionItemIds.Add(reader.ReadInt32());
            }

            List<NpcInteractionChoice> choices = optionItemIds
                .Select((itemId, index) => CreateNumericResponseChoice(
                    BuildAvatarChoiceLabel(itemId, index),
                    $"index={index}, itemId={itemId}",
                    index))
                .ToList();
            string optionDetails = optionItemIds.Count == 0
                ? "No avatar options were decoded from the packet payload."
                : string.Join("\n", optionItemIds.Select((itemId, index) => $"{index + 1}. {DescribeAvatarOption(itemId)}"));

            return CreateEntry(
                isMembershopAvatar ? "Member Shop Avatar" : "Avatar Selection",
                BuildSpeakerSubtitle(speaker, isMembershopAvatar ? "AskMembershopAvatar" : "AskAvatar", param),
                rawText,
                AppendMetadata(
                    NpcDialogueTextFormatter.Format(rawText),
                    optionDetails,
                    $"Client `CUtilDlgEx::SetUtilDlgEx_AVATAR` opened {optionItemIds.Count} indexed avatar option(s)."),
                choices);
        }

        private static NpcInteractionEntry DecodeAskQuiz(BinaryReader reader, PacketScriptSpeaker speaker, byte param)
        {
            long startPosition = reader.BaseStream.Position;
            if (TryDecodeAskQuizClientPacket(reader, speaker, param, out NpcInteractionEntry entry))
            {
                return entry;
            }

            reader.BaseStream.Position = startPosition;
            return DecodeAskQuizCompactPacket(reader, speaker, param);
        }

        private static NpcInteractionEntry DecodeAskSpeedQuiz(BinaryReader reader, PacketScriptSpeaker speaker, byte param)
        {
            long startPosition = reader.BaseStream.Position;
            if (TryDecodeAskSpeedQuizClientPacket(reader, speaker, param, out NpcInteractionEntry entry))
            {
                return entry;
            }

            reader.BaseStream.Position = startPosition;
            return DecodeAskSpeedQuizCompactPacket(reader, speaker, param);
        }

        private static NpcInteractionEntry DecodeAskPet(BinaryReader reader, PacketScriptSpeaker speaker, byte param, bool isPetAll)
        {
            long startPosition = reader.BaseStream.Position;
            if (TryDecodeAskPetClientPacket(reader, speaker, param, isPetAll, out NpcInteractionEntry entry))
            {
                return entry;
            }

            reader.BaseStream.Position = startPosition;
            return DecodeAskPetCompactPacket(reader, speaker, param, isPetAll);
        }

        private static NpcInteractionEntry DecodeAskSlideMenu(BinaryReader reader, PacketScriptSpeaker speaker, byte param)
        {
            long startPosition = reader.BaseStream.Position;
            if (TryDecodeAskSlideMenuClientPacket(reader, speaker, param, out NpcInteractionEntry entry))
            {
                return entry;
            }

            reader.BaseStream.Position = startPosition;
            return DecodeAskSlideMenuCompactPacket(reader, speaker, param);
        }

        private static NpcInteractionEntry DecodeAskBoxText(BinaryReader reader, PacketScriptSpeaker speaker, byte param)
        {
            string rawText = ReadMapleString(reader);
            string defaultText = ReadMapleString(reader);
            short columnCount = reader.ReadInt16();
            short lineCount = reader.ReadInt16();
            return CreateEntry(
                "Multi-line Input",
                BuildSpeakerSubtitle(speaker, "AskBoxText", param),
                rawText,
                AppendMetadata(
                    NpcDialogueTextFormatter.Format(rawText),
                    $"Default text: {FormatQuotedValue(defaultText)}",
                    $"Input box size: {columnCount} column(s) x {lineCount} line(s)."),
                null,
                new NpcInteractionInputRequest
                {
                    Kind = NpcInteractionInputKind.MultiLineText,
                    DefaultValue = defaultText,
                    ColumnCount = Math.Max(1, (int)columnCount),
                    LineCount = Math.Max(1, (int)lineCount),
                    MaxLength = Math.Max(1, (int)columnCount) * Math.Max(1, (int)lineCount)
                });
        }

        private static bool TryDecodeAskQuizClientPacket(BinaryReader reader, PacketScriptSpeaker speaker, byte param, out NpcInteractionEntry entry)
        {
            long startPosition = reader.BaseStream.Position;
            entry = null;
            try
            {
                if (!TryReadByte(reader, out byte mode))
                {
                    reader.BaseStream.Position = startPosition;
                    return false;
                }

                if (mode == 1 && reader.BaseStream.Position == reader.BaseStream.Length)
                {
                    entry = null;
                    return true;
                }

                string title = ReadMapleString(reader);
                string problemText = ReadMapleString(reader);
                string hintText = ReadMapleString(reader);
                int correctAnswer = reader.ReadInt32();
                int questionNumber = reader.ReadInt32();
                int remainingSeconds = reader.ReadInt32();

                entry = CreateEntry(
                    "Quiz",
                    BuildSpeakerSubtitle(speaker, "AskQuiz", param, mode),
                    problemText,
                    AppendMetadata(
                        $"Title: {FormatQuotedValue(title)}",
                        NpcDialogueTextFormatter.Format(problemText),
                        string.IsNullOrWhiteSpace(hintText) ? null : $"Hint text: {FormatQuotedValue(hintText)}",
                        $"Client payload: answer={correctAnswer}, questionNo={questionNumber}, remaining={remainingSeconds} sec.",
                        "WZ data exposes a dedicated `UIWindow(.img|2.img)/InitialQuiz` surface for this packet-owned prompt family."),
                    new[]
                    {
                        CreateNumericResponseChoice("OK", "OK", 1),
                        CreateNumericResponseChoice("Next", "Next", 2),
                        CreateNumericResponseChoice("Give Up", "Give Up", 0)
                    });
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is IOException || ex is ArgumentException)
            {
                reader.BaseStream.Position = startPosition;
                entry = null;
                return false;
            }
        }

        private static NpcInteractionEntry DecodeAskQuizCompactPacket(BinaryReader reader, PacketScriptSpeaker speaker, byte param)
        {
            string rawText = ReadMapleString(reader);
            int defaultValue = reader.ReadInt32();
            int minValue = reader.ReadInt32();
            int maxValue = reader.ReadInt32();
            return CreateEntry(
                "Quiz",
                BuildSpeakerSubtitle(speaker, "AskQuiz", param),
                rawText,
                AppendMetadata(
                    NpcDialogueTextFormatter.Format(rawText),
                    $"Compact helper payload: default={defaultValue}, range={minValue}..{maxValue}.",
                    "WZ data exposes a dedicated `UIWindow(.img|2.img)/InitialQuiz` surface for this packet-owned prompt family."),
                null,
                new NpcInteractionInputRequest
                {
                    Kind = NpcInteractionInputKind.Number,
                    DefaultValue = defaultValue.ToString(),
                    MinValue = minValue,
                    MaxValue = maxValue
                });
        }

        private static bool TryDecodeAskSpeedQuizClientPacket(BinaryReader reader, PacketScriptSpeaker speaker, byte param, out NpcInteractionEntry entry)
        {
            long startPosition = reader.BaseStream.Position;
            entry = null;
            try
            {
                if (!TryReadByte(reader, out byte mode))
                {
                    reader.BaseStream.Position = startPosition;
                    return false;
                }

                if (mode == 1 && reader.BaseStream.Position == reader.BaseStream.Length)
                {
                    entry = null;
                    return true;
                }

                int currentQuestion = reader.ReadInt32();
                int totalQuestions = reader.ReadInt32();
                int correctAnswers = reader.ReadInt32();
                int remainingQuestions = reader.ReadInt32();
                int remainingSeconds = reader.ReadInt32();

                entry = CreateEntry(
                    "Speed Quiz",
                    BuildSpeakerSubtitle(speaker, "AskSpeedQuiz", param, mode),
                    string.Empty,
                    AppendMetadata(
                        $"Question {currentQuestion} / {totalQuestions}",
                        $"Correct answers: {correctAnswers}",
                        $"Questions remaining: {remainingQuestions}",
                        $"Time remaining: {remainingSeconds} sec.",
                        "WZ data exposes `UIWindow(.img|2.img)/SpeedQuiz` with dedicated OK / Next / Give up controls."),
                    new[]
                    {
                        CreateNumericResponseChoice("OK", "OK", 1),
                        CreateNumericResponseChoice("Next", "Next", 2),
                        CreateNumericResponseChoice("Give Up", "Give Up", 0)
                    });
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is IOException || ex is ArgumentException)
            {
                reader.BaseStream.Position = startPosition;
                entry = null;
                return false;
            }
        }

        private static NpcInteractionEntry DecodeAskSpeedQuizCompactPacket(BinaryReader reader, PacketScriptSpeaker speaker, byte param)
        {
            string rawText = ReadMapleString(reader);
            string defaultText = ReadMapleString(reader);
            IReadOnlyList<string> options = TryReadMapleStringList(reader, out IReadOnlyList<string> decodedOptions)
                ? decodedOptions
                : Array.Empty<string>();
            List<NpcInteractionChoice> choices = options
                .Select((option, index) => CreateNumericResponseChoice(
                    $"{index + 1}. {NpcDialogueTextFormatter.Format(option)}",
                    option,
                    index))
                .ToList();

            return CreateEntry(
                "Speed Quiz",
                BuildSpeakerSubtitle(speaker, "AskSpeedQuiz", param),
                rawText,
                AppendMetadata(
                    NpcDialogueTextFormatter.Format(rawText),
                    string.IsNullOrWhiteSpace(defaultText) ? null : $"Default text: {FormatQuotedValue(defaultText)}",
                    choices.Count == 0
                        ? "Compact helper payload did not include explicit options."
                        : $"Compact helper payload exposed {choices.Count} answer option(s).",
                    "WZ data exposes `UIWindow(.img|2.img)/SpeedQuiz` with dedicated OK / Next / Give up controls."),
                choices);
        }

        private static bool TryDecodeAskPetClientPacket(BinaryReader reader, PacketScriptSpeaker speaker, byte param, bool isPetAll, out NpcInteractionEntry entry)
        {
            long startPosition = reader.BaseStream.Position;
            entry = null;
            try
            {
                string rawText = ReadMapleString(reader);
                if (!TryReadByte(reader, out byte count))
                {
                    reader.BaseStream.Position = startPosition;
                    return false;
                }

                bool exceptionExists = isPetAll && reader.ReadByte() != 0;
                List<long> petSerialNumbers = new(count);
                for (int i = 0; i < count; i++)
                {
                    petSerialNumbers.Add(reader.ReadInt64());
                    _ = reader.ReadByte();
                }

                entry = CreatePetSelectionEntry(
                    speaker,
                    param,
                    isPetAll,
                    rawText,
                    exceptionExists,
                    petSerialNumbers.Select((serialNumber, index) => new NpcInteractionChoice
                    {
                        Label = BuildPetChoiceLabel(serialNumber, index),
                        SubmitSelection = true,
                        SubmissionKind = NpcInteractionInputKind.None,
                        SubmissionValue = serialNumber.ToString(),
                        SubmissionNumericValue = null
                    }).ToList(),
                    petSerialNumbers.Count == 0
                        ? "No pet options were decoded from the packet payload."
                        : string.Join("\n", petSerialNumbers.Select((serialNumber, index) => $"{index + 1}. {DescribePetOption(serialNumber, index)}")));
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is IOException || ex is ArgumentException)
            {
                reader.BaseStream.Position = startPosition;
                entry = null;
                return false;
            }
        }

        private static NpcInteractionEntry DecodeAskPetCompactPacket(BinaryReader reader, PacketScriptSpeaker speaker, byte param, bool isPetAll)
        {
            string rawText = ReadMapleString(reader);
            if (!TryReadByte(reader, out byte count))
            {
                count = 0;
            }

            List<int> petItemIds = new(count);
            for (int i = 0; i < count; i++)
            {
                petItemIds.Add(reader.ReadInt32());
            }

            return CreatePetSelectionEntry(
                speaker,
                param,
                isPetAll,
                rawText,
                false,
                petItemIds.Select((itemId, index) => new NpcInteractionChoice
                {
                    Label = BuildPetItemChoiceLabel(itemId, index),
                    SubmitSelection = true,
                    SubmissionKind = NpcInteractionInputKind.None,
                    SubmissionValue = itemId.ToString(),
                    SubmissionNumericValue = null
                }).ToList(),
                petItemIds.Count == 0
                    ? "Compact helper payload did not decode any pet item ids."
                    : string.Join("\n", petItemIds.Select((itemId, index) => $"{index + 1}. {DescribePetItemOption(itemId, index)}")));
        }

        private static NpcInteractionEntry CreatePetSelectionEntry(
            PacketScriptSpeaker speaker,
            byte param,
            bool isPetAll,
            string rawText,
            bool exceptionExists,
            IReadOnlyList<NpcInteractionChoice> choices,
            string optionDetails)
        {
            return CreateEntry(
                isPetAll ? "Pet Selection (All)" : "Pet Selection",
                BuildSpeakerSubtitle(speaker, isPetAll ? "AskPetAll" : "AskPet", param),
                rawText,
                AppendMetadata(
                    NpcDialogueTextFormatter.Format(rawText),
                    optionDetails,
                    isPetAll ? $"Multi-pet exception flag: {(exceptionExists ? 1 : 0)}." : null,
                    "WZ data exposes packet-owned pet utility surfaces under `UIWindow(.img|2.img)/UtilDlgEx_Pet` and `UtilDlgEx_MultiPetEquip`."),
                choices);
        }

        private static string BuildPetItemChoiceLabel(int itemId, int index)
        {
            return $"{index + 1}. {DescribePetItemOption(itemId, index)}";
        }

        private static string DescribePetItemOption(int itemId, int index)
        {
            if (itemId <= 0)
            {
                return $"Pet option {index + 1}";
            }

            string itemName = null;
            if (HaCreator.Program.InfoManager?.ItemNameCache?.TryGetValue(itemId, out Tuple<string, string, string> itemInfo) == true)
            {
                itemName = itemInfo?.Item1;
            }

            itemName = string.IsNullOrWhiteSpace(itemName) ? $"Item {itemId}" : itemName.Trim();
            return $"{itemName} ({itemId})";
        }

        private static bool TryDecodeAskSlideMenuClientPacket(BinaryReader reader, PacketScriptSpeaker speaker, byte param, out NpcInteractionEntry entry)
        {
            long startPosition = reader.BaseStream.Position;
            entry = null;
            try
            {
                int slideMenuType = reader.ReadInt32();
                string buttonInfo = ReadMapleString(reader);
                IReadOnlyList<string> options = SplitSlideMenuOptions(buttonInfo);
                entry = CreateSlideMenuEntry(speaker, param, slideMenuType, buttonInfo, options, "Decoded");
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is IOException || ex is ArgumentException)
            {
                reader.BaseStream.Position = startPosition;
                entry = null;
                return false;
            }
        }

        private static NpcInteractionEntry DecodeAskSlideMenuCompactPacket(BinaryReader reader, PacketScriptSpeaker speaker, byte param)
        {
            string rawText = ReadMapleString(reader);
            byte slideMenuType = reader.ReadByte();
            IReadOnlyList<string> options = TryReadMapleStringList(reader, out IReadOnlyList<string> decodedOptions)
                ? decodedOptions
                : Array.Empty<string>();
            return CreateSlideMenuEntry(speaker, param, slideMenuType, rawText, options, "Compact helper payload exposed");
        }

        private static NpcInteractionEntry CreateSlideMenuEntry(
            PacketScriptSpeaker speaker,
            byte param,
            int slideMenuType,
            string rawText,
            IReadOnlyList<string> options,
            string decodedPrefix)
        {
            List<NpcInteractionChoice> choices = options
                .Select((option, index) => CreateNumericResponseChoice(
                    $"{index + 1}. {NpcDialogueTextFormatter.Format(option)}",
                    $"index={index}",
                    index))
                .ToList();

            return CreateEntry(
                "Slide Menu",
                BuildSpeakerSubtitle(speaker, "AskSlideMenu", param),
                rawText,
                AppendMetadata(
                    string.IsNullOrWhiteSpace(rawText) ? "No slide-menu prompt string was decoded." : NpcDialogueTextFormatter.Format(rawText),
                    choices.Count == 0
                        ? $"Slide-menu type {slideMenuType} did not expose any options."
                        : $"{decodedPrefix} {choices.Count} slide-menu option(s) for type {slideMenuType}.",
                    "WZ data exposes authored slide-menu variants under `UIWindow(.img|2.img)/SlideMenu`."),
                choices);
        }

        private static NpcInteractionEntry DecodeUnsupported(BinaryReader reader, PacketScriptSpeaker speaker, int messageType, byte param)
        {
            string remainingHex = reader.BaseStream.Position >= reader.BaseStream.Length
                ? "none"
                : BitConverter.ToString(reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position))).Replace("-", string.Empty, StringComparison.Ordinal);
            return CreateEntry(
                $"Message Type {messageType}",
                BuildSpeakerSubtitle(speaker, "Unsupported", param),
                string.Empty,
                AppendMetadata(
                    $"Packet-authored script message type {messageType} is not decoded by the simulator yet.",
                    $"Remaining payload: {remainingHex}",
                    "The client routes this through dedicated CScriptMan handlers and UI assets."),
                null);
        }

        private static NpcInteractionEntry AppendTrailingByteNotice(NpcInteractionEntry entry, int trailingBytes)
        {
            if (entry == null || trailingBytes <= 0 || entry.Pages.Count == 0)
            {
                return entry;
            }

            NpcInteractionPage page = entry.Pages[0];
            return new NpcInteractionEntry
            {
                EntryId = entry.EntryId,
                QuestId = entry.QuestId,
                Kind = entry.Kind,
                Title = entry.Title,
                Subtitle = entry.Subtitle,
                Pages = new[]
                {
                    new NpcInteractionPage
                    {
                        RawText = page.RawText,
                        Text = AppendMetadata(page.Text, $"Trailing bytes left unread: {trailingBytes}."),
                        Choices = page.Choices,
                        InputRequest = page.InputRequest
                    }
                },
                PrimaryActionLabel = entry.PrimaryActionLabel,
                PrimaryActionEnabled = entry.PrimaryActionEnabled,
                PrimaryActionKind = entry.PrimaryActionKind
            };
        }

        private static NpcInteractionEntry CreateEntry(
            string title,
            string subtitle,
            string rawText,
            string text,
            IReadOnlyList<NpcInteractionChoice> choices,
            NpcInteractionInputRequest inputRequest = null)
        {
            return new NpcInteractionEntry
            {
                EntryId = 1,
                Kind = NpcInteractionEntryKind.Talk,
                Title = title,
                Subtitle = subtitle,
                Pages = new[]
                {
                    new NpcInteractionPage
                    {
                        RawText = rawText ?? string.Empty,
                        Text = text ?? string.Empty,
                        Choices = choices ?? Array.Empty<NpcInteractionChoice>(),
                        InputRequest = inputRequest
                    }
                },
                PrimaryActionLabel = inputRequest == null ? string.Empty : "Send",
                PrimaryActionEnabled = inputRequest != null
            };
        }

        internal bool TryBuildResponsePacket(
            NpcInteractionInputSubmission submission,
            out PacketScriptResponsePacket responsePacket,
            out string message)
        {
            responsePacket = null;
            if (submission == null)
            {
                message = "Packet-owned script submission was empty.";
                return false;
            }

            _activePromptContext ??= new PacketScriptPromptContext(
                submission.NpcName,
                submission.EntryId,
                submission.EntryTitle,
                -1,
                0,
                0,
                0);

            string submittedValue = submission.Kind switch
            {
                NpcInteractionInputKind.Number when submission.NumericValue.HasValue => submission.NumericValue.Value.ToString(),
                _ => submission.Value
            };

            if (!TryEncodeResponsePayload(_activePromptContext.MessageType, submission, submittedValue, out byte[] rawPacket, out string encodeError))
            {
                message = encodeError ?? "Packet-owned script submission could not be encoded.";
                return false;
            }

            string summary =
                $"Prepared packet-authored {submission.EntryTitle} response for {_activePromptContext.SpeakerName}: {FormatQuotedValue(submittedValue)} " +
                $"(msgType={_activePromptContext.MessageType}, template={_activePromptContext.SpeakerTemplateId}, bParam=0x{_activePromptContext.Param:X2}).";
            responsePacket = new PacketScriptResponsePacket(
                _activePromptContext.MessageType,
                _activePromptContext.Param,
                _activePromptContext.SpeakerTemplateId,
                _activePromptContext.SpeakerNpcId,
                submittedValue ?? string.Empty,
                rawPacket,
                summary);
            _statusMessage = summary;
            message = _statusMessage;
            return true;
        }

        internal void RecordResponseDispatch(PacketScriptResponsePacket responsePacket, bool dispatched, string detail)
        {
            if (responsePacket == null)
            {
                return;
            }

            string dispatchText = string.IsNullOrWhiteSpace(detail)
                ? (dispatched ? "outbound dispatch sent." : "outbound dispatch unavailable.")
                : detail.Trim();
            _statusMessage = $"{responsePacket.Summary} {dispatchText}";
        }

        private static NpcInteractionChoice CreateResponseChoice(string label, string responseLabel, int responseValue)
        {
            return new NpcInteractionChoice
            {
                Label = label,
                SubmitSelection = true,
                SubmissionKind = NpcInteractionInputKind.None,
                SubmissionValue = responseLabel,
                SubmissionNumericValue = responseValue
            };
        }

        private static NpcInteractionChoice CreateNumericResponseChoice(string label, string responseLabel, int responseValue)
        {
            return new NpcInteractionChoice
            {
                Label = label,
                SubmitSelection = true,
                SubmissionKind = NpcInteractionInputKind.Number,
                SubmissionValue = responseLabel,
                SubmissionNumericValue = responseValue
            };
        }

        private static string BuildSpeakerSubtitle(PacketScriptSpeaker speaker, string messageKind, byte param, int? mode = null)
        {
            return mode.HasValue
                ? $"{messageKind} | template={speaker.TemplateId} | param=0x{param:X2} | mode={mode.Value}"
                : $"{messageKind} | template={speaker.TemplateId} | param=0x{param:X2}";
        }

        private static string BuildNavigationMetadata(bool hasPrev, bool hasNext)
        {
            if (!hasPrev && !hasNext)
            {
                return "Client navigation flags: no prev / no next.";
            }

            if (hasPrev && hasNext)
            {
                return "Client navigation flags: prev / next.";
            }

            return hasPrev ? "Client navigation flags: prev only." : "Client navigation flags: next only.";
        }

        private static string AppendMetadata(params string[] lines)
        {
            List<string> nonEmptyLines = new();
            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                {
                    nonEmptyLines.Add(lines[i].Trim());
                }
            }

            return string.Join("\n", nonEmptyLines);
        }

        private static string FormatQuotedValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(empty)" : $"\"{value}\"";
        }

        private static string ReadMapleString(BinaryReader reader)
        {
            ushort length = reader.ReadUInt16();
            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
            {
                throw new EndOfStreamException("Packet string terminated before the declared Maple string length.");
            }

            return Encoding.Default.GetString(bytes);
        }

        private static PacketScriptSpeaker ResolveSpeaker(
            int speakerTypeId,
            int speakerTemplateId,
            Func<int, NpcItem> findNpcById,
            NpcItem activeNpc)
        {
            if (speakerTemplateId > 0)
            {
                NpcItem mapNpc = findNpcById?.Invoke(speakerTemplateId);
                if (mapNpc != null)
                {
                    return CreateSpeakerFromNpc(mapNpc, speakerTypeId, speakerTemplateId);
                }

                string cachedName = ResolveNpcNameFromCache(speakerTemplateId);
                if (!string.IsNullOrWhiteSpace(cachedName))
                {
                    return new PacketScriptSpeaker(speakerTypeId, speakerTemplateId, speakerTemplateId, cachedName);
                }
            }

            if (activeNpc != null)
            {
                int activeNpcId = int.TryParse(activeNpc.NpcInstance?.NpcInfo?.ID, out int parsedNpcId) ? parsedNpcId : 0;
                return CreateSpeakerFromNpc(activeNpc, speakerTypeId, activeNpcId);
            }

            return new PacketScriptSpeaker(speakerTypeId, speakerTemplateId, 0, speakerTemplateId > 0 ? $"NPC #{speakerTemplateId}" : "Script");
        }

        private static PacketScriptSpeaker CreateSpeakerFromNpc(NpcItem npc, int speakerTypeId, int templateId)
        {
            int npcId = int.TryParse(npc.NpcInstance?.NpcInfo?.ID, out int parsedNpcId) ? parsedNpcId : 0;
            string displayName = npc.NpcInstance?.NpcInfo?.StringName;
            if (string.IsNullOrWhiteSpace(displayName) && npcId > 0)
            {
                displayName = ResolveNpcNameFromCache(npcId);
            }

            return new PacketScriptSpeaker(
                speakerTypeId,
                templateId > 0 ? templateId : npcId,
                npcId,
                string.IsNullOrWhiteSpace(displayName) ? "NPC" : displayName);
        }

        private static string ResolveNpcNameFromCache(int npcId)
        {
            string key = npcId.ToString();
            if (Program.InfoManager?.NpcNameCache != null &&
                Program.InfoManager.NpcNameCache.TryGetValue(key, out Tuple<string, string> info))
            {
                return info?.Item1;
            }

            return null;
        }

        private static string BuildAvatarChoiceLabel(int itemId, int index)
        {
            string itemName = ResolveItemName(itemId);
            return string.IsNullOrWhiteSpace(itemName)
                ? $"Option {index + 1} ({itemId})"
                : $"Option {index + 1}: {itemName}";
        }

        private static string DescribeAvatarOption(int itemId)
        {
            string itemName = ResolveItemName(itemId);
            return string.IsNullOrWhiteSpace(itemName)
                ? $"Item {itemId}"
                : $"{itemName} ({itemId})";
        }

        private static string ResolveItemName(int itemId)
        {
            return InventoryItemMetadataResolver.TryResolveItemName(itemId, out string itemName)
                ? itemName?.Trim()
                : null;
        }

        private static string BuildPetChoiceLabel(long petSerialNumber, int index)
        {
            return $"Pet {index + 1} (SN {petSerialNumber})";
        }

        private static string DescribePetOption(long petSerialNumber, int index)
        {
            return $"Pet option {index + 1} with cash serial {petSerialNumber}.";
        }

        private static List<int> ReadIndexedChoiceIds(BinaryReader reader)
        {
            List<int> values = new();
            if (!TryReadByte(reader, out byte count))
            {
                return values;
            }

            long remaining = reader.BaseStream.Length - reader.BaseStream.Position;
            if (remaining < count)
            {
                return values;
            }

            if (remaining >= count * sizeof(int))
            {
                for (int i = 0; i < count; i++)
                {
                    values.Add(reader.ReadInt32());
                }

                return values;
            }

            for (int i = 0; i < count; i++)
            {
                values.Add(reader.ReadByte());
            }

            return values;
        }

        private static bool TryReadMapleString(BinaryReader reader, out string value)
        {
            value = string.Empty;
            if (reader?.BaseStream == null || reader.BaseStream.Position + sizeof(ushort) > reader.BaseStream.Length)
            {
                return false;
            }

            long originalPosition = reader.BaseStream.Position;
            try
            {
                value = ReadMapleString(reader);
                return true;
            }
            catch (EndOfStreamException)
            {
                reader.BaseStream.Position = originalPosition;
                return false;
            }
        }

        private static bool TryReadMapleStringList(BinaryReader reader, out IReadOnlyList<string> values)
        {
            values = Array.Empty<string>();
            if (!TryReadByte(reader, out byte count))
            {
                return false;
            }

            long originalPosition = reader.BaseStream.Position;
            List<string> decoded = new(count);
            try
            {
                for (int i = 0; i < count; i++)
                {
                    decoded.Add(ReadMapleString(reader));
                }

                values = decoded;
                return true;
            }
            catch (EndOfStreamException)
            {
                reader.BaseStream.Position = originalPosition - sizeof(byte);
                values = Array.Empty<string>();
                return false;
            }
        }

        private static bool TryReadByte(BinaryReader reader, out byte value)
        {
            value = 0;
            if (reader?.BaseStream == null || reader.BaseStream.Position >= reader.BaseStream.Length)
            {
                return false;
            }

            value = reader.ReadByte();
            return true;
        }

        private static bool TryReadInt32(BinaryReader reader, out int value)
        {
            value = 0;
            if (reader?.BaseStream == null || reader.BaseStream.Position + sizeof(int) > reader.BaseStream.Length)
            {
                return false;
            }

            value = reader.ReadInt32();
            return true;
        }

        private static IReadOnlyList<string> SplitSlideMenuOptions(string buttonInfo)
        {
            if (string.IsNullOrWhiteSpace(buttonInfo))
            {
                return Array.Empty<string>();
            }

            return buttonInfo
                .Replace("\\n", "\n", StringComparison.Ordinal)
                .Split(new[] { '\r', '\n', '|'}, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(static option => !string.IsNullOrWhiteSpace(option))
                .ToArray();
        }

        private static bool TryEncodeResponsePayload(
            int messageType,
            NpcInteractionInputSubmission submission,
            string submittedValue,
            out byte[] rawPacket,
            out string error)
        {
            rawPacket = null;
            error = null;

            PacketWriter writer = new PacketWriter();
            writer.WriteShort(OutboundScriptAnswerOpcode);
            writer.WriteByte((byte)messageType);

            switch (messageType)
            {
                case 1:
                {
                    if (!submission.NumericValue.HasValue || (submission.NumericValue.Value != 1 && submission.NumericValue.Value != -1))
                    {
                        error = "Say-image submissions require a numeric selection value of 1 or -1.";
                        return false;
                    }

                    writer.WriteByte(unchecked((byte)(sbyte)submission.NumericValue.Value));
                    break;
                }

                case 2:
                case 13:
                {
                    if (!submission.NumericValue.HasValue)
                    {
                        error = "Yes / No submissions require a numeric selection value.";
                        return false;
                    }

                    writer.WriteByte(unchecked((byte)submission.NumericValue.Value));
                    break;
                }

                case 3:
                case 14:
                {
                    bool accepted = !string.IsNullOrEmpty(submittedValue);
                    writer.WriteByte(accepted ? (byte)1 : (byte)0);
                    if (accepted)
                    {
                        writer.WriteMapleString(submittedValue);
                    }

                    break;
                }

                case 4:
                {
                    bool accepted = submission.NumericValue.HasValue;
                    writer.WriteByte(accepted ? (byte)1 : (byte)0);
                    if (accepted)
                    {
                        writer.WriteInt(submission.NumericValue.Value);
                    }

                    break;
                }

                case 5:
                case 7:
                case 12:
                case 15:
                {
                    if (!submission.NumericValue.HasValue)
                    {
                        error = "Menu-style submissions require a numeric selection id.";
                        return false;
                    }

                    writer.WriteByte(1);
                    writer.WriteInt(submission.NumericValue.Value);
                    break;
                }

                case 8:
                case 9:
                {
                    bool accepted = submission.NumericValue.HasValue &&
                                    submission.NumericValue.Value >= 0 &&
                                    submission.NumericValue.Value <= byte.MaxValue;
                    writer.WriteByte(accepted ? (byte)1 : (byte)0);
                    if (accepted)
                    {
                        writer.WriteByte((byte)submission.NumericValue.Value);
                    }

                    break;
                }

                case 10:
                case 11:
                {
                    bool accepted = long.TryParse(submittedValue, out long petSerialNumber) && petSerialNumber > 0;
                    writer.WriteByte(accepted ? (byte)1 : (byte)0);
                    if (accepted)
                    {
                        writer.WriteLong(petSerialNumber);
                    }

                    break;
                }

                case 6:
                {
                    bool accepted = submission.NumericValue.HasValue;
                    writer.WriteByte(accepted ? (byte)1 : (byte)0);
                    if (accepted)
                    {
                        writer.WriteInt(submission.NumericValue.Value);
                    }

                    break;
                }

                default:
                    error = $"Packet-authored script message type {messageType} does not have a supported outbound reply encoder.";
                    return false;
            }

            rawPacket = writer.ToArray();
            return true;
        }

        internal sealed record PacketScriptMessageOpenRequest(NpcInteractionState State, int SpeakerNpcId, bool CloseExistingDialog = false);
        internal sealed record PacketScriptResponsePacket(
            int MessageType,
            byte Param,
            int SpeakerTemplateId,
            int SpeakerNpcId,
            string SubmittedValue,
            byte[] RawPacket,
            string Summary);
        private sealed record PacketScriptPromptContext(
            string SpeakerName,
            int EntryId,
            string EntryTitle,
            int MessageType,
            byte Param,
            int SpeakerTemplateId,
            int SpeakerNpcId);

        private sealed record PacketScriptSpeaker(int SpeakerTypeId, int TemplateId, int NpcId, string DisplayName)
        {
            public PacketScriptSpeaker WithOverrideTemplateId(int templateId)
            {
                return this with { TemplateId = templateId };
            }
        }
    }
}
