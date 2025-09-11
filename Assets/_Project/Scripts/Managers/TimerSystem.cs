using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Counts UP from 0 while running. Uses unscaled time (not affected by Freeze).
    /// Starts automatically on enable (can be disabled in Inspector).
    /// </summary>
    public class TimerSystem : MonoBehaviour
    {
        [Tooltip("If true, starts counting the moment this object is enabled.")]
        public bool autoStartOnEnable = true;

        float _elapsed;
        bool _running;

        void OnEnable()
        {
            GameEvents.OnGameStart += StartTimer;
            GameEvents.OnGameOver += StopTimer;

            if (autoStartOnEnable) StartTimer();
        }
        void OnDisable()
        {
            GameEvents.OnGameStart -= StartTimer;
            GameEvents.OnGameOver -= StopTimer;
        }

        public void StartTimer()
        {
            _elapsed = 0f;
            _running = true;
            GameEvents.RaiseTimerTick(_elapsed); // push 0 immediately
        }

        public void StopTimer()
        {
            _running = false;
        }

        void Update()
        {
            if (!_running) return;
            _elapsed += Time.unscaledDeltaTime;
            GameEvents.RaiseTimerTick(_elapsed);
        }
    }
}
/*
Unity:
- Create Empty "TimerSystem" → add this component.
- Leave "Auto Start On Enable" ON to see the timer immediately.
- If you prefer it to start only when your Start button is pressed,
  turn this OFF; your existing GameStart event will start it.
*/
