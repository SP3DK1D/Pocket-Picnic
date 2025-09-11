namespace CatchTheFruit
{
    /// <summary>
    /// Tiny State interface to keep GameStateMachine flexible but simple.
    /// </summary>
    public interface IGameState
    {
        void Enter();
        void Exit();
        void Tick();
    }
}
