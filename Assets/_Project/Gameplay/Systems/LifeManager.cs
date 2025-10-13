using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Owns the player's lives for a single run.
    /// - Resets on GameStart (optionally from GameConfig.startingLives).
    /// - Decrements on bomb catch and on normal-fruit miss (powerups/bombs don't penalize on miss).
    /// - Raises GameOver exactly once when lives hit 0.
    /// - Always guarded by RunState.InGameplay so menu/background fruit never affect lives.
    /// </summary>
    [DisallowMultipleComponent]
    public class LifeManager : MonoBehaviour
    {
        [Header("Config (optional)")]
        [Tooltip("If assigned, this overrides 'startingLives' at GameStart.")]
        [SerializeField] private GameConfig config;

        [Header("Lives")]
        [Min(1)]
        [SerializeField] private int startingLives = 3;

        [Tooltip("If true, catching a bomb costs one life.")]
        [SerializeField] private bool loseLifeOnBombCatch = true;

        [Header("Debug")]
        [SerializeField] private bool verboseLogs = false;

        /// <summary>Current lives in this run.</summary>
        public int CurrentLives { get; private set; }

        /// <summary>Maximum lives allowed (uses GameConfig if assigned; otherwise startingLives).</summary>
        public int MaxLives => Mathf.Max(1, config ? config.startingLives : startingLives);

        // Prevents double GameOver when multiple events arrive in the same frame
        bool _gameOverRaised;

        // ---------------- Lifecycle ----------------
        void OnEnable()
        {
            GameEvents.OnGameStart += HandleGameStart;
            GameEvents.OnGameOver += HandleGameOver;
            GameEvents.OnFruitCaught += HandleFruitCaught;
            GameEvents.OnFruitMissed += HandleFruitMissed;

            // In case this component is enabled while already in a gameplay scene
            // but before GameStart fires, initialize UI with a sane value.
            InitializeLivesForUIOnly();
        }

        void OnDisable()
        {
            GameEvents.OnGameStart -= HandleGameStart;
            GameEvents.OnGameOver -= HandleGameOver;
            GameEvents.OnFruitCaught -= HandleFruitCaught;
            GameEvents.OnFruitMissed -= HandleFruitMissed;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (startingLives < 1) startingLives = 1;
        }
#endif

        // ---------------- Event handlers ----------------

        void HandleGameStart()
        {
            RunState.SetGameplay(true);
            _gameOverRaised = false;

            // Source of truth for starting lives: prefer GameConfig if assigned.
            int start = MaxLives; // already handles config-or-serialized
            CurrentLives = Mathf.Max(1, start);

            if (verboseLogs) Debug.Log($"[Lives] Reset → {CurrentLives}");
            RaiseLivesChanged();
        }

        void HandleGameOver()
        {
            // End the run; ignore fruit after this.
            RunState.SetGameplay(false);
        }

        void HandleFruitCaught(string id, int baseScore, bool isBomb)
        {
            if (!RunState.InGameplay) return;

            if (isBomb && loseLifeOnBombCatch)
            {
                LoseLife(1, reason: "Bomb catch");
            }
        }

        void HandleFruitMissed(string id, bool isBomb, bool isPowerup)
        {
            if (!RunState.InGameplay) return;

            // Rules:
            // - Missing a power-up: no penalty
            // - Missing a bomb: no penalty (bombs only penalize when caught)
            // - Missing a normal fruit: -1 life
            if (!isBomb && !isPowerup)
            {
                LoseLife(1, reason: "Normal fruit missed");
            }
        }

        // ---------------- Public API ----------------

        /// <summary>Adds life up to MaxLives. Does nothing if amount <= 0.</summary>
        public void AddLife(int amount = 1)
        {
            if (amount <= 0 || CurrentLives <= 0) return;

            int before = CurrentLives;
            CurrentLives = Mathf.Min(CurrentLives + amount, MaxLives);

            if (verboseLogs) Debug.Log($"[Lives] +{amount}: {before} → {CurrentLives}");
            RaiseLivesChanged();
        }

        /// <summary>Removes life; if reaches 0, raises GameOver once.</summary>
        public void LoseLife(int amount = 1, string reason = null)
        {
            if (amount <= 0 || CurrentLives <= 0) return;

            int before = CurrentLives;
            CurrentLives = Mathf.Max(0, CurrentLives - amount);

            if (verboseLogs)
            {
                string why = string.IsNullOrEmpty(reason) ? "" : $" ({reason})";
                Debug.Log($"[Lives] -{amount}: {before} → {CurrentLives}{why}");
            }

            RaiseLivesChanged();

            if (CurrentLives <= 0 && !_gameOverRaised)
            {
                _gameOverRaised = true;  // guard against multiple triggers
                GameEvents.RaiseGameOver();
            }
        }

        /// <summary>Directly sets lives (0..MaxLives). If set to 0, triggers GameOver once.</summary>
        public void SetLives(int value)
        {
            int clamped = Mathf.Clamp(value, 0, MaxLives);
            if (clamped == CurrentLives) return;

            CurrentLives = clamped;
            if (verboseLogs) Debug.Log($"[Lives] Set → {CurrentLives}");
            RaiseLivesChanged();

            if (CurrentLives <= 0 && !_gameOverRaised)
            {
                _gameOverRaised = true;
                GameEvents.RaiseGameOver();
            }
        }

        // ---------------- Helpers ----------------

        /// <summary>
        /// When the component enables before any GameStart, show something sensible on HUD.
        /// This avoids an initial "0" flicker on Lives UI in editors/menus.
        /// </summary>
        void InitializeLivesForUIOnly()
        {
            if (CurrentLives <= 0)
            {
                CurrentLives = MaxLives;
                RaiseLivesChanged();
            }
        }

        void RaiseLivesChanged() => GameEvents.RaiseLivesChanged(CurrentLives);
    }
}
