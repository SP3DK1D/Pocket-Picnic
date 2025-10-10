using UnityEngine;

namespace CatchTheFruit
{
    public static class DifficultyManager
    {
        public static DifficultyDef Current { get; private set; }
        public static bool HasCurrent => Current != null;

        static float _startTime;

        public static void ApplyFromDef(DifficultyDef def)
        {
            Current = def;
            _startTime = Time.time;
        }

        public static void ClearCurrent()
        {
            Current = null;
        }

        public static float ElapsedSeconds => Time.time - _startTime;

        // ----------- Main fall speed progression -----------
        public static float CurrentFallSpeed()
        {
            if (!Current) return 6f;

            int steps = Mathf.FloorToInt(ElapsedSeconds / Mathf.Max(1f, Current.rampIntervalSeconds));
            float speed = Current.baseFallSpeed * Mathf.Pow(Current.fallSpeedMultiplier, steps);
            return Mathf.Min(speed, Current.maxFallSpeed);
        }

        // ----------- Optional helpers for other systems -----------
        public static float GravityScale => Current ? Current.gravityScale : 1.8f;
        public static float TumbleMultiplier => Current ? Current.tumbleMultiplier : 1.4f;
        public static float BombWeightScale => Current ? Current.GetBombWeightScale(ElapsedSeconds) : 1f;
    }
}
