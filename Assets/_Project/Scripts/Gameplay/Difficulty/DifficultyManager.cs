using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Holds current difficulty settings, presets, and progressive scaling.
    /// All systems read DifficultyManager.Current for spawn/motion params.
    /// </summary>
    public sealed class DifficultySettings
    {
        // Spawning
        public float initialInterval;     // seconds
        public float minInterval;         // seconds
        public float intervalDecay;       // multiply current interval each spawn

        // Motion
        public float fallSpeedMultiplier; // multiplies SpawnTable.fallSpeedMultiplier
        public float gravityScale;        // overrides Fruit gravity
        public float maxFallSpeed;        // terminal velocity (world u/s)
        public float initialDownBoost;    // downward kick at spawn

        public DifficultySettings(
            float initialInterval, float minInterval, float intervalDecay,
            float fallSpeedMultiplier, float gravityScale,
            float maxFallSpeed, float initialDownBoost)
        {
            this.initialInterval = initialInterval;
            this.minInterval = minInterval;
            this.intervalDecay = intervalDecay;
            this.fallSpeedMultiplier = fallSpeedMultiplier;
            this.gravityScale = gravityScale;
            this.maxFallSpeed = maxFallSpeed;
            this.initialDownBoost = initialDownBoost;
        }
    }

    public static class DifficultyManager
    {
        public static DifficultySettings Current { get; private set; }
        public static bool HasCurrent => Current != null;

        static float _startTime;

        // --- Progressive ramp parameters ---
        // How many minutes until we’re fully ramped (scaled difficulty).
        const float MINUTES_TO_MAX = 1.5f; 

        // Cap multipliers so game doesn’t become impossible.
        const float MAX_SPAWN_RATE_MUL = 1.8f;
        const float MAX_FALL_SPEED_MUL = 1.5f;

        // === Apply / Clear ===
        public static void Apply(DifficultySettings settings)
        {
            Current = settings;
            _startTime = Time.time;
        }

        public static void ClearCurrent()
        {
            Current = null;
        }

        public static float ElapsedMinutes => (Time.time - _startTime) / 60f;

        // === Progressive scaling helpers ===
        static float Progress01 => Mathf.Clamp01(ElapsedMinutes / MINUTES_TO_MAX);

        /// <summary>
        /// Returns spawn interval multiplier that shrinks as time passes.
        /// </summary>
        public static float SpawnRateRamp()
        {
            // At t=0 → 1.0. At full ramp → up to MAX_SPAWN_RATE_MUL faster.
            float mul = Mathf.Lerp(1f, MAX_SPAWN_RATE_MUL, Progress01);
            return mul;
        }

        /// <summary>
        /// Returns fall speed multiplier that rises over time.
        /// </summary>
        public static float FallSpeedRamp()
        {
            float mul = Mathf.Lerp(1f, MAX_FALL_SPEED_MUL, Progress01);
            return mul;
        }

        // === Presets ===
        public static void PickEasy()
        {
            Apply(new DifficultySettings(
                initialInterval: 1.00f,
                minInterval: 0.42f,
                intervalDecay: 0.982f,
                fallSpeedMultiplier: 1.05f,
                gravityScale: 1.7f,
                maxFallSpeed: 11.0f,
                initialDownBoost: 1.4f
            ));
        }

        public static void PickMedium()
        {
            Apply(new DifficultySettings(
                initialInterval: 0.85f,
                minInterval: 0.30f,
                intervalDecay: 0.978f,
                fallSpeedMultiplier: 1.20f,
                gravityScale: 1.9f,
                maxFallSpeed: 12.5f,
                initialDownBoost: 1.7f
            ));
        }

        public static void PickHard()
        {
            Apply(new DifficultySettings(
                initialInterval: 0.70f,
                minInterval: 0.22f,
                intervalDecay: 0.975f,
                fallSpeedMultiplier: 1.35f,
                gravityScale: 2.1f,
                maxFallSpeed: 14.0f,
                initialDownBoost: 2.2f
            ));
        }
    }
}
