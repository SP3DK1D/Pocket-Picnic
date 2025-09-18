using UnityEngine;
using TMPro;

namespace CatchTheFruit
{
    /// <summary>
    /// While ScoreMultiplier is active, tint score and delta labels gold.
    /// </summary>
    public class UIScoreMultiplierGlow : MonoBehaviour
    {
        public TMP_Text scoreText;       // e.g., TXT_Score
        public TMP_Text scoreDeltaText;  // e.g., TXT_ScoreDelta (optional)

        public Color normalColor = Color.black;
        public Color x2Color = new Color(1f, 0.84f, 0.1f); // gold

        bool _active;

        void OnEnable()
        {
            GameEvents.OnPowerupStarted += OnPUStart;
            GameEvents.OnPowerupEnded += OnPUEnd;
        }
        void OnDisable()
        {
            GameEvents.OnPowerupStarted -= OnPUStart;
            GameEvents.OnPowerupEnded -= OnPUEnd;
            SetColor(normalColor);
        }

        void OnPUStart(PowerupDef def)
        {
            if (def.kind != PowerupDef.PowerupKind.ScoreMultiplier) return;
            _active = true;
            SetColor(x2Color);
        }
        void OnPUEnd(PowerupDef def)
        {
            if (def.kind != PowerupDef.PowerupKind.ScoreMultiplier) return;
            _active = false;
            SetColor(normalColor);
        }

        void SetColor(Color c)
        {
            if (scoreText) scoreText.color = c;
            if (scoreDeltaText) scoreDeltaText.color = c;
        }
    }
}
