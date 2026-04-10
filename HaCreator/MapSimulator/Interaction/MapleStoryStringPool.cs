using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Interaction
{
    internal static partial class MapleStoryStringPool
    {
        private static readonly IReadOnlyDictionary<int, string> OverrideEntries = new Dictionary<int, string>
        {
            [0x1A15] = "%d",
            // Recovered from MapleStory.exe v95 `CWvsContext::OnDropPickUpMessage`.
            // The generated table drifts for several pickup-notice ids, so pin the
            // verified screen/chat strings here to keep pickup notice formatting on the
            // client-owned path instead of accidentally resolving unrelated text.
            [0x012F] = "You have gained mesos (+%d)",
            [0x0130] = "Internet Cafe Meso Bonus (+%d)",
            [0x0134] = "You can't get anymore items.",
            [0x0BD2] = "Your inventory is full.",
            [0x1491] = "Your pet has picked up some mesos.",
            [0x14D3] = "You cannot acquire any items because the game file has been damaged. Please try again after reinstalling the game.",
            [0x14D9] = "You cannot acquire any items.",
            [0x1542] = "You have gained a(n) %s (%s) x %d.",
            [0x1543] = "You have gained a(n) %s (%s).",
            // Recovered from MapleStory.exe v95 StringPool::GetString. The generated table
            // in this workspace uses the decoded storage order, not the client key lookup,
            // so direct index resolution for these MapleTV result ids is incorrect.
            [0x0F9E] = "The message was successfully sent.",
            [0x0F9F] = "The waiting line is longer than an hour. \r\nPlease try using it at a later time.",
            [0x0FA0] = "You've entered the wrong user name.",
            // Recovered from MapleStory.exe v95 `CUtilDlg::SetUtilDlg`. When callers do not
            // pass an explicit sound name, the client resolves string-pool id 1272 before
            // playing the notice-owner UI sound.
            [0x04F8] = "Sound/UI.img/DlgNotice",
            // Recovered from MapleStory.exe v95 `CReactorPool::LoadReactorLayer`. Reactor-hit
            // layer rebuild formats the per-state hit sound through string-pool id 2121 before
            // dispatching `play_reactor_sound`, so keep the template explicit here rather than
            // relying on generated-table ordering.
            [0x0849] = "Sound/Reactor.img/%s/%s",
            // Recovered from MapleStory.exe v95 StringPool::ms_aString via StringPool::GetString
            // using ms_aKey (0xB98830). These anti-macro ids are still null in the generated
            // table for this workspace, but `CWvsContext::OnAntiMacroResult` and
            // `CWvsContext::ShowAntiMacroNotice` use them directly for the packet-owned
            // anti-macro controller and notice owner.
            [0x0C84] = "The user cannot be found.",
            [0x0C85] = "You cannot use it on a user that isn't in the middle of attack.",
            [0x0C86] = "This user has already been tested before.",
            [0x0C87] = "This user is currently going through the Lie Detector Test.",
            [0x0C88] = "Thank you for cooperating with the Lie Detector Test. You'll be rewarded 5000 mesos for not botting.",
            [0x0C89] = "The Lie Detector Test confirms that you have been botting. Repeated failure of the test will result in game restrictions.",
            [0x0C8D] = "%s have used the Lie Detector Test.",
            [0x0C8E] = "%s_The screenshot has been saved. You have been notified of macro-assisted program monitoring.",
            [0x0C8F] = "%s_The screenshot has been saved. The Lie Detector has been activated.",
            [0x0C90] = "%s_You have passed the Lie Detector Test.",
            [0x0C91] = "%s_The screenshot has been saved. It appears that you may be using a macro-assisted program.",
            [0x0C98] = "The user has failed the Lie Detector Test. You'll be rewarded 7000 mesos from the user.",
            [0x0C99] = "You have succesfully passed the Lie Detector Test. Thank you for participating!",
            [0x0C9A] = "You will be sanctioned for using a macro-assisted program.",
            [0x1A65] = "Thank you for your cooperation.",
            // Recovered from MapleStory.exe v95 `CUIMapTransfer::OnRegister` and
            // `CUIMapTransfer::OnDelete`. The generated table drifts around this block
            // in the current workspace, so pin the client-owned map-transfer prompts
            // and notices here before the simulator formats register/delete/move UI.
            [0x0BB4] = "Your teleport list is full.\r\nPlease delete an entry before trying again.",
            [0x0BB5] = "You have already entered this map.",
            [0x0BB8] = "Will you enter this map\r\nin your teleport list?\r\n[%s]",
            [0x0BB9] = "Will you delete this map from the\r\nteleport list?\r\n[%s]",
            [0x0BBA] = "Will you teleport to this map?\r\n[%s]",
            // Recovered from MapleStory.exe v95 StringPool::ms_aString via StringPool::GetString
            // using ms_aKey (0xB98830). These ids are radio-owner literals that were still null
            // in the generated table for this workspace, but the simulator now needs the exact
            // client text and path templates for CRadioManager parity.
            [0x14CF] = "[%s]'s broadcasting will begin. Please turn up the volume.",
            [0x14D0] = "[%s]'s broadcasting has ended.",
            [0x1501] = "Sound/Radio.img/%s",
            [0x1502] = "Sound/Radio.img/%s/track",
            // Recovered from MapleStory.exe v95 `CUser::SetNewYearCardEffect`. The client
            // resolves the New Year midpoint vector class and `LoadLayer` item-effect
            // template through StringPool before formatting the owned card item id.
            [0x03D2] = "Shape2D#Vector2D",
            [0x09AB] = "Effect/ItemEff.img/%d",
            // Recovered from MapleStory.exe v95 dragon-layer owners. `CDragon::CreateEffect`
            // resolves these ids through StringPool before loading the layer from Effect/BasicEff.
            [0x0B6B] = "Effect/BasicEff.img/dragonBlink",
            [0x15DA] = "Effect/BasicEff.img/dragonFury",
            // Recovered from MapleStory.exe v95 `CWvsContext::OnSkillLearnItemResult` and
            // `CUIVega::OnVegaResult`. The generated table drifts for these production and
            // enhancement sound ids, so pin the verified sound paths here before the shared
            // production/enhancement owners resolve them.
            [0x0507] = "Sound/Game.img/EnchantSuccess",
            [0x0508] = "Sound/Game.img/EnchantFailure",
            [0x1534] = "Sound/UI.img/EnchantDelay",
            // Recovered from MapleStory.exe v95 `CWvsContext::OnSkillLearnItemResult`.
            // The packet-owned skill-book result branch formats these exact notices
            // through StringPool before writing to the status-bar chat log.
            [0x0F2F] = "Mastery Book",
            [0x0F30] = "Skill Book",
            [0x0F31] = "You cannot use %s.",
            [0x0F32] = "Despite using %s, the effect was nowhere to be found.",
            [0x0F33] = "The Book of Mastery glows brightly, and the current skills have gone through an upgrade.",
            [0x0F34] = "The Skill Book glows brightly, and new skills have now been added.",
            [0x0FF1] = "Effect/BasicEff.img/SkillBook/Success/0",
            [0x0FF2] = "Effect/BasicEff.img/SkillBook/Success/1",
            [0x0FF3] = "Effect/BasicEff.img/SkillBook/Failure/0",
            [0x0FF4] = "Effect/BasicEff.img/SkillBook/Failure/1",
            // Recovered from MapleStory.exe v95 `CUIAccountMoreInfo::LoadCountryName`
            // and `CUIAccountMoreInfo::OnSaveAccountMoreInfoResult`. The generated
            // table drifts in this block, so keep these account-more-info owner
            // literals explicit instead of resolving unrelated FriendRecommendations
            // resource paths.
            [0x16B7] = "Fail. Please try again later.",
            [0x16B8] = "Select",
            // Recovered from MapleStory.exe v95 `CDragon::UpdateQuestInfo`. The generated table
            // resolves 0x19BC to an unrelated UI list frame in this workspace, but the client
            // formats the dragon quest-info layer path from this string-pool slot with the raw
            // quest-state integer before loading the layer. `DragonCompanionRuntime` keeps the
            // client-shaped direct format first, then falls back to the verified v95 WZ mapping
            // (`QuestAlert`, `QuestAlert2`, `QuestAlert3`) when the formatted node is absent.
            [0x19BC] = "Effect/BasicEff.img/QuestAlert%d",
            // Recovered from MapleStory.exe v95 `CUIWeddingInvitation::OnCreate` and `Draw`.
            // The client resolves the invitation button UOL, the dialog backgrounds, and the
            // basic-black face through these StringPool ids before wiring the owner. Keep them
            // explicit here so wedding invitation parity does not drift if the generated table
            // changes shape again.
            [0x0EAF] = "UI/UIWindow.img/Wedding/Invitation/Vegas",
            [0x0EB0] = "UI/UIWindow.img/Wedding/Invitation/Cathedral",
            [0x19CE] = "UI/UIWindow2.img/Wedding/Invitation/BtOK",
            [0x1A25] = "Arial",
            // Recovered from MapleStory.exe v95 `CField::OnFieldEffect` /
            // `CField::ShowScreenEffect`. Keep these packet-owned field-feedback
            // effect templates explicit so summon and screen-effect resolution
            // does not silently fall back to simulator-owned defaults.
            [0x0663] = "Effect/Summon.img/%d",
            // Recovered from MapleStory.exe v95 `CAnimationDisplayer::Effect_RewardRullet`.
            // The generated table in this workspace drifts for the reward-roulette
            // owner strings, so pin the client-owned map-effect templates here.
            [0x03DA] = "%s%s",
            [0x09ED] = "Effect/MapEff.img/%s",
            [0x11E0] = "Map/Effect.img/miro/RR1/%d",
            [0x11E1] = "Map/Effect.img/miro/RR2/%d",
            [0x11E2] = "Map/Effect.img/miro/RR3/%d",
            // Recovered from MapleStory.exe v95 `CSetGuildMarkDlg::OnCreate`. The guild-mark
            // combo uses these client string ids rather than the raw WZ family node names.
            [0x0D14] = "Animal",
            [0x0D15] = "Plant",
            [0x0D16] = "Pattern",
            [0x0D17] = "Letter",
            [0x0D18] = "Etc",
            // Recovered from MapleStory.exe v95 `CUIFamily::Draw` and
            // `CUIFamilyChart::Draw` / `_DrawChartItem`. Keep these family ids
            // explicit here so the simulator follows the client wording even if
            // regenerated string-pool data drifts.
            [0x11FD] = "(You do not have family members.)",
            [0x1200] = "(You do not have a family yet.)",
            [0x1201] = "[Please add a Junior.]",
            [0x1202] = "%s Family",
            [0x1203] = "Senior(%d ppl.)",
            [0x1204] = "Junior(%d ppl.)",
            // Recovered from MapleStory.exe v95 `COmokDlg`. The generated table drifts for this
            // minigame block, so keep the explicit Omok prompts, notices, and MiniGame sound ids
            // here where the dialog-owner runtime can resolve them directly.
            [0x01D4] = "You win.",
            [0x01D5] = "It's a tie.",
            [0x01D6] = "You lost.",
            // Recovered from MapleStory.exe v95 `CMemoryGameDlg` / `CMiniRoomBaseDlg`.
            // The generated table currently lines up for this block, but pin the Match Cards
            // MiniRoom prompts and system messages here so the dialog owner stays on the same
            // shared StringPool seam even if a later regeneration drifts the minigame range.
            [0x01C4] = "[%s] have entered.",
            [0x01C5] = "[%s] have left.",
            [0x01C6] = "[%s] have called to leave after this game.",
            [0x01C7] = "[%s] have cancelled the request to leave after this game.",
            [0x01C8] = "[%s] have been expelled.",
            [0x01CA] = "[%s] have forfeited.",
            [0x01CB] = "[%s] have requested a handicap.",
            [0x01CC] = "You have left the room.",
            [0x01CD] = "[%s]'s turn.",
            [0x01CE] = "[%s] has matched cards. Please continue.",
            [0x01CF] = "[%s] can't start the game due to lack of mesos.",
            [0x01D0] = "The game has started.",
            [0x01D1] = "The game has ended.\r\nThe room will automatically close in 10 sec.",
            [0x01D2] = "10 sec. left.",
            [0x01D3] = "The room is closed.",
            [0x01D9] = "Your opponent requests a tie.\r\nWill you accept it?",
            [0x01DA] = "Will you request a tie?",
            [0x01DB] = "Your opponent denied your request for a tie.",
            [0x01D7] = "Are you sure you want to give up?",
            [0x01D8] = "Will you expel the user?",
            [0x01E0] = "Will you call to leave after this game?",
            [0x01E1] = "Will you cancel the request\r\nto leave after this game?",
            [0x01E4] = "Are you sure you want to leave?",
            [0x01DD] = "Your oppentent has requested to \r\nwithdraw his/her last move.\r\nDo you accept?",
            [0x01DE] = "Request to withdraw your last move?",
            [0x01DF] = "Your opponent denied your request.",
            [0x01E5] = "Time left : %d sec.",
            [0x01E6] = "It's [ %s ]'s turn.",
            [0x0645] = "Draw",
            [0x0646] = "Win",
            [0x0647] = "Loose",
            [0x0648] = "Timer",
            [0x064B] = "Mushroom",
            [0x064C] = "Slime",
            // The generated v95 string-pool table already carries the Snowball notice ids,
            // but these three entries arrive with `%2C`-escaped commas. Keep the
            // client-owned literals explicit here so `CField_SnowBall::OnSnowBallMsg`
            // resolves clean chat text through the existing StringPool seam.
            [0x0D75] = "%s Team's snowball has passed the %d stage.",
            [0x0D76] = "%s Team is attacking the snowman, stopping the progress of %s Team's snowball.",
            [0x0D77] = "%s Team's snowball is moving again.",
            // Recovered from MapleStory.exe v95 `CField_Wedding::OnWeddingProgress`. The
            // client resolves the opening wedding BGM through ids 0x108E/0x108F before step
            // 0 on the Cathedral or Saint Maple ceremony maps. The current WZ export only
            // exposes `Sound/BgmEvent.img/wedding`, so keep both ids pinned to that shared
            // path here instead of leaving the wedding runtime on a local constant.
            [0x108E] = "BgmEvent/wedding",
            [0x108F] = "BgmEvent/wedding",
            [0x1090] = "Would you like to give your blessing to the couple?",
            // Recovered from MapleStory.exe v95 `CField_MonsterCarnival::OnRequestResult`,
            // `OnProcessForDeath`, `OnShowGameResult`, and `OnShowMemberOutMsg`. The
            // generated table currently carries this block, but pin the client-owned ids
            // here so Monster Carnival parity does not drift if the CSV ordering changes.
            [0x1017] = "Maple Red",
            [0x1018] = "Maple Blue",
            [0x1019] = "[%s] has become unable to fight and [%s]team has lost %d CP.",
            [0x101A] = "\t\t[%s] has become unable to fight but [%s] has no CP so [%s] team did not lose any CP",
            [0x101B] = "You don't have enough CP to continue.",
            [0x101C] = "You can no longer summon the Monster.",
            [0x101D] = "You can no longer summon the being.",
            [0x101E] = "This being is already summoned.",
            [0x101F] = "\t\tThis request has failed due to an unknown error.",
            [0x1020] = "\tYou have won the Monster Carnival. Please wait as you'll be transported out of here shortly.",
            [0x1021] = "Unfortunately%2C you have lost the Monster Carnival. Please wait as you'll be transported out of here shortly.",
            [0x1022] = "Despite the Overtime%2C the carnival ended in a draw. Please wait as you'll be transported out of here shortly.",
            [0x1023] = "Monster Carnival has ended abruptly due to the opposing team leaving the game too early. Please wait as you'll be transported out of here shortly.",
            [0x1024] = "[%s] has summoned a being. [%s]",
            [0x1025] = "[%s] has used a skill. [%s]",
            [0x1026] = "[%s] has summoned the being. [%s]",
            [0x1027] = "\tMonster Carnival is now underway!!",
            [0x1028] = "\tBecause the carnival ended in a draw%2C there will be a 2 minute overtime.",
            [0x1029] = "[%s] of Team [%s] has quit the Monster Carnival.",
            [0x102A] = "Since the leader of the Team [%s] quit the Monster Carnival%2C [%s] has been appointed as the new leader of the team.",
            [0x102B] = "UI/UIWindow.img/MonsterCarnival/backgrnd2",
            [0x102C] = "UI/UIWindow.img/MonsterCarnival/backgrnd",
            [0x102D] = "UI/UIWindow.img/MonsterCarnival/backgrnd3/top/0",
            [0x102E] = "UI/UIWindow.img/MonsterCarnival/backgrnd3/middle1/0",
            [0x102F] = "UI/UIWindow.img/MonsterCarnival/backgrnd3/bottom/0",
            [0x1030] = "UI/UIWindow.img/MonsterCarnival/backgrnd3/middle0/0",
            [0x1031] = "UI/UIWindow.img/MonsterCarnival/BtSide",
            [0x1032] = "UI/UIWindow.img/MonsterCarnival/Tab/enabled",
            [0x1033] = "UI/UIWindow.img/MonsterCarnival/Tab/disabled",
            // Recovered from MapleStory.exe v95 `CTimerboard_Massacre::OnCreate`,
            // `CField_Massacre::Update`, `CField_Massacre::Init`, and
            // `CField_MassacreResult::OnMassacreResult`. The generated table drifts across
            // this block in the current workspace, which can resolve real but incorrect
            // MapleEvent / MonsterKilling resources. Pin the exact Massacre ids here so the
            // HUD, timerboard, key animation, gauge, and result-owner surfaces stay on the
            // client-backed resource paths.
            [0x14EC] = "killing/clear",
            [0x14EE] = "Map/Obj/etc.img/killing/backgrnd",
            [0x1510] = "UI/UIWindow.img/MonsterKilling/Count/keyBackgrd/close",
            [0x1511] = "UI/UIWindow.img/MonsterKilling/Count/keyBackgrd/ing",
            [0x1512] = "UI/UIWindow.img/MonsterKilling/Count/keyBackgrd/open",
            [0x1513] = "UI/UIWindow.img/MonsterKilling/Count/number2",
            [0x1516] = "UI/UIWindow.img/MonsterKilling/Gauge/backgrdD",
            [0x1517] = "UI/UIWindow.img/MonsterKilling/Gauge/danger",
            [0x1518] = "UI/UIWindow.img/MonsterKilling/Gauge/iconD",
            [0x1519] = "UI/UIWindow.img/MonsterKilling/Gauge/pixel",
            [0x151A] = "UI/UIWindow.img/MonsterKilling/Gauge/text",
            [0x151B] = "UI/UIWindow.img/MonsterKilling/Gauge/textD",
            [0x151C] = "UI/UIWindow.img/MonsterKilling/Result/backgrd",
            [0x151D] = "UI/UIWindow.img/MonsterKilling/Result/backgrd2",
            [0x151E] = "UI/UIWindow.img/MonsterKilling/Result/number",
            [0x151F] = "UI/UIWindow.img/MonsterKilling/Result/number2",
            [0x1520] = "UI/UIWindow.img/MonsterKilling/Result/Rank/a",
            [0x1521] = "UI/UIWindow.img/MonsterKilling/Result/Rank/b",
            [0x1522] = "UI/UIWindow.img/MonsterKilling/Result/Rank/c",
            [0x1523] = "UI/UIWindow.img/MonsterKilling/Result/Rank/d",
            [0x1524] = "UI/UIWindow.img/MonsterKilling/Result/Rank/s",
            // Recovered from MapleStory.exe v95 `CUIQuestAlarm::Draw` and
            // `CUIQuestAlarm::OnButtonClicked`. Keep these quest-alarm ids explicit so the
            // owner retains the exact client title and notice strings even if regenerated
            // string-pool order drifts again.
            [0x0E4C] = "Quest Helper (%d/5)",
            [0x106F] = "[%s] It has been excluded from the auto alarm and it will not be automatically reigstered until you re log-on",
            [0x18EC] = "There are no quests in the quest helper.",
            // Recovered from MapleStory.exe v95 `CEngageDlg` and the surrounding
            // CWvsContext engagement result handlers. The generated table in this
            // workspace already carries most of this block, but keeping the proposal
            // literals explicit here protects the owner from string-pool drift and
            // fixes the `%2C`-escaped withdrawn-request notice.
            [0x1093] = "Waiting for her response...",
            [0x109B] = "%s has requested engagement.\r\nWill you accept this proposal?",
            [0x109D] = "You are now engaged.",
            [0x109E] = "You are now married!",
            [0x109F] = "She has politely declined your engagement request.",
            [0x10A0] = "Your engagement has been broken.",
            [0x10A1] = "You are no longer married.",
            [0x10A2] = "You have entered the wrong character name.",
            [0x10A3] = "Your partner has to be in the same map.",
            [0x10A4] = "Your partner's ETC slots are full.",
            [0x10A5] = "You cannot be engaged to the same gender.",
            [0x10A6] = "You are already engaged.",
            [0x10A7] = "You are already married.",
            [0x10A8] = "She is already engaged.",
            [0x10A9] = "This person is already married.",
            [0x10AA] = "You're already in middle or proposing a person.",
            [0x10AB] = "She is currently being asked by another suitor.",
            [0x10AC] = "Unfortunately, the man who proposed to you has withdrawn his request for an engagement.",
            [0x10AD] = "You can't break the engagement after making reservations.",
            [0x10AE] = "The reservation has been canceled. Please try again later.",
            [0x10AF] = "This invitation is not valid.",
            [0x10B0] = "Congratulations!\r\nYour reservation was successfully made!",
            [0x10B1] = "Your ETC slot is full.\r\nPlease remove some items.",
            [0x10B2] = "Please enter your partner's name.",
        };

        public static int Count => Entries.Length;

        public static bool Contains(int stringPoolId)
        {
            return (uint)stringPoolId < (uint)Entries.Length;
        }

        public static bool TryGet(int stringPoolId, out string text)
        {
            if (OverrideEntries.TryGetValue(stringPoolId, out text))
            {
                return true;
            }

            if (Contains(stringPoolId))
            {
                text = Entries[stringPoolId];
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return true;
                }
            }

            text = null;
            return false;
        }

        public static string GetOrFallback(int stringPoolId, string fallbackText, bool appendFallbackSuffix = false, int minimumHexWidth = 0)
        {
            if (TryGet(stringPoolId, out string resolvedText))
            {
                return resolvedText;
            }

            if (!appendFallbackSuffix)
            {
                return fallbackText;
            }

            return $"{fallbackText} ({FormatFallbackLabel(stringPoolId, minimumHexWidth)} fallback)";
        }

        public static string GetCompositeFormatOrFallback(
            int stringPoolId,
            string fallbackFormat,
            int maxPlaceholderCount,
            out bool usedResolvedText)
        {
            if (TryGet(stringPoolId, out string resolvedFormat))
            {
                usedResolvedText = true;
                return ConvertPrintfFormatToCompositeFormat(resolvedFormat, maxPlaceholderCount);
            }

            usedResolvedText = false;
            return fallbackFormat;
        }

        public static string FormatFallbackLabel(int stringPoolId, int minimumHexWidth = 0)
        {
            string format = minimumHexWidth > 0 ? $"X{minimumHexWidth}" : "X";
            return $"StringPool 0x{stringPoolId.ToString(format)}";
        }

        private static string ConvertPrintfFormatToCompositeFormat(string format, int maxPlaceholderCount)
        {
            if (string.IsNullOrWhiteSpace(format))
            {
                return string.Empty;
            }

            int tokenIndex = 0;
            int searchStart = 0;
            while (tokenIndex < maxPlaceholderCount)
            {
                int markerIndex = FindNextPrintfPlaceholder(format, searchStart);
                if (markerIndex < 0)
                {
                    break;
                }

                string replacement = $"{{{tokenIndex}}}";
                format = format.Remove(markerIndex, 2).Insert(markerIndex, replacement);
                searchStart = markerIndex + replacement.Length;
                tokenIndex++;
            }

            return format;
        }

        private static int FindNextPrintfPlaceholder(string format, int searchStart)
        {
            int stringIndex = format.IndexOf("%s", searchStart, StringComparison.Ordinal);
            int digitIndex = format.IndexOf("%d", searchStart, StringComparison.Ordinal);

            if (stringIndex < 0)
            {
                return digitIndex;
            }

            if (digitIndex < 0)
            {
                return stringIndex;
            }

            return Math.Min(stringIndex, digitIndex);
        }
    }
}
