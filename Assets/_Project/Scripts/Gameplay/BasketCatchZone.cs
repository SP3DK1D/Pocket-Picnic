using System.Collections.Generic;
using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Player basket catch logic tuned for responsiveness.
    /// - Uses the basket's trigger collider to detect fruits.
    /// - Rim Assist: small, distance-based nudge toward center near the mouth.
    /// - Coyote Catch: brief grace period after exit to grab near-misses.
    /// - End-of-frame Scoop: one overlap sweep to catch edge cases due to order.
    /// - Smart priority: prefers non-bombs; bombs only if unavoidable (shield respected).
    /// - Emits one set of events and retires fruits pool-safely.
    /// Drop this on the same GameObject that has the Player trigger collider.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public class BasketCatchZone : MonoBehaviour
    {
        [Header("Filters")]
        [SerializeField] private LayerMask fruitMask = ~0;     // set to Fruit layer(s)

        [Header("Rim Assist")]
        [Tooltip("Max world units/second nudged toward center when close to rim.")]
        [Min(0f)] public float assistSpeed = 5.5f;
        [Tooltip("Within this world radius from center, assist is strongest; fades out to 0 at twice this.")]
        [Min(0.1f)] public float assistInnerRadius = 0.50f;

        [Header("Coyote Catch")]
        [Tooltip("Seconds after leaving the zone where a fruit can still be caught.")]
        [Range(0f, 0.25f)] public float coyoteSeconds = 0.08f;  // ~80ms feels right

        [Header("End-of-frame Scoop")]
        [Tooltip("Extra world-units radius around the basket center to check for near-misses.")]
        [Range(0f, 0.6f)] public float scoopInflation = 0.20f;
        [Tooltip("Max fruits to catch per frame (prevents spam).")]
        [Min(1)] public int maxCatchPerFrame = 3;

        [Header("Feedback (optional)")]
        [SerializeField] private CameraShaker2D shaker;
        [Range(0f, 0.12f)] public float shakeAmp = 0.04f;
        [Range(0f, 0.25f)] public float shakeDur = 0.08f;

        // --- runtime ---
        private Collider2D _zone;
        private readonly HashSet<Fruit> _inside = new();
        private readonly Dictionary<Fruit, float> _lastSeen = new();
        private readonly List<Fruit> _frameCandidates = new();
        private ContactFilter2D _filter;
        private readonly Collider2D[] _overlapBuf = new Collider2D[32];

        void Awake()
        {
            _zone = GetComponent<Collider2D>();
            _zone.isTrigger = true;

            _filter = new ContactFilter2D
            {
                useTriggers = true,
                useLayerMask = true,
                layerMask = fruitMask
            };
        }

        void OnEnable()
        {
            _inside.Clear();
            _lastSeen.Clear();
        }

        // Track entries/exits and do Rim Assist while inside
        void OnTriggerEnter2D(Collider2D other)
        {
            var f = FruitFrom(other);
            if (!f) return;
            _inside.Add(f);
            _lastSeen[f] = Time.unscaledTime; // update presence timestamp
        }

        void OnTriggerStay2D(Collider2D other)
        {
            var f = FruitFrom(other);
            if (!f) return;

            _inside.Add(f);
            _lastSeen[f] = Time.unscaledTime;

            // Gentle rim assist (distance-based), only on edible fruit (not bombs) for fairness
            if (assistSpeed > 0f && f.IsCatchable)
            {
                Vector2 c = (Vector2)transform.position;
                Vector2 pos = f.Position2D;
                float dist = Vector2.Distance(c, pos);

                // fade 1.0 at inner radius -> 0.0 at 2x radius
                float r0 = assistInnerRadius;
                float k = Mathf.InverseLerp(r0 * 2f, r0, dist);
                if (k > 0f)
                {
                    Vector2 dir = (c - pos).normalized;
                    float step = assistSpeed * k * Time.unscaledDeltaTime;
                    f.Nudge(dir * step);
                }
            }
        }

        void OnTriggerExit2D(Collider2D other)
        {
            var f = FruitFrom(other);
            if (!f) return;

            _inside.Remove(f);
            _lastSeen[f] = Time.unscaledTime; // used for coyote window
        }

        void LateUpdate()
        {
            // Gather candidates: (1) inside now, (2) within coyote window, (3) scoop near misses
            _frameCandidates.Clear();

            // 1) inside now
            foreach (var f in _inside)
            {
                if (f && f.isActiveAndEnabled) _frameCandidates.Add(f);
            }

            // 2) coyote window fruits
            if (coyoteSeconds > 0f)
            {
                float now = Time.unscaledTime;
                foreach (var kv in _lastSeen)
                {
                    var f = kv.Key;
                    if (!f || !f.isActiveAndEnabled) continue;
                    float dt = now - kv.Value;
                    if (dt > 0f && dt <= coyoteSeconds)
                    {
                        _frameCandidates.Add(f);
                    }
                }
            }

            // 3) scoop near-misses via a single overlap pass
            int found = _zone.Overlap(_filter, _overlapBuf);
            Vector2 center = transform.position;
            float scoopR = Mathf.Max(assistInnerRadius, 0.01f) + scoopInflation;

            for (int i = 0; i < found; i++)
            {
                var f = FruitFrom(_overlapBuf[i]);
                if (!f || !f.isActiveAndEnabled) continue;

                if (Vector2.Distance(f.Position2D, center) <= scoopR)
                    _frameCandidates.Add(f);
            }

            if (_frameCandidates.Count == 0) return;

            // Deduplicate and sort by priority: prefer non-bombs, then nearest to center
            var unique = new List<Fruit>(_frameCandidates.Count);
            var seen = new HashSet<Fruit>();
            for (int i = 0; i < _frameCandidates.Count; i++)
            {
                var f = _frameCandidates[i];
                if (!f || seen.Contains(f)) continue;
                seen.Add(f);
                unique.Add(f);
            }

            unique.Sort((a, b) =>
            {
                // non-bomb first
                int ab = a.IsBomb ? 1 : 0;
                int bb = b.IsBomb ? 1 : 0;
                if (ab != bb) return ab.CompareTo(bb);
                // then closer to center
                float da = (a.Position2D - center).sqrMagnitude;
                float db = (b.Position2D - center).sqrMagnitude;
                return da.CompareTo(db);
            });

            // Catch up to N fruits this frame
            int caught = 0;
            for (int i = 0; i < unique.Count && caught < maxCatchPerFrame; i++)
            {
                var f = unique[i];
                if (!f || !f.isActiveAndEnabled) continue;

                // Apply final decision respecting shield for bombs
                if (f.IsBomb)
                {
                    if (PowerupManager.ConsumeShieldIfActive())
                    {
                        // Shield consumed: safe catch (no bomb penalty)
                        f.RaiseCaughtSafe();
                    }
                    else
                    {
                        f.RaiseCaughtBomb(); // normal bomb path
                    }
                }
                else
                {
                    f.RaiseCaughtFruit();   // normal fruit
                }

                _inside.Remove(f);
                _lastSeen.Remove(f);
                caught++;
            }

            if (caught > 0)
            {
                // optional juice
                if (shaker) shaker.Shake(shakeAmp, shakeDur);
            }
        }

        // --- helpers ---
        static Fruit FruitFrom(Collider2D other)
        {
            if (!other) return null;
            other.TryGetComponent<Fruit>(out var f);
            return f;
        }
    }
}
