using System;
using System.Collections.Generic;
using System.Globalization;

namespace HaCreator.MapSimulator.Interaction
{
    internal static partial class MapleStoryStringPool
    {
        internal const int MobAngerGaugeBurstTemplatePathStringPoolId = 0x03CE;
        internal const int MobAngerGaugeBurstEffectNameStringPoolId = 0x0C2F;
        private const string MobAngerGaugeBurstTemplatePathFallback = "Mob/%07d.img";
        private const string MobAngerGaugeBurstEffectNameFallback = "AngerGaugeEffect";

        private static readonly IReadOnlyDictionary<int, string> OverrideEntries = new Dictionary<int, string>
        {
            // Recovered from MapleStory.exe v95 `CField_Coconut::DrawBoard`.
            // The Coconut board formats score and finish-timestamp time through
            // these StringPool ids before drawing the WZ bitmap glyphs.
            [0x0B02] = "%d",
            [0x0B0C] = "%d:%02d",
            [0x1A15] = "%d",
            // Recovered from MapleStory.exe v95 `CMob::AngerGaugeFullChargeEffect`.
            // Keep the mob-template and anger-gauge effect ids explicit here so the
            // owner seam can keep formatting `Mob/%07d.img/AngerGaugeEffect` from the
            // client StringPool ids even when generated-table ordering drifts.
            [0x03CE] = "Mob/%07d.img",
            [0x0C2F] = "AngerGaugeEffect",
            // Recovered from MapleStory.exe v95 `CWvsContext::OnDropPickUpMessage`.
            // The generated table drifts for several pickup-notice ids, so pin the
            // verified screen/chat strings here to keep pickup notice formatting on the
            // client-owned path instead of accidentally resolving unrelated text.
            [0x012F] = "You have gained mesos (+%d)",
            [0x0130] = "Internet Cafe Meso Bonus (+%d)",
            [0x0134] = "You can't get anymore items.",
            // Recovered from MapleStory.exe v95 `format_string`, which
            // `CWvsContext::OnDropPickUpMessage` uses before formatting
            // long item names into StringPool[0x1542]/[0x1543].
            [0x08B8] = "..",
            [0x0BD2] = "Your inventory is full.",
            [0x1491] = "Your pet has picked up some mesos.",
            [0x14D3] = "You cannot acquire any items because the game file has been damaged. Please try again after reinstalling the game.",
            [0x14D9] = "You cannot acquire any items.",
            [0x1542] = "You have gained a(n) %s (%s) x %d.",
            [0x1543] = "You have gained a(n) %s (%s).",
            // Recovered from MapleStory.exe v95 StringPool::GetString. The generated table
            // in this workspace uses the decoded storage order, not the client key lookup,
            // so direct index resolution for these MapleTV result ids is incorrect.
            [0x0F8D] = "UI/MapleTV.img/TVmedia",
            [0x0F9E] = "The message was successfully sent.",
            [0x0F9F] = "The waiting line is longer than an hour. \r\nPlease try using it at a later time.",
            [0x0FA0] = "You've entered the wrong user name.",
            // Recovered from MapleStory.exe v95 `CUILogoutGift::OnCreate`.
            // The logout-gift owner binds `CCtrlButton::CreateWnd` through StringPool id 0x146
            // (`paramButton.sUOL`) for `UI/Login.img/CharSelect/BtSelect`, so pin it here
            // because generated-table ordering in this workspace resolves 0x146 incorrectly.
            [0x0146] = "UI/Login.img/CharSelect/BtSelect",
            // Recovered from MapleStory.exe v95 `CAvatarMegaphone::OnCreate`.
            // The owner chooses id 0x0FB0 or 0x0FB1 from measured sender-name width
            // before resolving the name-tag canvas through the resource manager.
            [0x0FB0] = "Map/MapHelper.img/AvatarMegaphone/name/0",
            [0x0FB1] = "Map/MapHelper.img/AvatarMegaphone/name/1",
            // Recovered from MapleStory.exe v95 `CUserLocal::OnMesoGive_Succeeded`,
            // `CUserLocal::OnMesoGive_Failed`, `CUserLocal::OnRandomMesobag_Succeeded`,
            // `CUserLocal::OnRandomMesobag_Failed`, `CUIRandomMesoBag::CUIRandomMesoBag`,
            // `CUIRandomMesoBag::OnCreate`, and `CUtilDlg::OnCreate`. Keep the reward-result
            // owner text and resource ids explicit here so this packet-owned family stays on
            // the client-confirmed StringPool seam even if regenerated key ordering drifts.
            [0x032E] = "You have received %d mesos.",
            [0x032F] = "You have failed to use the meso bag.",
            [0x03D0] = "UI/UIWindow2.img/UtilDlgEx/notice",
            // Recovered from MapleStory.exe v95 `CUtilDlg::SetUtilDlg`. When callers do not
            // pass an explicit sound name, the client resolves string-pool id 1272 before
            // playing the notice-owner UI sound.
            [0x04F8] = "Sound/UI.img/DlgNotice",
            // Recovered from MapleStory.exe v95 `CUserLocal::OnQuestResult`. Keep the
            // packet-owned quest-result wrapper strings explicit here so subtype 10/12
            // notices stay on the client-confirmed StringPool seam even when the generated
            // table drifts or preserves an unrelated line-broken variant.
            [0x0CDC] = "%s item inventory is full.",
            [0x0CDD] = " or",
            [0x1015] = "The [%s] quest expired because the time limit ended",
            [0x11C8] = "Either you don't have enough Mesos or %s",
            [0x1961] = "UI/UIWindow2.img/UtilDlgEx/BtClose",
            [0x1963] = "UI/UIWindow2.img/UtilDlgEx/BtOK",
            [0x1965] = "UI/UIWindow2.img/UtilDlgEx/line",
            [0x1966] = "UI/UIWindow2.img/UtilDlgEx/c",
            [0x1967] = "UI/UIWindow2.img/UtilDlgEx/s",
            [0x196F] = "UI/UIWindow2.img/UtilDlgEx/t",
            [0x17A9] = "UI/UIWindow.img/RandomMesoBag/Back1",
            [0x17AA] = "UI/UIWindow.img/RandomMesoBag/Back2",
            [0x17AB] = "UI/UIWindow.img/RandomMesoBag/Back3",
            [0x17AC] = "UI/UIWindow.img/RandomMesoBag/Back4",
            [0x17AD] = "UI/UIWindow.img/RandomMesoBag/BtOk",
            [0x17AF] = "A small amount of mesos!",
            [0x17B0] = "An adequate amount of mesos!",
            [0x17B1] = "A large amount of mesos!",
            [0x17B2] = "A huge amount of mesos!",
            [0x17B3] = "You have failed to use the Random Meso Sack.",
            [0x17B4] = "You obtained %d mesos from the Random Meso Sack.",
            [0x17B6] = "Sound/Item.img/02000010/Use",
            [0x17B7] = "Sound/Item.img/02000011/Use",
            [0x17B8] = "Sound/Item.img/02022108/Use",
            [0x17B9] = "Sound/Item.img/02022109/Use",
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
            // Recovered from MapleStory.exe v95 `CUIMapTransfer::OnRegister`,
            // `CUIMapTransfer::OnDelete`, and `CWvsContext::OnMapTransferResult`.
            // The generated table drifts around this block in the current workspace,
            // so pin the client-owned map-transfer prompts and notices here before
            // the simulator formats register/delete/move UI or packet result failures.
            [0x0BB0] = "%s is currently difficult to locate, so\r\nthe teleport will not take place.",
            [0x0BB3] = "This map is not available to enter for the list.",
            [0x0BB4] = "Your teleport list is full.\r\nPlease delete an entry before trying again.",
            [0x0BB5] = "You have already entered this map.",
            [0x0BB6] = "It's the map you're currently on.",
            [0x0BB7] = "Users below level 7 are not allowed \r\nto go out from Maple Island.",
            [0x0BB8] = "Will you enter this map\r\nin your teleport list?\r\n[%s]",
            [0x0BB9] = "Will you delete this map from the\r\nteleport list?\r\n[%s]",
            [0x0BBA] = "Will you teleport to this map?\r\n[%s]",
            [0x0BD3] = "You cannot go to that place.",
            // Recovered from MapleStory.exe v95 `CField::OnTransferFieldReqIgnored`
            // (`0x52f3b0`) and `CField::OnTransferChannelReqIgnored` (`0x52f5f0`).
            // The generated table drifts heavily in these ranges in this workspace,
            // so pin the packet-owned transfer ignored reason families explicitly.
            [0x0181] = "The portal is closed for now.",
            [0x0BD4] = "This map cannot be entered right now.",
            [0x0BEF] = "The transfer request was ignored by the client warning path.",
            [0x0D30] = "Another channel transfer is already pending.",
            [0x0D31] = "The selected channel is unavailable.",
            [0x1299] = "The current field blocks channel change.",
            [0x12DA] = "The selected channel rejected the transfer.",
            [0x12DC] = "Channel change is unavailable right now.",
            [0x155B] = "The current field rules block map transfer right now.",
            [0x168B] = "The transfer portal is not ready yet.",
            [0x1A83] = "The requested field transfer is unavailable.",
            // Recovered from MapleStory.exe v95 `CField::OnWhisper` (`0x5448A0`),
            // `CField::OnGroupMessage` (`0x535490`), `CField::OnCoupleMessage`
            // (`0x5357F0`), and `CField::OnPlayJukeBox` (`0x537940`). The generated
            // table can drift in this region, so keep these packet-owned field chat
            // and notice families pinned to client-owned ids.
            [0x009A] = "%s have currently disabled whispers.",
            [0x009B] = "%s is on %s.",
            [0x009C] = "%s could not be found.",
            [0x009D] = "%s is in %s.",
            [0x009E] = "%s is in a hidden field.",
            [0x009F] = "You have whispered to '%s'",
            [0x00A1] = "Couple notice is unavailable.",
            [0x02D7] = "%s: %s",
            [0x072D] = "%s: %s",
            [0x072E] = "%s: %s",
            [0x072F] = "%s (%s): %s",
            [0x0730] = "> %s: %s",
            [0x18E0] = "Hidden field.",
            [0x1A2D] = "Not found.",
            [0x1AC3] = "%s played %s through the field jukebox.",
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
            // Recovered from MapleStory.exe v95 `CUIAccountMoreInfo::OnCreate`,
            // `CUIAccountMoreInfo::LoadCountryName`, `CUIAccountMoreInfo::OnDestroy`,
            // and `CUIAccountMoreInfo::OnSaveAccountMoreInfoResult`. The generated
            // table drifts in this block, so keep these account-more-info owner
            // literals explicit instead of resolving unrelated FriendRecommendations
            // resource paths.
            [0x16AE] = "UI/UIWindow.img/FriendRecommendations/UserInfo/back",
            [0x16B6] = "Please fill in your information later. If not, you may not receive friend recommendations.",
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
            [0x19CB] = "UI/UIWindow2.img/Wedding/Invitation/neat",
            [0x19CC] = "UI/UIWindow2.img/Wedding/Invitation/sweet",
            [0x19CD] = "UI/UIWindow2.img/Wedding/Invitation/premium",
            [0x19CE] = "UI/UIWindow2.img/Wedding/Invitation/BtOK",
            [0x1A25] = "Arial",
            // Recovered from MapleStory.exe v95 `CUIInitialQuiz::CUIInitialQuiz`,
            // `CUIInitialQuiz::OnCreate`, and `CUIInitialQuiz::Draw`. The generated
            // table is missing these owner ids in this workspace, but the client
            // resolves the dialog background, timer glyph UOLs, edit font, button
            // UOL, labels, and validation notices through StringPool before drawing
            // the context-owned initial quiz.
            [0x0512] = "UI/UIWindow2.img/InitialQuiz/BtOK",
            [0x0F72] = "UI/UIWindow2.img/InitialQuiz/backgrnd",
            [0x0F73] = "UI/UIWindow2.img/InitialQuiz/num1/%d",
            [0x0F74] = "UI/UIWindow2.img/InitialQuiz/num1/comma",
            [0x0F75] = "Question:",
            [0x0F76] = "Clue:",
            [0x0F77] = "Answer:",
            [0x0F78] = "Enter your answer.",
            [0x0F79] = "You must enter atleast %d letters. (Korean)",
            [0x0F7A] = "You must enter less than %d letters. (Korean)",
            [0x0F7C] = "Time is over.",
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
            // Recovered from MapleStory.exe v95 `CWvsContext::OnGuildResult(77..79)`.
            // The generated table stores URL-escaped punctuation in this block, but the client
            // emits these strings directly through StringPool before formatting guild-quest
            // queue notices into the status-bar chat log.
            [0x0DFD] = "There are less than 6 members remaining, so the quest cannot continue. Your Guild Quest will end in 5 seconds.",
            [0x0DFE] = "The user that registered has disconnected, so the quest cannot continue. Your Guild Quest will end in 5 seconds.",
            [0x0DFF] = "Please go see the Guild Quest NPC at Channel %s immediately to enter.",
            [0x0E00] = "Your guild is up next. Please head to the Guild Quest map at Channel %s and wait.",
            [0x0E01] = "There's currently 1 guild participating in the Guild Quest, and your guild is number %d on the waitlist.",
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
            // Recovered from MapleStory.exe v95 `CField_CookieHouse::Init`,
            // `CField_CookieHouse::Update`, and `CWvsContext::OnSessionValue`. The current
            // generated table keeps the Cookie House WZ ids stable, but the session-value key
            // slot at 0x11D9 still drifts in this workspace, so keep the client-owned Cookie
            // House block explicit here before the live-session bridge resolves opcode 93.
            [0x11D9] = "cookiePoint",
            [0x13FB] = "Map/Obj/etc.img/eventPointCount/backgrnd",
            [0x13FC] = "Map/Obj/etc.img/eventPointCount",
            [0x14F7] = "minus",
            [0x14FA] = "plus",
            // Recovered from MapleStory.exe v95 `CTimerboard_Massacre::OnCreate`,
            // `CField_Massacre::Update`, `CField_Massacre::Init`, and
            // `CField_MassacreResult::OnMassacreResult`. The generated table drifts across
            // this block in the current workspace, which can resolve real but incorrect
            // MapleEvent / MonsterKilling resources. Pin the exact Massacre ids here so the
            // HUD, timerboard, key animation, gauge, and result-owner surfaces stay on the
            // client-backed resource paths.
            [0x14EC] = "killing/clear",
            // Recovered from MapleStory.exe v95 `CWvsContext::OnSessionValue`. The cooldown
            // animation-displayer owner branch compares this key before dispatching
            // `CAnimationDisplayer::Effect_Cool`.
            [0x14F1] = "massacre_cool",
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
            // Recovered from MapleStory.exe v95 `CStageSystem::IterateStageSystemClient`.
            // The generated table in this workspace drifts across the stage-system block,
            // so pin the client-owned WZ paths here before context-owned stage-period
            // validation resolves the catalog inputs through StringPool.
            [0x17BE] = "Etc/StageAffectedMap.img",
            [0x17BF] = "Etc/StageSystem.img",
            [0x17C0] = "Etc/StageKeyword.img",
            // Recovered from MapleStory.exe v95 `LoadStageBackImgInfo`
            // and `CMapLoadable::MakeBack`. The stage-period catalog uses
            // these client-owned property-name ids when parsing native
            // StageBackImg objects and the authored background fallback.
            [0x03E5] = "x",
            [0x03E6] = "y",
            [0x0610] = "bS",
            [0x0612] = "rx",
            [0x0613] = "ry",
            [0x0614] = "cx",
            [0x0615] = "cy",
            [0x0617] = "a",
            [0x0618] = "type",
            [0x17F1] = "absRX",
            [0x17F2] = "absRY",
            // Recovered from MapleStory.exe v95 `CUIQuestAlarm::Draw`,
            // `CUIQuestAlarm::OnButtonClicked`, and `CUIQuestAlarm::OnMouseMove`.
            // Keep these quest-alarm ids explicit so the
            // owner retains the exact client title and notice strings even if regenerated
            // string-pool order drifts again.
            [0x0E4C] = "Quest Helper (%d/5)",
            [0x106F] = "[%s] It has been excluded from the auto alarm and it will not be automatically reigstered until you re log-on",
            [0x107A] = "Auto Alarm on",
            [0x107B] = "Auto Alarm off",
            [0x107C] = "When you click it%2C quests in progress will register automatically and if it is not in progress for 10 minutes%2C it will disappear.",
            [0x107D] = "When you click it%2C the quest will not register automatically even when the quest is in progress.",
            [0x18A8] = "This quest has recent progress updates.",
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

        public static string ResolveMobAngerGaugeBurstPath(string mobTemplateId)
        {
            if (!int.TryParse(mobTemplateId, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedTemplateId))
            {
                return null;
            }

            return ResolveMobAngerGaugeBurstPath(parsedTemplateId);
        }

        public static string ResolveMobAngerGaugeBurstPath(int mobTemplateId)
        {
            string templatePath = GetCompositeFormatOrFallback(
                MobAngerGaugeBurstTemplatePathStringPoolId,
                MobAngerGaugeBurstTemplatePathFallback,
                maxPlaceholderCount: 1,
                out _);
            string effectName = GetOrFallback(
                MobAngerGaugeBurstEffectNameStringPoolId,
                MobAngerGaugeBurstEffectNameFallback,
                appendFallbackSuffix: false);

            if (string.IsNullOrWhiteSpace(templatePath) || string.IsNullOrWhiteSpace(effectName))
            {
                return null;
            }

            return string.Format(CultureInfo.InvariantCulture, templatePath, mobTemplateId)
                + "/"
                + effectName.Trim().Trim('/');
        }

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

        public static string GetOrNull(int stringPoolId)
        {
            return TryGet(stringPoolId, out string resolvedText)
                ? resolvedText
                : null;
        }

        public static string ResolveCashGachaponImageName(bool isCopyResult)
        {
            const int cashGachaponImageStringPoolId = 0x13D4;
            const int cashGachaponCopyImageStringPoolId = 0x13D5;

            return GetOrFallback(
                isCopyResult ? cashGachaponCopyImageStringPoolId : cashGachaponImageStringPoolId,
                isCopyResult ? "CashGachaponCopy.img" : "CashGachapon.img");
        }

        public static string ResolveCashGachaponWindowPropertyPath(bool isCopyResult)
        {
            string imageName = ResolveCashGachaponImageName(isCopyResult);
            if (string.Equals(imageName, "CashGachaponCopy.img", StringComparison.OrdinalIgnoreCase))
            {
                return "CashGachapon1";
            }

            return "CashGachapon";
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
                if (!TryFindNextPrintfPlaceholder(format, searchStart, out int markerIndex, out int markerLength, out string numericFormat))
                {
                    break;
                }

                string replacement = string.IsNullOrEmpty(numericFormat)
                    ? $"{{{tokenIndex}}}"
                    : $"{{{tokenIndex}:{numericFormat}}}";
                format = format.Remove(markerIndex, markerLength).Insert(markerIndex, replacement);
                searchStart = markerIndex + replacement.Length;
                tokenIndex++;
            }

            return format;
        }

