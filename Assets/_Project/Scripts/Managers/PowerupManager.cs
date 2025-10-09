using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static CatchTheFruit.PowerupDef;

namespace CatchTheFruit
{
    public class PowerupManager : MonoBehaviour
    {
        public static PowerupManager Instance { get; private set; }

        public static bool   FreezeActive { get; private set; }
        public static float  FreezeSpeedMul { get; private set; } = 1f;
        public static bool   MagnetActive { get; private set; }
        public static float  MagnetRadius { get; private set; }
        public static float  MagnetPullSpeed { get; private set; }
        public static Transform PlayerTransform { get; private set; }
        public static bool   ShieldIsActive => Instance && Instance._shieldActive;
        public static bool   ConsumeShieldIfActive() => Instance && Instance.TryConsumeShieldHit();

        [Header("Player (for Magnet)")]
        [SerializeField] private Transform player;

        [Header("Optional Overrides (leave null to use pickup def)")]
        [SerializeField] private PowerupDef freezeDef;
        [SerializeField] private PowerupDef scoreDef;
        [SerializeField] private PowerupDef magnetDef;
        [SerializeField] private PowerupDef shieldDef;
        [SerializeField] private PowerupDef clearDef;

        [Header("Debug")]
        [SerializeField] private bool verboseLogs = false;

        Coroutine _freezeCo, _multCo, _magnetCo, _shieldCo;
        bool _shieldActive;
        float _shieldEndAt = 0f;
        bool _shieldConsumedByHit;

