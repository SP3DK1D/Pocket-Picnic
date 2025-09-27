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

        float _groundY;

        // tumble
        float _tumbleSpeed;  // deg/sec
        int _tumbleDir;    // +1 or -1

        void OnEnable() { Active.Add(this); }
        void OnDisable() { Active.Remove(this); }

        public void Init(FruitData fd, float speedMultiplier, float groundY) =>
            Init(fd, speedMultiplier, groundY, false);

        public void Init(FruitData fd, float speedMultiplier, float groundY, bool decorative)
        {
            data = fd;
            _groundY = groundY;
            this.decorative = decorative;

            var sr = GetComponent<SpriteRenderer>();
            if (fd != null)
            {
                sr.sprite = fd.sprite;
                sr.color = fd.tint;
            }

            // Fall speed with a solid floor
            float min = (fd != null) ? Mathf.Max(6f, fd.minFallSpeed) : 6f;
            float max = (fd != null) ? Mathf.Max(min + 3f, fd.maxFallSpeed) : (min + 4f);
            float mul = Mathf.Max(0.5f, speedMultiplier);
            fallSpeed = Mathf.Max(3.2f, URandom.Range(min, max) * mul);

            // Random tumble
            _tumbleDir = (URandom.value < 0.5f) ? -1 : 1;
            _tumbleSpeed = URandom.Range(35f, 90f);

            // Normalize to width ≈ 0.8 world units
            const float targetW = 0.8f;
            if (sr.sprite)
            {
                float w = sr.sprite.bounds.size.x;
                if (w > 0.0001f) transform.localScale = Vector3.one * (targetW / w);
            }

            name = fd ? $"Fruit_{fd.id}" : "Fruit";
        }

        void Update()
        {
            transform.position += Vector3.down * fallSpeed * Time.deltaTime;
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
            if (!decorative && transform.position.y <= _groundY)
            {
                bool isPowerup = (data != null && data.powerup != null);
                bool isBomb = (data != null && data.isBomb);
                GameEvents.RaiseFruitMissed(data?.id ?? "?", isBomb, isPowerup);
                Destroy(gameObject);
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            if (decorative) { Destroy(gameObject); return; }

            bool isBomb = (data != null && data.isBomb);
            int score = (data != null) ? data.scoreValue : 0;

            GameEvents.RaiseFruitCaught(data?.id ?? "?", score, isBomb);

            if (data != null && data.powerup != null)
                GameEvents.RaisePowerupPicked(data.powerup);

            if (isBomb)
                VFXManager.Instance?.PlayBombExplosion(transform.position);

            Destroy(gameObject);
        }

        public void Retire()
        {
            if (this) Destroy(gameObject);
        }
    }
}
