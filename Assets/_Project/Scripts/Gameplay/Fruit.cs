using UnityEngine;
using System.Collections.Generic;
// Explicit alias so we never collide with System.Random
using URandom = UnityEngine.Random;

namespace CatchTheFruit
{
    /// <summary>
    /// A single falling fruit.
    /// - Physics-driven fall (Rigidbody2D)
    /// - Random tumbling (per-instance)
    /// - Magnet homing when active (PowerupManager)
    /// - Safe despawn via Retire() for catch/miss/clear (pooling friendly)
    /// - Pool reset in Init() (re-enables collider/physics/sprite)
    /// Optional integrations:
    /// - DifficultyManager for gravity/terminal settings
    /// - ChallengeDirector for GoldenTime bonus/tint on catch
    /// - FruitPool for recycling
    /// - VFXManager for bomb pop
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class Fruit : MonoBehaviour
    {
        /// <summary>Registry of currently active fruits (fast board clears).</summary>
        public static readonly HashSet<Fruit> Active = new();

        [Header("Runtime (assigned by spawner)")]
        public FruitData data;          // what this fruit is
        public float fallSpeed;     // debugging only (physics drives actual fall)

        [Header("Physics Feel")]
        [Tooltip("Initial downward velocity given at spawn (world u/s).")]
        [Min(0f)] public float initialDownBoost = 0f;
        [Tooltip("Terminal fall speed cap (world u/s).")]
        [Min(0f)] public float maxFallSpeed = 10f;
        [Tooltip("If > 0, overrides Rigidbody2D.gravityScale at init.")]
        [Min(0f)] public float gravityScaleOverride = 0f;

        [Header("Tumbling (randomized per instance)")]
        public Vector2 angularVelRange = new Vector2(80f, 220f);    // deg/s
        public Vector2 torqueImpulseRange = new Vector2(0.02f, 0.08f); // N·m
        public Vector2 angularDragRange = new Vector2(0.12f, 0.28f);
        public bool randomizeInitialRotation = true;

        // --------- Private ---------
        float _groundY;
        Rigidbody2D _rb;
        Collider2D _col;
        SpriteRenderer _sr;
        bool _retiring; // once set, ignores further logic/physics

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _col = GetComponent<Collider2D>();
            _sr = GetComponent<SpriteRenderer>();

            // sensible defaults (you can still tune in inspector)
            _col.isTrigger = true;
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            _rb.freezeRotation = false;
        }

        void OnEnable() { _retiring = false; Active.Add(this); }
        void OnDisable() { Active.Remove(this); }

        /// <summary>Called by spawner after instantiation (or recycle) to re-seed state.</summary>
        public void Init(FruitData fd, float speedMultiplier, float groundY)
        {
            data = fd;
            _groundY = groundY;

            // ----- POOL RESET (critical!) -----
            if (_col) _col.enabled = true;       // re-enable trigger collider (prevents "ghost" fruits)
            if (_rb) _rb.simulated = true;      // re-enable physics
            if (_sr) _sr.enabled = true;      // in case Retire hid it elsewhere

            // ----- Visuals -----
            if (_sr)
            {
                _sr.sprite = fd.sprite;
                _sr.color = fd.tint;

                // Normalize width ≈ 0.8 world units so they feel consistent size
                const float targetW = 0.8f;
                if (_sr.sprite)
                {
                    float w = _sr.sprite.bounds.size.x;
                    if (w > 0.0001f) transform.localScale = Vector3.one * (targetW / w);
                }
            }

            // Keep for debugging reference
            fallSpeed = URandom.Range(fd.minFallSpeed, fd.maxFallSpeed) * speedMultiplier;

            // ----- Physics seed -----
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;

            if (gravityScaleOverride > 0f) _rb.gravityScale = gravityScaleOverride;

            if (DifficultyManager.HasCurrent)
            {
                var d = DifficultyManager.Current;
                if (d.gravityScale > 0f) _rb.gravityScale = d.gravityScale;
                if (d.maxFallSpeed > 0f) maxFallSpeed = d.maxFallSpeed;
                if (d.initialDownBoost > 0f) _rb.linearVelocity = new Vector2(0f, -d.initialDownBoost);
            }
            else if (initialDownBoost > 0f)
            {
                _rb.linearVelocity = new Vector2(0f, -initialDownBoost);
            }

            if (randomizeInitialRotation)
            {
                float z = URandom.Range(0f, 360f);
                transform.rotation = Quaternion.Euler(0f, 0f, z);
            }

            _rb.angularDamping = Mathf.Clamp(URandom.Range(angularDragRange.x, angularDragRange.y), 0f, 10f);

            float angMag = URandom.Range(Mathf.Min(angularVelRange.x, angularVelRange.y),
                                         Mathf.Max(angularVelRange.x, angularVelRange.y));
            float sign = (URandom.value < 0.5f) ? -1f : 1f;
            _rb.angularVelocity = sign * angMag;

            float torque = URandom.Range(torqueImpulseRange.x, torqueImpulseRange.y) * ((URandom.value < 0.5f) ? -1f : 1f);
            _rb.AddTorque(torque, ForceMode2D.Impulse);

            name = $"Fruit_{fd.id}";
        }

