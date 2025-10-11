// Assets/_Project/Scripts/Gameplay/FruitSpawner.cs
using System.Collections.Generic;
using UnityEngine;
using URandom = UnityEngine.Random;

namespace CatchTheFruit
{
    /// <summary>
    /// Spawns Fruit using a light pool and a decay-to-min interval.
    /// Behavior matches the previous implementation; code paths are simpler and GC-free.
    /// </summary>
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

        [Header("Spawn Pacing (fallback if no SpawnTable)")]
        [Tooltip("Base spawn interval at game start (used only when SpawnTable is missing).")]
        [Min(0.05f)] public float fallbackInitialInterval = 1.10f;
        [Tooltip("Minimum spawn interval (used only when SpawnTable is missing).")]
        [Min(0.05f)] public float fallbackMinInterval = 0.44f;
        [Tooltip("Per-spawn decay toward min (used only when SpawnTable is missing).")]
        [Range(0.5f, 1f)] public float fallbackIntervalDecay = 0.985f;

        [Header("Spawn Regularization")]
        [Tooltip("Extra spacing safety to avoid tight bursts.")]
        [Min(0.05f)] public float noBurstMinGap = 0.40f;

        [Header("Alive Cap")]
        [Min(1)] public int maxAlive = 14;

        [Header("Debug")]
        public bool verboseLogs = false;

        // ---------- runtime ----------
        readonly Queue<Fruit> _pool = new Queue<Fruit>(64);
        bool _running;

        // Per-run pacing snapshot (we do NOT mutate inspector fields or SpawnTable values)
        float _interval;
        float _minInterval;
        float _decay;

        // Small shared scratch to avoid allocations when clearing
        static readonly List<Fruit> s_scratch = new List<Fruit>(64);

        // ---------- camera cache (once per frame) ----------
        static Camera s_cam;
        static int s_camFrame = -1;
        static Camera Cam
        {
            get
            {
                int f = Time.frameCount;
                if (s_cam == null || s_camFrame != f)
                {
                    s_cam = Camera.main;
                    s_camFrame = f;
                }
                return s_cam;
            }
        }

        // ===== Legacy public API (kept for compatibility; waves disabled) =====
        public void SetSpeedMultiplier(float _) { /* waves disabled / ignored */ }
        public void StopAndClear() => StopRun();

        // ---------- LIFECYCLE ----------
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

            if (!fruitPrefab)
            {
                Debug.LogError("[FruitSpawner] Fruit prefab not assigned.", this);
            }

