using System.Collections.Generic;
using UnityEngine;
using URandom = UnityEngine.Random;

namespace CatchTheFruit
{
    public class FruitSpawner : MonoBehaviour
    {
        public static FruitSpawner Instance { get; private set; }

        [Header("Required")]
        [SerializeField] private GameConfig config;
        [SerializeField] private SpawnTable spawnTable;
        [SerializeField] private Fruit fruitPrefab;

        [Header("Pooling")]
        [Min(0)] public int prewarmCount = 24;
        [SerializeField] private Transform poolRoot;

        [Header("Baseline Tuning")]
        [Tooltip("Global multiplier applied to ALL fruits’ fall speeds.")]
        [Min(0.25f)] public float globalFallSpeed = 1.6f;

        // PATCH: enums cannot have [Header] attributes — move the header to the field below.
        public enum AliveCapPreset { Manual, EasyOrMedium, Hard }  // PATCH

        [Header("Alive Cap Preset")]                                // PATCH (moved here)
        [Tooltip("Choose a preset cap (14 or 16) or Manual to use the 'Manual Max Alive' value.")]
        public AliveCapPreset capPreset = AliveCapPreset.EasyOrMedium;

        [Tooltip("Used only when Preset = Manual.")]
        [Min(1)] public int manualMaxAlive = 18;

        [Tooltip("Cap for Easy/Medium.")]
        [Min(1)] public int easyMedCap = 14;

        [Tooltip("Cap for Hard.")]
        [Min(1)] public int hardCap = 16;

        [Header("Effective Caps (runtime)")]
        [SerializeField, Min(1)] private int maxAlive = 14;     // soft cap (we simply don't spawn beyond this)
        [SerializeField, Min(1)] private int hardMaxAlive = 14; // hard cap==soft cap to avoid mid-run deletions

        [Header("Adaptive Crowd Governor")]
        [Tooltip("When alive >= this, slow the next spawn cycle a bit.")]
        [Min(1)] public int crowdThreshold = 12;

        [Tooltip("Multiply the next interval by this when crowded.")]
        [Range(1f, 2f)] public float crowdIntervalBoost = 1.35f;

        [Header("Speed Ramp Clamp")]
        [Tooltip("Clamp total fall-speed multiplier (difficulty × time × wave × global).")]
        [Min(1f)] public float maxSpeedMulCap = 2.35f;

        [Header("Spawn Pacing Floors")]
        [Tooltip("Absolute minimum interval regardless of difficulty/time/waves.")]
        [Min(0.05f)] public float hardMinInterval = 0.22f;

        [Header("Debug")]
        [SerializeField] private bool verboseLogs = false;

        // ---------- runtime ----------
        float _interval;              // decays toward _minInterval (scaled by decay)
        float _minInterval;           // floor per run (from Difficulty/SpawnTable)
        float _decay = 1f;            // per-spawn multiplier toward min (from Difficulty)
        bool _running;

        float _difficultySpeedMul = 1f;  // from Difficulty at start
        float _waveSpeedMul       = 1f;  // set by WaveDirector during run

        readonly Queue<Fruit> _pool = new();

        // ---------- External controls ----------
        public void SetSpawnTable(SpawnTable table) => spawnTable = table;

        /// <summary>Called by WaveDirector to speed things up over time (multiplies fall speed only).</summary>
        public void SetSpeedMultiplier(float waveMul) => _waveSpeedMul = Mathf.Max(0.5f, waveMul);

        /// <summary>Set the alive cap preset at runtime (e.g., from your DifficultyManager).</summary>
        public void ApplyCapPreset(bool hard)
        {
            capPreset = hard ? AliveCapPreset.Hard : AliveCapPreset.EasyOrMedium;
            ApplyAliveCaps();
        }

        void ApplyAliveCaps()
        {
            switch (capPreset)
            {
                case AliveCapPreset.EasyOrMedium:
                    maxAlive = Mathf.Max(1, easyMedCap);
                    break;
                case AliveCapPreset.Hard:
                    maxAlive = Mathf.Max(1, hardCap);
                    break;
                case AliveCapPreset.Manual:
                default:
                    maxAlive = Mathf.Max(1, manualMaxAlive);
                    break;
            }

            // Keep hard cap equal to soft cap to avoid mid-run deletions
            hardMaxAlive = maxAlive;

            // Keep crowd governor sensible relative to cap
            crowdThreshold = Mathf.Clamp(crowdThreshold, 1, Mathf.Max(1, maxAlive - 2));

            if (verboseLogs) Debug.Log($"[Spawner] Alive cap set → {maxAlive} (preset={capPreset})");
        }

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

