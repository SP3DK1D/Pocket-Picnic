using UnityEngine;
using System.Collections.Generic;
// Alias Unity's Random to avoid clashes with System.Random
using URandom = UnityEngine.Random;

namespace CatchTheFruit
{
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class Fruit : MonoBehaviour
    {
        /// <summary>Global registry of active fruits (fast ClearScreen / pooling-friendly).</summary>
        public static readonly HashSet<Fruit> Active = new();

        [Header("Runtime (assigned by spawner)")]
        public FruitData data;      // what this fruit is
        public float fallSpeed; // world units per second

        float _groundY;

        void OnEnable() { Active.Add(this); }
        void OnDisable() { Active.Remove(this); }

        /// <summary>Called by spawner right after spawning.</summary>
        public void Init(FruitData fd, float speedMultiplier, float groundY)
        {
            data = fd;
            _groundY = groundY;

            var sr = GetComponent<SpriteRenderer>();
            sr.sprite = fd.sprite;
            sr.color = fd.tint;

            // Use UnityEngine.Random via alias (URandom) to avoid ambiguity
            fallSpeed = URandom.Range(fd.minFallSpeed, fd.maxFallSpeed) * speedMultiplier;

            // Normalize width ≈ 0.8 world units so all fruits look consistent.
            const float targetW = 0.8f;
            if (sr.sprite)
            {
                float w = sr.sprite.bounds.size.x;
                if (w > 0.0001f) transform.localScale = Vector3.one * (targetW / w);
            }

            name = $"Fruit_{fd.id}";
        }

        void Update()
        {
            // Fall downward each frame.
            transform.position += Vector3.down * fallSpeed * Time.deltaTime;

            // MAGNET: pull ONLY non-bombs toward the player (bombs are unaffected).
            if (!data.isBomb && PowerupManager.MagnetActive && PowerupManager.PlayerTransform)
            {
                Vector3 to = PowerupManager.PlayerTransform.position - transform.position;
                float dist = to.magnitude;
                float radius = PowerupManager.MagnetRadius;

                if (dist <= radius && dist > 0.001f)
                {
                    // Stronger pull when closer to player for a nicer curve.
                    float closeness = 1f - Mathf.Clamp01(dist / radius); // 0 far .. 1 near
                    float speed = PowerupManager.MagnetPullSpeed * (0.4f + 0.6f * closeness);

                    Vector3 step = to.normalized * speed * Time.deltaTime;

                    // Prevent overshooting the player center in one frame.
                    if (step.sqrMagnitude > to.sqrMagnitude) step = to;

                    transform.position += step;
                }
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            // Notify systems.
            GameEvents.RaiseFruitCaught(data.id, data.scoreValue, data.isBomb);

            // If this fruit grants a power-up, announce it.
            if (data.powerup)
                GameEvents.RaisePowerupPicked(data.powerup);
            if (data.isBomb) VFXManager.Instance?.PlayBombExplosion(transform.position);

            Destroy(gameObject);
        }

        void LateUpdate()
        {
            // If it hits the ground line, it's a miss.
            if (transform.position.y <= _groundY)
            {
                bool isPowerup = data.powerup != null;
                GameEvents.RaiseFruitMissed(data.id, data.isBomb, isPowerup);
                Destroy(gameObject);
            }
        }
    }
}

