using System.Collections.Generic;
using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Weighted random selection of FruitData, plus simple spawn pacing values.
    /// Keep it tiny (KISS).
    /// </summary>
    [CreateAssetMenu(menuName = "CatchTheFruit/Spawn Table")]
    public class SpawnTable : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            public FruitData fruit;
            [Min(0f)] public float weight = 1f; // 0 = never spawns
        }

        [Header("Entries (weighted)")]
        public List<Entry> entries = new List<Entry>();

        [Header("Spawn pacing")]
        [Min(0.05f)] public float initialInterval = 1.0f;  // first gap between spawns
        [Range(0.5f, 1f)] public float intervalDecay = 0.97f; // multiply interval each spawn
        [Min(0.05f)] public float minInterval = 0.35f;     // never go faster than this
        [Min(0.1f)] public float fallSpeedMultiplier = 1.0f; // global speed scaler

        // --- Internal cache for fast picks ---
        float _weightSum = -1f;

        void OnValidate()
        {
            if (intervalDecay > 1f) intervalDecay = 1f;
            if (intervalDecay < 0.5f) intervalDecay = 0.5f;
            RecalculateWeights();
        }

        void RecalculateWeights()
        {
            _weightSum = 0f;
            for (int i = 0; i < entries.Count; i++)
                _weightSum += Mathf.Max(0f, entries[i].weight);
        }

        /// <summary>Weighted random pick. Returns null if empty or all weights are 0.</summary>
        public FruitData Pick()
        {
            if (_weightSum < 0f) RecalculateWeights();
            if (_weightSum <= 0f || entries.Count == 0) return null;

            float r = Random.value * _weightSum;
            float accum = 0f;

            for (int i = 0; i < entries.Count; i++)
            {
                float w = Mathf.Max(0f, entries[i].weight);
                if (w <= 0f) continue;
                accum += w;
                if (r <= accum)
                    return entries[i].fruit;
            }

            // Fallback (shouldn’t happen): return first non-null with weight>0
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].fruit && entries[i].weight > 0f) return entries[i].fruit;

            return null;
        }
    }
}
/*
Unity (Step-by-step):
1) Project → Create → CatchTheFruit → Spawn Table → name it "SpawnTable_Default".
2) Open it and add entries:
   - Element 0: fruit = FD_Apple,  weight = 10
   - Element 1: fruit = FD_Banana, weight = 7
   - Element 2: fruit = FD_Golden, weight = 2
   - Element 3: fruit = FD_Bomb,   weight = 3
   - Element 4: fruit = FD_Freeze, weight = 1
   - Element 5: fruit = FD_Magnet, weight = 1
   - Element 6: fruit = FD_Clear,  weight = 1
   (Adjust weights to taste.)
3) Set pacing in the asset:
   - initialInterval = 1.0
   - intervalDecay   = 0.97
   - minInterval     = 0.35
   - fallSpeedMultiplier = 1.0 (or 1.1–1.3 if you want faster fall)
4) Select your **Spawner** GameObject and assign:
   - SpawnTable = SpawnTable_Default
   - Fruit Prefab = your Fruit prefab
   - GameConfig = your GameConfig asset
5) Press Play.
*/