            ApplyAliveCaps(); // keep caps coherent in Editor
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

            // Lock in alive caps for this run
            ApplyAliveCaps();

            // Pull pacing from Difficulty (with sensible fallbacks)
            float initialInterval = spawnTable.initialInterval;
            _minInterval = spawnTable.minInterval;
            _decay = Mathf.Clamp(spawnTable.intervalDecay, 0.5f, 1f);
            _difficultySpeedMul = 1f;

            if (DifficultyManager.HasCurrent)
            {
                var d = DifficultyManager.Current;
                initialInterval = Mathf.Max(0.05f, d.initialInterval);
                _minInterval    = Mathf.Max(0.05f, d.minInterval);
                _decay          = Mathf.Clamp(d.intervalDecay, 0.5f, 1f);
                _difficultySpeedMul = Mathf.Max(0.25f, d.fallSpeedMultiplier);
            }

            _minInterval = Mathf.Max(hardMinInterval, _minInterval);

            // Reset world & timers
            ClearExistingFruits();
            _waveSpeedMul = 1f;

            // Start a bit “looser” than min
            _interval = Mathf.Max(_minInterval, initialInterval);

            if (verboseLogs)
            {
                Debug.Log($"[Spawner] Start: interval={_interval:0.00}s → min={_minInterval:0.00}s, " +
                          $"decay={_decay:0.000}, fallMul(difficulty)={_difficultySpeedMul:0.##}, cap={maxAlive}");
            }

            _running = true;
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
            while (_running)
            {
                int alive = Fruit.Active.Count;

                // Never spawn beyond alive cap
                if (alive < maxAlive)
                {
                    SpawnOne();
                    alive = Fruit.Active.Count;
                }

                // Effective wait time
                float ramp = Mathf.Max(1f, DifficultyManager.SpawnRateRamp());
                float baseWait = Mathf.Max(_minInterval, _interval) / ramp;

                float randomized = baseWait * URandom.Range(0.88f, 1.12f);
                if (alive >= crowdThreshold)
                    randomized = Mathf.Max(randomized, (_minInterval / ramp) * crowdIntervalBoost);

                float wait = Mathf.Max(randomized, hardMinInterval);
                yield return new WaitForSeconds(wait);

                // Decay toward floor
                _interval = Mathf.Max(_minInterval, _interval * _decay);
            }
        }

        // ---------- Spawn ----------
        void SpawnOne()
        {
            var fd = spawnTable ? spawnTable.Pick() : null;
            if (!fd) return;

            // Final guard in case something else spawned meanwhile
            if (Fruit.Active.Count >= maxAlive) return;

            float halfWidth = ComputeHalfWidth();
            float x = URandom.Range(-halfWidth, halfWidth);
            float y = config.spawnY;

            var f = GetFromPool(new Vector3(x, y, 0f), Quaternion.identity);

            float timeRamp  = Mathf.Max(1f, DifficultyManager.FallSpeedRamp());
            float mul = spawnTable.fallSpeedMultiplier
                        * globalFallSpeed
                        * _difficultySpeedMul
                        * _waveSpeedMul
                        * timeRamp;

            mul = Mathf.Min(mul, maxSpeedMulCap);
            f.Init(fd, mul, config.groundY);

            if (verboseLogs)
                Debug.Log($"[Spawner] + {fd.id} @x={x:0.00}, fallMul={mul:0.##}, alive={Fruit.Active.Count}/{maxAlive}");
        }

        float ComputeHalfWidth()
        {
            var cam = Camera.main;
            float halfWidth = config.arenaHalfWidth;
            if (cam)
            {
                halfWidth = cam.orthographicSize * cam.aspect - 0.2f; // small edge margin
                halfWidth = Mathf.Max(0.1f, halfWidth);
            }
            return halfWidth;
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
