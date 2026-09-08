using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Instance.Misc;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapSimulator.Contracts;
using MapleLib.WzLib;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Bitmap = System.Drawing.Bitmap;

namespace HaCreator.MapEditor.Simulation
{
    /// <summary>
    /// Copies editor state into a detached runtime definition. The caller must hold the editor's
    /// snapshot/render barrier for the duration of this method.
    /// </summary>
    public sealed class BoardSnapshotBuilder
    {
        private readonly Func<WzImage, bool> _captureOverride;
        private readonly Dictionary<RuntimeAssetKey, RuntimeOwnedAssetOverride> _overrides = new();
        private readonly Dictionary<FootholdAnchor, int> _footholdAnchorIds =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<FootholdLine, int> _footholdNumberIds =
            new(ReferenceEqualityComparer.Instance);
        private readonly HashSet<int> _usedFootholdNumbers = new();
        private int _nextFootholdAnchorId;
        private int _nextSyntheticFootholdNumber;

        public BoardSnapshotBuilder(Func<WzImage, bool> captureOverride = null)
        {
            _captureOverride = captureOverride ?? (image => image != null && (image.Changed || image.WzFileParent == null));
        }

        public RuntimeMapDefinition Create(Board board)
        {
            ArgumentNullException.ThrowIfNull(board);
            _overrides.Clear();
            _footholdAnchorIds.Clear();
            _footholdNumberIds.Clear();
            _usedFootholdNumbers.Clear();
            _nextFootholdAnchorId = 1;
            _nextSyntheticFootholdNumber = int.MinValue + 1;
            try
            {
                var tiles = new List<RuntimeTileDefinition>();
                var objects = new List<RuntimeObjectDefinition>();
                int drawOrder = 0;
                foreach (LayeredItem item in board.BoardItems.TileObjs)
                {
                    if (item is TileInstance tile)
                        tiles.Add(CreateTile(tile, drawOrder++));
                    else if (item is ObjectInstance mapObject)
                        objects.Add(CreateObject(mapObject, drawOrder++));
                }

                List<RuntimeBackgroundDefinition> backgrounds = new();
                drawOrder = 0;
                backgrounds.AddRange(board.BoardItems.BackBackgrounds.Select(item => CreateBackground(item, drawOrder++)));
                backgrounds.AddRange(board.BoardItems.FrontBackgrounds.Select(item => CreateBackground(item, drawOrder++)));

                Rectangle? virtualBounds = board.VRRectangle == null
                    ? null
                    : new Rectangle(board.VRRectangle.X, board.VRRectangle.Y,
                        board.VRRectangle.Width, board.VRRectangle.Height);
                using Bitmap generatedMinimap = board.MiniMap == null ? CreatePreviewMinimap(board) : null;
                return new RuntimeMapDefinition(
                    board.MapInfo,
                    board.MapSize,
                    board.CenterPoint,
                    virtualBounds,
                    board.MinimapArea,
                    generatedMinimap == null ? board.MinimapPosition :
                        new System.Drawing.Point(board.MinimapRectangle.X, board.MinimapRectangle.Y),
                    EncodePng(board.MiniMap ?? generatedMinimap),
                    tiles,
                    objects,
                    backgrounds,
                    board.BoardItems.Mobs.Select(CreateMob),
                    board.BoardItems.NPCs.Select(CreateNpc),
                    board.BoardItems.Reactors.Select(CreateReactor),
                    board.BoardItems.Portals.Select(CreatePortal),
                    board.BoardItems.FootholdLines.Select(CreateFoothold),
                    board.BoardItems.Ropes.Select(CreateRope),
                    board.BoardItems.Chairs.Select(chair => new RuntimeChairDefinition(chair.X, chair.Y)),
                    board.BoardItems.ToolTips.Select(CreateTooltip),
                    // Rectangle handles are editor controls, not map content.
                    board.BoardItems.MiscItems.Where(item => item is not MiscDot).Select(CreateMisc),
                    board.BoardItems.MirrorFieldDatas.Select(CreateMirrorField),
                    _overrides.Values);
            }
            finally
            {
                foreach (RuntimeOwnedAssetOverride assetOverride in _overrides.Values)
                    assetOverride.Dispose();
                _overrides.Clear();
                _footholdAnchorIds.Clear();
                _footholdNumberIds.Clear();
                _usedFootholdNumbers.Clear();
            }
        }

