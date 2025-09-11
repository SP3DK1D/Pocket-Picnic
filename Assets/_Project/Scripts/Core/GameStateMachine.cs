using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Minimal State Machine: Menu → Play → GameOver.
    /// UI drives visibility; FSM logs & hosts future logic (pause, etc.).
    /// </summary>
    public class GameStateMachine : MonoBehaviour
    {
        [SerializeField] private GameConfig config;

        private IGameState _current;
        private MenuState _menu;
        private PlayState _play;
        private GameOverState _over;

        private void Awake()
        {
            _menu = new MenuState(this, config);
            _play = new PlayState(this, config);
            _over = new GameOverState(this, config);
            ChangeState(_menu);
        }

        private void OnEnable()
        {
            GameEvents.OnGameStart += HandleStart;
            GameEvents.OnGameOver += HandleOver;
        }
        private void OnDisable()
        {
            GameEvents.OnGameStart -= HandleStart;
            GameEvents.OnGameOver -= HandleOver;
        }

        private void Update() => _current?.Tick();

        public void ChangeState(IGameState next)
        {
            _current?.Exit();
            _current = next;
            _current.Enter();
            if (config && config.verboseLogs) Debug.Log($"[FSM] -> {_current.GetType().Name}");
        }

        private void HandleStart() => ChangeState(_play);
        private void HandleOver() => ChangeState(_over);

        // Optional button hooks
        public void StartGame() => GameEvents.RaiseGameStart();
        public void BackToMenu() => ChangeState(_menu);
    }

    // --- Simple States (internal classes keep files tidy) ---

    internal class MenuState : IGameState
    {
        private readonly GameStateMachine _fsm; private readonly GameConfig _cfg;
        public MenuState(GameStateMachine fsm, GameConfig cfg) { _fsm = fsm; _cfg = cfg; }
        public void Enter() { if (_cfg && _cfg.verboseLogs) Debug.Log("[State] Menu Enter"); }
        public void Exit() { if (_cfg && _cfg.verboseLogs) Debug.Log("[State] Menu Exit"); }
        public void Tick() { }
    }

    internal class PlayState : IGameState
    {
        private readonly GameStateMachine _fsm; private readonly GameConfig _cfg;
        public PlayState(GameStateMachine fsm, GameConfig cfg) { _fsm = fsm; _cfg = cfg; }
        public void Enter() { if (_cfg && _cfg.verboseLogs) Debug.Log("[State] Play Enter"); }
        public void Exit() { if (_cfg && _cfg.verboseLogs) Debug.Log("[State] Play Exit"); }
        public void Tick() { }
    }

    internal class GameOverState : IGameState
    {
        private readonly GameStateMachine _fsm; private readonly GameConfig _cfg;
        public GameOverState(GameStateMachine fsm, GameConfig cfg) { _fsm = fsm; _cfg = cfg; }
        public void Enter() { if (_cfg && _cfg.verboseLogs) Debug.Log("[State] GameOver Enter"); }
        public void Exit() { if (_cfg && _cfg.verboseLogs) Debug.Log("[State] GameOver Exit"); }
        public void Tick() { }
    }
}
/*
UNITY IMPLEMENTATION
1) Create empty "_GameStateMachine" → add GameStateMachine → assign GameConfig_Default.
2) Optionally wire UI buttons to StartGame()/BackToMenu(), or just raise events from UIMenuController.
*/
