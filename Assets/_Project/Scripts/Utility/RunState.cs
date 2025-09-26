using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>Lightweight global flag for whether the run is actually in gameplay.</summary>
    public static class RunState
    {
        /// <summary>True only between GameStart and GameOver.</summary>
        public static bool InGameplay { get; private set; }

        public static void SetGameplay(bool on) => InGameplay = on;
    }
}
