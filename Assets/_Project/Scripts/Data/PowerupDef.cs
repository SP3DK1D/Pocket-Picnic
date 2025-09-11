using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Declarative power-up asset picked by FruitData.
    /// Set the correct 'kind' on each asset (Freeze, Magnet, etc).
    /// </summary>
    [CreateAssetMenu(menuName = "CatchTheFruit/Powerup")]
    public class PowerupDef : ScriptableObject
    {
        public enum PowerupKind
        {
            TimeScale,       // Freeze/slow motion
            ScoreMultiplier, // e.g., x2 for N sec
            Shield,          // Ignore next bomb (or timed)
            Magnet,          // Pull nearby fruits toward player
            ClearScreen      // Instantly remove all fruits
        }

        public enum OverlapPolicy { RefreshDuration, Replace, Ignore }

        [Header("Identity")]
        public string id = "PU_Freeze";
        public PowerupKind kind = PowerupKind.TimeScale;
        public OverlapPolicy overlapPolicy = OverlapPolicy.RefreshDuration;
        [Min(0f)] public float duration = 2.5f;   // 0 => instant effect (no timer)
        public Sprite icon;                       // HUD badge icon (optional)

        [Header("Overlay (optional)")]
        public Color overlayColor = new Color(0.47f, 0.78f, 1f, 1f);
        [Range(0f, 1f)] public float overlayAlpha = 0.35f; // set 0 for no tint
        [Range(0f, 2f)] public float overlayFade = 0.15f;

        [Header("Params by Kind")]
        [Range(0.05f, 1f)] public float timeScale = 0.2f;  // TimeScale
        [Min(1f)] public float scoreMultiplier = 2f;       // ScoreMultiplier
        [Min(0f)] public float magnetRadius = 5f;          // Magnet (world units)
        [Min(0f)] public float magnetPullSpeed = 12f;      // Magnet (units/sec)
    }
}
/*
Unity (verify assets):
- PU_Freeze: kind=TimeScale, timeScale=0.2, duration≈2.5, overlayAlpha≈0.35 (blue).
- PU_Magnet: kind=Magnet, radius≈5–6, pull≈12–14, duration≈6–8, overlayAlpha=0 (no screen tint).
- PU_ScoreX2: kind=ScoreMultiplier, scoreMultiplier=2, duration≈6–8, overlayAlpha=0.
- PU_Shield: kind=Shield, duration=0 (until used), overlayAlpha=0.
- PU_Clear: kind=ClearScreen, duration=0, overlayAlpha 0–0.2 if you want a quick flash.

Make sure each FruitData that should give Magnet points to PU_Magnet (not PU_Freeze).
*/
