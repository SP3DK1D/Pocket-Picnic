// Assets/_Project/Scripts/Systems/RunState.cs
using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Single source of truth for whether the game is currently in live gameplay.
    /// <para>
    /// True strictly between <see cref="GameEvents.RaiseGameStart"/> and
    /// <see cref="GameEvents.RaiseGameOver"/>. Menus, pauses, and game over
    /// screens set this to false.
    /// </para>
    /// </summary>
    public static class RunState
    {
        /// <summary>
        /// True only during an active run (from GameStart until GameOver).
        /// </summary>
        public static bool InGameplay { get; private set; }

        /// <summary>
        /// Set the run-state. Intended to be called by flow controllers
        /// (e.g., <see cref="MenuFlowController"/>) and lifecycle listeners.
        /// </summary>
        /// <param name="on">True to enter gameplay; false to exit.</param>
        public static void SetGameplay(bool on) => InGameplay = on;

        /// <summary>
        /// Hard reset to a known safe state (used on scene boot/domain reload).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => InGameplay = false;
    }
}
