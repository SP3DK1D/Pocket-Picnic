using UnityEngine;

namespace CatchTheFruit
{
    [CreateAssetMenu(menuName = "CatchTheFruit/Fruit Data")]
    public class FruitData : ScriptableObject
    {
        [Tooltip("Unique ID for logs/analytics.")]
        public string id = "Apple";

        [Header("Visuals")]
        public Sprite sprite;
        public Color tint = Color.white;

        [Header("Scoring & Fall")]
        public int scoreValue = 1;
        public float minFallSpeed = 2f;
        public float maxFallSpeed = 5f;

        [Header("Spawn Weight")]
        [Min(0f)] public float weight = 1f;

        [Header("Flags")]
        [Tooltip("If true: catching costs a life; missing does NOT.")]
        public bool isBomb = false;

        [Header("Optional Powerup")]
        [Tooltip("If assigned, catching this fruit triggers the powerup.")]
        public PowerupDef powerup;
    }
}
/*
UNITY:
For your freeze pickup: duplicate a FruitData (e.g., FD_Freeze), set score=0,
tint light blue, and assign Powerup = PU_Freeze. Keep isBomb=false.
*/
