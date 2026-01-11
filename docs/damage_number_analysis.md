# Damage Number Animation System (CAnimationDisplayer Binary Analysis)

### Key Functions Decompiled
- `CAnimationDisplayer::Effect_HP` at 0x444eb0 - Main damage number display
- `CAnimationDisplayer::Effect_BasicFloat` at 0x446530 - Basic floating animation
- `CAnimationDisplayer::Effect_Miss` at 0x449a50 - Miss effect display
- `CAnimationDisplayer::Effect_Guard` at 0x4498e0 - Guard effect display
- `CAnimationDisplayer::CAnimationDisplayer` at 0x449010 - Constructor (resource loading)

---

## 1. Animation Timing Constants

### From Effect_HP and Effect_BasicFloat decompilation:

```cpp
// Phase 1: Initial display (stationary, full alpha)
vAlpha0 = 255          // Starting alpha (fully opaque)
vAlpha1 = 255          // Ending alpha for phase 1 (still opaque)
vDelay = 400           // Duration: 400ms (0.4 seconds) - number stays visible

// Phase 2: Fade out while rising
vAlpha0 = 255          // Starting alpha
vAlpha1 = 0            // Ending alpha (fully transparent)
vDelay = 600           // Duration: 600ms (0.6 seconds) - fade out period

// Total animation lifetime: 400ms + 600ms = 1000ms (1 second)
```

### Summary:
| Constant | Value | Description |
|----------|-------|-------------|
| Display Duration | 400ms | Time digits stay stationary at full alpha |
| Fade Duration | 600ms | Time for alpha fade from 255 to 0 |
| Total Lifetime | 1000ms | Complete animation duration |

---

## 2. Digit Spacing/Kerning Values

### From WZ Canvas Analysis (Effect.wz/BasicEff.img):

Digits are positioned based on their canvas width and origin point. No explicit kerning constant was found - spacing is determined by the canvas properties.

| Damage Type | Digit Width | Origin X | Effective Spacing |
|-------------|-------------|----------|-------------------|
| NoRed0 | ~31px | ~15 | ~31px between digit centers |
| NoRed1 | ~37px | ~18 | ~37px between digit centers |
| NoCri0 | ~37px | ~18 | ~37px between digit centers |
| NoCri1 | ~43px | ~21 | ~43px between digit centers |

### Digit Positioning Formula:
```csharp
// To render a multi-digit number centered at (centerX, centerY):
int totalWidth = 0;
foreach (char digit in damageString)
    totalWidth += GetDigitCanvas(digit).Width;

int startX = centerX - totalWidth / 2;
int currentX = startX;

foreach (char digit in damageString)
{
    var canvas = GetDigitCanvas(digit);
    int drawX = currentX - canvas.Origin.X;
    int drawY = centerY - canvas.Origin.Y;
    Draw(canvas, drawX, drawY);
    currentX += canvas.Width;  // No additional kerning adjustment
}
```

---

## 3. Scale and Fade Parameters

### Alpha Fade Curve:
```
Phase 1 (0-400ms):   Alpha = 255 (constant, fully opaque)
Phase 2 (400-1000ms): Alpha = Lerp(255, 0, progress)
                      where progress = (time - 400) / 600
```

### No Scale Animation:
The Effect_HP and Effect_BasicFloat functions do not apply any scale transformation.
Damage numbers maintain their original size throughout the animation.

**Note**: The binary uses "Large" vs "Small" digit sets (NoRed0 vs NoRed1) for different damage ranges, not dynamic scaling:
- Small digits (NoRed0, NoBlue0, etc.): ~31x33 pixels
- Large digits (NoRed1, NoBlue1, etc.): ~37x38 pixels
- Critical large (NoCri1): ~43x48 pixels

---

## 4. Multi-Hit Stacking Offsets

### Key Finding:
No explicit Y-axis stacking offset was found within the `Effect_HP` function itself.

Multi-hit damage display is handled at a higher level by making multiple calls to `Effect_HP` with:
1. Different timing (sequential calls)
2. Different Y positions passed as `lCenterTop` parameter

### Recommended Implementation:
```csharp
// For multi-hit attacks, stack numbers vertically
const int STACK_OFFSET_Y = 20;  // Suggested offset between stacked numbers

for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
{
    int stackedY = baseY - (hitIndex * STACK_OFFSET_Y);
    int delayMs = hitIndex * 100;  // 100ms between each hit display
    SpawnDamageNumber(damage[hitIndex], centerX, stackedY, delayMs);
}
```

---

## 5. Rise/Float Animation Parameters

### From Effect_BasicFloat (0x446530):

