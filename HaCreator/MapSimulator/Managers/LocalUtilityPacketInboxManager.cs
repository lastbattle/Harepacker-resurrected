using HaCreator.MapSimulator.Interaction;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class LocalUtilityPacketInboxMessage
    {
        public LocalUtilityPacketInboxMessage(int packetType, byte[] payload, string source, string rawText)
        {
            PacketType = packetType;
            Payload = payload ?? Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "local-utility-inbox" : source;
            RawText = rawText ?? string.Empty;
        }

        public int PacketType { get; }
        public byte[] Payload { get; }
        public string Source { get; }
        public string RawText { get; }
    }

    /// <summary>
    /// Loopback inbox for packet-owned local utility handlers that sit under
    /// CUserLocal::OnPacket. Each line is either a numeric packet type or one
    /// of the named aliases and an optional payload:
    /// "274 payloadhex=...", "questguide payloadb64=...", or "classcompetition".
    /// </summary>
    public sealed class LocalUtilityPacketInboxManager : IDisposable
    {
        public const int DefaultPort = 18485;
        public const int OpenUiPacketType = 1000;
        public const int OpenUiWithOptionPacketType = 1001;
        public const int GoToCommoditySnPacketType = 1002;
        public const int NoticeMsgPacketType = 1003;
        public const int ChatMsgPacketType = 1004;
        public const int BuffzoneEffectPacketType = 1005;
        public const int PlayEventSoundPacketType = 1006;
        public const int PlayMinigameSoundPacketType = 1007;
        public const int AskApspEventPacketType = 1008;
        public const int FollowCharacterFailedPacketType = 1009;
        public const int RadioSchedulePacketType = 1010;
        public const int AntiMacroResultPacketType = 1011;
        public const int FollowCharacterPacketType = 1012;
        public const int SetDirectionModePacketType = 1013;
        public const int SetStandAloneModePacketType = 1014;
        public const int FollowCharacterClientPacketType = 193;
        public const int SitResultPacketType = 231;
        public const int MesoGiveSucceededPacketType = 236;
        public const int MesoGiveFailedPacketType = 237;
        public const int RandomMesobagSucceededPacketType = 238;
        public const int RandomMesobagFailedPacketType = 239;
        public const int OpenSkillGuideClientPacketType = 262;
        public const int PlayEventSoundClientPacketType = 246;
        public const int PlayMinigameSoundClientPacketType = 247;
        public const int OpenClassCompetitionPagePacketType = 250;
        public const int MakerResultClientPacketType = PacketOwnedItemMakerResultRuntime.ClientPacketType;
        public const int OpenUiClientPacketType = 251;
        public const int OpenUiWithOptionClientPacketType = 252;
        public const int SetDirectionModeClientPacketType = 253;
        public const int SetStandAloneModeClientPacketType = 254;
        public const int HireTutorClientPacketType = 255;
        public const int TutorMsgClientPacketType = 256;
        public const int ResignQuestReturnClientPacketType = 259;
        public const int PassMateNameClientPacketType = 260;
        public const int EngagementRequestPacketType = 161;
        public const int QuestResultPacketType = 242;
        public const int NotifyHpDecByFieldPacketType = 243;
        public const int NoticeMsgClientPacketType = 263;
        public const int ChatMsgClientPacketType = 264;
        public const int BuffzoneEffectClientPacketType = 265;
        public const int GoToCommoditySnClientPacketType = 266;
        public const int RadioScheduleClientPacketType = 261;
        public const int QuestGuideResultPacketType = 274;
        public const int DeliveryQuestPacketType = 275;
        public const int DamageMeterPacketType = 267;
        public const int TimeBombAttackPacketType = 268;
        public const int FollowCharacterFailedClientPacketType = 270;
        public const int VengeanceSkillApplyPacketType = 271;
        public const int ExJablinApplyPacketType = 272;
        public const int AskApspEventClientPacketType = 273;
        public const int SkillCooltimeSetPacketType = 276;
        public const int FuncKeyMapInitPacketType = 398;
        public const int PetConsumeItemInitPacketType = 399;
        public const int PetConsumeMpItemInitPacketType = 400;
        public const int ParcelDialogPacketType = 1015;
        public const int TrunkDialogPacketType = 1016;
        public const int MessengerDispatchPacketType = 1017;
        public const int MarriageResultPacketType = 1018;
        public const int ItemMakerHiddenRecipeUnlockPacketType = PacketOwnedItemMakerHiddenRecipeUnlockRuntime.PacketType;

        private readonly ConcurrentQueue<LocalUtilityPacketInboxMessage> _pendingMessages = new();
        private readonly object _listenerLock = new();

        private TcpListener _listener;
        private CancellationTokenSource _listenerCancellation;
        private Task _listenerTask;

        public int Port { get; private set; } = DefaultPort;
        public bool IsRunning => _listenerTask != null && !_listenerTask.IsCompleted;
        public int ReceivedCount { get; private set; }
        public string LastStatus { get; private set; } = "Local utility packet inbox inactive.";

        public void Start(int port = DefaultPort)
        {
            lock (_listenerLock)
            {
                if (IsRunning)
                {
                    LastStatus = $"Local utility packet inbox already listening on 127.0.0.1:{Port}.";
                    return;
                }

                StopInternal();

                Port = port <= 0 ? DefaultPort : port;
                _listenerCancellation = new CancellationTokenSource();
                _listener = new TcpListener(IPAddress.Loopback, Port);
                _listener.Start();
                _listenerTask = Task.Run(() => ListenLoopAsync(_listenerCancellation.Token));
                LastStatus = $"Local utility packet inbox listening on 127.0.0.1:{Port}.";
            }
        }

        public void Stop()
        {
            lock (_listenerLock)
            {
                StopInternal();
                LastStatus = "Local utility packet inbox stopped.";
            }
        }

        public void EnqueueLocal(int packetType, byte[] payload, string source)
        {
            string packetSource = string.IsNullOrWhiteSpace(source) ? "local-utility-ui" : source;
            _pendingMessages.Enqueue(new LocalUtilityPacketInboxMessage(packetType, payload, packetSource, packetType.ToString()));
            ReceivedCount++;
            LastStatus = $"Queued {DescribePacketType(packetType)} from {packetSource}.";
        }

        public bool TryDequeue(out LocalUtilityPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void RecordDispatchResult(LocalUtilityPacketInboxMessage message, bool success, string detail)
        {
            string summary = DescribePacketType(message?.PacketType ?? 0);
            LastStatus = success
                ? $"Applied {summary} from {message?.Source ?? "local-utility-inbox"}."
                : $"Ignored {summary} from {message?.Source ?? "local-utility-inbox"}: {detail}";
        }

        public void Dispose()
        {
            lock (_listenerLock)
            {
                StopInternal();
            }
        }

        private async Task ListenLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _listener != null)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                    _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                LastStatus = $"Local utility packet inbox error: {ex.Message}";
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            string remoteEndpoint = client.Client?.RemoteEndPoint?.ToString() ?? "loopback-client";
            try
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        string line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                        if (line == null)
                        {
                            break;
                        }

                        if (!TryParseLine(line, out LocalUtilityPacketInboxMessage message, out string error))
                        {
                            LastStatus = $"Ignored local utility inbox line from {remoteEndpoint}: {error}";
                            continue;
                        }

                        _pendingMessages.Enqueue(new LocalUtilityPacketInboxMessage(message.PacketType, message.Payload, remoteEndpoint, line));
                        ReceivedCount++;
                        LastStatus = $"Queued {DescribePacketType(message.PacketType)} from {remoteEndpoint}.";
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (IOException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                LastStatus = $"Local utility packet inbox client error: {ex.Message}";
            }
        }

        public static bool TryParseLine(string text, out LocalUtilityPacketInboxMessage message, out string error)
        {
            message = null;
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Local utility inbox line is empty.";
                return false;
            }

            string trimmed = text.Trim();
            if (trimmed.StartsWith("/localutilitypacket", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed["/localutilitypacket".Length..].TrimStart();
            }

            if (trimmed.Length == 0)
            {
                error = "Local utility inbox line is empty.";
                return false;
            }

            if (trimmed.StartsWith("packetclientraw", StringComparison.OrdinalIgnoreCase))
            {
                string rawHex = trimmed["packetclientraw".Length..].Trim();
                if (!TryParsePayload(rawHex, out byte[] rawPacket, out error))
                {
                    return false;
                }

                if (!TryDecodeOpcodeFramedPacket(rawPacket, out int clientPacketType, out byte[] clientPayload, out error))
                {
                    return false;
                }

                message = new LocalUtilityPacketInboxMessage(clientPacketType, clientPayload, "local-utility-inbox", text);
                return true;
            }

            int splitIndex = FindTokenSeparatorIndex(trimmed);
            string packetToken = splitIndex >= 0 ? trimmed[..splitIndex].Trim() : trimmed;
            string payloadToken = splitIndex >= 0 ? trimmed[(splitIndex + 1)..].Trim() : string.Empty;

            if (!TryParsePacketType(packetToken, out int packetType))
            {
                error = $"Unsupported local utility packet '{packetToken}'.";
                return false;
            }

            byte[] payload = Array.Empty<byte>();
            if (!string.IsNullOrWhiteSpace(payloadToken))
            {
                if (!TryParsePayload(payloadToken, out payload, out error))
                {
                    return false;
                }
            }

            message = new LocalUtilityPacketInboxMessage(packetType, payload, "local-utility-inbox", text);
            return true;
        }

        public static bool TryParsePacketType(string token, out int packetType)
        {
            packetType = 0;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            if (int.TryParse(token, out packetType))
            {
                return IsSupportedPacketType(packetType);
            }

            if (token.Equals("openui", StringComparison.OrdinalIgnoreCase))
            {
                packetType = OpenUiPacketType;
                return true;
            }

            if (token.Equals("openuiwithoption", StringComparison.OrdinalIgnoreCase)
                || token.Equals("openuioption", StringComparison.OrdinalIgnoreCase))
            {
                packetType = OpenUiWithOptionPacketType;
                return true;
            }

            if (token.Equals("commodity", StringComparison.OrdinalIgnoreCase)
                || token.Equals("gotocommoditysn", StringComparison.OrdinalIgnoreCase))
            {
                packetType = GoToCommoditySnPacketType;
                return true;
            }

            if (token.Equals("notice", StringComparison.OrdinalIgnoreCase)
                || token.Equals("noticemsg", StringComparison.OrdinalIgnoreCase))
            {
                packetType = NoticeMsgPacketType;
                return true;
            }

            if (token.Equals("chat", StringComparison.OrdinalIgnoreCase)
                || token.Equals("chatmsg", StringComparison.OrdinalIgnoreCase))
            {
                packetType = ChatMsgPacketType;
                return true;
            }

            if (token.Equals("buffzone", StringComparison.OrdinalIgnoreCase)
                || token.Equals("buffzoneeffect", StringComparison.OrdinalIgnoreCase))
            {
                packetType = BuffzoneEffectPacketType;
                return true;
            }

            if (token.Equals("eventsound", StringComparison.OrdinalIgnoreCase)
                || token.Equals("playeventsound", StringComparison.OrdinalIgnoreCase))
            {
                packetType = PlayEventSoundPacketType;
                return true;
            }

            if (token.Equals("minigamesound", StringComparison.OrdinalIgnoreCase)
                || token.Equals("playminigamesound", StringComparison.OrdinalIgnoreCase))
            {
                packetType = PlayMinigameSoundPacketType;
                return true;
            }

            if (token.Equals("apspevent", StringComparison.OrdinalIgnoreCase)
                || token.Equals("askapspevent", StringComparison.OrdinalIgnoreCase))
            {
                packetType = AskApspEventPacketType;
                return true;
            }

            if (token.Equals("followfail", StringComparison.OrdinalIgnoreCase)
                || token.Equals("followcharacterfailed", StringComparison.OrdinalIgnoreCase))
            {
                packetType = FollowCharacterFailedPacketType;
                return true;
            }

            if (token.Equals("follow", StringComparison.OrdinalIgnoreCase)
                || token.Equals("followcharacter", StringComparison.OrdinalIgnoreCase))
            {
                packetType = FollowCharacterPacketType;
                return true;
            }

            if (token.Equals("sitresult", StringComparison.OrdinalIgnoreCase)
                || token.Equals("chairsit", StringComparison.OrdinalIgnoreCase)
                || token.Equals("onsitresult", StringComparison.OrdinalIgnoreCase))
            {
                packetType = SitResultPacketType;
                return true;
            }

            if (token.Equals("mesogivesucceeded", StringComparison.OrdinalIgnoreCase)
                || token.Equals("mesogiveok", StringComparison.OrdinalIgnoreCase)
                || token.Equals("onmesogive_succeeded", StringComparison.OrdinalIgnoreCase)
                || token.Equals("onmesogivesucceeded", StringComparison.OrdinalIgnoreCase))
            {
                packetType = MesoGiveSucceededPacketType;
                return true;
            }

            if (token.Equals("mesogivefailed", StringComparison.OrdinalIgnoreCase)
                || token.Equals("mesogivefail", StringComparison.OrdinalIgnoreCase)
                || token.Equals("onmesogive_failed", StringComparison.OrdinalIgnoreCase)
                || token.Equals("onmesogivefailed", StringComparison.OrdinalIgnoreCase))
            {
                packetType = MesoGiveFailedPacketType;
                return true;
            }

            if (token.Equals("randommesobagsucceeded", StringComparison.OrdinalIgnoreCase)
                || token.Equals("mesobagok", StringComparison.OrdinalIgnoreCase)
                || token.Equals("onrandommesobag_succeeded", StringComparison.OrdinalIgnoreCase)
                || token.Equals("onrandommesobagsucceeded", StringComparison.OrdinalIgnoreCase))
            {
                packetType = RandomMesobagSucceededPacketType;
                return true;
            }

            if (token.Equals("randommesobagfailed", StringComparison.OrdinalIgnoreCase)
                || token.Equals("mesobagfail", StringComparison.OrdinalIgnoreCase)
                || token.Equals("onrandommesobag_failed", StringComparison.OrdinalIgnoreCase)
                || token.Equals("onrandommesobagfailed", StringComparison.OrdinalIgnoreCase))
            {
                packetType = RandomMesobagFailedPacketType;
                return true;
            }

            if (token.Equals("directionmode", StringComparison.OrdinalIgnoreCase)
                || token.Equals("setdirectionmode", StringComparison.OrdinalIgnoreCase))
            {
                packetType = SetDirectionModePacketType;
                return true;
            }

            if (token.Equals("standalone", StringComparison.OrdinalIgnoreCase)
                || token.Equals("standalonemode", StringComparison.OrdinalIgnoreCase)
                || token.Equals("setstandalonemode", StringComparison.OrdinalIgnoreCase))
            {
                packetType = SetStandAloneModePacketType;
                return true;
            }

            if (token.Equals("radio", StringComparison.OrdinalIgnoreCase)
                || token.Equals("radioschedule", StringComparison.OrdinalIgnoreCase)
                || token.Equals("onradioschedule", StringComparison.OrdinalIgnoreCase))
            {
                packetType = RadioSchedulePacketType;
                return true;
            }

            if (token.Equals("mapletvset", StringComparison.OrdinalIgnoreCase)
                || token.Equals("mapletvmessage", StringComparison.OrdinalIgnoreCase))
            {
                packetType = MapleTvRuntime.PacketTypeSetMessage;
                return true;
            }

            if (token.Equals("mapletvclear", StringComparison.OrdinalIgnoreCase))
            {
                packetType = MapleTvRuntime.PacketTypeClearMessage;
                return true;
            }

            if (token.Equals("mapletvresult", StringComparison.OrdinalIgnoreCase)
                || token.Equals("mapletvsendresult", StringComparison.OrdinalIgnoreCase))
            {
                packetType = MapleTvRuntime.PacketTypeSendMessageResult;
                return true;
            }

            if (token.Equals("parcel", StringComparison.OrdinalIgnoreCase)
                || token.Equals("parceldialog", StringComparison.OrdinalIgnoreCase)
                || token.Equals("parcelpacket", StringComparison.OrdinalIgnoreCase))
            {
                packetType = ParcelDialogPacketType;
                return true;
            }

            if (token.Equals("trunk", StringComparison.OrdinalIgnoreCase)
                || token.Equals("trunkdialog", StringComparison.OrdinalIgnoreCase)
                || token.Equals("storage", StringComparison.OrdinalIgnoreCase))
            {
                packetType = TrunkDialogPacketType;
                return true;
            }

            if (token.Equals("messengerdispatch", StringComparison.OrdinalIgnoreCase)
                || token.Equals("messengeronpacket", StringComparison.OrdinalIgnoreCase)
                || token.Equals("messengerpacket", StringComparison.OrdinalIgnoreCase))
            {
                packetType = MessengerDispatchPacketType;
                return true;
            }

            if (token.Equals("marriageresult", StringComparison.OrdinalIgnoreCase)
                || token.Equals("weddinginvitation", StringComparison.OrdinalIgnoreCase)
                || token.Equals("onmarriageresult", StringComparison.OrdinalIgnoreCase))
            {
                packetType = MarriageResultPacketType;
                return true;
            }

            if (token.Equals("antimacro", StringComparison.OrdinalIgnoreCase)
                || token.Equals("antimacroresult", StringComparison.OrdinalIgnoreCase)
                || token.Equals("liedetector", StringComparison.OrdinalIgnoreCase))
            {
                packetType = AntiMacroResultPacketType;
                return true;
            }

            if (token.Equals("skillguide", StringComparison.OrdinalIgnoreCase)
                || token.Equals("openskillguide", StringComparison.OrdinalIgnoreCase))
            {
                packetType = OpenSkillGuideClientPacketType;
                return true;
            }

            if (token.Equals("makerresult", StringComparison.OrdinalIgnoreCase)
                || token.Equals("onmakerresult", StringComparison.OrdinalIgnoreCase))
            {
                packetType = MakerResultClientPacketType;
                return true;
            }

            if (token.Equals("makerhiddenunlock", StringComparison.OrdinalIgnoreCase)
                || token.Equals("itemmakerhiddenunlock", StringComparison.OrdinalIgnoreCase)
                || token.Equals("onmakerhiddenunlock", StringComparison.OrdinalIgnoreCase))
            {
                packetType = ItemMakerHiddenRecipeUnlockPacketType;
                return true;
            }

            if (token.Equals("questresult", StringComparison.OrdinalIgnoreCase)
                || token.Equals("onquestresult", StringComparison.OrdinalIgnoreCase))
            {
                packetType = QuestResultPacketType;
                return true;
            }

            if (token.Equals("resignquest", StringComparison.OrdinalIgnoreCase)
                || token.Equals("resignquestreturn", StringComparison.OrdinalIgnoreCase)
                || token.Equals("onresignquestreturn", StringComparison.OrdinalIgnoreCase))
            {
                packetType = ResignQuestReturnClientPacketType;
                return true;
            }

            if (token.Equals("passmatename", StringComparison.OrdinalIgnoreCase)
                || token.Equals("matename", StringComparison.OrdinalIgnoreCase)
                || token.Equals("onpassmatename", StringComparison.OrdinalIgnoreCase))
            {
                packetType = PassMateNameClientPacketType;
                return true;
            }

            if (token.Equals("engagementrequest", StringComparison.OrdinalIgnoreCase)
                || token.Equals("engagerequest", StringComparison.OrdinalIgnoreCase)
                || token.Equals("onengagementrequest", StringComparison.OrdinalIgnoreCase)
                || token.Equals("marriagerequest", StringComparison.OrdinalIgnoreCase))
            {
                packetType = EngagementRequestPacketType;
                return true;
            }

            if (token.Equals("hiretutor", StringComparison.OrdinalIgnoreCase)
                || token.Equals("tutorhire", StringComparison.OrdinalIgnoreCase)
                || token.Equals("onhiretutor", StringComparison.OrdinalIgnoreCase))
            {
                packetType = HireTutorClientPacketType;
                return true;
            }

            if (token.Equals("tutormsg", StringComparison.OrdinalIgnoreCase)
                || token.Equals("ontutormsg", StringComparison.OrdinalIgnoreCase))
            {
                packetType = TutorMsgClientPacketType;
                return true;
            }

            if (token.Equals("hpdec", StringComparison.OrdinalIgnoreCase)
                || token.Equals("hazard", StringComparison.OrdinalIgnoreCase)
                || token.Equals("notifyhpdecbyfield", StringComparison.OrdinalIgnoreCase))
            {
                packetType = NotifyHpDecByFieldPacketType;
                return true;
            }

            if (token.Equals("damagemeter", StringComparison.OrdinalIgnoreCase)
                || token.Equals("damage", StringComparison.OrdinalIgnoreCase)
                || token.Equals("ondamagemeter", StringComparison.OrdinalIgnoreCase))
            {
                packetType = DamageMeterPacketType;
                return true;
            }

            if (token.Equals("classcompetition", StringComparison.OrdinalIgnoreCase)
                || token.Equals("openclasscompetitionpage", StringComparison.OrdinalIgnoreCase)
                || token.Equals("classpage", StringComparison.OrdinalIgnoreCase))
            {
                packetType = OpenClassCompetitionPagePacketType;
                return true;
            }

            if (token.Equals("questguide", StringComparison.OrdinalIgnoreCase)
                || token.Equals("questguideresult", StringComparison.OrdinalIgnoreCase))
            {
                packetType = QuestGuideResultPacketType;
                return true;
            }

            if (token.Equals("deliveryquest", StringComparison.OrdinalIgnoreCase)
                || token.Equals("questdelivery", StringComparison.OrdinalIgnoreCase)
                || token.Equals("delivery", StringComparison.OrdinalIgnoreCase))
            {
                packetType = DeliveryQuestPacketType;
                return true;
            }

            if (token.Equals("timebomb", StringComparison.OrdinalIgnoreCase)
                || token.Equals("timebombattack", StringComparison.OrdinalIgnoreCase)
                || token.Equals("ontimebombattack", StringComparison.OrdinalIgnoreCase))
            {
                packetType = TimeBombAttackPacketType;
                return true;
            }

            if (token.Equals("vengeance", StringComparison.OrdinalIgnoreCase)
                || token.Equals("vengeanceskillapply", StringComparison.OrdinalIgnoreCase)
                || token.Equals("onvengeanceskillapply", StringComparison.OrdinalIgnoreCase))
            {
                packetType = VengeanceSkillApplyPacketType;
                return true;
            }

            if (token.Equals("exjablin", StringComparison.OrdinalIgnoreCase)
                || token.Equals("onexjablinapply", StringComparison.OrdinalIgnoreCase))
            {
                packetType = ExJablinApplyPacketType;
                return true;
            }

            if (token.Equals("skillcooltime", StringComparison.OrdinalIgnoreCase)
                || token.Equals("skillcooltimeset", StringComparison.OrdinalIgnoreCase)
                || token.Equals("cooltime", StringComparison.OrdinalIgnoreCase))
            {
                packetType = SkillCooltimeSetPacketType;
                return true;
            }

            if (token.Equals("funckeymap", StringComparison.OrdinalIgnoreCase)
                || token.Equals("keymap", StringComparison.OrdinalIgnoreCase)
                || token.Equals("funckeyinit", StringComparison.OrdinalIgnoreCase))
            {
                packetType = FuncKeyMapInitPacketType;
                return true;
            }

            if (token.Equals("petconsumehp", StringComparison.OrdinalIgnoreCase)
                || token.Equals("petconsumeitem", StringComparison.OrdinalIgnoreCase)
                || token.Equals("petautohp", StringComparison.OrdinalIgnoreCase))
            {
                packetType = PetConsumeItemInitPacketType;
                return true;
            }

            if (token.Equals("petconsumemp", StringComparison.OrdinalIgnoreCase)
                || token.Equals("petautomp", StringComparison.OrdinalIgnoreCase))
            {
                packetType = PetConsumeMpItemInitPacketType;
                return true;
            }

            return false;
        }

        public static bool IsSupportedPacketType(int packetType)
        {
            return packetType == OpenUiPacketType
                || packetType == OpenUiClientPacketType
                || packetType == OpenUiWithOptionPacketType
                || packetType == OpenUiWithOptionClientPacketType
                || packetType == HireTutorClientPacketType
                || packetType == TutorMsgClientPacketType
                || packetType == GoToCommoditySnPacketType
                || packetType == GoToCommoditySnClientPacketType
                || packetType == NoticeMsgPacketType
                || packetType == NoticeMsgClientPacketType
                || packetType == ChatMsgPacketType
                || packetType == ChatMsgClientPacketType
                || packetType == BuffzoneEffectPacketType
                || packetType == BuffzoneEffectClientPacketType
                || packetType == PlayEventSoundPacketType
                || packetType == PlayEventSoundClientPacketType
                || packetType == PlayMinigameSoundPacketType
                || packetType == PlayMinigameSoundClientPacketType
                || packetType == AskApspEventPacketType
                || packetType == AskApspEventClientPacketType
                || packetType == FollowCharacterFailedPacketType
                || packetType == FollowCharacterFailedClientPacketType
                || packetType == FollowCharacterPacketType
                || packetType == SetDirectionModePacketType
                || packetType == SetStandAloneModePacketType
                || packetType == SetDirectionModeClientPacketType
                || packetType == SetStandAloneModeClientPacketType
                || packetType == FollowCharacterClientPacketType
                || packetType == SitResultPacketType
                || packetType == MesoGiveSucceededPacketType
                || packetType == MesoGiveFailedPacketType
                || packetType == RandomMesobagSucceededPacketType
                || packetType == RandomMesobagFailedPacketType
                || packetType == RadioSchedulePacketType
                || packetType == RadioScheduleClientPacketType
                || packetType == AntiMacroResultPacketType
                || packetType == InitialQuizTimerRuntime.PacketType
                || packetType == OpenSkillGuideClientPacketType
                || packetType == QuestResultPacketType
                || packetType == ResignQuestReturnClientPacketType
                || packetType == PassMateNameClientPacketType
                || packetType == EngagementRequestPacketType
                || packetType == NotifyHpDecByFieldPacketType
                || packetType == OpenClassCompetitionPagePacketType
                || packetType == MakerResultClientPacketType
                || packetType == ItemMakerHiddenRecipeUnlockPacketType
                || packetType == DamageMeterPacketType
                || packetType == TimeBombAttackPacketType
                || packetType == VengeanceSkillApplyPacketType
                || packetType == ExJablinApplyPacketType
                || packetType == QuestGuideResultPacketType
                || packetType == DeliveryQuestPacketType
                || packetType == SkillCooltimeSetPacketType
                || packetType == FuncKeyMapInitPacketType
                || packetType == PetConsumeItemInitPacketType
                || packetType == PetConsumeMpItemInitPacketType
                || packetType == MapleTvRuntime.PacketTypeSetMessage
                || packetType == MapleTvRuntime.PacketTypeClearMessage
                || packetType == MapleTvRuntime.PacketTypeSendMessageResult
                || packetType == ParcelDialogPacketType
                || packetType == TrunkDialogPacketType
                || packetType == MessengerDispatchPacketType
                || packetType == MarriageResultPacketType;
        }

        public static bool TryDecodeOpcodeFramedPacket(byte[] rawPacket, out int packetType, out byte[] payload, out string error)
        {
            packetType = 0;
            payload = Array.Empty<byte>();
            error = null;

            if (rawPacket == null || rawPacket.Length < sizeof(ushort))
            {
                error = "Local utility client packet must include a 2-byte opcode.";
                return false;
            }

            packetType = BitConverter.ToUInt16(rawPacket, 0);
            if (!IsSupportedPacketType(packetType))
            {
                error = $"Unsupported local utility client opcode {packetType}.";
                return false;
            }

            payload = rawPacket.Length == sizeof(ushort)
                ? Array.Empty<byte>()
                : rawPacket[sizeof(ushort)..];
            return true;
        }

        private static bool TryParsePayload(string text, out byte[] payload, out string error)
        {
            payload = Array.Empty<byte>();
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            const string payloadHexPrefix = "payloadhex=";
            const string payloadBase64Prefix = "payloadb64=";
            if (text.StartsWith(payloadHexPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string hex = text[payloadHexPrefix.Length..].Trim();
                if (hex.Length == 0 || (hex.Length % 2) != 0)
                {
                    error = "payloadhex= must contain an even-length hexadecimal byte string.";
                    return false;
                }

                try
                {
                    payload = Convert.FromHexString(hex);
                    return true;
                }
                catch (FormatException)
                {
                    error = "payloadhex= must contain only hexadecimal characters.";
                    return false;
                }
            }

            if (text.StartsWith(payloadBase64Prefix, StringComparison.OrdinalIgnoreCase))
            {
                string base64 = text[payloadBase64Prefix.Length..].Trim();
                if (base64.Length == 0)
                {
                    error = "payloadb64= must not be empty.";
                    return false;
                }

                try
                {
                    payload = Convert.FromBase64String(base64);
                    return true;
                }
                catch (FormatException)
                {
                    error = "payloadb64= must contain a valid base64 payload.";
                    return false;
                }
            }

            try
            {
                payload = Convert.FromHexString(text.Replace(" ", string.Empty, StringComparison.Ordinal));
                return true;
            }
            catch (FormatException)
            {
                error = "Packet payload must use payloadhex=.., payloadb64=.., or a compact raw hex byte string.";
                return false;
            }
        }

        private static int FindTokenSeparatorIndex(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsWhiteSpace(text[i]) || text[i] == ':' || text[i] == '=')
                {
                    return i;
                }
            }

            return -1;
        }

        private static string DescribePacketType(int packetType)
        {
            return packetType switch
            {
                SitResultPacketType => "OnSitResult(231)",
                OpenUiPacketType => "OpenUI(1000)",
                OpenUiWithOptionPacketType => "OpenUIWithOption(1001)",
                GoToCommoditySnPacketType => "GoToCommoditySN(1002)",
                NoticeMsgPacketType => "NoticeMsg(1003)",
                ChatMsgPacketType => "ChatMsg(1004)",
                BuffzoneEffectPacketType => "BuffzoneEffect(1005)",
                PlayEventSoundPacketType => "PlayEventSound(1006)",
                PlayMinigameSoundPacketType => "PlayMinigameSound(1007)",
                AskApspEventPacketType => "AskAPSPEvent(1008)",
                FollowCharacterFailedPacketType => "FollowCharacterFailed(1009)",
                RadioSchedulePacketType => "RadioSchedule(1010)",
                AntiMacroResultPacketType => "AntiMacroResult(1011)",
                InitialQuizTimerRuntime.PacketType => "InitialQuizStart(43)",
                FollowCharacterPacketType => "FollowCharacter(1012)",
                SetDirectionModePacketType => "SetDirectionMode(1013)",
                SetStandAloneModePacketType => "SetStandAloneMode(1014)",
                FollowCharacterClientPacketType => "FollowCharacter(193)",
                MesoGiveSucceededPacketType => "OnMesoGive_Succeeded(236)",
                MesoGiveFailedPacketType => "OnMesoGive_Failed(237)",
                RandomMesobagSucceededPacketType => "OnRandomMesobag_Succeeded(238)",
                RandomMesobagFailedPacketType => "OnRandomMesobag_Failed(239)",
                PlayEventSoundClientPacketType => "PlayEventSound(246)",
                PlayMinigameSoundClientPacketType => "PlayMinigameSound(247)",
                QuestResultPacketType => "OnQuestResult(242)",
                NotifyHpDecByFieldPacketType => "NotifyHPDecByField(243)",
                OpenClassCompetitionPagePacketType => "OpenClassCompetitionPage(250)",
                MakerResultClientPacketType => "OnMakerResult(248)",
                OpenUiClientPacketType => "OpenUI(251)",
                OpenUiWithOptionClientPacketType => "OpenUIWithOption(252)",
                SetDirectionModeClientPacketType => "SetDirectionMode(253)",
                SetStandAloneModeClientPacketType => "SetStandAloneMode(254)",
                HireTutorClientPacketType => "HireTutor(255)",
                TutorMsgClientPacketType => "TutorMsg(256)",
                ResignQuestReturnClientPacketType => "OnResignQuestReturn(259)",
                PassMateNameClientPacketType => "OnPassMateName(260)",
                EngagementRequestPacketType => "SendEngagementRequest(161)",
                NoticeMsgClientPacketType => "NoticeMsg(263)",
                ChatMsgClientPacketType => "ChatMsg(264)",
                BuffzoneEffectClientPacketType => "BuffzoneEffect(265)",
                GoToCommoditySnClientPacketType => "GoToCommoditySN(266)",
                RadioScheduleClientPacketType => "RadioSchedule(261)",
                OpenSkillGuideClientPacketType => "OpenSkillGuide(262)",
                DamageMeterPacketType => "DamageMeter(267)",
                TimeBombAttackPacketType => "OnTimeBombAttack(268)",
                FollowCharacterFailedClientPacketType => "FollowCharacterFailed(270)",
                VengeanceSkillApplyPacketType => "OnVengeanceSkillApply(271)",
                ExJablinApplyPacketType => "OnExJablinApply(272)",
                AskApspEventClientPacketType => "AskAPSPEvent(273)",
                QuestGuideResultPacketType => "QuestGuideResult(274)",
                DeliveryQuestPacketType => "DeliveryQuest(275)",
                SkillCooltimeSetPacketType => "SkillCooltimeSet(276)",
                FuncKeyMapInitPacketType => "FuncKeyMapInit(398)",
                PetConsumeItemInitPacketType => "PetConsumeItemInit(399)",
                PetConsumeMpItemInitPacketType => "PetConsumeMPItemInit(400)",
                MapleTvRuntime.PacketTypeSetMessage => "MapleTV SetMessage(405)",
                MapleTvRuntime.PacketTypeClearMessage => "MapleTV ClearMessage(406)",
                MapleTvRuntime.PacketTypeSendMessageResult => "MapleTV SendMessageResult(407)",
                ParcelDialogPacketType => "ParcelDialog OnPacket(1015)",
                TrunkDialogPacketType => "TrunkDialog OnPacket(1016)",
                MessengerDispatchPacketType => "Messenger OnPacket(1017)",
                MarriageResultPacketType => "MarriageResult OnMarriageResult(1018)",
                ItemMakerHiddenRecipeUnlockPacketType => "ItemMakerHiddenRecipeUnlock(1019)",
                _ => $"packet {packetType}"
            };
        }

        private void StopInternal()
        {
            try
            {
                _listenerCancellation?.Cancel();
            }
            catch
            {
            }

            try
            {
                _listener?.Stop();
            }
            catch
            {
            }

            try
            {
                _listenerTask?.Wait(100);
            }
            catch
            {
            }

            _listenerTask = null;
            _listener = null;
            _listenerCancellation?.Dispose();
            _listenerCancellation = null;
        }
    }
}
