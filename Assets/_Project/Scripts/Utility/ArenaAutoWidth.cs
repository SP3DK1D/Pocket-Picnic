using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Auto-fit arenaHalfWidth to current camera aspect (minus a margin).
    /// Handy if you switch between portrait/landscape or different devices.
    /// </summary>
    public class ArenaAutoWidth : MonoBehaviour
    {
        public GameConfig config;
        public float margin = 0.1f;

        private void Start()
        {
            var cam = Camera.main;
            if (!cam || !config) return;

            float halfWidth = cam.orthographicSize * cam.aspect;
            config.arenaHalfWidth = Mathf.Max(0.1f, halfWidth - margin);

            if (config.verboseLogs)
                Debug.Log($"[ArenaAutoWidth] arenaHalfWidth = {config.arenaHalfWidth:F2}");
        }
    }
}
/*
UNITY IMPLEMENTATION
1) Create empty "ArenaAutoWidth" in scene → add component → assign GameConfig_Default.
2) Press Play once to auto-tune arena width to current resolution/aspect.
*/
