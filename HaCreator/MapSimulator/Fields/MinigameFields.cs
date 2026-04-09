using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using HaSharedLibrary.Wz;
using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Pools;
using MapleLib.Converters;
using MapleLib.PacketLib;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.CompilerServices;


namespace HaCreator.MapSimulator.Fields
{
    /// <summary>
    /// Aggregates minigame field runtimes behind a single simulator surface.
    /// This gives parity work a stable ownership seam before each minigame is
    /// expanded into client-like packet, timerboard, and result handling.
    /// </summary>
    public class MinigameFields
    {
        private GraphicsDevice _graphicsDevice;
        private readonly SnowBallField _snowBall = new();
        private readonly CoconutField _coconut = new();
        private readonly MemoryGameField _memoryGame = new();
        private readonly RockPaperScissorsField _rockPaperScissors = new();
        private readonly AriantArenaField _ariantArena = new();
        private readonly MonsterCarnivalField _monsterCarnival = new();
        private readonly TournamentField _tournament = new();


        public SnowBallField SnowBall => _snowBall;
        public CoconutField Coconut => _coconut;
        public MemoryGameField MemoryGame => _memoryGame;
        public RockPaperScissorsField RockPaperScissors => _rockPaperScissors;
        public AriantArenaField AriantArena => _ariantArena;
        public MonsterCarnivalField MonsterCarnival => _monsterCarnival;
        public TournamentField Tournament => _tournament;


        public void SetSnowBallPlayerState(Vector2? localWorldPosition)
        {
            _snowBall.SetLocalPlayerPosition(localWorldPosition);
        }


        public void Initialize(
            GraphicsDevice graphicsDevice,
            SoundManager soundManager = null,
            Func<LoginAvatarLook, string, CharacterBuild> ariantArenaRemoteBuildFactory = null)
        {
            _graphicsDevice = graphicsDevice;

            _coconut.Initialize(graphicsDevice);
            _memoryGame.Initialize(graphicsDevice);
            _rockPaperScissors.Initialize(graphicsDevice);
            _monsterCarnival.Initialize(graphicsDevice);

            _ariantArena.Initialize(graphicsDevice, soundManager, ariantArenaRemoteBuildFactory);
        }

        public void SetAriantArenaRemoteUserPool(RemoteUserActorPool remoteUserPool)
        {
            _ariantArena.SetRemoteUserPool(remoteUserPool);
        }


        public void BindMap(Board board)

        {

            _snowBall.BindMap(board, _graphicsDevice);

            _coconut.BindMap(board);
            _tournament.Configure(board?.MapInfo);
        }



        public void Update(int tickCount)
        {
            if (_snowBall.IsActive || _snowBall.State != SnowBallField.GameState.NotStarted)
            {
                _snowBall.Update(tickCount);
            }


            if (_coconut.IsActive)
            {
                _coconut.Update(tickCount);
            }


            if (_memoryGame.IsVisible)
            {
                _memoryGame.Update(tickCount);
            }

            if (_rockPaperScissors.IsVisible)
            {
                _rockPaperScissors.Update(tickCount);
            }


            if (_ariantArena.IsActive)
            {
                _ariantArena.Update(tickCount);
            }


            if (_monsterCarnival.IsVisible)
            {
                _monsterCarnival.Update(tickCount);
            }

            if (_tournament.IsActive)
            {
                _tournament.Update(tickCount);
            }
        }


        public void Draw(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int tickCount,
            Texture2D pixelTexture,
            SpriteFont font = null)
        {
            if (_snowBall.IsActive || _snowBall.State != SnowBallField.GameState.NotStarted)
            {
                _snowBall.Draw(
                    spriteBatch,
                    skeletonMeshRenderer,
                    gameTime,
                    mapShiftX,
                    mapShiftY,
                    centerX,
                    centerY,
                    tickCount,
                    pixelTexture,
                    font);
            }


            if (_coconut.IsActive)
            {
                _coconut.Draw(
                    spriteBatch,
                    skeletonMeshRenderer,
                    gameTime,
                    mapShiftX,
                    mapShiftY,
                    centerX,
                    centerY,
                    tickCount,
                    pixelTexture,
                    font);
            }


            if (_memoryGame.IsVisible)
            {
                _memoryGame.Draw(
                    spriteBatch,
                    skeletonMeshRenderer,
                    gameTime,
                    mapShiftX,
                    mapShiftY,
                    centerX,
                    centerY,
                    tickCount,
                    pixelTexture,
                    font);
            }

            if (_rockPaperScissors.IsVisible)
            {
                _rockPaperScissors.Draw(spriteBatch, pixelTexture, font);
            }


            if (_ariantArena.IsActive)
            {
                _ariantArena.Draw(
                    spriteBatch,
                    skeletonMeshRenderer,
                    gameTime,
                    mapShiftX,
                    mapShiftY,
                    centerX,
                    centerY,
                    tickCount,
                    pixelTexture,
                    font);
            }


            if (_monsterCarnival.IsVisible)
            {
                _monsterCarnival.Draw(spriteBatch, pixelTexture, font);
            }

            if (_tournament.IsActive)
            {
                _tournament.Draw(spriteBatch, pixelTexture, font);
            }
        }


        public void ResetAll()
        {
            _snowBall.Reset();
            _coconut.Reset();
            _memoryGame.Reset();
            _rockPaperScissors.Reset();
            _ariantArena.Reset();
            _monsterCarnival.Reset();
            _tournament.Reset();
        }
    }
}