```cpp
// Initial position calculation
v15 = lCenterLeft - lY / 2;      // Center horizontally based on canvas width
lY = lCenterTop - v35;           // Adjust for canvas height

// Phase 1: Stationary
IWzVector2D::RelMove(layer, v15, lY, ...);

// Phase 2: Rise upward
// After delay = GetAnimationDelay() + 250 (~250ms after first canvas inserted)
IWzVector2D::RelMove(layer, v15, lY - 30, ...);  // Rise 30 pixels
```

### Rise Animation Constants:
| Constant | Value | Description |
|----------|-------|-------------|
| Rise Distance | 30 pixels | Total vertical movement during fade |
| Rise Start Delay | ~400ms | Begins when fade phase starts |
| Rise Duration | 600ms | Matches fade duration |
| Rise Speed | 50 px/sec | 30px / 0.6sec |

---

## 6. Random Offset for Overlap Prevention

### Key Finding:
**No random offset was found in Effect_HP.**

The `lCenterLeft` and `lCenterTop` parameters are used directly without randomization.

If random offset is needed, it should be applied at the caller level:
```csharp
// Optional: Add small random offset to prevent perfect overlap
int randomOffsetX = Random.Shared.Next(-5, 6);  // -5 to +5 pixels
int randomOffsetY = Random.Shared.Next(-5, 6);
SpawnDamageNumber(damage, centerX + randomOffsetX, centerY + randomOffsetY);
```

---

## 7. Critical Hit Special Effects

### From Effect_HP decompilation:

```cpp
if (bCriticalAttack)
{
    pEffLarge = m_pEffNo_Cri1;  // Larger critical digits
    pEffSmall = m_pEffNo_Cri0;  // Normal-size critical digits

    // Critical effect sprite (burst behind number)
    pCanvasCriticalEffect = NoCri1["effect"];
    // Inserted at (idx, lY - 30) - 30 pixels above the digits
    // delay = GetAnimationDelay() + 250 (about 250ms after digits appear)
}
```

### Critical Effect Sprite Properties:
- Path: `Effect.wz/BasicEff.img/NoCri1/effect`
- Size: 62x57 pixels
- Origin: (41, 70) - positioned to center behind the critical number
- Position: 30 pixels above the damage number (lY - 30)
- Timing: Appears ~250ms after the digits

### Critical Hit Timing Summary:
| Time | Action |
|------|--------|
| 0ms | Spawn critical damage digits (NoCri0 or NoCri1) |
| ~250ms | Show critical burst effect (NoCri1/effect) |
| 400ms | Begin fade and rise |
| 1000ms | Animation complete |

---

## 8. Color Types (lColorType Parameter)

### From Effect_HP switch statement:

```cpp
switch (lColorType)
{
    case 0:  // Player damage to monster (RED)
        if (bCriticalAttack) {
            pEffLarge = m_pEffNo_Cri1;   // Critical orange/yellow
            pEffSmall = m_pEffNo_Cri0;
        } else {
            pEffLarge = m_pEffNo_Red1;   // Normal red
            pEffSmall = m_pEffNo_Red0;
        }
        break;

    case 1:  // Damage to player (BLUE)
        pEffLarge = m_pEffNo_Blue1;
        pEffSmall = m_pEffNo_Blue0;
        break;

    case 2:  // Party/summon damage (VIOLET)
        pEffLarge = m_pEffNo_Violet1;
        pEffSmall = m_pEffNo_Violet0;
        break;
}
```

---

## 9. WZ Resource Paths

### From Constructor (0x449010):

```
Base path: Effect.wz/BasicEff.img (StringPool ID 988 / 0x3DC)

Resource Members:
- m_pEffNo_Red0     (StringPool 989): "NoRed0"     - Player damage (small)
- m_pEffNo_Red1     (StringPool 990): "NoRed1"     - Player damage (large)
- m_pEffNo_Blue0    (StringPool 991): "NoBlue0"    - Received damage (small)
- m_pEffNo_Blue1    (StringPool 992): "NoBlue1"    - Received damage (large)
- m_pEffNo_Violet0  (StringPool 993): "NoViolet0"  - Party damage (small)
- m_pEffNo_Violet1  (StringPool 994): "NoViolet1"  - Party damage (large)
- m_pEffNo_Cri0     (StringPool 995): "NoCri0"     - Critical (small)
- m_pEffNo_Cri1     (StringPool 996): "NoCri1"     - Critical (large)
```

---

## 10. Canvas Properties Summary (from WzImg MCP)

### NoRed0 (Normal player damage - smaller)
| Element | Size | Origin |
|---------|------|--------|
| Digits 0-9 | ~31x33 | ~(15, 32) |
| Miss | 98x38 | (49, 37) |
| guard | 107x38 | (54, 38) |
| shot | 137x65 | - |
| counter | 130x36 | (66, 32) |
| resist | 99x36 | (49, 32) |

### NoRed1 (Normal player damage - larger)
| Element | Size | Origin |
|---------|------|--------|
| Digits 0-9 | ~37x38 | ~(18, 37) |

