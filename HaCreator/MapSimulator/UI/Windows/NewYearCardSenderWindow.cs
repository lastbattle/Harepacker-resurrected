using HaCreator.MapSimulator.Interaction;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class NewYearCardSenderWindow : UIWindowBase
    {
        private readonly IDXObject _searchResultBackground;
        private Func<NewYearCardSenderSnapshot> _snapshotProvider;
        private Func<string> _searchHandler;
        private Func<string> _sendHandler;
        private Action _cancelHandler;

        public NewYearCardSenderWindow(IDXObject frame, IDXObject searchResultBackground, Point position)
            : base(frame, position)
        {
            _searchResultBackground = searchResultBackground;
        }

        public override string WindowName => MapSimulatorWindowNames.NewYearCardSender;

        internal void SetSnapshotProvider(Func<NewYearCardSenderSnapshot> provider)
        {
            _snapshotProvider = provider;
        }

        internal void SetActions(Func<string> searchHandler, Func<string> sendHandler, Action cancelHandler)
        {
            _searchHandler = searchHandler;
            _sendHandler = sendHandler;
            _cancelHandler = cancelHandler;
        }

        internal void InitializeControls(UIObject searchButton, UIObject okButton, UIObject cancelButton)
        {
            ConfigureButton(searchButton, () => _searchHandler?.Invoke());
            ConfigureButton(okButton, () => _sendHandler?.Invoke());
            ConfigureButton(cancelButton, HandleCancel);
        }

        protected override IEnumerable<Rectangle> GetAdditionalInteractiveBounds()
        {
            yield return new Rectangle(Position.X + 353, Position.Y, 165, 188);
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
            _searchResultBackground?.DrawBackground(
                sprite,
                skeletonMeshRenderer,
                gameTime,
                Position.X + 353,
                Position.Y,
                Color.White,
                false,
                drawReflectionInfo);

            if (!CanDrawWindowText)
            {
                return;
            }

            NewYearCardSenderSnapshot snapshot = _snapshotProvider?.Invoke()
                ?? new NewYearCardSenderSnapshot(NewYearCardRuntime.DefaultInventoryPosition, NewYearCardRuntime.DefaultItemId, string.Empty, string.Empty, Array.Empty<string>(), string.Empty);

            Color label = new(74, 65, 55);
            Color text = Color.Black;
            DrawWindowText(sprite, "To:", new Vector2(Position.X + 14, Position.Y + 66), label, 0.43f);
            DrawWindowText(sprite, Truncate(snapshot.TargetName, 26), new Vector2(Position.X + 48, Position.Y + 66), text, 0.42f, 239);
            DrawWindowText(sprite, "Memo:", new Vector2(Position.X + 13, Position.Y + 91), label, 0.4f);

            float memoY = Position.Y + 106;
            foreach (string line in Wrap(snapshot.Memo, 42, 3))
            {
                DrawWindowText(sprite, line, new Vector2(Position.X + 15, memoY), text, 0.39f, 326);
                memoY += 14f;
            }

            DrawWindowText(sprite, $"Item {snapshot.ItemId} / slot {snapshot.InventoryPosition}", new Vector2(Position.X + 14, Position.Y + 154), new Color(96, 88, 78), 0.34f);

            int row = 0;
            foreach (string name in snapshot.SearchResults ?? Array.Empty<string>())
            {
                if (row >= 5)
                {
                    break;
                }

                DrawWindowText(sprite, Truncate(name, 16), new Vector2(Position.X + 368, Position.Y + 24 + (row * 15)), text, 0.4f, 118);
                row++;
            }

            DrawWindowText(sprite, Truncate(snapshot.LastStatus, 58), new Vector2(Position.X + 362, Position.Y + 112), new Color(92, 84, 74), 0.32f, 134);
        }

        private void ConfigureButton(UIObject button, Action action)
        {
            if (button == null)
            {
                return;
            }

            AddButton(button);
            button.ButtonClickReleased += _ => action?.Invoke();
        }

        private void HandleCancel()
        {
            Hide();
            _cancelHandler?.Invoke();
        }

        private static IEnumerable<string> Wrap(string text, int maxCharacters, int maxLines)
        {
            string[] words = (text ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string line = string.Empty;
            int emitted = 0;
            foreach (string word in words)
            {
                string candidate = string.IsNullOrEmpty(line) ? word : $"{line} {word}";
                if (line.Length > 0 && candidate.Length > maxCharacters)
                {
                    yield return line;
                    emitted++;
                    if (emitted >= maxLines)
                    {
                        yield break;
                    }
                    line = word;
                }
                else
                {
                    line = candidate;
                }
            }

            if (!string.IsNullOrEmpty(line) && emitted < maxLines)
            {
                yield return line;
            }
        }

        private static string Truncate(string text, int maxCharacters)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxCharacters)
            {
                return text ?? string.Empty;
            }

            return text.Substring(0, Math.Max(0, maxCharacters - 3)) + "...";
        }
    }
}
