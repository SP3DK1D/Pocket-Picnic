// Assets/_Project/Scripts/Gameplay/Fruit.cs
using UnityEngine;
using System.Collections.Generic;
using URandom = UnityEngine.Random;

namespace CatchTheFruit
{
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Collider2D))]
    public class Fruit : MonoBehaviour
    {
        public static readonly HashSet<Fruit> Active = new();

        [Header("Runtime (assigned by spawner)")]
        public FruitData data;
        public float fallSpeed;
        public bool decorative;

        // Fallback world Y from spawner/config if no camera detected
        float _groundY;

        // Camera-based despawn control
        [Header("Despawn (below view)")]
        [Tooltip("Extra world units below the bottom of the camera view before this fruit is culled.")]
        [Min(0f)][SerializeField] float despawnBelowMargin = 0.75f;

        [Tooltip("Fruit must be out of camera view for this long (seconds) before we retire it.")]
        [Range(0f, 2f)][SerializeField] float offscreenGraceSeconds = 0.40f;

        float _offscreenTimer;

        // tumble
        float _tumbleSpeed;  // deg/sec
        int _tumbleDir;      // +1 or -1

        // Cached helpers
        SpriteRenderer _sr;

        // Convenience props (used by BasketCatchZone)
        public bool IsBomb => data != null && data.isBomb;
        public bool IsPowerupCarrier => data != null && data.powerup != null;
        public bool IsCatchable => !decorative && (!IsBomb);
        public Vector2 Position2D => transform.position;

        // ---------- Camera cache (shared across all fruits) ----------
        // We refresh at most once per frame to avoid hammering Camera.main.
        static Camera s_Cam;
        static int s_CamCachedFrame = -1;

        static Camera Cam
        {
            get
            {
                int f = Time.frameCount;
                if (s_Cam == null || s_CamCachedFrame != f)
                {
                    s_Cam = Camera.main;     // may be null in menu/editor transitions; handled below
                    s_CamCachedFrame = f;
                }
                return s_Cam;
            }
        }

        void OnEnable()
        {
            Active.Add(this);
            _offscreenTimer = 0f;
        }
        void OnDisable()
        {
            Active.Remove(this);
        }

        public void Init(FruitData fd, float speedMultiplier, float groundY) =>
            Init(fd, speedMultiplier, groundY, false);

        public void Init(FruitData fd, float speedMultiplier, float groundY, bool decorative)
        {
            data = fd;
            _groundY = groundY;          // fallback only (camera preferred)
            this.decorative = decorative;

            if (!_sr) _sr = GetComponent<SpriteRenderer>();

            if (fd != null && _sr)
            {
                _sr.sprite = fd.sprite;
                _sr.color = fd.tint;
            }

            // Fall speed with a solid floor
            float min = (fd != null) ? Mathf.Max(6f, fd.minFallSpeed) : 6f;
            float max = (fd != null) ? Mathf.Max(min + 3f, fd.maxFallSpeed) : (min + 4f);
            float mul = Mathf.Max(0.5f, speedMultiplier);
            fallSpeed = Mathf.Max(3.2f, URandom.Range(min, max) * mul);

            // Random tumble (+40% via DifficultyManager)
            _tumbleDir = (URandom.value < 0.5f) ? -1 : 1;
            float baseTumble = URandom.Range(35f, 90f);
            float tumbleMul = Mathf.Max(0f, DifficultyManager.TumbleMultiplier); // default 1.4
            _tumbleSpeed = baseTumble * (tumbleMul <= 0f ? 1f : tumbleMul);

            // Normalize to width ≈ 0.8 world units
            const float targetW = 0.8f;
            if (_sr && _sr.sprite)
            {
                float w = _sr.sprite.bounds.size.x;
                if (w > 0.0001f) transform.localScale = Vector3.one * (targetW / w);
            }
            else
            {
                transform.localScale = Vector3.one;
            }

            name = fd ? $"Fruit_{fd.id}" : "Fruit";
            _offscreenTimer = 0f;
        }

        void Update()
        {
            // Apply freeze multiplier so Freeze always slows enough
            float freezeMul = PowerupManager.FreezeSpeedMul; // 1 when not freezing
            transform.position += Vector3.down * (fallSpeed * freezeMul) * Time.deltaTime;

            transform.Rotate(0f, 0f, _tumbleDir * _tumbleSpeed * Time.deltaTime);

            // Magnet homing (non-bombs only)
            if (!decorative && data != null && !data.isBomb &&
                PowerupManager.MagnetActive && PowerupManager.PlayerTransform)
            {
                Vector3 to = PowerupManager.PlayerTransform.position - transform.position;
                float dist = to.magnitude;
                float radius = PowerupManager.MagnetRadius;

                if (dist <= radius && dist > 0.001f)
                {
                    float closeness = 1f - Mathf.Clamp01(dist / radius);
                    float speed = PowerupManager.MagnetPullSpeed * (0.4f + 0.6f * closeness);
                    Vector3 step = to.normalized * speed * Time.deltaTime;
                    if (step.sqrMagnitude > to.sqrMagnitude) step = to;
                    transform.position += step;
                }
            }
        }

        void LateUpdate()
        {
            if (decorative) return;

            // Compute safe kill Y once per frame using cached camera
            float killY = ComputeKillYSafe();

            // Visible? Reset grace timer; otherwise accumulate
            bool visible = IsVisibleByCamera();
            _offscreenTimer = visible ? 0f : _offscreenTimer + Time.deltaTime;

            // Only retire if we've fallen past killY AND have been offscreen for the grace time
            if (transform.position.y <= killY && _offscreenTimer >= offscreenGraceSeconds)
            {
                bool isPowerup = (data != null && data.powerup != null);
                bool isBomb = (data != null && data.isBomb);
                GameEvents.RaiseFruitMissed(data?.id ?? "?", isBomb, isPowerup);
                Retire();
            }
        }

        float ComputeKillYSafe()
        {
            var cam = Cam;
            float camBottom = float.NegativeInfinity;

            if (cam && cam.orthographic)
            {
                camBottom = cam.transform.position.y - cam.orthographicSize;
                camBottom -= Mathf.Abs(despawnBelowMargin);
            }

            // Fallback to legacy groundY (usually below the camera)
            float legacy = _groundY;

            // Choose the LOWER of the two so we never cull inside the screen.
            float killY = Mathf.Min(camBottom, legacy);

            // As a final guard, never set killY above (camera bottom - 0.25f) if camera exists
            if (cam && cam.orthographic)
            {
                float conservative = (cam.transform.position.y - cam.orthographicSize) - 0.25f;
                killY = Mathf.Min(killY, conservative);
            }
            return killY;
        }

        bool IsVisibleByCamera()
        {
            // Fast path via renderer
            if (_sr && _sr.isVisible) return true;

            var cam = Cam;
            if (!cam) return true; // assume visible if no camera context (safer in transitions)

            Vector3 vp = cam.WorldToViewportPoint(transform.position);
            return vp.z > 0f && vp.x > -0.05f && vp.x < 1.05f && vp.y > -0.05f && vp.y < 1.05f;
        }

        // ----- Catch entry points (called by BasketCatchZone) -----
        public void RaiseCaughtFruit()
        {
            int score = (data != null) ? data.scoreValue : 0;
            GameEvents.RaiseFruitCaught(data?.id ?? "?", score, false);
            if (IsPowerupCarrier) GameEvents.RaisePowerupPicked(data.powerup);
            Retire();
        }

        public void RaiseCaughtSafe()
        {
            int score = (data != null) ? data.scoreValue : 0;
            GameEvents.RaiseFruitCaught(data?.id ?? "?", score, false);
            if (IsPowerupCarrier) GameEvents.RaisePowerupPicked(data.powerup);
            Retire();
        }

        public void RaiseCaughtBomb()
        {
            int score = (data != null) ? data.scoreValue : 0;
            GameEvents.RaiseFruitCaught(data?.id ?? "?", score, true);
            if (IsPowerupCarrier) GameEvents.RaisePowerupPicked(data.powerup);
            VFXManager.Instance?.PlayBombExplosion(transform.position);
            Retire();
        }

        public void Nudge(Vector2 delta)
        {
            if (decorative) return;
            transform.position += (Vector3)delta;
        }

        public void Retire()
        {
            // Recycle via spawner's pool if present, else destroy
            if (FruitSpawner.Instance)
                FruitSpawner.Instance.Recycle(this);
            else
                Destroy(gameObject);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            var cam = Cam;
            if (!cam || !cam.orthographic) return;
            float y = ComputeKillYSafe();
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.6f);
            Gizmos.DrawLine(new Vector3(-100f, y, 0f), new Vector3(100f, y, 0f));
        }
#endif
    }
}
