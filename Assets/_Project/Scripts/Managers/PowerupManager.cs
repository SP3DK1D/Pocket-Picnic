using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static CatchTheFruit.PowerupDef;

namespace CatchTheFruit
{
    /// <summary>
    /// Central power-up controller (safe version).
    /// Uses inspector defaults only and raises events for overlay/score/VFX/haptics.
    /// Freeze now slows falling by scaling Physics2D.gravity (not Time.timeScale),
    /// so player input/movement/UI remain identical during Freeze.
    /// </summary>
    public class PowerupManager : MonoBehaviour
    {
        // ===== Singleton =====
        public static PowerupManager Instance { get; private set; }

        // ===== Freeze visibility for debugging / guards =====
        public static bool FreezeActive { get; private set; }

        // ===== Magnet data read by Fruit.cs =====
        public static bool MagnetActive { get; private set; }
        public static float MagnetRadius { get; private set; }
        public static float MagnetPullSpeed { get; private set; }
        public static Transform PlayerTransform { get; private set; }

        // ===== Shield (static accessors) =====
        public static bool ShieldIsActive => Instance != null && Instance._shieldActive;
        public static bool ConsumeShieldIfActive() => Instance != null && Instance.TryConsumeShieldHit();

        [Header("Player (used by Magnet)")]
        [SerializeField] private Transform player; // auto-find by Tag=Player if null

        [Header("Freeze (GravityScale) — defaults")]
        [Tooltip("Gravity multiplier during Freeze (e.g., 0.2 = 20% of normal fall speed).")]
        [SerializeField, Range(0.01f, 1f)] private float freezeScale = 0.20f;
        [SerializeField, Min(0.1f)] private float freezeDuration = 2.5f;

        [Header("Score Multiplier — defaults")]
        [SerializeField, Min(1f)] private float scoreMultiplier = 2f;
        [SerializeField, Min(0.1f)] private float scoreMultDuration = 7f;

        [Header("Magnet — defaults")]
        [SerializeField, Min(0.1f)] private float magnetRadius = 5.5f;
        [SerializeField, Min(0.1f)] private float magnetPullSpeed = 12f;
        [SerializeField, Min(0.1f)] private float magnetDuration = 7f;

        [Header("Shield — defaults")]
        [SerializeField, Min(0f)] private float shieldDuration = 0f;  // 0 = until consumed

        [Header("Debug")]
        [SerializeField] private bool verboseLogs = false;

        // ===== Runtime =====
        Coroutine _freezeCo, _multCo, _magnetCo, _shieldCo;
        bool _shieldActive;
        float _shieldEndAt = 0f;

        // marks that EndShield() is happening due to a bomb hit (not timeout)
        bool _shieldConsumedByHit;

        // Store original global gravity so Freeze can restore it exactly
        Vector2 _origGravity2D;
        bool _origGravityCaptured = false;

        void Awake()
        {
            Instance = this;

            if (!player)
            {
                var pGo = GameObject.FindGameObjectWithTag("Player");
                if (pGo) player = pGo.transform;
            }
            PlayerTransform = player;

            // capture original gravity at boot
            _origGravity2D = Physics2D.gravity;
            _origGravityCaptured = true;
        }

        void OnEnable()
        {
            GameEvents.OnPowerupPicked += OnPicked;
            GameEvents.OnGameStart += OnStart;
            GameEvents.OnGameOver += OnOver;
        }

        void OnDisable()
        {
            GameEvents.OnPowerupPicked -= OnPicked;
            GameEvents.OnGameStart -= OnStart;
            GameEvents.OnGameOver -= OnOver;

            EndAllEffectsImmediate(); // restores gravity, ends powerups

            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (_shieldActive && _shieldEndAt > 0f && Time.unscaledTime >= _shieldEndAt)
                EndShield(); // ends by TIMEOUT
        }

        // ===== Event handlers =====
        void OnStart()
        {
            EndAllEffectsImmediate();
            PlayerTransform = player ? player : PlayerTransform;
        }

        void OnOver()
        {
            EndAllEffectsImmediate();
        }

