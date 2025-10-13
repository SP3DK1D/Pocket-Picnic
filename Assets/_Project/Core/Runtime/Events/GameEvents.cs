// Assets/_Project/Scripts/Systems/GameEvents.cs
using System;
using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Central event bus for lightweight game signals.
    /// <para>All events are intended for main-thread (Unity) usage.</para>
    /// </summary>
    public static class GameEvents
    {
        // -----------------------------
        // Types
        // -----------------------------
        public enum ChallengeKind { None = 0, BananaBlitz = 1, BombStorm = 2, GoldenTime = 3 }

        // -----------------------------
        // Lifecycle
        // -----------------------------
        public static event Action OnGameStart;
        public static event Action OnGameOver;

        /// <summary>Begin a new run.</summary>
        public static void RaiseGameStart()
        {
            RunState.SetGameplay(true);
            OnGameStart?.Invoke();
        }

        /// <summary>End the current run.</summary>
        public static void RaiseGameOver()
        {
            RunState.SetGameplay(false);
            OnGameOver?.Invoke();
        }

        // -----------------------------
        // Score & Lives
        // -----------------------------
        public static event Action<int> OnScoreChanged;
        public static event Action<int> OnLivesChanged;

        public static void RaiseScoreChanged(int score) => OnScoreChanged?.Invoke(score);
        public static void RaiseLivesChanged(int lives) => OnLivesChanged?.Invoke(lives);

        // -----------------------------
        // Fruits (catch/miss)
        // -----------------------------
        /// <summary>
        /// Fired whenever a fruit collider is processed as “caught”.
        /// </summary>
        /// <param name="id">FruitData.id string (or "?" when unknown).</param>
        /// <param name="baseScore">Fruit base score (pre-multiplier).</param>
        /// <param name="isBomb">True if this fruit is a bomb.</param>
        public static event Action<string, int, bool> OnFruitCaught;

        /// <summary>
        /// Fired when a fruit is considered “missed” (fell past killY/offscreen).
        /// </summary>
        /// <param name="id">FruitData.id string (or "?" when unknown).</param>
        /// <param name="isBomb">True if it was a bomb.</param>
        /// <param name="isPowerup">True if it carried a power-up.</param>
        public static event Action<string, bool, bool> OnFruitMissed;

        public static void RaiseFruitCaught(string id, int baseScore, bool isBomb)
            => OnFruitCaught?.Invoke(id, baseScore, isBomb);

        public static void RaiseFruitMissed(string id, bool isBomb, bool isPowerup)
            => OnFruitMissed?.Invoke(id, isBomb, isPowerup);

        // -----------------------------
        // Power-ups
        // -----------------------------
        public static event Action<PowerupDef> OnPowerupPicked;   // Player picked up a power-up carrier
        public static event Action<PowerupDef> OnPowerupStarted;  // Effect began (freeze, magnet, etc.)
        public static event Action<PowerupDef> OnPowerupEnded;    // Effect ended

        public static void RaisePowerupPicked(PowerupDef def) => OnPowerupPicked?.Invoke(def);
        public static void RaisePowerupStarted(PowerupDef def) => OnPowerupStarted?.Invoke(def);
        public static void RaisePowerupEnded(PowerupDef def) => OnPowerupEnded?.Invoke(def);

        // -----------------------------
        // Challenges & Announcements
        // -----------------------------
        /// <summary>Plain banner/toast message. Keep text UI-safe (font glyph coverage)</summary>
        public static event Action<string, float> OnWaveMessage;
        public static void RaiseWaveMessage(string message, float seconds = 1.4f)
            => OnWaveMessage?.Invoke(message, seconds);

        public static event Action<ChallengeKind> OnChallengeStarted;
        public static event Action<ChallengeKind> OnChallengeEnded;

        public static void RaiseChallengeStarted(ChallengeKind kind) => OnChallengeStarted?.Invoke(kind);
        public static void RaiseChallengeEnded(ChallengeKind kind) => OnChallengeEnded?.Invoke(kind);

        // -----------------------------
        // Timer (unscaled)
        // -----------------------------
        public static event Action<float> OnTimerTick;
        public static void RaiseTimerTick(float elapsedSeconds) => OnTimerTick?.Invoke(elapsedSeconds);

        // -----------------------------
        // Housekeeping
        // -----------------------------
        /// <summary>
        /// Clears all subscribers on domain reload. Helps avoid dangling references
        /// when entering play mode repeatedly in the Editor.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAll()
        {
            OnGameStart = null;
            OnGameOver = null;

            OnScoreChanged = null;
            OnLivesChanged = null;

            OnFruitCaught = null;
            OnFruitMissed = null;

            OnPowerupPicked = null;
            OnPowerupStarted = null;
            OnPowerupEnded = null;

            OnWaveMessage = null;
            OnChallengeStarted = null;
            OnChallengeEnded = null;

            OnTimerTick = null;
        }
    }
}
