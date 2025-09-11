using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Applies power-ups. Only TimeScale touches Time.timeScale.
    /// ClearScreen awards points for all visible NON-bomb fruits, then clears them.
    /// </summary>
    public class PowerupManager : MonoBehaviour
    {
        public static PowerupManager Instance { get; private set; }

        [Header("Player (for Magnet). Leave empty to find by tag 'Player'.")]
        [SerializeField] Transform player;

        // --- Freeze (TimeScale)
        bool _tsActive; float _tsEnd; float _prevTS = 1f; float _prevFDT = 0.02f; PowerupDef _tsDef;

        // --- Score Multiplier
        bool _mulActive; float _mulEnd; PowerupDef _mulDef;

        // --- Magnet
        bool _magActive; float _magEnd; PowerupDef _magDef;
        public static bool MagnetActive => Instance && Instance._magActive;
        public static float MagnetRadius => Instance ? Instance._magDef?.magnetRadius ?? 0f : 0f;
        public static float MagnetPullSpeed => Instance ? Instance._magDef?.magnetPullSpeed ?? 0f : 0f;
        public static Transform PlayerTransform => Instance ? Instance.player : null;

        // --- Shield
        bool _shieldActive; float _shieldEnd; PowerupDef _shieldDef;
        public static bool ShieldActive => Instance && Instance._shieldActive;

        void Awake()
        {
            Instance = this;
            if (!player)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p) player = p.transform;
            }
        }

        void OnEnable()
        {
            GameEvents.OnPowerupPicked += HandlePicked;
            GameEvents.OnGameOver += ClearAll;   // <— now exists
        }
        void OnDisable()
        {
            GameEvents.OnPowerupPicked -= HandlePicked;
            GameEvents.OnGameOver -= ClearAll;
            ClearAll();
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            float now = Time.unscaledTime;
            if (_tsActive && now >= _tsEnd) EndTimeScale();
            if (_mulActive && now >= _mulEnd) EndScoreMultiplier();
            if (_magActive && now >= _magEnd) EndMagnet();
            if (_shieldActive && _shieldDef && _shieldDef.duration > 0f && now >= _shieldEnd) EndShield();
        }

        void HandlePicked(PowerupDef def)
        {
            if (!def) return;
            Debug.Log($"[PU] Picked: {def.id} ({def.kind})");

            switch (def.kind)
            {
                case PowerupDef.PowerupKind.TimeScale: ApplyTimeScale(def); break;
                case PowerupDef.PowerupKind.ScoreMultiplier: ApplyScoreMultiplier(def); break;
                case PowerupDef.PowerupKind.Magnet: ApplyMagnet(def); break;
                case PowerupDef.PowerupKind.Shield: ApplyShield(def); break;
                case PowerupDef.PowerupKind.ClearScreen: DoClearScreen(def); break;
            }
        }

        // ---------- TimeScale ----------
        void ApplyTimeScale(PowerupDef def)
        {
            float dur = Mathf.Max(0.01f, def.duration);
            float scale = Mathf.Clamp(def.timeScale, 0.05f, 1f);

            if (_tsActive)
            {
                switch (def.overlapPolicy)
                {
                    case PowerupDef.OverlapPolicy.Ignore: return;
                    case PowerupDef.OverlapPolicy.RefreshDuration: _tsEnd = Time.unscaledTime + dur; return;
                    case PowerupDef.OverlapPolicy.Replace: EndTimeScale(); break;
                }
            }

            _prevTS = Time.timeScale; _prevFDT = Time.fixedDeltaTime;
            Time.timeScale = scale; Time.fixedDeltaTime = _prevFDT * scale;

            _tsActive = true; _tsDef = def; _tsEnd = Time.unscaledTime + dur;
            GameEvents.RaisePowerupStarted(def);
        }
        void EndTimeScale()
        {
            if (!_tsActive) return;
            Time.timeScale = _prevTS; Time.fixedDeltaTime = _prevFDT;
            _tsActive = false; GameEvents.RaisePowerupEnded(_tsDef); _tsDef = null;
        }

        // ---------- Score Multiplier ----------
        void ApplyScoreMultiplier(PowerupDef def)
        {
            float dur = Mathf.Max(0.01f, def.duration);
            if (_mulActive)
            {
                switch (def.overlapPolicy)
                {
                    case PowerupDef.OverlapPolicy.Ignore: return;
                    case PowerupDef.OverlapPolicy.RefreshDuration: _mulEnd = Time.unscaledTime + dur; return;
                    case PowerupDef.OverlapPolicy.Replace: EndScoreMultiplier(); break;
                }
            }
            _mulActive = true; _mulDef = def; _mulEnd = Time.unscaledTime + dur;
            GameEvents.RaisePowerupStarted(def);
        }
        void EndScoreMultiplier()
        {
            if (!_mulActive) return;
            _mulActive = false; GameEvents.RaisePowerupEnded(_mulDef); _mulDef = null;
        }

        // ---------- Magnet ----------
        void ApplyMagnet(PowerupDef def)
        {
            float dur = Mathf.Max(0.01f, def.duration);
            if (_magActive)
            {
                switch (def.overlapPolicy)
                {
                    case PowerupDef.OverlapPolicy.Ignore: return;
                    case PowerupDef.OverlapPolicy.RefreshDuration: _magEnd = Time.unscaledTime + dur; return;
                    case PowerupDef.OverlapPolicy.Replace: EndMagnet(); break;
                }
            }
            _magActive = true; _magDef = def; _magEnd = Time.unscaledTime + dur;
            GameEvents.RaisePowerupStarted(def);
        }
        void EndMagnet()
        {
            if (!_magActive) return;
            _magActive = false; GameEvents.RaisePowerupEnded(_magDef); _magDef = null;
        }

        // ---------- Shield ----------
        void ApplyShield(PowerupDef def)
        {
            _shieldActive = true; _shieldDef = def;
            _shieldEnd = Time.unscaledTime + Mathf.Max(0f, def.duration); // 0 = until used
            GameEvents.RaisePowerupStarted(def);
        }
        public static void ConsumeShield() => Instance?.EndShield();
        void EndShield()
        {
            if (!_shieldActive) return;
            _shieldActive = false; GameEvents.RaisePowerupEnded(_shieldDef); _shieldDef = null;
        }

        // ---------- Clear Screen (award points + clear) ----------
        void DoClearScreen(PowerupDef def)
        {
            GameEvents.RaisePowerupStarted(def);

            int baseTotal = 0;

            if (Fruit.Active.Count > 0)
            {
                var list = new System.Collections.Generic.List<Fruit>(Fruit.Active);
                foreach (var f in list)
                {
                    if (!f || f.data == null) continue;
                    if (!f.data.isBomb)
                        baseTotal += Mathf.Max(0, f.data.scoreValue);
                    Destroy(f.gameObject);
                }
            }
            else
            {
                var fruits = FindObjectsByType<Fruit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                foreach (var f in fruits)
                {
                    if (!f || f.data == null) continue;
                    if (!f.data.isBomb)
                        baseTotal += Mathf.Max(0, f.data.scoreValue);
                    Destroy(f.gameObject);
                }
            }

            if (baseTotal > 0)
                ScoreManager.Instance?.AddBulkPoints(baseTotal);

            GameEvents.RaisePowerupEnded(def);
        }

        // ---------- Utility ----------
        void ClearAll()
        {
            EndTimeScale();
            EndScoreMultiplier();
            EndMagnet();
            EndShield();
        }
    }
}
