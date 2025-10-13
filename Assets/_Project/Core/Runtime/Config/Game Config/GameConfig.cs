using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Global knobs for gameplay/balance & debug.
    /// Create one or more instances as ScriptableObjects and assign in the scene.
    /// </summary>
    [CreateAssetMenu(menuName = "CatchTheFruit/Game Config")]
    public class GameConfig : ScriptableObject
    {
        // ------------------------------ Core ------------------------------

        [Header("Core")]
        [Tooltip("How many lives the player starts with when a run begins.")]
        [Min(1)] public int startingLives = 3;

        [Tooltip("Half the playable width in world units.\nFruits spawn & player clamps within [-arenaHalfWidth, +arenaHalfWidth].")]
        [Range(1f, 50f)] public float arenaHalfWidth = 2.7f;

        [Tooltip("World Y where fruits appear/spawn.")]
        public float spawnY = 6f;

        [Tooltip("World Y treated as 'missed' (i.e., the ground/fail line). Typically below the bottom of the camera view.")]
        public float groundY = -4.5f;

        // ----------------------------- Player -----------------------------

        [Header("Player")]
        [Tooltip("Horizontal tracking speed toward finger/mouse X (used by PlayerMover Smooth mode).")]
        [Range(1f, 30f)] public float playerMoveSpeed = 10f;

        // ------------------------- Session Timer --------------------------

        [Header("Session Timer (optional)")]
        [Tooltip("If ON, a lightweight timer runs during gameplay for display/analytics.")]
        public bool useSessionTimer = false;

        [Tooltip("Duration (seconds) for the session timer if used.")]
        [Range(5f, 600f)] public float sessionDuration = 60f;

        // ----------------------------- Debug ------------------------------

        [Header("Debug")]
        [Tooltip("If ON, prints helpful state transitions and tuning info to the Console.")]
        public bool verboseLogs = true;

#if UNITY_EDITOR
        // Editor-only sanity checks to help catch misconfigurations in the Inspector.
        void OnValidate()
        {
            // Basic non-negative guards are handled by attributes; we keep extra relational checks here.

            // Warn (do not auto-fix) if the ground is above or equal to spawn height.
            if (groundY >= spawnY)
            {
                Debug.LogWarning($"[GameConfig] groundY ({groundY}) is >= spawnY ({spawnY}). " +
                                 $"Fruits may immediately count as missed. Place groundY below spawnY.", this);
            }

            // If someone drags arenaHalfWidth too tiny via script, keep it sensible at runtime.
            arenaHalfWidth = Mathf.Clamp(arenaHalfWidth, 0.1f, 100f);

            // Keep session duration positive even if attributes are bypassed via code.
            sessionDuration = Mathf.Max(1f, sessionDuration);

            // Player speed protection if modified via script.
            playerMoveSpeed = Mathf.Max(0f, playerMoveSpeed);
        }
#endif
    }
}
