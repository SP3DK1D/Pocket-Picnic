using System.Collections;
using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Emits periodic "Speeding up!" waves and ramps the spawner speed over time.
    /// Shows the banner via GameEvents.OnWaveMessage and compounds a speed multiplier.
    /// </summary>
    public class WaveDirector : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private FruitSpawner spawner;

        [Header("Wave pacing")]
        [Tooltip("Seconds between waves (when we show the banner and bump speed).")]
        [Min(1f)] public float waveEverySeconds = 20f;

        [Tooltip("How long the banner stays visible.")]
        [Min(0.2f)] public float bannerSeconds = 1.4f;

        [Header("Speed ramp")]
        [Tooltip("Each wave multiplies the current spawner speed override by this amount.")]
        [Min(1.0f)] public float waveSpeedStep = 1.15f;

        [Tooltip("Clamp the total speed override to avoid becoming impossible.")]
        [Min(1.0f)] public float maxTotalSpeedMul = 4f;

        Coroutine _runner;
        float _currentMul = 1f;

        void OnEnable()
        {
            GameEvents.OnGameStart += HandleStart;
            GameEvents.OnGameOver += HandleOver;
        }

        void OnDisable()
        {
            GameEvents.OnGameStart -= HandleStart;
            GameEvents.OnGameOver -= HandleOver;
            if (_runner != null) StopCoroutine(_runner);
            _runner = null;
        }

        void HandleStart()
        {
            if (!spawner) spawner = FindFirstObjectByType<FruitSpawner>();
            _currentMul = 1f;
            if (_runner != null) StopCoroutine(_runner);
            _runner = StartCoroutine(Run());
        }

        void HandleOver()
        {
            if (_runner != null) StopCoroutine(_runner);
            _runner = null;
            _currentMul = 1f;
        }

        IEnumerator Run()
        {
            var wait = new WaitForSeconds(waveEverySeconds);
            while (true)
            {
                yield return wait;

                // Announce (drives your banner via ScorePopper in Banner mode)
                GameEvents.RaiseWaveMessage("⚡ Speeding up!", bannerSeconds);

                // Increase speed
                _currentMul = Mathf.Min(maxTotalSpeedMul, _currentMul * waveSpeedStep);
                if (spawner) spawner.SetSpeedMultiplier(_currentMul);
            }
        }
    }
}