        private static Bitmap CreatePreviewMinimap(Board board)
        {
            var bounds = board.MinimapRectangle;
            if (bounds == null || bounds.Width <= 0 || bounds.Height <= 0 || board.mag <= 0)
                return null;

            // Render into an owned bitmap; preview must not assign Board.MiniMap or
            // update editor geometry, dirty state, or the undo history.
            using Bitmap map = new(board.MapSize.X, board.MapSize.Y);
            using (var graphics = System.Drawing.Graphics.FromImage(map))
            {
                foreach (BoardItem item in board.BoardItems.TileObjs)
                {
                    Bitmap image = item.Image;
                    if (image == null) continue;
                    var destination = new System.Drawing.Rectangle(
                        item.X + board.CenterPoint.X - item.Origin.X,
                        item.Y + board.CenterPoint.Y - item.Origin.Y, image.Width, image.Height);
                    if (item.IsFlipped())
                        graphics.DrawImage(image, destination, image.Width, 0, -image.Width,
                            image.Height, System.Drawing.GraphicsUnit.Pixel);
                    else
                        graphics.DrawImage(image, destination);
                }
            }
            using Bitmap cropped = Board.CropImage(map, new System.Drawing.Rectangle(
                bounds.X + board.CenterPoint.X, bounds.Y + board.CenterPoint.Y, bounds.Width, bounds.Height));
            return Board.ResizeImage(cropped, board.mag);
        }

        private RuntimeTileDefinition CreateTile(TileInstance item, int order)
        {
            TileInfo info = (TileInfo)item.BaseInfo;
            RuntimeAssetKey asset = CaptureAsset("Map/Tile", info.ParentObject, info.Image);
            return new RuntimeTileDefinition(asset, item.X, item.Y, item.Z, item.LayerNumber,
                item.PlatformNumber, order, info.tS, info.u, info.no, info.mag, info.z);
        }

        private RuntimeObjectDefinition CreateObject(ObjectInstance item, int order)
        {
            ObjectInfo info = (ObjectInfo)item.BaseInfo;
            RuntimeAssetKey asset = CaptureAsset("Map/Obj", info.ParentObject, info.Image);
            return new RuntimeObjectDefinition
            {
                Asset = asset,
                X = item.X,
                Y = item.Y,
                Z = item.Z,
                Layer = item.LayerNumber,
                Platform = item.PlatformNumber,
                DrawOrder = order,
                ObjectSet = info.oS,
                L0 = info.l0,
                L1 = info.l1,
                L2 = info.l2,
                Flip = item.Flip,
                Rotation = item.r,
                Hide = item.hide,
                Reactor = item.reactor,
                Dynamic = item.Dynamic,
                Flow = item.flow,
                Rx = item.rx,
                Ry = item.ry,
                Cx = item.cx,
                Cy = item.cy,
                Origin = info.Origin,
                Name = item.Name,
                Tags = item.tags,
                Quests = item.QuestInfo?.Select(q =>
                    new RuntimeObjectQuestDefinition(q.questId, (int)q.state)).ToArray()
                    ?? Array.Empty<RuntimeObjectQuestDefinition>()
            };
        }

        private RuntimeBackgroundDefinition CreateBackground(BackgroundInstance item, int order)
        {
            BackgroundInfo info = (BackgroundInfo)item.BaseInfo;
            RuntimeAssetKey asset = CaptureAsset("Map/Back", info.ParentObject, info.Image);
            return new RuntimeBackgroundDefinition(asset, item.BaseX, item.BaseY, item.Z, order,
                info.bS, info.no, (int)item.type, item.rx, item.ry, item.cx, item.cy, item.a,
                item.front, item.Flip, item.Page, item.screenMode, item.SpineAni, item.SpineRandomStart);
        }

        private RuntimeLifeDefinition CreateMob(MobInstance item)
        {
            MobInfo info = item.MobInfo;
            RuntimeAssetKey asset = CaptureAsset("Mob", info.LinkedWzImage, info.Image);
            return CreateLife(item, asset, info.ID, info.Name);
        }

