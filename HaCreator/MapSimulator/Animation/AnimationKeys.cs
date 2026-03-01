namespace HaCreator.MapSimulator.Animation
{
    /// <summary>
    /// Standard animation action keys used throughout the MapSimulator.
    /// Using constants instead of magic strings improves maintainability and prevents typos.
    /// </summary>
    public static class AnimationKeys
    {
        #region Common Actions

        /// <summary>
        /// Idle/standing animation
        /// </summary>
        public const string Stand = "stand";

        /// <summary>
        /// Walking/moving animation
        /// </summary>
        public const string Move = "move";

        /// <summary>
        /// Alternative walking animation
        /// </summary>
        public const string Walk = "walk";

        /// <summary>
        /// Flying animation (for airborne mobs)
        /// </summary>
        public const string Fly = "fly";

        /// <summary>
        /// Jump animation
        /// </summary>
        public const string Jump = "jump";

        #endregion

        #region Combat Actions

        /// <summary>
        /// Primary attack animation
        /// </summary>
        public const string Attack1 = "attack1";

        /// <summary>
        /// Secondary attack animation
        /// </summary>
        public const string Attack2 = "attack2";

        /// <summary>
        /// Tertiary attack animation
        /// </summary>
        public const string Attack3 = "attack3";

        /// <summary>
        /// Primary hit/damage taken animation
        /// </summary>
        public const string Hit1 = "hit1";

        /// <summary>
        /// Secondary hit animation
        /// </summary>
        public const string Hit2 = "hit2";

        /// <summary>
        /// Generic hit animation
        /// </summary>
        public const string Hit = "hit";

        /// <summary>
        /// Primary death animation
        /// </summary>
        public const string Die1 = "die1";

        /// <summary>
        /// Secondary death animation
        /// </summary>
        public const string Die2 = "die2";

        /// <summary>
        /// Generic die animation
        /// </summary>
        public const string Die = "die";

        #endregion

        #region NPC Actions

        /// <summary>
        /// Speaking animation
        /// </summary>
        public const string Speak = "speak";

        /// <summary>
        /// Blinking animation
        /// </summary>
        public const string Blink = "blink";

        /// <summary>
        /// Hair movement animation
        /// </summary>
        public const string Hair = "hair";

        /// <summary>
        /// Angry expression
        /// </summary>
        public const string Angry = "angry";

        /// <summary>
        /// Winking animation
        /// </summary>
        public const string Wink = "wink";

        #endregion

        #region Character Actions

        /// <summary>
        /// Standing pose 1
        /// </summary>
        public const string Stand1 = "stand1";

        /// <summary>
        /// Standing pose 2
        /// </summary>
        public const string Stand2 = "stand2";

        /// <summary>
        /// Walking pose 1
        /// </summary>
        public const string Walk1 = "walk1";

        /// <summary>
        /// Walking pose 2
        /// </summary>
        public const string Walk2 = "walk2";

        /// <summary>
        /// Sitting animation
        /// </summary>
        public const string Sit = "sit";

        /// <summary>
        /// Prone/lying down animation
        /// </summary>
        public const string Prone = "prone";

        /// <summary>
        /// Ladder climbing animation
        /// </summary>
        public const string Ladder = "ladder";

        /// <summary>
        /// Rope climbing animation
        /// </summary>
        public const string Rope = "rope";

        /// <summary>
        /// Alert/combat stance
        /// </summary>
        public const string Alert = "alert";

        /// <summary>
        /// Healing animation
        /// </summary>
        public const string Heal = "heal";

        /// <summary>
        /// Swing attack (melee weapons)
        /// </summary>
        public const string SwingO1 = "swingO1";

        /// <summary>
        /// Swing attack variant 2
        /// </summary>
        public const string SwingO2 = "swingO2";

        /// <summary>
        /// Swing attack variant 3
        /// </summary>
        public const string SwingO3 = "swingO3";

        /// <summary>
        /// Stab attack (piercing weapons)
        /// </summary>
        public const string StabO1 = "stabO1";

        /// <summary>
        /// Stab attack variant 2
        /// </summary>
        public const string StabO2 = "stabO2";

        /// <summary>
        /// Shoot animation (ranged weapons)
        /// </summary>
        public const string Shoot1 = "shoot1";

        /// <summary>
        /// Shoot animation variant 2
        /// </summary>
        public const string Shoot2 = "shoot2";

        /// <summary>
        /// Prone stab (ground attack)
        /// </summary>
        public const string ProneStab = "proneStab";

        #endregion

        #region Facial Expressions

        /// <summary>
        /// Default facial expression
        /// </summary>
        public const string Default = "default";

        /// <summary>
        /// Smile expression
        /// </summary>
        public const string Smile = "smile";

        /// <summary>
        /// Troubled expression
        /// </summary>
        public const string Troubled = "troubled";

        /// <summary>
        /// Crying expression
        /// </summary>
        public const string Cry = "cry";

        /// <summary>
        /// Bewildered expression
        /// </summary>
        public const string Bewildered = "bewildered";

        /// <summary>
        /// Stunned expression
        /// </summary>
        public const string Stunned = "stunned";

        /// <summary>
        /// Oops expression
        /// </summary>
        public const string Oops = "oops";

        #endregion

        #region Skill Actions

        /// <summary>
        /// Effect animation
        /// </summary>
        public const string Effect = "effect";

        /// <summary>
        /// Ball/projectile animation
        /// </summary>
        public const string Ball = "ball";

        #endregion

        #region Helper Methods

        /// <summary>
        /// Get attack action by index (1-based)
        /// </summary>
        public static string GetAttackAction(int index) => index switch
        {
            1 => Attack1,
            2 => Attack2,
            3 => Attack3,
            _ => Attack1
        };

        /// <summary>
        /// Get hit action by index (1-based)
        /// </summary>
        public static string GetHitAction(int index) => index switch
        {
            1 => Hit1,
            2 => Hit2,
            _ => Hit1
        };

        /// <summary>
        /// Get die action by index (1-based)
        /// </summary>
        public static string GetDieAction(int index) => index switch
        {
            1 => Die1,
            2 => Die2,
            _ => Die1
        };

        /// <summary>
        /// Check if action is a movement action
        /// </summary>
        public static bool IsMovementAction(string action)
        {
            return action == Move || action == Walk || action == Fly ||
                   action == Walk1 || action == Walk2;
        }

        /// <summary>
        /// Check if action is a combat action
        /// </summary>
        public static bool IsCombatAction(string action)
        {
            return action != null && (
                action.StartsWith("attack") ||
                action.StartsWith("hit") ||
                action.StartsWith("die") ||
                action.StartsWith("swing") ||
                action.StartsWith("stab") ||
                action.StartsWith("shoot"));
        }

        #endregion
    }
}
