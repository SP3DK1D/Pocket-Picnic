using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Central pause control. Freezes gameplay by setting Time.timeScale = 0
    /// and restores the exact scale that was active before pause (works with Freeze).
    /// </summary>
    public class PauseManager : MonoBehaviour
    {
        public static PauseManager Instance { get; private set; }

        public bool IsPaused { get; private set; }

        float _prePauseTimeScale = 1f;
        float _prePauseFixedDelta = 0.02f;

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDisable()
        {
            // Safety: if this object is disabled while paused, resume so timeScale isn't stuck at 0
            if (IsPaused) Resume();
            if (Instance == this) Instance = null;
        }

        public void Toggle()
        {
            if (IsPaused) Resume();
            else Pause();
        }

        public void Pause()
        {
            if (IsPaused) return;

            _prePauseTimeScale = Time.timeScale;
            _prePauseFixedDelta = Time.fixedDeltaTime;

            Time.timeScale = 0f;                     // freeze gameplay
            Time.fixedDeltaTime = _prePauseFixedDelta; // keep step size stored for resume
            IsPaused = true;
            Debug.Log("[Pause] Paused");
        }

        public void Resume()
        {
            if (!IsPaused) return;

            Time.timeScale = _prePauseTimeScale;     // restore whatever was active (e.g., Freeze 0.2)
            Time.fixedDeltaTime = _prePauseFixedDelta;
            IsPaused = false;
            Debug.Log("[Pause] Resumed");
        }
    }
}