        private static bool TryFindNextPrintfPlaceholder(
            string format,
            int searchStart,
            out int markerIndex,
            out int markerLength,
            out string numericFormat)
        {
            markerIndex = -1;
            markerLength = 0;
            numericFormat = null;

            for (int i = Math.Max(0, searchStart); i < format.Length - 1; i++)
            {
                if (format[i] != '%')
                {
                    continue;
                }

                int cursor = i + 1;
                if (format[cursor] == '%')
                {
                    i = cursor;
                    continue;
                }

                bool zeroPad = false;
                if (format[cursor] == '0')
                {
                    zeroPad = true;
                    cursor++;
                }

                int width = 0;
                bool hasWidth = false;
                while (cursor < format.Length && char.IsDigit(format[cursor]))
                {
                    hasWidth = true;
                    width = (width * 10) + (format[cursor] - '0');
                    cursor++;
                }

                if (cursor >= format.Length)
                {
                    continue;
                }

                char specifier = format[cursor];
                if (specifier != 'd' && specifier != 's')
                {
                    continue;
                }

                markerIndex = i;
                markerLength = (cursor - i) + 1;
                if (specifier == 'd' && zeroPad && hasWidth && width > 0)
                {
                    numericFormat = $"D{width}";
                }

                return true;
            }

            return false;
        }
    }
}
