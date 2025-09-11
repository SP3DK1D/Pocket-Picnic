using UnityEngine;
using TMPro;

namespace CatchTheFruit
{
    /// <summary>
    /// HUD that auto-binds TMP children and updates on events.
    /// If fields are unassigned, it searches by name: TXT_Score, TXT_Lives, TXT_Timer.
    /// </summary>
    public class UIHud : MonoBehaviour
    {
        [Header("TMP refs (leave blank to auto-bind by name)")]
        public TMP_Text txtScore;   // expect child named "TXT_Score"
        public TMP_Text txtLives;   // expect child named "TXT_Lives"
        public TMP_Text txtTimer;   // expect child named "TXT_Timer"

        [Header("Debug")]
        public bool debugLogs = false;

        void Awake()
        {
            // Auto-bind if any missing
            if (!txtScore) txtScore = FindChildTMP("TXT_Score");
            if (!txtLives) txtLives = FindChildTMP("TXT_Lives");
            if (!txtTimer) txtTimer = FindChildTMP("TXT_Timer");
        }

        TMP_Text FindChildTMP(string childName)
        {
            var t = transform.Find(childName);
            if (!t)
            {
                if (debugLogs) Debug.LogWarning($"[UIHud] Child '{childName}' not found under {name}.");
                return null;
            }
            var tmp = t.GetComponent<TMP_Text>();
            if (!tmp && debugLogs) Debug.LogWarning($"[UIHud] '{childName}' has no TMP_Text.");
            return tmp;
        }

        void OnEnable()
        {
            GameEvents.OnScoreChanged += SetScore;
            GameEvents.OnLivesChanged += SetLives;
            GameEvents.OnTimerTick += SetTimerUp;

            if (debugLogs) Debug.Log($"[UIHud] Subscribed. Active={gameObject.activeInHierarchy}");
        }

        void OnDisable()
        {
            GameEvents.OnScoreChanged -= SetScore;
            GameEvents.OnLivesChanged -= SetLives;
            GameEvents.OnTimerTick -= SetTimerUp;

            if (debugLogs) Debug.Log("[UIHud] Unsubscribed.");
        }

        void SetScore(int v)
        {
            if (!txtScore) return;
            txtScore.text = $"Score: {v}";
            if (debugLogs) Debug.Log($"[UIHud] Score -> {v}");
        }

        void SetLives(int v)
        {
            if (!txtLives) return;
            txtLives.text = $"Lives: {v}";
            if (debugLogs) Debug.Log($"[UIHud] Lives -> {v}");
        }

        // Elapsed seconds → "Time: mm:ss"
        void SetTimerUp(float elapsed)
        {
            if (!txtTimer) return;
            int total = Mathf.FloorToInt(elapsed);
            int m = total / 60;
            int s = total % 60;
            txtTimer.text = $"Time: {m:00}:{s:00}";
            if (debugLogs) Debug.Log($"[UIHud] Time -> {m:00}:{s:00}");
        }
    }
}
