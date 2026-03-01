using System;
using System.Collections.Generic;
using System.Drawing;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.Converters;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Point = Microsoft.Xna.Framework.Point;

namespace HaCreator.MapSimulator.Loaders
{
    /// <summary>
    /// Damage number color types based on MapleStory client.
    /// </summary>
    public enum DamageColorType
    {
        /// <summary>Player damage to monster (Red)</summary>
        Red = 0,
        /// <summary>Healing numbers (Blue)</summary>
        Blue = 1,
        /// <summary>Damage received by player from monsters (Violet)</summary>
        Violet = 2
    }

    /// <summary>
    /// Size variant for damage numbers.
    /// </summary>
    public enum DamageNumberSize
    {
        /// <summary>Smaller digits for normal hits</summary>
        Small = 0,
        /// <summary>Larger digits for critical or high damage</summary>
        Large = 1
    }

    /// <summary>
    /// Contains loaded digit textures and metadata for a damage number type.
    /// </summary>
    public class DamageNumberDigitSet
    {
        /// <summary>Digit textures 0-9</summary>
        public Texture2D[] Digits { get; } = new Texture2D[10];

        /// <summary>Origin points for each digit (for proper alignment)</summary>
        public Point[] Origins { get; } = new Point[10];

        /// <summary>Width of each digit</summary>
        public int[] Widths { get; } = new int[10];

        /// <summary>Height of each digit</summary>
        public int[] Heights { get; } = new int[10];

        /// <summary>Special text sprites (Miss, guard, counter, resist, shot)</summary>
        public Dictionary<string, Texture2D> SpecialTextures { get; } = new Dictionary<string, Texture2D>();

        /// <summary>Special text origins</summary>
        public Dictionary<string, Point> SpecialOrigins { get; } = new Dictionary<string, Point>();

        /// <summary>Critical effect sprite (for NoCri1)</summary>
        public Texture2D CriticalEffectTexture { get; set; }

        /// <summary>Critical effect origin</summary>
        public Point CriticalEffectOrigin { get; set; }

        /// <summary>Whether this digit set is valid and loaded</summary>
        public bool IsLoaded { get; set; }

        /// <summary>The name of this digit set (e.g., "NoRed0", "NoBlue1")</summary>
        public string Name { get; set; }

        /// <summary>
        /// Get the total width for rendering a number string.
        /// Uses the authentic MapleStory spacing algorithm from CAnimationDisplayer::Effect_HP.
        ///
        /// Binary analysis (v115, address 0x444eb0) revealed the spacing formula:
        /// - For each digit: overlap = 3 * (origin.x - width) / 5
        /// - Total width is accumulated using: accumulatedX = accumulatedX - previousOverlap + originX
        /// </summary>
        public int GetTotalWidth(string numberString)
        {
            int accumulatedX = 0;  // v15 in the binary
            int previousOverlap = 0;  // lY in the binary

            foreach (char c in numberString)
            {
                if (!char.IsDigit(c))
                    continue;

                int digit = c - '0';
                int width = Widths[digit];
                int originX = Origins[digit].X;

                // Update accumulated position: accumulatedX = accumulatedX - previousOverlap + originX
                // Binary: v15 = v15 - lY + idx; (where idx is origin.x)
                accumulatedX = accumulatedX - previousOverlap + originX;

                // Calculate overlap for next digit: lY = 3 * (origin.x - width) / 5
                // Binary: lY = 3 * v34 / 5; where v34 = idx - lWidth = origin.x - width
                previousOverlap = 3 * (originX - width) / 5;
            }

            // Total width is the final accumulated position
            // Binary: lWidth = v15; (line 305)
            return accumulatedX;
        }
    }

    /// <summary>
    /// Loads damage number digit sprites from Effect.wz/BasicEff.img.
    /// Based on MapleStory client CAnimationDisplayer analysis.
    /// </summary>
    public static class DamageNumberLoader
    {
        // Cached digit sets
        private static readonly Dictionary<string, DamageNumberDigitSet> _digitSets = new Dictionary<string, DamageNumberDigitSet>();
        private static bool _initialized = false;