            Prewarm();
        }

        void OnEnable()
        {
            GameEvents.OnGameStart += StartRun;
            GameEvents.OnGameOver += StopRun;
        }
        void OnDisable()
        {
            GameEvents.OnGameStart -= StartRun;
            GameEvents.OnGameOver -= StopRun;
        }

        // ---------- POOL ----------
        void Prewarm()
        {
            if (!fruitPrefab) return;
            for (int i = 0; i < prewarmCount; i++)
            {
                var f = Instantiate(fruitPrefab, poolRoot);
                f.gameObject.SetActive(false);
                _pool.Enqueue(f);
            }
        }

        Fruit GetFromPool(in Vector3 pos)
        {
            Fruit f = (_pool.Count > 0) ? _pool.Dequeue() : Instantiate(fruitPrefab);
            if (!f) return null;

            var t = f.transform;
            t.SetParent(null, false);
            t.position = pos;

            // ensure enabled before Init
            if (!f.gameObject.activeSelf) f.gameObject.SetActive(true);
            return f;
        }

        public void Recycle(Fruit f)
        {
            if (!f) return;
            f.gameObject.SetActive(false);
            f.transform.SetParent(poolRoot, false);
            _pool.Enqueue(f);
        }

        // ---------- RUN ----------
        void StartRun()
        {
            if (!enabled) return;

            StopAllCoroutines();
            _running = true;

            // Snapshot pacing (prefer SpawnTable if assigned)
            if (spawnTable)
            {
                _interval = Mathf.Max(spawnTable.minInterval, spawnTable.initialInterval);
                _minInterval = Mathf.Max(0.05f, spawnTable.minInterval);
                _decay = Mathf.Clamp(spawnTable.intervalDecay, 0.5f, 1f);
            }
            else
            {
                _interval = Mathf.Max(fallbackMinInterval, fallbackInitialInterval);
                _minInterval = Mathf.Max(0.05f, fallbackMinInterval);
                _decay = Mathf.Clamp(fallbackIntervalDecay, 0.5f, 1f);
            }

            ClearExistingFruits();
            StartCoroutine(SpawnLoop());
        }

        void StopRun()
        {
            _running = false;
            StopAllCoroutines();
            ClearExistingFruits();
        }

        System.Collections.IEnumerator SpawnLoop()
        {
            // small settle to avoid front-loading the first spawn with UI open
            yield return new WaitForSeconds(0.25f);

            while (_running)
            {
                // ----- Freeze-aware alive cap -----
                float freezeMul = GetFreezeMul();            // 1 normal, <1 when frozen
                int effectiveMaxAlive = Mathf.Max(1, Mathf.FloorToInt(maxAlive * Mathf.Clamp(freezeMul, 0.45f, 1f)));

                if (Fruit.Active.Count < effectiveMaxAlive)
                    SpawnOne();

                // decay toward min
                _interval = Mathf.Max(_minInterval, _interval * _decay);

                // base wait with anti-burst
                float wait = Mathf.Max(_interval, noBurstMinGap);

                // ----- Freeze-aware spawn slow-down -----
                // If fruits fall at half speed (freeze=0.5), stretch wait ≈ 2x.
                float freezeScale = 1f / Mathf.Clamp(freezeMul, 0.35f, 1f);
                wait *= freezeScale;

                yield return new WaitForSeconds(wait);
            }
        }

        // ---------- SPAWN ----------
        void SpawnOne()
        {
            if (!config || !fruitPrefab) return;

            // Pick FruitData
            var fd = spawnTable ? spawnTable.Pick() : null;
            if (!fd) return;

            // X position across visible half-width
            float halfWidth = ComputeHalfWidth();
            float x = URandom.Range(-halfWidth, halfWidth);
            float y = config.spawnY;

            var f = GetFromPool(new Vector3(x, y, 0f));
            if (!f) return;

            // Absolute desired fall speed from Difficulty (world units/sec)
            float desiredSpeed = DifficultyManager.CurrentFallSpeed();

            // Convert to multiplier Fruit.Init expects, based on data's baseline
            float baseline = Mathf.Max(0.01f, (fd.minFallSpeed + fd.maxFallSpeed) * 0.5f);
            float mul = Mathf.Max(0.2f, desiredSpeed / baseline);

            if (verboseLogs)
                Debug.Log($"[Spawner] {fd.id} desired={desiredSpeed:0.00} baseline={baseline:0.00} mul={mul:0.00} alive={Fruit.Active.Count}/{maxAlive}");

            f.Init(fd, mul, config.groundY);
        }

        float ComputeHalfWidth()
        {
            var cam = Cam;
            if (cam && cam.orthographic)
            {
                // a small margin keeps fruits from popping at exact edges
                float half = cam.orthographicSize * cam.aspect - 0.2f;
                return Mathf.Max(0.1f, half);
            }

            // fallback to config
            return (config ? Mathf.Max(0.1f, config.arenaHalfWidth) : 3f);
        }

        void ClearExistingFruits()
        {
            if (Fruit.Active.Count == 0) return;

            // Reuse static scratch to avoid repeated allocations
            s_scratch.Clear();
            s_scratch.Capacity = Mathf.Max(s_scratch.Capacity, Fruit.Active.Count);

            // Copy because Recycle mutates the HashSet
            foreach (var f in Fruit.Active) if (f) s_scratch.Add(f);

            for (int i = 0; i < s_scratch.Count; i++)
            {
                var f = s_scratch[i];
                if (f) Recycle(f);
            }

            s_scratch.Clear();
        }

        // ----- Helpers -----
        static float GetFreezeMul()
        {
            // Keep robust even if PowerupManager is missing in a test scene
            try { return Mathf.Clamp01(PowerupManager.FreezeSpeedMul); }
            catch { return 1f; }
        }
    }
}
