// Assets/_Project/Scripts/Data/DifficultyDef.cs
using UnityEngine;

namespace CatchTheFruit
{
    [CreateAssetMenu(fileName = "DifficultyDef", menuName = "CatchTheFruit/Difficulty")]
    public class DifficultyDef : ScriptableObject
    {
        [Header("Display")]
        public string displayName = "Easy";

        [Header("Fruit Physics (applied in Fruit.Init)")]
        [Min(0f)] public float gravityScale = 1.8f;
        [Min(0f)] public float maxFallSpeed = 9f;
        [Min(0f)] public float initialDownBoost = 0f;

        [Header("Spawner (applied on pick)")]
        // >1 = slower spawns (longer interval), <1 = faster
        [Range(0.5f, 1.5f)] public float spawnIntervalMul = 1.0f;
        // Multiplies SpawnTable.fallSpeedMultiplier passed into Fruit.Init
        [Range(0.5f, 2.0f)] public float speedMultiplier = 1.0f;
    }
}
