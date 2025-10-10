using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>Bridges GameEvents to AudioManager (no AudioHub).</summary>
    public class JuiceAudioListeners : MonoBehaviour
    {
        void OnEnable()
        {
            GameEvents.OnPowerupStarted += OnPowerupStarted;
            GameEvents.OnPowerupEnded += OnPowerupEnded;
            GameEvents.OnFruitCaught += OnFruitCaught;
            GameEvents.OnFruitMissed += OnFruitMissed;
            GameEvents.OnGameStart += OnGameStart;
            GameEvents.OnGameOver += OnGameOver;
        }

        void OnDisable()
        {
            GameEvents.OnPowerupStarted -= OnPowerupStarted;
            GameEvents.OnPowerupEnded -= OnPowerupEnded;
            GameEvents.OnFruitCaught -= OnFruitCaught;
            GameEvents.OnFruitMissed -= OnFruitMissed;
            GameEvents.OnGameStart -= OnGameStart;
            GameEvents.OnGameOver -= OnGameOver;
        }

        void OnPowerupStarted(PowerupDef def)
        {
            if (!def) return;
            var A = AudioManager.I; if (A == null) return;

            switch (def.kind)
            {
                case PowerupDef.PowerupKind.TimeScale: A.PlaySFX(A.sfxFreezeStart); break;
                case PowerupDef.PowerupKind.ScoreMultiplier: A.PlaySFX(A.sfxScoreStart); break;
                case PowerupDef.PowerupKind.Magnet: A.PlaySFX(A.sfxMagnetStart); break;
                case PowerupDef.PowerupKind.Shield: A.PlaySFX(A.sfxShieldOn); break;
                case PowerupDef.PowerupKind.ClearScreen: A.PlaySFX(A.sfxClearBurst); break;
            }
        }

        void OnPowerupEnded(PowerupDef def) { /* no-op; add sounds if desired */ }

        void OnFruitCaught(string id, int score, bool isBomb)
        {
            var A = AudioManager.I; if (A == null) return;
            if (isBomb) A.PlaySFX(A.sfxBomb);
            else A.PlaySFX(A.sfxCatch);
        }

        void OnFruitMissed(string id, bool isBomb, bool isPowerup) { /* optional */ }
        void OnGameStart() { /* optional */ }
        void OnGameOver() { /* optional */ }
    }
}
