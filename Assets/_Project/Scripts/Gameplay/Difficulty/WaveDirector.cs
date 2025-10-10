using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// No-op WaveDirector: disables all wave logic, mass spawns, blitzes, etc.
    /// Safe stub to satisfy scene references without affecting gameplay.
    /// </summary>
    public class WaveDirector : MonoBehaviour
    {
        void Awake()
        {
            // If any legacy scripts enabled this, just keep it alive but inert.
            enabled = false;
        }

        // Legacy API that other scripts might call; intentionally does nothing.
        public void StartWaves() { }
        public void StopWaves() { }
    }
}
