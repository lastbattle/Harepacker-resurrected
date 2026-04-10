using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class DragonBoxWindow : UIWindowBase
    {
        private const float TextScale = 0.45f;
        private readonly string _windowName;
        private readonly Texture2D _unsummonableBackground;
        private readonly Texture2D _summonableBackground;
        private readonly Point _unsummonableBackgroundOffset;
        private readonly Point _summonableBackgroundOffset;
        private readonly Dictionary<int, Texture2D> _orbIconCache = new();
        private UIObject _summonButton;
        private Func<DragonBoxWindowSnapshot> _snapshotProvider;
        private Func<int, Texture2D> _itemIconProvider;
        private Action _summonRequested;
        private static readonly Point[] ClientOrbIconPositions =
        {
            new(31, 75),
            new(64, 74),
            new(100, 75),
            new(28, 110),
            new(64, 111),
            new(102, 110),
            new(28, 143),
            new(65, 144),
            new(103, 144)
        };

        internal DragonBoxWindow(
            IDXObject frame,
            string windowName,
            Texture2D unsummonableBackground,
            Point unsummonableBackgroundOffset,
            Texture2D summonableBackground,
            Point summonableBackgroundOffset)
            : base(frame)
        {
            _windowName = string.IsNullOrWhiteSpace(windowName) ? MapSimulatorWindowNames.DragonBox : windowName;
            _unsummonableBackground = unsummonableBackground;
            _summonableBackground = summonableBackground ?? unsummonableBackground;
            _unsummonableBackgroundOffset = unsummonableBackgroundOffset;
            _summonableBackgroundOffset = summonableBackgroundOffset;
        }

        public override string WindowName => _windowName;

        internal void SetSnapshotProvider(Func<DragonBoxWindowSnapshot> snapshotProvider)
        {
            _snapshotProvider = snapshotProvider;
        }

        internal void SetItemIconProvider(Func<int, Texture2D> itemIconProvider)
        {
            _itemIconProvider = itemIconProvider;
            _orbIconCache.Clear();
        }

        internal void InitializeSummonButton(UIObject summonButton)
        {
            if (_summonButton != null)
            {
                _summonButton.ButtonClickReleased -= HandleSummonButtonReleased;
            }

            _summonButton = summonButton;
            if (_summonButton == null)
            {
                return;
            }

            AddButton(_summonButton);
            _summonButton.ButtonClickReleased += HandleSummonButtonReleased;
        }

        internal void SetSummonRequested(Action summonRequested)
        {
            _summonRequested = summonRequested;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            _summonButton?.SetEnabled(true);
        }

        protected override void DrawContents(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            DragonBoxWindowSnapshot snapshot = _snapshotProvider?.Invoke() ?? new DragonBoxWindowSnapshot();
            Texture2D background = snapshot.CanSummon ? _summonableBackground : _unsummonableBackground;
            Point backgroundOffset = snapshot.CanSummon ? _summonableBackgroundOffset : _unsummonableBackgroundOffset;
            if (background != null)
            {
                sprite.Draw(background, new Vector2(Position.X + backgroundOffset.X, Position.Y + backgroundOffset.Y), Color.White);
            }

            DrawCollectedOrbIcons(sprite, snapshot.OrbMask);

            if (!CanDrawWindowText)
            {
                return;
            }

            Vector2 origin = new(Position.X + 16, Position.Y + 34);
            DrawWindowText(sprite, snapshot.ProgressText, origin, Color.White, TextScale, 138f);
            DrawWindowText(
                sprite,
                $"Orbs: {snapshot.CollectedOrbCount}/9 (0x{snapshot.OrbMask:X3})",
                origin + new Vector2(0f, 22f),
                new Color(244, 220, 162),
                TextScale,
                138f);
            DrawWindowText(
                sprite,
                snapshot.StatusText,
                origin + new Vector2(0f, 44f),
                new Color(226, 226, 226),
                TextScale,
                138f);

            if (!string.IsNullOrWhiteSpace(snapshot.FooterText))
            {
                DrawWindowText(
                    sprite,
                    snapshot.FooterText,
                    new Vector2(Position.X + 16, Position.Y + 150),
                    new Color(255, 228, 151),
                    0.38f,
                    140f);
            }
        }

        private void HandleSummonButtonReleased(UIObject button)
        {
            _summonRequested?.Invoke();
        }

        private void DrawCollectedOrbIcons(SpriteBatch sprite, int orbMask)
        {
            for (int index = 0; index < ClientOrbIconPositions.Length; index++)
            {
                if ((orbMask & (1 << index)) == 0)
                {
                    continue;
                }

                Texture2D icon = ResolveOrbIcon(index);
                if (icon == null)
                {
                    continue;
                }

                Point position = ClientOrbIconPositions[index];
                sprite.Draw(icon, new Vector2(Position.X + position.X, Position.Y + position.Y), Color.White);
            }
        }

        private Texture2D ResolveOrbIcon(int index)
        {
            int itemId = DragonBoxWindowSnapshot.FirstDragonBallItemId + Math.Max(0, index);
            if (_orbIconCache.TryGetValue(itemId, out Texture2D icon))
            {
                return icon;
            }

            icon = _itemIconProvider?.Invoke(itemId);
            _orbIconCache[itemId] = icon;
            return icon;
        }
    }
}
