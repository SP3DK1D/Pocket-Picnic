// Assets/_Project/Scripts/Systems/GameEvents.cs
using System;

namespace CatchTheFruit
{
    public static class GameEvents
    {
        // Flow
        public static event Action OnGameStart;
        public static event Action OnGameOver;

        // Values
        public static event Action<int> OnScoreChanged;
        public static event Action<int> OnLivesChanged;

        // Fruit outcomes
        public static event Action<string, int, bool> OnFruitCaught;      // (id, score, isBomb)
        public static event Action<string, bool, bool> OnFruitMissed;     // (id, isBomb, isPowerup)

        // Timer
        public static event Action<float> OnTimerTick;

        // Powerups (generic)
        public static event Action<PowerupDef> OnPowerupPicked;
        public static event Action<PowerupDef> OnPowerupStarted;
        public static event Action<PowerupDef> OnPowerupEnded;

        // Raisers
        public static void RaiseGameStart() => OnGameStart?.Invoke();
        public static void RaiseGameOver()  => OnGameOver?.Invoke();

        public static void RaiseScoreChanged(int s) => OnScoreChanged?.Invoke(s);
        public static void RaiseLivesChanged(int l) => OnLivesChanged?.Invoke(l);

        public static void RaiseFruitCaught(string id, int score, bool isBomb)
            => OnFruitCaught?.Invoke(id, score, isBomb);

        public static void RaiseFruitMissed(string id, bool isBomb, bool isPowerup)
            => OnFruitMissed?.Invoke(id, isBomb, isPowerup);

        public static void RaiseTimerTick(float t) => OnTimerTick?.Invoke(t);

        public static void RaisePowerupPicked(PowerupDef def)  => OnPowerupPicked?.Invoke(def);
        public static void RaisePowerupStarted(PowerupDef def) => OnPowerupStarted?.Invoke(def);
        public static void RaisePowerupEnded(PowerupDef def)   => OnPowerupEnded?.Invoke(def);

        // Meta/UI (as you had)
        public static event Action<string, float> OnWaveMessage;
        public static void RaiseWaveMessage(string msg, float seconds = 1.6f) => OnWaveMessage?.Invoke(msg, seconds);

        public static event Action<int, int> OnStreakChanged;
        public static void RaiseStreakChanged(int current, int best) => OnStreakChanged?.Invoke(current, best);

        public enum ChallengeKind { None, BananaBlitz, BombStorm, GoldenTime }
        public static event Action<ChallengeKind> OnChallengeStarted;
        public static event Action<ChallengeKind> OnChallengeEnded;
        public static void RaiseChallengeStarted(ChallengeKind k) => OnChallengeStarted?.Invoke(k);
        public static void RaiseChallengeEnded(GameEvents.ChallengeKind k) => OnChallengeEnded?.Invoke(k);
    }
}
