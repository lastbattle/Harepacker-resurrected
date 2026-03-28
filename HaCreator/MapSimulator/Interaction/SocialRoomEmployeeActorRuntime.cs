using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Loaders;
using HaCreator.MapSimulator.Pools;
using HaSharedLibrary.Render;
using MapleLib.WzLib;
using MapleLib.WzLib.WzStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class SocialRoomEmployeeActorRuntime
    {
        private const string MerchantNpcId = "9071001";
        private const string StoreBankerNpcId = "9030000";
        private const int DefaultAnchorOffsetX = 72;
        private const int SignVerticalOffset = 34;
        private const float HeadlineScale = 0.48f;
        private const float DetailScale = 0.42f;

        private static readonly Color SignHeadlineColor = new(255, 242, 176);
        private static readonly Color SignDetailColor = new(244, 244, 244);
        private static readonly Color SignPanelColor = new(28, 22, 18, 210);
        private static readonly Color SignBorderColor = new(180, 138, 69, 255);

        private readonly Dictionary<SocialRoomFieldActorTemplate, NpcItem> _actorCache = new();
        private readonly ConcurrentBag<WzObject> _usedProps = new();

        private NpcItem _activeActor;
        private SocialRoomFieldActorSnapshot _activeSnapshot;
        private string _lastStateKey = string.Empty;

        public bool IsVisible => _activeActor != null && _activeSnapshot != null;

        public void Clear()
        {
            _activeActor = null;
            _activeSnapshot = null;
            _lastStateKey = string.Empty;
        }

        public void Update(
            SocialRoomFieldActorSnapshot snapshot,
            Board mapBoard,
            PlayerCharacter player,
            TexturePool texturePool,
            GraphicsDevice device,
            float userScreenScaleFactor,
            GameTime gameTime)
        {
            if (snapshot == null || mapBoard == null || player == null || texturePool == null || device == null)
            {
                Clear();
                return;
            }

            NpcItem actor = EnsureActor(snapshot.Template, mapBoard, texturePool, device, userScreenScaleFactor);
            if (actor == null)
            {
                Clear();
                return;
            }

            _activeActor = actor;
            _activeSnapshot = snapshot;

            SyncActorPosition(player, actor);
            TriggerStateAction(actor, snapshot.StateKey);

            actor.MovementEnabled = false;
            actor.Update((int)Math.Max(0d, gameTime.ElapsedGameTime.TotalMilliseconds));
        }

        public void Draw(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            int mapShiftX,
            int mapShiftY,
            int mapCenterX,
            int mapCenterY,
            RenderParameters renderParameters,
            int tickCount,
            SpriteFont font,
            Texture2D panelTexture)
        {
            if (_activeActor == null || _activeSnapshot == null)
            {
                return;
            }

            _activeActor.Draw(
                spriteBatch,
                skeletonMeshRenderer,
                gameTime,
                mapShiftX,
                mapShiftY,
                mapCenterX,
                mapCenterY,
                null,
                renderParameters,
                tickCount);

            if (font == null)
            {
                return;
            }

            DrawSign(spriteBatch, font, panelTexture, mapShiftX, mapShiftY, mapCenterX, mapCenterY);
        }

        private NpcItem EnsureActor(
            SocialRoomFieldActorTemplate template,
            Board mapBoard,
            TexturePool texturePool,
            GraphicsDevice device,
            float userScreenScaleFactor)
        {
            if (_actorCache.TryGetValue(template, out NpcItem cachedActor))
            {
                return cachedActor;
            }

            string npcId = template switch
            {
                SocialRoomFieldActorTemplate.StoreBanker => StoreBankerNpcId,
                _ => MerchantNpcId
            };

            NpcInfo npcInfo = NpcInfo.Get(npcId);
            if (npcInfo == null)
            {
                return null;
            }

            NpcInstance npcInstance = (NpcInstance)npcInfo.CreateInstance(
                mapBoard,
                0,
                0,
                UserSettings.Npcrx0Offset,
                UserSettings.Npcrx1Offset,
                8,
                null,
                0,
                false,
                false,
                null,
                null);
            NpcItem actor = LifeLoader.CreateNpcFromProperty(
                texturePool,
                npcInstance,
                userScreenScaleFactor,
                device,
                _usedProps,
                includeTooltips: false);
            if (actor == null)
            {
                return null;
            }

            actor.MovementEnabled = false;
            _actorCache[template] = actor;
            return actor;
        }

        private void SyncActorPosition(PlayerCharacter player, NpcItem actor)
        {
            int horizontalOffset = player.FacingRight ? DefaultAnchorOffsetX : -DefaultAnchorOffsetX;
            actor.SetRenderPositionOverride(
                (int)Math.Round(player.Position.X) + horizontalOffset,
                (int)Math.Round(player.Position.Y));
            actor.NpcInstance.Flip = horizontalOffset < 0;
        }

        private void TriggerStateAction(NpcItem actor, string stateKey)
        {
            if (string.Equals(_lastStateKey, stateKey, StringComparison.Ordinal))
            {
                return;
            }

            _lastStateKey = stateKey ?? string.Empty;
            string speakAction = ResolveSpeakAction(actor);
            if (speakAction != null)
            {
                actor.SetTemporaryAction(speakAction, 900);
            }
        }

        private static string ResolveSpeakAction(NpcItem actor)
        {
            if (actor == null)
            {
                return null;
            }

            return actor.HasAction("say")
                ? "say"
                : actor.HasAction("say0")
                    ? "say0"
                    : actor.HasAction("speak")
                        ? "speak"
                        : null;
        }

        private void DrawSign(
            SpriteBatch spriteBatch,
            SpriteFont font,
            Texture2D panelTexture,
            int mapShiftX,
            int mapShiftY,
            int mapCenterX,
            int mapCenterY)
        {
            string headline = _activeSnapshot.Headline;
            string detail = _activeSnapshot.Detail;
            if (string.IsNullOrWhiteSpace(headline) && string.IsNullOrWhiteSpace(detail))
            {
                return;
            }

            float headlineLineHeight = font.LineSpacing * HeadlineScale;
            float detailLineHeight = font.LineSpacing * DetailScale;
            Vector2 headlineSize = string.IsNullOrWhiteSpace(headline) ? Vector2.Zero : font.MeasureString(headline) * HeadlineScale;
            Vector2 detailSize = string.IsNullOrWhiteSpace(detail) ? Vector2.Zero : font.MeasureString(detail) * DetailScale;
            float contentWidth = Math.Max(headlineSize.X, detailSize.X);
            int boxWidth = Math.Max(84, (int)Math.Ceiling(contentWidth) + 18);
            int boxHeight = (int)Math.Ceiling(headlineLineHeight + detailLineHeight) + 14;

            int actorScreenX = _activeActor.CurrentX - mapShiftX + mapCenterX;
            int actorScreenY = _activeActor.CurrentY - mapShiftY + mapCenterY;
            int boxX = actorScreenX - (boxWidth / 2);
            int boxY = actorScreenY - _activeActor.NpcInstance.Height - boxHeight - SignVerticalOffset;
            Rectangle box = new(boxX, boxY, boxWidth, boxHeight);

            if (panelTexture != null)
            {
                spriteBatch.Draw(panelTexture, box, SignPanelColor);
                spriteBatch.Draw(panelTexture, new Rectangle(box.Left, box.Top, box.Width, 1), SignBorderColor);
                spriteBatch.Draw(panelTexture, new Rectangle(box.Left, box.Bottom - 1, box.Width, 1), SignBorderColor);
                spriteBatch.Draw(panelTexture, new Rectangle(box.Left, box.Top, 1, box.Height), SignBorderColor);
                spriteBatch.Draw(panelTexture, new Rectangle(box.Right - 1, box.Top, 1, box.Height), SignBorderColor);
            }

            float textY = boxY + 6f;
            if (!string.IsNullOrWhiteSpace(headline))
            {
                spriteBatch.DrawString(
                    font,
                    headline,
                    new Vector2(actorScreenX - (headlineSize.X / 2f), textY),
                    SignHeadlineColor,
                    0f,
                    Vector2.Zero,
                    HeadlineScale,
                    SpriteEffects.None,
                    0f);
                textY += headlineLineHeight;
            }

            if (!string.IsNullOrWhiteSpace(detail))
            {
                spriteBatch.DrawString(
                    font,
                    detail,
                    new Vector2(actorScreenX - (detailSize.X / 2f), textY),
                    SignDetailColor,
                    0f,
                    Vector2.Zero,
                    DetailScale,
                    SpriteEffects.None,
                    0f);
            }
        }
    }
}