        private RuntimeLifeDefinition CreateNpc(NpcInstance item)
        {
            NpcInfo info = item.NpcInfo;
            RuntimeAssetKey asset = CaptureAsset("Npc", info.LinkedWzImage, info.Image);
            return CreateLife(item, asset, info.ID, info.StringName);
        }

        private static RuntimeLifeDefinition CreateLife(LifeInstance item, RuntimeAssetKey asset, string id, string name)
        {
            return new RuntimeLifeDefinition
            {
                Asset = asset,
                Id = id,
                DisplayName = name,
                X = item.X,
                Y = item.Y,
                Z = item.Z,
                Flip = item.Flip,
                LimitedName = item.LimitedName,
                Hide = item.Hide,
                Rx0Shift = item.rx0Shift,
                Rx1Shift = item.rx1Shift,
                YShift = item.yShift,
                MobTime = item.MobTime,
                Info = item.Info,
                Team = item.Team
            };
        }

        private RuntimeReactorDefinition CreateReactor(ReactorInstance item)
        {
            ReactorInfo info = item.ReactorInfo;
            RuntimeAssetKey asset = CaptureAsset("Reactor", info.LinkedWzImage, info.Image);
            return new RuntimeReactorDefinition(asset, info.ID, info.Name, item.X, item.Y, item.Z,
                item.Flip, item.ReactorTime, item.Name);
        }

        private RuntimePortalDefinition CreatePortal(PortalInstance item)
        {
            RuntimeAssetKey asset = CaptureAsset("Map", item.BaseInfo?.ParentObject, item.BaseInfo?.Image);
            return new RuntimePortalDefinition
            {
                Asset = asset,
                X = item.X,
                Y = item.Y,
                Z = item.Z,
                Image = item.image,
                Name = item.pn,
                Type = item.pt,
                TargetName = item.tn,
                TargetMapId = item.tm,
                Script = item.script,
                Delay = item.delay,
                HideTooltip = item.hideTooltip,
                OnlyOnce = item.onlyOnce,
                HorizontalImpact = item.horizontalImpact,
                VerticalImpact = item.verticalImpact,
                HorizontalRange = item.hRange,
                VerticalRange = item.vRange,
                ReactorName = item.reactorName,
                SessionValueKey = item.sessionValueKey,
                SessionValue = item.sessionValue
            };
        }

        private RuntimeFootholdDefinition CreateFoothold(FootholdLine item)
        {
            int number = GetFootholdNumber(item);
            return new RuntimeFootholdDefinition(number, item.prev, item.next,
                item.FirstDot.X, item.FirstDot.Y, item.SecondDot.X, item.SecondDot.Y,
                item.LayerNumber, item.PlatformNumber, item.Force, item.Piece,
                item.ForbidFallDown, item.CantThrough,
                GetFootholdAnchorId(item.FirstDot), GetFootholdAnchorId(item.SecondDot));
        }

        private int GetFootholdAnchorId(MapleDot dot)
        {
            if (dot is not FootholdAnchor anchor)
                return 0;
            if (!_footholdAnchorIds.TryGetValue(anchor, out int id))
                _footholdAnchorIds.Add(anchor, id = _nextFootholdAnchorId++);
            return id;
        }

        private int GetFootholdNumber(FootholdLine foothold)
        {
            if (_footholdNumberIds.TryGetValue(foothold, out int number))
                return number;

            number = foothold.num;
            if (!_usedFootholdNumbers.Add(number))
            {
                do
                {
                    number = _nextSyntheticFootholdNumber++;
                }
                while (!_usedFootholdNumbers.Add(number));
            }
            _footholdNumberIds.Add(foothold, number);
            return number;
        }

        private static RuntimeRopeDefinition CreateRope(Rope item)
        {
            return new RuntimeRopeDefinition(item.FirstAnchor.X, item.FirstAnchor.Y, item.SecondAnchor.Y,
                item.LayerNumber, item.ladder, item.ladderSetByUser, item.uf);
        }

