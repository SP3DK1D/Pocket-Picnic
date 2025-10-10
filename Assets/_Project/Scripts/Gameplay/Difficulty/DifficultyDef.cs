using UnityEngine;

namespace CatchTheFruit
{
    [CreateAssetMenu(fileName = "DifficultyDef", menuName = "CatchTheFruit/Difficulty")]
    public class DifficultyDef : ScriptableObject
    {
        [Header("Display")]
        public string displayName = "Easy";

        // ===================== FALL SPEED CONTROL =====================
        [Header("Fall Speed Settings")]
        [Tooltip("Base fall speed at the start of the game (world units per second).")]
        [Min(0f)] public float baseFallSpeed = 6f;

        [Tooltip("How much the fall speed multiplies every step (e.g. 1.1 = +10% per step).")]
        [Range(1f, 2f)] public float fallSpeedMultiplier = 1.1f;

        [Tooltip("Time (seconds) between each speed increase step.")]
        [Range(5f, 30f)] public float rampIntervalSeconds = 15f;

        [Tooltip("Maximum fall speed allowed (world units per second).")]
        [Min(1f)] public float maxFallSpeed = 14f;

        // ===================== OTHER OPTIONAL PHYSICS =====================
        [Header("Extra Physics Tweaks (optional)")]
        [Tooltip("Extra gravity scale applied to fruits.")]
        [Min(0f)] public float gravityScale = 1.8f;

        [Tooltip("Extra downward force added when spawned (visual kick).")]
        [Min(0f)] public float initialDownBoost = 0f;

        [Tooltip("Multiplier for fruit rotation/tumble speed (1.4 = +40%).")]
        [Min(0f)] public float tumbleMultiplier = 1.4f;

        // ===================== BOMB RAMP (optional) =====================
        [Header("Bomb Ramp (selection weights only)")]
        [Tooltip("Base multiplier applied to bomb weights in SpawnTable.")]
        [Min(0f)] public float bombWeightMultiplier = 1.0f;

        [Tooltip("Extra bomb weight added over time (seconds). y = added multiplier.")]
        public AnimationCurve bombRampOverTime = AnimationCurve.Linear(0f, 0f, 60f, 0f);

        [Tooltip("Clamp for total multiplier (base + ramp). 0 = no clamp.")]
        [Min(0f)] public float bombWeightMaxClamp = 5f;

        // ===================== ACCESSORS =====================
        public float GetBombWeightScale(float elapsedSeconds)
        {
            float ramp = bombRampOverTime != null ? bombRampOverTime.Evaluate(elapsedSeconds) : 0f;
            float total = Mathf.Max(0f, bombWeightMultiplier + ramp);
            if (bombWeightMaxClamp > 0f)
                total = Mathf.Min(total, bombWeightMaxClamp);
            return total;
        }
    }
}
