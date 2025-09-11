using System.Collections;
using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Advances through a LevelSequence and pushes values into FruitSpawner.
    /// Safe even if references are missing (just logs a warning).
    /// </summary>
    public class WaveDirector : MonoBehaviour
    {
        [Header("Refs")]
        public FruitSpawner spawner;
        public LevelSequence sequence;

        Coroutine _routine;

        void OnEnable()
        {
            GameEvents.OnGameStart += StartWaves;
            GameEvents.OnGameOver += StopWaves;
        }
        void OnDisable()
        {
            GameEvents.OnGameStart -= StartWaves;
            GameEvents.OnGameOver -= StopWaves;
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
        }

        void StartWaves()
        {
            if (!spawner || !sequence || sequence.levels == null || sequence.levels.Count == 0)
            {
                Debug.LogWarning("[WaveDirector] Missing spawner/sequence or no levels.");
                return;
            }
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Run());
        }

        void StopWaves()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
        }

        IEnumerator Run()
        {
            int i = 0;
            while (true)
            {
                var lvl = sequence.levels[i];
                if (lvl == null)
                {
                    Debug.LogWarning($"[WaveDirector] Level {i} is null, skipping.");
                }
                else
                {
                    if (lvl.spawnTable) spawner.SetSpawnTable(lvl.spawnTable);
                    spawner.SetSpeedMultiplier(lvl.speedMultiplier);
                    spawner.SetIntervalMultiplier(lvl.intervalMultiplier);

                    Debug.Log($"[Waves] → {lvl.displayName} for {lvl.durationSeconds:0}s");
                    float t = 0f;
                    float dur = Mathf.Max(0.1f, lvl.durationSeconds);
                    while (t < dur)
                    {
                        t += Time.unscaledDeltaTime; // ignore Freeze
                        yield return null;
                    }
                }

                i++;
                if (i >= sequence.levels.Count)
                {
                    if (sequence.loop) i = 0;
                    else break;
                }
            }
        }
    }
}
