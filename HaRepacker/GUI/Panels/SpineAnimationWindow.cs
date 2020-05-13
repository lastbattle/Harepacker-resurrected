using MapleLib.WzLib.Spine;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace HaRepacker.GUI.Panels
{
	public class SpineAnimationWindow : Microsoft.Xna.Framework.Game
    {
		private GraphicsDeviceManager graphicsDeviceMgr;

		private SkeletonMeshRenderer skeletonRenderer;

		private readonly SpineAnimationItem spineAnimationItem;

		private Skeleton skeleton;
		private AnimationState state;
		private readonly SkeletonBounds bounds = new SkeletonBounds();

		// Text
		private SpriteBatch spriteBatch;
		private SpriteFont font;


		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="spineAnimationItem"></param>
		/// <param name="skeletonData"></param>
		public SpineAnimationWindow(SpineAnimationItem spineAnimationItem)
		{
			IsMouseVisible = true;

			graphicsDeviceMgr = new GraphicsDeviceManager(this);
			graphicsDeviceMgr.IsFullScreen = false;
			graphicsDeviceMgr.PreferredBackBufferWidth = 1366;
			graphicsDeviceMgr.PreferredBackBufferHeight = 768;

			this.spineAnimationItem = spineAnimationItem;
		}
		protected override void Initialize()
		{
			// TODO: Add your initialization logic here

			// https://stackoverflow.com/questions/55045066/how-do-i-convert-a-ttf-or-other-font-to-a-xnb-xna-game-studio-font
			// if you're having issues building on w10, install Visual C++ Redistributable for Visual Studio 2012 Update 4
			// 
			// to build your own font: /MonoGame Font Builder/game.mgcb
			// build -> obj -> copy it over to HaRepacker-resurrected [Content]
			font = Content.Load<SpriteFont>("XnaDefaultFont");  

			base.Initialize();
		}

		/// <summary>
		/// Load spine related assets and contents
		/// </summary>
		protected override void LoadContent()
		{
			// Font
			spriteBatch = new SpriteBatch(GraphicsDevice);


			// Spine
			spineAnimationItem.LoadResources(graphicsDeviceMgr.GraphicsDevice); //  load spine resources (this must happen after window is loaded)
			this.skeleton = new Skeleton(spineAnimationItem.SkeletonData);

			skeletonRenderer = new SkeletonMeshRenderer(GraphicsDevice);
			skeletonRenderer.PremultipliedAlpha = spineAnimationItem.PremultipliedAlpha;

			// Skin
			foreach (Skin skin in spineAnimationItem.SkeletonData.Skins)
			{
				this.skeleton.SetSkin(skin.Name); // just set the first skin
				break;
			}

			// Define mixing between animations.
			AnimationStateData stateData = new AnimationStateData(skeleton.Data);
			state = new AnimationState(stateData);

			int i = 0;
			foreach (Animation animation in spineAnimationItem.SkeletonData.Animations)
			{
				state.SetAnimation(i++, animation.Name, true);
			}
			/*if (name == "spineboy")
			{
				stateData.SetMix("run", "jump", 0.2f);
				stateData.SetMix("jump", "run", 0.4f);

				// Event handling for all animations.
				state.Start += Start;
				state.End += End;
				state.Complete += Complete;
				state.Event += Event;

				state.SetAnimation(0, "test", false);
				TrackEntry entry = state.AddAnimation(0, "jump", false, 0);
				entry.End += End; // Event handling for queued animations.
				state.AddAnimation(0, "run", true, 0);
			}
			else if (name == "raptor")
			{
				state.SetAnimation(0, "walk", true);
				state.SetAnimation(1, "empty", false);
				state.AddAnimation(1, "gungrab", false, 2);
			}
			else
			{
				state.SetAnimation(0, "walk", true);
			}*/

			skeleton.X = 800;
			skeleton.Y = 600;
			skeleton.UpdateWorldTransform();
		}

		protected override void UnloadContent()
		{
			// TODO: Unload any non ContentManager content here
			skeletonRenderer.End();

			graphicsDeviceMgr.EndDraw();
			graphicsDeviceMgr.Dispose();
			graphicsDeviceMgr = null;
		}

		protected override void Update(GameTime gameTime)
		{
			// Allows the game to exit
#if !WINDOWS_STOREAPP
			if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed
				|| Keyboard.GetState().IsKeyDown(Keys.Escape))
				this.Exit();
#endif
			// Navigate around the rendered object
			bool bIsShiftPressed = Keyboard.GetState().IsKeyDown(Keys.LeftShift) || Keyboard.GetState().IsKeyDown(Keys.RightShift);

			bool bIsUpKeyPressed = Keyboard.GetState().IsKeyDown(Keys.Up);
			bool bIsDownKeyPressed = Keyboard.GetState().IsKeyDown(Keys.Down);
			bool bIsLeftKeyPressed = Keyboard.GetState().IsKeyDown(Keys.Left);
			bool bIsRightKeyPressed = Keyboard.GetState().IsKeyDown(Keys.Right);

			int MOVE_XY_POSITION = 4;
			if (bIsShiftPressed) // Move 2x as fast with shift pressed
				MOVE_XY_POSITION *= 2;

			if (bIsUpKeyPressed)
				skeleton.Y += MOVE_XY_POSITION;
			if (bIsDownKeyPressed)
				skeleton.Y -= MOVE_XY_POSITION;
			if (bIsLeftKeyPressed)
				skeleton.X += MOVE_XY_POSITION;
			if (bIsRightKeyPressed)
				skeleton.X -= MOVE_XY_POSITION;

			base.Update(gameTime);
		}

		protected override void Draw(GameTime gameTime)
		{
			GraphicsDevice.Clear(Color.Black);

			state.Update(gameTime.ElapsedGameTime.Milliseconds / 1000f);
			state.Apply(skeleton);

			skeleton.UpdateWorldTransform();

			skeletonRenderer.Begin();
			skeletonRenderer.Draw(skeleton);
			skeletonRenderer.End();

			bounds.Update(skeleton, true);
			/*MouseState mouse = Mouse.GetState();
			if (headSlot != null)
			{
				headSlot.G = 1;
				headSlot.B = 1;
				if (bounds.AabbContainsPoint(mouse.X, mouse.Y))
				{
					BoundingBoxAttachment hit = bounds.ContainsPoint(mouse.X, mouse.Y);
					if (hit != null)
					{
						headSlot.G = 0;
						headSlot.B = 0;
					}
				}
			}*/
			
			spriteBatch.Begin(); 
			if (gameTime.TotalGameTime.TotalSeconds < 3)
				spriteBatch.DrawString(font, "Press [Left] [Right] [Up] [Down] [Shift] for navigation.", new Vector2(20, 10), Color.White);
			else
				spriteBatch.DrawString(font, "", new Vector2(0, 0), Color.White);

			spriteBatch.End();

			base.Draw(gameTime);
		}

		public void Start(AnimationState state, int trackIndex)
		{
#if !WINDOWS_STOREAPP
			Console.WriteLine(trackIndex + " " + state.GetCurrent(trackIndex) + ": start");
#endif
		}

		public void End(AnimationState state, int trackIndex)
		{
#if !WINDOWS_STOREAPP
			Console.WriteLine(trackIndex + " " + state.GetCurrent(trackIndex) + ": end");
#endif
		}

		public void Complete(AnimationState state, int trackIndex, int loopCount)
		{
#if !WINDOWS_STOREAPP
			Console.WriteLine(trackIndex + " " + state.GetCurrent(trackIndex) + ": complete " + loopCount);
#endif
		}

		public void Event(AnimationState state, int trackIndex, Event e)
		{
#if !WINDOWS_STOREAPP
			Console.WriteLine(trackIndex + " " + state.GetCurrent(trackIndex) + ": event " + e);
#endif
		}
	}
}
