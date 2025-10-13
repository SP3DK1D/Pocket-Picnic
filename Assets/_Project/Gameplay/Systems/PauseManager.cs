// Assets/_Project/Scripts/Systems/PauseManager.cs
using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Controls paused state & timeScale. Plays nice with Freeze/timeScale powerups:
    /// - Pause(): saves current timeScale and sets 0
    /// - Resume(): restores saved timeScale
    /// - ResumeForce(): hard sets timeScale=1 and clears paused flag (safety)
    /// </summary>
    public class PauseManager : MonoBehaviour
    {
        public static PauseManager Instance { get; private set; }
        public bool IsPaused { get; private set; }

        float _prePauseTimeScale = 1f;

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void Pause()
        {
            if (IsPaused) return;
            _prePauseTimeScale = Time.timeScale <= 0f ? 1f : Time.timeScale;
            Time.timeScale = 0f;
            IsPaused = true;
        }

        public void Resume()
        {
            if (!IsPaused) return;
            Time.timeScale = Mathf.Approximately(_prePauseTimeScale, 0f) ? 1f : _prePauseTimeScale;
            IsPaused = false;
        }

        /// <summary>Hard reset used on scene boot / start / restart.</summary>
        public void ResumeForce()
        {
            Time.timeScale = 1f;
            _prePauseTimeScale = 1f;
            IsPaused = false;
        }

        void OnDisable()
        {
            // Safety: never leave the app frozen if this is disabled/reloaded.
            if (IsPaused) ResumeForce();
        }
    }
}
