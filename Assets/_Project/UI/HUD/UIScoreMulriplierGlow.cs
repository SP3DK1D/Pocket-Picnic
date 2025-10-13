using TMPro;
using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Temporarily tints score / delta TMP labels when Score Multiplier is active.
    /// - Caches baseline color & material instances once on enable (no leaks).
    /// - Restores baseline on power-up end or when this object disables.
    ///
    /// Important:
    /// Use <see cref="TMP_Text.fontMaterial"/> (instance) — NOT sharedMaterial — so we
    /// never mutate the shared asset used by other labels.
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
                // Cache the instance material — NOT sharedMaterial — to restore later
                _scoreBaseColor = scoreText.color;
                _scoreBaseMat = scoreText.fontMaterial;
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
            RestoreBaseline(); // ensure consistent look if the object is turned off mid-powerup
        }

        void OnPUStart(PowerupDef def)
        {
            if (def == null || def.kind != PowerupDef.PowerupKind.ScoreMultiplier) return;
            CacheBaseline();
            SetColor(x2Color);
        }

        void OnPUEnd(PowerupDef def)
        {
            if (def == null || def.kind != PowerupDef.PowerupKind.ScoreMultiplier) return;
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
                if (_scoreBaseMat) scoreText.fontMaterial = _scoreBaseMat;
            }

            if (scoreDeltaText)
            {
                scoreDeltaText.color = _deltaBaseColor;
                if (_deltaBaseMat) scoreDeltaText.fontMaterial = _deltaBaseMat;
            }
        }
    }
}
