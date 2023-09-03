/*  MapleLib - A general-purpose MapleStory library
 * Copyright (C) 
 * 2009, 2010, 2015 Snow and haha01haha01
 * 2020 lastbattle
   
 * This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

 * This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

 * You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.WzLib.WzStructure;
using System.Drawing;
using MapleLib.Helpers;
using MapleLib.WzLib.WzStructure.Data.MapStructure;

namespace MapleLib.WzLib.WzStructure
{
    public class MapInfo //Credits to Bui for some of the info
    {
        public static MapInfo Default = new MapInfo();

        private WzImage image = null;

        //Editor related, not actual properties
        public MapType mapType = MapType.RegularMap;


        #region Wz related Properties
        //Cannot change
        public int version = 10;

        // Other properties that is not supported by HaCreator yet, but still gets dumped back when saving
        public readonly List<WzImageProperty> unsupportedInfoProperties = new List<WzImageProperty>();

        //Must have
        public string bgm = "Bgm00/GoPicnic";
        public string mapMark = "None";
        public long fieldLimit = 0; // FieldLimitType a | FieldLimitType b | etc
        public int returnMap = 999999999;
        public int forcedReturn = 999999999;
        public bool cloud = false;
        public bool swim = false;
        public bool hideMinimap = false;
        public bool town = false;
        public float mobRate = 1.5f;

        //Optional
        //public int link = -1;
        public int? VRTop = null, VRBottom = null, VRLeft = null, VRRight = null;
        public int? timeLimit = null;
        public int? lvLimit = null;
        public FieldType? fieldType = null;
        public string onFirstUserEnter = null;
        public string onUserEnter = null;
        public MapleBool fly = null;
        public MapleBool noMapCmd = null;
        public MapleBool partyOnly = null;
        public MapleBool reactorShuffle = null;
        public string reactorShuffleName = null;
        public MapleBool personalShop = null;
        public MapleBool entrustedShop = null;
        public string effect = null; //Bubbling; 610030550 and many others
        public int? lvForceMove = null; //limit FROM value
        public TimeMob? timeMob = null;
        public string help = null; //help string
        public MapleBool snow = null;
        public MapleBool rain = null;
        public int? dropExpire = null; //in seconds
        public int? decHP = null;
        public int? decInterval = null;
        public AutoLieDetector autoLieDetector = null;
        public MapleBool expeditionOnly = null;
        public float? fs = null; //slip on ice speed, default 0.2
        public List<int> protectItem = null; //ID, item protecting from cold
        public int? createMobInterval = null; //used for massacre pqs
        public int? fixedMobCapacity = null; //mob capacity to target (used for massacre pqs)
        public MapleBool mirror_Bottom = null; // Mirror Bottom (Reflection for objects near VRBottom of the field, arcane river maps)

        //Unknown optional
        public int? moveLimit = null;
        public string mapDesc = null;
        public string mapName = null;
        public string streetName = null;
        public MapleBool miniMapOnOff = null;
        public MapleBool noRegenMap = null; //610030400
        public List<int> allowedItem = null;
        public float? recovery = null; //recovery rate, like in sauna (3)
        public MapleBool blockPBossChange = null; //something with monster carnival
        public MapleBool everlast = null; //something with bonus stages of PQs
        public MapleBool damageCheckFree = null; //something with fishing event
        public float? dropRate = null;
        public MapleBool scrollDisable = null;
        public MapleBool needSkillForFly = null;
        public MapleBool zakum2Hack = null; //JQ hack protection
        public MapleBool allMoveCheck = null; //another JQ hack protection
        public MapleBool VRLimit = null; //use vr's as limits?
        public MapleBool consumeItemCoolTime = null; //cool time of consume item
        public MapleBool zeroSideOnly = null; // true if its zero's temple map

        //Special
        public List<WzImageProperty> additionalProps = new List<WzImageProperty>();
        public List<WzImageProperty> additionalNonInfoProps = new List<WzImageProperty>();
        public string strMapName = "<Untitled>";
        public string strStreetName = "<Untitled>";
        public string strCategoryName = "HaCreator";
        public int id = 0;
        #endregion



        /// <summary>
        /// Empty Constructor
        /// </summary>
        public MapInfo()
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="image"></param>
        /// <param name="strMapName"></param>
        /// <param name="strStreetName"></param>
        /// <param name="strCategoryName"></param>
        public MapInfo(WzImage image, string strMapName, string strStreetName, string strCategoryName)
        {
            this.image = image;
            int? startHour;
            int? endHour;
            this.strMapName = strMapName;
            this.strStreetName = strStreetName;
            this.strCategoryName = strCategoryName;
            WzFile file = (WzFile)image.WzFileParent;
            string loggerSuffix = ", map " + image.Name + ((file != null) ? (" of version " + Enum.GetName(typeof(WzMapleVersion), file.MapleVersion) + ", v" + file.Version.ToString()) : "");

            foreach (WzImageProperty prop in image["info"].WzProperties)
            {
                switch (prop.Name)
                {
                    case "bgm":
                        bgm = InfoTool.GetString(prop);
                        break;
                    case "cloud":
                        cloud = InfoTool.GetBool(prop);
                        break;
                    case "swim":
                        swim = InfoTool.GetBool(prop);
                        break;
                    case "forcedReturn":
                        forcedReturn = InfoTool.GetInt(prop);
                        break;
                    case "hideMinimap":
                        hideMinimap = InfoTool.GetBool(prop);
                        break;
                    case "mapDesc":
                        mapDesc = InfoTool.GetString(prop);
                        break;
                    case "mapName":
                        mapName = InfoTool.GetString(prop);
                        break;
                    case "mapMark":
                        mapMark = InfoTool.GetString(prop);
                        break;
                    case "mobRate":
                        mobRate = InfoTool.GetFloat(prop);
                        break;
                    case "moveLimit":
                        moveLimit = InfoTool.GetInt(prop);
                        break;
                    case "returnMap":
                        returnMap = InfoTool.GetInt(prop);
                        break;
                    case "town":
                        town = InfoTool.GetBool(prop);
                        break;
                    case "version":
                        version = InfoTool.GetInt(prop);
                        break;
                    case "fieldLimit":
                        long fl = InfoTool.GetLong(prop);
                        fieldLimit = fl;
                        break;
                    case "VRTop":
                        VRTop = InfoTool.GetOptionalInt(prop, 0);
                        break;
                    case "VRBottom":
                        VRBottom = InfoTool.GetOptionalInt(prop, 0);
                        break;
                    case "VRLeft":
                        VRLeft = InfoTool.GetOptionalInt(prop, 0);
                        break;
                    case "VRRight":
                        VRRight = InfoTool.GetOptionalInt(prop, 0);
                        break;
                    case "link":
                        //link = InfoTool.GetInt(prop);
                        break;
                    case "timeLimit":
                        timeLimit = InfoTool.GetInt(prop);
                        break;
                    case "lvLimit":
                        lvLimit = InfoTool.GetInt(prop);
                        break;
                    case "onFirstUserEnter":
                        onFirstUserEnter = InfoTool.GetString(prop);
                        break;
                    case "onUserEnter":
                        onUserEnter = InfoTool.GetString(prop);
                        break;
                    case "fly":
                        fly = InfoTool.GetBool(prop);
                        break;
                    case "noMapCmd":
                        noMapCmd = InfoTool.GetBool(prop);
                        break;
                    case "partyOnly":
                        partyOnly = InfoTool.GetBool(prop);
                        break;
                    case "fieldType":
                        int ft = InfoTool.GetInt(prop);
                        if (!Enum.IsDefined(typeof(FieldType), ft))
                        {
                            ErrorLogger.Log(ErrorLevel.IncorrectStructure, "Invalid fieldType " + ft.ToString() + loggerSuffix);
                            ft = 0;
                        }
                        fieldType = (FieldType)ft;
                        break;
                    case "miniMapOnOff":
                        miniMapOnOff = InfoTool.GetBool(prop);
                        break;
                    case "reactorShuffle":
                        reactorShuffle = InfoTool.GetBool(prop);
                        break;
                    case "reactorShuffleName":
                        reactorShuffleName = InfoTool.GetString(prop);
                        break;
                    case "personalShop":
                        personalShop = InfoTool.GetBool(prop);
                        break;
                    case "entrustedShop":
                        entrustedShop = InfoTool.GetBool(prop);
                        break;
                    case "effect":
                        effect = InfoTool.GetString(prop);
                        break;
                    case "lvForceMove":
                        lvForceMove = InfoTool.GetInt(prop);
                        break;
                    case "timeMob":
                        startHour = InfoTool.GetOptionalInt(prop["startHour"]);
                        endHour = InfoTool.GetOptionalInt(prop["endHour"]);
                        int? id = InfoTool.GetOptionalInt(prop["id"]);
                        string message = InfoTool.GetOptionalString(prop["message"]);
                        if (id == null || message == null || (startHour == null ^ endHour == null))
                        {
                            ErrorLogger.Log(ErrorLevel.IncorrectStructure, "timeMob" + loggerSuffix);
                        }
                        else
                            timeMob = new TimeMob((int?)startHour, (int?)endHour, (int)id, message);
                        break;
                    case "help":
                        help = InfoTool.GetString(prop);
                        break;
                    case "snow":
                        snow = InfoTool.GetBool(prop);
                        break;
                    case "rain":
                        rain = InfoTool.GetBool(prop);
                        break;
                    case "dropExpire":
                        dropExpire = InfoTool.GetInt(prop);
                        break;
                    case "decHP":
                        decHP = InfoTool.GetInt(prop);
                        break;
                    case "decInterval":
                        decInterval = InfoTool.GetInt(prop);
                        break;
                    case "autoLieDetector":
                        startHour = InfoTool.GetOptionalInt(prop["startHour"]);
                        endHour = InfoTool.GetOptionalInt(prop["endHour"]);
                        int? interval = InfoTool.GetOptionalInt(prop["interval"]);
                        int? propInt = InfoTool.GetOptionalInt(prop["prop"]);
                        if (startHour == null || endHour == null || interval == null || propInt == null)
                        {
                            ErrorLogger.Log(ErrorLevel.IncorrectStructure, "autoLieDetector" + loggerSuffix);
                        }
                        else
                            autoLieDetector = new AutoLieDetector((int)startHour, (int)endHour, (int)interval, (int)propInt);
                        break;
                    case "expeditionOnly":
                        expeditionOnly = InfoTool.GetBool(prop);
                        break;
                    case "fs":
                        fs = InfoTool.GetFloat(prop);
                        break;
                    case "protectItem":
                        if (prop is WzSubProperty) { // if its a WzSubProperty, then its a list of ints.  "Map002.wz\\Map\\Map2\\211000200.img\\info\\protectItem\\0"
                            WzSubProperty subProp = prop as WzSubProperty;
                            protectItem = new List<int>();
                            if (subProp.WzProperties != null && subProp.WzProperties.Count > 0) {
                                foreach (WzImageProperty item in subProp.WzProperties) {
                                    protectItem.Add(item.GetInt());
                                }
                            }
                        }
                        else {
                            protectItem = new List<int> {
                                InfoTool.GetInt(prop) // older versions uses only an int
                            };
                        }
                        break;
                    case "createMobInterval":
                        createMobInterval = InfoTool.GetInt(prop);
                        break;
                    case "fixedMobCapacity":
                        fixedMobCapacity = InfoTool.GetInt(prop);
                        break;
                    case "streetName":
                        streetName = InfoTool.GetString(prop);
                        break;
                    case "noRegenMap":
                        noRegenMap = InfoTool.GetBool(prop);
                        break;
                    case "allowedItem":
                        allowedItem = new List<int>();
                        if (prop.WzProperties != null && prop.WzProperties.Count > 0)
                            foreach (WzImageProperty item in prop.WzProperties)
                                allowedItem.Add(item.GetInt());
                        break;
                    case "recovery":
                        recovery = InfoTool.GetFloat(prop);
                        break;
                    case "blockPBossChange":
                        blockPBossChange = InfoTool.GetBool(prop);
                        break;
                    case "everlast":
                        everlast = InfoTool.GetBool(prop);
                        break;
                    case "damageCheckFree":
                        damageCheckFree = InfoTool.GetBool(prop);
                        break;
                    case "dropRate":
                        dropRate = InfoTool.GetFloat(prop);
                        break;
                    case "scrollDisable":
                        scrollDisable = InfoTool.GetBool(prop);
                        break;
                    case "needSkillForFly":
                        needSkillForFly = InfoTool.GetBool(prop);
                        break;
                    case "zakum2Hack":
                        zakum2Hack = InfoTool.GetBool(prop);
                        break;
                    case "allMoveCheck":
                        allMoveCheck = InfoTool.GetBool(prop);
                        break;
                    case "VRLimit":
                        VRLimit = InfoTool.GetBool(prop);
                        break;
                    case "consumeItemCoolTime":
                        consumeItemCoolTime = InfoTool.GetBool(prop);
                        break;
                    case "zeroSideOnly":
                        zeroSideOnly = InfoTool.GetBool(prop);
                        break;
                    case "mirror_Bottom":
                        mirror_Bottom = InfoTool.GetBool(prop);
                        break;
                    case "AmbientBGM":
                    case "AmbientBGMv":
                    case "areaCtrl":
                    case "onlyUseSkill":
                    case "limitUseShop":
                    case "limitUseTrunk":
                    case "freeFallingVX":
                    case "midAirAccelVX":
                    case "midAirDecelVX":
                    case "jumpSpeedR":
                    case "jumpAccUpKey":
                    case "jumpAccDownKey":
                    case "jumpApplyVX":
                    case "dashSkill":
                    case "speedMaxOver":
                    case "speedMaxOver ": // with a space, stupid nexon
                    case "isSpecialMoveCheck":
                    case "forceSpeed":
                    case "forceJump":
                    case "forceUseIndie":
                    case "vanishPet":
                    case "vanishAndroid":
                    case "vanishDragon":
                    case "limitUI":
                    case "largeSplit":
                    case "qrLimit":
                    case "noChair":
                    case "fieldScript":
                    case "standAlone":
                    case "partyStandAlone":
                    case "HobbangKing":
                    case "quarterView":
                    case "MRLeft":
                    case "MRTop":
                    case "MRRight":
                    case "MRBottom":
                    case "limitSpeedAndJump":
                    case "directionInfo":
                    case "LBSide":
                    case "LBTop":
                    case "LBBottom":
                    case "bgmSub":
                    case "fieldLimit2":
                    case "fieldLimit_tw":
                    case "particle":
                    case "qrLimitJob":
                    case "individualMobPool":
                    case "barrier":
                    case "remoteEffect":
                    case "isFixDec":
                    case "decHPr":
                    case "isDecView":
                    case "forceFadeInTime":
                    case "forceFadeOutTime":
                    case "qrLimitState":
                    case "qrLimitState2":
                    case "difficulty":
                    case "alarm":
                    case "bossMobID":
                    case "reviveCurField":
                    case "ReviveCurFieldOfNoTransfer":
                    case "ReviveCurFieldOfNoTransferPoint":
                    case "limitUserEffectField":
                    case "nofollowCharacter":
                    case "functionObjInfo":
                    case "quest":
                    case "ridingMove":
                    case "ridingField":
                    case "noLanding":
                    case "noCancelSkill":
                    case "defaultAvatarDir":
                    case "footStepSound":
                    case "taggedObjRegenInfo":
                    case "offSoulAbsorption":
                    case "canPartyStatChangeIgnoreParty":
                    case "canPartyStatChangeIgnoreParty ": // with a space
                    case "forceReturnOnDead":
                    case "zoomOutField":
                    case "EscortMinTime":
                    case "deathCount":
                    case "onEnterResetFifthSkill":
                    case "potionLimit":
                    case "lifeCount":
                    case "respawnCooltimeField":
                    case "scale":
                    case "skills":
                    case "noResurection":
                    case "incInterval":
                    case "incMPr":
                    case "bonusExpPerUserHPRate":
                    case "barrierArc":
                    case "noBackOverlapped":
                    case "partyBonusR":
                    case "specialSound":
                    case "inFieldsetForcedReturn":
                    case "chaser":
                    case "chaserEndTime":
                    case "chaserEffect":
                    case "cagePotal":
                    case "cageLT":
                    case "cageRB":
                    case "chaserHoldTime":
                    case "phase":
                    case "boss":
                    case "actionBarIdx":
                    case "shadowzone":
                    case "towerChairEnable":
                    case "DiceMasterUpIndex":
                    case "DiceMasterDownIndex":
                    case "ChangeName":
                    case "entrustedFishing":
                    case "forcedScreenMode":
                    case "isHideUI":
                    case "resetRidingField":
                    case "PL_Gunman":
                    case "noHekatonEffect":
                    case "mode":
                    case "skill":
                    case "MR":
                    case "ratemob":
                    case "bonusStageNoChangeBack":
                    case "questAmbience":
                    case "blockScriptItem":
                    case "remoteTranslucenceView":
                    case "rewardContentName":
                    case "specialDeadAction":
                    case "FriendsStoryBossDelay":
                    case "soulFieldType":
                    case "cameraMoveY":
                    case "subType":
                    case "playTime":
                    case "noEvent":
                    case "forParty":
                    case "whiteOut":
                    case "UserInvisible":
                    case "OnFirstUserEnter": // the same as onFirstUserEnter
                    case "teamPortal":
                    case "typingGame":
                    case "lt":
                    case "rb":
                    case "fieldAttackObj":
                    case "timeLeft":
                    case "timeLeft_d":
                    case "eventSummon":
                    case "rankCheckMob":
                    case "questCheckMob":
                    case "bindSkillLimit":
                    case "NoMobCapacityLimit":
                    case "userTimer":
                    case "eventChairIndex":
                    case "onlyEventChair":
                    case "waitReviveTime":
                    case "RidingTop":
                    case "temporarySkill":
                    case "largeSplt":
                    case "blockTakeOffItem":
                    case "PartyOnly":
                    case "climb":
                    case "bulletConsume":
                    case "gaugeDelay":
                    case "individualPet":
                    case "level":
                    case "hungryMuto":
                    case "property": //  map 921172300.img
                    case "spiritSavior":
                    case "standAlonePermitUpgrade": //  993059600.img
                    case "limitHeadAlarmField": // 993180000.img
                    case "noPsychicGrab": // 993194700.img
                    case "offSoulEffect": // 993194700.img
                    case "offActiveEffectItem": // 993194700.img
                    case "forceDamageSkinID": // 993194700.img
                    case "minimapFullSizeViewBlock": // 993194700.img
                    case "limitUIContextMenu": // 993210400.img  993220101.img
                    case "vanishHaku": // 993194700.img
                    case "pulbicTaggedObjectVisible": // 993210000.img 
                    case "housingGrid": // v225
                    case "noUseStardustField": // v225
                    case "shiftChannelForbidden": // v225
                    case "barrierAut": // v237
                        {
                            WzImageProperty cloneProperty = prop.DeepClone();
                            //cloneProperty.Parent = prop.Parent;

                            unsupportedInfoProperties.Add(cloneProperty);
                            break;
                        }
                    default:
                        ErrorLogger.Log(ErrorLevel.MissingFeature, string.Format("[MapInfo] Unknown field info/ property: '{0}'. {1}. Please fix it at MapInfo.cs", prop.Name, loggerSuffix));
                        additionalProps.Add(prop.DeepClone());
                        break;
                }
            }
        }

        public static Rectangle? GetVR(WzImage image)
        {
            Rectangle? result = null;
            if (image["info"]["VRLeft"] != null)
            {
                WzImageProperty info = image["info"];
                int left = InfoTool.GetInt(info["VRLeft"]);
                int right = InfoTool.GetInt(info["VRRight"]);
                int top = InfoTool.GetInt(info["VRTop"]);
                int bottom = InfoTool.GetInt(info["VRBottom"]);
                result = new Rectangle(left, top, right - left, bottom - top);
            }
            return result;
        }

        /// <summary>
        /// Save MapInfo variables to WzImage
        /// </summary>
        /// <param name="dest"></param>
        /// <param name="VR"></param>
        public void Save(WzImage dest, Rectangle? VR)
        {
            WzSubProperty info = new WzSubProperty();
            info["bgm"] = InfoTool.SetString(bgm);
            info["cloud"] = InfoTool.SetBool(cloud);
            info["swim"] = InfoTool.SetBool(swim);
            info["forcedReturn"] = InfoTool.SetInt(forcedReturn);
            info["hideMinimap"] = InfoTool.SetBool(hideMinimap);
            info["mapDesc"] = InfoTool.SetOptionalString(mapDesc);
            info["mapName"] = InfoTool.SetOptionalString(mapDesc);
            info["mapMark"] = InfoTool.SetString(mapMark);
            info["mobRate"] = InfoTool.SetFloat(mobRate);
            info["moveLimit"] = InfoTool.SetOptionalInt(moveLimit);
            info["returnMap"] = InfoTool.SetInt(returnMap);
            info["town"] = InfoTool.SetBool(town);
            info["version"] = InfoTool.SetInt(version);
            info["fieldLimit"] = InfoTool.SetInt((int)fieldLimit);
            info["timeLimit"] = InfoTool.SetOptionalInt(timeLimit);
            info["lvLimit"] = InfoTool.SetOptionalInt(lvLimit);

            info["VRTop"] = InfoTool.SetOptionalInt(VRTop);
            info["VRBottom"] = InfoTool.SetOptionalInt(VRBottom);
            info["VRLeft"] = InfoTool.SetOptionalInt(VRLeft);
            info["VRRight"] = InfoTool.SetOptionalInt(VRRight);

            info["onFirstUserEnter"] = InfoTool.SetOptionalString(onFirstUserEnter);
            info["onUserEnter"] = InfoTool.SetOptionalString(onUserEnter);
            info["fly"] = InfoTool.SetOptionalBool(fly);
            info["noMapCmd"] = InfoTool.SetOptionalBool(noMapCmd);
            info["partyOnly"] = InfoTool.SetOptionalBool(partyOnly);
            info["fieldType"] = InfoTool.SetOptionalInt((int?)fieldType);
            info["miniMapOnOff"] = InfoTool.SetOptionalBool(miniMapOnOff);
            info["reactorShuffle"] = InfoTool.SetOptionalBool(reactorShuffle);
            info["reactorShuffleName"] = InfoTool.SetOptionalString(reactorShuffleName);
            info["personalShop"] = InfoTool.SetOptionalBool(personalShop);
            info["entrustedShop"] = InfoTool.SetOptionalBool(entrustedShop);
            info["effect"] = InfoTool.SetOptionalString(effect);
            info["lvForceMove"] = InfoTool.SetOptionalInt(lvForceMove);
            info["mirror_Bottom"] = InfoTool.SetOptionalBool(mirror_Bottom);

            // Time mob
            if (timeMob != null)
            {
                WzSubProperty prop = new WzSubProperty();
                prop["startHour"] = InfoTool.SetOptionalInt(timeMob.Value.startHour);
                prop["endHour"] = InfoTool.SetOptionalInt(timeMob.Value.endHour);
                prop["id"] = InfoTool.SetOptionalInt(timeMob.Value.id);
                prop["message"] = InfoTool.SetOptionalString(timeMob.Value.message);
                info["timeMob"] = prop;
            }
            info["help"] = InfoTool.SetOptionalString(help);
            info["snow"] = InfoTool.SetOptionalBool(snow);
            info["rain"] = InfoTool.SetOptionalBool(rain);
            info["dropExpire"] = InfoTool.SetOptionalInt(dropExpire);
            info["decHP"] = InfoTool.SetOptionalInt(decHP);
            info["decInterval"] = InfoTool.SetOptionalInt(decInterval);

            // Lie detector
            if (autoLieDetector != null)
            {
                WzSubProperty prop = new WzSubProperty();
                prop["startHour"] = InfoTool.SetOptionalInt(autoLieDetector.startHour);
                prop["endHour"] = InfoTool.SetOptionalInt(autoLieDetector.endHour);
                prop["interval"] = InfoTool.SetOptionalInt(autoLieDetector.interval);
                prop["prop"] = InfoTool.SetOptionalInt(autoLieDetector.prop);
                info["autoLieDetector"] = prop;
            }
            info["expeditionOnly"] = InfoTool.SetOptionalBool(expeditionOnly);
            info["fs"] = InfoTool.SetOptionalFloat(fs);

            // Protect Item
            if (protectItem != null && protectItem.Count > 0) {
                if (protectItem.Count == 1) { // older versions uses only an int
                    info["protectItem"] = InfoTool.SetOptionalInt(protectItem[0]);
                } else {  // if its a WzSubProperty, then its a list of ints.  "Map002.wz\\Map\\Map2\\211000200.img\\info\\protectItem\\0"
                    WzSubProperty subProp = new WzSubProperty();
                    int i = 0;
                    foreach (var item in protectItem) {
                        WzIntProperty wzIntProperty = new WzIntProperty(i.ToString(), item);
                        subProp.AddProperty(wzIntProperty);
                        i++;
                    }
                    info["protectItem"] = subProp;
                }
            }
            info["createMobInterval"] = InfoTool.SetOptionalInt(createMobInterval);
            info["fixedMobCapacity"] = InfoTool.SetOptionalInt(fixedMobCapacity);
            info["streetName"] = InfoTool.SetOptionalString(streetName);
            info["noRegenMap"] = InfoTool.SetOptionalBool(noRegenMap);
            if (allowedItem != null)
            {
                WzSubProperty prop = new WzSubProperty();
                for (int i = 0; i < allowedItem.Count; i++)
                {
                    prop[i.ToString()] = InfoTool.SetInt(allowedItem[i]);
                }
                info["allowedItem"] = prop;
            }
            info["recovery"] = InfoTool.SetOptionalFloat(recovery);
            info["blockPBossChange"] = InfoTool.SetOptionalBool(blockPBossChange);
            info["everlast"] = InfoTool.SetOptionalBool(everlast);
            info["damageCheckFree"] = InfoTool.SetOptionalBool(damageCheckFree);
            info["dropRate"] = InfoTool.SetOptionalFloat(dropRate);
            info["scrollDisable"] = InfoTool.SetOptionalBool(scrollDisable);
            info["needSkillForFly"] = InfoTool.SetOptionalBool(needSkillForFly);
            info["zakum2Hack"] = InfoTool.SetOptionalBool(zakum2Hack);
            info["allMoveCheck"] = InfoTool.SetOptionalBool(allMoveCheck);
            info["VRLimit"] = InfoTool.SetOptionalBool(VRLimit);
            info["consumeItemCoolTime"] = InfoTool.SetOptionalBool(consumeItemCoolTime);
            info["zeroSideOnly"] = InfoTool.SetOptionalBool(zeroSideOnly);
            foreach (WzImageProperty prop in additionalProps)
            {
                info.AddProperty(prop);
            }
            if (VR.HasValue)
            {
                info["VRLeft"] = InfoTool.SetInt(VR.Value.Left);
                info["VRRight"] = InfoTool.SetInt(VR.Value.Right);
                info["VRTop"] = InfoTool.SetInt(VR.Value.Top);
                info["VRBottom"] = InfoTool.SetInt(VR.Value.Bottom);
            }

            // Add back all unsupported properties
            info.AddProperties(unsupportedInfoProperties);

            //
            dest["info"] = info;
        }

        public WzImage Image
        {
            get { return image; }
            set { image = value; }
        }

        public bool ShouldSerializeImage()
        {
            // To keep JSON.NET from serializing this
            return false;
        }
    }
}