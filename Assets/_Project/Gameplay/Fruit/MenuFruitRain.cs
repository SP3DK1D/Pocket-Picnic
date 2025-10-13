// Assets/_Project/Scripts/UI/MenuFruitRain.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// Alias for clarity with Random
using URandom = UnityEngine.Random;

namespace CatchTheFruit
{
    /// <summary>
    /// Lightweight fruit rain for the Main Menu with object pooling.
    /// - Spawns decorative fruits (no scoring/lives) behind UI.
    /// - Uses a local pool (no global FruitSpawner).
    /// - Unscaled time so it works regardless of Time.timeScale.
    /// - Optional content filter to avoid bombs/powerups in the menu.
    /// </summary>
    public class MenuFruitRain : MonoBehaviour
    {
        [Header("Required")]
        [SerializeField] private GameConfig config;
        [SerializeField] private SpawnTable spawnTable;
        [SerializeField] private Fruit fruitPrefab;

        [Header("Look & Pace")]
        [SerializeField] private float intervalMin = 0.35f;
        [SerializeField] private float intervalMax = 0.85f;
        [SerializeField] private float fallSpeedMul = 0.90f; // calmer than gameplay
        [SerializeField] private bool useCameraWidth = true;
        [SerializeField] private float edgeMargin = 0.15f;   // inward margin from screen edges (world units)
        [SerializeField] private int laneCount = 5;

        [Header("Content Filter")]
        [SerializeField] private bool excludeBombsAndPowerups = true;

        [Header("Despawn")]
        [SerializeField] private float despawnBelowMargin = 0.50f; // how far below screen before recycle
        [SerializeField] private float maxFruitLifetime = 20f;     // absolute safety cap (seconds, unscaled)

        [Header("Pooling")]
        [Tooltip("How many Fruit instances to allocate at start.")]
        [Min(0)][SerializeField] private int prewarm = 24;
        [Tooltip("Hard cap for pooled instances (excess spawns are skipped if pool is empty).")]
        [Min(1)][SerializeField] private int maxPool = 64;
        [SerializeField] private Transform poolRoot; // optional; auto-created if null

        // -------- runtime --------
        readonly Queue<Fruit> _pool = new Queue<Fruit>(64);
        readonly HashSet<Fruit> _live = new HashSet<Fruit>();
        Coroutine _loop;
        float _lastX = 999f;

        void Awake()
        {
            if (!poolRoot)
            {
                var go = new GameObject("~MenuFruitPool");
                go.transform.SetParent(transform, false);
                poolRoot = go.transform;
            }
            Prewarm();
        }

        void OnEnable()
        {
            if (_loop == null) _loop = StartCoroutine(SpawnLoop());
        }

        void OnDisable()
        {
            if (_loop != null) StopCoroutine(_loop);
            _loop = null;
            RecycleAllLive();
        }

        // -------- POOLING --------

        void Prewarm()
        {
            if (!fruitPrefab) return;
            int count = Mathf.Clamp(prewarm, 0, maxPool);
            for (int i = 0; i < count; i++)
            {
                var f = Instantiate(fruitPrefab, poolRoot);
                f.gameObject.SetActive(false);
                _pool.Enqueue(f);
            }
        }

        Fruit GetFromPool(Vector3 pos)
        {
            // If pool empty and we haven't hit cap, create one
            Fruit f = null;
            if (_pool.Count > 0)
            {
                f = _pool.Dequeue();
            }
            else if (TotalCount() < maxPool)
            {
                f = Instantiate(fruitPrefab, poolRoot);
            }

            if (!f) return null;

            var t = f.transform;
            t.SetParent(null, false);
            t.position = pos;
            f.gameObject.SetActive(true);
            _live.Add(f);
            return f;
        }

        void Recycle(Fruit f)
        {
            if (!f) return;
            if (_live.Contains(f)) _live.Remove(f);

            f.gameObject.SetActive(false);
            f.transform.SetParent(poolRoot, false);
            _pool.Enqueue(f);
        }