        void OnPicked(PowerupDef def)
        {
            if (def == null) return;
            switch (def.kind)
            {
                case PowerupKind.TimeScale:       ActivateFreeze();          break; // name kept for compatibility
                case PowerupKind.ScoreMultiplier: ActivateScoreMultiplier(); break;
                case PowerupKind.Magnet:          ActivateMagnet();          break;
                case PowerupKind.Shield:          ActivateShield();          break;
                case PowerupKind.ClearScreen:     DoClearScreen();           break;
            }
        }

        // ===== Freeze (now scales Physics2D.gravity, not Time.timeScale) =====
        public void ActivateFreeze()
        {
            if (_freezeCo != null) StopCoroutine(_freezeCo);
            _freezeCo = StartCoroutine(CoFreeze());
        }

        IEnumerator CoFreeze()
        {
            float dur = Mathf.Max(0.1f, freezeDuration);
            float gMul = Mathf.Clamp(freezeScale, 0.01f, 1f);

            if (!_origGravityCaptured) { _origGravity2D = Physics2D.gravity; _origGravityCaptured = true; }

            // Begin slow fall
            FreezeActive = true;
            var defStart = ScriptableObject.CreateInstance<PowerupDef>(); defStart.kind = PowerupKind.TimeScale;
            GameEvents.RaisePowerupStarted(defStart);

            // Apply gravity scaling (affects dynamic bodies like fruits; kinematic player is unaffected)
            Physics2D.gravity = _origGravity2D * gMul;

            if (verboseLogs) Debug.Log($"[Freeze] Gravity scaled to {gMul:0.##}x for {dur:0.##}s");

            // unscaled timer so duration is real seconds regardless of global timeScale
            float t = 0f;
            while (t < dur)
            {
                if (PauseManager.Instance != null && PauseManager.Instance.IsPaused)
                { yield return null; continue; }
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            // End slow fall
            RestoreGravityIfNeeded();

            var defEnd = ScriptableObject.CreateInstance<PowerupDef>(); defEnd.kind = PowerupKind.TimeScale;
            GameEvents.RaisePowerupEnded(defEnd);

            FreezeActive = false;
            _freezeCo = null;
        }

        void RestoreGravityIfNeeded()
        {
            if (!_origGravityCaptured) return;
            Physics2D.gravity = _origGravity2D;
            if (verboseLogs) Debug.Log("[Freeze] Gravity restored to original.");
        }

        // ===== Score Multiplier =====
        void ActivateScoreMultiplier()
        {
            if (_multCo != null) StopCoroutine(_multCo);
            _multCo = StartCoroutine(CoScoreMultiplier());
        }

        IEnumerator CoScoreMultiplier()
        {
            float dur = Mathf.Max(0.1f, scoreMultDuration);

            var defStart = ScriptableObject.CreateInstance<PowerupDef>();
            defStart.kind = PowerupKind.ScoreMultiplier;
            defStart.scoreMultiplier = scoreMultiplier; // FYI for listeners
            GameEvents.RaisePowerupStarted(defStart);

            // SFX for 2× power-up start
            AudioHub.I?.PlayScoreMultiplier();

            float t = 0f;
            while (t < dur)
            {
                if (PauseManager.Instance != null && PauseManager.Instance.IsPaused)
                { yield return null; continue; }
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            var defEnd = ScriptableObject.CreateInstance<PowerupDef>();
            defEnd.kind = PowerupKind.ScoreMultiplier;
            GameEvents.RaisePowerupEnded(defEnd);
            _multCo = null;
        }

        // ===== Magnet =====
        void ActivateMagnet()
        {
            if (_magnetCo != null) StopCoroutine(_magnetCo);
            _magnetCo = StartCoroutine(CoMagnet());
        }

        IEnumerator CoMagnet()
        {
            MagnetRadius = Mathf.Max(0.1f, magnetRadius);
            MagnetPullSpeed = Mathf.Max(0.1f, magnetPullSpeed);
            PlayerTransform = player ? player : PlayerTransform;

            var defStart = ScriptableObject.CreateInstance<PowerupDef>(); defStart.kind = PowerupKind.Magnet;
            GameEvents.RaisePowerupStarted(defStart);

            MagnetActive = true;

            float dur = Mathf.Max(0.1f, magnetDuration);
            float t = 0f;
            while (t < dur)
            {
                if (PauseManager.Instance != null && PauseManager.Instance.IsPaused)
                { yield return null; continue; }
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            MagnetActive = false;

            var defEnd = ScriptableObject.CreateInstance<PowerupDef>(); defEnd.kind = PowerupKind.Magnet;
            GameEvents.RaisePowerupEnded(defEnd);
            _magnetCo = null;
        }

        // ===== Shield =====
        void ActivateShield()
        {
            if (_shieldCo != null) StopCoroutine(_shieldCo);
            _shieldCo = StartCoroutine(CoShield());
        }

        IEnumerator CoShield()
        {
            float dur = Mathf.Max(0f, shieldDuration);

            _shieldActive = true;
            _shieldEndAt = (dur > 0f) ? Time.unscaledTime + dur : 0f;
            _shieldConsumedByHit = false; // reset on fresh activation

            var defStart = ScriptableObject.CreateInstance<PowerupDef>(); defStart.kind = PowerupKind.Shield;
            GameEvents.RaisePowerupStarted(defStart);

            if (verboseLogs) Debug.Log(dur > 0f ? $"[Shield] ON for {dur:0.##}s" : "[Shield] ON (until consumed)");

            while (_shieldActive && _shieldEndAt == 0f)
                yield return null;

            _shieldCo = null;
        }

        public bool TryConsumeShieldHit()
        {
            if (!_shieldActive) return false;

            _shieldConsumedByHit = true;
            EndShield();
            return true;
        }

        void EndShield()
        {
            if (!_shieldActive) return;

            _shieldActive = false;
            _shieldEndAt = 0f;

            if (_shieldConsumedByHit)
            {
                AudioHub.I?.PlayShieldBreak();
                if (verboseLogs) Debug.Log("[Shield] Consumed by hit (played break SFX).");
            }
            else
            {
                if (verboseLogs) Debug.Log("[Shield] Ended by timeout.");
            }

            _shieldConsumedByHit = false;

            var defEnd = ScriptableObject.CreateInstance<PowerupDef>(); defEnd.kind = PowerupKind.Shield;
            GameEvents.RaisePowerupEnded(defEnd);
        }

        // ===== Clear Screen =====
        void DoClearScreen()
        {
            var defStart = ScriptableObject.CreateInstance<PowerupDef>(); defStart.kind = PowerupKind.ClearScreen;
            GameEvents.RaisePowerupStarted(defStart);

            int sum = 0;
            if (Fruit.Active.Count > 0)
            {
                var list = new List<Fruit>(Fruit.Active);
                for (int i = 0; i < list.Count; i++)
                {
                    var f = list[i];
                    if (!f) continue;
                    if (f.data != null && !f.data.isBomb) sum += f.data.scoreValue;
                    f.Retire();
                }
            }
            else
            {
                var fruits = FindObjectsByType<Fruit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < fruits.Length; i++)
                {
                    var f = fruits[i];
                    if (!f) continue;
                    if (f.data != null && !f.data.isBomb) sum += f.data.scoreValue;
                    f.Retire();
                }
            }

            if (sum > 0) ScoreManager.Instance?.AddBulkPoints(sum);

            var defEnd = ScriptableObject.CreateInstance<PowerupDef>(); defEnd.kind = PowerupKind.ClearScreen;
            GameEvents.RaisePowerupEnded(defEnd);
        }

        // ===== Utilities =====
        void EndAllEffectsImmediate()
        {
            if (_freezeCo != null) { StopCoroutine(_freezeCo); _freezeCo = null; }
            if (_multCo != null) { StopCoroutine(_multCo); _multCo = null; }
            if (_magnetCo != null) { StopCoroutine(_magnetCo); _magnetCo = null; }
            if (_shieldCo != null) { StopCoroutine(_shieldCo); _shieldCo = null; }

            // Restore gravity if Freeze active
            if (FreezeActive)
            {
                RestoreGravityIfNeeded();
                var def = ScriptableObject.CreateInstance<PowerupDef>(); def.kind = PowerupKind.TimeScale;
                GameEvents.RaisePowerupEnded(def);
                FreezeActive = false;
            }

            MagnetActive = false;

            if (_shieldActive)
            {
                _shieldActive = false;
                _shieldEndAt = 0f;
                _shieldConsumedByHit = false;
                var def = ScriptableObject.CreateInstance<PowerupDef>(); def.kind = PowerupKind.Shield;
                GameEvents.RaisePowerupEnded(def);
            }
        }
    }
}
