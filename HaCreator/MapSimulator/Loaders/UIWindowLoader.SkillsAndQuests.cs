using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.UI.Controls;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using HaSharedLibrary.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;


namespace HaCreator.MapSimulator.Loaders
{
    public static partial class UIWindowLoader
    {
        #region Skill Window (Big Bang)
        /// <summary>
        /// Create the Skill window - selects pre-BB or post-BB version based on isBigBang
        /// </summary>
        public static UIWindowBase CreateSkillWindowUnified(
            WzImage uiWindow1Image, WzImage uiWindow2Image, WzImage basicImage, WzImage soundUIImage,
            GraphicsDevice device, int screenWidth, int screenHeight, bool isBigBang)
        {
            if (isBigBang && uiWindow2Image != null)
            {
                return CreateSkillWindowBigBang(uiWindow2Image, uiWindow1Image, basicImage, soundUIImage, device, screenWidth, screenHeight);
            }
            return CreateSkillWindow(uiWindow1Image, soundUIImage, device, screenWidth, screenHeight);
        }


        /// <summary>
        /// Create the Skill window from UI.wz/UIWindow2.img/Skill/main (Post-Big Bang)
        /// </summary>
        public static SkillUIBigBang CreateSkillWindowBigBang(
            WzImage uiWindow2Image, WzImage uiWindow1Image, WzImage basicImage, WzImage soundUIImage,
            GraphicsDevice device, int screenWidth, int screenHeight)
        {
            WzSubProperty skillProperty = (WzSubProperty)uiWindow2Image?["Skill"];
            WzSubProperty mainProperty = (WzSubProperty)skillProperty?["main"];
            if (mainProperty == null)
            {
                return CreatePlaceholderSkillBigBang(device, screenWidth, screenHeight);
            }


            // Get main background
            WzCanvasProperty backgrnd = (WzCanvasProperty)mainProperty["backgrnd"];
            if (backgrnd == null)
            {
                return CreatePlaceholderSkillBigBang(device, screenWidth, screenHeight);
            }


            System.Drawing.Bitmap bgBitmap = backgrnd.GetLinkedWzCanvasBitmap();

            Texture2D bgTexture = bgBitmap.ToTexture2DAndDispose(device);

            IDXObject frame = new DXObject(0, 0, bgTexture, 0);



            SkillUIBigBang skill = new SkillUIBigBang(frame, device);

            skill.Position = new Point(50, 100);



            // Load foreground (backgrnd2 - labels/overlay)
            WzCanvasProperty backgrnd2 = (WzCanvasProperty)mainProperty["backgrnd2"];
            if (backgrnd2 != null)
            {
                try
                {
                    System.Drawing.Bitmap fgBitmap = backgrnd2.GetLinkedWzCanvasBitmap();
                    Texture2D fgTexture = fgBitmap.ToTexture2DAndDispose(device);
                    IDXObject foreground = new DXObject(0, 0, fgTexture, 0);
                    System.Drawing.PointF? origin = backgrnd2.GetCanvasOriginPosition();
                    int offsetX = origin.HasValue ? -(int)origin.Value.X : 6;
                    int offsetY = origin.HasValue ? -(int)origin.Value.Y : 22;
                    skill.SetForeground(foreground, offsetX, offsetY);
                }
                catch { }
            }


            // Load skill list background (backgrnd3)
            WzCanvasProperty backgrnd3 = (WzCanvasProperty)mainProperty["backgrnd3"];
            if (backgrnd3 != null)
            {
                try
                {
                    System.Drawing.Bitmap bg3Bitmap = backgrnd3.GetLinkedWzCanvasBitmap();
                    Texture2D bg3Texture = bg3Bitmap.ToTexture2DAndDispose(device);
                    IDXObject skillListBg = new DXObject(0, 0, bg3Texture, 0);
                    System.Drawing.PointF? origin = backgrnd3.GetCanvasOriginPosition();
                    int offsetX = origin.HasValue ? -(int)origin.Value.X : 7;
                    int offsetY = origin.HasValue ? -(int)origin.Value.Y : 47;
                    skill.SetSkillListBackground(skillListBg, offsetX, offsetY);
                }
                catch { }
            }


            // Load skill row textures (skill0, skill1 - alternating row backgrounds)
            Texture2D skillRow0 = LoadCanvasTexture(mainProperty, "skill0", device);
            Texture2D skillRow1 = LoadCanvasTexture(mainProperty, "skill1", device);
            Texture2D recommendTexture = LoadCanvasTexture(mainProperty["recommend"] as WzSubProperty, "0", device);
            Texture2D skillLine = LoadCanvasTexture(mainProperty, "line", device);
            skill.SetSkillRowTextures(skillRow0, skillRow1, skillLine);
            skill.SetRecommendTexture(recommendTexture);
            System.Diagnostics.Debug.WriteLine($"[UIWindowLoader] Skill row textures: row0={skillRow0 != null}, row1={skillRow1 != null}, line={skillLine != null}");


            // Load tab textures
            WzSubProperty tabProperty = (WzSubProperty)mainProperty["Tab"];
            if (tabProperty != null)
            {
                Texture2D[] tabEnabled = new Texture2D[5];
                Texture2D[] tabDisabled = new Texture2D[5];
                Rectangle[] tabEnabledRects = new Rectangle[5];
                Rectangle[] tabDisabledRects = new Rectangle[5];


                WzSubProperty enabledProperty = (WzSubProperty)tabProperty["enabled"];

                WzSubProperty disabledProperty = (WzSubProperty)tabProperty["disabled"];



                for (int i = 0; i < 5; i++)
                {
                    string tabIndex = i.ToString();
                    tabEnabledRects[i] = new Rectangle(10 + (i * 31), 27, 30, 20);
                    tabDisabledRects[i] = new Rectangle(10 + (i * 31), 29, 30, 18);


                    if (enabledProperty != null)
                    {
                        tabEnabled[i] = LoadCanvasTexture(enabledProperty, tabIndex, device);
                        tabEnabledRects[i] = ResolveCanvasBounds(
                            enabledProperty[tabIndex] as WzCanvasProperty,
                            tabEnabled[i],
                            10 + (i * 31),
                            27,
                            30,
                            20);
                    }
                    if (disabledProperty != null)
                    {
                        tabDisabled[i] = LoadCanvasTexture(disabledProperty, tabIndex, device);
                        tabDisabledRects[i] = ResolveCanvasBounds(
                            disabledProperty[tabIndex] as WzCanvasProperty,
                            tabDisabled[i],
                            10 + (i * 31),
                            29,
                            30,
                            18);
                    }
                }


                skill.SetTabTextures(tabEnabled, tabDisabled);
                skill.SetTabLayout(tabEnabledRects, tabDisabledRects);
                System.Diagnostics.Debug.WriteLine($"[UIWindowLoader] Tab textures loaded: enabled[0]={tabEnabled[0] != null}, disabled[0]={tabDisabled[0] != null}");
            }


            WzSubProperty dualTabProperty = (WzSubProperty)tabProperty?["DualTab"];
            if (dualTabProperty != null)
            {
                WzSubProperty dualEnabledProperty = (WzSubProperty)dualTabProperty["enabled"];
                WzSubProperty dualDisabledProperty = (WzSubProperty)dualTabProperty["disabled"];
                Texture2D[] dualEnabled = new Texture2D[7];
                Texture2D[] dualDisabled = new Texture2D[7];
                Rectangle[] dualEnabledRects = new Rectangle[7];
                Rectangle[] dualDisabledRects = new Rectangle[7];


                for (int i = 0; i < dualEnabled.Length; i++)
                {
                    string tabIndex = i.ToString();
                    dualEnabledRects[i] = new Rectangle(11 + (i * 22), 27, 21, 20);
                    dualDisabledRects[i] = new Rectangle(11 + (i * 22), 29, 21, 18);


                    if (dualEnabledProperty != null)
                    {
                        dualEnabled[i] = LoadCanvasTexture(dualEnabledProperty, tabIndex, device);
                        dualEnabledRects[i] = ResolveCanvasBounds(
                            dualEnabledProperty[tabIndex] as WzCanvasProperty,
                            dualEnabled[i],
                            dualEnabledRects[i].X,
                            dualEnabledRects[i].Y,
                            dualEnabledRects[i].Width,
                            dualEnabledRects[i].Height);
                    }


                    if (dualDisabledProperty != null)
                    {
                        dualDisabled[i] = LoadCanvasTexture(dualDisabledProperty, tabIndex, device);
                        dualDisabledRects[i] = ResolveCanvasBounds(
                            dualDisabledProperty[tabIndex] as WzCanvasProperty,
                            dualDisabled[i],
                            dualDisabledRects[i].X,
                            dualDisabledRects[i].Y,
                            dualDisabledRects[i].Width,
                            dualDisabledRects[i].Height);
                    }
                }


                skill.SetDualTabTextures(dualEnabled, dualDisabled);

                skill.SetDualTabLayout(dualEnabledRects, dualDisabledRects);

            }



            // Load SP Up button textures
            WzSubProperty spUpProperty = (WzSubProperty)mainProperty["BtSpUp"];
            if (spUpProperty != null)
            {
                Texture2D spUpNormal = LoadButtonStateTexture(spUpProperty, "normal", device);
                Texture2D spUpPressed = LoadButtonStateTexture(spUpProperty, "pressed", device);
                Texture2D spUpDisabled = LoadButtonStateTexture(spUpProperty, "disabled", device);
                Texture2D spUpMouseOver = LoadButtonStateTexture(spUpProperty, "mouseOver", device);
                skill.SetSpUpTextures(spUpNormal, spUpPressed, spUpDisabled, spUpMouseOver);
            }


            Texture2D[] tooltipFrames =
            {
                LoadCanvasTexture(mainProperty, "tip0", device),
                LoadCanvasTexture(mainProperty, "tip1", device),
                LoadCanvasTexture(mainProperty, "tip2", device)
            };
            skill.SetTooltipTextures(tooltipFrames);
            Point[] tooltipOrigins =
            {
                ResolveTooltipOrigin(mainProperty["tip0"] as WzCanvasProperty),
                ResolveTooltipOrigin(mainProperty["tip1"] as WzCanvasProperty),
                ResolveTooltipOrigin(mainProperty["tip2"] as WzCanvasProperty)
            };
            skill.SetTooltipOrigins(tooltipOrigins);


            WzSubProperty vScrollProperty = (WzSubProperty)basicImage?["VScr"];
            if (vScrollProperty != null)
            {
                WzSubProperty enabledProperty = (WzSubProperty)vScrollProperty["enabled"];
                WzSubProperty disabledProperty = (WzSubProperty)vScrollProperty["disabled"];
                skill.SetScrollBarTextures(
                    LoadCanvasTexture(enabledProperty, "prev0", device),
                    LoadCanvasTexture(enabledProperty, "prev1", device),
                    LoadCanvasTexture(enabledProperty, "next0", device),
                    LoadCanvasTexture(enabledProperty, "next1", device),
                    LoadCanvasTexture(enabledProperty, "base", device),
                    LoadCanvasTexture(enabledProperty, "thumb0", device),
                    LoadCanvasTexture(enabledProperty, "thumb1", device),
                    LoadCanvasTexture(disabledProperty, "prev", device),
                    LoadCanvasTexture(disabledProperty, "next", device),
                    LoadCanvasTexture(disabledProperty, "base", device));
            }


            // Load button sounds

            WzBinaryProperty btClickSound = (WzBinaryProperty)soundUIImage?["BtMouseClick"];

            WzBinaryProperty btOverSound = (WzBinaryProperty)soundUIImage?["BtMouseOver"];



            // Close button - use BtClose from Basic.img (12x12 small X button)
            // Position from CUISkill constructor: (153, 6)
            WzSubProperty closeButtonProperty = (WzSubProperty)basicImage?["BtClose"];
            UIObject closeBtn = null;
            if (closeButtonProperty != null)
            {
                try
                {
                    closeBtn = new UIObject(closeButtonProperty, btClickSound, btOverSound, false, Point.Zero, device);
                    closeBtn.X = 153;
                    closeBtn.Y = 6;
                }
                catch { }
            }
            skill.InitializeCloseButton(closeBtn);


            // Load macro button - position from WZ origin (-114, -273) means X=114, Y=273
            UIObject macroBtn = LoadButton(mainProperty, "BtMacro", btClickSound, btOverSound, device);
            if (macroBtn != null)
            {
                macroBtn.X = 114;
                macroBtn.Y = 273;
            }
            skill.InitializeMacroButton(macroBtn);


            UIObject rideBtn = LoadButton(mainProperty, "BtRide", btClickSound, btOverSound, device);
            if (rideBtn != null)
            {
                rideBtn.X = 62;
                rideBtn.Y = 273;
            }
            skill.InitializeRideButton(rideBtn);


            UIObject guildSkillBtn = LoadButton(mainProperty, "BtGuildSkill", btClickSound, btOverSound, device);
            if (guildSkillBtn != null)
            {
                guildSkillBtn.X = 10;
                guildSkillBtn.Y = 273;
            }
            skill.InitializeGuildSkillButton(guildSkillBtn);


            WzSubProperty aranButtonProperty = (WzSubProperty)tabProperty?["AranButton"];
            if (aranButtonProperty != null)
            {
                UIObject[] guideButtons = new UIObject[4];
                for (int i = 0; i < guideButtons.Length; i++)
                {
                    guideButtons[i] = LoadButton(aranButtonProperty, $"Bt{i + 1}", btClickSound, btOverSound, device);
                }


                skill.InitializeAranGuideButtons(guideButtons);

            }



            return skill;

        }



        /// <summary>
        /// Load beginner skills into a skill window (legacy method for compatibility)
        /// </summary>
        public static void LoadBeginnerSkills(SkillUIBigBang skillWindow, WzFile skillWzFile, WzFile stringWzFile, GraphicsDevice device)
        {
            // Default to beginner job
            LoadSkillsForJob(skillWindow, 0, device);
        }


