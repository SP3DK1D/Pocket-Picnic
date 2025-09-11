using CatchTheFruit;
using UnityEngine;
using static CatchTheFruit.PowerupDef;

public class JuiceAudioListeners : MonoBehaviour
{
    [SerializeField] private GameEvents events; // assign in inspector, or will auto-find

    void Awake()
    {
        if (!events) events = FindObjectOfType<GameEvents>();
        if (!events) { Debug.LogWarning("JuiceAudioListeners: No GameEvents found."); return; }

        // Fruit
        events.OnFruitCaught += OnFruitCaught;
        events.OnFruitMissed += OnFruitMissed;

        // Power-ups
        events.OnPowerupPicked += OnPowerupPicked;
        events.OnPowerupStarted += OnPowerupStarted;
        events.OnPowerupEnded += OnPowerupEnded;

        // Uncomment if you want music tied to lifecycle:
        // events.OnGameStart += () => AudioHub.I?.StartMusic();
        // events.OnGameOver  += () => AudioHub.I?.StopMusic();
    }

    void OnDestroy()
    {
        if (!events) return;
        events.OnFruitCaught -= OnFruitCaught;
        events.OnFruitMissed -= OnFruitMissed;
        events.OnPowerupPicked -= OnPowerupPicked;
        events.OnPowerupStarted -= OnPowerupStarted;
        events.OnPowerupEnded -= OnPowerupEnded;
    }

    private void OnFruitCaught(string id, int baseScore, bool isBomb)
    {
        if (isBomb) AudioHub.I?.PlayBomb();
        else AudioHub.I?.PlayPickup();
    }

    private void OnFruitMissed(string id, bool isBomb, bool isPowerup)
    {
        if (isBomb) AudioHub.I?.PlayBomb();
    }

    private void OnPowerupPicked(PowerupDef def)
    {
        switch (def.kind)
        {
            case PowerupKind.TimeScale: AudioHub.I?.PlayFreezeOn(); break;
            case PowerupKind.ScoreMultiplier: AudioHub.I?.PlayPickup(); break;
            case PowerupKind.Magnet: AudioHub.I?.PlayPickup(); break;
            case PowerupKind.Shield: AudioHub.I?.PlayShieldOn(); break;
            case PowerupKind.ClearScreen: AudioHub.I?.PlayClear(); break;
        }
    }

    private void OnPowerupStarted(PowerupDef def) { }
    private void OnPowerupEnded(PowerupDef def)
    {
        if (def.kind == PowerupKind.TimeScale) AudioHub.I?.PlayFreezeOff();
    }
}
