// Assets/_Project/Scripts/Gameplay/FruitSpawner.cs
using System.Collections.Generic;
using UnityEngine;
using URandom = UnityEngine.Random;

namespace CatchTheFruit
{
    /// <summary>
    /// Spawns Fruit using a SpawnTable with internal object pooling.
    /// - Spawn pacing: currentInterval decays toward minInterval, then divided by DifficultyManager.SpawnRateRamp()
    ///   and randomized slightly to keep it organic.
    /// - Fall speed: SpawnTable.fallSpeedMultiplier × global × Difficulty × FallSpeedRamp() × wave.
    /// - Safety: hardMinInterval, maxAlive/hardMaxAlive, crowd governor.
    /// - Bomb lane safety: bombs avoid spawning too close (same row) to fruits near the spawn line.
    /// </summary>
    public class FruitSpawner : MonoBehaviour
    {
        public static FruitSpawner Instance { get; private set; }

        [Header("Required")]
        [SerializeField] private GameConfig config;       // arena bounds/spawn/ground
        [SerializeField] private SpawnTable spawnTable;   // weighted fruit list + base pacing
        [SerializeField] private Fruit fruitPrefab;       // pooled prefab

        [Header("Pooling")]
        [Min(0)] public int prewarmCount = 24;
        [SerializeField] private Transform poolRoot; // optional

        [Header("Baseline Tuning")]
        [Tooltip("Global multiplier applied to ALL fruits’ fall speeds.")]
        [Min(0.25f)] public float globalFallSpeed = 1.6f;

        [Tooltip("Soft limit: if reached, we temporarily slow spawns.")]
        [Min(1)] public int maxAlive = 18;

        [Header("Hard Safety Caps")]
        [Tooltip("Absolute minimum interval regardless of difficulty/time/waves.")]
        [Min(0.05f)] public float hardMinInterval = 0.22f;

        [Tooltip("Absolute maximum fruits alive (no spawns beyond this).")]
        [Min(1)] public int hardMaxAlive = 26;

        [Header("Adaptive Crowd Governor")]
        [Tooltip("When alive >= this, slow the next spawn cycle a bit.")]
        [Min(1)] public int crowdThreshold = 12;

        [Tooltip("Multiply the next interval by this when crowded.")]
        [Range(1f, 2f)] public float crowdIntervalBoost = 1.35f;

        [Header("Speed Ramp Clamp")]
        [Tooltip("Clamp total fall-speed multiplier (difficulty × time × wave × global).")]
        [Min(1f)] public float maxSpeedMulCap = 2.35f;

        [Header("Bomb Safety (same-row separation)")]
        [Tooltip("Horizontal separation required between a newly-spawned bomb and any fruit near the spawn line.")]
        [Min(0.1f)] public float bombMinSeparationX = 1.0f;

        [Tooltip("Vertical window around spawnY considered the same 'row' for the safety check.")]
        [Min(0.05f)] public float sameRowWindowY = 0.6f;

        [Tooltip("How many times to try new X for a bomb before giving up this tick.")]
        [Min(1)] public int bombSpawnMaxTries = 8;

        [Header("Debug")]
        [SerializeField] private bool verboseLogs = false;

        // ---------- runtime ----------
        float _interval;              // decays toward _minInterval (scaled by decay)
        float _minInterval;           // floor per run (from Difficulty)
        float _decay = 1f;            // per-spawn multiplier toward min (from Difficulty)

        bool _running;

        float _difficultySpeedMul = 1f;  // from Difficulty at start
        float _waveSpeedMul       = 1f;  // set by WaveDirector during run

        readonly Queue<Fruit> _pool = new();

        // ---------- External controls ----------
        public void SetSpawnTable(SpawnTable table) => spawnTable = table;

        /// <summary>Called by WaveDirector to speed things up over time (multiplies fall speed only).</summary>
        public void SetSpeedMultiplier(float waveMul) => _waveSpeedMul = Mathf.Max(0.5f, waveMul);

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
            if (hardMaxAlive < maxAlive) hardMaxAlive = maxAlive;
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

            // Always respect absolute floor
            _minInterval = Mathf.Max(hardMinInterval, _minInterval);

            // Reset world & timers
            ClearExistingFruits();
            _waveSpeedMul = 1f;

            // Start a bit “looser” than min
            _interval = Mathf.Max(_minInterval, initialInterval);