        void RecycleAllLive()
        {
            if (_live.Count == 0) return;
            // Copy to avoid modifying while iterating
            var snapshot = new List<Fruit>(_live);
            for (int i = 0; i < snapshot.Count; i++)
                Recycle(snapshot[i]);
            _live.Clear();
        }

        int TotalCount()
        {
            // Pool + live, approximate. We don’t track destroys here (we don’t Destroy pooled).
            return _pool.Count + _live.Count;
        }

        // -------- LOOP --------

        IEnumerator SpawnLoop()
        {
            // let UI settle first
            yield return null;

            while (true)
            {
                SpawnOneDecorative();

                // Unscaled wait so it keeps raining regardless of timeScale
                float wait = URandom.Range(intervalMin, intervalMax);
                float end = Time.unscaledTime + Mathf.Max(0.01f, wait);
                while (Time.unscaledTime < end) yield return null;
            }
        }

        // -------- SPAWN --------

        void SpawnOneDecorative()
        {
            if (!config || !spawnTable || !fruitPrefab) return;

            FruitData fd = spawnTable.Pick();
            if (!fd) return;

            if (excludeBombsAndPowerups && (fd.isBomb || fd.powerup))
            {
                // one retry to keep allocs down
                var retry = spawnTable.Pick();
                if (!retry || retry.isBomb || retry.powerup) return;
                fd = retry;
            }

            float halfW = ComputeHalfWidthClampedToCamera();
            float innerMargin = Mathf.Abs(edgeMargin);

            // Lanes across the visible span (already margin’d)
            float laneW = (halfW * 2f) / Mathf.Max(1, laneCount - 1);
            int lane = URandom.Range(0, Mathf.Max(1, laneCount));
            float x = -halfW + lane * laneW;

            // Nudge off same-lane repeats a bit
            if (Mathf.Abs(x - _lastX) < laneW * 0.4f)
            {
                x += (URandom.value < 0.5f ? -1f : 1f) * laneW;
                x = Mathf.Clamp(x, -halfW + innerMargin * 0.25f, halfW - innerMargin * 0.25f);
            }
            _lastX = x;

            float y = config.spawnY + URandom.Range(-0.05f, 0.05f);

            // Pull an instance
            var fruit = GetFromPool(new Vector3(x, y, 0f));
            if (!fruit) return;

            // Initialize as decorative (Fruit will respect magnet/freeze but won’t affect scoring/lives)
            fruit.Init(fd, fallSpeedMul, config.groundY, decorative: true);

            // Menu auto-despawn → RECYCLING (not Destroy)
            float cutoffY = ComputeDespawnY();
            StartCoroutine(CoAutoDespawn(fruit, cutoffY, maxFruitLifetime));
        }

        float ComputeHalfWidthClampedToCamera()
        {
            float marginW = Mathf.Abs(edgeMargin);
            Camera cam = Camera.main;

            if (useCameraWidth && cam && cam.orthographic)
            {
                float camHalf = cam.orthographicSize * cam.aspect;
                return Mathf.Max(0.1f, camHalf - marginW);
            }

            // Fallback to config-defined arena width (still respect margin)
            float cfgHalf = (config ? config.arenaHalfWidth : 3.2f);
            return Mathf.Max(0.1f, cfgHalf - marginW);
        }

        float ComputeDespawnY()
        {
            Camera cam = Camera.main;
            if (cam && cam.orthographic)
            {
                float bottom = cam.transform.position.y - cam.orthographicSize;
                return bottom - Mathf.Abs(despawnBelowMargin);
            }
            return (config ? config.groundY : -5f) - Mathf.Abs(despawnBelowMargin);
        }

        IEnumerator CoAutoDespawn(Fruit f, float cutoffY, float maxLifetime)
        {
            float t = 0f;
            maxLifetime = Mathf.Max(1f, maxLifetime);

            // Unscaled time so menu rain is independent of timeScale changes
            while (f != null && t < maxLifetime)
            {
                t += Time.unscaledDeltaTime;
                if (!f) yield break;

                if (f.transform.position.y <= cutoffY)
                {
                    Recycle(f);
                    yield break;
                }
                yield return null;
            }

            if (f != null) Recycle(f);
        }
    }
}
