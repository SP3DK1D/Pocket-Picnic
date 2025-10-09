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

        float _groundY;

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

        void OnEnable() { Active.Add(this); }
        void OnDisable() { Active.Remove(this); }

        public void Init(FruitData fd, float speedMultiplier, float groundY) =>
            Init(fd, speedMultiplier, groundY, false);

        public void Init(FruitData fd, float speedMultiplier, float groundY, bool decorative)
        {
            data = fd;
            _groundY = groundY;
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

            // Random tumble
            _tumbleDir = (URandom.value < 0.5f) ? -1 : 1;
            _tumbleSpeed = URandom.Range(35f, 90f);

            // Normalize to width ≈ 0.8 world units
            const float targetW = 0.8f;
            if (_sr && _sr.sprite)
            {
                float w = _sr.sprite.bounds.size.x;
                if (w > 0.0001f) transform.localScale = Vector3.one * (targetW / w);
            }
            else
            {
                // PATCH: safe default scale when no sprite is set (prevents odd sizes)
                transform.localScale = Vector3.one; // PATCH
            }

            name = fd ? $"Fruit_{fd.id}" : "Fruit";
        }

        void Update()
        {
            // PATCH: Apply global freeze multiplier so Freeze always slows enough
            float freezeMul = PowerupManager.FreezeSpeedMul; // 1 when not freezing  // PATCH
            transform.position += Vector3.down * (fallSpeed * freezeMul) * Time.deltaTime; // PATCH

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
                Retire();
            }
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
    }
}
