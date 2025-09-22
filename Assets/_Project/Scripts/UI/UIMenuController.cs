using UnityEngine;

namespace CatchTheFruit
{
    public class UIMenuController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject difficultyPanel;
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private GameObject pausePanel;

        [Header("Optional")]
        [SerializeField] private GameObject player;

        [Header("Debug")]
        [SerializeField] private bool autoStartDebug = false;   // <-- NEW

        void Awake()
        {
            ShowOnly(mainMenuPanel);
            SafeSetActive(player, false);

            // ---- TEMP: force a run so fruits spawn while we fix wiring ----
            if (autoStartDebug)
            {
                DifficultyManager.PickEasy();   // static
                BeginGame();
            }
        }

        public void OnStartPressed() => ShowOnly(difficultyPanel);
        public void OnPickEasy() { DifficultyManager.PickEasy(); BeginGame(); }
        public void OnPickMedium() { DifficultyManager.PickMedium(); BeginGame(); }
        public void OnPickHard() { DifficultyManager.PickHard(); BeginGame(); }

        void BeginGame()
        {
            ShowOnly(hudPanel);
            SafeSetActive(player, true);
            GameEvents.RaiseGameStart();        // <-- Spawner listens to this
        }

        public void OnPause() { PauseManager.Instance?.Pause(); SafeSetActive(pausePanel, true); }
        public void OnResume() { PauseManager.Instance?.Resume(); SafeSetActive(pausePanel, false); }
        public void OnRestart()
        {
            PauseManager.Instance?.ResumeForce();
            SafeSetActive(pausePanel, false);
            GameEvents.RaiseGameOver();
            GameEvents.RaiseGameStart();
            ShowOnly(hudPanel);
            SafeSetActive(player, true);
        }
        public void OnBackToMenu()
        {
            PauseManager.Instance?.ResumeForce();
            SafeSetActive(pausePanel, false);
            GameEvents.RaiseGameOver();
            DifficultyManager.ClearCurrent();
            ShowOnly(mainMenuPanel);
            SafeSetActive(player, false);
        }

        void ShowOnly(GameObject toShow)
        {
            if (mainMenuPanel) mainMenuPanel.SetActive(toShow == mainMenuPanel);
            if (difficultyPanel) difficultyPanel.SetActive(toShow == difficultyPanel);
            if (hudPanel) hudPanel.SetActive(toShow == hudPanel);
            if (pausePanel) pausePanel.SetActive(toShow == pausePanel);
        }
        void SafeSetActive(GameObject go, bool on)
        {
            if (go && go.activeSelf != on) go.SetActive(on);
        }
    }
}
