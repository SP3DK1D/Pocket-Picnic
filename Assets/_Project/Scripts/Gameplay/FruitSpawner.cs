using System.Collections.Generic;
using UnityEngine;
using URandom = UnityEngine.Random;

namespace CatchTheFruit
{
    /// <summary>
    /// Spawns Fruit using a SpawnTable and manages an internal object pool.
    /// - Singleton Instance provides Spawn/Recycle for Fruits.
    /// - Clears/recovers fruits on GameStart/GameOver.
    /// - Uses scaled time for spawn pacing (plays nicely with gravity-based Freeze).
    /// </summary>
    public class FruitSpawner : MonoBehaviour
    {
        public static FruitSpawner Instance { get; private set; }

        [Header("Required")]
        [SerializeField] private GameConfig config;       // arena sizes, spawn/ground Y
        [SerializeField] private SpawnTable spawnTable;   // weighted fruit list + pacing
        [SerializeField] private Fruit fruitPrefab;       // prefab for pooled items

        [Header("Pooling")]
        [Min(0)] public int prewarmCount = 24;
        [Tooltip("Optional container for pooled fruit objects (kept disabled).")]
        [SerializeField] private Transform poolRoot; // optional

        [Header("Tuning")]
        [Tooltip("Global multiplier applied to ALL fruits’ fall speeds.")]
        [Min(0.25f)] public float globalFallSpeed = 1.6f;
        [Tooltip("Prevent board flood: skip spawns if this many fruits are alive.")]
        [Min(1)] public int maxAlive = 20;

        [Header("Debug")]
        [SerializeField] private bool verboseLogs = false;

        // ---- runtime ----
        float _interval;
        bool _running;

        // External overrides (WaveDirector can set these)
        float _speedOverride = 1f;        // multiplies table.fallSpeedMultiplier
        float _intervalMulOverride = 1f;  // multiplies initial/min intervals

        // Pool
        readonly Queue<Fruit> _pool = new();

        // ---------- Public API for WaveDirector ----------
        public void SetSpawnTable(SpawnTable table) => spawnTable = table;
        public void SetSpeedMultiplier(float m) => _speedOverride = Mathf.Max(0.5f, m);
        public void SetIntervalMultiplier(float m) => _intervalMulOverride = Mathf.Clamp(m, 0.5f, 1.2f);

        // ---------- Lifecycle ----------
        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (!poolRoot)
            {
                var go = new GameObject("~FruitPool");
                go.transform.SetParent(transform, false);
                poolRoot = go.transform;
            }

            Prewarm();
        }

        void OnEnable()
        {
            GameEvents.OnGameStart += StartRun;
            GameEvents.OnGameOver  += StopRun;
        }

        void OnDisable()
        {
            GameEvents.OnGameStart -= StartRun;
            GameEvents.OnGameOver  -= StopRun;
            _running = false;
            StopAllCoroutines();
        }

        void OnValidate()
        {
            if (!config)     Debug.LogWarning("[Spawner] Missing GameConfig reference.", this);
            if (!spawnTable) Debug.LogWarning("[Spawner] Missing SpawnTable reference.", this);
            if (!fruitPrefab)Debug.LogWarning("[Spawner] Missing Fruit prefab reference.", this);
        }

        // ---------- Pool ----------
        void Prewarm()
        {
            if (!fruitPrefab) { Debug.LogError("[Spawner] Assign Fruit prefab.", this); return; }
            for (int i = 0; i < prewarmCount; i++)
            {
                var f = Instantiate(fruitPrefab, poolRoot);
                f.gameObject.SetActive(false);
                _pool.Enqueue(f);
            }
        }

        Fruit GetFromPool(Vector3 pos, Quaternion rot)
        {
            Fruit f = _pool.Count > 0 ? _pool.Dequeue() : Instantiate(fruitPrefab);
            var t = f.transform;
            t.SetParent(null, false);
            t.SetPositionAndRotation(pos, rot);
            f.gameObject.SetActive(true);
            return f;
        }

        public void Recycle(Fruit f)
        {
            if (!f) return;
            f.gameObject.SetActive(false);
            f.transform.SetParent(poolRoot, false);
            _pool.Enqueue(f);
        }

        // ---------- Run control ----------
        void StartRun()
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
                Debug.Log($"[Spawner] Start. interval={_interval:0.00}s, min={spawnTable.minInterval * _intervalMulOverride:0.00}s, speedMul={(spawnTable.fallSpeedMultiplier * _speedOverride * globalFallSpeed):0.##}");

            StartCoroutine(SpawnLoop());
        }

        void StopRun()
        {
            _running = false;
            StopAllCoroutines();
            ClearExistingFruits();
            if (verboseLogs) Debug.Log("[Spawner] Stopped and cleared.");
        }

        public void StopAndClear() => StopRun();

        System.Collections.IEnumerator SpawnLoop()
        {
            // Uses scaled time by design so pacing matches the slowed world feel.
            while (_running)
            {
                if (Fruit.Active.Count < maxAlive)
                {
                    SpawnOne();
                }

                yield return new WaitForSeconds(_interval);

                // Decay toward min (both scaled by _intervalMulOverride)
                float min = spawnTable.minInterval * _intervalMulOverride;
                _interval = Mathf.Max(min, _interval * spawnTable.intervalDecay);
            }
        }

        // ---------- Spawn ----------
        void SpawnOne()
        {
            var fd = spawnTable.Pick();
            if (!fd) return;

            // Calculate safe spawn range based on camera width
            var cam = Camera.main;
            float halfWidth = config.arenaHalfWidth;
            if (cam)
            {
                halfWidth = cam.orthographicSize * cam.aspect - 0.2f; // margin so fruits aren’t cut off
                halfWidth = Mathf.Max(0.1f, halfWidth);
            }

            float x = URandom.Range(-halfWidth, halfWidth);
            float y = config.spawnY;

            var f = GetFromPool(new Vector3(x, y, 0f), Quaternion.identity);

            // Final speed multiplier includes globalFallSpeed
            float speedMul = spawnTable.fallSpeedMultiplier * _speedOverride * globalFallSpeed;
            f.Init(fd, speedMul, config.groundY);

            if (verboseLogs)
                Debug.Log($"[Spawner] + {fd.id} at x={x:0.00}, speedMul={speedMul:0.##}, alive={Fruit.Active.Count}");
        }

        // ---------- Clear ----------
        void ClearExistingFruits()
        {
            if (Fruit.Active.Count > 0)
            {
                var list = new List<Fruit>(Fruit.Active);
                for (int i = 0; i < list.Count; i++)
                {
                    var fruit = list[i];
                    if (!fruit) continue;
                    Recycle(fruit);
                }
                return;
            }

            var fruits = FindObjectsByType<Fruit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < fruits.Length; i++)
            {
                var fruit = fruits[i];
                if (!fruit) continue;
                Recycle(fruit);
            }
        }
    }
}
