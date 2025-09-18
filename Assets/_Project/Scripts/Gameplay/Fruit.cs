using UnityEngine;
using System.Collections.Generic;
using URandom = UnityEngine.Random;

namespace CatchTheFruit
{
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class Fruit : MonoBehaviour
    {
        public static readonly HashSet<Fruit> Active = new();

        [Header("Runtime (assigned by spawner)")]
        public FruitData data;
        public float fallSpeed; // legacy/debug only

        [Header("Physics Feel")]
        [Min(0f)] public float initialDownBoost = 0f;
        [Min(0f)] public float maxFallSpeed = 10f;
        [Min(0f)] public float gravityScaleOverride = 0f;

        [Header("Tumbling (randomized per instance)")]
        [Tooltip("Angular velocity range (deg/sec). A random value in this range is chosen.")]
        public Vector2 angularVelRange = new Vector2(80f, 220f);

        [Tooltip("Extra impulse torque range (N·m). Adds a one-time twist at spawn.")]
        public Vector2 torqueImpulseRange = new Vector2(0.02f, 0.08f);

        [Tooltip("Per-instance angular drag range. Higher = spin slows down sooner.")]
        public Vector2 angularDragRange = new Vector2(0.12f, 0.28f);

        [Tooltip("Randomize initial Z rotation 0..360 so sprites don't align.")]
        public bool randomizeInitialRotation = true;

        float _groundY;
        Rigidbody2D _rb;
        Collider2D _col;
        SpriteRenderer _sr;

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _col = GetComponent<Collider2D>();
            _sr = GetComponent<SpriteRenderer>();

            _col.isTrigger = true;

            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            // allow rotation for tumbling
            _rb.freezeRotation = false;
        }

        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);

        public void Init(FruitData fd, float speedMultiplier, float groundY)
        {
            data = fd;
            _groundY = groundY;

            if (_sr)
            {
                _sr.sprite = fd.sprite;
                _sr.color = fd.tint;

                const float targetW = 0.8f;
                if (_sr.sprite)
                {
                    float w = _sr.sprite.bounds.size.x;
                    if (w > 0.0001f) transform.localScale = Vector3.one * (targetW / w);
                }
            }

            fallSpeed = URandom.Range(fd.minFallSpeed, fd.maxFallSpeed) * speedMultiplier;

            if (gravityScaleOverride > 0f)
                _rb.gravityScale = gravityScaleOverride;

            // Difficulty overrides (if present)
            if (DifficultyManager.HasCurrent)
            {
                var s = DifficultyManager.Current;
                if (s.gravityScale > 0f) _rb.gravityScale = s.gravityScale;
                if (s.maxFallSpeed > 0f) maxFallSpeed = s.maxFallSpeed;
                if (s.initialDownBoost > 0f) _rb.linearVelocity = new Vector2(0f, -s.initialDownBoost);
            }
            else if (initialDownBoost > 0f)
            {
                _rb.linearVelocity = new Vector2(0f, -initialDownBoost);
            }

            // --- Unique tumble per instance ---
            if (randomizeInitialRotation)
            {
                float z = URandom.Range(0f, 360f);
                transform.rotation = Quaternion.Euler(0f, 0f, z);
            }

            // Angular drag randomization
            _rb.angularDamping = Mathf.Clamp(URandom.Range(angularDragRange.x, angularDragRange.y), 0f, 10f);

            // Set angular velocity (deg/sec) with random direction
            float angMag = URandom.Range(Mathf.Min(angularVelRange.x, angularVelRange.y),
                                         Mathf.Max(angularVelRange.x, angularVelRange.y));
            float sign = (URandom.value < 0.5f) ? -1f : 1f; // clockwise negative by convention
            _rb.angularVelocity = sign * angMag;

            // Add a small one-time torque impulse for extra variation
            float torque = URandom.Range(torqueImpulseRange.x, torqueImpulseRange.y) * ((URandom.value < 0.5f) ? -1f : 1f);
            _rb.AddTorque(torque, ForceMode2D.Impulse);

            name = $"Fruit_{fd.id}";
        }

        void FixedUpdate()
        {
            // Magnet pulls non-bombs only
            if (!data.isBomb && PowerupManager.MagnetActive && PowerupManager.PlayerTransform)
            {
                Vector2 to = (Vector2)PowerupManager.PlayerTransform.position - _rb.position;
                float dist = to.magnitude;
                float radius = PowerupManager.MagnetRadius;

                if (dist <= radius && dist > 0.001f)
                {
                    float closeness = 1f - Mathf.Clamp01(dist / radius);
                    float accel = PowerupManager.MagnetPullSpeed * (0.4f + 0.6f * closeness);
                    _rb.AddForce(to.normalized * accel, ForceMode2D.Force);
                }
            }

            // Terminal velocity cap
            if (maxFallSpeed > 0f && _rb.linearVelocity.y < -maxFallSpeed)
            {
                var v = _rb.linearVelocity;
                v.y = -maxFallSpeed;
                _rb.linearVelocity = v;
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            GameEvents.RaiseFruitCaught(data.id, data.scoreValue, data.isBomb);

            if (data.powerup) GameEvents.RaisePowerupPicked(data.powerup);
            if (data.isBomb) VFXManager.Instance?.PlayBombExplosion(transform.position);

            Destroy(gameObject);
        }

        void LateUpdate()
        {
            if (transform.position.y <= _groundY)
            {
                bool isPowerup = data.powerup != null;
                GameEvents.RaiseFruitMissed(data.id, data.isBomb, isPowerup);
                Destroy(gameObject);
            }
        }
    }
}
