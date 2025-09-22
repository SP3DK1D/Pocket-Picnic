using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static CatchTheFruit.PowerupDef;

namespace CatchTheFruit
{
    /// <summary>
    /// Central handler for all power-ups.
    /// Works with minimal PowerupDef (kind + duration). The rest come from Inspector defaults.
    /// Freeze, Score×, Magnet, Shield, Clear are supported.
    /// </summary>
    public class PowerupManager : MonoBehaviour
    {
        public static PowerupManager Instance { get; private set; }

        // -------- Static state other systems read (Fruit, UI, etc.) --------
        public static Transform PlayerTransform { get; private set; }

        // Magnet
        public static bool MagnetActive { get; private set; }
        public static float MagnetRadius { get; private set; }
        public static float MagnetPullSpeed { get; private set; }

        // Shield
        public static bool ShieldActive { get; private set; }

        // -------- Inspector defaults (used if def lacks params) --------
        [Header("Freeze (TimeScale) Defaults")]
        [Range(0.05f, 1f)][SerializeField] float defaultFreezeScale = 0.20f;
        [Min(0.1f)][SerializeField] float defaultFreezeDur = 2.5f;

        [Header("Score× Defaults")]
        [Min(1f)][SerializeField] float defaultScoreMul = 2f;
        [Min(0.1f)][SerializeField] float defaultScoreDur = 7f;

        [Header("Magnet Defaults")]
        [Min(0.1f)][SerializeField] float defaultMagnetRadius = 6f;
        [Min(0.1f)][SerializeField] float defaultMagnetPull = 22f; // world u/s^2 used as Δv in FixedUpdate
        [Min(0.1f)][SerializeField] float defaultMagnetDur = 7f;

        [Header("Shield Defaults")]
        [Tooltip("0 = until consumed; >0 = time-limited")]
        [Min(0f)][SerializeField] float defaultShieldDur = 0f;

        // -------- internals --------
        float _preFreezeScale = 1f;
        bool _freezeActive;

