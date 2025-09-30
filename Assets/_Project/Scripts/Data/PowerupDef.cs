using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Simple power-up definition referenced by FruitData and raised via GameEvents.
    /// PowerupManager/VFXManager mainly use the <see cref="PowerupKind"/> value.
    /// </summary>
    [CreateAssetMenu(menuName = "CatchTheFruit/Powerup")]
    public class PowerupDef : ScriptableObject
    {
        public enum PowerupKind
        {
            TimeScale,        // Freeze / slow-time
            ScoreMultiplier,  // x2, x3, etc.
            Magnet,           // fruit pulls to player
            Shield,           // blocks one bomb
            ClearScreen       // clears all fruits
        }

        [Header("Type")]
        public PowerupKind kind = PowerupKind.TimeScale;

        [Header("Optional Tunables (not all are used)")]
        [Min(0f)] public float duration = 5f;     // generic duration if desired
        [Min(1f)] public float scoreMultiplier = 2f;
    }
}
