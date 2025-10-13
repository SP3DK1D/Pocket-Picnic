// Assets/_Project/Scripts/UI/ArenaAutoWidth.cs
using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Sets GameConfig.arenaHalfWidth from the current main camera aspect/size,
    /// then disables itself so it runs exactly once per app boot.
    ///
    /// Details:
    /// - Uses Camera.main.orthographicSize * aspect to compute half width.
    /// - Applies a small inward margin so fruit never spawn clipped at edges.
    /// - Idempotent: if multiple copies of this component exist across scenes,
    ///   only the FIRST one encountered after app launch will execute.
    /// - Provides Refresh() if you ever need to re-apply by code.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class ArenaAutoWidth : MonoBehaviour
    {
        [Tooltip("Game config to update.")]
        public GameConfig config;

        [Tooltip("Inward margin (world units) subtracted from camera half-width.")]
        public float margin = 0.10f;

        // Global guard so we only ever run once per boot (across all scenes).
        private static bool s_appliedThisBoot;

        private void Start()
        {
            // If we've already applied in this app session, disable and bail.
            if (s_appliedThisBoot)
            {
                enabled = false;
                return;
            }

            if (!ApplyInternal())
            {
#if UNITY_EDITOR
                Debug.LogWarning("[ArenaAutoWidth] Skipped: missing GameConfig or orthographic MainCamera.");
#endif
                // Still disable to honor "run once per boot" contract.
                enabled = false;
                return;
            }

            s_appliedThisBoot = true;
            enabled = false; // never run Update; job is done
        }

        /// <summary>
        /// Manually recompute and apply once (does not change the global guard).
        /// Useful if you change camera size at runtime and knowingly want to apply again.
        /// </summary>
        public bool Refresh() => ApplyInternal();

        // ---- Implementation ----
        bool ApplyInternal()
        {
            if (!config) return false;

            var cam = Camera.main;
            if (!cam || !cam.orthographic) return false;

            // Compute camera half-width in world units, then subtract margin
            float halfWidth = cam.orthographicSize * cam.aspect;
            halfWidth = Mathf.Max(0.1f, halfWidth - Mathf.Abs(margin));

            config.arenaHalfWidth = halfWidth;

            if (config.verboseLogs)
                Debug.Log($"[ArenaAutoWidth] arenaHalfWidth = {config.arenaHalfWidth:F2}  (margin {margin:0.##})", this);

            return true;
        }
    }
}
