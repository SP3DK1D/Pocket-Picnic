// Assets/_Project/Scripts/Data/SpawnTable.cs
using System.Collections.Generic;
using UnityEngine;
using URandom = UnityEngine.Random;

namespace CatchTheFruit
{
    /// <summary>
    /// Weighted picker for FruitData plus base spawn pacing.
    /// Used by FruitSpawner to choose which fruit to spawn and how fast to pace.
    /// </summary>
    [CreateAssetMenu(menuName = "CatchTheFruit/Spawn Table")]
    public class SpawnTable : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            public FruitData data;
            [Min(0)] public int weight = 1;
        }

        [Header("Fruit List")]
        public List<Entry> entries = new();

        [Header("Pacing (defaults, can be overridden by Difficulty)")]
        [Min(0.05f)] public float initialInterval = 0.9f;
        [Min(0.05f)] public float minInterval     = 0.35f;
        [Range(0.5f, 1f)] public float intervalDecay = 0.985f;

        [Header("Fall Speed")]
        [Min(0.1f)] public float fallSpeedMultiplier = 1f;

        /// <summary>Return a FruitData based on weights. Returns null if no valid entries.</summary>
        public FruitData Pick()
        {
            if (entries == null || entries.Count == 0) return null;

            int total = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null || e.data == null || e.weight <= 0) continue;
                total += e.weight;
            }
            if (total <= 0) return null;

            int roll = URandom.Range(0, total);
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null || e.data == null || e.weight <= 0) continue;
                if (roll < e.weight) return e.data;
                roll -= e.weight;
            }
            // Fallback (shouldn't hit)
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i]?.data != null) return entries[i].data;
            }
            return null;
        }
    }
}
