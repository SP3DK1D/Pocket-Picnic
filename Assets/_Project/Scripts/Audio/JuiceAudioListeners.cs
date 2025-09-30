using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Bridges GameEvents to AudioHub. Keeps SFX policy centralized.
    /// Updated to remove calls to PlayFreezeOff() and PlayShieldHit().
    /// Shield break SFX is now played inside PowerupManager when consumed by a hit.
    /// </summary>
    public class JuiceAudioListeners : MonoBehaviour
    {
        void OnEnable()
        {
            GameEvents.OnPowerupStarted += OnPowerupStarted;
            GameEvents.OnPowerupEnded   += OnPowerupEnded;
            GameEvents.OnFruitCaught    += OnFruitCaught;
            GameEvents.OnFruitMissed    += OnFruitMissed;
            GameEvents.OnGameStart      += OnGameStart;
            GameEvents.OnGameOver       += OnGameOver;
        }

        void OnDisable()
        {
            GameEvents.OnPowerupStarted -= OnPowerupStarted;
            GameEvents.OnPowerupEnded   -= OnPowerupEnded;
            GameEvents.OnFruitCaught    -= OnFruitCaught;
            GameEvents.OnFruitMissed    -= OnFruitMissed;
            GameEvents.OnGameStart      -= OnGameStart;
            GameEvents.OnGameOver       -= OnGameOver;
        }

        // --- Powerups ---
        void OnPowerupStarted(PowerupDef def)
        {
            if (def == null || AudioHub.I == null) return;

            switch (def.kind)
            {
                case PowerupDef.PowerupKind.TimeScale:
                    // Freeze ON sound only. (No "freeze off" anymore.)
                    AudioHub.I.PlayFreezeOn();
                    break;

                case PowerupDef.PowerupKind.ScoreMultiplier:
                    AudioHub.I.PlayScoreMultiplier();
                    break;

                case PowerupDef.PowerupKind.Magnet:
                    AudioHub.I.PlayMagnet();
                    break;

                case PowerupDef.PowerupKind.Shield:
                    // Shield ON (shield break is handled inside PowerupManager when consumed)
                    AudioHub.I.PlayShieldOn();
                    break;

                case PowerupDef.PowerupKind.ClearScreen:
                    // Play on start (instant effect)
                    AudioHub.I.PlayClear();
                    break;
            }
        }

        void OnPowerupEnded(PowerupDef def)
        {
            // Deliberately minimal:
            // - No FreezeOff sound (removed from AudioHub by request).
            // - No ShieldHit sound (shield break SFX is played by PowerupManager exactly on consume).
            // Keep this for future “end” sounds if needed.
        }

        // --- Fruit outcomes ---
        void OnFruitCaught(string id, int score, bool isBomb)
        {
            if (AudioHub.I == null) return;
            if (isBomb)
                AudioHub.I.PlayBomb();
            else
                AudioHub.I.PlayPickup();
        }

        void OnFruitMissed(string id, bool isBomb, bool isPowerup)
        {
            // No sound by default; easy to add policy later if you want.
        }

        // --- Flow ---
        void OnGameStart()
        {
            // No sound by default.
        }

        void OnGameOver()
        {
            // No sound by default.
        }
    }
}
