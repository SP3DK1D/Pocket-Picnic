using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static CatchTheFruit.PowerupDef;

namespace CatchTheFruit
{
    [DisallowMultipleComponent]
    public class UIHud : MonoBehaviour
    {
        // NEW: simple singleton so menu can poke a refresh after showing the panel
        public static UIHud Instance { get; private set; }

        [Header("HUD Texts (assign)")]
        [SerializeField] private TMP_Text txtScore;
        [SerializeField] private TMP_Text txtBest;         // optional (HUD best)
        [SerializeField] private TMP_Text txtTimer;        // optional
        [SerializeField] private TMP_Text txtScoreDelta;   // optional ("+X")
        [SerializeField] private TMP_Text txtLives;        // optional

        [Header("Game Over Panel Texts (assign)")]
        [SerializeField] private TMP_Text goCurrentScoreText;  // Game Over "Score"
        [SerializeField] private TMP_Text goBestScoreText;     // Game Over "Best"

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

        const string BestKey = "best";

        void Awake()
        {
            Instance = this; // NEW

            // Initialize HUD labels
            if (txtScore) txtScore.text = "0";
            if (txtTimer) txtTimer.text = "0:00";
            if (txtLives) txtLives.text = "0";

            // Load current persisted best for HUD (if you show it)
            _best = PlayerPrefs.GetInt(BestKey, 0);
            if (txtBest) txtBest.text = $"Best: {_best}";

            // Prep delta bubble
            if (txtScoreDelta)
            {
                _deltaBaseColor = txtScoreDelta.color;
                var c = _deltaBaseColor; c.a = 0f;
                txtScoreDelta.color = c;
                txtScoreDelta.gameObject.SetActive(true);
            }

            SetGlowActive(false, instant: true);

            // If the Game Over panel happens to be active in editor, keep it sane
            RefreshGameOverTexts();
        }

        void OnEnable()
        {
            GameEvents.OnScoreChanged += HandleScoreChanged;
            GameEvents.OnTimerTick += HandleTimerTick;
            GameEvents.OnFruitCaught += HandleFruitCaught;
            GameEvents.OnLivesChanged += HandleLivesChanged;
            GameEvents.OnGameOver += HandleGameOver;
            GameEvents.OnPowerupStarted += HandlePowerupStarted;
            GameEvents.OnPowerupEnded += HandlePowerupEnded;

            // Keep UI consistent if re-enabled
            RefreshHudBest();
            RefreshGameOverTexts();
        }
        void OnDisable()
        {
            GameEvents.OnScoreChanged -= HandleScoreChanged;
            GameEvents.OnTimerTick -= HandleTimerTick;
            GameEvents.OnFruitCaught -= HandleFruitCaught;
            GameEvents.OnLivesChanged -= HandleLivesChanged;
            GameEvents.OnGameOver -= HandleGameOver;
            GameEvents.OnPowerupStarted -= HandlePowerupStarted;
            GameEvents.OnPowerupEnded -= HandlePowerupEnded;

            if (Instance == this) Instance = null; // NEW
        }

        void Update()
        {
            // Multiplier glow pulse
            if (multiplierGlow && _multiplierActive)
            {
                _glowT += Time.unscaledDeltaTime * glowPulseSpeed;
                float a = Mathf.Lerp(glowMinAlpha, glowMaxAlpha, 0.5f * (Mathf.Sin(_glowT) + 1f));
                var gc = multiplierGlow.color; gc.a = a; multiplierGlow.color = gc;
            }

            // Floating +X fade
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
            // Optional live HUD best refresh
            RefreshHudBest();
        }

        void HandleLivesChanged(int newLives)
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
            // Compute correct best regardless of event order
            int savedBest = PlayerPrefs.GetInt(BestKey, 0);
            int computedBest = (_score > savedBest) ? _score : savedBest;

            // Persist if improved
            if (computedBest != savedBest)
            {
                _best = computedBest;
                PlayerPrefs.SetInt(BestKey, _best);
                PlayerPrefs.Save();
            }
            else
            {
                _best = savedBest;
            }

            // Update HUD Best
            if (txtBest) txtBest.text = $"Best: {_best}";

            // Update Game Over texts (even if panel is inactive, text will persist)
            RefreshGameOverTexts();

            // Clear +X bubble
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
            if (def.kind == PowerupDef.PowerupKind.ScoreMultiplier)
                SetGlowActive(true);
        }
        void HandlePowerupEnded(PowerupDef def)
        {
            if (def == null) return;
            if (def.kind == PowerupDef.PowerupKind.ScoreMultiplier)
                SetGlowActive(false);
        }

        // ---- helpers ----
        void RefreshHudBest()
        {
            int savedBest = PlayerPrefs.GetInt(BestKey, 0);
            if (txtBest) txtBest.text = $"Best: {savedBest}";
        }

        void RefreshGameOverTexts()
        {
            if (!goCurrentScoreText && !goBestScoreText)
            {
                // Helpful warning if you forgot to assign references
#if UNITY_EDITOR
                Debug.LogWarning("[UIHud] Game Over TMP references not assigned on UIHud. Assign goCurrentScoreText & goBestScoreText.");
#endif
                return;
            }

            int savedBest = PlayerPrefs.GetInt(BestKey, 0);
            int bestToShow = Mathf.Max(savedBest, _score);

            if (goCurrentScoreText) goCurrentScoreText.text = $"Score: {_score}";
            if (goBestScoreText) goBestScoreText.text = $"Best: {bestToShow}";
        }

        // NEW: menu can call this AFTER the Game Over panel is shown
        public void ForceRefreshGameOverUI()
        {
            // Ensure the latest persisted best vs current run is displayed
            RefreshGameOverTexts();
        }

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
