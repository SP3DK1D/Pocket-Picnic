using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Lightweight session timer that **counts up using unscaled time**.
    /// Unscaled time means powerups that change gameplay speed (like Freeze)
    /// and Pause (timeScale=0) won't affect this timer's cadence.
    ///
    /// Emits GameEvents.OnTimerTick(elapsedSeconds) every frame while running.
    /// </summary>
    public class TimerSystem : MonoBehaviour
    {
        [Tooltip("If true, starts counting the moment this object is enabled.")]
        [SerializeField] private bool autoStartOnEnable = true;

        /// <summary>Current elapsed time in seconds (unscaled).</summary>
        public float ElapsedSeconds { get; private set; }

        /// <summary>Is the timer currently advancing?</summary>
        public bool IsRunning { get; private set; }

        private void OnEnable()
        {
            GameEvents.OnGameStart += StartTimer;
            GameEvents.OnGameOver += StopTimer;

            if (autoStartOnEnable) StartTimer();
            else
            {
                // still push a 0 so any HUD labels can initialize
                GameEvents.RaiseTimerTick(ElapsedSeconds);
            }
        }

        private void OnDisable()
        {
            GameEvents.OnGameStart -= StartTimer;
            GameEvents.OnGameOver -= StopTimer;
        }

        /// <summary>Resets to 0 and begins advancing (unscaled).</summary>
        public void StartTimer()
        {
            ElapsedSeconds = 0f;
            IsRunning = true;
            GameEvents.RaiseTimerTick(ElapsedSeconds);
        }

        /// <summary>Stops advancing (value is retained).</summary>
        public void StopTimer()
        {
            IsRunning = false;
        }

        /// <summary>Sets elapsed to 0 but does not change running state.</summary>
        public void ResetTimer()
        {
            ElapsedSeconds = 0f;
            GameEvents.RaiseTimerTick(ElapsedSeconds);
        }

        private void Update()
        {
            if (!IsRunning) return;

            // Unscaled delta keeps the timer consistent during Freeze/timeScale changes.
            ElapsedSeconds += Time.unscaledDeltaTime;
            GameEvents.RaiseTimerTick(ElapsedSeconds);
        }
    }
}
