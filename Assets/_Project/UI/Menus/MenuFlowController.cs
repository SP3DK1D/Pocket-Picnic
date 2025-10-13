using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Central UI state machine for menu → difficulty → HUD → pause → game over → options.
    /// - Owns only panel visibility and a few side effects (pause state, rain/spawner/player).
    /// - Uses RunState for "in gameplay" flag and GameEvents for start/over.
    /// - Avoids duplicate logic via ApplyState() and small helpers.
    /// - Keeps the SAME public methods you already wired to buttons.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuFlowController : MonoBehaviour
    {
        public static MenuFlowController Instance { get; private set; }

        public enum UiState { MainMenu, Difficulty, Hud, Pause, GameOver, Options }

        [Header("Panels (assign)")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject difficultyPanel;
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private GameObject optionsPanel;

        [Header("Optional Scene Refs")]
        [Tooltip("Player root to show/hide during gameplay transitions.")]
        [SerializeField] private GameObject player;
        [Tooltip("Spawner used only during gameplay; is stopped when leaving gameplay states.")]
        [SerializeField] private FruitSpawner gameplaySpawner;
        [Tooltip("Decorative rain for main menu; enabled only on MainMenu.")]
        [SerializeField] private MenuFruitRain menuRain;

        [Header("Difficulty Assets")]
        [SerializeField] private DifficultyDef easyDef;
        [SerializeField] private DifficultyDef mediumDef;
        [SerializeField] private DifficultyDef hardDef;

        [Header("Boot")]
        [Tooltip("If true, we show MainMenu on Awake and ensure timeScale=1.")]
        [SerializeField] private bool autoShowMainOnAwake = true;

        [Header("Debug")]
        [SerializeField] private bool verboseLogs = false;

        UiState _state = UiState.MainMenu;

        // ---------- lifecycle ----------
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
            if (Instance == this) Instance = null;
        }

        // ---------- Game lifecycle (raised by buttons or systems) ----------
        void HandleGameStart()
        {
            RunState.SetGameplay(true);
            PauseManager.Instance?.ResumeForce();

            if (menuRain) menuRain.gameObject.SetActive(false);
            SafeSetActive(player, true);

            ApplyState(UiState.Hud);
            HeartLivesUI.ResetAllToFull();     // visual hearts to full at run start
        }

        void HandleGameOver()
        {
            RunState.SetGameplay(false);
            PauseManager.Instance?.ResumeForce();

            SafeSetActive(player, false);
            if (gameOverPanel)
            {
                ApplyState(UiState.GameOver);
            }
            else
            {
                // Fallback if panel not assigned
                ApplyState(UiState.MainMenu);
            }

            var hud = UIHud.Instance ? UIHud.Instance : FindController<UIHud>(true);
            hud?.ForceRefreshGameOverUI();
        }

        // ---------- Main Menu / Difficulty ----------
        public void OnStartPressed() => ApplyState(UiState.Difficulty);

        public void OnPickEasy() { ApplyDifficultyAsset(easyDef, "Easy"); GameEvents.RaiseGameStart(); }
        public void OnPickMedium() { ApplyDifficultyAsset(mediumDef, "Medium"); GameEvents.RaiseGameStart(); }
        public void OnPickHard() { ApplyDifficultyAsset(hardDef, "Hard"); GameEvents.RaiseGameStart(); }

        void ApplyDifficultyAsset(DifficultyDef def, string label)
        {
            if (!def)
            {
                if (verboseLogs) Debug.LogWarning($"[MenuFlow] {label} DifficultyDef not assigned; using defaults.");
                DifficultyManager.ApplyFromDef(null);
                return;
            }
            DifficultyManager.ApplyFromDef(def);
        }

        // ---------- GameOver flow ----------
        public void OnPlayAgainFromGameOver()
        {
            // Clear gameplay bits and return to Difficulty promptly
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

        // ---------- Pause flow ----------
        public void OnPause()
        {
            PauseManager.Instance?.Pause();
            ApplyState(UiState.Pause);

            // Keep toggles in sync with current audio state
            OptionManager.SyncUIFromAudio();
        }

        public void OnResume()
        {
            PauseManager.Instance?.Resume();
            ApplyState(UiState.Hud);
        }

        public void OnRestart()
        {
            // Hard-reset timeScale to avoid sticky pause/freeze
            PauseManager.Instance?.ResumeForce();

            // Fire standard end/start so all systems reset in their event hooks
            GameEvents.RaiseGameOver();
            GameEvents.RaiseGameStart();

            HeartLivesUI.ResetAllToFull();
            SafeSetActive(player, true);
            ApplyState(UiState.Hud);
        }

        // ---------- Options flow ----------
        public void OnOptionsFromMain()
        {
            PauseManager.Instance?.ResumeForce();
            ApplyState(UiState.Options);
            OptionManager.SyncUIFromAudio();
        }

        public void OnOptionsFromPause()
        {
            ApplyState(UiState.Options);
            OptionManager.SyncUIFromAudio();
        }

        public void OnOptionsClose()
        {
            // If we came from Pause, return to Pause and re-apply paused timeScale
            if (PauseManager.Instance != null && PauseManager.Instance.IsPaused)
            {
                ApplyState(UiState.Pause);
                OptionManager.SyncUIFromAudio();
                PauseManager.Instance.Pause(); // keep paused
            }
            else
            {
                ApplyState(UiState.MainMenu, ensureTimeScale: true);
            }
        }

        // ---------- Core state switcher ----------
        void ApplyState(UiState target, bool ensureTimeScale = false)
        {
            // 1) normalize timeScale if asked (useful when entering non-gameplay states)
            if (ensureTimeScale) PauseManager.Instance?.ResumeForce();

            // 2) panel visibility (single place so we don't forget to hide one)
            SafeSetActive(mainMenuPanel, target == UiState.MainMenu);
            SafeSetActive(difficultyPanel, target == UiState.Difficulty);
            SafeSetActive(hudPanel, target == UiState.Hud);
            SafeSetActive(pausePanel, target == UiState.Pause);
            SafeSetActive(gameOverPanel, target == UiState.GameOver);
            SafeSetActive(optionsPanel, target == UiState.Options);

            // 3) scene side-effects (keep tiny and symmetrical)
            switch (target)
            {
                case UiState.MainMenu:
                    // show decorative rain, ensure gameplay systems are idle
                    if (menuRain) menuRain.gameObject.SetActive(true);
                    if (gameplaySpawner) gameplaySpawner.StopAndClear();
                    SafeSetActive(player, false);
                    RunState.SetGameplay(false);
                    break;

                case UiState.Difficulty:
                    if (menuRain) menuRain.gameObject.SetActive(false);
                    if (gameplaySpawner) gameplaySpawner.StopAndClear();
                    SafeSetActive(player, false);
                    RunState.SetGameplay(false);
                    break;

                case UiState.Hud:
                    if (menuRain) menuRain.gameObject.SetActive(false);
                    SafeSetActive(player, true);
                    // Gameplay started via GameEvents; spawner is controlled elsewhere
                    RunState.SetGameplay(true);
                    break;

                case UiState.Pause:
                    // timeScale already paused in OnPause(); just standard visuals here
                    break;

                case UiState.GameOver:
                    // We don’t auto-show menu rain; remain clean until the user chooses
                    break;

                case UiState.Options:
                    // Pure UI state; nothing special besides toggles sync
                    break;
            }

            _state = target;
            if (verboseLogs) Debug.Log($"[MenuFlow] -> {_state}");
        }

        // ---------- small helpers ----------
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

        static void SafeSetActive(GameObject go, bool on)
        {
            if (go && go.activeSelf != on) go.SetActive(on);
        }
    }
}
