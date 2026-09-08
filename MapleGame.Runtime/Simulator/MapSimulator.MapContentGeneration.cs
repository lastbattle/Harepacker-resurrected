using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Companions;
using HaCreator.MapSimulator.Contracts;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Loaders;
using HaCreator.MapSimulator.Physics;
using HaSharedLibrary.Wz;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Animation;
using MobItem = HaCreator.MapSimulator.Entities.MobItem;
using HaSharedLibrary;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib;
using MapleLib.PacketLib;
using MapleLib.WzLib;
using MapleLib.WzLib.Spine;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.Util;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using MapleLib.WzLib.WzStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SD = System.Drawing;
using SDText = System.Drawing.Text;
using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Core;
using HaCreator.MapSimulator.Combat;
using MapleLib.Helpers;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;


namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private MapContentGeneration _activeContent = new();
        private MapContentGeneration _stagingContent;
        private MapContentGeneration ContentTarget => _stagingContent ?? _activeContent;

        private sealed class MapContentGeneration : IDisposable
        {
            public RuntimeMapDefinition Definition;
            public MapInfo Info;
            public RuntimePhysicsSnapshot Physics;
            public IReadOnlyList<RuntimePortal> RuntimePortals = Array.Empty<RuntimePortal>();
            public readonly TexturePool SceneryTextures = new();
            public readonly TexturePool MapTextures = new();
            public readonly UILoaderResourceCache UiResourceCache = new();
            public MobPool MobPool;
            public DropPool DropPool;
            public PortalPool PortalPool;
            public ReactorPool ReactorPool;
            public TemporaryPortalField TemporaryPortalField;
            public FieldRuleRuntime FieldRuleRuntime;
            public PacketFieldStateRuntime PacketFieldStateRuntime;
            public SpecialFieldRuntimeCoordinator SpecialFieldRuntime;
            public MinimapUI Minimap;
            public StatusBarUI StatusBar;
            public StatusBarChatUI StatusBarChat;
            public IReadOnlyDictionary<string, BuffIconCatalogEntry> BuffIconCatalog;
            public MouseCursorItem MouseCursor;
            public UIWindowManager WindowManager;
            public string WindowTitle = string.Empty;
            public bool IsLoginMap;
            public bool IsCashShopMap;
            public float SpawnX;
            public float SpawnY;
            public TransportationField TransportField = new();
            public bool HasTransportationField;
            public int mapShiftX = 0;
            public int mapShiftY = 0;
            public Point minimapPos;
            public int _mapCenterX = 0;
            public int _mapCenterY = 0;
            public List<BaseDXDrawableItem>[] mapObjects = new List<BaseDXDrawableItem>[MapConstants.MaxMapLayers];
            public List<BaseDXDrawableItem> mapObjects_NPCs = new List<BaseDXDrawableItem>();
            public List<BaseDXDrawableItem> mapObjects_Mobs = new List<BaseDXDrawableItem>();
            public List<BaseDXDrawableItem> mapObjects_Reactors = new List<BaseDXDrawableItem>();
            public List<BaseDXDrawableItem> mapObjects_Portal = new List<BaseDXDrawableItem>();
            public List<BaseDXDrawableItem> mapObjects_tooltips = new List<BaseDXDrawableItem>();
            public BaseDXDrawableItem[][] _mapObjectsArray;
            public Dictionary<BaseDXDrawableItem, QuestGatedMapObjectState> _questGatedMapObjects = new();
            public Dictionary<string, bool> _authoredDynamicObjectTagStates = new(StringComparer.OrdinalIgnoreCase);
            public List<FieldObjectDirectionEventTriggerPoint> _dynamicObjectDirectionEventTriggers = new();
            public HashSet<int> _triggeredDynamicObjectDirectionEventIndices = new();
            public List<ScheduledDynamicObjectScriptPublication> _scheduledDynamicObjectScriptPublications = new();
            public NpcItem[] _npcsArray;
            public Dictionary<int, NpcItem> _npcsById = new();
            public int LastNpcClientActionSelectionContextStamp = int.MinValue;
            public MobItem[] _mobsArray;
            public ReactorItem[] _reactorsArray;
            public PortalItem[] _portalsArray;
            public TooltipItem[] _tooltipsArray;
            public List<BackgroundItem> backgrounds_front = new List<BackgroundItem>();
            public List<BackgroundItem> backgrounds_back = new List<BackgroundItem>();
            public BackgroundItem[] _backgroundsFrontArray;
            public BackgroundItem[] _backgroundsBackArray;
            public SpatialGrid<BaseDXDrawableItem> _mapObjectsGrid;
            public SpatialGrid<PortalItem> _portalsGrid;
            public SpatialGrid<ReactorItem> _reactorsGrid;
            public bool _useSpatialPartitioning = false;
            public BaseDXDrawableItem[] _visibleMapObjects;
            public int _visibleMapObjectsCount;
            public PortalItem[] _visiblePortals;
            public int _visiblePortalsCount;
            public ReactorItem[] _visibleReactors;
            public int _visibleReactorsCount;
            public MobItem[] _visibleMobs;
            public int _visibleMobsCount;
            public NpcItem[] _visibleNpcs;
            public int _visibleNpcsCount;
            public TooltipItem[] _visibleTooltips;
            public int _visibleTooltipsCount;
            public bool[] _reactorVisibilityBuffer;
            public Rectangle _vrFieldBoundary;
            public Rectangle _vrRectangle;
            public bool _drawVRBorderLeftRight = false;
            public Rectangle _mirrorBottomRect;
            public ReflectionDrawableBoundary _mirrorBottomReflection;
            public string _mapBgmName = null;
            public string _spawnPortalName = null;
            public string[] _spawnPortalNameCandidates = Array.Empty<string>();
            public int _spawnPortalIndex = -1;
            public Texture2D _vrBoundaryTextureLeft;
            public Texture2D _vrBoundaryTextureRight;
            public Texture2D _vrBoundaryTextureTop;
            public Texture2D _vrBoundaryTextureBottom;
            public Texture2D _lbTextureLeft;
            public Texture2D _lbTextureRight;
            public Texture2D _lbTextureTop;
            public Texture2D _lbTextureBottom;
            public int _lbSide = 0;
            public int _lbTop = 0;
            public int _lbBottom = 0;
            private bool disposed;
            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                List<Exception> cleanupErrors = new();
                void Cleanup(Action action)
                {
                    try { action(); }
                    catch (Exception error) { cleanupErrors.Add(error); }
                }

                Cleanup(() => Definition?.Dispose());
                Cleanup(() => Info?.Image?.Dispose());
                Cleanup(() => SceneryTextures.Dispose());
                Cleanup(() => MapTextures.Dispose());
                Cleanup(() => UiResourceCache.Dispose());
                Cleanup(() => (WindowManager as IDisposable)?.Dispose());
                Cleanup(() => PacketFieldStateRuntime?.Dispose());
                Cleanup(() => SpecialFieldRuntime?.Retire());
                foreach (Texture2D texture in new[] { _vrBoundaryTextureLeft, _vrBoundaryTextureRight,
                    _vrBoundaryTextureTop, _vrBoundaryTextureBottom, _lbTextureLeft, _lbTextureRight,
                    _lbTextureTop, _lbTextureBottom }.Where(texture => texture != null).Distinct())
                    Cleanup(texture.Dispose);
                mapObjects_NPCs.Clear(); mapObjects_Mobs.Clear(); mapObjects_Reactors.Clear();
                mapObjects_Portal.Clear(); mapObjects_tooltips.Clear();
                backgrounds_front.Clear(); backgrounds_back.Clear(); _questGatedMapObjects.Clear();
                _authoredDynamicObjectTagStates.Clear(); _dynamicObjectDirectionEventTriggers.Clear();
                _triggeredDynamicObjectDirectionEventIndices.Clear(); _scheduledDynamicObjectScriptPublications.Clear();
                _npcsById.Clear();

                // Pool callbacks resolve simulator fields dynamically. Clearing a retired
                // generation after the active swap could therefore mutate the new map.
                // Dropping the generation references is sufficient; GPU assets are owned
                // and retired by the two texture pools above.
                MobPool = null; DropPool = null; PortalPool = null; ReactorPool = null;
                TemporaryPortalField = null; FieldRuleRuntime = null;
                PacketFieldStateRuntime = null;
                SpecialFieldRuntime = null;

                if (cleanupErrors.Count > 0)
                {
                    throw new AggregateException("One or more map-content resources failed to retire.", cleanupErrors);
                }
            }
        }
    }
}
