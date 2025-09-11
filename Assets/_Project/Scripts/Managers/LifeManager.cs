using UnityEngine;

namespace CatchTheFruit
{
    public class LifeManager : MonoBehaviour
    {
        [SerializeField] private GameConfig config;
        private int _lives;

        private void OnEnable()
        {
            GameEvents.OnGameStart += ResetLives;
            GameEvents.OnFruitMissed += HandleMissed;   // (id, isBomb, isPowerup)
            GameEvents.OnFruitCaught += HandleCaught;   // (id, score, isBomb)
        }
        private void OnDisable()
        {
            GameEvents.OnGameStart -= ResetLives;
            GameEvents.OnFruitMissed -= HandleMissed;
            GameEvents.OnFruitCaught -= HandleCaught;
        }

        private void ResetLives()
        {
            _lives = config ? config.startingLives : 3;
            GameEvents.RaiseLivesChanged(_lives);
            Debug.Log($"[Lives] Reset: {_lives}");
        }

        private void HandleMissed(string id, bool isBomb, bool isPowerup)
        {
            if (isBomb || isPowerup) return; // no penalty for missed bombs/powerups
            ModifyLives(-1, $"Missed {id}");
        }

        private void HandleCaught(string id, int score, bool isBomb)
        {
            if (!isBomb) return;

            // Shield active? consume instead of losing a life.
            if (PowerupManager.ShieldActive)
            {
                PowerupManager.ConsumeShield();
                Debug.Log("[Lives] Bomb caught but Shield consumed it!");
                return;
            }

            ModifyLives(-1, "Caught Bomb");
        }

        private void ModifyLives(int delta, string reason)
        {
            _lives += delta;
            GameEvents.RaiseLivesChanged(_lives);
            Debug.Log($"[Lives] {reason} (Δ{delta}) -> now: {_lives}");
            if (_lives <= 0) GameEvents.RaiseGameOver();
        }
    }
}
/*
Unity: no changes in Inspector. Save & recompile.
*/
