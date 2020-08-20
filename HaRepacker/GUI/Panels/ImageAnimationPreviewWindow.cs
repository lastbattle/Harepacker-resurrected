using HaSharedLibrary.Render.DX;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HaSharedLibrary.Util;
using Spine;
using System.Runtime.CompilerServices;

namespace HaRepacker.GUI.Panels
{
    public class ImageAnimationPreviewWindow : Microsoft.Xna.Framework.Game
	{
		// Engine
		private GraphicsDeviceManager graphicsDeviceMgr;

		// Constants
		private const int RENDER_WIDTH = 1366;
		private const int RENDER_HEIGHT = 768;
		private const float RENDER_SCALING = 1.0f;

		private float renderAnimationScaling = 1.0f;

		// Rendering objects
		private readonly List<WzNode> selectedAnimationNodes;
		private BaseDXDrawableItem dxDrawableItem = null;

		// Debug
		private SpriteFont font_DebugValues;
		private Texture2D texture_debugBoundaryRect;

		// Text
		private SpriteBatch spriteBatch;
		private SpriteFont font;

		// 
		public int mapShiftX = -600;
		public int mapShiftY = -400;

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="selectedAnimationNodes"></param>
		public ImageAnimationPreviewWindow(List<WzNode> selectedAnimationNodes)
        {
			this.selectedAnimationNodes = selectedAnimationNodes;

			IsMouseVisible = true;

			//Window.AllowUserResizing = true;
			//Window.IsBorderless = true;
			//Window.Position = new Point(0, 0);
			Window.Title = "Animation preview";
			IsFixedTimeStep = false; // dont cap fps
			graphicsDeviceMgr = new GraphicsDeviceManager(this)
			{
				SynchronizeWithVerticalRetrace = true,
				HardwareModeSwitch = true,
				GraphicsProfile = GraphicsProfile.HiDef,
				IsFullScreen = false,
				PreferMultiSampling = true,
				SupportedOrientations = DisplayOrientation.Default,
				PreferredBackBufferWidth = RENDER_WIDTH,
				PreferredBackBufferHeight = RENDER_HEIGHT,
				PreferredBackBufferFormat = SurfaceFormat.Color,
				PreferredDepthStencilFormat = DepthFormat.Depth24Stencil8,
			};
			graphicsDeviceMgr.ApplyChanges();
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
			font_DebugValues = Content.Load<SpriteFont>("XnaFont_Debug");

			base.Initialize();
		}

		/// <summary>
		/// Load spine related assets and contents
		/// </summary>
		protected override void LoadContent()
		{
			// Font
			spriteBatch = new SpriteBatch(GraphicsDevice);


			// Animation frames
			List<IDXObject> animationFrames = new List<IDXObject>();
			// WzNodes to DXObject
			foreach (WzNode selNode in selectedAnimationNodes)
			{
				WzObject obj = (WzObject)selNode.Tag;
				bool isUOLProperty = obj is WzUOLProperty;

				if (obj is WzCanvasProperty || isUOLProperty)
				{
					WzCanvasProperty canvasProperty;

					// Get image property
					System.Drawing.Bitmap image;
					if (!isUOLProperty)
					{
						canvasProperty = ((WzCanvasProperty)obj);
						image = canvasProperty.GetLinkedWzCanvasBitmap();
					}
					else
					{
						WzObject linkVal = ((WzUOLProperty)obj).LinkValue;
						if (linkVal is WzCanvasProperty property)
						{
							canvasProperty = property;
							image = canvasProperty.GetLinkedWzCanvasBitmap();
                        }
                        else
                        {
							break;
                        }
					}

					// Get delay property
					int? delay = canvasProperty[WzCanvasProperty.AnimationDelayPropertyName]?.GetInt();
					if (delay == null)
						delay = 0;

					// Add to the list of images to render
					System.Drawing.PointF origin = canvasProperty.GetCanvasOriginPosition();
					DXObject dxObject = new DXObject((int)-origin.X, (int)-origin.Y, image.ToTexture2D(graphicsDeviceMgr.GraphicsDevice), (int)delay)
					{
						Tag = obj.FullPath
					};

					animationFrames.Add(dxObject);
				}
			}
			
			dxDrawableItem = new BaseDXDrawableItem(animationFrames, false);


			// Debug items
			System.Drawing.Bitmap bitmap_debug = new System.Drawing.Bitmap(1, 1);
			bitmap_debug.SetPixel(0, 0, System.Drawing.Color.White);
			texture_debugBoundaryRect = bitmap_debug.ToTexture2D(graphicsDeviceMgr.GraphicsDevice);
		}

