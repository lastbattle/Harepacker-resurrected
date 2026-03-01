using System;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace HaCreator.MapSimulator.Core
{
    /// <summary>
    /// Physics constants loaded from Map.wz/Physics.img
    ///
    /// Official client formulas:
    ///   AccSpeed: v += (force / mass) * tSec, clamped to maxSpeed
    ///   DecSpeed: v -= (drag / mass) * tSec, clamped to 0
    ///   Position: pos += (v_old + v_new) * 0.5 * tSec (trapezoidal integration)
    ///
    /// Physics.img stores raw values that are modified by character attributes:
    ///   finalWalkSpeed = shoeWalkSpeed * physicsWalkSpeed * footholdWalk
    ///   finalForce = shoeWalkAcc * physicsWalkForce * footholdDrag * fieldWalk
    /// </summary>
    public class PhysicsConstants
    {
        #region Singleton

        private static PhysicsConstants _instance;
        public static PhysicsConstants Instance => _instance ?? (_instance = new PhysicsConstants());

        #endregion

        #region Character Attribute Defaults (from CAttrShoe)

        // ============================================================
        // These are the DEFAULT attribute values for a "naked" character.
        // Equipment (shoes) can modify these values.
        //
        // The CalcWalk formula uses these multipliers:
        //   maxSpeed = shoeWalkSpeed * physicsWalkSpeed * footholdDrag
        //   force = shoeWalkAcc * physicsWalkForce * footholdDrag * fieldWalk
        //
        // Character Speed stat (e.g., 100) converts to physics multiplier:
        //   shoeWalkSpeed = characterSpeed / 1000 = 100/1000 = 0.1
        // This gives: maxWalkSpeed = 0.1 * 1250 * 1.0 = 125 px/s
        // ============================================================

        /// <summary>
        /// Default CAttrShoe::walkSpeed attribute (from constructor at 0x50b710)
        /// Value is 1.0, but character Speed stat divides by 1000 when applied
        /// </summary>
        public double DefaultShoeWalkSpeed { get; set; } = 1.0;

        /// <summary>
        /// Default CAttrShoe::walkAcc attribute (from constructor)
        /// </summary>
        public double DefaultShoeWalkAcc { get; set; } = 1.0;

        /// <summary>
        /// Default CAttrShoe::walkDrag attribute (from constructor)
        /// </summary>
        public double DefaultShoeWalkDrag { get; set; } = 1.0;

        #endregion

        #region Raw Physics Values (from Physics.img)

        // ============================================================
        // These are the ACTUAL values from Map.wz/Physics.img
        // ============================================================

        // Walking physics (raw values from Physics.img)
        private double _rawWalkForce = 999999.0;   // Very high for near-instant acceleration
        private double _rawWalkSpeed = 1250.0;     // Base walk speed (before character multiplier)
        private double _rawWalkDrag = 10000.0;     // Deceleration force

        // Slipping physics (slopes)
        private double _rawSlipForce = 90000.0;    // Force on steep slopes
        private double _rawSlipSpeed = 420.0;      // Max slip speed

        // Floating/air physics
        private double _rawFloatDrag1 = 300000.0;  // Air drag (high altitude)
        private double _rawFloatDrag2 = 30000.0;   // Air drag (low altitude)
        private double _rawFloatCoefficient = 0.03;

        // Swimming physics
        private double _rawSwimForce = 320000.0;   // Swimming force
        private double _rawSwimSpeed = 440.0;      // Max swim speed
        private double _rawSwimSpeedDec = 0.1;     // Swim speed reduction

        // Flying physics
        private double _rawFlyForce = 420000.0;    // Flying force
        private double _rawFlySpeed = 600.0;       // Max fly speed
        private double _rawFlyJumpDec = 0.15f;     // Flying jump reduction

        // Gravity and falling
        private double _rawGravityAcc = 3000.0;    // Gravity acceleration (px/s² base, scaled by time in CalcFloat)
        private double _rawFallSpeed = 1670.0;     // Terminal velocity (raw)
        private double _rawJumpSpeed = 1555.0;     // Jump velocity (raw)

        // Friction limits
        private double _rawMaxFriction = 10.0;     // Maximum friction coefficient
        private double _rawMinFriction = 0.2;      // Minimum friction coefficient

        #endregion

        #region Scaled Physics Values (for gameplay)

        // These properties apply the character multipliers to get gameplay-ready values.
        // The official client applies character shoe/equipment stats, but since we don't
        // have a full equipment system, we use base multipliers.

        /// <summary>
        /// Walk force from Physics.img (raw value)
        /// Used in AccSpeed: acceleration = (walkForce * shoeWalkAcc) / mass
        /// Character Speed stat provides the multiplier via GetMoveSpeed()
        /// </summary>
        public double WalkForce => _rawWalkForce * DefaultShoeWalkAcc;

        /// <summary>
        /// Maximum walk speed base from Physics.img (raw value = 1250)
        ///
        /// Official CalcWalk formula:
        ///   vMax = walkSpeed * (dWalkSpeed * footholdDrag)
        /// Where:
        ///   walkSpeed = CAttrShoe::walkSpeed = characterSpeed / dWalkSpeed
        ///   dWalkSpeed = Physics.img walkSpeed = 1250
        ///
        /// This simplifies to:
        ///   vMax = (Speed / 1250) * 1250 * footholdDrag = Speed * footholdDrag
        ///
        /// So with default footholdDrag of 1.0, Speed stat = walk speed in px/s:
        ///   Speed 100 = 100 px/s
        ///   Speed 140 = 140 px/s (with Haste)
        /// </summary>
        public double WalkSpeed => _rawWalkSpeed * DefaultShoeWalkSpeed;

        /// <summary>
        /// Walk drag - tuned for responsive stopping
        /// Raw Physics.img value = 10000, gives 100 px/s² deceleration (too slow!)
        /// MapleNecrocer uses 0.25 px/frame² = ~900 px/s² at 60fps
        /// We use 80000 to give 800 px/s² deceleration (stops from 125 px/s in ~0.15s)
        /// </summary>
        public double WalkDrag => 80000.0;  // Tuned value for responsive stopping

        // Slipping physics - raw values from Physics.img
        public double SlipForce => _rawSlipForce * DefaultShoeWalkAcc;
        public double SlipSpeed => _rawSlipSpeed * DefaultShoeWalkSpeed;

        // Floating/air physics - raw values from Physics.img
        public double FloatDrag1 => _rawFloatDrag1;
        public double FloatDrag2 => _rawFloatDrag2;
        public double FloatCoefficient => _rawFloatCoefficient;

        // Swimming physics - raw values with default shoe attributes
        // CAttrShoe defaults: swimAcc = 1.0, swimSpeedH = 1.0, swimSpeedV = 1.0
        public double SwimForce => _rawSwimForce * 1.0;  // * shoeSwimAcc
        public double SwimSpeed => _rawSwimSpeed * 1.0;  // * shoeSwimSpeed
        public double SwimSpeedDec => _rawSwimSpeedDec;

        // Flying physics - raw values (disabled by default, flyAcc/flySpeed = 0 for non-flying)
        public double FlyForce => _rawFlyForce;
        public double FlySpeed => _rawFlySpeed;
        public double FlyJumpDec => _rawFlyJumpDec;

        // Gravity and falling - scaled for gameplay feel
        // Physics.img raw values are too high for natural-feeling movement
        // MapleNecrocer reference (at 60fps): jump=570, gravity=2160, fall=480
        // We use tuned values that match the reference implementation

        /// <summary>
        /// Gravity acceleration (px/s²)
        /// Raw Physics.img value = 3000, scaled to ~2000 for natural feel
        /// MapleNecrocer uses 0.6 per frame² = 2160 px/s² at 60fps
        /// </summary>
        public double GravityAcc => 2000.0;  // Tuned value, ignore raw

        /// <summary>
        /// Terminal fall velocity (px/s)
        /// Raw Physics.img value = 1670, scaled to ~670 for natural feel
        /// MapleNecrocer uses 8 per frame = 480 px/s at 60fps
        /// </summary>
        public double FallSpeed => 670.0;  // Tuned value, ignore raw

        /// <summary>
        /// Jump velocity (px/s)
        /// Raw Physics.img value = 1555, scaled to ~555 for natural feel
        /// MapleNecrocer uses 9.5 per frame = 570 px/s at 60fps
        /// </summary>
        public double JumpSpeed => 555.0;  // Tuned value, ignore raw

        // Friction limits
        public double MaxFriction => _rawMaxFriction;
        public double MinFriction => _rawMinFriction;

        #endregion

        #region Computed Values

        /// <summary>
        /// Default mass for physics calculations (from client)
        /// Used in AccSpeed/DecSpeed: acceleration = force / mass
        /// </summary>
        public double DefaultMass { get; private set; } = 100.0;

        /// <summary>
        /// Walk acceleration = WalkForce / DefaultMass (px/s²)
        /// Using official AccSpeed formula: v += (force / mass) * tSec
        /// </summary>
        public double WalkAcceleration => WalkForce / DefaultMass;

        /// <summary>
        /// Walk deceleration = WalkDrag / DefaultMass (px/s²)
        /// Using official DecSpeed formula: v -= (drag / mass) * tSec
        /// </summary>
        public double WalkDeceleration => WalkDrag / DefaultMass;

        /// <summary>
        /// Swim speed multiplier applied on top of walk speed
        /// </summary>
        public double SwimSpeedMultiplier => SwimSpeedDec;

        #endregion

        #region Loading

        public bool IsLoaded { get; private set; }

        /// <summary>
        /// Load physics constants from Physics.img WzImage
        /// </summary>
        public void LoadFromWzImage(WzImage physicsImg)
        {
            if (physicsImg == null)
            {
                System.Diagnostics.Debug.WriteLine("[PhysicsConstants] Physics.img is null, using defaults");
                return;
            }

            try
            {
                // Parse if needed
                if (!physicsImg.Parsed)
                    physicsImg.ParseImage();

                // Load raw values from Physics.img (these get scaled by properties)
                _rawWalkForce = GetDouble(physicsImg, "walkForce", _rawWalkForce);
                _rawWalkSpeed = GetDouble(physicsImg, "walkSpeed", _rawWalkSpeed);
                _rawWalkDrag = GetDouble(physicsImg, "walkDrag", _rawWalkDrag);

                _rawSlipForce = GetDouble(physicsImg, "slipForce", _rawSlipForce);
                _rawSlipSpeed = GetDouble(physicsImg, "slipSpeed", _rawSlipSpeed);

                _rawFloatDrag1 = GetDouble(physicsImg, "floatDrag1", _rawFloatDrag1);
                _rawFloatDrag2 = GetDouble(physicsImg, "floatDrag2", _rawFloatDrag2);
                _rawFloatCoefficient = GetDouble(physicsImg, "floatCoefficient", _rawFloatCoefficient);

                _rawSwimForce = GetDouble(physicsImg, "swimForce", _rawSwimForce);
                _rawSwimSpeed = GetDouble(physicsImg, "swimSpeed", _rawSwimSpeed);
                _rawSwimSpeedDec = GetDouble(physicsImg, "swimSpeedDec", _rawSwimSpeedDec);

                _rawFlyForce = GetDouble(physicsImg, "flyForce", _rawFlyForce);
                _rawFlySpeed = GetDouble(physicsImg, "flySpeed", _rawFlySpeed);
                _rawFlyJumpDec = GetDouble(physicsImg, "flyJumpDec", _rawFlyJumpDec);

                _rawGravityAcc = GetDouble(physicsImg, "gravityAcc", _rawGravityAcc);
                _rawFallSpeed = GetDouble(physicsImg, "fallSpeed", _rawFallSpeed);
                _rawJumpSpeed = GetDouble(physicsImg, "jumpSpeed", _rawJumpSpeed);

                _rawMaxFriction = GetDouble(physicsImg, "maxFriction", _rawMaxFriction);
                _rawMinFriction = GetDouble(physicsImg, "minFriction", _rawMinFriction);

                IsLoaded = true;

                System.Diagnostics.Debug.WriteLine($"[PhysicsConstants] Loaded from Physics.img:");
                System.Diagnostics.Debug.WriteLine($"  Raw: walkSpeed={_rawWalkSpeed}, walkForce={_rawWalkForce}, walkDrag={_rawWalkDrag}");
                System.Diagnostics.Debug.WriteLine($"  Raw: gravityAcc={_rawGravityAcc}, jumpSpeed={_rawJumpSpeed}, fallSpeed={_rawFallSpeed}");
                System.Diagnostics.Debug.WriteLine($"  CAttrShoe defaults: walkSpeed={DefaultShoeWalkSpeed}, walkAcc={DefaultShoeWalkAcc}, walkDrag={DefaultShoeWalkDrag}");
                System.Diagnostics.Debug.WriteLine($"  Formula: maxSpeed = (Speed/1000) * {WalkSpeed:F0} * footholdDrag, with Speed=100 → {WalkSpeed * 0.1:F1} px/s");
                System.Diagnostics.Debug.WriteLine($"  Physics: gravity={GravityAcc:F0} px/s², jump={JumpSpeed:F0} px/s, fall={FallSpeed:F0} px/s");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PhysicsConstants] Error loading: {ex.Message}");
            }
        }

        private double GetDouble(WzImage img, string name, double defaultValue)
        {
            var prop = img[name];
            if (prop == null) return defaultValue;

            if (prop is WzDoubleProperty doubleProp)
                return doubleProp.Value;
            if (prop is WzFloatProperty floatProp)
                return floatProp.Value;
            if (prop is WzIntProperty intProp)
                return intProp.Value;

            return defaultValue;
        }

        #endregion
    }
}
