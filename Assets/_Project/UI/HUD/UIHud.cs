using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static CatchTheFruit.PowerupDef;

namespace CatchTheFruit
{
    /// <summary>
    /// Heads-up Display for score, best, timer, lives, and a small "+X" delta bubble.
    /// - Listens only to GameEvents; it doesn't own gameplay state.
    /// - Reads Best from ScoreManager (cached) to avoid repeated PlayerPrefs IO.
    /// - Uses unscaled time for visuals (responsive during Freeze or Pause).
    /// - Multiplier glow is opt-in (assign any Graphic); it pulses only while active.
    ///
    /// Behavior is compatible with your previous UIHud: same public fields and events.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIHud : MonoBehaviour
    {
        // Lightweight singleton so MenuFlowController can call ForceRefreshGameOverUI().
        public static UIHud Instance { get; private set; }

        [Header("HUD Texts (assign)")]
        [SerializeField] private TMP_Text txtScore;
        [SerializeField] private TMP_Text txtBest;       // optional (HUD best)
        [SerializeField] private TMP_Text txtTimer;      // optional
        [SerializeField] private TMP_Text txtScoreDelta; // optional ("+X")
        [SerializeField] private TMP_Text txtLives;      // optional

        [Header("Game Over Panel Texts (assign)")]
        [SerializeField] private TMP_Text goCurrentScoreText; // Game Over "Score"
        [SerializeField] private TMP_Text goBestScoreText;    // Game Over "Best"

        [Header("Multiplier Glow (optional)")]
        [Tooltip("Any Graphic (Image, TMP, etc.) that should pulse while ScoreMultiplier is active.")]
        [SerializeField] private Graphic multiplierGlow;
        [SerializeField, Min(0.1f)] private float glowPulseSpeed = 4f;
        [SerializeField, Range(0f, 1f)] private float glowMinAlpha = 0.25f;
        [SerializeField, Range(0f, 1f)] private float glowMaxAlpha = 0.9f;

        [Header("Score Delta Settings")]
        [SerializeField, Min(0f)] private float deltaHoldSeconds = 0.5f;
        [SerializeField, Min(0f)] private float deltaFadeSeconds = 0.25f;
        [SerializeField] private string deltaPrefix = "+";

        // -------- runtime (HUD state) --------
        int _score;           // last known score (for HUD + GO)
        int _lives;           // last known lives
        int _deltaAccum;      // accumulated for the "+X" bubble
        float _deltaTimer;    // counts down hold + fade
        bool _deltaVisible;
        Color _deltaBaseColor;

        bool _multiplierActive;  // true while ScoreMultiplier power-up is active
        float _glowT;            // timer for glow pulse

        // Cached "best" for HUD reads. Source of truth is ScoreManager.Best.
        int BestCached => ScoreManager.Instance ? ScoreManager.Instance.Best : PlayerPrefs.GetInt("best", 0);

        // ---------- lifecycle ----------
        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // Initial HUD text (safe defaults)
            if (txtScore) txtScore.text = "0";
            if (txtLives) txtLives.text = "0";
            if (txtTimer) txtTimer.text = "0:00";

            // Initial HUD "Best" from cache
            if (txtBest) txtBest.text = $"Best: {BestCached}";

            // Prepare score delta bubble
            if (txtScoreDelta)
            {
                _deltaBaseColor = txtScoreDelta.color;
                SetScoreDeltaVisible(false, resetAccum: true);
            }

            // Ensure glow is hidden by default
            SetGlowActive(false, instant: true);

            // Make sure Game Over labels don't show stale data in-editor
            RefreshGameOverTexts();
        }

        void OnEnable()
        {
            GameEvents.OnScoreChanged += HandleScoreChanged;
            GameEvents.OnLivesChanged += HandleLivesChanged;
            GameEvents.OnTimerTick += HandleTimerTick;
            GameEvents.OnFruitCaught += HandleFruitCaught;
            GameEvents.OnGameOver += HandleGameOver;
            GameEvents.OnPowerupStarted += HandlePowerupStarted;
            GameEvents.OnPowerupEnded += HandlePowerupEnded;

            // Repaint labels if re-enabled mid session
            if (txtScore) txtScore.text = _score.ToString();
            if (txtLives) txtLives.text = _lives.ToString();
            if (txtBest) txtBest.text = $"Best: {BestCached}";
            RefreshGameOverTexts();
        }

        void OnDisable()
        {
            GameEvents.OnScoreChanged -= HandleScoreChanged;
            GameEvents.OnLivesChanged -= HandleLivesChanged;
            GameEvents.OnTimerTick -= HandleTimerTick;
            GameEvents.OnFruitCaught -= HandleFruitCaught;
            GameEvents.OnGameOver -= HandleGameOver;
            GameEvents.OnPowerupStarted -= HandlePowerupStarted;
            GameEvents.OnPowerupEnded -= HandlePowerupEnded;

            if (Instance == this) Instance = null;
        }

