using UnityEngine;

namespace CatchTheFruit
{
    public class UIMenuController : MonoBehaviour
    {
        public enum UiState { MainMenu, Difficulty, Hud, Pause, GameOver, Options }

        [Header("Panels (assign all)")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject difficultyPanel;
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private GameObject optionsPanel;   // Options panel (must be assigned or discoverable)

        [Header("Player root")]
        [SerializeField] private GameObject player;

        [Header("Spawners")]
        [SerializeField] private FruitSpawner gameplaySpawner;
        [SerializeField] private MenuFruitRain menuRain;

        private UiState _state = UiState.MainMenu;

        float _playerStartY;
        float _playerStartZ;

        // kept for compatibility; not relied on for close anymore
        bool _optionsFromPause;
        bool _resetHolding;

        void OnValidate()
        {
            if (!optionsPanel)
                Debug.LogWarning("[UIMenuController] optionsPanel is not assigned. Will try to auto-find an OptionManager at runtime.", this);
        }

        void Awake()
        {
            RunState.SetGameplay(false);

            CachePlayerStart();

            ApplyState(UiState.MainMenu, ensureTimeScale: true);
            SafeSetActive(player, false);

            if (menuRain) menuRain.gameObject.SetActive(true);
            if (gameplaySpawner) gameplaySpawner.StopAndClear();
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

        void HandleGameStart()
        {
            RunState.SetGameplay(true);
            PauseManager.Instance?.ResumeForce();
            if (menuRain) menuRain.gameObject.SetActive(false);

            SafeSetActive(player, true);
            CenterPlayerHorizontally();

            ApplyState(UiState.Hud);
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

        // ===== Main Menu buttons =====
        public void OnStartPressed() => ApplyState(UiState.Difficulty);

        public void OnPickEasy() { DifficultyManager.PickEasy(); GameEvents.RaiseGameStart(); }
        public void OnPickMedium() { DifficultyManager.PickMedium(); GameEvents.RaiseGameStart(); }
        public void OnPickHard() { DifficultyManager.PickHard(); GameEvents.RaiseGameStart(); }

        public void OnOptionsFromMain()
        {
            _optionsFromPause = false;
            PauseManager.Instance?.ResumeForce();
            ApplyState(UiState.Options);
        }

        // ===== Pause flow =====
        public void OnPause() { PauseManager.Instance?.Pause(); ApplyState(UiState.Pause); }
        public void OnResume() { PauseManager.Instance?.Resume(); ApplyState(UiState.Hud); }

        public void OnOptionsFromPause()
        {
            _optionsFromPause = true;
            // remain paused while viewing options
            ApplyState(UiState.Options);
        }

        public void OnOptionsClose()
        {
            // NEW: source-agnostic return — if the game is paused, go back to Pause; else Main Menu.
            if (PauseManager.Instance != null && PauseManager.Instance.IsPaused)
            {
                ApplyState(UiState.Pause);
                PauseManager.Instance.Pause(); // ensure paused (no side effects if already paused)
            }
            else
            {
                ApplyState(UiState.MainMenu, ensureTimeScale: true);
            }
        }

        public void OnRestartHoldStart()
        {
            _resetHolding = true;
            RunState.SetGameplay(false);
            ZeroPlayerVelocity();
        }

        public void OnRestart()
        {
            _resetHolding = false;

            PauseManager.Instance?.ResumeForce();
            GameEvents.RaiseGameOver();

            if (gameplaySpawner) gameplaySpawner.StopAndClear();
            if (menuRain) menuRain.gameObject.SetActive(false);

            GameEvents.RaiseGameStart();

            SafeSetActive(player, true);
            ApplyState(UiState.Hud);
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

        // ===== Panel state handling =====
        void ApplyState(UiState target, bool ensureTimeScale = false)
        {
            if (target == UiState.Options && !ResolveOptionsPanelIfNeeded())
            {
                Debug.LogError("[UIMenuController] Options panel is not assigned and could not be auto-found. Aborting state change to Options.", this);
                return;
            }

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

        bool ResolveOptionsPanelIfNeeded()
        {
            if (optionsPanel) return true;

            var mgr = FindController<OptionManager>(true);
            if (mgr)
            {
                optionsPanel = mgr.gameObject;
                return true;
            }
            return false;
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

        void CachePlayerStart()
        {
            if (!player) return;
            var p = player.transform.position;
            _playerStartY = p.y;
            _playerStartZ = p.z;
        }

        void CenterPlayerHorizontally()
        {
            if (!player) return;
            float centerX = 0f;
            var cam = Camera.main;
            if (cam) centerX = cam.transform.position.x;

            var pos = player.transform.position;
            pos.x = centerX;
            pos.y = _playerStartY;
            pos.z = _playerStartZ;
            player.transform.position = pos;

            ZeroPlayerVelocity();
        }

        void ZeroPlayerVelocity()
        {
            if (!player) return;

            var rb2d = player.GetComponent<Rigidbody2D>();
            if (rb2d) { rb2d.linearVelocity = Vector2.zero; rb2d.angularVelocity = 0f; }

            var rb3d = player.GetComponent<Rigidbody>();
            if (rb3d) { rb3d.linearVelocity = Vector3.zero; rb3d.angularVelocity = Vector3.zero; }
        }
    }
}