        void FixedUpdate()
        {
            if (_retiring) return;

            // ---------- Magnet homing (fruits only; bombs are ignored) ----------
            if (!data.isBomb && PowerupManager.MagnetActive && PowerupManager.PlayerTransform)
            {
                Vector2 to = (Vector2)PowerupManager.PlayerTransform.position - _rb.position;
                float dist = to.magnitude;
                float radius = Mathf.Max(0.01f, PowerupManager.MagnetRadius);

                if (dist <= radius && dist > 0.001f)
                {
                    // Smooth curve: stronger pull when closer, mass-independent (Δv)
                    float t = 1f - Mathf.Clamp01(dist / radius);  // 0 far .. 1 near
                    float accelPerSec = Mathf.Max(0.01f, PowerupManager.MagnetPullSpeed);
                    Vector2 dv = to.normalized * accelPerSec * (0.35f + 0.85f * t * t) * Time.fixedDeltaTime;

                    _rb.linearVelocity += dv;

                    // Cap homing speed so it doesn't look teleporty
                    const float maxHome = 14f;
                    if (_rb.linearVelocity.magnitude > maxHome)
                        _rb.linearVelocity = _rb.linearVelocity.normalized * maxHome;
                }
            }

            // ---------- Terminal velocity cap ----------
            if (maxFallSpeed > 0f && _rb.linearVelocity.y < -maxFallSpeed)
            {
                var v = _rb.linearVelocity; v.y = -maxFallSpeed;
                _rb.linearVelocity = v;
            }
        }

        void LateUpdate()
        {
            if (_retiring) return;

            // Ground line miss
            if (transform.position.y <= _groundY)
            {
                bool isPowerup = data.powerup != null;
                GameEvents.RaiseFruitMissed(data.id, data.isBomb, isPowerup);
                Retire();
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (_retiring || !other.CompareTag("Player")) return;

            // Score / events
            GameEvents.RaiseFruitCaught(data.id, data.scoreValue, data.isBomb);

            // Power-up pick
            if (data.powerup) GameEvents.RaisePowerupPicked(data.powerup);

            // Optional VFX for bombs
            if (data.isBomb) VFXManager.Instance?.PlayBombExplosion(transform.position);

            // Optional: GoldenTime instant bonus/tint at catch moment
            var cd = Object.FindFirstObjectByType<ChallengeDirector>();
            if (cd && _sr)
            {
                int bonus = cd.GoldenCatchBonus(data, out Color? golden);
                if (golden.HasValue) _sr.color = golden.Value;
                if (bonus > 0) ScoreManager.Instance?.AddBulkPoints(bonus);
            }

            Retire();
        }

        /// <summary>
        /// Safe despawn: disables collider & physics then recycles/destroys.
        /// Prevents mid-frame accesses (e.g., magnet/physics) from touching a destroyed object.
        /// </summary>
        public void Retire()
        {
            if (_retiring) return;
            _retiring = true;

            if (_col) _col.enabled = false;
            if (_rb)
            {
                _rb.linearVelocity = Vector2.zero;
                _rb.angularVelocity = 0f;
                _rb.simulated = false; // stops FixedUpdate physics immediately
            }

            if (FruitPool.Instance) FruitPool.Instance.Recycle(this);
            else Destroy(gameObject);
        }
    }
}
