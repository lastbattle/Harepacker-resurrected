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
            Func<long, byte, PacketScriptPetSelectionCandidate> resolveSelectablePet,
            Action<PacketScriptClientOwnerRuntimeSync> syncClientOwnerRuntime,
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
                PacketScriptDecodeResult decoded = messageType switch
                {
                    0 => CreateDecodedResult(DecodeSay(reader, ref speaker, ref speakerTemplateId, param, findNpcById)),
                    1 => CreateDecodedResult(DecodeSayImage(reader, speaker, param)),
                    2 => CreateDecodedResult(DecodeAskYesNo(reader, speaker, param, false)),
                    3 => CreateDecodedResult(DecodeAskText(reader, speaker, param)),
                    4 => CreateDecodedResult(DecodeAskNumber(reader, speaker, param)),
                    5 => CreateDecodedResult(DecodeAskMenu(reader, speaker, param)),
                    6 => DecodeAskQuiz(reader, speaker, param),
                    7 => DecodeAskSpeedQuiz(reader, speaker, param),
                    8 => DecodeAskAvatar(reader, speaker, param, false),
                    9 => DecodeAskAvatar(reader, speaker, param, true),
                    10 => DecodeAskPet(reader, speaker, param, false, resolveSelectablePet),
                    11 => DecodeAskPet(reader, speaker, param, true, resolveSelectablePet),
                    12 => DecodeIgnoredUnsupportedMessage(reader, speaker, messageType, param),
                    13 => CreateDecodedResult(DecodeAskYesNo(reader, speaker, param, true)),
                    14 => CreateDecodedResult(DecodeAskBoxText(reader, speaker, param)),
                    15 => DecodeAskSlideMenu(reader, speaker, param),
                    _ => DecodeIgnoredUnsupportedMessage(reader, speaker, messageType, param)
                };
                NpcInteractionEntry entry = decoded.Entry;
                PacketScriptResponsePacket autoResponse = decoded.AutoResponse;
                syncClientOwnerRuntime?.Invoke(decoded.ClientOwnerRuntimeSync);

                if (stream.Position != stream.Length)
                {
                    entry = AppendTrailingByteNotice(entry, (int)(stream.Length - stream.Position));
                }

                if (decoded.SuppressDialogMutation)
                {
                    request = null;
                    _activePromptContext = entry == null
                        ? null
                        : new PacketScriptPromptContext(
                            speaker.DisplayName,
                            entry.EntryId,
                            entry.Title,
                            messageType,
                            param,
                            speakerTemplateId,
                            speaker.NpcId);
                    _statusMessage = decoded.StatusMessage ?? $"Ignored packet-authored script payload for {speaker.DisplayName}.";
                    message = _statusMessage;
                    return true;
                }

                if (entry == null)
                {
                request = new PacketScriptMessageOpenRequest(
                    null,
                    speaker.NpcId,
                    CloseExistingDialog: decoded.CloseExistingDialog || entry == null,
                    AutoResponse: autoResponse,
                    DedicatedOwner: decoded.DedicatedOwner);
                    _activePromptContext = null;
                    _statusMessage = autoResponse?.Summary ?? decoded.StatusMessage ?? $"Closed packet-authored script dialog for {speaker.DisplayName}.";
                    message = _statusMessage;
                    return true;
                }

                request = new PacketScriptMessageOpenRequest(
                    new NpcInteractionState
                    {
                        NpcName = speaker.DisplayName,
                        SpeakerTemplateId = speakerTemplateId,
                        Entries = new[] { entry },
                        SelectedEntryId = entry.EntryId,
                        PresentationStyle = NpcInteractionPresentationStyle.PacketScriptUtilDialog
                    },
                    speaker.NpcId,
                    AutoResponse: autoResponse,
                    DedicatedOwner: decoded.DedicatedOwner);

                _activePromptContext = new PacketScriptPromptContext(
                    speaker.DisplayName,
                    entry.EntryId,
                    entry.Title,
                    messageType,
                    param,
                    speakerTemplateId,
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

        private static NpcInteractionEntry DecodeSay(
            BinaryReader reader,
            ref PacketScriptSpeaker speaker,
            ref int speakerTemplateId,
            byte param,
            Func<int, NpcItem> findNpcById)
        {
            if ((param & 4) != 0)
            {
                speakerTemplateId = reader.ReadInt32();
                speaker = ResolveSpeaker(speaker.SpeakerTypeId, speakerTemplateId, findNpcById, activeNpc: null);
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
                BuildSayNavigationChoices(hasPrev, hasNext));
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

        private static PacketScriptDecodeResult DecodeAskAvatar(BinaryReader reader, PacketScriptSpeaker speaker, byte param, bool isMembershopAvatar)
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

            NpcInteractionEntry entry = CreateEntry(
                isMembershopAvatar ? "Member Shop Avatar" : "Avatar Selection",
                BuildSpeakerSubtitle(speaker, isMembershopAvatar ? "AskMembershopAvatar" : "AskAvatar", param),
                rawText,
                AppendMetadata(
                    NpcDialogueTextFormatter.Format(rawText),
                    optionDetails,
                    $"Client `CUtilDlgEx::SetUtilDlgEx_AVATAR` opened {optionItemIds.Count} indexed avatar option(s).",
                    "WZ data exposes the packet-owned avatar utility surface under `UIWindow(.img|2.img)/UtilDlgEx_Avatar`."),
                choices);
            return CreateDecodedResult(
                entry,
                dedicatedOwner: CreateDedicatedOwner(
                    isMembershopAvatar ? PacketScriptDedicatedOwnerKind.MembershopAvatarSelection : PacketScriptDedicatedOwnerKind.AvatarSelection,
                    isMembershopAvatar ? "Member Shop Avatar" : "Avatar Selection",
                    rawText,
                    entry.Pages.Count > 0 ? entry.Pages[0].Text : string.Empty,
                    choices,
                    previewItemIds: optionItemIds));
        }

        private static PacketScriptDecodeResult DecodeAskQuiz(BinaryReader reader, PacketScriptSpeaker speaker, byte param)
        {
            long startPosition = reader.BaseStream.Position;
            if (TryDecodeAskQuizClientPacket(reader, speaker, param, out PacketScriptDecodeResult decoded))
            {
                return decoded;
            }

            reader.BaseStream.Position = startPosition;
            return CreateDecodedResult(DecodeAskQuizCompactPacket(reader, speaker, param));
        }

        private static PacketScriptDecodeResult DecodeAskSpeedQuiz(BinaryReader reader, PacketScriptSpeaker speaker, byte param)
        {
            long startPosition = reader.BaseStream.Position;
            if (TryDecodeAskSpeedQuizClientPacket(reader, speaker, param, out PacketScriptDecodeResult decoded))
            {
                return decoded;
            }

            reader.BaseStream.Position = startPosition;
            return CreateDecodedResult(DecodeAskSpeedQuizCompactPacket(reader, speaker, param));
        }

        private static PacketScriptDecodeResult DecodeAskPet(
            BinaryReader reader,
            PacketScriptSpeaker speaker,
            byte param,
            bool isPetAll,
            Func<long, byte, PacketScriptPetSelectionCandidate> resolveSelectablePet)
        {
            long startPosition = reader.BaseStream.Position;
            if (TryDecodeAskPetClientPacket(reader, speaker, param, isPetAll, resolveSelectablePet, out PacketScriptDecodeResult decoded))
            {
                return decoded;
            }

            reader.BaseStream.Position = startPosition;
            return CreateDecodedResult(DecodeAskPetCompactPacket(reader, speaker, param, isPetAll));
        }

        private static PacketScriptDecodeResult DecodeAskSlideMenu(BinaryReader reader, PacketScriptSpeaker speaker, byte param)
        {
            long startPosition = reader.BaseStream.Position;
            if (TryDecodeAskSlideMenuClientPacket(reader, speaker, param, out PacketScriptDecodeResult decoded))
            {
                return decoded;
            }

            reader.BaseStream.Position = startPosition;
            return CreateDecodedResult(DecodeAskSlideMenuCompactPacket(reader, speaker, param));
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

        private static bool TryDecodeAskQuizClientPacket(BinaryReader reader, PacketScriptSpeaker speaker, byte param, out PacketScriptDecodeResult result)
        {
            long startPosition = reader.BaseStream.Position;
            result = null;
            try
            {
                if (!TryReadByte(reader, out byte mode))
                {
                    reader.BaseStream.Position = startPosition;
                    return false;
                }

                if (mode == 1)
                {
                    result = CreateDecodedResult(
                        null,
                        clientOwnerRuntimeSync: PacketScriptClientOwnerRuntimeSync.CreateClose(PacketScriptClientOwnerRuntimeKind.InitialQuiz),
                        closeExistingDialog: true,
                        statusMessage: $"Closed packet-authored quiz owner for {speaker.DisplayName}: client mode 1 destroys `CUIInitialQuiz`.");
                    return true;
                }

                if (mode != 0)
                {
                    result = CreateDecodedResult(
                        null,
                        suppressDialogMutation: true,
                        statusMessage: $"Ignored packet-authored quiz payload for {speaker.DisplayName}: client `CWvsContext::OnInitialQuiz` only opens mode 0 and closes on mode 1.");
                    return true;
                }

                string title = ReadMapleString(reader);
                string problemText = ReadMapleString(reader);
                string hintText = ReadMapleString(reader);
                int minInputLength = reader.ReadInt32();
                int maxInputLength = reader.ReadInt32();
                int remainingSeconds = reader.ReadInt32();

                result = CreateDecodedResult(CreateEntry(
                    "Initial Quiz",
                    BuildSpeakerSubtitle(speaker, "AskQuiz", param, mode),
                    problemText,
                    AppendMetadata(
                        $"Title: {FormatQuotedValue(title)}",
                        NpcDialogueTextFormatter.Format(problemText),
                        string.IsNullOrWhiteSpace(hintText) ? null : $"Hint text: {FormatQuotedValue(hintText)}",
                        $"Client payload: minInput={minInputLength}, maxInput={maxInputLength}, remaining={remainingSeconds} sec.",
                        "Client `CWvsContext::OnInitialQuiz` opens the dedicated `CUIInitialQuiz` owner instead of mutating the NPC overlay."),
                    null),
                    suppressDialogMutation: true,
                    statusMessage: $"Opened packet-authored initial quiz owner for {speaker.DisplayName}: client mode 0 creates `CUIInitialQuiz`.",
                    clientOwnerRuntimeSync: PacketScriptClientOwnerRuntimeSync.CreateInitialQuiz(
                        title,
                        problemText,
                        hintText,
                        minInputLength,
                        maxInputLength,
                        remainingSeconds));
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is IOException || ex is ArgumentException)
            {
                reader.BaseStream.Position = startPosition;
                result = null;
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

        private static bool TryDecodeAskSpeedQuizClientPacket(BinaryReader reader, PacketScriptSpeaker speaker, byte param, out PacketScriptDecodeResult result)
        {
            long startPosition = reader.BaseStream.Position;
            result = null;
            try
            {
                if (!TryReadByte(reader, out byte mode))
                {
                    reader.BaseStream.Position = startPosition;
                    return false;
                }

                if (mode == 1)
                {
                    result = CreateDecodedResult(
                        null,
                        clientOwnerRuntimeSync: PacketScriptClientOwnerRuntimeSync.CreateClose(PacketScriptClientOwnerRuntimeKind.SpeedQuiz),
                        closeExistingDialog: true,
                        statusMessage: $"Closed packet-authored speed-quiz owner for {speaker.DisplayName}: client mode 1 destroys `CUISpeedQuiz`.");
                    return true;
                }

                if (mode != 0)
                {
                    result = CreateDecodedResult(
                        null,
                        suppressDialogMutation: true,
                        statusMessage: $"Ignored packet-authored speed-quiz payload for {speaker.DisplayName}: client `CWvsContext::OnInitialSpeedQuiz` only opens mode 0 and closes on mode 1.");
                    return true;
                }

                int currentQuestion = reader.ReadInt32();
                int totalQuestions = reader.ReadInt32();
                int correctAnswers = reader.ReadInt32();
                int remainingQuestions = reader.ReadInt32();
                int remainingSeconds = reader.ReadInt32();

                result = CreateDecodedResult(CreateEntry(
                    "Speed Quiz",
                    BuildSpeakerSubtitle(speaker, "AskSpeedQuiz", param, mode),
                    string.Empty,
                    AppendMetadata(
                        $"Question {currentQuestion} / {totalQuestions}",
                        $"Correct answers: {correctAnswers}",
                        $"Questions remaining: {remainingQuestions}",
                        $"Time remaining: {remainingSeconds} sec.",
                        "WZ data exposes `UIWindow(.img|2.img)/SpeedQuiz` with dedicated OK / Next / Give up controls plus the matching numeric-strip owners used by the client packet path."),
                    new[]
                    {
                        CreateNumericResponseChoice("OK", "OK", 1),
                        CreateNumericResponseChoice("Next", "Next", 2),
                        CreateNumericResponseChoice("Give Up", "Give Up", 0)
                    }),
                    suppressDialogMutation: true,
                    statusMessage: $"Opened packet-authored speed-quiz owner for {speaker.DisplayName}: client mode 0 creates `CUISpeedQuiz`.",
                    clientOwnerRuntimeSync: PacketScriptClientOwnerRuntimeSync.CreateSpeedQuiz(
                        currentQuestion,
                        totalQuestions,
                        correctAnswers,
                        remainingQuestions,
                        remainingSeconds));
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is IOException || ex is ArgumentException)
            {
                reader.BaseStream.Position = startPosition;
                result = null;
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

        private static bool TryDecodeAskPetClientPacket(
            BinaryReader reader,
            PacketScriptSpeaker speaker,
            byte param,
            bool isPetAll,
            Func<long, byte, PacketScriptPetSelectionCandidate> resolveSelectablePet,
            out PacketScriptDecodeResult result)
        {
            long startPosition = reader.BaseStream.Position;
            result = null;
            try
            {
                string rawText = ReadMapleString(reader);
                if (!TryReadByte(reader, out byte count))
                {
                    reader.BaseStream.Position = startPosition;
                    return false;
                }

                bool exceptionExists = isPetAll && reader.ReadByte() != 0;
                List<PacketScriptPetPacketEntry> petPacketEntries = new(count);
                for (int i = 0; i < count; i++)
                {
                    long petSerialNumber = reader.ReadInt64();
                    byte packetSlotHint = reader.ReadByte();
                    petPacketEntries.Add(new PacketScriptPetPacketEntry(petSerialNumber, packetSlotHint));
                }

                bool allowPacketFallback = !string.IsNullOrWhiteSpace(rawText);
                List<PacketScriptPetSelectionCandidate> selectablePets = FilterSelectablePets(
                    petPacketEntries,
                    resolveSelectablePet,
                    allowPacketFallback,
                    out int droppedSerialCount,
                    out int packetFallbackCount);

                bool implicitFallbackOnly = ShouldAutoClosePetClientPacket(isPetAll, count, exceptionExists);
                if (implicitFallbackOnly)
                {
                    result = CreateDecodedResult(
                        null,
                        CreateAutoResponsePacket(
                            isPetAll ? 11 : 10,
                            param,
                            speaker,
                            "auto-close",
                            BuildStatusOnlyResponsePacket(isPetAll ? 11 : 10, 2),
                            isPetAll
                                ? $"Auto-closed packet-authored multi-pet prompt for {speaker.DisplayName}: client path does not open `UtilDlgEx_MultiPetEquip` when the packet only exposes the implicit fallback branch."
                                : $"Auto-closed packet-authored pet prompt for {speaker.DisplayName}: client path does not open `UtilDlgEx_Pet` when the packet exposes no selectable pets."));
                    return true;
                }

                if (string.IsNullOrWhiteSpace(rawText))
                {
                    if (selectablePets.Count == 0)
                    {
                        result = CreateDecodedResult(
                            null,
                            CreateAutoResponsePacket(
                                isPetAll ? 11 : 10,
                                param,
                                speaker,
                                "auto-close",
                                BuildStatusOnlyResponsePacket(isPetAll ? 11 : 10, 2),
                                isPetAll
                                    ? $"Auto-closed packet-authored multi-pet prompt for {speaker.DisplayName}: packet serials did not resolve to selectable pets on the current runtime branch."
                                    : $"Auto-closed packet-authored pet prompt for {speaker.DisplayName}: packet serials did not resolve to selectable pets on the current runtime branch."));
                        return true;
                    }

                    long selectedPetSerialNumber = selectablePets[0].PetSerialNumber;
                    result = CreateDecodedResult(
                        null,
                        CreateAutoResponsePacket(
                            isPetAll ? 11 : 10,
                            param,
                            speaker,
                            selectedPetSerialNumber.ToString(),
                            BuildPetSelectionResponsePacket(isPetAll ? 11 : 10, selectedPetSerialNumber),
                            isPetAll
                                ? $"Auto-accepted packet-authored multi-pet prompt for {speaker.DisplayName}: client path skips `UtilDlgEx_MultiPetEquip` and returns the first resolved pet serial when the packet omits prompt text."
                                : $"Auto-accepted packet-authored pet prompt for {speaker.DisplayName}: client path skips `UtilDlgEx_Pet` and returns the first resolved pet serial when the packet omits prompt text."));
                    return true;
                }

                IReadOnlyList<NpcInteractionChoice> choices = selectablePets.Select((pet, index) => new NpcInteractionChoice
                {
                    Label = BuildPetChoiceLabel(pet, index),
                    SubmitSelection = true,
                    SubmissionKind = NpcInteractionInputKind.None,
                    SubmissionValue = pet.PetSerialNumber.ToString(),
                    SubmissionNumericValue = null
                }).ToList();
                string optionDetails = selectablePets.Count == 0
                    ? "No pet options were decoded from the packet payload."
                    : string.Join("\n", selectablePets.Select((pet, index) => $"{index + 1}. {DescribePetOption(pet, index)}"));
                string resolutionDetails = droppedSerialCount > 0
                    ? $"Skipped {droppedSerialCount} packet serial entr{(droppedSerialCount == 1 ? "y" : "ies")} that did not resolve to selectable pets on the current runtime branch."
                    : null;
                if (packetFallbackCount > 0)
                {
                    string fallbackDetails =
                        $"Used packet serial fallback for {packetFallbackCount} entr{(packetFallbackCount == 1 ? "y" : "ies")} that could not be mapped through active pets or CharacterData cash inventory ownership.";
                    resolutionDetails = string.IsNullOrWhiteSpace(resolutionDetails)
                        ? fallbackDetails
                        : $"{resolutionDetails}\n{fallbackDetails}";
                }
                string entryText = BuildPetSelectionEntryText(rawText, optionDetails, resolutionDetails, isPetAll, exceptionExists);
                result = CreateDecodedResult(
                    CreatePetSelectionEntry(
                        speaker,
                        param,
                        isPetAll,
                        rawText,
                        exceptionExists,
                        choices,
                        optionDetails,
                        resolutionDetails),
                    dedicatedOwner: CreateDedicatedOwner(
                        isPetAll ? PacketScriptDedicatedOwnerKind.MultiPetSelection : PacketScriptDedicatedOwnerKind.PetSelection,
                        isPetAll ? "Pet Selection (All)" : "Pet Selection",
                        rawText,
                        entryText,
                        choices,
                        previewItemIds: selectablePets.Select(static pet => pet?.ItemId ?? 0).ToArray()));
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is IOException || ex is ArgumentException)
            {
                reader.BaseStream.Position = startPosition;
                result = null;
                return false;
            }
        }

        private static bool ShouldAutoClosePetClientPacket(bool isPetAll, int packetPetCount, bool exceptionExists)
        {
            if (packetPetCount <= 0)
            {
                return true;
            }

            return isPetAll && !exceptionExists && packetPetCount == 1;
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

            IReadOnlyList<NpcInteractionChoice> choices = petItemIds.Select((itemId, index) => new NpcInteractionChoice
            {
                Label = BuildPetItemChoiceLabel(itemId, index),
                SubmitSelection = true,
                SubmissionKind = NpcInteractionInputKind.None,
                SubmissionValue = itemId.ToString(),
                SubmissionNumericValue = null
            }).ToList();
            string optionDetails = petItemIds.Count == 0
                ? "Compact helper payload did not decode any pet item ids."
                : string.Join("\n", petItemIds.Select((itemId, index) => $"{index + 1}. {DescribePetItemOption(itemId, index)}"));
            string resolutionDetails = "Compact helper payload does not include pet-serial resolution details.";
            return CreatePetSelectionEntry(
                speaker,
                param,
                isPetAll,
                rawText,
                false,
                choices,
                optionDetails,
                resolutionDetails);
        }

        private static NpcInteractionEntry CreatePetSelectionEntry(
            PacketScriptSpeaker speaker,
            byte param,
            bool isPetAll,
            string rawText,
            bool exceptionExists,
            IReadOnlyList<NpcInteractionChoice> choices,
            string optionDetails,
            string resolutionDetails)
        {
            return CreateEntry(
                isPetAll ? "Pet Selection (All)" : "Pet Selection",
                BuildSpeakerSubtitle(speaker, isPetAll ? "AskPetAll" : "AskPet", param),
                rawText,
                BuildPetSelectionEntryText(rawText, optionDetails, resolutionDetails, isPetAll, exceptionExists),
                choices);
        }

        private static List<PacketScriptPetSelectionCandidate> FilterSelectablePets(
            IReadOnlyList<PacketScriptPetPacketEntry> packetPetEntries,
            Func<long, byte, PacketScriptPetSelectionCandidate> resolveSelectablePet,
            bool allowPacketFallback,
            out int droppedSerialCount,
            out int packetFallbackCount)
        {
            List<PacketScriptPetSelectionCandidate> selectable = new();
            droppedSerialCount = 0;
            packetFallbackCount = 0;
            if (packetPetEntries == null)
            {
                return selectable;
            }

            HashSet<long> seenPetSerialNumbers = new();
            for (int i = 0; i < packetPetEntries.Count; i++)
            {
                PacketScriptPetPacketEntry packetEntry = packetPetEntries[i];
                long serialNumber = packetEntry.PetSerialNumber;
                if (serialNumber <= 0)
                {
                    droppedSerialCount++;
                    continue;
                }

                if (!seenPetSerialNumbers.Add(serialNumber))
                {
                    droppedSerialCount++;
                    continue;
                }

                PacketScriptPetSelectionCandidate selectablePet = resolveSelectablePet?.Invoke(serialNumber, packetEntry.CashSlotHint);
                if (selectablePet == null)
                {
                    if (!allowPacketFallback)
                    {
                        droppedSerialCount++;
                        continue;
                    }

                    selectable.Add(CreatePacketFallbackPetCandidate(serialNumber, packetEntry.CashSlotHint));
                    packetFallbackCount++;
                    continue;
                }

                selectable.Add(selectablePet);
            }

            return selectable;
        }

        private static PacketScriptPetSelectionCandidate CreatePacketFallbackPetCandidate(long petSerialNumber, byte packetSlotHint)
        {
            short cashSlotHint = packetSlotHint > 0 ? (short)packetSlotHint : (short)0;
            string displayName = cashSlotHint > 0
                ? $"Pet in cash slot {cashSlotHint}"
                : $"Pet SN {petSerialNumber}";
            return new PacketScriptPetSelectionCandidate(
                petSerialNumber,
                SlotIndex: -1,
                ItemId: 0,
                DisplayName: displayName,
                Source: PacketScriptPetSelectionSource.PacketPayloadFallback,
                InventoryPosition: cashSlotHint);
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

        private static bool TryDecodeAskSlideMenuClientPacket(BinaryReader reader, PacketScriptSpeaker speaker, byte param, out PacketScriptDecodeResult result)
        {
            long startPosition = reader.BaseStream.Position;
            result = null;
            try
            {
                int slideMenuType = reader.ReadInt32();
                if (slideMenuType is not (0 or 1))
                {
                    result = CreateDecodedResult(
                        null,
                        CreateAutoResponsePacket(
                            15,
                            param,
                            speaker,
                            "cancel",
                            BuildMenuSelectionResponsePacket(15, accepted: false, selectionId: 0),
                            $"Auto-cancelled packet-authored slide-menu prompt for {speaker.DisplayName}: client `CScriptMan::OnAskSlideMenu` only opens type 0 (`CSlideMenuDlgEX`) or type 1 (`CSlideMenuDlg`) owners."));
                    return true;
                }

                int initialSelectionId = reader.ReadInt32();
                string buttonInfo = ReadMapleString(reader);
                IReadOnlyList<SlideMenuOption> options = ParseSlideMenuButtonInfo(buttonInfo);
                NpcInteractionEntry entry = CreateSlideMenuEntry(
                    speaker,
                    param,
                    slideMenuType,
                    initialSelectionId,
                    buttonInfo,
                    options,
                    "Decoded");
                result = CreateDecodedResult(
                    entry,
                    dedicatedOwner: CreateDedicatedOwner(
                        PacketScriptDedicatedOwnerKind.SlideMenu,
                        "Slide Menu",
                        buttonInfo,
                        entry.Pages.Count > 0 ? entry.Pages[0].Text : string.Empty,
                        entry.Pages.Count > 0 ? entry.Pages[0].Choices : Array.Empty<NpcInteractionChoice>(),
                        mode: slideMenuType,
                        initialSelectionId: initialSelectionId));
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is IOException || ex is ArgumentException)
            {
                reader.BaseStream.Position = startPosition;
                result = null;
                return false;
            }
        }

        private static NpcInteractionEntry DecodeAskSlideMenuCompactPacket(BinaryReader reader, PacketScriptSpeaker speaker, byte param)
        {
            string rawText = ReadMapleString(reader);
            byte slideMenuType = reader.ReadByte();
            IReadOnlyList<SlideMenuOption> options = TryReadMapleStringList(reader, out IReadOnlyList<string> decodedOptions)
                ? decodedOptions
                    .Select((option, index) => new SlideMenuOption(index, option))
                    .ToArray()
                : Array.Empty<SlideMenuOption>();
            return CreateSlideMenuEntry(speaker, param, slideMenuType, 0, rawText, options, "Compact helper payload exposed");
        }

        private static NpcInteractionEntry CreateSlideMenuEntry(
            PacketScriptSpeaker speaker,
            byte param,
            int slideMenuType,
            int initialSelectionId,
            string rawText,
            IReadOnlyList<SlideMenuOption> options,
            string decodedPrefix)
        {
            List<NpcInteractionChoice> choices = options
                .Select((option, index) => CreateNumericResponseChoice(
                    $"{index + 1}. {NpcDialogueTextFormatter.Format(option.Label)}",
                    $"selectionId={option.SelectionId}",
                    option.SelectionId))
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
                    $"Initial selection id: {initialSelectionId}.",
                    slideMenuType switch
                    {
                        0 => "Client type 0 opens the extended `CSlideMenuDlgEX` owner backed by `UIWindow(.img|2.img)/SlideMenu/0`.",
                        1 => "Client type 1 opens the standard `CSlideMenuDlg` owner backed by `UIWindow(.img|2.img)/SlideMenu/1`.",
                        _ => null
                    },
                    "WZ data exposes authored slide-menu variants under `UIWindow(.img|2.img)/SlideMenu`."),
                choices);
        }

        private static IReadOnlyList<NpcInteractionChoice> BuildSayNavigationChoices(bool hasPrev, bool hasNext)
        {
            List<NpcInteractionChoice> choices = new();
            if (hasPrev)
            {
                choices.Add(CreateNumericResponseChoice("Previous", "Previous", -1));
            }

            choices.Add(CreateNumericResponseChoice(hasNext ? "End Chat" : "Close", "Close", 0));
            if (hasNext)
            {
                choices.Add(CreateNumericResponseChoice("Next", "Next", 1));
            }

            return choices;
        }

        private static PacketScriptDecodeResult CreateDecodedResult(
            NpcInteractionEntry entry,
            PacketScriptResponsePacket autoResponse = null,
            bool closeExistingDialog = false,
            bool suppressDialogMutation = false,
            string statusMessage = null,
            PacketScriptClientOwnerRuntimeSync clientOwnerRuntimeSync = null,
            PacketScriptDedicatedOwnerRequest dedicatedOwner = null)
        {
            return new PacketScriptDecodeResult(entry, autoResponse, closeExistingDialog, suppressDialogMutation, statusMessage, clientOwnerRuntimeSync, dedicatedOwner);
        }

        private static PacketScriptDecodeResult DecodeIgnoredUnsupportedMessage(BinaryReader reader, PacketScriptSpeaker speaker, int messageType, byte param)
        {
            string remainingHex = reader.BaseStream.Position >= reader.BaseStream.Length
                ? "none"
                : BitConverter.ToString(reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position))).Replace("-", string.Empty, StringComparison.Ordinal);
            return CreateDecodedResult(
                null,
                suppressDialogMutation: true,
                statusMessage: AppendMetadata(
                    $"Ignored packet-authored script message type {messageType} for {speaker.DisplayName}: client `CScriptMan::OnScriptMessage` falls through without opening a dialog for this message id.",
                    $"Remaining payload: {remainingHex}",
                    $"Header: template={speaker.TemplateId}, param=0x{param:X2}."));
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
            bool flipSpeaker = ResolveFlipSpeakerFromSubtitle(subtitle);
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
                        InputRequest = inputRequest,
                        FlipSpeaker = flipSpeaker
                    }
                },
                PrimaryActionLabel = inputRequest == null ? string.Empty : "Send",
                PrimaryActionEnabled = inputRequest != null
            };
        }

        private static bool ResolveFlipSpeakerFromSubtitle(string subtitle)
        {
            // Client CUtilDlgEx ownership uses `m_bSpeakerOnRight = (bParam & 6) != 0`.
            if (string.IsNullOrWhiteSpace(subtitle))
            {
                return false;
            }

            const string token = "param=0x";
            int tokenIndex = subtitle.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (tokenIndex < 0)
            {
                return false;
            }

            int valueStart = tokenIndex + token.Length;
            if (valueStart >= subtitle.Length)
            {
                return false;
            }

            int valueLength = 0;
            while (valueStart + valueLength < subtitle.Length)
            {
                char current = subtitle[valueStart + valueLength];
                if (!Uri.IsHexDigit(current))
                {
                    break;
                }

                valueLength++;
            }

            if (valueLength <= 0)
            {
                return false;
            }

            if (!byte.TryParse(
                    subtitle.Substring(valueStart, valueLength),
                    System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out byte paramValue))
            {
                return false;
            }

            return (paramValue & 0x06) != 0;
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

        internal PacketScriptResponsePacket BuildInitialQuizOwnerResponsePacket(string submittedValue)
        {
            submittedValue ??= string.Empty;
            byte[] rawPacket = BuildInitialQuizOwnerResponsePacketBytes(submittedValue);
            string summary = $"Prepared context-owned Initial Quiz response: {FormatQuotedValue(submittedValue)} (msgType=6).";
            _statusMessage = summary;
            return new PacketScriptResponsePacket(
                6,
                0,
                0,
                0,
                submittedValue,
                rawPacket,
                summary);
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

                return new PacketScriptSpeaker(
                    speakerTypeId,
                    speakerTemplateId,
                    speakerTemplateId,
                    $"NPC #{speakerTemplateId}");
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

        private static string BuildPetChoiceLabel(PacketScriptPetSelectionCandidate pet, int index)
        {
            if (pet == null)
            {
                return $"Pet {index + 1}";
            }

            string name = string.IsNullOrWhiteSpace(pet.DisplayName)
                ? $"Pet {index + 1}"
                : pet.DisplayName.Trim();
            string slotText = pet.Source switch
            {
                PacketScriptPetSelectionSource.LiveCashInventory when pet.InventoryPosition > 0 => $"cash slot {pet.InventoryPosition}",
                PacketScriptPetSelectionSource.LiveCashInventory => "cash slot ?",
                PacketScriptPetSelectionSource.AuthoritativeCharacterData when pet.InventoryPosition > 0 => $"cash slot {pet.InventoryPosition}",
                PacketScriptPetSelectionSource.AuthoritativeCharacterData => "cash slot ?",
                PacketScriptPetSelectionSource.PacketPayloadFallback when pet.InventoryPosition > 0 => $"cash slot {pet.InventoryPosition}",
                PacketScriptPetSelectionSource.PacketPayloadFallback => "packet serial",
                _ => pet.SlotIndex >= 0 ? $"slot {pet.SlotIndex + 1}" : "slot ?"
            };
            return $"{name} ({slotText}, SN {pet.PetSerialNumber})";
        }

        private static string DescribePetOption(PacketScriptPetSelectionCandidate pet, int index)
        {
            if (pet == null)
            {
                return $"Pet option {index + 1}.";
            }

            string name = string.IsNullOrWhiteSpace(pet.DisplayName)
                ? $"Pet option {index + 1}"
                : pet.DisplayName.Trim();
            string itemText = pet.ItemId > 0 ? $"item {pet.ItemId}" : "unknown item";
            string slotText = pet.Source switch
            {
                PacketScriptPetSelectionSource.LiveCashInventory when pet.InventoryPosition > 0 => $"cash slot {pet.InventoryPosition}",
                PacketScriptPetSelectionSource.LiveCashInventory => "unknown cash slot",
                PacketScriptPetSelectionSource.AuthoritativeCharacterData when pet.InventoryPosition > 0 => $"cash slot {pet.InventoryPosition}",
                PacketScriptPetSelectionSource.AuthoritativeCharacterData => "unknown cash slot",
                PacketScriptPetSelectionSource.PacketPayloadFallback when pet.InventoryPosition > 0 => $"cash slot {pet.InventoryPosition}",
                PacketScriptPetSelectionSource.PacketPayloadFallback => "packet serial",
                _ => pet.SlotIndex >= 0 ? $"slot {pet.SlotIndex + 1}" : "unknown slot"
            };
            return $"{name} on {slotText} with cash serial {pet.PetSerialNumber} ({itemText}).";
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

        private static IReadOnlyList<SlideMenuOption> ParseSlideMenuButtonInfo(string buttonInfo)
        {
            if (string.IsNullOrWhiteSpace(buttonInfo))
            {
                return Array.Empty<SlideMenuOption>();
            }

            List<SlideMenuOption> parsedOptions = new();
            string normalized = buttonInfo.EndsWith("#", StringComparison.Ordinal)
                ? buttonInfo
                : $"{buttonInfo}#";
            int cursor = 0;
            while (cursor < normalized.Length)
            {
                int numberStart = normalized.IndexOf('#', cursor);
                if (numberStart < 0 || numberStart + 1 >= normalized.Length)
                {
                    break;
                }

                int numberEnd = normalized.IndexOf('#', numberStart + 1);
                if (numberEnd < 0)
                {
                    break;
                }

                int labelEnd = normalized.IndexOf('#', numberEnd + 1);
                if (labelEnd < 0)
                {
                    break;
                }

                string numberText = normalized.Substring(numberStart + 1, numberEnd - numberStart - 1);
                string labelText = normalized.Substring(numberEnd + 1, labelEnd - numberEnd - 1);
                if (int.TryParse(numberText, out int selectionId) && !string.IsNullOrWhiteSpace(labelText))
                {
                    parsedOptions.Add(new SlideMenuOption(selectionId, labelText.Trim()));
                }

                cursor = labelEnd + 1;
            }

            if (parsedOptions.Count > 0)
            {
                return parsedOptions;
            }

            return buttonInfo
                .Replace("\\n", "\n", StringComparison.Ordinal)
                .Split(new[] { '\r', '\n', '|'}, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select((option, index) => new SlideMenuOption(index, option))
                .Where(static option => !string.IsNullOrWhiteSpace(option.Label))
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
                case 0:
                {
                    if (!submission.NumericValue.HasValue || submission.NumericValue.Value < -1 || submission.NumericValue.Value > 1)
                    {
                        error = "Say submissions require a numeric navigation value of -1, 0, or 1.";
                        return false;
                    }

                    writer.WriteByte(unchecked((byte)(sbyte)submission.NumericValue.Value));
                    break;
                }

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
                        // Client `CScriptMan::OnAskYesNo` encodes cancel/close as -1.
                        writer.WriteByte(0xFF);
                        break;
                    }

                    int responseValue = submission.NumericValue.Value;
                    if (responseValue < -1 || responseValue > 1)
                    {
                        error = "Yes / No submissions require a numeric selection value of -1, 0, or 1.";
                        return false;
                    }

                    writer.WriteByte(unchecked((byte)(sbyte)responseValue));
                    break;
                }

                case 3:
                case 14:
                {
                    bool accepted = submission.Kind is NpcInteractionInputKind.Text or NpcInteractionInputKind.MultiLineText;
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
                case 15:
                {
                    if (!submission.NumericValue.HasValue)
                    {
                        writer.WriteByte(0);
                        break;
                    }

                    writer.WriteByte(1);
                    writer.WriteInt(submission.NumericValue.Value);
                    break;
                }

                case 7:
                {
                    writer.WriteMapleString(submittedValue ?? string.Empty);
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
                    if (submission.Kind == NpcInteractionInputKind.Text
                        || submission.Kind == NpcInteractionInputKind.MultiLineText)
                    {
                        writer.WriteMapleString(submittedValue ?? string.Empty);
                        break;
                    }

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

        private static byte[] BuildStatusOnlyResponsePacket(int messageType, byte status)
        {
            PacketWriter writer = new PacketWriter();
            writer.WriteShort(OutboundScriptAnswerOpcode);
            writer.WriteByte((byte)messageType);
            writer.WriteByte(status);
            return writer.ToArray();
        }

        private static byte[] BuildPetSelectionResponsePacket(int messageType, long petSerialNumber)
        {
            PacketWriter writer = new PacketWriter();
            writer.WriteShort(OutboundScriptAnswerOpcode);
            writer.WriteByte((byte)messageType);
            writer.WriteByte(1);
            writer.WriteLong(petSerialNumber);
            return writer.ToArray();
        }

        private static byte[] BuildMenuSelectionResponsePacket(int messageType, bool accepted, int selectionId)
        {
            PacketWriter writer = new PacketWriter();
            writer.WriteShort(OutboundScriptAnswerOpcode);
            writer.WriteByte((byte)messageType);
            writer.WriteByte(accepted ? (byte)1 : (byte)0);
            if (accepted)
            {
                writer.WriteInt(selectionId);
            }

            return writer.ToArray();
        }

        private static string BuildPetSelectionEntryText(
            string rawText,
            string optionDetails,
            string resolutionDetails,
            bool isPetAll,
            bool exceptionExists)
        {
            return AppendMetadata(
                NpcDialogueTextFormatter.Format(rawText),
                optionDetails,
                resolutionDetails,
                isPetAll ? $"Multi-pet exception flag: {(exceptionExists ? 1 : 0)}." : null,
                "WZ data exposes packet-owned pet utility surfaces under `UIWindow(.img|2.img)/UtilDlgEx_Pet` and `UtilDlgEx_MultiPetEquip`.");
        }

        private static PacketScriptDedicatedOwnerRequest CreateDedicatedOwner(
            PacketScriptDedicatedOwnerKind kind,
            string title,
            string promptText,
            string detailText,
            IReadOnlyList<NpcInteractionChoice> choices,
            IReadOnlyList<int> previewItemIds = null,
            int mode = 0,
            int initialSelectionId = 0)
        {
            return new PacketScriptDedicatedOwnerRequest(
                kind,
                title ?? string.Empty,
                promptText ?? string.Empty,
                detailText ?? string.Empty,
                choices ?? Array.Empty<NpcInteractionChoice>(),
                previewItemIds ?? Array.Empty<int>(),
                mode,
                initialSelectionId);
        }

        internal static byte[] BuildInitialQuizOwnerResponsePacketBytes(string submittedValue)
        {
            PacketWriter writer = new PacketWriter();
            writer.WriteShort(OutboundScriptAnswerOpcode);
            writer.WriteByte(6);
            writer.WriteMapleString(submittedValue ?? string.Empty);
            return writer.ToArray();
        }

        private static PacketScriptResponsePacket CreateAutoResponsePacket(
            int messageType,
            byte param,
            PacketScriptSpeaker speaker,
            string submittedValue,
            byte[] rawPacket,
            string summary)
        {
            return new PacketScriptResponsePacket(
                messageType,
                param,
                speaker.TemplateId,
                speaker.NpcId,
                submittedValue ?? string.Empty,
                rawPacket,
                summary);
        }

        internal sealed record PacketScriptMessageOpenRequest(
            NpcInteractionState State,
            int SpeakerNpcId,
            bool CloseExistingDialog = false,
            PacketScriptResponsePacket AutoResponse = null,
            PacketScriptDedicatedOwnerRequest DedicatedOwner = null);
        internal sealed record PacketScriptPetSelectionCandidate(
            long PetSerialNumber,
            int SlotIndex,
            int ItemId,
            string DisplayName,
            PacketScriptPetSelectionSource Source,
            short InventoryPosition = 0);
        private sealed record PacketScriptDecodeResult(
            NpcInteractionEntry Entry,
            PacketScriptResponsePacket AutoResponse = null,
            bool CloseExistingDialog = false,
            bool SuppressDialogMutation = false,
            string StatusMessage = null,
            PacketScriptClientOwnerRuntimeSync ClientOwnerRuntimeSync = null,
            PacketScriptDedicatedOwnerRequest DedicatedOwner = null);
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

        private sealed record SlideMenuOption(int SelectionId, string Label);
        private sealed record PacketScriptPetPacketEntry(long PetSerialNumber, byte CashSlotHint);

        private sealed record PacketScriptSpeaker(int SpeakerTypeId, int TemplateId, int NpcId, string DisplayName);

        internal enum PacketScriptClientOwnerRuntimeKind
        {
            InitialQuiz,
            SpeedQuiz
        }

        internal enum PacketScriptDedicatedOwnerKind
        {
            AvatarSelection,
            MembershopAvatarSelection,
            SlideMenu,
            PetSelection,
            MultiPetSelection
        }

        internal sealed record PacketScriptDedicatedOwnerRequest(
            PacketScriptDedicatedOwnerKind Kind,
            string Title,
            string PromptText,
            string DetailText,
            IReadOnlyList<NpcInteractionChoice> Choices,
            IReadOnlyList<int> PreviewItemIds,
            int Mode,
            int InitialSelectionId);

        internal sealed record PacketScriptClientOwnerRuntimeSync(
            PacketScriptClientOwnerRuntimeKind Kind,
            bool CloseExistingOwner,
            string Title,
            string ProblemText,
            string HintText,
            int CorrectAnswer,
            int QuestionNumber,
            int TotalQuestions,
            int CorrectAnswers,
            int RemainingQuestions,
            int RemainingSeconds)
        {
            internal int InitialQuizMinInputLength => CorrectAnswer;
            internal int InitialQuizMaxInputLength => QuestionNumber;

            internal static PacketScriptClientOwnerRuntimeSync CreateClose(PacketScriptClientOwnerRuntimeKind kind)
            {
                return new PacketScriptClientOwnerRuntimeSync(kind, true, string.Empty, string.Empty, string.Empty, 0, 0, 0, 0, 0, 0);
            }

            internal static PacketScriptClientOwnerRuntimeSync CreateInitialQuiz(
                string title,
                string problemText,
                string hintText,
                int minInputLength,
                int maxInputLength,
                int remainingSeconds)
            {
                return new PacketScriptClientOwnerRuntimeSync(
                    PacketScriptClientOwnerRuntimeKind.InitialQuiz,
                    false,
                    title ?? string.Empty,
                    problemText ?? string.Empty,
                    hintText ?? string.Empty,
                    minInputLength,
                    maxInputLength,
                    0,
                    0,
                    0,
                    Math.Max(0, remainingSeconds));
            }

            internal static PacketScriptClientOwnerRuntimeSync CreateSpeedQuiz(
                int currentQuestion,
                int totalQuestions,
                int correctAnswers,
                int remainingQuestions,
                int remainingSeconds)
            {
                return new PacketScriptClientOwnerRuntimeSync(
                    PacketScriptClientOwnerRuntimeKind.SpeedQuiz,
                    false,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    0,
                    currentQuestion,
                    totalQuestions,
                    correctAnswers,
                    remainingQuestions,
                    Math.Max(0, remainingSeconds));
            }
        }
    }
}
