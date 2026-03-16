using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Managers;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;

namespace HaCreator.MapSimulator.UI
{
    public sealed class CharacterDetailWindow : UIWindowBase
    {
        private const int LabelX = 16;
        private const int ValueX = 84;
        private const int TitleY = 12;
        private const int NameY = 34;
        private const int DetailsStartY = 58;
        private const int DetailRowHeight = 17;

        private SpriteFont _font;
        private LoginCharacterRosterEntry _entry;
        private string _statusMessage = "Select a character to inspect details.";

        public CharacterDetailWindow(IDXObject frame)
            : base(frame)
        {
        }

        public override string WindowName => MapSimulatorWindowNames.CharacterDetail;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void SetEntry(LoginCharacterRosterEntry entry, string statusMessage)
        {
            _entry = entry;
            _statusMessage = statusMessage ?? string.Empty;
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
            if (_font == null)
            {
                return;
            }

            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                "Character Detail",
                new Vector2(Position.X + LabelX, Position.Y + TitleY),
                Color.White);

            if (_entry?.Build == null)
            {
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    _statusMessage,
                    new Vector2(Position.X + LabelX, Position.Y + NameY),
                    new Color(220, 220, 220));
                return;
            }

            CharacterBuild build = _entry.Build;
            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                build.Name,
                new Vector2(Position.X + LabelX, Position.Y + NameY),
                new Color(255, 234, 171));

            DrawRow(sprite, 0, "Job", build.JobName);
            DrawRow(sprite, 1, "Level", build.Level.ToString());
            DrawRow(sprite, 2, "Guild", build.GuildDisplayText);
            DrawRow(sprite, 3, "Alliance", build.AllianceDisplayText);
            DrawRow(sprite, 4, "Fame", build.Fame.ToString());
            DrawRow(sprite, 5, "EXP", build.ExpDisplayText);
            DrawRow(sprite, 6, "World", FormatRank(build.WorldRank));
            DrawRow(sprite, 7, "JobRank", FormatRank(build.JobRank));
            DrawRow(sprite, 8, "Map", _entry.FieldMapId.ToString());

            if (!string.IsNullOrWhiteSpace(_statusMessage))
            {
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    _statusMessage,
                    new Vector2(Position.X + LabelX, Position.Y + 216),
                    new Color(220, 220, 220));
            }
        }

        private void DrawRow(SpriteBatch sprite, int rowIndex, string label, string value)
        {
            int y = Position.Y + DetailsStartY + (rowIndex * DetailRowHeight);
            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                label,
                new Vector2(Position.X + LabelX, y),
                new Color(190, 200, 220));
            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                value,
                new Vector2(Position.X + ValueX, y),
                Color.White);
        }

        private static string FormatRank(int rank)
        {
            return rank > 0 ? rank.ToString() : "-";
        }
    }
}
