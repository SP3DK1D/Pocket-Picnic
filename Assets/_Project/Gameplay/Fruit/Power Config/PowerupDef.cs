using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// ScriptableObject that defines a single power-up kind and its tunables.
    /// Create one asset per power-up (e.g., PU_Freeze, PU_Magnet, PU_Shield, PU_ScoreX2, PU_Clear).
    /// </summary>
    [CreateAssetMenu(menuName = "CatchTheFruit/Powerup")]
    public class PowerupDef : ScriptableObject
    {
        /// <summary>
        /// Kind of power-up. See comments on fields below for which tunables are used by which kind.
        /// </summary>
        public enum PowerupKind
        {
            /// <summary>Freeze (implemented by scaling Physics2D.gravity).</summary>
            TimeScale,

            /// <summary>Multiplies score events by <see cref="scoreMultiplier"/>.</summary>
            ScoreMultiplier,

            /// <summary>Pulls nearby non-bomb fruits toward the player (radius/speed below).</summary>
            Magnet,

            /// <summary>Blocks one bomb (or time-limited if <see cref="duration"/> &gt; 0).</summary>
            Shield,

            /// <summary>Clears all non-bomb fruits and awards their base score once. Duration ignored.</summary>
            ClearScreen
        }

        // ------------------------------ Type ------------------------------

        [Header("Type")]
        [Tooltip("Which power-up this asset represents.")]
        public PowerupKind kind = PowerupKind.TimeScale;

        // ----------------------------- Shared -----------------------------

        [Header("Shared")]
        [Tooltip("Duration in seconds. Shield: 0 means 'until consumed'. Clear: ignored.")]
        [Min(0f)] public float duration = 5f;

        // ----------------------- Score Multiplier -------------------------

        [Header("Score Multiplier")]
        [Tooltip("x2, x3, ... Used only by ScoreMultiplier.")]
        [Min(1f)] public float scoreMultiplier = 2f;

        // ----------------------------- Freeze -----------------------------

        [Header("Freeze (TimeScale)")]
        [Tooltip("Gravity multiplier during Freeze (0.2 = ~20% fall speed). Used only by Freeze.")]
        [Range(0f, 1f)] public float freezeGravityScale = 0.20f;

        // ----------------------------- Magnet -----------------------------

        [Header("Magnet")]
        [Tooltip("World radius within which fruits start pulling. Used only by Magnet.")]
        [Min(0f)] public float magnetRadius = 5.5f;

        [Tooltip("Pull speed (units/sec). Used only by Magnet.")]
        [Min(0f)] public float magnetPullSpeed = 12f;

#if UNITY_EDITOR
        // Editor-only guard rails to keep assets sensible in the Inspector.
        void OnValidate()
        {
            // Freeze should not disable gravity entirely (0 can be useful for extreme slow, but prevent negatives).
            freezeGravityScale = Mathf.Clamp01(freezeGravityScale);

            // Magnet fields should be non-negative.
            magnetRadius = Mathf.Max(0f, magnetRadius);
            magnetPullSpeed = Mathf.Max(0f, magnetPullSpeed);

            // Multiplier should be >= 1.
            scoreMultiplier = Mathf.Max(1f, scoreMultiplier);

            // Duration can be 0 for infinite shield; otherwise keep non-negative.
            duration = Mathf.Max(0f, duration);
        }
#endif
    }
}