        void Update()
        {
            // Pulse the multiplier glow while active (unscaled time so Freeze doesn't affect)
            if (multiplierGlow && _multiplierActive)
            {
                _glowT += Time.unscaledDeltaTime * glowPulseSpeed;
                // Smooth oscillation 0..1
                float k = 0.5f * (Mathf.Sin(_glowT) + 1f);
                float a = Mathf.Lerp(glowMinAlpha, glowMaxAlpha, k);
                var c = multiplierGlow.color; c.a = a; multiplierGlow.color = c;
            }

            // Handle the "+X" bubble fade after the hold window
            if (txtScoreDelta && _deltaVisible)
            {
                _deltaTimer -= Time.unscaledDeltaTime;
                if (_deltaTimer <= 0f)
                {
                    float a = Mathf.Clamp01(1f + _deltaTimer / Mathf.Max(0.0001f, deltaFadeSeconds));
                    var c = _deltaBaseColor; c.a = a;
                    txtScoreDelta.color = c;

                    if (a <= 0f)
                        SetScoreDeltaVisible(false, resetAccum: true);
                }
            }
        }

        // ---------- event handlers ----------
        void HandleScoreChanged(int newScore)
        {
            _score = newScore;
            if (txtScore) txtScore.text = _score.ToString();

            // Keep HUD Best live if you show it
            if (txtBest) txtBest.text = $"Best: {BestCached}";
        }

        void HandleLivesChanged(int newLives)
        {
            _lives = newLives;
            if (txtLives) txtLives.text = _lives.ToString();
        }

        void HandleTimerTick(float elapsedSeconds)
        {
            if (!txtTimer) return;
            // Whole seconds; typical for arcade HUDs
            int total = Mathf.FloorToInt(elapsedSeconds);
            int m = total / 60;
            int s = total % 60;
            txtTimer.text = $"{m}:{s:00}";
        }

        void HandleFruitCaught(string id, int baseScore, bool isBomb)
        {
            // For the +X bubble we intentionally show BASE points (pre-multiplier).
            if (isBomb || !txtScoreDelta) return;

            _deltaAccum += baseScore;
            txtScoreDelta.text = $"{deltaPrefix}{_deltaAccum}";

            // Reset to fully visible each time a new catch happens
            var c = _deltaBaseColor; c.a = 1f;
            txtScoreDelta.color = c;

            _deltaVisible = true;
            _deltaTimer = deltaHoldSeconds;
        }

        void HandleGameOver()
        {
            // By now ScoreManager has saved Best if improved; read the cached value
            if (txtBest) txtBest.text = $"Best: {BestCached}";
            RefreshGameOverTexts();

            // Clear the +X bubble
            SetScoreDeltaVisible(false, resetAccum: true);

            // Stop glow immediately
            SetGlowActive(false, instant: true);
        }

        void HandlePowerupStarted(PowerupDef def)
        {
            if (def != null && def.kind == PowerupKind.ScoreMultiplier)
                SetGlowActive(true);
        }

        void HandlePowerupEnded(PowerupDef def)
        {
            if (def != null && def.kind == PowerupDef.PowerupKind.ScoreMultiplier)
                SetGlowActive(false);
        }

        // ---------- helpers ----------
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
                _glowT = 0f; // restart pulse
            }
            else
            {
                // Hide fully so the element doesn't look half-lit when inactive
                var c = multiplierGlow.color; c.a = 0f; multiplierGlow.color = c;
            }
        }

        void SetScoreDeltaVisible(bool on, bool resetAccum)
        {
            if (!txtScoreDelta) return;

            if (on)
            {
                var c = _deltaBaseColor; c.a = 1f;
                txtScoreDelta.color = c;
            }
            else
            {
                var c = _deltaBaseColor; c.a = 0f;
                txtScoreDelta.color = c;
                if (resetAccum)
                {
                    _deltaAccum = 0;
                    txtScoreDelta.text = "";
                }
            }

            _deltaVisible = on;
            _deltaTimer = on ? deltaHoldSeconds : 0f;
        }

        void RefreshGameOverTexts()
        {
            // Skip if nothing assigned (keeps console clean in editor)
            if (!goCurrentScoreText && !goBestScoreText) return;

            int bestToShow = Mathf.Max(BestCached, _score);
            if (goCurrentScoreText) goCurrentScoreText.text = $"Score: {_score}";
            if (goBestScoreText) goBestScoreText.text = $"Best: {bestToShow}";
        }

        /// <summary>
        /// Public hook: call after making the Game Over panel visible to force the latest
        /// numbers (useful if UI is enabled after GameOver already fired).
        /// </summary>
        public void ForceRefreshGameOverUI() => RefreshGameOverTexts();
    }
}
