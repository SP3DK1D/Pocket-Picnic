using UnityEngine;
using System.Collections;
using System.Collections.Generic;
// Alias Unity's Random to avoid clashes with System.Random
using URandom = UnityEngine.Random;

namespace CatchTheFruit
{
    /// <summary>
    /// Spawns Fruit using a SpawnTable with:
    /// - Soft lanes + anti-clump
    /// - Continuous ramp (spawn interval + fall speed)
    /// - Optional camera-width bounds (auto-fit aspect ratio)
    /// - ChallengeDirector bias (BananaBlitz/BombStorm/GoldenTime)
    /// - Pooling-safe clears and stall-proof spawn loop
    /// </summary>
    public class FruitSpawner : MonoBehaviour
    {
        [Header("Required")]
        [SerializeField] private GameConfig config;        // arena bounds (fallback if camera-width off)
        [SerializeField] private SpawnTable spawnTable;    // weighted fruit list + pacing
        [SerializeField] private Fruit fruitPrefab;   // used if FruitPool absent

        [Header("Spawn Bounds")]
        [Tooltip("If ON, compute half-width from Camera.main.orthographicSize * aspect.")]
        [SerializeField] private bool useCameraWidth = true;
        [Tooltip("If ON, clamp camera width to config.arenaHalfWidth when camera is wider.")]
        [SerializeField] private bool capToConfigWidth = true;
        [Tooltip("Keep fruits a little away from screen edges.")]
        [Min(0f)][SerializeField] private float edgeMargin = 0.12f;

        [Header("Lanes & Anti-clump")]
        [Tooltip("Number of soft lanes across the width. 5–7 works well.")]
        [SerializeField] private int laneCount = 5;
        [Tooltip("Minimum horizontal distance from previous spawn X.")]
        [SerializeField] private float minLaneSeparation = 0.55f;
        [Tooltip("Small random vertical offset to avoid perfect lines.")]
        [Range(0f, 0.6f)][SerializeField] private float spawnYJitter = 0.12f;

        [Header("Timing")]
        [Tooltip("Adds small randomness to each interval (+/-).")]
        [Range(0f, 0.25f)][SerializeField] private float intervalJitter = 0.06f;

        [Header("Continuous Ramp")]
        [Tooltip("Every N spawns, apply a small ramp.")]
        [Min(1)][SerializeField] private int rampEveryNSpawns = 25;
        [Tooltip("Each ramp multiplies SpawnTable.intervalDecay by this (e.g., 0.99).")]
        [Range(0.85f, 1.0f)][SerializeField] private float decayStep = 0.99f;
        [Tooltip("Each ramp multiplies our runtime fall-speed override (e.g., 1.04).")]
        [Range(1.0f, 1.2f)][SerializeField] private float fallSpeedRamp = 1.04f;

        [Header("Debug")]
        [SerializeField] private bool verboseLogs = false;

        // ---------- Runtime ----------
        private float _interval;                  // current spawn interval
        private bool _running;
        private int _spawnCounter;
        private float _lastSpawnX = 999f;

        // External overrides (Difficulty/Waves)
        private float _speedOverride = 1f;        // multiplies table.fallSpeedMultiplier
        private float _intervalMulOverride = 1f;  // scales initial/min intervals

        // ---------- Public API for Difficulty/Wave systems ----------
        public void SetSpawnTable(SpawnTable table) => spawnTable = table;
        public void SetSpeedMultiplier(float m) => _speedOverride = Mathf.Max(0.5f, m);
        public void SetIntervalMultiplier(float m) => _intervalMulOverride = Mathf.Clamp(m, 0.5f, 1.5f);

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
            if (!fruitPrefab) Debug.LogWarning("[Spawner] Missing Fruit prefab (used if FruitPool absent).", this);
        }

        // ---------- Start/Stop ----------
        private void StartRun()
        {
            if (!config || !spawnTable)
            {
                Debug.LogWarning("[Spawner] Cannot start: assign Config + SpawnTable (+ Fruit prefab or FruitPool).", this);
                return;
            }

            ClearExistingFruits(); // fresh board

            // Reset counters
            _spawnCounter = 0;
            _lastSpawnX = 999f;

            // Initialize interval from table (scaled by override)
            _interval = Mathf.Max(0.08f, spawnTable.initialInterval * _intervalMulOverride);
            _running = true;

            if (verboseLogs)
            {
                float hw = ComputeHalfWidth();
                Debug.Log($"[Spawner] START  interval={_interval:0.00}s  min={spawnTable.minInterval * _intervalMulOverride:0.00}s  decay={spawnTable.intervalDecay:0.000}  speedMul={spawnTable.fallSpeedMultiplier * _speedOverride:0.##}  halfW={hw:0.00}");
            }

            StartCoroutine(SpawnLoop());
        }

        private void StopRun()
        {
            _running = false;
            StopAllCoroutines();
            ClearExistingFruits();
            if (verboseLogs) Debug.Log("[Spawner] STOP (cleared).");
        }

