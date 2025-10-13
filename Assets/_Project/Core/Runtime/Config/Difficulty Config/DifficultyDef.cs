using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Tunables for a difficulty preset (create one asset per difficulty).
    /// Kept intentionally simple—other systems read via DifficultyManager.
    /// </summary>
    [CreateAssetMenu(fileName = "DifficultyDef", menuName = "CatchTheFruit/Difficulty")]
    public class DifficultyDef : ScriptableObject
    {
        // ---------------------- Display ----------------------

        [Header("Display")]
        [Tooltip("Shown on UI when this difficulty is selected.")]
        public string displayName = "Easy";

        // ------------------ Fall Speed Control ----------------

        [Header("Fall Speed Settings")]
        [Tooltip("Base fruit fall speed at t=0 (world units per second).")]
        [Min(0f)] public float baseFallSpeed = 6f;

        [Tooltip("Speed multiplier applied per step (1.10 = +10% each step).")]
        [Range(1f, 2f)] public float fallSpeedMultiplier = 1.10f;

        [Tooltip("Seconds between each speed step. Larger = slower ramp.")]
        [Range(5f, 60f)] public float rampIntervalSeconds = 15f;

        [Tooltip("Absolute ceiling on fall speed (world units per second).")]
        [Min(1f)] public float maxFallSpeed = 14f;

        // --------------- Other Optional Physics ---------------

        [Header("Extra Physics Tweaks (optional)")]
        [Tooltip("Extra gravity scale the game will favor for fruits at this difficulty.")]
        [Min(0f)] public float gravityScale = 1.8f;

        [Tooltip("Extra downward boost on spawn (visual kick only).")]
        [Min(0f)] public float initialDownBoost = 0f;

        [Tooltip("Multiplier for fruit tumble/rotation speed (1.4 = +40%).")]
        [Min(0f)] public float tumbleMultiplier = 1.4f;

        // ----------------- Bomb Weight Ramp -------------------

        [Header("Bomb Ramp (selection weights only)")]
        [Tooltip("Base multiplier applied to bomb weights in the SpawnTable (1 = unchanged).")]
        [Min(0f)] public float bombWeightMultiplier = 1.0f;

        [Tooltip("Added multiplier over elapsed time (seconds). Value is added to base.")]
        public AnimationCurve bombRampOverTime = AnimationCurve.Linear(0f, 0f, 60f, 0f);

        [Tooltip("Clamp for total (base + ramp). 0 = no clamp.")]
        [Min(0f)] public float bombWeightMaxClamp = 5f;

        /// <summary>
        /// Returns bomb weight scale at a given elapsed time.
        /// </summary>
        public float GetBombWeightScale(float elapsedSeconds)
        {
            float ramp = bombRampOverTime != null ? bombRampOverTime.Evaluate(Mathf.Max(0f, elapsedSeconds)) : 0f;
            float total = Mathf.Max(0f, bombWeightMultiplier + ramp);
            if (bombWeightMaxClamp > 0f) total = Mathf.Min(total, bombWeightMaxClamp);
            return total;
        }

#if UNITY_EDITOR
        // Inspector safety: nudge any out-of-range values back into sane territory
        void OnValidate()
        {
            // Ensure a curve exists
            if (bombRampOverTime == null)
                bombRampOverTime = AnimationCurve.Linear(0f, 0f, 60f, 0f);

            // Keep step interval and multipliers sensible
            rampIntervalSeconds = Mathf.Clamp(rampIntervalSeconds, 1f, 600f);
            fallSpeedMultiplier = Mathf.Clamp(fallSpeedMultiplier, 1f, 3f);
            baseFallSpeed = Mathf.Max(0f, baseFallSpeed);
            maxFallSpeed = Mathf.Max(1f, maxFallSpeed);

            // Ensure base <= max to avoid confusing configs
            if (baseFallSpeed > maxFallSpeed) maxFallSpeed = baseFallSpeed;

            // Tumble & gravity guards
            gravityScale = Mathf.Max(0f, gravityScale);
            tumbleMultiplier = Mathf.Max(0f, tumbleMultiplier);

            // Bomb ramp limits
            bombWeightMultiplier = Mathf.Max(0f, bombWeightMultiplier);
            bombWeightMaxClamp = Mathf.Max(0f, bombWeightMaxClamp);
        }
#endif
    }
}
