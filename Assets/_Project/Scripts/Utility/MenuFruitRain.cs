using UnityEngine;
using System.Collections;
// Alias
using URandom = UnityEngine.Random;

namespace CatchTheFruit
{
    /// <summary>
    /// Lightweight fruit rain for the Main Menu.
    /// Spawns decorative fruits (no scoring/lives) behind UI.
    /// Enable this object when Main Menu is shown; disable when leaving.
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
        [SerializeField] private float fallSpeedMul = 0.9f; // slightly calmer than gameplay
        [SerializeField] private bool useCameraWidth = true;
        [SerializeField] private float edgeMargin = 0.15f;  // inward margin from each screen edge (world units)
        [SerializeField] private int laneCount = 5;

        [Header("Content Filter")]
        [SerializeField] private bool excludeBombsAndPowerups = true;

        [Header("Despawn")]
        [SerializeField] private float despawnBelowMargin = 0.5f;  // how far below screen before despawn
        [SerializeField] private float maxFruitLifetime = 20f;     // safety cap (seconds, unscaled)

        private Coroutine _loop;
        private float _lastX = 999f;

        private void OnEnable()
        {
            if (_loop == null) _loop = StartCoroutine(SpawnLoop());
        }

        private void OnDisable()
        {
            if (_loop != null) StopCoroutine(_loop);
            _loop = null;
            ClearDecorativeOnly();
        }

        private IEnumerator SpawnLoop()
        {
            // let UI settle first
            yield return null;

            while (true)
            {
                SpawnOneDecorative();
                float wait = URandom.Range(intervalMin, intervalMax);
                yield return new WaitForSeconds(wait);
            }
        }

        private void SpawnOneDecorative()
        {
            if (!config || !spawnTable || !fruitPrefab) return;

            FruitData fd = spawnTable.Pick();
            if (!fd) return;

            if (excludeBombsAndPowerups && (fd.isBomb || fd.powerup))
            {
                // try one more pick; if still unsuitable, skip this frame
                FruitData retry = spawnTable.Pick();
                if (!retry || retry.isBomb || retry.powerup) return;
                fd = retry;
            }

            float halfW = ComputeHalfWidthClampedToCamera();   // <-- camera-based X span (with margin)
            float innerMargin = Mathf.Abs(edgeMargin);

            // Lanes across the visible span (already margin'd)
            float laneW = (halfW * 2f) / Mathf.Max(1, laneCount - 1);
            int lane = URandom.Range(0, Mathf.Max(1, laneCount));
            float x = -halfW + lane * laneW;

            // Avoid repeating nearly the same lane twice in a row
            if (Mathf.Abs(x - _lastX) < laneW * 0.4f)
            {
                x += (URandom.value < 0.5f ? -1f : 1f) * laneW;
                x = Mathf.Clamp(x, -halfW + innerMargin * 0.25f, halfW - innerMargin * 0.25f);
            }
            _lastX = x;

            float y = config.spawnY + URandom.Range(-0.05f, 0.05f);

            Fruit fruit = Instantiate(fruitPrefab, new Vector3(x, y, 0f), Quaternion.identity);
            fruit.Init(fd, fallSpeedMul, config.groundY, decorative: true);

            // Inline auto-despawn: kill decorative fruits once below the screen bottom (or after a max lifetime)
            float cutoffY = ComputeDespawnY();
            StartCoroutine(CoAutoDespawn(fruit.gameObject, cutoffY, maxFruitLifetime));
        }

        /// <summary>
        /// Returns the half-width for spawning. If useCameraWidth is ON and there is a valid
        /// orthographic Camera.main, returns (cameraHalfWidth - edgeMargin). Otherwise falls
        /// back to config.arenaHalfWidth (also minus margin).
        /// </summary>
        private float ComputeHalfWidthClampedToCamera()
        {
            float margin = Mathf.Abs(edgeMargin);
            Camera cam = Camera.main;

            if (useCameraWidth && cam && cam.orthographic)
            {
                float camHalf = cam.orthographicSize * cam.aspect;
                return Mathf.Max(0.1f, camHalf - margin);
            }

            // Fallback to config-defined arena width (still respect margin)
            float cfgHalf = (config ? config.arenaHalfWidth : 3.2f);
            return Mathf.Max(0.1f, cfgHalf - margin);
        }

        private float ComputeDespawnY()
        {
            Camera cam = Camera.main;
            if (cam && cam.orthographic)
            {
                float bottom = cam.transform.position.y - cam.orthographicSize;
                return bottom - Mathf.Abs(despawnBelowMargin);
            }
            return (config ? config.groundY : -5f) - Mathf.Abs(despawnBelowMargin);
        }

        private IEnumerator CoAutoDespawn(GameObject go, float cutoffY, float maxLifetime)
        {
            float t = 0f;
            maxLifetime = Mathf.Max(1f, maxLifetime);

            // Use unscaled time so it still cleans up if menu timeScale changes
            while (go != null && t < maxLifetime)
            {
                t += Time.unscaledDeltaTime;
                if (go == null) yield break;

                if (go.transform.position.y <= cutoffY)
                {
                    Destroy(go);
                    yield break;
                }
                yield return null;
            }

            if (go != null) Destroy(go);
        }

        private void ClearDecorativeOnly()
        {
            if (Fruit.Active.Count == 0) return;
            var list = new System.Collections.Generic.List<Fruit>(Fruit.Active);
            for (int i = 0; i < list.Count; i++)
            {
                Fruit f = list[i];
                if (!f) continue;
                if (f.decorative) Destroy(f.gameObject);
            }
        }
    }
}
