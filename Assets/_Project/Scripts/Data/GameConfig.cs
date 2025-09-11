using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Global knobs for gameplay/balance & debug.
    /// Store one or more instances as ScriptableObjects and assign in scene.
    /// </summary>
    [CreateAssetMenu(menuName = "CatchTheFruit/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Header("Core")]
        [Min(1)] public int startingLives = 3;
        [Tooltip("Half the playable width in world units. Fruits spawn & player clamps within [-arenaHalfWidth, +arenaHalfWidth].")]
        [Range(1f, 50f)] public float arenaHalfWidth = 2.7f;
        [Tooltip("World Y where fruits appear.")]
        public float spawnY = 6f;
        [Tooltip("World Y considered 'missed'.")]
        public float groundY = -4.5f;

        [Header("Player")]
        [Tooltip("Horizontal tracking speed toward finger/mouse X.")]
        [Range(1f, 30f)] public float playerMoveSpeed = 10f;

        [Header("Session Timer (optional)")]
        public bool useSessionTimer = false;
        [Range(5f, 600f)] public float sessionDuration = 60f;

        [Header("Debug")]
        public bool verboseLogs = true;
    }
}
/*
UNITY IMPLEMENTATION
1) Project → Create → CatchTheFruit → Game Config → name it "GameConfig_Default".
2) Set arenaHalfWidth (~2.7 for portrait, Cam size=5), spawnY (6), groundY (-4.5), lives, speed.
*/