		protected override void UnloadContent()
		{
			// TODO: Unload any non ContentManager content here
			graphicsDeviceMgr.EndDraw();
			graphicsDeviceMgr.Dispose();
			graphicsDeviceMgr = null;

			dxDrawableItem = null;
		}

		private KeyboardState oldKeyboardState = Keyboard.GetState();
		protected override void Update(GameTime gameTime)
		{
			float frameRate = 1 / (float)gameTime.ElapsedGameTime.TotalSeconds;
			int TickCount = Environment.TickCount;
			float delta = gameTime.ElapsedGameTime.Milliseconds / 1000f;
			KeyboardState newKeyboardState = Keyboard.GetState();  // get the newest state
			
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

			// Zoom
			bool bIsPlusKeyPressed = Keyboard.GetState().IsKeyDown(Keys.OemPlus);
			bool bIsMinusKeyPressed = Keyboard.GetState().IsKeyDown(Keys.OemMinus);
			float zoomOffset = (1.5f / frameRate); // move a fixed amount a second, not dependent on GPU speed

			if (bIsPlusKeyPressed)
				renderAnimationScaling += zoomOffset;
			if (bIsMinusKeyPressed)
				renderAnimationScaling -= zoomOffset;

			// Navigate around the rendered object
			bool bIsUpKeyPressed = Keyboard.GetState().IsKeyDown(Keys.Up);
			bool bIsDownKeyPressed = Keyboard.GetState().IsKeyDown(Keys.Down);
			bool bIsLeftKeyPressed = Keyboard.GetState().IsKeyDown(Keys.Left);
			bool bIsRightKeyPressed = Keyboard.GetState().IsKeyDown(Keys.Right);

			int moveOffset = (int)(500f / frameRate); // move a fixed amount a second, not dependent on GPU speed
			if (bIsLeftKeyPressed || bIsRightKeyPressed)
			{
				if (bIsLeftKeyPressed)
					mapShiftX += (int)(moveOffset / renderAnimationScaling);

				else if (bIsRightKeyPressed)
					mapShiftX -= (int)(moveOffset / renderAnimationScaling);
			}
			if (bIsUpKeyPressed || bIsDownKeyPressed)
			{
				if (bIsUpKeyPressed)
					mapShiftY += (int)(moveOffset / renderAnimationScaling);

				else if (bIsDownKeyPressed)
					mapShiftY -= (int) (moveOffset / renderAnimationScaling);
			}

			oldKeyboardState = Keyboard.GetState();  // set the new state as the old state for next time
			base.Update(gameTime);
		}

