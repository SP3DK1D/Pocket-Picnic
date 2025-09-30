using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Menu flow: Main → Difficulty → Game (HUD). Also Pause, Restart, Back to Menu, Options.
    /// </summary>
    public class MenuFlowController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject difficultyPanel;
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject optionsPanel;  // Options panel

        [Header("Optional")]
        [SerializeField] private GameObject player;
        [SerializeField] private bool autoShowMainOnAwake = true;

        bool _optionsFromPause;

        void Awake()
        {
            if (autoShowMainOnAwake)
            {
                ShowOnly(mainMenuPanel);
                SafeSetActive(player, false);
            }
        }

        // ===== Main Menu =====
        public void OnStartPressed() => ShowOnly(difficultyPanel);

        public void OnPickEasy() { DifficultyManager.PickEasy(); BeginGame(); }
        public void OnPickMedium() { DifficultyManager.PickMedium(); BeginGame(); }
        public void OnPickHard() { DifficultyManager.PickHard(); BeginGame(); }

        void BeginGame()
        {
            ShowOnly(hudPanel);
            SafeSetActive(player, true);
            GameEvents.RaiseGameStart();

            // hearts fresh at start
            HeartLivesUI.ResetAllToFull();
        }

        // ===== Pause flow =====
        public void OnPause()
        {
            PauseManager.Instance?.Pause();
            ShowOnly(pausePanel);
        }

        public void OnResume()
        {
            PauseManager.Instance?.Resume();
            ShowOnly(hudPanel);
        }

        public void OnRestart()
        {
            PauseManager.Instance?.ResumeForce();
            ShowOnly(hudPanel);
            GameEvents.RaiseGameOver();
            GameEvents.RaiseGameStart();

            // hard visual reset for hearts on explicit reset
            HeartLivesUI.ResetAllToFull();

            SafeSetActive(player, true);
        }

        public void OnBackToMenu()
        {
            PauseManager.Instance?.ResumeForce();
            ShowOnly(mainMenuPanel);
            GameEvents.RaiseGameOver();
            DifficultyManager.ClearCurrent();
            SafeSetActive(player, false);
        }

        // ===== Options =====
        public void OnOptionsFromMain()
        {
            _optionsFromPause = false;
            PauseManager.Instance?.ResumeForce();
            if (!optionsPanel) { Debug.LogError("[MenuFlowController] Options panel not assigned."); return; }
            ShowOnly(optionsPanel);
        }

        public void OnOptionsFromPause()
        {
            _optionsFromPause = true;
            if (!optionsPanel) { Debug.LogError("[MenuFlowController] Options panel not assigned."); return; }
            ShowOnly(optionsPanel);
        }

        public void OnOptionsClose()
        {
            if (PauseManager.Instance != null && PauseManager.Instance.IsPaused)
            {
                ShowOnly(pausePanel);
                PauseManager.Instance.Pause();
            }
            else
            {
                ShowOnly(mainMenuPanel);
            }
        }

        // ===== Helpers =====
        void ShowOnly(GameObject toShow)
        {
            if (!toShow) { Debug.LogError("[MenuFlowController] ShowOnly() got null.", this); return; }

            if (mainMenuPanel) mainMenuPanel.SetActive(toShow == mainMenuPanel);
            if (difficultyPanel) difficultyPanel.SetActive(toShow == difficultyPanel);
            if (hudPanel) hudPanel.SetActive(toShow == hudPanel);
            if (pausePanel) pausePanel.SetActive(toShow == pausePanel);
            if (optionsPanel) optionsPanel.SetActive(toShow == optionsPanel);
        }

        void SafeSetActive(GameObject go, bool on)
        {
            if (go && go.activeSelf != on) go.SetActive(on);
        }
    }
}
