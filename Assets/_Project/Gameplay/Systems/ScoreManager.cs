using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Owns the player's score, combo and power-up score multiplier for a single run.
    /// - Listens to GameEvents for catches/misses and power-up on/off.
    /// - Persists "Best" to PlayerPrefs on GameOver (and only writes when improved).
    /// - Exposes AddBulkPoints() for Clear-screen and milestone bonuses.
    ///
    /// Public surface stays compatible with the original:
    ///   - Instance (singleton), Score, Best
    ///   - enableCombo, comboEvery, comboBonus, verboseLogs (Inspector)
    ///   - AddBulkPoints(int)
    ///
    /// Design notes:
    /// - Reads PlayerPrefs "best" once at boot; keeps a cached copy to avoid repeated IO.
    /// - Only writes PlayerPrefs if the best actually improves (on GameOver).
    /// - Combo resets on bomb catch and normal-fruit miss (not on power-up or bomb miss).
    /// - Multiplier is controlled by Powerup start/end events (ScoreMultiplier type only).
    /// </summary>
    [DisallowMultipleComponent]
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        [Header("Combo (optional)")]
        [Tooltip("Enable simple combo bonuses. If off, combo is ignored.")]
        public bool enableCombo = true;

        [Tooltip("Grant a bonus every N consecutive non-bomb catches.")]
        [Min(1)] public int comboEvery = 5;

        [Tooltip("Bonus points granted each time the combo threshold is reached.")]
        public int comboBonus = 5;

        [Header("Debug")]
        public bool verboseLogs = false;

        // -------- Runtime state --------
        int _score;
        int _combo;
        float _multiplier = 1f;     // x1 by default; set by ScoreMultiplier power-up
        int _bestCached;            // cached PlayerPrefs "best" (loaded once, updated on over)

        // Key kept for backward compatibility with your project
        const string BestKey = "best";

        // Public accessors (read-only)
        public int Score => _score;
        public int Best => _bestCached;

        // ---------- Lifecycle ----------
        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // Read best once; keep it cached.
            _bestCached = PlayerPrefs.GetInt(BestKey, 0);
        }

        void OnEnable()
        {
            // Subscribe exactly once per enable. We do the symmetric remove in OnDisable.
            GameEvents.OnGameStart += ResetAll;
            GameEvents.OnFruitCaught += OnFruitCaught;
            GameEvents.OnFruitMissed += OnFruitMissed;
            GameEvents.OnPowerupStarted += OnPowerupStarted;
            GameEvents.OnPowerupEnded += OnPowerupEnded;
            GameEvents.OnGameOver += OnGameOverSaveBest;
        }

        void OnDisable()
        {
            GameEvents.OnGameStart -= ResetAll;
            GameEvents.OnFruitCaught -= OnFruitCaught;
            GameEvents.OnFruitMissed -= OnFruitMissed;
            GameEvents.OnPowerupStarted -= OnPowerupStarted;
            GameEvents.OnPowerupEnded -= OnPowerupEnded;
            GameEvents.OnGameOver -= OnGameOverSaveBest;

            if (Instance == this) Instance = null;
        }

        // ---------- Flow ----------
        void ResetAll()
        {
            _score = 0;
            _combo = 0;
            _multiplier = 1f;

            if (verboseLogs) Debug.Log("[Score] Reset. x1, Combo 0, Score 0");
            GameEvents.RaiseScoreChanged(_score);
        }

        void OnGameOverSaveBest()
        {
            // Persist only if improved; avoid unnecessary writes.
            if (_score > _bestCached)
            {
                _bestCached = _score;
                PlayerPrefs.SetInt(BestKey, _bestCached);
                PlayerPrefs.Save();
                if (verboseLogs) Debug.Log($"[Score] New BEST: {_bestCached}");
            }
            else if (verboseLogs)
            {
                if (_bestCached > 0)
                    Debug.Log($"[Score] Game Over. Score: {_score} | Best: {_bestCached}");
                else
                    Debug.Log($"[Score] Game Over. Score: {_score}");
            }
        }

        // ---------- Fruit events ----------
        void OnFruitCaught(string id, int baseScore, bool isBomb)
        {
            if (isBomb)
            {
                // No points; combo breaks if enabled.
                if (enableCombo) _combo = 0;
                if (verboseLogs) Debug.Log($"[Score] Caught bomb '{id}': no points. Combo reset.");
                return;
            }

            // Apply current multiplier to this catch.
            // Using RoundToInt for consistency with existing behavior.
            int gain = Mathf.RoundToInt(baseScore * _multiplier);
            _score += gain;

            // Combo bookkeeping
            if (enableCombo)
            {
                _combo++;
                if (comboEvery > 0 && (_combo % comboEvery) == 0)
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
            // Missing a normal fruit resets combo; missing bomb/powerup does nothing.
            if (!isBomb && !isPowerup && enableCombo)
            {
                _combo = 0;
                if (verboseLogs) Debug.Log($"[Score] Missed '{id}': combo reset.");
            }
        }

        // ---------- Power-up hooks ----------
        void OnPowerupStarted(PowerupDef def)
        {
            if (def == null) return;
            if (def.kind == PowerupDef.PowerupKind.ScoreMultiplier)
            {
                // Clamp to >= x1 just in case
                _multiplier = Mathf.Max(1f, def.scoreMultiplier);
                if (verboseLogs) Debug.Log($"[Score] Multiplier ON x{_multiplier}");
            }
        }

        void OnPowerupEnded(PowerupDef def)
        {
            if (def == null) return;
            if (def.kind == PowerupDef.PowerupKind.ScoreMultiplier)
            {
                _multiplier = 1f;
                if (verboseLogs) Debug.Log("[Score] Multiplier OFF (x1)");
            }
        }

        // ---------- Public: bulk points ----------
        /// <summary>
        /// Adds points in one go (applies current multiplier). Does NOT change combo.
        /// Used by Clear-screen and milestone bonuses.
        /// </summary>
        public void AddBulkPoints(int basePointsTotal)
        {
            if (basePointsTotal <= 0) return;
            int gain = Mathf.RoundToInt(basePointsTotal * _multiplier);
            _score += gain;

            if (verboseLogs) Debug.Log($"[Score] Bulk +{gain} (base {basePointsTotal}, x{_multiplier:0.##}) → {_score}");
            GameEvents.RaiseScoreChanged(_score);
        }
    }
}