        /// <summary>
        /// Load skills for a character's job into a skill window.
        /// Standard jobs populate their advancement path across tabs; admin jobs stay focused on a single book.
        /// </summary>
        /// <param name="skillWindow">The skill window to populate</param>
        /// <param name="jobId">The character's current job ID (e.g., 212 for Bishop)</param>
        /// <param name="device">Graphics device for texture creation</param>
        public static void LoadSkillsForJob(SkillUIBigBang skillWindow, int jobId, GraphicsDevice device)
        {
            if (skillWindow == null)
                return;


            try
            {
                // Clear any previously loaded skills.
                skillWindow.ClearSkills();
                skillWindow.SetUseDualTabStrip(IsDualBladeJob(jobId));


                var pathJobIds = GetDisplayedSkillBookJobIdsForJob(jobId);
                var visibleTabs = new HashSet<int>();
                foreach (int pathJobId in pathJobIds)
                {
                    visibleTabs.Add(GetSkillTabFromJobId(pathJobId));
                }


                // `CUISkill::GetSkillRootVisible` refreshes the visible skill roots from
                // the current job path. Mirror that at the tab layer so the simulator only
                // exposes books the active job can actually browse.
                skillWindow.SetVisibleTabs(visibleTabs);
                skillWindow.ConfigureAranGuideButtons(GetAranGuideUnlockedGrade(jobId));


                // Seed the default beginner book so tabs without a dedicated skill book
                // can still render the same fallback icon the client uses.
                Texture2D defaultBookIcon = SkillDataLoader.LoadJobIcon(0, device);
                if (defaultBookIcon != null)
                {
                    skillWindow.SetDisplayedSkillRootId(0, 0);
                    skillWindow.SetJobInfo(0, defaultBookIcon, SkillDataLoader.GetJobName(0));
                }



                foreach (int pathJobId in pathJobIds)

                {

                    int tabIndex = GetSkillTabFromJobId(pathJobId);

                    System.Diagnostics.Debug.WriteLine($"[UIWindowLoader] Loading skills for display job {pathJobId} into tab {tabIndex} (requested job {jobId})");
                    skillWindow.SetDisplayedSkillRootId(tabIndex, pathJobId);


                    var skillMap = new Dictionary<int, SkillDisplayData>();
                    foreach (int bookJobId in GetSkillBookAliasesForJob(pathJobId))
                    {
                        var skills = SkillDataLoader.LoadSkillsForJob(bookJobId, device);
                        foreach (var skill in skills)
                        {
                            if (skill == null)
                                continue;


                            if (!skillMap.ContainsKey(skill.SkillId))
                                skillMap[skill.SkillId] = skill;
                        }
                    }


                    var mergedSkills = skillMap.Values.ToList();
                    skillWindow.AddSkills(tabIndex, mergedSkills);
                    skillWindow.SetRecommendedSkillEntries(
                        tabIndex,
                        SkillDataLoader.LoadRecommendedSkillEntries(
                            pathJobId,
                            mergedSkills.Select(skill => skill.SkillId)));
                    System.Diagnostics.Debug.WriteLine($"[UIWindowLoader] Tab {tabIndex}: Loaded {mergedSkills.Count} skills for display job {pathJobId}");


                    // Load and set the job icon and name for the populated tab.
                    Texture2D jobIcon = SkillDataLoader.LoadJobIcon(pathJobId, device);
                    if (jobIcon == null)
                    {
                        // Fallback for jobs where the icon lives in another book (e.g. GM).
                        foreach (int bookJobId in GetSkillBookAliasesForJob(pathJobId))
                        {
                            jobIcon = SkillDataLoader.LoadJobIcon(bookJobId, device);
                            if (jobIcon != null)
                                break;
                        }
                    }


                    string jobName = SkillDataLoader.GetJobName(pathJobId);

                    skillWindow.SetJobInfo(tabIndex, jobIcon, jobName);

                }



                // Show the populated tab by default.
                skillWindow.CurrentTab = GetSkillTabFromJobId(jobId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UIWindowLoader] Failed to load skills: {ex.Message}");
            }
        }


        /// <summary>
        /// Load the full skill catalog into the skill window, grouped by advancement tab.
        /// </summary>
        public static void LoadAllSkills(SkillUIBigBang skillWindow, WzFile skillWzFile, GraphicsDevice device, int focusJobId = 0)
        {
            if (skillWindow == null)
                return;


            try
            {
                if (ShouldLoadFocusedJobOnly(focusJobId))
                {
                    LoadSkillsForJob(skillWindow, focusJobId, device);
                    return;
                }


                skillWindow.ClearSkills();

                skillWindow.SetVisibleTabs(new[] { 0, 1, 2, 3, 4 });



                var skillsByTab = new Dictionary<int, Dictionary<int, SkillDisplayData>>
                {
                    { 0, new Dictionary<int, SkillDisplayData>() },
                    { 1, new Dictionary<int, SkillDisplayData>() },
                    { 2, new Dictionary<int, SkillDisplayData>() },
                    { 3, new Dictionary<int, SkillDisplayData>() },
                    { 4, new Dictionary<int, SkillDisplayData>() }
                };


                var defaultIcon = SkillDataLoader.LoadJobIcon(0, device);
                skillWindow.SetJobInfo(0, defaultIcon, "All Beginner Skills");
                skillWindow.SetJobInfo(1, defaultIcon, "All 1st Job Skills");
                skillWindow.SetJobInfo(2, defaultIcon, "All 2nd Job Skills");
                skillWindow.SetJobInfo(3, defaultIcon, "All 3rd Job Skills");
                skillWindow.SetJobInfo(4, defaultIcon, "All 4th Job Skills");


                var availableBookIds = SkillDataLoader.GetAvailableSkillBookJobIds(skillWzFile);
                if (availableBookIds.Count == 0)
                {
                    LoadSkillsForJob(skillWindow, focusJobId, device);
                    return;
                }


                foreach (int bookJobId in availableBookIds)

                {

                    int tabIndex = GetSkillTabFromJobId(bookJobId);



                    foreach (int resolvedBookJobId in GetSkillBookAliasesForJob(bookJobId))
                    {
                        var skills = SkillDataLoader.LoadSkillsForJob(resolvedBookJobId, device);
                        foreach (var skill in skills)
                        {
                            if (skill == null)
                                continue;


                            if (!skillsByTab[tabIndex].ContainsKey(skill.SkillId))
                            {
                                skillsByTab[tabIndex][skill.SkillId] = skill;
                            }
                        }
                    }
                }


                for (int tab = 0; tab <= 4; tab++)
                {
                    skillWindow.AddSkills(tab, skillsByTab[tab].Values);
                }


                int focusTab = GetSkillTabFromJobId(focusJobId);
                skillWindow.CurrentTab = focusTab;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UIWindowLoader] Failed to load full skill catalog: {ex.Message}");
            }
        }


        /// <summary>
        /// Map a job id to a SkillUIBigBang tab index (0..4).
        /// This is a heuristic based on common MapleStory job id patterns:
        /// - 0 => beginner
        /// - xx00 => 1st job
        /// - xx10/xx20/... ending with 0 => 2nd job
        /// - ending with 1 => 3rd job
        /// - ending with 2 => 4th job
        /// </summary>
        private static int GetSkillTabFromJobId(int jobId)
        {
            if (jobId <= 0)
                return 0;


            // Special jobs (Manager/GM/SuperGM) should still show up on the first job tab.

            if (jobId >= 800 && jobId < 1000)

                return 1;



            if (IsDualBladeJob(jobId))
            {
                return jobId switch
                {
                    430 => 2,
                    431 => 3,
                    432 => 4,
                    433 => 5,
                    434 => 6,
                    _ => 1
                };
            }


            // 100, 200, 300, 1100, 1200, 3000, etc.

            if (jobId % 100 == 0)

                return 1;



            return (jobId % 10) switch
            {
                0 => 2,
                1 => 3,
                2 => 4,
                _ => 1
            };
        }


        private static IReadOnlyList<int> GetSkillBookAliasesForJob(int jobId)
        {
            return jobId switch
            {
                900 => new[] { 900, 910 },
                910 => new[] { 910, 900 },
                _ => new[] { jobId }
            };
        }


        private static IReadOnlyList<int> GetDisplayedSkillBookJobIdsForJob(int jobId)
        {
            if (ShouldLoadFocusedJobOnly(jobId))
                return GetSkillBookAliasesForJob(jobId);


            if (IsDualBladeJob(jobId))
            {
                List<int> dualBladePath = new() { 0, 400, 430, 431, 432, 433, 434 };
                return dualBladePath.Where(bookJobId => bookJobId <= jobId).ToList();
            }


            var path = new List<int> { 0 };

            if (jobId <= 0)

                return path;



            int firstJob = (jobId / 100) * 100;

            if (firstJob > 0 && !path.Contains(firstJob))

                path.Add(firstJob);



            int secondJob = (jobId / 10) * 10;

            if (secondJob > firstJob && !path.Contains(secondJob))

                path.Add(secondJob);



            int thirdJob = secondJob + (jobId % 10 > 0 ? 1 : 0);

            if (thirdJob > secondJob && thirdJob < jobId && !path.Contains(thirdJob))

                path.Add(thirdJob);



            if (!path.Contains(jobId))

                path.Add(jobId);



            return path;

        }



        private static int GetAranGuideUnlockedGrade(int jobId)
        {
            return jobId switch
            {
                2000 => 1,
                2100 => 1,
                2110 => 2,
                2111 => 3,
                2112 => 4,
                _ => 0
            };
        }


        private static bool ShouldLoadFocusedJobOnly(int jobId)
        {
            return jobId >= 800 && jobId < 1000;
        }


        private static bool IsDualBladeJob(int jobId)
        {
            return jobId >= 430 && jobId <= 434;
        }


