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

        [Header("Spawn Pacing")]
        [Tooltip("Base spawn interval at game start (already tuned down ~25%).")]
        [Min(0.05f)] public float initialInterval = 1.10f;   // was ~0.9
        [Tooltip("Minimum spawn interval (never faster than this).")]
        [Min(0.05f)] public float minInterval = 0.44f;       // was ~0.35
        [Tooltip("Per-spawn decay toward min interval (0.98 = slowly speeds up).")]
        [Range(0.5f, 1f)] public float intervalDecay = 0.985f;
        [Tooltip("Extra spacing safety to avoid bursts.")]
        [Min(0.05f)] public float noBurstMinGap = 0.40f;

        [Header("Alive Cap")]
        [Min(1)] public int maxAlive = 14;

        [Header("Debug")]
        public bool verboseLogs = false;

        // ---------- runtime ----------
        readonly Queue<Fruit> _pool = new();
        bool _running;
        float _interval;

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

        Fruit GetFromPool(Vector3 pos)
        {
            Fruit f = _pool.Count > 0 ? _pool.Dequeue() : Instantiate(fruitPrefab);
            var t = f.transform;
            t.SetParent(null, false);
            t.position = pos;
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

        // ---------- RUN ----------
        void StartRun()
        {
            StopAllCoroutines();
            _running = true;

            // Use SpawnTable pacing if assigned; otherwise local fields
            _interval = spawnTable ? Mathf.Max(spawnTable.minInterval, spawnTable.initialInterval) : initialInterval;
            var minFloor = spawnTable ? spawnTable.minInterval : minInterval;
            minInterval = Mathf.Max(minInterval, minFloor);
            intervalDecay = spawnTable ? spawnTable.intervalDecay : intervalDecay;

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
            yield return new WaitForSeconds(0.25f);

            while (_running)
            {
                // ----- Freeze-aware alive cap -----
                float freezeMul = GetFreezeMul();                   // 1 when normal, <1 when frozen
                int effectiveMaxAlive = Mathf.Max(
                    1,
                    Mathf.FloorToInt(maxAlive * Mathf.Clamp(freezeMul, 0.45f, 1f))
                );

                if (Fruit.Active.Count < effectiveMaxAlive)
                    SpawnOne();

                // decay toward min
                _interval = Mathf.Max(minInterval, _interval * intervalDecay);

                // base wait with anti-burst
                float wait = Mathf.Max(_interval, noBurstMinGap);

                // ----- Freeze-aware spawn slow-down -----
                // If fruits fall at half speed (freezeMul=0.5), stretch wait ≈ 2x.
                // Clamp to avoid extreme values if a powerup sets it too low.
                float freezeScale = 1f / Mathf.Clamp(freezeMul, 0.35f, 1f);
                wait *= freezeScale;

                yield return new WaitForSeconds(wait);
            }
        }

        // ---------- SPAWN ----------
        void SpawnOne()
        {
            var fd = spawnTable ? spawnTable.Pick() : null;
            if (!fd) return;

            float halfWidth = ComputeHalfWidth();
            float x = URandom.Range(-halfWidth, halfWidth);
            float y = config.spawnY;

            var f = GetFromPool(new Vector3(x, y, 0f));

            // Absolute target fall speed from Difficulty (base + step ramp + cap)
            float desiredSpeed = DifficultyManager.CurrentFallSpeed(); // world units/sec

            // Convert absolute target speed to the multiplier Fruit.Init expects
            float baseline = Mathf.Max(0.01f, (fd.minFallSpeed + fd.maxFallSpeed) * 0.5f);
            float mul = Mathf.Max(0.2f, desiredSpeed / baseline);

            if (verboseLogs)
                Debug.Log($"[Spawner] {fd.id} desired={desiredSpeed:0.00} baseline={baseline:0.00} mul={mul:0.00} alive={Fruit.Active.Count}/{maxAlive}");

            f.Init(fd, mul, config.groundY);
        }

        float ComputeHalfWidth()
        {
            var cam = Camera.main;
            float halfWidth = config.arenaHalfWidth;
            if (cam)
            {
                halfWidth = cam.orthographicSize * cam.aspect - 0.2f;
                halfWidth = Mathf.Max(0.1f, halfWidth);
            }
            return halfWidth;
        }

        void ClearExistingFruits()
        {
            if (Fruit.Active.Count > 0)
            {
                var list = new List<Fruit>(Fruit.Active);
                for (int i = 0; i < list.Count; i++)
                    if (list[i]) Recycle(list[i]);
            }
        }

        // ----- Helpers -----
        static float GetFreezeMul()
        {
            // Your Fruit.Update already uses PowerupManager.FreezeSpeedMul.
            // When not freezing this should be exactly 1.
            // Gracefully handle missing manager by treating as no-freeze.
            try
            {
                return Mathf.Clamp01(PowerupManager.FreezeSpeedMul);
            }
            catch
            {
                return 1f;
            }
        }
    }
}
