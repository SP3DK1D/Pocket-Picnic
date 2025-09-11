using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Tracks current score, (optional) combo bonus, and a score multiplier from power-ups.
    /// Saves "best" to PlayerPrefs on GameOver and emits score change events for UI.
    /// Also exposes AddBulkPoints() so Clear power-up can award all on-screen fruit at once.
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        [Header("Combo (optional)")]
        [Tooltip("Enable simple combo bonuses. If off, combo is ignored.")]
        public bool enableCombo = true;
        [Tooltip("Grant a bonus every N consecutive non-bomb catches.")]
        public int comboEvery = 5;
        [Tooltip("Bonus points granted each time the combo threshold is reached.")]
        public int comboBonus = 5;

        [Header("Debug")]
        public bool verboseLogs = false;

        // Runtime
        int _score;
        int _combo;
        float _multiplier = 1f;

        const string BestKey = "best";

        public int Score => _score;
        public int Best => PlayerPrefs.GetInt(BestKey, 0);

        void Awake() => Instance = this;

        void OnEnable()
        {
            GameEvents.OnGameStart += ResetAll;
            GameEvents.OnFruitCaught += OnFruitCaught;
            GameEvents.OnFruitMissed += OnFruitMissed;
            GameEvents.OnPowerupStarted += OnPowerupStarted;
            GameEvents.OnPowerupEnded += OnPowerupEnded;
            GameEvents.OnGameOver += SaveHighScore;
        }

        void OnDisable()
        {
            GameEvents.OnGameStart -= ResetAll;
            GameEvents.OnFruitCaught -= OnFruitCaught;
            GameEvents.OnFruitMissed -= OnFruitMissed;
            GameEvents.OnPowerupStarted -= OnPowerupStarted;
            GameEvents.OnPowerupEnded -= OnPowerupEnded;
            GameEvents.OnGameOver -= SaveHighScore;
            if (Instance == this) Instance = null;
        }

        // ----- Flow -----

        void ResetAll()
        {
            _score = 0;
            _combo = 0;
            _multiplier = 1f;

            if (verboseLogs) Debug.Log("[Score] Reset. Multiplier x1, Combo 0, Score 0");
            GameEvents.RaiseScoreChanged(_score);
        }

        void OnFruitCaught(string id, int baseScore, bool isBomb)
        {
            if (isBomb)
            {
                if (enableCombo) _combo = 0; // bombs never add points
                if (verboseLogs) Debug.Log($"[Score] Caught bomb '{id}': no points. Combo reset.");
                return;
            }

            int gain = Mathf.RoundToInt(baseScore * _multiplier);
            _score += gain;

            if (enableCombo)
            {
                _combo++;
                if (comboEvery > 0 && _combo > 0 && (_combo % comboEvery) == 0)
                {
                    _score += comboBonus;
                    if (verboseLogs) Debug.Log($"[Score] Combo {_combo}! +{comboBonus} bonus");
                }
            }

            if (verboseLogs)
                Debug.Log($"[Score] Catch '{id}': +{gain} (x{_multiplier:0.##}) → {_score}");

            GameEvents.RaiseScoreChanged(_score);
        }

        void OnFruitMissed(string id, bool isBomb, bool isPowerup)
        {
            // Missing normal fruit resets combo; missing bomb/powerup does nothing.
            if (!isBomb && !isPowerup && enableCombo)
            {
                _combo = 0;
                if (verboseLogs) Debug.Log($"[Score] Missed '{id}': combo reset.");
            }
        }

        // ----- Power-ups -----

        void OnPowerupStarted(PowerupDef def)
        {
            if (def.kind == PowerupDef.PowerupKind.ScoreMultiplier)
            {
                _multiplier = Mathf.Max(1f, def.scoreMultiplier);
                if (verboseLogs) Debug.Log($"[Score] Multiplier ON x{_multiplier}");
            }
        }

        void OnPowerupEnded(PowerupDef def)
        {
            if (def.kind == PowerupDef.PowerupKind.ScoreMultiplier)
            {
                _multiplier = 1f;
                if (verboseLogs) Debug.Log("[Score] Multiplier OFF (x1)");
            }
        }

        // ----- Bulk points for Clear -----

        /// <summary>
        /// Adds points in one go (applies current multiplier). Does NOT change combo.
        /// </summary>
        public void AddBulkPoints(int basePointsTotal)
        {
            if (basePointsTotal <= 0) return;
            int gain = Mathf.RoundToInt(basePointsTotal * _multiplier);
            _score += gain;
            if (verboseLogs) Debug.Log($"[Score] Clear bonus +{gain} (base {basePointsTotal}, x{_multiplier:0.##}) → {_score}");
            GameEvents.RaiseScoreChanged(_score);
        }

        // ----- Best (high score) -----

        void SaveHighScore()
        {
            int best = PlayerPrefs.GetInt(BestKey, 0);
            if (_score > best)
            {
                PlayerPrefs.SetInt(BestKey, _score);
                PlayerPrefs.Save();
                if (verboseLogs) Debug.Log($"[Score] New BEST: {_score}");
            }
            else if (verboseLogs)
            {
                Debug.Log($"[Score] Game Over. Score: {_score} | Best: {best}");
            }
        }
    }
}
