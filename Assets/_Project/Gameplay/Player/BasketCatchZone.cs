using System.Collections.Generic;
using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Player basket catch logic (optimized for minimal allocations).
    /// - Uses the basket's trigger collider to detect fruits.
    /// - Rim Assist: small, distance-based nudge toward center near the mouth.
    /// - Coyote Catch: brief grace period after exit to grab near-misses.
    /// - End-of-frame Scoop: one overlap sweep to catch edge cases due to order.
    /// - Smart priority: prefers non-bombs; bombs only if unavoidable (shield respected).
    /// - Picks only the top N fruits this frame (no full sort).
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

        // ---------- runtime ----------
        private Collider2D _zone;
        private readonly HashSet<Fruit> _inside = new();              // who is currently inside the trigger
        private readonly Dictionary<Fruit, float> _lastSeen = new();  // last time we saw the fruit (for coyote)

        private ContactFilter2D _filter;
        private readonly Collider2D[] _overlapBuf = new Collider2D[32];

        // Cached references for micro-optimizations
        private Transform _tr;

        // --------- static scratch buffers (shared, low-GC) ----------
        // Candidate buffer we fill each frame (expanded on demand, rarely).
        private static Fruit[] s_candidates = new Fruit[64];
        private static int s_candidateCount;

        // Small helper list to iterate a HashSet without allocation
        private static readonly List<Fruit> s_insideList = new(64);

        void Awake()
        {
            _tr = transform;
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

        // --- Trigger tracking + Rim Assist (only while inside) ---
        void OnTriggerEnter2D(Collider2D other)
        {
            var f = FruitFrom(other);
            if (!f) return;
            _inside.Add(f);
            _lastSeen[f] = Time.unscaledTime; // presence timestamp
        }

        void OnTriggerStay2D(Collider2D other)
        {
            var f = FruitFrom(other);
            if (!f) return;

            _inside.Add(f);
            _lastSeen[f] = Time.unscaledTime;

            // Gentle rim assist toward center (edible fruit only)
            if (assistSpeed > 0f && f.IsCatchable)
            {
                Vector2 c = _tr.position;
                Vector2 pos = f.Position2D;
                float dist = Vector2.Distance(c, pos);

                float r0 = assistInnerRadius;
                // fade 1.0 at inner radius -> 0.0 at 2x radius
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
            _lastSeen[f] = Time.unscaledTime; // track for coyote window
        }

        void LateUpdate()
        {
            // Reset candidate buffer counter
            s_candidateCount = 0;

            // Snapshot center & scoop radius once
            Vector2 center = _tr.position;
            float scoopR = Mathf.Max(assistInnerRadius, 0.01f) + scoopInflation;

            // 1) inside now
            if (_inside.Count > 0)
            {
                s_insideList.Clear();
                foreach (var f in _inside) s_insideList.Add(f); // copy to avoid "modified during iteration" edge cases
                for (int i = 0; i < s_insideList.Count; i++)
                {
                    var f = s_insideList[i];
                    if (f && f.isActiveAndEnabled) AddCandidate(f);
                }
            }

            // 2) coyote window fruits
            if (coyoteSeconds > 0f && _lastSeen.Count > 0)
            {
                float now = Time.unscaledTime;
                // Iterate dictionary without LINQ to avoid GC
                foreach (var kv in _lastSeen)
                {
                    var f = kv.Key;
                    if (!f || !f.isActiveAndEnabled) continue;
                    float dt = now - kv.Value;
                    if (dt > 0f && dt <= coyoteSeconds) AddCandidate(f);
                }
            }

            // 3) scoop near misses (single overlap pass)
            int found = _zone.Overlap(_filter, _overlapBuf);
            for (int i = 0; i < found; i++)
            {
                var f = FruitFrom(_overlapBuf[i]);
                if (!f || !f.isActiveAndEnabled) continue;
                if (Vector2.Distance(f.Position2D, center) <= scoopR) AddCandidate(f);
            }

            if (s_candidateCount == 0) return;

            // Deduplicate in-place: turn duplicates into nulls (O(n))
            // This is cheaper than HashSet when the count is small-to-medium.
            InPlaceDedupe(s_candidates, ref s_candidateCount);

            // Pick up to N best fruits without sorting the entire array.
            int taken = 0;
            float centerX = center.x; // cache scalar used in distance calc
            float centerY = center.y;

            while (taken < maxCatchPerFrame && s_candidateCount > 0)
            {
                int bestIdx = -1;
                Fruit best = null;
                float bestBombBias = float.MaxValue; // 0 for non-bomb, 1 for bomb (prefer lower)
                float bestDist2 = float.MaxValue;

                // One linear pass to find the next best candidate
                for (int i = 0; i < s_candidateCount; i++)
                {
                    var f = s_candidates[i];
                    if (!f) continue;

                    // prefer non-bombs
                    float bombBias = f.IsBomb ? 1f : 0f;

                    // squared distance to center
                    Vector2 fp = f.Position2D;
                    float dx = fp.x - centerX;
                    float dy = fp.y - centerY;
                    float dist2 = dx * dx + dy * dy;

                    // lexicographic: bombBias (0 before 1) then distance
                    if (bombBias < bestBombBias || (Mathf.Approximately(bombBias, bestBombBias) && dist2 < bestDist2))
                    {
                        best = f; bestIdx = i; bestBombBias = bombBias; bestDist2 = dist2;
                    }
                }

                if (best == null) break;

                // Remove the chosen one by compacting the array (swap-last, pop)
                s_candidates[bestIdx] = s_candidates[s_candidateCount - 1];
                s_candidates[s_candidateCount - 1] = null;
                s_candidateCount--;

                // Apply final decision respecting shield for bombs
                if (best.IsBomb)
                {
                    if (PowerupManager.ConsumeShieldIfActive())
                    {
                        // Shield consumed: safe catch (no bomb penalty)
                        best.RaiseCaughtSafe();
                    }
                    else
                    {
                        best.RaiseCaughtBomb(); // normal bomb path
                    }
                }
                else
                {
                    best.RaiseCaughtFruit();   // normal fruit
                }

                _inside.Remove(best);
                _lastSeen.Remove(best);
                taken++;
            }

            if (taken > 0 && shaker) shaker.Shake(shakeAmp, shakeDur);
        }

        // ---------- helpers ----------
        static Fruit FruitFrom(Collider2D other)
        {
            if (!other) return null;
            other.TryGetComponent<Fruit>(out var f);
            return f;
        }

        /// <summary>Adds a fruit to the candidate buffer, growing if needed.</summary>
        static void AddCandidate(Fruit f)
        {
            if (!f) return;
            if (s_candidateCount >= s_candidates.Length)
            {
                // grow by x1.5 (rare)
                int newLen = Mathf.CeilToInt(s_candidates.Length * 1.5f);
                if (newLen <= s_candidates.Length) newLen = s_candidates.Length + 32;
                System.Array.Resize(ref s_candidates, newLen);
            }
            s_candidates[s_candidateCount++] = f;
        }

        /// <summary>Remove duplicates in-place; preserves first occurrence order.</summary>
        static void InPlaceDedupe(Fruit[] arr, ref int count)
        {
            if (count <= 1) return;

            // Use a small HashSet for seen; with typical counts this is very cheap.
            // We avoid allocating every frame by using a static; but static HashSet
            // would need clear anyway. Because counts are small, a quadratic check
            // is actually cheaper and completely allocation-free.
            int w = 0;
            for (int i = 0; i < count; i++)
            {
                var cur = arr[i];
                if (cur == null) continue;

                bool dup = false;
                for (int j = 0; j < w; j++)
                {
                    if (ReferenceEquals(arr[j], cur)) { dup = true; break; }
                }
                if (!dup) arr[w++] = cur;
            }
            // clear trailing (so GC can collect when fruits are destroyed)
            for (int k = w; k < count; k++) arr[k] = null;
            count = w;
        }
    }
}