        private static RuntimeTooltipDefinition CreateTooltip(ToolTipInstance item)
        {
            return new RuntimeTooltipDefinition(item.Rectangle, item.Title, item.Desc, item.OriginalNumber,
                item.CharacterToolTip?.Rectangle);
        }

        private RuntimeMiscDefinition CreateMisc(BoardItem item)
        {
            return item switch
            {
                Area area => new RuntimeMiscDefinition { Kind = RuntimeMiscKind.Area, Bounds = area.Rectangle, Identifier = area.Identifier },
                SwimArea swim => new RuntimeMiscDefinition { Kind = RuntimeMiscKind.SwimArea, Bounds = swim.Rectangle, Identifier = swim.Identifier },
                BuffZone buff => new RuntimeMiscDefinition { Kind = RuntimeMiscKind.BuffZone, Bounds = buff.Rectangle, Identifier = buff.ZoneName, ItemId = buff.ItemID, Interval = buff.Interval, Duration = buff.Duration },
                Clock clock => new RuntimeMiscDefinition { Kind = RuntimeMiscKind.Clock, Bounds = clock.Rectangle },
                Healer healer => new RuntimeMiscDefinition { Kind = RuntimeMiscKind.Healer, Bounds = new Rectangle(healer.X, healer.yMin, 0, healer.yMax - healer.yMin), Asset = CaptureAsset("Map/Obj", healer.BaseInfo?.ParentObject, healer.BaseInfo?.Image), YMin = healer.yMin, YMax = healer.yMax, HealMin = healer.healMin, HealMax = healer.healMax, Fall = healer.fall, Rise = healer.rise },
                Pulley pulley => new RuntimeMiscDefinition { Kind = RuntimeMiscKind.Pulley, Bounds = new Rectangle(pulley.X, pulley.Y, pulley.Width, pulley.Height), Asset = CaptureAsset("Map/Obj", pulley.BaseInfo?.ParentObject, pulley.BaseInfo?.Image) },
                ShipObject ship => new RuntimeMiscDefinition { Kind = RuntimeMiscKind.Ship, Bounds = new Rectangle(ship.X, ship.Y, ship.Width, ship.Height), Asset = CaptureAsset("Map/Obj", ship.BaseInfo?.ParentObject, ship.BaseInfo?.Image), X0 = ship.X0, ZValue = ship.zValue, TimeMove = ship.TimeMove, ShipKind = ship.ShipKind, Flip = ship.Flip },
                _ => new RuntimeMiscDefinition { Kind = RuntimeMiscKind.Unknown, Bounds = new Rectangle(item.X, item.Y, item.Width, item.Height), Identifier = item.GetType().FullName }
            };
        }

        private static RuntimeMirrorFieldDefinition CreateMirrorField(MirrorFieldData item)
        {
            var reflection = item.ReflectionInfo;
            return new RuntimeMirrorFieldDefinition(item.Rectangle, (int)item.MirrorFieldDataType, item.Offset,
                new RuntimeReflectionDefinition(reflection.Gradient, reflection.Alpha,
                    reflection.ObjectForOverlay, reflection.Reflection, reflection.AlphaTest));
        }

        private RuntimeAssetKey CaptureAsset(string category, WzObject source, Bitmap preview)
        {
            WzImage root = FindImageRoot(source);
            var segments = new Stack<string>();
            for (WzObject current = source; current != null; current = current.Parent)
            {
                segments.Push(current.Name);
                if (ReferenceEquals(current, root)) break;
            }
            string path = root == null ? $"memory:{category}:{_overrides.Count}" : string.Join("/", segments);
            RuntimeAssetKey key = new(category, path);
            if ((root != null && _captureOverride(root)) || source == null)
                _overrides.TryAdd(key, new RuntimeOwnedAssetOverride(key, root, EncodePng(preview)));
            return key;
        }

        private static WzImage FindImageRoot(WzObject source)
        {
            for (WzObject current = source; current != null; current = current.Parent)
                if (current is WzImage image)
                    return image;
            return null;
        }

        private static byte[] EncodePng(Bitmap bitmap)
        {
            if (bitmap == null)
                return Array.Empty<byte>();
            using MemoryStream stream = new();
            bitmap.Save(stream, ImageFormat.Png);
            return stream.ToArray();
        }
    }
}
