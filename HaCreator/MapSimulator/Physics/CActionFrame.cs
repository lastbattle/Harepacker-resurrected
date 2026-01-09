using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace HaCreator.MapSimulator.Physics
{
    /// <summary>
    /// CActionFrame - Sprite composition and merging system based on MapleStory's client.
    /// Handles multi-layer sprite composition with z-ordering, origin alignment, and visibility optimization.
    ///
    /// Based on client's CActionFrame structure:
    /// - LoadMappers(): Load action/frame mappers from WZ
    /// - Merge(): Merge multiple sprites into composite
    /// - MergeGroup(): Merge sprite groups
    /// - UpdateMBR(): Calculate minimum bounding rectangle
    /// - UpdateVisibility(): Visibility optimization
    /// </summary>
    public class CActionFrame
    {
        private readonly List<SpriteLayer> _layers = new();
        private Rectangle _boundingRect;
        private Vector2 _origin;
        private bool _mbrDirty = true;
        private bool _isVisible = true;
        private int _totalDelay = 0;

        /// <summary>
        /// Minimum bounding rectangle encompassing all layers
        /// </summary>
        public Rectangle BoundingRect
        {
            get
            {
                if (_mbrDirty)
                    UpdateMBR();
                return _boundingRect;
            }
        }

        /// <summary>
        /// Combined origin point for the action frame
        /// </summary>
        public Vector2 Origin => _origin;

        /// <summary>
        /// Whether this frame is currently visible
        /// </summary>
        public bool IsVisible => _isVisible;

        /// <summary>
        /// Total animation delay for this frame
        /// </summary>
        public int Delay => _totalDelay;

        /// <summary>
        /// Number of sprite layers
        /// </summary>
        public int LayerCount => _layers.Count;

        #region Layer Management

        /// <summary>
        /// Add a sprite layer at a specific z-order
        /// </summary>
        /// <param name="texture">The sprite texture</param>
        /// <param name="localX">Local X offset from origin</param>
        /// <param name="localY">Local Y offset from origin</param>
        /// <param name="zOrder">Z-order for layering (higher = on top)</param>
        /// <param name="tint">Optional color tint</param>
        /// <param name="delay">Frame delay in ms</param>
        public void AddLayer(Texture2D texture, int localX, int localY, int zOrder, Color? tint = null, int delay = 100)
        {
            var layer = new SpriteLayer
            {
                Texture = texture,
                LocalX = localX,
                LocalY = localY,
                ZOrder = zOrder,
                Tint = tint ?? Color.White,
                Delay = delay,
                Visible = true
            };

            _layers.Add(layer);
            _mbrDirty = true;

            // Keep layers sorted by z-order
            _layers.Sort((a, b) => a.ZOrder.CompareTo(b.ZOrder));

            // Update total delay (use max delay among layers)
            if (delay > _totalDelay)
                _totalDelay = delay;
        }

        /// <summary>
        /// Add a sprite layer from an IDXObject
        /// </summary>
        public void AddLayerFromDXObject(IDXObject dxObject, int zOrder, Color? tint = null)
        {
            // IDXObject doesn't expose texture directly, so we create a wrapper layer
            var layer = new SpriteLayer
            {
                DXObject = dxObject,
                LocalX = dxObject.X,
                LocalY = dxObject.Y,
                ZOrder = zOrder,
                Tint = tint ?? Color.White,
                Delay = dxObject.Delay,
                Visible = true
            };

            _layers.Add(layer);
            _mbrDirty = true;
            _layers.Sort((a, b) => a.ZOrder.CompareTo(b.ZOrder));

            if (dxObject.Delay > _totalDelay)
                _totalDelay = dxObject.Delay;
        }

        /// <summary>
        /// Remove a layer by index
        /// </summary>
        public void RemoveLayer(int index)
        {
            if (index >= 0 && index < _layers.Count)
            {
                _layers.RemoveAt(index);
                _mbrDirty = true;
            }
        }

        /// <summary>
        /// Clear all layers
        /// </summary>
        public void ClearLayers()
        {
            _layers.Clear();
            _mbrDirty = true;
            _totalDelay = 0;
        }

        /// <summary>
        /// Set visibility of a specific layer
        /// </summary>
        public void SetLayerVisible(int index, bool visible)
        {
            if (index >= 0 && index < _layers.Count)
            {
                _layers[index].Visible = visible;
                _mbrDirty = true;
            }
        }

        #endregion

        #region Merge Operations

        /// <summary>
        /// Merge another CActionFrame into this one
        /// </summary>
        /// <param name="other">Frame to merge</param>
        /// <param name="offsetX">X offset for merged frame</param>
        /// <param name="offsetY">Y offset for merged frame</param>
        /// <param name="zOrderOffset">Z-order offset for merged layers</param>
        public void Merge(CActionFrame other, int offsetX = 0, int offsetY = 0, int zOrderOffset = 0)
        {
            foreach (var layer in other._layers)
            {
                var newLayer = new SpriteLayer
                {
                    Texture = layer.Texture,
                    DXObject = layer.DXObject,
                    LocalX = layer.LocalX + offsetX,
                    LocalY = layer.LocalY + offsetY,
                    ZOrder = layer.ZOrder + zOrderOffset,
                    Tint = layer.Tint,
                    Delay = layer.Delay,
                    Visible = layer.Visible
                };
                _layers.Add(newLayer);
            }

            _layers.Sort((a, b) => a.ZOrder.CompareTo(b.ZOrder));
            _mbrDirty = true;
        }

        /// <summary>
        /// Merge a group of CActionFrames
        /// </summary>
        public void MergeGroup(IEnumerable<CActionFrame> frames, int baseZOrder = 0)
        {
            int zOffset = baseZOrder;
            foreach (var frame in frames)
            {
                Merge(frame, 0, 0, zOffset);
                zOffset += 1000; // Large offset to separate groups
            }
        }

        #endregion

        #region MBR (Minimum Bounding Rectangle)

        /// <summary>
        /// Update the minimum bounding rectangle for all visible layers
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateMBR()
        {
            if (_layers.Count == 0)
            {
                _boundingRect = Rectangle.Empty;
                _origin = Vector2.Zero;
                _mbrDirty = false;
                return;
            }

            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;

            foreach (var layer in _layers)
            {
                if (!layer.Visible) continue;

                int layerWidth, layerHeight;
                if (layer.DXObject != null)
                {
                    layerWidth = layer.DXObject.Width;
                    layerHeight = layer.DXObject.Height;
                }
                else if (layer.Texture != null)
                {
                    layerWidth = layer.Texture.Width;
                    layerHeight = layer.Texture.Height;
                }
                else continue;

                int left = layer.LocalX;
                int top = layer.LocalY;
                int right = left + layerWidth;
                int bottom = top + layerHeight;

                if (left < minX) minX = left;
                if (top < minY) minY = top;
                if (right > maxX) maxX = right;
                if (bottom > maxY) maxY = bottom;
            }

            if (minX == int.MaxValue)
            {
                _boundingRect = Rectangle.Empty;
                _origin = Vector2.Zero;
            }
            else
            {
                _boundingRect = new Rectangle(minX, minY, maxX - minX, maxY - minY);
                _origin = new Vector2(-minX, -minY);
            }

            _mbrDirty = false;
        }

        #endregion

        #region Visibility

        /// <summary>
        /// Update visibility based on view frustum
        /// </summary>
        /// <param name="viewX">View left edge</param>
        /// <param name="viewY">View top edge</param>
        /// <param name="viewWidth">View width</param>
        /// <param name="viewHeight">View height</param>
        /// <param name="worldX">Frame world X position</param>
        /// <param name="worldY">Frame world Y position</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateVisibility(int viewX, int viewY, int viewWidth, int viewHeight, int worldX, int worldY)
        {
            if (_mbrDirty)
                UpdateMBR();

            // Check if bounding rect intersects view
            int frameLeft = worldX + _boundingRect.X;
            int frameTop = worldY + _boundingRect.Y;
            int frameRight = frameLeft + _boundingRect.Width;
            int frameBottom = frameTop + _boundingRect.Height;

            _isVisible = !(frameRight < viewX || frameLeft > viewX + viewWidth ||
                          frameBottom < viewY || frameTop > viewY + viewHeight);
        }

        #endregion

        #region Drawing

        /// <summary>
        /// Draw all visible layers
        /// </summary>
        /// <param name="spriteBatch">SpriteBatch for drawing</param>
        /// <param name="worldX">World X position</param>
        /// <param name="worldY">World Y position</param>
        /// <param name="flip">Flip horizontally</param>
        /// <param name="scale">Scale factor</param>
        public void Draw(SpriteBatch spriteBatch, int worldX, int worldY, bool flip = false, float scale = 1f)
        {
            if (!_isVisible) return;

            foreach (var layer in _layers)
            {
                if (!layer.Visible) continue;

                int drawX = worldX + layer.LocalX;
                int drawY = worldY + layer.LocalY;

                if (layer.Texture != null)
                {
                    Rectangle destRect = new Rectangle(
                        drawX,
                        drawY,
                        (int)(layer.Texture.Width * scale),
                        (int)(layer.Texture.Height * scale));

                    spriteBatch.Draw(
                        layer.Texture,
                        destRect,
                        null,
                        layer.Tint,
                        0f,
                        Vector2.Zero,
                        flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
                        0f);
                }
                // Note: DXObject layers should be drawn through their own DrawObject method
                // They're included here for MBR calculation but drawn separately
            }
        }

        /// <summary>
        /// Draw with map shifting (for world-space objects)
        /// </summary>
        public void DrawWorld(SpriteBatch spriteBatch, int mapShiftX, int mapShiftY, int worldX, int worldY, bool flip = false)
        {
            Draw(spriteBatch, worldX + mapShiftX, worldY + mapShiftY, flip);
        }

        #endregion
    }

    /// <summary>
    /// Individual sprite layer within a CActionFrame
    /// </summary>
    internal class SpriteLayer
    {
        public Texture2D Texture;
        public IDXObject DXObject;
        public int LocalX;
        public int LocalY;
        public int ZOrder;
        public Color Tint;
        public int Delay;
        public bool Visible;
    }

    /// <summary>
    /// Action mapper - maps action names to frame sequences
    /// Based on client's action mapping system
    /// </summary>
    public class ActionMapper
    {
        private readonly Dictionary<string, List<CActionFrame>> _actionFrames = new();
        private readonly Dictionary<string, string> _actionAliases = new();

        /// <summary>
        /// Register frames for an action
        /// </summary>
        public void RegisterAction(string actionName, List<CActionFrame> frames)
        {
            _actionFrames[actionName.ToLower()] = frames;
        }

        /// <summary>
        /// Register an alias for an action
        /// </summary>
        public void RegisterAlias(string alias, string targetAction)
        {
            _actionAliases[alias.ToLower()] = targetAction.ToLower();
        }

        /// <summary>
        /// Get frames for an action (follows aliases)
        /// </summary>
        public List<CActionFrame> GetActionFrames(string actionName)
        {
            string key = actionName?.ToLower() ?? "";

            // Follow alias chain
            while (_actionAliases.TryGetValue(key, out string alias))
            {
                key = alias;
            }

            if (_actionFrames.TryGetValue(key, out var frames))
                return frames;

            return null;
        }

        /// <summary>
        /// Check if action exists
        /// </summary>
        public bool HasAction(string actionName)
        {
            string key = actionName?.ToLower() ?? "";
            while (_actionAliases.TryGetValue(key, out string alias))
                key = alias;
            return _actionFrames.ContainsKey(key);
        }

        /// <summary>
        /// Get all registered action names
        /// </summary>
        public IEnumerable<string> GetActionNames() => _actionFrames.Keys;
    }
}
