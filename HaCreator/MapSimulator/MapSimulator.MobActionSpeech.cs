using System;
using HaCreator.MapSimulator.Entities;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private void DrawMobActionSpeechFeedback(in Managers.RenderContext renderContext)
        {
            if (_fontChat == null ||
                _debugBoundaryTexture == null ||
                _visibleMobs == null ||
                _visibleMobsCount <= 0)
            {
                return;
            }

            for (int i = 0; i < _visibleMobsCount; i++)
            {
                MobItem mob = _visibleMobs[i];
                if (mob?.IsActionSpeechActive(renderContext.TickCount) == true)
                {
                    DrawMobActionSpeechBalloon(mob, renderContext);
                }
            }
        }

        private void DrawMobActionSpeechBalloon(MobItem mob, in Managers.RenderContext renderContext)
        {
            if (mob == null ||
                !mob.HasActiveActionSpeech ||
                _fontChat == null ||
                _debugBoundaryTexture == null)
            {
                return;
            }

            Vector2 textSize = MeasureChatTextWithFallback(mob.ActiveActionSpeechText);
            Rectangle bounds = ResolveMobActionSpeechBounds(
                textSize,
                mob.CurrentX,
                mob.CurrentY - mob.GetVisualHeight(60),
                mob.ActiveActionSpeechChatBalloon,
                renderContext.MapShiftX,
                renderContext.MapShiftY,
                renderContext.MapCenterX,
                renderContext.MapCenterY,
                renderContext.RenderParams.RenderWidth,
                renderContext.RenderParams.RenderHeight);

            if (bounds.Right < 0 ||
                bounds.Bottom < 0 ||
                bounds.Left > renderContext.RenderParams.RenderWidth ||
                bounds.Top > renderContext.RenderParams.RenderHeight)
            {
                return;
            }

            float remainingAlpha = MathHelper.Clamp((mob.ActiveActionSpeechExpiresAt - renderContext.TickCount) / 400f, 0f, 1f);
            ResolveMobActionSpeechColors(
                mob.ActiveActionSpeechChatBalloon,
                remainingAlpha,
                out Color backgroundColor,
                out Color borderColor,
                out Color textColor);

            _spriteBatch.Draw(_debugBoundaryTexture, bounds, backgroundColor);
            _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(bounds.Left, bounds.Top, bounds.Width, 2), borderColor);
            _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(bounds.Left, bounds.Bottom - 2, bounds.Width, 2), borderColor);
            _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(bounds.Left, bounds.Top, 2, bounds.Height), borderColor);
            _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(bounds.Right - 2, bounds.Top, 2, bounds.Height), borderColor);

            if (!IsMobActionSpeechScreenChat(mob.ActiveActionSpeechChatBalloon))
            {
                int arrowX = bounds.Left + (bounds.Width / 2) - 5;
                int arrowY = bounds.Bottom - 1;
                _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(arrowX, arrowY, 10, 4), borderColor);
                _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(arrowX + 2, arrowY + 4, 6, 3), borderColor);
                _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(arrowX + 4, arrowY + 7, 2, 3), borderColor);
            }

            DrawChatTextWithFallback(mob.ActiveActionSpeechText, new Vector2(bounds.Left + 9, bounds.Top + 6), textColor);
        }

        internal static Rectangle ResolveMobActionSpeechBounds(
            Vector2 textSize,
            int mobWorldX,
            int mobWorldTop,
            int chatBalloon,
            int mapShiftX,
            int mapShiftY,
            int mapCenterX,
            int mapCenterY,
            int renderWidth,
            int renderHeight)
        {
            int boxWidth = Math.Max(18, (int)Math.Ceiling(textSize.X) + 18);
            int boxHeight = Math.Max(20, (int)Math.Ceiling(textSize.Y) + 12);

            if (IsMobActionSpeechScreenChat(chatBalloon))
            {
                return new Rectangle(
                    Math.Max(0, (renderWidth - boxWidth) / 2),
                    Math.Max(0, Math.Min(renderHeight - boxHeight, 84)),
                    boxWidth,
                    boxHeight);
            }

            int boxX = mobWorldX - mapShiftX + mapCenterX - (boxWidth / 2);
            int boxY = mobWorldTop - mapShiftY + mapCenterY - boxHeight - 24;
            return new Rectangle(boxX, boxY, boxWidth, boxHeight);
        }

        internal static bool IsMobActionSpeechScreenChat(int chatBalloon)
        {
            // UI/ChatBalloon.img/mob/1 carries screenChat=1; other mob balloons stay anchored to the owner.
            return chatBalloon == 1;
        }

        private static void ResolveMobActionSpeechColors(
            int chatBalloon,
            float alpha,
            out Color backgroundColor,
            out Color borderColor,
            out Color textColor)
        {
            float clampedAlpha = MathHelper.Clamp(alpha, 0f, 1f);
            if (IsMobActionSpeechScreenChat(chatBalloon))
            {
                backgroundColor = new Color(31, 31, 31) * (0.88f * clampedAlpha);
                borderColor = new Color(246, 246, 246) * clampedAlpha;
                textColor = Color.White * clampedAlpha;
                return;
            }

            backgroundColor = new Color(255, 255, 245) * (0.92f * clampedAlpha);
            borderColor = new Color(72, 72, 72) * clampedAlpha;
            textColor = Color.Black * clampedAlpha;
        }
    }
}
