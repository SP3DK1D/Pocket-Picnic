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

        // Convenience flags (kept for compatibility with other systems)
        public bool IsBomb => data != null && data.isBomb;
        public bool IsPowerupCarrier => data != null && data.powerup != null;

        // === Legacy compatibility for BasketCatchZone ===
        public bool IsCatchable => !decorative && !IsBomb;
        public Vector2 Position2D => transform.position;   // <- ADDED

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

            // Base fall speed from data
            float min = (fd != null) ? Mathf.Max(6f, fd.minFallSpeed) : 6f;
            float max = (fd != null) ? Mathf.Max(min + 3f, fd.maxFallSpeed) : (min + 4f);
            float mul = Mathf.Max(0.5f, speedMultiplier);

            // Progressive ramp + per-fruit variance
            float timeRamp = DifficultyManager.HasCurrent ? DifficultyManager.FallSpeedRamp() : 1f;
            float baseSpeed = URandom.Range(min, max) * mul * timeRamp;
            fallSpeed = Mathf.Max(3.2f, baseSpeed * URandom.Range(0.90f, 1.15f));

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
                Retire();
            }
        }

        // ----- Trigger path (if basket collider listens via physics) -----
        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            if (decorative) { Retire(); return; }
            HandleAutoCaught();
        }

        // ===== Public API (compat for BasketCatchZone) =====

        /// <summary>Legacy helper used by BasketCatchZone to gently pull fruit.</summary>
        public void Nudge(Vector2 delta)
        {
            if (decorative) return;
            transform.position += (Vector3)delta;
        }

        /// <summary>Force a normal (non-bomb) catch.</summary>
        public void RaiseCaughtFruit()
        {
            if (data != null && data.id == "coin") { DoCoinCatch(); return; }
            DoNormalCatch(isFromShield: false);
        }

        /// <summary>Force a safe catch (e.g., bomb blocked by shield).</summary>
        public void RaiseCaughtSafe()
        {
            if (data != null && data.id == "coin") { DoCoinCatch(); return; }
            DoNormalCatch(isFromShield: true);
        }

        /// <summary>Force a bomb catch (no shield involved).</summary>
        public void RaiseCaughtBomb()
        {
            DoBombCatch();
        }

        // ===== Internal catch handling =====

        void HandleAutoCaught()
        {
            // Coin fruit bypass
            if (data != null && data.id == "coin") { DoCoinCatch(); return; }

            if (IsBomb)
            {
                // Try to consume shield
                if (PowerupManager.ConsumeShieldIfActive())
                {
                    AudioHub.I?.PlayShieldBreak();
                    DoNormalCatch(isFromShield: true);
                    return;
                }

                // No shield
                DoBombCatch();
                return;
            }

            // Normal fruit
            DoNormalCatch(isFromShield: false);
        }

        void DoCoinCatch()
        {
            QuestManager.Instance?.AddCoins(1);
            AudioHub.I?.PlayPickup();
            GameEvents.RaiseFruitCaught(data?.id ?? "coin", 0, false);
            Retire();
        }

        void DoNormalCatch(bool isFromShield)
        {
            int score = (data != null) ? data.scoreValue : 0;
            GameEvents.RaiseFruitCaught(data?.id ?? "?", score, false);

            if (IsPowerupCarrier)
                GameEvents.RaisePowerupPicked(data.powerup);

            AudioHub.I?.PlayPickup();
            Retire();
        }

        void DoBombCatch()
        {
            int score = (data != null) ? data.scoreValue : 0;
            GameEvents.RaiseFruitCaught(data?.id ?? "?", score, true);
            if (IsPowerupCarrier)
                GameEvents.RaisePowerupPicked(data.powerup);
            VFXManager.Instance?.PlayBombExplosion(transform.position);
            Retire();
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
