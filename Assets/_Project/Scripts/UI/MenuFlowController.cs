using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Menu flow: Main → Difficulty → Game (HUD). Also Pause, Restart, Back to Menu.
    /// Works with static DifficultyManager (no instance variables!).
    /// </summary>
    public class MenuFlowController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject difficultyPanel;
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private GameObject pausePanel;

        [Header("Optional")]
        [SerializeField] private GameObject player; // enable/disable on start/end
        [SerializeField] private bool autoShowMainOnAwake = true;

        void Awake()
        {
            if (autoShowMainOnAwake)
            {
                ShowOnly(mainMenuPanel);
                SafeSetActive(player, false);
            }
        }

        // ============ Main Menu ============
        public void OnStartPressed()
        {
            ShowOnly(difficultyPanel);
        }

        public void OnPickEasy()
        {
            DifficultyManager.PickEasy();   // STATIC call
            BeginGame();
        }

        public void OnPickMedium()
        {
            DifficultyManager.PickMedium(); // STATIC call
            BeginGame();
        }

        public void OnPickHard()
        {
            DifficultyManager.PickHard();   // STATIC call
            BeginGame();
        }

        private void BeginGame()
        {
            // Panels
            ShowOnly(hudPanel);
            // Player active
            SafeSetActive(player, true);

            // Start a new run
            GameEvents.RaiseGameStart();
        }

        // ============ Pause ============
        public void OnPause()
        {
            PauseManager.Instance?.Pause();
            SafeSetActive(pausePanel, true);
        }

        public void OnResume()
        {
            PauseManager.Instance?.Resume();
            SafeSetActive(pausePanel, false);
        }

        public void OnRestart()
        {
            // Ensure time scale restored and panels correct
            PauseManager.Instance?.ResumeForce();
            SafeSetActive(pausePanel, false);

            // Soft-reset flow
            GameEvents.RaiseGameOver();
            GameEvents.RaiseGameStart();

            ShowOnly(hudPanel);
            SafeSetActive(player, true);
        }

        // Exit from Pause back to Main Menu (NOT app quit)
        public void OnBackToMenu()
        {
            PauseManager.Instance?.ResumeForce();
            SafeSetActive(pausePanel, false);

            // End the current run and clear difficulty
            GameEvents.RaiseGameOver();
            DifficultyManager.ClearCurrent();

            // Go back to main menu and hide player
            ShowOnly(mainMenuPanel);
            SafeSetActive(player, false);
        }

        // ============ Helpers ============
        private void ShowOnly(GameObject toShow)
        {
            if (mainMenuPanel) mainMenuPanel.SetActive(toShow == mainMenuPanel);
            if (difficultyPanel) difficultyPanel.SetActive(toShow == difficultyPanel);
            if (hudPanel) hudPanel.SetActive(toShow == hudPanel);
            if (pausePanel) pausePanel.SetActive(toShow == pausePanel);
        }

        private void SafeSetActive(GameObject go, bool on)
        {
            if (go && go.activeSelf != on) go.SetActive(on);
        }
    }
}
