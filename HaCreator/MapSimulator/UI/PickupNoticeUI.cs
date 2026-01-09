using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    /// <summary>
    /// Types of pickup messages matching CWvsContext::OnDropPickUpMessage from MapleStory client
    /// </summary>
    public enum PickupMessageType
    {
        ItemPickup = 0,         // Regular item pickup
        MesoPickup = 1,         // Meso pickup
        QuestItemPickup = 2,    // Quest item pickup
        InventoryFull = -2,     // Can't pick up - inventory full
        CantPickup = -3,        // Can't pick up - other reason
        Unknown = -1            // Unknown/default
    }

    /// <summary>
    /// A single pickup notice message with animation state
    /// Based on CUIScreenMsg from MapleStory client analysis
    /// </summary>
    public class PickupNotice
    {
        public string Message { get; set; }
        public PickupMessageType Type { get; set; }
        public Color TextColor { get; set; }
        public Color OutlineColor { get; set; }
        public int SpawnTime { get; set; }
        public float Alpha { get; set; } = 1.0f;
        public float YOffset { get; set; }
        public float TargetYOffset { get; set; }
        public bool IsExpired { get; set; }

        // Item icon for item pickups (optional)
        public Texture2D ItemIcon { get; set; }
        public int Quantity { get; set; } = 1;
    }

    /// <summary>
    /// Pickup Notice UI System - Displays item/meso pickup messages at the bottom right of the screen.
    /// Based on CUIScreenMsg::ScrMsg_Add from MapleStory client.
    ///
    /// Features:
    /// - Displays up to MAX_MESSAGES stacked messages
    /// - Messages fade in from alpha 0->255 instantly
    /// - Messages fade out over FADE_DURATION after DISPLAY_DURATION
    /// - Messages slide up as new ones are added (animation from CUIScreenMsg::LayoutScrMsg)
    /// - White text with black outline for readability
    /// </summary>
    public class PickupNoticeUI
    {
        #region Constants (from CUIScreenMsg analysis)
        private const int MAX_MESSAGES = 6;                     // m_llScrMsg._m_uCount >= 6
        private const int DISPLAY_DURATION = 5000;              // How long message stays visible (5s)
        private const int FADE_DURATION = 1500;                 // Alpha fade from 255->0 over 1500ms
        private const int MESSAGE_HEIGHT = 18;                  // Height per message line
        private const int SLIDE_SPEED = 200;                    // Pixels per second for slide animation
        private const int RIGHT_MARGIN = 10;                    // Margin from right edge
        private const int BOTTOM_MARGIN = 60;                   // Margin from bottom (above status bar)
        private const int TEXT_WIDTH = 290;                     // Max text width from client (290 - textWidth)
        #endregion

        #region Fields
        private readonly List<PickupNotice> _notices = new List<PickupNotice>();
        private SpriteFont _font;
        private Texture2D _pixelTexture;
        private int _screenWidth;
        private int _screenHeight;
        private bool _initialized = false;
        #endregion

        #region Initialization
        /// <summary>
        /// Initialize the pickup notice UI with required resources
        /// </summary>
        /// <param name="font">Font for rendering text</param>
        /// <param name="pixelTexture">1x1 white texture for backgrounds/outlines</param>
        /// <param name="screenWidth">Screen width for positioning</param>
        /// <param name="screenHeight">Screen height for positioning</param>
        public void Initialize(SpriteFont font, Texture2D pixelTexture, int screenWidth, int screenHeight)
        {
            _font = font;
            _pixelTexture = pixelTexture;
            _screenWidth = screenWidth;
            _screenHeight = screenHeight;
            _initialized = true;
        }

        /// <summary>
        /// Update screen dimensions (for resolution changes)
        /// </summary>
        public void SetScreenSize(int width, int height)
        {
            _screenWidth = width;
            _screenHeight = height;
        }
        #endregion

        #region Message Adding
        /// <summary>
        /// Add a meso pickup message.
        /// Official format: "You have gained X meso(s)." (StringPool ID 0x12F / 303)
        /// Always uses "(s)" suffix regardless of amount, matching official MapleStory client.
        /// </summary>
        public void AddMesoPickup(int amount, int currentTime)
        {
            // Official MapleStory format - always "meso(s)" with parenthetical s
            string message = $"You have gained {amount} meso(s).";

            AddNotice(new PickupNotice
            {
                Message = message,
                Type = PickupMessageType.MesoPickup,
                TextColor = Color.White, // FONT_BASIC_WHITE in client (yellow is for bonus meso only)
                OutlineColor = Color.Black,
                SpawnTime = currentTime,
                Quantity = amount
            });
        }

        /// <summary>
        /// Add an item pickup message.
        /// Official format from StringPool:
        /// - ID 5443 (single): "You have gained a(n) %s (%s)." - ItemTypeName, ItemName
        /// - ID 5442 (multiple): "You have gained a(n) %s (%s) x %d." - ItemTypeName, ItemName, Quantity
        /// </summary>
        public void AddItemPickup(string itemName, int quantity, int currentTime, Texture2D icon = null,
            bool isRare = false, string itemTypeName = null)
        {
            string message;
            if (string.IsNullOrEmpty(itemTypeName))
            {
                // Simplified format without item type
                message = quantity > 1
                    ? $"You have gained an item ({itemName}) x {quantity}."
                    : $"You have gained an item ({itemName}).";
            }
            else
            {
                // Official format with item type name
                message = quantity > 1
                    ? $"You have gained a(n) {itemTypeName} ({itemName}) x {quantity}."
                    : $"You have gained a(n) {itemTypeName} ({itemName}).";
            }

            var notice = new PickupNotice
            {
                Message = message,
                Type = PickupMessageType.ItemPickup,
                TextColor = isRare ? new Color(255, 200, 100) : Color.White, // Gold for rare items
                OutlineColor = Color.Black,
                SpawnTime = currentTime,
                ItemIcon = icon,
                Quantity = quantity
            };

            AddNotice(notice);
        }

        /// <summary>
        /// Add a quest item pickup message.
        /// Uses same format as regular items but with quest-specific color.
        /// </summary>
        public void AddQuestItemPickup(string itemName, int currentTime, Texture2D icon = null)
        {
            string message = $"You have gained an item ({itemName}).";

            AddNotice(new PickupNotice
            {
                Message = message,
                Type = PickupMessageType.QuestItemPickup,
                TextColor = new Color(150, 255, 150), // Light green for quest items
                OutlineColor = Color.Black,
                SpawnTime = currentTime,
                ItemIcon = icon,
                Quantity = 1
            });
        }

        /// <summary>
        /// Add an inventory full message.
        /// Format: "Your inventory is full." (StringPool ID 0xBD2 / 3026)
        /// </summary>
        public void AddInventoryFullMessage(int currentTime)
        {
            AddNotice(new PickupNotice
            {
                Message = "Your inventory is full.",
                Type = PickupMessageType.InventoryFull,
                TextColor = new Color(255, 100, 100), // Red for error
                OutlineColor = Color.Black,
                SpawnTime = currentTime
            });
        }

        /// <summary>
        /// Add a generic pickup failure message.
        /// </summary>
        public void AddCantPickupMessage(string reason, int currentTime)
        {
            AddNotice(new PickupNotice
            {
                Message = reason ?? "Unable to pick up item.",
                Type = PickupMessageType.CantPickup,
                TextColor = new Color(255, 100, 100), // Red for error
                OutlineColor = Color.Black,
                SpawnTime = currentTime
            });
        }

        /// <summary>
        /// Add a custom notice message
        /// </summary>
        public void AddCustomMessage(string message, Color textColor, int currentTime)
        {
            AddNotice(new PickupNotice
            {
                Message = message,
                Type = PickupMessageType.Unknown,
                TextColor = textColor,
                OutlineColor = Color.Black,
                SpawnTime = currentTime
            });
        }

        private void AddNotice(PickupNotice notice)
        {
            // Remove oldest if at capacity (matching client behavior)
            while (_notices.Count >= MAX_MESSAGES)
            {
                _notices.RemoveAt(0);
            }

            // Set initial Y offset (will animate from bottom)
            notice.YOffset = 0;
            notice.TargetYOffset = 0;

            // Shift existing messages up
            for (int i = 0; i < _notices.Count; i++)
            {
                _notices[i].TargetYOffset += MESSAGE_HEIGHT;
            }

            _notices.Add(notice);
        }
        #endregion

        #region Update
        /// <summary>
        /// Update all notices (animation and expiration)
        /// </summary>
        public void Update(int currentTime, float deltaTime)
        {
            if (!_initialized || _notices.Count == 0)
                return;

            for (int i = _notices.Count - 1; i >= 0; i--)
            {
                var notice = _notices[i];
                int elapsed = currentTime - notice.SpawnTime;

                // Update slide animation
                if (Math.Abs(notice.YOffset - notice.TargetYOffset) > 0.1f)
                {
                    float diff = notice.TargetYOffset - notice.YOffset;
                    float move = SLIDE_SPEED * deltaTime;
                    if (Math.Abs(diff) < move)
                        notice.YOffset = notice.TargetYOffset;
                    else
                        notice.YOffset += Math.Sign(diff) * move;
                }

                // Update alpha (fade out after display duration)
                if (elapsed >= DISPLAY_DURATION)
                {
                    int fadeElapsed = elapsed - DISPLAY_DURATION;
                    if (fadeElapsed >= FADE_DURATION)
                    {
                        notice.IsExpired = true;
                        notice.Alpha = 0;
                    }
                    else
                    {
                        notice.Alpha = 1.0f - (float)fadeElapsed / FADE_DURATION;
                    }
                }

                // Remove expired notices
                if (notice.IsExpired)
                {
                    _notices.RemoveAt(i);
                }
            }
        }
        #endregion

        #region Rendering
        /// <summary>
        /// Draw all pickup notices
        /// </summary>
        public void Draw(SpriteBatch spriteBatch)
        {
            if (!_initialized || _font == null || _notices.Count == 0)
                return;

            // Calculate base position (bottom right)
            float baseX = _screenWidth - RIGHT_MARGIN;
            float baseY = _screenHeight - BOTTOM_MARGIN;

            for (int i = 0; i < _notices.Count; i++)
            {
                var notice = _notices[i];
                if (notice.Alpha <= 0)
                    continue;

                // Measure text to right-align
                Vector2 textSize = _font.MeasureString(notice.Message);

                // Position (right-aligned, stacked from bottom)
                float x = baseX - textSize.X;
                float y = baseY - notice.YOffset - MESSAGE_HEIGHT;

                // Apply alpha
                Color textColor = notice.TextColor * notice.Alpha;
                Color outlineColor = notice.OutlineColor * notice.Alpha;

                // Draw outline (offset by 1 pixel in all directions for bold outline effect)
                DrawTextWithOutline(spriteBatch, notice.Message, new Vector2(x, y), textColor, outlineColor);
            }
        }

        /// <summary>
        /// Draw text with outline effect (like MapleStory's FONT_BASIC_WHITE with FONT_BASIC_BLACK outline)
        /// </summary>
        private void DrawTextWithOutline(SpriteBatch spriteBatch, string text, Vector2 position, Color textColor, Color outlineColor)
        {
            // Draw outline in 8 directions (or 4 for performance)
            Vector2[] offsets = new Vector2[]
            {
                new Vector2(-1, -1), new Vector2(0, -1), new Vector2(1, -1),
                new Vector2(-1, 0),                       new Vector2(1, 0),
                new Vector2(-1, 1),  new Vector2(0, 1),  new Vector2(1, 1)
            };

            foreach (var offset in offsets)
            {
                spriteBatch.DrawString(_font, text, position + offset, outlineColor);
            }

            // Draw main text
            spriteBatch.DrawString(_font, text, position, textColor);
        }
        #endregion

        #region Public Properties
        /// <summary>
        /// Number of active notices
        /// </summary>
        public int NoticeCount => _notices.Count;

        /// <summary>
        /// Check if initialized
        /// </summary>
        public bool IsInitialized => _initialized;

        /// <summary>
        /// Clear all notices
        /// </summary>
        public void Clear()
        {
            _notices.Clear();
        }
        #endregion
    }
}
