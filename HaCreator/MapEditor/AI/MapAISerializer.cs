/* Copyright (C) 2024 HaCreator AI Extension
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HaCreator.Collections;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.Wz;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;

namespace HaCreator.MapEditor.AI
{
    /// <summary>
    /// Serializes a map Board into a human-readable text format that can be understood and modified by an AI/LLM.
    /// The format is designed to be:
    /// - Easy for AI to parse and understand
    /// - Easy for AI to generate modification instructions
    /// - Comprehensive enough to represent all map elements
    /// </summary>
    public class MapAISerializer
    {
        private readonly Board board;
        private readonly StringBuilder sb;
        private int indentLevel = 0;

        public MapAISerializer(Board board)
        {
            this.board = board;
            this.sb = new StringBuilder();
        }

        // ASCII map configuration
        private const int ASCII_MAP_WIDTH = 80;  // Characters wide
        private const int ASCII_MAP_HEIGHT = 30; // Characters tall (max)

        /// <summary>
        /// Generate a compact summary for AI context (shorter than full serialize)
        /// </summary>
        public string GenerateAISummary()
        {
            sb.Clear();
            indentLevel = 0;

            WriteLine("# Map Summary for AI Editing");
            WriteLine($"Map: \"{board.MapInfo.strMapName}\" (ID: {board.MapInfo.id})");
            WriteLine($"Size: {board.MapSize.X}x{board.MapSize.Y}, Center: ({board.CenterPoint.X}, {board.CenterPoint.Y})");

            // Calculate actual content bounds from existing elements
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            bool hasElements = false;

            foreach (var portal in board.BoardItems.Portals)
            {
                minX = Math.Min(minX, portal.X); maxX = Math.Max(maxX, portal.X);
                minY = Math.Min(minY, portal.Y); maxY = Math.Max(maxY, portal.Y);
                hasElements = true;
            }
            foreach (var fh in board.BoardItems.FootholdLines)
            {
                minX = Math.Min(minX, Math.Min(fh.FirstDot.X, fh.SecondDot.X));
                maxX = Math.Max(maxX, Math.Max(fh.FirstDot.X, fh.SecondDot.X));
                minY = Math.Min(minY, Math.Min(fh.FirstDot.Y, fh.SecondDot.Y));
                maxY = Math.Max(maxY, Math.Max(fh.FirstDot.Y, fh.SecondDot.Y));
                hasElements = true;
            }
            foreach (var mob in board.BoardItems.Mobs)
            {
                minX = Math.Min(minX, mob.X); maxX = Math.Max(maxX, mob.X);
                minY = Math.Min(minY, mob.Y); maxY = Math.Max(maxY, mob.Y);
                hasElements = true;
            }
            foreach (var npc in board.BoardItems.NPCs)
            {
                minX = Math.Min(minX, npc.X); maxX = Math.Max(maxX, npc.X);
                minY = Math.Min(minY, npc.Y); maxY = Math.Max(maxY, npc.Y);
                hasElements = true;
            }

            if (hasElements)
            {
                WriteLine($"Content Bounds: X=[{minX} to {maxX}], Y=[{minY} to {maxY}]");
                WriteLine($"** IMPORTANT: Place new elements within these bounds! **");
            }
            WriteLine();

            // Generate ASCII map visualization
            if (hasElements)
            {
                GenerateAsciiMapVisualization(minX, maxX, minY, maxY);
            }

            // Quick counts
            WriteLine("## Element Counts");
            WriteLine($"- Tiles & Objects: {board.BoardItems.TileObjs.Count}");
            WriteLine($"- Mobs: {board.BoardItems.Mobs.Count}");
            WriteLine($"- NPCs: {board.BoardItems.NPCs.Count}");
            WriteLine($"- Portals: {board.BoardItems.Portals.Count}");
            WriteLine($"- Backgrounds: {board.BoardItems.BackBackgrounds.Count + board.BoardItems.FrontBackgrounds.Count}");
            WriteLine($"- Footholds: {board.BoardItems.FootholdLines.Count}");
            WriteLine($"- Ropes/Ladders: {board.BoardItems.RopeLines.Count}");
            WriteLine($"- Chairs: {board.BoardItems.Chairs.Count}");
            WriteLine($"- Reactors: {board.BoardItems.Reactors.Count}");
            WriteLine();

            // Map Settings
            SerializeMapSettings();

            // Portals with full details
            if (board.BoardItems.Portals.Count > 0)
            {
                WriteLine("## Portals");
                foreach (var portal in board.BoardItems.Portals.OrderBy(p => p.pn))
                {
                    var props = new List<string>();
                    props.Add($"pos=({portal.X}, {portal.Y})");
                    props.Add($"type={portal.pt}");
                    if (portal.tm != 999999999)
                    {
                        props.Add($"targetMap={portal.tm}");
                        if (!string.IsNullOrEmpty(portal.tn))
                            props.Add($"targetPortal=\"{portal.tn}\"");
                    }
                    if (!string.IsNullOrEmpty(portal.script))
                        props.Add($"script=\"{portal.script}\"");
                    if (portal.delay.HasValue)
                        props.Add($"delay={portal.delay.Value}ms");
                    if (portal.hideTooltip == true)
                        props.Add("hideTooltip");
                    if (portal.onlyOnce == true)
                        props.Add("onlyOnce");
                    if (portal.hRange.HasValue || portal.vRange.HasValue)
                        props.Add($"range=[h={portal.hRange ?? 0}, v={portal.vRange ?? 0}]");
                    if (portal.horizontalImpact.HasValue || portal.verticalImpact.HasValue)
                        props.Add($"impact=[h={portal.horizontalImpact ?? 0}, v={portal.verticalImpact ?? 0}]");
                    if (!string.IsNullOrEmpty(portal.image))
                        props.Add($"image=\"{portal.image}\"");
                    WriteLine($"- Portal \"{portal.pn}\": {string.Join(", ", props)}");
                }
                WriteLine();
            }

            // Mob summary with detailed spawn info
            if (board.BoardItems.Mobs.Count > 0)
            {
                WriteLine("## Mobs");
                var mobGroups = board.BoardItems.Mobs.GroupBy(m => m.MobInfo.ID);
                foreach (var group in mobGroups)
                {
                    var firstMob = group.First();
                    var name = firstMob.MobInfo.Name ?? "Unknown";
                    WriteLine($"### Mob {group.Key} \"{name}\" ({group.Count()} spawn(s))");
                    foreach (var mob in group.OrderBy(m => m.X))
                    {
                        var props = new List<string>();
                        props.Add($"pos=({mob.X}, {mob.Y})");
                        if (mob.rx0Shift != 0 || mob.rx1Shift != 0)
                            props.Add($"patrol=[{mob.rx0Shift}, {mob.rx1Shift}]");
                        if (mob.MobTime.HasValue && mob.MobTime.Value > 0)
                            props.Add($"respawn={mob.MobTime.Value}ms");
                        if (mob.Flip)
                            props.Add("flipped");
                        if (mob.Hide == true)
                            props.Add("hidden");
                        if (mob.Team.HasValue)
                            props.Add($"team={mob.Team.Value}");
                        if (mob.Info.HasValue)
                            props.Add($"info={mob.Info.Value}");
                        WriteLine($"- {string.Join(", ", props)}");
                    }
                }
                WriteLine();
            }

            // NPC summary with detailed info
            if (board.BoardItems.NPCs.Count > 0)
            {
                WriteLine("## NPCs");
                foreach (var npc in board.BoardItems.NPCs.OrderBy(n => n.X))
                {
                    var props = new List<string>();
                    props.Add($"pos=({npc.X}, {npc.Y})");
                    if (npc.rx0Shift != 0 || npc.rx1Shift != 0)
                        props.Add($"patrol=[{npc.rx0Shift}, {npc.rx1Shift}]");
                    if (npc.Flip)
                        props.Add("flipped");
                    if (npc.Hide == true)
                        props.Add("hidden");
                    if (npc.MobTime.HasValue && npc.MobTime.Value > 0)
                        props.Add($"respawn={npc.MobTime.Value}ms");
                    WriteLine($"- NPC {npc.NpcInfo.ID} \"{npc.NpcInfo.StringName}\": {string.Join(", ", props)}");
                }
                WriteLine();
            }

            // Footholds/Platforms summary with advanced properties
            if (board.BoardItems.FootholdLines.Count > 0)
            {
                WriteLine("## Platforms (Footholds)");
                var fhByLayer = board.BoardItems.FootholdLines.GroupBy(fh => fh.LayerNumber);
                foreach (var layerGroup in fhByLayer.OrderBy(g => g.Key))
                {
                    // Group by platform number within layer
                    var byPlatform = layerGroup.GroupBy(fh => fh.PlatformNumber);
                    foreach (var platGroup in byPlatform.OrderBy(g => g.Key))
                    {
                        WriteLine($"### Layer {layerGroup.Key}, Platform {platGroup.Key} ({platGroup.Count()} segments)");
                        foreach (var fh in platGroup.OrderBy(f => Math.Min(f.FirstDot.X, f.SecondDot.X)))
                        {
                            var props = new List<string>();
                            if (fh.IsWall)
                                props.Add("wall");
                            if (fh.CantThrough == true)
                                props.Add("cantThrough");
                            if (fh.ForbidFallDown == true)
                                props.Add("noFallDown");
                            if (fh.Force.HasValue)
                                props.Add($"force={fh.Force.Value}");
                            if (fh.Piece.HasValue)
                                props.Add($"piece={fh.Piece.Value}");
                            var propsStr = props.Count > 0 ? $" [{string.Join(", ", props)}]" : "";
                            WriteLine($"- ({fh.FirstDot.X}, {fh.FirstDot.Y}) to ({fh.SecondDot.X}, {fh.SecondDot.Y}){propsStr}");
                        }
                    }
                }
                WriteLine();
            }

            // Tiles summary
            var tiles = board.BoardItems.TileObjs.OfType<TileInstance>().ToList();
            if (tiles.Count > 0)
            {
                WriteLine("## Tiles");
                var tilesByLayer = tiles.GroupBy(t => t.Layer.LayerNumber);
                foreach (var layerGroup in tilesByLayer.OrderBy(g => g.Key))
                {
                    var layer = board.Layers[layerGroup.Key];
                    WriteLine($"### Layer {layerGroup.Key} (TileSet: \"{layer.tS ?? "None"}\")");
                    foreach (var tile in layerGroup.OrderBy(t => t.X).ThenBy(t => t.Y))
                    {
                        var tileInfo = (HaCreator.MapEditor.Info.TileInfo)tile.BaseInfo;
                        WriteLine($"- ({tile.X}, {tile.Y}): {tileInfo.u}/{tileInfo.no}, Z={tile.Z}");
                    }
                }
                WriteLine();
            }

            // Objects summary
            var objects = board.BoardItems.TileObjs.OfType<ObjectInstance>().ToList();
            if (objects.Count > 0)
            {
                WriteLine("## Objects");
                var objsByLayer = objects.GroupBy(o => o.Layer.LayerNumber);
                foreach (var layerGroup in objsByLayer.OrderBy(g => g.Key))
                {
                    WriteLine($"### Layer {layerGroup.Key}");
                    foreach (var obj in layerGroup.OrderBy(o => o.X).ThenBy(o => o.Y))
                    {
                        var objInfo = (HaCreator.MapEditor.Info.ObjectInfo)obj.BaseInfo;
                        var props = new List<string>();
                        if (obj.Flip) props.Add("Flipped");
                        if (obj.hide == true) props.Add("Hidden");
                        var propsStr = props.Count > 0 ? $" [{string.Join(", ", props)}]" : "";
                        WriteLine($"- ({obj.X}, {obj.Y}): {objInfo.oS}/{objInfo.l0}/{objInfo.l1}/{objInfo.l2}, Z={obj.Z}{propsStr}");
                    }
                }
                WriteLine();
            }

            // Backgrounds summary
            var backBGs = board.BoardItems.BackBackgrounds.ToList();
            var frontBGs = board.BoardItems.FrontBackgrounds.ToList();
            if (backBGs.Count > 0 || frontBGs.Count > 0)
            {
                WriteLine("## Backgrounds");
                if (backBGs.Count > 0)
                {
                    WriteLine("### Back Backgrounds");
                    foreach (var bg in backBGs.OrderBy(b => b.Z))
                    {
                        var bgInfo = (HaCreator.MapEditor.Info.BackgroundInfo)bg.BaseInfo;
                        var props = new List<string>();
                        if (bg.Flip) props.Add("Flipped");
                        if (!string.IsNullOrEmpty(bg.SpineAni)) props.Add($"Spine:{bg.SpineAni}");
                        var propsStr = props.Count > 0 ? $" [{string.Join(", ", props)}]" : "";
                        WriteLine($"- ({bg.BaseX}, {bg.BaseY}): {bgInfo.bS}/{bg.type}/{bgInfo.no}, Z={bg.Z}, rx={bg.rx}, ry={bg.ry}, cx={bg.cx}, cy={bg.cy}, a={bg.a}{propsStr}");
                    }
                }
                if (frontBGs.Count > 0)
                {
                    WriteLine("### Front Backgrounds");
                    foreach (var bg in frontBGs.OrderBy(b => b.Z))
                    {
                        var bgInfo = (HaCreator.MapEditor.Info.BackgroundInfo)bg.BaseInfo;
                        var props = new List<string>();
                        if (bg.Flip) props.Add("Flipped");
                        if (!string.IsNullOrEmpty(bg.SpineAni)) props.Add($"Spine:{bg.SpineAni}");
                        var propsStr = props.Count > 0 ? $" [{string.Join(", ", props)}]" : "";
                        WriteLine($"- ({bg.BaseX}, {bg.BaseY}): {bgInfo.bS}/{bg.type}/{bgInfo.no}, Z={bg.Z}, rx={bg.rx}, ry={bg.ry}, cx={bg.cx}, cy={bg.cy}, a={bg.a}{propsStr}");
                    }
                }
                WriteLine();
            }

            // Ropes/Ladders summary
            if (board.BoardItems.Ropes.Count > 0)
            {
                WriteLine("## Ropes & Ladders");
                foreach (var rope in board.BoardItems.Ropes.OrderBy(r => r.FirstAnchor.X))
                {
                    var type = rope.ladder ? "Ladder" : "Rope";
                    WriteLine($"- {type}: ({rope.FirstAnchor.X}, {rope.FirstAnchor.Y}) to ({rope.SecondAnchor.X}, {rope.SecondAnchor.Y})");
                }
                WriteLine();
            }

            // Chairs summary
            if (board.BoardItems.Chairs.Count > 0)
            {
                WriteLine("## Chairs");
                foreach (var chair in board.BoardItems.Chairs.OrderBy(c => c.X))
                {
                    WriteLine($"- ({chair.X}, {chair.Y})");
                }
                WriteLine();
            }

            // Reactors summary
            if (board.BoardItems.Reactors.Count > 0)
            {
                WriteLine("## Reactors");
                foreach (var reactor in board.BoardItems.Reactors.OrderBy(r => r.X))
                {
                    var reactorInfo = (HaCreator.MapEditor.Info.ReactorInfo)reactor.BaseInfo;
                    var name = !string.IsNullOrEmpty(reactor.Name) ? $" \"{reactor.Name}\"" : "";
                    WriteLine($"- Reactor {reactorInfo.ID}{name} at ({reactor.X}, {reactor.Y})");
                }
                WriteLine();
            }

            // ToolTips summary
            if (board.BoardItems.ToolTips.Count > 0)
            {
                WriteLine("## ToolTips (Area Text)");
                int tooltipIndex = 0;
                foreach (var tooltip in board.BoardItems.ToolTips.OrderBy(t => t.X))
                {
                    var title = !string.IsNullOrEmpty(tooltip.Title) ? $"\"{tooltip.Title}\"" : "(no title)";
                    var desc = !string.IsNullOrEmpty(tooltip.Desc) ? $" desc=\"{tooltip.Desc}\"" : "";
                    WriteLine($"- ToolTip #{tooltipIndex}: {title} at ({tooltip.X}, {tooltip.Y}) size=({tooltip.Width}x{tooltip.Height}){desc}");
                    tooltipIndex++;
                }
                WriteLine();
            }

            // Include asset catalog for AI awareness
            sb.Append(MapAssetCatalog.GenerateCompactSummary());

            // Add contextual analysis for better AI understanding
            SerializeContextualAnalysis();

            return sb.ToString();
        }

        /// <summary>
        /// Serialize contextual analysis to help AI make better decisions
        /// </summary>
        private void SerializeContextualAnalysis()
        {
            WriteLine("## Contextual Analysis");

            // Portal pair relationships
            if (board.BoardItems.Portals.Count > 0)
            {
                WriteLine("### Portal Connections");
                var exitPortals = board.BoardItems.Portals.Where(p => p.tm != 999999999).ToList();
                if (exitPortals.Count > 0)
                {
                    foreach (var p in exitPortals)
                    {
                        string targetInfo = !string.IsNullOrEmpty(p.tn) ? $" -> \"{p.tn}\"" : "";
                        WriteLine($"- \"{p.pn}\" exits to map {p.tm}{targetInfo}");
                    }
                }
                else
                {
                    WriteLine("- No exit portals (self-contained map)");
                }
            }

            // Spawn distribution analysis
            if (board.BoardItems.Mobs.Count > 0)
            {
                WriteLine("### Mob Distribution");
                var mobsByY = board.BoardItems.Mobs.GroupBy(m => (int)(m.Y / 100) * 100);
                foreach (var group in mobsByY.OrderBy(g => g.Key))
                {
                    int count = group.Count();
                    int minX = group.Min(m => m.X);
                    int maxX = group.Max(m => m.X);
                    string density = count > 5 ? "dense" : count > 2 ? "moderate" : "sparse";
                    WriteLine($"- Y ~{group.Key}: {count} mob(s), X range [{minX} to {maxX}] ({density})");
                }
            }

            // Platform height analysis
            if (board.BoardItems.FootholdLines.Count > 0)
            {
                WriteLine("### Platform Levels");
                var platformsByY = board.BoardItems.FootholdLines
                    .Where(f => !f.IsWall)
                    .GroupBy(f => (int)(Math.Min(f.FirstDot.Y, f.SecondDot.Y) / 50) * 50);

                foreach (var group in platformsByY.OrderBy(g => g.Key).Take(10))
                {
                    int totalWidth = group.Sum(f => Math.Abs(f.SecondDot.X - f.FirstDot.X));
                    int minX = group.Min(f => Math.Min(f.FirstDot.X, f.SecondDot.X));
                    int maxX = group.Max(f => Math.Max(f.FirstDot.X, f.SecondDot.X));
                    WriteLine($"- Y ~{group.Key}: {group.Count()} segment(s), total width ~{totalWidth}px, X range [{minX} to {maxX}]");
                }
            }

            // Layer usage summary
            WriteLine("### Layer Usage");
            var tilesByLayer = board.BoardItems.TileObjs.OfType<TileInstance>().GroupBy(t => t.LayerNumber);
            var objsByLayer = board.BoardItems.TileObjs.OfType<ObjectInstance>().GroupBy(o => o.LayerNumber);
            for (int i = 0; i < board.Layers.Count; i++)
            {
                int tileCount = tilesByLayer.FirstOrDefault(g => g.Key == i)?.Count() ?? 0;
                int objCount = objsByLayer.FirstOrDefault(g => g.Key == i)?.Count() ?? 0;
                string tileset = board.Layers[i].tS ?? "(none)";
                if (tileCount > 0 || objCount > 0)
                {
                    WriteLine($"- Layer {i}: {tileCount} tiles, {objCount} objects, tileset=\"{tileset}\"");
                }
            }

            // Suggestions/warnings
            WriteLine("### Suggestions");
            var suggestions = new List<string>();

            // Check for missing spawn point
            if (!board.BoardItems.Portals.Any(p => p.pn.Equals("sp", StringComparison.OrdinalIgnoreCase)))
            {
                suggestions.Add("MISSING spawn point portal 'sp' - players won't be able to enter this map!");
            }

            // Check for very sparse mob distribution
            if (board.BoardItems.Mobs.Count > 0 && board.BoardItems.Mobs.Count < 3)
            {
                suggestions.Add("Low mob count - consider adding more monsters for better training");
            }

            // Check for no footholds
            if (board.BoardItems.FootholdLines.Count == 0)
            {
                suggestions.Add("No footholds - players won't be able to walk! Add platforms first.");
            }

            // Check for empty map
            if (board.BoardItems.TileObjs.Count == 0)
            {
                suggestions.Add("No tiles or objects - map will appear empty. Consider adding visual elements.");
            }

            if (suggestions.Count > 0)
            {
                foreach (var suggestion in suggestions)
                {
                    WriteLine($"- **{suggestion}**");
                }
            }
            else
            {
                WriteLine("- Map appears well-configured");
            }

            WriteLine();
        }

        /// <summary>
        /// Serialize current map settings (options and field limits)
        /// </summary>
        private void SerializeMapSettings()
        {
            var info = board.MapInfo;

            WriteLine("## Map Settings");

            // Map dimensions
            WriteLine($"- Map Size: {board.MapSize.X} x {board.MapSize.Y} pixels");

            // Viewing Range (VR)
            if (board.VRRectangle != null)
            {
                var vr = board.VRRectangle;
                int vrLeft = vr.X;
                int vrTop = vr.Y;
                int vrRight = vr.X + vr.Width;
                int vrBottom = vr.Y + vr.Height;
                WriteLine($"- Viewing Range (VR): left={vrLeft}, top={vrTop}, right={vrRight}, bottom={vrBottom}");
                WriteLine($"  VR Size: {vr.Width} x {vr.Height} pixels");
            }
            else
            {
                WriteLine($"- Viewing Range (VR): (not set - uses map bounds)");
            }

            // Minimap Rectangle (if different from VR)
            if (board.MinimapRectangle != null)
            {
                var mm = board.MinimapRectangle;
                WriteLine($"- Minimap Rect: left={mm.X}, top={mm.Y}, right={mm.X + mm.Width}, bottom={mm.Y + mm.Height}");
            }

            // BGM
            if (!string.IsNullOrEmpty(info.bgm))
            {
                WriteLine($"- BGM: {info.bgm}");
            }
            else
            {
                WriteLine($"- BGM: (not set)");
            }

            // Return Maps
            WriteLine($"- Return Map: {(info.returnMap != 999999999 ? info.returnMap.ToString() : "(same map)")}");
            WriteLine($"- Forced Return: {(info.forcedReturn != 999999999 ? info.forcedReturn.ToString() : "(same map)")}");

            // Mob Rate
            WriteLine($"- Mob Rate: {info.mobRate}");

            // Field Type
            if (info.fieldType.HasValue)
            {
                WriteLine($"- Field Type: {info.fieldType.Value} ({(int)info.fieldType.Value})");
            }

            // Time/Level Limits
            if (info.timeLimit.HasValue)
            {
                WriteLine($"- Time Limit: {info.timeLimit.Value} seconds");
            }
            if (info.lvLimit.HasValue)
            {
                WriteLine($"- Level Limit: {info.lvLimit.Value}");
            }
            if (info.lvForceMove.HasValue)
            {
                WriteLine($"- Level Force Move: {info.lvForceMove.Value}");
            }

            // Scripts
            if (!string.IsNullOrEmpty(info.onUserEnter))
            {
                WriteLine($"- OnUserEnter Script: \"{info.onUserEnter}\"");
            }
            if (!string.IsNullOrEmpty(info.onFirstUserEnter))
            {
                WriteLine($"- OnFirstUserEnter Script: \"{info.onFirstUserEnter}\"");
            }

            // Visual Effect
            if (!string.IsNullOrEmpty(info.effect))
            {
                WriteLine($"- Effect: \"{info.effect}\"");
            }

            // Help Text
            if (!string.IsNullOrEmpty(info.help))
            {
                WriteLine($"- Help: \"{info.help}\"");
            }

            // Map Description
            if (!string.IsNullOrEmpty(info.mapDesc))
            {
                WriteLine($"- Description: \"{info.mapDesc}\"");
            }

            // Drop Settings
            if (info.dropExpire.HasValue)
            {
                WriteLine($"- Drop Expire: {info.dropExpire.Value} seconds");
            }
            if (info.dropRate.HasValue)
            {
                WriteLine($"- Drop Rate: {info.dropRate.Value}");
            }

            // HP Decay Settings
            if (info.decHP.HasValue || info.decInterval.HasValue)
            {
                WriteLine($"- HP Decay: {info.decHP ?? 0} HP every {info.decInterval ?? 0}ms");
            }

            // Recovery Rate
            if (info.recovery.HasValue)
            {
                WriteLine($"- Recovery Rate: {info.recovery.Value}");
            }

            // Ice Slip Speed
            if (info.fs.HasValue)
            {
                WriteLine($"- Ice Slip Speed (fs): {info.fs.Value}");
            }

            // Mob Capacity Settings (for massacre PQs)
            if (info.createMobInterval.HasValue)
            {
                WriteLine($"- Create Mob Interval: {info.createMobInterval.Value}ms");
            }
            if (info.fixedMobCapacity.HasValue)
            {
                WriteLine($"- Fixed Mob Capacity: {info.fixedMobCapacity.Value}");
            }

            // Time Mob
            if (info.timeMob.HasValue)
            {
                var tm = info.timeMob.Value;
                WriteLine($"- Time Mob: ID={tm.id}, Hours={tm.startHour}-{tm.endHour}, Message=\"{tm.message}\"");
            }

            // Protect/Allowed Items
            if (info.protectItem != null && info.protectItem.Count > 0)
            {
                WriteLine($"- Protect Items: {string.Join(", ", info.protectItem)}");
            }
            if (info.allowedItem != null && info.allowedItem.Count > 0)
            {
                WriteLine($"- Allowed Items: {string.Join(", ", info.allowedItem)}");
            }

            // Black Border Hack (LB values)
            if (info.LBSide.HasValue || info.LBTop.HasValue || info.LBBottom.HasValue)
            {
                WriteLine($"- Black Borders: Side={info.LBSide ?? 0}, Top={info.LBTop ?? 0}, Bottom={info.LBBottom ?? 0}");
            }

            // Collect enabled map options
            var enabledOptions = new List<string>();
            if (info.cloud) enabledOptions.Add("cloud");
            if (info.snow) enabledOptions.Add("snow");
            if (info.rain) enabledOptions.Add("rain");
            if (info.swim) enabledOptions.Add("swim");
            if (info.fly) enabledOptions.Add("fly");
            if (info.town) enabledOptions.Add("town");
            if (info.partyOnly) enabledOptions.Add("partyOnly");
            if (info.expeditionOnly) enabledOptions.Add("expeditionOnly");
            if (info.noMapCmd) enabledOptions.Add("noMapCmd");
            if (info.hideMinimap) enabledOptions.Add("hideMinimap");
            if (info.miniMapOnOff) enabledOptions.Add("miniMapOnOff");
            if (info.personalShop) enabledOptions.Add("personalShop");
            if (info.entrustedShop) enabledOptions.Add("entrustedShop");
            if (info.noRegenMap) enabledOptions.Add("noRegenMap");
            if (info.blockPBossChange) enabledOptions.Add("blockPBossChange");
            if (info.everlast) enabledOptions.Add("everlast");
            if (info.damageCheckFree) enabledOptions.Add("damageCheckFree");
            if (info.scrollDisable) enabledOptions.Add("scrollDisable");
            if (info.needSkillForFly) enabledOptions.Add("needSkillForFly");
            if (info.zakum2Hack) enabledOptions.Add("zakum2Hack");
            if (info.allMoveCheck) enabledOptions.Add("allMoveCheck");
            if (info.VRLimit) enabledOptions.Add("VRLimit");
            if (info.mirror_Bottom) enabledOptions.Add("mirror_Bottom");
            if (info.reactorShuffle) enabledOptions.Add("reactorShuffle");
            if (info.consumeItemCoolTime) enabledOptions.Add("consumeItemCoolTime");
            if (info.zeroSideOnly) enabledOptions.Add("zeroSideOnly");

            if (enabledOptions.Count > 0)
            {
                WriteLine($"- Options: {string.Join(", ", enabledOptions)}");
            }
            else
            {
                WriteLine($"- Options: (none enabled)");
            }

            // Collect enabled field limits
            var enabledLimits = new List<string>();
            foreach (FieldLimitType limitType in Enum.GetValues(typeof(FieldLimitType)))
            {
                if (limitType.Check(info.fieldLimit))
                {
                    enabledLimits.Add(limitType.ToString());
                }
            }

            if (enabledLimits.Count > 0)
            {
                WriteLine($"- Field Limits: {string.Join(", ", enabledLimits)}");
            }
            else
            {
                WriteLine($"- Field Limits: (none)");
            }

            WriteLine();
        }

        /// <summary>
        /// Generate an ASCII art visualization of the map layout.
        /// This helps the AI understand the spatial arrangement of elements.
        /// </summary>
        private void GenerateAsciiMapVisualization(int minX, int maxX, int minY, int maxY)
        {
            WriteLine("## ASCII Map Visualization");
            WriteLine("```");
            WriteLine("Legend: = Platform  | Wall  I Rope/Ladder  P Portal  M Mob  N NPC  C Chair  R Reactor  O Object  * Spawn");
            WriteLine();

            // Add padding to bounds
            int padding = 50;
            minX -= padding;
            maxX += padding;
            minY -= padding;
            maxY += padding;

            int worldWidth = maxX - minX;
            int worldHeight = maxY - minY;

            // Calculate scale to fit within ASCII_MAP dimensions
            // Use proportional scaling to maintain aspect ratio
            double scaleX = (double)(ASCII_MAP_WIDTH - 2) / Math.Max(worldWidth, 1);
            double scaleY = (double)(ASCII_MAP_HEIGHT - 2) / Math.Max(worldHeight, 1);
            double scale = Math.Min(scaleX, scaleY);

            int mapWidth = Math.Max(20, Math.Min(ASCII_MAP_WIDTH, (int)(worldWidth * scale) + 2));
            int mapHeight = Math.Max(10, Math.Min(ASCII_MAP_HEIGHT, (int)(worldHeight * scale) + 2));

            // Create the character grid
            char[,] grid = new char[mapHeight, mapWidth];
            int[,] priority = new int[mapHeight, mapWidth]; // Higher priority overwrites lower

            // Initialize with empty space
            for (int y = 0; y < mapHeight; y++)
            {
                for (int x = 0; x < mapWidth; x++)
                {
                    grid[y, x] = ' ';
                    priority[y, x] = 0;
                }
            }

            // Helper function to convert world coordinates to grid coordinates
            int ToGridX(int worldX) => Math.Max(0, Math.Min(mapWidth - 1, (int)((worldX - minX) * scale)));
            int ToGridY(int worldY) => Math.Max(0, Math.Min(mapHeight - 1, (int)((worldY - minY) * scale)));

            // Helper function to set a character with priority
            void SetChar(int gx, int gy, char c, int p)
            {
                if (gx >= 0 && gx < mapWidth && gy >= 0 && gy < mapHeight)
                {
                    if (p >= priority[gy, gx])
                    {
                        grid[gy, gx] = c;
                        priority[gy, gx] = p;
                    }
                }
            }

            // Draw footholds (platforms) - priority 1
            foreach (var fh in board.BoardItems.FootholdLines)
            {
                int x1 = ToGridX(fh.FirstDot.X);
                int y1 = ToGridY(fh.FirstDot.Y);
                int x2 = ToGridX(fh.SecondDot.X);
                int y2 = ToGridY(fh.SecondDot.Y);

                // Draw line between points
                DrawLine(grid, priority, x1, y1, x2, y2, mapWidth, mapHeight);
            }

            // Draw ropes and ladders - priority 2
            foreach (var rope in board.BoardItems.Ropes)
            {
                int x = ToGridX(rope.FirstAnchor.X);
                int y1 = ToGridY(rope.FirstAnchor.Y);
                int y2 = ToGridY(rope.SecondAnchor.Y);

                int startY = Math.Min(y1, y2);
                int endY = Math.Max(y1, y2);
                for (int y = startY; y <= endY; y++)
                {
                    SetChar(x, y, 'I', 2);
                }
            }

            // Draw chairs - priority 3
            foreach (var chair in board.BoardItems.Chairs)
            {
                int x = ToGridX(chair.X);
                int y = ToGridY(chair.Y);
                SetChar(x, y, 'C', 3);
            }

            // Draw reactors - priority 4
            foreach (var reactor in board.BoardItems.Reactors)
            {
                int x = ToGridX(reactor.X);
                int y = ToGridY(reactor.Y);
                SetChar(x, y, 'R', 4);
            }

            // Draw objects - priority 5 (sample some, not all)
            var objects = board.BoardItems.TileObjs.OfType<ObjectInstance>().Take(50).ToList();
            foreach (var obj in objects)
            {
                int x = ToGridX(obj.X);
                int y = ToGridY(obj.Y);
                SetChar(x, y, 'O', 5);
            }

            // Draw mobs - priority 6
            foreach (var mob in board.BoardItems.Mobs)
            {
                int x = ToGridX(mob.X);
                int y = ToGridY(mob.Y);
                SetChar(x, y, 'M', 6);
            }

            // Draw NPCs - priority 7
            foreach (var npc in board.BoardItems.NPCs)
            {
                int x = ToGridX(npc.X);
                int y = ToGridY(npc.Y);
                SetChar(x, y, 'N', 7);
            }

            // Draw portals - priority 8 (highest for important gameplay elements)
            foreach (var portal in board.BoardItems.Portals)
            {
                int x = ToGridX(portal.X);
                int y = ToGridY(portal.Y);
                char c = portal.pn == "sp" ? '*' : 'P';
                SetChar(x, y, c, 8);
            }

            // Draw axis labels
            WriteLine($"    X: {minX} to {maxX} (scale: 1 char = ~{(int)(1 / scale)} pixels)");
            WriteLine($"    Y: {minY} (top) to {maxY} (bottom)");
            WriteLine();

            // Output the grid with border
            StringBuilder rowSb = new StringBuilder();

            // Top border
            rowSb.Append("    +");
            for (int x = 0; x < mapWidth; x++) rowSb.Append('-');
            rowSb.Append('+');
            WriteLine(rowSb.ToString());

            // Grid rows
            for (int y = 0; y < mapHeight; y++)
            {
                rowSb.Clear();
                rowSb.Append("    |");
                for (int x = 0; x < mapWidth; x++)
                {
                    rowSb.Append(grid[y, x]);
                }
                rowSb.Append('|');
                WriteLine(rowSb.ToString());
            }

            // Bottom border
            rowSb.Clear();
            rowSb.Append("    +");
            for (int x = 0; x < mapWidth; x++) rowSb.Append('-');
            rowSb.Append('+');
            WriteLine(rowSb.ToString());

            WriteLine("```");
            WriteLine();
        }

        /// <summary>
        /// Draw a line on the grid using Bresenham's algorithm.
        /// Uses '=' for horizontal, '|' for vertical, '/' and '\' for diagonals.
        /// </summary>
        private void DrawLine(char[,] grid, int[,] priority, int x1, int y1, int x2, int y2, int width, int height)
        {
            int dx = Math.Abs(x2 - x1);
            int dy = Math.Abs(y2 - y1);
            int sx = x1 < x2 ? 1 : -1;
            int sy = y1 < y2 ? 1 : -1;
            int err = dx - dy;

            // Determine line character based on slope
            char lineChar;
            if (dy == 0)
            {
                lineChar = '='; // Horizontal
            }
            else if (dx == 0)
            {
                lineChar = '|'; // Vertical
            }
            else if ((sx > 0 && sy > 0) || (sx < 0 && sy < 0))
            {
                lineChar = '\\'; // Diagonal down-right or up-left
            }
            else
            {
                lineChar = '/'; // Diagonal down-left or up-right
            }

            while (true)
            {
                if (x1 >= 0 && x1 < width && y1 >= 0 && y1 < height)
                {
                    if (priority[y1, x1] <= 1)
                    {
                        grid[y1, x1] = lineChar;
                        priority[y1, x1] = 1;
                    }
                }

                if (x1 == x2 && y1 == y2) break;

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x1 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y1 += sy;
                }
            }
        }

        /// <summary>
        /// Serialize the entire map to AI-readable text format
        /// </summary>
        public string Serialize()
        {
            sb.Clear();
            indentLevel = 0;

            WriteHeader();
            WriteMapInfo();
            WriteMapBounds();
            WriteLayers();
            WriteBackgrounds();
            WritePortals();
            WriteMobs();
            WriteNPCs();
            WriteReactors();
            WriteFootholds();
            WriteRopes();
            WriteChairs();

            return sb.ToString();
        }

        /// <summary>
        /// Serialize only selected items for partial export
        /// </summary>
        public string SerializeSelected()
        {
            sb.Clear();
            indentLevel = 0;

            WriteLine("# Selected Map Elements");
            WriteLine($"# Selection Count: {board.SelectedItems.Count}");
            WriteLine();

            var selectedByType = board.SelectedItems.GroupBy(item => item.GetType().Name);
            foreach (var group in selectedByType)
            {
                WriteLine($"## {group.Key}s ({group.Count()})");
                foreach (var item in group)
                {
                    SerializeItem(item);
                }
                WriteLine();
            }

            return sb.ToString();
        }

        private void WriteHeader()
        {
            WriteLine("# MapleStory Map Description (AI-Readable Format)");
            WriteLine($"# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            WriteLine($"# Map ID: {board.MapInfo.id}");
            WriteLine($"# Map Name: {board.MapInfo.strMapName}");
            WriteLine();
        }

        private void WriteMapInfo()
        {
            WriteLine("## Map Information");
            var info = board.MapInfo;

            WriteLine($"MapID: {info.id}");
            WriteLine($"MapName: \"{info.strMapName}\"");
            WriteLine($"StreetName: \"{info.strStreetName}\"");
            WriteLine($"Category: \"{info.strCategoryName}\"");
            WriteLine($"BGM: \"{info.bgm}\"");
            WriteLine($"MapMark: \"{info.mapMark}\"");
            WriteLine($"IsTown: {info.town}");
            WriteLine($"CanFly: {info.fly}");
            WriteLine($"CanSwim: {info.swim}");
            WriteLine($"HasClouds: {info.cloud}");
            WriteLine($"ReturnMap: {info.returnMap}");
            WriteLine($"ForcedReturn: {info.forcedReturn}");
            WriteLine($"MobRate: {info.mobRate}");

            if (info.fieldType.HasValue)
                WriteLine($"FieldType: {info.fieldType.Value}");
            if (info.timeLimit.HasValue)
                WriteLine($"TimeLimit: {info.timeLimit.Value} seconds");
            if (info.lvLimit.HasValue)
                WriteLine($"LevelLimit: {info.lvLimit.Value}");
            if (!string.IsNullOrEmpty(info.onUserEnter))
                WriteLine($"OnUserEnter: \"{info.onUserEnter}\"");
            if (!string.IsNullOrEmpty(info.onFirstUserEnter))
                WriteLine($"OnFirstUserEnter: \"{info.onFirstUserEnter}\"");
            if (info.snow == true)
                WriteLine("Weather: Snow");
            if (info.rain == true)
                WriteLine("Weather: Rain");

            WriteLine();
        }

        private void WriteMapBounds()
        {
            WriteLine("## Map Dimensions");
            WriteLine($"MapSize: {board.MapSize.X} x {board.MapSize.Y} pixels");
            WriteLine($"CenterPoint: ({board.CenterPoint.X}, {board.CenterPoint.Y})");

            if (board.VRRectangle != null)
            {
                var vr = board.VRRectangle;
                WriteLine($"ViewableRegion: Left={vr.X}, Top={vr.Y}, Width={vr.Width}, Height={vr.Height}");
            }

            if (board.MinimapRectangle != null)
            {
                var mm = board.MinimapRectangle;
                WriteLine($"MinimapRegion: Left={mm.X}, Top={mm.Y}, Width={mm.Width}, Height={mm.Height}");
            }

            WriteLine();
        }

        private void WriteLayers()
        {
            WriteLine("## Layers and Objects");

            for (int i = 0; i < board.Layers.Count; i++)
            {
                var layer = board.Layers[i];
                var tilesInLayer = board.BoardItems.TileObjs.Where(t => t.Layer.LayerNumber == i).ToList();

                if (tilesInLayer.Count == 0)
                    continue;

                WriteLine($"### Layer {i} (TileSet: \"{layer.tS ?? "None"}\")");
                indentLevel++;

                var tiles = tilesInLayer.OfType<TileInstance>().ToList();
                var objects = tilesInLayer.OfType<ObjectInstance>().ToList();

                if (tiles.Count > 0)
                {
                    WriteLine($"Tiles ({tiles.Count}):");
                    indentLevel++;
                    foreach (var tile in tiles.OrderBy(t => t.X).ThenBy(t => t.Y))
                    {
                        var tileInfo = (HaCreator.MapEditor.Info.TileInfo)tile.BaseInfo;
                        WriteLine($"- Tile at ({tile.X}, {tile.Y}): Set=\"{tileInfo.tS}\", Type=\"{tileInfo.u}\", No={tileInfo.no}, Z={tile.Z}");
                    }
                    indentLevel--;
                }

                if (objects.Count > 0)
                {
                    WriteLine($"Objects ({objects.Count}):");
                    indentLevel++;
                    foreach (var obj in objects.OrderBy(o => o.X).ThenBy(o => o.Y))
                    {
                        var objInfo = (HaCreator.MapEditor.Info.ObjectInfo)obj.BaseInfo;
                        var flipText = obj.Flip ? ", Flipped" : "";
                        var hideText = obj.hide == true ? ", Hidden" : "";
                        WriteLine($"- Object at ({obj.X}, {obj.Y}): Set=\"{objInfo.oS}\", Path=\"{objInfo.l0}/{objInfo.l1}/{objInfo.l2}\", Z={obj.Z}{flipText}{hideText}");
                        if (!string.IsNullOrEmpty(obj.Name))
                            WriteLine($"  Name: \"{obj.Name}\"");
                    }
                    indentLevel--;
                }

                indentLevel--;
                WriteLine();
            }
        }

        private void WriteBackgrounds()
        {
            var backBGs = board.BoardItems.BackBackgrounds.ToList();
            var frontBGs = board.BoardItems.FrontBackgrounds.ToList();

            if (backBGs.Count == 0 && frontBGs.Count == 0)
                return;

            WriteLine("## Backgrounds");

            if (backBGs.Count > 0)
            {
                WriteLine($"### Back Backgrounds ({backBGs.Count})");
                indentLevel++;
                foreach (var bg in backBGs.OrderBy(b => b.Z))
                {
                    SerializeBackground(bg);
                }
                indentLevel--;
            }

            if (frontBGs.Count > 0)
            {
                WriteLine($"### Front Backgrounds ({frontBGs.Count})");
                indentLevel++;
                foreach (var bg in frontBGs.OrderBy(b => b.Z))
                {
                    SerializeBackground(bg);
                }
                indentLevel--;
            }

            WriteLine();
        }

        private void SerializeBackground(BackgroundInstance bg)
        {
            var bgInfo = (HaCreator.MapEditor.Info.BackgroundInfo)bg.BaseInfo;
            var flipText = bg.Flip ? ", Flipped" : "";

            WriteLine($"- Background at ({bg.BaseX}, {bg.BaseY}):");
            indentLevel++;
            WriteLine($"Set: \"{bgInfo.bS}\", No: \"{bgInfo.no}\", Type: {bg.type}");
            WriteLine($"Z-Order: {bg.Z}, Alpha: {bg.a}");
            WriteLine($"Parallax: rx={bg.rx}, ry={bg.ry}");
            if (bg.cx != 0 || bg.cy != 0)
                WriteLine($"Tiling: cx={bg.cx}, cy={bg.cy}");
            if (bg.Flip)
                WriteLine("Flipped: Yes");
            if (!string.IsNullOrEmpty(bg.SpineAni))
                WriteLine($"SpineAnimation: \"{bg.SpineAni}\"");
            indentLevel--;
        }

        private void WritePortals()
        {
            var portals = board.BoardItems.Portals.ToList();
            if (portals.Count == 0)
                return;

            WriteLine("## Portals");
            WriteLine($"Total Portals: {portals.Count}");
            indentLevel++;

            foreach (var portal in portals.OrderBy(p => p.pn))
            {
                WriteLine($"- Portal \"{portal.pn}\" at ({portal.X}, {portal.Y}):");
                indentLevel++;
                WriteLine($"Type: {portal.pt}");
                if (portal.tm != 999999999)
                    WriteLine($"TargetMap: {portal.tm}");
                if (!string.IsNullOrEmpty(portal.tn))
                    WriteLine($"TargetPortal: \"{portal.tn}\"");
                if (!string.IsNullOrEmpty(portal.script))
                    WriteLine($"Script: \"{portal.script}\"");
                if (portal.delay.HasValue)
                    WriteLine($"Delay: {portal.delay.Value}ms");
                indentLevel--;
            }

            indentLevel--;
            WriteLine();
        }

        private void WriteMobs()
        {
            var mobs = board.BoardItems.Mobs.ToList();
            if (mobs.Count == 0)
                return;

            WriteLine("## Mobs (Monsters)");
            WriteLine($"Total Mobs: {mobs.Count}");
            indentLevel++;

            // Group by mob ID for clarity
            var mobGroups = mobs.GroupBy(m => m.MobInfo.ID);
            foreach (var group in mobGroups.OrderBy(g => g.Key))
            {
                var firstMob = group.First();
                WriteLine($"### Mob ID {group.Key}: \"{firstMob.MobInfo.Name ?? "Unknown"}\" ({group.Count()} spawns)");
                indentLevel++;

                foreach (var mob in group.OrderBy(m => m.X))
                {
                    var flipText = mob.Flip == true ? ", Flipped" : "";
                    var hideText = mob.Hide == true ? ", Hidden" : "";
                    WriteLine($"- Spawn at ({mob.X}, {mob.Y}){flipText}{hideText}");
                    if (mob.MobTime.HasValue)
                        WriteLine($"  RespawnTime: {mob.MobTime.Value}ms");
                    if (mob.rx0Shift != 0 || mob.rx1Shift != 0)
                        WriteLine($"  PatrolRange: rx0={mob.rx0Shift}, rx1={mob.rx1Shift}");
                }

                indentLevel--;
            }

            indentLevel--;
            WriteLine();
        }

        private void WriteNPCs()
        {
            var npcs = board.BoardItems.NPCs.ToList();
            if (npcs.Count == 0)
                return;

            WriteLine("## NPCs");
            WriteLine($"Total NPCs: {npcs.Count}");
            indentLevel++;

            foreach (var npc in npcs.OrderBy(n => n.X))
            {
                var npcInfo = npc.NpcInfo;
                var flipText = npc.Flip == true ? ", Flipped" : "";
                var hideText = npc.Hide == true ? ", Hidden" : "";

                WriteLine($"- NPC ID {npcInfo.ID}: \"{npcInfo.StringName ?? "Unknown"}\" at ({npc.X}, {npc.Y}){flipText}{hideText}");
                if (npc.rx0Shift != 0 || npc.rx1Shift != 0)
                {
                    indentLevel++;
                    WriteLine($"PatrolRange: rx0={npc.rx0Shift}, rx1={npc.rx1Shift}");
                    indentLevel--;
                }
            }

            indentLevel--;
            WriteLine();
        }

        private void WriteReactors()
        {
            var reactors = board.BoardItems.Reactors.ToList();
            if (reactors.Count == 0)
                return;

            WriteLine("## Reactors");
            WriteLine($"Total Reactors: {reactors.Count}");
            indentLevel++;

            foreach (var reactor in reactors.OrderBy(r => r.X))
            {
                var reactorInfo = (HaCreator.MapEditor.Info.ReactorInfo)reactor.BaseInfo;
                var flipText = reactor.Flip ? ", Flipped" : "";

                WriteLine($"- Reactor ID {reactorInfo.ID} at ({reactor.X}, {reactor.Y}){flipText}");
                if (!string.IsNullOrEmpty(reactor.Name))
                {
                    indentLevel++;
                    WriteLine($"Name: \"{reactor.Name}\"");
                    indentLevel--;
                }
                if (reactor.ReactorTime != 0)
                {
                    indentLevel++;
                    WriteLine($"RespawnTime: {reactor.ReactorTime}ms");
                    indentLevel--;
                }
            }

            indentLevel--;
            WriteLine();
        }

        private void WriteFootholds()
        {
            var footholds = board.BoardItems.FootholdLines.ToList();
            if (footholds.Count == 0)
                return;

            WriteLine("## Footholds (Platforms)");
            WriteLine($"Total Foothold Segments: {footholds.Count}");
            WriteLine("Note: Footholds define where players can walk. They form connected platform segments.");
            indentLevel++;

            // Group footholds by layer
            var fhByLayer = footholds.GroupBy(fh => fh.LayerNumber);
            foreach (var layerGroup in fhByLayer.OrderBy(g => g.Key))
            {
                WriteLine($"### Layer {layerGroup.Key} ({layerGroup.Count()} segments)");
                indentLevel++;

                // Find continuous platform chains
                var segments = layerGroup.OrderBy(fh => Math.Min(fh.FirstDot.X, fh.SecondDot.X)).ToList();
                foreach (var fh in segments)
                {
                    var cantThrough = fh.CantThrough == true ? " [CantJumpThrough]" : "";
                    var forbidFall = fh.ForbidFallDown == true ? " [NoFallDown]" : "";
                    WriteLine($"- Platform from ({fh.FirstDot.X}, {fh.FirstDot.Y}) to ({fh.SecondDot.X}, {fh.SecondDot.Y}){cantThrough}{forbidFall}");
                }

                indentLevel--;
            }

            indentLevel--;
            WriteLine();
        }

        private void WriteRopes()
        {
            var ropes = board.BoardItems.Ropes.ToList();
            if (ropes.Count == 0)
                return;

            WriteLine("## Ropes and Ladders");
            WriteLine($"Total: {ropes.Count}");
            indentLevel++;

            foreach (var rope in ropes.OrderBy(r => r.FirstAnchor.X))
            {
                var type = rope.ladder ? "Ladder" : "Rope";
                WriteLine($"- {type} from ({rope.FirstAnchor.X}, {rope.FirstAnchor.Y}) to ({rope.SecondAnchor.X}, {rope.SecondAnchor.Y})");
            }

            indentLevel--;
            WriteLine();
        }

        private void WriteChairs()
        {
            var chairs = board.BoardItems.Chairs.ToList();
            if (chairs.Count == 0)
                return;

            WriteLine("## Chairs (Sitting Spots)");
            WriteLine($"Total: {chairs.Count}");
            indentLevel++;

            foreach (var chair in chairs.OrderBy(c => c.X))
            {
                WriteLine($"- Chair at ({chair.X}, {chair.Y})");
            }

            indentLevel--;
            WriteLine();
        }

        private void SerializeItem(BoardItem item)
        {
            indentLevel++;

            switch (item)
            {
                case TileInstance tile:
                    var tileInfo = (HaCreator.MapEditor.Info.TileInfo)tile.BaseInfo;
                    WriteLine($"- Tile at ({tile.X}, {tile.Y}): Layer={tile.Layer.LayerNumber}, Set=\"{tileInfo.tS}\", Type=\"{tileInfo.u}\", No={tileInfo.no}");
                    break;

                case ObjectInstance obj:
                    var objInfo = (HaCreator.MapEditor.Info.ObjectInfo)obj.BaseInfo;
                    WriteLine($"- Object at ({obj.X}, {obj.Y}): Layer={obj.Layer.LayerNumber}, Set=\"{objInfo.oS}\", Path=\"{objInfo.l0}/{objInfo.l1}/{objInfo.l2}\"");
                    break;

                case PortalInstance portal:
                    WriteLine($"- Portal \"{portal.pn}\" at ({portal.X}, {portal.Y}): Type={portal.pt}, Target={portal.tm}/{portal.tn}");
                    break;

                case MobInstance mob:
                    WriteLine($"- Mob ID {mob.MobInfo.ID} at ({mob.X}, {mob.Y})");
                    break;

                case NpcInstance npc:
                    WriteLine($"- NPC ID {npc.NpcInfo.ID} at ({npc.X}, {npc.Y})");
                    break;

                case BackgroundInstance bg:
                    var bgInfo = (HaCreator.MapEditor.Info.BackgroundInfo)bg.BaseInfo;
                    WriteLine($"- Background at ({bg.BaseX}, {bg.BaseY}): Set=\"{bgInfo.bS}\", Type={bg.type}");
                    break;

                case FootholdAnchor fhAnchor:
                    WriteLine($"- FootholdAnchor at ({fhAnchor.X}, {fhAnchor.Y}): Layer={fhAnchor.LayerNumber}");
                    break;

                case Chair chair:
                    WriteLine($"- Chair at ({chair.X}, {chair.Y})");
                    break;

                default:
                    WriteLine($"- {item.GetType().Name} at ({item.X}, {item.Y})");
                    break;
            }

            indentLevel--;
        }

        private void WriteLine(string text = "")
        {
            if (string.IsNullOrEmpty(text))
            {
                sb.AppendLine();
            }
            else
            {
                sb.Append(new string(' ', indentLevel * 2));
                sb.AppendLine(text);
            }
        }
    }
}
