using System.Reflection;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingPoint = System.Drawing.Point;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Pools;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib.WzStructure.Data.MobStructure;
using MapleLib.WzLib.WzStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;

namespace UnitTest_MapSimulator
{
    public class MobPoolRespawnTests
    {
        [Fact]
        public void Initialize_ConvertsLifeMobTimeSecondsToRespawnMilliseconds()
        {
            var mob = CreateMob(mobTimeSeconds: 12, yShift: 20);
            var pool = new MobPool();

            pool.Initialize(new[] { mob });

            MobSpawnPoint spawnPoint = GetSpawnPoints(pool).Single();

            Assert.Equal(12000, spawnPoint.RespawnTimeMs);
            Assert.Equal(20, spawnPoint.YShift);
        }

        [Fact]
        public void Update_RespawnsMobWhenSpawnPointBecomesReady()
        {
            var initialMob = CreateMob(mobTimeSeconds: 2);
            var respawnedMob = CreateMob(mobTimeSeconds: 2);
            var pool = new MobPool();
            int spawnCount = 0;

            pool.Initialize(new[] { initialMob });
            pool.ConfigureSpawnModel(
                mapWidth: 1024,
                mapHeight: 768,
                mobRate: 1.5f,
                createMobIntervalMs: 1000,
                fieldLimit: 0);
            pool.SetOnMobSpawned(_ => spawnCount++);

            MobSpawnPoint spawnPoint = GetSpawnPoints(pool).Single();
            spawnPoint.IsActive = false;
            spawnPoint.CurrentMob = null;
            spawnPoint.NextSpawnTime = 4000;

            pool.Update(3000, _ => respawnedMob);
            Assert.Equal(0, spawnCount);
            Assert.False(spawnPoint.IsActive);

            pool.Update(4000, _ => respawnedMob);

            Assert.Equal(1, spawnCount);
            Assert.True(spawnPoint.IsActive);
            Assert.Same(respawnedMob, spawnPoint.CurrentMob);
            Assert.Equal(1, pool.ActiveMobCount);
        }

        [Fact]
        public void Update_MobTimeZeroRespawnsOnGlobalRespawnTick()
        {
            var initialMob = CreateMob(mobTimeSeconds: 0);
            var respawnedMob = CreateMob(mobTimeSeconds: 0);
            var pool = new MobPool();
            int spawnCount = 0;

            pool.Initialize(new[] { initialMob });
            pool.ConfigureSpawnModel(
                mapWidth: 1024,
                mapHeight: 768,
                mobRate: 1.5f,
                createMobIntervalMs: 1000,
                fieldLimit: 0);
            pool.SetOnMobSpawned(_ => spawnCount++);

            MobSpawnPoint spawnPoint = GetSpawnPoints(pool).Single();
            spawnPoint.IsActive = false;
            spawnPoint.CurrentMob = null;
            spawnPoint.NextSpawnTime = 0;

            pool.Update(0, _ => respawnedMob);
            Assert.Equal(0, spawnCount);

            pool.Update(1000, _ => respawnedMob);

            Assert.Equal(1, spawnCount);
            Assert.True(spawnPoint.IsActive);
            Assert.Same(respawnedMob, spawnPoint.CurrentMob);
        }

        [Fact]
        public void TrimInitialPopulation_UsesMapCapacityDerivedFromMapSizeAndMobRate()
        {
            var pool = new MobPool();
            var mobs = Enumerable.Range(0, 12)
                .Select(_ => CreateMob(mobTimeSeconds: 2))
                .ToArray();

            pool.Initialize(mobs);
            pool.ConfigureSpawnModel(
                mapWidth: 1024,
                mapHeight: 768,
                mobRate: 1.5f,
                createMobIntervalMs: 9000,
                fieldLimit: 0);

            pool.TrimInitialPopulation();

            Assert.Equal(9, pool.MinRegularSpawnAtOnce);
            Assert.Equal(18, pool.MaxRegularSpawnAtOnce);
            Assert.Equal(9, pool.ActiveMobCount);
        }

        private static List<MobSpawnPoint> GetSpawnPoints(MobPool pool)
        {
            return (List<MobSpawnPoint>)typeof(MobPool)
                .GetField("_spawnPoints", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(pool)!;
        }

        private static MobItem CreateMob(int? mobTimeSeconds = null, int yShift = 0)
        {
            var mobInfo = new MobInfo(new DrawingBitmap(1, 1), DrawingPoint.Empty, "100100", "Test Mob", null);
            typeof(MobInfo)
                .GetField("_mobData", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(mobInfo, new MobData());

            var mobInstance = new MobInstance(
                mobInfo,
                board: null,
                x: 100,
                y: 200,
                rx0Shift: 15,
                rx1Shift: 25,
                yShift: yShift,
                limitedname: "test",
                mobTime: mobTimeSeconds,
                flip: (MapleBool)MapleBool.False,
                hide: (MapleBool)MapleBool.False,
                info: 7,
                team: 2);

            var animationSet = new MobAnimationSet();
            animationSet.AddAnimation(AnimationKeys.Stand, new List<IDXObject> { new TestDxObject(80, 140, 40, 50, 100) });

            var mob = new MobItem(mobInstance, animationSet, null);
            mob.AI.SetState(MobAIState.Idle, 0);
            return mob;
        }

        private sealed class TestDxObject : IDXObject
        {
            public TestDxObject(int x, int y, int width, int height, int delay)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
                Delay = delay;
            }

            public void DrawObject(SpriteBatch sprite, SkeletonMeshRenderer meshRenderer, GameTime gameTime, int mapShiftX, int mapShiftY, bool flip, ReflectionDrawableBoundary drawReflectionInfo)
            {
            }

            public void DrawBackground(SpriteBatch sprite, SkeletonMeshRenderer meshRenderer, GameTime gameTime, int x, int y, Microsoft.Xna.Framework.Color color, bool flip, ReflectionDrawableBoundary drawReflectionInfo)
            {
            }

            public int Delay { get; }
            public int X { get; }
            public int Y { get; }
            public int Width { get; }
            public int Height { get; }
            public object Tag { get; set; } = new();
        }
    }
}
