// Assets/_Project/Scripts/UI/MenuFlowController.cs
using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Menu flow: Main → Difficulty → Game (HUD).
    /// Also Pause, Options, Game Over, Restart, Back to Menu.
    ///
    /// IMPORTANT:
    /// - Assign ALL panels (including Game Over) in the inspector.
    /// - Hook your "Play Again" button to OnPlayAgainFromGameOver().
    /// </summary>
    public class MenuFlowController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject difficultyPanel;
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject optionsPanel;
        [SerializeField] private GameObject gameOverPanel;   // NEW: must be assigned

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

        void OnEnable()
        {
            GameEvents.OnGameStart += HandleGameStart;
            GameEvents.OnGameOver  += HandleGameOver;
        }

        void OnDisable()
        {
            GameEvents.OnGameStart -= HandleGameStart;
            GameEvents.OnGameOver  -= HandleGameOver;
        }

        // ===== Event handlers =====
        void HandleGameStart()
        {
            // Ensure gameplay time and panels
            PauseManager.Instance?.ResumeForce();
            ShowOnly(hudPanel);
            SafeSetActive(player, true);

            // Hearts fresh at start
            HeartLivesUI.ResetAllToFull();
        }

        void HandleGameOver()
        {
            // Always show Game Over panel (not Main Menu)
            PauseManager.Instance?.ResumeForce(); // normalize timeScale after Freeze
            SafeSetActive(player, false);

            if (gameOverPanel)
            {
                ShowOnly(gameOverPanel);
            }
            else
            {
                Debug.LogWarning("[MenuFlowController] GameOver panel not assigned. Falling back to Main Menu.");
                ShowOnly(mainMenuPanel);
            }

            // Let HUD update any final UI numbers if needed
            var hud = UIHud.Instance ? UIHud.Instance : FindController<UIHud>(true);
            hud?.ForceRefreshGameOverUI();
        }

        // ===== Main Menu =====
        public void OnStartPressed() => ShowOnly(difficultyPanel);

        public void OnPickEasy()   { DifficultyManager.PickEasy();   BeginGame(); }
        public void OnPickMedium() { DifficultyManager.PickMedium(); BeginGame(); }
        public void OnPickHard()   { DifficultyManager.PickHard();   BeginGame(); }

        void BeginGame()
        {
            ShowOnly(hudPanel);
            SafeSetActive(player, true);
            GameEvents.RaiseGameStart();   // spawner, timers, etc.
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

        // ===== Restart (in-run) =====
        public void OnRestart()
        {
            // Hard restart from pause/in-run: end then begin immediately.
            PauseManager.Instance?.ResumeForce();
            ShowOnly(hudPanel);
            GameEvents.RaiseGameOver();    // clear/stop systems listening to over
            GameEvents.RaiseGameStart();   // start fresh
            HeartLivesUI.ResetAllToFull();
            SafeSetActive(player, true);
        }

        // ===== Back to Menu =====
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
                PauseManager.Instance.Pause(); // re-assert paused state
            }
            else
            {
                ShowOnly(mainMenuPanel);
            }
        }

        // ===== Game Over actions =====
        /// <summary>
        /// Hook this to the "Play Again" button on the Game Over screen.
        /// Goes to difficulty selection and closes Game Over.
        /// </summary>
        public void OnPlayAgainFromGameOver()
        {
            PauseManager.Instance?.ResumeForce();
            ShowOnly(difficultyPanel);
            // Do NOT start the run yet; waits for the player to pick a difficulty.
        }

        // ===== Helpers =====
        void ShowOnly(GameObject toShow)
        {
            if (!toShow)
            {
                Debug.LogError("[MenuFlowController] ShowOnly() got null.", this);
                return;
            }

            if (mainMenuPanel)  mainMenuPanel.SetActive(toShow == mainMenuPanel);
            if (difficultyPanel) difficultyPanel.SetActive(toShow == difficultyPanel);
            if (hudPanel)       hudPanel.SetActive(toShow == hudPanel);
            if (pausePanel)     pausePanel.SetActive(toShow == pausePanel);
            if (optionsPanel)   optionsPanel.SetActive(toShow == optionsPanel);
            if (gameOverPanel)  gameOverPanel.SetActive(toShow == gameOverPanel);
        }

        static T FindController<T>(bool includeInactive = false) where T : MonoBehaviour
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<T>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
#else
#pragma warning disable 618
            return Object.FindObjectOfType<T>(includeInactive);
#pragma warning restore 618
#endif
        }

        void SafeSetActive(GameObject go, bool on)
        {
            if (go && go.activeSelf != on) go.SetActive(on);
        }
    }
}