        /// <summary>
        /// Damage number type names in WZ files.
        /// Index: [ColorType * 2 + SizeVariant]
        /// </summary>
        private static readonly string[] DamageTypeNames = new string[]
        {
            "NoRed0",    // Red small (player damage, normal)
            "NoRed1",    // Red large (player damage, large)
            "NoBlue0",   // Blue small (damage received, normal)
            "NoBlue1",   // Blue large (damage received, large)
            "NoViolet0", // Violet small (party damage, normal)
            "NoViolet1", // Violet large (party damage, large)
            "NoCri0",    // Critical small
            "NoCri1"     // Critical large (with effect)
        };

        /// <summary>
        /// Special text property names that may exist in damage number sets.
        /// </summary>
        private static readonly string[] SpecialTextNames = new string[]
        {
            "Miss",
            "guard",
            "shot",
            "counter",
            "resist"
        };

        /// <summary>
        /// Load all damage number digit sets from Effect.wz/BasicEff.img.
        /// </summary>
        /// <param name="device">Graphics device for texture creation</param>
        /// <param name="basicEffImage">Effect.wz/BasicEff.img WzImage</param>
        /// <returns>True if at least one digit set was loaded</returns>
        public static bool LoadDamageNumbers(GraphicsDevice device, WzImage basicEffImage)
        {
            if (basicEffImage == null)
            {
                System.Diagnostics.Debug.WriteLine("[DamageNumberLoader] BasicEff.img is null");
                return false;
            }

            _digitSets.Clear();
            bool anyLoaded = false;

            foreach (string typeName in DamageTypeNames)
            {
                WzSubProperty typeProperty = basicEffImage[typeName] as WzSubProperty;
                if (typeProperty == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[DamageNumberLoader] {typeName} not found in BasicEff.img");
                    continue;
                }

                var digitSet = LoadDigitSet(device, typeProperty, typeName);
                if (digitSet.IsLoaded)
                {
                    _digitSets[typeName] = digitSet;
                    anyLoaded = true;
                    System.Diagnostics.Debug.WriteLine($"[DamageNumberLoader] Loaded {typeName}");
                }
            }

            _initialized = anyLoaded;
            return anyLoaded;
        }

