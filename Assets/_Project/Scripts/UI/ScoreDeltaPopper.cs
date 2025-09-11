using UnityEngine;
using TMPro;
using UnityEngine;
using TMPro;
using System.Collections;

namespace CatchTheFruit
{
    /// <summary>
    /// Shows a single "+X" under the main score and accumulates values while visible.
    /// Attach to a TextMeshProUGUI object that sits below the score text.
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class ScoreDeltaPopper : MonoBehaviour
    {
        public float holdSeconds = 0.6f;
        public float floatPixels = 12f;

        TMP_Text _txt;
        int _lastScore, _accum;
        float _hideAt;
        Vector3 _basePos;
        Coroutine _routine;

        void Awake()
        {
            _txt = GetComponent<TMP_Text>();
            _txt.text = "";
            _basePos = transform.localPosition;
        }

        void OnEnable() => GameEvents.OnScoreChanged += OnScoreChanged;
        void OnDisable() => GameEvents.OnScoreChanged -= OnScoreChanged;

        void OnScoreChanged(int newScore)
        {
            int delta = newScore - _lastScore;
            _lastScore = newScore;
            if (delta <= 0) return;

            _accum += delta;
            _hideAt = Time.unscaledTime + holdSeconds;
            _txt.text = $"+{_accum}";

            if (_routine == null) _routine = StartCoroutine(ShowRoutine());
        }

        IEnumerator ShowRoutine()
        {
            // Fade/float in
            float t = 0f, durIn = 0.08f;
            while (t < durIn)
            {
                t += Time.unscaledDeltaTime;
                float k = t / durIn;
                _txt.alpha = Mathf.Lerp(0f, 1f, k);
                transform.localPosition = _basePos + Vector3.down * Mathf.Lerp(floatPixels, 0f, k);
                yield return null;
            }
            _txt.alpha = 1f; transform.localPosition = _basePos;

            // Hold until last increment expires
            while (Time.unscaledTime < _hideAt) yield return null;

            // Fade out
            t = 0f; float durOut = 0.15f;
            while (t < durOut)
            {
                t += Time.unscaledDeltaTime;
                _txt.alpha = Mathf.Lerp(1f, 0f, t / durOut);
                yield return null;
            }
            _txt.alpha = 0f;
            _txt.text = "";
            _accum = 0;
            _routine = null;
        }
    }
}
