// Assets/_Project/Scripts/Data/PowerupDef.cs
using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// ScriptableObject that defines a single power-up's kind and tunables.
    /// Make one asset per kind (FD_Freeze, FD_Magnet, FD_Shield, FD_ScoreX2, FD_Clear).
    /// </summary>
    [CreateAssetMenu(menuName = "CatchTheFruit/Powerup")]
    public class PowerupDef : ScriptableObject
    {
        public enum PowerupKind
        {
            TimeScale,        // Freeze (slow falling; scales Physics2D.gravity)
            ScoreMultiplier,  // Multiply score events
            Magnet,           // Pull non-bomb fruits toward player
            Shield,           // Block one bomb (or time-limited if duration > 0)
            ClearScreen       // Clear all non-bomb fruits and award their base score once
        }

        [Header("Type")]
        public PowerupKind kind = PowerupKind.TimeScale;

        [Header("Shared")]
        [Tooltip("Duration in seconds. Shield: 0 means 'until consumed'. Clear: ignored.")]
        [Min(0f)] public float duration = 5f;

        [Header("Score Multiplier")]
        [Tooltip("x2, x3, ... Used only by ScoreMultiplier.")]
        [Min(1f)] public float scoreMultiplier = 2f;

        [Header("Freeze (TimeScale)")]
        [Tooltip("Gravity multiplier during Freeze (0.2 = ~20% fall speed). Used only by Freeze.")]
        [Range(0f, 1f)] public float freezeGravityScale = 0.20f;

        [Header("Magnet")]
        [Tooltip("World radius within which fruits start pulling. Used only by Magnet.")]
        [Min(0f)] public float magnetRadius = 5.5f;
        [Tooltip("Pull speed (units/sec). Used only by Magnet.")]
        [Min(0f)] public float magnetPullSpeed = 12f;
    }
}
