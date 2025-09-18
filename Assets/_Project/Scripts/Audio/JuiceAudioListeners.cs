using CatchTheFruit;
using UnityEngine;
using static CatchTheFruit.PowerupDef;

public class JuiceAudioListeners : MonoBehaviour
{
    void OnEnable()
    {
        GameEvents.OnFruitCaught += OnFruitCaught;
        GameEvents.OnPowerupPicked += OnPowerupPicked;
        GameEvents.OnPowerupEnded += OnPowerupEnded;
    }

    void OnDisable()
    {
        GameEvents.OnFruitCaught -= OnFruitCaught;
        GameEvents.OnPowerupPicked -= OnPowerupPicked;
        GameEvents.OnPowerupEnded -= OnPowerupEnded;
    }

    // Only play sounds when fruit is caught
    void OnFruitCaught(string id, int score, bool isBomb)
    {
        if (isBomb) AudioHub.I?.PlayBomb();
        else AudioHub.I?.PlayPickup();
    }

    // Don’t play bomb on miss anymore

    void OnPowerupPicked(PowerupDef def)
    {
        switch (def.kind)
        {
            case PowerupKind.TimeScale: AudioHub.I?.PlayFreezeOn(); break;
            case PowerupKind.ScoreMultiplier: AudioHub.I?.PlayPickup(); break;
            case PowerupKind.Magnet: AudioHub.I?.PlayMagnet(); break;
            case PowerupKind.Shield: AudioHub.I?.PlayShieldOn(); break;
            case PowerupKind.ClearScreen: AudioHub.I?.PlayClear(); break;
        }
    }

    void OnPowerupEnded(PowerupDef def)
    {
        if (def.kind == PowerupKind.TimeScale)
            AudioHub.I?.PlayFreezeOff();
    }
}
