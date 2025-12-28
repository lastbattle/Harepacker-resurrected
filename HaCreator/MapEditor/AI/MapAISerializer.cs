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

            if (hasElements)
            {
                WriteLine($"Content Bounds: X=[{minX} to {maxX}], Y=[{minY} to {maxY}]");
                WriteLine($"** IMPORTANT: Place new elements within these bounds! **");
            }
            WriteLine();

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

            // Key portals
            if (board.BoardItems.Portals.Count > 0)
            {
                WriteLine("## Portals");
                foreach (var portal in board.BoardItems.Portals.OrderBy(p => p.pn))
                {
                    var targetInfo = portal.tm != 999999999 ? $" -> Map {portal.tm}" : "";
                    WriteLine($"- \"{portal.pn}\" at ({portal.X}, {portal.Y}) [{portal.pt}]{targetInfo}");
                }
                WriteLine();
            }

            // Mob summary
            if (board.BoardItems.Mobs.Count > 0)
            {
                WriteLine("## Mobs");
                var mobGroups = board.BoardItems.Mobs.GroupBy(m => m.MobInfo.ID);
                foreach (var group in mobGroups)
                {
                    var name = group.First().MobInfo.Name ?? "Unknown";
                    WriteLine($"- Mob {group.Key} \"{name}\": {group.Count()} spawn(s)");
                }
                WriteLine();
            }

            // NPC summary
            if (board.BoardItems.NPCs.Count > 0)
            {
                WriteLine("## NPCs");
                foreach (var npc in board.BoardItems.NPCs)
                {
                    WriteLine($"- NPC {npc.NpcInfo.ID} \"{npc.NpcInfo.StringName}\" at ({npc.X}, {npc.Y})");
                }
                WriteLine();
            }

            // Footholds/Platforms summary
            if (board.BoardItems.FootholdLines.Count > 0)
            {
                WriteLine("## Platforms (Footholds)");
                var fhByLayer = board.BoardItems.FootholdLines.GroupBy(fh => fh.LayerNumber);
                foreach (var layerGroup in fhByLayer.OrderBy(g => g.Key))
                {
                    WriteLine($"### Layer {layerGroup.Key}");
                    foreach (var fh in layerGroup.OrderBy(f => Math.Min(f.FirstDot.X, f.SecondDot.X)))
                    {
                        var props = new List<string>();
                        if (fh.CantThrough == true) props.Add("CantJumpThrough");
                        if (fh.ForbidFallDown == true) props.Add("NoFallDown");
                        var propsStr = props.Count > 0 ? $" [{string.Join(", ", props)}]" : "";
                        WriteLine($"- ({fh.FirstDot.X}, {fh.FirstDot.Y}) to ({fh.SecondDot.X}, {fh.SecondDot.Y}){propsStr}");
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

            // Include asset catalog for AI awareness
            sb.Append(MapAssetCatalog.GenerateCompactSummary());

            return sb.ToString();
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
