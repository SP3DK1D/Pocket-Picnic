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

        // kept for compatibility
        bool _optionsFromPause;

        void OnValidate()
        {
            if (!optionsPanel)
                Debug.LogWarning("[MenuFlowController] optionsPanel is not assigned. Will try to auto-find an OptionManager at runtime.", this);
        }

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
        }

        // ===== Pause flow =====
        public void OnPause()
        {
            PauseManager.Instance?.Pause();
            ShowOnly(pausePanel);                  // <— ensure ONLY pause is visible
        }

        public void OnResume()
        {
            PauseManager.Instance?.Resume();
            ShowOnly(hudPanel);                    // <— bring HUD back reliably
        }

        public void OnRestart()
        {
            PauseManager.Instance?.ResumeForce();
            ShowOnly(hudPanel);                    // <— show HUD immediately
            GameEvents.RaiseGameOver();
            GameEvents.RaiseGameStart();
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
            if (!ResolveOptionsPanelIfNeeded())
            {
                Debug.LogError("[MenuFlowController] Options panel not assigned and not found. Aborting.", this);
                return;
            }
            ShowOnly(optionsPanel);
        }

        public void OnOptionsFromPause()
        {
            _optionsFromPause = true;
            if (!ResolveOptionsPanelIfNeeded())
            {
                Debug.LogError("[MenuFlowController] Options panel not assigned and not found. Aborting.", this);
                return;
            }
            ShowOnly(optionsPanel);
        }

        public void OnOptionsClose()
        {
            // If paused, go back to Pause; else back to Main.
            if (PauseManager.Instance != null && PauseManager.Instance.IsPaused)
            {
                ShowOnly(pausePanel);
                PauseManager.Instance.Pause(); // ensure timeScale == 0
            }
            else
            {
                ShowOnly(mainMenuPanel);
            }
        }

        // ===== Helpers =====
        bool ResolveOptionsPanelIfNeeded()
        {
            if (optionsPanel) return true;
#if UNITY_2023_1_OR_NEWER
            var mgr = Object.FindFirstObjectByType<OptionManager>(FindObjectsInactive.Include);
#else
#pragma warning disable 618
            var mgr = Object.FindObjectOfType<OptionManager>(true);
#pragma warning restore 618
#endif
            if (mgr) { optionsPanel = mgr.gameObject; return true; }
            return false;
        }

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
