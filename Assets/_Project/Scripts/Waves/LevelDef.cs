using UnityEngine;

namespace CatchTheFruit
{
    [CreateAssetMenu(fileName = "LevelDef", menuName = "CatchTheFruit/Level Def")]
    public class LevelDef : ScriptableObject
    {
        public string displayName = "Wave 1";
        [Min(3f)] public float durationSeconds = 20f;   // unscaled time
        public SpawnTable spawnTable;
        [Min(0.5f)] public float speedMultiplier = 1f;  // multiplies fall speed
        [Range(0.5f, 1.2f)] public float intervalMultiplier = 1f; // 0.9 = spawn faster
    }
}
