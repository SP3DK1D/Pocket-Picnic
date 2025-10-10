using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static CatchTheFruit.PowerupDef;

namespace CatchTheFruit
{
    /// <summary>
    /// Central power-up controller.
    /// - Freeze scales Physics2D.gravity (not Time.timeScale) so input/UI stay responsive.
    /// - Score Multiplier, Magnet, Shield, Clear Screen.
    /// - Raises GameEvents for UI/VFX/Audio.
    /// </summary>
    public class PowerupManager : MonoBehaviour
    {
        public static PowerupManager Instance { get; private set; }

        // ==== Public state used by other systems ====
        public static bool FreezeActive { get; private set; }

        // Compatibility for older code (e.g., Fruit.cs) that reads FreezeSpeedMul.
        // When not frozen, this is 1.0f; during Freeze it equals the gravity scale (e.g., 0.2f).
        public static float FreezeSpeedMul { get; private set; } = 1f;

        // Magnet info read by Fruit.cs
        public static bool MagnetActive { get; private set; }
        public static float MagnetRadius { get; private set; }
        public static float MagnetPullSpeed { get; private set; }
        public static Transform PlayerTransform { get; private set; }

        // Shield helpers for gameplay
        public static bool ShieldIsActive => Instance != null && Instance._shieldActive;
        public static bool ConsumeShieldIfActive() => Instance != null && Instance.TryConsumeShieldHit();

        [Header("Player (for Magnet)")]
        [SerializeField] private Transform player; // auto-find by Tag=Player if null

        [Header("Freeze (GravityScale)")]
        [Tooltip("Gravity multiplier during Freeze (e.g., 0.2 = 20% of normal fall speed).")]
        [SerializeField, Range(0.01f, 1f)] private float freezeScale = 0.20f;
        [SerializeField, Min(0.1f)] private float freezeDuration = 2.5f;

        [Header("Score Multiplier")]
        [SerializeField, Min(1f)] private float scoreMultiplier = 2f;
        [SerializeField, Min(0.1f)] private float scoreMultDuration = 7f;

        [Header("Magnet")]
        [SerializeField, Min(0.1f)] private float magnetRadius = 5.5f;
        [SerializeField, Min(0.1f)] private float magnetPullSpeed = 12f;
        [SerializeField, Min(0.1f)] private float magnetDuration = 7f;

        [Header("Shield")]
        [Tooltip("0 = infinite (until consumed)")]
        [SerializeField, Min(0f)] private float shieldDuration = 0f;

        [Header("Debug")]
        [SerializeField] private bool verboseLogs = false;

        // ==== Runtime ====
        Coroutine _freezeCo, _multCo, _magnetCo, _shieldCo;
        bool _shieldActive;
        float _shieldEndAt = 0f;

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

            EndAllEffectsImmediate();

            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (_shieldActive && _shieldEndAt > 0f && Time.unscaledTime >= _shieldEndAt)
                EndShield(); // timeout end
        }

        // ----- Events -----
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
                case PowerupKind.TimeScale: ActivateFreeze(); break; // legacy name
                case PowerupKind.ScoreMultiplier: ActivateScoreMultiplier(); break;
                case PowerupKind.Magnet: ActivateMagnet(); break;
                case PowerupKind.Shield: ActivateShield(); break;
                case PowerupKind.ClearScreen: DoClearScreen(); break;
            }
        }

        // ===== Freeze (scale Physics2D.gravity) =====
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

            FreezeActive = true;
            FreezeSpeedMul = gMul;  // compatibility for existing code

            var defStart = ScriptableObject.CreateInstance<PowerupDef>(); defStart.kind = PowerupKind.TimeScale;
            GameEvents.RaisePowerupStarted(defStart);
            AudioManager.Instance?.PlayPowerupStart(PowerupKind.TimeScale);

            Physics2D.gravity = _origGravity2D * gMul;
            if (verboseLogs) Debug.Log($"[Freeze] Gravity → {gMul:0.##}x for {dur:0.##}s");

            float t = 0f;
            while (t < dur)
            {
                if (PauseManager.Instance != null && PauseManager.Instance.IsPaused)
                { yield return null; continue; }
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            RestoreGravityIfNeeded();

            var defEnd = ScriptableObject.CreateInstance<PowerupDef>(); defEnd.kind = PowerupKind.TimeScale;
            GameEvents.RaisePowerupEnded(defEnd);

            FreezeActive = false;
            FreezeSpeedMul = 1f;
            _freezeCo = null;
        }

        void RestoreGravityIfNeeded()
        {
            if (!_origGravityCaptured) return;
            Physics2D.gravity = _origGravity2D;
            FreezeSpeedMul = 1f; // keep the compat property accurate
            if (verboseLogs) Debug.Log("[Freeze] Gravity restored.");
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
            defStart.scoreMultiplier = scoreMultiplier;
            GameEvents.RaisePowerupStarted(defStart);
            AudioManager.Instance?.PlayPowerupStart(PowerupKind.ScoreMultiplier);

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
            AudioManager.Instance?.PlayPowerupStart(PowerupKind.Magnet);

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

            var defStart = ScriptableObject.CreateInstance<PowerupDef>(); defStart.kind = PowerupKind.Shield;
            GameEvents.RaisePowerupStarted(defStart);
            AudioManager.Instance?.PlayPowerupStart(PowerupKind.Shield);

            if (verboseLogs) Debug.Log(dur > 0f ? $"[Shield] ON for {dur:0.##}s" : "[Shield] ON (until consumed)");

            // Wait until consumed by hit (EndShield called) or time runs out
            while (_shieldActive && _shieldEndAt == 0f)
                yield return null;

            _shieldCo = null;
        }

        /// <summary>
        /// Called by gameplay (e.g., BasketCatchZone) when a bomb would hit the player.
        /// If shield is active, consume it and play the break SFX immediately.
        /// </summary>
        public bool TryConsumeShieldHit()
        {
            if (!_shieldActive) return false;

            // Play the break sound at the exact moment of impact
            AudioManager.Instance?.PlayShieldBreak();

            EndShield(); // ends visuals/state
            return true;
        }

        void EndShield()
        {
            if (!_shieldActive) return;

            _shieldActive = false;
            _shieldEndAt = 0f;

            var defEnd = ScriptableObject.CreateInstance<PowerupDef>(); defEnd.kind = PowerupKind.Shield;
            GameEvents.RaisePowerupEnded(defEnd);
        }

        // ===== Clear Screen =====
        void DoClearScreen()
        {
            var defStart = ScriptableObject.CreateInstance<PowerupDef>(); defStart.kind = PowerupKind.ClearScreen;
            GameEvents.RaisePowerupStarted(defStart);
            AudioManager.Instance?.PlayPowerupStart(PowerupKind.ClearScreen);

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

            if (FreezeActive)
            {
                RestoreGravityIfNeeded();
                var def = ScriptableObject.CreateInstance<PowerupDef>(); def.kind = PowerupKind.TimeScale;
                GameEvents.RaisePowerupEnded(def);
                FreezeActive = false;
                FreezeSpeedMul = 1f;
            }

            MagnetActive = false;

            if (_shieldActive)
            {
                _shieldActive = false;
                _shieldEndAt = 0f;
                var def = ScriptableObject.CreateInstance<PowerupDef>(); def.kind = PowerupKind.Shield;
                GameEvents.RaisePowerupEnded(def);
            }
        }
    }
}
