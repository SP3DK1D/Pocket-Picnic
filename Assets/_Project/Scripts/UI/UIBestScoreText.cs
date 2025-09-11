using UnityEngine;
using TMPro;
using System.Collections;

namespace CatchTheFruit
{
    /// <summary>
    /// Reads PlayerPrefs and writes "Best: {value}" to this TMP.
    /// Refreshes when the GameOver event fires and on enable (next frame)
    /// so it runs AFTER ScoreManager saves.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class UIBestScoreText : MonoBehaviour
    {
        [Header("Formatting")]
        public string prefsKey = "best";
        public string format = "Best: {0}";
        public bool refreshOnEnable = true;
        public bool debugLogs = false;

        TMP_Text _tmp;

        void Awake() { _tmp = GetComponent<TMP_Text>(); }

        void OnEnable()
        {
            GameEvents.OnGameOver += OnGameOver;
            if (refreshOnEnable) StartCoroutine(RefreshNextFrame());
        }

        void OnDisable()
        {
            GameEvents.OnGameOver -= OnGameOver;
        }

        void OnGameOver() => StartCoroutine(RefreshNextFrame());

        IEnumerator RefreshNextFrame()
        {
            // Wait one frame to ensure ScoreManager stored the new best.
            yield return null;
            Refresh();
        }

        public void Refresh()
        {
            int best = PlayerPrefs.GetInt(prefsKey, 0);
            if (_tmp) _tmp.text = string.Format(format, best);
            if (debugLogs) Debug.Log($"[UIBestScoreText] Best => {best}");
        }
    }
}
