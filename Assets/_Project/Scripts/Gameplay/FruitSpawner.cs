using UnityEngine;
using System.Collections.Generic;
using URandom = UnityEngine.Random;

namespace CatchTheFruit
{
    public class FruitSpawner : MonoBehaviour
    {
        [Header("Required")]
        [SerializeField] private GameConfig config;
        [SerializeField] private SpawnTable spawnTable;
        [SerializeField] private Fruit fruitPrefab;

        [Header("Spawn Bounds")]
        [SerializeField] private bool useCameraWidth = true;
        [SerializeField] private bool capToConfigWidth = true;
        [Min(0f)][SerializeField] private float edgeMargin = 0.15f;

        [Header("Anti-clump")]
        [Tooltip("Minimum desired spacing from recent X's. (We still choose the best candidate if none meet this fully.)")]
        [Min(0f)] public float targetSeparationX = 0.7f;

        [Tooltip("How many recent X positions to remember when spreading spawns.")]
        [Range(0, 10)] public int rememberLastN = 4;

        [Tooltip("How many X candidates to sample per spawn; the farthest from recent is chosen.")]
        [Range(1, 10)] public int candidatesPerSpawn = 5;

        [Tooltip("Small vertical jitter so rows don't align perfectly.")]
        [Range(0f, 0.6f)] public float spawnYJitter = 0.12f;

        [Header("Timing Jitter")]
        [Tooltip("Random jitter added to the wait after each spawn (seconds).")]
        [Range(0f, 0.25f)] public float intervalJitter = 0.06f;

        [Header("Debug")]
        [SerializeField] private bool verboseLogs = false;

        float _interval;
        bool _running;

        float _speedOverride = 1f;
        float _intervalMulOverride = 1f;

        readonly Queue<float> _recentX = new Queue<float>(10);

        public void SetSpawnTable(SpawnTable table) => spawnTable = table;
        public void SetSpeedMultiplier(float m) => _speedOverride = Mathf.Max(0.5f, m);
        public void SetIntervalMultiplier(float m) => _intervalMulOverride = Mathf.Clamp(m, 0.5f, 1.2f);

        void OnEnable()
        {
            GameEvents.OnGameStart += StartRun;
            GameEvents.OnGameOver += StopRun;
        }

        void OnDisable()
        {
            GameEvents.OnGameStart -= StartRun;
            GameEvents.OnGameOver -= StopRun;
            _running = false;
            StopAllCoroutines();
        }

        void OnValidate()
        {
            if (!config) Debug.LogWarning("[Spawner] Missing GameConfig.", this);
            if (!spawnTable) Debug.LogWarning("[Spawner] Missing SpawnTable.", this);
            if (!fruitPrefab) Debug.LogWarning("[Spawner] Missing Fruit prefab.", this);
        }

        void StartRun()
        {
            if (!config || !spawnTable || !fruitPrefab)
            {
                Debug.LogWarning("[Spawner] Cannot start: assign Config, SpawnTable, and Fruit prefab.", this);
                return;
            }

            _recentX.Clear();
            ClearExistingFruits();

            _interval = Mathf.Max(0.05f, spawnTable.initialInterval * _intervalMulOverride);
            _running = true;

            if (verboseLogs)
            {
                float hw = ComputeHalfWidth();
                Debug.Log($"[Spawner] Start interval={_interval:0.00}s min={spawnTable.minInterval * _intervalMulOverride:0.00}s speedMul={spawnTable.fallSpeedMultiplier * _speedOverride:0.##} halfW={hw:0.00}");
            }

            StartCoroutine(SpawnLoop());
        }

        void StopRun()
        {
            _running = false;
            StopAllCoroutines();
            ClearExistingFruits();
            if (verboseLogs) Debug.Log("[Spawner] Stopped and cleared.");
        }

        System.Collections.IEnumerator SpawnLoop()
        {
            while (_running)
            {
                SpawnOne();

                // Add micro jitter to the wait so rhythms don’t sync
                float jitter = (intervalJitter > 0f) ? URandom.Range(-intervalJitter, intervalJitter) : 0f;
                float wait = Mathf.Max(0.01f, _interval + jitter);
                yield return new WaitForSeconds(wait);

                float min = spawnTable.minInterval * _intervalMulOverride;
                _interval = Mathf.Max(min, _interval * spawnTable.intervalDecay);
            }
        }

        void SpawnOne()
        {
            var fd = spawnTable.Pick();
            if (!fd) return;

            float halfWidth = ComputeHalfWidth();
            if (halfWidth <= 0.0001f)
                halfWidth = Mathf.Max(0.1f, config ? config.arenaHalfWidth : 3.2f);

            float left = -halfWidth + edgeMargin;
            float right = halfWidth - edgeMargin;
            if (left > right) { left = right = 0f; }

            // Best-of-N candidate selection for blue-noise-like spread
            float bestX = 0f;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < Mathf.Max(1, candidatesPerSpawn); i++)
            {
                float cand = URandom.Range(left, right);
                float score = MinDistanceToRecent(cand);

                // bonus if candidate meets target separation
                if (score >= targetSeparationX) score += 0.25f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestX = cand;
                }
            }

            // Remember
            if (rememberLastN > 0)
            {
                _recentX.Enqueue(bestX);
                while (_recentX.Count > rememberLastN) _recentX.Dequeue();
            }

            float y = config.spawnY + (spawnYJitter > 0f ? URandom.Range(-spawnYJitter, spawnYJitter) : 0f);

            var f = Instantiate(fruitPrefab, new Vector3(bestX, y, 0f), Quaternion.identity);
            float speedMul = spawnTable.fallSpeedMultiplier * _speedOverride;
            f.Init(fd, speedMul, config.groundY);

            if (verboseLogs) Debug.Log($"[Spawner] + {fd.id} at x={bestX:0.00} (score {bestScore:0.00})");
        }

        float MinDistanceToRecent(float x)
        {
            if (_recentX.Count == 0) return 999f;
            float min = float.PositiveInfinity;
            foreach (var r in _recentX)
                min = Mathf.Min(min, Mathf.Abs(x - r));
            return min;
        }

        float ComputeHalfWidth()
        {
            if (!useCameraWidth || Camera.main == null || !Camera.main.orthographic)
                return config ? config.arenaHalfWidth : 3.2f;

            float fromCamera = Camera.main.orthographicSize * Camera.main.aspect;
            if (capToConfigWidth && config)
                return Mathf.Min(config.arenaHalfWidth, fromCamera);
            return fromCamera;
        }

        void ClearExistingFruits()
        {
            if (Fruit.Active.Count > 0)
            {
                var list = new List<Fruit>(Fruit.Active);
                for (int i = 0; i < list.Count; i++)
                    if (list[i]) Destroy(list[i].gameObject);
                return;
            }

            var fruits = FindObjectsByType<Fruit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < fruits.Length; i++)
                if (fruits[i]) Destroy(fruits[i].gameObject);
        }
    }
}