        Vector2 _origGravity2D;
        bool _origGravityCaptured;

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (!player)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p) player = p.transform;
            }
            PlayerTransform = player;

            _origGravity2D = Physics2D.gravity;
            _origGravityCaptured = true;
        }

        void OnEnable()
        {
            GameEvents.OnPowerupPicked += OnPicked;
            GameEvents.OnGameStart     += OnStart;
            GameEvents.OnGameOver      += OnOver;
        }
        void OnDisable()
        {
            GameEvents.OnPowerupPicked -= OnPicked;
            GameEvents.OnGameStart     -= OnStart;
            GameEvents.OnGameOver      -= OnOver;

            EndAllImmediate();
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (_shieldActive && _shieldEndAt > 0f && Time.unscaledTime >= _shieldEndAt)
                EndShield(); // timeout
        }

        void OnValidate()
        {
            CheckKind("freezeDef", freezeDef, PowerupKind.TimeScale);
            CheckKind("scoreDef",  scoreDef,  PowerupKind.ScoreMultiplier);
            CheckKind("magnetDef", magnetDef, PowerupKind.Magnet);
            CheckKind("shieldDef", shieldDef, PowerupKind.Shield);
            CheckKind("clearDef",  clearDef,  PowerupKind.ClearScreen);
        }
        static void CheckKind(string field, PowerupDef d, PowerupKind expected)
        {
            if (d && d.kind != expected)
                Debug.LogWarning($"[PowerupManager] {field} expects {expected} but has {d.kind}. Using pickup def at runtime.", d);
        }

        void OnStart()
        {
            EndAllImmediate();
            PlayerTransform = player ? player : PlayerTransform;
        }
        void OnOver() => EndAllImmediate();

        void OnPicked(PowerupDef def)
        {
            if (!def) return;
            var used = ChooseDef(def.kind, def);

            switch (used.kind)
            {
                case PowerupKind.TimeScale:       StartFreeze(used);          break;
                case PowerupKind.ScoreMultiplier: StartScoreMultiplier(used); break;
                case PowerupKind.Magnet:          StartMagnet(used);          break;
                case PowerupKind.Shield:          StartShield(used);          break;
                case PowerupKind.ClearScreen:     DoClear(used);              break;
            }
        }

        PowerupDef ChooseDef(PowerupKind kind, PowerupDef fallback)
        {
            PowerupDef pick = kind switch
            {
                PowerupKind.TimeScale       => freezeDef,
                PowerupKind.ScoreMultiplier => scoreDef,
                PowerupKind.Magnet          => magnetDef,
                PowerupKind.Shield          => shieldDef,
                PowerupKind.ClearScreen     => clearDef,
                _ => null
            };
            if (pick && pick.kind != kind)
            {
                Debug.LogWarning($"[PowerupManager] Override mismatch: wanted {kind}, got {pick.kind}. Fallback to pickup def.", pick);
                pick = null;
            }
            return pick ? pick : fallback;
        }

        // ---------- Freeze ----------
        void StartFreeze(PowerupDef def)
        {
            if (_freezeCo != null) StopCoroutine(_freezeCo);
            _freezeCo = StartCoroutine(CoFreeze(def));
        }

        IEnumerator CoFreeze(PowerupDef def)
        {
            float dur  = Mathf.Max(0.1f, def.duration);
            float gMul = Mathf.Clamp(def.freezeGravityScale, 0.01f, 1f);

            if (!_origGravityCaptured) { _origGravity2D = Physics2D.gravity; _origGravityCaptured = true; }

            FreezeActive   = true;
            FreezeSpeedMul = gMul;
            GameEvents.RaisePowerupStarted(def);
            AudioManager.Instance?.PlayPowerupStart(def.kind);   // << SFX per-kind

            Physics2D.gravity = _origGravity2D * gMul;
            if (verboseLogs) Debug.Log($"[Freeze] x{gMul:0.##} for {dur:0.##}s");

            float t = 0f;
            while (t < dur)
            {
                if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) { yield return null; continue; }
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            Physics2D.gravity = _origGravity2D;
            FreezeActive   = false;
            FreezeSpeedMul = 1f;
            GameEvents.RaisePowerupEnded(Temp(PowerupKind.TimeScale)); // always correct kind
            _freezeCo = null;
        }

        // ---------- Score Multiplier ----------
        void StartScoreMultiplier(PowerupDef def)
        {
            if (_multCo != null) StopCoroutine(_multCo);
            _multCo = StartCoroutine(CoScore(def));
        }

        IEnumerator CoScore(PowerupDef def)
        {
            float dur = Mathf.Max(0.1f, def.duration);
            GameEvents.RaisePowerupStarted(def);
            AudioManager.Instance?.PlayPowerupStart(def.kind);

            float t = 0f;
            while (t < dur)
            {
                if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) { yield return null; continue; }
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            GameEvents.RaisePowerupEnded(Temp(PowerupKind.ScoreMultiplier));
            _multCo = null;
        }

        // ---------- Magnet ----------
        void StartMagnet(PowerupDef def)
        {
            if (_magnetCo != null) StopCoroutine(_magnetCo);
            _magnetCo = StartCoroutine(CoMagnet(def));
        }

        IEnumerator CoMagnet(PowerupDef def)
        {
            MagnetRadius    = Mathf.Max(0.1f, def.magnetRadius);
            MagnetPullSpeed = Mathf.Max(0.1f, def.magnetPullSpeed);
            PlayerTransform = player ? player : PlayerTransform;

            GameEvents.RaisePowerupStarted(def);
            AudioManager.Instance?.PlayPowerupStart(def.kind);

            MagnetActive = true;

            float dur = Mathf.Max(0.1f, def.duration);
            float t = 0f;
            while (t < dur)
            {
                if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) { yield return null; continue; }
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            MagnetActive = false;
            GameEvents.RaisePowerupEnded(Temp(PowerupKind.Magnet));
            _magnetCo = null;
        }

        // ---------- Shield ----------
        void StartShield(PowerupDef def)
        {
            if (_shieldCo != null) StopCoroutine(_shieldCo);
            _shieldCo = StartCoroutine(CoShield(def));
        }

        IEnumerator CoShield(PowerupDef def)
        {
            _shieldActive = true;
            _shieldConsumedByHit = false;

            float dur = Mathf.Max(0f, def.duration);
            _shieldEndAt = dur > 0f ? Time.unscaledTime + dur : 0f;

            GameEvents.RaisePowerupStarted(def);
            AudioManager.Instance?.PlayPowerupStart(def.kind);

            if (verboseLogs) Debug.Log(dur > 0f ? $"[Shield] ON for {dur:0.##}s" : "[Shield] ON (until consumed)");

            while (_shieldActive && _shieldEndAt == 0f) // wait until consumed if infinite
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
                AudioManager.Instance?.PlayShieldBreak();

            _shieldConsumedByHit = false;

            // IMPORTANT: always raise with a *temp* Shield def so kind is never wrong,
            // regardless of what asset is in the shieldDef override slot.
            GameEvents.RaisePowerupEnded(Temp(PowerupKind.Shield));
        }

        // ---------- Clear Screen ----------
        void DoClear(PowerupDef def)
        {
            GameEvents.RaisePowerupStarted(def);
            AudioManager.Instance?.PlayPowerupStart(def.kind);

            int sum = 0;
            if (Fruit.Active.Count > 0)
            {
                var copy = new List<Fruit>(Fruit.Active);
                for (int i = 0; i < copy.Count; i++)
                {
                    var f = copy[i];
                    if (!f) continue;
                    if (f.data != null && !f.data.isBomb) sum += f.data.scoreValue;
                    f.Retire();
                }
            }
            else
            {
                var all = Object.FindObjectsByType<Fruit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < all.Length; i++)
                {
                    var f = all[i];
                    if (!f) continue;
                    if (f.data != null && !f.data.isBomb) sum += f.data.scoreValue;
                    f.Retire();
                }
            }

            if (sum > 0) ScoreManager.Instance?.AddBulkPoints(sum);
            GameEvents.RaisePowerupEnded(Temp(PowerupKind.ClearScreen));
            VFXManager.Instance?.PlayClearScreenBurst();
        }

        // ---------- Utilities ----------
        void EndAllImmediate()
        {
            if (_freezeCo != null) { StopCoroutine(_freezeCo); _freezeCo = null; }
            if (_multCo   != null) { StopCoroutine(_multCo);   _multCo = null; }
            if (_magnetCo != null) { StopCoroutine(_magnetCo); _magnetCo = null; }
            if (_shieldCo != null) { StopCoroutine(_shieldCo); _shieldCo = null; }

            if (FreezeActive)
            {
                Physics2D.gravity = _origGravity2D;
                FreezeActive   = false;
                FreezeSpeedMul = 1f;
                GameEvents.RaisePowerupEnded(Temp(PowerupKind.TimeScale));
            }

            MagnetActive = false;

            if (_shieldActive)
            {
                _shieldActive = false; _shieldEndAt = 0f; _shieldConsumedByHit = false;
                GameEvents.RaisePowerupEnded(Temp(PowerupKind.Shield));
            }
        }

        static PowerupDef Temp(PowerupKind k)
        {
            var d = ScriptableObject.CreateInstance<PowerupDef>();
            d.kind = k;
            return d;
        }
    }
}