		protected override void Draw(GameTime gameTime)
		{
			float frameRate = 1 / (float)gameTime.ElapsedGameTime.TotalSeconds;
			int TickCount = Environment.TickCount;
			float delta = gameTime.ElapsedGameTime.Milliseconds / 1000f;

			MouseState mouseState = Mouse.GetState();
			int mouseXRelativeToMap = mouseState.X - mapShiftX;
			int mouseYRelativeToMap = mouseState.Y - mapShiftY;

			// Clear prior drawings
			GraphicsDevice.Clear(Color.Black);


			/////////////////////// DRAW ANIMATION ///////////////////////
			spriteBatch.Begin(
			   SpriteSortMode.Deferred,
			   BlendState.NonPremultiplied, null, null, null, null, Matrix.CreateScale(renderAnimationScaling));

			// Animation
			dxDrawableItem.Draw(spriteBatch, null, gameTime,
						mapShiftX, mapShiftY, 0, 0,
						RENDER_WIDTH, RENDER_HEIGHT, renderAnimationScaling, RenderResolution.Res_All,
						TickCount);
			if (dxDrawableItem.LastFrameDrawn != null)
			{
				IDXObject lastFrameDrawn = dxDrawableItem.LastFrameDrawn;

				// Boundary box
				Rectangle rectBox = new Rectangle(
					lastFrameDrawn.X - mapShiftX, 
					lastFrameDrawn.Y - mapShiftY, 
					lastFrameDrawn.Width, 
					lastFrameDrawn.Height);
				DrawBorder(spriteBatch, rectBox, 1, Color.White);
			}
			
			spriteBatch.End();
			/////////////////////// ///////////////////////

			/////////////////////// DRAW DEBUG TEXT ///////////////////////
			spriteBatch.Begin(
				SpriteSortMode.Deferred,
				BlendState.NonPremultiplied, null, null, null, null, Matrix.CreateScale(RENDER_SCALING));

			// Debug at the top right corner
			StringBuilder sb = new StringBuilder();
			sb.Append("FPS: ").Append(frameRate).Append(Environment.NewLine);
			sb.Append("Mouse : X ").Append(mouseXRelativeToMap).Append(", Y ").Append(mouseYRelativeToMap).Append(Environment.NewLine);
			sb.Append("RMouse: X ").Append(mouseState.X).Append(", Y ").Append(mouseState.Y);
			spriteBatch.DrawString(font_DebugValues, sb.ToString(), new Vector2(RENDER_WIDTH - 170, 10), Color.White);

			// Current image render information
			if (dxDrawableItem.LastFrameDrawn != null)
			{
				IDXObject lastFrameDrawn = dxDrawableItem.LastFrameDrawn;
				string imageRenderInfoText = string.Format("[Path: {0}]{7}[Origin: x = {1}, y = {2}]{8}[Dimension: W = {3}, H = {4}]{9}[Delay: {5}]{10}[Scale: {6}x]",
					dxDrawableItem.LastFrameDrawn.Tag as string,
					lastFrameDrawn.X, lastFrameDrawn.Y, lastFrameDrawn.Width, lastFrameDrawn.Height, lastFrameDrawn.Delay, Math.Round(renderAnimationScaling, 2),
					Environment.NewLine, Environment.NewLine, Environment.NewLine, Environment.NewLine);

				spriteBatch.DrawString(font_DebugValues, imageRenderInfoText, new Vector2((RENDER_WIDTH /2) - 100, RENDER_HEIGHT - 100), Color.White);
			}

			// Keyboard navigation info
			if (gameTime.TotalGameTime.TotalSeconds < 3)
				spriteBatch.DrawString(font,
					string.Format("Press [Left] [Right] [Up] [Down] for navigation.{0}   [+ -] for zoom", Environment.NewLine),
					new Vector2(20, 10), Color.White);

			spriteBatch.End();
			/////////////////////// ///////////////////////

			base.Draw(gameTime);
		}


		/// <summary>
		/// Draws a border
		/// </summary>
		/// <param name="sprite"></param>
		/// <param name="rectangleToDraw"></param>
		/// <param name="thicknessOfBorder"></param>
		/// <param name="borderColor"></param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void DrawBorder(SpriteBatch sprite, Rectangle rectangleToDraw, int thicknessOfBorder, Color borderColor)
		{
			// Draw top line
			sprite.Draw(texture_debugBoundaryRect, new Rectangle(rectangleToDraw.X, rectangleToDraw.Y, rectangleToDraw.Width, thicknessOfBorder), borderColor);

			// Draw left line
			sprite.Draw(texture_debugBoundaryRect, new Rectangle(rectangleToDraw.X, rectangleToDraw.Y, thicknessOfBorder, rectangleToDraw.Height), borderColor);

			// Draw right line
			sprite.Draw(texture_debugBoundaryRect, new Rectangle((rectangleToDraw.X + rectangleToDraw.Width - thicknessOfBorder),
											rectangleToDraw.Y,
											thicknessOfBorder,
											rectangleToDraw.Height), borderColor);
			// Draw bottom line
			sprite.Draw(texture_debugBoundaryRect, new Rectangle(rectangleToDraw.X,
											rectangleToDraw.Y + rectangleToDraw.Height - thicknessOfBorder,
											rectangleToDraw.Width,
											thicknessOfBorder), borderColor);
		}
	}
}
