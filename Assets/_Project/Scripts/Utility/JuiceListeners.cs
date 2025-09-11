using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Haptics-only juice: vibrates on catches, bombs, and power-up start.
    /// No audio, no ScalePunch, no CameraShaker dependencies.
    /// </summary>
    [DisallowMultipleComponent]
    public class JuiceListeners : MonoBehaviour
    {
        [Header("Debug")]
        public bool verboseLogs = false;

        void OnEnable()
        {
            GameEvents.OnFruitCaught += OnCaught;
            GameEvents.OnPowerupStarted += OnPowerupStarted;
            // You can subscribe to more if you want (ScoreChanged/Missed), but not required.
        }

        void OnDisable()
        {
            GameEvents.OnFruitCaught -= OnCaught;
            GameEvents.OnPowerupStarted -= OnPowerupStarted;
        }

        void OnCaught(string id, int score, bool isBomb)
        {
            if (isBomb)
            {
                Haptics.PlayHeavy();                     // strong buzz for bombs
                if (verboseLogs) Debug.Log("[Juice] Bomb caught → Heavy haptic");
            }
            else
            {
                Haptics.PlayLight();                     // light buzz for good catch
                if (verboseLogs) Debug.Log("[Juice] Fruit caught → Light haptic");
            }
        }

        void OnPowerupStarted(PowerupDef def)
        {
            Haptics.PlayMedium();                        // medium buzz on any power-up start
            if (verboseLogs) Debug.Log($"[Juice] Power-up '{def?.id}' → Medium haptic");
        }
    }
}
