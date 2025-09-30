using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Central menu flow controller:
    /// Main → Difficulty → HUD/Game.
    /// Also handles Pause, GameOver, Restart, Back to Menu, and Options.
    /// </summary>
    public class MenuFlowController : MonoBehaviour
    {
        public enum UiState { MainMenu, Difficulty, Hud, Pause, GameOver, Options }

        [Header("Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject difficultyPanel;
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private GameObject optionsPanel;

        [Header("Optional")]
        [SerializeField] private GameObject player;
        [SerializeField] private FruitSpawner gameplaySpawner;
        [SerializeField] private MenuFruitRain menuRain;
        [SerializeField] private bool autoShowMainOnAwake = true;

        private UiState _state = UiState.MainMenu;
        private bool _optionsFromPause;

        void Awake()
        {
            RunState.SetGameplay(false);

            if (autoShowMainOnAwake)
            {
                ApplyState(UiState.MainMenu, ensureTimeScale: true);
                SafeSetActive(player, false);
                if (menuRain) menuRain.gameObject.SetActive(true);
                if (gameplaySpawner) gameplaySpawner.StopAndClear();
            }
        }

        void OnEnable()
        {
            GameEvents.OnGameStart += HandleGameStart;
            GameEvents.OnGameOver += HandleGameOver;
        }
        void OnDisable()
        {
            GameEvents.OnGameStart -= HandleGameStart;
            GameEvents.OnGameOver -= HandleGameOver;
        }

        // ===== Game lifecycle =====

        void HandleGameStart()
        {
            RunState.SetGameplay(true);
            PauseManager.Instance?.ResumeForce();
            if (menuRain) menuRain.gameObject.SetActive(false);

            SafeSetActive(player, true);
            ApplyState(UiState.Hud);

            HeartLivesUI.ResetAllToFull();
        }

        void HandleGameOver()
        {
            RunState.SetGameplay(false);
            PauseManager.Instance?.ResumeForce();
            SafeSetActive(player, false);
            ApplyState(gameOverPanel ? UiState.GameOver : UiState.MainMenu);

            var hud = UIHud.Instance ? UIHud.Instance : FindController<UIHud>(true);
            hud?.ForceRefreshGameOverUI();
        }

        // ===== Main Menu / Difficulty =====

        public void OnStartPressed() => ApplyState(UiState.Difficulty);

        public void OnPickEasy()   { DifficultyManager.PickEasy();   GameEvents.RaiseGameStart(); }
        public void OnPickMedium() { DifficultyManager.PickMedium(); GameEvents.RaiseGameStart(); }
        public void OnPickHard()   { DifficultyManager.PickHard();   GameEvents.RaiseGameStart(); }

        // ===== GameOver flow =====

        public void OnPlayAgainFromGameOver()
        {
            RunState.SetGameplay(false);
            PauseManager.Instance?.ResumeForce();
            SafeSetActive(player, false);
            DifficultyManager.ClearCurrent();

            if (gameplaySpawner) gameplaySpawner.StopAndClear();

            ApplyState(UiState.Difficulty, ensureTimeScale: true);
            AudioHub.I?.PlayButton();
        }

        public void OnBackToMenu()
        {
            RunState.SetGameplay(false);
            PauseManager.Instance?.ResumeForce();
            SafeSetActive(player, false);
            DifficultyManager.ClearCurrent();

            if (gameplaySpawner) gameplaySpawner.StopAndClear();
            if (menuRain) menuRain.gameObject.SetActive(true);

            ApplyState(UiState.MainMenu, ensureTimeScale: true);
        }

        // ===== Pause flow =====

        public void OnPause()
        {
            PauseManager.Instance?.Pause();
            ApplyState(UiState.Pause);
        }

        public void OnResume()
        {
            PauseManager.Instance?.Resume();
            ApplyState(UiState.Hud);
        }

        public void OnRestart()
        {
            PauseManager.Instance?.ResumeForce();
            GameEvents.RaiseGameOver();
            GameEvents.RaiseGameStart();

            HeartLivesUI.ResetAllToFull();

            SafeSetActive(player, true);
            ApplyState(UiState.Hud);
        }

        // ===== Options =====

        public void OnOptionsFromMain()
        {
            _optionsFromPause = false;
            PauseManager.Instance?.ResumeForce();
            ApplyState(UiState.Options);
        }

        public void OnOptionsFromPause()
        {
            _optionsFromPause = true;
            ApplyState(UiState.Options);
        }

        public void OnOptionsClose()
        {
            if (PauseManager.Instance != null && PauseManager.Instance.IsPaused)
            {
                ApplyState(UiState.Pause);
                PauseManager.Instance.Pause();
            }
            else
            {
                ApplyState(UiState.MainMenu, ensureTimeScale: true);
            }
        }

        // ===== Helpers =====

        void ApplyState(UiState target, bool ensureTimeScale = false)
        {
            SafeSetActive(mainMenuPanel, false);
            SafeSetActive(difficultyPanel, false);
            SafeSetActive(hudPanel, false);
            SafeSetActive(pausePanel, false);
            SafeSetActive(gameOverPanel, false);
            SafeSetActive(optionsPanel, false);

            if (ensureTimeScale) PauseManager.Instance?.ResumeForce();

            switch (target)
            {
                case UiState.MainMenu:   SafeSetActive(mainMenuPanel, true);   break;
                case UiState.Difficulty: SafeSetActive(difficultyPanel, true); break;
                case UiState.Hud:        SafeSetActive(hudPanel, true);        break;
                case UiState.Pause:      SafeSetActive(pausePanel, true);      break;
                case UiState.GameOver:   SafeSetActive(gameOverPanel, true);   break;
                case UiState.Options:    SafeSetActive(optionsPanel, true);    break;
            }

            _state = target;
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
