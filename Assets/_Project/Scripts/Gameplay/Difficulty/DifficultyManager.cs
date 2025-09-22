using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Static difficulty holder + light scaling helpers.
    /// Other systems read DifficultyManager.Current (if HasCurrent == true).
    /// </summary>
    public sealed class DifficultySettings
    {
        // Spawning
        public float initialInterval;     // seconds
        public float minInterval;         // seconds
        public float intervalDecay;       // multiply current interval each spawn

        // Motion
        public float fallSpeedMultiplier; // multiplies SpawnTable.fallSpeedMultiplier
        public float gravityScale;        // overrides Fruit.rb.gravityScale if > 0
        public float maxFallSpeed;        // Fruit terminal velocity (world u/s)
        public float initialDownBoost;    // initial downward velocity at spawn

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

    public static class DifficultyPresets
    {
        // Tune to taste
        public static DifficultySettings Easy() => new DifficultySettings(
            initialInterval: 1.00f, minInterval: 0.32f, intervalDecay: 0.985f,
            fallSpeedMultiplier: 1.00f, gravityScale: 1.6f,
            maxFallSpeed: 10.5f, initialDownBoost: 1.2f);

        public static DifficultySettings Medium() => new DifficultySettings(
            initialInterval: 0.90f, minInterval: 0.26f, intervalDecay: 0.980f,
            fallSpeedMultiplier: 1.10f, gravityScale: 1.8f,
            maxFallSpeed: 12.0f, initialDownBoost: 1.6f);

        public static DifficultySettings Hard() => new DifficultySettings(
            initialInterval: 0.80f, minInterval: 0.20f, intervalDecay: 0.975f,
            fallSpeedMultiplier: 1.20f, gravityScale: 2.0f,
            maxFallSpeed: 13.5f, initialDownBoost: 2.0f);
    }

    public static class DifficultyManager
    {
        public static DifficultySettings Current { get; private set; }
        public static bool HasCurrent => Current != null;

        static float _startTime;

        public static void Apply(DifficultySettings settings)
        {
            Current = settings;
            _startTime = Time.time;
        }

        public static void ClearCurrent()
        {
            Current = null;
        }

        // Optional dynamic scalers other systems can use
        public static float ElapsedMinutes => (Time.time - _startTime) / 60f;

        /// <summary>Simple time-based ramp (1.0 at t=0, climbs slowly).</summary>
        public static float FallSpeedRamp(float perMinute = 0.08f, float maxMul = 1.6f)
        {
            if (!HasCurrent) return 1f;
            float mul = 1f + ElapsedMinutes * perMinute;
            return Mathf.Min(mul, maxMul);
        }

        // --- Helpers to match legacy UI calls (prevents CS0103) ---
        public static void PickEasy() => Apply(DifficultyPresets.Easy());
        public static void PickMedium() => Apply(DifficultyPresets.Medium());
        public static void PickHard() => Apply(DifficultyPresets.Hard());
    }
}
