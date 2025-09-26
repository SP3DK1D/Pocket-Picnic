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
        [SerializeField] private float edgeMargin = 0.15f;
        [SerializeField] private int laneCount = 5;

        [Header("Content Filter")]
        [SerializeField] private bool excludeBombsAndPowerups = true;

        Coroutine _loop;
        float _lastX = 999f;

        void OnEnable()
        {
            if (_loop == null) _loop = StartCoroutine(SpawnLoop());
        }
        void OnDisable()
        {
            if (_loop != null) StopCoroutine(_loop);
            _loop = null;
            ClearDecorativeOnly();
        }

        IEnumerator SpawnLoop()
        {
            yield return null; // let UI settle first

            while (true)
            {
                SpawnOneDecorative();
                float wait = URandom.Range(intervalMin, intervalMax);
                yield return new WaitForSeconds(wait);
            }
        }

        void SpawnOneDecorative()
        {
            if (!config || !spawnTable || !fruitPrefab) return;

            var fd = spawnTable.Pick();
            if (!fd) return;

            if (excludeBombsAndPowerups && (fd.isBomb || fd.powerup))
            {
                // try one more pick; if still unsuitable, skip this frame
                var retry = spawnTable.Pick();
                if (!retry || retry.isBomb || retry.powerup) return;
                fd = retry;
            }

            float halfW = ComputeHalfWidth();
            float laneW = (halfW * 2f) / Mathf.Max(1, laneCount - 1);
            int lane = URandom.Range(0, Mathf.Max(1, laneCount));
            float x = -halfW + lane * laneW;

            if (Mathf.Abs(x - _lastX) < laneW * 0.4f)
            {
                x += (URandom.value < 0.5f ? -1f : 1f) * laneW;
                x = Mathf.Clamp(x, -halfW + edgeMargin, halfW - edgeMargin);
            }
            _lastX = x;

            float y = config.spawnY + URandom.Range(-0.05f, 0.05f);

            var fruit = Instantiate(fruitPrefab, new Vector3(x, y, 0f), Quaternion.identity);
            fruit.Init(fd, fallSpeedMul, config.groundY, decorative: true);
        }

        float ComputeHalfWidth()
        {
            if (!useCameraWidth || !Camera.main || !Camera.main.orthographic)
                return config ? config.arenaHalfWidth : 3.2f;

            float camHalf = Camera.main.orthographicSize * Camera.main.aspect;
            if (config) return Mathf.Min(config.arenaHalfWidth, camHalf);
            return camHalf;
        }

        void ClearDecorativeOnly()
        {
            if (Fruit.Active.Count == 0) return;
            var list = new System.Collections.Generic.List<Fruit>(Fruit.Active);
            for (int i = 0; i < list.Count; i++)
            {
                var f = list[i];
                if (!f) continue;
                if (f.decorative) Destroy(f.gameObject);
            }
        }
    }
}
