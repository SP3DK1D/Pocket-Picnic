using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static CatchTheFruit.PowerupDef;

namespace CatchTheFruit
{
    [DisallowMultipleComponent]
    public class UIHud : MonoBehaviour
    {
        [Header("Text References (assign)")]
        [SerializeField] private TMP_Text txtScore;
        [SerializeField] private TMP_Text txtBest;         // optional
        [SerializeField] private TMP_Text txtTimer;        // optional
        [SerializeField] private TMP_Text txtScoreDelta;   // optional ("+X")
        [SerializeField] private TMP_Text txtLives;        // <-- NEW (assign!)

        [Header("Multiplier Glow (optional)")]
        [SerializeField] private Graphic multiplierGlow;   // any Graphic
        [SerializeField] private float glowPulseSpeed = 4f;
        [SerializeField] private float glowMinAlpha = 0.25f;
        [SerializeField] private float glowMaxAlpha = 0.9f;

        [Header("Score Delta Settings")]
        [SerializeField] private float deltaHoldSeconds = 0.5f;
        [SerializeField] private float deltaFadeSeconds = 0.25f;
        [SerializeField] private string deltaPrefix = "+";

        int _score, _best, _lives;
        float _timer;
        int _deltaAccum;
        float _deltaTimer;
        bool _deltaVisible;
        Color _deltaBaseColor;
        bool _multiplierActive;
        float _glowT;

        void Awake()
        {
            if (txtScore) txtScore.text = "0";
            if (txtTimer) txtTimer.text = "0:00";
            if (txtLives) txtLives.text = "0";
            if (txtBest)
            {
                _best = PlayerPrefs.GetInt("best", 0);
                txtBest.text = $"Best: {_best}";
            }
            if (txtScoreDelta)
            {
                _deltaBaseColor = txtScoreDelta.color;
                var c = _deltaBaseColor; c.a = 0f;
                txtScoreDelta.color = c;
                txtScoreDelta.gameObject.SetActive(true);
            }
            SetGlowActive(false, instant: true);
        }

        void OnEnable()
        {
            GameEvents.OnScoreChanged += HandleScoreChanged;
            GameEvents.OnTimerTick += HandleTimerTick;
            GameEvents.OnFruitCaught += HandleFruitCaught;
            GameEvents.OnLivesChanged += HandleLivesChanged;   // <-- NEW
            GameEvents.OnGameOver += HandleGameOver;
            GameEvents.OnPowerupStarted += HandlePowerupStarted;
            GameEvents.OnPowerupEnded += HandlePowerupEnded;
        }
        void OnDisable()
        {
            GameEvents.OnScoreChanged -= HandleScoreChanged;
            GameEvents.OnTimerTick -= HandleTimerTick;
            GameEvents.OnFruitCaught -= HandleFruitCaught;
            GameEvents.OnLivesChanged -= HandleLivesChanged;   // <-- NEW
            GameEvents.OnGameOver -= HandleGameOver;
            GameEvents.OnPowerupStarted -= HandlePowerupStarted;
            GameEvents.OnPowerupEnded -= HandlePowerupEnded;
        }

        void Update()
        {
            if (multiplierGlow && _multiplierActive)
            {
                _glowT += Time.unscaledDeltaTime * glowPulseSpeed;
                float a = Mathf.Lerp(glowMinAlpha, glowMaxAlpha, 0.5f * (Mathf.Sin(_glowT) + 1f));
                var gc = multiplierGlow.color; gc.a = a; multiplierGlow.color = gc;
            }

            if (txtScoreDelta && _deltaVisible)
            {
                _deltaTimer -= Time.unscaledDeltaTime;
                if (_deltaTimer <= 0f)
                {
                    float a = Mathf.Clamp01(1f + _deltaTimer / deltaFadeSeconds);
                    var c = _deltaBaseColor; c.a = a;
                    txtScoreDelta.color = c;
                    if (a <= 0f)
                    {
                        _deltaVisible = false;
                        _deltaAccum = 0;
                        txtScoreDelta.text = "";
                    }
                }
            }
        }

        // ---- Event handlers ----
        void HandleScoreChanged(int newScore)
        {
            _score = newScore;
            if (txtScore) txtScore.text = _score.ToString();
        }

        void HandleLivesChanged(int newLives)              // <-- NEW
        {
            _lives = newLives;
            if (txtLives) txtLives.text = _lives.ToString();
        }

        void HandleTimerTick(float elapsedSeconds)
        {
            _timer = elapsedSeconds;
            if (!txtTimer) return;
            int total = Mathf.FloorToInt(_timer);
            int m = total / 60;
            int s = total % 60;
            txtTimer.text = $"{m}:{s:00}";
        }

        void HandleFruitCaught(string id, int baseScore, bool isBomb)
        {
            if (isBomb) return;
            _deltaAccum += baseScore;
            if (txtScoreDelta)
            {
                txtScoreDelta.text = $"{deltaPrefix}{_deltaAccum}";
                var c = _deltaBaseColor; c.a = 1f;
                txtScoreDelta.color = c;
                _deltaVisible = true;
                _deltaTimer = deltaHoldSeconds;
            }
        }

        void HandleGameOver()
        {
            if (_score > _best)
            {
                _best = _score;
                PlayerPrefs.SetInt("best", _best);
            }
            if (txtBest) txtBest.text = $"Best: {_best}";

            if (txtScoreDelta)
            {
                _deltaAccum = 0;
                _deltaVisible = false;
                var c = _deltaBaseColor; c.a = 0f;
                txtScoreDelta.color = c;
                txtScoreDelta.text = "";
            }
            SetGlowActive(false, instant: true);
        }

        void HandlePowerupStarted(PowerupDef def)
        {
            if (def == null) return;
            if (def.kind == PowerupKind.ScoreMultiplier)
                SetGlowActive(true);
        }
        void HandlePowerupEnded(PowerupDef def)
        {
            if (def == null) return;
            if (def.kind == PowerupKind.ScoreMultiplier)
                SetGlowActive(false);
        }

        // ---- helpers ----
        void SetGlowActive(bool on, bool instant = false)
        {
            _multiplierActive = on;
            if (!multiplierGlow) return;
            if (on)
            {
                if (instant)
                {
                    var c = multiplierGlow.color; c.a = glowMaxAlpha; multiplierGlow.color = c;
                }
                _glowT = 0f;
            }
            else
            {
                var c = multiplierGlow.color; c.a = 0f; multiplierGlow.color = c;
            }
        }
    }
}