### NoBlue0/1 (Damage received by player)
Same dimensions as NoRed0/1 but with blue-tinted sprites.

### NoViolet0/1 (Party/summon damage)
Same dimensions as NoRed0/1 but with violet-tinted sprites.

### NoCri0 (Critical - smaller)
| Element | Size | Origin |
|---------|------|--------|
| Digits 0-9 | ~37x38 | ~(18, 37) |

### NoCri1 (Critical - larger with effect)
| Element | Size | Origin |
|---------|------|--------|
| Digits 0-9 | ~43x48 | ~(21, 45) |
| effect | 62x57 | (41, 70) |

---

## 11. Complete Animation Timeline

```
Time 0ms:      Spawn damage digits at (centerX, centerTop - canvasHeight)
               Alpha = 255, Position = initial

Time 0-400ms:  Phase 1 - Display
               - Digits remain stationary
               - Alpha stays at 255 (fully opaque)
               - No position change

Time ~250ms:   (Critical only) Show burst effect
               - Effect sprite at (centerX, centerY - 30)
               - Behind the damage digits

Time 400ms:    Begin Phase 2 - Fade & Rise

Time 400-1000ms: Phase 2 - Animation
                 - Alpha fades: 255 -> 0 (linear)
                 - Position rises: Y -> Y - 30 (linear)
                 - Duration: 600ms

Time 1000ms:   Animation complete
               - Remove from display
```

---

## 12. Implementation Example

```csharp
public class DamageNumberAnimation
{
    // Constants from binary analysis
    public const int DISPLAY_DURATION_MS = 400;
    public const int FADE_DURATION_MS = 600;
    public const int TOTAL_LIFETIME_MS = 1000;
    public const float RISE_DISTANCE_PX = 30f;
    public const int CRITICAL_EFFECT_DELAY_MS = 250;
    public const int CRITICAL_EFFECT_OFFSET_Y = -30;

    private float _elapsedMs;
    private Vector2 _basePosition;
    private bool _isCritical;

    public float Alpha
    {
        get
        {
            if (_elapsedMs < DISPLAY_DURATION_MS)
                return 1.0f;

            float fadeProgress = (_elapsedMs - DISPLAY_DURATION_MS) / FADE_DURATION_MS;
            return 1.0f - Math.Clamp(fadeProgress, 0f, 1f);
        }
    }

    public float YOffset
    {
        get
        {
            if (_elapsedMs < DISPLAY_DURATION_MS)
                return 0f;

            float riseProgress = (_elapsedMs - DISPLAY_DURATION_MS) / FADE_DURATION_MS;
            return -RISE_DISTANCE_PX * Math.Clamp(riseProgress, 0f, 1f);
        }
    }

    public bool ShouldShowCriticalEffect =>
        _isCritical && _elapsedMs >= CRITICAL_EFFECT_DELAY_MS;

    public bool IsComplete => _elapsedMs >= TOTAL_LIFETIME_MS;

    public void Update(float deltaMs)
    {
        _elapsedMs += deltaMs;
    }
}
```

---

## 13. Digit Selection Logic

### Small vs Large Digits
The binary uses two size variants for each color type:
- **Small (index 0)**: NoRed0, NoBlue0, NoViolet0, NoCri0
- **Large (index 1)**: NoRed1, NoBlue1, NoViolet1, NoCri1

The selection between small and large is likely based on damage value thresholds (not found in Effect_HP itself - determined at caller level).

### Suggested Implementation:
```csharp
public enum DamageNumberSize { Small, Large }
public enum DamageColorType { Red = 0, Blue = 1, Violet = 2, Critical = 3 }

public string GetResourcePath(DamageColorType color, DamageNumberSize size, bool isCritical)
{
    if (isCritical || color == DamageColorType.Critical)
        return size == DamageNumberSize.Large ? "NoCri1" : "NoCri0";

    string colorName = color switch
    {
        DamageColorType.Red => "NoRed",
        DamageColorType.Blue => "NoBlue",
        DamageColorType.Violet => "NoViolet",
        _ => "NoRed"
    };

    return colorName + (size == DamageNumberSize.Large ? "1" : "0");
}
```

---

## Summary of Key Constants

| Constant | Value | Source |
|----------|-------|--------|
| Display Duration | 400ms | Effect_BasicFloat line 130 |
| Fade Duration | 600ms | Effect_BasicFloat line 220 |
| Total Lifetime | 1000ms | Sum of above |
| Rise Distance | 30px | Effect_BasicFloat line 345 |
| Rise Speed | 50 px/sec | Calculated |
| Critical Effect Delay | ~250ms | Effect_HP line 919 |
| Critical Effect Y Offset | -30px | Effect_HP line 928 |
| Start Alpha | 255 | Effect_BasicFloat line 126-128 |
| End Alpha | 0 | Effect_BasicFloat line 216-218 |
