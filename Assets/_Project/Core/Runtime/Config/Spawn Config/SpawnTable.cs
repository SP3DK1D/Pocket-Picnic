using System.Collections.Generic;
using UnityEngine;
using URandom = UnityEngine.Random;

namespace CatchTheFruit
{
    /// <summary>
    /// Weighted picker for FruitData plus base spawn pacing.
    /// Used by FruitSpawner to choose which fruit to spawn and how fast to pace.
    ///
    /// Notes:
    /// - Entries can be left at 0 weight to effectively disable them.
    /// - See the custom editor (SpawnTableEditor.cs) for tools: normalize, sort, random test, etc.
    /// </summary>
    [CreateAssetMenu(menuName = "CatchTheFruit/Spawn Table")]
    public class SpawnTable : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            public FruitData data;

            // Float (not int) so decimals are supported in Inspector.
            [Min(0f)] public float weight = 1f;

#if UNITY_EDITOR
            // Editor-only quick notes for the designer (ignored at runtime).
            [TextArea(1, 3)] public string note;
#endif
        }

        [Header("Fruit List")]
        public List<Entry> entries = new();

        [Header("Pacing (defaults, can be overridden by Difficulty)")]
        [Min(0.05f)] public float initialInterval = 0.9f;
        [Min(0.05f)] public float minInterval = 0.35f;
        [Range(0.5f, 1f)] public float intervalDecay = 0.985f;

        [Header("Fall Speed")]
        [Min(0.1f)] public float fallSpeedMultiplier = 1f;

        /// <summary>Return a FruitData based on weighted random selection. Returns null if no valid entries.</summary>
        public FruitData Pick()
        {
            float total = TotalWeight();
            if (total <= 0f) return null;

            float roll = URandom.Range(0f, total);

            // Single pass walk
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null || e.data == null || e.weight <= 0f) continue;
                if (roll < e.weight) return e.data;
                roll -= e.weight;
            }

            // Fallback (shouldn't happen if totals were > 0)
            for (int i = 0; i < entries.Count; i++)
                if (entries[i]?.data != null && entries[i].weight > 0f) return entries[i].data;

            return null;
        }

        /// <summary>Total of all positive weights.</summary>
        public float TotalWeight()
        {
            if (entries == null || entries.Count == 0) return 0f;
            float total = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null || e.data == null) continue;
                if (e.weight > 0f) total += e.weight;
            }
            return total;
        }

#if UNITY_EDITOR
        // -------------------------- Editor helpers (safe at runtime) --------------------------

        /// <summary>Remove null rows & negative weights, clamp to 0..∞ (editor-time hygiene).</summary>
        public int Sanitize()
        {
            if (entries == null) return 0;
            int removed = entries.RemoveAll(e => e == null || e.data == null);
            for (int i = 0; i < entries.Count; i++)
                entries[i].weight = Mathf.Max(0f, entries[i].weight);
            return removed;
        }

        /// <summary>Normalize weights so they sum to target (e.g., 100). Keeps zeros intact.</summary>
        public void Normalize(float targetSum = 100f)
        {
            float sum = TotalWeight();
            if (sum <= 0f) return;

            float scale = targetSum / sum;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null || e.data == null) continue;
                if (e.weight <= 0f) continue;
                e.weight *= scale;
            }
        }

        /// <summary>Sort by weight (descending). Zeros move to the bottom.</summary>
        public void SortByWeightDesc()
        {
            entries.Sort((a, b) =>
            {
                float aw = (a == null || a.data == null) ? -1f : a.weight;
                float bw = (b == null || b.data == null) ? -1f : b.weight;
                return -aw.CompareTo(bw);
            });
        }

        /// <summary>Quick random sample for editor testing (returns FruitData id or "(none)").</summary>
        public string DebugPickId()
        {
            var fd = Pick();
            return fd ? fd.id : "(none)";
        }
#endif
    }
}