        // ---------- Main Loop ----------
        private IEnumerator SpawnLoop()
        {
            const float MIN_WAIT = 0.02f; // safety lower bound (avoids stalls with extreme decays/Freeze)
            while (_running)
            {
                SpawnOne();

                float jitter = (intervalJitter > 0f) ? URandom.Range(-intervalJitter, intervalJitter) : 0f;
                float wait = Mathf.Max(MIN_WAIT, _interval + jitter);
                yield return new WaitForSeconds(wait); // scaled → Freeze slows spawns (nice cohesion)

                // Decay interval toward min (both respect interval override)
                float min = Mathf.Max(0.08f, spawnTable.minInterval * _intervalMulOverride);
                _interval = Mathf.Max(min, _interval * spawnTable.intervalDecay);

                // Guard against invalid values
                if (!float.IsFinite(_interval)) _interval = min;
            }
        }

        // ---------- Spawn One ----------
        private void SpawnOne()
        {
            // 1) Pick a FruitData from table
            var fd = spawnTable.Pick();
            if (!fd) return;

            // 2) Challenge bias (if a director is active)
            var cd = Object.FindFirstObjectByType<ChallengeDirector>();
            if (cd) fd = cd.BiasPick(fd, spawnTable);

            // 3) Compute bounds (half width)
            float halfWidth = ComputeHalfWidth();
            float left = -halfWidth + edgeMargin;
            float right = halfWidth - edgeMargin;
            if (left > right) { left = right = 0f; } // extreme margin case

            // 4) Soft lanes + anti-clump
            float laneW = (halfWidth * 2f) / Mathf.Max(1, laneCount - 1);
            int lane = URandom.Range(0, Mathf.Max(1, laneCount));
            float x = -halfWidth + lane * laneW;

            // Prevent repeating (or nearly repeating) last X
            if (Mathf.Abs(x - _lastSpawnX) < Mathf.Max(minLaneSeparation, laneW * 0.45f))
            {
                x += (URandom.value < 0.5f ? -1f : 1f) * laneW;
            }
            x = Mathf.Clamp(x, left, right);
            _lastSpawnX = x;

            float y = config.spawnY + (spawnYJitter > 0f ? URandom.Range(-spawnYJitter, spawnYJitter) : 0f);

            // 5) Create fruit (pool first, else instantiate)
            Fruit fruit;
            if (FruitPool.Instance)
                fruit = FruitPool.Instance.Spawn(new Vector3(x, y, 0f), Quaternion.identity);
            else
                fruit = Instantiate(fruitPrefab, new Vector3(x, y, 0f), Quaternion.identity);

            if (!fruit) return;

            // 6) Compute speed multiplier (table * runtime override * optional DifficultyManager scaling)
            float speedMul = spawnTable.fallSpeedMultiplier * _speedOverride;

            // If you have DifficultyManager with dynamic scaling, optionally apply here:
            // if (DifficultyManager.HasCurrent) speedMul *= DifficultyManager.GetFallSpeedMultiplier();

            fruit.Init(fd, speedMul, config.groundY);

            // 7) Continuous ramp every N spawns
            _spawnCounter++;
            if (_spawnCounter % Mathf.Max(1, rampEveryNSpawns) == 0)
            {
                // Slightly increase spawn rate (decay steeper)
                spawnTable.intervalDecay = Mathf.Clamp01(spawnTable.intervalDecay * decayStep);
                // Slightly increase fall speed (runtime override)
                _speedOverride *= fallSpeedRamp;

                if (verboseLogs)
                {
                    Debug.Log($"[Spawner] Ramp @{_spawnCounter} → decay={spawnTable.intervalDecay:0.000}, speedOverride={_speedOverride:0.000}");
                }

                // Optional: wave toast
                GameEvents.RaiseWaveMessage("⚡ Speeding up!", 1.4f);
            }
        }

        // ---------- Helpers ----------
        private float ComputeHalfWidth()
        {
            if (!useCameraWidth || Camera.main == null || !Camera.main.orthographic)
                return config ? config.arenaHalfWidth : 3.2f;

            float camHalf = Camera.main.orthographicSize * Camera.main.aspect;
            if (capToConfigWidth && config) return Mathf.Min(config.arenaHalfWidth, camHalf);
            return camHalf;
        }

        private void ClearExistingFruits()
        {
            if (Fruit.Active.Count > 0)
            {
                var list = new List<Fruit>(Fruit.Active);
                for (int i = 0; i < list.Count; i++)
                {
                    var f = list[i];
                    if (!f) continue;
                    if (FruitPool.Instance) FruitPool.Instance.Recycle(f);
                    else Destroy(f.gameObject);
                }
                return;
            }

            // Fallback search
            var fruits = FindObjectsByType<Fruit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < fruits.Length; i++)
            {
                var f = fruits[i];
                if (!f) continue;
                if (FruitPool.Instance) FruitPool.Instance.Recycle(f);
                else Destroy(f.gameObject);
            }
        }
    }
}