            if (verboseLogs)
            {
                Debug.Log($"[Spawner] Start: interval={_interval:0.00}s → min={_minInterval:0.00}s, " +
                          $"decay={_decay:0.000}, fallMul(difficulty)={_difficultySpeedMul:0.##}, cap={maxSpeedMulCap:0.##}");
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
                // Respect hard cap before spawning
                int alive = Fruit.Active.Count;
                if (alive < Mathf.Min(maxAlive, hardMaxAlive))
                {
                    SpawnOne();
                    alive = Fruit.Active.Count;
                }

                // Effective wait time:
                // - Smaller over time by SpawnRateRamp()
                // - Randomized slightly to feel organic
                // - Breathed by crowd governor when too many are alive
                float ramp = Mathf.Max(1f, DifficultyManager.SpawnRateRamp());
                float baseWait = Mathf.Max(_minInterval, _interval) / ramp;

                // A touch of randomness keeps cadence from becoming robotic
                float randomized = baseWait * URandom.Range(0.88f, 1.12f);

                // Crowd governor adds breathing room when the board is busy
                if (alive >= crowdThreshold)
                    randomized = Mathf.Max(randomized, (_minInterval / ramp) * crowdIntervalBoost);

                // Hard floor
                float wait = Mathf.Max(randomized, hardMinInterval);

                yield return new WaitForSeconds(wait);

                // Decay interval toward its floor for the next cycle
                _interval = Mathf.Max(_minInterval, _interval * _decay);
            }
        }

        // ---------- Spawn ----------
        void SpawnOne()
        {
            var fd = spawnTable ? spawnTable.Pick() : null;
            if (!fd) return;

            // Choose a safe X. For bombs, avoid "same row" proximity to any fruit near spawn line.
            if (!TryFindSpawnXFor(fd, out float x))
            {
                if (verboseLogs) Debug.Log("[Spawner] Skipped bomb spawn this tick (no safe lane found).");
                return; // skip this tick if we can't place bomb safely
            }

            float y = config.spawnY;

            var f = GetFromPool(new Vector3(x, y, 0f), Quaternion.identity);

            // Final fall speed multiplier (clamped)
            float timeRamp  = Mathf.Max(1f, DifficultyManager.FallSpeedRamp());
            float mul = spawnTable.fallSpeedMultiplier
                        * globalFallSpeed
                        * _difficultySpeedMul
                        * _waveSpeedMul
                        * timeRamp;

            mul = Mathf.Min(mul, maxSpeedMulCap);

            f.Init(fd, mul, config.groundY);

            if (verboseLogs)
                Debug.Log($"[Spawner] + {fd.id} @x={x:0.00}, fallMul={mul:0.##}, alive={Fruit.Active.Count}");
        }

        /// <summary>
        /// Finds a spawn X for the given fruit data. For bombs, avoids placing too close
        /// (horizontally) to any non-bomb fruit currently near the spawnY "row".
        /// </summary>
        bool TryFindSpawnXFor(FruitData fd, out float x)
        {
            float halfWidth = ComputeHalfWidth();

            // Non-bombs can just spawn anywhere
            if (fd == null || !fd.isBomb)
            {
                x = URandom.Range(-halfWidth, halfWidth);
                return true;
            }

            // Bombs: try multiple random lanes that don't conflict
            for (int attempt = 0; attempt < bombSpawnMaxTries; attempt++)
            {
                float candidate = URandom.Range(-halfWidth, halfWidth);
                if (IsBombXSafe(candidate))
                {
                    x = candidate;
                    return true;
                }
            }

            // Couldn’t find a safe lane this tick
            x = 0f;
            return false;
        }

        /// <summary>
        /// Returns true if placing a bomb at x would NOT be too close to any existing non-bomb fruit
        /// in the same "row" (|y - spawnY| <= sameRowWindowY).
        /// </summary>
        bool IsBombXSafe(float bombX)
        {
            if (Fruit.Active.Count == 0) return true;

            float rowMinY = config.spawnY - sameRowWindowY;
            float rowMaxY = config.spawnY + sameRowWindowY;

            // Check against active fruits (non-bombs) near the spawn line
            // Note: using foreach on HashSet is fine; Active is small.
            foreach (var f in Fruit.Active)
            {
                if (!f || f.decorative) continue;
                if (f.data != null && f.data.isBomb) continue; // only care about non-bomb fruit blockers

                Vector3 p = f.transform.position;
                if (p.y < rowMinY || p.y > rowMaxY) continue; // not in the same row window

                if (Mathf.Abs(p.x - bombX) < bombMinSeparationX)
                    return false; // too close horizontally to a fruit in the same row
            }

            return true;
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