        PowerupDef _shieldDef;
        bool _shieldTimed;

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // auto-bind player by tag
            if (!PlayerTransform)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                PlayerTransform = go ? go.transform : null;
            }
        }

        void OnEnable()
        {
            GameEvents.OnPowerupPicked += HandlePicked;
            GameEvents.OnGameStart += HandleGameStart;
            GameEvents.OnGameOver += HandleGameOver;
        }

        void OnDisable()
        {
            GameEvents.OnPowerupPicked -= HandlePicked;
            GameEvents.OnGameStart -= HandleGameStart;
            GameEvents.OnGameOver -= HandleGameOver;

            StopAllCoroutines();
            EndAllImmediate();
        }

        // ---------- lifecycle guards ----------
        void HandleGameStart()
        {
            if (!PlayerTransform)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                PlayerTransform = go ? go.transform : null;
            }
            EndAllImmediate();
        }

        void HandleGameOver() => EndAllImmediate();

        void EndAllImmediate()
        {
            StopAllCoroutines();

            if (_freezeActive)
            {
                Time.timeScale = 1f;
                _freezeActive = false;
            }

            MagnetActive = false;

            if (ShieldActive && _shieldDef != null)
                GameEvents.RaisePowerupEnded(_shieldDef);
            ShieldActive = false;
            _shieldDef = null;
            _shieldTimed = false;
        }

        // ---------- entry point from Fruit ----------
        void HandlePicked(PowerupDef def)
        {
            if (!def) return;

            switch (def.kind)
            {
                case PowerupKind.TimeScale: StartCoroutine(RunFreeze(def)); break;
                case PowerupKind.ScoreMultiplier: StartCoroutine(RunScoreMultiplier(def)); break;
                case PowerupKind.Magnet: StartCoroutine(RunMagnet(def)); break;
                case PowerupKind.Shield: RunShield(def); break;
                case PowerupKind.ClearScreen: StartCoroutine(RunClear(def)); break;
            }
        }

        // ---------------- Freeze ----------------
        IEnumerator RunFreeze(PowerupDef def)
        {
            float dur = (def.duration > 0f) ? def.duration : defaultFreezeDur;
            float scale = defaultFreezeScale; // we don't depend on def.timeScale

            GameEvents.RaisePowerupStarted(def);

            _preFreezeScale = (Time.timeScale <= 0f) ? 1f : Time.timeScale;
            _freezeActive = true;
            Time.timeScale = scale;

            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime; // ignore timeScale
                yield return null;
            }

            if (_freezeActive) Time.timeScale = 1f;
            _freezeActive = false;

            GameEvents.RaisePowerupEnded(def);
        }

        // ---------------- Score Multiplier ----------------
        IEnumerator RunScoreMultiplier(PowerupDef def)
        {
            float dur = (def.duration > 0f) ? def.duration : defaultScoreDur;

            GameEvents.RaisePowerupStarted(def);
            // ScoreManager should listen to Started/Ended to apply/remove the multiplier itself.

            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            GameEvents.RaisePowerupEnded(def);
        }

        // ---------------- Magnet ----------------
        IEnumerator RunMagnet(PowerupDef def)
        {
            float dur = (def.duration > 0f) ? def.duration : defaultMagnetDur;

            MagnetRadius = Mathf.Max(0.01f, defaultMagnetRadius);
            MagnetPullSpeed = Mathf.Max(0.01f, defaultMagnetPull);
            MagnetActive = true;

            GameEvents.RaisePowerupStarted(def);

            float t = 0f;
            while (t < dur)
            {
                if (!PlayerTransform)
                {
                    var go = GameObject.FindGameObjectWithTag("Player");
                    PlayerTransform = go ? go.transform : null;
                }
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            MagnetActive = false;
            GameEvents.RaisePowerupEnded(def);
        }

        // ---------------- Shield ----------------
        void RunShield(PowerupDef def)
        {
            ShieldActive = true;
            _shieldDef = def;

            float dur = (def.duration > 0f) ? def.duration : defaultShieldDur;
            _shieldTimed = dur > 0f;

            GameEvents.RaisePowerupStarted(def);

            if (_shieldTimed) StartCoroutine(ShieldTimer(dur));
        }

        IEnumerator ShieldTimer(float duration)
        {
            float t = 0f;
            while (t < duration && ShieldActive)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            if (ShieldActive)
            {
                ShieldActive = false;
                if (_shieldDef != null) GameEvents.RaisePowerupEnded(_shieldDef);
                _shieldDef = null;
                _shieldTimed = false;
            }
        }

        /// <summary>
        /// Called by gameplay (e.g., LifeManager) when a bomb would cost a life.
        /// Returns true if shield absorbed it. If shield was "until-consumed", it turns off.
        /// </summary>
        public static bool ConsumeShield()
        {
            if (!ShieldActive || Instance == null) return false;

            if (!Instance._shieldTimed)
            {
                ShieldActive = false;
                if (Instance._shieldDef != null) GameEvents.RaisePowerupEnded(Instance._shieldDef);
                Instance._shieldDef = null;
            }
            return true;
        }

        // ---------------- Clear Screen ----------------
        IEnumerator RunClear(PowerupDef def)
        {
            GameEvents.RaisePowerupStarted(def);

            int total = 0;
            var snapshot = new List<Fruit>();

            if (Fruit.Active.Count > 0)
            {
                foreach (var f in Fruit.Active)
                    if (f) snapshot.Add(f);
            }
            else
            {
                var fruits = FindObjectsByType<Fruit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < fruits.Length; i++)
                    if (fruits[i]) snapshot.Add(fruits[i]);
            }

            // sum points (non-bomb)
            for (int i = 0; i < snapshot.Count; i++)
            {
                var f = snapshot[i];
                if (f && f.data && !f.data.isBomb) total += f.data.scoreValue;
            }

            // award once
            try { ScoreManager.Instance?.AddBulkPoints(total); } catch { }

            // retire all (no extra triggers)
            for (int i = 0; i < snapshot.Count; i++)
            {
                var f = snapshot[i];
                if (f) f.Retire();
            }

            yield return null;
            GameEvents.RaisePowerupEnded(def);
        }
    }
}
