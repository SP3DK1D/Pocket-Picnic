using TMPro;
using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Tints score and (optionally) delta text while ScoreMultiplier is active,
    /// then cleanly restores the original color/material on end/disable.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIScoreMultiplierGlow : MonoBehaviour
    {
        [Header("Assign")]
        public TMP_Text scoreText;       // e.g., TXT_Score
        public TMP_Text scoreDeltaText;  // optional

        [Header("Multiplier Style")]
        public Color x2Color = new Color(1f, 0.84f, 0.10f); // gold

        // cached baseline so we never guess "normal" again
        Color _scoreBaseColor, _deltaBaseColor;
        Material _scoreBaseMat, _deltaBaseMat;
        bool _cached;

        void CacheBaseline()
        {
            if (_cached) return;
            if (scoreText)
            {
                _scoreBaseColor = scoreText.color;
                _scoreBaseMat = scoreText.fontMaterial;    // instance material, not shared
            }
            if (scoreDeltaText)
            {
                _deltaBaseColor = scoreDeltaText.color;
                _deltaBaseMat = scoreDeltaText.fontMaterial;
            }
            _cached = true;
        }

        void OnEnable()
        {
            CacheBaseline();
            GameEvents.OnPowerupStarted += OnPUStart;
            GameEvents.OnPowerupEnded += OnPUEnd;
        }

        void OnDisable()
        {
            GameEvents.OnPowerupStarted -= OnPUStart;
            GameEvents.OnPowerupEnded -= OnPUEnd;
            RestoreBaseline();
        }

        void OnPUStart(PowerupDef def)
        {
            if (def.kind != PowerupDef.PowerupKind.ScoreMultiplier) return;
            CacheBaseline();
            SetColor(x2Color);
        }

        void OnPUEnd(PowerupDef def)
        {
            if (def.kind != PowerupDef.PowerupKind.ScoreMultiplier) return;
            RestoreBaseline();
        }

        void SetColor(Color c)
        {
            if (scoreText) scoreText.color = c;
            if (scoreDeltaText) scoreDeltaText.color = c;
        }

        void RestoreBaseline()
        {
            if (scoreText)
            {
                scoreText.color = _scoreBaseColor;
                if (_scoreBaseMat) scoreText.fontMaterial = _scoreBaseMat; // undo any preset tweaks
            }
            if (scoreDeltaText)
            {
                scoreDeltaText.color = _deltaBaseColor;
                if (_deltaBaseMat) scoreDeltaText.fontMaterial = _deltaBaseMat;
            }
        }
    }
}