        /// <summary>
        /// Load a single digit set from a WZ property.
        /// </summary>
        private static DamageNumberDigitSet LoadDigitSet(GraphicsDevice device, WzSubProperty typeProperty, string name)
        {
            var digitSet = new DamageNumberDigitSet { Name = name };
            int loadedDigits = 0;

            // Load digits 0-9
            for (int i = 0; i < 10; i++)
            {
                WzCanvasProperty digitCanvas = typeProperty[i.ToString()] as WzCanvasProperty;
                if (digitCanvas == null)
                    continue;

                try
                {
                    var bitmap = digitCanvas.GetLinkedWzCanvasBitmap();
                    if (bitmap != null)
                    {
                        digitSet.Digits[i] = bitmap.ToTexture2D(device);
                        digitSet.Widths[i] = bitmap.Width;
                        digitSet.Heights[i] = bitmap.Height;

                        // Get origin point
                        var originProp = digitCanvas["origin"] as WzVectorProperty;
                        if (originProp != null)
                        {
                            digitSet.Origins[i] = new Point(originProp.X.Value, originProp.Y.Value);
                        }

                        loadedDigits++;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DamageNumberLoader] Error loading digit {i} from {name}: {ex.Message}");
                }
            }

            // Load special text sprites (Miss, guard, etc.)
            foreach (string specialName in SpecialTextNames)
            {
                WzCanvasProperty specialCanvas = typeProperty[specialName] as WzCanvasProperty;
                if (specialCanvas == null)
                    continue;

                try
                {
                    var bitmap = specialCanvas.GetLinkedWzCanvasBitmap();
                    if (bitmap != null)
                    {
                        digitSet.SpecialTextures[specialName] = bitmap.ToTexture2D(device);

                        var originProp = specialCanvas["origin"] as WzVectorProperty;
                        if (originProp != null)
                        {
                            digitSet.SpecialOrigins[specialName] = new Point(originProp.X.Value, originProp.Y.Value);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DamageNumberLoader] Error loading special {specialName} from {name}: {ex.Message}");
                }
            }

            // Load critical effect sprite (for NoCri1)
            if (name == "NoCri1")
            {
                WzCanvasProperty effectCanvas = typeProperty["effect"] as WzCanvasProperty;
                if (effectCanvas != null)
                {
                    try
                    {
                        var bitmap = effectCanvas.GetLinkedWzCanvasBitmap();
                        if (bitmap != null)
                        {
                            digitSet.CriticalEffectTexture = bitmap.ToTexture2D(device);

                            var originProp = effectCanvas["origin"] as WzVectorProperty;
                            if (originProp != null)
                            {
                                digitSet.CriticalEffectOrigin = new Point(originProp.X.Value, originProp.Y.Value);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DamageNumberLoader] Error loading effect from {name}: {ex.Message}");
                    }
                }
            }

            digitSet.IsLoaded = loadedDigits == 10;
            return digitSet;
        }

        /// <summary>
        /// Get the digit set for a specific damage type.
        /// </summary>
        /// <param name="colorType">Color type (Red, Blue, Violet)</param>
        /// <param name="size">Size variant (Small, Large)</param>
        /// <param name="isCritical">Whether to use critical digits</param>
        /// <returns>The digit set, or null if not loaded</returns>
        public static DamageNumberDigitSet GetDigitSet(DamageColorType colorType, DamageNumberSize size, bool isCritical)
        {
            string typeName = GetTypeName(colorType, size, isCritical);
            return _digitSets.TryGetValue(typeName, out var set) ? set : null;
        }

        /// <summary>
        /// Get a digit set by name.
        /// </summary>
        /// <param name="typeName">Type name (e.g., "NoRed0", "NoCri1")</param>
        /// <returns>The digit set, or null if not loaded</returns>
        public static DamageNumberDigitSet GetDigitSetByName(string typeName)
        {
            return _digitSets.TryGetValue(typeName, out var set) ? set : null;
        }

        /// <summary>
        /// Get the WZ type name for a damage configuration.
        /// </summary>
        private static string GetTypeName(DamageColorType colorType, DamageNumberSize size, bool isCritical)
        {
            if (isCritical)
            {
                return size == DamageNumberSize.Large ? "NoCri1" : "NoCri0";
            }

            string colorName = colorType switch
            {
                DamageColorType.Red => "NoRed",
                DamageColorType.Blue => "NoBlue",
                DamageColorType.Violet => "NoViolet",
                _ => "NoRed"
            };

            return colorName + (size == DamageNumberSize.Large ? "1" : "0");
        }

        /// <summary>
        /// Whether damage numbers are loaded and ready.
        /// </summary>
        public static bool IsInitialized => _initialized;

        /// <summary>
        /// Get count of loaded digit sets.
        /// </summary>
        public static int LoadedSetCount => _digitSets.Count;

        /// <summary>
        /// Clear all loaded digit sets and dispose textures.
        /// </summary>
        public static void Clear()
        {
            foreach (var set in _digitSets.Values)
            {
                // Dispose digit textures
                foreach (var texture in set.Digits)
                {
                    texture?.Dispose();
                }

                // Dispose special textures
                foreach (var texture in set.SpecialTextures.Values)
                {
                    texture?.Dispose();
                }

                // Dispose critical effect
                set.CriticalEffectTexture?.Dispose();
            }

            _digitSets.Clear();
            _initialized = false;
        }
    }
}
