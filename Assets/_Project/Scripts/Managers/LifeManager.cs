using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Owns the player's lives for a single run.
    /// - Resets on GameStart.
    /// - Decrements on bomb catch or normal-fruit miss (configurable).
    /// - Raises GameOver when lives reach 0.
    /// - Always guarded by RunState.InGameplay so menu/background fruit never affect lives.
    /// </summary>
    public class LifeManager : MonoBehaviour
    {
        [Header("Lives")]
        [Min(1)]
        [SerializeField] private int startingLives = 3;

        [Tooltip("If true, catching a bomb costs one life.")]
        [SerializeField] private bool loseLifeOnBombCatch = true;

        [Header("Debug")]
        [SerializeField] private bool verboseLogs = false;

        public int CurrentLives { get; private set; }
        public int MaxLives => startingLives;

        // ---------------- Lifecycle ----------------
        private void OnEnable()
        {
            GameEvents.OnGameStart += HandleGameStart;
            GameEvents.OnGameOver += HandleGameOver;
            GameEvents.OnFruitCaught += HandleFruitCaught;
            GameEvents.OnFruitMissed += HandleFruitMissed;
        }

        private void OnDisable()
        {
            GameEvents.OnGameStart -= HandleGameStart;
            GameEvents.OnGameOver -= HandleGameOver;
            GameEvents.OnFruitCaught -= HandleFruitCaught;
            GameEvents.OnFruitMissed -= HandleFruitMissed;
        }

        private void Start()
        {
            // If you enter Play while already in a gameplay scene that fires GameStart later,
            // we initialize UI right away with starting lives (no gameplay effects).
            CurrentLives = startingLives;
            RaiseLivesChanged();
        }

        // ---------------- Event handlers ----------------
        private void HandleGameStart()
        {
            // Begin a new run
            RunState.SetGameplay(true);
            CurrentLives = startingLives;
            if (verboseLogs) Debug.Log($"[Lives] Reset: {CurrentLives}");
            RaiseLivesChanged();
        }

        private void HandleGameOver()
        {
            // End the run; stop reacting to fruit while menu/gameover is up
            RunState.SetGameplay(false);
        }

        private void HandleFruitCaught(string id, int baseScore, bool isBomb)
        {
            if (!RunState.InGameplay) return;

            if (isBomb && loseLifeOnBombCatch)
            {
                LoseLife(1, reason: "Bomb catch");
            }
        }

        private void HandleFruitMissed(string id, bool isBomb, bool isPowerup)
        {
            if (!RunState.InGameplay) return;

            // Rules:
            // - Missing a power-up does NOT cost a life
            // - Missing a normal fruit (not bomb, not power-up) costs one life
            if (!isBomb && !isPowerup)
            {
                LoseLife(1, reason: "Normal fruit missed");
            }
        }

        // ---------------- Public API ----------------
        public void AddLife(int amount = 1)
        {
            if (amount <= 0) return;
            int before = CurrentLives;
            CurrentLives = Mathf.Clamp(CurrentLives + amount, 0, MaxLives);
            if (verboseLogs) Debug.Log($"[Lives] +{amount}: {before} → {CurrentLives}");
            RaiseLivesChanged();
        }

        public void LoseLife(int amount = 1, string reason = null)
        {
            if (amount <= 0) return;

            int before = CurrentLives;
            CurrentLives = Mathf.Max(0, CurrentLives - amount);

            if (verboseLogs)
            {
                string why = string.IsNullOrEmpty(reason) ? "" : $" ({reason})";
                Debug.Log($"[Lives] -{amount}: {before} → {CurrentLives}{why}");
            }

            RaiseLivesChanged();

            if (CurrentLives <= 0)
            {
                // Trigger end of run once.
                GameEvents.RaiseGameOver();
            }
        }

        public void SetLives(int value)
        {
            int clamped = Mathf.Clamp(value, 0, MaxLives);
            if (clamped == CurrentLives) return;
            CurrentLives = clamped;
            if (verboseLogs) Debug.Log($"[Lives] Set: {CurrentLives}");
            RaiseLivesChanged();

            if (CurrentLives <= 0)
                GameEvents.RaiseGameOver();
        }

        // ---------------- Helpers ----------------
        private void RaiseLivesChanged()
        {
            GameEvents.RaiseLivesChanged(CurrentLives);
        }
    }
}
