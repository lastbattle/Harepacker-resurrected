using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace HaCreator.MapSimulator.Fields
{
    /// <summary>
    /// Dedicated Cookie House HUD runtime.
    /// IDA shows the client initializes a field-owned point layer and redraws
    /// it when the score changes, so the simulator keeps that state here.
    /// </summary>
    public sealed class CookieHouseField
    {
        public const int LayerWidth = 187;
        public const int LayerHeight = 45;
        public const int LayerOffsetX = -93;
        public const int LayerTopY = 92;
        public const int ScoreDrawX = 92;
        public const int ScoreDrawY = 9;
        public const int GradeCount = 5;

        // The client uses four thresholds to select five bitmap-number grades.
        // These values keep the grade-owned simulator path alive until the
        // exact threshold table is lifted from the client data segment.
        public static readonly int[] GradeThresholds = { 1000, 3000, 6000, 10000 };

        private static readonly Color[] GradeAccentColors =
        {
            new(214, 163, 82),
            new(114, 187, 92),
            new(91, 169, 204),
            new(151, 122, 214),
            new(224, 105, 105)
        };

        private bool _isActive;
        private int _mapId;
        private int _point;
        private int _gradeIndex;

        public bool IsActive => _isActive;
        public int MapId => _mapId;
        public int Point => _point;
        public int GradeIndex => _gradeIndex;

        public void Enable(int mapId)
        {
            _isActive = true;
            _mapId = mapId;
            _point = 0;
            _gradeIndex = 0;
        }

        public void OnPointUpdate(int newPoint)
        {
            _point = Math.Max(0, newPoint);
            _gradeIndex = FindGrade(_point);
        }

        public string DescribeStatus()
        {
            return $"Cookie House map={_mapId}, point={_point}, grade={_gradeIndex + 1}/{GradeCount}";
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, int centerX)
        {
            if (!_isActive || spriteBatch == null || pixelTexture == null)
            {
                return;
            }

            int left = centerX + LayerOffsetX;
            Rectangle outer = new(left, LayerTopY, LayerWidth, LayerHeight);
            Rectangle inner = new(left + 2, LayerTopY + 2, LayerWidth - 4, LayerHeight - 4);
            Rectangle accent = new(left + 4, LayerTopY + 4, LayerWidth - 8, 6);
            Color accentColor = GradeAccentColors[Math.Clamp(_gradeIndex, 0, GradeAccentColors.Length - 1)];

            spriteBatch.Draw(pixelTexture, outer, new Color(37, 26, 18, 220));
            spriteBatch.Draw(pixelTexture, inner, new Color(248, 237, 210, 245));
            spriteBatch.Draw(pixelTexture, accent, accentColor);

            if (font != null)
            {
                string scoreText = _point.ToString();
                Vector2 textSize = font.MeasureString(scoreText);
                Vector2 textPosition = new(
                    left + ScoreDrawX - (textSize.X / 2f),
                    LayerTopY + ScoreDrawY);

                spriteBatch.DrawString(font, "COOKIE POINT", new Vector2(left + 10, LayerTopY + 20), new Color(94, 66, 45));
                spriteBatch.DrawString(font, scoreText, textPosition, new Color(56, 32, 20));
            }
        }

        public void Reset()
        {
            _isActive = false;
            _mapId = 0;
            _point = 0;
            _gradeIndex = 0;
        }

        private static int FindGrade(int point)
        {
            for (int i = 0; i < GradeThresholds.Length; i++)
            {
                if (point < GradeThresholds[i])
                {
                    return i;
                }
            }

            return GradeCount - 1;
        }
    }
}
