using UnityEngine;

namespace CatchTheFruit
{
    public class UIMenuController : MonoBehaviour
    {
        public enum UiState { MainMenu, Difficulty, Hud, Pause, GameOver }

        [Header("Panels (assign all)")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject difficultyPanel;
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject gameOverPanel;

        [Header("Player root")]
        [SerializeField] private GameObject player;

        [Header("Spawners")]
        [SerializeField] private FruitSpawner gameplaySpawner;
        [SerializeField] private MenuFruitRain menuRain;

        private UiState _state = UiState.MainMenu;

        // Remember the player's original Y/Z so we only re-center X
        float _playerStartY;
        float _playerStartZ;

        // NEW: track when Reset button is being held, so we can gate input
        bool _resetHolding;

        void Awake()
        {
            RunState.SetGameplay(false);
            CachePlayerStart();

            ApplyState(UiState.MainMenu, ensureTimeScale: true);
            SafeSetActive(player, false);

            if (menuRain) menuRain.gameObject.SetActive(true);
            if (gameplaySpawner) gameplaySpawner.StopAndClear();   // ensure no gameplay fruits remain
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

            CenterPlayerHorizontally(); // always start in the center

            ApplyState(UiState.Hud);
        }

        void HandleGameOver()
        {
            RunState.SetGameplay(false);
            PauseManager.Instance?.ResumeForce();
            SafeSetActive(player, false);
            ApplyState(gameOverPanel ? UiState.GameOver : UiState.MainMenu);

            // If you also want to pre-center for next run, uncomment:
            // CenterPlayerHorizontally();

            // If you added the UIHud ForceRefresh earlier, keep this:
            var hud = UIHud.Instance ? UIHud.Instance : FindObjectOfType<UIHud>(true);
            hud?.ForceRefreshGameOverUI();
        }

        public void OnStartPressed() => ApplyState(UiState.Difficulty);

        public void OnPickEasy() { DifficultyManager.PickEasy(); GameEvents.RaiseGameStart(); }
        public void OnPickMedium() { DifficultyManager.PickMedium(); GameEvents.RaiseGameStart(); }
        public void OnPickHard() { DifficultyManager.PickHard(); GameEvents.RaiseGameStart(); }

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

        // ========= Reset/Restart =========

        /// <summary>
        /// Hook this to the Reset button's OnPointerDown.
        /// Gates gameplay input while the button is held so the player can't drift left.
        /// </summary>
        public void OnRestartHoldStart()
        {
            _resetHolding = true;
            RunState.SetGameplay(false);  // stop gameplay input while holding

            // Stop any lingering velocity so physics can't push the player left
            ZeroPlayerVelocity();
        }

        /// <summary>
        /// Hook this to the Reset button's OnClick (Pointer Up).
        /// Performs the existing restart flow.
        /// </summary>
        public void OnRestart()
        {
            _resetHolding = false;

            PauseManager.Instance?.ResumeForce();

            // End the current run cleanly
            GameEvents.RaiseGameOver();

            // Clear gameplay fruits & hide menu rain during restart
            if (gameplaySpawner) gameplaySpawner.StopAndClear();
            if (menuRain) menuRain.gameObject.SetActive(false);

            // Start a new run (HandleGameStart will center the player)
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

        // ========= State helpers =========

        void ApplyState(UiState target, bool ensureTimeScale = false)
        {
            SafeSetActive(mainMenuPanel, false);
            SafeSetActive(difficultyPanel, false);
            SafeSetActive(hudPanel, false);
            SafeSetActive(pausePanel, false);
            SafeSetActive(gameOverPanel, false);

            if (ensureTimeScale) PauseManager.Instance?.ResumeForce();

            switch (target)
            {
                case UiState.MainMenu: SafeSetActive(mainMenuPanel, true); break;
                case UiState.Difficulty: SafeSetActive(difficultyPanel, true); break;
                case UiState.Hud: SafeSetActive(hudPanel, true); break;
                case UiState.Pause: SafeSetActive(pausePanel, true); break;
                case UiState.GameOver: SafeSetActive(gameOverPanel, true); break;
            }

            _state = target;
        }

        void SafeSetActive(GameObject go, bool on)
        {
            if (go && go.activeSelf != on) go.SetActive(on);
        }

        // ========= NEW helpers =========

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
            pos.y = _playerStartY;   // keep original height
            pos.z = _playerStartZ;
            player.transform.position = pos;

            ZeroPlayerVelocity();
        }

        void ZeroPlayerVelocity()
        {
            if (!player) return;

            var rb2d = player.GetComponent<Rigidbody2D>();
            if (rb2d)
            {
                rb2d.linearVelocity = Vector2.zero;
                rb2d.angularVelocity = 0f;
            }

            var rb3d = player.GetComponent<Rigidbody>();
            if (rb3d)
            {
                rb3d.linearVelocity = Vector3.zero;
                rb3d.angularVelocity = Vector3.zero;
            }
        }
    }
}
