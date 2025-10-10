using UnityEngine;

namespace CatchTheFruit
{
    public class MenuFlowController : MonoBehaviour
    {
        public static MenuFlowController Instance { get; private set; }

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

        [Header("Difficulty Assets (Inspector)")]
        [SerializeField] private DifficultyDef easyDef;
        [SerializeField] private DifficultyDef mediumDef;
        [SerializeField] private DifficultyDef hardDef;

        private UiState _state = UiState.MainMenu;

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

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

            if (gameOverPanel)
                ApplyState(UiState.GameOver);
            else
            {
                Debug.LogWarning("[MenuFlow] GameOver panel not assigned; falling back to MainMenu.");
                ApplyState(UiState.MainMenu);
            }

            var hud = UIHud.Instance ? UIHud.Instance : FindController<UIHud>(true);
            hud?.ForceRefreshGameOverUI();
        }

        // ===== Main Menu / Difficulty =====

        public void OnStartPressed() => ApplyState(UiState.Difficulty);

        public void OnPickEasy() { ApplyDifficultyAsset(easyDef, "Easy"); GameEvents.RaiseGameStart(); }
        public void OnPickMedium() { ApplyDifficultyAsset(mediumDef, "Medium"); GameEvents.RaiseGameStart(); }
        public void OnPickHard() { ApplyDifficultyAsset(hardDef, "Hard"); GameEvents.RaiseGameStart(); }

        void ApplyDifficultyAsset(DifficultyDef def, string label)
        {
            if (def == null)
            {
                Debug.LogWarning($"[MenuFlow] {label} DifficultyDef not assigned; applying safe defaults.");
                DifficultyManager.ApplyFromDef(null);
            }
            else
            {
                DifficultyManager.ApplyFromDef(def);
            }
        }

        // ===== GameOver flow =====

        public void OnPlayAgainFromGameOver()
        {
            RunState.SetGameplay(false);
            PauseManager.Instance?.ResumeForce();
            SafeSetActive(player, false);
            DifficultyManager.ClearCurrent();

            if (gameplaySpawner) gameplaySpawner.StopAndClear();

            ApplyState(UiState.Difficulty, ensureTimeScale: true);
            AudioManager.I?.PlayUIButton();
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
            OptionManager.SyncUIFromAudio(); // ensure pause toggles match current state
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
            PauseManager.Instance?.ResumeForce();
            ApplyState(UiState.Options);
            OptionManager.SyncUIFromAudio(); // ensure options toggles match current state
        }

        public void OnOptionsFromPause()
        {
            ApplyState(UiState.Options);
            OptionManager.SyncUIFromAudio(); // ensure options toggles match current state
        }

        public void OnOptionsClose()
        {
            if (PauseManager.Instance != null && PauseManager.Instance.IsPaused)
            {
                ApplyState(UiState.Pause);
                OptionManager.SyncUIFromAudio();
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
                case UiState.MainMenu: SafeSetActive(mainMenuPanel, true); break;
                case UiState.Difficulty: SafeSetActive(difficultyPanel, true); break;
                case UiState.Hud: SafeSetActive(hudPanel, true); break;
                case UiState.Pause: SafeSetActive(pausePanel, true); break;
                case UiState.GameOver: SafeSetActive(gameOverPanel, true); break;
                case UiState.Options: SafeSetActive(optionsPanel, true); break;
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