        /// <summary>
        /// Create the Skill Macro window for post-Big Bang
        /// Structure: UI.wz/UIWindow2.img/Skill/macro
        /// </summary>
        public static SkillMacroUI CreateSkillMacroWindowBigBang(
            WzImage uiWindow1Image, WzImage uiWindow2Image, WzImage soundUIImage, GraphicsDevice device, int screenWidth, int screenHeight)
        {

            if (uiWindow2Image == null)

                return null;



            try
            {
                // Get the Skill/macro property
                WzSubProperty skillProperty = (WzSubProperty)uiWindow2Image["Skill"];
                if (skillProperty == null)
                    return null;


                WzSubProperty macroProperty = (WzSubProperty)skillProperty["macro"];

                if (macroProperty == null)

                    return null;



                // Load background - handle both direct canvas and linked canvas
                WzObject backgrndObj = macroProperty["backgrnd"];
                if (backgrndObj == null)
                    return null;


                System.Drawing.Bitmap bgBitmap = null;
                if (backgrndObj is WzCanvasProperty canvasProp)
                {
                    bgBitmap = canvasProp.GetLinkedWzCanvasBitmap();
                }
                else if (backgrndObj is WzSubProperty subProp)
                {
                    // Try to find canvas inside sub-property (might be named "0" or direct child)
                    WzCanvasProperty innerCanvas = (WzCanvasProperty)subProp["0"] ?? (WzCanvasProperty)subProp.WzProperties.FirstOrDefault(p => p is WzCanvasProperty);
                    if (innerCanvas != null)
                        bgBitmap = innerCanvas.GetLinkedWzCanvasBitmap();
                }


                Texture2D bgTexture = bgBitmap?.ToTexture2DAndDispose(device);

                if (bgTexture == null)

                    return null;



                IDXObject frame = new DXObject(0, 0, bgTexture, 0);



                // Create the macro window

                SkillMacroUI macroUI = new SkillMacroUI(frame, device);

                Texture2D foregroundTexture = LoadCanvasTexture(macroProperty, "backgrnd2", device);
                Texture2D contentTexture = LoadCanvasTexture(macroProperty, "backgrnd3", device);
                Texture2D checkboxTexture = LoadCanvasTexture(macroProperty, "check", device);
                macroUI.SetOwnerChrome(
                    foregroundTexture,
                    ResolveCanvasOffset(macroProperty, "backgrnd2", new Point(6, 22)),
                    contentTexture,
                    ResolveCanvasOffset(macroProperty, "backgrnd3", new Point(11, 41)),
                    checkboxTexture);



                // Position window in center of screen
                macroUI.Position = new Point(
                    (screenWidth - bgTexture.Width) / 2,
                    (screenHeight - bgTexture.Height) / 2);


                // Load button sounds

                WzBinaryProperty btClickSound = (WzBinaryProperty)soundUIImage?["BtMouseClick"];

                WzBinaryProperty btOverSound = (WzBinaryProperty)soundUIImage?["BtMouseOver"];



                // Load OK button
                UIObject btnOK = LoadButton(macroProperty, "BtOK", btClickSound, btOverSound, device);
                if (btnOK != null)
                {
                    macroUI.InitializeButtons(btnOK, null, null);
                }


                // Load selection highlight texture
                Texture2D selectTexture = LoadCanvasTexture(macroProperty, "select", device);
                if (selectTexture != null)
                {
                    macroUI.SetSelectionTexture(selectTexture);
                }


                // Load macro slot icons from Macroicon
                WzSubProperty macroIconProp = (WzSubProperty)macroProperty["Macroicon"];
                if (macroIconProp != null)
                {
                    Texture2D[] macroIcons = new Texture2D[5];
                    Texture2D[] macroDisabledIcons = new Texture2D[5];
                    Texture2D[] macroMouseOverIcons = new Texture2D[5];
                    for (int i = 0; i < 5; i++)
                    {
                        WzSubProperty macroIconStateProperty = macroIconProp[i.ToString()] as WzSubProperty;
                        macroIcons[i] = LoadCanvasTexture(macroIconStateProperty, "icon", device);
                        macroDisabledIcons[i] = LoadCanvasTexture(macroIconStateProperty, "iconDisabled", device);
                        macroMouseOverIcons[i] = LoadCanvasTexture(macroIconStateProperty, "iconMouseOver", device);
                    }
                    macroUI.SetMacroSlotIcons(macroIcons, macroDisabledIcons, macroMouseOverIcons);
                }


                SkillMacroSoftKeyboardSkin softKeyboardSkin = LoadSkillMacroSoftKeyboardSkin(uiWindow1Image, device);
                if (softKeyboardSkin != null)
                {
                    macroUI.SetSoftKeyboardSkin(softKeyboardSkin);
                }

                System.Diagnostics.Debug.WriteLine("[UIWindowLoader] Created SkillMacroUI");
                return macroUI;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UIWindowLoader] Failed to create SkillMacroUI: {ex.Message}");
                return null;
            }
        }


        /// <summary>
        /// Load a canvas texture from a property
        /// </summary>
        private static Texture2D LoadCanvasTexture(WzSubProperty parent, string name, GraphicsDevice device)
        {
            WzObject obj = parent?[name];
            if (obj == null)
                return null;


            try
            {
                System.Drawing.Bitmap bitmap = null;
                if (obj is WzCanvasProperty canvas)
                {
                    bitmap = canvas.GetLinkedWzCanvasBitmap();
                }
                else if (obj is WzSubProperty subProp)
                {
                    // Try to find canvas inside sub-property
                    WzCanvasProperty innerCanvas = subProp["0"] as WzCanvasProperty
                        ?? subProp.WzProperties.FirstOrDefault(p => p is WzCanvasProperty) as WzCanvasProperty;
                    if (innerCanvas != null)
                        bitmap = innerCanvas.GetLinkedWzCanvasBitmap();
                }
                return bitmap?.ToTexture2DAndDispose(device);
            }
            catch
            {
                return null;
            }
        }


        private static Dictionary<string, Texture2D> LoadCanvasTextureMap(WzSubProperty parent, GraphicsDevice device)
        {
            var textures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
            if (parent == null)
            {
                return textures;
            }


            foreach (WzImageProperty property in parent.WzProperties)
            {
                Texture2D texture = LoadCanvasTexture(parent, property.Name, device);
                if (texture != null)
                {
                    textures[property.Name] = texture;
                }
            }


            return textures;

        }



        private static Texture2D[] LoadDigitTextures(WzSubProperty parent, GraphicsDevice device)
        {
            Texture2D[] textures = new Texture2D[10];
            if (parent == null)
            {
                return textures;
            }


            for (int i = 0; i < textures.Length; i++)
            {
                textures[i] = LoadCanvasTexture(parent, i.ToString(), device);
            }


            return textures;

        }



        private static Texture2D[] LoadGuildBbsEmoticonSet(WzSubProperty parent, int count, GraphicsDevice device)
        {
            Texture2D[] textures = new Texture2D[count];
            if (parent == null)
            {
                return textures;
            }


            for (int i = 0; i < count; i++)
            {
                textures[i] = LoadCanvasTexture(parent, i.ToString(), device);
            }


            return textures;

        }



        private static int GetPropertyChildCount(WzSubProperty parent, int fallbackCount)
        {
            return parent?.WzProperties?.Count > 0
                ? parent.WzProperties.Count
                : fallbackCount;
        }


        private static IReadOnlyList<VegaSpellUI.VegaAnimationFrame> LoadAnimationFrames(WzSubProperty parent, GraphicsDevice device)
        {
            var frames = new List<VegaSpellUI.VegaAnimationFrame>();
            if (parent == null || device == null)
            {
                return frames;
            }


            for (int i = 0; ; i++)
            {
                if (parent[i.ToString()] is not WzCanvasProperty canvas)
                {
                    break;
                }


                Texture2D texture = canvas.GetLinkedWzCanvasBitmap().ToTexture2DAndDispose(device);
                Point origin = ResolveCanvasOffset(canvas, Point.Zero);
                int delay = InfoTool.GetInt(canvas["delay"], 100);
                frames.Add(new VegaSpellUI.VegaAnimationFrame(texture, new Point(-origin.X, -origin.Y), delay));
            }


            return frames;

        }



        /// <summary>

        /// Load a button state texture (normal/pressed/disabled/mouseOver has sub-property "0")

        /// </summary>

        private static Texture2D LoadButtonStateTexture(WzSubProperty buttonProperty, string stateName, GraphicsDevice device)
        {
            WzSubProperty stateProperty = (WzSubProperty)buttonProperty?[stateName];
            if (stateProperty == null)
                return null;


            WzCanvasProperty canvas = (WzCanvasProperty)stateProperty["0"];

            if (canvas == null)

                return null;



            try
            {
                System.Drawing.Bitmap bitmap = canvas.GetLinkedWzCanvasBitmap();
                return bitmap?.ToTexture2DAndDispose(device);
            }
            catch
            {
                return null;
            }
        }

        private static SkillMacroSoftKeyboardSkin LoadSkillMacroSoftKeyboardSkin(WzImage uiWindow1Image, GraphicsDevice device)
        {
            WzSubProperty softKeyboardProperty = uiWindow1Image?["SoftKeyboard"] as WzSubProperty;
            if (softKeyboardProperty == null)
                return null;

            WzSubProperty backgroundProperty = softKeyboardProperty["backgrnd"] as WzSubProperty;
            WzSubProperty buttonProperty = softKeyboardProperty["Bt"] as WzSubProperty;

            SkillMacroSoftKeyboardSkin skin = new SkillMacroSoftKeyboardSkin
            {
                ExpandedBackground = LoadCanvasTexture(backgroundProperty?["1"] as WzSubProperty, "back", device),
                MinimizedBackground = LoadCanvasTexture(backgroundProperty?["0"] as WzSubProperty, "back", device),
                ExpandedTitle = LoadCanvasTexture(backgroundProperty?["1"] as WzSubProperty, "1", device),
                MinimizedTitle = LoadCanvasTexture(backgroundProperty?["0"] as WzSubProperty, "1", device),
                KeyboardBackground = LoadCanvasTexture(backgroundProperty?["1"] as WzSubProperty, "keyboard_new", device)
                    ?? LoadCanvasTexture(backgroundProperty?["1"] as WzSubProperty, "keyboard", device)
                    ?? LoadCanvasTexture(backgroundProperty?["0"] as WzSubProperty, "keyboard", device)
            };

            WzSubProperty keyProperty = softKeyboardProperty["key"] as WzSubProperty;
            if (keyProperty != null)
            {
                for (int i = 0; i <= 36; i++)
                {
                    WzSubProperty stateProperty = keyProperty[i.ToString()] as WzSubProperty;
                    if (stateProperty == null)
                        continue;

                    skin.KeyTextures[i] = LoadSoftKeyboardTextures(stateProperty, device);
                }
            }

            WzSubProperty functionKeyProperty = softKeyboardProperty["funckey"] as WzSubProperty;
            if (functionKeyProperty != null)
            {
                skin.FunctionKeyTextures[SkillMacroSoftKeyboardFunctionKey.CapsLock] = LoadSoftKeyboardTextures(functionKeyProperty["capslock"] as WzSubProperty, device);
                skin.FunctionKeyTextures[SkillMacroSoftKeyboardFunctionKey.LeftShift] = LoadSoftKeyboardTextures(functionKeyProperty["lshift"] as WzSubProperty, device);
                skin.FunctionKeyTextures[SkillMacroSoftKeyboardFunctionKey.RightShift] = LoadSoftKeyboardTextures(functionKeyProperty["rshift"] as WzSubProperty, device);
                skin.FunctionKeyTextures[SkillMacroSoftKeyboardFunctionKey.Enter] = LoadSoftKeyboardTextures(functionKeyProperty["enter"] as WzSubProperty, device);
                skin.FunctionKeyTextures[SkillMacroSoftKeyboardFunctionKey.Backspace] = LoadSoftKeyboardTextures(functionKeyProperty["backspace"] as WzSubProperty, device);
            }

            WzSubProperty expandedButtons = buttonProperty?["1"] as WzSubProperty;
            WzSubProperty minimizedButtons = buttonProperty?["0"] as WzSubProperty;
            skin.WindowButtonTextures[SkillMacroSoftKeyboardWindowButton.Close] = LoadSoftKeyboardTextures(expandedButtons?["BtClose"] as WzSubProperty ?? minimizedButtons?["BtClose"] as WzSubProperty, device);
            skin.WindowButtonTextures[SkillMacroSoftKeyboardWindowButton.Minimize] = LoadSoftKeyboardTextures(expandedButtons?["BtMin"] as WzSubProperty ?? minimizedButtons?["BtMin"] as WzSubProperty, device);
            skin.WindowButtonTextures[SkillMacroSoftKeyboardWindowButton.Maximize] = LoadSoftKeyboardTextures(expandedButtons?["BtMax"] as WzSubProperty ?? minimizedButtons?["BtMax"] as WzSubProperty, device);
            return skin;
        }

        private static SkillMacroSoftKeyboardKeyTextures LoadSoftKeyboardTextures(WzSubProperty property, GraphicsDevice device)
        {
            if (property == null)
                return null;

            return new SkillMacroSoftKeyboardKeyTextures
            {
                Normal = LoadButtonStateTexture(property, "normal", device),
                Hovered = LoadButtonStateTexture(property, "mouseOver", device),
                Pressed = LoadButtonStateTexture(property, "pressed", device),
                Disabled = LoadButtonStateTexture(property, "disabled", device)
            };
        }

        private static SkillUIBigBang CreatePlaceholderSkillBigBang(GraphicsDevice device, int screenWidth, int screenHeight)
        {
            int width = 174;

            int height = 299;



            Texture2D bgTexture = CreatePlaceholderWindowTexture(device, width, height, "Skills");

            IDXObject frame = new DXObject(0, 0, bgTexture, 0);



            SkillUIBigBang skill = new SkillUIBigBang(frame, device);

            skill.Position = new Point(50, 100);



            return skill;

        }

        #endregion



        #region Quest Window (Big Bang)
        /// <summary>
        /// Create the Quest window - selects pre-BB or post-BB version based on isBigBang
        /// </summary>
        public static UIWindowBase CreateQuestWindowUnified(
            WzImage uiWindow1Image, WzImage uiWindow2Image, WzImage basicImage, WzImage soundUIImage,
            GraphicsDevice device, int screenWidth, int screenHeight, bool isBigBang)
        {
            if (isBigBang && uiWindow2Image != null)
            {
                return CreateQuestWindowBigBang(uiWindow2Image, uiWindow1Image, basicImage, soundUIImage, device, screenWidth, screenHeight);
            }
            return CreateQuestWindow(uiWindow1Image, soundUIImage, device, screenWidth, screenHeight);
        }


        /// <summary>
        /// Create the Quest window from UI.wz/UIWindow2.img/Quest/list (Post-Big Bang)
        /// </summary>
        public static QuestUIBigBang CreateQuestWindowBigBang(
            WzImage uiWindow2Image, WzImage uiWindow1Image, WzImage basicImage, WzImage soundUIImage,
            GraphicsDevice device, int screenWidth, int screenHeight)
        {
            WzSubProperty questProperty = (WzSubProperty)uiWindow2Image?["Quest"];
            WzSubProperty listProperty = (WzSubProperty)questProperty?["list"];
            if (listProperty == null)
            {
                return CreatePlaceholderQuestBigBang(device, screenWidth, screenHeight);
            }


            // Get main background
            WzCanvasProperty backgrnd = (WzCanvasProperty)listProperty["backgrnd"];
            if (backgrnd == null)
            {
                return CreatePlaceholderQuestBigBang(device, screenWidth, screenHeight);
            }


            System.Drawing.Bitmap bgBitmap = backgrnd.GetLinkedWzCanvasBitmap();

            Texture2D bgTexture = bgBitmap.ToTexture2DAndDispose(device);

            IDXObject frame = new DXObject(0, 0, bgTexture, 0);



            QuestUIBigBang quest = new QuestUIBigBang(frame, device);

            quest.Position = new Point(50, 100);



            // Load foreground (backgrnd2 - labels/overlay)
            WzCanvasProperty backgrnd2 = (WzCanvasProperty)listProperty["backgrnd2"];
            if (backgrnd2 != null)
            {
                try
                {
                    System.Drawing.Bitmap fgBitmap = backgrnd2.GetLinkedWzCanvasBitmap();
                    Texture2D fgTexture = fgBitmap.ToTexture2DAndDispose(device);
                    IDXObject foreground = new DXObject(0, 0, fgTexture, 0);
                    System.Drawing.PointF? origin = backgrnd2.GetCanvasOriginPosition();
                    int offsetX = origin.HasValue ? -(int)origin.Value.X : 6;
                    int offsetY = origin.HasValue ? -(int)origin.Value.Y : 23;
                    quest.SetForeground(foreground, offsetX, offsetY);
                }
                catch { }
            }


            // Load button sounds

            WzBinaryProperty btClickSound = (WzBinaryProperty)soundUIImage?["BtMouseClick"];

            WzBinaryProperty btOverSound = (WzBinaryProperty)soundUIImage?["BtMouseOver"];



            // Close button - use BtClose from Basic.img (12x12 small X button)
            // Position from CUIQuestInfo constructor: (214, 6)
            WzSubProperty closeButtonProperty = (WzSubProperty)basicImage?["BtClose"];
            UIObject closeBtn = null;
            if (closeButtonProperty != null)
            {
                try
                {
                    closeBtn = new UIObject(closeButtonProperty, btClickSound, btOverSound, false, Point.Zero, device);
                    closeBtn.X = 214;
                    closeBtn.Y = 6;
                }
                catch { }
            }
            quest.InitializeCloseButton(closeBtn);


            // Load quest icons from UIWindow2.img/QuestIcon
            WzSubProperty questIconProperty = (WzSubProperty)uiWindow2Image["QuestIcon"];
            if (questIconProperty != null)
            {
                Texture2D iconAvailable = LoadQuestIcon(questIconProperty, "0", device);
                Texture2D iconInProgress = LoadQuestIcon(questIconProperty, "1", device);
                Texture2D iconCompleted = LoadQuestIcon(questIconProperty, "2", device);
                quest.SetQuestIcons(iconAvailable, iconInProgress, iconCompleted);
            }


            UIObject tabAvailable = LoadQuestCanvasTabButton(listProperty, "0", btClickSound, btOverSound, device);
            UIObject tabInProgress = LoadQuestCanvasTabButton(listProperty, "1", btClickSound, btOverSound, device);
            UIObject tabCompleted = LoadQuestCanvasTabButton(listProperty, "2", btClickSound, btOverSound, device);
            UIObject tabRecommended = LoadQuestCanvasTabButton(listProperty, "3", btClickSound, btOverSound, device);
            quest.InitializeTabs(tabAvailable, tabInProgress, tabCompleted, tabRecommended);


            UIObject myLevelButton = LoadButton(listProperty, "BtMyLevel", btClickSound, btOverSound, device);

            UIObject allLevelButton = LoadButton(listProperty, "BtAllLevel", btClickSound, btOverSound, device);

            quest.InitializeLevelFilterButtons(myLevelButton, allLevelButton);
            quest.InitializeCategoryFilterButtons(
                LoadButton(questProperty, "BtMax", btClickSound, btOverSound, device),
                LoadButton(questProperty, "BtMin", btClickSound, btOverSound, device),
                LoadButton(listProperty, "BtIconInfo", btClickSound, btOverSound, device));

            WzSubProperty iconInfoProperty = questProperty?["icon_info"] as WzSubProperty;
            WzSubProperty iconInfoSheetsProperty = iconInfoProperty?["Sheet"] as WzSubProperty;
            Texture2D[] iconInfoSheets = iconInfoSheetsProperty?.WzProperties
                ?.OfType<WzCanvasProperty>()
                .OrderBy(property => int.TryParse(property.Name, out int index) ? index : int.MaxValue)
                .Select(property => property.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device))
                .Where(texture => texture != null)
                .ToArray()
                ?? Array.Empty<Texture2D>();
            quest.SetCategoryLegendTextures(
                LoadCanvasTexture(iconInfoProperty, "backgrnd", device),
                LoadCanvasTexture(iconInfoProperty, "backgrnd2", device),
                iconInfoSheets);


            return quest;

        }



        private static QuestUIBigBang CreatePlaceholderQuestBigBang(GraphicsDevice device, int screenWidth, int screenHeight)
        {
            int width = 235;
            int height = 396;


            Texture2D bgTexture = CreatePlaceholderWindowTexture(device, width, height, "Quest");

            IDXObject frame = new DXObject(0, 0, bgTexture, 0);



            QuestUIBigBang quest = new QuestUIBigBang(frame, device);

            quest.Position = new Point(50, 100);



            return quest;

        }

        #endregion





        #region Quest Window
        /// <summary>
        /// Create the Quest window from UI.wz/UIWindow.img/Quest
        /// </summary>
        public static QuestUI CreateQuestWindow(
            WzImage uiWindowImage, WzImage soundUIImage,
            GraphicsDevice device, int screenWidth, int screenHeight)
        {
            WzSubProperty questProperty = (WzSubProperty)uiWindowImage?["Quest"];
            if (questProperty == null)
            {
                return CreatePlaceholderQuest(device, screenWidth, screenHeight);
            }


            // Get main background
            WzCanvasProperty backgrnd = (WzCanvasProperty)questProperty["backgrnd"];
            if (backgrnd == null)
            {
                return CreatePlaceholderQuest(device, screenWidth, screenHeight);
            }


            System.Drawing.Bitmap bgBitmap = backgrnd.GetLinkedWzCanvasBitmap();

            Texture2D bgTexture = bgBitmap.ToTexture2DAndDispose(device);

            IDXObject frame = new DXObject(0, 0, bgTexture, 0);



            QuestUI quest = new QuestUI(frame, device);

            quest.Position = new Point(50, 150);



            // Load buttons

            WzBinaryProperty btClickSound = (WzBinaryProperty)soundUIImage?["BtMouseClick"];

            WzBinaryProperty btOverSound = (WzBinaryProperty)soundUIImage?["BtMouseOver"];



            UIObject closeBtn = LoadButton(questProperty, "BtClose", btClickSound, btOverSound, device);

            quest.InitializeCloseButton(closeBtn);



            // Load quest icons
            WzSubProperty questIconProperty = (WzSubProperty)uiWindowImage["QuestIcon"];
            if (questIconProperty != null)
            {
                Texture2D iconAvailable = LoadQuestIcon(questIconProperty, "0", device);
                Texture2D iconInProgress = LoadQuestIcon(questIconProperty, "1", device);
                Texture2D iconCompleted = LoadQuestIcon(questIconProperty, "2", device);
                quest.SetQuestIcons(iconAvailable, iconInProgress, iconCompleted);
            }


            UIObject tabAvailable = LoadQuestCanvasTabButton(questProperty, "0", btClickSound, btOverSound, device);
            UIObject tabInProgress = LoadQuestCanvasTabButton(questProperty, "1", btClickSound, btOverSound, device);
            UIObject tabCompleted = LoadQuestCanvasTabButton(questProperty, "2", btClickSound, btOverSound, device);
            UIObject tabRecommended = LoadQuestCanvasTabButton(questProperty, "3", btClickSound, btOverSound, device);
            quest.InitializeTabs(tabAvailable, tabInProgress, tabCompleted, tabRecommended);


            UIObject myLevelButton = LoadButton(questProperty, "BtMyLevel", btClickSound, btOverSound, device);
            UIObject allLevelButton = LoadButton(questProperty, "BtAllLevel", btClickSound, btOverSound, device);
            quest.InitializeLevelFilterButtons(myLevelButton, allLevelButton);
            quest.InitializeDetailButton(LoadButton(questProperty, "BtDetail", btClickSound, btOverSound, device));


            return quest;

        }



        private static QuestUI CreatePlaceholderQuest(GraphicsDevice device, int screenWidth, int screenHeight)
        {
            int width = 220;
            int height = 350;


            Texture2D bgTexture = CreatePlaceholderWindowTexture(device, width, height, "Quest Log");

            IDXObject frame = new DXObject(0, 0, bgTexture, 0);



            QuestUI quest = new QuestUI(frame, device);

            quest.Position = new Point(50, 150);



            return quest;

        }



        private static QuestDetailWindow CreateQuestDetailWindowUnified(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight,
            bool isBigBang)
        {
            QuestDetailWindow window = isBigBang
                ? CreateQuestDetailWindowBigBang(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight)
                : CreateQuestDetailWindowPreBigBang(uiWindow1Image, basicImage, soundUIImage, device, screenWidth, screenHeight);


            if (window != null)
            {
                return window;
            }


            return CreatePlaceholderQuestDetailWindow(device, basicImage, soundUIImage, screenWidth, screenHeight);

        }



        private static QuestDetailWindow CreateQuestDetailWindowBigBang(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            WzSubProperty questInfoProperty = uiWindow2Image?["Quest"]?["quest_info"] as WzSubProperty;
            WzCanvasProperty backgroundProperty = questInfoProperty?["backgrnd"] as WzCanvasProperty;
            Texture2D frameTexture = backgroundProperty?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            if (frameTexture == null)
            {
                return null;
            }


            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            QuestDetailWindow window = CreateQuestDetailWindowShell(device, frameTexture, screenWidth, screenHeight);
            WzSubProperty questProperty = uiWindow2Image?["Quest"] as WzSubProperty;
            WzSubProperty legacyQuestProperty = uiWindow1Image?["Quest"] as WzSubProperty;

            Texture2D foregroundTexture = LoadCanvasTexture(questInfoProperty, "backgrnd2", device);
            if (foregroundTexture != null)
            {
                window.SetForeground(new DXObject(0, 0, foregroundTexture, 0), ResolveCanvasOffset(questInfoProperty, "backgrnd2", new Point(6, 23)));
            }


            Texture2D panelTexture = LoadCanvasTexture(questInfoProperty, "backgrnd3", device);
            if (panelTexture != null)
            {
                window.SetBottomPanel(new DXObject(0, 0, panelTexture, 0), ResolveCanvasOffset(questInfoProperty, "backgrnd3", new Point(10, 27)));
            }


            Texture2D summaryPanelTexture = LoadCanvasTexture(questInfoProperty, "summary", device);
            if (summaryPanelTexture != null)
            {
                window.SetSummaryPanel(new DXObject(0, 0, summaryPanelTexture, 0), ResolveCanvasOffset(questInfoProperty, "summary", new Point(10, 252)));
            }
            Texture2D detailTipTexture = LoadCanvasTexture(questInfoProperty, "tip", device);
            if (detailTipTexture != null)
            {
                window.SetDetailTip(new DXObject(0, 0, detailTipTexture, 0), ResolveCanvasOffset(questInfoProperty, "tip", new Point(24, 89)));
            }
            window.SetSectionTextures(
                LoadCanvasTexture(questInfoProperty["summary_icon"] as WzSubProperty, "summary", device),
                LoadCanvasTexture(questInfoProperty["summary_icon"] as WzSubProperty, "basic", device),
                LoadCanvasTexture(questInfoProperty["summary_icon"] as WzSubProperty, "reward", device),
                LoadCanvasTexture(questInfoProperty["summary_icon"] as WzSubProperty, "select", device),
                LoadCanvasTexture(questInfoProperty["summary_icon"] as WzSubProperty, "prob", device));
            window.SetProgressTextures(
                LoadCanvasTexture(questInfoProperty["Gauge"] as WzSubProperty, "frame", device),
                LoadCanvasTexture(questInfoProperty["Gauge"] as WzSubProperty, "gauge", device),
                LoadCanvasTexture(questInfoProperty["Gauge"] as WzSubProperty, "spot", device),
                new Point(30, 0));
            InitializeQuestDetailTimeLimitArt(window, uiWindow2Image, uiWindow1Image, device);
            InitializeQuestDetailNoticeArt(window, uiWindow2Image?["Quest"]?["list"] as WzSubProperty, legacyQuestProperty, device);


            UIObject closeButton = CreateUserInfoCloseButton(basicImage, clickSound, overSound, device, frameTexture.Width);

            window.InitializeCloseButton(closeButton);

            InitializeQuestDetailButtons(window, questInfoProperty, questProperty, legacyQuestProperty, clickSound, overSound, device, frameTexture.Width, frameTexture.Height, true);
            return window;

        }



        private static QuestDetailWindow CreateQuestDetailWindowPreBigBang(
            WzImage uiWindow1Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            WzSubProperty questProperty = uiWindow1Image?["Quest"] as WzSubProperty;
            WzCanvasProperty backgroundProperty = questProperty?["backgrnd2"] as WzCanvasProperty ?? questProperty?["backgrnd"] as WzCanvasProperty;
            Texture2D frameTexture = backgroundProperty?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            if (frameTexture == null)
            {
                return null;
            }


            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;

            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;

            QuestDetailWindow window = CreateQuestDetailWindowShell(device, frameTexture, screenWidth, screenHeight);



            Texture2D panelTexture = LoadCanvasTexture(questProperty, "backgrnd5", device);
            if (panelTexture != null)
            {
                window.SetBottomPanel(new DXObject(0, 0, panelTexture, 0), ResolveCanvasOffset(questProperty, "backgrnd5", new Point(20, 252)));
            }


            window.SetSectionTextures(
                LoadCanvasTexture(questProperty, "summary", device),
                LoadCanvasTexture(questProperty, "basic", device),
                LoadCanvasTexture(questProperty, "reward", device),
                LoadCanvasTexture(questProperty, "select", device),
                LoadCanvasTexture(questProperty, "prob", device));
            window.SetProgressTextures(
                LoadCanvasTexture(questProperty["Gauge"] as WzSubProperty, "frame", device),
                LoadCanvasTexture(questProperty["Gauge"] as WzSubProperty, "gauge", device),
                LoadCanvasTexture(questProperty["Gauge"] as WzSubProperty, "spot", device),
                new Point(32, 0));
            InitializeQuestDetailTimeLimitArt(window, uiWindow1Image, uiWindow1Image, device);
            InitializeQuestDetailNoticeArt(window, questProperty, questProperty, device);


            UIObject closeButton = CreateUserInfoCloseButton(basicImage, clickSound, overSound, device, frameTexture.Width);

            window.InitializeCloseButton(closeButton);

            InitializeQuestDetailButtons(window, questProperty, questProperty, questProperty, clickSound, overSound, device, frameTexture.Width, frameTexture.Height, false);
            return window;

        }



        private static QuestDetailWindow CreatePlaceholderQuestDetailWindow(
            GraphicsDevice device,
            WzImage basicImage,
            WzImage soundUIImage,
            int screenWidth,
            int screenHeight)
        {
            Texture2D frameTexture = CreatePlaceholderWindowTexture(device, 296, 396, "Quest Detail");
            QuestDetailWindow window = CreateQuestDetailWindowShell(device, frameTexture, screenWidth, screenHeight);
            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            window.InitializeCloseButton(CreateUserInfoCloseButton(basicImage, clickSound, overSound, device, frameTexture.Width));
            InitializeQuestDetailButtons(window, null, null, null, clickSound, overSound, device, frameTexture.Width, frameTexture.Height, true);
            return window;

        }



        private static QuestDetailWindow CreateQuestDetailWindowShell(
            GraphicsDevice device,
            Texture2D frameTexture,
            int screenWidth,
            int screenHeight)
        {
            return new QuestDetailWindow(new DXObject(0, 0, frameTexture, 0), MapSimulatorWindowNames.QuestDetail)
            {
                Position = new Point(
                    Math.Max(40, (screenWidth / 2) - (frameTexture.Width / 2)),
                    Math.Max(40, (screenHeight / 2) - (frameTexture.Height / 2)))
            };
        }


        private static void InitializeQuestDetailButtons(
            QuestDetailWindow window,
            WzSubProperty buttonSource,
            WzSubProperty defaultButtonSource,
            WzSubProperty legacyButtonSource,
            WzBinaryProperty clickSound,
            WzBinaryProperty overSound,
            GraphicsDevice device,
            int frameWidth,
            int frameHeight,
            bool isBigBang)
        {
            bool hasAcceptArt = (isBigBang ? defaultButtonSource?["BtOK"] : buttonSource?["BtOK"]) is WzSubProperty;
            bool hasCompleteArt = hasAcceptArt;
            bool hasTrackArt = (isBigBang ? buttonSource?["BtArlim"] : buttonSource?["BtAlert"]) is WzSubProperty;
            bool hasGiveUpArt = buttonSource?["BtGiveup"] is WzSubProperty;
            bool hasDeliveryAcceptArt = isBigBang && buttonSource?["BtQuestDeliveryAccept"] is WzSubProperty;
            bool hasDeliveryCompleteArt = isBigBang && buttonSource?["BtQuestDeliveryComplete"] is WzSubProperty;

            UIObject acceptButton = CreateQuestDetailActionButton(
                (isBigBang ? defaultButtonSource?["BtOK"] : buttonSource?["BtOK"]) as WzSubProperty,
                clickSound, overSound, device);
            UIObject completeButton = CreateQuestDetailActionButton(
                (isBigBang ? defaultButtonSource?["BtOK"] : buttonSource?["BtOK"]) as WzSubProperty,
                clickSound, overSound, device);
            UIObject trackButton = CreateQuestDetailActionButton(
                (isBigBang ? buttonSource?["BtArlim"] : buttonSource?["BtAlert"]) as WzSubProperty,
                clickSound, overSound, device);
            UIObject giveUpButton = CreateQuestDetailActionButton(buttonSource?["BtGiveup"] as WzSubProperty, clickSound, overSound, device);
            UIObject markMobButton = CreateQuestDetailActionButton((legacyButtonSource?["BtMarkMob"]) as WzSubProperty, clickSound, overSound, device);
            UIObject genericNpcButton = CreateQuestDetailActionButton((isBigBang ? buttonSource?["BtNPC"] : null) as WzSubProperty, clickSound, overSound, device);
            UIObject deliveryAcceptButton = isBigBang
                ? CreateQuestDetailActionButton(buttonSource?["BtQuestDeliveryAccept"] as WzSubProperty, clickSound, overSound, device)
                : null;
            UIObject deliveryCompleteButton = isBigBang
                ? CreateQuestDetailActionButton(buttonSource?["BtQuestDeliveryComplete"] as WzSubProperty, clickSound, overSound, device)
                : null;
            UIObject markNpcButton = CreateQuestDetailActionButton((legacyButtonSource?["BtMarkNpc"]) as WzSubProperty, clickSound, overSound, device)
                                      ?? CreateQuestDetailActionButton((isBigBang ? buttonSource?["BtNPC"] : null) as WzSubProperty, clickSound, overSound, device);
            UIObject gotoNpcButton = CreateQuestDetailActionButton((legacyButtonSource?["BtGotoNpc"]) as WzSubProperty, clickSound, overSound, device)
                                      ?? CreateQuestDetailActionButton((isBigBang ? buttonSource?["BtNPC"] : null) as WzSubProperty, clickSound, overSound, device);


            acceptButton ??= CreateFallbackQuestDetailButton(device, 117, 16);
            completeButton ??= CreateFallbackQuestDetailButton(device, 117, 16);
            trackButton ??= CreateFallbackQuestDetailButton(device, 86, 17);
            giveUpButton ??= CreateFallbackQuestDetailButton(device, 60, 16);
            markMobButton ??= CreateFallbackQuestDetailButton(device, 78, 16);
            genericNpcButton ??= CreateFallbackQuestDetailButton(device, 80, 16);
            markNpcButton ??= CreateFallbackQuestDetailButton(device, 81, 17);
            gotoNpcButton ??= CreateFallbackQuestDetailButton(device, 105, 16);
            if (isBigBang)
            {
                deliveryAcceptButton ??= CreateFallbackQuestDetailButton(device, 117, 16);
                deliveryCompleteButton ??= CreateFallbackQuestDetailButton(device, 117, 16);
            }


            PositionQuestDetailActionButton(acceptButton, frameWidth, frameHeight, 12, 8);
            PositionQuestDetailActionButton(completeButton, frameWidth, frameHeight, 12, 8);
            PositionQuestDetailActionButton(trackButton, frameWidth, frameHeight, 12, 8);
            PositionQuestDetailActionButton(giveUpButton, frameWidth, frameHeight, 16, acceptButton?.CanvasSnapshotWidth ?? completeButton?.CanvasSnapshotWidth ?? trackButton?.CanvasSnapshotWidth ?? 78);
            int npcOffset = (acceptButton?.CanvasSnapshotWidth ?? completeButton?.CanvasSnapshotWidth ?? trackButton?.CanvasSnapshotWidth ?? 78)
                + (giveUpButton?.CanvasSnapshotWidth ?? 60) + 20;
            PositionQuestDetailActionButton(genericNpcButton, frameWidth, frameHeight, 16, npcOffset);
            PositionQuestDetailActionButton(markNpcButton, frameWidth, frameHeight, 16, npcOffset);
            PositionQuestDetailActionButton(gotoNpcButton, frameWidth, frameHeight, 16, npcOffset);


            window.RegisterActionButton(QuestWindowActionKind.Accept, acceptButton, !hasAcceptArt);

            window.RegisterActionButton(QuestWindowActionKind.Complete, completeButton, !hasCompleteArt);

            window.RegisterActionButton(QuestWindowActionKind.Track, trackButton, !hasTrackArt);
            window.RegisterActionButton(QuestWindowActionKind.GiveUp, giveUpButton, !hasGiveUpArt);
            window.RegisterActionButton(QuestWindowActionKind.LocateMob, markMobButton, legacyButtonSource?["BtMarkMob"] is not WzSubProperty);
            window.RegisterActionButton(QuestWindowActionKind.QuestDeliveryAccept, deliveryAcceptButton, !hasDeliveryAcceptArt);
            window.RegisterActionButton(QuestWindowActionKind.QuestDeliveryComplete, deliveryCompleteButton, !hasDeliveryCompleteArt);
            window.RegisterNpcButton(QuestDetailNpcButtonStyle.GenericNpc, genericNpcButton, buttonSource?["BtNPC"] is not WzSubProperty);
            window.RegisterNpcButton(QuestDetailNpcButtonStyle.MarkNpc, markNpcButton, legacyButtonSource?["BtMarkNpc"] is not WzSubProperty);
            window.RegisterNpcButton(QuestDetailNpcButtonStyle.GotoNpc, gotoNpcButton, legacyButtonSource?["BtGotoNpc"] is not WzSubProperty);
            window.InitializeNavigationButtons(device);
        }


        private static void InitializeQuestDetailNoticeArt(
            QuestDetailWindow window,
            WzSubProperty noticeSource,
            WzSubProperty legacyQuestSource,
            GraphicsDevice device)
        {
            if (window == null)
            {
                return;
            }


            string[] noticeNames = { "notice0", "notice1", "notice2", "notice3" };
            Texture2D[] noticeTextures = new Texture2D[noticeNames.Length];
            Point[] noticeOffsets = new Point[noticeNames.Length];
            for (int i = 0; i < noticeNames.Length; i++)
            {
                noticeTextures[i] = LoadCanvasTexture(noticeSource, noticeNames[i], device) ?? LoadCanvasTexture(legacyQuestSource, noticeNames[i], device);
                noticeOffsets[i] = ResolveCanvasOffset(noticeSource, noticeNames[i], ResolveCanvasOffset(legacyQuestSource, noticeNames[i], new Point(118, 74)));
            }


            LoadQuestDetailNoticeAnimation(
                legacyQuestSource?["icon3"] as WzSubProperty
                ?? legacyQuestSource?["icon2"] as WzSubProperty
                ?? legacyQuestSource?["icon5"] as WzSubProperty,
                device,
                out Texture2D[] animationFrames,
                out int[] animationDelays);


            Point animationOffset = new Point(noticeOffsets[0].X + 8, noticeOffsets[0].Y + 9);

            window.SetNoticeTextures(noticeTextures, noticeOffsets, animationFrames, animationDelays, animationOffset);

        }

        private static void InitializeQuestDetailTimeLimitArt(
            QuestDetailWindow window,
            WzImage preferredUiImage,
            WzImage fallbackUiImage,
            GraphicsDevice device)
        {
            if (window == null)
            {
                return;
            }

            window.SetTimeLimitBarTextures(
                ResolveCanvasProperty(preferredUiImage, "Quest/TimeQuest/TimeBar/backgrnd")?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device)
                    ?? ResolveCanvasProperty(fallbackUiImage, "Quest/TimeQuest/TimeBar/backgrnd")?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device),
                ResolveCanvasProperty(preferredUiImage, "Quest/TimeQuest/TimeBar/TimeGage/0")?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device)
                    ?? ResolveCanvasProperty(fallbackUiImage, "Quest/TimeQuest/TimeBar/TimeGage/0")?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device),
                ResolveCanvasProperty(preferredUiImage, "Quest/TimeQuest/TimeBar/TimeGage/1")?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device)
                    ?? ResolveCanvasProperty(fallbackUiImage, "Quest/TimeQuest/TimeBar/TimeGage/1")?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device),
                ResolveCanvasProperty(preferredUiImage, "Quest/TimeQuest/TimeBar/TimeGage/2")?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device)
                    ?? ResolveCanvasProperty(fallbackUiImage, "Quest/TimeQuest/TimeBar/TimeGage/2")?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device));

            LoadQuestDetailTimeLimitIndicatorStyle(window, "default", preferredUiImage, fallbackUiImage, device);
            LoadQuestDetailTimeLimitIndicatorStyle(window, "SelectMob", preferredUiImage, fallbackUiImage, device);
        }

        private static void LoadQuestDetailTimeLimitIndicatorStyle(
            QuestDetailWindow window,
            string styleKey,
            WzImage preferredUiImage,
            WzImage fallbackUiImage,
            GraphicsDevice device)
        {
            if (window == null || string.IsNullOrWhiteSpace(styleKey))
            {
                return;
            }

            WzSubProperty styleProperty = ResolveProperty(preferredUiImage, $"Quest/TimeQuest/AlarmClock/{styleKey}") as WzSubProperty
                ?? ResolveProperty(fallbackUiImage, $"Quest/TimeQuest/AlarmClock/{styleKey}") as WzSubProperty;
            if (styleProperty == null)
            {
                return;
            }

            List<Texture2D> frames = new();
            List<Point> origins = new();
            List<int> delays = new();
            foreach (WzImageProperty groupProperty in styleProperty.WzProperties.OrderBy(static property => property.Name, StringComparer.Ordinal))
            {
                if (groupProperty is not WzSubProperty frameGroup)
                {
                    continue;
                }

                foreach (WzImageProperty frameProperty in frameGroup.WzProperties.OrderBy(static property => property.Name, StringComparer.Ordinal))
                {
                    if (frameProperty is not WzCanvasProperty canvas)
                    {
                        continue;
                    }

                    Texture2D texture = canvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
                    if (texture == null)
                    {
                        continue;
                    }

                    frames.Add(texture);
                    Point offset = ResolveCanvasOffset(canvas, Point.Zero);
                    origins.Add(new Point(-offset.X, -offset.Y));
                    delays.Add(Math.Max(120, (canvas["delay"] as WzIntProperty)?.Value ?? 120));
                }
            }

            if (frames.Count > 0)
            {
                window.SetTimeLimitIndicatorStyle(styleKey, frames.ToArray(), origins.ToArray(), delays.ToArray());
            }
        }

        private static WzImageProperty ResolveProperty(WzObject root, string propertyPath)
        {
            if (root == null || string.IsNullOrWhiteSpace(propertyPath))
            {
                return root as WzImageProperty;
            }

            WzObject current = root;
            string[] pathSegments = propertyPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < pathSegments.Length && current != null; i++)
            {
                current = current switch
                {
                    WzImage image => image[pathSegments[i]],
                    WzImageProperty property => property[pathSegments[i]],
                    _ => null
                };
            }

            return current as WzImageProperty;
        }



        private static void LoadQuestDetailNoticeAnimation(
            WzSubProperty animationSource,
            GraphicsDevice device,
            out Texture2D[] frames,
            out int[] delays)
        {
            if (animationSource?.WzProperties == null || animationSource.WzProperties.Count == 0)
            {
                frames = Array.Empty<Texture2D>();
                delays = Array.Empty<int>();
                return;
            }


            List<Texture2D> loadedFrames = new();
            List<int> loadedDelays = new();
            for (int i = 0; i < animationSource.WzProperties.Count; i++)
            {
                if (animationSource.WzProperties[i] is not WzCanvasProperty canvas)
                {
                    continue;
                }


                Texture2D frame = canvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
                if (frame == null)
                {
                    continue;
                }


                loadedFrames.Add(frame);

                loadedDelays.Add(Math.Max(1, canvas[WzCanvasProperty.AnimationDelayPropertyName]?.GetInt() ?? 120));

            }



            frames = loadedFrames.ToArray();

            delays = loadedDelays.ToArray();

        }



        private static void LoadWorldMapQuestOverlayAnimation(
            WzSubProperty animationSource,
            GraphicsDevice device,
            out Texture2D[] frames,
            out Point[] origins,
            out int[] delays)
        {
            if (animationSource?.WzProperties == null || animationSource.WzProperties.Count == 0)
            {
                frames = Array.Empty<Texture2D>();
                origins = Array.Empty<Point>();
                delays = Array.Empty<int>();
                return;
            }


            List<Texture2D> loadedFrames = new();
            List<Point> loadedOrigins = new();
            List<int> loadedDelays = new();
            for (int i = 0; i < animationSource.WzProperties.Count; i++)
            {
                if (animationSource.WzProperties[i] is not WzCanvasProperty canvas)
                {
                    continue;
                }


                Texture2D frame = canvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
                if (frame == null)
                {
                    continue;
                }


                loadedFrames.Add(frame);
                loadedOrigins.Add(ResolveCanvasOffset(canvas, Point.Zero));
                loadedDelays.Add(Math.Max(1, canvas[WzCanvasProperty.AnimationDelayPropertyName]?.GetInt() ?? 120));
            }


            frames = loadedFrames.ToArray();
            origins = loadedOrigins.ToArray();
            delays = loadedDelays.ToArray();
        }


        private static UIObject CreateQuestDetailActionButton(
            WzSubProperty buttonProperty,
            WzBinaryProperty clickSound,
            WzBinaryProperty overSound,
            GraphicsDevice device)
        {
            if (buttonProperty == null)
            {
                return null;
            }


            try
            {
                return new UIObject(buttonProperty, clickSound, overSound, false, Point.Zero, device);
            }
            catch
            {
                return null;
            }
        }


        private static IReadOnlyList<Texture2D> LoadButtonAnimationTextures(WzSubProperty buttonProperty, GraphicsDevice device)
        {
            WzSubProperty animationProperty = buttonProperty?["ani"] as WzSubProperty;
            if (animationProperty == null)
            {
                return Array.Empty<Texture2D>();
            }


            var frames = new List<Texture2D>();
            for (int i = 0; ; i++)
            {
                if (animationProperty[i.ToString()] is not WzCanvasProperty canvas)
                {
                    break;
                }


                try
                {
                    System.Drawing.Bitmap bitmap = canvas.GetLinkedWzCanvasBitmap();
                    Texture2D texture = bitmap?.ToTexture2DAndDispose(device);
                    if (texture != null)
                    {
                        frames.Add(texture);
                    }
                }
                catch
                {
                    break;
                }
            }


            return frames;

        }



        private static UIObject CreateFallbackQuestDetailButton(GraphicsDevice device, int width, int height)
        {
            return UiButtonFactory.CreateSolidButton(
                device, width, height,
                new Color(69, 95, 122, 230),
                new Color(101, 131, 160, 240),
                new Color(82, 110, 140, 240),
                new Color(42, 42, 42, 170));
        }


        private static void PositionQuestDetailActionButton(UIObject button, int frameWidth, int frameHeight, int rightMargin, int slotOffset)
        {
            if (button == null)
            {
                return;
            }


            int buttonWidth = Math.Max(1, button.CanvasSnapshotWidth);
            int buttonHeight = Math.Max(1, button.CanvasSnapshotHeight);
            button.X = Math.Max(12, frameWidth - buttonWidth - rightMargin - slotOffset);
            button.Y = Math.Max(16, frameHeight - buttonHeight - 10);
        }


        private static QuickSlotUI CreateQuickSlotWindow(WzImage uiWindow2Image, GraphicsDevice device, int screenWidth, int screenHeight)
        {
            const int width = 286;
            const int height = 96;


            Texture2D frameTexture = new Texture2D(device, width, height);
            Color[] data = new Color[width * height];
            Color fill = new Color(18, 24, 34, 130);
            Color border = new Color(85, 98, 120, 180);


            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool isBorder = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                    data[(y * width) + x] = isBorder ? border : fill;
                }
            }


            frameTexture.SetData(data);



            IDXObject frame = new DXObject(0, 0, frameTexture, 0);

            QuickSlotUI quickSlot = new QuickSlotUI(frame, device);

            quickSlot.Position = new Point((screenWidth - width) / 2, Math.Max(20, screenHeight - height - 120));



            WzSubProperty skillProperty = uiWindow2Image?["Skill"] as WzSubProperty;
            WzSubProperty mainProperty = skillProperty?["main"] as WzSubProperty;
            WzSubProperty coolTimeProperty = mainProperty?["CoolTime"] as WzSubProperty;
            if (coolTimeProperty != null)
            {
                Texture2D[] cooldownMasks = new Texture2D[16];
                for (int i = 0; i < cooldownMasks.Length; i++)
                {
                    cooldownMasks[i] = LoadCanvasTexture(coolTimeProperty, i.ToString(), device);
                }


                quickSlot.SetCooldownMasks(cooldownMasks);

            }



            if (mainProperty != null)
            {
                Texture2D[] tooltipFrames = new Texture2D[3];
                tooltipFrames[0] = LoadCanvasTexture(mainProperty, "tip0", device);
                tooltipFrames[1] = LoadCanvasTexture(mainProperty, "tip1", device);
                tooltipFrames[2] = LoadCanvasTexture(mainProperty, "tip2", device);
                quickSlot.SetTooltipTextures(tooltipFrames);
            }

            WzSubProperty equipTooltipProperty = uiWindow2Image?["ToolTip"]?["Equip"] as WzSubProperty;
            if (equipTooltipProperty != null)
            {
                quickSlot.SetEquipTooltipAssets(new EquipUIBigBang.EquipTooltipAssets
                {
                    CanLabels = LoadCanvasTextureMap(equipTooltipProperty["Can"] as WzSubProperty, device),
                    CannotLabels = LoadCanvasTextureMap(equipTooltipProperty["Cannot"] as WzSubProperty, device),
                    PropertyLabels = LoadCanvasTextureMap(equipTooltipProperty["Property"] as WzSubProperty, device),
                    ItemCategoryLabels = LoadCanvasTextureMap(equipTooltipProperty["ItemCategory"] as WzSubProperty, device),
                    WeaponCategoryLabels = LoadCanvasTextureMap(equipTooltipProperty["WeaponCategory"] as WzSubProperty, device),
                    SpeedLabels = LoadCanvasTextureMap(equipTooltipProperty["Speed"] as WzSubProperty, device),
                    GrowthEnabledLabels = LoadCanvasTextureMap(equipTooltipProperty["GrowthEnabled"] as WzSubProperty, device),
                    GrowthDisabledLabels = LoadCanvasTextureMap(equipTooltipProperty["GrowthDisabled"] as WzSubProperty, device),
                    CashLabel = LoadCanvasTexture(equipTooltipProperty, "cash", device),
                    MesosLabel = LoadCanvasTexture(equipTooltipProperty, "mesos", device),
                    StarLabel = LoadCanvasTexture(equipTooltipProperty["Star"] as WzSubProperty, "Star", device)
                });
            }


            quickSlot.Show();

            return quickSlot;

        }



        private static MapTransferUI CreateMapTransferWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            WzSubProperty teleport3Property = uiWindow2Image?["Teleport3"] as WzSubProperty;
            WzSubProperty teleport2Property = uiWindow2Image?["Teleport2"] as WzSubProperty;
            WzSubProperty teleportProperty =

                teleport3Property ??

                teleport2Property ??
                uiWindow2Image?["Teleport"] as WzSubProperty ??
                uiWindow1Image?["Teleport"] as WzSubProperty;
            if (teleportProperty == null)
            {
                return null;
            }


            WzCanvasProperty backgroundProperty = teleportProperty["backgrnd"] as WzCanvasProperty;
            Texture2D frameTexture = backgroundProperty?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            if (frameTexture == null)
            {
                return null;
            }


            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;

            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;

            WzSubProperty fadeYesNoProperty = uiWindow2Image?["FadeYesNo"] as WzSubProperty;
            bool continentVariant = teleportProperty == teleport3Property || teleportProperty == teleport2Property;
            WzSubProperty scrollProperty = basicImage?["VScr9"] as WzSubProperty;
            WzSubProperty scrollEnabledProperty = scrollProperty?["enabled"] as WzSubProperty;
            WzSubProperty scrollDisabledProperty = scrollProperty?["disabled"] as WzSubProperty;
            IDXObject frame = new DXObject(0, 0, frameTexture, 0);
            MapTransferUI window = new MapTransferUI(
                frame,
                LoadWindowCanvasLayer(teleportProperty, "backgrnd2", device),
                LoadWindowCanvasLayer(teleportProperty, "backgrnd3", device),
                LoadCanvasTexture(teleportProperty, "select", device),
                LoadButton(teleportProperty, "BtRegister", btClickSound, btOverSound, device),
                LoadButton(teleportProperty, "BtDelete", btClickSound, btOverSound, device),
                LoadButton(teleportProperty, "BtMove", btClickSound, btOverSound, device),
                LoadButton(teleportProperty, "BtMap", btClickSound, btOverSound, device),
                LoadCanvasTexture(fadeYesNoProperty, "backgrnd7", device),
                LoadButton(fadeYesNoProperty, "BtOK", btClickSound, btOverSound, device),
                LoadButton(fadeYesNoProperty, "BtCancel", btClickSound, btOverSound, device),
                continentVariant ? 10 : 5,
                LoadCanvasTexture(scrollEnabledProperty, "prev0", device),
                LoadCanvasTexture(scrollEnabledProperty, "prev1", device),
                LoadCanvasTexture(scrollDisabledProperty, "prev", device),
                LoadCanvasTexture(scrollEnabledProperty, "next0", device),
                LoadCanvasTexture(scrollEnabledProperty, "next1", device),
                LoadCanvasTexture(scrollDisabledProperty, "next", device),
                LoadCanvasTexture(scrollEnabledProperty, "base", device),
                LoadCanvasTexture(scrollDisabledProperty, "base", device),
                LoadCanvasTexture(scrollEnabledProperty, "thumb0", device),
                LoadCanvasTexture(scrollEnabledProperty, "thumb1", device),
                device);


            window.Position = new Point(

                Math.Max(24, screenWidth - frameTexture.Width - 44),

                Math.Max(36, (screenHeight - frameTexture.Height) / 2));



            WzSubProperty closeButtonProperty = basicImage?["BtClose"] as WzSubProperty;
            if (closeButtonProperty != null)
            {
                try
                {
                    UIObject closeBtn = new UIObject(closeButtonProperty, btClickSound, btOverSound, false, Point.Zero, device);
                    closeBtn.X = frameTexture.Width - closeBtn.CanvasSnapshotWidth - 8;
                    closeBtn.Y = 8;
                    window.InitializeCloseButton(closeBtn);
                }
                catch
                {
                }
            }


            return window;

        }

        private static SoftKeyboardUI CreateSoftKeyboardWindow(WzImage uiWindowImage, GraphicsDevice device, int screenWidth, int screenHeight)
        {
            if (uiWindowImage == null || device == null)
            {
                return null;
            }

            WzCanvasProperty compactBackgroundCanvas = ResolveCanvasProperty(uiWindowImage, "SoftKeyboard/backgrnd/0/back");
            WzCanvasProperty expandedBackgroundCanvas = ResolveCanvasProperty(uiWindowImage, "SoftKeyboard/backgrnd/1/back");
            WzCanvasProperty capsLockNormalCanvas = ResolveCanvasProperty(uiWindowImage, "SoftKeyboard/funckey/capslock/normal/0");
            WzCanvasProperty capsLockPressedCanvas = ResolveCanvasProperty(uiWindowImage, "SoftKeyboard/funckey/capslock/pressed/0");
            WzCanvasProperty capsLockDisabledCanvas = ResolveCanvasProperty(uiWindowImage, "SoftKeyboard/funckey/capslock/disabled/0");
            WzCanvasProperty capsLockHoverCanvas = ResolveCanvasProperty(uiWindowImage, "SoftKeyboard/funckey/capslock/mouseOver/0");
            WzCanvasProperty leftShiftNormalCanvas = ResolveCanvasProperty(uiWindowImage, "SoftKeyboard/funckey/lshift/normal/0");
            WzCanvasProperty leftShiftPressedCanvas = ResolveCanvasProperty(uiWindowImage, "SoftKeyboard/funckey/lshift/pressed/0");
            WzCanvasProperty leftShiftDisabledCanvas = ResolveCanvasProperty(uiWindowImage, "SoftKeyboard/funckey/lshift/disabled/0");
            WzCanvasProperty leftShiftHoverCanvas = ResolveCanvasProperty(uiWindowImage, "SoftKeyboard/funckey/lshift/mouseOver/0");
            WzCanvasProperty rightShiftNormalCanvas = ResolveCanvasProperty(uiWindowImage, "SoftKeyboard/funckey/rshift/normal/0");
            WzCanvasProperty rightShiftPressedCanvas = ResolveCanvasProperty(uiWindowImage, "SoftKeyboard/funckey/rshift/pressed/0");
            WzCanvasProperty rightShiftDisabledCanvas = ResolveCanvasProperty(uiWindowImage, "SoftKeyboard/funckey/rshift/disabled/0");
            WzCanvasProperty rightShiftHoverCanvas = ResolveCanvasProperty(uiWindowImage, "SoftKeyboard/funckey/rshift/mouseOver/0");
            WzCanvasProperty backspaceNormalCanvas = ResolveCanvasProperty(uiWindowImage, "SoftKeyboard/funckey/backspace/normal/0");
            WzCanvasProperty backspacePressedCanvas = ResolveCanvasProperty(uiWindowImage, "SoftKeyboard/funckey/backspace/pressed/0");
            WzCanvasProperty backspaceDisabledCanvas = ResolveCanvasProperty(uiWindowImage, "SoftKeyboard/funckey/backspace/disabled/0");
            WzCanvasProperty backspaceHoverCanvas = ResolveCanvasProperty(uiWindowImage, "SoftKeyboard/funckey/backspace/mouseOver/0");
            WzCanvasProperty enterNormalCanvas = ResolveCanvasProperty(uiWindowImage, "SoftKeyboard/funckey/enter/normal/0");
            WzCanvasProperty enterPressedCanvas = ResolveCanvasProperty(uiWindowImage, "SoftKeyboard/funckey/enter/pressed/0");
            WzCanvasProperty enterDisabledCanvas = ResolveCanvasProperty(uiWindowImage, "SoftKeyboard/funckey/enter/disabled/0");
            WzCanvasProperty enterHoverCanvas = ResolveCanvasProperty(uiWindowImage, "SoftKeyboard/funckey/enter/mouseOver/0");
            WzCanvasProperty minNormalCanvas = ResolveCanvasProperty(uiWindowImage, "SoftKeyboard/Bt/0/BtMin/normal/0");
            WzCanvasProperty minPressedCanvas = ResolveCanvasProperty(uiWindowImage, "SoftKeyboard/Bt/0/BtMin/pressed/0");
            WzCanvasProperty minDisabledCanvas = ResolveCanvasProperty(uiWindowImage, "SoftKeyboard/Bt/0/BtMin/disabled/0");
            WzCanvasProperty minHoverCanvas = ResolveCanvasProperty(uiWindowImage, "SoftKeyboard/Bt/0/BtMin/mouseOver/0");
            WzCanvasProperty maxNormalCanvas = ResolveCanvasProperty(uiWindowImage, "SoftKeyboard/Bt/1/BtMax/normal/0");
            WzCanvasProperty maxPressedCanvas = ResolveCanvasProperty(uiWindowImage, "SoftKeyboard/Bt/1/BtMax/pressed/0");
            WzCanvasProperty maxDisabledCanvas = ResolveCanvasProperty(uiWindowImage, "SoftKeyboard/Bt/1/BtMax/disabled/0");
            WzCanvasProperty maxHoverCanvas = ResolveCanvasProperty(uiWindowImage, "SoftKeyboard/Bt/1/BtMax/mouseOver/0");
            WzCanvasProperty closeNormalCanvas = ResolveCanvasProperty(uiWindowImage, "SoftKeyboard/Bt/0/BtClose/normal/0");
            WzCanvasProperty closePressedCanvas = ResolveCanvasProperty(uiWindowImage, "SoftKeyboard/Bt/0/BtClose/pressed/0");
            WzCanvasProperty closeDisabledCanvas = ResolveCanvasProperty(uiWindowImage, "SoftKeyboard/Bt/0/BtClose/disabled/0");
            WzCanvasProperty closeHoverCanvas = ResolveCanvasProperty(uiWindowImage, "SoftKeyboard/Bt/0/BtClose/mouseOver/0");

            if (compactBackgroundCanvas == null
                || capsLockNormalCanvas == null
                || capsLockPressedCanvas == null
                || capsLockDisabledCanvas == null
                || capsLockHoverCanvas == null
                || leftShiftNormalCanvas == null
                || leftShiftPressedCanvas == null
                || leftShiftDisabledCanvas == null
                || leftShiftHoverCanvas == null
                || rightShiftNormalCanvas == null
                || rightShiftPressedCanvas == null
                || rightShiftDisabledCanvas == null
                || rightShiftHoverCanvas == null
                || backspaceNormalCanvas == null
                || backspacePressedCanvas == null
                || backspaceDisabledCanvas == null
                || backspaceHoverCanvas == null
                || enterNormalCanvas == null
                || enterPressedCanvas == null
                || enterDisabledCanvas == null
                || enterHoverCanvas == null
                || minNormalCanvas == null
                || minPressedCanvas == null
                || minDisabledCanvas == null
                || minHoverCanvas == null
                || maxNormalCanvas == null
                || maxPressedCanvas == null
                || maxDisabledCanvas == null
                || maxHoverCanvas == null
                || closeNormalCanvas == null
                || closePressedCanvas == null
                || closeDisabledCanvas == null
                || closeHoverCanvas == null)
            {
                return null;
            }

            Texture2D compactBackgroundTexture = compactBackgroundCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            Texture2D expandedBackgroundTexture = expandedBackgroundCanvas?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            Texture2D[][] keyTextures = new Texture2D[37][];
            for (int i = 0; i < keyTextures.Length; i++)
            {
                keyTextures[i] = new[]
                {
                    ResolveCanvasProperty(uiWindowImage, $"SoftKeyboard/key/{i}/normal/0")?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device),
                    ResolveCanvasProperty(uiWindowImage, $"SoftKeyboard/key/{i}/pressed/0")?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device),
                    ResolveCanvasProperty(uiWindowImage, $"SoftKeyboard/key/{i}/disabled/0")?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device),
                    ResolveCanvasProperty(uiWindowImage, $"SoftKeyboard/key/{i}/mouseOver/0")?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device)
                };
            }
            Texture2D[] capsLockTextures =
            {
                capsLockNormalCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device),
                capsLockPressedCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device),
                capsLockDisabledCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device),
                capsLockHoverCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device)
            };
            Texture2D[] leftShiftTextures =
            {
                leftShiftNormalCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device),
                leftShiftPressedCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device),
                leftShiftDisabledCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device),
                leftShiftHoverCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device)
            };
            Texture2D[] rightShiftTextures =
            {
                rightShiftNormalCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device),
                rightShiftPressedCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device),
                rightShiftDisabledCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device),
                rightShiftHoverCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device)
            };
            Texture2D[] backspaceTextures =
            {
                backspaceNormalCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device),
                backspacePressedCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device),
                backspaceDisabledCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device),
                backspaceHoverCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device)
            };
            Texture2D[] enterTextures =
            {
                enterNormalCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device),
                enterPressedCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device),
                enterDisabledCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device),
                enterHoverCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device)
            };
            Texture2D[] minButtonTextures =
            {
                minNormalCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device),
                minPressedCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device),
                minDisabledCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device),
                minHoverCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device)
            };
            Texture2D[] maxButtonTextures =
            {
                maxNormalCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device),
                maxPressedCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device),
                maxDisabledCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device),
                maxHoverCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device)
            };
            Texture2D[] closeButtonTextures =
            {
                closeNormalCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device),
                closePressedCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device),
                closeDisabledCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device),
                closeHoverCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device)
            };

            if (compactBackgroundTexture == null
                || keyTextures.Any(textureFamily => textureFamily == null || textureFamily.Any(texture => texture == null))
                || capsLockTextures.Any(texture => texture == null)
                || leftShiftTextures.Any(texture => texture == null)
                || rightShiftTextures.Any(texture => texture == null)
                || backspaceTextures.Any(texture => texture == null)
                || enterTextures.Any(texture => texture == null)
                || minButtonTextures.Any(texture => texture == null)
                || maxButtonTextures.Any(texture => texture == null)
                || closeButtonTextures.Any(texture => texture == null))
            {
                return null;
            }

            IDXObject frame = new DXObject(0, 0, compactBackgroundTexture, 0);
            return new SoftKeyboardUI(
                frame,
                compactBackgroundTexture,
                expandedBackgroundTexture,
                keyTextures,
                capsLockTextures,
                leftShiftTextures,
                rightShiftTextures,
                backspaceTextures,
                enterTextures,
                minButtonTextures,
                maxButtonTextures,
                closeButtonTextures,
                device,
                screenWidth,
                screenHeight);
        }


        private static TrunkUI CreateTrunkWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight,
            InventoryUI inventory,
            IStorageRuntime storageRuntime)
        {
            WzSubProperty trunkProperty = uiWindow2Image?["Trunk"] as WzSubProperty
                ?? uiWindow1Image?["Trunk"] as WzSubProperty;
            if (trunkProperty == null)
            {
                return null;
            }


            WzCanvasProperty backgroundProperty = trunkProperty["backgrnd"] as WzCanvasProperty;
            Texture2D frameTexture = backgroundProperty?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            if (frameTexture == null)
            {
                return null;
            }


            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            TrunkUI window = new TrunkUI(
                new DXObject(0, 0, frameTexture, 0),
                LoadWindowCanvasLayerWithOffset(trunkProperty, "backgrnd2", device, out Point foregroundOffset),
                foregroundOffset,
                LoadWindowCanvasLayerWithOffset(trunkProperty, "backgrnd3", device, out Point contentOffset),
                contentOffset,
                LoadCanvasTexture(trunkProperty, "select", device),
                LoadButton(trunkProperty, "BtGet", btClickSound, btOverSound, device),
                LoadButton(trunkProperty, "BtPut", btClickSound, btOverSound, device),
                LoadButton(trunkProperty, "BtSort", btClickSound, btOverSound, device),
                LoadButton(trunkProperty, "BtExit", btClickSound, btOverSound, device),
                LoadButton(trunkProperty, "BtOutCoin", btClickSound, btOverSound, device),
                LoadButton(trunkProperty, "BtInCoin", btClickSound, btOverSound, device),
                device)
            {
                Position = new Point(
                    Math.Max(24, (screenWidth - frameTexture.Width) / 2),
                    Math.Max(36, (screenHeight - frameTexture.Height) / 2))
            };


            window.InitializeTabs(
                LoadInventoryCanvasTabButton(trunkProperty, "0", btClickSound, btOverSound, device),
                LoadInventoryCanvasTabButton(trunkProperty, "1", btClickSound, btOverSound, device),
                LoadInventoryCanvasTabButton(trunkProperty, "2", btClickSound, btOverSound, device),
                LoadInventoryCanvasTabButton(trunkProperty, "3", btClickSound, btOverSound, device),
                LoadInventoryCanvasTabButton(trunkProperty, "4", btClickSound, btOverSound, device));
            WzSubProperty skillMainProperty = uiWindow2Image?["Skill"]?["main"] as WzSubProperty;
            if (skillMainProperty != null)
            {
                Texture2D[] tooltipFrames = new Texture2D[3];
                tooltipFrames[0] = LoadCanvasTexture(skillMainProperty, "tip0", device);
                tooltipFrames[1] = LoadCanvasTexture(skillMainProperty, "tip1", device);
                tooltipFrames[2] = LoadCanvasTexture(skillMainProperty, "tip2", device);
                window.SetTooltipTextures(tooltipFrames);
            }

            WzSubProperty equipTooltipProperty = uiWindow2Image?["ToolTip"]?["Equip"] as WzSubProperty;
            if (equipTooltipProperty != null)
            {
                window.SetEquipTooltipAssets(new EquipUIBigBang.EquipTooltipAssets
                {
                    CanLabels = LoadCanvasTextureMap(equipTooltipProperty["Can"] as WzSubProperty, device),
                    CannotLabels = LoadCanvasTextureMap(equipTooltipProperty["Cannot"] as WzSubProperty, device),
                    PropertyLabels = LoadCanvasTextureMap(equipTooltipProperty["Property"] as WzSubProperty, device),
                    ItemCategoryLabels = LoadCanvasTextureMap(equipTooltipProperty["ItemCategory"] as WzSubProperty, device),
                    WeaponCategoryLabels = LoadCanvasTextureMap(equipTooltipProperty["WeaponCategory"] as WzSubProperty, device),
                    SpeedLabels = LoadCanvasTextureMap(equipTooltipProperty["Speed"] as WzSubProperty, device),
                    GrowthEnabledLabels = LoadCanvasTextureMap(equipTooltipProperty["GrowthEnabled"] as WzSubProperty, device),
                    GrowthDisabledLabels = LoadCanvasTextureMap(equipTooltipProperty["GrowthDisabled"] as WzSubProperty, device),
                    CashLabel = LoadCanvasTexture(equipTooltipProperty, "cash", device),
                    MesosLabel = LoadCanvasTexture(equipTooltipProperty, "mesos", device),
                    StarLabel = LoadCanvasTexture(equipTooltipProperty["Star"] as WzSubProperty, "Star", device)
                });
            }

            window.SetInventory(inventory);
            window.SetStorageRuntime(storageRuntime);

            return window;

        }



        private static WorldMapUI CreateWorldMapWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            WzSubProperty worldMapProperty = uiWindow2Image?["WorldMap"] as WzSubProperty;
            if (worldMapProperty == null)
            {
                return null;
            }


            Texture2D frameTexture = LoadCanvasTexture(worldMapProperty["Border"] as WzSubProperty, "0", device);
            if (frameTexture == null)
            {
                return null;
            }


            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;

            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;



            WzSubProperty worldMapSearchProperty = worldMapProperty["WorldMapSearch"] as WzSubProperty;
            Texture2D sidePanelTexture = LoadCanvasTexture(worldMapSearchProperty, "backgrnd", device);
            Point sidePanelOffset = ResolveCanvasOffset(worldMapSearchProperty, "backgrnd", new Point(507, 0));
            Texture2D searchNoticeTexture = LoadCanvasTexture(worldMapSearchProperty, "notice", device);
            Point searchNoticeOffset = ResolveCanvasOffset(worldMapSearchProperty, "notice", new Point(535, 220));


            Texture2D selectionTexture = new Texture2D(device, 1, 1);

            selectionTexture.SetData(new[] { Color.White });



            List<(string regionCode, UIObject button)> regionButtons = new List<(string, UIObject)>();
            WzSubProperty anotherWorldProperty = worldMapProperty["BtAnother"]?["AnotherWorld"] as WzSubProperty;
            foreach (WzImageProperty property in anotherWorldProperty?.WzProperties ?? Enumerable.Empty<WzImageProperty>())
            {
                if (!property.Name.StartsWith("Map", StringComparison.Ordinal))
                {
                    continue;
                }


                UIObject button = LoadButton(anotherWorldProperty, property.Name, clickSound, overSound, device);
                if (button == null)
                {
                    continue;
                }


                regionButtons.Add((property.Name.Substring(3), button));

            }



            WzSubProperty resultFieldProperty = worldMapSearchProperty?["resultField"] as WzSubProperty;
            WzSubProperty resultNpcProperty = worldMapSearchProperty?["resultNpc"] as WzSubProperty;
            WzSubProperty resultMobProperty = worldMapSearchProperty?["resultMob"] as WzSubProperty;
            LoadWorldMapQuestOverlayAnimation(
                uiWindow2Image?["QuestGuide"]?["QuestMark"] as WzSubProperty,
                device,
                out Texture2D[] overlayMarkerTextures,
                out Point[] overlayMarkerOrigins,
                out int[] overlayMarkerDelays);
            Dictionary<WorldMapUI.SearchResultKind, WorldMapUI.SearchResultVisualStyle> resultStyles = new Dictionary<WorldMapUI.SearchResultKind, WorldMapUI.SearchResultVisualStyle>
            {
                [WorldMapUI.SearchResultKind.Field] = new WorldMapUI.SearchResultVisualStyle(
                    LoadCanvasTexture(resultFieldProperty, "mouseOverBase", device),
                    ResolveCanvasOffset(resultFieldProperty, "mouseOverBase", Point.Zero),
                    LoadCanvasTexture(resultFieldProperty, "icon", device),
                    ResolveCanvasOffset(resultFieldProperty, "icon", Point.Zero)),
                [WorldMapUI.SearchResultKind.Npc] = new WorldMapUI.SearchResultVisualStyle(
                    LoadCanvasTexture(resultNpcProperty, "mouseOverBase", device),
                    ResolveCanvasOffset(resultNpcProperty, "mouseOverBase", Point.Zero),
                    LoadCanvasTexture(resultNpcProperty, "icon", device),
                    ResolveCanvasOffset(resultNpcProperty, "icon", Point.Zero)),
                [WorldMapUI.SearchResultKind.Mob] = new WorldMapUI.SearchResultVisualStyle(
                    LoadCanvasTexture(resultMobProperty, "mouseOverBase", device),
                    ResolveCanvasOffset(resultMobProperty, "mouseOverBase", Point.Zero),
                    LoadCanvasTexture(resultMobProperty, "icon", device),
                    ResolveCanvasOffset(resultMobProperty, "icon", Point.Zero))
            };


            WorldMapUI window = new WorldMapUI(
                new DXObject(0, 0, frameTexture, 0),
                LoadCanvasTexture(worldMapProperty, "title", device),
                sidePanelTexture,
                sidePanelOffset,
                searchNoticeTexture,
                searchNoticeOffset,
                selectionTexture,
                overlayMarkerTextures,
                overlayMarkerOrigins,
                overlayMarkerDelays,
                LoadButton(worldMapProperty, "BtAll", clickSound, overSound, device),
                LoadButton(worldMapProperty, "BtAnother", clickSound, overSound, device),
                LoadButton(worldMapProperty, "BtSearch", clickSound, overSound, device),
                LoadButton(worldMapSearchProperty, "BtAllsearch", clickSound, overSound, device),
                LoadButton(worldMapSearchProperty, "BtLevelMob", clickSound, overSound, device),
                LoadButton(worldMapProperty, "BtBefore", clickSound, overSound, device),
                LoadButton(worldMapProperty, "BtNext", clickSound, overSound, device),
                resultStyles,
                regionButtons,
                device)
            {
                Position = new Point(
                    Math.Max(12, (screenWidth - frameTexture.Width) / 2),
                    Math.Max(12, (screenHeight - frameTexture.Height) / 2))
            };
            window.InitializeQuestGuideButtons(
                LoadButton(uiWindow2Image?["QuestGuide"]?["Button"] as WzSubProperty, "Location", clickSound, overSound, device),
                LoadButton(uiWindow2Image?["QuestGuide"]?["Button"] as WzSubProperty, "WorldMapQuestToggle", clickSound, overSound, device));


            UIObject closeButton = CreateUserInfoCloseButton(basicImage, clickSound, overSound, device, frameTexture.Width);
            if (closeButton != null)
            {
                window.InitializeCloseButton(closeButton);
            }


            return window;

        }

        private static WzCanvasProperty ResolveCanvasProperty(WzImage image, string path)
        {
            if (image == null || string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            WzImageProperty current = null;
            string[] segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < segments.Length; i++)
            {
                current = i == 0
                    ? image[segments[i]]
                    : current?[segments[i]];

                if (current == null)
                {
                    return null;
                }
            }

            WzImageProperty resolved = WzInfoTools.GetRealProperty(current);
            if (resolved is WzCanvasProperty canvas)
            {
                return canvas;
            }

            return resolved?.WzProperties?.OfType<WzCanvasProperty>().FirstOrDefault();
        }


        private static Texture2D LoadQuestIcon(WzSubProperty questIconProperty, string iconNum, GraphicsDevice device)
        {
            WzSubProperty iconSub = (WzSubProperty)questIconProperty[iconNum];
            if (iconSub != null)
            {
                WzCanvasProperty canvas = (WzCanvasProperty)iconSub["0"];
                if (canvas != null)
                {
                    return canvas.GetLinkedWzCanvasBitmap().ToTexture2DAndDispose(device);
                }
            }
            return null;
        }


        private static UserInfoUI CreateCharacterInfoWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight,
            bool isBigBang)
        {
            if (isBigBang)
            {
                UserInfoUI bigBangWindow = CreateCharacterInfoWindowBigBang(
                    uiWindow2Image,
                    basicImage,
                    soundUIImage,
                    device,
                    screenWidth,
                    screenHeight);
                if (bigBangWindow != null)
                {
                    return bigBangWindow;
                }
            }


            return CreateCharacterInfoWindowPreBigBang(
                uiWindow1Image,
                basicImage,
                soundUIImage,
                device,
                screenWidth,
                screenHeight);
        }


        private static UserInfoUI CreateCharacterInfoWindowPreBigBang(
            WzImage uiWindowImage,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            WzSubProperty userInfoProperty = uiWindowImage?["UserInfo"] as WzSubProperty;
            WzCanvasProperty backgroundProperty = userInfoProperty?["backgrnd"] as WzCanvasProperty;
            Texture2D frameTexture = backgroundProperty?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            if (frameTexture == null)
            {
                return null;
            }


            UserInfoUI window = new UserInfoUI(new DXObject(0, 0, frameTexture, 0), false)
            {
                Position = new Point(
                    Math.Max(40, (screenWidth / 2) - (frameTexture.Width / 2)),
                    Math.Max(40, (screenHeight / 2) - (frameTexture.Height / 2)))
            };


            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            window.InitializeCloseButton(CreateUserInfoCloseButton(basicImage, clickSound, overSound, device, frameTexture.Width));
            window.InitializePrimaryButtons(
                LoadButton(userInfoProperty, "BtParty", clickSound, overSound, device),
                LoadButton(userInfoProperty, "BtTrade", clickSound, overSound, device),
                LoadButton(userInfoProperty, "BtItem", clickSound, overSound, device),
                LoadButton(userInfoProperty, "BtWish", clickSound, overSound, device),
                LoadButton(userInfoProperty, "BtFamily", clickSound, overSound, device));
            return window;
        }


        private static UserInfoUI CreateCharacterInfoWindowBigBang(
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            WzSubProperty userInfoProperty = uiWindow2Image?["UserInfo"] as WzSubProperty;
            WzSubProperty characterProperty = userInfoProperty?["character"] as WzSubProperty;
            WzCanvasProperty backgroundProperty = characterProperty?["backgrnd"] as WzCanvasProperty;
            Texture2D frameTexture = backgroundProperty?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            if (frameTexture == null)
            {
                return null;
            }


            UserInfoUI window = new UserInfoUI(new DXObject(0, 0, frameTexture, 0), true)
            {
                Position = new Point(
                    Math.Max(40, (screenWidth / 2) - (frameTexture.Width / 2)),
                    Math.Max(40, (screenHeight / 2) - (frameTexture.Height / 2)))
            };


            Texture2D foregroundTexture = LoadCanvasTexture(characterProperty, "backgrnd2", device);
            if (foregroundTexture != null)
            {
                IDXObject foreground = new DXObject(0, 0, foregroundTexture, 0);
                Point foregroundOffset = ResolveCanvasOffset(characterProperty, "backgrnd2", new Point(6, 23));
                window.SetForeground(foreground, foregroundOffset.X, foregroundOffset.Y);
            }


            Texture2D nameBannerTexture = LoadCanvasTexture(characterProperty, "backgrnd3", device);
            if (nameBannerTexture != null)
            {
                IDXObject nameBanner = new DXObject(0, 0, nameBannerTexture, 0);
                Point bannerOffset = ResolveCanvasOffset(characterProperty, "backgrnd3", new Point(14, 151));
                window.SetNameBanner(nameBanner, bannerOffset.X, bannerOffset.Y);
            }


            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            window.InitializeCloseButton(CreateUserInfoCloseButton(basicImage, clickSound, overSound, device, frameTexture.Width));
            window.InitializePrimaryButtons(
                LoadButton(characterProperty, "BtParty", clickSound, overSound, device),
                LoadButton(characterProperty, "BtTrad", clickSound, overSound, device),
                LoadButton(characterProperty, "BtItem", clickSound, overSound, device),
                LoadButton(characterProperty, "BtWish", clickSound, overSound, device),
                LoadButton(characterProperty, "BtFamily", clickSound, overSound, device));
            window.InitializePageButtons(
                LoadButton(characterProperty, "BtRide", clickSound, overSound, device),
                LoadButton(characterProperty, "BtPet", clickSound, overSound, device),
                LoadButton(characterProperty, "BtCollect", clickSound, overSound, device),
                LoadButton(characterProperty, "BtPersonality", clickSound, overSound, device));
            window.InitializePageActionButtons(
                LoadButton(userInfoProperty?["pet"] as WzSubProperty, "BtException", clickSound, overSound, device),
                LoadButton(userInfoProperty?["collect"] as WzSubProperty, "BtArrayName", clickSound, overSound, device),
                LoadButton(userInfoProperty?["collect"] as WzSubProperty, "BtArrayGet", clickSound, overSound, device));


            RegisterUserInfoSubPage(window, "ride", userInfoProperty?["ride"] as WzSubProperty, device);
            RegisterUserInfoSubPage(window, "pet", userInfoProperty?["pet"] as WzSubProperty, device);
            RegisterUserInfoSubPage(window, "collect", userInfoProperty?["collect"] as WzSubProperty, device);
            RegisterUserInfoSubPage(window, "personality", userInfoProperty?["personality"] as WzSubProperty, device);
            RegisterUserInfoExceptionPopup(window, userInfoProperty?["exception"] as WzSubProperty, clickSound, overSound, device);
            RegisterUserInfoItemPopup(window, userInfoProperty?["item"] as WzSubProperty, device);
            RegisterUserInfoWishPopup(window, userInfoProperty?["wish"] as WzSubProperty, clickSound, overSound, device);
            RegisterUserInfoCharacterControls(window, characterProperty, device, clickSound, overSound);
            RegisterUserInfoPersonalityTooltip(window, userInfoProperty?["personality"] as WzSubProperty, device);
            return window;
        }


        private static void RegisterUserInfoSubPage(UserInfoUI window, string pageName, WzSubProperty pageProperty, GraphicsDevice device)
        {
            if (window == null || pageProperty == null)
            {
                return;
            }


            Texture2D frameTexture = LoadCanvasTexture(pageProperty, "backgrnd", device);
            if (frameTexture != null)
            {
                window.RegisterPageFrame(pageName, new DXObject(0, 0, frameTexture, 0));
            }


            foreach (WzCanvasProperty canvas in pageProperty.WzProperties.OfType<WzCanvasProperty>())
            {
                if (string.Equals(canvas.Name, "backgrnd", StringComparison.Ordinal))
                {
                    continue;
                }


                Texture2D layerTexture = canvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
                if (layerTexture == null)
                {
                    continue;
                }


                Point offset = ResolveCanvasOffset(canvas, Point.Zero);
                if (string.Equals(pageName, "collect", StringComparison.Ordinal) && string.Equals(canvas.Name, "icon1", StringComparison.Ordinal))
                {
                    window.SetPageIcon(pageName, new DXObject(0, 0, layerTexture, 0), offset.X, offset.Y);
                    continue;
                }


                window.AddPageLayer(pageName, new DXObject(0, 0, layerTexture, 0), offset.X, offset.Y);

            }

        }



        private static void RegisterUserInfoExceptionPopup(
            UserInfoUI window,
            WzSubProperty exceptionProperty,
            WzBinaryProperty clickSound,
            WzBinaryProperty overSound,
            GraphicsDevice device)
        {
            if (window == null || exceptionProperty == null)
            {
                return;
            }


            Texture2D frameTexture = LoadCanvasTexture(exceptionProperty, "backgrnd", device);

            IDXObject frame = frameTexture != null ? new DXObject(0, 0, frameTexture, 0) : null;

            List<(IDXObject layer, Point offset)> layers = new List<(IDXObject, Point)>();



            foreach (WzCanvasProperty canvas in exceptionProperty.WzProperties.OfType<WzCanvasProperty>())
            {
                if (string.Equals(canvas.Name, "backgrnd", StringComparison.Ordinal))
                {
                    continue;
                }


                Texture2D layerTexture = canvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
                if (layerTexture == null)
                {
                    continue;
                }


                layers.Add((new DXObject(0, 0, layerTexture, 0), ResolveCanvasOffset(canvas, Point.Zero)));

            }



            window.InitializeExceptionPopup(
                frame,
                layers,
                LoadButton(exceptionProperty, "BtRegist", clickSound, overSound, device),
                LoadButton(exceptionProperty, "BtDelete", clickSound, overSound, device),
                LoadButton(exceptionProperty, "BtMeso", clickSound, overSound, device));
        }


        private static void RegisterUserInfoItemPopup(
            UserInfoUI window,
            WzSubProperty itemProperty,
            GraphicsDevice device)
        {
            if (window == null || itemProperty == null)
            {
                return;
            }


            window.InitializeItemPopup(
                CreateUserInfoPopupFrame(itemProperty, device),
                CreateUserInfoPopupLayers(itemProperty, device));
        }


        private static void RegisterUserInfoWishPopup(
            UserInfoUI window,
            WzSubProperty wishProperty,
            WzBinaryProperty clickSound,
            WzBinaryProperty overSound,
            GraphicsDevice device)
        {
            if (window == null || wishProperty == null)
            {
                return;
            }


            window.InitializeWishPopup(
                CreateUserInfoPopupFrame(wishProperty, device),
                CreateUserInfoPopupLayers(wishProperty, device),
                LoadButton(wishProperty, "BtPresent", clickSound, overSound, device));
        }


        private static void RegisterUserInfoCharacterControls(
            UserInfoUI window,
            WzSubProperty characterProperty,
            GraphicsDevice device,
            WzBinaryProperty clickSound,
            WzBinaryProperty overSound)
        {
            if (window == null || characterProperty == null)
            {
                return;
            }


            window.InitializePopupScrollButtons(

                LoadButton(characterProperty, "BtPopUp", clickSound, overSound, device),

                LoadButton(characterProperty, "BtPopDown", clickSound, overSound, device));



            WzSubProperty productSkillProperty = characterProperty["productSkill"] as WzSubProperty;
            RegisterUserInfoProductSkillIcon(window, productSkillProperty, "9200", ItemMakerRecipeFamily.Generic, device);
            RegisterUserInfoProductSkillIcon(window, productSkillProperty, "9201", ItemMakerRecipeFamily.Gloves, device);
            RegisterUserInfoProductSkillIcon(window, productSkillProperty, "9202", ItemMakerRecipeFamily.Shoes, device);
            RegisterUserInfoProductSkillIcon(window, productSkillProperty, "9203", ItemMakerRecipeFamily.Toys, device);
            RegisterUserInfoProductSkillRecipeIcon(window, productSkillProperty, "9204", device);
        }


        private static void RegisterUserInfoProductSkillIcon(
            UserInfoUI window,
            WzSubProperty productSkillProperty,
            string canvasName,
            ItemMakerRecipeFamily family,
            GraphicsDevice device)
        {
            if (window == null || productSkillProperty == null)
            {
                return;
            }


            Texture2D iconTexture = LoadCanvasTexture(productSkillProperty, canvasName, device);
            if (iconTexture != null)
            {
                window.SetProductSkillIcon(family, new DXObject(0, 0, iconTexture, 0));
            }
        }

        private static void RegisterUserInfoProductSkillRecipeIcon(
            UserInfoUI window,
            WzSubProperty productSkillProperty,
            string canvasName,
            GraphicsDevice device)
        {
            if (window == null || productSkillProperty == null)
            {
                return;
            }

            Texture2D iconTexture = LoadCanvasTexture(productSkillProperty, canvasName, device);
            if (iconTexture != null)
            {
                window.SetProductSkillRecipeIcon(new DXObject(0, 0, iconTexture, 0));
            }
        }


        private static void RegisterUserInfoPersonalityTooltip(UserInfoUI window, WzSubProperty personalityProperty, GraphicsDevice device)
        {
            WzSubProperty tooltipProperty = personalityProperty?["Tooltip"] as WzSubProperty;
            if (window == null || tooltipProperty == null)
            {
                return;
            }


            IDXObject baseTop = CreateUserInfoPopupTexture(tooltipProperty, "base", device);

            IDXObject baseMiddle = CreateUserInfoPopupTexture(tooltipProperty, "base2", device);

            IDXObject baseBottom = CreateUserInfoPopupTexture(tooltipProperty, "base3", device);

            IDXObject title = CreateUserInfoPopupTexture(tooltipProperty, "title", device);
            IDXObject charmCollectionBody = CreateUserInfoPopupTexture(tooltipProperty?["charm"]?["collection"] as WzSubProperty, "0", device);
            Dictionary<string, IDXObject> bodies = new Dictionary<string, IDXObject>(StringComparer.OrdinalIgnoreCase)
            {
                ["charisma"] = CreateUserInfoPopupTexture(tooltipProperty["charisma"] as WzSubProperty, "0", device),
                ["insight"] = CreateUserInfoPopupTexture(tooltipProperty["insight"] as WzSubProperty, "0", device),
                ["will"] = CreateUserInfoPopupTexture(tooltipProperty["will"] as WzSubProperty, "0", device),
                ["craft"] = CreateUserInfoPopupTexture(tooltipProperty["craft"] as WzSubProperty, "0", device),
                ["sense"] = CreateUserInfoPopupTexture(tooltipProperty["sense"] as WzSubProperty, "0", device),
                ["charm"] = CreateUserInfoPopupTexture(tooltipProperty["charm"] as WzSubProperty, "0", device)
            };
            Dictionary<char, IDXObject> numberGlyphs = new Dictionary<char, IDXObject>();
            if (tooltipProperty["number"] is WzSubProperty numberProperty)
            {
                foreach (WzCanvasProperty glyphCanvas in numberProperty.WzProperties.OfType<WzCanvasProperty>())
                {
                    if (string.IsNullOrEmpty(glyphCanvas.Name) || glyphCanvas.Name.Length != 1)
                    {
                        continue;
                    }

                    Texture2D glyphTexture = glyphCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
                    if (glyphTexture == null)
                    {
                        continue;
                    }

                    numberGlyphs[glyphCanvas.Name[0]] = new DXObject(0, 0, glyphTexture, 0);
                }
            }

            window.InitializePersonalityTooltip(baseTop, baseMiddle, baseBottom, title, bodies, charmCollectionBody, numberGlyphs);
        }



        private static IDXObject CreateUserInfoPopupTexture(WzObject parent, string canvasName, GraphicsDevice device)
        {
            WzCanvasProperty canvas = parent switch
            {
                WzSubProperty subProperty => subProperty[canvasName] as WzCanvasProperty,
                WzCanvasProperty directCanvas when string.Equals(directCanvas.Name, canvasName, StringComparison.Ordinal) => directCanvas,
                _ => null
            };
            Texture2D texture = canvas?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            return texture != null ? new DXObject(0, 0, texture, 0) : null;
        }


        private static IDXObject CreateUserInfoPopupFrame(WzSubProperty popupProperty, GraphicsDevice device)
        {
            Texture2D frameTexture = LoadCanvasTexture(popupProperty, "backgrnd", device);
            return frameTexture != null ? new DXObject(0, 0, frameTexture, 0) : null;
        }


        private static IEnumerable<(IDXObject layer, Point offset)> CreateUserInfoPopupLayers(WzSubProperty popupProperty, GraphicsDevice device)
        {
            if (popupProperty == null)
            {
                yield break;
            }


            foreach (WzCanvasProperty canvas in popupProperty.WzProperties.OfType<WzCanvasProperty>())
            {
                if (string.Equals(canvas.Name, "backgrnd", StringComparison.Ordinal))
                {
                    continue;
                }


                Texture2D layerTexture = canvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
                if (layerTexture == null)
                {
                    continue;
                }


                yield return (new DXObject(0, 0, layerTexture, 0), ResolveCanvasOffset(canvas, Point.Zero));

            }

        }



        private static UIObject CreateUserInfoCloseButton(
            WzImage basicImage,
            WzBinaryProperty clickSound,
            WzBinaryProperty overSound,
            GraphicsDevice device,
            int windowWidth)
        {
            WzSubProperty closeButtonProperty = basicImage?["BtClose"] as WzSubProperty;
            if (closeButtonProperty == null)
            {
                return null;
            }


            try
            {
                UIObject closeButton = new UIObject(closeButtonProperty, clickSound, overSound, false, Point.Zero, device);
                closeButton.X = windowWidth - closeButton.CanvasSnapshotWidth - 8;
                closeButton.Y = 7;
                return closeButton;
            }
            catch
            {
                return null;
            }
        }


        private static UIObject CreateTextureButton(Texture2D normalTexture, Texture2D pressedTexture)
        {
            if (normalTexture == null)
            {
                return null;
            }


            BaseDXDrawableItem normal = new BaseDXDrawableItem(new DXObject(0, 0, normalTexture, 0), false);
            BaseDXDrawableItem disabled = new BaseDXDrawableItem(new DXObject(0, 0, normalTexture, 0), false);
            Texture2D activeTexture = pressedTexture ?? normalTexture;
            BaseDXDrawableItem pressed = new BaseDXDrawableItem(new DXObject(0, 0, activeTexture, 0), false);
            BaseDXDrawableItem mouseOver = new BaseDXDrawableItem(new DXObject(0, 0, activeTexture, 0), false);
            return new UIObject(normal, disabled, pressed, mouseOver);
        }


        private static UIObject CreateTextureButton(
            Texture2D normalTexture,
            Texture2D disabledTexture,
            Texture2D pressedTexture,
            Texture2D mouseOverTexture)
        {
            if (normalTexture == null)
            {
                return null;
            }
            BaseDXDrawableItem normal = new BaseDXDrawableItem(new DXObject(0, 0, normalTexture, 0), false);
            BaseDXDrawableItem disabled = new BaseDXDrawableItem(new DXObject(0, 0, disabledTexture ?? normalTexture, 0), false);
            BaseDXDrawableItem pressed = new BaseDXDrawableItem(new DXObject(0, 0, pressedTexture ?? normalTexture, 0), false);
            BaseDXDrawableItem mouseOver = new BaseDXDrawableItem(new DXObject(0, 0, mouseOverTexture ?? pressedTexture ?? normalTexture, 0), false);
            return new UIObject(normal, disabled, pressed, mouseOver);

        }

        private static UIObject CreateCanvasButton(WzCanvasProperty normalCanvas, WzCanvasProperty pressedCanvas, GraphicsDevice device)
        {
            if (normalCanvas == null)
            {
                return null;
            }


            try
            {
                Texture2D normalTexture = normalCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
                Texture2D pressedTexture = (pressedCanvas ?? normalCanvas).GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device) ?? normalTexture;
                if (normalTexture == null || pressedTexture == null)
                {
                    return null;
                }


                Point normalOffset = ResolveCanvasOffset(normalCanvas, Point.Zero);
                Point pressedOffset = ResolveCanvasOffset(pressedCanvas ?? normalCanvas, normalOffset);
                BaseDXDrawableItem normal = new BaseDXDrawableItem(new DXObject(normalOffset.X, normalOffset.Y, normalTexture, 0), false);
                BaseDXDrawableItem disabled = new BaseDXDrawableItem(new DXObject(normalOffset.X, normalOffset.Y, normalTexture, 0), false);
                BaseDXDrawableItem pressed = new BaseDXDrawableItem(new DXObject(pressedOffset.X, pressedOffset.Y, pressedTexture, 0), false);
                BaseDXDrawableItem mouseOver = new BaseDXDrawableItem(new DXObject(pressedOffset.X, pressedOffset.Y, pressedTexture, 0), false);
                UIObject button = new UIObject(normal, disabled, pressed, mouseOver);
                button.X = normalOffset.X;
                button.Y = normalOffset.Y;
                return button;
            }
            catch
            {
                return null;
            }
        }


        private static Point GetCanvasOffset(WzCanvasProperty canvas)
        {
            System.Drawing.PointF? origin = canvas?.GetCanvasOriginPosition();
            return origin.HasValue
                ? new Point(-(int)origin.Value.X, -(int)origin.Value.Y)
                : Point.Zero;
        }


        private static Texture2D CreateSolidTexture(GraphicsDevice device, Color color)
        {
            Texture2D texture = new Texture2D(device, 1, 1);
            texture.SetData(new[] { color });
            return texture;
        }


        private static Texture2D CreateFilledTexture(GraphicsDevice device, int width, int height, Color fillColor, Color borderColor)
        {
            Texture2D texture = new Texture2D(device, width, height);
            Color[] data = new Color[width * height];


            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool isBorder = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                    data[(y * width) + x] = isBorder ? borderColor : fillColor;
                }
            }


            texture.SetData(data);
            return texture;
        }
        #endregion


    }

}

