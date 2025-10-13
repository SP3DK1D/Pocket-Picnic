using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Runtime access to the active DifficultyDef.
    /// Provides derived values used by gameplay systems.
    /// - Call ApplyFromDef(def) when a difficulty is selected (e.g., from menu).
    /// - Other systems read the static properties/methods here.
    /// </summary>
    public static class DifficultyManager
    {
        /// <summary>The currently applied difficulty asset, or null for safe defaults.</summary>
        public static DifficultyDef Current { get; private set; }

        /// <summary>True if a difficulty was applied for the current run.</summary>
        public static bool HasCurrent => Current != null;

        // When the def was applied; used to compute elapsed for ramps.
        static float _startTime;

        /// <summary>Apply a difficulty asset for the next/active run.</summary>
        public static void ApplyFromDef(DifficultyDef def)
        {
            Current = def;                // may be null → safe defaults will be used
            _startTime = Time.time;       // note: Time.time is scaled time (that’s fine for long ramping)
        }

        /// <summary>Clear current difficulty (e.g., when exiting a run to menu).</summary>
        public static void ClearCurrent() => Current = null;

        /// <summary>Seconds since ApplyFromDef was last called.</summary>
        public static float ElapsedSeconds => Time.time - _startTime;

        // ------------------------ Derived values ------------------------

        /// <summary>
        /// Absolute fruit fall speed (world units/sec) given the current ramp.
        /// Clamped by the difficulty's maxFallSpeed. Falls back to safe defaults if no Current.
        /// </summary>
        public static float CurrentFallSpeed()
        {
            if (!Current)
                return 6f; // safe default used elsewhere in the project

            // Number of completed steps
            int steps = Mathf.FloorToInt(ElapsedSeconds / Mathf.Max(1f, Current.rampIntervalSeconds));

            // Base * multiplier^steps, capped
            float speed = Current.baseFallSpeed * Mathf.Pow(Current.fallSpeedMultiplier, steps);
            return Mathf.Min(speed, Current.maxFallSpeed);
        }

        /// <summary>Preferred gravity scale for this difficulty (used by systems that care).</summary>
        public static float GravityScale => Current ? Current.gravityScale : 1.8f;

        /// <summary>Rotation/tumble multiplier hint for fruit (used by Fruit.cs).</summary>
        public static float TumbleMultiplier => Current ? Current.tumbleMultiplier : 1.4f;

        /// <summary>Total bomb weight scale (base + ramp, clamped) for SpawnTable weighting.</summary>
        public static float BombWeightScale => Current ? Current.GetBombWeightScale(ElapsedSeconds) : 1f;
    }
}
