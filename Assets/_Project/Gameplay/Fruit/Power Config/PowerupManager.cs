using System.Collections;
using UnityEngine;
using static CatchTheFruit.PowerupDef;

namespace CatchTheFruit
{
    /// <summary>
    /// Central power-up controller.
    /// - Uses unscaled time (pause-aware).
    /// - Avoids GC at runtime by caching event payload ScriptableObjects.
    /// </summary>
    public class PowerupManager : MonoBehaviour
    {
        public static PowerupManager Instance { get; private set; }

        // ===== Public state used by other systems =====
        public static bool FreezeActive { get; private set; }
        public static float FreezeSpeedMul { get; private set; } = 1f;

        public static bool MagnetActive { get; private set; }
        public static float MagnetRadius { get; private set; }
        public static float MagnetPullSpeed { get; private set; }
        public static Transform PlayerTransform { get; private set; }

        public static bool ShieldIsActive => Instance != null && Instance._shieldActive;
        public static bool ConsumeShieldIfActive() => Instance != null && Instance.TryConsumeShieldHit();

        [Header("Player (for Magnet)")]
        [SerializeField] private Transform player;

        [Header("Freeze (GravityScale)")]
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

        // ===== Runtime =====
        Coroutine _freezeCo, _multCo, _magnetCo, _shieldCo;
        bool _shieldActive;
        float _shieldEndAt = 0f;

        Vector2 _origGravity2D;
        bool _origGravityCaptured = false;

        // ===== Cached event payloads (avoid per-event allocations) =====
        static PowerupDef s_evtFreeze, s_evtMult, s_evtMagnet, s_evtShield, s_evtClear;

        static PowerupDef Evt(PowerupKind k)
        {
            switch (k)
            {
                case PowerupKind.TimeScale: return s_evtFreeze ??= ScriptableObject.CreateInstance<PowerupDef>();
                case PowerupKind.ScoreMultiplier: return s_evtMult ??= ScriptableObject.CreateInstance<PowerupDef>();
                case PowerupKind.Magnet: return s_evtMagnet ??= ScriptableObject.CreateInstance<PowerupDef>();
                case PowerupKind.Shield: return s_evtShield ??= ScriptableObject.CreateInstance<PowerupDef>();
                case PowerupKind.ClearScreen: return s_evtClear ??= ScriptableObject.CreateInstance<PowerupDef>();
            }
            return null;
        }

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

            // Initialize cached payload kinds (explicit statements to avoid CS0201)
            var eFreeze = Evt(PowerupKind.TimeScale); eFreeze.kind = PowerupKind.TimeScale;
            var eMult = Evt(PowerupKind.ScoreMultiplier); eMult.kind = PowerupKind.ScoreMultiplier;
            var eMag = Evt(PowerupKind.Magnet); eMag.kind = PowerupKind.Magnet;
            var eShield = Evt(PowerupKind.Shield); eShield.kind = PowerupKind.Shield;
            var eClear = Evt(PowerupKind.ClearScreen); eClear.kind = PowerupKind.ClearScreen;
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
                EndShield();
        }

        // ===== Event handlers =====
        void OnStart()
        {
            EndAllEffectsImmediate();
            PlayerTransform = player ? player : PlayerTransform;
        }

        void OnOver() => EndAllEffectsImmediate();

        void OnPicked(PowerupDef def)
        {
            if (!def) return;

            switch (def.kind)
            {
                case PowerupKind.TimeScale: ActivateFreeze(); break;
                case PowerupKind.ScoreMultiplier: ActivateScoreMultiplier(); break;
                case PowerupKind.Magnet: ActivateMagnet(); break;
                case PowerupKind.Shield: ActivateShield(); break;
                case PowerupKind.ClearScreen: DoClearScreen(); break;
            }
        }

        // ===== Freeze =====
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
            FreezeSpeedMul = gMul;

            GameEvents.RaisePowerupStarted(Evt(PowerupKind.TimeScale));
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
            GameEvents.RaisePowerupEnded(Evt(PowerupKind.TimeScale));

            FreezeActive = false;
            FreezeSpeedMul = 1f;
            _freezeCo = null;
        }

        void RestoreGravityIfNeeded()
        {
            if (!_origGravityCaptured) return;
            Physics2D.gravity = _origGravity2D;
            FreezeSpeedMul = 1f;
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

            var evt = Evt(PowerupKind.ScoreMultiplier);
            evt.scoreMultiplier = Mathf.Max(1f, scoreMultiplier);

            GameEvents.RaisePowerupStarted(evt);
            AudioManager.Instance?.PlayPowerupStart(PowerupKind.ScoreMultiplier);

            float t = 0f;
            while (t < dur)
            {
                if (PauseManager.Instance != null && PauseManager.Instance.IsPaused)
                { yield return null; continue; }

                t += Time.unscaledDeltaTime;
                yield return null;
            }

            GameEvents.RaisePowerupEnded(Evt(PowerupKind.ScoreMultiplier));
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

            GameEvents.RaisePowerupStarted(Evt(PowerupKind.Magnet));
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
            GameEvents.RaisePowerupEnded(Evt(PowerupKind.Magnet));
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

            GameEvents.RaisePowerupStarted(Evt(PowerupKind.Shield));
            AudioManager.Instance?.PlayPowerupStart(PowerupKind.Shield);

            if (verboseLogs)
                Debug.Log(dur > 0f ? $"[Shield] ON for {dur:0.##}s" : "[Shield] ON (until consumed)");

            // If duration == 0 → wait until consumed; if > 0, Update() will time it out.
            while (_shieldActive && _shieldEndAt == 0f)
                yield return null;

            _shieldCo = null;
        }

        public bool TryConsumeShieldHit()
        {
            if (!_shieldActive) return false;
            AudioManager.Instance?.PlayShieldBreak();
            EndShield();
            return true;
        }

        void EndShield()
        {
            if (!_shieldActive) return;
            _shieldActive = false;
            _shieldEndAt = 0f;
            GameEvents.RaisePowerupEnded(Evt(PowerupKind.Shield));
        }

        // ===== Clear Screen =====
        void DoClearScreen()
        {
            GameEvents.RaisePowerupStarted(Evt(PowerupKind.ClearScreen));
            AudioManager.Instance?.PlayPowerupStart(PowerupKind.ClearScreen);

            int sum = 0;

            if (Fruit.Active.Count > 0)
            {
                var list = new System.Collections.Generic.List<Fruit>(Fruit.Active);
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
            GameEvents.RaisePowerupEnded(Evt(PowerupKind.ClearScreen));
        }

        // ===== Teardown =====
        void EndAllEffectsImmediate()
        {
            if (_freezeCo != null) { StopCoroutine(_freezeCo); _freezeCo = null; }
            if (_multCo != null) { StopCoroutine(_multCo); _multCo = null; }
            if (_magnetCo != null) { StopCoroutine(_magnetCo); _magnetCo = null; }
            if (_shieldCo != null) { StopCoroutine(_shieldCo); _shieldCo = null; }

            if (FreezeActive)
            {
                RestoreGravityIfNeeded();
                GameEvents.RaisePowerupEnded(Evt(PowerupKind.TimeScale));
                FreezeActive = false;
                FreezeSpeedMul = 1f;
            }

            MagnetActive = false;

            if (_shieldActive)
            {
                _shieldActive = false;
                _shieldEndAt = 0f;
                GameEvents.RaisePowerupEnded(Evt(PowerupKind.Shield));
            }
        }
    }
}
