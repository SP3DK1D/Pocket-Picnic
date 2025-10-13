using System.Collections.Generic;
using UnityEngine;
using URandom = UnityEngine.Random;

namespace CatchTheFruit
{
    /// <summary>
    /// Spawns Fruit using a simple Update-driven scheduler (no coroutines / no GC).
    /// - Honors difficulty pacing and freeze slowdown.
    /// - Pooling first, Instantiate when pool runs dry.
    /// - Public API preserved: Start/Stop driven by GameEvents; StopAndClear(); SetSpeedMultiplier ignored (legacy).
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

        [Header("Spawn Pacing (fallback when no SpawnTable)")]
        [Tooltip("Base spawn interval at game start (already tuned down ~25%).")]
        [Min(0.05f)] public float initialInterval = 1.10f;   // used only if no SpawnTable
        [Tooltip("Minimum spawn interval (never faster than this).")]
        [Min(0.05f)] public float minInterval = 0.44f;       // used only if no SpawnTable
        [Tooltip("Per-spawn decay toward min interval (0.98 = slowly speeds up).")]
        [Range(0.5f, 1f)] public float intervalDecay = 0.985f;
        [Tooltip("Extra spacing safety to avoid bursts.")]
        [Min(0.05f)] public float noBurstMinGap = 0.40f;

        [Header("Alive Cap")]
        [Min(1)] public int maxAlive = 14;

        [Header("Debug")]
        public bool verboseLogs = false;

        // ---------- runtime ----------
        private readonly Queue<Fruit> _pool = new();
        private bool _running;
        private float _interval;         // current spawn interval (decays toward min)
        private float _minInterval;      // cached min based on SpawnTable/fallback
        private float _decay;            // cached decay based on SpawnTable/fallback

        private float _nextAt;           // absolute Time.time when next spawn should occur
        private float _lastWaitUsed;     // for debugging

        // ===== Legacy public API (kept for compatibility; waves disabled) =====
        public void SetSpeedMultiplier(float _) { /* waves disabled / ignored */ }
        public void StopAndClear() => StopRun();

        // ---------- LIFECYCLE ----------
        private void Awake()
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

        private void OnEnable()
        {
            GameEvents.OnGameStart += StartRun;
            GameEvents.OnGameOver += StopRun;
        }
        private void OnDisable()
        {
            GameEvents.OnGameStart -= StartRun;
            GameEvents.OnGameOver -= StopRun;
        }

        private void Prewarm()
        {
            if (!fruitPrefab) return;
            for (int i = 0; i < prewarmCount; i++)
            {
                var f = Instantiate(fruitPrefab, poolRoot);
                f.gameObject.SetActive(false);
                _pool.Enqueue(f);
            }
        }

        private Fruit GetFromPool(Vector3 pos)
        {
            Fruit f = _pool.Count > 0 ? _pool.Dequeue() : Instantiate(fruitPrefab);
            var t = f.transform;
            if (t.parent != null) t.SetParent(null, false);
            t.position = pos;
            f.gameObject.SetActive(true);
            return f;
        }

        public void Recycle(Fruit f)
        {
            if (!f) return;
            // Avoid double-enqueue if someone calls Recycle twice accidentally
            if (!f.gameObject.activeSelf) return;

            f.gameObject.SetActive(false);
            var t = f.transform;
            if (t.parent != poolRoot) t.SetParent(poolRoot, false);
            _pool.Enqueue(f);
        }

        // ---------- RUN ----------
        private void StartRun()
        {
            _running = true;

            // Use SpawnTable pacing if assigned; otherwise local fields
            _interval = spawnTable ? Mathf.Max(spawnTable.minInterval, spawnTable.initialInterval) : initialInterval;
            _minInterval = spawnTable ? spawnTable.minInterval : minInterval;
            _decay = spawnTable ? spawnTable.intervalDecay : intervalDecay;

            ClearExistingFruits();

            // small grace so UI settles
            _nextAt = Time.time + 0.25f;
        }

        private void StopRun()
        {
            _running = false;
            ClearExistingFruits();
        }

        private void Update()
        {
            if (!_running) return;
            if (Time.time < _nextAt) return;

            // ----- Freeze-aware alive cap -----
            float freezeMul = GetFreezeMul();                   // 1 normal; <1 when frozen
            int effectiveMaxAlive = Mathf.Max(
                1,
                Mathf.FloorToInt(maxAlive * Mathf.Clamp(freezeMul, 0.45f, 1f))
            );

            if (Fruit.Active.Count < effectiveMaxAlive)
            {
                SpawnOne();
                // decay toward min AFTER each successful spawn
                _interval = Mathf.Max(_minInterval, _interval * _decay);
            }

            // Base wait with anti-burst
            float wait = Mathf.Max(_interval, noBurstMinGap);

            // Freeze-aware spawn slow-down.
            // If fruits fall at half speed (freezeMul=0.5), stretch wait ≈ 2x (clamped).
            float freezeScale = 1f / Mathf.Clamp(freezeMul, 0.35f, 1f);
            wait *= freezeScale;

            _lastWaitUsed = wait;                // debug only
            _nextAt = Time.time + wait;          // schedule next check
        }

        // ---------- SPAWN ----------
        private void SpawnOne()
        {
            var fd = spawnTable ? spawnTable.Pick() : null;
            if (!fd) return;
            if (!config) return;

            float halfWidth = ComputeHalfWidthCached();
            float x = URandom.Range(-halfWidth, halfWidth);
            float y = config.spawnY;

            var f = GetFromPool(new Vector3(x, y, 0f));

            // Absolute target fall speed from Difficulty (base + step ramp + cap)
            float desiredSpeed = DifficultyManager.CurrentFallSpeed(); // world units/sec

            // Convert absolute target speed to the multiplier Fruit.Init expects
            float baseline = Mathf.Max(0.01f, (fd.minFallSpeed + fd.maxFallSpeed) * 0.5f);
            float mul = Mathf.Max(0.2f, desiredSpeed / baseline);

            if (verboseLogs)
                Debug.Log($"[Spawner] {fd.id} desired={desiredSpeed:0.00} baseline={baseline:0.00} mul={mul:0.00} alive={Fruit.Active.Count}/{maxAlive} wait≈{_lastWaitUsed:0.00}");

            f.Init(fd, mul, config.groundY);
        }

        // Cache one camera reference per frame to avoid repeated Camera.main lookups
        private float ComputeHalfWidthCached()
        {
            var cam = Camera.main;
            if (cam)
            {
                float half = cam.orthographicSize * cam.aspect - 0.2f;
                return Mathf.Max(0.1f, half);
            }
            return config ? config.arenaHalfWidth : 3f;
        }

        private void ClearExistingFruits()
        {
            if (Fruit.Active.Count > 0)
            {
                var list = new List<Fruit>(Fruit.Active);
                for (int i = 0; i < list.Count; i++)
                    if (list[i]) Recycle(list[i]);
            }
        }

        // ----- Helpers -----
        private static float GetFreezeMul()
        {
            // Fruit.Update already uses PowerupManager.FreezeSpeedMul (1 when not freezing).
            // Guard in case manager isn't present yet.
            try { return Mathf.Clamp01(PowerupManager.FreezeSpeedMul); }
            catch { return 1f; }
        }
    }
}
