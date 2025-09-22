using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Tracks catch streaks. Announces milestones and grants flat bonus points.
    /// No haptics dependency (to avoid missing symbol errors).
    /// </summary>
    public class StreakManager : MonoBehaviour
    {
        [Header("Milestones (streak → bonus points)")]
        public int m1 = 10; public int bonus1 = 50;
        public int m2 = 20; public int bonus2 = 120;
        public int m3 = 30; public int bonus3 = 240;

        [Header("Announce")]
        public string milestoneMsg = "Streak {0}! +{1}";
        public float toastSeconds = 1.4f;

        int _streak, _best;

        void OnEnable()
        {
            GameEvents.OnFruitCaught += OnCaught;
            GameEvents.OnFruitMissed += OnMissed;
            GameEvents.OnGameStart += ResetRun;
            GameEvents.OnGameOver += ResetRun;
        }
        void OnDisable()
        {
            GameEvents.OnFruitCaught -= OnCaught;
            GameEvents.OnFruitMissed -= OnMissed;
            GameEvents.OnGameStart -= ResetRun;
            GameEvents.OnGameOver -= ResetRun;
        }

        void ResetRun()
        {
            _streak = 0;
            GameEvents.RaiseStreakChanged(_streak, _best);
        }

        void OnCaught(string id, int baseScore, bool isBomb)
        {
            if (isBomb) { BreakStreak(); return; }
            _streak++;
            if (_streak > _best) _best = _streak;
            GameEvents.RaiseStreakChanged(_streak, _best);

            if (_streak == m1) Grant(bonus1);
            else if (_streak == m2) Grant(bonus2);
            else if (_streak == m3) Grant(bonus3);
        }

        void OnMissed(string id, bool isBomb, bool isPowerup) => BreakStreak();

        void BreakStreak()
        {
            if (_streak <= 0) return;
            _streak = 0;
            GameEvents.RaiseStreakChanged(_streak, _best);
        }

        void Grant(int pts)
        {
            ScoreManager.Instance?.AddBulkPoints(pts);
            GameEvents.RaiseWaveMessage(string.Format(milestoneMsg, _streak, pts), toastSeconds);
        }
    }
}
