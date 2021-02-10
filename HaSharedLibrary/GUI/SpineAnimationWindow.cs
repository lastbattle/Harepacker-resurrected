/*
 * Copyright (c) 2018~2020, LastBattle https://github.com/lastbattle
 * Copyright (c) 2010~2013, haha01haha http://forum.ragezone.com/f701/release-universal-harepacker-version-892005/

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using MapleLib.WzLib.Spine;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;

namespace HaSharedLibrary.GUI
{
	public class SpineAnimationWindow : Microsoft.Xna.Framework.Game
    {
		private GraphicsDeviceManager graphicsDeviceMgr;

		private SkeletonMeshRenderer skeletonRenderer;

		private readonly WzSpineObject wzSpineObject;

		// Text
		private SpriteBatch spriteBatch;
		private SpriteFont font;


		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="spineAnimationItem"></param>
		/// <param name="skeletonData"></param>
		public SpineAnimationWindow(WzSpineAnimationItem spineAnimationItem)
		{
			IsMouseVisible = true;

			//Window.IsBorderless = true;
			//Window.Position = new Point(0, 0);
			Window.Title = "Spine";
			IsFixedTimeStep = false; // dont cap fps
			Content.RootDirectory = "Content";

			graphicsDeviceMgr = new GraphicsDeviceManager(this)
			{
				SynchronizeWithVerticalRetrace = true, // max fps with the monitor
				HardwareModeSwitch = true,
				GraphicsProfile = GraphicsProfile.HiDef,
				IsFullScreen = false,
				PreferMultiSampling = true,
				SupportedOrientations = DisplayOrientation.Default,
				PreferredBackBufferWidth = 1366,
				PreferredBackBufferHeight = 768,
				PreferredBackBufferFormat = SurfaceFormat.Color,
				PreferredDepthStencilFormat = DepthFormat.Depth24Stencil8,
			};
			graphicsDeviceMgr.ApplyChanges();

			this.wzSpineObject = new WzSpineObject(spineAnimationItem);
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
			wzSpineObject.spineAnimationItem.LoadResources(graphicsDeviceMgr.GraphicsDevice); //  load spine resources (this must happen after window is loaded)
			wzSpineObject.skeleton = new Skeleton(wzSpineObject.spineAnimationItem.SkeletonData);

			skeletonRenderer = new SkeletonMeshRenderer(GraphicsDevice);
			skeletonRenderer.PremultipliedAlpha = wzSpineObject.spineAnimationItem.PremultipliedAlpha;

			// Skin
			foreach (Skin skin in wzSpineObject.spineAnimationItem.SkeletonData.Skins)
			{
				wzSpineObject.skeleton.SetSkin(skin.Name); // just set the first skin
				break;
			}

			// Define mixing between animations.
			wzSpineObject.stateData = new AnimationStateData(wzSpineObject.skeleton.Data);
			wzSpineObject.state = new AnimationState(wzSpineObject.stateData);

			int i = 0;
			foreach (Animation animation in wzSpineObject.spineAnimationItem.SkeletonData.Animations)
			{
				wzSpineObject.state.SetAnimation(i++, animation.Name, true);
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

			wzSpineObject.skeleton.X = 800;
			wzSpineObject.skeleton.Y = 600;
			wzSpineObject.skeleton.UpdateWorldTransform();
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
			// Handle full screen
			bool bIsAltEnterPressed = Keyboard.GetState().IsKeyDown(Keys.LeftAlt) && Keyboard.GetState().IsKeyDown(Keys.Enter);
			if (bIsAltEnterPressed)
			{
				graphicsDeviceMgr.IsFullScreen = !graphicsDeviceMgr.IsFullScreen;
				graphicsDeviceMgr.ApplyChanges();
			}

			// Navigate around the rendered object
			bool bIsShiftPressed = Keyboard.GetState().IsKeyDown(Keys.LeftShift) || Keyboard.GetState().IsKeyDown(Keys.RightShift);

			bool bIsUpKeyPressed = Keyboard.GetState().IsKeyDown(Keys.Up);
			bool bIsDownKeyPressed = Keyboard.GetState().IsKeyDown(Keys.Down);
			bool bIsLeftKeyPressed = Keyboard.GetState().IsKeyDown(Keys.Left);
			bool bIsRightKeyPressed = Keyboard.GetState().IsKeyDown(Keys.Right);

			int MOVE_XY_POSITION = 2;
			if (bIsShiftPressed) // Move 2x as fast with shift pressed
				MOVE_XY_POSITION *= 2;

			if (bIsUpKeyPressed)
				wzSpineObject.skeleton.Y += MOVE_XY_POSITION;
			if (bIsDownKeyPressed)
				wzSpineObject.skeleton.Y -= MOVE_XY_POSITION;
			if (bIsLeftKeyPressed)
				wzSpineObject.skeleton.X += MOVE_XY_POSITION;
			if (bIsRightKeyPressed)
				wzSpineObject.skeleton.X -= MOVE_XY_POSITION;

			base.Update(gameTime);
		}

		protected override void Draw(GameTime gameTime)
		{
			GraphicsDevice.Clear(Color.Black);

			wzSpineObject.state.Update(gameTime.ElapsedGameTime.Milliseconds / 1000f);
			wzSpineObject.state.Apply(wzSpineObject.skeleton);

			wzSpineObject.skeleton.UpdateWorldTransform();

			skeletonRenderer.Begin();
			skeletonRenderer.Draw(wzSpineObject.skeleton);
			skeletonRenderer.End();

			wzSpineObject.bounds.Update(wzSpineObject.skeleton, true);
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
