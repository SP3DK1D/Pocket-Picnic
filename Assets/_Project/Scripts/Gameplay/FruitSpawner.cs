using UnityEngine;
// Use Unity's Random explicitly to avoid clashes with System.Random
using URandom = UnityEngine.Random;

namespace CatchTheFruit
{
    /// <summary>
    /// Spawns Fruit using a SpawnTable. Supports external overrides (waves),
    /// clears all fruits on GameStart and GameOver, and uses Unity 6 APIs.
    /// KISS: one job—spawn fruits at an interval that decays over time.
    /// </summary>
    public class FruitSpawner : MonoBehaviour
    {
        [Header("Required")]
        [SerializeField] private GameConfig config;       // arena sizes, spawn/ground Y
        [SerializeField] private SpawnTable spawnTable;   // weighted fruit list + pacing
        [SerializeField] private Fruit fruitPrefab;  // the Fruit MonoBehaviour prefab

        [Header("Debug")]
        [Tooltip("Verbose logs (spawn starts, clears, warnings).")]
        [SerializeField] private bool verboseLogs = false;

        // Runtime
        private float _interval;
        private bool _running;

        // External overrides (WaveDirector can set these)
        private float _speedOverride = 1f; // multiplies table.fallSpeedMultiplier
        private float _intervalMulOverride = 1f; // multiplies initial/min intervals

        // ---------- Public API for WaveDirector ----------
        public void SetSpawnTable(SpawnTable table) => spawnTable = table;
        public void SetSpeedMultiplier(float m) => _speedOverride = Mathf.Max(0.5f, m);
        public void SetIntervalMultiplier(float m) => _intervalMulOverride = Mathf.Clamp(m, 0.5f, 1.2f);

        // ---------- Lifecycle ----------
        private void OnEnable()
        {
            GameEvents.OnGameStart += StartRun;
            GameEvents.OnGameOver += StopRun;
        }

        private void OnDisable()
        {
            GameEvents.OnGameStart -= StartRun;
            GameEvents.OnGameOver -= StopRun;
            _running = false;
            StopAllCoroutines();
        }

        private void OnValidate()
        {
            if (!config) Debug.LogWarning("[Spawner] Missing GameConfig reference.", this);
            if (!spawnTable) Debug.LogWarning("[Spawner] Missing SpawnTable reference.", this);
            if (!fruitPrefab) Debug.LogWarning("[Spawner] Missing Fruit prefab reference.", this);
        }

        // ---------- Run control ----------
        private void StartRun()
        {
            if (!config || !spawnTable || !fruitPrefab)
            {
                Debug.LogWarning("[Spawner] Cannot start: assign Config, SpawnTable, and Fruit prefab.", this);
                return;
            }

            ClearExistingFruits(); // clean slate on new run

            _interval = Mathf.Max(0.05f, spawnTable.initialInterval * _intervalMulOverride);
            _running = true;

            if (verboseLogs)
                Debug.Log($"[Spawner] Start. interval={_interval:0.00}s, min={spawnTable.minInterval * _intervalMulOverride:0.00}s, speedMul={spawnTable.fallSpeedMultiplier * _speedOverride:0.##}");

            StartCoroutine(SpawnLoop());
        }

        private void StopRun()
        {
            _running = false;
            StopAllCoroutines();
            ClearExistingFruits(); // also clear on lose
            if (verboseLogs) Debug.Log("[Spawner] Stopped and cleared.");
        }

        private System.Collections.IEnumerator SpawnLoop()
        {
            // Uses scaled time so Freeze slows spawns (feels cohesive).
            while (_running)
            {
                SpawnOne();
                yield return new WaitForSeconds(_interval);

                // Decay toward min (both scaled by _intervalMulOverride)
                float min = spawnTable.minInterval * _intervalMulOverride;
                _interval = Mathf.Max(min, _interval * spawnTable.intervalDecay);
            }
        }

        // ---------- Spawn ----------
        private void SpawnOne()
        {
            var fd = spawnTable.Pick();
            if (!fd) return;

            float x = URandom.Range(-config.arenaHalfWidth, config.arenaHalfWidth);
            float y = config.spawnY;

            var f = Instantiate(fruitPrefab, new Vector3(x, y, 0f), Quaternion.identity);

            float speedMul = spawnTable.fallSpeedMultiplier * _speedOverride;
            f.Init(fd, speedMul, config.groundY);

            if (verboseLogs) Debug.Log($"[Spawner] + {fd.id} at x={x:0.00}, speedMul={speedMul:0.##}");
        }

        // ---------- Clear ----------
        private void ClearExistingFruits()
        {
            // Fast path if registry exists
            if (Fruit.Active.Count > 0)
            {
                var list = new System.Collections.Generic.List<Fruit>(Fruit.Active);
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i]) Destroy(list[i].gameObject);
                }
                return;
            }

            // Fallback: Unity 6 API
            var fruits = FindObjectsByType<Fruit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < fruits.Length; i++)
            {
                if (fruits[i]) Destroy(fruits[i].gameObject);
            }
        }
    }
}
