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

        void Awake()
        {
            RunState.SetGameplay(false);                  // <— NEW
            ApplyState(UiState.MainMenu, ensureTimeScale: true);
            SafeSetActive(player, false);
            if (menuRain) menuRain.gameObject.SetActive(true);
            if (gameplaySpawner) gameplaySpawner.StopAndClear();   // <— ensure no gameplay fruits remain
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
            RunState.SetGameplay(true);                   // <— NEW
            PauseManager.Instance?.ResumeForce();
            if (menuRain) menuRain.gameObject.SetActive(false);
            SafeSetActive(player, true);
            ApplyState(UiState.Hud);
        }

        void HandleGameOver()
        {
            RunState.SetGameplay(false);                  // <— NEW
            PauseManager.Instance?.ResumeForce();
            SafeSetActive(player, false);
            ApplyState(gameOverPanel ? UiState.GameOver : UiState.MainMenu);
            // optional: menu rain behind game over
            // if (menuRain) menuRain.gameObject.SetActive(true);
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

        public void OnRestart()
        {
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
            // Exit from pause/game over → go to menu, no lives should change
            RunState.SetGameplay(false);                  // <— NEW
            PauseManager.Instance?.ResumeForce();
            SafeSetActive(player, false);
            DifficultyManager.ClearCurrent();

            // Stop gameplay spawns & clear any gameplay fruits
            if (gameplaySpawner) gameplaySpawner.StopAndClear();

            // Turn menu rain on (decorative fruit only)
            if (menuRain) menuRain.gameObject.SetActive(true);

            ApplyState(UiState.MainMenu, ensureTimeScale: true);
        }

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
    }
}
